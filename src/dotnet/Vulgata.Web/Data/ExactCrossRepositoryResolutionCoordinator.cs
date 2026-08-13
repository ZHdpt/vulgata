using System.Text;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Web.Data;

public sealed class ExactCrossRepositoryResolutionCoordinator(
    ExactResolutionProfileCatalog profileCatalog,
    IUserContext userContext,
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository,
    IScanExecutionStore scanExecutionStore,
    IRepositoryAuthorizationVerifier authorizationVerifier,
    IExactGitRevisionSnapshotService snapshotService,
    ExactCandidateExtractors extractors,
    IExactCrossRepositoryResolver resolver,
    IExactCrossRepositoryResolutionStore store,
    VulgataDbContext context)
{
    private const int MaximumUnsupportedSourceBytes = 1_048_576;
    private const string CleanupFailureCode = "cleanup_failed";
    private readonly Func<ExactResolutionProfile> _loadProfile = profileCatalog.Load;

    internal ExactCrossRepositoryResolutionCoordinator(
        ExactResolutionProfileCatalog profileCatalog,
        IUserContext userContext,
        ISystemRepository systemRepository,
        IRepositoryRepository repositoryRepository,
        IScanExecutionStore scanExecutionStore,
        IRepositoryAuthorizationVerifier authorizationVerifier,
        IExactGitRevisionSnapshotService snapshotService,
        ExactCandidateExtractors extractors,
        IExactCrossRepositoryResolver resolver,
        IExactCrossRepositoryResolutionStore store,
        VulgataDbContext context,
        Func<ExactResolutionProfile> loadProfile)
        : this(
            profileCatalog,
            userContext,
            systemRepository,
            repositoryRepository,
            scanExecutionStore,
            authorizationVerifier,
            snapshotService,
            extractors,
            resolver,
            store,
            context)
    {
        _loadProfile = loadProfile ?? throw new ArgumentNullException(nameof(loadProfile));
    }

    public async Task<ExactResolutionOutcome> ResolveAfterPass1Async(
        ExactResolutionRuntimeRequest request,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ResolveCoreAsync(request, scope, cancellationToken);
        }
        catch (InvalidOperationException exception) when (
            StringComparer.Ordinal.Equals(exception.Message, CleanupFailureCode))
        {
            return new(ExactResolutionOutcomeKind.Failed, CleanupFailureCode);
        }
    }

    private async Task<ExactResolutionOutcome> ResolveCoreAsync(
        ExactResolutionRuntimeRequest request,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(scope);
        if (cancellationToken.IsCancellationRequested)
        {
            return new(ExactResolutionOutcomeKind.Cancelled, "cancelled");
        }

        ExactResolutionProfile profile = _loadProfile();
        if (request.Bindings.Count != 2 ||
            !StringComparer.Ordinal.Equals(request.ProfileId, profile.ProfileId) ||
            !StringComparer.Ordinal.Equals(request.ProfileVersion, profile.ProfileVersion) ||
            !StringComparer.Ordinal.Equals(request.ProfileFingerprint, profile.ProfileFingerprint))
        {
            return Refused();
        }

        Guid[] repositoryIds =
        [
            .. request.Bindings.Select(binding => binding.RepositoryId).Order(),
                ];
        if (repositoryIds.Length != 2 ||
            repositoryIds.Distinct().Count() != 2 ||
            repositoryIds.Any(repositoryId => !scope.Contains(repositoryId)) ||
            !await authorizationVerifier.HasCurrentAccessAsync(repositoryIds, cancellationToken))
        {
            return Refused();
        }

        SystemOwnerFilter filter = userContext.ToSystemOwnerFilter();
        if (string.IsNullOrWhiteSpace(userContext.UserId) ||
            await systemRepository.GetVisibleByIdAsync(
                request.SystemId,
                filter,
                cancellationToken) is null)
        {
            return Refused();
        }

        var repositories = new Dictionary<string, RepositoryEntity>(StringComparer.Ordinal);
        var executions = new Dictionary<string, ScanExecution>(StringComparer.Ordinal);
        foreach (ExactResolutionRuntimeBinding binding in request.Bindings)
        {
            RepositoryEntity? repository = await repositoryRepository.GetBySystemAndIdAsync(
                request.SystemId,
                binding.RepositoryId,
                cancellationToken);
            ScanExecution? execution = await scanExecutionStore.GetAsync(
                binding.ScanExecutionId,
                cancellationToken);
            if (repository is null ||
                execution is null ||
                execution.RepositoryId != binding.RepositoryId ||
                execution.SystemId != request.SystemId ||
                execution.ScanRunId != binding.ScanRunId)
            {
                return Refused();
            }

            repositories.Add(binding.Alias, repository);
            executions.Add(binding.Alias, execution);
        }

        if (executions.Values.Any(execution =>
                execution.Phase is not (ScanExecutionPhase.Pass1Completed or
                    ScanExecutionPhase.Completed)))
        {
            return new(ExactResolutionOutcomeKind.WaitingForPass1);
        }

        foreach (ExactResolutionRuntimeBinding binding in request.Bindings)
        {
            ProfileRepository expected = profile.Repositories.Single(repository =>
                StringComparer.Ordinal.Equals(repository.Alias, binding.Alias));
            if (!StringComparer.Ordinal.Equals(
                    executions[binding.Alias].ProfiledRevision,
                    expected.ExpectedCommit))
            {
                return Refused();
            }
        }

        var leases = new Dictionary<string, ICommittedRepositorySnapshotLease>(
            StringComparer.Ordinal);
        try
        {
            foreach (ProfileRepository expected in profile.Repositories)
            {
                ExactResolutionRuntimeBinding binding = request.Bindings.Single(value =>
                    StringComparer.Ordinal.Equals(value.Alias, expected.Alias));
                ICommittedRepositorySnapshotLease lease = await snapshotService.AcquireAsync(
                    repositories[expected.Alias],
                    binding.ScanRunId,
                    expected.ExpectedRef,
                    expected.ExpectedCommit,
                    cancellationToken);
                leases.Add(expected.Alias, lease);
                if (lease.RepositoryId != binding.RepositoryId ||
                    lease.ScanRunId != binding.ScanRunId ||
                    !StringComparer.Ordinal.Equals(
                        lease.ProfiledRevision,
                        expected.ExpectedCommit))
                {
                    return Refused();
                }
            }

            var consumers = new List<ConsumerDispatchCandidate>();
            var providers = new List<ProviderEndpointCandidate>();
            ExactResolutionNonExactException? extractionFailure = null;
            foreach (ResolutionOccurrence occurrence in profile.Occurrences)
            {
                try
                {
                    consumers.AddRange(await extractors.ExtractConsumersAsync(
                        leases[occurrence.ConsumerAlias],
                        occurrence,
                        cancellationToken));
                }
                catch (ExactResolutionNonExactException exception)
                {
                    if (extractionFailure is not null)
                    {
                        return Refused();
                    }

                    extractionFailure = exception;
                }

                try
                {
                    providers.AddRange(await extractors.ExtractProvidersAsync(
                        leases[occurrence.ProviderAlias],
                        occurrence,
                        cancellationToken));
                }
                catch (ExactResolutionNonExactException exception)
                {
                    if (extractionFailure is not null)
                    {
                        return Refused();
                    }

                    extractionFailure = exception;
                }
            }

            IReadOnlyList<ExactResolutionDecision> unsupported =
                await MapUnsupportedAsync(profile, leases, cancellationToken);
            if (unsupported.Count != profile.UnsupportedObservations.Count)
            {
                return Refused();
            }

            ExactResolutionRuntimeBindingEvidence[] evidence =
                BuildBindingEvidence(
                    request,
                    profile,
                    leases,
                    executions,
                    consumers,
                    providers,
                    extractionFailure);
            _ = ExactResolutionProfileCatalog.BindForValidation(
                request.SystemId,
                profile,
                request.ProfileId,
                request.ProfileVersion,
                request.ProfileFingerprint,
                request.Bindings,
                evidence);

            ExactResolutionDecisionBatch decisions;
            if (extractionFailure is not null)
            {
                if (!TryCreateUncertaintyBatch(
                        profile,
                        consumers,
                        unsupported,
                        extractionFailure,
                        out decisions))
                {
                    return Refused();
                }
            }
            else
            {
                try
                {
                    decisions = await resolver.ResolveAsync(
                        profile,
                        consumers,
                        providers,
                        unsupported,
                        cancellationToken);
                }
                catch (ExactResolutionNonExactException exception)
                {
                    if (!TryCreateUncertaintyBatch(
                            profile,
                            consumers,
                            unsupported,
                            exception,
                            out decisions))
                    {
                        return Refused();
                    }
                }
            }

            var liveScope = new RepositoryAccessScope(repositoryIds, authorizationVerifier);
            ExactResolutionBatchOutcome outcome = await store.ApplyAsync(
                decisions,
                liveScope,
                cancellationToken);
            return new(outcome.Kind, outcome.Code);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(ExactResolutionOutcomeKind.Cancelled, "cancelled");
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or InvalidOperationException)
        {
            return Refused();
        }
        catch (Exception)
        {
            return new(ExactResolutionOutcomeKind.Failed, "failed");
        }
        finally
        {
            bool cleanupFailed = false;
            foreach (ICommittedRepositorySnapshotLease lease in leases.Values.Reverse())
            {
                try
                {
                    await lease.DisposeAsync();
                }
                catch (Exception)
                {
                    cleanupFailed = true;
                }
            }

            if (cleanupFailed)
            {
                throw new InvalidOperationException(CleanupFailureCode);
            }
        }
    }

    private async Task<IReadOnlyList<ExactResolutionDecision>> MapUnsupportedAsync(
        ExactResolutionProfile profile,
        IReadOnlyDictionary<string, ICommittedRepositorySnapshotLease> leases,
        CancellationToken cancellationToken)
    {
        var decisions = new List<ExactResolutionDecision>(
            profile.UnsupportedObservations.Count);
        foreach (UnsupportedObservation observation in profile.UnsupportedObservations)
        {
            ICommittedRepositorySnapshotLease lease = leases[observation.OwnerAlias];
            await lease.VerifyIdentityAsync(cancellationToken);
            if (!await HasConfiguredUnsupportedInvocationAsync(
                    lease.RootPath,
                    observation,
                    cancellationToken))
            {
                continue;
            }

            ExactResolutionOwner[] owners = await (
                from logicalDocument in context.LogicalDocuments.AsNoTracking()
                join documentVersion in context.DocumentVersions.AsNoTracking()
                    on logicalDocument.CurrentVersionId equals documentVersion.Id
                join sourceAnchor in context.SourceAnchors.AsNoTracking()
                    on documentVersion.Id equals sourceAnchor.DocumentVersionId
                join codeUnit in context.CodeUnits.AsNoTracking()
                    on sourceAnchor.CodeUnitId equals codeUnit.Id
                where logicalDocument.DocumentType == DocumentType.CodeLogic &&
                    documentVersion.RepositoryId == lease.RepositoryId &&
                    documentVersion.ScanRunId == lease.ScanRunId &&
                    documentVersion.ProfiledRevision == lease.ProfiledRevision &&
                    sourceAnchor.RepositoryId == lease.RepositoryId &&
                    sourceAnchor.ScanRunId == lease.ScanRunId &&
                    sourceAnchor.ProfiledRevision == lease.ProfiledRevision &&
                    sourceAnchor.RepositoryRelativePath ==
                        observation.OwnerDocument.RepositoryRelativePath &&
                    sourceAnchor.LineStart <= observation.Selector.Line &&
                    sourceAnchor.LineEnd >= observation.Selector.Line &&
                    codeUnit.RepositoryId == lease.RepositoryId &&
                    codeUnit.ScanRunId == lease.ScanRunId &&
                    codeUnit.FilePath == observation.OwnerDocument.RepositoryRelativePath &&
                    codeUnit.SymbolName == observation.OwnerDocument.Symbol
                select new ExactResolutionOwner(
                    documentVersion.RepositoryId,
                    documentVersion.ScanRunId,
                    documentVersion.Id,
                    sourceAnchor.Id,
                    documentVersion.ProfiledRevision))
                .Take(2)
                .ToArrayAsync(cancellationToken);
            if (owners.Length == 1)
            {
                decisions.Add(new(
                    observation.ObservationId,
                    ExactResolutionDecisionKind.UnsupportedObservation,
                    owners[0],
                    null,
                    "unsupported_kind",
                    observation.EvidenceLocator));
            }
        }

        return decisions.AsReadOnly();
    }

    private static ExactResolutionRuntimeBindingEvidence[] BuildBindingEvidence(
        ExactResolutionRuntimeRequest request,
        ExactResolutionProfile profile,
        IReadOnlyDictionary<string, ICommittedRepositorySnapshotLease> leases,
        IReadOnlyDictionary<string, ScanExecution> executions,
        IReadOnlyList<ConsumerDispatchCandidate> consumers,
        IReadOnlyList<ProviderEndpointCandidate> providers,
        ExactResolutionNonExactException? extractionFailure)
    {
        return
        [
            .. request.Bindings.Select(binding =>
            {
                ProfileRepository expected = profile.Repositories.Single(repository =>
                    StringComparer.Ordinal.Equals(repository.Alias, binding.Alias));
                ExactResolutionOwner[] owners =
                [
                    .. profile.Occurrences
                    .Where(occurrence => StringComparer.Ordinal.Equals(
                        occurrence.ConsumerAlias,
                        binding.Alias))
                    .SelectMany(occurrence => consumers.Where(candidate =>
                        StringComparer.Ordinal.Equals(
                            candidate.OccurrenceId,
                            occurrence.OccurrenceId)))
                    .Select(candidate => candidate.Owner)
                    .Concat(profile.Occurrences
                        .Where(occurrence => StringComparer.Ordinal.Equals(
                            occurrence.ProviderAlias,
                            binding.Alias))
                        .SelectMany(occurrence => providers.Where(candidate =>
                            StringComparer.Ordinal.Equals(
                                candidate.OccurrenceId,
                                occurrence.OccurrenceId)))
                        .Select(candidate => candidate.Owner))
                    .Concat(ExtractionOwners(
                        profile,
                        binding.Alias,
                        extractionFailure))
                    .Distinct()
                    .Take(2),
                ];
                if (owners.Length != 1)
                {
                                    throw new InvalidOperationException(
                                        "Runtime binding requires one current mapped owner.");
                                }

                ExactResolutionOwner owner = owners[0];
                return new ExactResolutionRuntimeBindingEvidence(
                    binding.Alias,
                    binding.StableGitIdentity,
                    binding.RepositoryId,
                    request.SystemId,
                    binding.ScanRunId,
                    binding.ScanExecutionId,
                    leases[binding.Alias].ProfiledRevision,
                    executions[binding.Alias].ProfiledRevision,
                    executions[binding.Alias].ProfiledRevision,
                    owner.ProfiledRevision,
                    owner.ProfiledRevision);
            }),
        ];
    }

    private static IEnumerable<ExactResolutionOwner> ExtractionOwners(
        ExactResolutionProfile profile,
        string alias,
        ExactResolutionNonExactException? failure)
    {
        if (failure?.EvidenceSource is null || failure.Owner is null)
        {
            return [];
        }

        ResolutionOccurrence? occurrence = profile.Occurrences.SingleOrDefault(value =>
            StringComparer.Ordinal.Equals(value.OccurrenceId, failure.OccurrenceId));
        if (occurrence is null)
        {
            return [];
        }

        string failureAlias = failure.EvidenceSource == ExactResolutionNonExactSource.Consumer
            ? occurrence.ConsumerAlias
            : occurrence.ProviderAlias;
        return StringComparer.Ordinal.Equals(failureAlias, alias)
            ? [failure.Owner]
            : [];
    }

    private static async Task<bool> HasConfiguredUnsupportedInvocationAsync(
        string rootPath,
        UnsupportedObservation observation,
        CancellationToken cancellationToken)
    {
        string root = Path.GetFullPath(rootPath);
        string fullPath = Path.GetFullPath(Path.Combine(
            root,
            observation.OwnerDocument.RepositoryRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar)));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(prefix, comparison) ||
            !File.Exists(fullPath) ||
            (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
        {
            return false;
        }

        var info = new FileInfo(fullPath);
        if (info.Length > MaximumUnsupportedSourceBytes)
        {
            return false;
        }

        string[] lines = await File.ReadAllLinesAsync(
            fullPath,
            new UTF8Encoding(false, true),
            cancellationToken);
        if (observation.Selector.Line > lines.Length)
        {
            return false;
        }

        string line = lines[observation.Selector.Line - 1];
        string expected = observation.Selector.ReceiverSymbol + "." +
            observation.Selector.MemberName;
        int count = 0;
        for (int index = 0; index <= line.Length - expected.Length; index++)
        {
            if (!line.AsSpan(index).StartsWith(expected, StringComparison.Ordinal))
            {
                continue;
            }
            if (index > 0 && IsIdentifierCharacter(line[index - 1]))
            {
                continue;
            }

            int after = index + expected.Length;
            while (after < line.Length && char.IsWhiteSpace(line[after]))
            {
                after++;
            }

            if (after < line.Length && line[after] == '(' &&
                count++ == observation.Selector.OccurrenceOrdinalOnLine)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIdentifierCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '_' or '$';

    private static bool TryCreateUncertaintyBatch(
        ExactResolutionProfile profile,
        IReadOnlyList<ConsumerDispatchCandidate> consumers,
        IReadOnlyList<ExactResolutionDecision> unsupported,
        ExactResolutionNonExactException exception,
        out ExactResolutionDecisionBatch decisions)
    {
        decisions = null!;
        if (!IsEligibleNonExactReason(exception.ReasonCode))
        {
            return false;
        }

        ResolutionOccurrence? occurrence = profile.Occurrences.SingleOrDefault(value =>
            StringComparer.Ordinal.Equals(value.OccurrenceId, exception.OccurrenceId));
        if (occurrence is null)
        {
            return false;
        }

        ProfileRepository consumerRepository = profile.Repositories.Single(repository =>
            StringComparer.Ordinal.Equals(repository.Alias, occurrence.ConsumerAlias));
        ConsumerDispatchCandidate[] owners =
        [
            .. consumers.Where(candidate =>
                StringComparer.Ordinal.Equals(
                    candidate.OccurrenceId,
                    occurrence.OccurrenceId) &&
                StringComparer.Ordinal.Equals(
                    candidate.Owner.ProfiledRevision,
                    consumerRepository.ExpectedCommit)),
        ];
        ExactResolutionOwner owner;
        string evidenceLocator;
        if (exception.EvidenceSource == ExactResolutionNonExactSource.Consumer)
        {
            if (exception.Owner is null ||
                string.IsNullOrWhiteSpace(exception.EvidenceLocator) ||
                !StringComparer.Ordinal.Equals(
                    exception.Owner.ProfiledRevision,
                    consumerRepository.ExpectedCommit))
            {
                return false;
            }

            owner = exception.Owner;
            evidenceLocator = exception.EvidenceLocator;
        }
        else if (owners.Length == 1)
        {
            owner = owners[0].Owner;
            evidenceLocator = owners[0].InvocationEvidenceLocator;
        }
        else
        {
            return false;
        }

        var uncertainty = new ExactResolutionDecision(
            occurrence.OccurrenceId,
            ExactResolutionDecisionKind.Uncertainty,
            owner,
            null,
            exception.ReasonCode,
            evidenceLocator);
        decisions = ExactResolutionDecisionBatch.Create(
            profile,
            unsupported.Append(uncertainty)
                .OrderBy(decision => decision.Identity, StringComparer.Ordinal));
        return true;
    }

    private static bool IsEligibleNonExactReason(string reasonCode) => reasonCode is
        "variable_target" or
        "disallowed_invocation" or
        "malformed_provider" or
        "provider_out_of_root" or
        "unmatched" or
        "multiple_providers" or
        "normalization_mismatch";

    private static ExactResolutionOutcome Refused() =>
        new(ExactResolutionOutcomeKind.Refused, "refused");
}

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Infrastructure.CrossRepository;

public sealed record CurrentCommunicationCandidateSet
{
    internal CurrentCommunicationCandidateSet(
        IEnumerable<ConsumerDispatchCandidate> consumers,
        IEnumerable<ProviderEndpointCandidate> providers)
    {
        Consumers = Array.AsReadOnly(
        [
            .. consumers.OrderBy(candidate => candidate.OccurrenceId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Owner.DocumentVersionId)
                .ThenBy(candidate => candidate.InvocationEvidenceLocator, StringComparer.Ordinal),
        ]);
        Providers = Array.AsReadOnly(
        [
            .. providers.OrderBy(candidate => candidate.OccurrenceId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Owner.DocumentVersionId)
                .ThenBy(candidate => candidate.DeclarationEvidenceLocator, StringComparer.Ordinal),
        ]);
    }

    public IReadOnlyList<ConsumerDispatchCandidate> Consumers { get; }
    public IReadOnlyList<ProviderEndpointCandidate> Providers { get; }
}

public sealed class CurrentCommunicationCandidateSource(VulgataDbContext context)
{
    private readonly VulgataDbContext _context = context;

    public async Task<CurrentCommunicationCandidateSet> ReconstructAsync(
        ExactResolutionRuntimeRequest request,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        if (!StringComparer.Ordinal.Equals(request.ProfileId, profile.ProfileId) ||
            !StringComparer.Ordinal.Equals(request.ProfileVersion, profile.ProfileVersion) ||
            !StringComparer.Ordinal.Equals(request.ProfileFingerprint, profile.ProfileFingerprint) ||
            request.Bindings.Count != 2)
        {
            throw new InvalidDataException("The terminal pair does not match the current exact profile.");
        }

        Guid[] repositoryIds =
        [
            .. request.Bindings.Select(binding => binding.RepositoryId).Distinct().Order(),
        ];
        if (repositoryIds.Length != 2 ||
            repositoryIds.Any(repositoryId => !scope.Contains(repositoryId)) ||
            !await scope.HasCurrentAccessAsync(repositoryIds, cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Current communication candidates require current access to the terminal pair.");
        }

        var documentsByAlias = new Dictionary<string, CurrentDocumentEvidence>(
            StringComparer.Ordinal);
        foreach (ExactResolutionRuntimeBinding binding in request.Bindings)
        {
            ProfileRepository expected = profile.Repositories.Single(repository =>
                StringComparer.Ordinal.Equals(repository.Alias, binding.Alias));
            if (!StringComparer.Ordinal.Equals(binding.StableGitIdentity, expected.StableGitIdentity))
            {
                throw new InvalidDataException("The terminal pair Repository identity has changed.");
            }

            PreScanResult[] preScans = await _context.PreScanResults.AsNoTracking()
                .Include(result => result.Classifications)
                .Where(result =>
                    result.RepositoryId == binding.RepositoryId &&
                    result.ScanRunId == binding.ScanRunId &&
                    result.State == PreScanApprovalState.PreScanApproved &&
                    result.ProfiledRevision == expected.ExpectedCommit)
                .Take(2)
                .ToArrayAsync(cancellationToken);
            if (preScans.Length != 1 || preScans[0].EvidenceTruncated)
            {
                throw new InvalidDataException("The terminal pair has no unique complete Pre-Scan evidence.");
            }

            CurrentDocumentEvidence[] documents = await (
                from logicalDocument in _context.LogicalDocuments.AsNoTracking()
                join documentVersion in _context.DocumentVersions.AsNoTracking()
                    on logicalDocument.CurrentVersionId equals documentVersion.Id
                where logicalDocument.DocumentType == DocumentType.CodeLogic &&
                    documentVersion.RepositoryId == binding.RepositoryId &&
                    documentVersion.SystemId == request.SystemId &&
                    documentVersion.ScanRunId == binding.ScanRunId &&
                    documentVersion.ProfiledRevision == expected.ExpectedCommit &&
                    documentVersion.SupersededById == null
                select new CurrentDocumentEvidence(documentVersion, preScans[0].Classifications))
                .ToArrayAsync(cancellationToken);
            if (documents.Length == 0)
            {
                throw new InvalidDataException("The terminal pair has no current Code Logic Document.");
            }

            documentsByAlias.Add(binding.Alias, SelectCurrentDocument(profile, binding.Alias, documents));
        }

        var consumers = new List<ConsumerDispatchCandidate>(profile.Occurrences.Count);
        var providers = new List<ProviderEndpointCandidate>(profile.Occurrences.Count);
        foreach (ResolutionOccurrence occurrence in profile.Occurrences)
        {
            CurrentDocumentEvidence consumerDocument = documentsByAlias[occurrence.ConsumerAlias];
            CurrentDocumentEvidence providerDocument = documentsByAlias[occurrence.ProviderAlias];
            SourceAnchor consumerAnchor = await RequireAnchorAsync(
                consumerDocument,
                occurrence.Protocol,
                cancellationToken);
            SourceAnchor providerAnchor = await RequireAnchorAsync(
                providerDocument,
                occurrence.Protocol,
                cancellationToken);
            string normalized = ProviderIdentifier(occurrence);
            consumers.Add(new(
                occurrence.OccurrenceId,
                Owner(consumerDocument.Document, consumerAnchor),
                occurrence.Consumer.Invocation.Form,
                occurrence.Consumer.Invocation.ReceiverSymbol,
                occurrence.Consumer.Invocation.MemberName,
                occurrence.Consumer.Invocation.EndpointArgumentIndex,
                occurrence.Consumer.Invocation.Line,
                occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine,
                occurrence.Normalization.ConsumerRequiredPrefix + normalized,
                normalized,
                consumerAnchor.EvidenceLocator));
            providers.Add(new(
                occurrence.OccurrenceId,
                Owner(providerDocument.Document, providerAnchor),
                occurrence.Normalization.ProviderRequiredPrefix + normalized,
                normalized,
                providerAnchor.EvidenceLocator));
        }

        if (!await scope.HasCurrentAccessAsync(repositoryIds, cancellationToken))
        {
            throw new UnauthorizedAccessException("Repository access changed during reconstruction.");
        }

        return new(consumers, providers);
    }

    private static CurrentDocumentEvidence SelectCurrentDocument(
        ExactResolutionProfile profile,
        string alias,
        IReadOnlyList<CurrentDocumentEvidence> documents)
    {
        HashSet<string> expectedPaths = profile.Occurrences
            .Where(occurrence => StringComparer.Ordinal.Equals(occurrence.ConsumerAlias, alias))
            .SelectMany(occurrence => occurrence.Consumer.Files)
            .Concat(profile.Occurrences
                .Where(occurrence => StringComparer.Ordinal.Equals(occurrence.ProviderAlias, alias))
                .Select(occurrence => occurrence.Provider.ImplementationDocument.RepositoryRelativePath))
            .ToHashSet(StringComparer.Ordinal);
        CurrentDocumentEvidence[] matches =
        [
            .. documents.Where(document => expectedPaths.Contains(
                document.Document.RepositoryRelativePath)),
        ];
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException("The configured current Code Logic Document is ambiguous.");
    }

    private async Task<SourceAnchor> RequireAnchorAsync(
        CurrentDocumentEvidence evidence,
        ExactResolutionProtocol protocol,
        CancellationToken cancellationToken)
    {
        CommunicationKind requiredKind = protocol == ExactResolutionProtocol.CustomRpc
            ? CommunicationKind.Rpc
            : throw new InvalidDataException("The exact protocol is not supported.");
        CommunicationKindClassification[] classifications =
        [
            .. evidence.Classifications.Where(classification =>
                classification.Kind == requiredKind &&
                classification.Level == ClassificationLevel.Confirmed &&
                classification.Confidence == ConfidenceBand.High &&
                StringComparer.Ordinal.Equals(
                    classification.RelativePath,
                    evidence.Document.RepositoryRelativePath) &&
                !string.IsNullOrWhiteSpace(classification.CatalogRuleIdentity)),
        ];
        if (classifications.Length != 1)
        {
            throw new InvalidDataException("The current Document has no unique confirmed catalog evidence.");
        }

        SourceAnchor[] anchors = await _context.SourceAnchors.AsNoTracking()
            .Where(anchor =>
                anchor.DocumentVersionId == evidence.Document.Id &&
                anchor.RepositoryId == evidence.Document.RepositoryId &&
                anchor.ScanRunId == evidence.Document.ScanRunId &&
                anchor.ProfiledRevision == evidence.Document.ProfiledRevision &&
                anchor.RepositoryRelativePath == evidence.Document.RepositoryRelativePath &&
                anchor.EvidenceLocator == classifications[0].Locator)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        return anchors.Length == 1
            ? anchors[0]
            : throw new InvalidDataException(
                "The confirmed catalog evidence has no unique current Source Anchor.");
    }

    private static ExactResolutionOwner Owner(DocumentVersion document, SourceAnchor anchor) =>
        new(document.RepositoryId, document.ScanRunId, document.Id, anchor.Id, document.ProfiledRevision);

    private static string ProviderIdentifier(ResolutionOccurrence occurrence)
    {
        if (occurrence.Provider.Files.Count != 1)
        {
            throw new InvalidDataException("The exact provider declaration is ambiguous.");
        }

        string file = occurrence.Provider.Files[0];
        string prefix = occurrence.Provider.Root + '/';
        if (!file.StartsWith(prefix, StringComparison.Ordinal) ||
            !file.EndsWith(".xml", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The exact provider declaration is outside the closed catalog root.");
        }

        string identifier = file[prefix.Length..^4];
        _ = ExactEndpointNormalizer.Normalize(
            occurrence.Normalization.ProviderRequiredPrefix + identifier,
            occurrence.Normalization.ProviderRequiredPrefix);
        return identifier;
    }

    private sealed record CurrentDocumentEvidence(
        DocumentVersion Document,
        IReadOnlyList<CommunicationKindClassification> Classifications);
}

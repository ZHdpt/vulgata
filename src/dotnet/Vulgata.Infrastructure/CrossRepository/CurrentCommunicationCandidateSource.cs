using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CommunicationDetection;
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

public sealed class CurrentCommunicationCandidateSource(
    VulgataDbContext context,
    IRevisionBoundSourceReader? sourceReader = null)
{
    private readonly VulgataDbContext _context = context;
    private readonly IRevisionBoundSourceReader? _sourceReader = sourceReader;

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

        PairAuthority pairAuthority = await LoadPairAuthorityAsync(
            request,
            profile,
            cancellationToken);
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

            PreparedScanManifest manifest = pairAuthority.Manifests[binding.Alias];
            if (!preScans[0].IsPreparedV2 ||
                preScans[0].PreparedScanManifestId != manifest.Id ||
                !StringComparer.Ordinal.Equals(
                    preScans[0].PreparedScanManifestFingerprint,
                    manifest.Fingerprint) ||
                !StringComparer.Ordinal.Equals(
                    preScans[0].PairPackageFingerprint,
                    pairAuthority.Pair.PairPackageFingerprint))
            {
                throw new InvalidDataException("The Pre-Scan evidence is not bound to the terminal prepared pair.");
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
            string consumerRawIdentifier = ValidateDocumentEvidence(
                consumerDocument,
                consumerAnchor,
                pairAuthority.Manifests[occurrence.ConsumerAlias],
                pairAuthority.Pair,
                occurrence.Protocol);
            _ = ValidateDocumentEvidence(
                providerDocument,
                providerAnchor,
                pairAuthority.Manifests[occurrence.ProviderAlias],
                pairAuthority.Pair,
                occurrence.Protocol);
            await VerifyRevisionBoundSourceAsync(
                consumerDocument.Document,
                consumerAnchor,
                pairAuthority,
                occurrence.ConsumerAlias,
                cancellationToken);
            await VerifyRevisionBoundSourceAsync(
                providerDocument.Document,
                providerAnchor,
                pairAuthority,
                occurrence.ProviderAlias,
                cancellationToken);
            string providerRawIdentifier = await ReproduceProviderIdentifierAsync(
                occurrence,
                providerDocument.Document,
                providerAnchor,
                pairAuthority,
                cancellationToken);
            string normalizedConsumer = ExactEndpointNormalizer.Normalize(
                consumerRawIdentifier,
                occurrence.Normalization.ConsumerRequiredPrefix);
            string normalizedProvider = ExactEndpointNormalizer.Normalize(
                providerRawIdentifier,
                occurrence.Normalization.ProviderRequiredPrefix);
            CodeUnit consumerCodeUnit = await RequireConsumerCodeUnitAsync(
                consumerDocument.Document,
                consumerAnchor,
                cancellationToken);
            string consumerEntryStableKey = ConsumerEntryStableKey(
                consumerDocument.Document.RepositoryId,
                consumerDocument.Document.RepositoryRelativePath,
                consumerCodeUnit.SymbolName,
                occurrence.Protocol,
                occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine);
            CommunicationKindClassification providerClassification =
                providerDocument.Classifications.Single(value =>
                    StringComparer.Ordinal.Equals(
                        value.Locator, providerAnchor.EvidenceLocator));
            consumers.Add(new(
                occurrence.OccurrenceId,
                Owner(consumerDocument.Document, consumerAnchor),
                occurrence.Consumer.Invocation.Form,
                occurrence.Consumer.Invocation.ReceiverSymbol,
                occurrence.Consumer.Invocation.MemberName,
                occurrence.Consumer.Invocation.EndpointArgumentIndex,
                occurrence.Consumer.Invocation.Line,
                occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine,
                consumerRawIdentifier,
                normalizedConsumer,
                consumerAnchor.EvidenceLocator,
                consumerEntryStableKey));
            providers.Add(new(
                occurrence.OccurrenceId,
                Owner(providerDocument.Document, providerAnchor),
                providerRawIdentifier,
                normalizedProvider,
                providerAnchor.EvidenceLocator,
                providerClassification.Id,
                providerClassification.Fingerprint,
                providerClassification.CatalogRuleIdentity,
                occurrence.Normalization.Id));
        }

        if (!await scope.HasCurrentAccessAsync(repositoryIds, cancellationToken))
        {
            throw new UnauthorizedAccessException("Repository access changed during reconstruction.");
        }

        return new(consumers, providers);
    }

    private async Task<CodeUnit> RequireConsumerCodeUnitAsync(
        DocumentVersion document,
        SourceAnchor anchor,
        CancellationToken cancellationToken)
    {
        CodeUnit[] codeUnits = await _context.CodeUnits.AsNoTracking()
            .Where(value =>
                value.Id == anchor.CodeUnitId &&
                value.RepositoryId == document.RepositoryId &&
                value.ScanRunId == document.ScanRunId &&
                value.FilePath == document.RepositoryRelativePath &&
                value.LineStart <= anchor.LineStart &&
                value.LineEnd >= anchor.LineEnd)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        return codeUnits.Length == 1 && !string.IsNullOrWhiteSpace(codeUnits[0].SymbolName)
            ? codeUnits[0]
            : throw new InvalidDataException(
                "The consumer Source Anchor has no unique containing semantic owner.");
    }

    private static string ConsumerEntryStableKey(
        Guid repositoryId,
        string repositoryRelativePath,
        string containingSemanticOwner,
        ExactResolutionProtocol siteKind,
        int structuralOrdinal)
    {
        string normalizedPath = repositoryRelativePath.Replace('\\', '/').TrimStart('/')
            .Normalize(NormalizationForm.FormC);
        string normalizedOwner = containingSemanticOwner.Trim().Normalize(NormalizationForm.FormC);
        string preimage = string.Join(
            '\n',
            "consumer-entry/v1",
            repositoryId,
            normalizedPath,
            normalizedOwner,
            siteKind,
            structuralOrdinal);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(preimage)));
    }

    private async Task<PairAuthority> LoadPairAuthorityAsync(
        ExactResolutionRuntimeRequest request,
        ExactResolutionProfile profile,
        CancellationToken cancellationToken)
    {
        Pass1PairExecution[] pairs = await _context.Pass1PairExecutions.AsNoTracking()
            .Where(pair =>
                pair.SystemId == request.SystemId &&
                pair.Phase == Pass1PairExecutionPhase.ReadyForResolution &&
                pair.TerminalAt != null &&
                pair.DocumentSchemaIdentity == "code-logic/file-v2")
            .Take(2)
            .ToArrayAsync(cancellationToken);
        if (pairs.Length != 1)
        {
            throw new InvalidDataException("The request has no unique ReadyForResolution Pass 1 pair.");
        }

        Pass1PairExecution pair = pairs[0];
        TwoRepositoryScanCoordination? coordination =
            await _context.TwoRepositoryScanCoordinations.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == pair.CoordinationId, cancellationToken);
        if (coordination is null ||
            coordination.SystemId != request.SystemId ||
            coordination.Phase != TwoRepositoryScanCoordinationPhase.Resolving)
        {
            throw new InvalidDataException("The terminal pair Coordination is not resolving.");
        }

        Guid[] manifestIds = [pair.ManifestOneId, pair.ManifestTwoId];
        PreparedScanManifest[] manifests = await _context.PreparedScanManifests.AsNoTracking()
            .Where(manifest => manifestIds.Contains(manifest.Id))
            .ToArrayAsync(cancellationToken);
        Vulgata.Infrastructure.CommunicationDetection.CommunicationPatternCatalog patternCatalog =
            CommunicationCatalogLoader.LoadEmbedded();
        if (manifests.Length != 2 ||
            manifests.Select(manifest => manifest.RepositoryId).Distinct().Count() != 2 ||
            manifests.Any(manifest =>
                manifest.CoordinationId != coordination.Id ||
                manifest.ApprovalState != PreparedScanManifestApprovalState.Approved ||
                !StringComparer.Ordinal.Equals(
                    manifest.SchemaIdentity,
                    PreparedScanManifest.CurrentSchemaIdentity) ||
                !StringComparer.Ordinal.Equals(
                    manifest.CatalogSchemaIdentity,
                    patternCatalog.SchemaIdentity) ||
                !StringComparer.Ordinal.Equals(manifest.CatalogHash, patternCatalog.CatalogSha256) ||
                !StringComparer.Ordinal.Equals(
                    manifest.PairSelectionFingerprint,
                    pair.PairSelectionFingerprint) ||
                !manifest.CodeGraphInitialized ||
                manifest.CodeGraphIndexState != CodeGraphIndexState.Complete ||
                manifest.CodeGraphRepositoryId != manifest.RepositoryId ||
                manifest.CodeGraphScanRunId != manifest.ScanRunId ||
                !StringComparer.Ordinal.Equals(
                    manifest.CodeGraphProfiledRevision,
                    manifest.ProfiledRevision) ||
                !StringComparer.Ordinal.Equals(
                    manifest.CodeGraphSnapshotIdentity,
                    manifest.SnapshotIdentity) ||
                !StringComparer.Ordinal.Equals(
                    manifest.CodeGraphRuntimeIdentityHash,
                    manifest.RuntimeIdentityHash) ||
                !manifest.CodeGraphRepositoryBindingMatched ||
                !manifest.CodeGraphScanRunBindingMatched ||
                !manifest.CodeGraphRevisionBindingMatched ||
                !manifest.CodeGraphRuntimeBindingMatched ||
                !manifest.CodeGraphSnapshotBindingMatched ||
                !manifest.CodeGraphProjectPathTrusted ||
                !manifest.CodeGraphIndexPathTrusted ||
                manifest.JavaReadinessOutcome != JavaReadinessOutcome.Ready ||
                manifest.JavaReadinessRepositoryId != manifest.RepositoryId ||
                manifest.JavaReadinessScanRunId != manifest.ScanRunId ||
                !StringComparer.Ordinal.Equals(
                    manifest.JavaReadinessProfiledRevision,
                    manifest.ProfiledRevision) ||
                !StringComparer.Ordinal.Equals(
                    manifest.JavaReadinessSnapshotIdentity,
                    manifest.SnapshotIdentity) ||
                !StringComparer.Ordinal.Equals(
                    manifest.JavaReadinessRuntimeIdentityHash,
                    manifest.RuntimeIdentityHash) ||
                !manifest.JavaReadinessRepositoryBindingMatched ||
                !manifest.JavaReadinessScanRunBindingMatched ||
                !manifest.JavaReadinessRevisionBindingMatched ||
                !manifest.JavaReadinessRuntimeBindingMatched ||
                !manifest.JavaReadinessSnapshotBindingMatched))
        {
            throw new InvalidDataException("The terminal pair Prepared Scan Manifests are inconsistent.");
        }

        var byAlias = new Dictionary<string, PreparedScanManifest>(StringComparer.Ordinal);
        foreach (ExactResolutionRuntimeBinding binding in request.Bindings)
        {
            ProfileRepository expected = profile.Repositories.Single(repository =>
                StringComparer.Ordinal.Equals(repository.Alias, binding.Alias));
            PreparedScanManifest[] matchingManifests =
            [
                .. manifests.Where(manifest =>
                    manifest.RepositoryId == binding.RepositoryId &&
                    manifest.ScanRunId == binding.ScanRunId &&
                    StringComparer.Ordinal.Equals(
                        manifest.ProfiledRevision,
                        expected.ExpectedCommit)),
            ];
            ScanExecution? execution = await _context.ScanExecutions.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == binding.ScanExecutionId, cancellationToken);
            bool coordinationMatches =
                coordination.RepositoryOneId == binding.RepositoryId &&
                coordination.ScanRunOneId == binding.ScanRunId &&
                coordination.ScanExecutionOneId == binding.ScanExecutionId &&
                StringComparer.Ordinal.Equals(
                    coordination.ProfiledRevisionOne,
                    expected.ExpectedCommit) ||
                coordination.RepositoryTwoId == binding.RepositoryId &&
                coordination.ScanRunTwoId == binding.ScanRunId &&
                coordination.ScanExecutionTwoId == binding.ScanExecutionId &&
                StringComparer.Ordinal.Equals(
                    coordination.ProfiledRevisionTwo,
                    expected.ExpectedCommit);
            if (matchingManifests.Length != 1 ||
                execution is null ||
                execution.SystemId != request.SystemId ||
                execution.RepositoryId != binding.RepositoryId ||
                execution.ScanRunId != binding.ScanRunId ||
                execution.Phase is not (ScanExecutionPhase.Pass1Completed or ScanExecutionPhase.Completed) ||
                !StringComparer.Ordinal.Equals(execution.ProfiledRevision, expected.ExpectedCommit) ||
                !coordinationMatches)
            {
                throw new InvalidDataException("The runtime binding is not the terminal prepared Scan Execution.");
            }

            byAlias.Add(binding.Alias, matchingManifests[0]);
        }

        return new(pair, byAlias);
    }

    private static string ValidateDocumentEvidence(
        CurrentDocumentEvidence evidence,
        SourceAnchor anchor,
        PreparedScanManifest manifest,
        Pass1PairExecution pair,
        ExactResolutionProtocol protocol)
    {
        try
        {
            using JsonDocument json = JsonDocument.Parse(evidence.Document.ContentJson);
            JsonElement root = json.RootElement;
            JsonElement binding = root.GetProperty("binding");
            if (!StringComparer.Ordinal.Equals(
                    root.GetProperty("schemaIdentity").GetString(),
                    "code-logic/file-v2") ||
                !StringComparer.Ordinal.Equals(
                    evidence.Document.SchemaIdentity,
                    "code-logic/file-v2") ||
                binding.GetProperty("systemId").GetGuid() != evidence.Document.SystemId ||
                binding.GetProperty("repositoryId").GetGuid() != evidence.Document.RepositoryId ||
                binding.GetProperty("scanRunId").GetGuid() != evidence.Document.ScanRunId ||
                !StringComparer.Ordinal.Equals(
                    binding.GetProperty("profiledRevision").GetString(),
                    evidence.Document.ProfiledRevision) ||
                binding.GetProperty("preparedScanManifestId").GetGuid() != manifest.Id ||
                !StringComparer.Ordinal.Equals(
                    binding.GetProperty("preparedScanManifestFingerprint").GetString(),
                    manifest.Fingerprint) ||
                !StringComparer.Ordinal.Equals(
                    binding.GetProperty("pairPackageFingerprint").GetString(),
                    pair.PairPackageFingerprint) ||
                !StringComparer.Ordinal.Equals(
                    binding.GetProperty("repositoryRelativePath").GetString(),
                    evidence.Document.RepositoryRelativePath) ||
                !StringComparer.Ordinal.Equals(
                    binding.GetProperty("sourceHash").GetString(),
                    anchor.SourceHash))
            {
                throw new InvalidDataException("The Code Logic Document binding is inconsistent.");
            }

            JsonElement[] sourceAnchors = root.GetProperty("sourceAnchors")
                .EnumerateArray()
                .Where(value =>
                    StringComparer.Ordinal.Equals(
                        value.GetProperty("evidenceLocator").GetString(),
                        anchor.EvidenceLocator) &&
                    StringComparer.Ordinal.Equals(
                        value.GetProperty("repositoryRelativePath").GetString(),
                        anchor.RepositoryRelativePath) &&
                    StringComparer.Ordinal.Equals(
                        value.GetProperty("sourceHash").GetString(),
                        anchor.SourceHash))
                .ToArray();
            if (sourceAnchors.Length != 1)
            {
                throw new InvalidDataException("The Code Logic Document has no unique current source anchor.");
            }

            string anchorKey = sourceAnchors[0].GetProperty("key").GetString() ?? string.Empty;
            CommunicationKindClassification classification = evidence.Classifications.Single(value =>
                StringComparer.Ordinal.Equals(value.Locator, anchor.EvidenceLocator));
            string communicationKind = protocol == ExactResolutionProtocol.CustomRpc
                ? "Rpc"
                : throw new InvalidDataException("The Code Logic protocol is unsupported.");
            bool hasCommunication = root.GetProperty("communicationEvidence")
                .EnumerateArray()
                .Any(item =>
                    StringComparer.Ordinal.Equals(
                        item.GetProperty("communicationKind").GetString(),
                        communicationKind) &&
                    ContainsString(item.GetProperty("sourceAnchorRefs"), anchorKey) &&
                    item.GetProperty("classificationEvidence").EnumerateArray().Any(value =>
                        value.GetProperty("classificationId").GetGuid() == classification.Id &&
                        StringComparer.Ordinal.Equals(
                            value.GetProperty("classificationFingerprint").GetString(),
                            classification.Fingerprint) &&
                        StringComparer.Ordinal.Equals(
                            value.GetProperty("catalogRuleIdentity").GetString(),
                            classification.CatalogRuleIdentity) &&
                        StringComparer.Ordinal.Equals(
                            value.GetProperty("evidenceLocator").GetString(),
                            anchor.EvidenceLocator)));
            JsonElement[] deferred = root.GetProperty("deferredObservations")
                .EnumerateArray()
                .Where(item =>
                    StringComparer.Ordinal.Equals(
                        item.GetProperty("outcome").GetString(),
                        "CrossRepository") &&
                    StringComparer.Ordinal.Equals(
                        item.GetProperty("relationshipKind").GetString(),
                        "References") &&
                    StringComparer.Ordinal.Equals(
                        item.GetProperty("sourceClaimLocator").GetString(),
                        anchor.EvidenceLocator) &&
                    ContainsString(item.GetProperty("sourceAnchorRefs"), anchorKey) &&
                    (!string.IsNullOrWhiteSpace(item.GetProperty("targetSymbol").GetString()) ||
                        !string.IsNullOrWhiteSpace(
                            item.GetProperty("targetRepositoryRelativePath").GetString())))
                .ToArray();
            if (!hasCommunication || deferred.Length != 1)
            {
                throw new InvalidDataException(
                    "Candidate reconstruction requires matching communication and Cross-Repository evidence.");
            }

            string? targetSymbol = deferred[0].GetProperty("targetSymbol").GetString();
            string? targetPath = deferred[0].GetProperty("targetRepositoryRelativePath").GetString();
            return !string.IsNullOrWhiteSpace(targetSymbol)
                ? targetSymbol
                : targetPath ?? throw new InvalidDataException(
                    "The Cross-Repository observation has no target identity.");
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException("The Code Logic Document evidence is invalid.", exception);
        }
    }

    private async Task VerifyRevisionBoundSourceAsync(
        DocumentVersion document,
        SourceAnchor anchor,
        PairAuthority authority,
        string alias,
        CancellationToken cancellationToken)
    {
        if (_sourceReader is null)
        {
            throw new InvalidDataException("A revision-bound source reader is required.");
        }

        PreparedScanManifest manifest = authority.Manifests[alias];
        string runtimeIdentityHash = manifest.Id == authority.Pair.ManifestOneId
            ? authority.Pair.AnalysisRuntimeIdentityHashOne
            : authority.Pair.AnalysisRuntimeIdentityHashTwo;
        var analysisScope = new AnalysisScope(
            document.RepositoryId,
            document.ScanRunId,
            document.ProfiledRevision,
            "candidate-source",
            runtimeIdentityHash,
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        var request = new RevisionBoundSourceRequest(
            anchor.RepositoryRelativePath,
            new AnalysisLineRange(anchor.LineStart, anchor.LineEnd),
            anchor.SourceHash,
            1_048_576);
        SourceExcerptResult result = await _sourceReader.ReadAsync(
            analysisScope,
            request,
            cancellationToken);
        if (result.Status != AnalysisOperationStatus.Succeeded ||
            result.Failure is not null ||
            result.RepositoryId != document.RepositoryId ||
            !StringComparer.Ordinal.Equals(result.ProfiledRevision, document.ProfiledRevision) ||
            !StringComparer.Ordinal.Equals(result.RelativePath, anchor.RepositoryRelativePath) ||
            !StringComparer.Ordinal.Equals(result.SourceSha256, anchor.SourceHash))
        {
            throw new InvalidDataException("Revision-bound source does not match the current Source Anchor.");
        }
    }

    private static bool ContainsString(JsonElement array, string expected) =>
        array.ValueKind == JsonValueKind.Array &&
        array.EnumerateArray().Any(value =>
            StringComparer.Ordinal.Equals(value.GetString(), expected));

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

        CommunicationEvidenceRule[] reproducedRules =
        [
            .. CommunicationEvidenceRuleCatalog.LoadEmbedded().Rules.Where(rule =>
                StringComparer.Ordinal.Equals(
                    rule.Id,
                    classifications[0].CatalogRuleIdentity) &&
                rule.Kind == requiredKind &&
                rule.Disposition == classifications[0].Level),
        ];
        if (reproducedRules.Length != 1 ||
            !ClassificationReproducesRule(classifications[0], reproducedRules[0]))
        {
            throw new InvalidDataException("The persisted classification is not reproduced by the embedded catalog.");
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

    private static bool ClassificationReproducesRule(
        CommunicationKindClassification classification,
        CommunicationEvidenceRule rule) =>
        rule.Category switch
        {
            CommunicationEvidenceCategory.DirectAnnotation =>
                classification.Rule == EvidenceRule.RpcDirectAnnotation &&
                classification.Provenance == EvidenceProvenance.StructuralAnnotation,
            CommunicationEvidenceCategory.MatchedConfiguration =>
                classification.Rule == EvidenceRule.RpcMatchedConfiguration &&
                classification.Provenance == EvidenceProvenance.MatchedConfiguration,
            _ => false,
        };

    private static ExactResolutionOwner Owner(DocumentVersion document, SourceAnchor anchor) =>
        new(document.RepositoryId, document.ScanRunId, document.Id, anchor.Id, document.ProfiledRevision);

    private async Task<string> ReproduceProviderIdentifierAsync(
        ResolutionOccurrence occurrence,
        DocumentVersion providerDocument,
        SourceAnchor providerAnchor,
        PairAuthority authority,
        CancellationToken cancellationToken)
    {
        if (_sourceReader is null || occurrence.Provider.Files.Count != 1)
        {
            throw new InvalidDataException("The exact provider declaration is ambiguous.");
        }

        string file = occurrence.Provider.Files[0];
        string prefix = occurrence.Provider.Root + '/';
        if (!file.StartsWith(prefix, StringComparison.Ordinal) || !file.EndsWith(".xml", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The exact provider declaration is outside the closed catalog root.");
        }

        PreparedScanManifest manifest = authority.Manifests[occurrence.ProviderAlias];
        string runtimeIdentityHash = manifest.Id == authority.Pair.ManifestOneId
            ? authority.Pair.AnalysisRuntimeIdentityHashOne
            : authority.Pair.AnalysisRuntimeIdentityHashTwo;
        var scope = new AnalysisScope(
            providerDocument.RepositoryId,
            providerDocument.ScanRunId,
            providerDocument.ProfiledRevision,
            "candidate-provider-source",
            runtimeIdentityHash,
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        var sourceRequest = new RevisionBoundSourceRequest(
            file,
            new AnalysisLineRange(
                occurrence.Provider.Declaration.Line,
                occurrence.Provider.Declaration.Line),
            providerAnchor.SourceHash,
            1_048_576);
        SourceExcerptResult source = await _sourceReader.ReadAsync(
            scope,
            sourceRequest,
            cancellationToken);
        if (source.Status != AnalysisOperationStatus.Succeeded ||
            source.Failure is not null ||
            source.RepositoryId != providerDocument.RepositoryId ||
            !StringComparer.Ordinal.Equals(source.ProfiledRevision, providerDocument.ProfiledRevision) ||
            !StringComparer.Ordinal.Equals(source.RelativePath, file))
        {
            throw new InvalidDataException("The provider declaration could not be reread at its revision.");
        }

        byte[] sourceBytes = Encoding.UTF8.GetBytes(source.Excerpt);
        if (!StringComparer.Ordinal.Equals(
            Convert.ToHexStringLower(SHA256.HashData(sourceBytes)),
            source.SourceSha256))
        {
            throw new InvalidDataException("The provider declaration is not safe catalog XML.");
        }

        return ReadProviderIdentifier(sourceBytes, occurrence.Provider.Declaration);
    }

    private static string ReadProviderIdentifier(
        byte[] source,
        ExactProviderDeclarationSelector selector)
    {
        try
        {
            using var stream = new MemoryStream(source, writable: false);
            using XmlReader reader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 1_048_576,
                    MaxCharactersFromEntities = 0,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    IgnoreWhitespace = true,
                });
            var ordinal = 0;
            string? selected = null;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.Depth != 0 ||
                    !StringComparer.Ordinal.Equals(reader.LocalName, selector.ElementLocalName) ||
                    reader.NamespaceURI.Length != 0)
                {
                    continue;
                }

                int line = reader is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
                    ? lineInfo.LineNumber
                    : 0;
                if (line != selector.Line)
                {
                    continue;
                }

                string? value = reader.GetAttribute(selector.EndpointAttributeLocalName, string.Empty);
                if (ordinal++ == selector.OccurrenceOrdinalOnLine &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    selected = value.Trim().Normalize(NormalizationForm.FormC);
                }
            }

            return selected ?? throw new InvalidDataException(
                "The provider catalog declaration is missing or ambiguous.");
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("The provider catalog declaration is malformed.", exception);
        }
    }

    private sealed record CurrentDocumentEvidence(
        DocumentVersion Document,
        IReadOnlyList<CommunicationKindClassification> Classifications);

    private sealed record PairAuthority(
        Pass1PairExecution Pair,
        IReadOnlyDictionary<string, PreparedScanManifest> Manifests);
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.CommunicationDetection;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared.Scans;
using CoreCatalog = Vulgata.Core.DomainServices.CommunicationPatternCatalog;
using InfrastructureCatalog = Vulgata.Infrastructure.CommunicationDetection.CommunicationPatternCatalog;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Web.Data;

internal sealed class PreparedScanPairBuilder(
    VulgataDbContext dbContext,
    IPreparedRevisionAcquirer revisionAcquirer,
    IPreparedSnapshotSource snapshotSource,
    IPreparedCommunicationCatalogSource catalogSource,
    IPreparedCodeGraphAnalysisPort codeGraph,
    IPreparedJavaReadinessPort javaReadiness,
    IPreparedTopLevelSymbolSource javaRangeSource,
    PreparedJavaRangeProvider javaRangeProvider,
    AnalysisRuntimeReadiness runtimeReadiness,
    TimeProvider timeProvider) : IPreparedScanPairBuilder
{
    public async Task<PreparedScanPairBuildResult> BuildAsync(
        Guid systemId,
        IReadOnlyList<RepositoryEntity> repositories,
        IReadOnlyList<PreparedScanRepositoryRevisionBinding> revisionBindings,
        CancellationToken cancellationToken)
    {
        if (systemId == Guid.Empty || repositories is not { Count: 2 } ||
            revisionBindings is not { Count: 2 } ||
            repositories.Select(repository => repository.Id).Distinct().Count() != 2)
        {
            return Reject(PreparedScanFailureReason.UnsafeInput, "prepared-scan-invalid-pair");
        }

        string[] runtimeHashes = runtimeReadiness.Current.RuntimeIdentityHash is { } runtimeHash
            ? [runtimeHash]
            : [];
        if (runtimeHashes.Length != 1)
        {
            return Reject(
                PreparedScanFailureReason.AnalysisNotReady,
                "prepared-scan-analysis-runtime-unavailable");
        }

        CoreCatalog? catalog;
        PreparedCatalogLoadResult catalogResult = await catalogSource.LoadAsync(cancellationToken);
        if ((catalog = catalogResult.Catalog) is null)
        {
            return new(null, 0, catalogResult.Failure ?? Failure(
                PreparedScanFailureReason.UnsafeInput,
                "prepared-scan-catalog-unavailable"));
        }

        Guid[] repositoryIds = repositories.Select(repository => repository.Id).ToArray();
        ScanRun[] scanRuns = await dbContext.ScanRuns
            .AsNoTracking()
            .Where(run => repositoryIds.Contains(run.RepositoryId))
            .ToArrayAsync(cancellationToken);
        TwoRepositoryScanCoordination? coordination = await dbContext.TwoRepositoryScanCoordinations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.SystemId == systemId &&
                repositoryIds.Contains(candidate.RepositoryOneId) &&
                repositoryIds.Contains(candidate.RepositoryTwoId),
                cancellationToken);
        if (coordination is null || coordination.Phase != TwoRepositoryScanCoordinationPhase.Preparing)
        {
            return Reject(PreparedScanFailureReason.NotFound, "prepared-scan-coordination-not-found");
        }

        var work = new List<RepositoryWork>(2);
        foreach (RepositoryEntity repository in repositories
                     .OrderBy(value => value.Id.ToString("D"), StringComparer.Ordinal))
        {
            PreparedScanRepositoryRevisionBinding? binding = revisionBindings.SingleOrDefault(
                value => value.RepositoryId == repository.Id);
            ScanRun? scanRun = binding is null
                ? null
                : scanRuns.SingleOrDefault(run =>
                    run.RepositoryId == repository.Id &&
                    StringComparer.Ordinal.Equals(run.ProfiledRevision, binding.ProfiledRevision));
            if (binding is null || scanRun is null || repository.SystemId != systemId)
            {
                return Reject(PreparedScanFailureReason.NotFound, "prepared-scan-binding-not-found");
            }

            PreparedRevisionAcquisitionResult acquisition = await revisionAcquirer.AcquireAsync(
                new PreparedRevisionRequest(
                    repository.Id,
                    scanRun.Id,
                    repository.GitUrl,
                    binding.GitRef,
                    binding.ProfiledRevision),
                cancellationToken);
            if (acquisition.Snapshot is not PreparedRevisionSnapshot revision)
            {
                return new(null, coordination.StateVersion, acquisition.Failure ?? Failure(
                    PreparedScanFailureReason.SourceIdentityMismatch,
                    "prepared-scan-revision-unavailable"));
            }

            PreparedSnapshotReadResult inventory = await snapshotSource.ReadAsync(
                revision,
                cancellationToken);
            if (inventory.Failure is not null)
            {
                return new(null, coordination.StateVersion, inventory.Failure);
            }

            string rootBindingHash = HashToken(revision.ContainedRootToken);
            AnalysisScope scope = new(
                repository.Id,
                scanRun.Id,
                binding.ProfiledRevision,
                revision.SnapshotBindingToken,
                runtimeHashes[0],
                new AnalysisDeadline(timeProvider.GetUtcNow().AddMinutes(2).ToUnixTimeMilliseconds()));
            PreparedCodeGraphProofResult codeGraphResult = await codeGraph.BuildCompletenessProofAsync(
                new PreparedCodeGraphProofRequest(
                    scope,
                    revision.SnapshotIdentity,
                    rootBindingHash,
                    $"repository-{repository.Id:N}",
                    $"index-{repository.Id:N}"),
                cancellationToken);
            if (codeGraphResult.Status != AnalysisOperationStatus.Succeeded ||
                codeGraphResult.Proof is not CodeGraphCompletenessProof proof)
            {
                return Reject(PreparedScanFailureReason.AnalysisNotReady, "prepared-scan-codegraph-not-ready");
            }
            PreparedCanonicalArtifact proofIdentity = PreparedScanIdentityBuilders.CreateCodeGraphProof(proof);
            proof = proof with { IdentityHash = proofIdentity.Fingerprint };

            string[] javaPaths = inventory.Files
                .Where(file => file.IsEligible && StringComparer.OrdinalIgnoreCase.Equals(file.Language, "Java"))
                .Select(file => file.RelativePath)
                .Order(StringComparer.Ordinal)
                .ToArray();
            PreparedJavaReadinessResult javaResult = await javaReadiness.ProveReadinessAsync(
                new PreparedJavaReadinessRequest(
                    scope,
                    revision.SnapshotIdentity,
                    HashToken($"jdt:{revision.SnapshotBindingToken}"),
                    javaPaths),
                cancellationToken);
            if (javaResult.Status != AnalysisOperationStatus.Succeeded ||
                javaResult.Proof is not JavaReadinessProof javaProof)
            {
                return Reject(PreparedScanFailureReason.AnalysisNotReady, "prepared-scan-java-not-ready");
            }
            PreparedCanonicalArtifact javaIdentity = PreparedScanIdentityBuilders.CreateJavaReadiness(javaProof);
            javaProof = javaProof with { IdentityHash = javaIdentity.Fingerprint };

            work.Add(new(repository, scanRun, binding, revision, inventory.Files, proof, javaProof, scope));
        }

        return await BuildPackageAsync(systemId, coordination, catalog, work, cancellationToken);
    }

    private async Task<PreparedScanPairBuildResult> BuildPackageAsync(
        Guid systemId,
        TwoRepositoryScanCoordination coordination,
        CoreCatalog catalog,
        IReadOnlyList<RepositoryWork> work,
        CancellationToken cancellationToken)
    {
        var candidates = new List<PreparedFileCodeUnitDraft>();
        var classifications = new Dictionary<Guid, PreparedCommunicationClassificationResult>();
        var inventoryAudits = new Dictionary<Guid, List<PreparedScanAuditDraft>>();
        InfrastructureCatalog evaluationCatalog = CreateEvaluationCatalog(catalog);
        foreach (RepositoryWork repository in work)
        {
            var observations = new List<PreparedCommunicationObservation>();
            var audits = new List<PreparedScanAuditDraft>();
            inventoryAudits.Add(repository.Repository.Id, audits);
            foreach (PreparedSnapshotFile file in repository.Files)
            {
                if (!file.IsEligible)
                {
                    audits.Add(CreateInventoryAudit(
                        repository,
                        file,
                        PreparedScanAuditOutcome.Filtered,
                        ParseFilteredReason(file.Outcome)));
                    continue;
                }

                if (file.FileHash is null || file.SourceHash is null || file.RawBytes.IsEmpty)
                {
                    audits.Add(CreateInventoryAudit(
                        repository,
                        file,
                        PreparedScanAuditOutcome.Skipped,
                        PreparedScanAuditReason.InvalidSplitRange));
                    continue;
                }

                IReadOnlyList<PreparedBusinessEvidence> evidence =
                    [PreparedBusinessEvidence.ProductionFallback];
                PreparedSourceSplitPlan? xmlSplit = null;
                if (StringComparer.OrdinalIgnoreCase.Equals(file.Language, "XML"))
                {
                    SafeXmlReadResult read = SafeXmlReader.Read(file.RelativePath, file.RawBytes);
                    if (read.Projection is not SafeXmlProjection projection)
                    {
                        bool boundExceeded = read.Outcome == SafeXmlOutcome.BoundExceeded;
                        audits.Add(CreateInventoryAudit(
                            repository,
                            file,
                            PreparedScanAuditOutcome.Warning,
                            boundExceeded
                                ? PreparedScanAuditReason.HardBoundExceeded
                                : PreparedScanAuditReason.UnsafeXml));
                        observations.Add(new PreparedCommunicationObservation(
                            null,
                            ClassificationLevel.Warning,
                            ConfidenceBand.None,
                            boundExceeded ? EvidenceRule.BoundExceeded : EvidenceRule.MalformedEvidence,
                            EvidenceProvenance.Detector,
                            file.RelativePath,
                            boundExceeded
                                ? $"{file.RelativePath}#xml-hard-bound"
                                : $"{file.RelativePath}#unsafe-xml",
                            boundExceeded
                                ? "detector.xml-hard-bound"
                                : "detector.unsafe-xml"));
                        continue;
                    }

                    CommunicationCatalogMatch[] matches =
                        [.. CommunicationCatalogEvaluator.Evaluate(evaluationCatalog, projection)];
                    observations.AddRange(matches.Select(match => new PreparedCommunicationObservation(
                        match.Kind,
                        ClassificationLevel.Confirmed,
                        ConfidenceBand.High,
                        match.Rule,
                        match.Provenance,
                        file.RelativePath,
                        match.Locator.ToString(),
                        match.CatalogRuleIdentity)));
                    if (matches.Length > 0)
                    {
                        evidence =
                        [
                            PreparedBusinessEvidence.ConfirmedCommunication,
                            PreparedBusinessEvidence.ExactXmlCommunicationDeclaration,
                        ];
                    }

                    if (file.RawBytes.Length > PreparedSourceRangePlanner.MaximumRawBytes ||
                        PreparedSourceRangePlanner.CountLogicalLines(file.RawBytes.Span) >
                            PreparedSourceRangePlanner.MaximumLogicalLines)
                    {
                        xmlSplit = PreparedXmlRangeProvider.PlanRanges(
                            file.RawBytes,
                            projection.DirectChildRanges);
                        if (xmlSplit.Outcome != PreparedRangeOutcome.Exact)
                        {
                            audits.Add(CreateInventoryAudit(
                                repository,
                                file,
                                PreparedScanAuditOutcome.Skipped,
                                PreparedScanAuditReason.InvalidSplitRange));
                            continue;
                        }
                    }
                }

                if (xmlSplit is not null)
                {
                    foreach (PreparedJavaRange range in xmlSplit.Ranges)
                    {
                        candidates.Add(CreateFileCodeUnit(repository, file, range, evidence));
                    }

                    continue;
                }

                int logicalLines = PreparedSourceRangePlanner.CountLogicalLines(file.RawBytes.Span);
                if (file.RawBytes.Length > PreparedSourceRangePlanner.MaximumRawBytes ||
                    logicalLines is < 1 or > PreparedSourceRangePlanner.MaximumLogicalLines)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(file.Language, "Java"))
                    {
                        PreparedJavaSourceResult sourceRanges = await javaRangeSource.GetRangesAsync(
                            repository.Scope,
                            file.RelativePath,
                            file.FileHash,
                            cancellationToken);
                        PreparedSourceSplitPlan split = sourceRanges.Failure is null
                            ? javaRangeProvider.GetRanges(file.RawBytes, file.FileHash, sourceRanges.Ranges)
                            : new PreparedSourceSplitPlan(
                                PreparedRangeOutcome.Skipped,
                                "missing_java_ranges",
                                []);
                        if (split.Outcome == PreparedRangeOutcome.Exact)
                        {
                            foreach (PreparedJavaRange range in split.Ranges)
                            {
                                candidates.Add(CreateFileCodeUnit(repository, file, range, evidence));
                            }

                            continue;
                        }
                    }

                    audits.Add(CreateInventoryAudit(
                        repository,
                        file,
                        PreparedScanAuditOutcome.Skipped,
                        PreparedScanAuditReason.InvalidSplitRange));
                    continue;
                }

                PreparedSourceRange wholeFile = PreparedSourceRangePlanner.Create(
                    file.RawBytes.Span,
                    0,
                    file.RawBytes.Length,
                    1);
                candidates.Add(PreparedScanPlanner.CreateFileCodeUnit(new PreparedFileCodeUnitInput(
                    repository.Repository.Id,
                    repository.ScanRun.Id,
                    repository.Binding.ProfiledRevision,
                    file.RelativePath,
                    file.FileHash,
                    wholeFile.ByteStart,
                    wholeFile.ByteEndExclusive,
                    wholeFile.LineStart,
                    wholeFile.LineEnd,
                    1,
                    1,
                    wholeFile.SourceHash,
                    evidence)));
            }

            classifications.Add(
                repository.Repository.Id,
                PreparedCommunicationDetector.Detect(repository.ScanRun.Id, observations));
        }

        var expansion = new PreparedRelatedFileExpansion(codeGraph);
        foreach (RepositoryWork repository in work)
        {
            PreparedFileCodeUnitDraft[] repositoryCandidates = candidates
                .Where(candidate => candidate.RepositoryId == repository.Repository.Id)
                .ToArray();
            PreparedFileCodeUnitDraft[] seeds = PreparedScanPlanner.SelectSeeds(repositoryCandidates)
                .ToArray();
            if (seeds.Length == 0)
            {
                continue;
            }

            long expansionDeadline = Math.Min(
                repository.Scope.Deadline.UnixTimeMilliseconds,
                timeProvider.GetUtcNow().Add(PreparedRelatedFileExpansion.Timeout).ToUnixTimeMilliseconds());
            PreparedRelatedFileExpansionResult expanded = await expansion.ExpandAsync(
                new PreparedRelatedFilesRequest(
                    repository.Scope,
                    seeds.Select(seed => seed.RelativePath).Distinct(StringComparer.Ordinal).ToArray(),
                    repositoryCandidates.Select(candidate => candidate.RelativePath)
                        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                    [
                        PreparedRelationshipKind.Calls,
                        PreparedRelationshipKind.Imports,
                        PreparedRelationshipKind.Inherits,
                    ],
                    PreparedRelatedFileExpansion.MaximumHops,
                    PreparedRelatedFileExpansion.MaximumFiles,
                    expansionDeadline),
                repositoryCandidates,
                cancellationToken);
            candidates.RemoveAll(candidate => candidate.RepositoryId == repository.Repository.Id);
            candidates.AddRange(expanded.Candidates);
            if (expanded.Outcome == PreparedRelatedFileExpansionOutcome.WarningFallback)
            {
                inventoryAudits[repository.Repository.Id].Add(PreparedScanAuditFactory.Create(
                    repository.Repository.Id,
                    repository.ScanRun.Id,
                    repositoryCandidates.OrderBy(candidate => candidate.RelativePath, StringComparer.Ordinal)
                        .First().RelativePath,
                    PreparedScanAuditOutcome.Warning,
                    PreparedScanAuditReason.RelatedFileExpansionFailed,
                    null,
                    null,
                    null,
                    null,
                    null));
            }
        }

        PreparedSelectionPlan selection;
        try
        {
            selection = PreparedScanPlanner.Plan(candidates);
        }
        catch (InvalidOperationException)
        {
            return Reject(PreparedScanFailureReason.SelectionBounds, "prepared-scan-selection-bounds");
        }

        IReadOnlyList<PreparedScanAuditDraft> selectionAudits =
            PreparedScanPlanner.CreateSelectionAuditLinks(selection);
        DateTimeOffset now = timeProvider.GetUtcNow();
        var repositoryPackages = new List<PreparedRepositoryPackage>(2);
        foreach (RepositoryWork repository in work)
        {
            PreparedCommunicationClassificationResult classification =
                classifications[repository.Repository.Id];
            PreparedFileCodeUnitDraft[] units = selection.Selected
                .Where(unit => unit.RepositoryId == repository.Repository.Id)
                .OrderBy(unit => unit.ManifestOrdinal)
                .ToArray();
            PreparedScanAuditDraft[] audits = selectionAudits
                .Where(audit => audit.RepositoryId == repository.Repository.Id)
                .Concat(inventoryAudits[repository.Repository.Id])
                .OrderBy(audit => audit.RelativePath, StringComparer.Ordinal)
                .ThenBy(audit => AuditOutcomeRank(audit.Outcome))
                .ThenBy(audit => audit.Reason.ToString(), StringComparer.Ordinal)
                .ThenBy(audit => audit.DetailFingerprint, StringComparer.Ordinal)
                .Select((audit, index) => audit with { Ordinal = index + 1 })
                .ToArray();
            DateTimeOffset capturedAt = now < repository.ScanRun.StartedAt
                ? repository.ScanRun.StartedAt
                : now;
            ScanContextSnapshot context = ScanContextSnapshot.CapturePrepared(
                repository.ScanRun,
                null,
                null,
                repository.Repository.Context,
                capturedAt);
            repositoryPackages.Add(new PreparedRepositoryPackage(
                repository.Repository.Id,
                repository.ScanRun.Id,
                coordination.Id,
                repository.Binding.ProfiledRevision,
                repository.Binding.GitRef,
                repository.Revision.SnapshotIdentity,
                repository.CodeGraphProof.RuntimeIdentityHash,
                repository.CodeGraphProof,
                repository.JavaProof,
                catalog,
                context.Id,
                context.PreparedScanContextIdentityHash!,
                context.ContentHash,
                string.Empty,
                units,
                audits,
                classification.Classifications,
                classification.TotalConfirmedCount,
                classification.TotalHintCount,
                classification.TotalWarningCount,
                classification.EvidenceTruncated,
                capturedAt,
                capturedAt)
            {
                RepositoryContext = repository.Repository.Context,
            });
        }

        PreparedCanonicalArtifact pairSelection = PreparedScanIdentityBuilders.CreatePairSelection(
            coordination.Id,
            selection.Selected);
        repositoryPackages = [.. repositoryPackages.Select(package => package with
        {
            ManifestFingerprint = PreparedScanIdentityBuilders.CreateManifest(
                package,
                pairSelection.Fingerprint).Fingerprint,
        })];
        PreparedCanonicalArtifact pairPackage = PreparedScanIdentityBuilders.CreatePairPackage(
            coordination.Id,
            pairSelection.Fingerprint,
            repositoryPackages.Select(package => new PreparedManifestFingerprintBinding(
                package.RepositoryId,
                package.ManifestFingerprint)),
            selection.Selected.Count);
        return new(
            new PreparedScanPairPackage(
                coordination.Id,
                pairSelection.Fingerprint,
                pairPackage.Fingerprint,
                repositoryPackages,
                selection.Selected.Count),
            coordination.StateVersion,
            null);
    }

    private static PreparedFileCodeUnitDraft CreateFileCodeUnit(
        RepositoryWork repository,
        PreparedSnapshotFile file,
        PreparedJavaRange range,
        IReadOnlyList<PreparedBusinessEvidence> evidence) =>
        PreparedScanPlanner.CreateFileCodeUnit(new PreparedFileCodeUnitInput(
            repository.Repository.Id,
            repository.ScanRun.Id,
            repository.Binding.ProfiledRevision,
            file.RelativePath,
            file.FileHash!,
            range.ByteStart,
            range.ByteEndExclusive,
            range.LineStart,
            range.LineEnd,
            range.SegmentOrdinal,
            range.SegmentCount,
            range.SourceHash,
            evidence));

    private static PreparedScanAuditDraft CreateInventoryAudit(
        RepositoryWork repository,
        PreparedSnapshotFile file,
        PreparedScanAuditOutcome outcome,
        PreparedScanAuditReason reason) => PreparedScanAuditFactory.Create(
            repository.Repository.Id,
            repository.ScanRun.Id,
            file.RelativePath,
            outcome,
            reason,
            file.FileHash,
            file.RawByteCount,
            file.LogicalLineCount,
            null,
            null);

    private static PreparedScanAuditReason ParseFilteredReason(string outcome) =>
        Enum.TryParse(outcome, ignoreCase: false, out PreparedScanAuditReason reason) &&
        reason is PreparedScanAuditReason.ExcludedPathSegment or
            PreparedScanAuditReason.ExcludedTestPath or
            PreparedScanAuditReason.UnsupportedExtension or
            PreparedScanAuditReason.UnsafePath or
            PreparedScanAuditReason.CaseInsensitivePathCollision or
            PreparedScanAuditReason.LinkOrReparsePoint or
            PreparedScanAuditReason.NonRegularEntry or
            PreparedScanAuditReason.BinaryJava or
            PreparedScanAuditReason.InvalidJavaEncoding or
            PreparedScanAuditReason.UnsupportedXmlEncoding
            ? reason
            : PreparedScanAuditReason.UnsupportedExtension;

    private static int AuditOutcomeRank(PreparedScanAuditOutcome outcome) => outcome switch
    {
        PreparedScanAuditOutcome.Eligible => 0,
        PreparedScanAuditOutcome.Filtered => 1,
        PreparedScanAuditOutcome.Skipped => 2,
        PreparedScanAuditOutcome.Selected => 3,
        PreparedScanAuditOutcome.Warning => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private static InfrastructureCatalog CreateEvaluationCatalog(CoreCatalog catalog) =>
        new(
            catalog.SchemaIdentity,
            catalog.CatalogId,
            catalog.Version,
            catalog.ResourceHash,
            catalog.Rules.Select(rule => new CommunicationPatternRule(
                rule.Identity,
                rule.PathGlob,
                rule.Selector.Select(step => new CommunicationPatternElementStep(
                    step.NamespaceUri,
                    step.LocalName)),
                rule.Ancestor?.Select(step => new CommunicationPatternElementStep(
                    step.NamespaceUri,
                    step.LocalName)),
                rule.AttributeTests.Select(test => new CommunicationPatternAttributeTest(
                    test.NamespaceUri,
                    test.LocalName,
                    true,
                    test.ExactValue)),
                new CommunicationPatternExtraction(
                    rule.Extraction.Kind == CommunicationCatalogExtractionKind.Attribute
                        ? CommunicationPatternExtractionKind.Attribute
                        : CommunicationPatternExtractionKind.ElementText,
                    rule.Extraction.NamespaceUri,
                    rule.Extraction.LocalName),
                rule.FixedPrefixRemoval,
                rule.Kind,
                rule.Rule,
                rule.Provenance)));

    private static string HashToken(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static PreparedScanPairBuildResult Reject(PreparedScanFailureReason reason, string code) =>
        new(null, 0, Failure(reason, code));

    private static PreparedScanFailure Failure(PreparedScanFailureReason reason, string code) =>
        new(reason, code, "Prepared scan pair construction failed closed.");

    private sealed record RepositoryWork(
        RepositoryEntity Repository,
        ScanRun ScanRun,
        PreparedScanRepositoryRevisionBinding Binding,
        PreparedRevisionSnapshot Revision,
        IReadOnlyList<PreparedSnapshotFile> Files,
        CodeGraphCompletenessProof CodeGraphProof,
        JavaReadinessProof JavaProof,
        AnalysisScope Scope);
}

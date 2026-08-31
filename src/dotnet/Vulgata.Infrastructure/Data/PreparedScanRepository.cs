using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

internal sealed class PreparedScanRepository(VulgataDbContext context)
{
    public async Task<PreparedScanCommitOutcome?> InspectAsync(
        PreparedScanAuthorizationScope authorization,
        PreparedScanPairPackage pair,
        long expectedCoordinationVersion,
        CancellationToken cancellationToken)
    {
        bool packageValid = ValidatePackage(authorization, pair);
        bool durablePairExists = await context.PreparedScanManifests.AsNoTracking()
            .AnyAsync(value => value.CoordinationId == pair.CoordinationId, cancellationToken);
        if (!packageValid)
        {
            return durablePairExists || IsCompletePairShape(pair)
                ? PreparedScanCommitOutcome.Conflict
                : PreparedScanCommitOutcome.Rejected;
        }

        TwoRepositoryScanCoordination? coordination = await context.TwoRepositoryScanCoordinations
            .SingleOrDefaultAsync(value => value.Id == pair.CoordinationId, cancellationToken);
        if (coordination is null || coordination.SystemId != authorization.SystemId ||
            coordination.StateVersion != expectedCoordinationVersion ||
            coordination.Phase != TwoRepositoryScanCoordinationPhase.Preparing)
        {
            return PreparedScanCommitOutcome.Conflict;
        }

        PreparedScanManifest[] existing = await context.PreparedScanManifests
            .AsNoTracking()
            .Where(value => value.CoordinationId == pair.CoordinationId)
            .ToArrayAsync(cancellationToken);
        if (existing.Length == 0)
        {
            return null;
        }

        bool exact = existing.Length == 2 && pair.Repositories.All(package => existing.Any(manifest =>
            manifest.RepositoryId == package.RepositoryId &&
            manifest.ScanRunId == package.ScanRunId &&
            StringComparer.Ordinal.Equals(manifest.Fingerprint, package.ManifestFingerprint) &&
            StringComparer.Ordinal.Equals(manifest.PairSelectionFingerprint, pair.PairSelectionFingerprint)));
        if (!exact)
        {
            return PreparedScanCommitOutcome.Conflict;
        }

        foreach (PreparedRepositoryPackage package in pair.Repositories)
        {
            Guid manifestId = PreparedScanIdentityBuilders.CreateDeterministicIdentity(
                "PreparedScanManifest", package.ManifestFingerprint);
            PreparedScanManifest manifest = existing.Single(value => value.Id == manifestId);
            PreparedCanonicalArtifact graph = PreparedScanIdentityBuilders.CreateCodeGraphProof(package.CodeGraphProof);
            PreparedCanonicalArtifact java = PreparedScanIdentityBuilders.CreateJavaReadiness(package.JavaReadiness);
            if (!StoredProofsMatch(manifest, graph.Fingerprint, java.Fingerprint) ||
                !ManifestMatches(manifest, package, pair, graph.Fingerprint, java.Fingerprint))
            {
                return PreparedScanCommitOutcome.Conflict;
            }

            PreparedScanManifestEntry[] entries = await context.PreparedScanManifestEntries
                .AsNoTracking()
                .Where(value => value.PreparedScanManifestId == manifestId)
                .OrderBy(value => value.ManifestOrdinal)
                .ToArrayAsync(cancellationToken);
            if (entries.Length != package.FileCodeUnits.Count)
            {
                return PreparedScanCommitOutcome.Conflict;
            }

            Guid[] unitIds = entries.Select(value => value.FileCodeUnitId).ToArray();
            CodeUnit[] storedUnits = await context.CodeUnits.AsNoTracking()
                .Where(value => unitIds.Contains(value.Id))
                .ToArrayAsync(cancellationToken);
            foreach (PreparedFileCodeUnitDraft supplied in package.FileCodeUnits)
            {
                PreparedScanManifestEntry? entry = entries.SingleOrDefault(value => value.FileCodeUnitId == supplied.Id);
                CodeUnit? stored = storedUnits.SingleOrDefault(value => value.Id == supplied.Id);
                if (entry is null || stored is null || !EntryMatches(entry, supplied) ||
                    !CodeUnitMatches(stored, supplied))
                {
                    return PreparedScanCommitOutcome.Conflict;
                }
            }

            PreparedScanAudit[] audits = await context.PreparedScanAudits.AsNoTracking()
                .Where(value => value.PreparedScanManifestId == manifestId)
                .OrderBy(value => value.Ordinal)
                .ToArrayAsync(cancellationToken);
            if (audits.Length != package.Audits.Count || audits.Any(audit =>
                    !AuditMatches(audit, package.Audits[audit.Ordinal - 1])))
            {
                return PreparedScanCommitOutcome.Conflict;
            }

            CommunicationKindClassification[] classifications = await context
                .CommunicationKindClassifications.AsNoTracking()
                .Where(value => value.PreScanResultId == package.ScanRunId)
                .ToArrayAsync(cancellationToken);
            classifications = PreparedScanIdentityBuilders
                .OrderCommunicationClassifications(classifications);
            if (!ClassificationsMatch(classifications, package.Classifications))
            {
                return PreparedScanCommitOutcome.Conflict;
            }

            if (!StoredManifestFingerprintMatches(manifest, entries, audits, classifications))
            {
                return PreparedScanCommitOutcome.Conflict;
            }

            ScanContextSnapshot? snapshot = await context.ScanContextSnapshots.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == manifest.ContextSnapshotId, cancellationToken);
            PreScanResult? result = await context.PreScanResults.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == package.ScanRunId, cancellationToken);
            if (snapshot is null || result is null ||
                snapshot.RepositoryId != package.RepositoryId || snapshot.ScanRunId != package.ScanRunId ||
                !StringComparer.Ordinal.Equals(snapshot.ProfiledRevision, package.ProfiledRevision) ||
                !StringComparer.Ordinal.Equals(snapshot.ContentHash, package.ContextContentHash) ||
                !StringComparer.Ordinal.Equals(snapshot.PreparedScanContextIdentityHash, package.PreparedScanContextIdentityHash) ||
                result.PreparedScanManifestId != manifestId ||
                !StringComparer.Ordinal.Equals(result.PreparedScanManifestFingerprint, package.ManifestFingerprint) ||
                !StringComparer.Ordinal.Equals(result.PairPackageFingerprint, pair.PairPackageFingerprint) ||
                !StringComparer.Ordinal.Equals(result.PreparedScanContextIdentityHash, package.PreparedScanContextIdentityHash) ||
                !StringComparer.Ordinal.Equals(result.ContextContentHash, package.ContextContentHash))
            {
                return PreparedScanCommitOutcome.Conflict;
            }
        }

        return PreparedScanCommitOutcome.Replay;
    }

    public async Task PersistAsync(PreparedScanPairPackage pair, CancellationToken cancellationToken)
    {
        foreach (PreparedRepositoryPackage package in pair.Repositories)
        {
            Guid manifestId = PreparedScanIdentityBuilders.CreateDeterministicIdentity(
                "PreparedScanManifest", package.ManifestFingerprint);
            ScanContextSnapshot snapshot = NewEntity<ScanContextSnapshot>();
            Set(snapshot, nameof(snapshot.Id), package.ContextSnapshotId);
            Set(snapshot, nameof(snapshot.RepositoryId), package.RepositoryId);
            Set(snapshot, nameof(snapshot.ScanRunId), package.ScanRunId);
            Set(snapshot, nameof(snapshot.ProfiledRevision), package.ProfiledRevision);
            Set(snapshot, nameof(snapshot.GlobalContext), package.GlobalContext);
            Set(snapshot, nameof(snapshot.SystemContext), package.SystemContext);
            Set(snapshot, nameof(snapshot.RepositoryContext), package.RepositoryContext);
            Set(snapshot, nameof(snapshot.SchemaIdentity), "scan-context-snapshot/1.0");
            Set(snapshot, nameof(snapshot.ContentHash), package.ContextContentHash);
            Set(snapshot, nameof(snapshot.PreparedScanContextIdentityHash), package.PreparedScanContextIdentityHash);
            Set(snapshot, nameof(snapshot.CapturedAt), package.CapturedAt);
            context.ScanContextSnapshots.Add(snapshot);

            PreparedCanonicalArtifact graph = PreparedScanIdentityBuilders.CreateCodeGraphProof(package.CodeGraphProof);
            PreparedCanonicalArtifact java = PreparedScanIdentityBuilders.CreateJavaReadiness(package.JavaReadiness);
            PreparedScanManifest manifest = NewEntity<PreparedScanManifest>();
            Set(manifest, nameof(manifest.Id), manifestId);
            Set(manifest, nameof(manifest.RepositoryId), package.RepositoryId);
            Set(manifest, nameof(manifest.ScanRunId), package.ScanRunId);
            Set(manifest, nameof(manifest.CoordinationId), package.CoordinationId);
            Set(manifest, nameof(manifest.ProfiledRevision), package.ProfiledRevision);
            Set(manifest, nameof(manifest.RequestedRef), package.RequestedRef);
            Set(manifest, nameof(manifest.SchemaIdentity), PreparedScanManifest.CurrentSchemaIdentity);
            Set(manifest, nameof(manifest.SnapshotIdentity), package.SnapshotIdentity);
            Set(manifest, nameof(manifest.RuntimeIdentityHash), package.RuntimeIdentityHash);
            Set(manifest, nameof(manifest.CodeGraphProofIdentityHash), graph.Fingerprint);
            Set(manifest, nameof(manifest.JavaReadinessIdentityHash), java.Fingerprint);
            SetCodeGraphProof(manifest, package.CodeGraphProof);
            SetJavaReadinessProof(manifest, package.JavaReadiness);
            Set(manifest, nameof(manifest.CodeGraphObservedDurationMilliseconds), package.CodeGraphProof.ObservedDurationMilliseconds);
            Set(manifest, nameof(manifest.JavaReadinessObservedDurationMilliseconds), package.JavaReadiness.ObservedDurationMilliseconds);
            Set(manifest, nameof(manifest.CatalogSchemaIdentity), package.Catalog.SchemaIdentity);
            Set(manifest, nameof(manifest.CatalogHash), package.Catalog.ResourceHash);
            Set(manifest, nameof(manifest.CandidateCount), package.Audits.Count);
            Set(manifest, nameof(manifest.EligibleCount), package.Audits.Count(value => value.Outcome is PreparedScanAuditOutcome.Eligible or PreparedScanAuditOutcome.Selected));
            Set(manifest, nameof(manifest.FilteredCount), package.Audits.Count(value => value.Outcome == PreparedScanAuditOutcome.Filtered));
            Set(manifest, nameof(manifest.SkippedCount), package.Audits.Count(value => value.Outcome == PreparedScanAuditOutcome.Skipped));
            Set(manifest, nameof(manifest.SelectedCount), package.FileCodeUnits.Count);
            Set(manifest, nameof(manifest.TotalConfirmedEvidenceCount), package.TotalConfirmedEvidenceCount);
            Set(manifest, nameof(manifest.TotalHintCount), package.TotalHintCount);
            Set(manifest, nameof(manifest.TotalWarningCount), package.TotalWarningCount);
            Set(manifest, nameof(manifest.RetainedClassificationCount), package.Classifications.Count);
            Set(manifest, nameof(manifest.EvidenceTruncated), package.EvidenceTruncated);
            Set(manifest, nameof(manifest.ContextSnapshotId), package.ContextSnapshotId);
            Set(manifest, nameof(manifest.PreparedScanContextIdentityHash), package.PreparedScanContextIdentityHash);
            Set(manifest, nameof(manifest.ContextContentHash), package.ContextContentHash);
            Set(manifest, nameof(manifest.PairSelectionFingerprint), pair.PairSelectionFingerprint);
            Set(manifest, nameof(manifest.Fingerprint), package.ManifestFingerprint);
            Set(manifest, nameof(manifest.ApprovalState), PreparedScanManifestApprovalState.Approved);
            Set(manifest, nameof(manifest.CreatedAt), package.ApprovedAt);
            Set(manifest, nameof(manifest.ApprovedAt), package.ApprovedAt);
            context.PreparedScanManifests.Add(manifest);

            foreach (PreparedFileCodeUnitDraft draft in package.FileCodeUnits)
            {
                CodeUnit unit = CodeUnit.CreatePreparedFileCodeUnit(
                    draft.Id, draft.ScanRunId, draft.RepositoryId, draft.RelativePath,
                    draft.FileHash, draft.SourceHash, draft.ByteStart, draft.ByteEndExclusive,
                    draft.LineStart, draft.LineEnd, draft.SegmentOrdinal, draft.SegmentCount,
                    draft.IdentityFingerprint, draft.Language, draft.SymbolName);
                context.CodeUnits.Add(unit);
                PreparedScanManifestEntry entry = NewEntity<PreparedScanManifestEntry>();
                Set(entry, nameof(entry.FileCodeUnitId), draft.Id);
                Set(entry, nameof(entry.PreparedScanManifestId), manifestId);
                Set(entry, nameof(entry.CoordinationId), package.CoordinationId);
                Set(entry, nameof(entry.FileCodeUnitIdentityFingerprint), draft.IdentityFingerprint);
                Set(entry, nameof(entry.DirectBusinessScore), draft.DirectBusinessScore);
                Set(entry, nameof(entry.FinalBusinessScore), draft.FinalBusinessScore);
                Set(entry, nameof(entry.SelectionReason), draft.SelectionReason);
                Set(entry, nameof(entry.ManifestOrdinal), draft.ManifestOrdinal);
                Set(entry, nameof(entry.PairSelectionOrdinal), draft.PairSelectionOrdinal);
                context.PreparedScanManifestEntries.Add(entry);
            }

            foreach (PreparedScanAuditDraft draft in package.Audits)
            {
                PreparedScanAudit audit = NewEntity<PreparedScanAudit>();
                Set(audit, nameof(audit.Id), draft.Id);
                Set(audit, nameof(audit.PreparedScanManifestId), manifestId);
                Set(audit, nameof(audit.RepositoryId), draft.RepositoryId);
                Set(audit, nameof(audit.ScanRunId), draft.ScanRunId);
                Set(audit, nameof(audit.Ordinal), draft.Ordinal);
                Set(audit, nameof(audit.RelativePath), draft.RelativePath);
                Set(audit, nameof(audit.Outcome), draft.Outcome);
                Set(audit, nameof(audit.Reason), draft.Reason);
                Set(audit, nameof(audit.FileHash), draft.FileHash);
                Set(audit, nameof(audit.RawByteCount), draft.RawByteCount);
                Set(audit, nameof(audit.LogicalLineCount), draft.LogicalLineCount);
                Set(audit, nameof(audit.FileCodeUnitIdentityFingerprint), draft.FileCodeUnitIdentityFingerprint);
                Set(audit, nameof(audit.PairSelectionOrdinal), draft.PairSelectionOrdinal);
                Set(audit, nameof(audit.PreparedScanManifestEntryId), draft.PreparedScanManifestEntryId);
                Set(audit, nameof(audit.DetailFingerprint), draft.DetailFingerprint);
                context.PreparedScanAudits.Add(audit);
            }

            PreScanResult? result = await context.PreScanResults.SingleOrDefaultAsync(
                value => value.Id == package.ScanRunId && value.RepositoryId == package.RepositoryId,
                cancellationToken);
            PreScanResult prepared = PreScanResult.CreatePreparedApproved(
                package, manifestId, pair.PairPackageFingerprint, "预扫描已批准——已准备确定性扫描清单");
            if (result is null)
            {
                context.PreScanResults.Add(prepared);
                continue;
            }

            context.Entry(result).Collection(nameof(PreScanResult.Classifications)).CurrentValue =
                package.Classifications.ToList();
            foreach (CommunicationKindClassification classification in package.Classifications)
            {
                context.Entry(classification).State = EntityState.Added;
            }
            foreach (string property in new[]
            {
                nameof(result.State), nameof(result.SchemaVersion), nameof(result.ApprovedAt),
                nameof(result.Fingerprint), nameof(result.ApprovalMessage),
                nameof(result.ConfirmedCommunicationKinds), nameof(result.TotalConfirmedEvidenceCount),
                nameof(result.TotalHintCount), nameof(result.TotalWarningCount), nameof(result.EvidenceTruncated),
                nameof(result.PreparedScanManifestId), nameof(result.PreparedScanManifestFingerprint),
                nameof(result.ContextSnapshotId), nameof(result.PreparedScanContextIdentityHash),
                nameof(result.ContextContentHash), nameof(result.HandoffSchemaIdentity),
                nameof(result.PairPackageFingerprint),
            })
            {
                context.Entry(result).Property(property).CurrentValue =
                    context.Entry(prepared).Property(property).CurrentValue;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool ValidatePackage(PreparedScanAuthorizationScope authorization, PreparedScanPairPackage pair)
    {
        if (pair.Repositories.Count != 2 || pair.SelectedCount is < 50 or > 150 ||
            pair.Repositories.Select(value => value.RepositoryId).Distinct().Count() != 2 ||
            pair.Repositories.Any(value => value.CoordinationId != pair.CoordinationId) ||
            !pair.Repositories.Select(value => value.RepositoryId).Order().SequenceEqual(authorization.RepositoryIds.Order()) ||
            pair.Repositories.Sum(value => value.FileCodeUnits.Count) != pair.SelectedCount)
        {
            return false;
        }

        PreparedFileCodeUnitDraft[] units = pair.Repositories.SelectMany(value => value.FileCodeUnits).ToArray();
        if (!units.Select(value => value.PairSelectionOrdinal).Order().SequenceEqual(Enumerable.Range(1, units.Length)) ||
            units.Any(value => !StringComparer.Ordinal.Equals(
                PreparedScanPlanner.FingerprintIdentity(value), value.IdentityFingerprint)))
        {
            return false;
        }

        bool auditsValid = pair.Repositories.All(package => package.Audits.All(draft =>
        {
            try
            {
                PreparedScanAuditDraft reconstructed = PreparedScanAuditFactory.Create(
                    draft.RepositoryId, draft.ScanRunId, draft.RelativePath, draft.Outcome, draft.Reason,
                    draft.FileHash, draft.RawByteCount, draft.LogicalLineCount,
                    draft.FileCodeUnitIdentityFingerprint, draft.PairSelectionOrdinal,
                    draft.Ordinal, draft.PreparedScanManifestEntryId);
                return reconstructed.Id == draft.Id &&
                    StringComparer.Ordinal.Equals(reconstructed.DetailFingerprint, draft.DetailFingerprint);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }));
        if (!auditsValid)
        {
            return false;
        }

        try
        {
            PreparedCanonicalArtifact selection = PreparedScanIdentityBuilders.CreatePairSelection(
                pair.CoordinationId, units);
            if (!StringComparer.Ordinal.Equals(selection.Fingerprint, pair.PairSelectionFingerprint))
            {
                return false;
            }

            foreach (PreparedRepositoryPackage package in pair.Repositories)
            {
                CommunicationKindClassification[] orderedClassifications =
                    PreparedScanIdentityBuilders.OrderCommunicationClassifications(package.Classifications);
                if (!package.Classifications.Select(value => value.Fingerprint).SequenceEqual(
                    orderedClassifications.Select(value => value.Fingerprint), StringComparer.Ordinal))
                {
                    return false;
                }

                string graph = PreparedScanIdentityBuilders.CreateCodeGraphProof(package.CodeGraphProof).Fingerprint;
                string java = PreparedScanIdentityBuilders.CreateJavaReadiness(package.JavaReadiness).Fingerprint;
                if (!StringComparer.Ordinal.Equals(graph, package.CodeGraphProof.IdentityHash) ||
                    !StringComparer.Ordinal.Equals(java, package.JavaReadiness.IdentityHash) ||
                    !StringComparer.Ordinal.Equals(
                        PreparedScanIdentityBuilders.CreateManifest(package, selection.Fingerprint).Fingerprint,
                        package.ManifestFingerprint))
                {
                    return false;
                }
            }

            PreparedCanonicalArtifact pairArtifact = PreparedScanIdentityBuilders.CreatePairPackage(
                pair.CoordinationId,
                selection.Fingerprint,
                pair.Repositories.Select(package => new PreparedManifestFingerprintBinding(
                    package.RepositoryId, package.ManifestFingerprint)),
                pair.SelectedCount);
            return StringComparer.Ordinal.Equals(pairArtifact.Fingerprint, pair.PairPackageFingerprint);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsCompletePairShape(PreparedScanPairPackage pair) =>
        pair.Repositories.Count == 2 &&
        pair.Repositories.Select(value => value.RepositoryId).Distinct().Count() == 2 &&
        pair.SelectedCount is >= PreparedScanPlanner.MinimumPairSelection and
            <= PreparedScanPlanner.MaximumPairSelection &&
        pair.Repositories.Sum(value => value.FileCodeUnits.Count) == pair.SelectedCount;

    private static bool AuditMatches(PreparedScanAudit stored, PreparedScanAuditDraft supplied)
    {
        PreparedScanAuditDraft reconstructed = PreparedScanAuditFactory.Create(
            stored.RepositoryId, stored.ScanRunId, stored.RelativePath, stored.Outcome, stored.Reason,
            stored.FileHash, stored.RawByteCount, stored.LogicalLineCount,
            stored.FileCodeUnitIdentityFingerprint, stored.PairSelectionOrdinal,
            stored.Ordinal, stored.PreparedScanManifestEntryId);
        return reconstructed.Id == stored.Id &&
            StringComparer.Ordinal.Equals(reconstructed.DetailFingerprint, stored.DetailFingerprint) &&
            reconstructed.Id == supplied.Id &&
            StringComparer.Ordinal.Equals(reconstructed.DetailFingerprint, supplied.DetailFingerprint);
    }

    private static bool ManifestMatches(
        PreparedScanManifest stored,
        PreparedRepositoryPackage supplied,
        PreparedScanPairPackage pair,
        string graphIdentity,
        string javaIdentity) =>
        stored.RepositoryId == supplied.RepositoryId && stored.ScanRunId == supplied.ScanRunId &&
        stored.CoordinationId == supplied.CoordinationId &&
        StringComparer.Ordinal.Equals(stored.ProfiledRevision, supplied.ProfiledRevision) &&
        StringComparer.Ordinal.Equals(stored.RequestedRef, supplied.RequestedRef) &&
        StringComparer.Ordinal.Equals(stored.SchemaIdentity, PreparedScanManifest.CurrentSchemaIdentity) &&
        StringComparer.Ordinal.Equals(stored.SnapshotIdentity, supplied.SnapshotIdentity) &&
        StringComparer.Ordinal.Equals(stored.RuntimeIdentityHash, supplied.RuntimeIdentityHash) &&
        StringComparer.Ordinal.Equals(stored.CodeGraphProofIdentityHash, graphIdentity) &&
        StringComparer.Ordinal.Equals(stored.JavaReadinessIdentityHash, javaIdentity) &&
        StringComparer.Ordinal.Equals(stored.CatalogSchemaIdentity, supplied.Catalog.SchemaIdentity) &&
        StringComparer.Ordinal.Equals(stored.CatalogHash, supplied.Catalog.ResourceHash) &&
        StringComparer.Ordinal.Equals(stored.PreparedScanContextIdentityHash, supplied.PreparedScanContextIdentityHash) &&
        StringComparer.Ordinal.Equals(stored.ContextContentHash, supplied.ContextContentHash) &&
        StringComparer.Ordinal.Equals(stored.PairSelectionFingerprint, pair.PairSelectionFingerprint) &&
        StringComparer.Ordinal.Equals(stored.Fingerprint, supplied.ManifestFingerprint) &&
        stored.CandidateCount == supplied.Audits.Count &&
        stored.EligibleCount == supplied.Audits.Count(value => value.Outcome is PreparedScanAuditOutcome.Eligible or PreparedScanAuditOutcome.Selected) &&
        stored.FilteredCount == supplied.Audits.Count(value => value.Outcome == PreparedScanAuditOutcome.Filtered) &&
        stored.SkippedCount == supplied.Audits.Count(value => value.Outcome == PreparedScanAuditOutcome.Skipped) &&
        stored.SelectedCount == supplied.FileCodeUnits.Count &&
        stored.TotalConfirmedEvidenceCount == supplied.TotalConfirmedEvidenceCount &&
        stored.TotalHintCount == supplied.TotalHintCount &&
        stored.TotalWarningCount == supplied.TotalWarningCount &&
        stored.RetainedClassificationCount == supplied.Classifications.Count &&
        stored.EvidenceTruncated == supplied.EvidenceTruncated &&
        stored.ApprovalState == PreparedScanManifestApprovalState.Approved;

    private static bool StoredProofsMatch(
        PreparedScanManifest stored,
        string suppliedGraphIdentity,
        string suppliedJavaIdentity)
    {
        try
        {
            PreparedCanonicalArtifact graph = PreparedScanIdentityBuilders.CreateCodeGraphProof(
                new CodeGraphCompletenessProof(
                    stored.CodeGraphRepositoryId,
                    stored.CodeGraphScanRunId,
                    stored.CodeGraphProfiledRevision,
                    stored.CodeGraphSnapshotIdentity,
                    stored.CodeGraphSnapshotRootBindingHash,
                    stored.CodeGraphRuntimeIdentityHash,
                    stored.CodeGraphIndexIdentity,
                    stored.CodeGraphProofSchemaIdentity,
                    stored.CodeGraphInitialized,
                    stored.CodeGraphIndexState,
                    stored.CodeGraphPendingAdded,
                    stored.CodeGraphPendingModified,
                    stored.CodeGraphPendingRemoved,
                    stored.CodeGraphWorktreeMismatch,
                    stored.CodeGraphReindexRecommended,
                    stored.CodeGraphPendingReferences,
                    stored.CodeGraphIndexedFileCount,
                    stored.CodeGraphLogicalProjectBinding,
                    stored.CodeGraphLogicalIndexBinding,
                    stored.CodeGraphRepositoryBindingMatched,
                    stored.CodeGraphScanRunBindingMatched,
                    stored.CodeGraphRevisionBindingMatched,
                    stored.CodeGraphRuntimeBindingMatched,
                    stored.CodeGraphSnapshotBindingMatched,
                    stored.CodeGraphProjectPathTrusted,
                    stored.CodeGraphIndexPathTrusted,
                    stored.CodeGraphProofIdentityHash,
                    stored.CodeGraphObservedDurationMilliseconds));
            PreparedCanonicalArtifact java = PreparedScanIdentityBuilders.CreateJavaReadiness(
                new JavaReadinessProof(
                    stored.JavaReadinessRepositoryId,
                    stored.JavaReadinessScanRunId,
                    stored.JavaReadinessProfiledRevision,
                    stored.JavaReadinessSnapshotIdentity,
                    stored.JavaReadinessRuntimeIdentityHash,
                    stored.JavaReadinessWorkspaceBindingHash,
                    stored.JavaReadinessSchemaIdentity,
                    stored.JavaReadinessOutcome,
                    stored.JavaReadinessJdtLsRelease,
                    stored.JavaReadinessJdtLsDistributionArchiveHash,
                    stored.JavaReadinessJavaMajor,
                    stored.JavaReadinessStreamJsonRpcVersion,
                    stored.JavaReadinessTransport,
                    stored.JavaReadinessWorkspaceExclusive,
                    stored.JavaReadinessFile,
                    stored.JavaReadinessInitializeCompleted,
                    stored.JavaReadinessInitializedNotificationSent,
                    stored.JavaReadinessImportState,
                    stored.JavaReadinessDocumentSymbolCount,
                    stored.JavaReadinessRepositoryBindingMatched,
                    stored.JavaReadinessScanRunBindingMatched,
                    stored.JavaReadinessRevisionBindingMatched,
                    stored.JavaReadinessRuntimeBindingMatched,
                    stored.JavaReadinessSnapshotBindingMatched,
                    stored.JavaReadinessIdentityHash,
                    stored.JavaReadinessObservedDurationMilliseconds));
            return StringComparer.Ordinal.Equals(graph.Fingerprint, stored.CodeGraphProofIdentityHash) &&
                StringComparer.Ordinal.Equals(graph.Fingerprint, suppliedGraphIdentity) &&
                StringComparer.Ordinal.Equals(java.Fingerprint, stored.JavaReadinessIdentityHash) &&
                StringComparer.Ordinal.Equals(java.Fingerprint, suppliedJavaIdentity);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool StoredManifestFingerprintMatches(
        PreparedScanManifest stored,
        IReadOnlyList<PreparedScanManifestEntry> entries,
        IReadOnlyList<PreparedScanAudit> audits,
        IReadOnlyList<CommunicationKindClassification> classifications)
    {
        try
        {
            int classificationTotal = checked(
                stored.TotalConfirmedEvidenceCount + stored.TotalHintCount + stored.TotalWarningCount);
            PreparedCanonicalArtifact manifest = PreparedScanIdentityBuilders.ReconstructCanonical(
                "vulgata/story-12.2/prepared-scan-manifest/1.0",
                [
                    new(1, CanonicalEnvelope.Uuid(stored.RepositoryId)),
                    new(2, CanonicalEnvelope.Uuid(stored.ScanRunId)),
                    new(3, CanonicalEnvelope.Uuid(stored.CoordinationId)),
                    new(4, CanonicalEnvelope.String(stored.ProfiledRevision)),
                    new(5, CanonicalEnvelope.String(stored.RequestedRef)),
                    new(6, CanonicalEnvelope.String(stored.SchemaIdentity)),
                    new(7, CanonicalEnvelope.Bytes(Convert.FromHexString(stored.SnapshotIdentity))),
                    new(8, CanonicalEnvelope.Bytes(Convert.FromHexString(stored.RuntimeIdentityHash))),
                    new(9, CanonicalEnvelope.Bytes(Convert.FromHexString(stored.CodeGraphProofIdentityHash))),
                    new(10, CanonicalEnvelope.Bytes(Convert.FromHexString(stored.JavaReadinessIdentityHash))),
                    new(11, CanonicalEnvelope.String(stored.CatalogSchemaIdentity)),
                    new(12, CanonicalEnvelope.Bytes(Convert.FromHexString(stored.CatalogHash))),
                    new(13, CanonicalEnvelope.Bytes(Convert.FromHexString(stored.PreparedScanContextIdentityHash))),
                    new(14, CanonicalEnvelope.Bytes(Convert.FromHexString(stored.ContextContentHash))),
                    new(15, CanonicalEnvelope.UInt64((ulong)stored.CandidateCount)),
                    new(16, CanonicalEnvelope.UInt64((ulong)stored.EligibleCount)),
                    new(17, CanonicalEnvelope.UInt64((ulong)stored.FilteredCount)),
                    new(18, CanonicalEnvelope.UInt64((ulong)stored.SkippedCount)),
                    new(19, CanonicalEnvelope.UInt64((ulong)stored.SelectedCount)),
                    new(20, CanonicalEnvelope.UInt64((ulong)classificationTotal)),
                    new(21, CanonicalEnvelope.UInt64((ulong)stored.RetainedClassificationCount)),
                    new(22, CanonicalEnvelope.Boolean(stored.EvidenceTruncated)),
                    new(23, CanonicalEnvelope.Bytes(Convert.FromHexString(stored.PairSelectionFingerprint))),
                    new(24, CanonicalEnvelope.List(entries.Select(entry => CanonicalEnvelope.Object([
                        new(1, CanonicalEnvelope.Bytes(Convert.FromHexString(entry.FileCodeUnitIdentityFingerprint))),
                        new(2, CanonicalEnvelope.Int64(entry.DirectBusinessScore)),
                        new(3, CanonicalEnvelope.Int64(entry.FinalBusinessScore)),
                        new(4, CanonicalEnvelope.Enum(entry.SelectionReason.ToString())),
                        new(5, CanonicalEnvelope.UInt64((ulong)entry.ManifestOrdinal)),
                        new(6, CanonicalEnvelope.UInt64((ulong)entry.PairSelectionOrdinal)),
                    ])))),
                    new(25, CanonicalEnvelope.List(audits.Select(audit => CanonicalEnvelope.Object([
                        new(1, CanonicalEnvelope.UInt64((ulong)audit.Ordinal)),
                        new(2, CanonicalEnvelope.Bytes(Convert.FromHexString(audit.DetailFingerprint))),
                    ])))),
                    new(26, CanonicalEnvelope.List(classifications.Select(classification =>
                        CanonicalEnvelope.Bytes(Convert.FromHexString(classification.Fingerprint))))),
                    new(27, CanonicalEnvelope.Enum(stored.ApprovalState.ToString())),
                ]);
            return StringComparer.Ordinal.Equals(manifest.Fingerprint, stored.Fingerprint);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private void SetCodeGraphProof(PreparedScanManifest manifest, CodeGraphCompletenessProof proof)
    {
        Set(manifest, nameof(manifest.CodeGraphRepositoryId), proof.RepositoryId);
        Set(manifest, nameof(manifest.CodeGraphScanRunId), proof.ScanRunId);
        Set(manifest, nameof(manifest.CodeGraphProfiledRevision), proof.ProfiledRevision);
        Set(manifest, nameof(manifest.CodeGraphSnapshotIdentity), proof.SnapshotIdentity);
        Set(manifest, nameof(manifest.CodeGraphSnapshotRootBindingHash), proof.SnapshotRootBindingHash);
        Set(manifest, nameof(manifest.CodeGraphRuntimeIdentityHash), proof.RuntimeIdentityHash);
        Set(manifest, nameof(manifest.CodeGraphIndexIdentity), proof.IndexIdentity);
        Set(manifest, nameof(manifest.CodeGraphProofSchemaIdentity), proof.ProofSchemaIdentity);
        Set(manifest, nameof(manifest.CodeGraphInitialized), proof.Initialized);
        Set(manifest, nameof(manifest.CodeGraphIndexState), proof.IndexState);
        Set(manifest, nameof(manifest.CodeGraphPendingAdded), proof.PendingAdded);
        Set(manifest, nameof(manifest.CodeGraphPendingModified), proof.PendingModified);
        Set(manifest, nameof(manifest.CodeGraphPendingRemoved), proof.PendingRemoved);
        Set(manifest, nameof(manifest.CodeGraphWorktreeMismatch), proof.WorktreeMismatch);
        Set(manifest, nameof(manifest.CodeGraphReindexRecommended), proof.ReindexRecommended);
        Set(manifest, nameof(manifest.CodeGraphPendingReferences), proof.PendingReferences);
        Set(manifest, nameof(manifest.CodeGraphIndexedFileCount), proof.IndexedFileCount);
        Set(manifest, nameof(manifest.CodeGraphLogicalProjectBinding), proof.LogicalProjectBinding);
        Set(manifest, nameof(manifest.CodeGraphLogicalIndexBinding), proof.LogicalIndexBinding);
        Set(manifest, nameof(manifest.CodeGraphRepositoryBindingMatched), proof.RepositoryBindingMatched);
        Set(manifest, nameof(manifest.CodeGraphScanRunBindingMatched), proof.ScanRunBindingMatched);
        Set(manifest, nameof(manifest.CodeGraphRevisionBindingMatched), proof.RevisionBindingMatched);
        Set(manifest, nameof(manifest.CodeGraphRuntimeBindingMatched), proof.RuntimeBindingMatched);
        Set(manifest, nameof(manifest.CodeGraphSnapshotBindingMatched), proof.SnapshotBindingMatched);
        Set(manifest, nameof(manifest.CodeGraphProjectPathTrusted), proof.ProjectPathTrusted);
        Set(manifest, nameof(manifest.CodeGraphIndexPathTrusted), proof.IndexPathTrusted);
    }

    private void SetJavaReadinessProof(PreparedScanManifest manifest, JavaReadinessProof proof)
    {
        Set(manifest, nameof(manifest.JavaReadinessRepositoryId), proof.RepositoryId);
        Set(manifest, nameof(manifest.JavaReadinessScanRunId), proof.ScanRunId);
        Set(manifest, nameof(manifest.JavaReadinessProfiledRevision), proof.ProfiledRevision);
        Set(manifest, nameof(manifest.JavaReadinessSnapshotIdentity), proof.SnapshotIdentity);
        Set(manifest, nameof(manifest.JavaReadinessRuntimeIdentityHash), proof.RuntimeIdentityHash);
        Set(manifest, nameof(manifest.JavaReadinessWorkspaceBindingHash), proof.WorkspaceBindingHash);
        Set(manifest, nameof(manifest.JavaReadinessSchemaIdentity), proof.ReadinessSchemaIdentity);
        Set(manifest, nameof(manifest.JavaReadinessOutcome), proof.Outcome);
        Set(manifest, nameof(manifest.JavaReadinessJdtLsRelease), proof.JdtLsRelease);
        Set(manifest, nameof(manifest.JavaReadinessJdtLsDistributionArchiveHash), proof.JdtLsDistributionArchiveHash);
        Set(manifest, nameof(manifest.JavaReadinessJavaMajor), proof.JavaMajor);
        Set(manifest, nameof(manifest.JavaReadinessStreamJsonRpcVersion), proof.StreamJsonRpcVersion);
        Set(manifest, nameof(manifest.JavaReadinessTransport), proof.Transport);
        Set(manifest, nameof(manifest.JavaReadinessWorkspaceExclusive), proof.WorkspaceExclusive);
        Set(manifest, nameof(manifest.JavaReadinessFile), proof.ReadinessFile);
        Set(manifest, nameof(manifest.JavaReadinessInitializeCompleted), proof.InitializeCompleted);
        Set(manifest, nameof(manifest.JavaReadinessInitializedNotificationSent), proof.InitializedNotificationSent);
        Set(manifest, nameof(manifest.JavaReadinessImportState), proof.ImportState);
        Set(manifest, nameof(manifest.JavaReadinessDocumentSymbolCount), proof.DocumentSymbolCount);
        Set(manifest, nameof(manifest.JavaReadinessRepositoryBindingMatched), proof.RepositoryBindingMatched);
        Set(manifest, nameof(manifest.JavaReadinessScanRunBindingMatched), proof.ScanRunBindingMatched);
        Set(manifest, nameof(manifest.JavaReadinessRevisionBindingMatched), proof.RevisionBindingMatched);
        Set(manifest, nameof(manifest.JavaReadinessRuntimeBindingMatched), proof.RuntimeBindingMatched);
        Set(manifest, nameof(manifest.JavaReadinessSnapshotBindingMatched), proof.SnapshotBindingMatched);
    }

    private static bool EntryMatches(PreparedScanManifestEntry stored, PreparedFileCodeUnitDraft supplied) =>
        StringComparer.Ordinal.Equals(stored.FileCodeUnitIdentityFingerprint, supplied.IdentityFingerprint) &&
        stored.DirectBusinessScore == supplied.DirectBusinessScore &&
        stored.FinalBusinessScore == supplied.FinalBusinessScore &&
        stored.SelectionReason == supplied.SelectionReason &&
        stored.ManifestOrdinal == supplied.ManifestOrdinal &&
        stored.PairSelectionOrdinal == supplied.PairSelectionOrdinal;

    private static bool CodeUnitMatches(CodeUnit stored, PreparedFileCodeUnitDraft supplied)
    {
        if (stored.ByteStart is null || stored.ByteEndExclusive is null ||
            stored.SegmentOrdinal is null || stored.SegmentCount is null ||
            stored.FileHash is null || stored.FileCodeUnitIdentityFingerprint is null)
        {
            return false;
        }

        PreparedCanonicalArtifact reconstructed = PreparedScanIdentityBuilders.CreateApplicableFileCodeUnitIdentity(
            stored.RepositoryId, stored.ScanRunId, supplied.ProfiledRevision, stored.FilePath,
            stored.SymbolName,
            stored.FileHash, stored.ByteStart.Value, stored.ByteEndExclusive.Value,
            stored.LineStart, stored.LineEnd, stored.SegmentOrdinal.Value, stored.SegmentCount.Value,
            stored.SourceHash);
        return stored.RepositoryId == supplied.RepositoryId && stored.ScanRunId == supplied.ScanRunId &&
            StringComparer.Ordinal.Equals(stored.FilePath, supplied.RelativePath) &&
            StringComparer.Ordinal.Equals(stored.SymbolName, supplied.SymbolName) &&
            StringComparer.Ordinal.Equals(stored.FileHash, supplied.FileHash) &&
            stored.ByteStart == supplied.ByteStart && stored.ByteEndExclusive == supplied.ByteEndExclusive &&
            stored.LineStart == supplied.LineStart && stored.LineEnd == supplied.LineEnd &&
            stored.SegmentOrdinal == supplied.SegmentOrdinal && stored.SegmentCount == supplied.SegmentCount &&
            StringComparer.Ordinal.Equals(stored.SourceHash, supplied.SourceHash) &&
            StringComparer.Ordinal.Equals(stored.Language, supplied.Language) &&
            StringComparer.Ordinal.Equals(reconstructed.Fingerprint, stored.FileCodeUnitIdentityFingerprint) &&
            StringComparer.Ordinal.Equals(reconstructed.Fingerprint, supplied.IdentityFingerprint);
    }

    private static bool ClassificationsMatch(
        IReadOnlyList<CommunicationKindClassification> stored,
        IReadOnlyList<CommunicationKindClassification> supplied)
    {
        if (stored.Count != supplied.Count)
        {
            return false;
        }

        CommunicationKindClassification[] orderedStored =
            PreparedScanIdentityBuilders.OrderCommunicationClassifications(stored);
        CommunicationKindClassification[] orderedSupplied =
            PreparedScanIdentityBuilders.OrderCommunicationClassifications(supplied);
        bool suppliedIsCanonical = supplied
            .Select(value => value.Fingerprint)
            .SequenceEqual(orderedSupplied.Select(value => value.Fingerprint), StringComparer.Ordinal);
        return suppliedIsCanonical && orderedStored
            .Select((value, index) => ClassificationMatches(value, orderedSupplied[index]))
            .All(value => value);
    }

    private static bool ClassificationMatches(
        CommunicationKindClassification stored,
        CommunicationKindClassification supplied) =>
        stored.Id == supplied.Id && stored.PreScanResultId == supplied.PreScanResultId &&
        stored.Kind == supplied.Kind && stored.Level == supplied.Level &&
        stored.Confidence == supplied.Confidence && stored.Rule == supplied.Rule &&
        stored.Provenance == supplied.Provenance &&
        StringComparer.Ordinal.Equals(stored.RelativePath, supplied.RelativePath) &&
        StringComparer.Ordinal.Equals(stored.Locator, supplied.Locator) &&
        stored.OccurrenceCount == supplied.OccurrenceCount &&
        StringComparer.Ordinal.Equals(stored.Fingerprint, supplied.Fingerprint) &&
        StringComparer.Ordinal.Equals(stored.CatalogRuleIdentity, supplied.CatalogRuleIdentity) &&
        stored.CatalogRuleIdentity is not null &&
        StringComparer.Ordinal.Equals(
            PreparedScanIdentityBuilders.CreateCommunicationClassification(
                stored.PreScanResultId, stored.Kind, stored.Level, stored.Confidence,
                stored.Rule, stored.Provenance, stored.RelativePath, stored.Locator,
                stored.OccurrenceCount, stored.CatalogRuleIdentity).Fingerprint,
            stored.Fingerprint);

    private static T NewEntity<T>() where T : class =>
        (T)(Activator.CreateInstance(typeof(T), nonPublic: true) ??
            throw new InvalidOperationException($"Cannot create {typeof(T).Name}."));

    private void Set<T>(T entity, string propertyName, object? value) where T : class =>
        context.Entry(entity).Property(propertyName).CurrentValue = value;
}

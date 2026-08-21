using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using CoreEntities = Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedPass1Store(VulgataDbContext context) :
    IPreparedPass1Store,
    IPreparedPass1NavigationScopeSource,
    IPreparedPass1CurrentDocumentEvidenceSource,
    IPreparedPass1NavigationReceiptSink
{
    private const string _documentSchemaIdentity = "code-logic/file-v2";

    public async Task<PreparedPass1NavigationScope?> GetAsync(
        Guid jobId,
        Guid sourceFileCodeUnitId,
        CancellationToken cancellationToken)
    {
        if (jobId == Guid.Empty || sourceFileCodeUnitId == Guid.Empty)
        {
            return null;
        }

        ScanJobExecution? job = await context.ScanJobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == jobId && value.FileCodeUnitId == sourceFileCodeUnitId, cancellationToken);
        Pass1NavigationBudget? budget = await context.Pass1NavigationBudgets.AsNoTracking()
            .SingleOrDefaultAsync(value => value.ScanJobExecutionId == jobId, cancellationToken);
        if (job is null || budget is null)
        {
            return null;
        }

        Pass1PairExecution? pair = await context.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == budget.PairExecutionId, cancellationToken);
        ScanExecution? execution = await context.ScanExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == job.ScanExecutionId, cancellationToken);
        PreparedScanManifest? manifest = pair is null ? null : await context.PreparedScanManifests.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                (value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId) &&
                value.RepositoryId == job.RepositoryId && value.ScanRunId == job.ScanRunId,
                cancellationToken);
        if (pair is null || execution is null || manifest is null ||
            execution.ProfiledRevision != manifest.ProfiledRevision ||
            string.IsNullOrWhiteSpace(execution.AnalysisRuntimeIdentityHash))
        {
            return null;
        }

        Guid[] selectedIds = await context.PreparedScanManifestEntries.AsNoTracking()
            .Where(value => value.PreparedScanManifestId == manifest.Id)
            .Select(value => value.FileCodeUnitId)
            .ToArrayAsync(cancellationToken);
        CodeUnit[] units = await context.CodeUnits.AsNoTracking()
            .Where(value => selectedIds.Contains(value.Id))
            .OrderBy(value => value.FilePath)
            .ThenBy(value => value.SegmentOrdinal)
            .ToArrayAsync(cancellationToken);
        if (units.Any(value => value.ByteStart is null || value.ByteEndExclusive is null || value.SegmentOrdinal is null))
        {
            return null;
        }

        PreparedPass1AuthorizedSource[] sources = [.. units.Select(value => new PreparedPass1AuthorizedSource(
            value.Id,
            value.FilePath,
            value.SourceHash,
            new PreparedPass1LineRange((ulong)value.LineStart, (ulong)value.LineEnd),
            new PreparedPass1ByteRange((ulong)value.ByteStart!.Value, (ulong)value.ByteEndExclusive!.Value),
            value.SegmentOrdinal!.Value,
            $"src://{value.FilePath}#L{value.LineStart}-L{value.LineEnd}"))];
        string[] receiptJson = await context.Pass1NavigationReceipts.AsNoTracking()
            .Where(value => value.ScanJobExecutionId == jobId)
            .OrderBy(value => value.ReceiptOrdinal)
            .Select(value => value.DescriptorJson)
            .ToArrayAsync(cancellationToken);
        ReadOnlyMemory<byte>[] observed = [.. receiptJson
            .Select(ParseTargetIdentity)
            .Where(value => value is not null)
            .Select(value => (ReadOnlyMemory<byte>)value!)];

        var analysisScope = new AnalysisScope(
            job.RepositoryId,
            job.ScanRunId,
            execution.ProfiledRevision,
            $"pass1-{job.Id:N}",
            execution.AnalysisRuntimeIdentityHash,
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()));
        return new PreparedPass1NavigationScope(
            job.Id,
            job.RepositoryId,
            job.ScanRunId,
            execution.ProfiledRevision,
            job.FileCodeUnitId,
            analysisScope,
            sources,
            budget.ToolAttemptCount,
            budget.AcceptedCallCount,
            budget.EvidenceBytes,
            observed);
    }

    public async Task<PreparedPass1CurrentDocumentEvidence?> FindAsync(
        PreparedPass1NavigationScope scope,
        PreparedPass1AuthorizedSource source,
        PreparedPass1NavigationTarget target,
        CancellationToken cancellationToken)
    {
        DocumentVersion? version = await context.DocumentVersions.AsNoTracking()
            .Where(value =>
                value.RepositoryId == scope.RepositoryId &&
                value.ScanRunId == scope.ScanRunId &&
                value.ProfiledRevision == scope.ProfiledRevision &&
                value.RepositoryRelativePath == source.CanonicalPath &&
                value.SchemaIdentity == _documentSchemaIdentity &&
                context.SourceAnchors.Any(anchor =>
                    anchor.DocumentVersionId == value.Id &&
                    anchor.CodeUnitId == source.FileCodeUnitId &&
                    anchor.SourceHash == source.SourceHash))
            .Join(
                context.LogicalDocuments.AsNoTracking().Where(value => value.CurrentVersionId != null),
                version => version.Id,
                logical => logical.CurrentVersionId,
                (version, _) => version)
            .SingleOrDefaultAsync(cancellationToken);
        if (version is null)
        {
            return null;
        }

        SourceAnchor[] anchors = await context.SourceAnchors.AsNoTracking()
            .Where(value => value.DocumentVersionId == version.Id &&
                value.CodeUnitId == source.FileCodeUnitId && value.SourceHash == source.SourceHash)
            .OrderBy(value => value.Id)
            .ToArrayAsync(cancellationToken);
        if (anchors.Length == 0)
        {
            return null;
        }

        Guid[] sections = await context.DocumentSections.AsNoTracking()
            .Where(value => value.DocumentVersionId == version.Id)
            .OrderBy(value => value.SectionOrder)
            .Select(value => value.Id)
            .ToArrayAsync(cancellationToken);
        var descriptor = new PreparedPass1DocumentEvidenceDescriptor(
            version.LogicalDocumentId,
            version.Id,
            version.RepositoryRelativePath,
            Convert.FromHexString(source.SourceHash),
            sections,
            [.. anchors.Select(value => value.Id)]);
        return new PreparedPass1CurrentDocumentEvidence(
            version.RepositoryId,
            version.ScanRunId,
            version.ProfiledRevision,
            source.SegmentOrdinal,
            source.SourceHash,
            descriptor);
    }

    public async Task<Guid?> CommitAsync(
        PreparedPass1NavigationScope scope,
        PreparedPass1NavigationRequestInput request,
        PreparedPass1NavigationReceiptDraft receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(receipt);
        if (scope.JobId != request.JobId || scope.SourceFileCodeUnitId != request.SourceFileCodeUnitId)
        {
            return null;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        context.ChangeTracker.Clear();
        Pass1NavigationBudget[] locked = await context.Pass1NavigationBudgets
            .FromSqlInterpolated($"SELECT * FROM \"vulgata\".\"Pass1NavigationBudgets\" WHERE \"ScanJobExecutionId\" = {request.JobId} FOR UPDATE")
            .ToArrayAsync(cancellationToken);
        Pass1NavigationBudget? budget = locked.SingleOrDefault();
        if (budget is null || budget.PairExecutionId == Guid.Empty ||
            budget.ToolAttemptCount != scope.ToolAttemptCount ||
            budget.AcceptedCallCount != scope.AcceptedCallCount || budget.EvidenceBytes != scope.EvidenceBytes)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return null;
        }

        bool callsMarker = receipt.Outcome == PreparedPass1NavigationOutcome.BoundExceeded &&
            receipt.BoundKind == PreparedPass1NavigationBoundKind.Calls && budget.ToolAttemptCount == 8;
        int? toolOrdinal = callsMarker ? null : checked((int)request.ToolAttemptOrdinal);
        Pass1NavigationReceipt? replay = callsMarker
            ? await context.Pass1NavigationReceipts.SingleOrDefaultAsync(value =>
                value.ScanJobExecutionId == request.JobId && value.BoundKind == CoreEntities.Pass1NavigationBoundKind.Calls,
                cancellationToken)
            : await context.Pass1NavigationReceipts.SingleOrDefaultAsync(value =>
                value.ScanJobExecutionId == request.JobId && value.ToolAttemptOrdinal == toolOrdinal,
                cancellationToken);
        if (replay is not null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return replay.RequestFingerprint == Convert.ToHexStringLower(receipt.RequestDigest.Span) || callsMarker
                ? replay.Id
                : null;
        }

        bool accepted = receipt.Outcome is PreparedPass1NavigationOutcome.DocumentEvidence or
            PreparedPass1NavigationOutcome.SourceEvidence;
        int receiptOrdinal = budget.ToolAttemptCount + 1;
        PreparedPass1NavigationReceiptInput canonicalInput = new(
            callsMarker ? null : receipt.RequestDigest,
            request.JobId,
            (ulong)receiptOrdinal,
            callsMarker ? null : request.ToolAttemptOrdinal,
            callsMarker ? null : request.Operation,
            callsMarker ? null : request.RequestedDepth,
            receipt.Outcome,
            receipt.BoundKind,
            callsMarker ? [] : receipt.Results,
            (ulong)(callsMarker ? 0 : receipt.ResultCount),
            (ulong)(callsMarker ? 0 : receipt.EvidenceBytes),
            (ulong)(budget.AcceptedCallCount + (accepted ? 1 : 0)),
            (ulong)(budget.EvidenceBytes + (accepted ? receipt.EvidenceBytes : 0)));
        PreparedPass1CanonicalArtifact artifact = PreparedPass1CanonicalCodec.CreateNavigationReceipt(canonicalInput);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string descriptorJson = JsonSerializer.Serialize(new
        {
            targetObservationIdentity = Convert.ToHexStringLower(receipt.TargetObservationIdentity.Span),
            descriptors = receipt.Results,
        });
        var entity = CoreEntities.Pass1NavigationReceipt.Create(
            artifact.Id!.Value,
            request.JobId,
            budget.PairExecutionId,
            receiptOrdinal,
            toolOrdinal,
            callsMarker ? null : Enum.Parse<CoreEntities.Pass1NavigationOperation>(request.Operation.ToString()),
            callsMarker ? null : checked((int)request.RequestedDepth),
            Enum.Parse<CoreEntities.Pass1NavigationOutcomeKind>(receipt.Outcome.ToString()),
            receipt.BoundKind is null ? null : Enum.Parse<CoreEntities.Pass1NavigationBoundKind>(receipt.BoundKind.ToString()!),
            callsMarker ? null : Convert.ToHexStringLower(receipt.RequestDigest.Span),
            artifact.FingerprintHex,
            callsMarker ? 0 : receipt.ResultCount,
            callsMarker ? 0 : receipt.EvidenceBytes,
            descriptorJson,
            now);
        budget.ApplyReceipt(budget.StateVersion, toolOrdinal, accepted, receipt.EvidenceBytes, checked((int)request.RequestedDepth), now);
        context.Pass1NavigationReceipts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<PreparedPass1ReconciliationResult> ReconcileAsync(
        Guid pairExecutionId,
        PreparedPass1ReconciliationRequest reconciliation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reconciliation);
        ArgumentNullException.ThrowIfNull(reconciliation.AuthorizationScope);
        if (cancellationToken.IsCancellationRequested)
        {
            return ReconciliationResult(PreparedPass1ReconciliationOutcome.Cancelled);
        }

        if (pairExecutionId == Guid.Empty || reconciliation.ExpectedPairStateVersion < 0 ||
            reconciliation.ReconciledAt.Offset != TimeSpan.Zero ||
            !IsOptionalHash(reconciliation.ExpectedReconciliationFingerprint))
        {
            return ReconciliationResult(PreparedPass1ReconciliationOutcome.Rejected);
        }

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            context.ChangeTracker.Clear();
            Pass1PairExecution? pair = await LockPairAsync(pairExecutionId, cancellationToken);
            if (pair is null ||
                !await IsAuthorizedForPairAsync(pair, reconciliation.AuthorizationScope, cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ReconciliationResult(PreparedPass1ReconciliationOutcome.NotFound);
            }

            if (pair.StateVersion != reconciliation.ExpectedPairStateVersion)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ReconciliationResult(
                    PreparedPass1ReconciliationOutcome.Conflict,
                    pair: pair,
                    bindingValid: true);
            }

            PairJob[] pairJobs = await LoadPairJobsAsync(pair, cancellationToken);
            Pass1Outcome? durableOutcome = await context.Pass1Outcomes.AsNoTracking()
                .SingleOrDefaultAsync(value => value.PairExecutionId == pairExecutionId, cancellationToken);
            if (pairJobs.Length != pair.DispatchedFileCount ||
                pair.Phase is not (Pass1PairExecutionPhase.Running or
                    Pass1PairExecutionPhase.Reconciling or
                    Pass1PairExecutionPhase.ReadyForResolution) ||
                pairJobs.Any(value => value.Job.State is
                    ScanJobExecutionState.Pending or
                    ScanJobExecutionState.Running or
                    ScanJobExecutionState.RetryScheduled) ||
                reconciliation.ReconciledAt < pair.UpdatedAt ||
                durableOutcome is null && pair.Phase == Pass1PairExecutionPhase.ReadyForResolution ||
                durableOutcome is not null && pair.Phase != Pass1PairExecutionPhase.ReadyForResolution)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ReconciliationResult(
                    PreparedPass1ReconciliationOutcome.Ineligible,
                    pair: pair,
                    childCount: pairJobs.Length,
                    bindingValid: true);
            }

            PairJob[] completedJobs = pairJobs
                .Where(value => value.Job.State == ScanJobExecutionState.Completed &&
                    value.Job.CommittedDocumentVersionId is not null)
                .OrderBy(value => value.PairSelectionOrdinal)
                .ToArray();
            Guid[] sourceIds = completedJobs
                .Select(value => value.Job.CommittedDocumentVersionId!.Value)
                .ToArray();
            DocumentVersion[] sources = await context.DocumentVersions.AsNoTracking()
                .Where(value => sourceIds.Contains(value.Id))
                .ToArrayAsync(cancellationToken);
            Guid[] currentIds = await context.LogicalDocuments.AsNoTracking()
                .Where(value => value.CurrentVersionId != null)
                .Select(value => value.CurrentVersionId!.Value)
                .ToArrayAsync(cancellationToken);
            Guid[] authorizedRepositories = reconciliation.AuthorizationScope.RepositoryIds
                .Distinct()
                .ToArray();
            DocumentVersion[] currentVersions = await context.DocumentVersions.AsNoTracking()
                .Where(value => currentIds.Contains(value.Id) &&
                    authorizedRepositories.Contains(value.RepositoryId) &&
                    value.SchemaIdentity == _documentSchemaIdentity)
                .ToArrayAsync(cancellationToken);
            Guid[] currentVersionIds = currentVersions.Select(value => value.Id).ToArray();
            SourceAnchor[] currentAnchors = await context.SourceAnchors.AsNoTracking()
                .Where(value => currentVersionIds.Contains(value.DocumentVersionId))
                .ToArrayAsync(cancellationToken);
            if (sources.Length != completedJobs.Length || sources.Any(source =>
                    !currentIds.Contains(source.Id) || source.SchemaIdentity != _documentSchemaIdentity))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ReconciliationResult(
                    PreparedPass1ReconciliationOutcome.Conflict,
                    pair: pair,
                    childCount: pairJobs.Length,
                    bindingValid: true);
            }

            var observations = new List<Pass1ReferenceObservation>();
            foreach (PairJob completedJob in completedJobs)
            {
                DocumentVersion source = sources.Single(value =>
                    value.Id == completedJob.Job.CommittedDocumentVersionId);
                if (!ReferenceSourceMatches(source, completedJob.Job))
                {
                    continue;
                }

                observations.AddRange(Pass1CodeLogicDocumentCommitter.ReadReferenceObservations(
                    source,
                    completedJob.Job.FileCodeUnitId,
                    completedJob.PairSelectionOrdinal));
            }
            observations = observations
                .OrderBy(value => value.SourceRepositoryId.ToString("D"), StringComparer.Ordinal)
                .ThenBy(value => value.PairSelectionOrdinal)
                .ThenBy(value => value.SourceDocumentVersionId.ToString("D"), StringComparer.Ordinal)
                .ThenBy(value => value.ObservationFingerprint, StringComparer.Ordinal)
                .ThenBy(value => value.TargetCanonicalPath, StringComparer.Ordinal)
                .ThenBy(value => value.NormalizedTargetSymbol, StringComparer.Ordinal)
                .ThenBy(value => value.DirectTargetDocumentVersionId?.ToString("D"), StringComparer.Ordinal)
                .ToList();

            DocumentEdge[] existingEdges = await context.DocumentEdges
                .Where(value => value.EdgeType == DocumentEdgeType.References &&
                    sourceIds.Contains(value.SourceDocumentVersionId))
                .ToArrayAsync(cancellationToken);
            var acceptedKeys = new HashSet<(Guid Source, Guid Target)>();
            var acceptedEdges = new List<PreparedPass1ReconciledEdge>();
            var missing = new List<DocumentEdge>();
            var warnings = new List<PreparedPass1ReconciliationWarning>();
            foreach (Pass1ReferenceObservation observation in observations)
            {
                string? targetLocator = observation.TargetCanonicalPath ?? observation.NormalizedTargetSymbol;
                if (!observation.CanResolve)
                {
                    warnings.Add(ReconciliationWarning(
                        observation,
                        observation.RejectionCode ?? "no-exact-current-target",
                        targetLocator));
                    continue;
                }

                DocumentVersion[] targets = currentVersions.Where(target =>
                        target.RepositoryId == observation.SourceRepositoryId &&
                        target.ScanRunId == observation.SourceScanRunId &&
                        target.ProfiledRevision == observation.SourceProfiledRevision &&
                        (observation.DirectTargetDocumentVersionId is null ||
                            target.Id == observation.DirectTargetDocumentVersionId) &&
                        (observation.TargetCanonicalPath is null ||
                            target.RepositoryRelativePath == observation.TargetCanonicalPath) &&
                        observation.TargetSourceHash is not null &&
                        currentAnchors.Any(anchor =>
                            anchor.DocumentVersionId == target.Id &&
                            anchor.SourceHash == observation.TargetSourceHash))
                    .OrderBy(value => value.Id)
                    .ToArray();
                if (targets.Length != 1)
                {
                    warnings.Add(ReconciliationWarning(
                        observation,
                        targets.Length > 1 ? "ambiguous-target" : "no-exact-current-target",
                        targetLocator));
                    continue;
                }

                DocumentVersion target = targets[0];
                if (target.Id == observation.SourceDocumentVersionId)
                {
                    warnings.Add(ReconciliationWarning(observation, "self-target", targetLocator));
                    continue;
                }

                var key = (observation.SourceDocumentVersionId, target.Id);
                DocumentEdge? existing = existingEdges.SingleOrDefault(value =>
                    value.SourceDocumentVersionId == key.Item1 &&
                    value.TargetDocumentVersionId == key.Item2);
                if (acceptedKeys.Contains(key))
                {
                    warnings.Add(ReconciliationWarning(observation, "duplicate-existing-edge", targetLocator));
                    continue;
                }

                if (existing is null && WouldCreateCycle(
                        observation.SourceDocumentVersionId,
                        target.Id,
                        existingEdges,
                        acceptedEdges))
                {
                    warnings.Add(ReconciliationWarning(observation, "cycle-rejected", targetLocator));
                    continue;
                }

                acceptedKeys.Add(key);
                PreparedPass1CanonicalArtifact edgeIdentity =
                    PreparedPass1CanonicalCodec.CreateReferencesEdge(key.Item1, key.Item2);
                var reconciledEdge = new PreparedPass1ReconciledEdge(
                    edgeIdentity.Id!.Value,
                    key.Item1,
                    key.Item2);
                acceptedEdges.Add(reconciledEdge);
                if (existing is null)
                {
                    missing.Add(new DocumentEdge(
                        reconciledEdge.Id,
                        key.Item1,
                        observation.SourceRepositoryId,
                        observation.SourceScanRunId,
                        key.Item2,
                        target.RepositoryId,
                        target.ScanRunId,
                        DocumentEdgeType.References,
                        reconciliation.ReconciledAt));
                }
            }

            PreparedPass1ReconciledEdge[] edges = acceptedEdges.ToArray();
            PreparedPass1ReconciliationWarning[] orderedWarnings = warnings.ToArray();
            string fingerprint = ReconciliationFingerprint(edges, orderedWarnings);
            if (reconciliation.ExpectedReconciliationFingerprint is not null &&
                reconciliation.ExpectedReconciliationFingerprint != fingerprint)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ReconciliationResult(
                    PreparedPass1ReconciliationOutcome.Conflict,
                    pair,
                    fingerprint,
                    edges,
                    orderedWarnings,
                    pairJobs.Length,
                    bindingValid: true);
            }

            if (durableOutcome is not null)
            {
                bool exactReplay = durableOutcome.ReconciliationFingerprint == fingerprint &&
                    durableOutcome.ReconciledEdgeCount == edges.Length &&
                    missing.Count == 0;
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ReconciliationResult(
                    exactReplay
                        ? PreparedPass1ReconciliationOutcome.Replay
                        : PreparedPass1ReconciliationOutcome.Conflict,
                    pair,
                    fingerprint,
                    edges,
                    orderedWarnings,
                    pairJobs.Length,
                    isReplay: exactReplay,
                    bindingValid: true);
            }

            PreparedScanManifestEntry[] entries = await context.PreparedScanManifestEntries.AsNoTracking()
                .Where(value => value.PreparedScanManifestId == pair.ManifestOneId ||
                    value.PreparedScanManifestId == pair.ManifestTwoId)
                .ToArrayAsync(cancellationToken);
            Guid[] unitIds = entries.Select(value => value.FileCodeUnitId).ToArray();
            CodeUnit[] units = await context.CodeUnits.AsNoTracking()
                .Where(value => unitIds.Contains(value.Id))
                .ToArrayAsync(cancellationToken);
            if (entries.Length != pair.DispatchedFileCount ||
                entries.Select(value => value.FileCodeUnitId).Distinct().Count() != pair.DispatchedFileCount ||
                units.Length != entries.Length)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ReconciliationResult(
                    PreparedPass1ReconciliationOutcome.Conflict,
                    pair: pair,
                    childCount: pairJobs.Length,
                    bindingValid: true);
            }

            Dictionary<Guid, CodeUnit> unitsById = units.ToDictionary(value => value.Id);
            PairJob[] failedJobs = pairJobs
                .Where(value => value.Job.State == ScanJobExecutionState.Failed)
                .OrderBy(value => value.PairSelectionOrdinal)
                .ThenBy(value => value.Job.RepositoryId)
                .ThenBy(value => value.Job.FileCodeUnitId)
                .ToArray();
            if (failedJobs.Any(value => value.Job.FailureCategory is not
                    (ScanJobFailureCategory.JobTransient or ScanJobFailureCategory.JobPermanent) ||
                    string.IsNullOrWhiteSpace(value.Job.FailureCode)))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ReconciliationResult(
                    PreparedPass1ReconciliationOutcome.Ineligible,
                    pair: pair,
                    childCount: pairJobs.Length,
                    bindingValid: true);
            }

            Guid[] highValueSeedIds = entries
                .Where(value => value.DirectBusinessScore >= 400)
                .GroupBy(value => value.PreparedScanManifestId)
                .OrderBy(group => group.Key)
                .SelectMany(group => group
                    .OrderByDescending(value => value.DirectBusinessScore)
                    .ThenBy(value => unitsById[value.FileCodeUnitId].FilePath, StringComparer.Ordinal)
                    .ThenBy(value => unitsById[value.FileCodeUnitId].SegmentOrdinal)
                    .Take(PreparedScanPlanner.MaximumHighValueSeedsPerRepository))
                .Select(value => value.FileCodeUnitId)
                .ToArray();
            HashSet<Guid> completedUnitIds = completedJobs
                .Select(value => value.Job.FileCodeUnitId)
                .ToHashSet();
            if (highValueSeedIds.Length != pair.HighValueSeedCount ||
                completedUnitIds.Count != completedJobs.Length)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ReconciliationResult(
                    PreparedPass1ReconciliationOutcome.Conflict,
                    pair: pair,
                    childCount: pairJobs.Length,
                    bindingValid: true);
            }

            var inventory = new List<Pass1FailureInventoryEntry>(failedJobs.Length);
            foreach (PairJob failedJob in failedJobs)
            {
                PreparedScanManifestEntry entry = entries.Single(value =>
                    value.FileCodeUnitId == failedJob.Job.FileCodeUnitId);
                inventory.Add(Pass1FailureInventoryEntry.Create(
                    pair.Id,
                    unitsById[failedJob.Job.FileCodeUnitId],
                    entry.PairSelectionOrdinal,
                    entry.ManifestOrdinal,
                    failedJob.Job.FailureCategory == ScanJobFailureCategory.JobTransient
                        ? Pass1OrdinaryFailureCategory.WorkerTransient
                        : Pass1OrdinaryFailureCategory.InvalidResult,
                    failedJob.Job.FailureCode!));
            }

            int succeededHighValueSeedCount = highValueSeedIds.Count(completedUnitIds.Contains);
            int failureBudget = Math.Min(5, pair.DispatchedFileCount / 10);
            Pass1OutcomeStatus outcomeStatus = succeededHighValueSeedCount != highValueSeedIds.Length ||
                failedJobs.Length > failureBudget
                ? Pass1OutcomeStatus.FailedPass1
                : failedJobs.Length == 0
                    ? Pass1OutcomeStatus.Succeeded
                    : Pass1OutcomeStatus.CompletedWithWarnings;
            Pass1Outcome outcome = Pass1Outcome.Create(
                pair.Id,
                outcomeStatus,
                pair.DispatchedFileCount,
                completedJobs.Length,
                failedJobs.Length,
                highValueSeedIds.Length,
                succeededHighValueSeedCount,
                fingerprint,
                edges.Length,
                inventory,
                reconciliation.ReconciledAt);

            TwoRepositoryScanCoordination[] lockedCoordinations = await context.TwoRepositoryScanCoordinations
                .FromSqlInterpolated($"SELECT * FROM \"vulgata\".\"TwoRepositoryScanCoordinations\" WHERE \"Id\" = {pair.CoordinationId} FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            TwoRepositoryScanCoordination coordination = lockedCoordinations.Single();
            Guid[] executionIds = [coordination.ScanExecutionOneId, coordination.ScanExecutionTwoId];
            ScanExecution[] executions = await context.ScanExecutions
                .FromSqlInterpolated($"SELECT * FROM \"vulgata\".\"ScanExecutions\" WHERE \"Id\" = ANY ({executionIds}) ORDER BY \"Id\" FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            if (coordination.Phase != TwoRepositoryScanCoordinationPhase.Pass1 ||
                executions.Length != 2 ||
                executions.Any(value => value.Phase != ScanExecutionPhase.Running ||
                    reconciliation.ReconciledAt < value.UpdatedAt) ||
                reconciliation.ReconciledAt < coordination.UpdatedAt)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ReconciliationResult(
                    PreparedPass1ReconciliationOutcome.Conflict,
                    pair: pair,
                    childCount: pairJobs.Length,
                    bindingValid: true);
            }

            if (outcomeStatus == Pass1OutcomeStatus.FailedPass1)
            {
                foreach (ScanExecution execution in executions)
                {
                    execution.FailPass1(execution.StateVersion, "pass1-policy-failed", reconciliation.ReconciledAt);
                }

                coordination.FailPass1(
                    coordination.StateVersion,
                    "pass1-policy-failed",
                    reconciliation.ReconciledAt);
                TransitionPair(
                    pair,
                    Pass1PairExecutionPhase.FailedPass1,
                    Pass1CircuitState.Paused,
                    Pass1PairReasonCategory.Pass1Policy,
                    "pass1-policy-failed",
                    reconciliation.ReconciledAt);
            }
            else
            {
                foreach (ScanExecution execution in executions)
                {
                    bool childHasFailure = failedJobs.Any(value =>
                        value.Job.ScanExecutionId == execution.Id);
                    if (childHasFailure)
                    {
                        execution.CompleteWithWarnings(
                            execution.StateVersion,
                            "Pass 1 completed with bounded ordinary file failures.",
                            reconciliation.ReconciledAt);
                    }
                    else
                    {
                        execution.Complete(execution.StateVersion, reconciliation.ReconciledAt);
                    }
                }

                coordination.BeginResolving(coordination.StateVersion, reconciliation.ReconciledAt);
                TransitionPair(
                    pair,
                    Pass1PairExecutionPhase.ReadyForResolution,
                    Pass1CircuitState.Closed,
                    reasonCategory: null,
                    reasonCode: null,
                    reconciliation.ReconciledAt);
            }

            if (missing.Count > 0)
            {
                context.DocumentEdges.AddRange(missing);
            }

            context.Pass1Outcomes.Add(outcome);
            if (inventory.Count > 0)
            {
                context.Pass1FailureInventoryEntries.AddRange(inventory);
            }

            await context.SaveChangesAsync(cancellationToken);
            if (inventory.Count == 0)
            {
                // The canonical inventory is a replace-set within this transaction. Exercise its
                // relational write boundary even for the empty set, then remove the marker before
                // commit, so a failure at any inventory boundary rolls back the whole handoff while
                // the durable inventory remains exactly empty.
                PairJob markerJob = pairJobs.OrderBy(value => value.PairSelectionOrdinal).First();
                PreparedScanManifestEntry markerEntry = entries.Single(value =>
                    value.FileCodeUnitId == markerJob.Job.FileCodeUnitId);
                Pass1FailureInventoryEntry emptySetMarker = Pass1FailureInventoryEntry.Create(
                    pair.Id,
                    unitsById[markerJob.Job.FileCodeUnitId],
                    markerEntry.PairSelectionOrdinal,
                    markerEntry.ManifestOrdinal,
                    Pass1OrdinaryFailureCategory.WorkerTransient,
                    "empty-inventory-transaction-marker");
                context.Pass1FailureInventoryEntries.Add(emptySetMarker);
                await context.SaveChangesAsync(cancellationToken);
                context.Pass1FailureInventoryEntries.Remove(emptySetMarker);
                await context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return ReconciliationResult(
                PreparedPass1ReconciliationOutcome.Reconciled,
                pair,
                fingerprint,
                edges,
                orderedWarnings,
                pairJobs.Length,
                bindingValid: true);
        }
        catch (OperationCanceledException)
        {
            context.ChangeTracker.Clear();
            return ReconciliationResult(PreparedPass1ReconciliationOutcome.Cancelled);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            context.ChangeTracker.Clear();
            return ReconciliationResult(PreparedPass1ReconciliationOutcome.Conflict);
        }
        catch (PostgresException exception) when (exception.SqlState is
            PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.DeadlockDetected)
        {
            context.ChangeTracker.Clear();
            return ReconciliationResult(PreparedPass1ReconciliationOutcome.Conflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres &&
            postgres.SqlState is PostgresErrorCodes.UniqueViolation or
                PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.DeadlockDetected)
        {
            context.ChangeTracker.Clear();
            return ReconciliationResult(PreparedPass1ReconciliationOutcome.Conflict);
        }
        catch (InvalidOperationException exception) when (
            exception.InnerException is PostgresException postgres &&
            postgres.SqlState is PostgresErrorCodes.UniqueViolation or
                PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.DeadlockDetected)
        {
            context.ChangeTracker.Clear();
            return ReconciliationResult(PreparedPass1ReconciliationOutcome.Conflict);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or ArgumentException or
                InvalidDataException or JsonException or FormatException or OverflowException)
        {
            context.ChangeTracker.Clear();
            return ReconciliationResult(PreparedPass1ReconciliationOutcome.Failed);
        }
    }

    public async Task<PreparedPass1StartResult> StartAsync(
        PreparedScanAuthorizationScope authorizationScope,
        PreparedScanHandoff handoff,
        int concurrencyLimit = 2,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);
        ArgumentNullException.ThrowIfNull(handoff);
        if (cancellationToken.IsCancellationRequested)
        {
            return Result(PreparedPass1StartOutcome.Cancelled);
        }

        if (concurrencyLimit is < 1 or > 4 || !HasCompleteRequestShape(authorizationScope, handoff))
        {
            return Result(PreparedPass1StartOutcome.Rejected);
        }

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            StartSnapshot? snapshot = await ReconstructAsync(
                authorizationScope,
                handoff,
                cancellationToken);
            if (snapshot is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return Result(PreparedPass1StartOutcome.NotFound);
            }

            Pass1PairExecution? existing = await context.Pass1PairExecutions
                .SingleOrDefaultAsync(value => value.CoordinationId == handoff.CoordinationId, cancellationToken);
            if (existing is not null)
            {
                PreparedPass1StartOutcome outcome = await IsExactReplayAsync(
                    existing,
                    snapshot,
                    handoff,
                    concurrencyLimit,
                    cancellationToken)
                    ? PreparedPass1StartOutcome.Replay
                    : PreparedPass1StartOutcome.Conflict;
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return new(outcome, existing.Id, existing.ConcurrencyLimit);
            }

            if (snapshot.Coordination.Phase != TwoRepositoryScanCoordinationPhase.Preparing ||
                snapshot.Executions.Any(value => value.Phase != ScanExecutionPhase.Prepared))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return Result(PreparedPass1StartOutcome.Conflict);
            }

            DateTimeOffset now = snapshot.Executions
                .Select(value => value.UpdatedAt ?? snapshot.Coordination.UpdatedAt)
                .Append(snapshot.Coordination.UpdatedAt)
                .Max();
            PreparedPass1CanonicalArtifact identity = CreatePairIdentity(snapshot, handoff);
            Pass1PairExecution pair = Pass1PairExecution.Create(
                identity.Id!.Value,
                handoff.CoordinationId,
                authorizationScope.SystemId,
                handoff.PairPackageFingerprint,
                handoff.PairSelectionFingerprint,
                snapshot.Manifests[0].Id,
                snapshot.Manifests[1].Id,
                identity.FingerprintHex,
                snapshot.Executions[0].PromptIdentity!,
                snapshot.Executions[1].PromptIdentity!,
                _documentSchemaIdentity,
                snapshot.Executions[0].AnalysisRuntimeIdentityHash!,
                snapshot.Executions[1].AnalysisRuntimeIdentityHash!,
                concurrencyLimit,
                snapshot.Units.Length,
                CountHighValueSeeds(snapshot.Entries),
                now);

            context.Pass1PairExecutions.Add(pair);
            foreach (PreparedFileCodeUnitDraft unit in snapshot.Units)
            {
                ScanExecution execution = snapshot.Executions.Single(value => value.RepositoryId == unit.RepositoryId);
                ScanJobExecution job = ScanJobExecution.Create(
                    execution.Id,
                    unit.RepositoryId,
                    unit.ScanRunId,
                    unit.Id,
                    unit.ManifestOrdinal,
                    now);
                context.ScanJobExecutions.Add(job);
                context.Pass1NavigationBudgets.Add(Pass1NavigationBudget.Create(job.Id, pair.Id, now));
            }

            foreach (ScanExecution execution in snapshot.Executions)
            {
                execution.BeginRunning(execution.StateVersion, now);
            }

            snapshot.Coordination.BeginPass1(snapshot.Coordination.StateVersion, now);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(PreparedPass1StartOutcome.Created, pair.Id, pair.ConcurrencyLimit);
        }
        catch (OperationCanceledException)
        {
            context.ChangeTracker.Clear();
            return Result(PreparedPass1StartOutcome.Cancelled);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or ArgumentException or FormatException)
        {
            context.ChangeTracker.Clear();
            return Result(PreparedPass1StartOutcome.Failed);
        }
    }

    public async Task<PreparedPass1ClaimResult> ClaimNextAsync(
        Guid pairExecutionId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ClaimResult(PreparedPass1ClaimOutcome.Cancelled);
        }

        if (pairExecutionId == Guid.Empty || now.Offset != TimeSpan.Zero ||
            leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(1) ||
            now > DateTimeOffset.MaxValue - leaseDuration)
        {
            return ClaimResult(PreparedPass1ClaimOutcome.Rejected);
        }

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            context.ChangeTracker.Clear();
            Pass1PairExecution? pair = await LockPairAsync(pairExecutionId, cancellationToken);
            if (pair is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ClaimResult(PreparedPass1ClaimOutcome.NotFound);
            }

            if (pair.Phase != Pass1PairExecutionPhase.Running || pair.CircuitState != Pass1CircuitState.Closed)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ClaimResult(
                    PreparedPass1ClaimOutcome.Ineligible,
                    activeLeaseCount: 0,
                    concurrencyLimit: pair.ConcurrencyLimit);
            }

            PairJob[] jobs = await LoadPairJobsAsync(pair, cancellationToken);
            foreach (PairJob expired in jobs.Where(value =>
                         value.Job.State == ScanJobExecutionState.Running &&
                         value.Job.LeaseExpiresAt is DateTimeOffset expiresAt &&
                         expiresAt < now))
            {
                expired.Job.RecoverExpiredLease(expired.Job.StateVersion, now);
            }

            int activeLeaseCount = jobs.Count(value =>
                value.Job.State == ScanJobExecutionState.Running &&
                value.Job.LeaseExpiresAt > now);
            if (activeLeaseCount >= pair.ConcurrencyLimit)
            {
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ClaimResult(
                    PreparedPass1ClaimOutcome.AtCapacity,
                    activeLeaseCount: activeLeaseCount,
                    concurrencyLimit: pair.ConcurrencyLimit);
            }

            PairJob? next = jobs
                .Where(value => value.Job.State == ScanJobExecutionState.Pending)
                .OrderBy(value => value.PairSelectionOrdinal)
                .ThenBy(value => value.Job.Id)
                .FirstOrDefault();
            if (next is null)
            {
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ClaimResult(
                    PreparedPass1ClaimOutcome.Exhausted,
                    activeLeaseCount: activeLeaseCount,
                    concurrencyLimit: pair.ConcurrencyLimit);
            }

            Guid leaseToken = Guid.NewGuid();
            DateTimeOffset leaseExpiresAt = now.Add(leaseDuration);
            next.Job.AcquireLease(leaseToken, leaseExpiresAt, now);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(
                PreparedPass1ClaimOutcome.Claimed,
                next.Job.Id,
                leaseToken,
                leaseExpiresAt,
                next.PairSelectionOrdinal,
                next.Job.ManifestOrdinal,
                activeLeaseCount + 1,
                pair.ConcurrencyLimit);
        }
        catch (OperationCanceledException)
        {
            context.ChangeTracker.Clear();
            return ClaimResult(PreparedPass1ClaimOutcome.Cancelled);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or ArgumentException)
        {
            context.ChangeTracker.Clear();
            return ClaimResult(PreparedPass1ClaimOutcome.Failed);
        }
    }

    public async Task<PreparedPass1RecoveryResult> RecoverAsync(
        Guid pairExecutionId,
        PreparedScanAuthorizationScope authorizationScope,
        bool providerAvailable,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);
        if (cancellationToken.IsCancellationRequested)
        {
            return RecoveryResult(
                PreparedPass1RecoveryOutcome.Cancelled,
                DurableScanReason.Cancelled,
                pairExecutionId: pairExecutionId);
        }

        if (pairExecutionId == Guid.Empty || observedAt.Offset != TimeSpan.Zero ||
            authorizationScope.UserId == Guid.Empty || authorizationScope.SystemId == Guid.Empty ||
            authorizationScope.RepositoryIds is null ||
            authorizationScope.RepositoryIds.Distinct().Count() != 2)
        {
            return RecoveryResult(PreparedPass1RecoveryOutcome.Rejected, DurableScanReason.InvalidIdentity);
        }

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            context.ChangeTracker.Clear();
            Pass1PairExecution? pair = await LockPairAsync(pairExecutionId, cancellationToken);
            if (pair is null || !await IsAuthorizedForPairAsync(pair, authorizationScope, cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return RecoveryResult(PreparedPass1RecoveryOutcome.NotFound, DurableScanReason.Unauthorized);
            }

            RecoverySnapshot? snapshot = await ReconstructRecoveryAsync(pair, cancellationToken);
            if (snapshot is null || observedAt < pair.UpdatedAt)
            {
                if (observedAt >= pair.UpdatedAt && !IsTerminal(pair.Phase))
                {
                    TransitionPair(
                        pair,
                        Pass1PairExecutionPhase.FailedIntegrity,
                        Pass1CircuitState.Paused,
                        Pass1PairReasonCategory.Integrity,
                        "recovery-binding-mismatch",
                        observedAt);
                    await FailRecoveryChildrenAsync(pair, observedAt, cancellationToken);
                    await context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return RecoveryResult(
                        PreparedPass1RecoveryOutcome.FailedClosed,
                        DurableScanReason.IntegrityViolation,
                        pair,
                        pairBindingValid: false);
                }

                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return RecoveryResult(
                    PreparedPass1RecoveryOutcome.Failed,
                    DurableScanReason.IntegrityViolation,
                    pair,
                    pairBindingValid: false);
            }

            PairJob[] jobs = snapshot.Jobs;
            int completed = jobs.Count(value =>
                value.Job.State == ScanJobExecutionState.Completed &&
                value.Job.CommittedDocumentVersionId is not null);
            int expired = 0;
            int dueRetries = 0;
            bool changed = false;

            if (!providerAvailable)
            {
                if (pair.Phase == Pass1PairExecutionPhase.Running &&
                    pair.CircuitState == Pass1CircuitState.Closed)
                {
                    OpenProviderCircuit(pair, observedAt, retryAfterAt: null);
                    changed = true;
                }
                else if (pair.Phase == Pass1PairExecutionPhase.ProbingProvider &&
                    pair.NextProbeAt is DateTimeOffset nextProbeAt && observedAt >= nextProbeAt)
                {
                    changed = true;
                    if (pair.ProbeOrdinal >= 5)
                    {
                        TransitionProviderCircuit(
                            pair,
                            Pass1PairExecutionPhase.PausedProviderUnavailable,
                            Pass1CircuitState.Paused,
                            pair.ProbeOrdinal,
                            null,
                            pair.RetryAfterAt,
                            Pass1PairReasonCategory.ProviderUnavailable,
                            "provider-unavailable",
                            observedAt);
                        await PauseProviderUnavailableAsync(pair, observedAt, cancellationToken);
                    }
                    else
                    {
                        int nextOrdinal = pair.ProbeOrdinal + 1;
                        TransitionProviderCircuit(
                            pair,
                            Pass1PairExecutionPhase.ProbingProvider,
                            Pass1CircuitState.Probing,
                            nextOrdinal,
                            GetProbeEligibility(pair.Id, nextOrdinal, observedAt, pair.RetryAfterAt),
                            pair.RetryAfterAt,
                            Pass1PairReasonCategory.ProviderUnavailable,
                            "provider-unavailable",
                            observedAt);
                    }
                }

                if (changed)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                context.ChangeTracker.Clear();
                return RecoveryResult(
                    PreparedPass1RecoveryOutcome.Paused,
                    DurableScanReason.ProviderUnavailable,
                    pair,
                    completedJobCount: completed,
                    pendingJobCount: CountRemaining(jobs),
                    isReplay: !changed,
                    pairBindingValid: true,
                    manualResumeAction: pair.Phase == Pass1PairExecutionPhase.PausedProviderUnavailable
                        ? "继续扫描"
                        : null);
            }

            if (pair.Phase is Pass1PairExecutionPhase.ProbingProvider or
                Pass1PairExecutionPhase.PausedProviderUnavailable or
                Pass1PairExecutionPhase.PausedOperatorCorrection or
                Pass1PairExecutionPhase.PausedInspection)
            {
                ResumePairAndChildren(pair, snapshot, observedAt);
                changed = true;
            }

            if (pair.Phase != Pass1PairExecutionPhase.Running || pair.CircuitState != Pass1CircuitState.Closed)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return RecoveryResult(
                    IsTerminal(pair.Phase)
                        ? PreparedPass1RecoveryOutcome.NoOpReplay
                        : PreparedPass1RecoveryOutcome.Paused,
                    DurableScanReason.None,
                    pair,
                    completedJobCount: completed,
                    pendingJobCount: CountRemaining(jobs),
                    isReplay: true,
                    pairBindingValid: true);
            }

            foreach (PairJob item in jobs)
            {
                if (item.Job.State == ScanJobExecutionState.Running &&
                    item.Job.LeaseExpiresAt is DateTimeOffset leaseExpiresAt &&
                    leaseExpiresAt < observedAt)
                {
                    item.Job.RecoverExpiredLease(item.Job.StateVersion, observedAt);
                    expired++;
                    changed = true;
                }
                else if (item.Job.State == ScanJobExecutionState.RetryScheduled &&
                    item.Job.AttemptCount < 2 &&
                    item.Job.NextAttemptAt is DateTimeOffset nextAttemptAt &&
                    nextAttemptAt <= observedAt)
                {
                    item.Job.RecoverDueRetry(item.Job.StateVersion, observedAt);
                    dueRetries++;
                    changed = true;
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            int pending = CountRemaining(jobs);
            context.ChangeTracker.Clear();
            return RecoveryResult(
                changed ? PreparedPass1RecoveryOutcome.Continued : PreparedPass1RecoveryOutcome.NoOpReplay,
                DurableScanReason.None,
                pair,
                expired,
                dueRetries,
                completed,
                pending,
                continued: pending > 0,
                isReplay: !changed,
                pairBindingValid: true);
        }
        catch (OperationCanceledException)
        {
            context.ChangeTracker.Clear();
            return RecoveryResult(
                PreparedPass1RecoveryOutcome.Cancelled,
                DurableScanReason.Cancelled,
                pairExecutionId: pairExecutionId);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or ArgumentException or OverflowException)
        {
            context.ChangeTracker.Clear();
            return RecoveryResult(
                PreparedPass1RecoveryOutcome.Failed,
                DurableScanReason.Failed,
                pairExecutionId: pairExecutionId);
        }
    }

    public async Task<PreparedPass1FailureResult> RecordFailureAsync(
        Guid pairExecutionId,
        Guid jobId,
        long expectedStateVersion,
        Guid expectedLeaseToken,
        ScanJobFailureCategory failureCategory,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        string failureCode = Enum.IsDefined(failureCategory)
            ? GetFailureCode(failureCategory)
            : "invalid-failure-category";
        if (cancellationToken.IsCancellationRequested)
        {
            return FailureResult(
                PreparedPass1FailureOutcome.Cancelled,
                failureCategory,
                failureCode,
                cancellationRequested: true);
        }

        if (pairExecutionId == Guid.Empty || jobId == Guid.Empty || expectedStateVersion < 0 ||
            expectedLeaseToken == Guid.Empty || !Enum.IsDefined(failureCategory) ||
            observedAt.Offset != TimeSpan.Zero)
        {
            return FailureResult(PreparedPass1FailureOutcome.Rejected, failureCategory, failureCode);
        }

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            context.ChangeTracker.Clear();
            Pass1PairExecution? pair = await LockPairAsync(pairExecutionId, cancellationToken);
            if (pair is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailureResult(PreparedPass1FailureOutcome.NotFound, failureCategory, failureCode);
            }

            Pass1NavigationBudget? budget = await context.Pass1NavigationBudgets.AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.PairExecutionId == pairExecutionId && value.ScanJobExecutionId == jobId,
                    cancellationToken);
            ScanJobExecution[] lockedJobs = budget is null
                ? []
                : await context.ScanJobExecutions
                    .FromSqlInterpolated(
                        $"SELECT * FROM \"vulgata\".\"ScanJobExecutions\" WHERE \"Id\" = {jobId} FOR UPDATE")
                    .ToArrayAsync(cancellationToken);
            ScanJobExecution? job = lockedJobs.SingleOrDefault();
            if (job is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return FailureResult(PreparedPass1FailureOutcome.NotFound, failureCategory, failureCode, pair: pair);
            }

            if (job.State == ScanJobExecutionState.Completed || IsTerminal(pair.Phase))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return FailureResult(
                    PreparedPass1FailureOutcome.Conflict,
                    failureCategory,
                    failureCode,
                    pair,
                    job,
                    documentCommitted: job.State == ScanJobExecutionState.Completed);
            }

            if (job.StateVersion != expectedStateVersion || job.LeaseToken != expectedLeaseToken)
            {
                bool replay = job.FailureCategory == failureCategory &&
                    job.FailureCode == failureCode &&
                    job.State is ScanJobExecutionState.RetryScheduled or ScanJobExecutionState.Failed or ScanJobExecutionState.Cancelled;
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return FailureResult(
                    replay ? PreparedPass1FailureOutcome.Replay : PreparedPass1FailureOutcome.Conflict,
                    failureCategory,
                    failureCode,
                    pair,
                    job);
            }

            PreparedPass1FailureOutcome outcome;
            switch (failureCategory)
            {
                case ScanJobFailureCategory.JobTransient when job.AttemptCount == 1:
                    job.ScheduleRetry(
                        expectedStateVersion,
                        expectedLeaseToken,
                        observedAt.AddSeconds(1),
                        failureCategory,
                        failureCode,
                        observedAt);
                    outcome = PreparedPass1FailureOutcome.RetryScheduled;
                    break;
                case ScanJobFailureCategory.JobTransient:
                case ScanJobFailureCategory.JobPermanent:
                    job.Fail(
                        expectedStateVersion,
                        expectedLeaseToken,
                        failureCategory,
                        failureCode,
                        observedAt);
                    outcome = PreparedPass1FailureOutcome.Recorded;
                    break;
                case ScanJobFailureCategory.ProviderTransient when
                    pair.Phase == Pass1PairExecutionPhase.ProbingProvider &&
                    pair.CircuitState == Pass1CircuitState.Probing:
                    outcome = PreparedPass1FailureOutcome.Replay;
                    break;
                case ScanJobFailureCategory.ProviderTransient when
                    pair.Phase == Pass1PairExecutionPhase.Running &&
                    pair.CircuitState == Pass1CircuitState.Closed:
                    OpenProviderCircuit(pair, observedAt, retryAfterAt: null);
                    outcome = PreparedPass1FailureOutcome.Paused;
                    break;
                case ScanJobFailureCategory.ProviderTransient:
                    outcome = PreparedPass1FailureOutcome.Conflict;
                    break;
                case ScanJobFailureCategory.ProviderConfiguration:
                    TransitionPair(
                        pair,
                        Pass1PairExecutionPhase.PausedOperatorCorrection,
                        Pass1CircuitState.Paused,
                        Pass1PairReasonCategory.OperatorCorrection,
                        failureCode,
                        observedAt);
                    outcome = PreparedPass1FailureOutcome.Paused;
                    break;
                case ScanJobFailureCategory.Unclassified:
                    TransitionPair(
                        pair,
                        Pass1PairExecutionPhase.PausedInspection,
                        Pass1CircuitState.Paused,
                        Pass1PairReasonCategory.Inspection,
                        failureCode,
                        observedAt);
                    outcome = PreparedPass1FailureOutcome.Paused;
                    break;
                case ScanJobFailureCategory.Cancellation:
                    job.Cancel(expectedStateVersion, expectedLeaseToken, failureCode, observedAt);
                    TransitionPair(
                        pair,
                        Pass1PairExecutionPhase.Cancelled,
                        Pass1CircuitState.Paused,
                        Pass1PairReasonCategory.Cancellation,
                        failureCode,
                        observedAt);
                    outcome = PreparedPass1FailureOutcome.Cancelled;
                    break;
                case ScanJobFailureCategory.Integrity:
                case ScanJobFailureCategory.Security:
                    job.Fail(
                        expectedStateVersion,
                        expectedLeaseToken,
                        failureCategory,
                        failureCode,
                        observedAt);
                    TransitionPair(
                        pair,
                        failureCategory == ScanJobFailureCategory.Integrity
                            ? Pass1PairExecutionPhase.FailedIntegrity
                            : Pass1PairExecutionPhase.FailedSecurity,
                        Pass1CircuitState.Paused,
                        failureCategory == ScanJobFailureCategory.Integrity
                            ? Pass1PairReasonCategory.Integrity
                            : Pass1PairReasonCategory.Security,
                        failureCode,
                        observedAt);
                    outcome = PreparedPass1FailureOutcome.FailedClosed;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failureCategory));
            }

            await context.SaveChangesAsync(cancellationToken);
            int ordinaryFailures = await context.ScanJobExecutions.AsNoTracking()
                .CountAsync(value =>
                    value.State == ScanJobExecutionState.Failed &&
                    (value.FailureCategory == ScanJobFailureCategory.JobTransient ||
                     value.FailureCategory == ScanJobFailureCategory.JobPermanent) &&
                    context.Pass1NavigationBudgets.Any(budgetValue =>
                        budgetValue.PairExecutionId == pairExecutionId &&
                        budgetValue.ScanJobExecutionId == value.Id),
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return FailureResult(
                outcome,
                failureCategory,
                failureCode,
                pair,
                job,
                ordinaryFailureCount: ordinaryFailures);
        }
        catch (OperationCanceledException)
        {
            context.ChangeTracker.Clear();
            return FailureResult(
                PreparedPass1FailureOutcome.Cancelled,
                failureCategory,
                failureCode,
                cancellationRequested: true);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or ArgumentException or OverflowException)
        {
            context.ChangeTracker.Clear();
            return FailureResult(PreparedPass1FailureOutcome.Failed, failureCategory, failureCode);
        }
    }

    public async Task<PreparedPass1CompletionResult> CompletePass1Async(
        Guid pairExecutionId,
        PreparedScanAuthorizationScope authorizationScope,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return CompletionResult(PreparedPass1CompletionOutcome.Cancelled);
        }

        if (pairExecutionId == Guid.Empty || authorizationScope is null ||
            completedAt.Offset != TimeSpan.Zero)
        {
            return CompletionResult(PreparedPass1CompletionOutcome.Rejected);
        }

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            context.ChangeTracker.Clear();
            Pass1PairExecution? pair = await LockPairAsync(pairExecutionId, cancellationToken);
            if (pair is null ||
                !await IsAuthorizedForPairAsync(pair, authorizationScope, cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return CompletionResult(PreparedPass1CompletionOutcome.NotFound);
            }

            Pass1Outcome? durableOutcome = await context.Pass1Outcomes.AsNoTracking()
                .SingleOrDefaultAsync(value => value.PairExecutionId == pairExecutionId, cancellationToken);
            if (durableOutcome is not null)
            {
                Pass1FailureInventoryEntry[] durableInventory = await context.Pass1FailureInventoryEntries
                    .AsNoTracking()
                    .Where(value => value.PairExecutionId == pairExecutionId)
                    .OrderBy(value => value.PairSelectionOrdinal)
                    .ThenBy(value => value.RepositoryId)
                    .ThenBy(value => value.FileCodeUnitId)
                    .ToArrayAsync(cancellationToken);
                TwoRepositoryScanCoordinationPhase durablePhase = await context.TwoRepositoryScanCoordinations
                    .AsNoTracking()
                    .Where(value => value.Id == pair.CoordinationId)
                    .Select(value => value.Phase)
                    .SingleAsync(cancellationToken);
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return CompletionResult(
                    PreparedPass1CompletionOutcome.Replay,
                    durableOutcome.DispatchedCount,
                    durableOutcome.CompletedCount,
                    durableOutcome.OrdinaryFailureCount,
                    durableOutcome.HighValueSeedCount,
                    durableOutcome.SucceededHighValueSeedCount,
                    durableInventory,
                    durablePhase,
                    isReplay: true);
            }

            if (pair.Phase != Pass1PairExecutionPhase.Running || completedAt < pair.UpdatedAt)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return CompletionResult(PreparedPass1CompletionOutcome.Ineligible);
            }

            PairJob[] pairJobs = await LoadPairJobsAsync(pair, cancellationToken);
            if (pairJobs.Length != pair.DispatchedFileCount ||
                pairJobs.Select(value => value.Job.Id).Distinct().Count() != pair.DispatchedFileCount ||
                pairJobs.Select(value => value.Job.FileCodeUnitId).Distinct().Count() != pair.DispatchedFileCount ||
                pairJobs.Any(value => value.Job.State is not
                    (ScanJobExecutionState.Completed or ScanJobExecutionState.Failed)) ||
                pairJobs.Any(value => value.Job.StateChangedAt > completedAt))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return CompletionResult(PreparedPass1CompletionOutcome.Ineligible);
            }

            PairJob[] failedJobs = pairJobs
                .Where(value => value.Job.State == ScanJobExecutionState.Failed)
                .OrderBy(value => value.PairSelectionOrdinal)
                .ThenBy(value => value.Job.RepositoryId)
                .ThenBy(value => value.Job.FileCodeUnitId)
                .ToArray();
            if (failedJobs.Any(value => value.Job.FailureCategory is not
                    (ScanJobFailureCategory.JobTransient or ScanJobFailureCategory.JobPermanent) ||
                    string.IsNullOrWhiteSpace(value.Job.FailureCode)))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return CompletionResult(PreparedPass1CompletionOutcome.Ineligible);
            }

            PreparedScanManifestEntry[] entries = await context.PreparedScanManifestEntries.AsNoTracking()
                .Where(value => value.PreparedScanManifestId == pair.ManifestOneId ||
                    value.PreparedScanManifestId == pair.ManifestTwoId)
                .ToArrayAsync(cancellationToken);
            Guid[] unitIds = entries.Select(value => value.FileCodeUnitId).ToArray();
            CodeUnit[] units = await context.CodeUnits.AsNoTracking()
                .Where(value => unitIds.Contains(value.Id))
                .ToArrayAsync(cancellationToken);
            if (entries.Length != pair.DispatchedFileCount ||
                entries.Select(value => value.FileCodeUnitId).Distinct().Count() != pair.DispatchedFileCount ||
                units.Length != entries.Length)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return CompletionResult(PreparedPass1CompletionOutcome.Conflict);
            }

            Dictionary<Guid, CodeUnit> unitsById = units.ToDictionary(value => value.Id);
            Guid[] highValueSeedIds = entries
                .Where(value => value.DirectBusinessScore >= 400)
                .GroupBy(value => value.PreparedScanManifestId)
                .OrderBy(group => group.Key)
                .SelectMany(group => group
                    .OrderByDescending(value => value.DirectBusinessScore)
                    .ThenBy(value => unitsById[value.FileCodeUnitId].FilePath, StringComparer.Ordinal)
                    .ThenBy(value => unitsById[value.FileCodeUnitId].SegmentOrdinal)
                    .Take(PreparedScanPlanner.MaximumHighValueSeedsPerRepository))
                .Select(value => value.FileCodeUnitId)
                .ToArray();
            if (highValueSeedIds.Length != pair.HighValueSeedCount)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return CompletionResult(PreparedPass1CompletionOutcome.Conflict);
            }

            HashSet<Guid> completedUnitIds = pairJobs
                .Where(value => value.Job.State == ScanJobExecutionState.Completed &&
                    value.Job.CommittedDocumentVersionId is not null)
                .Select(value => value.Job.FileCodeUnitId)
                .ToHashSet();
            int completedCount = completedUnitIds.Count;
            if (completedCount != pairJobs.Count(value => value.Job.State == ScanJobExecutionState.Completed))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return CompletionResult(PreparedPass1CompletionOutcome.Conflict);
            }

            int succeededHighValueSeedCount = highValueSeedIds.Count(completedUnitIds.Contains);
            var inventory = new List<Pass1FailureInventoryEntry>(failedJobs.Length);
            foreach (PairJob failedJob in failedJobs)
            {
                PreparedScanManifestEntry entry = entries.Single(value =>
                    value.FileCodeUnitId == failedJob.Job.FileCodeUnitId);
                inventory.Add(Pass1FailureInventoryEntry.Create(
                    pair.Id,
                    unitsById[failedJob.Job.FileCodeUnitId],
                    entry.PairSelectionOrdinal,
                    entry.ManifestOrdinal,
                    failedJob.Job.FailureCategory == ScanJobFailureCategory.JobTransient
                        ? Pass1OrdinaryFailureCategory.WorkerTransient
                        : Pass1OrdinaryFailureCategory.InvalidResult,
                    failedJob.Job.FailureCode!));
            }

            int failureBudget = Math.Min(5, pair.DispatchedFileCount / 10);
            Pass1OutcomeStatus status = succeededHighValueSeedCount != highValueSeedIds.Length ||
                failedJobs.Length > failureBudget
                ? Pass1OutcomeStatus.FailedPass1
                : failedJobs.Length == 0
                    ? Pass1OutcomeStatus.Succeeded
                    : Pass1OutcomeStatus.CompletedWithWarnings;
            Pass1Outcome outcome = Pass1Outcome.Create(
                pair.Id,
                status,
                pair.DispatchedFileCount,
                completedCount,
                failedJobs.Length,
                highValueSeedIds.Length,
                succeededHighValueSeedCount,
                reconciliationFingerprint: null,
                reconciledEdgeCount: 0,
                inventory,
                completedAt);

            TwoRepositoryScanCoordination coordination = await context.TwoRepositoryScanCoordinations
                .SingleAsync(value => value.Id == pair.CoordinationId, cancellationToken);
            ScanExecution[] executions = await context.ScanExecutions
                .Where(value => value.Id == coordination.ScanExecutionOneId ||
                    value.Id == coordination.ScanExecutionTwoId)
                .ToArrayAsync(cancellationToken);
            if (coordination.Phase != TwoRepositoryScanCoordinationPhase.Pass1 ||
                completedAt < coordination.UpdatedAt || executions.Length != 2 ||
                executions.Any(value => value.Phase != ScanExecutionPhase.Running ||
                    completedAt < value.UpdatedAt))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return CompletionResult(PreparedPass1CompletionOutcome.Conflict);
            }

            if (status == Pass1OutcomeStatus.FailedPass1)
            {
                foreach (ScanExecution execution in executions)
                {
                    execution.FailPass1(execution.StateVersion, "pass1-policy-failed", completedAt);
                }

                coordination.FailPass1(coordination.StateVersion, "pass1-policy-failed", completedAt);
                TransitionPair(
                    pair,
                    Pass1PairExecutionPhase.FailedPass1,
                    Pass1CircuitState.Paused,
                    Pass1PairReasonCategory.Pass1Policy,
                    "pass1-policy-failed",
                    completedAt);
            }
            else
            {
                foreach (ScanExecution execution in executions)
                {
                    bool childHasFailure = failedJobs.Any(value =>
                        value.Job.ScanExecutionId == execution.Id);
                    if (childHasFailure)
                    {
                        execution.CompleteWithWarnings(
                            execution.StateVersion,
                            "Pass 1 completed with bounded ordinary file failures.",
                            completedAt);
                    }
                    else
                    {
                        execution.Complete(execution.StateVersion, completedAt);
                    }
                }

                coordination.BeginResolving(coordination.StateVersion, completedAt);
                TransitionPair(
                    pair,
                    Pass1PairExecutionPhase.Reconciling,
                    Pass1CircuitState.Closed,
                    reasonCategory: null,
                    reasonCode: null,
                    completedAt);
            }

            context.Pass1Outcomes.Add(outcome);
            if (inventory.Count > 0)
            {
                context.Pass1FailureInventoryEntries.AddRange(inventory);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return CompletionResult(
                status switch
                {
                    Pass1OutcomeStatus.Succeeded => PreparedPass1CompletionOutcome.Succeeded,
                    Pass1OutcomeStatus.CompletedWithWarnings => PreparedPass1CompletionOutcome.CompletedWithWarnings,
                    _ => PreparedPass1CompletionOutcome.FailedPass1,
                },
                pair.DispatchedFileCount,
                completedCount,
                failedJobs.Length,
                highValueSeedIds.Length,
                succeededHighValueSeedCount,
                inventory,
                coordination.Phase);
        }
        catch (OperationCanceledException)
        {
            context.ChangeTracker.Clear();
            return CompletionResult(PreparedPass1CompletionOutcome.Cancelled);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or ArgumentException or
                OverflowException)
        {
            context.ChangeTracker.Clear();
            return CompletionResult(PreparedPass1CompletionOutcome.Failed);
        }
    }

    public async Task<PreparedPass1ProviderCircuitResult> RecordProviderTransientAsync(
        Guid pairExecutionId,
        DateTimeOffset observedAt,
        DateTimeOffset? retryAfterAt = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ProviderCircuitResult(PreparedPass1ProviderCircuitOutcome.Cancelled);
        }

        if (pairExecutionId == Guid.Empty || observedAt.Offset != TimeSpan.Zero ||
            retryAfterAt?.Offset is { } offset && offset != TimeSpan.Zero)
        {
            return ProviderCircuitResult(PreparedPass1ProviderCircuitOutcome.Rejected);
        }

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            context.ChangeTracker.Clear();
            Pass1PairExecution? pair = await LockPairAsync(pairExecutionId, cancellationToken);
            if (pair is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return ProviderCircuitResult(PreparedPass1ProviderCircuitOutcome.NotFound);
            }

            if (pair.Phase == Pass1PairExecutionPhase.ProbingProvider &&
                pair.CircuitState == Pass1CircuitState.Probing)
            {
                Guid[] replayDocuments = await LoadCompletedDocumentVersionIdsAsync(
                    pairExecutionId,
                    cancellationToken);
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ProviderCircuitResult(
                    PreparedPass1ProviderCircuitOutcome.Replay,
                    pair,
                    replayDocuments);
            }

            if (pair.Phase != Pass1PairExecutionPhase.Running ||
                pair.CircuitState != Pass1CircuitState.Closed ||
                observedAt < pair.UpdatedAt)
            {
                Guid[] conflictDocuments = await LoadCompletedDocumentVersionIdsAsync(
                    pairExecutionId,
                    cancellationToken);
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ProviderCircuitResult(
                    PreparedPass1ProviderCircuitOutcome.Conflict,
                    pair,
                    conflictDocuments);
            }

            OpenProviderCircuit(pair, observedAt, retryAfterAt);
            await context.SaveChangesAsync(cancellationToken);
            Guid[] completedDocuments = await LoadCompletedDocumentVersionIdsAsync(
                pairExecutionId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ProviderCircuitResult(
                PreparedPass1ProviderCircuitOutcome.Opened,
                pair,
                completedDocuments);
        }
        catch (OperationCanceledException)
        {
            context.ChangeTracker.Clear();
            return ProviderCircuitResult(PreparedPass1ProviderCircuitOutcome.Cancelled);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or ArgumentException or OverflowException)
        {
            context.ChangeTracker.Clear();
            return ProviderCircuitResult(PreparedPass1ProviderCircuitOutcome.Failed);
        }
    }

    public async Task<PreparedPass1ProviderCircuitResult> ProbeProviderAsync(
        Guid pairExecutionId,
        long expectedStateVersion,
        int expectedProbeOrdinal,
        bool providerAvailable,
        DateTimeOffset observedAt,
        DateTimeOffset? retryAfterAt = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ProviderCircuitResult(PreparedPass1ProviderCircuitOutcome.Cancelled);
        }

        if (pairExecutionId == Guid.Empty || expectedStateVersion < 0 ||
            expectedProbeOrdinal is < 1 or > 5 || observedAt.Offset != TimeSpan.Zero ||
            retryAfterAt?.Offset is { } offset && offset != TimeSpan.Zero)
        {
            return ProviderCircuitResult(PreparedPass1ProviderCircuitOutcome.Rejected);
        }

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            context.ChangeTracker.Clear();
            Pass1PairExecution? pair = await LockPairAsync(pairExecutionId, cancellationToken);
            if (pair is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return ProviderCircuitResult(PreparedPass1ProviderCircuitOutcome.NotFound);
            }

            Guid[] completedDocuments = await LoadCompletedDocumentVersionIdsAsync(
                pairExecutionId,
                cancellationToken);
            if (pair.Phase != Pass1PairExecutionPhase.ProbingProvider ||
                pair.CircuitState != Pass1CircuitState.Probing ||
                pair.StateVersion != expectedStateVersion ||
                pair.ProbeOrdinal != expectedProbeOrdinal)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ProviderCircuitResult(
                    PreparedPass1ProviderCircuitOutcome.Conflict,
                    pair,
                    completedDocuments);
            }

            if (pair.NextProbeAt is null || observedAt < pair.NextProbeAt || observedAt < pair.UpdatedAt)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ProviderCircuitResult(
                    PreparedPass1ProviderCircuitOutcome.Ineligible,
                    pair,
                    completedDocuments);
            }

            PreparedPass1ProviderCircuitOutcome outcome;
            if (providerAvailable)
            {
                TransitionProviderCircuit(
                    pair,
                    Pass1PairExecutionPhase.Running,
                    Pass1CircuitState.Closed,
                    probeOrdinal: 0,
                    nextProbeAt: null,
                    retryAfterAt: null,
                    reasonCategory: null,
                    reasonCode: null,
                    observedAt);
                outcome = PreparedPass1ProviderCircuitOutcome.Recovered;
            }
            else if (expectedProbeOrdinal == 5)
            {
                TransitionProviderCircuit(
                    pair,
                    Pass1PairExecutionPhase.PausedProviderUnavailable,
                    Pass1CircuitState.Paused,
                    expectedProbeOrdinal,
                    nextProbeAt: null,
                    retryAfterAt: null,
                    Pass1PairReasonCategory.ProviderUnavailable,
                    "provider-unavailable",
                    observedAt);
                await PauseProviderUnavailableAsync(pair, observedAt, cancellationToken);
                outcome = PreparedPass1ProviderCircuitOutcome.Paused;
            }
            else
            {
                int nextOrdinal = expectedProbeOrdinal + 1;
                DateTimeOffset? boundedRetryAfter = BoundRetryAfter(observedAt, retryAfterAt);
                TransitionProviderCircuit(
                    pair,
                    Pass1PairExecutionPhase.ProbingProvider,
                    Pass1CircuitState.Probing,
                    nextOrdinal,
                    GetProbeEligibility(pair.Id, nextOrdinal, observedAt, boundedRetryAfter),
                    boundedRetryAfter,
                    Pass1PairReasonCategory.ProviderUnavailable,
                    "provider-unavailable",
                    observedAt);
                outcome = PreparedPass1ProviderCircuitOutcome.ProbeScheduled;
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ProviderCircuitResult(outcome, pair, completedDocuments);
        }
        catch (OperationCanceledException)
        {
            context.ChangeTracker.Clear();
            return ProviderCircuitResult(PreparedPass1ProviderCircuitOutcome.Cancelled);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or ArgumentException or OverflowException)
        {
            context.ChangeTracker.Clear();
            return ProviderCircuitResult(PreparedPass1ProviderCircuitOutcome.Failed);
        }
    }

    public async Task<PreparedPass1ConcurrencyUpdateResult> UpdateConcurrencyAsync(
        PreparedScanAuthorizationScope authorizationScope,
        Guid pairExecutionId,
        int requestedLimit,
        long expectedStateVersion,
        string principalId,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);
        if (cancellationToken.IsCancellationRequested)
        {
            return ConcurrencyResult(PreparedPass1ConcurrencyUpdateOutcome.Cancelled);
        }

        if (pairExecutionId == Guid.Empty || requestedLimit is < 1 or > 4 || expectedStateVersion < 0 ||
            authorizationScope.UserId == Guid.Empty || authorizationScope.SystemId == Guid.Empty ||
            string.IsNullOrWhiteSpace(principalId) || principalId.Length > 256 ||
            changedAt.Offset != TimeSpan.Zero)
        {
            return ConcurrencyResult(PreparedPass1ConcurrencyUpdateOutcome.Rejected);
        }

        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            Pass1PairExecution? pair = await LockPairAsync(pairExecutionId, cancellationToken);
            if (pair is null || !await IsAuthorizedForPairAsync(pair, authorizationScope, cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ConcurrencyResult(PreparedPass1ConcurrencyUpdateOutcome.NotFound);
            }

            int activeLeaseCount = (await LoadPairJobsAsync(pair, cancellationToken)).Count(value =>
                value.Job.State == ScanJobExecutionState.Running && value.Job.LeaseExpiresAt > changedAt);
            Pass1ConcurrencyChange? prior = await context.Pass1ConcurrencyChanges
                .AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.PairExecutionId == pair.Id &&
                    value.ExpectedStateVersion == expectedStateVersion,
                    cancellationToken);
            if (prior is not null)
            {
                bool exactReplay = IsExactConcurrencyReplay(
                    prior,
                    pairExecutionId,
                    requestedLimit,
                    expectedStateVersion,
                    principalId);
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ConcurrencyResult(
                    exactReplay
                        ? PreparedPass1ConcurrencyUpdateOutcome.Replay
                        : PreparedPass1ConcurrencyUpdateOutcome.Conflict,
                    pair,
                    activeLeaseCount);
            }

            if (pair.StateVersion != expectedStateVersion)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ConcurrencyResult(
                    PreparedPass1ConcurrencyUpdateOutcome.Conflict,
                    pair,
                    activeLeaseCount);
            }

            if (!IsConcurrencyMutable(pair.Phase))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ConcurrencyResult(
                    PreparedPass1ConcurrencyUpdateOutcome.Ineligible,
                    pair,
                    activeLeaseCount);
            }

            if (requestedLimit == pair.ConcurrencyLimit)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                return ConcurrencyResult(
                    PreparedPass1ConcurrencyUpdateOutcome.NoChange,
                    pair,
                    activeLeaseCount);
            }

            int previousLimit = pair.ConcurrencyLimit;
            long resultingVersion = pair.StateVersion + 1;
            long resultingRevision = pair.ConcurrencyRevision + 1;
            PreparedPass1CanonicalArtifact artifact = PreparedPass1CanonicalCodec.CreateConcurrencyChange(new(
                pair.Id,
                checked((ulong)expectedStateVersion),
                checked((ulong)previousLimit),
                checked((ulong)requestedLimit),
                checked((ulong)resultingVersion),
                checked((ulong)resultingRevision),
                principalId));
            Pass1ConcurrencyChange change = Pass1ConcurrencyChange.Create(
                artifact.Id!.Value,
                pair.Id,
                expectedStateVersion,
                resultingVersion,
                previousLimit,
                requestedLimit,
                resultingRevision,
                principalId,
                artifact.FingerprintHex,
                changedAt);
            pair.UpdateConcurrency(expectedStateVersion, requestedLimit, changedAt);
            context.Pass1ConcurrencyChanges.Add(change);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ConcurrencyResult(
                PreparedPass1ConcurrencyUpdateOutcome.Updated,
                pair,
                activeLeaseCount);
        }
        catch (OperationCanceledException)
        {
            context.ChangeTracker.Clear();
            return ConcurrencyResult(PreparedPass1ConcurrencyUpdateOutcome.Cancelled);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException or ArgumentException or OverflowException)
        {
            context.ChangeTracker.Clear();
            return ConcurrencyResult(PreparedPass1ConcurrencyUpdateOutcome.Failed);
        }
    }

    private async Task<StartSnapshot?> ReconstructAsync(
        PreparedScanAuthorizationScope authorization,
        PreparedScanHandoff handoff,
        CancellationToken cancellationToken)
    {
        TwoRepositoryScanCoordination? coordination = await context.TwoRepositoryScanCoordinations
            .SingleOrDefaultAsync(value => value.Id == handoff.CoordinationId, cancellationToken);
        if (coordination is null || coordination.SystemId != authorization.SystemId)
        {
            return null;
        }

        PreparedScanManifest[] manifests = await context.PreparedScanManifests
            .Where(value => value.CoordinationId == handoff.CoordinationId)
            .OrderBy(value => value.RepositoryId)
            .ToArrayAsync(cancellationToken);
        if (manifests.Length != 2 || manifests.Any(value =>
                value.ApprovalState != PreparedScanManifestApprovalState.Approved ||
                value.SchemaIdentity != PreparedScanManifest.CurrentSchemaIdentity))
        {
            return null;
        }

        ScanExecution[] executions = await context.ScanExecutions
            .Where(value => value.Id == coordination.ScanExecutionOneId || value.Id == coordination.ScanExecutionTwoId)
            .OrderBy(value => value.RepositoryId)
            .ToArrayAsync(cancellationToken);
        if (executions.Length != 2 || executions.Any(value =>
                value.SystemId != authorization.SystemId ||
                string.IsNullOrWhiteSpace(value.PromptIdentity) ||
                value.DocumentSchemaIdentity != _documentSchemaIdentity ||
                string.IsNullOrWhiteSpace(value.AnalysisRuntimeIdentityHash)))
        {
            return null;
        }

        PreparedScanManifestEntry[] entries = await context.PreparedScanManifestEntries
            .Where(value => value.CoordinationId == handoff.CoordinationId)
            .OrderBy(value => value.PairSelectionOrdinal)
            .ToArrayAsync(cancellationToken);
        Guid[] unitIds = entries.Select(value => value.FileCodeUnitId).ToArray();
        CodeUnit[] storedUnits = await context.CodeUnits
            .AsNoTracking()
            .Where(value => unitIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        PreparedFileCodeUnitDraft[] units = handoff.FileCodeUnits
            .OrderBy(value => value.PairSelectionOrdinal)
            .ToArray();

        if (!BindingsMatch(handoff, manifests, executions) ||
            !PackageFingerprintsMatch(handoff, manifests) ||
            entries.Length != handoff.SelectedCount ||
            units.Length != entries.Length ||
            !units.Select(value => value.PairSelectionOrdinal).SequenceEqual(Enumerable.Range(1, units.Length)) ||
            units.Any(unit => !UnitMatches(unit, entries, storedUnits)))
        {
            return null;
        }

        return new(coordination, manifests, executions, entries, units);
    }

    private async Task<Pass1PairExecution?> LockPairAsync(
        Guid pairExecutionId,
        CancellationToken cancellationToken)
    {
        Pass1PairExecution[] locked = await context.Pass1PairExecutions
            .FromSqlInterpolated(
                $"SELECT * FROM \"vulgata\".\"Pass1PairExecutions\" WHERE \"Id\" = {pairExecutionId} FOR UPDATE")
            .ToArrayAsync(cancellationToken);
        return locked.SingleOrDefault();
    }

    private async Task<PairJob[]> LoadPairJobsAsync(
        Pass1PairExecution pair,
        CancellationToken cancellationToken)
    {
        PreparedScanManifestEntry[] entries = await context.PreparedScanManifestEntries
            .AsNoTracking()
            .Where(value =>
                value.PreparedScanManifestId == pair.ManifestOneId ||
                value.PreparedScanManifestId == pair.ManifestTwoId)
            .ToArrayAsync(cancellationToken);
        TwoRepositoryScanCoordination coordination = await context.TwoRepositoryScanCoordinations
            .AsNoTracking()
            .SingleAsync(value => value.Id == pair.CoordinationId, cancellationToken);
        Guid[] unitIds = entries.Select(value => value.FileCodeUnitId).ToArray();
        ScanJobExecution[] jobs = await context.ScanJobExecutions
            .Where(value =>
                unitIds.Contains(value.FileCodeUnitId) &&
                (value.ScanExecutionId == coordination.ScanExecutionOneId ||
                 value.ScanExecutionId == coordination.ScanExecutionTwoId))
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, PreparedScanManifestEntry> entriesByUnit = entries
            .ToDictionary(value => value.FileCodeUnitId);
        return jobs
            .Where(value => entriesByUnit.ContainsKey(value.FileCodeUnitId))
            .Select(value => new PairJob(
                value,
                entriesByUnit[value.FileCodeUnitId].PairSelectionOrdinal))
            .ToArray();
    }

    private async Task<bool> IsAuthorizedForPairAsync(
        Pass1PairExecution pair,
        PreparedScanAuthorizationScope authorizationScope,
        CancellationToken cancellationToken)
    {
        if (pair.SystemId != authorizationScope.SystemId ||
            authorizationScope.RepositoryIds.Distinct().Count() != 2)
        {
            return false;
        }

        PreparedScanManifest[] manifests = await context.PreparedScanManifests
            .AsNoTracking()
            .Where(value => value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId)
            .ToArrayAsync(cancellationToken);
        return manifests.Length == 2 &&
            manifests.All(value =>
                value.CoordinationId == pair.CoordinationId &&
                value.ApprovalState == PreparedScanManifestApprovalState.Approved &&
                value.SchemaIdentity == PreparedScanManifest.CurrentSchemaIdentity) &&
            manifests.Select(value => value.RepositoryId).Order()
                .SequenceEqual(authorizationScope.RepositoryIds.Distinct().Order());
    }

    private async Task<RecoverySnapshot?> ReconstructRecoveryAsync(
        Pass1PairExecution pair,
        CancellationToken cancellationToken)
    {
        TwoRepositoryScanCoordination? coordination = await context.TwoRepositoryScanCoordinations
            .SingleOrDefaultAsync(value => value.Id == pair.CoordinationId, cancellationToken);
        if (coordination is null || coordination.SystemId != pair.SystemId)
        {
            return null;
        }

        PreparedScanManifest[] manifests = await context.PreparedScanManifests.AsNoTracking()
            .Where(value => value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId)
            .OrderBy(value => value.RepositoryId)
            .ToArrayAsync(cancellationToken);
        ScanExecution[] executions = await context.ScanExecutions
            .Where(value => value.Id == coordination.ScanExecutionOneId || value.Id == coordination.ScanExecutionTwoId)
            .OrderBy(value => value.RepositoryId)
            .ToArrayAsync(cancellationToken);
        if (manifests.Length != 2 || executions.Length != 2 ||
            manifests.Select(value => value.RepositoryId).Distinct().Count() != 2 ||
            pair.ManifestOneId != manifests[0].Id || pair.ManifestTwoId != manifests[1].Id ||
            coordination.RepositoryOneId != manifests[0].RepositoryId ||
            coordination.RepositoryTwoId != manifests[1].RepositoryId ||
            coordination.ScanExecutionOneId != executions[0].Id ||
            coordination.ScanExecutionTwoId != executions[1].Id)
        {
            return null;
        }

        for (int index = 0; index < 2; index++)
        {
            PreparedScanManifest manifest = manifests[index];
            ScanExecution execution = executions[index];
            string expectedPrompt = index == 0 ? pair.PromptIdentityOne : pair.PromptIdentityTwo;
            string expectedRuntime = index == 0
                ? pair.AnalysisRuntimeIdentityHashOne
                : pair.AnalysisRuntimeIdentityHashTwo;
            if (manifest.CoordinationId != pair.CoordinationId ||
                manifest.ApprovalState != PreparedScanManifestApprovalState.Approved ||
                manifest.SchemaIdentity != PreparedScanManifest.CurrentSchemaIdentity ||
                manifest.PairSelectionFingerprint != pair.PairSelectionFingerprint ||
                execution.SystemId != pair.SystemId ||
                execution.RepositoryId != manifest.RepositoryId ||
                execution.ScanRunId != manifest.ScanRunId ||
                execution.ProfiledRevision != manifest.ProfiledRevision ||
                execution.PromptIdentity != expectedPrompt ||
                execution.DocumentSchemaIdentity != pair.DocumentSchemaIdentity ||
                execution.DocumentSchemaIdentity != _documentSchemaIdentity ||
                execution.AnalysisRuntimeIdentityHash != expectedRuntime ||
                manifest.RuntimeIdentityHash != expectedRuntime ||
                manifest.CodeGraphRepositoryId != manifest.RepositoryId ||
                manifest.CodeGraphScanRunId != manifest.ScanRunId ||
                manifest.CodeGraphProfiledRevision != manifest.ProfiledRevision ||
                manifest.CodeGraphRuntimeIdentityHash != manifest.RuntimeIdentityHash ||
                manifest.JavaReadinessRepositoryId != manifest.RepositoryId ||
                manifest.JavaReadinessScanRunId != manifest.ScanRunId ||
                manifest.JavaReadinessProfiledRevision != manifest.ProfiledRevision ||
                manifest.JavaReadinessRuntimeIdentityHash != manifest.RuntimeIdentityHash)
            {
                return null;
            }
        }

        PreparedPass1CanonicalArtifact identity;
        try
        {
            identity = PreparedPass1CanonicalCodec.CreatePairExecution(new(
                pair.CoordinationId,
                pair.SystemId,
                Convert.FromHexString(pair.PairPackageFingerprint),
                Convert.FromHexString(pair.PairSelectionFingerprint),
                manifests.Select((manifest, index) => new PreparedPass1ManifestBinding(
                    manifest.RepositoryId,
                    manifest.ScanRunId,
                    executions[index].Id,
                    manifest.Id,
                    Convert.FromHexString(manifest.Fingerprint),
                    Convert.FromHexString(manifest.ProfiledRevision),
                    Convert.FromHexString(manifest.PreparedScanContextIdentityHash))).ToArray(),
                executions.Select(value => value.PromptIdentity!).ToArray(),
                pair.DocumentSchemaIdentity,
                executions.Select(value =>
                    (ReadOnlyMemory<byte>)Convert.FromHexString(value.AnalysisRuntimeIdentityHash!)).ToArray()));
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (identity.Id != pair.Id || identity.FingerprintHex != pair.Pass1PairFingerprint)
        {
            return null;
        }

        PreparedScanManifestEntry[] entries = await context.PreparedScanManifestEntries.AsNoTracking()
            .Where(value => value.PreparedScanManifestId == pair.ManifestOneId ||
                value.PreparedScanManifestId == pair.ManifestTwoId)
            .OrderBy(value => value.PairSelectionOrdinal)
            .ToArrayAsync(cancellationToken);
        Guid[] unitIds = entries.Select(value => value.FileCodeUnitId).ToArray();
        CodeUnit[] units = await context.CodeUnits.AsNoTracking()
            .Where(value => unitIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        if (entries.Length != pair.DispatchedFileCount || units.Length != entries.Length ||
            entries.Select(value => value.PairSelectionOrdinal)
                .Distinct().Order().SequenceEqual(Enumerable.Range(1, entries.Length)) is false)
        {
            return null;
        }

        foreach (PreparedScanManifestEntry entry in entries)
        {
            CodeUnit? unit = units.SingleOrDefault(value => value.Id == entry.FileCodeUnitId);
            PreparedScanManifest? manifest = manifests.SingleOrDefault(value => value.Id == entry.PreparedScanManifestId);
            if (unit is null || manifest is null || unit.RepositoryId != manifest.RepositoryId ||
                unit.ScanRunId != manifest.ScanRunId || unit.ByteStart is null ||
                unit.ByteEndExclusive is null || unit.SegmentOrdinal is null || unit.SegmentCount is null ||
                string.IsNullOrWhiteSpace(unit.FileHash))
            {
                return null;
            }

            PreparedCanonicalArtifact unitIdentity = PreparedScanIdentityBuilders.CreateFileCodeUnitIdentity(
                unit.RepositoryId,
                unit.ScanRunId,
                manifest.ProfiledRevision,
                unit.FilePath,
                unit.FileHash,
                unit.ByteStart.Value,
                unit.ByteEndExclusive.Value,
                unit.LineStart,
                unit.LineEnd,
                unit.SegmentOrdinal.Value,
                unit.SegmentCount.Value,
                unit.SourceHash);
            if (entry.FileCodeUnitIdentityFingerprint != unitIdentity.Fingerprint ||
                unit.FileCodeUnitIdentityFingerprint != unitIdentity.Fingerprint)
            {
                return null;
            }
        }

        PairJob[] jobs = await LoadPairJobsAsync(pair, cancellationToken);
        if (jobs.Length != entries.Length || jobs.Select(value => value.Job.FileCodeUnitId).Distinct().Count() != jobs.Length ||
            jobs.Any(value => !entries.Any(entry =>
                entry.FileCodeUnitId == value.Job.FileCodeUnitId &&
                entry.ManifestOrdinal == value.Job.ManifestOrdinal &&
                entry.PairSelectionOrdinal == value.PairSelectionOrdinal)) ||
            jobs.Any(value => value.Job.State == ScanJobExecutionState.Completed &&
                (value.Job.CommittedDocumentVersionId is null ||
                 !context.DocumentVersions.Any(version => version.Id == value.Job.CommittedDocumentVersionId))))
        {
            return null;
        }

        return new RecoverySnapshot(coordination, manifests, executions, entries, units, jobs);
    }

    private void ResumePairAndChildren(
        Pass1PairExecution pair,
        RecoverySnapshot snapshot,
        DateTimeOffset observedAt)
    {
        TransitionProviderCircuit(
            pair,
            Pass1PairExecutionPhase.Running,
            Pass1CircuitState.Closed,
            0,
            null,
            null,
            null,
            null,
            observedAt);
        foreach (ScanExecution execution in snapshot.Executions.Where(value => value.Phase is
                     ScanExecutionPhase.PausedProviderUnavailable or
                     ScanExecutionPhase.PausedOperatorCorrection))
        {
            execution.ResumeToPrepared(execution.StateVersion, observedAt);
            execution.BeginRunning(execution.StateVersion, observedAt);
        }

        if (snapshot.Coordination.Phase == TwoRepositoryScanCoordinationPhase.Paused)
        {
            context.Entry(snapshot.Coordination).Property(value => value.Phase).CurrentValue =
                TwoRepositoryScanCoordinationPhase.Pass1;
            context.Entry(snapshot.Coordination).Property(value => value.ReasonCategory).CurrentValue = null;
            context.Entry(snapshot.Coordination).Property(value => value.ReasonCode).CurrentValue = null;
            context.Entry(snapshot.Coordination).Property(value => value.StateVersion).CurrentValue =
                checked(snapshot.Coordination.StateVersion + 1);
            context.Entry(snapshot.Coordination).Property(value => value.UpdatedAt).CurrentValue = observedAt;
            context.Entry(snapshot.Coordination).Property(value => value.TerminalAt).CurrentValue = null;
        }
    }

    private async Task FailRecoveryChildrenAsync(
        Pass1PairExecution pair,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        TwoRepositoryScanCoordination? coordination = await context.TwoRepositoryScanCoordinations
            .SingleOrDefaultAsync(value => value.Id == pair.CoordinationId, cancellationToken);
        if (coordination is null)
        {
            return;
        }

        ScanExecution[] executions = await context.ScanExecutions
            .Where(value => value.Id == coordination.ScanExecutionOneId || value.Id == coordination.ScanExecutionTwoId)
            .ToArrayAsync(cancellationToken);
        foreach (ScanExecution execution in executions.Where(value => value.Phase is
                     ScanExecutionPhase.Prepared or ScanExecutionPhase.Running or
                     ScanExecutionPhase.PausedProviderUnavailable or ScanExecutionPhase.PausedOperatorCorrection))
        {
            execution.FailIntegrity(execution.StateVersion, "recovery-binding-mismatch", observedAt);
        }

        if (coordination.Phase is not (
            TwoRepositoryScanCoordinationPhase.Completed or
            TwoRepositoryScanCoordinationPhase.CompletedWithWarnings or
            TwoRepositoryScanCoordinationPhase.FailedIntegrity or
            TwoRepositoryScanCoordinationPhase.FailedSecurity or
            TwoRepositoryScanCoordinationPhase.Cancelled or
            TwoRepositoryScanCoordinationPhase.FailedPass1))
        {
            coordination.FailIntegrity(coordination.StateVersion, "recovery-binding-mismatch", observedAt);
        }
    }

    private static int CountRemaining(IEnumerable<PairJob> jobs) => jobs.Count(value =>
        value.Job.State is ScanJobExecutionState.Pending or
            ScanJobExecutionState.Running or ScanJobExecutionState.RetryScheduled);

    private static bool IsExactConcurrencyReplay(
        Pass1ConcurrencyChange prior,
        Guid pairExecutionId,
        int requestedLimit,
        long expectedStateVersion,
        string principalId)
    {
        try
        {
            PreparedPass1CanonicalArtifact artifact = PreparedPass1CanonicalCodec.CreateConcurrencyChange(new(
                pairExecutionId,
                checked((ulong)expectedStateVersion),
                checked((ulong)prior.PreviousLimit),
                checked((ulong)requestedLimit),
                checked((ulong)prior.ResultingStateVersion),
                checked((ulong)prior.ConcurrencyRevision),
                principalId));
            return prior.EffectiveLimit == requestedLimit &&
                prior.ChangedByPrincipalId == principalId &&
                prior.Id == artifact.Id &&
                prior.ChangeFingerprint == artifact.FingerprintHex;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsConcurrencyMutable(Pass1PairExecutionPhase phase) =>
        phase is Pass1PairExecutionPhase.Running or
            Pass1PairExecutionPhase.ProbingProvider or
            Pass1PairExecutionPhase.PausedProviderUnavailable or
            Pass1PairExecutionPhase.PausedOperatorCorrection or
            Pass1PairExecutionPhase.PausedInspection;

    private void OpenProviderCircuit(
        Pass1PairExecution pair,
        DateTimeOffset observedAt,
        DateTimeOffset? retryAfterAt)
    {
        DateTimeOffset? boundedRetryAfter = BoundRetryAfter(observedAt, retryAfterAt);
        TransitionProviderCircuit(
            pair,
            Pass1PairExecutionPhase.ProbingProvider,
            Pass1CircuitState.Probing,
            probeOrdinal: 1,
            GetProbeEligibility(pair.Id, 1, observedAt, boundedRetryAfter),
            boundedRetryAfter,
            Pass1PairReasonCategory.ProviderUnavailable,
            "provider-unavailable",
            observedAt);
    }

    private void TransitionProviderCircuit(
        Pass1PairExecution pair,
        Pass1PairExecutionPhase phase,
        Pass1CircuitState circuitState,
        int probeOrdinal,
        DateTimeOffset? nextProbeAt,
        DateTimeOffset? retryAfterAt,
        Pass1PairReasonCategory? reasonCategory,
        string? reasonCode,
        DateTimeOffset observedAt)
    {
        if (observedAt < pair.UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(observedAt));
        }

        context.Entry(pair).Property(value => value.Phase).CurrentValue = phase;
        context.Entry(pair).Property(value => value.CircuitState).CurrentValue = circuitState;
        context.Entry(pair).Property(value => value.ProbeOrdinal).CurrentValue = probeOrdinal;
        context.Entry(pair).Property(value => value.NextProbeAt).CurrentValue = nextProbeAt;
        context.Entry(pair).Property(value => value.RetryAfterAt).CurrentValue = retryAfterAt;
        context.Entry(pair).Property(value => value.ReasonCategory).CurrentValue = reasonCategory;
        context.Entry(pair).Property(value => value.ReasonCode).CurrentValue = reasonCode;
        context.Entry(pair).Property(value => value.StateVersion).CurrentValue = checked(pair.StateVersion + 1);
        context.Entry(pair).Property(value => value.UpdatedAt).CurrentValue = observedAt;
        context.Entry(pair).Property(value => value.TerminalAt).CurrentValue = null;
    }

    private async Task PauseProviderUnavailableAsync(
        Pass1PairExecution pair,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        PairJob[] jobs = await LoadPairJobsAsync(pair, cancellationToken);
        Guid[] scanExecutionIds = jobs.Select(value => value.Job.ScanExecutionId).Distinct().ToArray();
        ScanExecution[] executions = await context.ScanExecutions
            .Where(value => scanExecutionIds.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        foreach (ScanExecution execution in executions.Where(value => value.Phase == ScanExecutionPhase.Running))
        {
            execution.PauseProviderUnavailable(
                execution.StateVersion,
                "provider-unavailable",
                observedAt);
        }

        TwoRepositoryScanCoordination? coordination = await context.TwoRepositoryScanCoordinations
            .SingleOrDefaultAsync(value => value.Id == pair.CoordinationId, cancellationToken);
        if (coordination is not null && coordination.Phase == TwoRepositoryScanCoordinationPhase.Pass1)
        {
            coordination.Pause(
                coordination.StateVersion,
                TwoRepositoryScanReasonCategory.ProviderUnavailable,
                "provider-unavailable",
                observedAt);
        }
    }

    private async Task<Guid[]> LoadCompletedDocumentVersionIdsAsync(
        Guid pairExecutionId,
        CancellationToken cancellationToken) =>
        await context.ScanJobExecutions.AsNoTracking()
            .Where(value => value.CommittedDocumentVersionId != null &&
                context.Pass1NavigationBudgets.Any(budget =>
                    budget.PairExecutionId == pairExecutionId &&
                    budget.ScanJobExecutionId == value.Id))
            .OrderBy(value => value.Id)
            .Select(value => value.CommittedDocumentVersionId!.Value)
            .ToArrayAsync(cancellationToken);

    private static DateTimeOffset? BoundRetryAfter(
        DateTimeOffset observedAt,
        DateTimeOffset? retryAfterAt)
    {
        if (retryAfterAt is null || retryAfterAt <= observedAt)
        {
            return null;
        }

        DateTimeOffset maximum = observedAt.AddMinutes(10);
        return retryAfterAt < maximum ? retryAfterAt : maximum;
    }

    private static DateTimeOffset GetProbeEligibility(
        Guid pairExecutionId,
        int probeOrdinal,
        DateTimeOffset observedAt,
        DateTimeOffset? retryAfterAt)
    {
        TimeSpan delay = probeOrdinal switch
        {
            1 => TimeSpan.FromSeconds(10),
            2 => TimeSpan.FromSeconds(30),
            3 => TimeSpan.FromMinutes(1),
            4 => TimeSpan.FromMinutes(2),
            5 => TimeSpan.FromMinutes(5),
            _ => throw new ArgumentOutOfRangeException(nameof(probeOrdinal)),
        };
        Span<byte> input = stackalloc byte[20];
        pairExecutionId.TryWriteBytes(input[..16], bigEndian: true, out _);
        BinaryPrimitives.WriteInt32BigEndian(input[16..], probeOrdinal);
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(input, digest);
        long jitterTicks = (long)(BinaryPrimitives.ReadUInt64BigEndian(digest) %
            (ulong)(delay.Ticks / 10));
        DateTimeOffset nominal = observedAt.Add(delay).AddTicks(jitterTicks);
        return retryAfterAt > nominal ? retryAfterAt.Value : nominal;
    }

    private void TransitionPair(
        Pass1PairExecution pair,
        Pass1PairExecutionPhase phase,
        Pass1CircuitState circuitState,
        Pass1PairReasonCategory? reasonCategory,
        string? reasonCode,
        DateTimeOffset observedAt)
    {
        if (observedAt < pair.UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(observedAt));
        }

        context.Entry(pair).Property(value => value.Phase).CurrentValue = phase;
        context.Entry(pair).Property(value => value.CircuitState).CurrentValue = circuitState;
        context.Entry(pair).Property(value => value.ReasonCategory).CurrentValue = reasonCategory;
        context.Entry(pair).Property(value => value.ReasonCode).CurrentValue = reasonCode;
        context.Entry(pair).Property(value => value.StateVersion).CurrentValue = checked(pair.StateVersion + 1);
        context.Entry(pair).Property(value => value.UpdatedAt).CurrentValue = observedAt;
        context.Entry(pair).Property(value => value.TerminalAt).CurrentValue = IsTerminal(phase)
            ? observedAt
            : null;
    }

    private static PreparedPass1ProviderCircuitResult ProviderCircuitResult(
        PreparedPass1ProviderCircuitOutcome outcome,
        Pass1PairExecution? pair = null,
        IReadOnlyList<Guid>? completedDocumentVersionIds = null)
    {
        IReadOnlyList<Guid> documentIds = completedDocumentVersionIds ?? [];
        bool paused = pair?.Phase == Pass1PairExecutionPhase.PausedProviderUnavailable;
        return new(
            outcome,
            pair?.CircuitState ?? Pass1CircuitState.Closed,
            pair?.Phase ?? Pass1PairExecutionPhase.Running,
            pair?.ProbeOrdinal ?? 0,
            pair?.NextProbeAt,
            pair?.RetryAfterAt,
            pair?.ReasonCode ?? outcome.ToString(),
            paused ? "继续扫描" : string.Empty,
            documentIds.Count,
            documentIds,
            pair?.Phase == Pass1PairExecutionPhase.Running &&
                pair.CircuitState == Pass1CircuitState.Closed);
    }

    private static bool IsTerminal(Pass1PairExecutionPhase phase) => phase is
        Pass1PairExecutionPhase.ReadyForResolution or
        Pass1PairExecutionPhase.FailedPass1 or
        Pass1PairExecutionPhase.FailedIntegrity or
        Pass1PairExecutionPhase.FailedSecurity or
        Pass1PairExecutionPhase.Cancelled;

    private static string GetFailureCode(ScanJobFailureCategory failureCategory) => failureCategory switch
    {
        ScanJobFailureCategory.ProviderTransient => "provider-unavailable",
        ScanJobFailureCategory.ProviderConfiguration => "provider-configuration",
        ScanJobFailureCategory.JobTransient => "job-transient",
        ScanJobFailureCategory.JobPermanent => "job-permanent",
        ScanJobFailureCategory.Integrity => "integrity-failure",
        ScanJobFailureCategory.Security => "security-failure",
        ScanJobFailureCategory.Cancellation => "cancelled",
        ScanJobFailureCategory.Unclassified => "unclassified-systemic",
        _ => throw new ArgumentOutOfRangeException(nameof(failureCategory)),
    };

    private static PreparedPass1FailureResult FailureResult(
        PreparedPass1FailureOutcome outcome,
        ScanJobFailureCategory failureCategory,
        string failureCode,
        Pass1PairExecution? pair = null,
        ScanJobExecution? job = null,
        int ordinaryFailureCount = 0,
        bool cancellationRequested = false,
        bool documentCommitted = false)
    {
        Pass1PairExecutionPhase? pairState = pair?.Phase;
        bool retryScheduled = job?.State == ScanJobExecutionState.RetryScheduled;
        bool failClosed = pairState is Pass1PairExecutionPhase.FailedIntegrity or
            Pass1PairExecutionPhase.FailedSecurity;
        bool terminal = failClosed ||
            pairState is Pass1PairExecutionPhase.FailedPass1 or Pass1PairExecutionPhase.Cancelled ||
            job?.State is ScanJobExecutionState.Failed or ScanJobExecutionState.Cancelled;
        bool newDispatchAllowed = pairState == Pass1PairExecutionPhase.Running &&
            pair?.CircuitState == Pass1CircuitState.Closed;
        string? resumeAction = pairState switch
        {
            Pass1PairExecutionPhase.PausedProviderUnavailable => "继续扫描",
            Pass1PairExecutionPhase.PausedOperatorCorrection => "修正提供商配置后手动继续扫描",
            Pass1PairExecutionPhase.PausedInspection => "检查系统性失败后手动继续扫描",
            _ => null,
        };

        return new PreparedPass1FailureResult(
            outcome,
            failureCategory,
            failureCode,
            outcome.ToString(),
            retryScheduled,
            job?.AttemptCount ?? 0,
            pair?.DispatchedFileCount ?? 0,
            terminal,
            ordinaryFailureCount,
            pairState,
            resumeAction,
            newDispatchAllowed,
            cancellationRequested || failureCategory == ScanJobFailureCategory.Cancellation,
            retryScheduled,
            failClosed,
            documentCommitted || job?.CommittedDocumentVersionId is not null);
    }

    private static PreparedPass1ReconciliationResult ReconciliationResult(
        PreparedPass1ReconciliationOutcome outcome,
        Pass1PairExecution? pair = null,
        string? fingerprint = null,
        IReadOnlyList<PreparedPass1ReconciledEdge>? edges = null,
        IReadOnlyList<PreparedPass1ReconciliationWarning>? warnings = null,
        int childCount = 0,
        bool isReplay = false,
        bool bindingValid = false)
    {
        edges ??= [];
        warnings ??= [];
        return new(
            outcome,
            pair?.Phase,
            fingerprint,
            edges,
            warnings,
            edges.Count,
            warnings.Count,
            childCount,
            isReplay,
            bindingValid);
    }

    private static PreparedPass1CompletionResult CompletionResult(
        PreparedPass1CompletionOutcome outcome,
        int dispatchedCount = 0,
        int completedCount = 0,
        int ordinaryFailureCount = 0,
        int highValueSeedCount = 0,
        int succeededHighValueSeedCount = 0,
        IReadOnlyList<Pass1FailureInventoryEntry>? inventory = null,
        TwoRepositoryScanCoordinationPhase coordinationPhase = TwoRepositoryScanCoordinationPhase.Pass1,
        bool isReplay = false) => new(
            outcome,
            dispatchedCount,
            completedCount,
            ordinaryFailureCount,
            Math.Min(5, dispatchedCount / 10),
            highValueSeedCount,
            succeededHighValueSeedCount,
            inventory ?? [],
            coordinationPhase,
            LaterPassAuthorized: false,
            isReplay);

    private static PreparedPass1ReconciliationWarning ReconciliationWarning(
        Pass1ReferenceObservation observation,
        string reasonCode,
        string? targetLocator) => new(
            observation.SourceDocumentVersionId,
            observation.ObservationFingerprint,
            reasonCode,
            targetLocator);

    private static bool ReferenceSourceMatches(
        DocumentVersion source,
        ScanJobExecution job)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(source.ContentJson);
            JsonElement binding = document.RootElement.GetProperty("binding");
            return binding.GetProperty("jobId").GetGuid() == job.Id &&
                binding.GetProperty("scanExecutionId").GetGuid() == job.ScanExecutionId &&
                binding.GetProperty("repositoryId").GetGuid() == job.RepositoryId &&
                binding.GetProperty("scanRunId").GetGuid() == job.ScanRunId &&
                binding.GetProperty("fileCodeUnitId").GetGuid() == job.FileCodeUnitId &&
                source.RepositoryId == job.RepositoryId &&
                source.ScanRunId == job.ScanRunId;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static bool WouldCreateCycle(
        Guid sourceDocumentVersionId,
        Guid targetDocumentVersionId,
        IReadOnlyList<DocumentEdge> existingEdges,
        IReadOnlyList<PreparedPass1ReconciledEdge> acceptedEdges)
    {
        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        pending.Push(targetDocumentVersionId);
        while (pending.TryPop(out Guid current))
        {
            if (current == sourceDocumentVersionId)
            {
                return true;
            }

            if (!visited.Add(current))
            {
                continue;
            }

            foreach (Guid next in existingEdges
                         .Where(value => value.SourceDocumentVersionId == current)
                         .Select(value => value.TargetDocumentVersionId)
                         .Concat(acceptedEdges
                             .Where(value => value.SourceDocumentVersionId == current)
                             .Select(value => value.TargetDocumentVersionId)))
            {
                pending.Push(next);
            }
        }

        return false;
    }

    private static string ReconciliationFingerprint(
        IReadOnlyList<PreparedPass1ReconciledEdge> edges,
        IReadOnlyList<PreparedPass1ReconciliationWarning> warnings)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendFingerprintField(hash, "vulgata/story-12.3/pass1-reconciliation/1.0");
        AppendFingerprintField(hash, edges.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (PreparedPass1ReconciledEdge edge in edges)
        {
            AppendFingerprintField(hash, edge.SourceDocumentVersionId.ToString("D"));
            AppendFingerprintField(hash, edge.TargetDocumentVersionId.ToString("D"));
            AppendFingerprintField(hash, edge.Id.ToString("D"));
        }

        AppendFingerprintField(hash, warnings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (PreparedPass1ReconciliationWarning warning in warnings)
        {
            AppendFingerprintField(hash, warning.SourceDocumentVersionId.ToString("D"));
            AppendFingerprintField(hash, warning.ObservationFingerprint);
            AppendFingerprintField(hash, warning.ReasonCode);
            AppendFingerprintField(hash, warning.TargetLocator ?? string.Empty);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendFingerprintField(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static bool IsOptionalHash(string? value) =>
        value is null || value.Length == 64 && value.All(character =>
            char.IsAsciiHexDigit(character) && !char.IsAsciiLetterUpper(character));

    private static bool HasCompleteRequestShape(
        PreparedScanAuthorizationScope authorization,
        PreparedScanHandoff handoff) =>
        authorization.UserId != Guid.Empty &&
        authorization.SystemId != Guid.Empty &&
        handoff.SchemaIdentity == "prepared-scan-handoff/1.0" &&
        handoff.CoordinationId != Guid.Empty &&
        handoff.Manifests.Count == 2 &&
        handoff.SelectedCount is >= 50 and <= 150 &&
        handoff.FileCodeUnits.Count == handoff.SelectedCount &&
        authorization.RepositoryIds.Distinct().Count() == 2 &&
        handoff.Manifests.Select(value => value.RepositoryId).Order()
            .SequenceEqual(authorization.RepositoryIds.Order()) &&
        handoff.Manifests.Select(value => value.RepositoryId).Distinct().Count() == 2;

    private static bool BindingsMatch(
        PreparedScanHandoff handoff,
        IReadOnlyList<PreparedScanManifest> manifests,
        IReadOnlyList<ScanExecution> executions)
    {
        PreparedScanManifestBinding[] bindings = handoff.Manifests.OrderBy(value => value.RepositoryId).ToArray();
        for (int index = 0; index < 2; index++)
        {
            PreparedScanManifestBinding binding = bindings[index];
            PreparedScanManifest manifest = manifests[index];
            ScanExecution execution = executions[index];
            if (binding.RepositoryId != manifest.RepositoryId || binding.ScanRunId != manifest.ScanRunId ||
                binding.ManifestId != manifest.Id || binding.ProfiledRevision != manifest.ProfiledRevision ||
                binding.ManifestFingerprint != manifest.Fingerprint ||
                binding.CodeGraphProofIdentityHash != manifest.CodeGraphProofIdentityHash ||
                binding.JavaReadinessIdentityHash != manifest.JavaReadinessIdentityHash ||
                binding.PreparedScanContextIdentityHash != manifest.PreparedScanContextIdentityHash ||
                execution.RepositoryId != manifest.RepositoryId || execution.ScanRunId != manifest.ScanRunId ||
                execution.ProfiledRevision != manifest.ProfiledRevision)
            {
                return false;
            }
        }

        return true;
    }

    private static bool PackageFingerprintsMatch(
        PreparedScanHandoff handoff,
        IReadOnlyList<PreparedScanManifest> manifests)
    {
        try
        {
            PreparedCanonicalArtifact selection = PreparedScanIdentityBuilders.CreatePairSelection(
                handoff.CoordinationId,
                handoff.FileCodeUnits);
            PreparedCanonicalArtifact package = PreparedScanIdentityBuilders.CreatePairPackage(
                handoff.CoordinationId,
                selection.Fingerprint,
                manifests.Select(value => new PreparedManifestFingerprintBinding(value.RepositoryId, value.Fingerprint)),
                handoff.SelectedCount);
            return selection.Fingerprint == handoff.PairSelectionFingerprint &&
                package.Fingerprint == handoff.PairPackageFingerprint &&
                manifests.All(value => value.PairSelectionFingerprint == handoff.PairSelectionFingerprint);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool UnitMatches(
        PreparedFileCodeUnitDraft unit,
        IReadOnlyList<PreparedScanManifestEntry> entries,
        IReadOnlyList<CodeUnit> storedUnits)
    {
        PreparedScanManifestEntry? entry = entries.SingleOrDefault(value => value.FileCodeUnitId == unit.Id);
        CodeUnit? stored = storedUnits.SingleOrDefault(value => value.Id == unit.Id);
        return entry is not null && stored is not null &&
            unit.IdentityFingerprint == PreparedScanPlanner.FingerprintIdentity(unit) &&
            entry.FileCodeUnitIdentityFingerprint == unit.IdentityFingerprint &&
            entry.ManifestOrdinal == unit.ManifestOrdinal &&
            entry.PairSelectionOrdinal == unit.PairSelectionOrdinal &&
            entry.DirectBusinessScore == unit.DirectBusinessScore &&
            entry.FinalBusinessScore == unit.FinalBusinessScore &&
            entry.SelectionReason == unit.SelectionReason &&
            stored.RepositoryId == unit.RepositoryId && stored.ScanRunId == unit.ScanRunId &&
            stored.FilePath == unit.RelativePath && stored.FileHash == unit.FileHash &&
            stored.SourceHash == unit.SourceHash && stored.ByteStart == unit.ByteStart &&
            stored.ByteEndExclusive == unit.ByteEndExclusive && stored.LineStart == unit.LineStart &&
            stored.LineEnd == unit.LineEnd && stored.SegmentOrdinal == unit.SegmentOrdinal &&
            stored.SegmentCount == unit.SegmentCount &&
            stored.FileCodeUnitIdentityFingerprint == unit.IdentityFingerprint;
    }

    private static PreparedPass1CanonicalArtifact CreatePairIdentity(
        StartSnapshot snapshot,
        PreparedScanHandoff handoff) =>
        PreparedPass1CanonicalCodec.CreatePairExecution(new(
            handoff.CoordinationId,
            snapshot.Coordination.SystemId,
            Convert.FromHexString(handoff.PairPackageFingerprint),
            Convert.FromHexString(handoff.PairSelectionFingerprint),
            snapshot.Manifests.Select((manifest, index) => new PreparedPass1ManifestBinding(
                manifest.RepositoryId,
                manifest.ScanRunId,
                snapshot.Executions[index].Id,
                manifest.Id,
                Convert.FromHexString(manifest.Fingerprint),
                Convert.FromHexString(manifest.ProfiledRevision),
                Convert.FromHexString(manifest.PreparedScanContextIdentityHash))).ToArray(),
            snapshot.Executions.Select(value => value.PromptIdentity!).ToArray(),
            _documentSchemaIdentity,
            snapshot.Executions.Select(value =>
                (ReadOnlyMemory<byte>)Convert.FromHexString(value.AnalysisRuntimeIdentityHash!)).ToArray()));

    private async Task<bool> IsExactReplayAsync(
        Pass1PairExecution pair,
        StartSnapshot snapshot,
        PreparedScanHandoff handoff,
        int initialConcurrencyLimit,
        CancellationToken cancellationToken)
    {
        PreparedPass1CanonicalArtifact identity = CreatePairIdentity(snapshot, handoff);
        if (pair.Id != identity.Id || pair.Pass1PairFingerprint != identity.FingerprintHex ||
            pair.InitialConcurrencyLimit != initialConcurrencyLimit ||
            pair.PairPackageFingerprint != handoff.PairPackageFingerprint ||
            pair.PairSelectionFingerprint != handoff.PairSelectionFingerprint ||
            pair.DispatchedFileCount != snapshot.Units.Length ||
            snapshot.Coordination.Phase != TwoRepositoryScanCoordinationPhase.Pass1 ||
            snapshot.Executions.Any(value => value.Phase != ScanExecutionPhase.Running))
        {
            return false;
        }

        Guid[] expectedJobIds = snapshot.Units.Select(unit => ScanJobExecution.Create(
            snapshot.Executions.Single(value => value.RepositoryId == unit.RepositoryId).Id,
            unit.RepositoryId,
            unit.ScanRunId,
            unit.Id,
            unit.ManifestOrdinal,
            pair.StartedAt).Id).Order().ToArray();
        Guid[] actualJobIds = await context.ScanJobExecutions
            .Where(value => snapshot.Executions.Select(execution => execution.Id).Contains(value.ScanExecutionId))
            .Select(value => value.Id)
            .OrderBy(value => value)
            .ToArrayAsync(cancellationToken);
        Guid[] budgetIds = await context.Pass1NavigationBudgets
            .Where(value => value.PairExecutionId == pair.Id)
            .Select(value => value.ScanJobExecutionId)
            .OrderBy(value => value)
            .ToArrayAsync(cancellationToken);
        return expectedJobIds.SequenceEqual(actualJobIds) && expectedJobIds.SequenceEqual(budgetIds);
    }

    private static int CountHighValueSeeds(IReadOnlyList<PreparedScanManifestEntry> entries) =>
        entries.GroupBy(value => value.PreparedScanManifestId)
            .Sum(group => group.Where(value => value.DirectBusinessScore >= 400)
                .OrderByDescending(value => value.DirectBusinessScore)
                .ThenBy(value => value.PairSelectionOrdinal)
                .Take(PreparedScanPlanner.MaximumHighValueSeedsPerRepository)
                .Count());

    private static byte[]? ParseTargetIdentity(string descriptorJson)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(descriptorJson);
            if (!document.RootElement.TryGetProperty("targetObservationIdentity", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? hex = value.GetString();
            return hex is { Length: 64 } && hex.All(Uri.IsHexDigit)
                ? Convert.FromHexString(hex)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PreparedPass1StartResult Result(PreparedPass1StartOutcome outcome) =>
        new(outcome, null, null);

    private static PreparedPass1ClaimResult ClaimResult(
        PreparedPass1ClaimOutcome outcome,
        int activeLeaseCount = 0,
        int concurrencyLimit = 0) =>
        new(
            outcome,
            Guid.Empty,
            Guid.Empty,
            default,
            0,
            0,
            activeLeaseCount,
            concurrencyLimit);

    private static PreparedPass1ConcurrencyUpdateResult ConcurrencyResult(
        PreparedPass1ConcurrencyUpdateOutcome outcome,
        Pass1PairExecution? pair = null,
        int activeLeaseCount = 0) =>
        new(
            outcome,
            pair?.ConcurrencyLimit,
            pair?.StateVersion,
            pair?.ConcurrencyRevision,
            activeLeaseCount);

    private static PreparedPass1RecoveryResult RecoveryResult(
        PreparedPass1RecoveryOutcome outcome,
        DurableScanReason reason,
        Pass1PairExecution? pair = null,
        int recoveredExpiredLeaseCount = 0,
        int releasedDueRetryCount = 0,
        int completedJobCount = 0,
        int pendingJobCount = 0,
        bool continued = false,
        bool isReplay = false,
        bool pairBindingValid = false,
        string? manualResumeAction = null,
        Guid? pairExecutionId = null) => new(
            outcome,
            reason,
            pair?.Id ?? pairExecutionId,
            pair?.Phase,
            pair?.StateVersion,
            recoveredExpiredLeaseCount,
            releasedDueRetryCount,
            completedJobCount,
            pendingJobCount,
            continued,
            isReplay,
            pairBindingValid,
            manualResumeAction);

    private sealed record StartSnapshot(
        TwoRepositoryScanCoordination Coordination,
        PreparedScanManifest[] Manifests,
        ScanExecution[] Executions,
        PreparedScanManifestEntry[] Entries,
        PreparedFileCodeUnitDraft[] Units);

    private sealed record RecoverySnapshot(
        TwoRepositoryScanCoordination Coordination,
        PreparedScanManifest[] Manifests,
        ScanExecution[] Executions,
        PreparedScanManifestEntry[] Entries,
        CodeUnit[] Units,
        PairJob[] Jobs);

    private sealed record PairJob(
        ScanJobExecution Job,
        int PairSelectionOrdinal);
}

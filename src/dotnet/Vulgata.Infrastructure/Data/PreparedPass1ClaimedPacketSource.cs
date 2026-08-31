using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

/// <summary>
/// Fail-closed reconstruction of one exact prepared File Code Unit from a
/// current durable claim. Authorization material never enters the returned
/// provider-visible packet.
/// </summary>
public sealed class PreparedPass1ClaimedPacketSource(
    VulgataDbContext context,
    IRevisionBoundSourceReader sourceReader,
    TimeProvider timeProvider,
    Microsoft.Extensions.Logging.ILogger<PreparedPass1ClaimedPacketSource> logger) :
    IPreparedPass1ClaimedPacketSource
{
    private static readonly TimeSpan _sourceDeadline = TimeSpan.FromMinutes(5);
    private static readonly Action<ILogger, Guid, Exception?> _claimEnvelopeRejected =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(120301, nameof(_claimEnvelopeRejected)),
            "Prepared Pass 1 packet hydration rejected the claim envelope for {PairExecutionId}.");
    private static readonly Action<ILogger, Guid, Guid, Exception?> _bindingMissing =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Warning,
            new EventId(120302, nameof(_bindingMissing)),
            "Prepared Pass 1 packet hydration could not reconstruct the durable binding for {PairExecutionId} and {JobId}.");
    private static readonly Action<ILogger, Guid, Guid, AnalysisOperationStatus, string, Exception?>
        _sourceRejected = LoggerMessage.Define<Guid, Guid, AnalysisOperationStatus, string>(
            LogLevel.Warning,
            new EventId(120303, nameof(_sourceRejected)),
            "Prepared Pass 1 packet hydration rejected exact source evidence for {PairExecutionId} and {JobId}; source outcome {SourceStatus}/{SourceFailureCode}.");
    private static readonly Action<ILogger, Guid, Guid, Exception?> _revalidationRejected =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Warning,
            new EventId(120304, nameof(_revalidationRejected)),
            "Prepared Pass 1 packet hydration rejected durable revalidation for {PairExecutionId} and {JobId}.");
    private static readonly Action<ILogger, Guid, Guid, Exception?> _hydrationCancelled =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Warning,
            new EventId(120305, nameof(_hydrationCancelled)),
            "Prepared Pass 1 packet hydration was cancelled for {PairExecutionId} and {JobId}.");
    private static readonly Action<ILogger, string, Guid, Guid, Exception?> _hydrationRejected =
        LoggerMessage.Define<string, Guid, Guid>(
            LogLevel.Warning,
            new EventId(120306, nameof(_hydrationRejected)),
            "Prepared Pass 1 packet hydration rejected {FailureType} for {PairExecutionId} and {JobId}.");
    private static readonly IReadOnlyList<PreparedPass1CommunicationClassificationProjection>
        _noClassifications = [];
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<PreparedPass1WorkerPacket?> HydrateAsync(
        Guid pairExecutionId,
        PreparedPass1ClaimResult claim,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        if (claim is null ||
            cancellationToken.IsCancellationRequested ||
            pairExecutionId == Guid.Empty ||
            claim.Outcome != PreparedPass1ClaimOutcome.Claimed ||
            claim.JobId == Guid.Empty ||
            claim.LeaseToken == Guid.Empty ||
            claim.JobStateVersion <= 0 ||
            claim.PairStateVersion < 0 ||
            observedAt.Offset != TimeSpan.Zero ||
            claim.LeaseExpiresAt.Offset != TimeSpan.Zero ||
            claim.LeaseExpiresAt <= observedAt)
        {
            _claimEnvelopeRejected(logger, pairExecutionId, null);
            return null;
        }

        try
        {
            ClaimedBinding? binding = await LoadCurrentBindingAsync(
                pairExecutionId,
                claim,
                observedAt,
                cancellationToken);
            if (binding is null)
            {
                _bindingMissing(logger, pairExecutionId, claim.JobId, null);
                return null;
            }

            var scope = new AnalysisScope(
                binding.RepositoryId,
                binding.ScanRunId,
                binding.ProfiledRevision,
                PreparedPass1RevisionBindingToken.Create(
                    pairExecutionId,
                    binding.RepositoryId,
                    binding.ScanRunId,
                    binding.ProfiledRevision),
                binding.AnalysisRuntimeIdentityHash,
                new AnalysisDeadline(observedAt.Add(_sourceDeadline).ToUnixTimeMilliseconds()));
            SourceExcerptResult source = await sourceReader.ReadAsync(
                scope,
                new RevisionBoundSourceRequest(
                    binding.RepositoryRelativePath,
                    new AnalysisLineRange(binding.LineStart, binding.LineEnd),
                    binding.FileHash,
                    checked((int)(binding.ByteEnd - binding.ByteStart)),
                    binding.ByteStart,
                    binding.ByteEnd),
                cancellationToken);

            byte[] sourceUtf8 = Encoding.UTF8.GetBytes(source.Excerpt);
            if (!IsExactSource(binding, source, sourceUtf8))
            {
                _sourceRejected(
                    logger,
                    pairExecutionId,
                    claim.JobId,
                    source.Status,
                    source.Failure?.FailureCode ?? "none",
                    null);
                return null;
            }

            context.ChangeTracker.Clear();
            DateTimeOffset revalidatedAt = _timeProvider.GetUtcNow();
            if (revalidatedAt < observedAt)
            {
                revalidatedAt = observedAt;
            }

            ClaimedBinding? revalidated = await LoadCurrentBindingAsync(
                pairExecutionId,
                claim,
                revalidatedAt,
                cancellationToken);
            if (revalidated is null || !Matches(binding, revalidated))
            {
                _revalidationRejected(logger, pairExecutionId, claim.JobId, null);
                return null;
            }

            PreparedPass1NavigationBudgetProjection budget = binding.NavigationBudget;
            return new PreparedPass1WorkerPacket(
                pairExecutionId,
                binding.JobId,
                binding.ScanExecutionId,
                binding.SystemId,
                binding.RepositoryId,
                binding.ScanRunId,
                binding.ProfiledRevision,
                binding.FileCodeUnitId,
                binding.FileCodeUnitIdentityFingerprint,
                binding.PreparedScanManifestId,
                binding.PreparedScanManifestFingerprint,
                binding.PairPackageFingerprint,
                binding.RepositoryRelativePath,
                binding.ByteStart,
                binding.ByteEnd,
                binding.LineStart,
                binding.LineEnd,
                binding.SegmentOrdinal,
                binding.SegmentCount,
                binding.FileHash,
                binding.SourceHash,
                binding.PromptIdentity,
                binding.DocumentSchemaIdentity,
                binding.AnalysisRuntimeIdentityHash,
                binding.ManifestOrdinal,
                binding.PairSelectionOrdinal,
                sourceUtf8,
                binding.ContextSnapshot,
                binding.CommunicationClassifications)
            {
                SymbolName = binding.SymbolName,
                NavigationBudget = budget,
                RemainingNavigationBudget = 8 - budget.ToolAttemptCount,
            };
        }
        catch (OperationCanceledException)
        {
            _hydrationCancelled(logger, pairExecutionId, claim.JobId, null);
            return null;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            _hydrationRejected(
                logger,
                exception.GetType().Name,
                pairExecutionId,
                claim.JobId,
                null);
            return null;
        }
    }

    private async Task<ClaimedBinding?> LoadCurrentBindingAsync(
        Guid pairExecutionId,
        PreparedPass1ClaimResult claim,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        Pass1PairExecution? pair = await context.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pairExecutionId, cancellationToken);
        if (pair is null ||
            pair.StateVersion != claim.PairStateVersion ||
            pair.Phase != Pass1PairExecutionPhase.Running ||
            pair.CircuitState != Pass1CircuitState.Closed)
        {
            return null;
        }

        ScanJobExecution? job = await context.ScanJobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == claim.JobId, cancellationToken);
        if (!IsCurrentJob(job, claim, observedAt) || job is null)
        {
            return null;
        }

        Pass1NavigationBudget? budget = await context.Pass1NavigationBudgets.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.ScanJobExecutionId == claim.JobId,
                cancellationToken);
        if (budget is null ||
            budget.PairExecutionId != pairExecutionId ||
            budget.ToolAttemptCount is < 0 or > 8 ||
            budget.AcceptedCallCount is < 0 or > 8 ||
            budget.AcceptedCallCount > budget.ToolAttemptCount ||
            budget.EvidenceBytes is < 0 or > 65_536 ||
            budget.StateVersion < budget.ToolAttemptCount)
        {
            return null;
        }

        ScanExecution? execution = await context.ScanExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == job.ScanExecutionId, cancellationToken);
        CodeUnit? unit = await context.CodeUnits.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == job.FileCodeUnitId, cancellationToken);
        if (execution is null || unit is null ||
            execution.SystemId != pair.SystemId ||
            execution.RepositoryId != job.RepositoryId ||
            execution.ScanRunId != job.ScanRunId ||
            unit.RepositoryId != job.RepositoryId ||
            unit.ScanRunId != job.ScanRunId ||
            string.IsNullOrWhiteSpace(execution.PromptIdentity) ||
            !string.Equals(execution.DocumentSchemaIdentity, "code-logic/file-v2", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(execution.AnalysisRuntimeIdentityHash) ||
            string.IsNullOrWhiteSpace(unit.FileCodeUnitIdentityFingerprint) ||
            string.IsNullOrWhiteSpace(unit.FileHash) ||
            unit.ByteStart is null || unit.ByteEndExclusive is null ||
            unit.SegmentOrdinal is null || unit.SegmentCount is null)
        {
            return null;
        }

        PreparedScanManifestEntry? entry = await context.PreparedScanManifestEntries.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.FileCodeUnitId == unit.Id &&
                value.ManifestOrdinal == claim.ManifestOrdinal &&
                value.PairSelectionOrdinal == claim.PairSelectionOrdinal &&
                (value.PreparedScanManifestId == pair.ManifestOneId ||
                    value.PreparedScanManifestId == pair.ManifestTwoId),
                cancellationToken);
        if (entry is null ||
            !string.Equals(
                entry.FileCodeUnitIdentityFingerprint,
                unit.FileCodeUnitIdentityFingerprint,
                StringComparison.Ordinal))
        {
            return null;
        }

        PreparedScanManifest? manifest = await context.PreparedScanManifests.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == entry.PreparedScanManifestId, cancellationToken);
        ScanContextSnapshot? snapshot = manifest is null
            ? null
            : await context.ScanContextSnapshots.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == manifest.ContextSnapshotId, cancellationToken);
        bool authorized = await context.SystemOwnerAssignments.AsNoTracking()
            .AnyAsync(value => value.SystemId == pair.SystemId, cancellationToken);
        bool repositoryBound = await context.Repositories.AsNoTracking()
            .AnyAsync(value => value.Id == job.RepositoryId && value.SystemId == pair.SystemId, cancellationToken);
        if (!authorized || !repositoryBound || manifest is null || snapshot is null ||
            manifest.ApprovalState != PreparedScanManifestApprovalState.Approved ||
            manifest.CoordinationId != pair.CoordinationId ||
            manifest.RepositoryId != job.RepositoryId ||
            manifest.ScanRunId != job.ScanRunId ||
            !string.Equals(manifest.ProfiledRevision, execution.ProfiledRevision, StringComparison.Ordinal) ||
            !string.Equals(manifest.PairSelectionFingerprint, pair.PairSelectionFingerprint, StringComparison.Ordinal) ||
            !string.Equals(snapshot.ProfiledRevision, execution.ProfiledRevision, StringComparison.Ordinal) ||
            snapshot.RepositoryId != job.RepositoryId ||
            snapshot.ScanRunId != job.ScanRunId ||
            !string.Equals(snapshot.PreparedScanContextIdentityHash, manifest.PreparedScanContextIdentityHash, StringComparison.Ordinal) ||
            !string.Equals(snapshot.ContentHash, manifest.ContextContentHash, StringComparison.Ordinal))
        {
            return null;
        }

        PreparedCanonicalArtifact unitIdentity = PreparedScanIdentityBuilders.CreateApplicableFileCodeUnitIdentity(
            unit.RepositoryId,
            unit.ScanRunId,
            manifest.ProfiledRevision,
            unit.FilePath,
            unit.SymbolName,
            unit.FileHash,
            unit.ByteStart.Value,
            unit.ByteEndExclusive.Value,
            unit.LineStart,
            unit.LineEnd,
            unit.SegmentOrdinal.Value,
            unit.SegmentCount.Value,
            unit.SourceHash);
        if (!string.Equals(entry.FileCodeUnitIdentityFingerprint, unitIdentity.Fingerprint, StringComparison.Ordinal) ||
            !string.Equals(unit.FileCodeUnitIdentityFingerprint, unitIdentity.Fingerprint, StringComparison.Ordinal))
        {
            return null;
        }

        Guid? preScanResultId = await context.PreScanResults.AsNoTracking()
            .Where(value => value.PreparedScanManifestId == manifest.Id)
            .Select(value => (Guid?)value.Id)
            .SingleOrDefaultAsync(cancellationToken);
        CommunicationKindClassification[] classifications = preScanResultId is null
            ? []
            : await context.CommunicationKindClassifications.AsNoTracking()
                .Where(value =>
                    value.PreScanResultId == preScanResultId &&
                    value.RelativePath == unit.FilePath &&
                    value.Locator.StartsWith(unit.FilePath + "#"))
                .OrderBy(value => value.Fingerprint)
                .ToArrayAsync(cancellationToken);
        if (classifications.Any(value => string.IsNullOrWhiteSpace(value.CatalogRuleIdentity)))
        {
            return null;
        }

        var snapshotProjection = new PreparedPass1ContextSnapshotProjection(
            snapshot.Id,
            snapshot.RepositoryId,
            snapshot.ScanRunId,
            snapshot.ProfiledRevision,
            snapshot.PreparedScanContextIdentityHash!,
            snapshot.ContentHash,
            snapshot.GlobalContext,
            snapshot.SystemContext,
            snapshot.RepositoryContext);
        PreparedPass1CommunicationClassificationProjection[] classificationProjections = [.. classifications.Select(value =>
            new PreparedPass1CommunicationClassificationProjection(
                unit.Id,
                value.Id,
                manifest.Id,
                manifest.RepositoryId,
                manifest.ScanRunId,
                manifest.ProfiledRevision,
                value.Fingerprint,
                value.Kind,
                value.Level,
                value.Confidence,
                value.Rule,
                value.Provenance,
                value.CatalogRuleIdentity!,
                value.RelativePath,
                value.Locator))];

        return new ClaimedBinding(
            job.Id,
            job.ScanExecutionId,
            pair.SystemId,
            job.RepositoryId,
            job.ScanRunId,
            execution.ProfiledRevision,
            unit.Id,
            unit.FileCodeUnitIdentityFingerprint,
            manifest.Id,
            manifest.Fingerprint,
            pair.PairPackageFingerprint,
            unit.FilePath,
            unit.SymbolName,
            unit.ByteStart.Value,
            unit.ByteEndExclusive.Value,
            unit.LineStart,
            unit.LineEnd,
            unit.SegmentOrdinal.Value,
            unit.SegmentCount.Value,
            unit.FileHash,
            unit.SourceHash,
            execution.PromptIdentity,
            execution.DocumentSchemaIdentity!,
            execution.AnalysisRuntimeIdentityHash,
            job.ManifestOrdinal,
            claim.PairSelectionOrdinal,
            snapshotProjection,
            Array.AsReadOnly(classificationProjections),
            new PreparedPass1NavigationBudgetProjection(
                budget.ToolAttemptCount,
                budget.StateVersion,
                budget.AcceptedCallCount,
                budget.EvidenceBytes));
    }

    private static bool IsCurrentJob(
        ScanJobExecution? job,
        PreparedPass1ClaimResult claim,
        DateTimeOffset observedAt) =>
        job is not null &&
        job.State == ScanJobExecutionState.Running &&
        job.StateVersion == claim.JobStateVersion &&
        job.LeaseToken == claim.LeaseToken &&
        job.LeaseExpiresAt == claim.LeaseExpiresAt &&
        job.LeaseExpiresAt > observedAt &&
        job.ManifestOrdinal == claim.ManifestOrdinal;

    private static bool IsExactSource(
        ClaimedBinding binding,
        SourceExcerptResult source,
        byte[] sourceUtf8) =>
        source.Status == AnalysisOperationStatus.Succeeded &&
        source.Failure is null &&
        source.RepositoryId == binding.RepositoryId &&
        string.Equals(source.ProfiledRevision, binding.ProfiledRevision, StringComparison.Ordinal) &&
        string.Equals(source.RelativePath, binding.RepositoryRelativePath, StringComparison.Ordinal) &&
        source.Range.StartLine == binding.LineStart &&
        source.Range.EndLine == binding.LineEnd &&
        string.Equals(source.SourceSha256, binding.FileHash, StringComparison.OrdinalIgnoreCase) &&
        source.ByteCount == sourceUtf8.Length &&
        sourceUtf8.LongLength == binding.ByteEnd - binding.ByteStart &&
        string.Equals(
            Convert.ToHexStringLower(SHA256.HashData(sourceUtf8)),
            binding.SourceHash,
            StringComparison.OrdinalIgnoreCase);

    private static bool Matches(ClaimedBinding first, ClaimedBinding second) =>
        first with { CommunicationClassifications = _noClassifications } ==
            second with { CommunicationClassifications = _noClassifications } &&
        first.CommunicationClassifications.SequenceEqual(second.CommunicationClassifications);

    private sealed record ClaimedBinding(
        Guid JobId,
        Guid ScanExecutionId,
        Guid SystemId,
        Guid RepositoryId,
        Guid ScanRunId,
        string ProfiledRevision,
        Guid FileCodeUnitId,
        string FileCodeUnitIdentityFingerprint,
        Guid PreparedScanManifestId,
        string PreparedScanManifestFingerprint,
        string PairPackageFingerprint,
        string RepositoryRelativePath,
        string SymbolName,
        long ByteStart,
        long ByteEnd,
        int LineStart,
        int LineEnd,
        int SegmentOrdinal,
        int SegmentCount,
        string FileHash,
        string SourceHash,
        string PromptIdentity,
        string DocumentSchemaIdentity,
        string AnalysisRuntimeIdentityHash,
        int ManifestOrdinal,
        int PairSelectionOrdinal,
        PreparedPass1ContextSnapshotProjection ContextSnapshot,
        IReadOnlyList<PreparedPass1CommunicationClassificationProjection> CommunicationClassifications,
        PreparedPass1NavigationBudgetProjection NavigationBudget);
}

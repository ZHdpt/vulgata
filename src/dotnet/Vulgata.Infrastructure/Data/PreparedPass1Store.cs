using System.Data;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedPass1Store(VulgataDbContext context) : IPreparedPass1Store
{
    private const string _documentSchemaIdentity = "code-logic/file-v2";

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

    private sealed record StartSnapshot(
        TwoRepositoryScanCoordination Coordination,
        PreparedScanManifest[] Manifests,
        ScanExecution[] Executions,
        PreparedScanManifestEntry[] Entries,
        PreparedFileCodeUnitDraft[] Units);

    private sealed record PairJob(
        ScanJobExecution Job,
        int PairSelectionOrdinal);
}

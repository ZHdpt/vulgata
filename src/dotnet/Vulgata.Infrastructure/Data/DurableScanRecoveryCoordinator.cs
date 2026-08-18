using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class DurableScanRecoveryCoordinator(
    IDurableScanStore store,
    IScanRecoveryRevalidator revalidator,
    TimeProvider timeProvider) : IScanRecoveryCoordinator
{
    public async Task<ScanRecoveryResult> ResumeAsync(
        Guid scanExecutionId,
        DurableScanAuthorizationScope authorizationScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);

        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        try
        {
            ScanRecoverySnapshotResult read = await store.ReadRecoverySnapshotAsync(
                authorizationScope,
                scanExecutionId,
                cancellationToken);
            if (read.Snapshot is not ScanRecoverySnapshot snapshot)
            {
                return new(read.Outcome, read.Reason, null);
            }

            ScanRecoveryRevalidationResult revalidation = await revalidator.RevalidateAsync(
                snapshot,
                authorizationScope,
                cancellationToken);
            if (!IsAcceptedRevalidation(revalidation))
            {
                DurableScanReason reason = IsBoundedStopReason(revalidation.Reason)
                    ? revalidation.Reason
                    : DurableScanReason.IntegrityViolation;
                return new(DurableScanStoreOutcome.Rejected, reason, snapshot.StateVersion);
            }

            DateTimeOffset observedAt = timeProvider.GetUtcNow();
            int recoveredExpiredLeaseCount = 0;
            int releasedDueRetryCount = 0;
            bool stateChanged = false;
            foreach (ScanRecoveryJobSnapshot job in snapshot.Jobs)
            {
                if (job.State == ScanJobExecutionState.Completed)
                {
                    DurableScanStoreResult verified = await store.ApplyRecoveryTransitionAsync(
                        authorizationScope,
                        new ScanRecoveryTransitionCommand(
                            snapshot.ScanExecutionId,
                            snapshot.StateVersion,
                            job.ScanJobExecutionId,
                            job.StateVersion,
                            ScanRecoveryTransitionKind.NoChange,
                            observedAt),
                        cancellationToken);
                    if (verified.Outcome is not DurableScanStoreOutcome.Applied and
                        not DurableScanStoreOutcome.Replay)
                    {
                        return new(
                            DurableScanStoreOutcome.Conflict,
                            DurableScanReason.IntegrityViolation,
                            verified.StateVersion);
                    }

                    continue;
                }

                ScanRecoveryTransitionKind? transition = SelectTransition(job, observedAt);
                if (transition is null)
                {
                    continue;
                }

                DurableScanStoreResult applied = await store.ApplyRecoveryTransitionAsync(
                    authorizationScope,
                    new ScanRecoveryTransitionCommand(
                        snapshot.ScanExecutionId,
                        snapshot.StateVersion,
                        job.ScanJobExecutionId,
                        job.StateVersion,
                        transition.Value,
                        observedAt),
                    cancellationToken);
                if (applied.Outcome is not DurableScanStoreOutcome.Applied and
                    not DurableScanStoreOutcome.Replay)
                {
                    return new(applied.Outcome, applied.Reason, applied.StateVersion);
                }

                if (applied.Outcome == DurableScanStoreOutcome.Applied)
                {
                    stateChanged = true;
                    if (transition == ScanRecoveryTransitionKind.ExpiredLeaseToPending)
                    {
                        recoveredExpiredLeaseCount++;
                    }
                    else
                    {
                        releasedDueRetryCount++;
                    }
                }
            }

            if (snapshot.Phase is ScanExecutionPhase.PausedProviderUnavailable or
                ScanExecutionPhase.PausedOperatorCorrection)
            {
                DurableScanStoreResult resumed = await store.ApplyRecoveryTransitionAsync(
                    authorizationScope,
                    new ScanRecoveryTransitionCommand(
                        snapshot.ScanExecutionId,
                        snapshot.StateVersion,
                        null,
                        null,
                        ScanRecoveryTransitionKind.PausedToPrepared,
                        observedAt),
                    cancellationToken);
                if (resumed.Outcome is not DurableScanStoreOutcome.Applied and
                    not DurableScanStoreOutcome.Replay)
                {
                    return new(resumed.Outcome, resumed.Reason, resumed.StateVersion);
                }

                stateChanged |= resumed.Outcome == DurableScanStoreOutcome.Applied;
            }

            ScanRecoverySnapshotResult reread = await store.ReadRecoverySnapshotAsync(
                authorizationScope,
                scanExecutionId,
                cancellationToken);
            if (reread.Snapshot is not ScanRecoverySnapshot durable)
            {
                return new(reread.Outcome, reread.Reason, null);
            }

            ScanRecoveryView view = CreateView(
                durable,
                recoveredExpiredLeaseCount,
                releasedDueRetryCount,
                stateChanged);
            return new(
                DurableScanStoreOutcome.Applied,
                DurableScanReason.None,
                durable.StateVersion,
                view,
                IsReplay: !stateChanged);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }
    }

    private static ScanRecoveryTransitionKind? SelectTransition(
        ScanRecoveryJobSnapshot job,
        DateTimeOffset observedAt)
    {
        if (job.State == ScanJobExecutionState.Running &&
            job.LeaseExpiresAt is DateTimeOffset leaseExpiresAt &&
            leaseExpiresAt < observedAt)
        {
            return ScanRecoveryTransitionKind.ExpiredLeaseToPending;
        }

        if (job.State == ScanJobExecutionState.RetryScheduled &&
            job.NextAttemptAt is DateTimeOffset nextAttemptAt &&
            nextAttemptAt <= observedAt)
        {
            return ScanRecoveryTransitionKind.DueRetryToPending;
        }

        return null;
    }

    private static bool IsAcceptedRevalidation(ScanRecoveryRevalidationResult result) =>
        result.IsValid && result.Reason == DurableScanReason.None;

    private static ScanRecoveryView CreateView(
        ScanRecoverySnapshot snapshot,
        int recoveredExpiredLeaseCount,
        int releasedDueRetryCount,
        bool stateChanged)
    {
        IReadOnlyList<ScanRecoveryJobSnapshot> jobs = snapshot.Jobs;
        return new ScanRecoveryView(
            snapshot.ScanExecutionId,
            snapshot.Phase,
            jobs.Count,
            jobs.Count(job => job.State == ScanJobExecutionState.Completed),
            jobs.Count(job => job.State == ScanJobExecutionState.Pending),
            jobs.Count(job => job.State == ScanJobExecutionState.Running),
            jobs.Count(job => job.State == ScanJobExecutionState.RetryScheduled),
            jobs.Count(job => job.State == ScanJobExecutionState.Failed),
            jobs.Count(job => job.State == ScanJobExecutionState.Cancelled),
            recoveredExpiredLeaseCount,
            releasedDueRetryCount,
            stateChanged,
            DurableScanReason.None);
    }

    private static bool IsBoundedStopReason(DurableScanReason reason) =>
        reason is DurableScanReason.Unauthorized or
            DurableScanReason.InvalidIdentity or
            DurableScanReason.ProviderUnavailable or
            DurableScanReason.IntegrityViolation or
            DurableScanReason.Cancelled;

    private static ScanRecoveryResult Cancelled() =>
        new(DurableScanStoreOutcome.Rejected, DurableScanReason.Cancelled, null);
}

using System.Data;
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public enum PreparedPass2StartOutcome
{
    Created,
    Replay,
    Conflict,
    Rejected,
    NotFound,
    Cancelled,
    Failed,
}

public sealed record PreparedPass2StartResult(
    PreparedPass2StartOutcome Outcome,
    Guid? ExecutionId,
    int FlowGroupCount,
    int OmissionCount,
    int JobCount,
    int RetrievalBudgetCount,
    bool IsReplay);

public enum PreparedPass2ClaimOutcome
{
    Claimed,
    Active,
    Exhausted,
    Ineligible,
    Conflict,
    Cancelled,
}

public sealed record PreparedPass2ClaimResult(
    PreparedPass2ClaimOutcome Outcome,
    Guid ExecutionId,
    Guid? JobId,
    Guid? FlowGroupId,
    int GroupOrder,
    int AttemptOrdinal,
    long JobStateVersion,
    Guid? LeaseToken,
    DateTimeOffset? LeaseExpiresAt,
    DateTimeOffset? NextEligibleAt);

public enum PreparedPass2RecoveryOutcome
{
    Continued,
    PausedProviderUnavailable,
    Completed,
    NoOpReplay,
    NotFound,
    Rejected,
    Conflict,
    Cancelled,
}

public sealed record PreparedPass2RecoveryResult(
    PreparedPass2RecoveryOutcome Outcome,
    Guid? ExecutionId,
    Pass2ExecutionState? State,
    long? StateVersion,
    int RecoveredJobCount,
    int FailedJobCount,
    bool IsReplay);

public enum PreparedPass2DispatchFailureOutcome
{
    RetryScheduled,
    PausedProviderUnavailable,
    Failed,
    Conflict,
    Cancelled,
}

internal sealed record Pass2RetrievalReservation(
    bool IsReserved,
    int AttemptOrdinal,
    int CallOrdinal,
    string ToolCallIdFingerprint,
    string RequestFingerprint,
    long JobStateVersion,
    Guid LeaseToken,
    DateTimeOffset LeaseExpiresAt,
    Pass2CodeLogicRetrievalRemainingBudget RemainingBudget)
{
    internal Func<CancellationToken, Task<bool>>? AdmissionAuthorityCheck { get; init; }
}

internal sealed record Pass2ExplicitRetrievalResolution(
    Pass2RetrievalReservation? Reservation,
    Pass2CodeLogicRetrievalResult? CompletedResult,
    Pass2RetrievalReceipt? CompletedReceipt = null);

public sealed class PreparedPass2Store
{
    private const int _maxFlowGroups = 10;
    private readonly VulgataDbContext _dbContext;
    private readonly IPreparedPass2WorkerContractAuthority? _workerContractAuthority;

    public PreparedPass2Store(VulgataDbContext dbContext)
        : this(dbContext, null, true)
    {
    }

    public PreparedPass2Store(
        VulgataDbContext dbContext,
        IPreparedPass2WorkerContractAuthority workerContractAuthority)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _workerContractAuthority = workerContractAuthority ??
            throw new ArgumentNullException(nameof(workerContractAuthority));
    }

    private PreparedPass2Store(
        VulgataDbContext dbContext,
        IPreparedPass2WorkerContractAuthority? workerContractAuthority,
        bool compatibilityAdapter = true)
    {
        _ = compatibilityAdapter;
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _workerContractAuthority = workerContractAuthority;
    }

    internal async Task<Pass2RetrievalReservation> ReserveRetrievalAsync(
        Guid pass2JobExecutionId,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await ReserveRetrievalOnceAsync(
                    pass2JobExecutionId,
                    request,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (attempt < 3 && IsSerializationFailure(exception))
            {
                _dbContext.ChangeTracker.Clear();
            }
        }
    }

    public async Task<PreparedPass2ClaimResult> ClaimNextAsync(
        Guid pass2ExecutionId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(pass2ExecutionId, Guid.Empty);
        RequireUtc(now, nameof(now));
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(30))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }
        if (cancellationToken.IsCancellationRequested)
        {
            return Claim(PreparedPass2ClaimOutcome.Cancelled, pass2ExecutionId);
        }

        try
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            Pass2Execution? execution = await LockExecutionAsync(
                pass2ExecutionId, cancellationToken).ConfigureAwait(false);
            if (execution is null || execution.State != Pass2ExecutionState.Running)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Claim(PreparedPass2ClaimOutcome.Ineligible, pass2ExecutionId);
            }

            Pass2JobExecution[] jobs = await LockJobsAsync(
                pass2ExecutionId, cancellationToken).ConfigureAwait(false);
            _ = await RecoverJobsAsync(jobs, execution, now, cancellationToken)
                .ConfigureAwait(false);
            Pass2JobExecution? active = jobs.SingleOrDefault(value =>
                value.State == Pass2JobExecutionState.Running);
            if (active is not null)
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(
                    PreparedPass2ClaimOutcome.Active,
                    pass2ExecutionId,
                    active.Id,
                    active.FlowGroupId,
                    active.GroupOrder,
                    active.AttemptCount,
                    active.StateVersion,
                    active.LeaseToken,
                    active.LeaseExpiresAt,
                    active.LeaseExpiresAt);
            }

            Pass2JobExecution? next = jobs
                .Where(value => value.State == Pass2JobExecutionState.Pending)
                .OrderBy(value => value.GroupOrder)
                .ThenBy(value => value.Id)
                .FirstOrDefault();
            if (next is not null)
            {
                Guid leaseToken = Guid.NewGuid();
                next.AcquireLease(leaseToken, now.Add(leaseDuration), now);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(
                    PreparedPass2ClaimOutcome.Claimed,
                    pass2ExecutionId,
                    next.Id,
                    next.FlowGroupId,
                    next.GroupOrder,
                    next.AttemptCount,
                    next.StateVersion,
                    leaseToken,
                    next.LeaseExpiresAt,
                    null);
            }

            DateTimeOffset? nextEligibleAt = jobs
                .Where(value => value.State == Pass2JobExecutionState.RetryScheduled)
                .Select(value => value.NextAttemptAt)
                .Where(value => value.HasValue)
                .Min();
            if (nextEligibleAt is null)
            {
                _ = await CompleteIfTerminalAsync(
                    execution, now, cancellationToken).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(
                nextEligibleAt is null
                    ? PreparedPass2ClaimOutcome.Exhausted
                    : PreparedPass2ClaimOutcome.Ineligible,
                pass2ExecutionId,
                null,
                null,
                0,
                0,
                0,
                null,
                null,
                nextEligibleAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _dbContext.ChangeTracker.Clear();
            return Claim(PreparedPass2ClaimOutcome.Cancelled, pass2ExecutionId);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException)
        {
            _dbContext.ChangeTracker.Clear();
            return Claim(PreparedPass2ClaimOutcome.Conflict, pass2ExecutionId);
        }
    }

    public async Task<PreparedPass2RecoveryResult> RecoverAsync(
        Guid pass2ExecutionId,
        RepositoryAccessScope scope,
        bool providerAvailable,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(pass2ExecutionId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(scope);
        RequireUtc(now, nameof(now));
        if (cancellationToken.IsCancellationRequested)
        {
            return Recovery(PreparedPass2RecoveryOutcome.Cancelled, pass2ExecutionId);
        }

        try
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            Pass2Execution? execution = await LockExecutionAsync(
                pass2ExecutionId, cancellationToken).ConfigureAwait(false);
            if (execution is null)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Recovery(PreparedPass2RecoveryOutcome.NotFound, pass2ExecutionId);
            }
            Guid[] repositoryIds = await ExecutionRepositoryIdsAsync(
                execution, cancellationToken).ConfigureAwait(false);
            if (repositoryIds.Length != 2 || !scope.HasLiveAuthorizationVerifier ||
                !await scope.HasCurrentAccessAsync(repositoryIds, cancellationToken)
                    .ConfigureAwait(false))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Recovery(PreparedPass2RecoveryOutcome.Rejected, pass2ExecutionId);
            }
            if (execution.State is Pass2ExecutionState.Completed or
                Pass2ExecutionState.CompletedWithWarnings)
            {
                int terminalCount = checked(
                    execution.PublishedCount + execution.FailedCount +
                    execution.TerminalIncompleteCount + execution.TerminalBoundExcludedCount +
                    execution.TerminalOmissionCount);
                if (!execution.PlanRecorded || terminalCount != execution.PlannedCount ||
                    execution.TerminalWarningInventory is null)
                {
                    execution.RejectCorruptTerminal(execution.StateVersion, now);
                    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return Recovery(PreparedPass2RecoveryOutcome.Rejected, pass2ExecutionId);
                }
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return new(
                    PreparedPass2RecoveryOutcome.Completed,
                    execution.Id,
                    execution.State,
                    execution.StateVersion,
                    0,
                    0,
                    true);
            }

            TwoRepositoryScanCoordination coordination = await LockCoordinationAsync(
                execution.CoordinationId, cancellationToken).ConfigureAwait(false) ??
                throw new InvalidOperationException("The Pass 2 coordination is unavailable.");
            Pass2JobExecution[] jobs = await LockJobsAsync(
                execution.Id, cancellationToken).ConfigureAwait(false);
            int recovered = 0;
            int failed = 0;
            if (execution.State == Pass2ExecutionState.PausedProviderUnavailable)
            {
                if (!providerAvailable)
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    return new(
                        PreparedPass2RecoveryOutcome.PausedProviderUnavailable,
                        execution.Id,
                        execution.State,
                        execution.StateVersion,
                        0,
                        0,
                        true);
                }

                execution.ResumeProvider(execution.StateVersion, now);
                coordination.ResumePass2(coordination.StateVersion, now);
            }
            else if (execution.State != Pass2ExecutionState.Running)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Recovery(PreparedPass2RecoveryOutcome.Conflict, pass2ExecutionId);
            }

            foreach (Pass2JobExecution job in jobs.OrderBy(value => value.GroupOrder))
            {
                Pass2JobExecutionState before = job.State;
                if (job.State == Pass2JobExecutionState.Running &&
                    job.LeaseExpiresAt is { } leaseExpiresAt && leaseExpiresAt <= now)
                {
                    job.RecoverExpiredLease(job.StateVersion, now);
                    recovered++;
                    if (job.State == Pass2JobExecutionState.Failed)
                    {
                        await MarkFailedAsync(job, execution, cancellationToken).ConfigureAwait(false);
                        failed++;
                    }
                }
                else if (job.State == Pass2JobExecutionState.RetryScheduled &&
                    job.NextAttemptAt is { } nextAttemptAt && nextAttemptAt <= now)
                {
                    job.RecoverDueRetry(job.StateVersion, now);
                    recovered++;
                }
                _ = before;
            }

            if (!providerAvailable)
            {
                Pass2JobExecution? running = jobs.SingleOrDefault(value =>
                    value.State == Pass2JobExecutionState.Running);
                if (running is not null && running.LeaseToken is { } leaseToken)
                {
                    running.ReleaseForProviderPause(running.StateVersion, leaseToken, now);
                }
                execution.PauseProviderUnavailable(execution.StateVersion, now);
                coordination.Pause(
                    coordination.StateVersion,
                    TwoRepositoryScanReasonCategory.ProviderUnavailable,
                    "provider-unavailable",
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(
                    PreparedPass2RecoveryOutcome.PausedProviderUnavailable,
                    execution.Id,
                    execution.State,
                    execution.StateVersion,
                    recovered,
                    failed,
                    false);
            }

            bool completed = await CompleteIfTerminalAsync(
                execution, now, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(
                completed ? PreparedPass2RecoveryOutcome.Completed :
                    PreparedPass2RecoveryOutcome.Continued,
                execution.Id,
                execution.State,
                execution.StateVersion,
                recovered,
                failed,
                false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _dbContext.ChangeTracker.Clear();
            return Recovery(PreparedPass2RecoveryOutcome.Cancelled, pass2ExecutionId);
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException)
        {
            _dbContext.ChangeTracker.Clear();
            return Recovery(PreparedPass2RecoveryOutcome.Conflict, pass2ExecutionId);
        }
    }

    public async Task<PreparedPass2DispatchFailureOutcome> RecordDispatchFailureAsync(
        Guid pass2ExecutionId,
        Guid jobId,
        long expectedJobStateVersion,
        Guid leaseToken,
        ScanJobFailureCategory failureCategory,
        string failureCode,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        RequireUtc(now, nameof(now));
        try
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            Pass2Execution execution = await LockExecutionAsync(
                pass2ExecutionId, cancellationToken).ConfigureAwait(false) ??
                throw new InvalidOperationException("The Pass 2 execution is unavailable.");
            Pass2JobExecution job = (await LockJobsAsync(
                pass2ExecutionId, cancellationToken).ConfigureAwait(false))
                .Single(value => value.Id == jobId);
            if (job.StateVersion != expectedJobStateVersion || job.LeaseToken != leaseToken)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return PreparedPass2DispatchFailureOutcome.Conflict;
            }

            if (failureCategory == ScanJobFailureCategory.ProviderTransient)
            {
                TwoRepositoryScanCoordination coordination = await LockCoordinationAsync(
                    execution.CoordinationId, cancellationToken).ConfigureAwait(false) ??
                    throw new InvalidOperationException("The Pass 2 coordination is unavailable.");
                job.ReleaseForProviderPause(job.StateVersion, leaseToken, now);
                execution.PauseProviderUnavailable(execution.StateVersion, now);
                coordination.Pause(
                    coordination.StateVersion,
                    TwoRepositoryScanReasonCategory.ProviderUnavailable,
                    "provider-unavailable",
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return PreparedPass2DispatchFailureOutcome.PausedProviderUnavailable;
            }

            if (failureCategory is ScanJobFailureCategory.Integrity or
                ScanJobFailureCategory.Security)
            {
                TwoRepositoryScanCoordination coordination = await LockCoordinationAsync(
                    execution.CoordinationId, cancellationToken).ConfigureAwait(false) ??
                    throw new InvalidOperationException("The Pass 2 coordination is unavailable.");
                string terminalCode = NormalizeFailureCode(failureCode);
                job.Fail(
                    job.StateVersion,
                    leaseToken,
                    failureCategory,
                    terminalCode,
                    now);
                FlowGroup group = await _dbContext.FlowGroups.SingleAsync(
                    value => value.Id == job.FlowGroupId,
                    cancellationToken).ConfigureAwait(false);
                if (group.State == "Complete")
                {
                    group.Fail(terminalCode);
                }

                if (failureCategory == ScanJobFailureCategory.Integrity)
                {
                    execution.FailIntegrity(execution.StateVersion, now);
                    coordination.FailIntegrity(coordination.StateVersion, terminalCode, now);
                }
                else
                {
                    execution.FailSecurity(execution.StateVersion, now);
                    coordination.FailSecurity(coordination.StateVersion, terminalCode, now);
                }

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return PreparedPass2DispatchFailureOutcome.Failed;
            }

            if (job.AttemptCount < 2)
            {
                job.ScheduleRetry(
                    job.StateVersion,
                    leaseToken,
                    now.AddSeconds(1),
                    failureCategory,
                    NormalizeFailureCode(failureCode),
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return PreparedPass2DispatchFailureOutcome.RetryScheduled;
            }

            job.Fail(
                job.StateVersion,
                leaseToken,
                failureCategory,
                NormalizeFailureCode(failureCode),
                now);
            await MarkFailedAsync(job, execution, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _ = await CompleteIfTerminalAsync(execution, now, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return PreparedPass2DispatchFailureOutcome.Failed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PreparedPass2DispatchFailureOutcome.Cancelled;
        }
        catch (Exception exception) when (
            exception is DbUpdateException or InvalidOperationException)
        {
            _dbContext.ChangeTracker.Clear();
            return PreparedPass2DispatchFailureOutcome.Conflict;
        }
    }

    private async Task<Pass2RetrievalReservation> ReserveRetrievalOnceAsync(
        Guid pass2JobExecutionId,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(pass2JobExecutionId, Guid.Empty);
        _dbContext.ChangeTracker.Clear();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        Pass2RetrievalBudget? budget = await LockedBudgetQuery(pass2JobExecutionId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        Pass2JobExecution? job = await _dbContext.Pass2JobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pass2JobExecutionId, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (budget is null || job is null || job.State != Pass2JobExecutionState.Running ||
            job.AttemptCount is < 1 or > 2 || job.LeaseToken is null ||
            job.LeaseExpiresAt is null || job.LeaseExpiresAt <= now)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return new(false, 0, 0, string.Empty, string.Empty, 0, Guid.Empty,
                DateTimeOffset.MinValue, EmptyRemainingBudget());
        }

        int attemptOrdinal = Math.Max(1, job.AttemptCount);
        if (budget.CallsReserved >= Pass2RetrievalContracts.MaxCalls)
        {
            Pass2CodeLogicRetrievalRemainingBudget remaining = RemainingBudget(budget);
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(false, attemptOrdinal, 0, string.Empty, string.Empty,
                job.StateVersion, job.LeaseToken.Value, job.LeaseExpiresAt.Value, remaining);
        }

        int callOrdinal = budget.ReserveCall(DateTimeOffset.UtcNow);
        string requestFingerprint = FingerprintRequest(request);
        string toolCallIdFingerprint = LowerSha256(string.Join(
            '\n',
            "pass2-automatic-tool-call/1.0",
            pass2JobExecutionId.ToString("D"),
            attemptOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            callOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Pass2CodeLogicRetrievalRemainingBudget reservedRemaining = RemainingBudget(budget);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _dbContext.ChangeTracker.Clear();
        return new(
            true,
            attemptOrdinal,
            callOrdinal,
            toolCallIdFingerprint,
            requestFingerprint,
            job.StateVersion,
            job.LeaseToken.Value,
            job.LeaseExpiresAt.Value,
            reservedRemaining);
    }

    internal async Task<Pass2ExplicitRetrievalResolution> ResolveExplicitRetrievalAsync(
        Guid pass2JobExecutionId,
        Pass2RetrievalCallIdentity identity,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(request);
        _dbContext.ChangeTracker.Clear();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        Pass2RetrievalBudget? budget = await LockedBudgetQuery(pass2JobExecutionId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        Pass2JobExecution? job = await _dbContext.Pass2JobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pass2JobExecutionId, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (budget is null || job is null || job.State != Pass2JobExecutionState.Running ||
            job.LeaseToken is null || job.LeaseExpiresAt is null || job.LeaseExpiresAt <= now ||
            identity.AttemptOrdinal != job.AttemptCount)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(null, Pass2CodeLogicRetrievalResult.Refused());
        }

        string requestFingerprint = FingerprintRequest(request);
        Pass2RetrievalReceipt? receipt = await _dbContext.Pass2RetrievalReceipts.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Pass2JobExecutionId == pass2JobExecutionId &&
                    value.CallOrdinal == identity.CallOrdinal,
                cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            if (receipt.AttemptOrdinal != identity.AttemptOrdinal ||
                !StringComparer.Ordinal.Equals(
                    receipt.ToolCallIdFingerprint,
                    identity.ToolCallIdFingerprint) ||
                !StringComparer.Ordinal.Equals(receipt.RequestFingerprint, requestFingerprint))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                _dbContext.ChangeTracker.Clear();
                return new(null, Pass2CodeLogicRetrievalResult.Refused());
            }

            Pass2CodeLogicRetrievalResult replay = Rehydrate(receipt, budget);
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(null, replay, receipt);
        }

        if (identity.CallOrdinal <= budget.CallsReserved ||
            identity.CallOrdinal != budget.CallsReserved + 1 ||
            budget.CallsReserved >= Pass2RetrievalContracts.MaxCalls)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(null, Pass2CodeLogicRetrievalResult.Refused());
        }

        int callOrdinal = budget.ReserveCall(DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Pass2RetrievalReservation reservation = new(
            true,
            identity.AttemptOrdinal,
            callOrdinal,
            identity.ToolCallIdFingerprint,
            requestFingerprint,
            job.StateVersion,
            job.LeaseToken.Value,
            job.LeaseExpiresAt.Value,
            RemainingBudget(budget));
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _dbContext.ChangeTracker.Clear();
        return new(reservation, null);
    }

    internal async Task<Pass2CodeLogicRetrievalResult> AdmitRetrievalAsync(
        Guid pass2JobExecutionId,
        Pass2RetrievalReservation reservation,
        Pass2CodeLogicRetrievalRequest request,
        Pass2CodeLogicRetrievalResult candidate,
        IReadOnlyCollection<Guid> initialDocumentVersionIds,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await AdmitRetrievalOnceAsync(
                    pass2JobExecutionId,
                    reservation,
                    request,
                    candidate,
                    initialDocumentVersionIds,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (attempt < 3 && IsSerializationFailure(exception))
            {
                _dbContext.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Pass2CodeLogicRetrievalResult> AdmitRetrievalOnceAsync(
        Guid pass2JobExecutionId,
        Pass2RetrievalReservation reservation,
        Pass2CodeLogicRetrievalRequest request,
        Pass2CodeLogicRetrievalResult candidate,
        IReadOnlyCollection<Guid> initialDocumentVersionIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(initialDocumentVersionIds);
        if (!reservation.IsReserved || reservation.AttemptOrdinal < 1 || reservation.CallOrdinal < 1)
        {
            throw new ArgumentException("A durable retrieval reservation is required.", nameof(reservation));
        }

        _dbContext.ChangeTracker.Clear();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        Pass2RetrievalBudget budget = await LockedBudgetQuery(pass2JobExecutionId)
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        Pass2JobExecution? currentJob = await _dbContext.Pass2JobExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pass2JobExecutionId, cancellationToken)
            .ConfigureAwait(false);
        if (currentJob is null || currentJob.State != Pass2JobExecutionState.Running ||
            currentJob.AttemptCount != reservation.AttemptOrdinal ||
            currentJob.StateVersion != reservation.JobStateVersion ||
            currentJob.LeaseToken != reservation.LeaseToken ||
            currentJob.LeaseExpiresAt != reservation.LeaseExpiresAt ||
            currentJob.LeaseExpiresAt <= DateTimeOffset.UtcNow ||
            reservation.CallOrdinal > budget.CallsReserved ||
            await _dbContext.Pass2RetrievalReceipts.AsNoTracking().AnyAsync(
                value => value.Pass2JobExecutionId == pass2JobExecutionId &&
                    value.CallOrdinal == reservation.CallOrdinal,
                cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        if (reservation.AdmissionAuthorityCheck is not null &&
            !await reservation.AdmissionAuthorityCheck(cancellationToken).ConfigureAwait(false))
        {
            candidate = Pass2CodeLogicRetrievalResult.Refused();
        }

        Guid[] candidateDocumentIds = candidate.Items
            .Select(value => value.DocumentVersionId)
            .Distinct()
            .ToArray();
        int currentCandidateCount = await _dbContext.DocumentVersions.AsNoTracking()
            .Where(value => candidateDocumentIds.Contains(value.Id) &&
                value.SupersededById == null &&
                _dbContext.LogicalDocuments.Any(logical =>
                    logical.Id == value.LogicalDocumentId &&
                    logical.CurrentVersionId == value.Id &&
                    logical.RepositoryId == value.RepositoryId &&
                    logical.DocumentType == DocumentType.CodeLogic))
            .CountAsync(cancellationToken).ConfigureAwait(false);
        if (currentCandidateCount != candidateDocumentIds.Length)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return Pass2CodeLogicRetrievalResult.Refused();
        }

        Pass2CodeLogicRetrievalItem[] canonicalItems = candidate.Outcome ==
                Pass2CodeLogicRetrievalOutcome.Accepted
            ? candidate.Items
                .GroupBy(value => value.OpaqueHandle, StringComparer.Ordinal)
                .Select(grouping => grouping.First())
                .ToArray()
            : Array.Empty<Pass2CodeLogicRetrievalItem>();
        if (canonicalItems.Length != candidate.Items.Count)
        {
            candidate = Pass2CodeLogicRetrievalResult.Accepted(canonicalItems);
        }

        int resultCount = candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted
            ? canonicalItems.Length
            : 0;
        int returnedUtf8Bytes = candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted
            ? checked(canonicalItems.Sum(value => value.ReturnedUtf8Bytes))
            : 0;
        Pass2RetrievalResult[] admittedResults = await _dbContext.Pass2RetrievalResults
            .AsNoTracking()
            .Where(value => value.Pass2JobExecutionId == pass2JobExecutionId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        HashSet<Guid> admittedAdditionalDocuments = admittedResults
            .Select(value => value.DocumentVersionId)
            .Where(value => !initialDocumentVersionIds.Contains(value))
            .ToHashSet();
        int additionalDocuments = candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted
            ? canonicalItems.Select(value => value.DocumentVersionId)
                .Where(value => !initialDocumentVersionIds.Contains(value) &&
                    !admittedAdditionalDocuments.Contains(value))
                .Distinct()
                .Count()
            : 0;
        bool exceedsSectionBound = canonicalItems
            .Where(value => value.Kind == Pass2CodeLogicRetrievalItemKind.Section &&
                value.SectionId.HasValue)
            .GroupBy(value => value.DocumentVersionId)
            .Any(grouping => admittedResults
                .Where(value =>
                    value.Kind == Pass2CodeLogicRetrievalItemKind.Section &&
                    value.DocumentVersionId == grouping.Key)
                .Select(value => value.ReadSectionId())
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Concat(grouping.Select(value => value.SectionId!.Value))
                .Distinct()
                .Count() > Pass2RetrievalContracts.MaxSectionsPerDocument);
        bool exceedsBound = budget.ResultsReturned + resultCount > Pass2RetrievalContracts.MaxResults ||
            budget.ReturnedUtf8Bytes + returnedUtf8Bytes > Pass2RetrievalContracts.MaxReturnedUtf8Bytes ||
            budget.AdditionalDocumentCount + additionalDocuments >
                Pass2RetrievalContracts.MaxAdditionalDocuments ||
            exceedsSectionBound;
        Pass2RetrievalReceiptState receiptState = exceedsBound
            ? Pass2RetrievalReceiptState.BoundExceeded
            : ReceiptState(candidate.Outcome);
        Guid receiptId = DeriveId(
            "pass2-retrieval-receipt/1.0",
            pass2JobExecutionId,
            GuidFromOrdinal(reservation.AttemptOrdinal),
            GuidFromOrdinal(reservation.CallOrdinal));
        Pass2RetrievalReceipt receipt = Pass2RetrievalReceipt.Create(
            receiptId,
            pass2JobExecutionId,
            reservation.AttemptOrdinal,
            reservation.CallOrdinal,
            reservation.ToolCallIdFingerprint,
            receiptState,
            request.Operation,
            reservation.RequestFingerprint,
            exceedsBound ? 0 : resultCount,
            exceedsBound ? 0 : returnedUtf8Bytes,
            exceedsBound ? 0 : additionalDocuments,
            ResultSetFingerprint(exceedsBound ? Array.Empty<Pass2CodeLogicRetrievalItem>() : candidate.Items),
            DateTimeOffset.UtcNow);
        _dbContext.Pass2RetrievalReceipts.Add(receipt);

        if (!exceedsBound && candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted)
        {
            budget.Admit(
                resultCount,
                additionalDocuments,
                returnedUtf8Bytes,
                DateTimeOffset.UtcNow);
            Guid[] documentVersionIds = candidate.Items
                .Select(value => value.DocumentVersionId)
                .Distinct()
                .ToArray();
            Dictionary<Guid, DocumentVersion> documents = await _dbContext.DocumentVersions
                .AsNoTracking()
                .Where(value => documentVersionIds.Contains(value.Id))
                .ToDictionaryAsync(value => value.Id, cancellationToken).ConfigureAwait(false);
            DateTimeOffset createdAt = receipt.CreatedAt;
            for (int index = 0; index < candidate.Items.Count; index++)
            {
                Pass2CodeLogicRetrievalItem item = candidate.Items[index];
                DocumentVersion document = documents[item.DocumentVersionId];
                int resultOrdinal = index + 1;
                Guid resultId = Guid.ParseExact(item.OpaqueHandle, "D");
                Pass2RetrievalResult? existingResult = admittedResults.SingleOrDefault(
                    value => value.Id == resultId);
                if (existingResult is not null)
                {
                    if (!PersistedResultMatches(
                            existingResult,
                            item,
                            document,
                            pass2JobExecutionId,
                            reservation.AttemptOrdinal))
                    {
                        await transaction.RollbackAsync(CancellationToken.None)
                            .ConfigureAwait(false);
                        _dbContext.ChangeTracker.Clear();
                        return Pass2CodeLogicRetrievalResult.Refused();
                    }

                    continue;
                }

                _dbContext.Pass2RetrievalResults.Add(Pass2RetrievalResult.CreateAdmitted(
                    resultId,
                    receiptId,
                    pass2JobExecutionId,
                    reservation.AttemptOrdinal,
                    resultOrdinal,
                    item.Kind,
                    item.DocumentVersionId,
                    document.RepositoryId,
                    document.ScanRunId,
                    item.SectionId,
                    item.SourceAnchorId,
                    item.EdgeId,
                    item.ByteStart,
                    item.ByteEnd,
                    FingerprintDescriptor(item),
                    FingerprintResult(item),
                    item.ReturnedUtf8Bytes,
                    createdAt));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Pass2CodeLogicRetrievalRemainingBudget remaining = RemainingBudget(budget);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _dbContext.ChangeTracker.Clear();
        return exceedsBound
            ? Pass2CodeLogicRetrievalResult.BoundExceeded(remaining)
            : candidate.WithRemainingBudget(remaining);
    }

    public async Task<PreparedPass2StartResult> StartAsync(
        Guid pass1PairExecutionId,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default) =>
        await StartAsync(
            pass1PairExecutionId,
            "server-authorized",
            scope,
            cancellationToken).ConfigureAwait(false);

    public async Task<PreparedPass2StartResult> StartAsync(
        Guid pass1PairExecutionId,
        string authorizationSubject,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(pass1PairExecutionId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationSubject);
        string normalizedAuthorizationSubject = authorizationSubject.Trim();
        if (normalizedAuthorizationSubject.Length > 450)
        {
            return Result(PreparedPass2StartOutcome.Rejected);
        }
        ArgumentNullException.ThrowIfNull(scope);
        if (cancellationToken.IsCancellationRequested)
        {
            return Result(PreparedPass2StartOutcome.Cancelled);
        }

        if (_workerContractAuthority is null)
        {
            return Result(PreparedPass2StartOutcome.Rejected);
        }

        PreparedPass2WorkerContractAuthority workerContract;
        try
        {
            workerContract = _workerContractAuthority.GetCurrent();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return Result(PreparedPass2StartOutcome.Rejected);
        }

        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken).ConfigureAwait(false);

            Pass1PairExecution? pair = await _dbContext.Pass1PairExecutions.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == pass1PairExecutionId, cancellationToken)
                .ConfigureAwait(false);
            if (pair is null)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Result(PreparedPass2StartOutcome.NotFound);
            }

            PreparedScanManifest[] manifests = await _dbContext.PreparedScanManifests.AsNoTracking()
                .Where(value => value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            Guid[] repositoryIds = manifests.Select(value => value.RepositoryId).Distinct().ToArray();
            if (!IsAuthorizedTerminalPair(pair, manifests, repositoryIds, scope) ||
                !await scope.HasCurrentAccessAsync(repositoryIds, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Result(PreparedPass2StartOutcome.Rejected);
            }

            Pass2Execution? existing = await _dbContext.Pass2Executions.AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.Pass1PairExecutionId == pass1PairExecutionId,
                    cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                PreparedPass2StartResult replay = await ReplayAsync(
                    existing,
                    pair,
                    normalizedAuthorizationSubject,
                    workerContract,
                    cancellationToken).ConfigureAwait(false);
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return replay;
            }

            DateTimeOffset startedAt = pair.TerminalAt ?? pair.UpdatedAt;
            Guid executionId = DeriveId("pass2-execution/1.0", pair.Id);
            Pass2Execution execution = Pass2Execution.CreatePlanning(
                executionId,
                pair.Id,
                pair.CoordinationId,
                pair.SystemId,
                startedAt,
                normalizedAuthorizationSubject,
                workerContract.PromptIdentity,
                workerContract.SubmissionSchemaIdentity,
                workerContract.ToolDeclarationFingerprint,
                workerContract.PromptFingerprint,
                workerContract.SubmissionSchemaFingerprint);
            _dbContext.Pass2Executions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            PreparedPass2FlowPlanningResult plan = await new PreparedPass2FlowPlanner(_dbContext)
                .PlanAsync(execution.Id, scope, cancellationToken).ConfigureAwait(false);
            FlowGroup[] groups = plan.FlowGroups.Take(_maxFlowGroups).ToArray();
            Pass2Omission[] boundOmissions = plan.FlowGroups.Skip(_maxFlowGroups)
                .Select(group => Pass2Omission.CreateBoundExcluded(
                    DeriveId(
                        "pass2-omission/1.0",
                        execution.Id,
                        group.ExactResolutionDecisionId ?? throw new InvalidOperationException(
                            "A bound-excluded flow candidate must retain its exact decision identity.")),
                    execution.Id,
                    group.ExactResolutionDecisionId!.Value,
                    startedAt))
                .ToArray();
            Pass2Omission[] omissions = plan.Omissions.Concat(boundOmissions).ToArray();
            execution.RecordPlan(checked(groups.Length + omissions.Length));
            CrossRepositoryResolutionDecision[] authorityDecisions = await _dbContext
                .CrossRepositoryResolutionDecisions.AsNoTracking()
                .Where(value =>
                    value.Pass1PairExecutionId == pair.Id &&
                    value.CoordinationId == pair.CoordinationId &&
                    value.SystemId == pair.SystemId)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            Pass2AuthoritySnapshot authoritySnapshot = CreateAuthoritySnapshot(
                authorityDecisions, groups, omissions, execution);
            execution.RecordAuthority(
                authoritySnapshot.CandidateCount,
                authoritySnapshot.ResolutionFingerprint,
                authoritySnapshot.PlanningFingerprint,
                authoritySnapshot.ExecutionFingerprint);
            _dbContext.FlowGroups.AddRange(groups);
            _dbContext.Pass2Omissions.AddRange(omissions);

            var jobs = new List<Pass2JobExecution>();
            var budgets = new List<Pass2RetrievalBudget>();
            foreach (FlowGroup group in groups.Where(value => value.State == "Complete"))
            {
                Guid jobId = DeriveId("pass2-job/1.0", execution.Id, group.Id);
                Pass2JobExecution job = Pass2JobExecution.CreatePending(
                    jobId,
                    execution.Id,
                    group.Id,
                    group.GroupOrder);
                jobs.Add(job);
                budgets.Add(Pass2RetrievalBudget.Create(job.Id, startedAt));
            }

            _dbContext.Pass2JobExecutions.AddRange(jobs);
            _dbContext.Pass2RetrievalBudgets.AddRange(budgets);
            TwoRepositoryScanCoordination coordination = await LockCoordinationAsync(
                pair.CoordinationId, cancellationToken).ConfigureAwait(false) ??
                throw new InvalidOperationException("The Pass 2 coordination is unavailable.");
            if (coordination.Phase == TwoRepositoryScanCoordinationPhase.Resolving)
            {
                DateTimeOffset transitionAt = startedAt < coordination.UpdatedAt
                    ? coordination.UpdatedAt
                    : startedAt;
                coordination.BeginPass2(coordination.StateVersion, transitionAt);
            }
            else if (coordination.Phase != TwoRepositoryScanCoordinationPhase.Pass2)
            {
                throw new InvalidOperationException("The coordination is not eligible for Pass 2.");
            }

            execution.MarkRunning(startedAt);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(
                PreparedPass2StartOutcome.Created,
                execution.Id,
                groups.Length,
                omissions.Length,
                jobs.Count,
                budgets.Count,
                false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(PreparedPass2StartOutcome.Cancelled);
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(PreparedPass2StartOutcome.Conflict);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(PreparedPass2StartOutcome.Failed);
        }
    }

    private static bool IsAuthorizedTerminalPair(
        Pass1PairExecution pair,
        IReadOnlyCollection<PreparedScanManifest> manifests,
        IReadOnlyCollection<Guid> repositoryIds,
        RepositoryAccessScope scope) =>
        pair.Phase == Pass1PairExecutionPhase.ReadyForResolution &&
        pair.TerminalAt.HasValue &&
        manifests.Count == 2 &&
        repositoryIds.Count == 2 &&
        manifests.All(value =>
            value.ApprovalState == PreparedScanManifestApprovalState.Approved &&
            value.CoordinationId == pair.CoordinationId &&
            (value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId) &&
            scope.Contains(value.RepositoryId));

    private async Task<PreparedPass2StartResult> ReplayAsync(
        Pass2Execution execution,
        Pass1PairExecution pair,
        string authorizationSubject,
        PreparedPass2WorkerContractAuthority workerContract,
        CancellationToken cancellationToken)
    {
        if (execution.Id != DeriveId("pass2-execution/1.0", pair.Id) ||
            execution.CoordinationId != pair.CoordinationId ||
            execution.SystemId != pair.SystemId ||
            !StringComparer.Ordinal.Equals(
                execution.AuthorizationSubject,
                authorizationSubject) ||
            !StringComparer.Ordinal.Equals(
                execution.WorkerPromptIdentity,
                workerContract.PromptIdentity) ||
            !StringComparer.Ordinal.Equals(
                execution.WorkerPromptFingerprint,
                workerContract.PromptFingerprint) ||
            !StringComparer.Ordinal.Equals(
                execution.SubmissionSchemaIdentity,
                workerContract.SubmissionSchemaIdentity) ||
            !StringComparer.Ordinal.Equals(
                execution.SubmissionSchemaFingerprint,
                workerContract.SubmissionSchemaFingerprint) ||
            !StringComparer.Ordinal.Equals(
                execution.ToolDeclarationFingerprint,
                workerContract.ToolDeclarationFingerprint))
        {
            return Result(PreparedPass2StartOutcome.Conflict, execution.Id);
        }

        FlowGroup[] groups = await _dbContext.FlowGroups.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Pass2Omission[] omissions = await _dbContext.Pass2Omissions.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Pass2JobExecution[] jobs = await _dbContext.Pass2JobExecutions.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] jobIds = jobs.Select(value => value.Id).ToArray();
        int budgets = await _dbContext.Pass2RetrievalBudgets.AsNoTracking()
            .CountAsync(value => jobIds.Contains(value.Pass2JobExecutionId), cancellationToken)
            .ConfigureAwait(false);
        CrossRepositoryResolutionDecision[] decisions = await _dbContext
            .CrossRepositoryResolutionDecisions.AsNoTracking()
            .Where(value =>
                value.Pass1PairExecutionId == pair.Id &&
                value.CoordinationId == pair.CoordinationId &&
                value.SystemId == pair.SystemId &&
                value.Outcome == "ExactMatch")
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] orderedDecisionIds = decisions
            .OrderBy(value => value.Protocol)
            .ThenBy(value => value.NormalizedEndpointIdentifier, StringComparer.Ordinal)
            .ThenBy(value => value.ConsumerEntryStableKey, StringComparer.Ordinal)
            .ThenBy(value => value.Id.ToString("N"), StringComparer.Ordinal)
            .Select(value => value.Id)
            .ToArray();
        Guid[] expectedGroupDecisionIds = orderedDecisionIds.Take(_maxFlowGroups).ToArray();
        Guid[] actualGroupDecisionIds = groups.OrderBy(value => value.GroupOrder)
            .Select(value => value.ExactResolutionDecisionId ?? Guid.Empty)
            .ToArray();
        Guid[] expectedOmissionDecisionIds = orderedDecisionIds.Skip(_maxFlowGroups).ToArray();
        Guid[] actualOmissionDecisionIds = omissions
            .OrderBy(value => Array.IndexOf(expectedOmissionDecisionIds, value.ExactResolutionDecisionId))
            .Select(value => value.ExactResolutionDecisionId)
            .ToArray();
        FlowGroup[] dispatchableGroups = groups.Where(value =>
            value.State is "Complete" or "Running" or "Published" or "Failed").ToArray();
        Guid[] budgetJobIds = await _dbContext.Pass2RetrievalBudgets.AsNoTracking()
            .Where(value => jobIds.Contains(value.Pass2JobExecutionId))
            .Select(value => value.Pass2JobExecutionId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        CrossRepositoryResolutionDecision[] allDecisions = await _dbContext
            .CrossRepositoryResolutionDecisions.AsNoTracking()
            .Where(value =>
                value.Pass1PairExecutionId == pair.Id &&
                value.CoordinationId == pair.CoordinationId &&
                value.SystemId == pair.SystemId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Pass2AuthoritySnapshot authoritySnapshot = CreateAuthoritySnapshot(
            allDecisions, groups, omissions, execution);
        if (groups.Length == 0 ||
            execution.CandidateCount != authoritySnapshot.CandidateCount ||
            !StringComparer.Ordinal.Equals(
                execution.ResolutionFingerprint, authoritySnapshot.ResolutionFingerprint) ||
            !StringComparer.Ordinal.Equals(
                execution.PlanningFingerprint, authoritySnapshot.PlanningFingerprint) ||
            !StringComparer.Ordinal.Equals(
                execution.ExecutionFingerprint, authoritySnapshot.ExecutionFingerprint) ||
            !actualGroupDecisionIds.SequenceEqual(expectedGroupDecisionIds) ||
            !actualOmissionDecisionIds.SequenceEqual(expectedOmissionDecisionIds) ||
            omissions.Any(value =>
                value.State != Pass2OmissionState.Omitted ||
                value.ReasonCode != "flow-group-bound" ||
                value.Id != DeriveId(
                    "pass2-omission/1.0",
                    execution.Id,
                    value.ExactResolutionDecisionId)) ||
            jobs.Length != dispatchableGroups.Length || budgets != jobs.Length ||
            budgetJobIds.Distinct().Count() != jobs.Length ||
            jobs.Any(job =>
                job.Id != DeriveId("pass2-job/1.0", execution.Id, job.FlowGroupId) ||
                job.Pass2ExecutionId != execution.Id ||
                dispatchableGroups.All(group =>
                    group.Id != job.FlowGroupId || group.GroupOrder != job.GroupOrder)) ||
            dispatchableGroups.Any(group => jobs.Count(job => job.FlowGroupId == group.Id) != 1))
        {
            return Result(PreparedPass2StartOutcome.Conflict, execution.Id);
        }

        return new(
            PreparedPass2StartOutcome.Replay,
            execution.Id,
            groups.Length,
            omissions.Length,
            jobs.Length,
            budgets,
            true);
    }

    private static Pass2AuthoritySnapshot CreateAuthoritySnapshot(
        IEnumerable<CrossRepositoryResolutionDecision> decisions,
        IEnumerable<FlowGroup> groups,
        IEnumerable<Pass2Omission> omissions,
        Pass2Execution execution)
    {
        string[] decisionFingerprints = decisions
            .OrderBy(value => value.Protocol)
            .ThenBy(value => value.NormalizedEndpointIdentifier, StringComparer.Ordinal)
            .ThenBy(value => value.ConsumerEntryStableKey, StringComparer.Ordinal)
            .ThenBy(value => value.Id.ToString("N"), StringComparer.Ordinal)
            .Select(value => value.DecisionFingerprint.Length == 64
                ? value.DecisionFingerprint
                : value.EvidenceFingerprint)
            .ToArray();
        string resolutionFingerprint = HashAuthority(
            "pass2-resolution-authority/1.0", decisionFingerprints);
        string[] planningParts = groups
            .OrderBy(value => value.GroupOrder)
            .Select(value => $"group:{value.Id:D}:{value.PlanFingerprint}:{value.MembershipFingerprint}")
            .Concat(omissions
                .OrderBy(value => value.ExactResolutionDecisionId)
                .Select(value => $"omission:{value.ExactResolutionDecisionId:D}:{value.ReasonCode}"))
            .ToArray();
        string planningFingerprint = HashAuthority(
            "pass2-planning-authority/1.0", planningParts);
        string executionFingerprint = HashAuthority(
            "pass2-execution-authority/1.0",
            [
                resolutionFingerprint,
                planningFingerprint,
                execution.WorkerPromptIdentity,
                execution.WorkerPromptFingerprint,
                execution.SubmissionSchemaIdentity,
                execution.SubmissionSchemaFingerprint,
                execution.ToolDeclarationFingerprint,
            ]);
        return new(
            decisionFingerprints.Length,
            resolutionFingerprint,
            planningFingerprint,
            executionFingerprint);
    }

    private static string HashAuthority(string identity, IEnumerable<string> values)
    {
        string preimage = string.Join('\n', values.Prepend(identity));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(preimage)));
    }

    private sealed record Pass2AuthoritySnapshot(
        int CandidateCount,
        string ResolutionFingerprint,
        string PlanningFingerprint,
        string ExecutionFingerprint);

    private async Task<int> RecoverJobsAsync(
        IReadOnlyList<Pass2JobExecution> jobs,
        Pass2Execution execution,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        int failed = 0;
        foreach (Pass2JobExecution job in jobs.OrderBy(value => value.GroupOrder))
        {
            if (job.State == Pass2JobExecutionState.Running &&
                job.LeaseExpiresAt is { } leaseExpiresAt && leaseExpiresAt <= now)
            {
                job.RecoverExpiredLease(job.StateVersion, now);
                if (job.State == Pass2JobExecutionState.Failed)
                {
                    await MarkFailedAsync(job, execution, cancellationToken).ConfigureAwait(false);
                    failed++;
                }
            }
            else if (job.State == Pass2JobExecutionState.RetryScheduled &&
                job.NextAttemptAt is { } nextAttemptAt && nextAttemptAt <= now)
            {
                job.RecoverDueRetry(job.StateVersion, now);
            }
        }

        return failed;
    }

    private async Task MarkFailedAsync(
        Pass2JobExecution job,
        Pass2Execution execution,
        CancellationToken cancellationToken)
    {
        FlowGroup group = await _dbContext.FlowGroups.SingleAsync(
            value => value.Id == job.FlowGroupId,
            cancellationToken).ConfigureAwait(false);
        if (group.State == "Complete")
        {
            group.Fail(job.FailureCode ?? "worker-failed");
            execution.RecordFailed(execution.StateVersion, job.StateChangedAt);
        }
    }

    internal async Task<bool> CompleteIfTerminalAsync(
        Pass2Execution execution,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (execution.State is Pass2ExecutionState.Completed or
            Pass2ExecutionState.CompletedWithWarnings)
        {
            return true;
        }
        if (execution.State != Pass2ExecutionState.Running)
        {
            return false;
        }

        Pass2JobExecutionState[] jobStates = await _dbContext.Pass2JobExecutions
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .Select(value => value.State)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (jobStates.Any(value => value is Pass2JobExecutionState.Pending or
            Pass2JobExecutionState.Running or Pass2JobExecutionState.RetryScheduled))
        {
            return false;
        }

        var groupRows = await _dbContext.FlowGroups
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .Select(value => new
            {
                value.Id,
                value.State,
                value.PlanFingerprint,
                value.ReasonCode,
            })
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (groupRows.Any(value => value.State == "Complete" || value.State == "Running"))
        {
            return false;
        }

        var omissionRows = await _dbContext.Pass2Omissions
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .Join(
                _dbContext.CrossRepositoryResolutionDecisions,
                omission => omission.ExactResolutionDecisionId,
                decision => decision.Id,
                (omission, decision) => new
                {
                    omission.ExactResolutionDecisionId,
                    decision.EvidenceFingerprint,
                    omission.ReasonCode,
                })
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        int incompleteCount = groupRows.Count(value => value.State == "Incomplete");
        int boundExcludedCount = groupRows.Count(value => value.State == "BoundExcluded");
        int omissionCount = omissionRows.Length;
        bool withWarnings = execution.PublishedCount == 0 || execution.FailedCount > 0 ||
            omissionCount > 0 || incompleteCount > 0 || boundExcludedCount > 0;
        string terminalWarningInventory = string.Join('\n', groupRows
            .Where(value => value.State is "Incomplete" or "BoundExcluded" or "Failed")
            .Select(value => $"{value.Id:D}|{value.PlanFingerprint}|{value.ReasonCode ?? "worker-failed"}")
            .Concat(omissionRows.Select(value =>
                $"{value.ExactResolutionDecisionId:D}|{value.EvidenceFingerprint}|{value.ReasonCode}"))
            .OrderBy(value => value, StringComparer.Ordinal));
        TwoRepositoryScanCoordination coordination = await LockCoordinationAsync(
            execution.CoordinationId, cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException("The Pass 2 coordination is unavailable.");
        execution.Complete(
            execution.StateVersion,
            withWarnings,
            incompleteCount,
            boundExcludedCount,
            omissionCount,
            terminalWarningInventory,
            now);
        TwoRepositoryScanCoordinationPhase expectedCoordinationPhase = withWarnings
            ? TwoRepositoryScanCoordinationPhase.CompletedWithWarnings
            : TwoRepositoryScanCoordinationPhase.Completed;
        if (coordination.Phase == TwoRepositoryScanCoordinationPhase.Pass2)
        {
            coordination.CompletePass2(coordination.StateVersion, withWarnings, now);
        }
        else if (coordination.Phase != expectedCoordinationPhase)
        {
            throw new InvalidOperationException(
                "The Pass 2 coordination terminal outcome does not match execution accounting.");
        }
        return true;
    }

    private async Task<Pass2Execution?> LockExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        if (_dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            return await _dbContext.Pass2Executions.FromSqlInterpolated(
                    $"SELECT * FROM vulgata.\"Pass2Executions\" WHERE \"Id\" = {executionId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        return await _dbContext.Pass2Executions.SingleOrDefaultAsync(
            value => value.Id == executionId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Pass2JobExecution[]> LockJobsAsync(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        if (_dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            return await _dbContext.Pass2JobExecutions.FromSqlInterpolated(
                    $"SELECT * FROM vulgata.\"Pass2JobExecutions\" WHERE \"Pass2ExecutionId\" = {executionId} ORDER BY \"GroupOrder\" FOR UPDATE")
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        return await _dbContext.Pass2JobExecutions
            .Where(value => value.Pass2ExecutionId == executionId)
            .OrderBy(value => value.GroupOrder)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<TwoRepositoryScanCoordination?> LockCoordinationAsync(
        Guid coordinationId,
        CancellationToken cancellationToken)
    {
        if (_dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            return await _dbContext.TwoRepositoryScanCoordinations.FromSqlInterpolated(
                    $"SELECT * FROM vulgata.\"TwoRepositoryScanCoordinations\" WHERE \"Id\" = {coordinationId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        return await _dbContext.TwoRepositoryScanCoordinations.SingleOrDefaultAsync(
            value => value.Id == coordinationId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Guid[]> ExecutionRepositoryIdsAsync(
        Pass2Execution execution,
        CancellationToken cancellationToken)
    {
        TwoRepositoryScanCoordination? coordination = await _dbContext
            .TwoRepositoryScanCoordinations.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == execution.CoordinationId, cancellationToken)
            .ConfigureAwait(false);
        return coordination is null
            ? []
            : [coordination.RepositoryOneId, coordination.RepositoryTwoId];
    }

    private static string NormalizeFailureCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().ToLowerInvariant();
        return normalized.Length <= 128 ? normalized : normalized[..128];
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use the UTC offset.", parameterName);
        }
    }

    private static PreparedPass2ClaimResult Claim(
        PreparedPass2ClaimOutcome outcome,
        Guid executionId) =>
        new(outcome, executionId, null, null, 0, 0, 0, null, null, null);

    private static PreparedPass2RecoveryResult Recovery(
        PreparedPass2RecoveryOutcome outcome,
        Guid executionId) =>
        new(outcome, executionId == Guid.Empty ? null : executionId, null, null, 0, 0, false);

    private static Guid DeriveId(string schema, params Guid[] identities)
    {
        string canonical = schema + "\n" + string.Join('\n', identities.Select(value => value.ToString("D")));
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private IQueryable<Pass2RetrievalBudget> LockedBudgetQuery(Guid pass2JobExecutionId)
    {
        if (_dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            return _dbContext.Pass2RetrievalBudgets.FromSqlInterpolated(
                $"SELECT * FROM vulgata.\"Pass2RetrievalBudgets\" WHERE \"Pass2JobExecutionId\" = {pass2JobExecutionId} FOR UPDATE");
        }

        return _dbContext.Pass2RetrievalBudgets
            .Where(value => value.Pass2JobExecutionId == pass2JobExecutionId);
    }

    private static Pass2RetrievalReceiptState ReceiptState(
        Pass2CodeLogicRetrievalOutcome outcome) => outcome switch
        {
            Pass2CodeLogicRetrievalOutcome.Accepted => Pass2RetrievalReceiptState.Accepted,
            Pass2CodeLogicRetrievalOutcome.NoResults => Pass2RetrievalReceiptState.NoResults,
            Pass2CodeLogicRetrievalOutcome.BoundExceeded => Pass2RetrievalReceiptState.BoundExceeded,
            Pass2CodeLogicRetrievalOutcome.Refused => Pass2RetrievalReceiptState.Refused,
            _ => Pass2RetrievalReceiptState.Unavailable,
        };

    private static Pass2CodeLogicRetrievalRemainingBudget RemainingBudget(
        Pass2RetrievalBudget budget) => new(
            Math.Max(0, Pass2RetrievalContracts.MaxCalls - budget.CallsReserved),
            Math.Max(0, Pass2RetrievalContracts.MaxResults - budget.ResultsReturned),
            Math.Max(
                0,
                Pass2RetrievalContracts.MaxAdditionalDocuments - budget.AdditionalDocumentCount),
            Pass2RetrievalContracts.MaxSectionsPerDocument,
            Math.Max(
                0,
                Pass2RetrievalContracts.MaxReturnedUtf8Bytes - budget.ReturnedUtf8Bytes));

    private static Pass2CodeLogicRetrievalRemainingBudget EmptyRemainingBudget() =>
        new(0, 0, 0, 0, 0);

    private static string FingerprintRequest(Pass2CodeLogicRetrievalRequest request) =>
        LowerSha256(string.Join(
            '\n',
            "pass2-retrieval-request/1.0",
            request.Operation.ToString(),
            request.QueryText ?? string.Empty,
            string.Join('\n', request.OpaqueHandles),
            request.Direction?.ToString() ?? string.Empty,
            request.ContainedByteOffset?.ToString(
                System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            request.ContainedByteLength?.ToString(
                System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));

    private static string FingerprintResult(Pass2CodeLogicRetrievalItem item) =>
        LowerSha256(string.Join(
            '\n',
            "pass2-retrieval-result/1.0",
            item.OpaqueHandle,
            item.Kind.ToString(),
            item.DocumentVersionId.ToString("D"),
            item.SectionId?.ToString("D") ?? string.Empty,
            item.SourceAnchorId?.ToString("D") ?? string.Empty,
            item.EdgeId?.ToString("D") ?? string.Empty,
            item.ByteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            item.ByteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            item.ReturnedUtf8Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static string FingerprintDescriptor(Pass2CodeLogicRetrievalItem item) =>
        LowerSha256(string.Join(
            '\n',
            "pass2-retrieval-descriptor/1.0",
            item.Kind.ToString(),
            item.DocumentVersionId.ToString("D"),
            item.SectionId?.ToString("D") ?? string.Empty,
            item.SourceAnchorId?.ToString("D") ?? string.Empty,
            item.EdgeId?.ToString("D") ?? string.Empty,
            item.ByteStart?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            item.ByteEnd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));

    private static string ResultSetFingerprint(
        IReadOnlyCollection<Pass2CodeLogicRetrievalItem> items) =>
        LowerSha256(string.Join(
            '\n',
            new[] { "pass2-retrieval-result-set/1.0" }
                .Concat(items.Select(FingerprintResult))));

    internal static bool MatchesCompletedReplay(
        Pass2RetrievalReceipt receipt,
        Pass2CodeLogicRetrievalResult candidate) =>
        receipt.State == Pass2RetrievalReceiptState.Accepted &&
        candidate.Outcome == Pass2CodeLogicRetrievalOutcome.Accepted &&
        candidate.Items.Count == receipt.ResultCount &&
        candidate.Items.Sum(value => value.ReturnedUtf8Bytes) == receipt.ReturnedUtf8Bytes &&
        StringComparer.Ordinal.Equals(
            ResultSetFingerprint(candidate.Items),
            receipt.ResultSetFingerprint);

    private static bool PersistedResultMatches(
        Pass2RetrievalResult persisted,
        Pass2CodeLogicRetrievalItem item,
        DocumentVersion document,
        Guid pass2JobExecutionId,
        int attemptOrdinal) =>
        persisted.Pass2JobExecutionId == pass2JobExecutionId &&
        persisted.AttemptOrdinal == attemptOrdinal &&
        persisted.State == Pass2RetrievalResultState.Admitted &&
        persisted.Kind == item.Kind &&
        persisted.DocumentVersionId == item.DocumentVersionId &&
        persisted.RepositoryId == document.RepositoryId &&
        persisted.ScanRunId == document.ScanRunId &&
        persisted.ReadSectionId() == item.SectionId &&
        persisted.ReadSourceAnchorId() == item.SourceAnchorId &&
        persisted.EdgeId == item.EdgeId &&
        persisted.ByteStart == item.ByteStart &&
        persisted.ByteEnd == item.ByteEnd &&
        StringComparer.Ordinal.Equals(
            persisted.DescriptorFingerprint,
            FingerprintDescriptor(item)) &&
        StringComparer.Ordinal.Equals(
            persisted.EvidenceFingerprint,
            FingerprintResult(item)) &&
        persisted.ReturnedUtf8Bytes == item.ReturnedUtf8Bytes;

    private static Pass2CodeLogicRetrievalResult Rehydrate(
        Pass2RetrievalReceipt receipt,
        Pass2RetrievalBudget budget)
    {
        Pass2CodeLogicRetrievalRemainingBudget remaining = RemainingBudget(budget);
        if (receipt.State == Pass2RetrievalReceiptState.NoResults)
        {
            return Pass2CodeLogicRetrievalResult.NoResults(remaining);
        }

        if (receipt.State == Pass2RetrievalReceiptState.BoundExceeded)
        {
            return Pass2CodeLogicRetrievalResult.BoundExceeded(remaining);
        }

        if (receipt.State == Pass2RetrievalReceiptState.Refused)
        {
            return Pass2CodeLogicRetrievalResult.Refused().WithRemainingBudget(remaining);
        }

        if (receipt.State == Pass2RetrievalReceiptState.Unavailable)
        {
            return Pass2CodeLogicRetrievalResult.Unavailable().WithRemainingBudget(remaining);
        }

        return Pass2CodeLogicRetrievalResult.Accepted(
            Array.Empty<Pass2CodeLogicRetrievalItem>(),
            remaining);
    }

    private static string LowerSha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsSerializationFailure(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } ||
        exception.InnerException is not null && IsSerializationFailure(exception.InnerException);

    private static Guid GuidFromOrdinal(int ordinal)
    {
        Span<byte> bytes = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes[12..], ordinal);
        return new Guid(bytes, bigEndian: true);
    }

    private static PreparedPass2StartResult Result(
        PreparedPass2StartOutcome outcome,
        Guid? executionId = null) =>
        new(outcome, executionId, 0, 0, 0, 0, false);
}

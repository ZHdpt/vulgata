using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class DurableScanRepository(VulgataDbContext dbContext) :
    IDurableScanStore,
    ITwoRepositoryScanCoordinationStore
{
    public async Task<DurableScanStoreResult> CreateContextSnapshotAsync(
        DurableScanAuthorizationScope authorizationScope,
        ScanContextSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!await IsRepositoryAuthorizedAsync(
                authorizationScope,
                snapshot.RepositoryId,
                cancellationToken))
        {
            return Rejected(DurableScanReason.Unauthorized);
        }

        ScanContextSnapshot? existing = await dbContext.ScanContextSnapshots
            .SingleOrDefaultAsync(
                value => value.ScanRunId == snapshot.ScanRunId &&
                    value.RepositoryId == snapshot.RepositoryId,
                cancellationToken);
        if (existing is not null)
        {
            return SnapshotIdentityMatches(existing, snapshot)
                ? Replay()
                : Conflict(DurableScanReason.InvalidIdentity);
        }

        return await AddAtomicallyAsync(snapshot, cancellationToken);
    }

    public async Task<DurableScanStoreResult> CreateScanExecutionAsync(
        DurableScanAuthorizationScope authorizationScope,
        ScanExecution execution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);
        ArgumentNullException.ThrowIfNull(execution);
        if (!IsScopeValid(authorizationScope) ||
            !authorizationScope.OwnedSystemIds.Contains(execution.SystemId))
        {
            return Rejected(DurableScanReason.Unauthorized);
        }

        if (!await RepositoryMatchesSystemAsync(
                execution.RepositoryId,
                execution.SystemId,
                cancellationToken))
        {
            return Rejected(DurableScanReason.InvalidIdentity);
        }

        if (execution.PromptIdentity is null ||
            execution.DocumentSchemaIdentity is null ||
            execution.AnalysisRuntimeIdentityHash is null)
        {
            return Rejected(DurableScanReason.InvalidIdentity);
        }

        ScanExecution? existing = await dbContext.ScanExecutions
            .SingleOrDefaultAsync(value => value.Id == execution.Id, cancellationToken);
        if (existing is not null)
        {
            return ExecutionIdentityMatches(existing, execution)
                ? Replay(existing.StateVersion)
                : Conflict(DurableScanReason.InvalidIdentity, existing.StateVersion);
        }

        return await AddAtomicallyAsync(execution, cancellationToken);
    }

    public async Task<DurableScanStoreResult> InsertJobExecutionsAsync(
        DurableScanAuthorizationScope authorizationScope,
        Guid scanExecutionId,
        IReadOnlyList<ScanJobExecution> jobs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        if (!IsScopeValid(authorizationScope) || scanExecutionId == Guid.Empty || jobs.Count == 0)
        {
            return Rejected(DurableScanReason.InvalidIdentity);
        }

        ScanExecution? execution = await dbContext.ScanExecutions
            .SingleOrDefaultAsync(value => value.Id == scanExecutionId, cancellationToken);
        if (execution is null)
        {
            return NotFound();
        }

        Guid repositoryId = execution.RepositoryId;
        Guid scanRunId = execution.ScanRunId;
        if (!authorizationScope.OwnedSystemIds.Contains(execution.SystemId))
        {
            return Rejected(DurableScanReason.Unauthorized);
        }

        if (jobs.Any(job =>
                job.ScanExecutionId != scanExecutionId ||
                job.RepositoryId != repositoryId ||
                job.ScanRunId != scanRunId) ||
            jobs.Select(job => job.Id).Distinct().Count() != jobs.Count ||
            jobs.Select(job => job.FileCodeUnitId).Distinct().Count() != jobs.Count ||
            jobs.Select(job => job.ManifestOrdinal).Distinct().Count() != jobs.Count)
        {
            return Rejected(DurableScanReason.InvalidIdentity);
        }

        List<ScanJobExecution> existing = await dbContext.ScanJobExecutions
            .Where(value => value.ScanExecutionId == scanExecutionId)
            .ToListAsync(cancellationToken);
        if (existing.Count != 0)
        {
            bool replay = jobs.All(requested => existing.Any(durable =>
                JobIdentityAndStateMatch(durable, requested)));
            return replay ? Replay() : Conflict(DurableScanReason.InvalidIdentity);
        }

        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.ScanJobExecutions.AddRangeAsync(jobs, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Applied();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            DetachAdded(jobs);
            return Conflict(DurableScanReason.IntegrityViolation);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            DetachAdded(jobs);
            throw;
        }
    }

    public async Task<ScanRecoverySnapshotResult> ReadRecoverySnapshotAsync(
        DurableScanAuthorizationScope authorizationScope,
        Guid scanExecutionId,
        CancellationToken cancellationToken)
    {
        ScanExecution? execution = await FindAuthorizedExecutionAsync(
            authorizationScope,
            scanExecutionId,
            cancellationToken);
        if (execution is null)
        {
            return new(
                DurableScanStoreOutcome.NotFound,
                DurableScanReason.InvalidIdentity,
                null);
        }

        List<ScanRecoveryJobSnapshot> jobs = await dbContext.ScanJobExecutions
            .AsNoTracking()
            .Where(job => job.ScanExecutionId == scanExecutionId &&
                job.RepositoryId == execution.RepositoryId &&
                job.ScanRunId == execution.ScanRunId)
            .OrderBy(job => job.ManifestOrdinal)
            .Select(job => new ScanRecoveryJobSnapshot(
                job.Id,
                job.FileCodeUnitId,
                job.State,
                job.StateVersion,
                job.AttemptCount,
                job.LeaseToken,
                job.LeaseExpiresAt,
                job.NextAttemptAt,
                job.CommittedDocumentVersionId))
            .ToListAsync(cancellationToken);

        Guid[] completedDocumentIds = jobs
            .Where(job => job.State == ScanJobExecutionState.Completed)
            .Select(job => job.CommittedDocumentVersionId)
            .OfType<Guid>()
            .ToArray();
        bool invalidCompletionShape = jobs.Any(job =>
            (job.State == ScanJobExecutionState.Completed) !=
            job.CommittedDocumentVersionId.HasValue);
        int matchingDocumentCount = completedDocumentIds.Length == 0
            ? 0
            : await dbContext.DocumentVersions
                .AsNoTracking()
                .CountAsync(document =>
                    completedDocumentIds.Contains(document.Id) &&
                    document.RepositoryId == execution.RepositoryId &&
                    document.ScanRunId == execution.ScanRunId,
                    cancellationToken);
        if (invalidCompletionShape ||
            completedDocumentIds.Distinct().Count() != completedDocumentIds.Length ||
            matchingDocumentCount != completedDocumentIds.Length)
        {
            return new(
                DurableScanStoreOutcome.Conflict,
                DurableScanReason.IntegrityViolation,
                null);
        }

        return new(
            DurableScanStoreOutcome.Applied,
            DurableScanReason.None,
            new ScanRecoverySnapshot(
                execution.Id,
                execution.SystemId,
                execution.RepositoryId,
                execution.StateVersion,
                jobs,
                execution.Phase));
    }

    public async Task<DurableScanStoreResult> ApplyRecoveryTransitionAsync(
        DurableScanAuthorizationScope authorizationScope,
        ScanRecoveryTransitionCommand command,
        CancellationToken cancellationToken)
    {
        ScanExecution? execution = await FindAuthorizedExecutionAsync(
            authorizationScope,
            command.ScanExecutionId,
            cancellationToken);
        if (execution is null)
        {
            return NotFound();
        }

        if (execution.StateVersion != command.ExpectedStateVersion)
        {
            return Conflict(DurableScanReason.StaleStateVersion, execution.StateVersion);
        }

        try
        {
            switch (command.Transition)
            {
                case ScanRecoveryTransitionKind.NoChange:
                    if (command.ScanJobExecutionId is Guid completedJobId)
                    {
                        if (command.ExpectedJobStateVersion is not long completedJobVersion)
                        {
                            return Rejected(DurableScanReason.InvalidIdentity);
                        }

                        ScanJobExecution? completedJob = await FindBoundJobAsync(
                            execution,
                            completedJobId,
                            cancellationToken);
                        if (completedJob is null)
                        {
                            return NotFound();
                        }

                        if (completedJob.StateVersion != completedJobVersion)
                        {
                            return Conflict(
                                DurableScanReason.StaleStateVersion,
                                completedJob.StateVersion);
                        }

                        if (completedJob.State != ScanJobExecutionState.Completed ||
                            completedJob.CommittedDocumentVersionId is not Guid documentVersionId ||
                            !await dbContext.DocumentVersions.AsNoTracking().AnyAsync(
                                document => document.Id == documentVersionId &&
                                    document.RepositoryId == execution.RepositoryId &&
                                    document.ScanRunId == execution.ScanRunId,
                                cancellationToken))
                        {
                            return Conflict(DurableScanReason.IntegrityViolation);
                        }
                    }

                    return Replay(execution.StateVersion);
                case ScanRecoveryTransitionKind.PausedToPrepared:
                    execution.ResumeToPrepared(command.ExpectedStateVersion, command.ObservedAt);
                    break;
                case ScanRecoveryTransitionKind.FailedIntegrity:
                    execution.FailIntegrity(
                        command.ExpectedStateVersion,
                        "durable-state-contradiction",
                        command.ObservedAt);
                    break;
                case ScanRecoveryTransitionKind.ExpiredLeaseToPending:
                case ScanRecoveryTransitionKind.DueRetryToPending:
                    if (command.ScanJobExecutionId is not Guid jobId ||
                        command.ExpectedJobStateVersion is not long expectedJobVersion)
                    {
                        return Rejected(DurableScanReason.InvalidIdentity);
                    }

                    ScanJobExecution? job = await FindBoundJobAsync(execution, jobId, cancellationToken);
                    if (job is null)
                    {
                        return NotFound();
                    }

                    if (job.StateVersion != expectedJobVersion)
                    {
                        return Conflict(DurableScanReason.StaleStateVersion, job.StateVersion);
                    }

                    if (command.Transition == ScanRecoveryTransitionKind.ExpiredLeaseToPending)
                    {
                        job.RecoverExpiredLease(expectedJobVersion, command.ObservedAt);
                    }
                    else
                    {
                        job.RecoverDueRetry(expectedJobVersion, command.ObservedAt);
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);
                    return Applied(job.StateVersion);
                default:
                    return Rejected(DurableScanReason.InvalidIdentity);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Applied(execution.StateVersion);
        }
        catch (InvalidOperationException)
        {
            return Conflict(DurableScanReason.IntegrityViolation);
        }
        catch (ArgumentException)
        {
            return Rejected(DurableScanReason.InvalidIdentity);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(DurableScanReason.StaleStateVersion);
        }
    }

    public async Task<DurableScanStoreResult> ClaimPendingJobAsync(
        DurableScanAuthorizationScope authorizationScope,
        ScanJobLeaseCommand command,
        CancellationToken cancellationToken)
    {
        ScanExecution? execution = await FindAuthorizedExecutionAsync(
            authorizationScope,
            command.ScanExecutionId,
            cancellationToken);
        ScanJobExecution? job = execution is null
            ? null
            : await FindBoundJobAsync(execution, command.ScanJobExecutionId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        if (job.StateVersion != command.ExpectedStateVersion)
        {
            return Conflict(DurableScanReason.StaleStateVersion, job.StateVersion);
        }

        try
        {
            job.AcquireLease(command.LeaseToken, command.LeaseExpiresAt, command.StateChangedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Applied(job.StateVersion, job.LeaseToken, job.LeaseExpiresAt);
        }
        catch (InvalidOperationException)
        {
            return Conflict(DurableScanReason.IntegrityViolation, job.StateVersion);
        }
        catch (ArgumentException)
        {
            return Rejected(DurableScanReason.InvalidIdentity, job.StateVersion);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(DurableScanReason.StaleStateVersion);
        }
    }

    public async Task<DurableScanStoreResult> TransitionRunningJobAsync(
        DurableScanAuthorizationScope authorizationScope,
        RunningScanJobTransitionCommand command,
        CancellationToken cancellationToken)
    {
        ScanExecution? execution = await FindAuthorizedExecutionAsync(
            authorizationScope,
            command.ScanExecutionId,
            cancellationToken);
        ScanJobExecution? job = execution is null
            ? null
            : await FindBoundJobAsync(execution, command.ScanJobExecutionId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        if (job.StateVersion != command.ExpectedStateVersion)
        {
            return Conflict(DurableScanReason.StaleStateVersion, job.StateVersion);
        }

        if (job.LeaseToken != command.LeaseToken)
        {
            return Conflict(DurableScanReason.LeaseMismatch, job.StateVersion);
        }

        try
        {
            switch (command.TargetState)
            {
                case ScanJobExecutionState.RetryScheduled when
                    command.NextAttemptAt is DateTimeOffset nextAttemptAt &&
                    command.FailureCategory is ScanJobFailureCategory retryCategory &&
                    command.FailureCode is not null:
                    job.ScheduleRetry(
                        command.ExpectedStateVersion,
                        command.LeaseToken,
                        nextAttemptAt,
                        retryCategory,
                        command.FailureCode,
                        command.StateChangedAt);
                    break;
                case ScanJobExecutionState.Failed when
                    command.FailureCategory is ScanJobFailureCategory failureCategory &&
                    command.FailureCode is not null:
                    job.Fail(
                        command.ExpectedStateVersion,
                        command.LeaseToken,
                        failureCategory,
                        command.FailureCode,
                        command.StateChangedAt);
                    break;
                case ScanJobExecutionState.Cancelled when command.FailureCode is not null:
                    job.Cancel(
                        command.ExpectedStateVersion,
                        command.LeaseToken,
                        command.FailureCode,
                        command.StateChangedAt);
                    break;
                default:
                    return Rejected(DurableScanReason.InvalidIdentity, job.StateVersion);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Applied(job.StateVersion, job.LeaseToken, job.LeaseExpiresAt);
        }
        catch (InvalidOperationException)
        {
            return Conflict(DurableScanReason.IntegrityViolation, job.StateVersion);
        }
        catch (ArgumentException)
        {
            return Rejected(DurableScanReason.InvalidIdentity, job.StateVersion);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(DurableScanReason.StaleStateVersion);
        }
    }

    public async Task<DurableScanProgressResult> ReadProgressAsync(
        DurableScanAuthorizationScope authorizationScope,
        Guid scanExecutionId,
        CancellationToken cancellationToken)
    {
        ScanExecution? execution = await FindAuthorizedExecutionAsync(
            authorizationScope,
            scanExecutionId,
            cancellationToken) ??
            throw new InvalidOperationException("The Scan Execution was not found in the authorized scope.");
        List<ScanJobExecutionState> states = await dbContext.ScanJobExecutions
            .AsNoTracking()
            .Where(job => job.ScanExecutionId == scanExecutionId &&
                job.RepositoryId == execution.RepositoryId &&
                job.ScanRunId == execution.ScanRunId)
            .Select(job => job.State)
            .ToListAsync(cancellationToken);
        return new DurableScanProgressResult(
            execution.Id,
            execution.StateVersion,
            states.Count,
            states.Count(value => value == ScanJobExecutionState.Completed),
            states.Count(value => value == ScanJobExecutionState.Pending),
            states.Count(value => value == ScanJobExecutionState.Running),
            states.Count(value => value == ScanJobExecutionState.RetryScheduled),
            states.Count(value => value == ScanJobExecutionState.Failed),
            states.Count(value => value == ScanJobExecutionState.Cancelled));
    }

    async Task<TwoRepositoryScanCoordinationResult> ITwoRepositoryScanCoordinationStore.CreateAsync(
        DurableScanAuthorizationScope authorizationScope,
        TwoRepositoryScanCoordination coordination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(coordination);
        if (!IsScopeValid(authorizationScope) ||
            !authorizationScope.OwnedSystemIds.Contains(coordination.SystemId))
        {
            return CoordinationRejected(DurableScanReason.Unauthorized);
        }

        ScanExecution? first = await dbContext.ScanExecutions.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == coordination.ScanExecutionOneId &&
                    value.SystemId == coordination.SystemId &&
                    value.RepositoryId == coordination.RepositoryOneId &&
                    value.ScanRunId == coordination.ScanRunOneId,
                cancellationToken);
        ScanExecution? second = await dbContext.ScanExecutions.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == coordination.ScanExecutionTwoId &&
                    value.SystemId == coordination.SystemId &&
                    value.RepositoryId == coordination.RepositoryTwoId &&
                    value.ScanRunId == coordination.ScanRunTwoId,
                cancellationToken);
        if (first is null || second is null ||
            first.ProfiledRevision != coordination.ProfiledRevisionOne ||
            second.ProfiledRevision != coordination.ProfiledRevisionTwo)
        {
            return CoordinationRejected(DurableScanReason.InvalidIdentity);
        }

        TwoRepositoryScanCoordination? existing = await dbContext.TwoRepositoryScanCoordinations
            .SingleOrDefaultAsync(value => value.Id == coordination.Id, cancellationToken);
        if (existing is not null)
        {
            return CoordinationIdentityMatches(existing, coordination)
                ? new(DurableScanStoreOutcome.Replay, DurableScanReason.None, existing)
                : CoordinationConflict(DurableScanReason.InvalidIdentity, existing);
        }

        DurableScanStoreResult added = await AddAtomicallyAsync(coordination, cancellationToken);
        return new(added.Outcome, added.Reason, added.Outcome == DurableScanStoreOutcome.Applied ? coordination : null);
    }

    async Task<TwoRepositoryScanCoordinationResult> ITwoRepositoryScanCoordinationStore.ReadAsync(
        DurableScanAuthorizationScope authorizationScope,
        Guid coordinationId,
        CancellationToken cancellationToken)
    {
        TwoRepositoryScanCoordination? coordination = await FindAuthorizedCoordinationAsync(
            authorizationScope,
            coordinationId,
            cancellationToken);
        return coordination is null
            ? new(DurableScanStoreOutcome.NotFound, DurableScanReason.InvalidIdentity, null)
            : new(DurableScanStoreOutcome.Applied, DurableScanReason.None, coordination);
    }

    async Task<TwoRepositoryScanCoordinationResult> ITwoRepositoryScanCoordinationStore.TransitionAsync(
        DurableScanAuthorizationScope authorizationScope,
        TwoRepositoryScanCoordinationTransitionCommand command,
        CancellationToken cancellationToken)
    {
        TwoRepositoryScanCoordination? coordination = await FindAuthorizedCoordinationAsync(
            authorizationScope,
            command.CoordinationId,
            cancellationToken);
        if (coordination is null)
        {
            return new(DurableScanStoreOutcome.NotFound, DurableScanReason.InvalidIdentity, null);
        }

        if (coordination.StateVersion != command.ExpectedStateVersion)
        {
            return CoordinationConflict(DurableScanReason.StaleStateVersion, coordination);
        }

        if (coordination.ScanExecutionOneId != command.ScanExecutionOneId ||
            coordination.ScanExecutionTwoId != command.ScanExecutionTwoId)
        {
            return CoordinationConflict(DurableScanReason.InvalidIdentity, coordination);
        }

        try
        {
            switch (command.TargetPhase)
            {
                case TwoRepositoryScanCoordinationPhase.Pass1:
                    coordination.BeginPass1(command.ExpectedStateVersion, command.UpdatedAt);
                    break;
                case TwoRepositoryScanCoordinationPhase.Resolving:
                    coordination.BeginResolving(command.ExpectedStateVersion, command.UpdatedAt);
                    break;
                case TwoRepositoryScanCoordinationPhase.Pass2:
                    coordination.BeginPass2(command.ExpectedStateVersion, command.UpdatedAt);
                    break;
                case TwoRepositoryScanCoordinationPhase.Paused when
                    command.ReasonCategory is TwoRepositoryScanReasonCategory pauseCategory &&
                    command.ReasonCode is not null:
                    coordination.Pause(
                        command.ExpectedStateVersion,
                        pauseCategory,
                        command.ReasonCode,
                        command.UpdatedAt);
                    break;
                case TwoRepositoryScanCoordinationPhase.Completed:
                case TwoRepositoryScanCoordinationPhase.CompletedWithWarnings:
                    ScanExecution? first = await dbContext.ScanExecutions.SingleOrDefaultAsync(
                        value => value.Id == command.ScanExecutionOneId,
                        cancellationToken);
                    ScanExecution? second = await dbContext.ScanExecutions.SingleOrDefaultAsync(
                        value => value.Id == command.ScanExecutionTwoId,
                        cancellationToken);
                    if (first is null || second is null)
                    {
                        return CoordinationRejected(DurableScanReason.IntegrityViolation);
                    }

                    coordination.CompleteFromChildren(
                        command.ExpectedStateVersion,
                        first,
                        second,
                        command.UpdatedAt);
                    if (coordination.Phase != command.TargetPhase)
                    {
                        return CoordinationConflict(DurableScanReason.IntegrityViolation, coordination);
                    }
                    break;
                case TwoRepositoryScanCoordinationPhase.FailedIntegrity when command.ReasonCode is not null:
                    coordination.FailIntegrity(
                        command.ExpectedStateVersion,
                        command.ReasonCode,
                        command.UpdatedAt);
                    break;
                case TwoRepositoryScanCoordinationPhase.FailedSecurity when command.ReasonCode is not null:
                    coordination.FailSecurity(
                        command.ExpectedStateVersion,
                        command.ReasonCode,
                        command.UpdatedAt);
                    break;
                case TwoRepositoryScanCoordinationPhase.Cancelled when command.ReasonCode is not null:
                    coordination.Cancel(
                        command.ExpectedStateVersion,
                        command.ReasonCode,
                        command.UpdatedAt);
                    break;
                default:
                    return CoordinationRejected(DurableScanReason.InvalidIdentity);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new(DurableScanStoreOutcome.Applied, DurableScanReason.None, coordination);
        }
        catch (InvalidOperationException)
        {
            return CoordinationConflict(DurableScanReason.IntegrityViolation, coordination);
        }
        catch (ArgumentException)
        {
            return CoordinationRejected(DurableScanReason.InvalidIdentity);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CoordinationConflict(DurableScanReason.StaleStateVersion, coordination);
        }
    }

    private async Task<DurableScanStoreResult> AddAtomicallyAsync<TEntity>(
        TEntity entity,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.Set<TEntity>().AddAsync(entity, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Applied();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.Entry(entity).State = EntityState.Detached;
            return Conflict(DurableScanReason.IntegrityViolation);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.Entry(entity).State = EntityState.Detached;
            return Rejected(DurableScanReason.IntegrityViolation);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.Entry(entity).State = EntityState.Detached;
            throw;
        }
    }

    private async Task<ScanExecution?> FindAuthorizedExecutionAsync(
        DurableScanAuthorizationScope scope,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        if (!IsScopeValid(scope) || executionId == Guid.Empty)
        {
            return null;
        }

        return await dbContext.ScanExecutions.SingleOrDefaultAsync(
            value => value.Id == executionId && scope.OwnedSystemIds.Contains(value.SystemId),
            cancellationToken);
    }

    private async Task<ScanJobExecution?> FindBoundJobAsync(
        ScanExecution execution,
        Guid jobId,
        CancellationToken cancellationToken) =>
        await dbContext.ScanJobExecutions.SingleOrDefaultAsync(
            value => value.Id == jobId &&
                value.ScanExecutionId == execution.Id &&
                value.RepositoryId == execution.RepositoryId &&
                value.ScanRunId == execution.ScanRunId,
            cancellationToken);

    private async Task<TwoRepositoryScanCoordination?> FindAuthorizedCoordinationAsync(
        DurableScanAuthorizationScope scope,
        Guid coordinationId,
        CancellationToken cancellationToken)
    {
        if (!IsScopeValid(scope) || coordinationId == Guid.Empty)
        {
            return null;
        }

        return await dbContext.TwoRepositoryScanCoordinations.SingleOrDefaultAsync(
            value => value.Id == coordinationId && scope.OwnedSystemIds.Contains(value.SystemId),
            cancellationToken);
    }

    private async Task<bool> IsRepositoryAuthorizedAsync(
        DurableScanAuthorizationScope scope,
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        if (!IsScopeValid(scope) || repositoryId == Guid.Empty)
        {
            return false;
        }

        Guid? systemId = await dbContext.Repositories
            .Where(value => value.Id == repositoryId)
            .Select(value => value.SystemId)
            .SingleOrDefaultAsync(cancellationToken);
        return systemId is not null && scope.OwnedSystemIds.Contains(systemId.Value);
    }

    private Task<bool> RepositoryMatchesSystemAsync(
        Guid repositoryId,
        Guid systemId,
        CancellationToken cancellationToken) =>
        dbContext.Repositories.AnyAsync(
            value => value.Id == repositoryId && value.SystemId == systemId,
            cancellationToken);

    private static bool IsScopeValid(DurableScanAuthorizationScope scope) =>
        scope is not null &&
        !string.IsNullOrWhiteSpace(scope.ActorId) &&
        scope.OwnedSystemIds is not null &&
        scope.OwnedSystemIds.Count != 0 &&
        scope.OwnedSystemIds.All(value => value != Guid.Empty);

    private static bool SnapshotIdentityMatches(
        ScanContextSnapshot left,
        ScanContextSnapshot right) =>
        left.Id == right.Id &&
        left.RepositoryId == right.RepositoryId &&
        left.ScanRunId == right.ScanRunId &&
        left.ProfiledRevision == right.ProfiledRevision &&
        left.GlobalContext == right.GlobalContext &&
        left.SystemContext == right.SystemContext &&
        left.RepositoryContext == right.RepositoryContext &&
        left.SchemaIdentity == right.SchemaIdentity &&
        left.ContentHash == right.ContentHash &&
        left.CapturedAt == right.CapturedAt;

    private static bool ExecutionIdentityMatches(ScanExecution left, ScanExecution right) =>
        left.Id == right.Id &&
        left.RepositoryId == right.RepositoryId &&
        left.SystemId == right.SystemId &&
        left.ScanRunId == right.ScanRunId &&
        left.ProfiledRevision == right.ProfiledRevision &&
        left.PromptIdentity == right.PromptIdentity &&
        left.DocumentSchemaIdentity == right.DocumentSchemaIdentity &&
        left.AnalysisRuntimeIdentityJson == right.AnalysisRuntimeIdentityJson &&
        left.AnalysisRuntimeIdentityHash == right.AnalysisRuntimeIdentityHash &&
        left.StartedAt == right.StartedAt &&
        left.Phase == right.Phase &&
        left.StateVersion == right.StateVersion;

    private static bool JobIdentityAndStateMatch(ScanJobExecution left, ScanJobExecution right) =>
        left.Id == right.Id &&
        left.ScanExecutionId == right.ScanExecutionId &&
        left.RepositoryId == right.RepositoryId &&
        left.ScanRunId == right.ScanRunId &&
        left.FileCodeUnitId == right.FileCodeUnitId &&
        left.ManifestOrdinal == right.ManifestOrdinal &&
        left.State == right.State &&
        left.StateVersion == right.StateVersion;

    private static bool CoordinationIdentityMatches(
        TwoRepositoryScanCoordination left,
        TwoRepositoryScanCoordination right) =>
        left.Id == right.Id &&
        left.SystemId == right.SystemId &&
        left.RepositoryOneId == right.RepositoryOneId &&
        left.RepositoryTwoId == right.RepositoryTwoId &&
        left.ScanRunOneId == right.ScanRunOneId &&
        left.ScanRunTwoId == right.ScanRunTwoId &&
        left.ScanExecutionOneId == right.ScanExecutionOneId &&
        left.ScanExecutionTwoId == right.ScanExecutionTwoId &&
        left.ProfiledRevisionOne == right.ProfiledRevisionOne &&
        left.ProfiledRevisionTwo == right.ProfiledRevisionTwo &&
        left.CreatedAt == right.CreatedAt &&
        left.Phase == right.Phase &&
        left.StateVersion == right.StateVersion;

    private void DetachAdded(IEnumerable<ScanJobExecution> jobs)
    {
        foreach (ScanJobExecution job in jobs)
        {
            dbContext.Entry(job).State = EntityState.Detached;
        }
    }

    private static DurableScanStoreResult Applied(
        long? version = null,
        Guid? lease = null,
        DateTimeOffset? expiry = null) =>
        new(DurableScanStoreOutcome.Applied, DurableScanReason.None, version, lease, expiry);

    private static DurableScanStoreResult Replay(long? version = null) =>
        new(DurableScanStoreOutcome.Replay, DurableScanReason.None, version, null, null);

    private static DurableScanStoreResult Conflict(
        DurableScanReason reason,
        long? version = null) =>
        new(DurableScanStoreOutcome.Conflict, reason, version, null, null);

    private static DurableScanStoreResult Rejected(
        DurableScanReason reason,
        long? version = null) =>
        new(DurableScanStoreOutcome.Rejected, reason, version, null, null);

    private static DurableScanStoreResult NotFound() =>
        new(DurableScanStoreOutcome.NotFound, DurableScanReason.InvalidIdentity, null, null, null);

    private static TwoRepositoryScanCoordinationResult CoordinationRejected(DurableScanReason reason) =>
        new(DurableScanStoreOutcome.Rejected, reason, null);

    private static TwoRepositoryScanCoordinationResult CoordinationConflict(
        DurableScanReason reason,
        TwoRepositoryScanCoordination coordination) =>
        new(DurableScanStoreOutcome.Conflict, reason, coordination);
}

using Vulgata.Tests.TestInfrastructure;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.DurableScanning;

public sealed class ScanJobExecutionTests
{
    private static readonly Guid _scanExecutionId =
        Guid.Parse("10000000-0000-4000-8000-000000000001");

    private static readonly Guid _repositoryId =
        Guid.Parse("20000000-0000-4000-8000-000000000002");

    private static readonly Guid _scanRunId =
        Guid.Parse("30000000-0000-4000-8000-000000000003");

    private static readonly Guid _fileCodeUnitId =
        Guid.Parse("40000000-0000-4000-8000-000000000004");

    private static readonly DateTimeOffset _createdAt =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CreateAndAcquireLease_WithSamePreparedJob_ProducesDeterministicIdentityAndMonotonicRunningLease()
    {
        ScanJobExecution first = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 7,
            stateChangedAt: _createdAt);
        ScanJobExecution repeat = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 7,
            stateChangedAt: _createdAt);
        ScanJobExecution neighboringOrdinal = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 8,
            stateChangedAt: _createdAt);

        Assert.Equal(first.Id, repeat.Id);
        Assert.NotEqual(first.Id, neighboringOrdinal.Id);
        Assert.Equal(ScanJobExecutionState.Pending, first.State);
        Assert.Equal(0, first.AttemptCount);
        Assert.Equal(0, first.StateVersion);
        Assert.Null(first.LeaseToken);
        Assert.Null(first.LeaseExpiresAt);

        Guid leaseToken = Guid.Parse("50000000-0000-4000-8000-000000000005");
        DateTimeOffset leaseExpiresAt = _createdAt.AddMinutes(5);

        first.AcquireLease(leaseToken, leaseExpiresAt, _createdAt.AddSeconds(1));

        Assert.Equal(ScanJobExecutionState.Running, first.State);
        Assert.Equal(1, first.AttemptCount);
        Assert.Equal(1, first.StateVersion);
        Assert.Equal(leaseToken, first.LeaseToken);
        Assert.Equal(leaseExpiresAt, first.LeaseExpiresAt);
        Assert.Equal(_createdAt.AddSeconds(1), first.StateChangedAt);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ScheduleRetry_WithCurrentLeaseAndVersion_ClearsLeaseAndAdvancesRetryStateWithoutAcceptingContradictions()
    {
        ScanJobExecution job = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 7,
            stateChangedAt: _createdAt);
        Guid leaseToken = Guid.Parse("50000000-0000-4000-8000-000000000005");
        job.AcquireLease(leaseToken, _createdAt.AddMinutes(5), _createdAt.AddSeconds(1));

        DateTimeOffset retryAt = _createdAt.AddMinutes(10);
        DateTimeOffset retriedAt = _createdAt.AddSeconds(2);
        Guid wrongLeaseToken = Guid.Parse("60000000-0000-4000-8000-000000000006");

        Assert.Throws<InvalidOperationException>(() => job.ScheduleRetry(
            expectedStateVersion: 0,
            expectedLeaseToken: leaseToken,
            nextAttemptAt: retryAt,
            failureCategory: ScanJobFailureCategory.ProviderTransient,
            failureCode: "provider-timeout",
            stateChangedAt: retriedAt));
        Assert.Throws<InvalidOperationException>(() => job.ScheduleRetry(
            expectedStateVersion: 1,
            expectedLeaseToken: wrongLeaseToken,
            nextAttemptAt: retryAt,
            failureCategory: ScanJobFailureCategory.ProviderTransient,
            failureCode: "provider-timeout",
            stateChangedAt: retriedAt));

        job.ScheduleRetry(
            expectedStateVersion: 1,
            expectedLeaseToken: leaseToken,
            nextAttemptAt: retryAt,
            failureCategory: ScanJobFailureCategory.ProviderTransient,
            failureCode: "provider-timeout",
            stateChangedAt: retriedAt);

        Assert.Equal(ScanJobExecutionState.RetryScheduled, job.State);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(2, job.StateVersion);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseExpiresAt);
        Assert.Equal(retryAt, job.NextAttemptAt);
        Assert.Equal(ScanJobFailureCategory.ProviderTransient, job.FailureCategory);
        Assert.Equal("provider-timeout", job.FailureCode);
        Assert.Equal(retriedAt, job.StateChangedAt);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void RecoverAndReacquire_WithDueRetryOrExpiredLease_RejectsEarlyAndStaleTransitionsWithoutMutation()
    {
        ScanJobExecution retryingJob = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 7,
            stateChangedAt: _createdAt);
        Guid originalLeaseToken = Guid.Parse("50000000-0000-4000-8000-000000000005");
        retryingJob.AcquireLease(
            originalLeaseToken,
            _createdAt.AddMinutes(5),
            _createdAt.AddSeconds(1));
        DateTimeOffset retryAt = _createdAt.AddMinutes(10);
        retryingJob.ScheduleRetry(
            expectedStateVersion: 1,
            expectedLeaseToken: originalLeaseToken,
            nextAttemptAt: retryAt,
            failureCategory: ScanJobFailureCategory.ProviderTransient,
            failureCode: "provider-timeout",
            stateChangedAt: _createdAt.AddSeconds(2));

        Assert.Throws<InvalidOperationException>(() => retryingJob.RecoverDueRetry(
            expectedStateVersion: 2,
            now: retryAt.AddTicks(-1)));
        Assert.Throws<InvalidOperationException>(() => retryingJob.RecoverDueRetry(
            expectedStateVersion: 1,
            now: retryAt));
        Assert.Equal(ScanJobExecutionState.RetryScheduled, retryingJob.State);
        Assert.Equal(2, retryingJob.StateVersion);
        Assert.Equal(retryAt, retryingJob.NextAttemptAt);

        retryingJob.RecoverDueRetry(expectedStateVersion: 2, now: retryAt);

        Assert.Equal(ScanJobExecutionState.Pending, retryingJob.State);
        Assert.Equal(1, retryingJob.AttemptCount);
        Assert.Equal(3, retryingJob.StateVersion);
        Assert.Null(retryingJob.LeaseToken);
        Assert.Null(retryingJob.LeaseExpiresAt);
        Assert.Null(retryingJob.NextAttemptAt);

        Guid reacquiredLeaseToken = Guid.Parse("70000000-0000-4000-8000-000000000007");
        retryingJob.AcquireLease(
            reacquiredLeaseToken,
            retryAt.AddMinutes(5),
            retryAt.AddSeconds(1));

        Assert.Equal(ScanJobExecutionState.Running, retryingJob.State);
        Assert.Equal(2, retryingJob.AttemptCount);
        Assert.Equal(4, retryingJob.StateVersion);
        Assert.Equal(reacquiredLeaseToken, retryingJob.LeaseToken);

        ScanJobExecution expiringJob = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 8,
            stateChangedAt: _createdAt);
        Guid expiringLeaseToken = Guid.Parse("80000000-0000-4000-8000-000000000008");
        DateTimeOffset leaseExpiresAt = _createdAt.AddMinutes(5);
        expiringJob.AcquireLease(expiringLeaseToken, leaseExpiresAt, _createdAt.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() => expiringJob.RecoverExpiredLease(
            expectedStateVersion: 1,
            now: leaseExpiresAt));
        Assert.Throws<InvalidOperationException>(() => expiringJob.RecoverExpiredLease(
            expectedStateVersion: 0,
            now: leaseExpiresAt.AddTicks(1)));
        Assert.Equal(ScanJobExecutionState.Running, expiringJob.State);
        Assert.Equal(1, expiringJob.StateVersion);
        Assert.Equal(expiringLeaseToken, expiringJob.LeaseToken);
        Assert.Equal(leaseExpiresAt, expiringJob.LeaseExpiresAt);

        expiringJob.RecoverExpiredLease(
            expectedStateVersion: 1,
            now: leaseExpiresAt.AddTicks(1));

        Assert.Equal(ScanJobExecutionState.Pending, expiringJob.State);
        Assert.Equal(1, expiringJob.AttemptCount);
        Assert.Equal(2, expiringJob.StateVersion);
        Assert.Null(expiringJob.LeaseToken);
        Assert.Null(expiringJob.LeaseExpiresAt);
        Assert.Equal(leaseExpiresAt.AddTicks(1), expiringJob.StateChangedAt);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Complete_WithCurrentRunningLeaseAndVersion_PreservesOnlyTheImmutableDocumentLinkAndRejectsFurtherMutation()
    {
        ScanJobExecution job = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 7,
            stateChangedAt: _createdAt);
        Guid leaseToken = Guid.Parse("50000000-0000-4000-8000-000000000005");
        Guid wrongLeaseToken = Guid.Parse("60000000-0000-4000-8000-000000000006");
        Guid documentVersionId = Guid.Parse("90000000-0000-4000-8000-000000000009");
        job.AcquireLease(leaseToken, _createdAt.AddMinutes(5), _createdAt.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() => job.Complete(
            expectedStateVersion: 0,
            expectedLeaseToken: leaseToken,
            committedDocumentVersionId: documentVersionId,
            stateChangedAt: _createdAt.AddSeconds(2)));
        Assert.Throws<InvalidOperationException>(() => job.Complete(
            expectedStateVersion: 1,
            expectedLeaseToken: wrongLeaseToken,
            committedDocumentVersionId: documentVersionId,
            stateChangedAt: _createdAt.AddSeconds(2)));
        Assert.Throws<ArgumentException>(() => job.Complete(
            expectedStateVersion: 1,
            expectedLeaseToken: leaseToken,
            committedDocumentVersionId: Guid.Empty,
            stateChangedAt: _createdAt.AddSeconds(2)));

        job.Complete(
            expectedStateVersion: 1,
            expectedLeaseToken: leaseToken,
            committedDocumentVersionId: documentVersionId,
            stateChangedAt: _createdAt.AddSeconds(2));

        Assert.Equal(ScanJobExecutionState.Completed, job.State);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(2, job.StateVersion);
        Assert.Equal(documentVersionId, job.CommittedDocumentVersionId);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseExpiresAt);
        Assert.Null(job.NextAttemptAt);
        Assert.Null(job.FailureCategory);
        Assert.Null(job.FailureCode);

        Assert.Throws<InvalidOperationException>(() => job.AcquireLease(
            Guid.Parse("a0000000-0000-4000-8000-00000000000a"),
            _createdAt.AddMinutes(10),
            _createdAt.AddSeconds(3)));
        Assert.Equal(ScanJobExecutionState.Completed, job.State);
        Assert.Equal(2, job.StateVersion);
        Assert.Equal(documentVersionId, job.CommittedDocumentVersionId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(ScanJobFailureCategory.ProviderTransient)]
    [InlineData(ScanJobFailureCategory.ProviderConfiguration)]
    [InlineData(ScanJobFailureCategory.JobTransient)]
    [InlineData(ScanJobFailureCategory.JobPermanent)]
    [InlineData(ScanJobFailureCategory.Integrity)]
    [InlineData(ScanJobFailureCategory.Security)]
    [InlineData(ScanJobFailureCategory.Cancellation)]
    [InlineData(ScanJobFailureCategory.Unclassified)]
    public void Fail_FromRunning_WithClosedFailureCategoryAndCurrentLease_ClearsDurableCompletionFields(
        ScanJobFailureCategory failureCategory)
    {
        ScanJobExecution job = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 7,
            stateChangedAt: _createdAt);
        Guid leaseToken = Guid.Parse("50000000-0000-4000-8000-000000000005");
        job.AcquireLease(leaseToken, _createdAt.AddMinutes(5), _createdAt.AddSeconds(1));

        job.Fail(
            expectedStateVersion: 1,
            expectedLeaseToken: leaseToken,
            failureCategory: failureCategory,
            failureCode: "  Provider-TimeOut  ",
            stateChangedAt: _createdAt.AddSeconds(2));

        Assert.Equal(ScanJobExecutionState.Failed, job.State);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(2, job.StateVersion);
        Assert.Null(job.CommittedDocumentVersionId);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseExpiresAt);
        Assert.Null(job.NextAttemptAt);
        Assert.Equal(failureCategory, job.FailureCategory);
        Assert.Equal("provider-timeout", job.FailureCode);

        Assert.Throws<InvalidOperationException>(() => job.AcquireLease(
            Guid.Parse("a0000000-0000-4000-8000-00000000000a"),
            _createdAt.AddMinutes(10),
            _createdAt.AddSeconds(3)));
        Assert.Equal(2, job.StateVersion);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Fail_FromRetryScheduled_UsesCurrentVersionWithoutLeaseAndRejectsFurtherMutation()
    {
        ScanJobExecution job = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 7,
            stateChangedAt: _createdAt);
        Guid leaseToken = Guid.Parse("50000000-0000-4000-8000-000000000005");
        job.AcquireLease(leaseToken, _createdAt.AddMinutes(5), _createdAt.AddSeconds(1));
        job.ScheduleRetry(
            expectedStateVersion: 1,
            expectedLeaseToken: leaseToken,
            nextAttemptAt: _createdAt.AddMinutes(10),
            failureCategory: ScanJobFailureCategory.ProviderTransient,
            failureCode: "provider-timeout",
            stateChangedAt: _createdAt.AddSeconds(2));

        job.Fail(
            expectedStateVersion: 2,
            expectedLeaseToken: null,
            failureCategory: ScanJobFailureCategory.JobPermanent,
            failureCode: "invalid-result",
            stateChangedAt: _createdAt.AddSeconds(3));

        Assert.Equal(ScanJobExecutionState.Failed, job.State);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(3, job.StateVersion);
        Assert.Null(job.CommittedDocumentVersionId);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseExpiresAt);
        Assert.Null(job.NextAttemptAt);
        Assert.Equal(ScanJobFailureCategory.JobPermanent, job.FailureCategory);
        Assert.Equal("invalid-result", job.FailureCode);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Cancel_FromPendingRunningOrRetryScheduled_RecordsCancellationAndLeavesNoLeaseOrDocument()
    {
        ScanJobExecution pending = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 7,
            stateChangedAt: _createdAt);
        pending.Cancel(
            expectedStateVersion: 0,
            expectedLeaseToken: null,
            failureCode: " operator-cancelled ",
            stateChangedAt: _createdAt.AddSeconds(1));

        ScanJobExecution running = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 8,
            stateChangedAt: _createdAt);
        Guid runningLeaseToken = Guid.Parse("80000000-0000-4000-8000-000000000008");
        running.AcquireLease(runningLeaseToken, _createdAt.AddMinutes(5), _createdAt.AddSeconds(1));
        running.Cancel(
            expectedStateVersion: 1,
            expectedLeaseToken: runningLeaseToken,
            failureCode: "operator-cancelled",
            stateChangedAt: _createdAt.AddSeconds(2));

        ScanJobExecution retryScheduled = ScanJobExecution.Create(
            _scanExecutionId,
            _repositoryId,
            _scanRunId,
            _fileCodeUnitId,
            manifestOrdinal: 9,
            stateChangedAt: _createdAt);
        Guid retryLeaseToken = Guid.Parse("a0000000-0000-4000-8000-00000000000a");
        retryScheduled.AcquireLease(retryLeaseToken, _createdAt.AddMinutes(5), _createdAt.AddSeconds(1));
        retryScheduled.ScheduleRetry(
            expectedStateVersion: 1,
            expectedLeaseToken: retryLeaseToken,
            nextAttemptAt: _createdAt.AddMinutes(10),
            failureCategory: ScanJobFailureCategory.ProviderTransient,
            failureCode: "provider-timeout",
            stateChangedAt: _createdAt.AddSeconds(2));
        retryScheduled.Cancel(
            expectedStateVersion: 2,
            expectedLeaseToken: null,
            failureCode: "operator-cancelled",
            stateChangedAt: _createdAt.AddSeconds(3));

        AssertCancellationTerminalShape(pending, expectedStateVersion: 1);
        AssertCancellationTerminalShape(running, expectedStateVersion: 2);
        AssertCancellationTerminalShape(retryScheduled, expectedStateVersion: 3);
    }

    private static void AssertCancellationTerminalShape(
        ScanJobExecution job,
        long expectedStateVersion)
    {
        Assert.Equal(ScanJobExecutionState.Cancelled, job.State);
        Assert.Equal(expectedStateVersion, job.StateVersion);
        Assert.Equal(ScanJobFailureCategory.Cancellation, job.FailureCategory);
        Assert.Equal("operator-cancelled", job.FailureCode);
        Assert.Null(job.CommittedDocumentVersionId);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseExpiresAt);
        Assert.Null(job.NextAttemptAt);

        Assert.Throws<InvalidOperationException>(() => job.AcquireLease(
            Guid.Parse("b0000000-0000-4000-8000-00000000000b"),
            new DateTimeOffset(2026, 8, 30, 9, 1, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero)));
        Assert.Equal(expectedStateVersion, job.StateVersion);
    }
}

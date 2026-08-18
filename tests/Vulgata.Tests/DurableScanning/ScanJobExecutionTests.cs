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

    [Fact]
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

    [Fact]
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
}

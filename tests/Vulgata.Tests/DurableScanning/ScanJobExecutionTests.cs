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
}

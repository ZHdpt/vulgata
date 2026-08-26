namespace Vulgata.Core.Entities;

public enum Pass2ExecutionState
{
    Planning,
    Running,
    ProbingProvider,
    PausedProviderUnavailable,
    PausedOperatorCorrection,
    Completing,
    Completed,
    CompletedWithWarnings,
    FailedIntegrity,
    FailedSecurity,
    Cancelled,
}

public sealed class Pass2Execution
{
    private Pass2Execution()
    {
    }

    public static Pass2Execution CreatePlanning(
        Guid id,
        Guid pass1PairExecutionId,
        Guid coordinationId,
        Guid systemId,
        DateTimeOffset startedAt)
    {
        if (id == Guid.Empty || pass1PairExecutionId == Guid.Empty ||
            coordinationId == Guid.Empty || systemId == Guid.Empty ||
            startedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A complete UTC-bound Pass 2 execution identity is required.");
        }

        return new Pass2Execution
        {
            Id = id,
            Pass1PairExecutionId = pass1PairExecutionId,
            CoordinationId = coordinationId,
            SystemId = systemId,
            State = Pass2ExecutionState.Planning,
            StartedAt = startedAt,
            UpdatedAt = startedAt,
        };
    }

    public void MarkRunning(DateTimeOffset changedAt)
    {
        if (State != Pass2ExecutionState.Planning ||
            changedAt.Offset != TimeSpan.Zero || changedAt < UpdatedAt)
        {
            throw new InvalidOperationException("Only a complete planned execution can start running.");
        }

        State = Pass2ExecutionState.Running;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    public void RecordPublished(long expectedStateVersion, DateTimeOffset changedAt)
    {
        RequireRunningAccountingTransition(expectedStateVersion, changedAt);
        PublishedCount++;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    public void RecordFailed(long expectedStateVersion, DateTimeOffset changedAt)
    {
        RequireRunningAccountingTransition(expectedStateVersion, changedAt);
        FailedCount++;
        StateVersion++;
        UpdatedAt = changedAt;
    }

    public Guid Id { get; private set; }
    public Guid Pass1PairExecutionId { get; private set; }
    public Guid CoordinationId { get; private set; }
    public Guid SystemId { get; private set; }
    public Pass2ExecutionState State { get; private set; }
    public int PublishedCount { get; private set; }
    public int FailedCount { get; private set; }
    public long StateVersion { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private void RequireRunningAccountingTransition(
        long expectedStateVersion,
        DateTimeOffset changedAt)
    {
        if (State != Pass2ExecutionState.Running || StateVersion != expectedStateVersion ||
            changedAt.Offset != TimeSpan.Zero || changedAt < UpdatedAt)
        {
            throw new InvalidOperationException(
                "Only the current running Pass 2 execution can advance terminal accounting.");
        }
    }
}

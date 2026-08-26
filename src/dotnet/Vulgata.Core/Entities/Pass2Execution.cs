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

    public Guid Id { get; private set; }
    public Guid Pass1PairExecutionId { get; private set; }
    public Guid CoordinationId { get; private set; }
    public Guid SystemId { get; private set; }
    public Pass2ExecutionState State { get; private set; }
    public long StateVersion { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}

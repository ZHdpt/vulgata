namespace Vulgata.Core.Entities;

public enum Pass2JobExecutionState
{
    Pending,
    Running,
    RetryScheduled,
    Completed,
    Failed,
    Cancelled,
}

public sealed class Pass2JobExecution
{
    private Pass2JobExecution()
    {
    }

    public static Pass2JobExecution CreatePending(
        Guid id,
        Guid pass2ExecutionId,
        Guid flowGroupId,
        int groupOrder)
    {
        if (id == Guid.Empty || pass2ExecutionId == Guid.Empty || flowGroupId == Guid.Empty)
        {
            throw new ArgumentException("Complete Pass 2 Job identities are required.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(groupOrder);
        return new Pass2JobExecution
        {
            Id = id,
            Pass2ExecutionId = pass2ExecutionId,
            FlowGroupId = flowGroupId,
            GroupOrder = groupOrder,
            State = Pass2JobExecutionState.Pending,
        };
    }

    public Guid Id { get; private set; }
    public Guid Pass2ExecutionId { get; private set; }
    public Guid FlowGroupId { get; private set; }
    public int GroupOrder { get; private set; }
    public Pass2JobExecutionState State { get; private set; }
    public int AttemptCount { get; private set; }
    public long StateVersion { get; private set; }
    public Guid? CommittedDocumentVersionId { get; private set; }
    public string? CompletionFingerprint { get; private set; }
}

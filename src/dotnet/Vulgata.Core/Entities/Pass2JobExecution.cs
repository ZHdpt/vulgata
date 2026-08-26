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

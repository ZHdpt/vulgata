namespace Vulgata.Core.Entities;

public sealed class Pass1NavigationBudget
{
    private Pass1NavigationBudget()
    {
    }

    public Guid ScanJobExecutionId { get; private set; }
    public Guid PairExecutionId { get; private set; }
    public int ToolAttemptCount { get; private set; }
    public int AcceptedCallCount { get; private set; }
    public int EvidenceBytes { get; private set; }
    public int MaximumDepthObserved { get; private set; }
    public long StateVersion { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}

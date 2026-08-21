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

    public static Pass1NavigationBudget Create(
        Guid scanJobExecutionId,
        Guid pairExecutionId,
        DateTimeOffset updatedAt)
    {
        if (scanJobExecutionId == Guid.Empty || pairExecutionId == Guid.Empty)
        {
            throw new ArgumentException("Complete job and pair identities are required.");
        }

        if (updatedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use the UTC offset.", nameof(updatedAt));
        }

        return new Pass1NavigationBudget
        {
            ScanJobExecutionId = scanJobExecutionId,
            PairExecutionId = pairExecutionId,
            UpdatedAt = updatedAt,
        };
    }
}

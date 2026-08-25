using Vulgata.Core.DomainServices;

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

    public void ApplyReceipt(
        long expectedStateVersion,
        int? toolAttemptOrdinal,
        bool accepted,
        int evidenceBytes,
        int depth,
        DateTimeOffset updatedAt)
    {
        if (StateVersion != expectedStateVersion ||
            updatedAt.Offset != TimeSpan.Zero ||
            evidenceBytes < 0 ||
            depth is < 0 or > 4 ||
            (toolAttemptOrdinal.HasValue &&
                (toolAttemptOrdinal.Value != ToolAttemptCount + 1 || toolAttemptOrdinal.Value > 8)) ||
            (!toolAttemptOrdinal.HasValue && ToolAttemptCount != 8) ||
            (accepted && !toolAttemptOrdinal.HasValue) ||
            (accepted && AcceptedCallCount >= 8) ||
            EvidenceBytes > 65_536 - evidenceBytes)
        {
            throw new InvalidOperationException("The navigation budget transition is invalid.");
        }

        if (toolAttemptOrdinal.HasValue)
        {
            ToolAttemptCount++;
        }

        if (accepted)
        {
            AcceptedCallCount++;
            EvidenceBytes += evidenceBytes;
        }

        MaximumDepthObserved = Math.Max(MaximumDepthObserved, Math.Min(depth, 3));
        StateVersion++;
        UpdatedAt = updatedAt;
    }

    public PreparedPass1NavigationBudgetCursor ReserveNextAttempt(
        long expectedStateVersion,
        int expectedToolAttemptCount,
        DateTimeOffset updatedAt)
    {
        if (StateVersion != expectedStateVersion ||
            ToolAttemptCount != expectedToolAttemptCount ||
            ToolAttemptCount >= 8 ||
            updatedAt.Offset != TimeSpan.Zero ||
            updatedAt < UpdatedAt)
        {
            throw new InvalidOperationException("The navigation attempt reservation is invalid.");
        }

        ToolAttemptCount++;
        StateVersion++;
        UpdatedAt = updatedAt;
        return new PreparedPass1NavigationBudgetCursor(
            ToolAttemptCount,
            ToolAttemptCount,
            StateVersion);
    }

    public PreparedPass1NavigationBudgetCursor ApplyReservedReceipt(
        long expectedStateVersion,
        int reservedToolAttemptCount,
        bool accepted,
        int evidenceBytes,
        int depth,
        DateTimeOffset updatedAt)
    {
        if (StateVersion != expectedStateVersion ||
            reservedToolAttemptCount is < 1 or > 8 ||
            reservedToolAttemptCount > ToolAttemptCount ||
            updatedAt.Offset != TimeSpan.Zero ||
            updatedAt < UpdatedAt ||
            evidenceBytes < 0 ||
            depth is < 0 or > 4 ||
            (accepted && AcceptedCallCount >= 8) ||
            (accepted && EvidenceBytes > 65_536 - evidenceBytes))
        {
            throw new InvalidOperationException("The reserved navigation receipt transition is invalid.");
        }

        if (accepted)
        {
            AcceptedCallCount++;
            EvidenceBytes += evidenceBytes;
        }

        MaximumDepthObserved = Math.Max(MaximumDepthObserved, Math.Min(depth, 3));
        StateVersion++;
        UpdatedAt = updatedAt;
        return new PreparedPass1NavigationBudgetCursor(
            reservedToolAttemptCount,
            ToolAttemptCount,
            StateVersion);
    }
}

namespace Vulgata.Core.Entities;

public enum Pass1NavigationOperation
{
    Definition,
    References,
    Implementations,
    Callers,
    Callees,
    SymbolSearch,
    RelatedFiles,
    ExactSourceEvidence,
}

public enum Pass1NavigationOutcomeKind
{
    DocumentEvidence,
    SourceEvidence,
    Unavailable,
    BoundExceeded,
}

public enum Pass1NavigationBoundKind
{
    Calls,
    Results,
    EvidenceBytes,
    Depth,
}

public sealed class Pass1NavigationReceipt
{
    private Pass1NavigationReceipt()
    {
    }

    public Guid Id { get; private set; }
    public Guid ScanJobExecutionId { get; private set; }
    public Guid PairExecutionId { get; private set; }
    public int ReceiptOrdinal { get; private set; }
    public int? ToolAttemptOrdinal { get; private set; }
    public Pass1NavigationOperation? Operation { get; private set; }
    public int? Depth { get; private set; }
    public Pass1NavigationOutcomeKind Outcome { get; private set; }
    public Pass1NavigationBoundKind? BoundKind { get; private set; }
    public string? RequestFingerprint { get; private set; }
    public string ReceiptFingerprint { get; private set; } = string.Empty;
    public int ResultCount { get; private set; }
    public int EvidenceBytes { get; private set; }
    public string DescriptorJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Pass1NavigationReceipt Create(
        Guid id,
        Guid scanJobExecutionId,
        Guid pairExecutionId,
        int receiptOrdinal,
        int? toolAttemptOrdinal,
        Pass1NavigationOperation? operation,
        int? depth,
        Pass1NavigationOutcomeKind outcome,
        Pass1NavigationBoundKind? boundKind,
        string? requestFingerprint,
        string receiptFingerprint,
        int resultCount,
        int evidenceBytes,
        string descriptorJson,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || scanJobExecutionId == Guid.Empty || pairExecutionId == Guid.Empty ||
            receiptOrdinal is < 1 or > 9 ||
            toolAttemptOrdinal is < 1 or > 8 ||
            depth is < 1 or > 4 ||
            resultCount is < 0 or > 20 || evidenceBytes is < 0 or > 65_536 ||
            receiptFingerprint is not { Length: 64 } ||
            requestFingerprint is { Length: not 64 } ||
            string.IsNullOrWhiteSpace(descriptorJson) || createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The navigation receipt is invalid.");
        }

        return new Pass1NavigationReceipt
        {
            Id = id,
            ScanJobExecutionId = scanJobExecutionId,
            PairExecutionId = pairExecutionId,
            ReceiptOrdinal = receiptOrdinal,
            ToolAttemptOrdinal = toolAttemptOrdinal,
            Operation = operation,
            Depth = depth,
            Outcome = outcome,
            BoundKind = boundKind,
            RequestFingerprint = requestFingerprint,
            ReceiptFingerprint = receiptFingerprint,
            ResultCount = resultCount,
            EvidenceBytes = evidenceBytes,
            DescriptorJson = descriptorJson,
            CreatedAt = createdAt,
        };
    }
}

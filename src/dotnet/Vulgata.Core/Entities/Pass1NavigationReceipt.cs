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
}

namespace Vulgata.Core.Entities;

public enum Pass2RetrievalReceiptState
{
    Accepted,
    NoResults,
    BoundExceeded,
    Refused,
    Unavailable,
}

public sealed class Pass2RetrievalReceipt
{
    private Pass2RetrievalReceipt()
    {
    }

    public Guid Id { get; private set; }
    public Guid Pass2JobExecutionId { get; private set; }
    public int AttemptOrdinal { get; private set; }
    public int CallOrdinal { get; private set; }
    public Pass2RetrievalReceiptState State { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}

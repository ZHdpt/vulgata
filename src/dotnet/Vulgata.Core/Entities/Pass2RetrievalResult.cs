namespace Vulgata.Core.Entities;

public enum Pass2RetrievalResultState
{
    Admitted,
}

public sealed class Pass2RetrievalResult
{
    private Pass2RetrievalResult()
    {
    }

    public Guid Id { get; private set; }
    public Guid ReceiptId { get; private set; }
    public Guid Pass2JobExecutionId { get; private set; }
    public int AttemptOrdinal { get; private set; }
    public int ResultOrdinal { get; private set; }
    public Pass2RetrievalResultState State { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public string EvidenceFingerprint { get; private set; } = string.Empty;
}

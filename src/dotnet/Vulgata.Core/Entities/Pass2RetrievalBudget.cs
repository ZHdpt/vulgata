namespace Vulgata.Core.Entities;

public enum Pass2RetrievalBudgetState
{
    Available,
    Exhausted,
}

public sealed class Pass2RetrievalBudget
{
    private Pass2RetrievalBudget()
    {
    }

    public Guid Id { get; private set; }
    public Guid Pass2JobExecutionId { get; private set; }
    public Pass2RetrievalBudgetState State { get; private set; }
    public int CallsReserved { get; private set; }
    public int ResultsReturned { get; private set; }
    public int AdditionalDocumentCount { get; private set; }
    public int ReturnedUtf8Bytes { get; private set; }
    public long StateVersion { get; private set; }
}

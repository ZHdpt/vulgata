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

    public static Pass2RetrievalBudget Create(Guid id, Guid pass2JobExecutionId)
    {
        if (id == Guid.Empty || pass2JobExecutionId == Guid.Empty)
        {
            throw new ArgumentException("Complete Pass 2 retrieval budget identities are required.");
        }

        return new Pass2RetrievalBudget
        {
            Id = id,
            Pass2JobExecutionId = pass2JobExecutionId,
            State = Pass2RetrievalBudgetState.Available,
        };
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

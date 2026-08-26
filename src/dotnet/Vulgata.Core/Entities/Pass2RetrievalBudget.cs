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

    public static Pass2RetrievalBudget Create(
        Guid pass2JobExecutionId,
        DateTimeOffset updatedAt)
    {
        if (pass2JobExecutionId == Guid.Empty)
        {
            throw new ArgumentException("Complete Pass 2 retrieval budget identities are required.");
        }
        if (updatedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The retrieval budget timestamp must be UTC.", nameof(updatedAt));
        }

        return new Pass2RetrievalBudget
        {
            Pass2JobExecutionId = pass2JobExecutionId,
            State = Pass2RetrievalBudgetState.Available,
            UpdatedAt = updatedAt,
        };
    }

    public Guid Pass2JobExecutionId { get; private set; }
    public Pass2RetrievalBudgetState State { get; private set; }
    public int CallsReserved { get; private set; }
    public int ResultsReturned { get; private set; }
    public int AdditionalDocumentCount { get; private set; }
    public int ReturnedUtf8Bytes { get; private set; }
    public long StateVersion { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public int ReserveCall(DateTimeOffset updatedAt)
    {
        if (CallsReserved >= DomainServices.Pass2RetrievalContracts.MaxCalls)
        {
            throw new InvalidOperationException("The retrieval call budget is exhausted.");
        }

        SetUpdatedAt(updatedAt);
        CallsReserved++;
        StateVersion++;
        if (CallsReserved == DomainServices.Pass2RetrievalContracts.MaxCalls)
        {
            State = Pass2RetrievalBudgetState.Exhausted;
        }

        return CallsReserved;
    }

    public void Admit(
        int resultCount,
        int additionalDocumentCount,
        int returnedUtf8Bytes,
        DateTimeOffset updatedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(resultCount);
        ArgumentOutOfRangeException.ThrowIfNegative(additionalDocumentCount);
        ArgumentOutOfRangeException.ThrowIfNegative(returnedUtf8Bytes);
        if (ResultsReturned + resultCount > DomainServices.Pass2RetrievalContracts.MaxResults ||
            AdditionalDocumentCount + additionalDocumentCount >
                DomainServices.Pass2RetrievalContracts.MaxAdditionalDocuments ||
            ReturnedUtf8Bytes + returnedUtf8Bytes >
                DomainServices.Pass2RetrievalContracts.MaxReturnedUtf8Bytes)
        {
            throw new InvalidOperationException("The retrieval result budget would be exceeded.");
        }

        SetUpdatedAt(updatedAt);
        ResultsReturned += resultCount;
        AdditionalDocumentCount += additionalDocumentCount;
        ReturnedUtf8Bytes += returnedUtf8Bytes;
        StateVersion++;
    }

    private void SetUpdatedAt(DateTimeOffset updatedAt)
    {
        if (updatedAt.Offset != TimeSpan.Zero || updatedAt < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAt));
        }

        UpdatedAt = updatedAt;
    }
}

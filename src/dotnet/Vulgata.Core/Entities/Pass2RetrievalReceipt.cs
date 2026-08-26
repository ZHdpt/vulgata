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

    public static Pass2RetrievalReceipt Create(
        Guid id,
        Guid pass2JobExecutionId,
        int attemptOrdinal,
        int callOrdinal,
        string toolCallIdFingerprint,
        Pass2RetrievalReceiptState state,
        DomainServices.Pass2CodeLogicRetrievalOperation operation,
        string requestFingerprint,
        int resultCount,
        int returnedUtf8Bytes,
        int newAdditionalDocumentCount,
        string resultSetFingerprint,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || pass2JobExecutionId == Guid.Empty ||
            attemptOrdinal is < 1 or > 2 || callOrdinal is < 1 or > 8 ||
            !Enum.IsDefined(state) || !Enum.IsDefined(operation) ||
            !IsLowerSha256(toolCallIdFingerprint) || !IsLowerSha256(requestFingerprint) ||
            !IsLowerSha256(resultSetFingerprint) || resultCount < 0 ||
            returnedUtf8Bytes < 0 || newAdditionalDocumentCount < 0 ||
            (state != Pass2RetrievalReceiptState.Accepted &&
                (resultCount != 0 || returnedUtf8Bytes != 0 || newAdditionalDocumentCount != 0)) ||
            createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The content-dark retrieval receipt is invalid.");
        }

        return new()
        {
            Id = id,
            Pass2JobExecutionId = pass2JobExecutionId,
            AttemptOrdinal = attemptOrdinal,
            CallOrdinal = callOrdinal,
            ToolCallIdFingerprint = toolCallIdFingerprint,
            State = state,
            Operation = operation,
            RequestFingerprint = requestFingerprint,
            ResultCount = resultCount,
            ReturnedUtf8Bytes = returnedUtf8Bytes,
            NewAdditionalDocumentCount = newAdditionalDocumentCount,
            ResultSetFingerprint = resultSetFingerprint,
            CreatedAt = createdAt,
        };
    }

    public Guid Id { get; private set; }
    public Guid Pass2JobExecutionId { get; private set; }
    public int AttemptOrdinal { get; private set; }
    public int CallOrdinal { get; private set; }
    public string ToolCallIdFingerprint { get; private set; } = string.Empty;
    public Pass2RetrievalReceiptState State { get; private set; }
    public DomainServices.Pass2CodeLogicRetrievalOperation Operation { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public int ResultCount { get; private set; }
    public int ReturnedUtf8Bytes { get; private set; }
    public int NewAdditionalDocumentCount { get; private set; }
    public string ResultSetFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private static bool IsLowerSha256(string value) =>
        value.Length == 64 && value.All(character =>
            char.IsAsciiHexDigit(character) && !char.IsAsciiLetterUpper(character));
}

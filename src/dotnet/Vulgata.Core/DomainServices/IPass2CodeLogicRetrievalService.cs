namespace Vulgata.Core.DomainServices;

public interface IPass2CodeLogicRetrievalService
{
    Task<Pass2CodeLogicRetrievalResult> RetrieveAsync(
        Guid pass2JobExecutionId,
        Pass2CodeLogicRetrievalRequest request,
        CancellationToken cancellationToken = default);
}

public enum Pass2CodeLogicRetrievalOperation
{
    Search,
    Sections,
    SourceAnchors,
    SourceExcerpt,
    RelatedDocuments,
}

public enum Pass2CodeLogicRetrievalOutcome
{
    Accepted,
    NoResults,
    BoundExceeded,
    Refused,
    Unavailable,
}

public enum Pass2RelatedDocumentDirection
{
    Outgoing,
    Incoming,
}

public sealed class Pass2CodeLogicRetrievalRequest
{
    public Pass2CodeLogicRetrievalRequest(
        Pass2CodeLogicRetrievalOperation operation,
        string? queryText = null,
        IReadOnlyList<string>? opaqueHandles = null,
        Pass2RelatedDocumentDirection? direction = null,
        int? containedByteOffset = null,
        int? containedByteLength = null)
    {
        Operation = operation;
        QueryText = queryText;
        OpaqueHandles = opaqueHandles ?? Array.Empty<string>();
        Direction = direction;
        ContainedByteOffset = containedByteOffset;
        ContainedByteLength = containedByteLength;
    }

    public Pass2CodeLogicRetrievalOperation Operation { get; }
    public string? QueryText { get; }
    public IReadOnlyList<string> OpaqueHandles { get; }
    public Pass2RelatedDocumentDirection? Direction { get; }
    public int? ContainedByteOffset { get; }
    public int? ContainedByteLength { get; }
}

public enum Pass2CodeLogicRetrievalItemKind
{
    Document,
    Section,
    SourceAnchor,
    SourceExcerpt,
    Relationship,
}

public sealed class Pass2CodeLogicRetrievalItem
{
    public Pass2CodeLogicRetrievalItem(
        string opaqueHandle,
        Pass2CodeLogicRetrievalItemKind kind,
        Guid documentVersionId,
        int returnedUtf8Bytes,
        Guid? sectionId = null,
        Guid? sourceAnchorId = null,
        Guid? edgeId = null,
        long? byteStart = null,
        long? byteEnd = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(opaqueHandle);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(documentVersionId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegative(returnedUtf8Bytes);
        if (sectionId == Guid.Empty || sourceAnchorId == Guid.Empty || edgeId == Guid.Empty ||
            byteStart < 0 || byteEnd <= byteStart)
        {
            throw new ArgumentException("Retrieval descriptor coordinates are invalid.");
        }

        bool coordinatesMatchKind = kind switch
        {
            Pass2CodeLogicRetrievalItemKind.Document =>
                sectionId is null && sourceAnchorId is null && edgeId is null &&
                byteStart is null && byteEnd is null,
            Pass2CodeLogicRetrievalItemKind.Section =>
                sectionId is not null && sourceAnchorId is null && edgeId is null &&
                byteStart is null && byteEnd is null,
            Pass2CodeLogicRetrievalItemKind.SourceAnchor =>
                sectionId is null && sourceAnchorId is not null && edgeId is null &&
                byteStart is null && byteEnd is null,
            Pass2CodeLogicRetrievalItemKind.SourceExcerpt =>
                sectionId is null && sourceAnchorId is not null && edgeId is null &&
                byteStart is not null && byteEnd is not null,
            Pass2CodeLogicRetrievalItemKind.Relationship =>
                sectionId is null && sourceAnchorId is null && edgeId is not null &&
                byteStart is null && byteEnd is null,
            _ => false,
        };
        if (!coordinatesMatchKind)
        {
            throw new ArgumentException("Retrieval descriptor coordinates do not match its kind.");
        }

        OpaqueHandle = opaqueHandle;
        Kind = kind;
        DocumentVersionId = documentVersionId;
        ReturnedUtf8Bytes = returnedUtf8Bytes;
        SectionId = sectionId;
        SourceAnchorId = sourceAnchorId;
        EdgeId = edgeId;
        ByteStart = byteStart;
        ByteEnd = byteEnd;
    }

    public string OpaqueHandle { get; }
    public Pass2CodeLogicRetrievalItemKind Kind { get; }
    public Guid DocumentVersionId { get; }
    public int ReturnedUtf8Bytes { get; }
    public Guid? SectionId { get; }
    public Guid? SourceAnchorId { get; }
    public Guid? EdgeId { get; }
    public long? ByteStart { get; }
    public long? ByteEnd { get; }
}

public sealed record Pass2CodeLogicRetrievalRemainingBudget(
    int Calls,
    int Results,
    int AdditionalDocuments,
    int SectionsPerDocument,
    int ReturnedUtf8Bytes);

public sealed class Pass2CodeLogicRetrievalResult
{
    private Pass2CodeLogicRetrievalResult(
        Pass2CodeLogicRetrievalOutcome outcome,
        string code,
        Pass2CodeLogicRetrievalRemainingBudget remainingBudget,
        IReadOnlyList<Pass2CodeLogicRetrievalItem> items)
    {
        Outcome = outcome;
        Code = code;
        RemainingBudget = remainingBudget;
        Items = items;
    }

    public Pass2CodeLogicRetrievalOutcome Outcome { get; }
    public string Code { get; }
    public Pass2CodeLogicRetrievalRemainingBudget RemainingBudget { get; }
    public IReadOnlyList<Pass2CodeLogicRetrievalItem> Items { get; }

    public static Pass2CodeLogicRetrievalResult Unavailable() =>
        new(
            Pass2CodeLogicRetrievalOutcome.Unavailable,
            "retrieval-unavailable",
            new(0, 0, 0, 0, 0),
            Array.Empty<Pass2CodeLogicRetrievalItem>());

    public static Pass2CodeLogicRetrievalResult Refused() =>
        new(
            Pass2CodeLogicRetrievalOutcome.Refused,
            "retrieval-refused",
            new(0, 0, 0, 0, 0),
            Array.Empty<Pass2CodeLogicRetrievalItem>());

    public static Pass2CodeLogicRetrievalResult Accepted(
        IReadOnlyList<Pass2CodeLogicRetrievalItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new(
            Pass2CodeLogicRetrievalOutcome.Accepted,
            "retrieval-accepted",
            new(0, 0, 0, 0, 0),
            Array.AsReadOnly(items.ToArray()));
    }

    public static Pass2CodeLogicRetrievalResult NoResults() =>
        new(
            Pass2CodeLogicRetrievalOutcome.NoResults,
            "retrieval-no-results",
            new(0, 0, 0, 0, 0),
            Array.Empty<Pass2CodeLogicRetrievalItem>());
}

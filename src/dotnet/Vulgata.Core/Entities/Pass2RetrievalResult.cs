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

    public static Pass2RetrievalResult CreateAdmitted(
        Guid id,
        Guid receiptId,
        Guid pass2JobExecutionId,
        int attemptOrdinal,
        int resultOrdinal,
        DomainServices.Pass2CodeLogicRetrievalItemKind kind,
        Guid documentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        Guid? sectionId,
        Guid? sourceAnchorId,
        Guid? edgeId,
        long? byteStart,
        long? byteEnd,
        string descriptorFingerprint,
        string evidenceFingerprint,
        int returnedUtf8Bytes,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || receiptId == Guid.Empty ||
            pass2JobExecutionId == Guid.Empty || documentVersionId == Guid.Empty ||
            repositoryId == Guid.Empty || scanRunId == Guid.Empty ||
            attemptOrdinal is < 1 or > 2 || resultOrdinal < 1 || !Enum.IsDefined(kind) ||
            !IsLowerSha256(descriptorFingerprint) || !IsLowerSha256(evidenceFingerprint) ||
            returnedUtf8Bytes < 0 || createdAt.Offset != TimeSpan.Zero ||
            !CoordinatesMatchKind(kind, sectionId, sourceAnchorId, edgeId, byteStart, byteEnd))
        {
            throw new ArgumentException("The content-dark retrieval result is invalid.");
        }

        return new()
        {
            Id = id,
            ReceiptId = receiptId,
            Pass2JobExecutionId = pass2JobExecutionId,
            AttemptOrdinal = attemptOrdinal,
            ResultOrdinal = resultOrdinal,
            State = Pass2RetrievalResultState.Admitted,
            Kind = kind,
            DocumentVersionId = documentVersionId,
            RepositoryId = repositoryId,
            ScanRunId = scanRunId,
            SectionId = sectionId,
            SourceAnchorId = sourceAnchorId,
            EdgeId = edgeId,
            ByteStart = byteStart,
            ByteEnd = byteEnd,
            DescriptorFingerprint = descriptorFingerprint,
            EvidenceFingerprint = evidenceFingerprint,
            ReturnedUtf8Bytes = returnedUtf8Bytes,
            CreatedAt = createdAt,
        };
    }

    public Guid Id { get; private set; }
    public Guid ReceiptId { get; private set; }
    public Guid Pass2JobExecutionId { get; private set; }
    public int AttemptOrdinal { get; private set; }
    public int ResultOrdinal { get; private set; }
    public Pass2RetrievalResultState State { get; private set; }
    public DomainServices.Pass2CodeLogicRetrievalItemKind Kind { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    private Guid? SectionId { get; set; }
    private Guid? SourceAnchorId { get; set; }
    public Guid? EdgeId { get; private set; }
    public long? ByteStart { get; private set; }
    public long? ByteEnd { get; private set; }
    public string DescriptorFingerprint { get; private set; } = string.Empty;
    public string EvidenceFingerprint { get; private set; } = string.Empty;
    public int ReturnedUtf8Bytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? ReadSectionId() => SectionId;

    public Guid? ReadSourceAnchorId() => SourceAnchorId;

    private static bool IsLowerSha256(string value) =>
        value.Length == 64 && value.All(character =>
            char.IsAsciiHexDigit(character) && !char.IsAsciiLetterUpper(character));

    private static bool CoordinatesMatchKind(
        DomainServices.Pass2CodeLogicRetrievalItemKind kind,
        Guid? sectionId,
        Guid? sourceAnchorId,
        Guid? edgeId,
        long? byteStart,
        long? byteEnd) => kind switch
        {
            DomainServices.Pass2CodeLogicRetrievalItemKind.Document =>
                sectionId is null && sourceAnchorId is null && edgeId is null &&
                byteStart is null && byteEnd is null,
            DomainServices.Pass2CodeLogicRetrievalItemKind.Section =>
                sectionId is not null && sourceAnchorId is null && edgeId is null &&
                byteStart is null && byteEnd is null,
            DomainServices.Pass2CodeLogicRetrievalItemKind.SourceAnchor =>
                sectionId is null && sourceAnchorId is not null && edgeId is null &&
                byteStart is null && byteEnd is null,
            DomainServices.Pass2CodeLogicRetrievalItemKind.SourceExcerpt =>
                sectionId is null && sourceAnchorId is not null && edgeId is null &&
                byteStart >= 0 && byteEnd > byteStart,
            DomainServices.Pass2CodeLogicRetrievalItemKind.Relationship =>
                sectionId is null && sourceAnchorId is null && edgeId is not null &&
                byteStart is null && byteEnd is null,
            _ => false,
        };
}

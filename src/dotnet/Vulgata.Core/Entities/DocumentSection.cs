namespace Vulgata.Core.Entities;

public sealed class DocumentSection
{
    private DocumentSection()
    {
    }

    private DocumentSection(
        Guid id,
        Guid logicalDocumentId,
        Guid documentVersionId,
        Guid repositoryId,
        Guid stableSectionId,
        int sectionOrder,
        DocumentSectionKind sectionKind,
        string heading,
        string contentMarkdown,
        string searchText,
        string sourceAnchorIdsJson,
        string documentVersionIdsJson,
        DateTimeOffset createdAt)
    {
        Id = id;
        LogicalDocumentId = logicalDocumentId;
        DocumentVersionId = documentVersionId;
        RepositoryId = repositoryId;
        StableSectionId = stableSectionId;
        SectionOrder = sectionOrder;
        SectionKind = sectionKind;
        Heading = heading;
        ContentMarkdown = contentMarkdown;
        SearchText = searchText;
        SourceAnchorIdsJson = sourceAnchorIdsJson;
        DocumentVersionIdsJson = documentVersionIdsJson;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid LogicalDocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid StableSectionId { get; private set; }
    public int SectionOrder { get; private set; }
    public DocumentSectionKind SectionKind { get; private set; }
    public string Heading { get; private set; } = string.Empty;
    public string ContentMarkdown { get; private set; } = string.Empty;
    public string SearchText { get; private set; } = string.Empty;
    public string SourceAnchorIdsJson { get; private set; } = string.Empty;
    public string DocumentVersionIdsJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DocumentVersion? DocumentVersion { get; private set; }
    public static DocumentSection Create(
        Guid id,
        Guid logicalDocumentId,
        Guid documentVersionId,
        Guid repositoryId,
        Guid stableSectionId,
        int sectionOrder,
        DocumentSectionKind sectionKind,
        string heading,
        string contentMarkdown,
        string searchText,
        string sourceAnchorIdsJson,
        string documentVersionIdsJson,
        DateTimeOffset createdAt) =>
        new(
            id,
            logicalDocumentId,
            documentVersionId,
            repositoryId,
            stableSectionId,
            sectionOrder,
            sectionKind,
            heading,
            contentMarkdown,
            searchText,
            sourceAnchorIdsJson,
            documentVersionIdsJson,
            createdAt);
}

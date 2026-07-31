namespace Vulgata.Core.Entities;

public sealed class IndexEntry
{
    private IndexEntry()
    {
    }

    private IndexEntry(
        Guid id,
        Guid logicalDocumentId,
        Guid documentVersionId,
        Guid repositoryId,
        Guid? systemId,
        DocumentType documentType,
        string title,
        string summary,
        string repositoryRelativePath,
        string keySymbolsJson,
        string businessTermsJson,
        string technicalTermsJson,
        string searchText,
        DateTimeOffset createdAt)
    {
        Id = id;
        LogicalDocumentId = logicalDocumentId;
        DocumentVersionId = documentVersionId;
        RepositoryId = repositoryId;
        SystemId = systemId;
        DocumentType = documentType;
        Title = title;
        Summary = summary;
        RepositoryRelativePath = repositoryRelativePath;
        KeySymbolsJson = keySymbolsJson;
        BusinessTermsJson = businessTermsJson;
        TechnicalTermsJson = technicalTermsJson;
        SearchText = searchText;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid LogicalDocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid? SystemId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string RepositoryRelativePath { get; private set; } = string.Empty;
    public string KeySymbolsJson { get; private set; } = string.Empty;
    public string BusinessTermsJson { get; private set; } = string.Empty;
    public string TechnicalTermsJson { get; private set; } = string.Empty;
    public string SearchText { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DocumentVersion? DocumentVersion { get; private set; }
    public static IndexEntry Create(
        Guid id,
        Guid logicalDocumentId,
        Guid documentVersionId,
        Guid repositoryId,
        Guid? systemId,
        DocumentType documentType,
        string title,
        string summary,
        string repositoryRelativePath,
        string keySymbolsJson,
        string businessTermsJson,
        string technicalTermsJson,
        string searchText,
        DateTimeOffset createdAt) =>
        new(
            id,
            logicalDocumentId,
            documentVersionId,
            repositoryId,
            systemId,
            documentType,
            title,
            summary,
            repositoryRelativePath,
            keySymbolsJson,
            businessTermsJson,
            technicalTermsJson,
            searchText,
            createdAt);
}

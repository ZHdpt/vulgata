namespace Vulgata.Core.Entities;

public sealed class DocumentVersion
{
    private DocumentVersion()
    {
    }

    private DocumentVersion(
        Guid id,
        Guid logicalDocumentId,
        Guid repositoryId,
        Guid? systemId,
        Guid scanRunId,
        string profiledRevision,
        int versionNumber,
        string title,
        string repositoryRelativePath,
        string schemaIdentity,
        string contentJson,
        string contentMarkdown,
        string contentFingerprint,
        string commitFingerprint,
        DateTimeOffset createdAt)
    {
        Id = id;
        LogicalDocumentId = logicalDocumentId;
        RepositoryId = repositoryId;
        SystemId = systemId;
        ScanRunId = scanRunId;
        ProfiledRevision = profiledRevision;
        VersionNumber = versionNumber;
        Title = title;
        RepositoryRelativePath = repositoryRelativePath;
        SchemaIdentity = schemaIdentity;
        ContentJson = contentJson;
        ContentMarkdown = contentMarkdown;
        ContentFingerprint = contentFingerprint;
        CommitFingerprint = commitFingerprint;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid LogicalDocumentId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid? SystemId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public string ProfiledRevision { get; private set; } = string.Empty;
    public int VersionNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string RepositoryRelativePath { get; private set; } = string.Empty;
    public string SchemaIdentity { get; private set; } = string.Empty;
    public string ContentJson { get; private set; } = string.Empty;
    public string ContentMarkdown { get; private set; } = string.Empty;
    public string ContentFingerprint { get; private set; } = string.Empty;
    public string CommitFingerprint { get; private set; } = string.Empty;
    public Guid? SupersededById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public LogicalDocument? LogicalDocument { get; private set; }
    public ScanRun? ScanRun { get; private set; }
    public DocumentVersion? SupersededBy { get; private set; }
    public ICollection<SourceAnchor> SourceAnchors { get; } = [];
    public ICollection<FlowGroup> FlowGroups { get; } = [];
    public ICollection<DocumentSection> Sections { get; } = [];
    public IndexEntry? IndexEntry { get; private set; }

    public static DocumentVersion Create(
        Guid id,
        Guid logicalDocumentId,
        Guid repositoryId,
        Guid? systemId,
        Guid scanRunId,
        string profiledRevision,
        int versionNumber,
        string title,
        string repositoryRelativePath,
        string schemaIdentity,
        string contentJson,
        string contentMarkdown,
        string contentFingerprint,
        string commitFingerprint,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty ||
            logicalDocumentId == Guid.Empty ||
            repositoryId == Guid.Empty ||
            scanRunId == Guid.Empty ||
            systemId == Guid.Empty ||
            versionNumber < 1)
        {
            throw new ArgumentException("Document Version identity is invalid.");
        }

        return new(
            id,
            logicalDocumentId,
            repositoryId,
            systemId,
            scanRunId,
            profiledRevision,
            versionNumber,
            title,
            repositoryRelativePath,
            schemaIdentity,
            contentJson,
            contentMarkdown,
            contentFingerprint,
            commitFingerprint,
            createdAt);
    }

    public void SupersedeWith(Guid successorId)
    {
        if (successorId == Guid.Empty || successorId == Id || SupersededById is not null)
        {
            throw new InvalidOperationException("The Version Chain transition is invalid.");
        }

        SupersededById = successorId;
    }
}

namespace Vulgata.Core.Entities;

public sealed class LogicalDocument
{
    private readonly List<DocumentVersion> _versions = [];

    private LogicalDocument()
    {
    }

    private LogicalDocument(
        Guid id,
        Guid repositoryId,
        DocumentType documentType,
        string stableKey,
        DateTimeOffset createdAt)
    {
        Id = id;
        RepositoryId = repositoryId;
        DocumentType = documentType;
        StableKey = stableKey;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid RepositoryId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string StableKey { get; private set; } = string.Empty;
    public Guid? CurrentVersionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Repository? Repository { get; private set; }
    public DocumentVersion? CurrentVersion { get; private set; }
    public IReadOnlyCollection<DocumentVersion> Versions => _versions;

    public static LogicalDocument Create(
        Guid id,
        Guid repositoryId,
        DocumentType documentType,
        string stableKey,
        DateTimeOffset createdAt)
    {
        RequireId(id);
        RequireId(repositoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stableKey);
        return new(id, repositoryId, documentType, stableKey, createdAt);
    }

    public void SetInitialCurrentVersion(DocumentVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (CurrentVersionId is not null ||
            version.LogicalDocumentId != Id ||
            version.RepositoryId != RepositoryId)
        {
            throw new InvalidOperationException("The initial Document Version owner is invalid.");
        }

        _versions.Add(version);
        CurrentVersion = version;
        CurrentVersionId = version.Id;
    }

    public void MoveCurrentVersion(Guid expectedVersionId, DocumentVersion successor)
    {
        ArgumentNullException.ThrowIfNull(successor);
        if (CurrentVersionId != expectedVersionId ||
            successor.LogicalDocumentId != Id ||
            successor.RepositoryId != RepositoryId)
        {
            throw new InvalidOperationException("The current Document Version changed.");
        }

        CurrentVersion?.SupersedeWith(successor.Id);
        _versions.Add(successor);
        CurrentVersion = successor;
        CurrentVersionId = successor.Id;
    }

    private static void RequireId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", nameof(id));
        }
    }
}

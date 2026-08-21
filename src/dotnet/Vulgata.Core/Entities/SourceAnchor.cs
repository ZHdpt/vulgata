namespace Vulgata.Core.Entities;

public sealed class SourceAnchor
{
    private SourceAnchor()
    {
    }

    public SourceAnchor(
        Guid id,
        Guid documentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        Guid codeUnitId,
        string profiledRevision,
        string repositoryRelativePath,
        string evidenceLocator,
        int lineStart,
        int lineEnd,
        string sourceHash,
        DateTimeOffset createdAt)
        : this(
            id,
            documentVersionId,
            repositoryId,
            scanRunId,
            codeUnitId,
            profiledRevision,
            repositoryRelativePath,
            evidenceLocator,
            lineStart,
            lineEnd,
            sourceHash,
            null,
            null,
            createdAt)
    {
    }

    public SourceAnchor(
        Guid id,
        Guid documentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        Guid codeUnitId,
        string profiledRevision,
        string repositoryRelativePath,
        string evidenceLocator,
        int lineStart,
        int lineEnd,
        string sourceHash,
        long? byteStart,
        long? byteEnd,
        DateTimeOffset createdAt)
    {
        Id = id;
        DocumentVersionId = documentVersionId;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        CodeUnitId = codeUnitId;
        ProfiledRevision = profiledRevision;
        RepositoryRelativePath = repositoryRelativePath;
        EvidenceLocator = evidenceLocator;
        LineStart = lineStart;
        LineEnd = lineEnd;
        SourceHash = sourceHash;
        ByteStart = byteStart;
        ByteEnd = byteEnd;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public Guid CodeUnitId { get; private set; }
    public string ProfiledRevision { get; private set; } = string.Empty;
    public string RepositoryRelativePath { get; private set; } = string.Empty;
    public string EvidenceLocator { get; private set; } = string.Empty;
    public int LineStart { get; private set; }
    public int LineEnd { get; private set; }
    public string SourceHash { get; private set; } = string.Empty;
    public long? ByteStart { get; private set; }
    public long? ByteEnd { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DocumentVersion? DocumentVersion { get; private set; }
    public CodeUnit? CodeUnit { get; private set; }
    public ScanRun? ScanRun { get; private set; }
}

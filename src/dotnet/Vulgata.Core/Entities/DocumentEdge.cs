namespace Vulgata.Core.Entities;

public sealed class DocumentEdge
{
    private DocumentEdge()
    {
    }

    public DocumentEdge(
        Guid id,
        Guid sourceDocumentVersionId,
        Guid sourceRepositoryId,
        Guid sourceScanRunId,
        Guid targetDocumentVersionId,
        Guid targetRepositoryId,
        Guid targetScanRunId,
        DocumentEdgeType edgeType,
        DateTimeOffset createdAt)
    {
        Id = id;
        SourceDocumentVersionId = sourceDocumentVersionId;
        SourceRepositoryId = sourceRepositoryId;
        SourceScanRunId = sourceScanRunId;
        TargetDocumentVersionId = targetDocumentVersionId;
        TargetRepositoryId = targetRepositoryId;
        TargetScanRunId = targetScanRunId;
        EdgeType = edgeType;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid SourceDocumentVersionId { get; private set; }
    public Guid SourceRepositoryId { get; private set; }
    public Guid SourceScanRunId { get; private set; }
    public Guid TargetDocumentVersionId { get; private set; }
    public Guid TargetRepositoryId { get; private set; }
    public Guid TargetScanRunId { get; private set; }
    public DocumentEdgeType EdgeType { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DocumentVersion? SourceDocumentVersion { get; private set; }
    public DocumentVersion? TargetDocumentVersion { get; private set; }
}

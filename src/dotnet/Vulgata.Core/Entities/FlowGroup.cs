namespace Vulgata.Core.Entities;

public sealed class FlowGroup
{
    private FlowGroup()
    {
    }

    public FlowGroup(
        Guid id,
        Guid businessLogicDocumentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        int groupOrder,
        string title,
        DateTimeOffset createdAt)
    {
        Id = id;
        BusinessLogicDocumentVersionId = businessLogicDocumentVersionId;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        GroupOrder = groupOrder;
        Title = title;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid BusinessLogicDocumentVersionId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public int GroupOrder { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DocumentVersion? BusinessLogicDocumentVersion { get; private set; }
    public ICollection<FlowTrace> Traces { get; } = [];
}

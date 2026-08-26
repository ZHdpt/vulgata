namespace Vulgata.Core.Entities;

public sealed class FlowTraceStep
{
    private FlowTraceStep()
    {
    }

    public FlowTraceStep(
        Guid id,
        Guid flowTraceId,
        int stepOrder,
        Guid documentVersionId,
        Guid documentRepositoryId,
        Guid documentScanRunId,
        Guid? edgeId,
        FlowTraceStepKind stepKind,
        string description,
        DateTimeOffset createdAt,
        Guid? exactResolutionDecisionId = null)
    {
        Id = id;
        FlowTraceId = flowTraceId;
        StepOrder = stepOrder;
        DocumentVersionId = documentVersionId;
        DocumentRepositoryId = documentRepositoryId;
        DocumentScanRunId = documentScanRunId;
        EdgeId = edgeId;
        ExactResolutionDecisionId = exactResolutionDecisionId;
        StepKind = stepKind;
        Description = description;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid FlowTraceId { get; private set; }
    public int StepOrder { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public Guid DocumentRepositoryId { get; private set; }
    public Guid DocumentScanRunId { get; private set; }
    public Guid? EdgeId { get; private set; }
    public Guid? ExactResolutionDecisionId { get; private set; }
    public FlowTraceStepKind StepKind { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public FlowTrace? FlowTrace { get; private set; }
    public DocumentVersion? DocumentVersion { get; private set; }
    public DocumentEdge? Edge { get; private set; }
}

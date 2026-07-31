namespace Vulgata.Core.Entities;

public sealed class FlowTrace
{
    private FlowTrace()
    {
    }

    public FlowTrace(Guid id, Guid flowGroupId, int traceOrder, DateTimeOffset createdAt)
    {
        Id = id;
        FlowGroupId = flowGroupId;
        TraceOrder = traceOrder;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid FlowGroupId { get; private set; }
    public int TraceOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public FlowGroup? FlowGroup { get; private set; }
    public ICollection<FlowTraceStep> Steps { get; } = [];
}

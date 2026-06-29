namespace Vulgata.Core.Entities;

public sealed class SystemLlmProviderOverride
{
    private SystemLlmProviderOverride()
    {
    }

    public Guid Id { get; private set; }

    public Guid SystemId { get; private set; }

    public Guid LlmProviderId { get; private set; }

    public AgentType AgentType { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public System System { get; private set; } = null!;

    public LlmProvider LlmProvider { get; private set; } = null!;

    public static SystemLlmProviderOverride Create(
        Guid systemId,
        Guid llmProviderId,
        AgentType agentType,
        DateTimeOffset now)
    {
        if (systemId == Guid.Empty)
        {
            throw new ArgumentException("系统标识不能为空。", nameof(systemId));
        }

        if (llmProviderId == Guid.Empty)
        {
            throw new ArgumentException("LLM 提供商标识不能为空。", nameof(llmProviderId));
        }

        return new SystemLlmProviderOverride
        {
            Id = Guid.NewGuid(),
            SystemId = systemId,
            LlmProviderId = llmProviderId,
            AgentType = agentType,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(Guid llmProviderId, AgentType agentType, DateTimeOffset now)
    {
        if (llmProviderId == Guid.Empty)
        {
            throw new ArgumentException("LLM 提供商标识不能为空。", nameof(llmProviderId));
        }

        LlmProviderId = llmProviderId;
        AgentType = agentType;
        UpdatedAt = now;
    }
}

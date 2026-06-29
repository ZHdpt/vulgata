using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface ISystemLlmProviderOverrideRepository
{
    Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(Guid systemId, CancellationToken cancellationToken = default);

    Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(
        Guid systemId,
        Guid overrideId,
        CancellationToken cancellationToken = default);

    Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(
        Guid systemId,
        AgentType agentType,
        CancellationToken cancellationToken = default);

    Task<SystemLlmProviderOverride> AddAsync(
        SystemLlmProviderOverride overrideEntry,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid systemId, Guid overrideId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

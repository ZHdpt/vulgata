using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface ILlmProviderRepository
{
    Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<LlmProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeProviderId = null, CancellationToken cancellationToken = default);

    Task<LlmProvider> AddAsync(LlmProvider provider, CancellationToken cancellationToken = default);

    void Remove(LlmProvider provider);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

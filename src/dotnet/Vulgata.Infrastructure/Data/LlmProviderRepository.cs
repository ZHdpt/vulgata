using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class LlmProviderRepository(VulgataDbContext dbContext) : ILlmProviderRepository
{
    public async Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.LlmProviders
            .AsNoTracking()
            .OrderBy(provider => provider.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<LlmProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.LlmProviders
            .FirstOrDefaultAsync(provider => provider.Id == id, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(
        string name,
        Guid? excludeProviderId = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = LlmProvider.NormalizeName(name);

        IQueryable<LlmProvider> query = dbContext.LlmProviders
            .AsNoTracking()
            .Where(provider => provider.NormalizedName == normalizedName);

        if (excludeProviderId.HasValue)
        {
            query = query.Where(provider => provider.Id != excludeProviderId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<LlmProvider> AddAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<LlmProvider> entry =
            await dbContext.LlmProviders.AddAsync(provider, cancellationToken);
        return entry.Entity;
    }

    public void Remove(LlmProvider provider)
    {
        dbContext.LlmProviders.Remove(provider);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class SystemLlmProviderOverrideRepository(VulgataDbContext dbContext) : ISystemLlmProviderOverrideRepository
{
    public async Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.SystemLlmProviderOverrides
            .AsNoTracking()
            .Include(overrideEntry => overrideEntry.LlmProvider)
            .Where(overrideEntry => overrideEntry.SystemId == systemId)
            .OrderBy(overrideEntry => overrideEntry.AgentType)
            .ToListAsync(cancellationToken);
    }

    public async Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(
        Guid systemId,
        Guid overrideId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.SystemLlmProviderOverrides
            .Include(overrideEntry => overrideEntry.LlmProvider)
            .FirstOrDefaultAsync(
                overrideEntry => overrideEntry.SystemId == systemId && overrideEntry.Id == overrideId,
                cancellationToken);
    }

    public async Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(
        Guid systemId,
        AgentType agentType,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.SystemLlmProviderOverrides
            .Include(overrideEntry => overrideEntry.LlmProvider)
            .FirstOrDefaultAsync(
                overrideEntry => overrideEntry.SystemId == systemId && overrideEntry.AgentType == agentType,
                cancellationToken);
    }

    public async Task<SystemLlmProviderOverride> AddAsync(
        SystemLlmProviderOverride overrideEntry,
        CancellationToken cancellationToken = default)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<SystemLlmProviderOverride> entry =
            await dbContext.SystemLlmProviderOverrides.AddAsync(overrideEntry, cancellationToken);
        return entry.Entity;
    }

    public async Task<bool> RemoveAsync(Guid systemId, Guid overrideId, CancellationToken cancellationToken = default)
    {
        SystemLlmProviderOverride? overrideEntry = await dbContext.SystemLlmProviderOverrides
            .FirstOrDefaultAsync(
                item => item.SystemId == systemId && item.Id == overrideId,
                cancellationToken);

        if (overrideEntry is null)
        {
            return false;
        }

        dbContext.SystemLlmProviderOverrides.Remove(overrideEntry);
        return true;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

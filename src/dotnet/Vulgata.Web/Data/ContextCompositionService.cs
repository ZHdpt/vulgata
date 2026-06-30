using Microsoft.EntityFrameworkCore;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Web.Data;

public sealed class ContextCompositionService(IDbContextFactory<VulgataDbContext> dbContextFactory)
    : Core.DomainServices.IContextCompositionService
{
    public async Task<string?> ComposeEffectiveContextAsync(
        Guid? systemId,
        Guid repositoryId,
        CancellationToken cancellationToken = default)
    {
        await using VulgataDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // 1. Global context
        Core.Entities.GlobalContext? global = await db.GlobalContexts
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        // 2. System context (skip for standalone repositories)
        string? systemContext = null;
        if (systemId.HasValue)
        {
            systemContext = await db.Systems
                .Where(s => s.Id == systemId.Value)
                .Select(s => s.Context)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // 3. Repository context
        string? repositoryContext = await db.Repositories
            .Where(r => r.Id == repositoryId)
            .Select(r => r.Context)
            .FirstOrDefaultAsync(cancellationToken);

        return ComposeOrdered(global?.Context, systemContext, repositoryContext, systemId.HasValue);
    }

    private static string? ComposeOrdered(
        string? globalContext,
        string? systemContext,
        string? repositoryContext,
        bool hasSystem)
    {
        List<string> parts = [];

        if (!string.IsNullOrWhiteSpace(globalContext))
            parts.Add(globalContext);

        if (hasSystem && !string.IsNullOrWhiteSpace(systemContext))
            parts.Add(systemContext);

        if (!string.IsNullOrWhiteSpace(repositoryContext))
            parts.Add(repositoryContext);

        return parts.Count > 0 ? string.Join("\n\n", parts) : null;
    }
}

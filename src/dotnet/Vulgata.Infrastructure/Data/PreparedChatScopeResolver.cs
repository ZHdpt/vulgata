using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedChatScopeResolver(VulgataDbContext context) : IPreparedChatScopeResolver
{
    public async Task<PreparedChatScopeBinding?> ResolveAsync(
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repositoryIds);
        ArgumentNullException.ThrowIfNull(accessScope);
        Guid[] requestedIds = repositoryIds.Distinct().Order().ToArray();
        if (requestedIds.Length != 2 ||
            requestedIds.Any(repositoryId => !accessScope.Contains(repositoryId)))
        {
            return null;
        }

        int repositoryCount = await context.Repositories.AsNoTracking().CountAsync(
            repository => requestedIds.Contains(repository.Id) && repository.SystemId == systemId,
            cancellationToken);
        if (repositoryCount != 2)
        {
            return null;
        }

        return new PreparedChatScopeBinding(
            systemId,
            requestedIds[0],
            requestedIds[1]);
    }
}

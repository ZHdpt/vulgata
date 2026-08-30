using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

internal sealed record PreparedScanInitiationAuthorizationResult(
    bool IsAuthorized,
    Guid UserId,
    string? EffectiveAuthorizationMode,
    IReadOnlyList<Guid> RepositoryIds)
{
    internal static PreparedScanInitiationAuthorizationResult Denied { get; } =
        new(false, Guid.Empty, null, []);
}

internal interface IPreparedScanInitiationAuthorizationRechecker
{
    Task<PreparedScanInitiationAuthorizationResult> RecheckAsync(
        string userId,
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        CancellationToken cancellationToken = default);
}

internal sealed class PreparedScanInitiationAuthorizationRechecker(
    IServiceScopeFactory scopeFactory) : IPreparedScanInitiationAuthorizationRechecker
{
    public async Task<PreparedScanInitiationAuthorizationResult> RecheckAsync(
        string userId,
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repositoryIds);
        Guid[] canonicalRepositoryIds = repositoryIds
            .Distinct()
            .Order()
            .ToArray();
        if (!Guid.TryParse(userId, out Guid authorizationUserId) ||
            authorizationUserId == Guid.Empty ||
            systemId == Guid.Empty ||
            canonicalRepositoryIds.Length != 2 ||
            canonicalRepositoryIds.Any(value => value == Guid.Empty))
        {
            return PreparedScanInitiationAuthorizationResult.Denied;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        VulgataDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<VulgataDbContext>();

        bool systemExists = await dbContext.Systems
            .AsNoTracking()
            .AnyAsync(value => value.Id == systemId, cancellationToken);
        int repositoryCount = await dbContext.Repositories
            .AsNoTracking()
            .CountAsync(
                value => value.SystemId == systemId &&
                    canonicalRepositoryIds.Contains(value.Id),
                cancellationToken);
        if (!systemExists || repositoryCount != canonicalRepositoryIds.Length)
        {
            return PreparedScanInitiationAuthorizationResult.Denied;
        }

        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return PreparedScanInitiationAuthorizationResult.Denied;
        }

        if (await userManager.IsInRoleAsync(user, RoleNames.Administrator))
        {
            return new(
                true,
                authorizationUserId,
                RoleNames.Administrator,
                canonicalRepositoryIds);
        }

        bool isSystemOwner = await userManager.IsInRoleAsync(user, RoleNames.SystemOwner);
        bool hasAssignment = isSystemOwner &&
            await dbContext.SystemOwnerAssignments
                .AsNoTracking()
                .AnyAsync(
                    value => value.SystemId == systemId && value.UserId == userId,
                    cancellationToken);
        return hasAssignment
            ? new PreparedScanInitiationAuthorizationResult(
                true,
                authorizationUserId,
                RoleNames.SystemOwner,
                canonicalRepositoryIds)
            : PreparedScanInitiationAuthorizationResult.Denied;
    }
}

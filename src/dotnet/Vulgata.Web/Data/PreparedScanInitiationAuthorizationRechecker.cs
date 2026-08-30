using Microsoft.AspNetCore.Identity;
using Vulgata.Core.DomainServices;
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
        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return PreparedScanInitiationAuthorizationResult.Denied;
        }

        bool isAdministrator =
            await userManager.IsInRoleAsync(user, RoleNames.Administrator);
        bool isSystemOwner = !isAdministrator &&
            await userManager.IsInRoleAsync(user, RoleNames.SystemOwner);
        if (!isAdministrator && !isSystemOwner)
        {
            return PreparedScanInitiationAuthorizationResult.Denied;
        }

        ISystemRepository systemRepository =
            scope.ServiceProvider.GetRequiredService<ISystemRepository>();
        IRepositoryRepository repositoryRepository =
            scope.ServiceProvider.GetRequiredService<IRepositoryRepository>();
        SystemOwnerFilter filter = isAdministrator
            ? SystemOwnerFilter.AllSystems()
            : SystemOwnerFilter.OwnedBy(userId);
        Vulgata.Core.Entities.System? system =
            await systemRepository.GetVisibleByIdAsync(
                systemId,
                filter,
                cancellationToken);
        if (system?.Id != systemId)
        {
            return PreparedScanInitiationAuthorizationResult.Denied;
        }

        foreach (Guid repositoryId in canonicalRepositoryIds)
        {
            Vulgata.Core.Entities.Repository? repository =
                await repositoryRepository.GetBySystemAndIdAsync(
                    systemId,
                    repositoryId,
                    cancellationToken);
            if (repository?.Id != repositoryId || repository.SystemId != systemId)
            {
                return PreparedScanInitiationAuthorizationResult.Denied;
            }
        }

        if (isAdministrator)
        {
            return new(
                true,
                authorizationUserId,
                RoleNames.Administrator,
                canonicalRepositoryIds);
        }

        return new PreparedScanInitiationAuthorizationResult(
            true,
            authorizationUserId,
            RoleNames.SystemOwner,
            canonicalRepositoryIds);
    }
}

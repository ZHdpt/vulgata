using Microsoft.AspNetCore.Identity;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

public interface IAdministratorRoleCoordinator
{
    Task<IdentityResult> AssignInitialRoleAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    Task<AdministratorRoleRemovalResult> RemoveAdministratorAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}

public enum AdministratorRoleRemovalOutcome
{
    Success,
    NotAdministrator,
    LastAdministratorBlocked,
    Failed,
}

public sealed record AdministratorRoleRemovalResult(AdministratorRoleRemovalOutcome Outcome, IdentityResult? IdentityResult = null)
{
    public static AdministratorRoleRemovalResult Success { get; } = new(AdministratorRoleRemovalOutcome.Success);

    public static AdministratorRoleRemovalResult NotAdministrator { get; } = new(AdministratorRoleRemovalOutcome.NotAdministrator);

    public static AdministratorRoleRemovalResult LastAdministratorBlocked { get; } = new(AdministratorRoleRemovalOutcome.LastAdministratorBlocked);

    public static AdministratorRoleRemovalResult Failed(IdentityResult result) => new(AdministratorRoleRemovalOutcome.Failed, result);
}

public sealed class AdministratorRoleCoordinator(UserManager<ApplicationUser> userManager) : IAdministratorRoleCoordinator
{
    private static readonly SemaphoreSlim RoleMutationLock = new(1, 1);

    public async Task<IdentityResult> AssignInitialRoleAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        await RoleMutationLock.WaitAsync(cancellationToken);
        try
        {
            IList<ApplicationUser> administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
            string targetRole = administrators.Count == 0
                ? RoleNames.Administrator
                : RoleNames.User;

            if (await userManager.IsInRoleAsync(user, targetRole))
            {
                return IdentityResult.Success;
            }

            return await userManager.AddToRoleAsync(user, targetRole);
        }
        finally
        {
            RoleMutationLock.Release();
        }
    }

    public async Task<AdministratorRoleRemovalResult> RemoveAdministratorAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        await RoleMutationLock.WaitAsync(cancellationToken);
        try
        {
            if (!await userManager.IsInRoleAsync(user, RoleNames.Administrator))
            {
                return AdministratorRoleRemovalResult.NotAdministrator;
            }

            IList<ApplicationUser> administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
            if (administrators.Count <= 1)
            {
                return AdministratorRoleRemovalResult.LastAdministratorBlocked;
            }

            IdentityResult removeResult = await userManager.RemoveFromRoleAsync(user, RoleNames.Administrator);
            if (!removeResult.Succeeded)
            {
                return AdministratorRoleRemovalResult.Failed(removeResult);
            }

            bool hasSystemOwner = await userManager.IsInRoleAsync(user, RoleNames.SystemOwner);
            bool hasUserRole = await userManager.IsInRoleAsync(user, RoleNames.User);
            if (!hasSystemOwner && !hasUserRole)
            {
                IdentityResult addUserRoleResult = await userManager.AddToRoleAsync(user, RoleNames.User);
                if (!addUserRoleResult.Succeeded)
                {
                    return AdministratorRoleRemovalResult.Failed(addUserRoleResult);
                }
            }

            return AdministratorRoleRemovalResult.Success;
        }
        finally
        {
            RoleMutationLock.Release();
        }
    }
}
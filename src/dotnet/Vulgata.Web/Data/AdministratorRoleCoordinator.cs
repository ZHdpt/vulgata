using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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

public sealed class AdministratorRoleCoordinator(UserManager<ApplicationUser> userManager, ILogger<AdministratorRoleCoordinator> logger) : IAdministratorRoleCoordinator
{
    private static readonly SemaphoreSlim _roleMutationLock = new(1, 1);

    public async Task<IdentityResult> AssignInitialRoleAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        await _roleMutationLock.WaitAsync(cancellationToken);
        try
        {
            IList<ApplicationUser> administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
            string targetRole = administrators.Count == 0
                ? RoleNames.Administrator
                : RoleNames.User;

            logger.LogInformation(
                "Assigning initial role to user {UserId} ({Email}). Found {AdminCount} existing administrator(s). Target role: {TargetRole}",
                user.Id, user.Email, administrators.Count, targetRole);

            if (await userManager.IsInRoleAsync(user, targetRole))
            {
                logger.LogInformation("User {UserId} already in role {TargetRole}, skipping assignment", user.Id, targetRole);
                return IdentityResult.Success;
            }

            IdentityResult addResult = await userManager.AddToRoleAsync(user, targetRole);
            if (addResult.Succeeded)
            {
                logger.LogInformation("Successfully assigned role {TargetRole} to user {UserId}", targetRole, user.Id);
            }
            else
            {
                logger.LogError("Failed to assign role {TargetRole} to user {UserId}: {Errors}",
                    targetRole, user.Id, string.Join(", ", addResult.Errors.Select(e => e.Description)));
            }

            return addResult;
        }
        finally
        {
            _roleMutationLock.Release();
        }
    }

    public async Task<AdministratorRoleRemovalResult> RemoveAdministratorAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        await _roleMutationLock.WaitAsync(cancellationToken);
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
            _roleMutationLock.Release();
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

public sealed class AdministratorOnlyRequirement : IAuthorizationRequirement
{
}

public sealed class AdministratorOnlyHandler(UserManager<ApplicationUser> userManager)
    : AuthorizationHandler<AdministratorOnlyRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorOnlyRequirement requirement)
    {
        ApplicationUser? user = await GetCurrentUserAsync(context);
        if (user is null)
        {
            return;
        }

        if (await userManager.IsInRoleAsync(user, RoleNames.Administrator))
        {
            context.Succeed(requirement);
        }
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync(AuthorizationHandlerContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        string? userId = userManager.GetUserId(context.User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await userManager.FindByIdAsync(userId);
    }
}

public sealed class ManagementAccessRequirement : IAuthorizationRequirement
{
}

public sealed class ManagementAccessHandler(UserManager<ApplicationUser> userManager)
    : AuthorizationHandler<ManagementAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ManagementAccessRequirement requirement)
    {
        ApplicationUser? user = await GetCurrentUserAsync(context);
        if (user is null)
        {
            return;
        }

        if (await userManager.IsInRoleAsync(user, RoleNames.Administrator)
            || await userManager.IsInRoleAsync(user, RoleNames.SystemOwner))
        {
            context.Succeed(requirement);
        }
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync(AuthorizationHandlerContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        string? userId = userManager.GetUserId(context.User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await userManager.FindByIdAsync(userId);
    }
}

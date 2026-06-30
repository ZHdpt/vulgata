using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

public sealed class AdministratorOnlyRequirement : IAuthorizationRequirement
{
}

public sealed class AdministratorOnlyHandler(IServiceScopeFactory scopeFactory)
    : AuthorizationHandler<AdministratorOnlyRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorOnlyRequirement requirement)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await GetCurrentUserAsync(context, userManager);
        if (user is null)
        {
            return;
        }

        if (await userManager.IsInRoleAsync(user, RoleNames.Administrator))
        {
            context.Succeed(requirement);
        }
    }

    private static async Task<ApplicationUser?> GetCurrentUserAsync(AuthorizationHandlerContext context, UserManager<ApplicationUser> userManager)
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

public sealed class ManagementAccessHandler(IServiceScopeFactory scopeFactory)
    : AuthorizationHandler<ManagementAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ManagementAccessRequirement requirement)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await GetCurrentUserAsync(context, userManager);
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

    private static async Task<ApplicationUser?> GetCurrentUserAsync(AuthorizationHandlerContext context, UserManager<ApplicationUser> userManager)
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

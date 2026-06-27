using Microsoft.AspNetCore.Authorization;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

public sealed class ManagementAccessRequirement : IAuthorizationRequirement
{
}

public sealed class ManagementAccessHandler : AuthorizationHandler<ManagementAccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ManagementAccessRequirement requirement)
    {
        if (context.User.IsInRole(RoleNames.Administrator) || context.User.IsInRole(RoleNames.SystemOwner))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

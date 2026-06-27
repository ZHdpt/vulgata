using Microsoft.AspNetCore.Identity;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

public sealed class RoleSeeder(RoleManager<IdentityRole> roleManager, ILogger<RoleSeeder> logger)
{
    public async Task SeedAsync()
    {
        foreach (string roleName in RoleNames.SeededRoles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                logger.LogInformation("Role {RoleName} already exists. Skipping role creation.", roleName);
                continue;
            }

            IdentityResult result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                string errorCodes = string.Join(", ", result.Errors.Select(error => error.Code));
                logger.LogError("Failed to create role {RoleName}. Error codes: {ErrorCodes}", roleName, errorCodes);
                throw new InvalidOperationException($"Failed to seed role '{roleName}'.");
            }

            logger.LogInformation("Role {RoleName} created successfully.", roleName);
        }
    }
}

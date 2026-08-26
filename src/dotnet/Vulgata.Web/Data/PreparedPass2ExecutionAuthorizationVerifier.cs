using Microsoft.AspNetCore.Identity;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

public sealed class PreparedPass2ExecutionAuthorizationVerifier(
    UserManager<ApplicationUser> userManager,
    IPass2AuthorizationDataReader authorityReader) : IPass2ExecutionAuthorizationVerifier
{
    public async ValueTask<bool> HasCurrentAccessAsync(
        string authorizationSubject,
        Guid systemId,
        IReadOnlyCollection<Guid> requiredRepositoryIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requiredRepositoryIds);
        if (string.IsNullOrWhiteSpace(authorizationSubject) ||
            systemId == Guid.Empty ||
            requiredRepositoryIds.Count != 2 ||
            requiredRepositoryIds.Any(value => value == Guid.Empty) ||
            requiredRepositoryIds.Distinct().Count() != requiredRepositoryIds.Count)
        {
            return false;
        }

        if (!await authorityReader.RepositoriesBelongToSystemAsync(
                systemId, requiredRepositoryIds, cancellationToken))
        {
            return false;
        }

        ApplicationUser? user = await userManager.FindByIdAsync(authorizationSubject);
        if (user is null)
        {
            return false;
        }

        if (await userManager.IsInRoleAsync(user, RoleNames.Administrator))
        {
            return true;
        }

        return await authorityReader.HasSystemOwnerAssignmentAsync(
            systemId, authorizationSubject, cancellationToken);
    }
}

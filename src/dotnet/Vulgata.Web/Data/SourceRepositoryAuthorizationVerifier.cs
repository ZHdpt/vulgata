using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

public sealed class SourceRepositoryAuthorizationVerifier(IServiceScopeFactory scopeFactory) :
    ISourceRepositoryAuthorizationVerifier
{
    public async Task<AuthorizedSourceRepository?> AuthorizeAsync(
        SourceAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RepositoryId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Subject?.SubjectId))
        {
            return null;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        UserManager<ApplicationUser> users =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser? user = await users.FindByIdAsync(request.Subject.SubjectId);
        if (user is null)
        {
            return null;
        }

        bool administrator = await users.IsInRoleAsync(user, RoleNames.Administrator);
        bool systemOwner = await users.IsInRoleAsync(user, RoleNames.SystemOwner);
        if (!administrator && !systemOwner)
        {
            return null;
        }

        VulgataDbContext domain = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        var repository = await domain.Repositories
            .AsNoTracking()
            .Where(candidate => candidate.Id == request.RepositoryId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.SystemId,
                candidate.GitUrl,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (repository is null)
        {
            return null;
        }

        if (!administrator && repository.SystemId is Guid systemId &&
            !await domain.SystemOwnerAssignments.AsNoTracking().AnyAsync(
                assignment => assignment.SystemId == systemId &&
                    assignment.UserId == user.Id,
                cancellationToken))
        {
            return null;
        }

        return new(repository.Id, repository.SystemId, repository.GitUrl);
    }
}

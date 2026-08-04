using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Data;

public sealed class RepositoryAuthorizationVerifier(
    IUserContext userContext,
    ISystemRepository systemRepository,
    IRepositoryRepository repositoryRepository) : IRepositoryAuthorizationVerifier
{
    private const int MaximumRepositoryCount = 2;

    public async ValueTask<bool> HasCurrentAccessAsync(
        IReadOnlyCollection<Guid> requiredRepositoryIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requiredRepositoryIds);
        if (string.IsNullOrWhiteSpace(userContext.UserId) ||
            requiredRepositoryIds.Count is < 1 or > MaximumRepositoryCount ||
            requiredRepositoryIds.Any(id => id == Guid.Empty) ||
            requiredRepositoryIds.Distinct().Count() != requiredRepositoryIds.Count)
        {
            return false;
        }

        SystemOwnerFilter filter = userContext.ToSystemOwnerFilter();
        Guid? commonSystemId = null;
        foreach (Guid repositoryId in requiredRepositoryIds.Order())
        {
            var repository = await repositoryRepository.GetByIdAsync(
                repositoryId,
                cancellationToken);
            if (repository?.SystemId is not Guid systemId ||
                commonSystemId is not null && commonSystemId != systemId ||
                await systemRepository.GetVisibleByIdAsync(
                    systemId,
                    filter,
                    cancellationToken) is null)
            {
                return false;
            }

            commonSystemId = systemId;
        }

        return commonSystemId is not null;
    }
}

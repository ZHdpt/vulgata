using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Core.DomainServices;

public interface IRepositoryRepository
{
    Task<IReadOnlyList<RepositoryEntity>> ListBySystemAsync(Guid systemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RepositoryEntity>> ListStandaloneAsync(CancellationToken cancellationToken = default);

    Task<RepositoryEntity?> GetByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default);

    Task<RepositoryEntity?> GetBySystemAndIdAsync(Guid systemId, Guid repositoryId, CancellationToken cancellationToken = default);

    Task<RepositoryEntity?> GetStandaloneByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(Guid systemId, string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default);

    Task<bool> StandaloneNameExistsAsync(string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default);

    Task<RepositoryEntity> AddAsync(RepositoryEntity repository, CancellationToken cancellationToken = default);

    void Remove(RepositoryEntity repository);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

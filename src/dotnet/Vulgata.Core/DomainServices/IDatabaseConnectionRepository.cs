using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface IDatabaseConnectionRepository
{
    Task<DatabaseConnection?> GetByRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken = default);

    Task<DatabaseConnection> AddAsync(DatabaseConnection databaseConnection, CancellationToken cancellationToken = default);

    Task UpdateAsync(DatabaseConnection databaseConnection, CancellationToken cancellationToken = default);

    Task DeleteAsync(DatabaseConnection databaseConnection, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

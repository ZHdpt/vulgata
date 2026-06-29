using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class DatabaseConnectionRepository(VulgataDbContext dbContext) : IDatabaseConnectionRepository
{
    public async Task<DatabaseConnection?> GetByRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken = default)
    {
        return await dbContext.DatabaseConnections
            .FirstOrDefaultAsync(connection => connection.RepositoryId == repositoryId, cancellationToken);
    }

    public async Task<DatabaseConnection> AddAsync(DatabaseConnection databaseConnection, CancellationToken cancellationToken = default)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<DatabaseConnection> entry =
            await dbContext.DatabaseConnections.AddAsync(databaseConnection, cancellationToken);

        return entry.Entity;
    }

    public Task UpdateAsync(DatabaseConnection databaseConnection, CancellationToken cancellationToken = default)
    {
        dbContext.DatabaseConnections.Update(databaseConnection);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(DatabaseConnection databaseConnection, CancellationToken cancellationToken = default)
    {
        dbContext.DatabaseConnections.Remove(databaseConnection);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

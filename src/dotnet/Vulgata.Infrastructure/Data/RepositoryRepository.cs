using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Infrastructure.Data;

public sealed class RepositoryRepository(VulgataDbContext dbContext) : IRepositoryRepository
{
    public async Task<IReadOnlyList<RepositoryEntity>> ListBySystemAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Repositories
            .AsNoTracking()
            .Where(repository => repository.SystemId == systemId)
            .OrderBy(repository => repository.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RepositoryEntity>> ListStandaloneAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Repositories
            .AsNoTracking()
            .Where(repository => repository.SystemId == null)
            .OrderBy(repository => repository.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<RepositoryEntity?> GetByIdAsync(
        Guid repositoryId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Repositories
            .Include(repository => repository.DatabaseConnection)
            .FirstOrDefaultAsync(repository => repository.Id == repositoryId, cancellationToken);
    }

    public async Task<RepositoryEntity?> GetBySystemAndIdAsync(
        Guid systemId,
        Guid repositoryId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Repositories
            .Include(repository => repository.DatabaseConnection)
            .FirstOrDefaultAsync(
                repository => repository.SystemId == systemId && repository.Id == repositoryId,
                cancellationToken);
    }

    public async Task<RepositoryEntity?> GetStandaloneByIdAsync(
        Guid repositoryId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Repositories
            .Include(repository => repository.DatabaseConnection)
            .FirstOrDefaultAsync(
                repository => repository.SystemId == null && repository.Id == repositoryId,
                cancellationToken);
    }

    public async Task<bool> NameExistsAsync(
        Guid systemId,
        string name,
        Guid? excludeRepositoryId = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = RepositoryEntity.NormalizeName(name);

        IQueryable<RepositoryEntity> query = dbContext.Repositories
            .AsNoTracking()
            .Where(repository => repository.SystemId == systemId && repository.NormalizedName == normalizedName);

        if (excludeRepositoryId.HasValue)
        {
            query = query.Where(repository => repository.Id != excludeRepositoryId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> StandaloneNameExistsAsync(
        string name,
        Guid? excludeRepositoryId = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = RepositoryEntity.NormalizeName(name);

        IQueryable<RepositoryEntity> query = dbContext.Repositories
            .AsNoTracking()
            .Where(repository => repository.SystemId == null && repository.NormalizedName == normalizedName);

        if (excludeRepositoryId.HasValue)
        {
            query = query.Where(repository => repository.Id != excludeRepositoryId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<RepositoryEntity> AddAsync(
        RepositoryEntity repository,
        CancellationToken cancellationToken = default)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<RepositoryEntity> entry =
            await dbContext.Repositories.AddAsync(repository, cancellationToken);

        return entry.Entity;
    }

    public void Remove(RepositoryEntity repository)
    {
        dbContext.Repositories.Remove(repository);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
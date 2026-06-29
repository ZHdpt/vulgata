using Microsoft.EntityFrameworkCore;
using SystemEntity = Vulgata.Core.Entities.System;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Data;

public sealed class SystemRepository(VulgataDbContext dbContext) : ISystemRepository
{
    public async Task<IReadOnlyList<SystemEntity>> ListVisibleAsync(
        string userId, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        IQueryable<SystemEntity> query = dbContext.Systems.AsNoTracking();

        if (!isAdministrator)
        {
            query = query.Where(s => s.OwnerAssignments.Any(a => a.UserId == userId));
        }

        return await query
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<SystemEntity?> GetVisibleByIdAsync(
        Guid id, string userId, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        IQueryable<SystemEntity> query = dbContext.Systems.AsNoTracking();

        if (!isAdministrator)
        {
            query = query.Where(s => s.OwnerAssignments.Any(a => a.UserId == userId));
        }

        return await query.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<SystemEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Systems
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(
        string name, Guid? excludeSystemId = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = SystemEntity.NormalizeName(name);

        IQueryable<SystemEntity> query = dbContext.Systems
            .AsNoTracking()
            .Where(s => s.NormalizedName == normalizedName);

        if (excludeSystemId.HasValue)
        {
            query = query.Where(s => s.Id != excludeSystemId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<SystemEntity> AddAsync(SystemEntity system, CancellationToken cancellationToken = default)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<SystemEntity> entry =
            await dbContext.Systems.AddAsync(system, cancellationToken);
        return entry.Entity;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SystemDeleteResult> DeleteIfNoDependenciesAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        SystemEntity? system = await dbContext.Systems
            .Include(s => s.Repositories)
            .Include(s => s.OwnerAssignments)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (system is null)
        {
            return SystemDeleteResult.NotFound;
        }

        SystemDependencyCounts dependencies = await GetDependencyCountsAsync(id, cancellationToken);

        if (dependencies.TotalCount > 0)
        {
            return SystemDeleteResult.HasDependencies(
                dependencies.RepositoryCount, dependencies.OwnerAssignmentCount);
        }

        dbContext.Systems.Remove(system);
        return SystemDeleteResult.Deleted;
    }

    public async Task<SystemDependencyCounts> GetDependencyCountsAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        int repositoryCount = await dbContext.Repositories
            .Where(r => r.SystemId == id)
            .CountAsync(cancellationToken);

        int ownerAssignmentCount = await dbContext.SystemOwnerAssignments
            .Where(a => a.SystemId == id)
            .CountAsync(cancellationToken);

        return new SystemDependencyCounts(repositoryCount, ownerAssignmentCount);
    }

    public async Task<SystemOwnerAssignmentWriteResult> AssignOwnerAsync(
        Guid systemId,
        string userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        bool systemExists = await dbContext.Systems
            .AsNoTracking()
            .AnyAsync(s => s.Id == systemId, cancellationToken);

        if (!systemExists)
        {
            return SystemOwnerAssignmentWriteResult.SystemNotFound;
        }

        string normalizedUserId = (userId ?? string.Empty).Trim();
        bool alreadyAssigned = await dbContext.SystemOwnerAssignments
            .AsNoTracking()
            .AnyAsync(a => a.SystemId == systemId && a.UserId == normalizedUserId, cancellationToken);

        if (alreadyAssigned)
        {
            return SystemOwnerAssignmentWriteResult.AlreadyAssigned;
        }

        Vulgata.Core.Entities.SystemOwnerAssignment assignment =
            Vulgata.Core.Entities.SystemOwnerAssignment.Create(systemId, normalizedUserId, now);

        await dbContext.SystemOwnerAssignments.AddAsync(assignment, cancellationToken);
        return SystemOwnerAssignmentWriteResult.Assigned;
    }

    public async Task<bool> RemoveOwnerAsync(
        Guid systemId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserId = (userId ?? string.Empty).Trim();

        Vulgata.Core.Entities.SystemOwnerAssignment? assignment = await dbContext.SystemOwnerAssignments
            .FirstOrDefaultAsync(a => a.SystemId == systemId && a.UserId == normalizedUserId, cancellationToken);

        if (assignment is null)
        {
            return false;
        }

        dbContext.SystemOwnerAssignments.Remove(assignment);
        return true;
    }

    public async Task<IReadOnlyList<SystemOwnerAssignmentSummary>> ListOwnersAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.SystemOwnerAssignments
            .AsNoTracking()
            .Where(a => a.SystemId == systemId)
            .Select(a => new SystemOwnerAssignmentSummary(a.UserId, a.AssignedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountOwnerAssignmentsByUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserId = (userId ?? string.Empty).Trim();

        return await dbContext.SystemOwnerAssignments
            .AsNoTracking()
            .Where(a => a.UserId == normalizedUserId)
            .CountAsync(cancellationToken);
    }
}

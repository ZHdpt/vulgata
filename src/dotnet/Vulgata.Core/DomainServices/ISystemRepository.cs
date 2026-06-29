using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Core.DomainServices;

public interface ISystemRepository
{
    Task<IReadOnlyList<SystemEntity>> ListVisibleAsync(string userId, bool isAdministrator, CancellationToken cancellationToken = default);

    Task<SystemEntity?> GetVisibleByIdAsync(Guid id, string userId, bool isAdministrator, CancellationToken cancellationToken = default);

    Task<SystemEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeSystemId = null, CancellationToken cancellationToken = default);

    Task<SystemEntity> AddAsync(SystemEntity system, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<SystemDeleteResult> DeleteIfNoDependenciesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SystemDependencyCounts> GetDependencyCountsAsync(Guid id, CancellationToken cancellationToken = default);
}

public enum SystemDeleteOutcome
{
    Deleted,
    NotFound,
    HasDependencies,
}

public sealed record SystemDeleteResult(SystemDeleteOutcome Outcome, int RepositoryCount, int OwnerAssignmentCount)
{
    public static SystemDeleteResult Deleted { get; } = new(SystemDeleteOutcome.Deleted, 0, 0);

    public static SystemDeleteResult NotFound { get; } = new(SystemDeleteOutcome.NotFound, 0, 0);

    public static SystemDeleteResult HasDependencies(int repositoryCount, int ownerAssignmentCount) =>
        new(SystemDeleteOutcome.HasDependencies, repositoryCount, ownerAssignmentCount);
}

public sealed record SystemDependencyCounts(int RepositoryCount, int OwnerAssignmentCount)
{
    public int TotalCount => RepositoryCount + OwnerAssignmentCount;
}

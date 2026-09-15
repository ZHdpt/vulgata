using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface IRetainedSourceRepository
{
    Task<bool> HasForRepositoryAsync(
        Guid repositoryId,
        CancellationToken cancellationToken = default);

    Task<SourceSnapshot?> GetAsync(
        Guid repositoryId,
        string profiledRevision,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        SourceSnapshot sourceSnapshot,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

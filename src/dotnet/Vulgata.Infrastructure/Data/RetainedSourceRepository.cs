using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

internal sealed class RetainedSourceRepository(VulgataDbContext context) :
    IRetainedSourceRepository
{
    public Task<bool> HasForRepositoryAsync(
        Guid repositoryId,
        CancellationToken cancellationToken = default) =>
        context.SourceSnapshots
            .AsNoTracking()
            .AnyAsync(snapshot => snapshot.RepositoryId == repositoryId, cancellationToken);

    public Task<SourceSnapshot?> GetAsync(
        Guid repositoryId,
        string profiledRevision,
        CancellationToken cancellationToken = default) =>
        context.SourceSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(
                snapshot => snapshot.RepositoryId == repositoryId &&
                    snapshot.ProfiledRevision == profiledRevision,
                cancellationToken);

    public async Task AddAsync(
        SourceSnapshot sourceSnapshot,
        CancellationToken cancellationToken = default) =>
        _ = await context.SourceSnapshots.AddAsync(sourceSnapshot, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _ = await context.SaveChangesAsync(cancellationToken);
}

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

internal sealed class RetainedSourceRepository(
    IDbContextFactory<VulgataDbContext> contextFactory) :
    IRetainedSourceRepository
{
    public async Task<bool> HasForRepositoryAsync(
        Guid repositoryId,
        CancellationToken cancellationToken = default)
    {
        await using VulgataDbContext readContext =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        return await readContext.SourceSnapshots
            .AsNoTracking()
            .AnyAsync(snapshot => snapshot.RepositoryId == repositoryId, cancellationToken);
    }

    public async Task<SourceSnapshot?> GetAsync(
        Guid repositoryId,
        string profiledRevision,
        CancellationToken cancellationToken = default)
    {
        await using VulgataDbContext readContext =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        return await readContext.SourceSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(
                snapshot => snapshot.RepositoryId == repositoryId &&
                    snapshot.ProfiledRevision == profiledRevision,
                cancellationToken);
    }

    public async Task PublishAsync(
        SourceSnapshot sourceSnapshot,
        CancellationToken cancellationToken = default)
    {
        await using VulgataDbContext publicationContext =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        _ = await publicationContext.SourceSnapshots.AddAsync(
            sourceSnapshot,
            cancellationToken);
        _ = await publicationContext.SaveChangesAsync(cancellationToken);
    }
}

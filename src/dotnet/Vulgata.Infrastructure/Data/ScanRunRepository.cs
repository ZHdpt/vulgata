using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class ScanRunRepository(VulgataDbContext dbContext) : IScanRunRepository
{
    public Task<ScanRun?> GetAsync(
        Guid repositoryId,
        Guid scanRunId,
        CancellationToken cancellationToken = default) =>
        dbContext.ScanRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                scanRun => scanRun.RepositoryId == repositoryId && scanRun.Id == scanRunId,
                cancellationToken);

    public async Task AddAsync(ScanRun scanRun, CancellationToken cancellationToken = default) =>
        await dbContext.ScanRuns.AddAsync(scanRun, cancellationToken);

    public void AddCandidateOutcomes(IEnumerable<ScanCandidateOutcome> outcomes) =>
        dbContext.ScanCandidateOutcomes.AddRange(outcomes);

    public void AddStructuralRecords(
        IEnumerable<CodeUnit> codeUnits,
        IEnumerable<CodeUnitEdge> edges,
        IEnumerable<UnresolvedStructuralRelationshipCandidate> unresolvedRelationships)
    {
        dbContext.CodeUnits.AddRange(codeUnits);
        dbContext.CodeUnitEdges.AddRange(edges);
        dbContext.UnresolvedStructuralRelationshipCandidates.AddRange(unresolvedRelationships);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

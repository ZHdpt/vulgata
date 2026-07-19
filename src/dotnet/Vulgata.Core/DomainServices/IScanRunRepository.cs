using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface IScanRunRepository
{
    Task<ScanRun?> GetAsync(Guid repositoryId, Guid scanRunId, CancellationToken cancellationToken = default);
    Task AddAsync(ScanRun scanRun, CancellationToken cancellationToken = default);
    void AddCandidateOutcomes(IEnumerable<ScanCandidateOutcome> outcomes);
    void AddStructuralRecords(
        IEnumerable<CodeUnit> codeUnits,
        IEnumerable<CodeUnitEdge> edges,
        IEnumerable<UnresolvedStructuralRelationshipCandidate> unresolvedRelationships);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

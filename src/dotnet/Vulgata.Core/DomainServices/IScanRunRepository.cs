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
    Task PersistCompletedProfileAsync(
        RepositoryProfileDraft draft,
        CancellationToken cancellationToken = default);
    Task RecoverFailedFinalCommitAsync(
        Guid repositoryId,
        Guid scanRunId,
        string sanitizedMessage,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken = default);
    Task RecoverAbortedFinalCommitAsync(
        Guid repositoryId,
        Guid scanRunId,
        string sanitizedMessage,
        DateTimeOffset abortedAt,
        CancellationToken cancellationToken = default) =>
        RecoverFailedFinalCommitAsync(
            repositoryId,
            scanRunId,
            sanitizedMessage,
            abortedAt,
            cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

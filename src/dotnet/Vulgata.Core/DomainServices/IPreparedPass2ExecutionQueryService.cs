using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public sealed record PreparedPass2StatusGroup(
    Guid Id, string State, string PlanFingerprint, string? ReasonCode);
public sealed record PreparedPass2StatusOmission(
    Guid DecisionId, string EvidenceFingerprint, string ReasonCode);
public sealed record PreparedPass2StatusAuthority(
    IReadOnlyList<Pass2JobExecutionState> JobStates,
    IReadOnlyList<PreparedPass2StatusGroup> Groups,
    IReadOnlyList<PreparedPass2StatusOmission> Omissions);

public interface IPreparedPass2ExecutionQueryService
{
    Task<Pass1PairExecution?> FindPairAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TwoRepositoryScanCoordination?> FindCoordinationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Pass2Execution?> FindExecutionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PreparedPass2StatusAuthority> ReadStatusAsync(Guid executionId, CancellationToken cancellationToken = default);
}

public interface IPass2AuthorizationDataReader
{
    Task<bool> RepositoriesBelongToSystemAsync(
        Guid systemId, IReadOnlyCollection<Guid> repositoryIds,
        CancellationToken cancellationToken = default);
    Task<bool> HasSystemOwnerAssignmentAsync(
        Guid systemId, string userId, CancellationToken cancellationToken = default);
}

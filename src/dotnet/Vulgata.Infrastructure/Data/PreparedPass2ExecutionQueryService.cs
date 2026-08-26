using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedPass2ExecutionQueryService(VulgataDbContext context) :
    IPreparedPass2ExecutionQueryService,
    IPass2AuthorizationDataReader
{
    public Task<Pass1PairExecution?> FindPairAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Pass1PairExecutions.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    public Task<TwoRepositoryScanCoordination?> FindCoordinationAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.TwoRepositoryScanCoordinations.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    public Task<Pass2Execution?> FindExecutionAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Pass2Executions.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    public async Task<PreparedPass2StatusAuthority> ReadStatusAsync(
        Guid executionId, CancellationToken cancellationToken = default)
    {
        Pass2JobExecutionState[] jobs = await context.Pass2JobExecutions.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == executionId)
            .Select(value => value.State).ToArrayAsync(cancellationToken);
        PreparedPass2StatusGroup[] groups = await context.FlowGroups.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == executionId)
            .Select(value => new PreparedPass2StatusGroup(
                value.Id, value.State, value.PlanFingerprint, value.ReasonCode))
            .ToArrayAsync(cancellationToken);
        PreparedPass2StatusOmission[] omissions = await context.Pass2Omissions.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == executionId)
            .Join(context.CrossRepositoryResolutionDecisions.AsNoTracking(),
                omission => omission.ExactResolutionDecisionId,
                decision => decision.Id,
                (omission, decision) => new PreparedPass2StatusOmission(
                    omission.ExactResolutionDecisionId,
                    decision.EvidenceFingerprint,
                    omission.ReasonCode))
            .ToArrayAsync(cancellationToken);
        return new(jobs, groups, omissions);
    }

    public async Task<bool> RepositoriesBelongToSystemAsync(
        Guid systemId, IReadOnlyCollection<Guid> repositoryIds,
        CancellationToken cancellationToken = default) =>
        await context.Repositories.AsNoTracking().CountAsync(value =>
            repositoryIds.Contains(value.Id) && value.SystemId == systemId,
            cancellationToken) == repositoryIds.Count;

    public Task<bool> HasSystemOwnerAssignmentAsync(
        Guid systemId, string userId, CancellationToken cancellationToken = default) =>
        context.SystemOwnerAssignments.AsNoTracking().AnyAsync(value =>
            value.SystemId == systemId && value.UserId == userId,
            cancellationToken);
}

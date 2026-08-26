using System.Data;
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public enum PreparedPass2StartOutcome
{
    Created,
    Replay,
    Conflict,
    Rejected,
    NotFound,
    Cancelled,
    Failed,
}

public sealed record PreparedPass2StartResult(
    PreparedPass2StartOutcome Outcome,
    Guid? ExecutionId,
    int FlowGroupCount,
    int OmissionCount,
    int JobCount,
    int RetrievalBudgetCount,
    bool IsReplay);

public sealed class PreparedPass2Store(VulgataDbContext dbContext)
{
    private const int _maxFlowGroups = 10;
    private readonly VulgataDbContext _dbContext = dbContext ??
        throw new ArgumentNullException(nameof(dbContext));

    public async Task<PreparedPass2StartResult> StartAsync(
        Guid pass1PairExecutionId,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(pass1PairExecutionId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(scope);
        if (cancellationToken.IsCancellationRequested)
        {
            return Result(PreparedPass2StartOutcome.Cancelled);
        }

        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken).ConfigureAwait(false);

            Pass1PairExecution? pair = await _dbContext.Pass1PairExecutions.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == pass1PairExecutionId, cancellationToken)
                .ConfigureAwait(false);
            if (pair is null)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Result(PreparedPass2StartOutcome.NotFound);
            }

            PreparedScanManifest[] manifests = await _dbContext.PreparedScanManifests.AsNoTracking()
                .Where(value => value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            Guid[] repositoryIds = manifests.Select(value => value.RepositoryId).Distinct().ToArray();
            if (!IsAuthorizedTerminalPair(pair, manifests, repositoryIds, scope) ||
                !await scope.HasCurrentAccessAsync(repositoryIds, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return Result(PreparedPass2StartOutcome.Rejected);
            }

            Pass2Execution? existing = await _dbContext.Pass2Executions.AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.Pass1PairExecutionId == pass1PairExecutionId,
                    cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                PreparedPass2StartResult replay = await ReplayAsync(
                    existing,
                    pair,
                    cancellationToken).ConfigureAwait(false);
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return replay;
            }

            DateTimeOffset startedAt = pair.TerminalAt ?? pair.UpdatedAt;
            Guid executionId = DeriveId("pass2-execution/1.0", pair.Id);
            Pass2Execution execution = Pass2Execution.CreatePlanning(
                executionId,
                pair.Id,
                pair.CoordinationId,
                pair.SystemId,
                startedAt);
            _dbContext.Pass2Executions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            PreparedPass2FlowPlanningResult plan = await new PreparedPass2FlowPlanner(_dbContext)
                .PlanAsync(execution.Id, scope, cancellationToken).ConfigureAwait(false);
            FlowGroup[] groups = plan.FlowGroups.Take(_maxFlowGroups).ToArray();
            Pass2Omission[] boundOmissions = plan.FlowGroups.Skip(_maxFlowGroups)
                .Select(group => Pass2Omission.CreateBoundExcluded(
                    DeriveId(
                        "pass2-omission/1.0",
                        execution.Id,
                        group.ExactResolutionDecisionId ?? throw new InvalidOperationException(
                            "A bound-excluded flow candidate must retain its exact decision identity.")),
                    execution.Id,
                    group.ExactResolutionDecisionId!.Value,
                    startedAt))
                .ToArray();
            Pass2Omission[] omissions = plan.Omissions.Concat(boundOmissions).ToArray();
            _dbContext.FlowGroups.AddRange(groups);
            _dbContext.Pass2Omissions.AddRange(omissions);

            var jobs = new List<Pass2JobExecution>();
            var budgets = new List<Pass2RetrievalBudget>();
            foreach (FlowGroup group in groups.Where(value => value.State == "Complete"))
            {
                Guid jobId = DeriveId("pass2-job/1.0", execution.Id, group.Id);
                Pass2JobExecution job = Pass2JobExecution.CreatePending(
                    jobId,
                    execution.Id,
                    group.Id,
                    group.GroupOrder);
                jobs.Add(job);
                budgets.Add(Pass2RetrievalBudget.Create(
                    DeriveId("pass2-retrieval-budget/1.0", job.Id),
                    job.Id));
            }

            _dbContext.Pass2JobExecutions.AddRange(jobs);
            _dbContext.Pass2RetrievalBudgets.AddRange(budgets);
            execution.MarkRunning(startedAt);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            return new(
                PreparedPass2StartOutcome.Created,
                execution.Id,
                groups.Length,
                omissions.Length,
                jobs.Count,
                budgets.Count,
                false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(PreparedPass2StartOutcome.Cancelled);
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(PreparedPass2StartOutcome.Conflict);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(PreparedPass2StartOutcome.Failed);
        }
    }

    private static bool IsAuthorizedTerminalPair(
        Pass1PairExecution pair,
        IReadOnlyCollection<PreparedScanManifest> manifests,
        IReadOnlyCollection<Guid> repositoryIds,
        RepositoryAccessScope scope) =>
        pair.Phase == Pass1PairExecutionPhase.ReadyForResolution &&
        pair.TerminalAt.HasValue &&
        manifests.Count == 2 &&
        repositoryIds.Count == 2 &&
        manifests.All(value =>
            value.ApprovalState == PreparedScanManifestApprovalState.Approved &&
            value.CoordinationId == pair.CoordinationId &&
            (value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId) &&
            scope.Contains(value.RepositoryId));

    private async Task<PreparedPass2StartResult> ReplayAsync(
        Pass2Execution execution,
        Pass1PairExecution pair,
        CancellationToken cancellationToken)
    {
        if (execution.Id != DeriveId("pass2-execution/1.0", pair.Id) ||
            execution.CoordinationId != pair.CoordinationId ||
            execution.SystemId != pair.SystemId ||
            execution.State != Pass2ExecutionState.Running)
        {
            return Result(PreparedPass2StartOutcome.Conflict, execution.Id);
        }

        FlowGroup[] groups = await _dbContext.FlowGroups.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Pass2Omission[] omissions = await _dbContext.Pass2Omissions.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Pass2JobExecution[] jobs = await _dbContext.Pass2JobExecutions.AsNoTracking()
            .Where(value => value.Pass2ExecutionId == execution.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] jobIds = jobs.Select(value => value.Id).ToArray();
        int budgets = await _dbContext.Pass2RetrievalBudgets.AsNoTracking()
            .CountAsync(value => jobIds.Contains(value.Pass2JobExecutionId), cancellationToken)
            .ConfigureAwait(false);
        CrossRepositoryResolutionDecision[] decisions = await _dbContext
            .CrossRepositoryResolutionDecisions.AsNoTracking()
            .Where(value =>
                value.Pass1PairExecutionId == pair.Id &&
                value.CoordinationId == pair.CoordinationId &&
                value.SystemId == pair.SystemId &&
                value.Outcome == "ExactMatch")
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        Guid[] orderedDecisionIds = decisions
            .OrderBy(value => value.Protocol)
            .ThenBy(value => value.NormalizedEndpointIdentifier, StringComparer.Ordinal)
            .ThenBy(value => value.ConsumerEntryStableKey, StringComparer.Ordinal)
            .ThenBy(value => value.Id.ToString("N"), StringComparer.Ordinal)
            .Select(value => value.Id)
            .ToArray();
        Guid[] expectedGroupDecisionIds = orderedDecisionIds.Take(_maxFlowGroups).ToArray();
        Guid[] actualGroupDecisionIds = groups.OrderBy(value => value.GroupOrder)
            .Select(value => value.ExactResolutionDecisionId ?? Guid.Empty)
            .ToArray();
        Guid[] expectedOmissionDecisionIds = orderedDecisionIds.Skip(_maxFlowGroups).ToArray();
        Guid[] actualOmissionDecisionIds = omissions
            .OrderBy(value => Array.IndexOf(expectedOmissionDecisionIds, value.ExactResolutionDecisionId))
            .Select(value => value.ExactResolutionDecisionId)
            .ToArray();
        int completeGroups = groups.Count(value => value.State == "Complete");
        if (groups.Length == 0 ||
            !actualGroupDecisionIds.SequenceEqual(expectedGroupDecisionIds) ||
            !actualOmissionDecisionIds.SequenceEqual(expectedOmissionDecisionIds) ||
            omissions.Any(value =>
                value.State != Pass2OmissionState.Omitted ||
                value.ReasonCode != "flow-group-bound" ||
                value.Id != DeriveId(
                    "pass2-omission/1.0",
                    execution.Id,
                    value.ExactResolutionDecisionId)) ||
            jobs.Length != completeGroups || budgets != jobs.Length ||
            jobs.Any(job => groups.All(group => group.Id != job.FlowGroupId)))
        {
            return Result(PreparedPass2StartOutcome.Conflict, execution.Id);
        }

        return new(
            PreparedPass2StartOutcome.Replay,
            execution.Id,
            groups.Length,
            omissions.Length,
            jobs.Length,
            budgets,
            true);
    }

    private static Guid DeriveId(string schema, params Guid[] identities)
    {
        string canonical = schema + "\n" + string.Join('\n', identities.Select(value => value.ToString("D")));
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private static PreparedPass2StartResult Result(
        PreparedPass2StartOutcome outcome,
        Guid? executionId = null) =>
        new(outcome, executionId, 0, 0, 0, 0, false);
}

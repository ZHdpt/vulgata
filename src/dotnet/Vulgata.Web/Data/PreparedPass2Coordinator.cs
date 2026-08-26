using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Web.Data;

public sealed record PreparedPass2ExecutionWarning(
    Guid GroupIdentity,
    string Fingerprint,
    string ReasonCode);

public sealed record PreparedPass2ExecutionStatus(
    Guid ExecutionId,
    string State,
    long StateVersion,
    int PublishedCount,
    int FailedCount,
    int IncompleteCount,
    int BoundExcludedCount,
    int OmissionCount,
    int PendingJobCount,
    int RunningJobCount,
    int RetryScheduledJobCount,
    IReadOnlyList<PreparedPass2ExecutionWarning> Warnings);

public sealed class PreparedPass2Coordinator(
    PreparedPass2Store store,
    IPreparedPass2ExecutionQueryService executionQueries,
    ISystemRepository systemRepository,
    IRepositoryAuthorizationVerifier authorizationVerifier,
    IPreparedPass1ProviderAvailabilityProbe providerProbe,
    PreparedPass2ExecutionActivator activator,
    TimeProvider timeProvider)
{
    public async Task<PreparedPass2StartResult> StartAsync(
        Guid pass1PairExecutionId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        Pass1PairExecution? pair = pass1PairExecutionId == Guid.Empty
            ? null
            : await executionQueries.FindPairAsync(
                pass1PairExecutionId, cancellationToken).ConfigureAwait(false);
        if (pair is null || !await IsVisibleAsync(
                pair.SystemId, user, cancellationToken).ConfigureAwait(false))
        {
            return new(
                PreparedPass2StartOutcome.NotFound,
                null, 0, 0, 0, 0, false);
        }

        TwoRepositoryScanCoordination? coordination = await executionQueries
            .FindCoordinationAsync(pair.CoordinationId, cancellationToken).ConfigureAwait(false);
        if (coordination is null)
        {
            return new(
                PreparedPass2StartOutcome.NotFound,
                null, 0, 0, 0, 0, false);
        }

        var accessScope = new RepositoryAccessScope(
            [coordination.RepositoryOneId, coordination.RepositoryTwoId],
            authorizationVerifier);
        PreparedPass2StartResult result = await store.StartAsync(
            pair.Id,
            user.UserId,
            accessScope,
            cancellationToken).ConfigureAwait(false);
        if (result.Outcome is PreparedPass2StartOutcome.Created or
            PreparedPass2StartOutcome.Replay && result.ExecutionId is Guid executionId)
        {
            activator.Signal(executionId);
        }

        return result;
    }

    public async Task<PreparedPass2ExecutionStatus?> GetStatusAsync(
        Guid pass2ExecutionId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        Pass2Execution? execution = await ResolveVisibleExecutionAsync(
            pass2ExecutionId, user, cancellationToken).ConfigureAwait(false);
        if (execution is null)
        {
            return null;
        }

        if (execution.State is Pass2ExecutionState.Completed or
            Pass2ExecutionState.CompletedWithWarnings)
        {
            return new(
                execution.Id,
                execution.State.ToString(),
                execution.StateVersion,
                execution.PublishedCount,
                execution.FailedCount,
                execution.TerminalIncompleteCount,
                execution.TerminalBoundExcludedCount,
                execution.TerminalOmissionCount,
                0,
                0,
                0,
                Array.AsReadOnly(ReadTerminalWarnings(execution.TerminalWarningInventory)));
        }

        PreparedPass2StatusAuthority statusAuthority = await executionQueries.ReadStatusAsync(
            execution.Id, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Pass2JobExecutionState> jobStates = statusAuthority.JobStates;
        IReadOnlyList<PreparedPass2StatusGroup> groupRows = statusAuthority.Groups;
        IReadOnlyList<PreparedPass2StatusOmission> omissionRows = statusAuthority.Omissions;
        PreparedPass2ExecutionWarning[] warnings = groupRows
            .Where(value => value.State is "Incomplete" or "BoundExcluded" or "Failed")
            .Select(value => new PreparedPass2ExecutionWarning(
                value.Id,
                value.PlanFingerprint,
                value.ReasonCode ?? "worker-failed"))
            .Concat(omissionRows.Select(value => new PreparedPass2ExecutionWarning(
                value.DecisionId,
                value.EvidenceFingerprint,
                value.ReasonCode)))
            .OrderBy(value => value.Fingerprint, StringComparer.Ordinal)
            .ThenBy(value => value.GroupIdentity)
            .ToArray();
        return new(
            execution.Id,
            execution.State.ToString(),
            execution.StateVersion,
            execution.PublishedCount,
            execution.FailedCount,
            groupRows.Count(value => value.State == "Incomplete"),
            groupRows.Count(value => value.State == "BoundExcluded"),
            omissionRows.Count,
            jobStates.Count(value => value == Pass2JobExecutionState.Pending),
            jobStates.Count(value => value == Pass2JobExecutionState.Running),
            jobStates.Count(value => value == Pass2JobExecutionState.RetryScheduled),
            Array.AsReadOnly(warnings));
    }

    private static PreparedPass2ExecutionWarning[] ReadTerminalWarnings(string? inventory)
    {
        if (inventory is null)
        {
            throw new InvalidOperationException("The terminal Pass 2 summary is unavailable.");
        }
        if (inventory.Length == 0)
        {
            return [];
        }

        return inventory.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(value =>
            {
                string[] fields = value.Split('|');
                if (fields.Length != 3 || !Guid.TryParseExact(fields[0], "D", out Guid identity) ||
                    fields[1].Length != 64 || fields[2].Length is < 1 or > 128)
                {
                    throw new InvalidOperationException("The terminal Pass 2 warning inventory is invalid.");
                }

                return new PreparedPass2ExecutionWarning(identity, fields[1], fields[2]);
            })
            .ToArray();
    }

    public async Task<PreparedPass2RecoveryResult> ResumeAsync(
        Guid pass2ExecutionId,
        IUserContext user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (pass2ExecutionId == Guid.Empty || string.IsNullOrWhiteSpace(user.UserId))
        {
            return NotFound(pass2ExecutionId);
        }

        Pass2Execution? execution = await ResolveVisibleExecutionAsync(
            pass2ExecutionId, user, cancellationToken).ConfigureAwait(false);
        if (execution is null)
        {
            return NotFound(pass2ExecutionId);
        }

        TwoRepositoryScanCoordination? coordination = await executionQueries
            .FindCoordinationAsync(execution.CoordinationId, cancellationToken)
            .ConfigureAwait(false);
        if (coordination is null)
        {
            return NotFound(pass2ExecutionId);
        }

        Guid[] repositoryIds =
            [coordination.RepositoryOneId, coordination.RepositoryTwoId];
        var accessScope = new RepositoryAccessScope(repositoryIds, authorizationVerifier);
        bool providerAvailable = await providerProbe.IsAvailableAsync(
            execution.Pass1PairExecutionId,
            cancellationToken).ConfigureAwait(false);
        PreparedPass2RecoveryResult result = await store.RecoverAsync(
            execution.Id,
            accessScope,
            providerAvailable,
            timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false);
        if (result.Outcome is PreparedPass2RecoveryOutcome.Continued or
            PreparedPass2RecoveryOutcome.NoOpReplay &&
            result.State == Pass2ExecutionState.Running)
        {
            activator.Signal(execution.Id);
        }

        return result;
    }

    private async Task<Pass2Execution?> ResolveVisibleExecutionAsync(
        Guid pass2ExecutionId,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        if (pass2ExecutionId == Guid.Empty || string.IsNullOrWhiteSpace(user.UserId))
        {
            return null;
        }

        Pass2Execution? execution = await executionQueries.FindExecutionAsync(
            pass2ExecutionId, cancellationToken).ConfigureAwait(false);
        return execution is not null && await IsVisibleAsync(
            execution.SystemId, user, cancellationToken).ConfigureAwait(false)
            ? execution
            : null;
    }

    private async Task<bool> IsVisibleAsync(
        Guid systemId,
        IUserContext user,
        CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(user.UserId) &&
        await systemRepository.GetVisibleByIdAsync(
            systemId,
            user.ToSystemOwnerFilter(),
            cancellationToken).ConfigureAwait(false) is not null;

    private static PreparedPass2RecoveryResult NotFound(Guid executionId) => new(
        PreparedPass2RecoveryOutcome.NotFound,
        executionId == Guid.Empty ? null : executionId,
        null,
        null,
        0,
        0,
        false);
}

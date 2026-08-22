using Microsoft.EntityFrameworkCore;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared.Scans;

namespace Vulgata.Web.Data;

public interface IPreparedPass1ProviderAvailabilityProbe
{
    Task<bool> IsAvailableAsync(Guid pairExecutionId, CancellationToken cancellationToken);
}

internal sealed class PreparedPass1ProviderAvailabilityProbe(
    VulgataDbContext context,
    WorkerAgentProviderResolver providerResolver) : IPreparedPass1ProviderAvailabilityProbe
{
    public async Task<bool> IsAvailableAsync(Guid pairExecutionId, CancellationToken cancellationToken)
    {
        Guid systemId = await context.Pass1PairExecutions.AsNoTracking()
            .Where(pair => pair.Id == pairExecutionId)
            .Select(pair => pair.SystemId)
            .SingleOrDefaultAsync(cancellationToken);
        if (systemId == Guid.Empty)
        {
            return false;
        }

        try
        {
            _ = await providerResolver.ResolveAsync(systemId, cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

internal sealed record PreparedPass1StartBinding(
    PreparedScanAuthorizationScope AuthorizationScope,
    PreparedScanHandoff Handoff);

internal sealed record PreparedPass1ExecutionBinding(
    PreparedScanAuthorizationScope AuthorizationScope,
    Pass1PairExecution Pair);

/// <summary>
/// Owns pair-wide restart/manual-resume orchestration below the HTTP boundary.
/// </summary>
public sealed class PreparedPass1Coordinator(
    IPreparedPass1Store store,
    IPreparedScanHandoffSource handoffSource,
    ISystemRepository systemRepository,
    VulgataDbContext context,
    IPreparedPass1ProviderAvailabilityProbe providerProbe,
    PreparedPass1ExecutionActivator executionActivator,
    TimeProvider timeProvider)
{
    internal async Task<PreparedPass1StartBinding?> ResolveStartAsync(
        StartPass1ExecutionRequest request,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);
        if (!Guid.TryParse(user.UserId, out Guid userId) || userId == Guid.Empty ||
            request.FirstManifestId == Guid.Empty || request.SecondManifestId == Guid.Empty ||
            request.FirstManifestId == request.SecondManifestId)
        {
            return null;
        }

        Guid[] requestedIds = [request.FirstManifestId, request.SecondManifestId];
        var manifests = await context.PreparedScanManifests.AsNoTracking()
            .Where(manifest => requestedIds.Contains(manifest.Id))
            .Select(manifest => new
            {
                manifest.Id,
                manifest.CoordinationId,
                manifest.RepositoryId,
                manifest.ApprovalState,
                manifest.SchemaIdentity,
            })
            .ToArrayAsync(cancellationToken);
        if (manifests.Length != 2 ||
            manifests.Select(manifest => manifest.Id).Distinct().Count() != 2 ||
            manifests.Select(manifest => manifest.CoordinationId).Distinct().Count() != 1 ||
            manifests.Select(manifest => manifest.RepositoryId).Distinct().Count() != 2 ||
            manifests.Any(manifest =>
                manifest.ApprovalState != PreparedScanManifestApprovalState.Approved ||
                manifest.SchemaIdentity != PreparedScanManifest.CurrentSchemaIdentity))
        {
            return null;
        }

        Guid[] repositoryIds = manifests.Select(manifest => manifest.RepositoryId).Order().ToArray();
        Guid?[] systemIds = await context.Repositories.AsNoTracking()
            .Where(repository => repositoryIds.Contains(repository.Id))
            .Select(repository => repository.SystemId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (systemIds.Length != 1 || systemIds[0] is not Guid systemId || systemId == Guid.Empty ||
            await systemRepository.GetVisibleByIdAsync(
                systemId, user.ToSystemOwnerFilter(), cancellationToken) is null)
        {
            return null;
        }

        PreparedScanAuthorizationScope scope = new(userId, systemId, repositoryIds);
        PreparedScanHandoff? handoff = await handoffSource.GetAsync(
            scope, manifests[0].CoordinationId, cancellationToken);
        if (handoff is null ||
            !handoff.Manifests.Select(manifest => manifest.ManifestId).Order()
                .SequenceEqual(requestedIds.Order()))
        {
            return null;
        }

        return new(scope, handoff);
    }

    internal async Task<PreparedPass1StartResult> StartAsync(
        PreparedPass1StartBinding binding,
        int concurrencyLimit,
        CancellationToken cancellationToken)
    {
        PreparedPass1StartResult result = await store.StartAsync(
            binding.AuthorizationScope,
            binding.Handoff,
            concurrencyLimit,
            cancellationToken);
        if (result.Outcome is PreparedPass1StartOutcome.Created or PreparedPass1StartOutcome.Replay &&
            result.PairExecutionId is Guid pairExecutionId)
        {
            executionActivator.Signal(pairExecutionId, binding.AuthorizationScope, binding.Handoff);
        }

        return result;
    }

    internal async Task<PreparedPass1ExecutionBinding?> ResolveExecutionAsync(
        Guid pairExecutionId,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (pairExecutionId == Guid.Empty ||
            !Guid.TryParse(user.UserId, out Guid userId) || userId == Guid.Empty)
        {
            return null;
        }

        Pass1PairExecution? pair = await context.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pairExecutionId, cancellationToken);
        if (pair is null ||
            await systemRepository.GetVisibleByIdAsync(
                pair.SystemId, user.ToSystemOwnerFilter(), cancellationToken) is null)
        {
            return null;
        }

        ScanJobExecution[] pairJobs = await LoadPairJobsAsync(pair, cancellationToken);
        Guid[] repositoryIds = pairJobs
            .Select(job => job.RepositoryId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        if (repositoryIds.Length != 2 ||
            await context.Repositories.AsNoTracking().CountAsync(
                repository => repositoryIds.Contains(repository.Id) &&
                    repository.SystemId == pair.SystemId,
                cancellationToken) != 2)
        {
            return null;
        }

        return new(new PreparedScanAuthorizationScope(userId, pair.SystemId, repositoryIds), pair);
    }

    internal async Task<Pass1ExecutionStatusResponse?> GetStatusAsync(
        PreparedPass1ExecutionBinding binding,
        CancellationToken cancellationToken)
    {
        ScanJobExecution[] jobs = await LoadPairJobsAsync(binding.Pair, cancellationToken);
        if (jobs.Length != binding.Pair.DispatchedFileCount)
        {
            return null;
        }

        Pass1ExecutionWarningResponse[] warnings = await context.Pass1FailureInventoryEntries
            .AsNoTracking()
            .Where(warning => warning.PairExecutionId == binding.Pair.Id)
            .OrderBy(warning => warning.PairSelectionOrdinal)
            .ThenBy(warning => warning.FailureCode)
            .Select(warning => new Pass1ExecutionWarningResponse(
                warning.PairSelectionOrdinal,
                warning.FailureCategory.ToString(),
                warning.FailureCode))
            .ToArrayAsync(cancellationToken);

        return new Pass1ExecutionStatusResponse(
            binding.Pair.Id,
            binding.Pair.Phase.ToString(),
            binding.Pair.StateVersion,
            binding.Pair.ConcurrencyLimit,
            binding.Pair.ConcurrencyRevision,
            binding.Pair.DispatchedFileCount,
            jobs.Count(job => job.State == ScanJobExecutionState.Pending),
            jobs.Count(job => job.State == ScanJobExecutionState.Running),
            jobs.Count(job => job.State == ScanJobExecutionState.RetryScheduled),
            jobs.Count(job => job.State == ScanJobExecutionState.Completed),
            jobs.Count(job => job.State == ScanJobExecutionState.Failed),
            jobs.Count(job => job.State == ScanJobExecutionState.Cancelled),
            ManualResumeAction(binding.Pair.Phase),
            warnings);
    }

    internal async Task<PreparedPass1ConcurrencyUpdateResult> UpdateConcurrencyAsync(
        PreparedPass1ExecutionBinding binding,
        UpdatePass1ConcurrencyRequest request,
        string principalId,
        CancellationToken cancellationToken)
    {
        PreparedPass1ConcurrencyUpdateResult result = await store.UpdateConcurrencyAsync(
            binding.AuthorizationScope,
            binding.Pair.Id,
            request.ConcurrencyLimit,
            request.ExpectedVersion,
            principalId,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (result.Outcome is PreparedPass1ConcurrencyUpdateOutcome.Updated or
            PreparedPass1ConcurrencyUpdateOutcome.NoChange or
            PreparedPass1ConcurrencyUpdateOutcome.Replay)
        {
            executionActivator.Signal(binding.Pair.Id, binding.AuthorizationScope);
        }

        return result;
    }

    public async Task<PreparedPass1RecoveryResult> ResumeAsync(
        Guid pairExecutionId,
        PreparedScanAuthorizationScope authorizationScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);
        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled(pairExecutionId);
        }

        bool providerAvailable;
        try
        {
            providerAvailable = await providerProbe.IsAvailableAsync(pairExecutionId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Cancelled(pairExecutionId);
        }

        PreparedPass1RecoveryResult result = await store.RecoverAsync(
            pairExecutionId,
            authorizationScope,
            providerAvailable,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (result.Outcome is PreparedPass1RecoveryOutcome.Continued or
            PreparedPass1RecoveryOutcome.NoOpReplay &&
            result.PairExecutionId == pairExecutionId)
        {
            executionActivator.Signal(pairExecutionId, authorizationScope);
        }

        return result;
    }

    internal static TimeSpan GetProviderProbeWindow(int zeroBasedOrdinal) =>
        WorkerAgentProviderHealthProbe.GetProbeDelay(checked(zeroBasedOrdinal + 1));

    private async Task<ScanJobExecution[]> LoadPairJobsAsync(
        Pass1PairExecution pair,
        CancellationToken cancellationToken)
    {
        Guid[] fileCodeUnitIds = await context.PreparedScanManifestEntries.AsNoTracking()
            .Where(entry => entry.PreparedScanManifestId == pair.ManifestOneId ||
                entry.PreparedScanManifestId == pair.ManifestTwoId)
            .Select(entry => entry.FileCodeUnitId)
            .ToArrayAsync(cancellationToken);
        return await context.ScanJobExecutions.AsNoTracking()
            .Where(job => fileCodeUnitIds.Contains(job.FileCodeUnitId))
            .ToArrayAsync(cancellationToken);
    }

    private static string? ManualResumeAction(Pass1PairExecutionPhase phase) => phase switch
    {
        Pass1PairExecutionPhase.PausedProviderUnavailable => "继续扫描",
        Pass1PairExecutionPhase.PausedOperatorCorrection => "修正提供商配置后手动继续扫描",
        Pass1PairExecutionPhase.PausedInspection => "检查系统性失败后手动继续扫描",
        _ => null,
    };

    private static PreparedPass1RecoveryResult Cancelled(Guid pairExecutionId) => new(
        PreparedPass1RecoveryOutcome.Cancelled,
        DurableScanReason.Cancelled,
        pairExecutionId == Guid.Empty ? null : pairExecutionId,
        null,
        null,
        0,
        0,
        0,
        0,
        false,
        false,
        false,
        null);
}

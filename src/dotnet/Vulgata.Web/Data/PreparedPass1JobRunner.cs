using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Web.Data;

/// <summary>
/// Runs one durable claim through the current server-authorized packet source.
/// </summary>
public sealed class PreparedPass1JobRunner(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan _leaseDuration = TimeSpan.FromMinutes(30);

    internal async Task RunPairAsync(
        PreparedPass1ExecutionActivator.ActivationSlot slot,
        CancellationToken cancellationToken)
    {
        var runners = new Dictionary<Guid, Task>();
        while (!cancellationToken.IsCancellationRequested)
        {
            PreparedPass1ClaimResult claim;
            await using (AsyncServiceScope claimScope = scopeFactory.CreateAsyncScope())
            {
                claim = await claimScope.ServiceProvider
                    .GetRequiredService<IPreparedPass1Store>()
                    .ClaimNextAsync(
                        slot.Current.PairExecutionId,
                        timeProvider.GetUtcNow(),
                        _leaseDuration,
                        cancellationToken);
            }

            if (claim.Outcome == PreparedPass1ClaimOutcome.Claimed)
            {
                PreparedPass1Activation activation = slot.Current;
                runners.Add(
                    claim.JobId,
                    RunClaimAsync(activation, claim, cancellationToken));
                continue;
            }

            if (claim.Outcome is PreparedPass1ClaimOutcome.AtCapacity or
                PreparedPass1ClaimOutcome.Exhausted)
            {
                RemoveCompleted(runners);
                if (runners.Count > 0)
                {
                    Task wake = slot.WaitAsync(cancellationToken);
                    Task completed = await Task.WhenAny(runners.Values.Append(wake));
                    await completed;
                    RemoveCompleted(runners);
                    continue;
                }

                if (claim.Outcome == PreparedPass1ClaimOutcome.Exhausted)
                {
                    await CompleteAndReconcileAsync(slot.Current, cancellationToken);
                    return;
                }

                await slot.WaitAsync(cancellationToken);
                continue;
            }

            await DrainAsync(runners);
            return;
        }
    }

    private async Task RunClaimAsync(
        PreparedPass1Activation activation,
        PreparedPass1ClaimResult claim,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        PreparedPass1WorkerPacket? hydratedPacket = await services
            .GetRequiredService<IPreparedPass1ClaimedPacketSource>()
            .HydrateAsync(
                activation.PairExecutionId,
                claim,
                timeProvider.GetUtcNow(),
                cancellationToken);
        if (hydratedPacket is null)
        {
            await RecordFailureAsync(
                services, activation.PairExecutionId, claim,
                ScanJobFailureCategory.Integrity, cancellationToken);
            return;
        }

        PreparedPass1DispatchRequest request = CreateDispatchRequest(hydratedPacket);

        PreparedPass1DispatchResult dispatch;
        try
        {
            dispatch = await services.GetRequiredService<PreparedPass1DispatchWorker>()
                .ExecuteAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            await RecordFailureAsync(
                services, activation.PairExecutionId, claim,
                ScanJobFailureCategory.Integrity, cancellationToken);
            return;
        }

        PreparedPass1JobDispatchResult? result = dispatch.Results.SingleOrDefault(value =>
            value.JobId == claim.JobId && value.PairSelectionOrdinal == claim.PairSelectionOrdinal);
        PreparedPass1WorkerPacket? packet = dispatch.Packets.SingleOrDefault(value =>
            value.JobId == claim.JobId && value.PairSelectionOrdinal == claim.PairSelectionOrdinal);
        if (result is null || packet is null)
        {
            await RecordFailureAsync(
                services, activation.PairExecutionId, claim,
                ScanJobFailureCategory.Integrity, cancellationToken);
            return;
        }


        if (result.Outcome == PreparedPass1RawWorkerOutcome.ProviderUnavailable)
        {
            await services.GetRequiredService<IPreparedPass1Store>()
                .RecordProviderTransientAsync(
                    activation.PairExecutionId,
                    timeProvider.GetUtcNow(),
                    cancellationToken: cancellationToken);
            return;
        }

        if (result.Outcome != PreparedPass1RawWorkerOutcome.Completed || result.ParsedResult is null)
        {
            await RecordFailureAsync(
                services, activation.PairExecutionId, claim,
                ScanJobFailureCategory.Integrity, cancellationToken);
            return;
        }

        PreparedPass1CanonicalArtifact logical = PreparedPass1CanonicalCodec.CreateLogicalDocument(
            new(packet.RepositoryId, packet.RepositoryRelativePath, checked((ulong)packet.SegmentOrdinal)));
        PreparedPass1CanonicalArtifact version = PreparedPass1CanonicalCodec.CreateDocumentVersion(
            new(packet.JobId, Convert.FromHexString(packet.FileCodeUnitIdentityFingerprint), packet.DocumentSchemaIdentity));
        _ = await services.GetRequiredService<IPass1CodeLogicDocumentCommitter>().CommitAsync(
            new Pass1DocumentCommitRequest(
                packet,
                result.ParsedResult.CanonicalCandidateJson,
                logical.Id!.Value,
                version.Id!.Value,
                claim.JobStateVersion,
                claim.LeaseToken,
                timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private async Task CompleteAndReconcileAsync(
        PreparedPass1Activation activation,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        IPreparedPass1Store store = services.GetRequiredService<IPreparedPass1Store>();
        PreparedPass1CompletionResult completed = await store.CompletePass1Async(
            activation.PairExecutionId,
            activation.AuthorizationScope,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (completed.Outcome is not (PreparedPass1CompletionOutcome.Succeeded or
            PreparedPass1CompletionOutcome.CompletedWithWarnings or
            PreparedPass1CompletionOutcome.Replay))
        {
            return;
        }

        PairSnapshot? snapshot = await LoadEligibleReconciliationAsync(
            services.GetRequiredService<VulgataDbContext>(), activation, cancellationToken);
        if (snapshot is null)
        {
            return;
        }

        PreparedPass1ReconciliationResult reconciled = await store.ReconcileAsync(
            activation.PairExecutionId,
            new(
                activation.AuthorizationScope,
                snapshot.StateVersion,
                null,
                timeProvider.GetUtcNow()),
            cancellationToken);
        if (reconciled.Outcome != PreparedPass1ReconciliationOutcome.Conflict)
        {
            return;
        }

        PairSnapshot? reloaded = await LoadEligibleReconciliationAsync(
            services.GetRequiredService<VulgataDbContext>(), activation, cancellationToken);
        if (reloaded is null || reloaded != snapshot)
        {
            return;
        }

        _ = await store.ReconcileAsync(
            activation.PairExecutionId,
            new(
                activation.AuthorizationScope,
                reloaded.StateVersion,
                null,
                timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private static PreparedPass1DispatchRequest CreateDispatchRequest(
        PreparedPass1WorkerPacket packet)
    {
        PreparedPass1FileCodeUnitAssignment assignment = new(
            packet.PairExecutionId,
            packet.JobId,
            packet.ScanExecutionId,
            packet.SystemId,
            packet.RepositoryId,
            packet.ScanRunId,
            packet.ProfiledRevision,
            packet.FileCodeUnitId,
            packet.FileCodeUnitIdentityFingerprint,
            packet.PreparedScanManifestId,
            packet.PreparedScanManifestFingerprint,
            packet.PairPackageFingerprint,
            packet.RepositoryRelativePath,
            packet.ByteStart,
            packet.ByteEnd,
            packet.LineStart,
            packet.LineEnd,
            packet.SegmentOrdinal,
            packet.SegmentCount,
            packet.FileHash,
            packet.SourceHash,
            packet.PromptIdentity,
            packet.DocumentSchemaIdentity,
            packet.AnalysisRuntimeIdentityHash,
            packet.ContextSnapshot.ContextSnapshotId,
            packet.ManifestOrdinal,
            packet.PairSelectionOrdinal,
            packet.SourceUtf8)
        {
            RemainingNavigationBudget = packet.RemainingNavigationBudget,
        };
        return new(
            [assignment],
            [packet.ContextSnapshot],
            packet.CommunicationClassifications,
            1);
    }

    private static async Task<long> ReadJobVersionAsync(
        VulgataDbContext context,
        Guid jobId,
        CancellationToken cancellationToken) =>
        await context.ScanJobExecutions.AsNoTracking()
            .Where(value => value.Id == jobId)
            .Select(value => value.StateVersion)
            .SingleAsync(cancellationToken);

    private async Task RecordFailureAsync(
        IServiceProvider services,
        Guid pairExecutionId,
        PreparedPass1ClaimResult claim,
        ScanJobFailureCategory category,
        CancellationToken cancellationToken)
    {
        long version = await ReadJobVersionAsync(
            services.GetRequiredService<VulgataDbContext>(), claim.JobId, cancellationToken);
        _ = await services.GetRequiredService<IPreparedPass1Store>().RecordFailureAsync(
            pairExecutionId,
            claim.JobId,
            version,
            claim.LeaseToken,
            category,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static async Task<PairSnapshot?> LoadEligibleReconciliationAsync(
        VulgataDbContext context,
        PreparedPass1Activation activation,
        CancellationToken cancellationToken)
    {
        Pass1PairExecution? pair = await context.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == activation.PairExecutionId, cancellationToken);
        if (pair is null || pair.Phase != Pass1PairExecutionPhase.Reconciling ||
            pair.CircuitState != Pass1CircuitState.Closed ||
            pair.SystemId != activation.AuthorizationScope.SystemId)
        {
            return null;
        }

        Guid[] authorizedRepositories = activation.AuthorizationScope.RepositoryIds.Distinct().Order().ToArray();
        PreparedScanManifest[] manifests = await context.PreparedScanManifests.AsNoTracking()
            .Where(value => value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId)
            .OrderBy(value => value.RepositoryId)
            .ToArrayAsync(cancellationToken);
        bool authorized = await context.SystemOwnerAssignments.AsNoTracking().AnyAsync(value =>
            value.SystemId == pair.SystemId &&
            value.UserId == activation.AuthorizationScope.UserId.ToString("D"),
            cancellationToken);
        if (!authorized || manifests.Length != 2 ||
            !manifests.Select(value => value.RepositoryId).SequenceEqual(authorizedRepositories) ||
            manifests.Any(value => value.ApprovalState != PreparedScanManifestApprovalState.Approved ||
                value.CoordinationId != pair.CoordinationId ||
                value.PairSelectionFingerprint != pair.PairSelectionFingerprint))
        {
            return null;
        }

        return new(pair.StateVersion, pair.PairPackageFingerprint, pair.PairSelectionFingerprint);
    }

    private static void RemoveCompleted(Dictionary<Guid, Task> runners)
    {
        foreach (Guid jobId in runners.Where(value => value.Value.IsCompleted).Select(value => value.Key).ToArray())
        {
            runners.Remove(jobId);
        }
    }

    private static async Task DrainAsync(Dictionary<Guid, Task> runners)
    {
        if (runners.Count > 0)
        {
            await Task.WhenAll(runners.Values);
            runners.Clear();
        }
    }

    private sealed record PairSnapshot(
        long StateVersion,
        string PairPackageFingerprint,
        string PairSelectionFingerprint);
}

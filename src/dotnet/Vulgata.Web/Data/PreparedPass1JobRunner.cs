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

    private static async Task<bool> EnsureNavigationCallsMarkerAsync(
        IServiceProvider services,
        PreparedPass1Activation activation,
        PreparedPass1ClaimResult claim,
        CancellationToken cancellationToken)
    {
        IPass1NavigationCallsMarkerRepairStore markerStore =
            services.GetRequiredService<IPass1NavigationCallsMarkerRepairStore>();
        return await markerStore.EnsureNavigationCallsMarkerAsync(
            new(
                activation.PairExecutionId,
                claim.JobId,
                claim.JobStateVersion,
                claim.PairStateVersion,
                claim.LeaseToken,
                claim.LeaseExpiresAt,
                PreparedPass1AuthorizationBinding.Create(activation.AuthorizationScope)),
            cancellationToken);
    }

    internal async Task RunPairAsync(
        PreparedPass1ExecutionActivator.ActivationSlot slot,
        CancellationToken cancellationToken)
    {
        var runners = new Dictionary<Guid, Task>();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                PreparedPass1Activation observedActivation = slot.Current;
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
                        await WaitForLocalCompletionOrActivationAsync(
                            slot,
                            observedActivation,
                            runners.Values,
                            cancellationToken);
                        RemoveCompleted(runners);
                        continue;
                    }

                    if (claim.Outcome == PreparedPass1ClaimOutcome.Exhausted)
                    {
                        if (await CompleteAndReconcileAsync(slot.Current, cancellationToken))
                        {
                            return;
                        }

                        await slot.WaitAsync(cancellationToken);
                        continue;
                    }

                    await WaitForActivationOrEligibilityAsync(
                        slot,
                        observedActivation,
                        claim.NextEligibleAt,
                        cancellationToken);
                    continue;
                }

                if (claim.Outcome == PreparedPass1ClaimOutcome.Ineligible)
                {
                    RemoveCompleted(runners);
                    if (runners.Count > 0)
                    {
                        await WaitForLocalCompletionOrActivationAsync(
                            slot,
                            observedActivation,
                            runners.Values,
                            cancellationToken);
                        RemoveCompleted(runners);
                        continue;
                    }

                    if (await CompleteAndReconcileAsync(slot.Current, cancellationToken))
                    {
                        return;
                    }

                    if (claim.NextEligibleAt is not null)
                    {
                        await WaitForActivationOrEligibilityAsync(
                            slot,
                            observedActivation,
                            claim.NextEligibleAt,
                            cancellationToken);
                        continue;
                    }
                }

                return;
            }
        }
        finally
        {
            await DrainAsync(runners);
        }
    }

    private async Task RunClaimAsync(
        PreparedPass1Activation activation,
        PreparedPass1ClaimResult claim,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        if (!await EnsureNavigationCallsMarkerAsync(
                services,
                activation,
                claim,
                cancellationToken))
        {
            return;
        }

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

        hydratedPacket = hydratedPacket with
        {
            ServerAuthorization = new PreparedPass1ServerAuthorization(
                activation.PairExecutionId,
                claim.JobStateVersion,
                claim.PairStateVersion,
                claim.LeaseToken,
                claim.LeaseExpiresAt,
                nameof(Pass1PairExecutionPhase.Running),
                Pass1CircuitState.Closed,
                ScanJobExecutionState.Running,
                PreparedPass1AuthorizationBinding.Create(activation.AuthorizationScope)),
        };

        if (!await EnsureNavigationCallsMarkerAsync(
                services,
                activation,
                claim,
                cancellationToken))
        {
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
            if (await EnsureNavigationCallsMarkerAsync(
                    services,
                    activation,
                    claim,
                    cancellationToken))
            {
                await RecordFailureAsync(
                    services, activation.PairExecutionId, claim,
                    ScanJobFailureCategory.Integrity, cancellationToken);
            }
            return;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (await EnsureNavigationCallsMarkerAsync(
                    services,
                    activation,
                    claim,
                    cancellationToken))
            {
                await RecordFailureAsync(
                    services, activation.PairExecutionId, claim,
                    ScanJobFailureCategory.Unclassified, cancellationToken);
            }
            return;
        }

        if (!await EnsureNavigationCallsMarkerAsync(
                services,
                activation,
                claim,
                cancellationToken))
        {
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
            if (string.Equals(result.ReasonCode, "provider-configuration", StringComparison.Ordinal))
            {
                await RecordFailureAsync(
                    services, activation.PairExecutionId, claim,
                    ScanJobFailureCategory.ProviderConfiguration, cancellationToken);
            }
            else if (string.Equals(result.ReasonCode, "provider-unavailable", StringComparison.Ordinal))
            {
                await services.GetRequiredService<IPreparedPass1Store>()
                    .RecordFailureAsync(
                        activation.PairExecutionId,
                        claim.JobId,
                        claim.JobStateVersion,
                        claim.LeaseToken,
                        ScanJobFailureCategory.ProviderTransient,
                        timeProvider.GetUtcNow(),
                        cancellationToken);
            }
            else
            {
                await RecordFailureAsync(
                    services, activation.PairExecutionId, claim,
                    ScanJobFailureCategory.Unclassified, cancellationToken);
            }
            return;
        }

        if (result.Outcome != PreparedPass1RawWorkerOutcome.Completed || result.ParsedResult is null)
        {
            await RecordFailureAsync(
                services, activation.PairExecutionId, claim,
                ClassifyRejectedResult(result), cancellationToken);
            return;
        }

        PreparedPass1CanonicalArtifact logical = PreparedPass1CanonicalCodec.CreateLogicalDocument(
            new(packet.RepositoryId, packet.RepositoryRelativePath, checked((ulong)packet.SegmentOrdinal)));
        PreparedPass1CanonicalArtifact version = PreparedPass1CanonicalCodec.CreateDocumentVersion(
            new(packet.JobId, Convert.FromHexString(packet.FileCodeUnitIdentityFingerprint), packet.DocumentSchemaIdentity));
        Pass1DocumentCommitResult commit;
        try
        {
            commit = await services.GetRequiredService<IPass1CodeLogicDocumentCommitter>().CommitAsync(
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
        catch (Exception exception) when (
            exception is InvalidDataException or ArgumentException or OverflowException)
        {
            await RecordFailureAsync(
                services, activation.PairExecutionId, claim,
                ScanJobFailureCategory.Integrity, cancellationToken);
            return;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordFailureAsync(
                services, activation.PairExecutionId, claim,
                ScanJobFailureCategory.Unclassified, cancellationToken);
            return;
        }

        if (commit.Status is not (Pass1DocumentCommitStatus.Committed or
            Pass1DocumentCommitStatus.Replayed))
        {
            await RecordFailureAsync(
                services, activation.PairExecutionId, claim,
                commit.Status is Pass1DocumentCommitStatus.Rejected or
                    Pass1DocumentCommitStatus.Conflict
                    ? ScanJobFailureCategory.Integrity
                    : ScanJobFailureCategory.Unclassified,
                cancellationToken);
        }
    }

    private async Task<bool> CompleteAndReconcileAsync(
        PreparedPass1Activation activation,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        IPreparedPass1Store store = services.GetRequiredService<IPreparedPass1Store>();
        PairSnapshot? snapshot = await LoadEligibleReconciliationAsync(
            services, activation, cancellationToken);
        if (snapshot is null)
        {
            return false;
        }

        PreparedPass1ReconciliationResult reconciled = await store.ReconcileAsync(
            activation.PairExecutionId,
            new(
                activation.AuthorizationScope,
                snapshot.StateVersion,
                null,
                timeProvider.GetUtcNow()),
            cancellationToken);
        if (reconciled.Outcome is PreparedPass1ReconciliationOutcome.Reconciled or
            PreparedPass1ReconciliationOutcome.Replay)
        {
            return true;
        }

        if (reconciled.Outcome != PreparedPass1ReconciliationOutcome.Conflict)
        {
            return false;
        }

        PairSnapshot? reloaded = await LoadEligibleReconciliationAsync(
            services, activation, cancellationToken);
        if (reloaded is null || reloaded != snapshot)
        {
            return true;
        }

        PreparedPass1ReconciliationResult retried = await store.ReconcileAsync(
            activation.PairExecutionId,
            new(
                activation.AuthorizationScope,
                reloaded.StateVersion,
                null,
                timeProvider.GetUtcNow()),
            cancellationToken);
        return retried.Outcome is PreparedPass1ReconciliationOutcome.Reconciled or
            PreparedPass1ReconciliationOutcome.Replay;
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
            NavigationBudget = packet.NavigationBudget,
            ServerAuthorization = packet.ServerAuthorization,
        };
        return new(
            [assignment],
            [packet.ContextSnapshot],
            packet.CommunicationClassifications,
            1);
    }


    private static ScanJobFailureCategory ClassifyRejectedResult(
        PreparedPass1JobDispatchResult result) => result.ReasonCode switch
        {
            "integrity-failure" => ScanJobFailureCategory.Integrity,
            "security-failure" => ScanJobFailureCategory.Security,
            "provider-configuration" => ScanJobFailureCategory.ProviderConfiguration,
            "cancelled" => ScanJobFailureCategory.Cancellation,
            "unclassified-systemic" => ScanJobFailureCategory.Unclassified,
            "worker-result-rejected" or "invalid-or-repeated-tool-call" =>
                ScanJobFailureCategory.JobTransient,
            _ => ScanJobFailureCategory.Unclassified,
        };

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
        IServiceProvider services,
        PreparedPass1Activation activation,
        CancellationToken cancellationToken)
    {
        VulgataDbContext context = services.GetRequiredService<VulgataDbContext>();
        Pass1PairExecution? pair = await context.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == activation.PairExecutionId, cancellationToken);
        bool policyDrain = pair is not null &&
            pair.Phase == Pass1PairExecutionPhase.Running &&
            pair.CircuitState == Pass1CircuitState.Paused &&
            pair.ReasonCategory == Pass1PairReasonCategory.Pass1Policy &&
            string.Equals(pair.ReasonCode, "pass1-policy-failed", StringComparison.Ordinal);
        if (pair is null || pair.Phase != Pass1PairExecutionPhase.Running ||
            (pair.CircuitState != Pass1CircuitState.Closed && !policyDrain) ||
            pair.SystemId != activation.AuthorizationScope.SystemId)
        {
            return null;
        }

        Guid[] authorizedRepositories = activation.AuthorizationScope.RepositoryIds.Distinct().ToArray();
        PreparedScanInitiationAuthorizationResult freshAuthorization = await services
            .GetRequiredService<IPreparedScanInitiationAuthorizationRechecker>()
            .RecheckAsync(
                activation.AuthorizationScope.UserId.ToString("D"),
                pair.SystemId,
                authorizedRepositories,
                cancellationToken);
        PreparedScanManifest[] manifests = await context.PreparedScanManifests.AsNoTracking()
            .Where(value => value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId)
            .OrderBy(value => value.RepositoryId)
            .ToArrayAsync(cancellationToken);
        if (!freshAuthorization.IsAuthorized ||
            freshAuthorization.UserId != activation.AuthorizationScope.UserId ||
            !freshAuthorization.RepositoryIds.Order().SequenceEqual(authorizedRepositories.Order()) ||
            manifests.Length != 2 || authorizedRepositories.Length != 2 ||
            manifests.Any(value => !authorizedRepositories.Contains(value.RepositoryId)) ||
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

    private static async Task WaitForLocalCompletionOrActivationAsync(
        PreparedPass1ExecutionActivator.ActivationSlot slot,
        PreparedPass1Activation observedActivation,
        IEnumerable<Task> runners,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using var wakeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task wake = slot.WaitAsync(wakeCancellation.Token);
            Task completed = await Task.WhenAny(runners.Append(wake));
            if (completed != wake)
            {
                wakeCancellation.Cancel();
            }

            try
            {
                await completed;
            }
            finally
            {
                wakeCancellation.Cancel();
                if (completed != wake)
                {
                    try
                    {
                        await wake;
                    }
                    catch (OperationCanceledException) when (wakeCancellation.IsCancellationRequested)
                    {
                    }
                }
            }

            if (completed != wake || !ReferenceEquals(observedActivation, slot.Current))
            {
                return;
            }
        }
    }

    private async Task WaitForActivationOrEligibilityAsync(
        PreparedPass1ExecutionActivator.ActivationSlot slot,
        PreparedPass1Activation observedActivation,
        DateTimeOffset? nextEligibleAt,
        CancellationToken cancellationToken)
    {
        if (nextEligibleAt is null)
        {
            await slot.WaitAsync(cancellationToken);
            return;
        }

        TimeSpan delay = nextEligibleAt.Value - timeProvider.GetUtcNow();
        if (delay <= TimeSpan.Zero)
        {
            delay = TimeSpan.FromMilliseconds(1);
        }

        while (true)
        {
            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task activation = slot.WaitAsync(waitCancellation.Token);
            Task eligibility = Task.Delay(delay, timeProvider, waitCancellation.Token);
            Task completed = await Task.WhenAny(activation, eligibility);
            try
            {
                await completed;
            }
            finally
            {
                waitCancellation.Cancel();
                if (completed == eligibility)
                {
                    try
                    {
                        await activation;
                    }
                    catch (OperationCanceledException) when (waitCancellation.IsCancellationRequested)
                    {
                    }
                }
            }

            if (completed == eligibility || !ReferenceEquals(observedActivation, slot.Current))
            {
                return;
            }
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

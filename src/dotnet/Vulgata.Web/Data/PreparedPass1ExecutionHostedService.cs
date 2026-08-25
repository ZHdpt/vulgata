using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

public sealed class PreparedPass1ExecutionHostedService(
    PreparedPass1ExecutionActivator activator,
    IServiceScopeFactory scopeFactory,
    IPreparedPass1RevisionBindingRegistry revisionBindings,
    TimeProvider timeProvider,
    ILogger<PreparedPass1ExecutionHostedService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, Task> _pumps = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (Guid pairExecutionId in activator.ReadActivatedPairsAsync(stoppingToken))
        {
            if (_pumps.TryGetValue(pairExecutionId, out Task? existing) && !existing.IsCompleted)
            {
                continue;
            }

            Task pump = RunPairAsync(pairExecutionId, stoppingToken);
            _pumps[pairExecutionId] = pump;
            _ = pump.ContinueWith(
                (_, state) => ((ConcurrentDictionary<Guid, Task>)state!).TryRemove(
                    pairExecutionId, out _),
                _pumps,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        Task[] pumps = [.. _pumps.Values];
        await Task.WhenAll(pumps).WaitAsync(cancellationToken);
    }

    private async Task RunPairAsync(Guid pairExecutionId, CancellationToken cancellationToken)
    {
        try
        {
            PreparedPass1ExecutionActivator.ActivationSlot? slot = activator.GetSlot(pairExecutionId);
            if (slot is null)
            {
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                Pass1PairExecutionPhase? phase = await ReadPairPhaseAsync(
                    pairExecutionId,
                    cancellationToken);
                if (phase == Pass1PairExecutionPhase.ProbingProvider)
                {
                    phase = await ProbeUntilTransitionAsync(pairExecutionId, slot, cancellationToken);
                    if (phase != Pass1PairExecutionPhase.Running)
                    {
                        return;
                    }
                }

                if (phase is not (
                        Pass1PairExecutionPhase.Running or
                        Pass1PairExecutionPhase.Reconciling))
                {
                    return;
                }

                IReadOnlyList<PreparedRevisionRequest>? requests = await LoadRevisionRequestsAsync(
                    pairExecutionId,
                    cancellationToken);
                if (requests is null || await revisionBindings.AcquirePairAsync(
                        pairExecutionId,
                        requests,
                        cancellationToken) is null)
                {
                    await FailRevisionBindingAsync(slot, cancellationToken);
                    logger.LogError(
                        "Prepared Pass 1 exact revision binding was rejected for {PairExecutionId}.",
                        pairExecutionId);
                    return;
                }

                try
                {
                    await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<PreparedPass1JobRunner>()
                        .RunPairAsync(slot, cancellationToken);
                }
                finally
                {
                    await revisionBindings.ReleasePairAsync(pairExecutionId);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Prepared Pass 1 pair pump stopped unexpectedly for {PairExecutionId}.", pairExecutionId);
        }
    }

    private async Task<Pass1PairExecutionPhase?> ReadPairPhaseAsync(
        Guid pairExecutionId,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<VulgataDbContext>()
            .Pass1PairExecutions.AsNoTracking()
            .Where(value => value.Id == pairExecutionId)
            .Select(value => (Pass1PairExecutionPhase?)value.Phase)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Pass1PairExecutionPhase?> ProbeUntilTransitionAsync(
        Guid pairExecutionId,
        PreparedPass1ExecutionActivator.ActivationSlot slot,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        VulgataDbContext context = services.GetRequiredService<VulgataDbContext>();
        WorkerAgentProviderResolver providerResolver =
            services.GetRequiredService<WorkerAgentProviderResolver>();
        ISecretEncryptionService encryption = services.GetRequiredService<ISecretEncryptionService>();
        PreparedPass1ProbeDriver driver = services.GetRequiredService<PreparedPass1ProbeDriver>();
        IPreparedPass1ProviderTransitionStore transitions =
            services.GetRequiredService<IPreparedPass1ProviderTransitionStore>();

        ResolvedWorkerAgentProvider? provider = null;
        string? apiKey = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            ProviderProbeTicket? ticket = await context.Pass1PairExecutions.AsNoTracking()
                .Where(value => value.Id == pairExecutionId)
                .Select(value => new ProviderProbeTicket(
                    value.SystemId,
                    value.Phase,
                    value.StateVersion,
                    value.ProbeOrdinal,
                    value.NextProbeAt))
                .SingleOrDefaultAsync(cancellationToken);
            if (ticket is null || ticket.Phase != Pass1PairExecutionPhase.ProbingProvider ||
                ticket.NextProbeAt is null)
            {
                return ticket?.Phase;
            }

            if (provider is null || apiKey is null)
            {
                try
                {
                    provider = await providerResolver.ResolveAsync(ticket.SystemId, cancellationToken);
                    apiKey = await encryption.DecryptAsync(
                        SecretPurposes.LlmApiKey,
                        provider.EncryptedApiKey,
                        cancellationToken);
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or ArgumentException)
                {
                    PreparedPass1ProviderCircuitResult configuration =
                        await transitions.ProbeProviderAsync(
                            pairExecutionId,
                            ticket.StateVersion,
                            ticket.ProbeOrdinal,
                            new PreparedPass1ProviderHealthResult(
                                PreparedPass1ProviderHealthOutcome.ProviderConfiguration,
                                null),
                            timeProvider.GetUtcNow(),
                            cancellationToken);
                    return configuration.Phase;
                }
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            if (ticket.NextProbeAt.Value > now)
            {
                using var waitCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task activation = slot.WaitAsync(waitCancellation.Token);
                Task due = Task.Delay(
                    ticket.NextProbeAt.Value - now,
                    timeProvider,
                    waitCancellation.Token);
                Task completed = await Task.WhenAny(activation, due);
                try
                {
                    await completed;
                }
                finally
                {
                    waitCancellation.Cancel();
                    Task pending = completed == activation ? due : activation;
                    if (pending == activation)
                    {
                        try
                        {
                            await pending;
                        }
                        catch (OperationCanceledException) when (waitCancellation.IsCancellationRequested)
                        {
                        }
                    }
                }

                continue;
            }

            PreparedPass1ProviderCircuitResult result = await driver.ProbeAsync(
                pairExecutionId,
                ticket.StateVersion,
                ticket.ProbeOrdinal,
                ticket.NextProbeAt.Value,
                provider.Endpoint,
                provider.Model,
                apiKey,
                cancellationToken);
            if (result.Phase != Pass1PairExecutionPhase.ProbingProvider)
            {
                return result.Phase;
            }
        }

        return null;
    }

    private async Task FailRevisionBindingAsync(
        PreparedPass1ExecutionActivator.ActivationSlot slot,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        VulgataDbContext context = services.GetRequiredService<VulgataDbContext>();
        long? stateVersion = await context.Pass1PairExecutions.AsNoTracking()
            .Where(value => value.Id == slot.Current.PairExecutionId)
            .Select(value => (long?)value.StateVersion)
            .SingleOrDefaultAsync(cancellationToken);
        if (stateVersion.HasValue)
        {
            _ = await services.GetRequiredService<IPreparedPass1Store>()
                .FailRevisionBindingAsync(
                    slot.Current.PairExecutionId,
                    slot.Current.AuthorizationScope,
                    stateVersion.Value,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
        }
    }

    private async Task<IReadOnlyList<PreparedRevisionRequest>?> LoadRevisionRequestsAsync(
        Guid pairExecutionId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        Pass1PairExecution? pair = await context.Pass1PairExecutions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == pairExecutionId, cancellationToken);
        if (pair is null || pair.Phase is not (
                Pass1PairExecutionPhase.Running or Pass1PairExecutionPhase.Reconciling))
        {
            return null;
        }

        PreparedScanManifest[] manifests = await context.PreparedScanManifests.AsNoTracking()
            .Where(value => value.Id == pair.ManifestOneId || value.Id == pair.ManifestTwoId)
            .OrderBy(value => value.RepositoryId)
            .ToArrayAsync(cancellationToken);
        if (manifests.Length != 2 ||
            manifests.Select(value => value.RepositoryId).Distinct().Count() != 2 ||
            pair.ManifestOneId != manifests[0].Id ||
            pair.ManifestTwoId != manifests[1].Id ||
            manifests.Any(value =>
                value.ApprovalState != PreparedScanManifestApprovalState.Approved ||
                !string.Equals(
                    value.SchemaIdentity,
                    PreparedScanManifest.CurrentSchemaIdentity,
                    StringComparison.Ordinal) ||
                value.CoordinationId != pair.CoordinationId ||
                !string.Equals(
                    value.PairSelectionFingerprint,
                    pair.PairSelectionFingerprint,
                    StringComparison.Ordinal)))
        {
            return null;
        }

        TwoRepositoryScanCoordination? coordination =
            await context.TwoRepositoryScanCoordinations.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == pair.CoordinationId, cancellationToken);
        ScanExecution[] executions = coordination is null
            ? []
            : await context.ScanExecutions.AsNoTracking()
                .Where(value =>
                    value.Id == coordination.ScanExecutionOneId ||
                    value.Id == coordination.ScanExecutionTwoId)
                .OrderBy(value => value.RepositoryId)
                .ToArrayAsync(cancellationToken);
        if (coordination is null || executions.Length != 2 ||
            coordination.SystemId != pair.SystemId ||
            coordination.RepositoryOneId != manifests[0].RepositoryId ||
            coordination.RepositoryTwoId != manifests[1].RepositoryId ||
            coordination.ScanRunOneId != manifests[0].ScanRunId ||
            coordination.ScanRunTwoId != manifests[1].ScanRunId ||
            coordination.ScanExecutionOneId != executions[0].Id ||
            coordination.ScanExecutionTwoId != executions[1].Id)
        {
            return null;
        }

        for (int index = 0; index < 2; index++)
        {
            PreparedScanManifest manifest = manifests[index];
            ScanExecution execution = executions[index];
            string expectedPrompt = index == 0
                ? pair.PromptIdentityOne
                : pair.PromptIdentityTwo;
            string expectedRuntime = index == 0
                ? pair.AnalysisRuntimeIdentityHashOne
                : pair.AnalysisRuntimeIdentityHashTwo;
            if (execution.SystemId != pair.SystemId ||
                execution.RepositoryId != manifest.RepositoryId ||
                execution.ScanRunId != manifest.ScanRunId ||
                !string.Equals(
                    execution.ProfiledRevision,
                    manifest.ProfiledRevision,
                    StringComparison.Ordinal) ||
                !string.Equals(execution.PromptIdentity, expectedPrompt, StringComparison.Ordinal) ||
                !string.Equals(
                    execution.DocumentSchemaIdentity,
                    pair.DocumentSchemaIdentity,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    execution.DocumentSchemaIdentity,
                    "code-logic/file-v2",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    execution.AnalysisRuntimeIdentityHash,
                    expectedRuntime,
                    StringComparison.Ordinal) ||
                !string.Equals(manifest.RuntimeIdentityHash, expectedRuntime, StringComparison.Ordinal) ||
                manifest.CodeGraphRepositoryId != manifest.RepositoryId ||
                manifest.CodeGraphScanRunId != manifest.ScanRunId ||
                !string.Equals(
                    manifest.CodeGraphProfiledRevision,
                    manifest.ProfiledRevision,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.CodeGraphRuntimeIdentityHash,
                    manifest.RuntimeIdentityHash,
                    StringComparison.Ordinal) ||
                manifest.JavaReadinessRepositoryId != manifest.RepositoryId ||
                manifest.JavaReadinessScanRunId != manifest.ScanRunId ||
                !string.Equals(
                    manifest.JavaReadinessProfiledRevision,
                    manifest.ProfiledRevision,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.JavaReadinessRuntimeIdentityHash,
                    manifest.RuntimeIdentityHash,
                    StringComparison.Ordinal))
            {
                return null;
            }
        }

        try
        {
            PreparedPass1CanonicalArtifact identity = PreparedPass1CanonicalCodec.CreatePairExecution(new(
                pair.CoordinationId,
                pair.SystemId,
                Convert.FromHexString(pair.PairPackageFingerprint),
                Convert.FromHexString(pair.PairSelectionFingerprint),
                manifests.Select((manifest, index) => new PreparedPass1ManifestBinding(
                    manifest.RepositoryId,
                    manifest.ScanRunId,
                    executions[index].Id,
                    manifest.Id,
                    Convert.FromHexString(manifest.Fingerprint),
                    Convert.FromHexString(manifest.ProfiledRevision),
                    Convert.FromHexString(manifest.PreparedScanContextIdentityHash))).ToArray(),
                executions.Select(value => value.PromptIdentity!).ToArray(),
                pair.DocumentSchemaIdentity,
                executions.Select(value =>
                    (ReadOnlyMemory<byte>)Convert.FromHexString(
                        value.AnalysisRuntimeIdentityHash!)).ToArray()));
            if (identity.Id != pair.Id ||
                !string.Equals(
                    identity.FingerprintHex,
                    pair.Pass1PairFingerprint,
                    StringComparison.Ordinal))
            {
                return null;
            }
        }
        catch (ArgumentException)
        {
            return null;
        }

        Guid[] repositoryIds = [.. manifests.Select(value => value.RepositoryId)];
        Dictionary<Guid, RepositoryBinding> repositories = await context.Repositories.AsNoTracking()
            .Where(value => repositoryIds.Contains(value.Id))
            .ToDictionaryAsync(
                value => value.Id,
                value => new RepositoryBinding(value.SystemId, value.GitUrl),
                cancellationToken);
        if (repositories.Count != 2 || repositories.Values.Any(value => value.SystemId != pair.SystemId))
        {
            return null;
        }

        Guid[] scanRunIds = [.. manifests.Select(value => value.ScanRunId)];
        ScanRunBinding[] scanRuns = await context.ScanRuns.AsNoTracking()
            .Where(value => scanRunIds.Contains(value.Id))
            .Select(value => new ScanRunBinding(
                value.Id,
                value.RepositoryId,
                value.ProfiledRevision))
            .ToArrayAsync(cancellationToken);
        PreScanResult[] preScans = await context.PreScanResults.AsNoTracking()
            .Where(value => scanRunIds.Contains(value.ScanRunId))
            .ToArrayAsync(cancellationToken);
        if (scanRuns.Length != 2 || preScans.Length != 2 || manifests.Any(manifest =>
                !scanRuns.Any(scan =>
                    scan.Id == manifest.ScanRunId &&
                    scan.RepositoryId == manifest.RepositoryId &&
                    string.Equals(scan.ProfiledRevision, manifest.ProfiledRevision, StringComparison.Ordinal)) ||
                !preScans.Any(preScan =>
                    preScan.ScanRunId == manifest.ScanRunId &&
                    preScan.RepositoryId == manifest.RepositoryId &&
                    preScan.State == PreScanApprovalState.PreScanApproved &&
                    preScan.IsPreparedV2 &&
                    preScan.PreparedScanManifestId == manifest.Id &&
                    string.Equals(
                        preScan.PreparedScanManifestFingerprint,
                        manifest.Fingerprint,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        preScan.PairPackageFingerprint,
                        pair.PairPackageFingerprint,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        preScan.ProfiledRevision,
                        manifest.ProfiledRevision,
                        StringComparison.Ordinal))))
        {
            return null;
        }

        return [.. manifests.Select(manifest => new PreparedRevisionRequest(
            manifest.RepositoryId,
            manifest.ScanRunId,
            repositories[manifest.RepositoryId].GitUrl,
            manifest.RequestedRef,
            manifest.ProfiledRevision))];
    }

    private sealed record RepositoryBinding(Guid? SystemId, string GitUrl);

    private sealed record ScanRunBinding(
        Guid Id,
        Guid RepositoryId,
        string? ProfiledRevision);

    private sealed record ProviderProbeTicket(
        Guid SystemId,
        Pass1PairExecutionPhase Phase,
        long StateVersion,
        int ProbeOrdinal,
        DateTimeOffset? NextProbeAt);
}

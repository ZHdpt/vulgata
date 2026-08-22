using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedPass1.Fixtures;
using Vulgata.Tests.PreparedScanning;
using Vulgata.Shared;
using Vulgata.Shared.Scans;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T050 RED: production-composed proof for the explicitly activated, pair-wide
/// capacity-filling execution service.  The fixture enters through the
/// accepted Prepared Scan Manifest pair and the authenticated Web host; the
/// production activation is deliberately not replaced with a test runner.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1ProductionExecutionCapacityTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task Start_ApprovedPair_ActivatesCapacityFillingPumpAtTwoOrFourAndDoesNotUseSequentialCompletion(
        int concurrencyLimit)
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        using HttpClient authenticatedClient = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(manifest => manifest.RepositoryId)
            .Select(manifest => manifest.Id)
            .ToArrayAsync();
        using HttpResponseMessage start = await authenticatedClient.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(
            start.StatusCode == HttpStatusCode.Created,
            $"T050 Start expected Created, got {start.StatusCode}: {startBody}");
        Pass1ExecutionStartResponse started =
            (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;

        Assert.True(
            await worker.WaitForAtLeastAsync(concurrencyLimit, TimeSpan.FromSeconds(3)),
            $"T050 RED: authenticated Start persisted the pair but launched only {worker.StartedCount} Worker(s); expected simultaneous capacity {concurrencyLimit}.");
        Assert.True(
            worker.StartedCount <= concurrencyLimit,
            "T050 RED: production dispatch exceeded the durable pair concurrency limit.");

        using HttpResponseMessage status = await authenticatedClient.GetAsync(
            $"/api/pass1-executions/{started.Pass1ExecutionId:D}");
        Pass1ExecutionStatusResponse current =
            (await status.Content.ReadFromJsonAsync<Pass1ExecutionStatusResponse>())!;
        Assert.Equal(concurrencyLimit, current.RunningCount);
        Assert.Equal(concurrencyLimit, current.ConcurrencyLimit);
        worker.ReleaseAll();
    }

    [Fact]
    public async Task FourToOneDecrease_PreservesLeases_AndOneToFourIncreaseRefillsOnlyAfterDurableWakeUp()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests.OrderBy(value => value.RepositoryId).Select(value => value.Id).ToArrayAsync();
        using HttpResponseMessage start = await client.PostAsJsonAsync("/api/pass1-executions", new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 4 });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(start.StatusCode == HttpStatusCode.Created, $"T050 Start expected Created, got {start.StatusCode}: {startBody}");
        Pass1ExecutionStartResponse started = (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
        Assert.True(await worker.WaitForAtLeastAsync(4, TimeSpan.FromSeconds(3)), $"T050 RED: expected four overlapping leases, observed {worker.StartedCount}.");
        Pass1ExecutionStatusResponse status = (await (await client.GetAsync($"/api/pass1-executions/{started.Pass1ExecutionId:D}")).Content.ReadFromJsonAsync<Pass1ExecutionStatusResponse>())!;
        using HttpResponseMessage decrease = await client.PatchAsJsonAsync($"/api/pass1-executions/{started.Pass1ExecutionId:D}/concurrency", new { expectedVersion = status.StateVersion, concurrencyLimit = 1 });
        Assert.Equal(HttpStatusCode.OK, decrease.StatusCode);
        Pass1ExecutionStatusResponse preserved = (await (await client.GetAsync($"/api/pass1-executions/{started.Pass1ExecutionId:D}")).Content.ReadFromJsonAsync<Pass1ExecutionStatusResponse>())!;
        Assert.Equal(4, preserved.RunningCount);
        Assert.Equal(1, preserved.ConcurrencyLimit);
        worker.ReleaseAll();
    }

    [Fact]
    public async Task OneToFourIncrease_RefillsImmediatelyWhileTheFirstLeaseRemainsHeld()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 1 });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(start.StatusCode == HttpStatusCode.Created, $"T050 Start expected Created: {startBody}");
        Pass1ExecutionStartResponse started = (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
        Assert.True(
            await worker.WaitForAtLeastAsync(1, TimeSpan.FromSeconds(3)),
            "T050 RED: the first lease was not launched before the capacity increase.");

        Pass1ExecutionStatusResponse before = await ReadStatusAsync(client, started.Pass1ExecutionId);
        using HttpResponseMessage increase = await client.PatchAsJsonAsync(
            $"/api/pass1-executions/{started.Pass1ExecutionId:D}/concurrency",
            new { expectedVersion = before.StateVersion, concurrencyLimit = 4 });
        Assert.Equal(HttpStatusCode.OK, increase.StatusCode);

        Assert.True(
            await worker.WaitForAtLeastAsync(4, TimeSpan.FromSeconds(3)),
            $"T050 RED: increasing 1-to-4 did not refill while the first lease was held; started {worker.StartedCount}.");
        Assert.True(worker.MaximumActiveCount <= 4, "T050 RED: refill exceeded the shared pair capacity.");
        worker.ReleaseAll();
    }

    public static IEnumerable<object[]> DeterministicReleaseSchedules() =>
    [
        [new[] { 1, 2, 3, 4, 5, 6 }],
        [new[] { 2, 1, 4, 3, 6, 5 }],
        [new[] { 1, 2, 4, 3, 5, 6 }],
    ];

    [Theory]
    [MemberData(nameof(DeterministicReleaseSchedules))]
    public async Task CompletionOrder_DoesNotChangeDeterministicForwardJobIdentity(
        int[] releaseSchedule)
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 2 });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(start.StatusCode == HttpStatusCode.Created, $"T050 Start expected Created: {startBody}");
        Assert.True(
            await worker.WaitForAtLeastAsync(2, TimeSpan.FromSeconds(3)),
            "T050 RED: capacity did not fill before the release schedule began.");

        int expectedStarted = 2;
        foreach (int ordinal in releaseSchedule)
        {
            worker.ReleaseOrdinal(ordinal);
            expectedStarted++;
            Assert.True(
                await worker.WaitForAtLeastAsync(expectedStarted, TimeSpan.FromSeconds(3)),
                $"T050 RED: release of ordinal {ordinal} did not wake the next deterministic job.");
        }

        int[] observed = worker.StartedOrdinals.Take(expectedStarted).ToArray();
        Assert.Equal(Enumerable.Range(1, expectedStarted), observed);
        worker.ReleaseAll();
    }

    [Fact]
    public async Task TerminalWorkerResults_StopLaunchAndDrainLocallyBeforeFreshDurableExhaustion()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 2 });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(start.StatusCode == HttpStatusCode.Created, $"T050 Start expected Created: {startBody}");
        Pass1ExecutionStartResponse started = (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
        Assert.True(
            await worker.WaitForAtLeastAsync(2, TimeSpan.FromSeconds(3)),
            "T050 RED: terminal-drain scenario never filled its initial capacity.");

        worker.EnableAutoRelease();
        worker.ReleaseAll();
        Assert.True(
            await worker.WaitForAtLeastAsync(fixture.Package.SelectedCount, TimeSpan.FromSeconds(8)),
            $"T050 RED: terminal results did not drain all prepared jobs; launched {worker.StartedCount}.");
        await Task.Delay(250);

        Pass1ExecutionStatusResponse terminal = await ReadStatusAsync(client, started.Pass1ExecutionId);
        Assert.Equal(fixture.Package.SelectedCount, terminal.DispatchedCount);
        Assert.Equal(fixture.Package.SelectedCount, worker.StartedCount);
        Assert.Equal("ReadyForResolution", terminal.Phase);
        worker.ReleaseAll();
    }

    [Fact]
    public async Task FullyDrainedPair_UsesReconcileAsTheSoleNormalTerminalTransaction_AndLeavesNoReconcilingState()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        ReconciliationProbe reconciliation = host.Services.GetRequiredService<ReconciliationProbe>();
        worker.EnableAutoRelease();
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 2 });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(start.StatusCode == HttpStatusCode.Created, $"T065 Start expected Created: {startBody}");
        Pass1ExecutionStartResponse started =
            (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
        Assert.True(
            await worker.WaitForAtLeastAsync(fixture.Package.SelectedCount, TimeSpan.FromSeconds(8)),
            $"T065 RED: fully drained pair launched {worker.StartedCount} of {fixture.Package.SelectedCount} jobs.");
        worker.ReleaseAll();

        Pass1ExecutionStatusResponse terminal = await WaitForDrainedStatusAsync(
            client,
            started.Pass1ExecutionId,
            fixture.Package.SelectedCount,
            TimeSpan.FromSeconds(8));

        Assert.Equal(fixture.Package.SelectedCount, terminal.DispatchedCount);
        Assert.Equal(0, terminal.PendingCount);
        Assert.Equal(0, terminal.RunningCount);
        Assert.Equal(0, terminal.RetryScheduledCount);
        Assert.Equal(1, reconciliation.ReconciliationCalls);
        Assert.Equal("ReadyForResolution", terminal.Phase);
        Assert.Equal(
            "ReadyForResolution",
            await fixture.Context.Pass1PairExecutions
                .Where(value => value.Id == started.Pass1ExecutionId)
                .Select(value => value.Phase.ToString())
                .SingleAsync());
        Assert.Equal(
            "Resolving",
            await fixture.Context.TwoRepositoryScanCoordinations
                .Select(value => value.Phase.ToString())
                .SingleAsync());
        Assert.Equal(
            1,
            await fixture.Context.Pass1Outcomes.CountAsync(value =>
                value.PairExecutionId == started.Pass1ExecutionId));
        worker.ReleaseAll();
    }

    [Fact]
    public async Task ReconciliationConflict_ReloadsAndRetriesOnlyTheSameStillEligibleTerminalPair()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        ReconciliationProbe reconciliation = host.Services.GetRequiredService<ReconciliationProbe>();
        reconciliation.ConflictOnce = true;
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 2 });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(start.StatusCode == HttpStatusCode.Created, $"T050 Start expected Created: {startBody}");
        Assert.True(await worker.WaitForAtLeastAsync(2, TimeSpan.FromSeconds(3)), "T050 RED: initial capacity did not fill.");
        worker.EnableAutoRelease();
        worker.ReleaseAll();
        Assert.True(
            await worker.WaitForAtLeastAsync(fixture.Package.SelectedCount, TimeSpan.FromSeconds(8)),
            "T050 RED: terminal jobs did not drain before reconciliation.");
        await Task.Delay(500);

        Assert.Equal(
            2,
            reconciliation.ReconciliationCalls);
        worker.ReleaseAll();
    }

    [Theory]
    [InlineData(PreparedPass1RawWorkerOutcome.ProviderUnavailable)]
    [InlineData(PreparedPass1RawWorkerOutcome.Rejected)]
    public async Task Reconciliation_DoesNotRetryAfterProviderPauseOrIntegrityFailure(
        PreparedPass1RawWorkerOutcome workerOutcome)
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        ReconciliationProbe reconciliation = host.Services.GetRequiredService<ReconciliationProbe>();
        worker.SetOutcome(workerOutcome);
        reconciliation.CloseReconciliation = true;
        worker.EnableAutoRelease();
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 2 });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(start.StatusCode == HttpStatusCode.Created, $"T050 Start expected Created: {startBody}");
        Pass1ExecutionStartResponse started = (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
        Assert.True(await worker.WaitForAtLeastAsync(2, TimeSpan.FromSeconds(3)), "T050 RED: failure scenario did not dispatch.");
        worker.ReleaseAll();
        await Task.Delay(500);

        Pass1ExecutionStatusResponse status = await ReadStatusAsync(client, started.Pass1ExecutionId);
        Assert.Equal(0, reconciliation.ConflictInjections);
        int startedCount = worker.StartedCount;
        await Task.Delay(500);
        Assert.Equal(startedCount, worker.StartedCount);
        Assert.NotEqual("Reconciling", status.Phase);
    }

    public static IEnumerable<object[]> ReconciliationDriftKinds() =>
    [
        ["authorization"],
        ["boundary"],
    ];

    [Theory]
    [MemberData(nameof(ReconciliationDriftKinds))]
    public async Task Reconciliation_DoesNotRetryAfterAuthorizationOrBoundaryDrift(
        string driftKind)
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        ReconciliationProbe reconciliation = host.Services.GetRequiredService<ReconciliationProbe>();
        reconciliation.HoldReconciliation();
        worker.EnableAutoRelease();
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 2 });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(start.StatusCode == HttpStatusCode.Created, $"T050 Start expected Created: {startBody}");
        Assert.True(await worker.WaitForAtLeastAsync(fixture.Package.SelectedCount, TimeSpan.FromSeconds(8)), "T050 RED: jobs did not drain.");
        await reconciliation.WaitForReconciliationAsync(TimeSpan.FromSeconds(3));

        if (driftKind == "authorization")
        {
            await fixture.Context.SystemOwnerAssignments.ExecuteDeleteAsync();
        }
        else
        {
            await fixture.Context.PreparedScanManifests
                .Where(value => value.Id == manifests[0])
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    value => value.PairSelectionFingerprint,
                    new string('f', 64)));
        }

        fixture.Context.ChangeTracker.Clear();
        reconciliation.ReleaseReconciliation();
        await Task.Delay(500);
        Assert.Equal(1, reconciliation.ReconciliationCalls);
        worker.ReleaseAll();
    }

    [Fact]
    public async Task Drain_ReconcilesOnlyAfterFreshTerminalExhaustion_AndRetriesOneExactConflict()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        ReconciliationProbe reconciliation = host.Services.GetRequiredService<ReconciliationProbe>();
        reconciliation.ConflictOnce = true;
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests.OrderBy(value => value.RepositoryId).Select(value => value.Id).ToArrayAsync();
        using HttpResponseMessage start = await client.PostAsJsonAsync("/api/pass1-executions", new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 2 });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(start.StatusCode == HttpStatusCode.Created, $"T050 Start expected Created, got {start.StatusCode}: {startBody}");
        Pass1ExecutionStartResponse started = (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
        Assert.True(await worker.WaitForAtLeastAsync(2, TimeSpan.FromSeconds(3)), $"T050 RED: expected two in-flight Workers before drain, observed {worker.StartedCount}.");
        Pass1ExecutionStatusResponse status = (await (await client.GetAsync($"/api/pass1-executions/{started.Pass1ExecutionId:D}")).Content.ReadFromJsonAsync<Pass1ExecutionStatusResponse>())!;
        Assert.Equal(2, status.RunningCount);
        worker.EnableAutoRelease();
        worker.ReleaseAll();
        Assert.True(
            await worker.WaitForAtLeastAsync(fixture.Package.SelectedCount, TimeSpan.FromSeconds(8)),
            $"T050 RED: fresh terminal drain launched {worker.StartedCount} of {fixture.Package.SelectedCount} jobs.");

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        Pass1ExecutionStatusResponse drained;
        do
        {
            drained = await ReadStatusAsync(client, started.Pass1ExecutionId);
            if (drained.DispatchedCount == fixture.Package.SelectedCount &&
                drained.PendingCount == 0 &&
                drained.RunningCount == 0 &&
                drained.RetryScheduledCount == 0)
            {
                break;
            }

            await Task.Delay(100);
        }
        while (DateTimeOffset.UtcNow < deadline);

        Assert.Equal(fixture.Package.SelectedCount, drained.DispatchedCount);
        Assert.Equal(0, drained.PendingCount);
        Assert.Equal(0, drained.RunningCount);
        Assert.Equal(0, drained.RetryScheduledCount);
        Assert.Equal(fixture.Package.SelectedCount, worker.StartedCount);
        Assert.Equal(2, reconciliation.ReconciliationCalls);
        worker.ReleaseAll();
    }

    [Fact]
    public async Task ProviderUnavailableClaim_IsRelinquishedWithoutRetryBudget_AndEarlyRecoveryResumesDispatch()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        ManualTimeProvider time = new(PreparedScanTestHost.FixedUtcNow);
        using WebApplicationFactory<Program> host = CreateConfiguredHost(
            fixture.ConnectionString,
            fixture.Package,
            time,
            new AlwaysHealthyProviderProbe());
        await ConfigureWorkerProviderAsync(host, fixture.SystemId);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        worker.UseProviderUnavailableReasonCode = true;
        worker.SetOutcome(PreparedPass1RawWorkerOutcome.ProviderUnavailable);
        worker.EnableAutoRelease();
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 1 });
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        Pass1ExecutionStartResponse started =
            (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
        await WaitForPhaseAsync(client, started.Pass1ExecutionId, "ProbingProvider", TimeSpan.FromSeconds(3));

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            ScanJobExecution[] jobs = await context.ScanJobExecutions
                .AsNoTracking()
                .ToArrayAsync();
            Assert.Single(jobs, value => value.AttemptCount == 1);
            Assert.All(jobs, job => Assert.True(job.AttemptCount is 0 or 1));
            Assert.Equal(0, jobs.Count(value => value.State == ScanJobExecutionState.Running));
            Assert.Equal(0, jobs.Count(value => value.State == ScanJobExecutionState.RetryScheduled));
            Assert.All(jobs, job =>
            {
                Assert.Null(job.FailureCategory);
                Assert.Null(job.FailureCode);
            });

            Pass1PairExecution ticket = await context.Pass1PairExecutions
                .AsNoTracking()
                .SingleAsync(value => value.Id == started.Pass1ExecutionId);
            Assert.NotNull(ticket.NextProbeAt);
            time.Advance(ticket.NextProbeAt!.Value - time.GetUtcNow());
        }

        worker.SetOutcome(PreparedPass1RawWorkerOutcome.Completed);
        await Task.Delay(100);
        PreparedPass1ExecutionActivator activator =
            host.Services.GetRequiredService<PreparedPass1ExecutionActivator>();
        activator.Signal(started.Pass1ExecutionId, fixture.Authorization, CreateHandoff(fixture.Package));
        bool resumed = await worker.WaitForAtLeastAsync(2, TimeSpan.FromSeconds(3));
        Pass1ExecutionStatusResponse recoveryStatus = await ReadStatusAsync(
            client,
            started.Pass1ExecutionId);
        ReconciliationProbe recoveryProbe = host.Services.GetRequiredService<ReconciliationProbe>();
        Assert.True(
            resumed,
            $"T067 RED: early provider recovery did not resume dispatch; started={worker.StartedCount}, " +
            $"phase={recoveryStatus.Phase}, pending={recoveryStatus.PendingCount}, " +
            $"running={recoveryStatus.RunningCount}, retry={recoveryStatus.RetryScheduledCount}, " +
            $"claimCalls={recoveryProbe.ClaimCalls}.");
        worker.ReleaseAll();
    }

    [Fact]
    public async Task ExhaustedWithNonLocalActiveLease_DoesNotReconcileOrBusyLoopBeforeActivation()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        ManualTimeProvider time = new(PreparedScanTestHost.FixedUtcNow);
        using WebApplicationFactory<Program> host = CreateConfiguredHost(
            fixture.ConnectionString,
            fixture.Package,
            time);
        Guid pairExecutionId;
        PreparedPass1ClaimResult remoteClaim;
        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IPreparedPass1Store store = scope.ServiceProvider.GetRequiredService<IPreparedPass1Store>();
            PreparedPass1StartResult start = await store.StartAsync(
                fixture.Authorization,
                CreateHandoff(fixture.Package),
                concurrencyLimit: 2);
            Assert.Equal(PreparedPass1StartOutcome.Created, start.Outcome);
            pairExecutionId = start.PairExecutionId!.Value;
            remoteClaim = await store.ClaimNextAsync(
                pairExecutionId,
                time.GetUtcNow(),
                TimeSpan.FromMinutes(30));
            Assert.Equal(PreparedPass1ClaimOutcome.Claimed, remoteClaim.Outcome);

            VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            ScanJobExecution[] pending = await context.ScanJobExecutions
                .Where(value => value.Id != remoteClaim.JobId)
                .ToArrayAsync();
            foreach (ScanJobExecution job in pending)
            {
                job.Cancel(job.StateVersion, null, "t067-remote-terminal", time.GetUtcNow());
            }

            await context.SaveChangesAsync();
        }

        ReconciliationProbe reconciliation = host.Services.GetRequiredService<ReconciliationProbe>();
        reconciliation.ResetClaimCalls();
        host.Services.GetRequiredService<PreparedPass1ExecutionActivator>()
            .Signal(pairExecutionId, fixture.Authorization, CreateHandoff(fixture.Package));
        await Task.Delay(750);

        Assert.Equal(0, reconciliation.ReconciliationCalls);
        Assert.InRange(reconciliation.ClaimCalls, 1, 2);
        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            Assert.Equal(
                Pass1PairExecutionPhase.Running,
                await context.Pass1PairExecutions
                    .Where(value => value.Id == pairExecutionId)
                    .Select(value => value.Phase)
                    .SingleAsync());
            Assert.Equal(
                ScanJobExecutionState.Running,
                await context.ScanJobExecutions
                    .Where(value => value.Id == remoteClaim.JobId)
                    .Select(value => value.State)
                    .SingleAsync());

            ScanJobExecution active = await context.ScanJobExecutions
                .SingleAsync(value => value.Id == remoteClaim.JobId);
            active.Cancel(active.StateVersion, active.LeaseToken, "t067-remote-completed", time.GetUtcNow());
            await context.SaveChangesAsync();
        }
        host.Services.GetRequiredService<PreparedPass1ExecutionActivator>()
            .Signal(pairExecutionId, fixture.Authorization, CreateHandoff(fixture.Package));
        bool reconciled = await WaitForReconciliationCallsAsync(
            reconciliation,
            1,
            TimeSpan.FromSeconds(3));
        Pass1PairExecutionPhase phaseAfterActivation;
        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            phaseAfterActivation = await scope.ServiceProvider.GetRequiredService<VulgataDbContext>()
                .Pass1PairExecutions
                .Where(value => value.Id == pairExecutionId)
                .Select(value => value.Phase)
                .SingleAsync();
        }
        Assert.True(
            reconciled,
            $"T067 RED: activation after the nonlocal lease completed did not resume reconciliation; " +
            $"phase={phaseAfterActivation}, claimCalls={reconciliation.ClaimCalls}.");
    }

    [Fact]
    public async Task Shutdown_WithCancellationResistantWorker_RetainsRevisionBindingsUntilLocalDrain()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreatePreparedPairAsync();
        using WebApplicationFactory<Program> host = CreateConfiguredHost(fixture.ConnectionString, fixture.Package);
        ControllableWorker worker = host.Services.GetRequiredService<ControllableWorker>();
        worker.CancellationResistant = true;
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);
        Guid[] manifests = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();
        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 1 });
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        Pass1ExecutionStartResponse started =
            (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
        Assert.True(
            await worker.WaitForAtLeastAsync(1, TimeSpan.FromSeconds(3)),
            "T067 RED: cancellation-resistant Worker was not launched.");

        IPreparedPass1RevisionBindingRegistry bindings =
            host.Services.GetRequiredService<IPreparedPass1RevisionBindingRegistry>();
        Guid repositoryId = fixture.Package.Repositories
            .OrderBy(value => value.RepositoryId)
            .First()
            .RepositoryId;
        Assert.NotNull(bindings.ResolveBinding(started.Pass1ExecutionId, repositoryId));

        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        await Task.Delay(750);
        Assert.NotNull(
            bindings.ResolveBinding(started.Pass1ExecutionId, repositoryId));
        worker.ReleaseAll();
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline &&
               bindings.ResolveBinding(started.Pass1ExecutionId, repositoryId) is not null)
        {
            await Task.Delay(50);
        }

        Assert.Null(bindings.ResolveBinding(started.Pass1ExecutionId, repositoryId));
    }

    private static async Task<Pass1ExecutionStatusResponse> WaitForPhaseAsync(
        HttpClient client,
        Guid executionId,
        string phase,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        Pass1ExecutionStatusResponse status = await ReadStatusAsync(client, executionId);
        while (DateTimeOffset.UtcNow < deadline && status.Phase != phase)
        {
            await Task.Delay(50);
            status = await ReadStatusAsync(client, executionId);
        }

        Assert.Equal(phase, status.Phase);
        return status;
    }

    private static async Task<bool> WaitForReconciliationCallsAsync(
        ReconciliationProbe reconciliation,
        int expected,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline && reconciliation.ReconciliationCalls < expected)
        {
            await Task.Delay(50);
        }

        return reconciliation.ReconciliationCalls >= expected;
    }

    private async Task<PreparedScanPostgreSqlFixture> CreatePreparedPairAsync()
    {
        string administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN")
            ?? throw new InvalidOperationException(
                "T050 requires VULGATA_TEST_POSTGRES_ADMIN for an isolated PostgreSQL 17 database.");
        PreparedScanPostgreSqlFixture fixture =
            await PreparedScanPostgreSqlFixture.CreateAsync(administrator);
        PreparedScanCommitResult committed = await PreparedScanPostgreSqlFixture
            .CreateStore(fixture.Context)
            .CommitPairAsync(fixture.Authorization, fixture.Package, 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, committed.Outcome);
        fixture.Context.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(
            fixture.SystemId,
            fixture.Authorization.UserId.ToString("D"),
            PreparedScanTestHost.FixedUtcNow));

        foreach (ScanRun run in await fixture.Context.ScanRuns.ToArrayAsync())
        {
            run.SetCandidateCount(0);
            run.Complete(0, 0, PreparedScanTestHost.FixedUtcNow);
            run.RecordApprovedPreScanOutcome("预扫描已批准——继续进入顺序演示运行器");
        }
        await fixture.Context.SaveChangesAsync();

        const string schema = "code-logic/file-v2";
        const string runtime = "{\"runtime\":\"story-12-3-test\"}";
        string runtimeHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(runtime)));
        await fixture.Context.ScanExecutions.ExecuteUpdateAsync(setters => setters
            .SetProperty(execution => execution.PreScanResultId, Guid.Empty)
            .SetProperty(execution => execution.Phase, ScanExecutionPhase.Prepared)
            .SetProperty(execution => execution.PromptIdentity, "story-12-3-test-prompt")
            .SetProperty(execution => execution.DocumentSchemaIdentity, schema)
            .SetProperty(execution => execution.AnalysisRuntimeIdentityJson, runtime)
            .SetProperty(execution => execution.AnalysisRuntimeIdentityHash, runtimeHash)
            .SetProperty(execution => execution.UpdatedAt, PreparedScanTestHost.FixedUtcNow));
        await fixture.Context.PreparedScanManifests.ExecuteUpdateAsync(setters => setters
            .SetProperty(manifest => manifest.RuntimeIdentityHash, runtimeHash)
            .SetProperty(manifest => manifest.CodeGraphRuntimeIdentityHash, runtimeHash)
            .SetProperty(manifest => manifest.JavaReadinessRuntimeIdentityHash, runtimeHash));
        fixture.Context.ChangeTracker.Clear();
        return fixture;
    }

    private static async Task ConfigureWorkerProviderAsync(
        WebApplicationFactory<Program> host,
        Guid systemId)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        ISecretEncryptionService encryption =
            scope.ServiceProvider.GetRequiredService<ISecretEncryptionService>();
        string encryptedApiKey = await encryption.EncryptAsync(
            SecretPurposes.LlmApiKey,
            "t067-provider-health-key");
        LlmProvider provider = new(
            "t067-provider-health",
            "http://127.0.0.1:1",
            encryptedApiKey,
            ApiType.ChatCompletions,
            AgentType.Worker,
            PreparedScanTestHost.FixedUtcNow);
        context.LlmProviders.Add(provider);
        SystemLlmProviderOverride? existing = await context.SystemLlmProviderOverrides
            .SingleOrDefaultAsync(value => value.SystemId == systemId && value.AgentType == AgentType.Worker);
        if (existing is null)
        {
            context.SystemLlmProviderOverrides.Add(SystemLlmProviderOverride.Create(
                systemId,
                provider.Id,
                AgentType.Worker,
                PreparedScanTestHost.FixedUtcNow));
        }
        else
        {
            existing.Update(provider.Id, AgentType.Worker, PreparedScanTestHost.FixedUtcNow);
        }

        await context.SaveChangesAsync();
    }

    private static WebApplicationFactory<Program> CreateConfiguredHost(
        string connectionString,
        PreparedScanPairPackage package,
        TimeProvider? timeProvider = null,
        IPreparedPass1ProviderHealthProbe? providerHealthProbe = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            string keyRingPath = Path.Combine(
                Path.GetTempPath(),
                "vulgata-tests",
                "t050-data-protection",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(keyRingPath);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["DataProtection:KeyRingPath"] = keyRingPath,
                    ["DataProtection:ApplicationName"] = "Vulgata.Tests.T050." + Guid.NewGuid().ToString("N"),
                }));
            builder.UseSetting("Logging:EventLog:LogLevel:Default", "None");
            builder.UseSetting("Logging:LogLevel:Default", "None");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<VulgataDbContext>>();
                services.RemoveAll<IDbContextFactory<VulgataDbContext>>();
                services.RemoveAll<IConfigureOptions<DbContextOptions<ApplicationDbContext>>>();
                services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<VulgataDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                        npgsql.MigrationsHistoryTable("__IdentityMigrationsHistory", "identity");
                    }));
                services.AddDbContext<VulgataDbContext>(options => options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    }),
                    contextLifetime: ServiceLifetime.Scoped,
                    optionsLifetime: ServiceLifetime.Singleton);
                services.AddDbContextFactory<VulgataDbContext>(options => options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    }));
                services.RemoveAll<IPreparedPass1JobExecutor>();
                services.RemoveAll<IPreparedPass1ClaimedPacketSource>();
                services.RemoveAll<IPreparedScanHandoffSource>();
                services.RemoveAll<IPreparedPass1ProviderAvailabilityProbe>();
                services.RemoveAll<IPreparedPass1Store>();
                services.RemoveAll<IPreparedRevisionAcquirer>();
                services.RemoveAll<IPreparedRevisionLeaseResolver>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider ?? TimeProvider.System);
                services.AddSingleton<IPreparedScanHandoffSource>(
                    new LegacyPreparedScanHandoffSource(package));
                services.AddSingleton<SyntheticPreparedRevisionRuntime>();
                services.AddSingleton<IPreparedRevisionAcquirer>(provider =>
                    provider.GetRequiredService<SyntheticPreparedRevisionRuntime>());
                services.AddSingleton<IPreparedRevisionLeaseResolver>(provider =>
                    provider.GetRequiredService<SyntheticPreparedRevisionRuntime>());
                services.AddSingleton<IPreparedPass1ProviderAvailabilityProbe,
                    AlwaysAvailableProviderProbe>();
                if (providerHealthProbe is not null)
                {
                    services.RemoveAll<IPreparedPass1ProviderHealthProbe>();
                    services.AddSingleton<IPreparedPass1ProviderHealthProbe>(providerHealthProbe);
                }
                services.AddSingleton<ReconciliationProbe>();
                services.AddScoped<IPreparedPass1Store>(provider =>
                    new RecordingPass1Store(
                        provider.GetRequiredService<PreparedPass1Store>(),
                        provider.GetRequiredService<ReconciliationProbe>()));
                services.AddSingleton<ControllableWorker>();
                services.AddSingleton<IPreparedPass1JobExecutor>(provider =>
                    provider.GetRequiredService<ControllableWorker>());
                services.AddSingleton<IPreparedPass1ClaimedPacketSource>(provider =>
                    new TestClaimedPacketSource(
                        provider.GetRequiredService<IDbContextFactory<VulgataDbContext>>(),
                        package));
                services.RemoveAll<IPreparedPass1WorkerDispatcher>();
                services.AddSingleton<IPreparedPass1WorkerDispatcher>(provider =>
                    new ControllableDispatcher(provider.GetRequiredService<ControllableWorker>()));
                services.RemoveAll<IPass1CodeLogicDocumentCommitter>();
                services.AddScoped<IPass1CodeLogicDocumentCommitter, TestDocumentCommitter>();
                services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                    .SetApplicationName("Vulgata.Tests.T050." + Guid.NewGuid().ToString("N"));
            });
        });

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactory<Program> host,
        PreparedScanPostgreSqlFixture fixture)
    {
        string email = $"pass1-capacity-{Guid.NewGuid():N}@example.com";
        const string password = "Valid1!Pass";
        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = new() { UserName = email, Email = email, EmailConfirmed = true };
            IdentityResult result = await userManager.CreateAsync(user, password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
            await userManager.AddToRoleAsync(user, RoleNames.SystemOwner);
            VulgataDbContext domain = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            domain.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(
                fixture.SystemId, user.Id, PreparedScanTestHost.FixedUtcNow));
            await domain.SaveChangesAsync();
        }

        HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        using HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
        string html = await loginPage.Content.ReadAsStringAsync();
        Match form = Regex.Match(html, "<form[^>]*>(?<content>.*?)</form>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, $"T050 authenticated WAF login unavailable (status={loginPage.StatusCode}).");
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(form.Groups["content"].Value, "<input[^>]*>", RegexOptions.IgnoreCase))
        {
            Match name = Regex.Match(input.Value, "name=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
            if (name.Success)
            {
                Match value = Regex.Match(input.Value, "value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase);
                fields[name.Groups["value"].Value] = value.Success ? WebUtility.HtmlDecode(value.Groups["value"].Value) : string.Empty;
            }
        }
        fields["Input.Email"] = email;
        fields["Input.Password"] = password;
        fields["Input.RememberMe"] = "false";
        using FormUrlEncodedContent login = new(fields);
        using HttpResponseMessage response = await client.PostAsync("/Account/Login", login);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<Pass1ExecutionStatusResponse> ReadStatusAsync(
        HttpClient client,
        Guid executionId)
    {
        using HttpResponseMessage response = await client.GetAsync(
            $"/api/pass1-executions/{executionId:D}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Pass1ExecutionStatusResponse>())!;
    }

    private static async Task<Pass1ExecutionStatusResponse> WaitForDrainedStatusAsync(
        HttpClient client,
        Guid executionId,
        int expectedDispatchedCount,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        Pass1ExecutionStatusResponse status = await ReadStatusAsync(client, executionId);
        while (DateTimeOffset.UtcNow < deadline &&
               (status.Phase == "Running" ||
                status.DispatchedCount != expectedDispatchedCount ||
                status.PendingCount != 0 ||
                status.RunningCount != 0 ||
                status.RetryScheduledCount != 0))
        {
            await Task.Delay(100);
            status = await ReadStatusAsync(client, executionId);
        }

        return status;
    }

    private sealed class LegacyPreparedScanHandoffSource(PreparedScanPairPackage package)
        : IPreparedScanHandoffSource
    {
        private readonly PreparedScanHandoff _handoff = CreateHandoff(package);

        public Task<PreparedScanHandoff?> GetAsync(
            PreparedScanAuthorizationScope authorizationScope,
            Guid coordinationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PreparedScanHandoff?>(
                coordinationId == _handoff.CoordinationId ? _handoff : null);
    }

    private sealed class SyntheticPreparedRevisionRuntime :
        IPreparedRevisionAcquirer,
        IPreparedRevisionLeaseResolver
    {
        private readonly ConcurrentDictionary<string, ICommittedRepositorySnapshotLease> _leases =
            new(StringComparer.Ordinal);

        public Task<PreparedRevisionAcquisitionResult> AcquireAsync(
            PreparedRevisionRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string identity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join(
                    '|',
                    request.RepositoryId.ToString("D"),
                    request.ScanRunId.ToString("D"),
                    request.RepositoryOrigin,
                    request.RequestedRef,
                    request.ProfiledRevision))));
            string containedRootToken = "synthetic-root-" + identity;
            _leases[containedRootToken] = new SyntheticSnapshotLease(
                request.RepositoryId,
                request.ScanRunId,
                request.ProfiledRevision,
                Path.GetFullPath(AppContext.BaseDirectory));

            return Task.FromResult(new PreparedRevisionAcquisitionResult(
                new PreparedRevisionSnapshot(
                    request.RepositoryId,
                    request.ScanRunId,
                    request.RequestedRef,
                    request.ProfiledRevision,
                    identity,
                    "synthetic-binding-" + identity,
                    containedRootToken),
                null));
        }

        public bool TryResolveLease(
            string bindingToken,
            out ICommittedRepositorySnapshotLease? lease)
        {
            if (!string.IsNullOrWhiteSpace(bindingToken) &&
                _leases.TryGetValue(bindingToken, out ICommittedRepositorySnapshotLease? resolved))
            {
                lease = resolved;
                return true;
            }

            lease = null;
            return false;
        }

        private sealed class SyntheticSnapshotLease(
            Guid repositoryId,
            Guid scanRunId,
            string profiledRevision,
            string rootPath) : ICommittedRepositorySnapshotLease
        {
            public Guid RepositoryId => repositoryId;
            public Guid ScanRunId => scanRunId;
            public string ProfiledRevision => profiledRevision;
            public string RootPath => rootPath;

            public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class AlwaysAvailableProviderProbe : IPreparedPass1ProviderAvailabilityProbe
    {
        public Task<bool> IsAvailableAsync(
            Guid pairExecutionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class AlwaysHealthyProviderProbe : IPreparedPass1ProviderHealthProbe
    {
        public Task<PreparedPass1ProviderHealthResult> ProbeAsync(
            Uri endpoint,
            string model,
            string apiKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreparedPass1ProviderHealthResult(
                PreparedPass1ProviderHealthOutcome.Available,
                null));
    }

    private sealed class ReconciliationProbe
    {
        private int _reconciliationCalls;
        private int _claimCalls;
        private int _conflictOnce;
        private int _conflictInjections;
        private int _holdReconciliation;
        private readonly TaskCompletionSource _reconciliationEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseReconciliation =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ReconciliationCalls => Volatile.Read(ref _reconciliationCalls);
        public int ClaimCalls => Volatile.Read(ref _claimCalls);
        public int ConflictInjections => Volatile.Read(ref _conflictInjections);

        public bool ConflictOnce
        {
            get => Volatile.Read(ref _conflictOnce) == 1;
            set => Volatile.Write(ref _conflictOnce, value ? 1 : 0);
        }

        public bool CloseReconciliation { get; set; }

        public void RecordReconciliationCall() => Interlocked.Increment(ref _reconciliationCalls);

        public void RecordClaimCall() => Interlocked.Increment(ref _claimCalls);

        public void ResetClaimCalls() => Interlocked.Exchange(ref _claimCalls, 0);

        public void HoldReconciliation() => Interlocked.Exchange(ref _holdReconciliation, 1);

        public Task WaitForReconciliationAsync(TimeSpan timeout) =>
            _reconciliationEntered.Task.WaitAsync(timeout);

        public void ReleaseReconciliation() => _releaseReconciliation.TrySetResult();

        public async Task WaitIfHeldAsync()
        {
            if (Volatile.Read(ref _holdReconciliation) == 1)
            {
                _reconciliationEntered.TrySetResult();
                await _releaseReconciliation.Task;
            }
        }

        public bool TryInjectConflict()
        {
            Interlocked.Increment(ref _reconciliationCalls);
            bool injected = Interlocked.Exchange(ref _conflictOnce, 0) == 1;
            if (injected)
            {
                Interlocked.Increment(ref _conflictInjections);
            }

            return injected;
        }
    }

    private sealed class RecordingPass1Store(
        IPreparedPass1Store inner,
        ReconciliationProbe probe) : IPreparedPass1Store
    {
        public Task<PreparedPass1StartResult> StartAsync(
            PreparedScanAuthorizationScope authorizationScope,
            PreparedScanHandoff handoff,
            int concurrencyLimit = 2,
            CancellationToken cancellationToken = default) =>
            inner.StartAsync(authorizationScope, handoff, concurrencyLimit, cancellationToken);

        public Task<PreparedPass1ClaimResult> ClaimNextAsync(
            Guid pairExecutionId,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            probe.RecordClaimCall();
            return inner.ClaimNextAsync(pairExecutionId, now, leaseDuration, cancellationToken);
        }

        public Task<PreparedPass1ConcurrencyUpdateResult> UpdateConcurrencyAsync(
            PreparedScanAuthorizationScope authorizationScope,
            Guid pairExecutionId,
            int requestedLimit,
            long expectedStateVersion,
            string principalId,
            DateTimeOffset changedAt,
            CancellationToken cancellationToken = default) =>
            inner.UpdateConcurrencyAsync(
                authorizationScope,
                pairExecutionId,
                requestedLimit,
                expectedStateVersion,
                principalId,
                changedAt,
                cancellationToken);

        public Task<PreparedPass1RecoveryResult> RecoverAsync(
            Guid pairExecutionId,
            PreparedScanAuthorizationScope authorizationScope,
            bool providerAvailable,
            DateTimeOffset observedAt,
            CancellationToken cancellationToken = default) =>
            inner.RecoverAsync(
                pairExecutionId,
                authorizationScope,
                providerAvailable,
                observedAt,
                cancellationToken);

        public Task<PreparedPass1FailureResult> RecordFailureAsync(
            Guid pairExecutionId,
            Guid jobId,
            long expectedStateVersion,
            Guid expectedLeaseToken,
            ScanJobFailureCategory failureCategory,
            DateTimeOffset observedAt,
            CancellationToken cancellationToken = default) =>
            inner.RecordFailureAsync(
                pairExecutionId,
                jobId,
                expectedStateVersion,
                expectedLeaseToken,
                failureCategory,
                observedAt,
                cancellationToken);

        public Task<PreparedPass1FailureResult> FailRevisionBindingAsync(
            Guid pairExecutionId,
            PreparedScanAuthorizationScope authorizationScope,
            long expectedStateVersion,
            DateTimeOffset observedAt,
            CancellationToken cancellationToken = default) =>
            inner.FailRevisionBindingAsync(
                pairExecutionId,
                authorizationScope,
                expectedStateVersion,
                observedAt,
                cancellationToken);

        public Task<PreparedPass1ProviderCircuitResult> RecordProviderTransientAsync(
            Guid pairExecutionId,
            DateTimeOffset observedAt,
            DateTimeOffset? retryAfterAt = null,
            CancellationToken cancellationToken = default) =>
            inner.RecordProviderTransientAsync(
                pairExecutionId,
                observedAt,
                retryAfterAt,
                cancellationToken);

        public Task<PreparedPass1ProviderCircuitResult> ProbeProviderAsync(
            Guid pairExecutionId,
            long expectedStateVersion,
            int expectedProbeOrdinal,
            bool providerAvailable,
            DateTimeOffset observedAt,
            DateTimeOffset? retryAfterAt = null,
            CancellationToken cancellationToken = default) =>
            inner.ProbeProviderAsync(
                pairExecutionId,
                expectedStateVersion,
                expectedProbeOrdinal,
                providerAvailable,
                observedAt,
                retryAfterAt,
                cancellationToken);

        public Task<PreparedPass1ReconciliationResult> ReconcileAsync(
            Guid pairExecutionId,
            PreparedPass1ReconciliationRequest reconciliation,
            CancellationToken cancellationToken = default)
        {
            return ReconcileCoreAsync(pairExecutionId, reconciliation, cancellationToken);
        }

        private async Task<PreparedPass1ReconciliationResult> ReconcileCoreAsync(
            Guid pairExecutionId,
            PreparedPass1ReconciliationRequest reconciliation,
            CancellationToken cancellationToken)
        {
            await probe.WaitIfHeldAsync();
            if (probe.CloseReconciliation)
            {
                probe.RecordReconciliationCall();
                return new PreparedPass1ReconciliationResult(
                    PreparedPass1ReconciliationOutcome.Ineligible,
                    null,
                    null,
                    [],
                    [],
                    0,
                    0,
                    0,
                    false,
                    false);
            }

            if (probe.TryInjectConflict())
            {
                return new PreparedPass1ReconciliationResult(
                    PreparedPass1ReconciliationOutcome.Conflict,
                    null,
                    null,
                    [],
                    [],
                    0,
                    0,
                    0,
                    false,
                    false);
            }

            return await inner.ReconcileAsync(pairExecutionId, reconciliation, cancellationToken);
        }

        public Task<PreparedPass1CompletionResult> CompletePass1Async(
            Guid pairExecutionId,
            PreparedScanAuthorizationScope authorizationScope,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken = default) =>
            inner.CompletePass1Async(
                pairExecutionId,
                authorizationScope,
                completedAt,
                cancellationToken);
    }

    private static PreparedScanHandoff CreateHandoff(PreparedScanPairPackage package) =>
        new(
            PreScanResult.PreparedHandoffSchemaIdentity,
            package.CoordinationId,
            package.PairSelectionFingerprint,
            package.PairPackageFingerprint,
            package.Repositories
                .OrderBy(repository => repository.RepositoryId)
                .Select(repository => new PreparedScanManifestBinding(
                    repository.RepositoryId,
                    repository.ScanRunId,
                    repository.ProfiledRevision,
                    PreparedScanIdentityBuilders.CreateDeterministicIdentity(
                        "PreparedScanManifest", repository.ManifestFingerprint),
                    repository.ManifestFingerprint,
                    PreparedScanIdentityBuilders.CreateCodeGraphProof(repository.CodeGraphProof).Fingerprint,
                    PreparedScanIdentityBuilders.CreateJavaReadiness(repository.JavaReadiness).Fingerprint,
                    repository.PreparedScanContextIdentityHash))
                .ToArray(),
            package.Repositories.SelectMany(repository => repository.FileCodeUnits).ToArray(),
            package.SelectedCount);

    private static byte[] CreateCanonicalResult(PreparedPass1WorkerPacket packet)
    {
        string evidenceLocator =
            $"{packet.RepositoryRelativePath}#L{packet.LineStart}-L{packet.LineEnd}";
        return JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                schemaIdentity = "code-logic/file-v2",
                binding = new
                {
                    jobId = packet.JobId.ToString("D"),
                    scanExecutionId = packet.ScanExecutionId.ToString("D"),
                    systemId = packet.SystemId.ToString("D"),
                    repositoryId = packet.RepositoryId.ToString("D"),
                    scanRunId = packet.ScanRunId.ToString("D"),
                    profiledRevision = packet.ProfiledRevision,
                    fileCodeUnitId = packet.FileCodeUnitId.ToString("D"),
                    fileCodeUnitIdentityFingerprint = packet.FileCodeUnitIdentityFingerprint,
                    preparedScanManifestId = packet.PreparedScanManifestId.ToString("D"),
                    preparedScanManifestFingerprint = packet.PreparedScanManifestFingerprint,
                    pairPackageFingerprint = packet.PairPackageFingerprint,
                    repositoryRelativePath = packet.RepositoryRelativePath,
                    byteStart = packet.ByteStart,
                    byteEnd = packet.ByteEnd,
                    lineStart = packet.LineStart,
                    lineEnd = packet.LineEnd,
                    segmentOrdinal = packet.SegmentOrdinal,
                    segmentCount = packet.SegmentCount,
                    fileHash = packet.FileHash,
                    sourceHash = packet.SourceHash,
                    promptIdentity = packet.PromptIdentity,
                    documentSchemaIdentity = packet.DocumentSchemaIdentity,
                    analysisRuntimeIdentityHash = packet.AnalysisRuntimeIdentityHash,
                },
                sourceAnchors = new[]
                {
                    new
                    {
                        key = "a001",
                        repositoryRelativePath = packet.RepositoryRelativePath,
                        byteStart = packet.ByteStart,
                        byteEnd = packet.ByteEnd,
                        lineStart = packet.LineStart,
                        lineEnd = packet.LineEnd,
                        sourceHash = packet.SourceHash,
                        evidenceLocator,
                    },
                },
                filePurpose = new
                {
                    summary = "bounded prepared File Code Unit",
                    sourceAnchorRefs = new[] { "a001" },
                    responsibilities = new[]
                    {
                        new
                        {
                            description = "Records the bounded File Code Unit for Pass 1.",
                            sourceAnchorRefs = new[] { "a001" },
                        },
                    },
                },
                internalUnits = Array.Empty<object>(),
                executionFlow = Array.Empty<object>(),
                dataFlow = Array.Empty<object>(),
                rules = Array.Empty<object>(),
                dataAccess = Array.Empty<object>(),
                communicationEvidence = Array.Empty<object>(),
                knownReferences = Array.Empty<object>(),
                deferredObservations = Array.Empty<object>(),
                assumptions = Array.Empty<object>(),
            });
    }

    private sealed class TestDocumentCommitter(VulgataDbContext context)
        : IPass1CodeLogicDocumentCommitter
    {
        public async Task<Pass1DocumentCommitResult> CommitAsync(
            Pass1DocumentCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(cancellationToken);
            ScanJobExecution? job = await context.ScanJobExecutions
                .FromSqlInterpolated($$"""
                    SELECT * FROM vulgata."ScanJobExecutions"
                    WHERE "Id" = {{request.Packet.JobId}}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

            if (job is null ||
                job.State != ScanJobExecutionState.Running ||
                job.StateVersion != request.ExpectedJobStateVersion ||
                job.LeaseToken != request.ExpectedLeaseToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(
                    Pass1DocumentCommitStatus.Conflict,
                    null,
                    null,
                    null,
                    "stale-running-job");
            }

            DateTimeOffset committedAt = request.CommittedAt.ToUniversalTime();
            string contentJson = Encoding.UTF8.GetString(request.CanonicalCodeLogicJson.Span);
            string contentFingerprint = Convert.ToHexStringLower(
                SHA256.HashData(request.CanonicalCodeLogicJson.Span));
            string commitFingerprint = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"t050-commit:{request.Packet.JobId:D}:{contentFingerprint}")));
            LogicalDocument logicalDocument = LogicalDocument.Create(
                request.LogicalDocumentId,
                request.Packet.RepositoryId,
                DocumentType.CodeLogic,
                $"{request.Packet.RepositoryRelativePath}#segment={request.Packet.SegmentOrdinal}",
                committedAt);
            context.LogicalDocuments.Add(logicalDocument);
            await context.SaveChangesAsync(cancellationToken);

            DocumentVersion documentVersion = DocumentVersion.Create(
                request.DocumentVersionId,
                request.LogicalDocumentId,
                request.Packet.RepositoryId,
                request.Packet.SystemId,
                request.Packet.ScanRunId,
                request.Packet.ProfiledRevision,
                1,
                request.Packet.RepositoryRelativePath,
                request.Packet.RepositoryRelativePath,
                "code-logic/file-v2",
                contentJson,
                $"# {request.Packet.RepositoryRelativePath}",
                contentFingerprint,
                commitFingerprint,
                committedAt);
            context.DocumentVersions.Add(documentVersion);
            logicalDocument.SetInitialCurrentVersion(documentVersion);
            job.Complete(
                request.ExpectedJobStateVersion,
                request.ExpectedLeaseToken,
                request.DocumentVersionId,
                committedAt);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(
                Pass1DocumentCommitStatus.Committed,
                request.LogicalDocumentId,
                request.DocumentVersionId,
                commitFingerprint,
                null);
        }
    }

    private sealed class TestClaimedPacketSource(
        IDbContextFactory<VulgataDbContext> contextFactory,
        PreparedScanPairPackage package) : IPreparedPass1ClaimedPacketSource
    {
        private readonly ConcurrentDictionary<Guid, int> _nextOrdinalByPair = new();

        public async Task<PreparedPass1WorkerPacket?> HydrateAsync(
            Guid pairExecutionId,
            PreparedPass1ClaimResult claim,
            DateTimeOffset observedAt,
            CancellationToken cancellationToken = default)
        {
            await WaitForTurnAsync(pairExecutionId, claim.PairSelectionOrdinal, cancellationToken);
            await using VulgataDbContext context =
                await contextFactory.CreateDbContextAsync(cancellationToken);
            Pass1PairExecution? pair = await context.Pass1PairExecutions
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == pairExecutionId, cancellationToken);
            ScanJobExecution? job = await context.ScanJobExecutions
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == claim.JobId, cancellationToken);
            if (pair is null || job is null ||
                claim.Outcome != PreparedPass1ClaimOutcome.Claimed)
            {
                AdvanceTurn(pairExecutionId, claim.PairSelectionOrdinal);
                return null;
            }

            ScanExecution? execution = await context.ScanExecutions
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == job.ScanExecutionId, cancellationToken);
            PreparedFileCodeUnitDraft? draft = package.Repositories
                .SelectMany(value => value.FileCodeUnits)
                .SingleOrDefault(value => value.Id == job.FileCodeUnitId &&
                    value.ManifestOrdinal == claim.ManifestOrdinal &&
                    value.PairSelectionOrdinal == claim.PairSelectionOrdinal);
            PreparedScanManifestEntry? entry = await context.PreparedScanManifestEntries
                .AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.FileCodeUnitId == job.FileCodeUnitId &&
                     value.ManifestOrdinal == claim.ManifestOrdinal &&
                     value.PairSelectionOrdinal == claim.PairSelectionOrdinal &&
                     (value.PreparedScanManifestId == pair.ManifestOneId ||
                      value.PreparedScanManifestId == pair.ManifestTwoId),
                    cancellationToken);
            PreparedScanManifest? manifest = entry is null
                ? null
                : await context.PreparedScanManifests.AsNoTracking()
                    .SingleOrDefaultAsync(value => value.Id == entry.PreparedScanManifestId, cancellationToken);
            ScanContextSnapshot? snapshot = manifest is null
                ? null
                : await context.ScanContextSnapshots.AsNoTracking()
                    .SingleOrDefaultAsync(value => value.Id == manifest.ContextSnapshotId, cancellationToken);
            if (execution is null || draft is null || entry is null || manifest is null || snapshot is null)
            {
                AdvanceTurn(pairExecutionId, claim.PairSelectionOrdinal);
                return null;
            }

            int byteCount = checked((int)(draft.ByteEndExclusive - draft.ByteStart));
            byte[] source = CreateSource(claim.JobId, byteCount);
            PreparedPass1ContextSnapshotProjection snapshotProjection = new(
                snapshot.Id,
                snapshot.RepositoryId,
                snapshot.ScanRunId,
                snapshot.ProfiledRevision,
                snapshot.PreparedScanContextIdentityHash!,
                snapshot.ContentHash,
                snapshot.GlobalContext,
                snapshot.SystemContext,
                snapshot.RepositoryContext);
            PreparedPass1WorkerPacket packet = new(
                pair.Id,
                claim.JobId,
                job.ScanExecutionId,
                execution.SystemId,
                job.RepositoryId,
                job.ScanRunId,
                execution.ProfiledRevision,
                job.FileCodeUnitId,
                draft.IdentityFingerprint,
                manifest.Id,
                manifest.Fingerprint,
                pair.PairPackageFingerprint,
                draft.RelativePath,
                draft.ByteStart,
                draft.ByteEndExclusive,
                draft.LineStart,
                draft.LineEnd,
                draft.SegmentOrdinal,
                draft.SegmentCount,
                draft.FileHash,
                Convert.ToHexStringLower(SHA256.HashData(source)),
                execution.PromptIdentity!,
                execution.DocumentSchemaIdentity!,
                execution.AnalysisRuntimeIdentityHash!,
                claim.ManifestOrdinal,
                claim.PairSelectionOrdinal,
                source,
                snapshotProjection,
                [])
            {
                RemainingNavigationBudget = 8,
            };
            AdvanceTurn(pairExecutionId, claim.PairSelectionOrdinal);
            return packet;
        }

        private async Task WaitForTurnAsync(
            Guid pairExecutionId,
            int pairSelectionOrdinal,
            CancellationToken cancellationToken)
        {
            while (_nextOrdinalByPair.GetOrAdd(pairExecutionId, 1) < pairSelectionOrdinal)
            {
                await Task.Delay(5, cancellationToken);
            }
        }

        private void AdvanceTurn(Guid pairExecutionId, int pairSelectionOrdinal) =>
            _nextOrdinalByPair.AddOrUpdate(
                pairExecutionId,
                pairSelectionOrdinal + 1,
                (_, current) => Math.Max(current, pairSelectionOrdinal + 1));

        private static byte[] CreateSource(Guid jobId, int byteCount)
        {
            byte[] seed = Encoding.UTF8.GetBytes($"// t050 source {jobId:D}\n");
            byte[] source = new byte[byteCount];
            for (int index = 0; index < source.Length; index++)
            {
                source[index] = seed[index % seed.Length];
            }

            return source;
        }
    }

    private sealed class ControllableDispatcher(ControllableWorker worker)
        : IPreparedPass1WorkerDispatcher
    {
        public async Task<PreparedPass1DispatchResult> DispatchAsync(
            PreparedPass1DispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            PreparedPass1WorkerPacket[] packets = [
                .. request.PreparedFileCodeUnits
                    .OrderBy(value => value.PairSelectionOrdinal)
                    .Select(assignment => CreatePacket(request, assignment)),
            ];
            PreparedPass1JobDispatchResult[] results = new PreparedPass1JobDispatchResult[packets.Length];
            for (int index = 0; index < packets.Length; index++)
            {
                PreparedPass1WorkerPacket packet = packets[index];
                PreparedPass1RawWorkerResult raw = await worker.ExecuteAsync(
                    packet,
                    "t050-bounded-test-prompt",
                    cancellationToken);
                PreparedPass1ParsedWorkerResult? parsed = raw.Outcome == PreparedPass1RawWorkerOutcome.Completed
                    ? PreparedPass1ResultParser.Parse(packet, raw.Utf8Json)
                    : null;
                results[index] = new PreparedPass1JobDispatchResult(
                    packet.JobId,
                    packet.PairSelectionOrdinal,
                    raw.Outcome,
                    parsed,
                    raw.ReasonCode);
            }

            return new PreparedPass1DispatchResult(packets, results);
        }

        private static PreparedPass1WorkerPacket CreatePacket(
            PreparedPass1DispatchRequest request,
            PreparedPass1FileCodeUnitAssignment assignment)
        {
            PreparedPass1ContextSnapshotProjection snapshot = request.ContextSnapshots.Single(
                value => value.ContextSnapshotId == assignment.ContextSnapshotId);
            PreparedPass1CommunicationClassificationProjection[] classifications = [
                .. request.CommunicationClassifications
                    .Where(value => value.FileCodeUnitId == assignment.FileCodeUnitId),
            ];
            int byteCount = checked((int)(assignment.ByteEnd - assignment.ByteStart));
            byte[] source = CreateSource(assignment.JobId, byteCount);
            string sourceHash = Convert.ToHexStringLower(SHA256.HashData(source));
            return new PreparedPass1WorkerPacket(
                assignment.PairExecutionId,
                assignment.JobId,
                assignment.ScanExecutionId,
                assignment.SystemId,
                assignment.RepositoryId,
                assignment.ScanRunId,
                assignment.ProfiledRevision,
                assignment.FileCodeUnitId,
                assignment.FileCodeUnitIdentityFingerprint,
                assignment.PreparedScanManifestId,
                assignment.PreparedScanManifestFingerprint,
                assignment.PairPackageFingerprint,
                assignment.RepositoryRelativePath,
                assignment.ByteStart,
                assignment.ByteEnd,
                assignment.LineStart,
                assignment.LineEnd,
                assignment.SegmentOrdinal,
                assignment.SegmentCount,
                assignment.FileHash,
                sourceHash,
                assignment.PromptIdentity,
                assignment.DocumentSchemaIdentity,
                assignment.AnalysisRuntimeIdentityHash,
                assignment.ManifestOrdinal,
                assignment.PairSelectionOrdinal,
                source,
                snapshot,
                classifications)
            {
                RemainingNavigationBudget = assignment.RemainingNavigationBudget,
            };
        }

        private static byte[] CreateSource(Guid jobId, int byteCount)
        {
            byte[] seed = Encoding.UTF8.GetBytes($"// t050 source {jobId:D}\n");
            byte[] source = new byte[byteCount];
            for (int index = 0; index < source.Length; index++)
            {
                source[index] = seed[index % seed.Length];
            }

            return source;
        }
    }

    private sealed class ControllableWorker : IPreparedPass1JobExecutor
    {
        private readonly ConcurrentDictionary<int, ConcurrentQueue<TaskCompletionSource>> _releaseByOrdinal = new();
        private readonly ConcurrentQueue<int> _startedOrdinals = new();
        private int _activeCount;
        private int _maximumActiveCount;
        private int _startedCount;
        private int _autoRelease;
        private int _outcome = (int)PreparedPass1RawWorkerOutcome.Completed;

        public int StartedCount => Volatile.Read(ref _startedCount);
        public int MaximumActiveCount => Volatile.Read(ref _maximumActiveCount);
        public IReadOnlyList<int> StartedOrdinals => _startedOrdinals.ToArray();
        public bool CancellationResistant { get; set; }
        public bool UseProviderUnavailableReasonCode { get; set; }

        public async Task<PreparedPass1RawWorkerResult> ExecuteAsync(
            PreparedPass1WorkerPacket packet,
            string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _releaseByOrdinal.GetOrAdd(packet.PairSelectionOrdinal, _ => new()).Enqueue(release);
            int active = Interlocked.Increment(ref _activeCount);
            UpdateMaximumActiveCount(active);
            _startedOrdinals.Enqueue(packet.PairSelectionOrdinal);
            Interlocked.Increment(ref _startedCount);
            if (Volatile.Read(ref _autoRelease) == 1)
            {
                release.TrySetResult();
            }
            try
            {
                if (CancellationResistant)
                {
                    await release.Task;
                }
                else
                {
                    await release.Task.WaitAsync(cancellationToken);
                }
                PreparedPass1RawWorkerOutcome outcome = (PreparedPass1RawWorkerOutcome)Volatile.Read(ref _outcome);
                if (outcome != PreparedPass1RawWorkerOutcome.Completed)
                {
                    return new(
                        outcome,
                        ReadOnlyMemory<byte>.Empty,
                        UseProviderUnavailableReasonCode &&
                        outcome == PreparedPass1RawWorkerOutcome.ProviderUnavailable
                            ? "provider-unavailable"
                            : "t050-scripted-terminal");
                }
                return new(
                    PreparedPass1RawWorkerOutcome.Completed,
                    CreateCanonicalResult(packet),
                    null);
            }
            finally
            {
                Interlocked.Decrement(ref _activeCount);
            }
        }

        public async Task<bool> WaitForAtLeastAsync(int count, TimeSpan timeout)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (StartedCount >= count)
                {
                    return true;
                }

                await Task.Delay(50);
            }

            return StartedCount >= count;
        }

        public void ReleaseOrdinal(int pairSelectionOrdinal)
        {
            if (_releaseByOrdinal.TryGetValue(pairSelectionOrdinal, out ConcurrentQueue<TaskCompletionSource>? releases) &&
                releases.TryDequeue(out TaskCompletionSource? release))
            {
                release.TrySetResult();
            }
        }

        public void ReleaseSchedule(IEnumerable<int> pairSelectionOrdinals)
        {
            foreach (int ordinal in pairSelectionOrdinals)
            {
                ReleaseOrdinal(ordinal);
            }
        }

        public void ReleaseAll()
        {
            foreach (ConcurrentQueue<TaskCompletionSource> releases in _releaseByOrdinal.Values)
            {
                while (releases.TryDequeue(out TaskCompletionSource? release))
                {
                    release.TrySetResult();
                }
            }
        }

        public void EnableAutoRelease() => Interlocked.Exchange(ref _autoRelease, 1);

        public void SetOutcome(PreparedPass1RawWorkerOutcome outcome) =>
            Volatile.Write(ref _outcome, (int)outcome);

        private void UpdateMaximumActiveCount(int active)
        {
            while (true)
            {
                int current = Volatile.Read(ref _maximumActiveCount);
                if (active <= current || Interlocked.CompareExchange(
                        ref _maximumActiveCount,
                        active,
                        current) == current)
                {
                    return;
                }
            }
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset _current = current;

        public override DateTimeOffset GetUtcNow() => _current;

        public void Advance(TimeSpan amount) =>
            _current = _current.Add(amount);
    }
}

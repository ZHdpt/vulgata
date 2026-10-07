using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Actual Scan scope plus actual resolution executor; only accepted input, provider response and disclosure authority are controlled.</summary>
public sealed class OctoberBusinessRealScopeTests
{
    private static Guid Run => ResolutionCase.Id(8100);
    private static OperationKey Finalization => new(ResolutionCase.Id(1), OperationKind.FinalizeCorpus, ResolutionCase.Id(8101));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_RealClOnlyScope_RefusesBusinessDispatchAsync()
    {
        var fixture = new ResolutionCase();
        var clock = new ScopeClock();
        await using var scope = Scope(clock, Policy() with { OutputStage = "cl-only" });
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(Response(fixture)));
        var result = await ExecuteAsync(fixture, client, scope, clock);
        Assert.Equal(ProviderAdmissionStatus.Denied, result.AdmissionFailure);
        Assert.Null(result.Outcome);
        Assert.Empty(result.Attempts);
        Assert.Equal(0, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("operation-id")]
    [InlineData("operation-kind")]
    [InlineData("unregistered")]
    [InlineData("profile")]
    public async Task ExecuteAsync_RealScopeExactRegistration_RejectsOtherOperationOrProfileAsync(string mismatch)
    {
        var fixture = new ResolutionCase();
        var clock = new ScopeClock();
        await using var scope = new OctoberExecutionScope(Run, "selected-profile", Policy(), Eligible,
            mismatch == "unregistered" ? null : Finalization, clock);
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(Response(fixture)));
        var operation = mismatch switch
        {
            "operation-id" => Finalization with { OperationId = Guid.NewGuid() },
            "operation-kind" => Finalization with { Kind = OperationKind.AcceptResolution },
            _ => Finalization
        };
        var result = await ExecuteAsync(fixture, client, scope, clock, operation,
            mismatch == "profile" ? "foreign-profile" : "selected-profile");
        Assert.Equal(ProviderAdmissionStatus.Denied, result.AdmissionFailure);
        Assert.Null(result.Outcome);
        Assert.Empty(result.Attempts);
        Assert.Equal(0, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_TwoInquiriesOnRealCapOne_NoParentPermitOrConcurrentProviderSendAsync()
    {
        var fixture = new ResolutionCase();
        var clock = new ScopeClock();
        await using var scope = Scope(clock);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ResolutionClient("selected-profile", async (call, _) =>
        {
            if (call == 1) await release.Task;
            return Response(fixture);
        });
        var first = ExecuteAsync(fixture, client, scope, clock);
        Task<ResolutionExecutionResult>? second = null;
        try
        {
            await client.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            second = ExecuteAsync(fixture, client, scope, clock);
            Assert.False(second.IsCompleted);
            Assert.Equal(1, client.Calls);
            clock.Advance(TimeSpan.FromSeconds(1)); // Mature the real host's next rate slot before capacity is released.
        }
        finally { release.TrySetResult(); }
        var completed = await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ResolutionOutcomeKind.Supported, completed.Outcome!.Kind);
        Assert.NotNull(second);
        Assert.Equal(ResolutionOutcomeKind.Supported, (await second.WaitAsync(TimeSpan.FromSeconds(5))).Outcome!.Kind);
        Assert.Equal(2, client.Calls);
        Assert.Equal(1, client.PeakActive);
        Assert.Single(completed.Attempts);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_WorkerAndResolutionShareRealScope_UsesOnePermitAndOneActualTokenAllowanceAsync()
    {
        var fixture = new ResolutionCase();
        var clock = new ScopeClock();
        await using var scope = Scope(clock);
        var granted = await scope.AcquireWorkerAttemptAsync(Worker(30));
        Assert.Equal(ProviderAdmissionStatus.Granted, granted.Status);
        var worker = Assert.IsAssignableFrom<IOctoberProviderAttemptLease>(granted.Lease);
        Assert.Equal(ProviderAdmissionStatus.Granted, await worker.CheckBeforeSendAsync());
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(Response(fixture)));
        var inquiry = ExecuteAsync(fixture, client, scope, clock);
        Assert.False(inquiry.IsCompleted);
        Assert.Equal(0, client.Calls);
        clock.Advance(TimeSpan.FromSeconds(1));
        await worker.SettleAsync(new(20, 0, null, TimeSpan.Zero, TimeSpan.FromSeconds(1), ProviderAttemptOutcome.Completed));
        await worker.DisposeAsync();
        var completed = await inquiry.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ResolutionOutcomeKind.Supported, completed.Outcome!.Kind);
        Assert.Equal(1, client.Calls);
        var usage = Assert.Single(completed.Attempts);
        Assert.Equal(10, usage.InputTokens);
        Assert.Equal(5, usage.OutputTokens);

        // From one total allowance of 100: Worker actually used 20 and Resolution 15.
        clock.Advance(TimeSpan.FromSeconds(1));
        var rest = await scope.AcquireWorkerAttemptAsync(Worker(65));
        Assert.Equal(ProviderAdmissionStatus.Granted, rest.Status);
        var remaining = Assert.IsAssignableFrom<IOctoberProviderAttemptLease>(rest.Lease);
        Assert.Equal(ProviderAdmissionStatus.Granted, await remaining.CheckBeforeSendAsync());
        await remaining.SettleAsync(new(65, 0, null, TimeSpan.Zero, TimeSpan.FromSeconds(1), ProviderAttemptOutcome.Completed));
        await remaining.DisposeAsync();
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, (await scope.AcquireWorkerAttemptAsync(Worker(1))).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_RealHardStop_CancelsActiveDeniesQueuedAndNeverAcceptsLateTransportResultAsync()
    {
        var fixture = new ResolutionCase();
        var clock = new ScopeClock();
        await using var scope = Scope(clock);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ResolutionClient("selected-profile", async (_, _) =>
        {
            await release.Task; // Controlled transport intentionally delays actual settlement after cancellation.
            return Response(fixture);
        });
        var active = ExecuteAsync(fixture, client, scope, clock);
        Task<ResolutionExecutionResult>? queued = null;
        try
        {
            await client.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            queued = ExecuteAsync(fixture, client, scope, clock);
            Assert.False(queued.IsCompleted);
            scope.HardStop();
            Assert.True(client.LastCancellation.IsCancellationRequested);
            Assert.False(active.IsCompleted);
            var pending = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask());
            Assert.Equal("october_provider_settlement_unconfirmed", pending.Message);
            Assert.Equal(ProviderAdmissionStatus.HardStopped, (await queued.WaitAsync(TimeSpan.FromSeconds(5))).AdmissionFailure);
        }
        finally
        {
            release.TrySetResult();
            await active.WaitAsync(TimeSpan.FromSeconds(5));
            if (queued is not null) await queued.WaitAsync(TimeSpan.FromSeconds(5));
        }
        var late = await active;
        Assert.Equal(ProviderAdmissionStatus.HardStopped, late.AdmissionFailure);
        Assert.Null(late.Outcome);
        Assert.Equal(1, client.Calls);
        Assert.Single(late.Attempts);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, (await scope.AcquireWorkerAttemptAsync(Worker(1))).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_RealScopeUnknownUsage_PreservesUnknownAndBlocksBothWorkerAndBusinessAllowanceAsync()
    {
        var fixture = new ResolutionCase();
        var clock = new ScopeClock();
        await using var scope = Scope(clock);
        using var client = new ResolutionClient("selected-profile", (_, _) => Task.FromResult(Response(fixture, knownUsage: false)));
        var completed = await ExecuteAsync(fixture, client, scope, clock);
        Assert.Equal(ResolutionOutcomeKind.Supported, completed.Outcome!.Kind);
        var usage = Assert.Single(completed.Attempts);
        Assert.Null(usage.InputTokens);
        Assert.Null(usage.OutputTokens);
        Assert.Null(usage.Cost);
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, (await scope.AcquireWorkerAttemptAsync(Worker(1))).Status);
        var next = await ExecuteAsync(fixture, client, scope, clock);
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, next.AdmissionFailure);
        Assert.Null(next.Outcome);
        Assert.Equal(1, client.Calls);
    }

    private static OctoberScanPolicy Policy() => new()
    {
        OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100,
        SourceRules = [new(ResolutionCase.Id(1003), "src", "selected", "Controlled component qualification")]
    };
    private static Task<ProviderAdmissionStatus> Eligible(OctoberProviderPhase phase, CancellationToken cancellationToken) =>
        Task.FromResult(ProviderAdmissionStatus.Granted);
    private static OctoberExecutionScope Scope(ScopeClock clock, OctoberScanPolicy? policy = null) =>
        new(Run, "selected-profile", policy ?? Policy(), Eligible, Finalization, clock);
    private static OctoberWorkerProviderAttemptRequest Worker(long tokens) =>
        new(Run, ResolutionCase.Id(8102), Guid.NewGuid(), "selected-profile", TimeSpan.FromSeconds(10), tokens);
    private static Task<ResolutionExecutionResult> ExecuteAsync(ResolutionCase fixture, ResolutionClient client,
        OctoberExecutionScope scope, ScopeClock clock, OperationKey? operation = null, string profile = "selected-profile") =>
        new OctoberResolutionExecutor(client, "Controlled component inquiry. Preserve accepted evidence and uncertainty; no tools.", clock)
            .ExecuteAsync(fixture.Corpus, fixture.Inquiry(), operation ?? Finalization, Run, profile, 40, scope, new ResolutionDisclosure());
    private static ChatResponse Response(ResolutionCase fixture, bool knownUsage = true) =>
        new(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(fixture.Witness())))
        {
            FinishReason = ChatFinishReason.Stop,
            Usage = knownUsage ? new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 } : null
        };

    // Only elapsed time is advanced. Tests mature the next rate slot before releasing a held
    // lease, so they require no timer pump, sleeps, scheduler replacement or provider runtime.
    private sealed class ScopeClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _ticks);
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref _ticks, elapsed.Ticks);
    }
}

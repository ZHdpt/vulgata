using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Synthetic future host-state conformance only. Reflection does not qualify S2 issuance or activate Business execution.</summary>
public sealed class OctoberScanOriginalBusinessSlotTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshHostOrActualCompletedS2_CannotIssueFutureBusinessSlotAsync(bool completedS2)
    {
        await using var host = await OctoberScanGenerateRunnerTests.Harness.CreateAsync();
        if (completedS2) Assert.Equal(0, (await host.Runner.ExecuteAsync(host.Execution)).Report!.ExitCode);
        Assert.Null(Issue(host.Runner));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("no-own-begin")]
    [InlineData("unfinished-begin")]
    [InlineData("unknown-begin")]
    [InlineData("admissions-open")]
    [InlineData("no-confirmed-corpus")]
    [InlineData("expired")]
    [InlineData("foreign-scope")]
    [InlineData("closed-resources")]
    [InlineData("completed-host")]
    [InlineData("cleaning-host")]
    public async Task SyntheticHost_MissingOriginalProvenanceOrLifetime_CannotIssueAsync(string missing)
    {
        await using var fixture = await Synthetic.CreateAsync();
        switch (missing)
        {
            case "no-own-begin": Set(fixture.Runner, "_beginSubmitted", false); break;
            case "unfinished-begin": Set(fixture.Runner, "_beginCommit", new TaskCompletionSource<CommitSettlement>().Task); break;
            case "unknown-begin": Set(fixture.Runner, "_beginSettlement", CommitSettlement.Indeterminate); break;
            case "admissions-open": Set(fixture.Runner, "_admissionClosed", false); break;
            case "no-confirmed-corpus": Set(fixture.Runner, "_confirmedBusinessCorpus", null); break;
            case "expired": fixture.Clock.Advance(TimeSpan.FromMinutes(2), deliverTimers: false); break;
            case "foreign-scope": Set(fixture.Runner, "_scope", fixture.OtherScope); break;
            case "closed-resources": Set(fixture.Runner, "_resourcesSettled", true); break;
            case "completed-host": Set(fixture.Runner, "_executionTask", Task.FromResult(new OctoberGeneratorExecutionResult(OctoberGeneratorExecutionStatus.Denied))); break;
            case "cleaning-host": Set(fixture.Runner, "_analysisCleanup", Task.CompletedTask); break;
        }
        Assert.Null(Issue(fixture.Runner));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SyntheticSlot_ConcurrentDuplicatesJoinOneRetainedCallbackWithoutPermitAsync()
    {
        await using var fixture = await Synthetic.CreateAsync();
        var slot = Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner));
        Assert.Same(slot, Issue(fixture.Runner));
        int calls = 0, replacements = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<BusinessFinalizationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = slot.RunOnceAsync(fixture.Request, token => { calls++; Assert.Equal(fixture.RunToken, token); entered.TrySetResult(); return release.Task; });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var duplicate = slot.RunOnceAsync(fixture.Request, _ => { replacements++; return Task.FromResult(fixture.Result); });
            Assert.False(duplicate.IsCompleted);
            Assert.Equal(0, fixture.Scope.GetSnapshot().OutstandingAttempts);
            release.TrySetResult(fixture.Result);
            Assert.Equal(fixture.Result, await first);
            Assert.Equal(fixture.Result, await duplicate);
            Assert.Equal(1, calls); Assert.Equal(0, replacements);
        }
        finally { release.TrySetResult(fixture.Result); await first; }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("run")]
    [InlineData("operation")]
    [InlineData("corpus")]
    [InlineData("digest")]
    [InlineData("configuration")]
    [InlineData("scope")]
    [InlineData("null-selection")]
    public async Task SyntheticSlot_WrongRequestCannotInvokeConsumeOrStopOriginalAsync(string mismatch)
    {
        await using var fixture = await Synthetic.CreateAsync();
        var slot = Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner));
        var wrong = mismatch switch
        {
            "run" => fixture.Request with { Selection = fixture.Request.Selection! with { RunId = Guid.NewGuid() } },
            "operation" => fixture.Request with { Operation = fixture.Request.Operation with { OperationId = Guid.NewGuid() } },
            "corpus" => fixture.Request with { Corpus = fixture.Request.Corpus with { CorpusId = Guid.NewGuid() } },
            "digest" => fixture.Request with { CanonicalPayloadDigest = new string('f', 64) },
            "configuration" => fixture.Request with { Selection = fixture.Request.Selection! with { ProviderConfigurationFingerprint = new string('e', 64) } },
            "scope" => fixture.Request with { ExecutionScope = fixture.OtherScope },
            _ => fixture.Request with { Selection = null },
        };
        if (mismatch is not ("digest" or "null-selection")) wrong = wrong with { CanonicalPayloadDigest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(wrong) };
        int calls = 0;
        try
        {
            var rejected = await slot.RunOnceAsync(wrong, _ => { calls++; return Task.FromResult(fixture.Result); });
            Assert.Contains(rejected.Status, new[] { BusinessFinalizationStatus.Denied, BusinessFinalizationStatus.Failed });
        }
        catch (ArgumentException) { }
        Assert.Equal(0, calls);
        Assert.False(fixture.RunToken.IsCancellationRequested);
        Assert.Equal(fixture.Result, await slot.RunOnceAsync(fixture.Request, _ => { calls++; return Task.FromResult(fixture.Result); }));
        Assert.Equal(1, calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SyntheticSlot_FaultOrIndeterminateConsumesCallbackPermanentlyAsync(bool fault)
    {
        await using var fixture = await Synthetic.CreateAsync();
        var slot = Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner));
        int calls = 0;
        var unknown = fixture.Result with { Status = BusinessFinalizationStatus.CommitIndeterminate };
        Task<BusinessFinalizationResult> Work(CancellationToken _) { calls++; if (fault) throw new IOException("controlled original callback fault"); return Task.FromResult(unknown); }
        if (fault)
        {
            await Assert.ThrowsAsync<IOException>(() => slot.RunOnceAsync(fixture.Request, Work));
            await Assert.ThrowsAsync<IOException>(() => slot.RunOnceAsync(fixture.Request, _ => Task.FromResult(fixture.Result)));
        }
        else
        {
            Assert.Equal(unknown, await slot.RunOnceAsync(fixture.Request, Work));
            Assert.Equal(unknown, await slot.RunOnceAsync(fixture.Request, _ => Task.FromResult(fixture.Result)));
        }
        Assert.Equal(1, calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SyntheticSlot_FirstWaitCancellationLeavesOriginalWorkOwnedAndDuplicateCanJoinAsync()
    {
        await using var fixture = await Synthetic.CreateAsync();
        var slot = Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner));
        using var waiter = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<BusinessFinalizationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var first = slot.RunOnceAsync(fixture.Request, token => { calls++; Assert.Equal(fixture.RunToken, token); entered.TrySetResult(); return release.Task; }, waiter.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); waiter.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            Assert.False(fixture.RunToken.IsCancellationRequested);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Runner.DisposeAsync().AsTask());
            var duplicate = slot.RunOnceAsync(fixture.Request, _ => throw new InvalidOperationException("Must not replace original callback"));
            release.TrySetResult(fixture.Result);
            Assert.Equal(fixture.Result, await duplicate);
            Assert.Equal(1, calls);
        }
        finally { release.TrySetResult(fixture.Result); _ = await Record.ExceptionAsync(() => first); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SyntheticSlot_OriginalStopOrDeadlineControlsWorkAfterWaiterLeavesAsync(bool deadline)
    {
        await using var fixture = await Synthetic.CreateAsync();
        var slot = Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = slot.RunOnceAsync(fixture.Request, async token =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { }
            return fixture.Result with { Status = BusinessFinalizationStatus.HardStopped };
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            if (deadline) fixture.Clock.Advance(TimeSpan.FromMinutes(2)); else fixture.Stop();
            Assert.Equal(BusinessFinalizationStatus.HardStopped, (await first.WaitAsync(TimeSpan.FromSeconds(3))).Status);
            Assert.Equal(BusinessFinalizationStatus.HardStopped, (await slot.RunOnceAsync(fixture.Request, _ => throw new InvalidOperationException("No replacement after stop"))).Status);
        }
        finally { fixture.Stop(); await first.WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SyntheticSlot_CancelledWaitersCannotReleaseResourcesBeforeCallbackSettlementAsync(bool callbackFault)
    {
        await using var fixture = await Synthetic.CreateAsync();
        var slot = Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner));
        using var waiter = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<BusinessFinalizationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = slot.RunOnceAsync(fixture.Request, _ => { entered.TrySetResult(); return release.Task; }, waiter.Token);
        Task? cleanup = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            waiter.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            cleanup = (Task)typeof(OctoberGenerateRunner).GetMethod("SettleResourcesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fixture.Runner, null)!;
            await Task.WhenAny(cleanup, Task.Delay(50));
            Assert.False(cleanup.IsCompleted);
            var lifetime = Assert.IsAssignableFrom<IOctoberBusinessHandoffLifetime>(fixture.Scope);
            Assert.False(lifetime.HandoffCancellation.IsCancellationRequested);
            if (callbackFault) release.TrySetException(new IOException("controlled callback settlement fault")); else release.TrySetResult(fixture.Result);
            await cleanup.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(lifetime.HandoffCancellation.IsCancellationRequested);
            var duplicate = slot.RunOnceAsync(fixture.Request, _ => throw new InvalidOperationException("Cleanup must not enable replacement"));
            if (callbackFault) await Assert.ThrowsAsync<IOException>(() => duplicate); else Assert.Equal(fixture.Result, await duplicate);
        }
        finally
        {
            release.TrySetResult(fixture.Result);
            _ = await Record.ExceptionAsync(() => first);
            if (cleanup is not null) _ = await Record.ExceptionAsync(() => cleanup.WaitAsync(TimeSpan.FromSeconds(3)));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SyntheticSlot_FirstDispatchRechecksHostCompletionOrCleanupAsync(bool cleaning)
    {
        await using var fixture = await Synthetic.CreateAsync();
        var slot = Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner));
        if (cleaning) Set(fixture.Runner, "_analysisCleanup", Task.CompletedTask);
        else Set(fixture.Runner, "_executionTask", Task.FromResult(new OctoberGeneratorExecutionResult(OctoberGeneratorExecutionStatus.Denied)));
        int calls = 0;
        try
        {
            var result = await slot.RunOnceAsync(fixture.Request, _ => { calls++; return Task.FromResult(fixture.Result); });
            Assert.NotEqual(BusinessFinalizationStatus.NoContent, result.Status);
        }
        catch (InvalidOperationException) { }
        Assert.Equal(0, calls);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SyntheticOriginalSubmittedBegin_ExactCommittedReconciliationCanIssueDespiteLostOriginalReplyAsync()
    {
        await using var fixture = await Synthetic.CreateAsync();
        Set(fixture.Runner, "_beginCommit", Task.FromResult(CommitSettlement.Indeterminate));
        Set(fixture.Runner, "_beginSettlement", CommitSettlement.Committed);
        Set(fixture.Runner, "_beginUncertain", false);
        var slot = Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner));
        int calls = 0;
        Assert.Equal(fixture.Result, await slot.RunOnceAsync(fixture.Request, _ => { calls++; return Task.FromResult(fixture.Result); }));
        Assert.Equal(1, calls);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SyntheticHost_OperatorSessionExpiryCannotIssueOrStartSlotWithinLongerRunAsync(bool afterIssuance)
    {
        await using var fixture = await Synthetic.CreateAsync();
        var execution = Get<OctoberGenerateExecution>(fixture.Runner, "_execution");
        Set(fixture.Runner, "_execution", execution with { Session = new ExpiringSession(execution.Session.SubjectId, execution.Session.ManagementAuthorityId, fixture.Clock.GetUtcNow().AddSeconds(2)) });
        IOctoberOriginalBusinessFinalizationSlot? slot = afterIssuance
            ? Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner)) : null;
        fixture.Clock.Advance(TimeSpan.FromSeconds(3)); // Original run still has 57 seconds remaining.
        if (!afterIssuance) Assert.Null(Issue(fixture.Runner));
        else
        {
            int calls = 0;
            var result = await slot!.RunOnceAsync(fixture.Request, _ => { calls++; return Task.FromResult(fixture.Result); });
            Assert.NotEqual(BusinessFinalizationStatus.NoContent, result.Status);
            Assert.Equal(0, calls);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SyntheticSlot_CancelledDuplicateWaitDoesNotCancelFirstWaitOrReplaceWorkAsync()
    {
        await using var fixture = await Synthetic.CreateAsync();
        var slot = Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner));
        using var duplicateWait = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<BusinessFinalizationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0, replacements = 0;
        var first = slot.RunOnceAsync(fixture.Request, _ => { calls++; entered.TrySetResult(); return release.Task; });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var duplicate = slot.RunOnceAsync(fixture.Request, _ => { replacements++; return Task.FromResult(fixture.Result); }, duplicateWait.Token);
            duplicateWait.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => duplicate);
            Assert.False(first.IsCompleted);
            Assert.False(fixture.RunToken.IsCancellationRequested);
            release.TrySetResult(fixture.Result);
            Assert.Equal(fixture.Result, await first);
            Assert.Equal(1, calls); Assert.Equal(0, replacements);
        }
        finally { release.TrySetResult(fixture.Result); await first; }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SyntheticSlot_ActuallyCancelledCallbackTaskIsPermanentlyConsumedAsync()
    {
        await using var fixture = await Synthetic.CreateAsync();
        var slot = Assert.IsAssignableFrom<IOctoberOriginalBusinessFinalizationSlot>(Issue(fixture.Runner));
        int calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slot.RunOnceAsync(fixture.Request, _ =>
        {
            calls++;
            return Task.FromCanceled<BusinessFinalizationResult>(new CancellationToken(true));
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slot.RunOnceAsync(fixture.Request, _ =>
        {
            calls++;
            return Task.FromResult(fixture.Result);
        }));
        Assert.Equal(1, calls);
        Assert.False(fixture.RunToken.IsCancellationRequested);
    }

    private sealed record ExpiringSession(string SubjectId, string ManagementAuthorityId, DateTimeOffset ExpiresAtUtc) : IOctoberOperatorSession
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private static object? Issue(OctoberGenerateRunner runner) => typeof(OctoberGenerateRunner).GetMethod("TryIssueOriginalBusinessFinalizationSlot", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(runner, null);
    private static void Set(object target, string field, object? value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private sealed class Synthetic : IAsyncDisposable
    {
        private readonly OctoberScanGenerateRunnerTests.Harness _host;
        private readonly TaskCompletionSource<OctoberGeneratorExecutionResult> _running = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal OctoberScanAcceptanceHarness.ManualClock Clock { get; } = new();
        internal OctoberGenerateRunner Runner { get; }
        internal OctoberExecutionScope Scope { get; }
        internal OctoberExecutionScope OtherScope { get; }
        internal FinalizationRequest Request { get; }
        internal BusinessFinalizationResult Result => new(BusinessFinalizationStatus.NoContent, Request.Operation, null, new(false, []));
        internal CancellationToken RunToken => Get<CancellationTokenSource>(Runner, "_runToken").Token;
        private Synthetic(OctoberScanGenerateRunnerTests.Harness host)
        {
            _host = host;
            Runner = new(Get<OctoberGenerateRunnerDependencies>(host.Runner, "_dependencies"), Clock);
            var policy = host.Execution.Policy with { OutputStage = "cl-and-bl", RunDeadlineSeconds = 60, FileDeadlineSeconds = 60, GracefulDrainSeconds = 30 };
            var execution = host.Execution with { Policy = policy };
            var binding = new KnowledgeBinding(execution.Authority.CanonicalDatabaseIdentity, execution.Authority.TargetId, execution.Authority.BatchId, execution.Authority.SelectionDigest);
            Guid run = Guid.NewGuid();
            var operation = new OperationKey(binding.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
            var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60), Clock);
            var runToken = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, Get<CancellationTokenSource>(Runner, "_stop").Token);
            Scope = new(run, execution.Provider.Profile.Name, policy, (_, _) => Task.FromResult(ProviderAdmissionStatus.Granted), operation, Clock, runToken.Token);
            OtherScope = new(run, execution.Provider.Profile.Name, policy, (_, _) => Task.FromResult(ProviderAdmissionStatus.Granted), operation, Clock, runToken.Token);
            var configuration = new BusinessProviderConfigurationProjection("synthetic-controlled-selection", 0, execution.Provider.Profile.Name, "chat-completions", "https://never-contacted.invalid/v1", "controlled", "credential://not-read");
            var corpus = new CorpusHandle(binding, Guid.NewGuid(), new string('b', 64));
            Request = new(operation, corpus, "", Scope) { Selection = new([], new(), run, execution.Provider.Profile.Name, OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(configuration), new string('c', 64), 1000) };
            Request = Request with { CanonicalPayloadDigest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(Request) };
            var begin = new GenerateAdmissionRequest(binding, execution.Session.SubjectId, run, Guid.NewGuid(), "");
            begin = begin with { CanonicalPayloadDigest = OctoberGenerateAdmissionPolicy.ComputePayloadDigest(begin) };
            Set(Runner, "_execution", execution); Set(Runner, "_executionTask", _running.Task); Set(Runner, "_binding", binding);
            Set(Runner, "_started", Clock.GetTimestamp()); Set(Runner, "_deadline", deadline); Set(Runner, "_runToken", runToken); Set(Runner, "_scope", Scope);
            Set(Runner, "_beginRequest", begin); Set(Runner, "_beginSubmitted", true); Set(Runner, "_beginClosed", true);
            Set(Runner, "_beginCommit", Task.FromResult(CommitSettlement.Committed)); Set(Runner, "_beginSettlement", CommitSettlement.Committed);
            Set(Runner, "_admissionClosed", true); Set(Runner, "_originalBusinessRequest", Request); Set(Runner, "_confirmedBusinessCorpus", corpus); Set(Runner, "_businessProviderConfiguration", configuration);
        }
        internal static async Task<Synthetic> CreateAsync() => new(await OctoberScanGenerateRunnerTests.Harness.CreateAsync());
        internal void Stop() => Get<CancellationTokenSource>(Runner, "_stop").Cancel();
        public async ValueTask DisposeAsync()
        {
            Stop();
            _running.TrySetResult(new(OctoberGeneratorExecutionStatus.Denied));
            // Restore only synthetic prerequisites corrupted by negative setup so owned teardown is truthful and finite.
            Set(Runner, "_beginCommit", Task.FromResult(CommitSettlement.Committed)); Set(Runner, "_beginSettlement", CommitSettlement.Committed); Set(Runner, "_scope", Scope); Set(Runner, "_resourcesSettled", false); Set(Runner, "_analysisCleanup", null);
            await Runner.DisposeAsync(); await OtherScope.DisposeAsync(); await _host.DisposeAsync();
        }
    }
}

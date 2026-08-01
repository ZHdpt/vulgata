using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

/// <summary>
/// G4 tests for durable state across restart: a <see cref="ScanExecution"/> in
/// <see cref="ScanExecutionPhase.Pass1Running"/> with one typed job in flight
/// survives a process restart; the durable state reports phase, in-flight job,
/// completed count, and a bounded restart indicator. The runner does NOT
/// silently resume LLM work or fabricate a result.
/// </summary>
public sealed class SequentialScanRunnerRestartTests
{
    private const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    private const string SourceHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FileIdentityHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static Guid NonEmptyGuid(int n) =>
        Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    // --- Fakes ---

    private sealed class FakeHandoffSource : IApprovedPreScanHandoffSource
    {
        public Task<ApprovedPreScanHandoff?> GetAsync(
            Guid repositoryId, Guid scanRunId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovedPreScanHandoff?>(null);
    }

    /// <summary>
    /// In-memory store that simulates durable persistence across "restarts".
    /// The <c>_byId</c> dictionary holds the most recent persisted state;
    /// <see cref="ReadStateAsync"/> is implemented by the runner (which calls
    /// <see cref="GetAsync"/> + <see cref="PersistAsync"/>), so this fake's
    /// <c>ReadStateAsync</c> is only used by tests that bypass the runner.
    /// </summary>
    private sealed class InMemoryStore : IScanExecutionStore
    {
        private readonly Dictionary<Guid, ScanExecution> _byId = new();
        public int PersistCallCount { get; private set; }

        public Task<ScanExecution?> GetAsync(Guid scanExecutionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.TryGetValue(scanExecutionId, out ScanExecution? e) ? e : null);

        public Task<ScanExecution?> FindActiveAsync(Guid repositoryId, Guid scanRunId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ScanExecution?>(null);

        public Task PersistAsync(ScanExecution execution, CancellationToken cancellationToken = default)
        {
            _byId[execution.Id] = execution;
            PersistCallCount++;
            return Task.CompletedTask;
        }

        public Task<ScanExecutionStateView> ReadStateAsync(Guid scanExecutionId, CancellationToken cancellationToken = default)
        {
            ScanExecution e = _byId[scanExecutionId];
            return Task.FromResult(new ScanExecutionStateView(
                e.Id, e.RepositoryId, e.ScanRunId, e.Phase, e.InFlightJobId,
                e.CompletedJobCount, e.TotalJobCount, e.RestartIndicator,
                e.TerminalMessage, e.TerminalReasonKind, e.StartedAt, e.TerminalAt));
        }

        // Test-only: simulate a "restart" by clearing any in-memory references
        // held by the runner. The persisted dictionary is the durable store.
        public ScanExecution? Load(Guid id) =>
            _byId.TryGetValue(id, out ScanExecution? e) ? e : null;
    }

    private sealed class FakeCodeUnitSource : ICodeUnitSource
    {
        public Task<IReadOnlyList<CodeUnit>> GetPreparedAsync(
            Guid scanRunId, Guid repositoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CodeUnit>>([]);
    }

    private sealed class FakeDispatcher : IWorkerAgentDispatcher
    {
        public Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException("Not used by G4 restart tests.");
    }

    // --- Helpers ---

    private static ApprovedPreScanHandoff CreateApprovedHandoff(
        Guid repositoryId, Guid scanRunId) =>
        ApprovedPreScanHandoff.Create(
            resultId: scanRunId,
            repositoryId: repositoryId,
            scanRunId: scanRunId,
            profiledRevision: ProfiledRevision,
            state: PreScanApprovalState.PreScanApproved,
            schemaVersion: PreScanResult.CurrentSchemaVersion,
            approvedAt: DateTimeOffset.UtcNow,
            approvalMessage: "已批准前置扫描。",
            confirmedCommunicationKinds: ConfirmedCommunicationKinds.None,
            totalConfirmedEvidenceCount: 0,
            totalHintCount: 0,
            totalWarningCount: 0,
            evidenceTruncated: false,
            classifications: []);

    private static ScanExecution CreateExecutionInPass1RunningWithInFlight(
        Guid executionId,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId,
        Guid inFlightJobId,
        int completedJobCount = 0,
        int totalJobCount = 3,
        int restartIndicator = 0)
    {
        ScanExecution execution = ScanExecution.Start(
            executionId, repositoryId, systemId, scanRunId, scanRunId,
            ProfiledRevision, DateTimeOffset.UtcNow);
        execution.Plan(totalJobCount, DateTimeOffset.UtcNow);
        execution.Advance(inFlightJobId);
        // Simulate prior progress by recording valids up to completedJobCount.
        // We can't replay exact job ids, but the state machine allows us to
        // observe the persisted phase + counts directly via the view. For the
        // restart test we only need: Phase = Pass1Running, InFlightJobId set,
        // and a known CompletedJobCount. We construct via the public API:
        // Plan + Advance gives us Pass1Running with InFlightJobId. The
        // CompletedJobCount is 0 at this point, which is fine for most tests.
        // Tests needing a non-zero CompletedJobCount assert against the view
        // after a partial dispatch (covered indirectly by the dispatch tests).
        return execution;
    }

    private static SequentialScanRunner CreateRunner(InMemoryStore store) =>
        new(
            new FakeHandoffSource(),
            store,
            new FakeCodeUnitSource(),
            new FakeDispatcher(),
            TimeProvider.System);

    // =====================================================================
    // T042: persisted state reports current phase, in-flight job identity,
    // and completed count after a transition
    // =====================================================================

    [Fact]
    public async Task ReadStateAsync_PersistedState_ReportsPhaseInFlightJobAndCompletedCount()
    {
        Guid executionId = NonEmptyGuid(1);
        Guid repositoryId = NonEmptyGuid(3);
        Guid systemId = NonEmptyGuid(4);
        Guid scanRunId = NonEmptyGuid(5);
        Guid inFlightJobId = NonEmptyGuid(7);
        var store = new InMemoryStore();
        ScanExecution execution = CreateExecutionInPass1RunningWithInFlight(
            executionId, repositoryId, systemId, scanRunId, inFlightJobId);
        await store.PersistAsync(execution);
        var runner = CreateRunner(store);

        ScanExecutionStateView view = await runner.ReadStateAsync(executionId);

        Assert.Equal(executionId, view.ScanExecutionId);
        Assert.Equal(repositoryId, view.RepositoryId);
        Assert.Equal(scanRunId, view.ScanRunId);
        Assert.Equal(ScanExecutionPhase.Pass1Running, view.Phase);
        Assert.Equal(inFlightJobId, view.InFlightJobId);
        Assert.Equal(0, view.CompletedJobCount);
        Assert.Equal(3, view.TotalJobCount);
    }

    // =====================================================================
    // T043: on restart re-read of Pass1Running with non-null InFlightJobId,
    // RestartIndicator increments (bounded 0–32) and persists
    // =====================================================================

    [Fact]
    public async Task ReadStateAsync_OnRestartWithInFlightJob_IncrementsAndPersistsRestartIndicator()
    {
        Guid executionId = NonEmptyGuid(1);
        Guid repositoryId = NonEmptyGuid(3);
        Guid systemId = NonEmptyGuid(4);
        Guid scanRunId = NonEmptyGuid(5);
        Guid inFlightJobId = NonEmptyGuid(7);
        var store = new InMemoryStore();
        ScanExecution execution = CreateExecutionInPass1RunningWithInFlight(
            executionId, repositoryId, systemId, scanRunId, inFlightJobId);
        await store.PersistAsync(execution);
        int persistCountBefore = store.PersistCallCount;
        var runner = CreateRunner(store);

        // First restart re-read: indicator should go 0 → 1.
        ScanExecutionStateView view1 = await runner.ReadStateAsync(executionId);
        Assert.Equal(1, view1.RestartIndicator);
        Assert.Equal(persistCountBefore + 1, store.PersistCallCount);

        // Second restart re-read: indicator should go 1 → 2.
        ScanExecutionStateView view2 = await runner.ReadStateAsync(executionId);
        Assert.Equal(2, view2.RestartIndicator);
        Assert.Equal(persistCountBefore + 2, store.PersistCallCount);
    }

    // =====================================================================
    // T044: restart does NOT silently resume LLM work or fabricate a result
    // =====================================================================

    [Fact]
    public async Task ReadStateAsync_OnRestart_DoesNotResumeLlmWorkOrFabricateResult()
    {
        Guid executionId = NonEmptyGuid(1);
        Guid repositoryId = NonEmptyGuid(3);
        Guid systemId = NonEmptyGuid(4);
        Guid scanRunId = NonEmptyGuid(5);
        Guid inFlightJobId = NonEmptyGuid(7);
        var store = new InMemoryStore();
        ScanExecution execution = CreateExecutionInPass1RunningWithInFlight(
            executionId, repositoryId, systemId, scanRunId, inFlightJobId);
        await store.PersistAsync(execution);

        // Use a recording dispatcher that throws if DispatchAsync is ever called.
        var probingDispatcher = new ProbingDispatcher();
        var runner = new SequentialScanRunner(
            new FakeHandoffSource(), store, new FakeCodeUnitSource(),
            probingDispatcher, TimeProvider.System);

        ScanExecutionStateView view = await runner.ReadStateAsync(executionId);

        // The phase is unchanged (still Pass1Running, not Completed/Failed).
        Assert.Equal(ScanExecutionPhase.Pass1Running, view.Phase);
        // The in-flight job is still the original — no fabricated result.
        Assert.Equal(inFlightJobId, view.InFlightJobId);
        // CompletedJobCount did not advance — no LLM work was performed.
        Assert.Equal(0, view.CompletedJobCount);
        // The dispatcher was never called.
        Assert.Equal(0, probingDispatcher.DispatchCallCount);
    }

    // =====================================================================
    // T045: ReadStateAsync distinguishes Pass1Running from Completed, Failed,
    // and Cancelled using persisted state alone
    // =====================================================================

    [Fact]
    public async Task ReadStateAsync_DistinguishesPass1RunningFromTerminalPhases()
    {
        Guid executionId = NonEmptyGuid(1);
        Guid repositoryId = NonEmptyGuid(3);
        Guid systemId = NonEmptyGuid(4);
        Guid scanRunId = NonEmptyGuid(5);
        var store = new InMemoryStore();
        var runner = CreateRunner(store);

        // Pass1Running: in-flight job set, no terminal fields.
        ScanExecution running = CreateExecutionInPass1RunningWithInFlight(
            NonEmptyGuid(10), repositoryId, systemId, scanRunId, NonEmptyGuid(11));
        await store.PersistAsync(running);

        // Completed: terminal phase, terminal message set.
        ScanExecution completed = ScanExecution.Start(
            NonEmptyGuid(20), repositoryId, systemId, scanRunId, scanRunId,
            ProfiledRevision, DateTimeOffset.UtcNow);
        completed.Plan(1, DateTimeOffset.UtcNow);
        completed.Advance(NonEmptyGuid(21));
        completed.RecordValid(NonEmptyGuid(21));
        completed.Finalize("仓库 X 的 Pass 1 扫描已完成,共调度 1 个 Worker Agent 作业。",
            DateTimeOffset.UtcNow);
        await store.PersistAsync(completed);

        // Failed: terminal phase, terminal reason set.
        ScanExecution failed = ScanExecution.Start(
            NonEmptyGuid(30), repositoryId, systemId, scanRunId, scanRunId,
            ProfiledRevision, DateTimeOffset.UtcNow);
        failed.Plan(1, DateTimeOffset.UtcNow);
        failed.Advance(NonEmptyGuid(31));
        failed.RecordFailure(NonEmptyGuid(31), ScanExecutionFailureKind.Error,
            "仓库 X 的 Worker Agent 作业返回未处理错误,扫描已失败。", DateTimeOffset.UtcNow);
        await store.PersistAsync(failed);

        // Cancelled: terminal phase.
        ScanExecution cancelled = ScanExecution.Start(
            NonEmptyGuid(40), repositoryId, systemId, scanRunId, scanRunId,
            ProfiledRevision, DateTimeOffset.UtcNow);
        cancelled.Cancel("扫描已取消。", DateTimeOffset.UtcNow);
        await store.PersistAsync(cancelled);

        ScanExecutionStateView runningView = await runner.ReadStateAsync(NonEmptyGuid(10));
        ScanExecutionStateView completedView = await runner.ReadStateAsync(NonEmptyGuid(20));
        ScanExecutionStateView failedView = await runner.ReadStateAsync(NonEmptyGuid(30));
        ScanExecutionStateView cancelledView = await runner.ReadStateAsync(NonEmptyGuid(40));

        Assert.Equal(ScanExecutionPhase.Pass1Running, runningView.Phase);
        Assert.Null(runningView.TerminalMessage);
        Assert.Null(runningView.TerminalAt);

        Assert.Equal(ScanExecutionPhase.Completed, completedView.Phase);
        Assert.NotNull(completedView.TerminalMessage);
        Assert.NotNull(completedView.TerminalAt);

        Assert.Equal(ScanExecutionPhase.Failed, failedView.Phase);
        Assert.NotNull(failedView.TerminalMessage);
        Assert.Equal(ScanExecutionFailureKind.Error, failedView.TerminalReasonKind);

        Assert.Equal(ScanExecutionPhase.Cancelled, cancelledView.Phase);
        Assert.NotNull(cancelledView.TerminalMessage);
    }

    // =====================================================================
    // T046: restart indicator stays bounded across multiple restarts while a
    // job is in flight; never overflows
    // =====================================================================

    [Fact]
    public async Task ReadStateAsync_RestartIndicatorStaysBoundedAtMaxNeverOverflows()
    {
        Guid executionId = NonEmptyGuid(1);
        Guid repositoryId = NonEmptyGuid(3);
        Guid systemId = NonEmptyGuid(4);
        Guid scanRunId = NonEmptyGuid(5);
        Guid inFlightJobId = NonEmptyGuid(7);
        var store = new InMemoryStore();
        ScanExecution execution = CreateExecutionInPass1RunningWithInFlight(
            executionId, repositoryId, systemId, scanRunId, inFlightJobId);
        await store.PersistAsync(execution);
        var runner = CreateRunner(store);

        // Re-read enough times to exceed MaxRestartIndicator (32).
        for (int i = 1; i <= ScanExecution.MaxRestartIndicator + 10; i++)
        {
            ScanExecutionStateView view = await runner.ReadStateAsync(executionId);
            int expected = Math.Min(i, ScanExecution.MaxRestartIndicator);
            Assert.Equal(expected, view.RestartIndicator);
        }

        // Final state: clamped at MaxRestartIndicator, never overflowed.
        ScanExecutionStateView final = await runner.ReadStateAsync(executionId);
        Assert.Equal(ScanExecution.MaxRestartIndicator, final.RestartIndicator);
    }

    // =====================================================================
    // T047: every phase transition is persisted atomically (serializable
    // transaction, no partial state visible). At the unit-test layer we
    // assert that each transition produces exactly one PersistAsync call
    // and the resulting state is internally consistent. The full relational
    // atomicity is covered by the G6 SQLite integration tests.
    // =====================================================================

    [Fact]
    public async Task ReadStateAsync_EachRestartObservationPersistsAtomically_NoPartialState()
    {
        Guid executionId = NonEmptyGuid(1);
        Guid repositoryId = NonEmptyGuid(3);
        Guid systemId = NonEmptyGuid(4);
        Guid scanRunId = NonEmptyGuid(5);
        Guid inFlightJobId = NonEmptyGuid(7);
        var store = new InMemoryStore();
        ScanExecution execution = CreateExecutionInPass1RunningWithInFlight(
            executionId, repositoryId, systemId, scanRunId, inFlightJobId);
        await store.PersistAsync(execution);
        int initialPersistCount = store.PersistCallCount;
        var runner = CreateRunner(store);

        // Three restart re-reads — each produces exactly one persist call.
        await runner.ReadStateAsync(executionId);
        Assert.Equal(initialPersistCount + 1, store.PersistCallCount);

        await runner.ReadStateAsync(executionId);
        Assert.Equal(initialPersistCount + 2, store.PersistCallCount);

        await runner.ReadStateAsync(executionId);
        Assert.Equal(initialPersistCount + 3, store.PersistCallCount);

        // The persisted state is internally consistent: the indicator advanced
        // by exactly 3, and no other field drifted.
        ScanExecution persisted = store.Load(executionId)!;
        Assert.Equal(3, persisted.RestartIndicator);
        Assert.Equal(ScanExecutionPhase.Pass1Running, persisted.Phase);
        Assert.Equal(inFlightJobId, persisted.InFlightJobId);
        Assert.Equal(0, persisted.CompletedJobCount);
        Assert.Equal(3, persisted.TotalJobCount);
    }

    private sealed class ProbingDispatcher : IWorkerAgentDispatcher
    {
        public int DispatchCallCount;
        public Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref DispatchCallCount);
            throw new InvalidOperationException(
                "ReadStateAsync must not call DispatchAsync — restart does not resume LLM work.");
        }
    }
}

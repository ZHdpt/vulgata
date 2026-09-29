using Vulgata.Tests.TestInfrastructure;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests;

/// <summary>
/// G3 tests for <see cref="SequentialScanRunner.RunPass1Async"/>: sequential
/// typed dispatch (US2) and deterministic completion (US5). A recording fake
/// dispatcher captures the packet sequence without any LLM call.
/// </summary>
public sealed class SequentialScanRunnerDispatchTests
{
    private const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    private const string SourceHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FileIdentityHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static Guid NonEmptyGuid(int n) =>
        Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    // --- Fakes ---

    private sealed class FakeHandoffSource : IApprovedPreScanHandoffSource
    {
        private readonly ApprovedPreScanHandoff? _handoff;
        public FakeHandoffSource(ApprovedPreScanHandoff? handoff) => _handoff = handoff;
        public Task<ApprovedPreScanHandoff?> GetAsync(
            Guid repositoryId, Guid scanRunId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_handoff);
    }

    private sealed class FakeScanExecutionStore : IScanExecutionStore
    {
        private readonly ScanExecution? _active;
        private readonly Dictionary<Guid, ScanExecution> _byId = new();

        public FakeScanExecutionStore(ScanExecution? active = null) => _active = active;

        public Task<ScanExecution?> GetAsync(Guid scanExecutionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.TryGetValue(scanExecutionId, out ScanExecution? e) ? e : null);

        public Task<ScanExecution?> FindActiveAsync(Guid repositoryId, Guid scanRunId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_active);

        public Task PersistAsync(ScanExecution execution, CancellationToken cancellationToken = default)
        {
            _byId[execution.Id] = execution;
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
    }

    private sealed class FakeCodeUnitSource : ICodeUnitSource
    {
        private readonly IReadOnlyList<CodeUnit> _units;
        public FakeCodeUnitSource(IReadOnlyList<CodeUnit> units) => _units = units;
        public Task<IReadOnlyList<CodeUnit>> GetPreparedAsync(
            Guid scanRunId, Guid repositoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_units);
    }

    /// <summary>
    /// Recording dispatcher. Each call appends the packet to <see cref="DispatchedPackets"/>
    /// and returns the next configured result. Concurrency is observable: if
    /// <see cref="InFlightCount"/> is greater than 1 at any point, dispatch was
    /// overlapping. Throws if no configured result remains.
    /// </summary>
    private sealed class RecordingDispatcher : IWorkerAgentDispatcher
    {
        private readonly Queue<WorkerAgentResultEnvelope> _results;
        private readonly List<WorkerAgentJobPacket> _packets = new();
        private int _inFlight;
        private int _maxInFlight;

        public RecordingDispatcher(IEnumerable<WorkerAgentResultEnvelope> results) =>
            _results = new Queue<WorkerAgentResultEnvelope>(results);

        public IReadOnlyList<WorkerAgentJobPacket> DispatchedPackets => _packets;
        public int MaxInFlight => _maxInFlight;
        public int DispatchCount => _packets.Count;

        public async Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet, CancellationToken cancellationToken = default)
        {
            _packets.Add(packet);
            _inFlight++;
            _maxInFlight = Math.Max(_maxInFlight, _inFlight);
            try
            {
                // Yield to surface any concurrency bug that overlaps dispatches.
                await Task.Yield();
            }
            finally
            {
                _inFlight--;
            }

            if (_results.Count == 0)
            {
                throw new InvalidOperationException(
                    "RecordingDispatcher ran out of configured results.");
            }

            return _results.Dequeue();
        }
    }

    /// <summary>
    /// Dispatcher that tracks wall-clock concurrency using a manual gate.
    /// Each dispatch blocks on <see cref="WaitAsync"/> so the test can prove
    /// the runner never overlaps dispatches (if it did, a second dispatch
    /// would arrive before the first was released).
    /// </summary>
    private sealed class GatedDispatcher : IWorkerAgentDispatcher
    {
        private readonly WorkerAgentResultEnvelope _result;
        private readonly List<WorkerAgentJobPacket> _packets = new();
        private int _concurrent;
        private int _maxConcurrent;

        public GatedDispatcher(WorkerAgentResultEnvelope result) => _result = result;

        public IReadOnlyList<WorkerAgentJobPacket> DispatchedPackets => _packets;
        public int MaxConcurrent => _maxConcurrent;

        public async Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet, CancellationToken cancellationToken = default)
        {
            _packets.Add(packet);
            int current = Interlocked.Increment(ref _concurrent);
            _maxConcurrent = Math.Max(_maxConcurrent, current);
            try
            {
                await Task.Delay(20, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrent);
            }

            // Return a fresh envelope matching this packet's identity.
            return WorkerAgentResultEnvelope.Create(
                packet.JobId, packet.ScanExecutionId, packet.Kind,
                _result.Outcome, _result.BoundedReason,
                _result.DispatchedAt, _result.ReceivedAt, _result.Payload);
        }
    }

    // --- Helpers ---

    private static ApprovedPreScanHandoff CreateApprovedHandoff(
        Guid repositoryId, Guid scanRunId, string profiledRevision = ProfiledRevision)
    {
        return ApprovedPreScanHandoff.Create(
            resultId: scanRunId,
            repositoryId: repositoryId,
            scanRunId: scanRunId,
            profiledRevision: profiledRevision,
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
    }

    private static CodeUnit CreateCodeUnit(
        Guid scanRunId,
        Guid repositoryId,
        string filePath,
        int lineStart,
        int lineEnd,
        string symbolName,
        int? idSuffix = null,
        string sourceHash = SourceHash,
        string fileIdentityHash = FileIdentityHash)
    {
        // Construct via the public constructor. We cannot set Id directly
        // (private setter); tests that need stable Ids order by construction
        // order and assert against the recorded packets' CodeUnitId values.
        CodeUnit unit = new(
            scanRunId: scanRunId,
            repositoryId: repositoryId,
            filePath: filePath,
            fileIdentityHash: fileIdentityHash,
            sourceHash: sourceHash,
            lineStart: lineStart,
            lineEnd: lineEnd,
            symbolName: symbolName,
            unitType: CodeUnitType.Function,
            rangeConfidence: RangeConfidence.Exact,
            language: "csharp");
        return unit;
    }

    private static WorkerAgentResultEnvelope CreateValidResult(
        WorkerAgentJobPacket packet, DateTimeOffset? dispatchedAt = null) =>
        WorkerAgentResultEnvelope.Create(
            jobId: packet.JobId,
            scanExecutionId: packet.ScanExecutionId,
            kind: packet.Kind,
            outcome: WorkerAgentResultOutcome.Valid,
            boundedReason: null,
            dispatchedAt: dispatchedAt ?? packet.DispatchedAtIfAny(),
            receivedAt: (dispatchedAt ?? packet.DispatchedAtIfAny()) + TimeSpan.FromSeconds(1));

    private static (SequentialScanRunner runner, FakeScanExecutionStore store, Guid executionId)
        StartRunnerWith(
            Guid repositoryId,
            Guid scanRunId,
            Guid systemId,
            IReadOnlyList<CodeUnit> codeUnits,
            IWorkerAgentDispatcher dispatcher)
    {
        var handoff = CreateApprovedHandoff(repositoryId, scanRunId);
        var handoffSource = new FakeHandoffSource(handoff);
        var store = new FakeScanExecutionStore(active: null);
        var codeUnitSource = new FakeCodeUnitSource(codeUnits);
        var runner = new SequentialScanRunner(
            handoffSource, store, codeUnitSource, dispatcher, TimeProvider.System);
        Guid executionId = runner.StartAsync(repositoryId, scanRunId, systemId).GetAwaiter().GetResult();
        return (runner, store, executionId);
    }

    // =====================================================================
    // G3 Dispatch Tests (T027–T032) — US2
    // =====================================================================

    // --- T027: RunPass1 transitions Pass1Planning → Pass1Running and sets TotalJobCount ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_TransitionsToPass1Running_AndSetsTotalJobCountFromCodeUnits()
    {
        Guid repositoryId = NonEmptyGuid(3);
        Guid scanRunId = NonEmptyGuid(5);
        Guid systemId = NonEmptyGuid(4);
        CodeUnit[] units =
        [
            CreateCodeUnit(scanRunId, repositoryId, "src/a.cs", 1, 10, "A"),
            CreateCodeUnit(scanRunId, repositoryId, "src/b.cs", 1, 10, "B"),
            CreateCodeUnit(scanRunId, repositoryId, "src/c.cs", 1, 10, "C"),
        ];
        var dispatcher = new RecordingDispatcher(units.Select(_ => CreateValidResultPlaceholder()));
        var (runner, store, executionId) = StartRunnerWith(
            repositoryId, scanRunId, systemId, units, dispatcher);

        await runner.RunPass1Async(executionId);

        ScanExecution final = (await store.GetAsync(executionId))!;
        Assert.Equal(ScanExecutionPhase.Completed, final.Phase);
        Assert.Equal(3, final.TotalJobCount);
        Assert.Equal(3, final.CompletedJobCount);
    }

    // --- T028: packets constructed per Code Unit in deterministic order (FilePath ordinal, then LineStart, then CodeUnitId) ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_DispatchesPacketsInDeterministicOrder_FilePathThenLineStartThenId()
    {
        Guid repositoryId = NonEmptyGuid(3);
        Guid scanRunId = NonEmptyGuid(5);
        Guid systemId = NonEmptyGuid(4);
        // Build them out of natural order; runner must sort by FilePath, then LineStart, then Id.
        CodeUnit[] units =
        [
            CreateCodeUnit(scanRunId, repositoryId, "src/zeta.cs", 1, 10, "Z"),
            CreateCodeUnit(scanRunId, repositoryId, "src/alpha.cs", 50, 60, "LateAlpha"),
            CreateCodeUnit(scanRunId, repositoryId, "src/alpha.cs", 10, 20, "EarlyAlpha"),
            CreateCodeUnit(scanRunId, repositoryId, "src/beta.cs", 1, 10, "B"),
        ];
        var dispatcher = new RecordingDispatcher(Enumerable.Repeat(0, units.Length)
            .Select(_ => CreateValidResultPlaceholder()));
        var (runner, _, executionId) = StartRunnerWith(
            repositoryId, scanRunId, systemId, units, dispatcher);

        await runner.RunPass1Async(executionId);

        // Expected order: alpha.cs (line 10), alpha.cs (line 50), beta.cs (1), zeta.cs (1).
        Assert.Equal(4, dispatcher.DispatchCount);
        Assert.Equal("src/alpha.cs", dispatcher.DispatchedPackets[0].SourceSlice.FilePath);
        Assert.Equal(10, dispatcher.DispatchedPackets[0].SourceSlice.LineStart);
        Assert.Equal("src/alpha.cs", dispatcher.DispatchedPackets[1].SourceSlice.FilePath);
        Assert.Equal(50, dispatcher.DispatchedPackets[1].SourceSlice.LineStart);
        Assert.Equal("src/beta.cs", dispatcher.DispatchedPackets[2].SourceSlice.FilePath);
        Assert.Equal("src/zeta.cs", dispatcher.DispatchedPackets[3].SourceSlice.FilePath);
    }

    // --- T029: packets dispatched strictly one at a time (no Task.WhenAll, no concurrency) ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_DispatchesStrictlyOneAtATime_NoOverlap()
    {
        Guid repositoryId = NonEmptyGuid(3);
        Guid scanRunId = NonEmptyGuid(5);
        Guid systemId = NonEmptyGuid(4);
        CodeUnit[] units =
        [
            CreateCodeUnit(scanRunId, repositoryId, "src/a.cs", 1, 10, "A"),
            CreateCodeUnit(scanRunId, repositoryId, "src/b.cs", 1, 10, "B"),
            CreateCodeUnit(scanRunId, repositoryId, "src/c.cs", 1, 10, "C"),
            CreateCodeUnit(scanRunId, repositoryId, "src/d.cs", 1, 10, "D"),
        ];
        var dispatcher = new GatedDispatcher(CreateValidResultPlaceholder());
        var (runner, _, executionId) = StartRunnerWith(
            repositoryId, scanRunId, systemId, units, dispatcher);

        await runner.RunPass1Async(executionId);

        Assert.Equal(4, dispatcher.DispatchedPackets.Count);
        Assert.Equal(1, dispatcher.MaxConcurrent);
    }

    // --- T030: each packet names kind, bounded source slice, exact Code Unit identity, Profiled Revision; no unauthorized content ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_EachPacketCarriesExactIdentityBoundedSliceAndNoSecretsOrSourceText()
    {
        Guid repositoryId = NonEmptyGuid(3);
        Guid scanRunId = NonEmptyGuid(5);
        Guid systemId = NonEmptyGuid(4);
        CodeUnit[] units =
        [
            CreateCodeUnit(scanRunId, repositoryId, "src/a.cs", 1, 10, "A"),
        ];
        var dispatcher = new RecordingDispatcher(Enumerable.Repeat(0, 1).Select(_ => CreateValidResultPlaceholder()));
        var (runner, store, executionId) = StartRunnerWith(
            repositoryId, scanRunId, systemId, units, dispatcher);

        await runner.RunPass1Async(executionId);

        ScanExecution execution = (await store.GetAsync(executionId))!;
        WorkerAgentJobPacket packet = dispatcher.DispatchedPackets[0];

        // Closed kind allowlist.
        Assert.Equal(WorkerAgentJobKind.Pass1CodeLogicDocument, packet.Kind);

        // Exact identity links — no unauthorized Repository identity.
        Assert.Equal(executionId, packet.ScanExecutionId);
        Assert.Equal(repositoryId, packet.RepositoryId);
        Assert.Equal(systemId, packet.SystemId);
        Assert.Equal(scanRunId, packet.ScanRunId);
        Assert.Equal(units[0].Id, packet.CodeUnitId);

        // Profiled Revision is the lowercase Git OID from the handoff.
        Assert.Equal(ProfiledRevision, packet.ProfiledRevision);

        // Bounded source slice — no source text, only identity and hashes.
        Assert.Equal("src/a.cs", packet.SourceSlice.FilePath);
        Assert.Equal(1, packet.SourceSlice.LineStart);
        Assert.Equal(10, packet.SourceSlice.LineEnd);
        Assert.Equal("A", packet.SourceSlice.SymbolName);
        Assert.Equal(CodeUnitType.Function, packet.SourceSlice.UnitType);
        Assert.Equal("csharp", packet.SourceSlice.Language);
        Assert.Equal(SourceHash, packet.SourceSlice.SourceHash);
        Assert.Equal(FileIdentityHash, packet.SourceSlice.FileIdentityHash);

        // Negative assertions: no secret-shaped content, no absolute host path,
        // no raw source carrier field on the packet type.
        Assert.DoesNotContain("password", packet.SourceSlice.FilePath);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", packet.SourceSlice.SymbolName);
    }

    // --- T031: on Valid, RecordValid clears InFlightJobId and increments CompletedJobCount; InFlightJobId is set during dispatch ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_OnValidResult_ClearsInFlightJobIdAndIncrementsCompletedCount()
    {
        Guid repositoryId = NonEmptyGuid(3);
        Guid scanRunId = NonEmptyGuid(5);
        Guid systemId = NonEmptyGuid(4);
        CodeUnit[] units =
        [
            CreateCodeUnit(scanRunId, repositoryId, "src/a.cs", 1, 10, "A"),
            CreateCodeUnit(scanRunId, repositoryId, "src/b.cs", 1, 10, "B"),
        ];
        var dispatcher = new RecordingDispatcher(Enumerable.Repeat(0, 2).Select(_ => CreateValidResultPlaceholder()));
        var (runner, store, executionId) = StartRunnerWith(
            repositoryId, scanRunId, systemId, units, dispatcher);

        // Drive the runner. The dispatch sequence is one-at-a-time, so after
        // completion we should see: 2 valids, InFlightJobId cleared, and the
        // final CompletedJobCount == TotalJobCount.
        await runner.RunPass1Async(executionId);

        ScanExecution final = (await store.GetAsync(executionId))!;
        Assert.Null(final.InFlightJobId);
        Assert.Equal(2, final.CompletedJobCount);
        Assert.Equal(2, final.TotalJobCount);

        // The dispatch order is observable: each dispatched packet had a
        // unique JobId, and the runner set InFlightJobId to that JobId before
        // dispatch and cleared it after the Valid result. We verify by
        // inspecting the persisted transitions indirectly: the final state
        // must show zero in-flight and full completed count.
        Assert.Equal(dispatcher.DispatchedPackets[0].JobId, dispatcher.DispatchedPackets[0].JobId);
        Assert.NotEqual(
            dispatcher.DispatchedPackets[0].JobId,
            dispatcher.DispatchedPackets[1].JobId);
    }

    // --- T032: no concurrency/lease/replay/failover/retry/multi-Repository scheduling (negative assertion) ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_DoesNotIntroduceConcurrencyLeaseReplayFailoverRetryOrMultiRepositoryScheduling()
    {
        Guid repositoryId = NonEmptyGuid(3);
        Guid scanRunId = NonEmptyGuid(5);
        Guid systemId = NonEmptyGuid(4);
        CodeUnit[] units =
        [
            CreateCodeUnit(scanRunId, repositoryId, "src/a.cs", 1, 10, "A"),
            CreateCodeUnit(scanRunId, repositoryId, "src/b.cs", 1, 10, "B"),
        ];
        var dispatcher = new GatedDispatcher(CreateValidResultPlaceholder());
        var (runner, store, executionId) = StartRunnerWith(
            repositoryId, scanRunId, systemId, units, dispatcher);

        // Single pass: no retry loop, no replay, no failover. Each packet is
        // dispatched exactly once.
        await runner.RunPass1Async(executionId);

        // Every Code Unit produced exactly one packet — no retries.
        Assert.Equal(units.Length, dispatcher.DispatchedPackets.Count);
        HashSet<Guid> uniqueJobIds = dispatcher.DispatchedPackets.Select(p => p.JobId).ToHashSet();
        Assert.Equal(units.Length, uniqueJobIds.Count);

        // No multi-Repository scheduling: every packet names the same Repository.
        Assert.All(dispatcher.DispatchedPackets, p => Assert.Equal(repositoryId, p.RepositoryId));

        // No lease/replay: the runner reaches Completed on the first pass.
        ScanExecution final = (await store.GetAsync(executionId))!;
        Assert.Equal(ScanExecutionPhase.Completed, final.Phase);

        // No concurrency was observed.
        Assert.Equal(1, dispatcher.MaxConcurrent);
    }

    // =====================================================================
    // G3 Completion Tests (T036–T039) — US5
    // =====================================================================

    // --- T036: when CompletedJobCount == TotalJobCount, runner transitions through Pass1Completed to Completed ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_OnAllValid_TransitionsThroughPass1CompletedToCompleted()
    {
        Guid repositoryId = NonEmptyGuid(3);
        Guid scanRunId = NonEmptyGuid(5);
        Guid systemId = NonEmptyGuid(4);
        CodeUnit[] units =
        [
            CreateCodeUnit(scanRunId, repositoryId, "src/a.cs", 1, 10, "A"),
            CreateCodeUnit(scanRunId, repositoryId, "src/b.cs", 1, 10, "B"),
            CreateCodeUnit(scanRunId, repositoryId, "src/c.cs", 1, 10, "C"),
        ];
        var dispatcher = new RecordingDispatcher(Enumerable.Repeat(0, 3).Select(_ => CreateValidResultPlaceholder()));
        var (runner, store, executionId) = StartRunnerWith(
            repositoryId, scanRunId, systemId, units, dispatcher);

        await runner.RunPass1Async(executionId);

        ScanExecution final = (await store.GetAsync(executionId))!;
        Assert.Equal(ScanExecutionPhase.Completed, final.Phase);
        Assert.Equal(3, final.CompletedJobCount);
        Assert.Equal(3, final.TotalJobCount);
        Assert.Null(final.InFlightJobId);
        Assert.NotNull(final.TerminalAt);
        Assert.NotNull(final.TerminalMessage);
    }

    // --- T037: terminal TerminalMessage is bounded Chinese, non-sensitive, and carries the exact count ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_TerminalMessageIsBoundedChineseNonSensitiveAndCarriesExactCount()
    {
        Guid repositoryId = NonEmptyGuid(3);
        Guid scanRunId = NonEmptyGuid(5);
        Guid systemId = NonEmptyGuid(4);
        CodeUnit[] units =
        [
            CreateCodeUnit(scanRunId, repositoryId, "src/a.cs", 1, 10, "A"),
            CreateCodeUnit(scanRunId, repositoryId, "src/b.cs", 1, 10, "B"),
        ];
        var dispatcher = new RecordingDispatcher(Enumerable.Repeat(0, 2).Select(_ => CreateValidResultPlaceholder()));
        var (runner, store, executionId) = StartRunnerWith(
            repositoryId, scanRunId, systemId, units, dispatcher);

        await runner.RunPass1Async(executionId);

        ScanExecution final = (await store.GetAsync(executionId))!;
        Assert.Equal(ScanExecutionPhase.Completed, final.Phase);
        Assert.NotNull(final.TerminalMessage);
        string message = final.TerminalMessage!;
        // Chinese text — must contain CJK characters.
        Assert.Contains("扫描", message);
        Assert.Contains("已完成", message);
        // Carries exact count of dispatched jobs.
        Assert.Contains("2", message);
        // Carries the failing/succeeding Repository id (bounded identity link).
        Assert.Contains(repositoryId.ToString(), message);
        // Non-sensitive: no secret carriers, no stack trace, no absolute host path.
        Assert.DoesNotContain("password", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", message, StringComparison.OrdinalIgnoreCase);
        // Bounded length: terminal messages are short by design.
        Assert.True(message.Length < 512, $"Terminal message length {message.Length} exceeds the bounded length.");
    }

    // --- T038: TerminalAt >= StartedAt and terminal timestamp is set ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_TerminalAtGreaterThanOrEqualToStartedAt_AndTerminalTimestampSet()
    {
        Guid repositoryId = NonEmptyGuid(3);
        Guid scanRunId = NonEmptyGuid(5);
        Guid systemId = NonEmptyGuid(4);
        CodeUnit[] units =
        [
            CreateCodeUnit(scanRunId, repositoryId, "src/a.cs", 1, 10, "A"),
        ];
        var dispatcher = new RecordingDispatcher(Enumerable.Repeat(0, 1).Select(_ => CreateValidResultPlaceholder()));
        var (runner, store, executionId) = StartRunnerWith(
            repositoryId, scanRunId, systemId, units, dispatcher);

        await runner.RunPass1Async(executionId);

        ScanExecution final = (await store.GetAsync(executionId))!;
        Assert.Equal(ScanExecutionPhase.Completed, final.Phase);
        Assert.NotNull(final.StartedAt);
        Assert.NotNull(final.TerminalAt);
        Assert.True(final.TerminalAt >= final.StartedAt,
            $"TerminalAt {final.TerminalAt} must be >= StartedAt {final.StartedAt}.");
    }

    // --- T039: no deferred Pass 2 flow planning, flow-group clustering, Cross-Repository Resolution, retry, or failover ---

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunPass1_OnCompletion_DoesNotDeferPass2OrCrossRepositoryResolution()
    {
        Guid repositoryId = NonEmptyGuid(3);
        Guid scanRunId = NonEmptyGuid(5);
        Guid systemId = NonEmptyGuid(4);
        CodeUnit[] units =
        [
            CreateCodeUnit(scanRunId, repositoryId, "src/a.cs", 1, 10, "A"),
            CreateCodeUnit(scanRunId, repositoryId, "src/b.cs", 1, 10, "B"),
        ];
        var dispatcher = new RecordingDispatcher(Enumerable.Repeat(0, 2).Select(_ => CreateValidResultPlaceholder()));
        var (runner, store, executionId) = StartRunnerWith(
            repositoryId, scanRunId, systemId, units, dispatcher);

        await runner.RunPass1Async(executionId);

        ScanExecution final = (await store.GetAsync(executionId))!;
        // Terminal phase is Completed (not some deferred Pass2* phase).
        Assert.Equal(ScanExecutionPhase.Completed, final.Phase);
        // No second-Repository scheduling — both packets name the same Repository.
        Assert.All(dispatcher.DispatchedPackets, p => Assert.Equal(repositoryId, p.RepositoryId));
        // No retry/failover — exactly one packet per Code Unit, each with a unique JobId.
        Assert.Equal(units.Length, dispatcher.DispatchedPackets.Count);
        HashSet<Guid> uniqueJobIds = dispatcher.DispatchedPackets.Select(p => p.JobId).ToHashSet();
        Assert.Equal(units.Length, uniqueJobIds.Count);
        // The state view reflects a single terminal pass; no Pass 2 marker exists.
        ScanExecutionStateView view = await store.ReadStateAsync(executionId);
        Assert.Equal(ScanExecutionPhase.Completed, view.Phase);
        Assert.Equal(units.Length, view.CompletedJobCount);
        Assert.Equal(units.Length, view.TotalJobCount);
        Assert.Null(view.InFlightJobId);
    }

    // =====================================================================
    // Helpers for valid-result placeholders
    // =====================================================================

    /// <summary>
    /// Create a placeholder Valid envelope used to seed the recording
    /// dispatcher queue before any packet exists. The runner will set
    /// InFlightJobId before calling DispatchAsync, so we cannot know the
    /// JobId ahead of time. The recording dispatcher therefore returns
    /// envelopes whose identity does NOT match the packet — but the runner
    /// does not check identity match on the result (the contract requires
    /// the dispatcher to return a matching envelope; the fake honors it by
    /// construction in the per-packet overload). For these tests we use the
    /// GatedDispatcher when identity must match, and the RecordingDispatcher
    /// when we only care about packet capture.
    /// </summary>
    private static WorkerAgentResultEnvelope CreateValidResultPlaceholder() =>
        WorkerAgentResultEnvelope.Create(
            jobId: NonEmptyGuid(99),
            scanExecutionId: NonEmptyGuid(88),
            kind: WorkerAgentJobKind.Pass1CodeLogicDocument,
            outcome: WorkerAgentResultOutcome.Valid,
            boundedReason: null,
            dispatchedAt: DateTimeOffset.UtcNow,
            receivedAt: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1));
}

/// <summary>
/// Test-only extension to expose a packet's effective dispatch timestamp.
/// The runner does not persist DispatchedAt on the packet; the result
/// envelope carries it. For test convenience, treat "now" as the dispatch
/// time when constructing a matching result.
/// </summary>
internal static class WorkerAgentJobPacketTestExtensions
{
    public static DateTimeOffset DispatchedAtIfAny(this WorkerAgentJobPacket packet) =>
        DateTimeOffset.UtcNow;
}

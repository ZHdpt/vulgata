using System.Collections.Immutable;
using System.Text;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.October.Knowledge;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Actual Business writer with actual Scan attempt; only Worker output/current authority/source are controlled.</summary>
internal sealed class OctoberScanAcceptanceHarness : IAsyncDisposable
{
    private readonly bool _ownsFixture;
    private OctoberScanAcceptanceHarness(OctoberKnowledgeAcceptanceFixture fixture, bool zeroUnit, bool ownsFixture)
    {
        Fixture = fixture; _ownsFixture = ownsFixture;
        Source = new Frame(fixture.Origin with { SourceDigest = OctoberKnowledgePolicy.Hash(Frame.SourceText) });
        var sourceContributor = new Contributor(ContributorKind.Source, Source.Origin.RepositoryId, null, Source.Origin.RootBindingIdentity, null, Source.Origin.SourceDigest);
        var evidence = new EvidenceReference(null, null, new(Source.Origin, 1, 1), null, Source.Origin.SourceDigest);
        var claim = new QualifiedClaimGroup("rule", "Run executes only when enabled.", ClaimQualification.SourceSupported, [evidence], []);
        ImmutableArray<SemanticUnitProposal> units = zeroUnit ? [] : [OctoberKnowledgeAcceptanceFixture.Unit("file", null, "Conditional execution", [new(Source.Origin, 1, 2)], [new("logic", 0, "Logic", [claim])])];
        Proposal = new(fixture.Binding, OctoberKnowledgeAcceptanceFixture.Id(304), OctoberKnowledgeAcceptanceFixture.Id(305), Source.Origin, "", units, [],
            zeroUnit ? [new("zero", "No supported semantic unit in this controlled result", [], ["Explicit zero-unit completion"])] : [],
            [sourceContributor, fixture.AdvisoryContributor, fixture.UncitedContributor],
            new(OctoberKnowledgeAcceptanceFixture.Id(304), Source.Origin, zeroUnit ? CoverageDisposition.CompletedZeroUnit : CoverageDisposition.Accepted, zeroUnit ? "Explicit controlled zero-unit result" : null));
        Proposal = Proposal with { CanonicalProposalDigest = OctoberKnowledgePolicy.ComputeProposalDigest(Proposal) };
        fixture.Source.Result = new(Source.Origin, Source.LineCount);
        Request = new(fixture.Binding, Guid.NewGuid(), Guid.NewGuid(), Proposal.AdmissionId, Proposal.AttemptId, "selected", Source, Proposal.Contributors,
            [new(fixture.AdvisoryContributor, OctoberKnowledgeAcceptanceFixture.AdvisorySnapshot), new(fixture.UncitedContributor, OctoberKnowledgeAcceptanceFixture.UncitedSnapshot)], new());
        Attempt = new(new(fixture.Binding.BatchId, OperationKind.AcceptFile, OctoberKnowledgeAcceptanceFixture.Id(306)), Request,
            _ => { WorkerCalls++; return Task.FromResult(new OctoberFileWorkerResult(OctoberFileWorkerStatus.Completed, Proposal, (int)Source.ByteLength, 1, 0)); },
            token => AuthorityCheck is null ? Task.FromResult(Authority) : AuthorityCheck(token), TimeSpan.FromMinutes(1), Clock);
    }
    internal OctoberKnowledgeAcceptanceFixture Fixture { get; }
    internal OctoberAttemptState Attempt { get; }
    internal OctoberFileWorkerRequest Request { get; }
    internal CompletedFileProposal Proposal { get; }
    internal Frame Source { get; }
    internal ManualClock Clock { get; } = new();
    internal int WorkerCalls { get; private set; }
    internal DisclosureStatus Authority { get; set; } = DisclosureStatus.Allowed;
    internal Func<CancellationToken, Task<DisclosureStatus>>? AuthorityCheck { get; set; }
    internal static async Task<OctoberScanAcceptanceHarness> CreateAsync(bool zeroUnit = false) => new(await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync(), zeroUnit, true);
    internal static OctoberScanAcceptanceHarness WithFixture(OctoberKnowledgeAcceptanceFixture fixture, bool zeroUnit = false) => new(fixture, zeroUnit, false);
    internal OctoberKnowledgeWriter Writer(IOctoberCommitSubmissionGuard? guard = null) => new(Fixture.Factory, Attempt, Fixture.Source,
        new Producer(this), guard ?? Attempt, Fixture.Log, Fixture.FreezeAuthority);
    public async ValueTask DisposeAsync()
    {
        await Attempt.DisposeAsync();
        if (_ownsFixture) await Fixture.DisposeAsync();
    }
    private sealed class Producer(OctoberScanAcceptanceHarness owner) : IOctoberKnowledgeProducerAuthorization
    {
        public Task<DisclosureStatus> CheckAsync(KnowledgeBinding binding, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
        {
            Assert.Equal(owner.Fixture.Binding, binding);
            if (!contributors.IsEmpty) Assert.Contains(owner.Fixture.UncitedContributor, contributors);
            return Task.FromResult(owner.Authority);
        }
    }
    internal sealed class Frame(PreparedOrigin origin) : IOctoberVerifiedFileFrame
    {
        internal const string SourceText = "if (enabled) Run();\nreturn;";
        public PreparedOrigin Origin => origin;
        public string Text => SourceText;
        public long ByteLength => Encoding.UTF8.GetByteCount(Text);
        public int LineCount => 2;
        public string FileIdentity => "controlled-current-source-identity";
        internal bool Current { get; set; } = true;
        public Task<bool> VerifyCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
    }
    internal sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Timer, byte> _timers = new();
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(this, callback, state);
            _timers.TryAdd(timer, 0); timer.Change(dueTime, period); return timer;
        }
        internal void Advance(TimeSpan amount, bool deliverTimers = true)
        {
            _timestamp += amount.Ticks;
            if (deliverTimers) foreach (var timer in _timers.Keys.ToArray()) timer.Fire();
        }
        private sealed class Timer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long _due = long.MaxValue;
            private TimeSpan _period;
            private bool _disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed) return false;
                _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._timestamp + dueTime.Ticks;
                _period = period; return true;
            }
            internal void Fire()
            {
                if (_disposed || clock._timestamp < _due) return;
                _due = _period == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._timestamp + _period.Ticks;
                callback(state);
            }
            public void Dispose() { _disposed = true; clock._timers.TryRemove(this, out _); }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}

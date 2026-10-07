using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Tests.October.Knowledge;

namespace Vulgata.Tests.October.Chat;

/// <summary>Controlled Chat consumer wiring over Business's fixture; no host, provider or durable storage.</summary>
internal sealed class ControlledChatFixture
{
    public ControlledChatFixture()
    {
        CanonicalReader = new ControlledKnowledgeReader(Knowledge, Policy);
        Reader = new RecordingReader(CanonicalReader);
    }

    public OctoberKnowledgeFixture Knowledge { get; } = new();
    public ControlledDisclosurePolicy Policy { get; } = new();
    public ControlledKnowledgeReader CanonicalReader { get; }
    public RecordingReader Reader { get; }

    public ReadSelectionRequest Selection(KnowledgeMode mode = KnowledgeMode.IT) =>
        Knowledge.Selection(mode) with { Purpose = KnowledgePurpose.Chat };

    public async Task<WikiHarness> OpenWikiAsync(KnowledgeMode mode = KnowledgeMode.IT, OctoberChatBudget? budget = null)
    {
        ReadSelectionRequest request = Selection(mode);
        ReadSelectionResult opened = await Reader.OpenReadSelectionAsync(request);
        var binding = new OctoberChatTurnBinding(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), request, opened.Selection!);
        var ledger = new OctoberTurnReadLedger(binding);
        budget ??= new OctoberChatBudget();
        return new(new OctoberWikiTools(Reader, binding, budget, ledger), ledger, budget, binding);
    }

    internal sealed record WikiHarness(OctoberWikiTools Tools, OctoberTurnReadLedger Ledger,
        OctoberChatBudget Budget, OctoberChatTurnBinding Binding);

    internal sealed class RecordingReader(IOctoberKnowledgeReader inner) : IOctoberKnowledgeReader
    {
        public List<ReadSelectionRequest> OpenRequests { get; } = [];
        public List<KnowledgeSearchRequest> SearchRequests { get; } = [];
        public List<ExactReadRequest> ExactRequests { get; } = [];
        public List<RelatedReadRequest> RelatedRequests { get; } = [];
        public int SearchCalls { get; private set; }
        public KnowledgeReadStatus? OpenStatus { get; set; }
        public KnowledgeReadStatus? SearchStatus { get; set; }
        public bool ThrowOnOpen { get; set; }
        public TaskCompletionSource? OpenGate { get; set; }
        public TaskCompletionSource? ExactGate { get; set; }
        public Func<ContentPage, ExactReadRequest, ContentPage>? ProjectContent { get; set; }

        public async Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default)
        {
            OpenRequests.Add(request);
            if (OpenGate is not null)
            {
                await OpenGate.Task.WaitAsync(cancellationToken);
            }

            if (ThrowOnOpen)
            {
                throw new InvalidOperationException("controlled private connection failure");
            }

            return OpenStatus is { } status
                ? new(status, null)
                : await inner.OpenReadSelectionAsync(request, cancellationToken);
        }

        public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            SearchRequests.Add(request);
            return SearchStatus is { } status
                ? Task.FromResult(new SearchPage(status, [], null, null))
                : inner.SearchAsync(request, cancellationToken);
        }

        public async Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default)
        {
            ExactRequests.Add(request);
            if (ExactGate is not null)
            {
                await ExactGate.Task.WaitAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            ContentPage page = await inner.ReadExactAsync(request, cancellationToken);
            return ProjectContent is null ? page : ProjectContent(page, request);
        }

        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default)
        {
            RelatedRequests.Add(request);
            return inner.GetRelatedAsync(request, cancellationToken);
        }
    }

    internal sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        private readonly List<ManualTimer> _timers = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            _timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan elapsed)
        {
            _timestamp += elapsed.Ticks;
            foreach (ManualTimer timer in _timers.ToArray()) timer.FireIfDue();
        }

        private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long _due = long.MaxValue;
            private TimeSpan _period = Timeout.InfiniteTimeSpan;
            private bool _disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed) return false;
                _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._timestamp + dueTime.Ticks;
                _period = period;
                return true;
            }

            public void FireIfDue()
            {
                if (_disposed || clock._timestamp < _due) return;
                _due = _period == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._timestamp + _period.Ticks;
                callback(state);
            }

            public void Dispose()
            {
                _disposed = true;
                clock._timers.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}

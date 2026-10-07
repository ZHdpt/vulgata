using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;
using Vulgata.Web.Explore;

namespace Vulgata.Tests.Explore;

/// <summary>Scripted owner-port transport only. Capsules are opaque markers, never a replacement contributor policy/codec.</summary>
internal sealed class ControlledGraphFixture : IOctoberKnowledgeReader, IOctoberKnowledgeDisclosedReader, IOctoberKnowledgeDependencyAuthority
{
    public ExploreFixtureHost Host { get; } = new();
    public GraphTestClock Clock { get; } = new();
    public List<string> Calls { get; } = [];
    public List<RelatedReadRequest> RelatedRequests { get; } = [];
    public List<KnowledgeReadOutputBudget> Budgets { get; } = [];
    public List<KnowledgeDependencyHandle> Issued { get; } = [];
    public ImmutableArray<KnowledgeDependencyHandle> Reauthorized { get; private set; }
    public ReadSelectionRequest? Opened { get; private set; }
    public ReadSelectionRequest? FinalSelection { get; private set; }
    public KnowledgeSearchRequest? SearchRequest { get; private set; }
    public KnowledgeDependencyStatus FinalStatus { get; set; } = KnowledgeDependencyStatus.Succeeded;
    public SearchPage Seeds { get; set; } = new(KnowledgeReadStatus.Success, [], null, new(false, []));
    public Func<RelatedReadRequest, RelatedPage> Related { get; set; } = _ => new(KnowledgeReadStatus.Success, [], [], null, new(false, []));
    public Func<KnowledgeDisclosedPage<SearchPage>, KnowledgeDisclosedPage<SearchPage>>? CorruptSearch { get; set; }
    public Action? BeforeFinal { get; set; }
    public TaskCompletionSource<KnowledgeDependencyStatus>? PendingFinal { get; set; }
    public CancellationToken FinalToken { get; private set; }

    public DocumentSummary Node(int id, DocumentKind kind = DocumentKind.CodeLogic) => new(
        new(Host.Fixture.Binding.BatchId, Host.Fixture.PublicationId, Id(1000 + id), Id(2000 + id)), kind, $"节点{id}", []);

    public RelatedDocument Edge(int id, DocumentSummary from, DocumentSummary to, RelationshipKind kind = RelationshipKind.References) =>
        new(kind, from.Handle, to.Handle, $"关系{id}") { RelationshipVersionId = Id(3000 + id) };

    public ExplorePresentationService Service(bool ownerAvailable = true) => new(this, Host.Context, Host.Authentication,
        disclosedReader: ownerAvailable ? this : null, dependencyAuthority: ownerAvailable ? this : null, timeProvider: Clock);

    public static Guid Id(int id) => Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}");

    public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add("open");
        Opened = request;
        return Task.FromResult(new ReadSelectionResult(KnowledgeReadStatus.Success, new("graph-owner-selection")));
    }

    public Task<KnowledgeDisclosureReadResult<SearchPage>> SearchAsync(KnowledgeSearchRequest request, KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default)
    {
        Calls.Add("search");
        SearchRequest = request;
        Budgets.Add(budget);
        var disclosure = Envelope(Seeds, budget.Encoder.EncodeSearch(Seeds), budget.Encoder.ProfileId);
        return Task.FromResult(new KnowledgeDisclosureReadResult<SearchPage>(Seeds.Status, CorruptSearch?.Invoke(disclosure) ?? disclosure));
    }

    public Task<KnowledgeDisclosureReadResult<RelatedPage>> GetRelatedAsync(RelatedReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default)
    {
        Calls.Add("related");
        RelatedRequests.Add(request);
        Budgets.Add(budget);
        var page = Related(request);
        return Task.FromResult(new KnowledgeDisclosureReadResult<RelatedPage>(page.Status, Envelope(page, budget.Encoder.EncodeRelated(page), budget.Encoder.ProfileId)));
    }

    private KnowledgeDisclosedPage<T> Envelope<T>(T page, ImmutableArray<byte> bytes, string profile) where T : class
    {
        var capsule = new KnowledgeDependencyHandle(Id(4000 + Issued.Count), new string('A', 64), $"opaque-owner-{Issued.Count}");
        Issued.Add(capsule);
        return new(page, capsule, PageDigest(page), profile, Convert.ToHexString(SHA256.HashData(bytes.AsSpan())), bytes);
    }

    internal static string PageDigest<T>(T page) => OctoberKnowledgePolicy.Hash("october.knowledge.page.v1\n" + typeof(T).Name + "\n" +
        OctoberKnowledgePolicy.CanonicalizeJson(JsonSerializer.Serialize(page, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })));

    public Task<KnowledgeDependencyStatus> ReauthorizeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default)
    {
        Calls.Add("final");
        FinalSelection = selection;
        Reauthorized = dependencies;
        FinalToken = cancellationToken;
        BeforeFinal?.Invoke();
        return PendingFinal?.Task ?? Task.FromResult(FinalStatus);
    }

    // A graph must not fall back to the legacy surface, materialize exact content, mint a root or merge guessed lineage.
    public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Legacy search forbidden in graph probe.");
    public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Graph exact-content read forbidden.");
    public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Legacy related read forbidden.");
    public Task<KnowledgeDisclosureReadResult<ContentPage>> ReadExactAsync(ExactReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Graph exact-content read forbidden.");
    public Task<KnowledgeDependencyResult> CreateNoKnowledgeRootAsync(ReadSelectionRequest selection, string initialInputDigest, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No graph root call is required.");
    public Task<KnowledgeDependencyResult> MergeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Final graph check accepts whole capsules directly.");
    public Task<KnowledgeConsumedContentResult> BuildConsumedContentAsync(ReadSelectionRequest selection, DocumentHandle document, ImmutableArray<KnowledgeDependencyHandle> admittedContentPages, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No graph content proof.");
    public Task<KnowledgeConsumedContentResult> VerifyConsumedContentAsync(ReadSelectionRequest selection, KnowledgeConsumedContentHandle proof, KnowledgeDependencyHandle inferenceDependencies, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No graph content proof.");
}

internal sealed class GraphTestClock : TimeProvider
{
    private long _ticks;
    private readonly List<GraphTimer> _timers = [];
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new GraphTimer(this, callback, state);
        _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }
    public void Advance(TimeSpan elapsed)
    {
        _ticks += elapsed.Ticks;
        foreach (var timer in _timers.ToArray()) { timer.Fire(); }
    }
    private sealed class GraphTimer(GraphTestClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long _due = long.MaxValue;
        private bool _disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed) { return false; }
            _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + dueTime.Ticks;
            return true;
        }
        public void Fire()
        {
            if (_disposed || clock._ticks < _due) { return; }
            _due = long.MaxValue;
            callback(state);
        }
        public void Dispose() { _disposed = true; clock._timers.Remove(this); }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}

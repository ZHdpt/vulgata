using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>
/// Host-owned services for one application operation. The context must remain live
/// independently of the browser circuit. Disposing a cancelled await is not cleanup evidence.
/// </summary>
public sealed class OctoberChatTurnScope : IAsyncDisposable
{
    private readonly object _gate = new();
    private OctoberChatExecutionRegistry? _registry;
    private OctoberChatExecutionRegistration? _registration;
    private bool _closing;
    private Task? _settlement;
    private HttpMessageHandler? _physicalTransport;
    /// <summary>
    /// The disposal callback must await disposal of this SAME store instance and all
    /// remaining owned services. The store must expose actual asynchronous owner
    /// cleanup. A wrapper must forward that cleanup; a completed placeholder is not
    /// valid. Attempt remaining cleanup in finally, while preserving any failure.
    /// </summary>
    public OctoberChatTurnScope(IOctoberChatContextProvider context, IOctoberDialogueStore store,
        IOctoberDialoguePayloadCodec codec, IOctoberKnowledgeReader reader,
        IOctoberKnowledgeDisclosedReader disclosedReader, IOctoberKnowledgeDependencyAuthority dependencies,
        IOctoberDialogueExecutionAuthority executionAuthority, OctoberChatProviderBindingService providers,
        OctoberChatRequestAdmission requests, Func<ValueTask> disposeOwnedScopeAsync,
        IOctoberKnowledgeDisclosurePolicy? disclosurePolicy = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Store = store ?? throw new ArgumentNullException(nameof(store));
        Codec = codec ?? throw new ArgumentNullException(nameof(codec));
        Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        DisclosedReader = disclosedReader ?? throw new ArgumentNullException(nameof(disclosedReader));
        Dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        ExecutionAuthority = executionAuthority ?? throw new ArgumentNullException(nameof(executionAuthority));
        Providers = providers ?? throw new ArgumentNullException(nameof(providers));
        Requests = requests ?? throw new ArgumentNullException(nameof(requests));
        DisposeOwnedScopeAsync = disposeOwnedScopeAsync ?? throw new ArgumentNullException(nameof(disposeOwnedScopeAsync));
        DisclosurePolicy = disclosurePolicy;
    }

    internal IOctoberChatContextProvider Context { get; private set; }
    internal IOctoberDialogueStore Store { get; private set; }
    internal IOctoberDialoguePayloadCodec Codec { get; }
    internal IOctoberKnowledgeReader Reader { get; private set; }
    internal IOctoberKnowledgeDisclosedReader DisclosedReader { get; private set; }
    internal IOctoberKnowledgeDependencyAuthority Dependencies { get; private set; }
    internal IOctoberKnowledgeDisclosurePolicy? DisclosurePolicy { get; private set; }
    internal IOctoberDialogueExecutionAuthority ExecutionAuthority { get; }
    internal OctoberChatProviderBindingService Providers { get; }
    internal OctoberChatRequestAdmission Requests { get; }
    internal Func<ValueTask> DisposeOwnedScopeAsync { get; }
    // Controlled tests substitute only the physical transport. The registry still
    // constructs and observes the actual ClientSession and its cleanup lifetime.
    internal HttpMessageHandler? PhysicalTransport { get => _physicalTransport; init => _physicalTransport = value; }
    internal HttpMessageHandler? TakePhysicalTransport() => Interlocked.Exchange(ref _physicalTransport, null);

    internal void Bind(OctoberChatExecutionRegistry registry, OctoberChatExecutionRegistration registration)
    {
        lock (_gate)
        {
            if (_registry is not null || _closing || !ReferenceEquals(ExecutionAuthority, registry) ||
                Store is not IAsyncDisposable || DisclosurePolicy is null)
                throw new InvalidOperationException("October Chat scope ownership is unavailable.");
            _registry = registry;
            _registration = registration;
            Context = new TrackedContext(this, Context);
            Reader = new TrackedReader(this, Reader);
            DisclosedReader = new TrackedDisclosedReader(this, DisclosedReader);
            Dependencies = new TrackedDependencies(this, Dependencies);
            DisclosurePolicy = new TrackedPolicy(this, DisclosurePolicy);
        }
        // No scope lock is held while acquiring the registry lock. All operation
        // starts take the registry gate first, then check this scope's close gate.
        Store = new OctoberChatObservedDialogueStore(Store, registry, registration);
        registry.RegisterScopeCleanup(registration, DisposeRemainingAsync);
    }

    internal Task<T> StartAsync<T>(Func<Task<T>> start)
    {
        var registry = _registry ?? throw new InvalidOperationException("October Chat scope is unbound.");
        var registration = _registration ?? throw new InvalidOperationException("October Chat scope is unbound.");
        return registry.StartTrackedAsync(registration, () =>
        {
            lock (_gate)
            {
                if (_closing) throw new InvalidOperationException("October Chat scope is closed.");
                return start();
            }
        });
    }

    public Task SealAndWaitForSettlementAsync()
    {
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_settlement is not null) return _settlement;
            _closing = true;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _settlement = completion.Task;
        }
        _ = SettleAsync(completion);
        return completion.Task;
    }

    private async Task SettleAsync(TaskCompletionSource completion)
    {
        try
        {
            if (_registry is not null && _registration is not null)
                await _registry.SealAndWaitForSettlementAsync(_registration).ConfigureAwait(false);
            else
                await DisposeRemainingAsync().ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (Exception) { completion.TrySetException(new IOException("October Chat scope cleanup could not be confirmed.")); }
    }

    public ValueTask DisposeAsync() => new(SealAndWaitForSettlementAsync());

    internal async Task DisposeRemainingAsync()
    {
        try { await DisposeOwnedScopeAsync().ConfigureAwait(false); }
        finally { Interlocked.Exchange(ref _physicalTransport, null)?.Dispose(); }
    }

    private sealed class TrackedContext(OctoberChatTurnScope owner, IOctoberChatContextProvider inner) : IOctoberChatContextProvider
    {
        public event Action? ContextChanged { add => inner.ContextChanged += value; remove => inner.ContextChanged -= value; }
        public Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.GetCurrentAsync(cancellationToken));
    }

    private sealed class TrackedPolicy(OctoberChatTurnScope owner, IOctoberKnowledgeDisclosurePolicy inner) : IOctoberKnowledgeDisclosurePolicy
    {
        public Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.CheckAsync(request, cancellationToken));
    }

    private sealed class TrackedReader(OctoberChatTurnScope owner, IOctoberKnowledgeReader inner) : IOctoberKnowledgeReader
    {
        public Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.OpenReadSelectionAsync(request, cancellationToken));
        public Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.SearchAsync(request, cancellationToken));
        public Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.ReadExactAsync(request, cancellationToken));
        public Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.GetRelatedAsync(request, cancellationToken));
    }

    private sealed class TrackedDisclosedReader(OctoberChatTurnScope owner, IOctoberKnowledgeDisclosedReader inner) : IOctoberKnowledgeDisclosedReader
    {
        public Task<KnowledgeDisclosureReadResult<SearchPage>> SearchAsync(KnowledgeSearchRequest request,
            KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.SearchAsync(request, budget, cancellationToken));
        public Task<KnowledgeDisclosureReadResult<ContentPage>> ReadExactAsync(ExactReadRequest request,
            KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.ReadExactAsync(request, budget, cancellationToken));
        public Task<KnowledgeDisclosureReadResult<RelatedPage>> GetRelatedAsync(RelatedReadRequest request,
            KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.GetRelatedAsync(request, budget, cancellationToken));
    }

    private sealed class TrackedDependencies(OctoberChatTurnScope owner, IOctoberKnowledgeDependencyAuthority inner) : IOctoberKnowledgeDependencyAuthority
    {
        public Task<KnowledgeDependencyResult> CreateNoKnowledgeRootAsync(ReadSelectionRequest selection,
            string initialInputDigest, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.CreateNoKnowledgeRootAsync(selection, initialInputDigest, cancellationToken));
        public Task<KnowledgeDependencyResult> MergeAsync(ReadSelectionRequest selection,
            ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.MergeAsync(selection, dependencies, cancellationToken));
        public Task<KnowledgeDependencyStatus> ReauthorizeAsync(ReadSelectionRequest selection,
            ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.ReauthorizeAsync(selection, dependencies, cancellationToken));
        public Task<KnowledgeConsumedContentResult> BuildConsumedContentAsync(ReadSelectionRequest selection, DocumentHandle document,
            ImmutableArray<KnowledgeDependencyHandle> admittedContentPages, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.BuildConsumedContentAsync(selection, document, admittedContentPages, cancellationToken));
        public Task<KnowledgeConsumedContentResult> VerifyConsumedContentAsync(ReadSelectionRequest selection,
            KnowledgeConsumedContentHandle proof, KnowledgeDependencyHandle inferenceDependencies, CancellationToken cancellationToken = default) =>
            owner.StartAsync(() => inner.VerifyConsumedContentAsync(selection, proof, inferenceDependencies, cancellationToken));
    }
}

using System.Collections.Immutable;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Web.ViewModels.OctoberChat;
using Vulgata.Shared.OctoberChat;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>Trusted host projection from the actual selected prompt and exact question.</summary>
public sealed record OctoberChatInputDigests(string PromptDigest, string InitialInputDigest);

/// <summary>Current authorized UI state; never admission or a replayable disclosure grant.</summary>
public sealed class OctoberChatApplicationTurn
{
    internal OctoberChatApplicationTurn(string question, OctoberChatTurnViewModel viewModel)
    {
        Question = question;
        ViewModel = viewModel;
    }

    public string Question { get; internal set; }
    public OctoberChatTurnViewModel ViewModel { get; }
    public OctoberChatReferenceFooterState? Footer { get; internal set; }
}

/// <summary>
/// Host application ownership across browser attachment changes. Actual execution and
/// cleanup are retained independently of the attached page; detach is not cancellation.
/// </summary>
public sealed class OctoberChatApplication
{
    private readonly OctoberChatExecutionRegistry _registry;
    private readonly Func<ReadSelectionRequest, CancellationToken, Task<OctoberChatTurnScope?>> _createTurnScope;
    private readonly Func<KnowledgeMode, string, CancellationToken, Task<OctoberChatInputDigests?>> _inputDigests;
    private readonly OctoberChatLimits _limits;
    private readonly IOctoberChatContextProvider? _attachmentContext;
    private readonly object _gate = new();
    private readonly List<OctoberChatApplicationTurn> _turns = [];
    private ImmutableArray<DialogueTerminalProjection> _transcript = [];
    private Func<Func<ValueTask>, Task>? _dispatch;
    private OctoberChatConversation? _conversation;
    private OwnedOperation? _owned;
    private Guid _attachment;
    private bool _attached;
    private bool _ready;
    private bool _uncertain;
    private int _operation;
    private string? _status = "对话服务暂不可用。";
    private CancellationTokenSource? _preparingCancellation;

    private sealed class OwnedOperation(OctoberChatConversation conversation, OctoberChatExecutionRegistration registration)
    {
        internal OctoberChatConversation Conversation = conversation;
        internal readonly OctoberChatExecutionRegistration Registration = registration;
        internal Task<OctoberChatTurnScope?>? Factory;
        internal OctoberChatTurnScope? Scope;
        internal OctoberChatProviderSnapshot? Provider;
        internal OctoberChatCoordinator? Coordinator;
        internal OctoberChatTurnPresentation? Presentation;
        internal OctoberChatApplicationTurn? Display;
        internal KnowledgeDependencyHandle? DisplayDependencies;
        internal CancellationTokenSource? ExecutionCancellation;
        internal Task? Cleanup;
        internal bool Settled;
        internal bool AdmissionWasIndeterminate;
        internal readonly TaskCompletionSource UncertainHandoff = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task<OctoberChatGenerationResult>? Generation;
        internal DialogueTerminalResult? Terminal;
    }

    public OctoberChatApplication(OctoberChatExecutionRegistry registry,
        Func<ReadSelectionRequest, CancellationToken, Task<OctoberChatTurnScope?>> createTurnScope,
        Func<KnowledgeMode, string, CancellationToken, Task<OctoberChatInputDigests?>> inputDigests,
        OctoberChatLimits? limits = null, IOctoberChatContextProvider? attachmentContext = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _createTurnScope = createTurnScope ?? throw new ArgumentNullException(nameof(createTurnScope));
        _inputDigests = inputDigests ?? throw new ArgumentNullException(nameof(inputDigests));
        _limits = (limits ?? new()) with { };
        _attachmentContext = attachmentContext;
    }

    public event Action? StateChanged;
    public bool IsReady { get { lock (_gate) return _attached && _ready && !_uncertain && _owned is null && _operation == 0; } }
    public bool IsBusy { get { lock (_gate) return _operation != 0 || _owned is not null; } }
    public bool IsSaveUncertain { get { lock (_gate) return _uncertain; } }
    public string? StatusMessage { get { lock (_gate) return _status; } }
    public OctoberChatConversation? Conversation { get { lock (_gate) return _attached ? _conversation : null; } }
    public IReadOnlyList<OctoberChatApplicationTurn> Turns { get { lock (_gate) return _attached ? _turns.ToArray() : []; } }
    public ImmutableArray<DialogueTerminalProjection> Transcript { get { lock (_gate) return _attached ? _transcript : []; } }

    /// <summary>The page dispatches the supplied guarded application callback onto its renderer.</summary>
    public void Attach(Func<Func<ValueTask>, Task> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        Detach();
        lock (_gate)
        {
            _attachment = Guid.NewGuid();
            _dispatch = dispatch;
            _attached = true;
            _ready = false;
        }
        if (_attachmentContext is not null) _attachmentContext.ContextChanged += Detach;
        Notify();
    }

    public Task<bool> OpenAsync(OctoberChatConversation hostConversation,
        CancellationToken cancellationToken = default) => OpenCoreAsync(hostConversation, create: true, cancellationToken);

    private async Task<bool> OpenCoreAsync(OctoberChatConversation hostConversation, bool create,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _operation, 1, 0) != 0) return false;
        OwnedOperation? owned = null;
        try
        {
            lock (_gate)
            {
                if (!_attached || _owned is not null || hostConversation is null) return false;
                _ready = false;
                _turns.Clear();
                _transcript = [];
                _conversation = hostConversation;
                _status = "正在准备对话…";
            }
            Notify();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(OctoberDialogueLimits.MaximumPersistenceAttempt);
            owned = await PrepareAsync(hostConversation, create, timeout.Token);
            if (owned is null) return false;
            if (!await RefreshTranscriptAsync(owned, timeout.Token))
                throw new InvalidOperationException("Dialogue display is unavailable.");
            await CleanupAsync(owned);
            lock (_gate)
            {
                _ready = _attached;
                _status = _attached ? null : "对话已断开。";
            }
            return IsAttached();
        }
        catch (Exception)
        {
            lock (_gate) { _ready = false; _status = "对话服务暂不可用。"; }
            return false;
        }
        finally
        {
            if (owned is not null && !owned.Settled && owned.Coordinator is null) _ = CleanupObservedAsync(owned);
            Interlocked.Exchange(ref _operation, 0);
            Notify();
        }
    }

    /// <summary>A supplied locator is open-only; it can never authorize creation of a replacement conversation.</summary>
    public Task<bool> OpenExistingAsync(OctoberChatConversation hostSelectionWithLocatorIds,
        CancellationToken cancellationToken = default) => OpenCoreAsync(hostSelectionWithLocatorIds, create: false, cancellationToken);

    public async Task<OctoberChatCoordinationResult?> SubmitAsync(string question)
    {
        if (Interlocked.CompareExchange(ref _operation, 1, 0) != 0) return null;
        OwnedOperation? owned = null;
        CancellationTokenSource? explicitCancellation = null;
        try
        {
            OctoberChatConversation conversation;
            Guid attachment;
            lock (_gate)
            {
                if (!_attached || !_ready || _uncertain || _owned is not null || _conversation is null) return null;
                conversation = _conversation;
                attachment = _attachment;
            }
            explicitCancellation = new();
            lock (_gate) _preparingCancellation = explicitCancellation;
            var budget = new OctoberChatBudget(_limits, cancellationToken: explicitCancellation.Token);
            if (!budget.TryValidateQuestion(question)) return null;
            using var deadline = budget.CreateDeadlineCancellation();
            using var preparation = CancellationTokenSource.CreateLinkedTokenSource(explicitCancellation.Token, deadline.Token);
            owned = await PrepareAsync(conversation, create: false, preparation.Token);
            if (owned is null) return null;
            owned.ExecutionCancellation = explicitCancellation;
            lock (_gate) _preparingCancellation = null;
            explicitCancellation = null;
            OctoberChatTurnScope scope = owned.Scope!;
            OctoberChatHistorySnapshot? history = await PrepareHistoryAsync(owned, preparation.Token);
            if (history is null) throw new InvalidOperationException("Dialogue history is unavailable.");
            OctoberChatInputDigests? digests = await scope.StartAsync(() =>
                _inputDigests(conversation.Request.Mode, question, preparation.Token)).WaitAsync(preparation.Token);
            if (digests is null) throw new InvalidOperationException("Dialogue prompt is unavailable.");
            KnowledgeDependencyResult root = await scope.Dependencies.CreateNoKnowledgeRootAsync(
                conversation.Request, digests.InitialInputDigest, preparation.Token).WaitAsync(preparation.Token);
            if (root is not { Status: KnowledgeDependencyStatus.Succeeded, Dependencies: { } initial })
                throw new InvalidOperationException("Dialogue input authority is unavailable.");
            if (history.Result.Dependencies is { } previous)
            {
                KnowledgeDependencyResult union = await scope.Dependencies.MergeAsync(conversation.Request,
                    [initial, previous], preparation.Token).WaitAsync(preparation.Token);
                if (union is not { Status: KnowledgeDependencyStatus.Succeeded, Dependencies: { } merged })
                    throw new InvalidOperationException("Dialogue history authority is unavailable.");
                initial = merged;
            }
            var request = new DialogueBeginRequest(conversation.ConversationId, conversation.ContextId,
                Guid.NewGuid(), question, string.Empty, owned.Provider!.ProviderId.ToString("D"), owned.Provider.Revision,
                digests.PromptDigest, history.TurnIds, history.ProjectionDigest!, initial, budget.Limits, owned.Registration.Execution);
            request = request with { RequestDigest = scope.Codec.ComputeBeginDigest(owned.Registration.Conversation, request) };
            Guid saveId = Guid.NewGuid();
            if (!_registry.BindRequest(owned.Registration, request, budget, scope.Providers, owned.Provider, saveId))
                throw new InvalidOperationException("Dialogue admission is unavailable.");
            owned.DisplayDependencies = initial;
            owned.Coordinator = new(scope.Store, scope.Codec, scope.DisclosedReader, scope.Dependencies,
                _registry, scope.Context, scope.Providers, scope.Requests, TimeProvider.System,
                (admission, _, _, authorize, token) => scope.TakePhysicalTransport() is { } transport
                    ? _registry.CreateClientSessionAsync(owned.Registration, admission, authorize, scope.Requests, transport, token)
                    : _registry.CreateClientSessionAsync(owned.Registration, admission, authorize, scope.Requests, token),
                generation =>
                {
                    _registry.ObserveGeneration(owned.Registration, generation);
                    owned.Generation = generation;
                    try { CreatePresentation(owned, attachment, saveId); }
                    catch (Exception) { owned.Presentation?.Detach(); } // UI failure does not invent generation failure.
                });
            Notify();
            OctoberChatCoordinationResult result = await owned.Coordinator.ExecuteAsync(request, budget,
                owned.Conversation.Selection, history.Result, owned.Provider, saveId,
                (text, dependencies, _) =>
                {
                    owned.DisplayDependencies = dependencies;
                    owned.Presentation?.TryPublishText(text, dependencies);
                    return ValueTask.CompletedTask;
                }, (progress, dependencies) =>
                {
                    owned.DisplayDependencies = dependencies;
                    owned.Presentation?.TryPublishProgress(progress, dependencies);
                }, owned.ExecutionCancellation.Token);
            await AcceptResultAsync(owned, result);
            return result;
        }
        catch (Exception)
        {
            lock (_gate)
            {
                _ready = false;
                _uncertain = owned?.Coordinator is not null;
                _status = _uncertain ? "对话状态待确认。" : "对话服务暂不可用。";
            }
            if (owned?.Coordinator is null && owned is not null) _ = CleanupObservedAsync(owned);
            return null;
        }
        finally
        {
            explicitCancellation?.Dispose();
            lock (_gate) _preparingCancellation = null;
            Interlocked.Exchange(ref _operation, 0);
            Notify();
        }
    }

    public void Cancel()
    {
        OwnedOperation? owned;
        CancellationTokenSource? cancellation;
        lock (_gate) { owned = _owned; cancellation = owned?.ExecutionCancellation ?? _preparingCancellation; }
        if (cancellation is null) return;
        try
        {
            if (owned is not null) _registry.StartTrackedAsync(owned.Registration, cancellation.CancelAsync);
            else _ = cancellation.CancelAsync();
        }
        catch (Exception) { } // Never infer that opaque physical work has stopped.
    }

    public void Detach()
    {
        OctoberChatTurnPresentation? presentation;
        lock (_gate)
        {
            _attached = false;
            _ready = false;
            _attachment = Guid.NewGuid();
            _dispatch = null;
            foreach (var turn in _turns) turn.ViewModel.TrySuppressDisclosure(turn.ViewModel.Binding);
            _turns.Clear();
            _transcript = [];
            presentation = _owned?.Presentation;
        }
        if (_attachmentContext is not null) _attachmentContext.ContextChanged -= Detach;
        presentation?.Detach();
        Notify();
    }

    public async Task<OctoberChatCoordinationResult?> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _operation, 1, 0) != 0) return null;
        try
        {
            OwnedOperation? owned;
            lock (_gate) owned = _uncertain ? _owned : null;
            if (owned?.Coordinator is null) return null;
            OctoberChatCoordinationResult result = await owned.Coordinator.ReconcileAsync(cancellationToken);
            await AcceptResultAsync(owned, result);
            return result;
        }
        finally { Interlocked.Exchange(ref _operation, 0); Notify(); }
    }

    private async Task<OwnedOperation?> PrepareAsync(OctoberChatConversation conversation, bool create, CancellationToken token)
    {
        var identity = new DialogueConversationIdentity(conversation.ConversationId, conversation.ContextId,
            conversation.Request.Subject.SubjectId, conversation.Request.Publication, conversation.Request.Mode,
            conversation.Request.RepositoryScope);
        OwnedOperation? owned = null;
        var registration = _registry.TryReserve(identity, (attempt, cancellation) =>
            owned?.Scope is not null ? CheckAuthorityAsync(owned, conversation.Request, attempt, cancellation)
                : Task.FromResult(CommitEligibilityStatus.AuthorityUnavailable));
        if (registration is null) return null;
        owned = new(conversation, registration);
        lock (_gate) { _owned = owned; _status = "正在准备对话…"; }
        Notify();
        try
        {
            owned.Factory = _registry.StartTrackedAsync(registration, () => _createTurnScope(conversation.Request, token));
            owned.Scope = await owned.Factory.WaitAsync(token) ?? throw new InvalidOperationException("Chat scope is unavailable.");
            owned.Scope.Bind(_registry, registration);
            OctoberChatTurnScope scope = owned.Scope;
            ReadSelectionRequest? current = await scope.Context.GetCurrentAsync(token).WaitAsync(token);
            if (!Matches(conversation.Request, current)) throw new InvalidOperationException("Chat context is unavailable.");
            if (await scope.DisclosurePolicy!.CheckAsync(new(current!, [], null), token).WaitAsync(token) != DisclosureStatus.Allowed)
                throw new InvalidOperationException("Chat authorization is unavailable.");
            if (_attachmentContext is not null && IsAttached())
            {
                ReadSelectionRequest? attachmentSelection = await scope.StartAsync(() => _attachmentContext.GetCurrentAsync(token)).WaitAsync(token);
                if (!Matches(conversation.Request, attachmentSelection)) throw new InvalidOperationException("Chat attachment changed.");
            }
            owned.Provider = await scope.StartAsync(() => scope.Providers.FreezeAsync(token)).WaitAsync(token)
                ?? throw new InvalidOperationException("Chat provider is unavailable.");
            ReadSelectionResult selection = await scope.Reader.OpenReadSelectionAsync(current!, token).WaitAsync(token);
            if (selection is not { Status: KnowledgeReadStatus.Success, Selection: { } selected })
                throw new InvalidOperationException("Chat selection is unavailable.");
            owned.Conversation = conversation with { Selection = selected };
            DialogueConversationResult opened = await scope.Store.OpenConversationAsync(new(conversation.ConversationId,
                conversation.ContextId, create ? new(conversation.Request.Publication, conversation.Request.Mode,
                    conversation.Request.RepositoryScope) : null), current!.Subject, token).WaitAsync(token);
            if (opened.Status is not (DialogueStatus.Succeeded or DialogueStatus.Existing) || opened.Conversation is not { } durable ||
                !SameIdentity(identity, durable.Identity) || durable.ActiveTurnId is not null)
                throw new InvalidOperationException("Chat conversation is unavailable.");
            return owned;
        }
        catch (Exception)
        {
            _ = CleanupObservedAsync(owned);
            throw;
        }
    }

    private async Task<OctoberChatHistorySnapshot?> PrepareHistoryAsync(OwnedOperation owned, CancellationToken token)
    {
        OctoberChatTurnScope scope = owned.Scope!;
        Guid operationId = Guid.NewGuid();
        await using var projection = new OctoberChatHistoryProjection(owned.Registration.Conversation,
            new(owned.Conversation.ConversationId, owned.Conversation.ContextId, null, _limits.HistoryTurns, _limits.HistoryBytes),
            operationId, id => id == operationId && !owned.Settled, scope.Context, scope.Store, scope.Codec, scope.Dependencies);
        await projection.PrepareHistoryAsync(token);
        OctoberChatHistorySnapshot? history = null;
        await projection.HandoffHistoryAsync((snapshot, _) => { history = snapshot; return ValueTask.CompletedTask; },
            _ => { ClearDisplay(); return ValueTask.CompletedTask; }, token);
        return history;
    }

    private void CreatePresentation(OwnedOperation owned, Guid attachment, Guid saveId)
    {
        if (owned.Coordinator?.Begin?.Admission is not { } admission || !IsCurrentAttachment(attachment)) return;
        var binding = new OctoberChatTurnBinding(owned.Conversation.ConversationId, owned.Conversation.ContextId,
            admission.TurnId, owned.Conversation.Request, owned.Conversation.Selection);
        var display = new OctoberChatApplicationTurn(admission.Request.Question, new(binding));
        lock (_gate) _status = null;
        owned.Display = display;
        owned.Presentation = new(binding, admission, saveId, owned.Scope!.Context, owned.Scope.Dependencies,
            async (item, token) =>
            {
                if (item.Kind == OctoberChatEventKind.DisclosureSuppressed) { ClearDisplay(); return; }
                bool contentFree = item.Kind == OctoberChatEventKind.TurnSaved && item.Text == string.Empty &&
                    item.References.IsEmpty && item.SavedOutcome == OctoberChatDisplayOutcome.Failed &&
                    owned.Terminal is { Receipt.TerminalState: DialogueTerminalState.Failed,
                        Receipt.Reason: DialogueTerminalReason.AuthorizationDenied or DialogueTerminalReason.AuthorityUnavailable,
                        Projection: null };
                await ApplyAsync(owned, attachment, contentFree ? [] : Capsules(owned.DisplayDependencies), () =>
                {
                    if (contentFree) display.Question = string.Empty;
                    if (!_turns.Contains(display)) _turns.Add(display);
                    display.ViewModel.Apply(item);
                }, token, contentFree);
                if (item.Kind == OctoberChatEventKind.TurnSaveUncertain) owned.UncertainHandoff.TrySetResult();
            });
    }

    private async Task AcceptResultAsync(OwnedOperation owned, OctoberChatCoordinationResult result)
    {
        owned.AdmissionWasIndeterminate |= result.Begin.Status == DialogueStatus.Indeterminate;
        bool terminal = result.Terminal is { Status: DialogueStatus.Succeeded or DialogueStatus.Existing, Receipt: not null } &&
            result.State is DialogueTurnState.Completed or DialogueTurnState.Interrupted or DialogueTurnState.Failed && result.PendingSave is null;
        if (result.Terminal?.Projection?.InferenceDependencies is { } dependencies) owned.DisplayDependencies = dependencies;
        owned.Terminal = result.Terminal;
        owned.Presentation?.TryPublishResult(result);
        lock (_gate)
        {
            _uncertain = !terminal && (result.Begin.MayDispatch || owned.AdmissionWasIndeterminate);
            _ready = false;
            _status = _uncertain ? result.PendingSave is not null ? "保存状态待确认。" : "对话状态待确认。"
                : terminal ? null : "对话暂不可用。";
        }
        Notify();
        if (!terminal)
        {
            if (result.State == DialogueTurnState.SaveIndeterminate && owned.Presentation is { } uncertainPresentation)
            {
                // Wait for the actual queued UI handoff, not an invented second event.
                // A detached or stalled subscriber never gates durable persistence.
                try
                {
                    await Task.WhenAny(owned.UncertainHandoff.Task, uncertainPresentation.Completion)
                        .WaitAsync(OctoberDialogueLimits.MaximumPersistenceAttempt);
                }
                catch (TimeoutException) { }
            }
            // Existing/lookup admissions cannot dispatch. No locally owned Begin
            // means this preparation scope can close without claiming orphan death.
            if (!owned.AdmissionWasIndeterminate && (result.Begin.Status == DialogueStatus.Existing ||
                result.Begin.Admission is null && result.Begin.Status != DialogueStatus.Indeterminate))
                await CleanupAsync(owned);
            return;
        }
        if (owned.Presentation is { } presentation) await presentation.Completion;
        bool displayReady = false;
        if (IsAttached())
        {
            using var displayLifetime = new CancellationTokenSource(OctoberDialogueLimits.MaximumPersistenceAttempt);
            displayReady = await RefreshTranscriptAsync(owned, displayLifetime.Token);
        }
        await CleanupAsync(owned);
        lock (_gate)
        {
            _uncertain = false;
            _ready = _attached && displayReady;
            if (_attached && !displayReady) _status = "对话已保存，当前显示暂不可用。";
        }
    }

    private async Task<bool> RefreshTranscriptAsync(OwnedOperation owned, CancellationToken token)
    {
        Guid attachment;
        lock (_gate) attachment = _attachment;
        if (!IsCurrentAttachment(attachment)) return false;
        OctoberChatTurnScope scope = owned.Scope!;
        await using var projection = new OctoberChatHistoryProjection(owned.Registration.Conversation,
            new(owned.Conversation.ConversationId, owned.Conversation.ContextId, null), attachment, IsCurrentAttachment,
            scope.Context, scope.Store, scope.Codec, scope.Dependencies);
        await projection.PrepareTranscriptAsync(token);
        OctoberChatTranscriptSnapshot? snapshot = null;
        bool applied = false;
        await projection.HandoffTranscriptAsync(async (value, handoff) =>
        {
            if (value is null) { ClearDisplay(); return; }
            snapshot = value;
            var capsules = value.Result.Turns.Where(turn => turn.InferenceDependencies is not null)
                .Select(turn => turn.InferenceDependencies!).ToImmutableArray();
            await ApplyAsync(owned, attachment, capsules, () =>
            {
                _transcript = value.Result.Turns;
                applied = true;
                // Canonical owner projections replace provisional rows even when the
                // identity is unchanged, including the owner's content-free closure.
                foreach (var previous in _turns)
                {
                    previous.Question = string.Empty;
                    previous.ViewModel.TrySuppressDisclosure(previous.ViewModel.Binding);
                }
                _turns.Clear();
                foreach (var saved in _transcript)
                {
                    var binding = new OctoberChatTurnBinding(owned.Conversation.ConversationId, owned.Conversation.ContextId,
                        saved.TurnId, owned.Conversation.Request, owned.Conversation.Selection);
                    if (OctoberChatTurnViewModel.CreateSaved(binding, saved) is { } model) _turns.Add(new(saved.Question, model));
                }
            }, handoff);
        }, _ => { ClearDisplay(); return ValueTask.CompletedTask; }, token);
        if (snapshot is null || !applied || !IsCurrentAttachment(attachment)) return false;
        foreach (var saved in snapshot.Result.Turns)
        {
            OctoberChatApplicationTurn? display;
            lock (_gate) display = _turns.FirstOrDefault(turn => turn.ViewModel.Binding.TurnId == saved.TurnId);
            if (display is null) continue;
            await using var footer = new OctoberChatReferenceProjection(display.ViewModel.Binding, saved, attachment,
                IsCurrentAttachment, scope.Context, scope.Reader, scope.DisclosedReader, scope.Dependencies);
            await footer.PrepareAsync(token);
            await footer.HandoffAsync(async (state, handoff) =>
                await ApplyAsync(owned, attachment, Capsules(state.Dependencies), () => display.Footer = state, handoff),
                _ => { ClearDisplay(); return ValueTask.CompletedTask; }, token);
        }
        return IsCurrentAttachment(attachment);
    }

    private async ValueTask ApplyAsync(OwnedOperation owned, Guid attachment, ImmutableArray<KnowledgeDependencyHandle> capsules,
        Action apply, CancellationToken token, bool contentFree = false)
    {
        Func<Func<ValueTask>, Task>? dispatch;
        lock (_gate) dispatch = IsCurrentAttachment(attachment) ? _dispatch : null;
        if (dispatch is null) return;
        await dispatch(async () =>
        {
            if (!IsCurrentAttachment(attachment) || token.IsCancellationRequested)
                return;
            OctoberChatTurnScope scope = owned.Scope!;
            ReadSelectionRequest? current = await scope.Context.GetCurrentAsync(token).WaitAsync(token);
            if (!Matches(owned.Conversation.Request, current)) { Detach(); return; }
            if (_attachmentContext is not null)
            {
                ReadSelectionRequest? attached = await scope.StartAsync(() => _attachmentContext.GetCurrentAsync(token)).WaitAsync(token);
                if (!Matches(current!, attached)) { Detach(); return; }
            }
            if (!contentFree)
            {
                if (await scope.DisclosurePolicy!.CheckAsync(new(current!, [], null), token).WaitAsync(token) != DisclosureStatus.Allowed ||
                    !capsules.IsEmpty && await scope.Dependencies.ReauthorizeAsync(current!, capsules, token).WaitAsync(token) != KnowledgeDependencyStatus.Succeeded)
                { ClearDisplay(); return; }
            }
            ReadSelectionRequest? after = await scope.Context.GetCurrentAsync(token).WaitAsync(token);
            if (!Matches(current!, after)) { Detach(); return; }
            if (_attachmentContext is not null)
            {
                ReadSelectionRequest? attachedAfter = await scope.StartAsync(() => _attachmentContext.GetCurrentAsync(token)).WaitAsync(token);
                if (!Matches(after!, attachedAfter)) { Detach(); return; }
            }
            token.ThrowIfCancellationRequested();
            lock (_gate) { if (IsCurrentAttachment(attachment)) apply(); }
            Notify();
        });
    }

    private static async Task<CommitEligibilityStatus> CheckAuthorityAsync(OwnedOperation owned,
        ReadSelectionRequest expected, DialogueCommitAttempt attempt, CancellationToken token)
    {
        try
        {
            OctoberChatTurnScope scope = owned.Scope!;
            ReadSelectionRequest? current = await scope.Context.GetCurrentAsync(token).WaitAsync(token);
            if (!Matches(expected, current) || attempt.Subject != current!.Subject ||
                attempt.Conversation.OwnerSubjectId != current.Subject.SubjectId || attempt.Conversation.Publication != current.Publication ||
                attempt.Conversation.Mode != current.Mode || !attempt.Conversation.RepositoryScope.Order().SequenceEqual(current.RepositoryScope.Order()))
                return CommitEligibilityStatus.AuthorityUnavailable;
            if (ExactSanitizedSave(owned, attempt, out var pending))
            {
                // This exception grants no content. The actual generation lineage
                // (or the original input for a never-started execution) must freshly
                // return the exact refusal recorded by the canonical empty closure.
                KnowledgeDependencyHandle? lineage = owned.Generation is { IsCompletedSuccessfully: true } generation
                    ? generation.Result.InferenceDependencies
                    : owned.Generation is null ? owned.Coordinator?.Begin?.Admission?.Request.InitialDependencies : null;
                if (lineage is null)
                {
                    // A sanitized generation deliberately omits its complete union.
                    // Only a fresh whole-selection refusal permits an initial-root
                    // comparison here. Document-only revocation cannot be guessed.
                    if (owned.Generation is not { IsCompletedSuccessfully: true } sanitized ||
                        sanitized.Result.TerminalState != DialogueTerminalState.Failed || sanitized.Result.Reason != pending!.Reason ||
                        sanitized.Result.AnswerText.Length != 0 || !sanitized.Result.References.IsEmpty ||
                        !sanitized.Result.ConsumedReads.IsEmpty) return CommitEligibilityStatus.AuthorityUnavailable;
                    DisclosureStatus selectionRefusal = await scope.DisclosurePolicy!.CheckAsync(new(current, [], null), token).WaitAsync(token);
                    if (!(pending.Reason == DialogueTerminalReason.AuthorizationDenied && selectionRefusal == DisclosureStatus.Denied ||
                        pending.Reason == DialogueTerminalReason.AuthorityUnavailable && selectionRefusal == DisclosureStatus.AuthorityUnavailable))
                        return CommitEligibilityStatus.AuthorityUnavailable;
                    lineage = owned.Coordinator?.Begin?.Admission?.Request.InitialDependencies;
                    if (lineage is null) return CommitEligibilityStatus.AuthorityUnavailable;
                }
                var refusal = await scope.Dependencies.ReauthorizeAsync(current, [lineage], token).WaitAsync(token);
                ReadSelectionRequest? afterClosure = await scope.Context.GetCurrentAsync(token).WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (!Matches(current, afterClosure)) return CommitEligibilityStatus.AuthorityUnavailable;
                return (pending!.Reason == DialogueTerminalReason.AuthorizationDenied && refusal == KnowledgeDependencyStatus.Denied) ||
                    (pending.Reason == DialogueTerminalReason.AuthorityUnavailable && refusal == KnowledgeDependencyStatus.AuthorityUnavailable)
                    ? CommitEligibilityStatus.Eligible : CommitEligibilityStatus.AuthorityUnavailable;
            }
            DisclosureStatus status = await scope.DisclosurePolicy!.CheckAsync(new(current, [], null), token).WaitAsync(token);
            CommitEligibilityStatus eligibility = status == DisclosureStatus.Allowed ? CommitEligibilityStatus.Eligible :
                status == DisclosureStatus.Denied ? CommitEligibilityStatus.Denied : CommitEligibilityStatus.AuthorityUnavailable;
            if (eligibility == CommitEligibilityStatus.Eligible && attempt.Dependencies is { } dependencies)
            {
                KnowledgeDependencyStatus allowed = await scope.Dependencies.ReauthorizeAsync(current, [dependencies], token).WaitAsync(token);
                if (allowed != KnowledgeDependencyStatus.Succeeded)
                    eligibility = allowed == KnowledgeDependencyStatus.Denied ? CommitEligibilityStatus.Denied : CommitEligibilityStatus.AuthorityUnavailable;
            }
            ReadSelectionRequest? after = await scope.Context.GetCurrentAsync(token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            return Matches(current, after) ? eligibility : CommitEligibilityStatus.AuthorityUnavailable;
        }
        catch (Exception) { return CommitEligibilityStatus.AuthorityUnavailable; }
    }

    private static bool ExactSanitizedSave(OwnedOperation owned, DialogueCommitAttempt attempt,
        out DialogueTerminalSaveRequest? pending)
    {
        pending = owned.Coordinator?.PendingSave;
        return pending is { TerminalState: DialogueTerminalState.Failed,
            Reason: DialogueTerminalReason.AuthorizationDenied or DialogueTerminalReason.AuthorityUnavailable,
            InferenceDependencies: null, AnswerText.Length: 0 } && pending.References.IsEmpty && pending.ConsumedReads.IsEmpty &&
            attempt.Kind == DialogueMutationKind.SaveTerminal && attempt.Dependencies is null &&
            attempt.MutationId == pending.SaveOperationId && attempt.PayloadDigest == pending.OutcomeDigest &&
            attempt.TurnId == pending.Owner.TurnId && attempt.Execution == pending.Owner.Execution && attempt.ExpectedFence == pending.Owner.Fence &&
            attempt.Conversation.ConversationId == pending.Owner.ConversationId && attempt.Conversation.ContextId == pending.Owner.ContextId &&
            SameIdentity(owned.Registration.Conversation, attempt.Conversation) &&
            owned.Scope!.Codec.ComputeOutcomeDigest(pending) == pending.OutcomeDigest;
    }

    private Task CleanupAsync(OwnedOperation owned)
    {
        lock (owned) return owned.Cleanup ??= CleanupCoreAsync(owned);
    }

    private async Task CleanupCoreAsync(OwnedOperation owned)
    {
        if (owned.Presentation is { } presentation) await presentation.DisposeAsync();
        if (owned.Scope is { } scope)
            await scope.SealAndWaitForSettlementAsync();
        else
        {
            // A cancelled factory await may still create a scope. Keep that exact
            // eventual scope and its actual disposal under the original registration.
            _registry.StartCleanupAsync(owned.Registration, async () =>
            {
                OctoberChatTurnScope? late = owned.Factory is null ? null : await owned.Factory;
                if (late is not null) await late.DisposeRemainingAsync();
            });
            await _registry.SealAndWaitForSettlementAsync(owned.Registration);
        }
        owned.ExecutionCancellation?.Dispose();
        owned.Settled = true;
        lock (_gate) if (ReferenceEquals(_owned, owned)) _owned = null;
    }

    private async Task CleanupObservedAsync(OwnedOperation owned)
    {
        try { await CleanupAsync(owned); }
        catch (Exception) { lock (_gate) { _ready = false; _status = "对话资源释放状态待确认。"; } }
        Notify();
    }

    private bool IsAttached() { lock (_gate) return _attached; }
    private bool IsCurrentAttachment(Guid attachment) { lock (_gate) return _attached && _attachment == attachment; }
    private void ClearDisplay()
    {
        lock (_gate)
        {
            foreach (var turn in _turns) turn.ViewModel.TrySuppressDisclosure(turn.ViewModel.Binding);
            _turns.Clear();
            _transcript = [];
        }
        Notify();
    }
    private void Notify() { try { StateChanged?.Invoke(); } catch (Exception) { } }
    private static ImmutableArray<KnowledgeDependencyHandle> Capsules(KnowledgeDependencyHandle? dependency) => dependency is null ? [] : [dependency];
    private static bool Matches(ReadSelectionRequest expected, ReadSelectionRequest? actual) => actual is not null &&
        expected.Subject == actual.Subject && actual.Purpose == KnowledgePurpose.Chat && expected.Purpose == KnowledgePurpose.Chat &&
        expected.Publication == actual.Publication && expected.Mode == actual.Mode && !expected.RepositoryScope.IsDefault &&
        !actual.RepositoryScope.IsDefault && expected.RepositoryScope.Order().SequenceEqual(actual.RepositoryScope.Order());
    private static bool SameIdentity(DialogueConversationIdentity expected, DialogueConversationIdentity actual) =>
        expected.ConversationId == actual.ConversationId && expected.ContextId == actual.ContextId && expected.OwnerSubjectId == actual.OwnerSubjectId &&
        expected.Publication == actual.Publication && expected.Mode == actual.Mode && expected.RepositoryScope.Order().SequenceEqual(actual.RepositoryScope.Order());
}

using System.Text.Json.Serialization;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>
/// Application state for one owned turn. PendingSave is retained host state, never
/// a browser retry payload or evidence that a terminal transaction committed.
/// </summary>
public sealed record OctoberChatCoordinationResult(DialogueBeginResult Begin, DialogueTurnState? State,
    DialogueTerminalResult? Terminal, [property: JsonIgnore] DialogueTerminalSaveRequest? PendingSave);

/// <summary>
/// Coordinates one canonical admission, generation lifetime and terminal save.
/// Trusted host registration and provider composition remain separate requirements;
/// constructing this component does not activate a Chat endpoint or issue execution authority.
/// </summary>
public sealed class OctoberChatCoordinator
{
    private readonly IOctoberDialogueStore _store;
    private readonly IOctoberDialoguePayloadCodec _codec;
    private readonly IOctoberKnowledgeDisclosedReader _reader;
    private readonly IOctoberKnowledgeDependencyAuthority _dependencies;
    private readonly IOctoberDialogueExecutionAuthority _executionAuthority;
    private readonly IOctoberChatContextProvider _context;
    private readonly TimeProvider _clock;
    private readonly Func<DialogueBeginResult, OctoberChatBudget, OctoberChatProviderSnapshot,
        Func<CancellationToken, Task>, CancellationToken, Task<OctoberChatClientSession>> _sessionFactory;
    private readonly Action<Task<OctoberChatGenerationResult>>? _observeGeneration;
    private readonly SemaphoreSlim _reconciliation = new(1, 1);
    private readonly List<Task> _persistenceTasks = [];
    private DialogueBeginRequest? _request;
    private ReadSelectionRequest? _initialContext;
    private DialogueTerminalResult? _terminal;
    private Task<OctoberChatClientSession>? _factoryTask;
    private OctoberChatClientSession? _session;
    private Task<OctoberChatGenerationResult>? _generationTask;
    private Task? _settlementTask;
    private DialogueAdmission? _confirmedAdmission;
    private OctoberChatGenerationResult? _endedGeneration;
    private Task<DialogueTransitionResult>? _phaseTask;
    private DialogueTurnState? _uncertainPhase;
    private bool _saveAttempted;
    private int _started;
    private int _executing;

    public OctoberChatCoordinator(IOctoberDialogueStore store, IOctoberDialoguePayloadCodec codec,
        IOctoberKnowledgeDisclosedReader reader, IOctoberKnowledgeDependencyAuthority dependencies,
        IOctoberDialogueExecutionAuthority executionAuthority, IOctoberChatContextProvider context,
        OctoberChatProviderBindingService providers, OctoberChatRequestAdmission requests,
        Action<Task<OctoberChatGenerationResult>> observeGeneration)
        : this(store, codec, reader, dependencies, executionAuthority, context, providers, requests,
            TimeProvider.System, (admitted, budget, snapshot, reauthorize, token) =>
                OctoberChatClientSession.CreateAsync(providers, snapshot, admitted, budget,
                    executionAuthority, reauthorize, requests, token),
            observeGeneration ?? throw new ArgumentNullException(nameof(observeGeneration)))
    {
    }

    // Controlled composition uses the same integration-owned session and transfers
    // physical transport ownership through its existing internal factory.
    internal OctoberChatCoordinator(IOctoberDialogueStore store, IOctoberDialoguePayloadCodec codec,
        IOctoberKnowledgeDisclosedReader reader, IOctoberKnowledgeDependencyAuthority dependencies,
        IOctoberDialogueExecutionAuthority executionAuthority, IOctoberChatContextProvider context,
        OctoberChatProviderBindingService providers, OctoberChatRequestAdmission requests, TimeProvider clock,
        Func<DialogueBeginResult, OctoberChatBudget, OctoberChatProviderSnapshot,
            Func<CancellationToken, Task>, CancellationToken, Task<OctoberChatClientSession>> sessionFactory,
        Action<Task<OctoberChatGenerationResult>>? observeGeneration = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _executionAuthority = executionAuthority ?? throw new ArgumentNullException(nameof(executionAuthority));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(requests);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _observeGeneration = observeGeneration;
    }

    public DialogueBeginResult? Begin { get; private set; }
    public DialogueTurnState? State { get; private set; }
    [JsonIgnore] public DialogueTerminalSaveRequest? PendingSave { get; private set; }

    /// <summary>
    /// Submits the frozen request once. Its initial capsule must already contain the
    /// owner-agreed history union; the coordinator never rewrites admission inputs.
    /// An uncertain or replayed admission cannot authorize provider dispatch.
    /// </summary>
    public async Task<OctoberChatCoordinationResult> ExecuteAsync(DialogueBeginRequest frozenRequest,
        OctoberChatBudget originalBudget, ReadSelection selection, DialogueHistoryResult frozenHistory,
        OctoberChatProviderSnapshot snapshot, Guid saveOperationId,
        Func<string, CancellationToken, ValueTask> authorizedText,
        Action<OctoberChatToolProgress>? onToolProgress = null,
        CancellationToken explicitExecutionCancellation = default)
    {
        ArgumentNullException.ThrowIfNull(frozenRequest);
        ArgumentNullException.ThrowIfNull(originalBudget);
        ArgumentNullException.ThrowIfNull(authorizedText);
        if (saveOperationId == Guid.Empty) throw new ArgumentException("A terminal save identity is required.", nameof(saveOperationId));
        if (Interlocked.Exchange(ref _started, 1) != 0) throw Unavailable();
        Volatile.Write(ref _executing, 1);
        _request = frozenRequest;
        try
        {
            Begin = await AttemptAsync((subject, token) => _store.BeginTurnAsync(frozenRequest, subject, token),
                status => new DialogueBeginResult(status, null, false));
            State = Begin.Admission?.State;
            if (Begin is not { Status: DialogueStatus.Succeeded, MayDispatch: true, Admission: { State: DialogueTurnState.Admitted } admission })
            {
                Begin = Begin with { MayDispatch = false };
                return Snapshot();
            }
            // Preserve the original Admitted snapshot for both execution factories.
            if (!MatchesAdmission(admission, frozenRequest))
            {
                Begin = new(DialogueStatus.Invalid, null, false);
                State = null;
                return Snapshot();
            }
            _confirmedAdmission = admission;
            DialogueTransitionResult running = await TransitionAsync(DialogueTurnState.Admitted, DialogueTurnState.Running);
            if (running.Status != DialogueStatus.Succeeded || running.State != DialogueTurnState.Running)
            {
                // No factory or generation has started. Retain this exact end time
                // if a lost phase acknowledgement can later be reconciled.
                _endedGeneration = new(DialogueTerminalState.Failed, DialogueTerminalReason.ExecutionFailure,
                    DialogueProviderFinish.NotStarted, string.Empty, [], [], frozenRequest.InitialDependencies,
                    _clock.GetUtcNow(), saveOperationId);
                return Snapshot();
            }
            State = DialogueTurnState.Running;
            using var originalDeadline = originalBudget.CreateDeadlineCancellation();
            using var ownedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                explicitExecutionCancellation, originalBudget.OriginalCancellationToken);
            var observationReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            OctoberChatTurnRunner? runner = null;
            bool observerFailed = false;
            OctoberChatGenerationResult generated;
            try
            {
                ownedCancellation.Token.ThrowIfCancellationRequested();
                if (originalDeadline.IsCancellationRequested || admission.DeadlineUtc <= _clock.GetUtcNow() || originalBudget.IsStopped)
                    throw Unavailable();
                _factoryTask = _sessionFactory(Begin, originalBudget, snapshot, async token =>
                {
                    // RunAsync can advance synchronously. Physical send must wait until
                    // its exact task has been attached to the host's execution owner.
                    await observationReady.Task.WaitAsync(token);
                    if (runner is null) throw Unavailable();
                    await runner.ReauthorizeBeforeSendAsync(token);
                }, ownedCancellation.Token);
                _session = await _factoryTask;
                ownedCancellation.Token.ThrowIfCancellationRequested();
                runner = await OctoberChatTurnRunner.CreateAsync(Begin, _session.Client, originalBudget, selection,
                    frozenHistory, snapshot.ProviderId.ToString("D"), snapshot.Revision, _reader, _dependencies,
                    _codec, _executionAuthority, _context.GetCurrentAsync, saveOperationId, ownedCancellation.Token);
                if (runner is null) throw Unavailable();
                _generationTask = runner.RunAsync(authorizedText, ownedCancellation.Token, onToolProgress);
                try
                {
                    _observeGeneration?.Invoke(_generationTask);
                    observationReady.TrySetResult();
                }
                catch (Exception)
                {
                    observerFailed = true;
                    ownedCancellation.Cancel();
                    _settlementTask = _session.SealAndWaitForSettlementAsync();
                }
                generated = await _generationTask;
                if (observerFailed && !IsSanitized(generated))
                    generated = generated with { TerminalState = DialogueTerminalState.Failed, Reason = DialogueTerminalReason.ExecutionFailure };
            }
            catch (Exception)
            {
                // Even a host observer/cancellation callback failure cannot detach
                // an already started generation task from this lifecycle.
                if (_generationTask is not null)
                {
                    try { await _generationTask; }
                    catch (Exception) { }
                }
                bool cancelled = explicitExecutionCancellation.IsCancellationRequested || originalBudget.OriginalCancellationToken.IsCancellationRequested;
                bool expired = originalDeadline.IsCancellationRequested || admission.DeadlineUtc <= _clock.GetUtcNow();
                DialogueTerminalReason reason = observerFailed ? DialogueTerminalReason.ExecutionFailure : cancelled
                    ? DialogueTerminalReason.UserCancelled : expired ? DialogueTerminalReason.DeadlineExceeded
                    : _generationTask is null ? DialogueTerminalReason.InvalidConfiguration : DialogueTerminalReason.ExecutionFailure;
                generated = new(cancelled || expired ? DialogueTerminalState.Interrupted : DialogueTerminalState.Failed,
                    reason, _generationTask is null ? DialogueProviderFinish.NotStarted : DialogueProviderFinish.Unknown,
                    string.Empty, [], [], frozenRequest.InitialDependencies, _clock.GetUtcNow(), saveOperationId);
            }

            // A failed or cancelled factory has settled before reaching here. An
            // obtained session remains owned until BOTH physical owners confirm drain.
            _endedGeneration = generated;
            DialogueTransitionResult saving = await TransitionAsync(DialogueTurnState.Running, DialogueTurnState.Saving);
            if (_session is not null)
            {
                _settlementTask ??= _session.SealAndWaitForSettlementAsync();
                try { await _settlementTask; }
                catch (Exception) { return Snapshot(); } // Faulted cleanup is not settlement or release authority.
            }
            await TryFirstSaveAsync(saving);
            return Snapshot();
        }
        finally { Volatile.Write(ref _executing, 0); }
    }

    /// <summary>
    /// Reconciles only this instance's retained operation. Lookup uncertainty never
    /// means absence. A confirmed recovered phase may finish the first never-attempted
    /// terminal save; a submitted save is thereafter resolved only by exact lookup.
    /// </summary>
    public async Task<OctoberChatCoordinationResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        await _reconciliation.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref _executing) != 0 || _request is null) return Snapshot();
            if (_saveAttempted && PendingSave is { } pending)
            {
                DialogueTerminalResult result = await AttemptAsync((subject, token) => _store.LookupTerminalAsync(
                    new(pending.Owner.ConversationId, pending.Owner.ContextId, pending.Owner.TurnId,
                        pending.SaveOperationId, pending.Owner.Fence, pending.OutcomeDigest), subject, token),
                    status => new DialogueTerminalResult(status, null, null), cancellationToken);
                AcceptTerminal(result);
            }
            else if (!_saveAttempted && _confirmedAdmission is not null && _endedGeneration is not null &&
                (_uncertainPhase is not null || State == DialogueTurnState.Saving))
            {
                await ReconcilePhaseAsync(cancellationToken);
            }
            else if (Begin?.Status == DialogueStatus.Indeterminate)
            {
                DialogueBeginResult result = await AttemptAsync((subject, token) => _store.LookupAdmissionAsync(
                    new(_request.ConversationId, _request.ContextId, _request.ClientRequestKey, _request.RequestDigest), subject, token),
                    status => new DialogueBeginResult(status, null, false), cancellationToken);
                Begin = result with { MayDispatch = false };
                State = result.Admission?.State ?? State;
            }
            return Snapshot();
        }
        finally { _reconciliation.Release(); }
    }

    private async Task<DialogueTransitionResult> TransitionAsync(DialogueTurnState expected, DialogueTurnState next,
        CancellationToken cancellationToken = default)
    {
        _phaseTask = null;
        DialogueTransitionResult result = await AttemptAsync((subject, token) =>
        {
            _phaseTask = _store.TransitionTurnAsync(new(Ownership(), expected, next), subject, token);
            return _phaseTask;
        }, status => new DialogueTransitionResult(status, null), cancellationToken);
        if (result.Status == DialogueStatus.Indeterminate)
        {
            _uncertainPhase = next;
            State = null; // Last observed state is not confirmation of an uncertain write.
        }
        else if (result.Status is DialogueStatus.Succeeded or DialogueStatus.Existing)
        {
            State = result.State;
            _uncertainPhase = result.State == next ? null : next;
        }
        return result;
    }

    private async Task ReconcilePhaseAsync(CancellationToken cancellationToken)
    {
        // An observed matching row cannot settle an outstanding store/transport task.
        if (_phaseTask is null || !_phaseTask.IsCompleted || !OwnedWorkSettled()) return;
        DialogueTurnState expected = _uncertainPhase ?? DialogueTurnState.Saving;
        DialogueBeginResult found = await AttemptAsync((subject, token) => _store.LookupAdmissionAsync(
            new(_request!.ConversationId, _request.ContextId, _request.ClientRequestKey, _request.RequestDigest), subject, token),
            status => new DialogueBeginResult(status, null, false), cancellationToken);
        Begin = Begin! with { MayDispatch = false };
        if (found.Status is not (DialogueStatus.Succeeded or DialogueStatus.Existing) || found.Admission is not { } current ||
            !MatchesAdmission(current, _request!) || current.TurnId != _confirmedAdmission!.TurnId ||
            current.Sequence != _confirmedAdmission.Sequence || current.Fence != _confirmedAdmission.Fence ||
            current.AdmittedAtUtc != _confirmedAdmission.AdmittedAtUtc || current.DeadlineUtc != _confirmedAdmission.DeadlineUtc ||
            current.State != expected)
        {
            State = null;
            _uncertainPhase = expected;
            return;
        }
        State = current.State;
        _uncertainPhase = null;
        // Recovering Running closes a provably unstarted execution. It never
        // restores dispatch permission or fabricates a runner completion task.
        DialogueTransitionResult saving = expected == DialogueTurnState.Running
            ? await TransitionAsync(DialogueTurnState.Running, DialogueTurnState.Saving, cancellationToken)
            : new(DialogueStatus.Existing, DialogueTurnState.Saving);
        await TryFirstSaveAsync(saving, cancellationToken);
    }

    private async Task TryFirstSaveAsync(DialogueTransitionResult saving, CancellationToken cancellationToken = default)
    {
        if (_saveAttempted || PendingSave is not null || _endedGeneration is null || !OwnedWorkSettled() ||
            _phaseTask is null || !_phaseTask.IsCompleted) return;
        bool confirmedSaving = saving.Status is (DialogueStatus.Succeeded or DialogueStatus.Existing) && saving.State == DialogueTurnState.Saving;
        // Only an actual owner-returned refusal qualifies, not AttemptAsync's
        // missing-context result, thrown exception, timeout or uncertain mutation.
        bool refusedPhase = _phaseTask.IsCompletedSuccessfully &&
            _phaseTask.Result.Status is (DialogueStatus.Denied or DialogueStatus.AuthorityUnavailable) &&
            saving.Status == _phaseTask.Result.Status;
        if (!confirmedSaving && !refusedPhase) return;

        using var timeout = new CancellationTokenSource(OctoberDialogueLimits.MaximumPersistenceAttempt, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        try
        {
            Task<ReadSelectionRequest?> contextTask = _context.GetCurrentAsync(linked.Token);
            _ = ObserveAsync(contextTask);
            ReadSelectionRequest? current = await contextTask.WaitAsync(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (!ValidContext(current)) return;
            OctoberChatGenerationResult generated = _endedGeneration;
            if (generated.InferenceDependencies is { } completeDependencies)
            {
                Task<KnowledgeDependencyStatus> authorization = _dependencies.ReauthorizeAsync(current!, [completeDependencies], linked.Token);
                _ = ObserveAsync(authorization);
                KnowledgeDependencyStatus status = await authorization.WaitAsync(linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                if (status is KnowledgeDependencyStatus.Denied or KnowledgeDependencyStatus.AuthorityUnavailable)
                {
                    generated = generated with
                    {
                        TerminalState = DialogueTerminalState.Failed,
                        Reason = status == KnowledgeDependencyStatus.Denied ? DialogueTerminalReason.AuthorizationDenied : DialogueTerminalReason.AuthorityUnavailable,
                        AnswerText = string.Empty, References = [], ConsumedReads = [], InferenceDependencies = null
                    };
                }
                else if (status != KnowledgeDependencyStatus.Succeeded) return;
            }
            else if (!IsSanitized(generated)) return; // Missing lineage is never an authorized empty union.
            if (!confirmedSaving && !IsSanitized(generated)) return;

            var candidate = new DialogueTerminalSaveRequest(Ownership(), generated.SaveOperationId, new string('0', 64),
                generated.TerminalState, generated.Reason, generated.ProviderFinish, generated.AnswerText,
                generated.References, generated.ConsumedReads, generated.InferenceDependencies,
                _request!.HistoryTurnIds, generated.GenerationEndedAtUtc);
            candidate = candidate with { OutcomeDigest = _codec.ComputeOutcomeDigest(candidate) };
            int bytes = _codec.MeasureTerminalEnvelopeUtf8Bytes(candidate);
            if (bytes <= 0 || bytes > OctoberDialogueLimits.MaximumTerminalEnvelopeUtf8Bytes) return;
            linked.Token.ThrowIfCancellationRequested();
            // From this point, neither payload nor identities are ever rewritten.
            // Set before calling the port: even a synchronous throw is an attempt.
            PendingSave = candidate;
            _saveAttempted = true;
            Task<DialogueTerminalResult> save = _store.SaveTerminalAsync(candidate, current!.Subject, linked.Token);
            lock (_persistenceTasks) _persistenceTasks.Add(save);
            _ = ObservePersistenceAsync(save);
            AcceptTerminal(await save.WaitAsync(linked.Token));
        }
        catch (Exception)
        {
            if (_saveAttempted) AcceptTerminal(new(DialogueStatus.Indeterminate, null, null));
            // Before the first submission, uncertainty holds the slot and facts;
            // it cannot be converted to a fabricated authorization refusal.
        }
    }

    private DialogueTurnOwnership Ownership() => new(_request!.ConversationId, _request.ContextId,
        _confirmedAdmission!.TurnId, _request.Execution, _confirmedAdmission.Fence);

    private bool OwnedWorkSettled() => (_factoryTask is null || _factoryTask.IsCompleted) &&
        (_generationTask is null || _generationTask.IsCompleted) &&
        (_session is null || _settlementTask is { IsCompletedSuccessfully: true });

    private static bool IsSanitized(OctoberChatGenerationResult result) =>
        result.TerminalState is (DialogueTerminalState.Failed or DialogueTerminalState.Interrupted) &&
        result.Reason is (DialogueTerminalReason.AuthorizationDenied or DialogueTerminalReason.AuthorityUnavailable) &&
        result.AnswerText.Length == 0 && result.References.IsEmpty && result.ConsumedReads.IsEmpty && result.InferenceDependencies is null;

    private async Task<T> AttemptAsync<T>(Func<KnowledgeSubject, CancellationToken, Task<T>> operation,
        Func<DialogueStatus, T> refused, CancellationToken cancellationToken = default)
    {
        using var timeout = new CancellationTokenSource(OctoberDialogueLimits.MaximumPersistenceAttempt, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        try
        {
            Task<ReadSelectionRequest?> currentTask = _context.GetCurrentAsync(linked.Token);
            _ = ObserveAsync(currentTask);
            ReadSelectionRequest? current = await currentTask.WaitAsync(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (!ValidContext(current)) return refused(DialogueStatus.AuthorityUnavailable);
            _initialContext ??= current;
            Task<T> pending = operation(current!.Subject, linked.Token);
            lock (_persistenceTasks) _persistenceTasks.Add(pending);
            _ = ObservePersistenceAsync(pending);
            return await pending.WaitAsync(linked.Token);
        }
        catch (Exception) { return refused(DialogueStatus.Indeterminate); }
    }

    private bool ValidContext(ReadSelectionRequest? current) =>
        current is not null && current.Purpose == KnowledgePurpose.Chat && current.Subject is not null &&
        !string.IsNullOrWhiteSpace(current.Subject.SubjectId) && !string.IsNullOrWhiteSpace(current.Subject.AuthenticationBinding) &&
        !current.RepositoryScope.IsDefaultOrEmpty && (_initialContext is null ||
        current.Subject.SubjectId == _initialContext.Subject.SubjectId && current.Publication == _initialContext.Publication &&
        current.Mode == _initialContext.Mode && current.RepositoryScope.Order().SequenceEqual(_initialContext.RepositoryScope.Order()));

    private bool MatchesAdmission(DialogueAdmission admission, DialogueBeginRequest request)
    {
        try
        {
            return admission.Conversation.ConversationId == request.ConversationId && admission.Conversation.ContextId == request.ContextId &&
                admission.Request.ConversationId == request.ConversationId && admission.Request.ContextId == request.ContextId &&
                admission.Request.Execution == request.Execution && admission.Request.RequestDigest == request.RequestDigest &&
                admission.Request.ClientRequestKey == request.ClientRequestKey && admission.TurnId != Guid.Empty && admission.Sequence > 0 && admission.Fence > 0 &&
                _initialContext is not null && admission.Conversation.OwnerSubjectId == _initialContext.Subject.SubjectId &&
                admission.Conversation.Publication == _initialContext.Publication && admission.Conversation.Mode == _initialContext.Mode &&
                admission.Conversation.RepositoryScope.Order().SequenceEqual(_initialContext.RepositoryScope.Order()) &&
                _codec.ComputeBeginDigest(admission.Conversation, request) == request.RequestDigest &&
                _codec.ComputeBeginDigest(admission.Conversation, admission.Request) == request.RequestDigest;
        }
        catch (Exception) { return false; }
    }

    private void AcceptTerminal(DialogueTerminalResult result)
    {
        DialogueTerminalSaveRequest pending = PendingSave!;
        DialogueTerminalReceipt? receipt = result.Receipt;
        if (result.Status is (DialogueStatus.Succeeded or DialogueStatus.Existing) && receipt is not null &&
            receipt.ConversationId == pending.Owner.ConversationId && receipt.ContextId == pending.Owner.ContextId &&
            receipt.TurnId == pending.Owner.TurnId && receipt.Sequence == Begin!.Admission!.Sequence &&
            receipt.Fence == pending.Owner.Fence && receipt.SaveOperationId == pending.SaveOperationId &&
            receipt.OutcomeDigest == pending.OutcomeDigest && receipt.TerminalState == pending.TerminalState && receipt.Reason == pending.Reason)
        {
            _terminal = result;
            State = receipt.TerminalState switch
            {
                DialogueTerminalState.Completed => DialogueTurnState.Completed,
                DialogueTerminalState.Interrupted => DialogueTurnState.Interrupted,
                _ => DialogueTurnState.Failed
            };
            PendingSave = null;
        }
        else
        {
            // This is only local uncertainty; no durable SaveIndeterminate CAS is claimed.
            State = DialogueTurnState.SaveIndeterminate;
            _terminal = new(result.Status is DialogueStatus.Succeeded or DialogueStatus.Existing
                ? DialogueStatus.Indeterminate : result.Status, null, null);
        }
    }

    private OctoberChatCoordinationResult Snapshot() =>
        new(Begin ?? new(DialogueStatus.Indeterminate, null, false), State, _terminal, PendingSave);

    private static async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch (Exception) { } // Observation does not infer success or mutate retained state.
    }

    private async Task ObservePersistenceAsync(Task task)
    {
        await ObserveAsync(task);
        lock (_persistenceTasks) _persistenceTasks.Remove(task);
    }

    private static InvalidOperationException Unavailable() => new("The Chat coordination operation is unavailable.");
}

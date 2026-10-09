using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>Finite host ownership. Neither age, cancellation nor a missing row proves release.</summary>
public sealed class OctoberChatExecutionRegistry : IOctoberDialogueExecutionAuthority
{
    private readonly object _gate = new();
    private readonly IOctoberDialoguePayloadCodec _codec;
    private readonly Func<DialogueCommitAttempt, CancellationToken, Task<CommitEligibilityStatus>> _authority;
    private readonly TimeProvider _clock;
    private readonly int _maximum;
    private readonly Guid _generation = Guid.NewGuid();
    private readonly Dictionary<OctoberChatExecutionRegistration, Entry> _entries = [];
    private readonly OctoberChatDialogueCommitGuard _guard;

    public OctoberChatExecutionRegistry(IOctoberDialoguePayloadCodec codec,
        Func<DialogueCommitAttempt, CancellationToken, Task<CommitEligibilityStatus>> freshAuthority,
        int maximumRegistrations = 16) : this(codec, freshAuthority, TimeProvider.System, maximumRegistrations) { }

    internal OctoberChatExecutionRegistry(IOctoberDialoguePayloadCodec codec,
        Func<DialogueCommitAttempt, CancellationToken, Task<CommitEligibilityStatus>> freshAuthority,
        TimeProvider clock, int maximumRegistrations = 16)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _authority = freshAuthority ?? throw new ArgumentNullException(nameof(freshAuthority));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (maximumRegistrations <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRegistrations));
        _maximum = maximumRegistrations;
        _guard = new(AuthorizeCommitAsync, CurrentOwnership, _gate);
        CommitGuard = new GuardPort(this);
    }

    public IOctoberDialogueCommitGuard CommitGuard { get; }
    public int ActiveRegistrations { get { lock (_gate) return _entries.Count; } }

    internal OctoberChatExecutionRegistration? TryReserve(DialogueConversationIdentity conversation,
        Func<DialogueCommitAttempt, CancellationToken, Task<CommitEligibilityStatus>>? freshAuthority = null)
    {
        lock (_gate)
        {
            if (conversation is null || conversation.ConversationId == Guid.Empty || conversation.ContextId == Guid.Empty ||
                string.IsNullOrWhiteSpace(conversation.OwnerSubjectId) || conversation.Publication is null ||
                !Enum.IsDefined(conversation.Mode) || conversation.RepositoryScope.IsDefault ||
                conversation.RepositoryScope.Length > OctoberDialogueLimits.MaximumRepositories ||
                conversation.RepositoryScope.Any(x => x == Guid.Empty) ||
                conversation.RepositoryScope.Distinct().Count() != conversation.RepositoryScope.Length ||
                _entries.Count >= _maximum || _entries.Keys.Any(x => x.Conversation.ConversationId == conversation.ConversationId)) return null;
            var registration = new OctoberChatExecutionRegistration(conversation, new(_generation, Guid.NewGuid()));
            _entries.Add(registration, new(registration, freshAuthority ?? _authority));
            return registration;
        }
    }

    internal bool BindRequest(OctoberChatExecutionRegistration registration, DialogueBeginRequest request,
        OctoberChatBudget budget, OctoberChatProviderBindingService providerBinding,
        OctoberChatProviderSnapshot snapshot, Guid saveOperationId)
    {
        lock (_gate)
        {
            if (!Open(registration, out var e) || e.Request is not null || request is null || budget is null ||
                providerBinding is null || snapshot is null || saveOperationId == Guid.Empty || budget.IsStopped ||
                request.Execution != registration.Execution || request.ConversationId != registration.Conversation.ConversationId ||
                request.ContextId != registration.Conversation.ContextId || request.ClientRequestKey == Guid.Empty ||
                request.Limits != budget.Limits || request.ProviderProfileId != snapshot.ProviderId.ToString("D") ||
                request.ProviderProfileRevision != snapshot.Revision || request.InitialDependencies is null) return false;
            try
            {
                if (_codec.ComputeBeginDigest(registration.Conversation, request) != request.RequestDigest ||
                    _codec.MeasureBeginEnvelopeUtf8Bytes(request) > OctoberDialogueLimits.MaximumBeginEnvelopeUtf8Bytes) return false;
            }
            catch (Exception) { return false; }
            e.Request = request; e.Budget = budget; e.Binding = providerBinding; e.Snapshot = snapshot; e.SaveId = saveOperationId;
            return true;
        }
    }

    internal void ObserveGeneration(OctoberChatExecutionRegistration registration, Task<OctoberChatGenerationResult> generation)
    {
        lock (_gate)
        {
            var e = RequireOpen(registration);
            if (e.Generation is not null || !e.FactoryStarted || e.State != DialogueTurnState.Running) throw Refused();
            e.Generation = generation ?? throw new ArgumentNullException(nameof(generation));
            AddTask(e, generation, false);
        }
    }

    internal void Retain(OctoberChatExecutionRegistration registration, Task actualTask)
    { lock (_gate) AddTask(RequireOpen(registration), actualTask, false); }
    internal void RetainCleanup(OctoberChatExecutionRegistration registration, Task actualCleanup)
    { lock (_gate) AddTask(RequireOpen(registration), actualCleanup, true); }

    internal void RegisterStoreOwner(OctoberChatExecutionRegistration registration, IOctoberDialogueStore store)
    {
        lock (_gate)
        {
            var e = RequireOpen(registration);
            if (e.Store is not null && !ReferenceEquals(e.Store, store)) throw Refused();
            e.Store = store;
            e.StoreOwner = store as IAsyncDisposable;
        }
    }

    internal void RegisterScopeCleanup(OctoberChatExecutionRegistration registration, Func<Task> finalCleanup)
    {
        ArgumentNullException.ThrowIfNull(finalCleanup);
        lock (_gate)
        {
            var e = RequireOpen(registration);
            if (e.ScopeCleanup is not null) throw Refused();
            e.ScopeCleanup = finalCleanup;
        }
    }

    internal Task<T> StartTrackedAsync<T>(OctoberChatExecutionRegistration registration, Func<Task<T>> start) =>
        Start(registration, start, false);
    internal Task StartTrackedAsync(OctoberChatExecutionRegistration registration, Func<Task> start) => Start(registration, start, false);
    internal Task StartCleanupAsync(OctoberChatExecutionRegistration registration, Func<Task> startCleanup) => Start(registration, startCleanup, true);

    private TTask Start<TTask>(OctoberChatExecutionRegistration registration, Func<TTask> start, bool cleanup) where TTask : Task
    {
        lock (_gate)
        {
            var e = RequireOpen(registration);
            e.Starting++;
            try
            {
                TTask task = start() ?? throw Refused();
                AddTask(e, task, cleanup);
                return task;
            }
            catch
            {
                if (cleanup) { e.CleanupFailed = true; e.BlockingCleanupFailed = true; }
                throw;
            }
            finally { e.Starting--; Complete(e); }
        }
    }

    private void AddTask(Entry e, Task task, bool cleanup)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (ReferenceEquals(task, e.Registration.Completion)) throw Refused();
        e.Tasks.Add((task, cleanup));
        _ = ObserveAsync(e, task, cleanup);
    }

    private async Task ObserveAsync(Entry e, Task task, bool cleanup)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception)
        {
            if (cleanup)
                lock (_gate)
                {
                    e.CleanupFailed = true;
                    if (!AllowsFinalScopeAttempt(e, task)) e.BlockingCleanupFailed = true;
                }
        }
        lock (_gate)
        {
            e.Tasks.RemoveAll(x => ReferenceEquals(x.Task, task));
            Complete(e);
        }
    }

    internal Task SealAndWaitForSettlementAsync(OctoberChatExecutionRegistration registration)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(registration, out var e)) return registration.Completion;
            e.Closed = true;
            StartSessionSettlement(e);
            Complete(e);
            return registration.Completion;
        }
    }

    private void Complete(Entry e)
    {
        if (!e.Closed || e.Starting != 0 || e.Tasks.Any(x => !x.Task.IsCompleted)) return;
        if (e.BlockingCleanupFailed || e.Tasks.Any(x => x.Cleanup && !x.Task.IsCompletedSuccessfully &&
            !AllowsFinalScopeAttempt(e, x.Task)))
        { e.Registration.End.TrySetException(new IOException("Chat cleanup could not be confirmed.")); return; }
        if (e.OwnBeginMayCommit && !e.TerminalConfirmed) return;
        if (e.StoreUsed && e.StoreSettlement is null)
        {
            if (e.StoreOwner is null) return; // Unknown physical owner cleanup is not a completed task.
            e.Starting++;
            try
            {
                e.StoreSettlement = e.StoreOwner.DisposeAsync().AsTask();
                AddTask(e, e.StoreSettlement, true);
            }
            catch (Exception)
            {
                e.CleanupFailed = true;
                e.BlockingCleanupFailed = true;
                e.Registration.End.TrySetException(new IOException("Dialogue cleanup could not be confirmed."));
                return;
            }
            finally { e.Starting--; }
            if (!e.StoreSettlement.IsCompleted) return;
            if (!e.StoreSettlement.IsCompletedSuccessfully && !e.StoreSettlement.IsFaulted)
            { e.Registration.End.TrySetException(new IOException("Dialogue cleanup could not be confirmed.")); return; }
        }
        if (e.ScopeCleanup is not null && e.ScopeSettlement is null)
        {
            e.Starting++;
            try
            {
                e.ScopeSettlement = FinishScopeAsync(e.ScopeCleanup);
                AddTask(e, e.ScopeSettlement, true);
            }
            finally { e.Starting--; }
            if (!e.ScopeSettlement.IsCompleted) return;
        }
        if (e.CleanupFailed || e.StoreSettlement is { IsCompletedSuccessfully: false } ||
            e.ScopeSettlement is { IsCompletedSuccessfully: false })
        {
            e.Registration.End.TrySetException(new IOException("Chat cleanup could not be confirmed."));
            return;
        }
        _entries.Remove(e.Registration);
        foreach (var operation in e.Operations)
            if (operation.Attempt is { } attempt) _guard.ForgetClosed(attempt);
        e.Registration.End.TrySetResult();
    }

    private static async Task FinishScopeAsync(Func<Task> cleanup)
    {
        await Task.Yield(); // Never synchronously dispose a service scope under the registry lock.
        await cleanup().ConfigureAwait(false);
    }

    // A returned, faulted owner-disposal task has settled. Attempt the remaining scope cleanup,
    // but never convert that failure into successful release. Pending or failed transport work
    // is a different obligation and cannot permit disposal of its owning services.
    private static bool AllowsFinalScopeAttempt(Entry e, Task task) =>
        (ReferenceEquals(task, e.StoreSettlement) && task.IsFaulted) || ReferenceEquals(task, e.ScopeSettlement);

    internal Task<OctoberChatClientSession> CreateClientSessionAsync(OctoberChatExecutionRegistration registration,
        DialogueBeginResult originalAdmission, Func<CancellationToken, Task> reauthorizeBeforeSend,
        OctoberChatRequestAdmission requests, CancellationToken cancellationToken = default) =>
        CreateClientSessionAsync(registration, originalAdmission, reauthorizeBeforeSend, requests,
            new SocketsHttpHandler { AllowAutoRedirect = false }, cancellationToken);

    internal Task<OctoberChatClientSession> CreateClientSessionAsync(OctoberChatExecutionRegistration registration,
        DialogueBeginResult originalAdmission, Func<CancellationToken, Task> reauthorizeBeforeSend,
        OctoberChatRequestAdmission requests, HttpMessageHandler physicalTransport, CancellationToken cancellationToken = default)
    {
        bool transferred = false;
        var physical = new OwnedPhysicalTransport(physicalTransport);
        try
        {
            lock (_gate)
            {
                var e = RequireOpen(registration);
                AddTask(e, physical.Completion, true);
                if (e.FactoryStarted || e.Admission is null || e.State != DialogueTurnState.Running ||
                    originalAdmission is not { Status: DialogueStatus.Succeeded, MayDispatch: true, Admission: { } admitted } ||
                    !SameAdmission(e.Admission, admitted)) throw Refused();
                e.FactoryStarted = true;
                e.PhysicalCleanup = physical.Completion;
                transferred = true;
                e.Factory = StartTrackedAsync(registration, async () =>
                {
                    var session = await OctoberChatClientSession.CreateAsync(e.Binding!, e.Snapshot!, originalAdmission,
                        e.Budget!, this, async token =>
                        {
                            lock (_gate) if (e.Closed) throw Refused();
                            // RunAsync may reach this callback before its actual task is
                            // registered. Let the coordinator's observation barrier settle
                            // before checking ownership, then recheck closure before send.
                            await reauthorizeBeforeSend(token);
                            lock (_gate) if (e.Closed || e.Generation is null) throw Refused();
                        }, requests, physical, _clock, cancellationToken,
                        start => StartTrackedAsync(registration, start));
                    Task? closedDrain = null;
                    lock (_gate)
                    {
                        e.Session = session;
                        if (e.Closed)
                        {
                            StartSessionSettlement(e);
                            closedDrain = e.SessionSettlement;
                        }
                    }
                    if (closedDrain is not null) { await closedDrain; throw Refused(); }
                    return session;
                });
                return e.Factory;
            }
        }
        catch
        {
            if (!transferred) physical.Dispose();
            throw;
        }
    }

    public async Task<DialogueExecutionStatus> VerifyAdmissionAsync(DialogueBeginRequest request, CancellationToken cancellationToken = default)
    {
        Entry? entry;
        Task<CommitEligibilityStatus> actual;
        lock (_gate)
        {
            var e = _entries.Values.SingleOrDefault(x => x.Request is not null && SameRequest(x.Request, request));
            if (cancellationToken.IsCancellationRequested || e is null || !AdmissionCurrent(e)) return DialogueExecutionStatus.NotVerified;
            entry = e;
            var probe = new DialogueCommitAttempt(DialogueMutationKind.AdmitTurn, request.ClientRequestKey,
                e.Registration.Conversation, null, request.Execution, null, request.RequestDigest, e.Subject, request.InitialDependencies);
            try { actual = StartTrackedAsync(e.Registration, () => e.Authority(probe, cancellationToken)); }
            catch (Exception) { return DialogueExecutionStatus.Unavailable; }
        }
        try
        {
            var result = await actual.WaitAsync(cancellationToken);
            lock (_gate) return result == CommitEligibilityStatus.Eligible && !cancellationToken.IsCancellationRequested && AdmissionCurrent(entry)
                ? DialogueExecutionStatus.Verified : DialogueExecutionStatus.NotVerified;
        }
        catch (Exception) { return DialogueExecutionStatus.Unavailable; }
    }

    private bool AdmissionCurrent(Entry e) => !e.Closed && !e.Budget!.IsStopped &&
        (e.Admission is null || e.Admission.DeadlineUtc > _clock.GetUtcNow()) &&
        e.State is null or DialogueTurnState.Admitted or DialogueTurnState.Running;

    public async Task<DialogueExecutionStatus> VerifyTerminalAsync(DialogueAdmission admission, DialogueTerminalSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        Entry? entry;
        DialogueCommitAttempt? authorityProbe = null;
        lock (_gate)
        {
            var e = _entries.Values.SingleOrDefault(x => x.Admission is not null && SameAdmission(x.Admission, admission));
            entry = e;
            if (e is { Session: not null, Factory.IsCompleted: true } && (e.Generation is null || e.Generation.IsCompleted))
                StartSessionSettlement(e);
            bool valid = !cancellationToken.IsCancellationRequested && e is { Closed: false } &&
                request.SaveOperationId == e.SaveId && OwnerMatches(e, request.Owner) &&
                e.Operations.Where(x => x.Kind != DialogueMutationKind.SaveTerminal).All(x => x.Done) &&
                (!e.FactoryStarted || e.Factory is { IsCompleted: true }) &&
                (!e.FactoryStarted || e.PhysicalCleanup is { IsCompletedSuccessfully: true }) && !e.CleanupFailed &&
                (e.Generation is null || e.Generation.IsCompleted) &&
                (e.Session is null || e.SessionSettlement is { IsCompletedSuccessfully: true });
            if (valid)
            {
                try
                {
                    valid = _codec.ComputeOutcomeDigest(request) == request.OutcomeDigest &&
                        request.HistoryTurnIds.SequenceEqual(e!.Request!.HistoryTurnIds);
                    if (valid && e!.Generation is { IsCompletedSuccessfully: true } generation)
                    {
                        var facts = generation.Result;
                        valid = facts.SaveOperationId == request.SaveOperationId && facts.GenerationEndedAtUtc == request.GenerationEndedAtUtc &&
                            facts.ProviderFinish == request.ProviderFinish;
                        if (valid && !SameOutcome(facts, request))
                        {
                            valid = IsSanitized(request) && facts.InferenceDependencies is not null;
                            if (valid) authorityProbe = new(DialogueMutationKind.SaveTerminal, request.SaveOperationId,
                                e.Registration.Conversation, request.Owner.TurnId, request.Owner.Execution, request.Owner.Fence,
                                request.OutcomeDigest, e.Subject, facts.InferenceDependencies);
                        }
                    }
                    else if (valid)
                    {
                        valid = e!.Generation is null && request.ProviderFinish == DialogueProviderFinish.NotStarted &&
                            request.TerminalState is DialogueTerminalState.Failed or DialogueTerminalState.Interrupted &&
                            request.AnswerText.Length == 0 && request.References.IsEmpty && request.ConsumedReads.IsEmpty &&
                            request.GenerationEndedAtUtc >= e.Admission!.AdmittedAtUtc && request.GenerationEndedAtUtc <= _clock.GetUtcNow();
                        if (valid && IsSanitized(request))
                            authorityProbe = new(DialogueMutationKind.SaveTerminal, request.SaveOperationId,
                                e.Registration.Conversation, request.Owner.TurnId, request.Owner.Execution, request.Owner.Fence,
                                request.OutcomeDigest, e.Subject, e.Request!.InitialDependencies);
                        else if (valid)
                            valid = request.InferenceDependencies == e.Request!.InitialDependencies &&
                                request.Reason is DialogueTerminalReason.InvalidConfiguration or DialogueTerminalReason.ExecutionFailure or
                                    DialogueTerminalReason.UserCancelled or DialogueTerminalReason.DeadlineExceeded;
                    }
                }
                catch (Exception) { valid = false; }
            }
            if (!valid) return DialogueExecutionStatus.NotVerified;
        }
        if (authorityProbe is null) return DialogueExecutionStatus.Verified;
        try
        {
            // This comparison uses observed generation lineage. It is not registered or submitted as a write.
            Task<CommitEligibilityStatus> actual = StartTrackedAsync(entry!.Registration,
                () => entry.Authority(authorityProbe, cancellationToken));
            var status = await actual.WaitAsync(cancellationToken);
            lock (_gate)
                return !entry.Closed && !cancellationToken.IsCancellationRequested &&
                    ((request.Reason == DialogueTerminalReason.AuthorizationDenied && status == CommitEligibilityStatus.Denied) ||
                     (request.Reason == DialogueTerminalReason.AuthorityUnavailable && status == CommitEligibilityStatus.AuthorityUnavailable))
                    ? DialogueExecutionStatus.Verified : DialogueExecutionStatus.NotVerified;
        }
        catch (Exception) { return DialogueExecutionStatus.Unavailable; }
    }

    public Task<DialogueExecutionStatus> VerifyOwnerDeathAsync(DialogueExecutionIdentity execution,
        CancellationToken cancellationToken = default) => Task.FromResult(DialogueExecutionStatus.NotVerified);

    private void StartSessionSettlement(Entry e)
    {
        if (e.Session is null || e.SessionSettlement is not null) return;
        e.Starting++;
        try
        {
            e.SessionSettlement = e.Session.SealAndWaitForSettlementAsync();
            AddTask(e, e.SessionSettlement, true);
        }
        catch (Exception) { e.CleanupFailed = true; e.BlockingCleanupFailed = true; }
        finally { e.Starting--; }
    }

    internal Task<T> ObserveStoreAsync<T>(OctoberChatExecutionRegistration registration, DialogueMutationKind? kind,
        object request, KnowledgeSubject? subject, Func<Task<T>> start)
    {
        lock (_gate)
        {
            var e = RequireOpen(registration);
            if (subject is not null && subject.SubjectId != registration.Conversation.OwnerSubjectId) throw Refused();
            if (!RequestMatches(e, request)) throw Refused();
            if (kind == DialogueMutationKind.InterruptOrphan || (kind is not null && e.Operations.Any(x => !x.Done))) throw Refused();
            if (kind is not null && (e.Operations.Count >= 5 || e.Operations.Any(x => x.Kind == kind &&
                (kind != DialogueMutationKind.TransitionTurn ||
                 OctoberKnowledgePolicy.SerializeCanonical(x.Request) == OctoberKnowledgePolicy.SerializeCanonical(request))))) throw Refused();
            e.Subject = subject;
            e.StoreUsed = true;
            var operation = kind is { } k ? new Operation(k, request, subject!) : null;
            if (operation is not null) e.Operations.Add(operation);
            if (kind == DialogueMutationKind.AdmitTurn) e.OwnBeginMayCommit = true;
            if (request is DialogueTerminalSaveRequest save) e.PendingSave = save;
            return StartTrackedAsync(registration, async () =>
            {
                try
                {
                    T result = await StartTrackedAsync(registration, start);
                    lock (_gate)
                    {
                        if (operation is not null && request is DialogueBeginRequest && result is DialogueBeginResult
                            { Admission: null, Status: DialogueStatus.Busy or DialogueStatus.Conflict or DialogueStatus.Invalid or
                                DialogueStatus.StaleFence or DialogueStatus.Denied or DialogueStatus.AuthorityUnavailable })
                            operation.DefinitiveRefusal = true;
                        AcceptResult(e, request, result);
                    }
                    return result;
                }
                finally
                {
                    if (operation is not null)
                    {
                        Task closure;
                        lock (_gate) closure = operation.Attempt is { } attempt ? _guard.Close(attempt) : Task.CompletedTask;
                        lock (_gate) AddTask(e, closure, false);
                        _ = FinishOperationAsync(e, operation, closure);
                    }
                }
            });
        }
    }

    private async Task FinishOperationAsync(Entry e, Operation operation, Task closure)
    {
        try { await closure.ConfigureAwait(false); }
        catch (Exception) { return; }
        lock (_gate)
        {
            operation.Done = true;
            if (operation.Kind == DialogueMutationKind.AdmitTurn && operation.DefinitiveRefusal &&
                (operation.Attempt is null || !_guard.SubmissionStarted(operation.Attempt))) e.OwnBeginMayCommit = false;
            Complete(e);
        }
    }

    private static bool RequestMatches(Entry e, object request)
    {
        var c = e.Registration.Conversation;
        return request switch
        {
            DialogueConversationOpenRequest r => r.ConversationId == c.ConversationId && r.ContextId == c.ContextId,
            DialogueBeginRequest r => e.Request is not null && SameRequest(e.Request, r),
            DialogueAdmissionLookup r => r.ConversationId == c.ConversationId && r.ContextId == c.ContextId &&
                e.Request is not null && r.ClientRequestKey == e.Request.ClientRequestKey && r.RequestDigest == e.Request.RequestDigest,
            DialogueTransitionRequest r => OwnerMatches(e, r.Owner),
            DialogueTerminalSaveRequest r => OwnerMatches(e, r.Owner) && r.SaveOperationId == e.SaveId,
            DialogueTerminalLookup r => e.Admission is { } a && r.ConversationId == c.ConversationId && r.ContextId == c.ContextId &&
                r.TurnId == a.TurnId && r.ExpectedFence == a.Fence && r.SaveOperationId == e.SaveId,
            DialogueHistoryRequest r => r.ConversationId == c.ConversationId && r.ContextId == c.ContextId,
            _ => false
        };
    }

    private void AcceptResult<T>(Entry e, object request, T result)
    {
        if (request is DialogueBeginRequest && result is DialogueBeginResult { Status: DialogueStatus.Succeeded, MayDispatch: true, Admission: { } a } &&
            SameConversation(e.Registration.Conversation, a.Conversation) && SameRequest(e.Request!, a.Request) &&
            a.State == DialogueTurnState.Admitted && a.TurnId != Guid.Empty && a.Fence > 0 && a.Sequence > 0 &&
            a.DeadlineUtc > a.AdmittedAtUtc && a.DeadlineUtc <= a.AdmittedAtUtc + a.Request.Limits.Duration)
        { e.Admission = a; e.State = a.State; }
        else if (request is DialogueTransitionRequest t && result is DialogueTransitionResult { Status: DialogueStatus.Succeeded } transition &&
            OwnerMatches(e, t.Owner) && transition.State == t.NextState) e.State = transition.State;
        else if (request is DialogueAdmissionLookup && result is DialogueBeginResult
            { Status: DialogueStatus.Succeeded or DialogueStatus.Existing, Admission: { } existing } &&
            e.Admission is not null && SameAdmission(e.Admission, existing) && e.Operations.All(x => x.Done))
            e.State = existing.State; // A lookup can reconcile phase; it never installs a dispatchable admission.
        if (result is DialogueTerminalResult { Status: DialogueStatus.Succeeded or DialogueStatus.Existing, Receipt: { } receipt } &&
            e.PendingSave is { } pending && e.Admission is { } admitted &&
            receipt.ConversationId == pending.Owner.ConversationId && receipt.ContextId == pending.Owner.ContextId &&
            receipt.TurnId == pending.Owner.TurnId && receipt.Fence == pending.Owner.Fence && receipt.Sequence == admitted.Sequence &&
            receipt.SaveOperationId == pending.SaveOperationId && receipt.OutcomeDigest == pending.OutcomeDigest &&
            receipt.TerminalState == pending.TerminalState && receipt.Reason == pending.Reason)
            e.TerminalConfirmed = true;
    }

    private bool Pin(DialogueCommitAttempt attempt)
    {
        lock (_gate)
        {
            var e = _entries.Values.SingleOrDefault(x => !x.Closed && SameConversation(x.Registration.Conversation, attempt.Conversation));
            var op = e?.Operations.SingleOrDefault(x => !x.Done);
            if (e is null || op is null || op.Kind != attempt.Kind || op.Subject != attempt.Subject || !Matches(e, op, attempt)) return false;
            if (op.Attempt is null) { _guard.Register(attempt); op.Attempt = attempt; }
            return ExactAttempt(op.Attempt) == ExactAttempt(attempt);
        }
    }

    private bool Matches(Entry e, Operation op, DialogueCommitAttempt a) => op.Request switch
    {
        DialogueConversationOpenRequest r => r.ConversationId == a.Conversation.ConversationId && r.ContextId == a.Conversation.ContextId &&
            r.CreateIfMissing is { } creation && creation.Publication == a.Conversation.Publication && creation.Mode == a.Conversation.Mode &&
            creation.RepositoryScope.SequenceEqual(a.Conversation.RepositoryScope) && a.MutationId == r.ConversationId &&
            a.TurnId is null && a.Execution is null && a.ExpectedFence is null && a.Dependencies is null &&
            a.PayloadDigest == OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(a.Conversation)),
        DialogueBeginRequest r => e.Request is not null && SameRequest(e.Request, r) && a.MutationId == r.ClientRequestKey &&
            a.Execution == r.Execution && a.TurnId is null && a.ExpectedFence is null && a.PayloadDigest == r.RequestDigest && a.Dependencies == r.InitialDependencies,
        DialogueTransitionRequest r => OwnerMatches(e, r.Owner) && e.State == r.ExpectedState &&
            ((r.ExpectedState == DialogueTurnState.Admitted && r.NextState == DialogueTurnState.Running) ||
             (r.ExpectedState == DialogueTurnState.Running && r.NextState == DialogueTurnState.Saving)) &&
            AttemptOwner(a, r.Owner) && a.Dependencies == e.Request!.InitialDependencies &&
            a.PayloadDigest == OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(r)) &&
            a.MutationId == new Guid(Convert.FromHexString(a.PayloadDigest).AsSpan(0, 16)),
        DialogueTerminalSaveRequest r => OwnerMatches(e, r.Owner) && r.SaveOperationId == e.SaveId && a.MutationId == r.SaveOperationId &&
            AttemptOwner(a, r.Owner) && a.Dependencies == r.InferenceDependencies && a.PayloadDigest == r.OutcomeDigest &&
            _codec.ComputeOutcomeDigest(r) == r.OutcomeDigest,
        _ => false
    };

    private Task<CommitEligibilityStatus> AuthorizeCommitAsync(DialogueCommitAttempt attempt, CancellationToken token)
    {
        lock (_gate)
        {
            var e = _entries.Values.SingleOrDefault(x => x.Operations.Any(o => o.Attempt is not null && ExactAttempt(o.Attempt) == ExactAttempt(attempt)));
            return e is null ? Task.FromResult(CommitEligibilityStatus.StaleOrFenced) :
                StartTrackedAsync(e.Registration, () => e.Authority(attempt, token));
        }
    }

    private CommitEligibilityStatus CurrentOwnership(DialogueCommitAttempt attempt) => Pin(attempt)
        ? CommitEligibilityStatus.Eligible : CommitEligibilityStatus.StaleOrFenced;
    private bool Open(OctoberChatExecutionRegistration r, out Entry e) => _entries.TryGetValue(r, out e!) && !e.Closed;
    private Entry RequireOpen(OctoberChatExecutionRegistration r) => Open(r, out var e) ? e : throw Refused();
    private static bool OwnerMatches(Entry e, DialogueTurnOwnership o) => e.Admission is { } a &&
        o.ConversationId == a.Conversation.ConversationId && o.ContextId == a.Conversation.ContextId &&
        o.TurnId == a.TurnId && o.Execution == a.Request.Execution && o.Fence == a.Fence;
    private static bool AttemptOwner(DialogueCommitAttempt a, DialogueTurnOwnership o) => a.TurnId == o.TurnId && a.Execution == o.Execution && a.ExpectedFence == o.Fence;
    private static bool SameConversation(DialogueConversationIdentity a, DialogueConversationIdentity b) =>
        OctoberKnowledgePolicy.SerializeCanonical(a) == OctoberKnowledgePolicy.SerializeCanonical(b);
    private static bool SameRequest(DialogueBeginRequest a, DialogueBeginRequest b) => a is not null && b is not null &&
        a.InitialDependencies == b.InitialDependencies && OctoberKnowledgePolicy.SerializeCanonical(a) == OctoberKnowledgePolicy.SerializeCanonical(b);
    private static bool SameAdmission(DialogueAdmission a, DialogueAdmission b) => SameRequest(a.Request, b.Request) &&
        a.TurnId == b.TurnId && a.Fence == b.Fence && a.Sequence == b.Sequence && a.AdmittedAtUtc == b.AdmittedAtUtc &&
        a.DeadlineUtc == b.DeadlineUtc && SameConversation(a.Conversation, b.Conversation);
    private static string ExactAttempt(DialogueCommitAttempt a) => OctoberKnowledgePolicy.SerializeCanonical(a);
    private static bool IsSanitized(DialogueTerminalSaveRequest r) => r.TerminalState == DialogueTerminalState.Failed &&
        r.Reason is DialogueTerminalReason.AuthorizationDenied or DialogueTerminalReason.AuthorityUnavailable &&
        r.AnswerText.Length == 0 && r.References.IsEmpty && r.ConsumedReads.IsEmpty && r.InferenceDependencies is null;
    private static bool SameOutcome(OctoberChatGenerationResult f, DialogueTerminalSaveRequest r) =>
        f.TerminalState == r.TerminalState && f.Reason == r.Reason && f.AnswerText == r.AnswerText &&
        f.InferenceDependencies == r.InferenceDependencies && f.References.SequenceEqual(r.References) &&
        f.ConsumedReads.SequenceEqual(r.ConsumedReads);
    private static InvalidOperationException Refused() => new("October Chat execution ownership is unavailable.");

    private sealed class GuardPort(OctoberChatExecutionRegistry registry) : IOctoberDialogueCommitGuard
    {
        public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(DialogueCommitAttempt attempt, CancellationToken cancellationToken = default) =>
            registry.Pin(attempt) ? registry._guard.CheckBeforeTransactionAsync(attempt, cancellationToken) : Task.FromResult(CommitEligibilityStatus.StaleOrFenced);
        public Task<CommitGuardResult> SubmitAsync(DialogueCommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync,
            CancellationToken cancellationToken = default) => registry.Pin(attempt) ? registry._guard.SubmitAsync(attempt, submitAsync, cancellationToken) :
                Task.FromResult(new CommitGuardResult(CommitSubmissionStatus.StaleOrFenced, null));
    }
    private sealed class Operation(DialogueMutationKind kind, object request, KnowledgeSubject subject)
    {
        internal DialogueMutationKind Kind { get; } = kind;
        internal object Request { get; } = request;
        internal KnowledgeSubject Subject { get; } = subject;
        internal DialogueCommitAttempt? Attempt { get; set; }
        internal bool Done { get; set; }
        internal bool DefinitiveRefusal { get; set; }
    }
    private sealed class Entry(OctoberChatExecutionRegistration registration,
        Func<DialogueCommitAttempt, CancellationToken, Task<CommitEligibilityStatus>> authority)
    {
        internal OctoberChatExecutionRegistration Registration { get; } = registration;
        internal Func<DialogueCommitAttempt, CancellationToken, Task<CommitEligibilityStatus>> Authority { get; } = authority;
        internal List<(Task Task, bool Cleanup)> Tasks { get; } = [];
        internal List<Operation> Operations { get; } = [];
        internal bool Closed, CleanupFailed, BlockingCleanupFailed, FactoryStarted, OwnBeginMayCommit, TerminalConfirmed, StoreUsed;
        internal int Starting;
        internal DialogueBeginRequest? Request;
        internal OctoberChatBudget? Budget;
        internal OctoberChatProviderBindingService? Binding;
        internal OctoberChatProviderSnapshot? Snapshot;
        internal Guid SaveId;
        internal DialogueAdmission? Admission;
        internal DialogueTurnState? State;
        internal Task<OctoberChatClientSession>? Factory;
        internal OctoberChatClientSession? Session;
        internal Task<OctoberChatGenerationResult>? Generation;
        internal Task? SessionSettlement;
        internal KnowledgeSubject? Subject;
        internal DialogueTerminalSaveRequest? PendingSave;
        internal IOctoberDialogueStore? Store;
        internal IAsyncDisposable? StoreOwner;
        internal Task? StoreSettlement;
        internal Task? PhysicalCleanup;
        internal Func<Task>? ScopeCleanup;
        internal Task? ScopeSettlement;
    }

    private sealed class OwnedPhysicalTransport(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _disposed;
        internal Task Completion => _completion.Task;
        protected override void Dispose(bool disposing)
        {
            if (!disposing || Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { base.Dispose(true); _completion.TrySetResult(); }
            catch (Exception)
            {
                _completion.TrySetException(new IOException("Chat transport cleanup could not be confirmed."));
                throw;
            }
        }
    }
}

public sealed class OctoberChatExecutionRegistration
{
    internal OctoberChatExecutionRegistration(DialogueConversationIdentity conversation, DialogueExecutionIdentity execution)
    { Conversation = conversation; Execution = execution; }
    public DialogueConversationIdentity Conversation { get; }
    public DialogueExecutionIdentity Execution { get; }
    internal TaskCompletionSource End { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Completion => End.Task;
}

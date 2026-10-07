using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>
/// Host-local submission arbitration. The owning execution registers an exact
/// mutation before calling the canonical store. This is not execution authority,
/// a durable receipt, or proof of owner death.
/// </summary>
internal sealed class OctoberChatDialogueCommitGuard(
    Func<DialogueCommitAttempt, CancellationToken, Task<CommitEligibilityStatus>> freshAuthority,
    Func<DialogueCommitAttempt, CommitEligibilityStatus> currentOwnership, object fenceGate)
    : IOctoberDialogueCommitGuard
{
    private readonly object _gate = fenceGate ?? throw new ArgumentNullException(nameof(fenceGate));
    private readonly Func<DialogueCommitAttempt, CancellationToken, Task<CommitEligibilityStatus>> _freshAuthority =
        freshAuthority ?? throw new ArgumentNullException(nameof(freshAuthority));
    private readonly Func<DialogueCommitAttempt, CommitEligibilityStatus> _currentOwnership =
        currentOwnership ?? throw new ArgumentNullException(nameof(currentOwnership));
    private readonly Dictionary<(Guid Conversation, DialogueMutationKind Kind, Guid Mutation), Entry> _entries = [];

    internal void Register(DialogueCommitAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        if (attempt.MutationId == Guid.Empty || attempt.Conversation.ConversationId == Guid.Empty || !Enum.IsDefined(attempt.Kind))
            throw new ArgumentException("A host mutation identity is required.", nameof(attempt));
        string binding = Binding(attempt);
        lock (_gate)
        {
            if (_entries.TryGetValue(Key(attempt), out var existing))
            {
                if (existing.Binding != binding) throw new InvalidOperationException("The mutation is already bound.");
                return; // Identical registration never renews a closed or used mutation.
            }
            _entries.Add(Key(attempt), new(binding));
        }
    }

    /// <summary>
    /// Revokes future submission under the callback's own lock. A returned pending
    /// task must remain owned; neither cancellation nor an exception proves cleanup.
    /// </summary>
    internal Task Close(DialogueCommitAttempt attempt)
    {
        lock (_gate)
        {
            if (!TryFind(attempt, out var entry))
                throw new InvalidOperationException("The host mutation is not registered.");
            entry.Closed = true;
            CompleteClosure(entry);
            return entry.Closure.Task;
        }
    }

    internal bool HasUnsettledSubmission
    {
        get
        {
            lock (_gate)
                return _entries.Values.Any(x => x.SubmissionStarted &&
                    (x.Settlement is null || !x.Settlement.IsCompleted));
        }
    }

    public async Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(DialogueCommitAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!Available(attempt)) return CommitEligibilityStatus.StaleOrFenced;
        }
        var authority = await AuthorizeAsync(attempt, cancellationToken);
        lock (_gate)
        {
            if (!Available(attempt)) return CommitEligibilityStatus.StaleOrFenced;
            if (cancellationToken.IsCancellationRequested) return CommitEligibilityStatus.Cancelled;
            return authority == CommitEligibilityStatus.Eligible ? CurrentOwnership(attempt) : authority;
        }
    }

    public async Task<CommitGuardResult> SubmitAsync(DialogueCommitAttempt attempt,
        Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submitAsync);
        lock (_gate)
        {
            if (!Available(attempt)) return new(CommitSubmissionStatus.StaleOrFenced, null);
        }
        var authority = await AuthorizeAsync(attempt, cancellationToken);
        Entry entry;
        Task<CommitSettlement> settlement;
        lock (_gate)
        {
            if (!TryFind(attempt, out entry!) || entry.Closed || entry.SubmissionStarted)
                return new(CommitSubmissionStatus.StaleOrFenced, null);
            if (cancellationToken.IsCancellationRequested)
                return new(CommitSubmissionStatus.CancelledBeforeSubmission, null);
            if (authority != CommitEligibilityStatus.Eligible) return new(SubmissionStatus(authority), null);
            var ownership = CurrentOwnership(attempt);
            if (ownership != CommitEligibilityStatus.Eligible) return new(SubmissionStatus(ownership), null);

            // Mark before invoking user code: even a synchronous throw could follow
            // the actual provider call. Never label that outcome pre-submission.
            entry.SubmissionStarted = true;
            try
            {
                settlement = submitAsync(cancellationToken)?.Settlement
                    ?? throw new InvalidOperationException("The submitted commit has no settlement task.");
                entry.Settlement = settlement;
            }
            catch (Exception)
            {
                // No actual task was handed back. Retain unresolved ownership and
                // prevent a second callback; no timeout can manufacture settlement.
                return new(CommitSubmissionStatus.Submitted, CommitSettlement.Indeterminate);
            }
        }

        _ = ObserveSettlementAsync(entry, settlement);
        try
        {
            var outcome = await settlement.WaitAsync(cancellationToken);
            return new(CommitSubmissionStatus.Submitted, Enum.IsDefined(outcome) ? outcome : CommitSettlement.Indeterminate);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(CommitSubmissionStatus.Submitted, null);
        }
        catch (Exception)
        {
            return new(CommitSubmissionStatus.Submitted, CommitSettlement.Indeterminate);
        }
    }

    private async Task<CommitEligibilityStatus> AuthorizeAsync(DialogueCommitAttempt attempt, CancellationToken token)
    {
        Task<CommitEligibilityStatus>? task = null;
        try
        {
            token.ThrowIfCancellationRequested();
            task = _freshAuthority(attempt, token);
            var status = await task.WaitAsync(token);
            return Enum.IsDefined(status) ? status : CommitEligibilityStatus.AuthorityUnavailable;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (task is not null) _ = ObserveAsync(task);
            return CommitEligibilityStatus.Cancelled;
        }
        catch (Exception) { return CommitEligibilityStatus.AuthorityUnavailable; }
    }

    private async Task ObserveSettlementAsync(Entry entry, Task task)
    {
        await ObserveAsync(task);
        lock (_gate) CompleteClosure(entry);
    }

    private static async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch (Exception) { } // Observing a fault does not certify a commit outcome.
    }

    private static void CompleteClosure(Entry entry)
    {
        if (entry.Closed && (!entry.SubmissionStarted || entry.Settlement is { IsCompleted: true }))
            entry.Closure.TrySetResult();
    }

    private bool Available(DialogueCommitAttempt attempt) =>
        TryFind(attempt, out var entry) && !entry.Closed && !entry.SubmissionStarted;

    private CommitEligibilityStatus CurrentOwnership(DialogueCommitAttempt attempt)
    {
        try
        {
            var status = _currentOwnership(attempt);
            return Enum.IsDefined(status) ? status : CommitEligibilityStatus.AuthorityUnavailable;
        }
        catch (Exception) { return CommitEligibilityStatus.AuthorityUnavailable; }
    }

    private bool TryFind(DialogueCommitAttempt attempt, out Entry entry)
    {
        entry = null!;
        return attempt is not null && _entries.TryGetValue(Key(attempt), out entry!) && entry.Binding == Binding(attempt);
    }

    // Internal exact-value comparison only, not a second persisted digest protocol.
    // Include subject authentication and the full protected capsule, not only its digest.
    private static string Binding(DialogueCommitAttempt attempt) => OctoberKnowledgePolicy.SerializeCanonical(attempt);
    private static (Guid, DialogueMutationKind, Guid) Key(DialogueCommitAttempt attempt) =>
        (attempt.Conversation.ConversationId, attempt.Kind, attempt.MutationId);

    private static CommitSubmissionStatus SubmissionStatus(CommitEligibilityStatus status) => status switch
    {
        CommitEligibilityStatus.StaleOrFenced => CommitSubmissionStatus.StaleOrFenced,
        CommitEligibilityStatus.Denied => CommitSubmissionStatus.Denied,
        CommitEligibilityStatus.Cancelled => CommitSubmissionStatus.CancelledBeforeSubmission,
        _ => CommitSubmissionStatus.AuthorityUnavailable
    };

    private sealed class Entry(string binding)
    {
        internal string Binding { get; } = binding;
        internal bool Closed { get; set; }
        internal bool SubmissionStarted { get; set; }
        internal Task<CommitSettlement>? Settlement { get; set; }
        internal TaskCompletionSource Closure { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

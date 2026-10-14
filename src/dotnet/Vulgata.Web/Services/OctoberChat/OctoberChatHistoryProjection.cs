using System.Collections.Immutable;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>Exact host handoff, not persisted state, admission or a reusable disclosure grant.</summary>
public sealed class OctoberChatHistorySnapshot
{
    internal OctoberChatHistorySnapshot(DialogueConversationIdentity conversation, ReadSelectionRequest readSelection,
        DialogueHistoryResult result, ImmutableArray<byte> projectionUtf8)
    {
        Conversation = conversation;
        ReadSelection = readSelection;
        Result = result;
        ProjectionUtf8 = projectionUtf8;
        TurnIds = result.Turns.Select(turn => turn.TurnId).ToImmutableArray();
    }

    [JsonIgnore]
    public DialogueConversationIdentity Conversation { get; }

    [JsonIgnore]
    public ReadSelectionRequest ReadSelection { get; }

    [JsonIgnore]
    public DialogueHistoryResult Result { get; }

    [JsonIgnore]
    public ImmutableArray<byte> ProjectionUtf8 { get; }

    [JsonIgnore]
    public ImmutableArray<Guid> TurnIds { get; }

    public string? ProjectionDigest => Result.ProjectionDigest;
    public bool ContextOmitted => Result.ContextOmitted;
}

/// <summary>Owner-selected saved display only. Never an inference-history encoding or selection.</summary>
public sealed class OctoberChatTranscriptSnapshot
{
    internal OctoberChatTranscriptSnapshot(DialogueConversationIdentity conversation, ReadSelectionRequest readSelection,
        DialogueTranscriptResult result)
    {
        Conversation = conversation;
        ReadSelection = readSelection;
        Result = result;
    }

    [JsonIgnore]
    public DialogueConversationIdentity Conversation { get; }

    [JsonIgnore]
    public ReadSelectionRequest ReadSelection { get; }

    [JsonIgnore]
    public DialogueTranscriptResult Result { get; }

    public bool ContextOmitted => Result.ContextOmitted;
}

/// <summary>
/// One read-only history OR saved-display attempt for an exact host attachment.
/// Preparation and handoff share one original ten-second lifetime. Business selects
/// whole turns; this consumer never repairs, truncates, summarizes or reconstructs lineage.
/// The trusted host checks attachment/token and fresh authority at actual use.
/// </summary>
public sealed class OctoberChatHistoryProjection : IAsyncDisposable
{
    private static readonly TimeSpan _duration = TimeSpan.FromSeconds(10);
    private static readonly UTF8Encoding _utf8 = new(false, true);
    private readonly object _gate = new();
    private readonly DialogueConversationIdentity _conversation;
    private readonly DialogueHistoryRequest _request;
    private readonly Guid _attachmentId;
    private readonly Func<Guid, bool> _isCurrentAttachment;
    private readonly IOctoberChatContextProvider _context;
    private readonly IOctoberDialogueStore _store;
    private readonly IOctoberDialoguePayloadCodec _codec;
    private readonly IOctoberKnowledgeDependencyAuthority _dependencies;
    private readonly TimeProvider _clock;
    private readonly CancellationTokenSource _closing = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HashSet<Task> _retained = [];
    private CancellationTokenSource? _deadline;
    private CancellationTokenSource? _lifetime;
    private OctoberChatHistorySnapshot? _history;
    private OctoberChatTranscriptSnapshot? _transcript;
    private ImmutableArray<KnowledgeDependencyHandle> _capsules = [];
    private ReadSelectionRequest? _readSelection;
    private long _startedAt;
    private int _mode;
    private int _activeStages;
    private bool _prepared;
    private bool _handedOff;
    private bool _contextLost;
    private bool _sealed;
    private int _disposed;

    public OctoberChatHistoryProjection(DialogueConversationIdentity conversation, DialogueHistoryRequest request,
        Guid attachmentId, Func<Guid, bool> isCurrentAttachment, IOctoberChatContextProvider context,
        IOctoberDialogueStore store, IOctoberDialoguePayloadCodec codec, IOctoberKnowledgeDependencyAuthority dependencies)
        : this(conversation, request, attachmentId, isCurrentAttachment, context, store, codec, dependencies, TimeProvider.System)
    {
    }

    internal OctoberChatHistoryProjection(DialogueConversationIdentity conversation, DialogueHistoryRequest request,
        Guid attachmentId, Func<Guid, bool> isCurrentAttachment, IOctoberChatContextProvider context,
        IOctoberDialogueStore store, IOctoberDialoguePayloadCodec codec, IOctoberKnowledgeDependencyAuthority dependencies,
        TimeProvider clock)
    {
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _attachmentId = attachmentId;
        _isCurrentAttachment = isCurrentAttachment ?? throw new ArgumentNullException(nameof(isCurrentAttachment));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task PrepareHistoryAsync(CancellationToken cancellationToken = default) => PrepareAsync(1, cancellationToken);
    public Task PrepareTranscriptAsync(CancellationToken cancellationToken = default) => PrepareAsync(2, cancellationToken);

    private async Task PrepareAsync(int mode, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_sealed || _mode != 0) return;
            _mode = mode;
            _activeStages++;
            _startedAt = _clock.GetTimestamp();
            _deadline = new(_duration, _clock);
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline.Token, _closing.Token);
        }

        try
        {
            CancellationToken token = _lifetime.Token;
            if (!ValidRequest()) return;
            ReadSelectionRequest? current = await CurrentAsync(token);
            if (current is null) return;
            if (mode == 1)
            {
                DialogueHistoryResult result = await AwaitAsync(() => _store.ReadHistoryAsync(_request, current.Subject, token), token);
                if (!ValidHistory(result, out ImmutableArray<byte> bytes) || !await SameContextAsync(current, token)) return;
                _capsules = result.Turns.Select(turn => turn.InferenceDependencies)
                    .Concat(result.Dependencies is { } aggregate ? new[] { aggregate } : []).Distinct().ToImmutableArray();
                _history = new(_conversation, current, result, bytes);
            }
            else
            {
                DialogueTranscriptResult result = await AwaitAsync(() => _store.ReadTranscriptAsync(_request, current.Subject, token), token);
                if (!ValidTranscript(result) || !await SameContextAsync(current, token)) return;
                _capsules = result.Turns.Where(turn => turn.InferenceDependencies is not null)
                    .Select(turn => turn.InferenceDependencies!).Distinct().ToImmutableArray();
                _transcript = new(_conversation, current, result);
            }

            _readSelection = current;
        }
        catch (Exception)
        {
            _history = null;
            _transcript = null;
        }
        finally
        {
            lock (_gate)
            {
                _prepared = true;
                _activeStages--;
                CompleteIfSettled();
            }
        }
    }

    public Task HandoffHistoryAsync(Func<OctoberChatHistorySnapshot?, CancellationToken, ValueTask> apply,
        Func<CancellationToken, ValueTask> suppressDependentDisplay, CancellationToken cancellationToken = default) =>
        HandoffAsync(1, token => apply(_history, token), suppressDependentDisplay, cancellationToken);

    public Task HandoffTranscriptAsync(Func<OctoberChatTranscriptSnapshot?, CancellationToken, ValueTask> apply,
        Func<CancellationToken, ValueTask> suppressDependentDisplay, CancellationToken cancellationToken = default) =>
        HandoffAsync(2, token => apply(_transcript, token), suppressDependentDisplay, cancellationToken);

    private async Task HandoffAsync(int mode, Func<CancellationToken, ValueTask> apply,
        Func<CancellationToken, ValueTask> suppressDependentDisplay, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(suppressDependentDisplay);
        lock (_gate)
        {
            if (_sealed || !_prepared || _mode != mode || _handedOff) return;
            _handedOff = true;
            _activeStages++;
        }

        try
        {
            if (_lifetime is null || _lifetime.IsCancellationRequested || _clock.GetElapsedTime(_startedAt) >= _duration) return;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            CancellationToken token = linked.Token;
            ReadSelectionRequest? current = await CurrentAsync(token);
            if (current is null) return;
            // The snapshot retains the exact selection used for its verified read.
            // A changed authentication binding requires a new read attempt.
            if (_readSelection is not null && current.Subject.AuthenticationBinding != _readSelection.Subject.AuthenticationBinding)
            {
                _contextLost = true;
                return;
            }

            KnowledgeDependencyStatus status = KnowledgeDependencyStatus.Succeeded;
            if (mode == 2)
            {
                foreach (var capsule in _capsules)
                {
                    status = await AwaitAsync(() => _dependencies.ReauthorizeAsync(current, [capsule], token), token);
                    if (status != KnowledgeDependencyStatus.Succeeded) break;
                }
            }
            else if (!_capsules.IsEmpty)
            {
                status = await AwaitAsync(() => _dependencies.ReauthorizeAsync(current, _capsules, token), token);
            }

            if (!await SameContextAsync(current, token)) return;
            if (status != KnowledgeDependencyStatus.Succeeded)
            {
                _history = null;
                _transcript = null;
                if (status is KnowledgeDependencyStatus.Denied or KnowledgeDependencyStatus.AuthorityUnavailable)
                {
                    await AwaitAsync(() => suppressDependentDisplay(token).AsTask(), token);
                    if (!await SameContextAsync(current, token)) return;
                }
            }

            if (!IsCurrent()) return;
            await AwaitAsync(() => apply(token).AsTask(), token);
        }
        catch (Exception)
        {
        }
        finally
        {
            Seal();
            lock (_gate)
            {
                _activeStages--;
                CompleteIfSettled();
            }
        }
    }

    public Task Completion => _completion.Task;

    public Task SealAndWaitForSettlementAsync()
    {
        Seal();
        return Completion;
    }

    public async ValueTask DisposeAsync()
    {
        await SealAndWaitForSettlementAsync();
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime?.Dispose();
        _deadline?.Dispose();
        _closing.Dispose();
    }

    private bool ValidRequest() => _conversation.ConversationId != Guid.Empty && _conversation.ContextId != Guid.Empty &&
                                   _request.ConversationId == _conversation.ConversationId && _request.ContextId == _conversation.ContextId &&
                                   _request.MaximumTurns is >= 1 and <= OctoberDialogueLimits.MaximumHistoryTurns &&
                                   _request.MaximumUtf8Bytes is >= 1 and <= OctoberDialogueLimits.MaximumHistoryProjectionUtf8Bytes &&
                                   (_request.BeforeSequenceExclusive is null or > 0) && Enum.IsDefined(_conversation.Mode) &&
                                   !_conversation.RepositoryScope.IsDefaultOrEmpty && _conversation.RepositoryScope.Length <= OctoberDialogueLimits.MaximumRepositories &&
                                   _conversation.RepositoryScope.All(id => id != Guid.Empty) &&
                                   _conversation.RepositoryScope.Distinct().Count() == _conversation.RepositoryScope.Length;

    private bool ValidHistory(DialogueHistoryResult result, out ImmutableArray<byte> bytes)
    {
        bytes = [];
        if (result is not { Status: DialogueStatus.Succeeded } || result.Turns.IsDefault || result.Turns.Length > _request.MaximumTurns ||
            result.Turns.Any(turn => turn is null || !ValidSequence(turn.Sequence) || turn.TurnId == Guid.Empty ||
                                     !ValidText(turn.Question, OctoberDialogueLimits.MaximumQuestionUtf8Bytes, true) ||
                                     !ValidText(turn.AnswerText, OctoberDialogueLimits.MaximumAnswerUtf8Bytes, true) ||
                                     !ValidReferences(turn.References) || !ValidCapsule(turn.InferenceDependencies)) ||
            !Ordered(result.Turns.Select(turn => (turn.TurnId, turn.Sequence))) ||
            result.Turns.IsEmpty && result.Dependencies is not null || !result.Turns.IsEmpty && !ValidCapsule(result.Dependencies) ||
            !ValidDigest(result.ProjectionDigest) || _codec.ComputeHistoryProjectionDigest(result.Turns) != result.ProjectionDigest) return false;
        bytes = _codec.EncodeHistoryProjectionUtf8(result.Turns);
        if (bytes.IsDefaultOrEmpty || bytes.Length > _request.MaximumUtf8Bytes || _codec.MeasureHistoryProjectionUtf8Bytes(result.Turns) != bytes.Length) return false;
        using JsonDocument json = JsonDocument.Parse(_utf8.GetString(bytes.AsSpan()));
        return json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() == result.Turns.Length;
    }

    private bool ValidTranscript(DialogueTranscriptResult result) => result is { Status: DialogueStatus.Succeeded } &&
                                                                     !result.Turns.IsDefault && result.Turns.Length <= _request.MaximumTurns && result.Turns.All(turn =>
                                                                         turn is not null && MatchesConversation(turn.Conversation) && turn.TurnId != Guid.Empty &&
                                                                         ValidSequence(turn.Sequence) &&
                                                                         Enum.IsDefined(turn.TerminalState) && Enum.IsDefined(turn.Reason) &&
                                                                         ValidText(turn.Question, OctoberDialogueLimits.MaximumQuestionUtf8Bytes) &&
                                                                         ValidText(turn.AnswerText, OctoberDialogueLimits.MaximumAnswerUtf8Bytes) &&
                                                                         ValidReferences(turn.References) &&
                                                                         (turn.InferenceDependencies is not null ? ValidCapsule(turn.InferenceDependencies)
                                                                             : EmptyOwnerClosure(turn))) &&
                                                                     Ordered(result.Turns.Select(turn => (turn.TurnId, turn.Sequence)));

    private static bool EmptyOwnerClosure(DialogueTerminalProjection turn) => turn.Question.Length == 0 && turn.AnswerText.Length == 0 &&
                                                                              turn.References.IsEmpty &&
                                                                              (turn.TerminalState is (DialogueTerminalState.Failed or DialogueTerminalState.Interrupted) &&
                                                                               turn.Reason is (DialogueTerminalReason.AuthorizationDenied
                                                                                   or DialogueTerminalReason.AuthorityUnavailable) ||
                                                                               turn.TerminalState == DialogueTerminalState.Interrupted &&
                                                                               turn.Reason == DialogueTerminalReason.OwnerLost);

    private bool ValidSequence(long sequence) => sequence > 0 && (_request.BeforeSequenceExclusive is null || sequence < _request.BeforeSequenceExclusive);

    private static bool Ordered(IEnumerable<(Guid Id, long Sequence)> turns)
    {
        var ids = new HashSet<Guid>();
        long previous = 0;
        foreach (var turn in turns)
        {
            if (!ids.Add(turn.Id) || turn.Sequence <= previous) return false;
            previous = turn.Sequence;
        }

        return true;
    }

    private bool ValidReferences(ImmutableArray<OctoberChatReference> references) => !references.IsDefault &&
                                                                                     references.Length <= OctoberDialogueLimits.MaximumReferences && references.All(reference =>
                                                                                         reference is not null &&
                                                                                         reference.Document is { } document &&
                                                                                         document.BatchId == _conversation.Publication.Knowledge.BatchId &&
                                                                                         document.PublicationId == _conversation.Publication.PublicationId &&
                                                                                         document.LogicalDocumentId != Guid.Empty && document.DocumentVersionId != Guid.Empty &&
                                                                                         (document.SectionId is null || ValidText(document.SectionId,
                                                                                             OctoberDialogueLimits.MaximumSectionIdUtf8Bytes, true)) &&
                                                                                         reference.SuccessfulChunks > 0 && reference.ContentBytes >= 0);

    private static bool ValidText(string? text, int bytes, bool nonblank = false) => text is not null &&
                                                                                     (!nonblank || !string.IsNullOrWhiteSpace(text)) && _utf8.GetByteCount(text) <= bytes;

    private static bool ValidDigest(string? digest) => digest is { Length: 64 } && digest.All(Uri.IsHexDigit);

    private static bool ValidCapsule(KnowledgeDependencyHandle? capsule) => capsule is not null && capsule.SetId != Guid.Empty &&
                                                                            ValidDigest(capsule.BindingDigest) && ValidText(capsule.ProtectedPayload,
                                                                                OctoberKnowledgeDisclosureLimits.MaximumProtectedPayloadUtf8Bytes, true);

    private bool MatchesConversation(DialogueConversationIdentity identity) => identity is not null && identity.ConversationId == _conversation.ConversationId &&
                                                                               identity.ContextId == _conversation.ContextId &&
                                                                               identity.OwnerSubjectId == _conversation.OwnerSubjectId &&
                                                                               identity.Publication == _conversation.Publication && identity.Mode == _conversation.Mode &&
                                                                               !identity.RepositoryScope.IsDefault &&
                                                                               identity.RepositoryScope.Order().SequenceEqual(_conversation.RepositoryScope.Order());

    private bool MatchesContext(ReadSelectionRequest? current) => current is not null && current.Subject is not null &&
                                                                  current.Subject.SubjectId == _conversation.OwnerSubjectId &&
                                                                  !string.IsNullOrWhiteSpace(current.Subject.AuthenticationBinding) &&
                                                                  current.Purpose == KnowledgePurpose.Chat && current.Publication == _conversation.Publication &&
                                                                  current.Mode == _conversation.Mode &&
                                                                  !current.RepositoryScope.IsDefault &&
                                                                  current.RepositoryScope.Order().SequenceEqual(_conversation.RepositoryScope.Order());

    private async Task<ReadSelectionRequest?> CurrentAsync(CancellationToken token)
    {
        if (!IsCurrent()) return null;
        ReadSelectionRequest? current = await AwaitAsync(() => _context.GetCurrentAsync(token), token);
        if (!MatchesContext(current) || !IsCurrent())
        {
            _contextLost = true;
            return null;
        }

        return current;
    }

    private async Task<bool> SameContextAsync(ReadSelectionRequest before, CancellationToken token)
    {
        ReadSelectionRequest? after = await CurrentAsync(token);
        if (after is null || after.Subject.AuthenticationBinding != before.Subject.AuthenticationBinding)
        {
            _contextLost = true;
            return false;
        }

        return true;
    }

    private bool IsCurrent()
    {
        try
        {
            lock (_gate)
                if (_sealed || _contextLost)
                    return false;
            if (_attachmentId != Guid.Empty && _isCurrentAttachment(_attachmentId))
            {
                lock (_gate) return !_sealed && !_contextLost;
            }
        }
        catch (Exception)
        {
        }

        _contextLost = true;
        return false;
    }

    private Task<T> Start<T>(Func<Task<T>> operation, CancellationToken token)
    {
        lock (_gate)
        {
            CheckStart(token);
            Task<T> task = operation();
            Retain(task);
            return task;
        }
    }

    private Task Start(Func<Task> operation, CancellationToken token)
    {
        lock (_gate)
        {
            CheckStart(token);
            Task task = operation();
            Retain(task);
            return task;
        }
    }

    private void CheckStart(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_sealed || _clock.GetElapsedTime(_startedAt) >= _duration) throw new OperationCanceledException(token);
    }

    private async Task<T> AwaitAsync<T>(Func<Task<T>> operation, CancellationToken token)
    {
        T value = await Start(operation, token).WaitAsync(token);
        lock (_gate) CheckStart(token);
        return value;
    }

    private async Task AwaitAsync(Func<Task> operation, CancellationToken token)
    {
        await Start(operation, token).WaitAsync(token);
        lock (_gate) CheckStart(token);
    }

    private void Retain(Task task)
    {
        lock (_gate) _retained.Add(task);
        _ = ObserveAsync(task);
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception)
        {
        }
        finally
        {
            lock (_gate)
            {
                _retained.Remove(task);
                CompleteIfSettled();
            }
        }
    }

    private void Seal()
    {
        lock (_gate)
        {
            if (_sealed) return;
            _sealed = true;
            Retain(Task.Run(() => _closing.CancelAsync()));
            CompleteIfSettled();
        }
    }

    private void CompleteIfSettled()
    {
        if (_sealed && _activeStages == 0 && _retained.Count == 0) _completion.TrySetResult();
    }
}

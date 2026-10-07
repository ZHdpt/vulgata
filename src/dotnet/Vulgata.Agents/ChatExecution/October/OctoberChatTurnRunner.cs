using System.Collections.Immutable;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.ChatExecution.October;

/// <summary>Host-only generation facts. Never a durable receipt, confirmed cleanup or authority to release a turn slot.</summary>
public sealed record OctoberChatGenerationResult(DialogueTerminalState TerminalState, DialogueTerminalReason Reason,
    DialogueProviderFinish ProviderFinish, string AnswerText,
    [property: JsonIgnore] ImmutableArray<OctoberChatReference> References,
    [property: JsonIgnore] ImmutableArray<DialogueConsumedTarget> ConsumedReads,
    [property: JsonIgnore] KnowledgeDependencyHandle? InferenceDependencies, DateTimeOffset GenerationEndedAtUtc,
    Guid SaveOperationId);

/// <summary>
/// Application-only admitted-turn entry. Host registration, original execution ownership,
/// provider admission/settlement, dialogue transitions and persistence remain owner prerequisites.
/// No browser activation or alternative model/tool loop is supplied by this type.
/// </summary>
public sealed class OctoberChatTurnRunner
{
    private static readonly UTF8Encoding _utf8 = new(false, true);
    private static readonly DateTimeOffset _capacityTimestamp = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1234567);
    private static readonly string _measurementDigest = new('0', 64);
    private readonly DialogueAdmission _admission;
    private readonly OctoberChatBudget _budget;
    private readonly IOctoberDialoguePayloadCodec _codec;
    private readonly IOctoberDialogueExecutionAuthority _executionAuthority;
    private readonly OctoberChatDisclosureSession _disclosure;
    private readonly OctoberChatAgent _agent;
    private readonly string _prompt;
    private readonly string? _history;
    private readonly Guid _saveId;
    private string _answer = string.Empty;
    private int _used;
    private int _active;
    private bool _arraysMeasured;
    private ImmutableArray<DialogueConsumedTarget> _measuredConsumed;

    private OctoberChatTurnRunner(DialogueAdmission admission, IChatClient client, OctoberChatBudget budget,
        OctoberChatTurnBinding binding, string prompt, string? history, Guid saveId,
        IOctoberKnowledgeDisclosedReader reader, IOctoberKnowledgeDependencyAuthority dependencies,
        IOctoberDialoguePayloadCodec codec, IOctoberDialogueExecutionAuthority executionAuthority,
        Func<CancellationToken, Task<ReadSelectionRequest?>> freshSelection)
    {
        _admission = admission;
        _budget = budget;
        _codec = codec;
        _executionAuthority = executionAuthority;
        _prompt = prompt;
        _history = history;
        _saveId = saveId;
        _disclosure = new(binding, budget, dependencies, freshSelection, admission.Request.InitialDependencies, CanRetainInput);
        _agent = new(client, new OctoberWikiTools(reader, binding, budget, _disclosure), budget);
    }

    public static async Task<OctoberChatTurnRunner?> CreateAsync(DialogueBeginResult admission, IChatClient plainClient,
        OctoberChatBudget originalBudget, ReadSelection selection, DialogueHistoryResult history,
        string selectedProfileId, string selectedProfileRevision, IOctoberKnowledgeDisclosedReader reader,
        IOctoberKnowledgeDependencyAuthority dependencies, IOctoberDialoguePayloadCodec codec,
        IOctoberDialogueExecutionAuthority executionAuthority,
        Func<CancellationToken, Task<ReadSelectionRequest?>> freshSelection, Guid terminalSaveOperationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (admission is not { Status: DialogueStatus.Succeeded, MayDispatch: true, Admission: { } granted } ||
                plainClient is null || originalBudget is null || reader is null || dependencies is null || codec is null ||
                executionAuthority is null || freshSelection is null || terminalSaveOperationId == Guid.Empty ||
                selection is null || string.IsNullOrWhiteSpace(selection.Token) || originalBudget.IsStopped ||
                granted.State != DialogueTurnState.Admitted || granted.TurnId == Guid.Empty || granted.Sequence <= 0 || granted.Fence <= 0)
                return null;
            DialogueBeginRequest request = granted.Request;
            DialogueConversationIdentity conversation = granted.Conversation;
            if (request is null || conversation is null || conversation.ConversationId == Guid.Empty || conversation.ContextId == Guid.Empty ||
                conversation.RepositoryScope.IsDefaultOrEmpty || conversation.RepositoryScope.Length > OctoberDialogueLimits.MaximumRepositories ||
                conversation.RepositoryScope.Any(repository => repository == Guid.Empty) ||
                conversation.RepositoryScope.Distinct().Count() != conversation.RepositoryScope.Length ||
                string.IsNullOrWhiteSpace(conversation.OwnerSubjectId) || _utf8.GetByteCount(conversation.OwnerSubjectId) > OctoberDialogueLimits.MaximumOwnerSubjectUtf8Bytes ||
                request.ConversationId != conversation.ConversationId || request.ContextId != conversation.ContextId ||
                request.ClientRequestKey == Guid.Empty || request.Execution is null || request.Execution.OwnerGeneration == Guid.Empty || request.Execution.AttemptId == Guid.Empty ||
                request.Limits != originalBudget.Limits || string.IsNullOrWhiteSpace(selectedProfileId) || string.IsNullOrWhiteSpace(selectedProfileRevision) ||
                request.ProviderProfileId != selectedProfileId || request.ProviderProfileRevision != selectedProfileRevision ||
                _utf8.GetByteCount(selectedProfileId) > OctoberDialogueLimits.MaximumProviderIdentityUtf8Bytes ||
                _utf8.GetByteCount(selectedProfileRevision) > OctoberDialogueLimits.MaximumProviderIdentityUtf8Bytes ||
                !OctoberChatDisclosureSession.ValidHandle(request.InitialDependencies) || !ValidDigest(request.RequestDigest) ||
                !ValidDigest(request.PromptDigest) || !ValidDigest(request.HistoryProjectionDigest) ||
                granted.AdmittedAtUtc.Offset != TimeSpan.Zero || granted.DeadlineUtc.Offset != TimeSpan.Zero ||
                granted.AdmittedAtUtc > originalBudget.CurrentUtc || granted.DeadlineUtc <= granted.AdmittedAtUtc ||
                granted.DeadlineUtc > granted.AdmittedAtUtc.Add(request.Limits.Duration) ||
                !originalBudget.ConstrainToDeadline(granted.DeadlineUtc)) return null;

            using var deadline = originalBudget.CreateDeadlineCancellation();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, originalBudget.OriginalCancellationToken, deadline.Token);
            CancellationToken token = linked.Token;
            token.ThrowIfCancellationRequested();
            if (request.Limits.QuestionCharacters > OctoberDialogueLimits.MaximumQuestionScalars ||
                request.Limits.OutputBytes > OctoberDialogueLimits.MaximumAnswerUtf8Bytes ||
                request.Limits.HistoryTurns > OctoberDialogueLimits.MaximumHistoryTurns || request.Limits.HistoryBytes > OctoberDialogueLimits.MaximumHistoryProjectionUtf8Bytes ||
                request.Limits.PageSize > KnowledgeContract.MaximumPageSize || request.Limits.ContentBytes > KnowledgeContract.MaximumContentBytes ||
                string.IsNullOrWhiteSpace(request.Question) || _utf8.GetByteCount(request.Question) > OctoberDialogueLimits.MaximumQuestionUtf8Bytes ||
                request.Question.EnumerateRunes().Count() > request.Limits.QuestionCharacters ||
                codec.ComputeBeginDigest(conversation, request) != request.RequestDigest) return null;
            int beginBytes = codec.MeasureBeginEnvelopeUtf8Bytes(request);
            if (beginBytes < 0 || beginBytes > OctoberDialogueLimits.MaximumBeginEnvelopeUtf8Bytes) return null;
            if (history is not { Status: DialogueStatus.Succeeded } || history.Turns.IsDefault || request.HistoryTurnIds.IsDefault ||
                history.Turns.Length > Math.Min(request.Limits.HistoryTurns, OctoberDialogueLimits.MaximumHistoryTurns) ||
                !history.Turns.Select(turn => turn.TurnId).SequenceEqual(request.HistoryTurnIds) ||
                history.Turns.Any(turn => turn.TurnId == Guid.Empty || turn.Sequence <= 0 || turn.Sequence >= granted.Sequence ||
                    !OctoberChatDisclosureSession.ValidHandle(turn.InferenceDependencies)) ||
                history.Turns.Select(turn => turn.TurnId).Distinct().Count() != history.Turns.Length ||
                !history.Turns.Select(turn => turn.Sequence).SequenceEqual(history.Turns.Select(turn => turn.Sequence).Distinct().Order()) ||
                history.ProjectionDigest != request.HistoryProjectionDigest ||
                codec.ComputeHistoryProjectionDigest(history.Turns) != request.HistoryProjectionDigest ||
                !history.Turns.IsEmpty && !OctoberChatDisclosureSession.ValidHandle(history.Dependencies)) return null;
            ImmutableArray<byte> historyBytes = codec.EncodeHistoryProjectionUtf8(history.Turns);
            if (historyBytes.IsDefaultOrEmpty || historyBytes.Length > Math.Min(request.Limits.HistoryBytes, OctoberDialogueLimits.MaximumHistoryProjectionUtf8Bytes) ||
                codec.MeasureHistoryProjectionUtf8Bytes(history.Turns) != historyBytes.Length) return null;
            string historyText = _utf8.GetString(historyBytes.AsSpan());
            using (JsonDocument json = JsonDocument.Parse(historyText))
                if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() != history.Turns.Length) return null;
            string? prompt = await new OctoberChatPromptCatalog().LoadAsync(conversation.Mode, token).WaitAsync(token);
            if (prompt is null || OctoberKnowledgePolicy.Hash(prompt) != request.PromptDigest) return null;
            ReadSelectionRequest? current = await freshSelection(token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (!Matches(conversation, current)) return null;
            if (await executionAuthority.VerifyAdmissionAsync(request, token).WaitAsync(token) != DialogueExecutionStatus.Verified) return null;
            token.ThrowIfCancellationRequested();
            var binding = new OctoberChatTurnBinding(conversation.ConversationId, conversation.ContextId, granted.TurnId, current!, selection);
            var runner = new OctoberChatTurnRunner(granted, plainClient, originalBudget, binding, prompt,
                history.Turns.IsEmpty ? null : historyText, terminalSaveOperationId, reader, dependencies, codec, executionAuthority, freshSelection);
            if (!runner.CanRetainInput(request.InitialDependencies, [])) return null;
            if (history.Dependencies is not null) await runner._disclosure.AdmitPageAsync(history.Dependencies, null, token);
            await runner._disclosure.ReauthorizeAsync(token);
            token.ThrowIfCancellationRequested();
            return runner;
        }
        catch (Exception) { return null; } // No dispatch, fallback, receipt or admission-absence inference.
    }

    public async Task<OctoberChatGenerationResult> RunAsync(Func<string, CancellationToken, ValueTask> authorizedText,
        CancellationToken cancellationToken = default, Action<OctoberChatToolProgress>? onToolProgress = null)
    {
        ArgumentNullException.ThrowIfNull(authorizedText);
        if (Interlocked.Exchange(ref _used, 1) != 0) throw new InvalidOperationException("This admitted Chat execution has already started.");
        Volatile.Write(ref _active, 1);
        OctoberChatProviderResult generated;
        try
        {
            if (cancellationToken.IsCancellationRequested || _budget.OriginalCancellationToken.IsCancellationRequested || _budget.DeadlineExpired)
                generated = new(OctoberChatProviderStatus.Cancelled, string.Empty) { ProviderFinish = DialogueProviderFinish.Cancelled };
            else
                generated = await _agent.RunAuthorizedAsync(_admission.Request.Question, _prompt, async (delta, token) =>
                {
                    string candidate = _answer + delta;
                    if (!CanRetain(_disclosure.InferenceDependencies, _disclosure.ConsumedReads, candidate))
                        throw _disclosure.Reject(KnowledgeDependencyStatus.BudgetExceeded);
                    token.ThrowIfCancellationRequested();
                    await authorizedText(delta, token).AsTask().WaitAsync(token);
                    _answer = candidate;
                }, cancellationToken, onToolProgress, _history, VerifyExecutionAsync);
        }
        finally { Volatile.Write(ref _active, 0); }

        bool callerCancelled = cancellationToken.IsCancellationRequested || _budget.OriginalCancellationToken.IsCancellationRequested;
        DialogueTerminalState state;
        DialogueTerminalReason reason;
        if (_disclosure.FailureStatus is KnowledgeDependencyStatus.Denied or KnowledgeDependencyStatus.AuthorityUnavailable or
            KnowledgeDependencyStatus.UnknownLineage or KnowledgeDependencyStatus.InvalidProof)
        {
            reason = _disclosure.FailureStatus == KnowledgeDependencyStatus.Denied ? DialogueTerminalReason.AuthorizationDenied : DialogueTerminalReason.AuthorityUnavailable;
            return new(DialogueTerminalState.Failed, reason, generated.ProviderFinish, string.Empty, [], [], null, _budget.CurrentUtc, _saveId);
        }
        if (generated.Status == OctoberChatProviderStatus.Completed)
        {
            state = string.IsNullOrWhiteSpace(generated.AnswerText) ? DialogueTerminalState.Failed : DialogueTerminalState.Completed;
            reason = state == DialogueTerminalState.Completed ? DialogueTerminalReason.None : DialogueTerminalReason.ExecutionFailure;
        }
        else if (generated.Status is OctoberChatProviderStatus.Cancelled or OctoberChatProviderStatus.BudgetExceeded)
        {
            state = DialogueTerminalState.Interrupted;
            reason = callerCancelled ? DialogueTerminalReason.UserCancelled : _budget.DeadlineExpired
                ? DialogueTerminalReason.DeadlineExceeded : DialogueTerminalReason.BudgetExceeded;
        }
        else if (generated.Status == OctoberChatProviderStatus.Interrupted)
        {
            state = DialogueTerminalState.Interrupted;
            reason = DialogueTerminalReason.ProviderInterrupted;
        }
        else
        {
            state = DialogueTerminalState.Failed;
            reason = generated.ProviderFinish is DialogueProviderFinish.Invalid or DialogueProviderFinish.Truncated or DialogueProviderFinish.UnfinishedTools
                ? DialogueTerminalReason.ProtocolFailure : DialogueTerminalReason.ExecutionFailure;
        }
        return new(state, reason, generated.ProviderFinish, generated.AnswerText, _disclosure.ReportedReferences,
            _disclosure.ConsumedReads, _disclosure.InferenceDependencies, _budget.CurrentUtc, _saveId);
    }

    /// <summary>Host calls after permit admission immediately before physical send; never a substitute for host execution/profile checks.</summary>
    public async Task ReauthorizeBeforeSendAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _active) != 1) throw new InvalidOperationException("No admitted Chat execution is active.");
        await VerifyExecutionAsync(cancellationToken);
        await _disclosure.ReauthorizeAsync(cancellationToken);
        if (Volatile.Read(ref _active) != 1) throw new InvalidOperationException("The admitted Chat execution has closed.");
    }

    private async Task VerifyExecutionAsync(CancellationToken cancellationToken)
    {
        using var deadline = _budget.CreateDeadlineCancellation();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _budget.OriginalCancellationToken, deadline.Token);
        if (Volatile.Read(ref _active) != 1 || _budget.IsStopped) throw new InvalidOperationException("No admitted Chat execution is active.");
        DialogueExecutionStatus status;
        try { status = await _executionAuthority.VerifyAdmissionAsync(_admission.Request, linked.Token).WaitAsync(linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw; }
        catch (Exception) { throw _disclosure.Reject(KnowledgeDependencyStatus.AuthorityUnavailable); }
        linked.Token.ThrowIfCancellationRequested();
        if (status != DialogueExecutionStatus.Verified) throw _disclosure.Reject(status == DialogueExecutionStatus.NotVerified
            ? KnowledgeDependencyStatus.Denied : KnowledgeDependencyStatus.AuthorityUnavailable);
        if (Volatile.Read(ref _active) != 1) throw new InvalidOperationException("The admitted Chat execution has closed.");
    }

    private bool CanRetainInput(KnowledgeDependencyHandle dependencies, ImmutableArray<DialogueConsumedTarget> consumed) =>
        CanRetain(dependencies, consumed, _answer);

    private bool CanRetain(KnowledgeDependencyHandle dependencies, ImmutableArray<DialogueConsumedTarget> consumed, string answer)
    {
        try
        {
            if (consumed.IsDefault || consumed.Length > OctoberDialogueLimits.MaximumConsumedTargets) return false;
            var references = consumed.Select(value => value.Reference).ToImmutableArray();
            if (references.Length > OctoberDialogueLimits.MaximumReferences) return false;
            // A measurement-only concrete maximal encoding: these numeric enum widths
            // and seven fractional digits cover schema 1's possible terminal labels
            // and timestamps. The digest is fixed-width, not a claimed outcome hash.
            // This projection is never submitted to persistence as an actual outcome.
            var candidate = new DialogueTerminalSaveRequest(new(_admission.Conversation.ConversationId,
                _admission.Conversation.ContextId, _admission.TurnId, _admission.Request.Execution, _admission.Fence),
                _saveId, _measurementDigest, DialogueTerminalState.Failed, DialogueTerminalReason.ExecutionFailure,
                DialogueProviderFinish.Unknown, answer, references, consumed, dependencies, _admission.Request.HistoryTurnIds, _capacityTimestamp);
            int full = _codec.MeasureTerminalEnvelopeUtf8Bytes(candidate);
            if (full < 0 || full > OctoberDialogueLimits.MaximumTerminalEnvelopeUtf8Bytes) return false;
            if (!_arraysMeasured || !_measuredConsumed.Equals(consumed))
            {
                int noReferences = _codec.MeasureTerminalEnvelopeUtf8Bytes(candidate with { References = [] });
                int neither = _codec.MeasureTerminalEnvelopeUtf8Bytes(candidate with { References = [], ConsumedReads = [] });
                long referenceBytes = (long)full - noReferences + 2;
                long consumedBytes = (long)noReferences - neither + 2;
                if (noReferences < 0 || neither < 0 || referenceBytes < 2 || consumedBytes < 2 ||
                    referenceBytes > OctoberDialogueLimits.MaximumReferenceArrayUtf8Bytes || consumedBytes > OctoberDialogueLimits.MaximumConsumedArrayUtf8Bytes)
                    return false;
                _measuredConsumed = consumed;
                _arraysMeasured = true;
            }
            return true;
        }
        catch (Exception) { return false; }
    }

    private static bool ValidDigest(string? digest) => digest is { Length: 64 } && digest.All(value => char.IsAsciiDigit(value) || value is >= 'A' and <= 'F');

    private static bool Matches(DialogueConversationIdentity conversation, ReadSelectionRequest? selection) =>
        selection is not null && selection.Subject is not null && selection.Subject.SubjectId == conversation.OwnerSubjectId &&
        !string.IsNullOrWhiteSpace(selection.Subject.AuthenticationBinding) && selection.Publication == conversation.Publication &&
        selection.Mode == conversation.Mode && selection.Purpose == KnowledgePurpose.Chat && !selection.RepositoryScope.IsDefault &&
        !conversation.RepositoryScope.IsDefault && selection.RepositoryScope.Length == conversation.RepositoryScope.Length &&
        selection.RepositoryScope.Distinct().Count() == selection.RepositoryScope.Length && selection.RepositoryScope.ToHashSet().SetEquals(conversation.RepositoryScope);
}

using System.Collections.Immutable;
using System.Text;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberChat;

namespace Vulgata.Web.ViewModels.OctoberChat;

/// <summary>Controlled presentation only; upstream disclosure authorization and durable outcomes remain prerequisites.</summary>
public sealed class OctoberChatTurnViewModel(OctoberChatTurnBinding binding)
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly object _gate = new();
    private readonly OctoberChatEventSequence _sequence = new(binding);
    private readonly HashSet<string> _activeTools = new(StringComparer.Ordinal);
    private bool _started;
    private bool _suppressed;

    public OctoberChatTurnBinding Binding { get; } = binding;
    public string ProgressText { get; private set; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public string AnswerText { get; private set; } = string.Empty;
    public string? StatusMessage { get; private set; }
    public string? ActivityMessage { get; private set; }
    public ImmutableArray<OctoberChatReference> References { get; private set; } = [];
    public bool IsTerminal { get; private set; }
    public bool IsSaveUncertain { get; private set; }
    public event Action? StateChanged;

    /// <summary>New display model from a currently authorized owner snapshot; no stream replay, admission or retry authority.</summary>
    public static OctoberChatTurnViewModel? CreateSaved(OctoberChatTurnBinding binding, DialogueTerminalProjection saved)
    {
        try
        {
            if (binding is null || saved is null || saved.Conversation is not { } identity ||
                binding.ConversationId == Guid.Empty || identity.ConversationId != binding.ConversationId ||
                identity.ContextId != binding.ContextId || binding.ContextId == Guid.Empty || saved.TurnId != binding.TurnId || saved.TurnId == Guid.Empty ||
                saved.Sequence <= 0 || binding.Request.Purpose != KnowledgePurpose.Chat ||
                binding.Request.Subject.SubjectId != identity.OwnerSubjectId || binding.Request.Publication != identity.Publication ||
                binding.Request.Mode != identity.Mode || binding.Request.RepositoryScope.IsDefaultOrEmpty || identity.RepositoryScope.IsDefault ||
                !binding.Request.RepositoryScope.Order().SequenceEqual(identity.RepositoryScope.Order()) ||
                !Enum.IsDefined(saved.TerminalState) || !Enum.IsDefined(saved.Reason) || saved.Question is null || saved.AnswerText is null ||
                _strictUtf8.GetByteCount(saved.Question) > OctoberDialogueLimits.MaximumQuestionUtf8Bytes ||
                _strictUtf8.GetByteCount(saved.AnswerText) > OctoberDialogueLimits.MaximumAnswerUtf8Bytes) return null;
            var model = new OctoberChatTurnViewModel(binding);
            if (!model.ValidReferences(saved.References) || saved.References.Any(reference => reference.Document.SectionId is { } section &&
                (string.IsNullOrWhiteSpace(section) || _strictUtf8.GetByteCount(section) > OctoberDialogueLimits.MaximumSectionIdUtf8Bytes))) return null;
            if (saved.InferenceDependencies is { } capsule)
            {
                if (capsule.SetId == Guid.Empty || capsule.BindingDigest is not { Length: 64 } || !capsule.BindingDigest.All(Uri.IsHexDigit) ||
                    string.IsNullOrWhiteSpace(capsule.ProtectedPayload) ||
                    _strictUtf8.GetByteCount(capsule.ProtectedPayload) > OctoberKnowledgeDisclosureLimits.MaximumProtectedPayloadUtf8Bytes) return null;
            }
            else if (saved.Question.Length != 0 || saved.AnswerText.Length != 0 || !saved.References.IsEmpty ||
                !(saved.TerminalState is (DialogueTerminalState.Failed or DialogueTerminalState.Interrupted) &&
                    saved.Reason is (DialogueTerminalReason.AuthorizationDenied or DialogueTerminalReason.AuthorityUnavailable) ||
                    saved.TerminalState == DialogueTerminalState.Interrupted && saved.Reason == DialogueTerminalReason.OwnerLost)) return null;

            model._started = true;
            model.AnswerText = saved.AnswerText;
            model.References = saved.References;
            model.IsTerminal = true;
            model.IsCompleted = saved.TerminalState == DialogueTerminalState.Completed;
            model.StatusMessage = SavedStatus(saved.TerminalState switch
            {
                DialogueTerminalState.Completed => OctoberChatDisplayOutcome.Completed,
                DialogueTerminalState.Interrupted => OctoberChatDisplayOutcome.Interrupted,
                _ => OctoberChatDisplayOutcome.Failed
            }, saved.AnswerText);
            return model;
        }
        catch (Exception) { return null; }
    }

    /// <summary>Trusted current-attachment suppression, including a saved display; never a new stream or terminal outcome.</summary>
    public bool TrySuppressDisclosure(OctoberChatTurnBinding expectedBinding)
    {
        lock (_gate)
        {
            if (expectedBinding is null || expectedBinding.ConversationId != Binding.ConversationId ||
                expectedBinding.ContextId != Binding.ContextId || expectedBinding.TurnId != Binding.TurnId ||
                expectedBinding.Request.Subject.SubjectId != Binding.Request.Subject.SubjectId ||
                expectedBinding.Request.Publication != Binding.Request.Publication || expectedBinding.Request.Mode != Binding.Request.Mode ||
                expectedBinding.Request.Purpose != Binding.Request.Purpose || expectedBinding.Request.RepositoryScope.IsDefault ||
                Binding.Request.RepositoryScope.IsDefault ||
                !expectedBinding.Request.RepositoryScope.Order().SequenceEqual(Binding.Request.RepositoryScope.Order())) return false;
            SuppressDisclosure();
        }
        StateChanged?.Invoke();
        return true;
    }

    public bool Apply(OctoberChatEvent item)
    {
        lock (_gate)
        {
            // Validate lifecycle/payload before consuming its sequence. An invalid high
            // sequence must not suppress a subsequent valid settlement projection.
            if (IsTerminal || !_started && item.Kind != OctoberChatEventKind.TurnStarted ||
                _started && item.Kind == OctoberChatEventKind.TurnStarted ||
                IsSaveUncertain && item.Kind is not (OctoberChatEventKind.TurnSaved or OctoberChatEventKind.DisclosureSuppressed) ||
                _suppressed && (item.Kind != OctoberChatEventKind.TurnSaved || item.Text != string.Empty ||
                    item.References.IsDefault || !item.References.IsEmpty ||
                    item.SavedOutcome is not (OctoberChatDisplayOutcome.Failed or OctoberChatDisplayOutcome.Interrupted)) ||
                !ValidPayload(item) ||
                !_sequence.TryAccept(item))
            {
                return false;
            }

            switch (item.Kind)
            {
                case OctoberChatEventKind.TurnStarted:
                    _started = true;
                    StatusMessage = "正在处理问题…";
                    break;
                case OctoberChatEventKind.AnswerDelta:
                    AnswerText += item.Text;
                    break;
                case OctoberChatEventKind.ToolStarted:
                    if (!string.IsNullOrEmpty(AnswerText))
                    {
                        ProgressText += (ProgressText.Length == 0 ? string.Empty : "\n\n") + AnswerText;
                        AnswerText = string.Empty;
                    }
                    _activeTools.Add(item.ToolCallId!);
                    ActivityMessage = "正在查阅知识…";
                    break;
                case OctoberChatEventKind.ToolCompleted:
                    _activeTools.Remove(item.ToolCallId!);
                    ActivityMessage = _activeTools.Count == 0 ? "本轮查阅完成，正在继续处理…" : "正在查阅知识…";
                    break;
                case OctoberChatEventKind.ToolFailed:
                    _activeTools.Remove(item.ToolCallId!);
                    ActivityMessage = "部分知识暂时无法读取。";
                    break;
                case OctoberChatEventKind.ReferencesUpdated:
                    References = item.References;
                    break;
                case OctoberChatEventKind.TurnSaveUncertain:
                    IsSaveUncertain = true;
                    ActivityMessage = null;
                    _activeTools.Clear();
                    StatusMessage = "保存状态待确认，暂时无法继续提问。";
                    break;
                case OctoberChatEventKind.DisclosureSuppressed:
                    SuppressDisclosure();
                    break;
                case OctoberChatEventKind.TurnSaved:
                    // An explicit canonical terminal projection replaces provisional
                    // output, including the owner's sanitized empty closure.
                    if (item.Text is not null)
                    {
                        AnswerText = item.Text;
                        References = item.References;
                    }
                    IsTerminal = true;
                    IsCompleted = item.SavedOutcome == OctoberChatDisplayOutcome.Completed;
                    if (item.Text == string.Empty) ProgressText = string.Empty;
                    IsSaveUncertain = false;
                    ActivityMessage = null;
                    _activeTools.Clear();
                    StatusMessage = SavedStatus(item.SavedOutcome!.Value, AnswerText);
                    break;
            }
        }

        StateChanged?.Invoke();
        return true;
    }

    private static string SavedStatus(OctoberChatDisplayOutcome outcome, string answer) => outcome switch
    {
        OctoberChatDisplayOutcome.Completed => "回答已保存。",
        OctoberChatDisplayOutcome.Interrupted => string.IsNullOrWhiteSpace(answer)
            ? "回答已中断，尚未收到可用内容。" : "回答已中断，已保留部分内容。",
        _ => string.IsNullOrWhiteSpace(answer)
            ? "回答失败，尚未收到可用内容。" : "回答失败，已保留部分内容。"
    };

    private void SuppressDisclosure()
    {
        _suppressed = true;
        ProgressText = string.Empty;
        AnswerText = string.Empty;
        References = [];
        ActivityMessage = null;
        _activeTools.Clear();
        StatusMessage = "当前内容暂不可用。";
        // Preserve durable terminal/uncertain flags and the original stream sequence.
    }

    private bool ValidPayload(OctoberChatEvent item)
    {
        switch (item.Kind)
        {
            case OctoberChatEventKind.AnswerDelta:
                if (item.Text is null) return false;
                try { _ = _strictUtf8.GetByteCount(item.Text); }
                catch (ArgumentException) { return false; }
                return true;
            case OctoberChatEventKind.ToolStarted:
                return !string.IsNullOrWhiteSpace(item.ToolCallId) && !_activeTools.Contains(item.ToolCallId);
            case OctoberChatEventKind.ToolCompleted:
            case OctoberChatEventKind.ToolFailed:
                return item.ToolCallId is not null && _activeTools.Contains(item.ToolCallId);
            case OctoberChatEventKind.ReferencesUpdated:
                return ValidReferences(item.References);
            case OctoberChatEventKind.DisclosureSuppressed:
                return string.IsNullOrEmpty(item.Text) && item.ToolCallId is null &&
                    item.References.IsDefaultOrEmpty && item.SavedOutcome is null;
            case OctoberChatEventKind.TurnSaved:
                if (item.SavedOutcome is not { } outcome || !Enum.IsDefined(outcome)) return false;
                if (item.Text is null) return item.References.IsDefault; // Existing controlled marker-only events.
                try { return _strictUtf8.GetByteCount(item.Text) <= OctoberDialogueLimits.MaximumAnswerUtf8Bytes && ValidReferences(item.References); }
                catch (ArgumentException) { return false; }
            default:
                return true;
        }
    }

    private bool ValidReferences(ImmutableArray<OctoberChatReference> references) => !references.IsDefault &&
        references.Length <= OctoberDialogueLimits.MaximumReferences && references.All(reference =>
            reference is not null && reference.Document is not null &&
            reference.Document.LogicalDocumentId != Guid.Empty && reference.Document.DocumentVersionId != Guid.Empty &&
            reference.Document.BatchId == Binding.Request.Publication.Knowledge.BatchId &&
            reference.Document.PublicationId == Binding.Request.Publication.PublicationId &&
            reference.SuccessfulChunks > 0 && reference.ContentBytes >= 0);
}

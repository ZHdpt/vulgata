using System.Collections.Immutable;
using System.Text;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
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
    public string AnswerText { get; private set; } = string.Empty;
    public string? StatusMessage { get; private set; }
    public string? ActivityMessage { get; private set; }
    public ImmutableArray<OctoberChatReference> References { get; private set; } = [];
    public bool IsTerminal { get; private set; }
    public bool IsSaveUncertain { get; private set; }
    public event Action? StateChanged;

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
                    StatusMessage = "正在生成回答…";
                    break;
                case OctoberChatEventKind.AnswerDelta:
                    AnswerText += item.Text;
                    break;
                case OctoberChatEventKind.ToolStarted:
                    _activeTools.Add(item.ToolCallId!);
                    ActivityMessage = "正在查阅知识…";
                    break;
                case OctoberChatEventKind.ToolCompleted:
                    _activeTools.Remove(item.ToolCallId!);
                    ActivityMessage = _activeTools.Count == 0 ? "本次知识查阅已结束。" : "正在查阅知识…";
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
                    _suppressed = true;
                    AnswerText = string.Empty;
                    References = [];
                    ActivityMessage = null;
                    _activeTools.Clear();
                    StatusMessage = "当前内容暂不可用。";
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
                    IsSaveUncertain = false;
                    ActivityMessage = null;
                    _activeTools.Clear();
                    StatusMessage = item.SavedOutcome switch
                    {
                        OctoberChatDisplayOutcome.Completed => "回答已保存。",
                        OctoberChatDisplayOutcome.Interrupted => string.IsNullOrWhiteSpace(AnswerText)
                            ? "回答已中断，尚未收到可用内容。" : "回答已中断，已保留部分内容。",
                        _ => string.IsNullOrWhiteSpace(AnswerText)
                            ? "回答失败，尚未收到可用内容。" : "回答失败，已保留部分内容。",
                    };
                    break;
            }
        }

        StateChanged?.Invoke();
        return true;
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

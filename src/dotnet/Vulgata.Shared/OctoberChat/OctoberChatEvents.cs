using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberChat;

namespace Vulgata.Shared.OctoberChat;

public enum OctoberChatEventKind
{
    TurnStarted, ToolStarted, ToolCompleted, ToolFailed, AnswerDelta, ReferencesUpdated, TurnSaved, TurnSaveUncertain,
    DisclosureSuppressed,
}

/// <summary>Host-supplied display projection, not a storage result or durable admission receipt.</summary>
public enum OctoberChatDisplayOutcome { Completed, Interrupted, Failed }

public sealed record OctoberChatEvent(Guid ConversationId, Guid ContextId, Guid TurnId, long Sequence,
    DateTimeOffset ObservedAtUtc, OctoberChatEventKind Kind, string? Text = null, string? ToolCallId = null,
    ImmutableArray<OctoberChatReference> References = default, OctoberChatDisplayOutcome? SavedOutcome = null);

/// <summary>Attachment-local ordering only; does not authorize text or establish save completion.</summary>
public sealed class OctoberChatEventSequence(OctoberChatTurnBinding binding)
{
    private readonly object _gate = new();
    private long _lastSequence;

    public bool TryAccept(OctoberChatEvent item)
    {
        lock (_gate)
        {
            if (item.ConversationId != binding.ConversationId || item.ContextId != binding.ContextId ||
                item.TurnId != binding.TurnId || item.Sequence <= _lastSequence ||
                item.ObservedAtUtc == default || item.ObservedAtUtc.Offset != TimeSpan.Zero ||
                !Enum.IsDefined(item.Kind))
            {
                return false;
            }

            _lastSequence = item.Sequence;
            return true;
        }
    }
}

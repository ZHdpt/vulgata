using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.DomainServices.OctoberChat;

public enum OctoberChatEntryStatus { Ready, Empty, Unavailable, InvalidSelection }
public enum OctoberChatWorkState { Settled, Active, SaveIndeterminate }

/// <summary>Ephemeral entry context; durable dialogue admission remains a separate prerequisite.</summary>
public sealed record OctoberChatConversation(Guid ConversationId, Guid ContextId,
    ReadSelectionRequest Request, ReadSelection Selection, DateTimeOffset CreatedAtUtc);

public sealed record OctoberChatSelectionResult(OctoberChatEntryStatus Status,
    OctoberChatConversation? Conversation, SearchPage? Catalog);

/// <summary>Receives host-resolved authority, never a browser or model authority payload.</summary>
public interface IOctoberChatSelectionService
{
    Task<OctoberChatSelectionResult> OpenAsync(ReadSelectionRequest? trustedSelection,
        CancellationToken cancellationToken = default);
}

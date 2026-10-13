using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.DomainServices.OctoberChat;

/// <summary>Host-bound controlled execution context; not proof of durable admission or disclosure authority.</summary>
public sealed record OctoberChatTurnBinding(Guid ConversationId, Guid ContextId, Guid TurnId,
    ReadSelectionRequest Request, ReadSelection Selection);

public sealed record OctoberChatLimits
{
    public int QuestionCharacters { get; init; } = 8_000;
    public int ProviderRounds { get; init; } = 30;
    public int ToolCalls { get; init; } = 64;
    public int ConcurrentTools { get; init; } = 2;
    public int ToolArgumentBytes { get; init; } = 16 * 1024;
    public int PageSize { get; init; } = 20;
    public int ContentBytes { get; init; } = 64 * 1024;
    public int ToolResultBytes { get; init; } = 256 * 1024;
    public int OutputBytes { get; init; } = 64 * 1024;
    public int HistoryTurns { get; init; } = 12;
    public int HistoryBytes { get; init; } = 48 * 1024;
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(540);
    public int SseRoundBytes { get; init; } = 2 * 1024 * 1024;
    public int SseTurnBytes { get; init; } = 8 * 1024 * 1024;
}

public sealed record OctoberChatReference(DocumentHandle Document, bool IsPartial, int SuccessfulChunks, long ContentBytes);
public enum OctoberChatToolStatus { Succeeded, InvalidArguments, Unavailable, BudgetExceeded, Cancelled, InvalidReport }
public sealed record OctoberChatToolResult(OctoberChatToolStatus Status, SearchPage? Search = null,
    ContentPage? Content = null, RelatedPage? Related = null,
    ImmutableArray<OctoberChatReference> References = default);

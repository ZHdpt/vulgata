using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>The sole canonical logical codec and complete host-envelope encoding for durable dialogue.</summary>
public sealed class OctoberDialoguePayloadCodec : IOctoberDialoguePayloadCodec
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string ComputeBeginDigest(DialogueConversationIdentity conversation, DialogueBeginRequest request) =>
        OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/dialogue-begin/1",
            schemaVersion = 1,
            conversation = new
            {
                conversation.ConversationId, conversation.ContextId, conversation.OwnerSubjectId,
                conversation.Publication, conversation.Mode,
                repositoryScope = conversation.RepositoryScope.OrderBy(x => x.ToString("D"), StringComparer.Ordinal),
                purpose = KnowledgePurpose.Chat
            },
            request = new
            {
                request.ClientRequestKey, request.Question, request.ProviderProfileId, request.ProviderProfileRevision,
                request.PromptDigest, request.HistoryTurnIds, request.HistoryProjectionDigest,
                initialDependencyDigest = request.InitialDependencies.BindingDigest,
                limits = new
                {
                    request.Limits.QuestionCharacters, request.Limits.ProviderRounds, request.Limits.ToolCalls,
                    request.Limits.ConcurrentTools, request.Limits.ToolArgumentBytes, request.Limits.PageSize,
                    request.Limits.ContentBytes, request.Limits.ToolResultBytes, request.Limits.OutputBytes,
                    request.Limits.HistoryTurns, request.Limits.HistoryBytes, durationTicks = request.Limits.Duration.Ticks,
                    request.Limits.SseRoundBytes, request.Limits.SseTurnBytes
                }
            }
        }));

    public string ComputeOutcomeDigest(DialogueTerminalSaveRequest request) =>
        OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/dialogue-terminal/1",
            schemaVersion = 1,
            request.Owner, request.SaveOperationId, request.TerminalState, request.Reason, request.ProviderFinish,
            request.AnswerText, request.References,
            consumedReads = request.ConsumedReads.Select(x => new { x.Reference, proofBindingDigest = x.Proof.BindingDigest }),
            inferenceDependencyDigest = request.InferenceDependencies?.BindingDigest,
            request.HistoryTurnIds,
            generationEndedAtUtc = request.GenerationEndedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)
        }));

    public string ComputeHistoryProjectionDigest(ImmutableArray<DialogueHistoryTurn> turns) =>
        OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new
        {
            domain = "october/dialogue-history/1",
            schemaVersion = 1,
            turns = turns.Select(x => new
            {
                x.TurnId, x.Sequence, x.Question, x.AnswerText, x.References,
                inferenceDependencyDigest = x.InferenceDependencies.BindingDigest
            })
        }));

    public int MeasureBeginEnvelopeUtf8Bytes(DialogueBeginRequest request) =>
        JsonSerializer.SerializeToUtf8Bytes(BeginEnvelope(request), Json).Length;

    public int MeasureTerminalEnvelopeUtf8Bytes(DialogueTerminalSaveRequest request) =>
        JsonSerializer.SerializeToUtf8Bytes(TerminalEnvelope(request), Json).Length;

    public ImmutableArray<byte> EncodeHistoryProjectionUtf8(ImmutableArray<DialogueHistoryTurn> turns) =>
        JsonSerializer.SerializeToUtf8Bytes(turns.Select(x => new { x.Question, x.AnswerText, x.References }), Json).ToImmutableArray();

    public int MeasureHistoryProjectionUtf8Bytes(ImmutableArray<DialogueHistoryTurn> turns) =>
        EncodeHistoryProjectionUtf8(turns).Length;

    // JsonIgnore intentionally protects outward DTO serialization; persistence must explicitly include every capsule.
    private static object BeginEnvelope(DialogueBeginRequest r) => new
    {
        r.ConversationId, r.ContextId, r.ClientRequestKey, r.Question, r.RequestDigest,
        r.ProviderProfileId, r.ProviderProfileRevision, r.PromptDigest, r.HistoryTurnIds,
        r.HistoryProjectionDigest, r.InitialDependencies, r.Limits, r.Execution
    };

    private static object TerminalEnvelope(DialogueTerminalSaveRequest r) => new
    {
        r.Owner, r.SaveOperationId, r.OutcomeDigest, r.TerminalState, r.Reason, r.ProviderFinish,
        r.AnswerText, r.References, consumedReads = r.ConsumedReads.Select(x => new { x.Reference, x.Proof }),
        r.InferenceDependencies, r.HistoryTurnIds, r.GenerationEndedAtUtc
    };

    internal static string EncodeBegin(DialogueBeginRequest request) => JsonSerializer.Serialize(BeginEnvelope(request), Json);
    internal static string EncodeTerminal(DialogueTerminalSaveRequest request) => JsonSerializer.Serialize(TerminalEnvelope(request), Json);
    internal static int MeasureReferences(ImmutableArray<OctoberChatReference> references) => JsonSerializer.SerializeToUtf8Bytes(references, Json).Length;
    internal static int MeasureConsumed(ImmutableArray<DialogueConsumedTarget> consumed) =>
        JsonSerializer.SerializeToUtf8Bytes(consumed.Select(x => new { x.Reference, x.Proof }), Json).Length;

    internal static DialogueBeginRequest DecodeBegin(string value)
    {
        using var document = JsonDocument.Parse(value);
        var r = document.RootElement;
        return new(Read<Guid>(r, "conversationId"), Read<Guid>(r, "contextId"), Read<Guid>(r, "clientRequestKey"),
            Read<string>(r, "question"), Read<string>(r, "requestDigest"), Read<string>(r, "providerProfileId"),
            Read<string>(r, "providerProfileRevision"), Read<string>(r, "promptDigest"), Read<ImmutableArray<Guid>>(r, "historyTurnIds"),
            Read<string>(r, "historyProjectionDigest"), Read<KnowledgeDependencyHandle>(r, "initialDependencies"),
            Read<OctoberChatLimits>(r, "limits"), Read<DialogueExecutionIdentity>(r, "execution"));
    }

    internal static DialogueTerminalSaveRequest DecodeTerminal(string value)
    {
        using var document = JsonDocument.Parse(value);
        var r = document.RootElement;
        return new(Read<DialogueTurnOwnership>(r, "owner"), Read<Guid>(r, "saveOperationId"), Read<string>(r, "outcomeDigest"),
            Read<DialogueTerminalState>(r, "terminalState"), Read<DialogueTerminalReason>(r, "reason"),
            Read<DialogueProviderFinish>(r, "providerFinish"), Read<string>(r, "answerText"), Read<ImmutableArray<OctoberChatReference>>(r, "references"),
            r.GetProperty("consumedReads").EnumerateArray().Select(x => new DialogueConsumedTarget(Read<OctoberChatReference>(x, "reference"),
                Read<KnowledgeConsumedContentHandle>(x, "proof"))).ToImmutableArray(),
            r.GetProperty("inferenceDependencies").Deserialize<KnowledgeDependencyHandle>(Json), Read<ImmutableArray<Guid>>(r, "historyTurnIds"),
            Read<DateTimeOffset>(r, "generationEndedAtUtc"));
    }

    private static T Read<T>(JsonElement element, string name) => element.GetProperty(name).Deserialize<T>(Json)
        ?? throw new JsonException("Missing durable dialogue field.");
}

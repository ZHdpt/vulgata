using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.OctoberBusiness;

internal static class OctoberBusinessSynthesisRequests
{
    private static readonly UTF8Encoding _utf8 = new(false, true);

    // Conservative UTF-8/escaped-message byte proxy, not a tokenizer measurement.
    // Includes instructions, packet, prior text/reasoning and a bounded protocol envelope.
    internal static long EstimateInputTokens(IReadOnlyCollection<ChatMessage> messages, IList<AITool>? tools = null)
    {
        if (messages.Count > 40) throw new ArgumentException("Synthesis history exceeds its message bound.");
        string envelope = JsonSerializer.Serialize(messages.Select(message => new
        {
            role = message.Role.Value,
            content = string.Concat(message.Contents.OfType<TextContent>().Select(content => content.Text)),
            reasoning_content = string.Concat(message.Contents.OfType<TextReasoningContent>().Select(content => content.Text))
        }));
        string calls = messages.Any(m => m.Contents.Any(c => c is FunctionCallContent or FunctionResultContent))
            ? JsonSerializer.Serialize(messages.Select(m => new
            {
                calls = m.Contents.OfType<FunctionCallContent>().Select(c => new { c.CallId, c.Name, c.Arguments }),
                results = m.Contents.OfType<FunctionResultContent>().Select(c => new { c.CallId, c.Result })
            })) : "";
        string schemas = tools is { Count: > 0 }
            ? JsonSerializer.Serialize(tools.OfType<AIFunction>().Select(t => new { t.Name, t.Description, t.JsonSchema })) : "";
        long bytes = checked(_utf8.GetByteCount(envelope) + _utf8.GetByteCount(calls) + _utf8.GetByteCount(schemas) + 4096L + 256L * messages.Count);
        if (bytes > OctoberBusinessSynthesisPolicy.MaximumPayloadUtf8Bytes)
            throw new ArgumentException("Synthesis messages exceed the model input bound.");
        return bytes;
    }

    internal static bool Correctable(BusinessSynthesisFailureReason reason) => reason is
        BusinessSynthesisFailureReason.JsonInvalid or BusinessSynthesisFailureReason.ProposalMissing or
        BusinessSynthesisFailureReason.ProposalShape or BusinessSynthesisFailureReason.SectionOrder or
        BusinessSynthesisFailureReason.ClaimShape;

    // All static instructions are in the original hash-bound system prompt.
    internal static string Correction(BusinessSynthesisFailureReason reason, int remaining) =>
        JsonSerializer.Serialize(new { failure = reason.ToString(), remainingCorrections = remaining });

    internal static void RequireUniqueProperties(string json)
    {
        using var document = JsonDocument.Parse(json);
        Visit(document.RootElement);
        static void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new JsonException("Duplicate synthesis field.");
                    Visit(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) Visit(item);
        }
    }
}

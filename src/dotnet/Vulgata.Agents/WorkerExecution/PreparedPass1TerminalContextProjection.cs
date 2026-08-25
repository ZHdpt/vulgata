using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

internal enum PreparedPass1TerminalFailureKind
{
    Prompt,
    Integrity,
    Bound,
}

internal static class PreparedPass1TerminalFailure
{
    private const string KindKey = "vulgata.pass1-terminal-failure-kind";

    internal static InvalidDataException Prompt(string message) =>
        Create(PreparedPass1TerminalFailureKind.Prompt, message);

    internal static InvalidDataException Integrity(
        string message,
        Exception? innerException = null) =>
        Create(PreparedPass1TerminalFailureKind.Integrity, message, innerException);

    internal static InvalidDataException Bound(string message) =>
        Create(PreparedPass1TerminalFailureKind.Bound, message);

    internal static bool Is(
        Exception exception,
        PreparedPass1TerminalFailureKind kind) =>
        exception is InvalidDataException &&
        exception.Data[KindKey] is PreparedPass1TerminalFailureKind actual &&
        actual == kind;

    private static InvalidDataException Create(
        PreparedPass1TerminalFailureKind kind,
        string message,
        Exception? innerException = null)
    {
        var exception = new InvalidDataException(message, innerException);
        exception.Data[KindKey] = kind;
        return exception;
    }
}

/// <summary>
/// The execution-local, finalization-only input to the Terminal Context
/// Projection. This is deliberately a small compile seam for the first RED
/// slice; it is not a second provider conversation or persistence authority.
/// </summary>
internal sealed record PreparedPass1TerminalContextProjectionRequest(
    string PromptResource,
    string PromptIdentity,
    ReadOnlyMemory<byte> OriginalPacketUtf8,
    int ProviderTurn,
    IReadOnlyList<PreparedPass1TerminalContextResult> EvidenceResults,
    PreparedPass1TerminalContextCorrection? FirstSubmissionCorrection = null,
    string? BoundPromptIdentity = null,
    ReadOnlyMemory<byte>? BoundOriginalPacketUtf8 = null);

/// <summary>
/// The only correction metadata that may cross the terminal projection seam.
/// </summary>
public enum PreparedPass1SubmissionRepairCode
{
    UseMinimalValidDocument,
    CopyPacketBinding,
    UseAuthorizedFullRangeAnchor,
}

internal sealed record PreparedPass1TerminalContextCorrection(
    int Attempt,
    PreparedPass1SubmissionRejectionKind Category,
    PreparedPass1SubmissionRepairCode? RepairCode = null);

/// <summary>
/// One ordered server-owned result retained by the bounded execution-local
/// accumulator. The function-call protocol itself is intentionally absent.
/// </summary>
internal sealed record PreparedPass1TerminalContextResult(
    int Order,
    object Result)
{
    public static PreparedPass1TerminalContextResult FromNavigationResponse(
        int order,
        PreparedPass1NavigationResponse result) =>
        FromNavigationResponse(order, result, DefaultNavigationSerializerOptions);

    public static PreparedPass1TerminalContextResult FromNavigationResponse(
        int order,
        PreparedPass1NavigationResponse result,
        JsonSerializerOptions serializerOptions)
    {
        if (result is null || serializerOptions is null)
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Navigation result capture authority is missing.");
        }

        try
        {
            JsonElement captured = JsonSerializer.SerializeToElement(
                result,
                serializerOptions);
            return new(order, captured.Clone());
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Navigation result canonicalization failed.",
                exception);
        }
    }

    public static PreparedPass1TerminalContextResult FromNavigationBudgetExhausted(
        int order) =>
        new(order, "navigation-budget-exhausted");

    private static JsonSerializerOptions DefaultNavigationSerializerOptions =>
        new(AIJsonUtilities.DefaultOptions)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
}

/// <summary>
/// The result of projecting one existing FICC request into its terminal view.
/// </summary>
internal sealed record PreparedPass1TerminalContextProjectionResult(
    bool IsValid,
    string TerminalInstruction,
    string ContextJson,
    IReadOnlyList<ChatMessage> Messages);

/// <summary>
/// Bounded, execution-local result accumulator. It has no durable backing and
/// exposes no source, provider argument, or function-call identity.
/// </summary>
internal sealed class PreparedPass1TerminalContextAccumulator : IDisposable
{
    internal const int MaximumResults = 64;
    private readonly List<PreparedPass1TerminalContextResult> _results = [];
    private bool _released;

    public int Count => _results.Count;

    public bool ReferenceReleased => _released;

    public void Add(PreparedPass1TerminalContextResult result)
    {
        ObjectDisposedException.ThrowIf(_released, this);
        ArgumentNullException.ThrowIfNull(result);
        if (result.Result is not JsonElement { ValueKind: JsonValueKind.Object } &&
            !string.Equals(
                result.Result as string,
                "navigation-budget-exhausted",
                StringComparison.Ordinal))
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Terminal context accepts only server-owned navigation results.");
        }

        if (result.Order != _results.Count + 1)
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Terminal context results must be contiguous and ordered.");
        }

        if (_results.Count >= MaximumResults)
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Terminal context cannot retain a sixty-fifth result.");
        }

        _results.Add(result);
    }

    public IReadOnlyList<PreparedPass1TerminalContextResult> Snapshot()
    {
        ObjectDisposedException.ThrowIf(_released, this);
        return [.. _results];
    }

    public void Release()
    {
        if (_released)
        {
            return;
        }

        _results.Clear();
        _released = true;
    }

    public void Dispose() => Release();
}

/// <summary>
/// Derives the finalization-only view of the existing Worker conversation.
/// </summary>
internal static class PreparedPass1TerminalContextProjection
{
    private const string PreambleBegin = "<!-- PASS1_TERMINAL_PREAMBLE_BEGIN -->";
    private const string PreambleEnd = "<!-- PASS1_TERMINAL_PREAMBLE_END -->";
    private const string CommonBegin = "<!-- PASS1_TERMINAL_COMMON_BEGIN -->";
    private const string CommonEnd = "<!-- PASS1_TERMINAL_COMMON_END -->";
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    public static PreparedPass1TerminalContextProjectionResult Create(
        PreparedPass1TerminalContextProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ShouldProject(request.ProviderTurn, request.FirstSubmissionCorrection))
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Terminal context cannot be projected before finalization is legal.");
        }

        if (string.IsNullOrWhiteSpace(request.PromptIdentity))
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Terminal prompt identity is missing.");
        }
        if (request.BoundPromptIdentity is not null &&
            !string.Equals(
                request.PromptIdentity,
                request.BoundPromptIdentity,
                StringComparison.Ordinal))
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Terminal prompt identity binding changed.");
        }

        if (request.BoundOriginalPacketUtf8 is ReadOnlyMemory<byte> boundPacket &&
            !request.OriginalPacketUtf8.Span.SequenceEqual(boundPacket.Span))
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Terminal packet binding changed.");
        }

        string terminalInstruction = DeriveTerminalInstruction(request.PromptResource);
        string packet = _strictUtf8.GetString(request.OriginalPacketUtf8.Span);
        string contextJson = WriteContext(
            request.EvidenceResults,
            request.FirstSubmissionCorrection);
        ChatMessage[] messages =
        [
            new(ChatRole.System, terminalInstruction),
            new(ChatRole.User, packet),
            new(ChatRole.User, contextJson),
        ];
        return new(true, terminalInstruction, contextJson, messages);
    }

    public static string SerializeInvokedResult(
        PreparedPass1NavigationResponse result) =>
        PreparedPass1TerminalContextResult.FromNavigationResponse(1, result)
            .Result is JsonElement captured
                ? captured.GetRawText()
                : throw new InvalidOperationException(
                    "The navigation result was not captured as canonical JSON.");

    public static string SerializeFixedResult() =>
        JsonSerializer.Serialize("navigation-budget-exhausted");

    public static bool ShouldProject(
        int providerTurn,
        PreparedPass1TerminalContextCorrection? firstSubmissionCorrection) =>
        providerTurn is 9 or 10 ||
        (providerTurn is >= 1 and < 9 && firstSubmissionCorrection is not null);

    private static string DeriveTerminalInstruction(string promptResource)
    {
        ArgumentNullException.ThrowIfNull(promptResource);
        if (promptResource.Contains('\r', StringComparison.Ordinal))
        {
            throw PreparedPass1TerminalFailure.Prompt(
                "Terminal prompt resources must use LF line endings only.");
        }

        string[] markers = [PreambleBegin, PreambleEnd, CommonBegin, CommonEnd];
        int[] positions = markers.Select(marker => FindFullLineMarker(promptResource, marker)).ToArray();
        if (!(positions[0] < positions[1] &&
              positions[1] < positions[2] &&
              positions[2] < positions[3]))
        {
            throw PreparedPass1TerminalFailure.Prompt(
                "Terminal prompt markers are out of order.");
        }

        string preamble = ExtractMarkedContent(
            promptResource,
            positions[0],
            PreambleBegin,
            positions[1]);
        string common = ExtractMarkedContent(
            promptResource,
            positions[2],
            CommonBegin,
            positions[3]);
        string derived = preamble + "\n" + common;
        if (derived.Contains("navigate_code", StringComparison.Ordinal))
        {
            throw PreparedPass1TerminalFailure.Prompt(
                "Terminal instructions contain a navigation function literal.");
        }

        return derived;
    }

    private static int FindFullLineMarker(string promptResource, string marker)
    {
        int position = promptResource.IndexOf(marker, StringComparison.Ordinal);
        if (position < 0 ||
            promptResource.IndexOf(marker, position + marker.Length, StringComparison.Ordinal) >= 0 ||
            (position > 0 && promptResource[position - 1] != '\n') ||
            position + marker.Length >= promptResource.Length ||
            promptResource[position + marker.Length] != '\n')
        {
            throw PreparedPass1TerminalFailure.Prompt(
                "Terminal prompt must contain each marker exactly once on a full line.");
        }

        return position;
    }

    private static string ExtractMarkedContent(
        string promptResource,
        int beginPosition,
        string beginMarker,
        int endPosition)
    {
        int contentStart = checked(beginPosition + beginMarker.Length + 1);
        if (endPosition <= contentStart)
        {
            throw PreparedPass1TerminalFailure.Prompt(
                "Terminal prompt marker content is empty.");
        }

        return promptResource[contentStart..endPosition];
    }

    private static string WriteContext(
        IReadOnlyList<PreparedPass1TerminalContextResult> results,
        PreparedPass1TerminalContextCorrection? correction)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (results.Count > PreparedPass1TerminalContextAccumulator.MaximumResults)
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Terminal context result bound was exceeded.");
        }

        ValidateCorrection(correction);
        using var destination = new MemoryStream();
        try
        {
            using (var writer = new Utf8JsonWriter(destination))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaIdentity", "pass1-terminal-context/1");
                writer.WriteStartArray("evidenceResults");
                for (int index = 0; index < results.Count; index++)
                {
                    PreparedPass1TerminalContextResult result = results[index]
                        ?? throw PreparedPass1TerminalFailure.Integrity(
                            "Terminal context result is missing.");
                    if (result.Order != index + 1)
                    {
                        throw PreparedPass1TerminalFailure.Integrity(
                            "Terminal context results must be contiguous and ordered.");
                    }

                    writer.WriteStartObject();
                    writer.WriteNumber("order", result.Order);
                    writer.WritePropertyName("result");
                    switch (result.Result)
                    {
                        case JsonElement captured when captured.ValueKind == JsonValueKind.Object:
                            captured.WriteTo(writer);
                            break;
                        case string fixedResult when string.Equals(
                            fixedResult,
                            "navigation-budget-exhausted",
                            StringComparison.Ordinal):
                            writer.WriteStringValue(fixedResult);
                            break;
                        default:
                            throw PreparedPass1TerminalFailure.Integrity(
                                "Terminal context contains an unsupported evidence value.");
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WritePropertyName("firstSubmissionCorrection");
                if (correction is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("attempt", correction.Attempt);
                    writer.WriteString("category", correction.Category.ToString());
                    writer.WriteString("repairCode", correction.RepairCode!.Value.ToString());
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            if (destination.Length > PreparedPass1WireBudget.MaximumToolResultAggregateBytes)
            {
                throw PreparedPass1TerminalFailure.Bound(
                    "Terminal context exceeds its canonical UTF-8 byte bound.");
            }

            byte[] buffer = destination.GetBuffer();
            return _strictUtf8.GetString(
                buffer.AsSpan(0, checked((int)destination.Length)));
        }
        finally
        {
            if (destination.TryGetBuffer(out ArraySegment<byte> buffer) &&
                buffer.Array is not null)
            {
                CryptographicOperations.ZeroMemory(buffer.Array);
            }
        }
    }

    private static void ValidateCorrection(
        PreparedPass1TerminalContextCorrection? correction)
    {
        if (correction is null)
        {
            return;
        }

        PreparedPass1SubmissionRepairCode? expectedRepairCode = correction.Category switch
        {
            PreparedPass1SubmissionRejectionKind.InvalidShape =>
                PreparedPass1SubmissionRepairCode.UseMinimalValidDocument,
            PreparedPass1SubmissionRejectionKind.InvalidSemantics =>
                PreparedPass1SubmissionRepairCode.UseMinimalValidDocument,
            PreparedPass1SubmissionRejectionKind.InvalidBinding =>
                PreparedPass1SubmissionRepairCode.CopyPacketBinding,
            PreparedPass1SubmissionRejectionKind.InvalidEvidence =>
                PreparedPass1SubmissionRepairCode.UseAuthorizedFullRangeAnchor,
            _ => null,
        };
        if (correction.Attempt != 1 ||
            expectedRepairCode is null ||
            correction.RepairCode != expectedRepairCode)
        {
            throw PreparedPass1TerminalFailure.Integrity(
                "Terminal correction must match the closed first-submission repair code.");
        }
    }
}

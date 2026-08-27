using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

public static class PreparedPass2WireBudget
{
    public const int MaximumInitialRequestUtf8Bytes = 131_072;
    public const int MaximumRawResponseUtf8Bytes = 16_777_216;
    public const int MaximumAssembledResponseUtf8Bytes = 16_777_216;
}

public sealed record PreparedPass2WorkerPacket(
    Guid JobId,
    PreparedPass2Flow Flow,
    PreparedPass2RuntimeBinding RuntimeBinding,
    IReadOnlyDictionary<Guid, string> EvidenceHandles,
    ReadOnlyMemory<byte> ProviderUtf8)
{
    public Guid LeaseToken { get; init; }
    public DateTimeOffset LeaseExpiresAt { get; init; }
    public int AttemptOrdinal { get; init; }
    public Guid Pass2ExecutionId { get; init; }
    public Guid FlowGroupId { get; init; }
    public Guid ExactResolutionDecisionId { get; init; }
    public string LogicalDocumentStableKey { get; init; } = string.Empty;
    public string PlanFingerprint { get; init; } = string.Empty;
    public string MembershipFingerprint { get; init; } = string.Empty;
    public string PromptIdentity { get; init; } = string.Empty;
    public string PromptFingerprint { get; init; } = string.Empty;
    public string SubmissionSchemaIdentity { get; init; } = string.Empty;
    public string SubmissionSchemaFingerprint { get; init; } = string.Empty;
    public string ToolDeclarationIdentity { get; init; } = string.Empty;
    public string ToolDeclarationFingerprint { get; init; } = string.Empty;
}

public sealed record PreparedPass2ProviderRequest(
    int RequestNumber,
    string Prompt,
    ReadOnlyMemory<byte> PacketUtf8,
    ReadOnlyMemory<byte> FunctionDeclarationsUtf8,
    bool SubmissionOnly,
    ReadOnlyMemory<byte> TranscriptUtf8 = default,
    IReadOnlyList<string>? ToolCallIds = null,
    ReadOnlyMemory<byte> SanitizedToolResultsUtf8 = default,
    ReadOnlyMemory<byte> RepairContextUtf8 = default);

public interface IPreparedPass2StreamingTransport
{
    IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
        PreparedPass2ProviderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PreparedPass2RawExecutionResult(
    bool Accepted,
    ReadOnlyMemory<byte> CanonicalUtf8,
    string? ReasonCode);

/// <summary>
/// Executes the bounded Pass 2 conversation. The transport is called once per
/// request and is never retried by this class.
/// </summary>
public sealed class PreparedPass2JobExecutor
{
    public static readonly TimeSpan ProviderInactivityTimeout = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ProviderAbsoluteTimeout = TimeSpan.FromMinutes(25);

    private readonly IPreparedPass2StreamingTransport _transport;
    private readonly PreparedRetrieveCodeLogicFunction _retrieveFactory;
    private readonly PreparedSubmitBusinessLogicFunction _submitFactory;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _inactivityTimeout;
    private readonly TimeSpan _absoluteTimeout;

    public PreparedPass2JobExecutor(
        IPreparedPass2StreamingTransport transport,
        PreparedRetrieveCodeLogicFunction retrieveFactory,
        PreparedSubmitBusinessLogicFunction submitFactory)
        : this(
            transport,
            retrieveFactory,
            submitFactory,
            TimeProvider.System,
            ProviderInactivityTimeout,
            ProviderAbsoluteTimeout)
    {
    }

    public PreparedPass2JobExecutor(
        IPreparedPass2StreamingTransport transport,
        PreparedRetrieveCodeLogicFunction retrieveFactory,
        PreparedSubmitBusinessLogicFunction submitFactory,
        TimeProvider timeProvider,
        TimeSpan inactivityTimeout,
        TimeSpan absoluteTimeout)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _retrieveFactory = retrieveFactory ?? throw new ArgumentNullException(nameof(retrieveFactory));
        _submitFactory = submitFactory ?? throw new ArgumentNullException(nameof(submitFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(inactivityTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(absoluteTimeout, TimeSpan.Zero);
        _inactivityTimeout = inactivityTimeout;
        _absoluteTimeout = absoluteTimeout;
    }

    public async Task<PreparedPass2RawExecutionResult> ExecuteAsync(
        PreparedPass2WorkerPacket packet,
        string prompt,
        Func<ReadOnlyMemory<byte>, CancellationToken, Task<PreparedPass2CandidateDecision>>
            validateCandidateAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(validateCandidateAsync);
        cancellationToken.ThrowIfCancellationRequested();
        if (packet.JobId == Guid.Empty ||
            !packet.RuntimeBinding.IsCanonical() ||
            !packet.Flow.IsExactMatch(packet.RuntimeBinding.Flow) ||
            packet.ProviderUtf8.Length > PreparedPass2WireBudget.MaximumInitialRequestUtf8Bytes)
        {
            return new(false, ReadOnlyMemory<byte>.Empty, "invalid-binding");
        }

        PreparedRetrieveCodeLogicSession retrieve =
            _retrieveFactory.CreateSession(packet.JobId);
        PreparedSubmitBusinessLogicSession submit =
            _submitFactory.CreateSession(validateCandidateAsync);
        Pass2StrictDeclarationSet declarations = Pass2StrictToolDeclarations.Create(
            retrieve.Function,
            submit.Function);
        if (packet.ToolDeclarationFingerprint.Length != 0 &&
            !StringComparer.Ordinal.Equals(
                packet.ToolDeclarationFingerprint,
                Convert.ToHexStringLower(SHA256.HashData(declarations.ProjectedUtf8))))
        {
            return new(false, ReadOnlyMemory<byte>.Empty, "invalid-binding");
        }

        if (packet.PromptFingerprint.Length != 0 &&
            !StringComparer.Ordinal.Equals(
                packet.PromptFingerprint,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prompt)))))
        {
            return new(false, ReadOnlyMemory<byte>.Empty, "invalid-binding");
        }

        using var absoluteCancellation = new CancellationTokenSource(
            _absoluteTimeout,
            _timeProvider);
        int retrievalCallCount = 0;
        bool retrievalBoundExceeded = false;
        var transcript = new List<PreparedPass2TranscriptEntry>();
        var toolCallIds = new List<string>();
        for (int requestNumber = 1; requestNumber <= 10; requestNumber++)
        {
            bool submissionOnly = requestNumber >= 9;
            ReadOnlyMemory<byte> declarationBytes = submissionOnly
                ? declarations.ProjectedSubmitOnlyUtf8
                : declarations.ProjectedUtf8;
            byte[] transcriptUtf8 = SerializeTranscript(transcript);
            byte[] sanitizedResultsUtf8 = SerializeSanitizedResults(transcript);
            byte[] repairContextUtf8 = requestNumber == 10
                ? SerializeRepairContext(submit.LastDisposition)
                : [];
            byte[] providerContextUtf8 = ComposeProviderContext(
                packet,
                transcriptUtf8,
                sanitizedResultsUtf8,
                repairContextUtf8);
            var request = new PreparedPass2ProviderRequest(
                requestNumber,
                prompt,
                providerContextUtf8,
                declarationBytes,
                submissionOnly,
                transcriptUtf8,
                Array.AsReadOnly(toolCallIds.ToArray()),
                sanitizedResultsUtf8,
                repairContextUtf8);
            if (MeasureRequest(request) >= PreparedPass2WireBudget.MaximumInitialRequestUtf8Bytes)
            {
                return new(false, ReadOnlyMemory<byte>.Empty, "request-bound");
            }

            byte[] response;
            try
            {
                response = await ReadBoundedAsync(
                    request,
                    absoluteCancellation.Token,
                    cancellationToken);
            }
            catch (TimeoutException)
            {
                return new(false, ReadOnlyMemory<byte>.Empty, "provider-timeout");
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException ||
                !cancellationToken.IsCancellationRequested)
            {
                return new(false, ReadOnlyMemory<byte>.Empty, "provider-unavailable");
            }

            try
            {
                if (!TryParseCalls(response, out PreparedPass2ToolCall[] calls) ||
                    !Preflight(
                        calls,
                        submissionOnly,
                        requestNumber,
                        retrievalCallCount))
                {
                    return new(false, ReadOnlyMemory<byte>.Empty, "invalid-tool-call");
                }

                toolCallIds.AddRange(calls.Select(call => call.Id));

                retrievalCallCount += calls.Count(call => string.Equals(
                    call.Name,
                    Pass2StrictToolDeclarations.RetrieveCodeLogicName,
                    StringComparison.Ordinal));

                foreach (PreparedPass2ToolCall call in calls)
                {
                    bool isRetrieval = string.Equals(
                        call.Name,
                        Pass2StrictToolDeclarations.RetrieveCodeLogicName,
                        StringComparison.Ordinal);
                    if (retrievalBoundExceeded)
                    {
                        if (!isRetrieval)
                        {
                            return new(
                                false,
                                ReadOnlyMemory<byte>.Empty,
                                "retrieval-bound-exceeded");
                        }

                        return new(
                            false,
                            ReadOnlyMemory<byte>.Empty,
                            "retrieval-bound-exceeded");
                    }

                    AIFunction function = string.Equals(
                        call.Name,
                        Pass2StrictToolDeclarations.RetrieveCodeLogicName,
                        StringComparison.Ordinal)
                        ? retrieve.Function
                        : submit.Function;
                    AIFunctionArguments arguments = CreateFunctionArguments(
                        call.Arguments,
                        isRetrieval);
                    try
                    {
                        await function.InvokeAsync(arguments, cancellationToken);
                    }
                    catch (Exception exception) when (
                        exception is JsonException or FormatException or OverflowException)
                    {
                        return new(false, ReadOnlyMemory<byte>.Empty, "invalid-tool-call");
                    }
                    if (isRetrieval && retrieve.IsBoundExceeded)
                    {
                        retrievalBoundExceeded = true;
                    }

                    if (isRetrieval && retrieve.LastResponse is { } retrievalResponse)
                    {
                        transcript.Add(new(
                            call.Id,
                            call.Name,
                            call.Arguments,
                            retrievalResponse,
                            null));
                    }
                    else if (!isRetrieval)
                    {
                        transcript.Add(new(
                            call.Id,
                            call.Name,
                            call.Arguments,
                            null,
                            submit.LastDisposition));
                    }
                }

                if (submit.IsAccepted)
                {
                    return new(true, submit.TakeAcceptedCanonicalUtf8(), null);
                }

                if (calls.Any(call => string.Equals(
                        call.Name,
                        Pass2StrictToolDeclarations.SubmitBusinessLogicName,
                        StringComparison.Ordinal)))
                {
                    if (requestNumber >= 10)
                    {
                        return new(false, ReadOnlyMemory<byte>.Empty, "submission-rejected");
                    }

                    if (requestNumber < 9)
                    {
                        requestNumber = 9;
                    }
                }
            }
            finally
            {
                Array.Clear(response);
            }
        }

        return new(false, ReadOnlyMemory<byte>.Empty, "submission-missing");
    }

    private async Task<byte[]> ReadBoundedAsync(
        PreparedPass2ProviderRequest request,
        CancellationToken absoluteCancellationToken,
        CancellationToken cancellationToken)
    {
        using var destination = new MemoryStream();
        using var streamCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IAsyncEnumerator<ReadOnlyMemory<byte>> enumerator =
            _transport.StreamAsync(request, streamCancellation.Token)
                .GetAsyncEnumerator(streamCancellation.Token);
        Task absoluteTimeout = Task.Delay(
            Timeout.InfiniteTimeSpan,
            absoluteCancellationToken);
        bool timedOut = false;
        try
        {
            while (true)
            {
                Task<bool> moveNext = enumerator.MoveNextAsync().AsTask();
                using var inactivityCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task inactivity = Task.Delay(
                    _inactivityTimeout,
                    _timeProvider,
                    inactivityCancellation.Token);
                Task completed = await Task.WhenAny(
                    moveNext,
                    inactivity,
                    absoluteTimeout);
                if (completed != moveNext)
                {
                    timedOut = true;
                    streamCancellation.Cancel();
                    break;
                }

                inactivityCancellation.Cancel();
                if (!await moveNext)
                {
                    break;
                }

                ReadOnlyMemory<byte> chunk = enumerator.Current;
                if (destination.Length + chunk.Length >=
                        PreparedPass2WireBudget.MaximumRawResponseUtf8Bytes ||
                    destination.Length + chunk.Length >=
                        PreparedPass2WireBudget.MaximumAssembledResponseUtf8Bytes)
                {
                    throw new InvalidDataException("Provider response exceeds its byte bound.");
                }

                destination.Write(chunk.Span);
            }
        }
        finally
        {
            if (timedOut)
            {
                streamCancellation.Cancel();
            }

            try
            {
                await enumerator.DisposeAsync();
            }
            catch (Exception) when (timedOut)
            {
                // Cancellation only unblocks a timed-out provider enumerator.
            }
        }

        if (timedOut)
        {
            throw new TimeoutException();
        }

        return destination.ToArray();
    }

    private static bool TryParseCalls(
        byte[] response,
        out PreparedPass2ToolCall[] calls)
    {
        calls = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(response, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32,
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("toolCalls", out JsonElement toolCalls) ||
                toolCalls.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            calls = toolCalls.EnumerateArray().Select(value => new PreparedPass2ToolCall(
                value.TryGetProperty("id", out JsonElement id) &&
                    id.ValueKind == JsonValueKind.String
                    ? id.GetString() ?? string.Empty
                    : string.Empty,
                value.GetProperty("name").GetString() ?? string.Empty,
                value.GetProperty("arguments").Clone())).ToArray();
            return calls.Length > 0;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
    }

    private static bool Preflight(
        IReadOnlyList<PreparedPass2ToolCall> calls,
        bool submissionOnly,
        int requestNumber,
        int retrievalCallCount)
    {
        if (calls.Any(call => !IsValidToolCallId(call.Id)) ||
            calls.Select(call => call.Id).Distinct(StringComparer.Ordinal).Count() != calls.Count)
        {
            return false;
        }

        bool allRetrieve = calls.All(call => string.Equals(
            call.Name,
            Pass2StrictToolDeclarations.RetrieveCodeLogicName,
            StringComparison.Ordinal));
        bool soleSubmit = calls.Count == 1 && string.Equals(
            calls[0].Name,
            Pass2StrictToolDeclarations.SubmitBusinessLogicName,
            StringComparison.Ordinal);
        if (submissionOnly || requestNumber >= 9)
        {
            return soleSubmit;
        }

        return soleSubmit || allRetrieve &&
            retrievalCallCount + calls.Count <= Pass2RetrievalContracts.MaxCalls;
    }

    private static int MeasureRequest(PreparedPass2ProviderRequest request) =>
        checked(
            Encoding.UTF8.GetByteCount(request.Prompt) +
            request.PacketUtf8.Length +
            request.FunctionDeclarationsUtf8.Length);

    private static bool IsValidToolCallId(string value) =>
        value.Length is >= 1 and <= 256 &&
        value.All(character => character is >= '!' and <= '~');

    private static AIFunctionArguments CreateFunctionArguments(
        JsonElement arguments,
        bool normalizeRetrievalNulls)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (JsonProperty property in arguments.EnumerateObject())
        {
            values[property.Name] = normalizeRetrievalNulls &&
                IsNullableRetrievalField(property.Name) &&
                property.Value.ValueKind == JsonValueKind.String &&
                string.Equals(property.Value.GetString(), "null", StringComparison.Ordinal)
                    ? null
                    : property.Value.Clone();
        }
        return new AIFunctionArguments(values);
    }

    private static bool IsNullableRetrievalField(string name) => name is
        "queryText" or "opaqueHandles" or "direction" or
        "containedByteOffset" or "containedByteLength";

    private static byte[] ComposeProviderContext(
        PreparedPass2WorkerPacket packet,
        ReadOnlySpan<byte> transcript,
        ReadOnlySpan<byte> sanitizedResults,
        ReadOnlySpan<byte> repairContext)
    {
        JsonDocument packetDocument;
        try
        {
            packetDocument = JsonDocument.Parse(packet.ProviderUtf8);
        }
        catch (JsonException)
        {
            return ComposeOpaqueProviderContext(
                packet, transcript, sanitizedResults, repairContext);
        }

        using (packetDocument)
        {
            using JsonDocument transcriptDocument = JsonDocument.Parse(transcript.ToArray());
            using JsonDocument resultsDocument = JsonDocument.Parse(sanitizedResults.ToArray());
            using var destination = new MemoryStream();
            using (var writer = new Utf8JsonWriter(destination))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in packetDocument.RootElement.EnumerateObject())
                {
                    property.WriteTo(writer);
                }

                writer.WritePropertyName("serverAuthority");
                JsonSerializer.Serialize(writer, new
                {
                    pass2JobExecutionId = packet.JobId,
                    flowId = packet.Flow.FlowId,
                    packet.Pass2ExecutionId,
                    packet.FlowGroupId,
                    packet.ExactResolutionDecisionId,
                    packet.AttemptOrdinal,
                    packet.LogicalDocumentStableKey,
                    packet.PlanFingerprint,
                    packet.MembershipFingerprint,
                    packet.PromptIdentity,
                    packet.PromptFingerprint,
                    packet.SubmissionSchemaIdentity,
                    packet.SubmissionSchemaFingerprint,
                    packet.ToolDeclarationIdentity,
                    packet.ToolDeclarationFingerprint,
                });
                writer.WritePropertyName("boundedConversationTranscript");
                transcriptDocument.RootElement.WriteTo(writer);
                writer.WritePropertyName("admittedRetrievalResults");
                resultsDocument.RootElement.WriteTo(writer);
                if (!repairContext.IsEmpty)
                {
                    using JsonDocument repairDocument = JsonDocument.Parse(repairContext.ToArray());
                    writer.WritePropertyName("firstSubmissionCorrection");
                    repairDocument.RootElement.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            return destination.ToArray();
        }
    }

    private static byte[] ComposeOpaqueProviderContext(
        PreparedPass2WorkerPacket packet,
        ReadOnlySpan<byte> transcript,
        ReadOnlySpan<byte> sanitizedResults,
        ReadOnlySpan<byte> repairContext)
    {
        using var destination = new MemoryStream();
        destination.Write(packet.ProviderUtf8.Span);
        WriteSection(
            destination,
            "serverAuthority",
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                pass2JobExecutionId = packet.JobId,
                flowId = packet.Flow.FlowId,
                packet.Pass2ExecutionId,
                packet.FlowGroupId,
                packet.ExactResolutionDecisionId,
                packet.AttemptOrdinal,
                packet.LogicalDocumentStableKey,
                packet.PlanFingerprint,
                packet.MembershipFingerprint,
                packet.PromptIdentity,
                packet.PromptFingerprint,
                packet.SubmissionSchemaIdentity,
                packet.SubmissionSchemaFingerprint,
                packet.ToolDeclarationIdentity,
                packet.ToolDeclarationFingerprint,
            }));
        WriteSection(destination, "boundedConversationTranscript", transcript);
        WriteSection(destination, "admittedRetrievalResults", sanitizedResults);
        if (!repairContext.IsEmpty)
        {
            WriteSection(destination, "firstSubmissionCorrection", repairContext);
        }

        return destination.ToArray();
    }

    private static void WriteSection(
        Stream destination,
        string name,
        ReadOnlySpan<byte> content)
    {
        destination.Write(Encoding.UTF8.GetBytes($"\n{name}:"));
        destination.Write(content);
    }

    private static byte[] SerializeTranscript(
        IReadOnlyList<PreparedPass2TranscriptEntry> entries) =>
        SerializeEntries(entries, includeContent: false);

    private static byte[] SerializeSanitizedResults(
        IReadOnlyList<PreparedPass2TranscriptEntry> entries) =>
        SerializeEntries(entries, includeContent: true);

    private static byte[] SerializeEntries(
        IReadOnlyList<PreparedPass2TranscriptEntry> entries,
        bool includeContent)
    {
        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            writer.WriteStartArray();
            foreach (PreparedPass2TranscriptEntry entry in entries)
            {
                writer.WriteStartObject();
                writer.WriteString("id", entry.Id);
                writer.WriteString("name", entry.Name);
                writer.WritePropertyName("arguments");
                entry.Arguments.WriteTo(writer);
                if (entry.Retrieval is { } retrieval)
                {
                    writer.WriteString("outcome", retrieval.Outcome);
                    writer.WriteString("code", retrieval.Code);
                    writer.WriteStartArray("items");
                    foreach (Pass2CodeLogicRetrievalProviderProjection item in retrieval.Items)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("opaqueHandle", item.OpaqueHandle);
                        writer.WriteString("kind", item.Kind.ToString());
                        writer.WriteString("documentVersionId", item.DocumentVersionId);
                        if (includeContent)
                        {
                            if (item.ContentMarkdown is not null)
                            {
                                writer.WriteString("content", item.ContentMarkdown);
                            }

                            if (item.Excerpt is not null)
                            {
                                writer.WriteString("excerpt", item.Excerpt);
                            }
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }
                else if (entry.SubmissionDisposition is { } disposition)
                {
                    writer.WriteString("submissionDisposition", disposition.ToString());
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return destination.ToArray();
    }

    private static byte[] SerializeRepairContext(
        PreparedPass2SubmissionDisposition? disposition) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            firstSubmissionCorrection = true,
            category = (disposition ?? PreparedPass2SubmissionDisposition.InvalidSemantics)
                .ToString(),
            repairCode = "UseMinimalValidDocument",
        });

    private sealed record PreparedPass2ToolCall(
        string Id,
        string Name,
        JsonElement Arguments);

    private sealed record PreparedPass2TranscriptEntry(
        string Id,
        string Name,
        JsonElement Arguments,
        PreparedRetrieveCodeLogicResponse? Retrieval,
        PreparedPass2SubmissionDisposition? SubmissionDisposition);
}

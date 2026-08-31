using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents.ChatExecution;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;

namespace Vulgata.Agents;

public static class DependencyInjection
{
    public static IServiceCollection AddPreparedPass2Agents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<ChatProviderReasoningOptions>();
        services.AddScoped<ChatStreamRuntimeObservationRecorder>();
        services.AddScoped<IChatStreamCompletionSummaryRecorder>(provider =>
            provider.GetRequiredService<ChatStreamRuntimeObservationRecorder>());
        services.AddScoped<IChatProviderStreamingTransport,
            OpenAiCompatibleChatProviderStreamingTransport>();
        services.AddScoped<IPreparedPass2StreamingTransport,
            OpenAiCompatiblePass2StreamingTransport>();
        services.AddScoped<PreparedRetrieveCodeLogicFunction>();
        services.AddScoped<PreparedSubmitBusinessLogicFunction>();
        services.AddScoped<PreparedPass2JobExecutor>();
        services.AddScoped<IPreparedPass2WorkerContractAuthority,
            PreparedPass2WorkerContractAuthoritySource>();
        services.AddScoped<PreparedPass2WorkerDispatcher>();
        services.AddScoped<IPreparedPass2WorkerDispatcher>(provider =>
            provider.GetRequiredService<PreparedPass2WorkerDispatcher>());
        return services;
    }
}

internal sealed class OpenAiCompatiblePass2StreamingTransport(
    WorkerAgentProviderResolver providerResolver,
    ISecretEncryptionService encryptionService,
    HttpMessageHandler httpMessageHandler) : IPreparedPass2StreamingTransport
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
        PreparedPass2ProviderRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guid systemId = ReadSystemId(request.PacketUtf8.Span);
        ResolvedWorkerAgentProvider provider = await providerResolver.ResolveAsync(
            systemId, cancellationToken);
        string apiKey = await encryptionService.DecryptAsync(
            SecretPurposes.LlmApiKey,
            provider.EncryptedApiKey,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("The Worker Agent credential is unavailable.");
        }

        byte[] body = CreateRequestBody(provider.Model, request);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            ResolveChatCompletionsEndpoint(provider.Endpoint));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        message.Content = new ByteArrayContent(body);
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8",
        };

        using var client = new HttpClient(httpMessageHandler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        using HttpResponseMessage response = await client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var calls = new Dictionary<int, ToolCallBuilder>();
        await foreach (string line in ReadBoundedLinesAsync(stream, cancellationToken))
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            string payload = line[5..].TrimStart();
            if (payload.Length == 0)
            {
                continue;
            }

            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                break;
            }

            CollectToolCallDeltas(payload, calls);
            // Empty updates preserve streaming liveness without exposing partial,
            // invalid JSON to the strict executor's assembled response parser.
            yield return ReadOnlyMemory<byte>.Empty;
        }

        yield return SerializeCalls(calls);
    }

    private static async IAsyncEnumerable<string> ReadBoundedLinesAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int maximum = PreparedPass2WireBudget.MaximumRawResponseUtf8Bytes;
        byte[] buffer = new byte[8_192];
        using var line = new MemoryStream();
        int consumed = 0;
        while (true)
        {
            int remaining = maximum - consumed;
            if (remaining <= 0)
            {
                throw new InvalidDataException("Provider response exceeds its byte bound.");
            }

            int read = await stream.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (line.Length > 0)
                {
                    yield return DecodeLine(line);
                }
                yield break;
            }

            for (int index = 0; index < read; index++)
            {
                consumed++;
                if (consumed >= maximum)
                {
                    throw new InvalidDataException("Provider response exceeds its byte bound.");
                }

                byte value = buffer[index];
                if (value == (byte)'\n')
                {
                    yield return DecodeLine(line);
                    line.SetLength(0);
                }
                else
                {
                    line.WriteByte(value);
                }
            }
        }
    }

    private static string DecodeLine(MemoryStream line)
    {
        ReadOnlySpan<byte> bytes = line.GetBuffer().AsSpan(0, checked((int)line.Length));
        if (!bytes.IsEmpty && bytes[^1] == (byte)'\r')
        {
            bytes = bytes[..^1];
        }
        return _strictUtf8.GetString(bytes);
    }

    private static Guid ReadSystemId(ReadOnlySpan<byte> providerContext)
    {
        ReadOnlySpan<byte> delimiter = "\nserverAuthority:"u8;
        int end = providerContext.IndexOf(delimiter);
        ReadOnlySpan<byte> packet = end < 0 ? providerContext : providerContext[..end];
        using JsonDocument document = JsonDocument.Parse(packet.ToArray());
        if (!document.RootElement.TryGetProperty("flow", out JsonElement flow) ||
            flow.ValueKind != JsonValueKind.Object ||
            (!flow.TryGetProperty("systemId", out JsonElement systemIdElement) &&
             !flow.TryGetProperty("SystemId", out systemIdElement)))
        {
            throw new InvalidDataException("The Pass 2 System binding is unavailable.");
        }

        string? value = systemIdElement.ValueKind == JsonValueKind.String
            ? systemIdElement.GetString()
            : null;
        return Guid.TryParse(value, out Guid systemId) && systemId != Guid.Empty
            ? systemId
            : throw new InvalidDataException("The Pass 2 System binding is unavailable.");
    }

    private static byte[] CreateRequestBody(
        string model,
        PreparedPass2ProviderRequest request)
    {
        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            writer.WriteStartObject();
            writer.WriteString("model", model);
            writer.WriteBoolean("stream", true);
            writer.WritePropertyName("thinking");
            writer.WriteStartObject();
            writer.WriteString("type", "disabled");
            writer.WriteEndObject();
            writer.WriteStartArray("messages");
            WriteMessage(writer, "system", request.Prompt);
            WriteMessage(writer, "user", _strictUtf8.GetString(request.PacketUtf8.Span));
            WriteToolHistory(writer, request);
            writer.WriteEndArray();
            writer.WritePropertyName("tools");
            writer.WriteRawValue(request.FunctionDeclarationsUtf8.Span, skipInputValidation: false);
            writer.WritePropertyName("tool_choice");
            if (request.SubmissionOnly)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "function");
                writer.WritePropertyName("function");
                writer.WriteStartObject();
                writer.WriteString("name", Pass2StrictToolDeclarations.SubmitBusinessLogicName);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteStringValue("required");
            }

            writer.WriteEndObject();
        }

        return destination.ToArray();
    }

    private static void WriteMessage(Utf8JsonWriter writer, string role, string content)
    {
        writer.WriteStartObject();
        writer.WriteString("role", role);
        writer.WriteString("content", content);
        writer.WriteEndObject();
    }

    private static void WriteToolHistory(
        Utf8JsonWriter writer,
        PreparedPass2ProviderRequest request)
    {
        if (request.TranscriptUtf8.IsEmpty && request.SanitizedToolResultsUtf8.IsEmpty)
        {
            return;
        }
        if (request.TranscriptUtf8.IsEmpty || request.SanitizedToolResultsUtf8.IsEmpty)
        {
            throw new InvalidDataException("The Pass 2 tool history is incomplete.");
        }

        using JsonDocument transcript = JsonDocument.Parse(request.TranscriptUtf8);
        using JsonDocument results = JsonDocument.Parse(request.SanitizedToolResultsUtf8);
        if (transcript.RootElement.ValueKind != JsonValueKind.Array ||
            results.RootElement.ValueKind != JsonValueKind.Array ||
            transcript.RootElement.GetArrayLength() != results.RootElement.GetArrayLength())
        {
            throw new InvalidDataException("The Pass 2 tool history is invalid.");
        }

        for (int index = 0; index < transcript.RootElement.GetArrayLength(); index++)
        {
            JsonElement entry = transcript.RootElement[index];
            JsonElement result = results.RootElement[index];
            string? id = entry.TryGetProperty("id", out JsonElement idValue) &&
                idValue.ValueKind == JsonValueKind.String
                    ? idValue.GetString()
                    : null;
            string? name = entry.TryGetProperty("name", out JsonElement nameValue) &&
                nameValue.ValueKind == JsonValueKind.String
                    ? nameValue.GetString()
                    : null;
            string? resultId = result.TryGetProperty("id", out JsonElement resultIdValue) &&
                resultIdValue.ValueKind == JsonValueKind.String
                    ? resultIdValue.GetString()
                    : null;
            bool hasArguments = entry.TryGetProperty(
                "arguments", out JsonElement arguments) &&
                arguments.ValueKind == JsonValueKind.Object;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) ||
                !string.Equals(id, resultId, StringComparison.Ordinal) || !hasArguments)
            {
                throw new InvalidDataException("The Pass 2 tool history identity is invalid.");
            }

            writer.WriteStartObject();
            writer.WriteString("role", "assistant");
            writer.WriteNull("content");
            writer.WriteStartArray("tool_calls");
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("type", "function");
            writer.WriteStartObject("function");
            writer.WriteString("name", name);
            writer.WriteString("arguments", arguments.GetRawText());
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteStartObject();
            writer.WriteString("role", "tool");
            writer.WriteString("tool_call_id", id);
            writer.WriteString("content", result.GetRawText());
            writer.WriteEndObject();
        }
    }

    private static Uri ResolveChatCompletionsEndpoint(Uri configured)
    {
        if (configured.AbsolutePath.EndsWith(
                "/chat/completions",
                StringComparison.OrdinalIgnoreCase))
        {
            return configured;
        }

        var builder = new UriBuilder(configured)
        {
            Path = configured.AbsolutePath.TrimEnd('/') + "/chat/completions",
            Query = string.Empty,
            Fragment = string.Empty,
        };
        return builder.Uri;
    }

    private static void CollectToolCallDeltas(
        string payload,
        IDictionary<int, ToolCallBuilder> calls)
    {
        using JsonDocument document = JsonDocument.Parse(payload);
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("error", out _))
        {
            throw new InvalidOperationException("The provider returned an error response.");
        }

        if (!root.TryGetProperty("choices", out JsonElement choices) ||
            choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0 ||
            !choices[0].TryGetProperty("delta", out JsonElement delta) ||
            !delta.TryGetProperty("tool_calls", out JsonElement toolCalls) ||
            toolCalls.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement toolCall in toolCalls.EnumerateArray())
        {
            int index = toolCall.GetProperty("index").GetInt32();
            if (!calls.TryGetValue(index, out ToolCallBuilder? value))
            {
                value = new();
                calls.Add(index, value);
            }

            if (toolCall.TryGetProperty("id", out JsonElement id) &&
                id.ValueKind == JsonValueKind.String)
            {
                value.Id = id.GetString() ?? string.Empty;
            }

            if (toolCall.TryGetProperty("function", out JsonElement function))
            {
                if (function.TryGetProperty("name", out JsonElement name) &&
                    name.ValueKind == JsonValueKind.String)
                {
                    value.Name.Append(name.GetString());
                }

                if (function.TryGetProperty("arguments", out JsonElement arguments) &&
                    arguments.ValueKind == JsonValueKind.String)
                {
                    value.Arguments.Append(arguments.GetString());
                }
            }
        }
    }

    private static byte[] SerializeCalls(IReadOnlyDictionary<int, ToolCallBuilder> calls)
    {
        using var destination = new MemoryStream();
        using (var writer = new Utf8JsonWriter(destination))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("toolCalls");
            foreach (ToolCallBuilder call in calls.OrderBy(value => value.Key)
                .Select(value => value.Value))
            {
                writer.WriteStartObject();
                writer.WriteString("id", call.Id);
                writer.WriteString("name", call.Name.ToString());
                writer.WritePropertyName("arguments");
                writer.WriteRawValue(call.Arguments.ToString(), skipInputValidation: false);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return destination.ToArray();
    }

    private sealed class ToolCallBuilder
    {
        internal string Id { get; set; } = string.Empty;
        internal StringBuilder Name { get; } = new();
        internal StringBuilder Arguments { get; } = new();
    }
}

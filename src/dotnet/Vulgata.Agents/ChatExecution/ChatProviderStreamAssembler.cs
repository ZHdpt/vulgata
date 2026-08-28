using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.ChatExecution;

internal static class ChatProviderStreamAssembler
{
    private const int _maximumRawBodyBytes = 1_048_576;
    private const int _maximumSseLineBytes = 131_072;
    private const int _maximumSseEventBytes = 131_072;
    private const int _maximumAnswerDeltaBytes = 16_384;
    private const int _maximumAnswerBytes = 65_536;
    private const int _decoderInputSliceBytes = 16_384;
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    public static async IAsyncEnumerable<ChatProviderStreamSignal> StreamAsync(
        IAsyncEnumerable<ReadOnlyMemory<byte>> body,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        using var rawBody = new MemoryStream();
        var pendingText = new StringBuilder();
        Decoder decoder = _strictUtf8.GetDecoder();
        char[] characters = ArrayPool<char>.Shared.Rent(
            _strictUtf8.GetMaxCharCount(_decoderInputSliceBytes));
        bool terminalObserved = false;

        try
        {
            await foreach (ReadOnlyMemory<byte> chunk in body
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (chunk.Length > _maximumRawBodyBytes - rawBody.Length)
                {
                    yield return ProviderError("raw-body-overflow");
                    yield break;
                }

                await rawBody.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
                ReadOnlyMemory<byte> remaining = chunk;
                while (!remaining.IsEmpty)
                {
                    int length = Math.Min(remaining.Length, _decoderInputSliceBytes);
                    decoder.Convert(
                        remaining.Span[..length],
                        characters,
                        flush: false,
                        out int bytesUsed,
                        out int charsUsed,
                        out _);
                    pendingText.Append(characters, 0, charsUsed);
                    remaining = remaining[bytesUsed..];
                }

                while (TryTakeCompletedEvent(pendingText, out string? completedEvent))
                {
                    ChatProviderStreamAssemblyResult eventResult = await AssembleAsync(
                        SingleChunkAsync(_strictUtf8.GetBytes(
                            completedEvent + "data: [DONE]\n\n")),
                        cancellationToken).ConfigureAwait(false);

                    if (!eventResult.Accepted)
                    {
                        if (StringComparer.Ordinal.Equals(
                                eventResult.RejectionCategory,
                                "duplicate-terminal"))
                        {
                            if (terminalObserved)
                            {
                                yield return ProviderError("duplicate-terminal");
                                yield break;
                            }

                            terminalObserved = true;
                            continue;
                        }

                        yield return ProviderError(
                            eventResult.RejectionCategory ?? "invalid-provider-stream");
                        yield break;
                    }

                    if (!string.IsNullOrEmpty(eventResult.Answer))
                    {
                        if (terminalObserved)
                        {
                            yield return ProviderError("post-terminal-data");
                            yield break;
                        }

                        yield return new ChatProviderStreamSignal(
                            ChatProviderStreamSignalKind.AnswerDelta,
                            _strictUtf8.GetBytes(eventResult.Answer));
                    }
                }
            }

            decoder.Convert(
                ReadOnlySpan<byte>.Empty,
                characters,
                flush: true,
                out _,
                out int finalChars,
                out _);
            pendingText.Append(characters, 0, finalChars);
        }
        finally
        {
            Array.Clear(characters);
            ArrayPool<char>.Shared.Return(characters);
        }

        ChatProviderStreamAssemblyResult finalResult = await AssembleAsync(
            SingleChunkAsync(rawBody.ToArray()),
            cancellationToken).ConfigureAwait(false);
        if (!finalResult.Accepted)
        {
            yield return ProviderError(
                finalResult.RejectionCategory ?? "invalid-provider-stream");
            yield break;
        }

        yield return new ChatProviderStreamSignal(
            ChatProviderStreamSignalKind.Terminal,
            ReadOnlyMemory<byte>.Empty);
    }

    public static async Task<ChatProviderStreamAssemblyResult> AssembleAsync(
        IAsyncEnumerable<ReadOnlyMemory<byte>> body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        int rawBodyByteCount = 0;
        var decodedBody = new StringBuilder();
        Decoder decoder = _strictUtf8.GetDecoder();
        char[] characters = ArrayPool<char>.Shared.Rent(
            _strictUtf8.GetMaxCharCount(_decoderInputSliceBytes));

        try
        {
            await foreach (ReadOnlyMemory<byte> chunk in body
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (chunk.Length > _maximumRawBodyBytes - rawBodyByteCount)
                {
                    return Rejected("raw-body-overflow", rawBodyByteCount + chunk.Length);
                }

                rawBodyByteCount += chunk.Length;
                ReadOnlyMemory<byte> remaining = chunk;
                while (!remaining.IsEmpty)
                {
                    int length = Math.Min(remaining.Length, _decoderInputSliceBytes);
                    ReadOnlySpan<byte> input = remaining.Span[..length];
                    decoder.Convert(
                        input,
                        characters,
                        flush: false,
                        out int bytesUsed,
                        out int charsUsed,
                        out _);
                    decodedBody.Append(characters, 0, charsUsed);
                    remaining = remaining[bytesUsed..];
                }
            }

            decoder.Convert(
                ReadOnlySpan<byte>.Empty,
                characters,
                flush: true,
                out _,
                out int finalChars,
                out _);
            decodedBody.Append(characters, 0, finalChars);
        }
        catch (DecoderFallbackException)
        {
            return Rejected("invalid-wire-utf8", rawBodyByteCount);
        }
        finally
        {
            Array.Clear(characters);
            ArrayPool<char>.Shared.Return(characters);
        }

        return ParseSse(decodedBody.ToString(), rawBodyByteCount);
    }

    private static ChatProviderStreamAssemblyResult ParseSse(
        string body,
        int rawBodyByteCount)
    {
        var answer = new StringBuilder();
        var eventData = new List<string>();
        int eventByteCount = 0;
        var state = new AssemblyState { RawBodyByteCount = rawBodyByteCount };

        foreach ((string line, int terminatorBytes) in EnumerateLines(body))
        {
            int lineByteCount = _strictUtf8.GetByteCount(line);
            if (lineByteCount > _maximumSseLineBytes)
            {
                return Rejected("sse-line-overflow");
            }

            if (line.Length == 0)
            {
                ChatProviderStreamAssemblyResult? failure = DispatchEvent(
                    eventData,
                    state,
                    answer);
                if (failure is not null)
                {
                    return failure;
                }

                eventData.Clear();
                eventByteCount = 0;
                continue;
            }

            eventByteCount += lineByteCount + terminatorBytes;
            if (eventByteCount > _maximumSseEventBytes)
            {
                return Rejected("sse-event-overflow");
            }

            if (line[0] == ':')
            {
                continue;
            }

            int separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                return Rejected("invalid-sse");
            }

            string field = line[..separator];
            if (!IsValidFieldName(field))
            {
                return Rejected("invalid-sse");
            }

            string value = line[(separator + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            if (StringComparer.Ordinal.Equals(field, "data"))
            {
                eventData.Add(value);
            }
        }

        if (state.TerminalCount == 0)
        {
            return Rejected("missing-terminal");
        }

        if (eventData.Count > 0)
        {
            return Rejected("missing-terminal");
        }

        if (state.FinishReason is not null &&
            !StringComparer.Ordinal.Equals(state.FinishReason, "stop"))
        {
            return Rejected("contradictory-terminal");
        }

        return ChatProviderStreamAssemblyResult.AcceptedResult(
            answer.ToString(),
            state.AnswerDeltaCount,
            state.AnswerDeltaByteCount,
            state.AssembledAnswerByteCount,
            state.TerminalCount,
            state.RawBodyByteCount);

        ChatProviderStreamAssemblyResult Rejected(string category) =>
            ChatProviderStreamAssemblyResult.RejectedResult(
                category,
                state.AnswerDeltaCount,
                state.AnswerDeltaByteCount,
                state.AssembledAnswerByteCount,
                state.TerminalCount,
                state.RawBodyByteCount);
    }

    private static ChatProviderStreamAssemblyResult? DispatchEvent(
        List<string> eventData,
        AssemblyState state,
        StringBuilder answer)
    {
        if (eventData.Count == 0)
        {
            return null;
        }

        string payload = string.Join('\n', eventData);
        if (StringComparer.Ordinal.Equals(payload, "[DONE]"))
        {
            state.TerminalCount++;
            return state.TerminalCount == 1
                ? null
                : Rejected("duplicate-terminal");
        }

        if (HasInvalidJsonEscape(payload))
        {
            return Rejected("invalid-json-escape");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32,
            });
        }
        catch (JsonException)
        {
            return Rejected(
                payload.Contains('\\', StringComparison.Ordinal)
                    ? "invalid-json-escape"
                    : "invalid-json-event");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || HasDuplicateProperties(root))
            {
                return Rejected("invalid-json-event");
            }

            if (root.TryGetProperty("error", out JsonElement error) &&
                error.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                return Rejected(
                    state.TerminalCount == 0 ? "provider-error" : "post-terminal-data");
            }

            if (!root.TryGetProperty("choices", out JsonElement choices))
            {
                return null;
            }

            if (choices.ValueKind != JsonValueKind.Array)
            {
                return Rejected("invalid-json-event");
            }

            foreach (JsonElement choice in choices.EnumerateArray())
            {
                if (choice.ValueKind != JsonValueKind.Object || HasDuplicateProperties(choice))
                {
                    return Rejected("invalid-json-event");
                }

                if (choice.TryGetProperty("finish_reason", out JsonElement finish) &&
                    finish.ValueKind != JsonValueKind.Null)
                {
                    if (finish.ValueKind != JsonValueKind.String)
                    {
                        return Rejected("contradictory-terminal");
                    }

                    string currentFinishReason = finish.GetString() ?? string.Empty;
                    if (state.FinishReason is not null &&
                        !StringComparer.Ordinal.Equals(state.FinishReason, currentFinishReason))
                    {
                        return Rejected("contradictory-terminal");
                    }

                    state.FinishReason = currentFinishReason;
                    if (!StringComparer.Ordinal.Equals(state.FinishReason, "stop"))
                    {
                        return Rejected("contradictory-terminal");
                    }
                }

                if (!choice.TryGetProperty("delta", out JsonElement delta) ||
                    delta.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (delta.ValueKind != JsonValueKind.Object || HasDuplicateProperties(delta))
                {
                    return Rejected("invalid-json-event");
                }

                if (!delta.TryGetProperty("content", out JsonElement content) ||
                    content.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (content.ValueKind != JsonValueKind.String)
                {
                    return Rejected("invalid-json-event");
                }

                string decodedDelta = content.GetString() ?? string.Empty;
                if (decodedDelta.Length == 0)
                {
                    continue;
                }

                int deltaByteCount;
                try
                {
                    deltaByteCount = _strictUtf8.GetByteCount(decodedDelta);
                }
                catch (EncoderFallbackException)
                {
                    return Rejected("invalid-json-escape");
                }

                if (state.TerminalCount > 0)
                {
                    return Rejected("post-terminal-data");
                }

                if (deltaByteCount > _maximumAnswerDeltaBytes)
                {
                    return Rejected("chunk-overflow");
                }

                if (deltaByteCount > _maximumAnswerBytes - state.AssembledAnswerByteCount)
                {
                    return Rejected("total-overflow");
                }

                answer.Append(decodedDelta);
                state.AnswerDeltaCount++;
                state.AnswerDeltaByteCount += deltaByteCount;
                state.AssembledAnswerByteCount += deltaByteCount;
            }
        }

        return null;

        ChatProviderStreamAssemblyResult Rejected(string category) =>
            ChatProviderStreamAssemblyResult.RejectedResult(
                category,
                state.AnswerDeltaCount,
                state.AnswerDeltaByteCount,
                state.AssembledAnswerByteCount,
                state.TerminalCount,
                state.RawBodyByteCount);
    }

    private sealed class AssemblyState
    {
        public int AnswerDeltaCount { get; set; }

        public int AnswerDeltaByteCount { get; set; }

        public int AssembledAnswerByteCount { get; set; }

        public int TerminalCount { get; set; }

        public int RawBodyByteCount { get; init; }

        public string? FinishReason { get; set; }
    }

    private static IEnumerable<(string Line, int TerminatorBytes)> EnumerateLines(string body)
    {
        int start = 0;
        for (int index = 0; index < body.Length; index++)
        {
            char current = body[index];
            if (current is not ('\r' or '\n'))
            {
                continue;
            }

            int terminatorBytes = 1;
            if (current == '\r' && index + 1 < body.Length && body[index + 1] == '\n')
            {
                terminatorBytes = 2;
            }

            yield return (body[start..index], terminatorBytes);
            index += terminatorBytes - 1;
            start = index + 1;
        }

        if (start < body.Length)
        {
            yield return (body[start..], 0);
        }
    }

    private static bool IsValidFieldName(string field)
    {
        foreach (char value in field)
        {
            if (!(char.IsAsciiLetter(value) || value is '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        var properties = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!properties.Add(property.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasInvalidJsonEscape(string json)
    {
        bool insideString = false;
        for (int index = 0; index < json.Length; index++)
        {
            char current = json[index];
            if (current == '"')
            {
                insideString = !insideString;
                continue;
            }

            if (!insideString || current != '\\')
            {
                continue;
            }

            if (++index >= json.Length)
            {
                return true;
            }

            char escape = json[index];
            if (escape is '"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't')
            {
                continue;
            }

            if (escape != 'u' || !TryReadHexCodeUnit(json, index + 1, out int codeUnit))
            {
                return true;
            }

            index += 4;
            if (codeUnit is >= 0xD800 and <= 0xDBFF)
            {
                if (index + 6 >= json.Length || json[index + 1] != '\\' ||
                    json[index + 2] != 'u' ||
                    !TryReadHexCodeUnit(json, index + 3, out int lowSurrogate) ||
                    lowSurrogate is < 0xDC00 or > 0xDFFF)
                {
                    return true;
                }

                index += 6;
            }
            else if (codeUnit is >= 0xDC00 and <= 0xDFFF)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadHexCodeUnit(string value, int start, out int codeUnit)
    {
        codeUnit = 0;
        if (start + 4 > value.Length)
        {
            return false;
        }

        for (int index = start; index < start + 4; index++)
        {
            int digit = value[index] switch
            {
                >= '0' and <= '9' => value[index] - '0',
                >= 'a' and <= 'f' => value[index] - 'a' + 10,
                >= 'A' and <= 'F' => value[index] - 'A' + 10,
                _ => -1,
            };
            if (digit < 0)
            {
                return false;
            }

            codeUnit = (codeUnit << 4) | digit;
        }

        return true;
    }

    private static bool TryTakeCompletedEvent(
        StringBuilder pending,
        out string completedEvent)
    {
        int lineStart = 0;
        for (int index = 0; index < pending.Length; index++)
        {
            char current = pending[index];
            if (current is not ('\r' or '\n'))
            {
                continue;
            }

            int terminatorLength = current == '\r' &&
                index + 1 < pending.Length && pending[index + 1] == '\n'
                ? 2
                : 1;
            if (index == lineStart)
            {
                int eventLength = index + terminatorLength;
                completedEvent = pending.ToString(0, eventLength);
                pending.Remove(0, eventLength);
                return true;
            }

            index += terminatorLength - 1;
            lineStart = index + 1;
        }

        completedEvent = string.Empty;
        return false;
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> SingleChunkAsync(
        byte[] content)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield return content;
    }

    private static ChatProviderStreamSignal ProviderError(string category) =>
        new(
            ChatProviderStreamSignalKind.ProviderError,
            ReadOnlyMemory<byte>.Empty,
            category);

    private static ChatProviderStreamAssemblyResult Rejected(
        string category,
        int rawBodyByteCount = 0) =>
        ChatProviderStreamAssemblyResult.RejectedResult(
            category,
            0,
            0,
            0,
            0,
            rawBodyByteCount);
}

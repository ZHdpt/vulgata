using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared;

namespace Vulgata.Agents.ChatExecution;

internal sealed class OpenAiCompatibleChatProviderStreamingTransport(
    ILlmProviderRepository providerRepository,
    ISecretEncryptionService encryptionService,
    HttpMessageHandler httpMessageHandler,
    ChatStreamRuntimeObservationRecorder observationRecorder) :
    IChatProviderStreamingTransport
{
    private const int _maximumRawBodyBytes = 1_048_576;
    private const int _maximumReadBytes = 16_384;
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    public async IAsyncEnumerable<ChatProviderStreamSignal> StreamAsync(
        ChatProviderStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var observation = new ObservationState
        {
            StreamStartedAtUtc = DateTimeOffset.UtcNow,
            OverallDeadlineAtUtc = ResolveDeadline(
                request.AbsoluteDeadlineUnixTimeMilliseconds),
        };
        ChatProviderStreamSignal? failureSignal = null;
        await using IAsyncEnumerator<ChatProviderStreamSignal> signals = ExecuteAsync(
                request,
                observation,
                cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool hasSignal;
                try
                {
                    hasSignal = await signals.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    observation.Outcome = "cancelled";
                    observation.RejectionCategory = "cancelled";
                    observation.PhysicalEofDisposition = "cancelled";
                    throw;
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or IOException or InvalidDataException or
                    JsonException or DecoderFallbackException or InvalidOperationException or
                    TimeoutException)
                {
                    observation.RejectionCategory = Classify(exception);
                    observation.Outcome = observation.RejectionCategory;
                    if (observation.Outcome == "disconnect")
                    {
                        observation.PhysicalEofDisposition = "disconnect";
                    }
                    failureSignal = new ChatProviderStreamSignal(
                        ChatProviderStreamSignalKind.ProviderError,
                        ReadOnlyMemory<byte>.Empty,
                        observation.RejectionCategory);
                    break;
                }

                if (!hasSignal)
                {
                    break;
                }

                yield return signals.Current;
            }
        }
        finally
        {
            RecordObservation(request, observation);
        }

        if (failureSignal is not null)
        {
            yield return failureSignal;
        }
    }

    private async IAsyncEnumerable<ChatProviderStreamSignal> ExecuteAsync(
        ChatProviderStreamRequest request,
        ObservationState observation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.ProviderIdentity, out Guid providerId) ||
            providerId == Guid.Empty)
        {
            throw new InvalidDataException("The selected Chat provider identity is invalid.");
        }

        LlmProvider? provider = await providerRepository.GetByIdAsync(
            providerId,
            cancellationToken).ConfigureAwait(false);
        if (provider is null ||
            (provider.SupportedApiTypes & ApiType.ChatCompletions) == 0 ||
            !Uri.TryCreate(provider.BaseEndpointUrl, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("The selected Chat provider is unavailable.");
        }

        string apiKey = await encryptionService.DecryptAsync(
            SecretPurposes.LlmApiKey,
            provider.EncryptedApiKey,
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("The selected Chat provider credential is unavailable.");
        }

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            model = provider.Name,
            stream = true,
            messages = new[] { new { role = "user", content = request.Prompt } },
        });
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            ResolveChatCompletionsEndpoint(endpoint));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        message.Content = new ByteArrayContent(body);
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8",
        };

        using var deadline = CreateDeadline(request.AbsoluteDeadlineUnixTimeMilliseconds);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);
        using var client = new HttpClient(httpMessageHandler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        using HttpResponseMessage response = await client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            linked.Token).ConfigureAwait(false);
        observation.ResponseHeaderDisposition = "received";
        observation.ResponseStatusCode = (int)response.StatusCode;
        observation.ResponseMediaType = response.Content.Headers.ContentType?.MediaType?
            .ToLowerInvariant();
        observation.NegotiatedProtocol = response.Version.Major switch
        {
            1 => "HTTP/1.1",
            2 => "HTTP/2",
            3 => "HTTP/3",
            _ => null,
        };
        observation.ResponseHeadersAtUtc = DateTimeOffset.UtcNow;
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                "The Chat provider returned a non-success status.",
                inner: null,
                response.StatusCode);
        }

        if (!StringComparer.Ordinal.Equals(
                observation.ResponseMediaType,
                "text/event-stream"))
        {
            throw new InvalidDataException("The Chat provider response media type is invalid.");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(linked.Token)
            .ConfigureAwait(false);
        await using IAsyncEnumerator<ChatProviderStreamSignal> assembled =
            ChatProviderStreamAssembler.StreamAsync(
                    ReadRawBodyAsync(stream, observation, linked.Token),
                    linked.Token)
                .GetAsyncEnumerator(linked.Token);
        while (true)
        {
            bool hasSignal;
            try
            {
                hasSignal = await assembled.MoveNextAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (
                deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("The Chat streaming deadline expired.");
            }

            if (!hasSignal)
            {
                break;
            }

            ChatProviderStreamSignal signal = assembled.Current;
            observation.ProviderChunkCount = signal.ProviderChunkCount;
            observation.MaximumReceivedProviderChunkByteCount =
                signal.MaximumReceivedProviderChunkByteCount;
            observation.AssembledReceivedProviderResponseByteCount =
                signal.AssembledReceivedProviderResponseByteCount;
            if (signal.Kind == ChatProviderStreamSignalKind.ProviderError)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (deadline.IsCancellationRequested)
                {
                    throw new TimeoutException("The Chat streaming deadline expired.");
                }

                observation.RejectionCategory = NormalizeTransportCategory(
                    signal.FailureCategory);
                observation.Outcome = observation.RejectionCategory;
                yield return signal;
                yield break;
            }

            if (signal.Kind == ChatProviderStreamSignalKind.Terminal)
            {
                observation.Outcome = "completed";
                observation.TerminalAtUtc = DateTimeOffset.UtcNow;
                observation.TerminalEventCount++;
                if (observation.PhysicalEofAtUtc is not null)
                {
                    observation.PhysicalEofDisposition = "observed-after-terminal";
                }
                observation.AcceptedAnswerDurationMilliseconds = Math.Max(
                    0,
                    (long)(observation.TerminalAtUtc.Value -
                        observation.StreamStartedAtUtc).TotalMilliseconds);
            }
            else if (signal.Kind == ChatProviderStreamSignalKind.AnswerDelta)
            {
                DateTimeOffset observedAt = DateTimeOffset.UtcNow;
                observation.FirstDataAtUtc ??= observedAt;
                observation.AnswerDeltaCount++;
                observation.MaximumAnswerDeltaByteCount = Math.Max(
                    observation.MaximumAnswerDeltaByteCount,
                    signal.AnswerDelta.Length);
                observation.AssembledAnswerByteCount += signal.AnswerDelta.Length;
            }

            yield return signal;
        }
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadRawBodyAsync(
        Stream stream,
        ObservationState observation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[_maximumReadBytes];
        while (true)
        {
            int remaining = _maximumRawBodyBytes - observation.RawBodyByteCount;
            if (remaining == 0)
            {
                int overflowRead = await stream.ReadAsync(
                    buffer.AsMemory(0, 1),
                    cancellationToken).ConfigureAwait(false);
                observation.RawReadCount++;
                observation.MaximumRawReadByteCount = Math.Max(
                    observation.MaximumRawReadByteCount,
                    overflowRead);
                if (overflowRead == 0)
                {
                    observation.PhysicalEofAtUtc = DateTimeOffset.UtcNow;
                    observation.PhysicalEofDisposition = "observed-before-terminal";
                    yield break;
                }

                throw new RawBodyOverflowException(
                    "The Chat provider response exceeds its raw byte bound.");
            }

            int read = await stream.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                cancellationToken).ConfigureAwait(false);
            observation.RawReadCount++;
            observation.MaximumRawReadByteCount = Math.Max(
                observation.MaximumRawReadByteCount,
                read);
            if (read == 0)
            {
                observation.PhysicalEofAtUtc = DateTimeOffset.UtcNow;
                observation.PhysicalEofDisposition = "observed-before-terminal";
                yield break;
            }

            if (observation.FirstPhysicalReadDisposition == "not-observed")
            {
                observation.FirstPhysicalReadDisposition = "observed";
                observation.FirstPhysicalReadAtUtc = DateTimeOffset.UtcNow;
            }

            observation.RawBodyByteCount += read;
            observation.ObserveSseBytes(buffer.AsSpan(0, read));
            yield return buffer.AsMemory(0, read).ToArray();
        }
    }

    private void RecordObservation(
        ChatProviderStreamRequest request,
        ObservationState state)
    {
        state.FinalizeSseCounters();
        if (!IsSha256(request.ProviderConfigurationFingerprint))
        {
            return;
        }

        _ = observationRecorder.Record((runtimeObservationId, observationSequence) =>
            new ChatStreamRuntimeObservation(
            runtimeObservationId,
            observationSequence,
            request.RequestId,
            request.AttemptKind,
            request.ProviderIdentity,
            "chat-completions",
            request.ProviderConfigurationFingerprint,
            request.StreamingRequestFingerprint,
            true,
            state.ResponseHeaderDisposition,
            state.ResponseStatusCode,
            state.ResponseMediaType,
            state.NegotiatedProtocol,
            state.ResponseHeadersAtUtc,
            state.FirstPhysicalReadDisposition,
            state.FirstPhysicalReadAtUtc,
            state.PhysicalEofAtUtc,
            state.PhysicalEofDisposition,
            true,
            state.StreamStartedAtUtc,
            state.FirstDataAtUtc,
            state.TerminalAtUtc,
            state.FirstDataAtUtc is null
                ? null
                : Math.Max(0, (long)(state.FirstDataAtUtc.Value -
                    state.StreamStartedAtUtc).TotalMilliseconds),
            state.RawReadCount,
            state.MaximumRawReadByteCount,
            state.RawBodyByteCount,
            state.MaximumSseLineByteCount,
            state.MaximumSseEventByteCount,
            state.ProviderChunkCount,
            state.MaximumReceivedProviderChunkByteCount,
            state.AssembledReceivedProviderResponseByteCount,
            state.AnswerDeltaCount,
            state.MaximumAnswerDeltaByteCount,
            state.AssembledAnswerByteCount,
            state.TerminalEventCount,
            10_000,
            state.OverallDeadlineAtUtc,
            state.Outcome,
            state.RejectionCategory));
    }

    private static void ValidateRequest(ChatProviderStreamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            request.AttemptKind is not ("primary" or "syntax-repair") ||
            string.IsNullOrWhiteSpace(request.ProviderIdentity) ||
            string.IsNullOrWhiteSpace(request.Prompt) ||
            _strictUtf8.GetByteCount(request.Prompt) > 131_072 ||
            !IsSha256(request.EvidencePacketFingerprint) ||
            !IsSha256(request.StreamingRequestFingerprint))
        {
            throw new ArgumentException("The raw Chat streaming request is invalid.", nameof(request));
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static CancellationTokenSource CreateDeadline(long deadlineUnixMilliseconds)
    {
        TimeSpan remaining = ResolveDeadline(deadlineUnixMilliseconds) - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException("The Chat streaming deadline has expired.");
        }

        var source = new CancellationTokenSource();
        source.CancelAfter(remaining);
        return source;
    }

    private static DateTimeOffset ResolveDeadline(long deadlineUnixMilliseconds)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset maximum = now.Add(ChatAgentDispatcher.ProviderTimeout);
        if (deadlineUnixMilliseconds >= maximum.ToUnixTimeMilliseconds())
        {
            return maximum;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(deadlineUnixMilliseconds);
    }

    private static Uri ResolveChatCompletionsEndpoint(Uri configured)
    {
        if (configured.AbsolutePath.EndsWith(
                "/chat/completions",
                StringComparison.OrdinalIgnoreCase))
        {
            return configured;
        }

        return new UriBuilder(configured)
        {
            Path = configured.AbsolutePath.TrimEnd('/') + "/chat/completions",
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri;
    }

    private static string Classify(Exception exception) => exception switch
    {
        RawBodyOverflowException => "raw-body-overflow",
        HttpRequestException => "provider-failure",
        IOException => "disconnect",
        DecoderFallbackException => "invalid-wire-utf8",
        JsonException => "invalid-json-event",
        TimeoutException => "overall-deadline",
        InvalidDataException => "provider-failure",
        _ => "provider-failure",
    };

    private static string NormalizeTransportCategory(string? category) => category switch
    {
        "cancelled" or "disconnect" or "inactivity" or "overall-deadline" or
        "raw-body-overflow" or "sse-line-overflow" or "sse-event-overflow" or
        "received-provider-chunk-overflow" or
        "received-provider-response-overflow" or
        "chunk-overflow" or "total-overflow" or "invalid-wire-utf8" or
        "invalid-sse" or "invalid-json-event" or "invalid-json-escape" or
        "invalid-answer-utf8" or "missing-terminal" or "duplicate-terminal" or
        "contradictory-terminal" or "post-terminal-data" => category,
        _ => "provider-failure",
    };

    private sealed class ObservationState
    {
        internal string ResponseHeaderDisposition { get; set; } = "not-received";
        internal int? ResponseStatusCode { get; set; }
        internal string? ResponseMediaType { get; set; }
        internal string? NegotiatedProtocol { get; set; }
        internal DateTimeOffset? ResponseHeadersAtUtc { get; set; }
        internal string FirstPhysicalReadDisposition { get; set; } = "not-observed";
        internal DateTimeOffset? FirstPhysicalReadAtUtc { get; set; }
        internal DateTimeOffset? PhysicalEofAtUtc { get; set; }
        internal string PhysicalEofDisposition { get; set; } = "not-observed";
        internal int RawReadCount { get; set; }
        internal int MaximumRawReadByteCount { get; set; }
        internal int RawBodyByteCount { get; set; }
        internal int ProviderChunkCount { get; set; }
        internal int MaximumReceivedProviderChunkByteCount { get; set; }
        internal int AssembledReceivedProviderResponseByteCount { get; set; }
        internal int MaximumSseLineByteCount { get; set; }
        internal int MaximumSseEventByteCount { get; set; }
        internal string Outcome { get; set; } = "rejected";
        internal string? RejectionCategory { get; set; }
        internal DateTimeOffset StreamStartedAtUtc { get; init; }
        internal DateTimeOffset? FirstDataAtUtc { get; set; }
        internal DateTimeOffset? TerminalAtUtc { get; set; }
        internal int AnswerDeltaCount { get; set; }
        internal int MaximumAnswerDeltaByteCount { get; set; }
        internal int AssembledAnswerByteCount { get; set; }
        internal int TerminalEventCount { get; set; }
        internal DateTimeOffset OverallDeadlineAtUtc { get; init; }
        internal long? AcceptedAnswerDurationMilliseconds { get; set; }

        private int _currentSseLineByteCount;
        private int _currentSseEventByteCount;
        private bool _previousWasCarriageReturn;
        private bool _carriageReturnCompletedEvent;
        private int _carriageReturnCompletedEventByteCount;

        internal void ObserveSseBytes(ReadOnlySpan<byte> bytes)
        {
            foreach (byte value in bytes)
            {
                if (_previousWasCarriageReturn)
                {
                    _previousWasCarriageReturn = false;
                    if (value == (byte)'\n')
                    {
                        if (_carriageReturnCompletedEvent)
                        {
                            MaximumSseEventByteCount = Math.Max(
                                MaximumSseEventByteCount,
                                _carriageReturnCompletedEventByteCount + 1);
                        }
                        else
                        {
                            _currentSseEventByteCount++;
                        }

                        _carriageReturnCompletedEvent = false;
                        continue;
                    }

                    _carriageReturnCompletedEvent = false;
                }

                if (value is (byte)'\r' or (byte)'\n')
                {
                    MaximumSseLineByteCount = Math.Max(
                        MaximumSseLineByteCount,
                        _currentSseLineByteCount);
                    _currentSseEventByteCount += _currentSseLineByteCount + 1;
                    if (_currentSseLineByteCount == 0)
                    {
                        _carriageReturnCompletedEvent = value == (byte)'\r';
                        _carriageReturnCompletedEventByteCount =
                            _currentSseEventByteCount;
                        MaximumSseEventByteCount = Math.Max(
                            MaximumSseEventByteCount,
                            _currentSseEventByteCount);
                        _currentSseEventByteCount = 0;
                    }

                    _currentSseLineByteCount = 0;
                    _previousWasCarriageReturn = value == (byte)'\r';
                    continue;
                }

                _currentSseLineByteCount++;
            }
        }

        internal void FinalizeSseCounters()
        {
            MaximumSseLineByteCount = Math.Max(
                MaximumSseLineByteCount,
                _currentSseLineByteCount);
            MaximumSseEventByteCount = Math.Max(
                MaximumSseEventByteCount,
                _currentSseEventByteCount + _currentSseLineByteCount);
        }
    }

    private sealed class RawBodyOverflowException(string message) :
        IOException(message);
}

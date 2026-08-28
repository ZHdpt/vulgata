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
    ChatStreamObservationAuthority observationAuthority) : IChatProviderStreamingTransport
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

        var observation = new ObservationState();
        ChatProviderStreamSignal? failureSignal = null;
        await using IAsyncEnumerator<ChatProviderStreamSignal> signals = ExecuteAsync(
                request,
                observation,
                cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
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
                RecordObservation(request, observation);
                throw;
            }
            catch (Exception exception) when (
                exception is HttpRequestException or InvalidDataException or
                JsonException or DecoderFallbackException or InvalidOperationException or
                TimeoutException)
            {
                observation.Outcome = "rejected";
                observation.RejectionCategory = Classify(exception);
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

        RecordObservation(request, observation);
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
        observation.NegotiatedProtocol = response.Version.ToString();
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
            if (signal.Kind == ChatProviderStreamSignalKind.ProviderError)
            {
                observation.Outcome = "rejected";
                observation.RejectionCategory = signal.FailureCategory ??
                    "invalid-provider-stream";
                yield return signal;
                yield break;
            }

            if (signal.Kind == ChatProviderStreamSignalKind.Terminal)
            {
                observation.Outcome = "accepted";
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
                    observation.PhysicalEofDisposition = "observed";
                    yield break;
                }

                throw new InvalidDataException(
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
                if (observation.FirstPhysicalReadDisposition == "not-observed")
                {
                    observation.FirstPhysicalReadDisposition = "eof";
                }
                observation.PhysicalEofDisposition = "observed";
                yield break;
            }

            if (observation.FirstPhysicalReadDisposition == "not-observed")
            {
                observation.FirstPhysicalReadDisposition = "data";
            }

            observation.RawBodyByteCount += read;
            yield return buffer.AsMemory(0, read).ToArray();
        }
    }

    private void RecordObservation(
        ChatProviderStreamRequest request,
        ObservationState state)
    {
        _ = observationAuthority.Record(new ChatStreamObservationReceipt(
            "production-raw-response-header-adapter",
            observationAuthority.ProcessObservationId,
            typeof(OpenAiCompatibleChatProviderStreamingTransport).FullName!,
            observationAuthority.AdapterVersion,
            observationAuthority.AdapterAssemblySha256,
            request.ProviderIdentity,
            request.RequestId,
            request.AttemptKind,
            request.StreamingRequestFingerprint,
            true,
            state.ResponseHeaderDisposition,
            state.ResponseStatusCode,
            state.ResponseMediaType,
            state.NegotiatedProtocol,
            state.FirstPhysicalReadDisposition,
            state.PhysicalEofDisposition,
            state.RawReadCount,
            state.MaximumRawReadByteCount,
            state.RawBodyByteCount,
            state.Outcome,
            state.RejectionCategory));
    }

    private static void ValidateRequest(ChatProviderStreamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            request.AttemptKind is not ("primary" or "repair") ||
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
        TimeSpan remaining = DateTimeOffset.FromUnixTimeMilliseconds(deadlineUnixMilliseconds) -
            DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException("The Chat streaming deadline has expired.");
        }

        var source = new CancellationTokenSource();
        source.CancelAfter(remaining);
        return source;
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
        HttpRequestException { StatusCode: not null } => "invalid-response-status",
        HttpRequestException => "provider-transport-failure",
        DecoderFallbackException => "invalid-wire-utf8",
        JsonException => "invalid-json-event",
        TimeoutException => "deadline-exceeded",
        InvalidDataException => "invalid-provider-stream",
        _ => "provider-configuration-failure",
    };

    private sealed class ObservationState
    {
        internal string ResponseHeaderDisposition { get; set; } = "not-received";
        internal int? ResponseStatusCode { get; set; }
        internal string? ResponseMediaType { get; set; }
        internal string? NegotiatedProtocol { get; set; }
        internal string FirstPhysicalReadDisposition { get; set; } = "not-observed";
        internal string PhysicalEofDisposition { get; set; } = "not-observed";
        internal int RawReadCount { get; set; }
        internal int MaximumRawReadByteCount { get; set; }
        internal int RawBodyByteCount { get; set; }
        internal string Outcome { get; set; } = "rejected";
        internal string? RejectionCategory { get; set; }
    }
}

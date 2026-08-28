using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;

namespace Vulgata.Agents.ChatExecution;

public sealed class ChatAgentDispatcher : IChatAgentDispatcher
{
    internal static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _streamInactivityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _minimumRepairWindow = TimeSpan.FromSeconds(3);
    private const string _requestSchemaVersion = "prepared-chat-selection-request-v1";
    private readonly ChatAgentProviderResolver _providerResolver;
    private readonly ISecretEncryptionService _encryptionService;
    private readonly ChatPromptCatalog _promptCatalog;
    private readonly HttpMessageHandler _httpMessageHandler;
    private readonly TimeProvider _timeProvider;
    private readonly IChatProviderStreamingTransport? _streamingTransport;
    private readonly ILogger<ChatAgentDispatcher>? _logger;

    public ChatAgentDispatcher(
        ChatAgentProviderResolver providerResolver,
        ISecretEncryptionService encryptionService,
        ChatPromptCatalog promptCatalog,
        HttpMessageHandler httpMessageHandler,
        TimeProvider timeProvider,
        ILogger<ChatAgentDispatcher>? logger = null)
    {
        _providerResolver = providerResolver;
        _encryptionService = encryptionService;
        _promptCatalog = promptCatalog;
        _httpMessageHandler = httpMessageHandler;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [ActivatorUtilitiesConstructor]
    public ChatAgentDispatcher(
        IServiceProvider services,
        ChatAgentProviderResolver providerResolver,
        ISecretEncryptionService encryptionService,
        ChatPromptCatalog promptCatalog,
        HttpMessageHandler httpMessageHandler,
        TimeProvider timeProvider,
        ILogger<ChatAgentDispatcher>? logger = null)
        : this(
            providerResolver,
            encryptionService,
            promptCatalog,
            httpMessageHandler,
            timeProvider,
            logger)
    {
        _streamingTransport = services.GetRequiredService<IChatProviderStreamingTransport>();
    }

    public async Task<ChatSynthesisOutcome> GenerateAsync(
        ChatSynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.Cancelled);
        }

        try
        {
            ResolvedChatAgentProvider provider = await _providerResolver.ResolveAsync(
                request.SystemId,
                cancellationToken);
            string prompt = _promptCatalog.GetPrompt(request.Mode);
            string requestJson = JsonSerializer.Serialize(new
            {
                schemaVersion = _requestSchemaVersion,
                requestId = request.RequestId,
                mode = ToProviderMode(request.Mode),
                question = request.Question,
                evidenceFingerprint = request.EvidenceFingerprint,
                candidates = request.CandidateClaims.Select(candidate => new
                {
                    candidateHandle = candidate.CandidateHandle,
                    text = candidate.Text,
                    narrativeAllowed = candidate.AllowsBusinessNarrative,
                }),
            });
            string apiKey = _streamingTransport is null
                ? await _encryptionService.DecryptAsync(
                    SecretPurposes.LlmApiKey,
                    provider.EncryptedApiKey,
                    cancellationToken)
                : string.Empty;
            if (_streamingTransport is null && string.IsNullOrWhiteSpace(apiKey))
            {
                return ChatSynthesisOutcome.Failed(ChatContractFailureCode.ProviderFailure);
            }

            long coordinatorDeadline = request.AbsoluteDeadlineUnixTimeMilliseconds;
            long localCap = _timeProvider.GetUtcNow().Add(ProviderTimeout)
                .ToUnixTimeMilliseconds();
            long absoluteDeadline = Math.Min(coordinatorDeadline, localCap);
            IChatProviderStreamingTransport transport = _streamingTransport ??
                new DirectConstructionStreamingTransport(
                    provider,
                    apiKey,
                    _httpMessageHandler);
            string boundedPrompt = _streamingTransport is null
                ? requestJson
                : string.Concat(prompt, "\n\n", requestJson);
            ChatSynthesisOutcome primary = await DispatchAttemptAsync(
                transport,
                request,
                provider,
                boundedPrompt,
                "primary",
                absoluteDeadline,
                cancellationToken);
            if (primary.FailureCode != ChatContractFailureCode.InvalidSelection ||
                transport is DirectConstructionStreamingTransport
                {
                    LastResponseWasStreaming: false,
                })
            {
                return primary;
            }

            long remainingMilliseconds = absoluteDeadline -
                _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            if (remainingMilliseconds < _minimumRepairWindow.TotalMilliseconds)
            {
                return primary;
            }

            string repairPrompt = string.Concat(
                boundedPrompt,
                "\n\nReturn only one syntactically valid JSON object matching the requested schema. " +
                "Do not change the request, evidence fingerprint, candidates, or answer semantics.");
            return await DispatchAttemptAsync(
                transport,
                request,
                provider,
                repairPrompt,
                "repair",
                absoluteDeadline,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.Cancelled);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException or JsonException)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.ProviderFailure);
        }
    }

    private async Task<ChatSynthesisOutcome> DispatchAttemptAsync(
        IChatProviderStreamingTransport transport,
        ChatSynthesisRequest synthesisRequest,
        ResolvedChatAgentProvider provider,
        string prompt,
        string attemptKind,
        long absoluteDeadline,
        CancellationToken cancellationToken)
    {
        string requestFingerprint = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join(
                '\u001f',
                synthesisRequest.RequestId,
                attemptKind,
                provider.ProviderId.ToString("D"),
                synthesisRequest.EvidenceFingerprint,
                "stream=true",
                prompt))));
        var streamRequest = new ChatProviderStreamRequest(
            synthesisRequest.RequestId,
            attemptKind,
            provider.ProviderId.ToString("D"),
            prompt,
            synthesisRequest.EvidenceFingerprint,
            requestFingerprint,
            absoluteDeadline);
        using var deadline = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(Math.Max(
                1,
                absoluteDeadline - _timeProvider.GetUtcNow().ToUnixTimeMilliseconds())),
            _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);
        var answer = new MemoryStream();
        try
        {
            await using IAsyncEnumerator<ChatProviderStreamSignal> signals = transport
                .StreamAsync(streamRequest, linked.Token)
                .GetAsyncEnumerator(linked.Token);
            bool enforceInactivity = transport is not DirectConstructionStreamingTransport;
            while (true)
            {
                bool hasNext;
                if (enforceInactivity)
                {
                    using var inactivity = new CancellationTokenSource(
                        _streamInactivityTimeout,
                        _timeProvider);
                    using var moveNext = CancellationTokenSource.CreateLinkedTokenSource(
                        linked.Token,
                        inactivity.Token);
                    try
                    {
                        hasNext = await signals.MoveNextAsync().AsTask()
                            .WaitAsync(moveNext.Token);
                    }
                    catch (OperationCanceledException) when (
                        inactivity.IsCancellationRequested && !linked.IsCancellationRequested)
                    {
                        return ChatSynthesisOutcome.Failed(ChatContractFailureCode.TimedOut);
                    }
                }
                else
                {
                    hasNext = await signals.MoveNextAsync().AsTask()
                        .WaitAsync(linked.Token);
                }

                if (!hasNext)
                {
                    return ChatSynthesisOutcome.Failed(ChatContractFailureCode.ProviderFailure);
                }

                ChatProviderStreamSignal signal = signals.Current;
                if (signal.Kind == ChatProviderStreamSignalKind.ProviderError)
                {
                    return ChatSynthesisOutcome.Failed(ChatContractFailureCode.ProviderFailure);
                }

                if (signal.Kind == ChatProviderStreamSignalKind.Terminal)
                {
                    return ParseResult(Encoding.UTF8.GetString(answer.ToArray()));
                }

                if (signal.Kind != ChatProviderStreamSignalKind.AnswerDelta ||
                    signal.AnswerDelta.IsEmpty)
                {
                    continue;
                }

                await answer.WriteAsync(signal.AnswerDelta, linked.Token);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.Cancelled);
        }
        catch (NotSupportedException) when (cancellationToken.IsCancellationRequested)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.Cancelled);
        }
        catch (NotSupportedException) when (deadline.IsCancellationRequested)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.TimedOut);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.TimedOut);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidDataException or JsonException or
            InvalidOperationException)
        {
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.ProviderFailure);
        }
        finally
        {
            Array.Clear(answer.GetBuffer(), 0, checked((int)answer.Length));
        }
    }

    private ChatSynthesisOutcome ParseResult(string responseText)
    {
        try
        {
            string responseJson = UnwrapJsonCodeFence(responseText);
            using JsonDocument document = JsonDocument.Parse(responseJson);
            JsonElement root = document.RootElement;
            string requestId = root.GetProperty("requestId").GetString() ?? string.Empty;
            ChatMode mode = root.GetProperty("mode").GetString() switch
            {
                "business" => ChatMode.Business,
                "it" => ChatMode.It,
                _ => throw new JsonException("Unsupported Chat mode."),
            };
            string fingerprint = root.GetProperty("evidenceFingerprint").GetString() ?? string.Empty;
            ChatAnswerability answerability = root.GetProperty("answerability").GetString() switch
            {
                "supported" => ChatAnswerability.Supported,
                "unsupported" => ChatAnswerability.Unsupported,
                _ => throw new JsonException("Unsupported answerability value."),
            };
            string? schemaVersion = root.GetProperty("schemaVersion").GetString();
            if (!StringComparer.Ordinal.Equals(
                    schemaVersion,
                    ChatSynthesisResult.SupportedSchemaVersion) &&
                !StringComparer.Ordinal.Equals(
                    schemaVersion,
                    ChatSynthesisResult.CompatibleProviderEchoSchemaVersion))
            {
                throw new JsonException("Unsupported Chat result schema.");
            }

            ChatSynthesisSelection[] selections = root.GetProperty("selections")
                .EnumerateArray()
                .Select(selection => new ChatSynthesisSelection(
                    selection.GetProperty("candidateHandle").GetString() ?? string.Empty,
                    selection.GetProperty("order").GetInt32(),
                    selection.TryGetProperty("narrativeText", out JsonElement narrative) &&
                        narrative.ValueKind == JsonValueKind.String
                            ? narrative.GetString()
                            : null))
                .ToArray();
            return ChatSynthesisOutcome.Generated(ChatSynthesisResult.Create(
                requestId,
                mode,
                fingerprint,
                answerability,
                selections), responseJson);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ChatContractException)
        {
            _logger?.LogWarning(
                "Prepared Chat provider result parsing failed with {ErrorType}: {ErrorMessage}",
                exception.GetType().Name,
                exception.Message);
            return ChatSynthesisOutcome.Failed(ChatContractFailureCode.InvalidSelection);
        }
    }

    private static string UnwrapJsonCodeFence(string responseText)
    {
        string trimmed = responseText.Trim();
        const string jsonFence = "```json";
        const string plainFence = "```";
        string openingFence = trimmed.StartsWith(jsonFence, StringComparison.OrdinalIgnoreCase)
            ? trimmed[..jsonFence.Length]
            : trimmed.StartsWith(plainFence, StringComparison.Ordinal)
                ? plainFence
                : string.Empty;
        if (openingFence.Length == 0 ||
            !trimmed.EndsWith(plainFence, StringComparison.Ordinal))
        {
            return responseText;
        }

        return trimmed[openingFence.Length..^plainFence.Length].Trim();
    }

    private static string ToProviderMode(ChatMode mode) => mode switch
    {
        ChatMode.Business => "business",
        ChatMode.It => "it",
        _ => throw new InvalidOperationException("The Chat Agent mode is not supported."),
    };

    // Preserves the existing direct-construction seam used by focused tests.
    // Production DI supplies the registered raw response-header adapter instead.
    private sealed class DirectConstructionStreamingTransport(
        ResolvedChatAgentProvider provider,
        string apiKey,
        HttpMessageHandler httpMessageHandler) : IChatProviderStreamingTransport
    {
        internal bool LastResponseWasStreaming { get; private set; }

        public async IAsyncEnumerable<ChatProviderStreamSignal> StreamAsync(
            ChatProviderStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            byte[] body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                model = provider.Model,
                stream = true,
                messages = new[] { new { role = "user", content = request.Prompt } },
            });
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                ResolveEndpoint(provider.Endpoint));
            message.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            message.Headers.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            message.Content = new ByteArrayContent(body);
            message.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
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
            if (!response.IsSuccessStatusCode)
            {
                yield return new ChatProviderStreamSignal(
                    ChatProviderStreamSignalKind.ProviderError,
                    ReadOnlyMemory<byte>.Empty,
                    "invalid-provider-response");
                yield break;
            }

            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            LastResponseWasStreaming = StringComparer.Ordinal.Equals(
                mediaType,
                "text/event-stream");
            if (!LastResponseWasStreaming)
            {
                yield return new ChatProviderStreamSignal(
                    ChatProviderStreamSignalKind.ProviderError,
                    ReadOnlyMemory<byte>.Empty,
                    "invalid-provider-response");
                yield break;
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            ChatProviderStreamAssemblyResult assembled =
                await ChatProviderStreamAssembler.AssembleAsync(
                    ReadBodyAsync(stream, cancellationToken),
                    cancellationToken);
            if (!assembled.Accepted || assembled.Answer is null)
            {
                yield return new ChatProviderStreamSignal(
                    ChatProviderStreamSignalKind.ProviderError,
                    ReadOnlyMemory<byte>.Empty,
                    assembled.RejectionCategory);
                yield break;
            }

            yield return new ChatProviderStreamSignal(
                ChatProviderStreamSignalKind.AnswerDelta,
                Encoding.UTF8.GetBytes(assembled.Answer));
            yield return new ChatProviderStreamSignal(
                ChatProviderStreamSignalKind.Terminal,
                ReadOnlyMemory<byte>.Empty);
        }

        private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadBodyAsync(
            Stream stream,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[16_384];
            while (true)
            {
                int read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    yield break;
                }

                yield return buffer.AsMemory(0, read).ToArray();
            }
        }

        private static Uri ResolveEndpoint(Uri endpoint) =>
            endpoint.AbsolutePath.EndsWith(
                "/chat/completions",
                StringComparison.OrdinalIgnoreCase)
                ? endpoint
                : new UriBuilder(endpoint)
                {
                    Path = endpoint.AbsolutePath.TrimEnd('/') + "/chat/completions",
                    Query = string.Empty,
                    Fragment = string.Empty,
                }.Uri;
    }
}

using Microsoft.Extensions.AI;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.Core.Entities;

namespace Vulgata.OctoberGenerator;

/// <summary>Creates only the explicitly selected Worker client; never selects Chat or falls back.</summary>
public sealed class OctoberWorkerClientFactory
{
    private readonly ISecretEncryptionService _encryption;
    private readonly HttpMessageHandler? _transport;
    private readonly OctoberWorkerHttpObservation? _httpObservation;
    private readonly string? _deepSeekWorkerReasoningEffort;

    public OctoberWorkerClientFactory(ISecretEncryptionService encryption, HttpMessageHandler? transport = null,
        string? deepSeekWorkerReasoningEffort = null)
    {
        if (deepSeekWorkerReasoningEffort is not (null or "low"))
            throw new InvalidOperationException("The Worker reasoning configuration is invalid.");
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
        _transport = transport;
        _deepSeekWorkerReasoningEffort = deepSeekWorkerReasoningEffort;
    }

    internal OctoberWorkerClientFactory(ISecretEncryptionService encryption, HttpMessageHandler? transport,
        OctoberWorkerHttpObservation observation, string? deepSeekWorkerReasoningEffort = null)
        : this(encryption, transport, deepSeekWorkerReasoningEffort)
        => _httpObservation = observation;

    public async Task<IChatClient> CreateAsync(OctoberSelectedWorkerProvider selected,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (selected is null || selected.Precedence != "only-source" ||
            string.IsNullOrWhiteSpace(selected.SourceIdentity) || selected.Profile.ApiType != ApiType.ChatCompletions ||
            string.IsNullOrWhiteSpace(selected.Profile.Name) || string.IsNullOrWhiteSpace(selected.Profile.Model) ||
            string.IsNullOrWhiteSpace(selected.Profile.CredentialReference) || string.IsNullOrWhiteSpace(selected.ProtectedCredential) ||
            !Uri.TryCreate(selected.Profile.Endpoint, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme is not ("https" or "http") || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0)
            throw new InvalidOperationException("The selected Worker profile is unsupported or invalid.");

        if (_deepSeekWorkerReasoningEffort is not null && !IsDeepSeekEndpoint(endpoint))
            throw new InvalidOperationException("Worker reasoning configuration requires the qualified DeepSeek endpoint.");

        string secret;
        try
        {
            secret = await _encryption.DecryptAsync("Vulgata.LlmProviders.ApiKey",
                selected.ProtectedCredential, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new InvalidOperationException("The selected Worker credential is unavailable."); }
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("The selected Worker credential is unavailable.");
        return new SelectedClient(endpoint, selected.Profile.Name, selected.Profile.Model, secret, _transport,
            _httpObservation, _deepSeekWorkerReasoningEffort);
    }

    private static bool IsDeepSeekEndpoint(Uri endpoint) => endpoint.Scheme == Uri.UriSchemeHttps && endpoint.IsDefaultPort &&
        endpoint.Host.Equals("api.deepseek.com", StringComparison.OrdinalIgnoreCase) && endpoint.AbsolutePath == "/" &&
        endpoint.Query.Length == 0 && endpoint.UserInfo.Length == 0 && endpoint.Fragment.Length == 0;

    private sealed class SelectedClient(Uri endpoint, string profileId, string model, string secret, HttpMessageHandler? supplied,
        OctoberWorkerHttpObservation? observation, string? deepSeekWorkerReasoningEffort)
        : IChatClient, IOctoberWorkerAttemptClient, IOctoberBusinessAttemptClient
    {
        private readonly HttpMessageHandler _transport = supplied ?? new SocketsHttpHandler { AllowAutoRedirect = false };
        private readonly bool _useDeepSeekMaxTokens = IsDeepSeekEndpoint(endpoint);
        private bool _disposed;
        public string ProviderProfileId => profileId;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("An explicit Worker attempt lease is required.");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("An explicit Worker attempt lease is required.");

        public async Task<ChatResponse> GetAttemptResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options,
            IOctoberProviderAttemptLease lease, Action<long> onDispatched,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(lease);
            ArgumentNullException.ThrowIfNull(onDispatched);
            if (lease is not IOctoberWorkerDispatchObserver)
                throw new InvalidOperationException("An observable Business attempt lease is required.");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
            var handler = new LeaseHandler(_transport, lease, onDispatched, businessAttempt: true, useDeepSeekMaxTokens: _useDeepSeekMaxTokens);
            using var http = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            var settings = new OpenAIClientOptions
            {
                Endpoint = endpoint,
                RetryPolicy = new ClientRetryPolicy(0),
                Transport = new HttpClientPipelineTransport(http, false, null),
                EnableDistributedTracing = false,
                ClientLoggingOptions = new ClientLoggingOptions
                {
                    EnableLogging = false, EnableMessageLogging = false, EnableMessageContentLogging = false
                }
            };
            using IChatClient client = new ChatClient(model, new ApiKeyCredential(secret), settings).AsIChatClient();
            // Await the actual SDK task through its body read and per-call wrapper cleanup.
            // The Business caller alone completes observation and settles the supplied lease.
            try { return await client.GetResponseAsync(messages, options, linked.Token); }
            catch (OctoberBusinessAttemptDeniedException error) when (!ReferenceEquals(error, handler.BusinessRefusal))
            {
                throw new InvalidOperationException("The selected Business transport failed.");
            }
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetAttemptStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options,
            IOctoberProviderAttemptLease lease, [EnumeratorCancellation] CancellationToken cancellationToken = default,
            Action<long>? onDispatched = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(lease);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
            using var http = new HttpClient(new LeaseHandler(_transport, lease, onDispatched, useDeepSeekMaxTokens: _useDeepSeekMaxTokens, observation: observation?.For(lease)), disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            var settings = new OpenAIClientOptions
            {
                Endpoint = endpoint,
                RetryPolicy = new ClientRetryPolicy(0),
                Transport = new HttpClientPipelineTransport(http, false, null),
                EnableDistributedTracing = false,
                ClientLoggingOptions = new ClientLoggingOptions
                {
                    EnableLogging = false, EnableMessageLogging = false, EnableMessageContentLogging = false
                }
            };
            using IChatClient client = new ChatClient(model, new ApiKeyCredential(secret), settings).AsIChatClient();
            // The current OpenAI adapter reads reasoning_content but does not
            // serialize TextReasoningContent back into Chat Completions history.
            // Adapt only the qualified DeepSeek endpoint, without mutating history.
            ChatOptions? requestOptions = options;
            if (deepSeekWorkerReasoningEffort is not null)
            {
                requestOptions = options?.Clone() ?? new();
                requestOptions.Reasoning = new ReasoningOptions
                {
                    Effort = ReasoningEffort.Low,
                    Output = options?.Reasoning?.Output
                };
            }
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> replay = _useDeepSeekMaxTokens
                ? WithDeepSeekReasoning(messages, requestOptions) : messages;
            await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(replay, requestOptions, linked.Token)
                .WithCancellation(linked.Token))
            {
                observation?.For(lease)?.Sdk(update);
                yield return update;
            }
            observation?.For(lease)?.SdkCompleted();
        }

        private static IEnumerable<Microsoft.Extensions.AI.ChatMessage> WithDeepSeekReasoning(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options)
        {
            foreach (Microsoft.Extensions.AI.ChatMessage message in messages)
            {
                var reasoning = message.Contents.OfType<TextReasoningContent>().ToArray();
                if (message.Role != ChatRole.Assistant || reasoning.Length == 0)
                {
                    yield return message;
                    continue;
                }
                Microsoft.Extensions.AI.ChatMessage copy = message.Clone();
                copy.RawRepresentation = null;
                OpenAI.Chat.ChatMessage wireMessage = new[] { copy }.AsOpenAIChatMessages(options).Single();
#pragma warning disable SCME0001 // The SDK's supported extension field mechanism.
                wireMessage.Patch.Set("$.reasoning_content"u8, string.Concat(reasoning.Select(content => content.Text)));
#pragma warning restore SCME0001
                copy.RawRepresentation = wireMessage;
                yield return copy;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType == typeof(IOctoberWorkerHttpObservationClient) ? observation :
                serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            observation?.Discard();
            if (supplied is null) _transport.Dispose();
        }
    }

    private sealed class LeaseHandler(HttpMessageHandler transport, IOctoberProviderAttemptLease lease,
        Action<long>? onDispatched, bool businessAttempt = false, bool useDeepSeekMaxTokens = false, OctoberWorkerHttpObservation.Leaf? observation = null) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _invoker = new(transport, disposeHandler: false);
        internal OctoberBusinessAttemptDeniedException? BusinessRefusal { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (useDeepSeekMaxTokens) await TranslateDeepSeekTokenParameterAsync(request, cancellationToken, observation);
            ProviderAdmissionStatus eligibility = await lease.CheckBeforeSendAsync(cancellationToken);
            if (eligibility != ProviderAdmissionStatus.Granted)
            {
                if (businessAttempt)
                    throw BusinessRefusal = new OctoberBusinessAttemptDeniedException(eligibility);
                throw new InvalidOperationException("The Worker attempt is no longer eligible.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            onDispatched?.Invoke(Stopwatch.GetTimestamp());
            (lease as IOctoberWorkerDispatchObserver)?.RecordDispatch();
            Task<HttpResponseMessage> response = _invoker.SendAsync(request, cancellationToken);
            HttpResponseMessage result = await response;
            // Observation never changes the original response or dispatch outcome on an observer failure.
            try
            {
                if (observation is not null && result.IsSuccessStatusCode &&
                    result.Content.Headers.ContentType?.MediaType == "text/event-stream")
                    result.Content = OctoberWorkerHttpObservation.Wrap(result.Content, observation);
            }
            catch (Exception) { }
            return result;
        }

        private static async Task TranslateDeepSeekTokenParameterAsync(HttpRequestMessage request, CancellationToken token, OctoberWorkerHttpObservation.Leaf? observation)
        {
            if (request.Content is not { } original) return;
            byte[] body = await original.ReadAsByteArrayAsync(token);
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("DeepSeek request body is invalid.");
            int sourceCount = 0, targetCount = 0;
            JsonElement admitted = default;
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("max_completion_tokens")) { sourceCount++; admitted = property.Value; }
                else if (property.NameEquals("max_tokens")) targetCount++;
            }
            if (sourceCount > 1 || targetCount > 1 || sourceCount != 0 && targetCount != 0)
                throw new InvalidOperationException("DeepSeek token parameters are ambiguous.");
            if (sourceCount == 0) return;
            if (admitted.ValueKind != JsonValueKind.Number || !admitted.TryGetInt64(out long maximum) || maximum <= 0)
                throw new InvalidOperationException("DeepSeek token parameter is invalid.");

            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    writer.WritePropertyName(property.NameEquals("max_completion_tokens") ? "max_tokens" : property.Name);
                    property.Value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            token.ThrowIfCancellationRequested();
            var replacement = new ByteArrayContent(output.ToArray());
            try
            {
                foreach (var header in original.Headers)
                    if (!header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) &&
                        !replacement.Headers.TryAddWithoutValidation(header.Key, header.Value))
                        throw new InvalidOperationException("DeepSeek content headers are invalid.");
                request.Content = replacement;
            }
            catch { replacement.Dispose(); throw; }
            original.Dispose();
            observation?.Cap(maximum);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _invoker.Dispose();
            base.Dispose(disposing);
        }
    }
}

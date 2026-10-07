using Microsoft.Extensions.AI;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using OpenAI;
using OpenAI.Chat;
using Vulgata.Agents.WorkerExecution;
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

    public OctoberWorkerClientFactory(ISecretEncryptionService encryption, HttpMessageHandler? transport = null)
    {
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
        _transport = transport;
    }

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
        return new SelectedClient(endpoint, selected.Profile.Name, selected.Profile.Model, secret, _transport);
    }

    private sealed class SelectedClient(Uri endpoint, string profileId, string model, string secret, HttpMessageHandler? supplied)
        : IChatClient, IOctoberWorkerAttemptClient
    {
        private readonly HttpMessageHandler _transport = supplied ?? new SocketsHttpHandler { AllowAutoRedirect = false };
        private bool _disposed;
        public string ProviderProfileId => profileId;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("An explicit Worker attempt lease is required.");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("An explicit Worker attempt lease is required.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetAttemptStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options,
            IOctoberProviderAttemptLease lease, [EnumeratorCancellation] CancellationToken cancellationToken = default,
            Action<long>? onDispatched = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(lease);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
            using var http = new HttpClient(new LeaseHandler(_transport, lease, onDispatched), disposeHandler: true)
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
            await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(messages, options, linked.Token)
                .WithCancellation(linked.Token))
                yield return update;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (supplied is null) _transport.Dispose();
        }
    }

    private sealed class LeaseHandler(HttpMessageHandler transport, IOctoberProviderAttemptLease lease, Action<long>? onDispatched) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _invoker = new(transport, disposeHandler: false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (await lease.CheckBeforeSendAsync(cancellationToken) != ProviderAdmissionStatus.Granted)
                throw new InvalidOperationException("The Worker attempt is no longer eligible.");
            cancellationToken.ThrowIfCancellationRequested();
            onDispatched?.Invoke(Stopwatch.GetTimestamp());
            Task<HttpResponseMessage> response = _invoker.SendAsync(request, cancellationToken);
            (lease as IOctoberWorkerDispatchObserver)?.RecordDispatch();
            return await response;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _invoker.Dispose();
            base.Dispose(disposing);
        }
    }
}

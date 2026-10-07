using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberDialogue;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>
/// One admitted turn's selected plain client and physical lifetime. This component
/// neither issues durable admissions nor enables a route or invents execution authority.
/// </summary>
public sealed class OctoberChatClientSession : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly HttpMessageHandler _physical;
    private readonly AdmissionHandler _admission;
    private readonly OctoberChatStreamingHandler _guard;
    private readonly HttpClient _http;
    private Task? _settlement;

    private OctoberChatClientSession(OctoberChatProviderBindingService binding,
        OctoberChatProviderSnapshot snapshot, DialogueAdmission admitted, OctoberChatBudget budget,
        IOctoberDialogueExecutionAuthority authority, Func<CancellationToken, Task> reauthorizeBeforeSend,
        OctoberChatRequestAdmission requests, HttpMessageHandler physical, TimeProvider clock,
        string secret, CancellationToken cancellationToken)
    {
        Budget = budget;
        _physical = physical;
        _admission = new(binding, snapshot, admitted, budget, authority, reauthorizeBeforeSend,
            requests, physical, clock, cancellationToken);
        _guard = new(_admission, budget);
        _http = new(_guard, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        var options = new OpenAIClientOptions
        {
            Endpoint = snapshot.Endpoint,
            RetryPolicy = new ClientRetryPolicy(0),
            Transport = new HttpClientPipelineTransport(_http, false, null),
            EnableDistributedTracing = false,
            ClientLoggingOptions = new ClientLoggingOptions
            {
                EnableLogging = false, EnableMessageLogging = false, EnableMessageContentLogging = false
            }
        };
        Client = new ChatClient(snapshot.Model, new ApiKeyCredential(secret), options).AsIChatClient();
        // Register only after every owned field exists: an expired token can invoke
        // synchronously. Its source owns this registration until confirmed cleanup.
        // A durable deadline can be earlier than the guard's already-created timer.
        _ = _admission.Stopping.Register(static state =>
            _ = ((OctoberChatClientSession)state!).SealAndWaitForSettlementAsync(), this);
    }

    public IChatClient Client { get; }
    public OctoberChatBudget Budget { get; }

    public static Task<OctoberChatClientSession> CreateAsync(OctoberChatProviderBindingService binding,
        OctoberChatProviderSnapshot snapshot, DialogueBeginResult admitted, OctoberChatBudget budget,
        IOctoberDialogueExecutionAuthority authority, Func<CancellationToken, Task> reauthorizeBeforeSend,
        OctoberChatRequestAdmission requests, CancellationToken cancellationToken = default) =>
        CreateAsync(binding, snapshot, admitted, budget, authority, reauthorizeBeforeSend, requests,
            new SocketsHttpHandler { AllowAutoRedirect = false }, TimeProvider.System, cancellationToken);

    // Controlled composition transfers ownership exactly as the production factory does.
    internal static async Task<OctoberChatClientSession> CreateAsync(OctoberChatProviderBindingService binding,
        OctoberChatProviderSnapshot snapshot, DialogueBeginResult admitted, OctoberChatBudget budget,
        IOctoberDialogueExecutionAuthority authority, Func<CancellationToken, Task> reauthorizeBeforeSend,
        OctoberChatRequestAdmission requests, HttpMessageHandler physicalTransport, TimeProvider clock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(physicalTransport);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (binding is null || snapshot is null || budget is null || authority is null ||
                reauthorizeBeforeSend is null || requests is null || clock is null ||
                admitted is not { Status: DialogueStatus.Succeeded, MayDispatch: true, Admission: { } admission } ||
                admission.State is not DialogueTurnState.Admitted || budget.IsStopped ||
                admission.Request.Limits != budget.Limits || admission.DeadlineUtc <= clock.GetUtcNow() ||
                admission.Request.ProviderProfileId != snapshot.ProviderId.ToString("D") ||
                admission.Request.ProviderProfileRevision != snapshot.Revision)
                throw Refused();

            using var originalDeadline = budget.CreateDeadlineCancellation();
            using var durableDeadline = new CancellationTokenSource(admission.DeadlineUtc - clock.GetUtcNow(), clock);
            using var readiness = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                budget.OriginalCancellationToken, originalDeadline.Token, durableDeadline.Token);
            var token = readiness.Token;
            token.ThrowIfCancellationRequested();
            if (await AwaitReadinessAsync(authority.VerifyAdmissionAsync(admission.Request, token), token) != DialogueExecutionStatus.Verified) throw Refused();
            token.ThrowIfCancellationRequested();
            string? secret = await AwaitReadinessAsync(binding.DecryptCurrentKeyAsync(snapshot, token), token);
            token.ThrowIfCancellationRequested();
            if (secret is null || budget.IsStopped || admission.DeadlineUtc <= clock.GetUtcNow()) throw Refused();
            return new(binding, snapshot, admission, budget, authority, reauthorizeBeforeSend,
                requests, physicalTransport, clock, secret, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            physicalTransport.Dispose();
            throw;
        }
        catch (Exception)
        {
            physicalTransport.Dispose();
            throw Refused();
        }
    }

    /// <summary>No caller token: abandoning a wait cannot certify cleanup or release.</summary>
    public Task SealAndWaitForSettlementAsync()
    {
        lock (_gate)
        {
            if (_settlement is not null) return _settlement;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _settlement = completion.Task;
            // Start both owners before waiting. Guard disposal releases response-held permits.
            Task guard = _guard.SealAndWaitForSettlementAsync();
            Task admission = _admission.SealAndWaitForSettlementAsync();
            _ = SettleAsync(guard, admission, completion);
            return _settlement;
        }
    }

    public ValueTask DisposeAsync() => new(SealAndWaitForSettlementAsync());

    private async Task SettleAsync(Task guard, Task admission, TaskCompletionSource completion)
    {
        await Task.Yield();
        try
        {
            await Task.WhenAll(guard, admission);
            Client.Dispose();
            _http.Dispose();
            _guard.Dispose();
            _admission.Dispose();
            _physical.Dispose();
            completion.TrySetResult();
        }
        catch (Exception)
        {
            // Do not destroy a transport whose physical cleanup remains unconfirmed.
            completion.TrySetException(new IOException("The Chat request cleanup could not be confirmed."));
        }
    }

    private static InvalidOperationException Refused() => new("The selected Chat execution is unavailable.");

    private static async Task<T> AwaitReadinessAsync<T>(Task<T> operation, CancellationToken token)
    {
        try { return await operation.WaitAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // These are read-only readiness calls, not submitted writes or provider
            // requests. Observe eventual completion without using a late credential,
            // creating a client or granting dispatch. The unstarted handler is safe
            // to dispose; dispatched physical I/O still uses the two-owner drain.
            _ = ObserveReadinessAsync(operation);
            throw;
        }
    }

    private static async Task ObserveReadinessAsync(Task operation)
    {
        try { await operation; }
        catch (Exception) { /* No credential or authority diagnostics are logged. */ }
    }

    /// <summary>
    /// Private to this guard chain. The guard tracks actual reads and invokes response
    /// disposal only after they settle; this owner retains capacity through that disposal.
    /// </summary>
    private sealed class AdmissionHandler : HttpMessageHandler
    {
        private readonly object _gate = new();
        private readonly OctoberChatProviderBindingService _binding;
        private readonly OctoberChatProviderSnapshot _snapshot;
        private readonly DialogueAdmission _admitted;
        private readonly OctoberChatBudget _budget;
        private readonly IOctoberDialogueExecutionAuthority _authority;
        private readonly Func<CancellationToken, Task> _reauthorize;
        private readonly OctoberChatRequestAdmission _requests;
        private readonly HttpMessageInvoker _transport;
        private readonly TimeProvider _clock;
        private readonly Uri _endpoint;
        private readonly CancellationTokenSource _deadline;
        private readonly CancellationTokenSource _stop;
        private readonly List<Exchange> _exchanges = [];
        private Task? _settlement;

        internal AdmissionHandler(OctoberChatProviderBindingService binding, OctoberChatProviderSnapshot snapshot,
            DialogueAdmission admitted, OctoberChatBudget budget, IOctoberDialogueExecutionAuthority authority,
            Func<CancellationToken, Task> reauthorize, OctoberChatRequestAdmission requests,
            HttpMessageHandler physical, TimeProvider clock, CancellationToken cancellationToken)
        {
            _binding = binding; _snapshot = snapshot; _admitted = admitted; _budget = budget;
            _authority = authority; _reauthorize = reauthorize; _requests = requests; _clock = clock;
            _transport = new(physical, disposeHandler: false);
            _endpoint = new(new Uri(snapshot.Endpoint.AbsoluteUri.TrimEnd('/') + "/"), "chat/completions");
            _deadline = new(admitted.DeadlineUtc - clock.GetUtcNow(), clock);
            _stop = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token, cancellationToken);
        }

        internal CancellationToken Stopping => _stop.Token;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_settlement is not null) throw Refused();
                var exchange = new Exchange(_requests);
                _exchanges.Add(exchange);
                return SendCoreAsync(request, exchange, cancellationToken);
            }
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, Exchange exchange, CancellationToken token)
        {
            // Do not invoke external callbacks while holding the dispatch/seal gate.
            await Task.Yield();
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
            HttpResponseMessage? response = null;
            try
            {
                var ct = lifetime.Token;
                ct.ThrowIfCancellationRequested();
                if (request.Method != HttpMethod.Post || request.RequestUri != _endpoint || request.Content is null) throw Refused();
                using (var body = JsonDocument.Parse(await request.Content.ReadAsByteArrayAsync(ct)))
                {
                    if (body.RootElement.ValueKind != JsonValueKind.Object ||
                        body.RootElement.EnumerateObject().Count(p => p.NameEquals("model")) != 1 ||
                        body.RootElement.EnumerateObject().Count(p => p.NameEquals("stream")) != 1 ||
                        body.RootElement.GetProperty("model").GetString() != _snapshot.Model ||
                        body.RootElement.GetProperty("stream").ValueKind != JsonValueKind.True) throw Refused();
                }

                await exchange.AcquireAsync(ct);
                // The permit wait consumes the original lifetime. Recheck full lineage,
                // execution and selected profile after it, before actual physical I/O.
                if (await _authority.VerifyAdmissionAsync(_admitted.Request, ct) != DialogueExecutionStatus.Verified ||
                    !await _binding.IsCurrentAsync(_snapshot, ct)) throw Refused();
                await _reauthorize(ct);
                ct.ThrowIfCancellationRequested();
                if (_budget.IsStopped || _clock.GetUtcNow() >= _admitted.DeadlineUtc) throw Refused();
                response = await _transport.SendAsync(request, ct);
                response.Content = new OwnedContent(response.Content, exchange);
                return response;
            }
            catch
            {
                // A send can fail after it obtained a response. Confirm cleanup before
                // releasing capacity; no response means the awaited physical send ended.
                try
                {
                    if (response is not null) await Task.Run(response.Dispose);
                    exchange.Complete();
                }
                catch (Exception) { exchange.CleanupFailed(); }
                throw;
            }
        }

        internal Task SealAndWaitForSettlementAsync()
        {
            lock (_gate)
            {
                if (_settlement is not null) return _settlement;
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _settlement = completion.Task;
                _ = DrainAsync(_exchanges.Select(e => e.Settlement).ToArray(), completion);
                return _settlement;
            }
        }

        private async Task DrainAsync(Task[] work, TaskCompletionSource completion)
        {
            await Task.Yield();
            try { _stop.Cancel(); } catch (Exception) { /* Stop requests do not certify settlement. */ }
            try { await Task.WhenAll(work); completion.TrySetResult(); }
            catch (Exception) { completion.TrySetException(new IOException("The Chat permit release could not be confirmed.")); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _transport.Dispose();
                _stop.Dispose();
                _deadline.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class Exchange(OctoberChatRequestAdmission requests)
    {
        private readonly TaskCompletionSource _settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _permit;
        private int _closed;
        internal Task Settlement => _settled.Task;
        internal async Task AcquireAsync(CancellationToken token) { await requests.AcquireAsync(token); _permit = true; }
        internal void Complete()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            if (_permit) requests.Release();
            _settled.TrySetResult();
        }
        internal void CleanupFailed()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
                _settled.TrySetException(new IOException("The Chat response release could not be confirmed."));
        }
    }

    private sealed class OwnedContent : HttpContent
    {
        private readonly HttpContent _content;
        private readonly Exchange _exchange;
        private int _disposed;
        internal OwnedContent(HttpContent content, Exchange exchange)
        {
            _content = content; _exchange = exchange;
            foreach (var header in content.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => _content.CopyToAsync(stream);
        protected override async Task<Stream> CreateContentReadStreamAsync() => new BorrowedStream(await _content.ReadAsStreamAsync());
        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken token) => new BorrowedStream(await _content.ReadAsStreamAsync(token));
        protected override void Dispose(bool disposing)
        {
            if (!disposing || Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try
            {
                // Base disposes only the non-owning stream facade. The original content
                // remains the single owner of physical stream disposal.
                base.Dispose(true);
                _content.Dispose();
                _exchange.Complete();
            }
            catch { _exchange.CleanupFailed(); throw; }
        }
    }

    private sealed class BorrowedStream(Stream stream) : Stream
    {
        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => stream.ReadAsync(buffer, offset, count, token);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => stream.ReadAsync(buffer, token);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

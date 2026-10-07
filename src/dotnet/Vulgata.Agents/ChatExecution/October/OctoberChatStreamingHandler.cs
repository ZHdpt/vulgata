using System.Net;

namespace Vulgata.Agents.ChatExecution.October;

/// <summary>One-turn borrowed transport guard; no provider or admission authority is created here.</summary>
public sealed class OctoberChatStreamingHandler : DelegatingHandler
{
    private readonly object _gate = new();
    private readonly HttpMessageInvoker _transport;
    private readonly OctoberChatBudget _budget;
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationTokenSource _deadline;
    private readonly List<Exchange> _exchanges = [];
    private readonly HashSet<int> _sentRounds = [];
    private Task? _settlement;

    public OctoberChatStreamingHandler(HttpMessageHandler admittedTransport, OctoberChatBudget budget)
    {
        ArgumentNullException.ThrowIfNull(admittedTransport);
        ArgumentNullException.ThrowIfNull(budget);
        _transport = new(admittedTransport, disposeHandler: false);
        _budget = budget;
        _deadline = budget.CreateDeadlineCancellation();
    }

    /// <summary>
    /// Seals dispatch and observes this guard's physical work and cleanup. Integration
    /// must also seal and await its admission owner; neither task substitutes for the other.
    /// </summary>
    public Task SealAndWaitForSettlementAsync()
    {
        lock (_gate)
        {
            if (_settlement is not null) return _settlement;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _settlement = completion.Task;
            // Mark every stream closed before returning. BeginCleanup only marks
            // under its lock here; cancellation callbacks run after its async yield.
            Task[] cleanup = _exchanges.Select(exchange => exchange.BeginCleanup()).ToArray();
            _ = SettleAsync(cleanup, completion);
            return _settlement;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Exchange exchange;
        lock (_gate)
        {
            int round = _budget.ProviderRounds;
            if (_settlement is not null || _budget.IsStopped || round == 0 ||
                request.Method != HttpMethod.Post || request.RequestUri is null ||
                !request.RequestUri.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal) ||
                !_sentRounds.Add(round))
                throw new InvalidOperationException("The controlled Chat request was refused.");

            Task predecessor = _exchanges.Count == 0 ? Task.CompletedTask : _exchanges[^1].Settlement;
            exchange = new(_transport, request, _budget, round, predecessor,
                cancellationToken, _budget.OriginalCancellationToken, _deadline.Token, _stop.Token);
            _exchanges.Add(exchange);
            exchange.Start();
        }

        return await exchange.OpenAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _ = SealAndWaitForSettlementAsync();
        // InnerHandler is deliberately unset. The invoker borrows the admission owner.
        base.Dispose(disposing);
    }

    private async Task SettleAsync(Task[] cleanup, TaskCompletionSource completion)
    {
        // Yield before cancellation callbacks can reenter code holding the seal gate.
        await Task.Yield();
        try
        {
            _stop.Cancel();
            await Task.WhenAll(cleanup);
            completion.TrySetResult();
        }
        catch (Exception)
        {
            // A cleanup failure is an unresolved release, not a successful drain.
            completion.TrySetException(new IOException("The controlled Chat transport cleanup could not be confirmed."));
        }
        finally
        {
            _transport.Dispose();
            _deadline.Dispose();
            _stop.Dispose();
        }
    }

    private sealed class Exchange
    {
        private readonly object _ioGate = new();
        private readonly HttpMessageInvoker _transport;
        private readonly HttpRequestMessage _request;
        private readonly Task _predecessor;
        private readonly OctoberChatBudget _budget;
        private readonly CancellationTokenSource _lifetime;
        private readonly OctoberChatSseValidator _validator;
        private readonly TaskCompletionSource _settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly byte[] _raw = new byte[4096];
        private readonly int _round;
        private Task _initialization = Task.CompletedTask;
        private Task<int>? _physicalRead;
        private HttpResponseMessage? _original;
        private Stream? _source;
        private bool _closing;
        private bool _eof;
        private int _rawPosition;
        private int _rawCount;
        private byte[]? _output;
        private int _outputPosition;
        private int _reading;

        internal Exchange(HttpMessageInvoker transport, HttpRequestMessage request, OctoberChatBudget budget,
            int round, Task predecessor, params CancellationToken[] cancellationTokens)
        {
            _transport = transport;
            _request = request;
            _budget = budget;
            _round = round;
            _predecessor = predecessor;
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokens);
            _validator = new(budget);
        }

        internal Task Settlement => _settled.Task;

        internal void Start() => _initialization = InitializeAsync();

        internal async Task<HttpResponseMessage> OpenAsync()
        {
            try
            {
                await _initialization.WaitAsync(_lifetime.Token);
                _lifetime.Token.ThrowIfCancellationRequested();
                var content = new GuardedContent(new GuardedStream(this));
                foreach (var header in _original!.Content.Headers)
                    if (!header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                        content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                var response = new HttpResponseMessage(_original.StatusCode)
                {
                    Version = _original.Version,
                    ReasonPhrase = _original.ReasonPhrase,
                    RequestMessage = _request,
                    Content = content,
                };
                foreach (var header in _original.Headers) response.Headers.TryAddWithoutValidation(header.Key, header.Value);
                return response;
            }
            catch
            {
                _ = BeginCleanup();
                throw;
            }
        }

        private async Task InitializeAsync()
        {
            await _predecessor.WaitAsync(_lifetime.Token);
            _lifetime.Token.ThrowIfCancellationRequested();
            // Track the physical task even if it ignores cancellation. OpenAsync only
            // abandons its own wait; cleanup still owns any late response.
            _original = await _transport.SendAsync(_request, _lifetime.Token);
            _lifetime.Token.ThrowIfCancellationRequested();
            if (!_original.IsSuccessStatusCode ||
                !string.Equals(_original.Content.Headers.ContentType?.MediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase) ||
                _original.Content.Headers.ContentType?.CharSet is { } charset &&
                    !charset.Trim('"').Equals("utf-8", StringComparison.OrdinalIgnoreCase))
                throw new OctoberChatTransportException();
            _source = await _original.Content.ReadAsStreamAsync(_lifetime.Token);
        }

        internal Task BeginCleanup()
        {
            lock (_ioGate)
            {
                if (_closing) return Settlement;
                _closing = true;
                // Cancellation is only a stop request. Completion below still waits
                // for physical send/read and the original response's actual disposal.
                _ = CleanupAsync();
                return Settlement;
            }
        }

        private async Task CleanupAsync()
        {
            await Task.Yield();
            try
            {
                _lifetime.Cancel();
                try { await _initialization; }
                catch (Exception) { }
                Task<int>? read;
                lock (_ioGate) read = _physicalRead;
                if (read is not null)
                {
                    try { await read; }
                    catch (Exception) { }
                }

                // HttpResponseMessage owns its content and stream. Dispose it once;
                // do not separately dispose the same stream through a second owner.
                // The task remains pending even if synchronous Dispose stalls.
                await Task.Run(() => _original?.Dispose());
                _settled.TrySetResult();
            }
            catch (Exception)
            {
                _settled.TrySetException(new IOException("The controlled Chat response cleanup could not be confirmed."));
            }
            finally { _lifetime.Dispose(); }
        }

        internal async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
        {
            if (destination.Length == 0) return 0;
            if (Interlocked.Exchange(ref _reading, 1) != 0)
                throw new InvalidOperationException("Concurrent controlled Chat reads are unsupported.");
            // Cancellation during a physical read never leaves the caller's memory
            // held by the transport. Only this exchange's fixed raw buffer is lent.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                while (_output is null)
                {
                    if (_eof) return 0;
                    while (_rawPosition < _rawCount && _output is null)
                        _output = _validator.Accept(_raw[_rawPosition++]);
                    if (_output is not null) break;

                    Task<int> pending;
                    lock (_ioGate)
                    {
                        if (_closing) throw new OperationCanceledException(linked.Token);
                        pending = _source!.ReadAsync(_raw, linked.Token).AsTask();
                        _physicalRead = pending;
                    }
                    int read = await pending.WaitAsync(linked.Token);
                    linked.Token.ThrowIfCancellationRequested();
                    lock (_ioGate) _physicalRead = null;
                    _rawPosition = 0;
                    _rawCount = read;
                    if (read == 0)
                    {
                        _output = _validator.Complete();
                        _eof = true;
                    }
                    else if (!_budget.TryAddSseBytes(_round, read)) throw new OctoberChatTransportException(true);
                }

                lock (_ioGate)
                {
                    // Disposal/sealing linearizes against delivery, including bytes
                    // validated before the close request. An entry-only check would
                    // leave a race while awaiting physical I/O above.
                    if (_closing) throw new OperationCanceledException(linked.Token);
                    linked.Token.ThrowIfCancellationRequested();
                    int count = Math.Min(destination.Length, _output.Length - _outputPosition);
                    _output.AsMemory(_outputPosition, count).CopyTo(destination);
                    _outputPosition += count;
                    if (_outputPosition == _output.Length)
                    {
                        _output = null;
                        _outputPosition = 0;
                    }
                    return count;
                }
            }
            catch (OctoberChatTransportException exception)
            {
                // Only a parser/bounds rejection charges raw refused attempts.
                // Consumer cancellation or SDK rejection/Dispose must not charge
                // calls a typed adapter may already have refused or dispatched.
                _budget.RecordTransportRejection(_validator.ObservedAttempts, exception.BudgetExceeded);
                _ = BeginCleanup();
                throw;
            }
            catch
            {
                _ = BeginCleanup();
                throw;
            }
            finally { Volatile.Write(ref _reading, 0); }
        }
    }

    private sealed class GuardedContent(GuardedStream body) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => body.CopyToAsync(stream);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            body.CopyToAsync(stream, cancellationToken);
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(body);
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => Task.FromResult<Stream>(body);
        protected override void Dispose(bool disposing)
        {
            if (disposing) body.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class GuardedStream(Exchange owner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => owner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) _ = owner.BeginCleanup();
            base.Dispose(disposing);
        }
        public override ValueTask DisposeAsync() => new(owner.BeginCleanup());
    }
}

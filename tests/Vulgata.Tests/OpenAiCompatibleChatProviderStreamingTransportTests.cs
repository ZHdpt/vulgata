using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents;
using Vulgata.Agents.ChatExecution;

namespace Vulgata.Tests;

/// <summary>
/// T032 RED boundary for the production Chat provider transport composition.
/// Dispatcher primary/repair wiring belongs to T034/T035; this test owns only
/// the Agents transport registration that T033 supplies.
/// </summary>
public sealed class OpenAiCompatibleChatProviderStreamingTransportTests
{
    [Fact]
    public void ProductionRegistration_UsesRawResponseHeaderAdapter()
    {
        var services = new ServiceCollection();
        services.AddPreparedPass2Agents();

        ServiceDescriptor registration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IChatProviderStreamingTransport));

        Assert.Equal(
            "Vulgata.Agents.ChatExecution.OpenAiCompatibleChatProviderStreamingTransport",
            registration.ImplementationType?.FullName);
    }

    [Fact]
    public async Task StreamAsync_EmitsEachDecodedDeltaBeforeTerminalAsync()
    {
        var provider = new LlmProvider(
            "streaming-chat",
            "https://provider.example/v1",
            "encrypted-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        using var responseBody = new GatedSseStream(
            CreateSseEvent("first"),
            CreateSseEvent("second", finishReason: "stop") + "data: [DONE]\n\n");
        using var handler = new GatedResponseHandler(responseBody);
        using var observationAuthority = new ChatStreamObservationAuthority();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        var request = new ChatProviderStreamRequest(
            "0123456789abcdef0123456789abcdef01234567",
            "case-stream-liveness",
            "stream-125-liveness",
            "primary",
            provider.Id.ToString("D"),
            "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
            "question",
            new string('a', 64),
            new string('b', 64),
            DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds());

        await using IAsyncEnumerator<ChatProviderStreamSignal> signals = transport
            .StreamAsync(request, CancellationToken.None)
            .GetAsyncEnumerator();
        Task<bool> firstMove = signals.MoveNextAsync().AsTask();

        try
        {
            await responseBody.FirstChunkDelivered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Task completed = await Task.WhenAny(
                firstMove,
                Task.Delay(TimeSpan.FromMilliseconds(250)));
            Assert.Same(firstMove, completed);
            Assert.True(await firstMove);
            Assert.Equal(ChatProviderStreamSignalKind.AnswerDelta, signals.Current.Kind);
            Assert.Equal("first", Encoding.UTF8.GetString(signals.Current.AnswerDelta.Span));

            responseBody.ReleaseSecondChunk();
            Assert.True(await signals.MoveNextAsync());
            Assert.Equal(ChatProviderStreamSignalKind.AnswerDelta, signals.Current.Kind);
            Assert.Equal("second", Encoding.UTF8.GetString(signals.Current.AnswerDelta.Span));
            Assert.True(await signals.MoveNextAsync());
            Assert.Equal(ChatProviderStreamSignalKind.Terminal, signals.Current.Kind);
        }
        finally
        {
            responseBody.ReleaseSecondChunk();
            await firstMove.WaitAsync(TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task StreamAsync_PhysicalIOException_EmitsOneBoundedSignedDisconnectRejectionAsync()
    {
        var provider = new LlmProvider(
            "streaming-chat",
            "https://provider.example/v1",
            "encrypted-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        using var responseBody = new DisconnectingSseStream(CreateSseEvent("partial"));
        using var handler = new DisconnectingResponseHandler(responseBody);
        using var observationAuthority = new ChatStreamObservationAuthority();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        var request = new ChatProviderStreamRequest(
            "0123456789abcdef0123456789abcdef01234567",
            "case-stream-disconnect",
            "stream-125-disconnect",
            "primary",
            provider.Id.ToString("D"),
            "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
            "question",
            new string('a', 64),
            new string('b', 64),
            DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds());

        var signals = new List<ChatProviderStreamSignal>();
        await foreach (ChatProviderStreamSignal signal in transport.StreamAsync(
            request,
            CancellationToken.None))
        {
            signals.Add(signal);
        }

        Assert.DoesNotContain(
            signals,
            signal => signal.Kind == ChatProviderStreamSignalKind.Terminal);
        Assert.All(
            signals.Where(signal => signal.Kind != ChatProviderStreamSignalKind.ProviderError),
            signal => Assert.Equal(ChatProviderStreamSignalKind.AnswerDelta, signal.Kind));
        ChatProviderStreamSignal rejection = Assert.Single(
            signals.Where(signal => signal.Kind == ChatProviderStreamSignalKind.ProviderError));
        Assert.Equal(ChatProviderStreamSignalKind.ProviderError, rejection.Kind);
        Assert.Equal("disconnect", rejection.FailureCategory);
        Assert.True(
            observationAuthority.TryGetVerifiedReceipt(
                request.RequestId,
                request.AttemptKind,
                out string? receipt));
        Assert.NotNull(receipt);
    }

    [Fact]
    public async Task StreamAsync_SignedReceipt_UsesSchemaV3LifecycleVocabularyAndPerDomainCountersAsync()
    {
        var provider = new LlmProvider(
            "streaming-chat",
            "https://provider.example/v1",
            "encrypted-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        using var responseBody = new MemoryStream(
            Encoding.UTF8.GetBytes(CreateSseEvent("answer", finishReason: "stop") +
                "data: [DONE]\n\n"));
        using var handler = new MemoryResponseHandler(responseBody);
        using var observationAuthority = new ChatStreamObservationAuthority();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        var request = new ChatProviderStreamRequest(
            "0123456789abcdef0123456789abcdef01234567",
            "case-stream-receipt",
            "stream-125-receipt",
            "primary",
            provider.Id.ToString("D"),
            "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
            "question",
            new string('a', 64),
            new string('b', 64),
            DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds());

        await foreach (ChatProviderStreamSignal _ in transport.StreamAsync(
            request,
            CancellationToken.None))
        {
        }

        Assert.True(observationAuthority.TryGetVerifiedReceipt(
            request.RequestId,
            request.AttemptKind,
            out string? serializedReceipt));
        using JsonDocument document = JsonDocument.Parse(serializedReceipt!);
        JsonElement receipt = document.RootElement.GetProperty("receipt");

        Assert.Equal("primary", receipt.GetProperty("attemptKind").GetString());
        Assert.Equal(request.CandidateCommit, receipt.GetProperty("candidateCommit").GetString());
        Assert.Equal(request.CaseId, receipt.GetProperty("caseId").GetString());
        Assert.Equal(request.RequestId, receipt.GetProperty("requestId").GetString());
        Assert.Equal(request.ProviderIdentity, receipt.GetProperty("providerIdentity").GetString());
        Assert.Equal(
            request.ProviderConfigurationFingerprint,
            receipt.GetProperty("providerConfigurationFingerprint").GetString());
        Assert.Equal("production-raw-response-header-adapter", receipt.GetProperty("observationAuthority").GetString());
        Assert.True(receipt.GetProperty("requestStreamMode").GetBoolean());
        Assert.Equal("received", receipt.GetProperty("responseHeaderDisposition").GetString());
        Assert.Equal("HTTP/1.1", receipt.GetProperty("negotiatedProtocol").GetString());
        Assert.Equal("observed", receipt.GetProperty("firstPhysicalReadDisposition").GetString());
        Assert.Equal("observed-after-terminal", receipt.GetProperty("physicalEofDisposition").GetString());
        Assert.Equal("completed", receipt.GetProperty("transportOutcome").GetString());
        Assert.Null(receipt.GetProperty("rejectionCategory").ValueKind == JsonValueKind.Null
            ? null
            : receipt.GetProperty("rejectionCategory").GetString());
        Assert.Equal("accepted", receipt.GetProperty("validationOutcome").GetString());
        Assert.True(receipt.GetProperty("rawReadCount").GetInt32() >= 1);
        Assert.True(receipt.GetProperty("maximumRawReadByteCount").GetInt32() >= 1);
        Assert.True(receipt.GetProperty("rawBodyByteCount").GetInt32() >= 1);
        Assert.True(receipt.GetProperty("maximumSseLineByteCount").GetInt32() >= 1);
        Assert.True(receipt.GetProperty("maximumSseEventByteCount").GetInt32() >= 1);
        Assert.Equal(1, receipt.GetProperty("answerDeltaCount").GetInt32());
        Assert.Equal(
            Encoding.UTF8.GetByteCount("answer"),
            receipt.GetProperty("maximumAnswerDeltaByteCount").GetInt32());
        Assert.Equal(
            Encoding.UTF8.GetByteCount("answer"),
            receipt.GetProperty("assembledAnswerByteCount").GetInt32());
        Assert.Equal(1, receipt.GetProperty("terminalEventCount").GetInt32());
    }

    [Theory]
    [InlineData("\n", 14, 15, 14)]
    [InlineData("\r", 14, 15, 14)]
    [InlineData("\r\n", 16, 17, 14)]
    public async Task StreamAsync_SplitLineEndingBoundaries_RecordExactWireCountersAsync(
        string lineEnding,
        int expectedRawBodyByteCount,
        int expectedRawReadCount,
        int expectedSseEventByteCount)
    {
        var provider = new LlmProvider(
            "streaming-chat",
            "https://provider.example/v1",
            "encrypted-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        byte[] body = Encoding.UTF8.GetBytes("data: [DONE]" + lineEnding + lineEnding);
        using var responseBody = new ByteAtATimeSseStream(body);
        using var handler = new StreamBodyResponseHandler(responseBody);
        using var observationAuthority = new ChatStreamObservationAuthority();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        var request = new ChatProviderStreamRequest(
            "0123456789abcdef0123456789abcdef01234567",
            "case-stream-boundary",
            $"stream-125-boundary-{expectedRawBodyByteCount}",
            "primary",
            provider.Id.ToString("D"),
            "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
            "question",
            new string('a', 64),
            new string('b', 64),
            DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds());

        await foreach (ChatProviderStreamSignal _ in transport.StreamAsync(
            request,
            CancellationToken.None))
        {
        }

        Assert.True(observationAuthority.TryGetVerifiedReceipt(
            request.RequestId,
            request.AttemptKind,
            out string? serializedReceipt));
        using JsonDocument document = JsonDocument.Parse(serializedReceipt!);
        JsonElement receipt = document.RootElement.GetProperty("receipt");
        Assert.Equal(expectedRawBodyByteCount, receipt.GetProperty("rawBodyByteCount").GetInt32());
        Assert.Equal(expectedRawReadCount, receipt.GetProperty("rawReadCount").GetInt32());
        Assert.Equal(1, receipt.GetProperty("maximumRawReadByteCount").GetInt32());
        Assert.Equal(12, receipt.GetProperty("maximumSseLineByteCount").GetInt32());
        Assert.Equal(expectedSseEventByteCount, receipt.GetProperty("maximumSseEventByteCount").GetInt32());
        Assert.Equal("completed", receipt.GetProperty("transportOutcome").GetString());
    }

    [Fact]
    public async Task StreamAsync_UnterminatedSseEvent_FinalizesPhysicalReadCountersBeforeRejectionAsync()
    {
        var provider = new LlmProvider(
            "streaming-chat",
            "https://provider.example/v1",
            "encrypted-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        byte[] body = Encoding.UTF8.GetBytes("data: [DONE]");
        using var responseBody = new ByteAtATimeSseStream(body, oneChunk: true);
        using var handler = new StreamBodyResponseHandler(responseBody);
        using var observationAuthority = new ChatStreamObservationAuthority();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        var request = new ChatProviderStreamRequest(
            "0123456789abcdef0123456789abcdef01234567",
            "case-stream-unterminated",
            "stream-125-unterminated",
            "primary",
            provider.Id.ToString("D"),
            "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
            "question",
            new string('a', 64),
            new string('b', 64),
            DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds());

        var signals = new List<ChatProviderStreamSignal>();
        await foreach (ChatProviderStreamSignal signal in transport.StreamAsync(
            request,
            CancellationToken.None))
        {
            signals.Add(signal);
        }

        Assert.Equal("missing-terminal", Assert.Single(signals).FailureCategory);
        Assert.True(observationAuthority.TryGetVerifiedReceipt(
            request.RequestId,
            request.AttemptKind,
            out string? serializedReceipt));
        using JsonDocument document = JsonDocument.Parse(serializedReceipt!);
        JsonElement receipt = document.RootElement.GetProperty("receipt");
        Assert.Equal("missing-terminal", receipt.GetProperty("rejectionCategory").GetString());
        Assert.Equal(12, receipt.GetProperty("rawBodyByteCount").GetInt32());
        Assert.Equal(2, receipt.GetProperty("rawReadCount").GetInt32());
        Assert.Equal(12, receipt.GetProperty("maximumRawReadByteCount").GetInt32());
        Assert.Equal(12, receipt.GetProperty("maximumSseLineByteCount").GetInt32());
        Assert.Equal(12, receipt.GetProperty("maximumSseEventByteCount").GetInt32());
        Assert.Equal("observed-before-terminal", receipt.GetProperty("physicalEofDisposition").GetString());
    }

    private static string CreateSseEvent(string content, string? finishReason = null)
    {
        string finishReasonJson = System.Text.Json.JsonSerializer.Serialize(finishReason);
        string json = $$"""{"id":"chatcmpl-125","choices":[{"delta":{"content":"{{content}}"},"finish_reason":{{finishReasonJson}}}]}""";
        return $"data: {json}\n\n";
    }

    private sealed class FakeProviderRepository(LlmProvider provider) : ILlmProviderRepository
    {
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LlmProvider>>([provider]);

        public Task<LlmProvider?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<LlmProvider?>(id == provider.Id ? provider : null);

        public Task<bool> NameExistsAsync(
            string name,
            Guid? excludeProviderId = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LlmProvider> AddAsync(
            LlmProvider value,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Remove(LlmProvider value) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeEncryptionService : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default) => Task.FromResult("api-key");
    }

    private sealed class GatedResponseHandler(GatedSseStream body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("text/event-stream") },
                },
            });
    }

    private sealed class DisconnectingResponseHandler(DisconnectingSseStream body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("text/event-stream") },
                },
            });
    }

    private sealed class MemoryResponseHandler(Stream body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body)
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("text/event-stream"),
                    },
                },
            });
    }

    private sealed class StreamBodyResponseHandler(Stream body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body)
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("text/event-stream"),
                    },
                },
            });
    }

    private sealed class ByteAtATimeSseStream(byte[] body, bool oneChunk = false) : Stream
    {
        private readonly byte[] _body = body;
        private readonly bool _oneChunk = oneChunk;
        private int _offset;

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_offset >= _body.Length)
            {
                return ValueTask.FromResult(0);
            }

            int count = _oneChunk
                ? _body.Length - _offset
                : 1;
            _body.AsSpan(_offset, count).CopyTo(buffer.Span);
            _offset += count;
            return ValueTask.FromResult(count);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _body.Length;
        public override long Position { get => _offset; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class GatedSseStream(string first, string second) : Stream
    {
        private readonly byte[][] _chunks = [Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(second)];
        private int _chunkIndex;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource FirstChunkDelivered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void ReleaseSecondChunk() => _release.TrySetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_chunkIndex == 0)
            {
                int count = CopyChunk(buffer, _chunks[_chunkIndex++]);
                FirstChunkDelivered.TrySetResult();
                return count;
            }

            if (_chunkIndex == 1)
            {
                await _release.Task.WaitAsync(cancellationToken);
                return CopyChunk(buffer, _chunks[_chunkIndex++]);
            }

            return 0;
        }

        private static int CopyChunk(Memory<byte> target, byte[] chunk)
        {
            chunk.AsSpan().CopyTo(target.Span);
            return chunk.Length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class DisconnectingSseStream(string first) : Stream
    {
        private readonly byte[] _first = Encoding.UTF8.GetBytes(first);
        private bool _delivered;

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_delivered)
            {
                throw new IOException("simulated physical disconnect");
            }

            _delivered = true;
            _first.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(_first.Length);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

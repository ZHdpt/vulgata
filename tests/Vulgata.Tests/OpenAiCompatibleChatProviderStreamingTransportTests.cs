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
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        var request = new ChatProviderStreamRequest(
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
    public async Task StreamAsync_ConfiguredReasoningEffort_SerializesOpenAiCompatibleFieldAsync()
    {
        var provider = CreateProvider();
        using var responseBody = new MemoryStream(Encoding.UTF8.GetBytes(
            CreateSseEvent("answer", finishReason: "stop") + "data: [DONE]\n\n"));
        using var handler = new CapturingRequestHandler(responseBody);
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            new ChatStreamRuntimeObservationRecorder());
        ChatProviderStreamRequest request = CreateRequest(
            provider,
            "stream-125-reasoning-none") with
        {
            ReasoningEffort = "none",
        };

        List<ChatProviderStreamSignal> signals = await CollectSignalsAsync(transport, request);

        Assert.Contains(signals, signal => signal.Kind == ChatProviderStreamSignalKind.Terminal);
        using JsonDocument body = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        Assert.Equal(
            "none",
            body.RootElement.GetProperty("reasoning_effort").GetString());
    }

    [Fact]
    public async Task StreamAsync_OmittedReasoningEffort_LeavesProviderDefaultUnspecifiedAsync()
    {
        var provider = CreateProvider();
        using var responseBody = new MemoryStream(Encoding.UTF8.GetBytes(
            CreateSseEvent("answer", finishReason: "stop") + "data: [DONE]\n\n"));
        using var handler = new CapturingRequestHandler(responseBody);
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            new ChatStreamRuntimeObservationRecorder());
        ChatProviderStreamRequest request = CreateRequest(
            provider,
            "stream-125-reasoning-omitted");

        List<ChatProviderStreamSignal> signals = await CollectSignalsAsync(transport, request);

        Assert.Contains(signals, signal => signal.Kind == ChatProviderStreamSignalKind.Terminal);
        using JsonDocument body = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        Assert.False(body.RootElement.TryGetProperty("reasoning_effort", out _));
    }

    [Fact]
    public async Task StreamAsync_PhysicalIOException_EmitsOneBoundedNeutralDisconnectObservationAsync()
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
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        var request = new ChatProviderStreamRequest(
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
        ChatStreamRuntimeObservation observation = ReadObservation(
            observationAuthority,
            request);
        Assert.Equal("disconnect", observation.TransportOutcome);
        Assert.Equal("disconnect", observation.RejectionCategory);
    }

    [Fact]
    public async Task StreamAsync_RuntimeObservation_UsesLifecycleVocabularyAndPerDomainCountersAsync()
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
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        var request = new ChatProviderStreamRequest(
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

        ChatStreamRuntimeObservation observation = ReadObservation(
            observationAuthority,
            request);

        Assert.Equal("primary", observation.AttemptKind);
        Assert.Equal(request.RequestId, observation.RequestId);
        Assert.Equal(request.ProviderIdentity, observation.ProviderIdentity);
        Assert.Equal(
            request.ProviderConfigurationFingerprint,
            observation.ProviderConfigurationFingerprint);
        Assert.StartsWith("runtime-", observation.RuntimeObservationId, StringComparison.Ordinal);
        Assert.True(observation.ObservationSequence > 0);
        Assert.True(observation.RequestStreamMode);
        Assert.Equal("received", observation.ResponseHeaderDisposition);
        Assert.Equal("HTTP/1.1", observation.NegotiatedProtocol);
        Assert.Equal("observed", observation.FirstPhysicalReadDisposition);
        Assert.Equal("observed-after-terminal", observation.PhysicalEofDisposition);
        Assert.Equal("completed", observation.TransportOutcome);
        Assert.Null(observation.RejectionCategory);
        Assert.True(observation.RawReadCount >= 1);
        Assert.True(observation.MaximumRawReadByteCount >= 1);
        Assert.True(observation.RawBodyByteCount >= 1);
        Assert.True(observation.MaximumSseLineByteCount >= 1);
        Assert.True(observation.MaximumSseEventByteCount >= 1);
        Assert.Equal(1, observation.AnswerDeltaCount);
        Assert.Equal(
            Encoding.UTF8.GetByteCount("answer"),
            observation.MaximumAnswerDeltaByteCount);
        Assert.Equal(
            Encoding.UTF8.GetByteCount("answer"),
            observation.AssembledAnswerByteCount);
        Assert.Equal(1, observation.TerminalEventCount);
    }

    [Theory]
    [InlineData("\n", 14, 15, 14)]
    [InlineData("\r", 14, 15, 14)]
    [InlineData("\r\n", 16, 17, 16)]
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
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        var request = new ChatProviderStreamRequest(
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

        ChatStreamRuntimeObservation observation = ReadObservation(
            observationAuthority,
            request);
        Assert.Equal(expectedRawBodyByteCount, observation.RawBodyByteCount);
        Assert.Equal(expectedRawReadCount, observation.RawReadCount);
        Assert.Equal(1, observation.MaximumRawReadByteCount);
        Assert.Equal(12, observation.MaximumSseLineByteCount);
        Assert.Equal(expectedSseEventByteCount, observation.MaximumSseEventByteCount);
        Assert.Equal("provider-failure", observation.TransportOutcome);
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
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        var request = new ChatProviderStreamRequest(
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
        ChatStreamRuntimeObservation observation = ReadObservation(
            observationAuthority,
            request);
        Assert.Equal("missing-terminal", observation.RejectionCategory);
        Assert.Equal(12, observation.RawBodyByteCount);
        Assert.Equal(2, observation.RawReadCount);
        Assert.Equal(12, observation.MaximumRawReadByteCount);
        Assert.Equal(12, observation.MaximumSseLineByteCount);
        Assert.Equal(12, observation.MaximumSseEventByteCount);
        Assert.Equal("observed-before-terminal", observation.PhysicalEofDisposition);
    }

    [Fact]
    public async Task StreamAsync_RawBodyAtExactSafetyLimit_RecordsExactWireCountAndRejectsZeroOutputAsync()
    {
        const int expectedRawBodyByteCount = 1_048_576;
        var provider = new LlmProvider(
            "streaming-chat",
            "https://provider.example/v1",
            "encrypted-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        using var responseBody = new ByteAtATimeSseStream(CreateCommentBody(expectedRawBodyByteCount), oneChunk: true);
        using var handler = new StreamBodyResponseHandler(responseBody);
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        ChatProviderStreamRequest request = CreateRequest(provider, "stream-125-raw-exact");

        var signals = new List<ChatProviderStreamSignal>();
        await foreach (ChatProviderStreamSignal signal in transport.StreamAsync(
            request,
            CancellationToken.None))
        {
            signals.Add(signal);
        }

        ChatProviderStreamSignal rejection = Assert.Single(
            signals.Where(signal => signal.Kind == ChatProviderStreamSignalKind.ProviderError));
        Assert.Equal("provider-failure", rejection.FailureCategory);
        Assert.DoesNotContain(
            signals,
            signal => signal.Kind == ChatProviderStreamSignalKind.Terminal);

        ChatStreamRuntimeObservation observation = ReadObservation(
            observationAuthority,
            request);
        Assert.Equal("provider-failure", observation.TransportOutcome);
        Assert.Equal(expectedRawBodyByteCount, observation.RawBodyByteCount);
        Assert.Equal(65, observation.RawReadCount);
        Assert.Equal(16_384, observation.MaximumRawReadByteCount);
    }

    [Fact]
    public async Task StreamAsync_RawBodyOneByteOverSafetyLimit_EmitsClosedRawOverflowObservationAsync()
    {
        const int expectedRawBodyByteCount = 1_048_576;
        var provider = new LlmProvider(
            "streaming-chat",
            "https://provider.example/v1",
            "encrypted-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        byte[] exactBody = CreateCommentBody(expectedRawBodyByteCount);
        byte[] overlongBody = exactBody.Concat([(byte)'x']).ToArray();
        using var responseBody = new ByteAtATimeSseStream(overlongBody, oneChunk: true);
        using var handler = new StreamBodyResponseHandler(responseBody);
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        ChatProviderStreamRequest request = CreateRequest(provider, "stream-125-raw-overflow");

        var signals = new List<ChatProviderStreamSignal>();
        await foreach (ChatProviderStreamSignal signal in transport.StreamAsync(
            request,
            CancellationToken.None))
        {
            signals.Add(signal);
        }

        ChatProviderStreamSignal rejection = Assert.Single(
            signals.Where(signal => signal.Kind == ChatProviderStreamSignalKind.ProviderError));

        Assert.Equal("raw-body-overflow", rejection.FailureCategory);
        ChatStreamRuntimeObservation observation = ReadObservation(
            observationAuthority,
            request);
        Assert.Equal("raw-body-overflow", observation.TransportOutcome);
        Assert.Equal("raw-body-overflow", observation.RejectionCategory);
        Assert.Equal(expectedRawBodyByteCount, observation.RawBodyByteCount);
    }

    [Theory]
    [InlineData(16_384, "completed")]
    [InlineData(16_385, "received-provider-chunk-overflow")]
    public async Task StreamAsync_ReceivedProviderChunkExactAndPlusOne_IsBoundedBeforeJsonAsync(
        int receivedPayloadBytes,
        string expectedOutcome)
    {
        var provider = CreateProvider();
        using var responseBody = new MemoryStream(Encoding.UTF8.GetBytes(
            CreateSizedDataEvent(receivedPayloadBytes) + "data: [DONE]\n\n"));
        using var handler = new MemoryResponseHandler(responseBody);
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        ChatProviderStreamRequest request = CreateRequest(
            provider,
            $"stream-125-received-chunk-{receivedPayloadBytes}");

        var signals = await CollectSignalsAsync(transport, request);

        if (expectedOutcome == "completed")
        {
            Assert.Contains(signals, signal => signal.Kind == ChatProviderStreamSignalKind.Terminal);
        }
        else
        {
            ChatProviderStreamSignal failure = Assert.Single(
                signals.Where(signal => signal.Kind == ChatProviderStreamSignalKind.ProviderError));
            Assert.Equal(expectedOutcome, failure.FailureCategory);
            Assert.DoesNotContain(
                signals,
                signal => signal.Kind == ChatProviderStreamSignalKind.AnswerDelta);
        }

        ChatStreamRuntimeObservation observation = ReadObservation(
            observationAuthority,
            request);
        Assert.Equal(expectedOutcome, observation.TransportOutcome);
        Assert.Equal(receivedPayloadBytes, observation.MaximumReceivedProviderChunkByteCount);
        Assert.Equal(
            receivedPayloadBytes,
            observation.AssembledReceivedProviderResponseByteCount);
    }

    [Theory]
    [InlineData(false, 65_536, "completed")]
    [InlineData(true, 65_537, "received-provider-response-overflow")]
    public async Task StreamAsync_AssembledReceivedResponseExactAndPlusOne_IsBoundedBeforeJsonAsync(
        bool oneByteOver,
        int expectedReceivedBytes,
        string expectedOutcome)
    {
        var provider = CreateProvider();
        int[] chunkSizes = oneByteOver
            ? [16_372, 16_372, 16_372, 16_372, 49]
            : [16_372, 16_372, 16_372, 16_372, 48];
        string body = string.Concat(
            chunkSizes.Select((size, index) =>
                CreateSizedDataEvent(size, includeAnswer: index == 0))) +
            "data: [DONE]\n\n";
        using var responseBody = new MemoryStream(Encoding.UTF8.GetBytes(body));
        using var handler = new MemoryResponseHandler(responseBody);
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        ChatProviderStreamRequest request = CreateRequest(
            provider,
            $"stream-125-received-response-{expectedReceivedBytes}");

        var signals = await CollectSignalsAsync(transport, request);

        if (expectedOutcome == "completed")
        {
            Assert.Contains(signals, signal => signal.Kind == ChatProviderStreamSignalKind.Terminal);
        }
        else
        {
            ChatProviderStreamSignal failure = Assert.Single(
                signals.Where(signal => signal.Kind == ChatProviderStreamSignalKind.ProviderError));
            Assert.Equal(expectedOutcome, failure.FailureCategory);
            Assert.Contains(
                signals,
                signal => signal.Kind == ChatProviderStreamSignalKind.AnswerDelta);
            Assert.DoesNotContain(
                signals,
                signal => signal.Kind == ChatProviderStreamSignalKind.Terminal);
        }

        ChatStreamRuntimeObservation observation = ReadObservation(
            observationAuthority,
            request);
        Assert.Equal(expectedOutcome, observation.TransportOutcome);
        Assert.Equal(
            expectedReceivedBytes,
            observation.AssembledReceivedProviderResponseByteCount);
    }

    [Fact]
    public async Task StreamAsync_EscapeAndMetadataHeavyPayloads_KeepReceivedAndDecodedDomainsSeparateAsync()
    {
        var provider = CreateProvider();
        const string escapedContent = "\\u4e2d\\uD83D\\uDE00";
        string json = "{\"choices\":[{\"delta\":{\"content\":\"" +
            escapedContent + "\"}}],\"metadata\":\"" +
            new string('m', 100) + "\"}";
        int receivedBytes = Encoding.UTF8.GetByteCount(json);
        using var responseBody = new MemoryStream(Encoding.UTF8.GetBytes(
            $"data: {json}\n\ndata: [DONE]\n\n"));
        using var handler = new MemoryResponseHandler(responseBody);
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        ChatProviderStreamRequest request = CreateRequest(
            provider,
            "stream-125-received-escape-metadata");

        var signals = await CollectSignalsAsync(transport, request);

        Assert.Contains(signals, signal => signal.Kind == ChatProviderStreamSignalKind.Terminal);
        ChatStreamRuntimeObservation observation = ReadObservation(
            observationAuthority,
            request);
        Assert.Equal(1, observation.ProviderChunkCount);
        Assert.Equal(receivedBytes, observation.MaximumReceivedProviderChunkByteCount);
        Assert.Equal(receivedBytes, observation.AssembledReceivedProviderResponseByteCount);
        Assert.Equal(1, observation.AnswerDeltaCount);
        Assert.Equal(7, observation.MaximumAnswerDeltaByteCount);
        Assert.Equal(7, observation.AssembledAnswerByteCount);
    }

    [Fact]
    public async Task StreamAsync_ZeroDecodedOutput_FailsClosedWithoutTerminalSuccessAsync()
    {
        var provider = CreateProvider();
        using var responseBody = new MemoryStream(Encoding.UTF8.GetBytes(
            "data: {\"choices\":[]}\n\ndata: [DONE]\n\n"));
        using var handler = new MemoryResponseHandler(responseBody);
        var observationAuthority = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new FakeProviderRepository(provider),
            new FakeEncryptionService(),
            handler,
            observationAuthority);
        ChatProviderStreamRequest request = CreateRequest(
            provider,
            "stream-125-zero-output");

        var signals = await CollectSignalsAsync(transport, request);

        ChatProviderStreamSignal failure = Assert.Single(
            signals.Where(signal => signal.Kind == ChatProviderStreamSignalKind.ProviderError));
        Assert.Equal("provider-failure", failure.FailureCategory);
        Assert.DoesNotContain(
            signals,
            signal => signal.Kind == ChatProviderStreamSignalKind.Terminal);
    }

    private static string CreateSseEvent(string content, string? finishReason = null)
    {
        string finishReasonJson = System.Text.Json.JsonSerializer.Serialize(finishReason);
        string json = $$"""{"id":"chatcmpl-125","choices":[{"delta":{"content":"{{content}}"},"finish_reason":{{finishReasonJson}}}]}""";
        return $"data: {json}\n\n";
    }

    private static ChatProviderStreamRequest CreateRequest(
        LlmProvider provider,
        string requestId) =>
        new(
            requestId,
            "primary",
            provider.Id.ToString("D"),
            "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
            "question",
            new string('a', 64),
            new string('b', 64),
            DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds());

    private static LlmProvider CreateProvider() =>
        new(
            "streaming-chat",
            "https://provider.example/v1",
            "encrypted-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);

    private static string CreateSizedDataEvent(int payloadBytes, bool includeAnswer = true)
    {
        string prefix = includeAnswer
            ? "{\"choices\":[{\"delta\":{\"content\":\"a\"}}],\"metadata\":\""
            : "{\"choices\":[],\"metadata\":\"";
        const string suffix = "\"}";
        int fillerLength = payloadBytes - Encoding.UTF8.GetByteCount(prefix + suffix);
        Assert.True(fillerLength >= 0, $"Payload target {payloadBytes} is smaller than JSON overhead.");
        string json = prefix + new string('m', fillerLength) + suffix;
        Assert.Equal(payloadBytes, Encoding.UTF8.GetByteCount(json));
        return $"data: {json}\n\n";
    }

    private static async Task<List<ChatProviderStreamSignal>> CollectSignalsAsync(
        OpenAiCompatibleChatProviderStreamingTransport transport,
        ChatProviderStreamRequest request)
    {
        var signals = new List<ChatProviderStreamSignal>();
        await foreach (ChatProviderStreamSignal signal in transport.StreamAsync(
            request,
            CancellationToken.None))
        {
            signals.Add(signal);
        }

        return signals;
    }

    private static ChatStreamRuntimeObservation ReadObservation(
        ChatStreamRuntimeObservationRecorder observationRecorder,
        ChatProviderStreamRequest request)
    {
        return Assert.Single(
            observationRecorder.GetRuntimeObservations(request.RequestId),
            observation => observation.AttemptKind == request.AttemptKind);
    }

    private static byte[] CreateCommentBody(int totalBytes)
    {
        const string terminal = "data: [DONE]\n\n";
        int paddingBytes = totalBytes - Encoding.UTF8.GetByteCount(terminal);
        int fullComments = paddingBytes / 4;
        int remainder = paddingBytes % 4;
        var body = new StringBuilder(totalBytes);
        body.Append(string.Concat(Enumerable.Repeat(":x\n\n", fullComments)));
        body.Append(remainder switch
        {
            0 => string.Empty,
            1 => ":",
            2 => ":\n",
            3 => ":\n\n",
            _ => throw new InvalidOperationException("Unexpected comment padding remainder."),
        });
        body.Append(terminal);
        return Encoding.UTF8.GetBytes(body.ToString());
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

    private sealed class CapturingRequestHandler(Stream body) : HttpMessageHandler
    {
        internal string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(body)
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("text/event-stream"),
                    },
                },
            };
        }
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
                ? Math.Min(_body.Length - _offset, buffer.Length)
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

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Vulgata.Agents;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T031 deterministic fake-wire characterization of the Pass 2 executor.
/// No provider, network, database, credentials, or source content is used.
/// </summary>
public sealed class PreparedPass2JobExecutorStreamingTests
{
    private static readonly TimeSpan Inactivity = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan Absolute = TimeSpan.FromMilliseconds(150);

    [Fact]
    public async Task ExecuteAsync_SilentStream_StopsAtInactivityCap()
    {
        ScriptedStreamingTransport transport = new([NeverAsync()]);
        PreparedPass2RawExecutionResult result = await CreateExecutor(transport)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("provider-timeout", result.ReasonCode);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_ProgressiveStream_StopsAtAbsoluteCapWithoutReset()
    {
        ScriptedStreamingTransport transport = new(
            [DelayedChunksAsync(
                Enumerable.Repeat<ReadOnlyMemory<byte>>(
                    Encoding.UTF8.GetBytes("{}"), 20).ToArray(),
                TimeSpan.FromMilliseconds(20))]);
        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                inactivityTimeout: TimeSpan.FromMilliseconds(50),
                absoluteTimeout: TimeSpan.FromMilliseconds(75))
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("provider-timeout", result.ReasonCode);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_RawProviderResponseAtBound_IsRejected()
    {
        ScriptedStreamingTransport transport = new(
            [ChunksAsync([new byte[PreparedPass2WireBudget.MaximumRawResponseUtf8Bytes]])]);
        PreparedPass2RawExecutionResult result = await CreateExecutor(transport)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("provider-unavailable", result.ReasonCode);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task StreamingTransport_OversizedUnterminatedSseLine_RejectsBeforeRawCapReadAsync()
    {
        Guid systemId = Guid.NewGuid();
        LlmProvider provider = new(
            "pass2-test-provider",
            "https://provider.test/v1",
            "ciphertext",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var stream = new OversizedUnterminatedSseStream(
            PreparedPass2WireBudget.MaximumRawResponseUtf8Bytes + 1);
        var transport = new OpenAiCompatiblePass2StreamingTransport(
            new WorkerAgentProviderResolver(
                new EmptyProviderOverrideRepository(),
                new SingleProviderRepository(provider)),
            new PlaintextEncryptionService(),
            new SseStreamHandler(stream));
        var request = new PreparedPass2ProviderRequest(
            1,
            "bounded prompt",
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                flow = new { systemId },
            }),
            "[]"u8.ToArray(),
            SubmissionOnly: false);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (ReadOnlyMemory<byte> _ in transport.StreamAsync(request))
            {
            }
        });
        Assert.True(
            stream.BytesRead <= PreparedPass2WireBudget.MaximumRawResponseUtf8Bytes,
            $"The unterminated SSE line consumed {stream.BytesRead} bytes before rejection.");
    }

    [Fact]
    public async Task StreamingTransport_CanonicalProjectedPacket_ResolvesSystemBindingAsync()
    {
        Guid systemId = Guid.NewGuid();
        LlmProvider provider = new(
            "pass2-test-provider",
            "https://provider.test/v1",
            "ciphertext",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        byte[] sse = Encoding.UTF8.GetBytes(
            "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call-1\",\"function\":{\"name\":\"ok\",\"arguments\":\"{}\"}}]}}]}\n\n" +
            "data: [DONE]\n\n");
        var transport = new OpenAiCompatiblePass2StreamingTransport(
            new WorkerAgentProviderResolver(
                new EmptyProviderOverrideRepository(),
                new SingleProviderRepository(provider)),
            new PlaintextEncryptionService(),
            new SseStreamHandler(new MemoryStream(sse)));
        var request = new PreparedPass2ProviderRequest(
            1,
            "bounded prompt",
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                flow = new { SystemId = systemId },
            }),
            "[]"u8.ToArray(),
            SubmissionOnly: false);

        var chunks = new List<ReadOnlyMemory<byte>>();
        await foreach (ReadOnlyMemory<byte> chunk in transport.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.Contains("\"name\":\"ok\"", Encoding.UTF8.GetString(chunks[^1].Span));
    }

    [Fact]
    public async Task StreamingTransport_SubsequentRequest_UsesCanonicalToolRoleHistoryAsync()
    {
        Guid systemId = Guid.NewGuid();
        LlmProvider provider = new(
            "pass2-test-provider",
            "https://provider.test/v1",
            "ciphertext",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        byte[] sse = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
        var handler = new RecordingSseHandler(sse);
        var transport = new OpenAiCompatiblePass2StreamingTransport(
            new WorkerAgentProviderResolver(
                new EmptyProviderOverrideRepository(),
                new SingleProviderRepository(provider)),
            new PlaintextEncryptionService(),
            handler);
        byte[] history = JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            new
            {
                id = "call-1",
                name = "retrieve_code_logic",
                arguments = new
                {
                    operation = "SourceExcerpt",
                    opaqueHandles = new[] { "anchor-marker" },
                },
                outcome = "Accepted",
                code = "ok",
                items = Array.Empty<object>(),
            },
        });
        var request = new PreparedPass2ProviderRequest(
            2,
            "bounded prompt",
            JsonSerializer.SerializeToUtf8Bytes(new { flow = new { SystemId = systemId } }),
            "[]"u8.ToArray(),
            SubmissionOnly: false,
            TranscriptUtf8: history,
            ToolCallIds: ["call-1"],
            SanitizedToolResultsUtf8: history);

        await foreach (ReadOnlyMemory<byte> _ in transport.StreamAsync(request))
        {
        }

        using JsonDocument body = JsonDocument.Parse(handler.RequestBody);
        string[] roles = body.RootElement.GetProperty("messages").EnumerateArray()
            .Select(value => value.GetProperty("role").GetString()!)
            .ToArray();
        Assert.Equal(["system", "user", "assistant", "tool"], roles);
        JsonElement assistant = body.RootElement.GetProperty("messages")[2];
        Assert.Equal("call-1", assistant.GetProperty("tool_calls")[0].GetProperty("id").GetString());
        string arguments = assistant.GetProperty("tool_calls")[0]
            .GetProperty("function").GetProperty("arguments").GetString()!;
        using JsonDocument argumentDocument = JsonDocument.Parse(arguments);
        Assert.Equal(
            "anchor-marker",
            argumentDocument.RootElement.GetProperty("opaqueHandles")[0].GetString());
        JsonElement tool = body.RootElement.GetProperty("messages")[3];
        Assert.Equal("call-1", tool.GetProperty("tool_call_id").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_RequestAtBound_IsRejectedBeforeTransport()
    {
        ScriptedStreamingTransport transport = new([NeverAsync()]);
        PreparedPass2RawExecutionResult result = await CreateExecutor(transport)
            .ExecuteAsync(
                CreatePacket(new byte[PreparedPass2WireBudget.MaximumInitialRequestUtf8Bytes]),
                "bounded worker prompt",
                AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("request-bound", result.ReasonCode);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_AssembledResponseAtBound_IsRejected()
    {
        ScriptedStreamingTransport transport = new(
            [ChunksAsync([new byte[PreparedPass2WireBudget.MaximumAssembledResponseUtf8Bytes]])]);
        PreparedPass2RawExecutionResult result = await CreateExecutor(transport)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("provider-unavailable", result.ReasonCode);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFailure_UsesZeroSdkRetries()
    {
        ScriptedStreamingTransport transport = new([ThrowsAsync()]);
        PreparedPass2RawExecutionResult result = await CreateExecutor(transport)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("provider-unavailable", result.ReasonCode);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_AllRetrieveBatch_PreflightsWholeBatchAndRunsCallbacksSerially()
    {
        RecordingRetrievalService retrieval = new(TimeSpan.FromMilliseconds(5));
        ScriptedStreamingTransport transport = new(
            [CallsAsync(RetrieveCall("Search", "call-1"), RetrieveCall("Search", "call-2")), CallsAsync(SubmitCall())]);
        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                retrievalService: retrieval)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.True(result.Accepted);
        Assert.Equal(2, retrieval.Calls);
        Assert.Equal(1, retrieval.MaximumConcurrency);
        Assert.Equal(["Search", "Search"], retrieval.Operations);
    }

    [Theory]
    [InlineData("Outgoing")]
    [InlineData("outgoing")]
    public async Task ExecuteAsync_RelatedDocumentsStringDirection_InvokesTypedCallbackAsync(
        string direction)
    {
        RecordingRetrievalService retrieval = new(TimeSpan.Zero);
        ScriptedStreamingTransport transport = new(
            [CallsAsync(RetrieveCall("RelatedDocuments", direction: direction)), CallsAsync(SubmitCall())]);

        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                retrievalService: retrieval)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.True(result.Accepted);
        Assert.Equal(1, retrieval.Calls);
        Assert.Equal(["RelatedDocuments"], retrieval.Operations);
    }

    [Fact]
    public async Task ExecuteAsync_StringNullForNullableRetrievalField_CoercesToNullAsync()
    {
        RecordingRetrievalService retrieval = new(TimeSpan.Zero);
        ScriptedStreamingTransport transport = new(
            [CallsAsync(RetrieveCall("SourceAnchors", direction: "null")), CallsAsync(SubmitCall())]);

        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                retrievalService: retrieval)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.True(result.Accepted);
        Assert.Equal(1, retrieval.Calls);
        Assert.Equal(["SourceAnchors"], retrieval.Operations);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidNullableEnum_ReturnsClosedInvalidToolCallAsync()
    {
        RecordingRetrievalService retrieval = new(TimeSpan.Zero);
        ScriptedStreamingTransport transport = new(
            [CallsAsync(RetrieveCall("RelatedDocuments", direction: "sideways"))]);

        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                retrievalService: retrieval)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("invalid-tool-call", result.ReasonCode);
        Assert.Equal(0, retrieval.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MixedBatch_FailsWholePreflightWithoutCallbacks()
    {
        RecordingRetrievalService retrieval = new(TimeSpan.Zero);
        ScriptedStreamingTransport transport = new([CallsAsync(RetrieveCall("Search"), SubmitCall())]);
        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                retrievalService: retrieval)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("invalid-tool-call", result.ReasonCode);
        Assert.Equal(0, retrieval.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_EightRetrievalTurns_UsesForcedSubmissionOnRequestNine()
    {
        ScriptedStreamingTransport transport = new(
            Enumerable.Repeat(CallsAsync(RetrieveCall("Search")), 8)
                .Append(CallsAsync(SubmitCall()))
                .ToArray());
        PreparedPass2RawExecutionResult result = await CreateExecutor(transport)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.True(result.Accepted);
        Assert.Equal(9, transport.Requests.Count);
        Assert.Equal(9, transport.Requests[^1].RequestNumber);
        Assert.True(transport.Requests[^1].SubmissionOnly);
        string declarations = Encoding.UTF8.GetString(
            transport.Requests[^1].FunctionDeclarationsUtf8.Span);
        Assert.Contains("submit_business_logic", declarations, StringComparison.Ordinal);
        Assert.DoesNotContain("retrieve_code_logic", declarations, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_FirstRejectedSubmission_UsesSoleCorrectionOnRequestTen()
    {
        ScriptedStreamingTransport transport = new(
            Enumerable.Repeat(CallsAsync(RetrieveCall("Search")), 8)
                .Append(CallsAsync(SubmitCall()))
                .Append(CallsAsync(SubmitCall()))
                .ToArray());
        int validationCalls = 0;
        PreparedPass2RawExecutionResult result = await CreateExecutor(transport)
            .ExecuteAsync(
                CreatePacket(),
                "bounded worker prompt",
                (_, _) => Task.FromResult(
                    ++validationCalls == 2
                        ? new PreparedPass2CandidateDecision(
                            PreparedPass2SubmissionDisposition.Accepted,
                            Encoding.UTF8.GetBytes("{\"accepted\":true}"))
                        : PreparedPass2CandidateDecision.Reject(
                            PreparedPass2SubmissionDisposition.InvalidSemantics)));

        Assert.True(result.Accepted);
        Assert.Equal(2, validationCalls);
        Assert.Equal(10, transport.Requests.Count);
        Assert.Equal(9, transport.Requests[^2].RequestNumber);
        Assert.Equal(10, transport.Requests[^1].RequestNumber);
        Assert.All(transport.Requests[^2..], request => Assert.True(request.SubmissionOnly));
    }

    [Fact]
    public async Task ExecuteAsync_EarlyRejectedSubmission_SkipsRequestNineAndUsesSoleCorrectionOnRequestTen()
    {
        ScriptedStreamingTransport transport = new(
            [CallsAsync(SubmitCall("submit-early")), CallsAsync(SubmitCall("submit-correction"))]);
        int validationCalls = 0;
        PreparedPass2RawExecutionResult result = await CreateExecutor(transport)
            .ExecuteAsync(
                CreatePacket(),
                "bounded worker prompt",
                (_, _) => Task.FromResult(
                    ++validationCalls == 2
                        ? new PreparedPass2CandidateDecision(
                            PreparedPass2SubmissionDisposition.Accepted,
                            Encoding.UTF8.GetBytes("{\"accepted\":true}"))
                        : PreparedPass2CandidateDecision.Reject(
                            PreparedPass2SubmissionDisposition.InvalidSemantics)));

        Assert.True(result.Accepted);
        Assert.Equal(2, validationCalls);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal(1, transport.Requests[0].RequestNumber);
        Assert.Equal(10, transport.Requests[1].RequestNumber);
        Assert.DoesNotContain(transport.Requests, request => request.RequestNumber == 9);
        Assert.DoesNotContain(transport.Requests, request => request.RequestNumber == 11);
        Assert.True(transport.Requests[1].SubmissionOnly);
    }

    [Fact]
    public async Task ExecuteAsync_RejectedCorrection_DoesNotSendRequestEleven()
    {
        ScriptedStreamingTransport transport = new(
            Enumerable.Repeat(CallsAsync(RetrieveCall("Search")), 8)
                .Append(CallsAsync(SubmitCall()))
                .Append(CallsAsync(SubmitCall()))
                .ToArray());
        PreparedPass2RawExecutionResult result = await CreateExecutor(transport)
            .ExecuteAsync(
                CreatePacket(),
                "bounded worker prompt",
                (_, _) => Task.FromResult(
                    PreparedPass2CandidateDecision.Reject(
                        PreparedPass2SubmissionDisposition.InvalidSemantics)));

        Assert.False(result.Accepted);
        Assert.Equal("submission-rejected", result.ReasonCode);
        Assert.Equal(10, transport.Requests.Count);
        Assert.DoesNotContain(transport.Requests, request => request.RequestNumber == 11);
    }

    [Fact]
    public async Task ExecuteAsync_RetryAndRestart_PreserveDurableRetrievalBudget()
    {
        RecordingRetrievalService retrieval = new(TimeSpan.Zero, maximumCalls: 1);
        ScriptedStreamingTransport firstTransport = new(
            [CallsAsync(RetrieveCall("Search")), CallsAsync(SubmitCall())]);
        PreparedPass2RawExecutionResult first = await CreateExecutor(
                firstTransport,
                retrievalService: retrieval)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);
        ScriptedStreamingTransport restartedTransport = new(
            [CallsAsync(RetrieveCall("Search")), CallsAsync(SubmitCall())]);
        PreparedPass2RawExecutionResult restarted = await CreateExecutor(
                restartedTransport,
                retrievalService: retrieval)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.True(first.Accepted);
        Assert.False(restarted.Accepted);
        Assert.Equal("retrieval-bound-exceeded", retrieval.LastCode);
        Assert.Equal(2, retrieval.Calls);
        Assert.Equal(2, restartedTransport.Requests.Count);
    }

    [Fact]
    public async Task ExecuteAsync_RetrievalResult_IsProjectedIntoNextProviderRequestTranscriptAsync()
    {
        const string projection = "admitted-retrieval-projection";
        RecordingRetrievalService retrieval = new(
            TimeSpan.Zero,
            projectionContent: projection);
        ScriptedStreamingTransport transport = new(
            [CallsAsync(RetrieveCall("Search", "call-1")), CallsAsync(SubmitCall("submit-1"))]);

        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                retrievalService: retrieval)
            .ExecuteAsync(CreatePacket(), "original server context", AcceptCandidateAsync);

        Assert.True(result.Accepted);
        Assert.Equal(2, transport.Requests.Count);
        string secondRequest = RequestText(transport.Requests[1]);
        Assert.Contains("call-1", secondRequest, StringComparison.Ordinal);
        Assert.Contains("retrieval-handle-1", secondRequest, StringComparison.Ordinal);
        Assert.Contains(projection, secondRequest, StringComparison.Ordinal);
        Assert.Contains("original server context", secondRequest, StringComparison.Ordinal);
        Assert.True(RequestByteCount(transport.Requests[1]) <
            PreparedPass2WireBudget.MaximumInitialRequestUtf8Bytes);
    }

    [Fact]
    public async Task ExecuteAsync_MissingToolCallId_IsRejectedBeforeAnyCallbackAsync()
    {
        RecordingRetrievalService retrieval = new(TimeSpan.Zero);
        ScriptedStreamingTransport transport = new(
            [CallsAsync(RetrieveCall("Search", id: null))]);

        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                retrievalService: retrieval)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("invalid-tool-call", result.ReasonCode);
        Assert.Equal(0, retrieval.Calls);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateToolCallIds_AreRejectedBeforeAnyCallbackAsync()
    {
        RecordingRetrievalService retrieval = new(TimeSpan.Zero);
        ScriptedStreamingTransport transport = new(
            [CallsAsync(
                RetrieveCall("Search", "duplicate-call"),
                RetrieveCall("Search", "duplicate-call"))]);

        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                retrievalService: retrieval)
            .ExecuteAsync(CreatePacket(), "bounded worker prompt", AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("invalid-tool-call", result.ReasonCode);
        Assert.Equal(0, retrieval.Calls);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_RequestTenCorrection_ContainsOnlyClosedRepairAndServerContextAsync()
    {
        PreparedPass2WorkerPacket packet = CreatePacket(
            Encoding.UTF8.GetBytes("{\"original-packet-context\":true}"));
        RecordingRetrievalService retrieval = new(
            TimeSpan.Zero,
            projectionContent: "correction-retrieval-context");
        ScriptedStreamingTransport transport = new(
            Enumerable.Repeat(CallsAsync(RetrieveCall("Search")), 8)
                .Append(CallsAsync(SubmitCall("submit-9")))
                .Append(CallsAsync(SubmitCall("submit-10")))
                .ToArray());
        const string rawProviderFailure = "raw-provider-exception-and-content";
        int validationCalls = 0;
        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                retrievalService: retrieval)
            .ExecuteAsync(
                packet,
                "original server context",
                (_, _) => Task.FromResult(
                    ++validationCalls == 2
                        ? new PreparedPass2CandidateDecision(
                            PreparedPass2SubmissionDisposition.Accepted,
                            Encoding.UTF8.GetBytes("{\"accepted\":true}"))
                        : PreparedPass2CandidateDecision.Reject(
                            PreparedPass2SubmissionDisposition.InvalidSemantics)));

        Assert.True(result.Accepted);
        Assert.Equal(10, transport.Requests.Count);
        string correction = RequestText(transport.Requests[^1]);
        Assert.Contains("firstSubmissionCorrection", correction, StringComparison.Ordinal);
        Assert.Contains("InvalidSemantics", correction, StringComparison.Ordinal);
        Assert.Contains("UseMinimalValidDocument", correction, StringComparison.Ordinal);
        Assert.Contains(packet.Flow.FlowId.ToString("D"), correction, StringComparison.Ordinal);
        Assert.Contains("original-packet-context", correction, StringComparison.Ordinal);
        Assert.Contains("correction-retrieval-context", correction, StringComparison.Ordinal);
        Assert.Contains("original server context", correction, StringComparison.Ordinal);
        Assert.DoesNotContain(rawProviderFailure, correction, StringComparison.Ordinal);
        Assert.DoesNotContain("rawException", correction, StringComparison.Ordinal);
        Assert.DoesNotContain("contentMarkdown", correction, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TranscriptAndAdmittedResultsCountTowardRequestByteCapAsync()
    {
        RecordingRetrievalService retrieval = new(
            TimeSpan.Zero,
            projectionContent: new string('r', 40_000));
        ScriptedStreamingTransport transport = new(
            [CallsAsync(RetrieveCall("Search", "call-1")), CallsAsync(SubmitCall("submit-1"))]);

        PreparedPass2RawExecutionResult result = await CreateExecutor(
                transport,
                retrievalService: retrieval)
            .ExecuteAsync(
                CreatePacket(new byte[100_000]),
                "bounded worker prompt",
                AcceptCandidateAsync);

        Assert.False(result.Accepted);
        Assert.Equal("request-bound", result.ReasonCode);
        Assert.Single(transport.Requests);
        Assert.True(RequestByteCount(transport.Requests[0]) <
            PreparedPass2WireBudget.MaximumInitialRequestUtf8Bytes);
    }

    private static int RequestByteCount(PreparedPass2ProviderRequest request) =>
        checked(
            Encoding.UTF8.GetByteCount(request.Prompt) +
            request.PacketUtf8.Length +
            request.FunctionDeclarationsUtf8.Length);

    private static string RequestText(PreparedPass2ProviderRequest request) =>
        request.Prompt +
        Encoding.UTF8.GetString(request.PacketUtf8.Span) +
        Encoding.UTF8.GetString(request.FunctionDeclarationsUtf8.Span);

    private static PreparedPass2JobExecutor CreateExecutor(
        ScriptedStreamingTransport transport,
        RecordingRetrievalService? retrievalService = null,
        TimeSpan? inactivityTimeout = null,
        TimeSpan? absoluteTimeout = null)
    {
        retrievalService ??= new(TimeSpan.Zero);
        return new(
            transport,
            new PreparedRetrieveCodeLogicFunction(retrievalService),
            new PreparedSubmitBusinessLogicFunction(),
            new DeterministicTimeProvider(),
            inactivityTimeout ?? Inactivity,
            absoluteTimeout ?? Absolute);
    }

    private static PreparedPass2WorkerPacket CreatePacket(byte[]? providerUtf8 = null)
    {
        Guid systemId = Guid.Parse("10000000-0000-4000-8000-000000000001");
        PreparedPass2RuntimeRepositoryBinding consumer = new(
            "consumer", "git-consumer", Guid.Parse("20000000-0000-4000-8000-000000000002"),
            Guid.Parse("21000000-0000-4000-8000-000000000002"),
            Guid.Parse("22000000-0000-4000-8000-000000000002"), "refs/tags/consumer",
            new('a', 40), new('a', 40));
        PreparedPass2RuntimeRepositoryBinding provider = new(
            "provider", "git-provider", Guid.Parse("30000000-0000-4000-8000-000000000003"),
            Guid.Parse("31000000-0000-4000-8000-000000000003"),
            Guid.Parse("32000000-0000-4000-8000-000000000003"), "refs/tags/provider",
            new('b', 40), new('b', 40));
        Guid relationshipId = Guid.Parse("70000000-0000-4000-8000-000000000007");
        Guid jobOne = Guid.Parse("41000000-0000-4000-8000-000000000004");
        Guid jobTwo = Guid.Parse("42000000-0000-4000-8000-000000000005");
        PreparedPass2RuntimeMember[] members =
        [
            new(1, jobOne, Guid.Parse("43000000-0000-4000-8000-000000000004"),
                Guid.Parse("44000000-0000-4000-8000-000000000004"), consumer.RepositoryId,
                Guid.Parse("45000000-0000-4000-8000-000000000004"),
                [Guid.Parse("46000000-0000-4000-8000-000000000004")]),
            new(2, jobTwo, Guid.Parse("47000000-0000-4000-8000-000000000005"),
                Guid.Parse("48000000-0000-4000-8000-000000000005"), provider.RepositoryId,
                Guid.Parse("49000000-0000-4000-8000-000000000005"),
                [Guid.Parse("50000000-0000-4000-8000-000000000005")]),
        ];
        PreparedPass2RuntimeBinding binding = PreparedPass2RuntimeBinding.Create(
            "1.0", "profile", "1.0.0", new('c', 64), "occurrence", systemId,
            consumer, provider, relationshipId,
            [Guid.Parse("51000000-0000-4000-8000-000000000005")], members,
            [
                new(1, jobOne, members[0].CodeUnitId, "consumer", "src/Consumer.cs#L1"),
                new(2, jobTwo, members[1].CodeUnitId, "provider", "src/Provider.cs#L1"),
            ]);
        PreparedPass2Flow flow = binding.Flow;
        return new(
            flow.FlowId, flow, binding, new Dictionary<Guid, string>(),
            providerUtf8 ?? Encoding.UTF8.GetBytes("{}"));
    }

    private static Task<PreparedPass2CandidateDecision> AcceptCandidateAsync(
        ReadOnlyMemory<byte> _, CancellationToken __) =>
        Task.FromResult(new PreparedPass2CandidateDecision(
            PreparedPass2SubmissionDisposition.Accepted,
            Encoding.UTF8.GetBytes("{\"accepted\":true}")));

    private static byte[] RetrieveCall(
        string operation,
        string? id = "call-1",
        string? direction = null) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            toolCalls = new[]
            {
                new
                {
                    id,
                    name = "retrieve_code_logic",
                    arguments = new
                    {
                        operation,
                        queryText = (string?)null,
                        opaqueHandles = Array.Empty<string>(),
                        direction,
                        containedByteOffset = (int?)null,
                        containedByteLength = (int?)null,
                    },
                },
            },
        });

    private static byte[] SubmitCall(string? id = "submit-1") =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            toolCalls = new[]
            {
                new { id, name = "submit_business_logic", arguments = new { document = new { } } },
            },
        });

    private static byte[] Calls(params byte[][] calls) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            toolCalls = calls.SelectMany(call =>
                JsonDocument.Parse(call).RootElement.GetProperty("toolCalls").EnumerateArray())
                .Select(call => new
                {
                    id = call.TryGetProperty("id", out JsonElement idElement)
                        ? idElement.GetString()
                        : null,
                    name = call.GetProperty("name").GetString(),
                    arguments = call.GetProperty("arguments").Clone(),
                }),
        });

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ChunksAsync(
        IReadOnlyList<byte[]> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (byte[] chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> DelayedChunksAsync(
        IReadOnlyList<ReadOnlyMemory<byte>> chunks,
        TimeSpan delay,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (ReadOnlyMemory<byte> chunk in chunks)
        {
            await Task.Delay(delay, cancellationToken);
            yield return chunk;
        }
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> NeverAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        yield return ReadOnlyMemory<byte>.Empty;
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ThrowsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("deterministic transport failure");
        #pragma warning disable CS0162
        yield return ReadOnlyMemory<byte>.Empty;
        #pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> CallsAsync(params byte[][] calls)
    {
        yield return Calls(calls);
        await Task.CompletedTask;
    }

    private sealed class ScriptedStreamingTransport(
        IReadOnlyList<IAsyncEnumerable<ReadOnlyMemory<byte>>> scripts)
        : IPreparedPass2StreamingTransport
    {
        private int _nextScript;
        public List<PreparedPass2ProviderRequest> Requests { get; } = [];

        public IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
            PreparedPass2ProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            int index = Interlocked.Increment(ref _nextScript) - 1;
            return scripts[Math.Min(index, scripts.Count - 1)];
        }
    }

    private sealed class OversizedUnterminatedSseStream(long length) : Stream
    {
        private static readonly byte[] _prefix = "data: "u8.ToArray();
        private long _position;
        internal long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int count = (int)Math.Min(buffer.Length, length - _position);
            if (count <= 0)
            {
                return 0;
            }

            for (int index = 0; index < count; index++)
            {
                long absolute = _position + index;
                buffer[index] = absolute < _prefix.Length
                    ? _prefix[(int)absolute]
                    : (byte)'a';
            }

            _position += count;
            BytesRead += count;
            return count;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class SseStreamHandler(Stream stream) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StreamContent(stream),
            });
    }

    private sealed class RecordingSseHandler(byte[] response) : HttpMessageHandler
    {
        internal byte[] RequestBody { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new ByteArrayContent(response),
            };
        }
    }

    private sealed class PlaintextEncryptionService : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default) => Task.FromResult("test-key");
    }

    private sealed class EmptyProviderOverrideRepository : ISystemLlmProviderOverrideRepository
    {
        public Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SystemLlmProviderOverride>>([]);

        public Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemLlmProviderOverride?>(null);
        public Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(
            Guid systemId,
            AgentType agentType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemLlmProviderOverride?>(null);

        public Task<SystemLlmProviderOverride> AddAsync(
            SystemLlmProviderOverride overrideEntry,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RemoveAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SingleProviderRepository(LlmProvider provider) : ILlmProviderRepository
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

    private sealed class RecordingRetrievalService(
        TimeSpan delay,
        int? maximumCalls = null,
        string? projectionContent = null) : IPass2CodeLogicRetrievalService
    {
        private int _active;
        public int Calls { get; private set; }
        public int MaximumConcurrency { get; private set; }
        public List<string> Operations { get; } = [];
        public string LastCode { get; private set; } = string.Empty;
        public string? ProjectionContent { get; } = projectionContent;

        public async Task<Pass2CodeLogicRetrievalResult> RetrieveAsync(
            Guid pass2JobExecutionId,
            Pass2CodeLogicRetrievalRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Operations.Add(request.Operation.ToString());
            int active = Interlocked.Increment(ref _active);
            MaximumConcurrency = Math.Max(MaximumConcurrency, active);
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }

                IReadOnlyList<Pass2CodeLogicRetrievalItem> items = ProjectionContent is null
                    ? []
                    :
                    [
                        new Pass2CodeLogicRetrievalItem(
                            "retrieval-handle-1",
                            Pass2CodeLogicRetrievalItemKind.Document,
                            Guid.Parse("61000000-0000-4000-8000-000000000006"),
                            Encoding.UTF8.GetByteCount(ProjectionContent),
                            contentMarkdown: ProjectionContent),
                    ];
                Pass2CodeLogicRetrievalResult result = maximumCalls is not null &&
                    Calls > maximumCalls
                    ? Pass2CodeLogicRetrievalResult.BoundExceeded(new(0, 0, 0, 0, 0))
                    : Pass2CodeLogicRetrievalResult.Accepted(items, new(1, 1, 1, 1, 1));
                LastCode = result.Code;
                return result;
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private sealed class DeterministicTimeProvider : TimeProvider
    {
        private static readonly DateTimeOffset Start =
            new(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Start;
    }
}

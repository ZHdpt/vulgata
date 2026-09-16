using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T062 correction RED slice: the Worker must consume a streamed provider
/// response through the public executor seam and return the final bounded
/// result bytes without exposing intermediate reasoning updates.
/// </summary>
public sealed class PreparedPass1JobExecutorStreamingTests
{
    private static readonly TimeSpan _testInactivityTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan _testAbsoluteTimeout = TimeSpan.FromMilliseconds(600);

    private static readonly Guid _systemId =
        Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid _repositoryId =
        Guid.Parse("22222222-3333-4444-8555-666666666666");
    private static readonly Guid _jobId =
        Guid.Parse("33333333-4444-4555-8666-777777777777");
    private static readonly Guid _pairId =
        Guid.Parse("44444444-5555-4666-8777-888888888888");
    private static readonly Guid _fileCodeUnitId =
        Guid.Parse("55555555-6666-4777-8888-999999999999");
    private static readonly Guid _manifestId =
        Guid.Parse("66666666-7777-4888-8999-aaaaaaaaaaaa");
    private static readonly Guid _scanExecutionId =
        Guid.Parse("77777777-8888-4999-8aaa-bbbbbbbbbbbb");
    private static readonly Guid _scanRunId =
        Guid.Parse("88888888-9999-4aaa-8bbb-cccccccccccc");
    private static readonly Guid _observedNavigationReceiptId =
        Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static readonly Guid _secondObservedNavigationReceiptId =
        Guid.Parse("cccccccc-dddd-4eee-8fff-aaaaaaaaaaaa");
    private static readonly Guid _inventedNavigationReceiptId =
        Guid.Parse("dddddddd-eeee-4fff-8aaa-bbbbbbbbbbbb");

    [Fact]
    public void WireBudgetDiagnosticObserver_RecordsPayloadFreeOutgoingRejectionSubtype()
    {
        PreparedPass1WireBudgetDiagnosticObserver observer = new();

        observer.ObserveOutgoingRequestRejection(
            PreparedPass1OutgoingRequestRejectionKind.StrictShape);

        Assert.Equal(
            PreparedPass1OutgoingRequestRejectionKind.StrictShape,
            observer.OutgoingRequestRejectionKind);
        Assert.Equal(
            [PreparedPass1WireBudgetDiagnosticKind.OutgoingRequest],
            observer.Categories);
    }

    [Fact]
    public async Task ExecuteAsync_StreamResponseWithReasoning_ProducesFinalCodeLogicResultAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string submittedDocument = CreateValidDocumentJson(packet);
        string expectedCanonical = Encoding.UTF8.GetString(
            PreparedPass1ResultParser.Parse(packet, Encoding.UTF8.GetBytes(submittedDocument))
                .CanonicalCandidateJson.Span);
        ReasoningSubmitHandler transport = new(submittedDocument);
        LlmProvider provider = new(
            "deepseek-v4-flash",
            "https://deepseek.test/v1",
            "ciphertext",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        WorkerAgentProviderResolver resolver = new(
            new NoOverrideRepository(),
            new SingleProviderRepository(provider));
        PreparedPass1JobExecutor executor = new(
            resolver,
            new PlaintextEncryptionService(),
            new PreparedNavigateCodeFunction(new NoOpNavigationService()),
            transport);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Return the bounded code-logic/file-v2 result.");

        Assert.True(transport.StreamRequested);
        Assert.NotEqual(0, transport.ReasoningEvents);
        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(expectedCanonical, Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.DoesNotContain("reasoning", Encoding.UTF8.GetString(result.Utf8Json.Span), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderRequestOmitsServerOwnedSymbolNameAndRetainsStreamingShapeAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string document = CreateValidDocumentJson(packet);
        SubmitCodeLogicHandler transport = new(document, document);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Return the bounded code-logic/file-v2 result.");

        // Characterization: this boundary is already provider-shape safe.
        // T047 must preserve it while adding server-only integrity checks.
        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.NotEmpty(transport.RequestBodies);
        Assert.All(
            transport.RequestBodies,
            body => Assert.DoesNotContain("SymbolName", body, StringComparison.Ordinal));
        Assert.All(
            transport.RequestBodies,
            body => Assert.Contains("\"stream\":true", body, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_RawSseDataAtEof_ProducesCanonicalResultWithoutRetainingRawBytesAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string document = CreateValidDocumentJson(packet);
        string rawComment = ": raw-comment-must-not-cross-the-public-seam\n\n";
        RawReadSseHandler transport = new(
            [
                Encoding.UTF8.GetBytes(rawComment),
                Encoding.UTF8.GetBytes(CreateSubmitSse(document, includeDone: false)),
            ]);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Consume the bounded SSE data through EOF and submit the canonical result.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(
            Encoding.UTF8.GetString(
                PreparedPass1ResultParser.Parse(
                    packet,
                    Encoding.UTF8.GetBytes(document)).CanonicalCandidateJson.Span),
            Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.DoesNotContain("raw-comment-must-not-cross-the-public-seam", Encoding.UTF8.GetString(result.Utf8Json.Span), StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetByteCount(rawComment + CreateSubmitSse(document, includeDone: false)), transport.RawBytesRead);
    }

    [Fact]
    public async Task ExecuteAsync_CommentOnlyRawReadResetsInactivityBeforeParsedUpdateAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string document = CreateValidDocumentJson(packet);
        RawReadSseHandler transport = new(
            [
                Encoding.UTF8.GetBytes(": keepalive\n\n"),
                Encoding.UTF8.GetBytes(CreateSubmitSse(document, includeDone: true)),
            ],
            delaysBeforeChunk: [
                TimeSpan.FromMilliseconds(40),
                TimeSpan.FromMilliseconds(40),
            ]);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            TimeSpan.FromMilliseconds(60),
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "A successful nonzero comment-only raw read keeps the five-minute inactivity window alive.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(Encoding.UTF8.GetByteCount(": keepalive\n\n" + CreateSubmitSse(document, includeDone: true)), transport.RawBytesRead);
    }

    [Fact]
    public async Task ExecuteAsync_RawSseReadFailureReturnsSanitizedEmptyEvidenceAsync()
    {
        RawReadSseHandler transport = new(
            [Encoding.UTF8.GetBytes(": private-provider-bytes\n\n")],
            throwAfterChunks: true);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "A transport failure exposes only the closed sanitized result.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("unclassified-systemic", result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.DoesNotContain("private-provider-bytes", result.ReasonCode, StringComparison.Ordinal);
    }

    [Fact]
    public void TimeoutDefaults_AreFiveMinutesInactivityAndTwentyFiveMinutesAbsolute()
    {
        Type executorType = typeof(PreparedPass1JobExecutor);
        TimeSpan inactivity = ReadTimeout(
            executorType,
            ["InactivityTimeout", "ProviderInactivityTimeout", "_inactivityTimeout", "_providerInactivityTimeout"]);
        TimeSpan absolute = ReadTimeout(
            executorType,
            ["AbsoluteTimeout", "ProviderAbsoluteTimeout", "_absoluteTimeout", "_providerAbsoluteTimeout"]);

        Assert.Equal(TimeSpan.FromMinutes(5), inactivity);
        Assert.Equal(TimeSpan.FromMinutes(25), absolute);
    }

    [Fact]
    public async Task ExecuteAsync_SilentStream_ExpiresAtInactivityAndReturnsProviderUnavailableAsync()
    {
        StreamingHandler transport = new(
            finalJson: null,
            new[] { TimeSpan.FromMilliseconds(600) });
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Return the bounded code-logic/file-v2 result.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal("provider-unavailable", result.ReasonCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReasoningProgressResetsInactivity_AndAcceptsFinalResultAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string submittedDocument = CreateValidDocumentJson(packet);
        string expectedCanonical = Encoding.UTF8.GetString(
            PreparedPass1ResultParser.Parse(packet, Encoding.UTF8.GetBytes(submittedDocument))
                .CanonicalCandidateJson.Span);
        ReasoningSubmitHandler transport = new(
            submittedDocument,
            [
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(60),
                TimeSpan.FromMilliseconds(60),
                TimeSpan.FromMilliseconds(60),
                TimeSpan.FromMilliseconds(60),
            ]);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Return the bounded code-logic/file-v2 result.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(expectedCanonical, Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.Equal(4, transport.ReasoningEvents);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuingProgress_ExpiresAtAbsoluteCapAsync()
    {
        StreamingHandler transport = new(
            finalJson: null,
            Enumerable.Repeat(TimeSpan.FromMilliseconds(50), 20).ToArray());
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Return the bounded code-logic/file-v2 result.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal("provider-unavailable", result.ReasonCode);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancellation_IsRethrownAsync()
    {
        StreamingHandler transport = new(
            finalJson: null,
            new[] { TimeSpan.FromMilliseconds(600) });
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(
            CreatePacket(),
            "Return the bounded code-logic/file-v2 result.",
            cancellation.Token));
    }

    [Fact]
    public async Task ExecuteAsync_StreamedNavigateCodeLoop_ReturnsOnlyPostToolFinalResultAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string submittedDocument = CreateValidDocumentJson(packet);
        string expectedCanonical = Encoding.UTF8.GetString(
            PreparedPass1ResultParser.Parse(packet, Encoding.UTF8.GetBytes(submittedDocument))
                .CanonicalCandidateJson.Span);
        ToolLoopHandler transport = new(submittedDocument);
        RecordingNavigationService navigation = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Use navigate_code for bounded evidence, then return the final result.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(expectedCanonical, Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.All(transport.RequestBodies, body =>
        {
            using JsonDocument request = JsonDocument.Parse(body);
            Assert.True(request.RootElement.GetProperty("stream").GetBoolean());
        });
        Assert.Contains("\"role\":\"tool\"", transport.RequestBodies[1], StringComparison.Ordinal);
        Assert.DoesNotContain("reasoning", Encoding.UTF8.GetString(result.Utf8Json.Span), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("navigate_code", Encoding.UTF8.GetString(result.Utf8Json.Span), StringComparison.OrdinalIgnoreCase);

        PreparedPass1NavigationRequestInput call = Assert.Single(navigation.Requests);
        Assert.Equal(PreparedPass1NavigationOperation.Definition, call.Operation);
        Assert.Equal<ulong>(1, call.RequestedDepth);
        Assert.Equal("src/Target.java", call.Target.CanonicalPath);
        Assert.Equal("Target", call.Target.Symbol);
        Assert.Null(call.Target.Position);
        Assert.Null(call.Target.ExactLineRange);
        Assert.Equal(new string('f', 64), Convert.ToHexStringLower(call.Target.ExpectedSourceHash!.Value.Span));
    }

    [Fact]
    public async Task ExecuteAsync_ReasoningNavigateCodeLoop_ReplaysCompleteToolTurnAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string submittedDocument = CreateValidDocumentJson(packet);
        ReasoningToolLoopHandler transport = new(submittedDocument);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            new RecordingNavigationService());

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Use bounded reasoning, navigate once, then submit the document.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(2, transport.RequestBodies.Count);
        using JsonDocument replay = JsonDocument.Parse(transport.RequestBodies[1]);
        JsonElement assistant = replay.RootElement.GetProperty("messages")
            .EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "assistant");
        Assert.Equal("bounded reasoning", assistant.GetProperty("reasoning_content").GetString());
        Assert.Equal(JsonValueKind.String, assistant.GetProperty("content").ValueKind);
        Assert.Single(assistant.GetProperty("tool_calls").EnumerateArray());
    }

    [Fact]
    public async Task ExecuteAsync_SubmitCodeLogic_InvalidNestedCandidateThenValidCorrection_CapturesCanonicalResultOnceAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string validDocument = CreateValidDocumentJson(packet);
        PreparedPass1ParsedWorkerResult parsed = PreparedPass1ResultParser.Parse(
            packet,
            Encoding.UTF8.GetBytes(validDocument));
        string expectedCanonical = Encoding.UTF8.GetString(parsed.CanonicalCandidateJson.Span);
        const string duplicateDocument =
            "{\"schemaIdentity\":\"code-logic/file-v2\",\"schemaIdentity\":\"code-logic/file-v2\"}";
        SubmitCodeLogicHandler transport = new(duplicateDocument, validDocument);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Use submit_code_logic for finalization; free-form text is never authoritative.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(expectedCanonical, Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.Equal(["submit_code_logic", "submit_code_logic"], transport.FunctionNames);
        using JsonDocument correctionRequest = JsonDocument.Parse(transport.RequestBodies[1]);
        JsonElement messages = correctionRequest.RootElement.GetProperty("messages");
        Assert.Equal(3, messages.GetArrayLength());
        Assert.Equal(
            ["system", "user", "user"],
            messages.EnumerateArray()
                .Select(message => message.GetProperty("role").GetString())
                .ToArray());
        Assert.DoesNotContain("assistant", messages.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tool_calls", messages.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("navigate_code", messages.GetRawText(), StringComparison.Ordinal);
        string correctionContextJson =
            messages[2].GetProperty("content").GetString()
            ?? throw new Xunit.Sdk.XunitException("AD-28 correction context message has no content.");
        using JsonDocument correctionContext = JsonDocument.Parse(correctionContextJson);
        Assert.Equal(
            ["schemaIdentity", "evidenceResults", "firstSubmissionCorrection"],
            correctionContext.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        JsonElement firstSubmissionCorrection = correctionContext.RootElement
            .GetProperty("firstSubmissionCorrection");
        Assert.Equal(1, firstSubmissionCorrection.GetProperty("attempt").GetInt32());
        Assert.Equal("InvalidShape", firstSubmissionCorrection.GetProperty("category").GetString());
        Assert.Equal(
            "UseMinimalValidDocument",
            firstSubmissionCorrection.GetProperty("repairCode").GetString());
        Assert.Equal(
            ["attempt", "category", "repairCode"],
            firstSubmissionCorrection.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        Assert.DoesNotContain("schemaIdentity\":\"code-logic/file-v2\",\"schemaIdentity", Encoding.UTF8.GetString(result.Utf8Json.Span), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InventedNavigationReceiptWithoutObservedCallback_UsesEvidenceCorrectionBeforeCaptureAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string inventedDocument = CreateDocumentWithNavigationReceiptIds(
            packet,
            [_inventedNavigationReceiptId]);
        string correctedDocument = CreateValidDocumentJson(packet);
        string expectedCorrectedCanonical = Encoding.UTF8.GetString(
            PreparedPass1ResultParser.Parse(
                packet,
                Encoding.UTF8.GetBytes(correctedDocument)).CanonicalCandidateJson.Span);
        SubmitCodeLogicHandler transport = new(inventedDocument, correctedDocument);
        RecordingSubmissionDiagnosticObserver submissions = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            observer: submissions);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Submit the bounded document; every cited navigation receipt must come from this execution.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(expectedCorrectedCanonical, Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.Equal([PreparedPass1SubmissionRejectionKind.InvalidEvidence], submissions.Categories);
        using JsonDocument correctionRequest = JsonDocument.Parse(transport.RequestBodies[1]);
        string correctionBody = correctionRequest.RootElement.GetProperty("messages")[2]
            .GetProperty("content").GetString()!;
        Assert.Contains("UseAuthorizedFullRangeAnchor", correctionBody, StringComparison.Ordinal);
        Assert.DoesNotContain(
            _inventedNavigationReceiptId.ToString("D"),
            correctionBody,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ObservedNavigationReceipt_IsAcceptedWithoutReceiptOrderCouplingAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string submittedDocument = CreateDocumentWithNavigationReceiptIds(
            packet,
            [_observedNavigationReceiptId]);
        string expectedCanonical = Encoding.UTF8.GetString(
            PreparedPass1ResultParser.Parse(
                packet,
                Encoding.UTF8.GetBytes(submittedDocument)).CanonicalCandidateJson.Span);
        ToolLoopHandler transport = new(submittedDocument);
        RecordingNavigationService navigation = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Navigate once, then submit the document using the observed receipt.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(expectedCanonical, Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.Single(navigation.Requests);
        Assert.Equal(2, transport.RequestBodies.Count);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleObservedReceipts_AcceptsReverseOrderAndDuplicateCitationsAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        Guid[] completionOrder =
        [
            _observedNavigationReceiptId,
            _secondObservedNavigationReceiptId,
        ];
        string submittedDocument = CreateDocumentWithNavigationReceiptIds(
            packet,
            [
                _secondObservedNavigationReceiptId,
                _observedNavigationReceiptId,
                _secondObservedNavigationReceiptId,
            ]);
        string expectedCanonical = Encoding.UTF8.GetString(
            PreparedPass1ResultParser.Parse(
                packet,
                Encoding.UTF8.GetBytes(submittedDocument)).CanonicalCandidateJson.Span);
        LegalAllNavigationBatchHandler transport = new(submittedDocument);
        MultipleReceiptNavigationService navigation = new(completionOrder);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Complete two admitted navigations, then cite their receipts in any order without duplicate effects.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(expectedCanonical, Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.Equal(2, navigation.Requests.Count);
        Assert.Equal(2, transport.RequestBodies.Count);
    }

    [Fact]
    public async Task ExecuteAsync_MixedObservedAndUnobservedReceipts_RejectsBeforeCaptureAndCorrectsAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string mixedDocument = CreateDocumentWithNavigationReceiptIds(
            packet,
            [_observedNavigationReceiptId, _inventedNavigationReceiptId]);
        string correctedDocument = CreateValidDocumentJson(packet);
        string expectedCorrectedCanonical = Encoding.UTF8.GetString(
            PreparedPass1ResultParser.Parse(
                packet,
                Encoding.UTF8.GetBytes(correctedDocument)).CanonicalCandidateJson.Span);
        NavigationThenCorrectionHandler transport = new(
            mixedDocument,
            correctedDocument,
            [NavigateCall("call-mixed-navigation", "mixed-navigation")]);
        RecordingNavigationService navigation = new();
        RecordingSubmissionDiagnosticObserver submissions = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            submissions);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Use only receipts admitted by this execution; correct any mixed receipt set before capture.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(expectedCorrectedCanonical, Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.Equal(3, transport.RequestBodies.Count);
        Assert.Single(navigation.Requests);
        Assert.Equal([PreparedPass1SubmissionRejectionKind.InvalidEvidence], submissions.Categories);
        AssertCorrectionContainsOnlyClosedMetadata(
            transport.RequestBodies[2],
            PreparedPass1SubmissionRepairCode.UseAuthorizedFullRangeAnchor,
            _observedNavigationReceiptId,
            _inventedNavigationReceiptId);
    }

    [Fact]
    public async Task ExecuteAsync_OneRemainingNavigationCapacity_DoesNotAuthorizeExcessReceiptAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacketWithBudget(1);
        string mixedDocument = CreateDocumentWithNavigationReceiptIds(
            packet,
            [_observedNavigationReceiptId, _inventedNavigationReceiptId]);
        string correctedDocument = CreateValidDocumentJson(packet);
        string expectedCorrectedCanonical = Encoding.UTF8.GetString(
            PreparedPass1ResultParser.Parse(
                packet,
                Encoding.UTF8.GetBytes(correctedDocument)).CanonicalCandidateJson.Span);
        NavigationThenCorrectionHandler transport = new(
            mixedDocument,
            correctedDocument,
            [
                NavigateCall("call-capacity-one", "capacity-one"),
                NavigateCall("call-capacity-excess", "capacity-excess"),
            ]);
        MultipleReceiptNavigationService navigation = new([_observedNavigationReceiptId]);
        RecordingSubmissionDiagnosticObserver submissions = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            submissions);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Admit one navigation callback, return a closed no-effect result for the excess sibling, then submit.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(expectedCorrectedCanonical, Encoding.UTF8.GetString(result.Utf8Json.Span));
        Assert.Equal(3, transport.RequestBodies.Count);
        Assert.Single(navigation.Requests);
        Assert.Equal(
            1,
            transport.RequestBodies[1].Split(
                "navigation-budget-exhausted",
                StringSplitOptions.None).Length - 1);
        Assert.Equal([PreparedPass1SubmissionRejectionKind.InvalidEvidence], submissions.Categories);
        AssertCorrectionContainsOnlyClosedMetadata(
            transport.RequestBodies[2],
            PreparedPass1SubmissionRepairCode.UseAuthorizedFullRangeAnchor,
            _observedNavigationReceiptId,
            _inventedNavigationReceiptId);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentExecutions_IsolateCrossExecutionReceiptIdsAsync()
    {
        PreparedPass1WorkerPacket firstPacket = CreatePacket();
        PreparedPass1WorkerPacket secondPacket = CreatePacket();
        string firstMixedDocument = CreateDocumentWithNavigationReceiptIds(
            firstPacket,
            [_observedNavigationReceiptId, _secondObservedNavigationReceiptId]);
        string secondMixedDocument = CreateDocumentWithNavigationReceiptIds(
            secondPacket,
            [_secondObservedNavigationReceiptId, _observedNavigationReceiptId]);
        string firstCorrectedDocument = CreateValidDocumentJson(firstPacket);
        string secondCorrectedDocument = CreateValidDocumentJson(secondPacket);
        string firstExpectedCanonical = Encoding.UTF8.GetString(
            PreparedPass1ResultParser.Parse(
                firstPacket,
                Encoding.UTF8.GetBytes(firstCorrectedDocument)).CanonicalCandidateJson.Span);
        string secondExpectedCanonical = Encoding.UTF8.GetString(
            PreparedPass1ResultParser.Parse(
                secondPacket,
                Encoding.UTF8.GetBytes(secondCorrectedDocument)).CanonicalCandidateJson.Span);
        NavigationThenCorrectionHandler firstTransport = new(
            firstMixedDocument,
            firstCorrectedDocument,
            [NavigateCall("call-concurrent-first", "concurrent-first")]);
        NavigationThenCorrectionHandler secondTransport = new(
            secondMixedDocument,
            secondCorrectedDocument,
            [NavigateCall("call-concurrent-second", "concurrent-second")]);
        MultipleReceiptNavigationService firstNavigation = new([_observedNavigationReceiptId]);
        MultipleReceiptNavigationService secondNavigation = new([_secondObservedNavigationReceiptId]);
        RecordingSubmissionDiagnosticObserver firstSubmissions = new();
        RecordingSubmissionDiagnosticObserver secondSubmissions = new();
        PreparedPass1JobExecutor firstExecutor = CreateExecutor(
            firstTransport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            firstNavigation,
            firstSubmissions);
        PreparedPass1JobExecutor secondExecutor = CreateExecutor(
            secondTransport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            secondNavigation,
            secondSubmissions);

        PreparedPass1RawWorkerResult[] results = await Task.WhenAll(
            firstExecutor.ExecuteAsync(firstPacket, "Keep execution one receipts local."),
            secondExecutor.ExecuteAsync(secondPacket, "Keep execution two receipts local."));

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, results[0].Outcome);
        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, results[1].Outcome);
        Assert.Equal(
            firstExpectedCanonical,
            Encoding.UTF8.GetString(results[0].Utf8Json.Span),
            ignoreCase: false);
        Assert.Equal(
            secondExpectedCanonical,
            Encoding.UTF8.GetString(results[1].Utf8Json.Span),
            ignoreCase: false);
        Assert.Single(firstNavigation.Requests);
        Assert.Single(secondNavigation.Requests);
        Assert.Equal([PreparedPass1SubmissionRejectionKind.InvalidEvidence], firstSubmissions.Categories);
        Assert.Equal([PreparedPass1SubmissionRejectionKind.InvalidEvidence], secondSubmissions.Categories);
        AssertCorrectionContainsOnlyClosedMetadata(
            firstTransport.RequestBodies[2],
            PreparedPass1SubmissionRepairCode.UseAuthorizedFullRangeAnchor,
            _observedNavigationReceiptId,
            _secondObservedNavigationReceiptId);
        AssertCorrectionContainsOnlyClosedMetadata(
            secondTransport.RequestBodies[2],
            PreparedPass1SubmissionRepairCode.UseAuthorizedFullRangeAnchor,
            _secondObservedNavigationReceiptId,
            _observedNavigationReceiptId);
    }

    [Fact]
    public async Task ExecuteAsync_FirstRejectedSubmission_SecondInvalidTerminatesWithoutRequestElevenAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        FirstRejectedThenCorrectedHandler transport = new(
            "{}",
            navigationTurns: 2);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "After the first rejected submission, allow one bounded correction only.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("worker-result-rejected", result.ReasonCode);
        Assert.Equal(4, transport.RequestBodies.Count);
        Assert.DoesNotContain(
            transport.RequestBodies,
            body => body.Contains("request-eleven", StringComparison.Ordinal));
        using JsonDocument correctionRequest = JsonDocument.Parse(transport.RequestBodies[3]);
        JsonElement correctionContext = JsonDocument.Parse(
            correctionRequest.RootElement.GetProperty("messages")[2]
                .GetProperty("content").GetString()!).RootElement;
        JsonElement firstSubmissionCorrection =
            correctionContext.GetProperty("firstSubmissionCorrection");
        Assert.Equal(
            ["attempt", "category", "repairCode"],
            firstSubmissionCorrection.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        Assert.Equal("UseMinimalValidDocument", firstSubmissionCorrection
            .GetProperty("repairCode").GetString());
    }

    [Theory]
    [InlineData(false, false, 9, true)]
    [InlineData(true, false, 10, true)]
    [InlineData(true, true, 10, false)]
    public async Task ExecuteAsync_Ad30TurnAllocation_UsesTurnNineFinalizationAndOnlyItsCorrectionAsync(
        bool firstSubmissionInvalid,
        bool correctionInvalid,
        int expectedRequestCount,
        bool expectedCompleted)
    {
        PreparedPass1WorkerPacket packet = CreatePacketWithBudget(8);
        string validDocument = CreateValidDocumentJson(packet);
        Ad30TurnAllocationHandler transport = new(
            firstSubmissionInvalid ? "{}" : validDocument,
            correctionInvalid ? "{}" : validDocument,
            includeCorrection: firstSubmissionInvalid);
        object authority = CreateDeclarationAuthority(packet);
        RecordingNavigationService navigation = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Use eight navigation-capable turns, then force finalization on turn nine and allow only its one correction.");

        Assert.Equal(
            expectedCompleted
                ? PreparedPass1RawWorkerOutcome.Completed
                : PreparedPass1RawWorkerOutcome.Rejected,
            result.Outcome);
        Assert.Equal(expectedRequestCount, transport.RequestBodies.Count);
        Assert.DoesNotContain(
            transport.RequestBodies,
            body => body.Contains("request-eleven", StringComparison.Ordinal));
        Assert.Equal(8, navigation.Requests.Count);

        for (int index = 0; index < 8; index++)
        {
            AssertT132ToolChoice(transport.RequestBodies[index], index + 1);
            AssertT145DeclarationIdentity(
                transport.RequestBodies[index],
                index + 1,
                authority);
        }

        AssertAd30TerminalRequest(
            transport.RequestBodies[8],
            authority,
            expectedCorrectionCategory: null);

        if (firstSubmissionInvalid)
        {
            AssertAd30TerminalRequest(
                transport.RequestBodies[9],
                authority,
                expectedCorrectionCategory: "InvalidShape");
        }
    }

    [Fact]
    public async Task SubmitSession_ValidCandidate_CapturesCanonicalBytesAndTerminatesImmediatelyAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string validDocument = CreateValidDocumentJson(packet);
        PreparedPass1ValidationResult expected = PreparedPass1ResultParser.Validate(
            packet,
            Encoding.UTF8.GetBytes(validDocument));
        Assert.True(expected.IsValid);

        PreparedSubmitCodeLogicSession session = CreateSubmissionSession(packet);
        string? outcome = await InvokeSubmitAsync(session, validDocument);

        Assert.Equal("accepted", outcome);
        Assert.True(session.HasSubmission);
        Assert.False(session.NavigationAllowed);
        Assert.True(session.ShouldTerminate);
        Assert.Equal(
            expected.CanonicalJson!.Value.ToArray(),
            session.TakeCapturedCanonicalJson());
        Assert.Null(session.TakeCapturedCanonicalJson());
    }

    [Theory]
    [InlineData("shape", "invalid-shape")]
    [InlineData("binding", "invalid-binding")]
    [InlineData("evidence", "invalid-evidence")]
    [InlineData("semantics", "invalid-semantics")]
    public async Task SubmitSession_InvalidCandidate_MapsOnlyItsClosedCategory(
        string mutation,
        string expectedCategory)
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string candidate = CreateMutatedDocumentJson(packet, mutation);
        PreparedSubmitCodeLogicSession session = CreateSubmissionSession(packet);

        string? outcome = await InvokeSubmitAsync(session, candidate);

        Assert.Equal(expectedCategory, outcome);
        Assert.True(session.HasSubmission);
        Assert.False(session.NavigationAllowed);
        Assert.False(session.ShouldTerminate);
        Assert.Null(session.TakeCapturedCanonicalJson());
        Assert.DoesNotContain(candidate, outcome, StringComparison.Ordinal);
        Assert.DoesNotContain("validator", outcome, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source", outcome, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitSession_InvalidCandidate_ReportsExactlyOneSanitizedCategoryAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string candidate = CreateMutatedDocumentJson(packet, "binding");
        RecordingSubmissionDiagnosticObserver observer = new();
        PreparedSubmitCodeLogicSession session =
            new PreparedSubmitCodeLogicFunction(observer).CreateSession(packet);

        string? outcome = await InvokeSubmitAsync(session, candidate);

        Assert.Equal("invalid-binding", outcome);
        PreparedPass1SubmissionRejectionKind category = Assert.Single(observer.Categories);
        Assert.Equal(PreparedPass1SubmissionRejectionKind.InvalidBinding, category);
        Assert.DoesNotContain(candidate, string.Join(',', observer.Categories));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSubmission_ReportsCategoryAndRetainsClosedReasonAsync()
    {
        const string invalidDocument =
            "{\"schemaIdentity\":\"code-logic/file-v2\",\"schemaIdentity\":\"code-logic/file-v2\"}";
        RecordingSubmissionDiagnosticObserver observer = new();
        SubmitThenNavigateHandler transport = new(invalidDocument);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            observer: observer);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Finalize only through submit_code_logic.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("worker-result-rejected", result.ReasonCode);
        Assert.Equal(
            PreparedPass1SubmissionRejectionKind.InvalidShape,
            Assert.Single(observer.Categories));
        Assert.Empty(result.Utf8Json.ToArray());
    }

    [Theory]
    [MemberData(nameof(ServerOwnedBindingPropertyNames))]
    public async Task SubmitSession_EveryServerOwnedBindingDriftMapsInvalidBindingAsync(
        string property)
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string candidate = CreateBindingMutationDocumentJson(packet, property);
        PreparedPass1ValidationResult validation = PreparedPass1ResultParser.Validate(
            packet,
            Encoding.UTF8.GetBytes(candidate));
        Assert.False(validation.IsValid);
        Assert.Equal(PreparedPass1ValidationFailureKind.Binding, validation.FailureKind);

        PreparedSubmitCodeLogicSession session = CreateSubmissionSession(packet);
        Assert.Equal("invalid-binding", await InvokeSubmitAsync(session, candidate));
    }

    [Theory]
    [InlineData("path")]
    [InlineData("sourceHash")]
    [InlineData("byteStart")]
    [InlineData("lineStart")]
    [InlineData("evidenceLocator")]
    public async Task SubmitSession_SourceAnchorEvidenceDriftMapsInvalidEvidenceAsync(
        string mutation)
    {
        PreparedPass1WorkerPacket packet = mutation is "lineStart"
            ? CreateMultiLinePacket()
            : CreatePacket();
        string candidate = CreateEvidenceMutationDocumentJson(packet, mutation);
        PreparedSubmitCodeLogicSession session = CreateSubmissionSession(packet);

        Assert.Equal("invalid-evidence", await InvokeSubmitAsync(session, candidate));
    }

    [Fact]
    public async Task SubmitSession_SecondInvalidAndThirdSubmission_TerminateWithoutCaptureAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        PreparedSubmitCodeLogicSession session = CreateSubmissionSession(packet);
        string invalidBinding = CreateMutatedDocumentJson(packet, "binding");
        string invalidSemantics = CreateMutatedDocumentJson(packet, "semantics");

        Assert.Equal("invalid-binding", await InvokeSubmitAsync(session, invalidBinding));
        Assert.False(session.ShouldTerminate);
        Assert.Equal("invalid-semantics", await InvokeSubmitAsync(session, invalidSemantics));
        Assert.True(session.ShouldTerminate);
        Assert.Null(session.TakeCapturedCanonicalJson());
        Assert.Equal("invalid-shape", await InvokeSubmitAsync(session, CreateValidDocumentJson(packet)));
        Assert.Null(session.TakeCapturedCanonicalJson());
    }

    [Fact]
    public async Task SubmitSession_InvalidSemantics_ReportsClosedRuleWithoutCandidateContentAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string candidate = CreateMutatedDocumentJson(packet, "semantics");
        PreparedPass1ValidationResult validation = PreparedPass1ResultParser.Validate(
            packet,
            Encoding.UTF8.GetBytes(candidate));

        Assert.Null(
            typeof(PreparedPass1ValidationResult).GetProperty(
                "DiagnosticMessage",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Equal(PreparedPass1ValidationFailureKind.Semantics, validation.FailureKind);

        var observer = new PreparedPass1SubmissionDiagnosticObserver();
        PreparedSubmitCodeLogicSession session = new PreparedSubmitCodeLogicFunction(observer)
            .CreateSession(packet);
        Assert.Equal("invalid-semantics", await InvokeSubmitAsync(session, candidate));
        Assert.Null(
            typeof(IPreparedPass1SubmissionDiagnosticObserver).GetProperty(
                "DiagnosticMessages",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.DoesNotContain(
            typeof(IPreparedPass1SubmissionDiagnosticObserver).GetMethods(),
            method => method.Name == "Observe" && method.GetParameters().Length == 2);
    }

    [Fact]
    public async Task SubmitSession_OuterShapeDriftAndWrongValueKind_ReturnOnlyInvalidShapeAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string validDocument = CreateValidDocumentJson(packet);

        PreparedSubmitCodeLogicSession missingDocument = CreateSubmissionSession(packet);
        object? missing = await missingDocument.Function.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>()),
            CancellationToken.None);
        Assert.Equal("invalid-shape", ToolResultText(missing));

        PreparedSubmitCodeLogicSession extraOuter = CreateSubmissionSession(packet);
        object? extra = await extraOuter.Function.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["document"] = ParseElement(validDocument),
                ["unexpected"] = "not-authoritative",
            }),
            CancellationToken.None);
        Assert.Equal("invalid-shape", ToolResultText(extra));

        PreparedSubmitCodeLogicSession wrongKind = CreateSubmissionSession(packet);
        object? wrong = await wrongKind.Function.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["document"] = ParseElement("\"not-an-object\""),
            }),
            CancellationToken.None);
        Assert.Equal("invalid-shape", ToolResultText(wrong));
    }

    [Fact]
    public async Task SubmitSession_CaptureIsExecutionLocalAndClearedAfterTakeAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string validDocument = CreateValidDocumentJson(packet);
        byte[] expected = PreparedPass1ResultParser.Validate(
            packet,
            Encoding.UTF8.GetBytes(validDocument)).CanonicalJson!.Value.ToArray();

        PreparedSubmitCodeLogicSession first = CreateSubmissionSession(packet);
        PreparedSubmitCodeLogicSession second = CreateSubmissionSession(packet);
        Assert.Equal("accepted", await InvokeSubmitAsync(first, validDocument));
        Assert.Equal("accepted", await InvokeSubmitAsync(second, validDocument));

        Assert.Equal(expected, first.TakeCapturedCanonicalJson());
        Assert.Null(first.TakeCapturedCanonicalJson());
        Assert.Equal(expected, second.TakeCapturedCanonicalJson());
        Assert.Null(second.TakeCapturedCanonicalJson());
    }

    [Fact]
    public async Task ExecuteAsync_SubmitThenNavigate_RejectsWithoutNavigationAccessAsync()
    {
        const string invalidDocument =
            "{\"schemaIdentity\":\"code-logic/file-v2\",\"schemaIdentity\":\"code-logic/file-v2\"}";
        SubmitThenNavigateHandler transport = new(invalidDocument);
        RecordingNavigationService navigation = new();
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            executionObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Finalize only through submit_code_logic.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("worker-result-rejected", result.ReasonCode);
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.Empty(navigation.Requests);
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall],
            diagnostics.Categories);
        Assert.Equal("NavigationUnavailable", ReadFunctionCallSubtype(diagnostics));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownFunctionCall_TerminatesAsClosedRejectionAsync()
    {
        SingleToolCallHandler transport = new("unknown_function", "{}");
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            executionObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Use only the two declared functions.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("unclassified-systemic", result.ReasonCode);
        Assert.Single(transport.RequestBodies);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall],
            diagnostics.Categories);
        Assert.Equal("UnknownFunction", ReadFunctionCallSubtype(diagnostics));
    }

    [Fact]
    public async Task ExecuteAsync_ParallelFunctionCalls_TerminatesWithoutCandidateCaptureAsync()
    {
        ParallelToolCallHandler transport = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Issue at most one function call at a time.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("unclassified-systemic", result.ReasonCode);
        Assert.Single(transport.RequestBodies);
        Assert.Empty(result.Utf8Json.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_CompleteTurnWithLegalNavigationThenUnknownSibling_PreflightsBeforeAnyCallbackAsync()
    {
        LegalNavigationThenUnknownSiblingHandler transport = new();
        RecordingNavigationService navigation = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Use only the two declared functions.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("unclassified-systemic", result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Single(transport.RequestBodies);

        // T103's public guarantee is whole-provider-turn atomicity: the
        // later illegal sibling must prevent the earlier legal navigation
        // callback from reaching the application seam.
        Assert.Empty(navigation.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleCallTurnWithLegalThenUnknownSibling_HasNoPartialCallbackAsync()
    {
        MultipleToolCallHandler transport = new("navigate_code", "unknown_function");
        RecordingNavigationService navigation = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Reject the complete turn if any sibling function call is illegal.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Empty(navigation.Requests);
    }

    [Theory]
    [InlineData("mixed-navigate-submit", "MixedFunctions")]
    [InlineData("multiple-submissions", "MultipleSubmissions")]
    [InlineData("over-remaining-navigation-budget", "NavigationBudgetExceeded")]
    [InlineData("duplicate-call-ids", "InvalidCallIdentity")]
    [InlineData("blank-call-ids", "InvalidCallIdentity")]
    public async Task ExecuteAsync_IllegalWholeTurnShapesHaveNoApplicationEffectsAsync(
        string shape,
        string expectedSubtype)
    {
        ToolCallShape[] calls = shape switch
        {
            "mixed-navigate-submit" =>
            [
                NavigateCall("call-navigate", "first"),
                new ToolCallShape("call-submit", "submit_code_logic", "{}"),
            ],
            "multiple-submissions" =>
            [
                new ToolCallShape("call-submit-1", "submit_code_logic", "{}"),
                new ToolCallShape("call-submit-2", "submit_code_logic", "{}"),
            ],
            "over-remaining-navigation-budget" =>
                Enumerable.Range(1, 9)
                    .Select(index => NavigateCall($"call-navigate-{index}", $"over-{index}"))
                    .ToArray(),
            "duplicate-call-ids" =>
            [
                NavigateCall("duplicate-call", "first"),
                NavigateCall("duplicate-call", "second"),
            ],
            "blank-call-ids" =>
            [
                NavigateCall(string.Empty, "first"),
                NavigateCall(string.Empty, "second"),
            ],
            _ => throw new Xunit.Sdk.XunitException($"Unexpected T103 shape: {shape}"),
        };
        ToolCallShapeHandler transport = new(calls);
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        RecordingSubmissionDiagnosticObserver submissionObserver = new();
        PreparedPass1ExecutionDiagnosticObserver executionDiagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            submissionObserver,
            executionObserver: executionDiagnostics,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Reject any complete turn that is not one legal all-navigation batch or one sole submission.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Single(transport.RequestBodies);
        Assert.Empty(navigation.Requests);
        Assert.Empty(reservations.Requests);
        Assert.Empty(submissionObserver.Categories);
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall],
            executionDiagnostics.Categories);
        Assert.Equal(expectedSubtype, ReadFunctionCallSubtype(executionDiagnostics));
    }

    [Fact]
    public async Task ExecuteAsync_BudgetExhaustionBeforeForcedTurnNine_StopsAtEightCallbacksWithoutRequestTenOrElevenAsync()
    {
        TurnTenIllegalShapeHandler transport = new();
        RecordingNavigationService navigation = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Stop at forced turn nine; reject its illegal function shape without request ten or eleven.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(9, transport.RequestBodies.Count);
        Assert.Equal(8, navigation.Requests.Count);
        Assert.DoesNotContain(
            transport.RequestBodies,
            body => body.Contains("request-ten", StringComparison.Ordinal) ||
                   body.Contains("request-eleven", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_TurnNineMixedFunctions_ReportsTerminalMixedFunctionsAsync()
    {
        TurnTenMixedShapeHandler transport = new();
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            executionObserver: diagnostics,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Reject any illegal function shape on forced turn nine before another callback or request.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(9, transport.RequestBodies.Count);
        Assert.Equal(8, navigation.Requests.Count);
        Assert.Equal(8, reservations.Requests.Count);
        Assert.DoesNotContain(
            transport.RequestBodies,
            body => body.Contains("request-eleven", StringComparison.Ordinal));
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall],
            diagnostics.Categories);
        Assert.Equal("TerminalMixedFunctions", ReadFunctionCallSubtype(diagnostics));
    }

    [Fact]
    public async Task ExecuteAsync_TurnNineTwoSubmissions_UsesDistinctTerminalTopologySubtypeAsync()
    {
        TurnTenDoubleSubmitHandler transport = new();
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        RecordingSubmissionDiagnosticObserver submissionObserver = new();
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            submissionObserver,
            executionObserver: diagnostics,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Reject a terminal response containing two submit_code_logic calls without any application effect.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(9, transport.RequestBodies.Count);
        Assert.Equal(8, navigation.Requests.Count);
        Assert.Equal(8, reservations.Requests.Count);
        Assert.Empty(submissionObserver.Categories);
        Assert.DoesNotContain(
            transport.RequestBodies,
            body => body.Contains("request-eleven", StringComparison.Ordinal));
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall],
            diagnostics.Categories);
        Assert.Equal("TerminalMultipleSubmissions", ReadFunctionCallSubtype(diagnostics));
    }

    [Fact]
    public async Task ExecuteAsync_TurnNineNavigation_ReportsTerminalNavigationAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        TurnTenNavigationHandler transport = new();
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        RecordingSubmissionDiagnosticObserver submissionObserver = new();
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            submissionObserver,
            executionObserver: diagnostics,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "The terminal request names submit_code_logic, but reject any provider navigation without invoking it.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(9, transport.RequestBodies.Count);
        Assert.Equal(8, navigation.Requests.Count);
        Assert.Equal(8, reservations.Requests.Count);
        Assert.Empty(submissionObserver.Categories);
        Assert.DoesNotContain(
            transport.RequestBodies,
            body => body.Contains("request-eleven", StringComparison.Ordinal));
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall],
            diagnostics.Categories);
        Assert.Equal("TerminalNavigation", ReadFunctionCallSubtype(diagnostics));

        object authority = CreateDeclarationAuthority(packet);
        for (int index = 0; index < 8; index++)
        {
            AssertT132ToolChoice(transport.RequestBodies[index], index + 1);
        }
        AssertAd30TerminalRequest(
            transport.RequestBodies[8],
            authority,
            expectedCorrectionCategory: null);
    }

    [Fact]
    public async Task ExecuteAsync_TurnNineMultipleNavigations_ReportsTerminalMultipleNavigationsAsync()
    {
        TurnTenMultipleNavigationHandler transport = new();
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        RecordingSubmissionDiagnosticObserver submissionObserver = new();
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            submissionObserver,
            executionObserver: diagnostics,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Reject a terminal response containing multiple navigate_code calls without any application effect.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(9, transport.RequestBodies.Count);
        Assert.Equal(8, navigation.Requests.Count);
        Assert.Equal(8, reservations.Requests.Count);
        Assert.Empty(submissionObserver.Categories);
        Assert.DoesNotContain(
            transport.RequestBodies,
            body => body.Contains("request-eleven", StringComparison.Ordinal));
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall],
            diagnostics.Categories);
        Assert.Equal("TerminalMultipleNavigations", ReadFunctionCallSubtype(diagnostics));
    }

    [Fact]
    public async Task ExecuteAsync_TurnNineFinalization_UsesHistoryFreeThreeMessageProjectionAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        TurnTenScriptedHandler transport = new(
            "first-valid",
            closedNavigationTurns: 8,
            CreateValidDocumentJson(packet));
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Finalize through the history-free Terminal Context Projection on forced turn nine.");

        Assert.Equal(9, transport.RequestBodies.Count);
        Assert.DoesNotContain(
            transport.RequestBodies,
            body => body.Contains("request-eleven", StringComparison.Ordinal));

        using JsonDocument finalRequest = JsonDocument.Parse(transport.RequestBodies[^1]);
        JsonElement root = finalRequest.RootElement;
        AssertT132ToolChoice(transport.RequestBodies[^1], 10);
        AssertT145DeclarationIdentity(
            transport.RequestBodies[^1],
            10,
            CreateDeclarationAuthority(packet));
        Assert.Equal("function", root.GetProperty("tool_choice").GetProperty("type").GetString());
        Assert.Equal(
            "submit_code_logic",
            root.GetProperty("tool_choice").GetProperty("function").GetProperty("name").GetString());
        Assert.Single(root.GetProperty("tools").EnumerateArray());
        JsonElement messages = root.GetProperty("messages");
        Assert.Equal(3, messages.GetArrayLength());
        Assert.Equal(
            ["system", "user", "user"],
            messages.EnumerateArray()
                .Select(message => message.GetProperty("role").GetString())
                .ToArray());
        Assert.DoesNotContain("assistant", messages.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tool_calls", messages.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("navigate_code", messages.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("reasoning", messages.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "submit_code_logic",
            root.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_FirstRejectedSubmission_UsesHistoryFreeCorrectionProjectionAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        FirstRejectedThenCorrectedHandler transport = new(
            CreateValidDocumentJson(packet),
            navigationTurns: 2);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "After one rejected submission, use the history-free correction projection.");

        Assert.Equal(4, transport.RequestBodies.Count);
        using JsonDocument correctionRequest = JsonDocument.Parse(transport.RequestBodies[^1]);
        JsonElement choice = correctionRequest.RootElement.GetProperty("tool_choice");
        Assert.Equal(JsonValueKind.Object, choice.ValueKind);
        Assert.Contains("submit_code_logic", choice.GetRawText(), StringComparison.Ordinal);
        JsonElement tools = correctionRequest.RootElement.GetProperty("tools");
        Assert.Single(tools.EnumerateArray());
        Assert.Contains("submit_code_logic", tools.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("navigate_code", tools.GetRawText(), StringComparison.Ordinal);
        JsonElement messages = correctionRequest.RootElement.GetProperty("messages");
        Assert.Equal(3, messages.GetArrayLength());
        Assert.Equal(
            ["system", "user", "user"],
            messages.EnumerateArray()
                .Select(message => message.GetProperty("role").GetString())
                .ToArray());
        Assert.DoesNotContain("assistant", messages.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tool_calls", messages.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("navigate_code", messages.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("firstSubmissionCorrection", messages.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void IllegalFunctionCallSubtype_SeparatesTurnDefenseBranchesByDistinctMembers()
    {
        string[] subtypeNames = Enum.GetNames(typeof(PreparedPass1IllegalFunctionCallSubtype));

        Assert.Contains("TerminalTurnRequiresSubmission", subtypeNames);
        Assert.Contains("SubmissionUnavailable", subtypeNames);
        Assert.Contains("PostTurnLimit", subtypeNames);
    }

    private static ToolCallShape NavigateCall(string id, string symbol) =>
        new(
            id,
            "navigate_code",
            $"{{\"operation\":\"Definition\",\"target\":{{\"repositoryRelativePath\":\"src/Target.java\",\"symbol\":\"{symbol}\",\"position\":null,\"exactLineRange\":null,\"expectedSourceHash\":\"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\"}},\"depth\":1,\"parentObservationIdentity\":null}}");

    [Fact]
    public async Task ExecuteAsync_CompleteAllNavigationBatch_RemainsEligibleAndReleasesUpdatesSeriallyAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string submittedDocument = CreateValidDocumentJson(packet);
        LegalAllNavigationBatchHandler transport = new(submittedDocument);
        RecordingNavigationService navigation = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Use a fully preflighted all-navigation batch, then submit the result.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.Equal(2, navigation.Requests.Count);
        Assert.Equal(
            ["call-navigate-1", "call-navigate-2"],
            navigation.Requests.Select(request => request.Target.Symbol).ToArray());
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(8, 2)]
    [InlineData(8, 3)]
    [InlineData(8, 4)]
    [InlineData(8, 5)]
    [InlineData(8, 6)]
    [InlineData(8, 7)]
    [InlineData(8, 8)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(1, 8)]
    public async Task ExecuteAsync_AllNavigationSaturation_ReplaysCompleteTurnAndSubmitsAfterCapacityPrefixAsync(
        int remainingNavigationBudget,
        int navigationCallCount)
    {
        PreparedPass1WorkerPacket packet = CreatePacketWithBudget(remainingNavigationBudget);
        string finalDocument = CreateValidDocumentJson(packet);
        SaturationReplayHandler transport = new(navigationCallCount, finalDocument);
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Admit the provider-order navigation prefix, return one bounded result per excess call, then accept a sole submission.");

        int expectedPrefixCount = Math.Min(remainingNavigationBudget, navigationCallCount);
        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.Equal(expectedPrefixCount, navigation.Requests.Count);
        Assert.Equal(expectedPrefixCount, reservations.Requests.Count);
        Assert.Equal(
            Enumerable.Range(9 - remainingNavigationBudget, expectedPrefixCount)
                .Select(value => (ulong)value),
            navigation.Requests.Select(request => request.ToolAttemptOrdinal));
        Assert.Equal(
            Enumerable.Range(8 - remainingNavigationBudget, expectedPrefixCount),
            reservations.Requests.Select(request =>
                request.Cursor?.ToolAttemptCount ?? request.Scope.ToolAttemptCount));

        string replay = transport.RequestBodies[1];
        for (int callOrdinal = 1; callOrdinal <= navigationCallCount; callOrdinal++)
        {
            Assert.Contains($"call-navigate-{callOrdinal}", replay, StringComparison.Ordinal);
        }

        for (int callOrdinal = expectedPrefixCount + 1;
             callOrdinal <= navigationCallCount;
             callOrdinal++)
        {
            Assert.Contains("navigation-budget-exhausted", replay, StringComparison.Ordinal);
        }

        Assert.Equal(
            navigationCallCount - expectedPrefixCount,
            replay.Split("navigation-budget-exhausted", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task ExecuteAsync_InitialZero_DirectSubmissionUsesProviderInsteadOfDeferredFallbackAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacketWithBudget(0);
        string finalDocument = CreateValidDocumentJson(packet);
        SaturationReplayHandler transport = new(0, finalDocument);
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "At exact zero remaining navigation capacity, submit the valid document directly.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Null(result.ReasonCode);
        Assert.Equal(
            PreparedPass1ResultParser.Validate(
                packet,
                Encoding.UTF8.GetBytes(finalDocument)).CanonicalJson!.Value.ToArray(),
            result.Utf8Json.ToArray());
        Assert.Single(transport.RequestBodies);
        Assert.Empty(navigation.Requests);
        Assert.Empty(reservations.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_InitialZero_ClosedNavigationThenSubmissionHasNoApplicationEffectsAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacketWithBudget(0);
        string finalDocument = CreateValidDocumentJson(packet);
        SaturationReplayHandler transport = new(1, finalDocument);
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "At exact zero remaining capacity, return a bounded navigation result and then submit.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.Contains("navigation-budget-exhausted", transport.RequestBodies[1], StringComparison.Ordinal);
        Assert.Empty(navigation.Requests);
        Assert.Empty(reservations.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_NineNavigationCallsRemainWholeResponseRejectedAtCapacityEightAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacketWithBudget(8);
        SaturationReplayHandler transport = new(9, CreateValidDocumentJson(packet));
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            executionObserver: diagnostics,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Reject a complete response containing nine navigation calls before any effect.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("unclassified-systemic", result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Single(transport.RequestBodies);
        Assert.Empty(navigation.Requests);
        Assert.Empty(reservations.Requests);
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall],
            diagnostics.Categories);
        Assert.Equal("NavigationBudgetExceeded", ReadFunctionCallSubtype(diagnostics));
    }

    [Theory]
    [InlineData("first-valid", 8, true, 9)]
    [InlineData("first-invalid", 8, false, 10)]
    [InlineData("second-valid", 8, true, 10)]
    public async Task ExecuteAsync_ProviderTurnNineAndOptionalCorrection_MapsSdkIterationsAndTerminatesWithoutRequestElevenAsync(
        string scenario,
        int closedNavigationTurns,
        bool expectCompleted,
        int expectedRequestCount)
    {
        PreparedPass1WorkerPacket packet = CreatePacketWithBudget(0);
        string finalDocument = CreateValidDocumentJson(packet);
        TurnTenScriptedHandler transport = new(scenario, closedNavigationTurns, finalDocument);
        object authority = CreateDeclarationAuthority(packet);
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Use forced provider turn nine (SDK iteration eight), then at most its SDK iteration nine correction, and never request turn eleven.");

        Assert.Equal(
            expectCompleted
                ? PreparedPass1RawWorkerOutcome.Completed
                : PreparedPass1RawWorkerOutcome.Rejected,
            result.Outcome);
        Assert.Equal(
            expectCompleted ? null : "worker-result-rejected",
            result.ReasonCode);
        Assert.Equal(expectedRequestCount, transport.RequestBodies.Count);
        Assert.DoesNotContain(
            transport.RequestBodies,
            body => body.Contains("request-eleven", StringComparison.Ordinal));
        Assert.Empty(navigation.Requests);
        Assert.Empty(reservations.Requests);
        for (int index = 0; index < 8; index++)
        {
            AssertT132ToolChoice(transport.RequestBodies[index], index + 1);
            AssertT145DeclarationIdentity(transport.RequestBodies[index], index + 1, authority);
        }
        AssertAd30TerminalRequest(
            transport.RequestBodies[8],
            authority,
            expectedCorrectionCategory: null);
        if (expectedRequestCount == 10)
        {
            AssertAd30TerminalRequest(
                transport.RequestBodies[9],
                authority,
                expectedCorrectionCategory: "InvalidShape");
        }
    }

    [Fact]
    public async Task ExecuteAsync_AllNavigationCallbacks_ReserveBeforeSerialCallbackAndUseLatestCursorAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        string submittedDocument = CreateValidDocumentJson(packet);
        LegalAllNavigationBatchHandler transport = new(submittedDocument);
        RecordingReservationStore reservations = new();
        ReservationAwareNavigationService navigation = new(reservations);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Preflight the complete navigation batch, reserve each call, then submit the result.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
        Assert.Equal(["reserve", "callback", "reserve", "callback"], reservations.Events);
        Assert.Equal(2, reservations.Requests.Count);
        Assert.Equal(2, navigation.Requests.Count);
        Assert.Null(reservations.Requests[0].Cursor);
        Assert.Same(reservations.Tickets[0], reservations.Requests[1].Cursor);
        Assert.Equal([1, 2], navigation.Requests.Select(request => (int)request.ToolAttemptOrdinal));
        Assert.Equal(
            [0, 1],
            reservations.Requests.Select(request => request.Scope.AcceptedCallCount));
        Assert.Equal(
            [0, 12],
            reservations.Requests.Select(request => request.Scope.EvidenceBytes));
        Assert.All(navigation.Requests, request =>
        {
            Assert.Equal(packet.JobId, request.JobId);
            Assert.Equal(packet.PairExecutionId, request.PairExecutionId);
            Assert.Equal(packet.ServerAuthorization!.JobStateVersion, request.JobStateVersion);
            Assert.Equal(packet.ServerAuthorization.PairStateVersion, request.PairStateVersion);
            Assert.Equal(packet.ServerAuthorization.LeaseToken, request.LeaseToken);
            Assert.Equal(packet.ServerAuthorization.LeaseExpiresAt, request.LeaseExpiresAt);
        });
        Assert.Contains("call-navigate-1", transport.RequestBodies[1], StringComparison.Ordinal);
        Assert.Contains("call-navigate-2", transport.RequestBodies[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public async Task ExecuteAsync_ReservationFailureStopsBeforeAffectedAndLaterCallbacksAsync(
        int failureOrdinal,
        int expectedCallbackCount)
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        LegalAllNavigationBatchHandler transport = new(CreateValidDocumentJson(packet));
        RecordingReservationStore reservations = new() { FailureOrdinal = failureOrdinal };
        ReservationAwareNavigationService navigation = new(reservations);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            reservationStore: reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Stop the complete navigation batch when a durable reservation is unavailable.");

        Assert.Equal(failureOrdinal, reservations.Requests.Count);
        Assert.Equal(expectedCallbackCount, navigation.Requests.Count);
        Assert.Single(transport.RequestBodies);
        Assert.NotEqual(PreparedPass1RawWorkerOutcome.Completed, result.Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_ClientBadRequest_ReportsOnlyProviderClientRequestRejectedAsync()
    {
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            new BadRequestHandler(),
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            executionObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Return a bounded result through the provider client.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("unclassified-systemic", result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.ProviderClientRequestRejected],
            diagnostics.Categories);
    }

    [Theory]
    [InlineData(401, "ProviderUnavailable", "provider-configuration", "")]
    [InlineData(402, "ProviderUnavailable", "provider-configuration", "")]
    [InlineData(403, "ProviderUnavailable", "provider-configuration", "")]
    [InlineData(400, "Rejected", "unclassified-systemic", "ProviderClientRequestRejected")]
    [InlineData(422, "Rejected", "unclassified-systemic", "ProviderClientRequestRejected")]
    [InlineData(408, "ProviderUnavailable", "provider-unavailable", "")]
    [InlineData(429, "ProviderUnavailable", "provider-unavailable", "")]
    [InlineData(500, "ProviderUnavailable", "provider-unavailable", "")]
    [InlineData(502, "ProviderUnavailable", "provider-unavailable", "")]
    [InlineData(503, "ProviderUnavailable", "provider-unavailable", "")]
    public async Task ExecuteAsync_StatusFamiliesReturnClosedSanitizedOutcomesWithoutRawProviderEvidenceAsync(
        int statusCode,
        string expectedOutcome,
        string expectedReason,
        string expectedDiagnostic)
    {
        StatusCodeHandler transport = new((HttpStatusCode)statusCode);
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            executionObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Do not retain this private-source-marker in a provider failure.");

        Assert.Equal(expectedOutcome, result.Outcome.ToString());
        Assert.Equal(expectedReason, result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.DoesNotContain(
            "private-source-marker",
            Encoding.UTF8.GetString(result.Utf8Json.Span),
            StringComparison.Ordinal);
        Assert.Single(transport.RequestBodies);
        if (expectedDiagnostic.Length == 0)
        {
            Assert.Empty(diagnostics.Categories);
        }
        else
        {
            Assert.Equal(
                [Enum.Parse<PreparedPass1ExecutionDiagnosticKind>(expectedDiagnostic)],
                diagnostics.Categories);
        }
    }

    [Theory]
    [InlineData("temperature", "0.2")]
    [InlineData("top_p", "0.2")]
    [InlineData("presence_penalty", "0.2")]
    [InlineData("frequency_penalty", "0.2")]
    public void NormalizeAndValidateDeepSeekRequestBody_RejectsInjectedSamplingFieldBeforeTransport(
        string fieldName,
        string fieldValue)
    {
        string body = $"{{\"max_tokens\":32768,\"stream\":true,\"{fieldName}\":{fieldValue}}}";

        byte[]? normalized = InvokeDeepSeekRequestNormalizer(body);

        Assert.Null(normalized);
    }

    [Fact]
    public void NormalizeAndValidateDeepSeekRequestBody_AssistantToolTextParts_CollapsesContentToString()
    {
        const string body =
            "{\"max_tokens\":32768,\"stream\":true,\"messages\":[" +
            "{\"role\":\"assistant\",\"content\":[{\"type\":\"text\",\"text\":\"Inspecting the bounded definition.\"}]," +
            "\"reasoning_content\":\"bounded reasoning\",\"tool_calls\":[{\"id\":\"call-1\",\"type\":\"function\"," +
            "\"function\":{\"name\":\"navigate_code\",\"arguments\":\"{}\"}}]}," +
            "{\"role\":\"tool\",\"tool_call_id\":\"call-1\",\"content\":\"{}\"}]}";

        byte[]? normalized = InvokeDeepSeekRequestNormalizer(body);

        Assert.NotNull(normalized);
        using JsonDocument request = JsonDocument.Parse(normalized);
        JsonElement assistant = request.RootElement.GetProperty("messages")[0];
        Assert.Equal(
            "Inspecting the bounded definition.",
            assistant.GetProperty("content").GetString());
        Assert.Equal("bounded reasoning", assistant.GetProperty("reasoning_content").GetString());
        Assert.Single(assistant.GetProperty("tool_calls").EnumerateArray());
    }

    [Fact]
    public void NormalizeAndValidateDeepSeekRequestBody_AssistantToolNonTextPart_RejectsClosed()
    {
        const string body =
            "{\"max_tokens\":32768,\"stream\":true,\"messages\":[" +
            "{\"role\":\"assistant\",\"content\":[{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/png;base64,AA==\"}}]," +
            "\"tool_calls\":[{\"id\":\"call-1\",\"type\":\"function\",\"function\":{\"name\":\"navigate_code\",\"arguments\":\"{}\"}}]}," +
            "{\"role\":\"tool\",\"tool_call_id\":\"call-1\",\"content\":\"{}\"}]}";

        byte[]? normalized = InvokeDeepSeekRequestNormalizer(body);

        Assert.Null(normalized);
    }

    [Theory]
    [InlineData("unknown", "UnknownFunction")]
    [InlineData("parallel", "MixedFunctions")]
    [InlineData("overlap", "UnknownFunction")]
    public async Task ExecuteAsync_IllegalFunctionCall_ReportsDeterministicClosedSubtypeAsync(
        string scenario,
        string expectedSubtype)
    {
        HttpMessageHandler transport = scenario switch
        {
            "unknown" => new SingleToolCallHandler("unknown_function", "{}"),
            "parallel" => new ParallelToolCallHandler(),
            "overlap" => new MultipleToolCallHandler(
                "submit_code_logic",
                "unknown_function"),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected T099 scenario: {scenario}"),
        };
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            executionObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Use only one of the two declared functions.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("unclassified-systemic", result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall],
            diagnostics.Categories);
        Assert.Equal(expectedSubtype, ReadFunctionCallSubtype(diagnostics));
    }

    private static string ReadFunctionCallSubtype(
        PreparedPass1ExecutionDiagnosticObserver diagnostics)
    {
        PropertyInfo? property = diagnostics.GetType().GetProperty(
            "FunctionCallSubtype",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.True(
            property is not null,
            "T099 RED: the execution-local observer has no closed FunctionCallSubtype seam.");
        object? value = property!.GetValue(diagnostics);
        Assert.NotNull(value);
        string subtype = value!.ToString()!;
        Assert.Contains(
            subtype,
            new[]
            {
                "UnknownFunction",
                "MultipleFunctions",
                "MissingFunction",
                "InvalidCallIdentity",
                "MixedFunctions",
                "MultipleSubmissions",
                "TerminalMultipleSubmissions",
                "TerminalNavigation",
                "TerminalMixedFunctions",
                "TerminalMultipleNavigations",
                "NavigationBudgetExceeded",
                "NavigationUnavailable",
                "TurnIneligible",
                "TerminalTurnRequiresSubmission",
                "SubmissionUnavailable",
                "PostTurnLimit",
            });
        return subtype;
    }

    private static void AssertT132ToolChoice(string body, int requestOrdinal)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        JsonElement choice = root.GetProperty("tool_choice");
        if (requestOrdinal <= 9)
        {
            Assert.Equal(JsonValueKind.String, choice.ValueKind);
            Assert.Equal("required", choice.GetString());
        }
        else
        {
            Assert.Equal(JsonValueKind.Object, choice.ValueKind);
            Assert.Equal("function", choice.GetProperty("type").GetString());
            Assert.Equal("submit_code_logic", choice.GetProperty("function").GetProperty("name").GetString());
            Assert.Equal(2, choice.EnumerateObject().Count());
        }

        JsonElement[] tools = root.GetProperty("tools").EnumerateArray().ToArray();
        if (requestOrdinal <= 9)
        {
            Assert.Equal(2, tools.Length);
            Assert.Equal(
                ["navigate_code", "submit_code_logic"],
                tools.Select(tool => tool.GetProperty("function").GetProperty("name").GetString())
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray());
        }
        else
        {
            Assert.Single(tools);
            Assert.Equal(
                "submit_code_logic",
                tools[0].GetProperty("function").GetProperty("name").GetString());
        }
        Assert.All(tools, tool => Assert.True(tool.GetProperty("function").GetProperty("strict").GetBoolean()));
    }

    private static void AssertAd30TerminalRequest(
        string body,
        object authority,
        string? expectedCorrectionCategory)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        JsonElement choice = root.GetProperty("tool_choice");
        Assert.Equal(JsonValueKind.Object, choice.ValueKind);
        Assert.Equal("function", choice.GetProperty("type").GetString());
        Assert.Equal(
            "submit_code_logic",
            choice.GetProperty("function").GetProperty("name").GetString());
        Assert.Single(root.GetProperty("tools").EnumerateArray());
        Assert.Equal(
            "submit_code_logic",
            root.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.True(root.GetProperty("tools")[0].GetProperty("function").GetProperty("strict").GetBoolean());
        AssertT145DeclarationIdentity(body, 10, authority);

        JsonElement messages = root.GetProperty("messages");
        Assert.Equal(3, messages.GetArrayLength());
        Assert.Equal(
            ["system", "user", "user"],
            messages.EnumerateArray()
                .Select(message => message.GetProperty("role").GetString())
                .ToArray());
        Assert.DoesNotContain("assistant", messages.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tool_calls", messages.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("navigate_code", messages.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("reasoning", messages.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("arguments", messages.GetRawText(), StringComparison.Ordinal);

        using JsonDocument context = JsonDocument.Parse(
            messages[2].GetProperty("content").GetString()
            ?? throw new Xunit.Sdk.XunitException(
                "AD-30 terminal context message has no content."));
        Assert.Equal(
            ["schemaIdentity", "evidenceResults", "firstSubmissionCorrection"],
            context.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        JsonElement correction = context.RootElement.GetProperty("firstSubmissionCorrection");
        if (expectedCorrectionCategory is null)
        {
            Assert.Equal(JsonValueKind.Null, correction.ValueKind);
        }
        else
        {
            Assert.Equal(1, correction.GetProperty("attempt").GetInt32());
            Assert.Equal(expectedCorrectionCategory, correction.GetProperty("category").GetString());
            Assert.Equal(
                "UseMinimalValidDocument",
                correction.GetProperty("repairCode").GetString());
        }
    }

    private static void AssertT145DeclarationIdentity(
        string body,
        int requestOrdinal,
        object authority)
    {
        byte[] expected = requestOrdinal <= 9
            ? ReadDeclarationBytes(authority, "ProjectedUtf8")
            : ReadDeclarationBytes(authority, "ProjectedSubmitOnlyUtf8");
        string expectedSha256 = requestOrdinal <= 9
            ? ReadDeclarationString(authority, "ProjectedSha256")
            : ReadDeclarationString(authority, "ProjectedSubmitOnlySha256");
        using JsonDocument document = JsonDocument.Parse(body);
        byte[] actual = Encoding.UTF8.GetBytes(
            document.RootElement.GetProperty("tools").GetRawText());
        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(expected, actual);
        Assert.Equal(expectedSha256, Convert.ToHexString(SHA256.HashData(actual)));
    }

    private static object CreateDeclarationAuthority(PreparedPass1WorkerPacket packet)
    {
        AIFunction navigate = new PreparedNavigateCodeFunction(new NoOpNavigationService())
            .Create(new PreparedNavigateCodeBinding(
                Guid.Parse("11111111-1111-4111-8111-111111111111"),
                1,
                Guid.Parse("22222222-2222-4222-8222-222222222222")));
        AIFunction submit = new PreparedSubmitCodeLogicFunction()
            .CreateSession(packet)
            .Function;
        return DeepSeekStrictToolDeclarations.Create(navigate, submit);
    }

    private static byte[] ReadDeclarationBytes(object authority, string name)
    {
        PropertyInfo? property = authority.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object? value = property?.GetValue(authority);
        Assert.NotNull(value);
        return value switch
        {
            byte[] bytes => [.. bytes],
            ReadOnlyMemory<byte> memory => memory.ToArray(),
            _ => throw new Xunit.Sdk.XunitException(
                $"T145 RED: declaration member '{name}' is not byte data."),
        };
    }

    private static string ReadDeclarationString(object authority, string name)
    {
        PropertyInfo? property = authority.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object? value = property?.GetValue(authority);
        Assert.NotNull(value);
        return Assert.IsType<string>(value);
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedTransportException_ReportsOnlyUnexpectedExceptionAsync()
    {
        PreparedPass1ExecutionDiagnosticObserver diagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            new UnexpectedExceptionHandler(),
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            executionObserver: diagnostics);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Return a bounded result through the provider client.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("unclassified-systemic", result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.UnexpectedException],
            diagnostics.Categories);
    }

    [Fact]
    public async Task ExecuteAsync_FreeFormJsonWithoutSubmission_IsNotAuthoritativeAsync()
    {
        string validLookingJson = CreateValidDocumentJson(CreatePacket());
        StreamingHandler transport = new(validLookingJson);
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            CreatePacket(),
            "Free-form content is never a result; call submit_code_logic.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("worker-result-rejected", result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_CompletedZeroCallTurnFailsBeforeFiccWithPayloadFreeMissingFunctionAsync()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        StreamingHandler transport = new("normal assistant text without a function call");
        RecordingNavigationService navigation = new();
        RecordingReservationStore reservations = new();
        RecordingSubmissionDiagnosticObserver submissions = new();
        PreparedPass1ExecutionDiagnosticObserver executionDiagnostics = new();
        PreparedPass1JobExecutor executor = CreateExecutor(
            transport,
            new TestTimeProvider(),
            _testInactivityTimeout,
            _testAbsoluteTimeout,
            navigation,
            submissions,
            executionDiagnostics,
            reservations);

        PreparedPass1RawWorkerResult result = await executor.ExecuteAsync(
            packet,
            "Every completed provider turn must contain a declared function call.");

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, result.Outcome);
        Assert.Equal("worker-result-rejected", result.ReasonCode);
        Assert.Empty(result.Utf8Json.ToArray());
        Assert.Equal(
            [PreparedPass1SubmissionRejectionKind.MissingSubmission],
            submissions.Categories);
        Assert.Equal(
            [PreparedPass1ExecutionDiagnosticKind.IllegalFunctionCall],
            executionDiagnostics.Categories);
        Assert.Equal("MissingFunction", ReadFunctionCallSubtype(executionDiagnostics));
        Assert.Empty(navigation.Requests);
        Assert.Empty(reservations.Requests);
        Assert.Single(transport.RequestBodies);
    }

    private static PreparedSubmitCodeLogicSession CreateSubmissionSession(
        PreparedPass1WorkerPacket packet) =>
        new PreparedSubmitCodeLogicFunction().CreateSession(packet);

    private static async Task<string?> InvokeSubmitAsync(
        PreparedSubmitCodeLogicSession session,
        string document)
    {
        object? result = await session.Function.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["document"] = ParseElement(document),
            }),
            CancellationToken.None);
        return ToolResultText(result);
    }

    private static string? ToolResultText(object? result) => result switch
        {
            string value => value,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element => element.ToString(),
            _ => result?.ToString(),
        };

    private static JsonElement ParseElement(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string CreateMutatedDocumentJson(
        PreparedPass1WorkerPacket packet,
        string mutation)
    {
        JsonObject root = JsonNode.Parse(CreateValidDocumentJson(packet))!.AsObject();
        switch (mutation)
        {
            case "shape":
                root["unexpected"] = true;
                break;
            case "binding":
                root["binding"]!["jobId"] = "00000000-0000-4000-8000-000000000000";
                break;
            case "evidence":
                root["sourceAnchors"]![0]!["repositoryRelativePath"] = "src/Other.java";
                break;
            case "semantics":
                root["filePurpose"]!["sourceAnchorRefs"] = new JsonArray();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    public static IEnumerable<object[]> ServerOwnedBindingPropertyNames() =>
    [
        ["jobId"],
        ["scanExecutionId"],
        ["systemId"],
        ["repositoryId"],
        ["scanRunId"],
        ["profiledRevision"],
        ["fileCodeUnitId"],
        ["fileCodeUnitIdentityFingerprint"],
        ["preparedScanManifestId"],
        ["preparedScanManifestFingerprint"],
        ["pairPackageFingerprint"],
        ["repositoryRelativePath"],
        ["byteStart"],
        ["byteEnd"],
        ["lineStart"],
        ["lineEnd"],
        ["segmentOrdinal"],
        ["segmentCount"],
        ["fileHash"],
        ["sourceHash"],
        ["promptIdentity"],
        ["documentSchemaIdentity"],
        ["analysisRuntimeIdentityHash"],
    ];

    private static string CreateBindingMutationDocumentJson(
        PreparedPass1WorkerPacket packet,
        string property)
    {
        JsonObject root = JsonNode.Parse(CreateValidDocumentJson(packet))!.AsObject();
        JsonObject binding = root["binding"]!.AsObject();
        switch (property)
        {
            case "byteStart":
                binding[property] = packet.ByteStart + 1;
                break;
            case "byteEnd":
                binding[property] = packet.ByteEnd + 1;
                break;
            case "lineStart":
                binding[property] = packet.LineStart + 1;
                break;
            case "lineEnd":
                binding[property] = packet.LineEnd + 1;
                break;
            case "segmentOrdinal":
                binding[property] = packet.SegmentOrdinal + 1;
                break;
            case "segmentCount":
                binding[property] = packet.SegmentCount + 1;
                break;
            case "fileCodeUnitIdentityFingerprint":
            case "preparedScanManifestFingerprint":
            case "pairPackageFingerprint":
            case "fileHash":
            case "sourceHash":
            case "analysisRuntimeIdentityHash":
                binding[property] = new string('b', 64);
                break;
            case "jobId":
            case "scanExecutionId":
            case "systemId":
            case "repositoryId":
            case "scanRunId":
            case "fileCodeUnitId":
            case "preparedScanManifestId":
                binding[property] = "99999999-aaaa-4bbb-8ccc-dddddddddddd";
                break;
            default:
                binding[property] = "different-server-value";
                break;
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static string CreateEvidenceMutationDocumentJson(
        PreparedPass1WorkerPacket packet,
        string mutation)
    {
        JsonObject root = JsonNode.Parse(CreateValidDocumentJson(packet))!.AsObject();
        JsonObject anchor = root["sourceAnchors"]![0]!.AsObject();
        switch (mutation)
        {
            case "path":
                anchor["repositoryRelativePath"] = "src/Other.java";
                break;
            case "sourceHash":
                anchor["sourceHash"] = new string('b', 64);
                break;
            case "byteStart":
                anchor["byteStart"] = packet.ByteStart - 1;
                break;
            case "lineStart":
                anchor["lineStart"] = packet.LineStart + 1;
                anchor["lineEnd"] = packet.LineEnd + 1;
                anchor["evidenceLocator"] =
                    $"{packet.RepositoryRelativePath}#L{packet.LineStart + 1}-L{packet.LineEnd + 1}";
                break;
            case "evidenceLocator":
                anchor["evidenceLocator"] = $"{packet.RepositoryRelativePath}#L{packet.LineStart}";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static PreparedPass1WorkerPacket CreateMultiLinePacket()
    {
        PreparedPass1WorkerPacket packet = CreatePacket();
        byte[] source = Encoding.UTF8.GetBytes("class Example {\n}\n");
        return packet with
        {
            ByteEnd = source.Length,
            LineEnd = 2,
            SourceUtf8 = source,
            SourceHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(source)),
        };
    }

    private static string CreateValidDocumentJson(PreparedPass1WorkerPacket packet)
    {
        MethodInfo method = typeof(CodeLogicFileV2SchemaTests).GetMethod(
            "BaseJson",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Xunit.Sdk.XunitException("The authoritative Code Logic fixture is unavailable.");
        JsonObject root = JsonNode.Parse((string)method.Invoke(null, null)!)!.AsObject();
        JsonObject binding = root["binding"]!.AsObject();
        binding["jobId"] = packet.JobId.ToString("D");
        binding["scanExecutionId"] = packet.ScanExecutionId.ToString("D");
        binding["systemId"] = packet.SystemId.ToString("D");
        binding["repositoryId"] = packet.RepositoryId.ToString("D");
        binding["scanRunId"] = packet.ScanRunId.ToString("D");
        binding["profiledRevision"] = packet.ProfiledRevision;
        binding["fileCodeUnitId"] = packet.FileCodeUnitId.ToString("D");
        binding["fileCodeUnitIdentityFingerprint"] = packet.FileCodeUnitIdentityFingerprint;
        binding["preparedScanManifestId"] = packet.PreparedScanManifestId.ToString("D");
        binding["preparedScanManifestFingerprint"] = packet.PreparedScanManifestFingerprint;
        binding["pairPackageFingerprint"] = packet.PairPackageFingerprint;
        binding["repositoryRelativePath"] = packet.RepositoryRelativePath;
        binding["byteStart"] = packet.ByteStart;
        binding["byteEnd"] = packet.ByteEnd;
        binding["lineStart"] = packet.LineStart;
        binding["lineEnd"] = packet.LineEnd;
        binding["segmentOrdinal"] = packet.SegmentOrdinal;
        binding["segmentCount"] = packet.SegmentCount;
        binding["fileHash"] = packet.FileHash;
        binding["sourceHash"] = packet.SourceHash;
        binding["promptIdentity"] = packet.PromptIdentity;
        binding["documentSchemaIdentity"] = packet.DocumentSchemaIdentity;
        binding["analysisRuntimeIdentityHash"] = packet.AnalysisRuntimeIdentityHash;

        JsonObject anchor = root["sourceAnchors"]![0]!.AsObject();
        anchor["repositoryRelativePath"] = packet.RepositoryRelativePath;
        anchor["byteStart"] = packet.ByteStart;
        anchor["byteEnd"] = packet.ByteEnd;
        anchor["lineStart"] = packet.LineStart;
        anchor["lineEnd"] = packet.LineEnd;
        anchor["sourceHash"] = packet.SourceHash;
        anchor["evidenceLocator"] = $"{packet.RepositoryRelativePath}#L{packet.LineStart}-L{packet.LineEnd}";

        // BaseJson is a rich semantic fixture for a three-line source unit. Keep
        // only the schema-required, packet-valid minimum for this one-line packet:
        // no internal ranges, communication classifications, or navigation receipts.
        root["internalUnits"] = new JsonArray();
        root["executionFlow"] = new JsonArray();
        root["dataFlow"] = new JsonArray();
        root["rules"] = new JsonArray();
        root["dataAccess"] = new JsonArray();
        root["communicationEvidence"] = new JsonArray();
        root["knownReferences"] = new JsonArray();
        root["deferredObservations"] = new JsonArray();
        root["assumptions"] = new JsonArray();
        root["filePurpose"]!["responsibilities"] = new JsonArray(
            new JsonObject
            {
                ["description"] = "Describes the prepared file.",
                ["sourceAnchorRefs"] = new JsonArray("a001"),
            });

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static string CreateDocumentWithNavigationReceiptIds(
        PreparedPass1WorkerPacket packet,
        IReadOnlyList<Guid> receiptIds)
    {
        JsonObject root = JsonNode.Parse(CreateValidDocumentJson(packet))!.AsObject();
        JsonArray references = [];
        JsonArray observations = [];
        for (int index = 0; index < receiptIds.Count; index++)
        {
            references.Add(
                new JsonObject
                {
                    ["order"] = index + 1,
                    ["relationshipKind"] = "References",
                    ["targetDocumentVersionId"] = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee",
                    ["targetRepositoryRelativePath"] = "src/neutral/Target.java",
                    ["targetSourceHash"] = new string('e', 64),
                    ["navigationReceiptId"] = receiptIds[index].ToString("D"),
                    ["sourceClaimLocator"] = $"{packet.RepositoryRelativePath}#L{packet.LineStart}",
                    ["sourceAnchorRefs"] = new JsonArray("a001"),
                });
        }
        observations.Add(
            new JsonObject
            {
                ["order"] = 1,
                ["relationshipKind"] = "References",
                ["outcome"] = "Insufficient",
                ["sourceClaimLocator"] = $"{packet.RepositoryRelativePath}#L{packet.LineStart}",
                ["targetSymbol"] = "neutral.Target",
                ["targetRepositoryRelativePath"] = "src/neutral/Target.java",
                ["targetSourceHash"] = new string('e', 64),
                ["navigationOperation"] = "Definition",
                ["navigationDepth"] = 1,
                ["navigationReceiptIds"] = new JsonArray(
                    receiptIds.Select(receipt => JsonValue.Create(receipt.ToString("D"))).ToArray()),
                ["sourceAnchorRefs"] = new JsonArray("a001"),
            });
        root["knownReferences"] = references;
        root["deferredObservations"] = observations;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void AssertCorrectionContainsOnlyClosedMetadata(
        string requestBody,
        PreparedPass1SubmissionRepairCode expectedRepairCode,
        params Guid[] forbiddenReceiptIds)
    {
        using JsonDocument request = JsonDocument.Parse(requestBody);
        JsonElement messages = request.RootElement.GetProperty("messages");
        string contextJson = messages[messages.GetArrayLength() - 1]
            .GetProperty("content")
            .GetString()!;
        using JsonDocument context = JsonDocument.Parse(contextJson);
        JsonElement correction = context.RootElement.GetProperty("firstSubmissionCorrection");
        Assert.Equal(
            ["attempt", "category", "repairCode"],
            correction.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(1, correction.GetProperty("attempt").GetInt32());
        Assert.Equal("InvalidEvidence", correction.GetProperty("category").GetString());
        Assert.Equal(expectedRepairCode.ToString(), correction.GetProperty("repairCode").GetString());
        string correctionText = correction.GetRawText();
        Assert.DoesNotContain("knownReferences", correctionText, StringComparison.Ordinal);
        Assert.DoesNotContain("deferredObservations", correctionText, StringComparison.Ordinal);
        foreach (Guid receiptId in forbiddenReceiptIds)
        {
            Assert.DoesNotContain(receiptId.ToString("D"), correctionText, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static byte[]? InvokeDeepSeekRequestNormalizer(string body)
    {
        MethodInfo normalizer = typeof(PreparedPass1JobExecutor)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .SingleOrDefault(candidate =>
                candidate.Name == "NormalizeAndValidateDeepSeekRequestBody" &&
                candidate.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == typeof(byte[]))
            ?? throw new Xunit.Sdk.XunitException(
                "T113 RED: the executor has no single-body DeepSeek pre-send normalizer seam.");

        return normalizer.Invoke(
            obj: null,
            parameters: [Encoding.UTF8.GetBytes(body)]) as byte[];
    }

    private static PreparedPass1JobExecutor CreateExecutor(
        HttpMessageHandler transport,
        TimeProvider timeProvider,
        TimeSpan inactivityTimeout,
        TimeSpan absoluteTimeout,
        IPass1NavigationService? navigationService = null,
        IPreparedPass1SubmissionDiagnosticObserver? observer = null,
        IPreparedPass1ExecutionDiagnosticObserver? executionObserver = null,
        IPass1NavigationAttemptReservationStore? reservationStore = null)
    {
        reservationStore ??= new RecordingReservationStore();
        ConstructorInfo? constructor = typeof(PreparedPass1JobExecutor)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(candidate =>
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Count(parameter => parameter.ParameterType == typeof(TimeSpan)) == 2 &&
                       parameters.Any(parameter => parameter.ParameterType == typeof(TimeProvider));
            });
        if (constructor is null)
        {
            throw new Xunit.Sdk.XunitException(
                "T062 RED: PreparedPass1JobExecutor lacks the internal deterministic " +
                "timeout seam (TimeProvider plus inactivity and absolute TimeSpan values).");
        }

        ParameterInfo[] parameters = constructor.GetParameters();
        int timeoutOrdinal = 0;
        object?[] arguments = parameters.Select(parameter =>
        {
            if (parameter.ParameterType == typeof(WorkerAgentProviderResolver))
            {
                return (object?)CreateResolver();
            }

            if (parameter.ParameterType == typeof(ISecretEncryptionService))
            {
                return new PlaintextEncryptionService();
            }

            if (parameter.ParameterType == typeof(PreparedNavigateCodeFunction))
            {
                return CreateNavigateCodeFunction(
                    navigationService ?? new NoOpNavigationService(),
                    reservationStore);
            }

            if (parameter.ParameterType == typeof(IPass1NavigationAttemptReservationStore))
            {
                return reservationStore;
            }

            if (parameter.ParameterType == typeof(HttpMessageHandler))
            {
                return transport;
            }

            if (parameter.ParameterType == typeof(IPreparedPass1SubmissionDiagnosticObserver))
            {
                return observer;
            }

            if (parameter.ParameterType == typeof(IPreparedPass1WireBudgetDiagnosticObserver))
            {
                return null;
            }

            if (parameter.ParameterType == typeof(IPreparedPass1ExecutionDiagnosticObserver))
            {
                return executionObserver;
            }

            if (parameter.ParameterType == typeof(TimeProvider))
            {
                return timeProvider;
            }

            if (parameter.ParameterType == typeof(TimeSpan))
            {
                return timeoutOrdinal++ == 0 ? inactivityTimeout : absoluteTimeout;
            }

            throw new Xunit.Sdk.XunitException(
                $"T062 RED: unsupported timeout-seam parameter {parameter.ParameterType.FullName}.");
        }).ToArray();

        return (PreparedPass1JobExecutor)constructor.Invoke(arguments);
    }

    private static PreparedNavigateCodeFunction CreateNavigateCodeFunction(
        IPass1NavigationService navigationService,
        IPass1NavigationAttemptReservationStore reservationStore)
    {
        ConstructorInfo constructor = typeof(PreparedNavigateCodeFunction)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();
        object?[] arguments = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(IPass1NavigationService)
                ? (object)navigationService
                : parameter.ParameterType == typeof(IPass1NavigationAttemptReservationStore)
                    ? reservationStore
                    : throw new Xunit.Sdk.XunitException(
                        $"T107 RED: unsupported PreparedNavigateCodeFunction parameter {parameter.ParameterType.FullName}."))
            .ToArray();
        return (PreparedNavigateCodeFunction)constructor.Invoke(arguments);
    }

    private sealed class RecordingSubmissionDiagnosticObserver
        : IPreparedPass1SubmissionDiagnosticObserver
    {
        public List<PreparedPass1SubmissionRejectionKind> Categories { get; } = [];

        public void Observe(PreparedPass1SubmissionRejectionKind category) =>
            Categories.Add(category);
    }

    private static WorkerAgentProviderResolver CreateResolver()
    {
        LlmProvider provider = new(
            "deepseek-v4-flash",
            "https://deepseek.test/v1",
            "ciphertext",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        return new(new NoOverrideRepository(), new SingleProviderRepository(provider));
    }

    private static TimeSpan ReadTimeout(Type executorType, IReadOnlyList<string> names)
    {
        foreach (string name in names)
        {
            FieldInfo? field = executorType.GetField(
                name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.FieldType == typeof(TimeSpan) && field.GetValue(null) is TimeSpan fieldValue)
            {
                return fieldValue;
            }

            PropertyInfo? property = executorType.GetProperty(
                name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.PropertyType == typeof(TimeSpan) && property.GetValue(null) is TimeSpan propertyValue)
            {
                return propertyValue;
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"T062 RED: timeout configuration seam is missing ({string.Join(", ", names)}).");
    }

    private static PreparedPass1WorkerPacket CreatePacket()
    {
        byte[] source = Encoding.UTF8.GetBytes("class Example { }\n");
        string hash = new('a', 64);
        PreparedPass1ContextSnapshotProjection context = new(
            Guid.NewGuid(),
            _repositoryId,
            _scanRunId,
            new('b', 40),
            hash,
            hash,
            null,
            null,
            null);
        PreparedPass1ServerAuthorization authorization = new(
            _pairId,
            1,
            1,
            Guid.Parse("99999999-aaaa-4bbb-8ccc-dddddddddddd"),
            DateTimeOffset.UtcNow.AddMinutes(5),
            nameof(Pass1PairExecutionPhase.Running),
            Pass1CircuitState.Closed,
            ScanJobExecutionState.Running,
            PreparedPass1AuthorizationBinding.Create(new(
                Guid.NewGuid(),
                _systemId,
                [_repositoryId])));

        return new(
            _pairId,
            _jobId,
            _scanExecutionId,
            _systemId,
            _repositoryId,
            _scanRunId,
            new('b', 40),
            _fileCodeUnitId,
            hash,
            _manifestId,
            new('c', 64),
            new('d', 64),
            "src/Example.java",
            0,
            source.Length,
            1,
            1,
            1,
            1,
            hash,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(source)),
            "story-12-3-worker-v2",
            "code-logic/file-v2",
            new('e', 64),
            1,
            1,
            source,
            context,
            [])
        {
            ServerAuthorization = authorization,
        };
    }

    private static PreparedPass1WorkerPacket CreatePacketWithBudget(int remainingNavigationBudget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(remainingNavigationBudget, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(remainingNavigationBudget, 8);
        PreparedPass1WorkerPacket packet = CreatePacket();
        return packet with
        {
            RemainingNavigationBudget = remainingNavigationBudget,
            NavigationBudget = new(
                8 - remainingNavigationBudget,
                0,
                0,
                0),
        };
    }

    private static string CreateSubmitSse(string document, bool includeDone)
    {
        string arguments = JsonSerializer.Serialize($"{{\"document\":{document}}}");
        string sse =
            $"data: {{\"id\":\"raw-submit\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-raw-submit\",\"type\":\"function\",\"function\":{{\"name\":\"submit_code_logic\",\"arguments\":{arguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n";
        return includeDone ? sse + "data: [DONE]\n\n" : sse;
    }

    private static string CreateToolCallsSse(
        params (string Id, string Name, string Arguments)[] calls)
    {
        string serializedCalls = string.Join(
            ",",
            calls.Select((call, index) =>
                $"{{\"index\":{index},\"id\":\"{call.Id}\",\"type\":\"function\",\"function\":{{\"name\":\"{call.Name}\",\"arguments\":{JsonSerializer.Serialize(call.Arguments)}}}}}"));
        return
            $"data: {{\"id\":\"t125-stream\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{serializedCalls}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\ndata: [DONE]\n\n";
    }

    private sealed class RawReadSseHandler(
        IReadOnlyList<byte[]> chunks,
        IReadOnlyList<TimeSpan>? delaysBeforeChunk = null,
        bool throwAfterChunks = false) : HttpMessageHandler
    {
        private readonly IReadOnlyList<byte[]> _chunks = chunks;
        private readonly IReadOnlyList<TimeSpan> _delaysBeforeChunk =
            delaysBeforeChunk ?? [];
        private readonly bool _throwAfterChunks = throwAfterChunks;

        public int RawBytesRead { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            StreamContent content = new(new RawReadStream(
                _chunks,
                _delaysBeforeChunk,
                _throwAfterChunks,
                bytesRead => RawBytesRead += bytesRead));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class RawReadStream(
        IReadOnlyList<byte[]> chunks,
        IReadOnlyList<TimeSpan> delaysBeforeChunk,
        bool throwAfterChunks,
        Action<int> onRead) : Stream
    {
        private int _index;
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => chunks.Sum(chunk => chunk.LongLength);
        public override long Position
        {
            get => _index;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None)
                .AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_index >= chunks.Count)
            {
                if (throwAfterChunks)
                {
                    throw new InvalidOperationException("raw stream failure");
                }

                return 0;
            }

            TimeSpan delayBeforeChunk = _index < delaysBeforeChunk.Count
                ? delaysBeforeChunk[_index]
                : TimeSpan.Zero;
            if (_offset == 0 && delayBeforeChunk > TimeSpan.Zero)
            {
                await Task.Delay(delayBeforeChunk, cancellationToken);
            }

            byte[] chunk = chunks[_index];
            int count = Math.Min(buffer.Length, chunk.Length - _offset);
            chunk.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            if (_offset == chunk.Length)
            {
                _index++;
                _offset = 0;
            }

            onRead(count);
            return count;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class StreamingHandler(string? finalJson, IReadOnlyList<TimeSpan>? delays = null) : HttpMessageHandler
    {
        public bool StreamRequested { get; private set; }
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            byte[] requestBytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            RequestBodies.Add(Encoding.UTF8.GetString(requestBytes));
            using JsonDocument body = JsonDocument.Parse(requestBytes);
            StreamRequested = body.RootElement.GetProperty("stream").GetBoolean();
            List<string> events = [];
            List<TimeSpan> pauses = [];
            if (finalJson is null)
            {
                // One delay means a silent stream; multiple delays mean
                // continuing reasoning updates with no early [DONE].
                int reasoningCount = delays?.Count > 1 ? delays.Count : 0;
                for (int index = 0; index < reasoningCount; index++)
                {
                    events.Add(ReasoningEvent(index));
                    pauses.Add(delays![index]);
                }
            }
            else
            {
                int reasoningCount = delays is { Count: > 1 } ? delays.Count - 1 : 1;
                for (int index = 0; index < reasoningCount; index++)
                {
                    events.Add(ReasoningEvent(index));
                    pauses.Add(delays is not null ? delays[index] : TimeSpan.Zero);
                }

                events.Add(
                    $"data: {{\"id\":\"stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"content\":{JsonSerializer.Serialize(finalJson)}}},\"finish_reason\":\"stop\"}}]}}\n\n");
                pauses.Add(delays is not null ? delays[^1] : TimeSpan.Zero);
                events.Add("data: [DONE]\n\n");
                pauses.Add(TimeSpan.Zero);
            }

            TimeSpan holdOpenDelay = finalJson is null && delays is { Count: 1 }
                ? delays[0]
                : TimeSpan.Zero;
            IncrementalSseStream stream = new(events, pauses, holdOpenDelay, finalJson is null);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content,
            };
        }

        private static string ReasoningEvent(int index) =>
            $"data: {{\"id\":\"stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"reasoning_content\":\"bounded reasoning {index}\"}},\"finish_reason\":null}}]}}\n\n";
    }

    private sealed class IncrementalSseStream(
        IReadOnlyList<string> events,
        IReadOnlyList<TimeSpan> delays,
        TimeSpan holdOpenDelay,
        bool holdOpen) : Stream
    {
        private readonly byte[][] _chunks = events.Select(Encoding.UTF8.GetBytes).ToArray();
        private int _index;
        private int _chunkOffset;
        private bool _holdOpenDelayConsumed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_index < _chunks.Length)
            {
                if (_chunkOffset == 0 && _index < delays.Count && delays[_index] > TimeSpan.Zero)
                {
                    await Task.Delay(delays[_index], cancellationToken);
                }

                if (buffer.Length == 0)
                {
                    return 0;
                }

                byte[] chunk = _chunks[_index];
                int copyLength = Math.Min(buffer.Length, chunk.Length - _chunkOffset);
                chunk.AsMemory(_chunkOffset, copyLength).CopyTo(buffer);
                _chunkOffset += copyLength;
                if (_chunkOffset == chunk.Length)
                {
                    _index++;
                    _chunkOffset = 0;
                }

                return copyLength;
            }

            if (holdOpen)
            {
                if (!_holdOpenDelayConsumed)
                {
                    _holdOpenDelayConsumed = true;
                    if (holdOpenDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(holdOpenDelay, cancellationToken);
                    }
                }

                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return 0;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ReasoningSubmitHandler(
        string document,
        IReadOnlyList<TimeSpan>? delays = null) : HttpMessageHandler
    {
        public bool StreamRequested { get; private set; }
        public int ReasoningEvents { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using JsonDocument body = JsonDocument.Parse(
                await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            StreamRequested = body.RootElement.GetProperty("stream").GetBoolean();

            int reasoningCount = delays is { Count: > 1 } ? delays.Count - 1 : 1;
            ReasoningEvents = reasoningCount;
            List<string> events = [];
            List<TimeSpan> pauses = [];
            for (int index = 0; index < reasoningCount; index++)
            {
                events.Add(ReasoningEvent(index));
                pauses.Add(delays is { Count: > 1 } ? delays[index] : TimeSpan.Zero);
            }

            string argumentJson = $"{{\"document\":{document}}}";
            string serializedArguments = JsonSerializer.Serialize(argumentJson);
            events.Add(
                $"data: {{\"id\":\"submit-stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-submit-1\",\"type\":\"function\",\"function\":{{\"name\":\"submit_code_logic\",\"arguments\":{serializedArguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n");
            pauses.Add(delays is { Count: > 0 } ? delays[^1] : TimeSpan.Zero);
            events.Add("data: [DONE]\n\n");
            pauses.Add(TimeSpan.Zero);

            IncrementalSseStream stream = new(events, pauses, TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }

        private static string ReasoningEvent(int index) =>
            $"data: {{\"id\":\"stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"reasoning_content\":\"bounded reasoning {index}\"}},\"finish_reason\":null}}]}}\n\n";
    }

    private sealed class ToolLoopHandler(string finalDocument) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);
            int callOrdinal = RequestBodies.Count;
            string[] events;
            if (callOrdinal == 1)
            {
                events =
                [
                    "data: {\"id\":\"tool-stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"tool_calls\":[{\"index\":0,\"id\":\"call-navigate-1\",\"type\":\"function\",\"function\":{\"name\":\"navigate_code\",\"arguments\":\"{\\\"operation\\\":\\\"Definition\\\",\\\"target\\\":{\\\"repositoryRelativePath\\\":\\\"src/Target.java\\\",\\\"symbol\\\":\\\"Target\\\",\\\"position\\\":null,\\\"exactLineRange\\\":null,\\\"expectedSourceHash\\\":\\\"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\\\"},\\\"depth\\\":1,\\\"parentObservationIdentity\\\":null}\"}}]},\"finish_reason\":\"tool_calls\"}]}\n\n",
                    "data: [DONE]\n\n",
                ];
            }
            else
            {
                string argumentJson = $"{{\"document\":{finalDocument}}}";
                string serializedArguments = JsonSerializer.Serialize(argumentJson);
                events =
                [
                    $"data: {{\"id\":\"submit-stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-submit-1\",\"type\":\"function\",\"function\":{{\"name\":\"submit_code_logic\",\"arguments\":{serializedArguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n",
                    "data: [DONE]\n\n",
                ];
            }
            IncrementalSseStream stream = new(
                events,
                [TimeSpan.Zero, TimeSpan.Zero],
                TimeSpan.Zero,
                holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class ReasoningToolLoopHandler(string finalDocument) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);
            string[] events;
            if (RequestBodies.Count == 1)
            {
                events =
                [
                    "data: {\"id\":\"tool-stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"reasoning_content\":\"bounded reasoning\"},\"finish_reason\":null}]}\n\n",
                    "data: {\"id\":\"tool-stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Inspecting the bounded definition.\"},\"finish_reason\":null}]}\n\n",
                    "data: {\"id\":\"tool-stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call-navigate-1\",\"type\":\"function\",\"function\":{\"name\":\"navigate_code\",\"arguments\":\"{\\\"operation\\\":\\\"Definition\\\",\\\"target\\\":{\\\"repositoryRelativePath\\\":\\\"src/Target.java\\\",\\\"symbol\\\":\\\"Target\\\",\\\"position\\\":null,\\\"exactLineRange\\\":null,\\\"expectedSourceHash\\\":\\\"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\\\"},\\\"depth\\\":1,\\\"parentObservationIdentity\\\":null}\"}}]},\"finish_reason\":\"tool_calls\"}]}\n\n",
                    "data: [DONE]\n\n",
                ];
            }
            else
            {
                string argumentJson = $"{{\"document\":{finalDocument}}}";
                string serializedArguments = JsonSerializer.Serialize(argumentJson);
                events =
                [
                    $"data: {{\"id\":\"submit-stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-submit-1\",\"type\":\"function\",\"function\":{{\"name\":\"submit_code_logic\",\"arguments\":{serializedArguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n",
                    "data: [DONE]\n\n",
                ];
            }

            IncrementalSseStream stream = new(
                events,
                Enumerable.Repeat(TimeSpan.Zero, events.Length).ToArray(),
                TimeSpan.Zero,
                holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class SubmitCodeLogicHandler(
        string firstDocument,
        string secondDocument) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];
        public List<string> FunctionNames { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int requestOrdinal = RequestBodies.Count;
            string document = requestOrdinal == 1 ? firstDocument : secondDocument;
            string argumentJson = $"{{\"document\":{document}}}";
            FunctionNames.Add("submit_code_logic");

            string serializedArguments = JsonSerializer.Serialize(argumentJson);
            string events =
                $"data: {{\"id\":\"submit-stream-{requestOrdinal}\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-submit-{requestOrdinal}\",\"type\":\"function\",\"function\":{{\"name\":\"submit_code_logic\",\"arguments\":{serializedArguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n" +
                "data: [DONE]\n\n";
            IncrementalSseStream stream = new(
                [events],
                [TimeSpan.Zero],
                TimeSpan.Zero,
                holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class NavigationThenCorrectionHandler(
        string firstDocument,
        string correctedDocument,
        IReadOnlyList<ToolCallShape> navigationCalls) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int requestOrdinal = RequestBodies.Count;
            string sse = requestOrdinal == 1
                ? CreateToolCallsSse(
                    navigationCalls.Select(call =>
                        (call.Id, call.Name, call.Arguments)).ToArray())
                : CreateToolCallsSse(
                    (requestOrdinal == 2 ? "call-submit-mixed" : "call-submit-corrected",
                        "submit_code_logic",
                        $"{{\"document\":{(requestOrdinal == 2 ? firstDocument : correctedDocument)}}}"));
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class SubmitThenNavigateHandler(string document) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int ordinal = RequestBodies.Count;
            string arguments = ordinal == 1
                ? $"{{\"document\":{document}}}"
                : "{\"operation\":\"Definition\",\"target\":{\"repositoryRelativePath\":\"src/Target.java\",\"symbol\":\"Target\",\"position\":null,\"exactLineRange\":null,\"expectedSourceHash\":\"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\"},\"depth\":1,\"parentObservationIdentity\":null}";
            string functionName = ordinal == 1 ? "submit_code_logic" : "navigate_code";
            string serializedArguments = JsonSerializer.Serialize(arguments);
            string sse =
                $"data: {{\"id\":\"boundary-stream-{ordinal}\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-boundary-{ordinal}\",\"type\":\"function\",\"function\":{{\"name\":\"{functionName}\",\"arguments\":{serializedArguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n" +
                "data: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class BadRequestHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("provider failure"),
            });
    }

    private sealed class StatusCodeHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("provider failure with private-source-marker"),
            };
        }
    }

    private sealed class UnexpectedExceptionHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException();
    }

    private sealed class SingleToolCallHandler(
        string functionName,
        string arguments) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string eventArguments = JsonSerializer.Serialize(arguments);
            string sse =
                $"data: {{\"id\":\"single-call\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-unknown\",\"type\":\"function\",\"function\":{{\"name\":\"{functionName}\",\"arguments\":{eventArguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n" +
                "data: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class ParallelToolCallHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string submitArguments = JsonSerializer.Serialize("{\"document\":{}}");
            string navigateArguments = JsonSerializer.Serialize("{}");
            string sse =
                $"data: {{\"id\":\"parallel-call\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{{\"index\":0,\"id\":\"call-submit\",\"type\":\"function\",\"function\":{{\"name\":\"submit_code_logic\",\"arguments\":{submitArguments}}}}},{{\"index\":1,\"id\":\"call-navigate\",\"type\":\"function\",\"function\":{{\"name\":\"navigate_code\",\"arguments\":{navigateArguments}}}}}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\n" +
                "data: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class MultipleToolCallHandler(
        string firstFunctionName,
        string secondFunctionName) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string payload = JsonSerializer.Serialize(new
            {
                id = "overlap-call",
                @object = "chat.completion.chunk",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new
                        {
                            role = "assistant",
                            tool_calls = new[]
                            {
                                new
                                {
                                    index = 0,
                                    id = "call-first",
                                    type = "function",
                                    function = new { name = firstFunctionName, arguments = "{}" },
                                },
                                new
                                {
                                    index = 1,
                                    id = "call-second",
                                    type = "function",
                                    function = new { name = secondFunctionName, arguments = "{}" },
                                },
                            },
                        },
                        finish_reason = "tool_calls",
                    },
                },
            });
            string sse = $"data: {payload}\n\ndata: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed record ToolCallShape(string Id, string Name, string Arguments);

    private sealed class ToolCallShapeHandler(IReadOnlyList<ToolCallShape> calls) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var payload = new
            {
                id = "shape-turn",
                @object = "chat.completion.chunk",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new
                        {
                            role = "assistant",
                            tool_calls = calls.Select((call, index) => new
                            {
                                index,
                                id = call.Id,
                                type = "function",
                                function = new { name = call.Name, arguments = call.Arguments },
                            }).ToArray(),
                        },
                        finish_reason = "tool_calls",
                    },
                },
            };
            string sse = $"data: {JsonSerializer.Serialize(payload)}\n\ndata: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class SaturationReplayHandler(
        int navigationCallCount,
        string finalDocument) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string sse = RequestBodies.Count == 1 && navigationCallCount > 0
                ? CreateToolCallsSse(
                    Enumerable.Range(1, navigationCallCount)
                        .Select(index =>
                        {
                            ToolCallShape call = NavigateCall(
                                $"call-navigate-{index}",
                                $"saturation-{index}");
                            return (call.Id, call.Name, call.Arguments);
                        })
                        .ToArray())
                : CreateToolCallsSse(
                    ("call-submit-final", "submit_code_logic", $"{{\"document\":{finalDocument}}}"));
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class TurnTenScriptedHandler(
        string scenario,
        int closedNavigationTurns,
        string finalDocument) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int turn = RequestBodies.Count;
            string sse;
            if (turn <= closedNavigationTurns)
            {
                ToolCallShape call = NavigateCall($"call-closed-{turn}", $"closed-{turn}");
                sse = CreateToolCallsSse((call.Id, call.Name, call.Arguments));
            }
            else if (scenario == "second-valid" && turn == 9)
            {
                sse = CreateToolCallsSse(
                    ("call-submit-first", "submit_code_logic", "{\"document\":{}}"));
            }
            else if (scenario == "first-invalid")
            {
                sse = CreateToolCallsSse(
                    ("call-submit-invalid", "submit_code_logic", "{\"document\":{}}"));
            }
            else
            {
                sse = CreateToolCallsSse(
                    ("call-submit-final", "submit_code_logic", $"{{\"document\":{finalDocument}}}"));
            }

            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class FirstRejectedThenCorrectedHandler(
        string finalDocument,
        int navigationTurns) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int turn = RequestBodies.Count;
            ToolCallShape navigationCall = NavigateCall($"call-closed-{turn}", $"closed-{turn}");
            string sse = turn <= navigationTurns
                ? CreateToolCallsSse(
                    (navigationCall.Id, navigationCall.Name, navigationCall.Arguments))
                : turn == navigationTurns + 1
                    ? CreateToolCallsSse(
                        ("call-submit-invalid", "submit_code_logic", "{\"document\":{}}"))
                    : CreateToolCallsSse(
                        ("call-submit-final", "submit_code_logic", $"{{\"document\":{finalDocument}}}"));
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class Ad30TurnAllocationHandler(
        string turnNineDocument,
        string turnTenDocument,
        bool includeCorrection) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int turn = RequestBodies.Count;
            string sse;
            if (turn <= 8)
            {
                ToolCallShape navigationCall = NavigateCall(
                    $"call-ad30-navigation-{turn}",
                    $"ad30-navigation-{turn}");
                sse = CreateToolCallsSse(
                    (navigationCall.Id, navigationCall.Name, navigationCall.Arguments));
            }
            else if (turn == 9)
            {
                sse = CreateToolCallsSse(
                    ("call-ad30-first-submit", "submit_code_logic", $"{{\"document\":{turnNineDocument}}}"));
            }
            else if (turn == 10 && includeCorrection)
            {
                sse = CreateToolCallsSse(
                    ("call-ad30-correction", "submit_code_logic", $"{{\"document\":{turnTenDocument}}}"));
            }
            else
            {
                sse = CreateToolCallsSse(
                    ("request-eleven", "submit_code_logic", "{\"document\":{}}"));
            }

            IncrementalSseStream stream = new(
                [sse],
                [TimeSpan.Zero],
                TimeSpan.Zero,
                holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class TurnTenNavigationHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int turn = RequestBodies.Count;
            ToolCallShape call = NavigateCall($"call-turn-{turn}", $"turn-{turn}");
            var payload = new
            {
                id = $"request-{turn}",
                @object = "chat.completion.chunk",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new
                        {
                            role = "assistant",
                            tool_calls = new[]
                            {
                                new
                                {
                                    index = 0,
                                    id = call.Id,
                                    type = "function",
                                    function = new { name = call.Name, arguments = call.Arguments },
                                },
                            },
                        },
                        finish_reason = "tool_calls",
                    },
                },
            };
            string sse = $"data: {JsonSerializer.Serialize(payload)}\n\ndata: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class TurnTenDoubleSubmitHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int turn = RequestBodies.Count;
            ToolCallShape[] calls = turn <= 8
                ? [NavigateCall($"call-turn-{turn}", $"turn-{turn}")]
                :
                [
                    new ToolCallShape("call-submit-one", "submit_code_logic", "{\"document\":{}}"),
                    new ToolCallShape("call-submit-two", "submit_code_logic", "{\"document\":{}}"),
                ];
            var payload = new
            {
                id = $"request-{turn}",
                @object = "chat.completion.chunk",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new
                        {
                            role = "assistant",
                            tool_calls = calls.Select((call, index) => new
                            {
                                index,
                                id = call.Id,
                                type = "function",
                                function = new { name = call.Name, arguments = call.Arguments },
                            }).ToArray(),
                        },
                        finish_reason = "tool_calls",
                    },
                },
            };
            string sse = $"data: {JsonSerializer.Serialize(payload)}\n\ndata: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class TurnTenMultipleNavigationHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int turn = RequestBodies.Count;
            ToolCallShape[] calls = turn <= 8
                ? [NavigateCall($"call-turn-{turn}", $"turn-{turn}")]
                :
                [
                    NavigateCall("call-turn-ten-one", "turn-ten-one"),
                    NavigateCall("call-turn-ten-two", "turn-ten-two"),
                ];
            var payload = new
            {
                id = $"request-{turn}",
                @object = "chat.completion.chunk",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new
                        {
                            role = "assistant",
                            tool_calls = calls.Select((call, index) => new
                            {
                                index,
                                id = call.Id,
                                type = "function",
                                function = new { name = call.Name, arguments = call.Arguments },
                            }).ToArray(),
                        },
                        finish_reason = "tool_calls",
                    },
                },
            };
            string sse = $"data: {JsonSerializer.Serialize(payload)}\n\ndata: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class TurnTenIllegalShapeHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int turn = RequestBodies.Count;
            ToolCallShape call = turn == 9
                ? new("request-nine-illegal", "unknown_function", "{}")
                : NavigateCall($"call-turn-{turn}", $"turn-{turn}");
            var payload = new
            {
                id = turn == 9 ? "request-nine-illegal" : $"request-{turn}",
                @object = "chat.completion.chunk",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new
                        {
                            role = "assistant",
                            tool_calls = new[]
                            {
                                new
                                {
                                    index = 0,
                                    id = call.Id,
                                    type = "function",
                                    function = new { name = call.Name, arguments = call.Arguments },
                                },
                            },
                        },
                        finish_reason = "tool_calls",
                    },
                },
            };
            string sse = $"data: {JsonSerializer.Serialize(payload)}\n\ndata: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class TurnTenMixedShapeHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            int turn = RequestBodies.Count;
            ToolCallShape[] calls = turn switch
            {
                <= 8 => [NavigateCall($"call-turn-{turn}", $"turn-{turn}")],
                _ =>
                [
                    NavigateCall("call-turn-ten", "turn-ten"),
                    new ToolCallShape("call-submit-ten", "submit_code_logic", "{\"document\":{}}"),
                ],
            };
            var payload = new
            {
                id = $"request-{turn}",
                @object = "chat.completion.chunk",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new
                        {
                            role = "assistant",
                            tool_calls = calls.Select((call, index) => new
                            {
                                index,
                                id = call.Id,
                                type = "function",
                                function = new { name = call.Name, arguments = call.Arguments },
                            }).ToArray(),
                        },
                        finish_reason = "tool_calls",
                    },
                },
            };
            string sse = $"data: {JsonSerializer.Serialize(payload)}\n\ndata: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class LegalNavigationThenUnknownSiblingHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            const string navigateArguments =
                "{\"operation\":\"Definition\",\"target\":{\"repositoryRelativePath\":\"src/Target.java\",\"symbol\":\"Target\",\"position\":null,\"exactLineRange\":null,\"expectedSourceHash\":\"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\"},\"depth\":1,\"parentObservationIdentity\":null}";
            string navigate = JsonSerializer.Serialize(navigateArguments);
            string unknown = JsonSerializer.Serialize("{}");
            string sse =
                "data: {\"id\":\"whole-turn\",\"object\":\"chat.completion.chunk\",\"choices\":[" +
                "{\"index\":0,\"delta\":{\"role\":\"assistant\",\"tool_calls\":[" +
                "{\"index\":0,\"id\":\"call-navigate\",\"type\":\"function\",\"function\":{\"name\":\"navigate_code\",\"arguments\":" + navigate + "}}]},\"finish_reason\":\"tool_calls\"}]}\n\n" +
                "data: {\"id\":\"whole-turn\",\"object\":\"chat.completion.chunk\",\"choices\":[" +
                "{\"index\":0,\"delta\":{\"role\":\"assistant\",\"tool_calls\":[" +
                "{\"index\":1,\"id\":\"call-unknown\",\"type\":\"function\",\"function\":{\"name\":\"unknown_function\",\"arguments\":" + unknown + "}}]},\"finish_reason\":\"tool_calls\"}]}\n\n" +
                "data: [DONE]\n\n";
            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class LegalAllNavigationBatchHandler(string finalDocument) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string sse;
            if (RequestBodies.Count == 1)
            {
                const string firstArguments =
                    "{\"operation\":\"Definition\",\"target\":{\"repositoryRelativePath\":\"src/Target.java\",\"symbol\":\"call-navigate-1\",\"position\":null,\"exactLineRange\":null,\"expectedSourceHash\":\"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\"},\"depth\":1,\"parentObservationIdentity\":null}";
                const string secondArguments =
                    "{\"operation\":\"Definition\",\"target\":{\"repositoryRelativePath\":\"src/Target.java\",\"symbol\":\"call-navigate-2\",\"position\":null,\"exactLineRange\":null,\"expectedSourceHash\":\"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff\"},\"depth\":1,\"parentObservationIdentity\":null}";
                sse = CreateToolCallsSse(
                    ("call-navigate-1", "navigate_code", firstArguments),
                    ("call-navigate-2", "navigate_code", secondArguments));
            }
            else
            {
                sse = CreateToolCallsSse(
                    ("call-submit-1", "submit_code_logic", $"{{\"document\":{finalDocument}}}"));
            }

            IncrementalSseStream stream = new([sse], [TimeSpan.Zero], TimeSpan.Zero, holdOpen: false);
            StreamContent content = new(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }

        private static string CreateToolCallsSse(
            params (string Id, string Name, string Arguments)[] calls)
        {
            string serializedCalls = string.Join(
                ",",
                calls.Select((call, index) =>
                    $"{{\"index\":{index},\"id\":\"{call.Id}\",\"type\":\"function\",\"function\":{{\"name\":\"{call.Name}\",\"arguments\":{JsonSerializer.Serialize(call.Arguments)}}}}}"));
            return
                $"data: {{\"id\":\"navigate-stream-1\",\"object\":\"chat.completion.chunk\",\"choices\":[{{\"index\":0,\"delta\":{{\"role\":\"assistant\",\"tool_calls\":[{serializedCalls}]}},\"finish_reason\":\"tool_calls\"}}]}}\n\ndata: [DONE]\n\n";
        }
    }

    private sealed class RecordingNavigationService : IPass1NavigationService
    {
        public List<PreparedPass1NavigationRequestInput> Requests { get; } = [];

        public Task<PreparedPass1NavigationResponse> NavigateAsync(
            PreparedPass1NavigationRequestInput request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new PreparedPass1NavigationResponse(
                PreparedPass1NavigationOutcome.DocumentEvidence,
                Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
                request.Operation,
                request.RequestedDepth,
                PreparedPass1NavigationBoundKind.Calls,
                0,
                0,
                []));
        }
    }

    private sealed class MultipleReceiptNavigationService(
        IReadOnlyList<Guid> receiptIds) : IPass1NavigationService
    {
        private readonly IReadOnlyList<Guid> _receiptIds = receiptIds;

        public List<PreparedPass1NavigationRequestInput> Requests { get; } = [];

        public Task<PreparedPass1NavigationResponse> NavigateAsync(
            PreparedPass1NavigationRequestInput request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Guid receiptId = _receiptIds[Requests.Count - 1];
            return Task.FromResult(new PreparedPass1NavigationResponse(
                PreparedPass1NavigationOutcome.DocumentEvidence,
                receiptId,
                request.Operation,
                request.RequestedDepth,
                PreparedPass1NavigationBoundKind.Calls,
                0,
                0,
                []));
        }
    }

    private sealed class ReservationAwareNavigationService(
        RecordingReservationStore reservations) : IPass1NavigationService
    {
        public List<PreparedPass1NavigationRequestInput> Requests { get; } = [];

        public Task<PreparedPass1NavigationResponse> NavigateAsync(
            PreparedPass1NavigationRequestInput request,
            CancellationToken cancellationToken)
        {
            reservations.Events.Add("callback");
            Requests.Add(request);
            int callbackOrdinal = Requests.Count;
            PreparedPass1NavigationResponse response = callbackOrdinal == 1
                ? new(
                    PreparedPass1NavigationOutcome.DocumentEvidence,
                    Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
                    request.Operation,
                    request.RequestedDepth,
                    PreparedPass1NavigationBoundKind.Results,
                    1,
                    12,
                    [])
                : new(
                    PreparedPass1NavigationOutcome.Unavailable,
                    Guid.Empty,
                    request.Operation,
                    request.RequestedDepth,
                    null,
                    0,
                    0,
                    []);
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingReservationStore : IPass1NavigationAttemptReservationStore
    {
        public List<PreparedPass1NavigationReservationRequest> Requests { get; } = [];
        public List<PreparedPass1NavigationReservationTicket> Tickets { get; } = [];
        public List<string> Events { get; } = [];
        public int? FailureOrdinal { get; init; }

        public Task<PreparedPass1NavigationReservationTicket?> ReserveNextAsync(
            PreparedPass1NavigationReservationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Events.Add("reserve");
            if (FailureOrdinal == Requests.Count)
            {
                return Task.FromResult<PreparedPass1NavigationReservationTicket?>(null);
            }

            int ordinal = checked((request.Cursor?.ToolAttemptCount ?? request.Scope.ToolAttemptCount) + 1);
            long stateVersion = (request.Cursor?.StateVersion ?? 0) + 1;
            PreparedPass1NavigationReservationTicket ticket = new(
                request.Request.JobId,
                request.Request.PairExecutionId,
                request.Request.LeaseToken,
                ordinal,
                ordinal,
                stateVersion);
            Tickets.Add(ticket);
            return Task.FromResult<PreparedPass1NavigationReservationTicket?>(ticket);
        }
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
    }

    private sealed class NoOverrideRepository : ISystemLlmProviderOverrideRepository
    {
        public Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(Guid systemId, AgentType agentType, CancellationToken cancellationToken = default) => Task.FromResult<SystemLlmProviderOverride?>(null);
        public Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(Guid systemId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SystemLlmProviderOverride>>([]);
        public Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(Guid systemId, Guid overrideId, CancellationToken cancellationToken = default) => Task.FromResult<SystemLlmProviderOverride?>(null);
        public Task<SystemLlmProviderOverride> AddAsync(SystemLlmProviderOverride overrideEntry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RemoveAsync(Guid systemId, Guid overrideId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SingleProviderRepository(LlmProvider provider) : ILlmProviderRepository
    {
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LlmProvider>>([provider]);
        public Task<LlmProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<LlmProvider?>(id == provider.Id ? provider : null);
        public Task<bool> NameExistsAsync(string name, Guid? excludeProviderId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LlmProvider> AddAsync(LlmProvider value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Remove(LlmProvider value) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PlaintextEncryptionService : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(string purpose, string plain, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> DecryptAsync(string purpose, string cipher, CancellationToken cancellationToken = default) => Task.FromResult("test-api-key");
    }

    private sealed class NoOpNavigationService : IPass1NavigationService
    {
        public Task<PreparedPass1NavigationResponse> NavigateAsync(PreparedPass1NavigationRequestInput request, CancellationToken cancellationToken) =>
            Task.FromResult(new PreparedPass1NavigationResponse(PreparedPass1NavigationOutcome.Unavailable, Guid.Empty, request.Operation, request.RequestedDepth, null, 0, 0, []));
    }
}

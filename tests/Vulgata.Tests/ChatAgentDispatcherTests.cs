using System.Runtime.CompilerServices;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared;

namespace Vulgata.Tests;

public sealed class ChatAgentDispatcherTests
{
    private const string _businessPromptResourceName =
        "Vulgata.Agents.Prompts.chat-business.v1.md";
    private const string _itPromptResourceName =
        "Vulgata.Agents.Prompts.chat-it.v1.md";

    [Fact]
    public async Task ResolveAsync_ExactSystemChatOverride_TakesPrecedence()
    {
        Guid systemId = Guid.NewGuid();
        var globalProvider = new LlmProvider(
            "global-chat-model",
            "https://global-chat.example.com/v1",
            "encrypted-global",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var overrideProvider = new LlmProvider(
            "system-chat-model",
            "https://system-chat.example.com/v1",
            "encrypted-system",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        SystemLlmProviderOverride overrideEntry = SystemLlmProviderOverride.Create(
            systemId,
            overrideProvider.Id,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var resolver = new ChatAgentProviderResolver(
            new FakeOverrideRepository(overrideEntry),
            new FakeProviderRepository([globalProvider, overrideProvider]));

        ResolvedChatAgentProvider result = await resolver.ResolveAsync(systemId);

        Assert.Equal(overrideProvider.Id, result.ProviderId);
        Assert.Equal("system-chat-model", result.Model);
        Assert.True(result.IsSystemOverride);
    }

    [Fact]
    public async Task ResolveAsync_WithoutOverride_UsesFirstOrderedGlobalChatAndNeverWorkerFallback()
    {
        var workerProvider = new LlmProvider(
            "worker-model",
            "https://worker.example.com/v1",
            "encrypted-worker",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var firstChatProvider = new LlmProvider(
            "first-chat-model",
            "https://first-chat.example.com/v1",
            "encrypted-first",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var secondChatProvider = new LlmProvider(
            "second-chat-model",
            "https://second-chat.example.com/v1",
            "encrypted-second",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var resolver = new ChatAgentProviderResolver(
            new FakeOverrideRepository(null),
            new FakeProviderRepository([workerProvider, firstChatProvider, secondChatProvider]));

        ResolvedChatAgentProvider result = await resolver.ResolveAsync(Guid.NewGuid());

        Assert.Equal(firstChatProvider.Id, result.ProviderId);
        Assert.False(result.IsSystemOverride);

        var workerOnlyResolver = new ChatAgentProviderResolver(
            new FakeOverrideRepository(null),
            new FakeProviderRepository([workerProvider]));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workerOnlyResolver.ResolveAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ResolveAsync_InvalidChatProviderConfiguration_FailsClosed()
    {
        LlmProvider[] invalidProviders =
        [
            new(
                "responses-only",
                "https://responses.example.com/v1",
                "encrypted-key",
                ApiType.Responses,
                AgentType.Chat,
                DateTimeOffset.UtcNow),
            new(
                "invalid-endpoint",
                "file:///host/provider",
                "encrypted-key",
                ApiType.ChatCompletions,
                AgentType.Chat,
                DateTimeOffset.UtcNow),
            new(
                " ",
                "https://model.example.com/v1",
                "encrypted-key",
                ApiType.ChatCompletions,
                AgentType.Chat,
                DateTimeOffset.UtcNow),
            new(
                "missing-key",
                "https://key.example.com/v1",
                " ",
                ApiType.ChatCompletions,
                AgentType.Chat,
                DateTimeOffset.UtcNow),
        ];

        foreach (LlmProvider provider in invalidProviders)
        {
            var resolver = new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider]));

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                resolver.ResolveAsync(Guid.NewGuid()));
        }
    }

    [Theory]
    [InlineData(ChatMode.Business, _businessPromptResourceName, "Business")]
    [InlineData(ChatMode.It, _itPromptResourceName, "IT")]
    public void GetPrompt_Mode_MapsToOneVersionedEmbeddedResource(
        ChatMode mode,
        string resourceName,
        string modeMarker)
    {
        var catalog = new ChatPromptCatalog();
        string[] matches = typeof(ChatPromptCatalog).Assembly.GetManifestResourceNames()
            .Where(name => StringComparer.Ordinal.Equals(name, resourceName))
            .ToArray();

        string prompt = catalog.GetPrompt(mode);

        Assert.Single(matches);
        Assert.Contains(modeMarker, prompt, StringComparison.Ordinal);
        Assert.Contains("prepared-chat-answer-v1", prompt, StringComparison.Ordinal);
        Assert.Contains("candidateHandle", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("citation", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPrompt_MissingDuplicateMalformedOrWrongModeResource_FailsClosed()
    {
        ChatPromptResource business = new(
            _businessPromptResourceName,
            () => new MemoryStream("Business prepared-chat-answer-v1 candidateHandle"u8.ToArray()));
        ChatPromptResource it = new(
            _itPromptResourceName,
            () => new MemoryStream("IT prepared-chat-answer-v1 candidateHandle"u8.ToArray()));

        _ = Assert.Throws<InvalidOperationException>(() =>
            ChatPromptCatalog.GetPrompt(ChatMode.Business, []));
        _ = Assert.Throws<InvalidOperationException>(() =>
            ChatPromptCatalog.GetPrompt(ChatMode.Business, [business, business]));
        _ = Assert.Throws<InvalidOperationException>(() =>
            ChatPromptCatalog.GetPrompt(
                ChatMode.Business,
                [new(_businessPromptResourceName, () => new MemoryStream())]));
        _ = Assert.Throws<InvalidOperationException>(() =>
            ChatPromptCatalog.GetPrompt(ChatMode.It, [business]));
        _ = Assert.Throws<InvalidOperationException>(() =>
            ChatPromptCatalog.GetPrompt((ChatMode)999, [business, it]));
    }

    [Fact]
    public async Task GenerateAsync_ValidBusinessRequest_SendsOneSelectionOnlyCall()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        string rawSelectionJson = CreateRawSelectionJson(request);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var encryption = new CapturingEncryptionService("plaintext-chat-key");
        var capture = new OpenAiCaptureHandler(
            () => CreateSseResponse(rawSelectionJson),
            "text/event-stream");
        var services = new ServiceCollection();
        services.AddSingleton<ILlmProviderRepository>(new FakeProviderRepository([provider]));
        services.AddSingleton<ISecretEncryptionService>(encryption);
        services.AddSingleton<HttpMessageHandler>(capture);
        services.AddPreparedPass2Agents();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            encryption,
            new ChatPromptCatalog(),
            capture,
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        ChatSynthesisResult result = Assert.IsType<ChatSynthesisResult>(outcome.Result);
        Assert.Null(outcome.FailureCode);
        Assert.Equal(rawSelectionJson, outcome.RawSelectionJson);
        Assert.Equal("k1", Assert.Single(result.Selections).CandidateHandle);
        Assert.Equal(1, capture.CallCount);
        Assert.Equal("chat-provider.example.com", capture.RequestUri?.Host);
        Assert.EndsWith("/v1/chat/completions", capture.RequestUri?.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("Bearer", capture.AuthorizationScheme);
        Assert.Equal("plaintext-chat-key", capture.AuthorizationParameter);
        Assert.Equal(1, encryption.DecryptCount);
        Assert.Equal(SecretPurposes.LlmApiKey, encryption.Purpose);

        using JsonDocument providerDocument = JsonDocument.Parse(capture.Body);
        JsonElement messages = providerDocument.RootElement.GetProperty("messages");
        string providerPrompt = messages.EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "user")
            .GetProperty("content")
            .GetString()!;
        const string requestJsonPrefix = "{\"schemaVersion\":";
        int requestJsonStart = providerPrompt.IndexOf(
            requestJsonPrefix,
            StringComparison.Ordinal);
        Assert.True(requestJsonStart >= 0);
        string requestJson = providerPrompt[requestJsonStart..];
        using JsonDocument selectionDocument = JsonDocument.Parse(requestJson);
        JsonElement selectionRoot = selectionDocument.RootElement;
        Assert.Equal(
            ["schemaVersion", "requestId", "mode", "question", "evidenceFingerprint", "candidates"],
            selectionRoot.EnumerateObject().Select(property => property.Name));
        Assert.Equal("客户如何完成付款？", selectionRoot.GetProperty("question").GetString());
        JsonElement candidate = Assert.Single(selectionRoot.GetProperty("candidates").EnumerateArray());
        Assert.Equal(
            ["candidateHandle", "text", "narrativeAllowed"],
            candidate.EnumerateObject().Select(property => property.Name));
        Assert.Equal("k1", candidate.GetProperty("candidateHandle").GetString());
        Assert.Equal("客户提交订单后，系统校验并处理请求。", candidate.GetProperty("text").GetString());
        Assert.False(candidate.GetProperty("narrativeAllowed").GetBoolean());
        Assert.DoesNotContain(request.SystemId.ToString(), requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request.CandidateClaims[0].DocumentVersionId.ToString(), requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request.CandidateClaims[0].StableSectionId.ToString(), requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("a1", requestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("r1", requestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("encrypted-chat-key", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("plaintext-chat-key", capture.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_StrictMissingModeEcho_ReturnsInvalidSelectionAsync()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        string providerJson = CreateRawSelectionJson(request).Replace(
            "\"mode\": \"business\",",
            string.Empty,
            StringComparison.Ordinal);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var transport = new RecordingStreamTransport([providerJson]);
        var services = new ServiceCollection();
        services.AddPreparedPass2Agents();
        services.AddScoped<IChatProviderStreamingTransport>(_ => transport);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            new OpenAiCaptureHandler(() => string.Empty),
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Equal(ChatContractFailureCode.InvalidSelection, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task GenerateAsync_ConfiguredReasoningEffort_PropagatesToStreamingRequestAsync()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var transport = new RecordingStreamTransport([CreateRawSelectionJson(request)]);
        var services = new ServiceCollection();
        services.Configure<ChatProviderReasoningOptions>(options =>
            options.ReasoningEffort = "none");
        services.AddPreparedPass2Agents();
        services.AddScoped<IChatProviderStreamingTransport>(_ => transport);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            new OpenAiCaptureHandler(() => string.Empty),
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.NotNull(outcome.Result);
        Assert.Equal("none", Assert.Single(transport.Requests).ReasoningEffort);
    }

    [Fact]
    public async Task GenerateAsync_UnsupportedQuestion_AcceptsExplicitEmptySelection()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        string rawSelectionJson = $$"""
            {
              "schemaVersion": "{{ChatSynthesisResult.SupportedSchemaVersion}}",
              "requestId": "{{request.RequestId}}",
              "mode": "business",
              "evidenceFingerprint": "{{request.EvidenceFingerprint}}",
              "answerability": "unsupported",
              "selections": []
            }
            """;
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var encryption = new CapturingEncryptionService("plaintext-chat-key");
        var capture = new OpenAiCaptureHandler(
            () => CreateSseResponse(rawSelectionJson),
            "text/event-stream");
        var services = new ServiceCollection();
        services.AddSingleton<ILlmProviderRepository>(new FakeProviderRepository([provider]));
        services.AddSingleton<ISecretEncryptionService>(encryption);
        services.AddSingleton<HttpMessageHandler>(capture);
        services.AddPreparedPass2Agents();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            encryption,
            new ChatPromptCatalog(),
            capture,
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        ChatSynthesisResult result = Assert.IsType<ChatSynthesisResult>(outcome.Result);
        Assert.Empty(result.Selections);
        Assert.Null(outcome.FailureCode);
    }

    [Fact]
    public async Task GenerateAsync_JsonFenceAndKnownSchemaEcho_NormalizesClosedSelection()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        string rawSelectionJson = CreateRawSelectionJson(request).Replace(
            ChatSynthesisResult.SupportedSchemaVersion,
            ChatSynthesisResult.CompatibleProviderEchoSchemaVersion,
            StringComparison.Ordinal);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var encryption = new CapturingEncryptionService("plaintext-chat-key");
        var capture = new OpenAiCaptureHandler(
            () => CreateSseResponse($"```json\n{rawSelectionJson}\n```"),
            "text/event-stream");
        var services = new ServiceCollection();
        services.AddSingleton<ILlmProviderRepository>(new FakeProviderRepository([provider]));
        services.AddSingleton<ISecretEncryptionService>(encryption);
        services.AddSingleton<HttpMessageHandler>(capture);
        services.AddPreparedPass2Agents();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            encryption,
            new ChatPromptCatalog(),
            capture,
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.IsType<ChatSynthesisResult>(outcome.Result);
        Assert.Equal(rawSelectionJson, outcome.RawSelectionJson);
        Assert.Null(outcome.FailureCode);
    }

    [Fact]
    public async Task GenerateAsync_CallerCancelsInFlight_PreservesCancellationWithoutRetry()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.It);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var handler = new BlockingHandler();
        var dispatcher = new ChatAgentDispatcher(
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            handler,
            TimeProvider.System);
        using var cancellation = new CancellationTokenSource();

        Task<ChatSynthesisOutcome> pending = dispatcher.GenerateAsync(request, cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        ChatSynthesisOutcome outcome = await pending;

        Assert.Equal(ChatContractFailureCode.Cancelled, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_ProviderExceedsThirtySeconds_TimesOutWithoutRetry()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var handler = new BlockingHandler();
        var timeProvider = new ManualTimeoutTimeProvider();
        var dispatcher = new ChatAgentDispatcher(
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            handler,
            timeProvider);

        Task<ChatSynthesisOutcome> pending = dispatcher.GenerateAsync(request);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(30), timeProvider.DueTime);
        timeProvider.Fire();
        ChatSynthesisOutcome outcome = await pending;

        Assert.Equal(ChatContractFailureCode.TimedOut, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_ProviderFailure_FailsClosedWithoutRetryOrDiagnosticLeak()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.It);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        const string rawDiagnostic = "provider-secret C:/host/stack-trace";
        var handler = new ProviderFailureHandler(rawDiagnostic);
        var dispatcher = new ChatAgentDispatcher(
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            handler,
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Equal(ChatContractFailureCode.ProviderFailure, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Equal(1, handler.CallCount);
        Assert.DoesNotContain(rawDiagnostic, outcome.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_MalformedSelection_FailsWithoutRetainingRawProviderContent()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        const string malformedSelection = "{\"schemaVersion\":\"prepared-chat-answer-v1\",";
        var handler = new OpenAiCaptureHandler(
            () => CreateSseResponse(malformedSelection),
            "text/event-stream");
        var dispatcher = new ChatAgentDispatcher(
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            handler,
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Equal(ChatContractFailureCode.InvalidSelection, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Equal(2, handler.CallCount);
        Assert.DoesNotContain(malformedSelection, outcome.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_SyntaxFailure_UsesOneStreamingRepairWithinTheSameBoundedRequest()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        string validSelection = CreateRawSelectionJson(request);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var handler = new StreamingSequenceHandler(
        [
            CreateSseResponse("{not-valid-json"),
            CreateSseResponse(validSelection),
        ]);
        var encryption = new CapturingEncryptionService("plaintext-chat-key");
        var services = new ServiceCollection();
        services.AddSingleton<ILlmProviderRepository>(new FakeProviderRepository([provider]));
        services.AddSingleton<ISecretEncryptionService>(encryption);
        services.AddSingleton<HttpMessageHandler>(handler);
        services.AddPreparedPass2Agents();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            encryption,
            new ChatPromptCatalog(),
            handler,
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Null(outcome.FailureCode);
        Assert.NotNull(outcome.Result);
        Assert.Equal(2, handler.CallCount);
        Assert.All(
            handler.RequestBodies,
            body => Assert.Contains("\"stream\":true", body, StringComparison.Ordinal));
        Assert.Equal(
            handler.RequestBodies[0].Contains(request.EvidenceFingerprint, StringComparison.Ordinal),
            handler.RequestBodies[1].Contains(request.EvidenceFingerprint, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("invalid-handle")]
    [InlineData("unsupported-selection")]
    [InlineData("mismatched-evidence-fingerprint")]
    public async Task GenerateAsync_ValidJsonSemanticOrContractFailure_DoesNotUseSyntaxRepair(
        string failureVector)
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        string validSelection = CreateRawSelectionJson(request);
        string invalidSelection = failureVector switch
        {
            "invalid-handle" => validSelection.Replace(
                "\"candidateHandle\": \"k1\"",
                "\"candidateHandle\": \"not a candidate handle\"",
                StringComparison.Ordinal),
            "unsupported-selection" => validSelection.Replace(
                "\"answerability\": \"supported\"",
                "\"answerability\": \"unsupported\"",
                StringComparison.Ordinal),
            "mismatched-evidence-fingerprint" => validSelection.Replace(
                request.EvidenceFingerprint,
                new string('b', request.EvidenceFingerprint.Length),
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(failureVector), failureVector, null),
        };
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var transport = new RecordingStreamTransport([invalidSelection, validSelection]);
        var services = new ServiceCollection();
        services.AddPreparedPass2Agents();
        services.AddScoped<IChatProviderStreamingTransport>(_ => transport);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            new OpenAiCaptureHandler(() => string.Empty),
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Equal(ChatContractFailureCode.InvalidSelection, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task GenerateAsync_DirectConstructionAggregateJson_IsRejectedAsProviderFailure()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        string validSelection = CreateRawSelectionJson(request);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var handler = new StreamingMediaTypeSequenceHandler(
            [(CreateChatResponse(validSelection), "application/json")]);
        var dispatcher = new ChatAgentDispatcher(
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            handler,
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Equal(ChatContractFailureCode.ProviderFailure, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_TenSecondsWithoutDecodedDataOrTerminal_CancelsUnderlyingProviderStreamAndTimesOut()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UnixEpoch);
        ChatSynthesisRequest request = CreateRequest(
            ChatMode.Business,
            DateTimeOffset.UnixEpoch.AddSeconds(29).ToUnixTimeMilliseconds());
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var transport = new CancellationTrackingBlockingStreamTransport();
        var services = new ServiceCollection();
        services.AddPreparedPass2Agents();
        services.AddScoped<IChatProviderStreamingTransport>(_ => transport);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            new OpenAiCaptureHandler(() => string.Empty),
            clock);
        using var cleanup = new CancellationTokenSource();

        Task<ChatSynthesisOutcome> pending = dispatcher.GenerateAsync(request, cleanup.Token);
        try
        {
            await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            clock.Fire(TimeSpan.FromSeconds(10));

            ChatSynthesisOutcome outcome = await pending.WaitAsync(TimeSpan.FromSeconds(2));
            await transport.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(ChatContractFailureCode.TimedOut, outcome.FailureCode);
            Assert.Null(outcome.Result);
            Assert.Null(outcome.RawSelectionJson);
            Assert.Single(transport.Requests);
        }
        finally
        {
            cleanup.Cancel();
            try
            {
                _ = await pending.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception exception) when (
                exception is OperationCanceledException or TimeoutException)
            {
            }
        }
    }

    [Fact]
    public async Task GenerateAsync_DeadlineCancellationReportedAsProviderError_ReturnsTimedOut()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UnixEpoch);
        ChatSynthesisRequest request = CreateRequest(
            ChatMode.Business,
            DateTimeOffset.UnixEpoch.AddSeconds(30).ToUnixTimeMilliseconds());
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var transport = new ProviderErrorOnCancellationStreamTransport();
        var services = new ServiceCollection();
        services.AddPreparedPass2Agents();
        services.AddScoped<IChatProviderStreamingTransport>(_ => transport);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            new OpenAiCaptureHandler(() => string.Empty),
            clock);

        Task<ChatSynthesisOutcome> pending = dispatcher.GenerateAsync(request);
        await transport.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        clock.Fire(TimeSpan.FromSeconds(30));
        ChatSynthesisOutcome outcome = await pending.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(ChatContractFailureCode.TimedOut, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task GenerateAsync_UnregisteredDirectSseTransport_CannotProduceAcceptedAttempt()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        string validSelection = CreateRawSelectionJson(request);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var handler = new OpenAiCaptureHandler(
            () => CreateSseResponse(validSelection),
            "text/event-stream");
        var dispatcher = new ChatAgentDispatcher(
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            handler,
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Equal(ChatContractFailureCode.ProviderFailure, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
    }

    [Fact]
    public async Task GenerateAsync_DirectConstructionAggregateJsonDuringSyntaxRepair_IsRejectedWithoutAcceptedResult()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var handler = new StreamingMediaTypeSequenceHandler(
        [
            (CreateSseResponse("{not-valid-json"), "text/event-stream"),
            (CreateChatResponse(CreateRawSelectionJson(request)), "application/json"),
        ]);
        var dispatcher = new ChatAgentDispatcher(
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            handler,
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Equal(ChatContractFailureCode.ProviderFailure, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_SyntaxRepair_UsesCanonicalAttemptNameAndPreservesBinding()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        string validSelection = CreateRawSelectionJson(request);
        var transport = new RecordingStreamTransport(["{not-valid-json", validSelection]);
        var services = new ServiceCollection();
        services.AddPreparedPass2Agents();
        services.AddScoped<IChatProviderStreamingTransport>(_ => transport);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            new OpenAiCaptureHandler(() => string.Empty),
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Null(outcome.FailureCode);
        Assert.NotNull(outcome.Result);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal("primary", transport.Requests[0].AttemptKind);
        Assert.Equal("syntax-repair", transport.Requests[1].AttemptKind);
        Assert.All(transport.Requests, dispatched =>
        {
            Assert.Equal(request.RequestId, dispatched.RequestId);
            Assert.Equal(provider.Id.ToString("D"), dispatched.ProviderIdentity);
            Assert.Equal(request.EvidenceFingerprint, dispatched.EvidencePacketFingerprint);
            Assert.Equal(request.AbsoluteDeadlineUnixTimeMilliseconds, dispatched.AbsoluteDeadlineUnixTimeMilliseconds);
        });
        Assert.Contains("{not-valid-json", transport.Requests[1].Prompt, StringComparison.Ordinal);
        Assert.Contains("category", transport.Requests[1].Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid", transport.Requests[1].Prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_SyntaxRepairWith2999MillisecondsRemaining_RefusesSecondStreamedAttempt()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UnixEpoch);
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business, 2_999);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var transport = new RecordingStreamTransport(["{not-valid-json"]);
        var services = new ServiceCollection();
        services.AddPreparedPass2Agents();
        services.AddScoped<IChatProviderStreamingTransport>(_ => transport);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            new OpenAiCaptureHandler(() => string.Empty),
            clock);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Equal(ChatContractFailureCode.InvalidSelection, outcome.FailureCode);
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
        Assert.Equal("primary", Assert.Single(transport.Requests).AttemptKind);
    }

    [Fact]
    public async Task GenerateAsync_SyntaxRepairWith3000MillisecondsRemaining_PermitsExactlyOneStreamedRepair()
    {
        var clock = new ControllableTimeProvider(DateTimeOffset.UnixEpoch);
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business, 3_000);
        string validSelection = CreateRawSelectionJson(request);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var transport = new RecordingStreamTransport(["{not-valid-json", validSelection]);
        var services = new ServiceCollection();
        services.AddPreparedPass2Agents();
        services.AddScoped<IChatProviderStreamingTransport>(_ => transport);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            new OpenAiCaptureHandler(() => string.Empty),
            clock);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Null(outcome.FailureCode);
        Assert.NotNull(outcome.Result);
        Assert.Equal(["primary", "syntax-repair"], transport.Requests.Select(item => item.AttemptKind));
    }

    [Fact]
    public async Task GenerateAsync_PrimarySyntaxFailureThenRepairSyntaxFailure_RecordsAttemptLifecycleInOrder()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var transport = new RecordingStreamTransport(["{not-valid-json", "{still-not-valid-json"]);
        var services = new ServiceCollection();
        services.AddPreparedPass2Agents();
        services.AddScoped<IChatProviderStreamingTransport>(_ => transport);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            new CapturingEncryptionService("plaintext-chat-key"),
            new ChatPromptCatalog(),
            new OpenAiCaptureHandler(() => string.Empty),
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Equal(ChatContractFailureCode.InvalidSelection, outcome.FailureCode);
        Assert.Equal(
            ["primary", "syntax-repair"],
            transport.Requests.Select(item => item.AttemptKind));
        Assert.Null(outcome.Result);
        Assert.Null(outcome.RawSelectionJson);
    }

    [Fact]
    // Characterization-only: the registered raw adapter already accepts a valid SSE response;
    // no RED is expected for this positive composition boundary.
    public async Task GenerateAsync_RegisteredRawSseTransport_AcceptsOnlyStreamedResponse()
    {
        ChatSynthesisRequest request = CreateRequest(ChatMode.Business);
        string validSelection = CreateRawSelectionJson(request);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        var handler = new StreamingMediaTypeSequenceHandler(
            [(CreateSseResponse(validSelection), "text/event-stream")]);
        var encryption = new CapturingEncryptionService("plaintext-chat-key");
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<ILlmProviderRepository>(new FakeProviderRepository([provider]));
        services.AddSingleton<ISecretEncryptionService>(encryption);
        services.AddSingleton<HttpMessageHandler>(handler);
        services.AddPreparedPass2Agents();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        var dispatcher = new ChatAgentDispatcher(
            scope.ServiceProvider,
            new ChatAgentProviderResolver(
                new FakeOverrideRepository(null),
                new FakeProviderRepository([provider])),
            encryption,
            new ChatPromptCatalog(),
            handler,
            TimeProvider.System);

        ChatSynthesisOutcome outcome = await dispatcher.GenerateAsync(request);

        Assert.Null(outcome.FailureCode);
        Assert.NotNull(outcome.Result);
        Assert.Equal(1, handler.CallCount);
    }

    private static ChatSynthesisRequest CreateRequest(
        ChatMode mode,
        long absoluteDeadlineUnixTimeMilliseconds = long.MaxValue)
    {
        string fingerprint = new('a', 64);
        CandidateClaim candidate = CandidateClaim.Create(
            "k1",
            fingerprint,
            mode,
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "客户提交订单后，系统校验并处理请求。",
            ["a1"],
            ["r1"]);
        return ChatSynthesisRequest.Create(
            "request-1",
            mode,
            Guid.NewGuid(),
            "客户如何完成付款？",
            fingerprint,
            [candidate],
            absoluteDeadlineUnixTimeMilliseconds);
    }

    private static string CreateRawSelectionJson(ChatSynthesisRequest request) => $$"""
        {
          "selections": [ { "order": 1, "candidateHandle": "k1" } ],
          "evidenceFingerprint": "{{request.EvidenceFingerprint}}",
          "mode": "{{(request.Mode == ChatMode.Business ? "business" : "it")}}",
          "answerability": "supported",
          "requestId": "{{request.RequestId}}",
          "schemaVersion": "{{ChatSynthesisResult.SupportedSchemaVersion}}"
        }
        """;

    private static string CreateChatResponse(string rawSelectionJson)
    {
        return JsonSerializer.Serialize(new
        {
            id = "chatcmpl-story-115",
            @object = "chat.completion",
            created = 1_785_542_400,
            model = "chat-model",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = rawSelectionJson },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 },
        });
    }

    private sealed class FakeOverrideRepository(SystemLlmProviderOverride? overrideEntry)
        : ISystemLlmProviderOverrideRepository
    {
        public Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(
            Guid systemId,
            AgentType agentType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                overrideEntry is not null &&
                overrideEntry.SystemId == systemId &&
                overrideEntry.AgentType == agentType
                    ? overrideEntry
                    : null);

        public Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SystemLlmProviderOverride> AddAsync(
            SystemLlmProviderOverride value,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> RemoveAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeProviderRepository(IReadOnlyList<LlmProvider> providers)
        : ILlmProviderRepository
    {
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(providers);

        public Task<LlmProvider?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(providers.SingleOrDefault(provider => provider.Id == id));

        public Task<bool> NameExistsAsync(
            string name,
            Guid? excludeProviderId = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LlmProvider> AddAsync(
            LlmProvider provider,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Remove(LlmProvider provider) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingEncryptionService(string plaintext) : ISecretEncryptionService
    {
        public int DecryptCount { get; private set; }
        public string? Purpose { get; private set; }

        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default)
        {
            Purpose = purpose;
            DecryptCount++;
            return Task.FromResult(plaintext);
        }
    }

    private sealed class OpenAiCaptureHandler(
        Func<string> responseFactory,
        string mediaType = "application/json") : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseFactory(), Encoding.UTF8, mediaType),
            };
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation delay unexpectedly completed.");
        }
    }

    private sealed class ProviderFailureHandler(string diagnostic) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(diagnostic, Encoding.UTF8, "text/plain"),
            });
        }
    }

    private sealed class StreamingSequenceHandler(IReadOnlyList<string> responses) : HttpMessageHandler
    {
        private int _responseIndex;

        public int CallCount { get; private set; }
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            if ((uint)_responseIndex >= (uint)responses.Count)
            {
                throw new InvalidOperationException("The dispatcher made more streaming attempts than allowed.");
            }

            string response = responses[_responseIndex++];
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "text/event-stream"),
            };
        }
    }

    private sealed class StreamingMediaTypeSequenceHandler(
        IReadOnlyList<(string Body, string MediaType)> responses) : HttpMessageHandler
    {
        private int _responseIndex;

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if ((uint)_responseIndex >= (uint)responses.Count)
            {
                throw new InvalidOperationException("The dispatcher made more streaming attempts than allowed.");
            }

            (string body, string mediaType) = responses[_responseIndex++];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),
            });
        }
    }

    private sealed class RecordingStreamTransport(IReadOnlyList<string> answers) :
        IChatProviderStreamingTransport
    {
        public List<ChatProviderStreamRequest> Requests { get; } = [];

        public async IAsyncEnumerable<ChatProviderStreamSignal> StreamAsync(
            ChatProviderStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await Task.Yield();
            string answer = answers[Requests.Count - 1];
            yield return new ChatProviderStreamSignal(
                ChatProviderStreamSignalKind.AnswerDelta,
                Encoding.UTF8.GetBytes(answer));
            yield return new ChatProviderStreamSignal(
                ChatProviderStreamSignalKind.Terminal,
                ReadOnlyMemory<byte>.Empty);
        }

    }

    private sealed class CancellationTrackingBlockingStreamTransport :
        IChatProviderStreamingTransport
    {
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ChatProviderStreamRequest> Requests { get; } = [];

        public async IAsyncEnumerable<ChatProviderStreamSignal> StreamAsync(
            ChatProviderStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult();
                throw;
            }

            yield break;
        }
    }

    private sealed class ProviderErrorOnCancellationStreamTransport :
        IChatProviderStreamingTransport
    {
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ChatProviderStreamRequest> Requests { get; } = [];

        public async IAsyncEnumerable<ChatProviderStreamSignal> StreamAsync(
            ChatProviderStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Started.TrySetResult();
            bool cancellationObserved = false;
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancellationObserved = true;
            }

            if (cancellationObserved)
            {
                yield return new ChatProviderStreamSignal(
                    ChatProviderStreamSignalKind.ProviderError,
                    ReadOnlyMemory<byte>.Empty);
            }
        }
    }

    private static string CreateSseResponse(string answer)
    {
        string json = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-story-125",
            @object = "chat.completion.chunk",
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { content = answer },
                    finish_reason = "stop",
                },
            },
        });
        return $"data: {json}\n\ndata: [DONE]\n\n";
    }

    private sealed class ManualTimeoutTimeProvider : TimeProvider
    {
        private TimerCallback? _callback;
        private object? _state;

        public TimeSpan? DueTime { get; private set; }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            _callback = callback;
            _state = state;
            DueTime = dueTime;
            return new ManualTimer();
        }

        public void Fire() =>
            (_callback ?? throw new InvalidOperationException("No timeout timer was created."))(_state);

        private sealed class ManualTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class ControllableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ControllableTimer> _timers = [];

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ControllableTimer(callback, state, dueTime, period);
            lock (_gate)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        public void Fire(TimeSpan dueTime)
        {
            ControllableTimer timer;
            lock (_gate)
            {
                timer = _timers.LastOrDefault(candidate =>
                    !candidate.IsDisposed && candidate.DueTime == dueTime) ??
                    throw new InvalidOperationException(
                        $"No active timer exists for due time {dueTime}.");
            }

            timer.Fire();
        }

        private sealed class ControllableTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period) : ITimer
        {
            public TimeSpan DueTime { get; private set; } = dueTime;
            public TimeSpan Period { get; private set; } = period;
            public bool IsDisposed { get; private set; }

            public bool Change(TimeSpan updatedDueTime, TimeSpan updatedPeriod)
            {
                if (IsDisposed)
                {
                    return false;
                }

                DueTime = updatedDueTime;
                Period = updatedPeriod;
                return true;
            }

            public void Fire()
            {
                if (!IsDisposed)
                {
                    callback(state);
                }
            }

            public void Dispose() => IsDisposed = true;

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}

using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents;
using Microsoft.AspNetCore.Components.Authorization;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared.Validators.Chat;
using Vulgata.Web.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

public sealed class PreparedChatCoordinatorTests
{
    [Fact]
    public void IChatEvidenceRetriever_RetrieveAsync_UsesServerOwnedConversationRequest()
    {
        MethodInfo retrieveMethod = Assert.Single(
            typeof(IChatEvidenceRetriever)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == nameof(IChatEvidenceRetriever.RetrieveAsync)));

        ParameterInfo[] parameters = retrieveMethod.GetParameters();
        Assert.Equal(typeof(Task<ChatEvidenceResult>), retrieveMethod.ReturnType);
        Assert.Equal(typeof(ChatRetrievalRequest), parameters[0].ParameterType);
        Assert.Equal(typeof(RepositoryAccessScope), parameters[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);

        Assert.NotNull(typeof(ChatRetrievalRequest).GetProperty("Conversation"));
        Assert.NotNull(typeof(ChatRetrievalRequest).GetProperty("DeadlineUtc"));
    }

    [Fact]
    public void Constructor_ChatAgentResultValidator_IsRequiredDependency()
    {
        ParameterInfo parameter = Assert.Single(
            Assert.Single(typeof(PreparedChatCoordinator).GetConstructors())
                .GetParameters()
                .Where(parameter => parameter.ParameterType == typeof(ChatAgentResultValidator)));

        Assert.False(parameter.HasDefaultValue);
    }

    [Fact]
    public async Task ListScopesAsync_AuthenticatedOwner_ReturnsVisibleSystemAndRepositories()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository(
            "账户仓库",
            "https://example.test/account.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        Repository second = system.AddRepository(
            "渠道仓库",
            "https://example.test/channel.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            new StubAuthorizationVerifier(),
            new CanonicalEmptyAdvisoryContextReader(),
            new StubEvidenceRetriever(),
            candidateExtractor,
            new StubDispatcher(),
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        IReadOnlyList<PreparedChatScopeOption> scopes = await coordinator.ListScopesAsync();

        PreparedChatScopeOption scope = Assert.Single(scopes);
        Assert.Equal(system.Id, scope.SystemId);
        Assert.Equal("支付系统", scope.SystemDisplayName);
        Assert.Equal(
            new[] { first.Id, second.Id }.Order(),
            scope.Repositories.Select(repository => repository.RepositoryId).Order());
        Assert.Contains(scope.Repositories, repository => repository.DisplayName == "账户仓库");
        Assert.Contains(scope.Repositories, repository => repository.DisplayName == "渠道仓库");
    }

    [Fact]
    public async Task ListScopesAsync_DecoySystemAndRepositoryPair_OffersOnlyPreparedPair()
    {
        var preparedSystem = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository preparedConsumer = preparedSystem.AddRepository(
            "账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository preparedProvider = preparedSystem.AddRepository(
            "渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var decoySystem = new SystemEntity("诱饵系统", null, null, DateTimeOffset.UtcNow);
        Repository decoyConsumer = decoySystem.AddRepository(
            "诱饵消费仓库", "https://example.test/decoy-consumer.git", null, null, DateTimeOffset.UtcNow);
        Repository decoyProvider = decoySystem.AddRepository(
            "诱饵提供仓库", "https://example.test/decoy-provider.git", null, null, DateTimeOffset.UtcNow);
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([preparedSystem, decoySystem]),
            new StubRepositoryRepository(
                [preparedConsumer, preparedProvider, decoyConsumer, decoyProvider]),
            new StubAuthorizationVerifier(),
            new CanonicalEmptyAdvisoryContextReader(),
            new StubEvidenceRetriever(),
            candidateExtractor,
            new StubDispatcher(),
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(preparedSystem.Id),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        IReadOnlyList<PreparedChatScopeOption> scopes = await coordinator.ListScopesAsync();

        PreparedChatScopeOption scope = Assert.Single(scopes);
        Assert.Equal(preparedSystem.Id, scope.SystemId);
        Assert.Equal(
            new[] { preparedConsumer.Id, preparedProvider.Id }.Order(),
            scope.Repositories.Select(repository => repository.RepositoryId).Order());
    }

    [Fact]
    public async Task AskAsync_AuthorizedPreparedScope_ReturnsExactServerClaimsAndCitations()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier();
        var retriever = new StubEvidenceRetriever();
        var dispatcher = new StubDispatcher();
        var currentness = new StubCurrentnessValidator();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            authorization,
            new CanonicalEmptyAdvisoryContextReader(),
            retriever,
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [second.Id, first.Id],
            "  客户   完成付款？ \n");

        Assert.Equal(PreparedChatResultKind.Succeeded, result.Kind);
        GroundedAnswer answer = Assert.IsType<GroundedAnswer>(result.Answer);
        MaterialClaim claim = Assert.Single(answer.MaterialClaims);
        Assert.Equal("客户提交付款后，系统完成校验。", claim.Text);
        DocumentCitation citation = Assert.Single(answer.DocumentCitations);
        Assert.Equal(claim.CitationIds, [citation.CitationId]);
        Assert.Equal("业务流程", citation.DocumentTitle);
        Assert.Equal("src/PaymentFlow.cs", citation.RepositoryRelativePath);
        Assert.Equal(20, citation.LineStart);
        Assert.Equal(24, citation.LineEnd);
        ChatRetrievalRequest retrievalRequest = Assert.IsType<ChatRetrievalRequest>(retriever.LastRequest);
        Assert.Equal("客户 完成付款？", retrievalRequest.Question);
        Assert.Equal(ChatMode.Business, retrievalRequest.Conversation.Scope.Mode);
        Assert.Equal(system.Id, retrievalRequest.Conversation.Scope.SystemId);
        Assert.Equal(
            new[] { first.Id, second.Id }.Order(),
            retrievalRequest.Conversation.Scope.RepositoryIds);
        Assert.Empty(retrievalRequest.Conversation.Turns);
        Assert.NotEqual(default, retrievalRequest.DeadlineUtc);
        Assert.Equal(2, authorization.CallCount);
        Assert.Equal(1, retriever.CallCount);
        Assert.Equal(1, dispatcher.CallCount);
        CandidateClaim dispatchedCandidate = Assert.Single(dispatcher.LastRequest?.CandidateClaims ?? []);
        Assert.Equal("客户提交付款后，系统完成校验。", dispatchedCandidate.Text);
        Assert.Equal(1, currentness.CallCount);
    }

    [Fact]
    public async Task AskAsync_RepeatedFollowUps_ReFetchesEvidenceAndRevalidatesCurrentnessOnEveryTurn()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var retriever = new StubEvidenceRetriever();
        var currentness = new StubCurrentnessValidator();
        var dispatcher = new StubDispatcher();
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            retriever: retriever,
            dispatcher: dispatcher,
            currentness: currentness);

        PreparedChatResult initial = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");
        PreparedChatResult firstFollowUp = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");
        PreparedChatResult secondFollowUp = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.All(
            new[] { initial, firstFollowUp, secondFollowUp },
            result => Assert.Equal(PreparedChatResultKind.Succeeded, result.Kind));
        Assert.Equal(3, retriever.CallCount);
        Assert.Equal(3, currentness.CallCount);
        Assert.Equal(3, dispatcher.CallCount);
    }

    [Fact]
    public async Task AskAsync_BusinessCodeLogicFallback_PublishesSafeNarrativeWithOriginalCitation()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var retriever = new StubEvidenceRetriever
        {
            PrimaryDocumentTypeOverride = DocumentType.CodeLogic,
            PrimaryContent = "系统调用会话服务并保存客户会话。",
        };
        var dispatcher = new StubDispatcher
        {
            NarrativeText = "系统接收客户请求后更新并保存登录会话。",
        };
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            new StubAuthorizationVerifier(),
            new CanonicalEmptyAdvisoryContextReader(),
            retriever,
            candidateExtractor,
            dispatcher,
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户登录会话如何更新？");

        GroundedAnswer answer = Assert.IsType<GroundedAnswer>(result.Answer);
        Assert.Equal("系统接收客户请求后更新并保存登录会话。", Assert.Single(answer.MaterialClaims).Text);
        Assert.Equal(DocumentType.CodeLogic, Assert.Single(answer.DocumentCitations).DocumentType);
    }

    [Fact]
    public async Task AskAsync_ProviderTimeoutWithStrongIdentifierMatch_ReturnsGroundedCandidate()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier();
        var retriever = new StubEvidenceRetriever
        {
            PrimaryContent = "UpdateSessionOp 构造 Redis key、写入 token 并提交会话。",
        };
        var dispatcher = new StubDispatcher { FailureCode = ChatContractFailureCode.TimedOut };
        var currentness = new StubCurrentnessValidator();
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            authorization,
            retriever,
            dispatcher,
            currentness);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.It,
            system.Id,
            [first.Id, second.Id],
            "UpdateSessionOp 如何构造 Redis key、写入 token 并提交会话？");

        GroundedAnswer answer = Assert.IsType<GroundedAnswer>(result.Answer);
        Assert.Equal(PreparedChatResultKind.Succeeded, result.Kind);
        Assert.Equal(retriever.PrimaryContent, Assert.Single(answer.MaterialClaims).Text);
        Assert.Single(answer.DocumentCitations);
        Assert.Equal(1, currentness.CallCount);
        Assert.Equal(2, authorization.CallCount);
    }

    [Fact]
    public async Task AskAsync_StrongGroundedCandidateWithoutRelationship_StillValidatesCurrentness()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var retriever = new StubEvidenceRetriever
        {
            IncludeRelationshipSupport = false,
            PrimaryContent = "AuthTimesCheckOp 检查登录认证错误次数是否超过允许上限。",
        };
        var dispatcher = new StubDispatcher { FailureCode = ChatContractFailureCode.TimedOut };
        var currentness = new StubCurrentnessValidator();
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            retriever: retriever,
            dispatcher: dispatcher,
            currentness: currentness);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "认证错误次数检查操作类 AuthTimesCheckOp 负责什么？");

        Assert.Equal(PreparedChatResultKind.Succeeded, result.Kind);
        Assert.IsType<GroundedAnswer>(result.Answer);
        Assert.Equal(1, currentness.CallCount);
        Assert.Empty(Assert.IsType<ChatCurrentnessValidationRequest>(
            currentness.LastRequest).Relationships);
    }

    [Fact]
    public async Task AskAsync_ProviderUnsupportedWithStrongChineseMatch_ReturnsGroundedCandidate()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var retriever = new StubEvidenceRetriever
        {
            PrimaryContent = "认证错误次数检查操作类负责检查登录认证错误次数是否超过上限。",
        };
        var dispatcher = new StubDispatcher { Answerability = ChatAnswerability.Unsupported };
        var currentness = new StubCurrentnessValidator();
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            retriever: retriever,
            dispatcher: dispatcher,
            currentness: currentness);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "认证错误次数检查操作类负责什么？");

        GroundedAnswer answer = Assert.IsType<GroundedAnswer>(result.Answer);
        Assert.Equal(PreparedChatResultKind.Succeeded, result.Kind);
        Assert.Equal(retriever.PrimaryContent, Assert.Single(answer.MaterialClaims).Text);
        Assert.Single(answer.DocumentCitations);
        Assert.Equal(1, currentness.CallCount);
    }

    [Fact]
    public async Task AskAsync_MalformedProviderSelection_FailsBeforeCurrentnessAndFinalAuthorizationWithoutLeak()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier();
        var dispatcher = new StubDispatcher
        {
            RawSelectionJsonOverride = "{\"diagnostic\":\"provider-secret /restricted/path edge-r1\"}",
        };
        var currentness = new StubCurrentnessValidator();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            authorization,
            new CanonicalEmptyAdvisoryContextReader(),
            new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(PreparedChatResultKind.EvidenceUnavailable, result.Kind);
        Assert.Equal("当前知识证据不可用或已更新，请稍后重试。", result.Message);
        Assert.Null(result.Answer);
        Assert.DoesNotContain("provider-secret", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("restricted", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("edge-r1", result.Message, StringComparison.Ordinal);
        Assert.Equal(1, authorization.CallCount);
        Assert.Equal(0, currentness.CallCount);
    }

    [Fact]
    public async Task AskAsync_ProviderDispatchCarriesOneCoordinatorOwnedTwentyNineSecondDeadline()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository(
            "账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository(
            "渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var dispatcher = new StubDispatcher();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            new StubAuthorizationVerifier(),
            new CanonicalEmptyAdvisoryContextReader(),
            new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher,
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        _ = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        PropertyInfo? deadlineProperty = typeof(ChatSynthesisRequest).GetProperty(
            "AbsoluteDeadlineUnixTimeMilliseconds");
        Assert.NotNull(deadlineProperty);
        long deadline = Assert.IsType<long>(deadlineProperty.GetValue(dispatcher.LastRequest));
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.InRange(deadline - now, 1, 29_000);
    }

    [Fact]
    public async Task AskAsync_SemanticValidationFailure_DoesNotInvokeARepairDispatch()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository(
            "账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository(
            "渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var dispatcher = new StubDispatcher
        {
            RawSelectionJsonOverride = "{\"schemaVersion\":\"prepared-chat-answer-v1\",\"requestId\":\"request-1\",\"mode\":\"business\",\"evidenceFingerprint\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"answerability\":\"supported\",\"selections\":[{\"candidateHandle\":\"unknown\",\"order\":1}]}",
        };
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            new StubAuthorizationVerifier(),
            new CanonicalEmptyAdvisoryContextReader(),
            new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher,
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(PreparedChatResultKind.EvidenceUnavailable, result.Kind);
        Assert.Equal(1, dispatcher.CallCount);
        Assert.Null(result.Answer);
    }

    [Fact]
    public async Task AskAsync_SemanticallyRejectedStreamingAnswer_RecordsContentDarkCompletionSummary()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository(
            "账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository(
            "渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var provider = new LlmProvider(
            "chat-model",
            "https://chat-provider.example.com/v1",
            "encrypted-chat-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        string semanticAnswer = JsonSerializer.Serialize(new
        {
            schemaVersion = ChatSynthesisResult.SupportedSchemaVersion,
            requestId = "placeholder",
            mode = "business",
            evidenceFingerprint = new string('a', 64),
            answerability = "supported",
            selections = new[] { new { candidateHandle = "unknown", order = 1 } },
        });
        var handler = new SemanticRejectionResponseHandler(semanticAnswer);
        var services = new ServiceCollection();
        services.AddSingleton<ILlmProviderRepository>(new SingleProviderRepository(provider));
        services.AddSingleton<ISecretEncryptionService>(new PlaintextSecretEncryptionService());
        services.AddSingleton<HttpMessageHandler>(handler);
        services.AddPreparedPass2Agents();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        ChatStreamRuntimeObservationRecorder runtimeRecorder =
            scope.ServiceProvider.GetRequiredService<ChatStreamRuntimeObservationRecorder>();
        IChatProviderStreamingTransport transport =
            scope.ServiceProvider.GetRequiredService<IChatProviderStreamingTransport>();
        var dispatcher = new SemanticRejectingDispatcher(
            transport,
            provider,
            semanticAnswer);
        var completionRecorder = new RecordingCompletionSummaryRecorder(runtimeRecorder);
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            new StubAuthorizationVerifier(),
            new CanonicalEmptyAdvisoryContextReader(),
            new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher,
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            completionRecorder);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(PreparedChatResultKind.EvidenceUnavailable, result.Kind);
        Assert.Null(result.Answer);
        Assert.NotNull(dispatcher.RequestId);
        ChatStreamCompletionSummary summary = Assert.Single(completionRecorder.Summaries);
        Assert.Equal("rejected-semantic", summary.FinalValidationOutcome);
        Assert.Equal(runtimeRecorder.GetRuntimeObservationIds(dispatcher.RequestId!), summary.RuntimeObservationIds);
        string serializedObservation = JsonSerializer.Serialize(
            runtimeRecorder.GetRuntimeObservations(dispatcher.RequestId!));
        string serializedSummary = JsonSerializer.Serialize(summary);
        Assert.DoesNotContain("unknown", serializedObservation, StringComparison.Ordinal);
        Assert.DoesNotContain(semanticAnswer, serializedObservation, StringComparison.Ordinal);
        Assert.DoesNotContain("unknown", serializedSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(semanticAnswer, serializedSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskAsync_UnsupportedStreamingAnswer_RecordsNonAcceptedCompletionSummary()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var provider = new LlmProvider(
            "streaming-chat",
            "https://provider.example/v1",
            "encrypted-key",
            ApiType.ChatCompletions,
            AgentType.Chat,
            DateTimeOffset.UtcNow);
        using var responseHandler = new SemanticRejectionResponseHandler("{}");
        var runtimeRecorder = new ChatStreamRuntimeObservationRecorder();
        var transport = new OpenAiCompatibleChatProviderStreamingTransport(
            new SingleProviderRepository(provider),
            new PlaintextSecretEncryptionService(),
            responseHandler,
            runtimeRecorder);
        var dispatcher = new SemanticRejectingDispatcher(
            transport,
            provider,
            "{}",
            ChatAnswerability.Unsupported);
        var retriever = new StubEvidenceRetriever
        {
            PrimaryContent = "客户付款记录。",
        };
        var completionRecorder = new RecordingCompletionSummaryRecorder(runtimeRecorder);
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            retriever: retriever,
            dispatcher: dispatcher,
            completionRecorder: completionRecorder);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(PreparedChatResultKind.Unsupported, result.Kind);
        Assert.Null(result.Answer);
        Assert.NotNull(dispatcher.RequestId);
        ChatStreamCompletionSummary summary = Assert.Single(completionRecorder.Summaries);
        Assert.Equal("rejected-semantic", summary.FinalValidationOutcome);
        Assert.Equal(runtimeRecorder.GetRuntimeObservationIds(dispatcher.RequestId!), summary.RuntimeObservationIds);
        string serializedObservation = JsonSerializer.Serialize(
            runtimeRecorder.GetRuntimeObservations(dispatcher.RequestId!));
        string serializedSummary = JsonSerializer.Serialize(summary);
        Assert.DoesNotContain("{}", serializedObservation, StringComparison.Ordinal);
        Assert.DoesNotContain("{}", serializedSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskAsync_UnrelatedQuestion_ReturnsChineseUnsupportedResponseWithoutCitations()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier();
        var dispatcher = new StubDispatcher { Answerability = ChatAnswerability.Unsupported };
        var currentness = new StubCurrentnessValidator();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            authorization,
            new CanonicalEmptyAdvisoryContextReader(),
            new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "北京天气怎么样？");

        Assert.Equal(PreparedChatResultKind.Unsupported, result.Kind);
        Assert.Equal("当前知识库中没有足够信息回答该问题。", result.Message);
        Assert.Null(result.Answer);
        Assert.Equal(0, dispatcher.CallCount);
        Assert.Equal(2, authorization.CallCount);
        Assert.Equal(0, currentness.CallCount);
    }

    [Fact]
    public async Task AskAsync_ValidSelection_UsesExactSecurityOrderAndPublishesCompleteFixedCitations()
    {
        var trace = new List<string>();
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier { Trace = trace };
        var retriever = new StubEvidenceRetriever { Trace = trace };
        var dispatcher = new StubDispatcher { Trace = trace };
        var currentness = new StubCurrentnessValidator { Trace = trace };
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            authorization,
            new CanonicalEmptyAdvisoryContextReader(),
            retriever,
            candidateExtractor,
            dispatcher,
            currentness,
            new RecordingTimeProvider(trace),
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(
            ["pre-authorization", "retrieval", "dispatch", "currentness", "final-authorization", "citation-materialization"],
            trace);
        Assert.Equal(1, currentness.CallCount);
        ChatCurrentnessValidationRequest currentnessRequest = Assert.IsType<ChatCurrentnessValidationRequest>(currentness.LastRequest);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), currentnessRequest.RepositoryIds.Order());
        Assert.Contains(currentnessRequest.SourceAnchors, anchor =>
            anchor.AnchorId == Id(401) &&
            anchor.RepositoryRelativePath == "src/PaymentFlow.cs" &&
            anchor.LineStart == 20 &&
            anchor.LineEnd == 24);
        GroundedAnswer answer = Assert.IsType<GroundedAnswer>(result.Answer);
        MaterialClaim claim = Assert.Single(answer.MaterialClaims);
        DocumentCitation citation = Assert.Single(answer.DocumentCitations);
        Assert.Equal([citation.CitationId], claim.CitationIds);
        Assert.Equal("src/PaymentFlow.cs", citation.RepositoryRelativePath);
    }

    [Fact]
    public async Task AskAsync_AuthorizedScope_ReadsServerOwnedAdvisoryBeforeEvidenceRetrieval()
    {
        var trace = new List<string>();
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier { Trace = trace };
        var retriever = new StubEvidenceRetriever { Trace = trace };
        var advisoryReader = new CanonicalEmptyAdvisoryContextReader { Trace = trace };
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            authorization,
            retriever,
            currentness: new StubCurrentnessValidator { Trace = trace },
            advisoryReader: advisoryReader);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(PreparedChatResultKind.Succeeded, result.Kind);
        Assert.Equal(2, advisoryReader.CallCount);
        Assert.Equal(1, retriever.CallCount);
        Assert.True(trace.IndexOf("pre-authorization") < trace.IndexOf("advisory"));
        Assert.True(trace.IndexOf("advisory") < trace.IndexOf("retrieval"));
        Assert.True(trace.IndexOf("currentness") < trace.LastIndexOf("advisory"));
    }

    [Theory]
    [InlineData("value")]
    [InlineData("updated-at")]
    [InlineData("authority-fingerprint")]
    [InlineData("repository-membership")]
    [InlineData("repository-order")]
    [InlineData("applied-pending-disposition")]
    public async Task AskAsync_AppliedAdvisoryChangesBeforeReveal_ReReadsAndWithholdsAnswer(
        string mutation)
    {
        var trace = new List<string>();
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        ChatAdvisoryContextSnapshot initial = CreateAdvisorySnapshot(system.Id, first.Id, second.Id);
        ChatAdvisoryContextReadResult changed = CreateChangedAdvisoryResult(
            mutation,
            initial);
        var advisoryReader = new SequencedAdvisoryContextReader(
            [ChatAdvisoryContextReadResult.Succeeded(initial), changed],
            trace);
        var authorization = new StubAuthorizationVerifier { Trace = trace };
        var retriever = new StubEvidenceRetriever { Trace = trace };
        var dispatcher = new StubDispatcher { Trace = trace };
        var currentness = new StubCurrentnessValidator { Trace = trace };
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            authorization,
            retriever,
            dispatcher,
            currentness,
            advisoryReader);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [second.Id, first.Id],
            "客户如何付款？");

        Assert.Equal(2, advisoryReader.CallCount);
        Assert.Equal(PreparedChatResultKind.EvidenceUnavailable, result.Kind);
        Assert.Null(result.Answer);
        Assert.True(trace.IndexOf("advisory-initial") < trace.IndexOf("retrieval"));
        Assert.True(trace.IndexOf("currentness") < trace.IndexOf("advisory-reveal"));
        Assert.DoesNotContain("citation-materialization", trace);
    }

    [Fact]
    public async Task AskAsync_RetrievalPacketAdvisoryDiffersFromInitialAndReveal_WithholdsWithoutConversationAppend()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        ChatAdvisoryContextSnapshot initial = CreateAdvisorySnapshot(system.Id, first.Id, second.Id);
        ChatAdvisoryContextSnapshot retrievalAuthority = initial with
        {
            ComposedText = "事务内检索规则",
            EncodedUtf8Bytes = Encoding.UTF8.GetByteCount("事务内检索规则"),
            AuthorityFingerprint = new string('b', 64),
        };
        var advisoryReader = new SequencedAdvisoryContextReader(
            Enumerable.Repeat(
                ChatAdvisoryContextReadResult.Succeeded(initial),
                4).ToArray(),
            []);
        var retriever = new StubEvidenceRetriever
        {
            PacketAdvisoryContextOverride = retrievalAuthority,
        };
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            retriever: retriever,
            advisoryReader: advisoryReader);

        PreparedChatResult firstResult = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");
        _ = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户付款还需要哪些规则？");

        Assert.Equal(PreparedChatResultKind.EvidenceUnavailable, firstResult.Kind);
        Assert.Null(firstResult.Answer);
        Assert.Equal(2, retriever.Requests.Count);
        Assert.Empty(retriever.Requests[1].Conversation.Turns);
        Assert.Equal(initial.AuthorityFingerprint, retriever.Requests[0].AdvisoryContext.AuthorityFingerprint);
        Assert.NotEqual(
            retriever.Requests[0].AdvisoryContext.AuthorityFingerprint,
            retriever.PacketAdvisoryContextOverride.AuthorityFingerprint);
    }

    [Theory]
    [InlineData("retrieval")]
    [InlineData("currentness")]
    [InlineData("advisory-reveal")]
    public async Task AskAsync_AbsoluteDeadlineExpiresDuringServerStage_TimesOutWithoutPublishingOrAppending(
        string expiringStage)
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var clock = new AdjustableCoordinatorTimeProvider(DateTimeOffset.UnixEpoch);
        var retriever = new StubEvidenceRetriever();
        var currentness = new StubCurrentnessValidator();
        var advisoryReader = new DeadlineAwareAdvisoryContextReader();
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            retriever: retriever,
            currentness: currentness,
            advisoryReader: advisoryReader,
            timeProvider: clock);
        PreparedChatResult prior = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");
        Assert.Equal(PreparedChatResultKind.Succeeded, prior.Kind);

        Action expire = () => clock.Advance(TimeSpan.FromSeconds(29));
        switch (expiringStage)
        {
            case "retrieval":
                retriever.BeforeResult = expire;
                break;
            case "currentness":
                currentness.BeforeResult = expire;
                break;
            case "advisory-reveal":
                advisoryReader.AdvanceOnCall = advisoryReader.CallCount + 2;
                advisoryReader.BeforeResult = expire;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expiringStage), expiringStage, null);
        }

        const string expiredQuestion = "客户付款需要哪些规则？";
        PreparedChatResult expired = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            expiredQuestion);
        retriever.BeforeResult = null;
        currentness.BeforeResult = null;
        advisoryReader.BeforeResult = null;
        advisoryReader.AdvanceOnCall = null;
        _ = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户付款如何确认？");

        Assert.Equal(PreparedChatResultKind.TimedOut, expired.Kind);
        Assert.Equal("回答超时，请重试。", expired.Message);
        Assert.Null(expired.Answer);
        Assert.True(expiringStage switch
        {
            "retrieval" => retriever.CancellationObservedAfterBeforeResult,
            "currentness" => currentness.CancellationObservedAfterBeforeResult,
            "advisory-reveal" => advisoryReader.CancellationObservedAfterBeforeResult,
            _ => false,
        });
        Assert.Equal(3, retriever.Requests.Count);
        Assert.DoesNotContain(
            retriever.Requests[2].Conversation.Turns,
            turn => string.Equals(turn.Question, expiredQuestion, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AskAsync_FinalPublicationBudgetExpiry_ReturnsTimedOutWithoutAppendingConversationTurn()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var clock = new AdjustableCoordinatorTimeProvider(DateTimeOffset.UnixEpoch);
        var retriever = new StubEvidenceRetriever();
        var dispatcher = new StubDispatcher();
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            retriever: retriever,
            dispatcher: dispatcher,
            timeProvider: clock);

        dispatcher.BeforeResult = () => clock.AdvanceOnReadAfter(1, TimeSpan.FromSeconds(29));

        PreparedChatResult expired = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户付款需要哪些规则？");

        dispatcher.BeforeResult = null;
        _ = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户付款如何确认？");

        Assert.Equal(PreparedChatResultKind.TimedOut, expired.Kind);
        Assert.Equal("回答超时，请重试。", expired.Message);
        Assert.Null(expired.Answer);
        Assert.Equal(2, retriever.Requests.Count);
        Assert.Empty(retriever.Requests[1].Conversation.Turns);
    }

    [Theory]
    [InlineData(ChatAdvisoryContextReadResultKind.NotFoundOrUnauthorized, PreparedChatResultKind.MissingOrUnauthorized)]
    [InlineData(ChatAdvisoryContextReadResultKind.InconsistentCurrentEvidence, PreparedChatResultKind.EvidenceUnavailable)]
    public async Task AskAsync_AdvisoryFailure_StopsBeforeEvidenceRetrieval(
        ChatAdvisoryContextReadResultKind advisoryResultKind,
        PreparedChatResultKind expectedResultKind)
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var retriever = new StubEvidenceRetriever();
        var advisoryReader = new CanonicalEmptyAdvisoryContextReader
        {
            ResultKind = advisoryResultKind,
        };
        var coordinator = CreateCoordinator(
            system,
            [first, second],
            retriever: retriever,
            advisoryReader: advisoryReader);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(expectedResultKind, result.Kind);
        Assert.Equal(1, advisoryReader.CallCount);
        Assert.Equal(0, retriever.CallCount);
    }

    [Fact]
    public async Task AskAsync_CurrentnessDenial_FailsBeforeFinalAuthorizationAndCitationPublication()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier();
        var currentness = new StubCurrentnessValidator
        {
            Outcome = ChatCurrentnessValidationOutcome.MissingOrStale,
        };
        var coordinator = CreateCoordinator(system, [first, second], authorization, currentness: currentness);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(PreparedChatResultKind.EvidenceUnavailable, result.Kind);
        Assert.Equal("当前知识证据不可用或已更新，请稍后重试。", result.Message);
        Assert.Null(result.Answer);
        Assert.Equal(1, currentness.CallCount);
        Assert.Equal(1, authorization.CallCount);
    }

    [Fact]
    public async Task AskAsync_FinalAuthorizationRevoked_FailsMissingEquivalentWithoutCitationPublication()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier();
        authorization.Results.Enqueue(true);
        authorization.Results.Enqueue(false);
        var currentness = new StubCurrentnessValidator();
        var coordinator = CreateCoordinator(system, [first, second], authorization, currentness: currentness);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.It,
            system.Id,
            [first.Id, second.Id],
            "付款接口如何实现？");

        Assert.Equal(PreparedChatResultKind.MissingOrUnauthorized, result.Kind);
        Assert.Equal("未找到可用的知识范围，或您无权访问。", result.Message);
        Assert.Null(result.Answer);
        Assert.Equal(1, currentness.CallCount);
        Assert.Equal(2, authorization.CallCount);
    }

    [Fact]
    public async Task AskAsync_PrecheckAuthorizationDenied_FailsMissingEquivalentBeforeRetrieval()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier();
        authorization.Results.Enqueue(false);
        var retriever = new StubEvidenceRetriever();
        var coordinator = CreateCoordinator(system, [first, second], authorization, retriever);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(PreparedChatResultKind.MissingOrUnauthorized, result.Kind);
        Assert.Equal("未找到可用的知识范围，或您无权访问。", result.Message);
        Assert.Null(result.Answer);
        Assert.Equal(1, authorization.CallCount);
        Assert.Equal(0, retriever.CallCount);
    }

    [Theory]
    [InlineData(ChatEvidenceResultKind.NotFoundOrUnauthorized, PreparedChatResultKind.MissingOrUnauthorized, "未找到可用的知识范围，或您无权访问。")]
    [InlineData(ChatEvidenceResultKind.IncompleteOrStale, PreparedChatResultKind.EvidenceUnavailable, "当前知识证据不可用或已更新，请稍后重试。")]
    public async Task AskAsync_LiveRetrievalDenialOrStaleEvidence_FailsClosedWithoutDispatch(
        ChatEvidenceResultKind evidenceKind,
        PreparedChatResultKind expectedKind,
        string expectedMessage)
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var retriever = new StubEvidenceRetriever { ForcedResultKind = evidenceKind };
        var dispatcher = new StubDispatcher();
        var coordinator = CreateCoordinator(system, [first, second], retriever: retriever, dispatcher: dispatcher);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(result.Answer);
        Assert.Equal(1, retriever.CallCount);
        Assert.Equal(0, dispatcher.CallCount);
    }

    [Theory]
    [InlineData(ChatContractFailureCode.ProviderFailure, PreparedChatResultKind.ProviderUnavailable, "暂时无法生成回答，请稍后重试。")]
    [InlineData(ChatContractFailureCode.TimedOut, PreparedChatResultKind.TimedOut, "回答超时，请重试。")]
    [InlineData(ChatContractFailureCode.Cancelled, PreparedChatResultKind.Cancelled, "已取消本次提问。")]
    [InlineData(ChatContractFailureCode.InvalidSelection, PreparedChatResultKind.EvidenceUnavailable, "当前知识证据不可用或已更新，请稍后重试。")]
    public async Task AskAsync_ProviderFailure_FailsClosedWithoutRawDiagnostic(
        ChatContractFailureCode failureCode,
        PreparedChatResultKind expectedKind,
        string expectedMessage)
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var dispatcher = new StubDispatcher { FailureCode = failureCode };
        var currentness = new StubCurrentnessValidator();
        var coordinator = CreateCoordinator(system, [first, second], dispatcher: dispatcher, currentness: currentness);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？");

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(result.Answer);
        Assert.DoesNotContain("provider-secret", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, currentness.CallCount);
    }

    [Fact]
    public async Task AskAsync_CallerAlreadyCancelled_ReturnsBoundedCancellationWithoutStartingWork()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier();
        var retriever = new StubEvidenceRetriever();
        var dispatcher = new StubDispatcher();
        var currentness = new StubCurrentnessValidator();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            authorization,
            new CanonicalEmptyAdvisoryContextReader(),
            retriever,
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [first.Id, second.Id],
            "客户如何付款？",
            cancellation.Token);

        Assert.Equal(PreparedChatResultKind.Cancelled, result.Kind);
        Assert.Equal("已取消本次提问。", result.Message);
        Assert.Null(result.Answer);
        Assert.Equal(0, authorization.CallCount);
        Assert.Equal(0, retriever.CallCount);
        Assert.Equal(0, dispatcher.CallCount);
        Assert.Equal(0, currentness.CallCount);
    }

    [Fact]
    public async Task AskAsync_InvalidModeScopeOrQuestion_ReturnsBoundedFailureWithoutStartingWork()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository first = system.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository second = system.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier();
        var retriever = new StubEvidenceRetriever();
        var dispatcher = new StubDispatcher();
        var currentness = new StubCurrentnessValidator();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository([first, second]),
            authorization,
            new CanonicalEmptyAdvisoryContextReader(),
            retriever,
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());
        (ChatMode Mode, Guid SystemId, IReadOnlyCollection<Guid> RepositoryIds, string Question)[] invalidRequests =
        [
            ((ChatMode)999, system.Id, [first.Id, second.Id], "客户如何付款？"),
            (ChatMode.Business, Guid.Empty, [first.Id, second.Id], "客户如何付款？"),
            (ChatMode.Business, system.Id, [first.Id], "客户如何付款？"),
            (ChatMode.Business, system.Id, [first.Id, first.Id], "客户如何付款？"),
            (ChatMode.Business, system.Id, [first.Id, second.Id], "  \r\n "),
            (ChatMode.Business, system.Id, [first.Id, second.Id], new string('问', 2001)),
        ];

        foreach ((ChatMode mode, Guid systemId, IReadOnlyCollection<Guid> repositoryIds, string question) in invalidRequests)
        {
            PreparedChatResult result = await coordinator.AskAsync(mode, systemId, repositoryIds, question);

            Assert.Equal(PreparedChatResultKind.InvalidRequest, result.Kind);
            Assert.Equal("请选择业务模式、系统和两个不同的仓库，并输入 1 至 2000 个字符的问题。", result.Message);
            Assert.Null(result.Answer);
        }

        Assert.Equal(0, authorization.CallCount);
        Assert.Equal(0, retriever.CallCount);
        Assert.Equal(0, dispatcher.CallCount);
        Assert.Equal(0, currentness.CallCount);
    }

    [Fact]
    public async Task AskAsync_CrossSystemRepository_ReturnsMissingBeforeAuthorizationOrRetrieval()
    {
        var selectedSystem = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository selectedRepository = selectedSystem.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        var otherSystem = new SystemEntity("清算系统", null, null, DateTimeOffset.UtcNow);
        Repository otherRepository = otherSystem.AddRepository("清算仓库", "https://example.test/clearing.git", null, null, DateTimeOffset.UtcNow);
        var authorization = new StubAuthorizationVerifier();
        var retriever = new StubEvidenceRetriever();
        var dispatcher = new StubDispatcher();
        var currentness = new StubCurrentnessValidator();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([selectedSystem]),
            new StubRepositoryRepository([selectedRepository, otherRepository]),
            authorization,
            new CanonicalEmptyAdvisoryContextReader(),
            retriever,
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.Business,
            selectedSystem.Id,
            [selectedRepository.Id, otherRepository.Id],
            "客户如何付款？");

        Assert.Equal(PreparedChatResultKind.MissingOrUnauthorized, result.Kind);
        Assert.Equal("未找到可用的知识范围，或您无权访问。", result.Message);
        Assert.Null(result.Answer);
        Assert.Equal(0, authorization.CallCount);
        Assert.Equal(0, retriever.CallCount);
        Assert.Equal(0, dispatcher.CallCount);
        Assert.Equal(0, currentness.CallCount);
    }

    [Fact]
    public async Task AskAsync_AfterPriorSuccessChangedModeAndScopeProviderFailure_PublishesNoPriorOrPartialAnswer()
    {
        var firstSystem = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository firstA = firstSystem.AddRepository("账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository firstB = firstSystem.AddRepository("渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var secondSystem = new SystemEntity("清算系统", null, null, DateTimeOffset.UtcNow);
        Repository secondA = secondSystem.AddRepository("账务仓库", "https://example.test/ledger.git", null, null, DateTimeOffset.UtcNow);
        Repository secondB = secondSystem.AddRepository("报表仓库", "https://example.test/report.git", null, null, DateTimeOffset.UtcNow);
        var dispatcher = new StubDispatcher();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([firstSystem, secondSystem]),
            new StubRepositoryRepository([firstA, firstB, secondA, secondB]),
            new StubAuthorizationVerifier(),
            new CanonicalEmptyAdvisoryContextReader(),
            new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher,
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new RecordingCompletionSummaryRecorder());

        PreparedChatResult prior = await coordinator.AskAsync(
            ChatMode.Business,
            firstSystem.Id,
            [firstA.Id, firstB.Id],
            "客户如何付款？");
        dispatcher.FailureCode = ChatContractFailureCode.ProviderFailure;

        PreparedChatResult failed = await coordinator.AskAsync(
            ChatMode.It,
            secondSystem.Id,
            [secondA.Id, secondB.Id],
            "付款接口如何实现？");

        Assert.Equal(PreparedChatResultKind.Succeeded, prior.Kind);
        Assert.Equal(PreparedChatResultKind.ProviderUnavailable, failed.Kind);
        Assert.Equal("暂时无法生成回答，请稍后重试。", failed.Message);
        Assert.DoesNotContain("provider-secret", failed.Message, StringComparison.Ordinal);
        Assert.Null(failed.Answer);
        Assert.Equal(2, dispatcher.CallCount);
    }

    private static PreparedChatCoordinator CreateCoordinator(
        SystemEntity system,
        IReadOnlyList<Repository> repositories,
        StubAuthorizationVerifier? authorization = null,
        StubEvidenceRetriever? retriever = null,
        IChatAgentDispatcher? dispatcher = null,
        StubCurrentnessValidator? currentness = null,
        IChatAdvisoryContextReader? advisoryReader = null,
        IChatStreamCompletionSummaryRecorder? completionRecorder = null,
        TimeProvider? timeProvider = null)
    {
        var candidateExtractor = new ChatCandidateExtractor();
        return new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository(repositories),
            authorization ?? new StubAuthorizationVerifier(),
            advisoryReader ?? new CanonicalEmptyAdvisoryContextReader(),
            retriever ?? new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher ?? new StubDispatcher(),
            currentness ?? new StubCurrentnessValidator(),
            timeProvider ?? TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            completionRecorder ?? new RecordingCompletionSummaryRecorder());
    }

    private static ChatAdvisoryContextSnapshot CreateAdvisorySnapshot(
        Guid systemId,
        Guid firstRepositoryId,
        Guid secondRepositoryId)
    {
        DateTimeOffset updatedAt = DateTimeOffset.Parse("2026-08-29T00:00:00Z");
        ChatAdvisoryContextSourceUnit[] units =
        [
            new(ChatAdvisoryContextSourceKind.Global, Id(900), updatedAt, "全局规则"),
            new(ChatAdvisoryContextSourceKind.System, systemId, updatedAt, "系统规则"),
            new(ChatAdvisoryContextSourceKind.Repository, firstRepositoryId, updatedAt, "账户规则"),
            new(ChatAdvisoryContextSourceKind.Repository, secondRepositoryId, updatedAt, "渠道规则"),
        ];
        const string composedText = "全局规则\n系统规则\n账户规则\n渠道规则";
        return new ChatAdvisoryContextSnapshot(
            units,
            composedText,
            ["全局", "系统", "账户", "渠道"],
            Encoding.UTF8.GetByteCount(composedText),
            new string('a', 64));
    }

    private static ChatAdvisoryContextReadResult CreateChangedAdvisoryResult(
        string mutation,
        ChatAdvisoryContextSnapshot initial)
    {
        if (mutation == "applied-pending-disposition")
        {
            return ChatAdvisoryContextReadResult.InconsistentCurrentEvidence();
        }

        ChatAdvisoryContextSourceUnit[] units = initial.SourceUnits.ToArray();
        string composedText = initial.ComposedText;
        string fingerprint = initial.AuthorityFingerprint;
        switch (mutation)
        {
            case "value":
                units[1] = units[1] with { Text = "已变更系统规则" };
                composedText = "全局规则\n已变更系统规则\n账户规则\n渠道规则";
                fingerprint = new string('b', 64);
                break;
            case "updated-at":
                units[1] = units[1] with { UpdatedAt = units[1].UpdatedAt.AddTicks(1) };
                fingerprint = new string('c', 64);
                break;
            case "authority-fingerprint":
                fingerprint = new string('d', 64);
                break;
            case "repository-membership":
                units[3] = units[3] with { SourceId = Id(999) };
                fingerprint = new string('e', 64);
                break;
            case "repository-order":
                (units[2], units[3]) = (units[3], units[2]);
                fingerprint = new string('f', 64);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        return ChatAdvisoryContextReadResult.Succeeded(new ChatAdvisoryContextSnapshot(
            units,
            composedText,
            initial.Terms,
            Encoding.UTF8.GetByteCount(composedText),
            fingerprint));
    }

    private sealed class RecordingCompletionSummaryRecorder(
        IChatStreamCompletionSummaryRecorder? runtimeSource = null) :
        IChatStreamCompletionSummaryRecorder
    {
        public List<ChatStreamCompletionSummary> Summaries { get; } = [];

        public IReadOnlyList<string> GetRuntimeObservationIds(string requestId) =>
            runtimeSource?.GetRuntimeObservationIds(requestId) ?? [];

        public void RecordCompletion(ChatStreamCompletionSummary summary) =>
            Summaries.Add(summary);
    }

    private sealed class SequencedAdvisoryContextReader(
        IReadOnlyCollection<ChatAdvisoryContextReadResult> results,
        List<string> trace) : IChatAdvisoryContextReader
    {
        private readonly Queue<ChatAdvisoryContextReadResult> _results = new(results);

        public int CallCount { get; private set; }

        public Task<ChatAdvisoryContextReadResult> ReadAsync(
            AuthorizedChatScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            trace.Add(CallCount == 1 ? "advisory-initial" : "advisory-reveal");
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class DeadlineAwareAdvisoryContextReader : IChatAdvisoryContextReader
    {
        private static readonly ChatAdvisoryContextSnapshot _empty = new(
            [],
            string.Empty,
            [],
            0,
            new string('0', 64));

        public int CallCount { get; private set; }
        public int? AdvanceOnCall { get; set; }
        public Action? BeforeResult { get; set; }
        public bool CancellationObservedAfterBeforeResult { get; private set; }

        public Task<ChatAdvisoryContextReadResult> ReadAsync(
            AuthorizedChatScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (AdvanceOnCall == CallCount)
            {
                BeforeResult?.Invoke();
                CancellationObservedAfterBeforeResult = cancellationToken.IsCancellationRequested;
            }

            return Task.FromResult(ChatAdvisoryContextReadResult.Succeeded(_empty));
        }
    }

    private sealed class AdjustableCoordinatorTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<AdjustableTimer> _timers = [];
        private DateTimeOffset _current = current;
        private int? _readsUntilAdvance;
        private TimeSpan _advanceOnReadBy;

        public void AdvanceOnReadAfter(int reads, TimeSpan duration)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(reads);
            _readsUntilAdvance = reads;
            _advanceOnReadBy = duration;
        }

        public override DateTimeOffset GetUtcNow()
        {
            bool advance;
            lock (_gate)
            {
                advance = _readsUntilAdvance == 0;
                if (_readsUntilAdvance is int readsUntilAdvance)
                {
                    _readsUntilAdvance = readsUntilAdvance == 0 ? null : readsUntilAdvance - 1;
                }
            }

            if (advance)
            {
                Advance(_advanceOnReadBy);
            }

            lock (_gate)
            {
                return _current;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new AdjustableTimer(this, callback, state, dueTime, period);
            lock (_gate)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            AdjustableTimer[] due;
            lock (_gate)
            {
                _current = _current.Add(duration);
                due = _timers.Where(timer => timer.IsDue(_current)).ToArray();
            }

            foreach (AdjustableTimer timer in due)
            {
                timer.Fire();
            }
        }

        private sealed class AdjustableTimer(
            AdjustableCoordinatorTimeProvider owner,
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period) : ITimer
        {
            private DateTimeOffset _dueAt = owner._current.Add(dueTime);
            private TimeSpan _period = period;
            private bool _disposed;

            internal bool IsDue(DateTimeOffset now) => !_disposed && _dueAt <= now;

            internal void Fire()
            {
                lock (owner._gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    if (_period == Timeout.InfiniteTimeSpan)
                    {
                        _disposed = true;
                        owner._timers.Remove(this);
                    }
                    else
                    {
                        _dueAt = owner._current.Add(_period);
                    }
                }

                callback(state);
            }

            public bool Change(TimeSpan updatedDueTime, TimeSpan updatedPeriod)
            {
                lock (owner._gate)
                {
                    if (_disposed)
                    {
                        return false;
                    }

                    _dueAt = owner._current.Add(updatedDueTime);
                    _period = updatedPeriod;
                    return true;
                }
            }

            public void Dispose()
            {
                lock (owner._gate)
                {
                    _disposed = true;
                    owner._timers.Remove(this);
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class RecordingTimeProvider(List<string> trace) : TimeProvider
    {
        private int _callCount;

        public override DateTimeOffset GetUtcNow()
        {
            if (++_callCount == 2)
            {
                trace.Add("citation-materialization");
            }

            return DateTimeOffset.UnixEpoch;
        }
    }

    private sealed class FixedAuthenticationStateProvider(string userId) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            ClaimsPrincipal principal = new(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "Test"));
            return Task.FromResult(new AuthenticationState(principal));
        }
    }

    private sealed class StubSystemRepository(IReadOnlyList<SystemEntity> systems) : ISystemRepository
    {
        public Task<IReadOnlyList<SystemEntity>> ListVisibleAsync(SystemOwnerFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(systems);

        public Task<SystemEntity?> GetVisibleByIdAsync(Guid id, SystemOwnerFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(systems.SingleOrDefault(system => system.Id == id));

        public Task<SystemEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(string name, Guid? excludeSystemId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemEntity> AddAsync(SystemEntity system, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemDeleteResult> DeleteIfNoDependenciesAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemDependencyCounts> GetDependencyCountsAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SystemOwnerAssignmentWriteResult> AssignOwnerAsync(Guid systemId, string userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveOwnerAsync(Guid systemId, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SystemOwnerAssignmentSummary>> ListOwnersAsync(Guid systemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountOwnerAssignmentsByUserAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubRepositoryRepository(IReadOnlyList<Repository> repositories) : IRepositoryRepository
    {
        public Task<IReadOnlyList<Repository>> ListBySystemAsync(Guid systemId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Repository>>(repositories.Where(repository => repository.SystemId == systemId).ToArray());

        public Task<Repository?> GetByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(repositories.SingleOrDefault(repository => repository.Id == repositoryId));

        public Task<Repository?> GetBySystemAndIdAsync(Guid systemId, Guid repositoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(repositories.SingleOrDefault(repository => repository.SystemId == systemId && repository.Id == repositoryId));

        public Task<IReadOnlyList<Repository>> ListStandaloneAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Repository?> GetStandaloneByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> NameExistsAsync(Guid systemId, string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> StandaloneNameExistsAsync(string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Repository> AddAsync(Repository repository, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Remove(Repository repository) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubAuthorizationVerifier : IRepositoryAuthorizationVerifier
    {
        public int CallCount { get; private set; }
        public Queue<bool> Results { get; } = new();
        public List<string>? Trace { get; set; }

        public ValueTask<bool> HasCurrentAccessAsync(IReadOnlyCollection<Guid> requiredRepositoryIds, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Trace?.Add(CallCount == 1 ? "pre-authorization" : "final-authorization");
            return ValueTask.FromResult(Results.TryDequeue(out bool result) ? result : true);
        }
    }

    private sealed class StubPreparedChatScopeResolver(Guid? preparedSystemId = null) :
        IPreparedChatScopeResolver
    {
        public Task<PreparedChatScopeBinding?> ResolveAsync(
            Guid systemId,
            IReadOnlyCollection<Guid> repositoryIds,
            RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Guid[] ids = repositoryIds.Distinct().ToArray();
            PreparedChatScopeBinding? binding =
                ids.Length == 2 && (preparedSystemId is null || preparedSystemId == systemId)
                    ? new PreparedChatScopeBinding(systemId, ids[0], ids[1])
                    : null;
            return Task.FromResult(binding);
        }
    }

    private sealed class StubEvidenceRetriever : IChatEvidenceRetriever
    {
        public int CallCount { get; private set; }
        public ChatRetrievalRequest? LastRequest { get; private set; }
        public List<string>? Trace { get; set; }
        public ChatEvidenceResultKind? ForcedResultKind { get; set; }
        public DocumentType? PrimaryDocumentTypeOverride { get; set; }
        public string PrimaryContent { get; set; } = "客户提交付款后，系统完成校验。";
        public bool IncludeRelationshipSupport { get; set; } = true;
        public ChatAdvisoryContextSnapshot? PacketAdvisoryContextOverride { get; set; }
        public Action? BeforeResult { get; set; }
        public bool CancellationObservedAfterBeforeResult { get; private set; }
        public List<ChatRetrievalRequest> Requests { get; } = [];

        public Task<ChatEvidenceResult> RetrieveAsync(ChatRetrievalRequest request, RepositoryAccessScope accessScope, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Trace?.Add("retrieval");
            LastRequest = request;
            Requests.Add(request);
            if (ForcedResultKind is ChatEvidenceResultKind forcedResultKind)
            {
                return Task.FromResult(forcedResultKind switch
                {
                    ChatEvidenceResultKind.NotFoundOrUnauthorized => ChatEvidenceResult.NotFoundOrUnauthorized(),
                    ChatEvidenceResultKind.InvalidRequest => ChatEvidenceResult.InvalidRequest(),
                    ChatEvidenceResultKind.IncompleteOrStale => ChatEvidenceResult.IncompleteOrStale(),
                    _ => throw new InvalidOperationException("Found requires the generated packet."),
                });
            }

            Guid firstRepositoryId = request.RepositoryIds[0];
            Guid secondRepositoryId = request.RepositoryIds[1];
            EvidenceDocument businessDocument = new(
                "d1",
                Id(101),
                Id(201),
                firstRepositoryId,
                PrimaryDocumentTypeOverride ??
                (request.Mode == ChatMode.Business ? DocumentType.BusinessLogic : DocumentType.CodeLogic),
                request.Mode == ChatMode.Business ? "业务流程" : "技术流程",
                new string('a', 40));
            EvidenceDocument codeDocument = new(
                "d2",
                Id(102),
                Id(202),
                secondRepositoryId,
                DocumentType.CodeLogic,
                "技术实现",
                new string('b', 40));
            ChatAdvisoryContextSnapshot packetAdvisory =
                PacketAdvisoryContextOverride ?? request.AdvisoryContext;
            EvidencePacket packet = EvidencePacket.Create(
                request.RequestId,
                request.Mode,
                request.SystemId,
                request.RepositoryIds,
                [businessDocument, codeDocument],
                [
                    new EvidenceSection(
                        "s1",
                        "d1",
                        Id(301),
                        1,
                        DocumentSectionKind.Overview,
                        "业务概览",
                        PrimaryContent,
                        ["a1"],
                        IncludeRelationshipSupport ? ["d2"] : [])
                ],
                [
                    new EvidenceAnchor(
                        "a1",
                        Id(401),
                        "d1",
                        businessDocument.DocumentVersionId,
                        firstRepositoryId,
                        "src/PaymentFlow.cs",
                        20,
                        24,
                        new string('c', 64))
                ],
                IncludeRelationshipSupport
                    ?
                    [
                        new EvidenceRelationship(
                            "r1",
                            Id(410),
                            ChatRelationshipType.References,
                            "d1",
                            "d2",
                            1,
                            Id(411),
                            1,
                            new string('d', 64),
                            null,
                            null)
                    ]
                    : [],
                    [],
                    advisoryContext: packetAdvisory.ComposedText,
                    advisoryContextAuthorityFingerprint: packetAdvisory.AuthorityFingerprint);
            if (BeforeResult is not null)
            {
                BeforeResult();
                CancellationObservedAfterBeforeResult = cancellationToken.IsCancellationRequested;
            }
            return Task.FromResult(ChatEvidenceResult.Found(packet));
        }
    }

    private sealed class SemanticRejectingDispatcher(
        IChatProviderStreamingTransport transport,
        LlmProvider provider,
        string streamedAnswer,
        ChatAnswerability answerability = ChatAnswerability.Supported) :
        IChatAgentDispatcher
    {
        public string? RequestId { get; private set; }

        public async Task<ChatSynthesisOutcome> GenerateAsync(
            ChatSynthesisRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestId = request.RequestId;
            var streamRequest = new ChatProviderStreamRequest(
                request.RequestId,
                "primary",
                provider.Id.ToString("D"),
                "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
                "question",
                request.EvidenceFingerprint,
                new string('b', 64),
                request.AbsoluteDeadlineUnixTimeMilliseconds);
            await foreach (ChatProviderStreamSignal _ in transport.StreamAsync(
                streamRequest,
                cancellationToken))
            {
            }

            ChatSynthesisResult synthesis = ChatSynthesisResult.Create(
                request.RequestId,
                request.Mode,
                request.EvidenceFingerprint,
                answerability,
                answerability == ChatAnswerability.Supported
                    ? [new ChatSynthesisSelection("unknown", 1)]
                    : []);
            string rawSelectionJson = answerability == ChatAnswerability.Unsupported
                ? $$"""
                    {
                      "schemaVersion": "{{ChatSynthesisResult.SupportedSchemaVersion}}",
                      "requestId": "{{request.RequestId}}",
                      "mode": "{{(request.Mode == ChatMode.Business ? "business" : "it")}}",
                      "evidenceFingerprint": "{{request.EvidenceFingerprint}}",
                      "answerability": "unsupported",
                      "selections": []
                    }
                    """
                : streamedAnswer;
            return ChatSynthesisOutcome.Generated(synthesis, rawSelectionJson);
        }
    }

    private sealed class SemanticRejectionResponseHandler(string answer) : HttpMessageHandler
    {
        private readonly string _body = CreateSseBody(answer);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "text/event-stream"),
            });

        private static string CreateSseBody(string answer)
        {
            string json = JsonSerializer.Serialize(new
            {
                id = "chatcmpl-story-125-semantic",
                choices = new[]
                {
                    new
                    {
                        delta = new { content = answer },
                        finish_reason = "stop",
                    },
                },
            });
            return $"data: {json}\n\ndata: [DONE]\n\n";
        }
    }

    private sealed class SingleProviderRepository(LlmProvider provider) : ILlmProviderRepository
    {
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LlmProvider>>([provider]);

        public Task<LlmProvider?> GetByIdAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<LlmProvider?>(providerId == provider.Id ? provider : null);

        public Task<bool> NameExistsAsync(string name, Guid? excludeProviderId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LlmProvider> AddAsync(LlmProvider value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Remove(LlmProvider value) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class PlaintextSecretEncryptionService : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(plain);

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default) =>
            Task.FromResult("plaintext-chat-key");
    }

    private sealed class StubDispatcher : IChatAgentDispatcher
    {
        public int CallCount { get; private set; }
        public ChatContractFailureCode? FailureCode { get; set; }
        public ChatAnswerability Answerability { get; set; } = ChatAnswerability.Supported;
        public ChatSynthesisRequest? LastRequest { get; private set; }
        public string? RawSelectionJsonOverride { get; set; }
        public string? NarrativeText { get; set; }
        public List<string>? Trace { get; set; }
        public Action? BeforeResult { get; set; }

        public Task<ChatSynthesisOutcome> GenerateAsync(ChatSynthesisRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Trace?.Add("dispatch");
            LastRequest = request;
            if (FailureCode is ChatContractFailureCode failureCode)
            {
                return Task.FromResult(ChatSynthesisOutcome.Failed(failureCode));
            }

            BeforeResult?.Invoke();

            ChatSynthesisResult result = ChatSynthesisResult.Create(
                request.RequestId,
                request.Mode,
                request.EvidenceFingerprint,
                Answerability,
                Answerability == ChatAnswerability.Supported
                    ? [new ChatSynthesisSelection("k1", 1, NarrativeText)]
                    : []);
            string rawSelectionJson = RawSelectionJsonOverride ?? JsonSerializer.Serialize(new
            {
                schemaVersion = ChatSynthesisResult.SupportedSchemaVersion,
                requestId = request.RequestId,
                mode = request.Mode == ChatMode.Business ? "business" : "it",
                evidenceFingerprint = request.EvidenceFingerprint,
                answerability = Answerability == ChatAnswerability.Supported ? "supported" : "unsupported",
                selections = Answerability == ChatAnswerability.Supported
                    ? NarrativeText is null
                        ? new object[] { new { candidateHandle = "k1", order = 1 } }
                        : [new { candidateHandle = "k1", order = 1, narrativeText = NarrativeText }]
                    : Array.Empty<object>(),
            });
            return Task.FromResult(ChatSynthesisOutcome.Generated(result, rawSelectionJson));
        }
    }

    private sealed class StubCurrentnessValidator : IChatEvidenceCurrentnessValidator
    {
        public int CallCount { get; private set; }
        public ChatCurrentnessValidationOutcome Outcome { get; set; } = ChatCurrentnessValidationOutcome.Current;
        public ChatCurrentnessValidationRequest? LastRequest { get; private set; }
        public List<string>? Trace { get; set; }
        public Action? BeforeResult { get; set; }
        public bool CancellationObservedAfterBeforeResult { get; private set; }

        public Task<ChatCurrentnessValidationOutcome> ValidateCurrentAsync(ChatCurrentnessValidationRequest request, RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Trace?.Add("currentness");
            LastRequest = request;
            if (BeforeResult is not null)
            {
                BeforeResult();
                CancellationObservedAfterBeforeResult = cancellationToken.IsCancellationRequested;
            }
            return Task.FromResult(Outcome);
        }
    }

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}

using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
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
            new StubEvidenceRetriever(),
            candidateExtractor,
            new StubDispatcher(),
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());

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
            new StubEvidenceRetriever(),
            candidateExtractor,
            new StubDispatcher(),
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(preparedSystem.Id),
            new PreparedChatQuestionRequestValidator());

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
            retriever,
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());

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
        Assert.Equal("客户 完成付款？", retriever.LastRequest?.Question);
        Assert.Equal(2, authorization.CallCount);
        Assert.Equal(1, retriever.CallCount);
        Assert.Equal(1, dispatcher.CallCount);
        CandidateClaim dispatchedCandidate = Assert.Single(dispatcher.LastRequest?.CandidateClaims ?? []);
        Assert.Equal("客户提交付款后，系统完成校验。", dispatchedCandidate.Text);
        Assert.Equal(1, currentness.CallCount);
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
            retriever,
            candidateExtractor,
            dispatcher,
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());

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
            new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());

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
            new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());

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
            retriever,
            candidateExtractor,
            dispatcher,
            currentness,
            new RecordingTimeProvider(trace),
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());

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
            "接口如何实现？");

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
            retriever,
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());
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
            retriever,
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());
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
            retriever,
            candidateExtractor,
            dispatcher,
            currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());

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
            new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher,
            new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());

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
            "接口如何实现？");

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
        StubDispatcher? dispatcher = null,
        StubCurrentnessValidator? currentness = null)
    {
        var candidateExtractor = new ChatCandidateExtractor();
        return new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([system]),
            new StubRepositoryRepository(repositories),
            authorization ?? new StubAuthorizationVerifier(),
            retriever ?? new StubEvidenceRetriever(),
            candidateExtractor,
            dispatcher ?? new StubDispatcher(),
            currentness ?? new StubCurrentnessValidator(),
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator());
    }

    private sealed class RecordingTimeProvider(List<string> trace) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            trace.Add("citation-materialization");
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
        public ChatQuestionRequest? LastRequest { get; private set; }
        public List<string>? Trace { get; set; }
        public ChatEvidenceResultKind? ForcedResultKind { get; set; }
        public DocumentType? PrimaryDocumentTypeOverride { get; set; }
        public string PrimaryContent { get; set; } = "客户提交付款后，系统完成校验。";
        public bool IncludeRelationshipSupport { get; set; } = true;

        public Task<ChatEvidenceResult> RetrieveAsync(ChatQuestionRequest request, RepositoryAccessScope accessScope, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Trace?.Add("retrieval");
            LastRequest = request;
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
                            "edge-r1",
                            ChatRelationshipType.References,
                            "d1",
                            "d2",
                            new string('d', 64))
                    ]
                    : [],
                [new EvidenceFlowStep(1, "d1", null, "准备回答。")]);
            return Task.FromResult(ChatEvidenceResult.Found(packet));
        }
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

        public Task<ChatSynthesisOutcome> GenerateAsync(ChatSynthesisRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Trace?.Add("dispatch");
            LastRequest = request;
            if (FailureCode is ChatContractFailureCode failureCode)
            {
                return Task.FromResult(ChatSynthesisOutcome.Failed(failureCode));
            }

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

        public Task<ChatCurrentnessValidationOutcome> ValidateCurrentAsync(ChatCurrentnessValidationRequest request, RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Trace?.Add("currentness");
            LastRequest = request;
            return Task.FromResult(Outcome);
        }
    }

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}

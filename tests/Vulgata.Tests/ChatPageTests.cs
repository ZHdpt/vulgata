using System.Security.Claims;
using System.Reflection;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Web.Components.Pages;
using Vulgata.Web.Data;
using Vulgata.Shared.Validators.Chat;
using Vulgata.Shared.Chat;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

public sealed class ChatPageTests : BunitContext
{
    private readonly SystemEntity _system;
    private readonly Repository _firstRepository;
    private readonly Repository _secondRepository;
    private readonly StubAuthorizationVerifier _authorization = new();
    private readonly StubEvidenceRetriever _retriever = new();
    private readonly StubDispatcher _dispatcher = new();
    private readonly StubCurrentnessValidator _currentness = new();

    public ChatPageTests()
    {
        Services.AddFluentUIComponents();
        Services.AddSingleton<IValidator<PreparedChatQuestionRequest>,
            PreparedChatQuestionRequestValidator>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        _firstRepository = _system.AddRepository(
            "账户仓库",
            "https://example.test/account.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        _secondRepository = _system.AddRepository(
            "渠道仓库",
            "https://example.test/channel.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        var candidateExtractor = new ChatCandidateExtractor();
        Services.AddSingleton(new PreparedChatCoordinator(
            new FixedAuthenticationStateProvider("owner-1"),
            new StubSystemRepository([_system]),
            new StubRepositoryRepository([_firstRepository, _secondRepository]),
            _authorization,
            _retriever,
            candidateExtractor,
            _dispatcher,
            _currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator()));
    }

    [Fact]
    public void ChatPage_InitialRender_RequiresExplicitPreparedSelectionsAndExcludesDeferredControls()
    {
        Assert.NotNull(typeof(ChatPage).GetCustomAttribute<AuthorizeAttribute>());

        IRenderedComponent<ChatPage> cut = Render<ChatPage>();

        Assert.Contains("业务模式", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("技术模式", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("false", FindButton(cut, "业务模式").GetAttribute("aria-pressed"));
        Assert.Equal("false", FindButton(cut, "技术模式").GetAttribute("aria-pressed"));
        Assert.NotNull(cut.Find("#chat-system"));
        Assert.NotNull(cut.Find("#chat-repository-first"));
        Assert.NotNull(cut.Find("#chat-repository-second"));
        Assert.Equal(2, cut.FindAll("select[id^='chat-repository-']").Count);
        Assert.NotNull(cut.Find("#chat-question"));
        Assert.DoesNotContain("type=\"file\"", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("自动选择", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("对话历史", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("知识图谱", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("实时通知", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatPage_MissingCoordinatorRegistration_FailsRequiredComposition()
    {
        using var context = new BunitContext();
        context.Services.AddFluentUIComponents();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => context.Render<ChatPage>());

        Assert.Contains(nameof(PreparedChatCoordinator), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatPage_LoadedPreparedScopes_RendersSystemAndRepositoryOptions()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();

        cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
        cut.Find("#chat-system").Change(_system.Id.ToString());

        Assert.Equal("Loaded", cut.Find(".chat-page").GetAttribute("data-load-state"));
        Assert.Contains("账户仓库", cut.Find("#chat-repository-first").TextContent, StringComparison.Ordinal);
        Assert.Contains("渠道仓库", cut.Find("#chat-repository-first").TextContent, StringComparison.Ordinal);
        Assert.Contains("账户仓库", cut.Find("#chat-repository-second").TextContent, StringComparison.Ordinal);
        Assert.Contains("渠道仓库", cut.Find("#chat-repository-second").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatPage_ModeSelection_LocksThemeAndStartsFreshTransientQuestion()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
        IElement question = cut.Find("#chat-question");
        question.Input("尚未发送的问题");

        FindButton(cut, "技术模式").Click();

        Assert.Equal("dark", cut.Find(".chat-page").GetAttribute("data-theme"));
        Assert.Contains("chat-page--it", cut.Find(".chat-page").ClassList);
        Assert.Equal("true", FindButton(cut, "技术模式").GetAttribute("aria-pressed"));
        Assert.Equal(string.Empty, cut.Find("#chat-question").GetAttribute("value"));
        Assert.Contains("你好，需要追踪哪段逻辑？", cut.Markup, StringComparison.Ordinal);

        FindButton(cut, "业务模式").Click();

        Assert.Equal("light", cut.Find(".chat-page").GetAttribute("data-theme"));
        Assert.Contains("chat-page--business", cut.Find(".chat-page").ClassList);
        Assert.Equal("true", FindButton(cut, "业务模式").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task ChatPage_EnterWithExplicitValidRequest_DispatchesAndRendersChineseAnswer()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));

        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Contains(
            "请选择业务模式、系统和两个不同的仓库，并输入 1 至 2000 个字符的问题。",
            cut.Markup,
            StringComparison.Ordinal);
        Assert.Equal(0, _dispatcher.CallCount);

        PrepareValidSelection(cut, ChatMode.Business);
        cut.Find("#chat-question").Input("客户如何完成付款？");
        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForState(() => cut.Markup.Contains("客户提交付款后，系统完成校验。", StringComparison.Ordinal));
        Assert.Equal(1, _retriever.CallCount);
        Assert.Equal(1, _dispatcher.CallCount);
        Assert.Equal("Loaded", cut.Find(".chat-page").GetAttribute("data-load-state"));
        Assert.DoesNotContain("暂时无法生成回答", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChatPage_BusinessAnswer_RendersChineseDocumentCitationWithExpandableAuthorizedLocatorAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
        PrepareValidSelection(cut, ChatMode.Business);

        await SendQuestionAsync(cut, "客户如何完成付款？");

        IElement claim = cut.Find(".material-claim");
        Assert.Contains("客户提交付款后，系统完成校验。", claim.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("src/PaymentFlow.cs", claim.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("20", claim.TextContent, StringComparison.Ordinal);

        IElement citation = cut.Find(".document-citation");
        Assert.Contains("文档引用", citation.TextContent, StringComparison.Ordinal);
        Assert.Contains("业务流程", citation.TextContent, StringComparison.Ordinal);
        Assert.Contains("查看来源", citation.QuerySelector("summary")?.TextContent, StringComparison.Ordinal);
        IElement expandableLocator = citation.QuerySelector(".citation-locator")!;
        Assert.NotNull(expandableLocator);
        Assert.True(
            expandableLocator.TextContent.Contains("账户仓库", StringComparison.Ordinal) ||
            expandableLocator.TextContent.Contains("渠道仓库", StringComparison.Ordinal));
        Assert.Contains("src/PaymentFlow.cs", expandableLocator.TextContent, StringComparison.Ordinal);
        Assert.Contains("第 20–24 行", expandableLocator.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(_firstRepository.Id.ToString(), citation.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.test", citation.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatPage_ItAnswer_RendersChineseDocumentCitationAndAuthorizedLocatorInlineAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
        PrepareValidSelection(cut, ChatMode.It);

        await SendQuestionAsync(cut, "付款流程如何实现？");

        IElement citation = cut.Find(".document-citation");
        Assert.Contains("文档引用", citation.TextContent, StringComparison.Ordinal);
        Assert.Contains("技术流程", citation.TextContent, StringComparison.Ordinal);
        IElement inlineLocator = citation.QuerySelector(".citation-locator--inline")!;
        Assert.NotNull(inlineLocator);
        Assert.True(
            inlineLocator.TextContent.Contains("账户仓库", StringComparison.Ordinal) ||
            inlineLocator.TextContent.Contains("渠道仓库", StringComparison.Ordinal));
        Assert.Contains("src/PaymentFlow.cs", inlineLocator.TextContent, StringComparison.Ordinal);
        Assert.Contains("第 20–24 行", inlineLocator.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(_firstRepository.Id.ToString(), citation.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.test", citation.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatPage_EscapeDuringPendingRequest_CancelsWithoutPartialAnswer()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
        PrepareValidSelection(cut, ChatMode.Business);
        cut.Find("#chat-question").Input("客户如何完成付款？");
        _dispatcher.BlockNextCall();

        Task sendTask = cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        await _dispatcher.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        cut.WaitForState(() => cut.Find(".chat-page").GetAttribute("data-load-state") == "Loading");

        Assert.Contains("正在查找相关文档…", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("客户提交付款后，系统完成校验。", cut.Markup, StringComparison.Ordinal);
        Assert.Null(cut.Find("#chat-question").GetAttribute("disabled"));

        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("Cancelling", cut.Find(".chat-page").GetAttribute("data-load-state"));
        Assert.Contains("正在取消本次提问…", cut.Markup, StringComparison.Ordinal);
        _dispatcher.ReleaseBlockedCall();
        await sendTask;

        Assert.Equal("Loaded", cut.Find(".chat-page").GetAttribute("data-load-state"));
        Assert.Contains("已取消本次提问。", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("客户提交付款后，系统完成校验。", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("complete-answer", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChatPage_NewRequest_ReplacesAnswerOnlyAfterCompleteResult()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
        PrepareValidSelection(cut, ChatMode.Business);
        _retriever.ClaimText = "第一份完整回答。";
        await SendQuestionAsync(cut, "第一个问题");
        Assert.Contains("第一份完整回答。", cut.Markup, StringComparison.Ordinal);

        _retriever.ClaimText = "第二份完整回答。";
        _dispatcher.BlockNextCall();
        cut.Find("#chat-question").Input("第二个问题");
        Task sendTask = cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        await _dispatcher.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        cut.WaitForState(() => cut.Find(".chat-page").GetAttribute("data-load-state") == "Loading");

        Assert.DoesNotContain("第一份完整回答。", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("第二份完整回答。", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("complete-answer", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".document-citation"));

        _dispatcher.ReleaseBlockedCall();
        await sendTask;

        Assert.DoesNotContain("第一份完整回答。", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("第二份完整回答。", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll(".complete-answer"));
    }

    [Fact]
    public async Task ChatPage_ModeScopeAndFailureChanges_ClearPriorTransientAnswer()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
        PrepareValidSelection(cut, ChatMode.Business);
        _retriever.ClaimText = "不应保留的旧回答。";
        await SendQuestionAsync(cut, "第一个问题");
        Assert.Contains("不应保留的旧回答。", cut.Markup, StringComparison.Ordinal);

        FindButton(cut, "技术模式").Click();

        Assert.DoesNotContain("不应保留的旧回答。", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(string.Empty, cut.Find("#chat-question").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".document-citation"));

        _retriever.ClaimText = "技术模式回答。";
        await SendQuestionAsync(cut, "第二个问题");
        Assert.Contains("技术模式回答。", cut.Markup, StringComparison.Ordinal);

        cut.Find("#chat-system").Change(string.Empty);

        Assert.DoesNotContain("技术模式回答。", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("complete-answer", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".document-citation"));

        PrepareValidSelection(cut, ChatMode.Business);
        _retriever.ClaimText = "失败前回答。";
        await SendQuestionAsync(cut, "第三个问题");
        _dispatcher.FailureCode = ChatContractFailureCode.ProviderFailure;
        await SendQuestionAsync(cut, "会失败的问题");

        Assert.Equal("Error", cut.Find(".chat-page").GetAttribute("data-load-state"));
        Assert.Contains("暂时无法生成回答，请稍后重试。", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("失败前回答。", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("complete-answer", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".document-citation"));
    }

    [Fact]
    public async Task ChatPage_StaleRevokedAndInvalidRequests_ClearAllPriorClaimsAndCitationsAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
        PrepareValidSelection(cut, ChatMode.Business);

        await SendQuestionAsync(cut, "第一问");
        Assert.Single(cut.FindAll(".complete-answer"));
        Assert.Single(cut.FindAll(".material-claim"));
        Assert.Single(cut.FindAll(".document-citation"));

        _currentness.Outcome = ChatCurrentnessValidationOutcome.MissingOrStale;
        await SendQuestionAsync(cut, "证据是否仍然有效？");

        IElement surface = cut.Find(".chat-surface");
        Assert.Contains("当前知识证据不可用或已更新，请稍后重试。", surface.TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".complete-answer"));
        Assert.Empty(cut.FindAll(".material-claim"));
        Assert.Empty(cut.FindAll(".document-citation"));
        Assert.DoesNotContain("客户提交付款后，系统完成校验。", surface.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("src/PaymentFlow.cs", surface.TextContent, StringComparison.Ordinal);

        _currentness.Outcome = ChatCurrentnessValidationOutcome.Current;
        await SendQuestionAsync(cut, "第二问");
        Assert.Single(cut.FindAll(".document-citation"));
        _authorization.DenyOnCall = _authorization.CallCount + 2;
        await SendQuestionAsync(cut, "权限是否仍然有效？");

        Assert.Contains("未找到可用的知识范围，或您无权访问。", surface.TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".complete-answer"));
        Assert.Empty(cut.FindAll(".material-claim"));
        Assert.Empty(cut.FindAll(".document-citation"));
        Assert.DoesNotContain("src/PaymentFlow.cs", surface.TextContent, StringComparison.Ordinal);

        cut.Find("#chat-question").Input(string.Empty);
        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Contains("请选择业务模式、系统和两个不同的仓库", surface.TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".complete-answer"));
        Assert.Empty(cut.FindAll(".material-claim"));
        Assert.Empty(cut.FindAll(".document-citation"));
    }

    [Fact]
    public async Task ChatPage_InvalidQuestionOrDuplicateScope_DoesNotDispatch()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Markup.Contains("支付系统", StringComparison.Ordinal));
        PrepareValidSelection(cut, ChatMode.Business);
        cut.Find("#chat-question").Input(new string('问', 2001));
        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(0, _dispatcher.CallCount);
        Assert.Contains("1 至 2000 个字符", cut.Markup, StringComparison.Ordinal);

        cut.Find("#chat-repository-second").Change(_firstRepository.Id.ToString());
        cut.Find("#chat-question").Input("范围重复");
        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(0, _dispatcher.CallCount);
        Assert.Contains("两个不同的仓库", cut.Markup, StringComparison.Ordinal);

        cut.Find("#chat-question").Input("保留换行");
        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", ShiftKey = true });

        Assert.Equal(0, _dispatcher.CallCount);
        Assert.Equal("保留换行", cut.Find("#chat-question").GetAttribute("value"));
    }

    private void PrepareValidSelection(IRenderedComponent<ChatPage> cut, ChatMode mode)
    {
        FindButton(cut, mode == ChatMode.Business ? "业务模式" : "技术模式").Click();
        cut.Find("#chat-system").Change(_system.Id.ToString());
        cut.Find("#chat-repository-first").Change(_firstRepository.Id.ToString());
        cut.Find("#chat-repository-second").Change(_secondRepository.Id.ToString());
    }

    private static async Task SendQuestionAsync(IRenderedComponent<ChatPage> cut, string question)
    {
        cut.Find("#chat-question").Input(question);
        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
    }

    private static IElement FindButton(IRenderedComponent<ChatPage> cut, string text) =>
        cut.FindAll("button, fluent-button")
            .Single(button => button.TextContent.Contains(text, StringComparison.Ordinal));

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
        public Task<SystemOwnerAssignmentWriteResult> AssignOwnerAsync(Guid systemId, string userId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
        public Task<bool> NameExistsAsync(Guid systemId, string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> StandaloneNameExistsAsync(string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Repository> AddAsync(Repository repository, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Remove(Repository repository) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubAuthorizationVerifier : IRepositoryAuthorizationVerifier
    {
        public int CallCount { get; private set; }
        public int? DenyOnCall { get; set; }

        public ValueTask<bool> HasCurrentAccessAsync(IReadOnlyCollection<Guid> requiredRepositoryIds, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(CallCount != DenyOnCall);
        }
    }

    private sealed class StubEvidenceRetriever : IChatEvidenceRetriever
    {
        public int CallCount { get; private set; }
        public string ClaimText { get; set; } = "客户提交付款后，系统完成校验。";

        public Task<ChatEvidenceResult> RetrieveAsync(
            ChatQuestionRequest request,
            RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Guid firstRepositoryId = request.RepositoryIds[0];
            Guid secondRepositoryId = request.RepositoryIds[1];
            EvidenceDocument primaryDocument = new(
                "d1",
                Id(101),
                Id(201),
                firstRepositoryId,
                request.Mode == ChatMode.Business ? DocumentType.BusinessLogic : DocumentType.CodeLogic,
                request.Mode == ChatMode.Business ? "业务流程" : "技术流程",
                new string('a', 40));
            EvidenceDocument relatedDocument = new(
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
                [primaryDocument, relatedDocument],
                [new EvidenceSection(
                    "s1",
                    "d1",
                    Id(301),
                    1,
                    DocumentSectionKind.Overview,
                    "流程概览",
                    ClaimText,
                    ["a1"],
                    ["d2"])],
                [new EvidenceAnchor(
                    "a1",
                    Id(401),
                    "d1",
                    primaryDocument.DocumentVersionId,
                    firstRepositoryId,
                    "src/PaymentFlow.cs",
                    20,
                    24,
                    new string('c', 64))],
                [new EvidenceRelationship(
                    "r1",
                    "edge-r1",
                    ChatRelationshipType.References,
                    "d1",
                    "d2",
                    new string('d', 64))],
                [new EvidenceFlowStep(1, "d1", null, "准备回答。")]);
            return Task.FromResult(ChatEvidenceResult.Found(packet));
        }
    }

    private sealed class StubDispatcher : IChatAgentDispatcher
    {
        private TaskCompletionSource<bool>? _entered;
        private TaskCompletionSource<bool>? _release;

        public int CallCount { get; private set; }
        public ChatContractFailureCode? FailureCode { get; set; }
        public Task Entered => _entered?.Task ?? Task.CompletedTask;

        public void BlockNextCall()
        {
            _entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void ReleaseBlockedCall() => _release?.TrySetResult(true);

        public async Task<ChatSynthesisOutcome> GenerateAsync(
            ChatSynthesisRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_release is not null)
            {
                _entered?.TrySetResult(true);
                await _release.Task.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                _entered = null;
                _release = null;
            }

            if (FailureCode is ChatContractFailureCode failureCode)
            {
                return ChatSynthesisOutcome.Failed(failureCode);
            }

            ChatSynthesisResult result = ChatSynthesisResult.Create(
                request.RequestId,
                request.Mode,
                request.EvidenceFingerprint,
                [new ChatSynthesisSelection(request.CandidateClaims[0].CandidateHandle, 1)]);
            string rawSelectionJson = JsonSerializer.Serialize(new
            {
                schemaVersion = ChatSynthesisResult.SupportedSchemaVersion,
                requestId = request.RequestId,
                mode = request.Mode == ChatMode.Business ? "business" : "it",
                evidenceFingerprint = request.EvidenceFingerprint,
                selections = new[]
                {
                    new
                    {
                        candidateHandle = request.CandidateClaims[0].CandidateHandle,
                        order = 1,
                    },
                },
            });
            return ChatSynthesisOutcome.Generated(result, rawSelectionJson);
        }
    }

    private sealed class StubCurrentnessValidator : IChatEvidenceCurrentnessValidator
    {
        public ChatCurrentnessValidationOutcome Outcome { get; set; } = ChatCurrentnessValidationOutcome.Current;

        public Task<ChatCurrentnessValidationOutcome> ValidateCurrentAsync(
            ChatCurrentnessValidationRequest request,
            RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Outcome);
    }

    private sealed class StubPreparedChatScopeResolver : IPreparedChatScopeResolver
    {
        public Task<PreparedChatScopeBinding?> ResolveAsync(
            Guid systemId,
            IReadOnlyCollection<Guid> repositoryIds,
            RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default)
        {
            Guid[] ids = repositoryIds.Distinct().ToArray();
            return Task.FromResult<PreparedChatScopeBinding?>(
                ids.Length == 2
                    ? new PreparedChatScopeBinding(systemId, ids[0], ids[1])
                    : null);
        }
    }

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}

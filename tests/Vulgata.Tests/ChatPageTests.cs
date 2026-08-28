using System.Security.Claims;
using System.Reflection;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Bunit;
using Bunit.JSInterop;
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
    private readonly ManualRevealDelay _revealDelay = new();
    private readonly AdjustableTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);
    private readonly BunitJSModuleInterop _chatModule;

    public ChatPageTests()
    {
        Services.AddFluentUIComponents();
        Services.AddSingleton<IValidator<PreparedChatQuestionRequest>,
            PreparedChatQuestionRequestValidator>();
        Services.AddSingleton<IChatRevealDelay>(_revealDelay);
        JSInterop.Mode = JSRuntimeMode.Loose;
        _chatModule = JSInterop.SetupModule("./Components/Pages/ChatPage.razor.js");
        _chatModule.Setup<bool>("prefersReducedMotion").SetResult(false);

        _system = new SystemEntity("DemoBank", null, null, DateTimeOffset.UtcNow);
        _firstRepository = _system.AddRepository(
            "mbs_account",
            "https://example.test/account.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        _secondRepository = _system.AddRepository(
            "mbs_channel",
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
            new CanonicalEmptyAdvisoryContextReader(),
            _retriever,
            candidateExtractor,
            _dispatcher,
            _currentness,
            _timeProvider,
            new ChatAgentResultValidator(candidateExtractor),
            new StubPreparedChatScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            new CapturingCompletionSummaryRecorder()));
    }

    [Fact]
    public void ChatPage_InitialRender_DefaultsToBusinessLightThemeAndHidesRepositorySelection()
    {
        Assert.NotNull(typeof(ChatPage).GetCustomAttribute<AuthorizeAttribute>());

        IRenderedComponent<ChatPage> cut = Render<ChatPage>();

        Assert.Contains("业务模式", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("技术模式", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("true", FindButton(cut, "业务模式").GetAttribute("aria-pressed"));
        Assert.Equal("false", FindButton(cut, "技术模式").GetAttribute("aria-pressed"));
        Assert.Equal("light", cut.Find(".chat-page").GetAttribute("data-theme"));
        Assert.Contains("chat-page--business", cut.Find(".chat-page").ClassList);
        Assert.Contains("你好，想了解什么业务流程？", cut.Markup, StringComparison.Ordinal);
        cut.WaitForState(() => cut.Find(".chat-page").GetAttribute("data-load-state") == "Loaded");
        Assert.Empty(cut.FindAll(".scope-selector"));
        Assert.Empty(cut.FindAll("#chat-system, #chat-repository-first, #chat-repository-second"));
        Assert.Equal("选择仓库", cut.Find(".repository-picker__trigger").GetAttribute("aria-label"));
        Assert.Equal("false", cut.Find(".repository-picker__trigger").GetAttribute("aria-expanded"));
        Assert.Equal("BUTTON", cut.Find(".repository-picker__trigger").TagName);
        Assert.Empty(cut.FindAll(".repository-chip"));
        IElement repositoryIcon = cut.Find(".repository-picker__trigger svg");
        Assert.Equal("0 0 1024 1024", repositoryIcon.GetAttribute("viewBox"));
        Assert.Equal("true", repositoryIcon.GetAttribute("aria-hidden"));
        Assert.Equal(2, repositoryIcon.QuerySelectorAll("path").Length);
        Assert.NotNull(cut.Find("#chat-question"));
        Assert.DoesNotContain("type=\"file\"", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("自动选择", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("对话历史", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("知识图谱", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("实时通知", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatPage_Render_ExposesDedicatedAccessibleConversationScrollRegion()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();

        IElement form = cut.Find("form");
        IElement surface = cut.Find(".chat-surface");
        Assert.Contains("chat-page__form", form.ClassList);
        Assert.Equal("对话记录", surface.GetAttribute("aria-label"));
        Assert.Equal("0", surface.GetAttribute("tabindex"));
    }

    [Fact]
    public async Task ChatPage_NewConversation_ClearsTransientStateAndPreservesPreparedContextAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Find(".chat-page").GetAttribute("data-load-state") == "Loaded");
        PrepareValidSelection(cut, ChatMode.Business);
        await SendQuestionAsync(cut, "客户如何完成付款？");
        cut.Find("#chat-question").Input("尚未发送的追问");

        await FindButton(cut, "新对话").ClickAsync(new MouseEventArgs());

        Assert.DoesNotContain("客户如何完成付款？", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("客户提交付款后，系统完成校验。", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".complete-answer"));
        Assert.Empty(cut.FindAll(".document-citation"));
        Assert.Equal(string.Empty, cut.Find("#chat-question").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".repository-chip"));
        Assert.Equal("true", FindButton(cut, "业务模式").GetAttribute("aria-pressed"));
        Assert.Contains("新对话已开始", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("你好，想了解什么业务流程？", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(
            JSInterop.Invocations,
            invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ChatPage_NewConversationDuringRequest_PreventsLateAnswerFromReappearingAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        _dispatcher.BlockNextCall();

        cut.Find("#chat-question").Input("付款问题仍在处理中");
        Task sendTask = cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        await _dispatcher.Entered.WaitAsync(TimeSpan.FromSeconds(5));

        await FindButton(cut, "新对话").ClickAsync(new MouseEventArgs());
        _dispatcher.ReleaseBlockedCall();
        await sendTask;

        Assert.DoesNotContain("付款问题仍在处理中", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("客户提交付款后，系统完成校验。", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("已取消本次提问。", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".complete-answer"));
        Assert.Empty(cut.FindAll(".document-citation"));
        Assert.Contains("新对话已开始", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatPage_MissingCoordinatorRegistration_FailsRequiredComposition()
    {
        using var context = new BunitContext();
        context.Services.AddFluentUIComponents();
        context.Services.AddSingleton<IChatRevealDelay>(new ManualRevealDelay());
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => context.Render<ChatPage>());

        Assert.Contains(nameof(PreparedChatCoordinator), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatPage_RepositoryPicker_OpensDemoBankCatalogAndSynchronizesCheckmarksWithChips()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();

        cut.WaitForState(() => cut.Find(".chat-page").GetAttribute("data-load-state") == "Loaded");
        cut.Find(".repository-picker__trigger").Click();

        Assert.Equal("Loaded", cut.Find(".chat-page").GetAttribute("data-load-state"));
        Assert.Equal("true", cut.Find(".repository-picker__trigger").GetAttribute("aria-expanded"));
        Assert.Equal(
            ["DemoBank"],
            cut.FindAll(".repository-menu__system").Select(system => system.TextContent.Trim().TrimEnd('›').Trim()).ToArray());
        Assert.Equal(
            ["mbs_account", "mbs_channel"],
            cut.FindAll(".repository-menu__repository").Select(repository => repository.TextContent.Trim().TrimStart('✓').Trim()).ToArray());

        IElement accountRepository = cut.Find(".repository-menu__repository[data-repository-name='mbs_account']");
        Assert.Equal("false", accountRepository.GetAttribute("aria-checked"));

        accountRepository.Click();
        Assert.Equal("true", cut.Find(".repository-menu__repository[data-repository-name='mbs_account']").GetAttribute("aria-checked"));
        Assert.NotNull(cut.Find(".repository-chip[data-repository-name='mbs_account']"));

        cut.Find(".repository-menu__repository[data-repository-name='mbs_account']").Click();
        Assert.Equal("false", cut.Find(".repository-menu__repository[data-repository-name='mbs_account']").GetAttribute("aria-checked"));
        Assert.Empty(cut.FindAll(".repository-chip[data-repository-name='mbs_account']"));
    }

    [Fact]
    public async Task ChatPage_RepositoryPicker_HoverFocusEscapeAndOutsideDismissRemainAccessibleAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Find(".chat-page").GetAttribute("data-load-state") == "Loaded");
        cut.Find(".repository-picker__trigger").Click();

        IElement demoBank = cut.Find(".repository-menu__system[data-system-id='demobank']");
        await demoBank.TriggerEventAsync("onmouseenter", new MouseEventArgs());
        await demoBank.TriggerEventAsync("onfocus", EventArgs.Empty);
        Assert.Equal("BUTTON", demoBank.TagName);
        Assert.Equal("BUTTON", cut.Find(".repository-menu__repository[data-repository-name='mbs_account']").TagName);

        await cut.Find(".repository-picker").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll(".repository-menu"));

        cut.Find(".repository-picker__trigger").Click();
        cut.Find(".repository-picker__dismiss").Click();
        Assert.Empty(cut.FindAll(".repository-menu"));
    }

    [Fact]
    public async Task ChatPage_RepositoryPresentationState_NeverChangesFixedDemoBackendScopeAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForState(() => cut.Find(".chat-page").GetAttribute("data-load-state") == "Loaded");

        Assert.Empty(cut.FindAll(".repository-chip"));

        await SendQuestionAsync(cut, "付款仓库如何工作？");

        ChatRetrievalRequest firstRequest = Assert.Single(_retriever.Requests);
        Assert.Equal(_system.Id, firstRequest.SystemId);
        Assert.Equal(
            new[] { _firstRepository.Id, _secondRepository.Id }.Order(),
            firstRequest.RepositoryIds.Order());

        await SendQuestionAsync(cut, "没有可见标签时仍可提问吗？");

        Assert.Equal(2, _retriever.Requests.Count);
        Assert.All(_retriever.Requests, request =>
        {
            Assert.Equal(_system.Id, request.SystemId);
            Assert.Equal(
                new[] { _firstRepository.Id, _secondRepository.Id }.Order(),
                request.RepositoryIds.Order());
        });
    }

    [Fact]
    public void ChatPage_ModeSelection_LocksThemeAndStartsFreshTransientQuestion()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
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
        WaitForLoaded(cut);

        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Contains(
            "请选择业务模式，并输入 1 至 2000 个字符的问题。",
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
    public async Task ChatPage_UnrelatedQuestion_RendersUnsupportedAssistantResponse()
    {
        _dispatcher.Answerability = ChatAnswerability.Unsupported;
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);

        await SendQuestionAsync(cut, "北京天气怎么样？");

        IElement response = cut.Find("[data-answer-state='unsupported']");
        Assert.Equal("当前知识库中没有足够信息回答该问题。", response.TextContent);
        Assert.Contains("北京天气怎么样？", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Empty", cut.Find(".chat-page").GetAttribute("data-load-state"));
        Assert.Empty(cut.FindAll(".document-citation"));
    }

    [Fact]
    public async Task ChatPage_BusinessAnswer_RendersChineseDocumentCitationWithExpandableAuthorizedLocatorAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
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
        IElement chevron = citation.QuerySelector(".citation-chevron")!;
        Assert.Equal(string.Empty, chevron.TextContent);
        Assert.Equal("true", chevron.GetAttribute("aria-hidden"));
        IElement expandableLocator = citation.QuerySelector(".citation-locator")!;
        Assert.NotNull(expandableLocator);
        Assert.True(
            expandableLocator.TextContent.Contains("mbs_account", StringComparison.Ordinal) ||
            expandableLocator.TextContent.Contains("mbs_channel", StringComparison.Ordinal));
        Assert.Contains("src/PaymentFlow.cs", expandableLocator.TextContent, StringComparison.Ordinal);
        Assert.Contains("第 20–24 行", expandableLocator.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(_firstRepository.Id.ToString(), citation.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.test", citation.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatPage_ItAnswer_RendersChineseDocumentCitationAndAuthorizedLocatorInlineAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.It);

        await SendQuestionAsync(cut, "付款流程如何实现？");

        IElement citation = cut.Find(".document-citation");
        Assert.Contains("文档引用", citation.TextContent, StringComparison.Ordinal);
        Assert.Contains("技术流程", citation.TextContent, StringComparison.Ordinal);
        IElement inlineLocator = citation.QuerySelector(".citation-locator--inline")!;
        Assert.NotNull(inlineLocator);
        Assert.True(
            inlineLocator.TextContent.Contains("mbs_account", StringComparison.Ordinal) ||
            inlineLocator.TextContent.Contains("mbs_channel", StringComparison.Ordinal));
        Assert.Contains("src/PaymentFlow.cs", inlineLocator.TextContent, StringComparison.Ordinal);
        Assert.Contains("第 20–24 行", inlineLocator.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(_firstRepository.Id.ToString(), citation.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.test", citation.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatPage_EscapeDuringPendingRequest_CancelsWithoutPartialAnswer()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
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
    public async Task ChatPage_SecondRequest_RetainsFirstTurnWhilePendingAndAfterCompletion()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
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

        Assert.Contains("第一个问题", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("第一份完整回答。", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("第二个问题", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("第二份完整回答。", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll(".complete-answer"));
        Assert.Single(cut.FindAll(".document-citation"));

        _dispatcher.ReleaseBlockedCall();
        await sendTask;

        Assert.Contains("第一份完整回答。", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("第二份完整回答。", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll(".complete-answer").Count);
        Assert.Equal(2, cut.FindAll(".document-citation").Count);
    }

    [Fact]
    public async Task ChatPage_ModeScopeAndFailureChanges_RetainPriorCompletedTurns()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        _retriever.ClaimText = "应保留的旧回答。";
        await SendQuestionAsync(cut, "应保留的旧回答如何产生？");
        Assert.Contains("应保留的旧回答。", cut.Markup, StringComparison.Ordinal);

        FindButton(cut, "技术模式").Click();

        Assert.Contains("应保留的旧回答。", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(string.Empty, cut.Find("#chat-question").GetAttribute("value"));
        Assert.Single(cut.FindAll(".document-citation"));

        _retriever.ClaimText = "技术模式回答。";
        await SendQuestionAsync(cut, "技术模式回答如何产生？");
        Assert.Contains("技术模式回答。", cut.Markup, StringComparison.Ordinal);

        cut.Find(".repository-picker__trigger").Click();
        cut.Find(".repository-menu__repository[data-repository-name='mbs_account']").Click();
        cut.Find(".repository-chip[data-repository-name='mbs_account'] .repository-chip__remove").Click();

        Assert.Contains("技术模式回答。", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll(".complete-answer").Count);
        Assert.Equal(2, cut.FindAll(".document-citation").Count);

        PrepareValidSelection(cut, ChatMode.Business);
        _retriever.ClaimText = "失败前回答。";
        await SendQuestionAsync(cut, "失败前回答如何产生？");
        _dispatcher.FailureCode = ChatContractFailureCode.ProviderFailure;
        await SendQuestionAsync(cut, "回答失败吗？");

        Assert.Equal("Error", cut.Find(".chat-page").GetAttribute("data-load-state"));
        Assert.Contains("暂时无法生成回答，请稍后重试。", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("失败前回答。", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(3, cut.FindAll(".complete-answer").Count);
        Assert.Equal(3, cut.FindAll(".document-citation").Count);
    }

    [Fact]
    public async Task ChatPage_StaleRevokedAndInvalidRequests_RetainPriorClaimsAndCitationsAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);

        await SendQuestionAsync(cut, "付款第一问");
        Assert.Single(cut.FindAll(".complete-answer"));
        Assert.Single(cut.FindAll(".material-claim"));
        Assert.Single(cut.FindAll(".document-citation"));

        _currentness.Outcome = ChatCurrentnessValidationOutcome.MissingOrStale;
        await SendQuestionAsync(cut, "付款证据是否仍然有效？");

        IElement surface = cut.Find(".chat-surface");
        Assert.Contains("当前知识证据不可用或已更新，请稍后重试。", surface.TextContent, StringComparison.Ordinal);
        Assert.Single(cut.FindAll(".complete-answer"));
        Assert.Single(cut.FindAll(".material-claim"));
        Assert.Single(cut.FindAll(".document-citation"));
        Assert.Contains("客户提交付款后，系统完成校验。", surface.TextContent, StringComparison.Ordinal);
        Assert.Contains("src/PaymentFlow.cs", surface.TextContent, StringComparison.Ordinal);

        _currentness.Outcome = ChatCurrentnessValidationOutcome.Current;
        await SendQuestionAsync(cut, "付款第二问");
        Assert.Equal(2, cut.FindAll(".document-citation").Count);
        _authorization.DenyOnCall = _authorization.CallCount + 2;
        await SendQuestionAsync(cut, "付款权限是否仍然有效？");

        Assert.Contains("未找到可用的知识范围，或您无权访问。", surface.TextContent, StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll(".complete-answer").Count);
        Assert.Equal(2, cut.FindAll(".material-claim").Count);
        Assert.Equal(2, cut.FindAll(".document-citation").Count);
        Assert.Contains("src/PaymentFlow.cs", surface.TextContent, StringComparison.Ordinal);

        cut.Find("#chat-question").Input(string.Empty);
        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Contains("请选择业务模式，并输入 1 至 2000 个字符的问题。", surface.TextContent, StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll(".complete-answer").Count);
        Assert.Equal(2, cut.FindAll(".material-claim").Count);
        Assert.Equal(2, cut.FindAll(".document-citation").Count);
    }

    [Fact]
    public async Task ChatPage_RetainedTimeline_DoesNotBecomeBackendConversationContext()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);

        await SendQuestionAsync(cut, "第一条独立问题");
        await SendQuestionAsync(cut, "第二条独立问题");

        Assert.Equal(2, _retriever.Requests.Count);
        Assert.Equal("第一条独立问题", _retriever.Requests[0].Question);
        Assert.Equal("第二条独立问题", _retriever.Requests[1].Question);
        Assert.DoesNotContain("第一条独立问题", _retriever.Requests[1].Question, StringComparison.Ordinal);
        Assert.Equal(2, cut.FindAll("[data-chat-turn]").Count);
    }

    [Fact]
    public async Task ChatPage_SuccessfulAnswer_RevealsFirstElementThenCompletesWithCitations()
    {
        _revealDelay.Pause();
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        _retriever.ClaimText = "账户完成。";

        Task sendTask = BeginQuestionAsync(cut, "如何完成？");
        cut.WaitForState(() => cut.FindAll("[data-answer-state='revealing']").Count == 1);

        IElement article = cut.Find("[data-answer-state='revealing']");
        Assert.Equal("账", cut.Find(".material-claim__revealed").TextContent);
        Assert.Empty(cut.FindAll(".document-citation"));
        Assert.Equal("off", article.GetAttribute("aria-live"));
        Assert.Equal("账户完成。", cut.Find(".material-claim").GetAttribute("aria-label"));
        Assert.Equal("true", cut.Find(".material-claim__revealed").GetAttribute("aria-hidden"));

        _revealDelay.ReleaseAll();
        await sendTask;

        Assert.Equal("账户完成。", cut.Find(".material-claim").TextContent);
        Assert.Single(cut.FindAll(".document-citation"));
        Assert.Equal("complete", cut.Find(".complete-answer").GetAttribute("data-answer-state"));
    }

    [Fact]
    public async Task ChatPage_PendingProviderStream_ExposesNoUnvalidatedContentOrEvidenceToClientAsync()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        _dispatcher.BlockNextCall();

        Task sendTask = BeginQuestionAsync(cut, "提交后应立即清空");
        await _dispatcher.Entered.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(cut.FindAll(".chat-thinking"));
        Assert.Empty(cut.FindAll(".material-claim, .document-citation, .citation-locator, [data-source-anchor], [data-relationship], [data-provider-fragment]"));
        Assert.DoesNotContain("provider-fragment", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("不可信", cut.Markup, StringComparison.Ordinal);

        _dispatcher.ReleaseBlockedCall();
        await sendTask;
    }

    [Fact]
    public async Task ChatPage_AcceptedAnswerWithInsufficientRemainingDeadline_RendersImmediatelyAsync()
    {
        _revealDelay.Pause();
        _dispatcher.BeforeResult = () => _timeProvider.Advance(TimeSpan.FromSeconds(28.5));

        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        _retriever.ClaimText = "截止前必须立即显示。";

        Task sendTask = BeginQuestionAsync(cut, "剩余时间不足时如何显示？");
        cut.WaitForState(() => cut.FindAll("[data-answer-state='revealing'], [data-answer-state='complete']").Count == 1);

        try
        {
            IElement answer = cut.Find(".complete-answer");
            Assert.Equal("complete", answer.GetAttribute("data-answer-state"));
            Assert.Equal("截止前必须立即显示。", cut.Find(".material-claim").TextContent);
            Assert.Single(cut.FindAll(".document-citation"));
            Assert.Equal(0, _revealDelay.CallCount);
        }
        finally
        {
            _revealDelay.ReleaseAll();
            await sendTask;
        }
    }

    [Fact]
    public async Task ChatPage_ValidPendingRequest_ClearsComposerImmediately()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        _dispatcher.BlockNextCall();

        Task sendTask = BeginQuestionAsync(cut, "提交后应立即清空");
        await _dispatcher.Entered.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(string.Empty, ((IHtmlTextAreaElement)cut.Find("#chat-question")).Value);

        _dispatcher.ReleaseBlockedCall();
        await sendTask;
    }

    [Fact]
    public async Task ChatPage_NewMessages_KeepConversationPinnedToLatestContent()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        _dispatcher.BlockNextCall();

        Task sendTask = BeginQuestionAsync(cut, "提交后应显示最新消息");
        await _dispatcher.Entered.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains("提交后应显示最新消息", cut.Find(".chat-surface").TextContent, StringComparison.Ordinal);
        Assert.Contains(
            JSInterop.Invocations,
            invocation => StringComparer.Ordinal.Equals(invocation.Identifier, "scrollConversationToEnd"));

        _dispatcher.ReleaseBlockedCall();
        await sendTask;

        cut.WaitForAssertion(() => Assert.True(CountConversationScrolls() >= 2));
    }

    [Fact]
    public async Task ChatPage_NewConversationDuringReveal_PreventsLateTextAndCitationFromReappearingAsync()
    {
        _revealDelay.Pause();
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        _retriever.ClaimText = "不会重新出现。";

        Task sendTask = BeginQuestionAsync(cut, "不会重新出现的回答如何显示？");
        cut.WaitForState(() => cut.FindAll("[data-answer-state='revealing']").Count == 1);
        await FindButton(cut, "新对话").ClickAsync(new MouseEventArgs());
        _revealDelay.ReleaseAll();
        await sendTask;

        Assert.DoesNotContain("不会重新出现。", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".document-citation"));
        Assert.Empty(cut.FindAll("[data-chat-turn]"));
    }

    [Fact]
    public async Task ChatPage_ProgressiveReveal_DoesNotSplitCombiningMarkOrFamilyEmoji()
    {
        _revealDelay.Pause();
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        _retriever.ClaimText = "e\u0301👨‍👩‍👧‍👦完成";

        Task sendTask = BeginQuestionAsync(cut, "Unicode 测试完成");
        cut.WaitForState(() => cut.FindAll("[data-answer-state='revealing']").Count == 1);
        Assert.Equal("e\u0301", cut.Find(".material-claim__revealed").TextContent);
        Assert.Equal(["e\u0301"], cut.FindAll(".material-claim__element").Select(element => element.TextContent));

        _revealDelay.ReleaseNext();
        cut.WaitForState(() => cut.Find(".material-claim__revealed").TextContent == "e\u0301👨‍👩‍👧‍👦");
        Assert.Equal(
            ["e\u0301", "👨‍👩‍👧‍👦"],
            cut.FindAll(".material-claim__element").Select(element => element.TextContent));

        _revealDelay.ReleaseAll();
        await sendTask;
        Assert.Equal("e\u0301👨‍👩‍👧‍👦完成", cut.Find(".material-claim").TextContent);
    }

    [Fact]
    public async Task ChatPage_ReducedMotion_PublishesCompleteAnswerWithoutDelay()
    {
        _chatModule.Setup<bool>("prefersReducedMotion").SetResult(true);
        _revealDelay.Pause();
        int delayCalls = _revealDelay.CallCount;
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        _retriever.ClaimText = "立即完成。";

        await SendQuestionAsync(cut, "立即完成的回答");

        Assert.Equal("立即完成。", cut.Find(".material-claim").TextContent);
        Assert.Single(cut.FindAll(".document-citation"));
        Assert.Equal(delayCalls, _revealDelay.CallCount);
    }

    [Fact]
    public async Task ChatPage_InvalidQuestionAndShiftEnter_DoNotDispatch()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        WaitForLoaded(cut);
        PrepareValidSelection(cut, ChatMode.Business);
        cut.Find("#chat-question").Input(new string('问', 2001));
        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(0, _dispatcher.CallCount);
        Assert.Contains("1 至 2000 个字符", cut.Markup, StringComparison.Ordinal);

        cut.Find("#chat-question").Input("保留换行");
        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", ShiftKey = true });

        Assert.Equal(0, _dispatcher.CallCount);
        Assert.Equal("保留换行", cut.Find("#chat-question").GetAttribute("value"));
    }

    private void PrepareValidSelection(IRenderedComponent<ChatPage> cut, ChatMode mode)
    {
        FindButton(cut, mode == ChatMode.Business ? "业务模式" : "技术模式").Click();
    }

    private static void WaitForLoaded(IRenderedComponent<ChatPage> cut)
    {
        cut.WaitForState(() => cut.Find(".chat-page").GetAttribute("data-load-state") == "Loaded");
    }

    private static async Task SendQuestionAsync(IRenderedComponent<ChatPage> cut, string question)
    {
        cut.Find("#chat-question").Input(question);
        await cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
    }

    private static Task BeginQuestionAsync(IRenderedComponent<ChatPage> cut, string question)
    {
        cut.Find("#chat-question").Input(question);
        return cut.Find("#chat-question").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
    }

    private int CountConversationScrolls() => JSInterop.Invocations.Count(
        invocation => StringComparer.Ordinal.Equals(invocation.Identifier, "scrollConversationToEnd"));

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
        public List<ChatRetrievalRequest> Requests { get; } = [];

        public Task<ChatEvidenceResult> RetrieveAsync(
            ChatRetrievalRequest request,
            RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Requests.Add(request);
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
                    Id(410),
                    ChatRelationshipType.References,
                    "d1",
                    "d2",
                    1,
                    Id(411),
                    1,
                    new string('d', 64),
                    null,
                    null)],
                []);
            return Task.FromResult(ChatEvidenceResult.Found(packet));
        }
    }

    private sealed class ManualRevealDelay : IChatRevealDelay
    {
        private readonly Queue<TaskCompletionSource> _pending = new();
        private bool _releaseImmediately = true;

        public int CallCount { get; private set; }

        public Task DelayAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            if (_releaseImmediately)
            {
                return Task.CompletedTask;
            }

            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => source.TrySetCanceled(cancellationToken));
            _pending.Enqueue(source);
            return source.Task;
        }

        public void Pause() => _releaseImmediately = false;

        public void ReleaseNext() => _pending.Dequeue().TrySetResult();

        public void ReleaseAll()
        {
            _releaseImmediately = true;
            while (_pending.TryDequeue(out TaskCompletionSource? source))
            {
                source.TrySetResult();
            }
        }
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class StubDispatcher : IChatAgentDispatcher
    {
        private TaskCompletionSource<bool>? _entered;
        private TaskCompletionSource<bool>? _release;

        public int CallCount { get; private set; }
        public ChatContractFailureCode? FailureCode { get; set; }
        public ChatAnswerability Answerability { get; set; } = ChatAnswerability.Supported;
        public Action? BeforeResult { get; set; }
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

            BeforeResult?.Invoke();

            ChatSynthesisResult result = ChatSynthesisResult.Create(
                request.RequestId,
                request.Mode,
                request.EvidenceFingerprint,
                Answerability,
                Answerability == ChatAnswerability.Supported
                    ? [new ChatSynthesisSelection(request.CandidateClaims[0].CandidateHandle, 1)]
                    : []);
            string rawSelectionJson = JsonSerializer.Serialize(new
            {
                schemaVersion = ChatSynthesisResult.SupportedSchemaVersion,
                requestId = request.RequestId,
                mode = request.Mode == ChatMode.Business ? "business" : "it",
                evidenceFingerprint = request.EvidenceFingerprint,
                answerability = Answerability == ChatAnswerability.Supported ? "supported" : "unsupported",
                selections = Answerability == ChatAnswerability.Supported
                    ? new object[]
                    {
                        new
                        {
                            candidateHandle = request.CandidateClaims[0].CandidateHandle,
                            order = 1,
                        },
                    }
                    : Array.Empty<object>(),
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

    private sealed class CapturingCompletionSummaryRecorder :
        IChatStreamCompletionSummaryRecorder
    {
        public List<ChatStreamCompletionSummary> Summaries { get; } = [];

        public IReadOnlyList<string> GetRuntimeObservationIds(string requestId) => [];

        public void RecordCompletion(ChatStreamCompletionSummary summary) =>
            Summaries.Add(summary);
    }

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}

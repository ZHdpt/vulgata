using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Components.Pages;
using Vulgata.Web.Services.OctoberChat;
using Vulgata.Web.ViewModels.OctoberChat;

namespace Vulgata.Tests.October.Chat;

public sealed class OctoberChatEntryTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeMode.Business, "light", "业务模式")]
    [InlineData(KnowledgeMode.IT, "dark", "技术模式")]
    public async Task InitializeAsync_SelectedMode_ShowsChineseThemeAndLoadedStateAsync(KnowledgeMode mode, string theme, string label)
    {
        var fixture = new ControlledChatFixture();
        var model = CreateModel(fixture);

        await model.InitializeAsync(fixture.Selection(mode));

        Assert.Equal(LoadState.Loaded, model.LoadState);
        Assert.Equal(theme, model.ThemeName);
        Assert.Equal(label, model.ModeLabel);
        Assert.Equal(mode, model.Conversation!.Request.Mode);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InitializeAsync_BoundedPendingRead_ExposesLoadingBeforeSettlementAsync()
    {
        var fixture = new ControlledChatFixture();
        fixture.Reader.OpenGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = CreateModel(fixture);

        Task opening = model.InitializeAsync(fixture.Selection());
        try
        {
            Assert.Equal(LoadState.Loading, model.LoadState);
            Assert.Null(model.Conversation);
        }
        finally
        {
            fixture.Reader.OpenGate.SetResult();
            await opening;
        }

        Assert.Equal(LoadState.Loaded, model.LoadState);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InitializeAsync_NoBusinessDocumentsAndMissingSelection_DistinguishesEmptyFromUnavailableAsync()
    {
        var fixture = new ControlledChatFixture();
        var empty = CreateModel(fixture);
        ReadSelectionRequest request = fixture.Selection(KnowledgeMode.Business);
        await empty.InitializeAsync(request with { Publication = request.Publication with { PublicationId = fixture.Knowledge.EarlyPublicationId } });
        var unavailable = CreateModel(fixture);
        await unavailable.InitializeAsync(null);

        Assert.Equal(LoadState.Empty, empty.LoadState);
        Assert.Equal(OctoberChatEntryStatus.Empty, empty.Result!.Status);
        Assert.Equal(LoadState.Error, unavailable.LoadState);
        Assert.Equal(OctoberChatEntryStatus.Unavailable, unavailable.Result!.Status);
        Assert.NotEqual(empty.StatusMessage, unavailable.StatusMessage);
        Assert.False(string.IsNullOrWhiteSpace(empty.StatusMessage));
        Assert.False(string.IsNullOrWhiteSpace(unavailable.StatusMessage));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("mode")]
    [InlineData("scope")]
    [InlineData("publication")]
    public async Task RequestContextChangeAsync_ChangedFixedContext_RequiresConfirmationThenStartsEmptyAsync(string changed)
    {
        var fixture = new ControlledChatFixture();
        var model = CreateModel(fixture);
        ReadSelectionRequest request = fixture.Selection();
        await model.InitializeAsync(request);
        OctoberChatConversation original = model.Conversation!;
        model.Question = "上一个上下文的草稿";
        ReadSelectionRequest next = changed switch
        {
            "mode" => request with { Mode = KnowledgeMode.Business },
            "scope" => request with { RepositoryScope = [fixture.Knowledge.RepositoryB] },
            _ => request with { Publication = request.Publication with { PublicationId = fixture.Knowledge.EarlyPublicationId } },
        };

        await model.RequestContextChangeAsync(next, confirmed: false);

        Assert.True(model.RequiresConfirmation);
        Assert.Same(original, model.Conversation);
        Assert.Equal("上一个上下文的草稿", model.Question);
        Assert.Single(fixture.Reader.OpenRequests);

        await model.RequestContextChangeAsync(next, confirmed: true);

        Assert.False(model.RequiresConfirmation);
        Assert.NotEqual(original.ConversationId, model.Conversation!.ConversationId);
        Assert.NotEqual(original.ContextId, model.Conversation.ContextId);
        Assert.Equal(next.Publication, model.Conversation.Request.Publication);
        Assert.Equal(next.Mode, model.Conversation.Request.Mode);
        Assert.Equal(next.RepositoryScope.Order(), model.Conversation.Request.RepositoryScope.Order());
        Assert.Empty(model.Question);
        Assert.NotSame(original.Selection, model.Conversation.Selection);
        Assert.Equal(2, fixture.Reader.OpenRequests.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberChatWorkState.Active)]
    [InlineData(OctoberChatWorkState.SaveIndeterminate)]
    public async Task RequestContextChangeAsync_UnsettledWork_RejectsEvenConfirmedChangeAsync(OctoberChatWorkState state)
    {
        var fixture = new ControlledChatFixture();
        var model = CreateModel(fixture);
        ReadSelectionRequest request = fixture.Selection();
        await model.InitializeAsync(request);
        OctoberChatConversation original = model.Conversation!;
        model.Question = "保留草稿";
        model.SetWorkState(state);

        await model.RequestContextChangeAsync(request with { Mode = KnowledgeMode.Business }, confirmed: true);
        await model.StartNewConversationAsync(confirmed: true);

        Assert.Same(original, model.Conversation);
        Assert.Equal(state, model.WorkState);
        Assert.Equal("保留草稿", model.Question);
        Assert.Single(fixture.Reader.OpenRequests);
        Assert.False(string.IsNullOrWhiteSpace(model.StatusMessage));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RequestContextChangeAsync_ReorderedSameScope_RetainsConversationAndDraftAsync()
    {
        var fixture = new ControlledChatFixture();
        var model = CreateModel(fixture);
        ReadSelectionRequest request = fixture.Selection();
        await model.InitializeAsync(request);
        OctoberChatConversation original = model.Conversation!;
        model.Question = "仍然有效的草稿";

        await model.RequestContextChangeAsync(request with { RepositoryScope = [fixture.Knowledge.RepositoryB, fixture.Knowledge.RepositoryA] }, confirmed: false);

        Assert.Same(original, model.Conversation);
        Assert.Equal("仍然有效的草稿", model.Question);
        Assert.False(model.RequiresConfirmation);
        Assert.Single(fixture.Reader.OpenRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("subject")]
    [InlineData("authentication")]
    public async Task RequestContextChangeAsync_HostIdentityChanged_ClearsPriorDataBeforeLiveReopenAsync(string changed)
    {
        var fixture = new ControlledChatFixture();
        var model = CreateModel(fixture);
        ReadSelectionRequest request = fixture.Selection();
        await model.InitializeAsync(request);
        OctoberChatConversation original = model.Conversation!;
        model.Question = "前一身份的私人草稿";
        ReadSelectionRequest next = request with
        {
            Subject = changed == "subject"
                ? request.Subject with { SubjectId = "another-ordinary-user" }
                : request.Subject with { AuthenticationBinding = "renewed-host-stamp" },
        };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Reader.OpenGate = gate;

        Task changing = model.RequestContextChangeAsync(next, confirmed: false);
        try
        {
            Assert.Null(model.Conversation);
            Assert.Null(model.Catalog);
            Assert.Empty(model.Question);
            Assert.False(model.RequiresConfirmation);
            Assert.Equal(LoadState.Loading, model.LoadState);
        }
        finally
        {
            gate.SetResult();
            await changing;
        }

        Assert.Equal(next.Subject, model.Conversation!.Request.Subject);
        Assert.NotEqual(original.ConversationId, model.Conversation.ConversationId);
        Assert.NotEqual(original.ContextId, model.Conversation.ContextId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializeAsync_SupersededPendingRead_DiscardsLateOldModeAndCatalogAsync(bool useHostParameterChange)
    {
        var fixture = new ControlledChatFixture();
        var model = CreateModel(fixture);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Reader.OpenGate = gate;
        Task oldOpening = model.InitializeAsync(fixture.Selection(KnowledgeMode.IT));
        fixture.Reader.OpenGate = null;
        try
        {
            if (useHostParameterChange)
            {
                await model.RequestContextChangeAsync(fixture.Selection(KnowledgeMode.Business), confirmed: false);
            }
            else
            {
                await model.InitializeAsync(fixture.Selection(KnowledgeMode.Business));
            }
            OctoberChatConversation current = Assert.IsType<OctoberChatConversation>(model.Conversation);
            Assert.Equal(KnowledgeMode.Business, current.Request.Mode);
        }
        finally
        {
            gate.SetResult();
            await oldOpening;
        }

        Assert.Equal(KnowledgeMode.Business, model.Conversation!.Request.Mode);
        Assert.Equal("light", model.ThemeName);
        Assert.All(model.Catalog!.Documents, document => Assert.Equal(DocumentKind.BusinessLogic, document.Kind));
        Assert.Equal(2, fixture.Reader.OpenRequests.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberChatWorkState.Active)]
    [InlineData(OctoberChatWorkState.SaveIndeterminate)]
    public async Task RequestContextChangeAsync_IdentityChangedWithUnsettledWork_HidesOldDataWithoutOpeningAnotherContextAsync(OctoberChatWorkState state)
    {
        var fixture = new ControlledChatFixture();
        var model = CreateModel(fixture);
        ReadSelectionRequest request = fixture.Selection();
        await model.InitializeAsync(request);
        model.Question = "不能泄露给新身份的草稿";
        model.SetWorkState(state);
        ReadSelectionRequest next = request with { Subject = request.Subject with { SubjectId = "new-owner" } };

        await model.RequestContextChangeAsync(next, confirmed: true);
        await model.InitializeAsync(next);

        Assert.Null(model.Conversation);
        Assert.Null(model.Catalog);
        Assert.Empty(model.Question);
        Assert.Equal(state, model.WorkState);
        Assert.Equal(LoadState.Error, model.LoadState);
        Assert.Single(fixture.Reader.OpenRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartNewConversationAsync_SettledConfirmedRequest_IssuesFreshContextWithoutImportingDraftAsync()
    {
        var fixture = new ControlledChatFixture();
        var model = CreateModel(fixture);
        await model.InitializeAsync(fixture.Selection());
        OctoberChatConversation original = model.Conversation!;
        model.Question = "旧的未发送问题";

        await model.StartNewConversationAsync(confirmed: false);
        Assert.True(model.RequiresConfirmation);
        Assert.Same(original, model.Conversation);

        await model.StartNewConversationAsync(confirmed: true);

        Assert.NotEqual(original.ConversationId, model.Conversation!.ConversationId);
        Assert.NotEqual(original.ContextId, model.Conversation.ContextId);
        Assert.Equal(original.Request, model.Conversation.Request);
        Assert.Empty(model.Question);
        Assert.False(model.RequiresConfirmation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AddOctoberChat_ExplicitOptIn_RegistersScopedConsumersWithoutKnowledgeFallback()
    {
        var fixture = new ControlledChatFixture();
        var services = new ServiceCollection();
        services.AddSingleton<IOctoberKnowledgeReader>(fixture.Reader);
        services.AddSingleton<IOctoberKnowledgeDisclosurePolicy>(fixture.Policy);

        services.AddOctoberChat();

        Assert.Single(services.Where(item => item.ServiceType == typeof(IOctoberKnowledgeReader)));
        Assert.Single(services.Where(item => item.ServiceType == typeof(IOctoberKnowledgeDisclosurePolicy)));
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope first = provider.CreateScope();
        using IServiceScope second = provider.CreateScope();
        OctoberChatEntryViewModel firstModel = first.ServiceProvider.GetRequiredService<OctoberChatEntryViewModel>();
        Assert.Same(firstModel, first.ServiceProvider.GetRequiredService<OctoberChatEntryViewModel>());
        Assert.NotSame(firstModel, second.ServiceProvider.GetRequiredService<OctoberChatEntryViewModel>());
        Assert.IsType<OctoberChatSelectionService>(first.ServiceProvider.GetRequiredService<IOctoberChatSelectionService>());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeMode.Business, "light")]
    [InlineData(KnowledgeMode.IT, "dark")]
    public void ChatPage_ExplicitControlledEntry_UsesExistingShellAndCanonicalMode(KnowledgeMode mode, string theme)
    {
        var fixture = new ControlledChatFixture();
        using var harness = new ChatPageTests();
        var model = CreateModel(fixture);

        IRenderedComponent<ChatPage> page = harness.Render<ChatPage>(parameters => parameters
            .Add(item => item.OctoberEntry, model)
            .Add(item => item.OctoberSelection, fixture.Selection(mode)));

        page.WaitForAssertion(() => Assert.Equal("Loaded", page.Find(".chat-page").GetAttribute("data-load-state")));
        Assert.Equal(theme, page.Find(".chat-page").GetAttribute("data-theme"));
        Assert.Equal("对话记录", page.Find(".chat-surface").GetAttribute("aria-label"));
        Assert.Contains("业务模式", page.Markup, StringComparison.Ordinal);
        Assert.Contains("技术模式", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mbs_account", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mbs_channel", page.Markup, StringComparison.Ordinal);
        Assert.Single(fixture.Reader.OpenRequests);
        if (mode == KnowledgeMode.Business)
        {
            Assert.DoesNotContain("技术代码 CL-A", page.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("src/Order.cs", page.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("隐藏业务标题", page.Markup, StringComparison.Ordinal);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ChatPage_ExplicitEntryWithoutHostBinding_ShowsUnavailableWithoutLegacyFallback()
    {
        var fixture = new ControlledChatFixture();
        using var harness = new ChatPageTests();
        var model = CreateModel(fixture);

        IRenderedComponent<ChatPage> page = harness.Render<ChatPage>(parameters => parameters.Add(item => item.OctoberEntry, model));

        page.WaitForAssertion(() => Assert.Equal("Error", page.Find(".chat-page").GetAttribute("data-load-state")));
        Assert.Equal(OctoberChatEntryStatus.Unavailable, model.Result!.Status);
        Assert.DoesNotContain("mbs_account", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mbs_channel", page.Markup, StringComparison.Ordinal);
        Assert.Empty(fixture.Reader.OpenRequests);
        Assert.Empty(page.FindAll(".complete-answer"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ChatPage_ModeButton_ConfirmsFreshContextBeforeChangingThemeAsync()
    {
        var fixture = new ControlledChatFixture();
        using var harness = new ChatPageTests();
        var model = CreateModel(fixture);
        IRenderedComponent<ChatPage> page = harness.Render<ChatPage>(parameters => parameters
            .Add(item => item.OctoberEntry, model)
            .Add(item => item.OctoberSelection, fixture.Selection(KnowledgeMode.Business)));
        page.WaitForAssertion(() => Assert.Equal("Loaded", page.Find(".chat-page").GetAttribute("data-load-state")));
        OctoberChatConversation original = model.Conversation!;
        model.Question = "原业务模式草稿";


        await page.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "技术模式")
            .ClickAsync(new MouseEventArgs());
        Assert.Single(page.FindAll("fluent-dialog"));
        Assert.DoesNotContain("更改模式或仓库范围", page.Find(".chat-surface").TextContent);
        await page.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "取消")
            .ClickAsync(new MouseEventArgs());
        Assert.Empty(page.FindAll("fluent-dialog"));
        Assert.False(model.RequiresConfirmation);
        Assert.Same(original, model.Conversation);
        Assert.Equal("原业务模式草稿", model.Question);
        Assert.Equal("light", page.Find(".chat-page").GetAttribute("data-theme"));
        Assert.Single(fixture.Reader.OpenRequests);


        await page.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "技术模式")
            .ClickAsync(new MouseEventArgs());
        Assert.Single(page.FindAll("fluent-dialog"));
        fixture.Reader.OpenGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task confirming = page.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "确定")
            .ClickAsync(new MouseEventArgs());
        try
        {
            page.WaitForAssertion(() => Assert.Empty(page.FindAll("fluent-dialog")));
            Assert.DoesNotContain("请确认", page.Find(".chat-surface").TextContent);
            Assert.DoesNotContain("确认开始新对话", page.Find(".chat-surface").TextContent);
        }
        finally
        {
            fixture.Reader.OpenGate.SetResult();
            await confirming;
        }
        Assert.False(model.RequiresConfirmation);
        Assert.Equal(KnowledgeMode.IT, model.Conversation!.Request.Mode);
        Assert.NotEqual(original.ConversationId, model.Conversation.ConversationId);
        Assert.Equal(original.Request.Subject, model.Conversation.Request.Subject);
        Assert.Equal(original.Request.Publication, model.Conversation.Request.Publication);
        Assert.Empty(model.Question);
        Assert.Equal("dark", page.Find(".chat-page").GetAttribute("data-theme"));
        Assert.Equal("true", page.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "技术模式").GetAttribute("aria-pressed"));
        Assert.Equal("false", page.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "业务模式").GetAttribute("aria-pressed"));
        Assert.Equal(2, fixture.Reader.OpenRequests.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ChatPage_ReplacedEntryWithSameHostSelection_InitializesFreshModel()
    {
        var fixture = new ControlledChatFixture();
        using var harness = new ChatPageTests();
        var original = CreateModel(fixture);
        ReadSelectionRequest request = fixture.Selection(KnowledgeMode.Business);
        IRenderedComponent<ChatPage> page = harness.Render<ChatPage>(parameters => parameters
            .Add(item => item.OctoberEntry, original).Add(item => item.OctoberSelection, request));
        page.WaitForAssertion(() => Assert.Equal("Loaded", page.Find(".chat-page").GetAttribute("data-load-state")));
        var replacement = CreateModel(fixture);

        page.Render(parameters => parameters.Add(item => item.OctoberEntry, replacement)
            .Add(item => item.OctoberSelection, request));

        page.WaitForAssertion(() => Assert.Equal("Loaded", page.Find(".chat-page").GetAttribute("data-load-state")));
        Assert.NotNull(replacement.Conversation);
        Assert.NotEqual(original.Conversation!.ConversationId, replacement.Conversation.ConversationId);
        Assert.Equal(request.Subject, replacement.Conversation.Request.Subject);
        Assert.Equal(2, fixture.Reader.OpenRequests.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ChatPage_RemovedExplicitEntry_FailsClosedWithoutLegacyShellFallback()
    {
        var fixture = new ControlledChatFixture();
        using var harness = new ChatPageTests();
        var model = CreateModel(fixture);
        IRenderedComponent<ChatPage> page = harness.Render<ChatPage>(parameters => parameters
            .Add(item => item.OctoberEntry, model).Add(item => item.OctoberSelection, fixture.Selection()));
        page.WaitForAssertion(() => Assert.Equal("Loaded", page.Find(".chat-page").GetAttribute("data-load-state")));
        model.Question = "移除模块后不能显示的草稿";

        page.Render(parameters => parameters.Add(item => item.OctoberEntry, (OctoberChatEntryViewModel?)null));

        page.WaitForAssertion(() => Assert.Equal("Error", page.Find(".chat-page").GetAttribute("data-load-state")));
        Assert.Null(model.Conversation);
        Assert.Null(model.Catalog);
        Assert.Empty(model.Question);
        Assert.DoesNotContain("mbs_account", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mbs_channel", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".complete-answer"));
        Assert.Single(fixture.Reader.OpenRequests);
    }

    private static OctoberChatEntryViewModel CreateModel(ControlledChatFixture fixture) =>
        new(new OctoberChatSelectionService(fixture.Reader, fixture.Policy));
}

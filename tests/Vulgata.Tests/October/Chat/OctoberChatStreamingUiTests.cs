using System.Collections.Immutable;
using Bunit;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberChat;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Components.Pages;
using Vulgata.Web.Services.OctoberChat;
using Vulgata.Web.ViewModels.OctoberChat;
using TurnComponent = Vulgata.Web.Components.Chat.OctoberChatTurn;

namespace Vulgata.Tests.October.Chat;

/// <summary>Explicit host-event projection only; no provider, browser, durable receipt or authorization qualification.</summary>
public sealed class OctoberChatStreamingUiTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ToolRoundNarration_IsSeparateFromFinalAnswer_AndSuppressedWithIt()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        var component = context.Render<TurnComponent>(p => p.Add(x => x.ViewModel, model));
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, "先检索登录文档。")));
        Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.ToolStarted, callId: "read-1")));
        Assert.True(model.Apply(Event(model, 4, OctoberChatEventKind.ToolCompleted, callId: "read-1")));
        component.WaitForAssertion(() => Assert.Contains("先检索登录文档。", component.Find(".october-chat-progress").TextContent));
        Assert.Empty(model.AnswerText);
        Assert.True(model.Apply(Event(model, 5, OctoberChatEventKind.AnswerDelta, "登录先校验身份。")));
        Assert.True(model.Apply(Event(model, 6, OctoberChatEventKind.TurnSaved) with { SavedOutcome = OctoberChatDisplayOutcome.Completed }));
        component.WaitForAssertion(() => Assert.Equal("登录先校验身份。", component.Find(".october-chat-answer").TextContent));
        Assert.DoesNotContain("先检索", component.Find(".october-chat-answer").TextContent);
        Assert.True(model.TrySuppressDisclosure(model.Binding));
        component.WaitForAssertion(() => Assert.DoesNotContain("先检索", component.Markup));
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Apply_TextBeforeControlledFinish_RendersWhileReferencesAndSaveAreStillPendingAsync()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task projection = EmitControlledEventsAsync();
        try
        {
            component.WaitForAssertion(() => Assert.Equal("后续工具仍在处理。", component.Find(".october-chat-answer").TextContent));
            Assert.False(projection.IsCompleted);
            Assert.False(model.IsTerminal);
            Assert.Empty(model.References);
            Assert.DoesNotContain("已保存", component.Markup, StringComparison.Ordinal);
        }
        finally
        {
            release.TrySetResult();
            await projection;
        }

        component.WaitForAssertion(() => Assert.Contains("已保存", component.Markup, StringComparison.Ordinal));
        Assert.True(model.IsTerminal);

        async Task EmitControlledEventsAsync()
        {
            await component.InvokeAsync(() =>
            {
                Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
                Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, "先说明已有证据。\n")));
                Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.ToolStarted, "不可信工具参数", "call-1")));
                Assert.True(model.Apply(Event(model, 4, OctoberChatEventKind.AnswerDelta, "后续工具仍在处理。")));
            });
            await release.Task;
            await component.InvokeAsync(() =>
            {
                Assert.True(model.Apply(Event(model, 5, OctoberChatEventKind.ToolCompleted, "不显示的原始结果", "call-1")));
                Assert.True(model.Apply(Event(model, 6, OctoberChatEventKind.TurnSaved) with { SavedOutcome = OctoberChatDisplayOutcome.Completed }));
            });
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Apply_DuplicateForeignAndOutOfOrderEvents_DoNotCorruptTextOrConsumeValidSequence()
    {
        var model = CreateModel();
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        OctoberChatEvent first = Event(model, 2, OctoberChatEventKind.AnswerDelta, "第一段");
        Assert.True(model.Apply(first));
        Assert.False(model.Apply(first));
        Assert.False(model.Apply(first with { ConversationId = Guid.NewGuid(), Sequence = 100, Text = "其他对话" }));
        Assert.False(model.Apply(first with { ContextId = Guid.NewGuid(), Sequence = 100, Text = "其他范围" }));
        Assert.False(model.Apply(first with { TurnId = Guid.NewGuid(), Sequence = 100, Text = "其他提问" }));
        Assert.False(model.Apply(first with { Sequence = 1, Text = "迟到片段" }));
        Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.AnswerDelta, "第二段")));

        Assert.Equal("第一段第二段", model.AnswerText);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Apply_InvalidLifecycleEvent_DoesNotPoisonLaterStartOrSave()
    {
        var model = CreateModel();
        Assert.False(model.Apply(Event(model, 100, OctoberChatEventKind.AnswerDelta, "开始之前")));
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.False(model.Apply(Event(model, 100, OctoberChatEventKind.TurnSaved)));
        Assert.False(model.Apply(Event(model, 100, OctoberChatEventKind.TurnSaved) with { SavedOutcome = (OctoberChatDisplayOutcome)99 }));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, "有效输出")));
        Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.TurnSaved) with { SavedOutcome = OctoberChatDisplayOutcome.Completed }));

        Assert.True(model.IsTerminal);
        Assert.Equal("有效输出", model.AnswerText);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberChatDisplayOutcome.Completed, "已保存")]
    [InlineData(OctoberChatDisplayOutcome.Interrupted, "中断")]
    [InlineData(OctoberChatDisplayOutcome.Failed, "失败")]
    public void Apply_TypedSavedOutcome_PreservesUsefulTextAndPreventsLaterGeneration(OctoberChatDisplayOutcome outcome, string label)
    {
        using var context = new BunitContext();
        var model = CreateModel();
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, "已经收到的有用片段")));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));

        Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.TurnSaved) with { SavedOutcome = outcome }));
        Assert.False(model.Apply(Event(model, 4, OctoberChatEventKind.AnswerDelta, "不能继续生成")));
        Assert.False(model.Apply(Event(model, 5, OctoberChatEventKind.ToolStarted, "不能再执行", "later-call")));
        Assert.False(model.Apply(Event(model, 6, OctoberChatEventKind.TurnStarted)));

        component.WaitForAssertion(() => Assert.Contains(label, component.Markup, StringComparison.Ordinal));
        Assert.Equal("已经收到的有用片段", component.Find(".october-chat-answer").TextContent);
        Assert.True(model.IsTerminal);
        if (outcome != OctoberChatDisplayOutcome.Completed)
        {
            Assert.Contains("部分", component.Markup, StringComparison.Ordinal);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Apply_SaveUncertain_FreezesGenerationUntilTypedSavedProjectionArrives()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, "有用片段")));
        Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.TurnSaveUncertain)));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));

        Assert.True(model.IsSaveUncertain);
        Assert.Contains("保存状态待确认", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("已保存", component.Markup, StringComparison.Ordinal);
        Assert.False(model.Apply(Event(model, 100, OctoberChatEventKind.AnswerDelta, "自动重试")));
        Assert.False(model.Apply(Event(model, 101, OctoberChatEventKind.ToolStarted, "再次调用", "retry")));
        Assert.True(model.Apply(Event(model, 4, OctoberChatEventKind.TurnSaved) with { SavedOutcome = OctoberChatDisplayOutcome.Interrupted }));

        Assert.True(model.IsTerminal);
        Assert.False(model.IsSaveUncertain);
        Assert.Equal("有用片段", model.AnswerText);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberChatDisplayOutcome.Interrupted, "")]
    [InlineData(OctoberChatDisplayOutcome.Interrupted, " \r\n\t")]
    [InlineData(OctoberChatDisplayOutcome.Failed, "")]
    [InlineData(OctoberChatDisplayOutcome.Failed, " \r\n\t")]
    public void Apply_EmptyOrWhitespaceInterruptedOutcome_DoesNotClaimRetainedUsefulContent(OctoberChatDisplayOutcome outcome, string text)
    {
        using var context = new BunitContext();
        var model = CreateModel();
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        if (text.Length > 0) Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, text)));
        Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.TurnSaved) with { SavedOutcome = outcome }));

        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));

        Assert.Contains(outcome == OctoberChatDisplayOutcome.Interrupted ? "中断" : "失败", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("已保留部分内容", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("已保存", component.Markup, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(component.Find(".october-chat-answer").TextContent));
        Assert.True(model.IsTerminal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberChatEventKind.ToolStarted)]
    [InlineData(OctoberChatEventKind.ToolCompleted)]
    [InlineData(OctoberChatEventKind.ToolFailed)]
    public void Apply_ToolActivity_UsesGenericChineseLabelWithoutRawPayloadOrCallId(OctoberChatEventKind kind)
    {
        using var context = new BunitContext();
        var model = CreateModel();
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        if (kind != OctoberChatEventKind.ToolStarted)
        {
            Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.ToolStarted, "SECRET-TITLE", "private-call-id")));
        }
        Assert.True(model.Apply(Event(model, kind == OctoberChatEventKind.ToolStarted ? 2 : 3, kind,
            "SECRET-TITLE src/private.cs api-key=private", "private-call-id")));

        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));

        Assert.False(string.IsNullOrWhiteSpace(model.ActivityMessage));
        Assert.Matches("[\u4e00-\u9fff]", model.ActivityMessage!);
        Assert.DoesNotContain("SECRET-TITLE", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("src/private.cs", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("private-call-id", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("api-key", component.Markup, StringComparison.Ordinal);
        Assert.Empty(model.AnswerText);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Apply_ModelMarkupAndLinks_RendersEncodedNarrativeWithoutActiveMarkup()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        const string prose = "说明😀\n<script>alert('x')</script><img src=x onerror=alert(1)> [模型链接](https://untrusted.example/) <a href='javascript:alert(1)'>跳转</a>";
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, prose)));

        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));

        Assert.Equal(prose, component.Find(".october-chat-answer").TextContent);
        Assert.Empty(component.FindAll("script,img,a,iframe,object"));
        Assert.Contains("&lt;script&gt;", component.Markup, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Apply_ReferencesArriveIndependently_ShowsCountAndPartialLabelWithoutInventedLinks()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, "先到达的正文")));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));
        var fixture = new ControlledChatFixture();
        ImmutableArray<OctoberChatReference> references = [new(fixture.Knowledge.Handle(fixture.Knowledge.BlA, section: "overview"), true, 1, 100)];

        Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.ReferencesUpdated) with { References = references }));

        component.WaitForAssertion(() => Assert.Contains("部分", component.Markup, StringComparison.Ordinal));
        Assert.Contains("1", component.Markup, StringComparison.Ordinal);
        Assert.Equal("先到达的正文", component.Find(".october-chat-answer").TextContent);
        Assert.Equal(references, model.References);
        Assert.Empty(component.FindAll("a"));
        Assert.DoesNotContain(references[0].Document.DocumentVersionId.ToString(), component.Markup, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Component_ModelReplacementAndDisposal_DetachesOldEventSource()
    {
        using var context = new BunitContext();
        var oldModel = CreateModel();
        Assert.True(oldModel.Apply(Event(oldModel, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(oldModel.Apply(Event(oldModel, 2, OctoberChatEventKind.AnswerDelta, "旧对话内容")));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, oldModel));
        var replacement = CreateModel();
        Assert.True(replacement.Apply(Event(replacement, 1, OctoberChatEventKind.TurnStarted)));

        component.Render(parameters => parameters.Add(item => item.ViewModel, replacement));
        Assert.True(oldModel.Apply(Event(oldModel, 3, OctoberChatEventKind.AnswerDelta, "迟到旧片段")));
        Assert.True(replacement.Apply(Event(replacement, 2, OctoberChatEventKind.AnswerDelta, "新对话片段")));

        component.WaitForAssertion(() => Assert.Equal("新对话片段", component.Find(".october-chat-answer").TextContent));
        Assert.DoesNotContain("旧对话", component.Markup, StringComparison.Ordinal);
        component.Dispose();
        Assert.True(replacement.Apply(Event(replacement, 3, OctoberChatEventKind.AnswerDelta, "组件已移除")));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ChatPage_ControlledTurnMustMatchCurrentContext_AndCannotEnableSending()
    {
        var fixture = new ControlledChatFixture();
        var entry = new OctoberChatEntryViewModel(new OctoberChatSelectionService(fixture.Reader, fixture.Policy));
        using var harness = new ChatPageTests();
        IRenderedComponent<ChatPage> page = harness.Render<ChatPage>(parameters => parameters
            .Add(item => item.OctoberEntry, entry).Add(item => item.OctoberSelection, fixture.Selection()));
        page.WaitForAssertion(() => Assert.NotNull(entry.Conversation));
        OctoberChatConversation conversation = entry.Conversation!;
        var turn = new OctoberChatTurnViewModel(new(conversation.ConversationId, conversation.ContextId, Guid.NewGuid(), conversation.Request, conversation.Selection));
        Assert.True(turn.Apply(Event(turn, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(turn.Apply(Event(turn, 2, OctoberChatEventKind.AnswerDelta, "当前对话的受控片段")));

        page.Render(parameters => parameters.Add(item => item.OctoberTurn, turn));

        Assert.Equal("当前对话的受控片段", page.Find(".october-chat-answer").TextContent);
        Assert.True(page.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "发送").HasAttribute("disabled"));
        var foreign = new OctoberChatTurnViewModel(turn.Binding with { ContextId = Guid.NewGuid() });
        Assert.True(foreign.Apply(Event(foreign, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(foreign.Apply(Event(foreign, 2, OctoberChatEventKind.AnswerDelta, "另一个上下文")));
        page.Render(parameters => parameters.Add(item => item.OctoberTurn, foreign));
        Assert.Empty(page.FindAll(".october-chat-turn"));
        Assert.DoesNotContain("另一个上下文", page.Markup, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Apply_ExplicitSanitizedTerminalClearsProvisionalProseAndReferences()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        var fixture = new ControlledChatFixture();
        ImmutableArray<OctoberChatReference> references = [new(fixture.Knowledge.Handle(fixture.Knowledge.BlA, section: "overview"), true, 1, 100)];
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, "授权撤销前显示的正文")));
        Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.ReferencesUpdated) with { References = references }));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));

        // This tests display semantics only; the adapter separately qualifies canonical receipt acceptance.
        Assert.True(model.Apply(Event(model, 4, OctoberChatEventKind.TurnSaved, string.Empty) with
        { SavedOutcome = OctoberChatDisplayOutcome.Failed, References = [] }));

        component.WaitForAssertion(() => Assert.Equal(string.Empty, component.Find(".october-chat-answer").TextContent));
        Assert.Empty(model.References);
        Assert.Empty(component.FindAll(".october-chat-references"));
        Assert.DoesNotContain("已保留部分内容", component.Markup, StringComparison.Ordinal);
        Assert.True(model.IsTerminal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Markdown_SupportedBlocksAndInlineFormattingHaveMeaningfulSafeElements()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta,
            "# 业务说明\n\n正文 **重要**、*推测* 与 `a < b`。\n\n- 第一项\n- 第二项\n\n1. 步骤一\n2. 步骤二\n\n> 明确限定\n> 保留不确定性")));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));

        Assert.Equal("业务说明", component.Find(".october-chat-answer h1").TextContent);
        Assert.Equal("重要", component.Find(".october-chat-answer strong").TextContent);
        Assert.Equal("推测", component.Find(".october-chat-answer em").TextContent);
        Assert.Equal("a < b", component.Find(".october-chat-answer p code").TextContent);
        Assert.Equal(new[] { "第一项", "第二项" }, component.FindAll(".october-chat-answer ul > li").Select(item => item.TextContent));
        Assert.Equal(new[] { "步骤一", "步骤二" }, component.FindAll(".october-chat-answer ol > li").Select(item => item.TextContent));
        Assert.Contains("明确限定", component.Find(".october-chat-answer blockquote").TextContent, StringComparison.Ordinal);
        Assert.Contains("保留不确定性", component.Find(".october-chat-answer blockquote").TextContent, StringComparison.Ordinal);
        Assert.False(model.IsTerminal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Markdown_IncompleteChunksRemainVisibleWithoutWaitingForTerminalOrClosingDelimiter()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, "# 流式标题\n\n正文 **尚未闭合")));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));
        Assert.Equal("流式标题", component.Find(".october-chat-answer h1").TextContent);
        Assert.Contains("**尚未闭合", component.Find(".october-chat-answer").TextContent, StringComparison.Ordinal);
        Assert.Empty(component.FindAll(".october-chat-answer strong"));

        Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.AnswerDelta, "现在完成**")));
        component.WaitForAssertion(() => Assert.Equal("尚未闭合现在完成", component.Find(".october-chat-answer strong").TextContent));
        string unclosed = "```csharp\n" + string.Concat(Enumerable.Repeat("<x> **未完成 ", 2048));
        Assert.True(model.Apply(Event(model, 4, OctoberChatEventKind.AnswerDelta, "\n\n" + unclosed)));
        component.WaitForAssertion(() => Assert.Contains(unclosed, component.Find(".october-chat-answer").TextContent, StringComparison.Ordinal));
        Assert.Empty(component.FindAll(".october-chat-answer pre"));
        Assert.False(model.IsTerminal);
        Assert.Equal("provisional", component.Find(".october-chat-turn").GetAttribute("data-answer-state"));
        Assert.DoesNotContain("已保存", component.Markup, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Markdown_ModelHtmlImagesAndUrlSchemesRemainEncodedLiteralWithoutNavigation()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        const string prose = "<script>alert(1)</script><img src=x onerror=alert(1)><svg onload=alert(1)></svg> " +
            "<a href='javascript:alert(1)' onclick='alert(1)'>链接</a> " +
            "[http](http://example.invalid/) [https](https://example.invalid/) [js](javascript:alert(1)) " +
            "[data](data:text/html,hello) [file](file:///C:/private) [mail](mailto:test@example.invalid) " +
            "[ftp](ftp://example.invalid/) [relative](/internal) [network](//example.invalid/) ![image](https://example.invalid/image.png)";
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, prose)));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));

        Assert.Equal(prose, component.Find(".october-chat-answer").TextContent);
        Assert.Empty(component.FindAll(".october-chat-answer a, .october-chat-answer img, .october-chat-answer script, .october-chat-answer svg, .october-chat-answer iframe"));
        Assert.Empty(component.FindAll(".october-chat-answer [href], .october-chat-answer [src], .october-chat-answer [onclick], .october-chat-answer [onerror], .october-chat-answer [onload]"));
        Assert.Contains("&lt;script&gt;", component.Markup, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Markdown_FencedAndInlineCodePreserveExactContentWithoutInterpretingMarkup()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        const string code = "\tif (x < 3 && y > 1) {\n    print(\"**literal** <script>alert(1)</script> 😀\");\n}\n";
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta,
            "```csharp\n" + code + "```\n\n行内 `a < b & c` 保持原样。")));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));

        Assert.Equal(code, component.Find(".october-chat-answer pre > code").TextContent);
        Assert.Equal("a < b & c", component.Find(".october-chat-answer p > code").TextContent);
        Assert.Empty(component.FindAll(".october-chat-answer pre strong, .october-chat-answer pre script"));
        Assert.Empty(component.FindAll(".october-chat-answer a, .october-chat-answer img"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Markdown_DisclosureSuppressionClearsRichDomAndRejectsStaleDelta()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, "# 受限标题\n\n**受限正文**\n\n- 私有列表")));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));
        Assert.Equal("受限标题", component.Find(".october-chat-answer h1").TextContent);
        Assert.Equal("受限正文", component.Find(".october-chat-answer strong").TextContent);

        Assert.True(model.Apply(Event(model, 3, OctoberChatEventKind.DisclosureSuppressed)));
        Assert.False(model.Apply(Event(model, 4, OctoberChatEventKind.AnswerDelta, "# 迟到旧标题")));

        component.WaitForAssertion(() => Assert.Equal(string.Empty, component.Find(".october-chat-answer").TextContent));
        Assert.Empty(component.FindAll(".october-chat-answer h1, .october-chat-answer strong, .october-chat-answer li"));
        Assert.DoesNotContain("受限", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("迟到旧标题", component.Markup, StringComparison.Ordinal);
        Assert.False(model.IsTerminal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Markdown_SyntaxBudgetAndOversizedInputFallbackPreserveEntireOriginalText()
    {
        using var context = new BunitContext();
        var model = CreateModel();
        // Thousands of distinct closed emphasis elements exceed 4,096 syntax/text nodes,
        // while the input remains well below the separate 65,536-character parse limit.
        string dense = string.Concat(Enumerable.Repeat("**x** ", 3000));
        Assert.True(dense.Length < 65_536);
        Assert.True(model.Apply(Event(model, 1, OctoberChatEventKind.TurnStarted)));
        Assert.True(model.Apply(Event(model, 2, OctoberChatEventKind.AnswerDelta, dense)));
        IRenderedComponent<TurnComponent> component = context.Render<TurnComponent>(parameters => parameters.Add(item => item.ViewModel, model));

        Assert.Equal(dense, component.Find(".october-chat-answer").TextContent);
        Assert.Empty(component.FindAll(".october-chat-answer strong"));
        Assert.False(model.IsTerminal);

        string oversized = "# " + new string('x', 65_537);
        IRenderedComponent<Vulgata.Web.Components.Chat.OctoberChatMarkdown> leaf =
            context.Render<Vulgata.Web.Components.Chat.OctoberChatMarkdown>(parameters => parameters.Add(item => item.Text, oversized));
        Assert.Equal(oversized, leaf.Markup);
        Assert.Empty(leaf.FindAll("h1"));
    }

    private static OctoberChatTurnViewModel CreateModel()
    {
        var fixture = new ControlledChatFixture();
        return new(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), fixture.Selection(KnowledgeMode.Business), new ReadSelection("controlled-ui-selection")));
    }

    private static OctoberChatEvent Event(OctoberChatTurnViewModel model, long sequence, OctoberChatEventKind kind, string? text = null, string? callId = null) =>
        new(model.Binding.ConversationId, model.Binding.ContextId, model.Binding.TurnId, sequence,
            DateTimeOffset.UnixEpoch.AddSeconds(sequence), kind, text, callId);
}

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

/// <summary>
/// Installed SDK probes and controlled October adapter tests. No raw SSE, endpoint,
/// credentials, provider admission, durable save or inference authorization is qualified.
/// </summary>
public sealed class OctoberChatProviderTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InstalledMaf_NativeToolContinuation_InvokesApprovedFacadeAndExposesTextBeforeFinalStopAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var earlyText = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Update("round-1", new TextReasoningContent("CONTROLLED hidden reasoning")),
                Update("round-1", new TextContent("开始说明。")),
                Update("round-1", new FunctionCallContent("read-1", "BrowseWiki", new Dictionary<string, object?> { ["query"] = "" })),
                Finish("round-1", ChatFinishReason.ToolCalls),
            ], token),
            token => StreamAsync([Update("round-2", new TextContent("已查阅受控目录。"))], token, finish.Task,
                Finish("round-2", ChatFinishReason.Stop)),
        ]);
        AIFunction browse = AIFunctionFactory.Create(
            async (string query, CancellationToken cancellationToken) =>
            {
                OctoberChatToolResult result = await wiki.Tools.InvokeAsync("BrowseWiki", JsonSerializer.Serialize(new { query }), cancellationToken);
                // The installed schema exporter cannot reflect the Core DTO's optional
                // ImmutableArray defaults. A bounded explicit JSON string is the native
                // test function's return shape; the actual canonical facade still executes.
                return JsonSerializer.Serialize(result);
            },
            "BrowseWiki", "Controlled installed-SDK capability probe");
        using var invoking = new FunctionInvokingChatClient(client) { MaximumIterationsPerRequest = 3 };
        var agent = new ChatClientAgent(invoking, new ChatClientAgentOptions
        {
            UseProvidedChatClientAsIs = true,
            ChatOptions = new ChatOptions { Tools = [browse] },
        });
        var text = new StringBuilder();
        Task run = ConsumeAsync();
        try
        {
            await earlyText.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
            Assert.Equal(2, client.Requests.Count);
            Assert.Single(fixture.Reader.SearchRequests);
            Assert.Equal(1, wiki.Budget.ToolCalls);
            Assert.Contains(client.Requests[1].SelectMany(message => message.Contents), content => content is FunctionResultContent);
            Assert.Contains("BrowseWiki", client.ToolNames[0]);
            Assert.DoesNotContain("CONTROLLED hidden reasoning", text.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("技术代码 CL-A", text.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            finish.TrySetResult();
            await run;
        }

        Assert.Equal("开始说明。已查阅受控目录。", text.ToString());
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal(0, client.NonStreamingCalls);

        async Task ConsumeAsync()
        {
            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("受控测试问题"))
            {
                text.Append(update.Text);
                if (text.ToString().Contains("已查阅受控目录。", StringComparison.Ordinal)) earlyText.TrySetResult();
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InstalledMaf_PendingInjectedStreamCancellation_PropagatesWithoutAnotherClientRequestAsync()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        using var client = new ScriptedChatClient([token => WaitingAsync(token)]);
        using var invoking = new FunctionInvokingChatClient(client);
        var agent = new ChatClientAgent(invoking, new ChatClientAgentOptions { UseProvidedChatClientAsIs = true });
        Task run = ConsumeAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Single(client.Requests);
            Assert.Equal(0, client.NonStreamingCalls);
        }
        finally
        {
            gate.TrySetResult();
        }

        async Task ConsumeAsync()
        {
            await foreach (AgentResponseUpdate _ in agent.RunStreamingAsync("受控取消测试", cancellationToken: cancellation.Token)) { }
        }

        async IAsyncEnumerable<ChatResponseUpdate> WaitingAsync([EnumeratorCancellation] CancellationToken token)
        {
            entered.TrySetResult();
            await gate.Task.WaitAsync(token);
            yield return Finish("cancelled-round", ChatFinishReason.Stop);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_NativeWikiRound_UsesFixedFacadeAndStreamsTextBeforeFinalCompletionAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Update("first", new TextReasoningContent("PRIVATE reasoning")),
                Update("first", new TextContent("已有证据。")),
                Call("first", "wiki-1", "BrowseWiki", new() { ["query"] = "" }),
                Finish("first", ChatFinishReason.ToolCalls),
            ], token),
            token => StreamAsync([Update("second", new TextContent("查阅完成，等待结束。"))], token, finish.Task, Finish("second", ChatFinishReason.Stop)),
        ]);
        var agent = new OctoberChatAgent(client, wiki.Tools, wiki.Budget);
        const string instructions = "CONTROLLED-BUSINESS-PROMPT: preserve all material qualifications.";
        var visible = new StringBuilder();
        Task<OctoberChatProviderResult> run = agent.RunAsync("受控问题", instructions, delta =>
        {
            visible.Append(delta);
            if (visible.ToString().Contains("查阅完成", StringComparison.Ordinal)) observed.TrySetResult();
        });
        try
        {
            Assert.Same(observed.Task, await Task.WhenAny(observed.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(run.IsCompleted);
            Assert.Equal(2, client.Requests.Count);
            Assert.Single(fixture.Reader.SearchRequests);
            Assert.Equal(wiki.Binding.Selection, fixture.Reader.SearchRequests[0].Selection);
            Assert.Equal(1, wiki.Budget.ToolCalls);
            Assert.Equal(2, wiki.Budget.ProviderRounds);
            Assert.Equal(new[] { "BrowseWiki", "GetRelatedDocuments", "ReadWiki", "ReportAnswerDocuments", "SearchWiki" }, client.ToolNames[0].Order());
            Assert.Contains(client.Requests[0], message => message.Role == ChatRole.System && message.Text == instructions);
            FunctionResultContent toolResult = Assert.Single(client.Requests[1].SelectMany(message => message.Contents).OfType<FunctionResultContent>());
            JsonElement modelVisibleToolResult = Assert.IsType<JsonElement>(toolResult.Result);
            Assert.Equal(JsonValueKind.Object, modelVisibleToolResult.ValueKind);
            Assert.True(modelVisibleToolResult.TryGetProperty("search", out _));
            Assert.Equal(Encoding.UTF8.GetByteCount(modelVisibleToolResult.GetRawText()), wiki.Budget.ToolResultBytes);
            JsonElement[] qualifications = modelVisibleToolResult.GetProperty("search").GetProperty("documents")
                .EnumerateArray().SelectMany(document => document.GetProperty("qualifiedExcerpt").EnumerateArray())
                .Select(claim => claim.GetProperty("qualification")).ToArray();
            Assert.All(qualifications, value => Assert.Equal(JsonValueKind.String, value.ValueKind));
            Assert.Contains(qualifications, value => value.GetString() == "SourceSupported");
            Assert.Contains(qualifications, value => value.GetString() == "Advisory");
            Assert.DoesNotContain("PRIVATE", visible.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("技术代码 CL-A", visible.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("wiki-1", visible.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            finish.TrySetResult();
            await run;
        }

        OctoberChatProviderResult result = await run;
        Assert.Equal(OctoberChatProviderStatus.Completed, result.Status);
        Assert.Equal("已有证据。查阅完成，等待结束。", result.AnswerText);
        Assert.Equal(result.AnswerText, visible.ToString());
        Assert.Equal(0, client.NonStreamingCalls);
        Assert.Equal(2, client.Requests.Count);
        // Completed is this controlled generation result, never a durable TurnSaved projection.
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("unknown-tool")]
    [InlineData("missing-call-id")]
    [InlineData("authority-override")]
    public async Task RunAsync_RefusedNativeCall_IsCountedOnceWithoutExecutingReaderOrRetryAsync(string refusal)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([token => StreamAsync([
            Call("first", refusal == "missing-call-id" ? "" : "call-1", refusal == "unknown-tool" ? "ReadSource" : "BrowseWiki",
                refusal == "authority-override" ? new() { ["query"] = "", ["subject"] = "administrator" } : new() { ["query"] = "" }),
            Finish("first", ChatFinishReason.ToolCalls),
        ], token)]);

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { });

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Empty(fixture.Reader.ExactRequests);
        Assert.Equal(1, wiki.Budget.ToolCalls);
        Assert.Equal(0, wiki.Budget.ActiveTools);
        Assert.Single(client.Requests);
        Assert.Equal(0, client.NonStreamingCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_DuplicateNativeIdAcrossRounds_DoesNotExecuteSecondCallOrRecountFirstAsync(bool differentName)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([
            token => StreamAsync([Call("first", "same-id", "BrowseWiki", new() { ["query"] = "" }), Finish("first", ChatFinishReason.ToolCalls)], token),
            token => StreamAsync([Call("second", "same-id", differentName ? "SearchWiki" : "BrowseWiki", new() { ["query"] = "" }), Finish("second", ChatFinishReason.ToolCalls)], token),
        ]);

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { });

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Single(fixture.Reader.SearchRequests);
        Assert.Equal(2, wiki.Budget.ToolCalls);
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal(0, wiki.Budget.ActiveTools);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_DuplicateNativeIdsInSameRound_RejectsBeforeSdkInvokesEitherCallAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([token => StreamAsync([
            Call("first", "duplicate", "BrowseWiki", new() { ["query"] = "" }),
            Call("first", "duplicate", "SearchWiki", new() { ["query"] = "" }),
            Finish("first", ChatFinishReason.ToolCalls),
        ], token)]);

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { });

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Single(client.Requests);
        Assert.Equal(0, wiki.Budget.ActiveTools);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("length", "Interrupted")]
    [InlineData("content_filter", "Interrupted")]
    [InlineData("missing", "Failed")]
    [InlineData("unknown", "Failed")]
    [InlineData("tool_calls", "Failed")]
    public async Task RunAsync_NonFinalStop_DoesNotClaimSuccessfulCompletionAsync(string reason, string expected)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        var updates = new List<ChatResponseUpdate> { Update("first", new TextContent("允许保留的片段")) };
        if (reason != "missing") updates.Add(Finish("first", new ChatFinishReason(reason)));
        using var client = new ScriptedChatClient([token => StreamAsync(updates, token)]);
        var text = new StringBuilder();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", delta => text.Append(delta));

        Assert.Equal(Enum.Parse<OctoberChatProviderStatus>(expected), result.Status);
        Assert.Equal("允许保留的片段", result.AnswerText);
        Assert.Equal(result.AnswerText, text.ToString());
        Assert.Single(client.Requests);
        Assert.Equal(0, client.NonStreamingCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_StopWithOutstandingNativeFunction_FailsWithoutExecutingItAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([token => StreamAsync([
            Call("first", "unfinished", "BrowseWiki", new() { ["query"] = "" }), Finish("first", ChatFinishReason.Stop),
        ], token)]);

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { });

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Single(client.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_TrailingDataAfterFinalStop_FailsWithoutReleasingTrailingDataAsync(bool trailingTool)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([token => StreamAsync([
            Update("first", new TextContent("合法的前缀")),
            Finish("first", ChatFinishReason.Stop),
            trailingTool ? Call("first", "late", "BrowseWiki", new() { ["query"] = "" }) : Update("first", new TextContent("结束后的非法片段")),
        ], token)]);
        var visible = new StringBuilder();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", delta => visible.Append(delta));

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Equal("合法的前缀", visible.ToString());
        Assert.Equal(visible.ToString(), result.AnswerText);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Single(client.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_ProviderRoundBudget_StopsBeforeNextUnderlyingRequestAsync()
    {
        var fixture = new ControlledChatFixture();
        var budget = new OctoberChatBudget(new OctoberChatLimits { ProviderRounds = 1 });
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync(budget: budget);
        using var client = new ScriptedChatClient([token => StreamAsync([
            Call("first", "call-1", "BrowseWiki", new() { ["query"] = "" }), Finish("first", ChatFinishReason.ToolCalls),
        ], token)]);

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, budget)
            .RunAsync("问题", "controlled instructions", _ => { });

        Assert.Equal(OctoberChatProviderStatus.BudgetExceeded, result.Status);
        Assert.Single(client.Requests);
        Assert.Single(fixture.Reader.SearchRequests);
        Assert.Equal(1, budget.ProviderRounds);
        Assert.Equal(1, budget.ToolCalls);
        Assert.True(budget.IsStopped);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_OutputBudget_RejectsWholeNextUtf8DeltaBeforeCallbackAsync()
    {
        var fixture = new ControlledChatFixture();
        var budget = new OctoberChatBudget(new OctoberChatLimits { OutputBytes = 3 });
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync(budget: budget);
        using var client = new ScriptedChatClient([token => StreamAsync([
            Update("first", new TextContent("中")), Update("first", new TextContent("😀")), Finish("first", ChatFinishReason.Stop),
        ], token)]);
        var visible = new StringBuilder();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, budget)
            .RunAsync("问题", "controlled instructions", delta => visible.Append(delta));

        Assert.Equal(OctoberChatProviderStatus.BudgetExceeded, result.Status);
        Assert.Equal("中", visible.ToString());
        Assert.Equal("中", result.AnswerText);
        Assert.Equal(3, budget.OutputBytes);
        Assert.Single(client.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_OversizedNativeArguments_AreCountedOnceAndRejectedBeforeReaderAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([token => StreamAsync([
            Call("first", "large-call", "BrowseWiki", new() { ["query"] = new string('x', 16385) }), Finish("first", ChatFinishReason.ToolCalls),
        ], token)]);

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { });

        Assert.Equal(OctoberChatProviderStatus.BudgetExceeded, result.Status);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Equal(1, wiki.Budget.ToolCalls);
        Assert.Equal(0, wiki.Budget.ActiveTools);
        Assert.Single(client.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("call")]
    [InlineData("original")]
    [InlineData("deadline")]
    public async Task RunAsync_PendingClientLifetimeEnds_SettlesWithoutAnotherRequestAsync(string cause)
    {
        var fixture = new ControlledChatFixture();
        var clock = new ControlledChatFixture.ManualClock();
        using var original = new CancellationTokenSource();
        using var call = new CancellationTokenSource();
        var budget = new OctoberChatBudget(timeProvider: clock, cancellationToken: original.Token);
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync(budget: budget);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ScriptedChatClient([token => PendingAsync(token)]);
        Task<OctoberChatProviderResult> run = new OctoberChatAgent(client, wiki.Tools, budget)
            .RunAsync("问题", "controlled instructions", _ => { }, call.Token);
        try
        {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            if (cause == "call") call.Cancel();
            else if (cause == "original") original.Cancel();
            else clock.Advance(TimeSpan.FromSeconds(180));
            OctoberChatProviderResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(cause == "deadline" ? OctoberChatProviderStatus.BudgetExceeded : OctoberChatProviderStatus.Cancelled, result.Status);
            Assert.Single(client.Requests);
            Assert.Equal(0, budget.ActiveTools);
            Assert.Empty(fixture.Reader.SearchRequests);
        }
        finally
        {
            gate.TrySetResult();
            await run;
        }

        async IAsyncEnumerable<ChatResponseUpdate> PendingAsync([EnumeratorCancellation] CancellationToken token)
        {
            entered.TrySetResult();
            await gate.Task.WaitAsync(token);
            yield return Finish("first", ChatFinishReason.Stop);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("question")]
    [InlineData("instructions")]
    public async Task RunAsync_MissingInput_DoesNotCallClientOrUseFallbackPromptAsync(string missing)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([]);

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync(missing == "question" ? " " : "问题", missing == "instructions" ? " " : "controlled instructions", _ => { });

        Assert.NotEqual(OctoberChatProviderStatus.Completed, result.Status);
        Assert.Empty(client.Requests);
        Assert.Empty(result.AnswerText);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Constructor_DifferentFacadeBudget_RejectsSplitTurnAccountingAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([]);

        Assert.Throws<ArgumentException>(() => new OctoberChatAgent(client, wiki.Tools, new OctoberChatBudget()));
        Assert.Empty(client.Requests);
        Assert.Empty(fixture.Reader.SearchRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("oversized-instructions")]
    [InlineData("invalid-utf16-instructions")]
    [InlineData("oversized-question")]
    public async Task RunAsync_InvalidBoundedInput_RejectsBeforeClientWithoutTruncationAsync(string invalid)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([]);
        string instructions = invalid switch
        {
            "oversized-instructions" => new string('x', 32769),
            "invalid-utf16-instructions" => "\ud800",
            _ => "controlled instructions",
        };

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync(invalid == "oversized-question" ? new string('问', 8001) : "问题", instructions, _ => { });

        Assert.NotEqual(OctoberChatProviderStatus.Completed, result.Status);
        Assert.Empty(client.Requests);
        Assert.Empty(result.AnswerText);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_NativeArgumentMappingException_RejectsBeforeInvocationAndHidesErrorAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        var malformed = new FunctionCallContent("mapping-error", "BrowseWiki", new Dictionary<string, object?> { ["query"] = "" })
        {
            Exception = new JsonException("PRIVATE raw provider argument parsing failure"),
        };
        using var client = new ScriptedChatClient([token => StreamAsync([
            Update("first", malformed), Finish("first", ChatFinishReason.ToolCalls),
        ], token)]);
        var text = new StringBuilder();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", delta => text.Append(delta));

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Equal(1, wiki.Budget.ToolCalls);
        Assert.Empty(text.ToString());
        Assert.Empty(result.AnswerText);
        Assert.Single(client.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_InformationalNativeCall_IsIgnoredWithoutExecutionOrPendingToolAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        var informational = new FunctionCallContent("already-processed", "BrowseWiki", new Dictionary<string, object?> { ["query"] = "" })
        {
            InformationalOnly = true,
        };
        using var client = new ScriptedChatClient([token => StreamAsync([
            Update("first", informational), Update("first", new TextContent("正常文本")), Finish("first", ChatFinishReason.Stop),
        ], token)]);
        var text = new StringBuilder();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", delta => text.Append(delta));

        Assert.Equal(OctoberChatProviderStatus.Completed, result.Status);
        Assert.Equal("正常文本", text.ToString());
        Assert.Equal(text.ToString(), result.AnswerText);
        Assert.Equal(0, wiki.Budget.ToolCalls);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Empty(wiki.Ledger.Snapshot);
        Assert.Single(client.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_UsageOnlyMetadataAfterStop_PreservesValidCompletionWithoutExtraTextAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([token => StreamAsync([
            Update("first", new TextContent("完整文本")), Finish("first", ChatFinishReason.Stop),
            Update("first", new UsageContent(new UsageDetails { InputTokenCount = 3, OutputTokenCount = 2, TotalTokenCount = 5 })),
        ], token)]);
        var text = new StringBuilder();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", delta => text.Append(delta));

        Assert.Equal(OctoberChatProviderStatus.Completed, result.Status);
        Assert.Equal("完整文本", text.ToString());
        Assert.Equal(text.ToString(), result.AnswerText);
        Assert.Equal(Encoding.UTF8.GetByteCount("完整文本"), wiki.Budget.OutputBytes);
        Assert.Single(client.Requests);
        Assert.Equal(0, wiki.Budget.ToolCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_SameBoundAgentReused_RejectsSecondGenerationWithoutCancellingFirstAsync(bool reentrant)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ScriptedChatClient([token => StreamAsync(
            [Update("first", new TextContent("原始生成"))], token, finish.Task, Finish("first", ChatFinishReason.Stop))]);
        var agent = new OctoberChatAgent(client, wiki.Tools, wiki.Budget);
        Task<OctoberChatProviderResult> first = agent.RunAsync("原问题", "controlled instructions", _ => observed.TrySetResult());
        try
        {
            Assert.Same(observed.Task, await Task.WhenAny(observed.Task, first).WaitAsync(TimeSpan.FromSeconds(5)));
            if (!reentrant)
            {
                finish.TrySetResult();
                Assert.Equal(OctoberChatProviderStatus.Completed, (await first).Status);
            }

            OctoberChatProviderResult second = await agent.RunAsync("再次生成", "controlled instructions", _ => { });

            Assert.Equal(OctoberChatProviderStatus.Failed, second.Status);
            Assert.Empty(second.AnswerText);
            Assert.Single(client.Requests);
            Assert.Equal(1, wiki.Budget.ProviderRounds);
            if (reentrant) Assert.False(first.IsCompleted);
        }
        finally
        {
            finish.TrySetResult();
            await first;
        }

        Assert.Equal(OctoberChatProviderStatus.Completed, (await first).Status);
        Assert.Equal("原始生成", (await first).AnswerText);
        // This protects one in-memory bound agent, not durable or cross-instance turn admission.
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_NonCooperativePendingMoveNext_SettlesOriginalLifetimeBeforeOwnerReleasesAsync(bool deadline)
    {
        var fixture = new ControlledChatFixture();
        var clock = new ControlledChatFixture.ManualClock();
        using var lifetime = new CancellationTokenSource();
        var budget = new OctoberChatBudget(timeProvider: clock, cancellationToken: lifetime.Token);
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync(budget: budget);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ScriptedChatClient([_ => NonCooperativeAsync()]);
        var text = new StringBuilder();
        Task<OctoberChatProviderResult> run = new OctoberChatAgent(client, wiki.Tools, budget)
            .RunAsync("问题", "controlled instructions", delta => text.Append(delta));
        try
        {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            if (deadline) clock.Advance(TimeSpan.FromSeconds(180));
            else lifetime.Cancel();

            OctoberChatProviderResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(deadline ? OctoberChatProviderStatus.BudgetExceeded : OctoberChatProviderStatus.Cancelled, result.Status);
            Assert.False(release.Task.IsCompleted);
            Assert.Empty(result.AnswerText);
            Assert.Empty(text.ToString());
            Assert.Single(client.Requests);
            Assert.Equal(0, client.DisposeCalls);
        }
        finally
        {
            release.TrySetResult();
            await run;
            await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Empty(text.ToString());
        Assert.Single(client.Requests);
        Assert.Equal(0, client.DisposeCalls);

        async IAsyncEnumerable<ChatResponseUpdate> NonCooperativeAsync()
        {
            try
            {
                entered.TrySetResult();
                await release.Task; // Deliberately ignores the injected cancellation token.
                yield return Update("first", new TextContent("取消后不得发布的片段"));
                yield return Finish("first", ChatFinishReason.Stop);
            }
            finally
            {
                disposed.TrySetResult();
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_PreTerminalNativeInputExceedsLimit_StopsBeforeWaitingForFinishOrInvokingReaderAsync(bool oversizedArguments)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        var limitInputProduced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ScriptedChatClient([token => PendingFinishAsync(token)]);
        Task<OctoberChatProviderResult> run = new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { });
        try
        {
            Assert.Same(limitInputProduced.Task, await Task.WhenAny(limitInputProduced.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));

            OctoberChatProviderResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(OctoberChatProviderStatus.BudgetExceeded, result.Status);
            Assert.False(release.Task.IsCompleted);
            Assert.Empty(fixture.Reader.SearchRequests);
            Assert.Single(client.Requests);
            Assert.True(wiki.Budget.IsStopped);
            Assert.Equal(oversizedArguments ? 1 : 16, wiki.Budget.ToolCalls);
            Assert.Equal(0, wiki.Budget.ActiveTools);
        }
        finally
        {
            release.TrySetResult();
            await run;
        }

        async IAsyncEnumerable<ChatResponseUpdate> PendingFinishAsync([EnumeratorCancellation] CancellationToken token)
        {
            int count = oversizedArguments ? 1 : 17;
            for (int index = 0; index < count; index++)
            {
                if (index == count - 1) limitInputProduced.TrySetResult();
                yield return Call("first", $"distinct-{index}", "BrowseWiki", new()
                {
                    ["query"] = oversizedArguments ? new string('x', 16385) : "",
                });
            }

            await release.Task.WaitAsync(token);
            yield return Finish("first", ChatFinishReason.ToolCalls);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_NonCooperativeDispose_SettlesLifetimeWhileOwnedCleanupRemainsPendingAsync(bool deadline)
    {
        var fixture = new ControlledChatFixture();
        var clock = new ControlledChatFixture.ManualClock();
        using var lifetime = new CancellationTokenSource();
        var budget = new OctoberChatBudget(timeProvider: clock, cancellationToken: lifetime.Token);
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync(budget: budget);
        var sequence = new PendingDisposeSequence([
            Update("first", new TextContent("已收到的片段")), Finish("first", ChatFinishReason.Stop),
        ]);
        using var client = new ScriptedChatClient([_ => sequence]);
        var text = new StringBuilder();
        Task<OctoberChatProviderResult> run = new OctoberChatAgent(client, wiki.Tools, budget)
            .RunAsync("问题", "controlled instructions", delta => text.Append(delta));
        try
        {
            Assert.Same(sequence.DisposeEntered.Task, await Task.WhenAny(sequence.DisposeEntered.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            if (deadline) clock.Advance(TimeSpan.FromSeconds(180));
            else lifetime.Cancel();

            OctoberChatProviderResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(deadline ? OctoberChatProviderStatus.BudgetExceeded : OctoberChatProviderStatus.Cancelled, result.Status);
            Assert.False(sequence.ReleaseDispose.Task.IsCompleted);
            Assert.False(sequence.DisposeCompleted.Task.IsCompleted);
            Assert.Equal("已收到的片段", result.AnswerText);
            Assert.Equal(result.AnswerText, text.ToString());
            Assert.Single(client.Requests);
            Assert.Equal(1, sequence.DisposeCalls);
            Assert.Equal(0, client.DisposeCalls);
        }
        finally
        {
            sequence.ReleaseDispose.TrySetResult();
            await run;
            await sequence.DisposeCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(1, sequence.DisposeCalls);
        Assert.Equal(0, client.DisposeCalls);
        Assert.Equal("已收到的片段", text.ToString());
        Assert.Single(client.Requests);
    }

    private static ChatResponseUpdate Call(string responseId, string callId, string name, Dictionary<string, object?> arguments) =>
        Update(responseId, new FunctionCallContent(callId, name, arguments));

    private static ChatResponseUpdate Update(string id, AIContent content) => new()
    {
        Role = ChatRole.Assistant,
        ResponseId = id,
        MessageId = id + "-message",
        Contents = [content],
    };

    private static ChatResponseUpdate Finish(string id, ChatFinishReason reason) => new()
    {
        Role = ChatRole.Assistant,
        ResponseId = id,
        MessageId = id + "-message",
        FinishReason = reason,
    };

    private static async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IReadOnlyList<ChatResponseUpdate> updates, [EnumeratorCancellation] CancellationToken token,
        Task? gate = null, ChatResponseUpdate? final = null)
    {
        foreach (ChatResponseUpdate update in updates)
        {
            token.ThrowIfCancellationRequested();
            yield return update;
        }
        if (gate is not null) await gate.WaitAsync(token);
        if (final is not null) yield return final;
    }

    private sealed class ScriptedChatClient(IReadOnlyList<Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>>> scripts) : IChatClient
    {
        public List<ChatMessage[]> Requests { get; } = [];
        public List<string[]> ToolNames { get; } = [];
        public int NonStreamingCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            NonStreamingCalls++;
            throw new InvalidOperationException("The controlled capability probe only supplies typed streaming updates.");
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            int index = Requests.Count;
            Requests.Add(messages.ToArray());
            ToolNames.Add(options?.Tools?.Select(tool => tool.Name).ToArray() ?? []);
            if (index >= scripts.Count) throw new InvalidOperationException("Unexpected extra controlled SDK request.");
            return scripts[index](cancellationToken);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() => DisposeCalls++;
    }

    private sealed class PendingDisposeSequence(IReadOnlyList<ChatResponseUpdate> updates)
        : IAsyncEnumerable<ChatResponseUpdate>, IAsyncEnumerator<ChatResponseUpdate>
    {
        private int _index = -1;
        public TaskCompletionSource DisposeEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDispose { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DisposeCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisposeCalls { get; private set; }
        public ChatResponseUpdate Current => updates[_index];
        public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(++_index < updates.Count);

        public async ValueTask DisposeAsync()
        {
            DisposeCalls++;
            DisposeEntered.TrySetResult();
            await ReleaseDispose.Task;
            DisposeCompleted.TrySetResult();
        }
    }
}

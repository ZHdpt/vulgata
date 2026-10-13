using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
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
    public async Task RunAsync_BufferedTinyProviderDeltas_AreBoundedBatchesFlushedBeforeProviderWaitAsync()
    {
        var fixture = new ControlledChatFixture();
        var wiki = await fixture.OpenWikiAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var visible = new StringBuilder();
        int callbacks = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ScriptedChatClient([token => StreamAsync(
            Enumerable.Range(0, 200).Select(_ => Update("text", new TextContent("界"))).ToArray(),
            token, release.Task, Finish("text", ChatFinishReason.Stop))]);
        Task<OctoberChatProviderResult> pending = new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", delta =>
            {
                callbacks++;
                visible.Append(delta);
                if (visible.Length == 200) ready.TrySetResult();
            });
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.InRange(callbacks, 2, 5);
            release.TrySetResult();
            var result = await pending;
            Assert.Equal(new string('界', 200), result.AnswerText);
            Assert.Equal(OctoberChatProviderStatus.Completed, result.Status);
            Assert.Equal(600, wiki.Budget.OutputBytes);
        }
        finally { release.TrySetResult(); await pending; }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(1000)]
    public async Task RunAsync_ToolSchemasAdvertiseEffectivePageSizeLimitAsync(int pageSize)
    {
        var fixture = new ControlledChatFixture();
        var budget = new OctoberChatBudget(new OctoberChatLimits { PageSize = pageSize });
        var wiki = await fixture.OpenWikiAsync(budget: budget);
        using var client = new ScriptedChatClient([token => StreamAsync([Finish("done", ChatFinishReason.Stop)], token)]);
        await new OctoberChatAgent(client, wiki.Tools, budget).RunAsync("问题", "controlled instructions", _ => { });
        foreach (string name in new[] { "SearchWiki", "BrowseWiki", "GetRelatedDocuments" })
        {
            var schema = client.ToolSchemas[0][name].GetProperty("properties").GetProperty("pageSize");
            Assert.Equal(1, schema.GetProperty("minimum").GetInt32());
            Assert.Equal(Math.Min(pageSize, Vulgata.Core.DomainServices.OctoberKnowledge.KnowledgeContract.MaximumPageSize),
                schema.GetProperty("maximum").GetInt32());
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_CitationRegistrationAfterAnswer_DoesNotDiscardAnswerAsRetrievalNarrationAsync()
    {
        var fixture = new ControlledChatFixture();
        var wiki = await fixture.OpenWikiAsync();
        var progress = new List<OctoberChatToolProgress>();
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Update("answer", new TextContent("完整回答。")),
                Call("answer", "report", "ReportAnswerDocuments", new() { ["documents"] = Array.Empty<object>() }),
                Finish("answer", ChatFinishReason.ToolCalls)
            ], token),
            token => StreamAsync([Finish("done", ChatFinishReason.Stop)], token)
        ]);
        var result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { }, onToolProgress: progress.Add);
        Assert.Equal(OctoberChatProviderStatus.Completed, result.Status);
        Assert.Equal("完整回答。", result.AnswerText);
        Assert.Empty(progress); // Reference bookkeeping is not a retrieval boundary.
        Assert.Equal(1, wiki.Budget.ToolCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_ProviderStreamDisconnect_PreservesPartialTextAsInterruptedWithoutRetryAsync()
    {
        var fixture = new ControlledChatFixture();
        var wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([token => DisconnectedAsync(token)]);
        var result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { });
        Assert.Equal(OctoberChatProviderStatus.Interrupted, result.Status);
        Assert.Equal(DialogueProviderFinish.Truncated, result.ProviderFinish);
        Assert.Equal("已收到的部分回答", result.AnswerText);
        Assert.Single(client.Requests);

        static async IAsyncEnumerable<ChatResponseUpdate> DisconnectedAsync([EnumeratorCancellation] CancellationToken token)
        {
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            yield return Update("disconnected", new TextContent("已收到的部分回答"));
            throw new IOException("Controlled stream disconnect.");
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_EachRoundReceivesActualRemainingBudgetWithoutStaleCopiesAsync()
    {
        var fixture = new ControlledChatFixture();
        var wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([
            token => StreamAsync([Call("first", "budget-call", "BrowseWiki", new() { ["query"] = "" }), Finish("first", ChatFinishReason.ToolCalls)], token),
            token => StreamAsync([Update("second", new TextContent("有边界的回答。")), Finish("second", ChatFinishReason.Stop)], token)
        ]);
        var result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { });
        Assert.Equal(OctoberChatProviderStatus.Completed, result.Status);
        for (int index = 0; index < 2; index++)
        {
            var message = Assert.Single(client.Requests[index].Where(m => m.Role == ChatRole.System && m.Text.Contains("october_chat_budget", StringComparison.Ordinal)));
            using var json = JsonDocument.Parse(message.Text);
            Assert.Equal(wiki.Budget.Limits.ProviderRounds, json.RootElement.GetProperty("providerRoundsLimit").GetInt32());
            Assert.Equal(wiki.Budget.Limits.ToolCalls, json.RootElement.GetProperty("toolCallsLimit").GetInt32());
            Assert.Equal(index + 1, json.RootElement.GetProperty("currentProviderRound").GetInt32());
            Assert.Equal(wiki.Budget.Limits.ProviderRounds - index - 1, json.RootElement.GetProperty("providerRoundsRemainingAfterCurrent").GetInt32());
            Assert.Equal(wiki.Budget.Limits.ToolCalls - index, json.RootElement.GetProperty("toolCallsRemaining").GetInt32());
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_OriginalCancellationBeforeLinkedPropagationPreservesCauseAndLateCancellationCannotRewriteFailureAsync(bool cancellationFirst)
    {
        var fixture = new ControlledChatFixture();
        using var original = new CancellationTokenSource();
        var budget = new OctoberChatBudget(cancellationToken: original.Token);
        var wiki = await fixture.OpenWikiAsync(budget: budget);
        var streamEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failStream = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var propagationBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releasePropagation = new ManualResetEventSlim(false);
        CancellationToken observedLinked = default;
        bool? originalAtFailure = null;
        bool? linkedAtFailure = null;
        CancellationToken exceptionToken = default;
        using var client = new ScriptedChatClient([token => OriginalAwareFailureAsync(token)]);
        var visible = new StringBuilder();
        var pending = new OctoberChatAgent(client, wiki.Tools, budget)
            .RunAsync("问题", "controlled instructions", text => visible.Append(text));
        CancellationTokenRegistration blocker = default;
        Task cancel = Task.CompletedTask;
        try
        {
            await streamEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancellationFirst)
            {
                // Registered after the agent's linked source: CancellationTokenSource's
                // LIFO callback order holds propagation while the original signal is true.
                blocker = original.Token.Register(() =>
                {
                    propagationBlocked.TrySetResult();
                    releasePropagation.Wait();
                });
                cancel = original.CancelAsync();
                await propagationBlocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(original.IsCancellationRequested);
                Assert.False(observedLinked.IsCancellationRequested);
            }

            failStream.TrySetResult();
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(cancellationFirst, originalAtFailure);
            Assert.False(linkedAtFailure);
            Assert.Equal("可保留部分。", visible.ToString());
            Assert.Equal("可保留部分。", result.AnswerText);
            Assert.Equal(cancellationFirst ? OctoberChatProviderStatus.Cancelled : OctoberChatProviderStatus.Failed, result.Status);
            Assert.Equal(cancellationFirst ? DialogueProviderFinish.Cancelled : DialogueProviderFinish.Unknown, result.ProviderFinish);
            if (cancellationFirst) Assert.Equal(original.Token, exceptionToken);
            else
            {
                original.Cancel();
                Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
                Assert.Equal(DialogueProviderFinish.Unknown, result.ProviderFinish);
            }

            Assert.Single(client.Requests);
            Assert.Equal(0, client.DisposeCalls);
        }
        finally
        {
            failStream.TrySetResult();
            releasePropagation.Set();
            await cancel.WaitAsync(TimeSpan.FromSeconds(5));
            blocker.Dispose();
            _ = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        async IAsyncEnumerable<ChatResponseUpdate> OriginalAwareFailureAsync([EnumeratorCancellation] CancellationToken token)
        {
            observedLinked = token;
            yield return Update("original-lifetime", new TextContent("可保留部分。"));
            streamEntered.TrySetResult();
            await failStream.Task;
            originalAtFailure = original.IsCancellationRequested;
            linkedAtFailure = token.IsCancellationRequested;
            if (original.IsCancellationRequested)
            {
                exceptionToken = original.Token;
                throw new OperationCanceledException("Controlled original-lifetime cancellation before linked propagation.", original.Token);
            }

            throw new InvalidOperationException("Controlled failure before any cancellation signal.");
        }
    }

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
            await foreach (AgentResponseUpdate _ in agent.RunStreamingAsync("受控取消测试", cancellationToken: cancellation.Token))
            {
            }
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
        Assert.Equal("查阅完成，等待结束。", result.AnswerText);
        Assert.Equal("已有证据。查阅完成，等待结束。", visible.ToString());
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
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Call("first", refusal == "missing-call-id" ? "" : "call-1", refusal == "unknown-tool" ? "ReadSource" : "BrowseWiki",
                    refusal == "authority-override" ? new() { ["query"] = "", ["subject"] = "administrator" } : new() { ["query"] = "" }),
                Finish("first", ChatFinishReason.ToolCalls),
            ], token)
        ]);

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
            token => StreamAsync([Call("second", "same-id", differentName ? "SearchWiki" : "BrowseWiki", new() { ["query"] = "" }), Finish("second", ChatFinishReason.ToolCalls)],
                token),
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
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Call("first", "duplicate", "BrowseWiki", new() { ["query"] = "" }),
                Call("first", "duplicate", "SearchWiki", new() { ["query"] = "" }),
                Finish("first", ChatFinishReason.ToolCalls),
            ], token)
        ]);

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
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Call("first", "unfinished", "BrowseWiki", new() { ["query"] = "" }), Finish("first", ChatFinishReason.Stop),
            ], token)
        ]);

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
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Update("first", new TextContent("合法的前缀")),
                Finish("first", ChatFinishReason.Stop),
                trailingTool ? Call("first", "late", "BrowseWiki", new() { ["query"] = "" }) : Update("first", new TextContent("结束后的非法片段")),
            ], token)
        ]);
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
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Call("first", "call-1", "BrowseWiki", new() { ["query"] = "" }), Finish("first", ChatFinishReason.ToolCalls),
            ], token)
        ]);

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
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Update("first", new TextContent("中")), Update("first", new TextContent("😀")), Finish("first", ChatFinishReason.Stop),
            ], token)
        ]);
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
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Call("first", "large-call", "BrowseWiki", new() { ["query"] = new string('x', 16385) }), Finish("first", ChatFinishReason.ToolCalls),
            ], token)
        ]);

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
            else clock.Advance(new OctoberChatLimits().Duration);
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
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Update("first", malformed), Finish("first", ChatFinishReason.ToolCalls),
            ], token)
        ]);
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
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Update("first", informational), Update("first", new TextContent("正常文本")), Finish("first", ChatFinishReason.Stop),
            ], token)
        ]);
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
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Update("first", new TextContent("完整文本")), Finish("first", ChatFinishReason.Stop),
                Update("first", new UsageContent(new UsageDetails { InputTokenCount = 3, OutputTokenCount = 2, TotalTokenCount = 5 })),
            ], token)
        ]);
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
        using var client = new ScriptedChatClient([
            token => StreamAsync(
                [Update("first", new TextContent("原始生成"))], token, finish.Task, Finish("first", ChatFinishReason.Stop))
        ]);
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
            if (deadline) clock.Advance(new OctoberChatLimits().Duration);
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
            Assert.Equal(oversizedArguments ? 1 : wiki.Budget.Limits.ToolCalls, wiki.Budget.ToolCalls);
            Assert.Equal(0, wiki.Budget.ActiveTools);
        }
        finally
        {
            release.TrySetResult();
            await run;
        }

        async IAsyncEnumerable<ChatResponseUpdate> PendingFinishAsync([EnumeratorCancellation] CancellationToken token)
        {
            int count = oversizedArguments ? 1 : wiki.Budget.Limits.ToolCalls + 1;
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
            if (deadline) clock.Advance(new OctoberChatLimits().Duration);
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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_ToolProgress_GatedReadReportsActualInvocationAndPreservesImmediateTextAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        var readGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Reader.ExactGate = readGate;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Guid privateProviderId = Guid.NewGuid();
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Update("private-response", new TextContent("先到达的文本。")),
                Call("private-response", privateProviderId.ToString(), "ReadWiki", new() { ["document"] = fixture.Knowledge.Handle(fixture.Knowledge.ClA) }),
                Finish("private-response", ChatFinishReason.ToolCalls),
            ], token),
            token => StreamAsync([
                Call("second", "private-browse-id", "BrowseWiki", new() { ["query"] = "" }),
                Finish("second", ChatFinishReason.ToolCalls),
            ], token),
            token => StreamAsync([Update("third", new TextContent("工具完成后的文本。"))], token, finish.Task, Finish("third", ChatFinishReason.Stop)),
        ]);
        var progress = new List<OctoberChatToolProgress>();
        var visible = new StringBuilder();
        Task<OctoberChatProviderResult> run = new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", delta =>
            {
                visible.Append(delta);
                if (visible.ToString().Contains("工具完成后", StringComparison.Ordinal)) answer.TrySetResult();
            }, onToolProgress: item =>
            {
                progress.Add(item);
                if (item.Kind == OctoberChatToolProgressKind.Started) started.TrySetResult();
            });
        try
        {
            Assert.Same(started.Task, await Task.WhenAny(started.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal("先到达的文本。", visible.ToString());
            Assert.False(run.IsCompleted);
            OctoberChatToolProgress pending = Assert.Single(progress);
            Assert.Equal(OctoberChatToolProgressKind.Started, pending.Kind);
            Assert.NotEqual(Guid.Empty, pending.OperationId);
            Assert.NotEqual(privateProviderId, pending.OperationId);
            Assert.Single(fixture.Reader.ExactRequests);
            Assert.Empty(wiki.Ledger.Snapshot);

            readGate.TrySetResult();
            Assert.Same(answer.Task, await Task.WhenAny(answer.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(run.IsCompleted);
            Assert.Equal(new[]
            {
                OctoberChatToolProgressKind.Started, OctoberChatToolProgressKind.Completed,
                OctoberChatToolProgressKind.Started, OctoberChatToolProgressKind.Completed
            }, progress.Select(item => item.Kind));
            Assert.Equal(progress[0].OperationId, progress[1].OperationId);
            Assert.Equal(progress[2].OperationId, progress[3].OperationId);
            Assert.NotEqual(progress[0].OperationId, progress[2].OperationId);
            Assert.NotEqual(Guid.Empty, progress[2].OperationId);
            Assert.Single(wiki.Ledger.Snapshot);
            Assert.Single(fixture.Reader.SearchRequests);
            Assert.Equal(3, client.Requests.Count);
            using JsonDocument projection = JsonDocument.Parse(JsonSerializer.Serialize(progress));
            Assert.All(projection.RootElement.EnumerateArray(), item =>
                Assert.Equal(new[] { "Kind", "OperationId" }, item.EnumerateObject().Select(property => property.Name).Order()));
        }
        finally
        {
            readGate.TrySetResult();
            finish.TrySetResult();
            await run;
        }

        Assert.Equal(OctoberChatProviderStatus.Completed, (await run).Status);
        Assert.Equal("先到达的文本。工具完成后的文本。", visible.ToString());
        Assert.Equal("工具完成后的文本。", (await run).AnswerText);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_ToolProgress_FacadeFailureReportsFailedWithoutClaimingToolSuccessAsync(bool invalidArguments)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        fixture.Reader.SearchStatus = KnowledgeReadStatus.Unavailable;
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Call("private-response", "private-call", "BrowseWiki", invalidArguments
                    ? new() { ["query"] = "", ["subject"] = "private-authority-override" } : new() { ["query"] = "" }),
                Finish("private-response", ChatFinishReason.ToolCalls),
            ], token),
            token => StreamAsync([Update("second", new TextContent("当前资料不可用。")), Finish("second", ChatFinishReason.Stop)], token),
        ]);
        var progress = new List<OctoberChatToolProgress>();
        var visible = new StringBuilder();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", delta => visible.Append(delta), onToolProgress: progress.Add);

        Assert.Equal(new[] { OctoberChatToolProgressKind.Started, OctoberChatToolProgressKind.Failed }, progress.Select(item => item.Kind));
        Assert.NotEqual(Guid.Empty, progress[0].OperationId);
        Assert.Equal(progress[0].OperationId, progress[1].OperationId);
        Assert.Equal(invalidArguments ? OctoberChatProviderStatus.Failed : OctoberChatProviderStatus.Completed, result.Status);
        Assert.Equal(invalidArguments ? 0 : 1, fixture.Reader.SearchRequests.Count);
        Assert.Equal(invalidArguments ? 1 : 2, client.Requests.Count);
        Assert.Equal(invalidArguments ? "" : "当前资料不可用。", visible.ToString());
        Assert.Empty(wiki.Ledger.Snapshot);
        Assert.Equal(0, wiki.Budget.ActiveTools);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_ToolProgress_PreDispatchRejectionDoesNotInventInvocationAsync(bool duplicate)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        var updates = new List<ChatResponseUpdate>
        {
            Call("first", "private-id", duplicate ? "BrowseWiki" : "UnapprovedPrivateTool", new() { ["query"] = "" }),
        };
        if (duplicate) updates.Add(Call("first", "private-id", "BrowseWiki", new() { ["query"] = "" }));
        updates.Add(Finish("first", ChatFinishReason.ToolCalls));
        using var client = new ScriptedChatClient([token => StreamAsync(updates, token)]);
        var progress = new List<OctoberChatToolProgress>();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { }, onToolProgress: progress.Add);

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Empty(progress);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Single(client.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_ToolProgress_CancelledReadReportsOneFailureBeforeReturnAndNoLateCallbacksAsync(bool deadline)
    {
        var fixture = new ControlledChatFixture();
        var clock = new ControlledChatFixture.ManualClock();
        using var cancellation = new CancellationTokenSource();
        var budget = new OctoberChatBudget(timeProvider: clock, cancellationToken: cancellation.Token);
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync(budget: budget);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Reader.ExactGate = gate;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Call("first", "private-read-id", "ReadWiki", new() { ["document"] = fixture.Knowledge.Handle(fixture.Knowledge.ClA) }),
                Finish("first", ChatFinishReason.ToolCalls),
            ], token)
        ]);
        var progress = new List<OctoberChatToolProgress>();
        Task<OctoberChatProviderResult> run = new OctoberChatAgent(client, wiki.Tools, budget)
            .RunAsync("问题", "controlled instructions", _ => { }, onToolProgress: item =>
            {
                progress.Add(item);
                if (item.Kind == OctoberChatToolProgressKind.Started) started.TrySetResult();
            });
        try
        {
            Assert.Same(started.Task, await Task.WhenAny(started.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Single(fixture.Reader.ExactRequests);
            if (deadline) clock.Advance(new OctoberChatLimits().Duration);
            else cancellation.Cancel();
            OctoberChatProviderResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(deadline ? OctoberChatProviderStatus.BudgetExceeded : OctoberChatProviderStatus.Cancelled, result.Status);
            Assert.False(gate.Task.IsCompleted);
            Assert.Equal(new[] { OctoberChatToolProgressKind.Started, OctoberChatToolProgressKind.Failed }, progress.Select(item => item.Kind));
            Assert.Equal(progress[0].OperationId, progress[1].OperationId);
            Assert.NotEqual(Guid.Empty, progress[0].OperationId);
        }
        finally
        {
            cancellation.Cancel();
            gate.TrySetResult();
            await run;
        }

        Assert.Equal(2, progress.Count);
        Assert.Empty(wiki.Ledger.Snapshot);
        Assert.Single(client.Requests);
        Assert.Equal(0, budget.ActiveTools);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_ToolProgress_ThrowingSinkStopsWithoutRecursiveNotificationOrFurtherDispatchAsync(bool throwOnCompletion)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        using var client = new ScriptedChatClient([
            token => StreamAsync([
                Call("first", "private-id", "BrowseWiki", new() { ["query"] = "" }),
                Finish("first", ChatFinishReason.ToolCalls),
            ], token)
        ]);
        var progress = new List<OctoberChatToolProgress>();
        var visible = new StringBuilder();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", delta => visible.Append(delta), onToolProgress: item =>
            {
                progress.Add(item);
                if (!throwOnCompletion || item.Kind == OctoberChatToolProgressKind.Completed)
                    throw new InvalidOperationException("PRIVATE progress sink failure");
            });

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Equal(throwOnCompletion ? 2 : 1, progress.Count);
        Assert.DoesNotContain(progress, item => item.Kind == OctoberChatToolProgressKind.Failed);
        Assert.Equal(throwOnCompletion ? 1 : 0, fixture.Reader.SearchRequests.Count);
        Assert.Single(client.Requests);
        Assert.Empty(visible.ToString());
        Assert.Empty(result.AnswerText);
        Assert.Equal(0, wiki.Budget.ActiveTools);
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
        public List<Dictionary<string, JsonElement>> ToolSchemas { get; } = [];
        public int NonStreamingCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            NonStreamingCalls++;
            throw new InvalidOperationException("The controlled capability probe only supplies typed streaming updates.");
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            int index = Requests.Count;
            Requests.Add(messages.ToArray());
            ToolNames.Add(options?.Tools?.Select(tool => tool.Name).ToArray() ?? []);
            ToolSchemas.Add(options?.Tools?.OfType<AIFunction>().ToDictionary(tool => tool.Name, tool => tool.JsonSchema) ?? []);
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

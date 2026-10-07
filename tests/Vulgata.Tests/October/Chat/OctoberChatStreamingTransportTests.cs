using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

/// <summary>Installed SDK over owned in-memory HTTP responses only; no endpoint, credential, host or live admission qualification.</summary>
public sealed class OctoberChatStreamingTransportTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task RunAsync_ActualSdkSplitBytesAndNativeFragments_StreamsTextBeforeFinishAndContinuesToolsAsync(string newline)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        string prefix = Event("{\"role\":\"assistant\",\"content\":\"先到😀。\"}");
        string suffix = Event("{\"tool_calls\":[{\"index\":0,\"type\":\"function\",\"function\":{\"arguments\":\"{\\\"query\\\":\"}}]}") +
            Event("{\"tool_calls\":[{\"index\":1,\"id\":\"native-b\",\"type\":\"function\",\"function\":{\"name\":\"SearchWiki\",\"arguments\":\"{\\\"query\\\":\"}}]}") +
            Event("{\"tool_calls\":[{\"index\":0,\"id\":\"native-a\",\"function\":{\"name\":\"BrowseWiki\",\"arguments\":\"\\\"\\\"}\"}},{\"index\":1,\"function\":{\"arguments\":\"\\\"\\\"}\"}}]}") +
            Event("{}", "tool_calls") + Done;
        var first = new ControlledStream(Bytes(prefix.Replace("\n", newline)), Bytes(suffix.Replace("\n", newline)), gated: true);
        var second = new ControlledStream(Bytes(Event("{\"content\":\"查阅后😀。\"}", id: "round-2")),
            Bytes(Event("{}", "stop", "round-2") + "data: {\"id\":\"round-2\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"controlled-model\",\"choices\":[],\"usage\":{\"prompt_tokens\":8,\"completion_tokens\":5,\"total_tokens\":13}}\n\n: controlled comment\n\n" + Done + " \r\n"), gated: true);
        using var physical = new ControlledHandler(first, second);
        using var guard = new OctoberChatStreamingHandler(physical, wiki.Budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        using var client = new ObservedChatClient(CreateSdkClient(http));
        var firstText = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextText = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var visible = new StringBuilder();
        var progress = new List<OctoberChatToolProgress>();
        Task<OctoberChatProviderResult> run = new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("受控问题", "controlled instructions", delta =>
            {
                visible.Append(delta);
                if (visible.ToString().Contains("先到", StringComparison.Ordinal)) firstText.TrySetResult();
                if (visible.ToString().Contains("查阅后", StringComparison.Ordinal)) nextText.TrySetResult();
            }, onToolProgress: progress.Add);
        try
        {
            Assert.Same(firstText.Task, await Task.WhenAny(firstText.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(run.IsCompleted);
            Assert.Equal("先到😀。", visible.ToString());
            Assert.Empty(fixture.Reader.SearchRequests);
            first.Release.TrySetResult();
            Task nextObserved = await Task.WhenAny(nextText.Task, run).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(ReferenceEquals(nextText.Task, nextObserved),
                $"Expected next text; completed={run.IsCompleted}, status={(run.IsCompletedSuccessfully ? run.Result.Status.ToString() : "pending")}, toolCalls={wiki.Budget.ToolCalls}, readerCalls={fixture.Reader.SearchRequests.Count}, sends={physical.SendCalls}, updates={string.Join(";", client.Observations)}.");
            Assert.False(run.IsCompleted);
            Assert.Equal(2, fixture.Reader.SearchRequests.Count);
            Assert.Equal(new[] { OctoberChatToolProgressKind.Started, OctoberChatToolProgressKind.Completed,
                OctoberChatToolProgressKind.Started, OctoberChatToolProgressKind.Completed }, progress.Select(item => item.Kind));
            Assert.Equal(2, physical.SendCalls);
            using JsonDocument request = JsonDocument.Parse(physical.RequestBodies[1]);
            Assert.Equal(2, request.RootElement.GetProperty("messages").EnumerateArray()
                .Count(message => message.GetProperty("role").GetString() == "tool"));
        }
        finally
        {
            first.Release.TrySetResult();
            second.Release.TrySetResult();
            await run;
            await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(OctoberChatProviderStatus.Completed, (await run).Status);
        Assert.Equal("先到😀。查阅后😀。", visible.ToString());
        Assert.Equal(visible.ToString(), (await run).AnswerText);
        Assert.Equal(2, wiki.Budget.ProviderRounds);
        Assert.Equal(2, wiki.Budget.ToolCalls);
        Assert.Equal(first.TotalBytes + second.TotalBytes, wiki.Budget.SseBytes);
        AssertReleased(physical, first, second);
        Assert.Equal(0, physical.DisposeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_DoneMarkerWaitsForPhysicalEofWithoutHoldingEarlierEventsAsync()
    {
        var body = new ControlledStream(Bytes(Event("{\"content\":\"即时文本\"}") + Event("{}", "stop") + Done), [], gated: true);
        using var physical = new ControlledHandler(body);
        var budget = new OctoberChatBudget();
        Assert.True(budget.TryStartProviderRound(out _));
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        var early = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var collected = new MemoryStream();
        Task run = ConsumeAsync();
        try
        {
            Assert.Same(early.Task, await Task.WhenAny(early.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            await body.GateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
            Assert.Contains("即时文本", Encoding.UTF8.GetString(collected.ToArray()), StringComparison.Ordinal);
            Assert.DoesNotContain("[DONE]", Encoding.UTF8.GetString(collected.ToArray()), StringComparison.Ordinal);
        }
        finally
        {
            body.Release.TrySetResult();
            await run;
            await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Contains("[DONE]", Encoding.UTF8.GetString(collected.ToArray()), StringComparison.Ordinal);
        Assert.Equal(body.TotalBytes, budget.SseBytes);

        async Task ConsumeAsync()
        {
            using HttpRequestMessage request = Request();
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            using Stream stream = await response.Content.ReadAsStreamAsync();
            byte[] buffer = new byte[512];
            int read;
            while ((read = await stream.ReadAsync(buffer)) != 0)
            {
                collected.Write(buffer, 0, read);
                if (Encoding.UTF8.GetString(collected.ToArray()).Contains("即时文本", StringComparison.Ordinal)) early.TrySetResult();
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-done")]
    [InlineData("duplicate-done")]
    [InlineData("trailing-content")]
    [InlineData("unterminated-event")]
    [InlineData("invalid-utf8")]
    [InlineData("multiple-choices")]
    [InlineData("conflicting-finish")]
    [InlineData("missing-finish")]
    public async Task ReadAsync_InvalidRawProtocolRejectsWithoutAcceptingTerminalAsync(string fault)
    {
        string finish = Event("{}", "stop");
        byte[] bytes = fault switch
        {
            "missing-done" => Bytes(finish),
            "duplicate-done" => Bytes(finish + Done + Done),
            "trailing-content" => Bytes(finish + Done + Event("{\"content\":\"private-late\"}")),
            "unterminated-event" => Bytes(finish + "data: [DONE]"),
            "invalid-utf8" => [.. Bytes("data: "), 0xc3, 0x28, .. Bytes("\n\n" + finish + Done)],
            "multiple-choices" => Bytes("data: {\"id\":\"r\",\"choices\":[{\"index\":0,\"delta\":{}},{\"index\":1,\"delta\":{}}]}\n\n" + finish + Done),
            "conflicting-finish" => Bytes(finish + Event("{}", "length") + Done),
            _ => Bytes(Event("{\"content\":\"partial\"}") + Done),
        };
        var body = new ControlledStream(bytes);
        using var physical = new ControlledHandler(body);
        var budget = new OctoberChatBudget();
        Assert.True(budget.TryStartProviderRound(out _));
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);

        await Assert.ThrowsAnyAsync<Exception>(() => ConsumeAsync(http));
        await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, physical.SendCalls);
        AssertReleased(physical, body);
        Assert.Equal(0, physical.DisposeCalls);
        Assert.Equal(0, budget.ToolCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("changed-id", 1)]
    [InlineData("changed-name", 1)]
    [InlineData("incomplete-final-identity", 2)]
    [InlineData("duplicate-id", 2)]
    [InlineData("negative-index", 2)]
    [InlineData("oversized-arguments", 1)]
    public async Task ReadAsync_InconsistentNativeFragmentsRejectBeforeSdkDispatchAsync(string fault, int refusedAttempts)
    {
        string first = Event("{\"tool_calls\":[{\"index\":0,\"id\":\"a\",\"type\":\"function\",\"function\":{\"name\":\"BrowseWiki\",\"arguments\":\"\"}}]}");
        string corrupt = fault switch
        {
            "changed-id" => "{\"index\":0,\"id\":\"b\",\"function\":{\"arguments\":\"{}\"}}",
            "changed-name" => "{\"index\":0,\"function\":{\"name\":\"ReadWiki\",\"arguments\":\"{}\"}}",
            "incomplete-final-identity" => "{\"index\":1,\"function\":{\"name\":\"BrowseWiki\",\"arguments\":\"{}\"}}",
            "duplicate-id" => "{\"index\":1,\"id\":\"a\",\"function\":{\"name\":\"BrowseWiki\",\"arguments\":\"{}\"}}",
            "negative-index" => "{\"index\":-1,\"id\":\"b\",\"function\":{\"name\":\"BrowseWiki\",\"arguments\":\"{}\"}}",
            _ => "{\"index\":0,\"function\":{\"arguments\":" + JsonSerializer.Serialize(new string('x', 16385)) + "}}",
        };
        var body = new ControlledStream(Bytes(first + Event("{\"tool_calls\":[" + corrupt + "]}") + Event("{}", "tool_calls") + Done));
        using var physical = new ControlledHandler(body);
        var budget = new OctoberChatBudget();
        Assert.True(budget.TryStartProviderRound(out _));
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);

        await Assert.ThrowsAnyAsync<Exception>(() => ConsumeAsync(http));
        await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, physical.SendCalls);
        Assert.Equal(refusedAttempts, budget.ToolCalls);
        Assert.Equal(0, budget.ActiveTools);
        AssertReleased(physical, body);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_ActualSdkDoneRequiresPhysicalEofBeforeSuccessfulCompletionAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        var body = new ControlledStream(Bytes(Event("{\"content\":\"真实增量😀\"}") + Event("{}", "stop") + Done), [], gated: true);
        using var physical = new ControlledHandler(body);
        using var guard = new OctoberChatStreamingHandler(physical, wiki.Budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        using IChatClient client = CreateSdkClient(http);
        var seenText = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var visible = new StringBuilder();
        Task<OctoberChatProviderResult> run = new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", delta =>
            {
                visible.Append(delta);
                seenText.TrySetResult();
            });
        try
        {
            Assert.Same(seenText.Task, await Task.WhenAny(seenText.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            await body.GateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
            Assert.Equal("真实增量😀", visible.ToString());
            Assert.Empty(fixture.Reader.SearchRequests);
        }
        finally
        {
            body.Release.TrySetResult();
            await run;
            await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(OctoberChatProviderStatus.Completed, (await run).Status);
        Assert.Equal(visible.ToString(), (await run).AnswerText);
        Assert.Equal(1, physical.SendCalls);
        Assert.Equal(0, wiki.Budget.ToolCalls);
        AssertReleased(physical, body);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_ActualSdkRawFailurePreservesOriginalCauseAndAlreadyDeliveredTextAsync(bool byteLimit)
    {
        byte[] prefix = Bytes(Event("{\"content\":\"已送达的真实片段😀\"}"));
        byte[] tail = byteLimit ? Bytes(":" + new string('x', 1024) + "\n\n" + Event("{}", "stop") + Done)
            : [.. Bytes("data: "), 0xc3, 0x28, .. Bytes("\n\n" + Event("{}", "stop") + Done)];
        var fixture = new ControlledChatFixture();
        var budget = new OctoberChatBudget(new OctoberChatLimits { SseRoundBytes = byteLimit ? prefix.Length + 64 : 2 * 1024 * 1024 });
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync(budget: budget);
        var body = new ControlledStream(prefix, tail, gated: true);
        using var physical = new ControlledHandler(body);
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        using IChatClient client = CreateSdkClient(http);
        var seenText = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var visible = new StringBuilder();
        Task<OctoberChatProviderResult> run = new OctoberChatAgent(client, wiki.Tools, budget)
            .RunAsync("问题", "controlled instructions", delta =>
            {
                visible.Append(delta);
                seenText.TrySetResult();
            });
        try
        {
            Assert.Same(seenText.Task, await Task.WhenAny(seenText.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(run.IsCompleted);
            Assert.Equal("已送达的真实片段😀", visible.ToString());
            body.Release.TrySetResult();
            OctoberChatProviderResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(byteLimit ? OctoberChatProviderStatus.BudgetExceeded : OctoberChatProviderStatus.Failed, result.Status);
            Assert.Equal(visible.ToString(), result.AnswerText);
            Assert.Equal("已送达的真实片段😀", result.AnswerText);
        }
        finally
        {
            body.Release.TrySetResult();
            await run;
            await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(1, physical.SendCalls);
        Assert.Equal(1, budget.ProviderRounds);
        Assert.Equal(0, budget.ToolCalls);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Empty(fixture.Reader.ExactRequests);
        AssertReleased(physical, body);
        Assert.Equal(0, physical.DisposeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SendAsync_SecondSendInOriginalRoundAndAfterSealNeverReachesPhysicalTransportAsync()
    {
        var body = new ControlledStream(Bytes(Event("{}", "stop") + Done));
        using var physical = new ControlledHandler(body);
        var budget = new OctoberChatBudget();
        Assert.True(budget.TryStartProviderRound(out _));
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        await ConsumeAsync(http);
        await Assert.ThrowsAnyAsync<Exception>(() => ConsumeAsync(http));
        Task settlement = guard.SealAndWaitForSettlementAsync();
        Assert.Same(settlement, guard.SealAndWaitForSettlementAsync());
        await settlement.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<Exception>(() => ConsumeAsync(http));

        Assert.Equal(1, physical.SendCalls);
        Assert.Equal(1, budget.ProviderRounds);
        AssertReleased(physical, body);
        Assert.Equal(0, physical.DisposeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsync_OriginalRoundAndAggregateRawBudgetsRemainEnforcedAsync(bool aggregate)
    {
        byte[] payload = Bytes(Event("{\"content\":\"bounded text\"}") + Event("{}", "stop") + Done);
        var budget = new OctoberChatBudget(new OctoberChatLimits
        {
            SseRoundBytes = aggregate ? payload.Length + 16 : payload.Length - 1,
            SseTurnBytes = aggregate ? payload.Length * 2 - 1 : payload.Length * 2,
        });
        var first = new ControlledStream(payload);
        var second = new ControlledStream(payload);
        using var physical = new ControlledHandler(first, second);
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        Assert.True(budget.TryStartProviderRound(out _));
        if (aggregate)
        {
            await ConsumeAsync(http);
            Assert.Equal(payload.Length, budget.SseBytes);
            Assert.True(budget.TryStartProviderRound(out _));
        }
        await Assert.ThrowsAnyAsync<Exception>(() => ConsumeAsync(http));
        await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(budget.IsStopped);
        Assert.Equal(aggregate ? 2 : 1, physical.SendCalls);
        Assert.Equal(aggregate ? 2 : 1, budget.ProviderRounds);
        Assert.Equal(0, budget.ToolCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsync_IgnoredCancellationBoundsConsumerButSettlementWaitsForPhysicalCleanupAsync(bool deadline)
    {
        var clock = new ControlledChatFixture.ManualClock();
        using var cancellation = new CancellationTokenSource();
        var budget = new OctoberChatBudget(timeProvider: clock, cancellationToken: cancellation.Token);
        Assert.True(budget.TryStartProviderRound(out _));
        var body = new ControlledStream([], Bytes(Event("{}", "stop") + Done), gated: true, ignoreCancellation: true);
        using var physical = new ControlledHandler(body);
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        Task run = ConsumeAsync(http, cancellation.Token);
        try
        {
            Assert.Same(body.GateEntered.Task, await Task.WhenAny(body.GateEntered.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            if (deadline) clock.Advance(TimeSpan.FromSeconds(180));
            else cancellation.Cancel();
            await Assert.ThrowsAnyAsync<Exception>(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(run.IsCompleted); // A WaitAsync timeout is not bounded-consumer evidence.
            Task settlement = guard.SealAndWaitForSettlementAsync();
            Assert.Same(settlement, guard.SealAndWaitForSettlementAsync());
            Assert.False(settlement.IsCompleted);
            Assert.Equal(0, body.DisposeCalls);
            Assert.False(body.DisposedWhileReading);
            body.Release.TrySetResult();
            await settlement.WaitAsync(TimeSpan.FromSeconds(5));
            AssertReleased(physical, body);
            Assert.False(body.DisposedWhileReading);
            Assert.Equal(1, physical.SendCalls);
            Assert.Equal(0, physical.DisposeCalls);
        }
        finally
        {
            cancellation.Cancel();
            body.Release.TrySetResult();
            try { await run; } catch (Exception) { }
            await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SendAsync_IgnoredCancellationRetainsSettlementUntilLateResponseDisposedAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new OctoberChatBudget(cancellationToken: cancellation.Token);
        Assert.True(budget.TryStartProviderRound(out _));
        var body = new ControlledStream(Bytes(Event("{}", "stop") + Done));
        using var physical = new ControlledHandler(body) { DelaySend = true };
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        Task run = ConsumeAsync(http, cancellation.Token);
        try
        {
            Assert.Same(physical.SendEntered.Task, await Task.WhenAny(physical.SendEntered.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<Exception>(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(run.IsCompleted);
            Task settlement = guard.SealAndWaitForSettlementAsync();
            Assert.False(settlement.IsCompleted);
            Assert.Equal(0, body.DisposeCalls);
            physical.ReleaseSend.TrySetResult();
            await settlement.WaitAsync(TimeSpan.FromSeconds(5));
            AssertReleased(physical, body);
            Assert.Equal(1, physical.SendCalls);
            Assert.Equal(0, physical.DisposeCalls);
        }
        finally
        {
            physical.ReleaseSend.TrySetResult();
            try { await run; } catch (Exception) { }
            await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("method")]
    [InlineData("path")]
    [InlineData("http-status")]
    [InlineData("content-type")]
    public async Task SendAsync_UnsupportedRequestOrResponseFailsClosedAsync(string fault)
    {
        var body = new ControlledStream(Bytes(Event("{}", "stop") + Done));
        using var physical = new ControlledHandler(body)
        {
            StatusCode = fault == "http-status" ? HttpStatusCode.BadRequest : HttpStatusCode.OK,
            ContentType = fault == "content-type" ? "application/json" : "text/event-stream",
        };
        var budget = new OctoberChatBudget();
        Assert.True(budget.TryStartProviderRound(out _));
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        using HttpRequestMessage request = Request();
        if (fault == "method") request.Method = HttpMethod.Get;
        if (fault == "path") request.RequestUri = new Uri("https://controlled.invalid/v1/responses");

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            await response.Content.ReadAsByteArrayAsync();
        });
        await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(fault is "method" or "path" ? 0 : 1, physical.SendCalls);
        if (fault is "method" or "path") Assert.Equal(0, body.DisposeCalls);
        else AssertReleased(physical, body);
        Assert.Equal(0, physical.DisposeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_ActualSdkDuplicateArgumentsRejectBeforeDictionaryNormalizationAsync(bool caseVariant)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness wiki = await fixture.OpenWikiAsync();
        string arguments = caseVariant ? "{\"query\":\"\",\"Query\":\"\"}" : "{\"query\":\"\",\"query\":\"\"}";
        string delta = "{\"tool_calls\":[{\"index\":0,\"id\":\"duplicate-call\",\"type\":\"function\",\"function\":{\"name\":\"BrowseWiki\",\"arguments\":" + JsonSerializer.Serialize(arguments) + "}}]}";
        var body = new ControlledStream(Bytes(Event(delta) + Event("{}", "tool_calls") + Done));
        using var physical = new ControlledHandler(body);
        using var guard = new OctoberChatStreamingHandler(physical, wiki.Budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        using IChatClient client = CreateSdkClient(http);

        OctoberChatProviderResult result = await new OctoberChatAgent(client, wiki.Tools, wiki.Budget)
            .RunAsync("问题", "controlled instructions", _ => { });
        await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Empty(fixture.Reader.ExactRequests);
        Assert.Empty(wiki.Ledger.Snapshot);
        Assert.Equal(1, wiki.Budget.ToolCalls);
        Assert.Equal(0, wiki.Budget.ActiveTools);
        Assert.Equal(1, physical.SendCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_NestedDuplicateArgumentsRejectBeforeSdkReceivesTerminalAsync()
    {
        const string arguments = "{\"document\":{\"batchId\":\"x\",\"BATCHID\":\"y\"}}";
        string delta = "{\"tool_calls\":[{\"index\":0,\"id\":\"nested-call\",\"type\":\"function\",\"function\":{\"name\":\"ReadWiki\",\"arguments\":" + JsonSerializer.Serialize(arguments) + "}}]}";
        var body = new ControlledStream(Bytes(Event(delta) + Event("{}", "tool_calls") + Done));
        using var physical = new ControlledHandler(body);
        var budget = new OctoberChatBudget();
        Assert.True(budget.TryStartProviderRound(out _));
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);

        await Assert.ThrowsAnyAsync<Exception>(() => ConsumeAsync(http));
        await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, budget.ToolCalls);
        Assert.Equal(0, budget.ActiveTools);
        Assert.Equal(1, physical.SendCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsync_ClosedGuardCannotReleasePreviouslyBufferedEventBytesAsync(bool sealGuard)
    {
        var body = new ControlledStream(Bytes(Event("{\"content\":\"buffered-controlled-text\"}") + Event("{}", "stop") + Done));
        using var physical = new ControlledHandler(body);
        var budget = new OctoberChatBudget();
        Assert.True(budget.TryStartProviderRound(out _));
        using var guard = new OctoberChatStreamingHandler(physical, budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        using HttpRequestMessage request = Request();
        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        using Stream stream = await response.Content.ReadAsStreamAsync();
        byte[] first = new byte[1];
        Assert.Equal(1, await stream.ReadAsync(first));
        byte[] afterClose = [0x7f];
        var deferred = new DeferredCallbacks();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task<int> read;
        try
        {
            // Keep cleanup's scheduled continuation pending only within this synchronous
            // block, so the close-before-cancellation race is reproduced without sleeps.
            SynchronizationContext.SetSynchronizationContext(deferred);
            if (sealGuard) _ = guard.SealAndWaitForSettlementAsync();
            else stream.Dispose();
            read = stream.ReadAsync(afterClose).AsTask();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
            deferred.Drain();
        }

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => read);
            Assert.Equal(0x7f, afterClose[0]);
        }
        finally
        {
            await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        AssertReleased(physical, body);
        Assert.Equal(1, physical.SendCalls);
        Assert.Equal(0, physical.DisposeCalls);
    }

    private const string Done = "data: [DONE]\n\n";
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static string Event(string delta, string? finish = null, string id = "round-1") =>
        "data: {\"id\":" + JsonSerializer.Serialize(id) + ",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"controlled-model\",\"choices\":[{\"index\":0,\"delta\":" + delta + ",\"finish_reason\":" + JsonSerializer.Serialize(finish) + "}]}\n\n";

    private static IChatClient CreateSdkClient(HttpClient http) => new ChatClient("controlled-model", new ApiKeyCredential("controlled-dummy-not-a-credential"), new OpenAIClientOptions
    {
        Endpoint = new Uri("https://controlled.invalid/v1/"),
        RetryPolicy = new ClientRetryPolicy(0),
        Transport = new HttpClientPipelineTransport(http, false, null),
        EnableDistributedTracing = false,
        ClientLoggingOptions = new ClientLoggingOptions { EnableLogging = false, EnableMessageLogging = false, EnableMessageContentLogging = false },
    }).AsIChatClient();

    private static HttpRequestMessage Request() => new(HttpMethod.Post, "https://controlled.invalid/v1/chat/completions")
    {
        Content = new StringContent("{\"model\":\"controlled-model\",\"stream\":true,\"messages\":[]}", Encoding.UTF8, "application/json"),
    };

    private static async Task ConsumeAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = Request();
        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        byte[] buffer = new byte[256];
        while (await stream.ReadAsync(buffer, cancellationToken) != 0) { }
    }

    private static void AssertReleased(ControlledHandler physical, params ControlledStream[] bodies)
    {
        Assert.Equal(bodies.Length, physical.Responses.Count);
        Assert.All(physical.Responses, response => Assert.Equal(1, response.DisposeCalls));
        Assert.All(bodies, body =>
        {
            Assert.Equal(1, body.CleanupCalls);
            Assert.False(body.DisposedWhileReading);
        });
    }

    private sealed class DeferredCallbacks : SynchronizationContext
    {
        private readonly List<(SendOrPostCallback Callback, object? State)> _callbacks = [];
        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (_callbacks) _callbacks.Add((callback, state));
        }
        public void Drain()
        {
            (SendOrPostCallback Callback, object? State)[] pending;
            lock (_callbacks)
            {
                pending = _callbacks.ToArray();
                _callbacks.Clear();
            }
            foreach ((SendOrPostCallback callback, object? state) in pending) callback(state);
        }
    }

    private sealed class ObservedChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public List<string> Observations { get; } = [];
        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (ChatResponseUpdate update in InnerClient.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                Observations.Add($"finish={update.FinishReason?.ToString() ?? "null"},types={string.Join(",", update.Contents.Select(content => content is FunctionCallContent call ? $"FunctionCall(informational={call.InformationalOnly})" : content.GetType().Name))}");
                yield return update;
            }
        }
    }

    private sealed class ControlledHandler(params ControlledStream[] bodies) : HttpMessageHandler
    {
        public int SendCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public bool DelaySend { get; init; }
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;
        public string ContentType { get; init; } = "text/event-stream";
        public TaskCompletionSource SendEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> RequestBodies { get; } = [];
        public List<ControlledResponse> Responses { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int index = SendCalls++;
            RequestBodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            SendEntered.TrySetResult();
            if (DelaySend) await ReleaseSend.Task; // Deliberately ignores cancellation; owned test releases it.
            if (index >= bodies.Length) throw new InvalidOperationException("Unexpected extra controlled send.");
            var response = new ControlledResponse(StatusCode) { Content = new StreamContent(bodies[index]) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(ContentType);
            Responses.Add(response);
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeCalls++;
            base.Dispose(disposing);
        }
    }

    private sealed class ControlledResponse(HttpStatusCode status) : HttpResponseMessage(status)
    {
        public int DisposeCalls { get; private set; }
        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeCalls++;
            base.Dispose(disposing);
        }
    }

    private sealed class ControlledStream(byte[] prefix, byte[]? suffix = null, bool gated = false, bool ignoreCancellation = false) : Stream
    {
        private readonly byte[] _bytes = [.. prefix, .. suffix ?? []];
        private int _position;
        private bool _reading;
        private int _cleaned;
        public TaskCompletionSource GateEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisposeCalls { get; private set; }
        public int CleanupCalls { get; private set; }
        public bool DisposedWhileReading { get; private set; }
        public int TotalBytes => _bytes.Length;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_reading) throw new InvalidOperationException("Concurrent controlled read.");
            _reading = true;
            try
            {
                if (gated && _position >= prefix.Length)
                {
                    GateEntered.TrySetResult();
                    if (ignoreCancellation) await Release.Task;
                    else await Release.Task.WaitAsync(cancellationToken);
                }
                if (!ignoreCancellation) cancellationToken.ThrowIfCancellationRequested();
                if (_position == _bytes.Length || buffer.Length == 0) return 0;
                buffer.Span[0] = _bytes[_position++]; // Split UTF-8 and every SSE/native field across physical reads.
                return 1;
            }
            finally { _reading = false; }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Controlled async transport only.");
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCalls++;
                DisposedWhileReading |= _reading;
                // StreamContent may call its inner stream twice through the cached
                // ReadAsStream wrapper even when the original response is disposed once.
                if (Interlocked.Exchange(ref _cleaned, 1) == 0) CleanupCalls++;
            }
            base.Dispose(disposing);
        }
    }
}

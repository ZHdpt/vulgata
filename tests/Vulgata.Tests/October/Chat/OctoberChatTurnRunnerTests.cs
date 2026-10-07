using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

/// <summary>
/// Admitted-runner consumer mechanics only: fixed codec answers, scripted execution authority and
/// opaque owner-port capsules. No durable codec, admission store, host registration or provider service is qualified.
/// </summary>
public sealed class OctoberChatTurnRunnerTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_NativeReadCapturesUpdatedOpaqueUnionForProgressAndTextWithoutChangingEarlierSnapshotAsync()
    {
        Harness h = await Harness.OpenAsync();
        KnowledgeDependencyHandle before = h.Initial;
        KnowledgeDependencyHandle after = Capsule('F', "owner-union-after-read");
        h.Dependencies.MergeResult = caps => caps.Contains(before) && caps.Length > 1 ? after : before;
        h.Client.Script = token => RoundAsync(token);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        var text = new List<(string Text, KnowledgeDependencyHandle Dependencies)>();
        var progress = new List<(OctoberChatToolProgress Progress, KnowledgeDependencyHandle Dependencies)>();

        OctoberChatGenerationResult result = await runner.RunAsync((delta, dependencies, _) =>
        {
            text.Add((delta, dependencies));
            return ValueTask.CompletedTask;
        }, onToolProgress: (item, dependencies) => progress.Add((item, dependencies)));

        Assert.Equal(DialogueTerminalState.Completed, result.TerminalState);
        Assert.Equal(2, h.Client.Requests.Count);
        Assert.Equal(1, h.Reader.Calls);
        Assert.Equal(new[] { "读取前。", "读取后。" }, text.Select(item => item.Text));
        Assert.Equal(before, text[0].Dependencies);
        Assert.Equal(after, text[1].Dependencies);
        Assert.Equal(new[] { OctoberChatToolProgressKind.Started, OctoberChatToolProgressKind.Completed }, progress.Select(item => item.Progress.Kind));
        Assert.Equal(before, progress[0].Dependencies);
        Assert.Equal(after, progress[1].Dependencies);
        Assert.Equal(progress[0].Progress.OperationId, progress[1].Progress.OperationId);
        Assert.Equal(after, result.InferenceDependencies);
        Assert.Contains(h.Dependencies.MergeCalls, caps => caps.Contains(before) && caps.Length > 1);
        Assert.Equal(before.ProtectedPayload, text[0].Dependencies.ProtectedPayload);
        Assert.NotEqual(text[0].Dependencies, text[1].Dependencies);

        async IAsyncEnumerable<ChatResponseUpdate> RoundAsync([EnumeratorCancellation] CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (h.Client.Requests.Count == 1)
            {
                yield return new() { Role = ChatRole.Assistant, Contents = [new TextContent("读取前。")] };
                yield return new() { Role = ChatRole.Assistant, Contents = [new FunctionCallContent("read", "ReadWiki",
                    new Dictionary<string, object?> { ["document"] = h.Reader.Document })] };
                yield return new() { Role = ChatRole.Assistant, FinishReason = ChatFinishReason.ToolCalls };
            }
            else
            {
                Assert.Equal(2, h.Client.Requests.Count);
                yield return new() { Role = ChatRole.Assistant, Contents = [new TextContent("读取后。")] };
                yield return new() { Role = ChatRole.Assistant, FinishReason = ChatFinishReason.Stop };
            }
            await Task.CompletedTask;
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAndRun_ConfirmedAdmissionUsesEmbeddedModePromptExactHistoryAndImmediateTextAsync(bool business)
    {
        Harness h = await Harness.OpenAsync(history: true, mode: business ? KnowledgeMode.Business : KnowledgeMode.IT);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Client.Script = token => StreamAsync("即时真实片段。", "stop", token, finish.Task);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        var text = new StringBuilder();
        Task<OctoberChatGenerationResult> run = runner.RunAsync((delta, _) => { text.Append(delta); seen.TrySetResult(); return ValueTask.CompletedTask; });
        try
        {
            Assert.Same(seen.Task, await Task.WhenAny(seen.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(run.IsCompleted);
            Assert.Equal("即时真实片段。", text.ToString());
            ChatMessage[] sent = Assert.Single(h.Client.Requests);
            Assert.Equal(h.Prompt, Assert.Single(sent.Where(message => message.Role == ChatRole.System)).Text);
            ChatMessage[] users = sent.Where(message => message.Role == ChatRole.User).ToArray();
            Assert.Equal(2, users.Length);
            Assert.Equal(h.Codec.HistoryBytes.ToArray(), Encoding.UTF8.GetBytes(users[0].Text));
            Assert.Equal(h.Request.Question, users[1].Text);
            Assert.DoesNotContain("CONTROLLED opaque", string.Join("|", sent.Select(message => message.Text)), StringComparison.Ordinal);
            Assert.Contains(h.Dependencies.MergeCalls, caps => caps.Contains(h.Initial) && caps.Contains(h.History.Dependencies!));
            Assert.Contains(h.Dependencies.CheckCalls, caps => caps.Contains(h.Dependencies.Merged));
            Assert.NotEmpty(h.Execution.AdmissionCalls);
            Assert.All(h.Execution.AdmissionCalls, admitted => Assert.Equal(h.Request, admitted));
            Assert.Equal(0, h.Execution.TerminalCalls);
            Assert.Equal(0, h.Execution.OrphanCalls);
            h.Clock.Advance(TimeSpan.FromSeconds(3));
        }
        finally
        {
            finish.TrySetResult();
            await run;
        }

        OctoberChatGenerationResult result = await run;
        Assert.Equal(DialogueTerminalState.Completed, result.TerminalState);
        Assert.Equal(DialogueTerminalReason.None, result.Reason);
        Assert.Equal(DialogueProviderFinish.ValidStop, result.ProviderFinish);
        Assert.Equal(h.SaveId, result.SaveOperationId);
        Assert.Equal(h.Clock.GetUtcNow(), result.GenerationEndedAtUtc);
        Assert.Equal(h.Dependencies.Merged, result.InferenceDependencies);
        Assert.Equal(text.ToString(), result.AnswerText);
        Assert.Empty(result.ConsumedReads);
        Assert.Equal(0, h.Dependencies.RootCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("existing")]
    [InlineData("may-dispatch-false")]
    [InlineData("indeterminate")]
    [InlineData("empty-turn")]
    [InlineData("zero-fence")]
    [InlineData("wrong-conversation")]
    [InlineData("profile")]
    [InlineData("revision")]
    [InlineData("prompt-digest")]
    [InlineData("begin-digest")]
    [InlineData("history-digest")]
    [InlineData("history-order")]
    [InlineData("limits")]
    [InlineData("execution-not-verified")]
    [InlineData("execution-unavailable")]
    [InlineData("empty-save-id")]
    public async Task CreateAsync_ReplayOrTamperedAdmissionCannotDispatchAsync(string invalid)
    {
        Harness h = await Harness.OpenAsync(history: true);
        switch (invalid)
        {
            case "existing": h.Begin = h.Begin with { Status = DialogueStatus.Existing }; break;
            case "may-dispatch-false": h.Begin = h.Begin with { MayDispatch = false }; break;
            case "indeterminate": h.Begin = h.Begin with { Status = DialogueStatus.Indeterminate }; break;
            case "empty-turn": h.Admission = h.Admission with { TurnId = Guid.Empty }; break;
            case "zero-fence": h.Admission = h.Admission with { Fence = 0 }; break;
            case "wrong-conversation": h.Request = h.Request with { ConversationId = Guid.NewGuid() }; break;
            case "profile": h.ProfileId = "other-profile"; break;
            case "revision": h.ProfileRevision = "other-revision"; break;
            case "prompt-digest": h.Request = h.Request with { PromptDigest = new string('0', 64) }; break;
            case "begin-digest": h.Request = h.Request with { RequestDigest = new string('0', 64) }; break;
            case "history-digest": h.History = h.History with { ProjectionDigest = new string('0', 64) }; break;
            case "history-order": h.Request = h.Request with { HistoryTurnIds = [Guid.NewGuid()] }; break;
            case "limits": h.Request = h.Request with { Limits = h.Request.Limits with { ProviderRounds = h.Request.Limits.ProviderRounds + 1 } }; break;
            case "execution-not-verified": h.Execution.Status = DialogueExecutionStatus.NotVerified; break;
            case "execution-unavailable": h.Execution.Status = DialogueExecutionStatus.Unavailable; break;
            case "empty-save-id": h.SaveId = Guid.Empty; break;
        }

        OctoberChatTurnRunner? runner = await h.CreateAsync();

        Assert.Null(runner);
        Assert.Empty(h.Client.Requests);
        Assert.Equal(0, h.Reader.Calls);
        Assert.Equal(0, h.Execution.TerminalCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_RejectsHistoryOrBeginEnvelopeBeyondOwnerCodecLimitsAsync(bool history)
    {
        Harness h = await Harness.OpenAsync(history: true);
        if (history) h.Codec.HistoryBytes = Encoding.UTF8.GetBytes(new string('x', OctoberDialogueLimits.MaximumHistoryProjectionUtf8Bytes + 1)).ToImmutableArray();
        else h.Codec.BeginBytes = OctoberDialogueLimits.MaximumBeginEnvelopeUtf8Bytes + 1;

        Assert.Null(await h.CreateAsync());
        Assert.Empty(h.Client.Requests);
        Assert.Equal(0, h.Reader.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_OriginalBudgetAndDurableDeadlineCannotRestartAtFactoryOrRunAsync(bool durableDeadline)
    {
        Harness h = await Harness.OpenAsync();
        h.Client.Script = token => StreamAsync("已有片段", "stop", token, h.ProviderGate.Task);
        h.Clock.Advance(TimeSpan.FromSeconds(170));
        if (durableDeadline) h.Admission = h.Admission with { DeadlineUtc = h.Clock.GetUtcNow().AddSeconds(5) };
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<OctoberChatGenerationResult> run = runner.RunAsync((_, _) => { seen.TrySetResult(); return ValueTask.CompletedTask; });
        try
        {
            Assert.Same(seen.Task, await Task.WhenAny(seen.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            h.Clock.Advance(TimeSpan.FromSeconds(durableDeadline ? 5 : 10));
            OctoberChatGenerationResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(DialogueTerminalState.Interrupted, result.TerminalState);
            Assert.Equal(DialogueTerminalReason.DeadlineExceeded, result.Reason);
            Assert.Equal(DialogueProviderFinish.Cancelled, result.ProviderFinish);
            Assert.Equal("已有片段", result.AnswerText);
            Assert.False(h.ProviderGate.Task.IsCompleted);
            Assert.Equal(1, h.Budget.ProviderRounds);
            Assert.Equal(h.Clock.GetUtcNow(), result.GenerationEndedAtUtc);
        }
        finally
        {
            h.ProviderGate.TrySetResult();
            await run;
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CreateAsync_AlreadyExpiredAdmissionRefusesBeforeClientOrAuthorityDispatchAsync()
    {
        Harness h = await Harness.OpenAsync();
        h.Clock.Advance(TimeSpan.FromSeconds(180));

        Assert.Null(await h.CreateAsync());
        Assert.Empty(h.Client.Requests);
        Assert.Equal(0, h.Reader.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_SingleUseRunnerCannotRedispatchOrCancelOriginalWorkAsync()
    {
        Harness h = await Harness.OpenAsync();
        h.Client.Script = token => StreamAsync("第一次生成", "stop", token, h.ProviderGate.Task);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<OctoberChatGenerationResult> first = runner.RunAsync((_, _) => { observed.TrySetResult(); return ValueTask.CompletedTask; });
        try
        {
            Assert.Same(observed.Task, await Task.WhenAny(observed.Task, first).WaitAsync(TimeSpan.FromSeconds(5)));
            OctoberChatGenerationResult? second = null;
            try { second = await runner.RunAsync((_, _) => ValueTask.CompletedTask); }
            catch (InvalidOperationException) { }
            Assert.True(second is null || second.TerminalState != DialogueTerminalState.Completed);
            Assert.False(first.IsCompleted);
            Assert.Single(h.Client.Requests);
        }
        finally { h.ProviderGate.TrySetResult(); await first; }
        Assert.Equal(DialogueTerminalState.Completed, (await first).TerminalState);
        Assert.Equal("第一次生成", (await first).AnswerText);
        Assert.Single(h.Client.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("stop", "answer", "Completed", "None", "ValidStop")]
    [InlineData("stop", "   ", "Failed", "ExecutionFailure", "ValidStop")]
    [InlineData("length", "partial", "Interrupted", "ProviderInterrupted", "Length")]
    [InlineData("content_filter", "partial", "Interrupted", "ProviderInterrupted", "ContentFilter")]
    [InlineData("unsupported", "partial", "Failed", "ProtocolFailure", "Invalid")]
    [InlineData("missing", "partial", "Failed", "ProtocolFailure", "Truncated")]
    public async Task RunAsync_KnownProviderFactsRemainDistinctFromDurableCompletionAsync(string finish, string text, string state, string reason, string providerFinish)
    {
        Harness h = await Harness.OpenAsync();
        h.Client.Script = token => StreamAsync(text, finish, token);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        var visible = new StringBuilder();

        OctoberChatGenerationResult result = await runner.RunAsync((delta, _) => { visible.Append(delta); return ValueTask.CompletedTask; });

        Assert.Equal(Enum.Parse<DialogueTerminalState>(state), result.TerminalState);
        Assert.Equal(Enum.Parse<DialogueTerminalReason>(reason), result.Reason);
        Assert.Equal(Enum.Parse<DialogueProviderFinish>(providerFinish), result.ProviderFinish);
        Assert.Equal(text, result.AnswerText);
        Assert.Equal(text, visible.ToString());
        Assert.Equal(h.SaveId, result.SaveOperationId);
        Assert.Equal(0, h.Execution.TerminalCalls);
        Assert.Equal(0, h.Execution.OrphanCalls);
        Assert.Single(h.Client.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_CallerCancellationPreservesPartialTextWithoutClaimingPhysicalSettlementAsync()
    {
        Harness h = await Harness.OpenAsync();
        h.Client.Script = token => StreamAsync("取消前片段", "stop", token, h.ProviderGate.Task);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        using var cancel = new CancellationTokenSource();
        var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<OctoberChatGenerationResult> run = runner.RunAsync((_, _) => { seen.TrySetResult(); return ValueTask.CompletedTask; }, cancel.Token);
        try
        {
            Assert.Same(seen.Task, await Task.WhenAny(seen.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            cancel.Cancel();
            OctoberChatGenerationResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(DialogueTerminalState.Interrupted, result.TerminalState);
            Assert.Equal(DialogueTerminalReason.UserCancelled, result.Reason);
            Assert.Equal(DialogueProviderFinish.Cancelled, result.ProviderFinish);
            Assert.Equal("取消前片段", result.AnswerText);
            Assert.Equal(0, h.Execution.TerminalCalls);
        }
        finally { h.ProviderGate.TrySetResult(); await run; }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_OriginalOutputBudgetProducesTruthfulNoncompletedFactsAsync()
    {
        Harness h = await Harness.OpenAsync(limits: new OctoberChatLimits { OutputBytes = 1 });
        h.Client.Script = token => StreamAsync("too large", "stop", token);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        var text = new StringBuilder();

        OctoberChatGenerationResult result = await runner.RunAsync((delta, _) => { text.Append(delta); return ValueTask.CompletedTask; });

        Assert.Equal(DialogueTerminalState.Interrupted, result.TerminalState);
        Assert.Equal(DialogueTerminalReason.BudgetExceeded, result.Reason);
        Assert.Equal(DialogueProviderFinish.Unknown, result.ProviderFinish);
        Assert.Empty(text.ToString());
        Assert.Empty(result.AnswerText);
        Assert.Single(h.Client.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_TerminalEnvelopeCapacityIsCheckedBeforeReleasingNextTextBatchAsync()
    {
        Harness h = await Harness.OpenAsync();
        h.Codec.TerminalSize = candidate => candidate.AnswerText.Contains("blocked", StringComparison.Ordinal)
            ? OctoberDialogueLimits.MaximumTerminalEnvelopeUtf8Bytes + 1 : 512;
        h.Client.Script = token => TwoChunksAsync(token);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        var visible = new StringBuilder();

        OctoberChatGenerationResult result = await runner.RunAsync((delta, _) => { visible.Append(delta); return ValueTask.CompletedTask; });

        Assert.Equal(DialogueTerminalState.Interrupted, result.TerminalState);
        Assert.Equal(DialogueTerminalReason.BudgetExceeded, result.Reason);
        Assert.Equal("allowed", visible.ToString());
        Assert.Equal(visible.ToString(), result.AnswerText);
        Assert.Contains(h.Codec.TerminalCandidates, candidate => candidate.AnswerText == "allowedblocked" && candidate.SaveOperationId == h.SaveId);
        Assert.All(h.Codec.TerminalCandidates, candidate =>
        {
            Assert.Equal(DialogueTerminalState.Failed, candidate.TerminalState);
            Assert.Equal(DialogueTerminalReason.ExecutionFailure, candidate.Reason);
            Assert.Equal(DialogueProviderFinish.Unknown, candidate.ProviderFinish);
            Assert.Equal(1234567, candidate.GenerationEndedAtUtc.Ticks % TimeSpan.TicksPerSecond);
        });
        Assert.Equal(h.Clock.GetUtcNow(), result.GenerationEndedAtUtc);
        Assert.Equal(0, h.Execution.TerminalCalls);

        static async IAsyncEnumerable<ChatResponseUpdate> TwoChunksAsync([EnumeratorCancellation] CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            yield return new(ChatRole.Assistant, "allowed");
            yield return new(ChatRole.Assistant, "blocked");
            yield return new() { Role = ChatRole.Assistant, FinishReason = ChatFinishReason.Stop };
            await Task.CompletedTask;
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_WholeOrConsumedArrayCapacityRejectsNewToolInputBeforeProofCreditAsync(bool consumedArray)
    {
        Harness h = await Harness.OpenAsync();
        h.Codec.TerminalSize = candidate => candidate.ConsumedReads.Length == 0 ? 512
            : consumedArray ? OctoberDialogueLimits.MaximumConsumedArrayUtf8Bytes + 1024
            : OctoberDialogueLimits.MaximumTerminalEnvelopeUtf8Bytes + 1;
        h.Client.Script = token => ReadRoundAsync(token);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        var visible = new StringBuilder();

        OctoberChatGenerationResult result = await runner.RunAsync((delta, _) => { visible.Append(delta); return ValueTask.CompletedTask; });

        Assert.Equal(DialogueTerminalState.Interrupted, result.TerminalState);
        Assert.Equal(DialogueTerminalReason.BudgetExceeded, result.Reason);
        Assert.Empty(result.ConsumedReads);
        Assert.Empty(result.References);
        Assert.Empty(result.AnswerText);
        Assert.Empty(visible.ToString());
        Assert.Single(h.Client.Requests);
        Assert.Equal(1, h.Reader.Calls);
        Assert.Contains(h.Codec.TerminalCandidates, candidate => candidate.ConsumedReads.Length == 1 && candidate.References.Length == 1);
        Assert.Equal(0, h.Budget.ToolResultBytes);

        async IAsyncEnumerable<ChatResponseUpdate> ReadRoundAsync([EnumeratorCancellation] CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            yield return new() { Role = ChatRole.Assistant, Contents = [new FunctionCallContent("read", "ReadWiki",
                new Dictionary<string, object?> { ["document"] = h.Reader.Document })] };
            yield return new() { Role = ChatRole.Assistant, FinishReason = ChatFinishReason.ToolCalls };
            await Task.CompletedTask;
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_ReportByteExhaustionKeepsOnlyPreviouslyAccountedFooterReferencesAsync()
    {
        var pages = new FixedReader(new OctoberKnowledgeFixture());
        var firstReference = new OctoberChatReference(pages.Document, false, 1, Encoding.UTF8.GetByteCount(pages.Content.Content!));
        var secondReference = new OctoberChatReference(pages.OtherDocument, false, 1, Encoding.UTF8.GetByteCount(pages.OtherContent.Content!));
        var encoder = new OctoberChatKnowledgeOutputEncoder();
        int priorBytes = encoder.EncodeContent(pages.Content).Length +
            OctoberWikiTools.SerializeResultUtf8(new(OctoberChatToolStatus.Succeeded, References: [firstReference])).Length +
            encoder.EncodeContent(pages.OtherContent).Length;
        int refusedReportBytes = OctoberWikiTools.SerializeResultUtf8(new(OctoberChatToolStatus.Succeeded, References: [firstReference, secondReference])).Length;
        Harness h = await Harness.OpenAsync(limits: new OctoberChatLimits { ToolResultBytes = priorBytes + refusedReportBytes - 1 });
        h.Client.Script = token => RoundAsync(token);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);

        OctoberChatGenerationResult result = await runner.RunAsync((_, _) => ValueTask.CompletedTask);

        Assert.Equal(DialogueTerminalState.Interrupted, result.TerminalState);
        Assert.Equal(DialogueTerminalReason.BudgetExceeded, result.Reason);
        Assert.Equal(new[] { firstReference }, result.References);
        Assert.Equal(2, result.ConsumedReads.Length);
        Assert.Equal(4, h.Client.Requests.Count);
        Assert.Equal(2, h.Reader.Calls);
        Assert.Equal(priorBytes, h.Budget.ToolResultBytes);

        async IAsyncEnumerable<ChatResponseUpdate> RoundAsync([EnumeratorCancellation] CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int round = h.Client.Requests.Count;
            if (round > 4) throw new InvalidOperationException("No provider request after refused report.");
            bool read = round is 1 or 3;
            DocumentHandle document = round < 3 ? h.Reader.Document : h.Reader.OtherDocument;
            yield return new()
            {
                Role = ChatRole.Assistant,
                Contents = [new FunctionCallContent("round-" + round, read ? "ReadWiki" : "ReportAnswerDocuments",
                    read ? new Dictionary<string, object?> { ["document"] = document }
                        : new Dictionary<string, object?> { ["documents"] = new[] { document } })],
            };
            yield return new() { Role = ChatRole.Assistant, FinishReason = ChatFinishReason.ToolCalls };
            await Task.CompletedTask;
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReauthorizeBeforeSendAsync_PostPermitRevocationPreventsPhysicalDispatchAsync(bool executionRevoked)
    {
        Harness h = await Harness.OpenAsync(history: true);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var permit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int physicalSends = 0;
        h.Client.Script = token => WaitingForPermitAsync(token);
        var visible = new StringBuilder();
        Task<OctoberChatGenerationResult> run = runner.RunAsync((text, _) => { visible.Append(text); return ValueTask.CompletedTask; });
        try
        {
            Assert.Same(waiting.Task, await Task.WhenAny(waiting.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            if (executionRevoked) h.Execution.Status = DialogueExecutionStatus.NotVerified;
            else h.Dependencies.Status = KnowledgeDependencyStatus.Denied;
            permit.TrySetResult();
            OctoberChatGenerationResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotEqual(DialogueTerminalState.Completed, result.TerminalState);
            Assert.Equal(0, physicalSends);
            Assert.Empty(visible.ToString());
            Assert.Empty(result.AnswerText);
            Assert.Contains(h.Dependencies.CheckCalls, caps => caps.Contains(h.Dependencies.Merged));
        }
        finally { permit.TrySetResult(); await run; }

        async IAsyncEnumerable<ChatResponseUpdate> WaitingForPermitAsync([EnumeratorCancellation] CancellationToken token)
        {
            waiting.TrySetResult();
            await permit.Task.WaitAsync(token);
            await runner.ReauthorizeBeforeSendAsync(token);
            physicalSends++;
            yield return new(ChatRole.Assistant, "must not send");
            yield return new() { Role = ChatRole.Assistant, FinishReason = ChatFinishReason.Stop };
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_KnowledgeRevokedDuringExecutionCheckCannotReachNextDispatchBoundaryAsync(bool postPermit)
    {
        Harness h = await Harness.OpenAsync(history: true);
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        int physicalSends = 0;
        h.Client.Script = token => PhysicalDispatchAsync(token);
        if (!postPermit) h.Execution.HoldNext = true;
        Task<OctoberChatGenerationResult> run = runner.RunAsync((_, _) => ValueTask.CompletedTask);
        try
        {
            Assert.Same(h.Execution.GateEntered.Task, await Task.WhenAny(h.Execution.GateEntered.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            h.Dependencies.Status = KnowledgeDependencyStatus.Denied;
            h.Execution.ReleaseGate.TrySetResult();
            OctoberChatGenerationResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotEqual(DialogueTerminalState.Completed, result.TerminalState);
            Assert.Equal(0, physicalSends);
            Assert.Equal(postPermit ? 1 : 0, h.Client.Requests.Count);
            Assert.Empty(result.AnswerText);
        }
        finally { h.Execution.ReleaseGate.TrySetResult(); await run; }

        async IAsyncEnumerable<ChatResponseUpdate> PhysicalDispatchAsync([EnumeratorCancellation] CancellationToken token)
        {
            if (postPermit)
            {
                h.Execution.HoldNext = true;
                await runner.ReauthorizeBeforeSendAsync(token);
            }
            physicalSends++;
            yield return new(ChatRole.Assistant, "unqualified dispatch");
            yield return new() { Role = ChatRole.Assistant, FinishReason = ChatFinishReason.Stop };
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReauthorizeBeforeSendAsync_OnlyActiveRunCanAuthorizePhysicalSendAsync(bool afterRun)
    {
        Harness h = await Harness.OpenAsync();
        OctoberChatTurnRunner? runner = await h.CreateAsync();
        Assert.NotNull(runner);
        if (afterRun)
            Assert.Equal(DialogueTerminalState.Completed, (await runner.RunAsync((_, _) => ValueTask.CompletedTask)).TerminalState);

        await Assert.ThrowsAnyAsync<Exception>(() => runner.ReauthorizeBeforeSendAsync());

        Assert.Equal(afterRun ? 1 : 0, h.Client.Requests.Count);
        Assert.Equal(0, h.Execution.TerminalCalls);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(string text, string finish,
        [EnumeratorCancellation] CancellationToken token, Task? gate = null)
    {
        token.ThrowIfCancellationRequested();
        yield return new(ChatRole.Assistant, text);
        if (gate is not null) await gate.WaitAsync(token);
        if (finish != "missing") yield return new() { Role = ChatRole.Assistant, FinishReason = new ChatFinishReason(finish) };
    }

    private sealed class Harness
    {
        private Harness(OctoberChatLimits? limits)
        {
            Budget = new(limits, Clock);
            var knowledge = new OctoberKnowledgeFixture();
            Current = knowledge.Selection() with { Purpose = KnowledgePurpose.Chat };
            Reader = new(knowledge);
            Dependencies.Document = Reader.Document;
            Dependencies.Content = Reader.Content;
            Dependencies.OtherDocument = Reader.OtherDocument;
            Dependencies.OtherContent = Reader.OtherContent;
        }
        public static async Task<Harness> OpenAsync(bool history = false, KnowledgeMode mode = KnowledgeMode.IT, OctoberChatLimits? limits = null)
        {
            var h = new Harness(limits);
            h.Current = h.Current! with { Mode = mode };
            h.Prompt = await new OctoberChatPromptCatalog().LoadAsync(mode) ?? throw new InvalidOperationException("Actual embedded mode prompt missing.");
            var conversation = new DialogueConversationIdentity(Guid.NewGuid(), Guid.NewGuid(), h.Current.Subject.SubjectId,
                h.Current.Publication, mode, h.Current.RepositoryScope);
            ImmutableArray<DialogueHistoryTurn> turns = history
                ? [new(Guid.NewGuid(), 1, "历史问题", "Advisory 历史限定说明", [], h.HistoryCapsule)] : [];
            h.Codec.HistoryBytes = Encoding.UTF8.GetBytes(history
                ? "[{\"question\":\"历史问题\",\"answerText\":\"Advisory 历史限定说明\",\"references\":[]}]" : "[]").ToImmutableArray();
            h.History = new(DialogueStatus.Succeeded, turns, false, h.Codec.HistoryDigest, history ? h.HistoryCapsule : null);
            var request = new DialogueBeginRequest(conversation.ConversationId, conversation.ContextId, Guid.NewGuid(), "当前问题",
                h.Codec.BeginDigest, h.ProfileId, h.ProfileRevision, OctoberKnowledgePolicy.Hash(h.Prompt),
                turns.Select(turn => turn.TurnId).ToImmutableArray(), h.Codec.HistoryDigest, h.Initial, h.Budget.Limits,
                new(Guid.NewGuid(), Guid.NewGuid()));
            h.Begin = new(DialogueStatus.Succeeded, new(conversation, request, Guid.NewGuid(), history ? 2 : 1, 1,
                h.Clock.GetUtcNow(), h.Clock.GetUtcNow().Add(h.Budget.Limits.Duration), DialogueTurnState.Admitted), true);
            return h;
        }
        public ControlledChatFixture.ManualClock Clock { get; } = new();
        public OctoberChatBudget Budget { get; }
        public DialogueBeginResult Begin { get; set; } = null!;
        public DialogueAdmission Admission { get => Begin.Admission!; set => Begin = Begin with { Admission = value }; }
        public DialogueBeginRequest Request { get => Admission.Request; set => Admission = Admission with { Request = value }; }
        public DialogueHistoryResult History { get; set; } = null!;
        public string Prompt { get; private set; } = "";
        public string ProfileId { get; set; } = "controlled-profile";
        public string ProfileRevision { get; set; } = "controlled-snapshot";
        public Guid SaveId { get; set; } = Guid.NewGuid();
        public ReadSelectionRequest? Current { get; set; }
        public ReadSelection Selection { get; } = new("CONTROLLED ephemeral selection");
        public KnowledgeDependencyHandle Initial { get; } = Capsule('C', "initial");
        public KnowledgeDependencyHandle HistoryCapsule { get; } = Capsule('D', "history");
        public FixedCodec Codec { get; } = new();
        public FixedExecutionAuthority Execution { get; } = new();
        public FixedDependencies Dependencies { get; } = new();
        public FixedReader Reader { get; }
        public RecordingClient Client { get; } = new();
        public TaskCompletionSource ProviderGate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<OctoberChatTurnRunner?> CreateAsync() => OctoberChatTurnRunner.CreateAsync(Begin, Client, Budget, Selection, History,
            ProfileId, ProfileRevision, Reader, Dependencies, Codec, Execution, _ => Task.FromResult(Current), SaveId);
    }

    private static KnowledgeDependencyHandle Capsule(char digest, string label) => new(Guid.NewGuid(), new string(digest, 64), "CONTROLLED opaque " + label);

    private sealed class FixedCodec : IOctoberDialoguePayloadCodec
    {
        public string BeginDigest { get; } = new('A', 64);
        public string HistoryDigest { get; } = new('B', 64);
        public ImmutableArray<byte> HistoryBytes { get; set; } = [91, 93];
        public int BeginBytes { get; set; } = 512;
        public Func<DialogueTerminalSaveRequest, int> TerminalSize { get; set; } = _ => 512;
        public List<DialogueTerminalSaveRequest> TerminalCandidates { get; } = [];
        public string ComputeBeginDigest(DialogueConversationIdentity conversation, DialogueBeginRequest request) => BeginDigest;
        public string ComputeOutcomeDigest(DialogueTerminalSaveRequest request) => new('F', 64);
        public string ComputeHistoryProjectionDigest(ImmutableArray<DialogueHistoryTurn> turns) => HistoryDigest;
        public int MeasureBeginEnvelopeUtf8Bytes(DialogueBeginRequest request) => BeginBytes;
        public int MeasureTerminalEnvelopeUtf8Bytes(DialogueTerminalSaveRequest request) { TerminalCandidates.Add(request); return TerminalSize(request); }
        public ImmutableArray<byte> EncodeHistoryProjectionUtf8(ImmutableArray<DialogueHistoryTurn> turns) => HistoryBytes;
        public int MeasureHistoryProjectionUtf8Bytes(ImmutableArray<DialogueHistoryTurn> turns) => HistoryBytes.Length;
    }

    private sealed class FixedExecutionAuthority : IOctoberDialogueExecutionAuthority
    {
        public DialogueExecutionStatus Status { get; set; } = DialogueExecutionStatus.Verified;
        public List<DialogueBeginRequest> AdmissionCalls { get; } = [];
        public int TerminalCalls { get; private set; }
        public int OrphanCalls { get; private set; }
        public bool HoldNext { get; set; }
        public TaskCompletionSource GateEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseGate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DialogueExecutionStatus> VerifyAdmissionAsync(DialogueBeginRequest request, CancellationToken cancellationToken = default)
        {
            AdmissionCalls.Add(request);
            if (HoldNext)
            {
                HoldNext = false;
                GateEntered.TrySetResult();
                await ReleaseGate.Task.WaitAsync(cancellationToken);
            }
            return Status;
        }
        public Task<DialogueExecutionStatus> VerifyTerminalAsync(DialogueAdmission admission, DialogueTerminalSaveRequest request, CancellationToken cancellationToken = default)
        { TerminalCalls++; return Task.FromResult(DialogueExecutionStatus.NotVerified); }
        public Task<DialogueExecutionStatus> VerifyOwnerDeathAsync(DialogueExecutionIdentity execution, CancellationToken cancellationToken = default)
        { OrphanCalls++; return Task.FromResult(DialogueExecutionStatus.NotVerified); }
    }

    private sealed class FixedDependencies : IOctoberKnowledgeDependencyAuthority
    {
        public KnowledgeDependencyHandle Merged { get; private set; } = Capsule('E', "merged-full-inputs");
        public Func<ImmutableArray<KnowledgeDependencyHandle>, KnowledgeDependencyHandle>? MergeResult { get; set; }
        public List<ImmutableArray<KnowledgeDependencyHandle>> MergeCalls { get; } = [];
        public List<ImmutableArray<KnowledgeDependencyHandle>> CheckCalls { get; } = [];
        public int RootCalls { get; private set; }
        public KnowledgeDependencyStatus Status { get; set; } = KnowledgeDependencyStatus.Succeeded;
        public DocumentHandle Document { get; set; } = null!;
        public ContentPage Content { get; set; } = null!;
        public DocumentHandle OtherDocument { get; set; } = null!;
        public ContentPage OtherContent { get; set; } = null!;
        public Task<KnowledgeDependencyResult> CreateNoKnowledgeRootAsync(ReadSelectionRequest selection, string initialInputDigest, CancellationToken cancellationToken = default)
        { RootCalls++; return Task.FromResult(new KnowledgeDependencyResult(KnowledgeDependencyStatus.UnknownLineage, null)); }
        public Task<KnowledgeDependencyResult> MergeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default)
        { MergeCalls.Add(dependencies); Merged = MergeResult?.Invoke(dependencies) ?? Merged; return Task.FromResult(new KnowledgeDependencyResult(KnowledgeDependencyStatus.Succeeded, Merged)); }
        public Task<KnowledgeDependencyStatus> ReauthorizeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default)
        { CheckCalls.Add(dependencies); return Task.FromResult(Status); }
        public Task<KnowledgeConsumedContentResult> BuildConsumedContentAsync(ReadSelectionRequest selection, DocumentHandle document,
            ImmutableArray<KnowledgeDependencyHandle> admittedContentPages, CancellationToken cancellationToken = default) => Task.FromResult(Receipt(document));
        public Task<KnowledgeConsumedContentResult> VerifyConsumedContentAsync(ReadSelectionRequest selection, KnowledgeConsumedContentHandle proof,
            KnowledgeDependencyHandle inferenceDependencies, CancellationToken cancellationToken = default) =>
            Task.FromResult(Receipt(proof.ReceiptId == Document.DocumentVersionId ? Document : OtherDocument));
        private KnowledgeConsumedContentResult Receipt(DocumentHandle document)
        {
            ContentPage content = document == Document ? Content : OtherContent;
            return new(KnowledgeDependencyStatus.Succeeded, new(
                new(document.DocumentVersionId, new string('F', 64), "CONTROLLED opaque content proof"),
                document, false, 1, Encoding.UTF8.GetByteCount(content.Content!), content.Coverage, Merged));
        }
    }

    private sealed class FixedReader
        : IOctoberKnowledgeDisclosedReader
    {
        public FixedReader(OctoberKnowledgeFixture knowledge)
        {
            Document = knowledge.Handle(knowledge.ClA);
            Content = new(KnowledgeReadStatus.Success, new(Document, DocumentKind.CodeLogic, "controlled title", knowledge.Claims),
                "controlled content", knowledge.Claims, null, new(true, ["qualified analysis gap"]));
            OtherDocument = knowledge.Handle(knowledge.ClB);
            OtherContent = Content with { Document = Content.Document! with { Handle = OtherDocument }, Content = "other controlled content" };
        }
        public DocumentHandle Document { get; }
        public ContentPage Content { get; }
        public DocumentHandle OtherDocument { get; }
        public ContentPage OtherContent { get; }
        public int Calls { get; private set; }
        public Task<KnowledgeDisclosureReadResult<ContentPage>> ReadExactAsync(ExactReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default)
        {
            Calls++;
            ContentPage content = request.Document == Document ? Content : OtherContent;
            ImmutableArray<byte> bytes = budget.Encoder.EncodeContent(content);
            return Task.FromResult(new KnowledgeDisclosureReadResult<ContentPage>(KnowledgeReadStatus.Success, new(content, Capsule('F', "content-page"),
                OctoberKnowledgePolicy.Hash("october.knowledge.page.v1\nContentPage\n" + OctoberKnowledgePolicy.SerializeCanonical(content)),
                budget.Encoder.ProfileId, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.AsSpan())), bytes)));
        }
        public Task<KnowledgeDisclosureReadResult<SearchPage>> SearchAsync(KnowledgeSearchRequest request, KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No search scripted.");
        public Task<KnowledgeDisclosureReadResult<RelatedPage>> GetRelatedAsync(RelatedReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No related read scripted.");
    }

    private sealed class RecordingClient : IChatClient
    {
        public List<ChatMessage[]> Requests { get; } = [];
        public Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> Script { get; set; } = token => StreamAsync("answer", "stop", token);
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        { Requests.Add(messages.ToArray()); return Script(cancellationToken); }
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }
}

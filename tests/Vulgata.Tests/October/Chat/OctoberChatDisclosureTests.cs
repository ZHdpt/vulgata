using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

/// <summary>
/// Consumer control flow over scripted canonical owner ports only. Opaque fixture capsules
/// are not cryptographic proofs; no owner codec, persistence, contributor resolution or live authorization is qualified.
/// </summary>
public sealed class OctoberChatDisclosureTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("search")]
    [InlineData("content")]
    [InlineData("related")]
    public void Encode_CompleteChatWrapperPreservesActualUtf8AndQualifications(string kind)
    {
        var harness = new Harness();
        var encoder = new OctoberChatKnowledgeOutputEncoder();
        OctoberChatToolResult expected = kind switch
        {
            "search" => new(OctoberChatToolStatus.Succeeded, Search: harness.Reader.Search, References: []),
            "content" => new(OctoberChatToolStatus.Succeeded, Content: harness.Reader.Content, References: []),
            _ => new(OctoberChatToolStatus.Succeeded, Related: harness.Reader.Related, References: []),
        };
        ImmutableArray<byte> actual = kind switch
        {
            "search" => encoder.EncodeSearch(harness.Reader.Search),
            "content" => encoder.EncodeContent(harness.Reader.Content),
            _ => encoder.EncodeRelated(harness.Reader.Related),
        };

        Assert.Equal("october.chat.tool-result.v1", encoder.ProfileId);
        Assert.Equal(Encoding.UTF8.GetBytes(OctoberWikiTools.SerializeResult(expected).GetRawText()), actual.ToArray());
        using JsonDocument json = JsonDocument.Parse(actual.AsMemory());
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        Assert.Equal("Succeeded", json.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain("CONTROLLED opaque", Encoding.UTF8.GetString(actual.AsSpan()), StringComparison.Ordinal);
        if (kind != "related") Assert.Contains("Advisory", Encoding.UTF8.GetString(actual.AsSpan()), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public void Encode_InvalidUnicodeInMetadataOrClaimRejectsWithoutReplacement(bool claim)
    {
        var harness = new Harness();
        ContentPage page = claim
            ? harness.Reader.Content with { Claims = [harness.Reader.Content.Claims[0] with { Text = "invalid\ud800claim" }] }
            : harness.Reader.Content with { Document = harness.Reader.Content.Document! with { Title = "invalid\ud800title" } };

        Assert.ThrowsAny<Exception>(() => new OctoberChatKnowledgeOutputEncoder().EncodeContent(page));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AdmitPage_UnionRetainsInitialHistoryAndUncitedSearchRelatedCapsulesAsync()
    {
        var harness = new Harness();
        KnowledgeDependencyHandle afterSearch = Capsule("root-history-search");
        KnowledgeDependencyHandle afterRelated = Capsule("root-history-search-related");
        harness.Authority.Merged.Enqueue(afterSearch);
        harness.Authority.Merged.Enqueue(afterRelated);

        await harness.Session.AdmitPageAsync(harness.Reader.SearchDependencies, null);
        await harness.Session.AdmitPageAsync(harness.Reader.RelatedDependencies, null);
        await harness.Session.ReauthorizeAsync();

        Assert.Equal(2, harness.Authority.MergeCalls.Count);
        Assert.Equal(new[] { harness.Initial, harness.Reader.SearchDependencies }, harness.Authority.MergeCalls[0].Dependencies);
        Assert.Equal(new[] { afterSearch, harness.Reader.RelatedDependencies }, harness.Authority.MergeCalls[1].Dependencies);
        Assert.Equal(afterRelated, harness.Session.InferenceDependencies);
        Assert.Contains(harness.Authority.CheckCalls, call => call.Dependencies.Contains(afterRelated));
        Assert.Empty(harness.Session.ConsumedReads);
        Assert.Equal(0, harness.Authority.RootCalls);
        Assert.Empty(harness.Authority.BuildCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Reauthorize_FreshStampAndEquivalentScopeArrayPreserveFixedIdentityAsync()
    {
        var harness = new Harness();
        harness.Current = harness.Binding.Request with
        {
            Subject = harness.Binding.Request.Subject with { AuthenticationBinding = "fresh-controlled-stamp" },
            RepositoryScope = harness.Binding.Request.RepositoryScope.ToArray().ToImmutableArray(),
        };

        await harness.Session.ReauthorizeAsync();

        Assert.NotEmpty(harness.Authority.CheckCalls);
        Assert.All(harness.Authority.CheckCalls, call => Assert.Equal("fresh-controlled-stamp", call.Selection.Subject.AuthenticationBinding));
        Assert.Equal(harness.Initial, harness.Session.InferenceDependencies);
        Assert.Null(harness.Session.FailureStatus);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("owner")]
    [InlineData("mode")]
    [InlineData("publication")]
    [InlineData("missing")]
    public async Task Reauthorize_ChangedOrMissingSelectionStopsWithoutTransplantingCapsulesAsync(string change)
    {
        var harness = new Harness();
        harness.Current = change switch
        {
            "owner" => harness.Binding.Request with { Subject = new("another-owner", "fresh") },
            "mode" => harness.Binding.Request with { Mode = KnowledgeMode.Business },
            "publication" => harness.Binding.Request with { Publication = harness.Binding.Request.Publication with { PublicationId = Guid.NewGuid() } },
            _ => null,
        };

        await Assert.ThrowsAnyAsync<Exception>(() => harness.Session.ReauthorizeAsync());

        Assert.NotNull(harness.Session.FailureStatus);
        Assert.True(harness.Budget.IsStopped);
        Assert.Empty(harness.Authority.CheckCalls);
        Assert.Equal(0, harness.Authority.RootCalls);
        Assert.Empty(harness.Authority.MergeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("Denied")]
    [InlineData("AuthorityUnavailable")]
    public async Task RunAuthorizedAsync_AwaitsFreshAuthorityBeforeSendAndEveryReleasedDeltaAsync(string refusal)
    {
        var harness = new Harness();
        harness.Authority.CheckGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstText = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ScriptedClient(token => TextAsync(token));
        var visible = new StringBuilder();
        Task<OctoberChatProviderResult> run = new OctoberChatAgent(client, harness.Tools, harness.Budget)
            .RunAuthorizedAsync("问题", "controlled static instructions", (text, _) =>
            {
                visible.Append(text);
                harness.Authority.CheckStatus = Enum.Parse<KnowledgeDependencyStatus>(refusal);
                firstText.TrySetResult();
                return ValueTask.CompletedTask;
            });
        try
        {
            Assert.Same(harness.Authority.CheckEntered.Task, await Task.WhenAny(harness.Authority.CheckEntered.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, client.Requests);
            Assert.Empty(visible.ToString());
            harness.Authority.CheckGate.TrySetResult();
            Assert.Same(firstText.Task, await Task.WhenAny(firstText.Task, run).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(run.IsCompleted);
            Assert.Equal("已获准的真实片段。", visible.ToString());
            providerGate.TrySetResult();
            OctoberChatProviderResult result = await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
            Assert.Equal("已获准的真实片段。", visible.ToString());
            Assert.Equal(visible.ToString(), result.AnswerText);
            Assert.Equal(Enum.Parse<KnowledgeDependencyStatus>(refusal), harness.Session.FailureStatus);
            Assert.True(harness.Budget.IsStopped);
            Assert.Equal(1, client.Requests);
        }
        finally
        {
            harness.Authority.CheckGate.TrySetResult();
            providerGate.TrySetResult();
            await run;
        }

        async IAsyncEnumerable<ChatResponseUpdate> TextAsync([EnumeratorCancellation] CancellationToken token)
        {
            yield return new(ChatRole.Assistant, "已获准的真实片段。");
            await providerGate.Task.WaitAsync(token);
            yield return new(ChatRole.Assistant, "撤销后禁止发布的片段");
            yield return new() { Role = ChatRole.Assistant, FinishReason = ChatFinishReason.Stop };
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAuthorizedAsync_ActualSdkUsesExactOwnerToolBytesWithoutCapsuleExposureAsync()
    {
        var harness = new Harness();
        harness.Authority.Merged.Enqueue(Capsule("root-history-uncited-search"));
        using var transport = new ScriptedHttp();
        using var guard = new OctoberChatStreamingHandler(transport, harness.Budget);
        using var http = new HttpClient(guard, disposeHandler: false);
        using IChatClient client = new OpenAI.Chat.ChatClient("controlled-model", new ApiKeyCredential("controlled-dummy-not-a-credential"), new OpenAIClientOptions
        {
            Endpoint = new Uri("https://controlled.invalid/v1/"),
            RetryPolicy = new ClientRetryPolicy(0),
            Transport = new HttpClientPipelineTransport(http, false, null),
            EnableDistributedTracing = false,
            ClientLoggingOptions = new ClientLoggingOptions { EnableLogging = false, EnableMessageLogging = false, EnableMessageContentLogging = false },
        }).AsIChatClient();
        var visible = new StringBuilder();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, harness.Tools, harness.Budget)
            .RunAuthorizedAsync("问题", "controlled static instructions", (text, _) => { visible.Append(text); return ValueTask.CompletedTask; });
        await guard.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(OctoberChatProviderStatus.Completed, result.Status);
        Assert.Equal("实际受控回答。", visible.ToString());
        Assert.Equal(2, transport.Requests.Count);
        using JsonDocument request = JsonDocument.Parse(transport.Requests[1]);
        JsonElement toolMessage = Assert.Single(request.RootElement.GetProperty("messages").EnumerateArray()
            .Where(message => message.GetProperty("role").GetString() == "tool"));
        string modelText = toolMessage.GetProperty("content").GetString()!;
        Assert.Equal(harness.Reader.LastBytes.ToArray(), Encoding.UTF8.GetBytes(modelText));
        Assert.Equal(harness.Reader.LastBytes.Length, harness.Budget.ToolResultBytes);
        using JsonDocument toolJson = JsonDocument.Parse(modelText);
        Assert.Equal(JsonValueKind.Object, toolJson.RootElement.ValueKind);
        Assert.Contains("Advisory", modelText, StringComparison.Ordinal);
        Assert.DoesNotContain("CONTROLLED opaque", modelText, StringComparison.Ordinal);
        Assert.DoesNotContain("protectedPayload", modelText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, harness.Reader.Calls);
        Assert.Equal(1, harness.Budget.ToolCalls);
        Assert.Equal(0, harness.Reader.LegacyCalls);
        Assert.Empty(harness.Session.ConsumedReads);
        Assert.Equal(0, harness.Authority.RootCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-envelope")]
    [InlineData("missing-capsule")]
    [InlineData("wrong-profile")]
    [InlineData("wrong-digest")]
    [InlineData("oversized-content")]
    public async Task InvokeForModelAsync_InvalidOwnerEnvelopeNeverFallsBackOrEarnsReadCreditAsync(string fault)
    {
        var harness = new Harness();
        harness.Reader.Fault = fault;
        if (fault == "oversized-content") harness.Reader.Content = harness.Reader.Content with { Content = new string('中', 65536) };
        string arguments = JsonSerializer.Serialize(new { document = harness.Document });
        OctoberChatToolInvocation? invocation = null;
        try { invocation = await harness.Tools.InvokeForModelAsync("ReadWiki", arguments); }
        catch (Exception) { }

        Assert.Equal(1, harness.Reader.Calls);
        Assert.Equal(0, harness.Reader.LegacyCalls);
        Assert.True(invocation is null || invocation.Result.Status != OctoberChatToolStatus.Succeeded);
        Assert.Empty(harness.Session.ConsumedReads);
        Assert.Empty(harness.Authority.BuildCalls);
        Assert.Equal(0, harness.Budget.ActiveTools);
        Assert.Equal(1, harness.Budget.ToolCalls);
        Assert.DoesNotContain(harness.Authority.MergeCalls, call => call.Dependencies.Contains(harness.Reader.ContentDependencies));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokeForModelAsync_MetadataFreeUnavailableRequiresExistingAggregateAuthorityAsync(bool denied)
    {
        var harness = new Harness();
        harness.Reader.Status = KnowledgeReadStatus.Unavailable;
        harness.Authority.CheckStatus = denied ? KnowledgeDependencyStatus.Denied : KnowledgeDependencyStatus.Succeeded;
        OctoberChatToolInvocation? invocation = null;
        try { invocation = await harness.Tools.InvokeForModelAsync("BrowseWiki", "{\"query\":\"unknown\"}"); }
        catch (Exception) when (denied) { }

        if (denied)
        {
            Assert.True(invocation is null || invocation.OutputUtf8.IsDefaultOrEmpty);
            Assert.Equal(KnowledgeDependencyStatus.Denied, harness.Session.FailureStatus);
            Assert.True(harness.Budget.IsStopped);
        }
        else
        {
            Assert.NotNull(invocation);
            Assert.Equal(OctoberChatToolStatus.Unavailable, invocation.Result.Status);
            Assert.Null(invocation.Result.Search);
            Assert.Null(invocation.Result.Content);
            Assert.Null(invocation.Result.Related);
            Assert.Empty(invocation.Result.References);
            using JsonDocument json = JsonDocument.Parse(invocation.OutputUtf8.AsMemory());
            Assert.Equal("Unavailable", json.RootElement.GetProperty("status").GetString());
            Assert.Null(harness.Session.FailureStatus);
            Assert.False(harness.Budget.IsStopped);
            Assert.Equal(1, harness.Reader.Calls);
        }
        Assert.NotEmpty(harness.Authority.CheckCalls);
        Assert.All(harness.Authority.CheckCalls, call => Assert.Equal(new[] { harness.Initial }, call.Dependencies));
        Assert.Empty(harness.Session.ConsumedReads);
        Assert.Empty(harness.Authority.MergeCalls);
        Assert.Equal(0, harness.Reader.LegacyCalls);
        Assert.Equal(0, harness.Authority.RootCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReportAsync_UsesOwnerResolvedSemanticReplayFactsAndExactConsumedProofAsync()
    {
        var harness = new Harness();
        KnowledgeDependencyHandle replay = Capsule("same-content-new-cursor-ciphertext");
        KnowledgeDependencyHandle union1 = Capsule("root-first-content");
        KnowledgeDependencyHandle union2 = Capsule("root-semantic-replay");
        harness.Authority.Merged.Enqueue(union1);
        harness.Authority.Merged.Enqueue(union2);
        var proof = new KnowledgeConsumedContentHandle(Guid.NewGuid(), new string('B', 64), "CONTROLLED opaque consumed proof");
        var ownerReceipt = new KnowledgeConsumedContentReceipt(proof, harness.Document, true, 1, 7,
            new CoverageProjection(true, ["owner source-analysis remains partial"]), Capsule("content-origin-only"));
        harness.Authority.ContentResult = new(KnowledgeDependencyStatus.Succeeded, ownerReceipt);
        await harness.Session.AdmitPageAsync(harness.Reader.ContentDependencies, harness.Document);
        await harness.Session.AdmitPageAsync(replay, harness.Document);

        Assert.Equal(2, harness.Authority.BuildCalls.Count);
        Assert.Equal(harness.Document, harness.Authority.BuildCalls[0].Document);
        Assert.Equal(new[] { harness.Reader.ContentDependencies }, harness.Authority.BuildCalls[0].Pages);
        Assert.Equal(harness.Document, harness.Authority.BuildCalls[1].Document);
        Assert.Equal(new[] { harness.Reader.ContentDependencies, replay }, harness.Authority.BuildCalls[1].Pages);
        DialogueConsumedTarget beforeReport = Assert.Single(harness.Session.ConsumedReads);
        Assert.Equal(new OctoberChatReference(harness.Document, true, 1, 7), beforeReport.Reference);
        Assert.Equal(proof, beforeReport.Proof);

        ImmutableArray<OctoberChatReference>? report = await harness.Session.ReportAsync([harness.Document, harness.Document]);

        Assert.NotNull(report);
        OctoberChatReference reference = Assert.Single(report.Value);
        Assert.Equal(new OctoberChatReference(harness.Document, true, 1, 7), reference);
        DialogueConsumedTarget saved = Assert.Single(harness.Session.ConsumedReads);
        Assert.Equal(reference, saved.Reference);
        Assert.Equal(proof, saved.Proof);
        Assert.Equal(union2, harness.Session.InferenceDependencies);
        Assert.Equal(0, harness.Reader.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InvokeForModelAsync_UncitedSuccessfulContentAlreadyHasOwnerProofBeforeBytesReturnAsync()
    {
        var harness = new Harness();
        harness.Authority.Merged.Enqueue(Capsule("root-and-uncited-content"));
        var proof = new KnowledgeConsumedContentHandle(Guid.NewGuid(), new string('C', 64), "CONTROLLED opaque uncited proof");
        long rawContentBytes = Encoding.UTF8.GetByteCount(harness.Reader.Content.Content!);
        harness.Authority.ContentResult = new(KnowledgeDependencyStatus.Succeeded,
            new(proof, harness.Document, true, 1, rawContentBytes, harness.Reader.Content.Coverage, harness.Reader.ContentDependencies));

        OctoberChatToolInvocation invocation = await harness.Tools.InvokeForModelAsync("ReadWiki",
            JsonSerializer.Serialize(new { document = harness.Document }));

        Assert.Equal(OctoberChatToolStatus.Succeeded, invocation.Result.Status);
        Assert.Empty(invocation.Result.References);
        Assert.Equal(harness.Reader.LastBytes.ToArray(), invocation.OutputUtf8.ToArray());
        Assert.Equal(invocation.OutputUtf8.Length, harness.Budget.ToolResultBytes);
        DialogueConsumedTarget consumed = Assert.Single(harness.Session.ConsumedReads);
        Assert.Equal(proof, consumed.Proof);
        Assert.Equal(new OctoberChatReference(harness.Document, true, 1, rawContentBytes), consumed.Reference);
        var built = Assert.Single(harness.Authority.BuildCalls);
        Assert.Equal(harness.Document, built.Document);
        Assert.Equal(new[] { harness.Reader.ContentDependencies }, built.Pages);
        Assert.Equal(0, harness.Reader.LegacyCalls);
        Assert.Equal(0, harness.Authority.RootCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReportAsync_UnreadTargetCannotBeReconstructedFromSearchOrReferenceIdentityAsync()
    {
        var harness = new Harness();
        harness.Authority.Merged.Enqueue(Capsule("root-search-only"));
        await harness.Session.AdmitPageAsync(harness.Reader.SearchDependencies, null);

        ImmutableArray<OctoberChatReference>? report = await harness.Session.ReportAsync([harness.Document]);

        Assert.Null(report);
        Assert.Empty(harness.Authority.BuildCalls);
        Assert.Empty(harness.Session.ConsumedReads);
        Assert.Equal(0, harness.Reader.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReauthorizeAsync_OriginalLifetimeBoundsUncooperativeOwnerWithoutLateAcceptanceAsync(bool deadline)
    {
        var clock = new ControlledChatFixture.ManualClock();
        using var cancel = new CancellationTokenSource();
        var harness = new Harness(new OctoberChatBudget(timeProvider: clock, cancellationToken: cancel.Token));
        harness.Authority.CheckGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Authority.IgnoreCheckCancellation = true;
        Task check = harness.Session.ReauthorizeAsync();
        try
        {
            Assert.Same(harness.Authority.CheckEntered.Task, await Task.WhenAny(harness.Authority.CheckEntered.Task, check).WaitAsync(TimeSpan.FromSeconds(5)));
            if (deadline) clock.Advance(TimeSpan.FromSeconds(180));
            else cancel.Cancel();
            await Assert.ThrowsAnyAsync<Exception>(async () => await check.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(check.IsCompleted);
            Assert.True(harness.Budget.IsStopped);
            Assert.NotNull(harness.Session.FailureStatus);
            Assert.False(harness.Authority.CheckGate.Task.IsCompleted);
        }
        finally
        {
            harness.Authority.CheckGate.TrySetResult();
            try { await check; } catch (Exception) { }
            await harness.Authority.CheckSettled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.NotNull(harness.Session.FailureStatus);
        Assert.Empty(harness.Authority.MergeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAuthorizedAsync_UncitedSuccessfulToolUnionIsRecheckedBeforeNextProviderRoundAsync()
    {
        var harness = new Harness();
        KnowledgeDependencyHandle fullUnion = Capsule("initial-history-and-uncited-search");
        harness.Authority.Merged.Enqueue(fullUnion);
        using var client = new ScriptedClient(token => ToolRoundAsync(token));
        var visible = new StringBuilder();
        var progress = new List<OctoberChatToolProgress>();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, harness.Tools, harness.Budget)
            .RunAuthorizedAsync("问题", "controlled instructions", (text, _) => { visible.Append(text); return ValueTask.CompletedTask; },
                onToolProgress: item =>
                {
                    progress.Add(item);
                    if (item.Kind == OctoberChatToolProgressKind.Completed) harness.Authority.CheckStatus = KnowledgeDependencyStatus.Denied;
                });

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Equal(1, client.Requests);
        Assert.Equal(1, harness.Reader.Calls);
        Assert.Equal(1, harness.Budget.ToolCalls);
        Assert.Contains(progress, item => item.Kind == OctoberChatToolProgressKind.Completed);
        Assert.Equal(fullUnion, harness.Session.InferenceDependencies);
        Assert.Contains(harness.Authority.CheckCalls, call => call.Dependencies.Contains(fullUnion));
        Assert.Equal(KnowledgeDependencyStatus.Denied, harness.Session.FailureStatus);
        Assert.Empty(harness.Session.ConsumedReads);
        Assert.Empty(visible.ToString());
        Assert.Empty(result.AnswerText);

        static async IAsyncEnumerable<ChatResponseUpdate> ToolRoundAsync([EnumeratorCancellation] CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            yield return new() { Role = ChatRole.Assistant, Contents = [new FunctionCallContent("controlled-call", "BrowseWiki", new Dictionary<string, object?> { ["query"] = "" })] };
            yield return new() { Role = ChatRole.Assistant, FinishReason = ChatFinishReason.ToolCalls };
            await Task.CompletedTask;
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("merge", false)]
    [InlineData("merge", true)]
    [InlineData("proof", false)]
    [InlineData("proof", true)]
    public async Task InvokeForModelAsync_LateOwnerMergeOrProofCannotPublishOrMutateAfterOriginalLifetimeAsync(string operation, bool deadline)
    {
        var clock = new ControlledChatFixture.ManualClock();
        using var cancel = new CancellationTokenSource();
        var harness = new Harness(new OctoberChatBudget(timeProvider: clock, cancellationToken: cancel.Token));
        harness.Authority.Merged.Enqueue(Capsule("controlled-content-union"));
        harness.Authority.ContentResult = new(KnowledgeDependencyStatus.Succeeded,
            new(new(Guid.NewGuid(), new string('D', 64), "CONTROLLED opaque late proof"), harness.Document, true, 1,
                Encoding.UTF8.GetByteCount(harness.Reader.Content.Content!), harness.Reader.Content.Coverage, harness.Reader.ContentDependencies));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (operation == "merge") harness.Authority.MergeGate = release;
        else harness.Authority.BuildGate = release;
        Task entered = operation == "merge" ? harness.Authority.MergeEntered.Task : harness.Authority.BuildEntered.Task;
        Task settled = operation == "merge" ? harness.Authority.MergeSettled.Task : harness.Authority.BuildSettled.Task;
        Task<OctoberChatToolInvocation> run = harness.Tools.InvokeForModelAsync("ReadWiki", JsonSerializer.Serialize(new { document = harness.Document }));
        KnowledgeDependencyHandle? beforeCancellation = null;
        OctoberChatToolInvocation? invocation = null;
        try
        {
            Assert.Same(entered, await Task.WhenAny(entered, run).WaitAsync(TimeSpan.FromSeconds(5)));
            beforeCancellation = harness.Session.InferenceDependencies;
            Assert.Empty(harness.Session.ConsumedReads);
            if (deadline) clock.Advance(TimeSpan.FromSeconds(180));
            else cancel.Cancel();
            try { invocation = await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception) { }
            Assert.True(run.IsCompleted); // Timing out the test's wait does not establish bounded consumer completion.
            Assert.True(invocation is null || invocation.Result.Status != OctoberChatToolStatus.Succeeded);
            Assert.False(release.Task.IsCompleted);
            Assert.Empty(harness.Session.ConsumedReads);
            Assert.Equal(beforeCancellation, harness.Session.InferenceDependencies);
            Assert.NotNull(harness.Session.FailureStatus);
        }
        finally
        {
            cancel.Cancel();
            release.TrySetResult();
            try { await run; } catch (Exception) { }
            await settled.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(beforeCancellation, harness.Session.InferenceDependencies);
        Assert.Empty(harness.Session.ConsumedReads);
        Assert.True(invocation is null || invocation.Result.Content is null);
        Assert.Equal(0, harness.Reader.LegacyCalls);
        Assert.Equal(0, harness.Authority.RootCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RunAsync_CanonicalToolsCannotUseUncheckedSynchronousTextEntryAsync()
    {
        var harness = new Harness();
        using var client = new ScriptedClient(_ => throw new InvalidOperationException("No dispatch expected."));
        var text = new StringBuilder();

        OctoberChatProviderResult result = await new OctoberChatAgent(client, harness.Tools, harness.Budget)
            .RunAsync("问题", "controlled instructions", delta => text.Append(delta));

        Assert.Equal(OctoberChatProviderStatus.Failed, result.Status);
        Assert.Equal(0, client.Requests);
        Assert.Empty(text.ToString());
        Assert.Equal(0, harness.Reader.LegacyCalls);
    }

    private static KnowledgeDependencyHandle Capsule(string label) => new(Guid.NewGuid(),
        OctoberKnowledgePolicy.Hash(label), "CONTROLLED opaque " + label);

    private sealed class Harness
    {
        public Harness(OctoberChatBudget? budget = null)
        {
            var knowledge = new OctoberKnowledgeFixture();
            ReadSelectionRequest selection = knowledge.Selection() with { Purpose = KnowledgePurpose.Chat };
            Binding = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), selection, new("CONTROLLED selected token"));
            Current = selection;
            Budget = budget ?? new();
            Document = knowledge.Handle(knowledge.ClA);
            var summary = new DocumentSummary(Document, DocumentKind.CodeLogic, "中文😀\"quoted\"/path", knowledge.Claims);
            Reader = new ScriptedReader
            {
                Search = new(KnowledgeReadStatus.Success, [summary], new("CONTROLLED continuation"), new(true, ["qualified gap"])),
                Content = new(KnowledgeReadStatus.Success, summary, "中文😀\"quoted\"\ncontent", knowledge.Claims, new("CONTROLLED content cursor"), new(true, ["qualified gap"])),
                Related = new(KnowledgeReadStatus.Success, [new(RelationshipKind.References, Document, knowledge.Handle(knowledge.ClB), "accepted qualified relation")
                    { RelationshipVersionId = Guid.NewGuid() }], [knowledge.Unknown], null, null),
            };
            Session = new(Binding, Budget, Authority, _ => Task.FromResult(Current), Initial);
            Tools = new(Reader, Binding, Budget, Session);
        }
        public OctoberChatBudget Budget { get; }
        public OctoberChatTurnBinding Binding { get; }
        public ReadSelectionRequest? Current { get; set; }
        public DocumentHandle Document { get; }
        public KnowledgeDependencyHandle Initial { get; } = Capsule("initial-question-static-and-retained-history");
        public ScriptedAuthority Authority { get; } = new();
        public ScriptedReader Reader { get; }
        public OctoberChatDisclosureSession Session { get; }
        public OctoberWikiTools Tools { get; }
    }

    private sealed class ScriptedAuthority : IOctoberKnowledgeDependencyAuthority
    {
        public Queue<KnowledgeDependencyHandle> Merged { get; } = new();
        public List<(ReadSelectionRequest Selection, ImmutableArray<KnowledgeDependencyHandle> Dependencies)> MergeCalls { get; } = [];
        public List<(ReadSelectionRequest Selection, ImmutableArray<KnowledgeDependencyHandle> Dependencies)> CheckCalls { get; } = [];
        public List<(DocumentHandle Document, ImmutableArray<KnowledgeDependencyHandle> Pages)> BuildCalls { get; } = [];
        public KnowledgeDependencyStatus CheckStatus { get; set; } = KnowledgeDependencyStatus.Succeeded;
        public KnowledgeConsumedContentResult ContentResult { get; set; } = new(KnowledgeDependencyStatus.InvalidProof, null);
        public int RootCalls { get; private set; }
        public TaskCompletionSource? CheckGate { get; set; }
        public bool IgnoreCheckCancellation { get; set; }
        public TaskCompletionSource CheckEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CheckSettled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? MergeGate { get; set; }
        public TaskCompletionSource? BuildGate { get; set; }
        public TaskCompletionSource MergeEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource MergeSettled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource BuildEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource BuildSettled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<KnowledgeDependencyResult> CreateNoKnowledgeRootAsync(ReadSelectionRequest selection, string initialInputDigest, CancellationToken cancellationToken = default)
        {
            RootCalls++;
            return Task.FromResult(new KnowledgeDependencyResult(KnowledgeDependencyStatus.UnknownLineage, null));
        }
        public async Task<KnowledgeDependencyResult> MergeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default)
        {
            MergeCalls.Add((selection, dependencies));
            MergeEntered.TrySetResult();
            try
            {
                if (MergeGate is not null) await MergeGate.Task; // Owned gate deliberately ignores cancellation.
                return Merged.Count == 0 ? new KnowledgeDependencyResult(KnowledgeDependencyStatus.UnknownLineage, null)
                    : new(KnowledgeDependencyStatus.Succeeded, Merged.Dequeue());
            }
            finally { MergeSettled.TrySetResult(); }
        }
        public async Task<KnowledgeDependencyStatus> ReauthorizeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default)
        {
            CheckCalls.Add((selection, dependencies));
            CheckEntered.TrySetResult();
            try
            {
                if (CheckGate is not null)
                {
                    if (IgnoreCheckCancellation) await CheckGate.Task;
                    else await CheckGate.Task.WaitAsync(cancellationToken);
                }
                return CheckStatus;
            }
            finally { CheckSettled.TrySetResult(); }
        }
        public async Task<KnowledgeConsumedContentResult> BuildConsumedContentAsync(ReadSelectionRequest selection, DocumentHandle document,
            ImmutableArray<KnowledgeDependencyHandle> admittedContentPages, CancellationToken cancellationToken = default)
        {
            BuildCalls.Add((document, admittedContentPages));
            BuildEntered.TrySetResult();
            try
            {
                if (BuildGate is not null) await BuildGate.Task; // Owned gate deliberately ignores cancellation.
                return ContentResult;
            }
            finally { BuildSettled.TrySetResult(); }
        }
        public Task<KnowledgeConsumedContentResult> VerifyConsumedContentAsync(ReadSelectionRequest selection, KnowledgeConsumedContentHandle proof,
            KnowledgeDependencyHandle inferenceDependencies, CancellationToken cancellationToken = default) => Task.FromResult(ContentResult);
    }

    /// <summary>Fixed owner responses and mechanical encoder callback; no owner-side hidden contributor or cursor implementation.</summary>
    private sealed class ScriptedReader : IOctoberKnowledgeDisclosedReader, IOctoberKnowledgeReader
    {
        public required SearchPage Search { get; init; }
        public required ContentPage Content { get; set; }
        public required RelatedPage Related { get; init; }
        public KnowledgeDependencyHandle SearchDependencies { get; } = Capsule("uncited-search-and-advisory-origins");
        public KnowledgeDependencyHandle ContentDependencies { get; } = Capsule("exact-content-transitive-origins");
        public KnowledgeDependencyHandle RelatedDependencies { get; } = Capsule("uncited-related-only-origins");
        public int Calls { get; private set; }
        public int LegacyCalls { get; private set; }
        public string? Fault { get; set; }
        public KnowledgeReadStatus Status { get; set; } = KnowledgeReadStatus.Success;
        public ImmutableArray<byte> LastBytes { get; private set; }

        public Task<KnowledgeDisclosureReadResult<SearchPage>> SearchAsync(KnowledgeSearchRequest request, KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
            Task.FromResult(Envelope(Search, SearchDependencies, budget, () => budget.Encoder.EncodeSearch(Search)));
        public Task<KnowledgeDisclosureReadResult<ContentPage>> ReadExactAsync(ExactReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
            Task.FromResult(Envelope(Content, ContentDependencies, budget, () => budget.Encoder.EncodeContent(Content)));
        public Task<KnowledgeDisclosureReadResult<RelatedPage>> GetRelatedAsync(RelatedReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default) =>
            Task.FromResult(Envelope(Related, RelatedDependencies, budget, () => budget.Encoder.EncodeRelated(Related)));

        private KnowledgeDisclosureReadResult<T> Envelope<T>(T page, KnowledgeDependencyHandle dependencies, KnowledgeReadOutputBudget budget,
            Func<ImmutableArray<byte>> encode) where T : class
        {
            Calls++;
            if (Status != KnowledgeReadStatus.Success) return new(Status, null);
            ImmutableArray<byte> bytes = encode();
            LastBytes = bytes;
            if (Fault == "missing-envelope") return new(KnowledgeReadStatus.Success, null);
            string pageDigest = OctoberKnowledgePolicy.Hash("october.knowledge.page.v1\n" + typeof(T).Name + "\n" + OctoberKnowledgePolicy.SerializeCanonical(page));
            string outputDigest = Convert.ToHexString(SHA256.HashData(bytes.AsSpan()));
            return new(KnowledgeReadStatus.Success, new(page, Fault == "missing-capsule" ? null! : dependencies, pageDigest,
                Fault == "wrong-profile" ? "foreign.profile" : budget.Encoder.ProfileId,
                Fault == "wrong-digest" ? new string('0', 64) : outputDigest, bytes));
        }

        Task<ReadSelectionResult> IOctoberKnowledgeReader.OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken) => Legacy<ReadSelectionResult>();
        Task<SearchPage> IOctoberKnowledgeReader.SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken) => Legacy<SearchPage>();
        Task<ContentPage> IOctoberKnowledgeReader.ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken) => Legacy<ContentPage>();
        Task<RelatedPage> IOctoberKnowledgeReader.GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken) => Legacy<RelatedPage>();
        private Task<T> Legacy<T>()
        {
            LegacyCalls++;
            return Task.FromException<T>(new InvalidOperationException("Legacy fallback is not authorized."));
        }
    }

    private sealed class ScriptedClient(Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> script) : IChatClient
    {
        public int Requests { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Requests++;
            return script(cancellationToken);
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }

    private sealed class ScriptedHttp : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string body = Requests.Count switch
            {
                1 => Event("{\"tool_calls\":[{\"index\":0,\"id\":\"controlled-call\",\"type\":\"function\",\"function\":{\"name\":\"BrowseWiki\",\"arguments\":\"{\\\"query\\\":\\\"\\\"}\"}}]}") + Event("{}", "tool_calls"),
                2 => Event("{\"content\":\"实际受控回答。\"}") + Event("{}", "stop"),
                _ => throw new InvalidOperationException("Unexpected additional controlled request."),
            };
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body + "data: [DONE]\n\n")) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return response;
        }
        private static string Event(string delta, string? finish = null) =>
            "data: {\"id\":\"controlled-response\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"controlled-model\",\"choices\":[{\"index\":0,\"delta\":" + delta + ",\"finish_reason\":" + JsonSerializer.Serialize(finish) + "}]}\n\n";
    }
}

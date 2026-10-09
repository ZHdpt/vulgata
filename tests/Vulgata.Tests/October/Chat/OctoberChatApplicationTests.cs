using Bunit;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Shared;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Components.Pages;
using Vulgata.Web.Data;
using Vulgata.Web.Explore;
using Vulgata.Web.Services.OctoberChat;
using Vulgata.Web.ViewModels.OctoberChat;

namespace Vulgata.Tests.October.Chat;

public sealed class OctoberChatApplicationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ChatPage_ValidDurableLocatorReloadsExactOwnerTranscriptWithoutCreateOrGenerationAsync()
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.EnqueueComplete(Text("持久重新打开内容。") + Stop + Done);
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        var original = Assert.IsType<OctoberChatCoordinationResult>(await fixture.Application.SubmitAsync("精确原问题")
            .WaitAsync(TimeSpan.FromSeconds(10)));
        var receipt = Assert.IsType<DialogueTerminalReceipt>(original.Terminal!.Receipt);
        fixture.Application.Detach();
        int opensBefore = fixture.OpenRequests.Count;
        var reload = await fixture.CreateReattachmentAsync();
        using var context = new ChatPageTests();
        var navigation = new RecordedNavigation($"?conversationId={receipt.ConversationId:D}&contextId={receipt.ContextId:D}");
        context.Services.AddSingleton<NavigationManager>(navigation);
        var page = context.Render<ChatPage>(parameters => parameters.Add(item => item.OctoberEntry, reload.Entry)
            .Add(item => item.OctoberSelection, reload.Selection).Add(item => item.OctoberApplication, reload.Application));
        page.WaitForAssertion(() =>
        {
            Assert.True(reload.Application.IsReady);
            Assert.Equal(receipt.ConversationId, reload.Application.Conversation!.ConversationId);
            Assert.Equal(receipt.ContextId, reload.Application.Conversation.ContextId);
            var turn = Assert.Single(reload.Application.Transcript);
            Assert.Equal(receipt.TurnId, turn.TurnId);
            Assert.Equal("精确原问题", turn.Question);
            Assert.Equal("持久重新打开内容。", turn.AnswerText);
            Assert.Contains("持久重新打开内容。", page.Find(".october-chat-answer").TextContent);
        });
        var requests = fixture.OpenRequests.Skip(opensBefore).ToArray();
        Assert.NotEmpty(requests);
        Assert.All(requests, request =>
        {
            Assert.Null(request.CreateIfMissing);
            Assert.Equal(receipt.ConversationId, request.ConversationId);
            Assert.Equal(receipt.ContextId, request.ContextId);
        });
        Assert.Single(fixture.RequestBodies);
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);
        var query = QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query);
        Assert.Equal(new[] { "contextId", "conversationId" }, query.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(receipt.ConversationId.ToString("D"), query["conversationId"].ToString());
        Assert.Equal(receipt.ContextId.ToString("D"), query["contextId"].ToString());
        Assert.DoesNotContain("精确原问题", navigation.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Selection.Subject.AuthenticationBinding, navigation.Uri, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("#chat-question"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("?conversationId=not-a-guid&contextId=22222222-2222-2222-2222-222222222222")]
    [InlineData("?conversationId=11111111-1111-1111-1111-111111111111")]
    [InlineData("?contextId=22222222-2222-2222-2222-222222222222")]
    [InlineData("?conversationId=&contextId=22222222-2222-2222-2222-222222222222")]
    [InlineData("?conversationId=00000000-0000-0000-0000-000000000000&contextId=22222222-2222-2222-2222-222222222222")]
    [InlineData("?conversationId=11111111-1111-1111-1111-111111111111&conversationId=11111111-1111-1111-1111-111111111111&contextId=22222222-2222-2222-2222-222222222222")]
    public async Task ChatPage_InvalidDurableLocatorFailsBeforeAnyCanonicalCreateOrLegacyFallbackAsync(string query)
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        using var context = new ChatPageTests();
        var navigation = new RecordedNavigation(query);
        context.Services.AddSingleton<NavigationManager>(navigation);
        var page = fixture.Render(context);
        Assert.False(fixture.Application.IsReady);
        Assert.Empty(fixture.OpenRequests);
        Assert.Empty(fixture.RequestBodies);
        Assert.Empty(page.FindAll("#chat-question"));
        Assert.All(page.FindAll("textarea"), field => Assert.True(field.HasAttribute("disabled")));
        Assert.Equal("http://localhost/" + query, navigation.Uri);
        Assert.Empty(navigation.Navigations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("?conversationId=11111111-1111-1111-1111-111111111111&contextId=22222222-2222-2222-2222-222222222222")]
    [InlineData("?conversationId=malformed")]
    public void ChatPage_SuppliedLocatorWithoutOctoberCompositionCannotEnterLegacy(string query)
    {
        using var context = new ChatPageTests();
        var navigation = new RecordedNavigation(query);
        context.Services.AddSingleton<NavigationManager>(navigation);
        var page = context.Render<ChatPage>();
        Assert.Empty(page.FindAll("#chat-question"));
        Assert.Empty(page.FindAll(".repository-picker__trigger"));
        Assert.DoesNotContain("mbs_account", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mbs_channel", page.Markup, StringComparison.Ordinal);
        Assert.All(page.FindAll("textarea"), field => Assert.True(field.HasAttribute("disabled")));
        Assert.Empty(navigation.Navigations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("unknown")]
    [InlineData("owner")]
    [InlineData("scope")]
    [InlineData("mode")]
    public async Task ChatPage_UnavailableDurableLocatorNeverCreatesReplacementOrDisclosesAnotherContextAsync(string scenario)
    {
        long setupStarted = Stopwatch.GetTimestamp();
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.EnqueueComplete(Text("原会话受限内容。") + Stop + Done);
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        int disposedBeforeSubmit = fixture.DisposedScopes;
        long submitStarted = Stopwatch.GetTimestamp();
        Task<OctoberChatCoordinationResult?> submit = fixture.Application.SubmitAsync("原会话问题");
        OctoberChatCoordinationResult? submitted;
        try
        {
            submitted = await submit.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException error)
        {
            // Capture before await-using teardown cancels/detaches the application.
            // These independently sampled values are diagnostic, not an atomic state or release proof.
            string snapshot = FormattableString.Invariant($"phase=initial-valid-turn; setupMs={Stopwatch.GetElapsedTime(setupStarted).TotalMilliseconds:F1}; submitMs={Stopwatch.GetElapsedTime(submitStarted).TotalMilliseconds:F1}; task={submit.Status}; busy={fixture.Application.IsBusy}; ready={fixture.Application.IsReady}; uncertain={fixture.Application.IsSaveUncertain}; opens={fixture.OpenRequests.Count}; begins={fixture.BeginCalls}; providerRequests={fixture.RequestBodies.Count}; saves={fixture.SaveCalls}; saveStatus={fixture.LastSaveResult?.Status}; receiptPresent={fixture.LastSaveResult?.Receipt is not null}; terminalState={fixture.LastSaveResult?.Receipt?.TerminalState}; terminalReason={fixture.LastSaveResult?.Receipt?.Reason}; lookups={fixture.LookupCalls}; admissionLookups={fixture.AdmissionLookups.Count}; activeRegistrations={fixture.Registry.ActiveRegistrations}; activeRequests={fixture.Requests.ActiveRequests}; disposedBefore={disposedBeforeSubmit}; disposedNow={fixture.DisposedScopes}; visibleTurns={fixture.Application.Turns.Count}; transcriptTurns={fixture.Application.Transcript.Length}");
            // Observe an eventual fault without waiting longer or changing the timeout assertion.
            _ = submit.ContinueWith(completed => { _ = completed.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw new TimeoutException("Initial valid-turn setup exceeded the unchanged 10-second wait. " + snapshot, error);
        }
        var saved = Assert.IsType<OctoberChatCoordinationResult>(submitted);
        var receipt = saved.Terminal!.Receipt!;
        fixture.Application.Detach();
        var selection = scenario switch
        {
            "owner" => fixture.Selection with { Subject = new("foreign-owner", "foreign-stamp") },
            "scope" => fixture.Selection with { RepositoryScope = [Guid.NewGuid()] },
            "mode" => fixture.Selection with { Mode = KnowledgeMode.Business },
            _ => fixture.Selection
        };
        var reload = await fixture.CreateReattachmentAsync(selection);
        int priorOpens = fixture.OpenRequests.Count;
        string query = $"?conversationId={(scenario == "unknown" ? Guid.NewGuid() : receipt.ConversationId):D}&contextId={receipt.ContextId:D}";
        using var context = new ChatPageTests();
        var navigation = new RecordedNavigation(query);
        context.Services.AddSingleton<NavigationManager>(navigation);
        var page = context.Render<ChatPage>(parameters => parameters.Add(item => item.OctoberEntry, reload.Entry)
            .Add(item => item.OctoberSelection, reload.Selection).Add(item => item.OctoberApplication, reload.Application));
        Assert.False(reload.Application.IsReady);
        Assert.Empty(reload.Application.Turns);
        Assert.Empty(reload.Application.Transcript);
        Assert.DoesNotContain("原会话受限内容。", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("#chat-question"));
        Assert.All(fixture.OpenRequests.Skip(priorOpens), request => Assert.Null(request.CreateIfMissing));
        Assert.Single(fixture.RequestBodies);
        Assert.Equal("http://localhost/" + query, navigation.Uri);
        Assert.Empty(navigation.Navigations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChatPage_ActiveOrUnresolvedLocatorCannotStartDuplicateExecutionAsync(bool uncertainSave)
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.LoseSaveAcknowledgement = uncertainSave;
        var body = fixture.Enqueue(Text("已有执行。"), Stop + Done);
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        var pending = fixture.Application.SubmitAsync("唯一执行");
        try
        {
            await body.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (uncertainSave)
            {
                body.Release.TrySetResult();
                await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(fixture.Application.IsSaveUncertain);
            }
            var reload = await fixture.CreateReattachmentAsync();
            using var context = new ChatPageTests();
            context.Services.AddSingleton<NavigationManager>(new RecordedNavigation(
                $"?conversationId={fixture.Conversation.ConversationId:D}&contextId={fixture.Conversation.ContextId:D}"));
            var page = context.Render<ChatPage>(parameters => parameters.Add(item => item.OctoberEntry, reload.Entry)
                .Add(item => item.OctoberSelection, reload.Selection).Add(item => item.OctoberApplication, reload.Application));
            Assert.False(reload.Application.IsReady);
            Assert.Null(await reload.Application.SubmitAsync("不得重新执行"));
            Assert.Single(fixture.RequestBodies);
            Assert.Equal(1, fixture.Registry.ActiveRegistrations);
            Assert.All(page.FindAll("textarea"), field => Assert.True(field.HasAttribute("disabled")));
        }
        finally
        {
            body.Release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (uncertainSave) await fixture.Application.ReconcileAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ChatPage_ConfirmedNewConversationReplacesDurableLocatorOnlyAfterExplicitConfirmationAsync()
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.EnqueueComplete(Text("应从新会话移除。") + Stop + Done);
        using var context = new ChatPageTests();
        var navigation = new RecordedNavigation("");
        context.Services.AddSingleton<NavigationManager>(navigation);
        var page = fixture.Render(context);
        page.WaitForAssertion(() => Assert.True(fixture.Application.IsReady));
        Guid oldConversation = fixture.Application.Conversation!.ConversationId;
        Guid oldContext = fixture.Application.Conversation.ContextId;
        await page.InvokeAsync(() => page.Find("#october-chat-question").Input("先前的问题"));
        await page.InvokeAsync(() => page.Find(".composer-action").ClickAsync(new())).WaitAsync(TimeSpan.FromSeconds(10));
        string oldUri = navigation.Uri;
        Assert.Contains(oldConversation.ToString("D"), oldUri, StringComparison.Ordinal);
        await page.InvokeAsync(() => page.Find(".new-conversation-button").ClickAsync(new()));
        Assert.True(fixture.Entry.RequiresConfirmation);
        Assert.Equal(oldUri, navigation.Uri);
        Assert.Equal(oldConversation, fixture.Application.Conversation!.ConversationId);
        await page.InvokeAsync(() => page.FindAll("fluent-button").Single(button => button.TextContent.Trim() == "确认开始新对话").ClickAsync(new()));
        page.WaitForAssertion(() => Assert.True(fixture.Application.IsReady));
        Assert.NotEqual(oldConversation, fixture.Application.Conversation!.ConversationId);
        Assert.NotEqual(oldContext, fixture.Application.Conversation.ContextId);
        Assert.Empty(fixture.Application.Transcript);
        Assert.Empty(fixture.Application.Turns);
        Assert.DoesNotContain("应从新会话移除。", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(oldConversation.ToString("D"), navigation.Uri, StringComparison.Ordinal);
        var query = QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query);
        Assert.Equal(new[] { "contextId", "conversationId" }, query.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(fixture.Application.Conversation.ConversationId.ToString("D"), query["conversationId"].ToString());
        Assert.Equal(fixture.Application.Conversation.ContextId.ToString("D"), query["contextId"].ToString());
        Assert.NotEmpty(navigation.Navigations);
        Assert.All(navigation.Navigations, entry => Assert.True(entry.Options.ReplaceHistoryEntry));
        Assert.Single(fixture.RequestBodies);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("")]
    [InlineData("?conversationId=invalid")]
    public async Task ChatPage_SameInstanceLocatorNavigationReopensExactContextAndCannotSilentlyCreateOnRemovalAsync(string finalQuery)
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.EnqueueComplete(Text("会话甲回答。") + Stop + Done);
        fixture.EnqueueComplete(Text("会话乙回答。") + Stop + Done);
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        var first = Assert.IsType<OctoberChatCoordinationResult>(await fixture.Application.SubmitAsync("会话甲问题")
            .WaitAsync(TimeSpan.FromSeconds(10)));
        var a = first.Terminal!.Receipt!;
        fixture.Application.Detach();
        var secondAttachment = await fixture.CreateReattachmentAsync();
        secondAttachment.Application.Attach(callback => callback().AsTask());
        Assert.True(await secondAttachment.Application.OpenAsync(secondAttachment.Entry.Conversation!));
        var second = Assert.IsType<OctoberChatCoordinationResult>(await secondAttachment.Application.SubmitAsync("会话乙问题")
            .WaitAsync(TimeSpan.FromSeconds(10)));
        var b = second.Terminal!.Receipt!;
        secondAttachment.Application.Detach();

        var reload = await fixture.CreateReattachmentAsync();
        int opensBefore = fixture.OpenRequests.Count;
        using var context = new ChatPageTests();
        // Braced GUIDs are valid locators; the parent's own canonical replacement must not reopen.
        var navigation = new RecordedNavigation($"?conversationId={a.ConversationId:B}&contextId={a.ContextId:B}");
        context.Services.AddSingleton<NavigationManager>(navigation);
        var page = context.Render<ChatPage>(parameters => parameters.Add(item => item.OctoberEntry, reload.Entry)
            .Add(item => item.OctoberSelection, reload.Selection).Add(item => item.OctoberApplication, reload.Application));
        page.WaitForAssertion(() => Assert.Equal(a.ConversationId, reload.Application.Conversation?.ConversationId));
        await page.InvokeAsync(() => Task.CompletedTask);
        Assert.Equal(opensBefore + 1, fixture.OpenRequests.Count);
        Assert.Single(navigation.Navigations);
        Assert.True(navigation.Navigations.Single().Options.ReplaceHistoryEntry);

        int opensAfterA = fixture.OpenRequests.Count;
        await page.InvokeAsync(() => navigation.NavigateTo($"/?conversationId={b.ConversationId:D}&contextId={b.ContextId:D}"));
        page.WaitForAssertion(() =>
        {
            Assert.True(reload.Application.IsReady);
            Assert.Equal(b.ConversationId, reload.Application.Conversation!.ConversationId);
            Assert.Equal(b.ContextId, reload.Application.Conversation.ContextId);
            Assert.Equal(b.TurnId, Assert.Single(reload.Application.Transcript).TurnId);
            Assert.Contains("会话乙回答。", page.Find(".chat-surface").TextContent);
            Assert.DoesNotContain("会话甲回答。", page.Find(".chat-surface").TextContent, StringComparison.Ordinal);
        });
        var opens = fixture.OpenRequests.Skip(opensAfterA).ToArray();
        Assert.Single(opens);
        Assert.Null(opens[0].CreateIfMissing);
        Assert.Equal(b.ConversationId, opens[0].ConversationId);
        Assert.Equal(b.ContextId, opens[0].ContextId);
        Assert.Equal(2, fixture.RequestBodies.Count);

        int beforeUnavailable = fixture.OpenRequests.Count;
        await page.InvokeAsync(() => navigation.NavigateTo("/" + finalQuery));
        page.WaitForAssertion(() =>
        {
            Assert.False(reload.Application.IsReady);
            Assert.Empty(reload.Application.Turns);
            Assert.Empty(reload.Application.Transcript);
            Assert.DoesNotContain("会话乙回答。", page.Find(".chat-surface").TextContent, StringComparison.Ordinal);
        });
        Assert.Equal(beforeUnavailable, fixture.OpenRequests.Count);
        Assert.Equal(2, fixture.RequestBodies.Count);
        Assert.Empty(page.FindAll("#chat-question"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ChatPage_LatestLocatorWhilePriorOwnerOpenIsPendingIsConsumedAfterActualSettlementAsync()
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        var a = fixture.Application.Conversation!;
        fixture.Application.Detach();
        var second = await fixture.CreateReattachmentAsync();
        second.Application.Attach(callback => callback().AsTask());
        Assert.True(await second.Application.OpenAsync(second.Entry.Conversation!));
        var b = second.Application.Conversation!;
        second.Application.Detach();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.AfterOpen = async (request, result) =>
        {
            if (request.ConversationId != a.ConversationId) return;
            Assert.Equal(DialogueStatus.Existing, result.Status);
            entered.TrySetResult();
            await release.Task; // Retain the actual owner's completed lookup while its acknowledgement is pending.
        };
        var reload = await fixture.CreateReattachmentAsync();
        int baseline = fixture.OpenRequests.Count;
        using var context = new ChatPageTests();
        var navigation = new RecordedNavigation($"?conversationId={a.ConversationId:D}&contextId={a.ContextId:D}");
        context.Services.AddSingleton<NavigationManager>(navigation);
        var page = context.Render<ChatPage>(parameters => parameters.Add(item => item.OctoberEntry, reload.Entry)
            .Add(item => item.OctoberSelection, reload.Selection).Add(item => item.OctoberApplication, reload.Application));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(reload.Application.IsBusy);
            Assert.Equal(1, fixture.Registry.ActiveRegistrations);
            await page.InvokeAsync(() => navigation.NavigateTo($"/?conversationId={b.ConversationId:D}&contextId={b.ContextId:D}"));
            Assert.Empty(reload.Application.Turns);
            Assert.Empty(reload.Application.Transcript);
            Assert.Single(fixture.OpenRequests.Skip(baseline));
            Assert.Equal(1, fixture.Registry.ActiveRegistrations);
            release.TrySetResult();
            page.WaitForAssertion(() =>
            {
                Assert.True(reload.Application.IsReady);
                Assert.Equal(b.ConversationId, reload.Application.Conversation!.ConversationId);
                Assert.Equal(b.ContextId, reload.Application.Conversation.ContextId);
                Assert.Equal(0, fixture.Registry.ActiveRegistrations);
            }, TimeSpan.FromSeconds(10));
            var opened = fixture.OpenRequests.Skip(baseline).ToArray();
            Assert.Equal(new[] { a.ConversationId, b.ConversationId }, opened.Select(request => request.ConversationId));
            Assert.All(opened, request => Assert.Null(request.CreateIfMissing));
            Assert.Contains(b.ConversationId.ToString("D"), navigation.Uri, StringComparison.Ordinal);
            Assert.DoesNotContain(a.ConversationId.ToString("D"), navigation.Uri, StringComparison.Ordinal);
            Assert.Empty(fixture.RequestBodies);
        }
        finally
        {
            release.TrySetResult();
            fixture.AfterOpen = null;
            page.WaitForAssertion(() => Assert.False(reload.Application.IsBusy), TimeSpan.FromSeconds(10));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ChatPage_TwoActualTurns_StreamBeforeCompletionThenUseExactOwnerHistoryAsync()
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        var first = fixture.Enqueue(Text("第一段即时文本。"), Tool(fixture.Document) + Done);
        fixture.EnqueueComplete(Report(fixture.Document) + Done);
        var continuation = fixture.Enqueue(Text("查阅完成。"), Stop + Done);
        fixture.EnqueueComplete(Text("第二轮回答。") + Stop + Done);
        using var context = new ChatPageTests();
        var page = fixture.Render(context);
        page.WaitForAssertion(() => Assert.True(fixture.Application.IsReady));
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);
        Task? firstClick = null;
        try
        {
            await page.InvokeAsync(() => page.Find("#october-chat-question").Input("第一轮精确问题？"));
            firstClick = page.InvokeAsync(() => page.Find(".composer-action").ClickAsync(new()));
            await first.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            page.WaitForAssertion(() => Assert.Contains("第一段即时文本。", page.Find(".october-chat-answer").TextContent));
            Assert.False(firstClick.IsCompleted);
            Assert.Equal("provisional", page.Find(".october-chat-turn").GetAttribute("data-answer-state"));
            Assert.True(fixture.Application.IsBusy);
            first.Release.TrySetResult();
            await continuation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            page.WaitForAssertion(() => Assert.Contains("本次知识查阅已结束。", page.Markup));
            Assert.DoesNotContain("read-exact-controlled", page.Markup, StringComparison.Ordinal);
            Assert.False(fixture.Application.Turns.Single().ViewModel.IsTerminal);
            continuation.Release.TrySetResult();
            await firstClick.WaitAsync(TimeSpan.FromSeconds(10));
            page.WaitForAssertion(() => Assert.True(fixture.Application.Turns.Single().ViewModel.IsTerminal));
            DialogueHistoryResult history = await fixture.ReadHistoryAsync();
            var prior = Assert.Single(history.Turns);
            Assert.Equal("第一轮精确问题？", prior.Question);
            Assert.Equal("第一段即时文本。查阅完成。", prior.AnswerText);
            Assert.NotNull(prior.InferenceDependencies);
            var savedReference = Assert.Single(prior.References);
            Assert.Equal(fixture.Document, savedReference.Document);
            Assert.True(savedReference.SuccessfulChunks > 0);
            Assert.True(savedReference.ContentBytes > 0);
            var footer = Assert.IsType<OctoberChatReferenceFooterState>(fixture.Application.Turns.Single().Footer);
            Assert.False(footer.IsUnavailable);
            Assert.Equal(savedReference, Assert.Single(footer.Rows).Reference);
            string expectedTitle = fixture.Entry.Catalog!.Documents.Single(document => document.Handle == fixture.Document).Title;
            page.WaitForAssertion(() =>
            {
                var link = Assert.Single(page.FindAll(".october-chat-reference-footer a"));
                Assert.Equal(ExactDocumentLinkBuilder.Build(fixture.Document), link.GetAttribute("href"));
                Assert.Equal(expectedTitle, link.TextContent);
            });
            Assert.Equal(DialogueTerminalState.Completed, Assert.Single(fixture.Application.Transcript).TerminalState);
            Assert.Equal(0, fixture.Registry.ActiveRegistrations);

            await page.InvokeAsync(() => page.Find("#october-chat-question").Input("第二轮精确问题？"));
            await page.InvokeAsync(() => page.Find(".composer-action").ClickAsync(new())).WaitAsync(TimeSpan.FromSeconds(10));
            page.WaitForAssertion(() => Assert.Equal(2, fixture.Application.Turns.Count));
            Assert.All(fixture.Application.Turns, turn => Assert.True(turn.ViewModel.IsTerminal));
            Assert.Equal(4, fixture.RequestBodies.Count);
            using var payload = JsonDocument.Parse(fixture.RequestBodies.Last());
            var messages = payload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
            var dialogue = messages.Where(message => message.GetProperty("role").GetString() is "user" or "assistant")
                .Select(message => (Role: message.GetProperty("role").GetString(), Text: MessageText(message))).ToArray();
            string exactOwnerHistory = Encoding.UTF8.GetString(new OctoberDialoguePayloadCodec()
                .EncodeHistoryProjectionUtf8(history.Turns).AsSpan());
            Assert.Equal(new[] { ("user", exactOwnerHistory), ("user", "第二轮精确问题？") }, dialogue);
            Assert.Equal(2, (await fixture.ReadHistoryAsync()).Turns.Length);
            Assert.Empty(page.FindAll("#chat-question"));
        }
        finally
        {
            first.Release.TrySetResult();
            continuation.Release.TrySetResult();
            if (firstClick is not null) _ = await Record.ExceptionAsync(() => firstClick.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ChatPage_IdleOriginatingContextChangeImmediatelyHidesPriorDisplayAndReattachCannotRestoreItAsync()
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.EnqueueComplete(Text("前一身份专属内容。") + Stop + Done);
        using var context = new ChatPageTests();
        var page = fixture.Render(context);
        page.WaitForAssertion(() => Assert.True(fixture.Application.IsReady));
        await page.InvokeAsync(() => page.Find("#october-chat-question").Input("前一身份的问题"));
        await page.InvokeAsync(() => page.Find(".composer-action").ClickAsync(new())).WaitAsync(TimeSpan.FromSeconds(10));
        page.WaitForAssertion(() => Assert.Contains("前一身份专属内容。", page.Find(".october-chat-answer").TextContent));
        Assert.Single(fixture.Application.Transcript);
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);

        await page.InvokeAsync(() => fixture.Attachment.Change(fixture.Selection with
        {
            Subject = new("replacement-owner", "replacement-authentication")
        }));
        page.WaitForAssertion(() =>
        {
            Assert.Empty(fixture.Application.Turns);
            Assert.Empty(fixture.Application.Transcript);
            Assert.False(fixture.Application.IsReady);
            Assert.DoesNotContain("前一身份专属内容。", page.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("前一身份的问题", page.Markup, StringComparison.Ordinal);
        });
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.Empty(fixture.Application.Turns);
        Assert.Empty(fixture.Application.Transcript);
        Assert.False(fixture.Application.IsReady);
        Assert.Null(await fixture.Application.SubmitAsync("不得复用旧身份"));
        Assert.Single(fixture.RequestBodies);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Application_DetachDoesNotCancelOwnedGenerationAndCanonicalSaveAsync()
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        var response = fixture.Enqueue(Text("已开始。"), Stop + Done);
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);
        Task<OctoberChatCoordinationResult?>? pending = null;
        try
        {
            pending = fixture.Application.SubmitAsync("继续完成");
            await response.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            fixture.Application.Detach();
            Assert.False(pending.IsCompleted);
            Assert.Equal(1, fixture.Registry.ActiveRegistrations);
            Assert.Equal(1, fixture.Requests.ActiveRequests);
            response.Release.TrySetResult();
            var result = Assert.IsType<OctoberChatCoordinationResult>(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(DialogueTerminalState.Completed, result.Terminal!.Receipt!.TerminalState);
            Assert.Equal("已开始。", Assert.Single((await fixture.ReadHistoryAsync()).Turns).AnswerText);
            Assert.Equal(0, fixture.Registry.ActiveRegistrations);
            Assert.Equal(0, fixture.Requests.ActiveRequests);
        }
        finally
        {
            response.Release.TrySetResult();
            if (pending is not null) _ = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Application_ExplicitCancelRetainsOwnershipUntilActualPhysicalReadAndScopeCleanupAsync()
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        var response = fixture.Enqueue(Text("可保留部分。"), Stop + Done);
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        Task<OctoberChatCoordinationResult?>? pending = null;
        try
        {
            int disposedBefore = fixture.DisposedScopes;
            pending = fixture.Application.SubmitAsync("等待明确取消");
            await response.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, fixture.SaveCalls);
            fixture.Application.Cancel();
            Assert.True(fixture.Application.IsBusy);
            Assert.False(pending.IsCompleted);
            Assert.Equal(1, fixture.Registry.ActiveRegistrations);
            Assert.Equal(1, fixture.Requests.ActiveRequests);
            Assert.Equal(disposedBefore, fixture.DisposedScopes);
            Assert.False(response.DisposedWhileReading);
            Assert.Null(await fixture.Application.SubmitAsync("禁止重叠"));
            Assert.Single(fixture.RequestBodies);
            response.Release.TrySetResult();
            var result = Assert.IsType<OctoberChatCoordinationResult>(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(result.Terminal!.Receipt!.Reason == DialogueTerminalReason.UserCancelled,
                $"Expected UserCancelled; actual receipt={result.Terminal.Receipt.Reason}; " +
                $"owner save state={fixture.LastSaveRequest?.TerminalState}, reason={fixture.LastSaveRequest?.Reason}, " +
                $"provider finish={fixture.LastSaveRequest?.ProviderFinish}, answer chars={fixture.LastSaveRequest?.AnswerText.Length}.");
            Assert.Equal(DialogueTerminalState.Interrupted, result.Terminal.Receipt.TerminalState);
            Assert.Equal(0, fixture.Registry.ActiveRegistrations);
            Assert.Equal(0, fixture.Requests.ActiveRequests);
            Assert.True(fixture.DisposedScopes > disposedBefore);
            Assert.False(response.DisposedWhileReading);
            Assert.Empty((await fixture.ReadHistoryAsync()).Turns);
        }
        finally
        {
            response.Release.TrySetResult();
            if (pending is not null) _ = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DisclosureStatus.Denied, DialogueTerminalReason.AuthorizationDenied)]
    [InlineData(DisclosureStatus.AuthorityUnavailable, DialogueTerminalReason.AuthorityUnavailable)]
    public async Task ChatPage_SanitizedOwnerClosureRemovesBothPriorQuestionAndAnswerAsync(
        DisclosureStatus status, DialogueTerminalReason reason)
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.RestorePolicyAfterSanitizedSave = true;
        var body = fixture.Enqueue(Text("必须撤回的暂存回答。"), Stop + Done);
        using var context = new ChatPageTests();
        var page = fixture.Render(context);
        page.WaitForAssertion(() => Assert.True(fixture.Application.IsReady));
        await page.InvokeAsync(() => page.Find("#october-chat-question").Input("必须撤回的原问题"));
        var pending = page.InvokeAsync(() => page.Find(".composer-action").ClickAsync(new()));
        try
        {
            await body.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            page.WaitForAssertion(() => Assert.Contains("必须撤回的暂存回答。", page.Find(".october-chat-answer").TextContent));
            fixture.PolicyStatus = status;
            body.Release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
            var canonical = Assert.IsType<DialogueTerminalSaveRequest>(fixture.LastSaveRequest);
            Assert.Equal(reason, canonical.Reason);
            Assert.Null(canonical.InferenceDependencies);
            Assert.Empty(canonical.AnswerText);
            Assert.Empty(canonical.References);
            Assert.Empty(canonical.ConsumedReads);
            var save = Assert.IsType<DialogueTerminalResult>(fixture.LastSaveResult);
            Assert.Contains(save.Status, new[] { DialogueStatus.Succeeded, DialogueStatus.Existing });
            var receipt = Assert.IsType<DialogueTerminalReceipt>(save.Receipt);
            Assert.Equal(reason, receipt.Reason);
            Assert.Equal(DialogueTerminalState.Failed, receipt.TerminalState);
            Assert.Equal(canonical.SaveOperationId, receipt.SaveOperationId);
            Assert.Equal(canonical.OutcomeDigest, receipt.OutcomeDigest);
            Assert.Null(save.Projection);
            Assert.Equal(0, fixture.Registry.ActiveRegistrations);
            Assert.False(fixture.Application.IsBusy);
            Assert.False(fixture.Application.IsSaveUncertain);
            Assert.All(fixture.Application.Turns, turn =>
            {
                Assert.Empty(turn.Question);
                Assert.Empty(turn.ViewModel.AnswerText);
                Assert.Empty(turn.ViewModel.References);
            });
            Assert.DoesNotContain("必须撤回的原问题", page.Find(".chat-surface").TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("必须撤回的暂存回答。", page.Find(".chat-surface").TextContent, StringComparison.Ordinal);
        }
        finally
        {
            body.Release.TrySetResult();
            _ = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Application_OriginatingContextChangesDuringRendererAuthorizationCannotPublishOldMaterialAsync()
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.EnqueueComplete(Text("不应交付给新身份。") + Stop + Done);
        var inRenderer = new AsyncLocal<bool>();
        bool armed = false;
        int switchClaimed = 0;
        int switched = 0;
        var leaks = new ConcurrentQueue<string>();
        fixture.BeforePolicyCheck = async () =>
        {
            if (!armed || !inRenderer.Value || Interlocked.CompareExchange(ref switchClaimed, 1, 0) != 0) return;
            await Task.Yield();
            fixture.Attachment.ChangeSilently(fixture.Selection with { Subject = new("changed-during-check", "new-stamp") });
            Volatile.Write(ref switched, 1);
        };
        fixture.Application.StateChanged += () =>
        {
            if (Volatile.Read(ref switched) == 0) return;
            foreach (var turn in fixture.Application.Turns)
                if (turn.Question.Length != 0 || turn.ViewModel.AnswerText.Length != 0)
                    leaks.Enqueue(turn.Question + turn.ViewModel.AnswerText);
        };
        fixture.Application.Attach(async callback =>
        {
            inRenderer.Value = true;
            try { await callback(); }
            finally { inRenderer.Value = false; }
        });
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        armed = true;
        await fixture.Application.SubmitAsync("只属于原身份的问题").WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, Volatile.Read(ref switched));
        Assert.Empty(leaks);
        Assert.Empty(fixture.Application.Turns);
        Assert.Empty(fixture.Application.Transcript);
        Assert.False(fixture.Application.IsReady);
        Assert.Single(fixture.RequestBodies);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Application_IndeterminateBeginExposesExactLookupWithoutDispatchOrInventedClosureAsync()
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.LoseBeginAcknowledgement = true;
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        var unknown = Assert.IsType<OctoberChatCoordinationResult>(await fixture.Application.SubmitAsync("保留未知准入")
            .WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(DialogueStatus.Indeterminate, unknown.Begin.Status);
        Assert.False(unknown.Begin.MayDispatch);
        Assert.Null(unknown.Begin.Admission);
        Assert.True(fixture.Application.IsSaveUncertain);
        Assert.Equal(1, fixture.Registry.ActiveRegistrations);
        Assert.Null(await fixture.Application.SubmitAsync("不能重试准入"));
        var recovered = Assert.IsType<OctoberChatCoordinationResult>(await fixture.Application.ReconcileAsync()
            .WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(DialogueStatus.Existing, recovered.Begin.Status);
        Assert.False(recovered.Begin.MayDispatch);
        Assert.Equal(DialogueTurnState.Admitted, recovered.State);
        Assert.Equal(JsonSerializer.Serialize(fixture.ActualBeginAdmission), JsonSerializer.Serialize(recovered.Begin.Admission));
        var request = Assert.IsType<DialogueBeginRequest>(fixture.LastBeginRequest);
        var lookup = Assert.Single(fixture.AdmissionLookups);
        Assert.Equal(new DialogueAdmissionLookup(request.ConversationId, request.ContextId,
            request.ClientRequestKey, request.RequestDigest), lookup);
        Assert.Null(recovered.Terminal);
        Assert.Null(recovered.PendingSave);
        Assert.True(fixture.Application.IsSaveUncertain);
        Assert.False(fixture.Application.IsReady);
        Assert.Equal(1, fixture.Registry.ActiveRegistrations);
        Assert.Equal(1, fixture.BeginCalls);
        Assert.Equal(0, fixture.SaveCalls);
        Assert.Empty(fixture.RequestBodies);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Application_LostSaveAcknowledgementBlocksRetryAndReconcilesByExactLookupOnlyAsync()
    {
        await using var fixture = await ApplicationFixture.CreateAsync();
        fixture.LoseSaveAcknowledgement = true;
        fixture.EnqueueComplete(Text("实际已保存。") + Stop + Done);
        fixture.Application.Attach(callback => callback().AsTask());
        Assert.True(await fixture.Application.OpenAsync(fixture.Conversation));
        var result = Assert.IsType<OctoberChatCoordinationResult>(await fixture.Application.SubmitAsync("仅核对保存")
            .WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(DialogueStatus.Indeterminate, result.Terminal!.Status);
        Assert.NotNull(result.PendingSave);
        Assert.True(fixture.Application.IsSaveUncertain);
        Assert.True(fixture.Application.Turns.Single().ViewModel.IsSaveUncertain);
        Assert.False(fixture.Application.Turns.Single().ViewModel.IsTerminal);
        Assert.Equal(1, fixture.Registry.ActiveRegistrations);
        Assert.Equal(1, fixture.SaveCalls);
        Assert.Null(await fixture.Application.SubmitAsync("不能重新生成"));
        Assert.Single(fixture.RequestBodies);
        Assert.Equal(1, fixture.SaveCalls);

        var reconciled = Assert.IsType<OctoberChatCoordinationResult>(await fixture.Application.ReconcileAsync()
            .WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(DialogueStatus.Existing, reconciled.Terminal!.Status);
        Assert.Equal(fixture.ActualSavedReceipt, reconciled.Terminal.Receipt);
        Assert.Equal(result.PendingSave.SaveOperationId, reconciled.Terminal.Receipt!.SaveOperationId);
        Assert.Equal(result.PendingSave.OutcomeDigest, reconciled.Terminal.Receipt.OutcomeDigest);
        Assert.Equal(1, fixture.SaveCalls);
        Assert.Equal(1, fixture.LookupCalls);
        Assert.Single(fixture.RequestBodies);
        Assert.False(fixture.Application.IsSaveUncertain);
        Assert.True(fixture.Application.Turns.Single().ViewModel.IsTerminal);
        Assert.Equal(0, fixture.Registry.ActiveRegistrations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ChatPage_ExplicitOctoberApplicationWithoutEntryCannotFallBackToLegacyComposer()
    {
        var registry = new OctoberChatExecutionRegistry(new OctoberDialoguePayloadCodec(),
            (_, _) => Task.FromResult(CommitEligibilityStatus.AuthorityUnavailable));
        int scopes = 0;
        var application = new OctoberChatApplication(registry, (_, _) =>
        {
            scopes++;
            return Task.FromResult<OctoberChatTurnScope?>(null);
        }, (_, _, _) => Task.FromResult<OctoberChatInputDigests?>(null));
        // This context supplies existing legacy injection requirements only; it is never an October execution fixture.
        using var context = new ChatPageTests();
        var page = context.Render<ChatPage>(parameters => parameters.Add(item => item.OctoberApplication, application));

        Assert.Empty(page.FindAll("#chat-question"));
        Assert.Empty(page.FindAll(".repository-picker__trigger"));
        Assert.DoesNotContain("mbs_account", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mbs_channel", page.Markup, StringComparison.Ordinal);
        Assert.False(application.IsReady);
        Assert.Equal(0, scopes);
        Assert.All(page.FindAll("textarea"), field => Assert.True(field.HasAttribute("disabled")));
        Assert.All(page.FindAll("fluent-button").Where(button => button.TextContent.Trim() == "发送"),
            button => Assert.True(button.HasAttribute("disabled")));
    }

    internal static string MessageText(JsonElement message)
    {
        var content = message.GetProperty("content");
        return content.ValueKind == JsonValueKind.String ? content.GetString()! :
            string.Concat(content.EnumerateArray().Where(part => part.GetProperty("type").GetString() == "text")
                .Select(part => part.GetProperty("text").GetString()));
    }
    internal const string Done = "data: [DONE]\n\n";
    internal static readonly string Stop = Chunk(new { }, "stop");
    internal static string Text(string text) => Chunk(new { role = "assistant", content = text });
    private static string Tool(DocumentHandle document) => Chunk(new
    {
        tool_calls = new[] { new { index = 0, id = "read-exact-controlled", type = "function",
            function = new { name = "ReadWiki", arguments = JsonSerializer.Serialize(new { document }) } } }
    }) + Chunk(new { }, "tool_calls");
    private static string Report(DocumentHandle document) => Chunk(new
    {
        tool_calls = new[] { new { index = 0, id = "report-exact-controlled", type = "function",
            function = new { name = "ReportAnswerDocuments", arguments = JsonSerializer.Serialize(new { documents = new[] { document } }) } } }
    }) + Chunk(new { }, "tool_calls");
    private static string Chunk(object delta, string? finish = null) => "data: " + JsonSerializer.Serialize(new
    {
        id = "controlled-response", @object = "chat.completion.chunk", created = 1, model = "controlled-model",
        choices = new[] { new { index = 0, delta, finish_reason = finish } }
    }) + "\n\n";

    /// <summary>Real owner persistence/lineage and registry; only current identity, configuration and physical HTTP are controlled.</summary>
    internal sealed class ApplicationFixture : IAsyncDisposable
    {
        private readonly DialoguePersistenceFixture _seed;
        private readonly ConcurrentQueue<GatedBody> _responses = new();
        private readonly List<OctoberDialogueStore> _stores = [];
        private readonly OctoberDialoguePayloadCodec _codec = new();
        private readonly LlmProvider _provider = new("controlled-model", "https://controlled.invalid/v1", "controlled-protected-key",
            ApiType.ChatCompletions, AgentType.Chat, DateTimeOffset.UtcNow);
        private readonly ControlledPolicy _policy = new();
        private readonly OctoberKnowledgeDependencyAuthority _dependencies;
        private readonly OctoberKnowledgeReader _reader;
        private int _disposedScopes;

        private ApplicationFixture(DialoguePersistenceFixture seed)
        {
            _seed = seed;
            Selection = new(seed.Identity.Publication, seed.Subject, KnowledgeMode.IT, KnowledgePurpose.Chat, seed.Identity.RepositoryScope);
            Attachment = new(Selection);
            var protection = new EphemeralDataProtectionProvider();
            _dependencies = new(_policy, new OctoberKnowledgeDisclosureDataProtectionProtector(protection));
            _reader = new(new OctoberKnowledgePublishedSource(seed.Factory), seed.Identity.Publication, _policy,
                new(new OctoberKnowledgeDataProtectionTokenProtector(protection)), new ReadLog(), _dependencies);
            Registry = new(_codec, CheckFreshAsync);
            Application = CreateApplication(Attachment);
            Entry = new(new OctoberChatSelectionService(_reader, _policy));
        }
        private OctoberChatApplication CreateApplication(Context attachment)
        {
            var application = new OctoberChatApplication(Registry, CreateScopeAsync, async (mode, question, token) =>
            {
                var digest = await Vulgata.Agents.ChatExecution.October.OctoberChatInputDigests.CreateAsync(mode, question, token);
                return digest is null ? null : new(digest.PromptDigest, digest.InitialInputDigest);
            }, attachmentContext: attachment);
            _applications.Add(application);
            return application;
        }
        private readonly List<OctoberChatApplication> _applications = [];
        public ConcurrentQueue<DialogueConversationOpenRequest> OpenRequests { get; } = new();
        public Func<DialogueConversationOpenRequest, DialogueConversationResult, Task>? AfterOpen { get; set; }
        public OctoberChatApplication Application { get; }
        public OctoberChatExecutionRegistry Registry { get; }
        public OctoberChatRequestAdmission Requests { get; } = new(1);
        public ConcurrentQueue<string> RequestBodies { get; } = new();
        public OctoberChatEntryViewModel Entry { get; }
        public ReadSelectionRequest Selection { get; }
        public Context Attachment { get; }
        public OctoberChatConversation Conversation => Entry.Conversation!;
        public DocumentHandle Document => new(_seed.Document.BatchId, Selection.Publication.PublicationId,
            _seed.Document.LogicalDocumentId, _seed.Document.DocumentVersionId);
        public int DisposedScopes => Volatile.Read(ref _disposedScopes);
        public bool LoseSaveAcknowledgement { get; set; }
        public int SaveCalls { get; set; }
        public int LookupCalls { get; set; }
        public DialogueTerminalReceipt? ActualSavedReceipt { get; set; }
        public DialogueTerminalSaveRequest? LastSaveRequest { get; set; }
        public DialogueTerminalResult? LastSaveResult { get; set; }
        public bool RestorePolicyAfterSanitizedSave { get; set; }
        public bool LoseBeginAcknowledgement { get; set; }
        public int BeginCalls { get; set; }
        public DialogueBeginRequest? LastBeginRequest { get; set; }
        public DialogueAdmission? ActualBeginAdmission { get; set; }
        public ConcurrentQueue<DialogueAdmissionLookup> AdmissionLookups { get; } = new();
        public DisclosureStatus PolicyStatus { get => _policy.Status; set => _policy.Status = value; }
        public Func<Task>? BeforePolicyCheck { get => _policy.BeforeCheck; set => _policy.BeforeCheck = value; }
        public async Task<(OctoberChatApplication Application, OctoberChatEntryViewModel Entry, ReadSelectionRequest Selection)>
            CreateReattachmentAsync(ReadSelectionRequest? selection = null)
        {
            selection ??= Selection;
            var entry = new OctoberChatEntryViewModel(new OctoberChatSelectionService(_reader, _policy));
            await entry.InitializeAsync(selection);
            return (CreateApplication(new Context(selection)), entry, selection);
        }
        public static async Task<ApplicationFixture> CreateAsync(DbContextOptions<OctoberDbContext>? options = null)
        {
            var fixture = new ApplicationFixture(await DialoguePersistenceFixture.CreateAsync(options));
            await fixture.Entry.InitializeAsync(fixture.Selection);
            Assert.NotNull(fixture.Conversation);
            return fixture;
        }
        public IRenderedComponent<ChatPage> Render(ChatPageTests context) => context.Render<ChatPage>(parameters => parameters
            .Add(page => page.OctoberEntry, Entry).Add(page => page.OctoberSelection, Selection)
            .Add(page => page.OctoberApplication, Application));
        public GatedBody Enqueue(string prefix, string suffix)
        {
            var body = new GatedBody(prefix, suffix);
            _responses.Enqueue(body);
            return body;
        }
        public void EnqueueComplete(string content) => Enqueue(content, "").Release.TrySetResult();
        private OctoberDialogueStore NewStore()
        {
            var store = new OctoberDialogueStore(_seed.Factory, Selection.Publication.Knowledge, _policy, _dependencies,
                Registry, Registry.CommitGuard, _codec, TimeProvider.System);
            _stores.Add(store);
            return store;
        }
        private Task<OctoberChatTurnScope?> CreateScopeAsync(ReadSelectionRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var store = new AcknowledgementObserver(NewStore(), this);
            var scope = new OctoberChatTurnScope(new Context(request), store, _codec, _reader, _reader, _dependencies,
                Registry, new OctoberChatProviderBindingService(_provider.Id, new Repository(_provider), new Secret()), Requests,
                async () =>
                {
                    await store.DisposeAsync();
                    Interlocked.Increment(ref _disposedScopes);
                }, disclosurePolicy: _policy) { PhysicalTransport = new PhysicalHandler(_responses, RequestBodies) };
            return Task.FromResult<OctoberChatTurnScope?>(scope);
        }
        private async Task<CommitEligibilityStatus> CheckFreshAsync(DialogueCommitAttempt attempt, CancellationToken token)
        {
            if (attempt.Subject != Selection.Subject || attempt.Conversation.Publication != Selection.Publication ||
                attempt.Conversation.Mode != Selection.Mode ||
                !attempt.Conversation.RepositoryScope.SequenceEqual(Selection.RepositoryScope)) return CommitEligibilityStatus.AuthorityUnavailable;
            var disclosure = await _policy.CheckAsync(new(Selection, [], null), token);
            if (disclosure != DisclosureStatus.Allowed) return disclosure == DisclosureStatus.Denied
                ? CommitEligibilityStatus.Denied : CommitEligibilityStatus.AuthorityUnavailable;
            if (attempt.Dependencies is { } dependencies)
            {
                var status = await _dependencies.ReauthorizeAsync(Selection, [dependencies], token);
                if (status != KnowledgeDependencyStatus.Succeeded) return status == KnowledgeDependencyStatus.Denied
                    ? CommitEligibilityStatus.Denied : CommitEligibilityStatus.AuthorityUnavailable;
            }
            return CommitEligibilityStatus.Eligible;
        }
        public async Task<DialogueHistoryResult> ReadHistoryAsync()
        {
            var store = NewStore();
            try { return await store.ReadHistoryAsync(new(Conversation.ConversationId, Conversation.ContextId, null), Selection.Subject); }
            finally { await Assert.IsAssignableFrom<IAsyncDisposable>(store).DisposeAsync(); }
        }
        public async Task<DialogueTerminalResult> ReadReceiptAsync(DialogueTerminalReceipt receipt)
        {
            var store = NewStore();
            try
            {
                return await store.LookupTerminalAsync(new(receipt.ConversationId, receipt.ContextId, receipt.TurnId,
                    receipt.SaveOperationId, receipt.Fence, receipt.OutcomeDigest), Selection.Subject);
            }
            finally { await Assert.IsAssignableFrom<IAsyncDisposable>(store).DisposeAsync(); }
        }
        public async ValueTask DisposeAsync()
        {
            foreach (var response in _responses) response.Release.TrySetResult();
            foreach (var application in _applications) { application.Cancel(); application.Detach(); }
            foreach (var store in _stores) if ((object)store is IAsyncDisposable disposable) await disposable.DisposeAsync();
            await _seed.DisposeAsync();
        }
    }
    /// <summary>Transparent owner port observer: loses one acknowledgement only after the real canonical save returned its receipt.</summary>
    private sealed class AcknowledgementObserver(OctoberDialogueStore owner, ApplicationFixture fixture) : IOctoberDialogueStore, IAsyncDisposable
    {
        public async Task<DialogueConversationResult> OpenConversationAsync(DialogueConversationOpenRequest request, KnowledgeSubject subject, CancellationToken token = default)
        {
            fixture.OpenRequests.Enqueue(request);
            var result = await owner.OpenConversationAsync(request, subject, token);
            if (fixture.AfterOpen is { } after) await after(request, result);
            return result;
        }
        public async Task<DialogueBeginResult> BeginTurnAsync(DialogueBeginRequest request, KnowledgeSubject subject, CancellationToken token = default)
        {
            fixture.BeginCalls++;
            fixture.LastBeginRequest = request;
            var result = await owner.BeginTurnAsync(request, subject, token);
            if (fixture.LoseBeginAcknowledgement && result.Admission is not null)
            {
                fixture.ActualBeginAdmission = result.Admission;
                fixture.LoseBeginAcknowledgement = false;
                return new(DialogueStatus.Indeterminate, null, false);
            }
            return result;
        }
        public Task<DialogueBeginResult> LookupAdmissionAsync(DialogueAdmissionLookup request, KnowledgeSubject subject, CancellationToken token = default)
        {
            fixture.AdmissionLookups.Enqueue(request);
            return owner.LookupAdmissionAsync(request, subject, token);
        }
        public Task<DialogueTransitionResult> TransitionTurnAsync(DialogueTransitionRequest request, KnowledgeSubject subject, CancellationToken token = default) => owner.TransitionTurnAsync(request, subject, token);
        public async Task<DialogueTerminalResult> SaveTerminalAsync(DialogueTerminalSaveRequest request, KnowledgeSubject subject, CancellationToken token = default)
        {
            fixture.SaveCalls++;
            fixture.LastSaveRequest = request;
            var result = await owner.SaveTerminalAsync(request, subject, token);
            fixture.LastSaveResult = result;
            if (fixture.RestorePolicyAfterSanitizedSave && request.InferenceDependencies is null && result.Receipt is not null)
                fixture.PolicyStatus = DisclosureStatus.Allowed;
            if (fixture.LoseSaveAcknowledgement && result.Receipt is not null)
            {
                fixture.ActualSavedReceipt = result.Receipt;
                fixture.LoseSaveAcknowledgement = false;
                return new(DialogueStatus.Indeterminate, null, null);
            }
            return result;
        }
        public Task<DialogueTerminalResult> LookupTerminalAsync(DialogueTerminalLookup request, KnowledgeSubject subject, CancellationToken token = default)
        {
            fixture.LookupCalls++;
            return owner.LookupTerminalAsync(request, subject, token);
        }
        public Task<DialogueHistoryResult> ReadHistoryAsync(DialogueHistoryRequest request, KnowledgeSubject subject, CancellationToken token = default) => owner.ReadHistoryAsync(request, subject, token);
        public Task<DialogueTranscriptResult> ReadTranscriptAsync(DialogueHistoryRequest request, KnowledgeSubject subject, CancellationToken token = default) => owner.ReadTranscriptAsync(request, subject, token);
        public Task<DialogueOrphanResult> InterruptOrphanAsync(DialogueOrphanRequest request, CancellationToken token = default) => owner.InterruptOrphanAsync(request, token);
        public ValueTask DisposeAsync() => Assert.IsAssignableFrom<IAsyncDisposable>(owner).DisposeAsync();
    }
    internal sealed class Context(ReadSelectionRequest selection) : IOctoberChatContextProvider
    {
        private ReadSelectionRequest _current = selection;
        public event Action? ContextChanged;
        public void Change(ReadSelectionRequest current)
        {
            _current = current;
            ContextChanged?.Invoke();
        }
        public void ChangeSilently(ReadSelectionRequest current) => _current = current;
        public Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ReadSelectionRequest?>(_current);
        }
    }
    private sealed class ControlledPolicy : IOctoberKnowledgeDisclosurePolicy
    {
        public DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
        public Func<Task>? BeforeCheck { get; set; }
        public async Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BeforeCheck is { } before) await before();
            cancellationToken.ThrowIfCancellationRequested();
            return Status;
        }
    }
    private sealed class RecordedNavigation : NavigationManager
    {
        public RecordedNavigation(string query) => Initialize("http://localhost/", "http://localhost/" + query);
        public List<(string Uri, NavigationOptions Options)> Navigations { get; } = [];
        protected override void NavigateToCore(string uri, bool forceLoad) => NavigateToCore(uri, new NavigationOptions { ForceLoad = forceLoad });
        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
            Navigations.Add((Uri, options));
            NotifyLocationChanged(false);
        }
    }
    private sealed class ReadLog : IOctoberKnowledgeReadLog
    {
        public Task<bool> AppendAsync(KnowledgeReadLogEntry entry, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
    private sealed class Repository(LlmProvider row) : ILlmProviderRepository
    {
        public Task<LlmProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<LlmProvider?>(id == row.Id ? row : null);
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> NameExistsAsync(string name, Guid? excludeProviderId = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<LlmProvider> AddAsync(LlmProvider provider, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Remove(LlmProvider provider) => throw new InvalidOperationException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
    private sealed class Secret : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(string purpose, string plaintext, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<string> DecryptAsync(string purpose, string ciphertext, CancellationToken cancellationToken = default) => Task.FromResult("controlled-dummy-key");
    }
    private sealed class PhysicalHandler(ConcurrentQueue<GatedBody> responses, ConcurrentQueue<string> requests) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Enqueue(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.True(responses.TryDequeue(out var body), "No controlled response remains; an unexpected request occurred.");
            return new(HttpStatusCode.OK) { Content = new StreamContent(body!) { Headers = { ContentType = new MediaTypeHeaderValue("text/event-stream") } } };
        }
    }
    internal sealed class GatedBody(string prefix, string suffix) : Stream
    {
        private readonly MemoryStream _prefix = new(Encoding.UTF8.GetBytes(prefix));
        private readonly MemoryStream _suffix = new(Encoding.UTF8.GetBytes(suffix));
        private int _reading;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool DisposedWhileReading { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _reading);
            try
            {
                int read = _prefix.Read(buffer.Span);
                if (read != 0) return read;
                Entered.TrySetResult();
                await Release.Task; // Deliberately noncooperative physical I/O; cleanup must retain it.
                return _suffix.Read(buffer.Span);
            }
            finally { Interlocked.Decrement(ref _reading); }
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        protected override void Dispose(bool disposing)
        {
            if (Volatile.Read(ref _reading) != 0) DisposedWhileReading = true;
            base.Dispose(disposing);
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

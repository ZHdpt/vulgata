using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Shared.OctoberChat;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Services.OctoberChat;

namespace Vulgata.Tests.October.Chat;

/// <summary>
/// Real SQLite dialogue store/codec and integration-owned client session over controlled HTTP.
/// Business fixture dependency/execution/commit authorities remain explicit controlled doubles;
/// this is not PostgreSQL, real host registration, provider or browser qualification.
/// </summary>
public sealed class OctoberChatTurnTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_PresentationDeliversEarlyTextAndBlockedSubscriberCannotDelayOwnedSaveAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.Body.HoldAfterText = true;
        var textDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubscriber = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivered = new List<OctoberChatEvent>();
        OctoberChatTurnPresentation? attachment = null;
        h.ObserveGeneration = task =>
        {
            Assert.False(task.IsCompleted);
            Assert.Equal(DialogueTurnState.Running, h.Coordinator.State);
            DialogueAdmission original = h.Coordinator.Begin!.Admission!;
            var selection = new ReadSelectionRequest(h.Owner.Identity.Publication, h.Owner.Subject,
                h.Owner.Identity.Mode, KnowledgePurpose.Chat, h.Owner.Identity.RepositoryScope);
            var binding = new OctoberChatTurnBinding(h.Request.ConversationId, h.Request.ContextId, original.TurnId, selection, h.Selection);
            attachment = new(binding, original, h.SaveId, h.Context, h.Owner.Dependencies, async (item, token) =>
            {
                token.ThrowIfCancellationRequested();
                if (attachment?.IsDetached == true) return;
                delivered.Add(item);
                if (item.Kind == OctoberChatEventKind.AnswerDelta)
                {
                    textDelivered.TrySetResult();
                    await releaseSubscriber.Task; // Actual subscriber lifetime deliberately ignores cancellation.
                }
            }, h.Clock);
        };
        Task<OctoberChatCoordinationResult> run = h.Coordinator.ExecuteAsync(h.Request, h.Budget, h.Selection,
            h.History, h.Snapshot, h.SaveId,
            (text, dependencies, _) =>
            {
                Assert.NotNull(attachment);
                Assert.True(attachment.TryPublishText(text, dependencies));
                return ValueTask.CompletedTask;
            }, (progress, dependencies) => attachment!.TryPublishProgress(progress, dependencies));
        try
        {
            Assert.Same(textDelivered.Task, await Task.WhenAny(textDelivered.Task, run).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("受控即时回答😀。", Assert.Single(delivered.Where(item => item.Kind == OctoberChatEventKind.AnswerDelta)).Text);
            Assert.False(run.IsCompleted);
            Assert.Empty(h.Store.Saves);
            h.Body.ReleaseText.TrySetResult();
            OctoberChatCoordinationResult saved = await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(DialogueTurnState.Completed, saved.State);
            Assert.Single(h.Store.Saves);
            Assert.Equal(0, h.Requests.ActiveRequests);
            Assert.True(attachment!.TryPublishResult(saved));
            Assert.False(attachment.Completion.IsCompleted);
            attachment.Detach();
            Assert.False(attachment.Completion.IsCompleted);
            Assert.DoesNotContain(delivered, item => item.Kind == OctoberChatEventKind.TurnSaved);
        }
        finally
        {
            h.Body.ReleaseText.TrySetResult();
            releaseSubscriber.TrySetResult();
            if (attachment is not null) await attachment.DisposeAsync();
            await run;
        }
        Assert.Single(h.GenerationTasks);
        Assert.Equal(1, h.Physical.Sends);
        Assert.Null((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_AdmittedSnapshotStreamsThenDrainsBeforeConfirmedAtomicSaveAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.Body.HoldAfterText = true;
        h.ObserveGeneration = task =>
        {
            Assert.False(task.IsCompleted);
            Assert.Equal(0, h.Physical.Sends);
        };
        var saveRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Store.Save = async (request, subject, token) =>
        {
            Assert.Equal(0, h.Requests.ActiveRequests);
            Assert.Equal(1, h.Physical.Disposals);
            Assert.False(token.IsCancellationRequested);
            await saveRelease.Task.WaitAsync(token);
            return await h.Owner.Store.SaveTerminalAsync(request, subject, token);
        };
        Task<OctoberChatCoordinationResult> run = h.ExecuteAsync();
        try
        {
            Assert.Same(h.TextObserved.Task, await Task.WhenAny(h.TextObserved.Task, run).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("受控即时回答😀。", h.Visible.ToString());
            Assert.False(run.IsCompleted);
            Assert.Equal(DialogueTurnState.Running, h.Coordinator.State);
            Assert.Equal(DialogueTurnState.Admitted, Assert.Single(h.FactoryAdmissions).Admission!.State);
            Task<OctoberChatGenerationResult> generation = Assert.Single(h.GenerationTasks);
            Assert.False(generation.IsCompleted);
            Assert.Empty(h.Store.Saves);
            DialogueConversationResult active = await h.Owner.OpenAsync();
            Assert.Equal(h.Coordinator.Begin!.Admission!.TurnId, active.Conversation!.ActiveTurnId);
            h.Body.ReleaseText.TrySetResult();
            Assert.Same(h.Store.SaveEntered.Task, await Task.WhenAny(h.Store.SaveEntered.Task, run).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(run.IsCompleted);
            Assert.Equal(DialogueTurnState.Saving, h.Coordinator.State);
            Assert.Equal(DialogueTerminalState.Completed, (await generation).TerminalState);
        }
        finally
        {
            h.Body.ReleaseText.TrySetResult();
            saveRelease.TrySetResult();
            await run;
        }

        OctoberChatCoordinationResult result = await run;
        Assert.Equal(DialogueTurnState.Completed, result.State);
        Assert.Equal(DialogueStatus.Succeeded, result.Terminal!.Status);
        Assert.Equal(h.SaveId, result.Terminal.Receipt!.SaveOperationId);
        DialogueTerminalSaveRequest saved = Assert.Single(h.Store.Saves);
        Assert.Equal(h.Visible.ToString(), saved.AnswerText);
        Assert.Equal(h.Codec.ComputeOutcomeDigest(saved), saved.OutcomeDigest);
        Assert.Equal(new[] { DialogueTurnState.Running, DialogueTurnState.Saving }, h.Store.Transitions.Select(item => item.NextState));
        Assert.All(h.Store.Transitions, item => Assert.Equal(saved.Owner, item.Owner));
        Assert.Null((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
        DialogueHistoryResult history = await h.Owner.Store.ReadHistoryAsync(h.Owner.HistoryRequest, h.Owner.Subject);
        Assert.Equal(h.Visible.ToString(), Assert.Single(history.Turns).AnswerText);
        Assert.Equal(1, h.Physical.Sends);
        using JsonDocument sent = JsonDocument.Parse(h.Physical.RequestBody!);
        JsonElement[] systems = sent.RootElement.GetProperty("messages").EnumerateArray().Where(item => item.GetProperty("role").GetString() == "system").ToArray();
        Assert.Equal(2, systems.Length);
        JsonElement system = systems[0];
        using var budgetInfo = JsonDocument.Parse(systems[1].GetProperty("content").GetString()!);
        Assert.Equal("october_chat_budget", budgetInfo.RootElement.GetProperty("type").GetString());
        Assert.Equal(await new OctoberChatPromptCatalog().LoadAsync(KnowledgeMode.IT), system.GetProperty("content").GetString());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_RealStoreReplayOrChangedSameKeyNeverRedispatchesAsync(bool conflict)
    {
        await using Harness h = await Harness.OpenAsync();
        Assert.Equal(DialogueTurnState.Completed, (await h.ExecuteAsync()).State);
        DialogueBeginRequest request = h.Request;
        if (conflict)
        {
            request = request with { Question = "changed logical question" };
            request = request with { RequestDigest = h.Codec.ComputeBeginDigest(h.Owner.Identity, request) };
        }
        OctoberChatCoordinator replay = h.NewCoordinator();

        OctoberChatCoordinationResult result = await h.ExecuteAsync(replay, request, Guid.NewGuid());

        Assert.Equal(conflict ? DialogueStatus.Conflict : DialogueStatus.Existing, result.Begin.Status);
        Assert.False(result.Begin.MayDispatch);
        Assert.Equal(1, h.FactoryAdmissions.Count);
        Assert.Equal(1, h.Physical.Sends);
        Assert.Single(h.Store.Saves);
        Assert.Equal(2, h.Store.Begins.Count);
        Assert.Equal(1, (await h.Owner.Store.ReadHistoryAsync(h.Owner.HistoryRequest, h.Owner.Subject)).Turns.Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_RealActiveConversationReturnsBusyWithoutCreatingClientAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        DialogueBeginResult existing = await h.Owner.Store.BeginTurnAsync(h.Request, h.Owner.Subject);
        Assert.True(existing.MayDispatch);
        DialogueBeginRequest next = h.Request with { ClientRequestKey = Guid.NewGuid() };
        next = next with { RequestDigest = h.Codec.ComputeBeginDigest(h.Owner.Identity, next) };

        OctoberChatCoordinationResult result = await h.ExecuteAsync(request: next);

        Assert.Equal(DialogueStatus.Busy, result.Begin.Status);
        Assert.False(result.Begin.MayDispatch);
        Assert.Empty(h.FactoryAdmissions);
        Assert.Empty(h.Store.Saves);
        Assert.Equal(0, h.Physical.Sends);
        Assert.Equal(existing.Admission!.TurnId, (await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAndReconcile_UnknownAdmissionOnlyLooksUpAndNeverDispatchesAsync(bool actuallyCommitted)
    {
        await using Harness h = await Harness.OpenAsync();
        h.Store.Admit = async (request, subject, token) =>
        {
            if (actuallyCommitted) Assert.Equal(DialogueStatus.Succeeded, (await h.Owner.Store.BeginTurnAsync(request, subject, token)).Status);
            return new(DialogueStatus.Indeterminate, null, false);
        };

        OctoberChatCoordinationResult first = await h.ExecuteAsync();
        Assert.False(first.Begin.MayDispatch);
        OctoberChatCoordinationResult reconciled = await h.Coordinator.ReconcileAsync();

        Assert.False(reconciled.Begin.MayDispatch);
        Assert.Equal(actuallyCommitted ? DialogueStatus.Existing : DialogueStatus.Indeterminate, reconciled.Begin.Status);
        Assert.Single(h.Store.Begins);
        Assert.NotEmpty(h.Store.AdmissionLookups);
        Assert.All(h.Store.AdmissionLookups, lookup =>
        {
            Assert.Equal(h.Request.ClientRequestKey, lookup.ClientRequestKey);
            Assert.Equal(h.Request.RequestDigest, lookup.RequestDigest);
        });
        Assert.Empty(h.FactoryAdmissions);
        Assert.Empty(h.Store.Saves);
        Assert.Equal(0, h.Physical.Sends);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReconcileAsync_LostSaveAcknowledgementResolvesExactReceiptWithoutRegenerationOrResubmissionAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.Store.Save = async (request, subject, token) =>
        {
            Assert.Equal(DialogueStatus.Succeeded, (await h.Owner.Store.SaveTerminalAsync(request, subject, token)).Status);
            return new(DialogueStatus.Indeterminate, null, null);
        };
        h.Store.Lookup = (_, _, _) => Task.FromResult(new DialogueTerminalResult(DialogueStatus.Indeterminate, null, null));
        OctoberChatCoordinationResult first = await h.ExecuteAsync();
        Assert.Equal(DialogueTurnState.SaveIndeterminate, first.State);
        DialogueTerminalSaveRequest retained = Assert.IsType<DialogueTerminalSaveRequest>(h.Coordinator.PendingSave);
        h.Store.Lookup = null;

        OctoberChatCoordinationResult result = await h.Coordinator.ReconcileAsync();

        Assert.Equal(DialogueTurnState.Completed, result.State);
        Assert.Equal(retained.SaveOperationId, result.Terminal!.Receipt!.SaveOperationId);
        Assert.Equal(retained.OutcomeDigest, result.Terminal.Receipt.OutcomeDigest);
        Assert.Equal(retained.Owner.Fence, result.Terminal.Receipt.Fence);
        Assert.Single(h.Store.Saves);
        Assert.Single(h.FactoryAdmissions);
        Assert.Equal(1, h.Physical.Sends);
        Assert.All(h.Store.TerminalLookups, lookup =>
        {
            Assert.Equal(retained.SaveOperationId, lookup.SaveOperationId);
            Assert.Equal(retained.OutcomeDigest, lookup.OutcomeDigest);
            Assert.Equal(retained.Owner.Fence, lookup.ExpectedFence);
        });
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReconcileAsync_MissingTerminalRowRemainsUncertainAndNeverRetriesSaveOrGenerationAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.Store.Save = (_, _, _) => Task.FromResult(new DialogueTerminalResult(DialogueStatus.Indeterminate, null, null));
        OctoberChatCoordinationResult first = await h.ExecuteAsync();
        Assert.Equal(DialogueTurnState.SaveIndeterminate, first.State);
        DialogueTerminalSaveRequest retained = Assert.IsType<DialogueTerminalSaveRequest>(h.Coordinator.PendingSave);

        OctoberChatCoordinationResult second = await h.Coordinator.ReconcileAsync();

        Assert.Equal(DialogueTurnState.SaveIndeterminate, second.State);
        Assert.Equal(retained, h.Coordinator.PendingSave);
        Assert.Single(h.Store.Saves);
        Assert.Single(h.FactoryAdmissions);
        Assert.Equal(1, h.Physical.Sends);
        Assert.NotEmpty(h.Store.TerminalLookups);
        DialogueBeginRequest next = h.Request with { ClientRequestKey = Guid.NewGuid() };
        next = next with { RequestDigest = h.Codec.ComputeBeginDigest(h.Owner.Identity, next) };
        Assert.Equal(DialogueStatus.Busy, (await h.Owner.Store.BeginTurnAsync(next, h.Owner.Subject)).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("fence")]
    [InlineData("turn")]
    [InlineData("digest")]
    public async Task ExecuteAndReconcile_MismatchedReceiptNeverConfirmsThisTurnAsync(string field)
    {
        await using Harness h = await Harness.OpenAsync();
        DialogueTerminalResult Corrupt(DialogueTerminalResult result)
        {
            DialogueTerminalReceipt receipt = result.Receipt!;
            return result with { Receipt = field switch
            {
                "fence" => receipt with { Fence = receipt.Fence + 1 },
                "turn" => receipt with { TurnId = Guid.NewGuid() },
                _ => receipt with { OutcomeDigest = new string('0', 64) },
            } };
        }
        h.Store.Save = async (request, subject, token) => Corrupt(await h.Owner.Store.SaveTerminalAsync(request, subject, token));
        h.Store.Lookup = async (request, subject, token) => Corrupt(await h.Owner.Store.LookupTerminalAsync(request, subject, token));

        OctoberChatCoordinationResult first = await h.ExecuteAsync();
        OctoberChatCoordinationResult reconciled = await h.Coordinator.ReconcileAsync();

        Assert.Equal(DialogueTurnState.SaveIndeterminate, first.State);
        Assert.Equal(DialogueTurnState.SaveIndeterminate, reconciled.State);
        Assert.True(first.Terminal?.Receipt is null);
        Assert.True(reconciled.Terminal?.Receipt is null);
        Assert.NotNull(h.Coordinator.PendingSave);
        Assert.Single(h.Store.Saves);
        Assert.Single(h.FactoryAdmissions);
        Assert.Equal(1, h.Physical.Sends);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_CancelledGenerationUsesSeparatePersistenceAttemptAndSavesTruthfulPartialAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.Body.HoldAfterText = true;
        using var cancel = new CancellationTokenSource();
        h.Store.Save = async (request, subject, token) =>
        {
            Assert.True(cancel.IsCancellationRequested);
            Assert.False(token.IsCancellationRequested);
            Assert.Equal(0, h.Requests.ActiveRequests);
            return await h.Owner.Store.SaveTerminalAsync(request, subject, token);
        };
        Task<OctoberChatCoordinationResult> run = h.ExecuteAsync(executionCancellation: cancel.Token);
        try
        {
            Assert.Same(h.TextObserved.Task, await Task.WhenAny(h.TextObserved.Task, run).WaitAsync(TimeSpan.FromSeconds(10)));
            cancel.Cancel();
            OctoberChatCoordinationResult result = await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(DialogueTurnState.Interrupted, result.State);
            Assert.Equal(DialogueTerminalReason.UserCancelled, result.Terminal!.Receipt!.Reason);
            DialogueTerminalSaveRequest saved = Assert.Single(h.Store.Saves);
            Assert.Equal("受控即时回答😀。", saved.AnswerText);
            Assert.Equal(DialogueTerminalState.Interrupted, saved.TerminalState);
            Assert.Equal(DialogueTerminalReason.UserCancelled, saved.Reason);
            Assert.Empty((await h.Owner.Store.ReadHistoryAsync(h.Owner.HistoryRequest, h.Owner.Subject)).Turns);
            Assert.Null((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
        }
        finally { h.Body.ReleaseText.TrySetResult(); await run; }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_SingleLifecycleCannotOverwriteActiveFrozenRequestAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.Body.HoldAfterText = true;
        Task<OctoberChatCoordinationResult> first = h.ExecuteAsync();
        try
        {
            Assert.Same(h.TextObserved.Task, await Task.WhenAny(h.TextObserved.Task, first).WaitAsync(TimeSpan.FromSeconds(10)));
            DialogueBeginRequest other = h.Request with { ClientRequestKey = Guid.NewGuid(), Question = "another request" };
            other = other with { RequestDigest = h.Codec.ComputeBeginDigest(h.Owner.Identity, other) };
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => h.ExecuteAsync(request: other));
            Assert.False(first.IsCompleted);
            Assert.Single(h.Store.Begins);
            Assert.Equal(h.Request, h.Coordinator.Begin!.Admission!.Request);
            Assert.Single(h.FactoryAdmissions);
        }
        finally { h.Body.ReleaseText.TrySetResult(); await first; }
        Assert.Equal(DialogueTurnState.Completed, (await first).State);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_LateFactoryRetainsWholeLifecycleAndDoesNotSaveBeforeSessionArrivesAndDrainsAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.FactoryRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = new CancellationTokenSource();
        h.Store.Save = async (request, subject, token) =>
        {
            Assert.True(h.FactoryRelease.Task.IsCompleted);
            Assert.Equal(1, h.Physical.Disposals);
            return await h.Owner.Store.SaveTerminalAsync(request, subject, token);
        };
        Task<OctoberChatCoordinationResult> run = h.ExecuteAsync(executionCancellation: cancel.Token);
        try
        {
            Assert.Same(h.FactoryCreated.Task, await Task.WhenAny(h.FactoryCreated.Task, run).WaitAsync(TimeSpan.FromSeconds(10)));
            cancel.Cancel();
            Assert.False(run.IsCompleted);
            Assert.Empty(h.Store.Saves);
            Assert.Equal(0, h.Physical.Sends);
            Assert.Equal(DialogueTurnState.Admitted, Assert.Single(h.FactoryAdmissions).Admission!.State);
        }
        finally { h.FactoryRelease.TrySetResult(); await run; }

        OctoberChatCoordinationResult result = await run;
        Assert.NotEqual(DialogueTurnState.Completed, result.State);
        Assert.Single(h.Store.Saves);
        Assert.Equal(0, h.Physical.Sends);
        Assert.Equal(1, h.Physical.Disposals);
        Assert.Null((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_IgnoredPhysicalReadCancellationKeepsSlotAndSavePendingUntilActualDrainAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.Body.HoldEofIgnoringCancellation = true;
        using var cancel = new CancellationTokenSource();
        h.Store.Save = async (request, subject, token) =>
        {
            Assert.True(h.Body.ReleaseEof.Task.IsCompleted);
            Assert.Equal(0, h.Requests.ActiveRequests);
            Assert.Equal(1, h.Physical.Disposals);
            return await h.Owner.Store.SaveTerminalAsync(request, subject, token);
        };
        Task<OctoberChatCoordinationResult> run = h.ExecuteAsync(executionCancellation: cancel.Token);
        try
        {
            Assert.Same(h.Body.EofEntered.Task, await Task.WhenAny(h.Body.EofEntered.Task, run).WaitAsync(TimeSpan.FromSeconds(10)));
            cancel.Cancel();
            Assert.False(run.IsCompleted);
            Assert.Empty(h.Store.Saves);
            Assert.Equal(1, h.Requests.ActiveRequests);
            Assert.Equal(0, h.Physical.Disposals);
            Assert.NotNull((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
        }
        finally { h.Body.ReleaseEof.TrySetResult(); await run; }

        Assert.Equal(DialogueTurnState.Interrupted, (await run).State);
        Assert.Single(h.Store.Saves);
        Assert.Equal(0, h.Requests.ActiveRequests);
        Assert.Equal(1, h.Physical.Disposals);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeDependencyStatus.Denied, DialogueStatus.Denied, DialogueTerminalReason.AuthorizationDenied)]
    [InlineData(KnowledgeDependencyStatus.AuthorityUnavailable, DialogueStatus.AuthorityUnavailable, DialogueTerminalReason.AuthorityUnavailable)]
    public async Task ExecuteAsync_AuthorityChangesDuringActualDrainSaveOnlySanitizedClosureAsync(
        KnowledgeDependencyStatus dependencyStatus, DialogueStatus transitionStatus, DialogueTerminalReason reason)
    {
        await using Harness h = await Harness.OpenAsync();
        h.Body.HoldEofIgnoringCancellation = true;
        using var cancel = new CancellationTokenSource();
        var releaseSaving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var savingResult = new TaskCompletionSource<DialogueTransitionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Store.Transition = async (request, subject, token) =>
        {
            if (request.NextState == DialogueTurnState.Saving) await releaseSaving.Task.WaitAsync(token);
            DialogueTransitionResult result = await h.Owner.Store.TransitionTurnAsync(request, subject, token);
            if (request.NextState == DialogueTurnState.Saving) savingResult.TrySetResult(result);
            return result;
        };
        h.Store.Save = async (request, subject, token) =>
        {
            Assert.True(h.Body.ReleaseEof.Task.IsCompleted);
            Assert.Equal(0, h.Requests.ActiveRequests);
            Assert.Equal(1, h.Physical.Disposals);
            Assert.False(token.IsCancellationRequested);
            Assert.NotNull((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
            return await h.Owner.Store.SaveTerminalAsync(request, subject, token);
        };
        Task<OctoberChatCoordinationResult> run = h.ExecuteAsync(executionCancellation: cancel.Token);
        try
        {
            Assert.Same(h.Body.EofEntered.Task, await Task.WhenAny(h.Body.EofEntered.Task, run).WaitAsync(TimeSpan.FromSeconds(10)));
            cancel.Cancel();
            OctoberChatGenerationResult generated = await Assert.Single(h.GenerationTasks).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("受控即时回答😀。", generated.AnswerText);
            Assert.NotNull(generated.InferenceDependencies);

            // Revoke only after generation has returned its old, previously authorized facts.
            h.Owner.Dependencies.Status = dependencyStatus;
            releaseSaving.TrySetResult();
            DialogueTransitionResult saving = await savingResult.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(transitionStatus, saving.Status);
            Assert.NotEqual(DialogueTurnState.Saving, saving.State);
            Assert.False(run.IsCompleted);
            Assert.Empty(h.Store.Saves);
            Assert.Equal(1, h.Requests.ActiveRequests);
            Assert.Equal(0, h.Physical.Disposals);
            Assert.NotNull((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
        }
        finally
        {
            releaseSaving.TrySetResult();
            h.Body.ReleaseEof.TrySetResult();
            await run;
        }

        OctoberChatCoordinationResult closed = await run;
        Assert.Equal(DialogueTurnState.Failed, closed.State);
        Assert.Equal(DialogueStatus.Succeeded, closed.Terminal!.Status);
        Assert.Equal(reason, closed.Terminal.Receipt!.Reason);
        Assert.Null(closed.Terminal.Projection);
        DialogueTerminalSaveRequest saved = Assert.Single(h.Store.Saves);
        Assert.Equal(DialogueTerminalState.Failed, saved.TerminalState);
        Assert.Equal(reason, saved.Reason);
        Assert.Equal(h.SaveId, saved.SaveOperationId);
        Assert.Equal(h.Coordinator.Begin!.Admission!.Fence, saved.Owner.Fence);
        Assert.Equal(string.Empty, saved.AnswerText);
        Assert.Empty(saved.References);
        Assert.Empty(saved.ConsumedReads);
        Assert.Null(saved.InferenceDependencies);
        Assert.Equal(h.Codec.ComputeOutcomeDigest(saved), saved.OutcomeDigest);
        Assert.Equal("受控即时回答😀。", h.Visible.ToString());
        Assert.Equal(1, h.Physical.Sends);
        Assert.Equal(0, h.Requests.ActiveRequests);

        // Restore access only to inspect the real stored projection and history.
        h.Owner.Dependencies.Status = KnowledgeDependencyStatus.Succeeded;
        Assert.Null((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
        Assert.Empty((await h.Owner.Store.ReadHistoryAsync(h.Owner.HistoryRequest, h.Owner.Subject)).Turns);
        var transcript = Assert.Single((await h.Owner.Store.ReadTranscriptAsync(h.Owner.HistoryRequest, h.Owner.Subject)).Turns);
        Assert.Equal(string.Empty, transcript.Question);
        Assert.Equal(string.Empty, transcript.AnswerText);
        Assert.Empty(transcript.References);
        Assert.Null(transcript.InferenceDependencies);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DialogueTurnState.Running)]
    [InlineData(DialogueTurnState.Saving)]
    public async Task ReconcileAsync_LostTransitionAcknowledgementConfirmsExactPhaseBeforeFirstSaveWithoutDispatchAsync(
        DialogueTurnState lostPhase)
    {
        await using Harness h = await Harness.OpenAsync();
        bool acknowledgementLost = false;
        DateTimeOffset executionTime = h.Clock.GetUtcNow();
        h.Store.Transition = async (request, subject, token) =>
        {
            DialogueTransitionResult committed = await h.Owner.Store.TransitionTurnAsync(request, subject, token);
            if (request.NextState != lostPhase || acknowledgementLost) return committed;
            Assert.Equal(DialogueStatus.Succeeded, committed.Status);
            Assert.Equal(lostPhase, committed.State);
            acknowledgementLost = true;
            return new(DialogueStatus.Indeterminate, null);
        };

        OctoberChatCoordinationResult uncertain = await h.ExecuteAsync();

        Assert.True(acknowledgementLost);
        Assert.Null(uncertain.State);
        Assert.Null(h.Coordinator.State);
        Assert.Null(uncertain.Terminal);
        Assert.Empty(h.Store.Saves);
        DialogueAdmission original = h.Coordinator.Begin!.Admission!;
        Assert.Equal(h.Request, original.Request);
        Assert.Equal(original.TurnId, (await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
        bool generationStarted = lostPhase == DialogueTurnState.Saving;
        Assert.Equal(generationStarted ? 1 : 0, h.FactoryAdmissions.Count);
        Assert.Equal(generationStarted ? 1 : 0, h.GenerationTasks.Count);
        Assert.Equal(generationStarted ? 1 : 0, h.Physical.Sends);
        Assert.Equal(0, h.Requests.ActiveRequests);
        OctoberChatGenerationResult? generated = generationStarted ? await Assert.Single(h.GenerationTasks) : null;
        if (generationStarted) Assert.Equal(1, h.Physical.Disposals);

        // Recovery must not replace the original outcome time with the later lookup time.
        h.Clock.Advance(TimeSpan.FromSeconds(3));
        OctoberChatCoordinationResult recovered = await h.Coordinator.ReconcileAsync();

        Assert.Equal(generationStarted ? DialogueTurnState.Completed : DialogueTurnState.Failed, recovered.State);
        Assert.Equal(DialogueStatus.Succeeded, recovered.Terminal!.Status);
        Assert.False(recovered.Begin.MayDispatch);
        Assert.NotEmpty(h.Store.AdmissionLookups);
        Assert.All(h.Store.AdmissionLookups, lookup =>
        {
            Assert.Equal(h.Request.ConversationId, lookup.ConversationId);
            Assert.Equal(h.Request.ContextId, lookup.ContextId);
            Assert.Equal(h.Request.ClientRequestKey, lookup.ClientRequestKey);
            Assert.Equal(h.Request.RequestDigest, lookup.RequestDigest);
        });
        DialogueTerminalSaveRequest saved = Assert.Single(h.Store.Saves);
        Assert.Equal(h.SaveId, saved.SaveOperationId);
        Assert.Equal(original.TurnId, saved.Owner.TurnId);
        Assert.Equal(original.Fence, saved.Owner.Fence);
        Assert.Equal(h.Request.Execution, saved.Owner.Execution);
        Assert.Equal(executionTime, saved.GenerationEndedAtUtc);
        Assert.Equal(h.Codec.ComputeOutcomeDigest(saved), saved.OutcomeDigest);
        if (generated is not null)
        {
            Assert.Equal(generated.AnswerText, saved.AnswerText);
            Assert.Equal(generated.GenerationEndedAtUtc, saved.GenerationEndedAtUtc);
            Assert.Equal(generated.InferenceDependencies, saved.InferenceDependencies);
            Assert.Equal(generated.ProviderFinish, saved.ProviderFinish);
            Assert.Equal(DialogueTerminalState.Completed, saved.TerminalState);
            Assert.Equal(DialogueTerminalReason.None, saved.Reason);
        }
        else
        {
            Assert.Equal(DialogueTerminalState.Failed, saved.TerminalState);
            Assert.Equal(DialogueTerminalReason.ExecutionFailure, saved.Reason);
            Assert.Equal(DialogueProviderFinish.NotStarted, saved.ProviderFinish);
            Assert.Equal(string.Empty, saved.AnswerText);
        }
        Assert.Null((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);

        OctoberChatCoordinationResult repeated = await h.Coordinator.ReconcileAsync();
        Assert.Equal(recovered.State, repeated.State);
        Assert.Equal(recovered.Terminal.Receipt, repeated.Terminal!.Receipt);
        Assert.Single(h.Store.Saves);
        Assert.Equal(generationStarted ? 1 : 0, h.FactoryAdmissions.Count);
        Assert.Equal(generationStarted ? 1 : 0, h.GenerationTasks.Count);
        Assert.Equal(generationStarted ? 1 : 0, h.Physical.Sends);
        Assert.Single(h.Store.Begins);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReconcileAsync_TimedOutSavingOperationMustActuallySettleBeforeFirstSaveAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOperation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DialogueTransitionResult>? pendingOperation = null;
        CancellationToken attemptToken = default;
        async Task<DialogueTransitionResult> CommitThenHoldAsync(DialogueTransitionRequest request, KnowledgeSubject subject,
            CancellationToken token)
        {
            DialogueTransitionResult actual = await h.Owner.Store.TransitionTurnAsync(request, subject, token);
            Assert.Equal(DialogueStatus.Succeeded, actual.Status);
            Assert.Equal(DialogueTurnState.Saving, actual.State);
            attemptToken = token;
            committed.TrySetResult();
            await releaseOperation.Task; // The owned store operation deliberately ignores cancellation.
            return actual;
        }
        h.Store.Transition = (request, subject, token) => request.NextState == DialogueTurnState.Saving
            ? pendingOperation = CommitThenHoldAsync(request, subject, token)
            : h.Owner.Store.TransitionTurnAsync(request, subject, token);
        Task<OctoberChatCoordinationResult> run = h.ExecuteAsync();
        try
        {
            Assert.Same(committed.Task, await Task.WhenAny(committed.Task, run).WaitAsync(TimeSpan.FromSeconds(10)));
            h.Clock.Advance(OctoberDialogueLimits.MaximumPersistenceAttempt + TimeSpan.FromSeconds(1));
            OctoberChatCoordinationResult uncertain = await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(attemptToken.IsCancellationRequested);
            Assert.NotNull(pendingOperation);
            Assert.False(pendingOperation.IsCompleted);
            Assert.Null(uncertain.State);
            Assert.Empty(h.Store.Saves);

            // A matching durable row is real, but is not proof that the original operation settled.
            DialogueBeginResult found = await h.Owner.Store.LookupAdmissionAsync(new(h.Request.ConversationId,
                h.Request.ContextId, h.Request.ClientRequestKey, h.Request.RequestDigest), h.Owner.Subject);
            Assert.Equal(DialogueStatus.Existing, found.Status);
            Assert.Equal(DialogueTurnState.Saving, found.Admission!.State);
            OctoberChatCoordinationResult held = await h.Coordinator.ReconcileAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Null(held.State);
            Assert.Null(held.Terminal);
            Assert.Empty(h.Store.Saves);
            Assert.Equal(found.Admission.TurnId, (await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
            Assert.Single(h.GenerationTasks);
            Assert.Equal(1, h.Physical.Sends);
        }
        finally
        {
            releaseOperation.TrySetResult();
            if (pendingOperation is not null) await pendingOperation;
            await run;
        }

        OctoberChatCoordinationResult settled = await h.Coordinator.ReconcileAsync();
        Assert.Equal(DialogueTurnState.Completed, settled.State);
        DialogueTerminalSaveRequest saved = Assert.Single(h.Store.Saves);
        OctoberChatGenerationResult generated = await Assert.Single(h.GenerationTasks);
        Assert.Equal(generated.AnswerText, saved.AnswerText);
        Assert.Equal(generated.GenerationEndedAtUtc, saved.GenerationEndedAtUtc);
        Assert.Equal(h.SaveId, saved.SaveOperationId);
        Assert.Equal(1, h.Physical.Sends);
        Assert.Null((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReconcileAsync_SynchronousSaveThrowNeverResubmitsOrRewritesAttemptedPayloadAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.Store.Save = (_, _, _) => throw new InvalidOperationException("Controlled synchronous save failure.");

        OctoberChatCoordinationResult failedAttempt = await h.ExecuteAsync();

        Assert.Equal(DialogueTurnState.SaveIndeterminate, failedAttempt.State);
        DialogueTerminalSaveRequest frozen = Assert.Single(h.Store.Saves);
        Assert.Same(frozen, h.Coordinator.PendingSave);
        Assert.Equal(DialogueTerminalState.Completed, frozen.TerminalState);
        Assert.Equal("受控即时回答😀。", frozen.AnswerText);
        Assert.NotNull(frozen.InferenceDependencies);
        h.Owner.Dependencies.Status = KnowledgeDependencyStatus.Denied;
        h.Clock.Advance(TimeSpan.FromSeconds(3));

        for (int attempt = 0; attempt < 2; attempt++)
        {
            OctoberChatCoordinationResult uncertain = await h.Coordinator.ReconcileAsync();
            Assert.Equal(DialogueTurnState.SaveIndeterminate, uncertain.State);
            Assert.Null(uncertain.Terminal?.Receipt);
            Assert.Same(frozen, h.Coordinator.PendingSave);
            Assert.Single(h.Store.Saves);
        }

        Assert.Equal(2, h.Store.TerminalLookups.Count);
        Assert.All(h.Store.TerminalLookups, lookup =>
        {
            Assert.Equal(frozen.Owner.TurnId, lookup.TurnId);
            Assert.Equal(frozen.Owner.Fence, lookup.ExpectedFence);
            Assert.Equal(frozen.SaveOperationId, lookup.SaveOperationId);
            Assert.Equal(frozen.OutcomeDigest, lookup.OutcomeDigest);
        });
        Assert.Single(h.FactoryAdmissions);
        Assert.Single(h.GenerationTasks);
        Assert.Equal(1, h.Physical.Sends);
        Assert.NotNull((await h.Owner.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ThrowingGenerationObserverRetainsTaskAndDrainsBeforeNoncompletedSaveAsync()
    {
        await using Harness h = await Harness.OpenAsync();
        h.Body.HoldAfterText = true;
        h.Physical.HoldDispose = true;
        bool observedPending = false;
        h.ObserveGeneration = task =>
        {
            observedPending = !task.IsCompleted;
            Assert.Equal(0, h.Physical.Sends);
            throw new InvalidOperationException("Controlled observer registration failure.");
        };
        h.Store.Save = async (request, subject, token) =>
        {
            Assert.True(h.Physical.ReleaseDispose.Task.IsCompleted);
            Assert.NotEqual(DialogueTerminalState.Completed, request.TerminalState);
            return await h.Owner.Store.SaveTerminalAsync(request, subject, token);
        };
        Task<OctoberChatCoordinationResult> run = h.ExecuteAsync();
        try
        {
            Assert.Same(h.Physical.DisposeEntered.Task, await Task.WhenAny(h.Physical.DisposeEntered.Task, run).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(observedPending);
            Assert.Single(h.GenerationTasks);
            Assert.False(run.IsCompleted);
            Assert.Empty(h.Store.Saves);
            Assert.Equal(0, h.Physical.Sends);
        }
        finally
        {
            h.Body.ReleaseText.TrySetResult();
            h.Physical.ReleaseDispose.TrySetResult();
            await run;
        }

        Assert.NotEqual(DialogueTurnState.Completed, (await run).State);
        Assert.True(Assert.Single(h.GenerationTasks).IsCompleted);
        Assert.Single(h.Store.Saves);
        Assert.Equal(0, h.Requests.ActiveRequests);
        Assert.Equal(0, h.Physical.Sends);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private Harness(DialoguePersistenceFixture owner)
        {
            Owner = owner;
            Clock = new(owner.Clock);
            Store = new(owner.Store);
            Budget = new(timeProvider: Clock);
            var row = new LlmProvider("controlled-model", "https://controlled.invalid/v1", "controlled-protected-key",
                ApiType.ChatCompletions, AgentType.Chat, Clock.GetUtcNow());
            Providers = new(row.Id, new ProviderRepository(row), new ControlledSecret());
            Context = new(new(owner.Identity.Publication, owner.Subject, owner.Identity.Mode, KnowledgePurpose.Chat, owner.Identity.RepositoryScope));
            Physical = new(Body);
        }
        public static async Task<Harness> OpenAsync()
        {
            var h = new Harness(await DialoguePersistenceFixture.CreateAsync());
            await h.Owner.RequireOpenAsync();
            h.Snapshot = Assert.IsType<OctoberChatProviderSnapshot>(await h.Providers.FreezeAsync());
            h.History = await h.Owner.Store.ReadHistoryAsync(h.Owner.HistoryRequest, h.Owner.Subject);
            Assert.Equal(DialogueStatus.Succeeded, h.History.Status);
            string prompt = (await new OctoberChatPromptCatalog().LoadAsync(KnowledgeMode.IT))!;
            DialogueBeginRequest request = h.Owner.BeginRequest(history: h.History) with
            {
                ProviderProfileId = h.Snapshot.ProviderId.ToString("D"), ProviderProfileRevision = h.Snapshot.Revision,
                PromptDigest = OctoberKnowledgePolicy.Hash(prompt), Limits = h.Budget.Limits,
            };
            h.Request = request with { RequestDigest = h.Codec.ComputeBeginDigest(h.Owner.Identity, request) };
            h.Coordinator = h.NewCoordinator();
            return h;
        }
        public DialoguePersistenceFixture Owner { get; }
        public ControlledClock Clock { get; }
        public StoreObserver Store { get; }
        public OctoberDialoguePayloadCodec Codec { get; } = new();
        public OctoberChatBudget Budget { get; }
        public OctoberChatProviderBindingService Providers { get; }
        public OctoberChatProviderSnapshot Snapshot { get; private set; } = null!;
        public FixedContext Context { get; }
        public OctoberChatRequestAdmission Requests { get; } = new(1);
        public ControlledBody Body { get; } = new();
        public PhysicalHandler Physical { get; }
        public DialogueBeginRequest Request { get; private set; } = null!;
        public DialogueHistoryResult History { get; private set; } = null!;
        public ReadSelection Selection { get; } = new("controlled Chat-purpose selection");
        public Guid SaveId { get; } = Guid.NewGuid();
        public OctoberChatCoordinator Coordinator { get; private set; } = null!;
        public OctoberChatClientSession? Session { get; private set; }
        public List<DialogueBeginResult> FactoryAdmissions { get; } = [];
        public List<Task<OctoberChatGenerationResult>> GenerationTasks { get; } = [];
        public Action<Task<OctoberChatGenerationResult>>? ObserveGeneration { get; set; }
        public StringBuilder Visible { get; } = new();
        public TaskCompletionSource TextObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FactoryCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? FactoryRelease { get; set; }

        public OctoberChatCoordinator NewCoordinator() => new(Store, Codec, new NoToolReader(), Owner.Dependencies, Owner.Execution,
            Context, Providers, Requests, Clock, CreateSessionAsync, task => { GenerationTasks.Add(task); ObserveGeneration?.Invoke(task); });
        private async Task<OctoberChatClientSession> CreateSessionAsync(DialogueBeginResult admission, OctoberChatBudget budget,
            OctoberChatProviderSnapshot snapshot, Func<CancellationToken, Task> reauthorize, CancellationToken cancellationToken)
        {
            FactoryAdmissions.Add(admission);
            Session = await OctoberChatClientSession.CreateAsync(Providers, snapshot, admission, budget, Owner.Execution,
                reauthorize, Requests, Physical, Clock, cancellationToken);
            FactoryCreated.TrySetResult();
            if (FactoryRelease is not null) await FactoryRelease.Task;
            return Session;
        }
        public Task<OctoberChatCoordinationResult> ExecuteAsync(OctoberChatCoordinator? coordinator = null, DialogueBeginRequest? request = null,
            Guid? saveId = null, CancellationToken executionCancellation = default) =>
            (coordinator ?? Coordinator).ExecuteAsync(request ?? Request, Budget, Selection, History, Snapshot, saveId ?? SaveId,
                (text, _) => { Visible.Append(text); TextObserved.TrySetResult(); return ValueTask.CompletedTask; },
                explicitExecutionCancellation: executionCancellation);
        public async ValueTask DisposeAsync()
        {
            Body.ReleaseText.TrySetResult();
            Body.ReleaseEof.TrySetResult();
            FactoryRelease?.TrySetResult();
            Physical.ReleaseDispose.TrySetResult();
            if (Session is not null) await Session.SealAndWaitForSettlementAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await Owner.DisposeAsync();
        }
    }

    /// <summary>Only observes/delegates canonical store operations; optional responses model lost acknowledgements, not a replacement transaction engine.</summary>
    private sealed class StoreObserver(IOctoberDialogueStore inner) : IOctoberDialogueStore
    {
        public List<DialogueBeginRequest> Begins { get; } = [];
        public List<DialogueAdmissionLookup> AdmissionLookups { get; } = [];
        public List<DialogueTransitionRequest> Transitions { get; } = [];
        public List<DialogueTerminalSaveRequest> Saves { get; } = [];
        public List<DialogueTerminalLookup> TerminalLookups { get; } = [];
        public TaskCompletionSource SaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<DialogueBeginRequest, KnowledgeSubject, CancellationToken, Task<DialogueBeginResult>>? Admit { get; set; }
        public Func<DialogueTransitionRequest, KnowledgeSubject, CancellationToken, Task<DialogueTransitionResult>>? Transition { get; set; }
        public Func<DialogueTerminalSaveRequest, KnowledgeSubject, CancellationToken, Task<DialogueTerminalResult>>? Save { get; set; }
        public Func<DialogueTerminalLookup, KnowledgeSubject, CancellationToken, Task<DialogueTerminalResult>>? Lookup { get; set; }
        public Task<DialogueConversationResult> OpenConversationAsync(DialogueConversationOpenRequest request, KnowledgeSubject subject, CancellationToken token = default) => inner.OpenConversationAsync(request, subject, token);
        public Task<DialogueBeginResult> BeginTurnAsync(DialogueBeginRequest request, KnowledgeSubject subject, CancellationToken token = default)
        { Begins.Add(request); return Admit?.Invoke(request, subject, token) ?? inner.BeginTurnAsync(request, subject, token); }
        public Task<DialogueBeginResult> LookupAdmissionAsync(DialogueAdmissionLookup request, KnowledgeSubject subject, CancellationToken token = default)
        { AdmissionLookups.Add(request); return inner.LookupAdmissionAsync(request, subject, token); }
        public Task<DialogueTransitionResult> TransitionTurnAsync(DialogueTransitionRequest request, KnowledgeSubject subject, CancellationToken token = default)
        { Transitions.Add(request); return Transition?.Invoke(request, subject, token) ?? inner.TransitionTurnAsync(request, subject, token); }
        public Task<DialogueTerminalResult> SaveTerminalAsync(DialogueTerminalSaveRequest request, KnowledgeSubject subject, CancellationToken token = default)
        { Saves.Add(request); SaveEntered.TrySetResult(); return Save?.Invoke(request, subject, token) ?? inner.SaveTerminalAsync(request, subject, token); }
        public Task<DialogueTerminalResult> LookupTerminalAsync(DialogueTerminalLookup request, KnowledgeSubject subject, CancellationToken token = default)
        { TerminalLookups.Add(request); return Lookup?.Invoke(request, subject, token) ?? inner.LookupTerminalAsync(request, subject, token); }
        public Task<DialogueHistoryResult> ReadHistoryAsync(DialogueHistoryRequest request, KnowledgeSubject subject, CancellationToken token = default) => inner.ReadHistoryAsync(request, subject, token);
        public Task<DialogueTranscriptResult> ReadTranscriptAsync(DialogueHistoryRequest request, KnowledgeSubject subject, CancellationToken token = default) => inner.ReadTranscriptAsync(request, subject, token);
        public Task<DialogueOrphanResult> InterruptOrphanAsync(DialogueOrphanRequest request, CancellationToken token = default) => inner.InterruptOrphanAsync(request, token);
    }

    private sealed class FixedContext(ReadSelectionRequest request) : IOctoberChatContextProvider
    {
        public event Action? ContextChanged { add { } remove { } }
        public Task<ReadSelectionRequest?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult<ReadSelectionRequest?>(request);
    }
    private sealed class ControlledClock(DialogueTestClock owner) : TimeProvider
    {
        private readonly ControlledChatFixture.ManualClock _elapsed = new();
        private readonly DateTimeOffset _origin = owner.Now;
        public override DateTimeOffset GetUtcNow() => _origin + (_elapsed.GetUtcNow() - DateTimeOffset.UnixEpoch);
        public override long GetTimestamp() => _elapsed.GetTimestamp();
        public override long TimestampFrequency => _elapsed.TimestampFrequency;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => _elapsed.CreateTimer(callback, state, dueTime, period);
        public void Advance(TimeSpan elapsed) { owner.Now = GetUtcNow() + elapsed; _elapsed.Advance(elapsed); }
    }
    private sealed class ProviderRepository(LlmProvider row) : ILlmProviderRepository
    {
        public Task<LlmProvider?> GetByIdAsync(Guid id, CancellationToken token = default) => Task.FromResult<LlmProvider?>(id == row.Id ? row : null);
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken token = default) => throw new InvalidOperationException();
        public Task<bool> NameExistsAsync(string name, Guid? excludeProviderId = null, CancellationToken token = default) => throw new InvalidOperationException();
        public Task<LlmProvider> AddAsync(LlmProvider provider, CancellationToken token = default) => throw new InvalidOperationException();
        public void Remove(LlmProvider provider) => throw new InvalidOperationException();
        public Task SaveChangesAsync(CancellationToken token = default) => throw new InvalidOperationException();
    }
    private sealed class ControlledSecret : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(string purpose, string plain, CancellationToken token = default) => throw new InvalidOperationException();
        public Task<string> DecryptAsync(string purpose, string cipher, CancellationToken token = default) => Task.FromResult("controlled-dummy-not-a-real-key");
    }
    private sealed class NoToolReader : IOctoberKnowledgeDisclosedReader
    {
        public Task<KnowledgeDisclosureReadResult<SearchPage>> SearchAsync(KnowledgeSearchRequest request, KnowledgeReadOutputBudget budget, CancellationToken token = default) => throw new InvalidOperationException("No tool scripted.");
        public Task<KnowledgeDisclosureReadResult<ContentPage>> ReadExactAsync(ExactReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken token = default) => throw new InvalidOperationException("No tool scripted.");
        public Task<KnowledgeDisclosureReadResult<RelatedPage>> GetRelatedAsync(RelatedReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken token = default) => throw new InvalidOperationException("No tool scripted.");
    }
    private sealed class PhysicalHandler(ControlledBody body) : HttpMessageHandler
    {
        public int Sends { get; private set; }
        public int Disposals { get; private set; }
        public string? RequestBody { get; private set; }
        public bool HoldDispose { get; set; }
        public TaskCompletionSource DisposeEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDispose { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Sends++;
            RequestBody = await request.Content!.ReadAsStringAsync(token);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return response;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
                DisposeEntered.TrySetResult();
                if (HoldDispose) ReleaseDispose.Task.GetAwaiter().GetResult();
            }
            base.Dispose(disposing);
        }
    }
    private sealed class ControlledBody : Stream
    {
        private const string TextEvent = "data: {\"id\":\"controlled-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"controlled-model\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"受控即时回答😀。\"},\"finish_reason\":null}]}\n\n";
        private const string EndEvents = "data: {\"id\":\"controlled-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"controlled-model\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(TextEvent + EndEvents);
        private readonly int _textBytes = Encoding.UTF8.GetByteCount(TextEvent);
        private int _position;
        public bool HoldAfterText { get; set; }
        public bool HoldEofIgnoringCancellation { get; set; }
        public TaskCompletionSource ReleaseText { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource EofEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseEof { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (_position == _bytes.Length && HoldEofIgnoringCancellation) { EofEntered.TrySetResult(); await ReleaseEof.Task; }
            if (_position == _textBytes && HoldAfterText) await ReleaseText.Task.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            int count = Math.Min(buffer.Length, (_position < _textBytes ? _textBytes : _bytes.Length) - _position);
            _bytes.AsMemory(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

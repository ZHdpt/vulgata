using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberDialoguePersistenceTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Codec_HistoryGoldenVectors_PreserveExactUnicodeAndExcludeHostCapsules()
    {
        var codec = new OctoberDialoguePayloadCodec();
        Assert.Equal("2D6A9251579D2AB4E8B5C0C28897CE4A9905ABA98E504EB9C273F1A62960D0D8", codec.ComputeHistoryProjectionDigest([]));
        Assert.Equal("[]", Encoding.UTF8.GetString(codec.EncodeHistoryProjectionUtf8([]).AsSpan()));
        var dependency = new KnowledgeDependencyHandle(DialogueTestProtocol.Id(11), new string('A', 64), "NEVER-MODEL-HOST-CAPSULE");
        ImmutableArray<DialogueHistoryTurn> turns = [new(DialogueTestProtocol.Id(12), 1, "你好😀", "e\u0301", [], dependency)];
        const string expectedModel = "[{\"question\":\"\\u4F60\\u597D\\uD83D\\uDE00\",\"answerText\":\"e\\u0301\",\"references\":[]}]";
        string encoded = Encoding.UTF8.GetString(codec.EncodeHistoryProjectionUtf8(turns).AsSpan());
        Assert.Equal(expectedModel, encoded);
        Assert.Equal(Encoding.UTF8.GetByteCount(expectedModel), codec.MeasureHistoryProjectionUtf8Bytes(turns));
        Assert.DoesNotContain(dependency.ProtectedPayload, encoded, StringComparison.Ordinal);
        Assert.Equal(DialogueTestProtocol.HistoryDigest(turns), codec.ComputeHistoryProjectionDigest(turns));
        Assert.NotEqual(codec.ComputeHistoryProjectionDigest(turns), codec.ComputeHistoryProjectionDigest([turns[0] with { AnswerText = "é" }]));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Codec_BeginAndTerminalCanonicalVectors_BindSemanticFieldsButNotRandomizedCapsules()
    {
        var codec = new OctoberDialoguePayloadCodec();
        var conversation = DialogueTestProtocol.Conversation();
        var begin = DialogueTestProtocol.Begin(conversation);
        Assert.Equal(DialogueTestProtocol.BeginDigest(conversation, begin), codec.ComputeBeginDigest(conversation, begin));
        var reissued = begin with { RequestDigest = new string('B', 64), Execution = new(Guid.NewGuid(), Guid.NewGuid()), InitialDependencies = begin.InitialDependencies with { SetId = Guid.NewGuid(), ProtectedPayload = "different protected encoding" } };
        Assert.Equal(codec.ComputeBeginDigest(conversation, begin), codec.ComputeBeginDigest(conversation, reissued));
        Assert.NotEqual(codec.ComputeBeginDigest(conversation, begin), codec.ComputeBeginDigest(conversation, begin with { Question = "different" }));
        Assert.NotEqual(codec.ComputeBeginDigest(conversation, begin), codec.ComputeBeginDigest(conversation, begin with { Limits = begin.Limits with { Duration = begin.Limits.Duration + TimeSpan.FromTicks(1) } }));
        var terminal = DialogueTestProtocol.Terminal(new(conversation, begin, DialogueTestProtocol.Id(15), 1, 1, DialogueTestProtocol.Now, DialogueTestProtocol.Now.AddMinutes(3), DialogueTurnState.Running));
        Assert.Equal(DialogueTestProtocol.OutcomeDigest(terminal), codec.ComputeOutcomeDigest(terminal));
        Assert.Equal(codec.ComputeOutcomeDigest(terminal), codec.ComputeOutcomeDigest(terminal with { OutcomeDigest = new string('C', 64), InferenceDependencies = terminal.InferenceDependencies! with { ProtectedPayload = "reissued", SetId = Guid.NewGuid() } }));
        Assert.NotEqual(codec.ComputeOutcomeDigest(terminal), codec.ComputeOutcomeDigest(terminal with { Owner = terminal.Owner with { Fence = 2 } }));
        Assert.NotEqual(codec.ComputeOutcomeDigest(terminal), codec.ComputeOutcomeDigest(terminal with { GenerationEndedAtUtc = terminal.GenerationEndedAtUtc.AddTicks(1) }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Codec_EnvelopeMeasurement_IncludesJsonIgnoredCapsulesAndEveryConsumedProof()
    {
        var codec = new OctoberDialoguePayloadCodec();
        var conversation = DialogueTestProtocol.Conversation();
        var begin = DialogueTestProtocol.Begin(conversation);
        Assert.Equal(DialogueTestProtocol.BeginEnvelopeBytes(begin), codec.MeasureBeginEnvelopeUtf8Bytes(begin));
        var larger = begin with { InitialDependencies = begin.InitialDependencies with { ProtectedPayload = new string('x', 5000) } };
        Assert.Equal(DialogueTestProtocol.BeginEnvelopeBytes(larger), codec.MeasureBeginEnvelopeUtf8Bytes(larger));
        Assert.True(codec.MeasureBeginEnvelopeUtf8Bytes(larger) > codec.MeasureBeginEnvelopeUtf8Bytes(begin) + 4000);
        var admission = new DialogueAdmission(conversation, begin, DialogueTestProtocol.Id(16), 1, 1, DialogueTestProtocol.Now, DialogueTestProtocol.Now.AddMinutes(3), DialogueTurnState.Running);
        var reference = new OctoberChatReference(new(conversation.Publication.Knowledge.BatchId, conversation.Publication.PublicationId, DialogueTestProtocol.Id(17), DialogueTestProtocol.Id(18), "章节"), true, 2, 123);
        var terminal = DialogueTestProtocol.Terminal(admission) with { References = [reference], ConsumedReads = [new(reference, new(DialogueTestProtocol.Id(19), new string('D', 64), new string('p', 9000)))] };
        Assert.Equal(DialogueTestProtocol.TerminalEnvelopeBytes(terminal), codec.MeasureTerminalEnvelopeUtf8Bytes(terminal));
        string outward = JsonSerializer.Serialize(terminal);
        Assert.DoesNotContain(new string('p', 9000), outward, StringComparison.Ordinal);
        Assert.True(codec.MeasureTerminalEnvelopeUtf8Bytes(terminal) > Encoding.UTF8.GetByteCount(outward) + 8000);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenConversationAsync_ImmutableOwnerContextSelectionAndNoAuthenticationPersistenceAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var opened = await f.OpenAsync();
        Assert.Equal(DialogueStatus.Succeeded, opened.Status);
        Assert.Equal(f.Identity.ConversationId, opened.Conversation!.Identity.ConversationId);
        Assert.Equal(f.Identity.ContextId, opened.Conversation.Identity.ContextId);
        Assert.Equal(f.Identity.OwnerSubjectId, opened.Conversation.Identity.OwnerSubjectId);
        Assert.Equal(f.Identity.Publication, opened.Conversation.Identity.Publication);
        Assert.Equal(f.Identity.RepositoryScope.ToArray(), opened.Conversation.Identity.RepositoryScope.ToArray());
        Assert.Equal(1, opened.Conversation.NextTurnSequence);
        Assert.Null(opened.Conversation.ActiveTurnId);
        Assert.Equal(DialogueStatus.Existing, (await f.OpenAsync()).Status);
        Assert.Equal(DialogueStatus.Denied, (await f.Store.OpenConversationAsync(f.OpenRequest, new("foreign-owner", "foreign-stamp"))).Status);
        Assert.Equal(DialogueStatus.Conflict, (await f.Store.OpenConversationAsync(f.OpenRequest with { ContextId = Guid.NewGuid() }, f.Subject)).Status);
        Assert.Equal(DialogueStatus.Conflict, (await f.Store.OpenConversationAsync(f.OpenRequest with { CreateIfMissing = f.OpenRequest.CreateIfMissing! with { Mode = KnowledgeMode.Business } }, f.Subject)).Status);
        f.Disclosure.Status = DisclosureStatus.Denied;
        Assert.Null((await f.Store.OpenConversationAsync(f.OpenRequest with { CreateIfMissing = null }, f.Subject)).Conversation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DialogueTurnState.Admitted)]
    [InlineData(DialogueTurnState.Running)]
    [InlineData(DialogueTurnState.Saving)]
    [InlineData(DialogueTurnState.SaveIndeterminate)]
    public async Task BeginTurnAsync_EveryNonterminalStateOccupiesSlot_ExactReplayNeverRedispatchesAsync(DialogueTurnState state)
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        await f.RequireOpenAsync();
        var request = f.BeginRequest();
        var admitted = await f.Store.BeginTurnAsync(request, f.Subject);
        Assert.Equal(DialogueStatus.Succeeded, admitted.Status);
        Assert.True(admitted.MayDispatch);
        var admission = Assert.IsType<DialogueAdmission>(admitted.Admission);
        Assert.Equal(1, admission.Sequence);
        await f.MoveAsync(admission, state);
        var replay = await f.Store.BeginTurnAsync(request, f.Subject);
        Assert.Equal(DialogueStatus.Existing, replay.Status);
        Assert.False(replay.MayDispatch);
        Assert.Equal(admission.TurnId, replay.Admission!.TurnId);
        var lookup = await f.Store.LookupAdmissionAsync(new(f.Identity.ConversationId, f.Identity.ContextId, request.ClientRequestKey, request.RequestDigest), f.Subject);
        Assert.Equal(DialogueStatus.Existing, lookup.Status);
        Assert.False(lookup.MayDispatch);
        Assert.Equal(DialogueStatus.Busy, (await f.Store.BeginTurnAsync(f.BeginRequest(Guid.NewGuid()), f.Subject)).Status);
        var changed = request with { Question = "changed logical input" };
        changed = changed with { RequestDigest = DialogueTestProtocol.BeginDigest(f.Identity, changed) };
        Assert.Equal(DialogueStatus.Conflict, (await f.Store.BeginTurnAsync(changed, f.Subject)).Status);
        Assert.Equal(2, (await f.OpenAsync()).Conversation!.NextTurnSequence);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SaveTerminalAsync_AtomicOutcomeReplayLookupAndFence_PreservesExactTextAndReleasesSlotAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var admission = await f.AdmitAsync();
        var request = f.TerminalRequest(admission);
        var stale = request with { Owner = request.Owner with { Fence = admission.Fence + 1 } };
        stale = stale with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(stale) };
        Assert.Equal(DialogueStatus.StaleFence, (await f.Store.SaveTerminalAsync(stale, f.Subject)).Status);
        var result = await f.Store.SaveTerminalAsync(request, f.Subject);
        Assert.Equal(DialogueStatus.Succeeded, result.Status);
        Assert.Equal(request.AnswerText, result.Projection!.AnswerText);
        Assert.Equal(DialogueTerminalState.Completed, result.Receipt!.TerminalState);
        var replay = await f.Store.SaveTerminalAsync(request, f.Subject);
        Assert.Equal(DialogueStatus.Existing, replay.Status);
        Assert.Equal(result.Receipt, replay.Receipt);
        var lookup = await f.Store.LookupTerminalAsync(new(f.Identity.ConversationId, f.Identity.ContextId, admission.TurnId, request.SaveOperationId, admission.Fence, request.OutcomeDigest), f.Subject);
        Assert.Equal(result.Receipt, lookup.Receipt);
        var changed = request with { AnswerText = "different" };
        changed = changed with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(changed) };
        Assert.Equal(DialogueStatus.Conflict, (await f.Store.SaveTerminalAsync(changed, f.Subject)).Status);
        var conversation = (await f.OpenAsync()).Conversation!;
        Assert.Null(conversation.ActiveTurnId);
        var history = await f.Store.ReadHistoryAsync(f.HistoryRequest, f.Subject);
        Assert.Equal(admission.TurnId, Assert.Single(history.Turns).TurnId);
        Assert.Equal(request.AnswerText, history.Turns[0].AnswerText);
        Assert.NotNull(history.Dependencies);
        Assert.Equal(DialogueTestProtocol.HistoryDigest(history.Turns), history.ProjectionDigest);
        var next = f.BeginRequest(Guid.NewGuid(), history);
        Assert.Equal(2, (await f.Store.BeginTurnAsync(next, f.Subject)).Admission!.Sequence);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SaveTerminalAsync_UnsettledExecutionOrGuardRefusal_DoesNotReleaseSlotOrPersistPartialOutcomeAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var admission = await f.AdmitAsync();
        var request = f.TerminalRequest(admission);
        f.Execution.TerminalStatus = DialogueExecutionStatus.NotVerified;
        Assert.NotEqual(DialogueStatus.Succeeded, (await f.Store.SaveTerminalAsync(request, f.Subject)).Status);
        Assert.Equal(admission.TurnId, (await f.OpenAsync()).Conversation!.ActiveTurnId);
        Assert.Empty((await f.Store.ReadTranscriptAsync(f.HistoryRequest, f.Subject)).Turns);
        f.Execution.TerminalStatus = DialogueExecutionStatus.Verified;
        f.Guard.RefuseKind = DialogueMutationKind.SaveTerminal;
        Assert.NotEqual(DialogueStatus.Succeeded, (await f.Store.SaveTerminalAsync(request, f.Subject)).Status);
        Assert.Equal(admission.TurnId, (await f.OpenAsync()).Conversation!.ActiveTurnId);
        Assert.Empty((await f.Store.ReadTranscriptAsync(f.HistoryRequest, f.Subject)).Turns);
        f.Guard.RefuseKind = null;
        Assert.Equal(DialogueStatus.Succeeded, (await f.Store.SaveTerminalAsync(request, f.Subject)).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadHistoryAsync_NoncompletedBoundary_ReturnsOnlyNewestContiguousSuffixAndDisplayRemainsSeparateAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var first = await f.AdmitAsync();
        await f.RequireSaveAsync(f.TerminalRequest(first));
        var history = await f.Store.ReadHistoryAsync(f.HistoryRequest, f.Subject);
        var second = await f.AdmitAsync(f.BeginRequest(Guid.NewGuid(), history));
        var interrupted = f.TerminalRequest(second) with { TerminalState = DialogueTerminalState.Interrupted, Reason = DialogueTerminalReason.UserCancelled, ProviderFinish = DialogueProviderFinish.Cancelled, AnswerText = "partial display only" };
        await f.RequireSaveAsync(interrupted with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(interrupted) });
        Assert.Empty((await f.Store.ReadHistoryAsync(f.HistoryRequest, f.Subject)).Turns);
        var third = await f.AdmitAsync(f.BeginRequest(Guid.NewGuid()));
        await f.RequireSaveAsync(f.TerminalRequest(third));
        var suffix = await f.Store.ReadHistoryAsync(f.HistoryRequest, f.Subject);
        Assert.Equal(third.TurnId, Assert.Single(suffix.Turns).TurnId);
        Assert.True(suffix.ContextOmitted);
        var transcript = await f.Store.ReadTranscriptAsync(f.HistoryRequest, f.Subject);
        Assert.Equal(3, transcript.Turns.Length);
        Assert.Contains(transcript.Turns, x => x.TurnId == second.TurnId && x.TerminalState == DialogueTerminalState.Interrupted);
        var staleHistory = history;
        Assert.NotEqual(DialogueStatus.Succeeded, (await f.Store.BeginTurnAsync(f.BeginRequest(Guid.NewGuid(), staleHistory), f.Subject)).Status);
        Assert.Empty((await f.Store.ReadHistoryAsync(f.HistoryRequest with { MaximumUtf8Bytes = 2 }, f.Subject)).Turns);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CompleteLineageAndConsumedProofs_MissingUncitedInputOrForgedCreditFailsAndRevocationSuppressesAllContentAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var admission = await f.AdmitAsync();
        var aggregate = f.Dependencies.Extend(f.Root, "uncited-advisory-and-read");
        var reference = new OctoberChatReference(new(f.Identity.Publication.Knowledge.BatchId, f.Identity.Publication.PublicationId, f.Document.LogicalDocumentId, f.Document.DocumentVersionId), true, 2, 345);
        var proof = f.Dependencies.Consume(reference, aggregate);
        var request = f.TerminalRequest(admission) with { InferenceDependencies = aggregate, References = [reference], ConsumedReads = [new(reference, proof)] };
        request = request with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(request) };
        var forged = request with { ConsumedReads = [new(reference with { IsPartial = false, SuccessfulChunks = 3 }, proof)] };
        forged = forged with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(forged) };
        Assert.NotEqual(DialogueStatus.Succeeded, (await f.Store.SaveTerminalAsync(forged, f.Subject)).Status);
        var missing = request with { InferenceDependencies = f.Dependencies.Create("unrelated") };
        missing = missing with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(missing) };
        Assert.NotEqual(DialogueStatus.Succeeded, (await f.Store.SaveTerminalAsync(missing, f.Subject)).Status);
        await f.RequireSaveAsync(request);
        Assert.True(f.Dependencies.VerifyCalls > 0);
        f.Dependencies.DeniedOrigins.Add("uncited-advisory-and-read");
        var history = await f.Store.ReadHistoryAsync(f.HistoryRequest, f.Subject);
        Assert.Empty(history.Turns);
        Assert.True(history.ContextOmitted);
        Assert.Empty((await f.Store.ReadTranscriptAsync(f.HistoryRequest, f.Subject)).Turns);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SanitizedRevocationClosure_StoresNoProseCapsuleOrHistoryAndReleasesOnlySettledOwnerAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var admission = await f.AdmitAsync();
        f.Disclosure.Status = DisclosureStatus.Denied;
        f.Dependencies.Status = KnowledgeDependencyStatus.Denied;
        var request = f.TerminalRequest(admission) with { TerminalState = DialogueTerminalState.Interrupted, Reason = DialogueTerminalReason.AuthorizationDenied, ProviderFinish = DialogueProviderFinish.Cancelled, AnswerText = "", References = [], ConsumedReads = [], InferenceDependencies = null };
        request = request with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(request) };
        var saved = await f.Store.SaveTerminalAsync(request, f.Subject);
        Assert.Equal(DialogueStatus.Succeeded, saved.Status);
        Assert.NotNull(saved.Receipt);
        Assert.Null(saved.Projection);
        f.Disclosure.Status = DisclosureStatus.Allowed;
        f.Dependencies.Status = KnowledgeDependencyStatus.Succeeded;
        var displayed = Assert.Single((await f.Store.ReadTranscriptAsync(f.HistoryRequest, f.Subject)).Turns);
        Assert.Equal("", displayed.Question);
        Assert.Equal("", displayed.AnswerText);
        Assert.Empty(displayed.References);
        Assert.Null(displayed.InferenceDependencies);
        Assert.Empty((await f.Store.ReadHistoryAsync(f.HistoryRequest, f.Subject)).Turns);
        Assert.Null((await f.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InterruptOrphanAsync_RequiresProvenDeathAndSettlement_FencesOriginalExecutionWithoutRegenerationAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var admission = await f.AdmitAsync();
        var request = new DialogueOrphanRequest(Guid.NewGuid(), f.Identity.ConversationId, f.Identity.ContextId, admission.TurnId, admission.Request.Execution, admission.Fence);
        f.Execution.OwnerDeathStatus = DialogueExecutionStatus.NotVerified;
        Assert.NotEqual(DialogueStatus.Succeeded, (await f.Store.InterruptOrphanAsync(request)).Status);
        Assert.Equal(admission.TurnId, (await f.OpenAsync()).Conversation!.ActiveTurnId);
        f.Execution.OwnerDeathStatus = DialogueExecutionStatus.Verified;
        var orphan = await f.Store.InterruptOrphanAsync(request);
        Assert.Equal(DialogueStatus.Succeeded, orphan.Status);
        Assert.Equal(DialogueTerminalReason.OwnerLost, orphan.Receipt!.Reason);
        Assert.True(orphan.Receipt.Fence > admission.Fence);
        Assert.Null((await f.OpenAsync()).Conversation!.ActiveTurnId);
        Assert.Equal(DialogueStatus.StaleFence, (await f.Store.SaveTerminalAsync(f.TerminalRequest(admission), f.Subject)).Status);
        Assert.Empty((await f.Store.ReadHistoryAsync(f.HistoryRequest, f.Subject)).Turns);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("question-scalars")]
    [InlineData("broken-unicode")]
    [InlineData("provider-bytes")]
    [InlineData("zero-duration")]
    [InlineData("oversized-capsule")]
    public async Task BeginTurnAsync_InvalidBounds_RefusesBeforeAllocatingSequenceAsync(string scenario)
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        await f.RequireOpenAsync();
        var request = f.BeginRequest();
        request = scenario switch
        {
            "question-scalars" => request with { Question = new string('x', 8001) },
            "broken-unicode" => request with { Question = "broken\uD800" },
            "provider-bytes" => request with { ProviderProfileRevision = new string('界', 86) },
            "zero-duration" => request with { Limits = request.Limits with { Duration = TimeSpan.Zero } },
            _ => request with { InitialDependencies = request.InitialDependencies with { ProtectedPayload = new string('p', OctoberDialogueLimits.MaximumBeginEnvelopeUtf8Bytes) } }
        };
        request = request with { RequestDigest = DialogueTestProtocol.BeginDigest(f.Identity, request) };
        Assert.Equal(DialogueStatus.Invalid, (await f.Store.BeginTurnAsync(request, f.Subject)).Status);
        Assert.Equal(1, (await f.OpenAsync()).Conversation!.NextTurnSequence);
        Assert.Null((await f.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SaveTerminalAsync_ProviderCommitHasNotReturnedItsTask_CancellationRetainsExactProducingResourcesUntilSettlementAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        var admission = await f.AdmitAsync();
        var request = f.TerminalRequest(admission);
        await using var reference = f.Factory.CreateDbContext();
        using var providerReturn = new ManualResetEventSlim(false);
        var probe = new AdmissionPhysicalCommitProbe { ReturnGate = providerReturn };
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite(reference.Database.GetConnectionString())
            .AddInterceptors(probe, new AdmissionCommandAdapter()).Options;
        var factory = new AdmissionLifetimeFactory(options);
        var guard = new DialogueAsyncCallbackGuard();
        var store = new OctoberDialogueStore(factory, f.Identity.Publication.Knowledge, f.Disclosure, f.Dependencies,
            f.Execution, guard, new OctoberDialoguePayloadCodec(), f.Clock);
        using var cancellation = new CancellationTokenSource();
        var pending = store.SaveTerminalAsync(request, f.Subject, cancellation.Token);
        OctoberDbContext? producingContext = null;
        try
        {
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            producingContext = Assert.IsType<OctoberDbContext>(probe.ProducingContext);
            // A natural provider synchronous prefix is still executing; its Task has not returned
            // to the store callback. This is not a pause injected into production instructions.
            Assert.False(guard.CallbackReturned);
            cancellation.Cancel();
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2))));
            var result = await pending;
            Assert.Equal(DialogueStatus.Indeterminate, result.Status);
            Assert.Null(result.Receipt);
            Assert.Null(result.Projection);
            Assert.False(probe.Disposed.Task.IsCompleted);
            Assert.Null(Record.Exception(() => producingContext.ChangeTracker.Entries().ToArray()));
            Assert.False(probe.Settled.Task.IsCompleted);
            Assert.Equal(DialogueStatus.Indeterminate, (await f.Store.LookupTerminalAsync(new(f.Identity.ConversationId,
                f.Identity.ContextId, admission.TurnId, request.SaveOperationId, admission.Fence, request.OutcomeDigest), f.Subject)).Status);
        }
        finally
        {
            // Always release both independent provider gates before awaiting cleanup, including on RED.
            providerReturn.Set();
            probe.Release.TrySetResult();
            if (guard.Running is not null) await guard.Running.WaitAsync(TimeSpan.FromSeconds(5));
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (probe.ProducingContext is not null)
            {
                await probe.Settled.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await probe.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await RequireDialogueContextDisposedAsync(probe.ProducingContext);
            }
        }
        var recovered = await f.Store.LookupTerminalAsync(new(f.Identity.ConversationId, f.Identity.ContextId,
            admission.TurnId, request.SaveOperationId, admission.Fence, request.OutcomeDigest), f.Subject);
        Assert.Equal(DialogueStatus.Existing, recovered.Status);
        Assert.Equal(request.OutcomeDigest, recovered.Receipt!.OutcomeDigest);
        Assert.Equal(request.AnswerText, recovered.Projection!.AnswerText);
    }

    private static async Task RequireDialogueContextDisposedAsync(OctoberDbContext context)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try { _ = context.DialogueTurns.ToArray(); }
            catch (ObjectDisposedException) { return; }
            await Task.Delay(20);
        }
        Assert.Fail("The physical dialogue transaction's producing context was not disposed after settlement.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BeginTurnAsync_AuthorityIgnoresCancellation_ReturnsPromptlyAndNeverAdmitsAfterLateAuthorityCompletionAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        await f.RequireOpenAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<DialogueExecutionStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var authorityReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Execution.AdmissionOverride = async () =>
        {
            entered.TrySetResult();
            try { return await release.Task; } // Deliberately ignores the supplied cancellation signal.
            finally { authorityReturned.TrySetResult(); }
        };
        using var cancellation = new CancellationTokenSource();
        var pending = f.Store.BeginTurnAsync(f.BeginRequest(), f.Subject, cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2))));
            try
            {
                var result = await pending;
                Assert.False(result.MayDispatch);
                Assert.Null(result.Admission);
            }
            catch (OperationCanceledException) { }
        }
        finally
        {
            release.TrySetResult(DialogueExecutionStatus.Verified);
            await authorityReturned.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
        await using var context = f.Factory.CreateDbContext();
        Assert.Empty(await context.DialogueTurns.ToArrayAsync());
        Assert.Empty(await context.DialogueTerminals.ToArrayAsync());
        Assert.Equal(1, (await context.DialogueConversations.SingleAsync()).NextTurnSequence);
        Assert.Null((await f.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BeginTurnAsync_ExactSupplementaryUnicodeBoundary_PreservesScalarsBytesAndOriginalExecutionLimitsAsync()
    {
        await using var f = await DialoguePersistenceFixture.CreateAsync();
        await f.RequireOpenAsync();
        var request = f.BeginRequest() with { Question = string.Concat(Enumerable.Repeat("😀", 8000)) };
        request = request with { RequestDigest = DialogueTestProtocol.BeginDigest(f.Identity, request) };
        var admitted = await f.Store.BeginTurnAsync(request, f.Subject);
        Assert.Equal(DialogueStatus.Succeeded, admitted.Status);
        Assert.Equal(32000, Encoding.UTF8.GetByteCount(admitted.Admission!.Request.Question));
        Assert.Equal(request.Question, admitted.Admission.Request.Question);
        Assert.Equal(DialogueTestProtocol.Now + request.Limits.Duration, admitted.Admission.DeadlineUtc);
    }
}

/// <summary>Independent protocol oracle: explicit projections and sorted JSON, never the application codec.</summary>
internal static class DialogueTestProtocol
{
    public static readonly DateTimeOffset Now = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
    public static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false, true).GetBytes(value)));
    public static DialogueConversationIdentity Conversation() => new(Id(1), Id(2), "测试-owner", new(new("controlled/dialogue", "target", Id(3), new string('a', 64)), Id(4)), KnowledgeMode.IT, [Id(5)]);
    public static DialogueBeginRequest Begin(DialogueConversationIdentity identity) => new(identity.ConversationId, identity.ContextId, Id(6), "你好😀 e\u0301", new string('A', 64), "profile", "snapshot", new string('B', 64), [], HistoryDigest([]), new(Id(7), new string('C', 64), "controlled-protected-root"), new(), new(Id(8), Id(9)));
    public static DialogueTerminalSaveRequest Terminal(DialogueAdmission admission) => new(new(admission.Conversation.ConversationId, admission.Conversation.ContextId, admission.TurnId, admission.Request.Execution, admission.Fence), Id(10), new string('D', 64), DialogueTerminalState.Completed, DialogueTerminalReason.None, DialogueProviderFinish.ValidStop, "答案😀 e\u0301\nKeep exact whitespace. ", [], [], admission.Request.InitialDependencies, admission.Request.HistoryTurnIds, Now.AddSeconds(1));
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string BeginDigest(DialogueConversationIdentity c, DialogueBeginRequest r) => CanonicalHash(new
    {
        domain = "october/dialogue-begin/1", schemaVersion = 1,
        conversation = new { c.ConversationId, c.ContextId, c.OwnerSubjectId, c.Publication, c.Mode, repositoryScope = c.RepositoryScope.OrderBy(x => x.ToString("D"), StringComparer.Ordinal), purpose = KnowledgePurpose.Chat },
        request = new { r.ClientRequestKey, r.Question, r.ProviderProfileId, r.ProviderProfileRevision, r.PromptDigest, r.HistoryTurnIds, r.HistoryProjectionDigest, initialDependencyDigest = r.InitialDependencies.BindingDigest,
            limits = new { r.Limits.QuestionCharacters, r.Limits.ProviderRounds, r.Limits.ToolCalls, r.Limits.ConcurrentTools, r.Limits.ToolArgumentBytes, r.Limits.PageSize, r.Limits.ContentBytes, r.Limits.ToolResultBytes, r.Limits.OutputBytes, r.Limits.HistoryTurns, r.Limits.HistoryBytes, durationTicks = r.Limits.Duration.Ticks, r.Limits.SseRoundBytes, r.Limits.SseTurnBytes } }
    });
    public static string OutcomeDigest(DialogueTerminalSaveRequest r) => CanonicalHash(new
    {
        domain = "october/dialogue-terminal/1", schemaVersion = 1, r.Owner, r.SaveOperationId, r.TerminalState, r.Reason, r.ProviderFinish, r.AnswerText, r.References,
        consumedReads = r.ConsumedReads.Select(x => new { x.Reference, proofBindingDigest = x.Proof.BindingDigest }),
        inferenceDependencyDigest = r.InferenceDependencies?.BindingDigest, r.HistoryTurnIds,
        generationEndedAtUtc = r.GenerationEndedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", System.Globalization.CultureInfo.InvariantCulture)
    });
    public static string HistoryDigest(ImmutableArray<DialogueHistoryTurn> turns) => CanonicalHash(new
    {
        domain = "october/dialogue-history/1", schemaVersion = 1,
        turns = turns.Select(x => new { x.TurnId, x.Sequence, x.Question, x.AnswerText, x.References, inferenceDependencyDigest = x.InferenceDependencies.BindingDigest })
    });
    public static int BeginEnvelopeBytes(DialogueBeginRequest r) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        r.ConversationId, r.ContextId, r.ClientRequestKey, r.Question, r.RequestDigest, r.ProviderProfileId, r.ProviderProfileRevision, r.PromptDigest, r.HistoryTurnIds, r.HistoryProjectionDigest, r.InitialDependencies, r.Limits, r.Execution
    }, _json).Length;
    public static int TerminalEnvelopeBytes(DialogueTerminalSaveRequest r) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        r.Owner, r.SaveOperationId, r.OutcomeDigest, r.TerminalState, r.Reason, r.ProviderFinish, r.AnswerText, r.References,
        consumedReads = r.ConsumedReads.Select(x => new { x.Reference, x.Proof }), r.InferenceDependencies, r.HistoryTurnIds, r.GenerationEndedAtUtc
    }, _json).Length;
    private static string CanonicalHash<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, _json));
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes)) Write(writer, document.RootElement);
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); Write(writer, property.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); break;
            default: value.WriteTo(writer); break;
        }
    }
}

/// <summary>Production store on owned SQLite or caller-owned suite database; host authority decisions are controlled, not runtime qualification.</summary>
internal sealed class DialoguePersistenceFixture : IAsyncDisposable
{
    private readonly OctoberKnowledgeAcceptanceFixture _knowledge;
    private DialoguePersistenceFixture(OctoberKnowledgeAcceptanceFixture knowledge, PublicationBinding publication, DocumentVersionReference document)
    {
        _knowledge = knowledge;
        Document = document;
        Identity = new(DialogueTestProtocol.Id(501), DialogueTestProtocol.Id(502), Subject.SubjectId, publication, KnowledgeMode.IT, [knowledge.Origin.RepositoryId]);
        Root = Dependencies.Create("question-root");
        Store = new OctoberDialogueStore(knowledge.Factory, knowledge.Binding, Disclosure, Dependencies, Execution, Guard, new OctoberDialoguePayloadCodec(), Clock);
    }
    public DialogueTestClock Clock { get; } = new();
    public IOctoberDialogueStore Store { get; }
    public KnowledgeContextFactory Factory => _knowledge.Factory;
    public DialogueConversationIdentity Identity { get; }
    public DocumentVersionReference Document { get; }
    public KnowledgeSubject Subject { get; } = new("dialogue-owner", "ephemeral-stamp-never-persist");
    public KnowledgeDependencyHandle Root { get; }
    public DialogueTestDisclosure Disclosure { get; } = new();
    public DialogueTestDependencies Dependencies { get; } = new();
    public DialogueTestExecution Execution { get; } = new();
    public DialogueTestGuard Guard { get; } = new();
    public DialogueConversationOpenRequest OpenRequest => new(Identity.ConversationId, Identity.ContextId, new(Identity.Publication, Identity.Mode, Identity.RepositoryScope));
    public DialogueHistoryRequest HistoryRequest => new(Identity.ConversationId, Identity.ContextId, null);
    public Task<DialogueConversationResult> OpenAsync() => Store.OpenConversationAsync(OpenRequest, Subject);
    public async Task RequireOpenAsync() => Assert.Contains((await OpenAsync()).Status, new[] { DialogueStatus.Succeeded, DialogueStatus.Existing });
    public DialogueBeginRequest BeginRequest(Guid? key = null, DialogueHistoryResult? history = null)
    {
        var root = history?.Dependencies is { } previous ? Dependencies.Union([Root, previous]) : Root;
        var request = DialogueTestProtocol.Begin(Identity) with { ClientRequestKey = key ?? DialogueTestProtocol.Id(503), InitialDependencies = root,
            HistoryTurnIds = history?.Turns.Select(x => x.TurnId).ToImmutableArray() ?? [], HistoryProjectionDigest = history?.ProjectionDigest ?? DialogueTestProtocol.HistoryDigest([]) };
        return request with { RequestDigest = DialogueTestProtocol.BeginDigest(Identity, request) };
    }
    public async Task<DialogueAdmission> AdmitAsync(DialogueBeginRequest? request = null)
    {
        await RequireOpenAsync();
        var admitted = await Store.BeginTurnAsync(request ?? BeginRequest(), Subject);
        Assert.Equal(DialogueStatus.Succeeded, admitted.Status);
        Assert.True(admitted.MayDispatch);
        var admission = Assert.IsType<DialogueAdmission>(admitted.Admission);
        await MoveAsync(admission, DialogueTurnState.Running);
        Clock.Now = admission.AdmittedAtUtc.AddSeconds(2);
        return admission with { State = DialogueTurnState.Running };
    }
    public DialogueTerminalSaveRequest TerminalRequest(DialogueAdmission admission)
    {
        var request = DialogueTestProtocol.Terminal(admission) with { SaveOperationId = Guid.NewGuid(), GenerationEndedAtUtc = admission.AdmittedAtUtc.AddSeconds(1) };
        return request with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(request) };
    }
    public async Task RequireSaveAsync(DialogueTerminalSaveRequest request) => Assert.Equal(DialogueStatus.Succeeded, (await Store.SaveTerminalAsync(request, Subject)).Status);
    public async Task MoveAsync(DialogueAdmission admission, DialogueTurnState state)
    {
        var current = DialogueTurnState.Admitted;
        foreach (var next in new[] { DialogueTurnState.Running, DialogueTurnState.Saving, DialogueTurnState.SaveIndeterminate })
        {
            if (current == state) break;
            var result = await Store.TransitionTurnAsync(new(new(Identity.ConversationId, Identity.ContextId, admission.TurnId, admission.Request.Execution, admission.Fence), current, next), Subject);
            Assert.Equal(DialogueStatus.Succeeded, result.Status);
            Assert.Equal(next, result.State);
            current = next;
        }
    }
    public static async Task<DialoguePersistenceFixture> CreateAsync(DbContextOptions<OctoberDbContext>? options = null)
    {
        var knowledge = options is null ? await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync() : await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options);
        try
        {
            var accepted = (await knowledge.Writer().AcceptCompletedFileAsync(knowledge.Seal())).AcceptedFile!;
            var freeze = new CorpusFreezeRequest(new(knowledge.Binding.BatchId, OperationKind.FreezeCorpus, DialogueTestProtocol.Id(590)), knowledge.Binding, "", [accepted], [accepted.Coverage]);
            freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
            knowledge.FreezeAuthority.Approve(freeze);
            var corpus = (await knowledge.Writer().FreezeCorpusAsync(freeze)).Corpus!;
            var publication = new PublicationRequest(new(knowledge.Binding.BatchId, OperationKind.PublishSelection, DialogueTestProtocol.Id(591)), corpus, "", accepted.Documents, [], [], [], [accepted.Coverage]);
            publication = publication with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(publication) };
            var published = await knowledge.Writer().PublishSelectionAsync(publication);
            Assert.Equal(KnowledgeWriteStatus.Committed, published.Status);
            return new(knowledge, published.Publication!, accepted.Documents[0]);
        }
        catch { await knowledge.DisposeAsync(); throw; }
    }
    public ValueTask DisposeAsync() => _knowledge.DisposeAsync();
}

internal sealed class DialogueTestClock : TimeProvider { public DateTimeOffset Now { get; set; } = DialogueTestProtocol.Now; public override DateTimeOffset GetUtcNow() => Now; }
internal sealed class DialogueTestDisclosure : IOctoberKnowledgeDisclosurePolicy
{
    public DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
    public List<DisclosureRequest> Requests { get; } = [];
    public Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); lock (Requests) Requests.Add(request); return Task.FromResult(Status);
    }
}
internal sealed class DialogueTestExecution : IOctoberDialogueExecutionAuthority
{
    public Func<Task<DialogueExecutionStatus>>? AdmissionOverride { get; set; }
    public DialogueExecutionStatus AdmissionStatus { get; set; } = DialogueExecutionStatus.Verified;
    public DialogueExecutionStatus TerminalStatus { get; set; } = DialogueExecutionStatus.Verified;
    public DialogueExecutionStatus OwnerDeathStatus { get; set; } = DialogueExecutionStatus.NotVerified;
    public Task<DialogueExecutionStatus> VerifyAdmissionAsync(DialogueBeginRequest request, CancellationToken cancellationToken = default) => AdmissionOverride?.Invoke() ?? Task.FromResult(AdmissionStatus);
    public Task<DialogueExecutionStatus> VerifyTerminalAsync(DialogueAdmission admission, DialogueTerminalSaveRequest request, CancellationToken cancellationToken = default) => Task.FromResult(TerminalStatus);
    public Task<DialogueExecutionStatus> VerifyOwnerDeathAsync(DialogueExecutionIdentity execution, CancellationToken cancellationToken = default) => Task.FromResult(OwnerDeathStatus);
}
internal sealed class DialogueTestGuard : IOctoberDialogueCommitGuard
{
    public DialogueMutationKind? RefuseKind { get; set; }
    public int SubmissionCalls { get; private set; }
    public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(DialogueCommitAttempt attempt, CancellationToken cancellationToken = default) => Task.FromResult(CommitEligibilityStatus.Eligible);
    public async Task<CommitGuardResult> SubmitAsync(DialogueCommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
    {
        SubmissionCalls++;
        if (attempt.Kind == RefuseKind) return new(CommitSubmissionStatus.CancelledBeforeSubmission, null);
        var submitted = submitAsync(cancellationToken);
        return new(CommitSubmissionStatus.Submitted, await submitted.Settlement);
    }
}

/// <summary>Explicit trusted-owner double. Only registered exact capsules/proofs authorize; text labels represent full input origins.</summary>
internal sealed class DialogueTestDependencies : IOctoberKnowledgeDependencyAuthority
{
    private readonly Dictionary<string, (KnowledgeDependencyHandle Handle, ImmutableHashSet<string> Origins)> _capsules = [];
    private readonly Dictionary<Guid, KnowledgeConsumedContentReceipt> _proofs = [];
    public KnowledgeDependencyStatus Status { get; set; } = KnowledgeDependencyStatus.Succeeded;
    public HashSet<string> DeniedOrigins { get; } = [];
    public int VerifyCalls { get; private set; }
    public KnowledgeDependencyHandle Create(string origin) => Register([origin]);
    public KnowledgeDependencyHandle Extend(KnowledgeDependencyHandle prior, string origin) => Register(_capsules[prior.ProtectedPayload].Origins.Add(origin));
    public KnowledgeDependencyHandle Union(ImmutableArray<KnowledgeDependencyHandle> handles) => Register(handles.SelectMany(x => _capsules[x.ProtectedPayload].Origins));
    private KnowledgeDependencyHandle Register(IEnumerable<string> values)
    {
        var origins = values.ToImmutableHashSet(StringComparer.Ordinal);
        string digest = DialogueTestProtocol.Hash(string.Join("|", origins.Order(StringComparer.Ordinal)));
        var existing = _capsules.Values.FirstOrDefault(x => x.Handle.BindingDigest == digest);
        if (existing.Handle is not null) return existing.Handle;
        var handle = new KnowledgeDependencyHandle(Guid.NewGuid(), digest, "controlled-capsule:" + Guid.NewGuid().ToString("N"));
        _capsules.Add(handle.ProtectedPayload, (handle, origins));
        return handle;
    }
    private KnowledgeDependencyStatus Check(ImmutableArray<KnowledgeDependencyHandle> handles)
    {
        if (Status != KnowledgeDependencyStatus.Succeeded) return Status;
        if (handles.IsDefaultOrEmpty || handles.Any(x => x is null || !_capsules.TryGetValue(x.ProtectedPayload, out var entry) || entry.Handle != x)) return KnowledgeDependencyStatus.InvalidProof;
        return handles.Any(x => _capsules[x.ProtectedPayload].Origins.Overlaps(DeniedOrigins)) ? KnowledgeDependencyStatus.Denied : KnowledgeDependencyStatus.Succeeded;
    }
    public KnowledgeConsumedContentHandle Consume(OctoberChatReference reference, KnowledgeDependencyHandle dependencies)
    {
        var proof = new KnowledgeConsumedContentHandle(Guid.NewGuid(), DialogueTestProtocol.Hash(reference.ToString()), "controlled-content-proof:" + Guid.NewGuid().ToString("N"));
        _proofs.Add(proof.ReceiptId, new(proof, reference.Document, reference.IsPartial, reference.SuccessfulChunks, reference.ContentBytes, new(true, ["Controlled incomplete source coverage"]), dependencies));
        return proof;
    }
    public Task<KnowledgeDependencyResult> CreateNoKnowledgeRootAsync(ReadSelectionRequest selection, string initialInputDigest, CancellationToken cancellationToken = default) =>
        Task.FromResult(new KnowledgeDependencyResult(Status, Status == KnowledgeDependencyStatus.Succeeded ? Create(initialInputDigest) : null));
    public Task<KnowledgeDependencyResult> MergeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = Check(dependencies);
        return Task.FromResult(new KnowledgeDependencyResult(status, status == KnowledgeDependencyStatus.Succeeded ? Union(dependencies) : null));
    }
    public Task<KnowledgeDependencyStatus> ReauthorizeAsync(ReadSelectionRequest selection, ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Check(dependencies)); }
    public Task<KnowledgeConsumedContentResult> BuildConsumedContentAsync(ReadSelectionRequest selection, DocumentHandle document, ImmutableArray<KnowledgeDependencyHandle> admittedContentPages, CancellationToken cancellationToken = default) =>
        Task.FromResult(new KnowledgeConsumedContentResult(KnowledgeDependencyStatus.InvalidProof, null));
    public Task<KnowledgeConsumedContentResult> VerifyConsumedContentAsync(ReadSelectionRequest selection, KnowledgeConsumedContentHandle proof, KnowledgeDependencyHandle inferenceDependencies, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); VerifyCalls++;
        var status = Check([inferenceDependencies]);
        if (status != KnowledgeDependencyStatus.Succeeded) return Task.FromResult(new KnowledgeConsumedContentResult(status, null));
        if (!_proofs.TryGetValue(proof.ReceiptId, out var receipt) || receipt.Proof != proof ||
            !_capsules[inferenceDependencies.ProtectedPayload].Origins.IsSupersetOf(_capsules[receipt.Dependencies.ProtectedPayload].Origins))
            return Task.FromResult(new KnowledgeConsumedContentResult(KnowledgeDependencyStatus.InvalidProof, null));
        return Task.FromResult(new KnowledgeConsumedContentResult(KnowledgeDependencyStatus.Succeeded, receipt));
    }
}





/// <summary>Trusted controlled host enters its callback asynchronously, allowing caller cancellation during a provider synchronous prefix.</summary>
internal sealed class DialogueAsyncCallbackGuard : IOctoberDialogueCommitGuard
{
    public Task<CommitGuardResult>? Running { get; private set; }
    public bool CallbackReturned { get; private set; }
    public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(DialogueCommitAttempt attempt, CancellationToken cancellationToken = default) => Task.FromResult(CommitEligibilityStatus.Eligible);
    public Task<CommitGuardResult> SubmitAsync(DialogueCommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
    {
        Running = Task.Run(async () =>
        {
            var submitted = submitAsync(CancellationToken.None);
            CallbackReturned = true;
            return new CommitGuardResult(CommitSubmissionStatus.Submitted, await submitted.Settlement);
        });
        return Running;
    }
}

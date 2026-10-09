using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Web.Services.OctoberChat;

namespace Vulgata.Tests.October.Chat;

/// <summary>Actual registry, canonical SQLite store and SDK over controlled transport; policy/capsules are labelled owner doubles, not crypto or live-provider qualification.</summary>
public sealed class OctoberChatExecutionOwnershipTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Reservation_IsFiniteAndReleasesOnlyAfterExplicitEmptyOwnershipSealAsync()
    {
        Fixture f = await Fixture.CreateAsync();
        var first = Assert.IsType<OctoberChatExecutionRegistration>(f.Registry.TryReserve(f.Conversation));
        Assert.Equal(1, f.Registry.ActiveRegistrations);
        var other = f.Conversation with { ConversationId = Guid.NewGuid(), ContextId = Guid.NewGuid() };
        Assert.Null(f.Registry.TryReserve(other));
        Assert.False(first.Completion.IsCompleted);

        await f.Registry.SealAndWaitForSettlementAsync(first);

        Assert.True(first.Completion.IsCompletedSuccessfully);
        Assert.Equal(0, f.Registry.ActiveRegistrations);
        var next = Assert.IsType<OctoberChatExecutionRegistration>(f.Registry.TryReserve(other));
        Assert.NotEqual(first.Execution, next.Execution);
        await f.Registry.SealAndWaitForSettlementAsync(next);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BindRequest_ExactCodecBudgetProviderAndMintedExecutionBindOnceWithoutReopeningAsync()
    {
        Fixture f = await Fixture.CreateAsync();
        var registration = Assert.IsType<OctoberChatExecutionRegistration>(f.Registry.TryReserve(f.Conversation));
        DialogueBeginRequest request = f.Request(registration);
        try
        {
            Assert.True(f.Registry.BindRequest(registration, request, f.Budget, f.Providers, f.Snapshot, f.SaveId));
            Assert.Equal(DialogueExecutionStatus.Verified, await f.Registry.VerifyAdmissionAsync(request));
            Assert.False(f.Registry.BindRequest(registration, request, f.Budget, f.Providers, f.Snapshot, f.SaveId));
            DialogueBeginRequest changed = request with { Question = "changed question", Execution = new(Guid.NewGuid(), Guid.NewGuid()) };
            changed = changed with { RequestDigest = f.Codec.ComputeBeginDigest(f.Conversation, changed) };
            Assert.False(f.Registry.BindRequest(registration, changed, f.Budget, f.Providers, f.Snapshot, Guid.NewGuid()));
            Assert.NotEqual(DialogueExecutionStatus.Verified, await f.Registry.VerifyAdmissionAsync(changed));
        }
        finally { await f.Registry.SealAndWaitForSettlementAsync(registration); }
        Assert.NotEqual(DialogueExecutionStatus.Verified, await f.Registry.VerifyAdmissionAsync(request));
        Assert.False(f.Registry.BindRequest(registration, request, f.Budget, f.Providers, f.Snapshot, f.SaveId));
        Assert.Equal(0, f.Secret.DecryptCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Retain_ReadOnlyFaultedTaskIsFinishedWorkAndDoesNotInventCleanupFailureAsync()
    {
        Fixture f = await Fixture.CreateAsync();
        var registration = Assert.IsType<OctoberChatExecutionRegistration>(f.Registry.TryReserve(f.Conversation));
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Registry.Retain(registration, read.Task);
        Task settlement = f.Registry.SealAndWaitForSettlementAsync(registration);
        Assert.False(settlement.IsCompleted);
        Assert.Equal(1, f.Registry.ActiveRegistrations);

        read.SetException(new IOException("Controlled readonly failure after actual completion."));
        await settlement.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(read.Task.IsFaulted);
        Assert.True(registration.Completion.IsCompletedSuccessfully);
        Assert.Equal(0, f.Registry.ActiveRegistrations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartCleanup_ActualOwnedDisposalMustFinishBeforeReservationReleasesAsync()
    {
        Fixture f = await Fixture.CreateAsync();
        var registration = Assert.IsType<OctoberChatExecutionRegistration>(f.Registry.TryReserve(f.Conversation));
        var resource = new ControlledOwnedResource();
        Task disposal = f.Registry.StartCleanupAsync(registration, () => resource.DisposeAsync().AsTask());
        await resource.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task settlement = f.Registry.SealAndWaitForSettlementAsync(registration);
        try
        {
            Assert.False(disposal.IsCompleted);
            Assert.False(settlement.IsCompleted);
            Assert.False(resource.Released);
            Assert.Equal(1, f.Registry.ActiveRegistrations);
            Assert.Null(f.Registry.TryReserve(f.Conversation with { ConversationId = Guid.NewGuid() }));
        }
        finally { resource.Release.TrySetResult(); await disposal; await settlement; }
        Assert.True(resource.Released);
        Assert.Equal(1, resource.Calls);
        Assert.Equal(0, f.Registry.ActiveRegistrations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartCleanup_FailedActualCleanupCannotReleaseOrReuseReservationCapacityAsync()
    {
        Fixture f = await Fixture.CreateAsync();
        var registration = Assert.IsType<OctoberChatExecutionRegistration>(f.Registry.TryReserve(f.Conversation));
        Task cleanup = f.Registry.StartCleanupAsync(registration,
            () => Task.FromException(new IOException("Controlled resource release could not be confirmed.")));
        Assert.NotNull(await Record.ExceptionAsync(() => cleanup));

        Task settlement = f.Registry.SealAndWaitForSettlementAsync(registration);

        Assert.False(settlement.IsCompletedSuccessfully);
        Assert.False(registration.Completion.IsCompletedSuccessfully);
        Assert.Equal(1, f.Registry.ActiveRegistrations);
        Assert.Null(f.Registry.TryReserve(f.Conversation with { ConversationId = Guid.NewGuid(), ContextId = Guid.NewGuid() }));
        Assert.NotEqual(DialogueExecutionStatus.Verified, await f.Registry.VerifyOwnerDeathAsync(registration.Execution));
        // A failed or unresolved cleanup is intentionally not force-completed by fixture teardown.
        if (settlement.IsFaulted) _ = settlement.Exception;
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task StartTracked_ReentrantSealRetainsTaskReturnedAfterCallbackAndBlocksFurtherStartsAsync()
    {
        Fixture f = await Fixture.CreateAsync();
        var registration = Assert.IsType<OctoberChatExecutionRegistration>(f.Registry.TryReserve(f.Conversation));
        var operation = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? settlement = null;
        int starts = 0;
        Task<int> tracked = f.Registry.StartTrackedAsync(registration, () =>
        {
            starts++;
            settlement = f.Registry.SealAndWaitForSettlementAsync(registration);
            return operation.Task;
        });
        try
        {
            Assert.NotNull(settlement);
            Assert.False(settlement.IsCompleted);
            Assert.False(tracked.IsCompleted);
            await Assert.ThrowsAnyAsync<Exception>(() => f.Registry.StartTrackedAsync(registration, () =>
            { starts++; return Task.CompletedTask; }));
            Assert.Equal(1, starts);
            Assert.Equal(1, f.Registry.ActiveRegistrations);
        }
        finally { operation.TrySetResult(7); await tracked; if (settlement is not null) await settlement; }
        Assert.Equal(7, await tracked);
        Assert.Equal(0, f.Registry.ActiveRegistrations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task VerifyOwnerDeath_ReservationAgeAndCompletedLocalWorkDoNotProveDeathAsync()
    {
        Fixture f = await Fixture.CreateAsync();
        var registration = Assert.IsType<OctoberChatExecutionRegistration>(f.Registry.TryReserve(f.Conversation));
        Assert.NotEqual(DialogueExecutionStatus.Verified, await f.Registry.VerifyOwnerDeathAsync(registration.Execution));
        f.Clock.Advance(TimeSpan.FromHours(1));
        Assert.NotEqual(DialogueExecutionStatus.Verified, await f.Registry.VerifyOwnerDeathAsync(registration.Execution));
        await f.Registry.SealAndWaitForSettlementAsync(registration);
        Assert.NotEqual(DialogueExecutionStatus.Verified, await f.Registry.VerifyOwnerDeathAsync(registration.Execution));
        Assert.NotEqual(DialogueExecutionStatus.Verified, await f.Registry.VerifyOwnerDeathAsync(new(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GenerationStartup_PreSendCallbackCanWaitForExactTaskObservationBeforePhysicalDispatchAsync()
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        await h.RunningAsync();
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observationReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OctoberChatTurnRunner? runner = null;
        OctoberChatClientSession session = await h.F.Registry.CreateClientSessionAsync(h.Registration, h.Admitted,
            async token =>
            {
                callbackEntered.TrySetResult();
                await observationReady.Task.WaitAsync(token);
                await runner!.ReauthorizeBeforeSendAsync(token);
            }, h.Permits, h.Physical);
        Task<ReadSelectionRequest?> Fresh(CancellationToken _) => Task.FromResult<ReadSelectionRequest?>(new(
            h.Owner.Identity.Publication, h.Owner.Subject, h.Owner.Identity.Mode, KnowledgePurpose.Chat, h.Owner.Identity.RepositoryScope));
        runner = Assert.IsType<OctoberChatTurnRunner>(await OctoberChatTurnRunner.CreateAsync(h.Admitted, session.Client,
            h.F.Budget, new ReadSelection("controlled-owner-issued-selection"), h.History, h.Request.ProviderProfileId,
            h.Request.ProviderProfileRevision, new NoToolReader(), h.Owner.Dependencies, h.F.Codec, h.F.Registry, Fresh, h.F.SaveId));
        Task<OctoberChatGenerationResult> actualGeneration = runner.RunAsync((_, _) => ValueTask.CompletedTask);
        bool observed = false;
        try
        {
            // The actual task or callback decides ordering. The timeout is only a hung-test watchdog.
            Task first = await Task.WhenAny(actualGeneration, callbackEntered.Task).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, h.Physical.Sends);
            Assert.Same(callbackEntered.Task, first);
            Assert.False(actualGeneration.IsCompleted);

            h.F.Registry.ObserveGeneration(h.Registration, actualGeneration);
            observed = true;
            observationReady.TrySetResult();
            OctoberChatGenerationResult generated = await actualGeneration.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(DialogueTerminalState.Completed, generated.TerminalState);
            Assert.Equal("受控回答。", generated.AnswerText);
            Assert.Equal(1, h.Physical.Sends);
            await session.SealAndWaitForSettlementAsync();
            Assert.Equal(0, h.Permits.ActiveRequests);
            await h.SavingAsync();
            Assert.Equal(DialogueStatus.Succeeded, (await h.Observed.SaveTerminalAsync(h.Terminal(generated), h.Owner.Subject)).Status);
        }
        finally
        {
            if (!observed) h.F.Registry.ObserveGeneration(h.Registration, actualGeneration);
            observationReady.TrySetResult();
            await actualGeneration.WaitAsync(TimeSpan.FromSeconds(10));
            await session.SealAndWaitForSettlementAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerationStartup_CallbackReleaseWithoutObservationOrAfterSealCannotDispatchAsync(bool sealWhileCallbackPending)
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        await h.RunningAsync();
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OctoberChatTurnRunner? runner = null;
        OctoberChatClientSession session = await h.F.Registry.CreateClientSessionAsync(h.Registration, h.Admitted,
            async token =>
            {
                callbackEntered.TrySetResult();
                // Controlled noncooperative callback: its actual lifetime must remain owned after cancellation.
                await releaseCallback.Task;
                await runner!.ReauthorizeBeforeSendAsync(token);
            }, h.Permits, h.Physical);
        Task<ReadSelectionRequest?> Fresh(CancellationToken _) => Task.FromResult<ReadSelectionRequest?>(new(
            h.Owner.Identity.Publication, h.Owner.Subject, h.Owner.Identity.Mode, KnowledgePurpose.Chat, h.Owner.Identity.RepositoryScope));
        runner = Assert.IsType<OctoberChatTurnRunner>(await OctoberChatTurnRunner.CreateAsync(h.Admitted, session.Client,
            h.F.Budget, new ReadSelection("controlled-owner-issued-selection"), h.History, h.Request.ProviderProfileId,
            h.Request.ProviderProfileRevision, new NoToolReader(), h.Owner.Dependencies, h.F.Codec, h.F.Registry, Fresh, h.F.SaveId));
        Task<OctoberChatGenerationResult> actualGeneration = runner.RunAsync((_, _) => ValueTask.CompletedTask);
        bool observed = false;
        Task? drain = null;
        try
        {
            Task first = await Task.WhenAny(actualGeneration, callbackEntered.Task).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, h.Physical.Sends);
            Assert.Same(callbackEntered.Task, first);
            Assert.False(actualGeneration.IsCompleted);
            if (sealWhileCallbackPending)
            {
                h.F.Registry.ObserveGeneration(h.Registration, actualGeneration);
                observed = true;
                _ = h.SealRegistration();
                drain = session.SealAndWaitForSettlementAsync();
                Assert.False(drain.IsCompleted);
                Assert.Equal(0, h.Physical.Disposals);
                Assert.False(h.Registration.Completion.IsCompleted);
            }

            releaseCallback.TrySetResult();
            OctoberChatGenerationResult generated = await actualGeneration.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.NotEqual(DialogueTerminalState.Completed, generated.TerminalState);
            Assert.Equal(0, h.Physical.Sends);
            drain ??= session.SealAndWaitForSettlementAsync();
            await drain.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, h.Physical.Disposals);
            Assert.Equal(0, h.Permits.ActiveRequests);
            Assert.False(h.Registration.Completion.IsCompletedSuccessfully);
        }
        finally
        {
            // Register only the exact real task, never a placeholder or a fabricated settled generation.
            if (!observed) h.F.Registry.ObserveGeneration(h.Registration, actualGeneration);
            releaseCallback.TrySetResult();
            await actualGeneration.WaitAsync(TimeSpan.FromSeconds(10));
            await session.SealAndWaitForSettlementAsync();
            await h.Inner.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Terminal_ExactObservedGenerationRequiresActualPhysicalDrainAndRejectsRehashedForgedAnswerAsync()
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        await h.RunningAsync();
        OctoberChatGenerationResult generated = await h.GenerateAsync(holdDisposal: true);
        DialogueTerminalSaveRequest exact = h.Terminal(generated);
        Task drain = h.Session!.SealAndWaitForSettlementAsync();
        try
        {
            await h.Physical.DisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(drain.IsCompleted);
            Assert.NotEqual(DialogueExecutionStatus.Verified, await h.F.Registry.VerifyTerminalAsync(h.Admitted.Admission!, exact));
        }
        finally { h.Physical.ReleaseDispose.TrySetResult(); await drain; }
        await h.SavingAsync();
        DialogueTerminalSaveRequest forged = exact with { AnswerText = "invented content absent from the actual generation" };
        forged = forged with { OutcomeDigest = h.F.Codec.ComputeOutcomeDigest(forged) };

        Assert.NotEqual(DialogueExecutionStatus.Verified, await h.F.Registry.VerifyTerminalAsync(h.Admitted.Admission!, forged));
        Assert.Equal(DialogueExecutionStatus.Verified, await h.F.Registry.VerifyTerminalAsync(h.Admitted.Admission!, exact));
        DialogueTerminalResult saved = await h.Observed.SaveTerminalAsync(exact, h.Owner.Subject).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(DialogueStatus.Succeeded, saved.Status);
        Assert.Equal(exact.AnswerText, saved.Projection!.AnswerText);
        Assert.Equal(1, h.Physical.Sends);
        Assert.Equal(0, h.Permits.ActiveRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(CommitEligibilityStatus.Denied, KnowledgeDependencyStatus.Denied, DialogueTerminalReason.AuthorizationDenied)]
    [InlineData(CommitEligibilityStatus.AuthorityUnavailable, KnowledgeDependencyStatus.AuthorityUnavailable, DialogueTerminalReason.AuthorityUnavailable)]
    public async Task Terminal_SanitizedClosureRequiresFreshOriginalDependencyRefusalAsync(CommitEligibilityStatus authority,
        KnowledgeDependencyStatus dependencies, DialogueTerminalReason reason)
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        await h.RunningAsync();
        OctoberChatGenerationResult generated = await h.GenerateAsync();
        await h.Session!.SealAndWaitForSettlementAsync();
        await h.SavingAsync();
        DialogueTerminalSaveRequest sanitized = h.Terminal(generated) with { TerminalState = DialogueTerminalState.Failed,
            Reason = reason, AnswerText = string.Empty, References = [], ConsumedReads = [], InferenceDependencies = null };
        sanitized = sanitized with { OutcomeDigest = h.F.Codec.ComputeOutcomeDigest(sanitized) };
        Assert.NotEqual(DialogueExecutionStatus.Verified, await h.F.Registry.VerifyTerminalAsync(h.Admitted.Admission!, sanitized));
        h.F.AuthorityStatus = authority;
        h.Owner.Dependencies.Status = dependencies;

        Assert.Equal(DialogueExecutionStatus.Verified, await h.F.Registry.VerifyTerminalAsync(h.Admitted.Admission!, sanitized));
        Assert.Contains(h.F.AuthorityCalls, call => call.Dependencies == generated.InferenceDependencies);
        DialogueTerminalResult saved = await h.Observed.SaveTerminalAsync(sanitized, h.Owner.Subject);
        Assert.Equal(DialogueStatus.Succeeded, saved.Status);
        Assert.Equal(reason, saved.Receipt!.Reason);
        Assert.Null(saved.Projection);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Terminal_ProvablyNotStartedCanCloseFailedButNeverClaimCompletedAsync()
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        await h.RunningAsync();
        await h.SavingAsync();
        DialogueTerminalSaveRequest failed = h.NotStarted();
        var completed = failed with { TerminalState = DialogueTerminalState.Completed, Reason = DialogueTerminalReason.None,
            ProviderFinish = DialogueProviderFinish.NotStarted, AnswerText = "no generation produced this" };
        completed = completed with { OutcomeDigest = h.F.Codec.ComputeOutcomeDigest(completed) };

        Assert.NotEqual(DialogueExecutionStatus.Verified, await h.F.Registry.VerifyTerminalAsync(h.Admitted.Admission!, completed));
        Assert.Equal(DialogueExecutionStatus.Verified, await h.F.Registry.VerifyTerminalAsync(h.Admitted.Admission!, failed));
        Assert.Equal(DialogueStatus.Succeeded, (await h.Observed.SaveTerminalAsync(failed, h.Owner.Subject)).Status);
        Assert.Equal(0, h.Physical.Sends);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(CommitEligibilityStatus.Denied, KnowledgeDependencyStatus.Denied, DialogueTerminalReason.AuthorizationDenied, true)]
    [InlineData(CommitEligibilityStatus.AuthorityUnavailable, KnowledgeDependencyStatus.AuthorityUnavailable, DialogueTerminalReason.AuthorityUnavailable, true)]
    [InlineData(CommitEligibilityStatus.Eligible, KnowledgeDependencyStatus.Succeeded, DialogueTerminalReason.AuthorizationDenied, false)]
    public async Task Terminal_NotStartedSanitizedClosureRequiresMatchingFreshOriginalDependencyRefusalAsync(
        CommitEligibilityStatus authority, KnowledgeDependencyStatus dependencies, DialogueTerminalReason reason, bool permitted)
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        await h.RunningAsync();
        await h.SavingAsync();
        DialogueTerminalSaveRequest sanitized = h.NotStarted() with { Reason = reason, InferenceDependencies = null };
        sanitized = sanitized with { OutcomeDigest = h.F.Codec.ComputeOutcomeDigest(sanitized) };
        h.F.AuthorityStatus = authority;
        h.Owner.Dependencies.Status = dependencies;
        h.F.AuthorityCalls.Clear();

        DialogueExecutionStatus verified = await h.F.Registry.VerifyTerminalAsync(h.Admitted.Admission!, sanitized);

        Assert.Equal(permitted, verified == DialogueExecutionStatus.Verified);
        if (permitted)
        {
            Assert.Contains(h.F.AuthorityCalls, call => call.Dependencies == h.Request.InitialDependencies);
            DialogueTerminalResult saved = await h.Observed.SaveTerminalAsync(sanitized, h.Owner.Subject);
            Assert.Equal(DialogueStatus.Succeeded, saved.Status);
            Assert.Equal(DialogueTerminalState.Failed, saved.Receipt!.TerminalState);
            Assert.Equal(reason, saved.Receipt.Reason);
            Assert.Null(saved.Projection);
        }
        Assert.Equal(0, h.Physical.Sends);
        Assert.Equal(0, h.F.Secret.DecryptCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ObservedLookup_ConfirmsLostRunningPhaseWithoutGrantingDispatchAndAllowsFirstUnstartedClosureAsync()
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        h.Inner.LoseNextTransitionAcknowledgement = true;
        DialogueTransitionResult uncertain = await h.Observed.TransitionTurnAsync(new(h.Ownership,
            DialogueTurnState.Admitted, DialogueTurnState.Running), h.Owner.Subject);
        Assert.Equal(DialogueStatus.Indeterminate, uncertain.Status);
        Assert.Null(uncertain.State);
        DialogueBeginResult found = await h.Observed.LookupAdmissionAsync(new(h.Request.ConversationId,
            h.Request.ContextId, h.Request.ClientRequestKey, h.Request.RequestDigest), h.Owner.Subject);
        Assert.Equal(DialogueStatus.Existing, found.Status);
        Assert.Equal(DialogueTurnState.Running, found.Admission!.State);
        Assert.False(found.MayDispatch);
        using var refusedTransport = new ControlledTransport();
        await Assert.ThrowsAnyAsync<Exception>(() => h.F.Registry.CreateClientSessionAsync(h.Registration, found,
            _ => Task.CompletedTask, h.Permits, refusedTransport));

        await h.SavingAsync();
        DialogueTerminalResult saved = await h.Observed.SaveTerminalAsync(h.NotStarted(), h.Owner.Subject).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(DialogueStatus.Succeeded, saved.Status);
        Assert.Equal(DialogueTerminalState.Failed, saved.Receipt!.TerminalState);
        Assert.Equal(0, refusedTransport.Sends);
        Assert.Equal(1, h.Inner.SaveCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("subject")]
    [InlineData("payload")]
    public async Task ObservedTransition_AlteredActualOwnerAttemptCannotCommitUnderArmedCallAsync(string changed)
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        h.Guard.Change = attempt => attempt.Kind != DialogueMutationKind.TransitionTurn ? attempt : changed == "subject"
            ? attempt with { Subject = attempt.Subject! with { AuthenticationBinding = "different-stamp" } }
            : attempt with { PayloadDigest = new string('0', 64) };

        DialogueTransitionResult result = await h.Observed.TransitionTurnAsync(new(h.Ownership,
            DialogueTurnState.Admitted, DialogueTurnState.Running), h.Owner.Subject);

        Assert.NotEqual(DialogueStatus.Succeeded, result.Status);
        DialogueBeginResult actual = await h.Observed.LookupAdmissionAsync(new(h.Request.ConversationId, h.Request.ContextId,
            h.Request.ClientRequestKey, h.Request.RequestDigest), h.Owner.Subject);
        Assert.Equal(DialogueTurnState.Admitted, actual.Admission!.State);
        Assert.Equal(0, h.Guard.TransitionSubmissions);
        Assert.Equal(0, h.Physical.Sends);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Factory_SealDuringPendingAuthorityPreventsLaterCredentialAcquisitionAsync()
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        await h.RunningAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.F.BeforeAuthority = async () => { entered.TrySetResult(); await release.Task; };
        Task<OctoberChatClientSession> factory = h.F.Registry.CreateClientSessionAsync(h.Registration, h.Admitted,
            _ => Task.CompletedTask, h.Permits, h.Physical);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await h.StartOwnerCleanupAsync();
        Task seal = h.SealRegistration();
        try
        {
            Assert.False(factory.IsCompleted);
            Assert.False(seal.IsCompleted);
            Assert.Equal(0, h.F.Secret.DecryptCalls);
        }
        finally { release.TrySetResult(); }
        Assert.NotNull(await Record.ExceptionAsync(() => factory.WaitAsync(TimeSpan.FromSeconds(10))));
        Assert.Equal(0, h.F.Secret.DecryptCalls);
        Assert.Equal(0, h.Physical.Sends);
        Assert.False(h.Registration.Completion.IsCompletedSuccessfully);
        Assert.Equal(1, h.F.Registry.ActiveRegistrations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Factory_AlreadyOwnedCredentialTaskIgnoringCancellationRemainsOwnedThroughSealAsync()
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        await h.RunningAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.F.Secret.BeforeDecrypt = async () => { entered.TrySetResult(); await release.Task; };
        Task<OctoberChatClientSession> factory = h.F.Registry.CreateClientSessionAsync(h.Registration, h.Admitted,
            _ => Task.CompletedTask, h.Permits, h.Physical);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await h.StartOwnerCleanupAsync();
        Task seal = h.SealRegistration();
        try
        {
            Assert.False(factory.IsCompleted);
            Assert.False(seal.IsCompleted);
            Assert.Equal(1, h.F.Secret.DecryptCalls);
            Assert.Equal(0, h.Physical.Disposals);
        }
        finally { release.TrySetResult(); }
        Assert.NotNull(await Record.ExceptionAsync(() => factory.WaitAsync(TimeSpan.FromSeconds(10))));
        await h.Physical.DisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, h.Physical.Sends);
        Assert.Equal(1, h.Physical.Disposals);
        Assert.False(h.Registration.Completion.IsCompletedSuccessfully);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task RegistrySeal_DirectlyDrainsCreatedSessionWithoutGenerationAndRetainsNonterminalOwnershipAsync()
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        await h.RunningAsync();
        OctoberChatClientSession session = await h.F.Registry.CreateClientSessionAsync(h.Registration, h.Admitted,
            _ => Task.CompletedTask, h.Permits, h.Physical);
        h.Physical.HoldDispose = true;
        await h.StartOwnerCleanupAsync();
        Task seal = h.SealRegistration();
        try
        {
            await h.Physical.DisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(seal.IsCompleted);
            Assert.False(h.Registration.Completion.IsCompleted);
            Assert.Equal(0, h.Physical.Sends);
        }
        finally { h.Physical.ReleaseDispose.TrySetResult(); await session.SealAndWaitForSettlementAsync(); }
        Assert.Equal(1, h.Physical.Disposals);
        Assert.Equal(0, h.Permits.ActiveRequests);
        Assert.False(h.Registration.Completion.IsCompletedSuccessfully);
        Assert.Equal(1, h.F.Registry.ActiveRegistrations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegistrySeal_PhysicalSettlementWithoutExactTerminalReceiptCannotReleaseOwnAdmissionAsync(bool loseBeginAcknowledgement)
    {
        await using StoreFixture h = await StoreFixture.OpenAsync(loseBeginAcknowledgement);
        await h.StartOwnerCleanupAsync();

        Task seal = h.SealRegistration();

        Assert.False(seal.IsCompletedSuccessfully);
        Assert.False(h.Registration.Completion.IsCompletedSuccessfully);
        Assert.Equal(1, h.F.Registry.ActiveRegistrations);
        Assert.Null(h.F.Registry.TryReserve(h.Owner.Identity with { ConversationId = Guid.NewGuid(), ContextId = Guid.NewGuid() }));
        Assert.Equal(0, h.Physical.Sends);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ScopeCleanup_WaitsForOwnedReadSessionAndCanonicalStoreThenActualFinalResourceReleaseAsync()
    {
        await using StoreFixture h = await StoreFixture.OpenAsync();
        await h.RunningAsync();
        OctoberChatGenerationResult generated = await h.GenerateAsync();
        Task physicalDrain = h.Session!.SealAndWaitForSettlementAsync();
        await physicalDrain;
        await h.SavingAsync();
        Assert.Equal(DialogueStatus.Succeeded, (await h.Observed.SaveTerminalAsync(h.Terminal(generated), h.Owner.Subject)).Status);
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resource = new ControlledOwnedResource();
        h.F.Registry.Retain(h.Registration, read.Task);
        h.F.Registry.RegisterScopeCleanup(h.Registration, () =>
        {
            Assert.True(read.Task.IsCompleted);
            Assert.True(physicalDrain.IsCompletedSuccessfully);
            Assert.True(h.Inner.DisposalSettled);
            Assert.Equal(0, h.Permits.ActiveRequests);
            return resource.DisposeAsync().AsTask();
        });

        Task seal = h.SealRegistration();
        try
        {
            Assert.Equal(0, resource.Calls);
            Assert.False(seal.IsCompleted);
            read.TrySetResult();
            await resource.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(h.Inner.DisposalSettled);
            Assert.False(resource.Released);
            Assert.False(seal.IsCompleted);
            Assert.Equal(1, h.F.Registry.ActiveRegistrations);
            Assert.Null(h.F.Registry.TryReserve(h.Owner.Identity with { ConversationId = Guid.NewGuid() }));
        }
        finally { read.TrySetResult(); resource.Release.TrySetResult(); await seal.WaitAsync(TimeSpan.FromSeconds(10)); }
        Assert.True(resource.Released);
        Assert.Equal(1, resource.Calls);
        Assert.True(h.Registration.Completion.IsCompletedSuccessfully);
        Assert.Equal(0, h.F.Registry.ActiveRegistrations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ScopeCleanup_FaultedFinalResourceCannotReleaseCapacityAsync()
    {
        Fixture f = await Fixture.CreateAsync();
        var registration = Assert.IsType<OctoberChatExecutionRegistration>(f.Registry.TryReserve(f.Conversation));
        var resource = new ControlledOwnedResource { FailRelease = true };
        f.Registry.RegisterScopeCleanup(registration, () => resource.DisposeAsync().AsTask());
        Task seal = f.Registry.SealAndWaitForSettlementAsync(registration);
        await resource.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(seal.IsCompleted);
        Assert.Equal(1, f.Registry.ActiveRegistrations);
        resource.Release.TrySetResult();

        await Assert.ThrowsAsync<IOException>(() => seal.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.False(registration.Completion.IsCompletedSuccessfully);
        Assert.False(resource.Released);
        Assert.Equal(1, resource.Calls);
        Assert.Equal(1, f.Registry.ActiveRegistrations);
        Assert.Null(f.Registry.TryReserve(f.Conversation with { ConversationId = Guid.NewGuid() }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ScopeCleanup_SettledOwnerDisposalFaultStillAwaitsFinalResourceAndRetainsFailedCapacityAsync()
    {
        StoreFixture h = await StoreFixture.OpenAsync();
        var resource = new ControlledOwnedResource();
        Task? seal = null;
        try
        {
            await h.RunningAsync();
            await h.SavingAsync();
            Assert.Equal(DialogueStatus.Succeeded, (await h.Observed.SaveTerminalAsync(h.NotStarted(), h.Owner.Subject)).Status);
            // The wrapper reports a controlled failure only AFTER actual canonical disposal settles.
            // This qualifies registry classification, not a claim that SQLite disposal itself failed.
            h.Inner.FailAfterActualDisposal = true;
            h.F.Registry.RegisterScopeCleanup(h.Registration, () =>
            {
                Assert.True(h.Inner.DisposalSettled);
                return resource.DisposeAsync().AsTask();
            });

            seal = h.SealRegistration();
            await resource.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(h.Inner.DisposalSettled);
            Assert.False(resource.Released);
            Assert.False(seal.IsCompleted);
            Assert.False(h.Registration.Completion.IsCompleted);
            Assert.Equal(1, h.F.Registry.ActiveRegistrations);
            resource.Release.TrySetResult();
            await Assert.ThrowsAsync<IOException>(() => seal.WaitAsync(TimeSpan.FromSeconds(10)));

            Assert.True(resource.Released);
            Assert.Equal(1, resource.Calls);
            Assert.Same(seal, h.SealRegistration());
            Assert.False(h.Registration.Completion.IsCompletedSuccessfully);
            Assert.Equal(1, h.F.Registry.ActiveRegistrations);
            Assert.Null(h.F.Registry.TryReserve(h.Owner.Identity with { ConversationId = Guid.NewGuid() }));
        }
        finally
        {
            resource.Release.TrySetResult();
            // Observe the deliberately faulted wrapper; the actual owner disposal was awaited inside it.
            _ = await Record.ExceptionAsync(() => h.Inner.DisposeAsync().AsTask());
            if (seal?.IsFaulted == true) _ = seal.Exception;
            await h.Owner.DisposeAsync();
        }
    }

    private sealed class StoreFixture : IAsyncDisposable
    {
        public DialoguePersistenceFixture Owner { get; private set; } = null!;
        public Fixture F { get; private set; } = null!;
        public OctoberChatExecutionRegistration Registration { get; private set; } = null!;
        public GuardProbe Guard { get; private set; } = null!;
        public ObservedInner Inner { get; private set; } = null!;
        public OctoberChatObservedDialogueStore Observed { get; private set; } = null!;
        public DialogueBeginRequest Request { get; private set; } = null!;
        public DialogueBeginResult Admitted { get; private set; } = null!;
        public DialogueHistoryResult History { get; private set; } = null!;
        public OctoberChatClientSession? Session { get; private set; }
        public OctoberChatRequestAdmission Permits { get; } = new(1);
        public ControlledTransport Physical { get; } = new();
        private Task? _cleanup;
        private bool _sealed;
        public DialogueTurnOwnership Ownership => new(Request.ConversationId, Request.ContextId,
            Admitted.Admission!.TurnId, Request.Execution, Admitted.Admission.Fence);
        public static async Task<StoreFixture> OpenAsync(bool loseBeginAcknowledgement = false)
        {
            var h = new StoreFixture { Owner = await DialoguePersistenceFixture.CreateAsync() };
            try
            {
                h.F = await Fixture.CreateAsync(h.Owner.Identity);
                h.Registration = Assert.IsType<OctoberChatExecutionRegistration>(h.F.Registry.TryReserve(h.Owner.Identity));
                h.Guard = new(h.F.Registry.CommitGuard);
                IOctoberDialogueStore canonical = new OctoberDialogueStore(h.Owner.Factory, h.Owner.Identity.Publication.Knowledge,
                    h.Owner.Disclosure, h.Owner.Dependencies, h.F.Registry, h.Guard, h.F.Codec, h.F.Clock);
                // This is a real owner prerequisite, not a completed-task substitute for deferred database cleanup.
                IAsyncDisposable cleanup = Assert.IsAssignableFrom<IAsyncDisposable>(canonical);
                h.Inner = new(canonical, cleanup);
                h.Observed = new(h.Inner, h.F.Registry, h.Registration);
                Assert.Equal(DialogueStatus.Succeeded, (await h.Observed.OpenConversationAsync(h.Owner.OpenRequest, h.Owner.Subject)).Status);
                h.History = await h.Observed.ReadHistoryAsync(h.Owner.HistoryRequest, h.Owner.Subject);
                Assert.Equal(DialogueStatus.Succeeded, h.History.Status);
                string prompt = Assert.IsType<string>(await new OctoberChatPromptCatalog().LoadAsync(h.Owner.Identity.Mode));
                h.Request = h.Owner.BeginRequest(Guid.NewGuid(), h.History) with
                {
                    Execution = h.Registration.Execution, ProviderProfileId = h.F.Snapshot.ProviderId.ToString("D"),
                    ProviderProfileRevision = h.F.Snapshot.Revision, Limits = h.F.Budget.Limits,
                    PromptDigest = OctoberKnowledgePolicy.Hash(prompt)
                };
                h.Request = h.Request with { RequestDigest = h.F.Codec.ComputeBeginDigest(h.Owner.Identity, h.Request) };
                Assert.True(h.F.Registry.BindRequest(h.Registration, h.Request, h.F.Budget, h.F.Providers, h.F.Snapshot, h.F.SaveId));
                h.Inner.LoseBeginAcknowledgement = loseBeginAcknowledgement;
                h.Admitted = await h.Observed.BeginTurnAsync(h.Request, h.Owner.Subject);
                Assert.Equal(loseBeginAcknowledgement ? DialogueStatus.Indeterminate : DialogueStatus.Succeeded, h.Admitted.Status);
                Assert.Equal(!loseBeginAcknowledgement, h.Admitted.MayDispatch);
                return h;
            }
            catch
            {
                if (h.Inner is not null) await h.Inner.DisposeAsync();
                await h.Owner.DisposeAsync();
                throw;
            }
        }
        public async Task RunningAsync() => Assert.Equal(DialogueStatus.Succeeded,
            (await Observed.TransitionTurnAsync(new(Ownership, DialogueTurnState.Admitted, DialogueTurnState.Running), Owner.Subject)).Status);
        public async Task SavingAsync() => Assert.Equal(DialogueStatus.Succeeded,
            (await Observed.TransitionTurnAsync(new(Ownership, DialogueTurnState.Running, DialogueTurnState.Saving), Owner.Subject)).Status);
        public async Task<OctoberChatGenerationResult> GenerateAsync(bool holdDisposal = false)
        {
            Physical.HoldDispose = holdDisposal;
            OctoberChatTurnRunner? runner = null;
            Session = await F.Registry.CreateClientSessionAsync(Registration, Admitted,
                token => runner!.ReauthorizeBeforeSendAsync(token), Permits, Physical);
            var runGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool gateRun = false;
            async Task<ReadSelectionRequest?> Fresh(CancellationToken token)
            {
                if (gateRun) await runGate.Task.WaitAsync(token);
                return new(Owner.Identity.Publication, Owner.Subject, Owner.Identity.Mode, KnowledgePurpose.Chat, Owner.Identity.RepositoryScope);
            }
            runner = Assert.IsType<OctoberChatTurnRunner>(await OctoberChatTurnRunner.CreateAsync(Admitted, Session.Client,
                F.Budget, new ReadSelection("controlled-owner-issued-selection"), History, Request.ProviderProfileId,
                Request.ProviderProfileRevision, new NoToolReader(), Owner.Dependencies, F.Codec, F.Registry, Fresh, F.SaveId));
            gateRun = true;
            Task<OctoberChatGenerationResult> actual = runner.RunAsync((_, _) => ValueTask.CompletedTask);
            F.Registry.ObserveGeneration(Registration, actual);
            Assert.Equal(0, Physical.Sends);
            runGate.TrySetResult();
            OctoberChatGenerationResult result = await actual.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(DialogueTerminalState.Completed, result.TerminalState);
            Assert.Equal("受控回答。", result.AnswerText);
            return result;
        }
        public DialogueTerminalSaveRequest Terminal(OctoberChatGenerationResult generated)
        {
            var request = new DialogueTerminalSaveRequest(Ownership, generated.SaveOperationId, "", generated.TerminalState,
                generated.Reason, generated.ProviderFinish, generated.AnswerText, generated.References, generated.ConsumedReads,
                generated.InferenceDependencies, Request.HistoryTurnIds, generated.GenerationEndedAtUtc);
            return request with { OutcomeDigest = F.Codec.ComputeOutcomeDigest(request) };
        }
        public DialogueTerminalSaveRequest NotStarted()
        {
            var request = new DialogueTerminalSaveRequest(Ownership, F.SaveId, "", DialogueTerminalState.Failed,
                DialogueTerminalReason.ExecutionFailure, DialogueProviderFinish.NotStarted, "", [], [],
                Request.InitialDependencies, Request.HistoryTurnIds, F.Clock.GetUtcNow());
            return request with { OutcomeDigest = F.Codec.ComputeOutcomeDigest(request) };
        }
        public Task StartOwnerCleanupAsync() => _cleanup ??= F.Registry.StartCleanupAsync(Registration, () => Inner.DisposeAsync().AsTask());
        public Task SealRegistration()
        { _sealed = true; return F.Registry.SealAndWaitForSettlementAsync(Registration); }
        public async ValueTask DisposeAsync()
        {
            Physical.ReleaseDispose.TrySetResult();
            if (Session is not null) await Session.SealAndWaitForSettlementAsync();
            if (_cleanup is null && !_sealed) _cleanup = F.Registry.StartCleanupAsync(Registration, () => Inner.DisposeAsync().AsTask());
            if (_cleanup is not null) await _cleanup;
            // Deliberately nonterminal cases retain durable ownership; settlement cannot fabricate an idle conversation.
            Task closed = F.Registry.SealAndWaitForSettlementAsync(Registration);
            if (closed.IsCompleted) await closed;
            await Owner.DisposeAsync();
        }
    }
    private sealed class GuardProbe(IOctoberDialogueCommitGuard inner) : IOctoberDialogueCommitGuard
    {
        public Func<DialogueCommitAttempt, DialogueCommitAttempt>? Change { get; set; }
        public int TransitionSubmissions { get; private set; }
        public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(DialogueCommitAttempt attempt, CancellationToken token = default) =>
            inner.CheckBeforeTransactionAsync(Change?.Invoke(attempt) ?? attempt, token);
        public Task<CommitGuardResult> SubmitAsync(DialogueCommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submit,
            CancellationToken token = default) => inner.SubmitAsync(Change?.Invoke(attempt) ?? attempt, submittedToken =>
            {
                if (attempt.Kind == DialogueMutationKind.TransitionTurn) TransitionSubmissions++;
                return submit(submittedToken);
            }, token);
    }
    /// <summary>Delegates every operation to the actual canonical store; only hides an acknowledgement after the actual phase commit.</summary>
    private sealed class ObservedInner(IOctoberDialogueStore inner, IAsyncDisposable cleanup) : IOctoberDialogueStore, IAsyncDisposable
    {
        private Task? _disposal;
        public bool DisposalSettled { get; private set; }
        public bool FailAfterActualDisposal { get; set; }
        public bool LoseNextTransitionAcknowledgement { get; set; }
        public bool LoseBeginAcknowledgement { get; set; }
        public int SaveCalls { get; private set; }
        public Task<DialogueConversationResult> OpenConversationAsync(DialogueConversationOpenRequest request, KnowledgeSubject subject, CancellationToken token = default) => inner.OpenConversationAsync(request, subject, token);
        public async Task<DialogueBeginResult> BeginTurnAsync(DialogueBeginRequest request, KnowledgeSubject subject, CancellationToken token = default)
        {
            var actual = await inner.BeginTurnAsync(request, subject, token);
            return LoseBeginAcknowledgement && actual.Status == DialogueStatus.Succeeded
                ? new(DialogueStatus.Indeterminate, null, false) : actual;
        }
        public Task<DialogueBeginResult> LookupAdmissionAsync(DialogueAdmissionLookup request, KnowledgeSubject subject, CancellationToken token = default) => inner.LookupAdmissionAsync(request, subject, token);
        public async Task<DialogueTransitionResult> TransitionTurnAsync(DialogueTransitionRequest request, KnowledgeSubject subject, CancellationToken token = default)
        {
            var result = await inner.TransitionTurnAsync(request, subject, token);
            if (LoseNextTransitionAcknowledgement && result.Status == DialogueStatus.Succeeded)
            { LoseNextTransitionAcknowledgement = false; return new(DialogueStatus.Indeterminate, null); }
            return result;
        }
        public Task<DialogueTerminalResult> SaveTerminalAsync(DialogueTerminalSaveRequest request, KnowledgeSubject subject, CancellationToken token = default)
        { SaveCalls++; return inner.SaveTerminalAsync(request, subject, token); }
        public Task<DialogueTerminalResult> LookupTerminalAsync(DialogueTerminalLookup request, KnowledgeSubject subject, CancellationToken token = default) => inner.LookupTerminalAsync(request, subject, token);
        public Task<DialogueHistoryResult> ReadHistoryAsync(DialogueHistoryRequest request, KnowledgeSubject subject, CancellationToken token = default) => inner.ReadHistoryAsync(request, subject, token);
        public Task<DialogueTranscriptResult> ReadTranscriptAsync(DialogueHistoryRequest request, KnowledgeSubject subject, CancellationToken token = default) => inner.ReadTranscriptAsync(request, subject, token);
        public Task<DialogueOrphanResult> InterruptOrphanAsync(DialogueOrphanRequest request, CancellationToken token = default) => inner.InterruptOrphanAsync(request, token);
        public ValueTask DisposeAsync() => new(_disposal ??= DisposeOwnerAsync());
        private async Task DisposeOwnerAsync()
        {
            await cleanup.DisposeAsync(); DisposalSettled = true;
            if (FailAfterActualDisposal) throw new IOException("Controlled wrapper failure after actual canonical disposal settled.");
        }
    }
    private sealed class NoToolReader : IOctoberKnowledgeDisclosedReader
    {
        public Task<KnowledgeDisclosureReadResult<SearchPage>> SearchAsync(KnowledgeSearchRequest request, KnowledgeReadOutputBudget budget, CancellationToken token = default) => throw new InvalidOperationException("No tool scripted.");
        public Task<KnowledgeDisclosureReadResult<ContentPage>> ReadExactAsync(ExactReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken token = default) => throw new InvalidOperationException("No tool scripted.");
        public Task<KnowledgeDisclosureReadResult<RelatedPage>> GetRelatedAsync(RelatedReadRequest request, KnowledgeReadOutputBudget budget, CancellationToken token = default) => throw new InvalidOperationException("No tool scripted.");
    }
    private sealed class ControlledTransport : HttpMessageHandler
    {
        public int Sends { get; private set; }
        public int Disposals { get; private set; }
        public bool HoldDispose { get; set; }
        public TaskCompletionSource DisposeEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDispose { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Sends++;
            const string events = "data: {\"id\":\"controlled\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"controlled-model\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"受控回答。\"},\"finish_reason\":null}]}\n\n" +
                "data: {\"id\":\"controlled\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"controlled-model\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(events))) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(response);
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
    private sealed class Fixture
    {
        public OctoberDialoguePayloadCodec Codec { get; } = new();
        public ControlledChatFixture.ManualClock Clock { get; } = new();
        public OctoberChatExecutionRegistry Registry { get; private set; } = null!;
        public OctoberChatBudget Budget { get; private set; } = null!;
        public OctoberChatProviderBindingService Providers { get; private set; } = null!;
        public OctoberChatProviderSnapshot Snapshot { get; private set; } = null!;
        public ControlledSecret Secret { get; } = new();
        public Guid SaveId { get; } = Guid.NewGuid();
        public CommitEligibilityStatus AuthorityStatus { get; set; } = CommitEligibilityStatus.Eligible;
        public Func<Task>? BeforeAuthority { get; set; }
        public List<DialogueCommitAttempt> AuthorityCalls { get; } = [];
        public DialogueConversationIdentity Conversation { get; private set; } = new(Guid.NewGuid(), Guid.NewGuid(), "controlled-owner",
            new(new("controlled-database", "controlled-target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid()),
            KnowledgeMode.IT, [Guid.NewGuid()]);
        public static async Task<Fixture> CreateAsync(DialogueConversationIdentity? conversation = null)
        {
            var f = new Fixture();
            if (conversation is not null) f.Conversation = conversation;
            f.Registry = new(f.Codec, async (attempt, _) =>
            {
                f.AuthorityCalls.Add(attempt);
                if (f.BeforeAuthority is not null) await f.BeforeAuthority();
                return attempt.Dependencies is null ? CommitEligibilityStatus.Eligible : f.AuthorityStatus;
            }, f.Clock, maximumRegistrations: 1);
            f.Budget = new(timeProvider: f.Clock);
            var provider = new LlmProvider("controlled-model", "https://controlled.invalid/v1", "controlled-protected-key",
                ApiType.ChatCompletions, AgentType.Chat, f.Clock.GetUtcNow());
            f.Providers = new(provider.Id, new ProviderRepository(provider), f.Secret);
            f.Snapshot = Assert.IsType<OctoberChatProviderSnapshot>(await f.Providers.FreezeAsync());
            return f;
        }
        public DialogueBeginRequest Request(OctoberChatExecutionRegistration registration)
        {
            var request = new DialogueBeginRequest(Conversation.ConversationId, Conversation.ContextId, Guid.NewGuid(), "controlled question",
                new string('0', 64), Snapshot.ProviderId.ToString("D"), Snapshot.Revision, new string('b', 64), [],
                Codec.ComputeHistoryProjectionDigest([]), new(Guid.NewGuid(), new string('c', 64), "controlled host input capsule"),
                Budget.Limits, registration.Execution);
            return request with { RequestDigest = Codec.ComputeBeginDigest(Conversation, request) };
        }
    }
    private sealed class ControlledOwnedResource : IAsyncDisposable
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public bool Released { get; private set; }
        public bool FailRelease { get; set; }
        public async ValueTask DisposeAsync()
        {
            Calls++; Entered.TrySetResult(); await Release.Task;
            if (FailRelease) throw new IOException("Controlled final scope resource release failed.");
            Released = true;
        }
    }
    private sealed class ProviderRepository(LlmProvider provider) : ILlmProviderRepository
    {
        public Task<LlmProvider?> GetByIdAsync(Guid id, CancellationToken token = default) => Task.FromResult<LlmProvider?>(id == provider.Id ? provider : null);
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(string name, Guid? excludeProviderId = null, CancellationToken token = default) => throw new NotSupportedException();
        public Task<LlmProvider> AddAsync(LlmProvider row, CancellationToken token = default) => throw new NotSupportedException();
        public void Remove(LlmProvider row) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken token = default) => throw new NotSupportedException();
    }
    private sealed class ControlledSecret : ISecretEncryptionService
    {
        public int DecryptCalls { get; private set; }
        public Func<Task>? BeforeDecrypt { get; set; }
        public Task<string> EncryptAsync(string purpose, string plain, CancellationToken token = default) => throw new NotSupportedException();
        public async Task<string> DecryptAsync(string purpose, string cipher, CancellationToken token = default)
        { DecryptCalls++; if (BeforeDecrypt is not null) await BeforeDecrypt(); return "controlled-dummy-key"; }
    }
}

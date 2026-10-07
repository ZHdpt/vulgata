using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Services.OctoberChat;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberChatDialogueCommitGuardTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SubmitAsync_CanonicallyEqualClone_CommitsOnceAndDoesNotRequireObjectIdentityAsync()
    {
        var attempt = Attempt();
        var clone = attempt with { Conversation = attempt.Conversation with { RepositoryScope = attempt.Conversation.RepositoryScope.ToArray().ToImmutableArray() }, Subject = attempt.Subject! with { }, Dependencies = attempt.Dependencies! with { } };
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(attempt), OctoberKnowledgePolicy.SerializeCanonical(clone));
        int checks = 0, submissions = 0;
        var fence = new object();
        var guard = new OctoberChatDialogueCommitGuard((_, _) => { checks++; return Task.FromResult(CommitEligibilityStatus.Eligible); }, _ => CommitEligibilityStatus.Eligible, fence);
        guard.Register(attempt);
        Assert.Equal(CommitEligibilityStatus.Eligible, await guard.CheckBeforeTransactionAsync(clone));
        var committed = await guard.SubmitAsync(clone, _ => { Assert.True(Monitor.IsEntered(fence)); submissions++; return new(Task.FromResult(CommitSettlement.Committed)); });
        Assert.Equal(CommitSubmissionStatus.Submitted, committed.Status);
        Assert.Equal(CommitSettlement.Committed, committed.Settlement);
        Assert.True(checks >= 2);
        Assert.NotEqual(CommitSubmissionStatus.Submitted, (await guard.SubmitAsync(clone, _ => { submissions++; return new(Task.FromResult(CommitSettlement.Committed)); })).Status);
        Assert.Equal(1, submissions);
        Assert.False(guard.HasUnsettledSubmission);
        await guard.Close(attempt).WaitAsync(TimeSpan.FromSeconds(10));
        guard.Register(attempt); // An idempotent registration cannot reopen a closed mutation.
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("subject")]
    [InlineData("authentication")]
    [InlineData("capsule")]
    [InlineData("fence")]
    [InlineData("execution")]
    [InlineData("scope")]
    public async Task SubmitAsync_SamePayloadDigestButDifferentAttemptField_RejectsBeforeSubmissionAsync(string field)
    {
        var attempt = Attempt();
        var changed = field switch
        {
            "subject" => attempt with { Subject = attempt.Subject! with { SubjectId = "foreign-user" } },
            "authentication" => attempt with { Subject = attempt.Subject! with { AuthenticationBinding = "new-stamp" } },
            "capsule" => attempt with { Dependencies = attempt.Dependencies! with { ProtectedPayload = "foreign-capsule" } },
            "fence" => attempt with { ExpectedFence = attempt.ExpectedFence + 1 },
            "execution" => attempt with { Execution = attempt.Execution! with { AttemptId = Guid.NewGuid() } },
            _ => attempt with { Conversation = attempt.Conversation with { RepositoryScope = [Guid.NewGuid()] } }
        };
        Assert.Equal(attempt.PayloadDigest, changed.PayloadDigest);
        Assert.NotEqual(OctoberKnowledgePolicy.SerializeCanonical(attempt), OctoberKnowledgePolicy.SerializeCanonical(changed));
        var guard = Guard();
        guard.Register(attempt);
        Assert.Equal(CommitEligibilityStatus.StaleOrFenced, await guard.CheckBeforeTransactionAsync(changed));
        int submissions = 0;
        var rejected = await guard.SubmitAsync(changed, _ => { submissions++; return new(Task.FromResult(CommitSettlement.Committed)); });
        Assert.Equal(CommitSubmissionStatus.StaleOrFenced, rejected.Status);
        Assert.Equal(0, submissions);
        Assert.False(guard.HasUnsettledSubmission);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SubmitAsync_UnregisteredAndClosedAttempts_CannotAcquireSubmissionAuthorityAsync()
    {
        var attempt = Attempt();
        var guard = Guard();
        int submissions = 0;
        SubmittedCommit Submit(CancellationToken _) { submissions++; return new(Task.FromResult(CommitSettlement.Committed)); }
        Assert.Equal(CommitSubmissionStatus.StaleOrFenced, (await guard.SubmitAsync(attempt, Submit)).Status);
        guard.Register(attempt);
        await guard.Close(attempt).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(CommitEligibilityStatus.StaleOrFenced, await guard.CheckBeforeTransactionAsync(attempt));
        Assert.Equal(CommitSubmissionStatus.StaleOrFenced, (await guard.SubmitAsync(attempt, Submit)).Status);
        Assert.Equal(0, submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(CommitEligibilityStatus.Denied, CommitSubmissionStatus.Denied)]
    [InlineData(CommitEligibilityStatus.AuthorityUnavailable, CommitSubmissionStatus.AuthorityUnavailable)]
    public async Task SubmitAsync_FreshAuthorityChangesAfterTransactionCheck_DoesNotReuseEarlierGrantAsync(CommitEligibilityStatus changed, CommitSubmissionStatus expected)
    {
        var decision = CommitEligibilityStatus.Eligible;
        var guard = new OctoberChatDialogueCommitGuard((_, _) => Task.FromResult(decision), _ => CommitEligibilityStatus.Eligible, new object());
        var attempt = Attempt();
        guard.Register(attempt);
        Assert.Equal(CommitEligibilityStatus.Eligible, await guard.CheckBeforeTransactionAsync(attempt));
        decision = changed;
        var result = await guard.SubmitAsync(attempt, _ => throw new InvalidOperationException("Callback must not run."));
        Assert.Equal(expected, result.Status);
        Assert.False(guard.HasUnsettledSubmission);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Close_DuringPendingAuthority_PreventsCallbackAfterLateGrantAsync()
    {
        var entered = Signal();
        var authority = new TaskCompletionSource<CommitEligibilityStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var guard = new OctoberChatDialogueCommitGuard((_, _) => { entered.TrySetResult(); return authority.Task; }, _ => CommitEligibilityStatus.Eligible, new object());
        var attempt = Attempt();
        guard.Register(attempt);
        int submissions = 0;
        var pending = guard.SubmitAsync(attempt, _ => { submissions++; return new(Task.FromResult(CommitSettlement.Committed)); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var close = guard.Close(attempt);
        authority.SetResult(CommitEligibilityStatus.Eligible);
        Assert.Equal(CommitSubmissionStatus.StaleOrFenced, (await pending.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        await close.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SubmitAsync_CancellationAfterPhysicalSubmission_DoesNotEraseActualSettlementOrCompleteCloseAsync()
    {
        var guard = Guard();
        var attempt = Attempt();
        guard.Register(attempt);
        var settlement = new TaskCompletionSource<CommitSettlement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var submitted = Signal();
        using var cancellation = new CancellationTokenSource();
        var pending = guard.SubmitAsync(attempt, _ => { submitted.TrySetResult(); return new(settlement.Task); }, cancellation.Token);
        await submitted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        Assert.True(guard.HasUnsettledSubmission);
        var close = guard.Close(attempt);
        Assert.False(close.IsCompleted);
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(CommitSubmissionStatus.Submitted, result.Status);
        Assert.Null(result.Settlement);
        Assert.True(guard.HasUnsettledSubmission);
        Assert.False(close.IsCompleted);
        settlement.SetResult(CommitSettlement.Committed);
        await close.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(guard.HasUnsettledSubmission);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SubmitAsync_PreCancelled_NeverInvokesCommitCallbackAsync()
    {
        var guard = Guard();
        var attempt = Attempt();
        guard.Register(attempt);
        var result = await guard.SubmitAsync(attempt, _ => throw new InvalidOperationException("Must not submit."), new CancellationToken(true));
        Assert.Equal(CommitSubmissionStatus.CancelledBeforeSubmission, result.Status);
        Assert.False(guard.HasUnsettledSubmission);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SubmitAsync_CallbackThrowsAfterEntry_ConservativelyRetainsUnknownOwnershipAndCannotRetryAsync()
    {
        var guard = Guard();
        var attempt = Attempt();
        guard.Register(attempt);
        int submissions = 0;
        var result = await guard.SubmitAsync(attempt, _ => { submissions++; throw new IOException("Actual commit outcome unknown."); });
        Assert.Equal(CommitSubmissionStatus.Submitted, result.Status);
        Assert.Equal(CommitSettlement.Indeterminate, result.Settlement);
        Assert.True(guard.HasUnsettledSubmission);
        Assert.False(guard.Close(attempt).IsCompleted);
        Assert.NotEqual(CommitSubmissionStatus.Submitted, (await guard.SubmitAsync(attempt, _ => { submissions++; return new(Task.FromResult(CommitSettlement.Committed)); })).Status);
        Assert.Equal(1, submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SubmitAsync_FaultedActualSettlement_IsIndeterminateButKnownFinishedAsync()
    {
        var guard = Guard();
        var attempt = Attempt();
        guard.Register(attempt);
        var result = await guard.SubmitAsync(attempt, _ => new(Task.FromException<CommitSettlement>(new IOException("Controlled settlement failure."))));
        Assert.Equal(CommitSubmissionStatus.Submitted, result.Status);
        Assert.Equal(CommitSettlement.Indeterminate, result.Settlement);
        Assert.False(guard.HasUnsettledSubmission);
        _ = await Record.ExceptionAsync(() => guard.Close(attempt).WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(guard.Close(attempt).IsCompleted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Close_OneMutation_DoesNotCloseIndependentRegisteredMutationAsync()
    {
        var guard = Guard();
        var first = Attempt();
        var second = first with { MutationId = Guid.NewGuid(), Kind = DialogueMutationKind.TransitionTurn };
        guard.Register(first);
        guard.Register(second);
        await guard.Close(first).WaitAsync(TimeSpan.FromSeconds(10));
        var result = await guard.SubmitAsync(second, _ => new(Task.FromResult(CommitSettlement.ConfirmedNotCommitted)));
        Assert.Equal(CommitSubmissionStatus.Submitted, result.Status);
        Assert.Equal(CommitSettlement.ConfirmedNotCommitted, result.Settlement);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SubmitAsync_OwnershipChangesDuringAwaitedAuthority_RechecksUnderSharedFenceBeforeCallbackAsync()
    {
        var entered = Signal();
        var authority = new TaskCompletionSource<CommitEligibilityStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fence = new object();
        var ownership = CommitEligibilityStatus.Eligible;
        var guard = new OctoberChatDialogueCommitGuard((_, _) => { entered.TrySetResult(); return authority.Task; },
            _ => { Assert.True(Monitor.IsEntered(fence)); return ownership; }, fence);
        var attempt = Attempt();
        guard.Register(attempt);
        int calls = 0;
        var pending = guard.SubmitAsync(attempt, _ => { calls++; return new(Task.FromResult(CommitSettlement.Committed)); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        lock (fence) ownership = CommitEligibilityStatus.StaleOrFenced;
        authority.SetResult(CommitEligibilityStatus.Eligible);
        Assert.Equal(CommitSubmissionStatus.StaleOrFenced, (await pending.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        Assert.Equal(0, calls);
        Assert.False(guard.HasUnsettledSubmission);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Register_SameMutationWithDifferentCanonicalSubject_CannotRebind()
    {
        var guard = Guard();
        var attempt = Attempt();
        guard.Register(attempt);
        Assert.Throws<InvalidOperationException>(() => guard.Register(attempt with { Subject = attempt.Subject! with { SubjectId = "replacement-owner" } }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Close_InsideSynchronousSubmissionCallback_RetainsSettlementCapturedAfterCallbackReturnsAsync()
    {
        var guard = Guard();
        var attempt = Attempt();
        guard.Register(attempt);
        var settlement = new TaskCompletionSource<CommitSettlement>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? close = null;
        var pending = guard.SubmitAsync(attempt, _ =>
        {
            close = guard.Close(attempt);
            Assert.True(guard.HasUnsettledSubmission);
            Assert.False(close.IsCompleted);
            return new(settlement.Task);
        });
        Assert.NotNull(close);
        Assert.False(close.IsCompleted);
        settlement.SetResult(CommitSettlement.Committed);
        Assert.Equal(CommitSubmissionStatus.Submitted, (await pending.WaitAsync(TimeSpan.FromSeconds(10))).Status);
        await close.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(guard.HasUnsettledSubmission);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Register_UndefinedMutationKind_RejectsInvalidBoundary()
    {
        var guard = Guard();
        Assert.Throws<ArgumentException>(() => guard.Register(Attempt() with { Kind = (DialogueMutationKind)999 }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckAndSubmit_UndefinedEligibility_FailsClosedAsAuthorityUnavailableAsync(bool synchronous)
    {
        var guard = new OctoberChatDialogueCommitGuard((_, _) => Task.FromResult(synchronous ? CommitEligibilityStatus.Eligible : (CommitEligibilityStatus)999),
            _ => synchronous ? (CommitEligibilityStatus)999 : CommitEligibilityStatus.Eligible, new object());
        var attempt = Attempt();
        guard.Register(attempt);
        Assert.Equal(CommitEligibilityStatus.AuthorityUnavailable, await guard.CheckBeforeTransactionAsync(attempt));
        int callbacks = 0;
        var result = await guard.SubmitAsync(attempt, _ => { callbacks++; return new(Task.FromResult(CommitSettlement.Committed)); });
        Assert.Equal(CommitSubmissionStatus.AuthorityUnavailable, result.Status);
        Assert.Equal(0, callbacks);
        Assert.False(guard.HasUnsettledSubmission);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SubmitAsync_UndefinedPhysicalSettlement_IsIndeterminateRatherThanUnknownEnumAsync()
    {
        var guard = Guard();
        var attempt = Attempt();
        guard.Register(attempt);
        var result = await guard.SubmitAsync(attempt, _ => new(Task.FromResult((CommitSettlement)999)));
        Assert.Equal(CommitSubmissionStatus.Submitted, result.Status);
        Assert.Equal(CommitSettlement.Indeterminate, result.Settlement);
        Assert.False(guard.HasUnsettledSubmission);
    }

    private static OctoberChatDialogueCommitGuard Guard() => new((_, _) => Task.FromResult(CommitEligibilityStatus.Eligible), _ => CommitEligibilityStatus.Eligible, new object());
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static DialogueCommitAttempt Attempt() => new(DialogueMutationKind.SaveTerminal, Guid.NewGuid(),
        new(Guid.NewGuid(), Guid.NewGuid(), "ordinary-user", new(new("owned-db", "owned-target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid()),
            KnowledgeMode.IT, [Guid.NewGuid()]), Guid.NewGuid(), new(Guid.NewGuid(), Guid.NewGuid()), 7, new string('b', 64),
        new("ordinary-user", "current-authentication"), new(Guid.NewGuid(), new string('c', 64), "owner-protected-capsule"));
}

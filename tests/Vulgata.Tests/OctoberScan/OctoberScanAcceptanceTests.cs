using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

public sealed class OctoberScanAcceptanceTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accept_ActualWriter_ReplayRetainsExactAcceptedOutcomeWithoutAnotherWorkerAsync(bool zeroUnit)
    {
        await using var harness = await OctoberScanAcceptanceHarness.CreateAsync(zeroUnit);
        var first = await harness.Attempt.AcceptAsync(harness.Writer());
        Assert.Equal(KnowledgeWriteStatus.Committed, first.Status);
        var replay = await harness.Attempt.AcceptAsync(harness.Writer());
        Assert.Contains(replay.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
        Assert.Equal(first.AcceptedFile!.ManifestId, replay.AcceptedFile!.ManifestId);
        Assert.Equal(first.AcceptedFile.Documents.ToArray(), replay.AcceptedFile.Documents.ToArray());
        Assert.Equal(zeroUnit ? CoverageDisposition.CompletedZeroUnit : CoverageDisposition.Accepted, first.AcceptedFile.Coverage.Disposition);
        Assert.Equal(zeroUnit ? 0 : 1, first.AcceptedFile.Documents.Length);
        Assert.Equal(1, harness.WorkerCalls);
        Assert.True(harness.Attempt.HasSubmitted);
        Assert.True(harness.Attempt.IsSettledForFreeze);
        await using var context = harness.Fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync());
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accept_SealedThenFencedOrExpired_ActualWriterCannotBeginCommitAsync(bool expire)
    {
        await using var harness = await OctoberScanAcceptanceHarness.CreateAsync();
        var submission = await harness.Attempt.ExecuteAndSealAsync();
        Assert.NotNull(submission);
        if (expire) harness.Clock.Advance(TimeSpan.FromMinutes(2), deliverTimers: false);
        else await harness.Attempt.FenceAsync();
        var result = await harness.Attempt.AcceptAsync(harness.Writer());
        Assert.Contains(result.Status, new[] { KnowledgeWriteStatus.StaleOrFenced, KnowledgeWriteStatus.NotCompleted, KnowledgeWriteStatus.Denied });
        Assert.False(harness.Attempt.HasSubmitted);
        Assert.Equal(submission.Proposal.CanonicalProposalDigest, harness.Attempt.Submission!.Proposal.CanonicalProposalDigest);
        await harness.Fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Accept_ExpiryAfterWriterStaging_FinalGuardRejectsAndRollsBackAsync()
    {
        await using var harness = await OctoberScanAcceptanceHarness.CreateAsync();
        var guard = new BeforeSubmission(harness.Attempt, () => harness.Clock.Advance(TimeSpan.FromMinutes(2), deliverTimers: false));
        var result = await harness.Attempt.AcceptAsync(harness.Writer(guard));
        Assert.Equal(1, guard.SubmissionChecks);
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, result.Status);
        Assert.False(harness.Attempt.HasSubmitted);
        await harness.Fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accept_SourceChangedOrContributorAuthorityLostAfterSeal_DeniesAsync(bool revoke)
    {
        await using var harness = await OctoberScanAcceptanceHarness.CreateAsync();
        Assert.NotNull(await harness.Attempt.ExecuteAndSealAsync());
        if (revoke) harness.Authority = DisclosureStatus.Denied;
        else harness.Source.Current = false;
        var result = await harness.Attempt.AcceptAsync(harness.Writer());
        Assert.NotEqual(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Null(result.AcceptedFile);
        Assert.False(harness.Attempt.HasSubmitted);
        await harness.Fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Verify_ChangedPayloadOrForeignProof_CannotReuseTrustedCompletionAsync()
    {
        await using var harness = await OctoberScanAcceptanceHarness.CreateAsync();
        var sealedSubmission = Assert.IsType<CompletedFileSubmission>(await harness.Attempt.ExecuteAndSealAsync());
        var again = await harness.Attempt.ExecuteAndSealAsync();
        Assert.Same(sealedSubmission, again);
        Assert.Equal(1, harness.WorkerCalls);
        var changed = sealedSubmission.Proposal with { Uncertainties = [new("new", "Changed proposal payload", [], ["Changed after seal"])] };
        changed = changed with { CanonicalProposalDigest = OctoberKnowledgePolicy.ComputeProposalDigest(changed) };
        Assert.NotEqual(DisclosureStatus.Allowed, await harness.Attempt.VerifyAsync(sealedSubmission with { Proposal = changed }));
        Assert.NotEqual(DisclosureStatus.Allowed, await harness.Attempt.VerifyAsync(sealedSubmission with { Proof = new TestCompletedFileProof(sealedSubmission.Proposal) }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Submit_FenceAfterActualSubmission_DoesNotClaimRollbackOrFreezeBeforeSettlementAsync()
    {
        await using var harness = await OctoberScanAcceptanceHarness.CreateAsync();
        var submission = Assert.IsType<CompletedFileSubmission>(await harness.Attempt.ExecuteAndSealAsync());
        var settlement = new TaskCompletionSource<CommitSettlement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = harness.Attempt.SubmitAsync(new(submission.Operation, submission.Proposal.AttemptId, submission.Proposal.CanonicalProposalDigest), _ =>
        {
            entered.TrySetResult();
            return new SubmittedCommit(settlement.Task);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(harness.Attempt.HasSubmitted);
            await harness.Attempt.FenceAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(harness.Attempt.IsSettledForFreeze);
            Assert.NotEqual(CommitSettlement.ConfirmedNotCommitted, harness.Attempt.Settlement);
            settlement.TrySetResult(CommitSettlement.Committed);
            var result = await pending;
            Assert.Equal(CommitSubmissionStatus.Submitted, result.Status);
            Assert.Equal(CommitSettlement.Committed, result.Settlement);
        }
        finally { settlement.TrySetResult(CommitSettlement.Committed); await pending; }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Reconcile_CommittedThenDisclosureRevoked_PreservesCommitWithoutExposingHandlesAsync()
    {
        await using var harness = await OctoberScanAcceptanceHarness.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await harness.Attempt.AcceptAsync(harness.Writer())).Status);
        harness.Authority = DisclosureStatus.Denied;
        var result = await harness.Attempt.ReconcileAsync(harness.Writer());
        Assert.Equal(KnowledgeWriteStatus.Denied, result.Status);
        Assert.Null(result.AcceptedFile);
        Assert.Equal(CommitSettlement.Committed, harness.Attempt.Settlement);
        Assert.Equal(1, harness.WorkerCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Submit_UncooperativeAuthority_OriginalDeadlineReleasesGuardWithoutSubmittingAsync()
    {
        await using var harness = await OctoberScanAcceptanceHarness.CreateAsync();
        var submission = Assert.IsType<CompletedFileSubmission>(await harness.Attempt.ExecuteAndSealAsync());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<DisclosureStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.AuthorityCheck = _ => { entered.TrySetResult(); return release.Task; };
        var commits = 0;
        var pending = harness.Attempt.SubmitAsync(new(submission.Operation, submission.Proposal.AttemptId, submission.Proposal.CanonicalProposalDigest), _ =>
        {
            commits++;
            return new SubmittedCommit(Task.FromResult(CommitSettlement.Committed));
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            harness.Clock.Advance(TimeSpan.FromMinutes(2));
            await harness.Attempt.FenceAsync().WaitAsync(TimeSpan.FromSeconds(3));
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.NotEqual(CommitSubmissionStatus.Submitted, result.Status);
            Assert.Equal(0, commits);
            Assert.False(release.Task.IsCompleted);
            Assert.False(harness.Attempt.IsSettledForFreeze);
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Attempt.DisposeAsync().AsTask());
        }
        finally { release.TrySetResult(DisclosureStatus.Allowed); await pending; }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("denied")]
    [InlineData("unavailable")]
    [InlineData("source-changed")]
    public async Task Verify_ObservedAuthorityOrSourceLossThenRestored_CannotReviveSameAttemptAsync(string loss)
    {
        await using var harness = await OctoberScanAcceptanceHarness.CreateAsync();
        var submission = Assert.IsType<CompletedFileSubmission>(await harness.Attempt.ExecuteAndSealAsync());
        if (loss == "source-changed") harness.Source.Current = false;
        else harness.Authority = loss == "denied" ? DisclosureStatus.Denied : DisclosureStatus.AuthorityUnavailable;
        Assert.Equal(loss == "unavailable" ? DisclosureStatus.AuthorityUnavailable : DisclosureStatus.Denied,
            await harness.Attempt.VerifyAsync(submission));
        harness.Source.Current = true;
        harness.Authority = DisclosureStatus.Allowed;
        var result = await harness.Attempt.AcceptAsync(harness.Writer());
        Assert.DoesNotContain(result.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
        Assert.Null(result.AcceptedFile);
        Assert.False(harness.Attempt.HasSubmitted);
        Assert.Equal(1, harness.WorkerCalls);
        await harness.Fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Submit_UnresolvedCompletedCallback_RefusesDisposalButPreservesExactReconciliationAsync(bool throwAfterPossibleSubmission)
    {
        await using var harness = await OctoberScanAcceptanceHarness.CreateAsync(zeroUnit: true);
        var submission = Assert.IsType<CompletedFileSubmission>(await harness.Attempt.ExecuteAndSealAsync());
        var lookup = new ControlledSettlementLookup(submission);
        var guard = await harness.Attempt.SubmitAsync(new(submission.Operation, submission.Proposal.AttemptId, submission.Proposal.CanonicalProposalDigest), _ =>
        {
            if (throwAfterPossibleSubmission) throw new IOException("Controlled acknowledgement loss after possible submission");
            return new SubmittedCommit(Task.FromResult(CommitSettlement.Indeterminate));
        });
        Assert.Equal(CommitSettlement.Indeterminate, guard.Settlement);
        try
        {
            Assert.False(harness.Attempt.IsSettledForFreeze);
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Attempt.DisposeAsync().AsTask());
            var unresolved = await harness.Attempt.ReconcileAsync(lookup);
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, unresolved.Status);
            Assert.Null(unresolved.AcceptedFile);
            Assert.False(harness.Attempt.IsSettledForFreeze);
            lookup.KnownCommitted = true;
            var confirmed = await harness.Attempt.ReconcileAsync(lookup);
            Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, confirmed.Status);
            Assert.Equal(lookup.Accepted.ManifestId, confirmed.AcceptedFile!.ManifestId);
            Assert.Equal(CommitSettlement.Committed, harness.Attempt.Settlement);
            Assert.True(harness.Attempt.IsSettledForFreeze);
            Assert.Equal(1, harness.WorkerCalls);
            Assert.Equal(2, lookup.Lookups);
        }
        finally
        {
            // This controlled lookup models authoritative settlement; it never invokes a second write.
            lookup.KnownCommitted = true;
            await harness.Attempt.ReconcileAsync(lookup);
        }
    }

    private sealed class ControlledSettlementLookup(CompletedFileSubmission submission) : IOctoberKnowledgeWriter
    {
        internal bool KnownCommitted { get; set; }
        internal int Lookups { get; private set; }
        internal AcceptedFile Accepted { get; } = new(Guid.NewGuid(), submission.Proposal.AdmissionId,
            submission.Proposal.CanonicalProposalDigest, [], submission.Proposal.Coverage);
        public Task<KnowledgeWriteResult> LookupOperationAsync(KnowledgeBinding binding, OperationKey operation, string payloadDigest, CancellationToken cancellationToken = default)
        {
            Assert.Equal(submission.Proposal.Binding, binding);
            Assert.Equal(submission.Operation, operation);
            Assert.Equal(submission.Proposal.CanonicalProposalDigest, payloadDigest);
            Lookups++;
            return Task.FromResult(new KnowledgeWriteResult(KnownCommitted ? KnowledgeWriteStatus.AlreadyCommitted : KnowledgeWriteStatus.CommitIndeterminate,
                operation, KnownCommitted ? Accepted : null));
        }
        public Task<KnowledgeWriteResult> AcceptCompletedFileAsync(CompletedFileSubmission value, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Reconciliation must not issue another write.");
        public Task<KnowledgeWriteResult> FreezeCorpusAsync(CorpusFreezeRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unresolved acceptance cannot freeze.");
        public Task<KnowledgeWriteResult> PublishSelectionAsync(PublicationRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Reconciliation cannot publish.");
    }

    private sealed class BeforeSubmission(IOctoberCommitSubmissionGuard inner, Action before) : IOctoberCommitSubmissionGuard
    {
        internal int SubmissionChecks { get; private set; }
        public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken cancellationToken = default) => inner.CheckBeforeTransactionAsync(attempt, cancellationToken);
        public Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
        {
            SubmissionChecks++; before();
            return inner.SubmitAsync(attempt, submitAsync, cancellationToken);
        }
    }
}

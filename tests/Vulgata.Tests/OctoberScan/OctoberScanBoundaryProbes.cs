using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Scripted owner-fixture F1/F2/F5 interactions; no real timers, transports, transactional store or finalizer.</summary>
public sealed class OctoberScanBoundaryProbes
{
    private static CompletedFileSubmission Submission(OctoberKnowledgeFixture fixture) => new(new(fixture.Binding.BatchId, OperationKind.AcceptFile, OctoberKnowledgeFixture.Id(910)), fixture.MultiUnit, new ControlledCompletedProof(fixture.MultiUnit));
    private static CorpusFreezeRequest Freeze(OctoberKnowledgeFixture fixture) => new(new(fixture.Binding.BatchId, OperationKind.FreezeCorpus, OctoberKnowledgeFixture.Id(911)), fixture.Binding, "scan-controlled-freeze", [], [fixture.MultiUnit.Coverage]);
    private static ProviderAttemptRequest Attempt(int id, BusinessWorkKind kind = BusinessWorkKind.Resolution) => new(OctoberKnowledgeFixture.Id(912), new(OctoberKnowledgeFixture.Id(1), OperationKind.FinalizeCorpus, OctoberKnowledgeFixture.Id(913)), OctoberKnowledgeFixture.Id(id), kind, "controlled-profile", TimeSpan.FromSeconds(10), 40);
    private static ProviderAttemptUsage Actual => new(10, 5, 0.01m, TimeSpan.Zero, TimeSpan.FromSeconds(1), ProviderAttemptOutcome.Completed);
    private static ProviderAttemptUsage Unknown => new(null, null, null, TimeSpan.Zero, TimeSpan.FromSeconds(1), ProviderAttemptOutcome.Cancelled);

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptCompletedFileAsync_SealThenFenceOrDeadline_RejectsBeforeEntryAsync(bool fence)
    {
        var fixture = new OctoberKnowledgeFixture();
        var submission = Submission(fixture);
        var guard = new ControlledCommitGuard { Fenced = fence, Clock = fence ? 0 : 10 };
        var model = new ControlledKnowledgeWriter(fixture, guard);
        IOctoberKnowledgeWriter writer = model;
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, (await writer.AcceptCompletedFileAsync(submission)).Status);
        Assert.Equal(submission.Proposal.CanonicalProposalDigest, submission.Proof.ProposalDigest);
        Assert.Equal(0, model.TransactionsStarted);
        Assert.Equal(0, guard.Submissions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_DeadlineDuringStagingWithoutTimerFence_RejectsSubmissionAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var guard = new ControlledCommitGuard();
        var model = new ControlledKnowledgeWriter(fixture, guard) { AfterStaging = () => guard.Clock = guard.Deadline };
        IOctoberKnowledgeWriter writer = model;
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, (await writer.AcceptCompletedFileAsync(Submission(fixture))).Status);
        Assert.False(guard.Fenced);
        Assert.Equal(1, model.TransactionsStarted);
        Assert.Equal(1, model.StagedRollbacks);
        Assert.Equal(0, guard.Submissions);
        Assert.Equal(0, model.AcceptedRows);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LookupOperationAsync_SubmittedThenFencedAndRevoked_WaitsForSettlementAndFreshDisclosureAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var guard = new ControlledCommitGuard();
        var settlement = new TaskCompletionSource<CommitSettlement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new ControlledKnowledgeWriter(fixture, guard) { SettleSubmission = () => settlement.Task };
        IOctoberKnowledgeWriter writer = model;
        var submission = Submission(fixture);
        var pending = writer.AcceptCompletedFileAsync(submission);
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, guard.Submissions);
        guard.Fenced = true;
        Assert.Equal(0, model.AcceptedRows);
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, (await writer.LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest)).Status);
        var frozen = await writer.FreezeCorpusAsync(Freeze(fixture));
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, frozen.Status);
        Assert.Null(frozen.Corpus);
        model.Authority = DisclosureStatus.Denied;
        settlement.SetResult(CommitSettlement.Committed);
        Assert.Equal(KnowledgeWriteStatus.Denied, (await pending).Status);
        Assert.Equal(1, model.AcceptedRows);
        var denied = await writer.LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest);
        Assert.Equal(KnowledgeWriteStatus.Denied, denied.Status);
        Assert.Null(denied.AcceptedFile);
        model.Authority = DisclosureStatus.Allowed;
        var recovered = await writer.LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest);
        Assert.Equal(KnowledgeWriteStatus.Committed, recovered.Status);
        Assert.Equal(submission.Proposal.AdmissionId, recovered.AcceptedFile!.AdmissionId);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await writer.FreezeCorpusAsync(Freeze(fixture))).Status);
        Assert.Equal(1, model.TransactionsStarted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(CommitSettlement.ConfirmedNotCommitted, KnowledgeWriteStatus.CancelledBeforeCommit)]
    [InlineData(CommitSettlement.Indeterminate, KnowledgeWriteStatus.CommitIndeterminate)]
    public async Task AcceptCompletedFileAsync_SubmittedSettlement_DoesNotInferRollbackFromFenceAsync(CommitSettlement settlement, KnowledgeWriteStatus expected)
    {
        var fixture = new OctoberKnowledgeFixture();
        var guard = new ControlledCommitGuard();
        IOctoberKnowledgeWriter writer = new ControlledKnowledgeWriter(fixture, guard) { SettleSubmission = () => { guard.Fenced = true; return Task.FromResult(settlement); } };
        Assert.Equal(expected, (await writer.AcceptCompletedFileAsync(Submission(fixture))).Status);
        Assert.Equal(settlement == CommitSettlement.Indeterminate ? KnowledgeWriteStatus.CommitIndeterminate : KnowledgeWriteStatus.Committed, (await writer.FreezeCorpusAsync(Freeze(fixture))).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcquireProviderAttemptAsync_CapOneChildAndRetry_SettlesBeforeReleaseWithoutRenewingBudgetAsync()
    {
        var model = new ControlledExecutionScope();
        IOctoberExecutionScope scope = model;
        var first = (await scope.AcquireProviderAttemptAsync(Attempt(920))).Lease!;
        Assert.Equal(ProviderAdmissionStatus.Granted, await first.CheckBeforeSendAsync());
        var child = scope.AcquireProviderAttemptAsync(Attempt(921, BusinessWorkKind.Synthesis));
        Assert.False(child.IsCompleted);
        await first.DisposeAsync();
        Assert.Equal(0, model.Releases);
        Assert.False(child.IsCompleted);
        await first.SettleAsync(Actual);
        await first.SettleAsync(Actual);
        Assert.False(child.IsCompleted);
        await first.DisposeAsync();
        await first.DisposeAsync();
        var second = (await child).Lease!;
        Assert.Equal(ProviderAdmissionStatus.Granted, await second.CheckBeforeSendAsync());
        await second.SettleAsync(Unknown);
        await second.DisposeAsync();
        Assert.Equal(45, model.RemainingTokens);
        var retry = await scope.AcquireProviderAttemptAsync(Attempt(922) with { MaximumTokens = 46 });
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, retry.Status);
        Assert.Null(retry.Lease);
        Assert.Equal(2, model.Grants);
        Assert.Equal(2, model.Settlements);
        Assert.Equal(2, model.Releases);
        Assert.Equal(2, model.RemainingRequests);
        Assert.Null(model.Usage[1].InputTokens);
        Assert.Null(model.Usage[1].Cost);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcquireProviderAttemptAsync_HardStop_DeniesQueuedAndCancelsActiveWithoutEarlyReleaseAsync()
    {
        var model = new ControlledExecutionScope();
        IOctoberExecutionScope scope = model;
        var active = (await scope.AcquireProviderAttemptAsync(Attempt(923))).Lease!;
        Assert.Equal(ProviderAdmissionStatus.Granted, await active.CheckBeforeSendAsync());
        var queued = scope.AcquireProviderAttemptAsync(Attempt(924));
        model.HardStop();
        Assert.True(active.CancellationToken.IsCancellationRequested);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, (await queued).Status);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, await active.CheckBeforeSendAsync());
        Assert.Equal(ProviderAdmissionStatus.HardStopped, (await scope.AcquireProviderAttemptAsync(Attempt(925))).Status);
        await active.DisposeAsync();
        Assert.Equal(0, model.Releases);
        await active.SettleAsync(Unknown);
        await active.DisposeAsync();
        Assert.Equal(1, model.Sends);
        Assert.Equal(1, model.Releases);
        Assert.Equal(60, model.RemainingTokens);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcquireProviderAttemptAsync_RateQueuedThenDeniedOrExpired_DoesNotChargeOrSendAsync(bool expired)
    {
        var model = new ControlledExecutionScope { RateAllowed = false };
        IOctoberExecutionScope scope = model;
        var queued = scope.AcquireProviderAttemptAsync(Attempt(926));
        Assert.False(queued.IsCompleted);
        model.DeadlineExpired = expired;
        model.RateDenied = !expired;
        model.Pump();
        Assert.Equal(expired ? ProviderAdmissionStatus.DeadlineExceeded : ProviderAdmissionStatus.Denied, (await queued).Status);
        Assert.Equal(0, model.Grants);
        Assert.Equal(0, model.Sends);
        Assert.Equal(4, model.RemainingRequests);
        Assert.Equal(100, model.RemainingTokens);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckBeforeSendAsync_RateRevokedAfterGrant_DoesNotSendAsync()
    {
        var model = new ControlledExecutionScope();
        IOctoberExecutionScope scope = model;
        var lease = (await scope.AcquireProviderAttemptAsync(Attempt(927))).Lease!;
        model.RateDenied = true;
        Assert.Equal(ProviderAdmissionStatus.Denied, await lease.CheckBeforeSendAsync());
        Assert.Equal(0, model.Sends);
        await lease.SettleAsync(Unknown);
        await lease.DisposeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishSelectionAsync_ClOnlyOwnerPort_ProvidesExactReaderHandlesWithoutBusinessCallsAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var model = new ControlledKnowledgeWriter(fixture, new());
        IOctoberKnowledgeWriter writer = model;
        var accepted = await writer.AcceptCompletedFileAsync(Submission(fixture));
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        Assert.Empty(fixture.EarlyPublication.Relationships);
        Assert.Empty(fixture.EarlyPublication.Flows);
        var publication = await writer.PublishSelectionAsync(fixture.EarlyPublication);
        Assert.Equal(KnowledgeWriteStatus.Committed, publication.Status);
        Assert.Equal(new PublicationBinding(fixture.Binding, fixture.EarlyPublicationId), publication.Publication);
        Assert.Equal(0, model.ProviderCalls);
        // Owner fixture membership is pre-scripted; this is not accept/freeze/publish persistence equivalence.
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selected = (await reader.OpenReadSelectionAsync(fixture.Selection(publication: publication.Publication!.PublicationId))).Selection!;
        var page = await reader.SearchAsync(new(selected, ""));
        Assert.Equal(3, page.Documents.Length);
        Assert.All(page.Documents, document => Assert.Equal(DocumentKind.CodeLogic, document.Kind));
        foreach (var document in page.Documents)
        {
            var exact = await reader.ReadExactAsync(new(selected, document.Handle));
            Assert.Equal(KnowledgeReadStatus.Success, exact.Status);
            Assert.Equal(document.Handle, exact.Document!.Handle);
        }
        var business = (await reader.OpenReadSelectionAsync(fixture.Selection(KnowledgeMode.Business, fixture.EarlyPublicationId))).Selection!;
        Assert.Empty((await reader.SearchAsync(new(business, ""))).Documents);
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, (await writer.PublishSelectionAsync(fixture.EarlyPublication with { Documents = [] })).Status);
    }
}

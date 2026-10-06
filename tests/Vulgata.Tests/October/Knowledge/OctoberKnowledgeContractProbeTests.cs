using System.Text;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Compiled consumer probes against labelled controlled models; none qualifies production persistence, scheduling, Identity or cursor cryptography.</summary>
public sealed class OctoberKnowledgeContractProbeTests
{
    private static CompletedFileSubmission Submission(OctoberKnowledgeFixture fixture) => new(new(fixture.Binding.BatchId, OperationKind.AcceptFile, OctoberKnowledgeFixture.Id(80)), fixture.MultiUnit, new ControlledCompletedProof(fixture.MultiUnit));
    private static CorpusFreezeRequest Freeze(OctoberKnowledgeFixture fixture) => new(new(fixture.Binding.BatchId, OperationKind.FreezeCorpus, OctoberKnowledgeFixture.Id(81)), fixture.Binding, "controlled-freeze", [], [fixture.MultiUnit.Coverage]);
    private static ProviderAttemptRequest Attempt(int id, BusinessWorkKind kind = BusinessWorkKind.Resolution) => new(OctoberKnowledgeFixture.Id(90), new(OctoberKnowledgeFixture.Id(1), OperationKind.FinalizeCorpus, OctoberKnowledgeFixture.Id(91)), OctoberKnowledgeFixture.Id(id), kind, "controlled-profile", TimeSpan.FromSeconds(10), 40);
    private static ProviderAttemptUsage Actual => new(10, 5, 0.01m, TimeSpan.Zero, TimeSpan.FromSeconds(1), ProviderAttemptOutcome.Completed);
    private static ProviderAttemptUsage Unknown => new(null, null, null, TimeSpan.Zero, TimeSpan.FromSeconds(1), ProviderAttemptOutcome.Cancelled);

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_SealThenDeadlineExpires_RejectsBeforeStagingAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var submission = Submission(fixture);
        var guard = new ControlledCommitGuard { Clock = 10 };
        var model = new ControlledKnowledgeWriter(fixture, guard);
        IOctoberKnowledgeWriter writer = model;
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, (await writer.AcceptCompletedFileAsync(submission)).Status);
        Assert.Equal(submission.Proposal.CanonicalProposalDigest, submission.Proof.ProposalDigest);
        Assert.Equal(0, model.TransactionsStarted);
        Assert.Equal(0, guard.Submissions);
        Assert.Equal(0, model.AcceptedRows);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_DeadlineExpiresDuringStaging_RollsBackWithoutSubmissionAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var guard = new ControlledCommitGuard();
        var model = new ControlledKnowledgeWriter(fixture, guard) { AfterStaging = () => guard.Clock = 10 };
        IOctoberKnowledgeWriter writer = model;
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, (await writer.AcceptCompletedFileAsync(Submission(fixture))).Status);
        Assert.Equal(1, model.TransactionsStarted);
        Assert.Equal(1, model.StagedRollbacks);
        Assert.Equal(0, guard.Submissions);
        Assert.Equal(0, model.AcceptedRows);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LookupOperationAsync_SubmittedCommitIsTemporarilyAbsent_BlocksFreezeUntilSettlementAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var guard = new ControlledCommitGuard();
        var settlement = new TaskCompletionSource<CommitSettlement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new ControlledKnowledgeWriter(fixture, guard) { SettleSubmission = () => settlement.Task };
        IOctoberKnowledgeWriter writer = model;
        var submission = Submission(fixture);
        var pending = writer.AcceptCompletedFileAsync(submission);
        Assert.False(pending.IsCompleted);
        guard.Fenced = true;
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, (await writer.LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest)).Status);
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, (await writer.FreezeCorpusAsync(Freeze(fixture))).Status);
        settlement.SetResult(CommitSettlement.Committed);
        var committed = await pending;
        Assert.Equal(KnowledgeWriteStatus.Committed, committed.Status);
        Assert.Equal(committed.AcceptedFile, (await writer.LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest)).AcceptedFile);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await writer.FreezeCorpusAsync(Freeze(fixture))).Status);
        Assert.Equal(1, model.AcceptedRows);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(CommitSettlement.ConfirmedNotCommitted, KnowledgeWriteStatus.CancelledBeforeCommit)]
    [InlineData(CommitSettlement.Indeterminate, KnowledgeWriteStatus.CommitIndeterminate)]
    public async Task AcceptCompletedFileAsync_SubmittedOutcome_UsesSettlementNotCancellationAssumptionAsync(CommitSettlement outcome, KnowledgeWriteStatus expected)
    {
        var fixture = new OctoberKnowledgeFixture();
        var guard = new ControlledCommitGuard();
        var model = new ControlledKnowledgeWriter(fixture, guard) { SettleSubmission = () => { guard.Fenced = true; return Task.FromResult(outcome); } };
        IOctoberKnowledgeWriter writer = model;
        Assert.Equal(expected, (await writer.AcceptCompletedFileAsync(Submission(fixture))).Status);
        Assert.Equal(0, model.AcceptedRows);
        Assert.Equal(outcome == CommitSettlement.Indeterminate ? KnowledgeWriteStatus.CommitIndeterminate : KnowledgeWriteStatus.Committed, (await writer.FreezeCorpusAsync(Freeze(fixture))).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_AuthorityLostAfterSubmission_PreservesHistoryWithoutDisclosureAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var model = new ControlledKnowledgeWriter(fixture, new());
        model.SettleSubmission = () => { model.Authority = DisclosureStatus.Denied; return Task.FromResult(CommitSettlement.Committed); };
        IOctoberKnowledgeWriter writer = model;
        var submission = Submission(fixture);
        Assert.Equal(KnowledgeWriteStatus.Denied, (await writer.AcceptCompletedFileAsync(submission)).Status);
        Assert.Equal(1, model.AcceptedRows);
        var denied = await writer.LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest);
        Assert.Equal(KnowledgeWriteStatus.Denied, denied.Status);
        Assert.Null(denied.AcceptedFile);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishSelectionAsync_ClOnlyBeforeB1B2_HasExactPublicationAndZeroBusinessCallsAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var model = new ControlledKnowledgeWriter(fixture, new());
        IOctoberKnowledgeWriter writer = model;
        Assert.Empty(fixture.EarlyPublication.Relationships);
        Assert.Empty(fixture.EarlyPublication.Flows);
        var result = await writer.PublishSelectionAsync(fixture.EarlyPublication);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Equal(fixture.EarlyPublicationId, result.Publication!.PublicationId);
        Assert.NotEqual(fixture.PublicationId, result.Publication.PublicationId);
        Assert.Equal(0, model.ProviderCalls);
        Assert.Equal(KnowledgeWriteStatus.InvalidProposal, (await writer.PublishSelectionAsync(fixture.EarlyPublication with { Documents = [] })).Status);
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection(publication: result.Publication.PublicationId))).Selection!;
        var page = await reader.SearchAsync(new(selection, ""));
        Assert.Equal(3, page.Documents.Length);
        Assert.All(page.Documents, d => Assert.Equal(DocumentKind.CodeLogic, d.Kind));
        Assert.True(page.Coverage!.IsPartial);
        Assert.Empty((await reader.GetRelatedAsync(new(selection, page.Documents[0].Handle))).Relationships);
        var business = (await reader.OpenReadSelectionAsync(fixture.Selection(KnowledgeMode.Business, result.Publication.PublicationId))).Selection!;
        var empty = await reader.SearchAsync(new(business, ""));
        Assert.Empty(empty.Documents);
        Assert.Null(empty.Next);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(BusinessWorkKind.Resolution)]
    [InlineData(BusinessWorkKind.Synthesis)]
    public async Task AcquireProviderAttemptAsync_CapOne_QueuesChildAndReusesRemainingBudgetAsync(BusinessWorkKind secondKind)
    {
        var model = new ControlledExecutionScope();
        IOctoberExecutionScope scope = model;
        var first = (await scope.AcquireProviderAttemptAsync(Attempt(100))).Lease!;
        Assert.Equal(ProviderAdmissionStatus.Granted, await first.CheckBeforeSendAsync());
        var waiting = scope.AcquireProviderAttemptAsync(Attempt(101, secondKind));
        Assert.False(waiting.IsCompleted);
        Assert.Equal(1, model.Grants);
        await first.DisposeAsync();
        Assert.False(waiting.IsCompleted);
        Assert.Equal(0, model.Releases);
        await first.SettleAsync(Actual);
        await first.SettleAsync(Actual);
        Assert.False(waiting.IsCompleted);
        await first.DisposeAsync();
        await first.DisposeAsync();
        var second = (await waiting).Lease!;
        Assert.Equal(ProviderAdmissionStatus.Granted, await second.CheckBeforeSendAsync());
        await second.SettleAsync(Actual);
        await second.DisposeAsync();
        Assert.Equal(2, model.Grants);
        Assert.Equal(2, model.Sends);
        Assert.Equal(2, model.Settlements);
        Assert.Equal(2, model.Releases);
        Assert.Equal(2, model.RemainingRequests);
        Assert.Equal(70, model.RemainingTokens);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcquireProviderAttemptAsync_HardStop_CancelsActiveDeniesQueuedRetryAndKeepsUnknownChargeAsync()
    {
        var model = new ControlledExecutionScope();
        IOctoberExecutionScope scope = model;
        var active = (await scope.AcquireProviderAttemptAsync(Attempt(100))).Lease!;
        await active.CheckBeforeSendAsync();
        var waiting = scope.AcquireProviderAttemptAsync(Attempt(101));
        model.HardStop();
        Assert.True(active.CancellationToken.IsCancellationRequested);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, (await waiting).Status);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, await active.CheckBeforeSendAsync());
        Assert.Equal(ProviderAdmissionStatus.HardStopped, (await scope.AcquireProviderAttemptAsync(Attempt(102))).Status);
        await active.DisposeAsync();
        Assert.Equal(0, model.Releases);
        await active.SettleAsync(Unknown);
        await active.DisposeAsync();
        Assert.Equal(1, model.Sends);
        Assert.Equal(1, model.Settlements);
        Assert.Equal(1, model.Releases);
        Assert.Equal(60, model.RemainingTokens);
        Assert.Null(Assert.Single(model.Usage).InputTokens);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcquireProviderAttemptAsync_RateQueued_DeniesOrExpiresWithoutSendingAsync(bool expiry)
    {
        var model = new ControlledExecutionScope { RateAllowed = false };
        IOctoberExecutionScope scope = model;
        var pending = scope.AcquireProviderAttemptAsync(Attempt(100));
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, model.Grants);
        Assert.Equal(0, model.Sends);
        model.DeadlineExpired = expiry;
        model.RateDenied = !expiry;
        model.Pump();
        Assert.Equal(expiry ? ProviderAdmissionStatus.DeadlineExceeded : ProviderAdmissionStatus.Denied, (await pending).Status);
        Assert.Equal(4, model.RemainingRequests);
        Assert.Equal(100, model.RemainingTokens);
        Assert.Equal(0, model.Sends);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckBeforeSendAsync_GrantThenRateOrHardStopChanges_NeverSendsAsync()
    {
        var model = new ControlledExecutionScope();
        IOctoberExecutionScope scope = model;
        var lease = (await scope.AcquireProviderAttemptAsync(Attempt(100))).Lease!;
        model.RateDenied = true;
        Assert.Equal(ProviderAdmissionStatus.Denied, await lease.CheckBeforeSendAsync());
        model.HardStop();
        Assert.Equal(ProviderAdmissionStatus.HardStopped, await lease.CheckBeforeSendAsync());
        Assert.Equal(0, model.Sends);
        await lease.SettleAsync(Unknown);
        await lease.DisposeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_BusinessAndUncitedDeniedContributor_ExcludesAllForbiddenMetadataAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection(KnowledgeMode.Business))).Selection!;
        var page = await reader.SearchAsync(new(selection, ""));
        Assert.Equal(2, page.Documents.Length);
        Assert.Null(page.Next);
        Assert.All(page.Documents, d => { Assert.Equal(DocumentKind.BusinessLogic, d.Kind); Assert.All(d.QualifiedExcerpt, c => Assert.Empty(c.Evidence)); });
        Assert.DoesNotContain(page.Documents, d => d.Handle.DocumentVersionId == fixture.BlHidden.DocumentVersionId);
        var hiddenSearch = await reader.SearchAsync(new(selection, "隐藏业务标题"));
        Assert.Empty(hiddenSearch.Documents);
        Assert.Null(hiddenSearch.Next);
        foreach (var handle in new[] { fixture.Handle(fixture.ClA), fixture.Handle(fixture.BlHidden) })
        {
            var denied = await reader.ReadExactAsync(new(selection, handle));
            Assert.Equal(KnowledgeReadStatus.Unavailable, denied.Status);
            Assert.Null(denied.Document);
            Assert.Null(denied.Content);
            Assert.Empty(denied.Claims);
        }
        Assert.Empty((await reader.GetRelatedAsync(new(selection, fixture.Handle(fixture.BlA)))).Relationships);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_OpaqueSectionContinuation_PreservesExactIdentityUtf8AndQualificationsAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        var handle = fixture.Handle(fixture.ClA, section: "overview");
        var first = await reader.ReadExactAsync(new(selection, handle));
        Assert.NotNull(first.Next);
        var second = await reader.ReadExactAsync(new(selection, handle, first.Next));
        Assert.Null(second.Next);
        Assert.Equal(handle, second.Document!.Handle);
        Assert.Equal(fixture.Documents[0].FirstChunk + fixture.Documents[0].SecondChunk, first.Content + second.Content);
        var strictUtf8 = new UTF8Encoding(false, true);
        foreach (var page in new[] { first, second })
        {
            Assert.InRange(strictUtf8.GetByteCount(page.Content!), 1, KnowledgeContract.MaximumContentBytes);
            Assert.Equal(page.Content, strictUtf8.GetString(strictUtf8.GetBytes(page.Content!)));
            Assert.Contains(page.Claims, c => c.Qualification == ClaimQualification.Advisory);
            Assert.Contains(page.Claims, c => c.Qualification == ClaimQualification.SourceSupported);
        }
        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, (await reader.ReadExactAsync(new(selection, handle, new(first.Next!.Value + "tampered")))).Status);
        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, (await reader.ReadExactAsync(new(selection, fixture.Handle(fixture.ClB), first.Next))).Status);
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await reader.ReadExactAsync(new(selection, handle with { PublicationId = fixture.EarlyPublicationId }))).Status);
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await reader.ReadExactAsync(new(selection, handle with { SectionId = "missing" }))).Status);
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await reader.OpenReadSelectionAsync(fixture.Selection() with { Publication = new(fixture.Binding with { CanonicalDatabaseIdentity = "wrong-database" }, fixture.PublicationId) })).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_Continuation_QuerySelectionAndModeRemainBoundAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        var first = await reader.SearchAsync(new(selection, "", 1));
        Assert.NotNull(first.Next);
        var second = await reader.SearchAsync(new(selection, "", 1, first.Next));
        Assert.NotEqual(first.Documents[0].Handle, second.Documents[0].Handle);
        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, (await reader.SearchAsync(new(selection, "other", 1, first.Next))).Status);
        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, (await reader.SearchAsync(new(selection, "", 2, first.Next))).Status);
        var business = (await reader.OpenReadSelectionAsync(fixture.Selection(KnowledgeMode.Business))).Selection!;
        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, (await reader.SearchAsync(new(business, "", 1, first.Next))).Status);
        var related = await reader.GetRelatedAsync(new(selection, fixture.Handle(fixture.ClA)));
        Assert.Equal(fixture.Handle(fixture.ClB), Assert.Single(related.Relationships).To);
        Assert.Equal(fixture.Unknown.Key, Assert.Single(related.PermittedUncertainties).Key);
        Assert.Empty((await reader.GetRelatedAsync(new(selection, fixture.Handle(fixture.ClB)))).Relationships);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_RevokedContributorOrAuthorityOutage_FailsClosedAfterMaterializationAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var authority = new ControlledDisclosurePolicy();
        var model = new ControlledKnowledgeReader(fixture, authority);
        IOctoberKnowledgeReader reader = model;
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        model.BeforeDisclosure = () => authority.RevokedRepositories.Add(fixture.RepositoryB);
        var denied = await reader.ReadExactAsync(new(selection, fixture.Handle(fixture.BlA)));
        Assert.Equal(KnowledgeReadStatus.Unavailable, denied.Status);
        Assert.Null(denied.Document);
        Assert.Null(denied.Content);
        Assert.True(authority.CheckCount >= 3);
        authority.LiveStatus = DisclosureStatus.AuthorityUnavailable;
        var unavailable = await reader.SearchAsync(new(selection, ""));
        Assert.Empty(unavailable.Documents);
        Assert.Null(unavailable.Next);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_IdenticalReplayAndChangedDigest_PreservesExactResultOrConflictsAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var model = new ControlledKnowledgeWriter(fixture, new());
        IOctoberKnowledgeWriter writer = model;
        var submission = Submission(fixture);
        var committed = await writer.AcceptCompletedFileAsync(submission);
        Assert.Equal(new[] { fixture.ClA, fixture.ClAHelper }, committed.AcceptedFile!.Documents);
        var replay = await writer.AcceptCompletedFileAsync(submission);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(committed.AcceptedFile, replay.AcceptedFile);
        var changed = submission.Proposal with { CanonicalProposalDigest = "changed-content" };
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await writer.AcceptCompletedFileAsync(submission with { Proposal = changed, Proof = new ControlledCompletedProof(changed) })).Status);
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await writer.LookupOperationAsync(fixture.Binding, submission.Operation, "changed-content")).Status);
        Assert.Equal(1, model.TransactionsStarted);
        Assert.Equal(1, model.AcceptedRows);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcceptCompletedFileAsync_ZeroUnitsAndUntrustedProof_DistinguishesCoverageFromAuthorityAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var model = new ControlledKnowledgeWriter(fixture, new());
        IOctoberKnowledgeWriter writer = model;
        var denied = await writer.AcceptCompletedFileAsync(Submission(fixture) with { Proof = new UntrustedControlledProof(fixture.MultiUnit) });
        Assert.Equal(KnowledgeWriteStatus.Denied, denied.Status);
        Assert.Equal(0, model.TransactionsStarted);
        var proposal = fixture.ZeroUnit;
        var accepted = await writer.AcceptCompletedFileAsync(new(new(fixture.Binding.BatchId, OperationKind.AcceptFile, OctoberKnowledgeFixture.Id(82)), proposal, new ControlledCompletedProof(proposal)));
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        Assert.Empty(accepted.AcceptedFile!.Documents);
        Assert.Equal(CoverageDisposition.CompletedZeroUnit, accepted.AcceptedFile.Coverage.Disposition);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AcquireProviderAttemptAsync_RetryAfterUnknownUsage_RetainsReservationAndOriginalAllowanceAsync()
    {
        var model = new ControlledExecutionScope { RateAllowed = false };
        IOctoberExecutionScope scope = model;
        var waiting = scope.AcquireProviderAttemptAsync(Attempt(100));
        Assert.False(waiting.IsCompleted);
        model.RateAllowed = true;
        model.Pump();
        var first = (await waiting).Lease!;
        await first.CheckBeforeSendAsync();
        await first.SettleAsync(Unknown with { Outcome = ProviderAttemptOutcome.Failed });
        await first.DisposeAsync();
        var retry = (await scope.AcquireProviderAttemptAsync(Attempt(101))).Lease!;
        await retry.CheckBeforeSendAsync();
        await retry.SettleAsync(Unknown);
        await retry.DisposeAsync();
        Assert.Equal(20, model.RemainingTokens);
        Assert.Equal(2, model.RemainingRequests);
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, (await scope.AcquireProviderAttemptAsync(Attempt(102))).Status);
        Assert.Equal(2, model.Sends);
        Assert.All(model.Usage, usage => Assert.Null(usage.InputTokens));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(BusinessFinalizationStatus.Published)]
    [InlineData(BusinessFinalizationStatus.PublishedPartial)]
    [InlineData(BusinessFinalizationStatus.NoContent)]
    [InlineData(BusinessFinalizationStatus.Failed)]
    [InlineData(BusinessFinalizationStatus.Denied)]
    [InlineData(BusinessFinalizationStatus.AuthorityUnavailable)]
    [InlineData(BusinessFinalizationStatus.HardStopped)]
    [InlineData(BusinessFinalizationStatus.CommitIndeterminate)]
    public async Task FinalizeAsync_ExplicitOutcomeAndReplay_PreservesPublicationAndCoverageAsync(BusinessFinalizationStatus status)
    {
        var fixture = new OctoberKnowledgeFixture();
        var scope = new ControlledExecutionScope();
        var operation = new OperationKey(fixture.Binding.BatchId, OperationKind.FinalizeCorpus, OctoberKnowledgeFixture.Id(91));
        var publication = status is BusinessFinalizationStatus.Published or BusinessFinalizationStatus.PublishedPartial ? new PublicationBinding(fixture.Binding, fixture.PublicationId) : null;
        var result = new BusinessFinalizationResult(status, operation, publication, new(status != BusinessFinalizationStatus.Published, ["ControlledFixture coverage"]), IsReplay: true);
        IOctoberBusinessFinalizer finalizer = new ControlledFinalizer(result);
        var request = new FinalizationRequest(operation, fixture.EarlyPublication.Corpus, "controlled-finalization", scope);
        var actual = await finalizer.FinalizeAsync(request);
        Assert.Equal(status, actual.Status);
        Assert.Equal(publication, actual.Publication);
        Assert.Equal(result.Coverage, actual.Coverage);
        Assert.True(actual.IsReplay);
        Assert.Equal(0, scope.Sends);
        var missing = await finalizer.FinalizeAsync(request with { ExecutionScope = null! });
        Assert.Equal(BusinessFinalizationStatus.Failed, missing.Status);
        Assert.Null(missing.Publication);
        Assert.Equal(0, scope.Grants);
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DisclosureStatus.Denied, KnowledgeWriteStatus.Denied)]
    [InlineData(DisclosureStatus.AuthorityUnavailable, KnowledgeWriteStatus.AuthorityUnavailable)]
    public async Task AcceptCompletedFileAsync_AuthorityLostDuringStaging_RollsBackBeforeSubmissionAsync(DisclosureStatus authority, KnowledgeWriteStatus expected)
    {
        var fixture = new OctoberKnowledgeFixture();
        var guard = new ControlledCommitGuard();
        var model = new ControlledKnowledgeWriter(fixture, guard);
        model.AfterStaging = () => model.Authority = authority;
        IOctoberKnowledgeWriter writer = model;
        var result = await writer.AcceptCompletedFileAsync(Submission(fixture));
        Assert.Equal(expected, result.Status);
        Assert.Null(result.AcceptedFile);
        Assert.Equal(1, model.TransactionsStarted);
        Assert.Equal(1, model.StagedRollbacks);
        Assert.Equal(0, guard.Submissions);
        Assert.Equal(0, model.AcceptedRows);
    }
}

using System.Collections.Immutable;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Actual B0 writer/published source/reader on owned SQLite; controlled authority, no Business finalizer or provider.</summary>
public sealed class OctoberScanClPublicationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(true, false, 0)]
    [InlineData(false, false, 2)]
    [InlineData(true, true, 5)]
    public async Task Execute_ClosedClCorpus_ReportsExactRequestedStageAndHonestCoverageAsync(bool complete, bool zeroUnit, int exitCode)
    {
        await using var harness = await PhaseHarness.CreateAsync(complete, zeroUnit);
        var report = await harness.Phase.ExecuteAsync();
        Assert.Equal(exitCode, report.ExitCode);
        Assert.Equal("cl-only", report.RequestedStage);
        Assert.Equal(complete ? OctoberCoverageStatus.Complete : OctoberCoverageStatus.Partial, report.Coverage);
        Assert.Equal(harness.Request.TerminalCoverage.ToArray(), report.TerminalCoverage.ToArray());
        Assert.Equal(1, harness.FreezeCalls);
        if (zeroUnit)
        {
            Assert.Equal(OctoberPublicationStatus.NoContent, report.PublicationStatus);
            Assert.Empty(report.Documents);
        }
        else
        {
            Assert.Equal("cl-only", report.CompletedStage);
            Assert.Equal(OctoberPublicationStatus.Confirmed, report.PublicationStatus);
            Assert.NotNull(report.Publication);
            Assert.Equal(harness.Request.Binding, report.Publication.Knowledge);
            Assert.Equal(harness.Fixture.Accepted.Documents.Select(item => item.DocumentVersionId).Order(), report.Documents.Select(item => item.DocumentVersionId).Order());
            Assert.All(report.Documents, item => Assert.Equal(report.Publication.PublicationId, item.PublicationId));
            Assert.Equal(1, harness.PublishCalls);
        }
        Assert.Equal(0, report.Usage.Requests);
        await using var context = harness.Fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.CorpusSelections.CountAsync());
        Assert.Equal(0, await context.LogicalDocuments.CountAsync(item => item.Kind == DocumentKind.BusinessLogic));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Execute_PublishedCl_IsReadableBySeparateOrdinaryItReaderAndHiddenFromBusinessOrRevokedReaderAsync()
    {
        await using var harness = await PhaseHarness.CreateAsync();
        var report = await harness.Phase.ExecuteAsync();
        Assert.Equal(0, report.ExitCode);
        var publication = Assert.IsType<PublicationBinding>(report.Publication);
        var policy = new B0bReaderDisclosure { CurrentSubject = "ordinary-reader" };
        var reader = new OctoberKnowledgeReader(new OctoberKnowledgePublishedSource(harness.Fixture.Acceptance.Factory), publication, policy,
            new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()), new B0bReadLog());
        var subject = new KnowledgeSubject("ordinary-reader", "separate-reader-authentication");
        var opened = await reader.OpenReadSelectionAsync(new(publication, subject, KnowledgeMode.IT, KnowledgePurpose.Reader, [harness.Fixture.Acceptance.Origin.RepositoryId]));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        var handle = report.Documents[0];
        var page = await reader.ReadExactAsync(new(opened.Selection!, handle));
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        Assert.Equal(handle, page.Document!.Handle);
        Assert.NotEmpty(page.Content!);
        var related = await reader.GetRelatedAsync(new(opened.Selection!, handle));
        Assert.Contains(related.PermittedUncertainties, uncertainty => uncertainty.Key == "unknown-destination");
        var business = await reader.OpenReadSelectionAsync(new(publication, subject, KnowledgeMode.Business, KnowledgePurpose.Reader, [harness.Fixture.Acceptance.Origin.RepositoryId]));
        Assert.Equal(KnowledgeReadStatus.Success, business.Status);
        var businessPage = await reader.ReadExactAsync(new(business.Selection!, handle));
        Assert.Equal(KnowledgeReadStatus.Unavailable, businessPage.Status);
        Assert.Null(businessPage.Document);
        Assert.Null(businessPage.Content);
        Assert.Empty((await reader.SearchAsync(new(business.Selection!, ""))).Documents);
        policy.Status = DisclosureStatus.Denied;
        var denied = await reader.ReadExactAsync(new(opened.Selection!, handle));
        Assert.Equal(KnowledgeReadStatus.Unavailable, denied.Status);
        Assert.Null(denied.Document);
        Assert.Null(denied.Content);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(CorpusFreezeEligibility.Unsettled)]
    [InlineData(CorpusFreezeEligibility.AdmissionOpen)]
    public async Task Execute_MissingTerminalInventoryAuthority_CannotPublishFromCompleteBooleanAsync(CorpusFreezeEligibility status)
    {
        await using var harness = await PhaseHarness.CreateAsync();
        harness.Fixture.Acceptance.FreezeAuthority.Eligibility = status;
        var report = await harness.Phase.ExecuteAsync();
        Assert.NotEqual(0, report.ExitCode);
        Assert.Null(report.Publication);
        Assert.Empty(report.Documents);
        Assert.Equal(0, harness.PublishCalls);
        await using var context = harness.Fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(0, await context.CorpusSelections.CountAsync());
        Assert.Equal(0, await context.KnowledgePublications.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Execute_FreezeAuthorityLostAfterFreeze_RechecksBeforePublicationSubmissionAsync()
    {
        await using var harness = await PhaseHarness.CreateAsync();
        harness.AfterFreeze = () => harness.Fixture.Acceptance.FreezeAuthority.Eligibility = CorpusFreezeEligibility.Unsettled;
        var report = await harness.Phase.ExecuteAsync();
        Assert.NotEqual(0, report.ExitCode);
        Assert.Null(report.Publication);
        await using var context = harness.Fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.CorpusSelections.CountAsync());
        Assert.Equal(0, await context.KnowledgePublications.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Execute_LostPublicationAcknowledgement_UsesExactLookupWithoutDuplicatePublicationAsync()
    {
        await using var harness = await PhaseHarness.CreateAsync();
        harness.LosePublicationAcknowledgement = true;
        var initial = await harness.Phase.ExecuteAsync();
        Assert.Contains(initial.ExitCode, new[] { 0, 4 });
        var report = await harness.Phase.ReconcileAsync();
        Assert.Equal(0, report.ExitCode);
        Assert.NotNull(report.Publication);
        Assert.Equal(1, harness.FreezeCalls);
        Assert.Equal(1, harness.PublishCalls);
        Assert.NotEmpty(harness.Lookups);
        Assert.All(harness.Lookups, item => Assert.Contains(item, new[] { harness.Request.FreezeOperation, harness.Request.PublicationOperation }));
        await using var context = harness.Fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Execute_MeasuresAwaitedStoreTimeAndRetainsRemainingDeadlineAsync()
    {
        await using var harness = await PhaseHarness.CreateAsync();
        harness.BeforeStore = () => harness.Clock.Advance(TimeSpan.FromSeconds(1));
        var report = await harness.Phase.ExecuteAsync();
        Assert.Equal(0, report.ExitCode);
        Assert.True(report.Timings.StoreWait >= TimeSpan.FromSeconds(2));
        Assert.True(report.Timings.Elapsed >= report.Timings.StoreWait);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Execute_OriginalRemainingBudgetExpiresAfterFreeze_DoesNotRenewForPublicationAsync()
    {
        await using var harness = await PhaseHarness.CreateAsync();
        harness.AfterFreeze = () => harness.Clock.Advance(TimeSpan.FromMinutes(2), deliverTimers: false);
        var report = await harness.Phase.ExecuteAsync();
        Assert.NotEqual(0, report.ExitCode);
        Assert.Null(report.Publication);
        await using var context = harness.Fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(0, await context.KnowledgePublications.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Execute_ClAndBlRequest_CannotSilentlyDowngradeThroughClOnlyComponentAsync()
    {
        await using var harness = await PhaseHarness.CreateAsync(stage: "cl-and-bl");
        var report = await harness.Phase.ExecuteAsync();
        Assert.Equal(1, report.ExitCode);
        Assert.Equal("cl-and-bl", report.RequestedStage);
        Assert.Null(report.CompletedStage);
        Assert.Equal(0, harness.FreezeCalls);
        Assert.Equal(0, harness.PublishCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberCoverageStatus.Complete, OctoberExecutionStatus.Completed, OctoberPublicationStatus.Confirmed, 0)]
    [InlineData(OctoberCoverageStatus.Partial, OctoberExecutionStatus.Completed, OctoberPublicationStatus.Confirmed, 2)]
    [InlineData(OctoberCoverageStatus.None, OctoberExecutionStatus.Failed, OctoberPublicationStatus.Failed, 1)]
    [InlineData(OctoberCoverageStatus.Partial, OctoberExecutionStatus.Interrupted, OctoberPublicationStatus.NotRequested, 3)]
    [InlineData(OctoberCoverageStatus.Partial, OctoberExecutionStatus.Indeterminate, OctoberPublicationStatus.Indeterminate, 4)]
    [InlineData(OctoberCoverageStatus.Complete, OctoberExecutionStatus.Completed, OctoberPublicationStatus.NoContent, 5)]
    public void Report_IndependentOutcomeFields_MapStageLabelledExitCodes(OctoberCoverageStatus coverage, OctoberExecutionStatus execution, OctoberPublicationStatus publicationStatus, int expected)
    {
        var publication = publicationStatus == OctoberPublicationStatus.Confirmed ? new PublicationBinding(new("owned-db", "target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid()) : null;
        var report = new OctoberScanReport("cl-only", publication is null ? null : "cl-only", coverage, execution, publicationStatus, publication, publication is null ? [] : [new(publication.Knowledge.BatchId, publication.PublicationId, Guid.NewGuid(), Guid.NewGuid())], [], [],
            new(TimeSpan.FromSeconds(3), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(2)), new(1, null, null, true), []);
        Assert.Equal(expected, report.ExitCode);
        Assert.Null(report.Usage.InputTokens);
        Assert.Null(report.Usage.OutputTokens);
        Assert.True(report.Usage.HasUnknownUsage);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Execute_LostFreezeAcknowledgementAndAbsentLookup_BlocksPublishUntilExactSettlementAsync()
    {
        await using var harness = await PhaseHarness.CreateAsync();
        harness.LoseFreezeAcknowledgement = true;
        harness.HideFreezeLookup = true;
        try
        {
            Assert.Equal(4, (await harness.Phase.ExecuteAsync()).ExitCode);
            Assert.Equal(4, (await harness.Phase.ReconcileAsync()).ExitCode);
            Assert.Equal(1, harness.FreezeCalls);
            Assert.Equal(0, harness.PublishCalls);
            await using (var context = harness.Fixture.Acceptance.Factory.CreateDbContext())
            {
                Assert.Equal(1, await context.CorpusSelections.CountAsync());
                Assert.Equal(0, await context.KnowledgePublications.CountAsync());
            }
            harness.HideFreezeLookup = false;
            var settled = await harness.Phase.ReconcileAsync();
            Assert.Equal(0, settled.ExitCode);
            Assert.NotNull(settled.Publication);
            Assert.Equal(1, harness.FreezeCalls);
            Assert.Equal(1, harness.PublishCalls);
            Assert.Contains(harness.Request.FreezeOperation, harness.Lookups);
        }
        finally
        {
            harness.HideFreezeLookup = false;
            await harness.Phase.ReconcileAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Execute_PublicationCommitsThenAuthorityRevoked_RetainsHistoryWithoutDisclosingHandlesAsync()
    {
        await using var harness = await PhaseHarness.CreateAsync();
        harness.AfterPublish = () => harness.Authority = DisclosureStatus.Denied;
        var initial = await harness.Phase.ExecuteAsync();
        Assert.Equal(1, initial.ExitCode);
        Assert.Null(initial.Publication);
        Assert.Empty(initial.Documents);
        Assert.Empty(initial.AcceptedFiles);
        var reconciled = await harness.Phase.ReconcileAsync();
        Assert.Equal(1, reconciled.ExitCode);
        Assert.Null(reconciled.Publication);
        Assert.Empty(reconciled.Documents);
        Assert.Empty(reconciled.AcceptedFiles);
        Assert.Equal(1, harness.PublishCalls);
        await using var context = harness.Fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Execute_HardStopBeforeWritesOrAfterKnownFreeze_ReportsInterruptedWithoutPublicationAsync(bool afterFreeze, bool zeroUnit)
    {
        using var stop = new CancellationTokenSource();
        await using var harness = await PhaseHarness.CreateAsync(zeroUnit: zeroUnit, hardStop: stop.Token);
        if (afterFreeze) harness.AfterFreeze = stop.Cancel;
        else stop.Cancel();
        var report = await harness.Phase.ExecuteAsync();
        Assert.Equal(3, report.ExitCode);
        Assert.Equal(OctoberExecutionStatus.Interrupted, report.Execution);
        Assert.Null(report.Publication);
        Assert.Empty(report.Documents);
        Assert.Null(report.CompletedStage);
        Assert.Equal(afterFreeze ? 1 : 0, harness.FreezeCalls);
        Assert.Equal(0, harness.PublishCalls);
        await using var context = harness.Fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(afterFreeze ? 1 : 0, await context.CorpusSelections.CountAsync());
        Assert.Equal(0, await context.KnowledgePublications.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Execute_KnownPublicationThenHardStop_ReturnsTerminalPublicationWithoutAnotherWriteAsync()
    {
        using var stop = new CancellationTokenSource();
        await using var harness = await PhaseHarness.CreateAsync(hardStop: stop.Token);
        harness.AfterPublish = stop.Cancel;
        var completed = await harness.Phase.ExecuteAsync();
        Assert.Equal(0, completed.ExitCode);
        Assert.Equal("cl-only", completed.CompletedStage);
        Assert.NotNull(completed.Publication);
        Assert.NotEmpty(completed.Documents);
        var replay = await harness.Phase.ReconcileAsync();
        Assert.Equal(0, replay.ExitCode);
        Assert.Equal(completed.Publication, replay.Publication);
        Assert.Equal(completed.Documents.ToArray(), replay.Documents.ToArray());
        Assert.Equal(1, harness.FreezeCalls);
        Assert.Equal(1, harness.PublishCalls);
    }

    private sealed class PhaseHarness : IAsyncDisposable
    {
        private PhaseHarness(OctoberKnowledgePublicationFixture fixture, bool complete, string stage, CancellationToken hardStop)
        {
            Fixture = fixture;
            var freeze = fixture.FreezeRequest(includeGap: !complete);
            fixture.Acceptance.FreezeAuthority.Approve(freeze);
            Request = new(fixture.Acceptance.Binding, freeze.Operation, new(fixture.Acceptance.Binding.BatchId, OperationKind.PublishSelection, OctoberKnowledgeAcceptanceFixture.Id(412)),
                freeze.AcceptedFiles, freeze.TerminalCoverage, fixture.Accepted.Documents.IsEmpty ? [] : fixture.Acceptance.Proposal.Uncertainties, complete, stage);
            Phase = new(Request, (guard, freezeAuthority) => new ObservedWriter(this, new OctoberKnowledgeWriter(fixture.Acceptance.Factory,
                fixture.Acceptance.Completion, fixture.Acceptance.Source, fixture.Acceptance.Authorization, guard, fixture.Acceptance.Log, freezeAuthority)),
                fixture.Acceptance.FreezeAuthority, _ => Task.FromResult(Authority), TimeSpan.FromMinutes(1), Clock, hardStop);
        }
        internal OctoberKnowledgePublicationFixture Fixture { get; }
        internal OctoberClPublicationRequest Request { get; }
        internal OctoberClPublicationPhase Phase { get; }
        internal OctoberScanAcceptanceHarness.ManualClock Clock { get; } = new();
        internal int FreezeCalls { get; private set; }
        internal int PublishCalls { get; private set; }
        internal bool LosePublicationAcknowledgement { get; set; }
        internal bool LoseFreezeAcknowledgement { get; set; }
        internal bool HideFreezeLookup { get; set; }
        internal DisclosureStatus Authority { get; set; } = DisclosureStatus.Allowed;
        internal Action? AfterPublish { get; set; }
        internal Action? AfterFreeze { get; set; }
        internal Action? BeforeStore { get; set; }
        internal List<OperationKey> Lookups { get; } = [];
        internal static async Task<PhaseHarness> CreateAsync(bool complete = true, bool zeroUnit = false, string stage = "cl-only", CancellationToken hardStop = default) => new(await OctoberKnowledgePublicationFixture.CreateAsync(zeroUnit, includeUncitedContributor: true), complete, stage, hardStop);
        public async ValueTask DisposeAsync() { await Phase.DisposeAsync(); await Fixture.DisposeAsync(); }
        private sealed class ObservedWriter(PhaseHarness owner, IOctoberKnowledgeWriter writer) : IOctoberKnowledgeWriter
        {
            public Task<KnowledgeWriteResult> AcceptCompletedFileAsync(CompletedFileSubmission submission, CancellationToken cancellationToken = default) => throw new InvalidOperationException("CL publication phase cannot rerun file acceptance.");
            public async Task<KnowledgeWriteResult> FreezeCorpusAsync(CorpusFreezeRequest request, CancellationToken cancellationToken = default)
            {
                owner.FreezeCalls++; owner.BeforeStore?.Invoke();
                Assert.Equal(owner.Request.FreezeOperation, request.Operation);
                var result = await writer.FreezeCorpusAsync(request, cancellationToken);
                owner.AfterFreeze?.Invoke();
                return owner.LoseFreezeAcknowledgement && result.Status == KnowledgeWriteStatus.Committed ? new(KnowledgeWriteStatus.CommitIndeterminate, request.Operation) : result;
            }
            public async Task<KnowledgeWriteResult> PublishSelectionAsync(PublicationRequest request, CancellationToken cancellationToken = default)
            {
                owner.PublishCalls++; owner.BeforeStore?.Invoke();
                Assert.Equal(owner.Request.PublicationOperation, request.Operation);
                Assert.Empty(request.Flows); Assert.Empty(request.Relationships);
                Assert.Equal(owner.Request.Uncertainties.ToArray(), request.Uncertainties.ToArray());
                if (!request.Documents.IsEmpty) Assert.NotEmpty(request.Uncertainties);
                var result = await writer.PublishSelectionAsync(request, cancellationToken);
                owner.AfterPublish?.Invoke();
                return owner.LosePublicationAcknowledgement && result.Status == KnowledgeWriteStatus.Committed ? new(KnowledgeWriteStatus.CommitIndeterminate, request.Operation) : result;
            }
            public Task<KnowledgeWriteResult> LookupOperationAsync(KnowledgeBinding binding, OperationKey operation, string payloadDigest, CancellationToken cancellationToken = default)
            {
                Assert.Equal(owner.Request.Binding, binding); owner.Lookups.Add(operation);
                if (owner.HideFreezeLookup && operation == owner.Request.FreezeOperation)
                    return Task.FromResult(new KnowledgeWriteResult(KnowledgeWriteStatus.CommitIndeterminate, operation));
                return writer.LookupOperationAsync(binding, operation, payloadDigest, cancellationToken);
            }
        }
    }
}

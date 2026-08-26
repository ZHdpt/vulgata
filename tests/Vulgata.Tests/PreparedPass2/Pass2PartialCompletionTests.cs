using System.Reflection;
using Vulgata.Agents.WorkerExecution;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Web.Data;
using Xunit.Sdk;

namespace Vulgata.Tests.PreparedPass2;

public sealed class Pass2PartialCompletionTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatePlannedFlowGroup_IsDocumentlessUntilPublished()
    {
        FlowGroup group = CreateCompleteGroup();

        Assert.Equal("Complete", group.State);
        Assert.Null(group.BusinessLogicDocumentVersionId);

        group.Publish(Id(90_100));

        Assert.Equal("Published", group.State);
        Assert.Equal(Id(90_100), group.BusinessLogicDocumentVersionId);
        Assert.Throws<InvalidOperationException>(() => group.Publish(Id(90_101)));
    }

    [Fact]
    public void CreateIncompleteFlowGroup_IsTerminalAndHasNoDocument()
    {
        FlowGroup group = FlowGroup.CreateIncomplete(
            Id(90_001),
            Id(90_002),
            Id(90_003),
            ExactResolutionProtocol.CustomRpc,
            "rpc://bank/customer",
            "CustomerClient.Load",
            new string('a', 64),
            "business-flow/v1/incomplete",
            Id(90_010),
            Id(90_011),
            Id(90_012),
            Id(90_013),
            Id(90_014),
            Id(90_015),
            Id(90_016),
            1,
            Hash('1'),
            Hash('2'),
            "missing-provider-implementation",
            CreatedAt);

        Assert.Equal("Incomplete", group.State);
        Assert.Equal("missing-provider-implementation", group.ReasonCode);
        Assert.Null(group.BusinessLogicDocumentVersionId);
    }

    [Fact]
    public void CreateBoundExcludedFlowGroup_IsTerminalAndHasNoDocument()
    {
        FlowGroup group = FlowGroup.CreateBoundExcluded(
            Id(90_021),
            Id(90_022),
            Id(90_023),
            ExactResolutionProtocol.CustomRpc,
            "rpc://bank/customer",
            "CustomerClient.Load",
            new string('b', 64),
            "business-flow/v1/bound",
            Id(90_030),
            Id(90_031),
            Id(90_032),
            Id(90_033),
            Id(90_034),
            Id(90_035),
            Id(90_036),
            Id(90_037),
            2,
            Hash('3'),
            Hash('4'),
            CreatedAt);

        Assert.Equal("BoundExcluded", group.State);
        Assert.Equal("flow-group-bound", group.ReasonCode);
        Assert.Null(group.BusinessLogicDocumentVersionId);
    }

    [Fact]
    public void BoundExcludedOmission_IsContentDarkAndReasonBounded()
    {
        Pass2Omission omission = Pass2Omission.CreateBoundExcluded(
            Id(90_041),
            Id(90_042),
            Id(90_043),
            CreatedAt);

        Assert.Equal(Pass2OmissionState.Omitted, omission.State);
        Assert.Equal("flow-group-bound", omission.ReasonCode);
        Assert.Equal(Id(90_042), omission.Pass2ExecutionId);
        Assert.Equal(Id(90_043), omission.ExactResolutionDecisionId);
    }

    [Fact]
    public void Pass2Execution_RecordsPublishedAndFailedTerminalCountsWithoutChangingResolution()
    {
        Pass2Execution execution = Pass2Execution.CreatePlanning(
            Id(90_051),
            Id(90_052),
            Id(90_053),
            Id(90_054),
            CreatedAt);
        execution.MarkRunning(CreatedAt.AddSeconds(1));

        execution.RecordPublished(1, CreatedAt.AddSeconds(2));
        execution.RecordFailed(2, CreatedAt.AddSeconds(3));

        Assert.Equal(Pass2ExecutionState.Running, execution.State);
        Assert.Equal(1, execution.PublishedCount);
        Assert.Equal(1, execution.FailedCount);
        Assert.Equal(3, execution.StateVersion);
    }

    [Fact]
    public void Pass2Execution_ZeroPlannedDenominator_DoesNotDeriveFromTerminalGraph()
    {
        Pass2Execution execution = Pass2Execution.CreatePlanning(
            Id(90_055), Id(90_056), Id(90_057), Id(90_058), CreatedAt);
        execution.RecordPlan(0);
        execution.MarkRunning(CreatedAt.AddSeconds(1));
        execution.RecordFailed(execution.StateVersion, CreatedAt.AddSeconds(2));

        Assert.Throws<InvalidOperationException>(() => execution.Complete(
            execution.StateVersion,
            withWarnings: true,
            incompleteCount: 0,
            boundExcludedCount: 0,
            omissionCount: 0,
            terminalWarningInventory: "[]",
            CreatedAt.AddSeconds(3)));
        Assert.Equal(0, execution.PlannedCount);
        Assert.Equal(Pass2ExecutionState.Running, execution.State);
    }

    [Fact]
    public async Task RecoverAsync_TerminalDenominatorDrift_RefusesInsteadOfFallingBackAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        await using VulgataDbContext context = fixture.Database.CreateContext();
        object seed = await SeedPass2Async(context, fixture);
        Pass1PairExecution pair = Property<Pass1PairExecution>(seed, "Pair");
        RepositoryAccessScope scope = AuthorizedScope(seed);
        await BeginPass2Async(context);
        var store = CreateAuthorizedStore(context);
        PreparedPass2StartResult start = await store.StartAsync(pair.Id, scope);
        Guid executionId = Assert.IsType<Guid>(start.ExecutionId);
        DateTimeOffset now = (await context.Pass2JobExecutions.SingleAsync()).StateChangedAt.AddMinutes(1);
        PreparedPass2ClaimResult first = await store.ClaimNextAsync(
            executionId, now, TimeSpan.FromMinutes(5));
        Assert.Equal(
            PreparedPass2DispatchFailureOutcome.RetryScheduled,
            await store.RecordDispatchFailureAsync(
                executionId,
                Assert.IsType<Guid>(first.JobId),
                first.JobStateVersion,
                Assert.IsType<Guid>(first.LeaseToken),
                ScanJobFailureCategory.JobPermanent,
                "terminal-drift",
                now.AddSeconds(1)));
        PreparedPass2ClaimResult retry = await store.ClaimNextAsync(
            executionId, now.AddSeconds(3), TimeSpan.FromMinutes(5));
        Assert.Equal(
            PreparedPass2DispatchFailureOutcome.Failed,
            await store.RecordDispatchFailureAsync(
                executionId,
                Assert.IsType<Guid>(retry.JobId),
                retry.JobStateVersion,
                Assert.IsType<Guid>(retry.LeaseToken),
                ScanJobFailureCategory.JobPermanent,
                "terminal-drift",
                now.AddSeconds(4)));
        await context.Database.ExecuteSqlRawAsync("PRAGMA ignore_check_constraints = ON");
        await context.Pass2Executions
            .Where(value => value.Id == executionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.PlannedCount, 0));
        await context.Database.ExecuteSqlRawAsync("PRAGMA ignore_check_constraints = OFF");
        context.ChangeTracker.Clear();

        PreparedPass2RecoveryResult recovery = await store.RecoverAsync(
            executionId, scope, providerAvailable: true, now.AddSeconds(5));
        Pass2Execution persisted = await context.Pass2Executions.AsNoTracking()
            .SingleAsync(value => value.Id == executionId);

        Assert.Equal(PreparedPass2RecoveryOutcome.Rejected, recovery.Outcome);
        Assert.Equal(Pass2ExecutionState.FailedIntegrity, persisted.State);
        Assert.Equal(0, persisted.PlannedCount);
    }

    [Fact]
    public void Pass2JobExecution_CompletedPublicationRetainsImmutableDocumentIdentity()
    {
        Pass2JobExecution job = Pass2JobExecution.CreatePending(
            Id(90_061),
            Id(90_062),
            Id(90_063),
            1,
            CreatedAt);
        Guid lease = Id(90_064);
        Guid documentVersion = Id(90_065);
        job.AcquireLease(lease, CreatedAt.AddMinutes(5), CreatedAt.AddSeconds(1));

        job.Complete(1, lease, documentVersion, Hash('5'), CreatedAt.AddSeconds(2));

        Assert.Equal(Pass2JobExecutionState.Completed, job.State);
        Assert.Equal(documentVersion, job.CommittedDocumentVersionId);
        Assert.Equal(Hash('5'), job.CompletionFingerprint);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseExpiresAt);
    }

    [Fact]
    public async Task Coordinator_ExhaustedJobWithNoPublishedDocument_CompletesWithWarningsAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        await using VulgataDbContext context = fixture.Database.CreateContext();
        object seed = await SeedPass2Async(context, fixture);
        Pass1PairExecution pair = Property<Pass1PairExecution>(seed, "Pair");
        RepositoryAccessScope scope = AuthorizedScope(seed);
        await BeginPass2Async(context);
        var store = CreateAuthorizedStore(context);

        PreparedPass2StartResult start = await store.StartAsync(pair.Id, scope);
        Assert.Equal(PreparedPass2StartOutcome.Created, start.Outcome);
        Guid executionId = Assert.IsType<Guid>(start.ExecutionId);

        DateTimeOffset firstNow = await context.Pass2JobExecutions
            .Where(value => value.Pass2ExecutionId == executionId)
            .Select(value => value.StateChangedAt)
            .SingleAsync();
        firstNow = firstNow.AddMinutes(1);
        PreparedPass2ClaimResult claim = await store.ClaimNextAsync(
            executionId,
            firstNow,
            TimeSpan.FromMinutes(5));
        Assert.Equal(PreparedPass2ClaimOutcome.Claimed, claim.Outcome);
        Guid jobId = Assert.IsType<Guid>(claim.JobId);
        Guid firstLease = Assert.IsType<Guid>(claim.LeaseToken);

        PreparedPass2DispatchFailureOutcome retry = await store.RecordDispatchFailureAsync(
            executionId,
            jobId,
            claim.JobStateVersion,
            firstLease,
            ScanJobFailureCategory.JobPermanent,
            "missing-business-document",
            firstNow.AddSeconds(1));
        Assert.Equal(PreparedPass2DispatchFailureOutcome.RetryScheduled, retry);

        PreparedPass2ClaimResult retryClaim = await store.ClaimNextAsync(
            executionId,
            firstNow.AddSeconds(3),
            TimeSpan.FromMinutes(5));
        Assert.Equal(PreparedPass2ClaimOutcome.Claimed, retryClaim.Outcome);
        Guid secondLease = Assert.IsType<Guid>(retryClaim.LeaseToken);

        PreparedPass2DispatchFailureOutcome failed = await store.RecordDispatchFailureAsync(
            executionId,
            jobId,
            retryClaim.JobStateVersion,
            secondLease,
            ScanJobFailureCategory.JobPermanent,
            "missing-business-document",
            firstNow.AddSeconds(4));
        Assert.Equal(PreparedPass2DispatchFailureOutcome.Failed, failed);

        PreparedPass2ExecutionStatus status = await CreateCoordinator(context, store)
            .GetStatusAsync(executionId, AdministratorUser())
            ?? throw new XunitException("The terminal Pass 2 execution was not visible.");
        FlowGroup group = await context.FlowGroups.SingleAsync(
            value => value.Pass2ExecutionId == executionId);

        Assert.Equal(nameof(Pass2ExecutionState.CompletedWithWarnings), status.State);
        Assert.Equal(0, status.PublishedCount);
        Assert.Equal(1, status.FailedCount);
        Assert.Equal(1, status.Warnings.Count);
        Assert.Equal("missing-business-document", status.Warnings[0].ReasonCode);
        Assert.Equal("Failed", group.State);
        Assert.Null(group.BusinessLogicDocumentVersionId);
    }

    [Fact]
    public async Task ResolutionStore_V2Authority_DoesNotPersistLegacyEvidenceRowAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        await using VulgataDbContext context = fixture.Database.CreateContext();

        _ = await SeedPass2Async(context, fixture);

        CrossRepositoryResolutionDecision[] decisions =
            await context.CrossRepositoryResolutionDecisions.ToArrayAsync();
        Assert.Single(decisions, value => value.Outcome == "ExactMatch");
        Assert.Contains(decisions, value => value.Outcome != "ExactMatch");
        Assert.NotEmpty(await context.ExactResolutionProviderEvidenceContributions.ToArrayAsync());
        Assert.Empty(await context.ExactCrossRepositoryEdgeEvidence.ToArrayAsync());
    }

    [Fact]
    public async Task V2Decisions_SharedReferencesEdge_OwnIndependentContributionsAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        await using VulgataDbContext context = fixture.Database.CreateContext();
        object seed = await SeedPass2Async(context, fixture);
        await AddExactDecisionsAsync(context, seed, 1);

        CrossRepositoryResolutionDecision[] decisions = (await context
            .CrossRepositoryResolutionDecisions.ToArrayAsync())
            .OrderBy(value => value.CreatedAt)
            .ToArray();
        CrossRepositoryResolutionDecision[] exactDecisions = decisions
            .Where(value => value.Outcome == "ExactMatch")
            .ToArray();
        Assert.Equal(2, exactDecisions.Length);
        Assert.Single(exactDecisions.Select(value => value.DocumentEdgeId).Distinct());
        Assert.Contains(decisions, value => value.Outcome != "ExactMatch");
        foreach (CrossRepositoryResolutionDecision decision in exactDecisions)
        {
            ExactResolutionProviderEvidenceContribution[] contributions = await context
                .ExactResolutionProviderEvidenceContributions
                .Where(value => value.ResolutionDecisionId == decision.Id)
                .ToArrayAsync();
            Assert.NotEmpty(contributions);
            Assert.All(contributions, value => Assert.Equal(decision.Id, value.ResolutionDecisionId));
        }
    }

    [Theory]
    [InlineData("legacy-absent")]
    [InlineData("v2-contribution-tampered")]
    public async Task CommitAsync_V2Authority_UsesContributionsAndIgnoresLegacyEvidenceAsync(string mutation)
    {
        object fixture = await CreateAcceptedCommitFixtureAsync();
        try
        {
            VulgataDbContext context = Property<VulgataDbContext>(fixture, "Context");
            IPass2BusinessLogicDocumentCommitter committer =
                Property<IPass2BusinessLogicDocumentCommitter>(fixture, "Committer");
            string[] handles = Property<string[]>(fixture, "CandidateHandleSet");
            if (mutation == "legacy-absent")
            {
                context.ExactCrossRepositoryEdgeEvidence.RemoveRange(
                    await context.ExactCrossRepositoryEdgeEvidence.ToArrayAsync());
            }
            else
            {
                ExactResolutionProviderEvidenceContribution contribution = await context
                    .ExactResolutionProviderEvidenceContributions.FirstAsync();
                SetProperty(
                    contribution,
                    nameof(ExactResolutionProviderEvidenceContribution.EvidenceFingerprint),
                    Hash('7'));
            }
            await context.SaveChangesAsync();
            MethodInfo createRequest = fixture.GetType().GetMethod(
                "CreateRequest",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            Pass2BusinessLogicCommitRequest request =
                (Pass2BusinessLogicCommitRequest)createRequest.Invoke(fixture, [handles])!;

            Pass2BusinessLogicCommitResult result = await committer.CommitAsync(request);

            Assert.Equal(
                mutation == "legacy-absent"
                    ? Pass2BusinessLogicCommitStatus.Committed
                    : Pass2BusinessLogicCommitStatus.Rejected,
                result.Status);
        }
        finally
        {
            await ((IAsyncDisposable)fixture).DisposeAsync();
        }
    }

    [Fact]
    public async Task Coordinator_Status_ReportsContentDarkWarningInventoryWithoutChangingResolutionAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        await using VulgataDbContext context = fixture.Database.CreateContext();
        object seed = await SeedPass2Async(context, fixture);
        await AddExactDecisionsAsync(context, seed, 10);
        Pass1PairExecution pair = Property<Pass1PairExecution>(seed, "Pair");
        RepositoryAccessScope scope = AuthorizedScope(seed);
        await BeginPass2Async(context);
        CrossRepositoryResolutionDecision originalDecision =
            (await context.CrossRepositoryResolutionDecisions
                .AsNoTracking()
                .ToArrayAsync())
            .OrderBy(value => value.CreatedAt)
            .First();
        var store = CreateAuthorizedStore(context);

        PreparedPass2StartResult start = await store.StartAsync(pair.Id, scope);
        Assert.Equal(PreparedPass2StartOutcome.Created, start.Outcome);
        Assert.Equal(10, start.FlowGroupCount);
        Assert.Equal(1, start.OmissionCount);

        Guid[] groupIds = await context.FlowGroups
            .OrderBy(value => value.GroupOrder)
            .Select(value => value.Id)
            .Take(2)
            .ToArrayAsync();
        Assert.Equal(2, groupIds.Length);
        await context.FlowGroups
            .Where(value => value.Id == groupIds[0])
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.State, "Incomplete")
                .SetProperty(value => value.ReasonCode, "missing-provider-implementation"));
        await context.FlowGroups
            .Where(value => value.Id == groupIds[1])
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.State, "BoundExcluded")
                .SetProperty(value => value.ReasonCode, "flow-group-bound"));
        context.ChangeTracker.Clear();

        PreparedPass2ExecutionStatus status = await CreateCoordinator(context, store)
            .GetStatusAsync(Assert.IsType<Guid>(start.ExecutionId), AdministratorUser())
            ?? throw new XunitException("The planned Pass 2 execution was not visible.");
        PreparedPass2ExecutionWarning[] warnings = status.Warnings.ToArray();
        CrossRepositoryResolutionDecision afterDecision =
            await context.CrossRepositoryResolutionDecisions
                .AsNoTracking()
                .SingleAsync(value => value.Id == originalDecision.Id);

        Assert.Equal(nameof(Pass2ExecutionState.Running), status.State);
        Assert.Equal(1, status.IncompleteCount);
        Assert.Equal(1, status.BoundExcludedCount);
        Assert.Equal(1, status.OmissionCount);
        Assert.Equal(3, warnings.Length);
        Assert.Contains(warnings, value => value.ReasonCode == "missing-provider-implementation");
        Assert.Contains(warnings, value => value.ReasonCode == "flow-group-bound");
        Assert.DoesNotContain(warnings, value => value.Fingerprint.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(originalDecision.EvidenceFingerprint, afterDecision.EvidenceFingerprint);
        Assert.Equal(originalDecision.ProviderDocumentVersionId, afterDecision.ProviderDocumentVersionId);
        Assert.Equal(originalDecision.DocumentEdgeId, afterDecision.DocumentEdgeId);
    }

    [Fact]
    public async Task Coordinator_AllPublishedGroups_CompletesWithoutWarningsAndPreservesDocumentAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        await using VulgataDbContext context = fixture.Database.CreateContext();
        object seed = await SeedPass2Async(context, fixture);
        Pass1PairExecution pair = Property<Pass1PairExecution>(seed, "Pair");
        RepositoryAccessScope scope = AuthorizedScope(seed);
        await BeginPass2Async(context);
        var store = CreateAuthorizedStore(context);

        PreparedPass2StartResult start = await store.StartAsync(pair.Id, scope);
        Guid executionId = Assert.IsType<Guid>(start.ExecutionId);
        PreparedPass2ClaimResult claim = await store.ClaimNextAsync(
            executionId,
            (await context.Pass2JobExecutions.SingleAsync()).StateChangedAt.AddMinutes(1),
            TimeSpan.FromMinutes(5));
        Guid jobId = Assert.IsType<Guid>(claim.JobId);
        Pass2JobExecution job = await context.Pass2JobExecutions.SingleAsync(value => value.Id == jobId);
        FlowGroup group = await context.FlowGroups.SingleAsync(value => value.Id == claim.FlowGroupId);
        Guid documentVersionId =
            Property<ExactPass1DocumentFixture>(seed, "Consumer").DocumentVersion.Id;
        group.Publish(documentVersionId);
        job.Complete(job.StateVersion, Assert.IsType<Guid>(claim.LeaseToken), documentVersionId, Hash('8'), job.StateChangedAt.AddSeconds(1));
        Pass2Execution execution = await context.Pass2Executions.SingleAsync(value => value.Id == executionId);
        execution.RecordPublished(execution.StateVersion, job.StateChangedAt.AddSeconds(1));
        await context.SaveChangesAsync();

        PreparedPass2RecoveryResult recovery = await store.RecoverAsync(
            executionId,
            scope,
            providerAvailable: true,
            job.StateChangedAt.AddMinutes(1));
        Assert.Equal(PreparedPass2RecoveryOutcome.Completed, recovery.Outcome);
        PreparedPass2ExecutionStatus status = await CreateCoordinator(context, store)
            .GetStatusAsync(executionId, AdministratorUser())
            ?? throw new XunitException("The completed Pass 2 execution was not visible.");

        Assert.Equal(nameof(Pass2ExecutionState.Completed), status.State);
        Assert.Equal(1, status.PublishedCount);
        Assert.Equal(0, status.FailedCount);
        Assert.Empty(status.Warnings);
        Assert.Equal("Published", group.State);
        Assert.Equal(documentVersionId, group.BusinessLogicDocumentVersionId);
    }

    [Fact]
    public async Task Coordinator_MixedPublishedAndFailedGroups_CompletesWithWarningsAndPreservesSuccessAsync()
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        await using VulgataDbContext context = fixture.Database.CreateContext();
        object seed = await SeedPass2Async(context, fixture);
        await AddExactDecisionsAsync(context, seed, 1);
        Pass1PairExecution pair = Property<Pass1PairExecution>(seed, "Pair");
        RepositoryAccessScope scope = AuthorizedScope(seed);
        await BeginPass2Async(context);
        var store = CreateAuthorizedStore(context);

        PreparedPass2StartResult start = await store.StartAsync(pair.Id, scope);
        Guid executionId = Assert.IsType<Guid>(start.ExecutionId);
        Pass2JobExecution[] jobs = await context.Pass2JobExecutions
            .OrderBy(value => value.GroupOrder)
            .ToArrayAsync();
        Assert.Equal(2, jobs.Length);
        DateTimeOffset firstNow = jobs[0].StateChangedAt.AddMinutes(1);
        PreparedPass2ClaimResult firstClaim = await store.ClaimNextAsync(
            executionId, firstNow, TimeSpan.FromMinutes(5));
        Guid firstDocumentId =
            Property<ExactPass1DocumentFixture>(seed, "Consumer").DocumentVersion.Id;
        FlowGroup firstGroup = await context.FlowGroups.SingleAsync(value => value.Id == firstClaim.FlowGroupId);
        Pass2JobExecution firstJob = await context.Pass2JobExecutions.SingleAsync(value => value.Id == firstClaim.JobId);
        firstGroup.Publish(firstDocumentId);
        DateTimeOffset firstCompletedAt = firstJob.StateChangedAt.AddSeconds(1);
        firstJob.Complete(firstJob.StateVersion, Assert.IsType<Guid>(firstClaim.LeaseToken), firstDocumentId, Hash('9'), firstCompletedAt);
        Pass2Execution execution = await context.Pass2Executions.SingleAsync(value => value.Id == executionId);
        execution.RecordPublished(execution.StateVersion, firstCompletedAt);
        await context.SaveChangesAsync();

        PreparedPass2ClaimResult secondClaim = await store.ClaimNextAsync(
            executionId, firstCompletedAt.AddSeconds(1), TimeSpan.FromMinutes(5));
        Assert.Equal(PreparedPass2ClaimOutcome.Claimed, secondClaim.Outcome);
        Guid secondJobId = Assert.IsType<Guid>(secondClaim.JobId);
        Guid firstLease = Assert.IsType<Guid>(secondClaim.LeaseToken);
        DateTimeOffset secondFailureAt = firstCompletedAt.AddSeconds(2);
        Assert.Equal(
            PreparedPass2DispatchFailureOutcome.RetryScheduled,
            await store.RecordDispatchFailureAsync(
                executionId,
                secondJobId,
                secondClaim.JobStateVersion,
                firstLease,
                ScanJobFailureCategory.JobPermanent,
                "provider-output-invalid",
                secondFailureAt));
        PreparedPass2ClaimResult retryClaim = await store.ClaimNextAsync(
            executionId, secondFailureAt.AddSeconds(2), TimeSpan.FromMinutes(5));
        Assert.Equal(
            PreparedPass2DispatchFailureOutcome.Failed,
            await store.RecordDispatchFailureAsync(
                executionId,
                secondJobId,
                retryClaim.JobStateVersion,
                Assert.IsType<Guid>(retryClaim.LeaseToken),
                ScanJobFailureCategory.JobPermanent,
                "provider-output-invalid",
                secondFailureAt.AddSeconds(3)));

        PreparedPass2RecoveryResult recovery = await store.RecoverAsync(
            executionId,
            scope,
            providerAvailable: true,
            secondFailureAt.AddSeconds(4));
        Assert.Equal(PreparedPass2RecoveryOutcome.Completed, recovery.Outcome);
        PreparedPass2ExecutionStatus status = await CreateCoordinator(context, store)
            .GetStatusAsync(executionId, AdministratorUser())
            ?? throw new XunitException("The mixed Pass 2 execution was not visible.");
        FlowGroup secondGroup = await context.FlowGroups.SingleAsync(value => value.Id == retryClaim.FlowGroupId);

        Assert.Equal(nameof(Pass2ExecutionState.CompletedWithWarnings), status.State);
        Assert.Equal(1, status.PublishedCount);
        Assert.Equal(1, status.FailedCount);
        Assert.Contains(status.Warnings, value => value.ReasonCode == "provider-output-invalid");
        Assert.Equal("Published", firstGroup.State);
        Assert.Equal(firstDocumentId, firstGroup.BusinessLogicDocumentVersionId);
        Assert.Equal("Failed", secondGroup.State);
        Assert.Null(secondGroup.BusinessLogicDocumentVersionId);

        string[] terminalWarnings = status.Warnings
            .Select(value => $"{value.GroupIdentity:D}|{value.Fingerprint}|{value.ReasonCode}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        await context.Pass2JobExecutions
            .Where(value => value.Id == secondJobId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.FailureCode, "drifted-warning"));
        await context.FlowGroups
            .Where(value => value.Id == secondGroup.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.MembershipFingerprint, Hash('8'))
                .SetProperty(value => value.ReasonCode, "drifted-warning"));
        context.ChangeTracker.Clear();

        await using VulgataDbContext replayContext = fixture.Database.CreateContext();
        var replayStore = CreateAuthorizedStore(replayContext);
        PreparedPass2RecoveryResult replay = await replayStore.RecoverAsync(
            executionId,
            scope,
            providerAvailable: true,
            secondFailureAt.AddMinutes(1));
        Assert.Equal(PreparedPass2RecoveryOutcome.Completed, replay.Outcome);
        PreparedPass2ExecutionStatus replayedStatus = await CreateCoordinator(
                replayContext,
                replayStore)
            .GetStatusAsync(executionId, AdministratorUser()) ??
            throw new XunitException("The immutable terminal summary was not readable after replay.");
        Assert.Equal(status.PublishedCount, replayedStatus.PublishedCount);
        Assert.Equal(status.FailedCount, replayedStatus.FailedCount);
        Assert.Equal(status.IncompleteCount, replayedStatus.IncompleteCount);
        Assert.Equal(status.BoundExcludedCount, replayedStatus.BoundExcludedCount);
        Assert.Equal(status.OmissionCount, replayedStatus.OmissionCount);
        Assert.Equal(
            terminalWarnings,
            replayedStatus.Warnings
                .Select(value => $"{value.GroupIdentity:D}|{value.Fingerprint}|{value.ReasonCode}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task CommitAsync_FinalSuccessfulPublication_TerminalizesExecutionAndCoordinationImmediatelyAsync()
    {
        object fixture = await CreateAcceptedCommitFixtureAsync();
        try
        {
            VulgataDbContext context = Property<VulgataDbContext>(fixture, "Context");
            IPass2BusinessLogicDocumentCommitter committer =
                Property<IPass2BusinessLogicDocumentCommitter>(fixture, "Committer");
            Pass2Execution expectedExecution = Property<Pass2Execution>(fixture, "Execution");
            string[] handles = Property<string[]>(fixture, "CandidateHandleSet");
            MethodInfo createRequest = fixture.GetType().GetMethod(
                "CreateRequest",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The accepted commit request fixture is unavailable.");
            Pass2BusinessLogicCommitRequest request =
                (Pass2BusinessLogicCommitRequest)(createRequest.Invoke(fixture, [handles])
                    ?? throw new InvalidOperationException("The accepted commit request fixture returned null."));

            Pass2BusinessLogicCommitResult result = await committer.CommitAsync(request);
            Assert.Equal(Pass2BusinessLogicCommitStatus.Committed, result.Status);

            Pass2Execution execution = await context.Pass2Executions.AsNoTracking()
                .SingleAsync(value => value.Id == expectedExecution.Id);
            TwoRepositoryScanCoordination coordination =
                await context.TwoRepositoryScanCoordinations.AsNoTracking().SingleAsync(
                    value => value.Id == execution.CoordinationId);
            PreparedPass2ExecutionStatus status = await CreateCoordinator(
                    context,
                    CreateAuthorizedStore(context))
                .GetStatusAsync(execution.Id, AdministratorUser())
                ?? throw new XunitException("The committed Pass 2 execution was not visible.");

            Assert.Equal(nameof(Pass2ExecutionState.Completed), execution.State.ToString());
            Assert.Equal(1, execution.PublishedCount);
            Assert.Equal(0, execution.FailedCount);
            Assert.Equal(
                TwoRepositoryScanCoordinationPhase.Completed,
                coordination.Phase);
            Assert.Equal(nameof(Pass2ExecutionState.Completed), status.State);
            Assert.Equal(1, status.PublishedCount);
            Assert.Equal(0, status.FailedCount);
            Assert.Empty(status.Warnings);
        }
        finally
        {
            await ((IAsyncDisposable)fixture).DisposeAsync();
        }
    }

    private static FlowGroup CreateCompleteGroup() => FlowGroup.CreatePlanned(
        Id(90_071),
        Id(90_072),
        Id(90_073),
        ExactResolutionProtocol.CustomRpc,
        "rpc://bank/customer",
        "CustomerClient.Load",
        new string('c', 64),
        "business-flow/v1/complete",
        Id(90_080),
        Id(90_081),
        Id(90_082),
        Id(90_083),
        Id(90_084),
        Id(90_085),
        Id(90_086),
        Id(90_087),
        1,
        Hash('6'),
        Hash('7'),
        CreatedAt);

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-4000-8000-{value:000000000000}");

    private static string Hash(char value) => new(value, 64);

    // Pass 2 start now requires the production worker-contract authority. Keep
    // this test-only adapter bound to the real embedded artifacts so these
    // fixtures exercise the same contract as composition, without restoring a
    // compatibility fallback in production.
    internal static PreparedPass2WorkerContractAuthority CreateWorkerContractAuthority() =>
        new PreparedPass2WorkerContractAuthoritySource(
            new PreparedRetrieveCodeLogicFunction(new EmptyRetrievalService()),
            new PreparedSubmitBusinessLogicFunction()).GetCurrent();

    internal static PreparedPass2Store CreateAuthorizedStore(VulgataDbContext context) =>
        new(context, new TestWorkerContractAuthority(CreateWorkerContractAuthority()));

    private sealed class TestWorkerContractAuthority(
        PreparedPass2WorkerContractAuthority contract) :
        IPreparedPass2WorkerContractAuthority
    {
        public PreparedPass2WorkerContractAuthority GetCurrent() => contract;
    }

    private sealed class EmptyRetrievalService : IPass2CodeLogicRetrievalService
    {
        public Task<Pass2CodeLogicRetrievalResult> RetrieveAsync(
            Guid pass2JobExecutionId,
            Pass2CodeLogicRetrievalRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Pass2CodeLogicRetrievalResult.NoResults());
    }

    private static async Task<object> SeedPass2Async(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture)
    {
        MethodInfo method = typeof(Pass2ProductionCompositionTests).GetMethod(
            "SeedCanonicalPass2Async",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The canonical Pass 2 fixture seeder is unavailable.");
        object invocation = method.Invoke(null, [context, fixture])
            ?? throw new InvalidOperationException("The accepted Pass 2 fixture seeder returned null.");
        Task task = Assert.IsAssignableFrom<Task>(invocation);
        await task;
        return task.GetType().GetProperty("Result")?.GetValue(task)
            ?? throw new InvalidOperationException("The accepted Pass 2 fixture result is unavailable.");
    }

    private static async Task<object> CreateAcceptedCommitFixtureAsync()
    {
        Type fixtureType = typeof(Pass2DocumentCommitTests).GetNestedType(
            "CommitFixture",
            BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The accepted commit fixture is unavailable.");
        MethodInfo create = fixtureType.GetMethod(
            "CreateAsync",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The accepted commit fixture factory is unavailable.");
        object invocation = create.Invoke(null, null)
            ?? throw new InvalidOperationException("The accepted commit fixture factory returned null.");
        Task task = Assert.IsAssignableFrom<Task>(invocation);
        await task;
        return task.GetType().GetProperty("Result")?.GetValue(task)
            ?? throw new InvalidOperationException("The accepted commit fixture result is unavailable.");
    }

    private static async Task AddExactDecisionsAsync(
        VulgataDbContext context,
        object seed,
        int additionalCount)
    {
        if (seed.GetType().Name == "CanonicalPass2Seed")
        {
            MethodInfo addShared = typeof(Pass2ProductionCompositionTests).GetMethod(
                "AddSharedV2DecisionAsync",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The canonical shared-edge fixture is unavailable.");
            for (int ordinal = 0; ordinal < additionalCount; ordinal++)
            {
                await Assert.IsAssignableFrom<Task>(addShared.Invoke(null, [context]));
            }
            return;
        }
        MethodInfo method = typeof(Pass2FlowPlanningPostgreSqlTests).GetMethod(
            "AddExactDecisionsAsync",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The accepted Pass 2 decision fixture is unavailable.");
        object invocation = method.Invoke(null, [context, seed, additionalCount])
            ?? throw new InvalidOperationException("The accepted Pass 2 decision fixture returned null.");
        await Assert.IsAssignableFrom<Task>(invocation);
    }

    private static RepositoryAccessScope AuthorizedScope(object seed)
    {
        ExactPass1DocumentFixture consumer = Property<ExactPass1DocumentFixture>(seed, "Consumer");
        ExactPass1DocumentFixture providerXml = Property<ExactPass1DocumentFixture>(seed, "ProviderXml");
        return new RepositoryAccessScope(
            [consumer.DocumentVersion.RepositoryId, providerXml.DocumentVersion.RepositoryId],
            new AllowAccessVerifier());
    }

    private static async Task BeginPass2Async(VulgataDbContext context)
    {
        TwoRepositoryScanCoordination coordination =
            await context.TwoRepositoryScanCoordinations.SingleAsync();
        coordination.BeginPass2(coordination.StateVersion, coordination.UpdatedAt.AddSeconds(1));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static T Property<T>(object source, string name) =>
        (T)(source.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(source)
            ?? throw new InvalidOperationException($"Seed property '{name}' is unavailable."));

    private static void SetProperty<T>(object target, string name, T value) =>
        target.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

    private static PreparedPass2Coordinator CreateCoordinator(
        VulgataDbContext context,
        PreparedPass2Store store) => new(
        store,
        context,
        new SystemRepository(context),
        new AllowAccessVerifier(),
        new AlwaysAvailableProviderProbe(),
        new PreparedPass2ExecutionActivator(),
        TimeProvider.System);

    private static IUserContext AdministratorUser() => new TestUserContext();

    private sealed class TestUserContext : IUserContext
    {
        public string UserId => "t040-admin";

        public bool IsAdministrator => true;

        public SystemOwnerFilter ToSystemOwnerFilter() => SystemOwnerFilter.AllSystems();
    }

    private sealed class AllowAccessVerifier : IRepositoryAuthorizationVerifier
    {
        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }

    private sealed class AlwaysAvailableProviderProbe : IPreparedPass1ProviderAvailabilityProbe
    {
        public Task<bool> IsAvailableAsync(
            Guid pairExecutionId,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }
}

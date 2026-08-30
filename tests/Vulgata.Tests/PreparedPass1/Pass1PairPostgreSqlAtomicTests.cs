using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedScanning;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T015 RED slice.  PostgreSQL is the authority for the pair-wide
/// start transaction: an injected failure at every durable write boundary
/// must expose no partial queue, and competing claimers must observe only a
/// committed complete queue under the shared pair limit.
///
/// The store is intentionally discovered through reflection.  T016 owns the
/// production PostgreSQL start/claim seam; this test must fail honestly while
/// that seam is absent instead of introducing a test-only implementation.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1PairPostgreSqlAtomicTests
{
    public static TheoryData<string, string, string, string> StartWriteBoundaries => new()
    {
        { "before-pair", "vulgata.\"Pass1PairExecutions\"", "INSERT", "BEFORE" },
        { "after-pair", "vulgata.\"Pass1PairExecutions\"", "INSERT", "AFTER" },
        { "before-job", "vulgata.\"ScanJobExecutions\"", "INSERT", "BEFORE" },
        { "after-job", "vulgata.\"ScanJobExecutions\"", "INSERT", "AFTER" },
        { "before-budget", "vulgata.\"Pass1NavigationBudgets\"", "INSERT", "BEFORE" },
        { "after-budget", "vulgata.\"Pass1NavigationBudgets\"", "INSERT", "AFTER" },
        { "before-child-scan", "vulgata.\"ScanExecutions\"", "UPDATE", "BEFORE" },
        { "after-child-scan", "vulgata.\"ScanExecutions\"", "UPDATE", "AFTER" },
        { "before-coordination", "vulgata.\"TwoRepositoryScanCoordinations\"", "UPDATE", "BEFORE" },
        { "after-coordination", "vulgata.\"TwoRepositoryScanCoordinations\"", "UPDATE", "AFTER" },
    };

    [Theory]
    [MemberData(nameof(StartWriteBoundaries))]
    public async Task PostgreSql17_StartFailureAtEveryWriteBoundary_RollsBackPairQueueAndStateAsync(
        string boundary,
        string table,
        string operation,
        string timing)
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        await InstallFailingTriggerAsync(fixture.ConnectionString, boundary, table, operation, timing);
        StartVisibility before = await ReadVisibilityAsync(fixture.ConnectionString);

        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult? result = null;
        Exception? failure = null;
        try
        {
            result = await store.StartAsync(
                fixture.Authorization,
                CreateHandoff(fixture.Package),
                2,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is not null)
        {
            Assert.True(
                IsInjectedTriggerException(failure),
                $"T015 injected {timing} failure at {boundary} produced an unexpected exception: {failure.GetType().Name}: {failure.Message}");
        }
        else
        {
            Assert.NotNull(result);
            Assert.Equal(PreparedPass1StartOutcome.Failed, result!.Outcome);
        }
        Assert.Equal(before, await ReadVisibilityAsync(fixture.ConnectionString));
    }

    [Fact]
    public async Task PostgreSql17_RetryingExecutionStrategy_StartsPreparedPairWithoutTransactionRejectionAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        await using VulgataDbContext retryingContext = CreateRetryingContext(
            fixture.ConnectionString);

        PreparedPass1StartResult started = await new PreparedPass1Store(retryingContext)
            .StartAsync(
                fixture.Authorization,
                CreateHandoff(fixture.Package),
                2,
                CancellationToken.None);

        Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);
        Assert.NotNull(started.PairExecutionId);
    }

    [Fact]
    public async Task PostgreSql17_RetryingExecutionStrategy_ClaimsPreparedJobWithoutTransactionRejectionAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        await using VulgataDbContext retryingContext = CreateRetryingContext(
            fixture.ConnectionString);
        PreparedPass1Store store = new(retryingContext);
        PreparedPass1StartResult started = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            2,
            CancellationToken.None);
        Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);

        PreparedPass1ClaimResult claimed = await store.ClaimNextAsync(
            started.PairExecutionId!.Value,
            PreparedScanTestHost.FixedUtcNow,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Equal(PreparedPass1ClaimOutcome.Claimed, claimed.Outcome);
        Assert.NotNull(claimed.JobId);
    }

    [Fact]
    public async Task PostgreSql17_CompetingClaimers_SeeOnlyCommittedCompleteQueueAndRespectPairLimitAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);

        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult started = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            2,
            CancellationToken.None);
        Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);

        Guid pairExecutionId = await ScalarAsync<Guid>(
            fixture.ConnectionString,
            "SELECT \"Id\" FROM vulgata.\"Pass1PairExecutions\" LIMIT 1;");
        StartVisibility committed = await ReadVisibilityAsync(fixture.ConnectionString);
        Assert.True(committed.PairExecutions == 1 && committed.Jobs > 0 && committed.Budgets == committed.Jobs);

        await using VulgataDbContext secondContext =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        IPreparedPass1Store firstStore = CreatePass1Store(fixture.Context);
        IPreparedPass1Store secondStore = CreatePass1Store(secondContext);
        Task<PreparedPass1ClaimResult> firstClaim = firstStore.ClaimNextAsync(
            pairExecutionId,
            PreparedScanTestHost.FixedUtcNow,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        Task<PreparedPass1ClaimResult> secondClaim = secondStore.ClaimNextAsync(
            pairExecutionId,
            PreparedScanTestHost.FixedUtcNow,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        PreparedPass1ClaimResult[] claims = await Task.WhenAll(firstClaim, secondClaim);

        Assert.All(claims, claimed => Assert.Equal(PreparedPass1ClaimOutcome.Claimed, claimed.Outcome));
        Guid[] claimedJobIds = claims.Select(claimed => claimed.JobId).ToArray();
        Assert.Equal(2, claimedJobIds.Distinct().Count());
        Assert.Equal(
            2,
            await ScalarAsync<int>(
                fixture.ConnectionString,
                "SELECT count(*)::integer FROM vulgata.\"ScanJobExecutions\" WHERE \"State\" = 'Running';"));
        Assert.Equal(committed, await ReadVisibilityAsync(fixture.ConnectionString));
    }

    [Fact]
    public async Task PostgreSql17_Claim_ReturnsExactDurableLeaseTimestampAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult started = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            2,
            CancellationToken.None);
        Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);

        DateTimeOffset observedAt = PreparedScanTestHost.FixedUtcNow.AddTicks(7);
        PreparedPass1ClaimResult claim = await store.ClaimNextAsync(
            started.PairExecutionId!.Value,
            observedAt,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        Assert.Equal(PreparedPass1ClaimOutcome.Claimed, claim.Outcome);

        await using VulgataDbContext verification =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        DateTimeOffset durableLease = (await verification.ScanJobExecutions
            .AsNoTracking()
            .SingleAsync(value => value.Id == claim.JobId)).LeaseExpiresAt!.Value;
        Assert.Equal(durableLease, claim.LeaseExpiresAt);
    }

    [Fact]
    public async Task PostgreSql17_RecoveryWithAllJobsCompleted_DoesNotRequireProviderAvailabilityAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult started = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            2,
            CancellationToken.None);
        Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);
        Guid pairId = Assert.IsType<Guid>(started.PairExecutionId);
        await MaterializeCompletedDocumentsAsync(fixture, pairId);

        PreparedPass1RecoveryResult recovered = await store.RecoverAsync(
            pairId,
            fixture.Authorization,
            providerAvailable: false,
            PreparedScanTestHost.FixedUtcNow.AddMinutes(2),
            CancellationToken.None);

        Assert.Equal(PreparedPass1RecoveryOutcome.NoOpReplay, recovered.Outcome);
        Assert.Equal(DurableScanReason.None, recovered.Reason);
        Assert.Equal(fixture.Package.SelectedCount, recovered.SkippedCompletedJobCount);
        Assert.Equal(0, recovered.PendingJobCount);
        Assert.True(recovered.IsReplay);
        fixture.Context.ChangeTracker.Clear();
        Pass1PairExecution pair = await fixture.Context.Pass1PairExecutions.AsNoTracking()
            .SingleAsync(value => value.Id == pairId);
        Assert.Equal(Pass1PairExecutionPhase.Running, pair.Phase);
        Assert.Equal(Pass1CircuitState.Closed, pair.CircuitState);
        Assert.Null(pair.ReasonCategory);
        Assert.Null(pair.ReasonCode);
    }

    [Fact]
    public async Task PostgreSql17_PolicyDrain_RestartBeforeAndAfterLeaseExpiry_ClosesAndAccountsTerminalStateAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult started = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            2,
            CancellationToken.None);
        Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);
        Guid pairExecutionId = Assert.IsType<Guid>(started.PairExecutionId);

        PreparedPass1ClaimResult held = await store.ClaimNextAsync(
            pairExecutionId,
            PreparedScanTestHost.FixedUtcNow.AddSeconds(30),
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        Assert.Equal(PreparedPass1ClaimOutcome.Claimed, held.Outcome);

        int failureBudget = Math.Min(5, fixture.Package.SelectedCount / 10);
        PreparedPass1FailureResult? failure = null;
        for (int index = 0; index <= failureBudget; index++)
        {
            DateTimeOffset observedAt = PreparedScanTestHost.FixedUtcNow
                .AddMinutes(1)
                .AddSeconds(index * 2);
            PreparedPass1ClaimResult claim = await store.ClaimNextAsync(
                pairExecutionId,
                observedAt,
                TimeSpan.FromMinutes(5),
                CancellationToken.None);
            Assert.Equal(PreparedPass1ClaimOutcome.Claimed, claim.Outcome);

            failure = await store.RecordFailureAsync(
                pairExecutionId,
                claim.JobId,
                claim.JobStateVersion,
                claim.LeaseToken,
                ScanJobFailureCategory.JobPermanent,
                observedAt.AddSeconds(1),
                CancellationToken.None);
        }

        Assert.NotNull(failure);
        Assert.Equal(PreparedPass1FailureOutcome.Paused, failure!.Outcome);
        Assert.Equal(Pass1PairExecutionPhase.Running, failure.PairState);
        PreparedPass1ClaimResult next = await store.ClaimNextAsync(
            pairExecutionId,
            PreparedScanTestHost.FixedUtcNow.AddMinutes(2),
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        Assert.Equal(PreparedPass1ClaimOutcome.Ineligible, next.Outcome);

        await using VulgataDbContext beforeExpiryContext =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        IPreparedPass1Store beforeExpiryStore = CreatePass1Store(beforeExpiryContext);
        Pass1PairExecution persistedDrain = await beforeExpiryContext.Pass1PairExecutions.AsNoTracking()
            .SingleAsync(value => value.Id == pairExecutionId);
        PreparedScanManifest[] persistedManifests = await beforeExpiryContext.PreparedScanManifests
            .AsNoTracking()
            .Where(value => value.Id == persistedDrain.ManifestOneId || value.Id == persistedDrain.ManifestTwoId)
            .OrderBy(value => value.RepositoryId)
            .ToArrayAsync();
        Assert.Equal(persistedDrain.ManifestOneId, persistedManifests[0].Id);
        Assert.Equal(persistedDrain.ManifestTwoId, persistedManifests[1].Id);
        TwoRepositoryScanCoordination persistedCoordination = await beforeExpiryContext
            .TwoRepositoryScanCoordinations.AsNoTracking().SingleAsync();
        ScanExecution[] persistedExecutions = await beforeExpiryContext.ScanExecutions.AsNoTracking()
            .Where(value => value.Id == persistedCoordination.ScanExecutionOneId ||
                value.Id == persistedCoordination.ScanExecutionTwoId)
            .OrderBy(value => value.RepositoryId)
            .ToArrayAsync();
        Assert.Equal(persistedCoordination.RepositoryOneId, persistedManifests[0].RepositoryId);
        Assert.Equal(persistedCoordination.RepositoryTwoId, persistedManifests[1].RepositoryId);
        Assert.Equal(persistedCoordination.ScanExecutionOneId, persistedExecutions[0].Id);
        Assert.Equal(persistedCoordination.ScanExecutionTwoId, persistedExecutions[1].Id);
        Assert.All(persistedExecutions, value => Assert.Equal(ScanExecutionPhase.Running, value.Phase));
        PreparedPass1RecoveryResult beforeExpiry = await beforeExpiryStore.RecoverAsync(
            pairExecutionId,
            fixture.Authorization,
            providerAvailable: true,
            held.LeaseExpiresAt,
            CancellationToken.None);
        Assert.Equal(PreparedPass1RecoveryOutcome.Continued, beforeExpiry.Outcome);
        Assert.Equal(0, beforeExpiry.RecoveredExpiredLeaseCount);
        Assert.True(beforeExpiry.IsReplay);
        PreparedPass1ClaimResult beforeExpiryClaim = await beforeExpiryStore.ClaimNextAsync(
            pairExecutionId,
            held.LeaseExpiresAt,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        Assert.Equal(PreparedPass1ClaimOutcome.Ineligible, beforeExpiryClaim.Outcome);
        Assert.Equal(held.LeaseExpiresAt, beforeExpiryClaim.NextEligibleAt);

        await using VulgataDbContext afterExpiryContext =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        IPreparedPass1Store afterExpiryStore = CreatePass1Store(afterExpiryContext);
        DateTimeOffset afterExpiryAt = held.LeaseExpiresAt.AddTicks(1);
        PreparedPass1RecoveryResult afterExpiry = await afterExpiryStore.RecoverAsync(
            pairExecutionId,
            fixture.Authorization,
            providerAvailable: true,
            afterExpiryAt,
            CancellationToken.None);
        Assert.Equal(PreparedPass1RecoveryOutcome.Continued, afterExpiry.Outcome);
        Assert.Equal(1, afterExpiry.RecoveredExpiredLeaseCount);
        Assert.False(afterExpiry.IsReplay);
        PreparedPass1ClaimResult afterExpiryClaim = await afterExpiryStore.ClaimNextAsync(
            pairExecutionId,
            afterExpiryAt,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        Assert.Equal(PreparedPass1ClaimOutcome.Ineligible, afterExpiryClaim.Outcome);
        Assert.Null(afterExpiryClaim.NextEligibleAt);

        Pass1PairExecution draining = await afterExpiryContext.Pass1PairExecutions.AsNoTracking()
            .SingleAsync(value => value.Id == pairExecutionId);
        PreparedPass1ReconciliationResult reconciled = await afterExpiryStore.ReconcileAsync(
            pairExecutionId,
            new(
                fixture.Authorization,
                draining.StateVersion,
                ExpectedReconciliationFingerprint: null,
                afterExpiryAt.AddTicks(1)),
            CancellationToken.None);
        Assert.Equal(PreparedPass1ReconciliationOutcome.Reconciled, reconciled.Outcome);
        Assert.Equal(Pass1PairExecutionPhase.FailedPass1, reconciled.PairState);

        Pass1Outcome outcome = await afterExpiryContext.Pass1Outcomes.AsNoTracking()
            .SingleAsync(value => value.PairExecutionId == pairExecutionId);
        Assert.Equal(Pass1OutcomeStatus.FailedPass1, outcome.Status);
        Assert.Equal(failureBudget + 1, outcome.OrdinaryFailureCount);
        Assert.Equal(failureBudget, outcome.OrdinaryFailureBudget);
        Assert.Equal(failureBudget + 1, await afterExpiryContext.Pass1FailureInventoryEntries
            .AsNoTracking()
            .CountAsync(value => value.PairExecutionId == pairExecutionId));
        Assert.Equal(failureBudget + 1, await afterExpiryContext.ScanJobExecutions.AsNoTracking()
            .CountAsync(value => value.State == ScanJobExecutionState.Failed));
        Assert.Equal(
            fixture.Package.SelectedCount - failureBudget - 1,
            await afterExpiryContext.ScanJobExecutions.AsNoTracking()
                .CountAsync(value => value.State == ScanJobExecutionState.Cancelled));
        Assert.False(await afterExpiryContext.ScanJobExecutions.AsNoTracking().AnyAsync(value =>
            value.State == ScanJobExecutionState.Pending ||
            value.State == ScanJobExecutionState.Running ||
            value.State == ScanJobExecutionState.RetryScheduled));
        Assert.All(
            await afterExpiryContext.ScanExecutions.AsNoTracking().ToArrayAsync(),
            value => Assert.Equal(ScanExecutionPhase.FailedPass1, value.Phase));
        Assert.Equal(
            TwoRepositoryScanCoordinationPhase.FailedPass1,
            (await afterExpiryContext.TwoRepositoryScanCoordinations.AsNoTracking().SingleAsync()).Phase);

        long terminalPairStateVersion = await afterExpiryContext.Pass1PairExecutions.AsNoTracking()
            .Where(value => value.Id == pairExecutionId)
            .Select(value => value.StateVersion)
            .SingleAsync();
        PreparedPass1ReconciliationResult replay = await afterExpiryStore.ReconcileAsync(
            pairExecutionId,
            new(
                fixture.Authorization,
                terminalPairStateVersion,
                reconciled.ReconciliationFingerprint,
                afterExpiryAt.AddTicks(1)),
            CancellationToken.None);
        Assert.Equal(PreparedPass1ReconciliationOutcome.Replay, replay.Outcome);

        PreparedPass1FailureResult postTerminal = await afterExpiryStore.RecordFailureAsync(
            pairExecutionId,
            held.JobId,
            held.JobStateVersion + 1,
            Guid.NewGuid(),
            ScanJobFailureCategory.JobPermanent,
            PreparedScanTestHost.FixedUtcNow.AddMinutes(4),
            CancellationToken.None);
        Assert.Equal(PreparedPass1FailureOutcome.Conflict, postTerminal.Outcome);
        Assert.Equal(failureBudget + 1, await afterExpiryContext.Pass1FailureInventoryEntries
            .AsNoTracking()
            .CountAsync(value => value.PairExecutionId == pairExecutionId));
    }

    [Fact]
    public async Task PostgreSql17_OrdinaryFailureBeyondBudgetDuringProviderProbe_EntersPolicyDrainImmediatelyAsync()
    {
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult started = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            2,
            CancellationToken.None);
        Guid pairExecutionId = Assert.IsType<Guid>(started.PairExecutionId);
        DateTimeOffset observedAt = PreparedScanTestHost.FixedUtcNow.AddMinutes(1);

        PreparedPass1ClaimResult held = await store.ClaimNextAsync(
            pairExecutionId,
            observedAt,
            TimeSpan.FromMinutes(5));
        Assert.Equal(PreparedPass1ClaimOutcome.Claimed, held.Outcome);
        int failureBudget = Math.Min(5, fixture.Package.SelectedCount / 10);
        for (int index = 0; index < failureBudget; index++)
        {
            PreparedPass1ClaimResult ordinary = await store.ClaimNextAsync(
                pairExecutionId,
                observedAt.AddSeconds(index + 1),
                TimeSpan.FromMinutes(5));
            PreparedPass1FailureResult recorded = await store.RecordFailureAsync(
                pairExecutionId,
                ordinary.JobId,
                ordinary.JobStateVersion,
                ordinary.LeaseToken,
                ScanJobFailureCategory.JobPermanent,
                observedAt.AddSeconds(index + 1));
            Assert.Equal(PreparedPass1FailureOutcome.Recorded, recorded.Outcome);
        }

        PreparedPass1ClaimResult provider = await store.ClaimNextAsync(
            pairExecutionId,
            observedAt.AddSeconds(10),
            TimeSpan.FromMinutes(5));
        PreparedPass1FailureResult probing = await store.RecordFailureAsync(
            pairExecutionId,
            provider.JobId,
            provider.JobStateVersion,
            provider.LeaseToken,
            ScanJobFailureCategory.ProviderTransient,
            observedAt.AddSeconds(10));
        Assert.Equal(PreparedPass1FailureOutcome.Paused, probing.Outcome);
        Assert.Equal(Pass1PairExecutionPhase.ProbingProvider, probing.PairState);

        PreparedPass1FailureResult threshold = await store.RecordFailureAsync(
            pairExecutionId,
            held.JobId,
            held.JobStateVersion,
            held.LeaseToken,
            ScanJobFailureCategory.JobPermanent,
            observedAt.AddSeconds(11));
        Assert.Equal(PreparedPass1FailureOutcome.Paused, threshold.Outcome);
        Assert.Equal(Pass1PairExecutionPhase.Running, threshold.PairState);
        Assert.Equal(failureBudget + 1, threshold.OrdinaryFailureCount);

        PreparedPass1ClaimResult blocked = await store.ClaimNextAsync(
            pairExecutionId,
            observedAt.AddSeconds(12),
            TimeSpan.FromMinutes(5));
        Assert.Equal(PreparedPass1ClaimOutcome.Ineligible, blocked.Outcome);
        Assert.Null(blocked.NextEligibleAt);

        fixture.Context.ChangeTracker.Clear();
        Pass1PairExecution drain = await fixture.Context.Pass1PairExecutions.AsNoTracking()
            .SingleAsync(value => value.Id == pairExecutionId);
        Assert.Equal(Pass1CircuitState.Paused, drain.CircuitState);
        Assert.Equal(Pass1PairReasonCategory.Pass1Policy, drain.ReasonCategory);
        Assert.Equal("pass1-policy-failed", drain.ReasonCode);
        Assert.Equal(0, drain.ProbeOrdinal);
        Assert.Null(drain.NextProbeAt);
        Assert.Null(drain.RetryAfterAt);
    }

    [Fact]
    public async Task PostgreSql17_RejectedExactPairBinding_FailsBothScansAndNeverDispatchesOrCommitsAsync()
    {
        MethodInfo failureMethod = RequireRevisionBindingFailureMethod();
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult started = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            2,
            CancellationToken.None);
        Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);
        Guid pairId = started.PairExecutionId!.Value;
        long stateVersion = await ReadPairStateVersionAsync(fixture.ConnectionString, pairId);
        int documentsBefore = await ScalarAsync<int>(
            fixture.ConnectionString,
            "SELECT count(*)::integer FROM vulgata.\"DocumentVersions\";");

        object result = await InvokeRevisionBindingFailureAsync(
            store,
            failureMethod,
            pairId,
            fixture.Authorization,
            stateVersion);
        Assert.Equal("FailedIntegrity", ReadProperty(result, "PairState")?.ToString());
        Assert.Equal("revision-binding-failure", ReadProperty(result, "FailureCode"));
        Assert.Equal(
            documentsBefore,
            await ScalarAsync<int>(
                fixture.ConnectionString,
                "SELECT count(*)::integer FROM vulgata.\"DocumentVersions\";"));
        Assert.Equal(
            "FailedIntegrity",
            await ScalarAsync<string>(
                fixture.ConnectionString,
                $"SELECT \"Phase\" FROM vulgata.\"Pass1PairExecutions\" WHERE \"Id\" = '{pairId:D}';"));
        Assert.Equal(
            "FailedIntegrity,FailedIntegrity",
            await ScalarAsync<string>(
                fixture.ConnectionString,
                "SELECT string_agg(\"Phase\", ',' ORDER BY \"RepositoryId\") FROM vulgata.\"ScanExecutions\";"));
        Assert.Equal(
            "FailedIntegrity",
            await ScalarAsync<string>(
                fixture.ConnectionString,
                "SELECT \"Phase\" FROM vulgata.\"TwoRepositoryScanCoordinations\" LIMIT 1;"));
        Assert.Equal(
            0,
            await ScalarAsync<int>(
                fixture.ConnectionString,
                "SELECT count(*)::integer FROM vulgata.\"ScanJobExecutions\" WHERE \"State\" = 'Running';"));

        long failedVersion = await ReadPairStateVersionAsync(fixture.ConnectionString, pairId);
        object replay = await InvokeRevisionBindingFailureAsync(
            store,
            failureMethod,
            pairId,
            fixture.Authorization,
            failedVersion);
        Assert.Contains("Replay", ReadProperty(replay, "Outcome")?.ToString() ?? string.Empty);
        Assert.Equal(failedVersion, await ReadPairStateVersionAsync(fixture.ConnectionString, pairId));
        Assert.Equal(
            documentsBefore,
            await ScalarAsync<int>(
                fixture.ConnectionString,
                "SELECT count(*)::integer FROM vulgata.\"DocumentVersions\";"));
    }

    [Fact]
    public async Task PostgreSql17_RevisionBindingFailure_RejectsStaleAndUnauthorizedAuthorityWithoutMutationAsync()
    {
        MethodInfo failureMethod = RequireRevisionBindingFailureMethod();
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult started = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            2,
            CancellationToken.None);
        Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);
        Guid pairId = started.PairExecutionId!.Value;
        long currentVersion = await ReadPairStateVersionAsync(fixture.ConnectionString, pairId);
        string beforePhase = await ScalarAsync<string>(
            fixture.ConnectionString,
            $"SELECT \"Phase\" FROM vulgata.\"Pass1PairExecutions\" WHERE \"Id\" = '{pairId:D}';");

        PreparedScanAuthorizationScope unauthorized = fixture.Authorization with
        {
            SystemId = Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff"),
        };
        object unauthorizedResult = await InvokeRevisionBindingFailureAsync(
            store,
            failureMethod,
            pairId,
            unauthorized,
            currentVersion);
        Assert.Contains(
            ReadProperty(unauthorizedResult, "Outcome")?.ToString(),
            new[] { "Rejected", "Conflict" });
        Assert.Equal(beforePhase, await ScalarAsync<string>(
            fixture.ConnectionString,
            $"SELECT \"Phase\" FROM vulgata.\"Pass1PairExecutions\" WHERE \"Id\" = '{pairId:D}';"));

        object staleResult = await InvokeRevisionBindingFailureAsync(
            store,
            failureMethod,
            pairId,
            fixture.Authorization,
            currentVersion + 1);
        Assert.Contains(
            ReadProperty(staleResult, "Outcome")?.ToString(),
            new[] { "Rejected", "Conflict" });
        Assert.Equal(currentVersion, await ReadPairStateVersionAsync(fixture.ConnectionString, pairId));
    }

    [Fact]
    public async Task PostgreSql17_RevisionBindingFailureAfterReconciling_PreservesCompletedChildrenAndDocumentsAsync()
    {
        MethodInfo failureMethod = RequireRevisionBindingFailureMethod();
        await using PreparedScanPostgreSqlFixture fixture = await CreateFixtureAsync();
        await CommitPreparedPairAsync(fixture);
        await PromoteFixtureScanExecutionsAsync(fixture);
        IPreparedPass1Store store = CreatePass1Store(fixture.Context);
        PreparedPass1StartResult started = await store.StartAsync(
            fixture.Authorization,
            CreateHandoff(fixture.Package),
            2,
            CancellationToken.None);
        Assert.Equal(PreparedPass1StartOutcome.Created, started.Outcome);
        Guid pairId = started.PairExecutionId!.Value;
        await MaterializeCompletedDocumentsAsync(fixture, pairId);

        PreparedPass1CompletionResult completion = await store.CompletePass1Async(
            pairId,
            fixture.Authorization,
            PreparedScanTestHost.FixedUtcNow.AddMinutes(2),
            CancellationToken.None);
        Assert.Equal(PreparedPass1CompletionOutcome.Succeeded, completion.Outcome);
        Assert.Equal(
            "Reconciling",
            await ScalarAsync<string>(
                fixture.ConnectionString,
                $"SELECT \"Phase\" FROM vulgata.\"Pass1PairExecutions\" WHERE \"Id\" = '{pairId:D}';"));
        Assert.Equal(
            "Completed,Completed",
            await ScalarAsync<string>(
                fixture.ConnectionString,
                "SELECT string_agg(\"Phase\", ',' ORDER BY \"RepositoryId\") FROM vulgata.\"ScanExecutions\";"));
        Assert.Equal(
            "Resolving",
            await ScalarAsync<string>(
                fixture.ConnectionString,
                "SELECT \"Phase\" FROM vulgata.\"TwoRepositoryScanCoordinations\" LIMIT 1;"));

        int documentsBefore = await ScalarAsync<int>(
            fixture.ConnectionString,
            "SELECT count(*)::integer FROM vulgata.\"DocumentVersions\";");
        long versionBefore = await ReadPairStateVersionAsync(fixture.ConnectionString, pairId);
        int outcomesBefore = await ScalarAsync<int>(
            fixture.ConnectionString,
            "SELECT count(*)::integer FROM vulgata.\"Pass1Outcomes\";");

        object result = await InvokeRevisionBindingFailureAsync(
            store,
            failureMethod,
            pairId,
            fixture.Authorization,
            versionBefore);
        Assert.Equal("FailedIntegrity", ReadProperty(result, "PairState")?.ToString());
        Assert.Equal("revision-binding-failure", ReadProperty(result, "FailureCode"));
        Assert.Equal(
            "FailedIntegrity",
            await ScalarAsync<string>(
                fixture.ConnectionString,
                $"SELECT \"Phase\" FROM vulgata.\"Pass1PairExecutions\" WHERE \"Id\" = '{pairId:D}';"));
        Assert.Equal(
            "FailedIntegrity",
            await ScalarAsync<string>(
                fixture.ConnectionString,
                "SELECT \"Phase\" FROM vulgata.\"TwoRepositoryScanCoordinations\" LIMIT 1;"));
        Assert.Equal(
            "Completed,Completed",
            await ScalarAsync<string>(
                fixture.ConnectionString,
                "SELECT string_agg(\"Phase\", ',' ORDER BY \"RepositoryId\") FROM vulgata.\"ScanExecutions\";"));
        Assert.Equal(
            documentsBefore,
            await ScalarAsync<int>(
                fixture.ConnectionString,
                "SELECT count(*)::integer FROM vulgata.\"DocumentVersions\";"));
        Assert.Equal(
            outcomesBefore,
            await ScalarAsync<int>(
                fixture.ConnectionString,
                "SELECT count(*)::integer FROM vulgata.\"Pass1Outcomes\";"));
        Assert.Equal(
            0,
            await ScalarAsync<int>(
                fixture.ConnectionString,
                "SELECT count(*)::integer FROM vulgata.\"ScanJobExecutions\" WHERE \"State\" <> 'Completed';"));

        long failedVersion = await ReadPairStateVersionAsync(fixture.ConnectionString, pairId);
        object replay = await InvokeRevisionBindingFailureAsync(
            store,
            failureMethod,
            pairId,
            fixture.Authorization,
            failedVersion);
        Assert.Contains("Replay", ReadProperty(replay, "Outcome")?.ToString() ?? string.Empty);
        Assert.Equal(failedVersion, await ReadPairStateVersionAsync(fixture.ConnectionString, pairId));
        Assert.Equal(
            documentsBefore,
            await ScalarAsync<int>(
                fixture.ConnectionString,
                "SELECT count(*)::integer FROM vulgata.\"DocumentVersions\";"));
    }

    private static IPreparedPass1Store CreatePass1Store(VulgataDbContext context) =>
        new PreparedPass1Store(context);

    private static VulgataDbContext CreateRetryingContext(string connectionString)
    {
        DbContextOptions<VulgataDbContext> options =
            new DbContextOptionsBuilder<VulgataDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3))
                .Options;
        return new VulgataDbContext(options);
    }

    private static MethodInfo RequireRevisionBindingFailureMethod()
    {
        MethodInfo? method = typeof(IPreparedPass1Store).GetMethods()
            .SingleOrDefault(candidate =>
                candidate.Name.Contains("Fail", StringComparison.OrdinalIgnoreCase) &&
                candidate.Name.Contains("Revision", StringComparison.OrdinalIgnoreCase) &&
                candidate.Name.Contains("Binding", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            method is not null,
            "T062 RED: IPreparedPass1Store has no atomic FailRevisionBindingAsync " +
            "transition for rejected exact pair acquisition.");
        return method!;
    }

    private static async Task<object> InvokeRevisionBindingFailureAsync(
        IPreparedPass1Store store,
        MethodInfo method,
        Guid pairId,
        PreparedScanAuthorizationScope authorization,
        long expectedStateVersion)
    {
        object?[] arguments = method.GetParameters()
            .Select(parameter => (object?)(parameter.ParameterType == typeof(Guid)
                ? pairId
                : parameter.ParameterType == typeof(PreparedScanAuthorizationScope)
                    ? authorization
                    : parameter.ParameterType == typeof(long)
                        ? expectedStateVersion
                        : parameter.ParameterType == typeof(DateTimeOffset)
                            ? PreparedScanTestHost.FixedUtcNow.AddMinutes(1)
                            : parameter.ParameterType == typeof(CancellationToken)
                                ? CancellationToken.None
                                : null))
            .ToArray();
        object invocation = method.Invoke(store, arguments)!;
        Task task = Assert.IsAssignableFrom<Task>(invocation);
        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static object? ReadProperty(object instance, string name) =>
        instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);

    private static Task<long> ReadPairStateVersionAsync(string connectionString, Guid pairId) =>
        ScalarAsync<long>(
            connectionString,
            $"SELECT \"StateVersion\" FROM vulgata.\"Pass1PairExecutions\" WHERE \"Id\" = '{pairId:D}';");

    private static async Task MaterializeCompletedDocumentsAsync(
        PreparedScanPostgreSqlFixture fixture,
        Guid pairId)
    {
        DateTimeOffset now = PreparedScanTestHost.FixedUtcNow.AddMinutes(1);
        ScanJobExecution[] jobs = await fixture.Context.ScanJobExecutions
            .AsNoTracking()
            .OrderBy(job => job.Id)
            .ToArrayAsync();
        ScanExecution[] executions = await fixture.Context.ScanExecutions
            .AsNoTracking()
            .ToArrayAsync();
        Dictionary<Guid, ScanExecution> executionsById = executions.ToDictionary(value => value.Id);
        List<(LogicalDocument Logical, DocumentVersion Version, ScanJobExecution Job)> materialized = [];
        foreach (ScanJobExecution job in jobs)
        {
            ScanExecution execution = executionsById[job.ScanExecutionId];
            CodeUnit unit = await fixture.Context.CodeUnits
                .AsNoTracking()
                .SingleAsync(value => value.Id == job.FileCodeUnitId);
            Guid logicalId = StableGuid($"t062-reconciling-logical:{job.Id:D}");
            Guid versionId = StableGuid($"t062-reconciling-version:{job.Id:D}");
            LogicalDocument logical = LogicalDocument.Create(
                logicalId,
                job.RepositoryId,
                DocumentType.CodeLogic,
                $"{unit.FilePath}#segment={unit.SegmentOrdinal}",
                now);
            DocumentVersion version = DocumentVersion.Create(
                versionId,
                logicalId,
                job.RepositoryId,
                execution.SystemId,
                job.ScanRunId,
                execution.ProfiledRevision,
                1,
                unit.FilePath,
                unit.FilePath,
                "code-logic/file-v2",
                "{}",
                "# completed",
                new string('1', 64),
                new string('2', 64),
                now);
            materialized.Add((logical, version, job));
            fixture.Context.LogicalDocuments.Add(logical);
        }

        await fixture.Context.SaveChangesAsync();
        foreach ((LogicalDocument logical, DocumentVersion version, _) in materialized)
        {
            fixture.Context.DocumentVersions.Add(version);
            logical.SetInitialCurrentVersion(version);
        }

        await fixture.Context.SaveChangesAsync();
        foreach ((_, DocumentVersion version, ScanJobExecution job) in materialized)
        {
            await fixture.Context.ScanJobExecutions
                .Where(value => value.Id == job.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.State, ScanJobExecutionState.Completed)
                    .SetProperty(value => value.StateVersion, 1L)
                    .SetProperty(value => value.LeaseToken, (Guid?)null)
                    .SetProperty(value => value.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(value => value.NextAttemptAt, (DateTimeOffset?)null)
                    .SetProperty(value => value.FailureCategory, (ScanJobFailureCategory?)null)
                    .SetProperty(value => value.FailureCode, (string?)null)
                    .SetProperty(value => value.CommittedDocumentVersionId, version.Id)
                    .SetProperty(value => value.StateChangedAt, now));
        }

        fixture.Context.ChangeTracker.Clear();
    }

    private static Guid StableGuid(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16], bigEndian: true);
    }

    private static PreparedScanHandoff CreateHandoff(PreparedScanPairPackage package) =>
        new(
            PreScanResult.PreparedHandoffSchemaIdentity,
            package.CoordinationId,
            package.PairSelectionFingerprint,
            package.PairPackageFingerprint,
            package.Repositories
                .OrderBy(repository => repository.RepositoryId)
                .Select(repository => new PreparedScanManifestBinding(
                    repository.RepositoryId,
                    repository.ScanRunId,
                    repository.ProfiledRevision,
                    PreparedScanIdentityBuilders.CreateDeterministicIdentity(
                        "PreparedScanManifest", repository.ManifestFingerprint),
                    repository.ManifestFingerprint,
                    PreparedScanIdentityBuilders.CreateCodeGraphProof(repository.CodeGraphProof).Fingerprint,
                    PreparedScanIdentityBuilders.CreateJavaReadiness(repository.JavaReadiness).Fingerprint,
                    repository.PreparedScanContextIdentityHash))
                .ToArray(),
            package.Repositories.SelectMany(repository => repository.FileCodeUnits).ToArray(),
            package.SelectedCount);

    private static async Task CommitPreparedPairAsync(PreparedScanPostgreSqlFixture fixture)
    {
        PreparedScanCommitResult result = await PreparedScanPostgreSqlFixture
            .CreateStore(fixture.Context)
            .CommitPairAsync(fixture.Authorization, fixture.Package, 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, result.Outcome);
    }

    private static async Task PromoteFixtureScanExecutionsAsync(
        PreparedScanPostgreSqlFixture fixture)
    {
        const string documentSchemaIdentity = "code-logic/file-v2";
        const string runtimeIdentityJson = "{\"runtime\":\"story-12-3-test\"}";
        string runtimeIdentityHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(runtimeIdentityJson)));
        Guid[] repositoryIds = fixture.Package.Repositories
            .Select(repository => repository.RepositoryId)
            .OrderBy(repositoryId => repositoryId)
            .ToArray();
        DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;

        for (int index = 0; index < repositoryIds.Length; index++)
        {
            int updated = await fixture.Context.ScanExecutions
                .Where(execution => execution.RepositoryId == repositoryIds[index])
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(execution => execution.PreScanResultId, Guid.Empty)
                    .SetProperty(execution => execution.Phase, ScanExecutionPhase.Prepared)
                    .SetProperty(execution => execution.PromptIdentity, $"story-12-3-test-prompt-{index + 1}")
                    .SetProperty(execution => execution.DocumentSchemaIdentity, documentSchemaIdentity)
                    .SetProperty(execution => execution.AnalysisRuntimeIdentityJson, runtimeIdentityJson)
                    .SetProperty(execution => execution.AnalysisRuntimeIdentityHash, runtimeIdentityHash)
                    .SetProperty(execution => execution.UpdatedAt, now));
            Assert.Equal(1, updated);
        }

        int updatedManifests = await fixture.Context.PreparedScanManifests
            .Where(manifest => repositoryIds.Contains(manifest.RepositoryId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(manifest => manifest.RuntimeIdentityHash, runtimeIdentityHash)
                .SetProperty(manifest => manifest.CodeGraphRuntimeIdentityHash, runtimeIdentityHash)
                .SetProperty(manifest => manifest.JavaReadinessRuntimeIdentityHash, runtimeIdentityHash));
        Assert.Equal(2, updatedManifests);

        fixture.Context.ChangeTracker.Clear();
    }

    private static bool IsInjectedTriggerException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                postgres.MessageText.Contains("T015 injected start boundary", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task InstallFailingTriggerAsync(
        string connectionString,
        string boundary,
        string table,
        string operation,
        string timing)
    {
        string suffix = boundary.Replace('-', '_');
        await ExecuteSqlAsync(connectionString, $"""
            CREATE OR REPLACE FUNCTION t123_fail_{suffix}() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'T015 injected start boundary: {boundary}';
            END $$;
            CREATE TRIGGER t123_fail_{suffix}_trigger
            {timing} {operation} ON {table}
            FOR EACH ROW EXECUTE FUNCTION t123_fail_{suffix}();
            """);
    }

    private static async Task<StartVisibility> ReadVisibilityAsync(string connectionString) => new(
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"Pass1PairExecutions\";"),
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"ScanJobExecutions\";"),
        await ScalarAsync<int>(connectionString, "SELECT count(*)::integer FROM vulgata.\"Pass1NavigationBudgets\";"),
        await ScalarAsync<string>(connectionString, "SELECT coalesce(string_agg(\"Phase\", ',' ORDER BY \"Id\"), '') FROM vulgata.\"ScanExecutions\";"),
        await ScalarAsync<string>(connectionString, "SELECT coalesce(string_agg(\"Phase\", ',' ORDER BY \"Id\"), '') FROM vulgata.\"TwoRepositoryScanCoordinations\";"));

    private static async Task<PreparedScanPostgreSqlFixture> CreateFixtureAsync()
    {
        string administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN")
            ?? throw new InvalidOperationException(
                "T015 requires VULGATA_TEST_POSTGRES_ADMIN for an isolated PostgreSQL 17 database; no integration prerequisite was contacted.");
        return await PreparedScanPostgreSqlFixture.CreateAsync(administrator);
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        object? value = await command.ExecuteScalarAsync();
        if (value is null or DBNull)
        {
            return default!;
        }

        return value is T typed
            ? typed
            : (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record StartVisibility(
        int PairExecutions,
        int Jobs,
        int Budgets,
        string ScanPhases,
        string CoordinationPhases);
}

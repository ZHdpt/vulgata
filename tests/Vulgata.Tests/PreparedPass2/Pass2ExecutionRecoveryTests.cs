using Vulgata.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.Migrations;
using Vulgata.Web.Data;
using Xunit.Abstractions;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T039 RED/characterization slice for the durable Pass 2 execution boundary.
///
/// The Core job state machine already exposes the accepted lease and one-retry
/// rules, so those tests are characterization checks and are not presented as
/// fabricated REDs.  The durable claim/recovery and authenticated Web
/// coordination seams are exercised directly below.  No test-local scheduler
/// or recovery implementation is supplied.
/// </summary>
public sealed class Pass2ExecutionRecoveryTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset _start =
        new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Pass2JobExecution_AcquireLease_IsSingleAttemptAndBindsLeaseIdentity()
    {
        Pass2JobExecution job = NewJob();
        Guid leaseToken = Guid.NewGuid();

        job.AcquireLease(leaseToken, _start.AddMinutes(10), _start.AddMinutes(1));

        Assert.Equal(Pass2JobExecutionState.Running, job.State);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(leaseToken, job.LeaseToken);
        Assert.Equal(_start.AddMinutes(10), job.LeaseExpiresAt);
        Assert.Equal(1, job.StateVersion);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Pass2JobExecution_ScheduleRetry_PreservesJobIdentityAndAllowsOneDurableRetry()
    {
        Pass2JobExecution job = NewJob();
        Guid leaseToken = Guid.NewGuid();
        job.AcquireLease(leaseToken, _start.AddMinutes(10), _start.AddMinutes(1));

        job.ScheduleRetry(
            job.StateVersion,
            leaseToken,
            _start.AddMinutes(3),
            ScanJobFailureCategory.JobPermanent,
            "invalid-output",
            _start.AddMinutes(2));

        Assert.Equal(Pass2JobExecutionState.RetryScheduled, job.State);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(_start.AddMinutes(3), job.NextAttemptAt);
        Assert.Null(job.LeaseToken);
        Assert.Equal(ScanJobFailureCategory.JobPermanent, job.FailureCategory);
        Assert.Equal("invalid-output", job.FailureCode);
        Assert.Equal(2, job.StateVersion);

        job.RecoverDueRetry(job.StateVersion, _start.AddMinutes(3));
        Guid retryLeaseToken = Guid.NewGuid();
        job.AcquireLease(retryLeaseToken, _start.AddMinutes(13), _start.AddMinutes(3));

        Assert.Equal(Pass2JobExecutionState.Running, job.State);
        Assert.Equal(2, job.AttemptCount);
        Assert.Equal(retryLeaseToken, job.LeaseToken);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Pass2JobExecution_RejectsThirdAttemptAfterOneDurableRetry()
    {
        Pass2JobExecution job = NewJob();
        Guid firstLease = Guid.NewGuid();
        job.AcquireLease(firstLease, _start.AddMinutes(10), _start.AddMinutes(1));
        job.ScheduleRetry(
            job.StateVersion,
            firstLease,
            _start.AddMinutes(3),
            ScanJobFailureCategory.JobPermanent,
            "invalid-output",
            _start.AddMinutes(2));
        job.RecoverDueRetry(job.StateVersion, _start.AddMinutes(3));
        Guid secondLease = Guid.NewGuid();
        job.AcquireLease(secondLease, _start.AddMinutes(13), _start.AddMinutes(3));

        Assert.Throws<InvalidOperationException>(() => job.ScheduleRetry(
            job.StateVersion,
            secondLease,
            _start.AddMinutes(5),
            ScanJobFailureCategory.JobPermanent,
            "invalid-output-again",
            _start.AddMinutes(4)));
        Assert.Equal(Pass2JobExecutionState.Running, job.State);
        Assert.Equal(2, job.AttemptCount);
        Assert.Equal(secondLease, job.LeaseToken);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Pass2JobExecution_ExpiredFirstAttempt_IsReclaimedWithSameIdentity()
    {
        Pass2JobExecution job = NewJob();
        Guid leaseToken = Guid.NewGuid();
        job.AcquireLease(leaseToken, _start.AddMinutes(5), _start.AddMinutes(1));

        job.RecoverExpiredLease(job.StateVersion, _start.AddMinutes(5));

        Assert.Equal(Pass2JobExecutionState.Pending, job.State);
        Assert.Equal(1, job.AttemptCount);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseExpiresAt);
        Assert.Equal(2, job.StateVersion);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Pass2JobExecution_ExpiredSecondAttempt_IsTerminalFailureAndCannotReopen()
    {
        Pass2JobExecution job = NewJob();
        Guid firstLease = Guid.NewGuid();
        job.AcquireLease(firstLease, _start.AddMinutes(5), _start.AddMinutes(1));
        job.ScheduleRetry(
            job.StateVersion,
            firstLease,
            _start.AddMinutes(3),
            ScanJobFailureCategory.JobPermanent,
            "invalid-output",
            _start.AddMinutes(2));
        job.RecoverDueRetry(job.StateVersion, _start.AddMinutes(3));
        Guid secondLease = Guid.NewGuid();
        job.AcquireLease(secondLease, _start.AddMinutes(8), _start.AddMinutes(3));

        job.RecoverExpiredLease(job.StateVersion, _start.AddMinutes(8));

        Assert.Equal(Pass2JobExecutionState.Failed, job.State);
        Assert.Equal(2, job.AttemptCount);
        Assert.Equal(ScanJobFailureCategory.Unclassified, job.FailureCategory);
        Assert.Equal("lease-expired", job.FailureCode);
        Assert.Null(job.LeaseToken);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Pass2JobExecution_ProviderPauseReleasesLeaseWithoutChargingAttempt()
    {
        Pass2JobExecution job = NewJob();
        Guid leaseToken = Guid.NewGuid();
        job.AcquireLease(leaseToken, _start.AddMinutes(10), _start.AddMinutes(1));

        job.ReleaseForProviderPause(job.StateVersion, leaseToken, _start.AddMinutes(2));

        Assert.Equal(Pass2JobExecutionState.Pending, job.State);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseExpiresAt);
        Assert.Equal(2, job.StateVersion);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Pass2Execution_ProviderPauseAndResume_PreserveExecutionIdentity()
    {
        Pass2Execution execution = Pass2Execution.CreatePlanning(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _start);
        execution.MarkRunning(_start.AddMinutes(1));
        Guid executionId = execution.Id;

        execution.PauseProviderUnavailable(execution.StateVersion, _start.AddMinutes(2));
        Assert.Equal(Pass2ExecutionState.PausedProviderUnavailable, execution.State);
        execution.ResumeProvider(execution.StateVersion, _start.AddMinutes(3));

        Assert.Equal(executionId, execution.Id);
        Assert.Equal(Pass2ExecutionState.Running, execution.State);
        Assert.Equal(3, execution.StateVersion);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Pass2Execution_ExposesRunningTerminalAccountingForRecoveryPreservation()
    {
        Pass2Execution execution = Pass2Execution.CreatePlanning(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _start);
        execution.MarkRunning(_start.AddMinutes(1));

        execution.RecordPublished(execution.StateVersion, _start.AddMinutes(2));
        execution.RecordFailed(execution.StateVersion, _start.AddMinutes(3));

        Assert.Equal(1, execution.PublishedCount);
        Assert.Equal(1, execution.FailedCount);
        Assert.Equal(3, execution.StateVersion);
        Assert.Equal(Pass2ExecutionState.Running, execution.State);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Pass2RetrievalBudget_CountersRemainDurableAcrossJobLocalRetry()
    {
        Pass2RetrievalBudget budget = Pass2RetrievalBudget.Create(Guid.NewGuid(), _start);

        budget.ReserveCall(_start.AddMinutes(1));
        budget.Admit(2, 1, 128, _start.AddMinutes(2));

        Assert.Equal(1, budget.CallsReserved);
        Assert.Equal(2, budget.ResultsReturned);
        Assert.Equal(1, budget.AdditionalDocumentCount);
        Assert.Equal(128, budget.ReturnedUtf8Bytes);
        Assert.Equal(Pass2RetrievalBudgetState.Available, budget.State);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ClaimNextAsync_SerialCanonicalOrder_AllowsOnlyOneActiveJobAsync()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();

        PreparedPass2ClaimResult first = await fixture.Store.ClaimNextAsync(
            fixture.Execution.Id, _start.AddMinutes(1), TimeSpan.FromMinutes(5));
        PreparedPass2ClaimResult second = await fixture.Store.ClaimNextAsync(
            fixture.Execution.Id, _start.AddMinutes(2), TimeSpan.FromMinutes(5));

        Assert.Equal(PreparedPass2ClaimOutcome.Claimed, first.Outcome);
        Assert.Equal(fixture.FirstJob.Id, first.JobId);
        Assert.Equal(PreparedPass2ClaimOutcome.Active, second.Outcome);
        Assert.Equal(first.JobId, second.JobId);
        Assert.Equal(first.LeaseToken, second.LeaseToken);
        Assert.Equal(Pass2JobExecutionState.Running, await fixture.StateAsync(fixture.FirstJob.Id));
        Assert.Equal(Pass2JobExecutionState.Pending, await fixture.StateAsync(fixture.SecondJob.Id));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ClaimNextAsync_ExpiredLease_IsReclaimedOnHostRestartWithoutChangingJobIdentityAsync()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();

        PreparedPass2ClaimResult first = await fixture.Store.ClaimNextAsync(
            fixture.Execution.Id, _start.AddMinutes(1), TimeSpan.FromMinutes(5));
        PreparedPass2ClaimResult recovered = await fixture.Store.ClaimNextAsync(
            fixture.Execution.Id, _start.AddMinutes(6), TimeSpan.FromMinutes(5));

        Assert.Equal(PreparedPass2ClaimOutcome.Claimed, first.Outcome);
        Assert.Equal(PreparedPass2ClaimOutcome.Claimed, recovered.Outcome);
        Assert.Equal(first.JobId, recovered.JobId);
        Assert.Equal(2, recovered.AttemptOrdinal);
        Assert.NotEqual(first.LeaseToken, recovered.LeaseToken);
        Assert.Equal(2, await fixture.AttemptCountAsync(fixture.FirstJob.Id));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ClaimNextAsync_DueRetry_IsReclaimedAsSecondAttemptWithSameBudgetIdentityAsync()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        PreparedPass2ClaimResult first = await fixture.Store.ClaimNextAsync(
            fixture.Execution.Id, _start.AddMinutes(1), TimeSpan.FromMinutes(5));

        Pass2JobExecution tracked = await fixture.Context.Pass2JobExecutions
            .SingleAsync(value => value.Id == fixture.FirstJob.Id);
        tracked.ScheduleRetry(
            tracked.StateVersion,
            first.LeaseToken!.Value,
            _start.AddMinutes(3),
            ScanJobFailureCategory.JobPermanent,
            "invalid-output",
            _start.AddMinutes(2));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        PreparedPass2ClaimResult retry = await fixture.Store.ClaimNextAsync(
            fixture.Execution.Id, _start.AddMinutes(3), TimeSpan.FromMinutes(5));

        Assert.Equal(PreparedPass2ClaimOutcome.Claimed, retry.Outcome);
        Assert.Equal(fixture.FirstJob.Id, retry.JobId);
        Assert.Equal(2, retry.AttemptOrdinal);
        Assert.Equal(0, await fixture.BudgetCallCountAsync(fixture.FirstJob.Id));
        Assert.Equal(2, await fixture.AttemptCountAsync(fixture.FirstJob.Id));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(ScanJobFailureCategory.Integrity, Pass2ExecutionState.FailedIntegrity,
        TwoRepositoryScanCoordinationPhase.FailedIntegrity)]
    [InlineData(ScanJobFailureCategory.Security, Pass2ExecutionState.FailedSecurity,
        TwoRepositoryScanCoordinationPhase.FailedSecurity)]
    public async Task RecordDispatchFailureAsync_IntegrityOrSecurityTerminalizesExecutionAndCoordinationAtomicallyAsync(
        ScanJobFailureCategory category,
        Pass2ExecutionState expectedExecutionState,
        TwoRepositoryScanCoordinationPhase expectedCoordinationPhase)
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        PreparedPass2ClaimResult claim = await fixture.Store.ClaimNextAsync(
            fixture.Execution.Id, _start.AddMinutes(1), TimeSpan.FromMinutes(5));
        Assert.Equal(PreparedPass2ClaimOutcome.Claimed, claim.Outcome);
        int priorDocumentCount = await fixture.DocumentCountAsync();

        PreparedPass2DispatchFailureOutcome outcome =
            await fixture.Store.RecordDispatchFailureAsync(
                fixture.Execution.Id,
                claim.JobId!.Value,
                claim.JobStateVersion,
                claim.LeaseToken!.Value,
                category,
                category == ScanJobFailureCategory.Integrity
                    ? "integrity-failure"
                    : "security-failure",
                _start.AddMinutes(2));

        Assert.Equal(PreparedPass2DispatchFailureOutcome.Failed, outcome);
        Assert.Equal(expectedExecutionState, await fixture.ExecutionStateAsync());
        Assert.Equal(expectedCoordinationPhase, await fixture.CoordinationPhaseAsync());
        Assert.Equal(Pass2JobExecutionState.Failed, await fixture.StateAsync(fixture.FirstJob.Id));
        Assert.Null(await fixture.LeaseTokenAsync(fixture.FirstJob.Id));
        Assert.Equal(priorDocumentCount, await fixture.DocumentCountAsync());

        PreparedPass2ClaimResult laterClaim = await fixture.Store.ClaimNextAsync(
            fixture.Execution.Id, _start.AddMinutes(3), TimeSpan.FromMinutes(5));
        Assert.Equal(PreparedPass2ClaimOutcome.Ineligible, laterClaim.Outcome);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSql17_ConcurrentClaimHasOneActiveJobAndPreservesJobBudgetIdentityAsync()
    {
        string? administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administrator))
        {
            output.WriteLine(
                "Qualification not executed: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
            throw new InvalidOperationException("Required PostgreSQL qualification prerequisite is unavailable; use eng/test.ps1 -Suite PostgreSql.");
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administrator);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using VulgataDbContext seedContext = CreatePostgreSqlContext(database.ConnectionString);
            await seedContext.Database.MigrateAsync();
            (Guid executionId, Guid firstJobId) = await SeedPostgreSqlClaimGraphAsync(seedContext);

            await using VulgataDbContext firstContext = CreatePostgreSqlContext(database.ConnectionString);
            await using VulgataDbContext secondContext = CreatePostgreSqlContext(database.ConnectionString);
            Task<PreparedPass2ClaimResult> firstTask = new PreparedPass2Store(firstContext).ClaimNextAsync(
                executionId, _start.AddMinutes(1), TimeSpan.FromMinutes(5));
            Task<PreparedPass2ClaimResult> secondTask = new PreparedPass2Store(secondContext).ClaimNextAsync(
                executionId, _start.AddMinutes(1), TimeSpan.FromMinutes(5));
            PreparedPass2ClaimResult[] claims = await Task.WhenAll(firstTask, secondTask);

            Assert.Single(claims, value => value.Outcome == PreparedPass2ClaimOutcome.Claimed);
            Assert.DoesNotContain(claims, value => value.Outcome == PreparedPass2ClaimOutcome.Exhausted);
            Assert.All(claims.Where(value => value.Outcome == PreparedPass2ClaimOutcome.Claimed), value =>
            {
                Assert.Equal(firstJobId, value.JobId);
                Assert.Equal(1, value.AttemptOrdinal);
            });

            await using VulgataDbContext verifyContext = CreatePostgreSqlContext(database.ConnectionString);
            Assert.Equal(1, await verifyContext.Pass2JobExecutions.CountAsync(value =>
                value.Pass2ExecutionId == executionId && value.State == Pass2JobExecutionState.Running));
            Assert.Equal(0, await verifyContext.Pass2RetrievalBudgets
                .Where(value => value.Pass2JobExecutionId == firstJobId)
                .Select(value => value.CallsReserved)
                .SingleAsync());
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertPostgreSqlDatabaseDroppedAsync(administrator, databaseName);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PostgreSql17_ExpiredLeaseAndDueRetryPreserveJobAndBudgetIdentityAsync()
    {
        string? administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administrator))
        {
            output.WriteLine(
                "Qualification not executed: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
            throw new InvalidOperationException("Required PostgreSQL qualification prerequisite is unavailable; use eng/test.ps1 -Suite PostgreSql.");
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administrator);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using VulgataDbContext context = CreatePostgreSqlContext(database.ConnectionString);
            await context.Database.MigrateAsync();
            (Guid executionId, Guid firstJobId) = await SeedPostgreSqlClaimGraphAsync(context);
            var store = new PreparedPass2Store(context);

            PreparedPass2ClaimResult first = await store.ClaimNextAsync(
                executionId, _start.AddMinutes(1), TimeSpan.FromMinutes(5));
            Assert.Equal(PreparedPass2ClaimOutcome.Claimed, first.Outcome);
            Assert.Equal(firstJobId, first.JobId);

            Pass2JobExecution job = await context.Pass2JobExecutions.SingleAsync(value => value.Id == firstJobId);
            job.ScheduleRetry(
                job.StateVersion,
                first.LeaseToken!.Value,
                _start.AddMinutes(3),
                ScanJobFailureCategory.JobPermanent,
                "provider-invalid-output",
                _start.AddMinutes(2));
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            PreparedPass2ClaimResult retry = await store.ClaimNextAsync(
                executionId, _start.AddMinutes(3), TimeSpan.FromMinutes(5));
            Assert.Equal(PreparedPass2ClaimOutcome.Claimed, retry.Outcome);
            Assert.Equal(firstJobId, retry.JobId);
            Assert.Equal(2, retry.AttemptOrdinal);
            Assert.NotEqual(first.LeaseToken, retry.LeaseToken);
            Assert.Equal(0, await context.Pass2RetrievalBudgets
                .Where(value => value.Pass2JobExecutionId == firstJobId)
                .Select(value => value.CallsReserved)
                .SingleAsync());
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertPostgreSqlDatabaseDroppedAsync(administrator, databaseName);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task RecoverAsync_CancelledBeforeDatabaseAccess_PreservesDurableIdentityAsync()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        await using VulgataDbContext context = CreateContext(connection);
        var store = new PreparedPass2Store(context);
        RepositoryAccessScope scope = new([Guid.NewGuid(), Guid.NewGuid()]);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Guid executionId = Guid.NewGuid();

        PreparedPass2RecoveryResult result = await store.RecoverAsync(
            executionId,
            scope,
            providerAvailable: true,
            _start,
            cancellation.Token);

        Assert.Equal(PreparedPass2RecoveryOutcome.Cancelled, result.Outcome);
        Assert.Equal(executionId, result.ExecutionId);
        Assert.Equal(0, result.RecoveredJobCount);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task Coordinator_StartAndResume_UseAuthenticatedUserWithoutCallerSuppliedScopeAsync()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        await using VulgataDbContext context = CreateContext(connection);
        var coordinator = new PreparedPass2Coordinator(
            null!, null!, null!, null!, null!, new PreparedPass2ExecutionActivator(), TimeProvider.System);
        FakeUserContext user = new("owner-1");

        PreparedPass2StartResult start = await coordinator.StartAsync(Guid.Empty, user);
        PreparedPass2RecoveryResult resume = await coordinator.ResumeAsync(Guid.Empty, user);

        Assert.Equal(PreparedPass2StartOutcome.NotFound, start.Outcome);
        Assert.Equal(PreparedPass2RecoveryOutcome.NotFound, resume.Outcome);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ExecutionActivator_SignalsOnlyTheDurableExecutionIdentityAsync()
    {
        var activator = new PreparedPass2ExecutionActivator();
        Guid executionId = Guid.NewGuid();
        activator.Signal(executionId);

        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(1));
        Guid observed = await activator.ReadAllAsync(cancellation.Token).FirstAsync(cancellation.Token);

        Assert.Equal(executionId, observed);
        Assert.NotEqual(Guid.Empty, observed);
    }

    private static VulgataDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<VulgataDbContext>().UseSqlite(connection).Options);

    private static VulgataDbContext CreatePostgreSqlContext(string connectionString) =>
        new(new DbContextOptionsBuilder<VulgataDbContext>().UseNpgsql(connectionString).Options);

    private static async Task<(Guid ExecutionId, Guid FirstJobId)> SeedPostgreSqlClaimGraphAsync(
        VulgataDbContext context)
    {
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("SET session_replication_role = replica;");
        Guid executionId = Guid.NewGuid();
        Guid firstJobId = Guid.NewGuid();
        Guid secondJobId = Guid.NewGuid();
        Pass2Execution execution = Pass2Execution.CreatePlanning(
            executionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _start);
        execution.MarkRunning(_start.AddMinutes(1));
        FlowGroup firstGroup = FlowGroup.CreatePlanned(
            Guid.NewGuid(),
            executionId,
            execution.SystemId,
            ExactResolutionProtocol.CustomRpc,
            "rpc.example/operation",
            "consumer.operation",
            new string('c', 64),
            "consumer.operation",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            new string('a', 64),
            new string('b', 64),
            _start);
        FlowGroup secondGroup = FlowGroup.CreatePlanned(
            Guid.NewGuid(),
            executionId,
            execution.SystemId,
            ExactResolutionProtocol.CustomRpc,
            "rpc.example/operation-two",
            "consumer.operation-two",
            new string('d', 64),
            "consumer.operation-two",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            new string('e', 64),
            new string('f', 64),
            _start);
        Pass2JobExecution first = Pass2JobExecution.CreatePending(
            firstJobId, executionId, firstGroup.Id, 1, _start);
        Pass2JobExecution second = Pass2JobExecution.CreatePending(
            secondJobId, executionId, secondGroup.Id, 2, _start);
        context.AddRange(
            execution,
            firstGroup,
            secondGroup,
            first,
            second,
            Pass2RetrievalBudget.Create(firstJobId, _start),
            Pass2RetrievalBudget.Create(secondJobId, _start));
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("SET session_replication_role = origin;");
        context.ChangeTracker.Clear();
        return (executionId, firstJobId);
    }

    private static async Task AssertPostgreSqlDatabaseDroppedAsync(
        string administratorConnectionString,
        string databaseName)
    {
        await using NpgsqlConnection connection = new(administratorConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection);
        command.Parameters.AddWithValue("name", databaseName);
        Assert.False(Assert.IsType<bool>(await command.ExecuteScalarAsync()));
    }

    private sealed class RecoveryFixture : IAsyncDisposable
    {
        private RecoveryFixture(
            SqliteConnection connection,
            VulgataDbContext context,
            Pass2Execution execution,
            Pass2JobExecution firstJob,
            Pass2JobExecution secondJob)
        {
            Connection = connection;
            Context = context;
            Execution = execution;
            FirstJob = firstJob;
            SecondJob = secondJob;
            Store = new PreparedPass2Store(context);
        }

        internal SqliteConnection Connection { get; }
        internal VulgataDbContext Context { get; }
        internal Pass2Execution Execution { get; }
        internal Pass2JobExecution FirstJob { get; }
        internal Pass2JobExecution SecondJob { get; }
        internal PreparedPass2Store Store { get; }

        internal static async Task<RecoveryFixture> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=False");
            connection.Open();
            var context = new VulgataDbContext(
                new DbContextOptionsBuilder<VulgataDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();

            Pass2Execution execution = Pass2Execution.CreatePlanning(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _start);
            execution.MarkRunning(_start.AddMinutes(1));
            Pass2JobExecution first = Pass2JobExecution.CreatePending(
                Guid.NewGuid(), execution.Id, Guid.NewGuid(), 1, _start);
            Pass2JobExecution second = Pass2JobExecution.CreatePending(
                Guid.NewGuid(), execution.Id, Guid.NewGuid(), 2, _start);
            context.AddRange(first, second);
            context.AddRange(
                Pass2RetrievalBudget.Create(first.Id, _start),
                Pass2RetrievalBudget.Create(second.Id, _start));

            Guid systemId = execution.SystemId;
            Guid repositoryOneId = Guid.NewGuid();
            Guid repositoryTwoId = Guid.NewGuid();
            ScanExecution scanOne = NewScanExecution(
                repositoryOneId, systemId, Guid.NewGuid(), _start);
            ScanExecution scanTwo = NewScanExecution(
                repositoryTwoId, systemId, Guid.NewGuid(), _start);
            scanOne.BeginRunning(scanOne.StateVersion, _start.AddMinutes(1));
            scanTwo.BeginRunning(scanTwo.StateVersion, _start.AddMinutes(1));
            TwoRepositoryScanCoordination coordination =
                TwoRepositoryScanCoordination.Create(scanOne, scanTwo, _start.AddMinutes(1));
            coordination.BeginPass1(coordination.StateVersion, _start.AddMinutes(1));
            coordination.BeginResolving(coordination.StateVersion, _start.AddMinutes(1));
            coordination.BeginPass2(coordination.StateVersion, _start.AddMinutes(1));
            execution = Pass2Execution.CreatePlanning(
                execution.Id,
                execution.Pass1PairExecutionId,
                coordination.Id,
                execution.SystemId,
                _start);
            execution.MarkRunning(_start.AddMinutes(1));

            FlowGroup group = FlowGroup.CreatePlanned(
                first.FlowGroupId,
                execution.Id,
                systemId,
                ExactResolutionProtocol.CustomRpc,
                "rpc.example/operation",
                "consumer.operation",
                new string('c', 64),
                "consumer.operation",
                Guid.NewGuid(),
                repositoryOneId,
                scanOne.ScanRunId,
                repositoryTwoId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                new string('a', 64),
                new string('b', 64),
                _start);
            DocumentVersion priorDocument = DocumentVersion.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                repositoryTwoId,
                systemId,
                scanTwo.ScanRunId,
                new string('d', 40),
                1,
                "Prior document",
                "src/Prior.cs",
                KnowledgeDocumentLimits.SchemaIdentity,
                "{}",
                "prior document",
                new string('e', 64),
                new string('f', 64),
                _start);
            context.ChangeTracker.Clear();
            context.AddRange(
                scanOne,
                scanTwo,
                coordination,
                execution,
                first,
                second,
                Pass2RetrievalBudget.Create(first.Id, _start),
                Pass2RetrievalBudget.Create(second.Id, _start),
                group,
                priorDocument);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return new(connection, context, execution, first, second);
        }

        private static ScanExecution NewScanExecution(
            Guid repositoryId,
            Guid systemId,
            Guid scanRunId,
            DateTimeOffset startedAt) => ScanExecution.CreateNewPath(
                Guid.NewGuid(),
                repositoryId,
                systemId,
                scanRunId,
                new string('1', 40),
                "prompt/1.0",
                "document-schema/1.0",
                "{}",
                new string('2', 64),
                startedAt);

        internal async Task<Pass2JobExecutionState> StateAsync(Guid id) =>
            await Context.Pass2JobExecutions.AsNoTracking()
                .Where(value => value.Id == id)
                .Select(value => value.State)
                .SingleAsync();

        internal async Task<int> AttemptCountAsync(Guid id) =>
            await Context.Pass2JobExecutions.AsNoTracking()
                .Where(value => value.Id == id)
                .Select(value => value.AttemptCount)
                .SingleAsync();

        internal async Task<int> BudgetCallCountAsync(Guid jobId) =>
            await Context.Pass2RetrievalBudgets.AsNoTracking()
                .Where(value => value.Pass2JobExecutionId == jobId)
                .Select(value => value.CallsReserved)
                .SingleAsync();

        internal async Task<Pass2ExecutionState> ExecutionStateAsync() =>
            await Context.Pass2Executions.AsNoTracking()
                .Where(value => value.Id == Execution.Id)
                .Select(value => value.State)
                .SingleAsync();

        internal async Task<TwoRepositoryScanCoordinationPhase> CoordinationPhaseAsync() =>
            await Context.TwoRepositoryScanCoordinations.AsNoTracking()
                .Where(value => value.Id == Execution.CoordinationId)
                .Select(value => value.Phase)
                .SingleAsync();

        internal async Task<Guid?> LeaseTokenAsync(Guid jobId) =>
            await Context.Pass2JobExecutions.AsNoTracking()
                .Where(value => value.Id == jobId)
                .Select(value => value.LeaseToken)
                .SingleAsync();

        internal Task<int> DocumentCountAsync() =>
            Context.DocumentVersions.CountAsync();

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class FakeUserContext(string userId) : IUserContext
    {
        public string UserId { get; } = userId;
        public bool IsAdministrator => false;
        public SystemOwnerFilter ToSystemOwnerFilter() => SystemOwnerFilter.OwnedBy(UserId);
    }

    private static Pass2JobExecution NewJob() => Pass2JobExecution.CreatePending(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        1,
        _start);
}

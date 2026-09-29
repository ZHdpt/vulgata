using Vulgata.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.Migrations;
using Vulgata.Web.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.DurableScanning;

/// <summary>
/// T028 RED: exercises the durable recovery seam against a real PostgreSQL
/// 17 database across a host/service-provider restart boundary.  This test is
/// intentionally test-only; T029 owns any missing integration behavior.
/// </summary>
public sealed class DurableScanRestartPostgreSqlTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 30, 9, 20, 0, TimeSpan.Zero);
    private const string ProfiledRevision =
        "0123456789abcdef0123456789abcdef01234567";
    private const string RuntimeHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SourceHash =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task RestartThenRepeatedManualResume_PreservesMixedDurableStatesAndRejectsStaleCompetingTransition()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            // The real PostgreSQL qualification is opt-in and must not fall
            // back to SQLite or an in-memory substitute.
            throw new InvalidOperationException("Required PostgreSQL qualification prerequisite is unavailable; use eng/test.ps1 -Suite PostgreSql.");
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string ownedDatabaseName = database.DatabaseName;
        try
        {
            await MigrateAsync(database.ConnectionString);
            RestartFixture fixture = await SeedMixedStateFixtureAsync(database.ConnectionString);

            // A restart is represented by disposing the actual Program host
            // and constructing a fresh production service graph over the same
            // database. The first GetRequiredService calls are deliberately
            // public composition seams owned by T029.
            using (PostgreSqlProductionFactory observationHost =
                new(database.ConnectionString, ObservedAt))
            {
                using IServiceScope scope = observationHost.Services.CreateScope();
                IDurableScanStore store = scope.ServiceProvider
                    .GetRequiredService<IDurableScanStore>();
                ScanRecoverySnapshotResult before = await store.ReadRecoverySnapshotAsync(
                    fixture.Scope,
                    fixture.ScanExecutionId,
                    CancellationToken.None);
                ScanRecoverySnapshot snapshot = Assert.IsType<ScanRecoverySnapshot>(before.Snapshot);

                Assert.Equal(DurableScanStoreOutcome.Applied, before.Outcome);
                Assert.Equal(8, snapshot.Jobs.Count);
                Assert.Equal(1, snapshot.Jobs.Count(job => job.State == ScanJobExecutionState.Completed));
                Assert.Equal(1, snapshot.Jobs.Count(job => job.State == ScanJobExecutionState.Running &&
                    job.LeaseExpiresAt > ObservedAt));
                Assert.Equal(1, snapshot.Jobs.Count(job => job.State == ScanJobExecutionState.Running &&
                    job.LeaseExpiresAt < ObservedAt));
                Assert.Equal(1, snapshot.Jobs.Count(job => job.State == ScanJobExecutionState.RetryScheduled &&
                    job.NextAttemptAt <= ObservedAt));
                Assert.Equal(1, snapshot.Jobs.Count(job => job.State == ScanJobExecutionState.RetryScheduled &&
                    job.NextAttemptAt > ObservedAt));
                Assert.Equal(1, snapshot.Jobs.Count(job => job.State == ScanJobExecutionState.Pending));
                Assert.Equal(1, snapshot.Jobs.Count(job => job.State == ScanJobExecutionState.Failed));
                Assert.Equal(1, snapshot.Jobs.Count(job => job.State == ScanJobExecutionState.Cancelled));

                // Reading after restart is observational only: no automatic
                // dispatch, lease acquisition, retry, or state mutation occurs.
                Assert.Equal(
                    ScanExecutionPhase.PausedProviderUnavailable,
                    snapshot.Phase);
            }

            using (PostgreSqlProductionFactory resumedHost =
                new(database.ConnectionString, ObservedAt))
            {
                using IServiceScope scope = resumedHost.Services.CreateScope();
                IDurableScanStore store = scope.ServiceProvider
                    .GetRequiredService<IDurableScanStore>();
                IScanRecoveryCoordinator coordinator = scope.ServiceProvider
                    .GetRequiredService<IScanRecoveryCoordinator>();
                VulgataDbContext resumedContext = scope.ServiceProvider
                    .GetRequiredService<VulgataDbContext>();

                ScanRecoveryResult resumed = await coordinator.ResumeAsync(
                    fixture.ScanExecutionId,
                    fixture.Scope,
                    CancellationToken.None);

                Assert.Equal(DurableScanStoreOutcome.Applied, resumed.Outcome);
                ScanRecoveryView view = Assert.IsType<ScanRecoveryView>(resumed.View);
                Assert.Equal(1, view.RecoveredExpiredLeaseCount);
                Assert.Equal(1, view.ReleasedDueRetryCount);
                Assert.Equal(1, view.CompletedJobCount);
                Assert.Equal(3, view.PendingJobCount);
                Assert.Equal(1, view.RunningJobCount);
                Assert.Equal(1, view.RetryScheduledJobCount);
                Assert.Equal(1, view.FailedJobCount);
                Assert.Equal(1, view.CancelledJobCount);
                Assert.Equal(ScanExecutionPhase.Prepared, view.Phase);
                Assert.True(view.StateChanged);

                // A second operator action after the restart is idempotent and
                // must not increment attempts, versions, or create a dispatch.
                long executionVersion = await resumedContext.ScanExecutions
                    .Where(value => value.Id == fixture.ScanExecutionId)
                    .Select(value => value.StateVersion)
                    .SingleAsync();
                Dictionary<Guid, (ScanJobExecutionState State, int Attempts, long Version, Guid? Document)> durableBefore =
                    await ReadJobStateAsync(resumedContext, fixture.ScanExecutionId);

                ScanRecoveryResult replay = await coordinator.ResumeAsync(
                    fixture.ScanExecutionId,
                    fixture.Scope,
                    CancellationToken.None);

                Assert.Equal(DurableScanStoreOutcome.Applied, replay.Outcome);
                Assert.True(replay.IsReplay);
                Assert.Equal(executionVersion, await resumedContext.ScanExecutions
                    .Where(value => value.Id == fixture.ScanExecutionId)
                    .Select(value => value.StateVersion)
                    .SingleAsync());
                Assert.Equal(
                    durableBefore,
                    await ReadJobStateAsync(resumedContext, fixture.ScanExecutionId));
            }

            // Two independently restarted hosts compete to claim the same
            // Pending job with the same stale state version.  Exactly one lease
            // may win; the other must report a bounded stale/conflict result.
            using PostgreSqlProductionFactory firstHost =
                new(database.ConnectionString, ObservedAt);
            using PostgreSqlProductionFactory secondHost =
                new(database.ConnectionString, ObservedAt);
            using IServiceScope firstScope = firstHost.Services.CreateScope();
            using IServiceScope secondScope = secondHost.Services.CreateScope();
            IDurableScanStore firstStore = firstScope.ServiceProvider
                .GetRequiredService<IDurableScanStore>();
            IDurableScanStore secondStore = secondScope.ServiceProvider
                .GetRequiredService<IDurableScanStore>();
            Guid pendingJobId = fixture.PendingJobId;
            var barrier = new Barrier(2);
            Task<DurableScanStoreResult> firstClaim = ClaimTogetherAsync(
                firstStore,
                fixture.Scope,
                fixture.ScanExecutionId,
                pendingJobId,
                DeterministicTestIds.Create("t028-lease", "first"),
                barrier);
            Task<DurableScanStoreResult> secondClaim = ClaimTogetherAsync(
                secondStore,
                fixture.Scope,
                fixture.ScanExecutionId,
                pendingJobId,
                DeterministicTestIds.Create("t028-lease", "second"),
                barrier);
            DurableScanStoreResult[] claims = await Task.WhenAll(firstClaim, secondClaim);

            Assert.Single(claims, result => result.Outcome == DurableScanStoreOutcome.Applied);
            Assert.Single(claims, result => result.Outcome == DurableScanStoreOutcome.Conflict);
            Assert.All(
                claims,
                result => Assert.Contains(
                    result.Outcome,
                    new[] { DurableScanStoreOutcome.Applied, DurableScanStoreOutcome.Conflict }));

            await using (VulgataDbContext verification = CreateContext(database.ConnectionString))
            {
                ScanJobExecution completed = await verification.ScanJobExecutions
                    .AsNoTracking()
                    .SingleAsync(job => job.Id == fixture.CompletedJobId);
                Assert.Equal(ScanJobExecutionState.Completed, completed.State);
                Assert.Equal(fixture.DocumentVersionId, completed.CommittedDocumentVersionId);
                Assert.Equal(1, await verification.DocumentVersions.CountAsync(
                    document => document.Id == fixture.DocumentVersionId));
                Assert.Equal(1, await verification.LogicalDocuments.CountAsync(
                    document => document.Id == fixture.LogicalDocumentId));
                Assert.Equal(
                    1,
                    await verification.ScanJobExecutions.CountAsync(job =>
                        job.CommittedDocumentVersionId == fixture.DocumentVersionId));
            }
        }
        finally
        {
            await database.DisposeAsync();
        }

        await using NpgsqlConnection administrator = new(administratorConnectionString);
        await administrator.OpenAsync();
        await using NpgsqlCommand exists = new(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            administrator);
        exists.Parameters.AddWithValue("name", ownedDatabaseName);
        Assert.False((bool)(await exists.ExecuteScalarAsync() ?? true));
    }

    private static async Task<DurableScanStoreResult> ClaimTogetherAsync(
        IDurableScanStore store,
        DurableScanAuthorizationScope scope,
        Guid scanExecutionId,
        Guid scanJobExecutionId,
        Guid leaseToken,
        Barrier barrier)
    {
        await Task.Run(() => barrier.SignalAndWait(), CancellationToken.None);
        return await store.ClaimPendingJobAsync(
            scope,
            new ScanJobLeaseCommand(
                scanExecutionId,
                scanJobExecutionId,
                ExpectedStateVersion: 0,
                leaseToken,
                ObservedAt.AddMinutes(5),
                ObservedAt.AddMinutes(-1)),
            CancellationToken.None);
    }

    private static async Task<RestartFixture> SeedMixedStateFixtureAsync(string connectionString)
    {
        await using VulgataDbContext context = CreateContext(connectionString);
        SystemEntity system = new("T028 restart system", null, null, StartedAt);
        Repository repository = system.AddRepository(
            "T028 restart repository",
            "https://example.invalid/t028.git",
            null,
            null,
            StartedAt);
        ScanRun scanRun = ScanRun.Start(repository.Id, 1_800, StartedAt);
        scanRun.BindProfiledRevision(ProfiledRevision);
        scanRun.SetCandidateCount(8);
        CodeUnit[] codeUnits = Enumerable.Range(1, 8)
            .Select(ordinal => new CodeUnit(
                scanRun.Id,
                repository.Id,
                $"src/T028-{ordinal}.java",
                SourceHash,
                SourceHash,
                1,
                10,
                $"T028_{ordinal}",
                CodeUnitType.File,
                RangeConfidence.Exact,
                "java"))
            .ToArray();
        context.Systems.Add(system);
        context.ScanRuns.Add(scanRun);
        context.CodeUnits.AddRange(codeUnits);
        await context.SaveChangesAsync();

        DurableScanAuthorizationScope scope = new("t028-owner", [system.Id]);
        ScanContextSnapshot snapshot = ScanContextSnapshot.Capture(
            scanRun,
            "global-context",
            "system-context",
            "repository-context",
            StartedAt.AddMinutes(1));
        context.ScanContextSnapshots.Add(snapshot);

        Guid executionId = DeterministicTestIds.Create("t028-execution");
        ScanExecution execution = ScanExecution.CreateNewPath(
            executionId,
            repository.Id,
            system.Id,
            scanRun.Id,
            ProfiledRevision,
            "prompt/t028",
            "document-schema/1.0",
            "{\"codeGraph\":\"1.5.0\"}",
            RuntimeHash,
            StartedAt.AddMinutes(2));
        execution.BeginRunning(0, StartedAt.AddMinutes(3));
        execution.PauseProviderUnavailable(1, "provider-unavailable", StartedAt.AddMinutes(4));
        context.ScanExecutions.Add(execution);

        Guid logicalDocumentId = DeterministicTestIds.Create("t028-logical-document");
        Guid documentVersionId = DeterministicTestIds.Create("t028-document-version");
        LogicalDocument logicalDocument = LogicalDocument.Create(
            logicalDocumentId,
            repository.Id,
            DocumentType.CodeLogic,
            "t028/committed",
            StartedAt.AddMinutes(5));
        DocumentVersion documentVersion = DocumentVersion.Create(
            documentVersionId,
            logicalDocumentId,
            repository.Id,
            system.Id,
            scanRun.Id,
            ProfiledRevision,
            1,
            "T028 committed document",
            "src/T028-6.java",
            "vulgata.knowledge-document/1.0",
            "{\"committed\":true}",
            "# T028 committed document",
            SourceHash,
            SourceHash,
            StartedAt.AddMinutes(5));
        context.LogicalDocuments.Add(logicalDocument);
        await context.SaveChangesAsync();
        // Insert the immutable Version only after its Logical Document exists,
        // then advance the current-version pointer in a separate legal phase.
        // Adding both navigation ends at once creates an EF circular insert
        // (LogicalDocument.CurrentVersionId -> DocumentVersion.LogicalDocumentId).
        context.DocumentVersions.Add(documentVersion);
        await context.SaveChangesAsync();
        logicalDocument.SetInitialCurrentVersion(documentVersion);
        await context.SaveChangesAsync();

        ScanJobExecution[] jobs = codeUnits
            .Select((unit, index) => ScanJobExecution.Create(
                executionId,
                repository.Id,
                scanRun.Id,
                unit.Id,
                index + 1,
                StartedAt.AddMinutes(6)))
            .ToArray();
        Guid expiredLease = DeterministicTestIds.Create("t028-expired-lease");
        jobs[1].AcquireLease(expiredLease, ObservedAt.AddMinutes(-5), StartedAt.AddMinutes(7));
        Guid validLease = DeterministicTestIds.Create("t028-valid-lease");
        jobs[2].AcquireLease(validLease, ObservedAt.AddMinutes(5), StartedAt.AddMinutes(7));
        Guid dueLease = DeterministicTestIds.Create("t028-due-retry-lease");
        jobs[3].AcquireLease(dueLease, ObservedAt.AddMinutes(5), StartedAt.AddMinutes(7));
        jobs[3].ScheduleRetry(
            1,
            dueLease,
            ObservedAt.AddMinutes(-1),
            ScanJobFailureCategory.ProviderTransient,
            "provider-timeout",
            StartedAt.AddMinutes(8));
        Guid futureLease = DeterministicTestIds.Create("t028-future-retry-lease");
        jobs[4].AcquireLease(futureLease, ObservedAt.AddMinutes(5), StartedAt.AddMinutes(7));
        jobs[4].ScheduleRetry(
            1,
            futureLease,
            ObservedAt.AddMinutes(5),
            ScanJobFailureCategory.ProviderTransient,
            "provider-timeout",
            StartedAt.AddMinutes(8));
        jobs[5].AcquireLease(
            DeterministicTestIds.Create("t028-completed-lease"),
            ObservedAt.AddMinutes(5),
            StartedAt.AddMinutes(7));
        jobs[5].Complete(1, jobs[5].LeaseToken!.Value, documentVersionId, StartedAt.AddMinutes(8));
        jobs[6].AcquireLease(
            DeterministicTestIds.Create("t028-failed-lease"),
            ObservedAt.AddMinutes(5),
            StartedAt.AddMinutes(7));
        jobs[6].Fail(
            1,
            jobs[6].LeaseToken,
            ScanJobFailureCategory.JobPermanent,
            "invalid-result",
            StartedAt.AddMinutes(8));
        jobs[7].Cancel(0, null, "operator-cancelled", StartedAt.AddMinutes(8));
        context.ScanJobExecutions.AddRange(jobs);
        await context.SaveChangesAsync();

        return new(
            executionId,
            scope,
            jobs[0].Id,
            jobs[5].Id,
            logicalDocumentId,
            documentVersionId);
    }

    private static async Task<Dictionary<Guid, (ScanJobExecutionState State, int Attempts, long Version, Guid? Document)>> ReadJobStateAsync(
        VulgataDbContext context,
        Guid executionId) =>
        await context.ScanJobExecutions
            .AsNoTracking()
            .Where(job => job.ScanExecutionId == executionId)
            .ToDictionaryAsync(
                job => job.Id,
                job => (job.State, job.AttemptCount, job.StateVersion, job.CommittedDocumentVersionId));

    private static async Task MigrateAsync(string connectionString)
    {
        await using VulgataDbContext context = CreateContext(connectionString);
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static VulgataDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(
                connectionString,
                options => options.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options);

    private sealed record RestartFixture(
        Guid ScanExecutionId,
        DurableScanAuthorizationScope Scope,
        Guid PendingJobId,
        Guid CompletedJobId,
        Guid LogicalDocumentId,
        Guid DocumentVersionId);

    private sealed class PostgreSqlProductionFactory(
        string connectionString,
        DateTimeOffset observedAt) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<VulgataDbContext>>();
                services.RemoveAll<IDbContextFactory<VulgataDbContext>>();
                services.RemoveAll<IConfigureOptions<DbContextOptions<ApplicationDbContext>>>();
                services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<VulgataDbContext>();

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsHistoryTable(
                            "__IdentityMigrationsHistory",
                            "identity");
                        npgsqlOptions.EnableRetryOnFailure(3);
                    }));
                services.AddDbContext<VulgataDbContext>(
                    options => options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                        npgsqlOptions.EnableRetryOnFailure(3);
                    }),
                    contextLifetime: ServiceLifetime.Scoped,
                    optionsLifetime: ServiceLifetime.Singleton);
                services.AddDbContextFactory<VulgataDbContext>(options =>
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                        npgsqlOptions.EnableRetryOnFailure(3);
                    }));

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTestClock(observedAt));
                services.RemoveAll<IScanRecoveryRevalidator>();
                services.AddSingleton<IScanRecoveryRevalidator, AcceptingRecoveryRevalidator>();
            });
        }
    }

    private sealed class AcceptingRecoveryRevalidator : IScanRecoveryRevalidator
    {
        public Task<ScanRecoveryRevalidationResult> RevalidateAsync(
            ScanRecoverySnapshot snapshot,
            DurableScanAuthorizationScope authorizationScope,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ScanRecoveryRevalidationResult(true, DurableScanReason.None));
    }
}

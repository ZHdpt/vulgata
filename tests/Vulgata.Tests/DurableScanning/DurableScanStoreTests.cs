using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.DurableScanning;

public sealed class DurableScanStoreTests
{
    private static readonly Guid _executionId =
        Guid.Parse("44000000-0000-4000-8000-000000000044");
    private static readonly DateTimeOffset _startedAt =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);
    private const string _revision = "abcdef0123456789abcdef0123456789abcdef01";
    private const string _runtimeHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task CreateContextSnapshotAsync_DuplicateDeterministicKeyIsReplayAndDoesNotDuplicateAsync()
    {
        await using SqliteConnection connection = OpenConnection();
        await using VulgataDbContext context = CreateContext(connection);
        DurableScanFixture fixture = await SeedFixtureAsync(context);
        IDurableScanStore store = new DurableScanRepository(context);
        DurableScanAuthorizationScope scope = fixture.Scope;
        ScanContextSnapshot snapshot = ScanContextSnapshot.Capture(
            fixture.ScanRun,
            "global",
            "system",
            "repository",
            _startedAt.AddMinutes(1));

        DurableScanStoreResult first = await store.CreateContextSnapshotAsync(
            scope,
            snapshot,
            CancellationToken.None);
        DurableScanStoreResult executionResult = await store.CreateScanExecutionAsync(
            scope,
            CreateExecution(fixture, _startedAt.AddMinutes(2)),
            CancellationToken.None);
        DurableScanStoreResult replay = await store.CreateContextSnapshotAsync(
            scope,
            snapshot,
            CancellationToken.None);

        Assert.Equal(DurableScanStoreOutcome.Applied, first.Outcome);
        Assert.Equal(DurableScanStoreOutcome.Applied, executionResult.Outcome);
        Assert.Equal(DurableScanStoreOutcome.Replay, replay.Outcome);
        Assert.Equal(
            1,
            await context.Set<ScanContextSnapshot>().CountAsync());
    }

    [Fact]
    public async Task InsertJobExecutionsAsync_RejectsDuplicateAndCrossRepositoryOwnershipWithoutPartialInsertAsync()
    {
        await using SqliteConnection connection = OpenConnection();
        await using VulgataDbContext context = CreateContext(connection);
        DurableScanFixture fixture = await SeedFixtureWithExecutionAsync(context);
        IDurableScanStore store = new DurableScanRepository(context);
        DurableScanAuthorizationScope scope = fixture.Scope;
        ScanJobExecution job = ScanJobExecution.Create(
            fixture.ExecutionId,
            fixture.Repository.Id,
            fixture.ScanRun.Id,
            fixture.CodeUnit.Id,
            manifestOrdinal: 1,
            _startedAt);

        DurableScanStoreResult first = await store.InsertJobExecutionsAsync(
            scope,
            fixture.ExecutionId,
            [job],
            CancellationToken.None);
        DurableScanStoreResult duplicate = await store.InsertJobExecutionsAsync(
            scope,
            fixture.ExecutionId,
            [job],
            CancellationToken.None);
        ScanJobExecution wrongOwner = ScanJobExecution.Create(
            fixture.ExecutionId,
            Guid.Parse("66000000-0000-4000-8000-000000000066"),
            fixture.ScanRun.Id,
            Guid.Parse("77000000-0000-4000-8000-000000000077"),
            manifestOrdinal: 2,
            _startedAt);
        DurableScanStoreResult crossOwner = await store.InsertJobExecutionsAsync(
            scope,
            fixture.ExecutionId,
            [wrongOwner],
            CancellationToken.None);

        Assert.Equal(DurableScanStoreOutcome.Applied, first.Outcome);
        Assert.Contains(
            duplicate.Outcome,
            new[] { DurableScanStoreOutcome.Replay, DurableScanStoreOutcome.Conflict });
        Assert.Equal(DurableScanStoreOutcome.Rejected, crossOwner.Outcome);
        Assert.Equal(1, await context.Set<ScanJobExecution>().CountAsync());
    }

    [Fact]
    public async Task InsertJobExecutionsAsync_StrictSubsetReplayIsRejectedWithoutMutationAsync()
    {
        await using SqliteConnection connection = OpenConnection();
        await using VulgataDbContext context = CreateContext(connection);
        DurableScanFixture fixture = await SeedFixtureWithExecutionAsync(context);
        IReadOnlyList<CodeUnit> additionalCodeUnits =
            await AddAdditionalCodeUnitsAsync(context, fixture, 2);
        IDurableScanStore store = new DurableScanRepository(context);

        ScanJobExecution[] manifest = CreateManifest(fixture, additionalCodeUnits);
        Assert.Equal(
            DurableScanStoreOutcome.Applied,
            (await store.InsertJobExecutionsAsync(
                fixture.Scope,
                fixture.ExecutionId,
                manifest,
                CancellationToken.None)).Outcome);
        ScanRecoveryJobSnapshot[] before = await ReadJobsAsync(store, fixture);

        DurableScanStoreResult replay = await store.InsertJobExecutionsAsync(
            fixture.Scope,
            fixture.ExecutionId,
            manifest.Take(2).ToArray(),
            CancellationToken.None);
        ScanRecoveryJobSnapshot[] after = await ReadJobsAsync(store, fixture);

        Assert.NotEqual(DurableScanStoreOutcome.Replay, replay.Outcome);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task InsertJobExecutionsAsync_StrictSupersetReplayIsRejectedWithoutMutationAsync()
    {
        await using SqliteConnection connection = OpenConnection();
        await using VulgataDbContext context = CreateContext(connection);
        DurableScanFixture fixture = await SeedFixtureWithExecutionAsync(context);
        IReadOnlyList<CodeUnit> additionalCodeUnits =
            await AddAdditionalCodeUnitsAsync(context, fixture, 3);
        IDurableScanStore store = new DurableScanRepository(context);

        ScanJobExecution[] manifest = CreateManifest(
            fixture,
            additionalCodeUnits.Take(2).ToArray());
        Assert.Equal(
            DurableScanStoreOutcome.Applied,
            (await store.InsertJobExecutionsAsync(
                fixture.Scope,
                fixture.ExecutionId,
                manifest,
                CancellationToken.None)).Outcome);
        ScanRecoveryJobSnapshot[] before = await ReadJobsAsync(store, fixture);

        ScanJobExecution extra = CreateJob(
            fixture,
            additionalCodeUnits[2],
            manifestOrdinal: 4);
        DurableScanStoreResult replay = await store.InsertJobExecutionsAsync(
            fixture.Scope,
            fixture.ExecutionId,
            [.. manifest, extra],
            CancellationToken.None);
        ScanRecoveryJobSnapshot[] after = await ReadJobsAsync(store, fixture);

        Assert.NotEqual(DurableScanStoreOutcome.Replay, replay.Outcome);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task InsertJobExecutionsAsync_ReorderedReplayIsRejectedWithoutMutationAsync()
    {
        await using SqliteConnection connection = OpenConnection();
        await using VulgataDbContext context = CreateContext(connection);
        DurableScanFixture fixture = await SeedFixtureWithExecutionAsync(context);
        IReadOnlyList<CodeUnit> additionalCodeUnits =
            await AddAdditionalCodeUnitsAsync(context, fixture, 2);
        IDurableScanStore store = new DurableScanRepository(context);

        ScanJobExecution[] manifest = CreateManifest(fixture, additionalCodeUnits);
        Assert.Equal(
            DurableScanStoreOutcome.Applied,
            (await store.InsertJobExecutionsAsync(
                fixture.Scope,
                fixture.ExecutionId,
                manifest,
                CancellationToken.None)).Outcome);
        ScanRecoveryJobSnapshot[] before = await ReadJobsAsync(store, fixture);

        DurableScanStoreResult replay = await store.InsertJobExecutionsAsync(
            fixture.Scope,
            fixture.ExecutionId,
            [manifest[1], manifest[0], manifest[2]],
            CancellationToken.None);
        ScanRecoveryJobSnapshot[] after = await ReadJobsAsync(store, fixture);

        Assert.NotEqual(DurableScanStoreOutcome.Replay, replay.Outcome);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task InsertJobExecutionsAsync_StateMutatedManifestIsRejectedWithoutMutationAsync()
    {
        await using SqliteConnection connection = OpenConnection();
        await using VulgataDbContext context = CreateContext(connection);
        DurableScanFixture fixture = await SeedFixtureWithExecutionAsync(context);
        IReadOnlyList<CodeUnit> additionalCodeUnits =
            await AddAdditionalCodeUnitsAsync(context, fixture, 2);
        IDurableScanStore store = new DurableScanRepository(context);

        ScanJobExecution[] manifest = CreateManifest(fixture, additionalCodeUnits);
        manifest[0].AcquireLease(
            Guid.Parse("99000000-0000-4000-8000-000000000099"),
            _startedAt.AddMinutes(5),
            _startedAt.AddMinutes(1));

        DurableScanStoreResult result = await store.InsertJobExecutionsAsync(
            fixture.Scope,
            fixture.ExecutionId,
            manifest,
            CancellationToken.None);

        Assert.Equal(DurableScanStoreOutcome.Rejected, result.Outcome);
        Assert.Empty(await ReadJobsAsync(store, fixture));
    }

    [Fact]
    public async Task JobTransitions_PersistLeaseRetryAndCompletionShapesAndProtectTerminalRowsAsync()
    {
        await using SqliteConnection connection = OpenConnection();
        await using VulgataDbContext context = CreateContext(connection);
        DurableScanFixture fixture = await SeedFixtureWithExecutionAsync(context);
        IDurableScanStore store = new DurableScanRepository(context);
        DurableScanAuthorizationScope scope = fixture.Scope;
        ScanJobExecution job = ScanJobExecution.Create(
            fixture.ExecutionId,
            fixture.Repository.Id,
            fixture.ScanRun.Id,
            fixture.CodeUnit.Id,
            manifestOrdinal: 1,
            _startedAt);

        await store.InsertJobExecutionsAsync(
            scope,
            fixture.ExecutionId,
            [job],
            CancellationToken.None);
        Guid leaseToken = Guid.Parse("88000000-0000-4000-8000-000000000088");
        DurableScanStoreResult claimed = await store.ClaimPendingJobAsync(
            scope,
            new ScanJobLeaseCommand(
                fixture.ExecutionId,
                job.Id,
                ExpectedStateVersion: 0,
                leaseToken,
                _startedAt.AddMinutes(5),
                _startedAt.AddMinutes(1)),
            CancellationToken.None);
        DurableScanStoreResult retried = await store.TransitionRunningJobAsync(
            scope,
            new RunningScanJobTransitionCommand(
                fixture.ExecutionId,
                job.Id,
                ExpectedStateVersion: 1,
                leaseToken,
                ScanJobExecutionState.RetryScheduled,
                _startedAt.AddMinutes(10),
                ScanJobFailureCategory.ProviderTransient,
                "provider-timeout",
                _startedAt.AddMinutes(2)),
            CancellationToken.None);

        Assert.Equal(DurableScanStoreOutcome.Applied, claimed.Outcome);
        Assert.Equal(DurableScanStoreOutcome.Applied, retried.Outcome);
        ScanJobExecution persisted = await context.Set<ScanJobExecution>().SingleAsync();
        Assert.Equal(ScanJobExecutionState.RetryScheduled, persisted.State);
        Assert.Null(persisted.LeaseToken);
        Assert.Equal(ScanJobFailureCategory.ProviderTransient, persisted.FailureCategory);
        Assert.Equal("provider-timeout", persisted.FailureCode);

        DurableScanStoreResult stale = await store.TransitionRunningJobAsync(
            scope,
            new RunningScanJobTransitionCommand(
                fixture.ExecutionId,
                job.Id,
                ExpectedStateVersion: 1,
                leaseToken,
                ScanJobExecutionState.Completed,
                null,
                null,
                null,
                _startedAt.AddMinutes(3)),
            CancellationToken.None);
        Assert.Equal(DurableScanStoreOutcome.Conflict, stale.Outcome);
        Assert.Equal(ScanJobExecutionState.RetryScheduled, persisted.State);
    }

    [Fact]
    public void Model_DurableKeysChecksAndRestrictiveDeletesAreDeclared()
    {
        using SqliteConnection connection = OpenConnection();
        using VulgataDbContext context = CreateContext(connection);
        IModel model = context.GetService<IDesignTimeModel>().Model;
        IEntityType snapshot = RequireEntity<ScanContextSnapshot>(model);
        IEntityType job = RequireEntity<ScanJobExecution>(model);
        IEntityType coordination = RequireEntity<TwoRepositoryScanCoordination>(model);

        Assert.Contains(
            snapshot.GetKeys(),
            key => Names(key.Properties) == "ScanRunId,RepositoryId");
        Assert.Contains(
            job.GetIndexes(),
            index => index.IsUnique && Names(index.Properties) == "ScanExecutionId,FileCodeUnitId");
        Assert.Contains(
            job.GetIndexes(),
            index => index.IsUnique && Names(index.Properties) == "ScanExecutionId,ManifestOrdinal");
        Assert.Contains(
            job.GetIndexes(),
            index => index.IsUnique &&
                Names(index.Properties) == nameof(ScanJobExecution.CommittedDocumentVersionId) &&
                index.GetFilter() is not null);
        Assert.NotEmpty(job.GetCheckConstraints());
        Assert.NotEmpty(coordination.GetForeignKeys());

        foreach (IEntityType entity in new[] { snapshot, job, coordination })
        {
            Assert.All(
                entity.GetForeignKeys(),
                foreignKey => Assert.Contains(
                    foreignKey.DeleteBehavior,
                    new[] { DeleteBehavior.Restrict, DeleteBehavior.NoAction }));
        }
    }

    [Fact]
    public async Task CreateContextSnapshotAsync_ExecutionFirstRejectsLateCaptureWithoutPartialSnapshotAsync()
    {
        await using SqliteConnection connection = OpenConnection();
        await using VulgataDbContext context = CreateContext(connection);
        DurableScanFixture fixture = await SeedFixtureAsync(context);
        IDurableScanStore store = new DurableScanRepository(context);
        DurableScanAuthorizationScope scope = fixture.Scope;
        ScanExecution execution = CreateExecution(fixture, _startedAt.AddMinutes(2));

        DurableScanStoreResult executionResult = await store.CreateScanExecutionAsync(
            scope,
            execution,
            CancellationToken.None);
        ScanContextSnapshot lateSnapshot = ScanContextSnapshot.Capture(
            fixture.ScanRun,
            "global",
            "system",
            "repository",
            _startedAt.AddMinutes(3));
        DurableScanStoreResult snapshotResult = await store.CreateContextSnapshotAsync(
            scope,
            lateSnapshot,
            CancellationToken.None);

        Assert.Equal(DurableScanStoreOutcome.Applied, executionResult.Outcome);
        Assert.Equal(DurableScanStoreOutcome.Rejected, snapshotResult.Outcome);
        Assert.Equal(0, await context.Set<ScanContextSnapshot>().CountAsync());
        Assert.Equal(1, await context.Set<ScanExecution>().CountAsync());
    }

    [Fact]
    public async Task CreateScanExecutionAsync_SnapshotFirstRejectsEarlyExecutionWithoutPartialExecutionAsync()
    {
        await using SqliteConnection connection = OpenConnection();
        await using VulgataDbContext context = CreateContext(connection);
        DurableScanFixture fixture = await SeedFixtureAsync(context);
        IDurableScanStore store = new DurableScanRepository(context);
        DurableScanAuthorizationScope scope = fixture.Scope;
        ScanContextSnapshot snapshot = ScanContextSnapshot.Capture(
            fixture.ScanRun,
            "global",
            "system",
            "repository",
            _startedAt.AddMinutes(3));
        DurableScanStoreResult snapshotResult = await store.CreateContextSnapshotAsync(
            scope,
            snapshot,
            CancellationToken.None);
        ScanExecution earlyExecution = CreateExecution(fixture, _startedAt.AddMinutes(2));
        DurableScanStoreResult executionResult = await store.CreateScanExecutionAsync(
            scope,
            earlyExecution,
            CancellationToken.None);

        Assert.Equal(DurableScanStoreOutcome.Applied, snapshotResult.Outcome);
        Assert.Equal(DurableScanStoreOutcome.Rejected, executionResult.Outcome);
        Assert.Equal(1, await context.Set<ScanContextSnapshot>().CountAsync());
        Assert.Equal(0, await context.Set<ScanExecution>().CountAsync());
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static VulgataDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new VulgataDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static async Task<DurableScanFixture> SeedFixtureAsync(VulgataDbContext context)
    {
        var system = new SystemEntity("T020 system", null, null, _startedAt);
        Repository repository = system.AddRepository(
            "T020 repository",
            "https://example.invalid/t020.git",
            null,
            null,
            _startedAt);
        ScanRun scanRun = ScanRun.Start(repository.Id, 1_800, _startedAt);
        scanRun.BindProfiledRevision(_revision);
        CodeUnit codeUnit = new(
            scanRun.Id,
            repository.Id,
            "src/T020.java",
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            1,
            10,
            "T020",
            CodeUnitType.File,
            RangeConfidence.Exact,
            "java");
        context.Systems.Add(system);
        context.ScanRuns.Add(scanRun);
        context.CodeUnits.Add(codeUnit);
        await context.SaveChangesAsync();
        return new(
            system,
            repository,
            scanRun,
            codeUnit,
            _executionId,
            new DurableScanAuthorizationScope("story-12-1-test-actor", [system.Id]));
    }

    private static async Task<DurableScanFixture> SeedFixtureWithExecutionAsync(
        VulgataDbContext context)
    {
        DurableScanFixture fixture = await SeedFixtureAsync(context);
        IDurableScanStore store = new DurableScanRepository(context);
        ScanContextSnapshot snapshot = ScanContextSnapshot.Capture(
            fixture.ScanRun,
            "global",
            "system",
            "repository",
            _startedAt.AddMinutes(1));
        Assert.Equal(
            DurableScanStoreOutcome.Applied,
            (await store.CreateContextSnapshotAsync(
                fixture.Scope,
                snapshot,
                CancellationToken.None)).Outcome);
        ScanExecution execution = CreateExecution(fixture, _startedAt.AddMinutes(2));
        Assert.Equal(
            DurableScanStoreOutcome.Applied,
            (await store.CreateScanExecutionAsync(
                fixture.Scope,
                execution,
                CancellationToken.None)).Outcome);
        return fixture;
    }

    private static ScanExecution CreateExecution(
        DurableScanFixture fixture,
        DateTimeOffset startedAt) =>
        ScanExecution.CreateNewPath(
            _executionId,
            fixture.Repository.Id,
            fixture.System.Id,
            fixture.ScanRun.Id,
            _revision,
            "prompt/1.0",
            "document-schema/1.0",
            "{\"codeGraph\":\"1.5.0\"}",
            _runtimeHash,
            startedAt);

    private static async Task<IReadOnlyList<CodeUnit>> AddAdditionalCodeUnitsAsync(
        VulgataDbContext context,
        DurableScanFixture fixture,
        int count)
    {
        var codeUnits = Enumerable.Range(1, count)
            .Select(index =>
            {
                char hashCharacter = "def"[(index - 1) % 3];
                string hash = new(hashCharacter, 64);
                return new CodeUnit(
                    fixture.ScanRun.Id,
                    fixture.Repository.Id,
                    $"src/T020-{index}.java",
                    hash,
                    hash,
                    index * 10,
                    index * 10 + 9,
                    $"T020-{index}",
                    CodeUnitType.File,
                    RangeConfidence.Exact,
                    "java");
            })
            .ToArray();
        context.CodeUnits.AddRange(codeUnits);
        await context.SaveChangesAsync();
        return codeUnits;
    }

    private static ScanJobExecution[] CreateManifest(
        DurableScanFixture fixture,
        IReadOnlyList<CodeUnit> additionalCodeUnits)
    {
        var jobs = new ScanJobExecution[additionalCodeUnits.Count + 1];
        jobs[0] = CreateJob(fixture, fixture.CodeUnit, manifestOrdinal: 1);
        for (int index = 0; index < additionalCodeUnits.Count; index++)
        {
            jobs[index + 1] = CreateJob(
                fixture,
                additionalCodeUnits[index],
                manifestOrdinal: index + 2);
        }

        return jobs;
    }

    private static ScanJobExecution CreateJob(
        DurableScanFixture fixture,
        CodeUnit codeUnit,
        int manifestOrdinal) =>
        ScanJobExecution.Create(
            fixture.ExecutionId,
            fixture.Repository.Id,
            fixture.ScanRun.Id,
            codeUnit.Id,
            manifestOrdinal,
            _startedAt);

    private static async Task<ScanRecoveryJobSnapshot[]> ReadJobsAsync(
        IDurableScanStore store,
        DurableScanFixture fixture)
    {
        ScanRecoverySnapshotResult result = await store.ReadRecoverySnapshotAsync(
            fixture.Scope,
            fixture.ExecutionId,
            CancellationToken.None);
        Assert.Equal(DurableScanStoreOutcome.Applied, result.Outcome);
        return result.Snapshot!.Jobs.ToArray();
    }

    private static IEntityType RequireEntity<T>(IModel model) =>
        Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(typeof(T)));

    private static string Names(IEnumerable<IProperty> properties) =>
        string.Join(',', properties.Select(property => property.Name));

    private sealed record DurableScanFixture(
        SystemEntity System,
        Repository Repository,
        ScanRun ScanRun,
        CodeUnit CodeUnit,
        Guid ExecutionId,
        DurableScanAuthorizationScope Scope);
}

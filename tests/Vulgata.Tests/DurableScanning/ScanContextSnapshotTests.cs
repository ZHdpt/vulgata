using Vulgata.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.DurableScanning;

public sealed class ScanContextSnapshotTests
{
    private static readonly Guid _repositoryId =
        Guid.Parse("bbbbbbbb-cccc-4ddd-8eee-ffffffffffff");

    private static readonly DateTimeOffset _scanStartedAt =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    private const string _profiledRevision =
        "0123456789abcdef0123456789abcdef01234567";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Capture_ValidContext_ProducesCanonicalImmutableBindingAndRejectsCaptureBeforeScanRunStart()
    {
        ScanRun scanRun = ScanRun.Start(_repositoryId, 1_800, _scanStartedAt);
        scanRun.BindProfiledRevision(_profiledRevision);
        DateTimeOffset capturedAt = _scanStartedAt.AddMinutes(2);

        ScanContextSnapshot snapshot = ScanContextSnapshot.Capture(
            scanRun,
            globalContext: "  global guidance  ",
            systemContext: "system guidance\n",
            repositoryContext: "\trepository guidance",
            capturedAt: capturedAt);
        ScanContextSnapshot repeat = ScanContextSnapshot.Capture(
            scanRun,
            globalContext: "global guidance",
            systemContext: "system guidance",
            repositoryContext: "repository guidance",
            capturedAt: capturedAt);

        Assert.Equal(snapshot.Id, repeat.Id);
        Assert.Equal(snapshot.ContentHash, repeat.ContentHash);
        Assert.Equal("scan-context-snapshot/1.0", snapshot.SchemaIdentity);
        Assert.Equal(_repositoryId, snapshot.RepositoryId);
        Assert.Equal(scanRun.Id, snapshot.ScanRunId);
        Assert.Equal(_profiledRevision, snapshot.ProfiledRevision);
        Assert.Equal("global guidance", snapshot.GlobalContext);
        Assert.Equal("system guidance", snapshot.SystemContext);
        Assert.Equal("repository guidance", snapshot.RepositoryContext);
        Assert.Equal(capturedAt, snapshot.CapturedAt);
        Assert.Matches("^[0-9a-f]{64}$", snapshot.ContentHash);

        Assert.Throws<ArgumentOutOfRangeException>(() => ScanContextSnapshot.Capture(
            scanRun,
            globalContext: "global guidance",
            systemContext: "system guidance",
            repositoryContext: "repository guidance",
            capturedAt: _scanStartedAt.AddTicks(-1)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PersistSnapshotFirst_ValidTemporalBinding_CommitsBothRows()
    {
        await using PersistenceScenario scenario = await PersistenceScenario.CreateAsync();
        ScanContextSnapshot snapshot = scenario.CreateSnapshot(_scanStartedAt.AddMinutes(2));
        ScanExecution execution = scenario.CreateExecution(_scanStartedAt.AddMinutes(3));

        await scenario.PersistSnapshotFirstAsync(snapshot, execution);

        Assert.Equal(
            snapshot.Id,
            await scenario.Db.ScanContextSnapshots.AsNoTracking()
                .Select(value => value.Id)
                .SingleAsync());
        Assert.Equal(
            execution.Id,
            await scenario.Db.ScanExecutions.AsNoTracking()
                .Select(value => value.Id)
                .SingleAsync());
        Assert.InRange(
            snapshot.CapturedAt,
            scenario.ScanRun.StartedAt,
            execution.StartedAt);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PersistExecutionFirst_ValidTemporalBinding_CommitsBothRows()
    {
        await using PersistenceScenario scenario = await PersistenceScenario.CreateAsync();
        ScanContextSnapshot snapshot = scenario.CreateSnapshot(_scanStartedAt.AddMinutes(2));
        ScanExecution execution = scenario.CreateExecution(_scanStartedAt.AddMinutes(3));

        await scenario.PersistExecutionFirstAsync(execution, snapshot);

        Assert.Equal(
            snapshot.Id,
            await scenario.Db.ScanContextSnapshots.AsNoTracking()
                .Select(value => value.Id)
                .SingleAsync());
        Assert.Equal(
            execution.Id,
            await scenario.Db.ScanExecutions.AsNoTracking()
                .Select(value => value.Id)
                .SingleAsync());
        Assert.InRange(
            snapshot.CapturedAt,
            scenario.ScanRun.StartedAt,
            execution.StartedAt);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PersistSnapshotFirst_ExecutionStartsBeforeCapture_RollsBackBothRows()
    {
        await using PersistenceScenario scenario = await PersistenceScenario.CreateAsync();
        ScanContextSnapshot snapshot = scenario.CreateSnapshot(_scanStartedAt.AddMinutes(2));
        ScanExecution execution = scenario.CreateExecution(_scanStartedAt.AddMinutes(1));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            scenario.PersistSnapshotFirstAsync(snapshot, execution));

        await scenario.AssertNoDurableRowsAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PersistExecutionFirst_CaptureAfterExecutionStart_RollsBackBothRows()
    {
        await using PersistenceScenario scenario = await PersistenceScenario.CreateAsync();
        ScanContextSnapshot snapshot = scenario.CreateSnapshot(_scanStartedAt.AddMinutes(2));
        ScanExecution execution = scenario.CreateExecution(_scanStartedAt.AddMinutes(1));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            scenario.PersistExecutionFirstAsync(execution, snapshot));

        await scenario.AssertNoDurableRowsAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CaptureBeforeScanRunStart_DoesNotEnterPersistence()
    {
        await using PersistenceScenario scenario = await PersistenceScenario.CreateAsync();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scenario.CreateSnapshot(_scanStartedAt.AddTicks(-1)));

        await scenario.AssertNoDurableRowsAsync();
    }

    private sealed class PersistenceScenario : IAsyncDisposable
    {
        private PersistenceScenario(
            SqliteConnection connection,
            VulgataDbContext db,
            Vulgata.Core.Entities.System system,
            Repository repository,
            ScanRun scanRun)
        {
            Connection = connection;
            Db = db;
            System = system;
            Repository = repository;
            ScanRun = scanRun;
        }

        private SqliteConnection Connection { get; }

        internal VulgataDbContext Db { get; }

        internal Vulgata.Core.Entities.System System { get; }

        internal Repository Repository { get; }

        internal ScanRun ScanRun { get; }

        internal static async Task<PersistenceScenario> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new VulgataDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var system = new Vulgata.Core.Entities.System(
                "durable-snapshot-system",
                null,
                null,
                _scanStartedAt);
            Repository repository = system.AddRepository(
                "durable-snapshot-test",
                "https://example.test/durable-snapshot-test.git",
                null,
                null,
                _scanStartedAt);
            ScanRun scanRun = ScanRun.Start(
                repository.Id,
                providerProcessTimeoutSeconds: 1_800,
                _scanStartedAt);
            scanRun.BindProfiledRevision(_profiledRevision);
            db.Systems.Add(system);
            db.Repositories.Add(repository);
            db.ScanRuns.Add(scanRun);
            await db.SaveChangesAsync();
            return new PersistenceScenario(connection, db, system, repository, scanRun);
        }

        internal ScanContextSnapshot CreateSnapshot(DateTimeOffset capturedAt) =>
            ScanContextSnapshot.Capture(
                ScanRun,
                "global guidance",
                "system guidance",
                "repository guidance",
                capturedAt);

        internal ScanExecution CreateExecution(DateTimeOffset startedAt) =>
            ScanExecution.CreateNewPath(
                DeterministicTestIds.Create("scan-execution", ScanRun.Id, startedAt),
                Repository.Id,
                System.Id,
                ScanRun.Id,
                _profiledRevision,
                "prompt/1.0",
                "document/1.0",
                "{\"runtime\":\"test\"}",
                new string('a', 64),
                startedAt);

        internal async Task PersistSnapshotFirstAsync(
            ScanContextSnapshot snapshot,
            ScanExecution execution)
        {
            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
                await Db.Database.BeginTransactionAsync();
            try
            {
                Db.ScanContextSnapshots.Add(snapshot);
                await Db.SaveChangesAsync();
                Db.ScanExecutions.Add(execution);
                await Db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        internal async Task PersistExecutionFirstAsync(
            ScanExecution execution,
            ScanContextSnapshot snapshot)
        {
            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
                await Db.Database.BeginTransactionAsync();
            try
            {
                Db.ScanExecutions.Add(execution);
                await Db.SaveChangesAsync();
                Db.ScanContextSnapshots.Add(snapshot);
                await Db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        internal async Task AssertNoDurableRowsAsync()
        {
            Db.ChangeTracker.Clear();
            Assert.Empty(await Db.ScanContextSnapshots.AsNoTracking().ToArrayAsync());
            Assert.Empty(await Db.ScanExecutions.AsNoTracking().ToArrayAsync());
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}

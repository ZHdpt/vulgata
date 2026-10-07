using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Runner-owned PostgreSQL only; actual Business migrations/writer with the actual Scan F1 adapter.</summary>
public sealed class OctoberScanAcceptancePostgreSqlTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accept_ActualPostgreSqlWriter_AtomicOutcomeReplaysExactHandlesAsync(bool zeroUnit)
    {
        await using var owned = await OwnedDatabase.CreateAsync();
        await using var harness = OctoberScanAcceptanceHarness.WithFixture(owned.Fixture, zeroUnit);
        Assert.NotNull(await harness.Attempt.ExecuteAndSealAsync());
        var accepted = await harness.Attempt.AcceptAsync(harness.Writer());
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var replay = await harness.Attempt.ReconcileAsync(harness.Writer());
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(accepted.AcceptedFile!.ManifestId, replay.AcceptedFile!.ManifestId);
        Assert.Equal(accepted.AcceptedFile.Documents.ToArray(), replay.AcceptedFile.Documents.ToArray());
        Assert.Equal(zeroUnit ? 0 : 1, replay.AcceptedFile.Documents.Length);
        Assert.Equal(1, harness.WorkerCalls);
        await using var context = owned.Fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync());
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
        Assert.Equal(zeroUnit ? 0 : 1, await context.DocumentVersions.CountAsync());
        Assert.Empty(await context.KnowledgePublications.ToListAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task Accept_ServerCommitStillPending_FenceAndAbsentLookupCannotClaimNoncommitAsync()
    {
        await using var owned = await OwnedDatabase.CreateAsync();
        await using var harness = OctoberScanAcceptanceHarness.WithFixture(owned.Fixture);
        Assert.NotNull(await harness.Attempt.ExecuteAndSealAsync());
        await using var observer = await owned.Database.OpenConnectionAsync();
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(763219, 1)", observer)) await acquire.ExecuteNonQueryAsync();
        await using (var context = owned.Fixture.Factory.CreateDbContext())
        {
            await context.Database.ExecuteSqlRawAsync("CREATE FUNCTION october.hold_scan_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(763219, 1); RETURN NEW; END $$;");
            await context.Database.ExecuteSqlRawAsync("CREATE CONSTRAINT TRIGGER hold_scan_commit AFTER INSERT ON october.\"KnowledgeOperations\" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION october.hold_scan_commit();");
        }
        var pending = harness.Attempt.AcceptAsync(harness.Writer());
        try
        {
            await WaitForCommitAsync(observer, pending);
            Assert.True(harness.Attempt.HasSubmitted);
            Assert.False(harness.Attempt.IsSettledForFreeze);
            await harness.Attempt.FenceAsync().WaitAsync(TimeSpan.FromSeconds(3));
            var lookup = await harness.Attempt.ReconcileAsync(harness.Writer()).WaitAsync(TimeSpan.FromSeconds(8));
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, lookup.Status);
            Assert.Null(lookup.AcceptedFile);
            Assert.False(harness.Attempt.IsSettledForFreeze);
            Assert.NotEqual(CommitSettlement.ConfirmedNotCommitted, harness.Attempt.Settlement);
            Assert.Equal(1, harness.WorkerCalls);
        }
        finally
        {
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(763219, 1)", observer);
            await release.ExecuteNonQueryAsync();
            await pending.WaitAsync(TimeSpan.FromSeconds(15));
        }
        var final = await harness.Attempt.ReconcileAsync(harness.Writer());
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, final.Status);
        Assert.NotNull(final.AcceptedFile);
        Assert.Equal(CommitSettlement.Committed, harness.Attempt.Settlement);
        harness.Authority = DisclosureStatus.Denied;
        var denied = await harness.Attempt.ReconcileAsync(harness.Writer());
        Assert.Equal(KnowledgeWriteStatus.Denied, denied.Status);
        Assert.Null(denied.AcceptedFile);
    }

    private static async Task WaitForCommitAsync(NpgsqlConnection observer, Task<KnowledgeWriteResult> pending)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (pending.IsCompleted)
                Assert.Fail("The actual acceptance completed before reaching the controlled PostgreSQL commit barrier: " + (await pending).Status);
            await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype='advisory' AND NOT granted AND classid=763219 AND objid=1 AND database=(SELECT oid FROM pg_database WHERE datname=current_database()))", observer);
            if (await query.ExecuteScalarAsync() is true) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("Owned PostgreSQL did not reach the controlled commit barrier.");
    }
    private sealed class OwnedDatabase(DisposablePostgreSqlDatabase database, OctoberKnowledgeAcceptanceFixture fixture) : IAsyncDisposable
    {
        internal DisposablePostgreSqlDatabase Database => database;
        internal OctoberKnowledgeAcceptanceFixture Fixture => fixture;
        internal static async Task<OwnedDatabase> CreateAsync()
        {
            var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
            try
            {
                TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
                var options = new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString, postgres => postgres.CommandTimeout(30)).Options;
                await using (var context = new OctoberDbContext(options)) await context.Database.MigrateAsync();
                return new(database, await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options));
            }
            catch { await database.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync() { await fixture.DisposeAsync(); await database.DisposeAsync(); }
    }
}

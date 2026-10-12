using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>One launcher-owned PostgreSQL resource; each case creates and removes its own database. No retained runtime target is used.</summary>
public sealed class OctoberBusinessRecoveryPostgreSqlTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task RecoverAsync_ExclusiveBatchLock_WaitsForEarlierSemanticShareThenFencesOldOwnerAsync()
    {
        await using var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        var options = await MigrateAsync(database);
        await using var fixture = await BusinessRecoveryFixture.CreateAsync(await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options));
        string original = await fixture.SemanticSnapshotAsync();
        await using var holder = await database.OpenConnectionAsync();
        await using var transaction = await holder.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand("SELECT \"BatchId\" FROM october.\"OctoberBatches\" WHERE \"BatchId\"=@batch FOR SHARE", holder, transaction))
        {
            hold.Parameters.AddWithValue("batch", fixture.Begin.Publication.Knowledge.BatchId);
            await hold.ExecuteNonQueryAsync();
        }
        Task<KnowledgeWriteResult>? pending = null;
        try
        {
            Assert.Equal(CommitEligibilityStatus.Eligible, await fixture.Store.CheckActiveAsync(fixture.Begin, fixture.Admission));
            pending = fixture.Recovery.RecoverAsync(fixture.Request);
            await WaitForRecoveryBlockedByAsync(database, holder.ProcessID);
            Assert.False(pending.IsCompleted);
            await fixture.AssertNoAbandonmentAsync();
            Assert.Equal(original, await fixture.SemanticSnapshotAsync());
        }
        finally
        {
            await transaction.RollbackAsync();
            if (pending is not null) await pending.WaitAsync(TimeSpan.FromSeconds(20));
        }
        Assert.NotNull(pending);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await pending).Status);
        Assert.Equal(CommitEligibilityStatus.StaleOrFenced, await fixture.Store.CheckActiveAsync(fixture.Begin, fixture.Admission));
        Assert.Equal(original, await fixture.SemanticSnapshotAsync());
        var fresh = await fixture.NewStore().BeginAsync(BusinessRecoveryFixture.NewBegin(fixture.Begin.Publication, fixture.Begin.SubjectId));
        Assert.Equal(KnowledgeWriteStatus.Committed, fresh.Status);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoverAsync_ConcurrentReplayOrConflict_AppendsOneReceiptAndPreservesAllPriorHistoryAsync(bool conflicting)
    {
        await using var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        var options = await MigrateAsync(database);
        await using var fixture = await BusinessRecoveryFixture.CreateAsync(await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options));
        string original = await fixture.SemanticSnapshotAsync();
        var second = conflicting ? BusinessRecoveryFixture.Seal(fixture.Request with { EvidenceDigest = new string('e', 64) }) : fixture.Request;
        var results = await Task.WhenAll(fixture.Recovery.RecoverAsync(fixture.Request), fixture.Recovery.RecoverAsync(second));
        Assert.Single(results.Where(result => result.Status == KnowledgeWriteStatus.Committed));
        Assert.Single(results.Where(result => result.Status == (conflicting ? KnowledgeWriteStatus.Conflict : KnowledgeWriteStatus.AlreadyCommitted)));
        Assert.Equal(original, await fixture.SemanticSnapshotAsync());
        await using var context = fixture.Database.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync(row => row.Kind == OperationKind.AbandonBusinessFromCl));
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync(row => row.Kind == OperationKind.BeginBusinessFromCl));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static async Task<DbContextOptions<OctoberDbContext>> MigrateAsync(DisposablePostgreSqlDatabase database)
    {
        TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
        var options = new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString,
            postgres => postgres.MigrationsHistoryTable("__OctoberMigrationsHistory", "october").CommandTimeout(30)).Options;
        await using var context = new OctoberDbContext(options);
        await context.Database.MigrateAsync();
        return options;
    }

    private static async Task WaitForRecoveryBlockedByAsync(DisposablePostgreSqlDatabase database, int blocker)
    {
        await using var observer = await database.OpenConnectionAsync();
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            await using var command = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE datname=current_database() AND @blocker=ANY(pg_blocking_pids(pid))
                AND query ILIKE '%FOR UPDATE%'
                """, observer);
            command.Parameters.AddWithValue("blocker", blocker);
            if ((long)(await command.ExecuteScalarAsync())! > 0) return;
            await Task.Delay(20);
        }
        Assert.Fail("Recovery never reached an exclusive batch lock blocked by the owned semantic FOR SHARE transaction.");
    }
}

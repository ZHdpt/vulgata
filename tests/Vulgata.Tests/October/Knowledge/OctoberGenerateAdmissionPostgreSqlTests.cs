using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Launcher-owned PostgreSQL admission qualification. Host authority/guard remain controlled; no Scan dispatch is performed.</summary>
public sealed class OctoberGenerateAdmissionPostgreSqlTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartFreshExecutionAsync_ConcurrentSameOrDifferentRuns_StoresOneBatchClaimAndExactReplayAsync(bool identical)
    {
        await using var owned = await OwnedAdmission.CreateAsync();
        var first = owned.Request;
        var second = identical ? first : AdmissionProtocol.Seal(first with { RunId = Guid.NewGuid(), AdmissionOperationId = Guid.NewGuid() });
        var results = await Task.WhenAll(owned.Store.StartFreshExecutionAsync(first), owned.Store.StartFreshExecutionAsync(second));
        Assert.Single(results.Where(x => x.Status == KnowledgeWriteStatus.Committed));
        Assert.Single(results.Where(x => x.Status == (identical ? KnowledgeWriteStatus.AlreadyCommitted : KnowledgeWriteStatus.Conflict)));
        await using var context = owned.Knowledge.Factory.CreateDbContext();
        var row = await context.KnowledgeOperations.SingleAsync();
        Assert.Equal(OperationKind.BeginGenerate, row.Kind);
        Assert.Equal(first.Binding.BatchId, row.OperationId);
        Assert.Equal(row.PayloadDigest == first.CanonicalPayloadDigest ? KnowledgeWriteStatus.AlreadyCommitted : KnowledgeWriteStatus.Conflict, (await owned.Store.LookupAsync(first)).Status);
        Assert.Equal(row.PayloadDigest == second.CanonicalPayloadDigest ? KnowledgeWriteStatus.AlreadyCommitted : KnowledgeWriteStatus.Conflict, (await owned.Store.LookupAsync(second)).Status);
        Assert.Single(owned.Guard.SubmittedAttempts);
        Assert.Empty(await context.AcceptedFileManifests.ToArrayAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CommittedAdmission_RawBatchAndRepositorySelectionMutation_IsRejectedByExistingSetupHistoryGuardsAsync()
    {
        await using var owned = await OwnedAdmission.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await owned.Store.StartFreshExecutionAsync(owned.Request)).Status);
        await AssertRejectedAsync(owned.Database, "UPDATE october.\"OctoberBatches\" SET \"TargetId\"='replacement-target'", PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(owned.Database, "UPDATE october.\"OctoberRepositorySelections\" SET \"RootBindingIdentity\"='replacement-root'", PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(owned.Database, "DELETE FROM october.\"OctoberContextReferences\"", PostgresErrorCodes.CheckViolation);
        await using var context = owned.Knowledge.Factory.CreateDbContext();
        Assert.Equal(owned.Request.Binding.TargetId, (await context.Batches.SingleAsync()).TargetId);
        Assert.Equal(owned.Knowledge.Origin.RootBindingIdentity, (await context.RepositorySelections.SingleAsync()).RootBindingIdentity);
        Assert.Equal(2, await context.ContextReferences.CountAsync());
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, (await owned.Store.LookupAsync(owned.Request)).Status);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task StartFreshExecutionAsync_SubmissionFence_RollsBackStagedClaimBeforeCleanStorageRetryAsync()
    {
        await using var owned = await OwnedAdmission.CreateAsync();
        owned.Guard.SubmissionFenced = true;
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, (await owned.Store.StartFreshExecutionAsync(owned.Request)).Status);
        Assert.Empty(owned.Guard.SubmittedAttempts);
        await using (var context = owned.Knowledge.Factory.CreateDbContext()) Assert.Empty(await context.KnowledgeOperations.ToArrayAsync());
        Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, (await owned.Store.LookupAsync(owned.Request)).Status);
        owned.Guard.SubmissionFenced = false;
        Assert.Equal(KnowledgeWriteStatus.Committed, (await owned.Store.StartFreshExecutionAsync(owned.Request)).Status);
        Assert.Single(owned.Guard.SubmittedAttempts);
        await using var retained = owned.Knowledge.Factory.CreateDbContext();
        Assert.Equal(1, await retained.KnowledgeOperations.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task StartFreshExecutionAsync_ActualServerCommitPending_LookupCannotDeclareAbsenceOrAReplacementRunAsync()
    {
        await using var owned = await OwnedAdmission.CreateAsync();
        await using var blocker = await HoldCommitAsync(owned.Database);
        var pending = owned.Store.StartFreshExecutionAsync(owned.Request);
        try
        {
            await WaitForCommitLockAsync(blocker);
            Assert.False(pending.IsCompleted);
            var original = await owned.Store.LookupAsync(owned.Request);
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, original.Status);
            Assert.Null(original.AcceptedFile); Assert.Null(original.Corpus); Assert.Null(original.Publication);
            var otherRun = AdmissionProtocol.Seal(owned.Request with { RunId = Guid.NewGuid(), AdmissionOperationId = Guid.NewGuid() });
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, (await owned.Store.LookupAsync(otherRun)).Status);
            await using var context = owned.Knowledge.Factory.CreateDbContext();
            Assert.Empty(await context.KnowledgeOperations.ToArrayAsync());
        }
        finally
        {
            await using var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(763204, 1)", blocker);
            Assert.True(Assert.IsType<bool>(await unlock.ExecuteScalarAsync()));
            await pending.WaitAsync(TimeSpan.FromSeconds(15));
        }
        Assert.Equal(KnowledgeWriteStatus.Committed, (await pending).Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, (await owned.Store.LookupAsync(owned.Request)).Status);
        Assert.Single(owned.Guard.SubmittedAttempts);
        await using var final = owned.Knowledge.Factory.CreateDbContext();
        Assert.Equal(1, await final.KnowledgeOperations.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task StartFreshExecutionAsync_ActualCommitWithLostGuardAcknowledgement_ReconcilesWithoutAnotherSubmissionAsync()
    {
        await using var owned = await OwnedAdmission.CreateAsync();
        owned.Guard.LoseAcknowledgement = true;
        var first = await owned.Store.StartFreshExecutionAsync(owned.Request);
        Assert.Contains(first.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, (await owned.Store.LookupAsync(owned.Request)).Status);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, (await owned.Store.StartFreshExecutionAsync(owned.Request)).Status);
        Assert.Single(owned.Guard.SubmittedAttempts);
        await using var context = owned.Knowledge.Factory.CreateDbContext();
        Assert.Equal(owned.Request.CanonicalPayloadDigest, (await context.KnowledgeOperations.SingleAsync()).PayloadDigest);
    }

    private static async Task AssertRejectedAsync(DisposablePostgreSqlDatabase database, string sql, string sqlState)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var rejected = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(sqlState, rejected.SqlState);
    }
    private static async Task<NpgsqlConnection> HoldCommitAsync(DisposablePostgreSqlDatabase database)
    {
        var connection = await database.OpenConnectionAsync();
        try
        {
            await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(763204, 1)", connection)) await acquire.ExecuteNonQueryAsync();
            await using var create = new NpgsqlCommand("CREATE FUNCTION october.hold_generate_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Kind\"=5 THEN PERFORM pg_advisory_xact_lock(763204, 1); END IF; RETURN NEW; END $$; CREATE CONSTRAINT TRIGGER hold_generate_commit AFTER INSERT ON october.\"KnowledgeOperations\" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION october.hold_generate_commit();", connection);
            await create.ExecuteNonQueryAsync();
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }
    private static async Task WaitForCommitLockAsync(NpgsqlConnection connection)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype='advisory' AND NOT granted AND classid=763204 AND objid=1 AND database=(SELECT oid FROM pg_database WHERE datname=current_database()))", connection);
            if (await command.ExecuteScalarAsync() is true) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("Owned Generate admission did not reach the deferred server COMMIT boundary.");
    }
    private sealed class OwnedAdmission(DisposablePostgreSqlDatabase database, OctoberKnowledgeAcceptanceFixture knowledge) : IAsyncDisposable
    {
        public DisposablePostgreSqlDatabase Database => database;
        public OctoberKnowledgeAcceptanceFixture Knowledge => knowledge;
        public AdmissionAuthority Authority { get; } = new();
        public AdmissionGuard Guard { get; } = new();
        public AdmissionLog Log { get; } = new();
        public IOctoberGenerateAdmissionStore Store => new OctoberGenerateAdmissionStore(Knowledge.Factory, Authority, Guard, Log);
        public GenerateAdmissionRequest Request => AdmissionProtocol.Seal(new(Knowledge.Binding, "held-operator", Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("44444444-4444-4444-4444-444444444444"), ""));
        public static async Task<OwnedAdmission> CreateAsync()
        {
            var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
            try
            {
                TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
                var options = new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString, options => options.CommandTimeout(30)).Options;
                await using (var context = new OctoberDbContext(options)) await context.Database.MigrateAsync();
                return new(database, await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options));
            }
            catch { await database.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync() { await Knowledge.DisposeAsync(); await Database.DisposeAsync(); }
    }
}

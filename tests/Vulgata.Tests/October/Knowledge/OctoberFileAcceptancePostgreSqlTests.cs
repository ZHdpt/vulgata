using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.Json;
using Npgsql;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Actual owned PostgreSQL 17 migration/transaction/constraint qualification, independent of SQLite evidence.</summary>
public sealed class OctoberFileAcceptancePostgreSqlTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task MigrateAsync_ExistingOctoberHistory_AddsKnowledgeWithoutPendingModelChangesAsync()
    {
        await using var owned = await OwnedPostgreSqlKnowledge.CreateAsync(upgradeExisting: true);
        await using var context = owned.Fixture.Factory.CreateDbContext();
        Assert.Contains(await context.Database.GetAppliedMigrationsAsync(), migration => migration.Contains("AddOctoberKnowledgeFoundation", StringComparison.Ordinal));
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.NotNull(owned.PriorSetupSnapshot);
        Assert.Equal(owned.PriorSetupSnapshot, await CapturePriorSetupAsync(context));
        var migrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(new[] { "20260930000000_InitialOctoberSetup", "20260930010000_AddOctoberPreparedSourceApprovals" }, migrations.Take(2));
        var result = await owned.Fixture.Writer().AcceptCompletedFileAsync(owned.Fixture.Seal());
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        Assert.Equal(2, await context.DocumentVersions.CountAsync());
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("identical")]
    [InlineData("changed-payload")]
    [InlineData("same-admission-new-operation")]
    public async Task AcceptCompletedFileAsync_ConcurrentWriters_CommitOnceAndReconcileExactOperationAsync(string collision)
    {
        await using var owned = await OwnedPostgreSqlKnowledge.CreateAsync();
        var fixture = owned.Fixture;
        var first = fixture.Seal();
        var second = first;
        if (collision == "changed-payload")
        {
            var unit = fixture.Proposal.Units[0];
            var changed = OctoberKnowledgeAcceptanceFixture.Unit(unit.SemanticSelector, unit.ParentSelector, "Alternative accepted title", unit.Anchors, unit.Sections);
            second = fixture.Seal(fixture.Proposal with { Units = fixture.Proposal.Units.SetItem(0, changed) });
        }
        if (collision == "same-admission-new-operation") second = fixture.Seal(operationId: Guid.NewGuid());
        var outcomes = await Task.WhenAll(fixture.Writer().AcceptCompletedFileAsync(first), fixture.Writer().AcceptCompletedFileAsync(second));
        Assert.Single(outcomes.Where(result => result.Status == KnowledgeWriteStatus.Committed));
        Assert.Single(outcomes.Where(result => result.Status == (collision == "identical" ? KnowledgeWriteStatus.AlreadyCommitted : KnowledgeWriteStatus.Conflict)));
        if (collision == "identical")
        {
            Assert.Equal(outcomes[0].AcceptedFile!.ManifestId, outcomes[1].AcceptedFile!.ManifestId);
            Assert.Equal(outcomes[0].AcceptedFile.Documents.Select(document => document.DocumentVersionId), outcomes[1].AcceptedFile.Documents.Select(document => document.DocumentVersionId));
        }
        await using var context = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync());
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
        Assert.Equal(2, await context.DocumentVersions.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcceptCompletedFileAsync_ServerCommitBlockedThenFenced_AbsentLookupRemainsIndeterminateUntilActualSettlementAsync()
    {
        await using var owned = await OwnedPostgreSqlKnowledge.CreateAsync();
        var fixture = owned.Fixture;
        await using var blocker = await owned.Database.OpenConnectionAsync();
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(763201, 1)", blocker)) await acquire.ExecuteNonQueryAsync();
        await using (var context = fixture.Factory.CreateDbContext())
        {
            await context.Database.ExecuteSqlRawAsync("CREATE FUNCTION october.hold_knowledge_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(763201, 1); RETURN NEW; END $$;");
            await context.Database.ExecuteSqlRawAsync("CREATE CONSTRAINT TRIGGER hold_knowledge_commit AFTER INSERT ON october.\"KnowledgeOperations\" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION october.hold_knowledge_commit();");
        }
        var submission = fixture.Seal();
        var pending = fixture.Writer().AcceptCompletedFileAsync(submission);
        try
        {
            await WaitForServerCommitLockAsync(blocker);
            Assert.False(pending.IsCompleted);
            Assert.Equal(1, fixture.Guard.Submissions);
            var lookup = await fixture.Writer().LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest);
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, lookup.Status);
            Assert.Null(lookup.AcceptedFile);
            fixture.Guard.Fenced = true;
        }
        finally
        {
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(763201, 1)", blocker);
            await release.ExecuteNonQueryAsync();
        }
        var completed = await pending.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(KnowledgeWriteStatus.Committed, completed.Status);
        var exact = await fixture.Writer().LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest);
        Assert.Equal(completed.AcceptedFile!.ManifestId, exact.AcceptedFile!.ManifestId);
        await using var final = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await final.AcceptedFileManifests.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcceptCompletedFileAsync_MiddleOfFileInsertFailure_RollsBackAllAcceptedStateAsync()
    {
        await using var owned = await OwnedPostgreSqlKnowledge.CreateAsync();
        await using (var context = owned.Fixture.Factory.CreateDbContext())
        {
            await context.Database.ExecuteSqlRawAsync("CREATE FUNCTION october.reject_document_insert() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'controlled document insert failure'; END $$;");
            await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_document_insert BEFORE INSERT ON october.\"OctoberDocumentVersions\" FOR EACH ROW EXECUTE FUNCTION october.reject_document_insert();");
        }
        var result = await owned.Fixture.Writer().AcceptCompletedFileAsync(owned.Fixture.Seal());
        Assert.Equal(KnowledgeWriteStatus.StorageUnavailable, result.Status);
        await owned.Fixture.AssertNoKnowledgeAsync();
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task RawSql_AcceptedLeavesAndBoundSetup_RejectsMutationAndDeletionAsync()
    {
        await using var owned = await OwnedPostgreSqlKnowledge.CreateAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await owned.Fixture.Writer().AcceptCompletedFileAsync(owned.Fixture.Seal())).Status);
        string[] forbidden =
        [
            "UPDATE october.\"OctoberDocumentVersions\" SET \"Title\" = 'Changed title', \"ContentJson\" = jsonb_set(\"ContentJson\", '{title}', to_jsonb('Changed title'::text))",
            "DELETE FROM october.\"OctoberQualifiedClaimGroups\"",
            "UPDATE october.\"KnowledgeContributors\" SET \"ContributorJson\" = '{}'::jsonb",
            "DELETE FROM october.\"OctoberKnowledgeUncertainties\"",
            "INSERT INTO october.\"OctoberDocumentSections\" (\"BatchId\", \"VersionId\", \"SectionId\", \"Ordinal\", \"Title\") SELECT \"BatchId\", \"VersionId\", 'postcommit-section', 99, 'Unaccepted section' FROM october.\"OctoberDocumentVersions\" LIMIT 1",
            "INSERT INTO october.\"KnowledgeContributors\" (\"BatchId\", \"ManifestId\", \"Ordinal\", \"ContributorJson\") SELECT \"BatchId\", \"ManifestId\", 999, \"ContributorJson\" FROM october.\"KnowledgeContributors\" WHERE \"Ordinal\" = 0",
            "UPDATE october.\"OctoberBatches\" SET \"SelectionDigest\" = repeat('f', 64)",
            "UPDATE october.\"OctoberRepositorySelections\" SET \"RootBindingIdentity\" = 'retargeted-root'",
            "DELETE FROM october.\"OctoberBatches\""
        ];
        foreach (string sql in forbidden)
        {
            await using var connection = await owned.Database.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            var rejected = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            if (sql == "DELETE FROM october.\"OctoberBatches\"")
                Assert.Contains(rejected.SqlState, new[] { PostgresErrorCodes.CheckViolation, PostgresErrorCodes.ForeignKeyViolation });
            else Assert.Equal(PostgresErrorCodes.CheckViolation, rejected.SqlState);
        }
        await using var retained = owned.Fixture.Factory.CreateDbContext();
        Assert.Equal(2, await retained.DocumentVersions.CountAsync());
        Assert.Equal(3, await retained.ClaimGroups.CountAsync());
        Assert.Equal(2, await retained.DocumentSections.CountAsync());
        Assert.Equal(2, await retained.KnowledgeContributors.CountAsync());
        Assert.Equal(owned.Fixture.Binding.SelectionDigest, (await retained.Batches.SingleAsync()).SelectionDigest);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task RawSql_CrossBatchVersionReference_RejectsForeignKeyEvenWhenBothBatchesExistAsync()
    {
        await using var owned = await OwnedPostgreSqlKnowledge.CreateAsync();
        var fixture = owned.Fixture;
        var accepted = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal());
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        Guid otherBatch = Guid.NewGuid();
        await using (var context = fixture.Factory.CreateDbContext())
        {
            context.Batches.Add(new() { BatchId = otherBatch, SelectionSlot = 2, TargetId = "other-owned-target", SubjectId = "owned-user", CanonicalDatabaseIdentity = "other-owned-database", SelectionDigest = new string('d', 64), ManagementAuthorityId = "owned-authority", State = OctoberBatchState.Selectable, SelectedAtUtc = DateTimeOffset.UtcNow });
            context.RepositorySelections.Add(new() { BatchId = otherBatch, RepositoryId = fixture.Origin.RepositoryId, RootBindingIdentity = fixture.Origin.RootBindingIdentity, OperatorLabel = "owned-second-batch" });
            await context.SaveChangesAsync();
        }
        await using (var context = fixture.Factory.CreateDbContext())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            // Match the acceptance protocol: parent rows must be born in the top-level transaction,
            // not in EF's automatic savepoint subtransaction with a different PostgreSQL xmin.
            context.Database.AutoSavepointsEnabled = false;
            Guid operation = Guid.NewGuid();
            Guid manifest = Guid.NewGuid();
            context.KnowledgeOperations.Add(new() { BatchId = otherBatch, Kind = OperationKind.AcceptFile, OperationId = operation, PayloadDigest = new string('e', 64), CommittedResultJson = "{}", ContributorsJson = "[]", CommittedAtUtc = DateTimeOffset.UtcNow });
            context.AcceptedFileManifests.Add(new() { BatchId = otherBatch, ManifestId = manifest, AdmissionId = Guid.NewGuid(), RepositoryId = fixture.Origin.RepositoryId, RootBindingIdentity = fixture.Origin.RootBindingIdentity, RelativePath = fixture.Origin.RelativePath, SourceDigest = fixture.Origin.SourceDigest, PreparedVersion = fixture.Origin.PreparedVersion, ProposalDigest = new string('e', 64), CoverageDisposition = CoverageDisposition.Accepted, UnitCount = 1, OperationId = operation, OperationKind = OperationKind.AcceptFile });
            await context.SaveChangesAsync();
            // The new manifest is a legitimate same-transaction parent, so the insertion guard
            // passes. Only the logical-document endpoint is foreign: it exists solely in batch A.
            await using var command = new NpgsqlCommand("INSERT INTO october.\"OctoberDocumentVersions\" (\"BatchId\", \"VersionId\", \"LogicalDocumentId\", \"ManifestId\", \"SchemaVersion\", \"Title\", \"ContentJson\", \"ContentDigest\", \"AnchorsJson\", \"CreatedAtUtc\") VALUES (@batch, @version, @document, @manifest, 1, 'foreign', '{}'::jsonb, @digest, '[]'::jsonb, NOW())", (NpgsqlConnection)context.Database.GetDbConnection(), (NpgsqlTransaction)transaction.GetDbTransaction());
            command.Parameters.AddWithValue("batch", otherBatch);
            command.Parameters.AddWithValue("version", Guid.NewGuid());
            command.Parameters.AddWithValue("document", accepted.AcceptedFile!.Documents[0].LogicalDocumentId);
            command.Parameters.AddWithValue("manifest", manifest);
            command.Parameters.AddWithValue("digest", new string('f', 64));
            var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
            await transaction.RollbackAsync();
        }
        await using var retained = fixture.Factory.CreateDbContext();
        Assert.Equal(1, await retained.KnowledgeOperations.CountAsync());
        Assert.Equal(2, await retained.DocumentVersions.CountAsync());
    }
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcceptCompletedFileAsync_AcknowledgementLostAfterPhysicalCommit_RecoversExactlyAsync()
    {
        await using var owned = await OwnedPostgreSqlKnowledge.CreateAsync();
        var fixture = owned.Fixture;
        fixture.Guard.LoseAcknowledgement = true;
        var submission = fixture.Seal();
        var result = await fixture.Writer().AcceptCompletedFileAsync(submission);
        Assert.Contains(result.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
        var lookup = await fixture.Writer().LookupOperationAsync(fixture.Binding, submission.Operation, submission.Proposal.CanonicalProposalDigest);
        Assert.Equal(result.AcceptedFile!.ManifestId, lookup.AcceptedFile!.ManifestId);
        Assert.Equal(1, fixture.Guard.Submissions);
    }

    private static async Task WaitForServerCommitLockAsync(NpgsqlConnection observer)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(15))
        {
            await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND classid = 763201 AND objid = 1 AND database = (SELECT oid FROM pg_database WHERE datname = current_database()))", observer);
            if (await query.ExecuteScalarAsync() is true) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("The owned database did not observe the expected submitted COMMIT waiting on its deferred trigger lock.");
    }

    private static Guid PriorBatch => Guid.Parse("00000000-0000-0000-0000-000000009001");
    private static async Task SeedPriorSetupAsync(NpgsqlConnection connection)
    {
        // These are existing setup tables only. The current context's new Knowledge guards
        // cannot seed before their referenced tables exist, so this upgrade precondition uses owned SQL.
        string[] statements =
        [
            "INSERT INTO october.\"OctoberBatches\" (\"BatchId\", \"SelectionSlot\", \"TargetId\", \"SubjectId\", \"CanonicalDatabaseIdentity\", \"SelectionDigest\", \"ManagementAuthorityId\", \"SelectedAtUtc\", \"State\") VALUES ('00000000-0000-0000-0000-000000009001', 2, 'prior-target', 'prior-operator', 'prior-bound-database', repeat('d',64), 'prior-management-authority', '2026-09-30T00:00:00Z', 1)",
            "INSERT INTO october.\"OctoberRepositorySelections\" (\"BatchId\", \"RepositoryId\", \"SystemId\", \"RootBindingIdentity\", \"OperatorLabel\") VALUES ('00000000-0000-0000-0000-000000009001', '00000000-0000-0000-0000-000000009002', '00000000-0000-0000-0000-000000009003', 'prior-prepared-root', 'prior-label')",
            "INSERT INTO october.\"OctoberContextReferences\" (\"BatchId\", \"Scope\", \"RecordId\") VALUES ('00000000-0000-0000-0000-000000009001', 2, '00000000-0000-0000-0000-000000009004')",
            "INSERT INTO october.\"OctoberAdvisoryEntries\" (\"BatchId\", \"RepositoryId\", \"Scope\", \"Applicability\", \"OriginRecordId\", \"Value\", \"EvidenceKind\") VALUES ('00000000-0000-0000-0000-000000009001', '00000000-0000-0000-0000-000000009002', 2, 0, '00000000-0000-0000-0000-000000009004', ' Retained prior advisory snapshot. ', 0)",
            "INSERT INTO october.\"OctoberContextDifferences\" (\"BatchId\", \"RepositoryId\", \"LeftScope\", \"RightScope\") VALUES ('00000000-0000-0000-0000-000000009001', '00000000-0000-0000-0000-000000009002', 0, 2)",
            "INSERT INTO october.\"OctoberPreparedSourceApprovals\" (\"ApprovalIdentity\", \"BatchId\", \"RepositoryId\", \"SubjectId\", \"Purpose\", \"RootBindingIdentity\", \"OperatorLabel\", \"ExpiresAtUtc\", \"IsRevoked\") VALUES ('prior-approval', '00000000-0000-0000-0000-000000009001', '00000000-0000-0000-0000-000000009002', 'prior-operator', 0, 'prior-prepared-root', 'prior-label', '2026-12-31T00:00:00Z', false)"
        ];
        foreach (string statement in statements)
        {
            await using var command = new NpgsqlCommand(statement, connection);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
    }
    private static async Task<string> CapturePriorSetupAsync(OctoberDbContext context)
    {
        var batch = await context.Batches.AsNoTracking().SingleAsync(record => record.BatchId == PriorBatch);
        var repository = await context.RepositorySelections.AsNoTracking().SingleAsync(record => record.BatchId == PriorBatch);
        var reference = await context.ContextReferences.AsNoTracking().SingleAsync(record => record.BatchId == PriorBatch);
        var advisory = await context.AdvisoryEntries.AsNoTracking().SingleAsync(record => record.BatchId == PriorBatch);
        var difference = await context.ContextDifferences.AsNoTracking().SingleAsync(record => record.BatchId == PriorBatch);
        var approval = await context.PreparedSourceApprovals.AsNoTracking().SingleAsync(record => record.BatchId == PriorBatch);
        Assert.Equal("prior-prepared-root", repository.RootBindingIdentity);
        Assert.Equal(" Retained prior advisory snapshot. ", advisory.Value);
        Assert.Equal("prior-approval", approval.ApprovalIdentity);
        Assert.False(approval.IsRevoked);
        return JsonSerializer.Serialize(new { batch, repository, reference, advisory, difference, approval });
    }
    private sealed class OwnedPostgreSqlKnowledge(DisposablePostgreSqlDatabase database, OctoberKnowledgeAcceptanceFixture fixture, string? priorSetupSnapshot = null) : IAsyncDisposable
    {
        public DisposablePostgreSqlDatabase Database => database;
        public OctoberKnowledgeAcceptanceFixture Fixture => fixture;
        public string? PriorSetupSnapshot => priorSetupSnapshot;
        public static async Task<OwnedPostgreSqlKnowledge> CreateAsync(bool upgradeExisting = false)
        {
            var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
            try
            {
                TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
                var options = new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString, postgres => postgres.CommandTimeout(30)).Options;
                string? before = null;
                await using (var context = new OctoberDbContext(options))
                {
                    if (upgradeExisting)
                    {
                        await context.GetService<IMigrator>().MigrateAsync("20260930010000_AddOctoberPreparedSourceApprovals");
                        await using var connection = await database.OpenConnectionAsync();
                        await SeedPriorSetupAsync(connection);
                        before = await CapturePriorSetupAsync(context);
                        Assert.Equal(2, (await context.Database.GetAppliedMigrationsAsync()).Count());
                    }
                    await context.Database.MigrateAsync();
                }
                var fixture = await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options);
                return new(database, fixture, before);
            }
            catch { await database.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            await fixture.DisposeAsync();
            await database.DisposeAsync();
        }
    }
}

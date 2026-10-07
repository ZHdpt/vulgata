using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Launcher-owned PostgreSQL 17 qualification of publication transactions and database constraints.</summary>
public sealed class OctoberPublicationPostgreSqlTests
{
    private static readonly string[] _publicationTables =
    [
        "OctoberCorpusSelections", "OctoberCorpusManifests", "OctoberCorpusDocuments", "OctoberCorpusCoverage",
        "OctoberKnowledgePublications", "OctoberPublicationMembers", "OctoberPublicationCoverage", "OctoberPublicationUncertainties"
    ];

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task MigrateAsync_PopulatedB0a_PreservesEveryExistingRowAndHasNoPendingModelChangesAsync()
    {
        await using var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        var options = Options(database);
        await using var context = new OctoberDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync("20261006000000_AddOctoberKnowledgeFoundation");
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options);
        await SeedPriorKnowledgeAsync(database, fixture);
        string[] priorTables =
        [
            "OctoberBatches", "OctoberRepositorySelections", "OctoberContextReferences", "OctoberAdvisoryEntries",
            "OctoberContextDifferences", "OctoberPreparedSourceApprovals", "KnowledgeOperations", "AcceptedFileManifests",
            "OctoberCodeUnits", "OctoberLogicalDocuments", "OctoberDocumentVersions", "OctoberDocumentSections",
            "OctoberQualifiedClaimGroups", "KnowledgeContributors", "OctoberCommunicationObservations", "OctoberKnowledgeUncertainties"
        ];
        var before = await CaptureAsync(database, priorTables);
        Assert.All(before.Values, value => Assert.NotEqual("[]", value));
        var priorMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(3, priorMigrations.Length);
        await context.Database.MigrateAsync();
        var after = await CaptureAsync(database, priorTables);
        foreach (string table in priorTables) Assert.Equal(before[table], after[table]);
        Assert.False(context.Database.HasPendingModelChanges());
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(priorMigrations, applied.Take(3));
        Assert.Equal("20261006010000_AddOctoberKnowledgePublications", applied[3]);
        Assert.All((await CaptureAsync(database, _publicationTables)).Values, value => Assert.Equal("[]", value));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PublishSelectionAsync_ExactCorpus_PersistsMembershipCoverageAndRejectsHistoryMutationAsync()
    {
        await using var owned = await OwnedPublication.CreateAsync();
        var request = await owned.FreezeAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await owned.Fixture.Writer().PublishSelectionAsync(request)).Status);
        await using (var context = owned.Fixture.Factory.CreateDbContext())
        {
            Assert.Equal(owned.Accepted.Documents.Select(x => x.DocumentVersionId).Order(),
                (await context.PublicationMembers.Select(x => x.VersionId).ToArrayAsync()).Order());
            Assert.Equal(2, await context.CorpusCoverage.CountAsync());
            Assert.Equal(2, await context.PublicationCoverage.CountAsync());
            Assert.Equal(1, await context.PublicationUncertainties.CountAsync());
            var corpusCoverage = await context.CorpusCoverage.OrderBy(x => x.AdmissionId).Select(x => x.CoverageJson).ToArrayAsync();
            var publishedCoverage = await context.PublicationCoverage.OrderBy(x => x.AdmissionId).Select(x => x.CoverageJson).ToArrayAsync();
            Assert.Equal(corpusCoverage, publishedCoverage);
        }
        var retained = await CaptureAsync(owned.Database, _publicationTables);
        foreach (string table in _publicationTables)
        {
            await AssertRejectedAsync(owned.Database, $"UPDATE october.\"{table}\" SET \"BatchId\" = \"BatchId\"", PostgresErrorCodes.CheckViolation);
            await AssertRejectedAsync(owned.Database, $"DELETE FROM october.\"{table}\"", PostgresErrorCodes.CheckViolation);
            // BEFORE INSERT must reject the old parent before the duplicate-key constraint fires.
            await AssertRejectedAsync(owned.Database, $"INSERT INTO october.\"{table}\" SELECT * FROM october.\"{table}\" LIMIT 1", PostgresErrorCodes.CheckViolation);
        }
        string truncate = "TRUNCATE " + string.Join(", ", _publicationTables.Select(table => $"october.\"{table}\""));
        await AssertRejectedAsync(owned.Database, truncate, PostgresErrorCodes.CheckViolation);
        var after = await CaptureAsync(owned.Database, _publicationTables);
        foreach (string table in _publicationTables) Assert.Equal(retained[table], after[table]);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task LoadAndReadAsync_RealPublishedPostgreSqlJsonb_PreservesCanonicalContentAndExactQualifiedReadAsync()
    {
        await using var owned = await OwnedPublication.CreateAsync();
        var request = await owned.FreezeAsync();
        var result = await owned.Fixture.Writer().PublishSelectionAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        var publication = Assert.IsType<PublicationBinding>(result.Publication);
        var source = new OctoberKnowledgePublishedSource(owned.Fixture.Factory);

        // Real persisted source exercises PostgreSQL JSONB representation and every populated
        // preflight path: document, coverage, contributor and uncertainty payloads.
        var loaded = Assert.IsType<OctoberPublishedKnowledge>(await source.LoadAsync(publication));
        Assert.Equal(publication, loaded.Publication);
        Assert.Equal(2, loaded.Documents.Length);
        Assert.Equal(2, loaded.Coverage.Length);
        Assert.Single(loaded.Uncertainties);
        var proposal = owned.Fixture.Proposal.Units[0];
        var document = Assert.Single(loaded.Documents.Where(x => x.Title == proposal.Title));
        Assert.Equal(OctoberKnowledgePolicy.CanonicalizeJson(proposal.ContentJson), document.ContentJson);
        Assert.Contains(document.Reference, owned.Accepted.Documents);
        await using (var context = owned.Fixture.Factory.CreateDbContext())
        {
            string storedJsonb = await context.DocumentVersions.Where(x => x.VersionId == document.Reference.DocumentVersionId)
                .Select(x => x.ContentJson).SingleAsync();
            Assert.NotEqual(document.ContentJson, storedJsonb);
            Assert.Equal(document.ContentJson, OctoberKnowledgePolicy.CanonicalizeJson(storedJsonb));
        }

        // Disclosure/time/logging are explicit controlled adapters; the source and reader are
        // production implementations. Tokens use a test-owned ephemeral Data Protection keyring.
        var policy = new B0bReaderDisclosure();
        var log = new B0bReadLog();
        var tokens = new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider());
        var reader = new OctoberKnowledgeReader(source, publication, policy,
            new OctoberKnowledgeCursorProtector(tokens, new B0bReadClock()), log);
        var opened = await reader.OpenReadSelectionAsync(new(publication, new("ordinary-user", "controlled-stamp"),
            KnowledgeMode.IT, KnowledgePurpose.Reader, [owned.Fixture.Origin.RepositoryId]));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        var selection = Assert.IsType<ReadSelection>(opened.Selection);
        var handle = new DocumentHandle(document.Reference.BatchId, publication.PublicationId,
            document.Reference.LogicalDocumentId, document.Reference.DocumentVersionId);
        var page = await reader.ReadExactAsync(new(selection, handle));
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        Assert.Equal(handle, page.Document!.Handle);
        Assert.Contains("Acquires the order lock.", page.Content!, StringComparison.Ordinal);
        Assert.Contains(page.Claims, claim => claim.Qualification == ClaimQualification.SourceSupported);
        Assert.Contains(page.Claims, claim => claim.Qualification == ClaimQualification.Advisory);
        Assert.True(page.Coverage!.IsPartial);
        Assert.Null(page.Next);
        var related = await reader.GetRelatedAsync(new(selection, handle));
        Assert.Equal(KnowledgeReadStatus.Success, related.Status);
        Assert.Equal("unknown-destination", Assert.Single(related.PermittedUncertainties).Key);
        Assert.Contains(log.Entries, entry => entry.Operation == KnowledgeReadOperation.ReadExact);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("member")]
    [InlineData("coverage")]
    public async Task RawSql_NewPublicationParentWithInexactEndpoint_RejectsForeignKeyAsync(string endpoint)
    {
        await using var owned = await OwnedPublication.CreateAsync();
        var request = await owned.FreezeAsync();
        Assert.Equal(KnowledgeWriteStatus.Committed, (await owned.Fixture.Writer().PublishSelectionAsync(request)).Status);
        await using var connection = await owned.Database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        Guid publication = Guid.NewGuid();
        Guid operation = Guid.NewGuid();
        const string parents = """
            INSERT INTO october."KnowledgeOperations" ("BatchId", "Kind", "OperationId", "PayloadDigest", "CommittedResultJson", "ContributorsJson", "CommittedAtUtc")
            SELECT "BatchId", "Kind", @operation, "PayloadDigest", "CommittedResultJson", "ContributorsJson", "CommittedAtUtc"
            FROM october."KnowledgeOperations" WHERE "Kind" = @kind LIMIT 1;
            INSERT INTO october."OctoberKnowledgePublications" ("BatchId", "PublicationId", "CorpusId", "CorpusDigest", "OperationKind", "OperationId", "ContributorsJson", "CreatedAtUtc")
            SELECT "BatchId", @publication, "CorpusId", "CorpusDigest", "OperationKind", @operation, "ContributorsJson", "CreatedAtUtc"
            FROM october."OctoberKnowledgePublications" LIMIT 1;
            """;
        await using (var create = new NpgsqlCommand(parents, connection, transaction))
        {
            create.Parameters.AddWithValue("operation", operation);
            create.Parameters.AddWithValue("publication", publication);
            create.Parameters.AddWithValue("kind", (int)OperationKind.PublishSelection);
            Assert.Equal(2, await create.ExecuteNonQueryAsync());
        }
        // A fresh parent passes the creation seal. An existing version attached to a different
        // logical document must nevertheless fail the exact endpoint foreign key.
        string sql = endpoint == "member" ? """
            INSERT INTO october."OctoberPublicationMembers" ("BatchId", "PublicationId", "CorpusId", "LogicalDocumentId", "VersionId", "Ordinal")
            SELECT a."BatchId", @publication, a."CorpusId", a."LogicalDocumentId", b."VersionId", 0
            FROM october."OctoberCorpusDocuments" a JOIN october."OctoberCorpusDocuments" b
            ON a."BatchId" = b."BatchId" AND a."CorpusId" = b."CorpusId" AND a."LogicalDocumentId" <> b."LogicalDocumentId" LIMIT 1
            """ : """
            INSERT INTO october."OctoberPublicationCoverage" ("BatchId", "PublicationId", "CorpusId", "AdmissionId", "RepositoryId", "CoverageJson", "ContributorsJson")
            SELECT "BatchId", @publication, "CorpusId", @foreignAdmission, "RepositoryId", "CoverageJson", "ContributorsJson"
            FROM october."OctoberCorpusCoverage" LIMIT 1
            """;
        await using var invalid = new NpgsqlCommand(sql, connection, transaction);
        invalid.Parameters.AddWithValue("publication", publication);
        if (endpoint == "coverage") invalid.Parameters.AddWithValue("foreignAdmission", Guid.NewGuid());
        var failure = await Assert.ThrowsAsync<PostgresException>(() => invalid.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        await transaction.RollbackAsync();
        await using var retained = owned.Fixture.Factory.CreateDbContext();
        Assert.Equal(1, await retained.KnowledgePublications.CountAsync());
        Assert.Equal(3, await retained.KnowledgeOperations.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishSelectionAsync_ConcurrentOperation_CommitsOnceAndReplaysOrConflictsAsync(bool changedPayload)
    {
        await using var owned = await OwnedPublication.CreateAsync();
        var first = await owned.FreezeAsync();
        var second = changedPayload ? first with { Documents = [first.Documents[0]], Uncertainties = [] } : first;
        second = second with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(second) };
        var results = await Task.WhenAll(owned.Fixture.Writer().PublishSelectionAsync(first), owned.Fixture.Writer().PublishSelectionAsync(second));
        Assert.Single(results.Where(x => x.Status == KnowledgeWriteStatus.Committed));
        Assert.Single(results.Where(x => x.Status == (changedPayload ? KnowledgeWriteStatus.Conflict : KnowledgeWriteStatus.AlreadyCommitted)));
        if (!changedPayload) Assert.Equal(results[0].Publication, results[1].Publication);
        await using var context = owned.Fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        Assert.Equal(3, await context.KnowledgeOperations.CountAsync());
        Assert.NotNull(results.Single(x => x.Status == KnowledgeWriteStatus.Committed).Publication);
        int expectedMembers = changedPayload && results[1].Status == KnowledgeWriteStatus.Committed ? 1 : 2;
        Assert.Equal(expectedMembers, await context.PublicationMembers.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task FreezeCorpusAsync_CommitOverlapsLateAcceptance_SealsCorpusAndReplaysExactFreezeAsync()
    {
        await using var owned = await OwnedPublication.CreateAsync();
        var freeze = owned.FreezeRequest();
        owned.Fixture.FreezeAuthority.Approve(freeze);
        await using var blocker = await HoldCommitAsync(owned.Database);
        var pendingFreeze = owned.Fixture.Writer().FreezeCorpusAsync(freeze);
        Task<KnowledgeWriteResult>? late = null;
        try
        {
            await WaitForCommitLockAsync(blocker);
            Assert.False(pendingFreeze.IsCompleted);
            var proposal = owned.Fixture.Proposal;
            Guid admission = Guid.NewGuid();
            proposal = proposal with
            {
                AdmissionId = admission, AttemptId = Guid.NewGuid(), Units = [], Observations = [], Uncertainties = [],
                Coverage = proposal.Coverage with { AdmissionId = admission, Disposition = CoverageDisposition.CompletedZeroUnit, Reason = "Late complete file" }
            };
            late = owned.Fixture.Writer().AcceptCompletedFileAsync(owned.Fixture.Seal(proposal, Guid.NewGuid()));
            // A server-side lock wait proves overlap without using a scheduling delay as the oracle.
            await WaitForBatchLockAsync(blocker);
            Assert.False(late.IsCompleted);
        }
        finally
        {
            await ReleaseCommitAsync(blocker);
            await pendingFreeze.WaitAsync(TimeSpan.FromSeconds(30));
            if (late is not null) await late.WaitAsync(TimeSpan.FromSeconds(30));
        }
        var frozen = await pendingFreeze.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
        Assert.NotNull(late);
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, (await late.WaitAsync(TimeSpan.FromSeconds(30))).Status);
        var replay = await owned.Fixture.Writer().FreezeCorpusAsync(freeze);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(frozen.Corpus, replay.Corpus);
        var changed = freeze with { TerminalCoverage = freeze.TerminalCoverage.RemoveAt(1) };
        changed = changed with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(changed) };
        Assert.Equal(KnowledgeWriteStatus.Conflict, (await owned.Fixture.Writer().FreezeCorpusAsync(changed)).Status);
        await using var context = owned.Fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.AcceptedFileManifests.CountAsync());
        Assert.Equal(1, await context.CorpusSelections.CountAsync());
        Assert.Equal(2, await context.CorpusDocuments.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PublishSelectionAsync_StagingFenceThenLostAcknowledgement_RollsBackBeforeSubmissionAndReconcilesPhysicalCommitAsync()
    {
        await using var owned = await OwnedPublication.CreateAsync();
        var request = await owned.FreezeAsync();
        int priorSubmissions = owned.Fixture.Guard.Submissions;
        owned.Fixture.Guard.BeforeSubmission = () => owned.Fixture.Guard.Fenced = true;
        Assert.Equal(KnowledgeWriteStatus.StaleOrFenced, (await owned.Fixture.Writer().PublishSelectionAsync(request)).Status);
        Assert.Equal(priorSubmissions, owned.Fixture.Guard.Submissions);
        await AssertNoPublicationAsync(owned);
        owned.Fixture.Guard.Fenced = false;
        owned.Fixture.Guard.BeforeSubmission = null;
        owned.Fixture.Guard.LoseAcknowledgement = true;
        var committed = await owned.Fixture.Writer().PublishSelectionAsync(request);
        Assert.Contains(committed.Status, new[] { KnowledgeWriteStatus.Committed, KnowledgeWriteStatus.AlreadyCommitted });
        Assert.NotNull(committed.Publication);
        var lookup = await owned.Fixture.Writer().LookupOperationAsync(owned.Fixture.Binding, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(committed.Publication, lookup.Publication);
        Assert.Equal(priorSubmissions + 1, owned.Fixture.Guard.Submissions);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PublishSelectionAsync_ServerCommitPending_AbsentLookupIsIndeterminateUntilActualSettlementAsync()
    {
        await using var owned = await OwnedPublication.CreateAsync();
        var request = await owned.FreezeAsync();
        await using var blocker = await HoldCommitAsync(owned.Database);
        var pending = owned.Fixture.Writer().PublishSelectionAsync(request);
        try
        {
            await WaitForCommitLockAsync(blocker);
            Assert.False(pending.IsCompleted);
            var lookup = await owned.Fixture.Writer().LookupOperationAsync(owned.Fixture.Binding, request.Operation, request.CanonicalPayloadDigest);
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, lookup.Status);
            Assert.Null(lookup.Publication);
            await AssertNoPublicationAsync(owned);
            owned.Fixture.Guard.Fenced = true;
        }
        finally
        {
            await ReleaseCommitAsync(blocker);
            await pending.WaitAsync(TimeSpan.FromSeconds(30));
        }
        var committed = await pending.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(KnowledgeWriteStatus.Committed, committed.Status);
        Assert.NotNull(committed.Publication);
        var exact = await owned.Fixture.Writer().LookupOperationAsync(owned.Fixture.Binding, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(committed.Publication, exact.Publication);
        await using var context = owned.Fixture.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgePublications.CountAsync());
        Assert.Equal(2, await context.PublicationMembers.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PublishSelectionAsync_MemberInsertFails_RollsBackOperationAndEveryPublicationChildAsync()
    {
        await using var owned = await OwnedPublication.CreateAsync();
        var request = await owned.FreezeAsync();
        await using (var context = owned.Fixture.Factory.CreateDbContext())
        {
            await context.Database.ExecuteSqlRawAsync("CREATE FUNCTION october.reject_publication_member() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'controlled publication member insert failure'; END $$;");
            await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_publication_member BEFORE INSERT ON october.\"OctoberPublicationMembers\" FOR EACH ROW EXECUTE FUNCTION october.reject_publication_member();");
        }
        Assert.Equal(KnowledgeWriteStatus.StorageUnavailable, (await owned.Fixture.Writer().PublishSelectionAsync(request)).Status);
        await AssertNoPublicationAsync(owned);
        await using var retained = owned.Fixture.Factory.CreateDbContext();
        Assert.Equal(1, await retained.CorpusSelections.CountAsync());
        Assert.Equal(2, await retained.DocumentVersions.CountAsync());
    }

    private static async Task AssertNoPublicationAsync(OwnedPublication owned)
    {
        await using var context = owned.Fixture.Factory.CreateDbContext();
        Assert.Equal(0, await context.KnowledgePublications.CountAsync());
        Assert.Equal(0, await context.PublicationMembers.CountAsync());
        Assert.Equal(0, await context.PublicationCoverage.CountAsync());
        Assert.Equal(0, await context.PublicationUncertainties.CountAsync());
        Assert.Equal(0, await context.KnowledgeOperations.CountAsync(x => x.Kind == OperationKind.PublishSelection));
    }

    private static DbContextOptions<OctoberDbContext> Options(DisposablePostgreSqlDatabase database)
    {
        TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
        return new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString, postgres => postgres.CommandTimeout(30)).Options;
    }

    private static async Task<Dictionary<string, string>> CaptureAsync(DisposablePostgreSqlDatabase database, IEnumerable<string> tables)
    {
        await using var connection = await database.OpenConnectionAsync();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string table in tables)
        {
            await using var command = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text), '[]'::jsonb)::text FROM october.\"{table}\" t", connection);
            result.Add(table, Assert.IsType<string>(await command.ExecuteScalarAsync()));
        }
        return result;
    }

    private static async Task AssertRejectedAsync(DisposablePostgreSqlDatabase database, string sql, string state)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(state, failure.SqlState);
    }

    private static async Task<NpgsqlConnection> HoldCommitAsync(DisposablePostgreSqlDatabase database)
    {
        var blocker = await database.OpenConnectionAsync();
        try
        {
            await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(763202, 1)", blocker)) await acquire.ExecuteNonQueryAsync();
            await using var create = new NpgsqlCommand("CREATE FUNCTION october.hold_publication_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(763202, 1); RETURN NEW; END $$; CREATE CONSTRAINT TRIGGER hold_publication_commit AFTER INSERT ON october.\"KnowledgeOperations\" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION october.hold_publication_commit();", blocker);
            await create.ExecuteNonQueryAsync();
            return blocker;
        }
        catch { await blocker.DisposeAsync(); throw; }
    }

    private static async Task ReleaseCommitAsync(NpgsqlConnection blocker)
    {
        await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(763202, 1)", blocker);
        Assert.True(Assert.IsType<bool>(await release.ExecuteScalarAsync()));
    }

    private static Task WaitForCommitLockAsync(NpgsqlConnection observer) => WaitForLockAsync(observer,
        "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND classid = 763202 AND objid = 1 AND database = (SELECT oid FROM pg_database WHERE datname = current_database()))");

    private static Task WaitForBatchLockAsync(NpgsqlConnection observer) => WaitForLockAsync(observer,
        "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid() AND wait_event_type = 'Lock' AND query LIKE '%OctoberBatches%FOR SHARE%')");

    private static async Task WaitForLockAsync(NpgsqlConnection observer, string sql)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15))
        {
            await using var query = new NpgsqlCommand(sql, observer);
            if (await query.ExecuteScalarAsync() is true) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("The owned PostgreSQL database did not observe the expected transaction lock wait.");
    }

    private static async Task SeedPriorKnowledgeAsync(DisposablePostgreSqlDatabase database, OctoberKnowledgeAcceptanceFixture fixture)
    {
        // The current writer checks new corpus tables, so seed the old schema directly.
        // Accepted parents and children share one transaction, preserving the B0a membership seals.
        const string sql = """
            INSERT INTO october."OctoberContextDifferences" VALUES (@batch, @repository, 0, 2);
            INSERT INTO october."OctoberPreparedSourceApprovals" VALUES ('retained-approval', @batch, @repository, 'owned-operator', 0, @root, 'owned-label', '2026-12-31T00:00:00Z', false);
            INSERT INTO october."KnowledgeOperations" VALUES (@batch, @kind, @operation, @digest, '{"retained":"operation"}', '[{"retained":"contributors"}]', '2026-10-06T00:00:00Z');
            INSERT INTO october."AcceptedFileManifests" VALUES (@batch, @manifest, @admission, @repository, @root, @path, @source, @prepared, @digest, @coverage, NULL, 1, @kind, @operation);
            INSERT INTO october."OctoberCodeUnits" VALUES (@batch, @unit, @manifest, @repository, @digest, 'retained-unit', NULL);
            INSERT INTO october."OctoberLogicalDocuments" VALUES (@batch, @document, @unit, @documentKind);
            INSERT INTO october."OctoberDocumentVersions" VALUES (@batch, @version, @document, @manifest, 1, ' Retained title ', '{"retained":"canonical document"}', @digest, '[{"retained":"anchor"}]', '2026-10-06T00:00:00Z');
            INSERT INTO october."OctoberDocumentSections" VALUES (@batch, @version, 'retained-section', 0, 'Retained section');
            INSERT INTO october."OctoberQualifiedClaimGroups" VALUES (@batch, @version, 'retained-section', 'retained-claim', ' Retained qualified claim ', 0, '[{"retained":"evidence"}]', '["Retained limitation"]');
            INSERT INTO october."KnowledgeContributors" VALUES (@batch, @manifest, 0, '{"retained":"contributor"}');
            INSERT INTO october."OctoberCommunicationObservations" VALUES (@batch, @manifest, 'retained-observation', @unit, '{"retained":"observation"}');
            INSERT INTO october."OctoberKnowledgeUncertainties" VALUES (@batch, @manifest, 'retained-uncertainty', '{"retained":"uncertainty"}');
            """;
        await using var connection = await database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("batch", fixture.Binding.BatchId);
        command.Parameters.AddWithValue("repository", fixture.Origin.RepositoryId);
        command.Parameters.AddWithValue("root", fixture.Origin.RootBindingIdentity);
        command.Parameters.AddWithValue("path", fixture.Origin.RelativePath);
        command.Parameters.AddWithValue("source", fixture.Origin.SourceDigest);
        command.Parameters.AddWithValue("prepared", fixture.Origin.PreparedVersion);
        command.Parameters.AddWithValue("digest", new string('c', 64));
        command.Parameters.AddWithValue("kind", (int)OperationKind.AcceptFile);
        command.Parameters.AddWithValue("coverage", (int)CoverageDisposition.Accepted);
        command.Parameters.AddWithValue("documentKind", (int)DocumentKind.CodeLogic);
        foreach (string parameter in new[] { "operation", "manifest", "admission", "unit", "document", "version" }) command.Parameters.AddWithValue(parameter, Guid.NewGuid());
        Assert.Equal(12, await command.ExecuteNonQueryAsync());
        await transaction.CommitAsync();
    }

    private sealed class OwnedPublication(DisposablePostgreSqlDatabase database, OctoberKnowledgeAcceptanceFixture fixture, AcceptedFile accepted) : IAsyncDisposable
    {
        public DisposablePostgreSqlDatabase Database => database;
        public OctoberKnowledgeAcceptanceFixture Fixture => fixture;
        public AcceptedFile Accepted => accepted;

        public static async Task<OwnedPublication> CreateAsync()
        {
            var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
            OctoberKnowledgeAcceptanceFixture? fixture = null;
            try
            {
                var options = Options(database);
                await using (var context = new OctoberDbContext(options)) await context.Database.MigrateAsync();
                fixture = await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options);
                var result = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal());
                Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
                return new(database, fixture, Assert.IsType<AcceptedFile>(result.AcceptedFile));
            }
            catch
            {
                if (fixture is not null) await fixture.DisposeAsync();
                await database.DisposeAsync();
                throw;
            }
        }

        public CorpusFreezeRequest FreezeRequest()
        {
            var request = new CorpusFreezeRequest(new(fixture.Binding.BatchId, OperationKind.FreezeCorpus, OctoberKnowledgeAcceptanceFixture.Id(610)), fixture.Binding, "", [accepted],
                [accepted.Coverage, new(OctoberKnowledgeAcceptanceFixture.Id(611), fixture.Origin with { RelativePath = "src/unprocessed.cs" }, CoverageDisposition.Unprocessed, "Stopped before dispatch")]);
            return request with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(request) };
        }

        public async Task<PublicationRequest> FreezeAsync()
        {
            var request = FreezeRequest();
            fixture.FreezeAuthority.Approve(request);
            var result = await fixture.Writer().FreezeCorpusAsync(request);
            Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
            var publication = new PublicationRequest(new(fixture.Binding.BatchId, OperationKind.PublishSelection, OctoberKnowledgeAcceptanceFixture.Id(612)), Assert.IsType<CorpusHandle>(result.Corpus), "", accepted.Documents, [], fixture.Proposal.Uncertainties, [], request.TerminalCoverage);
            return publication with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(publication) };
        }

        public async ValueTask DisposeAsync()
        {
            await fixture.DisposeAsync();
            await database.DisposeAsync();
        }
    }
}

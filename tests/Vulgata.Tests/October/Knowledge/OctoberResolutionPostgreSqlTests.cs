using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Actual PostgreSQL migration/transaction qualification in launcher-owned databases; no real provider or Scan claim.</summary>
public sealed class OctoberResolutionPostgreSqlTests
{
    private static readonly string[] _tables =
    [
        "OctoberResolutionItems", "OctoberRelationshipVersions", "OctoberUncertaintyVersions",
        "OctoberResolutionDocumentDependencies", "OctoberPublicationRelationships", "OctoberPublicationResolutionUncertainties"
    ];

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task MigrateAsync_PopulatedB0AndDialogue_PreservesExactPriorRowsAndAddsEmptyResolutionAsync()
    {
        await using var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        var options = Options(database);
        await using (var old = new OctoberDbContext(options))
            await old.GetService<IMigrator>().MigrateAsync("20261007000000_AddOctoberDialogue");
        await using var dialogue = await DialoguePersistenceFixture.CreateAsync(options);
        var admission = await dialogue.AdmitAsync();
        var terminal = dialogue.TerminalRequest(admission);
        var reference = new OctoberChatReference(new(dialogue.Document.BatchId, dialogue.Identity.Publication.PublicationId,
            dialogue.Document.LogicalDocumentId, dialogue.Document.DocumentVersionId, "overview"), true, 2, 345);
        var dependencies = dialogue.Dependencies.Extend(terminal.InferenceDependencies!, "prior-exact-document-input");
        var proof = dialogue.Dependencies.Consume(reference, dependencies);
        terminal = terminal with { InferenceDependencies = dependencies, References = [reference], ConsumedReads = [new(reference, proof)] };
        terminal = terminal with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(terminal) };
        await dialogue.RequireSaveAsync(terminal);
        var before = await CaptureExistingAsync(database);
        foreach (string table in new[] { "OctoberDocumentVersions", "OctoberKnowledgePublications", "OctoberDialogueConversations", "OctoberDialogueTurns", "OctoberDialogueTerminals", "OctoberDialogueReferences" })
            Assert.NotEqual("[]", before[table]);
        await using var current = new OctoberDbContext(options);
        var priorMigrations = (await current.Database.GetAppliedMigrationsAsync()).ToArray();

        await current.Database.MigrateAsync();

        foreach (var table in before) Assert.Equal(table.Value, await CaptureAsync(database, table.Key));
        var applied = (await current.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(priorMigrations, applied.Take(priorMigrations.Length));
        Assert.Contains("20261007010000_AddOctoberResolutionEvidence", applied);
        Assert.False(current.Database.HasPendingModelChanges());
        foreach (string table in _tables) Assert.Equal("[]", await CaptureAsync(database, table));
        var history = await dialogue.Store.ReadHistoryAsync(dialogue.HistoryRequest, dialogue.Subject);
        Assert.Equal(terminal.AnswerText, Assert.Single(history.Turns).AnswerText);
        Assert.Equal(terminal.References.ToArray(), history.Turns[0].References.ToArray());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcceptAndPublishAsync_JsonbRoundTrip_PreservesWitnessAllInputsQualifiedEdgeAndOriginalB0Async()
    {
        await using var owned = await OwnedResolution.CreateAsync();
        var fixture = owned.Fixture;
        string clBefore = await fixture.ClSnapshotAsync();
        var loaded = await fixture.Store().LoadCorpusAsync(fixture.Corpus.Handle);
        Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
        Assert.Equal(fixture.Inquiry.Fingerprint, Assert.Single(OctoberResolutionPolicy.Plan(loaded.Corpus!).Inquiries).Fingerprint);
        var request = fixture.Request();
        var accepted = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var outcome = accepted.Outcome!;
        Assert.Equal(ResolutionOutcomeKind.Supported, outcome.Kind);
        Assert.True(fixture.AllContributors.ToHashSet().SetEquals(outcome.Contributors));
        Assert.Contains(ResolutionPersistenceFixture.SourceLimit, outcome.Uncertainty!.Limitations);
        Assert.Equal(3, await CountAsync(owned.Database, "OctoberResolutionDocumentDependencies"));
        await using (var connection = await owned.Database.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("SELECT \"LogicalDocumentId\", \"VersionId\" FROM october.\"OctoberResolutionDocumentDependencies\" WHERE \"IsInquiryCaller\"", connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(fixture.Inquiry.Caller.Document.LogicalDocumentId, reader.GetGuid(0));
            Assert.Equal(fixture.Inquiry.Caller.Document.DocumentVersionId, reader.GetGuid(1));
            Assert.False(await reader.ReadAsync());
        }
        await using (var connection = await owned.Database.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("SELECT \"WitnessJson\"::text FROM october.\"OctoberResolutionItems\"", connection))
        {
            var stored = JsonSerializer.Deserialize<ResolutionWitness>(Assert.IsType<string>(await command.ExecuteScalarAsync()), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(request.Witness), OctoberKnowledgePolicy.SerializeCanonical(stored));
            Assert.All(stored!.SupportingAssertions, assertion => { Assert.Null(assertion.Evidence.Document); Assert.Null(assertion.Evidence.ClaimGroupId); Assert.NotNull(assertion.Evidence.Source); });
        }
        var publication = await fixture.Acceptance.Writer().PublishSelectionAsync(fixture.Publication([outcome]));
        Assert.Equal(KnowledgeWriteStatus.Committed, publication.Status);
        Assert.NotEqual(fixture.B0Publication.PublicationId, publication.Publication!.PublicationId);
        var page = await fixture.RelatedAsync(publication.Publication, outcome.Relationship!.From);
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        Assert.Equal(outcome.Relationship.RelationshipVersionId, Assert.Single(page.Relationships).RelationshipVersionId);
        Assert.Contains(page.PermittedUncertainties, unknown => unknown.Key == outcome.Uncertainty.Key);
        Assert.Empty((await fixture.RelatedAsync(fixture.B0Publication, outcome.Relationship.From)).Relationships);
        Assert.Equal(clBefore, await fixture.ClSnapshotAsync());
        var replay = await fixture.Store().LookupOutcomeAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, replay.Status);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(outcome), OctoberKnowledgePolicy.SerializeCanonical(replay.Outcome));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("identical")]
    [InlineData("changed-payload")]
    [InlineData("duplicate-inquiry")]
    public async Task AcceptOutcomeAsync_ConcurrentWriters_PersistsOneExactOperationAndInquiryWinnerAsync(string collision)
    {
        await using var owned = await OwnedResolution.CreateAsync();
        var fixture = owned.Fixture;
        var first = fixture.Request();
        var second = collision switch
        {
            "changed-payload" => ResolutionPersistenceFixture.Digest(first with { Witness = first.Witness! with { ConnectionExplanation = "Another supported static interpretation." } }),
            "duplicate-inquiry" => ResolutionPersistenceFixture.Digest(first with { Operation = first.Operation with { OperationId = Guid.NewGuid() }, AttemptId = Guid.NewGuid() }),
            _ => first
        };
        var outcomes = await Task.WhenAll(fixture.Store().AcceptOutcomeAsync(first), fixture.Store().AcceptOutcomeAsync(second));
        Assert.Single(outcomes.Where(x => x.Status == KnowledgeWriteStatus.Committed));
        Assert.Single(outcomes.Where(x => x.Status == (collision == "identical" ? KnowledgeWriteStatus.AlreadyCommitted : KnowledgeWriteStatus.Conflict)));
        if (collision == "identical") Assert.Equal(outcomes[0].Outcome!.Relationship!.RelationshipVersionId, outcomes[1].Outcome!.Relationship!.RelationshipVersionId);
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberResolutionItems"));
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberRelationshipVersions"));
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberUncertaintyVersions"));
        Assert.Equal(3, await CountAsync(owned.Database, "OctoberResolutionDocumentDependencies"));
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(1, await context.KnowledgeOperations.CountAsync(x => x.Kind == OperationKind.AcceptResolution));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ImmutableEvidenceAndSelection_RawMutationDeletionAndOldParentAppend_AreRejectedAsync()
    {
        await using var owned = await OwnedResolution.CreateAsync();
        await AcceptAndPublishAsync(owned);
        var before = new Dictionary<string, string>();
        foreach (string table in _tables)
        {
            before[table] = await CaptureAsync(owned.Database, table);
            Assert.NotEqual("[]", before[table]);
            await AssertRejectedAsync(owned.Database, $"UPDATE october.\"{table}\" SET \"BatchId\" = \"BatchId\"", PostgresErrorCodes.CheckViolation);
            await AssertRejectedAsync(owned.Database, $"DELETE FROM october.\"{table}\"", PostgresErrorCodes.CheckViolation);
            await AssertRejectedAsync(owned.Database, $"INSERT INTO october.\"{table}\" SELECT * FROM october.\"{table}\" LIMIT 1", PostgresErrorCodes.CheckViolation);
        }
        await AssertRejectedAsync(owned.Database, "TRUNCATE " + string.Join(", ", _tables.Select(table => $"october.\"{table}\"")), PostgresErrorCodes.CheckViolation);
        foreach (var table in before) Assert.Equal(table.Value, await CaptureAsync(owned.Database, table.Key));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("relationship")]
    [InlineData("dependency")]
    public async Task RawFreshItem_ExistingVersionWithWrongLogicalEndpoint_RejectsExactCorpusForeignKeyAsync(string child)
    {
        await using var owned = await OwnedResolution.CreateAsync();
        var accepted = await owned.Fixture.Store().AcceptOutcomeAsync(owned.Fixture.Request());
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        await using var connection = await owned.Database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        Guid item = Guid.NewGuid();
        Guid operation = Guid.NewGuid();
        const string parents = """
            INSERT INTO october."KnowledgeOperations" ("BatchId","Kind","OperationId","PayloadDigest","CommittedResultJson","ContributorsJson","CommittedAtUtc")
            SELECT "BatchId","Kind",@operation,"PayloadDigest","CommittedResultJson","ContributorsJson","CommittedAtUtc" FROM october."KnowledgeOperations" WHERE "Kind"=4 LIMIT 1;
            INSERT INTO october."OctoberResolutionItems" ("BatchId","CorpusId","ItemId","OperationKind","OperationId","InquiryFingerprint","PolicyVersion","WitnessJson","OutcomeJson","ContributorsJson","PlanDispositionsJson","CreatedAtUtc")
            SELECT "BatchId","CorpusId",@item,"OperationKind",@operation,@fingerprint,"PolicyVersion","WitnessJson","OutcomeJson","ContributorsJson","PlanDispositionsJson","CreatedAtUtc" FROM october."OctoberResolutionItems" LIMIT 1;
            """;
        await using (var create = new NpgsqlCommand(parents, connection, transaction))
        {
            create.Parameters.AddWithValue("operation", operation);
            create.Parameters.AddWithValue("item", item);
            create.Parameters.AddWithValue("fingerprint", new string('f', 64));
            Assert.Equal(2, await create.ExecuteNonQueryAsync());
        }
        string sql = child == "relationship" ? """
            INSERT INTO october."OctoberRelationshipVersions" ("BatchId","RelationshipVersionId","CorpusId","ItemId","FromLogicalDocumentId","FromVersionId","ToLogicalDocumentId","ToVersionId","ContentJson")
            SELECT "BatchId",@relationship,"CorpusId",@item,"FromLogicalDocumentId","ToVersionId","ToLogicalDocumentId","ToVersionId","ContentJson" FROM october."OctoberRelationshipVersions" LIMIT 1
            """ : """
            INSERT INTO october."OctoberResolutionDocumentDependencies" ("BatchId","CorpusId","ItemId","LogicalDocumentId","VersionId")
            SELECT a."BatchId",a."CorpusId",@item,a."LogicalDocumentId",b."VersionId" FROM october."OctoberCorpusDocuments" a
            JOIN october."OctoberCorpusDocuments" b ON a."BatchId"=b."BatchId" AND a."CorpusId"=b."CorpusId" AND a."LogicalDocumentId"<>b."LogicalDocumentId" LIMIT 1
            """;
        await using var invalid = new NpgsqlCommand(sql, connection, transaction);
        invalid.Parameters.AddWithValue("item", item);
        if (child == "relationship") invalid.Parameters.AddWithValue("relationship", Guid.NewGuid());
        var failure = await Assert.ThrowsAsync<PostgresException>(() => invalid.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        await transaction.RollbackAsync();
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberResolutionItems"));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task RawFreshPublication_RetargetedRelationshipEndpoint_RejectsExactEvidenceAndPublishedVersionForeignKeysAsync()
    {
        await using var owned = await OwnedResolution.CreateAsync();
        var publication = await AcceptAndPublishAsync(owned);
        await using var connection = await owned.Database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        Guid next = Guid.NewGuid();
        Guid operation = Guid.NewGuid();
        const string parents = """
            INSERT INTO october."KnowledgeOperations" ("BatchId","Kind","OperationId","PayloadDigest","CommittedResultJson","ContributorsJson","CommittedAtUtc")
            SELECT "BatchId","Kind",@operation,"PayloadDigest","CommittedResultJson","ContributorsJson","CommittedAtUtc" FROM october."KnowledgeOperations" WHERE "Kind"=2 LIMIT 1;
            INSERT INTO october."OctoberKnowledgePublications" ("BatchId","PublicationId","CorpusId","CorpusDigest","OperationKind","OperationId","ContributorsJson","CreatedAtUtc")
            SELECT "BatchId",@next,"CorpusId","CorpusDigest","OperationKind",@operation,"ContributorsJson","CreatedAtUtc" FROM october."OctoberKnowledgePublications" WHERE "PublicationId"=@prior;
            INSERT INTO october."OctoberPublicationMembers" ("BatchId","PublicationId","CorpusId","LogicalDocumentId","VersionId","Ordinal")
            SELECT "BatchId",@next,"CorpusId","LogicalDocumentId","VersionId","Ordinal" FROM october."OctoberPublicationMembers" WHERE "PublicationId"=@prior;
            """;
        await using (var create = new NpgsqlCommand(parents, connection, transaction))
        {
            create.Parameters.AddWithValue("operation", operation);
            create.Parameters.AddWithValue("next", next);
            create.Parameters.AddWithValue("prior", publication.PublicationId);
            Assert.Equal(5, await create.ExecuteNonQueryAsync());
        }
        const string invalidSql = """
            INSERT INTO october."OctoberPublicationRelationships" ("BatchId","PublicationId","CorpusId","RelationshipVersionId","FromLogicalDocumentId","FromVersionId","ToLogicalDocumentId","ToVersionId")
            SELECT "BatchId",@next,"CorpusId","RelationshipVersionId","FromLogicalDocumentId","ToVersionId","ToLogicalDocumentId","ToVersionId" FROM october."OctoberRelationshipVersions" LIMIT 1
            """;
        await using var invalid = new NpgsqlCommand(invalidSql, connection, transaction);
        invalid.Parameters.AddWithValue("next", next);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => invalid.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
        await transaction.RollbackAsync();
        Assert.Equal(2, await CountAsync(owned.Database, "OctoberKnowledgePublications"));
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberPublicationRelationships"));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcceptOutcomeAsync_QualificationInsertFails_RollsBackAllEvidenceAndOperationThenRetriesExactlyAsync()
    {
        await using var owned = await OwnedResolution.CreateAsync();
        var request = owned.Fixture.Request();
        await ExecuteAsync(owned.Database, "CREATE FUNCTION october.reject_resolution_unknown() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'controlled qualification failure'; END $$; CREATE TRIGGER reject_resolution_unknown BEFORE INSERT ON october.\"OctoberUncertaintyVersions\" FOR EACH ROW EXECUTE FUNCTION october.reject_resolution_unknown();");
        var failed = await owned.Fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.StorageUnavailable, failed.Status);
        foreach (string table in _tables) Assert.Equal(0, await CountAsync(owned.Database, table));
        await using (var context = owned.Fixture.Acceptance.Factory.CreateDbContext())
            Assert.Equal(0, await context.KnowledgeOperations.CountAsync(x => x.Kind == OperationKind.AcceptResolution));
        await ExecuteAsync(owned.Database, "DROP TRIGGER reject_resolution_unknown ON october.\"OctoberUncertaintyVersions\"; DROP FUNCTION october.reject_resolution_unknown();");
        Assert.Equal(KnowledgeWriteStatus.Committed, (await owned.Fixture.Store().AcceptOutcomeAsync(request)).Status);
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberResolutionItems"));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PublishSelectionAsync_EvidenceSelectionInsertFails_RollsBackPublicationAndPreservesAcceptedPrivateEvidenceAsync()
    {
        await using var owned = await OwnedResolution.CreateAsync();
        var accepted = await owned.Fixture.Store().AcceptOutcomeAsync(owned.Fixture.Request());
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var request = owned.Fixture.Publication([accepted.Outcome!]);
        await ExecuteAsync(owned.Database, "CREATE FUNCTION october.reject_resolution_selection() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'controlled selection failure'; END $$; CREATE TRIGGER reject_resolution_selection BEFORE INSERT ON october.\"OctoberPublicationRelationships\" FOR EACH ROW EXECUTE FUNCTION october.reject_resolution_selection();");
        var failed = await owned.Fixture.Acceptance.Writer().PublishSelectionAsync(request);
        Assert.Equal(KnowledgeWriteStatus.StorageUnavailable, failed.Status);
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberKnowledgePublications"));
        Assert.Equal(3, await CountAsync(owned.Database, "OctoberPublicationMembers"));
        Assert.Equal(0, await CountAsync(owned.Database, "OctoberPublicationRelationships"));
        Assert.Equal(0, await CountAsync(owned.Database, "OctoberPublicationResolutionUncertainties"));
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberResolutionItems"));
        Assert.Empty((await owned.Fixture.RelatedAsync(owned.Fixture.B0Publication, accepted.Outcome!.Relationship!.From)).Relationships);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcceptOutcomeAsync_ServerCommitPending_AbsenceRemainsIndeterminateUntilActualSettlementAsync()
    {
        await using var owned = await OwnedResolution.CreateAsync();
        var fixture = owned.Fixture;
        var request = fixture.Request();
        await using var blocker = await owned.Database.OpenConnectionAsync();
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(763204, 1)", blocker)) await acquire.ExecuteNonQueryAsync();
        await ExecuteAsync(owned.Database, "CREATE FUNCTION october.hold_resolution_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(763204, 1); RETURN NEW; END $$; CREATE CONSTRAINT TRIGGER hold_resolution_commit AFTER INSERT ON october.\"OctoberResolutionItems\" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION october.hold_resolution_commit();");
        var pending = fixture.Store().AcceptOutcomeAsync(request);
        try
        {
            await WaitForCommitLockAsync(blocker);
            Assert.False(pending.IsCompleted);
            var lookup = await fixture.Store().LookupOutcomeAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
            Assert.Equal(KnowledgeWriteStatus.CommitIndeterminate, lookup.Status);
            Assert.Null(lookup.Outcome);
            Assert.Equal(0, await CountAsync(owned.Database, "OctoberResolutionItems"));
            fixture.Acceptance.Guard.Fenced = true;
        }
        finally
        {
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(763204, 1)", blocker);
            Assert.True(Assert.IsType<bool>(await release.ExecuteScalarAsync()));
            await pending.WaitAsync(TimeSpan.FromSeconds(30));
        }
        var result = await pending;
        Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
        var exact = await fixture.Store().LookupOutcomeAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, exact.Status);
        Assert.Equal(result.Outcome!.Relationship!.RelationshipVersionId, exact.Outcome!.Relationship!.RelationshipVersionId);
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberResolutionItems"));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PublishSelectionAsync_UndisposedInquiry_PreventsPartialB1UntilExplicitFailedOutcomeAsync()
    {
        await using var owned = await OwnedResolution.CreateAsync(secondCaller: true);
        var fixture = owned.Fixture;
        var first = await fixture.Store().AcceptOutcomeAsync(fixture.Request());
        Assert.Equal(KnowledgeWriteStatus.Committed, first.Status);
        var missing = await fixture.Acceptance.Writer().PublishSelectionAsync(fixture.Publication([first.Outcome!]));
        Assert.Contains(missing.Status, new[] { KnowledgeWriteStatus.InvalidProposal, KnowledgeWriteStatus.NotCompleted });
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberKnowledgePublications"));
        var other = OctoberResolutionPolicy.Plan(fixture.Corpus).Inquiries.Single(x => x.Fingerprint != fixture.Inquiry.Fingerprint);
        var request = ResolutionPersistenceFixture.Digest(fixture.Request() with
        {
            Operation = fixture.Request().Operation with { OperationId = Guid.NewGuid() }, AttemptId = Guid.NewGuid(),
            InquiryFingerprint = other.Fingerprint, Witness = null, ExecutionDisposition = ResolutionOutcomeKind.Failed
        });
        var failed = await fixture.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, failed.Status);
        Assert.Equal(ResolutionOutcomeKind.Failed, failed.Outcome!.Kind);
        Assert.Null(failed.Outcome.Relationship);
        var published = await fixture.Acceptance.Writer().PublishSelectionAsync(fixture.Publication([first.Outcome!, failed.Outcome]));
        Assert.Equal(KnowledgeWriteStatus.Committed, published.Status);
        Assert.Equal(1, await CountAsync(owned.Database, "OctoberPublicationRelationships"));
        Assert.Equal(2, await CountAsync(owned.Database, "OctoberPublicationResolutionUncertainties"));
        var unknown = await fixture.RelatedAsync(published.Publication!, other.Caller.Document);
        Assert.Contains(unknown.PermittedUncertainties, x => x.Key == failed.Outcome.Uncertainty!.Key);
        Assert.Empty((await fixture.RelatedAsync(fixture.B0Publication, other.Caller.Document)).PermittedUncertainties);
    }

    private static async Task<PublicationBinding> AcceptAndPublishAsync(OwnedResolution owned)
    {
        var accepted = await owned.Fixture.Store().AcceptOutcomeAsync(owned.Fixture.Request());
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var publication = await owned.Fixture.Acceptance.Writer().PublishSelectionAsync(owned.Fixture.Publication([accepted.Outcome!]));
        Assert.Equal(KnowledgeWriteStatus.Committed, publication.Status);
        return Assert.IsType<PublicationBinding>(publication.Publication);
    }

    private static DbContextOptions<OctoberDbContext> Options(DisposablePostgreSqlDatabase database)
    {
        TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
        return new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString, postgres => postgres.CommandTimeout(30)).Options;
    }

    private static async Task<Dictionary<string, string>> CaptureExistingAsync(DisposablePostgreSqlDatabase database)
    {
        var names = new List<string>();
        await using (var connection = await database.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema='october' AND table_type='BASE TABLE' AND table_name <> '__OctoberMigrationsHistory' ORDER BY table_name", connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in names) result.Add(name, await CaptureAsync(database, name));
        return result;
    }

    private static async Task<string> CaptureAsync(DisposablePostgreSqlDatabase database, string table)
    {
        await using var connection = await database.OpenConnectionAsync();
        string identifier = table.Replace("\"", "\"\"", StringComparison.Ordinal);
        await using var command = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text),'[]'::jsonb)::text FROM october.\"{identifier}\" t", connection);
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }

    private static async Task<long> CountAsync(DisposablePostgreSqlDatabase database, string table)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM october.\"{table}\"", connection);
        return Assert.IsType<long>(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(DisposablePostgreSqlDatabase database, string sql)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertRejectedAsync(DisposablePostgreSqlDatabase database, string sql, string expected)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(expected, error.SqlState);
    }

    private static async Task WaitForCommitLockAsync(NpgsqlConnection observer)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(15))
        {
            await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype='advisory' AND NOT granted AND classid=763204 AND objid=1 AND database=(SELECT oid FROM pg_database WHERE datname=current_database()))", observer);
            if (await query.ExecuteScalarAsync() is true) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("The owned database did not observe the actual submitted resolution COMMIT waiting on its deferred trigger.");
    }

    private sealed class OwnedResolution(DisposablePostgreSqlDatabase database, ResolutionPersistenceFixture fixture) : IAsyncDisposable
    {
        public DisposablePostgreSqlDatabase Database => database;
        public ResolutionPersistenceFixture Fixture => fixture;
        public static async Task<OwnedResolution> CreateAsync(bool secondCaller = false)
        {
            var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
            ResolutionPersistenceFixture? fixture = null;
            try
            {
                var options = Options(database);
                await using (var context = new OctoberDbContext(options)) await context.Database.MigrateAsync();
                fixture = await ResolutionPersistenceFixture.CreateWithOptionsAsync(options, secondCaller);
                return new(database, fixture);
            }
            catch
            {
                if (fixture is not null) await fixture.DisposeAsync();
                await database.DisposeAsync();
                throw;
            }
        }
        public async ValueTask DisposeAsync()
        {
            await fixture.DisposeAsync();
            await database.DisposeAsync();
        }
    }
}

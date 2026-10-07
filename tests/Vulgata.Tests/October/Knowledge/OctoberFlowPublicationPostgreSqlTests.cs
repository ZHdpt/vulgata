using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NpgsqlTypes;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Authored real-storage checks, executable only against runner-owned PostgreSQL. Controlled seeds are not live source/provider qualification.</summary>
public sealed class OctoberFlowPublicationPostgreSqlTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task MigrateAsync_PopulatedB1EvidenceAndPublication_PreservesExactRowsAndOwnerReadsAsync()
    {
        await using var donor = await FreshAsync();
        await using var basis = await ResolutionPersistenceFixture.CreateWithOptionsAsync(OctoberFlowHistoricalSeed.Options(donor));
        var request = basis.Request();
        var accepted = await basis.Store().AcceptOutcomeAsync(request);
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var outcome = Assert.IsType<ResolutionOutcome>(accepted.Outcome);
        var published = await basis.Acceptance.Writer().PublishSelectionAsync(basis.Publication([outcome]));
        Assert.Equal(KnowledgeWriteStatus.Committed, published.Status);
        var publication = Assert.IsType<PublicationBinding>(published.Publication);
        await using var target = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        var options = OctoberFlowHistoricalSeed.Options(target);
        await using (var old = new OctoberDbContext(options))
            await old.GetService<IMigrator>().MigrateAsync("20261007010000_AddOctoberResolutionEvidence");
        await OctoberFlowHistoricalSeed.CopyAsync(donor, target, false, resolution: true);
        var before = await OctoberFlowHistoricalSeed.CaptureAsync(target, false, resolution: true);
        foreach (string table in OctoberFlowHistoricalSeed.ResolutionTables) Assert.NotEqual("[]", before[table]);
        await using var context = new OctoberDbContext(options);
        await context.Database.MigrateAsync();
        var after = await OctoberFlowHistoricalSeed.CaptureAsync(target, false, resolution: true);
        foreach (var row in before) Assert.Equal(row.Value, after[row.Key]);
        Assert.Contains(Migration, await context.Database.GetAppliedMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        foreach (string table in FlowTables) Assert.Equal("[]", await OctoberFlowHistoricalSeed.RowsAsync(target, table));

        var factory = new KnowledgeContextFactory(options);
        var owner = new OctoberResolutionStore(factory, basis.Authorization, basis.Acceptance.Guard, basis.Acceptance.Log);
        var lookup = await owner.LookupOutcomeAsync(request.Corpus, request.Operation, request.CanonicalPayloadDigest);
        Assert.Equal(KnowledgeWriteStatus.AlreadyCommitted, lookup.Status);
        Assert.Equal(OctoberKnowledgePolicy.SerializeCanonical(outcome), OctoberKnowledgePolicy.SerializeCanonical(lookup.Outcome));
        var reader = new OctoberKnowledgeReader(new OctoberKnowledgePublishedSource(factory), publication, new B0bReaderDisclosure(),
            new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()), new B0bReadLog());
        var opened = await reader.OpenReadSelectionAsync(new(publication, new("ordinary-user", "controlled-stamp"), KnowledgeMode.IT,
            KnowledgePurpose.Reader, basis.Corpus.Documents.Select(x => x.RepositoryId).Distinct().ToImmutableArray()));
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        var from = outcome.Relationship!.From;
        var related = await reader.GetRelatedAsync(new(opened.Selection!, new(from.BatchId, publication.PublicationId, from.LogicalDocumentId, from.DocumentVersionId)));
        Assert.Equal(KnowledgeReadStatus.Success, related.Status);
        Assert.Equal(outcome.Relationship.RelationshipVersionId, Assert.Single(related.Relationships).RelationshipVersionId);
        Assert.Contains(related.PermittedUncertainties, uncertainty => uncertainty.Key == outcome.Uncertainty!.Key);
    }
    internal const string Migration = "20261007140153_AddOctoberFlowPublications";
    internal static readonly string[] FlowTables = ["OctoberBusinessFinalizations", "OctoberLogicalFlows", "OctoberFlowGroupVersions",
        "OctoberFlowDocumentDependencies", "OctoberFlowRelationshipDependencies", "OctoberFlowResolutionUncertaintyDependencies", "OctoberFlowFileUncertaintyDependencies"];

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task MigrateAsync_PopulatedPriorClAndDialogue_PreservesEveryPriorColumnAndExactReferencesAsync()
    {
        await using var target = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        await using var seeded = await OctoberFlowHistoricalSeed.CreateAsync(target, "20261007010000_AddOctoberResolutionEvidence", true);
        var before = await OctoberFlowHistoricalSeed.CaptureAsync(target, true);
        foreach (string table in new[] { "OctoberDocumentVersions", "OctoberKnowledgePublications", "OctoberDialogueTerminals", "OctoberDialogueReferences" })
            Assert.NotEqual("[]", before[table]);
        await using var context = new OctoberDbContext(OctoberFlowHistoricalSeed.Options(target));
        var prior = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        await context.Database.MigrateAsync();
        var after = await OctoberFlowHistoricalSeed.CaptureAsync(target, true);
        foreach (var row in before) Assert.Equal(row.Value, after[row.Key]);
        Assert.Equal(prior, (await context.Database.GetAppliedMigrationsAsync()).Take(prior.Length));
        Assert.Contains(Migration, await context.Database.GetAppliedMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        foreach (string table in FlowTables) Assert.Equal("[]", await OctoberFlowHistoricalSeed.RowsAsync(target, table));
        var history = await seeded.TargetStore(target).ReadHistoryAsync(seeded.Fixture.HistoryRequest, seeded.Fixture.Subject);
        var turn = Assert.Single(history.Turns);
        Assert.Equal(seeded.Terminal!.AnswerText, turn.AnswerText);
        Assert.Equal(seeded.Terminal.References.ToArray(), turn.References.ToArray());
        Assert.All(await context.DocumentVersions.ToArrayAsync(), version =>
        {
            Assert.Equal(DocumentKind.CodeLogic, version.Kind); Assert.NotNull(version.ManifestId);
            Assert.Null(version.FlowGroupVersionId); Assert.Null(version.FinalizationOperationId);
        });
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task NativeClManifestQualifiedForeignKey_StillRejectsValidDocumentPairedWithDifferentAcceptedManifestAsync()
    {
        await using var donor = await FlowPgCase.CreateAsync();
        await using var target = await FreshAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => OctoberFlowHistoricalSeed.CopyAsync(donor.Database, target, false, (table, rows) =>
        {
            if (table != "OctoberCorpusDocuments") return;
            Assert.True(rows.Count >= 2);
            string first = rows[0]!["ManifestId"]!.GetValue<string>();
            var other = rows.First(row => row!["ManifestId"]!.GetValue<string>() != first)!;
            rows[0]!["ManifestId"] = other["ManifestId"]!.DeepClone();
        }));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("FK_OctoberCorpusDocuments_OctoberDocumentVersions_BatchId_Mani~", error.ConstraintName);
        Assert.Equal("[]", await OctoberFlowHistoricalSeed.RowsAsync(target, "KnowledgeOperations"));
        await using var connection = await target.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM pg_constraint WHERE conrelid='october.\"OctoberDocumentVersions\"'::regclass AND conname='AK_OctoberDocumentVersions_BatchId_ManifestId_LogicalDocumentI~' AND contype='u'", connection);
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ClAndBlOrigins_AreExclusiveAndRejectMixedOwnerColumnsAsync()
    {
        await using var donor = await FlowPgCase.CreateAsync();
        foreach (string owner in new[] { "OctoberLogicalDocuments", "OctoberDocumentVersions" })
        {
            await using var target = await FreshAsync();
            var error = await Assert.ThrowsAsync<PostgresException>(() => OctoberFlowHistoricalSeed.CopyAsync(donor.Database, target, false, (table, rows) =>
            {
                if (table == owner) rows[0]![owner == "OctoberLogicalDocuments" ? "LogicalFlowId" : "FlowGroupVersionId"] = Guid.NewGuid().ToString();
            }));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal(owner == "OctoberLogicalDocuments" ? "CK_LogicalDocument_Owner" : "CK_DocumentVersion_Owner", error.ConstraintName);
        }
        await donor.PublishAsync();
        Guid acceptedManifest;
        await using (var context = donor.Basis.Acceptance.Factory.CreateDbContext())
        {
            var logical = await context.LogicalDocuments.SingleAsync(x => x.Kind == DocumentKind.BusinessLogic);
            var version = await context.DocumentVersions.SingleAsync(x => x.Kind == DocumentKind.BusinessLogic);
            Assert.Null(logical.UnitId); Assert.NotNull(logical.LogicalFlowId);
            Assert.Null(version.ManifestId); Assert.NotNull(version.FlowGroupVersionId);
            Assert.Equal(OperationKind.FinalizeCorpus, version.FinalizationOperationKind);
            acceptedManifest = await context.AcceptedFileManifests.Select(x => x.ManifestId).FirstAsync();
        }
        await using var connection = await donor.Database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var next = await NewFlowParentsAsync(connection, transaction);
        var rejected = await Assert.ThrowsAsync<PostgresException>(() => CloneAsync(connection, transaction, "OctoberDocumentVersions", "\"Kind\"=1", new
        {
            VersionId = Guid.NewGuid(), FlowGroupVersionId = next.Group, FinalizationOperationId = next.Operation,
            ManifestId = acceptedManifest
        }));
        Assert.Equal(PostgresErrorCodes.CheckViolation, rejected.SqlState);
        Assert.Equal("CK_DocumentVersion_Owner", rejected.ConstraintName);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task FlowDependencies_RejectCrossBatchAndWrongCorpusEvenForExistingDocumentIdsAsync()
    {
        await using var owned = await FlowPgCase.CreateAsync();
        await owned.PublishAsync();
        foreach (bool wrongBatch in new[] { true, false })
        {
            await using var connection = await owned.Database.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var next = await NewFlowParentsAsync(connection, transaction);
            object patch = wrongBatch ? new { GroupVersionId = next.Group, BatchId = Guid.NewGuid() }
                : (object)new { GroupVersionId = next.Group, CorpusId = Guid.NewGuid() };
            var error = await Assert.ThrowsAsync<PostgresException>(() => CloneAsync(connection, transaction, "OctoberFlowDocumentDependencies", "TRUE", patch));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        }
        Assert.Equal(BusinessFinalizationStatus.Published, (await owned.Store.LookupAsync(owned.Request.Corpus, owned.Request.Operation, owned.Request.CanonicalPayloadDigest)).Status);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PublishedFlowAndBlHistory_RejectsUpdatesDeletesTruncationAndLateDependenciesAsync()
    {
        await using var owned = await FlowPgCase.CreateAsync();
        await owned.PublishAsync();
        var before = new Dictionary<string, string>();
        foreach (string table in FlowTables) before[table] = await OctoberFlowHistoricalSeed.RowsAsync(owned.Database, table);
        foreach (string table in new[] { "OctoberBusinessFinalizations", "OctoberLogicalFlows", "OctoberFlowGroupVersions", "OctoberFlowDocumentDependencies" })
        {
            Assert.NotEqual("[]", before[table]);
            foreach (string sql in new[] { $"UPDATE october.\"{table}\" SET \"BatchId\"=\"BatchId\"", $"DELETE FROM october.\"{table}\"" })
                await RejectAsync(owned.Database, sql, PostgresErrorCodes.CheckViolation);
        }
        foreach (string table in FlowTables) await RejectAsync(owned.Database, $"TRUNCATE october.\"{table}\" CASCADE", PostgresErrorCodes.CheckViolation);
        await RejectAsync(owned.Database, "UPDATE october.\"OctoberDocumentVersions\" SET \"Title\"='changed' WHERE \"Kind\"=1", PostgresErrorCodes.CheckViolation);
        await RejectAsync(owned.Database, "DELETE FROM october.\"OctoberLogicalDocuments\" WHERE \"Kind\"=1", PostgresErrorCodes.CheckViolation);
        await RejectAsync(owned.Database, "INSERT INTO october.\"OctoberFlowDocumentDependencies\" SELECT * FROM october.\"OctoberFlowDocumentDependencies\" LIMIT 1", PostgresErrorCodes.CheckViolation);
        foreach (var row in before) Assert.Equal(row.Value, await OctoberFlowHistoricalSeed.RowsAsync(owned.Database, row.Key));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task PublishAsync_DatabaseRejectsDependency_RollsBackFlowBlPublicationAndReceiptTogetherAsync()
    {
        await using var owned = await FlowPgCase.CreateAsync();
        string priorVersions = await OctoberFlowHistoricalSeed.RowsAsync(owned.Database, "OctoberDocumentVersions");
        string priorPublications = await OctoberFlowHistoricalSeed.RowsAsync(owned.Database, "OctoberKnowledgePublications");
        await ExecuteAsync(owned.Database, "CREATE FUNCTION october.reject_owned_flow_dependency() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'controlled dependency write rejection'; END $$; CREATE TRIGGER reject_owned_flow_dependency BEFORE INSERT ON october.\"OctoberFlowDocumentDependencies\" FOR EACH ROW EXECUTE FUNCTION october.reject_owned_flow_dependency();");
        var failed = await owned.Store.PublishAsync(owned.Request);
        Assert.Null(failed.Publication);
        Assert.DoesNotContain(failed.Status, new[] { BusinessFinalizationStatus.Published, BusinessFinalizationStatus.PublishedPartial });
        foreach (string table in FlowTables) Assert.Equal("[]", await OctoberFlowHistoricalSeed.RowsAsync(owned.Database, table));
        Assert.Equal(priorVersions, await OctoberFlowHistoricalSeed.RowsAsync(owned.Database, "OctoberDocumentVersions"));
        Assert.Equal(priorPublications, await OctoberFlowHistoricalSeed.RowsAsync(owned.Database, "OctoberKnowledgePublications"));
        await using (var context = owned.Basis.Acceptance.Factory.CreateDbContext())
        {
            Assert.Equal(0, await context.KnowledgeOperations.CountAsync(x => x.Kind == OperationKind.FinalizeCorpus));
            Assert.Equal(0, await context.LogicalDocuments.CountAsync(x => x.Kind == DocumentKind.BusinessLogic));
        }
        await ExecuteAsync(owned.Database, "DROP TRIGGER reject_owned_flow_dependency ON october.\"OctoberFlowDocumentDependencies\"; DROP FUNCTION october.reject_owned_flow_dependency();");
        await owned.PublishAsync();
        Assert.NotEqual("[]", await OctoberFlowHistoricalSeed.RowsAsync(owned.Database, "OctoberBusinessFinalizations"));
    }

    private static async Task<DisposablePostgreSqlDatabase> FreshAsync()
    {
        var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        try { await using var context = new OctoberDbContext(OctoberFlowHistoricalSeed.Options(database)); await context.Database.MigrateAsync(); return database; }
        catch { await database.DisposeAsync(); throw; }
    }
    private static async Task<(Guid Operation, Guid Group)> NewFlowParentsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var operation = Guid.NewGuid(); var group = Guid.NewGuid();
        await CloneAsync(connection, transaction, "KnowledgeOperations", "\"Kind\"=3", new { OperationId = operation });
        await CloneAsync(connection, transaction, "OctoberBusinessFinalizations", "TRUE", new { OperationId = operation, PublicationId = (Guid?)null });
        await CloneAsync(connection, transaction, "OctoberFlowGroupVersions", "TRUE", new { OperationId = operation, GroupVersionId = group });
        return (operation, group);
    }
    private static async Task CloneAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, string predicate, object patch)
    {
        // Fixed test call sites only; no schema introspection or arbitrary database input.
        Assert.Contains(table, new[] { "KnowledgeOperations", "OctoberBusinessFinalizations", "OctoberFlowGroupVersions", "OctoberDocumentVersions", "OctoberFlowDocumentDependencies" });
        await using var command = new NpgsqlCommand($"INSERT INTO october.\"{table}\" SELECT (jsonb_populate_record(NULL::october.\"{table}\", to_jsonb(t)||@patch)).* FROM (SELECT * FROM october.\"{table}\" WHERE {predicate} LIMIT 1) t", connection, transaction);
        command.Parameters.AddWithValue("patch", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(patch));
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
    private static async Task ExecuteAsync(DisposablePostgreSqlDatabase database, string sql)
    { await using var connection = await database.OpenConnectionAsync(); await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
    private static async Task RejectAsync(DisposablePostgreSqlDatabase database, string sql, string expected)
    {
        await using var connection = await database.OpenConnectionAsync(); await using var command = new NpgsqlCommand(sql, connection);
        Assert.Equal(expected, (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
    }

    private sealed class FlowPgCase(DisposablePostgreSqlDatabase database, ResolutionPersistenceFixture basis, FlowFinalizationPublicationRequest request) : IAsyncDisposable
    {
        public DisposablePostgreSqlDatabase Database => database;
        public ResolutionPersistenceFixture Basis => basis;
        public FlowFinalizationPublicationRequest Request => request;
        public OctoberFlowPublicationStore Store => new(basis.Acceptance.Factory, basis.Authorization, basis.Acceptance.Guard, basis.Acceptance.Log);
        public async Task PublishAsync() => Assert.Equal(BusinessFinalizationStatus.Published, (await Store.PublishAsync(request)).Status);
        public static async Task<FlowPgCase> CreateAsync()
        {
            var database = await FreshAsync(); ResolutionPersistenceFixture? basis = null;
            try
            {
                basis = await ResolutionPersistenceFixture.CreateWithOptionsAsync(OctoberFlowHistoricalSeed.Options(database));
                Assert.Equal(KnowledgeWriteStatus.Committed, (await basis.Store().AcceptOutcomeAsync(basis.Request())).Status);
                await using var context = basis.Acceptance.Factory.CreateDbContext();
                var document = basis.Corpus.Documents[0].Version;
                var logical = await context.LogicalDocuments.SingleAsync(x => x.LogicalDocumentId == document.LogicalDocumentId);
                var unit = await context.CodeUnits.SingleAsync(x => x.UnitId == logical.UnitId);
                var root = new FlowRootSelection(unit.UnitId, document, FlowRootKind.SelectedCodeUnit, unit.SemanticSelector);
                var loaded = await new OctoberFlowPlanningInputLoader(basis.Acceptance.Factory, basis.Authorization).LoadAsync(basis.Corpus.Handle, [root]);
                Assert.Equal(KnowledgeReadStatus.Success, loaded.Status);
                var group = Assert.Single(OctoberFlowPlanner.Plan(loaded.Input!).Groups);
                var selection = new BusinessFinalizationSelection([root], new(), Guid.NewGuid(), "controlled-profile",
                    OctoberBusinessFinalizationPolicy.ComputeProviderFingerprint(FinalizationContractCase.Provider), OctoberKnowledgePolicy.Hash("Controlled PG synthesis"), 40);
                var operation = new OperationKey(document.BatchId, OperationKind.FinalizeCorpus, Guid.NewGuid());
                string digest = OctoberBusinessFinalizationPolicy.ComputeRequestDigest(new(operation, basis.Corpus.Handle, "", new FinalizationForbiddenScope()) { Selection = selection });
                var proposal = new BusinessSynthesisProposal("业务处理说明", [new("business-main", 0, "处理过程",
                    [new("business-action", "满足条件后转交后续处理。", ClaimQualification.SourceSupported, [new(root.Document, "operation", "action")], [])])]);
                var request = new FlowFinalizationPublicationRequest(operation, basis.Corpus.Handle, digest, selection, [new(root, group.GroupVersionId, proposal)], [], "");
                request = request with { CanonicalOutcomeDigest = OctoberBusinessFinalizationPolicy.ComputeOutcomeDigest(request) };
                return new(database, basis, request);
            }
            catch { if (basis is not null) await basis.DisposeAsync(); await database.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync() { await basis.DisposeAsync(); await database.DisposeAsync(); }
    }
}

/// <summary>Fixed B0/Dialogue fixture transfer only. The current-schema donor creates seed data; prior-schema insertion and target migration establish upgrade behavior.</summary>
internal sealed class OctoberFlowHistoricalSeed(DisposablePostgreSqlDatabase donor, DialoguePersistenceFixture fixture, DialogueTerminalSaveRequest? terminal) : IAsyncDisposable
{
    private static readonly string[] _baseTables = ["OctoberBatches", "OctoberRepositorySelections", "OctoberContextReferences", "OctoberAdvisoryEntries",
        "KnowledgeOperations", "AcceptedFileManifests", "OctoberCodeUnits", "OctoberLogicalDocuments", "OctoberDocumentVersions", "OctoberDocumentSections",
        "OctoberQualifiedClaimGroups", "KnowledgeContributors", "OctoberCommunicationObservations", "OctoberKnowledgeUncertainties", "OctoberCorpusSelections",
        "OctoberCorpusManifests", "OctoberCorpusDocuments", "OctoberCorpusCoverage", "OctoberKnowledgePublications", "OctoberPublicationMembers",
        "OctoberPublicationCoverage", "OctoberPublicationUncertainties"];
    private static readonly string[] _dialogueTables = ["OctoberDialogueConversations", "OctoberDialogueTurns", "OctoberDialogueTerminals", "OctoberDialogueReferences"];
    internal static readonly string[] ResolutionTables = ["OctoberResolutionItems", "OctoberRelationshipVersions", "OctoberUncertaintyVersions",
        "OctoberResolutionDocumentDependencies", "OctoberPublicationRelationships", "OctoberPublicationResolutionUncertainties"];
    public DialoguePersistenceFixture Fixture => fixture;
    public DialogueTerminalSaveRequest? Terminal => terminal;
    public IOctoberDialogueStore TargetStore(DisposablePostgreSqlDatabase target) => new OctoberDialogueStore(new KnowledgeContextFactory(Options(target)),
        fixture.Identity.Publication.Knowledge, fixture.Disclosure, fixture.Dependencies, fixture.Execution, fixture.Guard, new OctoberDialoguePayloadCodec(), fixture.Clock);
    public static DbContextOptions<OctoberDbContext> Options(DisposablePostgreSqlDatabase database)
    {
        TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
        return new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString, postgres => postgres.CommandTimeout(30)).Options;
    }
    public static async Task<OctoberFlowHistoricalSeed> CreateAsync(DisposablePostgreSqlDatabase target, string priorMigration, bool completedDialogue)
    {
        Assert.Contains(priorMigration, new[] { "20261006010000_AddOctoberKnowledgePublications", "20261007000000_AddOctoberDialogue", "20261007010000_AddOctoberResolutionEvidence" });
        await using (var old = new OctoberDbContext(Options(target))) await old.GetService<IMigrator>().MigrateAsync(priorMigration);
        var donor = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        DialoguePersistenceFixture? fixture = null;
        try
        {
            await using (var current = new OctoberDbContext(Options(donor))) await current.Database.MigrateAsync();
            fixture = await DialoguePersistenceFixture.CreateAsync(Options(donor));
            DialogueTerminalSaveRequest? terminal = null;
            if (completedDialogue)
            {
                var admission = await fixture.AdmitAsync(); terminal = fixture.TerminalRequest(admission);
                var reference = new OctoberChatReference(new(fixture.Document.BatchId, fixture.Identity.Publication.PublicationId,
                    fixture.Document.LogicalDocumentId, fixture.Document.DocumentVersionId, "overview"), true, 2, 345);
                var dependencies = fixture.Dependencies.Extend(terminal.InferenceDependencies!, "prior-exact-document-input");
                var proof = fixture.Dependencies.Consume(reference, dependencies);
                terminal = terminal with { InferenceDependencies = dependencies, References = [reference], ConsumedReads = [new(reference, proof)] };
                terminal = terminal with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(terminal) };
                await fixture.RequireSaveAsync(terminal);
            }
            await CopyAsync(donor, target, completedDialogue);
            return new(donor, fixture, terminal);
        }
        catch { if (fixture is not null) await fixture.DisposeAsync(); await donor.DisposeAsync(); throw; }
    }
    public static async Task CopyAsync(DisposablePostgreSqlDatabase donor, DisposablePostgreSqlDatabase target, bool dialogue,
        Action<string, JsonArray>? mutate = null, bool resolution = false)
    {
        TestResourceScope.ValidateOwnedTarget(donor.ConnectionString); TestResourceScope.ValidateOwnedTarget(target.ConnectionString);
        Assert.NotEqual(donor.ConnectionString, target.ConnectionString);
        await using var connection = await target.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
        foreach (string table in _baseTables.Concat(dialogue ? _dialogueTables : []).Concat(resolution ? ResolutionTables : []))
        {
            var rows = JsonNode.Parse(await RowsAsync(donor, table))!.AsArray();
            mutate?.Invoke(table, rows);
            await using var command = new NpgsqlCommand($"INSERT INTO october.\"{table}\" SELECT * FROM jsonb_populate_recordset(NULL::october.\"{table}\", @rows)", connection, transaction);
            command.Parameters.AddWithValue("rows", NpgsqlDbType.Jsonb, rows.ToJsonString());
            Assert.Equal(rows.Count, await command.ExecuteNonQueryAsync());
        }
        await transaction.CommitAsync();
    }
    public static async Task<Dictionary<string, string>> CaptureAsync(DisposablePostgreSqlDatabase target, bool dialogue, bool resolution = false)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string table in _baseTables.Concat(dialogue ? _dialogueTables : []).Concat(resolution ? ResolutionTables : [])) result.Add(table, await RowsAsync(target, table, priorColumns: true));
        return result;
    }
    public static async Task<string> RowsAsync(DisposablePostgreSqlDatabase database, string table, bool priorColumns = false)
    {
        Assert.True(_baseTables.Contains(table) || _dialogueTables.Contains(table) || ResolutionTables.Contains(table) || OctoberFlowPublicationPostgreSqlTests.FlowTables.Contains(table));
        string projection = priorColumns ? PriorProjection(table) : "to_jsonb(t)";
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(payload ORDER BY payload::text),'[]'::jsonb)::text FROM (SELECT {projection} AS payload FROM october.\"{table}\" t) q", connection);
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }
    public static string PriorProjection(string table) => table == "OctoberLogicalDocuments" ? "to_jsonb(t)-'LogicalFlowId'"
        : table == "OctoberDocumentVersions" ? "to_jsonb(t)-ARRAY['Kind','FlowGroupVersionId','FinalizationOperationKind','FinalizationOperationId']" : "to_jsonb(t)";
    public async ValueTask DisposeAsync() { await fixture.DisposeAsync(); await donor.DisposeAsync(); }
}

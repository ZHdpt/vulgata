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
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Real PostgreSQL transactions/constraints in launcher-owned databases; execution and dependency authority remain labelled controlled adapters.</summary>
public sealed class OctoberDialoguePostgreSqlTests
{
    private static readonly string[] _dialogueTables = ["OctoberDialogueConversations", "OctoberDialogueTurns", "OctoberDialogueTerminals", "OctoberDialogueReferences"];

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task MigrateAsync_PopulatedB0_PreservesPriorKnowledgeAndAddsEmptyDialogueWithoutPendingModelChangesAsync()
    {
        await using var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
        var options = Options(database);
        await using (var old = new OctoberDbContext(options))
            await old.GetService<IMigrator>().MigrateAsync("20261006010000_AddOctoberKnowledgePublications");
        await using var fixture = await DialoguePersistenceFixture.CreateAsync(options);
        var before = await CaptureExistingTablesAsync(database);
        Assert.NotEqual("[]", before["OctoberDocumentVersions"]);
        Assert.NotEqual("[]", before["OctoberKnowledgePublications"]);
        await using var current = new OctoberDbContext(options);
        var oldMigrations = (await current.Database.GetAppliedMigrationsAsync()).ToArray();
        await current.Database.MigrateAsync();
        foreach (var table in before) Assert.Equal(table.Value, await CaptureTableAsync(database, table.Key));
        var migrations = (await current.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(oldMigrations, migrations.Take(oldMigrations.Length));
        Assert.Contains("20261007000000_AddOctoberDialogue", migrations);
        Assert.False(current.Database.HasPendingModelChanges());
        foreach (var table in _dialogueTables) Assert.Equal("[]", await CaptureTableAsync(database, table));
        await fixture.RequireOpenAsync();
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(DialogueTurnState.Admitted)]
    [InlineData(DialogueTurnState.Running)]
    [InlineData(DialogueTurnState.Saving)]
    [InlineData(DialogueTurnState.SaveIndeterminate)]
    public async Task UniqueActiveSlot_AllFourNonterminalStates_RejectsSecondRawAdmissionAndStateOnlyReleaseAsync(DialogueTurnState state)
    {
        await using var owned = await OwnedDialogue.CreateAsync();
        var f = owned.Fixture;
        await f.RequireOpenAsync();
        var result = await f.Store.BeginTurnAsync(f.BeginRequest(), f.Subject);
        Assert.Equal(DialogueStatus.Succeeded, result.Status);
        var admission = result.Admission!;
        await f.MoveAsync(admission, state);
        const string duplicate = """
            INSERT INTO october."OctoberDialogueTurns" ("ConversationId","TurnId","ClientRequestKey","Sequence","Fence","OwnerGeneration","AttemptId","RequestDigest","BeginEnvelopeJson","AdmittedAtUtc","DeadlineUtc","State")
            SELECT "ConversationId",@newTurn,@newKey,"Sequence"+1,"Fence"+1,"OwnerGeneration","AttemptId","RequestDigest","BeginEnvelopeJson","AdmittedAtUtc","DeadlineUtc",0
            FROM october."OctoberDialogueTurns" WHERE "TurnId"=@turn;
            """;
        await AssertRejectedAsync(owned.Database, duplicate, PostgresErrorCodes.UniqueViolation,
            ("newTurn", Guid.NewGuid()), ("newKey", Guid.NewGuid()), ("turn", admission.TurnId));
        await AssertRejectedAsync(owned.Database,
            "UPDATE october.\"OctoberDialogueTurns\" SET \"State\"=4 WHERE \"TurnId\"=@turn", PostgresErrorCodes.CheckViolation, ("turn", admission.TurnId));
        await using var context = f.Factory.CreateDbContext();
        Assert.Equal(state, (await context.DialogueTurns.SingleAsync()).State);
        Assert.Empty(await context.DialogueTerminals.ToArrayAsync());
        Assert.Equal(admission.TurnId, (await f.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BeginTurnAsync_ConcurrentRequests_AllocateOneSequenceAndNeverRedispatchReplayAsync(bool identicalKey)
    {
        await using var owned = await OwnedDialogue.CreateAsync();
        var f = owned.Fixture;
        await f.RequireOpenAsync();
        var first = f.BeginRequest();
        var second = identicalKey ? first : f.BeginRequest(Guid.NewGuid());
        var results = await Task.WhenAll(f.Store.BeginTurnAsync(first, f.Subject), f.Store.BeginTurnAsync(second, f.Subject));
        Assert.Single(results.Where(x => x.Status == DialogueStatus.Succeeded && x.MayDispatch));
        var other = Assert.Single(results.Where(x => !x.MayDispatch));
        Assert.Equal(identicalKey ? DialogueStatus.Existing : DialogueStatus.Busy, other.Status);
        await using var context = f.Factory.CreateDbContext();
        Assert.Equal(1, await context.DialogueTurns.CountAsync());
        var conversation = await context.DialogueConversations.SingleAsync();
        Assert.Equal(2, conversation.NextTurnSequence);
        var winner = results.Single(x => x.MayDispatch).Admission!;
        var lookup = await f.Store.LookupAdmissionAsync(new(f.Identity.ConversationId, f.Identity.ContextId, winner.Request.ClientRequestKey, winner.Request.RequestDigest), f.Subject);
        Assert.Equal(DialogueStatus.Existing, lookup.Status);
        Assert.False(lookup.MayDispatch);
        Assert.Equal(winner.TurnId, lookup.Admission!.TurnId);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveTerminalAsync_ConcurrentIdenticalOrConflictingOutcomes_PersistOneImmutableAtomicWinnerAsync(bool conflict)
    {
        await using var owned = await OwnedDialogue.CreateAsync();
        var f = owned.Fixture;
        var admission = await f.AdmitAsync();
        var first = WithReference(f, f.TerminalRequest(admission));
        var second = conflict ? first with { AnswerText = "A different terminal answer." } : first;
        second = second with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(second) };
        var results = await Task.WhenAll(f.Store.SaveTerminalAsync(first, f.Subject), f.Store.SaveTerminalAsync(second, f.Subject));
        Assert.Single(results.Where(x => x.Status == DialogueStatus.Succeeded));
        Assert.Single(results.Where(x => x.Status == (conflict ? DialogueStatus.Conflict : DialogueStatus.Existing)));
        await using var context = f.Factory.CreateDbContext();
        var terminal = await context.DialogueTerminals.SingleAsync();
        Assert.Equal(1, await context.DialogueReferences.CountAsync());
        Assert.Equal(DialogueTurnState.Completed, (await context.DialogueTurns.SingleAsync()).State);
        Assert.Equal(results.Single(x => x.Status == DialogueStatus.Succeeded).Receipt!.OutcomeDigest, terminal.OutcomeDigest);
        Assert.Null((await f.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task SaveTerminalAsync_JsonbRoundTrip_RetainsOpaqueHostCapsulesProofFactsAndExactUnicodeWithoutAuthenticationBindingAsync()
    {
        await using var owned = await OwnedDialogue.CreateAsync();
        var f = owned.Fixture;
        var admission = await f.AdmitAsync();
        var request = WithReference(f, f.TerminalRequest(admission));
        await f.RequireSaveAsync(request);
        await using var context = f.Factory.CreateDbContext();
        var turn = await context.DialogueTurns.SingleAsync();
        var terminal = await context.DialogueTerminals.SingleAsync();
        using var beginJson = JsonDocument.Parse(turn.BeginEnvelopeJson);
        using var terminalJson = JsonDocument.Parse(terminal.TerminalEnvelopeJson);
        using var consumedJson = JsonDocument.Parse(terminal.VerifiedConsumedJson);
        Assert.Equal(admission.Request.InitialDependencies.ProtectedPayload, beginJson.RootElement.GetProperty("initialDependencies").GetProperty("protectedPayload").GetString());
        Assert.Equal(request.InferenceDependencies!.ProtectedPayload, terminalJson.RootElement.GetProperty("inferenceDependencies").GetProperty("protectedPayload").GetString());
        Assert.Equal(request.ConsumedReads[0].Proof.ProtectedPayload, terminalJson.RootElement.GetProperty("consumedReads")[0].GetProperty("proof").GetProperty("protectedPayload").GetString());
        Assert.Equal(request.AnswerText, terminalJson.RootElement.GetProperty("answerText").GetString());
        Assert.Equal(JsonValueKind.Array, consumedJson.RootElement.ValueKind);
        Assert.Equal(1, consumedJson.RootElement.GetArrayLength());
        Assert.Contains("Controlled incomplete source coverage", terminal.VerifiedConsumedJson, StringComparison.Ordinal);
        // Full inference is persisted once; the proof facts do not duplicate the whole dependency capsule.
        Assert.DoesNotContain(request.InferenceDependencies.ProtectedPayload, terminal.VerifiedConsumedJson, StringComparison.Ordinal);
        foreach (var table in _dialogueTables) Assert.DoesNotContain(f.Subject.AuthenticationBinding, await CaptureTableAsync(owned.Database, table), StringComparison.Ordinal);
        var lookup = await f.Store.LookupTerminalAsync(new(f.Identity.ConversationId, f.Identity.ContextId, admission.TurnId, request.SaveOperationId, admission.Fence, request.OutcomeDigest), f.Subject);
        Assert.Equal(request.AnswerText, lookup.Projection!.AnswerText);
        Assert.Equal(request.References.ToArray(), lookup.Projection.References.ToArray());
        var history = await f.Store.ReadHistoryAsync(f.HistoryRequest, f.Subject);
        Assert.Equal(request.AnswerText, Assert.Single(history.Turns).AnswerText);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ImmutableHistory_RawIdentityOutcomeReferenceUpdatesDeletesAndLateChildrenAreRejectedAsync()
    {
        await using var owned = await OwnedDialogue.CreateAsync();
        var f = owned.Fixture;
        var admission = await f.AdmitAsync();
        await f.RequireSaveAsync(WithReference(f, f.TerminalRequest(admission)));
        var before = new Dictionary<string, string>();
        foreach (var table in _dialogueTables) before.Add(table, await CaptureTableAsync(owned.Database, table));
        await AssertRejectedAsync(owned.Database, "UPDATE october.\"OctoberDialogueConversations\" SET \"OwnerSubjectId\"='foreign-owner'", PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(owned.Database, "UPDATE october.\"OctoberDialogueTurns\" SET \"BeginEnvelopeJson\"='{}'::jsonb", PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(owned.Database, "UPDATE october.\"OctoberDialogueTerminals\" SET \"TerminalEnvelopeJson\"='{}'::jsonb", PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(owned.Database, "UPDATE october.\"OctoberDialogueReferences\" SET \"SectionId\"=NULL", PostgresErrorCodes.CheckViolation);
        foreach (var table in _dialogueTables)
            await AssertRejectedAsync(owned.Database, $"DELETE FROM october.\"{table}\"", PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(owned.Database, "INSERT INTO october.\"OctoberDialogueReferences\" SELECT \"ConversationId\",\"TurnId\",\"Ordinal\"+1,\"BatchId\",\"PublicationId\",\"LogicalDocumentId\",\"VersionId\",\"SectionId\" FROM october.\"OctoberDialogueReferences\"", PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(owned.Database, "TRUNCATE october.\"OctoberDialogueReferences\", october.\"OctoberDialogueTerminals\", october.\"OctoberDialogueTurns\", october.\"OctoberDialogueConversations\"", PostgresErrorCodes.CheckViolation);
        foreach (var table in before) Assert.Equal(table.Value, await CaptureTableAsync(owned.Database, table.Key));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawNewTerminalReference_RequiresExactPublishedVersionAndExactSectionForeignKeysAsync(bool invalidSection)
    {
        await using var owned = await OwnedDialogue.CreateAsync();
        var f = owned.Fixture;
        var admission = await f.AdmitAsync();
        var terminal = WithReference(f, f.TerminalRequest(admission));
        await f.RequireSaveAsync(terminal);
        // A fresh turn/terminal/reference transaction tests relational constraints independently of store validation.
        const string sql = """
            INSERT INTO october."OctoberDialogueTurns" ("ConversationId","TurnId","ClientRequestKey","Sequence","Fence","OwnerGeneration","AttemptId","RequestDigest","BeginEnvelopeJson","AdmittedAtUtc","DeadlineUtc","State")
            SELECT "ConversationId",@turn,@key,"Sequence"+1,"Fence"+1,"OwnerGeneration","AttemptId","RequestDigest","BeginEnvelopeJson","AdmittedAtUtc","DeadlineUtc",4 FROM october."OctoberDialogueTurns" WHERE "TurnId"=@oldTurn;
            INSERT INTO october."OctoberDialogueTerminals" ("ConversationId","TurnId","BatchId","PublicationId","SaveOperationId","OutcomeDigest","TerminalState","Reason","Fence","CommittedAtUtc","TerminalEnvelopeJson","VerifiedConsumedJson")
            SELECT "ConversationId",@turn,"BatchId","PublicationId",@operation,"OutcomeDigest",0,0,"Fence"+1,"CommittedAtUtc","TerminalEnvelopeJson","VerifiedConsumedJson" FROM october."OctoberDialogueTerminals" WHERE "TurnId"=@oldTurn;
            INSERT INTO october."OctoberDialogueReferences" ("ConversationId","TurnId","Ordinal","BatchId","PublicationId","LogicalDocumentId","VersionId","SectionId")
            SELECT "ConversationId",@turn,0,"BatchId","PublicationId","LogicalDocumentId",@version,@section FROM october."OctoberDialogueReferences" WHERE "TurnId"=@oldTurn;
            """;
        await AssertRejectedAsync(owned.Database, sql, PostgresErrorCodes.ForeignKeyViolation,
            ("turn", Guid.NewGuid()), ("key", Guid.NewGuid()), ("operation", Guid.NewGuid()), ("oldTurn", admission.TurnId),
            ("version", invalidSection ? f.Document.DocumentVersionId : Guid.NewGuid()), ("section", invalidSection ? "missing-section" : "overview"));
        await using var context = f.Factory.CreateDbContext();
        Assert.Equal(1, await context.DialogueTurns.CountAsync());
        Assert.Equal(1, await context.DialogueTerminals.CountAsync());
        Assert.Equal(1, await context.DialogueReferences.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task SaveTerminalAsync_ReferenceInsertFailure_RollsBackOutcomeAndSlotThenStorageRetrySucceedsAsync()
    {
        await using var owned = await OwnedDialogue.CreateAsync();
        var f = owned.Fixture;
        var admission = await f.AdmitAsync();
        var request = WithReference(f, f.TerminalRequest(admission));
        await ExecuteAsync(owned.Database, "CREATE FUNCTION october.reject_dialogue_reference() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'controlled reference failure'; END $$; CREATE TRIGGER reject_dialogue_reference BEFORE INSERT ON october.\"OctoberDialogueReferences\" FOR EACH ROW EXECUTE FUNCTION october.reject_dialogue_reference();");
        var failed = await f.Store.SaveTerminalAsync(request, f.Subject);
        Assert.NotEqual(DialogueStatus.Succeeded, failed.Status);
        await using (var context = f.Factory.CreateDbContext())
        {
            Assert.Empty(await context.DialogueTerminals.ToArrayAsync());
            Assert.Empty(await context.DialogueReferences.ToArrayAsync());
            Assert.Equal(DialogueTurnState.Running, (await context.DialogueTurns.SingleAsync()).State);
        }
        Assert.Equal(admission.TurnId, (await f.OpenAsync()).Conversation!.ActiveTurnId);
        await ExecuteAsync(owned.Database, "DROP TRIGGER reject_dialogue_reference ON october.\"OctoberDialogueReferences\"; DROP FUNCTION october.reject_dialogue_reference();");
        await f.RequireSaveAsync(request);
        Assert.Null((await f.OpenAsync()).Conversation!.ActiveTurnId);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task SaveTerminalAsync_CommitAcknowledgementLost_ExactLookupRecoversWithoutDuplicateOutcomeAsync()
    {
        await using var owned = await OwnedDialogue.CreateAsync();
        var f = owned.Fixture;
        var admission = await f.AdmitAsync();
        var request = WithReference(f, f.TerminalRequest(admission));
        var guard = new DialogueAcknowledgementLossGuard();
        var store = Store(f, guard);
        var result = await store.SaveTerminalAsync(request, f.Subject);
        Assert.Contains(result.Status, new[] { DialogueStatus.Succeeded, DialogueStatus.Existing, DialogueStatus.Indeterminate });
        var lookup = await store.LookupTerminalAsync(new(f.Identity.ConversationId, f.Identity.ContextId, admission.TurnId, request.SaveOperationId, admission.Fence, request.OutcomeDigest), f.Subject);
        Assert.Equal(DialogueStatus.Existing, lookup.Status);
        Assert.Equal(request.OutcomeDigest, lookup.Receipt!.OutcomeDigest);
        Assert.Equal(DialogueStatus.Existing, (await store.SaveTerminalAsync(request, f.Subject)).Status);
        Assert.Equal(1, guard.Submissions);
        await using var context = f.Factory.CreateDbContext();
        Assert.Equal(1, await context.DialogueTerminals.CountAsync());
        Assert.Equal(1, await context.DialogueReferences.CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task SaveTerminalAsync_ServerCommitPending_AbsentLookupIsIndeterminateAndRacingOrphanPreservesCommittedWinnerAsync()
    {
        await using var owned = await OwnedDialogue.CreateAsync();
        var f = owned.Fixture;
        var admission = await f.AdmitAsync();
        var request = WithReference(f, f.TerminalRequest(admission));
        await using var blocker = await HoldCommitAsync(owned.Database);
        var pending = f.Store.SaveTerminalAsync(request, f.Subject);
        Task<DialogueOrphanResult>? orphan = null;
        try
        {
            await WaitForCommitLockAsync(blocker);
            Assert.False(pending.IsCompleted);
            var lookup = await f.Store.LookupTerminalAsync(new(f.Identity.ConversationId, f.Identity.ContextId, admission.TurnId, request.SaveOperationId, admission.Fence, request.OutcomeDigest), f.Subject);
            Assert.Equal(DialogueStatus.Indeterminate, lookup.Status);
            Assert.Null(lookup.Receipt);
            await using (var context = f.Factory.CreateDbContext())
            {
                Assert.Empty(await context.DialogueTerminals.ToArrayAsync());
                Assert.Empty(await context.DialogueReferences.ToArrayAsync());
                Assert.Equal(DialogueTurnState.Running, (await context.DialogueTurns.SingleAsync()).State);
            }
            f.Execution.OwnerDeathStatus = DialogueExecutionStatus.Verified;
            orphan = f.Store.InterruptOrphanAsync(new(Guid.NewGuid(), f.Identity.ConversationId, f.Identity.ContextId, admission.TurnId, admission.Request.Execution, admission.Fence));
            Assert.False(orphan.IsCompleted);
        }
        finally
        {
            await ReleaseCommitAsync(blocker);
            await pending.WaitAsync(TimeSpan.FromSeconds(15));
            if (orphan is not null) await orphan.WaitAsync(TimeSpan.FromSeconds(15));
        }
        var committed = await pending;
        Assert.Equal(DialogueStatus.Succeeded, committed.Status);
        Assert.NotNull(orphan);
        var reconciled = await orphan;
        Assert.Equal(DialogueStatus.Existing, reconciled.Status);
        Assert.Equal(DialogueTerminalState.Completed, reconciled.Receipt!.TerminalState);
        Assert.Equal(request.SaveOperationId, reconciled.Receipt.SaveOperationId);
        await using var final = f.Factory.CreateDbContext();
        Assert.Equal(1, await final.DialogueTerminals.CountAsync());
        Assert.Equal(1, await final.DialogueReferences.CountAsync());
        Assert.Null((await f.OpenAsync()).Conversation!.ActiveTurnId);
    }

    private static DialogueTerminalSaveRequest WithReference(DialoguePersistenceFixture f, DialogueTerminalSaveRequest request)
    {
        var reference = new OctoberChatReference(new(f.Identity.Publication.Knowledge.BatchId, f.Identity.Publication.PublicationId, f.Document.LogicalDocumentId, f.Document.DocumentVersionId, "overview"), true, 2, 345);
        var dependencies = f.Dependencies.Extend(request.InferenceDependencies!, "exact-document-input");
        var proof = f.Dependencies.Consume(reference, dependencies);
        request = request with { InferenceDependencies = dependencies, References = [reference], ConsumedReads = [new(reference, proof)] };
        return request with { OutcomeDigest = DialogueTestProtocol.OutcomeDigest(request) };
    }
    private static IOctoberDialogueStore Store(DialoguePersistenceFixture f, IOctoberDialogueCommitGuard guard) =>
        new OctoberDialogueStore(f.Factory, f.Identity.Publication.Knowledge, f.Disclosure, f.Dependencies, f.Execution, guard, new OctoberDialoguePayloadCodec(), f.Clock);
    private static DbContextOptions<OctoberDbContext> Options(DisposablePostgreSqlDatabase database)
    {
        TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
        return new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString, options => options.CommandTimeout(30)).Options;
    }
    private static async Task ExecuteAsync(DisposablePostgreSqlDatabase database, string sql)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
    private static async Task AssertRejectedAsync(DisposablePostgreSqlDatabase database, string sql, string state, params (string Name, object Value)[] parameters)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        var rejected = await Assert.ThrowsAsync<PostgresException>(async () => { await command.ExecuteNonQueryAsync(); await transaction.CommitAsync(); });
        Assert.Equal(state, rejected.SqlState);
    }
    private static async Task<Dictionary<string, string>> CaptureExistingTablesAsync(DisposablePostgreSqlDatabase database)
    {
        var tables = new List<string>();
        await using (var connection = await database.OpenConnectionAsync())
        {
            await using var command = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='october' AND tablename <> '__OctoberMigrationsHistory' ORDER BY tablename", connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        }
        var result = new Dictionary<string, string>();
        foreach (var table in tables) result.Add(table, await CaptureTableAsync(database, table));
        return result;
    }
    private static async Task<string> CaptureTableAsync(DisposablePostgreSqlDatabase database, string table)
    {
        await using var connection = await database.OpenConnectionAsync();
        string identifier = table.Replace("\"", "\"\"", StringComparison.Ordinal);
        await using var command = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text), '[]'::jsonb)::text FROM october.\"{identifier}\" t", connection);
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }
    private static async Task<NpgsqlConnection> HoldCommitAsync(DisposablePostgreSqlDatabase database)
    {
        var blocker = await database.OpenConnectionAsync();
        try
        {
            await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(763203, 1)", blocker)) await acquire.ExecuteNonQueryAsync();
            await using var create = new NpgsqlCommand("CREATE FUNCTION october.hold_dialogue_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN PERFORM pg_advisory_xact_lock(763203, 1); RETURN NEW; END $$; CREATE CONSTRAINT TRIGGER hold_dialogue_commit AFTER INSERT ON october.\"OctoberDialogueTerminals\" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION october.hold_dialogue_commit();", blocker);
            await create.ExecuteNonQueryAsync();
            return blocker;
        }
        catch { await blocker.DisposeAsync(); throw; }
    }
    private static async Task ReleaseCommitAsync(NpgsqlConnection blocker)
    {
        await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(763203, 1)", blocker);
        Assert.True(Assert.IsType<bool>(await release.ExecuteScalarAsync()));
    }
    private static async Task WaitForCommitLockAsync(NpgsqlConnection observer)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            await using var query = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype='advisory' AND NOT granted AND classid=763203 AND objid=1 AND database=(SELECT oid FROM pg_database WHERE datname=current_database()))", observer);
            if (await query.ExecuteScalarAsync() is true) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("The owned dialogue transaction did not reach its deferred COMMIT lock.");
    }
    private sealed class OwnedDialogue(DisposablePostgreSqlDatabase database, DialoguePersistenceFixture fixture) : IAsyncDisposable
    {
        public DisposablePostgreSqlDatabase Database => database;
        public DialoguePersistenceFixture Fixture => fixture;
        public static async Task<OwnedDialogue> CreateAsync()
        {
            var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
            try
            {
                var options = Options(database);
                await using (var context = new OctoberDbContext(options)) await context.Database.MigrateAsync();
                return new(database, await DialoguePersistenceFixture.CreateAsync(options));
            }
            catch { await database.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync() { await Fixture.DisposeAsync(); await Database.DisposeAsync(); }
    }
    private sealed class DialogueAcknowledgementLossGuard : IOctoberDialogueCommitGuard
    {
        public int Submissions { get; private set; }
        public Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(DialogueCommitAttempt attempt, CancellationToken cancellationToken = default) => Task.FromResult(CommitEligibilityStatus.Eligible);
        public async Task<CommitGuardResult> SubmitAsync(DialogueCommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default)
        {
            Submissions++;
            var submitted = submitAsync(cancellationToken);
            Assert.Equal(CommitSettlement.Committed, await submitted.Settlement);
            throw new InvalidOperationException("Controlled acknowledgement lost after actual server COMMIT.");
        }
    }
}


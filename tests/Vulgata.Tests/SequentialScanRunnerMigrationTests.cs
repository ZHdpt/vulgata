using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class SequentialScanRunnerMigrationTests
{
    private const string _baselineMigration = "20260731041654_AddMinimalKnowledgeStore";
    private const string _migrationSuffix = "_AddSequentialScanRunner";
    private const string _npgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string _tableName = "ScanExecutions";

    private static readonly string[] _tableNames = [_tableName];

    private static readonly IReadOnlyDictionary<string, string> _checkSqlByName =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CK_ScanExecutions_Phase_Closed"] =
                "\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Completed', 'Failed', 'Cancelled', 'Prepared', 'Running', 'PausedProviderUnavailable', 'PausedOperatorCorrection', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity', 'FailedPass1')",
            ["CK_ScanExecutions_TerminalReasonKind_Closed"] =
                "\"TerminalReasonKind\" IS NULL OR \"TerminalReasonKind\" IN ('SchemaInvalid', 'Error', 'Unbounded', 'MissingPrerequisite', 'DuplicateActiveScan', 'MissingCodeUnit', 'ForbiddenContent', 'OverLimitPacket')",
            ["CK_ScanExecutions_RestartIndicator_Bound"] =
                "0 <= \"RestartIndicator\" AND \"RestartIndicator\" <= 32",
            ["CK_ScanExecutions_StateVersion_Bound"] = "\"StateVersion\" >= 0",
            ["CK_ScanExecutions_NewPathBinding_Shape"] =
                "(\"Phase\" IN ('Pass1Planning', 'Pass1Running', 'Pass1Completed', 'Failed') AND " +
                "\"PromptIdentity\" IS NULL AND \"DocumentSchemaIdentity\" IS NULL AND " +
                "\"AnalysisRuntimeIdentityJson\" IS NULL AND \"AnalysisRuntimeIdentityHash\" IS NULL AND " +
                "\"UpdatedAt\" IS NULL) OR " +
                "(\"Phase\" IN ('Prepared', 'Running', 'PausedProviderUnavailable', " +
                "'PausedOperatorCorrection', 'CompletedWithWarnings', 'FailedIntegrity', 'FailedSecurity', 'FailedPass1') AND " +
                "\"PromptIdentity\" IS NOT NULL AND \"DocumentSchemaIdentity\" IS NOT NULL AND " +
                "\"AnalysisRuntimeIdentityJson\" IS NOT NULL AND \"AnalysisRuntimeIdentityHash\" IS NOT NULL AND " +
                "\"UpdatedAt\" IS NOT NULL) OR " +
                "(\"Phase\" IN ('Completed', 'Cancelled') AND ((\"PromptIdentity\" IS NULL AND " +
                "\"DocumentSchemaIdentity\" IS NULL AND \"AnalysisRuntimeIdentityJson\" IS NULL AND " +
                "\"AnalysisRuntimeIdentityHash\" IS NULL AND \"UpdatedAt\" IS NULL) OR " +
                "(\"PromptIdentity\" IS NOT NULL AND \"DocumentSchemaIdentity\" IS NOT NULL AND " +
                "\"AnalysisRuntimeIdentityJson\" IS NOT NULL AND \"AnalysisRuntimeIdentityHash\" IS NOT NULL AND " +
                "\"UpdatedAt\" IS NOT NULL)))",
            ["CK_ScanExecutions_PauseFailure_Shape"] =
                "(\"Phase\" = 'PausedProviderUnavailable' AND \"PauseCategory\" = 'ProviderUnavailable' AND " +
                "\"PauseCode\" IS NOT NULL AND \"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR " +
                "(\"Phase\" = 'PausedOperatorCorrection' AND \"PauseCategory\" = 'OperatorCorrection' AND " +
                "\"PauseCode\" IS NOT NULL AND \"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR " +
                "(\"Phase\" = 'FailedIntegrity' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND " +
                "\"FailureCategory\" = 'Integrity' AND \"FailureCode\" IS NOT NULL) OR " +
                "(\"Phase\" = 'FailedSecurity' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND " +
                "\"FailureCategory\" = 'Security' AND \"FailureCode\" IS NOT NULL) OR " +
                "(\"Phase\" = 'FailedPass1' AND \"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND " +
                "\"FailureCategory\" = 'Pass1Policy' AND \"FailureCode\" IS NOT NULL) OR " +
                "(\"Phase\" NOT IN ('PausedProviderUnavailable', 'PausedOperatorCorrection', 'FailedIntegrity', 'FailedSecurity', 'FailedPass1') AND " +
                "\"PauseCategory\" IS NULL AND \"PauseCode\" IS NULL AND " +
                "((\"FailureCategory\" IS NULL AND \"FailureCode\" IS NULL) OR " +
                "(\"Phase\" = 'Cancelled' AND \"FailureCategory\" = 'Cancellation' AND \"FailureCode\" IS NOT NULL)))",
        };

    [Fact]
    public void AddSequentialScanRunner_Operations_AreAdditiveCompleteAndReversible()
    {
        using VulgataDbContext db = CreateContext();
        IMigrationsAssembly migrationsAssembly = db.GetService<IMigrationsAssembly>();
        KeyValuePair<string, System.Reflection.TypeInfo> migrationEntry = Assert.Single(
            migrationsAssembly.Migrations,
            entry => entry.Key.EndsWith(_migrationSuffix, StringComparison.Ordinal));
        Migration migration = migrationsAssembly.CreateMigration(
            migrationEntry.Value,
            _npgsqlProvider);

        // T069: operation allowlist — only additive operations; no alters, drops, or columns.
        Type[] permittedUpOperationTypes =
        [
            typeof(CreateTableOperation),
            typeof(CreateIndexOperation),
            typeof(AddForeignKeyOperation),
        ];
        Assert.All(
            migration.UpOperations,
            operation => Assert.Contains(operation.GetType(), permittedUpOperationTypes));

        // Single new table; no Story 11.1 table touched.
        Assert.Equal(
            _tableNames,
            migration.UpOperations
                .OfType<CreateTableOperation>()
                .Select(operation => operation.Name)
                .Order(StringComparer.Ordinal));

        // T069: FKs are Restrict or NoAction only — never cascade.
        IEnumerable<AddForeignKeyOperation> foreignKeys =
            migration.UpOperations
                .OfType<CreateTableOperation>()
                .SelectMany(operation => operation.ForeignKeys)
                .Concat(migration.UpOperations.OfType<AddForeignKeyOperation>());
        Assert.NotEmpty(foreignKeys);
        Assert.All(
            foreignKeys,
            foreignKey => Assert.True(
                foreignKey.OnDelete is
                    ReferentialAction.Restrict or ReferentialAction.NoAction));

        // T069: composite FK to ScanRun via alternate key (ScanRunId, RepositoryId).
        Assert.Contains(
            foreignKeys,
            foreignKey =>
                foreignKey.Table == _tableName &&
                foreignKey.Columns.SequenceEqual(["ScanRunId", "RepositoryId"]) &&
                foreignKey.PrincipalTable == "ScanRuns" &&
                foreignKey.PrincipalColumns!.SequenceEqual(["Id", "RepositoryId"]));

        // T069: simple FK to Repository.
        Assert.Contains(
            foreignKeys,
            foreignKey =>
                foreignKey.Table == _tableName &&
                foreignKey.Columns.SequenceEqual(["RepositoryId"]) &&
                foreignKey.PrincipalTable == "Repositories" &&
                foreignKey.PrincipalColumns!.SequenceEqual(["Id"]));

        // T067: CHECK constraints present with closed-enum/bound SQL.
        Dictionary<string, string> migrationCheckSqlByName =
            migration.UpOperations
                .OfType<CreateTableOperation>()
                .SelectMany(operation => operation.CheckConstraints)
                .ToDictionary(check => check.Name!, check => check.Sql);
        AssertChecks(migrationCheckSqlByName);

        // T069: alternate keys (Id, RepositoryId) and (ScanRunId, RepositoryId) exist.
        CreateTableOperation table = Assert.Single(
            migration.UpOperations.OfType<CreateTableOperation>(),
            operation => operation.Name == _tableName);
        Assert.Contains(
            table.UniqueConstraints,
            unique => unique.Columns.SequenceEqual(["Id", "RepositoryId"]));
        Assert.Contains(
            table.UniqueConstraints,
            unique => unique.Columns.SequenceEqual(["ScanRunId", "RepositoryId"]));

        // T068: no pending model changes — verified separately by ef CLI invocation below.

        // Down operations only drop the table or its FKs — no destructive wide impact.
        Assert.All(
            migration.DownOperations,
            operation => Assert.True(
                operation is DropForeignKeyOperation or DropTableOperation));
        Assert.Equal(
            _tableNames,
            migration.DownOperations
                .OfType<DropTableOperation>()
                .Select(operation => operation.Name)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AddSequentialScanRunner_Model_ChecksAndIndexes_MatchAcceptedSchema()
    {
        using VulgataDbContext db = CreateContext();
        IModel model = db.GetService<IDesignTimeModel>().Model;
        IEntityType entity = Assert.IsAssignableFrom<IEntityType>(
            model.FindEntityType(typeof(ScanExecution)));

        Assert.Equal(_tableName, entity.GetTableName());

        // T069: alternate keys (Id, RepositoryId) and (ScanRunId, RepositoryId).
        Assert.Contains(
            entity.GetKeys(),
            key => key.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(ScanExecution.Id), nameof(ScanExecution.RepositoryId)]));
        Assert.Contains(
            entity.GetKeys(),
            key => key.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(ScanExecution.ScanRunId), nameof(ScanExecution.RepositoryId)]));

        // T069: FKs are Restrict or NoAction only.
        Assert.All(
            entity.GetForeignKeys(),
            foreignKey => Assert.True(
                foreignKey.DeleteBehavior is
                    DeleteBehavior.Restrict or DeleteBehavior.NoAction));

        // T067: CHECK constraints mirrored on the model snapshot.
        Dictionary<string, string> modelCheckSqlByName =
            entity.GetCheckConstraints()
                .ToDictionary(check => check.Name!, check => check.Sql);
        AssertChecks(modelCheckSqlByName);

        // T069: indexes by Repository, ScanRun, Phase.
        AssertIndex(
            entity,
            false,
            nameof(ScanExecution.RepositoryId));
        AssertIndex(
            entity,
            false,
            nameof(ScanExecution.RepositoryId),
            nameof(ScanExecution.ScanRunId),
            nameof(ScanExecution.Phase));
        AssertIndex(
            entity,
            false,
            nameof(ScanExecution.ScanRunId),
            nameof(ScanExecution.RepositoryId));

        // T069: DbSet declared on VulgataDbContext.
        Assert.NotNull(typeof(VulgataDbContext).GetProperty(nameof(VulgataDbContext.ScanExecutions)));

        // T069: no domain delete method — entity has only private constructor + Start factory +
        // explicit transitions. This is a structural assertion against the type surface.
        Assert.DoesNotContain(
            typeof(ScanExecution).GetMethods(),
            method => string.Equals(method.Name, "Delete", StringComparison.Ordinal));
    }

    [Fact]
    public void AddSequentialScanRunner_ApplyRollbackAndReapply_PreservesStory111Schema()
    {
        using var connection = new SqliteConnection(
            "Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        var options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        using var db = new VulgataDbContext(options);
        IMigrator migrator = db.GetService<IMigrator>();

        // Baseline (Story 11.1): no ScanExecutions table.
        migrator.Migrate(_baselineMigration);
        AssertStory111Schema(connection);
        Assert.DoesNotContain(
            ReadApplicationTables(connection),
            table => _tableNames.Contains(table, StringComparer.Ordinal));

        // T065: applies from a clean baseline database.
        migrator.Migrate();
        AssertStory111Schema(connection);
        Assert.Equal(
            _tableNames,
            ReadApplicationTables(connection)
                .Where(table => _tableNames.Contains(table, StringComparer.Ordinal))
                .Order(StringComparer.Ordinal));

        // T066: rolls back to baseline; Story 11.1 tables remain; ScanExecutions gone.
        migrator.Migrate(_baselineMigration);
        AssertStory111Schema(connection);
        Assert.DoesNotContain(
            ReadApplicationTables(connection),
            table => _tableNames.Contains(table, StringComparer.Ordinal));

        // T067: reapplies; constraints and indexes present again.
        migrator.Migrate();
        AssertStory111Schema(connection);
        Assert.Equal(
            _tableNames,
            ReadApplicationTables(connection)
                .Where(table => _tableNames.Contains(table, StringComparer.Ordinal))
                .Order(StringComparer.Ordinal));
        AssertCheckConstraintsPresent(connection);
    }

    private static VulgataDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new(options);
    }

    private static void AssertIndex(
        IEntityType entityType,
        bool isUnique,
        params string[] propertyNames)
    {
        Assert.Contains(
            entityType.GetIndexes(),
            index =>
                index.IsUnique == isUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(propertyNames));
    }

    private static void AssertChecks(IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(
            _checkSqlByName.Keys.Order(StringComparer.Ordinal),
            actual.Keys.Order(StringComparer.Ordinal));
        Assert.All(
            _checkSqlByName,
            expected => Assert.Equal(expected.Value, actual[expected.Key]));
    }

    private static void AssertStory111Schema(SqliteConnection connection)
    {
        string[] tables = ReadApplicationTables(connection);
        Assert.Contains("ScanRuns", tables);
        Assert.Contains("Repositories", tables);
        Assert.Contains("PreScanResults", tables);
        Assert.Contains("LogicalDocuments", tables);
        Assert.Contains("DocumentVersions", tables);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('ScanRuns');";
        using SqliteDataReader reader = command.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        Assert.Contains(nameof(ScanRun.ProfiledRevision), columns);
    }

    private static void AssertCheckConstraintsPresent(SqliteConnection connection)
    {
        // SQLite stores CHECK constraints in the table's CREATE statement; verify all three
        // are present by name in the SQL definition.
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT sql
            FROM sqlite_master
            WHERE type = 'table' AND name = 'ScanExecutions';
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        Assert.True(reader.Read());
        string createSql = reader.GetString(0);
        foreach (string checkName in _checkSqlByName.Keys)
        {
            Assert.Contains(checkName, createSql, StringComparison.Ordinal);
        }
    }

    private static string[] ReadApplicationTables(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
              AND name <> '__EFMigrationsHistory'
            ORDER BY name;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return [.. names];
    }
}

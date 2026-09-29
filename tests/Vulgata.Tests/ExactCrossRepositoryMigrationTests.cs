using Vulgata.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

public sealed class ExactCrossRepositoryMigrationTests
{
    private const string _baselineMigration = "20260801033655_AddSequentialScanRunner";
    private const string _migrationSuffix = "_AddExactCrossRepositoryResolution";
    private const string _npgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string _sourceAnchorAlternateKey =
        "AK_SourceAnchors_Id_DocumentVersionId_RepositoryId_ScanRunId";

    private static readonly string[] _tableNames =
    [
        "ExactCrossRepositoryEdgeEvidence",
        "Uncertainties",
    ];

    private static readonly IReadOnlyDictionary<string, string> _evidenceChecks =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CK_ExactCrossRepositoryEdgeEvidence_Repositories_Distinct"] =
                "\"ConsumerRepositoryId\" <> \"ProviderRepositoryId\"",
            ["CK_ExactCrossRepositoryEdgeEvidence_Protocol_Closed"] =
                "\"Protocol\" = 'CustomRpc'",
            ["CK_ExactCrossRepositoryEdgeEvidence_ResolutionKind_Closed"] =
                "\"ResolutionKind\" = 'ExactConfigured'",
            ["CK_ExactCrossRepositoryEdgeEvidence_Confidence_Exact"] =
                "\"Confidence\" = 1.0000",
            ["CK_ExactCrossRepositoryEdgeEvidence_SchemaVersion_Closed"] =
                "\"ProfileSchemaVersion\" = '1.0'",
            ["CK_ExactCrossRepositoryEdgeEvidence_Hashes_Closed"] =
                LowerHex64("\"ProfileFingerprint\"") + " AND " +
                LowerHex64("\"EvidenceFingerprint\""),
        };

    private static readonly IReadOnlyDictionary<string, string> _uncertaintyChecks =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CK_Uncertainties_Type_Closed"] =
                "\"Type\" IN ('cross_repository', 'unsupported_communication')",
            ["CK_Uncertainties_Status_Closed"] =
                "\"Status\" IN ('unresolved', 'resolved', 'wontfix')",
            ["CK_Uncertainties_SubStatus_Closed"] =
                "\"SubStatus\" IN ('cross_repo', 'unknown_target', 'ambiguous')",
            ["CK_Uncertainties_ReasonCode_Closed"] =
                "\"ReasonCode\" IN ('variable_target', 'disallowed_invocation', " +
                "'malformed_provider', 'provider_out_of_root', 'unmatched', " +
                "'multiple_providers', 'normalization_mismatch', 'unsupported_kind')",
            ["CK_Uncertainties_ObservationKind_Closed"] =
                "\"ObservationKind\" IS NULL OR \"ObservationKind\" = 'file_storage'",
            ["CK_Uncertainties_UnsupportedCombination_Closed"] =
                "(\"ReasonCode\" = 'unsupported_kind' AND \"Type\" = " +
                "'unsupported_communication' AND \"ObservationKind\" = 'file_storage') OR " +
                "(\"ReasonCode\" <> 'unsupported_kind' AND \"Type\" = 'cross_repository' " +
                "AND \"ObservationKind\" IS NULL)",
            ["CK_Uncertainties_EndpointPair_Closed"] =
                "(\"EndpointIdentifier\" IS NULL) = " +
                "(\"EndpointIdentifierNormalized\" IS NULL)",
            ["CK_Uncertainties_Resolution_Closed"] =
                "(\"Status\" = 'unresolved' AND \"ResolvedAt\" IS NULL AND " +
                "\"ResolvedByDocumentVersionId\" IS NULL) OR \"Status\" <> 'unresolved'",
            ["CK_Uncertainties_SchemaVersion_Closed"] =
                "\"ProfileSchemaVersion\" = '1.0'",
            ["CK_Uncertainties_Hashes_Closed"] =
                LowerHex64("\"ProfileFingerprint\"") + " AND " +
                LowerHex64("\"EvidenceFingerprint\""),
        };

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void AddExactCrossRepositoryResolution_Operations_AreAdditiveCompleteAndReversible()
    {
        using VulgataDbContext db = CreateSqliteContext();
        IMigrationsAssembly migrationsAssembly = db.GetService<IMigrationsAssembly>();
        KeyValuePair<string, System.Reflection.TypeInfo> migrationEntry = Assert.Single(
            migrationsAssembly.Migrations,
            entry => entry.Key.EndsWith(_migrationSuffix, StringComparison.Ordinal));
        Migration migration = migrationsAssembly.CreateMigration(
            migrationEntry.Value,
            _npgsqlProvider);

        Type[] permittedUpOperationTypes =
        [
            typeof(AddForeignKeyOperation),
            typeof(AddUniqueConstraintOperation),
            typeof(CreateIndexOperation),
            typeof(CreateTableOperation),
        ];
        Assert.All(
            migration.UpOperations,
            operation => Assert.Contains(operation.GetType(), permittedUpOperationTypes));
        Assert.Equal(
            _tableNames,
            migration.UpOperations
                .OfType<CreateTableOperation>()
                .Select(operation => operation.Name)
                .Order(StringComparer.Ordinal));

        AddUniqueConstraintOperation sourceAnchorKey = Assert.Single(
            migration.UpOperations.OfType<AddUniqueConstraintOperation>());
        Assert.Equal(_sourceAnchorAlternateKey, sourceAnchorKey.Name);
        Assert.Equal("SourceAnchors", sourceAnchorKey.Table);
        Assert.Equal(
            ["Id", "DocumentVersionId", "RepositoryId", "ScanRunId"],
            sourceAnchorKey.Columns);

        IReadOnlyList<AddForeignKeyOperation> foreignKeys =
        [
            .. migration.UpOperations
                .OfType<CreateTableOperation>()
                .SelectMany(operation => operation.ForeignKeys),
            .. migration.UpOperations.OfType<AddForeignKeyOperation>(),
        ];
        Assert.NotEmpty(foreignKeys);
        Assert.All(
            foreignKeys,
            foreignKey => Assert.True(
                foreignKey.OnDelete is
                    ReferentialAction.Restrict or ReferentialAction.NoAction));
        AssertForeignKey(
            foreignKeys,
            "ExactCrossRepositoryEdgeEvidence",
            ["EdgeId"],
            "DocumentEdges",
            ["Id"]);
        AssertForeignKey(
            foreignKeys,
            "ExactCrossRepositoryEdgeEvidence",
            [
                "ConsumerSourceAnchorId",
                "ConsumerDocumentVersionId",
                "ConsumerRepositoryId",
                "ConsumerScanRunId",
            ],
            "SourceAnchors",
            ["Id", "DocumentVersionId", "RepositoryId", "ScanRunId"]);
        AssertForeignKey(
            foreignKeys,
            "ExactCrossRepositoryEdgeEvidence",
            [
                "ProviderSourceAnchorId",
                "ProviderDocumentVersionId",
                "ProviderRepositoryId",
                "ProviderScanRunId",
            ],
            "SourceAnchors",
            ["Id", "DocumentVersionId", "RepositoryId", "ScanRunId"]);
        AssertForeignKey(
            foreignKeys,
            "Uncertainties",
            ["DocumentVersionId", "RepositoryId", "ScanRunId"],
            "DocumentVersions",
            ["Id", "RepositoryId", "ScanRunId"]);
        AssertForeignKey(
            foreignKeys,
            "Uncertainties",
            ["SourceAnchorId", "DocumentVersionId", "RepositoryId", "ScanRunId"],
            "SourceAnchors",
            ["Id", "DocumentVersionId", "RepositoryId", "ScanRunId"]);
        AssertForeignKey(
            foreignKeys,
            "Uncertainties",
            ["TargetRepositoryId"],
            "Repositories",
            ["Id"]);

        CreateTableOperation evidenceTable = Assert.Single(
            migration.UpOperations.OfType<CreateTableOperation>(),
            operation => operation.Name == "ExactCrossRepositoryEdgeEvidence");
        CreateTableOperation uncertaintyTable = Assert.Single(
            migration.UpOperations.OfType<CreateTableOperation>(),
            operation => operation.Name == "Uncertainties");
        AssertChecks(evidenceTable, _evidenceChecks);
        AssertChecks(uncertaintyTable, _uncertaintyChecks);
        AssertUniqueIndex(
            migration.UpOperations,
            "ExactCrossRepositoryEdgeEvidence",
            "EvidenceFingerprint");
        AssertUniqueIndex(
            migration.UpOperations,
            "Uncertainties",
            "EvidenceFingerprint");
        AssertIndex(
            migration.UpOperations,
            "Uncertainties",
            false,
            "RepositoryId",
            "Status",
            "SubStatus");

        Assert.All(
            migration.DownOperations,
            operation => Assert.True(
                operation is DropTableOperation or DropUniqueConstraintOperation));
        Assert.Equal(
            _tableNames,
            migration.DownOperations
                .OfType<DropTableOperation>()
                .Select(operation => operation.Name)
                .Order(StringComparer.Ordinal));
        DropUniqueConstraintOperation droppedSourceAnchorKey = Assert.Single(
            migration.DownOperations.OfType<DropUniqueConstraintOperation>());
        Assert.Equal(_sourceAnchorAlternateKey, droppedSourceAnchorKey.Name);
        Assert.Equal("SourceAnchors", droppedSourceAnchorKey.Table);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void AddExactCrossRepositoryResolution_Model_MatchesAcceptedSchema()
    {
        using VulgataDbContext db = CreateSqliteContext();
        IModel model = db.GetService<IDesignTimeModel>().Model;
        IEntityType evidence = Assert.IsAssignableFrom<IEntityType>(
            model.FindEntityType(typeof(ExactCrossRepositoryEdgeEvidence)));
        IEntityType uncertainty = Assert.IsAssignableFrom<IEntityType>(
            model.FindEntityType(typeof(Uncertainty)));
        IEntityType sourceAnchor = Assert.IsAssignableFrom<IEntityType>(
            model.FindEntityType(typeof(SourceAnchor)));

        Assert.Equal("ExactCrossRepositoryEdgeEvidence", evidence.GetTableName());
        Assert.Equal("Uncertainties", uncertainty.GetTableName());
        Assert.Contains(
            sourceAnchor.GetKeys(),
            key => key.Properties.Select(property => property.Name).SequenceEqual(
                [
                    nameof(SourceAnchor.Id),
                    nameof(SourceAnchor.DocumentVersionId),
                    nameof(SourceAnchor.RepositoryId),
                    nameof(SourceAnchor.ScanRunId),
                ]));
        Assert.All(
            evidence.GetForeignKeys().Concat(uncertainty.GetForeignKeys()),
            foreignKey => Assert.True(
                foreignKey.DeleteBehavior is DeleteBehavior.Restrict or DeleteBehavior.NoAction));
        AssertModelForeignKey(
            evidence,
            [
                nameof(ExactCrossRepositoryEdgeEvidence.ConsumerSourceAnchorId),
                nameof(ExactCrossRepositoryEdgeEvidence.ConsumerDocumentVersionId),
                nameof(ExactCrossRepositoryEdgeEvidence.ConsumerRepositoryId),
                nameof(ExactCrossRepositoryEdgeEvidence.ConsumerScanRunId),
            ],
            typeof(SourceAnchor),
            [
                nameof(SourceAnchor.Id),
                nameof(SourceAnchor.DocumentVersionId),
                nameof(SourceAnchor.RepositoryId),
                nameof(SourceAnchor.ScanRunId),
            ]);
        AssertModelForeignKey(
            evidence,
            [
                nameof(ExactCrossRepositoryEdgeEvidence.ProviderSourceAnchorId),
                nameof(ExactCrossRepositoryEdgeEvidence.ProviderDocumentVersionId),
                nameof(ExactCrossRepositoryEdgeEvidence.ProviderRepositoryId),
                nameof(ExactCrossRepositoryEdgeEvidence.ProviderScanRunId),
            ],
            typeof(SourceAnchor),
            [
                nameof(SourceAnchor.Id),
                nameof(SourceAnchor.DocumentVersionId),
                nameof(SourceAnchor.RepositoryId),
                nameof(SourceAnchor.ScanRunId),
            ]);
        AssertModelForeignKey(
            uncertainty,
            [
                nameof(Uncertainty.DocumentVersionId),
                nameof(Uncertainty.RepositoryId),
                nameof(Uncertainty.ScanRunId),
            ],
            typeof(DocumentVersion),
            [
                nameof(DocumentVersion.Id),
                nameof(DocumentVersion.RepositoryId),
                nameof(DocumentVersion.ScanRunId),
            ]);
        AssertModelForeignKey(
            uncertainty,
            [
                nameof(Uncertainty.SourceAnchorId),
                nameof(Uncertainty.DocumentVersionId),
                nameof(Uncertainty.RepositoryId),
                nameof(Uncertainty.ScanRunId),
            ],
            typeof(SourceAnchor),
            [
                nameof(SourceAnchor.Id),
                nameof(SourceAnchor.DocumentVersionId),
                nameof(SourceAnchor.RepositoryId),
                nameof(SourceAnchor.ScanRunId),
            ]);
        AssertChecks(evidence, _evidenceChecks);
        AssertChecks(uncertainty, _uncertaintyChecks);
        AssertModelIndex(
            evidence,
            true,
            nameof(ExactCrossRepositoryEdgeEvidence.EvidenceFingerprint));
        AssertModelIndex(
            uncertainty,
            true,
            nameof(Uncertainty.EvidenceFingerprint));
        AssertModelIndex(
            uncertainty,
            false,
            nameof(Uncertainty.RepositoryId),
            nameof(Uncertainty.Status),
            nameof(Uncertainty.SubStatus));
        Assert.NotNull(
            typeof(VulgataDbContext).GetProperty(
                nameof(VulgataDbContext.ExactCrossRepositoryEdgeEvidence)));
        Assert.NotNull(
            typeof(VulgataDbContext).GetProperty(nameof(VulgataDbContext.Uncertainties)));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void AddExactCrossRepositoryResolution_ApplyRollbackAndReapply_PreservesPredecessor()
    {
        using var connection = new SqliteConnection(
            "Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        DbContextOptions<VulgataDbContext> options =
            new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite(connection)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options;
        using var db = new VulgataDbContext(options);
        IMigrator migrator = db.GetService<IMigrator>();

        migrator.Migrate(_baselineMigration);
        string[] predecessorTables = ReadApplicationTables(connection);
        Assert.Contains("ScanExecutions", predecessorTables);
        Assert.Contains("SourceAnchors", predecessorTables);
        Assert.DoesNotContain(
            predecessorTables,
            table => _tableNames.Contains(table, StringComparer.Ordinal));
        Guid predecessorSystemId = SeedPredecessorSystem(options);
        AssertPredecessorSystem(connection, predecessorSystemId);

        migrator.Migrate();
        AssertStory114Schema(connection);
        AssertPredecessorSystem(connection, predecessorSystemId);
        string[] firstAppliedSchema = ReadStory114Schema(connection);

        migrator.Migrate(_baselineMigration);
        Assert.Equal(predecessorTables, ReadApplicationTables(connection));
        AssertPredecessorSystem(connection, predecessorSystemId);
        Assert.DoesNotContain(
            _sourceAnchorAlternateKey,
            ReadTableSql(connection, "SourceAnchors"),
            StringComparison.Ordinal);

        migrator.Migrate();
        AssertStory114Schema(connection);
        AssertPredecessorSystem(connection, predecessorSystemId);
        Assert.Equal(firstAppliedSchema, ReadStory114Schema(connection));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AddExactCrossRepositoryResolution_PostgreSqlLifecycle_UsesDisposableDatabase()
    {
        string? infrastructureConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(infrastructureConnectionString))
        {
            throw new InvalidOperationException("Required PostgreSQL qualification prerequisite is unavailable; use eng/test.ps1 -Suite PostgreSql.");
        }

        Vulgata.Tests.TestInfrastructure.TestResourceScope.ValidateAdministrator(infrastructureConnectionString);
        var infrastructureBuilder = new NpgsqlConnectionStringBuilder(
            infrastructureConnectionString);
        string databaseName =
            $"vulgata_story114_g10_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(infrastructureBuilder.ConnectionString);
        adminBuilder.Database = "postgres";
        var targetBuilder = new NpgsqlConnectionStringBuilder(infrastructureBuilder.ConnectionString)
        {
            Database = databaseName,
            Pooling = false,
        };
        bool databaseCreated = false;

        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        try
        {
            await ExecuteAdminCommandAsync(
                admin,
                $"CREATE DATABASE \"{databaseName}\"");
            databaseCreated = true;

            DbContextOptions<VulgataDbContext> options =
                new DbContextOptionsBuilder<VulgataDbContext>()
                    .UseNpgsql(
                        targetBuilder.ConnectionString,
                        npgsql => npgsql.MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            "public"))
                    .Options;
            await using (var db = new VulgataDbContext(options))
            {
                IMigrator migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync(_baselineMigration);
            }

            Guid predecessorSystemId = SeedPredecessorSystem(options);
            await using (var db = new VulgataDbContext(options))
            {
                IMigrator migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync();
                await AssertPostgreSqlStory114SchemaAsync(db);
                Assert.Equal(1, await db.Systems.CountAsync(system => system.Id == predecessorSystemId));

                await migrator.MigrateAsync(_baselineMigration);
                Assert.Equal(1, await db.Systems.CountAsync(system => system.Id == predecessorSystemId));
                Assert.Equal(0, await CountPostgreSqlStory114TablesAsync(db));

                await migrator.MigrateAsync();
                await AssertPostgreSqlStory114SchemaAsync(db);
                Assert.Equal(1, await db.Systems.CountAsync(system => system.Id == predecessorSystemId));
            }
        }
        finally
        {
            if (databaseCreated)
            {
                await TerminateDatabaseConnectionsAsync(admin, databaseName);
                await ExecuteAdminCommandAsync(
                    admin,
                    $"DROP DATABASE IF EXISTS \"{databaseName}\"");
                Assert.False(await DatabaseExistsAsync(admin, databaseName));
            }
        }
    }

    private static VulgataDbContext CreateSqliteContext()
    {
        var options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new(options);
    }

    private static Guid SeedPredecessorSystem(DbContextOptions<VulgataDbContext> options)
    {
        using var db = new VulgataDbContext(options);
        var system = new SystemEntity(
            "Story 11.4 migration predecessor sentinel",
            null,
            null,
            new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero));
        db.Systems.Add(system);
        db.SaveChanges();
        return system.Id;
    }

    private static void AssertPredecessorSystem(
        SqliteConnection connection,
        Guid systemId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Systems WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", systemId);
        Assert.Equal(1L, Assert.IsType<long>(command.ExecuteScalar()));
    }

    private static void AssertStory114Schema(SqliteConnection connection)
    {
        Assert.Equal(
            _tableNames,
            ReadApplicationTables(connection)
                .Where(table => _tableNames.Contains(table, StringComparer.Ordinal))
                .Order(StringComparer.Ordinal));
        Assert.Contains(
            _sourceAnchorAlternateKey,
            ReadTableSql(connection, "SourceAnchors"),
            StringComparison.Ordinal);
        foreach (string checkName in _evidenceChecks.Keys)
        {
            Assert.Contains(
                checkName,
                ReadTableSql(connection, "ExactCrossRepositoryEdgeEvidence"),
                StringComparison.Ordinal);
        }

        foreach (string checkName in _uncertaintyChecks.Keys)
        {
            Assert.Contains(
                checkName,
                ReadTableSql(connection, "Uncertainties"),
                StringComparison.Ordinal);
        }
    }

    private static void AssertForeignKey(
        IEnumerable<AddForeignKeyOperation> foreignKeys,
        string table,
        string[] columns,
        string principalTable,
        string[] principalColumns)
    {
        Assert.Contains(
            foreignKeys,
            foreignKey =>
                foreignKey.Table == table &&
                foreignKey.Columns.SequenceEqual(columns) &&
                foreignKey.PrincipalTable == principalTable &&
                foreignKey.PrincipalColumns!.SequenceEqual(principalColumns));
    }

    private static void AssertModelForeignKey(
        IEntityType entityType,
        string[] propertyNames,
        Type principalType,
        string[] principalPropertyNames)
    {
        Assert.Contains(
            entityType.GetForeignKeys(),
            foreignKey =>
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(propertyNames) &&
                foreignKey.PrincipalEntityType.ClrType == principalType &&
                foreignKey.PrincipalKey.Properties.Select(property => property.Name)
                    .SequenceEqual(principalPropertyNames));
    }

    private static void AssertChecks(
        CreateTableOperation table,
        IReadOnlyDictionary<string, string> expected)
    {
        Dictionary<string, string> actual = table.CheckConstraints
            .ToDictionary(check => check.Name!, check => check.Sql);
        AssertChecks(actual, expected);
    }

    private static void AssertChecks(
        IEntityType entityType,
        IReadOnlyDictionary<string, string> expected)
    {
        Dictionary<string, string> actual = entityType.GetCheckConstraints()
            .ToDictionary(check => check.Name!, check => check.Sql);
        AssertChecks(actual, expected);
    }

    private static void AssertChecks(
        IReadOnlyDictionary<string, string> actual,
        IReadOnlyDictionary<string, string> expected)
    {
        Assert.Equal(
            expected.Keys.Order(StringComparer.Ordinal),
            actual.Keys.Order(StringComparer.Ordinal));
        Assert.All(expected, check => Assert.Equal(check.Value, actual[check.Key]));
    }

    private static void AssertUniqueIndex(
        IReadOnlyList<MigrationOperation> operations,
        string table,
        params string[] columns)
    {
        AssertIndex(operations, table, true, columns);
    }

    private static void AssertIndex(
        IReadOnlyList<MigrationOperation> operations,
        string table,
        bool unique,
        params string[] columns)
    {
        Assert.Contains(
            operations.OfType<CreateIndexOperation>(),
            index =>
                index.Table == table &&
                index.IsUnique == unique &&
                index.Columns.SequenceEqual(columns));
    }

    private static void AssertModelIndex(
        IEntityType entityType,
        bool unique,
        params string[] propertyNames)
    {
        Assert.Contains(
            entityType.GetIndexes(),
            index =>
                index.IsUnique == unique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(propertyNames));
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

    private static string ReadTableSql(SqliteConnection connection, string tableName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Assert.IsType<string>(command.ExecuteScalar());
    }

    private static string[] ReadStory114Schema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT type || ':' || name || ':' || COALESCE(sql, '')
            FROM sqlite_master
            WHERE name IN (
                    'ExactCrossRepositoryEdgeEvidence',
                    'Uncertainties',
                    'SourceAnchors')
               OR tbl_name IN (
                    'ExactCrossRepositoryEdgeEvidence',
                    'Uncertainties')
            ORDER BY type, name;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        var schema = new List<string>();
        while (reader.Read())
        {
            schema.Add(reader.GetString(0));
        }

        return [.. schema];
    }

    private static string LowerHex64(string column)
    {
        string stripped = column;
        foreach (char character in "0123456789abcdef")
        {
            stripped = $"replace({stripped}, '{character}', '')";
        }

        return $"length({column}) = 64 AND lower({column}) = {column} AND length({stripped}) = 0";
    }

    private static async Task AssertPostgreSqlStory114SchemaAsync(VulgataDbContext db)
    {
        Assert.Equal(2, await CountPostgreSqlStory114TablesAsync(db));
        await using System.Data.Common.DbCommand command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM pg_constraint
            WHERE connamespace = 'vulgata'::regnamespace
              AND conname = ANY (@names);
            """;
        command.Parameters.Add(new NpgsqlParameter<string[]>(
            "names",
            [
                _sourceAnchorAlternateKey,
                .. _evidenceChecks.Keys,
                .. _uncertaintyChecks.Keys,
            ]));
        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync();
        }

        int expectedConstraintCount =
            1 + _evidenceChecks.Count + _uncertaintyChecks.Count;
        Assert.Equal(expectedConstraintCount, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    private static async Task<int> CountPostgreSqlStory114TablesAsync(VulgataDbContext db)
    {
        await using System.Data.Common.DbCommand command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'vulgata'
              AND table_name = ANY (@names);
            """;
        command.Parameters.Add(new NpgsqlParameter<string[]>("names", _tableNames));
        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync();
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAdminCommandAsync(
        NpgsqlConnection connection,
        string commandText)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task TerminateDatabaseConnectionsAsync(
        NpgsqlConnection connection,
        string databaseName)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @databaseName
              AND pid <> pg_backend_pid();
            """;
        command.Parameters.AddWithValue("databaseName", databaseName);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> DatabaseExistsAsync(
        NpgsqlConnection connection,
        string databaseName)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);";
        command.Parameters.AddWithValue("name", databaseName);
        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }
}

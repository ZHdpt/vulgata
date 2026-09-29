using Vulgata.Tests.TestInfrastructure;
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

public sealed class PreScanMigrationTests
{
    private const string _baselineMigration = "20260720060442_FrameworkEvidenceAuditCount";
    private const string _migrationSuffix = "_AddConservativePreScanGate";
    private const string _npgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AddConservativePreScanGate_Operations_AreAdditiveCompleteAndReversible()
    {
        using VulgataDbContext db = CreateContext();
        IMigrationsAssembly migrationsAssembly = db.GetService<IMigrationsAssembly>();
        KeyValuePair<string, System.Reflection.TypeInfo> migrationEntry = Assert.Single(
            migrationsAssembly.Migrations,
            entry => entry.Key.EndsWith(_migrationSuffix, StringComparison.Ordinal));
        Migration migration = migrationsAssembly.CreateMigration(
            migrationEntry.Value,
            _npgsqlProvider);

        AssertUpOperations(migration.UpOperations);
        AssertDownOperations(migration.DownOperations);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreScanModel_ConstraintsAndOwnership_MatchApprovedAggregate()
    {
        using VulgataDbContext db = CreateContext();
        IModel model = db.GetService<IDesignTimeModel>().Model;
        IEntityType? result = model.FindEntityType(typeof(PreScanResult));
        IEntityType? classification = model.FindEntityType(
            typeof(CommunicationKindClassification));
        Assert.NotNull(result);
        Assert.NotNull(classification);
        IKey? resultKey = result.FindPrimaryKey();
        IKey? classificationKey = classification.FindPrimaryKey();
        Assert.NotNull(resultKey);
        Assert.NotNull(classificationKey);

        Assert.Equal(
            [nameof(PreScanResult.Id)],
            resultKey.Properties.Select(property => property.Name));
        IForeignKey resultOwnership = Assert.Single(
            result.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(ScanRun));
        Assert.Equal(
            [nameof(PreScanResult.Id), nameof(PreScanResult.RepositoryId)],
            resultOwnership.Properties.Select(property => property.Name));
        Assert.Equal(
            [nameof(ScanRun.Id), nameof(ScanRun.RepositoryId)],
            resultOwnership.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, resultOwnership.DeleteBehavior);
        Assert.Contains(
            result.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(PreScanResult.RepositoryId),
                        nameof(PreScanResult.ScanRunId),
                    ]));

        Assert.Equal(
            [nameof(CommunicationKindClassification.Id)],
            classificationKey.Properties
                .Select(property => property.Name));
        IForeignKey childOwnership = Assert.Single(classification.GetForeignKeys());
        Assert.Equal(
            [nameof(CommunicationKindClassification.PreScanResultId)],
            childOwnership.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, childOwnership.DeleteBehavior);
        Assert.Contains(
            classification.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(CommunicationKindClassification.PreScanResultId),
                        nameof(CommunicationKindClassification.Fingerprint),
                    ]));
        Assert.Contains(
            classification.GetIndexes(),
            index =>
                !index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(CommunicationKindClassification.PreScanResultId),
                        nameof(CommunicationKindClassification.Level),
                        nameof(CommunicationKindClassification.Kind),
                    ]));

        AssertResultChecks(result.GetCheckConstraints());
        AssertClassificationChecks(classification.GetCheckConstraints());

        string[] newTableNames =
        [
            .. model.GetEntityTypes()
                .Select(entity => entity.GetTableName())
                .Where(tableName => tableName is
                    "PreScanResults" or "CommunicationKindClassifications")
                .Cast<string>()
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal(
            ["CommunicationKindClassifications", "PreScanResults"],
            newTableNames);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AddConservativePreScanGate_ApplyAndRollback_RestoresBaselineAtomically()
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

        migrator.Migrate(_baselineMigration);
        string[] baselineTables = ReadApplicationTables(connection);
        Assert.DoesNotContain(
            nameof(ScanRun.ProfiledRevision),
            ReadScanRunColumns(connection));

        migrator.Migrate("20260730083207_AddConservativePreScanGate");

        Assert.Equal(
            ["CommunicationKindClassifications", "PreScanResults"],
            ReadApplicationTables(connection)
                .Except(baselineTables, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
        Assert.Contains(
            nameof(ScanRun.ProfiledRevision),
            ReadScanRunColumns(connection));
        string resultSql = ReadTableSql(connection, "PreScanResults");
        Assert.Contains("CK_PreScanResults_State", resultSql, StringComparison.Ordinal);
        Assert.Contains("CK_PreScanResults_SchemaVersion", resultSql, StringComparison.Ordinal);
        string classificationSql = ReadTableSql(
            connection,
            "CommunicationKindClassifications");
        Assert.Contains(
            "CK_CommunicationKindClassifications_Compatibility",
            classificationSql,
            StringComparison.Ordinal);
        Assert.Contains(
            "CK_CommunicationKindClassifications_PathLocator",
            classificationSql,
            StringComparison.Ordinal);

        migrator.Migrate(_baselineMigration);

        Assert.Equal(baselineTables, ReadApplicationTables(connection));
        Assert.DoesNotContain(
            nameof(ScanRun.ProfiledRevision),
            ReadScanRunColumns(connection));
    }

    private static VulgataDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new(options);
    }

    private static void AssertUpOperations(IReadOnlyList<MigrationOperation> operations)
    {
        Type[] permittedOperationTypes =
        [
            typeof(AddColumnOperation),
            typeof(CreateTableOperation),
            typeof(CreateIndexOperation),
        ];
        Assert.All(
            operations,
            operation => Assert.Contains(operation.GetType(), permittedOperationTypes));

        AddColumnOperation profiledRevision = Assert.Single(
            operations.OfType<AddColumnOperation>());
        Assert.Equal("ScanRuns", profiledRevision.Table);
        Assert.Equal(nameof(ScanRun.ProfiledRevision), profiledRevision.Name);
        Assert.True(profiledRevision.IsNullable);
        Assert.Equal(64, profiledRevision.MaxLength);

        CreateTableOperation[] tables =
        [
            .. operations.OfType<CreateTableOperation>()
                .OrderBy(operation => operation.Name, StringComparer.Ordinal),
        ];
        Assert.Equal(
            ["CommunicationKindClassifications", "PreScanResults"],
            tables.Select(operation => operation.Name));
        Assert.DoesNotContain(
            tables,
            table => ContainsDeferredFamilyName(table.Name));

        CreateTableOperation result = Assert.Single(
            tables,
            table => table.Name == "PreScanResults");
        Assert.NotNull(result.PrimaryKey);
        Assert.Equal(["Id"], result.PrimaryKey.Columns);
        AddForeignKeyOperation resultOwnership = Assert.Single(result.ForeignKeys);
        Assert.Equal(["Id", "RepositoryId"], resultOwnership.Columns);
        Assert.Equal("ScanRuns", resultOwnership.PrincipalTable);
        Assert.Equal(["Id", "RepositoryId"], resultOwnership.PrincipalColumns!);
        Assert.Equal(ReferentialAction.Cascade, resultOwnership.OnDelete);
        AssertResultChecks(result.CheckConstraints);

        CreateTableOperation classification = Assert.Single(
            tables,
            table => table.Name == "CommunicationKindClassifications");
        Assert.NotNull(classification.PrimaryKey);
        Assert.Equal(["Id"], classification.PrimaryKey.Columns);
        AddForeignKeyOperation childOwnership = Assert.Single(classification.ForeignKeys);
        Assert.Equal(["PreScanResultId"], childOwnership.Columns);
        Assert.Equal("PreScanResults", childOwnership.PrincipalTable);
        Assert.Equal(["Id"], childOwnership.PrincipalColumns!);
        Assert.Equal(ReferentialAction.Cascade, childOwnership.OnDelete);
        AssertClassificationChecks(classification.CheckConstraints);

        CreateIndexOperation[] indexes = [.. operations.OfType<CreateIndexOperation>()];
        Assert.Contains(
            indexes,
            index =>
                index.Table == "PreScanResults" &&
                index.IsUnique &&
                index.Columns.SequenceEqual(["RepositoryId", "ScanRunId"]));
        Assert.Contains(
            indexes,
            index =>
                index.Table == "CommunicationKindClassifications" &&
                index.IsUnique &&
                index.Columns.SequenceEqual(["PreScanResultId", "Fingerprint"]));
        Assert.Contains(
            indexes,
            index =>
                index.Table == "CommunicationKindClassifications" &&
                !index.IsUnique &&
                index.Columns.SequenceEqual(["PreScanResultId", "Level", "Kind"]));
    }

    private static void AssertDownOperations(IReadOnlyList<MigrationOperation> operations)
    {
        Assert.Equal(3, operations.Count);
        Assert.Collection(
            operations,
            operation => Assert.Equal(
                "CommunicationKindClassifications",
                Assert.IsType<DropTableOperation>(operation).Name),
            operation => Assert.Equal(
                "PreScanResults",
                Assert.IsType<DropTableOperation>(operation).Name),
            operation =>
            {
                DropColumnOperation column = Assert.IsType<DropColumnOperation>(operation);
                Assert.Equal("ScanRuns", column.Table);
                Assert.Equal(nameof(ScanRun.ProfiledRevision), column.Name);
            });
    }

    private static void AssertResultChecks(IEnumerable<ICheckConstraint> checks)
    {
        Dictionary<string, string> sqlByName = checks.ToDictionary(
            check => check.Name!,
            check => check.Sql);
        AssertResultChecks(sqlByName);
    }

    private static void AssertResultChecks(IEnumerable<AddCheckConstraintOperation> checks)
    {
        Dictionary<string, string> sqlByName = checks.ToDictionary(
            check => check.Name,
            check => check.Sql);
        AssertResultChecks(sqlByName);
    }

    private static void AssertResultChecks(IReadOnlyDictionary<string, string> sqlByName)
    {
        AssertCheckContains(
            sqlByName,
            "CK_PreScanResults_Identity",
            "\"Id\"",
            "\"ScanRunId\"");
        AssertCheckContains(
            sqlByName,
            "CK_PreScanResults_State",
            "'PreScanApproved'");
        AssertCheckContains(
            sqlByName,
            "CK_PreScanResults_SchemaVersion",
            "'1.0'");
        AssertCheckContains(
            sqlByName,
            "CK_PreScanResults_ProfiledRevision_Length",
            "\"ProfiledRevision\"",
            "1",
            "64");
        AssertCheckContains(
            sqlByName,
            "CK_PreScanResults_Fingerprint_Length",
            "\"Fingerprint\"",
            "64");
        AssertCheckContains(
            sqlByName,
            "CK_PreScanResults_ApprovalMessage_Length",
            "\"ApprovalMessage\"",
            "256");
        AssertCheckContains(
            sqlByName,
            "CK_PreScanResults_ConfirmedKinds",
            "\"ConfirmedCommunicationKinds\"",
            "0",
            "3");
        AssertCheckContains(
            sqlByName,
            "CK_PreScanResults_Counts",
            "\"TotalConfirmedEvidenceCount\"",
            "\"TotalHintCount\"",
            "\"TotalWarningCount\"",
            "250000");
    }

    private static void AssertClassificationChecks(IEnumerable<ICheckConstraint> checks)
    {
        Dictionary<string, string> sqlByName = checks.ToDictionary(
            check => check.Name!,
            check => check.Sql);
        AssertClassificationChecks(sqlByName);
    }

    private static void AssertClassificationChecks(
        IEnumerable<AddCheckConstraintOperation> checks)
    {
        Dictionary<string, string> sqlByName = checks.ToDictionary(
            check => check.Name,
            check => check.Sql);
        AssertClassificationChecks(sqlByName);
    }

    private static void AssertClassificationChecks(
        IReadOnlyDictionary<string, string> sqlByName)
    {
        AssertCheckContains(
            sqlByName,
            "CK_CommunicationKindClassifications_Kind",
            "'Rpc'",
            "'Http'");
        AssertCheckContains(
            sqlByName,
            "CK_CommunicationKindClassifications_Level",
            "'Confirmed'",
            "'Hint'",
            "'Warning'");
        AssertCheckContains(
            sqlByName,
            "CK_CommunicationKindClassifications_Confidence",
            "'High'",
            "'Conservative'",
            "'None'");
        AssertCheckContains(
            sqlByName,
            "CK_CommunicationKindClassifications_Rule",
            "'RpcDirectAnnotation'",
            "'HttpMatchedConfiguration'",
            "'UnsupportedFamily'");
        AssertCheckContains(
            sqlByName,
            "CK_CommunicationKindClassifications_Provenance",
            "'StructuralAnnotation'",
            "'MatchedConfiguration'",
            "'Detector'");
        AssertCheckContains(
            sqlByName,
            "CK_CommunicationKindClassifications_LevelConfidence",
            "\"Level\"",
            "\"Confidence\"",
            "\"Kind\"");
        AssertCheckContains(
            sqlByName,
            "CK_CommunicationKindClassifications_Compatibility",
            "'RpcDirectAnnotation'",
            "'HttpMatchedConfiguration'",
            "'RpcImportOnly'",
            "'UnsupportedFamily'",
            "\"Provenance\"");
        AssertCheckContains(
            sqlByName,
            "CK_CommunicationKindClassifications_PathLocator",
            "\"RelativePath\"",
            "2048",
            "\"Locator\"",
            "4096");
        AssertCheckContains(
            sqlByName,
            "CK_CommunicationKindClassifications_OccurrenceCount",
            "\"OccurrenceCount\"",
            "1",
            "250000");
        AssertCheckContains(
            sqlByName,
            "CK_CommunicationKindClassifications_Fingerprint_Length",
            "\"Fingerprint\"",
            "64");
    }

    private static void AssertCheckContains(
        IReadOnlyDictionary<string, string> sqlByName,
        string name,
        params string[] expectedFragments)
    {
        string sql = sqlByName[name];
        Assert.All(
            expectedFragments,
            fragment => Assert.Contains(fragment, sql, StringComparison.Ordinal));
    }

    private static bool ContainsDeferredFamilyName(string tableName)
    {
        string[] forbiddenFragments =
        [
            "Document",
            "Retrieval",
            "Graph",
            "Endpoint",
            "Dispatch",
            "Uncertainty",
        ];
        return forbiddenFragments.Any(
            fragment => tableName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
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

    private static string[] ReadScanRunColumns(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('ScanRuns');";
        using SqliteDataReader reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(1));
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
}

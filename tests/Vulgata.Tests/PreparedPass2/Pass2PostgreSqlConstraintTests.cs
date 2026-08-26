using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.Migrations;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Vulgata.Tests.PreparedPass2;

public sealed class Pass2PostgreSqlConstraintTests(ITestOutputHelper output)
{
    private const string MigrationSuffix = "_ExactCrossRepositoryFlowPass2";

    private static readonly string[] V2TableNames =
    [
        "CrossRepositoryResolutionDecisions",
        "ExactResolutionProviderEvidenceContributions",
        "Pass2Executions",
        "Pass2Omissions",
        "Pass2JobExecutions",
        "Pass2RetrievalBudgets",
        "Pass2RetrievalReceipts",
        "Pass2RetrievalResults",
    ];

    [PostgreSqlQualificationFact]
    public async Task Story124PostgreSql17_FreshApplyRollbackReapply_PreservesLegacySchema()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            throw SkipException.ForSkip(
                "Qualification skipped visibly: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                await context.Database.MigrateAsync();
                string[] applied = [.. await context.Database.GetAppliedMigrationsAsync()];
                Assert.Contains(
                    applied,
                    migration => migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));
                Assert.Empty(await context.Database.GetPendingMigrationsAsync());
                await AssertV2SchemaAsync(database.ConnectionString);
                Assert.True(await TableExistsAsync(
                    database.ConnectionString,
                    "ExactCrossRepositoryEdgeEvidence"));
                Assert.True(await TableExistsAsync(database.ConnectionString, "Uncertainties"));
            }

            string migrationId;
            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                migrationId = (await context.Database.GetAppliedMigrationsAsync())
                    .Single(migration => migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));
                string baseline = (await context.Database.GetAppliedMigrationsAsync())
                    .Last(migration => migration != migrationId);
                await context.Database.GetService<IMigrator>().MigrateAsync(baseline);
                Assert.False(await TableExistsAsync(
                    database.ConnectionString,
                    "CrossRepositoryResolutionDecisions"));
                Assert.True(await TableExistsAsync(
                    database.ConnectionString,
                    "ExactCrossRepositoryEdgeEvidence"));
                await context.Database.GetService<IMigrator>().MigrateAsync(migrationId);
                await AssertV2SchemaAsync(database.ConnectionString);
            }
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
    }

    [PostgreSqlQualificationFact]
    public async Task Story124PostgreSql17_ConstraintsEnforceNullablePrepublicationAndReusableEdge()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            throw SkipException.ForSkip(
                "Qualification skipped visibly: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();
            await AssertV2SchemaAsync(database.ConnectionString);

            Assert.Equal(
                "YES",
                await ColumnNullabilityAsync(
                    database.ConnectionString,
                    "FlowGroups",
                    "BusinessLogicDocumentVersionId"));
            Assert.Equal(
                "YES",
                await ColumnNullabilityAsync(
                    database.ConnectionString,
                    "CrossRepositoryResolutionDecisions",
                    "DocumentEdgeId"));
            Assert.Equal(
                1,
                await ForeignKeyCountAsync(
                    database.ConnectionString,
                    "CrossRepositoryResolutionDecisions",
                    "DocumentEdgeId"));
            Assert.Equal(
                1,
                await ForeignKeyCountAsync(
                    database.ConnectionString,
                    "ExactResolutionProviderEvidenceContributions",
                    "ResolutionDecisionId"));
            Assert.True(await UniqueIndexExistsAsync(
                database.ConnectionString,
                "DocumentEdges",
                ["SourceDocumentVersionId", "TargetDocumentVersionId", "EdgeType"]));
            Assert.True(await TableExistsAsync(
                database.ConnectionString,
                "ExactCrossRepositoryEdgeEvidence"));
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
    }

    [PostgreSqlQualificationFact]
    public async Task Story124PostgreSql17_RetrievalBudgetUsesJobPrimaryKeyAndUtcUpdatedAt()
    {
        string administratorConnectionString = RequireAdministratorConnectionString();
        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();

            Assert.Equal(
                "Pass2JobExecutionId",
                await PrimaryKeyColumnAsync(database.ConnectionString, "Pass2RetrievalBudgets"));
            Assert.Equal(
                "timestamp with time zone",
                await ColumnDataTypeAsync(database.ConnectionString, "Pass2RetrievalBudgets", "UpdatedAt"));
            Assert.Equal(
                1,
                await ForeignKeyCountAsync(
                    database.ConnectionString,
                    "Pass2RetrievalBudgets",
                    "Pass2JobExecutionId"));
        }
        finally
        {
            await database.DisposeAsync();
            await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
        }
    }

    [PostgreSqlQualificationFact]
    public async Task Story124PostgreSql17_RetrievalTablesEnforceContractChecksRelationally()
    {
        string administratorConnectionString = RequireAdministratorConnectionString();
        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();

            (string Table, string[] Fragments)[] requiredChecks =
            [
                ("Pass2RetrievalBudgets", [
                    "CallsReserved", "ResultsReturned", "AdditionalDocumentCount",
                    "ReturnedUtf8Bytes", "StateVersion", "State"]),
                ("Pass2RetrievalReceipts", [
                    "AttemptOrdinal", "CallOrdinal", "ToolCallIdFingerprint",
                    "RequestFingerprint", "ResultSetFingerprint", "State", "Operation",
                    "ResultCount", "ReturnedUtf8Bytes", "NewAdditionalDocumentCount"]),
                ("Pass2RetrievalResults", [
                    "AttemptOrdinal", "ResultOrdinal", "Kind", "DescriptorFingerprint",
                    "EvidenceFingerprint", "ReturnedUtf8Bytes", "SectionId",
                    "SourceAnchorId", "EdgeId", "ByteStart", "ByteEnd"]),
            ];

            List<string> missing = [];
            foreach ((string table, string[] fragments) in requiredChecks)
            {
                string definitions = string.Join(
                    Environment.NewLine,
                    await CheckConstraintDefinitionsAsync(database.ConnectionString, table));
                missing.AddRange(
                    fragments
                        .Where(fragment => !definitions.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                        .Select(fragment => $"{table}:{fragment}"));
            }

            Assert.Empty(missing);
        }
        finally
        {
            await database.DisposeAsync();
            await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
        }
    }

    [PostgreSqlQualificationFact]
    public async Task Story124PostgreSql17_RetrievalFingerprintsRejectLowercaseNonHexValues()
    {
        string administratorConnectionString = RequireAdministratorConnectionString();
        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();
            foreach (string column in new[]
            {
                "ToolCallIdFingerprint", "RequestFingerprint", "ResultSetFingerprint",
            })
            {
                await AssertReceiptFingerprintRejectedAsync(database.ConnectionString, column);
            }

            foreach (string column in new[] { "DescriptorFingerprint", "EvidenceFingerprint" })
            {
                await AssertResultFingerprintRejectedAsync(database.ConnectionString, column);
            }
        }
        finally
        {
            await database.DisposeAsync();
            await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
        }
    }

    private static VulgataDbContext CreateContext(string connectionString)
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                "public"))
            .Options;
        return new VulgataDbContext(options);
    }

    private static async Task AssertV2SchemaAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT count(*)
            FROM information_schema.tables
            WHERE table_schema = 'vulgata' AND table_name = ANY (@names);
            """,
            connection);
        command.Parameters.AddWithValue("names", V2TableNames);
        Assert.Equal(V2TableNames.Length, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    private static async Task<string> ColumnNullabilityAsync(
        string connectionString,
        string tableName,
        string columnName)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'vulgata' AND table_name = @table AND column_name = @column;
            """,
            connection);
        command.Parameters.AddWithValue("table", tableName);
        command.Parameters.AddWithValue("column", columnName);
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }

    private static async Task<string?> PrimaryKeyColumnAsync(
        string connectionString,
        string tableName)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name
             AND tc.table_schema = kcu.table_schema
             AND tc.table_name = kcu.table_name
            WHERE tc.constraint_type = 'PRIMARY KEY'
              AND tc.table_schema = 'vulgata'
              AND tc.table_name = @table
            ORDER BY kcu.ordinal_position;
            """,
            connection);
        command.Parameters.AddWithValue("table", tableName);
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<string?> ColumnDataTypeAsync(
        string connectionString,
        string tableName,
        string columnName)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'vulgata'
              AND table_name = @table
              AND column_name = @column;
            """,
            connection);
        command.Parameters.AddWithValue("table", tableName);
        command.Parameters.AddWithValue("column", columnName);
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<string[]> CheckConstraintDefinitionsAsync(
        string connectionString,
        string tableName)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT pg_get_constraintdef(pc.oid)
            FROM pg_constraint pc
            JOIN pg_class c ON c.oid = pc.conrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE pc.contype = 'c'
              AND n.nspname = 'vulgata'
              AND c.relname = @table
            ORDER BY pc.conname;
            """,
            connection);
        command.Parameters.AddWithValue("table", tableName);
        List<string> definitions = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            definitions.Add(reader.GetString(0));
        }

        return [.. definitions];
    }

    private static string RequireAdministratorConnectionString()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            throw SkipException.ForSkip(
                "Qualification skipped visibly: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
        }

        return administratorConnectionString;
    }

    private static async Task<int> ForeignKeyCountAsync(
        string connectionString,
        string tableName,
        string columnName)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT count(*)
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name
             AND tc.table_schema = kcu.table_schema
             AND tc.table_name = kcu.table_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema = 'vulgata'
              AND tc.table_name = @table
              AND kcu.column_name = @column;
            """,
            connection);
        command.Parameters.AddWithValue("table", tableName);
        command.Parameters.AddWithValue("column", columnName);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> UniqueIndexExistsAsync(
        string connectionString,
        string tableName,
        IReadOnlyList<string> columns)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_index i
                JOIN pg_class c ON c.oid = i.indrelid
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'vulgata'
                  AND c.relname = @table
                  AND i.indisunique
                  AND (SELECT array_agg(a.attname::text ORDER BY x.ordinality)
                       FROM unnest(i.indkey) WITH ORDINALITY AS x(attnum, ordinality)
                       JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = x.attnum)
                      = @columns);
            """,
            connection);
        command.Parameters.AddWithValue("table", tableName);
        command.Parameters.AddWithValue("columns", columns.ToArray());
        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'vulgata' AND table_name = @name);",
            connection);
        command.Parameters.AddWithValue("name", tableName);
        return Assert.IsType<bool>(await command.ExecuteScalarAsync());
    }

    private static async Task AssertReceiptFingerprintRejectedAsync(
        string connectionString,
        string fingerprintColumn)
    {
        const string validFingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        string[] columns = ["ToolCallIdFingerprint", "RequestFingerprint", "ResultSetFingerprint"];
        Assert.Contains(fingerprintColumn, columns);
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using (NpgsqlCommand disable = new(
            "ALTER TABLE \"vulgata\".\"Pass2RetrievalReceipts\" DISABLE TRIGGER ALL;",
            connection))
        {
            await disable.ExecuteNonQueryAsync();
        }

        try
        {
            await using NpgsqlCommand insert = new(
                """
                INSERT INTO "vulgata"."Pass2RetrievalReceipts"
                    ("Id", "Pass2JobExecutionId", "AttemptOrdinal", "CallOrdinal",
                     "ToolCallIdFingerprint", "State", "Operation", "RequestFingerprint",
                     "ResultCount", "ReturnedUtf8Bytes", "NewAdditionalDocumentCount",
                     "ResultSetFingerprint", "CreatedAt")
                VALUES (@id, @job, 1, 1, @tool, 'NoResults', 'Search', @request, 0, 0, 0, @result, CURRENT_TIMESTAMP);
                """,
                connection);
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("job", Guid.NewGuid());
            insert.Parameters.AddWithValue("tool", fingerprintColumn == "ToolCallIdFingerprint"
                ? new string('g', 64) : validFingerprint);
            insert.Parameters.AddWithValue("request", fingerprintColumn == "RequestFingerprint"
                ? new string('g', 64) : validFingerprint);
            insert.Parameters.AddWithValue("result", fingerprintColumn == "ResultSetFingerprint"
                ? new string('g', 64) : validFingerprint);

            PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
                () => insert.ExecuteNonQueryAsync());
            Assert.Contains("Fingerprints", exception.ConstraintName, StringComparison.Ordinal);
        }
        finally
        {
            await using NpgsqlCommand enable = new(
                "ALTER TABLE \"vulgata\".\"Pass2RetrievalReceipts\" ENABLE TRIGGER ALL;",
                connection);
            await enable.ExecuteNonQueryAsync();
        }
    }

    private static async Task AssertResultFingerprintRejectedAsync(
        string connectionString,
        string fingerprintColumn)
    {
        const string validFingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        Assert.Contains(fingerprintColumn, new[] { "DescriptorFingerprint", "EvidenceFingerprint" });
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using (NpgsqlCommand disable = new(
            "ALTER TABLE \"vulgata\".\"Pass2RetrievalResults\" DISABLE TRIGGER ALL;",
            connection))
        {
            await disable.ExecuteNonQueryAsync();
        }

        try
        {
            await using NpgsqlCommand insert = new(
                """
                INSERT INTO "vulgata"."Pass2RetrievalResults"
                    ("Id", "ReceiptId", "Pass2JobExecutionId", "AttemptOrdinal", "ResultOrdinal",
                     "State", "Kind", "DocumentVersionId", "RepositoryId", "ScanRunId",
                     "DescriptorFingerprint", "EvidenceFingerprint", "ReturnedUtf8Bytes", "CreatedAt")
                VALUES (@id, @receipt, @job, 1, 1, 'Admitted', 'Document', @document,
                        @repository, @scan, @descriptor, @evidence, 0, CURRENT_TIMESTAMP);
                """,
                connection);
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("receipt", Guid.NewGuid());
            insert.Parameters.AddWithValue("job", Guid.NewGuid());
            insert.Parameters.AddWithValue("document", Guid.NewGuid());
            insert.Parameters.AddWithValue("repository", Guid.NewGuid());
            insert.Parameters.AddWithValue("scan", Guid.NewGuid());
            insert.Parameters.AddWithValue("descriptor", fingerprintColumn == "DescriptorFingerprint"
                ? new string('g', 64) : validFingerprint);
            insert.Parameters.AddWithValue("evidence", fingerprintColumn == "EvidenceFingerprint"
                ? new string('g', 64) : validFingerprint);

            PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
                () => insert.ExecuteNonQueryAsync());
            Assert.Contains("Fingerprints", exception.ConstraintName, StringComparison.Ordinal);
        }
        finally
        {
            await using NpgsqlCommand enable = new(
                "ALTER TABLE \"vulgata\".\"Pass2RetrievalResults\" ENABLE TRIGGER ALL;",
                connection);
            await enable.ExecuteNonQueryAsync();
        }
    }

    private static async Task AssertDatabaseDroppedAsync(
        string administratorConnectionString,
        string databaseName)
    {
        await using NpgsqlConnection connection = new(administratorConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection);
        command.Parameters.AddWithValue("name", databaseName);
        Assert.False(Assert.IsType<bool>(await command.ExecuteScalarAsync()));
    }
}

/// <summary>
/// PostgreSQL qualification is opt-in. xUnit discovery reports the missing
/// administrator connection as a skipped test, while setting the approved
/// switch enables the same test body without changing the default suite.
/// </summary>
internal sealed class PostgreSqlQualificationFactAttribute : FactAttribute
{
    public PostgreSqlQualificationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN")))
        {
            Skip = "PostgreSQL qualification not run: set VULGATA_TEST_POSTGRES_ADMIN.";
        }
    }
}

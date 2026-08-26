using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.Migrations;
using Xunit.Abstractions;

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

    [Fact]
    public async Task Story124PostgreSql17_FreshApplyRollbackReapply_PreservesLegacySchema()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            output.WriteLine(
                "Qualification not executed: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
            return;
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

    [Fact]
    public async Task Story124PostgreSql17_ConstraintsEnforceNullablePrepublicationAndReusableEdge()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            output.WriteLine(
                "Qualification not executed: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
            return;
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

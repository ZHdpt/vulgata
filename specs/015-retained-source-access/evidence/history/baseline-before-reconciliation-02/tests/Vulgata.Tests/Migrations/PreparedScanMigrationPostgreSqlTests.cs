using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit.Abstractions;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.Migrations;

/// <summary>
/// Story 12.2 PostgreSQL 17 migration lifecycle qualification. Every test creates
/// and drops an exact-name database through <see cref="DisposablePostgreSqlDatabase"/>.
/// </summary>
public sealed class PreparedScanMigrationPostgreSqlTests(ITestOutputHelper output)
{
    private const string BaselineMigration = "20260818124712_AddDurableScanFoundation";
    private const string StoryMigration = "20260820120000_AddPreparedScanManifest";
    private const string LaterMigration = "20260821114734_AddResumablePass1CodeLogic";
    private const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    private const string Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly Guid SystemId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid RepositoryId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid ScanRunId = Guid.Parse("a3000000-0000-0000-0000-000000000001");
    private static readonly Guid CodeUnitId = Guid.Parse("a4000000-0000-0000-0000-000000000001");
    private static readonly Guid SnapshotId = Guid.Parse("a5000000-0000-0000-0000-000000000001");
    private static readonly Guid ClassificationId = Guid.Parse("a6000000-0000-0000-0000-000000000001");
    private static readonly Guid ScanExecutionId = Guid.Parse("a7000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 18, 12, 34, 56, TimeSpan.Zero);

    [Fact]
    public async Task PreparedScanManifest_PostgreSql17_CleanApplyRollbackReapply_PreservesHistoricalRows()
    {
        string? administratorConnectionString = GetAdministratorConnectionString();
        if (administratorConnectionString is null)
        {
            return;
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        output.WriteLine("Owned PostgreSQL 17 database: {0}", database.DatabaseName);
        string databaseName = database.DatabaseName;
        try
        {
            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                IMigrator migrator = context.GetService<IMigrator>();

                // Clean database -> current Story 12.2 migration.
                await migrator.MigrateAsync(StoryMigration);
                Assert.Contains(StoryMigration, await context.Database.GetAppliedMigrationsAsync());
                Assert.Equal([LaterMigration], await context.Database.GetPendingMigrationsAsync());
                await AssertPreparedSchemaPresentAsync(context);

                // Return to the Story 12.1 boundary so protected legacy rows can be
                // inserted with their original relational shape.
                await migrator.MigrateAsync(BaselineMigration);
                await AssertPreparedSchemaAbsentAsync(context);
            }

            await SeedProtectedHistoricalRowsAsync(database.ConnectionString);

            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                IMigrator migrator = context.GetService<IMigrator>();
                Assert.Equal(
                    [StoryMigration, LaterMigration],
                    await context.Database.GetPendingMigrationsAsync());

                await migrator.MigrateAsync(StoryMigration);
                Assert.Equal([LaterMigration], await context.Database.GetPendingMigrationsAsync());
                await AssertPreparedSchemaPresentAsync(context);
                await AssertProtectedHistoricalRowsAsync(database.ConnectionString, preparedColumnsExist: true);

                await migrator.MigrateAsync(BaselineMigration);
                Assert.DoesNotContain(StoryMigration, await context.Database.GetAppliedMigrationsAsync());
                await AssertPreparedSchemaAbsentAsync(context);
                await AssertProtectedHistoricalRowsAsync(database.ConnectionString, preparedColumnsExist: false);

                await migrator.MigrateAsync(StoryMigration);
                Assert.Contains(StoryMigration, await context.Database.GetAppliedMigrationsAsync());
                Assert.Equal([LaterMigration], await context.Database.GetPendingMigrationsAsync());
                await AssertPreparedSchemaPresentAsync(context);
                await AssertProtectedHistoricalRowsAsync(database.ConnectionString, preparedColumnsExist: true);
            }
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
    }

    [Fact]
    public async Task PreparedScanManifest_PostgreSql17_ForcedLateBoundaryFailure_RollsBackWholeMigration()
    {
        string? administratorConnectionString = GetAdministratorConnectionString();
        if (administratorConnectionString is null)
        {
            return;
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        output.WriteLine("Owned PostgreSQL 17 database: {0}", database.DatabaseName);
        string databaseName = database.DatabaseName;
        try
        {
            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                await context.GetService<IMigrator>().MigrateAsync(BaselineMigration);
            }

            await SeedProtectedHistoricalRowsAsync(database.ConnectionString);
            await ExecuteSqlAsync(
                database.ConnectionString,
                "CREATE TABLE vulgata.\"PreparedScanAudits\" (\"Sentinel\" integer NOT NULL);");

            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
                    () => context.GetService<IMigrator>().MigrateAsync(StoryMigration));
                Assert.Equal(PostgresErrorCodes.DuplicateTable, exception.SqlState);
            }

            // PreparedScanAudits is created after several columns, constraints and
            // principal tables. Its forced collision proves PostgreSQL rolled the
            // entire EF migration transaction back, not merely the last statement.
            Assert.Equal(
                0,
                await ScalarAsync<int>(database.ConnectionString, $"""
                    SELECT count(*)::integer
                    FROM public."__EFMigrationsHistory"
                    WHERE "MigrationId" = '{StoryMigration}';
                    """));
            Assert.False(await ColumnExistsAsync(
                database.ConnectionString, "ScanContextSnapshots", "PreparedScanContextIdentityHash"));
            Assert.False(await ColumnExistsAsync(database.ConnectionString, "CodeUnits", "FileHash"));
            Assert.False(await TableExistsAsync(database.ConnectionString, "PreparedScanManifests"));
            Assert.False(await TableExistsAsync(database.ConnectionString, "PreparedScanManifestEntries"));
            Assert.True(await TableExistsAsync(database.ConnectionString, "PreparedScanAudits"));
            await AssertProtectedHistoricalRowsAsync(database.ConnectionString, preparedColumnsExist: false);

            await ExecuteSqlAsync(
                database.ConnectionString,
                "DROP TABLE vulgata.\"PreparedScanAudits\";");
            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                await context.GetService<IMigrator>().MigrateAsync(StoryMigration);
                Assert.Contains(StoryMigration, await context.Database.GetAppliedMigrationsAsync());
                Assert.Equal([LaterMigration], await context.Database.GetPendingMigrationsAsync());
            }

            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                await AssertPreparedSchemaPresentAsync(context);
            }
            await AssertProtectedHistoricalRowsAsync(database.ConnectionString, preparedColumnsExist: true);
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
    }

    private string? GetAdministratorConnectionString()
    {
        string? value = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(value))
        {
            output.WriteLine(
                "Qualification not executed: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
            return null;
        }

        return value;
    }

    private static VulgataDbContext CreateContext(string connectionString)
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options;
        return new VulgataDbContext(options);
    }

    private static async Task SeedProtectedHistoricalRowsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO vulgata."Systems"
                ("Id", "Name", "NormalizedName", "Description", "Context", "CreatedAt", "UpdatedAt")
            VALUES
                (@systemId, 'T045 protected system', 'T045 PROTECTED SYSTEM',
                 'historical v1 and prepared-demo compatibility', NULL, @createdAt, @createdAt);

            INSERT INTO vulgata."Repositories"
                ("Id", "SystemId", "Name", "NormalizedName", "GitUrl", "Description", "Context", "CreatedAt", "UpdatedAt")
            VALUES
                (@repositoryId, @systemId, 'T045 protected repository', 'T045 PROTECTED REPOSITORY',
                 'https://example.invalid/t045.git', 'prepared-demo historical row', NULL, @createdAt, @createdAt);

            INSERT INTO vulgata."ScanRuns"
                ("Id", "RepositoryId", "Status", "TotalCandidateFileCount", "ParsedFileCount",
                 "ProviderProcessTimeoutSeconds", "StartedAt", "CompletedAt", "DurationMilliseconds",
                 "NeedsHumanReview", "TerminalMessage", "ProfiledRevision")
            VALUES
                (@scanRunId, @repositoryId, 'Completed', 1, 1, 30, @createdAt, @createdAt,
                 0, FALSE, 'Story 12.1 protected row', @profiledRevision);

            INSERT INTO vulgata."CodeUnits"
                ("Id", "ScanRunId", "RepositoryId", "FilePath", "FileIdentityHash", "SourceHash",
                 "LineStart", "LineEnd", "SymbolName", "UnitType", "RangeConfidence", "Language", "Status")
            VALUES
                (@codeUnitId, @scanRunId, @repositoryId, 'src/t045-legacy.cs', @sha256, @sha256,
                 1, 7, 'T045.Legacy', 'Class', 'Exact', 'CSharp', 'Pending');

            INSERT INTO vulgata."PreScanResults"
                ("Id", "RepositoryId", "ScanRunId", "ProfiledRevision", "State", "SchemaVersion",
                 "ApprovedAt", "Fingerprint", "ApprovalMessage", "ConfirmedCommunicationKinds",
                 "TotalConfirmedEvidenceCount", "TotalHintCount", "TotalWarningCount", "EvidenceTruncated")
            VALUES
                (@scanRunId, @repositoryId, @scanRunId, @profiledRevision, 'PreScanApproved', '1.0',
                 @createdAt, @sha256, 'T045 historical v1 approval', 1, 1, 0, 0, FALSE);

            INSERT INTO vulgata."CommunicationKindClassifications"
                ("Id", "PreScanResultId", "Kind", "Level", "Confidence", "Rule", "Provenance",
                 "RelativePath", "Locator", "OccurrenceCount", "Fingerprint")
            VALUES
                (@classificationId, @scanRunId, 'Rpc', 'Confirmed', 'High', 'RpcDirectAnnotation',
                 'StructuralAnnotation', 'src/t045-legacy.cs', 'T045.Legacy:1', 1, @sha256);

            INSERT INTO vulgata."ScanContextSnapshots"
                ("Id", "RepositoryId", "ScanRunId", "ProfiledRevision", "GlobalContext", "SystemContext",
                 "RepositoryContext", "SchemaIdentity", "ContentHash", "CapturedAt")
            VALUES
                (@snapshotId, @repositoryId, @scanRunId, @profiledRevision, 'legacy-global',
                 'legacy-system', 'prepared-demo-context', 'scan-context-snapshot/1.0', @sha256, @createdAt);

            INSERT INTO vulgata."ScanExecutions"
                ("Id", "RepositoryId", "SystemId", "ScanRunId", "PreScanResultId", "ProfiledRevision",
                 "Phase", "InFlightJobId", "CompletedJobCount", "TotalJobCount", "RestartIndicator",
                 "TerminalMessage", "TerminalReasonKind", "StartedAt", "TerminalAt", "StateVersion")
            VALUES
                (@scanExecutionId, @repositoryId, @systemId, @scanRunId, @scanRunId, @profiledRevision,
                 'Completed', NULL, 0, 0, 0, 'prepared-demo historical completion', NULL,
                 @createdAt, @createdAt, 0);
            """, connection, transaction);
        command.Parameters.AddWithValue("systemId", SystemId);
        command.Parameters.AddWithValue("repositoryId", RepositoryId);
        command.Parameters.AddWithValue("scanRunId", ScanRunId);
        command.Parameters.AddWithValue("codeUnitId", CodeUnitId);
        command.Parameters.AddWithValue("snapshotId", SnapshotId);
        command.Parameters.AddWithValue("classificationId", ClassificationId);
        command.Parameters.AddWithValue("scanExecutionId", ScanExecutionId);
        command.Parameters.AddWithValue("profiledRevision", ProfiledRevision);
        command.Parameters.AddWithValue("sha256", Sha256);
        command.Parameters.AddWithValue("createdAt", CreatedAt);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private static async Task AssertProtectedHistoricalRowsAsync(
        string connectionString,
        bool preparedColumnsExist)
    {
        Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
            SELECT count(*)::integer FROM vulgata."CodeUnits" WHERE "Id" = '{CodeUnitId}';
            """));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
            SELECT count(*)::integer FROM vulgata."PreScanResults"
            WHERE "Id" = '{ScanRunId}' AND "SchemaVersion" = '1.0';
            """));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
            SELECT count(*)::integer FROM vulgata."CommunicationKindClassifications"
            WHERE "Id" = '{ClassificationId}';
            """));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
            SELECT count(*)::integer FROM vulgata."ScanExecutions"
            WHERE "Id" = '{ScanExecutionId}' AND "StartedAt" = '{CreatedAt:O}'::timestamptz;
            """));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
            SELECT count(*)::integer FROM vulgata."ScanContextSnapshots"
            WHERE "Id" = '{SnapshotId}' AND "CapturedAt" = '{CreatedAt:O}'::timestamptz;
            """));

        if (!preparedColumnsExist)
        {
            return;
        }

        Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
            SELECT count(*)::integer FROM vulgata."CodeUnits"
            WHERE "Id" = '{CodeUnitId}'
              AND "FileCodeUnitIdentityFingerprint" IS NULL AND "FileHash" IS NULL
              AND "ByteStart" IS NULL AND "ByteEndExclusive" IS NULL
              AND "SegmentOrdinal" IS NULL AND "SegmentCount" IS NULL;
            """));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
            SELECT count(*)::integer FROM vulgata."PreScanResults"
            WHERE "Id" = '{ScanRunId}' AND "SchemaVersion" = '1.0'
              AND "PreparedScanManifestId" IS NULL
              AND "PreparedScanManifestFingerprint" IS NULL
              AND "ContextSnapshotId" IS NULL
              AND "PreparedScanContextIdentityHash" IS NULL
              AND "ContextContentHash" IS NULL
              AND "HandoffSchemaIdentity" IS NULL
              AND "PairPackageFingerprint" IS NULL;
            """));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
            SELECT count(*)::integer FROM vulgata."ScanContextSnapshots"
            WHERE "Id" = '{SnapshotId}' AND "CapturedAt" = '{CreatedAt:O}'::timestamptz
              AND "PreparedScanContextIdentityHash" IS NULL;
            """));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
            SELECT count(*)::integer FROM vulgata."CommunicationKindClassifications"
            WHERE "Id" = '{ClassificationId}' AND "CatalogRuleIdentity" IS NULL;
            """));
    }

    private static async Task AssertPreparedSchemaPresentAsync(VulgataDbContext context)
    {
        Assert.True(await TableExistsAsync(context.Database.GetConnectionString()!, "PreparedScanManifests"));
        Assert.True(await TableExistsAsync(context.Database.GetConnectionString()!, "PreparedScanManifestEntries"));
        Assert.True(await TableExistsAsync(context.Database.GetConnectionString()!, "PreparedScanAudits"));
        Assert.True(await ColumnExistsAsync(
            context.Database.GetConnectionString()!, "ScanContextSnapshots", "PreparedScanContextIdentityHash"));
        Assert.True(await ColumnExistsAsync(
            context.Database.GetConnectionString()!, "CodeUnits", "FileCodeUnitIdentityFingerprint"));
    }

    private static async Task AssertPreparedSchemaAbsentAsync(VulgataDbContext context)
    {
        Assert.False(await TableExistsAsync(context.Database.GetConnectionString()!, "PreparedScanManifests"));
        Assert.False(await TableExistsAsync(context.Database.GetConnectionString()!, "PreparedScanManifestEntries"));
        Assert.False(await TableExistsAsync(context.Database.GetConnectionString()!, "PreparedScanAudits"));
        Assert.False(await ColumnExistsAsync(
            context.Database.GetConnectionString()!, "ScanContextSnapshots", "PreparedScanContextIdentityHash"));
        Assert.False(await ColumnExistsAsync(
            context.Database.GetConnectionString()!, "CodeUnits", "FileCodeUnitIdentityFingerprint"));
    }

    private static Task<bool> TableExistsAsync(string connectionString, string tableName) =>
        ScalarAsync<bool>(connectionString, $"""
            SELECT to_regclass('vulgata."{tableName}"') IS NOT NULL;
            """);

    private static Task<bool> ColumnExistsAsync(
        string connectionString,
        string tableName,
        string columnName) =>
        ScalarAsync<bool>(connectionString, $"""
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'vulgata'
                  AND table_name = '{tableName}'
                  AND column_name = '{columnName}');
            """);

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)Convert.ChangeType(await command.ExecuteScalarAsync() ?? default(T)!, typeof(T));
    }

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertDatabaseDroppedAsync(
        string administratorConnectionString,
        string databaseName)
    {
        await using var administrator = new NpgsqlConnection(administratorConnectionString);
        await administrator.OpenAsync();
        await using var exists = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            administrator);
        exists.Parameters.AddWithValue("name", databaseName);
        Assert.False((bool)(await exists.ExecuteScalarAsync() ?? true));
    }
}

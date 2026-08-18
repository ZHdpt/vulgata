using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using SystemEntity = Vulgata.Core.Entities.System;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Tests.Migrations;

/// <summary>
/// T047 real PostgreSQL lifecycle qualification.  The database is always
/// created and dropped through <see cref="DisposablePostgreSqlDatabase"/>.
/// </summary>
public sealed class DurableScanMigrationPostgreSqlTests
{
    private const string BaselineMigration = "20260803140908_AddExactCrossRepositoryResolution";
    private const string StoryMigration = "20260818124712_AddDurableScanFoundation";
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SystemId = Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid SourceRepositoryId = Guid.Parse("92000000-0000-0000-0000-000000000001");
    private static readonly Guid TargetRepositoryId = Guid.Parse("92000000-0000-0000-0000-000000000002");
    private static readonly Guid SourceScanRunId = Guid.Parse("93000000-0000-0000-0000-000000000001");
    private static readonly Guid TargetScanRunId = Guid.Parse("93000000-0000-0000-0000-000000000002");
    private static readonly Guid SourceCodeUnitId = Guid.Parse("94000000-0000-0000-0000-000000000001");
    private static readonly Guid TargetCodeUnitId = Guid.Parse("94000000-0000-0000-0000-000000000002");
    private static readonly Guid SourceLogicalDocumentId = Guid.Parse("95000000-0000-0000-0000-000000000001");
    private static readonly Guid TargetLogicalDocumentId = Guid.Parse("95000000-0000-0000-0000-000000000002");
    private static readonly Guid SourceDocumentVersionId = Guid.Parse("96000000-0000-0000-0000-000000000001");
    private static readonly Guid TargetDocumentVersionId = Guid.Parse("96000000-0000-0000-0000-000000000002");
    private static readonly Guid SourceAnchorId = Guid.Parse("97000000-0000-0000-0000-000000000001");
    private static readonly Guid TargetAnchorId = Guid.Parse("97000000-0000-0000-0000-000000000002");
    private static readonly Guid EdgeId = Guid.Parse("98000000-0000-0000-0000-000000000001");
    private static readonly Guid UncertaintyId = Guid.Parse("99000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyScanExecutionId = Guid.Parse("9a000000-0000-0000-0000-000000000001");
    private static readonly Guid PreparedScanExecutionId = Guid.Parse("9b000000-0000-0000-0000-000000000001");
    private const string ProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    private const string SourceHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task DurableScanFoundation_PostgreSql17_ApplyRollbackReapply_PreservesProtectedRows()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            return;
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        try
        {
            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                Assert.Equal(17, await ReadPostgreSqlMajorAsync(context));
                IMigrator migrator = context.GetService<IMigrator>();
                await migrator.MigrateAsync(BaselineMigration);
                Assert.Contains(BaselineMigration, await context.Database.GetAppliedMigrationsAsync());
                await SeedProtectedRowsAsync(context);
                await InsertLegacyScanExecutionAsync(context);
                await AssertProtectedRowsAsync(context);
            }

            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                IMigrator migrator = context.GetService<IMigrator>();
                Assert.Equal([StoryMigration], await context.Database.GetPendingMigrationsAsync());
                await migrator.MigrateAsync(StoryMigration);
                Assert.Contains(StoryMigration, await context.Database.GetAppliedMigrationsAsync());
                await AssertProtectedRowsAsync(context);
                await AssertLegacyScanReadableAsync(context);
                Assert.False(context.Database.HasPendingModelChanges());
                Assert.NotNull(await ReadTableRegClassAsync(context, "ScanContextSnapshots"));
                Assert.NotNull(await ReadTableRegClassAsync(context, "ScanJobExecutions"));

                await migrator.MigrateAsync(BaselineMigration);
                await AssertProtectedRowsWithRawSqlAsync(context);
                await AssertLegacyScanReadableWithRawSqlAsync(context);
                Assert.Null(await ReadTableRegClassAsync(context, "ScanContextSnapshots"));
                Assert.Null(await ReadTableRegClassAsync(context, "ScanJobExecutions"));

                await migrator.MigrateAsync(StoryMigration);
                Assert.Contains(StoryMigration, await context.Database.GetAppliedMigrationsAsync());
                Assert.False(context.Database.HasPendingModelChanges());
                await AssertProtectedRowsAsync(context);
                await AssertLegacyScanReadableAsync(context);
                await SeedPreparedCompatibilityRowAsync(context);
                Assert.Equal(
                    ScanExecutionPhase.Prepared,
                    await context.ScanExecutions
                        .Where(execution => execution.Id == PreparedScanExecutionId)
                        .Select(execution => execution.Phase)
                        .SingleAsync());
            }
        }
        finally
        {
            await database.DisposeAsync();
        }

        await using var administrator = new NpgsqlConnection(administratorConnectionString);
        await administrator.OpenAsync();
        await using var exists = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            administrator);
        exists.Parameters.AddWithValue("name", databaseName);
        Assert.False((bool)(await exists.ExecuteScalarAsync() ?? true));
    }

    private static VulgataDbContext CreateContext(string connectionString)
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options;
        return new VulgataDbContext(options);
    }

    private static async Task<int> ReadPostgreSqlMajorAsync(VulgataDbContext context)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = new NpgsqlCommand(
            "SELECT (current_setting('server_version_num')::integer / 10000);",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task SeedProtectedRowsAsync(VulgataDbContext context)
    {
        SystemEntity system = new("T047 protected system", null, "prepared-demo-compatible", CreatedAt);
        SetId(system, SystemId);
        RepositoryEntity sourceRepository = RepositoryEntity.Create(
            SystemId, "T047 source", "https://example.invalid/t047-source.git", null, null, CreatedAt);
        RepositoryEntity targetRepository = RepositoryEntity.Create(
            SystemId, "T047 target", "https://example.invalid/t047-target.git", null, null, CreatedAt);
        SetId(sourceRepository, SourceRepositoryId);
        SetId(targetRepository, TargetRepositoryId);
        ScanRun sourceRun = CreateCompletedScanRun(SourceScanRunId, SourceRepositoryId);
        ScanRun targetRun = CreateCompletedScanRun(TargetScanRunId, TargetRepositoryId);
        CodeUnit sourceUnit = CreateCodeUnit(SourceCodeUnitId, SourceScanRunId, SourceRepositoryId, "src/t047-source.cs");
        CodeUnit targetUnit = CreateCodeUnit(TargetCodeUnitId, TargetScanRunId, TargetRepositoryId, "src/t047-target.cs");

        context.Systems.Add(system);
        context.Repositories.AddRange(sourceRepository, targetRepository);
        context.ScanRuns.AddRange(sourceRun, targetRun);
        context.CodeUnits.AddRange(sourceUnit, targetUnit);
        await context.SaveChangesAsync();

        LogicalDocument sourceLogical = LogicalDocument.Create(
            SourceLogicalDocumentId, SourceRepositoryId, DocumentType.CodeLogic, "t047/source", CreatedAt);
        LogicalDocument targetLogical = LogicalDocument.Create(
            TargetLogicalDocumentId, TargetRepositoryId, DocumentType.BusinessLogic, "t047/target", CreatedAt);
        context.LogicalDocuments.AddRange(sourceLogical, targetLogical);
        await context.SaveChangesAsync();

        DocumentVersion sourceVersion = DocumentVersion.Create(
            SourceDocumentVersionId, SourceLogicalDocumentId, SourceRepositoryId, SystemId, SourceScanRunId,
            ProfiledRevision, 1, "T047 Source", "src/t047-source.cs", "vulgata.knowledge-document/1.0",
            "{\"prepared\":true,\"kind\":\"source\"}", "# T047 Source", SourceHash, SourceHash, CreatedAt);
        DocumentVersion targetVersion = DocumentVersion.Create(
            TargetDocumentVersionId, TargetLogicalDocumentId, TargetRepositoryId, SystemId, TargetScanRunId,
            ProfiledRevision, 1, "T047 Target", "src/t047-target.cs", "vulgata.knowledge-document/1.0",
            "{\"prepared\":true,\"kind\":\"target\"}", "# T047 Target", SourceHash, SourceHash, CreatedAt);
        context.DocumentVersions.AddRange(sourceVersion, targetVersion);
        await context.SaveChangesAsync();
        sourceLogical.SetInitialCurrentVersion(sourceVersion);
        targetLogical.SetInitialCurrentVersion(targetVersion);
        await context.SaveChangesAsync();

        SourceAnchor sourceAnchor = new(
            SourceAnchorId, SourceDocumentVersionId, SourceRepositoryId, SourceScanRunId, SourceCodeUnitId,
            ProfiledRevision, "src/t047-source.cs", "T047.Source:1-10", 1, 10, SourceHash, CreatedAt);
        SourceAnchor targetAnchor = new(
            TargetAnchorId, TargetDocumentVersionId, TargetRepositoryId, TargetScanRunId, TargetCodeUnitId,
            ProfiledRevision, "src/t047-target.cs", "T047.Target:1-10", 1, 10, SourceHash, CreatedAt);
        DocumentEdge edge = new(
            EdgeId, SourceDocumentVersionId, SourceRepositoryId, SourceScanRunId,
            TargetDocumentVersionId, TargetRepositoryId, TargetScanRunId,
            DocumentEdgeType.GeneratedFrom, CreatedAt);
        Uncertainty uncertainty = new(
            UncertaintyId, SourceDocumentVersionId, SourceRepositoryId, SourceScanRunId, SourceAnchorId,
            "cross_repository", "unknown_target", "unmatched", null, null, null, TargetRepositoryId,
            "1.0", "demo", "1", SourceHash, "t047-occurrence", "T047.Source:1-10", SourceHash, CreatedAt);
        context.SourceAnchors.AddRange(sourceAnchor, targetAnchor);
        context.DocumentEdges.Add(edge);
        context.Uncertainties.Add(uncertainty);
        await context.SaveChangesAsync();
    }

    private static async Task InsertLegacyScanExecutionAsync(VulgataDbContext context)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO vulgata."ScanExecutions"
                ("Id", "RepositoryId", "SystemId", "ScanRunId", "PreScanResultId", "ProfiledRevision",
                 "Phase", "InFlightJobId", "CompletedJobCount", "TotalJobCount", "RestartIndicator",
                 "TerminalMessage", "TerminalReasonKind", "StartedAt", "TerminalAt")
            VALUES ({LegacyScanExecutionId}, {SourceRepositoryId}, {SystemId}, {SourceScanRunId},
                    {SourceScanRunId}, {ProfiledRevision}, 'Completed', NULL, 0, 0, 0, NULL, NULL,
                    {CreatedAt}, {CreatedAt});
            """);
    }

    private static async Task SeedPreparedCompatibilityRowAsync(VulgataDbContext context)
    {
        PreScanResult preScan = PreScanResult.CreateApproved(
            TargetRepositoryId, TargetScanRunId, ProfiledRevision, CreatedAt,
            SourceHash, "预扫描已批准——继续进入顺序演示运行器",
            ConfirmedCommunicationKinds.None, 0, 0, 0, false, []);
        context.PreScanResults.Add(preScan);
        await context.SaveChangesAsync();

        string runtimeIdentity = "{\"schemaVersion\":\"analysis-runtime/1.0\",\"preparedDemo\":true}";
        string runtimeHash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(runtimeIdentity)));
        ScanExecution execution = ScanExecution.CreateNewPath(
            PreparedScanExecutionId, TargetRepositoryId, SystemId,
            TargetScanRunId, ProfiledRevision, "t047-prompt", "vulgata.knowledge-document/1.0",
            runtimeIdentity, runtimeHash, CreatedAt.AddMinutes(1));
        context.ScanExecutions.Add(execution);
        await context.SaveChangesAsync();
    }

    private static async Task AssertProtectedRowsAsync(VulgataDbContext context)
    {
        Assert.Equal(2, await context.DocumentVersions.CountAsync());
        Assert.Equal(2, await context.SourceAnchors.CountAsync());
        Assert.Equal(1, await context.DocumentEdges.CountAsync());
        Assert.Equal(1, await context.Uncertainties.CountAsync());
        string sourceContent = await context.DocumentVersions
            .Where(value => value.Id == SourceDocumentVersionId)
            .Select(value => value.ContentJson)
            .SingleAsync();
        using System.Text.Json.JsonDocument sourceDocument =
            System.Text.Json.JsonDocument.Parse(sourceContent);
        Assert.True(sourceDocument.RootElement.GetProperty("prepared").GetBoolean());
        Assert.Equal("source", sourceDocument.RootElement.GetProperty("kind").GetString());
    }

    private static async Task AssertProtectedRowsWithRawSqlAsync(VulgataDbContext context)
    {
        Assert.Equal(2, await CountRowsAsync(context, "DocumentVersions"));
        Assert.Equal(2, await CountRowsAsync(context, "SourceAnchors"));
        Assert.Equal(1, await CountRowsAsync(context, "DocumentEdges"));
        Assert.Equal(1, await CountRowsAsync(context, "Uncertainties"));
    }

    private static async Task AssertLegacyScanReadableAsync(VulgataDbContext context)
    {
        Assert.Equal(ScanExecutionPhase.Completed,
            await context.ScanExecutions.Where(value => value.Id == LegacyScanExecutionId)
                .Select(value => value.Phase).SingleAsync());
    }

    private static async Task AssertLegacyScanReadableWithRawSqlAsync(VulgataDbContext context)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = new NpgsqlCommand(
            "SELECT \"Phase\" FROM vulgata.\"ScanExecutions\" WHERE \"Id\" = @id;", connection);
        command.Parameters.AddWithValue("id", LegacyScanExecutionId);
        Assert.Equal("Completed", await command.ExecuteScalarAsync());
    }

    private static async Task<long> CountRowsAsync(
        VulgataDbContext context,
        string table,
        bool expectMissing = false)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM vulgata.\"{table}\";", connection);
        try
        {
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        catch (PostgresException) when (expectMissing)
        {
            return 0;
        }
    }

    private static async Task<string?> ReadTableRegClassAsync(VulgataDbContext context, string table)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = new NpgsqlCommand(
            "SELECT to_regclass(@qualified)::text;", connection);
        command.Parameters.AddWithValue("qualified", $"vulgata.\"{table}\"");
        object? value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private static ScanRun CreateCompletedScanRun(Guid id, Guid repositoryId)
    {
        ScanRun run = ScanRun.Start(repositoryId, 1_800, CreatedAt);
        SetId(run, id);
        run.BindProfiledRevision(ProfiledRevision);
        run.SetCandidateCount(0);
        run.Complete(0, 0, CreatedAt.AddSeconds(1));
        return run;
    }

    private static CodeUnit CreateCodeUnit(Guid id, Guid scanRunId, Guid repositoryId, string path)
    {
        CodeUnit unit = new(
            scanRunId, repositoryId, path, SourceHash, SourceHash, 1, 10,
            Path.GetFileNameWithoutExtension(path), CodeUnitType.Class, RangeConfidence.Exact, "csharp");
        SetId(unit, id);
        return unit;
    }

    private static void SetId<T>(T value, Guid id)
    {
        typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)!.SetValue(value, id);
    }
}

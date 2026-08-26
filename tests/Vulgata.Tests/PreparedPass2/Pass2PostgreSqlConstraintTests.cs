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
    private const string Story123BaselineMigration = "20260821114734_AddResumablePass1CodeLogic";
    private const string MigrationSuffix = "_ExactCrossRepositoryFlowPass2";
    private const string LegacyProfiledRevision = "0123456789abcdef0123456789abcdef01234567";
    private const string LegacySha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly Guid LegacySystemId = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyConsumerRepositoryId = Guid.Parse("b2000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyProviderRepositoryId = Guid.Parse("b2000000-0000-0000-0000-000000000002");
    private static readonly Guid LegacyConsumerScanRunId = Guid.Parse("b3000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyProviderScanRunId = Guid.Parse("b3000000-0000-0000-0000-000000000002");
    private static readonly Guid LegacyConsumerCodeUnitId = Guid.Parse("b4000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyProviderCodeUnitId = Guid.Parse("b4000000-0000-0000-0000-000000000002");
    private static readonly Guid LegacyConsumerLogicalDocumentId = Guid.Parse("b5000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyProviderLogicalDocumentId = Guid.Parse("b5000000-0000-0000-0000-000000000002");
    private static readonly Guid LegacyBusinessLogicalDocumentId = Guid.Parse("b5000000-0000-0000-0000-000000000003");
    private static readonly Guid LegacyConsumerDocumentVersionId = Guid.Parse("b6000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyProviderDocumentVersionId = Guid.Parse("b6000000-0000-0000-0000-000000000002");
    private static readonly Guid LegacyBusinessDocumentVersionId = Guid.Parse("b6000000-0000-0000-0000-000000000003");
    private static readonly Guid LegacyConsumerAnchorId = Guid.Parse("b7000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyProviderAnchorId = Guid.Parse("b7000000-0000-0000-0000-000000000002");
    private static readonly Guid LegacyEdgeId = Guid.Parse("b8000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyUncertaintyId = Guid.Parse("b9000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyFlowGroupId = Guid.Parse("ba000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset LegacyCreatedAt =
        new(2026, 8, 21, 12, 34, 56, TimeSpan.Zero);

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

            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                string[] applied = [.. await context.Database.GetAppliedMigrationsAsync()];
                string migrationId = applied.Single(migration =>
                    migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));
                int storyMigrationIndex = Array.IndexOf(applied, migrationId);
                Assert.True(storyMigrationIndex > 0);
                string baseline = applied[storyMigrationIndex - 1];
                await context.Database.GetService<IMigrator>().MigrateAsync(baseline);
                Assert.False(await TableExistsAsync(
                    database.ConnectionString,
                    "CrossRepositoryResolutionDecisions"));
                Assert.True(await TableExistsAsync(
                    database.ConnectionString,
                    "ExactCrossRepositoryEdgeEvidence"));
            }

            await SeedLegacyPreparedDemoFixtureAsync(database.ConnectionString);
            await AssertLegacyPreparedDemoFixtureAsync(
                database.ConnectionString,
                story124ColumnsPresent: false);

            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                await context.Database.MigrateAsync();
                await AssertV2SchemaAsync(database.ConnectionString);
                await AssertLegacyPreparedDemoFixtureAsync(
                    database.ConnectionString,
                    story124ColumnsPresent: true);
                await AssertLegacyPreparedDemoFixtureReadableThroughCurrentModelAsync(context);
            }

            await using (VulgataDbContext context = CreateContext(database.ConnectionString))
            {
                await context.Database.GetService<IMigrator>()
                    .MigrateAsync(Story123BaselineMigration);
                await AssertLegacyPreparedDemoFixtureAsync(
                    database.ConnectionString,
                    story124ColumnsPresent: false);
                await context.Database.MigrateAsync();
                await AssertV2SchemaAsync(database.ConnectionString);
                await AssertLegacyPreparedDemoFixtureAsync(
                    database.ConnectionString,
                    story124ColumnsPresent: true);
                await AssertLegacyPreparedDemoFixtureReadableThroughCurrentModelAsync(context);
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

    private static async Task SeedLegacyPreparedDemoFixtureAsync(string connectionString)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await using NpgsqlCommand command = new(
            """
            INSERT INTO vulgata."Systems"
                ("Id", "Name", "NormalizedName", "Description", "Context", "CreatedAt", "UpdatedAt")
            VALUES
                (@systemId, 'Story 12.4 legacy system', 'STORY 12.4 LEGACY SYSTEM',
                 'protected prepared-demo migration fixture', NULL, @createdAt, @createdAt);

            INSERT INTO vulgata."Repositories"
                ("Id", "SystemId", "Name", "NormalizedName", "GitUrl", "Description", "Context", "CreatedAt", "UpdatedAt")
            VALUES
                (@consumerRepositoryId, @systemId, 'Legacy consumer', 'LEGACY CONSUMER',
                 'https://example.invalid/legacy-consumer.git', 'protected consumer', NULL, @createdAt, @createdAt),
                (@providerRepositoryId, @systemId, 'Legacy provider', 'LEGACY PROVIDER',
                 'https://example.invalid/legacy-provider.git', 'protected provider', NULL, @createdAt, @createdAt);

            INSERT INTO vulgata."ScanRuns"
                ("Id", "RepositoryId", "Status", "TotalCandidateFileCount", "ParsedFileCount",
                 "ProviderProcessTimeoutSeconds", "StartedAt", "CompletedAt", "DurationMilliseconds",
                 "NeedsHumanReview", "TerminalMessage", "ProfiledRevision")
            VALUES
                (@consumerScanRunId, @consumerRepositoryId, 'Completed', 1, 1, 30, @createdAt, @createdAt,
                 0, FALSE, 'protected legacy consumer scan', @profiledRevision),
                (@providerScanRunId, @providerRepositoryId, 'Completed', 1, 1, 30, @createdAt, @createdAt,
                 0, FALSE, 'protected legacy provider scan', @profiledRevision);

            INSERT INTO vulgata."CodeUnits"
                ("Id", "ScanRunId", "RepositoryId", "FilePath", "FileIdentityHash", "SourceHash",
                 "LineStart", "LineEnd", "SymbolName", "UnitType", "RangeConfidence", "Language", "Status")
            VALUES
                (@consumerCodeUnitId, @consumerScanRunId, @consumerRepositoryId,
                 'src/legacy-consumer.cs', @sha256, @sha256, 1, 7, 'Legacy.Consumer', 'Class', 'Exact', 'CSharp', 'Pending'),
                (@providerCodeUnitId, @providerScanRunId, @providerRepositoryId,
                 'src/legacy-provider.cs', @sha256, @sha256, 1, 7, 'Legacy.Provider', 'Class', 'Exact', 'CSharp', 'Pending');

            INSERT INTO vulgata."LogicalDocuments"
                ("Id", "RepositoryId", "DocumentType", "StableKey", "CurrentVersionId", "CreatedAt")
            VALUES
                (@consumerLogicalDocumentId, @consumerRepositoryId, 'CodeLogic', 'legacy-consumer-code', NULL, @createdAt),
                (@providerLogicalDocumentId, @providerRepositoryId, 'CodeLogic', 'legacy-provider-code', NULL, @createdAt),
                (@businessLogicalDocumentId, @consumerRepositoryId, 'BusinessLogic', 'legacy-business-flow', NULL, @createdAt);

            INSERT INTO vulgata."DocumentVersions"
                ("Id", "LogicalDocumentId", "RepositoryId", "SystemId", "ScanRunId", "ProfiledRevision",
                 "VersionNumber", "Title", "RepositoryRelativePath", "SchemaIdentity", "ContentJson",
                 "ContentMarkdown", "ContentFingerprint", "CommitFingerprint", "SupersededById", "CreatedAt")
            VALUES
                (@consumerDocumentVersionId, @consumerLogicalDocumentId, @consumerRepositoryId, @systemId,
                 @consumerScanRunId, @profiledRevision, 1, 'Legacy consumer code', 'src/legacy-consumer.cs',
                 'vulgata.knowledge-document/1.0', '{}'::jsonb, 'legacy consumer', @sha256, @sha256, NULL, @createdAt),
                (@providerDocumentVersionId, @providerLogicalDocumentId, @providerRepositoryId, @systemId,
                 @providerScanRunId, @profiledRevision, 1, 'Legacy provider code', 'src/legacy-provider.cs',
                 'vulgata.knowledge-document/1.0', '{}'::jsonb, 'legacy provider', @sha256, @sha256, NULL, @createdAt),
                (@businessDocumentVersionId, @businessLogicalDocumentId, @consumerRepositoryId, @systemId,
                 @consumerScanRunId, @profiledRevision, 1, 'Legacy business flow', 'business/legacy-flow.md',
                 'vulgata.knowledge-document/1.0', '{}'::jsonb, 'legacy business flow', @sha256, @sha256, NULL, @createdAt);

            UPDATE vulgata."LogicalDocuments" SET "CurrentVersionId" = @consumerDocumentVersionId
            WHERE "Id" = @consumerLogicalDocumentId;
            UPDATE vulgata."LogicalDocuments" SET "CurrentVersionId" = @providerDocumentVersionId
            WHERE "Id" = @providerLogicalDocumentId;
            UPDATE vulgata."LogicalDocuments" SET "CurrentVersionId" = @businessDocumentVersionId
            WHERE "Id" = @businessLogicalDocumentId;

            INSERT INTO vulgata."SourceAnchors"
                ("Id", "DocumentVersionId", "RepositoryId", "ScanRunId", "CodeUnitId", "ProfiledRevision",
                 "RepositoryRelativePath", "EvidenceLocator", "LineStart", "LineEnd", "SourceHash", "CreatedAt")
            VALUES
                (@consumerAnchorId, @consumerDocumentVersionId, @consumerRepositoryId, @consumerScanRunId,
                 @consumerCodeUnitId, @profiledRevision, 'src/legacy-consumer.cs', 'Legacy.Consumer:1', 1, 7, @sha256, @createdAt),
                (@providerAnchorId, @providerDocumentVersionId, @providerRepositoryId, @providerScanRunId,
                 @providerCodeUnitId, @profiledRevision, 'src/legacy-provider.cs', 'Legacy.Provider:1', 1, 7, @sha256, @createdAt);

            INSERT INTO vulgata."DocumentEdges"
                ("Id", "SourceDocumentVersionId", "SourceRepositoryId", "SourceScanRunId",
                 "TargetDocumentVersionId", "TargetRepositoryId", "TargetScanRunId", "EdgeType", "CreatedAt")
            VALUES
                (@edgeId, @consumerDocumentVersionId, @consumerRepositoryId, @consumerScanRunId,
                 @providerDocumentVersionId, @providerRepositoryId, @providerScanRunId, 'References', @createdAt);

            INSERT INTO vulgata."ExactCrossRepositoryEdgeEvidence"
                ("EdgeId", "ConsumerSourceAnchorId", "ConsumerDocumentVersionId", "ConsumerRepositoryId", "ConsumerScanRunId",
                 "ProviderSourceAnchorId", "ProviderDocumentVersionId", "ProviderRepositoryId", "ProviderScanRunId",
                 "ConsumerRawIdentifier", "ProviderRawIdentifier", "NormalizedEndpointIdentifier", "Protocol",
                 "ResolutionKind", "Confidence", "ProfileSchemaVersion", "ProfileId", "ProfileVersion",
                 "ProfileFingerprint", "OccurrenceId", "ConsumerEvidenceLocator", "ProviderEvidenceLocator",
                 "EvidenceFingerprint", "CreatedAt")
            VALUES
                (@edgeId, @consumerAnchorId, @consumerDocumentVersionId, @consumerRepositoryId, @consumerScanRunId,
                 @providerAnchorId, @providerDocumentVersionId, @providerRepositoryId, @providerScanRunId,
                 'legacy/raw', 'legacy/provider', 'legacy/endpoint', 'CustomRpc', 'ExactConfigured', 1.0000,
                 '1.0', 'legacy-profile', '1.0.0', @sha256, 'legacy-occurrence', 'Legacy.Consumer:1',
                 'Legacy.Provider:1', @evidenceFingerprint, @createdAt);

            INSERT INTO vulgata."Uncertainties"
                ("Id", "DocumentVersionId", "RepositoryId", "ScanRunId", "SourceAnchorId", "Type", "Status",
                 "SubStatus", "ReasonCode", "ObservationKind", "EndpointIdentifier", "EndpointIdentifierNormalized",
                 "TargetRepositoryId", "ProfileSchemaVersion", "ProfileId", "ProfileVersion", "ProfileFingerprint",
                 "OccurrenceId", "EvidenceLocator", "EvidenceFingerprint", "CreatedAt", "ResolvedAt",
                 "ResolvedByDocumentVersionId")
            VALUES
                (@uncertaintyId, @consumerDocumentVersionId, @consumerRepositoryId, @consumerScanRunId,
                 @consumerAnchorId, 'cross_repository', 'unresolved', 'ambiguous', 'multiple_providers', NULL,
                 'legacy/raw', 'legacy/endpoint', @providerRepositoryId, '1.0', 'legacy-profile', '1.0.0',
                 @sha256, 'legacy-uncertainty', 'Legacy.Consumer:1', @uncertaintyFingerprint, @createdAt, NULL, NULL);

            INSERT INTO vulgata."FlowGroups"
                ("Id", "BusinessLogicDocumentVersionId", "RepositoryId", "ScanRunId", "GroupOrder", "Title", "CreatedAt")
            VALUES
                (@flowGroupId, @businessDocumentVersionId, @consumerRepositoryId, @consumerScanRunId,
                 1, 'Legacy prepared-demo flow', @createdAt);
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("systemId", LegacySystemId);
        command.Parameters.AddWithValue("consumerRepositoryId", LegacyConsumerRepositoryId);
        command.Parameters.AddWithValue("providerRepositoryId", LegacyProviderRepositoryId);
        command.Parameters.AddWithValue("consumerScanRunId", LegacyConsumerScanRunId);
        command.Parameters.AddWithValue("providerScanRunId", LegacyProviderScanRunId);
        command.Parameters.AddWithValue("consumerCodeUnitId", LegacyConsumerCodeUnitId);
        command.Parameters.AddWithValue("providerCodeUnitId", LegacyProviderCodeUnitId);
        command.Parameters.AddWithValue("consumerLogicalDocumentId", LegacyConsumerLogicalDocumentId);
        command.Parameters.AddWithValue("providerLogicalDocumentId", LegacyProviderLogicalDocumentId);
        command.Parameters.AddWithValue("businessLogicalDocumentId", LegacyBusinessLogicalDocumentId);
        command.Parameters.AddWithValue("consumerDocumentVersionId", LegacyConsumerDocumentVersionId);
        command.Parameters.AddWithValue("providerDocumentVersionId", LegacyProviderDocumentVersionId);
        command.Parameters.AddWithValue("businessDocumentVersionId", LegacyBusinessDocumentVersionId);
        command.Parameters.AddWithValue("consumerAnchorId", LegacyConsumerAnchorId);
        command.Parameters.AddWithValue("providerAnchorId", LegacyProviderAnchorId);
        command.Parameters.AddWithValue("edgeId", LegacyEdgeId);
        command.Parameters.AddWithValue("uncertaintyId", LegacyUncertaintyId);
        command.Parameters.AddWithValue("flowGroupId", LegacyFlowGroupId);
        command.Parameters.AddWithValue("profiledRevision", LegacyProfiledRevision);
        command.Parameters.AddWithValue("sha256", LegacySha256);
        command.Parameters.AddWithValue("evidenceFingerprint", new string('a', 64));
        command.Parameters.AddWithValue("uncertaintyFingerprint", new string('b', 64));
        command.Parameters.AddWithValue("createdAt", LegacyCreatedAt);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private static async Task AssertLegacyPreparedDemoFixtureAsync(
        string connectionString,
        bool story124ColumnsPresent)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        string flowGroupProjection = story124ColumnsPresent
            ? "AND \"SchemaIdentity\" IS NULL AND \"Pass2ExecutionId\" IS NULL " +
              "AND \"ExactResolutionDecisionId\" IS NULL AND \"PlanFingerprint\" = '' AND \"State\" = ''"
            : string.Empty;
        await using NpgsqlCommand command = new(
            $"""
            SELECT
              (SELECT count(*) FROM vulgata."LogicalDocuments"
               WHERE "Id" IN ('{LegacyConsumerLogicalDocumentId}', '{LegacyProviderLogicalDocumentId}', '{LegacyBusinessLogicalDocumentId}')),
              (SELECT count(*) FROM vulgata."DocumentVersions"
               WHERE "Id" IN ('{LegacyConsumerDocumentVersionId}', '{LegacyProviderDocumentVersionId}', '{LegacyBusinessDocumentVersionId}')),
              (SELECT count(*) FROM vulgata."DocumentEdges"
               WHERE "Id" = '{LegacyEdgeId}' AND "SourceDocumentVersionId" = '{LegacyConsumerDocumentVersionId}'
                 AND "TargetDocumentVersionId" = '{LegacyProviderDocumentVersionId}' AND "EdgeType" = 'References'),
              (SELECT count(*) FROM vulgata."ExactCrossRepositoryEdgeEvidence"
               WHERE "EdgeId" = '{LegacyEdgeId}' AND "ConsumerSourceAnchorId" = '{LegacyConsumerAnchorId}'
                 AND "ProviderSourceAnchorId" = '{LegacyProviderAnchorId}'),
              (SELECT count(*) FROM vulgata."Uncertainties"
               WHERE "Id" = '{LegacyUncertaintyId}' AND "Status" = 'unresolved' AND "ReasonCode" = 'multiple_providers'),
              (SELECT count(*) FROM vulgata."FlowGroups"
               WHERE "Id" = '{LegacyFlowGroupId}' AND "BusinessLogicDocumentVersionId" = '{LegacyBusinessDocumentVersionId}'
                 {flowGroupProjection});
            """,
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(3L, reader.GetInt64(0));
        Assert.Equal(3L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
        Assert.Equal(1L, reader.GetInt64(3));
        Assert.Equal(1L, reader.GetInt64(4));
        Assert.Equal(1L, reader.GetInt64(5));
    }

    private static async Task AssertLegacyPreparedDemoFixtureReadableThroughCurrentModelAsync(
        VulgataDbContext context)
    {
        Assert.True(await context.LogicalDocuments.AsNoTracking()
            .AnyAsync(value => value.Id == LegacyBusinessLogicalDocumentId));
        Assert.True(await context.DocumentVersions.AsNoTracking()
            .AnyAsync(value => value.Id == LegacyBusinessDocumentVersionId));
        Assert.True(await context.DocumentEdges.AsNoTracking()
            .AnyAsync(value => value.Id == LegacyEdgeId));
        Assert.True(await context.ExactCrossRepositoryEdgeEvidence.AsNoTracking()
            .AnyAsync(value => value.EdgeId == LegacyEdgeId));
        Assert.True(await context.Uncertainties.AsNoTracking()
            .AnyAsync(value => value.Id == LegacyUncertaintyId));
        Assert.True(await context.FlowGroups.AsNoTracking()
            .AnyAsync(value => value.Id == LegacyFlowGroupId &&
                value.BusinessLogicDocumentVersionId == LegacyBusinessDocumentVersionId));
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

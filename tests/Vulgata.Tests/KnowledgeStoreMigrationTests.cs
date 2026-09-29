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

public sealed class KnowledgeStoreMigrationTests
{
    private const string _baselineMigration = "20260730083207_AddConservativePreScanGate";
    private const string _migrationSuffix = "_AddMinimalKnowledgeStore";
    private const string _npgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private static readonly string[] _tableNames =
    [
        "DocumentEdges",
        "DocumentSections",
        "DocumentVersions",
        "FlowGroups",
        "FlowTraceSteps",
        "FlowTraces",
        "IndexEntries",
        "LogicalDocuments",
        "SourceAnchors",
    ];

    private static readonly IReadOnlyDictionary<string, string> _checkSqlByName =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CK_DocumentEdges_EdgeType_Closed"] =
                "\"EdgeType\" IN ('References', 'GeneratedFrom')",
            ["CK_DocumentSections_SectionKind_Closed"] =
                "\"SectionKind\" IN ('Overview', 'BusinessRule', 'ProcessStep', 'TechnicalDetail', 'Evidence', 'Other')",
            ["CK_DocumentSections_SectionOrder_Positive"] = "\"SectionOrder\" > 0",
            ["CK_DocumentVersions_CommitFingerprint_Length"] =
                "length(\"CommitFingerprint\") = 64",
            ["CK_DocumentVersions_ContentFingerprint_Length"] =
                "length(\"ContentFingerprint\") = 64",
            ["CK_DocumentVersions_SchemaIdentity_Closed"] =
                "\"SchemaIdentity\" = 'vulgata.knowledge-document/1.0'",
            ["CK_DocumentVersions_SupersededBy_NotSelf"] =
                "\"SupersededById\" IS NULL OR \"SupersededById\" <> \"Id\"",
            ["CK_DocumentVersions_VersionNumber_Positive"] = "\"VersionNumber\" > 0",
            ["CK_FlowGroups_GroupOrder_Positive"] = "\"GroupOrder\" > 0",
            ["CK_FlowTraceSteps_StepKind_Closed"] =
                "\"StepKind\" IN ('Document', 'Relationship', 'Termination')",
            ["CK_FlowTraceSteps_StepOrder_Positive"] = "\"StepOrder\" > 0",
            ["CK_FlowTraces_TraceOrder_Positive"] = "\"TraceOrder\" > 0",
            ["CK_IndexEntries_DocumentType_Closed"] =
                "\"DocumentType\" IN ('CodeLogic', 'BusinessLogic')",
            ["CK_LogicalDocuments_DocumentType_Closed"] =
                "\"DocumentType\" IN ('CodeLogic', 'BusinessLogic')",
            ["CK_SourceAnchors_LineEnd_Range"] = "\"LineEnd\" >= \"LineStart\"",
            ["CK_SourceAnchors_LineStart_Positive"] = "\"LineStart\" > 0",
        };

    private static readonly IReadOnlyDictionary<string, string> _currentModelCheckSqlByName =
        new Dictionary<string, string>(_checkSqlByName, StringComparer.Ordinal)
        {
            ["CK_DocumentVersions_SchemaIdentity_Closed"] =
                "\"SchemaIdentity\" IN ('vulgata.knowledge-document/1.0', 'code-logic/file-v2')",
            ["CK_FlowGroups_Fingerprint_Lengths"] =
                "(\"PlanFingerprint\" = '' OR length(\"PlanFingerprint\") = 64) AND (\"MembershipFingerprint\" IS NULL OR length(\"MembershipFingerprint\") = 64) AND (\"ConsumerEntryStableKey\" IS NULL OR length(\"ConsumerEntryStableKey\") = 64)",
            ["CK_FlowGroups_LogicalKeySchemaIdentity_Closed"] =
                "\"LogicalKeySchemaIdentity\" IS NULL OR \"LogicalKeySchemaIdentity\" = 'business-flow-logical-key/1.0'",
            ["CK_FlowGroups_Protocol_Closed"] =
                "\"Protocol\" IS NULL OR \"Protocol\" IN ('CustomRpc', 'Http')",
            ["CK_FlowGroups_ProviderRepository_CrossRepository"] =
                "\"ProviderRepositoryId\" IS NULL OR \"ProviderRepositoryId\" <> \"RepositoryId\"",
            ["CK_FlowGroups_SchemaIdentity_Closed"] =
                "\"SchemaIdentity\" IS NULL OR \"SchemaIdentity\" = 'endpoint-flow-group/1.0'",
            ["CK_FlowGroups_State_Closed"] =
                "\"SchemaIdentity\" IS NULL OR \"State\" IN ('Complete', 'Incomplete', 'BoundExcluded', 'Running', 'Published', 'Failed')",
            ["CK_FlowTraceSteps_ExactDecision_RelationshipOnly"] =
                "\"ExactResolutionDecisionId\" IS NULL OR \"StepKind\" = 'Relationship'",
            ["CK_SourceAnchors_ByteRange_Shape"] =
                "(\"ByteStart\" IS NULL AND \"ByteEnd\" IS NULL) OR " +
                "(\"ByteStart\" IS NOT NULL AND \"ByteEnd\" IS NOT NULL AND \"ByteStart\" >= 0 AND \"ByteEnd\" > \"ByteStart\")",
        };

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AddMinimalKnowledgeStore_Operations_AreAdditiveCompleteAndReversible()
    {
        using VulgataDbContext db = CreateContext();
        IMigrationsAssembly migrationsAssembly = db.GetService<IMigrationsAssembly>();
        KeyValuePair<string, System.Reflection.TypeInfo> migrationEntry = Assert.Single(
            migrationsAssembly.Migrations,
            entry => entry.Key.EndsWith(_migrationSuffix, StringComparison.Ordinal));
        Migration migration = migrationsAssembly.CreateMigration(
            migrationEntry.Value,
            _npgsqlProvider);

        Type[] permittedUpOperationTypes =
        [
            typeof(CreateTableOperation),
            typeof(CreateIndexOperation),
            typeof(AddForeignKeyOperation),
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
        Assert.Contains(
            foreignKeys,
            foreignKey =>
                foreignKey.Table == "LogicalDocuments" &&
                foreignKey.Columns.SequenceEqual(["CurrentVersionId", "Id", "RepositoryId"]) &&
                foreignKey.PrincipalTable == "DocumentVersions" &&
                foreignKey.PrincipalColumns!.SequenceEqual(
                    ["Id", "LogicalDocumentId", "RepositoryId"]));
        Assert.Contains(
            foreignKeys,
            foreignKey =>
                foreignKey.Table == "SourceAnchors" &&
                foreignKey.Columns.SequenceEqual(
                    ["CodeUnitId", "ScanRunId", "RepositoryId"]) &&
                foreignKey.PrincipalTable == "CodeUnits" &&
                foreignKey.PrincipalColumns!.SequenceEqual(
                    ["Id", "ScanRunId", "RepositoryId"]));

        CreateTableOperation documentVersions = Assert.Single(
            migration.UpOperations.OfType<CreateTableOperation>(),
            operation => operation.Name == "DocumentVersions");
        AddColumnOperation commitFingerprint = Assert.Single(
            documentVersions.Columns,
            column => column.Name == nameof(DocumentVersion.CommitFingerprint));
        Assert.Equal(typeof(string), commitFingerprint.ClrType);
        Assert.False(commitFingerprint.IsNullable);
        Assert.True(commitFingerprint.IsFixedLength);
        Assert.Equal(64, commitFingerprint.MaxLength);

        Dictionary<string, string> migrationCheckSqlByName =
            migration.UpOperations
                .OfType<CreateTableOperation>()
                .SelectMany(operation => operation.CheckConstraints)
                .ToDictionary(check => check.Name, check => check.Sql);
        AssertChecks(migrationCheckSqlByName);

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

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void KnowledgeStoreModel_OwnershipChecksAndIndexes_MatchAcceptedSchema()
    {
        using VulgataDbContext db = CreateContext();
        IModel model = db.GetService<IDesignTimeModel>().Model;
        Type[] entityTypes =
        [
            typeof(LogicalDocument),
            typeof(DocumentVersion),
            typeof(SourceAnchor),
            typeof(DocumentEdge),
            typeof(FlowGroup),
            typeof(FlowTrace),
            typeof(FlowTraceStep),
            typeof(IndexEntry),
            typeof(DocumentSection),
        ];

        Assert.Equal(
            _tableNames,
            entityTypes
                .Select(type => Assert.IsAssignableFrom<IEntityType>(
                    model.FindEntityType(type)))
                .Select(entity => entity.GetTableName())
                .Cast<string>()
                .Order(StringComparer.Ordinal));
        Assert.All(
            entityTypes.Select(type => model.FindEntityType(type)!),
            entity => Assert.All(
                entity.GetForeignKeys(),
                foreignKey => Assert.True(
                    foreignKey.DeleteBehavior is
                        DeleteBehavior.Restrict or DeleteBehavior.NoAction)));
        Dictionary<string, string> modelCheckSqlByName =
            entityTypes
                .Select(type => model.FindEntityType(type)!)
                .SelectMany(entity => entity.GetCheckConstraints())
                .ToDictionary(check => check.Name!, check => check.Sql);
        AssertChecks(modelCheckSqlByName, _currentModelCheckSqlByName);

        IEntityType logicalDocument = model.FindEntityType(typeof(LogicalDocument))!;
        Assert.Contains(
            logicalDocument.GetForeignKeys(),
            foreignKey =>
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(LogicalDocument.CurrentVersionId),
                        nameof(LogicalDocument.Id),
                        nameof(LogicalDocument.RepositoryId),
                    ]) &&
                foreignKey.PrincipalKey.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(DocumentVersion.Id),
                        nameof(DocumentVersion.LogicalDocumentId),
                        nameof(DocumentVersion.RepositoryId),
                    ]));

        IEntityType documentVersion = model.FindEntityType(typeof(DocumentVersion))!;
        IProperty commitFingerprint = Assert.IsAssignableFrom<IProperty>(
            documentVersion.FindProperty(nameof(DocumentVersion.CommitFingerprint)));
        Assert.False(commitFingerprint.IsNullable);
        Assert.True(commitFingerprint.IsFixedLength());
        Assert.Equal(64, commitFingerprint.GetMaxLength());
        AssertIndex(
            logicalDocument,
            true,
            nameof(LogicalDocument.RepositoryId),
            nameof(LogicalDocument.DocumentType),
            nameof(LogicalDocument.StableKey));
        AssertIndex(
            logicalDocument,
            false,
            nameof(LogicalDocument.RepositoryId),
            nameof(LogicalDocument.CurrentVersionId));
        AssertIndex(
            documentVersion,
            true,
            nameof(DocumentVersion.LogicalDocumentId),
            nameof(DocumentVersion.VersionNumber));
        AssertIndex(
            documentVersion,
            true,
            nameof(DocumentVersion.SupersededById));
        AssertIndex(
            documentVersion,
            false,
            nameof(DocumentVersion.RepositoryId),
            nameof(DocumentVersion.ScanRunId));

        IEntityType sourceAnchor = model.FindEntityType(typeof(SourceAnchor))!;
        Assert.Contains(
            sourceAnchor.GetForeignKeys(),
            foreignKey =>
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(SourceAnchor.CodeUnitId),
                        nameof(SourceAnchor.ScanRunId),
                        nameof(SourceAnchor.RepositoryId),
                    ]) &&
                foreignKey.PrincipalKey.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(CodeUnit.Id),
                        nameof(CodeUnit.ScanRunId),
                        nameof(CodeUnit.RepositoryId),
                    ]));
        AssertIndex(
            sourceAnchor,
            false,
            nameof(SourceAnchor.RepositoryId),
            nameof(SourceAnchor.DocumentVersionId),
            nameof(SourceAnchor.CodeUnitId));

        AssertIndex(
            model.FindEntityType(typeof(DocumentEdge))!,
            true,
            nameof(DocumentEdge.SourceDocumentVersionId),
            nameof(DocumentEdge.TargetDocumentVersionId),
            nameof(DocumentEdge.EdgeType));
        AssertIndex(
            model.FindEntityType(typeof(FlowGroup))!,
            true,
            nameof(FlowGroup.BusinessLogicDocumentVersionId),
            nameof(FlowGroup.GroupOrder));
        AssertIndex(
            model.FindEntityType(typeof(FlowTrace))!,
            true,
            nameof(FlowTrace.FlowGroupId),
            nameof(FlowTrace.TraceOrder));
        AssertIndex(
            model.FindEntityType(typeof(FlowTraceStep))!,
            true,
            nameof(FlowTraceStep.FlowTraceId),
            nameof(FlowTraceStep.StepOrder));
        AssertIndex(
            model.FindEntityType(typeof(IndexEntry))!,
            true,
            nameof(IndexEntry.DocumentVersionId));
        IEntityType documentSection = model.FindEntityType(typeof(DocumentSection))!;
        AssertIndex(
            documentSection,
            true,
            nameof(DocumentSection.DocumentVersionId),
            nameof(DocumentSection.StableSectionId));
        AssertIndex(
            documentSection,
            true,
            nameof(DocumentSection.DocumentVersionId),
            nameof(DocumentSection.SectionOrder));

        string[] dbSetNames =
        [
            "LogicalDocuments",
            "DocumentVersions",
            "SourceAnchors",
            "DocumentEdges",
            "FlowGroups",
            "FlowTraces",
            "FlowTraceSteps",
            "IndexEntries",
            "DocumentSections",
        ];
        Assert.All(
            dbSetNames,
            name => Assert.NotNull(typeof(VulgataDbContext).GetProperty(name)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AddMinimalKnowledgeStore_ApplyRollbackAndReapply_PreservesStory43Schema()
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
        AssertStory43Schema(connection);
        Assert.DoesNotContain(
            ReadApplicationTables(connection),
            table => _tableNames.Contains(table, StringComparer.Ordinal));

        migrator.Migrate();
        AssertStory43Schema(connection);
        Assert.Equal(
            _tableNames,
            ReadApplicationTables(connection)
                .Where(table => _tableNames.Contains(table, StringComparer.Ordinal))
                .Order(StringComparer.Ordinal));

        migrator.Migrate(_baselineMigration);
        AssertStory43Schema(connection);
        Assert.DoesNotContain(
            ReadApplicationTables(connection),
            table => _tableNames.Contains(table, StringComparer.Ordinal));

        migrator.Migrate();
        AssertStory43Schema(connection);
        Assert.Equal(
            _tableNames,
            ReadApplicationTables(connection)
                .Where(table => _tableNames.Contains(table, StringComparer.Ordinal))
                .Order(StringComparer.Ordinal));
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

    private static void AssertChecks(
        IReadOnlyDictionary<string, string> actual,
        IReadOnlyDictionary<string, string>? expected = null)
    {
        expected ??= _checkSqlByName;
        Assert.Equal(
            expected.Keys.Order(StringComparer.Ordinal),
            actual.Keys.Order(StringComparer.Ordinal));
        Assert.All(
            expected,
            expected => Assert.Equal(expected.Value, actual[expected.Key]));
    }

    private static void AssertStory43Schema(SqliteConnection connection)
    {
        string[] tables = ReadApplicationTables(connection);
        Assert.Contains("PreScanResults", tables);
        Assert.Contains("CommunicationKindClassifications", tables);

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

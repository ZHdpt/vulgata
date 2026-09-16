using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.PreparedPass2;

public sealed class Pass2MigrationTests
{
    private const string BaselineMigration = "20260821114734_AddResumablePass1CodeLogic";
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
    public void Story124Model_RegistersV2EntitiesAndPrePublicationBindings()
    {
        using VulgataDbContext context = CreateSqliteContext();
        IModel model = context.GetService<IDesignTimeModel>().Model;

        foreach (Type entityType in new[]
        {
            typeof(CrossRepositoryResolutionDecision),
            typeof(ExactResolutionProviderEvidenceContribution),
            typeof(Pass2Execution),
            typeof(Pass2Omission),
            typeof(Pass2JobExecution),
            typeof(Pass2RetrievalBudget),
            typeof(Pass2RetrievalReceipt),
            typeof(Pass2RetrievalResult),
        })
        {
            Assert.NotNull(model.FindEntityType(entityType));
        }

        IEntityType flowGroup = Assert.IsAssignableFrom<IEntityType>(
            model.FindEntityType(typeof(FlowGroup)));
        IProperty businessDocument = Assert.IsAssignableFrom<IProperty>(
            flowGroup.FindProperty(nameof(FlowGroup.BusinessLogicDocumentVersionId)));
        Assert.Equal(typeof(Guid?), businessDocument.ClrType);
        Assert.NotNull(flowGroup.FindProperty(nameof(FlowGroup.LogicalDocumentStableKey)));
        Assert.NotNull(flowGroup.FindProperty(nameof(FlowGroup.State)));
        Assert.NotNull(flowGroup.FindProperty(nameof(FlowGroup.PlanFingerprint)));

        IEntityType logicalDocument = Assert.IsAssignableFrom<IEntityType>(
            model.FindEntityType(typeof(LogicalDocument)));
        Assert.NotNull(logicalDocument.FindProperty(nameof(LogicalDocument.StableKey)));
        Assert.NotNull(logicalDocument.FindProperty(nameof(LogicalDocument.CurrentVersionId)));

        IEntityType edge = Assert.IsAssignableFrom<IEntityType>(
            model.FindEntityType(typeof(DocumentEdge)));
        Assert.Contains(
            edge.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(DocumentEdge.SourceDocumentVersionId),
                    nameof(DocumentEdge.TargetDocumentVersionId),
                    nameof(DocumentEdge.EdgeType),
                ]));
    }

    [Fact]
    public void Story124Migration_IsOneCohesiveAdditiveMigration_WithRollbackOperations()
    {
        using VulgataDbContext context = CreateSqliteContext();
        IMigrationsAssembly assembly = context.GetService<IMigrationsAssembly>();
        string[] storyMigrations = assembly.Migrations.Keys
            .Where(value => value.EndsWith(MigrationSuffix, StringComparison.Ordinal))
            .ToArray();
        Assert.Single(storyMigrations);
        KeyValuePair<string, TypeInfo> entry = Assert.Single(
            assembly.Migrations,
            migration => migration.Key.EndsWith(MigrationSuffix, StringComparison.Ordinal));
        Migration migration = assembly.CreateMigration(entry.Value, "Microsoft.EntityFrameworkCore.Sqlite");

        Assert.Contains(
            migration.UpOperations.OfType<CreateTableOperation>(),
            operation => V2TableNames.Contains(operation.Name, StringComparer.Ordinal));
        Assert.All(
            V2TableNames,
            tableName => Assert.Contains(
                migration.UpOperations.OfType<CreateTableOperation>(),
                operation => operation.Name == tableName));
        CreateTableOperation contributions = Assert.Single(
            migration.UpOperations.OfType<CreateTableOperation>(),
            operation => operation.Name == "ExactResolutionProviderEvidenceContributions");
        Assert.Contains(contributions.Columns, column =>
            column.Name == nameof(ExactResolutionProviderEvidenceContribution.ProviderRawIdentifier) &&
            column.MaxLength == 512 && !column.IsNullable);
        Assert.Contains(contributions.Columns, column =>
            column.Name == nameof(ExactResolutionProviderEvidenceContribution.NormalizedEndpointIdentifier) &&
            column.MaxLength == 512 && !column.IsNullable);
        Assert.Contains(
            migration.DownOperations.OfType<DropTableOperation>(),
            operation => V2TableNames.Contains(operation.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void Story124Migration_AfterApply_HasNoPendingModelChanges_AndLegacyRowsRemainMapped()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        using VulgataDbContext context = new(options);

        context.GetService<IMigrator>().Migrate();

        Assert.Empty(context.Database.GetPendingMigrations());
        Assert.NotNull(context.Model.FindEntityType(typeof(ExactCrossRepositoryEdgeEvidence)));
        Assert.NotNull(context.Model.FindEntityType(typeof(Uncertainty)));
        Assert.NotNull(typeof(VulgataDbContext).GetProperty(
            nameof(VulgataDbContext.ExactCrossRepositoryEdgeEvidence)));
        Assert.NotNull(typeof(VulgataDbContext).GetProperty(nameof(VulgataDbContext.Uncertainties)));
    }

    [Fact]
    public void Story124Rollback_IsDefinedAgainstTheLastStory123Migration()
    {
        using VulgataDbContext context = CreateSqliteContext();
        IReadOnlyList<string> migrations = context.Database.GetMigrations().ToArray();
        Assert.Contains(BaselineMigration, migrations);
        Assert.Contains(
            migrations,
            migration => migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));
    }

    private static VulgataDbContext CreateSqliteContext()
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new VulgataDbContext(options);
    }
}

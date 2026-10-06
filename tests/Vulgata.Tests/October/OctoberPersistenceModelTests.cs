using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed class OctoberPersistenceModelTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OctoberModel_UsesFixedOctoberSchema()
    {
        using OctoberDbContext october = CreateOctoberModelContext();

        Assert.Equal("october", october.Model.GetDefaultSchema());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OctoberModel_UsesDedicatedMigrationsHistoryTable()
    {
        using OctoberDbContext october = CreateOctoberModelContext();

        string historyScript = october.GetService<IHistoryRepository>().GetCreateScript();
        Assert.Contains("october.\"__OctoberMigrationsHistory\"", historyScript);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OctoberModel_HasSeparateModelSnapshot()
    {
        using OctoberDbContext october = CreateOctoberModelContext();

        ModelSnapshot? snapshot = october.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);
        Assert.Equal("october", snapshot!.Model.GetDefaultSchema());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OctoberModel_HasNoPendingModelChanges()
    {
        using OctoberDbContext october = CreateOctoberModelContext();
        bool hasPendingModelChanges = october.Database.HasPendingModelChanges();
        string differences = hasPendingModelChanges
            ? DescribeModelDifferences(october)
            : string.Empty;

        Assert.False(
            hasPendingModelChanges,
            $"The October model must match its migration snapshot. Differences: {differences}");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OrdinaryVulgataModel_ContainsNoOctoberMappings()
    {
        using VulgataDbContext ordinary = CreateOrdinaryModelContext();

        Assert.DoesNotContain(ordinary.Model.GetEntityTypes(), entity =>
            string.Equals(entity.GetSchema(), "october", StringComparison.Ordinal) ||
            entity.ClrType.Namespace?.Contains(".October", StringComparison.Ordinal) == true);
    }

    private static OctoberDbContext CreateOctoberModelContext()
    {
        DbContextOptions<OctoberDbContext> options = new DbContextOptionsBuilder<OctoberDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=october_model_only;Username=model;Password=model")
            .Options;

        return new OctoberDbContext(options);
    }

    private static VulgataDbContext CreateOrdinaryModelContext()
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=vulgata_model_only;Username=model;Password=model")
            .Options;

        return new VulgataDbContext(options);
    }

    private static string DescribeModelDifferences(OctoberDbContext october)
    {
        ModelSnapshot? snapshot = october.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);

        IMigrationsModelDiffer differ = october.GetService<IMigrationsModelDiffer>();
        IModel designTimeModel = october.GetService<IDesignTimeModel>().Model;
        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(
            snapshot!.Model.GetRelationalModel(),
            designTimeModel.GetRelationalModel());

        return string.Join(
            "; ",
            operations.Select(operation =>
            {
                string operationName = operation.GetType().Name;
                string annotations = string.Join(",", operation.GetAnnotations().Select(annotation => annotation.Name));
                string annotationDetails = annotations.Length == 0
                    ? string.Empty
                    : $" Annotations=[{annotations}]";

                return operation switch
                {
                    AddColumnOperation column =>
                        $"{operationName} {column.Schema}.{column.Table}.{column.Name}:" +
                        $"{column.ClrType.Name}/{column.ColumnType}/{(column.IsNullable ? "nullable" : "required")}" +
                        annotationDetails,
                    AlterColumnOperation column =>
                        $"{operationName} {column.Schema}.{column.Table}.{column.Name}:" +
                        $"{column.ClrType.Name}/{column.ColumnType}/{(column.IsNullable ? "nullable" : "required")}" +
                        annotationDetails,
                    CreateIndexOperation index =>
                        $"{operationName} {index.Schema}.{index.Table}.{index.Name}" +
                        $"({string.Join(",", index.Columns)}) unique={index.IsUnique}{annotationDetails}",
                    AddForeignKeyOperation foreignKey =>
                        $"{operationName} {foreignKey.Schema}.{foreignKey.Table}" +
                        $"({string.Join(",", foreignKey.Columns)})->" +
                        $"{foreignKey.PrincipalSchema}.{foreignKey.PrincipalTable}" +
                        $"({string.Join(",", foreignKey.PrincipalColumns)}){annotationDetails}",
                    DropForeignKeyOperation foreignKey =>
                        $"{operationName} {foreignKey.Schema}.{foreignKey.Table}{annotationDetails}",
                    _ => $"{operationName}{annotationDetails}"
                };
            }));
    }
}

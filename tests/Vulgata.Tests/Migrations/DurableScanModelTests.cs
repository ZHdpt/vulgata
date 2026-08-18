using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.Migrations;

/// <summary>
/// T045 RED coverage for the Story 12.1 additive EF model.  These tests are
/// deliberately model-only: live PostgreSQL apply/rollback and migration
/// lifecycle evidence belongs to T047.
/// </summary>
public sealed class DurableScanModelTests
{
    [Fact]
    public void DurableScanModel_ContainsNewTablesAndBoundedColumns()
    {
        using VulgataDbContext db = CreatePostgreSqlModelContext();

        IModel model = DesignTimeModel(db);
        IEntityType snapshot = RequireEntity(model, typeof(ScanContextSnapshot), "ScanContextSnapshots");
        IEntityType job = RequireEntity(model, typeof(ScanJobExecution), "ScanJobExecutions");
        IEntityType coordination = RequireEntity(
            model,
            typeof(TwoRepositoryScanCoordination),
            "TwoRepositoryScanCoordinations");
        IEntityType execution = RequireEntity(model, typeof(ScanExecution), "ScanExecutions");

        AssertRequiredString(snapshot, nameof(ScanContextSnapshot.ProfiledRevision), 64);
        AssertRequiredString(snapshot, nameof(ScanContextSnapshot.SchemaIdentity), 128);
        AssertFixedString(snapshot, nameof(ScanContextSnapshot.ContentHash), 64);
        AssertOptionalString(snapshot, nameof(ScanContextSnapshot.GlobalContext), 10_000);
        AssertOptionalString(snapshot, nameof(ScanContextSnapshot.SystemContext), 10_000);
        AssertOptionalString(snapshot, nameof(ScanContextSnapshot.RepositoryContext), 10_000);

        AssertRequiredString(job, nameof(ScanJobExecution.FailureCode), 64, nullable: true);
        AssertEnumString(job, nameof(ScanJobExecution.State), 32);
        AssertEnumString(job, nameof(ScanJobExecution.FailureCategory), 32, nullable: true);
        AssertEnumString(coordination, nameof(TwoRepositoryScanCoordination.Phase), 32);
        AssertEnumString(coordination, nameof(TwoRepositoryScanCoordination.ReasonCategory), 32, nullable: true);
        AssertOptionalString(coordination, nameof(TwoRepositoryScanCoordination.ReasonCode), 64);

        AssertUtcTimestamp(snapshot, nameof(ScanContextSnapshot.CapturedAt));
        AssertUtcTimestamp(job, nameof(ScanJobExecution.StateChangedAt));
        AssertUtcTimestamp(job, nameof(ScanJobExecution.NextAttemptAt), nullable: true);
        AssertUtcTimestamp(coordination, nameof(TwoRepositoryScanCoordination.CreatedAt));
        AssertUtcTimestamp(coordination, nameof(TwoRepositoryScanCoordination.UpdatedAt));
        AssertUtcTimestamp(coordination, nameof(TwoRepositoryScanCoordination.TerminalAt), nullable: true);
        AssertUtcTimestamp(execution, nameof(ScanExecution.StartedAt));
        AssertUtcTimestamp(execution, nameof(ScanExecution.UpdatedAt), nullable: true);

        AssertConcurrencyToken(job, nameof(ScanJobExecution.StateVersion));
        AssertConcurrencyToken(coordination, nameof(TwoRepositoryScanCoordination.StateVersion));
        AssertConcurrencyToken(execution, nameof(ScanExecution.StateVersion));
    }

    [Fact]
    public void DurableScanModel_UsesCompositeRepositoryOwnershipAndRestrictiveDeletes()
    {
        using VulgataDbContext db = CreatePostgreSqlModelContext();
        IModel model = DesignTimeModel(db);
        IEntityType snapshot = RequireEntity(model, typeof(ScanContextSnapshot), "ScanContextSnapshots");
        IEntityType job = RequireEntity(model, typeof(ScanJobExecution), "ScanJobExecutions");
        IEntityType coordination = RequireEntity(
            model,
            typeof(TwoRepositoryScanCoordination),
            "TwoRepositoryScanCoordinations");

        AssertForeignKey(snapshot, typeof(Repository), [nameof(ScanContextSnapshot.RepositoryId)], [nameof(Repository.Id)]);
        AssertForeignKey(
            snapshot,
            typeof(ScanRun),
            [nameof(ScanContextSnapshot.ScanRunId), nameof(ScanContextSnapshot.RepositoryId)],
            [nameof(ScanRun.Id), nameof(ScanRun.RepositoryId)]);

        AssertForeignKey(job, typeof(Repository), [nameof(ScanJobExecution.RepositoryId)], [nameof(Repository.Id)]);
        AssertForeignKey(
            job,
            typeof(ScanRun),
            [nameof(ScanJobExecution.ScanRunId), nameof(ScanJobExecution.RepositoryId)],
            [nameof(ScanRun.Id), nameof(ScanRun.RepositoryId)]);
        AssertForeignKey(
            job,
            typeof(ScanExecution),
            [nameof(ScanJobExecution.ScanExecutionId), nameof(ScanJobExecution.RepositoryId)],
            [nameof(ScanExecution.Id), nameof(ScanExecution.RepositoryId)]);
        AssertForeignKey(
            job,
            typeof(CodeUnit),
            [nameof(ScanJobExecution.FileCodeUnitId), nameof(ScanJobExecution.ScanRunId), nameof(ScanJobExecution.RepositoryId)],
            [nameof(CodeUnit.Id), nameof(CodeUnit.ScanRunId), nameof(CodeUnit.RepositoryId)]);
        AssertForeignKey(
            job,
            typeof(DocumentVersion),
            [nameof(ScanJobExecution.CommittedDocumentVersionId), nameof(ScanJobExecution.RepositoryId), nameof(ScanJobExecution.ScanRunId)],
            [nameof(DocumentVersion.Id), nameof(DocumentVersion.RepositoryId), nameof(DocumentVersion.ScanRunId)]);

        AssertForeignKey(coordination, typeof(SystemEntity), [nameof(TwoRepositoryScanCoordination.SystemId)], [nameof(SystemEntity.Id)]);
        AssertForeignKey(coordination, typeof(Repository), [nameof(TwoRepositoryScanCoordination.RepositoryOneId)], [nameof(Repository.Id)]);
        AssertForeignKey(coordination, typeof(Repository), [nameof(TwoRepositoryScanCoordination.RepositoryTwoId)], [nameof(Repository.Id)]);
        AssertForeignKey(
            coordination,
            typeof(ScanRun),
            [nameof(TwoRepositoryScanCoordination.ScanRunOneId), nameof(TwoRepositoryScanCoordination.RepositoryOneId)],
            [nameof(ScanRun.Id), nameof(ScanRun.RepositoryId)]);
        AssertForeignKey(
            coordination,
            typeof(ScanRun),
            [nameof(TwoRepositoryScanCoordination.ScanRunTwoId), nameof(TwoRepositoryScanCoordination.RepositoryTwoId)],
            [nameof(ScanRun.Id), nameof(ScanRun.RepositoryId)]);
        AssertForeignKey(
            coordination,
            typeof(ScanExecution),
            [nameof(TwoRepositoryScanCoordination.ScanExecutionOneId), nameof(TwoRepositoryScanCoordination.RepositoryOneId)],
            [nameof(ScanExecution.Id), nameof(ScanExecution.RepositoryId)]);
        AssertForeignKey(
            coordination,
            typeof(ScanExecution),
            [nameof(TwoRepositoryScanCoordination.ScanExecutionTwoId), nameof(TwoRepositoryScanCoordination.RepositoryTwoId)],
            [nameof(ScanExecution.Id), nameof(ScanExecution.RepositoryId)]);

        foreach (IEntityType entity in new[] { snapshot, job, coordination })
        {
            Assert.NotEmpty(entity.GetForeignKeys());
            Assert.All(
                entity.GetForeignKeys(),
                foreignKey => Assert.True(
                    foreignKey.DeleteBehavior is DeleteBehavior.Restrict or DeleteBehavior.NoAction));
        }
    }

    [Fact]
    public void DurableScanModel_EnforcesDeterministicUniqueIdentityAndFilteredCommitIndex()
    {
        using VulgataDbContext db = CreatePostgreSqlModelContext();
        IModel model = DesignTimeModel(db);
        IEntityType snapshot = RequireEntity(model, typeof(ScanContextSnapshot), "ScanContextSnapshots");
        IEntityType job = RequireEntity(model, typeof(ScanJobExecution), "ScanJobExecutions");

        AssertUniqueIndex(
            snapshot,
            nameof(ScanContextSnapshot.ScanRunId),
            nameof(ScanContextSnapshot.RepositoryId));
        AssertUniqueIndex(
            job,
            nameof(ScanJobExecution.ScanExecutionId),
            nameof(ScanJobExecution.FileCodeUnitId));
        AssertUniqueIndex(
            job,
            nameof(ScanJobExecution.ScanExecutionId),
            nameof(ScanJobExecution.ManifestOrdinal));

        IIndex committedDocumentIndex = Assert.Single(
            job.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Any(property =>
                    property.Name == nameof(ScanJobExecution.CommittedDocumentVersionId)) &&
                !string.IsNullOrWhiteSpace(index.GetFilter()));
        Assert.Contains("CommittedDocumentVersionId", committedDocumentIndex.GetFilter(), StringComparison.Ordinal);
        Assert.Contains("IS NOT NULL", committedDocumentIndex.GetFilter(), StringComparison.OrdinalIgnoreCase);

        Assert.False(job.FindProperty(nameof(ScanJobExecution.Id))!.ValueGenerated == ValueGenerated.OnAdd);
        Assert.False(snapshot.FindProperty(nameof(ScanContextSnapshot.Id))!.ValueGenerated == ValueGenerated.OnAdd);
    }

    [Fact]
    public void DurableScanModel_ClosesStateLeaseRetryCompletionAndCoordinationShapes()
    {
        using VulgataDbContext db = CreatePostgreSqlModelContext();
        IModel model = DesignTimeModel(db);
        IEntityType job = RequireEntity(model, typeof(ScanJobExecution), "ScanJobExecutions");
        IEntityType coordination = RequireEntity(
            model,
            typeof(TwoRepositoryScanCoordination),
            "TwoRepositoryScanCoordinations");
        IEntityType execution = RequireEntity(model, typeof(ScanExecution), "ScanExecutions");

        string jobChecks = JoinChecks(job);
        Assert.Contains("Pending", jobChecks, StringComparison.Ordinal);
        Assert.Contains("Running", jobChecks, StringComparison.Ordinal);
        Assert.Contains("RetryScheduled", jobChecks, StringComparison.Ordinal);
        Assert.Contains("Completed", jobChecks, StringComparison.Ordinal);
        Assert.Contains("LeaseToken", jobChecks, StringComparison.Ordinal);
        Assert.Contains("LeaseExpiresAt", jobChecks, StringComparison.Ordinal);
        Assert.Contains("NextAttemptAt", jobChecks, StringComparison.Ordinal);
        Assert.Contains("CommittedDocumentVersionId", jobChecks, StringComparison.Ordinal);

        string coordinationChecks = JoinChecks(coordination);
        Assert.Contains("Preparing", coordinationChecks, StringComparison.Ordinal);
        Assert.Contains("CompletedWithWarnings", coordinationChecks, StringComparison.Ordinal);
        Assert.Contains("FailedIntegrity", coordinationChecks, StringComparison.Ordinal);
        Assert.Contains("FailedSecurity", coordinationChecks, StringComparison.Ordinal);
        Assert.Contains("TerminalAt", coordinationChecks, StringComparison.Ordinal);

        string executionChecks = JoinChecks(execution);
        foreach (string phase in Enum.GetNames<ScanExecutionPhase>())
        {
            Assert.Contains(phase, executionChecks, StringComparison.Ordinal);
        }

        Assert.Contains("PauseCategory", executionChecks, StringComparison.Ordinal);
        Assert.Contains("FailureCategory", executionChecks, StringComparison.Ordinal);
        Assert.Contains("StateVersion", executionChecks, StringComparison.Ordinal);
    }

    [Fact]
    public void DurableScanModel_RetainsLegacyScanExecutionAtomsAndColumnsForCompatibility()
    {
        using VulgataDbContext db = CreatePostgreSqlModelContext();
        IModel model = DesignTimeModel(db);
        IEntityType execution = RequireEntity(model, typeof(ScanExecution), "ScanExecutions");

        string[] retainedProperties =
        [
            nameof(ScanExecution.Id),
            nameof(ScanExecution.RepositoryId),
            nameof(ScanExecution.SystemId),
            nameof(ScanExecution.ScanRunId),
            nameof(ScanExecution.PreScanResultId),
            nameof(ScanExecution.ProfiledRevision),
            nameof(ScanExecution.Phase),
            nameof(ScanExecution.InFlightJobId),
            nameof(ScanExecution.CompletedJobCount),
            nameof(ScanExecution.TotalJobCount),
            nameof(ScanExecution.RestartIndicator),
            nameof(ScanExecution.TerminalMessage),
            nameof(ScanExecution.TerminalReasonKind),
            nameof(ScanExecution.StartedAt),
            nameof(ScanExecution.TerminalAt),
        ];
        Assert.All(retainedProperties, property => Assert.NotNull(execution.FindProperty(property)));

        string phaseCheckSql = JoinChecks(execution);
        foreach (string phase in
                 Enum.GetNames<ScanExecutionPhase>().Where(phase => phase is
                     "Pass1Planning" or "Pass1Running" or "Pass1Completed" or
                     "Completed" or "Failed" or "Cancelled"))
        {
            Assert.Contains(phase, phaseCheckSql, StringComparison.Ordinal);
        }

        Assert.True(execution.FindProperty(nameof(ScanExecution.PromptIdentity))!.IsNullable);
        Assert.True(execution.FindProperty(nameof(ScanExecution.DocumentSchemaIdentity))!.IsNullable);
        Assert.True(execution.FindProperty(nameof(ScanExecution.AnalysisRuntimeIdentityJson))!.IsNullable);
    }

    [Fact]
    public void DurableScanModel_PR003TemporalSecondInsertBoundary_UsesUtcBoundCompositeBindings()
    {
        using VulgataDbContext db = CreatePostgreSqlModelContext();
        IModel model = DesignTimeModel(db);
        IEntityType snapshot = RequireEntity(model, typeof(ScanContextSnapshot), "ScanContextSnapshots");
        IEntityType execution = RequireEntity(model, typeof(ScanExecution), "ScanExecutions");

        AssertUtcTimestamp(snapshot, nameof(ScanContextSnapshot.CapturedAt));
        AssertUtcTimestamp(execution, nameof(ScanExecution.StartedAt));
        AssertForeignKey(
            snapshot,
            typeof(ScanRun),
            [nameof(ScanContextSnapshot.ScanRunId), nameof(ScanContextSnapshot.RepositoryId)],
            [nameof(ScanRun.Id), nameof(ScanRun.RepositoryId)]);
        AssertForeignKey(
            execution,
            typeof(ScanRun),
            [nameof(ScanExecution.ScanRunId), nameof(ScanExecution.RepositoryId)],
            [nameof(ScanRun.Id), nameof(ScanRun.RepositoryId)]);

        // The model must expose both sides of the temporal insertion boundary;
        // transactionally rejecting an invalid second insert is implemented by
        // T046 and verified against PostgreSQL by T047/T048.
        Assert.NotEqual(
            nameof(ScanContextSnapshot.CapturedAt),
            nameof(ScanExecution.StartedAt));
    }

    private static VulgataDbContext CreatePostgreSqlModelContext()
    {
        var options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql("Host=localhost;Database=durable_scan_model_metadata_only")
            .Options;
        return new VulgataDbContext(options);
    }

    private static IModel DesignTimeModel(VulgataDbContext db) =>
        db.GetService<IDesignTimeModel>().Model;

    private static IEntityType RequireEntity(IModel model, Type clrType, string tableName)
    {
        IEntityType entity = Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(clrType));
        Assert.Equal("vulgata", entity.GetSchema());
        Assert.Equal(tableName, entity.GetTableName());
        Assert.Equal(["Id"], entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
        return entity;
    }

    private static void AssertRequiredString(
        IEntityType entity,
        string propertyName,
        int maxLength,
        bool nullable = false)
    {
        IProperty? property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        property = property!;
        Assert.Equal(nullable, property.IsNullable);
        Assert.Equal(typeof(string), property.ClrType);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    private static void AssertOptionalString(IEntityType entity, string propertyName, int maxLength) =>
        AssertRequiredString(entity, propertyName, maxLength, nullable: true);

    private static void AssertFixedString(IEntityType entity, string propertyName, int maxLength)
    {
        IProperty? property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        property = property!;
        Assert.False(property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
        Assert.True(property.IsFixedLength());
    }

    private static void AssertEnumString(
        IEntityType entity,
        string propertyName,
        int maxLength,
        bool nullable = false)
    {
        IProperty? property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        property = property!;
        Assert.Equal(nullable, property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
        Assert.Equal(typeof(string), property.GetProviderClrType());
    }

    private static void AssertUtcTimestamp(IEntityType entity, string propertyName, bool nullable = false)
    {
        IProperty? property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        property = property!;
        Assert.Equal(nullable, property.IsNullable);
        Assert.Equal(
            nullable ? typeof(DateTimeOffset?) : typeof(DateTimeOffset),
            property.ClrType);
        Assert.Equal("timestamp with time zone", property.GetColumnType());
    }

    private static void AssertConcurrencyToken(IEntityType entity, string propertyName)
    {
        IProperty? property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        property = property!;
        Assert.Equal(typeof(long), property.ClrType);
        Assert.False(property.IsNullable);
        Assert.True(property.IsConcurrencyToken);
    }

    private static void AssertForeignKey(
        IEntityType entity,
        Type principalType,
        string[] dependentProperties,
        string[] principalProperties)
    {
        Assert.Contains(
            entity.GetForeignKeys(),
            foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == principalType &&
                foreignKey.Properties.Select(property => property.Name).SequenceEqual(dependentProperties) &&
                foreignKey.PrincipalKey.Properties.Select(property => property.Name).SequenceEqual(principalProperties) &&
                foreignKey.DeleteBehavior is DeleteBehavior.Restrict or DeleteBehavior.NoAction);
    }

    private static void AssertUniqueIndex(IEntityType entity, params string[] properties)
    {
        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(properties));
    }

    private static string JoinChecks(IEntityType entity) =>
        string.Join("\n", entity.GetCheckConstraints().Select(constraint => constraint.Sql));
}

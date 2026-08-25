using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T006 RED seam for the small compatibility correction required by the
/// prepared Pass 1 path.  These tests intentionally observe only public
/// entity transitions and the EF migration assembly; they do not reach into
/// private state or prescribe an implementation strategy.
/// </summary>
public sealed class Pass1MigrationTests
{
    private static readonly Guid _firstExecutionId =
        Guid.Parse("11000000-0000-4000-8000-000000000011");
    private static readonly Guid _secondExecutionId =
        Guid.Parse("11000000-0000-4000-8000-000000000012");
    private static readonly Guid _firstRepositoryId =
        Guid.Parse("22000000-0000-4000-8000-000000000022");
    private static readonly Guid _secondRepositoryId =
        Guid.Parse("22000000-0000-4000-8000-000000000023");
    private static readonly Guid _systemId =
        Guid.Parse("33000000-0000-4000-8000-000000000033");
    private static readonly Guid _firstScanRunId =
        Guid.Parse("44000000-0000-4000-8000-000000000044");
    private static readonly Guid _secondScanRunId =
        Guid.Parse("44000000-0000-4000-8000-000000000045");
    private static readonly DateTimeOffset _startedAt =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ScanExecution_NewPathComplete_UsesVersionAndTimeGuardsAndIsTerminallyImmutable()
    {
        ScanExecution execution = CreateExecution(_firstExecutionId, _firstRepositoryId, _firstScanRunId);
        DateTimeOffset runningAt = _startedAt.AddSeconds(1);
        DateTimeOffset completedAt = _startedAt.AddSeconds(2);
        execution.BeginRunning(expectedStateVersion: 0, updatedAt: runningAt);

        InvokePublicTransition(
            execution,
            "Complete",
            expectedStateVersion: 1,
            updatedAt: completedAt);

        Assert.Equal(ParseEnum<ScanExecutionPhase>("Completed"), execution.Phase);
        Assert.Equal(2, execution.StateVersion);
        Assert.Equal(completedAt, execution.TerminalAt);
        Assert.Null(execution.TerminalMessage);
        Assert.Null(execution.PauseCategory);
        Assert.Null(execution.PauseCode);

        Assert.Throws<InvalidOperationException>(() => InvokePublicTransition(
            execution,
            "Complete",
            expectedStateVersion: 2,
            updatedAt: completedAt.AddSeconds(1)));
    }

    [Fact]
    public void ScanExecution_NewPathComplete_RejectsStaleVersionAndRegressingTime()
    {
        ScanExecution stale = CreateExecution(_firstExecutionId, _firstRepositoryId, _firstScanRunId);
        stale.BeginRunning(expectedStateVersion: 0, updatedAt: _startedAt.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() => InvokePublicTransition(
            stale,
            "Complete",
            expectedStateVersion: 0,
            updatedAt: _startedAt.AddSeconds(2)));

        ScanExecution regressing = CreateExecution(_firstExecutionId, _firstRepositoryId, _firstScanRunId);
        regressing.BeginRunning(expectedStateVersion: 0, updatedAt: _startedAt.AddSeconds(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => InvokePublicTransition(
            regressing,
            "Complete",
            expectedStateVersion: 1,
            updatedAt: _startedAt.AddSeconds(1)));
    }

    [Fact]
    public void ScanExecution_FailedPass1_UsesClosedPolicyAtomAndCannotResume()
    {
        ScanExecution execution = CreateExecution(_firstExecutionId, _firstRepositoryId, _firstScanRunId);
        DateTimeOffset failedAt = _startedAt.AddSeconds(1);
        execution.BeginRunning(expectedStateVersion: 0, updatedAt: failedAt);

        InvokePublicTransition(
            execution,
            "FailPass1",
            expectedStateVersion: 1,
            reasonCode: "ordinary-failure-budget");

        Assert.Equal(ParseEnum<ScanExecutionPhase>("FailedPass1"), execution.Phase);
        Assert.Equal(
            ParseEnum<ScanExecutionFailureCategory>("Pass1Policy"),
            execution.FailureCategory);
        Assert.Equal("ordinary-failure-budget", execution.FailureCode);
        Assert.Equal(failedAt, execution.TerminalAt);
        Assert.Null(execution.PauseCategory);
        Assert.Null(execution.PauseCode);
        Assert.Throws<InvalidOperationException>(() => execution.BeginRunning(
            expectedStateVersion: execution.StateVersion,
            updatedAt: failedAt.AddSeconds(1)));
    }

    [Fact]
    public void TwoRepositoryCoordination_FailedPass1_UsesVersionGuardAndTerminalImmutability()
    {
        ScanExecution first = CreateExecution(_firstExecutionId, _firstRepositoryId, _firstScanRunId);
        ScanExecution second = CreateExecution(_secondExecutionId, _secondRepositoryId, _secondScanRunId);
        TwoRepositoryScanCoordination coordination = TwoRepositoryScanCoordination.Create(
            first,
            second,
            _startedAt.AddSeconds(1));
        coordination.BeginPass1(expectedStateVersion: 0, updatedAt: _startedAt.AddSeconds(2));

        Assert.Throws<InvalidOperationException>(() => InvokePublicTransition(
            coordination,
            "FailPass1",
            expectedStateVersion: 0,
            reasonCode: "ordinary-failure-budget"));

        DateTimeOffset failedAt = _startedAt.AddSeconds(3);
        InvokePublicTransition(
            coordination,
            "FailPass1",
            expectedStateVersion: 1,
            reasonCode: "ordinary-failure-budget",
            updatedAt: failedAt);

        Assert.Equal(
            ParseEnum<TwoRepositoryScanCoordinationPhase>("FailedPass1"),
            coordination.Phase);
        Assert.Equal(
            ParseEnum<TwoRepositoryScanReasonCategory>("Pass1Policy"),
            coordination.ReasonCategory);
        Assert.Equal("ordinary-failure-budget", coordination.ReasonCode);
        Assert.Equal(failedAt, coordination.TerminalAt);
        Assert.Throws<InvalidOperationException>(() => InvokePublicTransition(
            coordination,
            "FailPass1",
            expectedStateVersion: coordination.StateVersion,
            reasonCode: "another-failure",
            updatedAt: failedAt.AddSeconds(1)));
    }

    [Fact]
    public void LegacyScanAndCoordinationAtoms_RemainAvailableAndUnchanged()
    {
        ScanExecution legacy = ScanExecution.Start(
            _firstExecutionId,
            _firstRepositoryId,
            _systemId,
            _firstScanRunId,
            Guid.Parse("55000000-0000-4000-8000-000000000055"),
            "abcdef0123456789abcdef0123456789abcdef01",
            _startedAt);

        Assert.Equal(ScanExecutionPhase.Pass1Planning, legacy.Phase);
        Assert.Null(legacy.UpdatedAt);
        Assert.Null(legacy.PromptIdentity);
        Assert.Null(legacy.DocumentSchemaIdentity);
        Assert.Null(legacy.AnalysisRuntimeIdentityJson);
        Assert.Null(legacy.AnalysisRuntimeIdentityHash);
        Assert.Contains(ScanExecutionPhase.Pass1Planning, Enum.GetValues<ScanExecutionPhase>());
        Assert.Contains(TwoRepositoryScanCoordinationPhase.Pass1, Enum.GetValues<TwoRepositoryScanCoordinationPhase>());
        Assert.Contains(TwoRepositoryScanReasonCategory.Unclassified, Enum.GetValues<TwoRepositoryScanReasonCategory>());

        ScanExecution v2 = CreateExecution(_secondExecutionId, _secondRepositoryId, _secondScanRunId);
        Assert.Equal("code-logic/file-v2", v2.DocumentSchemaIdentity);
        Assert.NotEqual(legacy.DocumentSchemaIdentity, v2.DocumentSchemaIdentity);
    }

    [Fact]
    public void StrictResultCorrection_IsExecutionLocalAndLeavesTheEfModelUnchanged()
    {
        using VulgataDbContext db = CreateNpgsqlModelContext();

        Assert.False(db.Database.HasPendingModelChanges());
        Assert.DoesNotContain(
            db.Model.GetEntityTypes(),
            entity =>
            {
                string identity = entity.ClrType?.FullName ?? entity.Name;
                return identity.Contains("DeepSeek", StringComparison.OrdinalIgnoreCase) ||
                       identity.Contains("StrictResult", StringComparison.OrdinalIgnoreCase) ||
                       identity.Contains("SubmissionSession", StringComparison.OrdinalIgnoreCase) ||
                       identity.Contains("ProviderTranscript", StringComparison.OrdinalIgnoreCase);
            });

        IMigrationsAssembly migrationsAssembly = db.GetService<IMigrationsAssembly>();
        Assert.DoesNotContain(
            migrationsAssembly.Migrations.Keys,
            name => name.Contains("DeepSeek", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("StrictResult", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("SubmissionSession", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("ProviderTranscript", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ScanJobExecution_OneRetryLifecycle_PreservesOnlyDurableRetryState()
    {
        ScanJobExecution job = ScanJobExecution.Create(
            _firstExecutionId,
            _firstRepositoryId,
            _firstScanRunId,
            Guid.Parse("55000000-0000-4000-8000-000000000056"),
            manifestOrdinal: 1,
            _startedAt);
        Guid firstLease = Guid.Parse("66000000-0000-4000-8000-000000000066");
        DateTimeOffset retryAt = _startedAt.AddMinutes(2);

        job.AcquireLease(firstLease, _startedAt.AddMinutes(5), _startedAt.AddMinutes(1));
        job.ScheduleRetry(
            job.StateVersion,
            firstLease,
            retryAt,
            ScanJobFailureCategory.JobTransient,
            "worker-transient",
            _startedAt.AddMinutes(1).AddSeconds(1));

        Assert.Equal(ScanJobExecutionState.RetryScheduled, job.State);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(retryAt, job.NextAttemptAt);

        job.RecoverDueRetry(job.StateVersion, retryAt);
        job.AcquireLease(
            Guid.Parse("66000000-0000-4000-8000-000000000067"),
            retryAt.AddMinutes(5),
            retryAt.AddSeconds(1));

        Assert.Equal(ScanJobExecutionState.Running, job.State);
        Assert.Equal(2, job.AttemptCount);
        Assert.Null(job.NextAttemptAt);

        // PreparedPass1Store owns the at-most-one retry policy. This entity and
        // model assertion protects only the durable state used by that policy.
        using VulgataDbContext db = CreateNpgsqlModelContext();
        var jobEntity = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType>(
            db.Model.FindEntityType(typeof(ScanJobExecution)));
        string[] durableProperties = jobEntity.GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.Contains(nameof(ScanJobExecution.AttemptCount), durableProperties);
        Assert.Contains(nameof(ScanJobExecution.State), durableProperties);
        Assert.Contains(nameof(ScanJobExecution.NextAttemptAt), durableProperties);
        Assert.DoesNotContain(
            durableProperties,
            name => name.Contains("StrictResult", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("SubmissionSession", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("ProviderTranscript", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddResumablePass1CodeLogic_IsAnAdditiveNoBackfillMigration()
    {
        using VulgataDbContext db = CreateSqliteContext();
        IMigrationsAssembly migrationsAssembly = db.GetService<IMigrationsAssembly>();
        KeyValuePair<string, TypeInfo> migrationEntry = Assert.Single(
            migrationsAssembly.Migrations,
            entry => entry.Key.EndsWith(
                "_AddResumablePass1CodeLogic",
                StringComparison.Ordinal));
        Migration migration = migrationsAssembly.CreateMigration(
            migrationEntry.Value,
            "Npgsql.EntityFrameworkCore.PostgreSQL");

        Assert.NotEmpty(migration.UpOperations);
        Assert.DoesNotContain(
            migration.UpOperations,
            operation => operation is DeleteDataOperation or UpdateDataOperation or DropTableOperation or DropColumnOperation);

        string[] expectedTables =
        [
            "Pass1PairExecutions",
            "Pass1ConcurrencyChanges",
            "Pass1NavigationBudgets",
            "Pass1NavigationReceipts",
            "Pass1Outcomes",
            "Pass1FailureInventoryEntries",
        ];
        Assert.Equal(
            expectedTables.Order(StringComparer.Ordinal),
            migration.UpOperations
                .OfType<CreateTableOperation>()
                .Select(operation => operation.Name)
                .Where(expectedTables.Contains)
                .Order(StringComparer.Ordinal));

        Assert.Contains(
            migration.UpOperations.OfType<AddColumnOperation>(),
            operation => operation.Table == "SourceAnchors" && operation.Name == "ByteStart");
        Assert.Contains(
            migration.UpOperations.OfType<AddColumnOperation>(),
            operation => operation.Table == "SourceAnchors" && operation.Name == "ByteEnd");
    }

    [Fact]
    public void AddResumablePass1CodeLogic_DownIsReversibleWithoutHistoricalBackfill()
    {
        using VulgataDbContext db = CreateSqliteContext();
        IMigrationsAssembly migrationsAssembly = db.GetService<IMigrationsAssembly>();
        KeyValuePair<string, TypeInfo> migrationEntry = Assert.Single(
            migrationsAssembly.Migrations,
            entry => entry.Key.EndsWith(
                "_AddResumablePass1CodeLogic",
                StringComparison.Ordinal));
        Migration migration = migrationsAssembly.CreateMigration(
            migrationEntry.Value,
            "Npgsql.EntityFrameworkCore.PostgreSQL");

        Assert.NotEmpty(migration.DownOperations);
        Assert.DoesNotContain(
            migration.DownOperations,
            operation => operation is InsertDataOperation or UpdateDataOperation or DeleteDataOperation);
        Assert.Contains(
            migration.DownOperations.OfType<DropTableOperation>(),
            operation => operation.Name == "Pass1PairExecutions");
        Assert.Contains(
            migration.DownOperations.OfType<DropColumnOperation>(),
            operation => operation.Table == "SourceAnchors" && operation.Name == "ByteStart");
    }

    private static ScanExecution CreateExecution(Guid executionId, Guid repositoryId, Guid scanRunId) =>
        ScanExecution.CreateNewPath(
            executionId,
            repositoryId,
            _systemId,
            scanRunId,
            "abcdef0123456789abcdef0123456789abcdef01",
            "prompt/1.0",
            "code-logic/file-v2",
            "{\"codeGraph\":\"1.5.0\",\"java\":\"21\"}",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            _startedAt);

    private static VulgataDbContext CreateSqliteContext()
    {
        var options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite(new SqliteConnection("Data Source=:memory:"))
            .Options;
        return new(options);
    }

    private static VulgataDbContext CreateNpgsqlModelContext()
    {
        var options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=vulgata")
            .Options;
        return new(options);
    }

    private static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value, ignoreCase: false);

    private static void InvokePublicTransition(
        object target,
        string methodName,
        long expectedStateVersion,
        DateTimeOffset? updatedAt = null,
        string? reasonCode = null)
    {
        MethodInfo? method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            [typeof(long), typeof(string), typeof(DateTimeOffset)],
            modifiers: null);
        if (method is null && reasonCode is null)
        {
            method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                [typeof(long), typeof(DateTimeOffset)],
                modifiers: null);
        }

        if (method is null)
        {
            throw new MissingMethodException(
                target.GetType().FullName,
                methodName);
        }
        DateTimeOffset effectiveUpdatedAt = updatedAt ?? target switch
        {
            ScanExecution execution when execution.UpdatedAt is { } current => current,
            TwoRepositoryScanCoordination coordination => coordination.UpdatedAt,
            _ => throw new InvalidOperationException(
                $"No current durable timestamp is available for {target.GetType().FullName}.")
        };
        object?[] arguments = method!.GetParameters().Length == 2
            ? [expectedStateVersion, effectiveUpdatedAt]
            : [expectedStateVersion, reasonCode ?? throw new ArgumentNullException(nameof(reasonCode)), effectiveUpdatedAt];
        try
        {
            method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }
}

using Vulgata.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.DurableScanning;

/// <summary>
/// T024 RED: manual recovery must be an explicit, idempotent, bounded
/// coordinator operation.  The production coordinator is deliberately not
/// supplied by this test slice; the compile failure is the RED seam for T025.
/// These tests use the accepted durable-store and Core recovery contracts and
/// do not introduce a test-local recovery implementation.
/// </summary>
public sealed class RecoveryCoordinatorTests
{
    private static readonly DateTimeOffset _start =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);
    private const string _revision = "0123456789abcdef0123456789abcdef01234567";
    private const string _runtimeHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    // T024 remediation RED: the accepted manual-resume contract proposes
    // this immutable Core seam (not a test-local projection):
    // ScanRecoveryView(
    //   ScanExecutionId, Phase, TotalJobCount, CompletedJobCount,
    //   PendingJobCount, RunningJobCount, RetryScheduledJobCount,
    //   FailedJobCount, CancelledJobCount, RecoveredExpiredLeaseCount,
    //   ReleasedDueRetryCount, StateChanged, Reason), carried by
    // ScanRecoveryResult.View with ScanRecoveryResult.IsReplay.

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_ValidPausedExecutionWithMixedJobs_ReturnsPreparedBoundedViewAndTransitionCounts()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        await fixture.PauseProviderExecutionAsync();
        await fixture.AddRunningJobAsync(1, _start.AddMinutes(5));
        await fixture.AddRetryScheduledJobAsync(2, _start.AddMinutes(6));
        await fixture.AddPendingJobAsync(3);
        await fixture.AddRetryScheduledJobAsync(4, _start.AddMinutes(20));
        await fixture.AddFailedJobAsync(5);
        await fixture.AddCancelledJobAsync(6);
        fixture.Clock.SetUtcNow(_start.AddMinutes(10));

        ScanRecoveryResult result = await fixture.ResumeAsync();

        Assert.Equal(DurableScanStoreOutcome.Applied, result.Outcome);
        Assert.False(result.IsReplay);
        ScanRecoveryView view = Assert.IsType<ScanRecoveryView>(result.View);
        Assert.Equal(fixture.ExecutionId, view.ScanExecutionId);
        Assert.Equal(ScanExecutionPhase.Prepared, view.Phase);
        Assert.Equal(6, view.TotalJobCount);
        Assert.Equal(0, view.CompletedJobCount);
        Assert.Equal(3, view.PendingJobCount);
        Assert.Equal(0, view.RunningJobCount);
        Assert.Equal(1, view.RetryScheduledJobCount);
        Assert.Equal(1, view.FailedJobCount);
        Assert.Equal(1, view.CancelledJobCount);
        Assert.Equal(1, view.RecoveredExpiredLeaseCount);
        Assert.Equal(1, view.ReleasedDueRetryCount);
        Assert.True(view.StateChanged);
        Assert.Equal(DurableScanReason.None, view.Reason);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_ValidPausedExecution_ReplayReturnsSameBoundedViewWithoutAnotherVersionChange()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        await fixture.PauseProviderExecutionAsync();
        await fixture.AddRunningJobAsync(1, _start.AddMinutes(5));
        await fixture.AddRetryScheduledJobAsync(2, _start.AddMinutes(6));
        fixture.Clock.SetUtcNow(_start.AddMinutes(10));

        ScanRecoveryResult first = await fixture.ResumeAsync();
        long versionAfterFirst = await fixture.ExecutionVersionAsync();
        ScanRecoveryResult replay = await fixture.ResumeAsync();

        ScanRecoveryView firstView = Assert.IsType<ScanRecoveryView>(first.View);
        ScanRecoveryView replayView = Assert.IsType<ScanRecoveryView>(replay.View);
        Assert.Equal(firstView.ScanExecutionId, replayView.ScanExecutionId);
        Assert.Equal(firstView.Phase, replayView.Phase);
        Assert.Equal(firstView.TotalJobCount, replayView.TotalJobCount);
        Assert.Equal(firstView.CompletedJobCount, replayView.CompletedJobCount);
        Assert.Equal(firstView.PendingJobCount, replayView.PendingJobCount);
        Assert.Equal(firstView.RunningJobCount, replayView.RunningJobCount);
        Assert.Equal(firstView.RetryScheduledJobCount, replayView.RetryScheduledJobCount);
        Assert.Equal(firstView.FailedJobCount, replayView.FailedJobCount);
        Assert.Equal(firstView.CancelledJobCount, replayView.CancelledJobCount);
        Assert.Equal(1, firstView.RecoveredExpiredLeaseCount);
        Assert.Equal(1, firstView.ReleasedDueRetryCount);
        Assert.Equal(0, replayView.RecoveredExpiredLeaseCount);
        Assert.Equal(0, replayView.ReleasedDueRetryCount);
        Assert.True(firstView.StateChanged);
        Assert.False(first.IsReplay);
        Assert.False(replayView.StateChanged);
        Assert.True(replay.IsReplay);
        Assert.Equal(firstView.Reason, replayView.Reason);
        Assert.Equal(versionAfterFirst, await fixture.ExecutionVersionAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_RepeatedInvocation_IsIdempotentAndDoesNotDispatchOrScheduleWork()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        fixture.Clock.SetUtcNow(_start.AddMinutes(10));

        ScanRecoveryResult first = await fixture.ResumeAsync();
        long versionAfterFirst = await fixture.ExecutionVersionAsync();
        ScanRecoveryResult second = await fixture.ResumeAsync();

        Assert.Equal(first, second);
        Assert.Equal(versionAfterFirst, await fixture.ExecutionVersionAsync());
        Assert.Equal(0, fixture.DispatchCount);
        Assert.Equal(0, fixture.RetryScheduleCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_ExpiredLeaseAndDueRetry_ReturnPending_WithoutChangingIdentityOrAttemptCount()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        ScanJobExecution expired = await fixture.AddRunningJobAsync(
            manifestOrdinal: 1,
            leaseExpiresAt: _start.AddMinutes(5));
        ScanJobExecution dueRetry = await fixture.AddRetryScheduledJobAsync(
            manifestOrdinal: 2,
            nextAttemptAt: _start.AddMinutes(6));
        fixture.Clock.SetUtcNow(_start.AddMinutes(7));

        ScanRecoveryResult result = await fixture.ResumeAsync();

        Assert.Equal(DurableScanStoreOutcome.Applied, result.Outcome);
        Assert.Equal(ScanJobExecutionState.Pending, await fixture.StateAsync(expired.Id));
        Assert.Equal(ScanJobExecutionState.Pending, await fixture.StateAsync(dueRetry.Id));
        Assert.Equal(1, await fixture.AttemptCountAsync(expired.Id));
        Assert.Equal(1, await fixture.AttemptCountAsync(dueRetry.Id));
        Assert.Equal(expired.Id, await fixture.DeterministicJobIdAsync(1));
        Assert.Equal(dueRetry.Id, await fixture.DeterministicJobIdAsync(2));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_LeaseAtExpiryOrFutureRetry_LeavesJobsUnchanged()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        ScanJobExecution exactExpiry = await fixture.AddRunningJobAsync(
            manifestOrdinal: 1,
            leaseExpiresAt: _start.AddMinutes(5));
        ScanJobExecution futureRetry = await fixture.AddRetryScheduledJobAsync(
            manifestOrdinal: 2,
            nextAttemptAt: _start.AddMinutes(10));
        fixture.Clock.SetUtcNow(_start.AddMinutes(5));
        (long exactVersion, int exactAttempts) = await fixture.VersionAndAttemptsAsync(exactExpiry.Id);
        (long futureVersion, int futureAttempts) = await fixture.VersionAndAttemptsAsync(futureRetry.Id);

        ScanRecoveryResult result = await fixture.ResumeAsync();

        Assert.Equal(DurableScanStoreOutcome.Applied, result.Outcome);
        Assert.Equal(ScanJobExecutionState.Running, await fixture.StateAsync(exactExpiry.Id));
        Assert.Equal(ScanJobExecutionState.RetryScheduled, await fixture.StateAsync(futureRetry.Id));
        Assert.Equal((exactVersion, exactAttempts), await fixture.VersionAndAttemptsAsync(exactExpiry.Id));
        Assert.Equal((futureVersion, futureAttempts), await fixture.VersionAndAttemptsAsync(futureRetry.Id));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_ExhaustedAndTerminalJobs_AreNeverReopened()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        ScanJobExecution failed = await fixture.AddFailedJobAsync(1);
        ScanJobExecution cancelled = await fixture.AddCancelledJobAsync(2);
        fixture.Clock.SetUtcNow(_start.AddMinutes(10));
        var before = await fixture.SnapshotJobStateAsync(failed.Id, cancelled.Id);

        ScanRecoveryResult result = await fixture.ResumeAsync();

        Assert.Contains(result.Outcome, new[]
        {
            DurableScanStoreOutcome.Applied,
            DurableScanStoreOutcome.Conflict,
        });
        Assert.Equal(before, await fixture.SnapshotJobStateAsync(failed.Id, cancelled.Id));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_InvalidAuthorization_IsRejectedWithoutLookupMutation()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        fixture.Clock.SetUtcNow(_start.AddMinutes(10));
        ScanRecoveryResult result = await fixture.ResumeAsync(
            new DurableScanAuthorizationScope("other-actor", []));

        Assert.Equal(DurableScanStoreOutcome.NotFound, result.Outcome);
        Assert.Equal(DurableScanReason.InvalidIdentity, result.Reason);
        Assert.Equal(0, fixture.DispatchCount);
        Assert.Equal(0, fixture.RetryScheduleCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_CancelledBeforeConditionalWrite_ReturnsCancelledAndPreservesDurableRows()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        ScanJobExecution job = await fixture.AddRunningJobAsync(1, _start.AddMinutes(5));
        fixture.Clock.SetUtcNow(_start.AddMinutes(10));
        var before = await fixture.VersionAndAttemptsAsync(job.Id);

        ScanRecoveryResult result = await fixture.ResumeAsync(
            fixture.Scope,
            cancellation.Token);

        Assert.NotEqual(DurableScanStoreOutcome.Applied, result.Outcome);
        Assert.Equal(DurableScanReason.Cancelled, result.Reason);
        Assert.Equal(before, await fixture.VersionAndAttemptsAsync(job.Id));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_LateCapturedVersionConflict_UsesOneBatchWithoutPartialApplication()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        await fixture.AddRunningJobAsync(1, _start.AddMinutes(5));
        await fixture.AddRetryScheduledJobAsync(2, _start.AddMinutes(6));
        fixture.Clock.SetUtcNow(_start.AddMinutes(10));
        string before = await fixture.SnapshotDurableStateAsync();
        RecoveryFixture.BatchConflictStore batchStore =
            fixture.InstallBatchConflictStore();

        ScanRecoveryResult result = await fixture.ResumeAsync();

        Assert.Equal(DurableScanStoreOutcome.Conflict, result.Outcome);
        Assert.Equal(DurableScanReason.StaleStateVersion, result.Reason);
        Assert.Equal(1, batchStore.BatchInvocationCount);
        Assert.Equal(0, batchStore.SingleTransitionInvocationCount);
        ScanRecoveryBatchTransitionCommand command =
            Assert.IsType<ScanRecoveryBatchTransitionCommand>(batchStore.LastCommand);
        Assert.Equal(fixture.ExecutionId, command.ScanExecutionId);
        Assert.Equal(0, command.ExpectedStateVersion);
        Assert.Equal(2, command.Transitions.Count);
        Assert.Equal(before, await fixture.SnapshotDurableStateAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("authorization")]
    [InlineData("profiled-revision")]
    [InlineData("affected-file-hash")]
    [InlineData("prompt-identity")]
    [InlineData("schema-identity")]
    [InlineData("provider-availability")]
    [InlineData("analysis-runtime-identity")]
    public async Task ResumeAsync_EachAcceptedRevalidationMismatch_StopsClosedAndPreservesCommittedDocuments(
        string mismatch)
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        await fixture.ApplyRevalidationMismatchAsync(mismatch);
        fixture.Clock.SetUtcNow(_start.AddMinutes(10));
        ScanJobExecution job = await fixture.AddRunningJobAsync(1, _start.AddMinutes(5));
        var before = await fixture.SnapshotDurableStateAsync();

        ScanRecoveryResult result = await fixture.ResumeAsync();

        Assert.NotEqual(DurableScanStoreOutcome.Applied, result.Outcome);
        Assert.Contains(result.Reason, new[]
        {
            DurableScanReason.Unauthorized,
            DurableScanReason.InvalidIdentity,
            DurableScanReason.ProviderUnavailable,
            DurableScanReason.IntegrityViolation,
        });
        Assert.Equal(before, await fixture.SnapshotDurableStateAsync());
        Assert.Equal(ScanJobExecutionState.Running, await fixture.StateAsync(job.Id));
        Assert.Equal(0, fixture.DispatchCount);
        Assert.Equal(0, fixture.RetryScheduleCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_ProviderUnavailable_PausesWithoutReopeningJobs()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        fixture.Revalidator.Mismatch = RecoveryFixture.RecoveryMismatch.ProviderAvailability;
        ScanJobExecution job = await fixture.AddRunningJobAsync(1, _start.AddMinutes(5));
        fixture.Clock.SetUtcNow(_start.AddMinutes(10));
        var before = await fixture.VersionAndAttemptsAsync(job.Id);

        ScanRecoveryResult result = await fixture.ResumeAsync();

        Assert.Equal(DurableScanReason.ProviderUnavailable, result.Reason);
        Assert.Equal(before, await fixture.VersionAndAttemptsAsync(job.Id));
        Assert.Equal(ScanJobExecutionState.Running, await fixture.StateAsync(job.Id));
        Assert.Equal(0, fixture.DispatchCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_CompletedWithoutMatchingDocument_FailsIntegrityAndRetainsAnyValidDocumentRows()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        ScanJobExecution job = await fixture.AddRunningJobAsync(1, _start.AddMinutes(5));
        await fixture.UseContradictoryCompletedSnapshotAsync(job.Id);
        fixture.Clock.SetUtcNow(_start.AddMinutes(10));
        int documentCount = await fixture.DocumentCountAsync();

        ScanRecoveryResult result = await fixture.ResumeAsync();

        Assert.Equal(DurableScanReason.IntegrityViolation, result.Reason);
        Assert.Equal(documentCount, await fixture.DocumentCountAsync());
        Assert.Equal(0, fixture.DispatchCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResumeAsync_ResultIsBoundedAndNeverStartsAutomaticLoopOrWorkerDispatch()
    {
        await using RecoveryFixture fixture = await RecoveryFixture.CreateAsync();
        ScanRecoveryResult result = await fixture.ResumeAsync();

        Assert.True(Enum.IsDefined(result.Outcome));
        Assert.True(Enum.IsDefined(result.Reason));
        Assert.InRange(result.StateVersion ?? 0, 0, long.MaxValue);
        Assert.Equal(0, fixture.DispatchCount);
        Assert.Equal(0, fixture.RetryScheduleCount);
        Assert.Equal(1, fixture.ResumeInvocationCount);
    }

    private sealed class RecoveryFixture : IAsyncDisposable
    {
        private RecoveryFixture(
            SqliteConnection connection,
            VulgataDbContext context,
            SystemEntity system,
            Repository repository,
            ScanRun scanRun,
            ScanExecution execution,
            DurableScanAuthorizationScope scope)
        {
            Connection = connection;
            Context = context;
            System = system;
            Repository = repository;
            ScanRun = scanRun;
            Execution = execution;
            Scope = scope;
            Clock = new FixedTestClock(_start);
            Store = new DurableScanRepository(context);
            Revalidator = new FakeRecoveryRevalidator();
            // T024 RED: T025 must provide this concrete infrastructure seam
            // and the closed Core revalidation port below.
            Coordinator = new DurableScanRecoveryCoordinator(Store, Revalidator, Clock);
        }

        private SqliteConnection Connection { get; }
        internal VulgataDbContext Context { get; }
        private SystemEntity System { get; }
        private Repository Repository { get; }
        private ScanRun ScanRun { get; }
        private ScanExecution Execution { get; }
        internal DurableScanAuthorizationScope Scope { get; }
        internal Guid ExecutionId => Execution.Id;
        internal FixedTestClock Clock { get; }
        internal IDurableScanStore Store { get; private set; }
        internal FakeRecoveryRevalidator Revalidator { get; }
        internal IScanRecoveryCoordinator Coordinator { get; private set; }
        internal int DispatchCount { get; private set; }
        internal int RetryScheduleCount { get; private set; }
        internal int ResumeInvocationCount { get; private set; }

        internal static async Task<RecoveryFixture> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            VulgataDbContext context = new(new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite(connection)
                .Options);
            await context.Database.EnsureCreatedAsync();
            SystemEntity system = new("T024 system", null, null, _start);
            Repository repository = system.AddRepository(
                "T024 repository", "https://example.invalid/t024.git", null, null, _start);
            ScanRun scanRun = ScanRun.Start(repository.Id, 1_800, _start);
            scanRun.BindProfiledRevision(_revision);
            CodeUnit codeUnit = new(
                scanRun.Id,
                repository.Id,
                "src/T024.java",
                new string('b', 64),
                new string('c', 64),
                1,
                10,
                "T024",
                CodeUnitType.File,
                RangeConfidence.Exact,
                "java");
            context.Systems.Add(system);
            context.ScanRuns.Add(scanRun);
            context.CodeUnits.Add(codeUnit);
            await context.SaveChangesAsync();
            DurableScanAuthorizationScope scope = new("t024-actor", [system.Id]);
            ScanContextSnapshot snapshot = ScanContextSnapshot.Capture(
                scanRun, "global", "system", "repository", _start.AddMinutes(1));
            ScanExecution execution = ScanExecution.CreateNewPath(
                DeterministicTestIds.Create("t024-execution"),
                repository.Id,
                system.Id,
                scanRun.Id,
                _revision,
                "prompt/1.0",
                "document-schema/1.0",
                "{\"codegraph\":\"1.5.0\"}",
                _runtimeHash,
                _start.AddMinutes(2));
            DurableScanRepository store = new(context);
            Assert.Equal(DurableScanStoreOutcome.Applied,
                (await store.CreateContextSnapshotAsync(scope, snapshot, CancellationToken.None)).Outcome);
            Assert.Equal(DurableScanStoreOutcome.Applied,
                (await store.CreateScanExecutionAsync(scope, execution, CancellationToken.None)).Outcome);
            return new(connection, context, system, repository, scanRun, execution, scope);
        }

        internal Task<ScanRecoveryResult> ResumeAsync(
            DurableScanAuthorizationScope? scope = null,
            CancellationToken cancellationToken = default)
        {
            ResumeInvocationCount++;
            DurableScanAuthorizationScope effectiveScope = scope ?? Scope;
            return Coordinator.ResumeAsync(Execution.Id, effectiveScope, cancellationToken);
        }

        internal async Task<ScanJobExecution> AddRunningJobAsync(int manifestOrdinal, DateTimeOffset leaseExpiresAt)
        {
            ScanJobExecution job = await AddPendingJobAsync(manifestOrdinal);
            Guid lease = DeterministicTestIds.Create("t024-lease", manifestOrdinal);
            Assert.Equal(DurableScanStoreOutcome.Applied,
                (await Store.ClaimPendingJobAsync(
                    Scope,
                    new ScanJobLeaseCommand(
                        Execution.Id, job.Id, 0, lease, leaseExpiresAt, _start.AddMinutes(3)),
                    CancellationToken.None)).Outcome);
            return job;
        }

        internal async Task<ScanJobExecution> AddRetryScheduledJobAsync(int manifestOrdinal, DateTimeOffset nextAttemptAt)
        {
            ScanJobExecution job = await AddRunningJobAsync(manifestOrdinal, _start.AddMinutes(5));
            Assert.Equal(DurableScanStoreOutcome.Applied,
                (await Store.TransitionRunningJobAsync(
                    Scope,
                    new RunningScanJobTransitionCommand(
                        Execution.Id,
                        job.Id,
                        1,
                        DeterministicTestIds.Create("t024-lease", manifestOrdinal),
                        ScanJobExecutionState.RetryScheduled,
                        nextAttemptAt,
                        ScanJobFailureCategory.ProviderTransient,
                        "provider-timeout",
                        _start.AddMinutes(4)),
                    CancellationToken.None)).Outcome);
            return job;
        }

        internal async Task<ScanJobExecution> AddPendingJobAsync(int manifestOrdinal)
        {
            string path = $"src/T024-{manifestOrdinal}.java";
            CodeUnit? codeUnit = await Context.CodeUnits
                .SingleOrDefaultAsync(value => value.FilePath == path);
            if (codeUnit is null)
            {
                codeUnit = new CodeUnit(
                    ScanRun.Id,
                    Repository.Id,
                    path,
                    new string('b', 64),
                    new string('c', 64),
                    1,
                    10,
                    $"T024_{manifestOrdinal}",
                    CodeUnitType.File,
                    RangeConfidence.Exact,
                    "java");
                Context.CodeUnits.Add(codeUnit);
                await Context.SaveChangesAsync();
            }
            ScanJobExecution job = ScanJobExecution.Create(
                Execution.Id, Repository.Id, ScanRun.Id, codeUnit.Id, manifestOrdinal, _start.AddMinutes(2));
            if (await Context.ScanJobExecutions.AnyAsync())
            {
                // The durable store accepts the prepared manifest as one
                // ordered atomic batch. Additional rows here are fixture
                // setup for mixed-state recovery scenarios.
                Context.ScanJobExecutions.Add(job);
                await Context.SaveChangesAsync();
            }
            else
            {
                Assert.Equal(DurableScanStoreOutcome.Applied,
                    (await Store.InsertJobExecutionsAsync(Scope, Execution.Id, [job], CancellationToken.None)).Outcome);
            }
            return job;
        }

        internal async Task<ScanJobExecution> AddFailedJobAsync(int ordinal)
        {
            ScanJobExecution job = await AddRunningJobAsync(ordinal, _start.AddMinutes(5));
            await Store.TransitionRunningJobAsync(Scope,
                new RunningScanJobTransitionCommand(Execution.Id, job.Id, 1,
                    DeterministicTestIds.Create("t024-lease", ordinal), ScanJobExecutionState.Failed,
                    null, ScanJobFailureCategory.JobPermanent, "exhausted", _start.AddMinutes(4)), CancellationToken.None);
            return job;
        }

        internal async Task<ScanJobExecution> AddCancelledJobAsync(int ordinal)
        {
            ScanJobExecution job = await AddRunningJobAsync(ordinal, _start.AddMinutes(5));
            await Store.TransitionRunningJobAsync(Scope,
                new RunningScanJobTransitionCommand(Execution.Id, job.Id, 1,
                    DeterministicTestIds.Create("t024-lease", ordinal), ScanJobExecutionState.Cancelled,
                    null, ScanJobFailureCategory.Cancellation, "operator-cancelled", _start.AddMinutes(4)), CancellationToken.None);
            return job;
        }

        internal async Task UseContradictoryCompletedSnapshotAsync(Guid jobId)
        {
            ScanRecoverySnapshotResult read = await Store.ReadRecoverySnapshotAsync(
                Scope,
                Execution.Id,
                CancellationToken.None);
            ScanRecoverySnapshot snapshot = Assert.IsType<ScanRecoverySnapshot>(read.Snapshot);
            ScanRecoveryJobSnapshot[] jobs = snapshot.Jobs
                .Select(job => job.ScanJobExecutionId == jobId
                    ? job with
                    {
                        State = ScanJobExecutionState.Completed,
                        StateVersion = job.StateVersion + 1,
                        CommittedDocumentVersionId = DeterministicTestIds.Create(
                            "t024-missing-document", jobId),
                    }
                    : job)
                .ToArray();
            Store = new ContradictorySnapshotStore(
                Store,
                snapshot with { Jobs = jobs });
            Coordinator = new DurableScanRecoveryCoordinator(Store, Revalidator, Clock);
        }

        internal Task<int> AttemptCountAsync(Guid id) =>
            Context.ScanJobExecutions.Where(job => job.Id == id).Select(job => job.AttemptCount).SingleAsync();

        internal Task<long> ExecutionVersionAsync() =>
            Context.ScanExecutions.Where(value => value.Id == Execution.Id).Select(value => value.StateVersion).SingleAsync();

        internal Task<ScanJobExecutionState> StateAsync(Guid id) =>
            Context.ScanJobExecutions.Where(job => job.Id == id).Select(job => job.State).SingleAsync();

        internal Task<Guid> DeterministicJobIdAsync(int ordinal) =>
            Context.ScanJobExecutions.Where(job => job.ManifestOrdinal == ordinal).Select(job => job.Id).SingleAsync();

        internal async Task<(long Version, int Attempts)> VersionAndAttemptsAsync(Guid id)
        {
            var value = await Context.ScanJobExecutions
                .Where(job => job.Id == id)
                .Select(job => new { job.StateVersion, job.AttemptCount })
                .SingleAsync();
            return (value.StateVersion, value.AttemptCount);
        }

        internal async Task<Dictionary<Guid, (ScanJobExecutionState State, long Version)>> SnapshotJobStateAsync(params Guid[] ids) =>
            await Context.ScanJobExecutions.Where(job => ids.Contains(job.Id))
                .ToDictionaryAsync(job => job.Id, job => (job.State, job.StateVersion));

        internal async Task<string> SnapshotDurableStateAsync() =>
            JsonSerializer.Serialize(new
            {
                Execution = await Context.ScanExecutions.AsNoTracking()
                    .Where(value => value.Id == Execution.Id)
                    .Select(value => new { value.Phase, value.StateVersion })
                    .SingleAsync(),
                Jobs = await Context.ScanJobExecutions.AsNoTracking()
                    .OrderBy(job => job.ManifestOrdinal)
                    .Select(job => new { job.Id, job.State, job.StateVersion, job.AttemptCount })
                    .ToArrayAsync(),
            });

        internal BatchConflictStore InstallBatchConflictStore()
        {
            BatchConflictStore store = new(Store);
            Store = store;
            Coordinator = new DurableScanRecoveryCoordinator(Store, Revalidator, Clock);
            return store;
        }

        internal Task<int> DocumentCountAsync() => Context.DocumentVersions.CountAsync();

        internal async Task PauseProviderExecutionAsync()
        {
            Execution.BeginRunning(0, _start.AddMinutes(3));
            Execution.PauseProviderUnavailable(1, "provider-unavailable", _start.AddMinutes(4));
            await Context.SaveChangesAsync();
        }

        internal async Task PauseProviderAsync()
        {
            Revalidator.Mismatch = RecoveryMismatch.ProviderAvailability;
        }

        internal Task ApplyRevalidationMismatchAsync(string mismatch)
        {
            Revalidator.Mismatch = mismatch switch
            {
                "authorization" => RecoveryMismatch.Authorization,
                "profiled-revision" => RecoveryMismatch.ProfiledRevision,
                "affected-file-hash" => RecoveryMismatch.AffectedFileHash,
                "prompt-identity" => RecoveryMismatch.PromptIdentity,
                "schema-identity" => RecoveryMismatch.SchemaIdentity,
                "provider-availability" => RecoveryMismatch.ProviderAvailability,
                "analysis-runtime-identity" => RecoveryMismatch.AnalysisRuntimeIdentity,
                _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
            };
            return Task.CompletedTask;
        }

        private sealed class ContradictorySnapshotStore(
            IDurableScanStore inner,
            ScanRecoverySnapshot contradictorySnapshot) : IDurableScanStore
        {
            public Task<DurableScanStoreResult> CreateContextSnapshotAsync(
                DurableScanAuthorizationScope authorizationScope,
                ScanContextSnapshot snapshot,
                CancellationToken cancellationToken) =>
                inner.CreateContextSnapshotAsync(authorizationScope, snapshot, cancellationToken);

            public Task<DurableScanStoreResult> CreateScanExecutionAsync(
                DurableScanAuthorizationScope authorizationScope,
                ScanExecution execution,
                CancellationToken cancellationToken) =>
                inner.CreateScanExecutionAsync(authorizationScope, execution, cancellationToken);

            public Task<DurableScanStoreResult> InsertJobExecutionsAsync(
                DurableScanAuthorizationScope authorizationScope,
                Guid scanExecutionId,
                IReadOnlyList<ScanJobExecution> jobs,
                CancellationToken cancellationToken) =>
                inner.InsertJobExecutionsAsync(authorizationScope, scanExecutionId, jobs, cancellationToken);

            public Task<ScanRecoverySnapshotResult> ReadRecoverySnapshotAsync(
                DurableScanAuthorizationScope authorizationScope,
                Guid scanExecutionId,
                CancellationToken cancellationToken) =>
                Task.FromResult(new ScanRecoverySnapshotResult(
                    DurableScanStoreOutcome.Applied,
                    DurableScanReason.None,
                    contradictorySnapshot));

            public Task<DurableScanStoreResult> ApplyRecoveryTransitionAsync(
                DurableScanAuthorizationScope authorizationScope,
                ScanRecoveryTransitionCommand command,
                CancellationToken cancellationToken) =>
                inner.ApplyRecoveryTransitionAsync(authorizationScope, command, cancellationToken);

            public Task<DurableScanStoreResult> ApplyRecoveryTransitionsAsync(
                DurableScanAuthorizationScope authorizationScope,
                ScanRecoveryBatchTransitionCommand command,
                CancellationToken cancellationToken) =>
                inner.ApplyRecoveryTransitionsAsync(authorizationScope, command, cancellationToken);

            public Task<DurableScanStoreResult> ClaimPendingJobAsync(
                DurableScanAuthorizationScope authorizationScope,
                ScanJobLeaseCommand command,
                CancellationToken cancellationToken) =>
                inner.ClaimPendingJobAsync(authorizationScope, command, cancellationToken);

            public Task<DurableScanStoreResult> TransitionRunningJobAsync(
                DurableScanAuthorizationScope authorizationScope,
                RunningScanJobTransitionCommand command,
                CancellationToken cancellationToken) =>
                inner.TransitionRunningJobAsync(authorizationScope, command, cancellationToken);

            public Task<DurableScanProgressResult> ReadProgressAsync(
                DurableScanAuthorizationScope authorizationScope,
                Guid scanExecutionId,
                CancellationToken cancellationToken) =>
                inner.ReadProgressAsync(authorizationScope, scanExecutionId, cancellationToken);
        }

        internal sealed class BatchConflictStore(IDurableScanStore inner) : IDurableScanStore
        {
            internal int BatchInvocationCount { get; private set; }
            internal int SingleTransitionInvocationCount { get; private set; }
            internal ScanRecoveryBatchTransitionCommand? LastCommand { get; private set; }

            public Task<DurableScanStoreResult> CreateContextSnapshotAsync(
                DurableScanAuthorizationScope authorizationScope,
                ScanContextSnapshot snapshot,
                CancellationToken cancellationToken) =>
                inner.CreateContextSnapshotAsync(authorizationScope, snapshot, cancellationToken);

            public Task<DurableScanStoreResult> CreateScanExecutionAsync(
                DurableScanAuthorizationScope authorizationScope,
                ScanExecution execution,
                CancellationToken cancellationToken) =>
                inner.CreateScanExecutionAsync(authorizationScope, execution, cancellationToken);

            public Task<DurableScanStoreResult> InsertJobExecutionsAsync(
                DurableScanAuthorizationScope authorizationScope,
                Guid scanExecutionId,
                IReadOnlyList<ScanJobExecution> jobs,
                CancellationToken cancellationToken) =>
                inner.InsertJobExecutionsAsync(authorizationScope, scanExecutionId, jobs, cancellationToken);

            public Task<ScanRecoverySnapshotResult> ReadRecoverySnapshotAsync(
                DurableScanAuthorizationScope authorizationScope,
                Guid scanExecutionId,
                CancellationToken cancellationToken) =>
                inner.ReadRecoverySnapshotAsync(authorizationScope, scanExecutionId, cancellationToken);

            public Task<DurableScanStoreResult> ApplyRecoveryTransitionsAsync(
                DurableScanAuthorizationScope authorizationScope,
                ScanRecoveryBatchTransitionCommand command,
                CancellationToken cancellationToken)
            {
                BatchInvocationCount++;
                LastCommand = command;
                return Task.FromResult(new DurableScanStoreResult(
                    DurableScanStoreOutcome.Conflict,
                    DurableScanReason.StaleStateVersion,
                    command.ExpectedStateVersion,
                    null,
                    null));
            }

            public Task<DurableScanStoreResult> ApplyRecoveryTransitionAsync(
                DurableScanAuthorizationScope authorizationScope,
                ScanRecoveryTransitionCommand command,
                CancellationToken cancellationToken)
            {
                SingleTransitionInvocationCount++;
                return inner.ApplyRecoveryTransitionAsync(
                    authorizationScope,
                    command,
                    cancellationToken);
            }

            public Task<DurableScanStoreResult> ClaimPendingJobAsync(
                DurableScanAuthorizationScope authorizationScope,
                ScanJobLeaseCommand command,
                CancellationToken cancellationToken) =>
                inner.ClaimPendingJobAsync(authorizationScope, command, cancellationToken);

            public Task<DurableScanStoreResult> TransitionRunningJobAsync(
                DurableScanAuthorizationScope authorizationScope,
                RunningScanJobTransitionCommand command,
                CancellationToken cancellationToken) =>
                inner.TransitionRunningJobAsync(authorizationScope, command, cancellationToken);

            public Task<DurableScanProgressResult> ReadProgressAsync(
                DurableScanAuthorizationScope authorizationScope,
                Guid scanExecutionId,
                CancellationToken cancellationToken) =>
                inner.ReadProgressAsync(authorizationScope, scanExecutionId, cancellationToken);
        }

        internal sealed class FakeRecoveryRevalidator : IScanRecoveryRevalidator
        {
            internal RecoveryMismatch Mismatch { get; set; }

            // Proposed exact Core seam for T025:
            // Task<ScanRecoveryRevalidationResult> RevalidateAsync(
            //     ScanRecoverySnapshot snapshot,
            //     DurableScanAuthorizationScope authorizationScope,
            //     CancellationToken cancellationToken);
            public Task<ScanRecoveryRevalidationResult> RevalidateAsync(
                ScanRecoverySnapshot snapshot,
                DurableScanAuthorizationScope authorizationScope,
                CancellationToken cancellationToken)
            {
                ScanRecoveryRevalidationResult result = Mismatch switch
                {
                    RecoveryMismatch.None => new(true, DurableScanReason.None),
                    RecoveryMismatch.Authorization => new(false, DurableScanReason.Unauthorized),
                    RecoveryMismatch.ProfiledRevision => new(false, DurableScanReason.InvalidIdentity),
                    RecoveryMismatch.AffectedFileHash => new(false, DurableScanReason.IntegrityViolation),
                    RecoveryMismatch.PromptIdentity => new(false, DurableScanReason.InvalidIdentity),
                    RecoveryMismatch.SchemaIdentity => new(false, DurableScanReason.InvalidIdentity),
                    RecoveryMismatch.ProviderAvailability => new(false, DurableScanReason.ProviderUnavailable),
                    RecoveryMismatch.AnalysisRuntimeIdentity => new(false, DurableScanReason.InvalidIdentity),
                    _ => throw new ArgumentOutOfRangeException(),
                };
                return Task.FromResult(result);
            }
        }

        internal enum RecoveryMismatch
        {
            None,
            Authorization,
            ProfiledRevision,
            AffectedFileHash,
            PromptIdentity,
            SchemaIdentity,
            ProviderAvailability,
            AnalysisRuntimeIdentity,
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}

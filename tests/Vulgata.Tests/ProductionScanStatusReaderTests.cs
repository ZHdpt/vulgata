using Vulgata.Tests.TestInfrastructure;
using System.Collections;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Web.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

/// <summary>
/// T010 RED/characterization coverage for the production Scan status seam.
///
/// The production reader and its Core contracts are deliberately introduced
/// by T011.  Reflection keeps these tests compilable at the T010 boundary,
/// while the explicit type assertion gives an honest RED until that seam
/// exists.  The assertions below are against the accepted public contract;
/// they do not reach into reader implementation details.
/// </summary>
public sealed class ProductionScanStatusReaderTests
{
    private const string ReaderContractName =
        "Vulgata.Core.DomainServices.IProductionScanStatusReader";

    private const string ViewContractName =
        "Vulgata.Core.DomainServices.ProductionScanStatusView";

    private const string ResultContractName =
        "Vulgata.Core.DomainServices.ProductionScanStatusResult";

    private static readonly DateTimeOffset _now =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PublicSeam_DeclaresOwnershipFilteredListAndGetOperations()
    {
        Type readerContract = RequireContract(ReaderContractName);
        Type viewContract = RequireContract(ViewContractName);
        Type resultContract = RequireContract(ResultContractName);

        MethodInfo list = readerContract.GetMethod("ListVisibleAsync")!;
        MethodInfo get = readerContract.GetMethod("GetVisibleAsync")!;

        Assert.NotNull(list);
        Assert.NotNull(get);
        Assert.Equal(2, list.GetParameters().Length);
        Assert.Equal(3, get.GetParameters().Length);
        Assert.Equal(typeof(DurableScanAuthorizationScope), list.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(DurableScanAuthorizationScope), get.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(Guid), get.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(Task<>), list.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(Task<>), get.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(
            typeof(IReadOnlyList<>).MakeGenericType(viewContract),
            list.ReturnType.GetGenericArguments()[0]);
        Assert.Equal(resultContract, get.ReturnType.GetGenericArguments()[0]);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ProductionComposition_ResolvesStatusReaderAndCoordinatorAsScopedServices()
    {
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        using IServiceScope firstScope = factory.Services.CreateScope();
        using IServiceScope secondScope = factory.Services.CreateScope();

        IProductionScanStatusReader firstReader =
            firstScope.ServiceProvider.GetRequiredService<IProductionScanStatusReader>();
        ProductionScanStatusCoordinator firstCoordinator =
            firstScope.ServiceProvider.GetRequiredService<ProductionScanStatusCoordinator>();

        Assert.IsType<ProductionScanStatusReader>(firstReader);
        Assert.Same(firstReader, firstScope.ServiceProvider.GetRequiredService<IProductionScanStatusReader>());
        Assert.Same(firstCoordinator, firstScope.ServiceProvider.GetRequiredService<ProductionScanStatusCoordinator>());
        Assert.NotSame(firstReader, secondScope.ServiceProvider.GetRequiredService<IProductionScanStatusReader>());
        Assert.NotSame(firstCoordinator, secondScope.ServiceProvider.GetRequiredService<ProductionScanStatusCoordinator>());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ViewContract_ContainsBoundedStateCountsReasonResumeAndProductionEvidenceOrigin()
    {
        Type view = RequireContract(ViewContractName);

        AssertRequiredProperty(view, "ScanExecutionId");
        AssertRequiredProperty(view, "SystemId");
        AssertRequiredProperty(view, "RepositoryId");
        AssertRequiredProperty(view, "DisplayState");
        AssertRequiredProperty(view, "CompletedJobCount");
        AssertRequiredProperty(view, "TotalJobCount");
        AssertRequiredProperty(view, "PendingJobCount");
        AssertRequiredProperty(view, "RunningJobCount");
        AssertRequiredProperty(view, "RetryScheduledJobCount");
        AssertRequiredProperty(view, "FailedJobCount");
        AssertRequiredProperty(view, "NextRetryAt");
        AssertRequiredProperty(view, "ReasonCode");
        AssertRequiredProperty(view, "ReasonTextZh");
        AssertRequiredProperty(view, "CanResume");
        AssertRequiredProperty(view, "FailedFiles");
        AssertRequiredProperty(view, "FailedFileCount");
        AssertRequiredProperty(view, "ObservedAt");
        AssertRequiredProperty(view, "StateVersion");
        AssertRequiredProperty(view, "EvidenceEligibility");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(ProjectionState.Prepared, "Preparing")]
    [InlineData(ProjectionState.Running, "Running")]
    [InlineData(ProjectionState.RetryScheduled, "Retrying")]
    [InlineData(ProjectionState.Paused, "Paused")]
    [InlineData(ProjectionState.Resumed, "Resumed")]
    [InlineData(ProjectionState.Completed, "Completed")]
    [InlineData(ProjectionState.CompletedWithWarnings, "CompletedWithWarnings")]
    [InlineData(ProjectionState.FailedIntegrity, "FailedIntegrity")]
    [InlineData(ProjectionState.FailedSecurity, "FailedSecurity")]
    [InlineData(ProjectionState.Failed, "Failed")]
    [InlineData(ProjectionState.Cancelled, "Cancelled")]
    public async Task ListVisibleAsync_MapsEachPersistedPhaseToClosedDisplayState(
        ProjectionState state,
        string expectedDisplayState)
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync(state);
        object reader = CreateReader(fixture.Context);
        Type readerContract = RequireContract(ReaderContractName);
        DurableScanAuthorizationScope scope = fixture.AuthorizedScope;

        object[] views = await InvokeListAsync(readerContract, reader, scope);
        object view = Assert.Single(views);

        Assert.Equal(expectedDisplayState, GetProperty(view, "DisplayState")?.ToString());
        Assert.Equal("ProductionPersisted", GetProperty(view, "EvidenceEligibility")?.ToString());
        Assert.Equal(fixture.ExecutionId, GetProperty(view, "ScanExecutionId"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListVisibleAsync_DerivesCountsRetryTimeAndBoundedFailureReasonFromPersistedJobs()
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync(
            ProjectionState.RetryScheduled,
            failedFileCount: 25,
            retryJobCount: 2,
            completedJobCount: 1,
            totalJobCount: 28);
        object reader = CreateReader(fixture.Context);
        object view = Assert.Single(await InvokeListAsync(
            RequireContract(ReaderContractName),
            reader,
            fixture.AuthorizedScope));

        Assert.Equal(1, GetProperty(view, "CompletedJobCount"));
        Assert.Equal(28, GetProperty(view, "TotalJobCount"));
        Assert.Equal(2, GetProperty(view, "RetryScheduledJobCount"));
        Assert.Equal(fixture.EarliestRetryAt, GetProperty(view, "NextRetryAt"));
        Assert.Equal(25, GetProperty(view, "FailedFileCount"));
        Assert.True(((IEnumerable)GetProperty(view, "FailedFiles")!).Cast<object>().Count() <= 20);

        string reason = Assert.IsType<string>(GetProperty(view, "ReasonTextZh"));
        Assert.NotEmpty(reason);
        Assert.InRange(reason.Length, 1, 64);
        Assert.DoesNotContain("provider", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetVisibleAsync_HiddenAndUnauthorizedExecutionsAreMissingEquivalent()
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync(ProjectionState.Running);
        object reader = CreateReader(fixture.Context);
        Type readerContract = RequireContract(ReaderContractName);

        DurableScanAuthorizationScope unauthorized = new(
            "story125-other-owner",
            OwnedSystemIds: [Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")]);

        object authorizedResult = await InvokeGetAsync(
            readerContract,
            reader,
            fixture.AuthorizedScope,
            fixture.ExecutionId);
        object unauthorizedResult = await InvokeGetAsync(
            readerContract,
            reader,
            unauthorized,
            fixture.ExecutionId);
        object missingResult = await InvokeGetAsync(
            readerContract,
            reader,
            fixture.AuthorizedScope,
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));

        Assert.NotNull(authorizedResult);
        Assert.Equal(
            GetProperty(unauthorizedResult, "Outcome")?.ToString(),
            GetProperty(missingResult, "Outcome")?.ToString());
        Assert.Null(GetProperty(unauthorizedResult, "View"));
        Assert.Null(GetProperty(missingResult, "View"));
        Assert.DoesNotContain(
            "secret",
            unauthorizedResult.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ListVisibleAsync_OnlyRecoverableCurrentPausedStateCanResumeAndFakeStateIsNeverEligible()
    {
        await using ReaderFixture paused = await ReaderFixture.CreateAsync(ProjectionState.Paused);
        object reader = CreateReader(paused.Context);
        object pausedView = Assert.Single(await InvokeListAsync(
            RequireContract(ReaderContractName),
            reader,
            paused.AuthorizedScope));

        Assert.True((bool)GetProperty(pausedView, "CanResume")!);
        Assert.Equal("ProductionPersisted", GetProperty(pausedView, "EvidenceEligibility")?.ToString());

        Type fakeStateType = typeof(DemoScanUiState);
        Assert.DoesNotContain(
            fakeStateType.Assembly.GetTypes(),
            type => type == RequireContract(ViewContractName));
        Assert.NotEqual("Demo", GetProperty(pausedView, "EvidenceEligibility")?.ToString());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DurableScanStoreOutcome.Applied, ProductionScanResumeOutcome.Applied, DurableScanReason.None)]
    [InlineData(DurableScanStoreOutcome.Replay, ProductionScanResumeOutcome.Applied, DurableScanReason.None)]
    [InlineData(DurableScanStoreOutcome.Conflict, ProductionScanResumeOutcome.Conflict, DurableScanReason.StaleStateVersion)]
    [InlineData(DurableScanStoreOutcome.Rejected, ProductionScanResumeOutcome.Failed, DurableScanReason.Failed)]
    public async Task ResumeAsync_PostResumeOutcome_ReReadsVisiblePersistedStatus(
        DurableScanStoreOutcome recoveryOutcome,
        ProductionScanResumeOutcome expectedOutcome,
        DurableScanReason recoveryReason)
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync(ProjectionState.Paused);
        ProductionScanStatusView initial = CreateStatusView(
            fixture.ExecutionId,
            fixture.SystemId,
            fixture.RepositoryId,
            ProductionScanDisplayState.Paused,
            canResume: true,
            stateVersion: 1);
        ProductionScanStatusView refreshed = CreateStatusView(
            fixture.ExecutionId,
            fixture.SystemId,
            fixture.RepositoryId,
            ProductionScanDisplayState.Running,
            canResume: false,
            stateVersion: 2);
        RecordingResumeSeams statusReader = new(
            ProductionScanStatusResult.Found(initial),
            ProductionScanStatusResult.Found(refreshed));
        RecordingResumeSeams recovery = statusReader;
        recovery.RecoveryResult = new ScanRecoveryResult(recoveryOutcome, recoveryReason, 2);
        ProductionScanStatusCoordinator coordinator = new(
            new AdministratorUserContext(),
            new SystemRepository(fixture.Context),
            statusReader,
            recovery);

        ProductionScanResumeResult result = await coordinator.ResumeAsync(fixture.ExecutionId);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(ProductionScanStatusOutcome.Found, result.Status.Outcome);
        Assert.Equal(refreshed, result.Status.View);
        Assert.Equal(2, statusReader.GetCalls.Count);
        Assert.Equal(fixture.ExecutionId, statusReader.GetCalls[0].ExecutionId);
        Assert.Equal(fixture.ExecutionId, statusReader.GetCalls[1].ExecutionId);
        Assert.Null(statusReader.GetCalls[0].Scope.OwnedRepositoryIds);
        Assert.Equal([fixture.RepositoryId], statusReader.GetCalls[1].Scope.OwnedRepositoryIds);
        Assert.Single(recovery.RecoveryCalls);
        Assert.Equal(fixture.ExecutionId, recovery.RecoveryCalls[0].ExecutionId);
        Assert.Equal([fixture.RepositoryId], recovery.RecoveryCalls[0].Scope.OwnedRepositoryIds);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ExistingDurableEntities_CharacterizePersistedStateInputsWithoutFabricatingReaderRED()
    {
        ScanExecution execution = ScanExecution.CreateNewPath(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "abcdef0123456789abcdef0123456789abcdef01",
            "prompt/1.0",
            "code-logic/file-v2",
            "{\"codeGraph\":\"1.5.0\"}",
            new string('a', 64),
            _now);

        execution.BeginRunning(0, _now.AddSeconds(1));
        Assert.Equal(ScanExecutionPhase.Running, execution.Phase);
        Assert.Equal(1, execution.StateVersion);

        ScanJobExecution job = ScanJobExecution.Create(
            execution.Id,
            execution.RepositoryId,
            execution.ScanRunId,
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            1,
            _now);
        Assert.Equal(ScanJobExecutionState.Pending, job.State);

        // This is characterization of existing persisted entities.  No RED
        // is expected here; the missing behavior is the new reader seam.
        Assert.Equal(0, execution.CompletedJobCount);
        Assert.Equal(0, execution.TotalJobCount);
        Assert.Null(job.NextAttemptAt);
    }

    private static Type RequireContract(string fullName)
    {
        Type? type = typeof(DurableScanAuthorizationScope).Assembly.GetType(fullName)
            ?? typeof(VulgataDbContext).Assembly.GetType(fullName);
        Assert.True(
            type is not null,
            $"RED T010: accepted public contract {fullName} is absent; T011 must add it before this behavior can pass.");
        return type!;
    }

    private static void AssertRequiredProperty(Type type, string propertyName) =>
        Assert.NotNull(type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public));

    private static object CreateReader(VulgataDbContext context)
    {
        Type readerType = RequireContract(
            "Vulgata.Infrastructure.Data.ProductionScanStatusReader");
        ConstructorInfo? constructor = readerType.GetConstructors()
            .SingleOrDefault(value =>
                value.GetParameters().Length == 1 &&
                value.GetParameters()[0].ParameterType == typeof(VulgataDbContext));
        Assert.True(
            constructor is not null,
            "The accepted reader seam must be constructible from the existing VulgataDbContext without a new persistence seam.");
        return constructor!.Invoke([context]);
    }

    private static async Task<object[]> InvokeListAsync(
        Type readerContract,
        object reader,
        DurableScanAuthorizationScope scope)
    {
        MethodInfo method = readerContract.GetMethod("ListVisibleAsync")!;
        Task task = Assert.IsAssignableFrom<Task>(method.Invoke(
            reader,
            [scope, CancellationToken.None]));
        await task;
        object result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        return ((IEnumerable)result).Cast<object>().ToArray();
    }

    private static async Task<object> InvokeGetAsync(
        Type readerContract,
        object reader,
        DurableScanAuthorizationScope scope,
        Guid executionId)
    {
        MethodInfo method = readerContract.GetMethod("GetVisibleAsync")!;
        Task task = Assert.IsAssignableFrom<Task>(method.Invoke(
            reader,
            [scope, executionId, CancellationToken.None]));
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static object? GetProperty(object instance, string name) =>
        instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);

    public enum ProjectionState
    {
        Prepared,
        Running,
        RetryScheduled,
        Paused,
        Resumed,
        Completed,
        CompletedWithWarnings,
        FailedIntegrity,
        FailedSecurity,
        Failed,
        Cancelled,
    }

    private sealed class ReaderFixture : IAsyncDisposable
    {
        private ReaderFixture(
            SqliteConnection connection,
            VulgataDbContext context,
            Guid executionId,
            DurableScanAuthorizationScope authorizedScope,
            DateTimeOffset? earliestRetryAt)
        {
            Connection = connection;
            Context = context;
            ExecutionId = executionId;
            AuthorizedScope = authorizedScope;
            EarliestRetryAt = earliestRetryAt;
        }

        public SqliteConnection Connection { get; }
        public VulgataDbContext Context { get; }
        public Guid ExecutionId { get; }
        public Guid SystemId => Context.ScanExecutions.Single(execution => execution.Id == ExecutionId).SystemId;
        public Guid RepositoryId => Context.ScanExecutions.Single(execution => execution.Id == ExecutionId).RepositoryId;
        public DurableScanAuthorizationScope AuthorizedScope { get; }
        public DateTimeOffset? EarliestRetryAt { get; }

        public static async Task<ReaderFixture> CreateAsync(
            ProjectionState state,
            int failedFileCount = 0,
            int retryJobCount = 0,
            int completedJobCount = 0,
            int totalJobCount = 0)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite(connection)
                .Options;
            VulgataDbContext context = new(options);
            await context.Database.EnsureCreatedAsync();

            SystemEntity system = new("Story 12.5", null, null, _now);
            Repository repository = system.AddRepository(
                "story125-repository",
                "https://example.invalid/story125.git",
                null,
                null,
                _now);
            ScanRun scanRun = ScanRun.Start(repository.Id, 30, _now);
            scanRun.BindProfiledRevision("abcdef0123456789abcdef0123456789abcdef01");
            context.Add(system);
            context.Add(scanRun);
            await context.SaveChangesAsync();

            Guid executionId = Guid.NewGuid();
            ScanExecution execution = ScanExecution.CreateNewPath(
                executionId,
                repository.Id,
                system.Id,
                scanRun.Id,
                "abcdef0123456789abcdef0123456789abcdef01",
                "prompt/1.0",
                "code-logic/file-v2",
                "{\"codeGraph\":\"1.5.0\"}",
                new string('a', 64),
                _now);

            DateTimeOffset? earliestRetryAt = null;
            if (state is ProjectionState.Running or ProjectionState.RetryScheduled or
                ProjectionState.Paused or ProjectionState.Resumed or
                ProjectionState.Completed or ProjectionState.CompletedWithWarnings or
                ProjectionState.FailedIntegrity or ProjectionState.FailedSecurity or
                ProjectionState.Failed or ProjectionState.Cancelled)
            {
                execution.BeginRunning(0, _now.AddSeconds(1));
            }

            if (state == ProjectionState.RetryScheduled && retryJobCount == 0)
            {
                retryJobCount = 1;
            }

            switch (state)
            {
                case ProjectionState.RetryScheduled:
                    execution.PauseProviderUnavailable(1, "provider-timeout", _now.AddSeconds(2));
                    break;
                case ProjectionState.Paused:
                    execution.PauseProviderUnavailable(1, "provider-timeout", _now.AddSeconds(2));
                    break;
                case ProjectionState.Resumed:
                    execution.PauseProviderUnavailable(1, "provider-timeout", _now.AddSeconds(2));
                    execution.ResumeToPrepared(2, _now.AddSeconds(3));
                    break;
                case ProjectionState.Completed:
                    execution.Complete(1, _now.AddSeconds(2));
                    break;
                case ProjectionState.CompletedWithWarnings:
                    execution.CompleteWithWarnings(1, "warning", _now.AddSeconds(2));
                    break;
                case ProjectionState.FailedIntegrity:
                    execution.FailIntegrity(1, "revision-mismatch", _now.AddSeconds(2));
                    break;
                case ProjectionState.FailedSecurity:
                    execution.FailSecurity(1, "forbidden-content", _now.AddSeconds(2));
                    break;
                case ProjectionState.Failed:
                    execution.FailPass1(1, "pass1-failed", _now.AddSeconds(2));
                    break;
                case ProjectionState.Cancelled:
                    execution.Cancel(1, "operator-cancelled", _now.AddSeconds(2));
                    break;
            }

            context.Add(execution);
            Guid completedDocumentVersionId = Guid.Empty;
            if (completedJobCount > 0)
            {
                Guid logicalDocumentId = Guid.NewGuid();
                completedDocumentVersionId = Guid.NewGuid();
                LogicalDocument logicalDocument = LogicalDocument.Create(
                    logicalDocumentId,
                    repository.Id,
                    DocumentType.CodeLogic,
                    "story125/status-fixture",
                    _now);
                DocumentVersion documentVersion = DocumentVersion.Create(
                    completedDocumentVersionId,
                    logicalDocumentId,
                    repository.Id,
                    system.Id,
                    scanRun.Id,
                    "abcdef0123456789abcdef0123456789abcdef01",
                    1,
                    "Story 12.5 status fixture",
                    "src/story125/status-fixture.java",
                    KnowledgeDocumentLimits.SchemaIdentity,
                    "{}",
                    "status fixture",
                    new string('e', 64),
                    new string('f', 64),
                    _now);
                context.Add(logicalDocument);
                await context.SaveChangesAsync();
                context.Add(documentVersion);
            }
            int jobCount = Math.Max(totalJobCount, Math.Max(1, retryJobCount + completedJobCount));
            for (int index = 1; index <= jobCount; index++)
            {
                CodeUnit codeUnit = CodeUnit.CreatePreparedFileCodeUnit(
                    Guid.NewGuid(),
                    scanRun.Id,
                    repository.Id,
                    $"src/story125/File{index}.java",
                    new string('b', 64),
                    new string('c', 64),
                    0,
                    10,
                    1,
                    1,
                    index,
                    jobCount,
                    new string('d', 64),
                    "Java");
                context.Add(codeUnit);
                ScanJobExecution job = ScanJobExecution.Create(
                    execution.Id,
                    repository.Id,
                    scanRun.Id,
                    codeUnit.Id,
                    index,
                    _now);
                if (index <= completedJobCount)
                {
                    Guid lease = Guid.NewGuid();
                    job.AcquireLease(lease, _now.AddMinutes(5), _now.AddSeconds(1));
                    job.Complete(1, lease, completedDocumentVersionId, _now.AddSeconds(2));
                }
                else if (index <= completedJobCount + retryJobCount)
                {
                    Guid lease = Guid.NewGuid();
                    job.AcquireLease(lease, _now.AddMinutes(5), _now.AddSeconds(1));
                    DateTimeOffset retryAt = _now.AddMinutes(index);
                    job.ScheduleRetry(
                        1,
                        lease,
                        retryAt,
                        ScanJobFailureCategory.ProviderTransient,
                        "provider-timeout",
                        _now.AddSeconds(2));
                    earliestRetryAt = earliestRetryAt is null || retryAt < earliestRetryAt
                        ? retryAt
                        : earliestRetryAt;
                }
                else if (index > jobCount - failedFileCount)
                {
                    Guid lease = Guid.NewGuid();
                    job.AcquireLease(lease, _now.AddMinutes(5), _now.AddSeconds(1));
                    job.Fail(1, lease, ScanJobFailureCategory.JobPermanent, "job-failed", _now.AddSeconds(2));
                }
                context.Add(job);
            }

            await context.SaveChangesAsync();
            return new(connection, context, executionId, new("story125-owner", [system.Id]), earliestRetryAt);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private static ProductionScanStatusView CreateStatusView(
        Guid executionId,
        Guid systemId,
        Guid repositoryId,
        ProductionScanDisplayState displayState,
        bool canResume,
        long stateVersion) =>
        new(
            executionId,
            systemId,
            repositoryId,
            displayState,
            CompletedJobCount: 0,
            TotalJobCount: 1,
            PendingJobCount: displayState == ProductionScanDisplayState.Running ? 1 : 0,
            RunningJobCount: displayState == ProductionScanDisplayState.Running ? 1 : 0,
            RetryScheduledJobCount: 0,
            FailedJobCount: 0,
            NextRetryAt: null,
            ReasonCode: null,
            ReasonTextZh: null,
            canResume,
            FailedFiles: [],
            FailedFileCount: 0,
            ObservedAt: _now,
            stateVersion,
            ProductionScanEvidenceEligibility.ProductionPersisted);

    private sealed class AdministratorUserContext : IUserContext
    {
        public string UserId => "story125-administrator";

        public bool IsAdministrator => true;

        public SystemOwnerFilter ToSystemOwnerFilter() => SystemOwnerFilter.AllSystems();
    }

    private sealed class RecordingResumeSeams(params ProductionScanStatusResult[] results)
        : IProductionScanStatusReader, IScanRecoveryCoordinator
    {
        private readonly Queue<ProductionScanStatusResult> _results = new(results);

        public List<StatusReadCall> GetCalls { get; } = [];

        public List<RecoveryCall> RecoveryCalls { get; } = [];

        public ScanRecoveryResult RecoveryResult { get; set; } =
            new(DurableScanStoreOutcome.Applied, DurableScanReason.None, 1);

        public Task<IReadOnlyList<ProductionScanStatusView>> ListVisibleAsync(
            DurableScanAuthorizationScope scope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductionScanStatusView>>([]);

        public Task<ProductionScanStatusResult> GetVisibleAsync(
            DurableScanAuthorizationScope scope,
            Guid scanExecutionId,
            CancellationToken cancellationToken = default)
        {
            GetCalls.Add(new StatusReadCall(scope, scanExecutionId));
            ProductionScanStatusResult result = _results.Count > 0
                ? _results.Dequeue()
                : ProductionScanStatusResult.Missing();
            return Task.FromResult(result);
        }

        public Task<ScanRecoveryResult> ResumeAsync(
            Guid scanExecutionId,
            DurableScanAuthorizationScope authorizationScope,
            CancellationToken cancellationToken)
        {
            RecoveryCalls.Add(new RecoveryCall(scanExecutionId, authorizationScope));
            return Task.FromResult(RecoveryResult);
        }
    }

    private sealed record StatusReadCall(
        DurableScanAuthorizationScope Scope,
        Guid ExecutionId);

    private sealed record RecoveryCall(
        Guid ExecutionId,
        DurableScanAuthorizationScope Scope);
}

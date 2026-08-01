using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

public sealed class SequentialScanRunnerDocumentGenerationTests
{
    [Fact]
    public async Task RunCurrentBoundaryAsync_CommittedPreparedRepository_GeneratesExactDocumentsSecurely()
    {
        const string plaintextCredential = "g7-plaintext-secret";
        const string rawDiagnostic = "provider stack at C:\\sensitive\\worker.cs";
        const string unrelatedSource = "UNRELATED-SOURCE-MUST-NOT-CROSS";
        await using SqliteKnowledgeStoreSession session =
            await KnowledgeStoreTestHarness.CreateSqliteSessionAsync();
        using var git = new G7TemporaryGitRepository(unrelatedSource);
        PreparedPass2Flow flow = PreparedPass2FlowCatalog
            .Load(typeof(WorkerAgentDispatcher).Assembly)
            .Flow;
        Assert.Equal(WorkerAgentTestFixtures.Id(4), flow.SystemId);
        Assert.Equal(WorkerAgentTestFixtures.Id(3), flow.RepositoryId);

        (Repository repository, ScanRun scanRun) = await SeedG7PreparedRepositoryAsync(
            session.Context,
            flow,
            git);
        Guid systemId = Assert.IsType<Guid>(repository.SystemId);
        git.MoveHeadAndDirty();
        Assert.NotEqual(git.ProfiledRevision, git.HeadRevision);
        Assert.False(string.IsNullOrWhiteSpace(git.Status));

        var scope = new RepositoryAccessScope([repository.Id]);
        var events = new List<string>();
        var executionStore = new G7ExecutionStore(
            new ScanExecutionRepository(session.Context),
            session.Context,
            events);
        var encryption = new G7EncryptionService(plaintextCredential);
        string encryptedAssignment = await encryption.EncryptAsync(
            SecretPurposes.LlmApiKey,
            plaintextCredential);
        var provider = new LlmProvider(
            "g7-loopback-worker",
            "http://127.0.0.1:42007/v1",
            encryptedAssignment,
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UnixEpoch);
        SystemLlmProviderOverride systemAssignment = SystemLlmProviderOverride.Create(
            systemId,
            provider.Id,
            AgentType.Worker,
            DateTimeOffset.UnixEpoch);
        var loopback = new G7LoopbackHandler(events);
        var knowledgeStore = new KnowledgeStoreRepository(
            session.Context,
            new KnowledgeDocumentMaterializer());
        var services = new ServiceCollection();
        services.AddSingleton(flow);
        services.AddSingleton<IKnowledgeStoreRepository>(knowledgeStore);
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        WorkerAgentResultCommitter durableCommitter =
            ActivatorUtilities.CreateInstance<WorkerAgentResultCommitter>(serviceProvider);
        var recordingCommitter = new G7RecordingCommitter(durableCommitter, events);
        var providerResolver = new WorkerAgentProviderResolver(
            new G7OverrideRepository(systemAssignment),
            new G7ProviderRepository(provider));
        var contextSource = new RevisionBoundWorkerExecutionContextSource(
            new G7RepositoryRepository(repository),
            new G7WorkingCopyService(git.RootPath, git.ProfiledRevision),
            new RevisionBoundWorkerContextHydrator(
                new G7SnapshotService(
                    repository.Id,
                    scanRun.Id,
                    git.ProfiledRevision,
                    git.SnapshotPath)));
        var dispatcher = new WorkerAgentDispatcher(
            providerResolver,
            encryption,
            contextSource,
            loopback,
            new G7TimeProvider());
        services = new ServiceCollection();
        services.AddSingleton(flow);
        var handoffSource = new G7HandoffSource(
            repository.Id,
            scanRun.Id,
            git.ProfiledRevision);
        services.AddSingleton<IApprovedPreScanHandoffSource>(handoffSource);
        services.AddSingleton<IScanExecutionStore>(executionStore);
        services.AddSingleton<ICodeUnitSource>(new CodeUnitSource(session.Context));
        services.AddSingleton<IWorkerAgentDispatcher>(dispatcher);
        services.AddSingleton<IWorkerAgentResultCommitter>(recordingCommitter);
        services.AddSingleton<TimeProvider>(new G7TimeProvider());
        using ServiceProvider runnerProvider = services.BuildServiceProvider();
        SequentialScanRunner runner =
            ActivatorUtilities.CreateInstance<SequentialScanRunner>(runnerProvider);

        ApprovedPreScanHandoff handoff = Assert.IsType<ApprovedPreScanHandoff>(
            await handoffSource.GetAsync(repository.Id, scanRun.Id));
        Guid executionId = Guid.NewGuid();
        await executionStore.PersistAsync(ScanExecution.Start(
            executionId,
            handoff.RepositoryId,
            systemId,
            handoff.ScanRunId,
            handoff.ResultId,
            handoff.ProfiledRevision,
            handoff.ApprovedAt));
        await runner.RunPass1Async(executionId, scope);

        ScanExecutionStateView pass1State = await runner.ReadStateAsync(executionId);
        Assert.Equal(ScanExecutionPhase.Pass1Completed, pass1State.Phase);
        Assert.Equal(flow.Members.Count, pass1State.CompletedJobCount);
        Assert.True(executionStore.CommitWasVisibleBeforeEveryProgress);
        Assert.Equal(flow.Members.Count, loopback.Pass1Requests.Count);
        Assert.Equal(
            git.CommittedSlices.Order(StringComparer.Ordinal),
            loopback.Pass1Requests.Select(request => request.SourceText)
                .Order(StringComparer.Ordinal));

        var candidates = new List<PreparedPass2CodeLogicCandidate>();
        foreach (PreparedPass2FlowMember member in flow.Members)
        {
            G7Pass1Request request = Assert.Single(
                loopback.Pass1Requests,
                value => value.CodeUnitId == member.CodeUnitId);
            CurrentKnowledge current = Assert.IsType<CurrentKnowledge>(
                await knowledgeStore.GetCurrentAsync(
                    member.LogicalDocumentId,
                    scope,
                    [G7LoopbackHandler.SectionId(member.CodeUnitId)]));
            candidates.Add(new(
                request.JobId,
                member.CodeUnitId,
                systemId,
                repository.Id,
                scanRun.Id,
                git.ProfiledRevision,
                current.DocumentVersionId,
                current));
        }

        ResolvedPreparedPass2Flow resolved = PreparedPass2FlowResolver.Resolve(
            flow,
            systemId,
            repository.Id,
            scanRun.Id,
            git.ProfiledRevision,
            scope,
            candidates);
        Assert.Equal(
            flow.Members.Select(member => member.LogicalDocumentId),
            resolved.CodeLogicDocuments.Select(document => document.LogicalDocumentId));

        await runner.RunPreparedPass2Async(executionId, resolved, scope);

        ScanExecutionStateView completed = await runner.ReadStateAsync(executionId);
        Assert.Equal(ScanExecutionPhase.Completed, completed.Phase);
        Assert.Single(loopback.Pass2Requests);
        Assert.Equal(
            resolved.CodeLogicDocuments.Select(document => document.DocumentVersionId),
            loopback.Pass2Requests[0].DocumentVersionIds);
        Assert.Equal(
            ["pass1-completed", "pass2-dispatch", "pass2-knowledge", "completed"],
            events.Where(value =>
                value is "pass1-completed" or "pass2-dispatch" or
                    "pass2-knowledge" or "completed"));
        Assert.True(executionStore.BusinessCommitWasVisibleBeforeCompleted);

        Guid businessVersionId = Assert.IsType<Guid>(await session.Context.LogicalDocuments
            .AsNoTracking()
            .Where(document => document.DocumentType == DocumentType.BusinessLogic)
            .Select(document => document.CurrentVersionId)
            .SingleAsync());
        DocumentVersion businessVersion = await session.Context.DocumentVersions
            .AsNoTracking()
            .SingleAsync(version => version.Id == businessVersionId);
        DocumentEdge[] provenance = await session.Context.DocumentEdges
            .AsNoTracking()
            .Where(edge => edge.SourceDocumentVersionId == businessVersion.Id)
            .ToArrayAsync();
        Assert.Equal(
            resolved.CodeLogicDocuments.Select(document => document.DocumentVersionId)
                .Order(),
            provenance.Select(edge => edge.TargetDocumentVersionId).Order());
        Assert.All(
            provenance,
            edge => Assert.Equal(DocumentEdgeType.GeneratedFrom, edge.EdgeType));
        Assert.DoesNotContain(
            await session.Context.DocumentEdges.AsNoTracking().ToArrayAsync(),
            edge => edge.EdgeType == DocumentEdgeType.References);

        Assert.Equal(flow.Members.Count + 1, encryption.DecryptCount);
        Assert.All(loopback.AuthorizationParameters, value => Assert.Equal(plaintextCredential, value));
        string capturedSecuritySurface = string.Join(
            Environment.NewLine,
            [
                .. loopback.RequestBodies,
                .. await ReadG7DurableTextAsync(session.Context),
                completed.TerminalMessage ?? string.Empty,
            ]);
        Assert.DoesNotContain(plaintextCredential, capturedSecuritySurface, StringComparison.Ordinal);
        Assert.DoesNotContain(rawDiagnostic, capturedSecuritySurface, StringComparison.Ordinal);
        Assert.DoesNotContain(unrelatedSource, capturedSecuritySurface, StringComparison.Ordinal);
        Assert.DoesNotContain(git.RootPath, capturedSecuritySurface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(git.SnapshotPath, capturedSecuritySurface, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunPreparedPass2Async_ExactResolvedFlow_CommitsBeforeCompletedExactlyOnce()
    {
        Guid executionId = WorkerAgentTestFixtures.Id(301);
        Guid repositoryId = WorkerAgentTestFixtures.Id(302);
        Guid systemId = WorkerAgentTestFixtures.Id(303);
        Guid scanRunId = WorkerAgentTestFixtures.Id(304);
        ScanExecution execution = CreatePass1CompletedExecution(
            executionId,
            repositoryId,
            systemId,
            scanRunId);
        var events = new List<string>();
        var store = new RecordingExecutionStore(execution, events);
        var dispatcher = new ValidPass2Dispatcher(events);
        var committer = new GatedPass2Committer(events);
        ResolvedPreparedPass2Flow resolved = CreateResolvedPass2Flow(
            repositoryId,
            systemId,
            scanRunId);
        var runner = new SequentialScanRunner(
            new EmptyHandoffSource(),
            store,
            new EmptyCodeUnitSource(),
            dispatcher,
            committer,
            resolved.Flow,
            new FixedTimeProvider());

        Task run = runner.RunPreparedPass2Async(
            executionId,
            resolved,
            new RepositoryAccessScope([repositoryId]),
            CancellationToken.None);
        await committer.WaitUntilCalledAsync();

        Assert.False(run.IsCompleted);
        Assert.Equal(ScanExecutionPhase.Pass1Completed, execution.Phase);
        Assert.NotNull(execution.InFlightJobId);
        WorkerAgentJobPacket packet = Assert.Single(dispatcher.Packets);
        var payload = Assert.IsType<Pass2BusinessLogicJobPayload>(packet.Payload);
        Assert.Equal(
            resolved.CodeLogicDocuments.Select(document => document.DocumentVersionId),
            payload.CodeLogicDocumentVersions.Select(document => document.DocumentVersionId));
        Assert.Equal(1, committer.CallCount);
        Assert.DoesNotContain("completed", events);

        committer.Release(WorkerAgentResultCommitOutcome.Committed);
        await run;

        Assert.Equal(ScanExecutionPhase.Completed, execution.Phase);
        Assert.Null(execution.InFlightJobId);
        Assert.NotNull(execution.TerminalAt);
        Assert.Equal(1, dispatcher.Packets.Count);
        Assert.Equal(1, committer.CallCount);
        Assert.Equal(
            ["pass2-dispatch", "pass2-commit", "completed"],
            events.Where(value => value is "pass2-dispatch" or "pass2-commit" or "completed"));
    }

    [Fact]
    public async Task RunPreparedPass2Async_AlternateSelfConsistentFlow_RejectsBeforeDispatch()
    {
        Guid executionId = WorkerAgentTestFixtures.Id(314);
        Guid repositoryId = WorkerAgentTestFixtures.Id(315);
        Guid systemId = WorkerAgentTestFixtures.Id(316);
        Guid scanRunId = WorkerAgentTestFixtures.Id(317);
        ScanExecution execution = CreatePass1CompletedExecution(
            executionId,
            repositoryId,
            systemId,
            scanRunId);
        ResolvedPreparedPass2Flow authoritative = CreateResolvedPass2Flow(
            repositoryId,
            systemId,
            scanRunId);
        PreparedPass2Flow alternateFlow = PreparedPass2Flow.Create(
            authoritative.Flow.SchemaIdentity,
            WorkerAgentTestFixtures.Id(318),
            WorkerAgentTestFixtures.Id(319),
            systemId,
            repositoryId,
            authoritative.Flow.Members,
            authoritative.Flow.TraceEvidence);
        var events = new List<string>();
        var dispatcher = new ValidPass2Dispatcher(events);
        var committer = new CountingCommitter();
        var runner = new SequentialScanRunner(
            new EmptyHandoffSource(),
            new RecordingExecutionStore(execution, events),
            new EmptyCodeUnitSource(),
            dispatcher,
            committer,
            authoritative.Flow,
            new FixedTimeProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunPreparedPass2Async(
                executionId,
                new(alternateFlow, authoritative.CodeLogicDocuments),
                new RepositoryAccessScope([repositoryId]),
                CancellationToken.None));

        Assert.Empty(dispatcher.Packets);
        Assert.Equal(0, committer.CallCount);
        Assert.Equal(ScanExecutionPhase.Pass1Completed, execution.Phase);
    }

    [Fact]
    public async Task RunPreparedPass2Async_CancelledInFlightReentry_ReusesJobAndCompletes()
    {
        Guid executionId = WorkerAgentTestFixtures.Id(325);
        Guid repositoryId = WorkerAgentTestFixtures.Id(326);
        Guid systemId = WorkerAgentTestFixtures.Id(327);
        Guid scanRunId = WorkerAgentTestFixtures.Id(328);
        ScanExecution execution = CreatePass1CompletedExecution(
            executionId,
            repositoryId,
            systemId,
            scanRunId);
        ResolvedPreparedPass2Flow resolved = CreateResolvedPass2Flow(
            repositoryId,
            systemId,
            scanRunId);
        var events = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var inner = new ValidPass2Dispatcher(events);
        var dispatcher = new CancelOncePass2Dispatcher(cancellation, inner);
        var runner = new SequentialScanRunner(
            new EmptyHandoffSource(),
            new RecordingExecutionStore(execution, events),
            new EmptyCodeUnitSource(),
            dispatcher,
            new CountingCommitter(),
            resolved.Flow,
            new FixedTimeProvider());
        RepositoryAccessScope scope = new([repositoryId]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunPreparedPass2Async(
                executionId,
                resolved,
                scope,
                cancellation.Token));
        Guid durableJobId = Assert.IsType<Guid>(execution.InFlightJobId);

        await runner.RunPreparedPass2Async(
            executionId,
            resolved,
            scope,
            CancellationToken.None);

        Assert.Equal([durableJobId, durableJobId], dispatcher.JobIds);
        Assert.Equal(ScanExecutionPhase.Completed, execution.Phase);
        Assert.Null(execution.InFlightJobId);
    }

    [Theory]
    [InlineData(WorkerAgentResultCommitOutcome.Conflict)]
    [InlineData(WorkerAgentResultCommitOutcome.Rejected)]
    [InlineData(WorkerAgentResultCommitOutcome.Cancelled)]
    [InlineData(WorkerAgentResultCommitOutcome.Failed)]
    public async Task RunPreparedPass2Async_NonCommittedKnowledgeOutcome_NeverCompletes(
        WorkerAgentResultCommitOutcome outcome)
    {
        Guid executionId = WorkerAgentTestFixtures.Id(321);
        Guid repositoryId = WorkerAgentTestFixtures.Id(322);
        Guid systemId = WorkerAgentTestFixtures.Id(323);
        Guid scanRunId = WorkerAgentTestFixtures.Id(324);
        ScanExecution execution = CreatePass1CompletedExecution(
            executionId,
            repositoryId,
            systemId,
            scanRunId);
        var events = new List<string>();
        var dispatcher = new ValidPass2Dispatcher(events);
        var committer = new FixedOutcomeCommitter(outcome);
        ResolvedPreparedPass2Flow resolved = CreateResolvedPass2Flow(
            repositoryId,
            systemId,
            scanRunId);
        var runner = new SequentialScanRunner(
            new EmptyHandoffSource(),
            new RecordingExecutionStore(execution, events),
            new EmptyCodeUnitSource(),
            dispatcher,
            committer,
            resolved.Flow,
            new FixedTimeProvider());

        await runner.RunPreparedPass2Async(
            executionId,
            resolved,
            new RepositoryAccessScope([repositoryId]),
            CancellationToken.None);

        Assert.NotEqual(ScanExecutionPhase.Completed, execution.Phase);
        Assert.Equal(ScanExecutionPhase.Failed, execution.Phase);
        Assert.Single(dispatcher.Packets);
        Assert.Equal(1, committer.CallCount);
        Assert.DoesNotContain("completed", events);
    }

    [Theory]
    [InlineData(WorkerAgentResultOutcome.SchemaInvalid)]
    [InlineData(WorkerAgentResultOutcome.Error)]
    [InlineData(WorkerAgentResultOutcome.Unbounded)]
    public async Task RunPreparedPass2Async_NonValidDispatchOutcome_NeverCommitsOrCompletes(
        WorkerAgentResultOutcome outcome)
    {
        Guid executionId = WorkerAgentTestFixtures.Id(331);
        Guid repositoryId = WorkerAgentTestFixtures.Id(332);
        Guid systemId = WorkerAgentTestFixtures.Id(333);
        Guid scanRunId = WorkerAgentTestFixtures.Id(334);
        ScanExecution execution = CreatePass1CompletedExecution(
            executionId,
            repositoryId,
            systemId,
            scanRunId);
        var events = new List<string>();
        var committer = new CountingCommitter();
        ResolvedPreparedPass2Flow resolved = CreateResolvedPass2Flow(
            repositoryId,
            systemId,
            scanRunId);
        var runner = new SequentialScanRunner(
            new EmptyHandoffSource(),
            new RecordingExecutionStore(execution, events),
            new EmptyCodeUnitSource(),
            new NonValidPass2Dispatcher(outcome),
            committer,
            resolved.Flow,
            new FixedTimeProvider());

        await runner.RunPreparedPass2Async(
            executionId,
            resolved,
            new RepositoryAccessScope([repositoryId]),
            CancellationToken.None);

        Assert.NotEqual(ScanExecutionPhase.Completed, execution.Phase);
        Assert.Equal(ScanExecutionPhase.Failed, execution.Phase);
        Assert.Equal(0, committer.CallCount);
        Assert.DoesNotContain("completed", events);
    }

    [Fact]
    public async Task RunPreparedPass2Async_DispatchFailure_NeverCommitsOrCompletes()
    {
        Guid executionId = WorkerAgentTestFixtures.Id(341);
        Guid repositoryId = WorkerAgentTestFixtures.Id(342);
        Guid systemId = WorkerAgentTestFixtures.Id(343);
        Guid scanRunId = WorkerAgentTestFixtures.Id(344);
        ScanExecution execution = CreatePass1CompletedExecution(
            executionId,
            repositoryId,
            systemId,
            scanRunId);
        var events = new List<string>();
        var committer = new CountingCommitter();
        ResolvedPreparedPass2Flow resolved = CreateResolvedPass2Flow(
            repositoryId,
            systemId,
            scanRunId);
        var runner = new SequentialScanRunner(
            new EmptyHandoffSource(),
            new RecordingExecutionStore(execution, events),
            new EmptyCodeUnitSource(),
            new ThrowingDispatcher(new InvalidOperationException("provider diagnostic")),
            committer,
            resolved.Flow,
            new FixedTimeProvider());

        await runner.RunPreparedPass2Async(
            executionId,
            resolved,
            new RepositoryAccessScope([repositoryId]),
            CancellationToken.None);

        Assert.NotEqual(ScanExecutionPhase.Completed, execution.Phase);
        Assert.Equal(ScanExecutionPhase.Failed, execution.Phase);
        Assert.Equal(0, committer.CallCount);
        Assert.DoesNotContain("provider diagnostic", execution.TerminalMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("completed", events);
    }

    [Fact]
    public async Task RunPass1Async_ValidResult_AwaitsKnowledgeBeforeProgressAndStopsAtPass1Completed()
    {
        Guid executionId = WorkerAgentTestFixtures.Id(201);
        Guid repositoryId = WorkerAgentTestFixtures.Id(202);
        Guid systemId = WorkerAgentTestFixtures.Id(203);
        Guid scanRunId = WorkerAgentTestFixtures.Id(204);
        var execution = ScanExecution.Start(
            executionId,
            repositoryId,
            systemId,
            scanRunId,
            WorkerAgentTestFixtures.Id(205),
            WorkerAgentTestFixtures.ProfiledRevision,
            DateTimeOffset.UnixEpoch);
        var events = new List<string>();
        var store = new RecordingExecutionStore(execution, events);
        CodeUnit unit = new(
            scanRunId,
            repositoryId,
            WorkerAgentTestFixtures.RepositoryRelativePath,
            WorkerAgentTestFixtures.FileIdentityHash,
            WorkerAgentTestFixtures.SourceHash,
            12,
            24,
            "CalculateTotal",
            CodeUnitType.Method,
            RangeConfidence.Exact,
            "csharp");
        var committer = new GatedCommitter(events);
        var runner = new SequentialScanRunner(
            new EmptyHandoffSource(),
            store,
            new SingleCodeUnitSource(unit),
            new ValidDispatcher(),
            committer,
            new FixedTimeProvider());
        RepositoryAccessScope scope = new([repositoryId]);

        Task run = runner.RunPass1Async(executionId, scope, CancellationToken.None);
        await committer.WaitUntilCalledAsync();

        Assert.False(run.IsCompleted);
        Assert.Equal(0, execution.CompletedJobCount);
        Assert.NotNull(execution.InFlightJobId);
        Assert.DoesNotContain("progress", events);

        committer.Release(WorkerAgentResultCommitOutcome.Committed);
        await run;

        Assert.Equal(["knowledge", "progress"], events.Where(value =>
            value is "knowledge" or "progress"));
        Assert.Equal(1, execution.CompletedJobCount);
        Assert.Null(execution.InFlightJobId);
        Assert.Equal(ScanExecutionPhase.Pass1Completed, execution.Phase);
        Assert.Null(execution.TerminalAt);
    }

    [Fact]
    public async Task RunPass1Async_AuthoritativePreparedFlow_CarriesClosedAuthorizedContext()
    {
        Guid executionId = WorkerAgentTestFixtures.Id(231);
        Guid repositoryId = WorkerAgentTestFixtures.Id(232);
        Guid systemId = WorkerAgentTestFixtures.Id(233);
        Guid scanRunId = WorkerAgentTestFixtures.Id(234);
        var execution = ScanExecution.Start(
            executionId,
            repositoryId,
            systemId,
            scanRunId,
            WorkerAgentTestFixtures.Id(235),
            WorkerAgentTestFixtures.ProfiledRevision,
            DateTimeOffset.UnixEpoch);
        CodeUnit unit = new(
            scanRunId,
            repositoryId,
            WorkerAgentTestFixtures.RepositoryRelativePath,
            WorkerAgentTestFixtures.FileIdentityHash,
            WorkerAgentTestFixtures.SourceHash,
            12,
            24,
            "CalculateTotal",
            CodeUnitType.Method,
            RangeConfidence.Exact,
            "csharp");
        Guid currentJobId = WorkerAgentTestFixtures.Id(236);
        Guid neighboringJobId = WorkerAgentTestFixtures.Id(237);
        Guid neighboringCodeUnitId = WorkerAgentTestFixtures.Id(238);
        PreparedPass2Flow flow = PreparedPass2Flow.Create(
            PreparedPass2FlowLimits.SupportedSchemaIdentity,
            WorkerAgentTestFixtures.Id(239),
            WorkerAgentTestFixtures.Id(240),
            systemId,
            repositoryId,
            [
                new(1, currentJobId, unit.Id, WorkerAgentTestFixtures.Id(241)),
                new(2, neighboringJobId, neighboringCodeUnitId, WorkerAgentTestFixtures.Id(242)),
            ],
            [
                new(1, currentJobId, unit.Id, "订单总额计算", "src/Orders/OrderService.cs#L12-L24"),
                new(2, neighboringJobId, neighboringCodeUnitId, "库存预留", "src/Inventory/InventoryService.cs#L30-L44"),
            ]);
        var dispatcher = new ValidDispatcher();
        var runner = new SequentialScanRunner(
            new EmptyHandoffSource(),
            new RecordingExecutionStore(execution, []),
            new SingleCodeUnitSource(unit),
            dispatcher,
            new FixedOutcomeCommitter(WorkerAgentResultCommitOutcome.Committed),
            flow,
            new FixedTimeProvider());

        await runner.RunPass1Async(
            executionId,
            new RepositoryAccessScope([repositoryId]),
            CancellationToken.None);

        WorkerAgentJobPacket packet = Assert.Single(dispatcher.Packets);
        var payload = Assert.IsType<Pass1CodeLogicJobPayload>(packet.Payload);
        Assert.Contains(
            payload.NeighboringStructuralContext,
            context => context.CodeUnitId == neighboringCodeUnitId);
        Assert.NotEmpty(payload.ApprovedCommunicationEvidence);
        Assert.Equal([neighboringJobId], payload.AllowedReferenceDocumentVersionIds);
    }

    [Fact]
    public async Task RunPass1Async_ProgressPersistenceRetry_ReplaysIdenticalKnowledgeFirst()
    {
        Guid executionId = WorkerAgentTestFixtures.Id(211);
        Guid repositoryId = WorkerAgentTestFixtures.Id(212);
        Guid systemId = WorkerAgentTestFixtures.Id(213);
        Guid scanRunId = WorkerAgentTestFixtures.Id(214);
        var execution = ScanExecution.Start(
            executionId,
            repositoryId,
            systemId,
            scanRunId,
            WorkerAgentTestFixtures.Id(215),
            WorkerAgentTestFixtures.ProfiledRevision,
            DateTimeOffset.UnixEpoch);
        var events = new List<string>();
        var store = new FailFirstProgressStore(execution, events);
        CodeUnit unit = new(
            scanRunId,
            repositoryId,
            WorkerAgentTestFixtures.RepositoryRelativePath,
            WorkerAgentTestFixtures.FileIdentityHash,
            WorkerAgentTestFixtures.SourceHash,
            12,
            24,
            "CalculateTotal",
            CodeUnitType.Method,
            RangeConfidence.Exact,
            "csharp");
        var committer = new RecordingCommitter(events);
        var runner = new SequentialScanRunner(
            new EmptyHandoffSource(),
            store,
            new SingleCodeUnitSource(unit),
            new ValidDispatcher(),
            committer,
            new FixedTimeProvider());

        await runner.RunPass1Async(
            executionId,
            new RepositoryAccessScope([repositoryId]),
            CancellationToken.None);

        Assert.Equal(2, committer.Calls.Count);
        Assert.Same(committer.Calls[0].Packet, committer.Calls[1].Packet);
        Assert.Same(committer.Calls[0].Result, committer.Calls[1].Result);
        Assert.Equal(
            ["knowledge", "progress-attempt", "knowledge", "progress-attempt"],
            events.Where(value => value is "knowledge" or "progress-attempt"));
        Assert.Equal(1, execution.CompletedJobCount);
        Assert.Equal(ScanExecutionPhase.Pass1Completed, execution.Phase);
    }

    [Fact]
    public async Task RunPass1Async_HydrationFailure_RecordsBoundedFailureWithZeroProgressAsync()
    {
        const string rawDiagnostic = "hydrated-source-secret-diagnostic";
        Guid executionId = WorkerAgentTestFixtures.Id(221);
        Guid repositoryId = WorkerAgentTestFixtures.Id(222);
        Guid systemId = WorkerAgentTestFixtures.Id(223);
        Guid scanRunId = WorkerAgentTestFixtures.Id(224);
        var execution = ScanExecution.Start(
            executionId,
            repositoryId,
            systemId,
            scanRunId,
            WorkerAgentTestFixtures.Id(225),
            WorkerAgentTestFixtures.ProfiledRevision,
            DateTimeOffset.UnixEpoch);
        var store = new RecordingExecutionStore(execution, []);
        CodeUnit unit = new(
            scanRunId,
            repositoryId,
            WorkerAgentTestFixtures.RepositoryRelativePath,
            WorkerAgentTestFixtures.FileIdentityHash,
            WorkerAgentTestFixtures.SourceHash,
            12,
            24,
            "CalculateTotal",
            CodeUnitType.Method,
            RangeConfidence.Exact,
            "csharp");
        var committer = new CountingCommitter();
        var runner = new SequentialScanRunner(
            new EmptyHandoffSource(),
            store,
            new SingleCodeUnitSource(unit),
            new ThrowingDispatcher(new InvalidDataException(rawDiagnostic)),
            committer,
            new FixedTimeProvider());

        await runner.RunPass1Async(
            executionId,
            new RepositoryAccessScope([repositoryId]),
            CancellationToken.None);

        Assert.Equal(ScanExecutionPhase.Failed, execution.Phase);
        Assert.Equal(0, execution.CompletedJobCount);
        Assert.Equal(0, committer.CallCount);
        Assert.DoesNotContain(rawDiagnostic, execution.TerminalMessage, StringComparison.Ordinal);
        Assert.Equal(ScanExecutionFailureKind.Error, execution.TerminalReasonKind);
    }

    private sealed class EmptyHandoffSource : IApprovedPreScanHandoffSource
    {
        public Task<ApprovedPreScanHandoff?> GetAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default) => Task.FromResult<ApprovedPreScanHandoff?>(null);
    }

    private sealed class RecordingExecutionStore(
        ScanExecution execution,
        List<string> events) : IScanExecutionStore
    {
        public Task<ScanExecution?> GetAsync(
            Guid scanExecutionId,
            CancellationToken cancellationToken = default) => Task.FromResult<ScanExecution?>(execution);

        public Task<ScanExecution?> FindActiveAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default) => Task.FromResult<ScanExecution?>(null);

        public Task PersistAsync(
            ScanExecution value,
            CancellationToken cancellationToken = default)
        {
            if (value.CompletedJobCount > 0 && !events.Contains("progress"))
            {
                events.Add("progress");
            }

            if (value.Phase == ScanExecutionPhase.Completed && !events.Contains("completed"))
            {
                events.Add("completed");
            }

            return Task.CompletedTask;
        }

        public Task<ScanExecutionStateView> ReadStateAsync(
            Guid scanExecutionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SingleCodeUnitSource(CodeUnit unit) : ICodeUnitSource
    {
        public Task<IReadOnlyList<CodeUnit>> GetPreparedAsync(
            Guid scanRunId,
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CodeUnit>>([unit]);
    }

    private sealed class EmptyCodeUnitSource : ICodeUnitSource
    {
        public Task<IReadOnlyList<CodeUnit>> GetPreparedAsync(
            Guid scanRunId,
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CodeUnit>>([]);
    }

    private sealed class FailFirstProgressStore(
        ScanExecution execution,
        List<string> events) : IScanExecutionStore
    {
        private bool _failed;

        public Task<ScanExecution?> GetAsync(
            Guid scanExecutionId,
            CancellationToken cancellationToken = default) => Task.FromResult<ScanExecution?>(execution);

        public Task<ScanExecution?> FindActiveAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default) => Task.FromResult<ScanExecution?>(null);

        public Task PersistAsync(
            ScanExecution value,
            CancellationToken cancellationToken = default)
        {
            if (value.CompletedJobCount > 0 && value.Phase == ScanExecutionPhase.Pass1Running)
            {
                events.Add("progress-attempt");
                if (!_failed)
                {
                    _failed = true;
                    throw new InvalidOperationException("Injected progress persistence failure.");
                }
            }

            return Task.CompletedTask;
        }

        public Task<ScanExecutionStateView> ReadStateAsync(
            Guid scanExecutionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ValidDispatcher : IWorkerAgentDispatcher
    {
        internal List<WorkerAgentJobPacket> Packets { get; } = [];

        public Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet,
            CancellationToken cancellationToken = default)
        {
            Packets.Add(packet);
            var pass1 = Assert.IsType<Pass1CodeLogicJobPayload>(packet.Payload);
            Guid anchorId = WorkerAgentTestFixtures.Id(206);
            CanonicalDocumentContent content = new(
                KnowledgeDocumentLimits.SchemaIdentity,
                DocumentType.CodeLogic,
                "orders.runner",
                "订单运行器",
                pass1.RepositoryRelativePath,
                "记录顺序提交。",
                ["CalculateTotal"],
                ["订单"],
                ["Runner"],
                [
                    new CanonicalSection(
                        WorkerAgentTestFixtures.Id(207),
                        1,
                        DocumentSectionKind.TechnicalDetail,
                        "顺序",
                        "知识提交先于进度。",
                        "知识 提交 进度",
                        [anchorId],
                        []),
                ]);
            Pass1CodeLogicDocumentResultPayload payload =
                Pass1CodeLogicDocumentResultPayload.Create(
                    packet.JobId,
                    packet.SystemId,
                    packet.RepositoryId,
                    packet.ScanRunId,
                    packet.CodeUnitId,
                    packet.ProfiledRevision,
                    pass1.RepositoryRelativePath,
                    pass1.LineStart,
                    pass1.LineEnd,
                    pass1.SourceHash,
                    pass1.FileIdentityHash,
                    content,
                    [
                        WorkerAgentSourceAnchor.Create(
                            anchorId,
                            pass1.RepositoryRelativePath,
                            "OrderService.CalculateTotal",
                            pass1.LineStart,
                            pass1.LineEnd,
                            pass1.SourceHash),
                    ],
                    [],
                    []);
            DateTimeOffset dispatchedAt = DateTimeOffset.UnixEpoch.AddSeconds(1);
            return Task.FromResult(WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                dispatchedAt,
                dispatchedAt.AddSeconds(1),
                payload));
        }
    }

    private sealed class ValidPass2Dispatcher(List<string> events) : IWorkerAgentDispatcher
    {
        internal List<WorkerAgentJobPacket> Packets { get; } = [];

        public Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet,
            CancellationToken cancellationToken = default)
        {
            events.Add("pass2-dispatch");
            Packets.Add(packet);
            var pass2 = Assert.IsType<Pass2BusinessLogicJobPayload>(packet.Payload);
            WorkerAgentCodeLogicDocumentVersionProjection source = Assert.Single(
                pass2.CodeLogicDocumentVersions);
            WorkerAgentSourceAnchor anchor = Assert.Single(source.SourceAnchors);
            CanonicalDocumentContent content = new(
                KnowledgeDocumentLimits.SchemaIdentity,
                DocumentType.BusinessLogic,
                "orders.prepared-flow",
                "订单准备流程",
                "flows/orders-prepared-flow",
                "订单完成处理。",
                [],
                ["订单"],
                [],
                [
                    new CanonicalSection(
                        WorkerAgentTestFixtures.Id(390),
                        1,
                        DocumentSectionKind.ProcessStep,
                        "主要步骤",
                        "处理订单并返回结果。",
                        "订单 处理 结果",
                        [anchor.Id],
                        [source.DocumentVersionId]),
                ]);
            Pass2BusinessLogicDocumentResultPayload result =
                Pass2BusinessLogicDocumentResultPayload.Create(
                    packet.JobId,
                    packet.SystemId,
                    packet.RepositoryId,
                    packet.ScanRunId,
                    packet.ProfiledRevision,
                    pass2.FlowId,
                    "帮助业务人员理解订单处理过程。",
                    [WorkerAgentBusinessMainStep.Create(1, "处理订单。", [1])],
                    "订单处理完成。",
                    content,
                    [anchor],
                    [1],
                    [source.DocumentVersionId]);
            DateTimeOffset dispatchedAt = DateTimeOffset.UnixEpoch.AddSeconds(1);
            return Task.FromResult(WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                dispatchedAt,
                dispatchedAt.AddSeconds(1),
                result));
        }
    }

    private sealed class CancelOncePass2Dispatcher(
        CancellationTokenSource cancellation,
        ValidPass2Dispatcher inner) : IWorkerAgentDispatcher
    {
        internal List<Guid> JobIds { get; } = [];

        public Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet,
            CancellationToken cancellationToken = default)
        {
            JobIds.Add(packet.JobId);
            if (JobIds.Count == 1)
            {
                cancellation.Cancel();
                return Task.FromCanceled<WorkerAgentResultEnvelope>(cancellation.Token);
            }

            return inner.DispatchAsync(packet, cancellationToken);
        }
    }

    private sealed class GatedCommitter(List<string> events) : IWorkerAgentResultCommitter
    {
        private readonly TaskCompletionSource _called = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<WorkerAgentResultCommitOutcome> _released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<WorkerAgentResultCommitOutcome> CommitAsync(
            WorkerAgentJobPacket packet,
            WorkerAgentResultEnvelope validResult,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            events.Add("knowledge");
            _called.TrySetResult();
            return _released.Task;
        }

        internal Task WaitUntilCalledAsync() => _called.Task;

        internal void Release(WorkerAgentResultCommitOutcome outcome) =>
            _released.TrySetResult(outcome);
    }

    private sealed class GatedPass2Committer(List<string> events) : IWorkerAgentResultCommitter
    {
        private readonly TaskCompletionSource _called = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<WorkerAgentResultCommitOutcome> _released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public Task<WorkerAgentResultCommitOutcome> CommitAsync(
            WorkerAgentJobPacket packet,
            WorkerAgentResultEnvelope validResult,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            events.Add("pass2-commit");
            CallCount++;
            _called.TrySetResult();
            return _released.Task;
        }

        internal Task WaitUntilCalledAsync() => _called.Task;

        internal void Release(WorkerAgentResultCommitOutcome outcome) =>
            _released.TrySetResult(outcome);
    }

    private sealed class FixedOutcomeCommitter(
        WorkerAgentResultCommitOutcome outcome) : IWorkerAgentResultCommitter
    {
        public int CallCount { get; private set; }

        public Task<WorkerAgentResultCommitOutcome> CommitAsync(
            WorkerAgentJobPacket packet,
            WorkerAgentResultEnvelope validResult,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(outcome);
        }
    }

    private sealed class NonValidPass2Dispatcher(
        WorkerAgentResultOutcome outcome) : IWorkerAgentDispatcher
    {
        public Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet,
            CancellationToken cancellationToken = default)
        {
            DateTimeOffset dispatchedAt = DateTimeOffset.UnixEpoch.AddSeconds(1);
            return Task.FromResult(WorkerAgentResultEnvelope.CreateForJob(
                packet,
                outcome,
                "准备业务流程未生成。",
                dispatchedAt,
                dispatchedAt.AddSeconds(1)));
        }
    }

    private sealed class RecordingCommitter(List<string> events) : IWorkerAgentResultCommitter
    {
        internal List<(WorkerAgentJobPacket Packet, WorkerAgentResultEnvelope Result)> Calls { get; } = [];

        public Task<WorkerAgentResultCommitOutcome> CommitAsync(
            WorkerAgentJobPacket packet,
            WorkerAgentResultEnvelope validResult,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            events.Add("knowledge");
            Calls.Add((packet, validResult));
            return Task.FromResult(WorkerAgentResultCommitOutcome.Committed);
        }
    }

    private sealed class ThrowingDispatcher(Exception exception) : IWorkerAgentDispatcher
    {
        public Task<WorkerAgentResultEnvelope> DispatchAsync(
            WorkerAgentJobPacket packet,
            CancellationToken cancellationToken = default) => Task.FromException<WorkerAgentResultEnvelope>(exception);
    }

    private sealed class CountingCommitter : IWorkerAgentResultCommitter
    {
        public int CallCount { get; private set; }

        public Task<WorkerAgentResultCommitOutcome> CommitAsync(
            WorkerAgentJobPacket packet,
            WorkerAgentResultEnvelope validResult,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(WorkerAgentResultCommitOutcome.Committed);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddSeconds(10);
    }

    private static ScanExecution CreatePass1CompletedExecution(
        Guid executionId,
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId)
    {
        ScanExecution execution = ScanExecution.Start(
            executionId,
            repositoryId,
            systemId,
            scanRunId,
            WorkerAgentTestFixtures.Id(305),
            WorkerAgentTestFixtures.ProfiledRevision,
            DateTimeOffset.UnixEpoch);
        execution.Plan(1, DateTimeOffset.UnixEpoch);
        Guid pass1JobId = WorkerAgentTestFixtures.Id(306);
        execution.Advance(pass1JobId);
        execution.RecordValid(pass1JobId);
        execution.CompletePass1();
        return execution;
    }

    private static ResolvedPreparedPass2Flow CreateResolvedPass2Flow(
        Guid repositoryId,
        Guid systemId,
        Guid scanRunId)
    {
        Guid codeUnitId = WorkerAgentTestFixtures.Id(307);
        Guid logicalDocumentId = WorkerAgentTestFixtures.Id(308);
        Guid documentVersionId = WorkerAgentTestFixtures.Id(309);
        Guid anchorId = WorkerAgentTestFixtures.Id(310);
        PreparedPass2Flow flow = PreparedPass2Flow.Create(
            PreparedPass2FlowLimits.SupportedSchemaIdentity,
            WorkerAgentTestFixtures.Id(311),
            WorkerAgentTestFixtures.Id(312),
            systemId,
            repositoryId,
            [new(1, WorkerAgentTestFixtures.Id(306), codeUnitId, logicalDocumentId)],
            [new(1, WorkerAgentTestFixtures.Id(306), codeUnitId, "订单处理", "src/Orders/OrderService.cs#L12-L24")]);
        CurrentKnowledge current = new(
            logicalDocumentId,
            documentVersionId,
            repositoryId,
            DocumentType.CodeLogic,
            "订单处理",
            WorkerAgentTestFixtures.RepositoryRelativePath,
            "订单处理\n\n## 证据\n\n处理订单。",
            "订单处理\n\n## 证据\n\n处理订单。",
            [
                new CurrentSourceAnchor(
                    anchorId,
                    codeUnitId,
                    WorkerAgentTestFixtures.RepositoryRelativePath,
                    "OrderService.CalculateTotal",
                    12,
                    24,
                    WorkerAgentTestFixtures.SourceHash),
            ],
            [],
            [],
            new CurrentIndexEntry("订单处理。", [], ["订单"], [], "订单 处理"),
            [
                new CurrentDocumentSection(
                    WorkerAgentTestFixtures.Id(313),
                    1,
                    DocumentSectionKind.Overview,
                    "概览",
                    "处理订单。\n返回结果。",
                    "订单 处理 结果",
                    [anchorId],
                    [],
                    false),
            ]);
        return new(flow, [current]);
    }

    private static async Task<(Repository Repository, ScanRun ScanRun)>
        SeedG7PreparedRepositoryAsync(
            VulgataDbContext context,
            PreparedPass2Flow flow,
            G7TemporaryGitRepository git)
    {
        var system = new SystemEntity(
            "G7 System",
            null,
            null,
            DateTimeOffset.UnixEpoch);
        SetG7Id(system, flow.SystemId);
        Repository repository = system.AddRepository(
            "G7 Repository",
            git.RootPath,
            null,
            null,
            DateTimeOffset.UnixEpoch);
        SetG7Id(repository, flow.RepositoryId);
        ScanRun scanRun = ScanRun.Start(repository.Id, 1_800, DateTimeOffset.UnixEpoch);
        SetG7Id(scanRun, WorkerAgentTestFixtures.Id(5));
        scanRun.BindProfiledRevision(git.ProfiledRevision);
        scanRun.SetCandidateCount(flow.Members.Count);

        foreach (PreparedPass2FlowMember member in flow.Members)
        {
            G7CommittedSource source = git.Sources[member.Order - 1];
            var codeUnit = new CodeUnit(
                scanRun.Id,
                repository.Id,
                source.RepositoryRelativePath,
                source.FileIdentityHash,
                source.SourceHash,
                source.LineStart,
                source.LineEnd,
                source.SymbolName,
                CodeUnitType.Method,
                RangeConfidence.Exact,
                "csharp");
            SetG7Id(codeUnit, member.CodeUnitId);
            scanRun.CodeUnits.Add(codeUnit);
        }

        scanRun.Complete(
            flow.Members.Count,
            flow.Members.Count,
            DateTimeOffset.UnixEpoch.AddSeconds(1));
        scanRun.RecordApprovedPreScanOutcome(
            "预扫描已批准——继续进入顺序演示运行器");
        context.Systems.Add(system);
        context.ScanRuns.Add(scanRun);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return (repository, scanRun);
    }

    private static void SetG7Id<T>(T entity, Guid id)
        where T : class
    {
        PropertyInfo property = typeof(T).GetProperty(
            "Id",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property.");
        property.SetValue(entity, id);
    }

    private static async Task<string[]> ReadG7DurableTextAsync(VulgataDbContext context)
    {
        string[] versions = await context.DocumentVersions
            .AsNoTracking()
            .Select(value => value.Title + "\n" + value.ContentJson + "\n" + value.ContentMarkdown)
            .ToArrayAsync();
        string[] anchors = await context.SourceAnchors
            .AsNoTracking()
            .Select(value => value.RepositoryRelativePath + "\n" + value.EvidenceLocator)
            .ToArrayAsync();
        string[] sections = await context.DocumentSections
            .AsNoTracking()
            .Select(value => value.Heading + "\n" + value.ContentMarkdown + "\n" + value.SearchText)
            .ToArrayAsync();
        return [.. versions, .. anchors, .. sections];
    }

    private sealed class G7HandoffSource(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision) : IApprovedPreScanHandoffSource
    {
        public Task<ApprovedPreScanHandoff?> GetAsync(
            Guid requestedRepositoryId,
            Guid requestedScanRunId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ApprovedPreScanHandoff?>(
                requestedRepositoryId == repositoryId && requestedScanRunId == scanRunId
                    ? ApprovedPreScanHandoff.Create(
                        scanRunId,
                        repositoryId,
                        scanRunId,
                        profiledRevision,
                        PreScanApprovalState.PreScanApproved,
                        PreScanResult.CurrentSchemaVersion,
                        DateTimeOffset.UnixEpoch.AddSeconds(2),
                        "已批准前置扫描。",
                        ConfirmedCommunicationKinds.None,
                        0,
                        0,
                        0,
                        false,
                        [])
                    : null);
    }

    private sealed class G7ExecutionStore(
        ScanExecutionRepository inner,
        VulgataDbContext context,
        List<string> events) : IScanExecutionStore
    {
        private int _observedProgress;
        private bool _observedPass1Completed;
        private bool _observedCompleted;

        internal bool CommitWasVisibleBeforeEveryProgress { get; private set; } = true;
        internal bool BusinessCommitWasVisibleBeforeCompleted { get; private set; }

        public Task<ScanExecution?> GetAsync(
            Guid scanExecutionId,
            CancellationToken cancellationToken = default) =>
            inner.GetAsync(scanExecutionId, cancellationToken);

        public Task<ScanExecution?> FindActiveAsync(
            Guid repositoryId,
            Guid scanRunId,
            CancellationToken cancellationToken = default) =>
            inner.FindActiveAsync(repositoryId, scanRunId, cancellationToken);

        public async Task PersistAsync(
            ScanExecution execution,
            CancellationToken cancellationToken = default)
        {
            await inner.PersistAsync(execution, cancellationToken);
            ScanExecution durable = Assert.IsType<ScanExecution>(
                await inner.GetAsync(execution.Id, cancellationToken));
            if (durable.CompletedJobCount > _observedProgress)
            {
                int documentCount = await context.LogicalDocuments
                    .AsNoTracking()
                    .CountAsync(
                        document => document.DocumentType == DocumentType.CodeLogic,
                        cancellationToken);
                int projectionCount = await context.IndexEntries
                    .AsNoTracking()
                    .CountAsync(cancellationToken);
                CommitWasVisibleBeforeEveryProgress &=
                    documentCount >= durable.CompletedJobCount &&
                    projectionCount >= durable.CompletedJobCount;
                _observedProgress = durable.CompletedJobCount;
                events.Add("pass1-progress");
            }

            if (durable.Phase == ScanExecutionPhase.Pass1Completed &&
                !_observedPass1Completed)
            {
                _observedPass1Completed = true;
                events.Add("pass1-completed");
            }

            if (durable.Phase == ScanExecutionPhase.Completed && !_observedCompleted)
            {
                BusinessCommitWasVisibleBeforeCompleted = await context.LogicalDocuments
                    .AsNoTracking()
                    .AnyAsync(
                        document => document.DocumentType == DocumentType.BusinessLogic,
                        cancellationToken);
                _observedCompleted = true;
                events.Add("completed");
            }
        }

        public Task<ScanExecutionStateView> ReadStateAsync(
            Guid scanExecutionId,
            CancellationToken cancellationToken = default) =>
            inner.ReadStateAsync(scanExecutionId, cancellationToken);
    }

    private sealed class G7RecordingCommitter(
        IWorkerAgentResultCommitter inner,
        List<string> events) : IWorkerAgentResultCommitter
    {
        public async Task<WorkerAgentResultCommitOutcome> CommitAsync(
            WorkerAgentJobPacket packet,
            WorkerAgentResultEnvelope validResult,
            RepositoryAccessScope scope,
            CancellationToken cancellationToken = default)
        {
            WorkerAgentResultCommitOutcome result = await inner.CommitAsync(
                packet,
                validResult,
                scope,
                cancellationToken);
            if (result == WorkerAgentResultCommitOutcome.Committed)
            {
                events.Add(packet.Kind == WorkerAgentJobKind.Pass2BusinessLogicDocument
                    ? "pass2-knowledge"
                    : "pass1-knowledge");
            }

            return result;
        }
    }

    private sealed class G7RepositoryRepository(Repository repository)
        : IRepositoryRepository
    {
        public Task<Repository?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Repository?>(
                repository.SystemId == systemId && repository.Id == repositoryId
                    ? repository
                    : null);

        public Task<IReadOnlyList<Repository>> ListBySystemAsync(Guid systemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Repository>> ListStandaloneAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Repository?> GetByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Repository?> GetStandaloneByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(Guid systemId, string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> StandaloneNameExistsAsync(string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Repository> AddAsync(Repository value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Remove(Repository value) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class G7WorkingCopyService(
        string rootPath,
        string profiledRevision) : IGitWorkingCopyService
    {
        public Task<GitWorkingCopyResult> AcquireAsync(
            Repository repository,
            Guid scanRunId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GitWorkingCopyResult.Success(rootPath, profiledRevision));
    }

    private sealed class G7SnapshotService(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string snapshotPath) : ICommittedRepositorySnapshotService
    {
        public Task<ICommittedRepositorySnapshotLease> AcquireAsync(
            Repository repository,
            Guid requestedScanRunId,
            GitWorkingCopyResult workingCopy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ICommittedRepositorySnapshotLease>(
                new G7SnapshotLease(
                    repositoryId,
                    scanRunId,
                    profiledRevision,
                    snapshotPath));
    }

    private sealed class G7SnapshotLease(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string rootPath) : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId => repositoryId;
        public Guid ScanRunId => scanRunId;
        public string ProfiledRevision => profiledRevision;
        public string RootPath => rootPath;
        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class G7OverrideRepository(SystemLlmProviderOverride assignment)
        : ISystemLlmProviderOverrideRepository
    {
        public Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(
            Guid systemId,
            AgentType agentType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemLlmProviderOverride?>(
                assignment.SystemId == systemId && assignment.AgentType == agentType
                    ? assignment
                    : null);

        public Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(Guid systemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(Guid systemId, Guid overrideId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SystemLlmProviderOverride> AddAsync(SystemLlmProviderOverride overrideEntry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RemoveAsync(Guid systemId, Guid overrideId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class G7ProviderRepository(LlmProvider provider)
        : ILlmProviderRepository
    {
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LlmProvider>>([provider]);
        public Task<LlmProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<LlmProvider?>(provider.Id == id ? provider : null);
        public Task<bool> NameExistsAsync(string name, Guid? excludeProviderId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LlmProvider> AddAsync(LlmProvider value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Remove(LlmProvider value) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class G7EncryptionService(string plaintext) : ISecretEncryptionService
    {
        internal int DecryptCount { get; private set; }

        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) =>
            Task.FromResult($"ciphertext:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain)))}");

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(SecretPurposes.LlmApiKey, purpose);
            Assert.StartsWith("ciphertext:", cipher, StringComparison.Ordinal);
            DecryptCount++;
            return Task.FromResult(plaintext);
        }
    }

    private sealed class G7LoopbackHandler(List<string> events) : HttpMessageHandler
    {
        internal List<G7Pass1Request> Pass1Requests { get; } = [];
        internal List<G7Pass2Request> Pass2Requests { get; } = [];
        internal List<string> RequestBodies { get; } = [];
        internal List<string?> AuthorizationParameters { get; } = [];

        internal static Guid SectionId(Guid codeUnitId) =>
            StableId("g7-section", codeUnitId);

        private static Guid AnchorId(Guid codeUnitId) =>
            StableId("g7-anchor", codeUnitId);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);
            AuthorizationParameters.Add(request.Headers.Authorization?.Parameter);
            using JsonDocument outer = JsonDocument.Parse(body);
            string requestJson = outer.RootElement.GetProperty("messages")
                .EnumerateArray()
                .Single(message => message.GetProperty("role").GetString() == "user")
                .GetProperty("content")
                .GetString()!;
            using JsonDocument input = JsonDocument.Parse(requestJson);
            JsonElement root = input.RootElement;
            string responseJson;
            if (root.GetProperty("kind").GetString() ==
                nameof(WorkerAgentJobKind.Pass1CodeLogicDocument))
            {
                responseJson = CreatePass1Response(root);
            }
            else
            {
                events.Add("pass2-dispatch");
                responseJson = CreatePass2Response(root);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    CreateChatResponse(responseJson),
                    Encoding.UTF8,
                    "application/json"),
            };
        }

        private string CreatePass1Response(JsonElement root)
        {
            JsonElement source = root.GetProperty("source");
            Guid jobId = root.GetProperty("jobId").GetGuid();
            Guid codeUnitId = root.GetProperty("codeUnitId").GetGuid();
            Guid anchorId = AnchorId(codeUnitId);
            Pass1Requests.Add(new(
                jobId,
                codeUnitId,
                source.GetProperty("sourceText").GetString()!));
            return JsonSerializer.Serialize(new
            {
                jobId,
                systemId = root.GetProperty("systemId").GetGuid(),
                repositoryId = root.GetProperty("repositoryId").GetGuid(),
                scanRunId = root.GetProperty("scanRunId").GetGuid(),
                codeUnitId,
                profiledRevision = root.GetProperty("profiledRevision").GetString(),
                repositoryRelativePath = source.GetProperty("repositoryRelativePath").GetString(),
                lineStart = source.GetProperty("lineStart").GetInt32(),
                lineEnd = source.GetProperty("lineEnd").GetInt32(),
                sourceHash = source.GetProperty("sourceHash").GetString(),
                fileIdentityHash = source.GetProperty("fileIdentityHash").GetString(),
                content = new
                {
                    schemaIdentity = KnowledgeDocumentLimits.SchemaIdentity,
                    documentType = nameof(DocumentType.CodeLogic),
                    stableKey = $"g7.code-unit.{codeUnitId:D}",
                    title = $"代码逻辑 {codeUnitId:D}",
                    repositoryRelativePath = source.GetProperty("repositoryRelativePath").GetString(),
                    summary = "说明准备代码单元的确定性处理。",
                    keySymbols = new[] { source.GetProperty("symbolName").GetString() },
                    businessTerms = new[] { "订单" },
                    technicalTerms = new[] { "Worker Agent" },
                    sections = new[]
                    {
                        new
                        {
                            stableSectionId = SectionId(codeUnitId),
                            order = 1,
                            kind = nameof(DocumentSectionKind.TechnicalDetail),
                            heading = "处理逻辑",
                            markdown = "按已提交代码切片生成代码逻辑文档。",
                            searchText = "提交 切片 代码逻辑",
                            sourceAnchorIds = new[] { anchorId },
                            documentVersionIds = Array.Empty<Guid>(),
                        },
                    },
                },
                sourceAnchors = new[]
                {
                    new
                    {
                        id = anchorId,
                        repositoryRelativePath = source.GetProperty("repositoryRelativePath").GetString(),
                        evidenceLocator = source.GetProperty("symbolName").GetString(),
                        lineStart = source.GetProperty("lineStart").GetInt32(),
                        lineEnd = source.GetProperty("lineEnd").GetInt32(),
                        sourceHash = source.GetProperty("sourceHash").GetString(),
                    },
                },
                referenceDocumentVersionIds = Array.Empty<Guid>(),
                communicationEvidenceIds = Array.Empty<Guid>(),
            });
        }

        private string CreatePass2Response(JsonElement root)
        {
            JsonElement[] documents = root.GetProperty("codeLogicDocumentVersions")
                .EnumerateArray()
                .ToArray();
            Guid[] documentVersionIds = documents
                .Select(document => document.GetProperty("documentVersionId").GetGuid())
                .ToArray();
            G7ResponseAnchor[] anchors = documents
                .SelectMany(document => document.GetProperty("sourceAnchors").EnumerateArray())
                .Select(anchor => new G7ResponseAnchor(
                    anchor.GetProperty("id").GetGuid(),
                    anchor.GetProperty("repositoryRelativePath").GetString()!,
                    anchor.GetProperty("evidenceLocator").GetString()!,
                    anchor.GetProperty("lineStart").GetInt32(),
                    anchor.GetProperty("lineEnd").GetInt32(),
                    anchor.GetProperty("sourceHash").GetString()!))
                .OrderBy(anchor => anchor.Id)
                .ToArray();
            Pass2Requests.Add(new(documentVersionIds));
            return JsonSerializer.Serialize(new
            {
                jobId = root.GetProperty("jobId").GetGuid(),
                systemId = root.GetProperty("systemId").GetGuid(),
                repositoryId = root.GetProperty("repositoryId").GetGuid(),
                scanRunId = root.GetProperty("scanRunId").GetGuid(),
                profiledRevision = root.GetProperty("profiledRevision").GetString(),
                flowId = root.GetProperty("flowId").GetGuid(),
                purpose = "帮助业务人员理解订单处理流程。",
                mainSteps = new[]
                {
                    new { order = 1, description = "计算订单总额。", traceEvidenceOrders = new[] { 1 } },
                    new { order = 2, description = "预留对应库存。", traceEvidenceOrders = new[] { 2 } },
                },
                outcome = "订单完成计价并预留库存。",
                content = new
                {
                    schemaIdentity = KnowledgeDocumentLimits.SchemaIdentity,
                    documentType = nameof(DocumentType.BusinessLogic),
                    stableKey = "g7.orders.prepared-flow",
                    title = "订单处理流程",
                    repositoryRelativePath = "flows/orders-prepared-flow",
                    summary = "订单完成计价并预留库存。",
                    keySymbols = Array.Empty<string>(),
                    businessTerms = new[] { "库存", "订单" },
                    technicalTerms = Array.Empty<string>(),
                    sections = new[]
                    {
                        new
                        {
                            stableSectionId = StableId("g7-business-section", root.GetProperty("flowId").GetGuid()),
                            order = 1,
                            kind = nameof(DocumentSectionKind.ProcessStep),
                            heading = "主要步骤",
                            markdown = "先计算订单总额，再预留对应库存。",
                            searchText = "订单 总额 库存 预留",
                            sourceAnchorIds = anchors.Select(anchor => anchor.Id).ToArray(),
                            documentVersionIds = documentVersionIds.Order().ToArray(),
                        },
                    },
                },
                sourceAnchors = anchors.Select(anchor => new
                {
                    id = anchor.Id,
                    repositoryRelativePath = anchor.RepositoryRelativePath,
                    evidenceLocator = anchor.EvidenceLocator,
                    lineStart = anchor.LineStart,
                    lineEnd = anchor.LineEnd,
                    sourceHash = anchor.SourceHash,
                }),
                traceEvidenceOrders = new[] { 1, 2 },
                generatedFromDocumentVersionIds = documentVersionIds,
            });
        }

        private static string CreateChatResponse(string responseJson) =>
            JsonSerializer.Serialize(new
            {
                id = "chatcmpl-g7",
                @object = "chat.completion",
                created = 1_785_542_400,
                model = "g7-loopback-worker",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content = responseJson },
                        finish_reason = "stop",
                    },
                },
                usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 },
            });

        private static Guid StableId(string prefix, Guid value)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{prefix}\n{value:D}"));
            return new Guid(hash.AsSpan(0, 16));
        }
    }

    private sealed record G7Pass1Request(Guid JobId, Guid CodeUnitId, string SourceText);
    private sealed record G7Pass2Request(IReadOnlyList<Guid> DocumentVersionIds);
    private sealed record G7ResponseAnchor(
        Guid Id,
        string RepositoryRelativePath,
        string EvidenceLocator,
        int LineStart,
        int LineEnd,
        string SourceHash);

    private sealed class G7TimeProvider : TimeProvider
    {
        private long _seconds;
        public override DateTimeOffset GetUtcNow() =>
            DateTimeOffset.UnixEpoch.AddSeconds(Interlocked.Increment(ref _seconds));
    }

    private sealed class G7TemporaryGitRepository : IDisposable
    {
        private readonly string _containerPath;

        internal G7TemporaryGitRepository(string unrelatedSource)
        {
            _containerPath = Path.Combine(
                Path.GetTempPath(),
                $"vulgata-g7-{Guid.NewGuid():N}");
            RootPath = Path.Combine(_containerPath, "working");
            SnapshotPath = Path.Combine(_containerPath, "snapshot");
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(SnapshotPath);
            RunGit("init", "--quiet");
            RunGit("config", "core.autocrlf", "false");
            RunGit("config", "user.email", "vulgata-tests@example.invalid");
            RunGit("config", "user.name", "Vulgata Tests");

            Sources =
            [
                WriteSource(
                    "src/Orders/OrderService.cs",
                    12,
                    24,
                    "CalculateTotal",
                    "COMMITTED-ORDER-SLICE",
                    unrelatedSource),
                WriteSource(
                    "src/Inventory/InventoryService.cs",
                    30,
                    44,
                    "ReserveInventory",
                    "COMMITTED-INVENTORY-SLICE",
                    unrelatedSource),
            ];
            RunGit("add", "--all");
            RunGit("commit", "--quiet", "-m", "g7 committed boundary");
            ProfiledRevision = RunGit("rev-parse", "--verify", "HEAD").Trim();
            foreach (G7CommittedSource source in Sources)
            {
                string sourcePath = Path.Combine(
                    RootPath,
                    source.RepositoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string snapshotFile = Path.Combine(
                    SnapshotPath,
                    source.RepositoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(snapshotFile)!);
                File.Copy(sourcePath, snapshotFile);
            }
        }

        internal string RootPath { get; }
        internal string SnapshotPath { get; }
        internal string ProfiledRevision { get; }
        internal string HeadRevision => RunGit("rev-parse", "--verify", "HEAD").Trim();
        internal string Status => RunGit("status", "--porcelain=v1", "--untracked-files=all");
        internal IReadOnlyList<G7CommittedSource> Sources { get; }
        internal IReadOnlyList<string> CommittedSlices =>
            Sources.Select(source => source.SourceText).ToArray();

        internal void MoveHeadAndDirty()
        {
            string inventory = Path.Combine(
                RootPath,
                "src",
                "Inventory",
                "InventoryService.cs");
            File.AppendAllText(inventory, "moved-head-only\n", new UTF8Encoding(false));
            RunGit("add", "--all");
            RunGit("commit", "--quiet", "-m", "move head after profiling");
            string orders = Path.Combine(RootPath, "src", "Orders", "OrderService.cs");
            File.WriteAllText(
                orders,
                "DIRTY-WORKING-COPY-MUST-NOT-CROSS\n",
                new UTF8Encoding(false));
        }

        public void Dispose()
        {
            if (!Directory.Exists(_containerPath))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(
                         _containerPath,
                         "*",
                         SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_containerPath, recursive: true);
        }

        private G7CommittedSource WriteSource(
            string relativePath,
            int lineStart,
            int lineEnd,
            string symbolName,
            string committedMarker,
            string unrelatedSource)
        {
            int lineCount = lineEnd + 5;
            string[] lines = Enumerable.Range(1, lineCount)
                .Select(line => line == 1
                    ? unrelatedSource
                    : line >= lineStart && line <= lineEnd
                        ? $"{committedMarker}-{line}"
                        : $"outside-requested-slice-{line}")
                .ToArray();
            string content = string.Join('\n', lines) + "\n";
            string path = Path.Combine(
                RootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            string sourceText = string.Join('\n', lines[(lineStart - 1)..lineEnd]);
            byte[] fileBytes = File.ReadAllBytes(path);
            return new(
                relativePath,
                lineStart,
                lineEnd,
                symbolName,
                sourceText,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceText))).ToLowerInvariant(),
                Convert.ToHexString(SHA256.HashData(fileBytes.AsSpan(0, Math.Min(4_096, fileBytes.Length)))).ToLowerInvariant());
        }

        private string RunGit(params string[] arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("git")
                {
                    WorkingDirectory = RootPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            foreach (string argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                $"git {string.Join(' ', arguments)} failed: {error}");
            return output;
        }
    }

    private sealed record G7CommittedSource(
        string RepositoryRelativePath,
        int LineStart,
        int LineEnd,
        string SymbolName,
        string SourceText,
        string SourceHash,
        string FileIdentityHash);
}

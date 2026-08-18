using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.DurableScanning;

/// <summary>
/// T022 RED: the durable result committer must make the immutable knowledge
/// graph and the matching Scan Job Execution completion one transaction.
/// These tests deliberately cross the accepted <see cref="IScanJobResultCommitter"/>
/// seam; no test-local store is allowed to make the atomicity claim true.
/// </summary>
public sealed class AtomicDocumentJobCommitTests
{
    private static readonly DateTimeOffset _at =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);
    private const string _revision = "0123456789abcdef0123456789abcdef01234567";
    private const string _runtimeHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string _sourceHash =
        "e3b0c44298fc1c149afbf4f8996fb92427ae41e4649b934ca495991b7852b855";
    private const string _fileIdentityHash =
        "a3f15bb08b2bd1684e84c4b2e2d673282c0e31f27a6a2e56aeb894ecff0c10f4";

    [Fact]
    public async Task CommitAsync_RunningJob_CreatesExactlyOneImmutableDocumentGraphAndCompletesTheMatchingJob()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        ScanJobResultCommitRequest request = fixture.CreateRequest();

        ScanJobResultCommitResult result = await fixture.Committer.CommitAsync(
            request,
            CancellationToken.None);

        Assert.Equal(ScanJobResultCommitOutcome.Committed, result.Outcome);
        Assert.Equal(request.DocumentVersionId, result.CommittedDocumentVersionId);
        Assert.False(result.IsReplay);
        Assert.Equal(1, await fixture.Context.Set<LogicalDocument>().CountAsync());
        Assert.Equal(1, await fixture.Context.Set<DocumentVersion>().CountAsync());
        Assert.Equal(1, await fixture.Context.Set<SourceAnchor>().CountAsync());
        Assert.Equal(1, await fixture.Context.Set<DocumentSection>().CountAsync());
        Assert.Equal(1, await fixture.Context.Set<IndexEntry>().CountAsync());
        Assert.Equal(0, await fixture.Context.Set<DocumentEdge>().CountAsync());

        ScanJobExecution job = await fixture.Context.Set<ScanJobExecution>()
            .SingleAsync();
        Assert.Equal(ScanJobExecutionState.Completed, job.State);
        Assert.Equal(request.DocumentVersionId, job.CommittedDocumentVersionId);
        Assert.Equal(2, job.StateVersion);
        Assert.Null(job.LeaseToken);
    }

    [Fact]
    public async Task CommitAsync_NewCommit_RequiresExactExpectedVersionAndLeaseToken()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        ScanJobResultCommitRequest request = fixture.CreateRequest();

        ScanJobResultCommitResult staleVersion = await fixture.Committer.CommitAsync(
            request with { ExpectedStateVersion = request.ExpectedStateVersion - 1 },
            CancellationToken.None);
        ScanJobResultCommitResult wrongLease = await fixture.Committer.CommitAsync(
            request with { LeaseToken = DeterministicTestIds.Create("wrong-lease") },
            CancellationToken.None);

        Assert.Equal(ScanJobResultCommitOutcome.Conflict, staleVersion.Outcome);
        Assert.Equal(ScanJobResultCommitOutcome.Conflict, wrongLease.Outcome);
        Assert.Equal(0, await fixture.Context.Set<DocumentVersion>().CountAsync());
        Assert.Equal(ScanJobExecutionState.Running,
            (await fixture.Context.Set<ScanJobExecution>().SingleAsync()).State);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CommitAsync_InjectedFailureBeforeAfterMaterializationOrBeforeCompletion_RollsBackBothSides(
        int saveChangesOrdinal)
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync(
            new ThrowingSaveChangesInterceptor(saveChangesOrdinal));

        ScanJobResultCommitResult result = await fixture.Committer.CommitAsync(
            fixture.CreateRequest(),
            CancellationToken.None);

        Assert.Equal(ScanJobResultCommitOutcome.Failed, result.Outcome);
        Assert.Equal(0, await fixture.Context.Set<LogicalDocument>().CountAsync());
        Assert.Equal(0, await fixture.Context.Set<DocumentVersion>().CountAsync());
        Assert.Equal(0, await fixture.Context.Set<SourceAnchor>().CountAsync());
        Assert.Equal(0, await fixture.Context.Set<DocumentSection>().CountAsync());
        Assert.Equal(0, await fixture.Context.Set<IndexEntry>().CountAsync());
        ScanJobExecution job = await fixture.Context.Set<ScanJobExecution>()
            .SingleAsync();
        Assert.Equal(ScanJobExecutionState.Running, job.State);
        Assert.Null(job.CommittedDocumentVersionId);
        Assert.Equal(1, job.StateVersion);
        Assert.Equal(fixture.LeaseToken, job.LeaseToken);
    }

    [Fact]
    public async Task CommitAsync_LostResponseReplay_ExactCompletedIdentityAndContentReturnsMutationFreeCommittedDespiteStaleLease()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        ScanJobResultCommitRequest request = fixture.CreateRequest();
        ScanJobResultCommitResult first = await fixture.Committer.CommitAsync(
            request,
            CancellationToken.None);
        long completedVersion = (await fixture.Context.Set<ScanJobExecution>()
            .SingleAsync()).StateVersion;

        ScanJobResultCommitResult replay = await fixture.Committer.CommitAsync(
            request,
            CancellationToken.None);

        Assert.Equal(ScanJobResultCommitOutcome.Committed, first.Outcome);
        Assert.Equal(ScanJobResultCommitOutcome.Committed, replay.Outcome);
        Assert.True(replay.IsReplay);
        Assert.Equal(completedVersion,
            (await fixture.Context.Set<ScanJobExecution>().SingleAsync()).StateVersion);
        Assert.Equal(1, await fixture.Context.Set<DocumentVersion>().CountAsync());
    }

    [Theory]
    [MemberData(nameof(NonIdenticalCompletedReplays))]
    public async Task CommitAsync_CompletedReplay_WithAnyNonIdenticalCanonicalIdentityFingerprintOrContentConflictsWithoutMutation(
        Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest> change)
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        ScanJobResultCommitRequest request = fixture.CreateRequest();
        await fixture.Committer.CommitAsync(request, CancellationToken.None);
        ScanJobExecution before = await fixture.Context.Set<ScanJobExecution>()
            .AsNoTracking().SingleAsync();

        ScanJobResultCommitResult replay = await fixture.Committer.CommitAsync(
            change(request),
            CancellationToken.None);

        Assert.Equal(ScanJobResultCommitOutcome.Conflict, replay.Outcome);
        ScanJobExecution after = await fixture.Context.Set<ScanJobExecution>()
            .AsNoTracking().SingleAsync();
        Assert.Equal(before.StateVersion, after.StateVersion);
        Assert.Equal(before.CommittedDocumentVersionId, after.CommittedDocumentVersionId);
        Assert.Equal(1, await fixture.Context.Set<DocumentVersion>().CountAsync());
    }

    public static IEnumerable<object[]> NonIdenticalCompletedReplays() =>
    [
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { ScanExecutionId = DeterministicTestIds.Create("other-execution") })],
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { RepositoryId = DeterministicTestIds.Create("other-repository") })],
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { ScanRunId = DeterministicTestIds.Create("other-run") })],
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { FileCodeUnitId = DeterministicTestIds.Create("other-file") })],
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { ProfiledRevision = new string('b', 40) })],
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { PromptIdentity = "prompt/changed" })],
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { DocumentSchemaIdentity = "document-schema/changed" })],
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { AnalysisRuntimeIdentityHash = new string('b', 64) })],
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { DocumentVersionId = DeterministicTestIds.Create("other-version") })],
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { CommitFingerprint = new string('b', 64) })],
        [new Func<ScanJobResultCommitRequest, ScanJobResultCommitRequest>(request =>
            request with { WorkerResult = ChangeContent(request.WorkerResult) })],
    ];

    [Fact]
    public async Task CommitAsync_CancelledBeforeCommit_RollsBackWithoutCompletingTheJob()
    {
        await using CommitFixture fixture = await CommitFixture.CreateAsync();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        ScanJobResultCommitResult result = await fixture.Committer.CommitAsync(
            fixture.CreateRequest(),
            cancellation.Token);

        Assert.Equal(ScanJobResultCommitOutcome.Cancelled, result.Outcome);
        Assert.Equal(0, await fixture.Context.Set<DocumentVersion>().CountAsync());
        ScanJobExecution job = await fixture.Context.Set<ScanJobExecution>().SingleAsync();
        Assert.Equal(ScanJobExecutionState.Running, job.State);
        Assert.Null(job.CommittedDocumentVersionId);
        Assert.Equal(fixture.LeaseToken, job.LeaseToken);
    }

    private static async Task<IScanJobResultCommitter> CreateCommitterAsync(
        VulgataDbContext context)
    {
        var knowledgeStore = new KnowledgeStoreRepository(
            context,
            new KnowledgeDocumentMaterializer());
        // T022 RED: the existing knowledge committer is not yet the durable
        // job/document committer. T023 must make this accepted seam real.
        WorkerAgentResultCommitter legacy = new(knowledgeStore);
        return Assert.IsAssignableFrom<IScanJobResultCommitter>(legacy);
    }

    private static WorkerAgentResultEnvelope ChangeContent(
        WorkerAgentResultEnvelope original)
    {
        Pass1CodeLogicDocumentResultPayload payload =
            Assert.IsType<Pass1CodeLogicDocumentResultPayload>(original.Payload);
        CanonicalDocumentContent changedContent = payload.Content with
        {
            Title = "T022 changed document content",
        };
        Pass1CodeLogicDocumentResultPayload changedPayload =
            Pass1CodeLogicDocumentResultPayload.Create(
                payload.JobId,
                payload.SystemId,
                payload.RepositoryId,
                payload.ScanRunId,
                payload.CodeUnitId,
                payload.ProfiledRevision,
                payload.RepositoryRelativePath,
                payload.LineStart,
                payload.LineEnd,
                payload.SourceHash,
                payload.FileIdentityHash,
                changedContent,
                payload.SourceAnchors,
                payload.ReferenceDocumentVersionIds,
                payload.CommunicationEvidenceIds);
        return WorkerAgentResultEnvelope.Create(
            original.JobId,
            original.ScanExecutionId,
            original.Kind,
            original.Outcome,
            original.BoundedReason,
            original.DispatchedAt,
            original.ReceivedAt,
            changedPayload);
    }

    private sealed class CommitFixture : IAsyncDisposable
    {
        private CommitFixture(
            SqliteConnection connection,
            VulgataDbContext context,
            SystemEntity system,
            Repository repository,
            ScanRun scanRun,
            CodeUnit codeUnit,
            ScanExecution execution,
            ScanJobExecution job,
            Guid leaseToken,
            IScanJobResultCommitter committer)
        {
            Connection = connection;
            Context = context;
            System = system;
            Repository = repository;
            ScanRun = scanRun;
            CodeUnit = codeUnit;
            Execution = execution;
            Job = job;
            LeaseToken = leaseToken;
            Committer = committer;
        }

        private SqliteConnection Connection { get; }
        internal VulgataDbContext Context { get; }
        private SystemEntity System { get; }
        private Repository Repository { get; }
        private ScanRun ScanRun { get; }
        private CodeUnit CodeUnit { get; }
        private ScanExecution Execution { get; }
        private ScanJobExecution Job { get; }
        internal Guid LeaseToken { get; }
        internal IScanJobResultCommitter Committer { get; }

        internal static async Task<CommitFixture> CreateAsync(
            ThrowingSaveChangesInterceptor? interceptor = null)
        {
            SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<VulgataDbContext>()
                .UseSqlite(connection);
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }

            VulgataDbContext context = new(options.Options);
            await context.Database.EnsureCreatedAsync();
            DateTimeOffset started = _at;
            var system = new SystemEntity("T022 system", null, null, started);
            Repository repository = system.AddRepository(
                "T022 repository", "https://example.invalid/t022.git", null, null, started);
            ScanRun scanRun = ScanRun.Start(repository.Id, 1_800, started);
            scanRun.BindProfiledRevision(_revision);
            var codeUnit = new CodeUnit(
                scanRun.Id,
                repository.Id,
                "src/T022.cs",
                _fileIdentityHash,
                _sourceHash,
                1,
                10,
                "T022",
                CodeUnitType.File,
                RangeConfidence.Exact,
                "csharp");
            Guid executionId = DeterministicTestIds.Create("t022-execution");
            ScanExecution execution = ScanExecution.CreateNewPath(
                executionId,
                repository.Id,
                system.Id,
                scanRun.Id,
                _revision,
                "prompt/1.0",
                "document-schema/1.0",
                "{\"codeGraph\":\"1.5.0\"}",
                _runtimeHash,
                started.AddMinutes(2));
            context.Systems.Add(system);
            context.ScanRuns.Add(scanRun);
            context.CodeUnits.Add(codeUnit);
            await context.SaveChangesAsync();

            var scope = new DurableScanAuthorizationScope("t022-actor", [system.Id]);
            var durableStore = new DurableScanRepository(context);
            ScanContextSnapshot snapshot = ScanContextSnapshot.Capture(
                scanRun, "global", "system", "repository", started.AddMinutes(1));
            await durableStore.CreateContextSnapshotAsync(scope, snapshot, CancellationToken.None);
            await durableStore.CreateScanExecutionAsync(scope, execution, CancellationToken.None);
            ScanJobExecution job = ScanJobExecution.Create(
                execution.Id, repository.Id, scanRun.Id, codeUnit.Id, 1, started.AddMinutes(3));
            await durableStore.InsertJobExecutionsAsync(
                scope, execution.Id, [job], CancellationToken.None);
            Guid lease = DeterministicTestIds.Create("t022-lease");
            await durableStore.ClaimPendingJobAsync(
                scope,
                new ScanJobLeaseCommand(
                    execution.Id,
                    job.Id,
                    0,
                    lease,
                    started.AddMinutes(10),
                    started.AddMinutes(4)),
                CancellationToken.None);

            // Seed writes are complete before fault injection is armed.
            if (interceptor is not null)
            {
                interceptor.Arm();
            }

            return new(
                connection,
                context,
                system,
                repository,
                scanRun,
                codeUnit,
                execution,
                job,
                lease,
                await CreateCommitterAsync(context));
        }

        internal ScanJobResultCommitRequest CreateRequest()
        {
            Guid anchorId = DeterministicTestIds.Create("t022-anchor", Job.Id);
            Guid sectionId = DeterministicTestIds.Create("t022-section", Job.Id);
            WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
                Job.Id,
                WorkerAgentJobKind.Pass1CodeLogicDocument,
                Execution.Id,
                Repository.Id,
                System.Id,
                ScanRun.Id,
                _revision,
                WorkerAgentContractLimits.SupportedSchemaVersion,
                Pass1CodeLogicJobPayload.Create(
                    CodeUnit.Id,
                    CodeUnit.FilePath,
                    CodeUnit.LineStart,
                    CodeUnit.LineEnd,
                    CodeUnit.SymbolName,
                    CodeUnit.UnitType,
                    CodeUnit.Language,
                    CodeUnit.SourceHash,
                    CodeUnit.FileIdentityHash,
                    [], [], [], []));
            CanonicalDocumentContent content = new(
                KnowledgeDocumentLimits.SchemaIdentity,
                DocumentType.CodeLogic,
                "t022.document",
                "T022 document",
                CodeUnit.FilePath,
                "Atomic durable scan result.",
                [], [], [],
                [new CanonicalSection(
                    sectionId,
                    1,
                    DocumentSectionKind.Overview,
                    "Overview",
                    "Atomic durable scan result.",
                    "atomic durable scan result",
                    [anchorId],
                    [])]);
            Pass1CodeLogicDocumentResultPayload payload =
                Pass1CodeLogicDocumentResultPayload.Create(
                    packet.JobId,
                    packet.SystemId,
                    packet.RepositoryId,
                    packet.ScanRunId,
                    packet.CodeUnitId,
                    packet.ProfiledRevision,
                CodeUnit.FilePath,
                    CodeUnit.LineStart,
                    CodeUnit.LineEnd,
                    CodeUnit.SourceHash,
                    CodeUnit.FileIdentityHash,
                    content,
                    [WorkerAgentSourceAnchor.Create(
                        anchorId,
                        CodeUnit.FilePath,
                        "T022",
                        CodeUnit.LineStart,
                        CodeUnit.LineEnd,
                        CodeUnit.SourceHash)],
                    [],
                    []);
            WorkerAgentResultEnvelope envelope = WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                _at.AddMinutes(4),
                _at.AddMinutes(4).AddSeconds(1),
                payload);
            return new(
                new DurableScanAuthorizationScope("t022-actor", [System.Id]),
                Repository.Id,
                Execution.Id,
                Job.Id,
                CodeUnit.Id,
                ScanRun.Id,
                _revision,
                "prompt/1.0",
                "document-schema/1.0",
                _runtimeHash,
                DeterministicTestIds.Create("t022-document-version", Job.Id),
                new string('f', 64),
                1,
                LeaseToken,
                envelope);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class ThrowingSaveChangesInterceptor(int failureOrdinal) : SaveChangesInterceptor
    {
        private int _saveOrdinal;
        private bool _armed;

        internal void Arm() => _armed = true;

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ThrowWhenSelected();
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowWhenSelected();
            return ValueTask.FromResult(result);
        }

        private void ThrowWhenSelected()
        {
            if (_armed && ++_saveOrdinal == failureOrdinal)
            {
                throw new InjectedAtomicCommitFailureException();
            }
        }
    }

    private sealed class InjectedAtomicCommitFailureException : Exception;
}

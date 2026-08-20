using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.DurableScanning;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.Migrations;

/// <summary>
/// T048 is post-GREEN qualification.  These tests deliberately use the
/// disposable PostgreSQL 17 harness to prove that the accepted state-version,
/// lease, and atomic-commit contracts also hold on the production provider.
/// No RED is expected for this qualification-only task.
/// </summary>
public sealed class DurableScanConcurrencyPostgreSqlTests
{
    private static readonly DateTimeOffset At =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string RuntimeHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SourceHash =
        "e3b0c44298fc1c149afbf4f8996fb92427ae41e4649b934ca495991b7852b855";
    private const string FileIdentityHash =
        "a3f15bb08b2bd1684e84c4b2e2d673282c0e31f27a6a2e56aeb894ecff0c10f4";

    [Fact]
    public async Task PostgreSql17_CompetingClaimsAndTransitions_AllowExactlyOneWinnerAndRejectStaleLease()
    {
        string? administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administrator))
        {
            return;
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administrator);
        string databaseName = database.DatabaseName;
        try
        {
            await using (VulgataDbContext context = await CreateMigratedContextAsync(database.ConnectionString))
            {
                Assert.Equal(17, await ReadPostgreSqlMajorAsync(context));
                DurableFixture seeded = await DurableFixture.SeedAsync(context);
                DurableScanAuthorizationScope scope = seeded.Scope;
                DateTimeOffset claimAt = At.AddMinutes(10);
                Guid leaseOne = DeterministicTestIds.Create("t048-lease-one");
                Guid leaseTwo = DeterministicTestIds.Create("t048-lease-two");

                DurableScanStoreResult[] claims = await Task.WhenAll(
                    ClaimAsync(database.ConnectionString, scope, seeded, leaseOne, claimAt),
                    ClaimAsync(database.ConnectionString, scope, seeded, leaseTwo, claimAt));

                Assert.Equal(1, claims.Count(result => result.Outcome == DurableScanStoreOutcome.Applied));
                Assert.Equal(1, claims.Count(result => result.Outcome == DurableScanStoreOutcome.Conflict));
                DurableScanStoreResult winner = Assert.Single(
                    claims.Where(result => result.Outcome == DurableScanStoreOutcome.Applied));
                Guid winningLease = Assert.IsType<Guid>(winner.LeaseToken);
                Assert.Contains(
                    claims,
                    result => result.Outcome == DurableScanStoreOutcome.Conflict &&
                        result.Reason == DurableScanReason.StaleStateVersion);

                RunningScanJobTransitionCommand transition = new(
                    seeded.Execution.Id,
                    seeded.Job.Id,
                    1,
                    winningLease,
                    ScanJobExecutionState.RetryScheduled,
                    claimAt.AddMinutes(5),
                    ScanJobFailureCategory.JobTransient,
                    "t048-retry",
                    claimAt.AddMinutes(1));
                DurableScanStoreResult[] transitions = await Task.WhenAll(
                    TransitionAsync(database.ConnectionString, scope, transition),
                    TransitionAsync(database.ConnectionString, scope, transition));

                Assert.Equal(1, transitions.Count(result => result.Outcome == DurableScanStoreOutcome.Applied));
                Assert.Equal(1, transitions.Count(result => result.Outcome == DurableScanStoreOutcome.Conflict));
                Assert.Contains(
                    transitions,
                    result => result.Outcome == DurableScanStoreOutcome.Conflict &&
                        result.Reason == DurableScanReason.StaleStateVersion);
            }

            await using VulgataDbContext verify = CreateContext(database.ConnectionString);
            ScanJobExecutionState state = await verify.ScanJobExecutions
                .Select(job => job.State)
                .SingleAsync();
            Assert.Equal(ScanJobExecutionState.RetryScheduled, state);
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseWasDroppedAsync(administrator, databaseName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PostgreSql17_InjectedAtomicCommitFailure_LeavesNoPartialDocumentOrCompletedJob(
        int saveChangesOrdinal)
    {
        string? administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administrator))
        {
            return;
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administrator);
        string databaseName = database.DatabaseName;
        try
        {
            await using (VulgataDbContext seedContext =
                await CreateMigratedContextAsync(database.ConnectionString))
            {
                Assert.Equal(17, await ReadPostgreSqlMajorAsync(seedContext));
                _ = await DurableFixture.SeedAsync(seedContext, claim: true);
            }

            var interceptor = new ThrowingSaveChangesInterceptor(saveChangesOrdinal);
            await using VulgataDbContext context = CreateContext(database.ConnectionString, interceptor);
            DurableFixture fixture = await DurableFixture.LoadAsync(context);
            WorkerAgentResultCommitter committer = new(
                new KnowledgeStoreRepository(context, new KnowledgeDocumentMaterializer()));
            interceptor.Arm();

            ScanJobResultCommitResult result = await committer.CommitAsync(
                fixture.CreateCommitRequest(),
                CancellationToken.None);

            Assert.Equal(ScanJobResultCommitOutcome.Failed, result.Outcome);
            Assert.Equal(0, await context.Set<LogicalDocument>().CountAsync());
            Assert.Equal(0, await context.Set<DocumentVersion>().CountAsync());
            Assert.Equal(0, await context.Set<SourceAnchor>().CountAsync());
            Assert.Equal(0, await context.Set<DocumentSection>().CountAsync());
            Assert.Equal(0, await context.Set<IndexEntry>().CountAsync());
            ScanJobExecution job = await context.ScanJobExecutions.SingleAsync();
            Assert.Equal(ScanJobExecutionState.Running, job.State);
            Assert.Null(job.CommittedDocumentVersionId);
            Assert.Equal(1, job.StateVersion);
            Assert.Equal(fixture.LeaseToken, job.LeaseToken);
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseWasDroppedAsync(administrator, databaseName);
    }

    private static async Task<DurableScanStoreResult> ClaimAsync(
        string connectionString,
        DurableScanAuthorizationScope scope,
        DurableFixture fixture,
        Guid leaseToken,
        DateTimeOffset changedAt)
    {
        await using VulgataDbContext context = CreateContext(connectionString);
        return await new DurableScanRepository(context).ClaimPendingJobAsync(
            scope,
            new ScanJobLeaseCommand(
                fixture.Execution.Id,
                fixture.Job.Id,
                0,
                leaseToken,
                changedAt.AddMinutes(5),
                changedAt),
            CancellationToken.None);
    }

    private static async Task<DurableScanStoreResult> TransitionAsync(
        string connectionString,
        DurableScanAuthorizationScope scope,
        RunningScanJobTransitionCommand command)
    {
        await using VulgataDbContext context = CreateContext(connectionString);
        return await new DurableScanRepository(context).TransitionRunningJobAsync(
            scope,
            command,
            CancellationToken.None);
    }

    private static async Task<VulgataDbContext> CreateMigratedContextAsync(string connectionString)
    {
        VulgataDbContext context = CreateContext(connectionString);
        await context.Database.MigrateAsync();
        return context;
    }

    private static VulgataDbContext CreateContext(
        string connectionString,
        SaveChangesInterceptor? interceptor = null)
    {
        DbContextOptionsBuilder<VulgataDbContext> builder = new();
        builder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"));
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new VulgataDbContext(builder.Options);
    }

    private static async Task<int> ReadPostgreSqlMajorAsync(VulgataDbContext context)
    {
        NpgsqlConnection connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = new NpgsqlCommand(
            "SELECT (current_setting('server_version_num')::integer / 10000);",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task AssertDatabaseWasDroppedAsync(
        string administratorConnectionString,
        string databaseName)
    {
        await using var administrator = new NpgsqlConnection(administratorConnectionString);
        await administrator.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            administrator);
        command.Parameters.AddWithValue("name", databaseName);
        Assert.False((bool)(await command.ExecuteScalarAsync() ?? true));
    }

    private sealed class DurableFixture
    {
        private DurableFixture(
            SystemEntity system,
            Repository repository,
            ScanRun scanRun,
            CodeUnit codeUnit,
            ScanExecution execution,
            ScanJobExecution job,
            DurableScanAuthorizationScope scope,
            Guid leaseToken)
        {
            System = system;
            Repository = repository;
            ScanRun = scanRun;
            CodeUnit = codeUnit;
            Execution = execution;
            Job = job;
            Scope = scope;
            LeaseToken = leaseToken;
        }

        internal SystemEntity System { get; }
        internal Repository Repository { get; }
        internal ScanRun ScanRun { get; }
        internal CodeUnit CodeUnit { get; }
        internal ScanExecution Execution { get; }
        internal ScanJobExecution Job { get; }
        internal DurableScanAuthorizationScope Scope { get; }
        internal Guid LeaseToken { get; }

        internal static async Task<DurableFixture> SeedAsync(
            VulgataDbContext context,
            bool claim = false)
        {
            SystemEntity system = new("T048 system", null, null, At);
            Repository repository = system.AddRepository(
                "T048 repository", "https://example.invalid/t048.git", null, null, At);
            ScanRun scanRun = ScanRun.Start(repository.Id, 1_800, At);
            scanRun.BindProfiledRevision(Revision);
            CodeUnit codeUnit = new(
                scanRun.Id,
                repository.Id,
                "src/T048.cs",
                FileIdentityHash,
                SourceHash,
                1,
                10,
                "T048",
                CodeUnitType.File,
                RangeConfidence.Exact,
                "csharp");
            ScanExecution execution = ScanExecution.CreateNewPath(
                DeterministicTestIds.Create("t048-execution"),
                repository.Id,
                system.Id,
                scanRun.Id,
                Revision,
                "prompt/1.0",
                "document-schema/1.0",
                "{\"codeGraph\":\"1.5.0\"}",
                RuntimeHash,
                At.AddMinutes(2));
            context.Systems.Add(system);
            context.ScanRuns.Add(scanRun);
            context.CodeUnits.Add(codeUnit);
            await context.SaveChangesAsync();

            DurableScanAuthorizationScope scope = new("t048-actor", [system.Id]);
            DurableScanRepository store = new(context);
            ScanContextSnapshot snapshot = ScanContextSnapshot.Capture(
                scanRun, "global", "system", "repository", At.AddMinutes(1));
            await store.CreateContextSnapshotAsync(scope, snapshot, CancellationToken.None);
            await store.CreateScanExecutionAsync(scope, execution, CancellationToken.None);
            ScanJobExecution job = ScanJobExecution.Create(
                execution.Id,
                repository.Id,
                scanRun.Id,
                codeUnit.Id,
                1,
                At.AddMinutes(3));
            await store.InsertJobExecutionsAsync(
                scope, execution.Id, [job], CancellationToken.None);

            Guid leaseToken = DeterministicTestIds.Create("t048-lease");
            if (claim)
            {
                DurableScanStoreResult claimed = await store.ClaimPendingJobAsync(
                    scope,
                    new ScanJobLeaseCommand(
                        execution.Id,
                        job.Id,
                        0,
                        leaseToken,
                        At.AddMinutes(10),
                        At.AddMinutes(4)),
                    CancellationToken.None);
                Assert.Equal(DurableScanStoreOutcome.Applied, claimed.Outcome);
            }

            return new(system, repository, scanRun, codeUnit, execution, job, scope, leaseToken);
        }

        internal static async Task<DurableFixture> LoadAsync(VulgataDbContext context)
        {
            ScanJobExecution job = await context.ScanJobExecutions.SingleAsync();
            ScanExecution execution = await context.ScanExecutions.SingleAsync();
            CodeUnit codeUnit = await context.CodeUnits.SingleAsync();
            ScanRun scanRun = await context.ScanRuns.SingleAsync();
            Repository repository = await context.Repositories.SingleAsync();
            SystemEntity system = await context.Systems.SingleAsync();
            return new(
                system,
                repository,
                scanRun,
                codeUnit,
                execution,
                job,
                new DurableScanAuthorizationScope("t048-actor", [system.Id]),
                job.LeaseToken ?? throw new InvalidOperationException("The qualification job must be Running."));
        }

        internal ScanJobResultCommitRequest CreateCommitRequest()
        {
            Guid anchorId = DeterministicTestIds.Create("t048-anchor", Job.Id);
            Guid sectionId = DeterministicTestIds.Create("t048-section", Job.Id);
            WorkerAgentJobPacket packet = WorkerAgentJobPacket.Create(
                Job.Id,
                WorkerAgentJobKind.Pass1CodeLogicDocument,
                Execution.Id,
                Repository.Id,
                System.Id,
                ScanRun.Id,
                Revision,
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
                    [],
                    [],
                    [],
                    []));
            CanonicalDocumentContent content = new(
                KnowledgeDocumentLimits.SchemaIdentity,
                DocumentType.CodeLogic,
                "t048.document",
                "T048 document",
                CodeUnit.FilePath,
                "PostgreSQL atomic qualification.",
                [],
                [],
                [],
                [new CanonicalSection(
                    sectionId,
                    1,
                    DocumentSectionKind.Overview,
                    "Overview",
                    "PostgreSQL atomic qualification.",
                    "postgresql atomic qualification",
                    [anchorId],
                    [])]);
            Pass1CodeLogicDocumentResultPayload payload =
                Pass1CodeLogicDocumentResultPayload.Create(
                    packet.JobId,
                    packet.SystemId,
                    packet.RepositoryId,
                    packet.ScanRunId,
                    packet.CodeUnitId,
                    Revision,
                    CodeUnit.FilePath,
                    CodeUnit.LineStart,
                    CodeUnit.LineEnd,
                    CodeUnit.SourceHash,
                    CodeUnit.FileIdentityHash,
                    content,
                    [WorkerAgentSourceAnchor.Create(
                        anchorId,
                        CodeUnit.FilePath,
                        "T048",
                        CodeUnit.LineStart,
                        CodeUnit.LineEnd,
                        CodeUnit.SourceHash)],
                    [],
                    []);
            WorkerAgentResultEnvelope envelope = WorkerAgentResultEnvelope.CreateForJob(
                packet,
                WorkerAgentResultOutcome.Valid,
                null,
                At.AddMinutes(4),
                At.AddMinutes(4).AddSeconds(1),
                payload);
            ScanJobResultCommitRequest request = new(
                Scope,
                Repository.Id,
                Execution.Id,
                Job.Id,
                CodeUnit.Id,
                ScanRun.Id,
                Revision,
                "prompt/1.0",
                "document-schema/1.0",
                RuntimeHash,
                DeterministicTestIds.Create("t048-document-version", Job.Id),
                string.Empty,
                1,
                LeaseToken,
                envelope);
            SourceAnchorDraft[] anchors = payload.SourceAnchors
                .Select(anchor => new SourceAnchorDraft(
                    anchor.Id,
                    request.DocumentVersionId,
                    Repository.Id,
                    ScanRun.Id,
                    CodeUnit.Id,
                    Revision,
                    anchor.RepositoryRelativePath,
                    anchor.EvidenceLocator,
                    anchor.LineStart,
                    anchor.LineEnd,
                    anchor.SourceHash))
                .ToArray();
            KnowledgeCommit canonicalCommit = new(
                CreateScanStableId(
                    "durable-pass1-operation",
                    request.ScanJobExecutionId.ToString("D")),
                CreateScanStableId(
                    "durable-pass1-logical-document",
                    request.RepositoryId.ToString("D"),
                    payload.Content.StableKey),
                null,
                request.DocumentVersionId,
                request.RepositoryId,
                request.ScanRunId,
                request.ProfiledRevision,
                payload.Content,
                anchors,
                [],
                [],
                envelope.ReceivedAt);
            KnowledgeDocumentMaterializer materializer = new();
            KnowledgeDocumentMaterialization projection = materializer.Materialize(
                canonicalCommit.LogicalDocumentId,
                canonicalCommit.NewDocumentVersionId,
                canonicalCommit.RepositoryId,
                System.Id,
                canonicalCommit.Content,
                canonicalCommit.CommittedAt);
            return request with
            {
                CommitFingerprint = materializer.CreateCommitFingerprint(
                    canonicalCommit,
                    projection),
            };
        }

        private Guid CreateScanStableId(params string[] parts)
        {
            byte[] value = global::System.Security.Cryptography.SHA256.HashData(
                global::System.Text.Encoding.UTF8.GetBytes(string.Join('\n', parts)));
            return new Guid(value.AsSpan(0, 16));
        }
    }

    private sealed class ThrowingSaveChangesInterceptor(int failureOrdinal) : SaveChangesInterceptor
    {
        private int saveOrdinal;
        private bool armed;

        internal void Arm() => armed = true;

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
            if (armed && ++saveOrdinal == failureOrdinal)
            {
                throw new InjectedAtomicCommitFailureException();
            }
        }
    }

    private sealed class InjectedAtomicCommitFailureException : Exception;
}

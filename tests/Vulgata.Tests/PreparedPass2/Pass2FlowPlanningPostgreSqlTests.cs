using Vulgata.Tests.TestInfrastructure;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Tests.Migrations;
using Xunit.Abstractions;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T020 PostgreSQL RED baseline for the approved durable Pass 2 start seam.
/// The fixture persists a terminal Pass 1 pair/manifests, current consumer,
/// provider XML, provider implementation, and unsupported Documents, anchors,
/// reusable REFERENCES Edges, and one v2 exact decision before invoking
/// StartAsync. Boundary vectors then add only persisted current Documents and
/// independent exact decisions before observing the same public start seam.
/// </summary>
public sealed class Pass2FlowPlanningPostgreSqlTests(ITestOutputHelper output)
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task StartAsync_SeededTerminalPair_CreatesOneCompleteGroupJobBudgetAndExactReplayAsync()
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            output.WriteLine(
                "Qualification not executed: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
            throw new InvalidOperationException("Required PostgreSQL qualification prerequisite is unavailable; use eng/test.ps1 -Suite PostgreSql.");
        }

        await using DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            await using VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();

            ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
                ExactPass1DocumentKind.Consumer);
            ExactPass1DocumentFixture providerImplementation = fixture.CreatePass1Document(
                ExactPass1DocumentKind.Provider);
            await SeedAuthoritativePairAsync(context, fixture, consumer, providerImplementation);
            ExactPass1DocumentFixture providerXml = await SeedProviderXmlAsync(
                context,
                providerImplementation);
            ExactPass1DocumentFixture unsupported = fixture.CreatePass1Document(
                ExactPass1DocumentKind.UnsupportedConsumer);
            await SeedUnsupportedDocumentAsync(context, unsupported);
            await SeedProviderImplementationEdgeAsync(
                context,
                providerXml,
                providerImplementation.DocumentVersion.Id);

            ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
            ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
            ExactResolutionBatchOutcome resolution = await new ExactCrossRepositoryResolutionStore(
                context).ApplyAsync(
                BuildResolutionBatch(profile, occurrence, consumer, providerXml, unsupported),
                CreateLiveScope(
                    consumer.DocumentVersion.RepositoryId,
                    providerXml.DocumentVersion.RepositoryId));
            Assert.Equal(ExactResolutionOutcomeKind.Applied, resolution.Kind);
            CrossRepositoryResolutionDecision decision = await context
                .CrossRepositoryResolutionDecisions
                .SingleAsync(value => value.Outcome == "ExactMatch");
            Assert.Equal("ExactMatch", decision.Outcome);
            Assert.NotNull(decision.DocumentEdgeId);
            Assert.Equal(2, await context.DocumentEdges.CountAsync());

            Pass1PairExecution pair = await context.Pass1PairExecutions.SingleAsync();
            Assert.Equal(PreparedScanManifestApprovalState.Approved,
                await context.PreparedScanManifests.Select(manifest => manifest.ApprovalState)
                    .FirstAsync());
            Assert.Equal(4, await context.DocumentVersions.CountAsync());
            Assert.Equal(4, await context.SourceAnchors.CountAsync());

            string consumerEntryStableKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join(
                    '\n',
                    "consumer-entry/v1",
                    consumer.DocumentVersion.RepositoryId,
                    consumer.DocumentVersion.RepositoryRelativePath,
                    consumer.CodeUnit.SymbolName,
                    ExactResolutionProtocol.CustomRpc,
                    0))));
            Assert.Equal(pair.Id, decision.Pass1PairExecutionId);
            Assert.Equal(pair.CoordinationId, decision.CoordinationId);
            Assert.Equal(pair.SystemId, decision.SystemId);
            Assert.Equal(consumerEntryStableKey, decision.ConsumerEntryStableKey);

            RepositoryAccessScope scope = CreateLiveScope(
                consumer.DocumentVersion.RepositoryId,
                providerXml.DocumentVersion.RepositoryId);
            PreparedPass2Store store = CreateStore(context);
            PreparedPass2StartResult result = await store.StartAsync(
                decision.Pass1PairExecutionId,
                scope);

            Assert.Equal(PreparedPass2StartOutcome.Created, result.Outcome);
            Assert.NotNull(result.ExecutionId);
            Assert.Equal(1, result.FlowGroupCount);
            Assert.Equal(0, result.OmissionCount);
            Assert.Equal(1, result.JobCount);
            Assert.Equal(1, result.RetrievalBudgetCount);
            Assert.False(result.IsReplay);

            PreparedPass2StartResult replay = await store.StartAsync(
                decision.Pass1PairExecutionId,
                scope);
            Assert.Equal(PreparedPass2StartOutcome.Replay, replay.Outcome);
            Assert.Equal(result.ExecutionId, replay.ExecutionId);
            Assert.True(replay.IsReplay);
            Assert.Equal(1, await context.Pass2Executions.CountAsync());
            Assert.Equal(1, await context.FlowGroups.CountAsync());
            Assert.Equal(1, await context.Pass2JobExecutions.CountAsync());
            Assert.Equal(1, await context.Pass2RetrievalBudgets.CountAsync());
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task StartAsync_EightCurrentDocumentsAtSixteenStepBound_CreatesCompleteGroupAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, seed) =>
        {
            await AddOptionalDocumentsAsync(context, seed, 5);

            PreparedPass2StartResult result = await CreateStore(context).StartAsync(
                seed.Pair.Id,
                seed.Scope);

            Assert.Equal(PreparedPass2StartOutcome.Created, result.Outcome);
            Assert.Equal(1, result.FlowGroupCount);
            Assert.Equal(0, result.OmissionCount);
            FlowTrace trace = await context.FlowTraces.SingleAsync();
            FlowTraceStep[] steps = await context.FlowTraceSteps
                .Where(step => step.FlowTraceId == trace.Id)
                .OrderBy(step => step.StepOrder)
                .ToArrayAsync();
            Assert.Equal(15, steps.Length);
            Assert.InRange(steps.Length, 1, 16);
            Assert.Equal(8, steps.Select(step => step.DocumentVersionId).Distinct().Count());
            Assert.Equal(2, steps.Select(step => step.DocumentRepositoryId).Distinct().Count());
        });
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task StartAsync_NinthCurrentDocumentAndSeventeenthStep_AreBoundExcludedWithoutJobAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, seed) =>
        {
            await AddOptionalDocumentsAsync(context, seed, 6);

            PreparedPass2StartResult result = await CreateStore(context).StartAsync(
                seed.Pair.Id,
                seed.Scope);

            Assert.Equal(PreparedPass2StartOutcome.Created, result.Outcome);
            Assert.Equal(1, result.FlowGroupCount);
            Assert.Equal(0, result.OmissionCount);
            FlowGroup group = await context.FlowGroups.SingleAsync();
            Assert.Equal("BoundExcluded", group.State);
            Assert.Equal("flow-group-bound", group.ReasonCode);
            Assert.Empty(await context.Pass2JobExecutions.ToArrayAsync());
        });
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task StartAsync_EleventhGroupCandidate_PersistsContentDarkOmissionWithoutLeakageAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, seed) =>
        {
            await AddExactDecisionsAsync(context, seed, 10);

            PreparedPass2StartResult result = await CreateStore(context).StartAsync(
                seed.Pair.Id,
                seed.Scope);

            Assert.Equal(PreparedPass2StartOutcome.Created, result.Outcome);
            Assert.Equal(10, result.FlowGroupCount);
            Assert.Equal(1, result.OmissionCount);
            Pass2Omission omission = await context.Pass2Omissions.SingleAsync();
            Assert.Equal(Pass2OmissionState.Omitted, omission.State);
            Assert.Equal("flow-group-bound", omission.ReasonCode);
            Assert.Equal(10, await context.Pass2JobExecutions.CountAsync());
            Assert.Null(typeof(Pass2Omission).GetProperty("NormalizedEndpointIdentifier"));
            Assert.Null(typeof(Pass2Omission).GetProperty("ConsumerEntryIdentity"));
        });
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task StartAsync_RepeatedPlanningInput_IsByteEquivalentAndDoesNotDuplicateRowsAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, seed) =>
        {
            Guid executionId = ExactCrossRepositoryTestValues.Id(18_001);
            context.Pass2Executions.Add(Pass2Execution.CreatePlanning(
                executionId,
                seed.Pair.Id,
                seed.Pair.CoordinationId,
                seed.Pair.SystemId,
                seed.Pair.TerminalAt!.Value));
            await context.SaveChangesAsync();

            PreparedPass2FlowPlanningResult first = await new PreparedPass2FlowPlanner(context)
                .PlanAsync(executionId, seed.Scope);
            PreparedPass2FlowPlanningResult second = await new PreparedPass2FlowPlanner(context)
                .PlanAsync(executionId, seed.Scope);
            Assert.Equal(PlanningSnapshot(first), PlanningSnapshot(second));
            Assert.Empty(await context.FlowGroups.ToArrayAsync());
            Assert.Empty(await context.Pass2Omissions.ToArrayAsync());
        });
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task StartAsync_InducedPersistenceFailure_RollsBackExecutionGroupJobAndBudgetGraphAsync()
    {
        await RunOnSeededPostgreSqlAsync(async (context, seed) =>
        {
            context.Entry(seed.Decision).Property(value => value.ConsumerEntryStableKey).CurrentValue = "invalid";
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            PreparedPass2StartResult result = await CreateStore(context).StartAsync(
                seed.Pair.Id,
                seed.Scope);

            Assert.Equal(PreparedPass2StartOutcome.Conflict, result.Outcome);
            Assert.Empty(await context.Pass2Executions.ToArrayAsync());
            Assert.Empty(await context.FlowGroups.ToArrayAsync());
            Assert.Empty(await context.FlowTraces.ToArrayAsync());
            Assert.Empty(await context.Pass2JobExecutions.ToArrayAsync());
            Assert.Empty(await context.Pass2RetrievalBudgets.ToArrayAsync());
            Assert.Empty(await context.Pass2Omissions.ToArrayAsync());
        });
    }

    private async Task RunOnSeededPostgreSqlAsync(
        Func<VulgataDbContext, SeededPass2Fixture, Task> action)
    {
        string? administratorConnectionString =
            Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        if (string.IsNullOrWhiteSpace(administratorConnectionString))
        {
            output.WriteLine(
                "Qualification not executed: VULGATA_TEST_POSTGRES_ADMIN is unset; no database was contacted.");
            throw new InvalidOperationException("Required PostgreSQL qualification prerequisite is unavailable; use eng/test.ps1 -Suite PostgreSql.");
        }

        DisposablePostgreSqlDatabase database =
            await DisposablePostgreSqlDatabase.CreateAsync(administratorConnectionString);
        string databaseName = database.DatabaseName;
        output.WriteLine("Owned disposable PostgreSQL database: {0}", databaseName);
        try
        {
            await using ExactCrossRepositoryTestFixture fixture =
                await ExactCrossRepositoryTestFixture.CreateAsync();
            await using VulgataDbContext context = CreateContext(database.ConnectionString);
            await context.Database.MigrateAsync();
            SeededPass2Fixture seed = await SeedPass2Async(context, fixture);
            await action(context, seed);
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseDroppedAsync(administratorConnectionString, databaseName);
    }

    private static async Task<SeededPass2Fixture> SeedPass2Async(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture)
    {
        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(ExactPass1DocumentKind.Consumer);
        ExactPass1DocumentFixture providerImplementation = fixture.CreatePass1Document(ExactPass1DocumentKind.Provider);
        await SeedAuthoritativePairAsync(context, fixture, consumer, providerImplementation);
        ExactPass1DocumentFixture providerXml = await SeedProviderXmlAsync(context, providerImplementation);
        ExactPass1DocumentFixture unsupported = fixture.CreatePass1Document(ExactPass1DocumentKind.UnsupportedConsumer);
        await SeedUnsupportedDocumentAsync(context, unsupported);
        await SeedProviderImplementationEdgeAsync(context, providerXml, providerImplementation.DocumentVersion.Id);

        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        ResolutionOccurrence occurrence = Assert.Single(profile.Occurrences);
        ExactResolutionBatchOutcome resolution = await new ExactCrossRepositoryResolutionStore(context).ApplyAsync(
            BuildResolutionBatch(profile, occurrence, consumer, providerXml, unsupported),
            CreateLiveScope(
                consumer.DocumentVersion.RepositoryId,
                providerXml.DocumentVersion.RepositoryId));
        Assert.Equal(ExactResolutionOutcomeKind.Applied, resolution.Kind);
        CrossRepositoryResolutionDecision decision = await context
            .CrossRepositoryResolutionDecisions
            .SingleAsync(value => value.Outcome == "ExactMatch");
        Pass1PairExecution pair = await context.Pass1PairExecutions.SingleAsync();
        string stableKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            "consumer-entry/v1",
            consumer.DocumentVersion.RepositoryId,
            consumer.DocumentVersion.RepositoryRelativePath,
            consumer.CodeUnit.SymbolName,
            ExactResolutionProtocol.CustomRpc,
            0))));
        Assert.Equal(pair.Id, decision.Pass1PairExecutionId);
        Assert.Equal(pair.CoordinationId, decision.CoordinationId);
        Assert.Equal(pair.SystemId, decision.SystemId);
        Assert.Equal(stableKey, decision.ConsumerEntryStableKey);
        return new(
            consumer,
            providerXml,
            providerImplementation,
            unsupported,
            pair,
            decision,
            CreateLiveScope(
                consumer.DocumentVersion.RepositoryId,
                providerXml.DocumentVersion.RepositoryId));
    }

    private static RepositoryAccessScope CreateLiveScope(params Guid[] repositoryIds) =>
        new(repositoryIds, new AlwaysAllowAuthorizationVerifier());

    internal static PreparedPass2Store CreateStore(VulgataDbContext context) =>
        new(context, CreateWorkerContractAuthority());

    internal static IPreparedPass2WorkerContractAuthority CreateWorkerContractAuthority() =>
        new PreparedPass2WorkerContractAuthoritySource(
            new PreparedRetrieveCodeLogicFunction(new EmptyRetrievalService()),
            new PreparedSubmitBusinessLogicFunction());

    private sealed class AlwaysAllowAuthorizationVerifier : IRepositoryAuthorizationVerifier
    {
        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(true);
        }
    }

    private sealed class EmptyRetrievalService : IPass2CodeLogicRetrievalService
    {
        public Task<Pass2CodeLogicRetrievalResult> RetrieveAsync(
            Guid pass2JobExecutionId,
            Pass2CodeLogicRetrievalRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Pass2CodeLogicRetrievalResult.NoResults());
    }

    private static async Task AddOptionalDocumentsAsync(
        VulgataDbContext context,
        SeededPass2Fixture seed,
        int count)
    {
        for (int ordinal = 1; ordinal <= count; ordinal++)
        {
            Guid logicalDocumentId = ExactCrossRepositoryTestValues.Id(15_000 + ordinal);
            Guid documentVersionId = ExactCrossRepositoryTestValues.Id(15_100 + ordinal);
            Guid sourceAnchorId = ExactCrossRepositoryTestValues.Id(15_200 + ordinal);
            Guid edgeId = ExactCrossRepositoryTestValues.Id(15_300 + ordinal);
            string path = $"src/main/java/related/{documentVersionId:N}.java";
            LogicalDocument logicalDocument = LogicalDocument.Create(
                logicalDocumentId,
                seed.ProviderImplementation.DocumentVersion.RepositoryId,
                DocumentType.CodeLogic,
                $"exact-pass2/provider-optional-{ordinal:00}",
                ExactCrossRepositoryTestValues.CreatedAt);
            DocumentVersion documentVersion = DocumentVersion.Create(
                documentVersionId,
                logicalDocumentId,
                seed.ProviderImplementation.DocumentVersion.RepositoryId,
                seed.Pair.SystemId,
                seed.ProviderImplementation.ScanRun.Id,
                seed.ProviderImplementation.DocumentVersion.ProfiledRevision,
                1,
                $"Related implementation {ordinal:00}",
                path,
                KnowledgeDocumentLimits.SchemaIdentity,
                "{}",
                $"# Related implementation {ordinal:00}",
                new string((char)('3' + ordinal), 64),
                seed.ProviderImplementation.DocumentVersion.CommitFingerprint,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(10 + ordinal));
            documentVersion.SourceAnchors.Add(new SourceAnchor(
                sourceAnchorId,
                documentVersionId,
                seed.ProviderImplementation.DocumentVersion.RepositoryId,
                seed.ProviderImplementation.ScanRun.Id,
                seed.ProviderImplementation.CodeUnit.Id,
                seed.ProviderImplementation.DocumentVersion.ProfiledRevision,
                path,
                $"{path}#fixture",
                1,
                3,
                seed.ProviderImplementation.SourceAnchor.SourceHash,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(10 + ordinal)));

            context.LogicalDocuments.Add(logicalDocument);
            await context.SaveChangesAsync();
            context.DocumentVersions.Add(documentVersion);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"vulgata\".\"LogicalDocuments\" SET \"CurrentVersionId\" = {documentVersionId} WHERE \"Id\" = {logicalDocumentId}");
            context.DocumentEdges.Add(new DocumentEdge(
                edgeId,
                seed.ProviderImplementation.DocumentVersion.Id,
                seed.ProviderImplementation.DocumentVersion.RepositoryId,
                seed.ProviderImplementation.ScanRun.Id,
                documentVersionId,
                seed.ProviderImplementation.DocumentVersion.RepositoryId,
                seed.ProviderImplementation.ScanRun.Id,
                DocumentEdgeType.References,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddSeconds(10 + ordinal)));
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
    }

    private static async Task AddExactDecisionsAsync(
        VulgataDbContext context,
        SeededPass2Fixture seed,
        int additionalCount)
    {
        _ = seed;
        for (int ordinal = 0; ordinal < additionalCount; ordinal++)
        {
            await Pass2ProductionCompositionTests.AddSharedV2DecisionAsync(context);
        }
    }

    private static string PlanningSnapshot(PreparedPass2FlowPlanningResult result) =>
        string.Join(
            "|",
            result.FlowGroups
                .OrderBy(group => group.GroupOrder)
                .Select(group => string.Join(
                    ":",
                    group.Id,
                    group.State,
                    group.PlanFingerprint,
                    group.MembershipFingerprint,
                    string.Join(",", group.Traces.SelectMany(trace => trace.Steps).Select(step =>
                        $"{step.StepOrder}/{step.DocumentVersionId}/{step.EdgeId}/{step.ExactResolutionDecisionId}")))));

    private static VulgataDbContext CreateContext(string connectionString)
    {
        DbContextOptions<VulgataDbContext> options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                "public"))
            .Options;
        return new(options);
    }

    private static async Task SeedAuthoritativePairAsync(
        VulgataDbContext context,
        ExactCrossRepositoryTestFixture fixture,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider)
    {
        MethodInfo seedMethod = typeof(ExactCrossRepositoryResolutionTests).GetMethod(
            "SeedCurrentPairAsync",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The accepted Pass 1 fixture seeder is unavailable.");
        object? invocation = seedMethod.Invoke(null, [context, fixture, consumer, provider]);
        await Assert.IsAssignableFrom<Task>(invocation!);
        await (Task)invocation;
        context.ChangeTracker.Clear();
    }

    private static async Task<ExactPass1DocumentFixture> SeedProviderXmlAsync(
        VulgataDbContext context,
        ExactPass1DocumentFixture provider,
        CancellationToken cancellationToken = default)
    {
        Guid logicalDocumentId = Guid.Parse("11040000-0000-0000-0000-000000000911");
        Guid documentVersionId = Guid.Parse("11040000-0000-0000-0000-000000000912");
        Guid sourceAnchorId = Guid.Parse("11040000-0000-0000-0000-000000000913");
        Guid codeUnitId = Guid.Parse("11040000-0000-0000-0000-000000000914");
        var codeUnit = new CodeUnit(
            provider.ScanRun.Id,
            provider.DocumentVersion.RepositoryId,
            "src/main/resources/disgram/in/cust/updateSession.xml",
            ExactCrossRepositoryTestValues.SourceHash,
            ExactCrossRepositoryTestValues.SourceHash,
            1,
            2,
            "in/cust/updateSession",
            CodeUnitType.File,
            RangeConfidence.Exact,
            "xml");
        ExactCrossRepositoryReflection.SetId(codeUnit, codeUnitId);
        var logicalDocument = LogicalDocument.Create(
            logicalDocumentId,
            provider.DocumentVersion.RepositoryId,
            DocumentType.CodeLogic,
            "exact-pass2/provider-xml",
            ExactCrossRepositoryTestValues.CreatedAt);
        var documentVersion = DocumentVersion.Create(
            documentVersionId,
            logicalDocumentId,
            provider.DocumentVersion.RepositoryId,
            ExactCrossRepositoryTestValues.SystemId,
            provider.ScanRun.Id,
            provider.DocumentVersion.ProfiledRevision,
            1,
            "in/cust/updateSession",
            "src/main/resources/disgram/in/cust/updateSession.xml",
            KnowledgeDocumentLimits.SchemaIdentity,
            "{\"kind\":\"xml\",\"name\":\"in/cust/updateSession\"}",
            "# in/cust/updateSession",
            ExactCrossRepositoryTestValues.ContentFingerprint,
            ExactCrossRepositoryTestValues.CommitFingerprint,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        var sourceAnchor = new SourceAnchor(
            sourceAnchorId,
            documentVersionId,
            provider.DocumentVersion.RepositoryId,
            provider.ScanRun.Id,
            codeUnitId,
            provider.DocumentVersion.ProfiledRevision,
            "src/main/resources/disgram/in/cust/updateSession.xml",
            "provider.xml#line=2;rule=exact-xml-root-attribute-v1",
            1,
            2,
            ExactCrossRepositoryTestValues.SourceHash,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        documentVersion.SourceAnchors.Add(sourceAnchor);
        context.CodeUnits.Add(codeUnit);
        context.LogicalDocuments.Add(logicalDocument);
        // CurrentVersionId is a nullable FK to DocumentVersions. Persist the
        // owner first with a null pointer, then add the version and move the
        // pointer in a separate statement to avoid a circular insert.
        context.Entry(logicalDocument).Property(nameof(LogicalDocument.CurrentVersionId)).CurrentValue = null;
        await context.SaveChangesAsync(cancellationToken);
        context.DocumentVersions.Add(documentVersion);
        await context.SaveChangesAsync(cancellationToken);
        await context.LogicalDocuments
            .Where(value => value.Id == logicalDocumentId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.CurrentVersionId, documentVersionId),
                cancellationToken);
        context.ChangeTracker.Clear();
        return new(
            provider.ScanRun,
            provider.ScanExecution,
            codeUnit,
            logicalDocument,
            documentVersion,
            sourceAnchor);
    }

    private static async Task SeedUnsupportedDocumentAsync(
        VulgataDbContext context,
        ExactPass1DocumentFixture unsupported,
        CancellationToken cancellationToken = default)
    {
        context.ScanRuns.Add(unsupported.ScanRun);
        context.LogicalDocuments.Add(unsupported.LogicalDocument);
        context.Entry(unsupported.LogicalDocument)
            .Property(nameof(LogicalDocument.CurrentVersionId))
            .CurrentValue = null;
        context.DocumentVersions.Add(unsupported.DocumentVersion);
        await context.SaveChangesAsync(cancellationToken);
        await context.LogicalDocuments
            .Where(value => value.Id == unsupported.LogicalDocument.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    value => value.CurrentVersionId,
                    unsupported.DocumentVersion.Id),
                cancellationToken);
        context.ChangeTracker.Clear();
    }

    private static async Task SeedProviderImplementationEdgeAsync(
        VulgataDbContext context,
        ExactPass1DocumentFixture provider,
        Guid implementationVersionId)
    {
        context.DocumentEdges.Add(new DocumentEdge(
            Guid.Parse("11040000-0000-0000-0000-000000000915"),
            provider.DocumentVersion.Id,
            provider.DocumentVersion.RepositoryId,
            provider.DocumentVersion.ScanRunId,
            implementationVersionId,
            provider.DocumentVersion.RepositoryId,
            provider.DocumentVersion.ScanRunId,
            DocumentEdgeType.References,
            ExactCrossRepositoryTestValues.Pass1CompletedAt));
        await context.SaveChangesAsync();
    }

    private static ExactResolutionDecisionBatch BuildResolutionBatch(
        ExactResolutionProfile profile,
        ResolutionOccurrence occurrence,
        ExactPass1DocumentFixture consumer,
        ExactPass1DocumentFixture provider,
        ExactPass1DocumentFixture unsupported)
    {
        ExactResolutionOwner consumerOwner = Owner(consumer);
        ExactResolutionOwner providerOwner = Owner(provider);
        string consumerEntryStableKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join(
                '\n',
                "consumer-entry/v1",
                consumer.DocumentVersion.RepositoryId,
                consumer.DocumentVersion.RepositoryRelativePath,
                consumer.CodeUnit.SymbolName,
                ExactResolutionProtocol.CustomRpc,
                0))));
        var exact = new ExactResolutionDecision(
            occurrence.OccurrenceId,
            ExactResolutionDecisionKind.ExactMatch,
            consumerOwner,
            providerOwner,
            "exact_configured",
            "consumer.java#line=45;rule=exact-java-direct-literal-v1",
            "provider.xml#line=2;rule=exact-xml-root-attribute-v1",
            new ExactResolutionDecisionEvidence(
                "cust/updateSession",
                "in/cust/updateSession",
                "cust/updateSession",
                ExactResolutionProtocol.CustomRpc,
                1.0000m,
                "consumer.java#line=45;rule=exact-java-direct-literal-v1",
                "provider.xml#line=2;rule=exact-xml-root-attribute-v1"),
            consumerEntryStableKey,
            [new ProviderEndpointCandidate(
                occurrence.OccurrenceId,
                providerOwner,
                "in/cust/updateSession",
                "cust/updateSession",
                "provider.xml#line=2;rule=exact-xml-root-attribute-v1")]);
        var unsupportedDecision = new ExactResolutionDecision(
            Assert.Single(profile.UnsupportedObservations).ObservationId,
            ExactResolutionDecisionKind.UnsupportedObservation,
            consumerOwner,
            null,
            "unsupported_kind",
            "unsupported.java#line=66;rule=unsupported-member-invocation-v1",
            ConsumerEntryStableKey: consumerEntryStableKey);
        return ExactResolutionDecisionBatch.Create(
            profile,
            new[] { exact, unsupportedDecision }
                .OrderBy(value => value.Identity, StringComparer.Ordinal));
    }

    private static ExactResolutionOwner Owner(ExactPass1DocumentFixture document) =>
        new(
            document.DocumentVersion.RepositoryId,
            document.DocumentVersion.ScanRunId,
            document.DocumentVersion.Id,
            document.SourceAnchor.Id,
            document.DocumentVersion.ProfiledRevision);

    private static async Task AssertDatabaseDroppedAsync(
        string administratorConnectionString,
        string databaseName)
    {
        await using NpgsqlConnection connection = new(administratorConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);",
            connection);
        command.Parameters.AddWithValue("name", databaseName);
        Assert.False(Assert.IsType<bool>(await command.ExecuteScalarAsync()));
    }

    private static T New<T>() => (T)Activator.CreateInstance(
        typeof(T),
        BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null,
        args: null,
        culture: null)!;

    private static void Set<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

    private sealed record SeededPass2Fixture(
        ExactPass1DocumentFixture Consumer,
        ExactPass1DocumentFixture ProviderXml,
        ExactPass1DocumentFixture ProviderImplementation,
        ExactPass1DocumentFixture Unsupported,
        Pass1PairExecution Pair,
        CrossRepositoryResolutionDecision Decision,
        RepositoryAccessScope Scope);
}

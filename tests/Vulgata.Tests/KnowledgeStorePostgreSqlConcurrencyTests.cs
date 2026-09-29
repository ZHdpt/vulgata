using Vulgata.Tests.TestInfrastructure;
using System.Data.Common;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Tests;

public sealed class KnowledgeStorePostgreSqlConcurrencyTests
{
    private static readonly Guid _targetLogicalDocumentId =
        Guid.Parse("50000000-0000-0000-0000-000000000002");

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CommitAsync_TwoSuccessorsFromOneCurrentVersion_CommitsOneAndConflictsOne()
    {
        await using var database = await Vulgata.Tests.Migrations.DisposablePostgreSqlDatabase.CreateAsync(
            TestResourceScope.RequireAdministrator());
        string connectionString = database.ConnectionString;
        await ResetAndSeedAsync(connectionString);
        await using (VulgataDbContext setup = CreateContext(connectionString))
        {
            KnowledgeCommitResult initial = await CreateRepository(setup).CommitAsync(
                CreateCodeCommit(
                    KnowledgeStoreTestFixtures.LogicalDocumentId,
                    null,
                    KnowledgeStoreTestFixtures.DocumentVersionId,
                    Guid.Parse("e1000000-0000-0000-0000-000000000001")),
                Scope());
            Assert.Equal(KnowledgeCommitStatus.Committed, initial.Status);
        }

        var firstCapture = new ParameterCaptureInterceptor();
        var secondCapture = new ParameterCaptureInterceptor();
        await using VulgataDbContext firstContext =
            CreateContext(connectionString, firstCapture);
        await using VulgataDbContext secondContext =
            CreateContext(connectionString, secondCapture);
        var barrier = new PostgreSqlBarrier(2);
        KnowledgeCommit firstCommit = CreateCodeCommit(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            Guid.Parse("60000000-0000-0000-0000-000000000031"),
            Guid.Parse("e1000000-0000-0000-0000-000000000031"));
        KnowledgeCommit secondCommit = CreateCodeCommit(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            Guid.Parse("60000000-0000-0000-0000-000000000032"),
            Guid.Parse("e1000000-0000-0000-0000-000000000032"));

        Task<KnowledgeCommitResult> firstTask = StartTogetherAsync(
            CreateRepository(firstContext),
            firstCommit,
            barrier);
        Task<KnowledgeCommitResult> secondTask = StartTogetherAsync(
            CreateRepository(secondContext),
            secondCommit,
            barrier);
        KnowledgeCommitResult[] results = await Task.WhenAll(firstTask, secondTask);

        Assert.Single(results, result => result.Status == KnowledgeCommitStatus.Committed);
        Assert.Single(results, result => result.Status == KnowledgeCommitStatus.Conflict);
        Assert.All(
            results,
            result => Assert.True(
                result.Status is KnowledgeCommitStatus.Committed or KnowledgeCommitStatus.Conflict));
        Assert.Contains(
            firstCapture.Commands.Concat(secondCapture.Commands),
            command =>
                command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal) &&
                command.Parameters.Count > 0);
        await using VulgataDbContext verification = CreateContext(connectionString);
        LogicalDocument logicalDocument = await verification.Set<LogicalDocument>()
            .AsNoTracking()
            .SingleAsync(document => document.Id == KnowledgeStoreTestFixtures.LogicalDocumentId);
        DocumentVersion[] versions = await verification.Set<DocumentVersion>()
            .AsNoTracking()
            .OrderBy(version => version.VersionNumber)
            .ToArrayAsync();
        Assert.Equal(2, versions.Length);
        Assert.Equal(versions[1].Id, logicalDocument.CurrentVersionId);
        Assert.Equal(versions[1].Id, versions[0].SupersededById);
        Assert.Null(versions[1].SupersededById);
        Assert.Equal(2, await verification.Set<IndexEntry>().CountAsync());
        Assert.Equal(4, await verification.Set<DocumentSection>().CountAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CommitAsync_WaitingEdgeTargetIsSuperseded_ReturnsNonCurrentEndpointWithoutRows()
    {
        await using var database = await Vulgata.Tests.Migrations.DisposablePostgreSqlDatabase.CreateAsync(
            TestResourceScope.RequireAdministrator());
        string connectionString = database.ConnectionString;
        await ResetAndSeedAsync(connectionString);
        await using (VulgataDbContext setup = CreateContext(connectionString))
        {
            KnowledgeCommitResult initialTarget = await CreateRepository(setup).CommitAsync(
                CreateCodeCommit(
                    _targetLogicalDocumentId,
                    null,
                    KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                    Guid.Parse("e2000000-0000-0000-0000-000000000001"),
                    KnowledgeStoreTestFixtures.TargetRepositoryId,
                    KnowledgeStoreTestFixtures.TargetScanRunId),
                FullScope());
            Assert.Equal(KnowledgeCommitStatus.Committed, initialTarget.Status);
        }

        var supersederLock = new OwnerLockObserverInterceptor(pauseAfterAcquisition: true);
        var edgeLock = new OwnerLockObserverInterceptor(pauseAfterAcquisition: false);
        await using VulgataDbContext supersederContext =
            CreateContext(connectionString, supersederLock);
        await using VulgataDbContext edgeContext =
            CreateContext(connectionString, edgeLock);
        Guid targetSuccessorVersionId =
            Guid.Parse("60000000-0000-0000-0000-000000000041");
        Task<KnowledgeCommitResult> supersederTask = CreateRepository(supersederContext)
            .CommitAsync(
                CreateCodeCommit(
                    _targetLogicalDocumentId,
                    KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                    targetSuccessorVersionId,
                    Guid.Parse("e2000000-0000-0000-0000-000000000041"),
                    KnowledgeStoreTestFixtures.TargetRepositoryId,
                    KnowledgeStoreTestFixtures.TargetScanRunId),
                FullScope());
        await supersederLock.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Guid sourceVersionId = KnowledgeStoreTestFixtures.DocumentVersionId;
        Task<KnowledgeCommitResult> edgeTask = CreateRepository(edgeContext).CommitAsync(
            CreateBusinessCommitReferencingTarget(sourceVersionId),
            FullScope());
        await edgeLock.LockCommandStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(200);
        Assert.False(edgeTask.IsCompleted);

        supersederLock.Release();
        KnowledgeCommitResult supersederResult = await supersederTask;
        KnowledgeCommitResult edgeResult = await edgeTask;

        Assert.Equal(KnowledgeCommitStatus.Committed, supersederResult.Status);
        Assert.Equal(KnowledgeCommitStatus.Rejected, edgeResult.Status);
        Assert.Equal(KnowledgeRejectionCode.NonCurrentEndpoint, edgeResult.RejectionCode);
        await using VulgataDbContext verification = CreateContext(connectionString);
        Assert.False(await verification.Set<LogicalDocument>()
            .AnyAsync(document => document.Id == KnowledgeStoreTestFixtures.LogicalDocumentId));
        Assert.False(await verification.Set<DocumentVersion>()
            .AnyAsync(version => version.Id == sourceVersionId));
        Assert.Equal(0, await verification.Set<DocumentEdge>().CountAsync());
        Assert.False(await verification.Set<IndexEntry>()
            .AnyAsync(entry => entry.DocumentVersionId == sourceVersionId));
        Assert.False(await verification.Set<DocumentSection>()
            .AnyAsync(section => section.DocumentVersionId == sourceVersionId));
        LogicalDocument target = await verification.Set<LogicalDocument>()
            .AsNoTracking()
            .SingleAsync(
                document => document.Id == _targetLogicalDocumentId);
        Assert.Equal(targetSuccessorVersionId, target.CurrentVersionId);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CompositeForeignKeys_CrossOwnerCurrentSuccessorAndEdgeTuples_AreRejected()
    {
        await using var database = await Vulgata.Tests.Migrations.DisposablePostgreSqlDatabase.CreateAsync(
            TestResourceScope.RequireAdministrator());
        string connectionString = database.ConnectionString;
        await ResetAndSeedAsync(connectionString);
        await using VulgataDbContext context = CreateContext(connectionString);
        KnowledgeStoreRepository repository = CreateRepository(context);
        Assert.Equal(
            KnowledgeCommitStatus.Committed,
            (await repository.CommitAsync(
                CreateCodeCommit(
                    KnowledgeStoreTestFixtures.LogicalDocumentId,
                    null,
                    KnowledgeStoreTestFixtures.DocumentVersionId,
                    Guid.Parse("e4000000-0000-0000-0000-000000000001")),
                FullScope())).Status);
        Assert.Equal(
            KnowledgeCommitStatus.Committed,
            (await repository.CommitAsync(
                CreateCodeCommit(
                    _targetLogicalDocumentId,
                    null,
                    KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                    Guid.Parse("e4000000-0000-0000-0000-000000000002"),
                    KnowledgeStoreTestFixtures.TargetRepositoryId,
                    KnowledgeStoreTestFixtures.TargetScanRunId),
                FullScope())).Status);

        LogicalDocument sourceDocument = await context.Set<LogicalDocument>()
            .SingleAsync(document => document.Id == KnowledgeStoreTestFixtures.LogicalDocumentId);
        context.Entry(sourceDocument)
            .Property(document => document.CurrentVersionId)
            .CurrentValue = KnowledgeStoreTestFixtures.TargetDocumentVersionId;
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        context.ChangeTracker.Clear();
        DocumentVersion sourceVersion = await context.Set<DocumentVersion>()
            .SingleAsync(version => version.Id == KnowledgeStoreTestFixtures.DocumentVersionId);
        context.Entry(sourceVersion)
            .Property(version => version.SupersededById)
            .CurrentValue = KnowledgeStoreTestFixtures.TargetDocumentVersionId;
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        context.ChangeTracker.Clear();
        context.Set<DocumentEdge>().Add(new DocumentEdge(
            Guid.Parse("a4000000-0000-0000-0000-000000000001"),
            KnowledgeStoreTestFixtures.DocumentVersionId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.TargetDocumentVersionId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
            KnowledgeStoreTestFixtures.TargetScanRunId,
            DocumentEdgeType.References,
            KnowledgeStoreTestFixtures.CreatedAt));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task CommitAsync_ReplayedStableOperation_VerifiesCommittedVersionWithoutDuplicateRows()
    {
        await using var database = await Vulgata.Tests.Migrations.DisposablePostgreSqlDatabase.CreateAsync(
            TestResourceScope.RequireAdministrator());
        string connectionString = database.ConnectionString;
        await ResetAndSeedAsync(connectionString);
        KnowledgeCommit commit = CreateCodeCommit(
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            null,
            KnowledgeStoreTestFixtures.DocumentVersionId,
            Guid.Parse("e5000000-0000-0000-0000-000000000001"));
        await using VulgataDbContext firstContext = CreateContext(connectionString);
        KnowledgeCommitResult first =
            await CreateRepository(firstContext).CommitAsync(commit, Scope());
        await using VulgataDbContext replayContext = CreateContext(connectionString);
        DocumentVersion storedVersion = await replayContext.Set<DocumentVersion>()
            .AsNoTracking()
            .SingleAsync(version => version.Id == commit.NewDocumentVersionId);
        var materializer = new KnowledgeDocumentMaterializer();
        KnowledgeDocumentMaterialization expected = materializer.Materialize(
            commit.LogicalDocumentId,
            commit.NewDocumentVersionId,
            commit.RepositoryId,
            storedVersion.SystemId,
            commit.Content,
            commit.CommittedAt);
        Assert.Equal(expected.Content.Title, storedVersion.Title);
        Assert.Equal(
            expected.Content.RepositoryRelativePath,
            storedVersion.RepositoryRelativePath);
        Assert.Equal(expected.Content.SchemaIdentity, storedVersion.SchemaIdentity);
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(expected.ContentJson),
                JsonNode.Parse(storedVersion.ContentJson)));
        Assert.Equal(expected.ContentMarkdown, storedVersion.ContentMarkdown);
        Assert.Equal(expected.ContentFingerprint, storedVersion.ContentFingerprint);
        Assert.Equal(
            materializer.CreateCommitFingerprint(commit, expected),
            storedVersion.CommitFingerprint);
        Assert.Equal(commit.CommittedAt, storedVersion.CreatedAt);
        IndexEntry storedIndex = await replayContext.Set<IndexEntry>()
            .AsNoTracking()
            .SingleAsync(entry => entry.DocumentVersionId == commit.NewDocumentVersionId);
        Assert.Equal(expected.IndexEntry.Id, storedIndex.Id);
        Assert.Equal(expected.IndexEntry.Title, storedIndex.Title);
        Assert.Equal(expected.IndexEntry.Summary, storedIndex.Summary);
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(expected.IndexEntry.KeySymbolsJson),
                JsonNode.Parse(storedIndex.KeySymbolsJson)));
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(expected.IndexEntry.BusinessTermsJson),
                JsonNode.Parse(storedIndex.BusinessTermsJson)));
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(expected.IndexEntry.TechnicalTermsJson),
                JsonNode.Parse(storedIndex.TechnicalTermsJson)));
        DocumentSection[] storedSections = await replayContext.Set<DocumentSection>()
            .AsNoTracking()
            .Where(section => section.DocumentVersionId == commit.NewDocumentVersionId)
            .OrderBy(section => section.SectionOrder)
            .ToArrayAsync();
        Assert.Equal(expected.Sections.Count, storedSections.Length);
        foreach ((DocumentSection expectedSection, DocumentSection storedSection) in
            expected.Sections.Zip(storedSections))
        {
            Assert.Equal(expectedSection.Id, storedSection.Id);
            Assert.Equal(expectedSection.Heading, storedSection.Heading);
            Assert.Equal(expectedSection.ContentMarkdown, storedSection.ContentMarkdown);
            Assert.True(
                JsonNode.DeepEquals(
                    JsonNode.Parse(expectedSection.SourceAnchorIdsJson),
                    JsonNode.Parse(storedSection.SourceAnchorIdsJson)));
            Assert.True(
                JsonNode.DeepEquals(
                    JsonNode.Parse(expectedSection.DocumentVersionIdsJson),
                    JsonNode.Parse(storedSection.DocumentVersionIdsJson)));
        }

        KnowledgeCommitResult replay =
            await CreateRepository(replayContext).CommitAsync(commit, Scope());

        Assert.Equal(KnowledgeCommitStatus.Committed, first.Status);
        Assert.Equal(KnowledgeCommitStatus.Committed, replay.Status);
        Assert.Equal(commit.LogicalDocumentId, replay.LogicalDocumentId);
        Assert.Equal(commit.NewDocumentVersionId, replay.DocumentVersionId);
        await using VulgataDbContext verification = CreateContext(connectionString);
        Assert.Equal(1, await verification.Set<LogicalDocument>().CountAsync());
        Assert.Equal(1, await verification.Set<DocumentVersion>().CountAsync());
        Assert.Equal(1, await verification.Set<IndexEntry>().CountAsync());
        Assert.Equal(2, await verification.Set<DocumentSection>().CountAsync());
    }

    private static async Task<KnowledgeCommitResult> StartTogetherAsync(
        KnowledgeStoreRepository repository,
        KnowledgeCommit commit,
        PostgreSqlBarrier barrier)
    {
        await barrier.SignalAndWaitAsync();
        return await repository.CommitAsync(commit, Scope());
    }

    private static KnowledgeStoreRepository CreateRepository(VulgataDbContext context) =>
        new(context, new KnowledgeDocumentMaterializer());

    private static RepositoryAccessScope Scope() =>
        new([KnowledgeStoreTestFixtures.SourceRepositoryId]);

    private static RepositoryAccessScope FullScope() =>
        new(
        [
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.TargetRepositoryId,
        ]);

    private static KnowledgeCommit CreateCodeCommit(
        Guid logicalDocumentId,
        Guid? expectedCurrentVersionId,
        Guid documentVersionId,
        Guid operationId) =>
        CreateCodeCommit(
            logicalDocumentId,
            expectedCurrentVersionId,
            documentVersionId,
            operationId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId);

    private static KnowledgeCommit CreateCodeCommit(
        Guid logicalDocumentId,
        Guid? expectedCurrentVersionId,
        Guid documentVersionId,
        Guid operationId,
        Guid repositoryId,
        Guid scanRunId)
    {
        string json = KnowledgeStoreTestFixtures.CreateCanonicalJson(
            sections:
            [
                new(
                    KnowledgeStoreTestFixtures.FirstSectionId,
                    1,
                    "Overview",
                    "概览",
                    "处理订单请求。",
                    "处理订单请求",
                    [],
                    []),
                new(
                    KnowledgeStoreTestFixtures.SecondSectionId,
                    2,
                    "TechnicalDetail",
                    "实现",
                    "调用 API。",
                    "API orders",
                    [],
                    []),
            ]);
        KnowledgeDocumentMaterialization materialization =
            new KnowledgeDocumentMaterializer().Materialize(
                logicalDocumentId,
                documentVersionId,
                repositoryId,
                null,
                json,
                KnowledgeStoreTestFixtures.CreatedAt);
        return new(
            operationId,
            logicalDocumentId,
            expectedCurrentVersionId,
            documentVersionId,
            repositoryId,
            scanRunId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            materialization.Content,
            [],
            [],
            [],
            KnowledgeStoreTestFixtures.CreatedAt.AddMinutes(
                expectedCurrentVersionId is null ? 0 : 1));
    }

    private static KnowledgeCommit CreateBusinessCommitReferencingTarget(Guid documentVersionId)
    {
        string json = KnowledgeStoreTestFixtures.CreateCanonicalJson(
            documentType: "BusinessLogic",
            sections:
            [
                new(
                    KnowledgeStoreTestFixtures.FirstSectionId,
                    1,
                    "Overview",
                    "概览",
                    "跨仓库订单业务。",
                    "跨仓库订单业务",
                    [],
                    []),
            ]);
        KnowledgeDocumentMaterialization materialization =
            new KnowledgeDocumentMaterializer().Materialize(
                KnowledgeStoreTestFixtures.LogicalDocumentId,
                documentVersionId,
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                null,
                json,
                KnowledgeStoreTestFixtures.CreatedAt);
        return new(
            Guid.Parse("e3000000-0000-0000-0000-000000000001"),
            KnowledgeStoreTestFixtures.LogicalDocumentId,
            null,
            documentVersionId,
            KnowledgeStoreTestFixtures.SourceRepositoryId,
            KnowledgeStoreTestFixtures.SourceScanRunId,
            KnowledgeStoreTestFixtures.ProfiledRevision,
            materialization.Content,
            [],
            [
                new EdgeDraft(
                    Guid.Parse("a3000000-0000-0000-0000-000000000001"),
                    documentVersionId,
                    KnowledgeStoreTestFixtures.SourceRepositoryId,
                    KnowledgeStoreTestFixtures.SourceScanRunId,
                    KnowledgeStoreTestFixtures.TargetDocumentVersionId,
                    KnowledgeStoreTestFixtures.TargetRepositoryId,
                    KnowledgeStoreTestFixtures.TargetScanRunId,
                    DocumentEdgeType.GeneratedFrom),
            ],
            [],
            KnowledgeStoreTestFixtures.CreatedAt.AddMinutes(1));
    }

    private static async Task ResetAndSeedAsync(string connectionString)
    {
        await using VulgataDbContext context = CreateContext(connectionString);
        await context.Database.MigrateAsync();
        context.Repositories.AddRange(
            CreateStandaloneRepository(
                KnowledgeStoreTestFixtures.SourceRepositoryId,
                "source"),
            CreateStandaloneRepository(
                KnowledgeStoreTestFixtures.TargetRepositoryId,
                "target"));
        context.ScanRuns.AddRange(
            KnowledgeStoreTestFixtures.CreateCompletedScanRun(
                KnowledgeStoreTestFixtures.SourceScanRunId,
                KnowledgeStoreTestFixtures.SourceRepositoryId),
            KnowledgeStoreTestFixtures.CreateCompletedScanRun(
                KnowledgeStoreTestFixtures.TargetScanRunId,
                KnowledgeStoreTestFixtures.TargetRepositoryId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static VulgataDbContext CreateContext(
        string connectionString,
        params IInterceptor[] interceptors)
    {
        DbContextOptionsBuilder<VulgataDbContext> options = new();
        options.UseNpgsql(connectionString);
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new VulgataDbContext(options.Options);
    }

    private static Repository CreateStandaloneRepository(Guid id, string name)
    {
        Repository repository = Repository.CreateStandalone(
            name,
            $"https://example.invalid/{name}.git",
            null,
            null,
            KnowledgeStoreTestFixtures.CreatedAt);
        typeof(Repository)
            .GetProperty(nameof(Repository.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(repository, id);
        return repository;
    }

}

internal sealed class OwnerLockObserverInterceptor(bool pauseAfterAcquisition)
    : DbCommandInterceptor
{
    private readonly TaskCompletionSource _lockCommandStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _lockAcquired =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _observed;

    internal TaskCompletionSource LockCommandStarted => _lockCommandStarted;
    internal TaskCompletionSource LockAcquired => _lockAcquired;

    internal void Release() => _release.TrySetResult();

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
        {
            _lockCommandStarted.TrySetResult();
        }

        return ValueTask.FromResult(result);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal) &&
            Interlocked.Exchange(ref _observed, 1) == 0)
        {
            _lockAcquired.TrySetResult();
            if (pauseAfterAcquisition)
            {
                await _release.Task.WaitAsync(cancellationToken);
            }
        }

        return result;
    }
}

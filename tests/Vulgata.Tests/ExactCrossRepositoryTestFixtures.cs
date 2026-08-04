using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

public sealed class ExactCrossRepositoryTestFixtureTests
{
    [Fact]
    public async Task CreateAsync_DisposableTwoRepositoryFixture_IsReusableAndCleanedAsync()
    {
        ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        string[] roots =
        [
            fixture.ConsumerRepository.RootPath,
            fixture.ProviderRepository.RootPath,
            fixture.Database.RootPath,
        ];

        try
        {
            Assert.Equal(roots.Length, roots.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(roots, root => Assert.True(Directory.Exists(root)));
            Assert.True(File.Exists(fixture.Database.DatabasePath));
            Assert.NotEqual(fixture.ConsumerRepository.Commit, fixture.ProviderRepository.Commit);
            Assert.All(
                [fixture.ConsumerRepository.Commit, fixture.ProviderRepository.Commit],
                commit => Assert.Matches("^[0-9a-f]{40}$", commit));

            Assert.True(fixture.Access.Authorization.Authorize(
                ExactCrossRepositoryTestValues.SystemId,
                fixture.Access.AuthorizedScope));
            Assert.False(fixture.Access.Authorization.Authorize(
                ExactCrossRepositoryTestValues.SystemId,
                fixture.Access.ConsumerOnlyScope));
            Assert.True(fixture.Access.Snapshot().IsZero);

            ExactPass1DocumentFixture consumer =
                fixture.CreatePass1Document(ExactPass1DocumentKind.Consumer);
            ExactPass1DocumentFixture provider =
                fixture.CreatePass1Document(ExactPass1DocumentKind.Provider);
            Assert.Equal(ScanExecutionPhase.Pass1Completed, consumer.ScanExecution.Phase);
            Assert.Equal(ScanExecutionPhase.Pass1Completed, provider.ScanExecution.Phase);
            Assert.Equal(consumer.DocumentVersion.Id, consumer.SourceAnchor.DocumentVersionId);
            Assert.Equal(provider.DocumentVersion.Id, provider.SourceAnchor.DocumentVersionId);
            Assert.Equal(fixture.ConsumerRepository.Commit, consumer.DocumentVersion.ProfiledRevision);
            Assert.Equal(fixture.ProviderRepository.Commit, provider.DocumentVersion.ProfiledRevision);

            ILogger logger = fixture.Logs.CreateLogger("ExactCrossRepositoryFixture");
            logger.LogWarning("bounded fixture warning");
            ExactCrossRepositoryCapturedLog captured = Assert.Single(fixture.Logs.Messages);
            Assert.Equal("bounded fixture warning", captured.Message);
            Assert.Null(captured.ExceptionMessage);
        }
        finally
        {
            await fixture.DisposeAsync();
        }

        Assert.All(roots, root => Assert.False(Directory.Exists(root)));
    }
}

internal sealed class ExactCrossRepositoryTestFixture : IAsyncDisposable
{
    private ExactCrossRepositoryTestFixture(
        ExactCrossRepositoryTemporaryGitRepository consumerRepository,
        ExactCrossRepositoryTemporaryGitRepository providerRepository,
        ExactCrossRepositorySqliteDatabase database)
    {
        ConsumerRepository = consumerRepository;
        ProviderRepository = providerRepository;
        Database = database;
        Access = new(
            ExactCrossRepositoryTestValues.SystemId,
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ProviderRepositoryId);
        Logs = new();
    }

    internal ExactCrossRepositoryTemporaryGitRepository ConsumerRepository { get; }

    internal ExactCrossRepositoryTemporaryGitRepository ProviderRepository { get; }

    internal ExactCrossRepositorySqliteDatabase Database { get; }

    internal ExactCrossRepositoryAccessProbe Access { get; }

    internal ExactCrossRepositoryLogCapture Logs { get; }

    internal static async Task<ExactCrossRepositoryTestFixture> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        ExactCrossRepositoryTemporaryGitRepository? consumer = null;
        ExactCrossRepositoryTemporaryGitRepository? provider = null;
        ExactCrossRepositorySqliteDatabase? database = null;
        try
        {
            consumer = await ExactCrossRepositoryTemporaryGitRepository.CreateAsync(
                "consumer",
                ConsumerFiles(),
                cancellationToken);
            provider = await ExactCrossRepositoryTemporaryGitRepository.CreateAsync(
                "provider",
                ProviderFiles(),
                cancellationToken);
            database = await ExactCrossRepositorySqliteDatabase.CreateAsync(cancellationToken);
            return new(consumer, provider, database);
        }
        catch
        {
            if (database is not null)
            {
                await database.DisposeAsync();
            }

            if (provider is not null)
            {
                await provider.DisposeAsync();
            }

            if (consumer is not null)
            {
                await consumer.DisposeAsync();
            }

            throw;
        }
    }

    internal ExactCrossRepositoryManagedFixture CreateManagedRepositories()
    {
        var system = new SystemEntity(
            "精确跨仓库解析测试系统",
            null,
            null,
            ExactCrossRepositoryTestValues.CreatedAt);
        ExactCrossRepositoryReflection.SetId(system, ExactCrossRepositoryTestValues.SystemId);

        RepositoryEntity consumer = system.AddRepository(
            "fixture-consumer",
            ConsumerRepository.RootPath,
            null,
            null,
            ExactCrossRepositoryTestValues.CreatedAt);
        ExactCrossRepositoryReflection.SetId(
            consumer,
            ExactCrossRepositoryTestValues.ConsumerRepositoryId);

        RepositoryEntity provider = system.AddRepository(
            "fixture-provider",
            ProviderRepository.RootPath,
            null,
            null,
            ExactCrossRepositoryTestValues.CreatedAt);
        ExactCrossRepositoryReflection.SetId(
            provider,
            ExactCrossRepositoryTestValues.ProviderRepositoryId);

        return new(system, consumer, provider);
    }

    internal ExactPass1DocumentFixture CreatePass1Document(
        ExactPass1DocumentKind kind,
        string? profiledRevision = null)
    {
        ExactPass1DocumentDescriptor descriptor = kind switch
        {
            ExactPass1DocumentKind.Consumer => new(
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.ConsumerScanRunId,
                ExactCrossRepositoryTestValues.ConsumerScanExecutionId,
                ExactCrossRepositoryTestValues.ConsumerCodeUnitId,
                ExactCrossRepositoryTestValues.ConsumerLogicalDocumentId,
                ExactCrossRepositoryTestValues.ConsumerDocumentVersionId,
                ExactCrossRepositoryTestValues.ConsumerSourceAnchorId,
                ExactCrossRepositoryTestValues.ConsumerPass1JobId,
                ExactCrossRepositoryTestValues.ConsumerPreScanResultId,
                "src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java",
                "cn.com.yitong.actions.mobile.UpdateChannelSessionOp",
                40,
                50,
                "UpdateChannelSessionOp.java#line=45;ordinal=0"),
            ExactPass1DocumentKind.Provider => new(
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                ExactCrossRepositoryTestValues.ProviderScanRunId,
                ExactCrossRepositoryTestValues.ProviderScanExecutionId,
                ExactCrossRepositoryTestValues.ProviderCodeUnitId,
                ExactCrossRepositoryTestValues.ProviderLogicalDocumentId,
                ExactCrossRepositoryTestValues.ProviderDocumentVersionId,
                ExactCrossRepositoryTestValues.ProviderSourceAnchorId,
                ExactCrossRepositoryTestValues.ProviderPass1JobId,
                ExactCrossRepositoryTestValues.ProviderPreScanResultId,
                "src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java",
                "cn.com.yitong.actions.domain.login.UpdateSessionOp",
                1,
                20,
                "UpdateSessionOp.java#type=cn.com.yitong.actions.domain.login.UpdateSessionOp"),
            ExactPass1DocumentKind.UnsupportedConsumer => new(
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                ExactCrossRepositoryTestValues.UnsupportedScanRunId,
                ExactCrossRepositoryTestValues.UnsupportedScanExecutionId,
                ExactCrossRepositoryTestValues.UnsupportedCodeUnitId,
                ExactCrossRepositoryTestValues.UnsupportedLogicalDocumentId,
                ExactCrossRepositoryTestValues.UnsupportedDocumentVersionId,
                ExactCrossRepositoryTestValues.UnsupportedSourceAnchorId,
                ExactCrossRepositoryTestValues.UnsupportedPass1JobId,
                ExactCrossRepositoryTestValues.UnsupportedPreScanResultId,
                "src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java",
                "cn.com.yitong.actions.cust.UploadOcrToOssOp",
                60,
                70,
                "UploadOcrToOssOp.java#line=66;ordinal=0"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        string revision = profiledRevision ?? (kind == ExactPass1DocumentKind.Provider
            ? ProviderRepository.Commit
            : ConsumerRepository.Commit);
        ScanRun scanRun = ScanRun.Start(
            descriptor.RepositoryId,
            1_800,
            ExactCrossRepositoryTestValues.CreatedAt);
        ExactCrossRepositoryReflection.SetId(scanRun, descriptor.ScanRunId);
        scanRun.BindProfiledRevision(revision);
        scanRun.SetCandidateCount(1);

        var codeUnit = new CodeUnit(
            descriptor.ScanRunId,
            descriptor.RepositoryId,
            descriptor.RepositoryRelativePath,
            ExactCrossRepositoryTestValues.SourceHash,
            ExactCrossRepositoryTestValues.SourceHash,
            descriptor.LineStart,
            descriptor.LineEnd,
            descriptor.Symbol,
            CodeUnitType.Class,
            RangeConfidence.Exact,
            "java");
        ExactCrossRepositoryReflection.SetId(codeUnit, descriptor.CodeUnitId);
        scanRun.CodeUnits.Add(codeUnit);
        scanRun.Complete(
            1,
            1,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);

        LogicalDocument logicalDocument = LogicalDocument.Create(
            descriptor.LogicalDocumentId,
            descriptor.RepositoryId,
            DocumentType.CodeLogic,
            $"exact-pass1/{kind.ToString().ToLowerInvariant()}",
            ExactCrossRepositoryTestValues.CreatedAt);
        DocumentVersion documentVersion = DocumentVersion.Create(
            descriptor.DocumentVersionId,
            descriptor.LogicalDocumentId,
            descriptor.RepositoryId,
            ExactCrossRepositoryTestValues.SystemId,
            descriptor.ScanRunId,
            revision,
            1,
            descriptor.Symbol,
            descriptor.RepositoryRelativePath,
            KnowledgeDocumentLimits.SchemaIdentity,
            "{}",
            $"# {descriptor.Symbol}",
            ExactCrossRepositoryTestValues.ContentFingerprint,
            ExactCrossRepositoryTestValues.CommitFingerprint,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        var sourceAnchor = new SourceAnchor(
            descriptor.SourceAnchorId,
            descriptor.DocumentVersionId,
            descriptor.RepositoryId,
            descriptor.ScanRunId,
            descriptor.CodeUnitId,
            revision,
            descriptor.RepositoryRelativePath,
            descriptor.EvidenceLocator,
            descriptor.LineStart,
            descriptor.LineEnd,
            ExactCrossRepositoryTestValues.SourceHash,
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        documentVersion.SourceAnchors.Add(sourceAnchor);
        logicalDocument.SetInitialCurrentVersion(documentVersion);

        ScanExecution execution = ScanExecution.Start(
            descriptor.ScanExecutionId,
            descriptor.RepositoryId,
            ExactCrossRepositoryTestValues.SystemId,
            descriptor.ScanRunId,
            descriptor.PreScanResultId,
            revision,
            ExactCrossRepositoryTestValues.CreatedAt);
        execution.Plan(1, ExactCrossRepositoryTestValues.CreatedAt);
        execution.Advance(descriptor.Pass1JobId);
        execution.RecordValid(descriptor.Pass1JobId);
        execution.CompletePass1();

        return new(
            scanRun,
            execution,
            codeUnit,
            logicalDocument,
            documentVersion,
            sourceAnchor);
    }

    public async ValueTask DisposeAsync()
    {
        Logs.Dispose();
        await Database.DisposeAsync();
        await ProviderRepository.DisposeAsync();
        await ConsumerRepository.DisposeAsync();
    }

    private static IReadOnlyDictionary<string, string> ConsumerFiles() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java"] =
                SourceWithLine(
                    45,
                    "return netTools.execute(ctx, \"cust/updateSession\");"),
            ["src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java"] =
                SourceWithLine(
                    66,
                    "ossClientService.simpleUploadToOSSBase64String(request);"),
        };

    private static IReadOnlyDictionary<string, string> ProviderFiles() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/main/resources/disgram/in/cust/updateSession.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<trans name=\"in/cust/updateSession\"/>\n",
            ["src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java"] =
                SourceWithLine(3, "public final class UpdateSessionOp {}"),
        };

    private static string SourceWithLine(int line, string statement)
    {
        var builder = new StringBuilder();
        for (int currentLine = 1; currentLine < line; currentLine++)
        {
            builder.Append("// fixture line ")
                .Append(currentLine)
                .Append('\n');
        }

        return builder.Append(statement).Append('\n').ToString();
    }
}

internal enum ExactPass1DocumentKind
{
    Consumer,
    Provider,
    UnsupportedConsumer,
}

internal sealed record ExactCrossRepositoryManagedFixture(
    SystemEntity System,
    RepositoryEntity ConsumerRepository,
    RepositoryEntity ProviderRepository);

internal sealed record ExactPass1DocumentFixture(
    ScanRun ScanRun,
    ScanExecution ScanExecution,
    CodeUnit CodeUnit,
    LogicalDocument LogicalDocument,
    DocumentVersion DocumentVersion,
    SourceAnchor SourceAnchor);

internal sealed record ExactPass1DocumentDescriptor(
    Guid RepositoryId,
    Guid ScanRunId,
    Guid ScanExecutionId,
    Guid CodeUnitId,
    Guid LogicalDocumentId,
    Guid DocumentVersionId,
    Guid SourceAnchorId,
    Guid Pass1JobId,
    Guid PreScanResultId,
    string RepositoryRelativePath,
    string Symbol,
    int LineStart,
    int LineEnd,
    string EvidenceLocator);

internal static class ExactCrossRepositoryTestValues
{
    internal static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);

    internal static readonly DateTimeOffset Pass1CompletedAt = CreatedAt.AddMinutes(1);

    internal static readonly Guid SystemId = Id(1);
    internal static readonly Guid ConsumerRepositoryId = Id(101);
    internal static readonly Guid ProviderRepositoryId = Id(102);
    internal static readonly Guid ConsumerScanRunId = Id(201);
    internal static readonly Guid ProviderScanRunId = Id(202);
    internal static readonly Guid UnsupportedScanRunId = Id(203);
    internal static readonly Guid ConsumerScanExecutionId = Id(301);
    internal static readonly Guid ProviderScanExecutionId = Id(302);
    internal static readonly Guid UnsupportedScanExecutionId = Id(303);
    internal static readonly Guid ConsumerCodeUnitId = Id(401);
    internal static readonly Guid ProviderCodeUnitId = Id(402);
    internal static readonly Guid UnsupportedCodeUnitId = Id(403);
    internal static readonly Guid ConsumerLogicalDocumentId = Id(501);
    internal static readonly Guid ProviderLogicalDocumentId = Id(502);
    internal static readonly Guid UnsupportedLogicalDocumentId = Id(503);
    internal static readonly Guid ConsumerDocumentVersionId = Id(601);
    internal static readonly Guid ProviderDocumentVersionId = Id(602);
    internal static readonly Guid UnsupportedDocumentVersionId = Id(603);
    internal static readonly Guid ConsumerSourceAnchorId = Id(701);
    internal static readonly Guid ProviderSourceAnchorId = Id(702);
    internal static readonly Guid UnsupportedSourceAnchorId = Id(703);
    internal static readonly Guid ConsumerPass1JobId = Id(801);
    internal static readonly Guid ProviderPass1JobId = Id(802);
    internal static readonly Guid UnsupportedPass1JobId = Id(803);
    internal static readonly Guid ConsumerPreScanResultId = Id(901);
    internal static readonly Guid ProviderPreScanResultId = Id(902);
    internal static readonly Guid UnsupportedPreScanResultId = Id(903);

    internal const string SourceHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    internal const string ContentFingerprint =
        "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    internal const string CommitFingerprint =
        "2123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    internal const string ExpectedRef = "refs/tags/vulgata/August15th-sample";

    internal static Guid Id(int value) =>
        Guid.Parse($"11040000-0000-0000-0000-{value:D12}");
}

internal sealed class ExactCrossRepositoryAuthorizationProbe(
    Guid systemId,
    Guid consumerRepositoryId,
    Guid providerRepositoryId)
{
    private int _callCount;

    internal bool SystemVisible { get; set; } = true;

    internal int CallCount => Volatile.Read(ref _callCount);

    internal bool Authorize(Guid requestedSystemId, RepositoryAccessScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Interlocked.Increment(ref _callCount);
        return SystemVisible &&
            requestedSystemId == systemId &&
            scope.Contains(consumerRepositoryId) &&
            scope.Contains(providerRepositoryId);
    }
}

internal sealed class ExactCrossRepositoryAccessProbe
{
    private int _gitCalls;
    private int _sourceCalls;
    private int _queryCalls;
    private int _writeCalls;

    internal ExactCrossRepositoryAccessProbe(
        Guid systemId,
        Guid consumerRepositoryId,
        Guid providerRepositoryId)
    {
        Authorization = new(systemId, consumerRepositoryId, providerRepositoryId);
        AuthorizedScope = new([consumerRepositoryId, providerRepositoryId]);
        ConsumerOnlyScope = new([consumerRepositoryId]);
        ProviderOnlyScope = new([providerRepositoryId]);
    }

    internal ExactCrossRepositoryAuthorizationProbe Authorization { get; }

    internal RepositoryAccessScope AuthorizedScope { get; }

    internal RepositoryAccessScope ConsumerOnlyScope { get; }

    internal RepositoryAccessScope ProviderOnlyScope { get; }

    internal int GitCallCount => Volatile.Read(ref _gitCalls);

    internal int SourceCallCount => Volatile.Read(ref _sourceCalls);

    internal int QueryCallCount => Volatile.Read(ref _queryCalls);

    internal int WriteCallCount => Volatile.Read(ref _writeCalls);

    internal ExactCrossRepositoryAccessCounts Snapshot() =>
        new(GitCallCount, SourceCallCount, QueryCallCount, WriteCallCount);

    internal void RecordGitCall() => Interlocked.Increment(ref _gitCalls);

    internal void RecordSourceCall() => Interlocked.Increment(ref _sourceCalls);

    internal void RecordQueryCall() => Interlocked.Increment(ref _queryCalls);

    internal void RecordWriteCall() => Interlocked.Increment(ref _writeCalls);

    internal void ResetDataAccessCounters()
    {
        Interlocked.Exchange(ref _gitCalls, 0);
        Interlocked.Exchange(ref _sourceCalls, 0);
        Interlocked.Exchange(ref _queryCalls, 0);
        Interlocked.Exchange(ref _writeCalls, 0);
    }
}

internal sealed record ExactCrossRepositoryAccessCounts(
    int GitCalls,
    int SourceCalls,
    int QueryCalls,
    int WriteCalls)
{
    internal bool IsZero =>
        GitCalls == 0 && SourceCalls == 0 && QueryCalls == 0 && WriteCalls == 0;
}

internal sealed class ExactCrossRepositoryDatabaseAccessInterceptor(
    ExactCrossRepositoryAccessProbe access) : DbCommandInterceptor
{
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        access.RecordWriteCall();
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        access.RecordWriteCall();
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        access.RecordQueryCall();
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        access.RecordQueryCall();
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        access.RecordQueryCall();
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        access.RecordQueryCall();
        return ValueTask.FromResult(result);
    }
}

internal sealed class ExactCrossRepositorySqliteDatabase : IAsyncDisposable
{
    private ExactCrossRepositorySqliteDatabase(string rootPath, string databasePath)
    {
        RootPath = rootPath;
        DatabasePath = databasePath;
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();
    }

    internal string RootPath { get; }

    internal string DatabasePath { get; }

    internal string ConnectionString { get; }

    internal static async Task<ExactCrossRepositorySqliteDatabase> CreateAsync(
        CancellationToken cancellationToken)
    {
        string root = ExactCrossRepositoryCleanup.CreateUniqueRoot("sqlite");
        string path = Path.Combine(root, $"exact-cross-repository-{Guid.NewGuid():N}.db");
        var database = new ExactCrossRepositorySqliteDatabase(root, path);
        await using VulgataDbContext context = database.CreateContext();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        return database;
    }

    internal VulgataDbContext CreateContext(
        ExactCrossRepositoryAccessProbe? access = null,
        params IInterceptor[] interceptors)
    {
        DbContextOptionsBuilder<VulgataDbContext> options = new();
        options.UseSqlite(ConnectionString);
        if (access is not null)
        {
            options.AddInterceptors(new ExactCrossRepositoryDatabaseAccessInterceptor(access));
        }

        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new(options.Options);
    }

    public ValueTask DisposeAsync() => ExactCrossRepositoryCleanup.DeleteAsync(RootPath);
}

internal sealed class ExactCrossRepositoryTemporaryGitRepository : IAsyncDisposable
{
    private ExactCrossRepositoryTemporaryGitRepository(string rootPath)
    {
        RootPath = rootPath;
    }

    internal string RootPath { get; }

    internal string Commit { get; private set; } = string.Empty;

    internal static async Task<ExactCrossRepositoryTemporaryGitRepository> CreateAsync(
        string role,
        IReadOnlyDictionary<string, string> files,
        CancellationToken cancellationToken)
    {
        string root = ExactCrossRepositoryCleanup.CreateUniqueRoot($"git-{role}");
        var repository = new ExactCrossRepositoryTemporaryGitRepository(root);
        try
        {
            await repository.RunAsync(["init", "--quiet"], cancellationToken);
            await repository.RunAsync(
                ["config", "user.email", "vulgata-tests@example.invalid"],
                cancellationToken);
            await repository.RunAsync(
                ["config", "user.name", "Vulgata Tests"],
                cancellationToken);
            await repository.RunAsync(["config", "core.autocrlf", "false"], cancellationToken);
            foreach ((string relativePath, string content) in files)
            {
                repository.Write(relativePath, content);
            }

            repository.Commit = await repository.CommitAllAsync(cancellationToken);
            await repository.RunAsync(
                ["tag", "vulgata/August15th-sample", repository.Commit],
                cancellationToken);
            return repository;
        }
        catch
        {
            await repository.DisposeAsync();
            throw;
        }
    }

    internal void Write(string repositoryRelativePath, string content)
    {
        string path = ExactCrossRepositoryCleanup.ResolveRelativePath(
            RootPath,
            repositoryRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    internal async Task<string> CommitAllAsync(CancellationToken cancellationToken = default)
    {
        await RunAsync(["add", "--all"], cancellationToken);
        await RunAsync(
            ["commit", "--quiet", "--message", "exact cross-repository fixture"],
            cancellationToken);
        return (await RunAsync(
            ["rev-parse", "--verify", "HEAD^{commit}"],
            cancellationToken)).Trim();
    }

    internal Task<string> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default) =>
        RunGitAsync(RootPath, arguments, cancellationToken);

    public ValueTask DisposeAsync() => ExactCrossRepositoryCleanup.DeleteAsync(RootPath);

    private static async Task<string> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        startInfo.Environment["GIT_AUTHOR_DATE"] = "2026-08-03T00:00:00Z";
        startInfo.Environment["GIT_COMMITTER_DATE"] = "2026-08-03T00:00:00Z";
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("The disposable Git fixture could not start Git.");
        }

        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        _ = await error;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The disposable Git fixture command failed with exit code {process.ExitCode}.");
        }

        return await output;
    }
}

internal sealed class ExactCrossRepositoryLogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<ExactCrossRepositoryCapturedLog> _messages = [];

    internal IReadOnlyCollection<ExactCrossRepositoryCapturedLog> Messages => _messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _messages);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(
        string category,
        ConcurrentQueue<ExactCrossRepositoryCapturedLog> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(new(
                category,
                logLevel,
                eventId,
                formatter(state, exception),
                exception?.GetType().FullName,
                exception?.Message));
        }
    }
}

internal sealed record ExactCrossRepositoryCapturedLog(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    string? ExceptionType,
    string? ExceptionMessage);

internal static class ExactCrossRepositoryCleanup
{
    private const string RootFolderName = "vulgata-exact-cross-repository-tests";

    private static string BaseRoot => Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), RootFolderName));

    internal static string CreateUniqueRoot(string purpose)
    {
        string safePurpose = new(purpose
            .Where(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            .ToArray());
        if (safePurpose.Length == 0)
        {
            throw new ArgumentException("A temporary-root purpose is required.", nameof(purpose));
        }

        string root = Path.Combine(BaseRoot, $"{safePurpose}-{Guid.NewGuid():N}");
        EnsureOwnedRoot(root);
        Directory.CreateDirectory(root);
        return root;
    }

    internal static string ResolveRelativePath(string root, string repositoryRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativePath);
        if (Path.IsPathRooted(repositoryRelativePath) ||
            repositoryRelativePath.Replace('\\', '/').Split('/').Any(
                segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException(
                "Fixture paths must be safe Repository-relative paths.",
                nameof(repositoryRelativePath));
        }

        EnsureOwnedRoot(root);
        string fullPath = Path.GetFullPath(Path.Combine(
            root,
            repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The fixture path escapes its disposable Repository root.",
                nameof(repositoryRelativePath));
        }

        return fullPath;
    }

    internal static async ValueTask DeleteAsync(string root)
    {
        EnsureOwnedRoot(root);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            try
            {
                ClearReadOnlyAttributes(root);
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 3)
            {
                await Task.Delay(25 * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < 3)
            {
                await Task.Delay(25 * (attempt + 1));
            }
        }

        throw new IOException("A disposable exact Cross-Repository fixture root could not be removed.");
    }

    private static void ClearReadOnlyAttributes(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out string? current))
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(current))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
                }

                if ((attributes & FileAttributes.Directory) != 0 &&
                    (attributes & FileAttributes.ReparsePoint) == 0)
                {
                    pending.Push(entry);
                }
            }
        }
    }

    private static void EnsureOwnedRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        string fullPath = Path.GetFullPath(root);
        string basePrefix = BaseRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, BaseRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Cleanup is restricted to a unique exact Cross-Repository test root.");
        }
    }
}

internal static class ExactCrossRepositoryReflection
{
    internal static void SetId<T>(T value, Guid id)
    {
        ArgumentNullException.ThrowIfNull(value);
        PropertyInfo property = typeof(T).GetProperty("Id", BindingFlags.Instance | BindingFlags.Public) ??
            throw new InvalidOperationException("The fixture entity has no Id property.");
        property.SetValue(value, id);
    }
}

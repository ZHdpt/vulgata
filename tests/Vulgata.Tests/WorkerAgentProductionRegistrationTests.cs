using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Git;
using Vulgata.Shared;

namespace Vulgata.Tests;

public sealed class WorkerAgentProductionRegistrationTests
{
    private const string _plainSecret = "g6-plaintext-secret";
    private const string _rawDiagnostic =
        "provider stack trace at C:\\sensitive\\worker\\client.cs";
    private const string _unrelatedSource = "UNRELATED-SOURCE-MUST-NOT-CROSS";

    [Fact]
    public async Task ProductionComposition_NotReadyGate_PrecedesAuthorizationAndInnerDispatchAsync()
    {
        using var files = new TemporarySourceDirectory();
        Guid systemId = WorkerAgentTestFixtures.Id(4);
        Repository repository = Repository.Create(
            systemId,
            "Orders",
            "https://git.invalid/orders.git",
            null,
            null,
            DateTimeOffset.UtcNow);
        string committedSource = $"first{Environment.NewLine}committed-only{Environment.NewLine}last";
        string expectedSlice = "committed-only";
        string sourcePath = files.Write(
            WorkerAgentTestFixtures.RepositoryRelativePath,
            committedSource);
        WorkerAgentJobPacket packet = CreatePass1Packet(
            repository,
            sourcePath,
            expectedSlice);
        var repositorySource = new FakeRepositoryRepository(repository);
        var workingCopy = new FakeWorkingCopyService(
            GitWorkingCopyResult.Success(files.RootPath, packet.ProfiledRevision));
        var snapshot = new FixedSnapshotService(files.RootPath, packet);
        var provider = new LlmProvider(
            "system-worker-model",
            "http://127.0.0.1:42003/v1",
            "ciphertext-system",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var requestCapture = new LoopbackCaptureHandler(_rawDiagnostic);

        using var databaseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
        using WebApplicationFactory<Program> factory = databaseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRepositoryRepository>();
                services.AddScoped<IRepositoryRepository>(_ => repositorySource);
                services.RemoveAll<IGitWorkingCopyService>();
                services.AddScoped<IGitWorkingCopyService>(_ => workingCopy);
                services.RemoveAll<ICommittedRepositorySnapshotService>();
                services.AddScoped<ICommittedRepositorySnapshotService>(_ => snapshot);
                services.RemoveAll<ISystemLlmProviderOverrideRepository>();
                services.AddScoped<ISystemLlmProviderOverrideRepository>(_ =>
                    new FakeOverrideRepository(null));
                services.RemoveAll<ILlmProviderRepository>();
                services.AddScoped<ILlmProviderRepository>(_ =>
                    new FakeProviderRepository([provider]));
                services.RemoveAll<ISecretEncryptionService>();
                services.AddScoped<ISecretEncryptionService>(_ =>
                    new CapturingEncryptionService(_plainSecret));
                services.RemoveAll<HttpMessageHandler>();
                services.AddScoped<HttpMessageHandler>(_ => requestCapture);
            });
        });

        using IServiceScope serviceScope = factory.Services.CreateScope();
        IServiceProvider services = serviceScope.ServiceProvider;
        IWorkerAgentExecutionContextSource contextSource =
            services.GetRequiredService<IWorkerAgentExecutionContextSource>();
        Assert.Equal(
            "RevisionBoundWorkerExecutionContextSource",
            contextSource.GetType().Name);
        IWorkerAgentDispatcher dispatcher =
            services.GetRequiredService<IWorkerAgentDispatcher>();

        InvalidOperationException unauthorizedFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(
                packet,
                new RepositoryAccessScope([WorkerAgentTestFixtures.Id(999)]),
                CancellationToken.None));
        Assert.Equal("Analysis Runtime is NotReady.", unauthorizedFailure.Message);
        Assert.Equal(0, repositorySource.LookupCount);
        Assert.Equal(0, workingCopy.AcquireCount);
        Assert.Equal(0, snapshot.AcquireCount);
        Assert.Equal(0, requestCapture.CallCount);

        var authorizedScope = new RepositoryAccessScope([repository.Id]);
        InvalidOperationException authorizedFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(
            packet,
            authorizedScope,
            CancellationToken.None));

        Assert.Equal("Analysis Runtime is NotReady.", authorizedFailure.Message);
        Assert.Equal(0, repositorySource.LookupCount);
        Assert.Equal(0, workingCopy.AcquireCount);
        Assert.Equal(0, snapshot.AcquireCount);
        Assert.Equal(0, requestCapture.CallCount);
    }

    [Theory]
    [InlineData(true, 42002)]
    [InlineData(false, 42001)]
    public async Task ProductionComposition_SystemAssignment_ResolvesScopedSecureGraphAsync(
        bool useSystemOverride,
        int expectedPort)
    {
        Guid systemId = WorkerAgentTestFixtures.Id(4);
        var globalProvider = new LlmProvider(
            "global-worker-model",
            "http://127.0.0.1:42001/v1",
            "ciphertext-global",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var overrideProvider = new LlmProvider(
            "system-worker-model",
            "http://127.0.0.1:42002/v1",
            "ciphertext-system",
            ApiType.ChatCompletions,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        SystemLlmProviderOverride overrideEntry = SystemLlmProviderOverride.Create(
            systemId,
            overrideProvider.Id,
            AgentType.Worker,
            DateTimeOffset.UtcNow);
        var requestCapture = new LoopbackCaptureHandler(_rawDiagnostic);
        var logCapture = new CapturingLoggerProvider();

        using var databaseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
        using WebApplicationFactory<Program> factory = databaseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logCapture));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISystemLlmProviderOverrideRepository>();
                services.AddScoped<ISystemLlmProviderOverrideRepository>(_ =>
                    new FakeOverrideRepository(useSystemOverride ? overrideEntry : null));
                services.RemoveAll<ILlmProviderRepository>();
                services.AddScoped<ILlmProviderRepository>(_ =>
                    new FakeProviderRepository([globalProvider, overrideProvider]));
                services.RemoveAll<ISecretEncryptionService>();
                services.AddScoped<ISecretEncryptionService>(_ =>
                    new CapturingEncryptionService(_plainSecret));
                services.RemoveAll<HttpMessageHandler>();
                services.AddScoped<HttpMessageHandler>(_ => requestCapture);
            });
        });

        using IServiceScope scope = factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        SequentialScanRunner runner = services.GetRequiredService<SequentialScanRunner>();
        IWorkerAgentDispatcher dispatcher =
            services.GetRequiredService<IWorkerAgentDispatcher>();
        IWorkerAgentResultCommitter committer =
            services.GetRequiredService<IWorkerAgentResultCommitter>();
        var encryption = Assert.IsType<CapturingEncryptionService>(
            services.GetRequiredService<ISecretEncryptionService>());

        Assert.NotNull(runner);
        Assert.IsType<RuntimeGatedWorkerAgentDispatcher>(dispatcher);
        Assert.IsType<WorkerAgentResultCommitter>(committer);
        Assert.IsType<WorkerAgentProviderResolver>(
            services.GetRequiredService<WorkerAgentProviderResolver>());
        Assert.IsType<RevisionBoundWorkerContextHydrator>(
            services.GetRequiredService<RevisionBoundWorkerContextHydrator>());
        Assert.IsType<CommittedRepositorySnapshotService>(
            services.GetRequiredService<ICommittedRepositorySnapshotService>());
        Assert.IsType<ScanExecutionRepository>(
            services.GetRequiredService<IScanExecutionStore>());
        Assert.IsType<KnowledgeStoreRepository>(
            services.GetRequiredService<IKnowledgeStoreRepository>());
        Assert.Equal(0, encryption.DecryptCount);

        ResolvedWorkerAgentProvider assignment = await services
            .GetRequiredService<WorkerAgentProviderResolver>()
            .ResolveAsync(systemId);
        Assert.Equal(useSystemOverride, assignment.IsSystemOverride);
        Assert.Equal(expectedPort, assignment.Endpoint.Port);

        WorkerAgentJobPacket packet = CreatePass2Packet();
        InvalidOperationException notReadyFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(
            packet,
            new RepositoryAccessScope([packet.RepositoryId]),
            CancellationToken.None));
        Assert.Equal("Analysis Runtime is NotReady.", notReadyFailure.Message);
        Assert.Equal(0, requestCapture.CallCount);
        Assert.Equal(0, encryption.DecryptCount);
        Assert.DoesNotContain(
            logCapture.Messages,
            message => message.Category.StartsWith("Vulgata.Agents", StringComparison.Ordinal));

        VulgataDbContext dbContext = services.GetRequiredService<VulgataDbContext>();
        Assert.Empty(await dbContext.DocumentVersions.AsNoTracking().ToListAsync());
        Assert.Empty(await dbContext.SourceAnchors.AsNoTracking().ToListAsync());
        Assert.Empty(await dbContext.DocumentEdges.AsNoTracking().ToListAsync());
        Assert.Empty(await dbContext.IndexEntries.AsNoTracking().ToListAsync());

        using IServiceScope secondScope = factory.Services.CreateScope();
        Assert.NotSame(
            dispatcher,
            secondScope.ServiceProvider.GetRequiredService<IWorkerAgentDispatcher>());
        Assert.NotSame(
            committer,
            secondScope.ServiceProvider.GetRequiredService<IWorkerAgentResultCommitter>());
        Assert.NotSame(
            runner,
            secondScope.ServiceProvider.GetRequiredService<SequentialScanRunner>());
        Assert.NotSame(
            encryption,
            secondScope.ServiceProvider.GetRequiredService<ISecretEncryptionService>());
    }

    private static WorkerAgentJobPacket CreatePass2Packet()
    {
        PreparedPass2Flow flow = PreparedPass2Flow.Create(
            PreparedPass2FlowLimits.SupportedSchemaIdentity,
            WorkerAgentTestFixtures.Id(200),
            WorkerAgentTestFixtures.Id(201),
            WorkerAgentTestFixtures.Id(4),
            WorkerAgentTestFixtures.Id(3),
            [
                new(1, WorkerAgentTestFixtures.Id(1), WorkerAgentTestFixtures.Id(6), WorkerAgentTestFixtures.Id(101)),
                new(2, WorkerAgentTestFixtures.Id(10), WorkerAgentTestFixtures.Id(11), WorkerAgentTestFixtures.Id(102)),
            ],
            [
                new(1, WorkerAgentTestFixtures.Id(1), WorkerAgentTestFixtures.Id(6), "订单总额计算", "src/Orders/OrderService.cs#L12-L24"),
                new(2, WorkerAgentTestFixtures.Id(10), WorkerAgentTestFixtures.Id(11), "库存预留", "src/Inventory/InventoryService.cs#L30-L44"),
            ]);
        CurrentKnowledge[] documents =
        [
            CreatePass2CurrentDocument(flow.Members[0], 301, 401),
            CreatePass2CurrentDocument(flow.Members[1], 302, 402),
        ];
        return WorkerAgentJobPacket.Create(
            WorkerAgentTestFixtures.Id(501),
            WorkerAgentJobKind.Pass2BusinessLogicDocument,
            WorkerAgentTestFixtures.Id(2),
            WorkerAgentTestFixtures.Id(3),
            WorkerAgentTestFixtures.Id(4),
            WorkerAgentTestFixtures.Id(5),
            WorkerAgentTestFixtures.ProfiledRevision,
            WorkerAgentContractLimits.SupportedSchemaVersion,
            Pass2BusinessLogicJobPayload.Create(flow, documents));
    }

    private static WorkerAgentJobPacket CreatePass1Packet(
        Repository repository,
        string sourcePath,
        string sourceSlice)
    {
        byte[] fileBytes = File.ReadAllBytes(sourcePath);
        string fileIdentityHash = Hash(
            fileBytes.AsSpan(0, Math.Min(4_096, fileBytes.Length)));
        string sourceHash = Hash(Encoding.UTF8.GetBytes(sourceSlice));
        Pass1CodeLogicJobPayload payload = Pass1CodeLogicJobPayload.Create(
            WorkerAgentTestFixtures.Id(6),
            WorkerAgentTestFixtures.RepositoryRelativePath,
            2,
            2,
            "CalculateTotal",
            CodeUnitType.Method,
            "csharp",
            sourceHash,
            fileIdentityHash,
            [],
            [],
            []);
        return WorkerAgentJobPacket.Create(
            WorkerAgentTestFixtures.Id(1),
            WorkerAgentJobKind.Pass1CodeLogicDocument,
            WorkerAgentTestFixtures.Id(2),
            repository.Id,
            repository.SystemId!.Value,
            WorkerAgentTestFixtures.Id(5),
            WorkerAgentTestFixtures.ProfiledRevision,
            WorkerAgentContractLimits.SupportedSchemaVersion,
            payload);
    }

    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static CurrentKnowledge CreatePass2CurrentDocument(
        PreparedPass2FlowMember member,
        int documentVersionId,
        int anchorId) =>
        new(
            member.LogicalDocumentId,
            WorkerAgentTestFixtures.Id(documentVersionId),
            WorkerAgentTestFixtures.Id(3),
            DocumentType.CodeLogic,
            $"Code Logic {member.Order}",
            $"src/Demo/Flow{member.Order}.cs",
            "{}",
            $"Code Logic {member.Order}",
            [new CurrentSourceAnchor(
                WorkerAgentTestFixtures.Id(anchorId),
                member.CodeUnitId,
                $"src/Demo/Flow{member.Order}.cs",
                $"Flow{member.Order}.Execute",
                1,
                2,
                new string('a', 64))],
            [],
            [],
            new CurrentIndexEntry($"Summary {member.Order}", [], [], [], $"flow {member.Order}"),
            []);

    private sealed class FakeOverrideRepository(SystemLlmProviderOverride? overrideEntry)
        : ISystemLlmProviderOverrideRepository
    {
        public Task<SystemLlmProviderOverride?> GetBySystemAndAgentTypeAsync(
            Guid systemId,
            AgentType agentType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                overrideEntry is not null &&
                overrideEntry.SystemId == systemId &&
                overrideEntry.AgentType == agentType
                    ? overrideEntry
                    : null);

        public Task<IReadOnlyList<SystemLlmProviderOverride>> ListBySystemAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SystemLlmProviderOverride?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SystemLlmProviderOverride> AddAsync(
            SystemLlmProviderOverride value,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveAsync(
            Guid systemId,
            Guid overrideId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeProviderRepository(IReadOnlyList<LlmProvider> providers)
        : ILlmProviderRepository
    {
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(providers);

        public Task<LlmProvider?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(providers.SingleOrDefault(provider => provider.Id == id));

        public Task<bool> NameExistsAsync(
            string name,
            Guid? excludeProviderId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LlmProvider> AddAsync(
            LlmProvider provider,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Remove(LlmProvider provider) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRepositoryRepository(Repository repository)
        : IRepositoryRepository
    {
        public int LookupCount { get; private set; }

        public Task<Repository?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid repositoryId,
            CancellationToken cancellationToken = default)
        {
            LookupCount++;
            return Task.FromResult<Repository?>(
                repository.SystemId == systemId && repository.Id == repositoryId
                    ? repository
                    : null);
        }

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

    private sealed class FakeWorkingCopyService(GitWorkingCopyResult result)
        : IGitWorkingCopyService
    {
        public int AcquireCount { get; private set; }
        public Repository? Repository { get; private set; }
        public Guid ScanRunId { get; private set; }

        public Task<GitWorkingCopyResult> AcquireAsync(
            Repository repository,
            Guid scanRunId,
            CancellationToken cancellationToken = default)
        {
            AcquireCount++;
            Repository = repository;
            ScanRunId = scanRunId;
            return Task.FromResult(result);
        }
    }

    private sealed class FixedSnapshotService(string rootPath, WorkerAgentJobPacket packet)
        : ICommittedRepositorySnapshotService
    {
        public int AcquireCount { get; private set; }

        public Task<ICommittedRepositorySnapshotLease> AcquireAsync(
            Repository repository,
            Guid scanRunId,
            GitWorkingCopyResult workingCopy,
            CancellationToken cancellationToken = default)
        {
            AcquireCount++;
            return Task.FromResult<ICommittedRepositorySnapshotLease>(
                new FixedSnapshotLease(rootPath, packet));
        }
    }

    private sealed class FixedSnapshotLease(string rootPath, WorkerAgentJobPacket packet)
        : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId => packet.RepositoryId;
        public Guid ScanRunId => packet.ScanRunId;
        public string ProfiledRevision => packet.ProfiledRevision;
        public string RootPath => rootPath;
        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CapturingEncryptionService(string plaintext)
        : ISecretEncryptionService
    {
        public int DecryptCount { get; private set; }
        public string? Purpose { get; private set; }

        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default)
        {
            Purpose = purpose;
            DecryptCount++;
            return Task.FromResult(plaintext);
        }
    }

    private sealed class LoopbackCaptureHandler(string rawDiagnostic) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(rawDiagnostic, Encoding.UTF8, "text/plain"),
            };
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<CapturedLog> _messages = [];

        public IReadOnlyCollection<CapturedLog> Messages => _messages.ToArray();

        public ILogger CreateLogger(string categoryName) =>
            new CapturingLogger(categoryName, _messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(
            string category,
            ConcurrentQueue<CapturedLog> messages) : ILogger
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
                messages.Enqueue(new(category, formatter(state, exception)));
                if (exception is not null)
                {
                    messages.Enqueue(new(category, exception.ToString()));
                }
            }
        }
    }

    private sealed class TemporarySourceDirectory : IDisposable
    {
        public TemporarySourceDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "vulgata-worker-agent-g6",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string Write(string relativePath, string content)
        {
            string fullPath = Path.Combine(
                RootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content, new UTF8Encoding(false));
            return fullPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed record CapturedLog(string Category, string Text);
}

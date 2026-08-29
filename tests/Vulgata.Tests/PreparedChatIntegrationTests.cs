using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Agents.ChatExecution;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Shared.Chat;
using Vulgata.Shared.Validators.Chat;
using Vulgata.Web.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests;

[Collection(DataProtectionProductionCompositionCollection.Name)]
public sealed class PreparedChatIntegrationTests
{
    private const string _applicationName = "Vulgata.Story115.ReleaseGate";

    [Fact]
    public async Task AuthenticatedBusinessInitialAndFollowUp_TraversesEachAcceptedBoundaryOnceAsync()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository consumer = system.AddRepository(
            "账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository provider = system.AddRepository(
            "渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var ports = new InteractionPorts();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new InteractionAuthenticationStateProvider("story125-business-user"),
            new InteractionSystemRepository(system),
            new InteractionRepositoryRepository(consumer, provider),
            ports.Authorization,
            ports.Advisory,
            ports.Retriever,
            candidateExtractor,
            ports.Dispatcher,
            ports.Currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new InteractionScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            completionSummaryRecorder: ports.CompletionRecorder,
            publicationOwner: ports.PublicationOwner);

        PreparedChatResult initial = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [consumer.Id, provider.Id],
            "客户如何完成付款？");
        PreparedChatResult followUp = await coordinator.AskAsync(
            ChatMode.Business,
            system.Id,
            [consumer.Id, provider.Id],
            "后续校验由谁负责？");

        Assert.Equal(PreparedChatResultKind.Succeeded, initial.Kind);
        Assert.Equal(PreparedChatResultKind.Succeeded, followUp.Kind);
        Assert.Equal(2, ports.Retriever.LexicalProjectionCount);
        Assert.Equal(2, ports.Retriever.RelationshipProjectionCount);
        Assert.Equal(2, ports.Retriever.PacketBuildCount);
        Assert.Equal(1, ports.Retriever.FollowUpConversationRefetchCount);
        Assert.Equal(2, ports.Dispatcher.StrictRawProviderAttemptCount);
        Assert.Equal(2, ports.Currentness.Count);
        Assert.Equal(2, ports.Authorization.FinalAuthorizationCount);
        Assert.Equal(2, ports.PublicationOwner.CommitCount);
        Assert.Equal(
            ["business-initial", "business-follow-up"],
            ports.Retriever.ConversationShapes);
    }

    [Fact]
    public async Task AuthenticatedItInitialAndFollowUp_TraversesEachAcceptedBoundaryOnceAsync()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository consumer = system.AddRepository(
            "账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository provider = system.AddRepository(
            "渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var ports = new InteractionPorts();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new InteractionAuthenticationStateProvider("story125-it-user"),
            new InteractionSystemRepository(system),
            new InteractionRepositoryRepository(consumer, provider),
            ports.Authorization,
            ports.Advisory,
            ports.Retriever,
            candidateExtractor,
            ports.Dispatcher,
            ports.Currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new InteractionScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            completionSummaryRecorder: ports.CompletionRecorder,
            publicationOwner: ports.PublicationOwner);

        PreparedChatResult initial = await coordinator.AskAsync(
            ChatMode.It,
            system.Id,
            [consumer.Id, provider.Id],
            "付款接口如何实现？");
        PreparedChatResult followUp = await coordinator.AskAsync(
            ChatMode.It,
            system.Id,
            [consumer.Id, provider.Id],
            "接口失败时如何重试？");

        Assert.Equal(PreparedChatResultKind.Succeeded, initial.Kind);
        Assert.Equal(PreparedChatResultKind.Succeeded, followUp.Kind);
        Assert.Equal(2, ports.Retriever.LexicalProjectionCount);
        Assert.Equal(2, ports.Retriever.RelationshipProjectionCount);
        Assert.Equal(2, ports.Retriever.PacketBuildCount);
        Assert.Equal(1, ports.Retriever.FollowUpConversationRefetchCount);
        Assert.Equal(2, ports.Dispatcher.StrictRawProviderAttemptCount);
        Assert.Equal(2, ports.Currentness.Count);
        Assert.Equal(2, ports.Authorization.FinalAuthorizationCount);
        Assert.Equal(2, ports.PublicationOwner.CommitCount);
        Assert.Equal(
            ["it-initial", "it-follow-up"],
            ports.Retriever.ConversationShapes);
    }

    [Fact]
    public async Task AuthenticatedItFollowUp_InvokesConversationRefetchPortExactlyOnceAsync()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository consumer = system.AddRepository(
            "账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository provider = system.AddRepository(
            "渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var ports = new InteractionPorts();
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new InteractionAuthenticationStateProvider("story125-it-user"),
            new InteractionSystemRepository(system),
            new InteractionRepositoryRepository(consumer, provider),
            ports.Authorization,
            ports.Advisory,
            ports.Retriever,
            candidateExtractor,
            ports.Dispatcher,
            ports.Currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new InteractionScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            completionSummaryRecorder: ports.CompletionRecorder,
            publicationOwner: ports.PublicationOwner);

        _ = await coordinator.AskAsync(
            ChatMode.It,
            system.Id,
            [consumer.Id, provider.Id],
            "付款接口如何实现？");
        PreparedChatResult followUp = await coordinator.AskAsync(
            ChatMode.It,
            system.Id,
            [consumer.Id, provider.Id],
            "接口失败时如何重试？");

        Assert.Equal(PreparedChatResultKind.Succeeded, followUp.Kind);
        Assert.Equal(1, ports.Retriever.FollowUpConversationRefetchCount);
        Assert.Equal(2, ports.PublicationOwner.CommitCount);
    }

    [Fact]
    public async Task AuthenticatedItFlow_WhenLexicalPrimaryIsReplaced_IsRejectedAsync()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository consumer = system.AddRepository(
            "账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository provider = system.AddRepository(
            "渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var ports = new InteractionPorts();
        ports.Retriever.ReplaceLexicalPrimary = true;
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new InteractionAuthenticationStateProvider("story125-it-user"),
            new InteractionSystemRepository(system),
            new InteractionRepositoryRepository(consumer, provider),
            ports.Authorization,
            ports.Advisory,
            ports.Retriever,
            candidateExtractor,
            ports.Dispatcher,
            ports.Currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new InteractionScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            completionSummaryRecorder: ports.CompletionRecorder,
            publicationOwner: ports.PublicationOwner);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.It,
            system.Id,
            [consumer.Id, provider.Id],
            "付款接口如何实现？");

        Assert.NotEqual(PreparedChatResultKind.Succeeded, result.Kind);
        Assert.Equal(0, ports.Dispatcher.StrictRawProviderAttemptCount);
        Assert.Equal(0, ports.PublicationOwner.CommitCount);
    }

    [Fact]
    public async Task AuthenticatedItFlow_WhenRelationshipExpansionIsSkipped_IsRejectedAsync()
    {
        var system = new SystemEntity("支付系统", null, null, DateTimeOffset.UtcNow);
        Repository consumer = system.AddRepository(
            "账户仓库", "https://example.test/account.git", null, null, DateTimeOffset.UtcNow);
        Repository provider = system.AddRepository(
            "渠道仓库", "https://example.test/channel.git", null, null, DateTimeOffset.UtcNow);
        var ports = new InteractionPorts();
        ports.Retriever.SkipRelationshipProjection = true;
        var candidateExtractor = new ChatCandidateExtractor();
        var coordinator = new PreparedChatCoordinator(
            new InteractionAuthenticationStateProvider("story125-it-user"),
            new InteractionSystemRepository(system),
            new InteractionRepositoryRepository(consumer, provider),
            ports.Authorization,
            ports.Advisory,
            ports.Retriever,
            candidateExtractor,
            ports.Dispatcher,
            ports.Currentness,
            TimeProvider.System,
            new ChatAgentResultValidator(candidateExtractor),
            new InteractionScopeResolver(),
            new PreparedChatQuestionRequestValidator(),
            completionSummaryRecorder: ports.CompletionRecorder,
            publicationOwner: ports.PublicationOwner);

        PreparedChatResult result = await coordinator.AskAsync(
            ChatMode.It,
            system.Id,
            [consumer.Id, provider.Id],
            "付款接口如何实现？");

        Assert.NotEqual(PreparedChatResultKind.Succeeded, result.Kind);
        Assert.Equal(0, ports.Dispatcher.StrictRawProviderAttemptCount);
        Assert.Equal(0, ports.PublicationOwner.CommitCount);
    }

    [Fact]
    public void ProductionComposition_ChatServices_ResolveAsScopedRelationalGraph()
    {
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        using IServiceScope firstScope = factory.Services.CreateScope();
        using IServiceScope secondScope = factory.Services.CreateScope();

        Assert.True(firstScope.ServiceProvider
            .GetRequiredService<VulgataDbContext>()
            .Database
            .IsRelational());

        Type[] serviceTypes =
        [
            typeof(IChatEvidenceRetriever),
            typeof(IChatEvidenceCurrentnessValidator),
            typeof(ChatCandidateExtractor),
            typeof(ChatAgentProviderResolver),
            typeof(IChatAgentDispatcher),
            typeof(ChatAgentResultValidator),
            typeof(PreparedChatCoordinator),
        ];
        object?[] firstInstances = serviceTypes
            .Select(firstScope.ServiceProvider.GetService)
            .ToArray();
        string[] missingRegistrations = serviceTypes
            .Zip(firstInstances)
            .Where(pair => pair.Second is null)
            .Select(pair => pair.First.FullName!)
            .ToArray();

        Assert.True(
            missingRegistrations.Length == 0,
            $"Missing production registrations: {string.Join(", ", missingRegistrations)}");

        Assert.IsType<ChatEvidenceRetriever>(firstInstances[0]);
        Assert.IsType<ChatEvidenceCurrentnessValidator>(firstInstances[1]);
        Assert.IsType<ChatCandidateExtractor>(firstInstances[2]);
        Assert.IsType<ChatAgentProviderResolver>(firstInstances[3]);
        Assert.IsType<ChatAgentDispatcher>(firstInstances[4]);
        Assert.IsType<ChatAgentResultValidator>(firstInstances[5]);
        Assert.IsType<PreparedChatCoordinator>(firstInstances[6]);

        for (int index = 0; index < serviceTypes.Length; index++)
        {
            Assert.Same(
                firstInstances[index],
                firstScope.ServiceProvider.GetRequiredService(serviceTypes[index]));
            Assert.NotSame(
                firstInstances[index],
                secondScope.ServiceProvider.GetRequiredService(serviceTypes[index]));
        }

        Assert.IsType<LlmProviderRepository>(
            firstScope.ServiceProvider.GetRequiredService<ILlmProviderRepository>());
        Assert.IsType<SystemLlmProviderOverrideRepository>(
            firstScope.ServiceProvider.GetRequiredService<ISystemLlmProviderOverrideRepository>());
        Assert.IsType<SecretEncryptionService>(
            firstScope.ServiceProvider.GetRequiredService<ISecretEncryptionService>());
        Assert.IsType<SocketsHttpHandler>(
            firstScope.ServiceProvider.GetRequiredService<HttpMessageHandler>());

        IRepositoryAuthorizationVerifier firstAuthorization = firstScope.ServiceProvider
            .GetRequiredService<IRepositoryAuthorizationVerifier>();
        Assert.Same(
            firstAuthorization,
            firstScope.ServiceProvider.GetRequiredService<IRepositoryAuthorizationVerifier>());
        Assert.NotSame(
            firstAuthorization,
            secondScope.ServiceProvider.GetRequiredService<IRepositoryAuthorizationVerifier>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>(),
            secondScope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>());
    }

    [Fact]
    public async Task ProductionComposition_PairedDataProtectionConfiguration_SharesPersistedProtectedSecretsAsync()
    {
        using var keyRing = new TemporaryKeyRingDirectory();
        const string plaintext = "story-11-5-provider-secret";

        string cipher;
        using (new DataProtectionEnvironment(keyRing.Path, _applicationName))
        using (var provisioner = new LoginLogoutTests.CustomWebApplicationFactory())
        {
            using IServiceScope scope = provisioner.Services.CreateScope();
            Assert.Equal(
                _applicationName,
                scope.ServiceProvider
                    .GetRequiredService<IOptions<DataProtectionOptions>>()
                    .Value.ApplicationDiscriminator);
            var repository = Assert.IsType<FileSystemXmlRepository>(
                scope.ServiceProvider
                    .GetRequiredService<IOptions<KeyManagementOptions>>()
                    .Value.XmlRepository);
            Assert.Equal(
                new DirectoryInfo(keyRing.Path).FullName,
                repository.Directory.FullName);
            cipher = await scope.ServiceProvider
                .GetRequiredService<ISecretEncryptionService>()
                .EncryptAsync(SecretPurposes.LlmApiKey, plaintext);
        }

        string[] persistedKeyFiles = Directory
            .EnumerateFiles(keyRing.Path, "key-*.xml", SearchOption.TopDirectoryOnly)
            .ToArray();
        Assert.NotEmpty(persistedKeyFiles);
        Assert.DoesNotContain(plaintext, cipher, StringComparison.Ordinal);
        Assert.All(
            persistedKeyFiles,
            keyFile => Assert.DoesNotContain(
                plaintext,
                File.ReadAllText(keyFile),
                StringComparison.Ordinal));

        using (new DataProtectionEnvironment(keyRing.Path, _applicationName))
        using (var runtime = new LoginLogoutTests.CustomWebApplicationFactory())
        {
            using IServiceScope scope = runtime.Services.CreateScope();
            string decrypted = await scope.ServiceProvider
                .GetRequiredService<ISecretEncryptionService>()
                .DecryptAsync(SecretPurposes.LlmApiKey, cipher);
            Assert.Equal(plaintext, decrypted);
        }

        using var isolatedEnvironment = new DataProtectionEnvironment(
            keyRing.Path,
            $"{_applicationName}.Other");
        using var isolatedRuntime = new LoginLogoutTests.CustomWebApplicationFactory();
        using IServiceScope isolatedScope = isolatedRuntime.Services.CreateScope();
        _ = await Assert.ThrowsAsync<CryptographicException>(() =>
            isolatedScope.ServiceProvider
                .GetRequiredService<ISecretEncryptionService>()
                .DecryptAsync(SecretPurposes.LlmApiKey, cipher));
    }

    [Fact]
    public async Task ProductionComposition_KeyRingPathWithoutApplicationName_PreservesDefaultsAsync()
    {
        using var configuredPath = new TemporaryKeyRingDirectory();
        using var environment = new DataProtectionEnvironment(
            configuredPath.Path,
            applicationName: null);
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        using IServiceScope scope = factory.Services.CreateScope();
        ISecretEncryptionService encryption = scope.ServiceProvider
            .GetRequiredService<ISecretEncryptionService>();

        string cipher = await encryption.EncryptAsync(
            SecretPurposes.LlmApiKey,
            "default-key-ring-secret");

        Assert.Equal(
            "default-key-ring-secret",
            await encryption.DecryptAsync(SecretPurposes.LlmApiKey, cipher));
        Assert.Empty(Directory.EnumerateFileSystemEntries(configuredPath.Path));
    }

    [Fact]
    public async Task ProductionComposition_ApplicationNameWithoutKeyRingPath_PreservesDefaultsAsync()
    {
        using var environment = new DataProtectionEnvironment(
            keyRingPath: null,
            _applicationName);
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        using IServiceScope scope = factory.Services.CreateScope();
        ISecretEncryptionService encryption = scope.ServiceProvider
            .GetRequiredService<ISecretEncryptionService>();

        string cipher = await encryption.EncryptAsync(
            SecretPurposes.LlmApiKey,
            "default-application-secret");

        Assert.Equal(
            "default-application-secret",
            await encryption.DecryptAsync(SecretPurposes.LlmApiKey, cipher));
        Assert.NotEqual(
            _applicationName,
            scope.ServiceProvider
                .GetRequiredService<IOptions<DataProtectionOptions>>()
                .Value.ApplicationDiscriminator);
    }

    private sealed class DataProtectionEnvironment : IDisposable
    {
        private const string _applicationNameVariable = "DataProtection__ApplicationName";
        private const string _keyRingPathVariable = "DataProtection__KeyRingPath";
        private readonly string? _originalApplicationName;
        private readonly string? _originalKeyRingPath;

        public DataProtectionEnvironment(string? keyRingPath, string? applicationName)
        {
            _originalKeyRingPath = Environment.GetEnvironmentVariable(_keyRingPathVariable);
            _originalApplicationName = Environment.GetEnvironmentVariable(_applicationNameVariable);
            Environment.SetEnvironmentVariable(_keyRingPathVariable, keyRingPath);
            Environment.SetEnvironmentVariable(_applicationNameVariable, applicationName);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_keyRingPathVariable, _originalKeyRingPath);
            Environment.SetEnvironmentVariable(_applicationNameVariable, _originalApplicationName);
        }
    }

    private sealed class TemporaryKeyRingDirectory : IDisposable
    {
        public TemporaryKeyRingDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "vulgata-story-11-5-dpkeys",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class InteractionPorts
    {
        public InteractionAuthorization Authorization { get; } = new();
        public InteractionAdvisoryReader Advisory { get; } = new();
        public InteractionRetriever Retriever { get; } = new();
        public InteractionDispatcher Dispatcher { get; } = new();
        public InteractionCurrentness Currentness { get; } = new();
        public ChatStreamRuntimeObservationRecorder CompletionRecorder { get; } = new();
        public InteractionPublicationOwner PublicationOwner { get; } = new();
    }

    private sealed class InteractionAuthenticationStateProvider(string userId) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            ClaimsPrincipal principal = new(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "Story125-B-INT"));
            return Task.FromResult(new AuthenticationState(principal));
        }
    }

    private sealed class InteractionSystemRepository(SystemEntity system) : ISystemRepository
    {
        public Task<SystemEntity?> GetVisibleByIdAsync(
            Guid id,
            SystemOwnerFilter filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemEntity?>(id == system.Id ? system : null);

        public Task<IReadOnlyList<SystemEntity>> ListVisibleAsync(
            SystemOwnerFilter filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SystemEntity>>([system]);

        public Task<SystemEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> NameExistsAsync(string name, Guid? excludeSystemId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<SystemEntity> AddAsync(SystemEntity value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<SystemDeleteResult> DeleteIfNoDependenciesAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<SystemDependencyCounts> GetDependencyCountsAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<SystemOwnerAssignmentWriteResult> AssignOwnerAsync(Guid systemId, string userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> RemoveOwnerAsync(Guid systemId, string userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<SystemOwnerAssignmentSummary>> ListOwnersAsync(Guid systemId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<int> CountOwnerAssignmentsByUserAsync(string userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InteractionRepositoryRepository(params Repository[] repositories) : IRepositoryRepository
    {
        public Task<Repository?> GetBySystemAndIdAsync(
            Guid systemId,
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Repository?>(repositories.SingleOrDefault(repository =>
                repository.SystemId == systemId && repository.Id == repositoryId));

        public Task<IReadOnlyList<Repository>> ListBySystemAsync(
            Guid systemId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Repository>>(
                repositories.Where(repository => repository.SystemId == systemId).ToArray());

        public Task<Repository?> GetByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Repository>> ListStandaloneAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Repository?> GetStandaloneByIdAsync(Guid repositoryId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> NameExistsAsync(Guid systemId, string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> StandaloneNameExistsAsync(string name, Guid? excludeRepositoryId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Repository> AddAsync(Repository value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public void Remove(Repository value) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InteractionAuthorization : IRepositoryAuthorizationVerifier
    {
        public int CallCount { get; private set; }
        public int FinalAuthorizationCount { get; private set; }

        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (CallCount % 2 == 0)
            {
                FinalAuthorizationCount++;
            }

            return ValueTask.FromResult(true);
        }
    }

    private sealed class InteractionAdvisoryReader : IChatAdvisoryContextReader
    {
        public Task<ChatAdvisoryContextReadResult> ReadAsync(
            AuthorizedChatScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ChatAdvisoryContextReadResult.Succeeded(
                new ChatAdvisoryContextSnapshot([], string.Empty, [], 0, new string('0', 64))));
        }
    }

    private sealed class InteractionScopeResolver : IPreparedChatScopeResolver
    {
        public Task<PreparedChatScopeBinding?> ResolveAsync(
            Guid systemId,
            IReadOnlyCollection<Guid> repositoryIds,
            RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default)
        {
            Guid[] ids = repositoryIds.Order().ToArray();
            return Task.FromResult<PreparedChatScopeBinding?>(
                ids.Length == 2 ? new PreparedChatScopeBinding(systemId, ids[0], ids[1]) : null);
        }
    }

    private sealed class InteractionRetriever : IChatEvidenceRetriever
    {
        public int LexicalProjectionCount { get; private set; }
        public int RelationshipProjectionCount { get; private set; }
        public int PacketBuildCount { get; private set; }
        public int FollowUpConversationRefetchCount { get; private set; }
        public bool ReplaceLexicalPrimary { get; set; }
        public bool SkipRelationshipProjection { get; set; }
        public List<string> ConversationShapes { get; } = [];

        public Task<ChatEvidenceResult> RetrieveAsync(
            ChatRetrievalRequest request,
            RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LexicalProjectionCount++;
            RelationshipProjectionCount++;
            PacketBuildCount++;
            if (request.Conversation.Turns.Count == 0)
            {
                ConversationShapes.Add(request.Mode == ChatMode.It ? "it-initial" : "business-initial");
            }
            else
            {
                FollowUpConversationRefetchCount++;

                ConversationShapes.Add(request.Mode == ChatMode.It ? "it-follow-up" : "business-follow-up");
            }

            EvidenceDocument document = new(
                "d1",
                Guid.Parse("00000000-0000-0000-0000-000000000101"),
                Guid.Parse("00000000-0000-0000-0000-000000000201"),
                request.RepositoryIds[0],
                request.Mode == ChatMode.It ? DocumentType.CodeLogic : DocumentType.BusinessLogic,
                "付款流程",
                "revision-1");
            EvidenceDocument supportingDocument = new(
                "d2",
                Guid.Parse("00000000-0000-0000-0000-000000000102"),
                Guid.Parse("00000000-0000-0000-0000-000000000202"),
                request.RepositoryIds[1],
                DocumentType.CodeLogic,
                "渠道服务",
                "revision-1");
            EvidencePacket packet = EvidencePacket.Create(
                request.RequestId,
                request.Mode,
                request.SystemId,
                request.RepositoryIds,
                SkipRelationshipProjection ? [document, supportingDocument] : [document],
                [new EvidenceSection(
                    "s1",
                    "d1",
                    Guid.Parse("00000000-0000-0000-0000-000000000301"),
                    1,
                    request.Mode == ChatMode.It
                        ? DocumentSectionKind.TechnicalDetail
                        : DocumentSectionKind.Overview,
                    "付款概览",
                    ReplaceLexicalPrimary
                        ? "无关替换主投影。"
                        : request.Mode == ChatMode.It
                            ? "付款接口完成参数校验并调用渠道服务。"
                    : "客户完成付款后，系统完成校验。",
                    ["a1"],
                    SkipRelationshipProjection ? ["d2"] : [])],
                [new EvidenceAnchor(
                    "a1",
                    Guid.Parse("00000000-0000-0000-0000-000000000401"),
                    "d1",
                    document.DocumentVersionId,
                    document.RepositoryId,
                    "src/Payment.cs",
                    1,
                    2,
                    new string('a', 64))],
                [],
                [],
                advisoryContext: request.AdvisoryContext.ComposedText,
                conversation: request.Conversation,
                advisoryContextAuthorityFingerprint: request.AdvisoryContext.AuthorityFingerprint,
                primaryDocumentHandles: ["d1"],
                primarySectionHandles: ["s1"],
                expansionSeedDocumentHandles: ["d1"]);
            return Task.FromResult(ChatEvidenceResult.Found(packet));
        }
    }

    private sealed class InteractionDispatcher : IChatAgentDispatcher
    {
        public int StrictRawProviderAttemptCount { get; private set; }

        public Task<ChatSynthesisOutcome> GenerateAsync(
            ChatSynthesisRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StrictRawProviderAttemptCount++;
            ChatSynthesisResult result = ChatSynthesisResult.Create(
                request.RequestId,
                request.Mode,
                request.EvidenceFingerprint,
                [new ChatSynthesisSelection("k1", 1)]);
            string raw = JsonSerializer.Serialize(new
            {
                schemaVersion = ChatSynthesisResult.SupportedSchemaVersion,
                requestId = request.RequestId,
                mode = request.Mode == ChatMode.Business ? "business" : "it",
                evidenceFingerprint = request.EvidenceFingerprint,
                answerability = "supported",
                selections = new[] { new { candidateHandle = "k1", order = 1 } },
            });
            return Task.FromResult(ChatSynthesisOutcome.Generated(result, raw));
        }
    }

    private sealed class InteractionCurrentness : IChatEvidenceCurrentnessValidator
    {
        public int Count { get; private set; }

        public Task<ChatCurrentnessValidationOutcome> ValidateCurrentAsync(
            ChatCurrentnessValidationRequest request,
            RepositoryAccessScope accessScope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Count++;
            return Task.FromResult(ChatCurrentnessValidationOutcome.Current);
        }
    }

    private sealed class InteractionPublicationOwner : IPreparedChatPublicationOwner
    {
        private readonly PreparedChatPublicationOwner _inner = new(TimeProvider.System);

        public int CommitCount { get; private set; }

        public AcceptedPublicationCommit Commit(
            PreparedChatPublicationCandidate candidate,
            CancellationToken cancellationToken)
        {
            CommitCount++;
            return _inner.Commit(candidate, cancellationToken);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DataProtectionProductionCompositionCollection
{
    public const string Name = "Data Protection production composition";
}

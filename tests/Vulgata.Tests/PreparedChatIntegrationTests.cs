using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

[Collection(DataProtectionProductionCompositionCollection.Name)]
public sealed class PreparedChatIntegrationTests
{
    private const string _applicationName = "Vulgata.Story115.ReleaseGate";

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
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DataProtectionProductionCompositionCollection
{
    public const string Name = "Data Protection production composition";
}

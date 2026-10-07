using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Services.OctoberChat;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberChatProviderBindingTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IsCurrentAsync_RealRepositoryExternalRowRotation_DetectsPersistedChangeDespiteEarlierTrackedReadAsync(bool rotateModel)
    {
        await using var identity = await B0bIdentityFixture.CreateAsync();
        var factory = identity.Services.GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        var row = Row();
        await using (var seed = await factory.CreateDbContextAsync())
        {
            seed.LlmProviders.Add(row);
            await seed.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        services.AddScoped<VulgataDbContext>(_ => factory.CreateDbContext());
        services.AddScoped<ILlmProviderRepository, LlmProviderRepository>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var secrets = new Secrets();
        var binding = new OctoberChatProviderBindingService(row.Id, provider.GetRequiredService<IServiceScopeFactory>(), secrets);
        var snapshot = Assert.IsType<OctoberChatProviderSnapshot>(await binding.FreezeAsync());
        await using (var independent = await factory.CreateDbContextAsync())
        {
            var changed = await independent.LlmProviders.SingleAsync(provider => provider.Id == row.Id);
            changed.UpdateDetails(rotateModel ? "rotated-model" : changed.Name, changed.BaseEndpointUrl,
                rotateModel ? changed.EncryptedApiKey : "rotated-protected-key", changed.SupportedApiTypes, changed.DefaultAgentType, changed.UpdatedAt.AddSeconds(1));
            await independent.SaveChangesAsync();
        }
        Assert.False(await binding.IsCurrentAsync(snapshot));
        Assert.Null(await binding.DecryptCurrentKeyAsync(snapshot));
        Assert.Empty(secrets.Decrypted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DecryptCurrentKeyAsync_PublicScopedBinding_DetectsExternalRotationDuringDecryptionAsync()
    {
        await using var identity = await B0bIdentityFixture.CreateAsync();
        var factory = identity.Services.GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        var row = Row();
        await using (var seed = await factory.CreateDbContextAsync())
        {
            seed.LlmProviders.Add(row);
            await seed.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        var resolvedContexts = new List<VulgataDbContext>();
        services.AddScoped<VulgataDbContext>(_ =>
        {
            var context = factory.CreateDbContext();
            resolvedContexts.Add(context);
            return context;
        });
        services.AddScoped<ILlmProviderRepository, LlmProviderRepository>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var secrets = new Secrets
        {
            BeforeReturnAsync = async () =>
            {
                await using var independent = await factory.CreateDbContextAsync();
                var changed = await independent.LlmProviders.SingleAsync(candidate => candidate.Id == row.Id);
                changed.UpdateDetails(changed.Name, changed.BaseEndpointUrl, "rotated-during-decryption", changed.SupportedApiTypes,
                    changed.DefaultAgentType, changed.UpdatedAt.AddSeconds(1));
                await independent.SaveChangesAsync();
            }
        };
        var binding = new OctoberChatProviderBindingService(row.Id, provider.GetRequiredService<IServiceScopeFactory>(), secrets);
        var snapshot = Assert.IsType<OctoberChatProviderSnapshot>(await binding.FreezeAsync());
        Assert.Null(await binding.DecryptCurrentKeyAsync(snapshot));
        Assert.Equal((SecretPurposes.LlmApiKey, row.EncryptedApiKey), Assert.Single(secrets.Decrypted));
        Assert.True(resolvedContexts.Count >= 3, "Freeze, pre-decrypt and post-decrypt must each read through a fresh scope.");
        Assert.Equal(resolvedContexts.Count, resolvedContexts.Distinct().Count());
        Assert.All(resolvedContexts, context => Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker.Entries().ToList()));
        Assert.False(await binding.IsCurrentAsync(snapshot));
        Assert.Null(await binding.FreezeAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FreezeAsync_ExplicitRowOnly_ProducesOpaqueAdmissionWithoutDecryptingAsync()
    {
        var row = Row();
        var repository = new ProviderRepository(row);
        var secrets = new Secrets();
        var service = new OctoberChatProviderBindingService(row.Id, repository, secrets);
        var snapshot = Assert.IsType<OctoberChatProviderSnapshot>(await service.FreezeAsync());
        Assert.Equal(row.Id, snapshot.ProviderId);
        Assert.Equal(row.Name, snapshot.Model); // Matches existing ChatAgentProviderResolver convention.
        Assert.Equal(row.BaseEndpointUrl, snapshot.Endpoint.ToString().TrimEnd('/'));
        Assert.Equal(ApiType.ChatCompletions, snapshot.ApiType);
        Assert.InRange(Encoding.UTF8.GetByteCount(snapshot.Revision), 1, 256);
        Assert.All(repository.Requested, id => Assert.Equal(row.Id, id));
        Assert.Equal(0, repository.ListCalls);
        Assert.Empty(secrets.Decrypted);
        Assert.Same(snapshot, await service.FreezeAsync());
        Assert.True(await service.IsCurrentAsync(snapshot));
        var serialized = JsonSerializer.Serialize(snapshot) + snapshot;
        Assert.DoesNotContain(row.EncryptedApiKey, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(secrets.Plaintext, serialized, StringComparison.Ordinal);
        Assert.All(typeof(OctoberChatProviderSnapshot).GetProperties(), property => Assert.Null(property.SetMethod));
        var other = new OctoberChatProviderBindingService(row.Id, repository, secrets);
        var otherSnapshot = Assert.IsType<OctoberChatProviderSnapshot>(await other.FreezeAsync());
        Assert.NotEqual(snapshot.Revision, otherSnapshot.Revision);
        Assert.False(await other.IsCurrentAsync(snapshot));
        Assert.Null(await other.DecryptCurrentKeyAsync(snapshot));
        Assert.Empty(secrets.Decrypted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("Name")]
    [InlineData("NormalizedName")]
    [InlineData("BaseEndpointUrl")]
    [InlineData("EncryptedApiKey")]
    [InlineData("SupportedApiTypes")]
    [InlineData("DefaultAgentType")]
    [InlineData("CreatedAt")]
    [InlineData("UpdatedAt")]
    [InlineData("Id")]
    public async Task IsCurrentAsync_AnyCapturedRowFieldDrifts_InvalidationIsLatchedEvenAfterRevertAsync(string propertyName)
    {
        var row = Row();
        var repository = new ProviderRepository(row);
        var secrets = new Secrets();
        var service = new OctoberChatProviderBindingService(row.Id, repository, secrets);
        var snapshot = Assert.IsType<OctoberChatProviderSnapshot>(await service.FreezeAsync());
        var property = typeof(LlmProvider).GetProperty(propertyName)!;
        object? before = property.GetValue(row);
        object after = propertyName switch
        {
            "Id" => Guid.NewGuid(),
            "SupportedApiTypes" => ApiType.ChatCompletions | ApiType.Messages,
            "DefaultAgentType" => AgentType.Worker,
            "CreatedAt" or "UpdatedAt" => ((DateTimeOffset)before!).AddSeconds(1),
            _ => (string)before! + "-changed"
        };
        property.SetValue(row, after); // Mutate the SAME repository object: service must have copied values.
        Assert.False(await service.IsCurrentAsync(snapshot));
        property.SetValue(row, before);
        Assert.False(await service.IsCurrentAsync(snapshot));
        Assert.Null(await service.FreezeAsync());
        Assert.Null(await service.DecryptCurrentKeyAsync(snapshot));
        Assert.Empty(secrets.Decrypted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("ftp://fixture.invalid")]
    [InlineData("relative")]
    [InlineData("https://user:pass@fixture.invalid")]
    [InlineData("https://fixture.invalid?key=hidden")]
    [InlineData("https://fixture.invalid#hidden")]
    public async Task FreezeAsync_EndpointOutsideDeclaredBoundary_FailsClosedAsync(string endpoint)
    {
        var row = Row(endpoint: endpoint);
        var secrets = new Secrets();
        Assert.Null(await new OctoberChatProviderBindingService(row.Id, new ProviderRepository(row), secrets).FreezeAsync());
        Assert.Empty(secrets.Decrypted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("name")]
    [InlineData("key")]
    [InlineData("api")]
    [InlineData("missing")]
    [InlineData("outage")]
    public async Task FreezeAsync_MissingOrUnsupportedSelection_DoesNotFindAnotherProviderAsync(string invalid)
    {
        var row = Row();
        row.UpdateDetails(invalid == "name" ? "" : row.Name, row.BaseEndpointUrl,
            invalid == "key" ? "" : row.EncryptedApiKey, invalid == "api" ? ApiType.Responses : row.SupportedApiTypes,
            row.DefaultAgentType, row.UpdatedAt);
        var repository = new ProviderRepository(invalid == "missing" ? null : row) { Throw = invalid == "outage" };
        var secrets = new Secrets();
        Assert.Null(await new OctoberChatProviderBindingService(row.Id, repository, secrets).FreezeAsync());
        Assert.Equal(0, repository.ListCalls);
        Assert.Empty(secrets.Decrypted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FreezeAsync_EmptySelectedId_RefusesBeforeRepositoryLookupAsync()
    {
        var repository = new ProviderRepository(Row());
        try { Assert.Null(await new OctoberChatProviderBindingService(Guid.Empty, repository, new Secrets()).FreezeAsync()); }
        catch (ArgumentException) { }
        Assert.Empty(repository.Requested);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DecryptCurrentKeyAsync_ExactLiveBinding_UsesCapturedValueAndPurposeThenRechecksAsync()
    {
        var row = Row();
        var repository = new ProviderRepository(row);
        var secrets = new Secrets();
        var service = new OctoberChatProviderBindingService(row.Id, repository, secrets);
        var snapshot = Assert.IsType<OctoberChatProviderSnapshot>(await service.FreezeAsync());
        int before = repository.Requested.Count;
        Assert.Equal(secrets.Plaintext, await service.DecryptCurrentKeyAsync(snapshot));
        Assert.Equal((SecretPurposes.LlmApiKey, row.EncryptedApiKey), Assert.Single(secrets.Decrypted));
        Assert.True(repository.Requested.Count >= before + 2, "Eligibility must be observed before and after asynchronous decryption.");
        secrets.BeforeReturn = () => row.UpdateDetails(row.Name, row.BaseEndpointUrl, "rotated-protected-value", row.SupportedApiTypes, row.DefaultAgentType, row.UpdatedAt);
        Assert.Null(await service.DecryptCurrentKeyAsync(snapshot));
        Assert.False(await service.IsCurrentAsync(snapshot));
        Assert.Null(await service.FreezeAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecryptCurrentKeyAsync_SecretUnavailable_ReturnsNoCredentialAsync(bool throws)
    {
        var row = Row();
        var secrets = new Secrets { Plaintext = "", Throw = throws };
        var service = new OctoberChatProviderBindingService(row.Id, new ProviderRepository(row), secrets);
        var snapshot = Assert.IsType<OctoberChatProviderSnapshot>(await service.FreezeAsync());
        Assert.Null(await service.DecryptCurrentKeyAsync(snapshot));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublicOperations_CallerCancellation_PropagatesAsync()
    {
        var row = Row();
        var service = new OctoberChatProviderBindingService(row.Id, new ProviderRepository(row), new Secrets());
        var snapshot = Assert.IsType<OctoberChatProviderSnapshot>(await service.FreezeAsync());
        var cancelled = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.FreezeAsync(cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.IsCurrentAsync(snapshot, cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DecryptCurrentKeyAsync(snapshot, cancelled));
    }

    private static LlmProvider Row(string endpoint = "https://fixture.invalid/v1") =>
        new("explicit-model", endpoint, "protected-marker-never-public", ApiType.ChatCompletions, AgentType.Chat, new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero));

    private sealed class ProviderRepository(LlmProvider? row) : ILlmProviderRepository
    {
        public List<Guid> Requested { get; } = [];
        public bool Throw { get; set; }
        public int ListCalls { get; private set; }
        public Task<LlmProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requested.Add(id);
            if (Throw) throw new IOException("Controlled provider repository outage.");
            return Task.FromResult(row);
        }
        public Task<IReadOnlyList<LlmProvider>> ListAllAsync(CancellationToken cancellationToken = default)
        {
            ListCalls++;
            throw new InvalidOperationException("Provider fallback forbidden.");
        }
        public Task<bool> NameExistsAsync(string name, Guid? excludeProviderId = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<LlmProvider> AddAsync(LlmProvider provider, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Remove(LlmProvider provider) => throw new InvalidOperationException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    private sealed class Secrets : ISecretEncryptionService
    {
        public string Plaintext { get; set; } = "plaintext-marker-never-public";
        public bool Throw { get; set; }
        public Action? BeforeReturn { get; set; }
        public Func<Task>? BeforeReturnAsync { get; set; }
        public List<(string Purpose, string Cipher)> Decrypted { get; } = [];
        public Task<string> EncryptAsync(string purpose, string plain, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public async Task<string> DecryptAsync(string purpose, string cipher, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Decrypted.Add((purpose, cipher));
            if (Throw) throw new IOException("Controlled secret outage.");
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            BeforeReturn?.Invoke();
            if (BeforeReturnAsync is not null) await BeforeReturnAsync();
            return Plaintext;
        }
    }
}

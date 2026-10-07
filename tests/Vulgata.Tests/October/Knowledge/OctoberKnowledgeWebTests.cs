using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberKnowledgeWebTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddOctoberKnowledgeReads_MissingBindingOrLog_LeavesOrdinaryServicesAvailableWithoutKnowledgeDependenciesAsync(bool bindingWithoutLog)
    {
        var fixture = new B0bReadFixture();
        var services = new ServiceCollection();
        var ordinary = new OrdinaryServiceMarker();
        services.AddSingleton(ordinary);
        services.AddOctoberKnowledgeReads(bindingWithoutLog ? fixture.Publication : null);
        await using var provider = services.BuildServiceProvider();
        Assert.Same(ordinary, provider.GetRequiredService<OrdinaryServiceMarker>());
        var reader = provider.GetService<IOctoberKnowledgeReader>();
        Assert.NotNull(reader);
        var result = await reader.OpenReadSelectionAsync(fixture.Request());
        Assert.Equal(KnowledgeReadStatus.Unavailable, result.Status);
        Assert.Null(result.Selection);
        Assert.Null(provider.GetService<IOctoberKnowledgeWriter>());
        Assert.Null(provider.GetService<IOctoberBusinessFinalizer>());
        Assert.Null(provider.GetService<IOctoberExecutionScope>());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TokenProtector_ProtectedPayload_IsOpaqueRoundTripsAndRejectsTampering()
    {
        IOctoberKnowledgeTokenProtector protector = new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider());
        const string plaintext = "selection-private-subject-query-and-publication";
        string token = protector.Protect(plaintext);
        Assert.NotEqual(plaintext, token);
        Assert.DoesNotContain(plaintext, token, StringComparison.Ordinal);
        Assert.Equal(plaintext, protector.Unprotect(token));
        Assert.ThrowsAny<Exception>(() => protector.Unprotect(token + "tamper"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AddOctoberKnowledgeReadAuthority_ExplicitHostAccessor_RegistersRequiredPolicyLiveAuthorityAndTokenProtection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<IOctoberKnowledgeSelectionAuthority>(new B0bSelectionAuthority());
        services.AddOctoberKnowledgeReadAuthority(_ => Task.FromResult<ClaimsPrincipal?>(null));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOctoberKnowledgeDisclosurePolicy));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOctoberKnowledgeLiveReadAuthority));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOctoberKnowledgeTokenProtector));
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IOctoberKnowledgeDisclosurePolicy>());
        Assert.NotNull(provider.GetRequiredService<IOctoberKnowledgeTokenProtector>());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishedSourceAndSelectionAuthority_RealExplicitPublication_ReturnExactPersistedMembershipProvenanceAndScopeAsync()
    {
        await using var fixture = await OctoberKnowledgePublicationFixture.CreateAsync(includeUncitedContributor: true);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.FreezeAsync()).Status);
        var request = fixture.PublicationRequest() with { Documents = [fixture.Accepted.Documents[0]], Uncertainties = [] };
        request = request with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(request) };
        var publication = (await fixture.Acceptance.Writer().PublishSelectionAsync(request)).Publication;
        Assert.NotNull(publication);
        IOctoberPublishedKnowledgeSource source = new OctoberKnowledgePublishedSource(fixture.Acceptance.Factory);
        var loaded = await source.LoadAsync(publication);
        Assert.NotNull(loaded);
        Assert.Equal(publication, loaded.Publication);
        Assert.Equal(fixture.Accepted.Documents[0], Assert.Single(loaded.Documents).Reference);
        Assert.Equal(2, loaded.Coverage.Length);
        Assert.Contains(fixture.Acceptance.SourceContributor, loaded.Documents[0].Contributors);
        Assert.Contains(fixture.Acceptance.AdvisoryContributor, loaded.Documents[0].Contributors);
        Assert.Contains(fixture.Acceptance.UncitedContributor, loaded.Documents[0].Contributors);
        Assert.DoesNotContain(loaded.Documents[0].Sections.SelectMany(section => section.Claims).SelectMany(claim => claim.Evidence), evidence => evidence.AdvisoryRecordId == fixture.Acceptance.UncitedContributor.AdvisoryRecordId);
        Assert.Empty(loaded.Relationships);
        Assert.Null(await source.LoadAsync(publication with { PublicationId = Guid.NewGuid() }));
        Assert.Null(await source.LoadAsync(publication with { Knowledge = publication.Knowledge with { CanonicalDatabaseIdentity = "foreign-database" } }));
        IOctoberKnowledgeSelectionAuthority authority = new OctoberKnowledgeSelectionAuthority(fixture.Acceptance.Factory);
        var snapshot = await authority.LoadAsync(publication);
        Assert.NotNull(snapshot);
        Assert.Equal(publication, snapshot.Publication);
        Assert.Equal(fixture.Acceptance.Origin.RepositoryId, Assert.Single(snapshot.Repositories).RepositoryId);
        Assert.Contains(snapshot.Advisory, advisory => advisory.RecordId == fixture.Acceptance.AdvisoryContributor.AdvisoryRecordId && advisory.ContentDigest == fixture.Acceptance.AdvisoryContributor.ContentDigest);
        Assert.Null(await authority.LoadAsync(publication with { PublicationId = Guid.NewGuid() }));
        await using var context = fixture.Acceptance.Factory.CreateDbContext();
        Assert.Equal(2, await context.DocumentVersions.CountAsync());
        Assert.Equal(1, await context.PublicationMembers.CountAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadLog_RealAppend_RecordsSafeOutcomeAndCorrelationWithoutPrivateBindingTextAsync()
    {
        string directory = OctoberKnowledgeLogTests.CreateOwnedDirectory();
        try
        {
            IOctoberKnowledgeReadLog log = new OctoberKnowledgeOperationLog(directory);
            var fixture = new B0bReadFixture();
            var publication = fixture.Publication with { Knowledge = fixture.Publication.Knowledge with { TargetId = "TARGET-SECRET\ninjected", CanonicalDatabaseIdentity = "Host=DB-SECRET;Password=SECRET" } };
            Guid correlation = Guid.NewGuid();
            Assert.True(await log.AppendAsync(new(publication, correlation, KnowledgeReadOperation.ReadExact, KnowledgeReadStatus.Success)));
            string text = await File.ReadAllTextAsync(Path.Combine(directory, "log.md"));
            Assert.Contains(correlation.ToString(), text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(nameof(KnowledgeReadOperation.ReadExact), text, StringComparison.Ordinal);
            Assert.Contains(nameof(KnowledgeReadStatus.Success), text, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
            Assert.DoesNotContain("injected", text, StringComparison.Ordinal);
            Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
        finally { Directory.Delete(directory, true); }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task EnabledReadComposition_UsesExplicitHostDependenciesAndRejectsMissingOrMismatchedPublicationAsync()
    {
        await using var fixture = await OctoberKnowledgePublicationFixture.CreateAsync(includeUncitedContributor: true);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.FreezeAsync()).Status);
        Assert.Equal(KnowledgeWriteStatus.Committed, (await fixture.PublishAsync()).Status);
        var publication = fixture.Publication!;
        string directory = OctoberKnowledgeLogTests.CreateOwnedDirectory();
        try
        {
            foreach (var configured in new[] { publication, publication with { PublicationId = Guid.NewGuid() } })
            {
                var services = new ServiceCollection();
                services.AddSingleton<IDbContextFactory<OctoberDbContext>>(fixture.Acceptance.Factory);
                services.AddSingleton<IOctoberKnowledgeDisclosurePolicy>(new B0bReaderDisclosure());
                services.AddSingleton<IOctoberKnowledgeTokenProtector>(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()));
                services.AddOctoberKnowledgeReads(configured, directory);
                await using var provider = services.BuildServiceProvider();
                await using var scope = provider.CreateAsyncScope();
                var reader = scope.ServiceProvider.GetRequiredService<IOctoberKnowledgeReader>();
                var request = new ReadSelectionRequest(configured, new("ordinary-user", "controlled-stamp"), KnowledgeMode.IT, KnowledgePurpose.Reader, [fixture.Acceptance.Origin.RepositoryId]);
                var opened = await reader.OpenReadSelectionAsync(request);
                Assert.Equal(configured == publication ? KnowledgeReadStatus.Success : KnowledgeReadStatus.Unavailable, opened.Status);
                if (configured == publication)
                {
                    var page = await reader.SearchAsync(new(opened.Selection!, ""));
                    Assert.Equal(KnowledgeReadStatus.Success, page.Status);
                    Assert.Equal(2, page.Documents.Length);
                }
                else Assert.Null(opened.Selection);
                var mismatch = await reader.OpenReadSelectionAsync(request with { Publication = configured with { Knowledge = configured.Knowledge with { TargetId = "foreign-target" } } });
                Assert.Equal(KnowledgeReadStatus.Unavailable, mismatch.Status);
                Assert.Null(mismatch.Selection);
                Assert.Null(scope.ServiceProvider.GetService<IOctoberKnowledgeWriter>());
                Assert.Null(scope.ServiceProvider.GetService<IOctoberBusinessFinalizer>());
                Assert.Null(scope.ServiceProvider.GetService<IOctoberExecutionScope>());
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DisabledKnowledge_RealInMemoryWebStartup_PreservesLoginAndRoleSeedWithoutOctoberSchemaAsync()
    {
        var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        string connectionString = factory.ConnectionString;
        string directory = Path.GetDirectoryName(new SqliteConnectionStringBuilder(connectionString).DataSource)!;
        try
        {
            using (factory)
            using (var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddOctoberKnowledgeReads(null))))
            using (var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
            {
                using var response = await client.GetAsync("/Account/Login");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                await using var scope = host.Services.CreateAsyncScope();
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                Assert.True(await roles.RoleExistsAsync("User"));
                Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOctoberKnowledgeReader>());
                Assert.Null(scope.ServiceProvider.GetService<OctoberDbContext>());
                await using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name LIKE 'October%';";
                Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
            }
        }
        finally
        {
            factory.Dispose();
            // Clear only this fixture's connection pool after the in-memory host has stopped.
            using var pooled = new SqliteConnection(connectionString);
            SqliteConnection.ClearPool(pooled);
            string ownedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "vulgata-tests")) + Path.DirectorySeparatorChar;
            Assert.StartsWith(ownedRoot, Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase);
            Assert.True(Guid.TryParseExact(Path.GetFileName(directory), "N", out _));
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        Assert.False(Directory.Exists(directory));
    }
    private sealed class OrdinaryServiceMarker;
}

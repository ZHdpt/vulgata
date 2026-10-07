using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Explore;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberWikiHostTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ProductionComposition_EnabledWikiFreezesFinalConnectionAndPublicationBeforeFirstResolutionAsync()
    {
        var publication = new PublicationBinding(new("owned-composition-db", "owned-target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid());
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production, ApplicationName = typeof(Program).Assembly.GetName().Name, Args = []
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(OctoberWikiCompositionTests.Configuration(publication, Guid.NewGuid(), Path.GetTempPath()).AsEnumerable());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SourceStorage:RootPath"] = "data/source",
            ["SourceStorage:MaximumEntries"] = "10000",
            ["SourceStorage:MaximumBlobBytes"] = "8388608",
            ["SourceStorage:MaximumAggregateBlobBytes"] = "67108864",
            ["SourceStorage:CaptureDeadlineSeconds"] = "600",
            ["SourceStorage:OperationStaleAfterMinutes"] = "1440",
            ["SourceStorage:VerificationBufferBytes"] = "65536"
        });
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=initial_not_opened;Username=fixture;Password=fixture-only";
        VulgataWebComposition.ConfigureServices(builder);
        builder.Services.Replace(ServiceDescriptor.Singleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));
        builder.Services.Replace(ServiceDescriptor.Singleton<AuthenticationStateProvider>(new IntegrationAuthentication()));
        // Disclosure itself is qualified separately against real Identity; no DB may be opened in this build-only probe.
        builder.Host.ConfigureContainer<IServiceCollection>((_, services) => services.Replace(
            ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(new B0bReaderDisclosure())));
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=final_not_opened;Username=fixture;Password=fixture-only";
        Guid finalPublication = Guid.NewGuid();
        builder.Configuration["October:Wiki:PublicationId"] = finalPublication.ToString();
        await using var app = builder.Build();
        app.Configuration["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=reloaded_not_opened;Username=fixture;Password=fixture-only";
        app.Configuration["October:Wiki:PublicationId"] = Guid.NewGuid().ToString();
        await using var scope = app.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OctoberDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal("final_not_opened", context.Database.GetDbConnection().Database);
        Assert.IsType<OctoberKnowledgeReader>(scope.ServiceProvider.GetRequiredService<IOctoberKnowledgeReader>());
        var selection = Assert.IsType<ExploreContext>(await scope.ServiceProvider.GetRequiredService<IExploreContextProvider>().GetCurrentAsync());
        Assert.Equal(finalPublication, selection.Selection.Publication.PublicationId);
        Assert.Equal(publication.Knowledge, selection.Selection.Publication.Knowledge);
        OctoberWikiCompositionTests.AssertNoProducer(scope.ServiceProvider);
        // Deliberately never StartAsync: no listeners, startup migration, PostgreSQL connection or provider call.
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Program_WithoutWiki_RegistersUnavailableReaderAndLeavesLoginUsableWithoutOctoberSchemaAsync()
    {
        var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        string connectionString = factory.ConnectionString;
        string directory = Path.GetDirectoryName(new SqliteConnectionStringBuilder(connectionString).DataSource)!;
        try
        {
            using (factory)
            using (var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
            {
                using var response = await client.GetAsync("/Account/Login");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                await using var scope = factory.Services.CreateAsyncScope();
                Assert.Null(await scope.ServiceProvider.GetRequiredService<IExploreContextProvider>().GetCurrentAsync());
                Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOctoberKnowledgeReader>());
                OctoberWikiCompositionTests.AssertNoProducer(scope.ServiceProvider);
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
            using var pooled = new SqliteConnection(connectionString);
            SqliteConnection.ClearPool(pooled);
            string ownedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "vulgata-tests")) + Path.DirectorySeparatorChar;
            Assert.StartsWith(ownedRoot, Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase);
            Assert.True(Guid.TryParseExact(Path.GetFileName(directory), "N", out _));
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

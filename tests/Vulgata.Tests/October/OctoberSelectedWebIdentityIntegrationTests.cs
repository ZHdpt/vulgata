using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Vulgata.Core.Entities;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.OctoberGenerator;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.October;

public sealed class OctoberSelectedWebIdentityIntegrationTests
{
    private const string SelectedWebTargetId = "october-host-target-b";
    private const string SelectedWebAuthorityId = "october-host-management-b";
    private const string SelectedWebCanonicalDatabaseIdentity =
        "postgres://owned-test/october-host-target-b";
    private const string SelectedWebUserName = "october-host-operator@example.test";
    private const string SelectedWebPassword = "OctoberHost!Passw0rd8";
    private const string BaselineWebNpgsqlConnection =
        "Host=localhost;Port=5432;Database=october_host_baseline_a;Username=test;Password=test-only";
    private const string SelectedWebNpgsqlConnection =
        "Host=localhost;Port=5432;Database=october_host_selected_b;Username=test;Password=test-only";
    private static readonly Guid SelectedWebBatchId =
        Guid.Parse("2746c8b4-bd3a-4fc6-a603-6bacda77be11");

    private const string BatchActivationIssue = "/api/october-batch-activation-grants";
    private const string BatchActivationValidation = "/api/october-batch-activation-grants/validations";
    private const string PreparedSourceApprovalIssue = "/api/october-prepared-source-approvals";
    private const string PreparedSourceApprovalVerification =
        "/api/october-prepared-source-approvals/verifications";

    private static async Task<SelectedWebRunFixture> CreateSelectedWebRunFixtureAsync(
        bool separateIdentityDatabase,
        Guid configuredBatchId)
    {
        OwnedSelectedWebIdentityDatabase? identityDatabase = null;
        OwnedSelectedWebSqliteStore? selectedDataStore = null;
        OwnedSelectedWebSqliteStore? baselineStore = null;
        OwnedProviderDocument? providerDocument = null;
        WebApplicationFactory<Program>? baselineFactory = null;
        WebApplicationFactory<Program>? selectedFactory = null;
        try
        {
            identityDatabase = await OwnedSelectedWebIdentityDatabase.CreateAsync();
            selectedDataStore = OwnedSelectedWebSqliteStore.Create();
            baselineStore = OwnedSelectedWebSqliteStore.Create();
            string selectedDataNpgsqlConnection = separateIdentityDatabase
                ? SelectedWebNpgsqlConnection
                : identityDatabase.ConnectionString;
            string selectedTargetIdentity = separateIdentityDatabase
                ? SelectedWebCanonicalDatabaseIdentity
                : identityDatabase.CanonicalDatabaseIdentity;
            string subjectId = Guid.NewGuid().ToString("D");
            await SeedOwnedIdentityAsync(identityDatabase.ConnectionString, subjectId);
            await SeedSelectedWebBatchAsync(
                selectedDataStore.ConnectionString,
                SelectedWebAuthorityId,
                selectedTargetIdentity,
                SelectedWebTargetId,
                SelectedWebBatchId,
                subjectId);
            await AssertOwnedIdentityAndSelectionPreconditionsAsync(
                identityDatabase,
                selectedDataStore.ConnectionString,
                selectedDataNpgsqlConnection,
                selectedTargetIdentity);

            providerDocument = await OwnedProviderDocument.CreateAsync();
            List<string> startupEvents = [];
            RecordingSecretEncryptionService? recordingEncryption = null;
            CountingSelectedWebCredentialSource credentialSource = new(
                SelectedWebUserName, SelectedWebPassword, startupEvents);
            ProductionNpgsqlDeclarations? productionDeclarations = null;
            SelectedWebConnectionTranslation translation = new(
                BaselineWebNpgsqlConnection,
                selectedDataNpgsqlConnection,
                baselineStore.ConnectionString,
                selectedDataStore.ConnectionString);

            baselineFactory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                    builder.ConfigureAppConfiguration((_, configuration) =>
                        configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] = identityDatabase.ConnectionString
                        })));
            selectedFactory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.ConfigureAppConfiguration((_, configuration) =>
                    {
                        configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] = BaselineWebNpgsqlConnection
                        });
                        configuration.AddInMemoryCollection(CreateSelectedWebConfiguration(
                            selectedDataNpgsqlConnection,
                            separateIdentityDatabase ? "Identity" : null,
                            identityDatabase.CanonicalDatabaseIdentity,
                            configuredBatchId));
                        if (separateIdentityDatabase)
                        {
                            configuration.AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["ConnectionStrings:Identity"] = identityDatabase.ConnectionString,
                                ["October:Identity:CanonicalDatabaseIdentity"] =
                                    identityDatabase.CanonicalDatabaseIdentity
                            });
                        }
                        else
                        {
                            configuration.AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["October:Identity:CanonicalDatabaseIdentity"] =
                                    identityDatabase.CanonicalDatabaseIdentity
                            });
                        }

                        configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["October:Providers:ConfigurationFile"] = providerDocument.ConfigurationFilePath,
                            ["DataProtection:KeyRingPath"] = providerDocument.KeyRingPath,
                            ["DataProtection:ApplicationName"] = providerDocument.ApplicationName
                        });
                    });
                    builder.ConfigureTestServices(services =>
                    {
                        productionDeclarations = CaptureProductionNpgsqlDeclarations(services);
                        services.RemoveAll<IOctoberOperatorCredentialSource>();
                        services.AddSingleton<IOctoberOperatorCredentialSource>(credentialSource);
                        services.RemoveAll<IDataProtectionProvider>();
                        services.AddSingleton(providerDocument.DataProtectionProvider);
                        services.RemoveAll<ISecretEncryptionService>();
                        services.AddSingleton<ISecretEncryptionService>(_ =>
                            recordingEncryption = new RecordingSecretEncryptionService(
                                new SecretEncryptionService(providerDocument.DataProtectionProvider),
                                startupEvents));
                        ReplaceSelectedWebSqliteOptionsPreservingIdentity(
                            services, productionDeclarations, translation);
                    });
                });

            return new SelectedWebRunFixture(
                identityDatabase,
                selectedDataStore,
                baselineStore,
                providerDocument,
                baselineFactory,
                selectedFactory,
                subjectId,
                selectedDataNpgsqlConnection,
                selectedTargetIdentity,
                startupEvents,
                credentialSource,
                () => recordingEncryption,
                () => productionDeclarations);
        }
        catch
        {
            try
            {
                selectedFactory?.Dispose();
            }
            finally
            {
                try
                {
                    baselineFactory?.Dispose();
                }
                finally
                {
                    try
                    {
                        providerDocument?.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            selectedDataStore?.Dispose();
                        }
                        finally
                        {
                            try
                            {
                                baselineStore?.Dispose();
                            }
                            finally
                            {
                                if (identityDatabase is not null)
                                    await identityDatabase.DisposeAsync();
                            }
                        }
                    }
                }
            }
            throw;
        }
    }

    [Trait("Suite", "E2E")]
    [SuiteTheory("E2E")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedDataStartup_BindsExactConfiguredBatchWithoutLegacyGrant(
        bool separateIdentityDatabase)
    {
        await using SelectedWebRunFixture fixture = await CreateSelectedWebRunFixtureAsync(
            separateIdentityDatabase, SelectedWebBatchId);
        OwnedSelectedWebIdentityDatabase identityDatabase = fixture.IdentityDatabase;
        OwnedSelectedWebSqliteStore selectedDataStore = fixture.SelectedDataStore;
        OwnedProviderDocument providerDocument = fixture.ProviderDocument;
        WebApplicationFactory<Program> baselineFactory = fixture.BaselineFactory;
        WebApplicationFactory<Program> selectedFactory = fixture.SelectedFactory;
        string subjectId = fixture.SubjectId;
        string selectedDataNpgsqlConnection = fixture.SelectedDataNpgsqlConnection;
        List<string> startupEvents = fixture.StartupEvents;
        CountingSelectedWebCredentialSource credentialSource = fixture.CredentialSource;



        using IServiceScope ordinaryScope = baselineFactory.Services.CreateScope();
        IServiceProvider ordinaryServices = ordinaryScope.ServiceProvider;
        Assert.Equal(OctoberWebMode.Ordinary,
            ordinaryServices.GetRequiredService<OctoberWebStartupSelection>().Mode);
        Assert.Null(ordinaryServices.GetService<OctoberAuthorityConfiguration>());
        Assert.Null(ordinaryServices.GetService<OctoberDbContext>());
        Assert.Null(ordinaryServices.GetService<IOctoberLocalAuthority>());
        Assert.Null(ordinaryServices.GetService<IOctoberSelectedDataStartupBinder>());
        Assert.Null(ordinaryServices.GetService<IOctoberBoundSetupData>());
        Assert.Null(ordinaryServices.GetService<IOctoberOperatorCredentialSource>());
        AssertLegacyRoutesAbsent(baselineFactory.Services);

        using HttpClient selectedClient = selectedFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using HttpResponseMessage anonymousSystemsResponse = await selectedClient.GetAsync("/api/systems/");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousSystemsResponse.StatusCode);
        Dictionary<string, string> loginFields = await GetSelectedHostLoginFieldsAsync(selectedClient);
        loginFields["Input.Email"] = SelectedWebUserName;
        loginFields["Input.Password"] = SelectedWebPassword;
        loginFields["Input.RememberMe"] = "false";
        using (FormUrlEncodedContent login = new(loginFields))
        using (HttpResponseMessage loginResponse = await selectedClient.PostAsync("/Account/Login", login))
        {
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
            string[] setCookies = loginResponse.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
                ? values.ToArray()
                : [];
            Assert.Contains(setCookies, cookie =>
                cookie.Contains(".AspNetCore.Identity.Application", StringComparison.Ordinal));
        }

        using HttpResponseMessage permittedSystemsResponse = await selectedClient.GetAsync("/api/systems/");
        Assert.Equal(HttpStatusCode.OK, permittedSystemsResponse.StatusCode);
        AssertLegacyRoutesAbsent(selectedFactory.Services);

        RecordingSecretEncryptionService actualEncryption =
            Assert.IsType<RecordingSecretEncryptionService>(fixture.RecordingEncryption);
        Assert.Equal(2, actualEncryption.Decryptions.Count);
        Assert.All(actualEncryption.Decryptions,
            decryption => Assert.Equal("Vulgata.LlmProviders.ApiKey", decryption.Purpose));
        Assert.Contains(providerDocument.WorkerCiphertext,
            actualEncryption.Decryptions.Select(decryption => decryption.Ciphertext));
        Assert.Contains(providerDocument.ChatCiphertext,
            actualEncryption.Decryptions.Select(decryption => decryption.Ciphertext));
        Assert.Equal(new[] { "provider-decrypt", "provider-decrypt", "operator-credentials" },
            startupEvents.ToArray());
        Assert.Equal(1, credentialSource.ReadCount);

        using IServiceScope scope = selectedFactory.Services.CreateScope();
        IConfigurationRoot finalConfiguration = Assert.IsAssignableFrom<IConfigurationRoot>(
            scope.ServiceProvider.GetRequiredService<IConfiguration>());
        await providerDocument.WriteAlternativeValidDocumentAsync();
        finalConfiguration.Reload();
        Assert.Equal(providerDocument.ConfigurationFilePath,
            finalConfiguration["October:Providers:ConfigurationFile"]);
        Assert.Equal(2, actualEncryption.Decryptions.Count);
        Assert.Equal(1, credentialSource.ReadCount);
        Assert.Equal(selectedDataNpgsqlConnection,
            finalConfiguration.GetConnectionString("DefaultConnection"));
        Assert.NotEqual(BaselineWebNpgsqlConnection,
            finalConfiguration.GetConnectionString("DefaultConnection"));
        Assert.Equal(SelectedWebTargetId, finalConfiguration["October:Web:TargetId"]);
        Assert.Equal(SelectedWebBatchId.ToString("D"), finalConfiguration["October:Web:BatchId"]);
        Assert.Null(finalConfiguration["October:Web:ActivationGrant"]);
        Assert.Null(finalConfiguration["October:Web:Endpoint"]);
        Assert.Empty(finalConfiguration.GetSection("October:Web:GeneratorCertificates").GetChildren());

        IOctoberBoundSetupData bound = scope.ServiceProvider.GetRequiredService<IOctoberBoundSetupData>();
        Assert.Equal(SelectedWebTargetId, bound.TargetId);
        Assert.Equal(SelectedWebBatchId, bound.BatchId);

        ProductionNpgsqlDeclarations declarations = Assert.IsType<ProductionNpgsqlDeclarations>(
            fixture.ProductionDeclarations);
        Assert.Equal(identityDatabase.ConnectionString,
            CaptureProductionNpgsqlConnection(scope.ServiceProvider, declarations.Identity));
        Assert.All(CaptureProductionNpgsqlConnections(scope.ServiceProvider, declarations.Management),
            connection => Assert.Equal(selectedDataNpgsqlConnection, connection));

        OctoberAuthorityConfiguration authority =
            scope.ServiceProvider.GetRequiredService<OctoberAuthorityConfiguration>();
        Assert.Equal(selectedDataNpgsqlConnection, authority.DataConnectionString);
        Assert.Equal(identityDatabase.ConnectionString, authority.IdentityConnectionString);
        Assert.Equal(identityDatabase.CanonicalDatabaseIdentity,
            authority.IdentityCanonicalDatabaseIdentity);

        ApplicationDbContext identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationUser seededOperator = Assert.IsAssignableFrom<ApplicationUser>(
            await identity.Users.SingleOrDefaultAsync(user => user.Id == subjectId));
        Assert.Equal(SelectedWebUserName, seededOperator.UserName);
        NpgsqlConnectionStringBuilder connectedIdentity =
            new(identity.Database.GetDbConnection().ConnectionString);
        NpgsqlConnectionStringBuilder expectedIdentity = new(identityDatabase.ConnectionString);
        Assert.Equal(expectedIdentity.Host, connectedIdentity.Host);
        Assert.Equal(expectedIdentity.Port, connectedIdentity.Port);
        Assert.Equal(expectedIdentity.Database, connectedIdentity.Database);

        VulgataDbContext management = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        OctoberDbContext october = scope.ServiceProvider.GetRequiredService<OctoberDbContext>();
        IDbContextFactory<VulgataDbContext> managementFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext factoryContext = await managementFactory.CreateDbContextAsync();
        AssertSelectedSqliteConnection(selectedDataStore.ConnectionString,
            management.Database.GetDbConnection().ConnectionString);
        AssertSelectedSqliteConnection(selectedDataStore.ConnectionString,
            october.Database.GetDbConnection().ConnectionString);
        AssertSelectedSqliteConnection(selectedDataStore.ConnectionString,
            factoryContext.Database.GetDbConnection().ConnectionString);
    }

    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task SelectedDataStartup_MissingBatchAfterValidProviderBinding_ReportsSanitizedDeniedStatus()
    {
        Guid missingBatchId = Guid.Parse("d3c9b744-14f8-4a97-a52c-7f0abec52e04");
        await using SelectedWebRunFixture fixture = await CreateSelectedWebRunFixtureAsync(
            separateIdentityDatabase: false, missingBatchId);
        ImmutableArray<SelectedWebBatchState> batchStateBefore =
            await CaptureSelectedWebBatchStateAsync(fixture.SelectedDataStore.ConnectionString);

        Exception? startupFailure = Record.Exception(() =>
        {
            using HttpClient client = fixture.SelectedFactory.CreateClient();
        });
        ImmutableArray<SelectedWebBatchState> batchStateAfter =
            await CaptureSelectedWebBatchStateAsync(fixture.SelectedDataStore.ConnectionString);

        RecordingSecretEncryptionService actualEncryption =
            Assert.IsType<RecordingSecretEncryptionService>(fixture.RecordingEncryption);
        Assert.Equal(2, actualEncryption.Decryptions.Count);
        Assert.Equal(1, fixture.CredentialSource.ReadCount);
        InvalidOperationException failure = Assert.IsType<InvalidOperationException>(startupFailure);
        string diagnostic = failure.ToString();
        Assert.Contains("October selected-data startup failed: Denied.", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("October provider configuration failed", diagnostic, StringComparison.Ordinal);
        foreach (string canary in new[]
        {
            SelectedWebPassword,
            SelectedWebUserName,
            "synthetic-worker-api-key",
            "synthetic-chat-api-key",
            fixture.ProviderDocument.WorkerCiphertext,
            fixture.ProviderDocument.ChatCiphertext,
            fixture.ProviderDocument.ConfigurationFilePath,
            fixture.ProviderDocument.KeyRingPath,
            fixture.ProviderDocument.ApplicationName,
            BaselineWebNpgsqlConnection,
            SelectedWebNpgsqlConnection,
            SelectedWebTargetId,
            SelectedWebBatchId.ToString("D"),
            missingBatchId.ToString("D"),
            SelectedWebAuthorityId,
            fixture.SelectedTargetIdentity,
            "selected-web-root-v1",
            "Global advisory",
            "System advisory",
            "Repository advisory",
            fixture.SubjectId,
            "selected-web-provider-startup-test"
        })
        {
            Assert.DoesNotContain(canary, diagnostic, StringComparison.Ordinal);
        }

        Assert.Equal([SelectedWebBatchId], batchStateBefore.Select(batch => batch.BatchId));
        Assert.Equal(batchStateBefore.ToArray(), batchStateAfter.ToArray());
    }

    private static Dictionary<string, string?> CreateSelectedWebConfiguration(
        string selectedConnection,
        string? identityConnectionName,
        string identityCanonicalDatabaseIdentity,
        Guid batchId) =>
        new(StringComparer.Ordinal)
        {
            ["ConnectionStrings:DefaultConnection"] = selectedConnection,
            ["ConnectionStrings:Identity"] = identityConnectionName is null ? null : selectedConnection,
            ["October:Web:Mode"] = "selected-data",
            ["October:Web:TargetId"] = SelectedWebTargetId,
            ["October:Web:BatchId"] = batchId.ToString("D"),
            ["October:Authority:Id"] = SelectedWebAuthorityId,
            ["October:Targets:0:TargetId"] = SelectedWebTargetId,
            ["October:Targets:0:CanonicalDatabaseIdentity"] =
                identityConnectionName is null
                    ? identityCanonicalDatabaseIdentity
                    : SelectedWebCanonicalDatabaseIdentity,
            ["October:Identity:ConnectionName"] = identityConnectionName,
            ["October:Identity:CanonicalDatabaseIdentity"] = identityCanonicalDatabaseIdentity,
            ["October:Operator:CredentialSource"] = "console",
            ["October:Operator:CredentialFile"] = null,
            ["October:Web:ActivationGrant"] = null,
            ["October:Web:Endpoint"] = null,
            ["October:Web:ManagementAuthorityId"] = null,
            ["October:Web:AllowedTargets"] = null,
            ["October:Web:GeneratorCertificates"] = null
        };

    private static async Task SeedSelectedWebBatchAsync(
        string sqliteConnectionString,
        string authorityId,
        string canonicalDatabaseIdentity,
        string targetId,
        Guid batchId,
        string subjectId)
    {
        DateTimeOffset now = new(2026, 10, 1, 4, 0, 0, TimeSpan.Zero);
        DbContextOptions<VulgataDbContext> managementOptions = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite(sqliteConnectionString)
            .Options;
        await using VulgataDbContext management = new(managementOptions);
        await management.Database.EnsureCreatedAsync();
        SystemEntity system = new("October selected Web System", "Owned test fixture",
            "System advisory", now);
        Repository repository = system.AddRepository("October selected Web Repository",
            "https://example.test/october-selected-web.git", "Owned test fixture",
            "Repository advisory", now);
        GlobalContext global = new("Global advisory", now);
        management.Systems.Add(system);
        management.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(system.Id, subjectId, now));
        management.Repositories.Add(repository);
        management.GlobalContexts.Add(global);
        await management.SaveChangesAsync();

        OctoberRepositorySelection selectedRepository = new(
            repository.Id, system.Id, "selected-web-root-v1", "selected-web-operator");
        OctoberContextSelection[] references =
        [
            new(OctoberContextScope.Global, global.Id),
            new(OctoberContextScope.System, system.Id),
            new(OctoberContextScope.Repository, repository.Id)
        ];
        OctoberAdvisoryContextEntry[] entries =
        [
            new(repository.Id, OctoberContextScope.Global, OctoberContextApplicability.Selected,
                global.Id, "Global advisory", OctoberContextEvidenceKind.AdvisoryNotSourceProof),
            new(repository.Id, OctoberContextScope.System, OctoberContextApplicability.Selected,
                system.Id, "System advisory", OctoberContextEvidenceKind.AdvisoryNotSourceProof),
            new(repository.Id, OctoberContextScope.Repository, OctoberContextApplicability.Selected,
                repository.Id, "Repository advisory", OctoberContextEvidenceKind.AdvisoryNotSourceProof)
        ];
        OctoberContextDifference[] differences =
        [
            new(repository.Id, OctoberContextScope.Global, OctoberContextScope.System),
            new(repository.Id, OctoberContextScope.Global, OctoberContextScope.Repository),
            new(repository.Id, OctoberContextScope.System, OctoberContextScope.Repository)
        ];
        OctoberSelectedAdvisoryContext context = new(
            authorityId,
            ImmutableArray.Create(selectedRepository),
            ImmutableArray.Create(entries),
            ImmutableArray.Create(differences));
        OctoberSetupRequest request = new(targetId, batchId, [selectedRepository], references, string.Empty);
        string digest = OctoberSelectionDigest.Compute(request, context);
        request = request with { SelectionDigest = digest };
        OctoberAuthorizedSelection authority = new(
            targetId, batchId, subjectId, canonicalDatabaseIdentity, [repository.Id], digest, context);

        await using OctoberDbContext october = new(
            new DbContextOptionsBuilder<OctoberDbContext>()
                .UseSqlite(sqliteConnectionString)
                .Options);
        await october.Database.ExecuteSqlRawAsync(october.Database.GenerateCreateScript());
        OctoberPrepareResult prepared = await new OctoberBatchSetupPolicy(
                new OctoberSetupStore(october, TimeProvider.System))
            .PrepareAsync(request, authority, CancellationToken.None);
        Assert.Equal(OctoberPrepareStatus.Created, prepared.Status);
        Assert.Equal(batchId, prepared.CommittedBatch!.BatchId);
    }

    private static async Task SeedOwnedIdentityAsync(string connectionString, string subjectId)
    {
        ServiceCollection services = new();
        const string authorityId = "selected-web-seed-authority";
        const string targetId = "selected-web-seed-target";
        NpgsqlConnectionStringBuilder builder = new(connectionString);
        string canonicalDatabaseIdentity =
            $"postgres://{builder.Host}:{builder.Port}/{builder.Database}";
        OctoberAuthorityConfiguration authorityConfiguration = new(
            authorityId,
            targetId,
            Guid.NewGuid(),
            canonicalDatabaseIdentity,
            connectionString,
            connectionString,
            canonicalDatabaseIdentity,
            ImmutableDictionary<string, string>.Empty.Add(targetId, canonicalDatabaseIdentity),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromHours(1));
        CountingSelectedWebCredentialSource credentialSource = new(
            SelectedWebUserName, SelectedWebPassword, []);
        OctoberHeadlessAuthorityRegistration.AddOctoberLocalAuthority(
            services, authorityConfiguration, credentialSource, TimeProvider.System);
        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ApplicationDbContext database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await database.Database.MigrateAsync();

        RoleManager<IdentityRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(RoleNames.SystemOwner))
        {
            IdentityResult createdRole = await roles.CreateAsync(new IdentityRole(RoleNames.SystemOwner));
            Assert.True(createdRole.Succeeded,
                string.Join("; ", createdRole.Errors.Select(error => error.Code)));
        }

        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = new()
        {
            Id = subjectId,
            UserName = SelectedWebUserName,
            Email = SelectedWebUserName,
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        IdentityResult createdUser = await users.CreateAsync(user, SelectedWebPassword);
        Assert.True(createdUser.Succeeded,
            string.Join("; ", createdUser.Errors.Select(error => error.Code)));
        IdentityResult assigned = await users.AddToRoleAsync(user, RoleNames.SystemOwner);
        Assert.True(assigned.Succeeded,
            string.Join("; ", assigned.Errors.Select(error => error.Code)));
    }

    private static async Task AssertOwnedIdentityAndSelectionPreconditionsAsync(
        OwnedSelectedWebIdentityDatabase identityDatabase,
        string selectedSqliteConnection,
        string dataConnection,
        string selectedTargetIdentity)
    {
        OctoberAuthorityConfiguration authorityConfiguration = new(
            SelectedWebAuthorityId,
            SelectedWebTargetId,
            SelectedWebBatchId,
            selectedTargetIdentity,
            dataConnection,
            identityDatabase.ConnectionString,
            identityDatabase.CanonicalDatabaseIdentity,
            ImmutableDictionary<string, string>.Empty.Add(SelectedWebTargetId, selectedTargetIdentity),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromHours(1));
        List<string> preconditionEvents = [];
        CountingSelectedWebCredentialSource credentials = new(
            SelectedWebUserName, SelectedWebPassword, preconditionEvents);
        ServiceCollection services = new();
        OctoberHeadlessAuthorityRegistration.AddOctoberLocalAuthority(
            services, authorityConfiguration, credentials, TimeProvider.System);
        services.RemoveAll<VulgataDbContext>();
        services.RemoveAll<DbContextOptions<VulgataDbContext>>();
        services.RemoveAll<IDbContextFactory<VulgataDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
        services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
        services.AddDbContextFactory<VulgataDbContext>(options => options.UseSqlite(selectedSqliteConnection));
        services.AddDbContext<OctoberDbContext>(options => options.UseSqlite(selectedSqliteConnection));

        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ApplicationDbContext identityContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        NpgsqlConnectionStringBuilder configuredIdentity = new(identityDatabase.ConnectionString);
        NpgsqlConnectionStringBuilder effectiveIdentity =
            new(identityContext.Database.GetDbConnection().ConnectionString);
        Assert.Equal(configuredIdentity.Host, effectiveIdentity.Host);
        Assert.Equal(configuredIdentity.Port, effectiveIdentity.Port);
        Assert.Equal(configuredIdentity.Database, effectiveIdentity.Database);
        await identityContext.Database.OpenConnectionAsync();
        try
        {
            await using NpgsqlCommand currentDatabase =
                identityContext.Database.GetDbConnection().CreateCommand() as NpgsqlCommand ??
                throw new InvalidOperationException("The owned Identity connection must be Npgsql.");
            currentDatabase.CommandText = "SELECT current_database();";
            Assert.Equal(configuredIdentity.Database, await currentDatabase.ExecuteScalarAsync());
        }
        finally
        {
            await identityContext.Database.CloseConnectionAsync();
        }

        UserManager<ApplicationUser> seededUsers = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser seededUser = Assert.IsAssignableFrom<ApplicationUser>(
            await seededUsers.FindByNameAsync(SelectedWebUserName));
        Assert.True(await seededUsers.CheckPasswordAsync(seededUser, SelectedWebPassword));
        Assert.True(await seededUsers.IsInRoleAsync(seededUser, RoleNames.SystemOwner));

        IOctoberLocalAuthority authority = scope.ServiceProvider.GetRequiredService<IOctoberLocalAuthority>();
        OctoberAuthenticationResult authenticated = await authority.AuthenticateAsync(CancellationToken.None);
        Assert.Equal(OctoberAuthenticationStatus.Authenticated, authenticated.Status);
        await using IOctoberOperatorSession session =
            Assert.IsAssignableFrom<IOctoberOperatorSession>(authenticated.Session);
        Assert.Equal(SelectedWebAuthorityId, session.ManagementAuthorityId);
        OctoberAuthoritySelectionResult selection = await authority.AuthorizeSelectionAsync(
            session,
            new OctoberAuthoritySelectionRequest(SelectedWebTargetId, SelectedWebBatchId, null),
            CancellationToken.None);
        Assert.Equal(OctoberAuthorityDecisionStatus.Authorized, selection.Status);
        Assert.Equal(1, credentials.ReadCount);
    }

    private static ProductionNpgsqlDeclarations CaptureProductionNpgsqlDeclarations(
        IServiceCollection services) =>
        new(
            CaptureProductionDeclarations<ApplicationDbContext>(services),
            CaptureProductionDeclarations<VulgataDbContext>(services));

    private static ImmutableArray<IDbContextOptionsConfiguration<TContext>>
        CaptureProductionDeclarations<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        ServiceDescriptor[] descriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<TContext>))
            .ToArray();
        Assert.NotEmpty(descriptors);
        return descriptors.Select(descriptor =>
        {
            object? declaration = descriptor.ImplementationInstance ??
                descriptor.ImplementationFactory?.Invoke(EmptyServiceProvider.Instance);
            return Assert.IsAssignableFrom<IDbContextOptionsConfiguration<TContext>>(
                declaration ?? throw new InvalidOperationException(
                    $"The production {typeof(TContext).Name} options declaration was not captured."));
        }).ToImmutableArray();
    }

    private static void ReplaceSelectedWebSqliteOptionsPreservingIdentity(
        IServiceCollection services,
        ProductionNpgsqlDeclarations declarations,
        SelectedWebConnectionTranslation translation)
    {
        services.RemoveAll<VulgataDbContext>();
        services.RemoveAll<DbContextOptions<VulgataDbContext>>();
        services.RemoveAll<IDbContextFactory<VulgataDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
        services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
        services.AddDbContext<VulgataDbContext>((provider, options) =>
                options.UseSqlite(translation.ToSqliteConnection(
                    CaptureProductionNpgsqlConnection(provider, declarations.Management))),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<VulgataDbContext>((provider, options) =>
            options.UseSqlite(translation.ToSqliteConnection(
                CaptureProductionNpgsqlConnection(provider, declarations.Management))));

        services.RemoveAll<OctoberDbContext>();
        services.RemoveAll<DbContextOptions<OctoberDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<OctoberDbContext>>();
        services.RemoveAll<IConfigureOptions<DbContextOptions<OctoberDbContext>>>();
        services.AddDbContext<OctoberDbContext>((provider, options) =>
        {
            OctoberAuthorityConfiguration authority =
                provider.GetRequiredService<OctoberAuthorityConfiguration>();
            options.UseSqlite(translation.ToSqliteConnection(authority.DataConnectionString));
        });
    }

    private static string CaptureProductionNpgsqlConnection<TContext>(
        IServiceProvider provider,
        ImmutableArray<IDbContextOptionsConfiguration<TContext>> declarations)
        where TContext : DbContext
    {
        string[] connections = CaptureProductionNpgsqlConnections(provider, declarations);
        Assert.NotEmpty(connections);
        Assert.All(connections, connection => Assert.Equal(connections[0], connection));
        return connections[0];
    }

    private static string[] CaptureProductionNpgsqlConnections<TContext>(
        IServiceProvider provider,
        ImmutableArray<IDbContextOptionsConfiguration<TContext>> declarations)
        where TContext : DbContext => declarations.Select(declaration =>
        {
            DbContextOptionsBuilder<TContext> options = new();
            declaration.Configure(provider, options);
            NpgsqlOptionsExtension npgsql = Assert.Single(
                options.Options.Extensions.OfType<NpgsqlOptionsExtension>());
            return Assert.IsType<string>(npgsql.ConnectionString);
        }).ToArray();

    private static void AssertSelectedSqliteConnection(string expected, string actual)
    {
        string expectedDataSource = new SqliteConnectionStringBuilder(expected).DataSource;
        string actualDataSource = new SqliteConnectionStringBuilder(actual).DataSource;
        Assert.Equal(expectedDataSource, actualDataSource);
    }

    private static async Task<ImmutableArray<SelectedWebBatchState>> CaptureSelectedWebBatchStateAsync(
        string sqliteConnectionString)
    {
        await using OctoberDbContext database = new(
            new DbContextOptionsBuilder<OctoberDbContext>()
                .UseSqlite(sqliteConnectionString)
                .Options);
        OctoberBatchRecord[] records = await database.Batches.AsNoTracking().ToArrayAsync();
        return records
            .OrderBy(record => record.BatchId)
            .Select(record => new SelectedWebBatchState(
                record.BatchId,
                record.TargetId,
                record.SubjectId,
                record.CanonicalDatabaseIdentity,
                record.SelectionDigest,
                record.ManagementAuthorityId,
                record.SelectedAtUtc,
                record.State))
            .ToImmutableArray();
    }

    private static void AssertLegacyRoutesAbsent(IServiceProvider services)
    {
        string[] mappedRoutes = services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();
        foreach (string route in new[]
        {
            BatchActivationIssue,
            BatchActivationValidation,
            PreparedSourceApprovalIssue,
            PreparedSourceApprovalVerification
        })
            Assert.DoesNotContain(route, mappedRoutes);
    }

    private static async Task<Dictionary<string, string>> GetSelectedHostLoginFieldsAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/Account/Login");
        response.EnsureSuccessStatusCode();
        string html = await response.Content.ReadAsStringAsync();
        Match[] loginForms = Regex.Matches(
                html,
                @"<form\b(?<attributes>[^>]*)>(?<content>.*?)</form>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Cast<Match>()
            .Where(form => string.Equals(
                ReadHtmlAttribute(form.Groups["attributes"].Value, "method"),
                "post",
                StringComparison.OrdinalIgnoreCase))
            .Where(form =>
            {
                Dictionary<string, string> values = ReadHtmlInputValues(form.Groups["content"].Value);
                bool namedLoginForm =
                    (values.TryGetValue("_handler", out string? handler) && handler == "login") ||
                    (values.TryGetValue("__formName", out string? formName) && formName == "login");
                string? action = ReadHtmlAttribute(form.Groups["attributes"].Value, "action");
                bool targetsLoginRoute = string.IsNullOrWhiteSpace(action) ||
                    (Uri.TryCreate(new Uri("http://localhost"), action, out Uri? actionUri) &&
                     string.Equals(actionUri.AbsolutePath, "/Account/Login", StringComparison.OrdinalIgnoreCase));
                return namedLoginForm && targetsLoginRoute;
            })
            .ToArray();
        Match loginForm = Assert.Single(loginForms);
        return ReadHtmlInputValues(loginForm.Groups["content"].Value);
    }

    private static Dictionary<string, string> ReadHtmlInputValues(string content)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(
                     content,
                     @"<input\b(?<attributes>[^>]*)>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            string attributes = input.Groups["attributes"].Value;
            string? name = ReadHtmlAttribute(attributes, "name");
            string? value = ReadHtmlAttribute(attributes, "value");
            if (name is not null && value is not null)
                values[name] = value;
        }
        return values;
    }

    private static string? ReadHtmlAttribute(string attributes, string attributeName)
    {
        Match attribute = Regex.Match(
            attributes,
            "(?:^|\\s)" + Regex.Escape(attributeName) +
                "\\s*=\\s*(?:\"(?<double>[^\"]*)\"|'(?<single>[^']*)'|(?<bare>[^\\s/>]+))",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!attribute.Success)
            return null;
        string value = attribute.Groups["double"].Success
            ? attribute.Groups["double"].Value
            : attribute.Groups["single"].Success
                ? attribute.Groups["single"].Value
                : attribute.Groups["bare"].Value;
        return WebUtility.HtmlDecode(value);
    }

    private sealed class SelectedWebRunFixture : IAsyncDisposable
    {
        private readonly Func<RecordingSecretEncryptionService?> _recordingEncryption;
        private readonly Func<ProductionNpgsqlDeclarations?> _productionDeclarations;

        internal SelectedWebRunFixture(
            OwnedSelectedWebIdentityDatabase identityDatabase,
            OwnedSelectedWebSqliteStore selectedDataStore,
            OwnedSelectedWebSqliteStore baselineStore,
            OwnedProviderDocument providerDocument,
            WebApplicationFactory<Program> baselineFactory,
            WebApplicationFactory<Program> selectedFactory,
            string subjectId,
            string selectedDataNpgsqlConnection,
            string selectedTargetIdentity,
            List<string> startupEvents,
            CountingSelectedWebCredentialSource credentialSource,
            Func<RecordingSecretEncryptionService?> recordingEncryption,
            Func<ProductionNpgsqlDeclarations?> productionDeclarations)
        {
            IdentityDatabase = identityDatabase;
            SelectedDataStore = selectedDataStore;
            BaselineStore = baselineStore;
            ProviderDocument = providerDocument;
            BaselineFactory = baselineFactory;
            SelectedFactory = selectedFactory;
            SubjectId = subjectId;
            SelectedDataNpgsqlConnection = selectedDataNpgsqlConnection;
            SelectedTargetIdentity = selectedTargetIdentity;
            StartupEvents = startupEvents;
            CredentialSource = credentialSource;
            _recordingEncryption = recordingEncryption;
            _productionDeclarations = productionDeclarations;
        }

        internal OwnedSelectedWebIdentityDatabase IdentityDatabase { get; }
        internal OwnedSelectedWebSqliteStore SelectedDataStore { get; }
        internal OwnedSelectedWebSqliteStore BaselineStore { get; }
        internal OwnedProviderDocument ProviderDocument { get; }
        internal WebApplicationFactory<Program> BaselineFactory { get; }
        internal WebApplicationFactory<Program> SelectedFactory { get; }
        internal string SubjectId { get; }
        internal string SelectedDataNpgsqlConnection { get; }
        internal string SelectedTargetIdentity { get; }
        internal List<string> StartupEvents { get; }
        internal CountingSelectedWebCredentialSource CredentialSource { get; }
        internal RecordingSecretEncryptionService? RecordingEncryption => _recordingEncryption();
        internal ProductionNpgsqlDeclarations? ProductionDeclarations => _productionDeclarations();

        public async ValueTask DisposeAsync()
        {
            try
            {
                SelectedFactory.Dispose();
            }
            finally
            {
                try
                {
                    BaselineFactory.Dispose();
                }
                finally
                {
                    try
                    {
                        ProviderDocument.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            SelectedDataStore.Dispose();
                        }
                        finally
                        {
                            try
                            {
                                BaselineStore.Dispose();
                            }
                            finally
                            {
                                await IdentityDatabase.DisposeAsync();
                            }
                        }
                    }
                }
            }
        }
    }

    private sealed record ProductionNpgsqlDeclarations(
        ImmutableArray<IDbContextOptionsConfiguration<ApplicationDbContext>> Identity,
        ImmutableArray<IDbContextOptionsConfiguration<VulgataDbContext>> Management);

    private sealed record SelectedWebBatchState(
        Guid BatchId,
        string TargetId,
        string SubjectId,
        string CanonicalDatabaseIdentity,
        string SelectionDigest,
        string ManagementAuthorityId,
        DateTimeOffset SelectedAtUtc,
        OctoberBatchState State);

    private sealed record SelectedWebConnectionTranslation(
        string BaselineNpgsqlConnection,
        string SelectedNpgsqlConnection,
        string BaselineSqliteConnection,
        string SelectedSqliteConnection)
    {
        public string ToSqliteConnection(string productionConnection) =>
            StringComparer.Ordinal.Equals(productionConnection, BaselineNpgsqlConnection)
                ? BaselineSqliteConnection
                : StringComparer.Ordinal.Equals(productionConnection, SelectedNpgsqlConnection)
                    ? SelectedSqliteConnection
                    : throw new InvalidOperationException(
                        "A production selected-data context chose an unowned connection.");
    }

    private sealed class CountingSelectedWebCredentialSource(
        string userName,
        string password,
        ICollection<string> events) : IOctoberOperatorCredentialSource
    {
        private int _readCount;
        public int ReadCount => Volatile.Read(ref _readCount);

        public ValueTask<OctoberOperatorCredentials?> ReadAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _readCount);
            events.Add("operator-credentials");
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<OctoberOperatorCredentials?>(
                new OctoberOperatorCredentials(userName, password));
        }
    }

    private sealed class RecordingSecretEncryptionService(
        ISecretEncryptionService inner,
        ICollection<string> events) : ISecretEncryptionService
    {
        public List<(string Purpose, string Ciphertext)> Decryptions { get; } = [];

        public Task<string> EncryptAsync(
            string purpose,
            string plain,
            CancellationToken cancellationToken = default) =>
            inner.EncryptAsync(purpose, plain, cancellationToken);

        public Task<string> DecryptAsync(
            string purpose,
            string cipher,
            CancellationToken cancellationToken = default)
        {
            Decryptions.Add((purpose, cipher));
            events.Add("provider-decrypt");
            return inner.DecryptAsync(purpose, cipher, cancellationToken);
        }
    }

    private sealed class OwnedProviderDocument(
        string directoryPath,
        string configurationFilePath,
        string keyRingPath,
        string applicationName,
        ServiceProvider protectionServices,
        IDataProtectionProvider dataProtectionProvider,
        string workerCiphertext,
        string chatCiphertext) : IDisposable
    {
        public string ConfigurationFilePath { get; } = configurationFilePath;
        public string KeyRingPath { get; } = keyRingPath;
        public string ApplicationName { get; } = applicationName;
        public IDataProtectionProvider DataProtectionProvider { get; } = dataProtectionProvider;
        public string WorkerCiphertext { get; } = workerCiphertext;
        public string ChatCiphertext { get; } = chatCiphertext;

        internal static async Task<OwnedProviderDocument> CreateAsync()
        {
            const string appName = "Vulgata.Tests.October.ProviderStartup";
            const string purpose = "Vulgata.LlmProviders.ApiKey";
            string directory = Path.Combine(Path.GetTempPath(), $"vulgata-provider-startup-{Guid.NewGuid():N}");
            string keyPath = Path.Combine(directory, "keys");
            string configPath = Path.Combine(directory, "providers.json");
            Directory.CreateDirectory(keyPath);
            ServiceProvider? providerToDispose = null;
            try
            {
                ServiceCollection services = new();
                services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
                    .SetApplicationName(appName);
                ServiceProvider provider = services.BuildServiceProvider();
                providerToDispose = provider;
                SecretEncryptionService encryption = new(provider.GetRequiredService<IDataProtectionProvider>());
                string workerCiphertext = await encryption.EncryptAsync(purpose, "synthetic-worker-api-key");
                string chatCiphertext = await encryption.EncryptAsync(purpose, "synthetic-chat-api-key");
                string document = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sourceIdentity = "selected-web-provider-startup-test",
                    precedence = "only-source",
                    workerProfile = "worker-primary",
                    chatProfile = "chat-primary",
                    profiles = new[]
                    {
                        new
                        {
                            name = "worker-primary",
                            apiType = "chat-completions",
                            endpoint = "https://worker.example.test/v1",
                            model = "worker-model",
                            credentialReference = "worker-key"
                        },
                        new
                        {
                            name = "chat-primary",
                            apiType = "responses",
                            endpoint = "https://chat.example.test/v1",
                            model = "chat-model",
                            credentialReference = "chat-key"
                        }
                    },
                    protectedCredentials = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["worker-key"] = workerCiphertext,
                        ["chat-key"] = chatCiphertext
                    }
                });
                await File.WriteAllTextAsync(
                    configPath, document, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return new OwnedProviderDocument(
                    directory, configPath, keyPath, appName, provider,
                    provider.GetRequiredService<IDataProtectionProvider>(),
                    workerCiphertext, chatCiphertext);
            }
            catch
            {
                providerToDispose?.Dispose();
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
                throw;
            }
        }

        public async Task WriteAlternativeValidDocumentAsync()
        {
            System.Text.Json.Nodes.JsonObject document = System.Text.Json.Nodes.JsonNode.Parse(
                await File.ReadAllTextAsync(ConfigurationFilePath))?.AsObject() ??
                throw new InvalidOperationException("The owned provider document was not a JSON object.");
            document["sourceIdentity"] = "selected-web-provider-after-startup";
            System.Text.Json.Nodes.JsonArray profiles = document["profiles"]?.AsArray() ??
                throw new InvalidOperationException("The owned provider profiles were not an array.");
            System.Text.Json.Nodes.JsonObject worker = profiles[0]?.AsObject() ??
                throw new InvalidOperationException("The owned worker profile was not an object.");
            worker["model"] = "worker-model-after-startup";
            await File.WriteAllTextAsync(
                ConfigurationFilePath, document.ToJsonString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public void Dispose()
        {
            try
            {
                protectionServices.Dispose();
            }
            finally
            {
                if (Directory.Exists(directoryPath))
                    Directory.Delete(directoryPath, recursive: true);
            }
        }
    }

    private sealed class OwnedSelectedWebSqliteStore : IDisposable
    {
        private readonly string _directory;

        private OwnedSelectedWebSqliteStore(string directory, string connectionString)
        {
            _directory = directory;
            ConnectionString = connectionString;
        }

        internal string ConnectionString { get; }

        internal static OwnedSelectedWebSqliteStore Create()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"vulgata-selected-web-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string databasePath = Path.Combine(directory, "selected-web.db");
            return new OwnedSelectedWebSqliteStore(
                directory,
                new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    ForeignKeys = true,
                    Pooling = false
                }.ToString());
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class OwnedSelectedWebIdentityDatabase : IAsyncDisposable
    {
        private const string DatabasePrefix = "vulgata_story184_webid_";
        private readonly string _administratorConnectionString;
        private bool _disposed;

        private OwnedSelectedWebIdentityDatabase(
            string administratorConnectionString,
            string connectionString,
            string canonicalDatabaseIdentity,
            string databaseName)
        {
            _administratorConnectionString = administratorConnectionString;
            ConnectionString = connectionString;
            CanonicalDatabaseIdentity = canonicalDatabaseIdentity;
            DatabaseName = databaseName;
        }

        internal string ConnectionString { get; }
        internal string CanonicalDatabaseIdentity { get; }
        private string DatabaseName { get; }

        internal static async Task<OwnedSelectedWebIdentityDatabase> CreateAsync()
        {
            string runId = Environment.GetEnvironmentVariable("VULGATA_TEST_RESOURCE_RUN") ?? string.Empty;
            if (!Guid.TryParseExact(runId, "N", out _))
                throw new InvalidOperationException("The E2E suite must supply its owned run identifier.");
            string administrator = TestResourceScope.RequireAdministrator();
            TestResourceScope.ValidateAdministrator(administrator);
            NpgsqlConnectionStringBuilder admin = new(administrator)
            {
                Pooling = false,
                Timeout = 5,
                CommandTimeout = 15
            };
            string databaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
            bool created = false;
            string? target = null;
            try
            {
                await using (NpgsqlConnection connection = new(admin.ConnectionString))
                {
                    await connection.OpenAsync();
                    int version = Convert.ToInt32(await new NpgsqlCommand(
                        "SELECT current_setting('server_version_num')::integer;", connection)
                        .ExecuteScalarAsync()) / 10000;
                    if (version != 17)
                        throw new InvalidOperationException("The owned PostgreSQL fixture must be version 17.");
                    await using NpgsqlCommand exists = new(
                        "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);", connection);
                    exists.Parameters.AddWithValue("name", databaseName);
                    if ((bool)(await exists.ExecuteScalarAsync() ?? true))
                        throw new InvalidOperationException("The generated owned database name already exists.");
                    await using NpgsqlCommand create = new($"CREATE DATABASE \"{databaseName}\";", connection);
                    await create.ExecuteNonQueryAsync();
                    created = true;
                    TestResourceScope.RecordCreated(admin.ConnectionString, databaseName);
                }

                NpgsqlConnectionStringBuilder owned = new(admin.ConnectionString)
                {
                    Database = databaseName,
                    Pooling = false,
                    Timeout = 5,
                    CommandTimeout = 30
                };
                target = owned.ConnectionString;
                TestResourceScope.ValidateOwnedTarget(target);
                string canonical = $"postgres://{owned.Host}:{owned.Port}/{databaseName}";
                return new OwnedSelectedWebIdentityDatabase(admin.ConnectionString, target, canonical, databaseName);
            }
            catch
            {
                if (created)
                    await DropOwnedDatabaseAsync(admin.ConnectionString, databaseName,
                        target ?? new NpgsqlConnectionStringBuilder(admin.ConnectionString)
                        {
                            Database = databaseName,
                            Pooling = false
                        }.ConnectionString);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            await DropOwnedDatabaseAsync(_administratorConnectionString, DatabaseName, ConnectionString);
        }

        private static async Task DropOwnedDatabaseAsync(
            string administrator,
            string databaseName,
            string targetConnectionString)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    databaseName, "^vulgata_story184_webid_[0-9a-f]{32}$",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Refusing to drop a database outside the owned Identity fixture name.");
            TestResourceScope.ValidateOwnedTarget(targetConnectionString);
            NpgsqlConnectionStringBuilder admin = new(administrator) { Pooling = false, Timeout = 5 };
            TestResourceScope.ValidateAdministrator(admin.ConnectionString);
            await using NpgsqlConnection connection = new(admin.ConnectionString);
            await connection.OpenAsync();
            await using (NpgsqlCommand terminate = new(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid();",
                connection))
            {
                terminate.Parameters.AddWithValue("name", databaseName);
                await terminate.ExecuteNonQueryAsync();
            }
            await using NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS \"{databaseName}\";", connection);
            await drop.ExecuteNonQueryAsync();
            await using NpgsqlCommand exists = new(
                "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);", connection);
            exists.Parameters.AddWithValue("name", databaseName);
            if ((bool)(await exists.ExecuteScalarAsync() ?? true))
                throw new InvalidOperationException("The owned Identity database remained after cleanup.");
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        internal static EmptyServiceProvider Instance { get; } = new();
        public object? GetService(Type serviceType) => null;
    }
}

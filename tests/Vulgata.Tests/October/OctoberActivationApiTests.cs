using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Identity;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using Vulgata.Tests;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October;

public sealed class OctoberActivationApiTests
{
    private const string TargetA = "october-target-a";
    private const string TargetB = "october-target-b";
    private const string FingerprintA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void StartupSelector_AuthorizationOnlyMode_SelectsOnlyBootstrap()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["October:Web:Mode"] = "authorization-only",
            })
            .Build();

        OctoberWebStartupSelection selection = OctoberWebStartupSelector.Resolve(configuration);

        Assert.Equal(
            new OctoberWebStartupSelection(OctoberWebMode.AuthorizationOnly, null, null, null),
            selection);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void StartupSelector_AbsentModeKeepsOrdinaryStartup()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        OctoberWebStartupSelection selection = OctoberWebStartupSelector.Resolve(configuration);

        Assert.Equal(new OctoberWebStartupSelection(OctoberWebMode.Ordinary, null, null, null), selection);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorizationOnlyProgram_MapsOnlyGrantIssuanceAndValidationRoutes()
    {
        using BootstrapWebHost host = CreateAuthorizationOnlyHost(new OctoberCertificateIdentityResult(
            OctoberCertificateIdentityStatus.Authenticated,
            FingerprintA));
        using HttpClient client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        string[] routes = ReadOctoberRoutes(host.Factory.Services);

        Assert.Equal(
            [
                "POST /api/october-batch-activation-grants",
                "POST /api/october-batch-activation-grants/validations",
            ],
            routes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorizationOnlyProgram_RegistersVerifierButNoOctoberDataServices()
    {
        using BootstrapWebHost host = CreateAuthorizationOnlyHost(new OctoberCertificateIdentityResult(
            OctoberCertificateIdentityStatus.Authenticated,
            FingerprintA));
        _ = host.Factory.CreateClient();

        using IServiceScope scope = host.Factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<OctoberGeneratorCertificateVerifier>());
        Assert.NotNull(scope.ServiceProvider.GetService<IOctoberAuthenticatedCertificateSource>());
        Assert.Null(scope.ServiceProvider.GetService<OctoberDbContext>());
        Assert.Null(scope.ServiceProvider.GetService<OctoberBatchSetupPolicy>());
        Assert.Null(scope.ServiceProvider.GetService<IOctoberSetupWriter>());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorizationOnlyProgram_UsesOneSelectionFromFinalHostConfiguration()
    {
        using BootstrapWebHost host = CreateAuthorizationOnlyHost(new OctoberCertificateIdentityResult(
            OctoberCertificateIdentityStatus.Authenticated,
            FingerprintA));
        _ = host.Factory.CreateClient();

        IConfiguration configuration = host.Factory.Services.GetRequiredService<IConfiguration>();
        OctoberWebStartupSelection[] selections = host.Factory.Services
            .GetServices<OctoberWebStartupSelection>()
            .ToArray();
        OctoberWebStartupSelection selection = Assert.Single(selections);

        Assert.Equal("authorization-only", configuration["October:Web:Mode"]);
        Assert.Equal(
            new OctoberWebStartupSelection(OctoberWebMode.AuthorizationOnly, null, null, null),
            selection);
        Assert.Same(selection, host.Factory.Services.GetRequiredService<OctoberWebStartupSelection>());
        Assert.Same(selection, host.Factory.Services.GetRequiredService<OctoberWebStartupSelection>());
        Assert.Equal(
            [
                "POST /api/october-batch-activation-grants",
                "POST /api/october-batch-activation-grants/validations",
            ],
            ReadOctoberRoutes(host.Factory.Services));
        Assert.NotNull(host.Factory.Services.GetRequiredService<OctoberGeneratorCertificateVerifier>());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorizationOnlyProgram_ConfigurationChangesAfterBuildDoNotChangeSelectionOrRoutes()
    {
        using BootstrapWebHost host = CreateAuthorizationOnlyHost(new OctoberCertificateIdentityResult(
            OctoberCertificateIdentityStatus.Authenticated,
            FingerprintA));
        _ = host.Factory.CreateClient();
        IConfiguration configuration = host.Factory.Services.GetRequiredService<IConfiguration>();
        OctoberWebStartupSelection selection =
            host.Factory.Services.GetRequiredService<OctoberWebStartupSelection>();
        string[] routesBeforeChange = ReadOctoberRoutes(host.Factory.Services);

        configuration["October:Web:Mode"] = "ordinary";

        Assert.Equal("ordinary", configuration["October:Web:Mode"]);
        Assert.Same(selection, host.Factory.Services.GetRequiredService<OctoberWebStartupSelection>());
        Assert.Equal(OctoberWebMode.AuthorizationOnly, selection.Mode);
        Assert.Equal(routesBeforeChange, ReadOctoberRoutes(host.Factory.Services));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("October:Web:Mode", "unknown-mode")]
    [InlineData("October:Web:GeneratorCertificates:0:TargetIds:0", null)]
    [InlineData("October:Web:TargetId", TargetA)]
    [InlineData("October:Web:Mode", "selected-data")]
    public void InvalidAuthorizationOnlyConfiguration_FailsBeforeHostServes(
        string key,
        string? value)
    {
        using BootstrapWebHost host = CreateAuthorizationOnlyHost(
            new OctoberCertificateIdentityResult(OctoberCertificateIdentityStatus.Unauthenticated, null),
            new Dictionary<string, string?> { [key] = value });

        Exception exception = Assert.ThrowsAny<Exception>(() => host.Factory.CreateClient());

        Assert.Contains("Invalid October Web", exception.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizationOnlyValidation_UsesControlledSourceThroughConcreteVerifier()
    {
        ControlledCertificateSource source = new(new OctoberCertificateIdentityResult(
            OctoberCertificateIdentityStatus.Authenticated,
            FingerprintA));
        using BootstrapWebHost host = CreateAuthorizationOnlyHost(
            new OctoberCertificateIdentityResult(OctoberCertificateIdentityStatus.Unauthenticated, null),
            controlledSource: source);
        using HttpClient client = CreateClient(host.Factory);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/october-batch-activation-grants/validations",
            CreateGrantValidationRequest());

        await AssertProblemDetailsAsync(response, HttpStatusCode.Forbidden);
        Assert.Same(source, host.Factory.Services.GetRequiredService<IOctoberAuthenticatedCertificateSource>());
        Assert.IsType<OctoberGeneratorCertificateVerifier>(
            host.Factory.Services.GetRequiredService<OctoberGeneratorCertificateVerifier>());
        Assert.Equal(1, source.ReadCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ManagementAccessOperator_IssuesAndValidatesCanonicalSystemAndStandaloneGrant()
    {
        ControlledCertificateSource source = new(new OctoberCertificateIdentityResult(
            OctoberCertificateIdentityStatus.Authenticated,
            FingerprintA));
        using BootstrapWebHost host = CreateAuthorizationOnlyHost(
            new OctoberCertificateIdentityResult(OctoberCertificateIdentityStatus.Unauthenticated, null),
            controlledSource: source);
        using HttpClient client = CreateClient(host.Factory);
        string subjectId = await CreateManagementAdministratorAsync(host.Factory.Services);
        await LoginAsync(client, "october.operator@example.com", "Valid1!Pass");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Vulgata.Core.Entities.System system = new("October system", null, "system advisory", now);
        Repository systemRepository = system.AddRepository(
            "Member repository", "https://example.test/member.git", null, "repository advisory", now);
        Repository standaloneRepository = Repository.CreateStandalone(
            "Standalone repository", "https://example.test/standalone.git", null, "standalone advisory", now);
        GlobalContext globalContext = new("global advisory", now);
        await using (AsyncServiceScope scope = host.Factory.Services.CreateAsyncScope())
        {
            VulgataDbContext database = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            database.Systems.Add(system);
            database.Repositories.Add(standaloneRepository);
            database.GlobalContexts.Add(globalContext);
            database.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(system.Id, subjectId, now));
            await database.SaveChangesAsync();
        }

        Guid batchId = Guid.Parse("d216e6c4-a859-4f72-85af-001066ad09e1");
        var memberSelection = new
        {
            repositoryId = systemRepository.Id,
            systemId = (Guid?)system.Id,
            rootBindingIdentity = "member-root-v1",
            operatorLabel = "October operator",
        };
        var standaloneSelection = new
        {
            repositoryId = standaloneRepository.Id,
            systemId = (Guid?)null,
            rootBindingIdentity = "standalone-root-v1",
            operatorLabel = "October operator",
        };
        object[] repositorySelections = [memberSelection, standaloneSelection];
        var contextSelections = new[]
        {
            new { scope = "global", recordId = globalContext.Id },
            new { scope = "system", recordId = system.Id },
            new { scope = "repository", recordId = systemRepository.Id },
            new { scope = "repository", recordId = standaloneRepository.Id },
        };
        object[] orderedContexts = contextSelections.Cast<object>().ToArray();
        object request = CreateAuthenticatedGrantRequest(
            TargetA, batchId, repositorySelections, orderedContexts);

        using HttpResponseMessage issued = await client.PostAsJsonAsync(
            "/api/october-batch-activation-grants", request);

        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        using JsonDocument issuedBody = JsonDocument.Parse(await issued.Content.ReadAsByteArrayAsync());
        JsonElement issuedJson = issuedBody.RootElement;
        string activationGrant = issuedJson.GetProperty("activationGrant").GetString()!;
        string selectionDigest = issuedJson.GetProperty("selectionDigest").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(activationGrant));
        Assert.Equal(TargetA, issuedJson.GetProperty("targetId").GetString());
        Assert.Equal(batchId, issuedJson.GetProperty("batchId").GetGuid());
        Assert.Matches("^[0-9a-f]{64}$", selectionDigest);
        Assert.Equal(subjectId, issuedJson.GetProperty("subjectId").GetString());

        object reorderedRequest = CreateAuthenticatedGrantRequest(
            TargetA,
            batchId,
            repositorySelections.Reverse().ToArray(),
            orderedContexts.Reverse().ToArray());
        using HttpResponseMessage reordered = await client.PostAsJsonAsync(
            "/api/october-batch-activation-grants", reorderedRequest);
        Assert.Equal(HttpStatusCode.Created, reordered.StatusCode);
        using JsonDocument reorderedBody = JsonDocument.Parse(await reordered.Content.ReadAsByteArrayAsync());
        Assert.Equal(selectionDigest, reorderedBody.RootElement.GetProperty("selectionDigest").GetString());

        object validationRequest = CreateGrantValidationRequest(
            activationGrant, TargetA, batchId, repositorySelections, orderedContexts);
        using HttpResponseMessage validated = await client.PostAsJsonAsync(
            "/api/october-batch-activation-grants/validations", validationRequest);

        Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
        using JsonDocument validatedBody = JsonDocument.Parse(await validated.Content.ReadAsByteArrayAsync());
        Assert.Equal(subjectId, validatedBody.RootElement.GetProperty("subjectId").GetString());
        Assert.Equal(TargetA, validatedBody.RootElement.GetProperty("targetId").GetString());
        Assert.Equal(batchId, validatedBody.RootElement.GetProperty("batchId").GetGuid());
        Assert.Equal(selectionDigest, validatedBody.RootElement.GetProperty("selectionDigest").GetString());
        Assert.Equal(1, source.ReadCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizationOnlyGrantIssuance_AnonymousRequestGetsProblem401()
    {
        using BootstrapWebHost host = CreateAuthorizationOnlyHost(new OctoberCertificateIdentityResult(
            OctoberCertificateIdentityStatus.Unauthenticated,
            null));
        using HttpClient client = CreateClient(host.Factory);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/october-batch-activation-grants",
            CreateGrantRequest());

        await AssertProblemDetailsAsync(response, HttpStatusCode.Unauthorized);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizationOnlyGrantValidation_WithoutCertificateGetsProblem401()
    {
        using BootstrapWebHost host = CreateAuthorizationOnlyHost(new OctoberCertificateIdentityResult(
            OctoberCertificateIdentityStatus.Unauthenticated,
            null));
        using HttpClient client = CreateClient(host.Factory);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/october-batch-activation-grants/validations",
            CreateGrantValidationRequest());

        await AssertProblemDetailsAsync(response, HttpStatusCode.Unauthorized);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void OrdinaryProgram_MapsNoOctoberRoutesOrDataServices()
    {
        using BootstrapWebHost host = CreateOrdinaryHost();
        _ = host.Factory.CreateClient();

        string[] octoberRoutes = ReadOctoberRoutes(host.Factory.Services);
        Assert.Empty(octoberRoutes);

        using IServiceScope scope = host.Factory.Services.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<OctoberGeneratorCertificateVerifier>());
        Assert.Null(scope.ServiceProvider.GetService<OctoberDbContext>());
        Assert.Null(scope.ServiceProvider.GetService<OctoberBatchSetupPolicy>());
        Assert.Null(scope.ServiceProvider.GetService<IOctoberSetupWriter>());
        Assert.Null(scope.ServiceProvider.GetService<IOctoberAuthenticatedCertificateSource>());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(OctoberCertificateIdentityStatus.Authenticated, FingerprintA, TargetA, OctoberGeneratorCertificateStatus.Authorized)]
    [InlineData(OctoberCertificateIdentityStatus.Authenticated, FingerprintA, TargetB, OctoberGeneratorCertificateStatus.Forbidden)]
    [InlineData(OctoberCertificateIdentityStatus.Authenticated, FingerprintB, TargetA, OctoberGeneratorCertificateStatus.Forbidden)]
    [InlineData(OctoberCertificateIdentityStatus.Authenticated, null, TargetA, OctoberGeneratorCertificateStatus.Forbidden)]
    [InlineData(OctoberCertificateIdentityStatus.Unauthenticated, FingerprintA, TargetA, OctoberGeneratorCertificateStatus.Unauthenticated)]
    [InlineData(OctoberCertificateIdentityStatus.AuthorityUnavailable, null, TargetA, OctoberGeneratorCertificateStatus.AuthorityUnavailable)]
    [InlineData(OctoberCertificateIdentityStatus.Cancelled, null, TargetA, OctoberGeneratorCertificateStatus.Cancelled)]
    public async Task CertificateVerifier_UsesAuthenticatedFingerprintAndExactTargetScope(
        OctoberCertificateIdentityStatus identityStatus,
        string? fingerprint,
        string targetId,
        OctoberGeneratorCertificateStatus expectedStatus)
    {
        ControlledCertificateSource source = new(new OctoberCertificateIdentityResult(identityStatus, fingerprint));
        OctoberGeneratorCertificateVerifier verifier = new(source, CreateAllowlist());

        OctoberGeneratorCertificateDecision decision = await verifier.VerifyAsync(
            new DefaultHttpContext(),
            targetId,
            CancellationToken.None);

        Assert.Equal(expectedStatus, decision.Status);
        Assert.Equal(1, source.ReadCount);
        Assert.Equal(CancellationToken.None, source.CancellationToken);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CertificateVerifier_DoesNotTreatRequestHeaderCookieOrBodyAsCertificateProof()
    {
        ControlledCertificateSource source = new(new OctoberCertificateIdentityResult(
            OctoberCertificateIdentityStatus.Unauthenticated,
            FingerprintA));
        OctoberGeneratorCertificateVerifier verifier = new(source, CreateAllowlist());
        DefaultHttpContext context = new();
        context.Request.Headers["X-Client-Certificate-Fingerprint"] = FingerprintA;
        context.Request.Headers.Cookie = $"ClientCertificate={FingerprintA}";
        context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            $$"""{"certificateFingerprint":"{{FingerprintA}}"}"""));

        OctoberGeneratorCertificateDecision decision = await verifier.VerifyAsync(
            context,
            TargetA,
            CancellationToken.None);

        Assert.Equal(OctoberGeneratorCertificateStatus.Unauthenticated, decision.Status);
        Assert.Equal(1, source.ReadCount);
    }

    private static OctoberGeneratorCertificateAllowlist CreateAllowlist() => new(
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [TargetA] = new HashSet<string>([FingerprintA], StringComparer.Ordinal),
            [TargetB] = new HashSet<string>([FingerprintB], StringComparer.Ordinal),
        });

    private static BootstrapWebHost CreateAuthorizationOnlyHost(
        OctoberCertificateIdentityResult certificateIdentity,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        ControlledCertificateSource? controlledSource = null)
    {
        LoginLogoutTests.CustomWebApplicationFactory ordinaryFactory = new();
        WebApplicationFactory<Program> factory = ConfigureFactory(
            ordinaryFactory,
            authorizationOnly: true,
            certificateIdentity: certificateIdentity,
            configurationOverrides: configurationOverrides,
            controlledSource: controlledSource);

        return new BootstrapWebHost(ordinaryFactory, factory);
    }

    private static BootstrapWebHost CreateOrdinaryHost()
    {
        LoginLogoutTests.CustomWebApplicationFactory ordinaryFactory = new();
        WebApplicationFactory<Program> factory = ConfigureFactory(
            ordinaryFactory,
            authorizationOnly: false,
            certificateIdentity: new OctoberCertificateIdentityResult(
                OctoberCertificateIdentityStatus.Unauthenticated,
                null));

        return new BootstrapWebHost(ordinaryFactory, factory);
    }

    private static WebApplicationFactory<Program> ConfigureFactory(
        LoginLogoutTests.CustomWebApplicationFactory ordinaryFactory,
        bool authorizationOnly,
        OctoberCertificateIdentityResult certificateIdentity,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        ControlledCertificateSource? controlledSource = null) =>
        ordinaryFactory.WithWebHostBuilder(builder =>
        {
            if (authorizationOnly)
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    Dictionary<string, string?> settings = new(StringComparer.Ordinal)
                    {
                        ["October:Web:Mode"] = "authorization-only",
                        ["October:Web:AllowedTargets:0:TargetId"] = TargetA,
                        ["October:Web:AllowedTargets:0:CanonicalDatabaseIdentity"] =
                            "postgres://test-cluster/october-target-a",
                        ["October:Web:AllowedTargets:1:TargetId"] = TargetB,
                        ["October:Web:AllowedTargets:1:CanonicalDatabaseIdentity"] =
                            "postgres://test-cluster/october-target-b",
                        ["October:Web:GeneratorCertificates:0:Sha256Fingerprint"] = FingerprintA,
                        ["October:Web:GeneratorCertificates:0:TargetIds:0"] = TargetA,
                        ["October:Web:GeneratorCertificates:1:Sha256Fingerprint"] = FingerprintB,
                        ["October:Web:GeneratorCertificates:1:TargetIds:0"] = TargetB,
                    };
                    if (configurationOverrides is not null)
                    {
                        foreach ((string key, string? value) in configurationOverrides)
                        {
                            settings[key] = value;
                        }
                    }

                    configuration.AddInMemoryCollection(settings);
                });
            }

            if (!authorizationOnly)
            {
                return;
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOctoberAuthenticatedCertificateSource>();
                services.AddSingleton<IOctoberAuthenticatedCertificateSource>(
                    controlledSource ?? new ControlledCertificateSource(certificateIdentity));
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private static string[] ReadOctoberRoutes(IServiceProvider services) =>
        services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint =>
            {
                string route = endpoint.RoutePattern.RawText ?? string.Empty;
                return route.StartsWith("/api/october-", StringComparison.Ordinal)
                    ? endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                        .Select(method => $"{method} {route}") ?? []
                    : [];
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static object CreateGrantRequest() => new
    {
        targetId = TargetA,
        batchId = Guid.Parse("d216e6c4-a859-4f72-85af-001066ad09e1"),
        repositories = new[]
        {
            new
            {
                repositoryId = Guid.Parse("7a9fb1f8-9683-409b-9649-97c8420cbd1b"),
                systemId = (Guid?)null,
                rootBindingIdentity = "owned-root-v1",
                operatorLabel = "operator-choice",
            },
        },
        contextSelections = Array.Empty<object>(),
    };

    private static object CreateGrantValidationRequest() => new
    {
        activationGrant = "protected-grant-fixture",
        targetId = TargetA,
        batchId = Guid.Parse("d216e6c4-a859-4f72-85af-001066ad09e1"),
        repositories = new[]
        {
            new
            {
                repositoryId = Guid.Parse("7a9fb1f8-9683-409b-9649-97c8420cbd1b"),
                systemId = (Guid?)null,
                rootBindingIdentity = "owned-root-v1",
                operatorLabel = "operator-choice",
            },
        },
        contextSelections = Array.Empty<object>(),
    };

    private static object CreateAuthenticatedGrantRequest(
        string targetId,
        Guid batchId,
        object[] repositories,
        object[] contextSelections) => new
        {
            targetId,
            batchId,
            repositories,
            contextSelections,
        };

    private static object CreateGrantValidationRequest(
        string activationGrant,
        string targetId,
        Guid batchId,
        object[] repositories,
        object[] contextSelections) => new
        {
            activationGrant,
            targetId,
            batchId,
            repositories,
            contextSelections,
        };

    private static async Task<string> CreateManagementAdministratorAsync(IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = new()
        {
            UserName = "october.operator@example.com",
            Email = "october.operator@example.com",
            EmailConfirmed = true,
        };

        IdentityResult created = await userManager.CreateAsync(user, "Valid1!Pass");
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        IdentityResult assigned = await userManager.AddToRoleAsync(user, RoleNames.Administrator);
        Assert.True(assigned.Succeeded, string.Join("; ", assigned.Errors.Select(error => error.Description)));
        return user.Id;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        Dictionary<string, string> fields = await GetLoginFormFieldsAsync(client);
        fields["Input.Email"] = email;
        fields["Input.Password"] = password;
        fields["Input.RememberMe"] = "false";
        using HttpResponseMessage response = await client.PostAsync(
            "/Account/Login", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Dictionary<string, string> authenticatedFields = await GetLoginFormFieldsAsync(client);
        string token = authenticatedFields.Single(
            pair => pair.Key.Contains("RequestVerificationToken", StringComparison.Ordinal)).Value;
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.TryAddWithoutValidation("RequestVerificationToken", token);
    }

    private static async Task<Dictionary<string, string>> GetLoginFormFieldsAsync(HttpClient client)
    {
        using HttpResponseMessage page = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        string html = await page.Content.ReadAsStringAsync();
        Match form = Regex.Match(
            html,
            "<form\\b(?=[^>]*method=\"post\")[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, "The rendered Identity login POST form is required for antiforgery tokens.");
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(form.Groups["content"].Value, "<input\\b[^>]*>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            string name = Regex.Match(input.Value, "(?:^|\\s)name=\"(?<value>[^\"]*)\"",
                RegexOptions.IgnoreCase).Groups["value"].Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string value = Regex.Match(input.Value, "(?:^|\\s)value=\"(?<value>[^\"]*)\"",
                RegexOptions.IgnoreCase).Groups["value"].Value;
            fields[WebUtility.HtmlDecode(name)] = WebUtility.HtmlDecode(value);
        }

        Assert.Contains(fields.Keys, key => key.Contains("RequestVerificationToken", StringComparison.Ordinal));
        return fields;
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal((int)expectedStatus, body.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("<html", body.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class BootstrapWebHost(
        LoginLogoutTests.CustomWebApplicationFactory ordinaryFactory,
        WebApplicationFactory<Program> factory) : IDisposable
    {
        public WebApplicationFactory<Program> Factory { get; } = factory;

        public void Dispose()
        {
            Factory.Dispose();
            ordinaryFactory.Dispose();
        }
    }

    private sealed class ControlledCertificateSource(OctoberCertificateIdentityResult result)
        : IOctoberAuthenticatedCertificateSource
    {
        public int ReadCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<OctoberCertificateIdentityResult> ReadAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            CancellationToken = cancellationToken;
            return Task.FromResult(result);
        }
    }
}

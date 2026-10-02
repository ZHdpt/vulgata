using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Tests;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed class OctoberActivationApiTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OrdinaryProgram_DoesNotExposeFormerBatchActivationRoutes()
    {
        using LoginLogoutTests.CustomWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        OctoberLegacyAuthorityRoutes.AssertAbsent(
            factory.Services,
            OctoberLegacyAuthorityRoutes.BatchActivationIssue,
            OctoberLegacyAuthorityRoutes.BatchActivationValidation);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("October:Web:Mode", "authorization-only")]
    [InlineData("October:Web:Endpoint", "https://obsolete.example.test")]
    [InlineData("October:Web:ActivationGrant", "obsolete-grant")]
    [InlineData("October:Web:ManagementAuthorityId", "obsolete-authority")]
    [InlineData("October:Web:AllowedTargets:0:TargetId", "obsolete-target")]
    [InlineData("October:Web:GeneratorCertificates:0:Sha256Fingerprint", "obsolete-fingerprint")]
    public async Task FormerWebModeOrAuthorityConfiguration_LeavesOrdinaryHostAvailable(
        string key, string value)
    {
        using LoginLogoutTests.CustomWebApplicationFactory ordinaryFactory = new();
        using WebApplicationFactory<Program> factory = ordinaryFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [key] = value })));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage login = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using HttpResponseMessage protectedPage = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        string location = protectedPage.Headers.Location?.OriginalString
            ?? throw new InvalidOperationException("Expected the ordinary authorization redirect to identify login.");
        Assert.Contains("/Account/Login", location, StringComparison.Ordinal);

        OctoberLegacyAuthorityRoutes.AssertAbsent(
            factory.Services,
            OctoberLegacyAuthorityRoutes.BatchActivationIssue,
            OctoberLegacyAuthorityRoutes.BatchActivationValidation);
    }
}

internal static class OctoberLegacyAuthorityRoutes
{
    public const string BatchActivationIssue = "/api/october-batch-activation-grants";
    public const string BatchActivationValidation = "/api/october-batch-activation-grants/validations";
    public const string PreparedSourceApprovalIssue = "/api/october-prepared-source-approvals";
    public const string PreparedSourceApprovalVerification =
        "/api/october-prepared-source-approvals/verifications";

    public static void AssertAbsent(IServiceProvider services, params string[] paths)
    {
        string[] mappedRoutes = services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        foreach (string path in paths)
            Assert.DoesNotContain(path, mappedRoutes);
    }
}

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
    public void ObsoleteWebModeOrAuthorityConfiguration_FailsBeforeHostServes(string key, string value)
    {
        using LoginLogoutTests.CustomWebApplicationFactory ordinaryFactory = new();
        using WebApplicationFactory<Program> factory = ordinaryFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [key] = value })));

        Exception startupFailure = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Invalid October Web startup selection.",
            startupFailure.ToString(), StringComparison.Ordinal);
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

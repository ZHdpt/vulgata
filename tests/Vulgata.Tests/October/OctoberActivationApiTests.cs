using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Vulgata.Tests;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed class OctoberActivationApiTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task FormerWebAuthorityConfiguration_LeavesOrdinaryHostAvailableWithoutFormerRoutes()
    {
        using LoginLogoutTests.CustomWebApplicationFactory ordinaryFactory = new();
        using WebApplicationFactory<Program> factory = ordinaryFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["October:Web:Mode"] = "authorization-only",
                    ["October:Web:Endpoint"] = "https://obsolete.example.test",
                    ["October:Web:ActivationGrant"] = "obsolete-grant",
                    ["October:Web:ManagementAuthorityId"] = "obsolete-authority",
                    ["October:Web:AllowedTargets:0:TargetId"] = "obsolete-target",
                    ["October:Web:GeneratorCertificates:0:Sha256Fingerprint"] = "obsolete-fingerprint"
                })));
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
            OctoberLegacyAuthorityRoutes.BatchActivationValidation,
            OctoberLegacyAuthorityRoutes.PreparedSourceApprovalIssue,
            OctoberLegacyAuthorityRoutes.PreparedSourceApprovalVerification);
    }
}

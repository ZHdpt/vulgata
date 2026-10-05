using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Vulgata.Tests.October;

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

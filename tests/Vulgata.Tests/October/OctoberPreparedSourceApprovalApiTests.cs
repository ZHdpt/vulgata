using Vulgata.Tests;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October;

public sealed class OctoberPreparedSourceApprovalApiTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OrdinaryProgram_DoesNotExposeFormerPreparedSourceApprovalRoutes()
    {
        using LoginLogoutTests.CustomWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        OctoberLegacyAuthorityRoutes.AssertAbsent(
            factory.Services,
            OctoberLegacyAuthorityRoutes.PreparedSourceApprovalIssue,
            OctoberLegacyAuthorityRoutes.PreparedSourceApprovalVerification);
    }
}

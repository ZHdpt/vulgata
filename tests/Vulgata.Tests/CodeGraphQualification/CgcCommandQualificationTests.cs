namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class CgcCommandQualificationTests
{
    [Fact]
    public void CreateContract_WithSyntheticWholeRepository_DeclaresExactApprovedLifecycle()
    {
        CgcQualificationRunner runner = new(
            QualificationTestPaths.RepositoryRoot,
            TimeSpan.FromSeconds(1_800),
            new FakeQualificationProcessLauncher());

        CgcQualificationContract contract = runner.CreateContract();

        Assert.Equal(
            [
                "cgc-version",
                "cgc-doctor",
                "cgc-context-create",
                "cgc-context-mode",
                "cgc-context-default",
                "cgc-index",
                "cgc-api-start",
                "cgc-api-health",
                "cgc-api-query",
                "gateway-stop",
                "cgc-context-delete",
            ],
            contract.LifecycleEntries);
        Assert.Equal(11, contract.LifecycleEntries.Distinct(StringComparer.Ordinal).Count());
    }
}

namespace Vulgata.Tests.PreparedScanning;

public sealed class Story12_2BoundaryTests
{
    public const string AcceptedSourceCommit =
        "6fe7d4a88ff344b1d71329d19fdb9d6d6d64b7e6";
    public const string AuthorizedScopeSha256 =
        "a77c1118e1c4a33ab43094b554745fa2b1297c749a7853e63d9834e43a53e150";
    public const string AcceptedPlanReview = "plan-review-03";
    public const string AuthorizationDecision = "planned-change-authorization-01";

    [Fact]
    public void AcceptedDefinitionBoundary_RecordedInputs_MatchDurableArtifacts()
    {
        string repositoryRoot = FindRepositoryRoot();
        string plan = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "specs",
            "011-java-xml-prescan-manifest",
            "plan.md"));
        string review = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "specs",
            "011-java-xml-prescan-manifest",
            "reviews",
            "plan-review",
            $"{AcceptedPlanReview}.json"));
        string decision = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "specs",
            "011-java-xml-prescan-manifest",
            "decisions",
            $"{AuthorizationDecision}.md"));

        Assert.Contains(AcceptedSourceCommit, plan, StringComparison.Ordinal);
        Assert.Contains("\"verdict\": \"ACCEPT\"", review, StringComparison.Ordinal);
        Assert.Contains(AuthorizedScopeSha256, decision, StringComparison.Ordinal);
        Assert.Contains("**Status:** Accepted", decision, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Vulgata repository root.");
    }
}

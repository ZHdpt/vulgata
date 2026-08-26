namespace Vulgata.Tests.PreparedPass2;

internal static class Pass2PrivacyAssertions
{
    private static readonly string[] ForbiddenContentMarkers =
    [
        "api_key",
        "authorization:",
        "bearer ",
        "connectionstring",
        "password=",
        "repositoryroot",
        "providerrequest",
        "providerresponse",
        "reasoning",
        "sourcecontent",
        "source root",
        "absolute path",
    ];

    internal static void AssertContentDark(string? value)
    {
        Assert.NotNull(value);
        foreach (string marker in ForbiddenContentMarkers)
        {
            Assert.DoesNotContain(marker, value!, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static void AssertNoSecretsOrPaths(IEnumerable<string?> values)
    {
        foreach (string? value in values)
        {
            AssertContentDark(value);
            if (value is not null)
            {
                Assert.DoesNotContain("\\", value, StringComparison.Ordinal);
                Assert.DoesNotContain("/", value, StringComparison.Ordinal);
            }
        }
    }
}

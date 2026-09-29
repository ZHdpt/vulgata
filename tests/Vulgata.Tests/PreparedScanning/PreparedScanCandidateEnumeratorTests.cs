using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Infrastructure.Git;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T015 RED coverage for canonical Java/XML enumeration and closed filtering.
/// The concrete enumerator is intentionally discovered through the accepted
/// Infrastructure seam so the test remains compilable before T016 exists.
/// </summary>
public sealed class PreparedScanCandidateEnumeratorTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedCandidateEnumerator_ExposesDeterministicAsyncInventoryPort()
    {
        Type enumerator = RequirePublicType(
            typeof(ScanCandidateEnumerator).Assembly,
            "Vulgata.Infrastructure.Git.PreparedScanCandidateEnumerator");

        MethodInfo method = Assert.Single(
            enumerator.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            candidate => candidate.Name == "EnumerateAsync");

        Assert.True(typeof(Task).IsAssignableFrom(method.ReturnType));
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.Contains(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(string) ||
                parameter.ParameterType.Name.Contains("Snapshot", StringComparison.Ordinal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedCandidateEnumerator_UsesClosedPathAndExtensionRules()
    {
        Type enumerator = RequirePublicType(
            typeof(ScanCandidateEnumerator).Assembly,
            "Vulgata.Infrastructure.Git.PreparedScanCandidateEnumerator");

        string[] requiredFilterSegments =
        [
            ".git", ".idea", ".vscode", ".settings", ".gradle", ".mvn", "target", "build",
            "out", "dist", "generated", "node_modules", "vendor", "vendors", "packages",
            "third_party", "third-party", "src/test", "test", "tests", "fixture", "fixtures",
        ];

        PropertyInfo[] publicProperties = enumerator
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Concat(
                enumerator.GetNestedTypes(BindingFlags.Public)
                    .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
            .ToArray();

        Assert.NotEmpty(publicProperties);
        Assert.All(requiredFilterSegments, segment => Assert.NotEqual("", segment));
        Assert.DoesNotContain(
            publicProperties,
            property => property.Name.Contains("AbsolutePath", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            publicProperties,
            property => property.Name.Contains("SourceContent", StringComparison.OrdinalIgnoreCase));
    }

    private static Type RequirePublicType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName);
        Assert.NotNull(type);
        Assert.True(type!.IsPublic, $"{fullName} must be public for the prepared inventory seam.");
        Assert.False(type.IsAbstract);
        return type;
    }
}

using System.Reflection;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CommunicationDetection;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T028 RED boundary for the safe, bounded, non-evaluating XML projection.
/// Reflection keeps the test buildable while the new public Infrastructure
/// seams are introduced; the expected failure is the missing contract, not a
/// test-harness or production compile failure.
/// </summary>
public sealed class PreparedXmlSecurityTests
{
    private const string _readerName =
        "Vulgata.Infrastructure.CommunicationDetection.SafeXmlReader";
    private const string _projectionName =
        "Vulgata.Infrastructure.CommunicationDetection.SafeXmlProjection";
    private const string _locatorName =
        "Vulgata.Infrastructure.CommunicationDetection.XmlEvidenceLocator";

    [Fact]
    public void SafeXmlReader_ExposesBoundedProjectionAndStrictEncodingOutcome()
    {
        Type reader = RequireType(_readerName);
        Type projection = RequireType(_projectionName);

        Assert.Contains(
            reader.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            method => method.Name is "Read" or "Parse" or "ReadAsync" or "ParseAsync");
        Assert.Contains(
            projection.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            method => method.Name.Contains("Project", StringComparison.Ordinal) ||
                method.Name.Contains("Parse", StringComparison.Ordinal));

        Type result = FindPublicType(
            type => type.Name.Contains("Xml", StringComparison.Ordinal) &&
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Any(property => property.Name is "Outcome" or "Status"));
        Assert.Contains(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name),
            name => name.Contains("Encoding", StringComparison.Ordinal));
        Assert.Contains(
            result.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name),
            name => name.Contains("Bound", StringComparison.Ordinal) ||
                name.Contains("Limit", StringComparison.Ordinal));
    }

    [Fact]
    public void SafeXmlReader_RejectsEvaluationFeaturesAndNeverExposesHostOrSourceContent()
    {
        Type reader = RequireType(_readerName);
        Type projection = RequireType(_projectionName);
        Type locator = RequireType(_locatorName);

        string[] forbiddenOperationFragments =
        ["Resolver", "XPath", "Xsl", "Script", "Plugin", "Network", "Execute"];
        foreach (Type type in new[] { reader, projection, locator })
        {
            Assert.DoesNotContain(
                type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
                method => forbiddenOperationFragments.Any(fragment =>
                    method.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
        }

        Type[] exposedTypes =
        [
            reader,
            projection,
            locator,
            FindPublicType(type => type.Name.Contains("Xml", StringComparison.Ordinal) &&
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Any(property => property.Name is "Outcome" or "Status")),
        ];
        string[] forbiddenPropertyFragments =
            ["AbsolutePath", "HostPath", "SourceContent", "RawSource", "DocumentText"];
        Assert.DoesNotContain(
            exposedTypes.SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance)),
            property => forbiddenPropertyFragments.Any(fragment =>
                property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void SafeXmlEvidenceLocator_MapsExactRepositoryRelativeNodeLineAndColumn()
    {
        Type locator = RequireType(_locatorName);
        PropertyInfo[] properties = locator.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        MethodInfo[] methods = locator.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

        Assert.Contains(
            methods,
            method => method.Name.Contains("Locate", StringComparison.Ordinal) ||
                method.Name.Contains("Map", StringComparison.Ordinal));
        Assert.Contains(properties, property => property.Name is "RelativePath" or "CanonicalPath");
        Assert.Contains(properties, property => property.Name.Contains("Line", StringComparison.Ordinal));
        Assert.Contains(properties, property => property.Name.Contains("Column", StringComparison.Ordinal));
        Assert.Contains(properties, property => property.Name.Contains("Offset", StringComparison.Ordinal) ||
            property.Name.Contains("Byte", StringComparison.Ordinal));
    }

    private static Type RequireType(string fullName)
    {
        Type? type = typeof(CommunicationEvidenceRuleCatalog).Assembly.GetType(fullName);
        Assert.NotNull(type);
        Assert.True(type!.IsPublic, $"{fullName} must be a public Infrastructure seam.");
        return type;
    }

    private static Type FindPublicType(Func<Type, bool> predicate)
    {
        Type? type = typeof(CommunicationEvidenceRuleCatalog).Assembly
            .GetTypes()
            .Where(candidate => candidate.IsPublic)
            .FirstOrDefault(predicate);
        Assert.NotNull(type);
        return type!;
    }
}

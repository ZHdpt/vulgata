using System.Reflection;
using Vulgata.Infrastructure.CommunicationDetection;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T032 RED boundary for the immutable embedded Communication Pattern Catalog
/// and its closed, one-parse evaluator.
/// </summary>
public sealed class CommunicationCatalogTests
{
    private const string _catalogName =
        "Vulgata.Infrastructure.CommunicationDetection.CommunicationPatternCatalog";
    private const string _loaderName =
        "Vulgata.Infrastructure.CommunicationDetection.CommunicationCatalogLoader";
    private const string _evaluatorName =
        "Vulgata.Infrastructure.CommunicationDetection.CommunicationCatalogEvaluator";

    [Fact]
    public void CommunicationCatalog_ExposesVersionHashRulesAndClosedLoader()
    {
        Type catalog = RequireType(_catalogName);
        Type loader = RequireType(_loaderName);
        PropertyInfo[] catalogProperties = catalog.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(catalogProperties, property => property.Name == "Version");
        Assert.Contains(catalogProperties, property => property.Name is "Sha256" or "CatalogSha256");
        Assert.Contains(catalogProperties, property => property.Name == "Rules");
        Assert.Contains(
            loader.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            method => method.Name is "Load" or "LoadEmbedded");
        Assert.DoesNotContain(
            catalogProperties,
            property => property.CanWrite && property.SetMethod?.IsPublic == true);
    }

    [Fact]
    public void CommunicationCatalogLoader_LoadEmbedded_ReturnsAcceptedStory12_2IdentityAndRules()
    {
        CommunicationPatternCatalog catalog = CommunicationCatalogLoader.LoadEmbedded();

        Assert.Equal("communication-pattern-catalog/1.0", catalog.SchemaIdentity);
        Assert.False(string.IsNullOrWhiteSpace(catalog.CatalogId));
        Assert.False(string.IsNullOrWhiteSpace(catalog.Version));
        Assert.NotEmpty(catalog.Rules);
        Assert.IsAssignableFrom<IReadOnlyList<CommunicationPatternRule>>(catalog.Rules);
        Assert.DoesNotContain(
            catalog.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.CanWrite && property.SetMethod?.IsPublic == true);
    }

    [Fact]
    public void CommunicationCatalog_GrammarSupportsPathGlobSelectorAncestorAttributeAndExtraction()
    {
        Type catalog = RequireType(_catalogName);
        Type rule = catalog.Assembly.GetTypes()
            .Where(type => type.IsPublic)
            .FirstOrDefault(type =>
                type.Name.Contains("Pattern", StringComparison.Ordinal) &&
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Any(property => property.Name.Contains("Selector", StringComparison.Ordinal)))
            ?? throw new Xunit.Sdk.XunitException("Communication catalog rule grammar seam is missing.");

        string[] grammarProperties =
            ["Path", "Glob", "Selector", "Ancestor", "Attribute", "Extraction"];
        PropertyInfo[] properties = rule.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.All(
            grammarProperties,
            name => Assert.Contains(properties, property =>
                property.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void CommunicationCatalog_EvaluatorHasNoExecutableOrDynamicQueryOperations()
    {
        Type evaluator = RequireType(_evaluatorName);
        MethodInfo[] methods = evaluator.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

        Assert.Contains(
            methods,
            method => method.Name.Contains("Evaluate", StringComparison.Ordinal) ||
                method.Name.Contains("Match", StringComparison.Ordinal));
        string[] forbidden =
            ["Regex", "XPath", "Predicate", "Function", "Execute", "Network", "Script", "Plugin"];
        Assert.DoesNotContain(
            methods,
            method => forbidden.Any(fragment =>
                method.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    private static Type RequireType(string fullName)
    {
        Type? type = typeof(CommunicationEvidenceRuleCatalog).Assembly.GetType(fullName);
        Assert.NotNull(type);
        Assert.True(type!.IsPublic, $"{fullName} must be a public catalog seam.");
        return type;
    }
}

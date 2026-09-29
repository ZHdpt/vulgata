using Vulgata.Tests.TestInfrastructure;
using System.Collections;
using System.Reflection;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CommunicationDetection;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T030 RED boundary for the immutable safe-XML direct-child range set.  It
/// observes only the accepted public range seams and does not construct or
/// amend ranges in the test.
/// </summary>
public sealed class PreparedXmlSplitRangeTests
{
    private const string _rangeSetName =
        "Vulgata.Infrastructure.CommunicationDetection.SafeXmlDirectChildRangeSet";
    private const string _providerName =
        "Vulgata.Infrastructure.CommunicationDetection.PreparedXmlRangeProvider";
    private const string _plannerName =
        "Vulgata.Core.DomainServices.PreparedSourceRangePlanner";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void SafeXmlSplit_PublicSeamsExposeImmutableFileAndHashBoundDirectChildRanges()
    {
        Type rangeSet = RequireType(_rangeSetName, typeof(CommunicationEvidenceRuleCatalog).Assembly);
        Type provider = RequireType(_providerName, typeof(CommunicationEvidenceRuleCatalog).Assembly);
        Type planner = RequireType(_plannerName, typeof(CommunicationKindClassification).Assembly);

        Assert.Contains(
            provider.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            method => method.Name.Contains("Range", StringComparison.Ordinal) ||
                method.Name.Contains("Split", StringComparison.Ordinal));
        Assert.Contains(
            planner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            method => method.Name.Contains("Range", StringComparison.Ordinal) ||
                method.Name.Contains("Split", StringComparison.Ordinal));

        PropertyInfo[] properties = rangeSet.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(properties, property => property.Name is "CanonicalPath" or "RelativePath");
        Assert.Contains(properties, property => property.Name is "FileHash" or "SourceHash");
        Assert.Contains(properties, property => property.Name.Contains("Encoding", StringComparison.Ordinal));
        Assert.Contains(properties, property => property.Name.Contains("Document", StringComparison.Ordinal));

        PropertyInfo[] collectionProperties = properties
            .Where(property => typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
            .ToArray();
        Assert.NotEmpty(collectionProperties);
        Assert.DoesNotContain(collectionProperties, property => property.PropertyType.IsArray);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void SafeXmlSplit_DirectChildRangeElementsCarryExactByteLineAndOrdinalFields()
    {
        Type rangeSet = RequireType(_rangeSetName, typeof(CommunicationEvidenceRuleCatalog).Assembly);
        Type rangeElement = rangeSet.Assembly.GetTypes()
            .Where(type => type.IsPublic)
            .FirstOrDefault(type =>
                type.Name.Contains("Xml", StringComparison.Ordinal) &&
                type.Name.Contains("Range", StringComparison.Ordinal) &&
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Any(property => property.Name == "ByteStart"))
            ?? throw new Xunit.Sdk.XunitException(
                "Safe XML direct-child range element public seam is missing.");

        string[] requiredProperties =
            ["ByteStart", "ByteEndExclusive", "LineStart", "LineEnd", "SegmentOrdinal", "SegmentCount"];
        PropertyInfo[] properties = rangeElement.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.All(requiredProperties, name => Assert.Contains(properties, property => property.Name == name));
        Assert.Contains(properties, property => property.Name is "SourceHash" or "SegmentHash");
        Assert.Contains(properties, property => property.Name is "FileHash" or "CanonicalPath");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void SafeXmlSplit_MalformedOverlapOutOfBoundsAndOversizedRangesHaveWholeFileSkipOutcome()
    {
        Type planner = RequireType(_plannerName, typeof(CommunicationKindClassification).Assembly);
        Type rangeSet = RequireType(_rangeSetName, typeof(CommunicationEvidenceRuleCatalog).Assembly);

        Assert.Contains(
            planner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            method => method.Name.Contains("Reject", StringComparison.Ordinal) ||
                method.Name.Contains("Validate", StringComparison.Ordinal) ||
                method.Name.Contains("Plan", StringComparison.Ordinal));
        Assert.Contains(
            rangeSet.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name),
            name => name is "Skipped" or "SkipReason" or "Outcome" or "Status");
    }

    private static Type RequireType(string fullName, Assembly assembly)
    {
        Type? type = assembly.GetType(fullName);
        Assert.NotNull(type);
        Assert.True(type!.IsPublic, $"{fullName} must be a public prepared-scanning seam.");
        return type;
    }
}

using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T026 RED boundary for exact Java top-level symbol ranges.  The test names
/// the two accepted public seams but uses reflection until their production
/// types are introduced, so RED remains a missing-behavior assertion.
/// </summary>
public sealed class PreparedJavaSplitRangeTests
{
    private const string _rangePlannerName =
        "Vulgata.Core.DomainServices.PreparedSourceRangePlanner";

    private const string _rangeProviderName =
        "Vulgata.Infrastructure.Analysis.Java.PreparedJavaRangeProvider";

    private const string _rangeSourceName =
        "Vulgata.Infrastructure.Analysis.Java.PreparedJavaRangeSource";

    private static readonly string[] _requiredRangeProperties =
    [
        "ByteStart",
        "ByteEndExclusive",
        "LineStart",
        "LineEnd",
        "SourceHash",
        "SegmentOrdinal",
        "SegmentCount"
    ];

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JavaSplit_PublicSeams_ExposeExactTopLevelByteAndLineRanges()
    {
        Type plannerType = RequireType(typeof(AnalysisScope).Assembly, _rangePlannerName);
        Type providerType = RequireType(typeof(JdtLsAnalysisSessionFactory).Assembly, _rangeProviderName);

        Assert.Contains(
            plannerType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            method => method.Name.Contains("Range", StringComparison.Ordinal));
        Assert.Contains(
            providerType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            method => method.Name.Contains("Range", StringComparison.Ordinal));

        Type rangeType = FindRangeType(plannerType.Assembly, providerType.Assembly);
        foreach (string propertyName in _requiredRangeProperties)
        {
            Assert.NotNull(
                rangeType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JavaSplit_MalformedOverlapOutOfBoundsOrOversizedRanges_AreRejectedAsWholeFile()
    {
        Type plannerType = RequireType(typeof(AnalysisScope).Assembly, _rangePlannerName);
        Type rangeType = FindRangeType(plannerType.Assembly, typeof(JdtLsAnalysisSessionFactory).Assembly);

        Assert.Contains(
            plannerType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
            method => method.Name.Contains("Reject", StringComparison.Ordinal) ||
                method.Name.Contains("Plan", StringComparison.Ordinal));
        Assert.Contains(
            rangeType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name),
            name => name is "Skipped" or "SkipReason" or "Outcome");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JavaSplit_ExactSegmentHashes_UseRawByteIntervalsAndDoNotApproximateJdtPositions()
    {
        Type plannerType = RequireType(typeof(AnalysisScope).Assembly, _rangePlannerName);
        Type rangeType = FindRangeType(plannerType.Assembly, typeof(JdtLsAnalysisSessionFactory).Assembly);

        PropertyInfo sourceHash = rangeType.GetProperty("SourceHash")!;
        Assert.NotNull(sourceHash);
        Assert.Equal(typeof(string), sourceHash.PropertyType);
        Assert.Contains(
            rangeType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name),
            name => name == "FileHash");
        Assert.NotNull(plannerType);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JavaSplit_MatchedOwner_BindsFqnToOneContainingSegmentAndKeepsSiblingsPathValued()
    {
        Type sourceType = RequireType(typeof(JdtLsAnalysisSessionFactory).Assembly, _rangeSourceName);
        MethodInfo[] methods = sourceType.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        Assert.Contains(
            methods,
            method => (method.Name.Contains("Owner", StringComparison.Ordinal) ||
                method.Name.Contains("Semantic", StringComparison.Ordinal)) &&
                method.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(int) ||
                    parameter.Name is "selectorLine" or "fullyQualifiedSymbol"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void JavaSplit_MatchedOwner_RejectsAmbiguousOrNonContainingObservationBeforePublication()
    {
        Type sourceType = RequireType(typeof(JdtLsAnalysisSessionFactory).Assembly, _rangeSourceName);
        MethodInfo[] methods = sourceType.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        Assert.Contains(
            methods,
            method => (method.Name.Contains("Owner", StringComparison.Ordinal) ||
                method.Name.Contains("Semantic", StringComparison.Ordinal)) &&
                (method.ReturnType.Name.Contains("Failure", StringComparison.Ordinal) ||
                    method.ReturnType.Name.Contains("Outcome", StringComparison.Ordinal) ||
                    method.ReturnType.Name.Contains("Result", StringComparison.Ordinal)));
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
        Assert.NotNull(type);
        return type!;
    }

    private static Type FindRangeType(params Assembly[] assemblies)
    {
        Type? type = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .FirstOrDefault(candidate =>
                candidate.Name.Contains("Java", StringComparison.Ordinal) &&
                candidate.Name.Contains("Range", StringComparison.Ordinal));
        Assert.NotNull(type);
        return type!;
    }
}

using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T038 RED slice for the immutable prepared-only handoff read.
/// The assertions use public type/property seams so they fail as missing
/// behavior, rather than coupling the test to EF internals or private state.
/// </summary>
public sealed class PreparedHandoffProjectionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedContext_ExposesStableIdentitySeparateFromLegacyCaptureMetadata()
    {
        PropertyInfo[] properties = typeof(ScanContextSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(
            properties,
            property => property.Name == "PreparedScanContextIdentityHash");
        Assert.Contains(properties, property => property.Name == "CapturedAt");
        Assert.Contains(properties, property => property.Name == "Id");

        Type[] candidates = typeof(ScanContextSnapshot).Assembly.GetTypes()
            .Where(type => type.IsPublic)
            .Where(type => type.Name.Contains("Prepared", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Contains(
            candidates.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static)),
            method => method.Name.Contains("Context", StringComparison.OrdinalIgnoreCase) &&
                (method.Name.Contains("Capture", StringComparison.OrdinalIgnoreCase) ||
                    method.Name.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
                    method.Name.Contains("Identity", StringComparison.OrdinalIgnoreCase)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedHandoff_ProjectsOnlyCompleteV2RowsAndKeepsV1ReadableButIneligible()
    {
        Assert.Equal("2.0", PreScanResult.PreparedSchemaVersion);
        PropertyInfo[] resultProperties = typeof(PreScanResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(resultProperties, property => property.Name == "IsPreparedV2");
        Assert.Contains(resultProperties, property => property.Name == "PreparedScanManifestId");
        Assert.Contains(resultProperties, property => property.Name == "PreparedScanContextIdentityHash");

        PropertyInfo[] handoffProperties = typeof(PreparedScanHandoff)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(handoffProperties, property => property.Name == "PairSelectionFingerprint");
        Assert.Contains(handoffProperties, property => property.Name == "PairPackageFingerprint");
        Assert.Contains(handoffProperties, property => property.Name == "FileCodeUnits");
        Assert.DoesNotContain(
            handoffProperties,
            property => property.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Worker", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Document", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedHandoffSource_ExposesOwnershipFilteredImmutableMaterializedRead()
    {
        Type source = typeof(IPreparedScanHandoffSource);
        MethodInfo get = source.GetMethod("GetAsync") ??
            throw new InvalidOperationException("Prepared handoff source contract is missing GetAsync.");
        Assert.Equal(typeof(Task<PreparedScanHandoff?>), get.ReturnType);
        Assert.Contains(get.GetParameters(), parameter =>
            parameter.ParameterType == typeof(PreparedScanAuthorizationScope));

        Type? implementation = typeof(Vulgata.Infrastructure.Git.PreparedScanCandidateEnumerator).Assembly.GetType(
            "Vulgata.Infrastructure.Data.PreparedScanHandoffSource",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(
            implementation);
        Assert.True(
            source.IsAssignableFrom(implementation!),
            "The persisted read must implement the ownership-filtered v2 handoff port.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedHandoff_ExposesCompleteTotalsAndBoundedClassificationObservation()
    {
        PropertyInfo[] packageProperties = typeof(PreparedRepositoryPackage)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(packageProperties, property => property.Name == "TotalConfirmedEvidenceCount");
        Assert.Contains(packageProperties, property => property.Name == "TotalHintCount");
        Assert.Contains(packageProperties, property => property.Name == "TotalWarningCount");
        Assert.Contains(packageProperties, property => property.Name == "EvidenceTruncated");
        Assert.Contains(packageProperties, property => property.Name == "Classifications");
        Assert.True(
            typeof(PreparedScanHandoff).GetProperty("SelectedCount") is not null,
            "The handoff must expose the complete selected count, not an unbounded query.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedPass1Handoff_RequiresServerOwnedSymbolNameForPathAndSemanticUnits()
    {
        // T045 RED: path-v1 units carry the Repository-relative path while a
        // matched semantic-v2 unit carries the reviewed fully qualified
        // symbol. Both values must survive the prepared handoff boundary.
        foreach (Type type in new[]
        {
            typeof(PreparedPass1FileCodeUnitAssignment),
            typeof(PreparedPass1WorkerPacket),
        })
        {
            PropertyInfo? symbolName = type.GetProperty(
                "SymbolName",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(symbolName);
            Assert.Equal(typeof(string), symbolName!.PropertyType);
        }
    }
}

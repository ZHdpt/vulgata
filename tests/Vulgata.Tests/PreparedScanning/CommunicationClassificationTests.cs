using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CommunicationDetection;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T034 RED boundary for conservative Communication Kind Classification
/// extraction, bounded retention, and classification-only output.
/// </summary>
public sealed class CommunicationClassificationTests
{
    private const string _detectorName =
        "Vulgata.Infrastructure.CommunicationDetection.PreparedCommunicationDetector";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedCommunicationDetector_ExposesClassificationOnlyDetectionSeam()
    {
        Type detector = RequireType(_detectorName);
        MethodInfo[] methods = detector.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

        Assert.Contains(
            methods,
            method => method.Name.Contains("Detect", StringComparison.Ordinal) ||
                method.Name.Contains("Classif", StringComparison.Ordinal));
        Assert.DoesNotContain(
            methods,
            method => method.Name.Contains("Provider", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Consumer", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Edge", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Navigate", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedClassification_ExposesClosedKindLevelConfidenceRuleProvenanceAndExactEvidence()
    {
        PropertyInfo[] properties = typeof(CommunicationKindClassification)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        string[] requiredProperties =
        [
            "Kind", "Level", "Confidence", "Rule", "Provenance", "RelativePath", "Locator",
            "OccurrenceCount", "Fingerprint",
        ];
        Assert.All(requiredProperties, name => Assert.Contains(properties, property => property.Name == name));
        Assert.DoesNotContain(
            properties,
            property => property.Name.Contains("SourceContent", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("AbsolutePath", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedClassification_OutcomeProjectionContainsBoundedTotalsAndRetentionMetadata()
    {
        Type detector = RequireType(_detectorName);
        Type? result = detector.Assembly.GetTypes()
            .Where(type => type.IsPublic)
            .FirstOrDefault(type =>
            {
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                return properties.Any(property => property.Name.Contains("Classification", StringComparison.Ordinal)) &&
                    properties.Any(property => property.Name.Contains("Total", StringComparison.Ordinal));
            });
        Assert.NotNull(result);
        PropertyInfo[] resultProperties = result!.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(resultProperties, property => property.Name.Contains("Retained", StringComparison.Ordinal));
        Assert.Contains(resultProperties, property => property.Name.Contains("Warning", StringComparison.Ordinal));
        Assert.Contains(resultProperties, property => property.Name.Contains("Confirmed", StringComparison.Ordinal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedClassification_ContractContainsWarningForContradictoryOrUnsupportedEvidence()
    {
        Assert.Contains(nameof(EvidenceRule.ContradictoryEvidence), Enum.GetNames<EvidenceRule>());
        Assert.Contains(nameof(EvidenceRule.UnsupportedFamily), Enum.GetNames<EvidenceRule>());
        Assert.Contains(nameof(EvidenceRule.BoundExceeded), Enum.GetNames<EvidenceRule>());
        Assert.Contains(nameof(ClassificationLevel.Warning), Enum.GetNames<ClassificationLevel>());
        Assert.Contains(nameof(ConfidenceBand.None), Enum.GetNames<ConfidenceBand>());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedCommunicationDetector_OverBoundWarningsRetainExactly256InNormativeOrder()
    {
        Guid preScanResultId = Guid.NewGuid();
        PreparedCommunicationObservation[] observations = Enumerable.Range(0, 257)
            .Select(index => new PreparedCommunicationObservation(
                null,
                ClassificationLevel.Warning,
                ConfidenceBand.None,
                EvidenceRule.MalformedEvidence,
                EvidenceProvenance.Detector,
                $"src/malformed-{index:D3}.java",
                $"src/malformed-{index:D3}.java#warning",
                "qualification.over-bound"))
            .ToArray();

        PreparedCommunicationClassificationResult result = PreparedCommunicationDetector.Detect(
            preScanResultId, observations);
        IReadOnlyList<CommunicationKindClassification> retained = result.Classifications;

        Assert.Equal(257, result.TotalClassificationCount);
        Assert.Equal(257, result.TotalWarningCount);
        Assert.Equal(256, result.RetainedClassificationCount);
        Assert.True(result.EvidenceTruncated);
        Assert.Equal(1, retained.Count(classification => classification.Rule == EvidenceRule.BoundExceeded));
        Assert.NotEqual(retained.Count - 1, retained
            .Select((classification, index) => (classification, index))
            .Single(value => value.classification.Rule == EvidenceRule.BoundExceeded)
            .index);
        Assert.Equal(
            PreparedScanIdentityBuilders.OrderCommunicationClassifications(retained)
                .Select(classification => classification.Fingerprint),
            retained.Select(classification => classification.Fingerprint));
    }

    private static Type RequireType(string fullName)
    {
        Type? type = typeof(CommunicationEvidenceRuleCatalog).Assembly.GetType(fullName);
        Assert.NotNull(type);
        Assert.True(type!.IsPublic, $"{fullName} must be a public detector seam.");
        return type;
    }
}

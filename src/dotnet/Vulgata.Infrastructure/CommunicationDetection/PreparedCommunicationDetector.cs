using System.Collections.ObjectModel;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.CommunicationDetection;

public sealed record PreparedCommunicationObservation(
    CommunicationKind? Kind,
    ClassificationLevel Level,
    ConfidenceBand Confidence,
    EvidenceRule Rule,
    EvidenceProvenance Provenance,
    string RelativePath,
    string Locator,
    string CatalogRuleIdentity);

public sealed class PreparedCommunicationClassificationResult
{
    public PreparedCommunicationClassificationResult(
        IEnumerable<CommunicationKindClassification> classifications,
        int totalClassificationCount,
        int totalConfirmedCount,
        int totalHintCount,
        int totalWarningCount,
        int retainedClassificationCount,
        bool evidenceTruncated)
    {
        Classifications = new ReadOnlyCollection<CommunicationKindClassification>([.. classifications]);
        TotalClassificationCount = totalClassificationCount;
        TotalConfirmedCount = totalConfirmedCount;
        TotalHintCount = totalHintCount;
        TotalWarningCount = totalWarningCount;
        RetainedClassificationCount = retainedClassificationCount;
        EvidenceTruncated = evidenceTruncated;
    }

    public IReadOnlyList<CommunicationKindClassification> Classifications { get; }
    public int TotalClassificationCount { get; }
    public int TotalConfirmedCount { get; }
    public int TotalHintCount { get; }
    public int TotalWarningCount { get; }
    public int RetainedClassificationCount { get; }
    public bool EvidenceTruncated { get; }
}

public static class PreparedCommunicationDetector
{
    public const int MaximumRetainedClassifications = 256;

    public static PreparedCommunicationClassificationResult Detect(
        Guid preScanResultId,
        IEnumerable<PreparedCommunicationObservation> observations)
    {
        if (preScanResultId == Guid.Empty)
        {
            throw new ArgumentException("A Pre-Scan Result identity is required.", nameof(preScanResultId));
        }

        ArgumentNullException.ThrowIfNull(observations);
        PreparedCommunicationObservation[] orderedObservations = observations
            .OrderBy(observation => LevelRank(observation.Level))
            .ThenBy(observation => KindRank(observation.Kind))
            .ThenBy(observation => observation.Rule.ToString(), StringComparer.Ordinal)
            .ThenBy(observation => observation.RelativePath, StringComparer.Ordinal)
            .ThenBy(observation => observation.Locator, StringComparer.Ordinal)
            .ThenBy(observation => observation.CatalogRuleIdentity, StringComparer.Ordinal)
            .ToArray();

        List<CommunicationKindClassification> complete = PreparedScanIdentityBuilders
            .OrderCommunicationClassifications(orderedObservations
            .GroupBy(static observation => observation)
            .Select(group => CreateClassification(preScanResultId, group.Key, group.Count())))
            .ToList();

        int confirmed = orderedObservations.Count(item => item.Level == ClassificationLevel.Confirmed);
        int hints = orderedObservations.Count(item => item.Level == ClassificationLevel.Hint);
        int warnings = orderedObservations.Count(item => item.Level == ClassificationLevel.Warning);
        bool truncated = complete.Count > MaximumRetainedClassifications;
        IReadOnlyList<CommunicationKindClassification> retained;
        if (!truncated)
        {
            retained = complete;
        }
        else
        {
            List<CommunicationKindClassification> bounded = complete
                .Take(MaximumRetainedClassifications - 1)
                .ToList();
            bounded.Add(CreateBoundWarning(
                preScanResultId,
                complete.Count - (MaximumRetainedClassifications - 1)));
            retained = PreparedScanIdentityBuilders.OrderCommunicationClassifications(bounded);
        }

        return new(
            retained,
            orderedObservations.Length,
            confirmed,
            hints,
            warnings,
            retained.Count,
            truncated);
    }

    private static CommunicationKindClassification CreateClassification(
        Guid preScanResultId,
        PreparedCommunicationObservation observation,
        int occurrenceCount)
    {
        PreparedCanonicalArtifact artifact = PreparedScanIdentityBuilders.CreateCommunicationClassification(
            preScanResultId,
            observation.Kind,
            observation.Level,
            observation.Confidence,
            observation.Rule,
            observation.Provenance,
            observation.RelativePath,
            observation.Locator,
            occurrenceCount,
            observation.CatalogRuleIdentity);
        return CommunicationKindClassification.CreatePrepared(
            preScanResultId,
            observation.Kind,
            observation.Level,
            observation.Confidence,
            observation.Rule,
            observation.Provenance,
            observation.RelativePath,
            observation.Locator,
            occurrenceCount,
            artifact.Fingerprint,
            observation.CatalogRuleIdentity);
    }

    private static CommunicationKindClassification CreateBoundWarning(
        Guid preScanResultId,
        int omittedCount)
    {
        const string path = CommunicationKindClassification.UnknownRelativePath;
        const string locator = "(unknown)#communication-observation-bound";
        const string catalogRuleIdentity = "detector.bound-exceeded";
        var observation = new PreparedCommunicationObservation(
            null,
            ClassificationLevel.Warning,
            ConfidenceBand.None,
            EvidenceRule.BoundExceeded,
            EvidenceProvenance.Detector,
            path,
            locator,
            catalogRuleIdentity);
        return CreateClassification(preScanResultId, observation, 1);
    }

    private static int LevelRank(ClassificationLevel level) => level switch
    {
        ClassificationLevel.Confirmed => 0,
        ClassificationLevel.Hint => 1,
        ClassificationLevel.Warning => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static int KindRank(CommunicationKind? kind) => kind switch
    {
        CommunicationKind.Rpc => 0,
        CommunicationKind.Http => 1,
        null => 255,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

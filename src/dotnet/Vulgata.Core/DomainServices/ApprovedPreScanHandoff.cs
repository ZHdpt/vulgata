using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public sealed class ApprovedPreScanHandoff
{
    private const int _maximumClassificationSamples = 100;
    private const ConfirmedCommunicationKinds _allConfirmedKinds =
        ConfirmedCommunicationKinds.Rpc | ConfirmedCommunicationKinds.Http;

    private ApprovedPreScanHandoff(
        Guid resultId,
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        DateTimeOffset approvedAt,
        string approvalMessage,
        IReadOnlyList<CommunicationKind> confirmedKinds,
        int totalConfirmedEvidenceCount,
        int totalHintCount,
        int totalWarningCount,
        bool evidenceTruncated,
        IReadOnlyList<ApprovedPreScanClassification> confirmedEvidence,
        IReadOnlyList<ApprovedPreScanClassification> hints,
        IReadOnlyList<ApprovedPreScanClassification> warnings)
    {
        ResultId = resultId;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        ProfiledRevision = profiledRevision;
        State = PreScanApprovalState.PreScanApproved;
        SchemaVersion = PreScanResult.CurrentSchemaVersion;
        ApprovedAt = approvedAt;
        ApprovalMessage = approvalMessage;
        ConfirmedKinds = confirmedKinds;
        TotalConfirmedEvidenceCount = totalConfirmedEvidenceCount;
        TotalHintCount = totalHintCount;
        TotalWarningCount = totalWarningCount;
        EvidenceTruncated = evidenceTruncated;
        ConfirmedEvidence = confirmedEvidence;
        Hints = hints;
        Warnings = warnings;
    }

    public Guid ResultId { get; }
    public Guid RepositoryId { get; }
    public Guid ScanRunId { get; }
    public string ProfiledRevision { get; }
    public PreScanApprovalState State { get; }
    public string SchemaVersion { get; }
    public DateTimeOffset ApprovedAt { get; }
    public string ApprovalMessage { get; }
    public IReadOnlyList<CommunicationKind> ConfirmedKinds { get; }
    public int TotalConfirmedEvidenceCount { get; }
    public int TotalHintCount { get; }
    public int TotalWarningCount { get; }
    public bool EvidenceTruncated { get; }
    public IReadOnlyList<ApprovedPreScanClassification> ConfirmedEvidence { get; }
    public IReadOnlyList<ApprovedPreScanClassification> Hints { get; }
    public IReadOnlyList<ApprovedPreScanClassification> Warnings { get; }

    public static ApprovedPreScanHandoff Create(
        Guid resultId,
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        PreScanApprovalState state,
        string schemaVersion,
        DateTimeOffset approvedAt,
        string approvalMessage,
        ConfirmedCommunicationKinds confirmedCommunicationKinds,
        int totalConfirmedEvidenceCount,
        int totalHintCount,
        int totalWarningCount,
        bool evidenceTruncated,
        IEnumerable<CommunicationKindClassification> classifications)
    {
        if (resultId == Guid.Empty ||
            repositoryId == Guid.Empty ||
            scanRunId == Guid.Empty ||
            resultId != scanRunId)
        {
            throw new ArgumentException(
                "An Approved Pre-Scan Handoff requires one valid result identity.");
        }

        if (state != PreScanApprovalState.PreScanApproved ||
            !StringComparer.Ordinal.Equals(
                schemaVersion,
                PreScanResult.CurrentSchemaVersion))
        {
            throw new ArgumentException(
                "Only the current approved Pre-Scan schema can be materialized.");
        }

        string revision = RequireProfiledRevision(profiledRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalMessage);
        if (approvalMessage.Length > 256 ||
            approvalMessage.Any(char.IsControl))
        {
            throw new ArgumentException(
                "The approval message must be bounded.",
                nameof(approvalMessage));
        }

        if ((confirmedCommunicationKinds & ~_allConfirmedKinds) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(confirmedCommunicationKinds));
        }

        ValidateCount(totalConfirmedEvidenceCount, nameof(totalConfirmedEvidenceCount));
        ValidateCount(totalHintCount, nameof(totalHintCount));
        ValidateCount(totalWarningCount, nameof(totalWarningCount));
        ArgumentNullException.ThrowIfNull(classifications);
        CommunicationKindClassification[] samples = [.. classifications];
        if (samples.Length > _maximumClassificationSamples ||
            samples.Any(sample => sample.PreScanResultId != scanRunId))
        {
            throw new ArgumentException(
                "Approved Pre-Scan samples must be bounded and owned by the ScanRun.",
                nameof(classifications));
        }

        ApprovedPreScanClassification[] projected =
        [
            .. samples
                .OrderBy(sample => sample.Level)
                .ThenBy(sample => sample.Kind)
                .ThenBy(sample => sample.Rule)
                .ThenBy(sample => sample.RelativePath, StringComparer.Ordinal)
                .ThenBy(sample => sample.Locator, StringComparer.Ordinal)
                .Select(ApprovedPreScanClassification.FromEntity),
        ];
        ApprovedPreScanClassification[] confirmed =
        [
            .. projected.Where(sample => sample.Level == ClassificationLevel.Confirmed),
        ];
        ApprovedPreScanClassification[] hints =
        [
            .. projected.Where(sample => sample.Level == ClassificationLevel.Hint),
        ];
        ApprovedPreScanClassification[] warnings =
        [
            .. projected.Where(sample => sample.Level == ClassificationLevel.Warning),
        ];
        ValidateRetainedTotals(
            confirmed,
            hints,
            warnings,
            totalConfirmedEvidenceCount,
            totalHintCount,
            totalWarningCount,
            evidenceTruncated);

        CommunicationKind[] confirmedKinds = DeriveConfirmedKinds(
            confirmedCommunicationKinds);
        ConfirmedCommunicationKinds retainedConfirmedKinds = confirmed.Aggregate(
            ConfirmedCommunicationKinds.None,
            (flags, sample) => flags | (sample.Kind switch
            {
                CommunicationKind.Rpc => ConfirmedCommunicationKinds.Rpc,
                CommunicationKind.Http => ConfirmedCommunicationKinds.Http,
                _ => ConfirmedCommunicationKinds.None,
            }));
        bool hasConfirmedKinds = confirmedKinds.Length != 0;
        bool hasConfirmedEvidence = totalConfirmedEvidenceCount != 0;
        if (retainedConfirmedKinds != confirmedCommunicationKinds ||
            hasConfirmedKinds != hasConfirmedEvidence)
        {
            throw new InvalidOperationException(
                "Every confirmed kind requires its deterministic retained sample.");
        }

        return new(
            resultId,
            repositoryId,
            scanRunId,
            revision,
            approvedAt,
            approvalMessage,
            Array.AsReadOnly(confirmedKinds),
            totalConfirmedEvidenceCount,
            totalHintCount,
            totalWarningCount,
            evidenceTruncated,
            Array.AsReadOnly(confirmed),
            Array.AsReadOnly(hints),
            Array.AsReadOnly(warnings));
    }

    public bool MatchesProfiledRevision(string requestedProfiledRevision) =>
        StringComparer.Ordinal.Equals(
            ProfiledRevision,
            RequireProfiledRevision(requestedProfiledRevision));

    private static CommunicationKind[] DeriveConfirmedKinds(
        ConfirmedCommunicationKinds flags) =>
        [
            .. new[]
            {
                (Flag: ConfirmedCommunicationKinds.Rpc, Kind: CommunicationKind.Rpc),
                (Flag: ConfirmedCommunicationKinds.Http, Kind: CommunicationKind.Http),
            }
                .Where(item => flags.HasFlag(item.Flag))
                .Select(item => item.Kind),
        ];

    private static void ValidateRetainedTotals(
        IReadOnlyCollection<ApprovedPreScanClassification> confirmed,
        IReadOnlyCollection<ApprovedPreScanClassification> hints,
        IReadOnlyCollection<ApprovedPreScanClassification> warnings,
        int totalConfirmedEvidenceCount,
        int totalHintCount,
        int totalWarningCount,
        bool evidenceTruncated)
    {
        int retainedConfirmed = SumOccurrences(confirmed);
        int retainedHints = SumOccurrences(hints);
        int retainedWarnings = SumOccurrences(warnings);
        if (retainedConfirmed > totalConfirmedEvidenceCount ||
            retainedHints > totalHintCount ||
            retainedWarnings > totalWarningCount ||
            (!evidenceTruncated &&
                (retainedConfirmed != totalConfirmedEvidenceCount ||
                    retainedHints != totalHintCount ||
                    retainedWarnings != totalWarningCount)))
        {
            throw new InvalidOperationException(
                "Approved Pre-Scan sample totals are inconsistent.");
        }
    }

    private static int SumOccurrences(
        IEnumerable<ApprovedPreScanClassification> classifications) =>
        checked(classifications.Sum(item => item.OccurrenceCount));

    private static void ValidateCount(int count, string parameterName)
    {
        if (count is < 0 or > 250_000)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static string RequireProfiledRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 64 ||
            value.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "The Profiled Revision must be a lowercase Git object ID.",
                nameof(value));
        }

        return value;
    }
}

public sealed class ApprovedPreScanClassification
{
    private ApprovedPreScanClassification(
        CommunicationKind? kind,
        ClassificationLevel level,
        ConfidenceBand confidence,
        EvidenceRule rule,
        EvidenceProvenance provenance,
        string evidenceLocator,
        int occurrenceCount)
    {
        Kind = kind;
        Level = level;
        Confidence = confidence;
        Rule = rule;
        Provenance = provenance;
        EvidenceLocator = evidenceLocator;
        OccurrenceCount = occurrenceCount;
    }

    public CommunicationKind? Kind { get; }
    public ClassificationLevel Level { get; }
    public ConfidenceBand Confidence { get; }
    public EvidenceRule Rule { get; }
    public EvidenceProvenance Provenance { get; }
    public string EvidenceLocator { get; }
    public int OccurrenceCount { get; }

    internal static ApprovedPreScanClassification FromEntity(
        CommunicationKindClassification classification)
    {
        _ = CommunicationKindClassification.Create(
            classification.PreScanResultId,
            classification.Kind,
            classification.Level,
            classification.Confidence,
            classification.Rule,
            classification.Provenance,
            classification.RelativePath,
            classification.Locator,
            classification.OccurrenceCount,
            classification.Fingerprint);
        return new(
            classification.Kind,
            classification.Level,
            classification.Confidence,
            classification.Rule,
            classification.Provenance,
            classification.Locator,
            classification.OccurrenceCount);
    }
}

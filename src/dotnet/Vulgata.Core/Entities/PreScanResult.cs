namespace Vulgata.Core.Entities;

public enum PreScanApprovalState
{
    PreScanApproved,
}

[Flags]
public enum ConfirmedCommunicationKinds
{
    None = 0,
    Rpc = 1,
    Http = 2,
}

public sealed class PreScanResult
{
    public const string CurrentSchemaVersion = "1.0";
    private const ConfirmedCommunicationKinds _allCommunicationKinds =
        ConfirmedCommunicationKinds.Rpc | ConfirmedCommunicationKinds.Http;

    private PreScanResult()
    {
    }

    private PreScanResult(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        DateTimeOffset approvedAt,
        string fingerprint,
        string approvalMessage,
        ConfirmedCommunicationKinds confirmedCommunicationKinds,
        int totalConfirmedEvidenceCount,
        int totalHintCount,
        int totalWarningCount,
        bool evidenceTruncated,
        IReadOnlyList<CommunicationKindClassification> classifications)
    {
        Id = scanRunId;
        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        ProfiledRevision = profiledRevision;
        State = PreScanApprovalState.PreScanApproved;
        SchemaVersion = CurrentSchemaVersion;
        ApprovedAt = approvedAt;
        Fingerprint = fingerprint;
        ApprovalMessage = approvalMessage;
        ConfirmedCommunicationKinds = confirmedCommunicationKinds;
        TotalConfirmedEvidenceCount = totalConfirmedEvidenceCount;
        TotalHintCount = totalHintCount;
        TotalWarningCount = totalWarningCount;
        EvidenceTruncated = evidenceTruncated;
        Classifications = classifications;
    }

    public Guid Id { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public string ProfiledRevision { get; private set; } = string.Empty;
    public PreScanApprovalState State { get; private set; }
    public string SchemaVersion { get; private set; } = string.Empty;
    public DateTimeOffset ApprovedAt { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public string ApprovalMessage { get; private set; } = string.Empty;
    public ConfirmedCommunicationKinds ConfirmedCommunicationKinds { get; private set; }
    public int TotalConfirmedEvidenceCount { get; private set; }
    public int TotalHintCount { get; private set; }
    public int TotalWarningCount { get; private set; }
    public bool EvidenceTruncated { get; private set; }
    public IReadOnlyList<CommunicationKindClassification> Classifications { get; private set; } =
        new List<CommunicationKindClassification>();
    public ScanRun? ScanRun { get; private set; }

    public static PreScanResult CreateApproved(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        DateTimeOffset approvedAt,
        string fingerprint,
        string approvalMessage,
        ConfirmedCommunicationKinds confirmedCommunicationKinds,
        int totalConfirmedEvidenceCount,
        int totalHintCount,
        int totalWarningCount,
        bool evidenceTruncated,
        IEnumerable<CommunicationKindClassification> classifications)
    {
        if (repositoryId == Guid.Empty || scanRunId == Guid.Empty)
        {
            throw new ArgumentException(
                "A Pre-Scan Result requires one non-empty Repository and ScanRun identity.");
        }

        string revision = RequireProfiledRevision(profiledRevision);
        string aggregateFingerprint = RequireLowercaseSha256(fingerprint);
        string message = RequireApprovalMessage(approvalMessage);
        if ((confirmedCommunicationKinds & ~_allCommunicationKinds) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confirmedCommunicationKinds),
                "Confirmed communication kinds must use only Rpc and Http flags.");
        }

        ValidateCount(totalConfirmedEvidenceCount, nameof(totalConfirmedEvidenceCount));
        ValidateCount(totalHintCount, nameof(totalHintCount));
        ValidateCount(totalWarningCount, nameof(totalWarningCount));
        ArgumentNullException.ThrowIfNull(classifications);
        CommunicationKindClassification[] retainedClassifications = [.. classifications];
        ValidateClassifications(
            scanRunId,
            confirmedCommunicationKinds,
            totalConfirmedEvidenceCount,
            totalHintCount,
            totalWarningCount,
            evidenceTruncated,
            retainedClassifications);

        return new(
            repositoryId,
            scanRunId,
            revision,
            approvedAt,
            aggregateFingerprint,
            message,
            confirmedCommunicationKinds,
            totalConfirmedEvidenceCount,
            totalHintCount,
            totalWarningCount,
            evidenceTruncated,
            Array.AsReadOnly(retainedClassifications));
    }

    private static void ValidateClassifications(
        Guid scanRunId,
        ConfirmedCommunicationKinds confirmedCommunicationKinds,
        int totalConfirmedEvidenceCount,
        int totalHintCount,
        int totalWarningCount,
        bool evidenceTruncated,
        CommunicationKindClassification[] classifications)
    {
        if (classifications.Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(classifications),
                "A Pre-Scan Result retains at most 100 classification samples.");
        }

        if (classifications.Any(classification =>
                classification.PreScanResultId != scanRunId) ||
            classifications
                .GroupBy(classification => classification.Fingerprint, StringComparer.Ordinal)
                .Any(group => group.Skip(1).Any()))
        {
            throw new ArgumentException(
                "Classification samples must have one result identity and unique fingerprints.",
                nameof(classifications));
        }

        int retainedConfirmed = SumOccurrences(classifications, ClassificationLevel.Confirmed);
        int retainedHints = SumOccurrences(classifications, ClassificationLevel.Hint);
        int retainedWarnings = SumOccurrences(classifications, ClassificationLevel.Warning);
        if (retainedConfirmed > totalConfirmedEvidenceCount ||
            retainedHints > totalHintCount ||
            retainedWarnings > totalWarningCount ||
            (!evidenceTruncated &&
                (retainedConfirmed != totalConfirmedEvidenceCount ||
                    retainedHints != totalHintCount ||
                    retainedWarnings != totalWarningCount)))
        {
            throw new ArgumentException(
                "Complete classification totals must contain every retained sample occurrence.");
        }

        ConfirmedCommunicationKinds sampledConfirmedKinds = ConfirmedCommunicationKinds.None;
        foreach (CommunicationKindClassification classification in classifications)
        {
            if (classification.Level == ClassificationLevel.Confirmed)
            {
                sampledConfirmedKinds |= classification.Kind switch
                {
                    CommunicationKind.Rpc => ConfirmedCommunicationKinds.Rpc,
                    CommunicationKind.Http => ConfirmedCommunicationKinds.Http,
                    _ => ConfirmedCommunicationKinds.None,
                };
            }
        }

        bool hasConfirmedKind = confirmedCommunicationKinds != ConfirmedCommunicationKinds.None;
        bool hasConfirmedEvidence = totalConfirmedEvidenceCount != 0;
        if (sampledConfirmedKinds != confirmedCommunicationKinds ||
            hasConfirmedKind != hasConfirmedEvidence)
        {
            throw new ArgumentException(
                "Confirmed samples, authoritative flags, and complete confirmed count must agree.");
        }
    }

    private static int SumOccurrences(
        IEnumerable<CommunicationKindClassification> classifications,
        ClassificationLevel level)
    {
        long count = classifications
            .Where(classification => classification.Level == level)
            .Sum(classification => (long)classification.OccurrenceCount);
        return checked((int)count);
    }

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
                "Profiled Revision must be a bounded lowercase Git object ID.",
                nameof(value));
        }

        return value;
    }

    private static string RequireLowercaseSha256(string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (fingerprint.Length != 64 ||
            fingerprint.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "The Pre-Scan Result fingerprint must be lowercase SHA-256.",
                nameof(fingerprint));
        }

        return fingerprint;
    }

    private static string RequireApprovalMessage(string approvalMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalMessage);
        if (approvalMessage.Length > 256 ||
            approvalMessage.Length != approvalMessage.Trim().Length)
        {
            throw new ArgumentException(
                "The approval message must be normalized and at most 256 characters.",
                nameof(approvalMessage));
        }

        return approvalMessage;
    }
}

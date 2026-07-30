namespace Vulgata.Shared.Repositories;

public sealed class ApprovedPreScanHandoffDto
{
    public Guid ResultId { get; init; }
    public Guid RepositoryId { get; init; }
    public Guid ScanRunId { get; init; }
    public string ProfiledRevision { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string SchemaVersion { get; init; } = string.Empty;
    public DateTimeOffset ApprovedAt { get; init; }
    public string ApprovalMessage { get; init; } = string.Empty;
    public IReadOnlyList<string> ConfirmedKinds { get; init; } = [];
    public int TotalConfirmedEvidenceCount { get; init; }
    public int TotalHintCount { get; init; }
    public int TotalWarningCount { get; init; }
    public bool EvidenceTruncated { get; init; }
    public IReadOnlyList<ApprovedPreScanClassificationDto> ConfirmedEvidence { get; init; } = [];
    public IReadOnlyList<ApprovedPreScanClassificationDto> Hints { get; init; } = [];
    public IReadOnlyList<ApprovedPreScanClassificationDto> Warnings { get; init; } = [];
}

public sealed class ApprovedPreScanClassificationDto
{
    public string? Kind { get; init; }
    public string Level { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
    public string Rule { get; init; } = string.Empty;
    public string Provenance { get; init; } = string.Empty;
    public string EvidenceLocator { get; init; } = string.Empty;
    public int OccurrenceCount { get; init; }
}

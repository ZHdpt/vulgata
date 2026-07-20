namespace Vulgata.Shared.Repositories;

public sealed class ScanRunSummaryDto
{
    private IReadOnlyList<ScanRunFrameworkSummaryDto> _frameworks = [];

    public Guid Id { get; init; }
    public Guid RepositoryId { get; init; }
    public string Status { get; init; } = string.Empty;
    public int ParsedFileCount { get; init; }
    public int TotalCandidateFileCount { get; init; }
    public bool NeedsHumanReview { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<ScanRunLanguageSummaryDto> Languages { get; init; } = [];
    public IReadOnlyList<ScanRunFrameworkSummaryDto> Frameworks
    {
        get => _frameworks;
        init => _frameworks =
            value.OrderBy(item => item.FrameworkName, StringComparer.Ordinal).ToArray();
    }
    public UnknownLanguageSummaryDto UnknownLanguage { get; init; } = new();
    public FrameworkEvidenceAuditSummaryDto FrameworkEvidenceAudit { get; init; } = new();
}

public sealed class ScanRunLanguageSummaryDto
{
    public string Language { get; init; } = string.Empty;
    public int DistinctFileCount { get; init; }
}

public sealed class ScanRunFrameworkSummaryDto
{
    public string FrameworkName { get; init; } = string.Empty;
    public int CodeUnitCount { get; init; }
    public string StrongestConfidence { get; init; } = string.Empty;
    public int TotalFactCount { get; init; }
    public IReadOnlyList<FrameworkEvidenceSampleDto> EvidenceSamples { get; init; } = [];
}

public sealed class UnknownLanguageSummaryDto
{
    public int DistinctFileCount => Paths.Count;
    public IReadOnlyList<string> Paths { get; init; } = [];
}

public sealed class FrameworkEvidenceSampleDto
{
    public Guid CodeUnitId { get; init; }
    public string Confidence { get; init; } = string.Empty;
    public string DetectionSource { get; init; } = string.Empty;
}

public sealed class FrameworkEvidenceAuditSummaryDto
{
    public int SampledOutcomeCount { get; init; }
    public int TotalOutcomeCount { get; init; }
    public bool SamplingApplied { get; init; }
    public IReadOnlyList<FrameworkEvidenceAuditDetailDto> Details { get; init; } = [];
}

public sealed class FrameworkEvidenceAuditDetailDto
{
    public string Path { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

namespace Vulgata.Shared.Repositories;

public sealed class ScanRunSummaryDto
{
    public Guid Id { get; init; }
    public Guid RepositoryId { get; init; }
    public string Status { get; init; } = string.Empty;
    public int ParsedFileCount { get; init; }
    public int TotalCandidateFileCount { get; init; }
    public bool NeedsHumanReview { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? Message { get; init; }
}

namespace Vulgata.Core.Entities;

public enum ScanRunStatus
{
    Profiling,
    Completed,
    Failed,
    Aborted,
}

public sealed class ScanRun
{
    private ScanRun()
    {
    }

    private ScanRun(Guid repositoryId, int providerProcessTimeoutSeconds, DateTimeOffset now)
    {
        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException("仓库标识不能为空。", nameof(repositoryId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(providerProcessTimeoutSeconds, 0);
        Id = Guid.NewGuid();
        RepositoryId = repositoryId;
        ProviderProcessTimeoutSeconds = providerProcessTimeoutSeconds;
        Status = ScanRunStatus.Profiling;
        StartedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid RepositoryId { get; private set; }
    public ScanRunStatus Status { get; private set; }
    public int TotalCandidateFileCount { get; private set; }
    public int ParsedFileCount { get; private set; }
    public int ProviderProcessTimeoutSeconds { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public long? DurationMilliseconds { get; private set; }
    public bool NeedsHumanReview { get; private set; }
    public string? TerminalMessage { get; private set; }
    public Repository? Repository { get; private set; }
    public ICollection<ScanCandidateOutcome> CandidateOutcomes { get; } = [];
    public ICollection<CodeUnit> CodeUnits { get; } = [];
    public ICollection<CodeUnitEdge> CodeUnitEdges { get; } = [];
    public ICollection<UnresolvedStructuralRelationshipCandidate> UnresolvedRelationships { get; } = [];
    public ICollection<CodeUnitFramework> CodeUnitFrameworks { get; } = [];
    public ICollection<ScanRunLanguageSummary> LanguageSummaries { get; } = [];

    public static ScanRun Start(Guid repositoryId, int providerProcessTimeoutSeconds, DateTimeOffset now) =>
        new(repositoryId, providerProcessTimeoutSeconds, now);

    public void SetCandidateCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        EnsureProfiling();
        TotalCandidateFileCount = count;
    }

    public void Complete(int parsedFileCount, int codeUnitCount, DateTimeOffset now, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(parsedFileCount);
        if (parsedFileCount > TotalCandidateFileCount)
        {
            throw new ArgumentOutOfRangeException(nameof(parsedFileCount));
        }

        EnsureProfiling();
        ParsedFileCount = parsedFileCount;
        NeedsHumanReview = TotalCandidateFileCount >= 500 && codeUnitCount == 0;
        Finish(ScanRunStatus.Completed, now, message);
    }

    public void Fail(string message, DateTimeOffset now) =>
        Finish(ScanRunStatus.Failed, now, NormalizeMessage(message));

    public void Abort(string message, DateTimeOffset now) =>
        Finish(ScanRunStatus.Aborted, now, NormalizeMessage(message));

    private void Finish(ScanRunStatus status, DateTimeOffset now, string? message)
    {
        EnsureProfiling();
        if (now < StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(now));
        }

        Status = status;
        CompletedAt = now;
        DurationMilliseconds = (long)(now - StartedAt).TotalMilliseconds;
        TerminalMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
    }

    private void EnsureProfiling()
    {
        if (Status != ScanRunStatus.Profiling)
        {
            throw new InvalidOperationException("扫描运行已结束。");
        }
    }

    private static string NormalizeMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        string value = message.Trim();
        return value.Length <= 512 ? value : value[..512];
    }
}

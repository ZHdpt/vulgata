namespace Vulgata.Shared.Scans;

public sealed record StartPass1ExecutionRequest(
    Guid FirstManifestId,
    Guid SecondManifestId,
    int? ConcurrencyLimit = null);

public sealed record UpdatePass1ConcurrencyRequest(
    long ExpectedVersion,
    int ConcurrencyLimit,
    string? ActorId = null);

public sealed record Pass1ExecutionStartResponse(
    Guid Pass1ExecutionId,
    int ConcurrencyLimit,
    bool IsReplay);

public sealed record Pass1ExecutionWarningResponse(
    int PairSelectionOrdinal,
    string FailureCategory,
    string FailureCode);

public sealed record Pass1ExecutionStatusResponse(
    Guid Pass1ExecutionId,
    string Phase,
    long StateVersion,
    int ConcurrencyLimit,
    long ConcurrencyRevision,
    int DispatchedCount,
    int PendingCount,
    int RunningCount,
    int RetryScheduledCount,
    int CompletedCount,
    int FailedCount,
    int CancelledCount,
    string? ManualResumeAction,
    IReadOnlyList<Pass1ExecutionWarningResponse> Warnings);

public sealed record Pass1ConcurrencyResponse(
    Guid Pass1ExecutionId,
    int ConcurrencyLimit,
    long StateVersion,
    long ConcurrencyRevision,
    int PreservedLeaseCount,
    bool IsReplay);

public sealed record Pass1ResumeResponse(
    Guid Pass1ExecutionId,
    string Phase,
    long StateVersion,
    int RecoveredExpiredLeaseCount,
    int ReleasedDueRetryCount,
    int SkippedCompletedCount,
    int PendingCount,
    bool Continued,
    bool IsReplay,
    string? ManualResumeAction);

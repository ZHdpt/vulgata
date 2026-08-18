using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public enum DurableScanStoreOutcome
{
    Applied,
    Replay,
    Conflict,
    Rejected,
    NotFound,
}

public enum DurableScanReason
{
    None,
    Unauthorized,
    InvalidIdentity,
    StaleStateVersion,
    LeaseMismatch,
    IntegrityViolation,
    ProviderUnavailable,
    Cancelled,
    Failed,
}

public enum ScanJobResultCommitOutcome
{
    Committed,
    Conflict,
    Rejected,
    Cancelled,
    Failed,
}

public enum ScanRecoveryTransitionKind
{
    NoChange,
    ExpiredLeaseToPending,
    DueRetryToPending,
    PausedToPrepared,
    FailedIntegrity,
}

public sealed record DurableScanAuthorizationScope(
    string ActorId,
    IReadOnlyList<Guid> OwnedSystemIds);

public sealed record DurableScanStoreResult(
    DurableScanStoreOutcome Outcome,
    DurableScanReason Reason,
    long? StateVersion,
    Guid? LeaseToken,
    DateTimeOffset? LeaseExpiresAt);

public sealed record ScanRecoveryJobSnapshot(
    Guid ScanJobExecutionId,
    Guid FileCodeUnitId,
    ScanJobExecutionState State,
    long StateVersion,
    int AttemptCount,
    Guid? LeaseToken,
    DateTimeOffset? LeaseExpiresAt,
    DateTimeOffset? NextAttemptAt,
    Guid? CommittedDocumentVersionId);

public sealed record ScanRecoverySnapshot(
    Guid ScanExecutionId,
    Guid SystemId,
    Guid RepositoryId,
    long StateVersion,
    IReadOnlyList<ScanRecoveryJobSnapshot> Jobs,
    ScanExecutionPhase Phase = ScanExecutionPhase.Pass1Planning);

public sealed record ScanRecoverySnapshotResult(
    DurableScanStoreOutcome Outcome,
    DurableScanReason Reason,
    ScanRecoverySnapshot? Snapshot);

public sealed record ScanRecoveryRevalidationResult(
    bool IsValid,
    DurableScanReason Reason);

public sealed record ScanRecoveryView(
    Guid ScanExecutionId,
    ScanExecutionPhase Phase,
    int TotalJobCount,
    int CompletedJobCount,
    int PendingJobCount,
    int RunningJobCount,
    int RetryScheduledJobCount,
    int FailedJobCount,
    int CancelledJobCount,
    int RecoveredExpiredLeaseCount,
    int ReleasedDueRetryCount,
    bool StateChanged,
    DurableScanReason Reason);

public sealed record ScanRecoveryResult(
    DurableScanStoreOutcome Outcome,
    DurableScanReason Reason,
    long? StateVersion,
    ScanRecoveryView? View = null,
    bool IsReplay = false);

public sealed record TwoRepositoryScanCoordinationResult(
    DurableScanStoreOutcome Outcome,
    DurableScanReason Reason,
    TwoRepositoryScanCoordination? Coordination);

public sealed record DurableScanProgressResult(
    Guid ScanExecutionId,
    long StateVersion,
    int TotalJobCount,
    int CompletedJobCount,
    int PendingJobCount,
    int RunningJobCount,
    int RetryScheduledJobCount,
    int FailedJobCount,
    int CancelledJobCount);

public sealed record ScanRecoveryTransitionCommand(
    Guid ScanExecutionId,
    long ExpectedStateVersion,
    Guid? ScanJobExecutionId,
    long? ExpectedJobStateVersion,
    ScanRecoveryTransitionKind Transition,
    DateTimeOffset ObservedAt);

public sealed record ScanJobLeaseCommand(
    Guid ScanExecutionId,
    Guid ScanJobExecutionId,
    long ExpectedStateVersion,
    Guid LeaseToken,
    DateTimeOffset LeaseExpiresAt,
    DateTimeOffset StateChangedAt);

public sealed record RunningScanJobTransitionCommand(
    Guid ScanExecutionId,
    Guid ScanJobExecutionId,
    long ExpectedStateVersion,
    Guid LeaseToken,
    ScanJobExecutionState TargetState,
    DateTimeOffset? NextAttemptAt,
    ScanJobFailureCategory? FailureCategory,
    string? FailureCode,
    DateTimeOffset StateChangedAt);

public sealed record TwoRepositoryScanCoordinationTransitionCommand(
    Guid CoordinationId,
    long ExpectedStateVersion,
    TwoRepositoryScanCoordinationPhase TargetPhase,
    TwoRepositoryScanReasonCategory? ReasonCategory,
    string? ReasonCode,
    Guid ScanExecutionOneId,
    Guid ScanExecutionTwoId,
    DateTimeOffset UpdatedAt);

public sealed record ScanJobResultCommitRequest(
    DurableScanAuthorizationScope AuthorizationScope,
    Guid RepositoryId,
    Guid ScanExecutionId,
    Guid ScanJobExecutionId,
    Guid FileCodeUnitId,
    Guid ScanRunId,
    string ProfiledRevision,
    string PromptIdentity,
    string DocumentSchemaIdentity,
    string AnalysisRuntimeIdentityHash,
    Guid DocumentVersionId,
    string CommitFingerprint,
    long ExpectedStateVersion,
    Guid LeaseToken,
    WorkerAgentResultEnvelope WorkerResult);

public sealed record ScanJobResultCommitResult(
    ScanJobResultCommitOutcome Outcome,
    DurableScanReason Reason,
    Guid? CommittedDocumentVersionId,
    long? StateVersion,
    Guid? LeaseToken,
    bool IsReplay);

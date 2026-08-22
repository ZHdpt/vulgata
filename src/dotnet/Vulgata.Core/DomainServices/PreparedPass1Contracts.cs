using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public enum PreparedPass1FailureOutcome
{
    Recorded,
    RetryScheduled,
    Paused,
    FailedClosed,
    Cancelled,
    Replay,
    Conflict,
    NotFound,
    Rejected,
    Failed,
}

public sealed record PreparedPass1FailureResult(
    PreparedPass1FailureOutcome Outcome,
    ScanJobFailureCategory FailureCategory,
    string FailureCode,
    string Status,
    bool RetryScheduled,
    int AttemptCount,
    int DispatchedCount,
    bool Terminal,
    int OrdinaryFailureCount,
    Pass1PairExecutionPhase? PairState,
    string? ManualResumeAction,
    bool NewDispatchAllowed,
    bool CancellationRequested,
    bool AutomaticResumeAllowed,
    bool FailClosed,
    bool DocumentCommitted);

public enum PreparedPass1ProviderCircuitOutcome
{
    Opened,
    Replay,
    ProbeScheduled,
    Recovered,
    Paused,
    Ineligible,
    Conflict,
    NotFound,
    Rejected,
    Cancelled,
    Failed,
}

public sealed record PreparedPass1ProviderCircuitResult(
    PreparedPass1ProviderCircuitOutcome Outcome,
    Pass1CircuitState CircuitState,
    Pass1PairExecutionPhase Phase,
    int ProbeOrdinal,
    DateTimeOffset? NextProbeAt,
    DateTimeOffset? RetryAfterAt,
    string ReasonCode,
    string ManualResumeAction,
    int CompletedDocumentCount,
    IReadOnlyList<Guid> CompletedDocumentVersionIds,
    bool AllowsNewClaims);

public enum PreparedPass1RecoveryOutcome
{
    Continued,
    NoOpReplay,
    Paused,
    FailedClosed,
    Cancelled,
    NotFound,
    Rejected,
    Failed,
}

/// <summary>
/// Sanitized durable result of one pair-wide Pass 1 recovery attempt.
/// Binding details and provider diagnostics intentionally never cross this boundary.
/// </summary>
public sealed record PreparedPass1RecoveryResult(
    PreparedPass1RecoveryOutcome Outcome,
    DurableScanReason Reason,
    Guid? PairExecutionId,
    Pass1PairExecutionPhase? PairState,
    long? StateVersion,
    int RecoveredExpiredLeaseCount,
    int ReleasedDueRetryCount,
    int SkippedCompletedJobCount,
    int PendingJobCount,
    bool Continued,
    bool IsReplay,
    bool PairBindingValid,
    string? ManualResumeAction);

public sealed record PreparedPass1CanonicalArtifact(
    ReadOnlyMemory<byte> Envelope,
    ReadOnlyMemory<byte> Fingerprint,
    Guid? Id)
{
    public string FingerprintHex => Convert.ToHexStringLower(Fingerprint.Span);
}

public sealed record PreparedPass1ManifestBinding(
    Guid RepositoryId,
    Guid ScanRunId,
    Guid ScanExecutionId,
    Guid ManifestId,
    ReadOnlyMemory<byte> ManifestFingerprint,
    ReadOnlyMemory<byte> ProfiledRevision,
    ReadOnlyMemory<byte> PreparedScanContextIdentityHash);

public sealed record PreparedPass1PairIdentityInput(
    Guid CoordinationId,
    Guid SystemId,
    ReadOnlyMemory<byte> PairPackageFingerprint,
    ReadOnlyMemory<byte> PairSelectionFingerprint,
    IReadOnlyList<PreparedPass1ManifestBinding> ManifestBindings,
    IReadOnlyList<string> PromptIdentityBindings,
    string DocumentSchemaIdentity,
    IReadOnlyList<ReadOnlyMemory<byte>> AnalysisRuntimeIdentityHashBindings);

public sealed record PreparedPass1ConcurrencyChangeInput(
    Guid PairExecutionId,
    ulong ExpectedStateVersion,
    ulong PreviousLimit,
    ulong EffectiveLimit,
    ulong ResultingStateVersion,
    ulong ResultingConcurrencyRevision,
    string AuthorizedPrincipalIdentity);

public sealed record PreparedPass1LogicalDocumentInput(
    Guid RepositoryId,
    string CanonicalPath,
    ulong SegmentOrdinal);

public sealed record PreparedPass1DocumentVersionInput(
    Guid ScanJobExecutionId,
    ReadOnlyMemory<byte> FileCodeUnitIdentityFingerprint,
    string SchemaIdentity);

public sealed record PreparedPass1SourceAnchorInput(
    Guid DocumentVersionId,
    Guid FileCodeUnitId,
    string CanonicalPath,
    ulong ByteStart,
    ulong ByteEnd,
    ulong LineStart,
    ulong LineEnd,
    ReadOnlyMemory<byte> SourceHash,
    string EvidenceLocator);

public enum PreparedPass1NavigationOperation
{
    Definition,
    References,
    Implementations,
    Callers,
    Callees,
    SymbolSearch,
    RelatedFiles,
    ExactSourceEvidence,
}

public sealed record PreparedPass1LineRange(ulong Start, ulong End);

public sealed record PreparedPass1ByteRange(ulong Start, ulong End);

public sealed record PreparedPass1Position(ulong Line, ulong Utf16Character);

public sealed record PreparedPass1NavigationTarget(
    string? CanonicalPath,
    string? Symbol,
    PreparedPass1Position? Position,
    PreparedPass1LineRange? ExactLineRange,
    ReadOnlyMemory<byte>? ExpectedSourceHash);

public sealed record PreparedPass1NavigationRequestInput(
    Guid JobId,
    ulong ToolAttemptOrdinal,
    PreparedPass1NavigationOperation Operation,
    Guid SourceFileCodeUnitId,
    PreparedPass1NavigationTarget Target,
    ulong RequestedDepth,
    ReadOnlyMemory<byte>? ParentObservationIdentity,
    Guid PairExecutionId = default,
    long JobStateVersion = 0,
    long PairStateVersion = 0,
    Guid LeaseToken = default,
    DateTimeOffset LeaseExpiresAt = default,
    string PairState = "",
    Pass1CircuitState CircuitState = Pass1CircuitState.Closed,
    ScanJobExecutionState JobState = ScanJobExecutionState.Pending,
    string AuthorizationBinding = "");

public sealed record PreparedPass1ServerAuthorization(
    Guid PairExecutionId,
    long JobStateVersion,
    long PairStateVersion,
    Guid LeaseToken,
    DateTimeOffset LeaseExpiresAt,
    string PairState,
    Pass1CircuitState CircuitState,
    ScanJobExecutionState JobState,
    string AuthorizationBinding);

public static class PreparedPass1AuthorizationBinding
{
    public static string Create(PreparedScanAuthorizationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        string canonical = string.Join(
            '\n',
            scope.SystemId.ToString("D"),
            string.Join(',', scope.RepositoryIds.Distinct().Order().Select(value => value.ToString("D"))));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public abstract record PreparedPass1EvidenceDescriptor(string CanonicalPath);

public sealed record PreparedPass1DocumentEvidenceDescriptor(
    Guid LogicalDocumentId,
    Guid DocumentVersionId,
    string Path,
    ReadOnlyMemory<byte> TargetSourceHash,
    IReadOnlyList<Guid> SectionIds,
    IReadOnlyList<Guid> SourceAnchorIds)
    : PreparedPass1EvidenceDescriptor(Path);

public sealed record PreparedPass1SourceEvidenceDescriptor(
    string Path,
    string? Symbol,
    PreparedPass1LineRange LineRange,
    PreparedPass1ByteRange ByteRange,
    ReadOnlyMemory<byte> SourceHash,
    string EvidenceLocator)
    : PreparedPass1EvidenceDescriptor(Path);

public enum PreparedPass1NavigationOutcome
{
    DocumentEvidence,
    SourceEvidence,
    Unavailable,
    BoundExceeded,
}

public enum PreparedPass1NavigationBoundKind
{
    Calls,
    Results,
    EvidenceBytes,
    Depth,
}

public sealed record PreparedPass1NavigationReceiptInput(
    ReadOnlyMemory<byte>? RequestDigest,
    Guid JobId,
    ulong? ReceiptOrdinal,
    ulong? ToolAttemptOrdinal,
    PreparedPass1NavigationOperation? Operation,
    ulong? Depth,
    PreparedPass1NavigationOutcome Outcome,
    PreparedPass1NavigationBoundKind? BoundKind,
    IReadOnlyList<PreparedPass1EvidenceDescriptor> Descriptors,
    ulong ResultCount,
    ulong ReturnedEvidenceByteCount,
    ulong AcceptedCallCountAfter,
    ulong EvidenceBytesAfter);

public sealed record PreparedPass1ReferenceObservationInput(
    Guid SourceDocumentVersionId,
    Guid SourceFileCodeUnitId,
    string SourceClaimLocator,
    string? NormalizedTargetSymbol,
    string? TargetCanonicalPath,
    ReadOnlyMemory<byte>? TargetSourceHash,
    PreparedPass1LineRange? TargetLineRange);

public enum PreparedPass1DocumentSectionGroup
{
    Overview,
    InternalUnits,
    ExecutionAndDataFlow,
    BusinessAndCodeRules,
    DataAccess,
    CommunicationEvidence,
    References,
    DeferredObservations,
    AssumptionsAndUnresolvedEvidence,
}

public sealed record PreparedPass1CommitInput(
    Guid PairExecutionId,
    Guid JobId,
    Guid LogicalDocumentId,
    Guid DocumentVersionId,
    ReadOnlyMemory<byte> ContentFingerprint,
    IReadOnlyList<ReadOnlyMemory<byte>> SourceAnchorFingerprints,
    IReadOnlyList<Guid> DirectReferenceTargetDocumentVersionIds,
    IReadOnlyList<ReadOnlyMemory<byte>> DocumentSectionProjectionFingerprints,
    ReadOnlyMemory<byte> IndexEntryProjectionFingerprint,
    IReadOnlyList<ReadOnlyMemory<byte>> NavigationReceiptFingerprints,
    IReadOnlyList<ReadOnlyMemory<byte>> CommunicationClassificationFingerprints);

public enum PreparedPass1ReconciliationOutcome
{
    Reconciled,
    Replay,
    Conflict,
    Ineligible,
    NotFound,
    Rejected,
    Cancelled,
    Failed,
}

public sealed record PreparedPass1ReconciliationRequest(
    PreparedScanAuthorizationScope AuthorizationScope,
    long ExpectedPairStateVersion,
    string? ExpectedReconciliationFingerprint,
    DateTimeOffset ReconciledAt);

public sealed record PreparedPass1ReconciledEdge(
    Guid Id,
    Guid SourceDocumentVersionId,
    Guid TargetDocumentVersionId);

public sealed record PreparedPass1ReconciliationWarning(
    Guid SourceDocumentVersionId,
    string ObservationFingerprint,
    string ReasonCode,
    string? TargetLocator);

/// <summary>
/// Sanitized deterministic reconciliation result. Immutable Document
/// representations never cross this operational boundary.
/// </summary>
public sealed record PreparedPass1ReconciliationResult(
    PreparedPass1ReconciliationOutcome Outcome,
    Pass1PairExecutionPhase? PairState,
    string? ReconciliationFingerprint,
    IReadOnlyList<PreparedPass1ReconciledEdge> Edges,
    IReadOnlyList<PreparedPass1ReconciliationWarning> Warnings,
    int ReconciledEdgeCount,
    int WarningCount,
    int ChildExecutionCount,
    bool IsReplay,
    bool PairBindingValid);

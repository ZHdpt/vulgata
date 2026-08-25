using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Closed, server-bound navigation facade for one prepared Pass 1 job.
/// </summary>
public interface IPass1NavigationService
{
    Task<PreparedPass1NavigationResponse> NavigateAsync(
        PreparedPass1NavigationRequestInput request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Server-only reserved invocation path. The model-visible navigation request
/// remains closed while its durable ticket reaches the receipt owner.
/// </summary>
public interface IPass1ReservedNavigationService
{
    Task<PreparedPass1NavigationResponse> NavigateReservedAsync(
        PreparedPass1NavigationRequestInput request,
        PreparedPass1NavigationReservationTicket reservation,
        CancellationToken cancellationToken);
}

public sealed record PreparedPass1NavigationResponse(
    PreparedPass1NavigationOutcome Outcome,
    Guid ReceiptId,
    PreparedPass1NavigationOperation? Operation,
    ulong? Depth,
    PreparedPass1NavigationBoundKind? BoundKind,
    int ResultCount,
    int EvidenceBytes,
    IReadOnlyList<PreparedPass1NavigationResult> Results,
    PreparedPass1NavigationReservationTicket? ReservationTicket = null);

public sealed record PreparedPass1NavigationResult(
    string CanonicalPath,
    Guid? DocumentVersionId,
    ReadOnlyMemory<byte> SourceHash,
    IReadOnlyList<Guid> SectionIds,
    IReadOnlyList<Guid> SourceAnchorIds,
    string? Symbol,
    PreparedPass1LineRange? LineRange,
    PreparedPass1ByteRange? ByteRange,
    string? EvidenceLocator);

/// <summary>
/// One selected source segment from the approved Prepared Scan Manifest.
/// </summary>
public sealed record PreparedPass1AuthorizedSource(
    Guid FileCodeUnitId,
    string CanonicalPath,
    string SourceHash,
    PreparedPass1LineRange LineRange,
    PreparedPass1ByteRange ByteRange,
    int SegmentOrdinal,
    string EvidenceLocator);

/// <summary>
/// Server-owned scope and durable budget snapshot for one navigation attempt.
/// </summary>
public sealed record PreparedPass1NavigationScope(
    Guid JobId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    Guid SourceFileCodeUnitId,
    AnalysisScope AnalysisScope,
    IReadOnlyList<PreparedPass1AuthorizedSource> AuthorizedSources,
    int ToolAttemptCount,
    int AcceptedCallCount,
    int EvidenceBytes,
    IReadOnlyList<ReadOnlyMemory<byte>> ObservedTargetIdentities,
    Guid PairExecutionId = default,
    long JobStateVersion = 0,
    long PairStateVersion = 0,
    Guid LeaseToken = default,
    DateTimeOffset LeaseExpiresAt = default,
    string PairState = "",
    Pass1CircuitState CircuitState = Pass1CircuitState.Closed,
    ScanJobExecutionState JobState = ScanJobExecutionState.Pending,
    string AuthorizationBinding = "");

public sealed record PreparedPass1CurrentDocumentEvidence(
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    int SegmentOrdinal,
    string SourceHash,
    PreparedPass1DocumentEvidenceDescriptor Descriptor);

public sealed record PreparedPass1NavigationReceiptDraft(
    ReadOnlyMemory<byte> RequestDigest,
    PreparedPass1NavigationOperation Operation,
    ulong Depth,
    PreparedPass1NavigationOutcome Outcome,
    PreparedPass1NavigationBoundKind? BoundKind,
    IReadOnlyList<PreparedPass1EvidenceDescriptor> Results,
    int ResultCount,
    int EvidenceBytes,
    ReadOnlyMemory<byte> TargetObservationIdentity);

/// <summary>
/// Resolves only server-owned job, revision, selected-inventory and budget data.
/// Missing and unauthorized jobs are deliberately indistinguishable.
/// </summary>
public interface IPreparedPass1NavigationScopeSource
{
    Task<PreparedPass1NavigationScope?> GetAsync(
        Guid jobId,
        Guid sourceFileCodeUnitId,
        CancellationToken cancellationToken);

    Task<PreparedPass1NavigationScope?> GetAsync(
        PreparedPass1NavigationRequestInput request,
        CancellationToken cancellationToken) =>
        GetAsync(request.JobId, request.SourceFileCodeUnitId, cancellationToken);
}

/// <summary>
/// Reads a current same-Scan Code Logic Document projection through a typed port.
/// </summary>
public interface IPreparedPass1CurrentDocumentEvidenceSource
{
    Task<PreparedPass1CurrentDocumentEvidence?> FindAsync(
        PreparedPass1NavigationScope scope,
        PreparedPass1AuthorizedSource source,
        PreparedPass1NavigationTarget target,
        CancellationToken cancellationToken);
}

/// <summary>
/// Atomically revalidates the durable budget and commits one bounded receipt.
/// </summary>
public interface IPreparedPass1NavigationReceiptSink
{
    Task<Guid?> CommitAsync(
        PreparedPass1NavigationScope scope,
        PreparedPass1NavigationRequestInput request,
        PreparedPass1NavigationReceiptDraft receipt,
        CancellationToken cancellationToken);

    Task<PreparedPass1NavigationReservationTicket?> CommitReservedAsync(
        PreparedPass1NavigationScope scope,
        PreparedPass1NavigationRequestInput request,
        PreparedPass1NavigationReservationTicket reservation,
        PreparedPass1NavigationReceiptDraft receipt,
        CancellationToken cancellationToken) =>
        Task.FromResult<PreparedPass1NavigationReservationTicket?>(null);
}

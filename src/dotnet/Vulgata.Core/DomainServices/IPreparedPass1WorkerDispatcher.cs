using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Dedicated Story 12.3 dispatch boundary. The request is a server-owned
/// projection of an already-approved Prepared Scan Manifest pair; this port
/// never enumerates source or schedules navigation.
/// </summary>
public interface IPreparedPass1WorkerDispatcher
{
    Task<PreparedPass1DispatchResult> DispatchAsync(
        PreparedPass1DispatchRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provider boundary used by the prepared dispatcher. A later bounded
/// navigate_code loop can implement this port without exposing provider or
/// analysis-tool objects through the Core dispatch contract.
/// </summary>
public interface IPreparedPass1JobExecutor
{
    Task<PreparedPass1RawWorkerResult> ExecuteAsync(
        PreparedPass1WorkerPacket packet,
        string systemPrompt,
        CancellationToken cancellationToken = default);
}

public sealed record PreparedPass1DispatchRequest(
    IReadOnlyList<PreparedPass1FileCodeUnitAssignment> PreparedFileCodeUnits,
    IReadOnlyList<PreparedPass1ContextSnapshotProjection> ContextSnapshots,
    IReadOnlyList<PreparedPass1CommunicationClassificationProjection> CommunicationClassifications,
    int ConcurrencyLimit)
{
    public IReadOnlyList<string> ProfiledRevisionBindings => PreparedFileCodeUnits
        .Select(value => value.ProfiledRevision)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<Guid> RepositoryIds => PreparedFileCodeUnits
        .Select(value => value.RepositoryId)
        .Distinct()
        .Order()
        .ToArray();

    public IReadOnlyList<Guid> ScanRunIds => PreparedFileCodeUnits
        .Select(value => value.ScanRunId)
        .Distinct()
        .Order()
        .ToArray();
}

public sealed record PreparedPass1FileCodeUnitAssignment(
    Guid PairExecutionId,
    Guid JobId,
    Guid ScanExecutionId,
    Guid SystemId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    Guid FileCodeUnitId,
    string FileCodeUnitIdentityFingerprint,
    Guid PreparedScanManifestId,
    string PreparedScanManifestFingerprint,
    string PairPackageFingerprint,
    string RepositoryRelativePath,
    long ByteStart,
    long ByteEnd,
    int LineStart,
    int LineEnd,
    int SegmentOrdinal,
    int SegmentCount,
    string FileHash,
    string SourceHash,
    string PromptIdentity,
    string DocumentSchemaIdentity,
    string AnalysisRuntimeIdentityHash,
    Guid ContextSnapshotId,
    int ManifestOrdinal,
    int PairSelectionOrdinal,
    ReadOnlyMemory<byte> SourceUtf8)
{
    public int RemainingNavigationBudget { get; init; } = 8;
    public PreparedPass1NavigationBudgetProjection? NavigationBudget { get; init; }
    public PreparedPass1ServerAuthorization? ServerAuthorization { get; init; }
}

public sealed record PreparedPass1NavigationBudgetProjection(
    int ToolAttemptCount,
    long StateVersion,
    int AcceptedCallCount,
    int EvidenceBytes);

public sealed record PreparedPass1ContextSnapshotProjection(
    Guid ContextSnapshotId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string PreparedScanContextIdentityHash,
    string ContextContentHash,
    string? GlobalContext,
    string? SystemContext,
    string? RepositoryContext);

public sealed record PreparedPass1CommunicationClassificationProjection(
    Guid FileCodeUnitId,
    Guid ClassificationId,
    Guid PreparedScanManifestId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string ClassificationFingerprint,
    CommunicationKind? Kind,
    ClassificationLevel Level,
    ConfidenceBand Confidence,
    EvidenceRule Rule,
    EvidenceProvenance Provenance,
    string CatalogRuleIdentity,
    string RepositoryRelativePath,
    string EvidenceLocator);

/// <summary>
/// One immutable provider packet for exactly one already-prepared File Code
/// Unit. Completion time/order is deliberately absent from this type.
/// </summary>
public sealed record PreparedPass1WorkerPacket(
    Guid PairExecutionId,
    Guid JobId,
    Guid ScanExecutionId,
    Guid SystemId,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    Guid FileCodeUnitId,
    string FileCodeUnitIdentityFingerprint,
    Guid PreparedScanManifestId,
    string PreparedScanManifestFingerprint,
    string PairPackageFingerprint,
    string RepositoryRelativePath,
    long ByteStart,
    long ByteEnd,
    int LineStart,
    int LineEnd,
    int SegmentOrdinal,
    int SegmentCount,
    string FileHash,
    string SourceHash,
    string PromptIdentity,
    string DocumentSchemaIdentity,
    string AnalysisRuntimeIdentityHash,
    int ManifestOrdinal,
    int PairSelectionOrdinal,
    ReadOnlyMemory<byte> SourceUtf8,
    PreparedPass1ContextSnapshotProjection ContextSnapshot,
    IReadOnlyList<PreparedPass1CommunicationClassificationProjection> CommunicationClassifications)
{
    public int RemainingNavigationBudget { get; init; } = 8;
    public PreparedPass1NavigationBudgetProjection? NavigationBudget { get; init; }
    public PreparedPass1ServerAuthorization? ServerAuthorization { get; init; }
}

public enum PreparedPass1RawWorkerOutcome
{
    Completed,
    ProviderUnavailable,
    Rejected,
}

public sealed record PreparedPass1RawWorkerResult(
    PreparedPass1RawWorkerOutcome Outcome,
    ReadOnlyMemory<byte> Utf8Json,
    string? ReasonCode);

public sealed record PreparedPass1ParsedWorkerResult(
    string SchemaIdentity,
    ReadOnlyMemory<byte> CanonicalCandidateJson);

public sealed record PreparedPass1JobDispatchResult(
    Guid JobId,
    int PairSelectionOrdinal,
    PreparedPass1RawWorkerOutcome Outcome,
    PreparedPass1ParsedWorkerResult? ParsedResult,
    string? ReasonCode);

public sealed record PreparedPass1DispatchResult(
    IReadOnlyList<PreparedPass1WorkerPacket> Packets,
    IReadOnlyList<PreparedPass1JobDispatchResult> Results);

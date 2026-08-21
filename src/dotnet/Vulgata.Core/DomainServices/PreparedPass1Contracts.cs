namespace Vulgata.Core.DomainServices;

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
    ReadOnlyMemory<byte>? ParentObservationIdentity);

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

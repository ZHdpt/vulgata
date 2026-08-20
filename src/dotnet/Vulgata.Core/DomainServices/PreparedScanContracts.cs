using Vulgata.Core.Entities;
using System.Security.Cryptography;

namespace Vulgata.Core.DomainServices;

public enum PreparedScanFailureReason
{
    None,
    NotFound,
    SourceIdentityMismatch,
    AnalysisNotReady,
    UnsafeInput,
    SelectionBounds,
    StaleCoordination,
    CanonicalConflict,
    PersistenceFailure,
    Cancelled,
}

public enum PreparedScanCommitOutcome
{
    Committed,
    Replay,
    Conflict,
    Rejected,
    Cancelled,
    Failed,
}

public sealed record PreparedScanFailure(
    PreparedScanFailureReason Reason,
    string FailureCode,
    string Diagnostic);

public sealed record PreparedScanAuthorizationScope(
    Guid UserId,
    Guid SystemId,
    IReadOnlyList<Guid> RepositoryIds);

public sealed record PreparedFileCodeUnitDraft(
    Guid Id,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string RelativePath,
    string FileHash,
    long ByteStart,
    long ByteEndExclusive,
    int LineStart,
    int LineEnd,
    int SegmentOrdinal,
    int SegmentCount,
    string SourceHash,
    string IdentityFingerprint,
    int DirectBusinessScore,
    int FinalBusinessScore,
    PreparedSelectionReason SelectionReason,
    int ManifestOrdinal,
    int PairSelectionOrdinal)
{
    public static PreparedFileCodeUnitDraft Create(PreparedFileCodeUnitInput input) =>
        PreparedScanPlanner.CreateFileCodeUnit(input);
}

public sealed record PreparedFileCodeUnitInput(
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string RelativePath,
    string FileHash,
    long ByteStart,
    long ByteEndExclusive,
    int LineStart,
    int LineEnd,
    int SegmentOrdinal,
    int SegmentCount,
    string SourceHash,
    IReadOnlyList<PreparedBusinessEvidence> Evidence);

public sealed record PreparedFileCodeUnitConstruction(
    PreparedFileCodeUnitDraft Draft,
    CodeUnit Entity);

public enum PreparedRelatedFileExpansionOutcome
{
    Expanded,
    WarningFallback,
}

public sealed record PreparedRelatedFileExpansionResult(
    PreparedRelatedFileExpansionOutcome Outcome,
    string? WarningCode,
    IReadOnlyList<PreparedFileCodeUnitDraft> Candidates);

public sealed record PreparedScanAuditDraft(
    Guid Id,
    Guid RepositoryId,
    Guid ScanRunId,
    int Ordinal,
    string RelativePath,
    PreparedScanAuditOutcome Outcome,
    PreparedScanAuditReason Reason,
    string? FileHash,
    long? RawByteCount,
    int? LogicalLineCount,
    string? FileCodeUnitIdentityFingerprint,
    int? PairSelectionOrdinal,
    Guid? PreparedScanManifestEntryId,
    string DetailFingerprint);

public sealed record CommunicationCatalogRule(
    string Identity,
    string PathGlob,
    IReadOnlyList<CommunicationCatalogElementStep> Selector,
    IReadOnlyList<CommunicationCatalogElementStep>? Ancestor,
    IReadOnlyList<CommunicationCatalogAttributeTest> AttributeTests,
    CommunicationCatalogExtraction Extraction,
    string? FixedPrefixRemoval,
    CommunicationKind Kind,
    EvidenceRule Rule,
    EvidenceProvenance Provenance);

public sealed record CommunicationCatalogElementStep(string NamespaceUri, string LocalName);

public sealed record CommunicationCatalogAttributeTest(
    string NamespaceUri,
    string LocalName,
    string? ExactValue);

public enum CommunicationCatalogExtractionKind
{
    Attribute,
    ElementText,
}

public sealed record CommunicationCatalogExtraction(
    CommunicationCatalogExtractionKind Kind,
    string? NamespaceUri,
    string? LocalName);

public sealed record CommunicationPatternCatalog(
    string SchemaIdentity,
    string CatalogId,
    string Version,
    string ResourceHash,
    IReadOnlyList<CommunicationCatalogRule> Rules);

public sealed record PreparedRepositoryPackage(
    Guid RepositoryId,
    Guid ScanRunId,
    Guid CoordinationId,
    string ProfiledRevision,
    string RequestedRef,
    string SnapshotIdentity,
    string RuntimeIdentityHash,
    CodeGraphCompletenessProof CodeGraphProof,
    JavaReadinessProof JavaReadiness,
    CommunicationPatternCatalog Catalog,
    Guid ContextSnapshotId,
    string PreparedScanContextIdentityHash,
    string ContextContentHash,
    string ManifestFingerprint,
    IReadOnlyList<PreparedFileCodeUnitDraft> FileCodeUnits,
    IReadOnlyList<PreparedScanAuditDraft> Audits,
    IReadOnlyList<CommunicationKindClassification> Classifications,
    int TotalConfirmedEvidenceCount,
    int TotalHintCount,
    int TotalWarningCount,
    bool EvidenceTruncated,
    DateTimeOffset CapturedAt,
    DateTimeOffset ApprovedAt);

public sealed record PreparedScanPairPackage(
    Guid CoordinationId,
    string PairSelectionFingerprint,
    string PairPackageFingerprint,
    IReadOnlyList<PreparedRepositoryPackage> Repositories,
    int SelectedCount);

public sealed record PreparedScanCommitResult(
    PreparedScanCommitOutcome Outcome,
    string? PairPackageFingerprint,
    PreparedScanFailure? Failure);

public sealed record PreparedScanManifestBinding(
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    Guid ManifestId,
    string ManifestFingerprint,
    string CodeGraphProofIdentityHash,
    string JavaReadinessIdentityHash,
    string PreparedScanContextIdentityHash);

public sealed record PreparedScanHandoff(
    string SchemaIdentity,
    Guid CoordinationId,
    string PairSelectionFingerprint,
    string PairPackageFingerprint,
    IReadOnlyList<PreparedScanManifestBinding> Manifests,
    IReadOnlyList<PreparedFileCodeUnitDraft> FileCodeUnits,
    int SelectedCount);

public sealed record PreparedRevisionRequest(
    Guid RepositoryId,
    Guid ScanRunId,
    string RepositoryOrigin,
    string RequestedRef,
    string ProfiledRevision);

public sealed record PreparedRevisionSnapshot(
    Guid RepositoryId,
    Guid ScanRunId,
    string RequestedRef,
    string ProfiledRevision,
    string SnapshotIdentity,
    string SnapshotBindingToken,
    string ContainedRootToken);

public sealed record PreparedRevisionAcquisitionResult(
    PreparedRevisionSnapshot? Snapshot,
    PreparedScanFailure? Failure);

public interface IPreparedRevisionAcquirer
{
    Task<PreparedRevisionAcquisitionResult> AcquireAsync(
        PreparedRevisionRequest request,
        CancellationToken cancellationToken);
}

public sealed record PreparedCatalogLoadResult(
    CommunicationPatternCatalog? Catalog,
    PreparedScanFailure? Failure);

public interface IPreparedCommunicationCatalogSource
{
    Task<PreparedCatalogLoadResult> LoadAsync(CancellationToken cancellationToken);
}

public sealed record PreparedCanonicalArtifact(ReadOnlyMemory<byte> Envelope, string Fingerprint);

/// <summary>
/// Stable Story 12.2 identity projections. Volatile observation metadata and
/// time-derived relational identifiers are intentionally not accepted here.
/// </summary>
public static class PreparedScanIdentityBuilders
{
    public static PreparedCanonicalArtifact Create(
        string domain,
        IEnumerable<CanonicalField> fields) => CreateArtifact(domain, fields);

    public static PreparedCanonicalArtifact ReconstructCanonical(
        string domain,
        IEnumerable<CanonicalField> fields) => CreateArtifact(domain, fields);

    public static PreparedCanonicalArtifact CreateCodeGraphProof(CodeGraphCompletenessProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (!proof.Initialized || proof.IndexState != CodeGraphIndexState.Complete ||
            proof.PendingAdded != 0 || proof.PendingModified != 0 || proof.PendingRemoved != 0 ||
            proof.WorktreeMismatch || proof.ReindexRecommended || proof.PendingReferences != 0 ||
            !proof.RepositoryBindingMatched || !proof.ScanRunBindingMatched ||
            !proof.RevisionBindingMatched || !proof.RuntimeBindingMatched ||
            !proof.SnapshotBindingMatched || !proof.ProjectPathTrusted || !proof.IndexPathTrusted)
        {
            throw new ArgumentException("Only a complete, revision-bound CodeGraph proof can be canonicalized.", nameof(proof));
        }

        return CreateArtifact(
            "vulgata/story-12.2/codegraph-completeness-proof/1.0",
            [
                Field(1, CanonicalEnvelope.Uuid(proof.RepositoryId)),
                Field(2, CanonicalEnvelope.Uuid(proof.ScanRunId)),
                Field(3, CanonicalEnvelope.String(RequireRevision(proof.ProfiledRevision))),
                Field(4, Hash(proof.SnapshotIdentity)),
                Field(5, Hash(proof.SnapshotRootBindingHash)),
                Field(6, Hash(proof.RuntimeIdentityHash)),
                Field(7, CanonicalEnvelope.String(proof.IndexIdentity)),
                Field(8, CanonicalEnvelope.String(RequireExact(proof.ProofSchemaIdentity, "codegraph-completeness-proof/1.0"))),
                Field(9, CanonicalEnvelope.Boolean(proof.Initialized)),
                Field(10, CanonicalEnvelope.Enum(proof.IndexState.ToString())),
                Field(11, NonNegative(proof.PendingAdded)),
                Field(12, NonNegative(proof.PendingModified)),
                Field(13, NonNegative(proof.PendingRemoved)),
                Field(14, CanonicalEnvelope.Boolean(proof.WorktreeMismatch)),
                Field(15, CanonicalEnvelope.Boolean(proof.ReindexRecommended)),
                Field(16, NonNegative(proof.PendingReferences)),
                Field(17, NonNegative(proof.IndexedFileCount)),
                Field(18, CanonicalEnvelope.String(proof.LogicalProjectBinding)),
                Field(19, CanonicalEnvelope.String(proof.LogicalIndexBinding)),
                Field(20, CanonicalEnvelope.Boolean(proof.RepositoryBindingMatched)),
                Field(21, CanonicalEnvelope.Boolean(proof.ScanRunBindingMatched)),
                Field(22, CanonicalEnvelope.Boolean(proof.RevisionBindingMatched)),
                Field(23, CanonicalEnvelope.Boolean(proof.RuntimeBindingMatched)),
                Field(24, CanonicalEnvelope.Boolean(proof.SnapshotBindingMatched)),
                Field(25, CanonicalEnvelope.Boolean(proof.ProjectPathTrusted)),
                Field(26, CanonicalEnvelope.Boolean(proof.IndexPathTrusted)),
            ]);
    }

    public static PreparedCanonicalArtifact CreateJavaReadiness(JavaReadinessProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        bool ready = proof.Outcome == JavaReadinessOutcome.Ready;
        bool notApplicable = proof.Outcome == JavaReadinessOutcome.NotApplicable;
        if ((!ready && !notApplicable) || proof.JavaMajor != 21 ||
            proof.JdtLsRelease != "jdt-language-server-1.61.0-202608141332" ||
            proof.StreamJsonRpcVersion != "2.25.29" || proof.Transport != AnalysisTransport.Stdio ||
            proof.RepositoryBindingMatched is false || proof.ScanRunBindingMatched is false ||
            proof.RevisionBindingMatched is false || proof.RuntimeBindingMatched is false ||
            proof.SnapshotBindingMatched is false ||
            (ready && (!proof.WorkspaceExclusive || proof.ReadinessFile is null ||
                !proof.InitializeCompleted || !proof.InitializedNotificationSent ||
                proof.ImportState != JavaImportState.Usable || proof.DocumentSymbolCount < 1)) ||
            (notApplicable && (proof.WorkspaceExclusive || proof.ReadinessFile is not null ||
                proof.InitializeCompleted || proof.InitializedNotificationSent ||
                proof.ImportState != JavaImportState.NotApplicable || proof.DocumentSymbolCount != 0)))
        {
            throw new ArgumentException("Java readiness is outside the approved Ready/NotApplicable identity contract.", nameof(proof));
        }

        return CreateArtifact(
            "vulgata/story-12.2/java-readiness/1.0",
            [
                Field(1, CanonicalEnvelope.Uuid(proof.RepositoryId)),
                Field(2, CanonicalEnvelope.Uuid(proof.ScanRunId)),
                Field(3, CanonicalEnvelope.String(RequireRevision(proof.ProfiledRevision))),
                Field(4, Hash(proof.SnapshotIdentity)),
                Field(5, Hash(proof.RuntimeIdentityHash)),
                Field(6, Hash(proof.WorkspaceBindingHash)),
                Field(7, CanonicalEnvelope.String(RequireExact(proof.ReadinessSchemaIdentity, "java-readiness/1.0"))),
                Field(8, CanonicalEnvelope.Enum(proof.Outcome.ToString())),
                Field(9, CanonicalEnvelope.String(proof.JdtLsRelease)),
                Field(10, Hash(proof.JdtLsDistributionArchiveHash)),
                Field(11, CanonicalEnvelope.UInt64((ulong)proof.JavaMajor)),
                Field(12, CanonicalEnvelope.String(proof.StreamJsonRpcVersion)),
                Field(13, CanonicalEnvelope.Enum(proof.Transport.ToString())),
                Field(14, CanonicalEnvelope.Boolean(proof.WorkspaceExclusive)),
                Field(15, proof.ReadinessFile is null ? CanonicalEnvelope.Null() : CanonicalEnvelope.String(proof.ReadinessFile)),
                Field(16, CanonicalEnvelope.Boolean(proof.InitializeCompleted)),
                Field(17, CanonicalEnvelope.Boolean(proof.InitializedNotificationSent)),
                Field(18, CanonicalEnvelope.Enum(proof.ImportState.ToString())),
                Field(19, NonNegative(proof.DocumentSymbolCount)),
                Field(20, CanonicalEnvelope.Boolean(proof.RepositoryBindingMatched)),
                Field(21, CanonicalEnvelope.Boolean(proof.ScanRunBindingMatched)),
                Field(22, CanonicalEnvelope.Boolean(proof.RevisionBindingMatched)),
                Field(23, CanonicalEnvelope.Boolean(proof.RuntimeBindingMatched)),
                Field(24, CanonicalEnvelope.Boolean(proof.SnapshotBindingMatched)),
            ]);
    }

    public static PreparedCanonicalArtifact CreateContextIdentity(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string contentHash) =>
        CreateArtifact(
            "vulgata/story-12.2/prepared-scan-context-snapshot/1.0",
            [
                Field(1, CanonicalEnvelope.Uuid(repositoryId)),
                Field(2, CanonicalEnvelope.Uuid(scanRunId)),
                Field(3, CanonicalEnvelope.String(RequireRevision(profiledRevision))),
                Field(4, CanonicalEnvelope.String("scan-context-snapshot/1.0")),
                Field(5, Hash(contentHash)),
            ]);

    public static PreparedCanonicalArtifact CreateFileCodeUnitIdentity(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string relativePath,
        string fileHash,
        long byteStart,
        long byteEndExclusive,
        int lineStart,
        int lineEnd,
        int segmentOrdinal,
        int segmentCount,
        string sourceHash)
    {
        if (byteStart < 0 || byteEndExclusive <= byteStart || lineStart < 1 || lineEnd < lineStart ||
            segmentOrdinal < 1 || segmentCount < segmentOrdinal)
        {
            throw new ArgumentOutOfRangeException(nameof(byteStart), "File Code Unit ranges and segment ordinals must be exact and bounded.");
        }

        return CreateArtifact(
            "vulgata/story-12.2/file-code-unit-identity/1.0",
            [
                Field(1, CanonicalEnvelope.Uuid(repositoryId)),
                Field(2, CanonicalEnvelope.Uuid(scanRunId)),
                Field(3, CanonicalEnvelope.String(RequireRevision(profiledRevision))),
                Field(4, CanonicalEnvelope.String(AnalysisContractGuard.RequireRelativePath(relativePath))),
                Field(5, Hash(fileHash)),
                Field(6, CanonicalEnvelope.UInt64((ulong)byteStart)),
                Field(7, CanonicalEnvelope.UInt64((ulong)byteEndExclusive)),
                Field(8, CanonicalEnvelope.UInt64((ulong)lineStart)),
                Field(9, CanonicalEnvelope.UInt64((ulong)lineEnd)),
                Field(10, CanonicalEnvelope.UInt64((ulong)segmentOrdinal)),
                Field(11, CanonicalEnvelope.UInt64((ulong)segmentCount)),
                Field(12, Hash(sourceHash)),
            ]);
    }

    public static PreparedCanonicalArtifact CreateCommunicationClassification(
        Guid preScanResultId,
        CommunicationKind? kind,
        ClassificationLevel level,
        ConfidenceBand confidence,
        EvidenceRule rule,
        EvidenceProvenance provenance,
        string relativePath,
        string locator,
        int occurrenceCount,
        string catalogRuleIdentity)
    {
        if (preScanResultId == Guid.Empty || occurrenceCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrenceCount));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(catalogRuleIdentity);
        return CreateArtifact(
            "vulgata/story-12.2/communication-classification/1.0",
            [
                Field(1, CanonicalEnvelope.Uuid(preScanResultId)),
                Field(2, kind is null ? CanonicalEnvelope.Null() : CanonicalEnvelope.Enum(kind.Value.ToString())),
                Field(3, CanonicalEnvelope.Enum(level.ToString())),
                Field(4, CanonicalEnvelope.Enum(confidence.ToString())),
                Field(5, CanonicalEnvelope.Enum(rule.ToString())),
                Field(6, CanonicalEnvelope.Enum(provenance.ToString())),
                Field(7, CanonicalEnvelope.String(AnalysisContractGuard.RequireRelativePath(relativePath))),
                Field(8, CanonicalEnvelope.String(locator)),
                Field(9, CanonicalEnvelope.UInt64((ulong)occurrenceCount)),
                Field(10, CanonicalEnvelope.String(catalogRuleIdentity)),
            ]);
    }

    public static Guid CreateDeterministicIdentity(string kind, string fingerprint) =>
        CanonicalEnvelope.DeriveUuid(kind, RequireHashBytes(fingerprint));

    private static PreparedCanonicalArtifact CreateArtifact(string domain, IEnumerable<CanonicalField> fields)
    {
        byte[] envelope = CanonicalEnvelope.Create(domain, fields);
        return new PreparedCanonicalArtifact(envelope, Convert.ToHexStringLower(SHA256.HashData(envelope)));
    }

    private static CanonicalField Field(ushort tag, CanonicalValue value) => new(tag, value);

    private static CanonicalValue Hash(string value) => CanonicalEnvelope.Bytes(RequireHashBytes(value));

    private static byte[] RequireHashBytes(string value)
    {
        string hash = AnalysisContractGuard.RequireSha256(value, nameof(value));
        return Convert.FromHexString(hash);
    }

    private static CanonicalValue NonNegative(long value) => value < 0
        ? throw new ArgumentOutOfRangeException(nameof(value))
        : CanonicalEnvelope.UInt64((ulong)value);

    private static string RequireRevision(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 40 ||
            value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("An exact lowercase 40-hex Profiled Revision is required.", nameof(value));
        }

        return value;
    }

    private static string RequireExact(string value, string expected) =>
        StringComparer.Ordinal.Equals(value, expected)
            ? value
            : throw new ArgumentException($"The schema identity must be {expected}.", nameof(value));
}

namespace Vulgata.Core.Entities;

public enum PreparedScanManifestApprovalState
{
    Approved,
}

public enum PreparedScanAuditOutcome
{
    Eligible,
    Filtered,
    Skipped,
    Selected,
    Warning,
}

public enum PreparedScanAuditReason
{
    EligibleProductionSource,
    ExcludedPathSegment,
    ExcludedTestPath,
    UnsupportedExtension,
    UnsafePath,
    CaseInsensitivePathCollision,
    LinkOrReparsePoint,
    NonRegularEntry,
    BinaryJava,
    InvalidJavaEncoding,
    UnsupportedXmlEncoding,
    UnsafeXml,
    HardBoundExceeded,
    InvalidSplitRange,
    SelectionCapExceeded,
    RelatedFileExpansionFailed,
    SelectedForPreparation,
}

public enum PreparedSelectionReason
{
    DirectBusinessEvidence,
    RelatedCall,
    RelatedImport,
    RelatedInheritance,
    ProductionFallback,
}

public enum CodeGraphIndexState
{
    Complete,
}

public enum JavaReadinessOutcome
{
    Ready,
    NotApplicable,
    Failed,
}

public enum JavaImportState
{
    Usable,
    NotApplicable,
}

public enum AnalysisTransport
{
    Stdio,
}

public sealed record CodeGraphCompletenessProof(
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string SnapshotIdentity,
    string SnapshotRootBindingHash,
    string RuntimeIdentityHash,
    string IndexIdentity,
    string ProofSchemaIdentity,
    bool Initialized,
    CodeGraphIndexState IndexState,
    long PendingAdded,
    long PendingModified,
    long PendingRemoved,
    bool WorktreeMismatch,
    bool ReindexRecommended,
    long PendingReferences,
    long IndexedFileCount,
    string LogicalProjectBinding,
    string LogicalIndexBinding,
    bool RepositoryBindingMatched,
    bool ScanRunBindingMatched,
    bool RevisionBindingMatched,
    bool RuntimeBindingMatched,
    bool SnapshotBindingMatched,
    bool ProjectPathTrusted,
    bool IndexPathTrusted,
    string IdentityHash,
    long ObservedDurationMilliseconds);

public sealed record JavaReadinessProof(
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string SnapshotIdentity,
    string RuntimeIdentityHash,
    string WorkspaceBindingHash,
    string ReadinessSchemaIdentity,
    JavaReadinessOutcome Outcome,
    string JdtLsRelease,
    string JdtLsDistributionArchiveHash,
    int JavaMajor,
    string StreamJsonRpcVersion,
    AnalysisTransport Transport,
    bool WorkspaceExclusive,
    string? ReadinessFile,
    bool InitializeCompleted,
    bool InitializedNotificationSent,
    JavaImportState ImportState,
    long DocumentSymbolCount,
    bool RepositoryBindingMatched,
    bool ScanRunBindingMatched,
    bool RevisionBindingMatched,
    bool RuntimeBindingMatched,
    bool SnapshotBindingMatched,
    string? IdentityHash,
    long ObservedDurationMilliseconds,
    string? FailureCode = null);

public sealed class PreparedScanManifest
{
    public const string CurrentSchemaIdentity = "prepared-scan-manifest/1.0";

    private PreparedScanManifest()
    {
    }

    public Guid Id { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public Guid CoordinationId { get; private set; }
    public string ProfiledRevision { get; private set; } = string.Empty;
    public string RequestedRef { get; private set; } = string.Empty;
    public string SchemaIdentity { get; private set; } = CurrentSchemaIdentity;
    public string SnapshotIdentity { get; private set; } = string.Empty;
    public string RuntimeIdentityHash { get; private set; } = string.Empty;
    public string CodeGraphProofIdentityHash { get; private set; } = string.Empty;
    public string JavaReadinessIdentityHash { get; private set; } = string.Empty;
    public long CodeGraphObservedDurationMilliseconds { get; private set; }
    public long JavaReadinessObservedDurationMilliseconds { get; private set; }
    public string CatalogSchemaIdentity { get; private set; } = string.Empty;
    public string CatalogHash { get; private set; } = string.Empty;
    public int CandidateCount { get; private set; }
    public int EligibleCount { get; private set; }
    public int FilteredCount { get; private set; }
    public int SkippedCount { get; private set; }
    public int SelectedCount { get; private set; }
    public int TotalConfirmedEvidenceCount { get; private set; }
    public int TotalHintCount { get; private set; }
    public int TotalWarningCount { get; private set; }
    public int RetainedClassificationCount { get; private set; }
    public bool EvidenceTruncated { get; private set; }
    public Guid ContextSnapshotId { get; private set; }
    public string PreparedScanContextIdentityHash { get; private set; } = string.Empty;
    public string ContextContentHash { get; private set; } = string.Empty;
    public string PairSelectionFingerprint { get; private set; } = string.Empty;
    public string Fingerprint { get; private set; } = string.Empty;
    public PreparedScanManifestApprovalState ApprovalState { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ApprovedAt { get; private set; }
    public ScanRun? ScanRun { get; private set; }
    public ICollection<PreparedScanManifestEntry> Entries { get; } = [];
    public ICollection<PreparedScanAudit> Audits { get; } = [];
}

public sealed class PreparedScanManifestEntry
{
    private PreparedScanManifestEntry()
    {
    }

    public Guid FileCodeUnitId { get; private set; }
    public Guid PreparedScanManifestId { get; private set; }
    public Guid CoordinationId { get; private set; }
    public string FileCodeUnitIdentityFingerprint { get; private set; } = string.Empty;
    public int DirectBusinessScore { get; private set; }
    public int FinalBusinessScore { get; private set; }
    public PreparedSelectionReason SelectionReason { get; private set; }
    public int ManifestOrdinal { get; private set; }
    public int PairSelectionOrdinal { get; private set; }
    public PreparedScanManifest? PreparedScanManifest { get; private set; }
    public CodeUnit? FileCodeUnit { get; private set; }
}

public sealed class PreparedScanAudit
{
    private PreparedScanAudit()
    {
    }

    public Guid Id { get; private set; }
    public Guid PreparedScanManifestId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public int Ordinal { get; private set; }
    public string RelativePath { get; private set; } = string.Empty;
    public PreparedScanAuditOutcome Outcome { get; private set; }
    public PreparedScanAuditReason Reason { get; private set; }
    public string? FileHash { get; private set; }
    public long? RawByteCount { get; private set; }
    public int? LogicalLineCount { get; private set; }
    public string? FileCodeUnitIdentityFingerprint { get; private set; }
    public int? PairSelectionOrdinal { get; private set; }
    public Guid? PreparedScanManifestEntryId { get; private set; }
    public string DetailFingerprint { get; private set; } = string.Empty;
    public PreparedScanManifest? PreparedScanManifest { get; private set; }
    public PreparedScanManifestEntry? PreparedScanManifestEntry { get; private set; }
}

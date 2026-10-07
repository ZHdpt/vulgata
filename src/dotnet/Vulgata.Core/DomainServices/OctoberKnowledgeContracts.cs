using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>Versioned October contracts. Limits constrain a host grant; they never enlarge it.</summary>
public static class KnowledgeContract
{
    public const int SchemaVersion = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;
    public const int MaximumQueryCharacters = 512;
    public const int MaximumContentBytes = 64 * 1024;
    public static TimeSpan ContinuationLifetime => TimeSpan.FromMinutes(30);
}

public enum KnowledgeMode { Business, IT }
public enum KnowledgePurpose { Reader, Chat, Graph, Resolution, Synthesis }
public enum DocumentKind { CodeLogic, BusinessLogic }
public enum ClaimQualification { SourceSupported, Advisory, Hypothesis, Uncertain, Mixed }
public enum ContributorKind { Source = 0, GlobalAdvisory = 1, SystemAdvisory = 2, RepositoryAdvisory = 3, SourceMetadata = 4 }
public enum CoverageDisposition { Accepted, CompletedZeroUnit, Failed, Cancelled, Unprocessed }
public enum RelationshipKind { References, GeneratedFrom }
public enum OperationKind { AcceptFile = 0, FreezeCorpus = 1, PublishSelection = 2, FinalizeCorpus = 3, AcceptResolution = 4, BeginGenerate = 5 }
public enum KnowledgeWriteStatus
{
    Committed, AlreadyCommitted, Conflict, InvalidProposal, NotCompleted, StaleOrFenced,
    Denied, AuthorityUnavailable, CancelledBeforeCommit, StorageUnavailable, CommitIndeterminate,
    LogUnavailable,
}
public enum KnowledgeReadStatus { Success, Unavailable, UnavailableService, InvalidContinuation, ReadBudgetExceeded, Cancelled }
public enum DisclosureStatus { Allowed, Denied, AuthorityUnavailable }

/// <summary>Host-resolved identity; never a connection string supplied by a model or browser.</summary>
public sealed record KnowledgeBinding(string CanonicalDatabaseIdentity, string TargetId, Guid BatchId, string SelectionDigest);
public sealed record PublicationBinding(KnowledgeBinding Knowledge, Guid PublicationId);
public sealed record DocumentVersionReference(Guid BatchId, Guid LogicalDocumentId, Guid DocumentVersionId);
public sealed record DocumentHandle(Guid BatchId, Guid PublicationId, Guid LogicalDocumentId, Guid DocumentVersionId, string? SectionId = null);
public sealed record OperationKey(Guid BatchId, OperationKind Kind, Guid OperationId);
public sealed record PreparedOrigin(Guid RepositoryId, string RootBindingIdentity, string RelativePath, string SourceDigest, string PreparedVersion);
public sealed record SourceAnchor(PreparedOrigin Origin, int StartLine, int EndLine);
public sealed record Contributor(ContributorKind Kind, Guid? RepositoryId, Guid? SystemId, string? RootBindingIdentity, Guid? AdvisoryRecordId, string ContentDigest)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreparedOrigin? SourceOrigin { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourceMetadataOrigin? MetadataOrigin { get; init; }
}
public sealed record EvidenceReference(DocumentVersionReference? Document, string? ClaimGroupId, SourceAnchor? Source, Guid? AdvisoryRecordId, string ContentDigest);
public sealed record QualifiedClaimGroup(string ClaimGroupId, string Text, ClaimQualification Qualification,
    ImmutableArray<EvidenceReference> Evidence, ImmutableArray<string> Limitations);
public sealed record DocumentSection(string SectionId, int Ordinal, string Title, ImmutableArray<QualifiedClaimGroup> Claims);
public sealed record SemanticUnitProposal(string SemanticSelector, string? ParentSelector, string Title,
    string ContentJson, ImmutableArray<SourceAnchor> Anchors, ImmutableArray<DocumentSection> Sections);
public sealed record CommunicationObservation(string ObservationKey, string OwnerSelector, string Role, string Mechanism,
    string Operation, string? LogicalNamespace, ImmutableArray<string> LiteralsOrExpressions,
    ClaimQualification Qualification, ImmutableArray<EvidenceReference> Evidence);
public sealed record Uncertainty(string Key, string Explanation, ImmutableArray<EvidenceReference> Evidence, ImmutableArray<string> Limitations);
public sealed record FileCoverage(Guid AdmissionId, PreparedOrigin Origin, CoverageDisposition Disposition, string? Reason);
public sealed record CompletedFileProposal(KnowledgeBinding Binding, Guid AdmissionId, Guid AttemptId,
    PreparedOrigin Origin, string CanonicalProposalDigest, ImmutableArray<SemanticUnitProposal> Units,
    ImmutableArray<CommunicationObservation> Observations, ImmutableArray<Uncertainty> Uncertainties,
    ImmutableArray<Contributor> Contributors, FileCoverage Coverage, int SchemaVersion = KnowledgeContract.SchemaVersion);

/// <summary>
/// Opaque host-issued proof, not JSON authority. Immutable analysis completion never grants
/// permission after a fence, expiry or authority loss. The writer must verify the trusted issuer.
/// </summary>
public abstract class CompletedFileProof
{
    protected CompletedFileProof(Guid admissionId, Guid attemptId, string proposalDigest)
    {
        AdmissionId = admissionId;
        AttemptId = attemptId;
        ProposalDigest = proposalDigest;
    }

    public Guid AdmissionId { get; }
    public Guid AttemptId { get; }
    public string ProposalDigest { get; }
}

public sealed record CompletedFileSubmission(OperationKey Operation, CompletedFileProposal Proposal, CompletedFileProof Proof);
public sealed record AcceptedFile(Guid ManifestId, Guid AdmissionId, string ProposalDigest,
    ImmutableArray<DocumentVersionReference> Documents, FileCoverage Coverage);
public sealed record KnowledgeWriteResult(KnowledgeWriteStatus Status, OperationKey Operation,
    AcceptedFile? AcceptedFile = null, CorpusHandle? Corpus = null, PublicationBinding? Publication = null);

/// <summary>Mandatory server-side proof/issuer and live producer authority verification.</summary>
public interface IOctoberFileAcceptanceAuthority
{
    Task<DisclosureStatus> VerifyAsync(CompletedFileSubmission submission, CancellationToken cancellationToken = default);
}

public enum CommitEligibilityStatus { Eligible, StaleOrFenced, Denied, AuthorityUnavailable, Cancelled }
public enum CommitSubmissionStatus { Submitted, StaleOrFenced, Denied, AuthorityUnavailable, CancelledBeforeSubmission }
public enum CommitSettlement { Committed, ConfirmedNotCommitted, Indeterminate }
public sealed record CommitAttempt(OperationKey Operation, Guid AttemptId, string PayloadDigest);
public sealed record CommitGuardResult(CommitSubmissionStatus Status, CommitSettlement? Settlement);
/// <summary>Created synchronously only AFTER the database commit API has been invoked; never a deferred task factory.</summary>
public sealed record SubmittedCommit(Task<CommitSettlement> Settlement);

/// <summary>
/// Scan-owned process-local guard shared with fences. Check current eligibility, monotonic
/// deadline, hard stop and fresh authority immediately before invoking submitAsync under the
/// same guard, with no unguarded await between checks and submission. The synchronous callback
/// invokes the database commit API before returning SubmittedCommit; it must not defer that call
/// using Task.Run, an async preamble or a queued action. Callback invocation alone is not submission.
/// Await the returned settlement task outside
/// the submission boundary. Cancellation after submission is not proof of rollback; an absent
/// lookup row while the original write may still commit is indeterminate. No durable receipt.
/// </summary>
public interface IOctoberCommitSubmissionGuard
{
    /// <summary>Required before transaction entry; a check is not a grant to skip submission-time rechecks.</summary>
    Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(CommitAttempt attempt, CancellationToken cancellationToken = default);

    Task<CommitGuardResult> SubmitAsync(CommitAttempt attempt,
        Func<CancellationToken, SubmittedCommit> submitAsync, CancellationToken cancellationToken = default);
}

public sealed record CorpusHandle(KnowledgeBinding Binding, Guid CorpusId, string CorpusDigest);
public sealed record CorpusFreezeRequest(OperationKey Operation, KnowledgeBinding Binding, string CanonicalPayloadDigest,
    ImmutableArray<AcceptedFile> AcceptedFiles, ImmutableArray<FileCoverage> TerminalCoverage);
public sealed record AcceptedRelationship(Guid RelationshipVersionId, RelationshipKind Kind,
    DocumentVersionReference From, DocumentVersionReference To, string Meaning,
    ImmutableArray<EvidenceReference> SupportingEvidence, ImmutableArray<EvidenceReference> OpposingEvidence);
public sealed record BusinessLogicContent(string Title, string ContentJson, ImmutableArray<DocumentSection> Sections);
public sealed record FlowPublication(Guid LogicalFlowId, Guid FlowGroupVersionId, DocumentVersionReference BusinessLogic,
    ImmutableArray<DocumentVersionReference> DocumentDependencies, ImmutableArray<Guid> RelationshipDependencies,
    ImmutableArray<string> UncertaintyDependencies, ImmutableArray<Contributor> Contributors, BusinessLogicContent Content);
public sealed record PublicationRequest(OperationKey Operation, CorpusHandle Corpus, string CanonicalPayloadDigest,
    ImmutableArray<DocumentVersionReference> Documents, ImmutableArray<AcceptedRelationship> Relationships,
    ImmutableArray<Uncertainty> Uncertainties, ImmutableArray<FlowPublication> Flows,
    ImmutableArray<FileCoverage> Coverage);

/// <summary>
/// Producer-only port, never an agent tool. All writes use exact operation/digest reconciliation,
/// live authority and the shared commit guard; accepted drafts stay private until publication.
/// Empty relationship/flow sets are valid for CL-only publication before B1/B2.
/// </summary>
public interface IOctoberKnowledgeWriter
{
    Task<KnowledgeWriteResult> AcceptCompletedFileAsync(CompletedFileSubmission submission, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> LookupOperationAsync(KnowledgeBinding binding, OperationKey operation, string payloadDigest, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> FreezeCorpusAsync(CorpusFreezeRequest request, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> PublishSelectionAsync(PublicationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Host-derived subject/authentication binding, never credentials chosen by a model.</summary>
public sealed record KnowledgeSubject(string SubjectId, string AuthenticationBinding);
public sealed record ReadSelectionRequest(PublicationBinding Publication, KnowledgeSubject Subject,
    KnowledgeMode Mode, KnowledgePurpose Purpose, ImmutableArray<Guid> RepositoryScope);
/// <summary>Opaque handle bound to exact publication/subject/purpose/mode/scope; no lasting permission.</summary>
public sealed record ReadSelection(string Token);
public sealed record ContinuationToken(string Value);
public sealed record DisclosureRequest(ReadSelectionRequest Selection, ImmutableArray<Contributor> AllContributors,
    DocumentKind? Kind);
public interface IOctoberKnowledgeDisclosurePolicy
{
    Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default);
}

public sealed record ReadSelectionResult(KnowledgeReadStatus Status, ReadSelection? Selection);
public sealed record KnowledgeSearchRequest(ReadSelection Selection, string Query, int PageSize = KnowledgeContract.DefaultPageSize,
    ContinuationToken? Continuation = null);
public sealed record ExactReadRequest(ReadSelection Selection, DocumentHandle Document, ContinuationToken? Continuation = null);
public sealed record RelatedReadRequest(ReadSelection Selection, DocumentHandle Document, int PageSize = KnowledgeContract.DefaultPageSize,
    ContinuationToken? Continuation = null);
public sealed record CoverageProjection(bool IsPartial, ImmutableArray<string> PermittedGaps);
public sealed record DocumentSummary(DocumentHandle Handle, DocumentKind Kind, string Title,
    ImmutableArray<QualifiedClaimGroup> QualifiedExcerpt);
public sealed record SearchPage(KnowledgeReadStatus Status, ImmutableArray<DocumentSummary> Documents,
    ContinuationToken? Next, CoverageProjection? Coverage);
/// <summary>Next continues this exact content stream; consumers cannot guess offsets or tokens.</summary>
public sealed record ContentPage(KnowledgeReadStatus Status, DocumentSummary? Document, string? Content,
    ImmutableArray<QualifiedClaimGroup> Claims, ContinuationToken? Next, CoverageProjection? Coverage);
public sealed record RelatedDocument(RelationshipKind Kind, DocumentHandle From, DocumentHandle To, string Meaning)
{
    /// <summary>Exact accepted edge identity. Null legacy results are not eligible graph edges.</summary>
    public Guid? RelationshipVersionId { get; init; }
}
public sealed record RelatedPage(KnowledgeReadStatus Status, ImmutableArray<RelatedDocument> Relationships,
    ImmutableArray<Uncertainty> PermittedUncertainties, ContinuationToken? Next, CoverageProjection? Coverage);

/// <summary>
/// Every call requires live subject/purpose/mode/all-contributor authorization before disclosure.
/// Business returns BL only, including metadata, paths, counts and related targets. Hidden/missing
/// exact handles both return metadata-free Unavailable. Protected cursors grant no authority.
/// </summary>
public interface IOctoberKnowledgeReader
{
    Task<ReadSelectionResult> OpenReadSelectionAsync(ReadSelectionRequest request, CancellationToken cancellationToken = default);
    Task<SearchPage> SearchAsync(KnowledgeSearchRequest request, CancellationToken cancellationToken = default);
    Task<ContentPage> ReadExactAsync(ExactReadRequest request, CancellationToken cancellationToken = default);
    Task<RelatedPage> GetRelatedAsync(RelatedReadRequest request, CancellationToken cancellationToken = default);
}

public enum BusinessWorkKind { Resolution, Synthesis }
public enum ProviderAdmissionStatus { Granted, Denied, BudgetExhausted, DeadlineExceeded, HardStopped, AuthorityUnavailable }
public enum ProviderAttemptOutcome { Completed, Failed, Cancelled }
public sealed record ProviderAttemptRequest(Guid RunId, OperationKey FinalizationOperation, Guid AttemptId,
    BusinessWorkKind WorkKind, string ProviderProfileId, TimeSpan MaximumDuration, long MaximumTokens);
public sealed record ProviderAttemptUsage(long? InputTokens, long? OutputTokens, decimal? Cost,
    TimeSpan WaitDuration, TimeSpan ExecutionDuration, ProviderAttemptOutcome Outcome);
public sealed record ProviderAdmission(ProviderAdmissionStatus Status, IOctoberProviderAttemptLease? Lease);

/// <summary>
/// One host-owned process-local cap/request/rate/budget/deadline for all Worker and Business
/// attempts. Each retry acquires anew. Finalization orchestration holds no parent permit.
/// Queued/rate-denied/expired/hard-stopped work sends nothing; no independent Business pool.
/// </summary>
public interface IOctoberExecutionScope
{
    Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Recheck host eligibility/rate immediately before sending. Cancellation links deadline/hard stop.
/// Settle actual or explicitly unknown usage exactly once (unknown never zero), conservatively
/// reserve or stop further constrained calls. Release only after transport settlement/disposal;
/// unconfirmed cancellation stops further dispatch. Do not double charge/release.
/// </summary>
public interface IOctoberProviderAttemptLease : IAsyncDisposable
{
    Guid AttemptId { get; }
    CancellationToken CancellationToken { get; }
    Task<ProviderAdmissionStatus> CheckBeforeSendAsync(CancellationToken cancellationToken = default);
    Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default);
}

public enum BusinessFinalizationStatus
{
    Published, PublishedPartial, NoContent, Failed, Denied, AuthorityUnavailable, HardStopped, CommitIndeterminate,
}
/// <summary>Replay retains the original explicit outcome and exact publication; it never regenerates.</summary>
public sealed record BusinessFinalizationResult(BusinessFinalizationStatus Status, OperationKey Operation,
    PublicationBinding? Publication, CoverageProjection Coverage, bool IsReplay = false);

public sealed record FinalizationRequest(OperationKey Operation, CorpusHandle Corpus, string CanonicalPayloadDigest,
    IOctoberExecutionScope ExecutionScope);
public interface IOctoberBusinessFinalizer
{
    Task<BusinessFinalizationResult> FinalizeAsync(FinalizationRequest request, CancellationToken cancellationToken = default);
}



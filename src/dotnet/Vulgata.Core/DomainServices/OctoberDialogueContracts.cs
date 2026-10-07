using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.DomainServices.OctoberDialogue;

/// <summary>Durable dialogue protocol ceilings; execution configuration may only narrow applicable limits.</summary>
public static class OctoberDialogueLimits
{
    public const int SchemaVersion = 1;
    public const int MaximumQuestionScalars = 8_000;
    public const int MaximumQuestionUtf8Bytes = 32_000;
    public const int MaximumAnswerUtf8Bytes = 65_536;
    public const int MaximumOwnerSubjectUtf8Bytes = 1_024;
    public const int MaximumDatabaseIdentityUtf8Bytes = 4_096;
    public const int MaximumTargetIdUtf8Bytes = 256;
    public const int MaximumProviderIdentityUtf8Bytes = 256;
    public const int DigestCharacters = 64;
    public const int MaximumRepositories = 256;
    public const int MaximumSectionIdUtf8Bytes = 1_024;
    public const int MaximumReferences = 256;
    public const int MaximumConsumedTargets = 256;
    public const int MaximumReferenceArrayUtf8Bytes = 524_288;
    public const int MaximumConsumedArrayUtf8Bytes = 524_288;
    public const int MaximumHistoryTurns = 12;
    public const int MaximumHistoryProjectionUtf8Bytes = 49_152;
    public const int MaximumBeginEnvelopeUtf8Bytes = 1_048_576;
    public const int MaximumTerminalEnvelopeUtf8Bytes = 2_097_152;
    public static TimeSpan MaximumPersistenceAttempt => TimeSpan.FromSeconds(10);
}

public enum DialogueTurnState { Admitted = 0, Running = 1, Saving = 2, SaveIndeterminate = 3, Completed = 4, Interrupted = 5, Failed = 6 }
public enum DialogueTerminalState { Completed = 0, Interrupted = 1, Failed = 2 }
public enum DialogueTerminalReason
{
    None = 0, UserCancelled = 1, DeadlineExceeded = 2, BudgetExceeded = 3, ProviderInterrupted = 4,
    InvalidConfiguration = 5, ProtocolFailure = 6, AuthorizationDenied = 7, AuthorityUnavailable = 8,
    OwnerLost = 9, ExecutionFailure = 10
}
public enum DialogueProviderFinish
{
    ValidStop = 0, Length = 1, ContentFilter = 2, Truncated = 3, UnfinishedTools = 4,
    Invalid = 5, Cancelled = 6, NotStarted = 7, Unknown = 8
}
public enum DialogueStatus
{
    Succeeded = 0, Existing = 1, Busy = 2, Conflict = 3, Invalid = 4, StaleFence = 5,
    Denied = 6, AuthorityUnavailable = 7, Indeterminate = 8, ConfirmedAbsent = 9
}

/// <summary>Host-issued identities; immutable Chat purpose and owner/selection. Authentication binding is never persisted.</summary>
public sealed record DialogueConversationIdentity(Guid ConversationId, Guid ContextId, string OwnerSubjectId,
    PublicationBinding Publication, KnowledgeMode Mode, ImmutableArray<Guid> RepositoryScope);
public sealed record DialogueConversationCreation(PublicationBinding Publication, KnowledgeMode Mode, ImmutableArray<Guid> RepositoryScope);
/// <summary>Null CreateIfMissing is open-only. Non-null creation requires host-issued IDs and current subject as owner.</summary>
public sealed record DialogueConversationOpenRequest(Guid ConversationId, Guid ContextId, DialogueConversationCreation? CreateIfMissing);
public sealed record DialogueConversation(DialogueConversationIdentity Identity, DateTimeOffset CreatedAtUtc,
    long NextTurnSequence, Guid? ActiveTurnId);
public sealed record DialogueConversationResult(DialogueStatus Status, DialogueConversation? Conversation);

public sealed record DialogueExecutionIdentity(Guid OwnerGeneration, Guid AttemptId);
public sealed record DialogueBeginRequest(Guid ConversationId, Guid ContextId, Guid ClientRequestKey, string Question,
    string RequestDigest, string ProviderProfileId, string ProviderProfileRevision, string PromptDigest,
    ImmutableArray<Guid> HistoryTurnIds, string HistoryProjectionDigest, [property: JsonIgnore] KnowledgeDependencyHandle InitialDependencies,
    OctoberChatLimits Limits, DialogueExecutionIdentity Execution);
/// <summary>Host-only durable snapshot. Existing/reconciled admissions never authorize another dispatch.</summary>
public sealed record DialogueAdmission(DialogueConversationIdentity Conversation, [property: JsonIgnore] DialogueBeginRequest Request,
    Guid TurnId, long Sequence, long Fence, DateTimeOffset AdmittedAtUtc, DateTimeOffset DeadlineUtc, DialogueTurnState State);
public sealed record DialogueBeginResult(DialogueStatus Status, DialogueAdmission? Admission, bool MayDispatch);
public sealed record DialogueAdmissionLookup(Guid ConversationId, Guid ContextId, Guid ClientRequestKey, string RequestDigest);
public sealed record DialogueTurnOwnership(Guid ConversationId, Guid ContextId, Guid TurnId,
    DialogueExecutionIdentity Execution, long Fence);
public sealed record DialogueTransitionRequest(DialogueTurnOwnership Owner, DialogueTurnState ExpectedState, DialogueTurnState NextState);
public sealed record DialogueTransitionResult(DialogueStatus Status, DialogueTurnState? State);

/// <summary>Proof authenticates all reference fields and source coverage; the footer is not the inference dependency set.</summary>
public sealed record DialogueConsumedTarget(OctoberChatReference Reference, [property: JsonIgnore] KnowledgeConsumedContentHandle Proof);
/// <summary>
/// Host-only request. Full inference lineage is required for content. A null lineage is allowed only for
/// the explicitly sanitized, empty authorization-failure closure described in dialogue-contract.md.
/// It never marks an unknown lineage as an authorized empty set or creates eligible inference history.
/// </summary>
public sealed record DialogueTerminalSaveRequest(DialogueTurnOwnership Owner, Guid SaveOperationId, string OutcomeDigest,
    DialogueTerminalState TerminalState, DialogueTerminalReason Reason, DialogueProviderFinish ProviderFinish,
    string AnswerText, ImmutableArray<OctoberChatReference> References, ImmutableArray<DialogueConsumedTarget> ConsumedReads,
    [property: JsonIgnore] KnowledgeDependencyHandle? InferenceDependencies, ImmutableArray<Guid> HistoryTurnIds, DateTimeOffset GenerationEndedAtUtc);
public sealed record DialogueTerminalReceipt(Guid ConversationId, Guid ContextId, Guid TurnId, long Sequence, long Fence,
    Guid SaveOperationId, string OutcomeDigest, DialogueTerminalState TerminalState, DialogueTerminalReason Reason,
    DateTimeOffset CommittedAtUtc);
/// <summary>Only a currently authorized projection, never a durable permission grant or a model-facing store DTO.</summary>
public sealed record DialogueTerminalProjection(DialogueConversationIdentity Conversation, Guid TurnId, long Sequence,
    string Question, string AnswerText, DialogueTerminalState TerminalState, DialogueTerminalReason Reason,
    ImmutableArray<OctoberChatReference> References, [property: JsonIgnore] KnowledgeDependencyHandle? InferenceDependencies);
public sealed record DialogueTerminalResult(DialogueStatus Status, DialogueTerminalReceipt? Receipt,
    DialogueTerminalProjection? Projection);
public sealed record DialogueTerminalLookup(Guid ConversationId, Guid ContextId, Guid TurnId, Guid SaveOperationId,
    long ExpectedFence, string OutcomeDigest);

public sealed record DialogueHistoryRequest(Guid ConversationId, Guid ContextId, long? BeforeSequenceExclusive,
    int MaximumTurns = OctoberDialogueLimits.MaximumHistoryTurns,
    int MaximumUtf8Bytes = OctoberDialogueLimits.MaximumHistoryProjectionUtf8Bytes);
public sealed record DialogueHistoryTurn(Guid TurnId, long Sequence, string Question, string AnswerText,
    ImmutableArray<OctoberChatReference> References, [property: JsonIgnore] KnowledgeDependencyHandle InferenceDependencies);
/// <summary>
/// Completed, contiguous, currently authorized inference history only. Null dependencies requires no
/// returned turns; it is not a no-Knowledge attestation. Capsules stay outside the model projection.
/// </summary>
public sealed record DialogueHistoryResult(DialogueStatus Status, ImmutableArray<DialogueHistoryTurn> Turns,
    bool ContextOmitted, string? ProjectionDigest, [property: JsonIgnore] KnowledgeDependencyHandle? Dependencies);
/// <summary>Authorized labelled terminal display only; never feed this transcript directly to inference.</summary>
public sealed record DialogueTranscriptResult(DialogueStatus Status, ImmutableArray<DialogueTerminalProjection> Turns, bool ContextOmitted);
public sealed record DialogueOrphanRequest(Guid OrphanOperationId, Guid ConversationId, Guid ContextId, Guid TurnId,
    DialogueExecutionIdentity ExpectedExecution, long ExpectedFence);
public sealed record DialogueOrphanResult(DialogueStatus Status, DialogueTerminalReceipt? Receipt);

/// <summary>
/// Application-only persistence port, never an agent tool or browser authority surface. All nonterminal
/// states occupy one durable conversation slot; a terminal transaction saves outcome/references/lineage
/// and clears the slot together. Every disclosure requires fresh current owner and dependency authority.
/// </summary>
public interface IOctoberDialogueStore
{
    Task<DialogueConversationResult> OpenConversationAsync(DialogueConversationOpenRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default);
    Task<DialogueBeginResult> BeginTurnAsync(DialogueBeginRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default);
    Task<DialogueBeginResult> LookupAdmissionAsync(DialogueAdmissionLookup request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default);
    Task<DialogueTransitionResult> TransitionTurnAsync(DialogueTransitionRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default);
    Task<DialogueTerminalResult> SaveTerminalAsync(DialogueTerminalSaveRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default);
    Task<DialogueTerminalResult> LookupTerminalAsync(DialogueTerminalLookup request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default);
    Task<DialogueHistoryResult> ReadHistoryAsync(DialogueHistoryRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default);
    Task<DialogueTranscriptResult> ReadTranscriptAsync(DialogueHistoryRequest request, KnowledgeSubject subject,
        CancellationToken cancellationToken = default);
    /// <summary>Trusted host cleanup only; returns no content and requires proven owner death plus settled owned resources.</summary>
    Task<DialogueOrphanResult> InterruptOrphanAsync(DialogueOrphanRequest request, CancellationToken cancellationToken = default);
}

public enum DialogueExecutionStatus { Verified = 0, NotVerified = 1, Unavailable = 2 }
/// <summary>
/// Mandatory trusted host integration, not caller booleans or age thresholds. A failed or cancelled
/// generation can be settled: physical request termination and permit/resource release are the facts.
/// </summary>
public interface IOctoberDialogueExecutionAuthority
{
    Task<DialogueExecutionStatus> VerifyAdmissionAsync(DialogueBeginRequest request, CancellationToken cancellationToken = default);
    Task<DialogueExecutionStatus> VerifyTerminalAsync(DialogueAdmission admission, DialogueTerminalSaveRequest request,
        CancellationToken cancellationToken = default);
    Task<DialogueExecutionStatus> VerifyOwnerDeathAsync(DialogueExecutionIdentity execution, CancellationToken cancellationToken = default);
}

public enum DialogueMutationKind { CreateConversation = 0, AdmitTurn = 1, TransitionTurn = 2, SaveTerminal = 3, InterruptOrphan = 4 }
/// <summary>Trusted operation context. Null Subject is legal only for no-content orphan cleanup.</summary>
public sealed record DialogueCommitAttempt(DialogueMutationKind Kind, Guid MutationId, DialogueConversationIdentity Conversation,
    Guid? TurnId, DialogueExecutionIdentity? Execution, long? ExpectedFence, string PayloadDigest,
    KnowledgeSubject? Subject, KnowledgeDependencyHandle? Dependencies);
/// <summary>
/// Reuses F1 mechanics without a Scan execution scope. Check live ownership/fence/phase and applicable
/// authority before entry and under the same fence guard at submission. The synchronous callback invokes
/// the actual provider CommitAsync before returning SubmittedCommit; await settlement outside the guard.
/// Sanitized revocation closure and trusted orphan cleanup grant no content disclosure. Cancellation or
/// an absent lookup row never proves that a submitted commit cannot still finish.
/// </summary>
public interface IOctoberDialogueCommitGuard
{
    Task<CommitEligibilityStatus> CheckBeforeTransactionAsync(DialogueCommitAttempt attempt, CancellationToken cancellationToken = default);
    Task<CommitGuardResult> SubmitAsync(DialogueCommitAttempt attempt, Func<CancellationToken, SubmittedCommit> submitAsync,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One Business-owned codec; implement the exact canonical projections and envelope measurements in
/// dialogue-contract.md using OctoberKnowledgePolicy. No consumer-specific hash or serializer fallback.
/// </summary>
public interface IOctoberDialoguePayloadCodec
{
    string ComputeBeginDigest(DialogueConversationIdentity conversation, DialogueBeginRequest request);
    string ComputeOutcomeDigest(DialogueTerminalSaveRequest request);
    string ComputeHistoryProjectionDigest(ImmutableArray<DialogueHistoryTurn> turns);
    int MeasureBeginEnvelopeUtf8Bytes(DialogueBeginRequest request);
    int MeasureTerminalEnvelopeUtf8Bytes(DialogueTerminalSaveRequest request);
    ImmutableArray<byte> EncodeHistoryProjectionUtf8(ImmutableArray<DialogueHistoryTurn> turns);
    int MeasureHistoryProjectionUtf8Bytes(ImmutableArray<DialogueHistoryTurn> turns);
}

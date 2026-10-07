using Vulgata.Core.DomainServices.OctoberDialogue;

namespace Vulgata.Core.Entities.OctoberDialogue;

/// <summary>Immutable conversation identity with serialized monotonic admission counters.</summary>
public sealed class DialogueConversationRecord
{
    public Guid ConversationId { get; set; }
    public Guid ContextId { get; set; }
    public string OwnerSubjectId { get; set; } = "";
    public Guid BatchId { get; set; }
    public Guid PublicationId { get; set; }
    public string IdentityJson { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public long NextTurnSequence { get; set; }
    public long NextFence { get; set; }
}

/// <summary>The original admission is immutable; only guarded phase and orphan fencing can change.</summary>
public sealed class DialogueTurnRecord
{
    public Guid ConversationId { get; set; }
    public Guid TurnId { get; set; }
    public Guid ClientRequestKey { get; set; }
    public long Sequence { get; set; }
    public long Fence { get; set; }
    public Guid OwnerGeneration { get; set; }
    public Guid AttemptId { get; set; }
    public string RequestDigest { get; set; } = "";
    public string BeginEnvelopeJson { get; set; } = "";
    public DateTimeOffset AdmittedAtUtc { get; set; }
    public DateTimeOffset DeadlineUtc { get; set; }
    public DialogueTurnState State { get; set; }
}

/// <summary>One immutable terminal fact; its insertion and terminal phase release the slot together.</summary>
public sealed class DialogueTerminalRecord
{
    public Guid ConversationId { get; set; }
    public Guid TurnId { get; set; }
    public Guid BatchId { get; set; }
    public Guid PublicationId { get; set; }
    public Guid SaveOperationId { get; set; }
    public string OutcomeDigest { get; set; } = "";
    public DialogueTerminalState TerminalState { get; set; }
    public DialogueTerminalReason Reason { get; set; }
    public long Fence { get; set; }
    public DateTimeOffset CommittedAtUtc { get; set; }
    public string TerminalEnvelopeJson { get; set; } = "";
    public string VerifiedConsumedJson { get; set; } = "[]";
}

/// <summary>Exact published version/section references, never mutable latest-document links.</summary>
public sealed class DialogueReferenceRecord
{
    public Guid ConversationId { get; set; }
    public Guid TurnId { get; set; }
    public int Ordinal { get; set; }
    public Guid BatchId { get; set; }
    public Guid PublicationId { get; set; }
    public Guid LogicalDocumentId { get; set; }
    public Guid VersionId { get; set; }
    public string? SectionId { get; set; }
}

using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.Entities.OctoberKnowledge;

/// <summary>One immutable, policy-recomputed disposition of an exact pinned inquiry.</summary>
public sealed class ResolutionItem : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid ItemId { get; set; }
    public OperationKind OperationKind { get; set; } = OperationKind.AcceptResolution;
    public Guid OperationId { get; set; }
    public string InquiryFingerprint { get; set; } = "";
    public string PolicyVersion { get; set; } = "";
    public string? WitnessJson { get; set; }
    public string OutcomeJson { get; set; } = "";
    public string ContributorsJson { get; set; } = "";
    public string PlanDispositionsJson { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

/// <summary>All corpus material admitted to the inquiry, including uncited and third-party CL.</summary>
public sealed class ResolutionDocumentDependencyRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid ItemId { get; set; }
    public Guid LogicalDocumentId { get; set; }
    public Guid VersionId { get; set; }
    /// <summary>Exact inquiry ownership, separate from the complete authorization/input dependency set.</summary>
    public bool IsInquiryCaller { get; set; }
}

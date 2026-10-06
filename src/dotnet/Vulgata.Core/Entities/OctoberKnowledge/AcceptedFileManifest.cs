using Vulgata.Core.DomainServices.OctoberKnowledge;
namespace Vulgata.Core.Entities.OctoberKnowledge;

/// <summary>Append-only accepted records; no model-supplied persistent identities.</summary>
public interface IOctoberKnowledgeRecord { }
public sealed class KnowledgeOperation : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public OperationKind Kind { get; set; }
    public Guid OperationId { get; set; }
    public string PayloadDigest { get; set; } = "";
    public string CommittedResultJson { get; set; } = "";
    public string ContributorsJson { get; set; } = "";
    public DateTimeOffset CommittedAtUtc { get; set; }
}
public sealed class AcceptedFileManifest : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid ManifestId { get; set; }
    public Guid AdmissionId { get; set; }
    public Guid RepositoryId { get; set; }
    public string RootBindingIdentity { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string SourceDigest { get; set; } = "";
    public string PreparedVersion { get; set; } = "";
    public string ProposalDigest { get; set; } = "";
    public CoverageDisposition CoverageDisposition { get; set; }
    public string? CoverageReason { get; set; }
    public int UnitCount { get; set; }
    public OperationKind OperationKind { get; set; } = OperationKind.AcceptFile;
    public Guid OperationId { get; set; }
}

using Vulgata.Core.DomainServices.OctoberKnowledge;
namespace Vulgata.Core.Entities.OctoberKnowledge;
public sealed class QualifiedClaimGroupRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid VersionId { get; set; }
    public string SectionId { get; set; } = "";
    public string ClaimGroupId { get; set; } = "";
    public string Text { get; set; } = "";
    public ClaimQualification Qualification { get; set; }
    public string EvidenceJson { get; set; } = "";
    public string LimitationsJson { get; set; } = "";
}
public sealed class KnowledgeContributorRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid ManifestId { get; set; }
    public int Ordinal { get; set; }
    public string ContributorJson { get; set; } = "";
}
public sealed class CommunicationObservationRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid ManifestId { get; set; }
    public string ObservationKey { get; set; } = "";
    public Guid UnitId { get; set; }
    public string ContentJson { get; set; } = "";
}
public sealed class KnowledgeUncertaintyRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid ManifestId { get; set; }
    public string Key { get; set; } = "";
    public string ContentJson { get; set; } = "";
}

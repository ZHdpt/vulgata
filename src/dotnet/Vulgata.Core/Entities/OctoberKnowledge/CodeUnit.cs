using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.Entities.OctoberKnowledge;

public sealed class CodeUnit : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid UnitId { get; set; }
    public Guid ManifestId { get; set; }
    public Guid RepositoryId { get; set; }
    public string SemanticKey { get; set; } = "";
    public string SemanticSelector { get; set; } = "";
    public Guid? ParentUnitId { get; set; }
}
public sealed class LogicalDocument : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid LogicalDocumentId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? LogicalFlowId { get; set; }
    public DocumentKind Kind { get; set; }
}

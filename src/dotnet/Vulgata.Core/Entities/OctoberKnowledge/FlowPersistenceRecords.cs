using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.Entities.OctoberKnowledge;

public sealed class BusinessFinalizationRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public OperationKind OperationKind { get; set; } = OperationKind.FinalizeCorpus;
    public Guid OperationId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid? PublicationId { get; set; }
    public string RequestDigest { get; set; } = "";
    public string OutcomeDigest { get; set; } = "";
    public string RequestJson { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public string ContributorsJson { get; set; } = "";
}

public sealed class LogicalFlowRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid LogicalFlowId { get; set; }
    public Guid RootUnitId { get; set; }
    public FlowRootKind RootKind { get; set; }
    public string RootSelector { get; set; } = "";
    public Guid HomeRepositoryId { get; set; }
    public OperationKind OperationKind { get; set; } = OperationKind.FinalizeCorpus;
    public Guid OperationId { get; set; }
}

public sealed class FlowGroupVersionRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid GroupVersionId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid LogicalFlowId { get; set; }
    public Guid RootLogicalDocumentId { get; set; }
    public Guid RootVersionId { get; set; }
    public OperationKind OperationKind { get; set; } = OperationKind.FinalizeCorpus;
    public Guid OperationId { get; set; }
    public string GroupJson { get; set; } = "";
    public string ContributorsJson { get; set; } = "";
}

public sealed class FlowDocumentDependencyRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid GroupVersionId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid LogicalDocumentId { get; set; }
    public Guid VersionId { get; set; }
    public bool IsMember { get; set; }
}

public sealed class FlowRelationshipDependencyRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid GroupVersionId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid RelationshipVersionId { get; set; }
    public bool IsMember { get; set; }
}

public sealed class FlowResolutionUncertaintyDependencyRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid GroupVersionId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid UncertaintyId { get; set; }
    public bool IsSliceQualifier { get; set; }
}

public sealed class FlowFileUncertaintyDependencyRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid GroupVersionId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid ManifestId { get; set; }
    public string Key { get; set; } = "";
}

using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Core.Entities.OctoberKnowledge;

public sealed class CorpusSelectionRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid CorpusId { get; set; }
    public OperationKind OperationKind { get; set; } = OperationKind.FreezeCorpus;
    public Guid OperationId { get; set; }
    public string CorpusDigest { get; set; } = "";
    public string ContributorsJson { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CorpusManifestRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid ManifestId { get; set; }
}

public sealed class CorpusDocumentRecord : IOctoberKnowledgeRecord
{
    public Guid ManifestId { get; set; }
    public Guid BatchId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid LogicalDocumentId { get; set; }
    public Guid VersionId { get; set; }
    public int Ordinal { get; set; }
}

public sealed class CorpusCoverageRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid AdmissionId { get; set; }
    public Guid RepositoryId { get; set; }
    public string CoverageJson { get; set; } = "";
    public string ContributorsJson { get; set; } = "";
}

public sealed class KnowledgePublicationRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid PublicationId { get; set; }
    public Guid CorpusId { get; set; }
    public string CorpusDigest { get; set; } = "";
    public OperationKind OperationKind { get; set; } = OperationKind.PublishSelection;
    public Guid OperationId { get; set; }
    public string ContributorsJson { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class PublicationMemberRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid PublicationId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid LogicalDocumentId { get; set; }
    public Guid VersionId { get; set; }
    public int Ordinal { get; set; }
}

public sealed class PublicationCoverageRecord : IOctoberKnowledgeRecord
{
    public Guid CorpusId { get; set; }
    public Guid BatchId { get; set; }
    public Guid PublicationId { get; set; }
    public Guid AdmissionId { get; set; }
    public Guid RepositoryId { get; set; }
    public string CoverageJson { get; set; } = "";
    public string ContributorsJson { get; set; } = "";
}

public sealed class PublicationUncertaintyRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid PublicationId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid ManifestId { get; set; }
    public string Key { get; set; } = "";
}

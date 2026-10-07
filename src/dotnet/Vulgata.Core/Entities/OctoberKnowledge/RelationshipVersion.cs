namespace Vulgata.Core.Entities.OctoberKnowledge;

public sealed class RelationshipVersion : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid RelationshipVersionId { get; set; }
    public Guid ItemId { get; set; }
    public Guid FromLogicalDocumentId { get; set; }
    public Guid FromVersionId { get; set; }
    public Guid ToLogicalDocumentId { get; set; }
    public Guid ToVersionId { get; set; }
    public string ContentJson { get; set; } = "";
}

public sealed class PublicationRelationshipRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid PublicationId { get; set; }
    public Guid RelationshipVersionId { get; set; }
    public Guid FromLogicalDocumentId { get; set; }
    public Guid FromVersionId { get; set; }
    public Guid ToLogicalDocumentId { get; set; }
    public Guid ToVersionId { get; set; }
}

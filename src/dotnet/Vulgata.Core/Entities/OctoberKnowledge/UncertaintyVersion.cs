namespace Vulgata.Core.Entities.OctoberKnowledge;

public sealed class UncertaintyVersion : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid UncertaintyId { get; set; }
    public Guid ItemId { get; set; }
    public string Key { get; set; } = "";
    public string ContentJson { get; set; } = "";
}

public sealed class PublicationResolutionUncertaintyRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid CorpusId { get; set; }
    public Guid PublicationId { get; set; }
    public Guid UncertaintyId { get; set; }
}

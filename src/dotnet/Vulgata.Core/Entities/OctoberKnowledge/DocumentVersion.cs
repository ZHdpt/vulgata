namespace Vulgata.Core.Entities.OctoberKnowledge;
public sealed class DocumentVersion : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid VersionId { get; set; }
    public Guid LogicalDocumentId { get; set; }
    public Guid ManifestId { get; set; }
    public int SchemaVersion { get; set; }
    public string Title { get; set; } = "";
    public string ContentJson { get; set; } = "";
    public string ContentDigest { get; set; } = "";
    public string AnchorsJson { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class DocumentSectionRecord : IOctoberKnowledgeRecord
{
    public Guid BatchId { get; set; }
    public Guid VersionId { get; set; }
    public string SectionId { get; set; } = "";
    public int Ordinal { get; set; }
    public string Title { get; set; } = "";
}

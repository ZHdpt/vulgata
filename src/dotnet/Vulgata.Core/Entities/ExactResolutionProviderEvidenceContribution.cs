namespace Vulgata.Core.Entities;

public sealed class ExactResolutionProviderEvidenceContribution
{
    private ExactResolutionProviderEvidenceContribution()
    {
    }

    public Guid Id { get; private set; }
    public Guid ResolutionDecisionId { get; private set; }
    public Guid ProviderDocumentVersionId { get; private set; }
    public Guid SourceAnchorId { get; private set; }
    public string EvidenceFingerprint { get; private set; } = string.Empty;
    public int ProviderOrdinal { get; private set; }
    public int EvidenceOrdinal { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

namespace Vulgata.Core.Entities;

public sealed class ExactResolutionProviderEvidenceContribution
{
    private ExactResolutionProviderEvidenceContribution()
    {
    }

    public Guid Id { get; private set; }
    public Guid ResolutionDecisionId { get; private set; }
    public Guid ProviderIdentity { get; private set; }
    public Guid ProviderDocumentVersionId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid ScanRunId { get; private set; }
    public string ProfiledRevision { get; private set; } = string.Empty;
    public Guid CommunicationClassificationId { get; private set; }
    public Guid SourceAnchorId { get; private set; }
    public string ProviderRawIdentifier { get; private set; } = string.Empty;
    public string NormalizedEndpointIdentifier { get; private set; } = string.Empty;
    public string EvidenceLocator { get; private set; } = string.Empty;
    public string ClassificationFingerprint { get; private set; } = string.Empty;
    public string CatalogRuleIdentity { get; private set; } = string.Empty;
    public string NormalizationRuleIdentity { get; private set; } = string.Empty;
    public string EvidenceFingerprint { get; private set; } = string.Empty;
    public int ProviderOrdinal { get; private set; }
    public int EvidenceOrdinal { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

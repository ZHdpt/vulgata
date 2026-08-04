using Vulgata.Core.DomainServices;

namespace Vulgata.Core.Entities;

public sealed class ExactCrossRepositoryEdgeEvidence
{
    private ExactCrossRepositoryEdgeEvidence()
    {
    }

    public ExactCrossRepositoryEdgeEvidence(
        Guid edgeId,
        Guid consumerSourceAnchorId,
        Guid consumerDocumentVersionId,
        Guid consumerRepositoryId,
        Guid consumerScanRunId,
        Guid providerSourceAnchorId,
        Guid providerDocumentVersionId,
        Guid providerRepositoryId,
        Guid providerScanRunId,
        string consumerRawIdentifier,
        string providerRawIdentifier,
        string normalizedEndpointIdentifier,
        ExactResolutionProtocol protocol,
        string resolutionKind,
        decimal confidence,
        string profileSchemaVersion,
        string profileId,
        string profileVersion,
        string profileFingerprint,
        string occurrenceId,
        string consumerEvidenceLocator,
        string providerEvidenceLocator,
        string evidenceFingerprint,
        DateTimeOffset createdAt)
    {
        EdgeId = edgeId;
        ConsumerSourceAnchorId = consumerSourceAnchorId;
        ConsumerDocumentVersionId = consumerDocumentVersionId;
        ConsumerRepositoryId = consumerRepositoryId;
        ConsumerScanRunId = consumerScanRunId;
        ProviderSourceAnchorId = providerSourceAnchorId;
        ProviderDocumentVersionId = providerDocumentVersionId;
        ProviderRepositoryId = providerRepositoryId;
        ProviderScanRunId = providerScanRunId;
        ConsumerRawIdentifier = consumerRawIdentifier;
        ProviderRawIdentifier = providerRawIdentifier;
        NormalizedEndpointIdentifier = normalizedEndpointIdentifier;
        Protocol = protocol;
        ResolutionKind = resolutionKind;
        Confidence = confidence;
        ProfileSchemaVersion = profileSchemaVersion;
        ProfileId = profileId;
        ProfileVersion = profileVersion;
        ProfileFingerprint = profileFingerprint;
        OccurrenceId = occurrenceId;
        ConsumerEvidenceLocator = consumerEvidenceLocator;
        ProviderEvidenceLocator = providerEvidenceLocator;
        EvidenceFingerprint = evidenceFingerprint;
        CreatedAt = createdAt;
    }

    public Guid EdgeId { get; private set; }
    public Guid ConsumerSourceAnchorId { get; private set; }
    public Guid ConsumerDocumentVersionId { get; private set; }
    public Guid ConsumerRepositoryId { get; private set; }
    public Guid ConsumerScanRunId { get; private set; }
    public Guid ProviderSourceAnchorId { get; private set; }
    public Guid ProviderDocumentVersionId { get; private set; }
    public Guid ProviderRepositoryId { get; private set; }
    public Guid ProviderScanRunId { get; private set; }
    public string ConsumerRawIdentifier { get; private set; } = string.Empty;
    public string ProviderRawIdentifier { get; private set; } = string.Empty;
    public string NormalizedEndpointIdentifier { get; private set; } = string.Empty;
    public ExactResolutionProtocol Protocol { get; private set; }
    public string ResolutionKind { get; private set; } = string.Empty;
    public decimal Confidence { get; private set; }
    public string ProfileSchemaVersion { get; private set; } = string.Empty;
    public string ProfileId { get; private set; } = string.Empty;
    public string ProfileVersion { get; private set; } = string.Empty;
    public string ProfileFingerprint { get; private set; } = string.Empty;
    public string OccurrenceId { get; private set; } = string.Empty;
    public string ConsumerEvidenceLocator { get; private set; } = string.Empty;
    public string ProviderEvidenceLocator { get; private set; } = string.Empty;
    public string EvidenceFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DocumentEdge? Edge { get; private set; }
    public SourceAnchor? ConsumerSourceAnchor { get; private set; }
    public SourceAnchor? ProviderSourceAnchor { get; private set; }
}

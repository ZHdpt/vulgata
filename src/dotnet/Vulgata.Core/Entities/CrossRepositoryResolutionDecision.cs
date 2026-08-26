using Vulgata.Core.DomainServices;

namespace Vulgata.Core.Entities;

public sealed class CrossRepositoryResolutionDecision
{
    private CrossRepositoryResolutionDecision()
    {
    }

    public Guid Id { get; private set; }
    public string ResolutionSchemaIdentity { get; private set; } = string.Empty;
    public Guid Pass1PairExecutionId { get; private set; }
    public Guid CoordinationId { get; private set; }
    public Guid SystemId { get; private set; }
    public Guid ConsumerCandidateId { get; private set; }
    public string ConsumerEntryIdentity { get; private set; } = string.Empty;
    public string ConsumerEntryStableKey { get; private set; } = string.Empty;
    public Guid ConsumerDocumentVersionId { get; private set; }
    public Guid ConsumerRepositoryId { get; private set; }
    public Guid ConsumerScanRunId { get; private set; }
    public string ConsumerProfiledRevision { get; private set; } = string.Empty;
    public ExactResolutionProtocol Protocol { get; private set; }
    public string NormalizedEndpointIdentifier { get; private set; } = string.Empty;
    public string NormalizationRuleIdentity { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public string OutcomeKind { get; private set; } = string.Empty;
    public string ReasonCode { get; private set; } = string.Empty;
    public Guid? ProviderIdentity { get; private set; }
    public Guid? ProviderDocumentVersionId { get; private set; }
    public Guid? ProviderRepositoryId { get; private set; }
    public Guid? ProviderScanRunId { get; private set; }
    public string? ProviderProfiledRevision { get; private set; }
    public Guid? DocumentEdgeId { get; private set; }
    public Guid? UncertaintyId { get; private set; }
    public Guid? ResolvedUncertaintyId { get; private set; }
    public string ConsumerEvidenceFingerprint { get; private set; } = string.Empty;
    public string ProviderEvidenceSetFingerprint { get; private set; } = string.Empty;
    public string DecisionFingerprint { get; private set; } = string.Empty;
    public string EvidenceFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}

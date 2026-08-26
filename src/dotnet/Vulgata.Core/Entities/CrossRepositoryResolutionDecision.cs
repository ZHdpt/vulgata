using Vulgata.Core.DomainServices;

namespace Vulgata.Core.Entities;

public sealed class CrossRepositoryResolutionDecision
{
    private CrossRepositoryResolutionDecision()
    {
    }

    public Guid Id { get; private set; }
    public Guid Pass1PairExecutionId { get; private set; }
    public Guid CoordinationId { get; private set; }
    public Guid SystemId { get; private set; }
    public Guid ConsumerCandidateId { get; private set; }
    public string ConsumerEntryIdentity { get; private set; } = string.Empty;
    public string ConsumerEntryStableKey { get; private set; } = string.Empty;
    public Guid ConsumerDocumentVersionId { get; private set; }
    public ExactResolutionProtocol Protocol { get; private set; }
    public string NormalizedEndpointIdentifier { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public Guid? ProviderDocumentVersionId { get; private set; }
    public Guid? DocumentEdgeId { get; private set; }
    public Guid? UncertaintyId { get; private set; }
    public string EvidenceFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}

namespace Vulgata.Core.DomainServices;

public enum ExactResolutionNonExactSource
{
    Consumer,
    Provider,
}

public sealed class ExactResolutionNonExactException : InvalidOperationException
{
    public ExactResolutionNonExactException()
        : base("Eligible exact resolution evidence was non-exact.")
    {
        OccurrenceId = string.Empty;
        ReasonCode = string.Empty;
        EvidenceSource = null;
        Owner = null;
        EvidenceLocator = null;
    }

    public ExactResolutionNonExactException(string message)
        : base(message)
    {
        OccurrenceId = string.Empty;
        ReasonCode = string.Empty;
        EvidenceSource = null;
        Owner = null;
        EvidenceLocator = null;
    }

    public ExactResolutionNonExactException(string message, Exception innerException)
        : base(message, innerException)
    {
        OccurrenceId = string.Empty;
        ReasonCode = string.Empty;
        EvidenceSource = null;
        Owner = null;
        EvidenceLocator = null;
    }

    public ExactResolutionNonExactException(string occurrenceId, string reasonCode)
        : base("Eligible exact resolution evidence was non-exact.")
    {
        OccurrenceId = occurrenceId;
        ReasonCode = reasonCode;
        EvidenceSource = null;
        Owner = null;
        EvidenceLocator = null;
    }

    public ExactResolutionNonExactException(
        string occurrenceId,
        string reasonCode,
        ExactResolutionNonExactSource source,
        ExactResolutionOwner? owner,
        string? evidenceLocator,
        Exception? innerException = null)
        : base("Eligible exact resolution evidence was non-exact.", innerException)
    {
        OccurrenceId = occurrenceId;
        ReasonCode = reasonCode;
        EvidenceSource = source;
        Owner = owner;
        EvidenceLocator = evidenceLocator;
    }

    public string OccurrenceId { get; }
    public string ReasonCode { get; }
    public ExactResolutionNonExactSource? EvidenceSource { get; }
    public ExactResolutionOwner? Owner { get; }
    public string? EvidenceLocator { get; }
}

public interface IExactCrossRepositoryResolver
{
    Task<ExactResolutionDecisionBatch> ResolveAsync(
        ExactResolutionProfile profile,
        IReadOnlyList<ConsumerDispatchCandidate> consumerCandidates,
        IReadOnlyList<ProviderEndpointCandidate> providerCandidates,
        IReadOnlyList<ExactResolutionDecision> unsupportedDecisions,
        CancellationToken cancellationToken = default);
}

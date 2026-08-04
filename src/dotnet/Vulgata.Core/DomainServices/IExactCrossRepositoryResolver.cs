namespace Vulgata.Core.DomainServices;

public interface IExactCrossRepositoryResolver
{
    Task<ExactResolutionDecisionBatch> ResolveAsync(
        ExactResolutionProfile profile,
        IReadOnlyList<ConsumerDispatchCandidate> consumerCandidates,
        IReadOnlyList<ProviderEndpointCandidate> providerCandidates,
        IReadOnlyList<ExactResolutionDecision> unsupportedDecisions,
        CancellationToken cancellationToken = default);
}

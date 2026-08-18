using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface ITwoRepositoryScanCoordinationStore
{
    Task<TwoRepositoryScanCoordinationResult> CreateAsync(
        DurableScanAuthorizationScope authorizationScope,
        TwoRepositoryScanCoordination coordination,
        CancellationToken cancellationToken);

    Task<TwoRepositoryScanCoordinationResult> ReadAsync(
        DurableScanAuthorizationScope authorizationScope,
        Guid coordinationId,
        CancellationToken cancellationToken);

    Task<TwoRepositoryScanCoordinationResult> TransitionAsync(
        DurableScanAuthorizationScope authorizationScope,
        TwoRepositoryScanCoordinationTransitionCommand command,
        CancellationToken cancellationToken);
}

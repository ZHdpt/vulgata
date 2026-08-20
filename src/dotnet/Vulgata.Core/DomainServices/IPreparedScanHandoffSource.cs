namespace Vulgata.Core.DomainServices;

public interface IPreparedScanHandoffSource
{
    Task<PreparedScanHandoff?> GetAsync(
        PreparedScanAuthorizationScope authorizationScope,
        Guid coordinationId,
        CancellationToken cancellationToken = default);
}

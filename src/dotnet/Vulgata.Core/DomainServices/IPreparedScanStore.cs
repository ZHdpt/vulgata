namespace Vulgata.Core.DomainServices;

public interface IPreparedScanStore
{
    Task<PreparedScanCommitResult> CommitPairAsync(
        PreparedScanAuthorizationScope authorizationScope,
        PreparedScanPairPackage pairPackage,
        long expectedCoordinationVersion,
        CancellationToken cancellationToken = default);
}

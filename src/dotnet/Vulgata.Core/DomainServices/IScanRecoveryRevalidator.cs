namespace Vulgata.Core.DomainServices;

public interface IScanRecoveryRevalidator
{
    Task<ScanRecoveryRevalidationResult> RevalidateAsync(
        ScanRecoverySnapshot snapshot,
        DurableScanAuthorizationScope authorizationScope,
        CancellationToken cancellationToken);
}

namespace Vulgata.Core.DomainServices;

public interface IScanRecoveryBindingProbe
{
    Task<ScanRecoveryObservedBindings?> ObserveAsync(
        ScanRecoverySnapshot snapshot,
        DurableScanAuthorizationScope authorizationScope,
        CancellationToken cancellationToken);
}

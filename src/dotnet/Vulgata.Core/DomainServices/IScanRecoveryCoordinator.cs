namespace Vulgata.Core.DomainServices;

public interface IScanRecoveryCoordinator
{
    Task<ScanRecoveryResult> ResumeAsync(
        Guid scanExecutionId,
        DurableScanAuthorizationScope authorizationScope,
        CancellationToken cancellationToken);
}

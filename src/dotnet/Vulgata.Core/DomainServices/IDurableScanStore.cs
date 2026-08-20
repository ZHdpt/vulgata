using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public interface IDurableScanStore
{
    Task<DurableScanStoreResult> CreateContextSnapshotAsync(
        DurableScanAuthorizationScope authorizationScope,
        ScanContextSnapshot snapshot,
        CancellationToken cancellationToken);

    Task<DurableScanStoreResult> CreateScanExecutionAsync(
        DurableScanAuthorizationScope authorizationScope,
        ScanExecution execution,
        CancellationToken cancellationToken);

    Task<DurableScanStoreResult> InsertJobExecutionsAsync(
        DurableScanAuthorizationScope authorizationScope,
        Guid scanExecutionId,
        IReadOnlyList<ScanJobExecution> jobs,
        CancellationToken cancellationToken);

    Task<ScanRecoverySnapshotResult> ReadRecoverySnapshotAsync(
        DurableScanAuthorizationScope authorizationScope,
        Guid scanExecutionId,
        CancellationToken cancellationToken);

    Task<DurableScanStoreResult> ApplyRecoveryTransitionAsync(
        DurableScanAuthorizationScope authorizationScope,
        ScanRecoveryTransitionCommand command,
        CancellationToken cancellationToken);

    Task<DurableScanStoreResult> ApplyRecoveryTransitionsAsync(
        DurableScanAuthorizationScope authorizationScope,
        ScanRecoveryBatchTransitionCommand command,
        CancellationToken cancellationToken);

    Task<DurableScanStoreResult> ClaimPendingJobAsync(
        DurableScanAuthorizationScope authorizationScope,
        ScanJobLeaseCommand command,
        CancellationToken cancellationToken);

    Task<DurableScanStoreResult> TransitionRunningJobAsync(
        DurableScanAuthorizationScope authorizationScope,
        RunningScanJobTransitionCommand command,
        CancellationToken cancellationToken);

    Task<DurableScanProgressResult> ReadProgressAsync(
        DurableScanAuthorizationScope authorizationScope,
        Guid scanExecutionId,
        CancellationToken cancellationToken);
}

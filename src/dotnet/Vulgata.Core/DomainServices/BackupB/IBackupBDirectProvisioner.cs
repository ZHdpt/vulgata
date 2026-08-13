namespace Vulgata.Core.DomainServices.BackupB;

/// <summary>
/// Interface for Backup B direct provisioner that creates pre-scan state
/// without invoking CodeGraphContext.
/// </summary>
public interface IBackupBDirectProvisioner
{
    /// <summary>
    /// Provisions the complete pre-scan state for Backup B demonstration.
    /// Creates System, Repository, ScanRun, CodeUnits, PreScanResult, and
    /// ApprovedPreScanHandoff entities directly from the reviewed manifest.
    /// </summary>
    /// <param name="request">The provisioning request containing system, repositories, and manifest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing execution status and created entity IDs</returns>
    Task<BackupBProvisioningResult> ProvisionAsync(
        BackupBProvisioningRequest request,
        CancellationToken cancellationToken = default);
}

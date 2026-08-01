namespace Vulgata.Core.DomainServices;

/// <summary>
/// Read port for the approved Pre-Scan Handoff consumed by the Sequential
/// Scan Runner. Implemented by Infrastructure via the existing
/// <see cref="IScanRunRepository.GetApprovedPreScanHandoffAsync"/> wrapper.
/// </summary>
public interface IApprovedPreScanHandoffSource
{
    /// <summary>
    /// Load the approved Pre-Scan Handoff for the given Repository and
    /// ScanRun, or <c>null</c> if no approved handoff exists.
    /// </summary>
    Task<ApprovedPreScanHandoff?> GetAsync(
        Guid repositoryId,
        Guid scanRunId,
        CancellationToken cancellationToken = default);
}

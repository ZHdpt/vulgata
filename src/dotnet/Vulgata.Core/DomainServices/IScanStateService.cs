namespace Vulgata.Core.DomainServices;

/// <summary>
/// Determines whether a scan is currently running for a given scope.
/// Uses a no-op default implementation until Epic 5 provides the real scan engine.
/// </summary>
public interface IScanStateService
{
    /// <summary>
    /// Returns true if any repository within the global scope has an active scan.
    /// </summary>
    Task<bool> IsAnyScanRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if any repository belonging to the specified system has an active scan.
    /// </summary>
    Task<bool> IsSystemScanRunningAsync(Guid systemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the specified repository has an active scan.
    /// </summary>
    Task<bool> IsRepositoryScanRunningAsync(Guid repositoryId, CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op implementation: always reports no scans running.
/// Replace with real implementation in Epic 5.
/// </summary>
public sealed class NoOpScanStateService : IScanStateService
{
    public Task<bool> IsAnyScanRunningAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> IsSystemScanRunningAsync(Guid systemId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> IsRepositoryScanRunningAsync(Guid repositoryId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

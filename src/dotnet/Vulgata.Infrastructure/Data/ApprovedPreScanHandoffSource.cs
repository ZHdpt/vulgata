using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Data;

/// <summary>
/// Infrastructure adapter that implements
/// <see cref="IApprovedPreScanHandoffSource"/> by wrapping the existing
/// <see cref="IScanRunRepository.GetApprovedPreScanHandoffAsync"/> default
/// method. Story 11.2 only reads the handoff; no new persistence is added.
/// </summary>
public sealed class ApprovedPreScanHandoffSource(IScanRunRepository scanRunRepository)
    : IApprovedPreScanHandoffSource
{
    public Task<ApprovedPreScanHandoff?> GetAsync(
        Guid repositoryId,
        Guid scanRunId,
        CancellationToken cancellationToken = default) =>
        scanRunRepository.GetApprovedPreScanHandoffAsync(
            repositoryId, scanRunId, cancellationToken);
}

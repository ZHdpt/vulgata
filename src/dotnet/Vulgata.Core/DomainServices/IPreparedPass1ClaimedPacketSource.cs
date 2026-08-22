namespace Vulgata.Core.DomainServices;

/// <summary>
/// Reconstructs one provider-visible packet from a server-issued durable claim.
/// Lease tokens and state versions remain inside this authorization boundary.
/// </summary>
public interface IPreparedPass1ClaimedPacketSource
{
    Task<PreparedPass1WorkerPacket?> HydrateAsync(
        Guid pairExecutionId,
        PreparedPass1ClaimResult claim,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);
}

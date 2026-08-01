namespace Vulgata.Core.DomainServices;

/// <summary>
/// The port the Sequential Scan Runner dispatches typed Worker Agent jobs
/// against. Story 11.2 defines the port and ships only a fake/test dispatcher
/// in Tests; Story 11.3 supplies the LLM-backed implementation. The runner
/// depends on the port, never on a concrete LLM client.
/// </summary>
public interface IWorkerAgentDispatcher
{
    /// <summary>
    /// Dispatch one bounded <see cref="WorkerAgentJobPacket"/> and return a
    /// schema-validated <see cref="WorkerAgentResultEnvelope"/>. The runner
    /// calls this strictly one at a time; no concurrency, lease, replay,
    /// failover, or retry is introduced by the port.
    /// </summary>
    Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatch one bounded job under an explicit Repository authorization
    /// scope. Implementations that retain only the Story 11.2 compatibility
    /// method may proceed only after this default guard proves the packet's
    /// Repository is in scope.
    /// </summary>
    Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(scope);
        return !scope.Contains(packet.RepositoryId)
            ? Task.FromException<WorkerAgentResultEnvelope>(
                new UnauthorizedAccessException(
                    "The Worker Agent Repository is outside the authorized scope."))
            : DispatchAsync(packet, cancellationToken);
    }
}

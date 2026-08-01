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
}

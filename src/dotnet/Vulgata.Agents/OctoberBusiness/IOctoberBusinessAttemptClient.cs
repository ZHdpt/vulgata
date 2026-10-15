using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.OctoberBusiness;

public enum BusinessSynthesisAttemptStrategy { Configured, ThinkingDisabled, CompactedThinkingDisabled }

/// <summary>Request-local synthesis finalization settings. This port never grants another attempt.</summary>
public interface IOctoberBusinessSynthesisClient : IOctoberBusinessAttemptClient
{
    bool SupportsSynthesisCorrections { get; }
    Task<ChatResponse> GetSynthesisResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options,
        IOctoberProviderAttemptLease lease, BusinessSynthesisAttemptStrategy strategy,
        Action<long> onDispatched, CancellationToken cancellationToken = default);
}

/// <summary>
/// Selected nonstreaming transport for one already-admitted Business attempt. SDK types stay in
/// Agents. The host binds this client to the exact selected profile; unbound calls are forbidden.
/// </summary>
public interface IOctoberBusinessAttemptClient
{
    string ProviderProfileId { get; }

    /// <summary>
    /// Receives the SAME existing leaf lease; never acquires another permit or uses ambient lease
    /// lookup. The transport links caller/lease cancellation, performs the final lease eligibility
    /// check immediately before HTTP dispatch and disables SDK retries. At that boundary it invokes
    /// the required callback with a Stopwatch timestamp and records dispatch on the lease's existing
    /// IOctoberWorkerDispatchObserver. Eligibility/rate reservation alone is not a physical send.
    /// The returned task retains actual HTTP/body/SDK cleanup until terminal settlement, including
    /// ignored cancellation; it must not be a timeout facade over surviving physical work.
    /// The Business caller then completes the SAME observer before SettleAsync/DisposeAsync. Only
    /// successful terminal observation with no dispatch callback proves unsent/zero usage; a sent
    /// failure or missing response usage remains unknown. Failed observation must never fund a refund.
    /// The callback is non-null and must not throw. This method does not settle or dispose the lease.
    /// Host-admitted Business retries and the original execution budget remain outside this method.
    /// </summary>
    Task<ChatResponse> GetAttemptResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options,
        IOctoberProviderAttemptLease lease,
        Action<long> onDispatched,
        CancellationToken cancellationToken = default);
}

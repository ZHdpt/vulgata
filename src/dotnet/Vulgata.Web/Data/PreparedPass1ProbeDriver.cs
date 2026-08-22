using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Web.Data;

/// <summary>
/// Drives one explicitly activated durable provider-probe ticket. It is not a
/// hosted service and performs no startup discovery or polling.
/// </summary>
public sealed class PreparedPass1ProbeDriver(
    IPreparedPass1ProviderTransitionStore transitionStore,
    IPreparedPass1ProviderHealthProbe healthProbe,
    TimeProvider timeProvider)
{
    public async Task<PreparedPass1ProviderCircuitResult> ProbeAsync(
        Guid pairExecutionId,
        long expectedStateVersion,
        int probeOrdinal,
        DateTimeOffset nextProbeAt,
        Uri endpoint,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        int effectiveOrdinal = probeOrdinal == 0 ? 1 : probeOrdinal;
        if (effectiveOrdinal is < 1 or > 5)
        {
            return Rejected(PreparedPass1ProviderCircuitOutcome.Ineligible, effectiveOrdinal);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (nextProbeAt > now)
        {
            await Task.Delay(nextProbeAt - now, timeProvider, cancellationToken);
        }

        PreparedPass1ProviderHealthResult? observation = await healthProbe.ProbeAsync(
            endpoint,
            model,
            apiKey,
            cancellationToken);
        DateTimeOffset observedAt = timeProvider.GetUtcNow();
        PreparedPass1ProviderHealthResult durableObservation = observation ?? new(
            PreparedPass1ProviderHealthOutcome.ProviderUnavailable,
            null);
        return await transitionStore.ProbeProviderAsync(
            pairExecutionId,
            expectedStateVersion,
            effectiveOrdinal,
            durableObservation,
            observedAt,
            cancellationToken);
    }

    private static PreparedPass1ProviderCircuitResult Rejected(
        PreparedPass1ProviderCircuitOutcome outcome,
        int ordinal) => new(
            outcome,
            Pass1CircuitState.Probing,
            Pass1PairExecutionPhase.ProbingProvider,
            ordinal,
            null,
            null,
            "provider-unavailable",
            string.Empty,
            0,
            [],
            false);
}

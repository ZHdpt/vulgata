namespace Vulgata.Core.DomainServices;

public enum PreparedPass1ProviderHealthOutcome
{
    Available,
    ProviderConfiguration,
    ProviderUnavailable,
    UnclassifiedSystemic,
}

/// <summary>
/// Sanitized observation from one bounded, source-free provider request.
/// </summary>
public sealed record PreparedPass1ProviderHealthResult(
    PreparedPass1ProviderHealthOutcome Outcome,
    DateTimeOffset? RetryAfterAt)
{
    public bool IsAvailable => Outcome == PreparedPass1ProviderHealthOutcome.Available;
}

/// <summary>
/// Performs one active server-side provider health request. The operation accepts
/// provider connection material only; no Repository or Scan evidence crosses it.
/// </summary>
public interface IPreparedPass1ProviderHealthProbe
{
    Task<PreparedPass1ProviderHealthResult> ProbeAsync(
        Uri endpoint,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies one sanitized active-probe observation to its exact durable,
/// versioned pair ticket.
/// </summary>
public interface IPreparedPass1ProviderTransitionStore
{
    Task<PreparedPass1ProviderCircuitResult> ProbeProviderAsync(
        Guid pairExecutionId,
        long expectedStateVersion,
        int expectedProbeOrdinal,
        PreparedPass1ProviderHealthResult observation,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);
}

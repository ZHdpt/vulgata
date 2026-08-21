using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public enum PreparedPass1StartOutcome
{
    Created,
    Replay,
    Conflict,
    Rejected,
    NotFound,
    Cancelled,
    Failed,
}

public sealed record PreparedPass1StartResult(
    PreparedPass1StartOutcome Outcome,
    Guid? PairExecutionId,
    int? ConcurrencyLimit);

public enum PreparedPass1ClaimOutcome
{
    Claimed,
    AtCapacity,
    Exhausted,
    Ineligible,
    NotFound,
    Rejected,
    Cancelled,
    Failed,
}

public sealed record PreparedPass1ClaimResult(
    PreparedPass1ClaimOutcome Outcome,
    Guid JobId,
    Guid LeaseToken,
    DateTimeOffset LeaseExpiresAt,
    int PairSelectionOrdinal,
    int ManifestOrdinal,
    int ActiveLeaseCount,
    int ConcurrencyLimit);

public enum PreparedPass1ConcurrencyUpdateOutcome
{
    Updated,
    NoChange,
    Replay,
    Conflict,
    Ineligible,
    NotFound,
    Rejected,
    Cancelled,
    Failed,
}

public sealed record PreparedPass1ConcurrencyUpdateResult(
    PreparedPass1ConcurrencyUpdateOutcome Outcome,
    int? ConcurrencyLimit,
    long? StateVersion,
    long? ConcurrencyRevision,
    int PreservedLeaseCount);

public interface IPreparedPass1Store
{
    Task<PreparedPass1StartResult> StartAsync(
        PreparedScanAuthorizationScope authorizationScope,
        PreparedScanHandoff handoff,
        int concurrencyLimit = 2,
        CancellationToken cancellationToken = default);

    Task<PreparedPass1ClaimResult> ClaimNextAsync(
        Guid pairExecutionId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<PreparedPass1ConcurrencyUpdateResult> UpdateConcurrencyAsync(
        PreparedScanAuthorizationScope authorizationScope,
        Guid pairExecutionId,
        int requestedLimit,
        long expectedStateVersion,
        string principalId,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default);

    Task<PreparedPass1RecoveryResult> RecoverAsync(
        Guid pairExecutionId,
        PreparedScanAuthorizationScope authorizationScope,
        bool providerAvailable,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);

    Task<PreparedPass1FailureResult> RecordFailureAsync(
        Guid pairExecutionId,
        Guid jobId,
        long expectedStateVersion,
        Guid expectedLeaseToken,
        ScanJobFailureCategory failureCategory,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);

    Task<PreparedPass1ProviderCircuitResult> RecordProviderTransientAsync(
        Guid pairExecutionId,
        DateTimeOffset observedAt,
        DateTimeOffset? retryAfterAt = null,
        CancellationToken cancellationToken = default);

    Task<PreparedPass1ProviderCircuitResult> ProbeProviderAsync(
        Guid pairExecutionId,
        long expectedStateVersion,
        int expectedProbeOrdinal,
        bool providerAvailable,
        DateTimeOffset observedAt,
        DateTimeOffset? retryAfterAt = null,
        CancellationToken cancellationToken = default);
}

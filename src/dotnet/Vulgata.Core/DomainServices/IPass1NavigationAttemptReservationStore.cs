namespace Vulgata.Core.DomainServices;

/// <summary>
/// Durable cursor returned after reserving or recording one navigation attempt.
/// </summary>
public sealed record PreparedPass1NavigationBudgetCursor(
    int ToolAttemptOrdinal,
    int ToolAttemptCount,
    long StateVersion);

/// <summary>
/// Server-only authorization and budget cursor for one reserved navigation attempt.
/// </summary>
public sealed record PreparedPass1NavigationReservationTicket(
    Guid JobId,
    Guid PairExecutionId,
    Guid LeaseToken,
    int ToolAttemptOrdinal,
    int ToolAttemptCount,
    long StateVersion,
    Guid? ReceiptId = null);

/// <summary>
/// Server-owned inputs required to reserve the next durable navigation attempt.
/// </summary>
public sealed record PreparedPass1NavigationReservationRequest(
    PreparedPass1NavigationScope Scope,
    PreparedPass1NavigationRequestInput Request,
    PreparedPass1NavigationReservationTicket? Cursor = null);

public interface IPass1NavigationAttemptReservationStore
{
    Task<PreparedPass1NavigationReservationTicket?> ReserveNextAsync(
        PreparedPass1NavigationReservationRequest request,
        CancellationToken cancellationToken);
}

public sealed record PreparedPass1NavigationCallsMarkerRepairRequest(
    Guid PairExecutionId,
    Guid JobId,
    long JobStateVersion,
    long PairStateVersion,
    Guid LeaseToken,
    DateTimeOffset LeaseExpiresAt,
    string AuthorizationBinding);

public interface IPass1NavigationCallsMarkerRepairStore
{
    Task<bool> EnsureNavigationCallsMarkerAsync(
        PreparedPass1NavigationCallsMarkerRepairRequest request,
        CancellationToken cancellationToken);
}

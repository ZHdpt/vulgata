namespace Vulgata.Core.DomainServices;

public interface IPreparedPass2WorkerDispatcher
{
    Task<PreparedPass2DispatchResult> DispatchAsync(
        PreparedPass2DispatchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PreparedPass2DispatchRequest(
    PreparedPass2Flow Flow,
    PreparedPass2RuntimeBinding RuntimeBinding);

public sealed record PreparedPass2DispatchResult(
    bool Accepted,
    string? ReasonCode);

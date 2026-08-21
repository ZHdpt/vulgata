using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Data;

/// <summary>
/// Host composition seam for one already-claimed prepared dispatch batch.
/// Claim persistence remains owned by IPreparedPass1Store; this worker neither
/// enumerates files nor creates jobs.
/// </summary>
public sealed class PreparedPass1DispatchWorker(IPreparedPass1WorkerDispatcher dispatcher)
{
    public Task<PreparedPass1DispatchResult> ExecuteAsync(
        PreparedPass1DispatchRequest request,
        CancellationToken cancellationToken = default) =>
        dispatcher.DispatchAsync(request, cancellationToken);
}

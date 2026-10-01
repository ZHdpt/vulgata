using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Data;

internal sealed class OctoberSelectedDataBoundSetup
{
    private IOctoberBoundSetupData? _bound;

    internal void SetOnce(IOctoberBoundSetupData bound)
    {
        ArgumentNullException.ThrowIfNull(bound);
        if (Interlocked.CompareExchange(ref _bound, bound, null) is not null)
            throw new InvalidOperationException("October selected-data batch is already bound.");
    }

    internal IOctoberBoundSetupData GetBound() =>
        Volatile.Read(ref _bound) ??
        throw new InvalidOperationException("October selected-data batch is not bound.");
}

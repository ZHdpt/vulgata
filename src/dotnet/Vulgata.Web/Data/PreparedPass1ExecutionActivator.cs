using System.Collections.Concurrent;
using System.Threading.Channels;
using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Data;

internal sealed record PreparedPass1Activation(
    Guid PairExecutionId,
    PreparedScanAuthorizationScope AuthorizationScope,
    PreparedScanHandoff? Handoff);

/// <summary>
/// Process-local wake-up projection. Durable claims remain the sole work authority.
/// </summary>
public sealed class PreparedPass1ExecutionActivator
{
    private readonly ConcurrentDictionary<Guid, ActivationSlot> _slots = new();
    private readonly Channel<Guid> _activatedPairs = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    internal void Signal(
        Guid pairExecutionId,
        PreparedScanAuthorizationScope authorizationScope,
        PreparedScanHandoff? handoff = null)
    {
        if (pairExecutionId == Guid.Empty)
        {
            return;
        }

        ActivationSlot slot = _slots.AddOrUpdate(
            pairExecutionId,
            _ => new ActivationSlot(new(pairExecutionId, authorizationScope, handoff)),
            (_, current) => current.Update(authorizationScope, handoff));
        slot.Wake();
        _activatedPairs.Writer.TryWrite(pairExecutionId);
    }

    internal IAsyncEnumerable<Guid> ReadActivatedPairsAsync(CancellationToken cancellationToken) =>
        _activatedPairs.Reader.ReadAllAsync(cancellationToken);

    internal ActivationSlot? GetSlot(Guid pairExecutionId) =>
        _slots.TryGetValue(pairExecutionId, out ActivationSlot? slot) ? slot : null;

    internal sealed class ActivationSlot
    {
        private readonly Channel<bool> _wakeups = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });
        private PreparedPass1Activation _activation;

        internal ActivationSlot(PreparedPass1Activation activation) => _activation = activation;

        internal PreparedPass1Activation Current => Volatile.Read(ref _activation);

        internal ActivationSlot Update(
            PreparedScanAuthorizationScope authorizationScope,
            PreparedScanHandoff? handoff)
        {
            PreparedPass1Activation current = Current;
            Volatile.Write(
                ref _activation,
                new(current.PairExecutionId, authorizationScope, handoff ?? current.Handoff));
            return this;
        }

        internal void Wake() => _wakeups.Writer.TryWrite(true);

        internal async Task WaitAsync(CancellationToken cancellationToken)
        {
            _ = await _wakeups.Reader.ReadAsync(cancellationToken);
        }
    }
}

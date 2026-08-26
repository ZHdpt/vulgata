using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Vulgata.Web.Data;

/// <summary>
/// Process-local wake-up projection. PostgreSQL claims remain the only work authority.
/// </summary>
public sealed class PreparedPass2ExecutionActivator
{
    private readonly ConcurrentDictionary<Guid, ActivationSlot> _slots = new();
    private readonly Channel<Guid> _activations = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public void Signal(Guid pass2ExecutionId)
    {
        if (pass2ExecutionId == Guid.Empty)
        {
            return;
        }

        ActivationSlot slot = _slots.GetOrAdd(pass2ExecutionId, static id => new(id));
        slot.Wake();
        _activations.Writer.TryWrite(pass2ExecutionId);
    }

    internal IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _activations.Reader.ReadAllAsync(cancellationToken);

    internal ActivationSlot? GetSlot(Guid pass2ExecutionId) =>
        _slots.TryGetValue(pass2ExecutionId, out ActivationSlot? slot) ? slot : null;

    internal sealed class ActivationSlot(Guid pass2ExecutionId)
    {
        private readonly Channel<bool> _wakeups = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });

        internal Guid Pass2ExecutionId { get; } = pass2ExecutionId;
        internal void Wake() => _wakeups.Writer.TryWrite(true);
        internal async Task WaitAsync(CancellationToken cancellationToken) =>
            _ = await _wakeups.Reader.ReadAsync(cancellationToken);
    }
}

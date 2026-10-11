using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Core.DomainServices;

namespace Vulgata.OctoberGenerator;

/// <summary>One exact prepared descriptor for one Repository in the original source run.</summary>
internal sealed class OctoberCodeGraphOwner(Guid repositoryId, string rootBindingIdentity,
    Func<TimeSpan> remainingRun) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposalGate = new();
    private PreparedCodeGraphConnection? _connection;
    private Task? _disposal;
    private volatile bool _closed;
    private bool _faulted;
    internal Guid RepositoryId { get; } = repositoryId;
    internal string RootBindingIdentity { get; } = rootBindingIdentity;

    internal async Task<PreparedCodeGraphConnection> GetOrCreateAsync(
        Func<TimeSpan, CancellationToken, Task<PreparedCodeGraphConnection>> prepare,
        CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            TimeSpan remaining = remainingRun();
            if (_closed || _faulted || remaining <= TimeSpan.Zero || remaining > ExecutionOwnerLifetime.Maximum)
                throw new InvalidOperationException("The original CodeGraph owner is unavailable.");
            if (_connection is not null) return _connection;
            try
            {
                // Cache failure: preparation may have created state before failing.
                // Never retry creation or adopt a path after an uncertain preparation.
                _connection = await prepare(remaining, token);
                token.ThrowIfCancellationRequested();
                if (_closed || remainingRun() <= TimeSpan.Zero || _connection.RepositoryId != RepositoryId)
                    throw new InvalidOperationException("The prepared CodeGraph owner binding is unavailable.");
                return _connection;
            }
            catch { _faulted = true; throw; }
        }
        finally { _gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposalGate)
        {
            _closed = true;
            return new(_disposal ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync();
        try { if (_connection is not null) await _connection.DisposeAsync(); }
        finally { _gate.Release(); }
    }
}

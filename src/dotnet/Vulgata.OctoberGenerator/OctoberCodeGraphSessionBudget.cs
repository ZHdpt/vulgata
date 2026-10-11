namespace Vulgata.OctoberGenerator;

/// <summary>One repository's native session allowance across approval-bound runtimes.</summary>
internal sealed class OctoberCodeGraphSessionBudget(Guid repositoryId)
{
    private readonly Lock _sync = new();
    private int _used;
    internal Guid RepositoryId { get; } = repositoryId;
    internal int Used { get { lock (_sync) return _used; } }
    internal bool TryReserve()
    {
        lock (_sync)
        {
            if (_used >= 21) return false;
            _used++;
            return true;
        }
    }
}

internal enum OctoberSourceWindowFailure { Expired, AuthorityUnavailable, BindingMismatch, CleanupUnconfirmed, InsufficientLifetime, Closed }

internal sealed class OctoberSourceWindowException(OctoberSourceWindowFailure reason)
    : InvalidOperationException("The source authority window is unavailable.")
{
    internal OctoberSourceWindowFailure Reason { get; } = reason;
}

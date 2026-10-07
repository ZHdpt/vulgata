namespace Vulgata.Web.Services.OctoberChat;

/// <summary>Host-shared physical Chat request capacity; independent of Scan execution scopes.</summary>
public sealed class OctoberChatRequestAdmission
{
    private readonly SemaphoreSlim _permits;
    private int _activeRequests;

    public OctoberChatRequestAdmission(int maximumConcurrentRequests = 4)
    {
        if (maximumConcurrentRequests <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConcurrentRequests));
        _permits = new(maximumConcurrentRequests, maximumConcurrentRequests);
    }

    internal int ActiveRequests => Volatile.Read(ref _activeRequests);

    internal async Task AcquireAsync(CancellationToken cancellationToken)
    {
        await _permits.WaitAsync(cancellationToken);
        Interlocked.Increment(ref _activeRequests);
    }

    internal void Release()
    {
        Interlocked.Decrement(ref _activeRequests);
        _permits.Release();
    }
}

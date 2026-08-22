using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Git;

public sealed class PreparedPass1RevisionBindingRegistry(
    IServiceScopeFactory scopeFactory) :
    IPreparedPass1RevisionBindingRegistry,
    IPreparedRevisionLeaseResolver,
    IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, PairEntry> _pairs = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public async Task<PreparedPass1PairRevisionBindings?> AcquirePairAsync(
        Guid pairExecutionId,
        IReadOnlyList<PreparedRevisionRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (pairExecutionId == Guid.Empty || requests is null || requests.Count != 2 ||
            requests.Any(request => request is null) ||
            requests.Select(request => request.RepositoryId).Distinct().Count() != 2)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pairs.TryGetValue(pairExecutionId, out PairEntry? current))
            {
                return current.Matches(requests) ? current.Projection : null;
            }

            AsyncServiceScope retainedScope = scopeFactory.CreateAsyncScope();
            try
            {
                IPreparedRevisionAcquirer acquirer = retainedScope.ServiceProvider
                    .GetRequiredService<IPreparedRevisionAcquirer>();
                var acquired = new List<AcquiredBinding>(2);
                foreach (PreparedRevisionRequest request in requests
                             .OrderBy(value => value.RepositoryId))
                {
                    PreparedRevisionAcquisitionResult result = await acquirer.AcquireAsync(
                        request,
                        cancellationToken).ConfigureAwait(false);
                    PreparedRevisionSnapshot? snapshot = result.Snapshot;
                    if (snapshot is null || result.Failure is not null ||
                        snapshot.RepositoryId != request.RepositoryId ||
                        snapshot.ScanRunId != request.ScanRunId ||
                        !string.Equals(snapshot.RequestedRef, request.RequestedRef, StringComparison.Ordinal) ||
                        !string.Equals(snapshot.ProfiledRevision, request.ProfiledRevision, StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(snapshot.ContainedRootToken))
                    {
                        await retainedScope.DisposeAsync().ConfigureAwait(false);
                        return null;
                    }

                    string alias = PreparedPass1RevisionBindingToken.Create(
                        pairExecutionId,
                        request.RepositoryId,
                        request.ScanRunId,
                        request.ProfiledRevision);
                    acquired.Add(new(
                        new PreparedPass1RevisionBinding(
                            pairExecutionId,
                            request.RepositoryId,
                            request.ScanRunId,
                            request.ProfiledRevision,
                            alias),
                        snapshot.ContainedRootToken,
                        request.RepositoryOrigin,
                        request.RequestedRef));
                }

                PreparedPass1RevisionBinding[] bindings =
                    [.. acquired.Select(value => value.Binding).OrderBy(value => value.RepositoryId)];
                var entry = new PairEntry(
                    retainedScope,
                    new PreparedPass1PairRevisionBindings(pairExecutionId, bindings),
                    acquired);
                if (!_pairs.TryAdd(pairExecutionId, entry))
                {
                    await retainedScope.DisposeAsync().ConfigureAwait(false);
                    return _pairs.TryGetValue(pairExecutionId, out PairEntry? winner) &&
                        winner.Matches(requests)
                        ? winner.Projection
                        : null;
                }

                return entry.Projection;
            }
            catch
            {
                await retainedScope.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public PreparedPass1RevisionBinding? ResolveBinding(
        Guid pairExecutionId,
        Guid repositoryId) =>
        _pairs.TryGetValue(pairExecutionId, out PairEntry? entry)
            ? entry.Projection.Bindings.SingleOrDefault(value => value.RepositoryId == repositoryId)
            : null;

    public bool TryResolveLease(
        string bindingToken,
        out ICommittedRepositorySnapshotLease? lease)
    {
        lease = null;
        if (string.IsNullOrWhiteSpace(bindingToken))
        {
            return false;
        }

        foreach (PairEntry entry in _pairs.Values)
        {
            AcquiredBinding? binding = entry.Acquired.SingleOrDefault(value =>
                string.Equals(value.Binding.BindingToken, bindingToken, StringComparison.Ordinal));
            if (binding is null)
            {
                continue;
            }

            if (entry.Scope.ServiceProvider.GetService<PreparedScanRevisionAcquirer>() is
                    PreparedScanRevisionAcquirer resolver &&
                resolver.TryResolveLease(binding.ContainedRootToken, out lease))
            {
                return true;
            }

            return false;
        }

        return false;
    }

    public async ValueTask ReleasePairAsync(Guid pairExecutionId)
    {
        PairEntry? entry = null;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (pairExecutionId != Guid.Empty)
            {
                _pairs.TryRemove(pairExecutionId, out entry);
            }
        }
        finally
        {
            _gate.Release();
        }

        if (entry is not null)
        {
            await entry.Scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        PairEntry[] entries;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            entries = [.. _pairs.Values];
            _pairs.Clear();
        }
        finally
        {
            _gate.Release();
        }

        foreach (PairEntry entry in entries)
        {
            await entry.Scope.DisposeAsync().ConfigureAwait(false);
        }

    }

    private sealed record AcquiredBinding(
        PreparedPass1RevisionBinding Binding,
        string ContainedRootToken,
        string RepositoryOrigin,
        string RequestedRef);

    private sealed record PairEntry(
        AsyncServiceScope Scope,
        PreparedPass1PairRevisionBindings Projection,
        IReadOnlyList<AcquiredBinding> Acquired)
    {
        internal bool Matches(IReadOnlyList<PreparedRevisionRequest> requests) =>
            requests.Count == Projection.Bindings.Count &&
            requests.All(request => Acquired.Any(acquired =>
                acquired.Binding.RepositoryId == request.RepositoryId &&
                acquired.Binding.ScanRunId == request.ScanRunId &&
                string.Equals(acquired.RepositoryOrigin, request.RepositoryOrigin, StringComparison.Ordinal) &&
                string.Equals(acquired.RequestedRef, request.RequestedRef, StringComparison.Ordinal) &&
                string.Equals(
                    acquired.Binding.ProfiledRevision,
                    request.ProfiledRevision,
                    StringComparison.Ordinal)));
    }
}

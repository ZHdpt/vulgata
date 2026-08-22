using Vulgata.Core.DomainServices;
using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace Vulgata.Infrastructure.Git;

/// <summary>
/// Narrows exact Git acquisition to the values required by prepared scanning.
/// Ordinary working-copy and arbitrary revision inputs are intentionally absent.
/// </summary>
public sealed class PreparedScanRevisionAcquirer :
    IPreparedRevisionAcquirer,
    IPreparedRevisionLeaseResolver,
    IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ICommittedRepositorySnapshotLease> _leases =
        new(StringComparer.Ordinal);
    private readonly ExactGitRevisionSnapshotService _snapshots;

    public PreparedScanRevisionAcquirer(ExactGitRevisionSnapshotService snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        _snapshots = snapshots;
    }

    public Task<ICommittedRepositorySnapshotLease> AcquireAsync(
        Guid repositoryId,
        Guid scanRunId,
        string repositoryOrigin,
        string requestedRef,
        string expectedCommit,
        CancellationToken cancellationToken = default)
    {
        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException("Repository identity cannot be empty.", nameof(repositoryId));
        }

        if (scanRunId == Guid.Empty)
        {
            throw new ArgumentException("Scan Run identity cannot be empty.", nameof(scanRunId));
        }

        return _snapshots.AcquireAsync(
            repositoryId,
            repositoryOrigin,
            scanRunId,
            requestedRef,
            expectedCommit,
            cancellationToken);
    }

    async Task<PreparedRevisionAcquisitionResult> IPreparedRevisionAcquirer.AcquireAsync(
        PreparedRevisionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ICommittedRepositorySnapshotLease lease = await AcquireAsync(
                request.RepositoryId,
                request.ScanRunId,
                request.RepositoryOrigin,
                request.RequestedRef,
                request.ProfiledRevision,
                cancellationToken);
            await lease.VerifyIdentityAsync(cancellationToken);
            string identity = await PreparedSnapshotIdentity.ComputeAsync(
                lease.RootPath,
                cancellationToken);
            string token = Guid.NewGuid().ToString("N");
            if (!_leases.TryAdd(token, lease))
            {
                await lease.DisposeAsync();
                throw new InvalidOperationException("The prepared snapshot lease token collided.");
            }

            return new(
                new PreparedRevisionSnapshot(
                    request.RepositoryId,
                    request.ScanRunId,
                    request.RequestedRef,
                    request.ProfiledRevision,
                    identity,
                    token,
                    token),
                Failure: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException or
                UnauthorizedAccessException)
        {
            return new(
                Snapshot: null,
                new PreparedScanFailure(
                    PreparedScanFailureReason.SourceIdentityMismatch,
                    "prepared-revision-acquisition-failed",
                    "The exact Profiled Revision could not be acquired and verified."));
        }
    }

    internal ICommittedRepositorySnapshotLease RequireLease(string containedRootToken) =>
        _leases.TryGetValue(containedRootToken, out ICommittedRepositorySnapshotLease? lease)
            ? lease
            : throw new InvalidOperationException("The prepared snapshot lease is unavailable.");

    public bool TryResolveLease(
        string bindingToken,
        out ICommittedRepositorySnapshotLease? lease) =>
        _leases.TryGetValue(bindingToken, out lease);

    public async ValueTask DisposeAsync()
    {
        foreach (KeyValuePair<string, ICommittedRepositorySnapshotLease> pair in _leases.ToArray())
        {
            if (_leases.TryRemove(pair.Key, out ICommittedRepositorySnapshotLease? lease))
            {
                await lease.DisposeAsync();
            }
        }
    }

}

internal static class PreparedSnapshotIdentity
{
    internal static async Task<string> ComputeAsync(
        string snapshotRoot,
        CancellationToken cancellationToken)
    {
        string root = Path.GetFullPath(snapshotRoot);
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            byte[] pathBytes = System.Text.Encoding.UTF8.GetBytes(relative);
            byte[] length = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(length, pathBytes.Length);
            hash.AppendData(length);
            hash.AppendData(pathBytes);
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) != 0)
            {
                hash.AppendData(buffer.AsSpan(0, read));
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}

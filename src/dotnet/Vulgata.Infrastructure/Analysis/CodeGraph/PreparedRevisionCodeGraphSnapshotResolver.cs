using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Git;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

internal sealed class PreparedRevisionCodeGraphSnapshotResolver :
    ICodeGraphRepositorySnapshotResolver,
    IAsyncDisposable
{
    private readonly PreparedScanRevisionAcquirer _revisionAcquirer;
    private readonly ConcurrentDictionary<string, AnalysisWorkspace> _workspaces =
        new(StringComparer.Ordinal);
    private readonly string _workspaceRoot = Path.Combine(
        Path.GetTempPath(),
        "vulgata-codegraph",
        Guid.NewGuid().ToString("N"));

    public PreparedRevisionCodeGraphSnapshotResolver(
        PreparedScanRevisionAcquirer revisionAcquirer)
    {
        _revisionAcquirer = revisionAcquirer ??
            throw new ArgumentNullException(nameof(revisionAcquirer));
    }

    public async Task<CodeGraphRepositorySnapshot?> ResolveAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        ICommittedRepositorySnapshotLease lease = _revisionAcquirer.RequireLease(
            scope.WorkingCopyBindingToken);
        RequireLeaseBinding(lease, scope);
        await lease.VerifyIdentityAsync(cancellationToken).ConfigureAwait(false);

        AnalysisWorkspace workspace;
        if (!_workspaces.TryGetValue(scope.WorkingCopyBindingToken, out workspace!))
        {
            workspace = await CreateWorkspaceAsync(lease, scope, cancellationToken)
                .ConfigureAwait(false);
            if (!_workspaces.TryAdd(scope.WorkingCopyBindingToken, workspace))
            {
                DeleteDirectory(workspace.RootPath);
                workspace = _workspaces[scope.WorkingCopyBindingToken];
            }
        }

        string currentIdentity = await ComputeSourceIdentityAsync(
            workspace.RootPath,
            cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(workspace.SourceIdentity),
                Convert.FromHexString(currentIdentity)))
        {
            throw new InvalidOperationException("codegraph_analysis_source_identity_mismatch");
        }

        return new CodeGraphRepositorySnapshot(
            scope.RepositoryId,
            scope.ScanRunId,
            scope.ProfiledRevision,
            scope.WorkingCopyBindingToken,
            workspace.RootPath,
            $"index-{scope.RepositoryId:N}",
            scope.RuntimeIdentityHash);
    }

    public ValueTask DisposeAsync()
    {
        DeleteDirectory(_workspaceRoot);
        return ValueTask.CompletedTask;
    }

    private async Task<AnalysisWorkspace> CreateWorkspaceAsync(
        ICommittedRepositorySnapshotLease lease,
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        string root = Path.Combine(
            _workspaceRoot,
            scope.RepositoryId.ToString("N"),
            scope.ScanRunId.ToString("N"),
            scope.ProfiledRevision);
        Directory.CreateDirectory(root);
        foreach (string sourcePath in Directory.EnumerateFiles(
                     lease.RootPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = Path.GetRelativePath(lease.RootPath, sourcePath);
            string targetPath = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(sourcePath, targetPath, overwrite: false);
        }

        string sourceIdentity = await ComputeSourceIdentityAsync(root, cancellationToken)
            .ConfigureAwait(false);
        return new AnalysisWorkspace(root, sourceIdentity);
    }

    private static void RequireLeaseBinding(
        ICommittedRepositorySnapshotLease lease,
        AnalysisScope scope)
    {
        if (lease.RepositoryId != scope.RepositoryId ||
            lease.ScanRunId != scope.ScanRunId ||
            !string.Equals(lease.ProfiledRevision, scope.ProfiledRevision, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("codegraph_revision_lease_binding_mismatch");
        }
    }

    private static async Task<string> ComputeSourceIdentityAsync(
        string root,
        CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(path => !Path.GetRelativePath(root, path)
                         .Replace('\\', '/')
                         .StartsWith(".codegraph/", StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            byte[] pathBytes = Encoding.UTF8.GetBytes(relative);
            byte[] length = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(length, pathBytes.Length);
            hash.AppendData(length);
            hash.AppendData(pathBytes);
            await using FileStream stream = new(
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

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }

    private sealed record AnalysisWorkspace(string RootPath, string SourceIdentity);
}

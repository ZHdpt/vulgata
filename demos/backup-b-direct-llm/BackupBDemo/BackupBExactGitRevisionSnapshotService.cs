using System.Buffers.Binary;
using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace BackupBDemo;

/// <summary>
/// Demo-only <see cref="IExactGitRevisionSnapshotService"/> that creates snapshots
/// from pre-existing local git working copies instead of fetching from a remote URL.
/// </summary>
internal sealed class BackupBExactGitRevisionSnapshotService : IExactGitRevisionSnapshotService
{
    private readonly Dictionary<Guid, (string Path, string Revision)> _repositoryMap;

    public BackupBExactGitRevisionSnapshotService(
        Dictionary<Guid, (string Path, string Revision)> repositoryMap)
    {
        _repositoryMap = repositoryMap ?? throw new ArgumentNullException(nameof(repositoryMap));
    }

    public async Task<ICommittedRepositorySnapshotLease> AcquireAsync(
        Repository repository,
        Guid scanRunId,
        string expectedRef,
        string expectedCommit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (!_repositoryMap.TryGetValue(repository.Id, out (string Path, string Revision) mapping))
        {
            throw new InvalidOperationException(
                $"Repository {repository.Id} is not in the demo repository map.");
        }

        string workingCopyPath = mapping.Path;
        string profiledRevision = mapping.Revision;

        if (!StringComparer.Ordinal.Equals(profiledRevision, expectedCommit))
        {
            throw new InvalidOperationException(
                $"Local commit {profiledRevision} does not match expected commit {expectedCommit}.");
        }

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "vulgata-backup-b-exact",
            repository.Id.ToString("N"),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        string snapshotRoot = Path.Combine(tempRoot, "snapshot");
        Directory.CreateDirectory(snapshotRoot);

        // Create tar archive from the local repo at the expected commit
        string tarPath = Path.Combine(tempRoot, "snapshot.tar");
        await RunGitArchiveAsync(workingCopyPath, expectedCommit, tarPath, cancellationToken);

        // Extract tar to snapshot root
        await using (var archiveStream = new FileStream(
            tarPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            using var reader = new TarReader(archiveStream, leaveOpen: true);
            TarEntry? entry;
            while ((entry = await reader.GetNextEntryAsync(copyData: false, cancellationToken)) is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.EntryType == TarEntryType.GlobalExtendedAttributes)
                {
                    continue;
                }

                if (entry.EntryType is not TarEntryType.RegularFile and
                    not TarEntryType.V7RegularFile and
                    not TarEntryType.Directory)
                {
                    continue;
                }

                string relativePath = entry.Name.Replace('/', Path.DirectorySeparatorChar);
                string destinationPath = Path.Combine(snapshotRoot, relativePath);

                // Ensure the full path is within the snapshot root
                string fullSnapshotRoot = Path.GetFullPath(snapshotRoot);
                string fullDestination = Path.GetFullPath(destinationPath);
                if (!fullDestination.StartsWith(fullSnapshotRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.EntryType == TarEntryType.Directory)
                {
                    Directory.CreateDirectory(fullDestination);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fullDestination)!);
                await using var destination = new FileStream(
                    fullDestination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (entry.DataStream is not null)
                {
                    await entry.DataStream.CopyToAsync(destination, cancellationToken);
                }
            }
        }

        // Clean up the tar file
        try { File.Delete(tarPath); } catch { /* best effort */ }

        byte[] snapshotIdentity = await ComputeSnapshotIdentityAsync(snapshotRoot, cancellationToken);

        return new BackupBSnapshotLease(
            repository.Id,
            scanRunId,
            expectedCommit,
            snapshotRoot,
            tempRoot,
            snapshotIdentity);
    }

    private static async Task RunGitArchiveAsync(
        string workingCopyPath,
        string commit,
        string outputPath,
        CancellationToken cancellationToken)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"archive --format=tar -- {commit}",
            WorkingDirectory = workingCopyPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        process.Start();

        await using var output = new FileStream(
            outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);

        string error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git archive failed with exit code {process.ExitCode}: {error}");
        }
    }

    private static async Task<byte[]> ComputeSnapshotIdentityAsync(
        string snapshotRoot,
        CancellationToken cancellationToken)
    {
        string normalizedRoot = Path.GetFullPath(snapshotRoot);
        string[] entries = Directory.EnumerateFileSystemEntries(
                normalizedRoot, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] lengthBytes = new byte[sizeof(long)];
        byte[] buffer = new byte[64 * 1024];

        foreach (string entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.GetFullPath(entry);
            FileAttributes attributes = File.GetAttributes(fullPath);
            bool directory = (attributes & FileAttributes.Directory) != 0;
            string relativePath = Path.GetRelativePath(normalizedRoot, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/');

            byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
            hash.AppendData([directory ? (byte)1 : (byte)0]);
            BinaryPrimitives.WriteInt64LittleEndian(lengthBytes, pathBytes.Length);
            hash.AppendData(lengthBytes);
            hash.AppendData(pathBytes);

            if (directory)
            {
                continue;
            }

            long length = new FileInfo(fullPath).Length;
            BinaryPrimitives.WriteInt64LittleEndian(lengthBytes, length);
            hash.AppendData(lengthBytes);

            await using var stream = new FileStream(
                fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hash.AppendData(buffer.AsSpan(0, read));
            }
        }

        return hash.GetHashAndReset();
    }

    private sealed class BackupBSnapshotLease : ICommittedRepositorySnapshotLease
    {
        private readonly string _tempRoot;
        private readonly byte[] _snapshotIdentity;
        private bool _disposed;

        internal BackupBSnapshotLease(
            Guid repositoryId,
            Guid scanRunId,
            string profiledRevision,
            string snapshotRoot,
            string tempRoot,
            byte[] snapshotIdentity)
        {
            RepositoryId = repositoryId;
            ScanRunId = scanRunId;
            ProfiledRevision = profiledRevision;
            RootPath = snapshotRoot;
            _tempRoot = tempRoot;
            _snapshotIdentity = snapshotIdentity;
        }

        public Guid RepositoryId { get; }
        public Guid ScanRunId { get; }
        public string ProfiledRevision { get; }
        public string RootPath { get; }

        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                try
                {
                    if (Directory.Exists(_tempRoot))
                    {
                        Directory.Delete(_tempRoot, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup
                }
            }

            return ValueTask.CompletedTask;
        }
    }
}
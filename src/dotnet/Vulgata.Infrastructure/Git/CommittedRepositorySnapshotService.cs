using System.Collections.Concurrent;
using System.Diagnostics;
using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Git;

internal sealed record CommittedRepositorySnapshotLimits(
    long MaximumEntryBytes,
    long MaximumAggregateBytes,
    int MaximumEntries = 10_000)
{
    public static CommittedRepositorySnapshotLimits Production { get; } =
        new(8L * 1024 * 1024, 64L * 1024 * 1024);
}

internal interface ICommittedRepositoryArchiveSource
{
    Task<Stream> OpenArchiveAsync(
        string workingCopyPath,
        string profiledRevision,
        CancellationToken cancellationToken);
}

public sealed class CommittedRepositorySnapshotService : ICommittedRepositorySnapshotService
{
    private const int _maximumRelativePathCharacters = 2_048;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _repositoryLocks = new();
    private readonly GitWorkingCopyOptions _options;
    private readonly ICommittedRepositoryArchiveSource _archiveSource;
    private readonly CommittedRepositorySnapshotLimits _limits;

    public CommittedRepositorySnapshotService(GitWorkingCopyOptions options)
        : this(options, GitCommittedRepositoryArchiveSource.Instance, CommittedRepositorySnapshotLimits.Production)
    {
    }

    internal CommittedRepositorySnapshotService(
        GitWorkingCopyOptions options,
        ICommittedRepositoryArchiveSource archiveSource,
        CommittedRepositorySnapshotLimits limits)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(archiveSource);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limits.MaximumEntryBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limits.MaximumAggregateBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limits.MaximumEntries);
        if (limits.MaximumEntryBytes > limits.MaximumAggregateBytes)
        {
            throw new ArgumentException(
                "The per-entry snapshot bound cannot exceed the aggregate bound.",
                nameof(limits));
        }

        _options = options;
        _archiveSource = archiveSource;
        _limits = limits;
    }

    public async Task<ICommittedRepositorySnapshotLease> AcquireAsync(
        Repository repository,
        Guid scanRunId,
        GitWorkingCopyResult workingCopy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(workingCopy);
        if (scanRunId == Guid.Empty)
        {
            throw new ArgumentException("Scan Run identity cannot be empty.", nameof(scanRunId));
        }

        if (!workingCopy.Succeeded ||
            string.IsNullOrWhiteSpace(workingCopy.WorkingCopyPath) ||
            string.IsNullOrWhiteSpace(workingCopy.ProfiledRevision))
        {
            throw new InvalidOperationException(
                "A successful revision-bound working copy is required.");
        }

        string profiledRevision = RequireProfiledRevision(workingCopy.ProfiledRevision);
        string workingCopyPath = Path.GetFullPath(workingCopy.WorkingCopyPath);
        SemaphoreSlim acquisitionLock =
            _repositoryLocks.GetOrAdd(repository.Id, static _ => new SemaphoreSlim(1, 1));
        await acquisitionLock.WaitAsync(cancellationToken);

        string? snapshotRoot = null;
        try
        {
            await VerifyCleanRevisionAsync(
                workingCopyPath,
                profiledRevision,
                cancellationToken);
            snapshotRoot = CreateSnapshotRoot(repository.Id, scanRunId);
            await MaterializeAsync(
                workingCopyPath,
                profiledRevision,
                snapshotRoot,
                cancellationToken);
            await VerifyCleanRevisionAsync(
                workingCopyPath,
                profiledRevision,
                cancellationToken);
            byte[] snapshotIdentity = await ComputeSnapshotIdentityAsync(
                snapshotRoot,
                _limits,
                cancellationToken);
            return new CommittedRepositorySnapshotLease(
                repository.Id,
                scanRunId,
                profiledRevision,
                snapshotRoot,
                workingCopyPath,
                snapshotIdentity,
                _limits,
                acquisitionLock);
        }
        catch
        {
            try
            {
                if (snapshotRoot is not null)
                {
                    DeleteSnapshot(snapshotRoot);
                }
            }
            catch
            {
                // Preserve the acquisition failure; an unavailable file may
                // prevent immediate best-effort cleanup.
            }
            finally
            {
                acquisitionLock.Release();
            }

            throw;
        }
    }

    private string CreateSnapshotRoot(Guid repositoryId, Guid scanRunId)
    {
        string storageRoot = Path.GetFullPath(_options.RootPath);
        Directory.CreateDirectory(storageRoot);
        RejectReparsePoint(storageRoot);
        string snapshotStorageRoot = Path.Combine(storageRoot, ".snapshots");
        Directory.CreateDirectory(snapshotStorageRoot);
        RejectReparsePoint(snapshotStorageRoot);
        string snapshotParent = Path.Combine(
            snapshotStorageRoot,
            repositoryId.ToString("N"));
        Directory.CreateDirectory(snapshotParent);
        RejectReparsePoint(snapshotParent);
        string snapshotRoot = Path.Combine(
            snapshotParent,
            $"{scanRunId:N}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(snapshotRoot);
        RejectReparsePoint(snapshotRoot);
        return snapshotRoot;
    }

    private async Task MaterializeAsync(
        string workingCopyPath,
        string profiledRevision,
        string snapshotRoot,
        CancellationToken cancellationToken)
    {
        await using Stream archive = await _archiveSource.OpenArchiveAsync(
            workingCopyPath,
            profiledRevision,
            cancellationToken);
        using var reader = new TarReader(archive, leaveOpen: true);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long aggregateBytes = 0;

        try
        {
            TarEntry? entry;
            while ((entry = await reader.GetNextEntryAsync(
                copyData: false,
                cancellationToken)) is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.EntryType == TarEntryType.GlobalExtendedAttributes)
                {
                    continue;
                }

                bool directory = entry.EntryType == TarEntryType.Directory;
                if (!directory &&
                    entry.EntryType is not TarEntryType.RegularFile and
                        not TarEntryType.V7RegularFile)
                {
                    throw new InvalidDataException(
                        "Committed snapshot contains a non-regular entry.");
                }

                string relativePath = RequireSafeRelativePath(entry.Name, directory);
                if (!paths.Add(relativePath))
                {
                    throw new InvalidDataException(
                        "Committed snapshot contains a duplicate path.");
                }

                if (paths.Count > _limits.MaximumEntries)
                {
                    throw new InvalidDataException(
                        "Committed snapshot exceeds its entry bound.");
                }

                string destinationPath = ResolveContainedPath(snapshotRoot, relativePath);
                EnsureSafeAncestors(snapshotRoot, destinationPath);
                if (directory)
                {
                    Directory.CreateDirectory(destinationPath);
                    RejectReparsePoint(destinationPath);
                    continue;
                }

                if (entry.Length < 0 || entry.Length > _limits.MaximumEntryBytes)
                {
                    throw new InvalidDataException(
                        "Committed snapshot entry exceeds its safe bound.");
                }

                aggregateBytes = checked(aggregateBytes + entry.Length);
                if (aggregateBytes > _limits.MaximumAggregateBytes)
                {
                    throw new InvalidDataException(
                        "Committed snapshot exceeds its aggregate safe bound.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                EnsureSafeAncestors(snapshotRoot, destinationPath);
                if (entry.DataStream is null && entry.Length != 0)
                {
                    throw new InvalidDataException(
                        "Committed snapshot regular entry has no data.");
                }

                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (entry.DataStream is not null)
                {
                    await CopyExactlyAsync(
                        entry.DataStream,
                        destination,
                        entry.Length,
                        cancellationToken);
                }

                RejectReparsePoint(destinationPath);
            }
        }
        catch (Exception exception) when (
            exception is EndOfStreamException or FormatException or OverflowException)
        {
            throw new InvalidDataException("Committed snapshot archive is malformed.", exception);
        }
    }

    private static async Task CopyExactlyAsync(
        Stream source,
        Stream destination,
        long length,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[64 * 1024];
        long remaining = length;
        while (remaining > 0)
        {
            int read = await source.ReadAsync(
                buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                cancellationToken);
            if (read == 0)
            {
                throw new InvalidDataException(
                    "Committed snapshot entry ended before its declared length.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    private static string RequireSafeRelativePath(string path, bool directory)
    {
        string candidate = directory ? path.TrimEnd('/') : path;
        if (candidate.Length == 0 ||
            candidate.Length > _maximumRelativePathCharacters ||
            Path.IsPathFullyQualified(candidate) ||
            IsWindowsDrivePath(candidate) ||
            candidate[0] is '/' or '\\' ||
            candidate.Contains('\\', StringComparison.Ordinal) ||
            candidate.Contains(':', StringComparison.Ordinal) ||
            candidate.Any(char.IsControl))
        {
            throw new InvalidDataException("Committed snapshot path is unsafe.");
        }

        string[] segments = candidate.Split('/');
        char[] invalidFileNameCharacters = Path.GetInvalidFileNameChars();
        if (segments.Any(segment =>
                segment.Length == 0 ||
                segment is "." or ".." ||
                segment.IndexOfAny(invalidFileNameCharacters) >= 0))
        {
            throw new InvalidDataException("Committed snapshot path is unsafe.");
        }

        return string.Join('/', segments);
    }

    private static bool IsWindowsDrivePath(string path) =>
        path.Length >= 3 &&
        char.IsAsciiLetter(path[0]) &&
        path[1] == ':' &&
        path[2] == '/';

    private static string ResolveContainedPath(string snapshotRoot, string relativePath)
    {
        string normalizedRoot = Path.GetFullPath(snapshotRoot);
        string destinationPath = Path.GetFullPath(
            Path.Combine(
                normalizedRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        if (!destinationPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Committed snapshot path escapes its private root.");
        }

        return destinationPath;
    }

    private static void EnsureSafeAncestors(string snapshotRoot, string destinationPath)
    {
        string? current = Path.GetDirectoryName(destinationPath);
        while (current is not null &&
            !StringComparer.OrdinalIgnoreCase.Equals(current, snapshotRoot))
        {
            if (Directory.Exists(current))
            {
                RejectReparsePoint(current);
            }

            current = Path.GetDirectoryName(current);
        }

        RejectReparsePoint(snapshotRoot);
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "Committed snapshot contains a reparse point.");
        }
    }

    private static async Task VerifyCleanRevisionAsync(
        string workingCopyPath,
        string expectedRevision,
        CancellationToken cancellationToken)
    {
        GitWorkingCopyService.ProcessResult status = await GitWorkingCopyService.RunGitAsync(
            ["-C", workingCopyPath, "status", "--porcelain=v1", "--untracked-files=all"],
            cancellationToken);
        if (status.ExitCode != 0 || status.StandardOutput.Length != 0)
        {
            throw new InvalidOperationException("Reusable working copy is not clean.");
        }

        GitWorkingCopyService.ProcessResult revision = await GitWorkingCopyService.RunGitAsync(
            ["-C", workingCopyPath, "rev-parse", "--verify", "HEAD"],
            cancellationToken,
            maximumStandardOutputCharacters: 66,
            requireCompleteStandardOutput: true);
        if (revision.ExitCode != 0 ||
            !GitWorkingCopyService.TryParseProfiledRevision(
                revision.StandardOutput,
                out string? actualRevision) ||
            !StringComparer.Ordinal.Equals(expectedRevision, actualRevision))
        {
            throw new InvalidOperationException(
                "Reusable working copy revision does not match the Profiled Revision.");
        }
    }

    private static string RequireProfiledRevision(string revision)
    {
        if (revision.Length == 0 ||
            revision.Length > 64 ||
            revision.Any(static character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException("Profiled Revision is invalid.");
        }

        return revision;
    }

    private static async Task<byte[]> ComputeSnapshotIdentityAsync(
        string snapshotRoot,
        CommittedRepositorySnapshotLimits limits,
        CancellationToken cancellationToken)
    {
        string normalizedRoot = Path.GetFullPath(snapshotRoot);
        if (!Directory.Exists(normalizedRoot))
        {
            throw new InvalidOperationException(
                "Committed snapshot identity is unavailable.");
        }

        RejectReparsePoint(normalizedRoot);
        var entries = new List<SnapshotIdentityEntry>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(normalizedRoot);
        long aggregateBytes = 0;
        while (pendingDirectories.TryPop(out string? directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(directory);
            foreach (string entryPath in Directory.EnumerateFileSystemEntries(
                         directory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entries.Count >= limits.MaximumEntries)
                {
                    throw new InvalidDataException(
                        "Committed snapshot identity exceeds its entry bound.");
                }

                string fullPath = Path.GetFullPath(entryPath);
                string relativePath = Path.GetRelativePath(
                        normalizedRoot,
                        fullPath)
                    .Replace(Path.DirectorySeparatorChar, '/');
                FileAttributes attributes = File.GetAttributes(fullPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        "Committed snapshot identity contains a reparse point.");
                }

                bool directoryEntry = (attributes & FileAttributes.Directory) != 0;
                RequireSafeRelativePath(relativePath, directoryEntry);
                ResolveContainedPath(normalizedRoot, relativePath);
                long length = 0;
                if (directoryEntry)
                {
                    pendingDirectories.Push(fullPath);
                }
                else
                {
                    length = new FileInfo(fullPath).Length;
                    if (length < 0 || length > limits.MaximumEntryBytes)
                    {
                        throw new InvalidDataException(
                            "Committed snapshot identity entry exceeds its safe bound.");
                    }

                    aggregateBytes = checked(aggregateBytes + length);
                    if (aggregateBytes > limits.MaximumAggregateBytes)
                    {
                        throw new InvalidDataException(
                            "Committed snapshot identity exceeds its aggregate safe bound.");
                    }
                }

                entries.Add(new SnapshotIdentityEntry(
                    relativePath,
                    fullPath,
                    directoryEntry,
                    length));
            }
        }

        entries.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        byte[] header = new byte[13];
        foreach (SnapshotIdentityEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] pathBytes = Encoding.UTF8.GetBytes(entry.RelativePath);
            header[0] = entry.IsDirectory ? (byte)1 : (byte)2;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
                header.AsSpan(1, 4),
                pathBytes.Length);
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(
                header.AsSpan(5),
                entry.Length);
            hash.AppendData(header);
            hash.AppendData(pathBytes);
            if (entry.IsDirectory)
            {
                continue;
            }

            await using var stream = new FileStream(
                entry.FullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: buffer.Length,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            long remaining = entry.Length;
            while (remaining > 0)
            {
                int read = await stream.ReadAsync(
                    buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                    cancellationToken);
                if (read == 0)
                {
                    throw new InvalidDataException(
                        "Committed snapshot changed during identity verification.");
                }

                hash.AppendData(buffer.AsSpan(0, read));
                remaining -= read;
            }

            if (stream.ReadByte() != -1)
            {
                throw new InvalidDataException(
                    "Committed snapshot changed during identity verification.");
            }
        }

        return hash.GetHashAndReset();
    }

    private static void DeleteSnapshot(string snapshotRoot)
    {
        TryDeleteDirectory(snapshotRoot, recursive: true);
        string? repositoryRoot = Path.GetDirectoryName(snapshotRoot);
        if (repositoryRoot is not null)
        {
            TryDeleteDirectory(repositoryRoot, recursive: false);
            string? snapshotStorageRoot = Path.GetDirectoryName(repositoryRoot);
            if (snapshotStorageRoot is not null)
            {
                TryDeleteDirectory(snapshotStorageRoot, recursive: false);
            }
        }
    }

    private static void TryDeleteDirectory(string path, bool recursive)
    {
        try
        {
            Directory.Delete(path, recursive);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException) when (!recursive)
        {
        }
    }

    private sealed class CommittedRepositorySnapshotLease(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string rootPath,
        string workingCopyPath,
        byte[] snapshotIdentity,
        CommittedRepositorySnapshotLimits limits,
        SemaphoreSlim acquisitionLock) : ICommittedRepositorySnapshotLease
    {
        private int _disposed;

        public Guid RepositoryId { get; } = repositoryId;
        public Guid ScanRunId { get; } = scanRunId;
        public string ProfiledRevision { get; } = profiledRevision;
        public string RootPath { get; } = rootPath;

        public async Task VerifyIdentityAsync(
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(
                Volatile.Read(ref _disposed) != 0,
                this);
            await VerifyCleanRevisionAsync(
                workingCopyPath,
                ProfiledRevision,
                cancellationToken);
            byte[] actualIdentity;
            try
            {
                actualIdentity = await ComputeSnapshotIdentityAsync(
                    RootPath,
                    limits,
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or
                    InvalidDataException or OverflowException)
            {
                throw new InvalidOperationException(
                    "Committed snapshot identity verification failed.",
                    exception);
            }

            if (!CryptographicOperations.FixedTimeEquals(
                    snapshotIdentity,
                    actualIdentity))
            {
                throw new InvalidOperationException(
                    "Committed snapshot identity no longer matches its leased identity.");
            }
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try
                {
                    DeleteSnapshot(RootPath);
                }
                catch (IOException)
                {
                    // Snapshot cleanup is best effort and must not turn a
                    // completed or already-failed profile into a new failure.
                }
                catch (UnauthorizedAccessException)
                {
                    // Preserve the profiling outcome when cleanup is denied.
                }
                finally
                {
                    acquisitionLock.Release();
                }
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed record SnapshotIdentityEntry(
        string RelativePath,
        string FullPath,
        bool IsDirectory,
        long Length);
}

internal sealed class GitCommittedRepositoryArchiveSource : ICommittedRepositoryArchiveSource
{
    private const long _maximumRawArchiveBytes = 128L * 1024 * 1024;

    private GitCommittedRepositoryArchiveSource()
    {
    }

    public static GitCommittedRepositoryArchiveSource Instance { get; } = new();

    public async Task<Stream> OpenArchiveAsync(
        string workingCopyPath,
        string profiledRevision,
        CancellationToken cancellationToken)
    {
        string archivePath = Path.Combine(
            Path.GetTempPath(),
            $"vulgata-committed-snapshot-{Guid.NewGuid():N}.tar");
        var archive = new FileStream(
            archivePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous |
                FileOptions.SequentialScan |
                FileOptions.DeleteOnClose);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingCopyPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        process.StartInfo.ArgumentList.Add("archive");
        process.StartInfo.ArgumentList.Add("--format=tar");
        process.StartInfo.ArgumentList.Add("--");
        process.StartInfo.ArgumentList.Add(profiledRevision);

        Task<string>? standardError = null;
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Unable to start committed snapshot materialization.");
            }

            standardError = GitWorkingCopyService.ReadBoundedAsync(
                process.StandardError,
                maximumCharacters: 4_096,
                requireComplete: false,
                CancellationToken.None);
            await CopyBoundedAsync(
                process.StandardOutput.BaseStream,
                archive,
                _maximumRawArchiveBytes,
                cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await standardError;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Unable to materialize the committed Git tree.");
            }

            archive.Position = 0;
            return archive;
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            await archive.DisposeAsync();
            if (standardError is not null)
            {
                try
                {
                    await standardError;
                }
                catch
                {
                    // Preserve the primary archive failure.
                }
            }

            throw;
        }
    }

    private static async Task CopyBoundedAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[64 * 1024];
        long totalBytes = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            totalBytes = checked(totalBytes + read);
            if (totalBytes > maximumBytes)
            {
                throw new InvalidDataException(
                    "Committed snapshot archive exceeds its raw safety bound.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}

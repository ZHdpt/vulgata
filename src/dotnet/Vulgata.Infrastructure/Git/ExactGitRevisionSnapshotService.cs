using System.Buffers.Binary;
using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Git;

public sealed class ExactGitRevisionSnapshotService : IExactGitRevisionSnapshotService
{
    private const int _maximumRevisionOutputCharacters = 42;
    private const int _maximumRelativePathCharacters = 2_048;
    private readonly GitWorkingCopyOptions _options;
    private readonly CommittedRepositorySnapshotLimits _limits;

    public ExactGitRevisionSnapshotService(GitWorkingCopyOptions options)
        : this(options, CommittedRepositorySnapshotLimits.Production)
    {
    }

    internal ExactGitRevisionSnapshotService(
        GitWorkingCopyOptions options,
        CommittedRepositorySnapshotLimits limits)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(limits);
        _options = options;
        _limits = limits;
    }

    public async Task<ICommittedRepositorySnapshotLease> AcquireAsync(
        Repository repository,
        Guid scanRunId,
        string expectedRef,
        string expectedCommit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return await AcquireAsync(
            repository.Id,
            repository.GitUrl,
            scanRunId,
            expectedRef,
            expectedCommit,
            cancellationToken);
    }

    internal async Task<ICommittedRepositorySnapshotLease> AcquireAsync(
        Guid repositoryId,
        string repositoryOrigin,
        Guid scanRunId,
        string expectedRef,
        string expectedCommit,
        CancellationToken cancellationToken = default)
    {
        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException("Repository identity cannot be empty.", nameof(repositoryId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryOrigin);
        if (scanRunId == Guid.Empty)
        {
            throw new ArgumentException("Scan Run identity cannot be empty.", nameof(scanRunId));
        }

        RequireFullRef(expectedRef);
        RequireFullCommit(expectedCommit);
        string storageRoot = Path.GetFullPath(_options.RootPath);
        string exactRoot = CreateExactRoot(storageRoot, repositoryId);
        string operationRoot = CreateOperationRoot(exactRoot);
        string gitRoot = Path.Combine(operationRoot, "g");
        string snapshotRoot = Path.Combine(operationRoot, "s");

        try
        {
            await RunRequiredGitAsync(
                ["init", "--quiet", "--bare", "--", gitRoot],
                cancellationToken);
            RejectReparsePoint(gitRoot);
            await RunRequiredGitAsync(
                ["-C", gitRoot, "remote", "add", "origin", "--", repositoryOrigin],
                cancellationToken);
            await VerifyOriginAsync(gitRoot, repositoryOrigin, cancellationToken);
            await RunRequiredGitAsync(
                [
                    "-C",
                    gitRoot,
                    "fetch",
                    "--no-tags",
                    "--no-write-fetch-head",
                    "origin",
                    $"+{expectedRef}:{expectedRef}",
                ],
                cancellationToken);
            await VerifyExactCommitAsync(
                gitRoot,
                expectedRef,
                expectedCommit,
                cancellationToken);
            Directory.CreateDirectory(snapshotRoot);
            RejectReparsePoint(snapshotRoot);
            await MaterializeAsync(
                gitRoot,
                expectedCommit,
                snapshotRoot,
                cancellationToken);
            await VerifyExactCommitAsync(
                gitRoot,
                expectedRef,
                expectedCommit,
                cancellationToken);
            byte[] snapshotIdentity = await ComputeSnapshotIdentityAsync(
                snapshotRoot,
                _limits,
                cancellationToken);
            return new ExactGitRevisionSnapshotLease(
                repositoryId,
                scanRunId,
                expectedRef,
                expectedCommit,
                exactRoot,
                operationRoot,
                gitRoot,
                snapshotRoot,
                snapshotIdentity,
                _limits);
        }
        catch (OperationCanceledException)
        {
            await CleanupOperationBestEffortAsync(exactRoot, operationRoot);
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                InvalidDataException or InvalidOperationException or
                CryptographicException)
        {
            bool cleaned = await CleanupOperationBestEffortAsync(exactRoot, operationRoot);
            throw new InvalidOperationException(
                cleaned
                    ? "精确 Git 版本获取失败。"
                    : "精确 Git 版本资源清理失败。");
        }
    }

    private static string CreateExactRoot(string storageRoot, Guid repositoryId)
    {
        Directory.CreateDirectory(storageRoot);
        RejectReparsePoint(storageRoot);
        string revisionsRoot = Path.Combine(storageRoot, ".exact-revisions");
        Directory.CreateDirectory(revisionsRoot);
        RejectReparsePoint(revisionsRoot);
        string exactRoot = Path.Combine(revisionsRoot, repositoryId.ToString("N"));
        Directory.CreateDirectory(exactRoot);
        RejectReparsePoint(exactRoot);
        return exactRoot;
    }

    private static string CreateOperationRoot(string exactRoot)
    {
        string operationToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        string operationRoot = Path.Combine(exactRoot, operationToken);
        Directory.CreateDirectory(operationRoot);
        RejectReparsePoint(operationRoot);
        return operationRoot;
    }

    private static async Task RunRequiredGitAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        GitWorkingCopyService.ProcessResult result = await GitWorkingCopyService.RunGitAsync(
            arguments,
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("精确 Git 版本命令失败。");
        }
    }

    private static async Task VerifyExactCommitAsync(
        string gitRoot,
        string expectedRef,
        string expectedCommit,
        CancellationToken cancellationToken)
    {
        GitWorkingCopyService.ProcessResult result = await GitWorkingCopyService.RunGitAsync(
            ["-C", gitRoot, "rev-parse", "--verify", $"{expectedRef}^{{commit}}"],
            cancellationToken,
            _maximumRevisionOutputCharacters,
            requireCompleteStandardOutput: true);
        string actualCommit = result.StandardOutput.TrimEnd('\r', '\n');
        if (result.ExitCode != 0 ||
            actualCommit.Length != 40 ||
            actualCommit.Any(static character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')) ||
            !StringComparer.Ordinal.Equals(actualCommit, expectedCommit))
        {
            throw new InvalidOperationException("精确 Git 版本不匹配。");
        }
    }

    private static async Task VerifyOriginAsync(
        string gitRoot,
        string expectedGitUrl,
        CancellationToken cancellationToken)
    {
        GitWorkingCopyService.ProcessResult result = await GitWorkingCopyService.RunGitAsync(
            ["-C", gitRoot, "remote", "get-url", "origin"],
            cancellationToken,
            maximumStandardOutputCharacters: 4_096,
            requireCompleteStandardOutput: true);
        if (result.ExitCode != 0 ||
            !StringComparer.Ordinal.Equals(
                result.StandardOutput.TrimEnd('\r', '\n'),
                expectedGitUrl))
        {
            throw new InvalidOperationException("精确 Git 版本来源绑定失败。");
        }
    }

    private async Task MaterializeAsync(
        string gitRoot,
        string expectedCommit,
        string snapshotRoot,
        CancellationToken cancellationToken)
    {
        await using Stream archive = await GitCommittedRepositoryArchiveSource.Instance
            .OpenArchiveAsync(gitRoot, expectedCommit, cancellationToken);
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
                    throw new InvalidDataException("精确版本快照包含不支持的条目。");
                }

                string relativePath = RequireSafeRelativePath(entry.Name, directory);
                if (!paths.Add(relativePath) || paths.Count > _limits.MaximumEntries)
                {
                    throw new InvalidDataException("精确版本快照超过安全边界。");
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
                    throw new InvalidDataException("精确版本快照超过安全边界。");
                }

                aggregateBytes = checked(aggregateBytes + entry.Length);
                if (aggregateBytes > _limits.MaximumAggregateBytes)
                {
                    throw new InvalidDataException("精确版本快照超过安全边界。");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                EnsureSafeAncestors(snapshotRoot, destinationPath);
                if (entry.DataStream is null && entry.Length > 0)
                {
                    throw new InvalidDataException("精确版本快照条目无效。");
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
            throw new InvalidDataException("精确版本快照归档无效。");
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
                throw new InvalidDataException("精确版本快照条目不完整。");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    private static async Task<byte[]> ComputeSnapshotIdentityAsync(
        string snapshotRoot,
        CommittedRepositorySnapshotLimits limits,
        CancellationToken cancellationToken)
    {
        string normalizedRoot = Path.GetFullPath(snapshotRoot);
        if (!Directory.Exists(normalizedRoot))
        {
            throw new InvalidOperationException("精确版本快照标识不可用。");
        }

        RejectReparsePoint(normalizedRoot);
        string[] entries = Directory.EnumerateFileSystemEntries(
                normalizedRoot,
                "*",
                SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (entries.Length > limits.MaximumEntries)
        {
            throw new InvalidDataException("精确版本快照超过安全边界。");
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long aggregateBytes = 0;
        byte[] lengthBytes = new byte[sizeof(long)];
        byte[] buffer = new byte[64 * 1024];
        foreach (string entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.GetFullPath(entry);
            FileAttributes attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("精确版本快照包含重解析点。");
            }

            bool directory = (attributes & FileAttributes.Directory) != 0;
            string relativePath = Path.GetRelativePath(normalizedRoot, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/');
            _ = RequireSafeRelativePath(relativePath, directory);
            _ = ResolveContainedPath(normalizedRoot, relativePath);
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
            if (length < 0 || length > limits.MaximumEntryBytes)
            {
                throw new InvalidDataException("精确版本快照超过安全边界。");
            }

            aggregateBytes = checked(aggregateBytes + length);
            if (aggregateBytes > limits.MaximumAggregateBytes)
            {
                throw new InvalidDataException("精确版本快照超过安全边界。");
            }

            BinaryPrimitives.WriteInt64LittleEndian(lengthBytes, length);
            hash.AppendData(lengthBytes);
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hash.AppendData(buffer.AsSpan(0, read));
            }
        }

        return hash.GetHashAndReset();
    }

    private static string RequireSafeRelativePath(string path, bool directory)
    {
        string candidate = directory ? path.TrimEnd('/') : path;
        if (candidate.Length == 0 ||
            candidate.Length > _maximumRelativePathCharacters ||
            Path.IsPathFullyQualified(candidate) ||
            candidate[0] is '/' or '\\' ||
            candidate.Contains('\\', StringComparison.Ordinal) ||
            candidate.Contains(':', StringComparison.Ordinal) ||
            candidate.Any(char.IsControl))
        {
            throw new InvalidDataException("精确版本快照路径无效。");
        }

        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        if (candidate.Split('/').Any(segment =>
                segment.Length == 0 ||
                segment is "." or ".." ||
                segment.IndexOfAny(invalidCharacters) >= 0))
        {
            throw new InvalidDataException("精确版本快照路径无效。");
        }

        return candidate;
    }

    private static string ResolveContainedPath(string root, string relativePath)
    {
        string normalizedRoot = Path.GetFullPath(root);
        string destination = Path.GetFullPath(Path.Combine(
            normalizedRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("精确版本快照路径越界。");
        }

        return destination;
    }

    private static void EnsureSafeAncestors(string root, string destination)
    {
        string? current = Path.GetDirectoryName(destination);
        while (current is not null &&
            !StringComparer.OrdinalIgnoreCase.Equals(current, root))
        {
            if (Directory.Exists(current))
            {
                RejectReparsePoint(current);
            }

            current = Path.GetDirectoryName(current);
        }

        RejectReparsePoint(root);
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("精确版本私有目录无效。");
        }
    }

    private static void RequireFullRef(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 256 ||
            !(value.StartsWith("refs/tags/", StringComparison.Ordinal) ||
                value.StartsWith("refs/heads/", StringComparison.Ordinal)) ||
            value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains("@{", StringComparison.Ordinal) ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.EndsWith("/", StringComparison.Ordinal) ||
            value.EndsWith(".", StringComparison.Ordinal) ||
            value.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) ||
            value.IndexOfAny(['~', '^', ':', '?', '*', '[', '\\', '+']) >= 0)
        {
            throw new ArgumentException("One safe full tag ref is required.", nameof(value));
        }
    }

    private static void RequireFullCommit(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 40 ||
            value.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("One full lowercase Git commit is required.", nameof(value));
        }
    }

    private static async Task<bool> CleanupOperationBestEffortAsync(
        string exactRoot,
        string operationRoot)
    {
        try
        {
            await DeleteOwnedDirectoryAsync(exactRoot, operationRoot);
            RemoveEmptyParent(exactRoot);
            RemoveEmptyParent(Path.GetDirectoryName(exactRoot)!);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task DeleteOwnedDirectoryAsync(string exactRoot, string path)
    {
        RequireOwnedOperationPath(exactRoot, path);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                ClearReadOnlyAttributes(path);
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 3)
            {
                await Task.Delay(25 * (attempt + 1), CancellationToken.None);
            }
            catch (UnauthorizedAccessException) when (attempt < 3)
            {
                await Task.Delay(25 * (attempt + 1), CancellationToken.None);
            }
        }

        throw new IOException("精确版本私有目录清理失败。");
    }

    private static void RequireOwnedOperationPath(string exactRoot, string path)
    {
        string normalizedRoot = Path.GetFullPath(exactRoot);
        string normalizedPath = Path.GetFullPath(path);
        string prefix = normalizedRoot + Path.DirectorySeparatorChar;
        if (!normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            StringComparer.OrdinalIgnoreCase.Equals(normalizedPath, normalizedRoot))
        {
            throw new InvalidOperationException("精确版本清理目标无效。");
        }
    }

    private static void ClearReadOnlyAttributes(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out string? current))
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(current))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("精确版本私有目录包含重解析点。");
                }

                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
            }
        }
    }

    private static void RemoveEmptyParent(string path)
    {
        try
        {
            Directory.Delete(path, recursive: false);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
    }

    private sealed class ExactGitRevisionSnapshotLease : ICommittedRepositorySnapshotLease
    {
        private readonly SemaphoreSlim _disposeLock = new(1, 1);
        private readonly string _exactRoot;
        private readonly string _expectedCommit;
        private readonly string _expectedRef;
        private readonly string _gitRoot;
        private readonly CommittedRepositorySnapshotLimits _limits;
        private readonly string _operationRoot;
        private readonly byte[] _snapshotIdentity;
        private readonly string _snapshotRoot;
        private bool _cleaned;
        private bool _invalidated;

        internal ExactGitRevisionSnapshotLease(
            Guid repositoryId,
            Guid scanRunId,
            string expectedRef,
            string expectedCommit,
            string exactRoot,
            string operationRoot,
            string gitRoot,
            string snapshotRoot,
            byte[] snapshotIdentity,
            CommittedRepositorySnapshotLimits limits)
        {
            RepositoryId = repositoryId;
            ScanRunId = scanRunId;
            ProfiledRevision = expectedCommit;
            RootPath = snapshotRoot;
            _expectedRef = expectedRef;
            _expectedCommit = expectedCommit;
            _exactRoot = exactRoot;
            _operationRoot = operationRoot;
            _gitRoot = gitRoot;
            _snapshotRoot = snapshotRoot;
            _snapshotIdentity = snapshotIdentity;
            _limits = limits;
        }

        public Guid RepositoryId { get; }
        public Guid ScanRunId { get; }
        public string ProfiledRevision { get; }
        public string RootPath { get; }

        public async Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_invalidated, this);
            await VerifyExactCommitAsync(
                _gitRoot,
                _expectedRef,
                _expectedCommit,
                cancellationToken);
            byte[] actualIdentity = await ComputeSnapshotIdentityAsync(
                _snapshotRoot,
                _limits,
                cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(_snapshotIdentity, actualIdentity))
            {
                throw new InvalidOperationException("精确版本快照标识不匹配。");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _disposeLock.WaitAsync(CancellationToken.None);
            try
            {
                if (_cleaned)
                {
                    return;
                }

                _invalidated = true;
                bool cleaned = await CleanupOperationBestEffortAsync(_exactRoot, _operationRoot);
                if (!cleaned)
                {
                    throw new InvalidOperationException("精确 Git 版本资源清理失败。");
                }

                _cleaned = true;
            }
            finally
            {
                _disposeLock.Release();
            }
        }
    }
}

using System.Diagnostics;
using System.Text;

namespace Vulgata.Tests.RetainedSource;

/// <summary>
/// Owns one local Git repository and one durable source-storage directory for a
/// retained-source test. The fixture has no application, tool, provider, or
/// database dependency; later behavioral tests compose the application seam
/// around these paths.
/// </summary>
internal sealed class RetainedSourceFixture : IAsyncDisposable
{
    private RetainedSourceFixture(
        string rootPath,
        string sourceStorageRoot,
        RetainedSourceGitRepository repository)
    {
        RootPath = rootPath;
        SourceStorageRoot = sourceStorageRoot;
        Repository = repository;
    }

    internal string RootPath { get; }

    internal string SourceStorageRoot { get; }

    internal RetainedSourceGitRepository Repository { get; }

    internal Guid RepositoryId { get; } = Guid.NewGuid();

    internal static async Task<RetainedSourceFixture> CreateAsync(
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>>? files = null,
        CancellationToken cancellationToken = default)
    {
        string rootPath = RetainedSourceOwnedPaths.CreateUniqueRoot("fixture");
        string repositoryPath = RetainedSourceOwnedPaths.ResolveChild(rootPath, "repository");
        string sourceStorageRoot = RetainedSourceOwnedPaths.ResolveChild(rootPath, "source-storage");
        Directory.CreateDirectory(sourceStorageRoot);

        RetainedSourceGitRepository? repository = null;
        try
        {
            repository = await RetainedSourceGitRepository.CreateAsync(
                repositoryPath,
                files,
                cancellationToken);
            return new(rootPath, sourceStorageRoot, repository);
        }
        catch
        {
            if (repository is not null)
            {
                await repository.DisposeAsync();
            }

            await RetainedSourceOwnedPaths.DeleteAsync(rootPath);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Repository.DisposeAsync();
        await RetainedSourceOwnedPaths.DeleteAsync(RootPath);
    }
}

/// <summary>
/// A local, owned Git repository whose contents are controlled entirely by the
/// test. It deliberately exposes only fixture operations and never accepts a
/// caller-supplied executable, credential, or host path.
/// </summary>
internal sealed class RetainedSourceGitRepository : IAsyncDisposable
{
    private RetainedSourceGitRepository(string rootPath)
    {
        RootPath = rootPath;
    }

    internal string RootPath { get; private set; }

    internal string Commit { get; private set; } = string.Empty;

    internal static async Task<RetainedSourceGitRepository> CreateAsync(
        string rootPath,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>>? files,
        CancellationToken cancellationToken = default)
    {
        RetainedSourceOwnedPaths.EnsureOwnedPath(rootPath);
        Directory.CreateDirectory(rootPath);

        var repository = new RetainedSourceGitRepository(rootPath);
        try
        {
            await repository.RunGitAsync(["init", "--quiet"], cancellationToken);
            await repository.RunGitAsync(
                ["config", "user.email", "vulgata-retained-source-tests@example.invalid"],
                cancellationToken);
            await repository.RunGitAsync(
                ["config", "user.name", "Vulgata Retained Source Tests"],
                cancellationToken);
            await repository.RunGitAsync(["config", "core.autocrlf", "false"], cancellationToken);
            await repository.RunGitAsync(["config", "core.symlinks", "true"], cancellationToken);

            IReadOnlyDictionary<string, ReadOnlyMemory<byte>> initialFiles = files ??
                new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
                {
                    ["src/retained-source.txt"] =
                        Encoding.UTF8.GetBytes("retained-source-fixture\n"),
                };
            foreach ((string relativePath, ReadOnlyMemory<byte> content) in initialFiles)
            {
                repository.WriteBytes(relativePath, content.Span);
            }

            repository.Commit = await repository.CommitAsync(
                "retained-source fixture",
                cancellationToken);
            return repository;
        }
        catch
        {
            await repository.DisposeAsync();
            throw;
        }
    }

    internal void WriteBytes(string repositoryRelativePath, ReadOnlySpan<byte> content)
    {
        string path = RetainedSourceOwnedPaths.ResolveRelativePath(
            RootPath,
            repositoryRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content.ToArray());
    }

    internal void WriteText(
        string repositoryRelativePath,
        string content,
        bool includeUtf8Bom = false)
    {
        byte[] encoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetBytes(content);
        if (!includeUtf8Bom)
        {
            WriteBytes(repositoryRelativePath, encoded);
            return;
        }

        byte[] preamble = Encoding.UTF8.GetPreamble();
        byte[] withPreamble = new byte[preamble.Length + encoded.Length];
        preamble.CopyTo(withPreamble, 0);
        encoded.CopyTo(withPreamble, preamble.Length);
        WriteBytes(repositoryRelativePath, withPreamble);
    }

    internal Task<string> CommitAsync(
        string message = "retained-source fixture update",
        CancellationToken cancellationToken = default) =>
        CommitCoreAsync(message, cancellationToken);

    internal async Task<string> CommitIndexAsync(
        string message = "retained-source fixture index update",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        await RunGitAsync(["commit", "--quiet", "--message", message], cancellationToken);
        Commit = await ResolveRevisionAsync("HEAD", cancellationToken);
        return Commit;
    }

    internal Task<string> ResolveRevisionAsync(
        string permittedRef = "HEAD",
        CancellationToken cancellationToken = default) =>
        RunGitAsync(["rev-parse", "--verify", $"{permittedRef}^{{commit}}"], cancellationToken);

    internal Task<string> RunGitAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default) =>
        RunGitProcessAsync(RootPath, arguments, cancellationToken);

    internal Task<string> RunGitWithInputAsync(
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default) =>
        RunGitProcessAsync(RootPath, arguments, cancellationToken, input);

    internal Task<string> MakeRemoteUnavailableAsync()
    {
        RetainedSourceOwnedPaths.EnsureOwnedPath(RootPath);
        string parent = Directory.GetParent(RootPath)!.FullName;
        string unavailableRoot = RetainedSourceOwnedPaths.ResolveChild(
            parent,
            $"remote-unavailable-{Guid.NewGuid():N}");
        if (!Directory.Exists(RootPath))
        {
            throw new DirectoryNotFoundException(
                "The retained-source fixture remote is already unavailable.");
        }

        Directory.Move(RootPath, unavailableRoot);
        RootPath = unavailableRoot;
        return Task.FromResult(unavailableRoot);
    }

    /// <summary>
    /// Adds a symbolic-link tree entry through the Git index. The fixture does
    /// not create an operating-system link, so this remains portable on hosts
    /// where link privileges are unavailable.
    /// </summary>
    internal async Task AddSymbolicLinkEntryAsync(
        string relativePath,
        string target,
        CancellationToken cancellationToken = default)
    {
        string blobId = await RunGitWithInputAsync(
            ["hash-object", "-w", "--stdin"],
            Encoding.UTF8.GetBytes(target),
            cancellationToken);
        await RunGitAsync(
            ["update-index", "--add", "--cacheinfo", $"120000,{blobId},{relativePath}"],
            cancellationToken);
    }

    /// <summary>
    /// Adds a gitlink tree entry from an owned commit object created with Git
    /// plumbing. No nested checkout or external repository is involved.
    /// </summary>
    internal async Task AddGitlinkEntryAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        string blobId = await RunGitWithInputAsync(
            ["hash-object", "-w", "--stdin"],
            Encoding.UTF8.GetBytes("gitlink-fixture\n"),
            cancellationToken);
        string treeId = await RunGitWithInputAsync(
            ["mktree"],
            Encoding.UTF8.GetBytes($"100644 blob {blobId}\tchild.txt\n"),
            cancellationToken);
        string commitId = await RunGitWithInputAsync(
            ["commit-tree", treeId, "-m", "gitlink fixture"],
            ReadOnlyMemory<byte>.Empty,
            cancellationToken);
        await RunGitAsync(
            ["update-index", "--add", "--cacheinfo", $"160000,{commitId},{relativePath}"],
            cancellationToken);
    }

    /// <summary>
    /// Replaces HEAD with a commit whose leaf names are supplied as raw bytes.
    /// This is used only for Git tree entries that a Windows checkout cannot
    /// represent, such as invalid UTF-8 or unsafe path bytes.
    /// </summary>
    internal async Task<string> CommitRawTreeAsync(
        IReadOnlyList<(byte[] PathBytes, byte[] Content)> entries,
        string message = "retained-source raw tree fixture",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            throw new ArgumentException("At least one raw tree entry is required.", nameof(entries));
        }

        var treeInput = new List<byte>();
        foreach ((byte[] pathBytes, byte[] content) in entries)
        {
            string blobId = await RunGitWithInputAsync(
                ["hash-object", "-w", "--stdin"],
                content,
                cancellationToken);
            treeInput.AddRange(Encoding.ASCII.GetBytes($"100644 blob {blobId}\t"));
            treeInput.AddRange(pathBytes);
            treeInput.Add(0);
        }

        string treeId = await RunGitWithInputAsync(
            ["mktree", "-z"],
            treeInput.ToArray(),
            cancellationToken);
        string commitId = await RunGitWithInputAsync(
            ["commit-tree", treeId, "-m", message],
            ReadOnlyMemory<byte>.Empty,
            cancellationToken);
        string headRef = await RunGitAsync(["symbolic-ref", "HEAD"], cancellationToken);
        await RunGitAsync(["update-ref", headRef, commitId], cancellationToken);
        Commit = await ResolveRevisionAsync("HEAD", cancellationToken);
        return Commit;
    }

    public ValueTask DisposeAsync() => RetainedSourceOwnedPaths.DeleteAsync(RootPath);

    private async Task<string> CommitCoreAsync(
        string message,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        await RunGitAsync(["add", "--all"], cancellationToken);
        await RunGitAsync(["commit", "--quiet", "--message", message], cancellationToken);
        Commit = await ResolveRevisionAsync("HEAD", cancellationToken);
        return Commit;
    }

    private static async Task<string> RunGitProcessAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        ReadOnlyMemory<byte>? input = null)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        startInfo.Environment["GIT_AUTHOR_DATE"] = "2026-09-15T00:00:00Z";
        startInfo.Environment["GIT_COMMITTER_DATE"] = "2026-09-15T00:00:00Z";
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException(
                "The retained-source Git fixture could not start Git.");
        }

        if (input is not null)
        {
            await process.StandardInput.BaseStream.WriteAsync(input.Value, cancellationToken);
            await process.StandardInput.BaseStream.FlushAsync(cancellationToken);
            process.StandardInput.Close();
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string output = await outputTask;
        string error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The retained-source Git fixture command failed with exit code {process.ExitCode}: " +
                error.Trim());
        }

        return output.TrimEnd('\r', '\n');
    }
}

internal static class RetainedSourceOwnedPaths
{
    private const string BaseFolderName = "vulgata-retained-source-tests";

    private static string BaseRoot => Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), BaseFolderName));

    internal static string CreateUniqueRoot(string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        string safePurpose = new(purpose
            .Where(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            .ToArray());
        if (safePurpose.Length == 0)
        {
            throw new ArgumentException(
                "A temporary-root purpose is required.",
                nameof(purpose));
        }

        string root = Path.GetFullPath(Path.Combine(
            BaseRoot,
            $"{safePurpose}-{Guid.NewGuid():N}"));
        EnsureOwnedPath(root);
        Directory.CreateDirectory(root);
        return root;
    }

    internal static string ResolveChild(string root, string childName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        if (childName.Contains(Path.DirectorySeparatorChar) ||
            childName.Contains(Path.AltDirectorySeparatorChar) ||
            childName is "." or "..")
        {
            throw new ArgumentException(
                "Fixture child names must be one contained path segment.",
                nameof(childName));
        }

        EnsureOwnedPath(root);
        string child = Path.GetFullPath(Path.Combine(root, childName));
        string rootPrefix = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!child.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The fixture child path escapes its owned root.");
        }

        return child;
    }

    internal static string ResolveRelativePath(string root, string repositoryRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativePath);
        if (Path.IsPathRooted(repositoryRelativePath) ||
            repositoryRelativePath.Contains(':') ||
            repositoryRelativePath.Replace('\\', '/')
                .Split('/')
                .Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException(
                "Fixture paths must be safe Repository-relative paths.",
                nameof(repositoryRelativePath));
        }

        EnsureOwnedPath(root);
        string fullPath = Path.GetFullPath(Path.Combine(
            root,
            repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The fixture path escapes its disposable Repository root.",
                nameof(repositoryRelativePath));
        }

        return fullPath;
    }

    internal static async ValueTask DeleteAsync(string root)
    {
        EnsureOwnedPath(root);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            try
            {
                ClearReadOnlyAttributes(root);
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 3)
            {
                await Task.Delay(25 * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < 3)
            {
                await Task.Delay(25 * (attempt + 1));
            }
        }

        throw new IOException(
            "A disposable retained-source fixture root could not be removed.");
    }

    internal static void EnsureOwnedPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string baseRoot = BaseRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string basePrefix = baseRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, baseRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Retained-source cleanup is restricted to one unique owned test root.");
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
                    continue;
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
}

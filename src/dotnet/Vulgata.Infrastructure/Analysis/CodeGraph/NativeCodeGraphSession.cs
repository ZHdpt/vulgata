using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;
using ModelContextProtocol.Client;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using Vulgata.Infrastructure.Source;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

/// <summary>Prepares one application-selected dedicated workspace for native CodeGraph.</summary>
public sealed class PreparedCodeGraphConnectionFactory
{
    private static readonly ConcurrentDictionary<string, byte> _activeRoots = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private static readonly TimeSpan _preparationTimeout = TimeSpan.FromMinutes(2);
    private const int DiagnosticByteLimit = 16 * 1024;
    private readonly string _commandPath;
    private readonly ICodeGraphProcessRunner _process;
    private readonly Func<string, CancellationToken, Task> _preflight;
    private readonly Action<string>? _beforeStateCreate;

    public PreparedCodeGraphConnectionFactory(string commandPath)
        : this(commandPath, new CodeGraphProcessRunner(), VerifyPinnedBundleAsync)
    {
    }

    internal PreparedCodeGraphConnectionFactory(
        string commandPath,
        ICodeGraphProcessRunner process,
        Func<string, CancellationToken, Task> preflight,
        Action<string>? beforeStateCreate = null)
    {
        if (string.IsNullOrWhiteSpace(commandPath) || !Path.IsPathFullyQualified(commandPath))
        {
            throw new ArgumentException("A declared absolute CodeGraph command is required.", nameof(commandPath));
        }

        _commandPath = Path.GetFullPath(commandPath);
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
        _beforeStateCreate = beforeStateCreate;
    }

    public Task<PreparedCodeGraphConnection> PrepareAsync(
        Guid repositoryId,
        string applicationSelectedDirectory,
        CancellationToken cancellationToken)
        => PrepareAsync(repositoryId, applicationSelectedDirectory, TimeSpan.FromMinutes(30), cancellationToken);

    public async Task<PreparedCodeGraphConnection> PrepareAsync(
        Guid repositoryId,
        string applicationSelectedDirectory,
        TimeSpan remainingLifetime,
        CancellationToken cancellationToken)
    {
        if (remainingLifetime <= TimeSpan.Zero || remainingLifetime > ExecutionOwnerLifetime.Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingLifetime));
        }
        long ownerStartedAt = Stopwatch.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();
        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException("A Repository identity is required.", nameof(repositoryId));
        }

        if (string.IsNullOrWhiteSpace(applicationSelectedDirectory) ||
            !Path.IsPathFullyQualified(applicationSelectedDirectory))
        {
            throw new ArgumentException(
                "A trusted absolute prepared directory is required.",
                nameof(applicationSelectedDirectory));
        }

        string root = Path.GetFullPath(applicationSelectedDirectory);
        if (!Directory.Exists(root) ||
            (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
        {
            throw new DirectoryNotFoundException("The dedicated prepared directory is unavailable.");
        }

        if (!_activeRoots.TryAdd(root, 0))
        {
            throw new InvalidOperationException("The prepared directory already has an index owner.");
        }

        try
        {
            (_, SafeFileHandle? openedRoot) = PreparedSourceTraversal.OpenRoot(root);
            using SafeFileHandle? heldRoot = openedRoot;
            PreparedSourceTraversal.DirectoryIdentity? rootIdentity = heldRoot is null
                ? null : PreparedSourceTraversal.ObserveDirectory(heldRoot);
            if (rootIdentity is null)
            {
                throw new InvalidOperationException("The prepared root identity is unavailable.");
            }
            await _preflight(_commandPath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            (_, SafeFileHandle? checkedRoot) = PreparedSourceTraversal.OpenRoot(root);
            using (checkedRoot)
            {
                if (checkedRoot is null || !PreparedCodeGraphConnection.SameObject(
                    rootIdentity, PreparedSourceTraversal.ObserveDirectory(checkedRoot)))
                {
                    throw new InvalidOperationException("The prepared root changed before initialization.");
                }
            }
            string statePath = Path.Combine(root, ".codegraph");
            if (Directory.EnumerateFileSystemEntries(root).Any(path =>
                string.Equals(Path.GetFileName(path), ".codegraph",
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Pre-existing CodeGraph state is not owned by this preparation.");
            }

            _beforeStateCreate?.Invoke(root);
            (PreparedSourceTraversal.OpenStatus created, SafeFileHandle? openedState) =
                PreparedSourceTraversal.CreateRelativeDirectory(heldRoot!, ".codegraph");
            using SafeFileHandle? heldState = openedState;
            PreparedSourceTraversal.DirectoryIdentity? stateIdentity = heldState is null
                ? null : PreparedSourceTraversal.ObserveDirectory(heldState);
            if (created != PreparedSourceTraversal.OpenStatus.Opened || stateIdentity is null)
            {
                throw new InvalidOperationException("Exclusive CodeGraph state creation failed; existing state is not adopted.");
            }
            (_, SafeFileHandle? dispatchRoot) = PreparedSourceTraversal.OpenRoot(root);
            using (dispatchRoot)
            {
                if (dispatchRoot is null || !PreparedCodeGraphConnection.SameObject(
                    rootIdentity, PreparedSourceTraversal.ObserveDirectory(dispatchRoot)))
                    throw new InvalidOperationException("The prepared root changed before dispatch.");
            }
            IReadOnlyDictionary<string, string> environment = CreateEnvironment();
            TimeSpan preparationBudget = remainingLifetime - Stopwatch.GetElapsedTime(ownerStartedAt);
            if (preparationBudget <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("The prepared connection lifetime expired before initialization.");
            }
            preparationBudget = preparationBudget < _preparationTimeout ? preparationBudget : _preparationTimeout;
            using var preparationDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            preparationDeadline.CancelAfter(preparationBudget);
            CodeGraphProcessRequest request = new(
                _commandPath,
                ["init", root],
                root,
                preparationBudget,
                DiagnosticByteLimit,
                DiagnosticByteLimit,
                environment,
                closeStandardInput: true,
                inheritEnvironmentVariables: false);
            CodeGraphProcessResult result = await _process.RunAsync(request, preparationDeadline.Token);
            preparationDeadline.Token.ThrowIfCancellationRequested();
            if (result.TimedOut || result.ExitCode != 0 ||
                !File.Exists(Path.Combine(statePath, "codegraph.db")))
            {
                throw new InvalidOperationException("CodeGraph preparation did not complete.");
            }

            bool isPowerShellScript = Path.GetExtension(_commandPath).Equals(
                ".ps1", StringComparison.OrdinalIgnoreCase);
            string command = isPowerShellScript ? "pwsh" : _commandPath;
            string[] arguments = isPowerShellScript
                ? ["-NoProfile", "-File", _commandPath,
                   "serve", "--mcp", "-p", root, "--no-watch"]
                : ["serve", "--mcp", "-p", root, "--no-watch"];
            TimeSpan remaining = remainingLifetime - Stopwatch.GetElapsedTime(ownerStartedAt);
            if (remaining <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("The prepared connection lifetime expired during preparation.");
            }
            var connection = new PreparedCodeGraphConnection(
                repositoryId,
                _commandPath,
                command,
                Array.AsReadOnly(arguments),
                root,
                environment,
                () => _activeRoots.TryRemove(root, out _), remainingLifetime: remaining);
            if (!connection.MatchesPreparedObjects(rootIdentity, stateIdentity) ||
                !connection.IsCurrentOwnedState(heldRoot!))
            {
                await connection.DisposeAsync();
                throw new InvalidOperationException("The prepared root or owned state changed during preparation.");
            }
            return connection;
        }
        catch
        {
            _activeRoots.TryRemove(root, out _);
            throw;
        }
    }

    private static IReadOnlyDictionary<string, string> CreateEnvironment()
    {
        var environment = new Dictionary<string, string>(
            StdioClientTransportOptions.GetDefaultEnvironmentVariables(),
            StringComparer.OrdinalIgnoreCase)
        {
            ["CODEGRAPH_NO_DAEMON"] = "1",
            ["CODEGRAPH_NO_WATCH"] = "1",
            ["DO_NOT_TRACK"] = "1",
            ["CODEGRAPH_TELEMETRY"] = "0",
            ["CODEGRAPH_NO_UPDATE_CHECK"] = "1",
            ["CODEGRAPH_NO_DOWNLOAD"] = "1",
        };
        if (OperatingSystem.IsWindows())
        {
            environment["GIT_CONFIG_GLOBAL"] = "NUL";
        }

        return new ReadOnlyDictionary<string, string>(environment);
    }

    private static async Task VerifyPinnedBundleAsync(
        string commandPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(commandPath))
        {
            throw new InvalidOperationException("The declared CodeGraph command is unavailable.");
        }

        if (!Path.GetExtension(commandPath).Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected CodeGraph launcher has no qualified 1.5.0 bundle preflight.");
        }

        string? searchPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(searchPath) ||
            !searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Any(directory => File.Exists(Path.Combine(directory.Trim(), "node.exe"))))
        {
            throw new InvalidOperationException("The pinned CodeGraph shim requires a declared Node launcher on PATH.");
        }

        string prefix = Path.GetDirectoryName(commandPath)!;
        string packageRoot = Path.Combine(prefix, "node_modules", "@colbymchenry", "codegraph");
        string platformRoot = Path.Combine(packageRoot, "node_modules", "@colbymchenry", "codegraph-win32-x64");
        await RequirePackageAsync(Path.Combine(packageRoot, "package.json"),
            "@colbymchenry/codegraph", cancellationToken);
        await RequirePackageAsync(Path.Combine(platformRoot, "package.json"),
            "@colbymchenry/codegraph-win32-x64", cancellationToken);
        if (!File.Exists(Path.Combine(platformRoot, "node.exe")) ||
            !File.Exists(Path.Combine(platformRoot, "lib", "dist", "bin", "codegraph.js")))
        {
            throw new InvalidOperationException("The pinned CodeGraph platform bundle is incomplete.");
        }
    }

    private static async Task RequirePackageAsync(
        string path,
        string expectedName,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The pinned CodeGraph package is unavailable.");
        }

        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using JsonDocument package = await JsonDocument.ParseAsync(stream,
            cancellationToken: cancellationToken);
        JsonElement root = package.RootElement;
        if (!root.TryGetProperty("name", out JsonElement name) ||
            !root.TryGetProperty("version", out JsonElement version) ||
            name.GetString() != expectedName || version.GetString() != "1.5.0")
        {
            throw new InvalidOperationException("The declared CodeGraph bundle is not pinned 1.5.0.");
        }
    }
}

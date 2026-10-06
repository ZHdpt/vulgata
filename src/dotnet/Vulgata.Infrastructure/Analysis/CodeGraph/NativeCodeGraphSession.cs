using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;
using ModelContextProtocol.Client;

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

    public PreparedCodeGraphConnectionFactory(string commandPath)
        : this(commandPath, new CodeGraphProcessRunner(), VerifyPinnedBundleAsync)
    {
    }

    internal PreparedCodeGraphConnectionFactory(
        string commandPath,
        ICodeGraphProcessRunner process,
        Func<string, CancellationToken, Task> preflight)
    {
        if (string.IsNullOrWhiteSpace(commandPath) || !Path.IsPathFullyQualified(commandPath))
        {
            throw new ArgumentException("A declared absolute CodeGraph command is required.", nameof(commandPath));
        }

        _commandPath = Path.GetFullPath(commandPath);
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
    }

    public async Task<PreparedCodeGraphConnection> PrepareAsync(
        Guid repositoryId,
        string applicationSelectedDirectory,
        CancellationToken cancellationToken)
    {
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
            await _preflight(_commandPath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            string statePath = Path.Combine(root, ".codegraph");
            if (Directory.EnumerateFileSystemEntries(root).Any(path =>
                string.Equals(Path.GetFileName(path), ".codegraph",
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Pre-existing CodeGraph state is not owned by this preparation.");
            }

            Directory.CreateDirectory(statePath);
            IReadOnlyDictionary<string, string> environment = CreateEnvironment();
            CodeGraphProcessRequest request = new(
                _commandPath,
                ["init", root],
                root,
                _preparationTimeout,
                DiagnosticByteLimit,
                DiagnosticByteLimit,
                environment,
                closeStandardInput: true,
                inheritEnvironmentVariables: false);
            CodeGraphProcessResult result = await _process.RunAsync(request, cancellationToken);
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
            return new PreparedCodeGraphConnection(
                repositoryId,
                _commandPath,
                command,
                Array.AsReadOnly(arguments),
                root,
                environment,
                () => _activeRoots.TryRemove(root, out _));
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

using System.Security.Cryptography;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

public sealed partial class CodeGraphAnalysisAdapter : PreparedCodeGraphAnalysisAdapter
{
    private static readonly AnalysisPortFailure _unavailable = new(
        AnalysisPortFailureReason.Unavailable,
        "codegraph_binding_unavailable",
        "codegraph binding unavailable");

    private readonly CodeGraphAdapterOptions? _options;
    private readonly ICodeGraphRepositorySnapshotResolver? _snapshots;
    private readonly ICodeGraphProcessRunner? _process;

    public CodeGraphAnalysisAdapter()
    {
    }

    internal CodeGraphAnalysisAdapter(
        CodeGraphAdapterOptions options,
        ICodeGraphRepositorySnapshotResolver snapshots,
        ICodeGraphProcessRunner process)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public override Task<AnalysisIndexResult> IndexAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
        ExecuteIndexAsync(scope, ["init"], cancellationToken);

    public override Task<AnalysisIndexResult> RebuildAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken) =>
        ExecuteIndexAsync(scope, ["index", "--quiet"], cancellationToken);

    public override Task<CodeGraphStatusResult> GetStatusAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
        ExecuteStatusAsync(scope, cancellationToken);

    public override Task<SymbolSearchResult> SearchSymbolsAsync(
        AnalysisScope scope,
        AnalysisSymbolQuery query,
        CancellationToken cancellationToken)
        => ExecuteSymbolSearchAsync(scope, query, cancellationToken);

    public override Task<RelationshipResult> GetCallersAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken) =>
        ExecuteRelationshipsAsync(scope, request, "callers", cancellationToken);

    public override Task<RelationshipResult> GetCalleesAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken) =>
        ExecuteRelationshipsAsync(scope, request, "callees", cancellationToken);

    public override Task<RelatedFilesResult> GetRelatedFilesAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken)
        => ExecuteRelatedFilesAsync(scope, request, cancellationToken);
}

public sealed class CodeGraphIntegrityProbe : IAnalysisRuntimeHealthProbe
{
    private static readonly (string RelativePath, string Hash)[] _components =
    [
        (Path.Combine("node_modules", "@colbymchenry", "codegraph-win32-x64", "lib", "dist", "bin", "codegraph.js"),
            "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03"),
        (Path.Combine("node_modules", "@colbymchenry", "codegraph-win32-x64", "node.exe"),
            "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3"),
        ("npm-shim.js", "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877"),
    ];

    public async Task<AnalysisHealthProbeResult> CheckConfiguredIntegrityAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        string? commandPath = ResolveCommandPath();
        if (commandPath is null)
        {
            return NotReady("codegraph_unavailable", "codegraph unavailable");
        }

        string packageRoot = Path.Combine(
            Path.GetDirectoryName(commandPath)!, "node_modules", "@colbymchenry", "codegraph");
        foreach ((string relativePath, string expectedHash) in _components)
        {
            string path = Path.Combine(packageRoot, relativePath);
            if (!File.Exists(path) ||
                !StringComparer.Ordinal.Equals(
                    await ComputeSha256Async(path, cancellationToken), expectedHash))
            {
                return NotReady("codegraph_integrity_mismatch", "codegraph integrity mismatch");
            }
        }

        return new AnalysisHealthProbeResult(
            AnalysisRuntimeHealthStatus.Ready,
            "none",
            string.Empty,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            [new AnalysisHealthCheck("codegraph", AnalysisRuntimeHealthStatus.Ready)]);
    }

    private static string? ResolveCommandPath()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim(), "codegraph.ps1");
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static AnalysisHealthProbeResult NotReady(string code, string diagnostic) =>
        new(
            AnalysisRuntimeHealthStatus.NotReady,
            code,
            diagnostic,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            [new AnalysisHealthCheck("codegraph", AnalysisRuntimeHealthStatus.NotReady)]);
}

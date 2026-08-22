using System.Security.Cryptography;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

public sealed partial class CodeGraphAnalysisAdapter :
    PreparedCodeGraphAnalysisAdapter,
    IAsyncDisposable
{
    private static readonly AnalysisPortFailure _unavailable = new(
        AnalysisPortFailureReason.Unavailable,
        "codegraph_binding_unavailable",
        "codegraph binding unavailable");

    private readonly CodeGraphAdapterOptions? _options;
    private readonly ICodeGraphRepositorySnapshotResolver? _snapshots;
    private readonly ICodeGraphProcessRunner? _process;
    private readonly object _preparedStatusLock = new();
    private PreparedStatusObservation? _preparedStatus;

    public CodeGraphAnalysisAdapter()
    {
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The adapter owns and disposes the revision-bound snapshot resolver.")]
    public CodeGraphAnalysisAdapter(
        AnalysisRuntimeOptions options,
        IPreparedRevisionLeaseResolver revisionLeases)
        : this(
            new CodeGraphAdapterOptions(
                ResolveCommandPath((options ?? throw new ArgumentNullException(nameof(options)))
                    .CodeGraphCommandPath),
                TimeSpan.FromSeconds(30),
                StandardOutputByteLimit: 256 * 1024,
                StandardErrorByteLimit: 16 * 1024),
            new PreparedRevisionCodeGraphSnapshotResolver(
                revisionLeases ?? throw new ArgumentNullException(nameof(revisionLeases))),
            new CodeGraphProcessRunner())
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

    public override async Task<CodeGraphStatusResult> GetStatusAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        long startedAt = Environment.TickCount64;
        CodeGraphStatusResult result = await ExecuteStatusAsync(scope, cancellationToken);
        if (result.Status == AnalysisOperationStatus.Succeeded &&
            result.Ready &&
            !result.Stale &&
            result.Failure is null)
        {
            var observation = new PreparedStatusObservation(
                scope.RepositoryId,
                scope.ScanRunId,
                scope.ProfiledRevision,
                scope.RuntimeIdentityHash,
                result.IndexedFileCount,
                Math.Max(0, Environment.TickCount64 - startedAt));
            lock (_preparedStatusLock)
            {
                _preparedStatus = observation;
            }
        }

        return result;
    }

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

    public override async Task<PreparedCodeGraphProofResult> BuildCompletenessProofAsync(
        PreparedCodeGraphProofRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsBound)
        {
            return new PreparedCodeGraphProofResult(
                AnalysisOperationStatus.Failed,
                Proof: null,
                _unavailable);
        }

        try
        {
            CodeGraphRepositorySnapshot snapshot = await ResolveSnapshotAsync(
                request.Scope,
                cancellationToken);
            PreparedStatusObservation? observation;
            lock (_preparedStatusLock)
            {
                observation = _preparedStatus;
            }

            if (observation is null ||
                observation.RepositoryId != request.Scope.RepositoryId ||
                observation.ScanRunId != request.Scope.ScanRunId ||
                !StringComparer.Ordinal.Equals(
                    observation.ProfiledRevision,
                    request.Scope.ProfiledRevision) ||
                !StringComparer.Ordinal.Equals(
                    observation.RuntimeIdentityHash,
                    request.Scope.RuntimeIdentityHash))
            {
                return new PreparedCodeGraphProofResult(
                    AnalysisOperationStatus.Failed,
                    Proof: null,
                    new AnalysisPortFailure(
                        AnalysisPortFailureReason.NotReady,
                        "codegraph_complete_status_required",
                        "codegraph complete status required"));
            }

            var proof = new CodeGraphCompletenessProof(
                request.Scope.RepositoryId,
                request.Scope.ScanRunId,
                request.Scope.ProfiledRevision,
                request.SnapshotIdentity,
                request.SnapshotRootBindingHash,
                request.Scope.RuntimeIdentityHash,
                snapshot.ExpectedIndexIdentity,
                "codegraph-completeness-proof/1.0",
                Initialized: true,
                CodeGraphIndexState.Complete,
                PendingAdded: 0,
                PendingModified: 0,
                PendingRemoved: 0,
                WorktreeMismatch: false,
                ReindexRecommended: false,
                PendingReferences: 0,
                observation.IndexedFileCount,
                request.LogicalProjectBinding,
                request.LogicalIndexBinding,
                RepositoryBindingMatched: true,
                ScanRunBindingMatched: true,
                RevisionBindingMatched: true,
                RuntimeBindingMatched: true,
                SnapshotBindingMatched: true,
                ProjectPathTrusted: true,
                IndexPathTrusted: true,
                IdentityHash: string.Empty,
                observation.DurationMilliseconds);
            PreparedCanonicalArtifact identity = PreparedScanIdentityBuilders.CreateCodeGraphProof(proof);
            proof = proof with { IdentityHash = identity.Fingerprint };
            return new PreparedCodeGraphProofResult(
                AnalysisOperationStatus.Succeeded,
                proof,
                Failure: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new PreparedCodeGraphProofResult(
                AnalysisOperationStatus.Failed,
                Proof: null,
                MapFailure(exception));
        }
    }

    public override Task<PreparedRelatedFilesResult> GetPreparedRelatedFilesAsync(
        PreparedRelatedFilesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PreparedRelatedFilesResult(
            AnalysisOperationStatus.Failed,
            [],
            new AnalysisPortFailure(
                AnalysisPortFailureReason.Unavailable,
                "codegraph_prepared_expansion_unavailable",
                "codegraph prepared expansion unavailable")));
    }

    public async ValueTask DisposeAsync()
    {
        if (_snapshots is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed record PreparedStatusObservation(
        Guid RepositoryId,
        Guid ScanRunId,
        string ProfiledRevision,
        string RuntimeIdentityHash,
        int IndexedFileCount,
        long DurationMilliseconds);

    private static string ResolveCommandPath(string configuredPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);
        if (Path.IsPathFullyQualified(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (string directory in path.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string candidate = Path.Combine(directory, configuredPath);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return Path.GetFullPath(configuredPath);
    }
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

using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedScanning.TestDoubles;

public sealed class StubPreparedRevisionAcquirer : IPreparedRevisionAcquirer
{
    public Func<PreparedRevisionRequest, CancellationToken, Task<PreparedRevisionAcquisitionResult>>
        Handler { get; set; } = (_, _) => Task.FromResult(new PreparedRevisionAcquisitionResult(
            null,
            new PreparedScanFailure(
                PreparedScanFailureReason.SourceIdentityMismatch,
                "revision_not_configured",
                "revision acquisition was not configured")));

    public Task<PreparedRevisionAcquisitionResult> AcquireAsync(
        PreparedRevisionRequest request,
        CancellationToken cancellationToken) => Handler(request, cancellationToken);
}

public sealed class StubPreparedSnapshotSource : IPreparedSnapshotSource
{
    public Func<PreparedRevisionSnapshot, CancellationToken, Task<PreparedSnapshotReadResult>>
        Handler { get; set; } = (_, _) => Task.FromResult(new PreparedSnapshotReadResult(
            [],
            new PreparedScanFailure(
                PreparedScanFailureReason.UnsafeInput,
                "snapshot_not_configured",
                "snapshot test double was not configured")));

    public Task<PreparedSnapshotReadResult> ReadAsync(
        PreparedRevisionSnapshot snapshot,
        CancellationToken cancellationToken) => Handler(snapshot, cancellationToken);
}

public sealed class StubReadyAnalysisRuntimeHealthProbe : IAnalysisRuntimeHealthProbe
{
    public Task<AnalysisHealthProbeResult> CheckConfiguredIntegrityAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AnalysisHealthProbeResult(
            AnalysisRuntimeHealthStatus.Ready,
            "test-runtime-ready",
            "deterministic prepared-scan test runtime",
            1_800_000_000_000,
            [
                new AnalysisHealthCheck("analysis-runtime", AnalysisRuntimeHealthStatus.Ready),
                new AnalysisHealthCheck("codegraph", AnalysisRuntimeHealthStatus.Ready),
                new AnalysisHealthCheck("jdt-ls", AnalysisRuntimeHealthStatus.Ready),
            ],
            new('9', 64)));
    }
}

public sealed class StubPreparedCodeGraphAnalysisPort : IPreparedCodeGraphAnalysisPort
{
    public Func<PreparedCodeGraphProofRequest, CancellationToken, Task<PreparedCodeGraphProofResult>>
        ProofHandler { get; set; } = (_, _) => Task.FromResult(new PreparedCodeGraphProofResult(
            AnalysisOperationStatus.Failed,
            null,
            Failure("codegraph_proof_not_configured")));

    public Func<PreparedRelatedFilesRequest, CancellationToken, Task<PreparedRelatedFilesResult>>
        RelatedFilesHandler { get; set; } = (_, _) => Task.FromResult(new PreparedRelatedFilesResult(
            AnalysisOperationStatus.Failed,
            [],
            Failure("codegraph_related_not_configured")));

    public Task<PreparedCodeGraphProofResult> BuildCompletenessProofAsync(
        PreparedCodeGraphProofRequest request,
        CancellationToken cancellationToken) => ProofHandler(request, cancellationToken);

    public Task<PreparedRelatedFilesResult> GetPreparedRelatedFilesAsync(
        PreparedRelatedFilesRequest request,
        CancellationToken cancellationToken) => RelatedFilesHandler(request, cancellationToken);

    public Task<AnalysisIndexResult> IndexAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(FailedIndex("codegraph_index_not_configured"));

    public Task<AnalysisIndexResult> RebuildAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(FailedIndex("codegraph_rebuild_not_configured"));

    public Task<CodeGraphStatusResult> GetStatusAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
        Task.FromResult(new CodeGraphStatusResult(
            AnalysisOperationStatus.Failed,
            ready: false,
            stale: true,
            indexedFileCount: 0,
            Failure("codegraph_status_not_configured")));

    public Task<SymbolSearchResult> SearchSymbolsAsync(
        AnalysisScope scope,
        AnalysisSymbolQuery query,
        CancellationToken cancellationToken) => Task.FromResult(new SymbolSearchResult(
            AnalysisOperationStatus.Failed,
            [],
            Failure("codegraph_search_not_configured")));

    public Task<RelationshipResult> GetCallersAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken) => FailedRelationshipAsync("codegraph_callers_not_configured");

    public Task<RelationshipResult> GetCalleesAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken) => FailedRelationshipAsync("codegraph_callees_not_configured");

    public Task<RelatedFilesResult> GetRelatedFilesAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken) => Task.FromResult(new RelatedFilesResult(
            AnalysisOperationStatus.Failed,
            [],
            Failure("codegraph_v1_related_not_configured")));

    private static AnalysisIndexResult FailedIndex(string code) => new(
        AnalysisOperationStatus.Failed,
        "not-configured",
        durationMilliseconds: 0,
        indexedFileCount: 0,
        Failure(code));

    private static Task<RelationshipResult> FailedRelationshipAsync(string code) =>
        Task.FromResult(new RelationshipResult(
            AnalysisOperationStatus.Failed,
            [],
            Failure(code)));

    private static AnalysisPortFailure Failure(string code) => new(
        AnalysisPortFailureReason.NotReady,
        code,
        "test double not configured");
}

public sealed class StubPreparedJavaReadinessPort : IPreparedJavaReadinessPort
{
    public Func<PreparedJavaReadinessRequest, CancellationToken, Task<PreparedJavaReadinessResult>>
        Handler { get; set; } = (_, _) => Task.FromResult(new PreparedJavaReadinessResult(
            AnalysisOperationStatus.Failed,
            null,
            new AnalysisPortFailure(
                AnalysisPortFailureReason.NotReady,
                "jdt_readiness_not_configured",
                "test double not configured")));

    public Task<PreparedJavaReadinessResult> ProveReadinessAsync(
        PreparedJavaReadinessRequest request,
        CancellationToken cancellationToken) => Handler(request, cancellationToken);
}

public sealed class StubPreparedCommunicationCatalogSource : IPreparedCommunicationCatalogSource
{
    public Func<CancellationToken, Task<PreparedCatalogLoadResult>> Handler { get; set; } =
        _ => Task.FromResult(new PreparedCatalogLoadResult(
            null,
            new PreparedScanFailure(
                PreparedScanFailureReason.UnsafeInput,
                "catalog_not_configured",
                "catalog test double was not configured")));

    public Task<PreparedCatalogLoadResult> LoadAsync(CancellationToken cancellationToken) =>
        Handler(cancellationToken);
}

public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow.ToUniversalTime();

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        _utcNow += amount;
    }
}

public sealed class PreparedFailureInjector
{
    private readonly HashSet<string> _armed = new(StringComparer.Ordinal);

    public void Arm(string failurePoint) => _armed.Add(RequireFailurePoint(failurePoint));

    public bool IsArmed(string failurePoint) => _armed.Contains(RequireFailurePoint(failurePoint));

    public void ThrowIfArmed(string failurePoint)
    {
        string point = RequireFailurePoint(failurePoint);
        if (_armed.Contains(point))
        {
            throw new InjectedPreparedScanFailureException(point);
        }
    }

    private static string RequireFailurePoint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 64 || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException("A bounded machine failure point is required.", nameof(value));
        }

        return value;
    }
}

public sealed class InjectedPreparedScanFailureException(string failurePoint) : Exception(failurePoint)
{
    public string FailurePoint { get; } = failurePoint;
}

using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Analysis.Java;

public enum PreparedReadinessStatus
{
    Ready,
    NotApplicable,
    Failed,
}

/// <summary>Typed, stable fields observed at the prepared Java readiness seam.</summary>
public sealed record PreparedJavaReadinessOutcome(
    PreparedReadinessStatus Outcome,
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string SnapshotIdentity,
    string RuntimeIdentityHash,
    string JdtLsReleaseIdentity,
    string JdtLsArchiveSha256,
    int JavaMajorVersion,
    string ClientName,
    string ClientVersion,
    AnalysisTransport Transport,
    bool WorkspaceBindingMatches,
    bool Initialized,
    bool ImportStateUsable,
    long DocumentSymbolCount,
    string JavaReadinessIdentityHash,
    long DurationMilliseconds,
    AnalysisPortFailure? Failure);

/// <summary>
/// Closed JDT-LS readiness entry point. Readiness is fail-closed until the
/// caller supplies the prepared inventory and stable snapshot bindings through
/// the accepted request contract.
/// </summary>
public sealed class PreparedJavaReadinessService : IPreparedJavaReadinessPort
{
    private static readonly AnalysisPortFailure _inventoryRequired = new(
        AnalysisPortFailureReason.Rejected,
        "jdt_readiness_inventory_required",
        "jdt readiness inventory required");

    public Task<PreparedJavaReadinessOutcome> EvaluateReadinessAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PreparedJavaReadinessOutcome(
            PreparedReadinessStatus.Failed,
            scope.RepositoryId,
            scope.ScanRunId,
            scope.ProfiledRevision,
            string.Empty,
            scope.RuntimeIdentityHash,
            "jdt-language-server-1.61.0-202608141332",
            string.Empty,
            21,
            "StreamJsonRpc",
            "2.25.29",
            AnalysisTransport.Stdio,
            WorkspaceBindingMatches: false,
            Initialized: false,
            ImportStateUsable: false,
            DocumentSymbolCount: 0,
            JavaReadinessIdentityHash: string.Empty,
            DurationMilliseconds: 0,
            _inventoryRequired));
    }

    public Task<PreparedJavaReadinessResult> ProveReadinessAsync(
        PreparedJavaReadinessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PreparedJavaReadinessResult(
            AnalysisOperationStatus.Failed,
            Proof: null,
            _inventoryRequired));
    }
}

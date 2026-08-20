using System.Diagnostics;
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
    private const string _jdtLsRelease = "jdt-language-server-1.61.0-202608141332";
    private const string _jdtLsArchiveSha256 =
        "bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f";
    private static readonly AnalysisPortFailure _inventoryRequired = new(
        AnalysisPortFailureReason.Rejected,
        "jdt_readiness_inventory_required",
        "jdt readiness inventory required");
    private readonly IJavaAnalysisSessionFactory? _sessionFactory;

    public PreparedJavaReadinessService()
    {
    }

    public PreparedJavaReadinessService(IJavaAnalysisSessionFactory sessionFactory)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

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

    public async Task<PreparedJavaReadinessResult> ProveReadinessAsync(
        PreparedJavaReadinessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.EligibleJavaRelativePaths.Count == 0)
        {
            var proof = new JavaReadinessProof(
                request.Scope.RepositoryId,
                request.Scope.ScanRunId,
                request.Scope.ProfiledRevision,
                request.SnapshotIdentity,
                request.Scope.RuntimeIdentityHash,
                request.WorkspaceBindingHash,
                "java-readiness/1.0",
                JavaReadinessOutcome.NotApplicable,
                _jdtLsRelease,
                _jdtLsArchiveSha256,
                JavaMajor: 21,
                StreamJsonRpcVersion: "2.25.29",
                AnalysisTransport.Stdio,
                WorkspaceExclusive: false,
                ReadinessFile: null,
                InitializeCompleted: false,
                InitializedNotificationSent: false,
                JavaImportState.NotApplicable,
                DocumentSymbolCount: 0,
                RepositoryBindingMatched: true,
                ScanRunBindingMatched: true,
                RevisionBindingMatched: true,
                RuntimeBindingMatched: true,
                SnapshotBindingMatched: true,
                IdentityHash: null,
                ObservedDurationMilliseconds: 0);
            PreparedCanonicalArtifact identity = PreparedScanIdentityBuilders.CreateJavaReadiness(proof);
            proof = proof with { IdentityHash = identity.Fingerprint };
            return new PreparedJavaReadinessResult(
                AnalysisOperationStatus.Succeeded,
                proof,
                Failure: null);
        }

        string? readinessFile = request.EligibleJavaRelativePaths
            .Where(IsReadinessCandidate)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        if (readinessFile is null || _sessionFactory is null)
        {
            return new PreparedJavaReadinessResult(
                AnalysisOperationStatus.Failed,
                Proof: null,
                _inventoryRequired);
        }

        long remainingMilliseconds = request.Scope.Deadline.UnixTimeMilliseconds -
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (remainingMilliseconds <= 0)
        {
            return Failed(AnalysisPortFailureReason.TimedOut, "jdt_readiness_timed_out");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(60_000, remainingMilliseconds)));
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            await using IJavaAnalysisSession session = await _sessionFactory
                .OpenAsync(request.Scope, timeout.Token)
                .ConfigureAwait(false);
            JavaNavigationResult symbols = await session.GetDocumentSymbolsAsync(
                request.Scope,
                new JavaNavigationRequest(
                    readinessFile,
                    null,
                    new AnalysisOperationBounds(1_000, 1_048_576)),
                timeout.Token).ConfigureAwait(false);
            timer.Stop();
            if (symbols.Status != AnalysisOperationStatus.Succeeded ||
                symbols.Failure is not null ||
                symbols.Evidence.Count == 0)
            {
                return new PreparedJavaReadinessResult(
                    AnalysisOperationStatus.Failed,
                    Proof: null,
                    symbols.Failure ?? new AnalysisPortFailure(
                        AnalysisPortFailureReason.NotReady,
                        "jdt_readiness_symbols_empty",
                        "jdt readiness symbols empty"));
            }

            var proof = new JavaReadinessProof(
                request.Scope.RepositoryId,
                request.Scope.ScanRunId,
                request.Scope.ProfiledRevision,
                request.SnapshotIdentity,
                request.Scope.RuntimeIdentityHash,
                request.WorkspaceBindingHash,
                "java-readiness/1.0",
                JavaReadinessOutcome.Ready,
                _jdtLsRelease,
                _jdtLsArchiveSha256,
                JavaMajor: 21,
                StreamJsonRpcVersion: "2.25.29",
                AnalysisTransport.Stdio,
                WorkspaceExclusive: true,
                readinessFile,
                InitializeCompleted: true,
                InitializedNotificationSent: true,
                JavaImportState.Usable,
                DocumentSymbolCount: symbols.Evidence.Count,
                RepositoryBindingMatched: true,
                ScanRunBindingMatched: true,
                RevisionBindingMatched: true,
                RuntimeBindingMatched: true,
                SnapshotBindingMatched: true,
                IdentityHash: null,
                ObservedDurationMilliseconds: timer.ElapsedMilliseconds);
            PreparedCanonicalArtifact identity = PreparedScanIdentityBuilders.CreateJavaReadiness(proof);
            return new PreparedJavaReadinessResult(
                AnalysisOperationStatus.Succeeded,
                proof with { IdentityHash = identity.Fingerprint },
                Failure: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(AnalysisPortFailureReason.TimedOut, "jdt_readiness_timed_out");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Failed(AnalysisPortFailureReason.Unavailable, "jdt_readiness_unavailable");
        }
    }

    private static bool IsReadinessCandidate(string value)
    {
        string fileName = Path.GetFileName(value);
        return !string.IsNullOrWhiteSpace(value) &&
            !string.Equals(fileName, "module-info.java", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fileName, "package-info.java", StringComparison.OrdinalIgnoreCase);
    }

    private static PreparedJavaReadinessResult Failed(
        AnalysisPortFailureReason reason,
        string code) => new(
            AnalysisOperationStatus.Failed,
            Proof: null,
            new AnalysisPortFailure(reason, code, code.Replace('_', ' ')));
}

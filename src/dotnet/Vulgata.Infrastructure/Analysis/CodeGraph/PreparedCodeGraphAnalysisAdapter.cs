using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

/// <summary>
/// Closed prepared-scan CodeGraph surface. The Story 12.1 concrete adapter
/// derives from this seam so adding preparation does not create a second
/// concrete raw-tool adapter.
/// </summary>
public abstract class PreparedCodeGraphAnalysisAdapter : IPreparedCodeGraphAnalysisPort
{
    public abstract Task<AnalysisIndexResult> IndexAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken);

    public abstract Task<AnalysisIndexResult> RebuildAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken);

    public abstract Task<CodeGraphStatusResult> GetStatusAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken);

    public abstract Task<SymbolSearchResult> SearchSymbolsAsync(
        AnalysisScope scope,
        AnalysisSymbolQuery query,
        CancellationToken cancellationToken);

    public abstract Task<RelationshipResult> GetCallersAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken);

    public abstract Task<RelationshipResult> GetCalleesAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken);

    public abstract Task<RelatedFilesResult> GetRelatedFilesAsync(
        AnalysisScope scope,
        AnalysisSymbolRequest request,
        CancellationToken cancellationToken);

    public abstract Task<PreparedCodeGraphProofResult> BuildCompletenessProofAsync(
        PreparedCodeGraphProofRequest request,
        CancellationToken cancellationToken);

    public abstract Task<PreparedRelatedFilesResult> GetPreparedRelatedFilesAsync(
        PreparedRelatedFilesRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Bounded outcome metadata for a prepared CodeGraph proof attempt.</summary>
public sealed record PreparedCodeGraphProof(
    bool Complete,
    string CodeGraphProofIdentityHash,
    long DurationMilliseconds,
    AnalysisPortFailure? Failure);

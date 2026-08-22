using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.Source;

public sealed class PreparedRevisionSourceReader(
    IPreparedRevisionLeaseResolver revisionLeases) : IRevisionBoundSourceReader
{
    public Task<SourceExcerptResult> ReadAsync(
        AnalysisScope scope,
        RevisionBoundSourceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);

        if (!revisionLeases.TryResolveLease(
                scope.WorkingCopyBindingToken,
                out ICommittedRepositorySnapshotLease? lease) || lease is null)
        {
            const string unavailable = "analysis_unavailable";
            return Task.FromResult(new SourceExcerptResult(
                AnalysisOperationStatus.Failed,
                scope.RepositoryId,
                scope.ProfiledRevision,
                request.RelativePath,
                request.Range,
                request.ExpectedSourceSha256,
                unavailable,
                Encoding.UTF8.GetByteCount(unavailable),
                new AnalysisPortFailure(
                    AnalysisPortFailureReason.Unavailable,
                    "source_revision_lease_unavailable",
                    "source_revision_lease_unavailable")));
        }

        IRevisionBoundSourceReader reader = new RevisionBoundSourceReader(
            lease,
            scope.WorkingCopyBindingToken);
        return reader.ReadAsync(scope, request, cancellationToken);
    }
}

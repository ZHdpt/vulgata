using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Git;

public sealed class CompositePreparedRevisionLeaseResolver(
    PreparedScanRevisionAcquirer preparedScanRevisions,
    PreparedPass1RevisionBindingRegistry preparedPass1Revisions) :
    IPreparedRevisionLeaseResolver
{
    public bool TryResolveLease(
        string bindingToken,
        out ICommittedRepositorySnapshotLease? lease) =>
        preparedScanRevisions.TryResolveLease(bindingToken, out lease) ||
        preparedPass1Revisions.TryResolveLease(bindingToken, out lease);
}

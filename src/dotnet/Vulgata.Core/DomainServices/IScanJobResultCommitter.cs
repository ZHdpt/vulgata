namespace Vulgata.Core.DomainServices;

public interface IScanJobResultCommitter
{
    Task<ScanJobResultCommitResult> CommitAsync(
        ScanJobResultCommitRequest request,
        CancellationToken cancellationToken);
}

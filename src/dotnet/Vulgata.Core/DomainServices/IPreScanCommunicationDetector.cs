namespace Vulgata.Core.DomainServices;

public interface IPreScanCommunicationDetector
{
    Task<PreScanResultDraft> DetectAsync(
        RepositoryProfileDraft profile,
        string profiledRevision,
        ICommittedRepositorySnapshotLease committedSnapshot,
        CancellationToken cancellationToken = default);
}

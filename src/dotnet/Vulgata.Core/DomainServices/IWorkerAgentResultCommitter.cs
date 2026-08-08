namespace Vulgata.Core.DomainServices;

public enum WorkerAgentResultCommitOutcome
{
    Committed,
    Conflict,
    Rejected,
    Cancelled,
    Failed,
}

public interface IWorkerAgentResultCommitter
{
    Task<bool> HasCommittedResultAsync(
        WorkerAgentJobPacket packet,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default);

    Task<WorkerAgentResultCommitOutcome> CommitAsync(
        WorkerAgentJobPacket packet,
        WorkerAgentResultEnvelope validResult,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default);

    Task<bool> HasCommittedResultAsync(
        WorkerAgentJobPacket packet,
        RepositoryAccessScope scope,
        PreparedPass2RuntimeBinding runtimeBinding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeBinding);
        return !runtimeBinding.IsCanonical()
            ? Task.FromResult(false)
            : HasCommittedResultAsync(packet, scope, cancellationToken);
    }

    Task<WorkerAgentResultCommitOutcome> CommitAsync(
        WorkerAgentJobPacket packet,
        WorkerAgentResultEnvelope validResult,
        RepositoryAccessScope scope,
        PreparedPass2RuntimeBinding runtimeBinding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeBinding);
        return !runtimeBinding.IsCanonical()
            ? Task.FromResult(WorkerAgentResultCommitOutcome.Rejected)
            : CommitAsync(packet, validResult, scope, cancellationToken);
    }
}

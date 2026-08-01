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
    Task<WorkerAgentResultCommitOutcome> CommitAsync(
        WorkerAgentJobPacket packet,
        WorkerAgentResultEnvelope validResult,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default);
}

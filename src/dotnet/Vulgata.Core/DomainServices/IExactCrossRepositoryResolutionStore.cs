namespace Vulgata.Core.DomainServices;

public interface IExactCrossRepositoryResolutionStore
{
    Task<ExactResolutionBatchOutcome> ApplyAsync(
        ExactResolutionDecisionBatch decisions,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default);
}

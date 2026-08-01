using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Resolves one authorized Repository and its trusted working-copy identity,
/// then delegates exact committed-revision slice hydration. Repository lookup,
/// working-copy acquisition, and snapshot hydration all remain scoped to the
/// caller-supplied Repository access scope.
/// </summary>
public sealed class RevisionBoundWorkerExecutionContextSource(
    IRepositoryRepository repositoryRepository,
    IGitWorkingCopyService workingCopyService,
    RevisionBoundWorkerContextHydrator hydrator)
    : IWorkerAgentExecutionContextSource
{
    private readonly IRepositoryRepository _repositoryRepository =
        repositoryRepository ?? throw new ArgumentNullException(nameof(repositoryRepository));
    private readonly IGitWorkingCopyService _workingCopyService =
        workingCopyService ?? throw new ArgumentNullException(nameof(workingCopyService));
    private readonly RevisionBoundWorkerContextHydrator _hydrator =
        hydrator ?? throw new ArgumentNullException(nameof(hydrator));

    public async Task<WorkerAgentExecutionContext> GetAsync(
        WorkerAgentJobPacket packet,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(scope);
        if (!scope.Contains(packet.RepositoryId))
        {
            throw new UnauthorizedAccessException(
                "The Worker Agent Repository is outside the authorized scope.");
        }

        try
        {
            Repository? repository = await _repositoryRepository
                .GetBySystemAndIdAsync(
                    packet.SystemId,
                    packet.RepositoryId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (repository is null ||
                repository.Id != packet.RepositoryId ||
                repository.SystemId != packet.SystemId)
            {
                throw new InvalidDataException();
            }

            GitWorkingCopyResult workingCopy = await _workingCopyService
                .AcquireAsync(repository, packet.ScanRunId, cancellationToken)
                .ConfigureAwait(false);
            if (!workingCopy.Succeeded ||
                string.IsNullOrWhiteSpace(workingCopy.WorkingCopyPath) ||
                !StringComparer.Ordinal.Equals(
                    workingCopy.ProfiledRevision,
                    packet.ProfiledRevision))
            {
                throw new InvalidDataException();
            }

            return await _hydrator.HydrateAsync(
                packet,
                repository,
                workingCopy,
                scope,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidDataException("Worker Agent 源上下文校验失败。");
        }
    }
}

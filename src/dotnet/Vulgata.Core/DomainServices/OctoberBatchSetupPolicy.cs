namespace Vulgata.Core.DomainServices;

public sealed class OctoberBatchSetupPolicy
{
    private readonly IOctoberSetupWriter _writer;

    public OctoberBatchSetupPolicy(IOctoberSetupWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public async Task<OctoberPrepareResult> PrepareAsync(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new OctoberPrepareResult(OctoberPrepareStatus.Cancelled, null);
        }

        if (!IsExactAuthorizedSelection(request, authority))
        {
            return new OctoberPrepareResult(OctoberPrepareStatus.Denied, null);
        }

        OctoberPrepareResult result = await _writer.CommitAsync(request, authority, cancellationToken);
        if (result.Status is not (OctoberPrepareStatus.Created or OctoberPrepareStatus.AlreadyIdentical))
        {
            return result with { CommittedBatch = null };
        }

        OctoberCommittedBatch? committed = result.CommittedBatch;
        if (committed is null ||
            !string.Equals(committed.TargetId, request.TargetId, StringComparison.Ordinal) ||
            committed.BatchId != request.BatchId ||
            !string.Equals(committed.CanonicalDatabaseIdentity, authority.CanonicalDatabaseIdentity,
                StringComparison.Ordinal) ||
            !string.Equals(committed.SelectionDigest, request.SelectionDigest, StringComparison.Ordinal))
        {
            return new OctoberPrepareResult(OctoberPrepareStatus.StorageFailure, null);
        }

        return result;
    }

    private static bool IsExactAuthorizedSelection(
        OctoberSetupRequest? request,
        OctoberAuthorizedSelection? authority)
    {
        if (request is null || authority is null ||
            string.IsNullOrWhiteSpace(request.TargetId) ||
            request.BatchId == Guid.Empty ||
            string.IsNullOrWhiteSpace(authority.SubjectId) ||
            string.IsNullOrWhiteSpace(authority.CanonicalDatabaseIdentity) ||
            string.IsNullOrWhiteSpace(request.SelectionDigest) ||
            request.Repositories is null ||
            request.ContextSelections is null ||
            authority.RepositoryIds is null ||
            !string.Equals(request.TargetId, authority.TargetId, StringComparison.Ordinal) ||
            request.BatchId != authority.BatchId ||
            !string.Equals(request.SelectionDigest, authority.SelectionDigest, StringComparison.Ordinal) ||
            request.Repositories.Count == 0 ||
            request.Repositories.Count != authority.RepositoryIds.Count)
        {
            return false;
        }

        HashSet<Guid> selectedRepositoryIds = [];
        foreach (OctoberRepositorySelection repository in request.Repositories)
        {
            if (repository is null || repository.RepositoryId == Guid.Empty ||
                repository.SystemId == Guid.Empty ||
                string.IsNullOrWhiteSpace(repository.RootBindingIdentity) ||
                string.IsNullOrWhiteSpace(repository.OperatorLabel) ||
                !selectedRepositoryIds.Add(repository.RepositoryId))
            {
                return false;
            }
        }

        HashSet<Guid> authorizedRepositoryIds = [];
        foreach (Guid repositoryId in authority.RepositoryIds)
        {
            if (repositoryId == Guid.Empty || !authorizedRepositoryIds.Add(repositoryId))
            {
                return false;
            }
        }

        return selectedRepositoryIds.SetEquals(authorizedRepositoryIds);
    }
}

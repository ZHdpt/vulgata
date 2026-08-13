using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace BackupBDemo;

/// <summary>
/// Demo-only IGitWorkingCopyService that returns pre-existing local git
/// working copies instead of cloning from a remote URL. The demo seed
/// repositories already have the expected commit checked out as HEAD.
/// </summary>
internal sealed class BackupBLocalGitWorkingCopyService : IGitWorkingCopyService
{
    private readonly Dictionary<Guid, (string Path, string Revision)> _repositoryMap;

    public BackupBLocalGitWorkingCopyService(
        Dictionary<Guid, (string Path, string Revision)> repositoryMap)
    {
        _repositoryMap = repositoryMap;
    }

    public Task<GitWorkingCopyResult> AcquireAsync(
        Repository repository,
        Guid scanRunId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        if (!_repositoryMap.TryGetValue(repository.Id, out var mapping))
        {
            return Task.FromResult(GitWorkingCopyResult.Failure(
                "No local working copy mapping for repository " + repository.Id));
        }

        if (!Directory.Exists(mapping.Path))
        {
            return Task.FromResult(GitWorkingCopyResult.Failure(
                "Local working copy path does not exist: " + mapping.Path));
        }

        return Task.FromResult(GitWorkingCopyResult.Success(
            mapping.Path,
            mapping.Revision.ToLowerInvariant()));
    }
}
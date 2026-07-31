namespace Vulgata.Core.DomainServices;

public interface IKnowledgeStoreRepository
{
    Task<KnowledgeCommitResult> CommitAsync(
        KnowledgeCommit request,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default);

    Task<RebuildProjectionStatus> RebuildProjectionAsync(
        Guid documentVersionId,
        RepositoryAccessScope scope,
        CancellationToken cancellationToken = default);

    Task<CurrentKnowledge?> GetCurrentAsync(
        Guid logicalDocumentId,
        RepositoryAccessScope scope,
        IReadOnlyCollection<Guid> requestedStableSectionIds,
        CancellationToken cancellationToken = default);
}

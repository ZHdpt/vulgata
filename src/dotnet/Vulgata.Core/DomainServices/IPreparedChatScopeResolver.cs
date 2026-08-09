namespace Vulgata.Core.DomainServices;

public sealed record PreparedChatScopeBinding(
    Guid SystemId,
    Guid ConsumerRepositoryId,
    Guid ProviderRepositoryId)
{
    public IReadOnlyList<Guid> RepositoryIds { get; } =
        Array.AsReadOnly([ConsumerRepositoryId, ProviderRepositoryId]);
}

public interface IPreparedChatScopeResolver
{
    Task<PreparedChatScopeBinding?> ResolveAsync(
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default);
}

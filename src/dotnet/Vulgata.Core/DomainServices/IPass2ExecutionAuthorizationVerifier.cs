namespace Vulgata.Core.DomainServices;

/// <summary>
/// Revalidates the durable authorization subject for a prepared Pass 2 execution.
/// Implementations must use current server-owned Identity and System authority.
/// </summary>
public interface IPass2ExecutionAuthorizationVerifier
{
    ValueTask<bool> HasCurrentAccessAsync(
        string authorizationSubject,
        Guid systemId,
        IReadOnlyCollection<Guid> requiredRepositoryIds,
        CancellationToken cancellationToken = default);
}

namespace Vulgata.Core.DomainServices;

/// <summary>
/// Composes multi-level context in the fixed order: Global → System → Repository.
/// Standalone repositories skip the System level.
/// </summary>
public interface IContextCompositionService
{
    Task<string?> ComposeEffectiveContextAsync(
        Guid? systemId,
        Guid repositoryId,
        CancellationToken cancellationToken = default);
}

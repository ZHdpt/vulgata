namespace Vulgata.Shared.Explore;

public interface IExploreGraphService
{
    event Action? ContextChanged;
    Task<ExploreGraphResult> LoadGraphAsync(CancellationToken cancellationToken = default);
}

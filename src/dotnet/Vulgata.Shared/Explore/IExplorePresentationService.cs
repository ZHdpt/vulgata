using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Shared.Explore;

public interface IExplorePresentationService
{
    event Action? ContextChanged;
    Task<ExploreReadResult> OpenDocumentAsync(DocumentHandle document, CancellationToken cancellationToken = default);
    Task<ExploreReadResult> ContinueDocumentAsync(ExploreReadSession session, CancellationToken cancellationToken = default);
    Task<ExploreBrowseResult> BrowseAsync(ExploreBrowseSession? continuation = null, CancellationToken cancellationToken = default);
}

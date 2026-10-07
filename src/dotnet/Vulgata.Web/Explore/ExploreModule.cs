using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;
using Vulgata.Web.ViewModels.Explore;

namespace Vulgata.Web.Explore;

public static class ExploreModule
{
    public static IServiceCollection AddExploreReader(this IServiceCollection services)
    {
        services.TryAddScoped<IExploreContextProvider, UnavailableExploreContextProvider>();
        services.TryAddScoped<IExplorePresentationService>(provider => new ExplorePresentationService(
            provider.GetService<IOctoberKnowledgeReader>(), provider.GetRequiredService<IExploreContextProvider>(),
            provider.GetService<AuthenticationStateProvider>()));
        services.TryAddTransient<DocumentReaderViewModel>();
        services.TryAddTransient<DocumentBrowseViewModel>();
        return services;
    }

    private sealed class UnavailableExploreContextProvider : IExploreContextProvider
    {
        public UnavailableExploreContextProvider() { }
        public event Action? ContextChanged { add { } remove { } }
        public Task<ExploreContext?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult<ExploreContext?>(null);
    }
}

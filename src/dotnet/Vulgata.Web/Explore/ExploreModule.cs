using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;
using Vulgata.Web.ViewModels.Explore;

namespace Vulgata.Web.Explore;

public static class ExploreModule
{
    public static IServiceCollection AddExploreReader(this IServiceCollection services)
    {
        services.TryAddScoped<IExploreContextProvider, UnavailableExploreContextProvider>();
        services.TryAddScoped<ExplorePresentationService>(provider => new ExplorePresentationService(
            provider.GetService<IOctoberKnowledgeReader>(), provider.GetRequiredService<IExploreContextProvider>(),
            provider.GetService<AuthenticationStateProvider>(), provider.GetService<IOptions<IdentityOptions>>(),
            provider.GetService<IOctoberKnowledgeDisclosedReader>(), provider.GetService<IOctoberKnowledgeDependencyAuthority>(),
            provider.GetService<TimeProvider>()));
        services.TryAddScoped<IExplorePresentationService>(provider => provider.GetRequiredService<ExplorePresentationService>());
        services.TryAddScoped<IExploreGraphService>(provider => provider.GetRequiredService<ExplorePresentationService>());
        services.TryAddTransient<DocumentReaderViewModel>();
        services.TryAddTransient<DocumentBrowseViewModel>();
        services.TryAddTransient<KnowledgeGraphViewModel>();
        return services;
    }

    private sealed class UnavailableExploreContextProvider : IExploreContextProvider
    {
        public UnavailableExploreContextProvider() { }
        public event Action? ContextChanged { add { } remove { } }
        public Task<ExploreContext?> GetCurrentAsync(CancellationToken cancellationToken = default) => Task.FromResult<ExploreContext?>(null);
    }
}

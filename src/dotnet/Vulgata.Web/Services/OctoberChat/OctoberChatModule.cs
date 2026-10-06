using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Web.ViewModels.OctoberChat;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Vulgata.Web.Services.OctoberChat;

/// <summary>Opt-in feature services only. The integration owner supplies Knowledge services and host binding.</summary>
public static class OctoberChatModule
{
    public static IServiceCollection AddOctoberChat(this IServiceCollection services)
    {
        services.TryAddScoped<IOctoberChatSelectionService, OctoberChatSelectionService>();
        services.TryAddScoped<OctoberChatEntryViewModel>();
        return services;
    }
}

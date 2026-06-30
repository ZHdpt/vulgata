using Vulgata.Web.Api.Context;
using Vulgata.Web.Api.LlmProviders;
using Vulgata.Web.Api.Repositories;
using Vulgata.Web.Api.Systems;

namespace Vulgata.Web.Api;

internal static class ApiEndpointRegistrationExtensions
{
    public static void MapVulgataApiEndpoints(this WebApplication app)
    {
        app.MapSystemApiEndpoints();
        app.MapSystemLlmProviderOverrideApiEndpoints();
        app.MapRepositoryApiEndpoints();
        app.MapLlmProviderApiEndpoints();
        app.MapContextApiEndpoints();
    }
}

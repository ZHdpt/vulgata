using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;

namespace Vulgata.Web.Data;

/// <summary>Feature-local composition. The host owns activation, contexts and Data Protection keys.</summary>
public static class OctoberKnowledgeReadModule
{
    public static IServiceCollection AddOctoberKnowledgeReadAuthority(this IServiceCollection services,
        Func<CancellationToken, Task<ClaimsPrincipal?>> trustedCurrentPrincipal, TimeSpan? operationTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(trustedCurrentPrincipal);
        if (operationTimeout is { } timeout && (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30)))
            throw new ArgumentOutOfRangeException(nameof(operationTimeout));

        services.AddScoped<IOctoberKnowledgeLiveReadAuthority>(provider => new OctoberKnowledgeIdentityAdapter(
            provider.GetRequiredService<IServiceScopeFactory>(), trustedCurrentPrincipal, operationTimeout));
        services.AddScoped<IOctoberKnowledgeDisclosurePolicy, OctoberKnowledgeDisclosurePolicy>();
        services.AddSingleton<IOctoberKnowledgeTokenProtector, OctoberKnowledgeDataProtectionTokenProtector>();
        // The existing host supplies Identity, management contexts and durable Data Protection keys.
        // Registration neither opens October storage nor migrates or constructs generator services.
        return services;
    }
}

public sealed class OctoberKnowledgeDataProtectionTokenProtector : IOctoberKnowledgeTokenProtector
{
    private readonly IDataProtector _protector;

    public OctoberKnowledgeDataProtectionTokenProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector("Vulgata.October.Knowledge.Read.v1");
    }

    public string Protect(string value) => _protector.Protect(value);
    public string Unprotect(string value) => _protector.Unprotect(value);
}

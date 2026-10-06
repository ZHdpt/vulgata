using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Web.Data;

public static class OctoberHeadlessAuthorityRegistration
{
    public static IServiceCollection AddOctoberLocalAuthority(
        IServiceCollection services,
        OctoberAuthorityConfiguration configuration,
        IOctoberOperatorCredentialSource credentialSource,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(credentialSource);
        ArgumentNullException.ThrowIfNull(clock);

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.IdentityConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__IdentityMigrationsHistory", "identity");
                npgsql.CommandTimeout(Math.Max(1, (int)Math.Floor(
                    configuration.OperationTimeout.TotalSeconds)));
            }));
        services.AddDbContextFactory<VulgataDbContext>(options =>
            options.UseNpgsql(configuration.DataConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public")));

        services.AddIdentityCore<ApplicationUser>(IdentityOptionsConfiguration.Configure)
            .AddRoles<IdentityRole>()
            .AddErrorDescriber<ChineseIdentityErrorDescriber>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        services.Replace(ServiceDescriptor.Scoped<IPasswordHasher<ApplicationUser>,
            BcryptPasswordHasher<ApplicationUser>>());

        services.AddSingleton(configuration);
        services.AddSingleton(credentialSource);
        services.AddSingleton(clock);
        services.AddScoped<IOctoberLocalAuthority, OctoberLocalAuthority>();
        return services;
    }
}

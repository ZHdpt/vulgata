using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
using Vulgata.Web.Explore;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberWikiCompositionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("Enabled")]
    [InlineData("PublicationId")]
    [InlineData("RepositoryIds:0")]
    [InlineData("Mode")]
    public async Task Registration_IncompleteConfiguration_RemainsUnavailableWithoutConnectingAsync(string removed)
    {
        var binding = Binding();
        var config = Configuration(binding, Guid.NewGuid(), Path.GetTempPath());
        config["October:Wiki:" + removed] = null;
        var services = Services(new IntegrationAuthentication());
        int connections = 0;
        services.AddOctoberWikiReader(config, _ => { connections++; throw new InvalidOperationException("Must not connect"); });
        await using var provider = services.BuildServiceProvider();
        Assert.Null(await provider.GetRequiredService<IExploreContextProvider>().GetCurrentAsync());
        Assert.Equal(0, connections);
        AssertNoProducer(provider);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Context_FreezesConfigurationButRechecksPolicyAndAuthenticationAsync()
    {
        var publication = Binding();
        Guid repository = Guid.NewGuid();
        var config = Configuration(publication, repository, Path.GetTempPath());
        var auth = new IntegrationAuthentication();
        var policy = new B0bReaderDisclosure();
        var services = Services(auth);
        services.AddOctoberWikiReader(config, _ => throw new InvalidOperationException("Controlled context test must not connect"));
        services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(policy));
        await using var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<IExploreContextProvider>();
        config["October:Wiki:PublicationId"] = Guid.NewGuid().ToString();
        config["October:Wiki:Mode"] = "Business";
        config["October:Wiki:RepositoryIds:0"] = Guid.NewGuid().ToString();
        var first = Assert.IsType<ExploreContext>(await context.GetCurrentAsync());
        Assert.Equal(publication, first.Selection.Publication);
        Assert.Equal(KnowledgeMode.IT, first.Selection.Mode);
        Assert.Equal(repository, Assert.Single(first.Selection.RepositoryScope));
        Assert.False(first.IsControlledFixture);
        policy.Status = DisclosureStatus.Denied;
        Assert.Null(await context.GetCurrentAsync());
        policy.Status = DisclosureStatus.Allowed;
        int changed = 0;
        context.ContextChanged += () => changed++;
        auth.Set(new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.True(changed > 0);
        Assert.Null(await context.GetCurrentAsync());
        AssertNoProducer(provider);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Context_OrdinaryUserAllowedThenLiveRoleRevocationDeniedAsync()
    {
        await using var identity = await B0bIdentityFixture.CreateAsync();
        var auth = new IntegrationAuthentication(identity.Principal!);
        var services = Services(auth);
        services.AddOctoberWikiReader(Configuration(identity.Publication, identity.RepositoryId, Path.GetTempPath()), _ => throw new InvalidOperationException());
        services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(identity.Policy));
        await using var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<IExploreContextProvider>();
        Assert.NotNull(await context.GetCurrentAsync());
        await using var scope = identity.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = Assert.IsType<ApplicationUser>(await users.FindByIdAsync(identity.Subject.SubjectId));
        Assert.True((await users.RemoveFromRoleAsync(user, "User")).Succeeded);
        Assert.Null(await context.GetCurrentAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Context_PreCancelled_DoesNotReturnAnAuthorizedContextAsync()
    {
        var services = Services(new IntegrationAuthentication());
        services.AddOctoberWikiReader(Configuration(Binding(), Guid.NewGuid(), Path.GetTempPath()), _ => throw new InvalidOperationException());
        services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(new B0bReaderDisclosure()));
        await using var provider = services.BuildServiceProvider();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IExploreContextProvider>().GetCurrentAsync(new CancellationToken(true)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Registration_RealIdentityAdapterChecksOrdinaryUserAndCurrentSecurityStampAsync()
    {
        await using var identity = await B0bIdentityFixture.CreateAsync();
        await using var identityContext = await identity.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        await using var managementContext = await identity.Services.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
        var auth = new IntegrationAuthentication(identity.Principal!);
        var services = Services(auth);
        services.AddHttpContextAccessor();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme);
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(identityContext.Database.GetConnectionString()));
        services.AddDbContextFactory<VulgataDbContext>(options => options.UseSqlite(managementContext.Database.GetConnectionString()));
        services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
            .AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddSignInManager();
        services.AddOctoberWikiReader(Configuration(identity.Publication, identity.RepositoryId, Path.GetTempPath()), _ => throw new InvalidOperationException("Selection fixture is explicitly controlled"));
        services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeSelectionAuthority>(identity.Selection));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<OctoberKnowledgeIdentityAdapter>(scope.ServiceProvider.GetRequiredService<IOctoberKnowledgeLiveReadAuthority>());
        Assert.IsType<OctoberKnowledgeDisclosurePolicy>(scope.ServiceProvider.GetRequiredService<IOctoberKnowledgeDisclosurePolicy>());
        var context = scope.ServiceProvider.GetRequiredService<IExploreContextProvider>();
        Assert.NotNull(await context.GetCurrentAsync());
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = Assert.IsType<ApplicationUser>(await users.FindByIdAsync(identity.Subject.SubjectId));
        Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
        Assert.Null(await context.GetCurrentAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Context_AuthenticationChangesWhilePolicyPending_DiscardsGrantAsync()
    {
        var auth = new IntegrationAuthentication();
        var services = Services(auth);
        var policy = new PendingIntegrationPolicy();
        services.AddOctoberWikiReader(Configuration(Binding(), Guid.NewGuid(), Path.GetTempPath()), _ => throw new InvalidOperationException());
        services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(policy));
        await using var provider = services.BuildServiceProvider();
        var pending = provider.GetRequiredService<IExploreContextProvider>().GetCurrentAsync();
        await policy.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        auth.Set(new ClaimsPrincipal(new ClaimsIdentity()));
        policy.Complete.SetResult(DisclosureStatus.Allowed);
        Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("duplicate")]
    [InlineData("malformed")]
    [InlineData("empty")]
    public async Task Context_ExplicitScope_DoesNotExpandOrAcceptMalformedMembersAsync(string variant)
    {
        Guid repository = Guid.NewGuid();
        var config = Configuration(Binding(), repository, Path.GetTempPath());
        if (variant == "duplicate") config["October:Wiki:RepositoryIds:1"] = repository.ToString();
        else if (variant == "malformed") config["October:Wiki:RepositoryIds:0"] = "not-a-repository";
        else
        {
            // A scalar empty value is the explicit cleared-scope syntax; a missing setting is invalid.
            config = new ConfigurationBuilder().AddInMemoryCollection(config.AsEnumerable()
                .Where(pair => pair.Key != "October:Wiki:RepositoryIds:0")).Build();
            config["October:Wiki:RepositoryIds"] = "";
        }
        var services = Services(new IntegrationAuthentication());
        services.AddOctoberWikiReader(config, _ => throw new InvalidOperationException());
        services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(new B0bReaderDisclosure()));
        await using var provider = services.BuildServiceProvider();
        var context = await provider.GetRequiredService<IExploreContextProvider>().GetCurrentAsync();
        if (variant == "empty") Assert.Empty(Assert.IsType<ExploreContext>(context).Selection.RepositoryScope);
        else Assert.Null(context);
    }

    internal static ServiceCollection Services(IntegrationAuthentication auth)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<IdentityOptions>();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<AuthenticationStateProvider>(auth);
        return services;
    }

    internal static IConfigurationRoot Configuration(PublicationBinding publication, Guid repository, string logDirectory) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["October:Wiki:Enabled"] = "true",
            ["October:Wiki:CanonicalDatabaseIdentity"] = publication.Knowledge.CanonicalDatabaseIdentity,
            ["October:Wiki:TargetId"] = publication.Knowledge.TargetId,
            ["October:Wiki:BatchId"] = publication.Knowledge.BatchId.ToString(),
            ["October:Wiki:SelectionDigest"] = publication.Knowledge.SelectionDigest,
            ["October:Wiki:PublicationId"] = publication.PublicationId.ToString(),
            ["October:Wiki:LogDirectory"] = logDirectory,
            ["October:Wiki:Mode"] = "IT",
            ["October:Wiki:RepositoryIds:0"] = repository.ToString()
        }).Build();

    private static PublicationBinding Binding() => new(new("controlled-db", "controlled-target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid());
    internal static void AssertNoProducer(IServiceProvider provider)
    {
        Assert.Null(provider.GetService<IOctoberKnowledgeWriter>());
        Assert.Null(provider.GetService<IOctoberBusinessFinalizer>());
        Assert.Null(provider.GetService<IOctoberExecutionScope>());
    }
}

internal sealed class IntegrationAuthentication : AuthenticationStateProvider
{
    private ClaimsPrincipal _principal;
    public IntegrationAuthentication(ClaimsPrincipal? principal = null) => _principal = principal ?? new ClaimsPrincipal(new ClaimsIdentity(
        [new(ClaimTypes.NameIdentifier, "ordinary-user"), new("AspNet.Identity.SecurityStamp", "controlled-stamp"), new(ClaimTypes.Role, "User")], "ControlledIdentity"));
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(_principal));
    public void Set(ClaimsPrincipal principal) { _principal = principal; NotifyAuthenticationStateChanged(GetAuthenticationStateAsync()); }
}

internal sealed class PendingIntegrationPolicy : IOctoberKnowledgeDisclosurePolicy
{
    public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<DisclosureStatus> Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default)
    {
        Entered.TrySetResult(true);
        return Complete.Task.WaitAsync(cancellationToken);
    }
}

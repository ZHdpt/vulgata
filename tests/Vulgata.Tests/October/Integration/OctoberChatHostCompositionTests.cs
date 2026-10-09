using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberDialogue;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;
using Vulgata.Web.Explore;
using Vulgata.Web.Services.OctoberChat;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberChatHostCompositionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("provider")]
    [InlineData("request-zero")]
    [InlineData("request-high")]
    [InlineData("execution-zero")]
    [InlineData("execution-high")]
    public async Task ValidWiki_InvalidChatConfiguration_KeepsInertOctoberApplicationWithoutConnectionAsync(string variant)
    {
        var publication = new PublicationBinding(new("controlled", "target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid());
        var config = OctoberWikiCompositionTests.Configuration(publication, Guid.NewGuid(), Path.GetTempPath());
        EnableChat(config);
        switch (variant)
        {
            case "missing": config["October:Chat:Enabled"] = null; break;
            case "disabled": config["October:Chat:Enabled"] = "false"; break;
            case "provider": config["October:Chat:ProviderId"] = "not-a-guid"; break;
            case "request-zero": config["October:Chat:MaximumConcurrentRequests"] = "0"; break;
            case "request-high": config["October:Chat:MaximumConcurrentRequests"] = "5"; break;
            case "execution-zero": config["October:Chat:MaximumConcurrentExecutions"] = "0"; break;
            case "execution-high": config["October:Chat:MaximumConcurrentExecutions"] = "17"; break;
        }
        var services = OctoberWikiCompositionTests.Services(new IntegrationAuthentication());
        int connections = 0;
        services.AddOctoberWikiReader(config, _ => { connections++; throw new InvalidOperationException("No connection allowed."); });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OctoberChatApplication>());
        Assert.Null(scope.ServiceProvider.GetService<OctoberChatHostScopeFactory>());
        Assert.Contains(services, item => item.ServiceType == typeof(IOctoberChatContextProvider));
        Assert.Equal(0, connections);
        OctoberWikiCompositionTests.AssertNoProducer(scope.ServiceProvider);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task MissingWikiAndNoChatIntent_DoesNotActivateOctoberApplicationAsync()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = OctoberWikiCompositionTests.Services(new IntegrationAuthentication());
        services.AddOctoberWikiReader(config, _ => throw new InvalidOperationException("No connection allowed."));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        Assert.Null(scope.ServiceProvider.GetService<OctoberChatApplication>());
        Assert.Null(scope.ServiceProvider.GetService<OctoberChatHostScopeFactory>());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task MissingWikiWithExplicitChatIntent_KeepsInertApplicationInsteadOfLegacyFallbackAsync()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        EnableChat(config);
        var services = OctoberWikiCompositionTests.Services(new IntegrationAuthentication());
        int connections = 0;
        services.AddOctoberWikiReader(config, _ => { connections++; throw new InvalidOperationException("No connection allowed."); });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OctoberChatApplication>());
        Assert.Null(scope.ServiceProvider.GetService<OctoberChatHostScopeFactory>());
        Assert.Equal(0, connections);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Factory_OrdinaryUserGetsDistinctTurnOwnersAndSharedHostLimitsWithoutProviderAccessAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var circuit = fixture.Services.CreateAsyncScope();
        var factory = circuit.ServiceProvider.GetRequiredService<OctoberChatHostScopeFactory>();
        await using var first = Assert.IsType<OctoberChatTurnScope>(await factory.CreateAsync(fixture.Request()));
        await using var second = Assert.IsType<OctoberChatTurnScope>(await factory.CreateAsync(fixture.Request()));
        Assert.IsType<OctoberDialogueStore>(first.Store);
        Assert.IsType<OctoberDialogueStore>(second.Store);
        Assert.NotSame(first.Store, second.Store);
        Assert.NotSame(first.Providers, second.Providers);
        Assert.Same(first.Codec, second.Codec);
        Assert.IsType<OctoberDialoguePayloadCodec>(first.Codec);
        Assert.Same(first.Requests, second.Requests);
        Assert.Same(first.ExecutionAuthority, second.ExecutionAuthority);
        Assert.Same(circuit.ServiceProvider.GetRequiredService<OctoberChatExecutionRegistry>(), first.ExecutionAuthority);
        Assert.Same(circuit.ServiceProvider.GetRequiredService<OctoberChatRequestAdmission>(), first.Requests);
        Assert.Same(first.Reader, first.DisclosedReader);
        Assert.IsType<OctoberKnowledgeDependencyAuthority>(first.Dependencies);
        AssertSelection(fixture.Request(), await first.Context.GetCurrentAsync());
        Assert.Equal(1, fixture.AuthenticationResolutions);
        Assert.Equal(0, fixture.Contexts.Created);
        Assert.Equal(0, fixture.Secrets.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("publication")]
    [InlineData("scope")]
    [InlineData("purpose")]
    [InlineData("mode")]
    [InlineData("subject")]
    [InlineData("stamp")]
    public async Task Factory_ForeignOrInvalidSelection_IsRejectedBeforeKnowledgeOrProviderAccessAsync(string variant)
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var circuit = fixture.Services.CreateAsyncScope();
        var request = fixture.Request();
        request = variant switch
        {
            "publication" => request with { Publication = request.Publication with { PublicationId = Guid.NewGuid() } },
            "scope" => request with { RepositoryScope = [Guid.NewGuid()] },
            "purpose" => request with { Purpose = KnowledgePurpose.Reader },
            "mode" => request with { Mode = (KnowledgeMode)999 },
            "subject" => request with { Subject = request.Subject with { SubjectId = "another-user" } },
            _ => request with { Subject = request.Subject with { AuthenticationBinding = "another-stamp" } }
        };
        Assert.Null(await circuit.ServiceProvider.GetRequiredService<OctoberChatHostScopeFactory>().CreateAsync(request));
        Assert.Equal(0, fixture.Contexts.Created);
        Assert.Equal(0, fixture.Secrets.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("anonymous")]
    [InlineData("another-user")]
    [InlineData("stamp-rotation")]
    public async Task TurnContext_OriginatingAuthenticationRevocation_DoesNotAdoptAnotherIdentityAsync(string change)
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var circuit = fixture.Services.CreateAsyncScope();
        await using var turn = Assert.IsType<OctoberChatTurnScope>(await circuit.ServiceProvider
            .GetRequiredService<OctoberChatHostScopeFactory>().CreateAsync(fixture.Request()));
        Assert.NotNull(await turn.Context.GetCurrentAsync());
        if (change == "stamp-rotation")
        {
            await using var scope = fixture.Identity.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByIdAsync(fixture.Identity.Subject.SubjectId));
            Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
        }
        else
        {
            fixture.Authentication.Set(change == "anonymous" ? new ClaimsPrincipal(new ClaimsIdentity()) :
                new ClaimsPrincipal(new ClaimsIdentity([
                    new(ClaimTypes.NameIdentifier, "another-user"), new("AspNet.Identity.SecurityStamp", "another-stamp")], "Controlled")));
        }
        Assert.Null(await turn.Context.GetCurrentAsync());
        Assert.Equal(1, fixture.AuthenticationResolutions);
        Assert.Equal(0, fixture.Contexts.Created);
        Assert.Equal(0, fixture.Secrets.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Factory_ConfirmedModeAndFrozenSelection_KeepExplorerReaderIndependentAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var circuit = fixture.Services.CreateAsyncScope();
        var factory = circuit.ServiceProvider.GetRequiredService<OctoberChatHostScopeFactory>();
        // This is the SAME scoped context the registration passes to the application,
        // not just the independently constructed turn's context.
        var attachment = circuit.ServiceProvider.GetRequiredService<IOctoberChatContextProvider>();
        fixture.Configuration["October:Wiki:PublicationId"] = Guid.NewGuid().ToString();
        fixture.Configuration["October:Wiki:RepositoryIds:0"] = Guid.NewGuid().ToString();
        await using var it = Assert.IsType<OctoberChatTurnScope>(await factory.CreateAsync(fixture.Request()));
        AssertSelection(fixture.Request(), await attachment.GetCurrentAsync());
        await using var business = Assert.IsType<OctoberChatTurnScope>(await factory.CreateAsync(fixture.Request(KnowledgeMode.Business)));
        AssertSelection(fixture.Request(), await it.Context.GetCurrentAsync());
        AssertSelection(fixture.Request(KnowledgeMode.Business), await business.Context.GetCurrentAsync());
        AssertSelection(fixture.Request(KnowledgeMode.Business), await attachment.GetCurrentAsync());
        var reader = Assert.IsType<ExploreContext>(await circuit.ServiceProvider.GetRequiredService<IExploreContextProvider>().GetCurrentAsync());
        Assert.Equal(KnowledgePurpose.Reader, reader.Selection.Purpose);
        Assert.Equal(KnowledgeMode.IT, reader.Selection.Mode);
        Assert.Equal(fixture.Identity.Publication, reader.Selection.Publication);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Factory_RejectedModeSelection_DoesNotRetargetApplicationAttachmentAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var circuit = fixture.Services.CreateAsyncScope();
        var factory = circuit.ServiceProvider.GetRequiredService<OctoberChatHostScopeFactory>();
        var attachment = circuit.ServiceProvider.GetRequiredService<IOctoberChatContextProvider>();
        await using var business = Assert.IsType<OctoberChatTurnScope>(await factory.CreateAsync(fixture.Request(KnowledgeMode.Business)));
        AssertSelection(fixture.Request(KnowledgeMode.Business), await attachment.GetCurrentAsync());
        var foreign = fixture.Request() with { Subject = fixture.Identity.Subject with { SubjectId = "foreign-user" } };
        Assert.Null(await factory.CreateAsync(foreign));
        AssertSelection(fixture.Request(KnowledgeMode.Business), await attachment.GetCurrentAsync());
        fixture.Identity.Selection.Fail = true;
        Assert.Null(await factory.CreateAsync(fixture.Request()));
        fixture.Identity.Selection.Fail = false;
        AssertSelection(fixture.Request(KnowledgeMode.Business), await attachment.GetCurrentAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task TurnDisposal_ClosesSameCanonicalStoreAndUnsubscribesLiveContextAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var circuit = fixture.Services.CreateAsyncScope();
        var turn = Assert.IsType<OctoberChatTurnScope>(await circuit.ServiceProvider
            .GetRequiredService<OctoberChatHostScopeFactory>().CreateAsync(fixture.Request()));
        var store = turn.Store;
        var context = turn.Context;
        int changed = 0;
        context.ContextChanged += () => changed++;
        await turn.DisposeAsync();
        int afterDisposal = changed;
        fixture.Authentication.Set(new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.Equal(afterDisposal, changed);
        Assert.Null(await context.GetCurrentAsync());
        var result = await store.OpenConversationAsync(new(Guid.NewGuid(), Guid.NewGuid(), null), fixture.Identity.Subject);
        Assert.Equal(DialogueStatus.Indeterminate, result.Status);
        Assert.Equal(0, fixture.Contexts.Created);
        await turn.DisposeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Factory_PreCancelledToken_ThrowsWithoutCreatingTurnAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var circuit = fixture.Services.CreateAsyncScope();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => circuit.ServiceProvider
            .GetRequiredService<OctoberChatHostScopeFactory>().CreateAsync(fixture.Request(), new CancellationToken(true)));
        Assert.Equal(0, fixture.Contexts.Created);
        Assert.Equal(0, fixture.Secrets.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttachmentContext_ReplacementOrDisposalDuringAuthorization_DiscardsLateGrantAsync(bool dispose)
    {
        var publication = new PublicationBinding(new("controlled", "target", Guid.NewGuid(), new string('a', 64)), Guid.NewGuid());
        Guid repository = Guid.NewGuid();
        var selection = new OctoberWikiSelection(publication, KnowledgeMode.IT, [repository], Path.GetTempPath());
        var authentication = new IntegrationAuthentication();
        var policy = new PendingIntegrationPolicy();
        using var attachment = new OctoberChatAttachmentContext(selection, authentication,
            Options.Create(new IdentityOptions()), policy);
        int changed = 0;
        attachment.ContextChanged += () => changed++;
        var pending = attachment.GetCurrentAsync();
        var replacement = new ReadSelectionRequest(publication, new("ordinary-user", "controlled-stamp"),
            KnowledgeMode.Business, KnowledgePurpose.Chat, [repository]);
        try
        {
            await policy.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (dispose) attachment.Dispose();
            else attachment.BindOperation(replacement);
            Assert.Equal(0, changed); // Deliberate selection is not an authentication change.
        }
        finally
        {
            policy.Complete.TrySetResult(DisclosureStatus.Allowed);
        }

        Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        if (dispose) Assert.Null(await attachment.GetCurrentAsync());
        else AssertSelection(replacement, await attachment.GetCurrentAsync());
        authentication.Set(new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.Equal(dispose ? 0 : 1, changed);
        Assert.Null(await attachment.GetCurrentAsync());
    }

    private static void EnableChat(IConfiguration configuration)
    {
        configuration["October:Chat:Enabled"] = "true";
        configuration["October:Chat:ProviderId"] = Guid.NewGuid().ToString();
    }

    private static void AssertSelection(ReadSelectionRequest expected, ReadSelectionRequest? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Publication, actual.Publication);
        Assert.Equal(expected.Subject, actual.Subject);
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.Purpose, actual.Purpose);
        Assert.Equal(expected.RepositoryScope.ToArray(), actual.RepositoryScope.ToArray());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required B0bIdentityFixture Identity { get; init; }
        public required IntegrationAuthentication Authentication { get; init; }
        public required IConfigurationRoot Configuration { get; init; }
        public ServiceProvider Services { get; private set; } = null!;
        public InertContexts Contexts { get; } = new();
        public UnusedSecrets Secrets { get; } = new();
        public int AuthenticationResolutions { get; private set; }
        public ReadSelectionRequest Request(KnowledgeMode mode = KnowledgeMode.IT) =>
            new(Identity.Publication, Identity.Subject, mode, KnowledgePurpose.Chat, [Identity.RepositoryId]);

        public static async Task<Fixture> CreateAsync()
        {
            var identity = await B0bIdentityFixture.CreateAsync();
            var fixture = new Fixture
            {
                Identity = identity,
                Authentication = new IntegrationAuthentication(identity.Principal!),
                Configuration = OctoberWikiCompositionTests.Configuration(identity.Publication, identity.RepositoryId, Path.GetTempPath())
            };
            try
            {
                EnableChat(fixture.Configuration);
                await using var identityDb = await identity.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
                await using var managementDb = await identity.Services.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
                string identityConnection = identityDb.Database.GetConnectionString()!;
                string managementConnection = managementDb.Database.GetConnectionString()!;
                var services = OctoberWikiCompositionTests.Services(fixture.Authentication);
                services.RemoveAll<AuthenticationStateProvider>();
                services.AddScoped<AuthenticationStateProvider>(_ => ++fixture.AuthenticationResolutions == 1
                    ? fixture.Authentication : new IntegrationAuthentication(new ClaimsPrincipal(new ClaimsIdentity())));
                services.AddHttpContextAccessor();
                services.AddAuthentication(IdentityConstants.ApplicationScheme).AddCookie(IdentityConstants.ApplicationScheme);
                services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(identityConnection));
                services.AddDbContextFactory<VulgataDbContext>(options => options.UseSqlite(managementConnection));
                services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
                    .AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddSignInManager();
                services.AddSingleton<ISecretEncryptionService>(fixture.Secrets);
                services.AddOctoberWikiReader(fixture.Configuration, _ => throw new InvalidOperationException("No PostgreSQL connection allowed."));
                services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeSelectionAuthority>(identity.Selection));
                services.Replace(ServiceDescriptor.Singleton<IOctoberPublishedKnowledgeSource>(
                    new B0bPublishedSource(new(identity.Publication, [], [], [], []))));
                services.RemoveAll<IDbContextFactory<OctoberDbContext>>();
                services.AddSingleton<IDbContextFactory<OctoberDbContext>>(fixture.Contexts);
                fixture.Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                return fixture;
            }
            catch { await identity.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            try { await Services.DisposeAsync(); }
            finally { await Identity.DisposeAsync(); }
        }
    }

    private sealed class InertContexts : IDbContextFactory<OctoberDbContext>
    {
        public int Created { get; private set; }
        public OctoberDbContext CreateDbContext()
        {
            Created++;
            return new(new DbContextOptionsBuilder<OctoberDbContext>().UseSqlite("Data Source=:memory:").Options);
        }
        public Task<OctoberDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class UnusedSecrets : ISecretEncryptionService
    {
        public int Calls { get; private set; }
        public Task<string> EncryptAsync(string purpose, string plain, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("No credential access allowed.");
        }
        public Task<string> DecryptAsync(string purpose, string cipher, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("No credential access allowed.");
        }
    }
}

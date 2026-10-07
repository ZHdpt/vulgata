using Bunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Components.Pages.Explore;
using Vulgata.Web.Explore;
using Vulgata.Web.ViewModels.Explore;

namespace Vulgata.Tests.Explore;

public sealed class ExploreCompositionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddExploreReader_ConfiguredIdentityClaimNames_AreUsedForSubjectAndStampAsync(bool mismatchedStamp)
    {
        var host = new ExploreFixtureHost();
        var claims = new List<Claim>
        {
            new("vulgata-subject", "ordinary-user"),
            new("vulgata-stamp", mismatchedStamp ? "stale-stamp" : "controlled-stamp"),
        };
        // A legacy claim cannot bypass the configured stamp check. The success case has only custom names.
        if (mismatchedStamp) { claims.Add(new(ClaimTypes.NameIdentifier, "ordinary-user")); }
        host.Authentication.User = new(new ClaimsIdentity(claims, "ControlledFixture"));
        var services = new ServiceCollection();
        services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.UserIdClaimType = "vulgata-subject";
            options.ClaimsIdentity.SecurityStampClaimType = "vulgata-stamp";
        });
        services.AddSingleton<IOctoberKnowledgeReader>(host.Reader);
        services.AddSingleton<IExploreContextProvider>(host.Context);
        services.AddSingleton<AuthenticationStateProvider>(host.Authentication);
        services.AddExploreReader();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var explorer = scope.ServiceProvider.GetRequiredService<IExplorePresentationService>();
        var result = await explorer.OpenDocumentAsync(host.Fixture.Handle(host.Fixture.ClA));
        if (mismatchedStamp)
        {
            Assert.NotEqual(KnowledgeReadStatus.Success, result.Page.Status);
            Assert.Null(result.Page.Document);
            Assert.Null(result.Page.Content);
            Assert.Empty(result.Page.Claims);
            Assert.Null(result.Page.Coverage);
            Assert.Null(result.Session);
            Assert.Empty(host.Reader.Reads);
        }
        else
        {
            Assert.Equal(KnowledgeReadStatus.Success, result.Page.Status);
            Assert.Equal(host.Fixture.Handle(host.Fixture.ClA), result.Page.Document!.Handle);
            Assert.Single(host.Reader.Reads);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AddExploreReader_ControlledOwnerServices_ResolvesActualFacadeAndReaderAsync()
    {
        var host = new ExploreFixtureHost();
        var services = new ServiceCollection();
        services.AddSingleton<IOctoberKnowledgeReader>(host.Reader);
        services.AddSingleton<IExploreContextProvider>(host.Context);
        services.AddSingleton<AuthenticationStateProvider>(host.Authentication);
        services.AddExploreReader();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<DocumentReaderViewModel>();
        Assert.IsType<ExplorePresentationService>(scope.ServiceProvider.GetRequiredService<IExplorePresentationService>());
        await vm.OpenAsync(host.Fixture.Handle(host.Fixture.ClA));
        Assert.NotNull(vm.Page);
        Assert.Single(host.Reader.Reads);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AddExploreReader_MissingWikiDependencies_ResolvesSafelyWithoutStartupAcquisitionAsync()
    {
        var services = new ServiceCollection();
        services.AddExploreReader();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var fixture = new ExploreFixtureHost();
        var vm = scope.ServiceProvider.GetRequiredService<DocumentReaderViewModel>();
        await vm.OpenAsync(fixture.Fixture.Handle(fixture.Fixture.ClA));
        Assert.Null(vm.Page);
        Assert.Null(vm.Session);
        Assert.False(vm.CanContinue);
        Assert.NotEmpty(vm.StatusText);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IOctoberKnowledgeReader));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void DocumentsPage_WithoutModule_RendersUnavailableInsteadOfThrowing()
    {
        using var context = new BunitContext();
        var cut = context.Render<DocumentsPage>();
        Assert.Contains("不可用", cut.Markup, StringComparison.Ordinal);
    }
}

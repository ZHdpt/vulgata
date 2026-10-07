using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
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

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Web.Components.Pages.Management;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class DemoScanPageTests : TestContext
{
    [Fact]
    public void DemoScanPage_OpenedForScanningRepository_CompletesStateAndHostsAnimation()
    {
        var state = new DemoScanUiState(TimeProvider.System);
        var repositoryId = Guid.NewGuid();
        state.Start(repositoryId);
        Services.AddSingleton(state);

        var cut = Render<DemoScanPage>(parameters => parameters
            .Add(p => p.RepositoryId, repositoryId));

        Assert.Equal(
            DemoRepositoryScanPhase.Completed,
            state.GetSnapshot(repositoryId).Phase);
        Assert.Equal(
            "/demo/vulgata-scanning.html",
            cut.Find("iframe").GetAttribute("src"));
        Assert.Equal("/management", cut.Find("a").GetAttribute("href"));
        Assert.Contains("返回管理后台", cut.Markup, StringComparison.Ordinal);
    }
}

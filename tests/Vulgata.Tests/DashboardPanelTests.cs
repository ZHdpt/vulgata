using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared.Repositories;
using Vulgata.Web.Components.Management.Databases;
using Vulgata.Web.Components.Management.Repositories;
using Vulgata.Web.Components.Management.Systems;
using Vulgata.Web.Data;
using Vulgata.Web.RequestPipeline;
using Vulgata.Web.Validators;
using SystemEntity = Vulgata.Core.Entities.System;
using FluentValidation;

namespace Vulgata.Tests;

/// <summary>
/// bunit component tests for the DashboardPage panels extracted in PR4. Each panel is
/// rendered in isolation with mock data; we assert that rows render and that EventCallbacks
/// fire back to the parent with the right payload. These complement the existing
/// WebApplicationFactory integration tests, which still cover the wired-up dashboard.
/// </summary>
public sealed class DashboardPanelTests : TestContext
{
    public DashboardPanelTests()
    {
        // FluentUI components are used by every panel; bunit needs the services registered.
        Services.AddFluentUIComponents();
        Services.AddValidatorsFromAssemblyContaining<UpsertDatabaseConnectionRequestValidator>();
        // FluentUI web components invoke JS interop during render (e.g. FluentTextField, InputSelect).
        // bunit's default JSRuntime mode is strict and throws on unhandled calls; switch to loose
        // mode so those calls are silently stubbed instead of failing the render.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void SystemLlmOverridesPanel_RendersAllAgentTypes_AndFallsBackToGlobalDefault()
    {
        Guid systemId = Guid.NewGuid();
        IReadOnlyList<SystemLlmProviderOverrideSummaryDto> overrides =
        [
            new() { Id = Guid.NewGuid(), SystemId = systemId, AgentType = (int)AgentType.Orchestrator, ProviderName = "DeepSeek V4" },
        ];

        IRenderedComponent<SystemLlmOverridesPanel> cut = RenderComponent<SystemLlmOverridesPanel>(parameters => parameters
            .Add(p => p.SystemId, systemId)
            .Add(p => p.SystemName, "测试系统")
            .Add(p => p.Overrides, overrides));

        Assert.Contains("LLM Provider 覆盖", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("编排代理", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("工作代理", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("对话代理", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("DeepSeek V4", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("使用全局默认提供商", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void StandaloneReposPanel_RendersRepositoryRows_AndEmptyState()
    {
        IReadOnlyList<RepositorySummaryDto> repositories =
        [
            new() { Id = Guid.NewGuid(), Name = "共享组件库", ScanStatus = "未扫描", DocumentCount = 0 },
            new() { Id = Guid.NewGuid(), Name = "团队工具库", ScanStatus = "已扫描", DocumentCount = 5 },
        ];

        IRenderedComponent<StandaloneReposPanel> cut = RenderComponent<StandaloneReposPanel>(parameters => parameters
            .Add(p => p.Repositories, repositories));

        Assert.Contains("独立仓库", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("+ 新建独立仓库", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("共享组件库", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("团队工具库", cut.Markup, StringComparison.Ordinal);

        // Empty state path
        IRenderedComponent<StandaloneReposPanel> empty = RenderComponent<StandaloneReposPanel>(parameters => parameters
            .Add(p => p.Repositories, []));

        Assert.Contains("暂无独立仓库", empty.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void StandaloneReposPanel_OnCreateRequested_FiresEventCallback()
    {
        bool fired = false;
        IRenderedComponent<StandaloneReposPanel> cut = RenderComponent<StandaloneReposPanel>(parameters => parameters
            .Add(p => p.Repositories, [])
            .Add(p => p.OnCreateRequested, EventCallback.Factory.Create(this, () => fired = true)));

        // FluentButton renders as a <fluent-button> web component; find by visible text instead of tag name.
        IRefreshableElementCollection<AngleSharp.Dom.IElement> buttons = cut.FindAll("button, fluent-button");
        buttons.Single(button => button.TextContent.Contains("+ 新建独立仓库", StringComparison.Ordinal)).Click();

        Assert.True(fired);
    }

    [Fact]
    public void StandaloneReposPanel_OnRepositorySelected_FiresWithRepositoryId()
    {
        Guid repositoryId = Guid.NewGuid();
        Guid? capturedId = null;
        IReadOnlyList<RepositorySummaryDto> repositories =
        [
            new() { Id = repositoryId, Name = "可点击仓库", ScanStatus = "未扫描" },
        ];

        IRenderedComponent<StandaloneReposPanel> cut = RenderComponent<StandaloneReposPanel>(parameters => parameters
            .Add(p => p.Repositories, repositories)
            .Add(p => p.OnRepositorySelected, EventCallback.Factory.Create<Guid>(this, id => capturedId = id)));

        IRefreshableElementCollection<AngleSharp.Dom.IElement> buttons = cut.FindAll("button, fluent-button");
        buttons.Single(button => button.TextContent.Contains("查看", StringComparison.Ordinal)).Click();

        Assert.Equal(repositoryId, capturedId);
    }

    [Fact]
    public void SystemListPanel_RendersSystems_AndRepositoriesPerSystem()
    {
        Guid alphaId = Guid.NewGuid();
        Guid betaId = Guid.NewGuid();
        SystemEntity alpha = new("Alpha 平台", "描述一", "上下文一", DateTimeOffset.UtcNow);
        SystemEntity beta = new("Beta 平台", null, null, DateTimeOffset.UtcNow);

        // Use reflection to set the ids so the lookup dictionaries line up (System.Id has a private setter).
        typeof(SystemEntity).GetProperty(nameof(SystemEntity.Id))!.SetValue(alpha, alphaId);
        typeof(SystemEntity).GetProperty(nameof(SystemEntity.Id))!.SetValue(beta, betaId);

        IReadOnlyDictionary<Guid, IReadOnlyList<RepositorySummaryDto>> repositoriesBySystem = new Dictionary<Guid, IReadOnlyList<RepositorySummaryDto>>
        {
            [alphaId] = [new() { Id = Guid.NewGuid(), Name = "Alpha 主仓库", ScanStatus = "未扫描" }],
            [betaId] = [],
        };

        IReadOnlyDictionary<Guid, IReadOnlyList<SystemLlmProviderOverrideSummaryDto>> overridesBySystem = new Dictionary<Guid, IReadOnlyList<SystemLlmProviderOverrideSummaryDto>>
        {
            [alphaId] = [],
            [betaId] = [],
        };

        IRenderedComponent<SystemListPanel> cut = RenderComponent<SystemListPanel>(parameters => parameters
            .Add(p => p.Systems, (IReadOnlyList<SystemEntity>)[alpha, beta])
            .Add(p => p.RepositoriesBySystem, repositoriesBySystem)
            .Add(p => p.LlmOverridesBySystem, overridesBySystem)
            .Add(p => p.IsAdministrator, false));

        Assert.Contains("Alpha 平台", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Alpha 主仓库", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Beta 平台", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("暂无仓库", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("+ 新建仓库", cut.Markup, StringComparison.Ordinal);
        // Non-admin path: edit / manage owners / delete buttons must NOT render
        Assert.DoesNotContain("管理所有者", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemListPanel_EmptySystems_RendersEmptyState()
    {
        IRenderedComponent<SystemListPanel> cut = RenderComponent<SystemListPanel>(parameters => parameters
            .Add(p => p.Systems, [])
            .Add(p => p.IsAdministrator, false));

        Assert.Contains("尚未添加任何系统。创建你的第一个系统开始扫描。", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemListPanel_Administrator_RendersEditAndDeleteButtons()
    {
        SystemEntity system = new("管理平台", "描述", "上下文", DateTimeOffset.UtcNow);

        IRenderedComponent<SystemListPanel> cut = RenderComponent<SystemListPanel>(parameters => parameters
            .Add(p => p.Systems, (IReadOnlyList<SystemEntity>)[system])
            .Add(p => p.RepositoriesBySystem, new Dictionary<Guid, IReadOnlyList<RepositorySummaryDto>>())
            .Add(p => p.LlmOverridesBySystem, new Dictionary<Guid, IReadOnlyList<SystemLlmProviderOverrideSummaryDto>>())
            .Add(p => p.IsAdministrator, true));

        Assert.Contains("编辑", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("管理所有者", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("删除", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryDetailDialog_WhenVisible_RendersRepositoryNameAndDatabaseConnectionSection()
    {
        Services.AddScoped<IRepositoryDatabaseConnectionCoordinator>(_ => new StubDatabaseConnectionCoordinator());

        RepositorySummaryDto model = new() { Id = Guid.NewGuid(), Name = "订单仓库" };

        IRenderedComponent<RepositoryDetailDialog> cut = RenderComponent<RepositoryDetailDialog>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.Visible, true)
            .Add(p => p.User, CreateUserContext(isAdministrator: true)));

        // The dialog loads the database connection asynchronously; wait for the section to appear.
        cut.WaitForState(() => cut.Markup.Contains("数据库连接", StringComparison.Ordinal));

        Assert.Contains("仓库名称：", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("订单仓库", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("数据库连接", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("测试连接", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("保存", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("关闭", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryDetailDialog_WhenNotVisible_RendersNothing()
    {
        Services.AddScoped<IRepositoryDatabaseConnectionCoordinator>(_ => new StubDatabaseConnectionCoordinator());

        RepositorySummaryDto model = new() { Id = Guid.NewGuid(), Name = "隐藏仓库" };

        IRenderedComponent<RepositoryDetailDialog> cut = RenderComponent<RepositoryDetailDialog>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.Visible, false)
            .Add(p => p.User, CreateUserContext(isAdministrator: true)));

        Assert.DoesNotContain("隐藏仓库", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseConnectionPanel_RendersStatusAndFormFields()
    {
        Services.AddScoped<IRepositoryDatabaseConnectionCoordinator>(_ => new StubDatabaseConnectionCoordinator());

        RepositoryDatabaseConnectionSummaryDto model = new()
        {
            RepositoryId = Guid.NewGuid(),
            IsConfigured = true,
            HasConnectionString = true,
            HasUsername = false,
            HasPassword = false,
            DatabaseType = (int)DatabaseType.PostgreSql,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        IRenderedComponent<DatabaseConnectionPanel> cut = RenderComponent<DatabaseConnectionPanel>(parameters => parameters
            .Add(p => p.RepositoryId, model.RepositoryId)
            .Add(p => p.Model, model)
            .Add(p => p.User, CreateUserContext(isAdministrator: true)));

        Assert.Contains("数据库连接", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("已配置", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("PostgreSql", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("测试连接", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("保存", cut.Markup, StringComparison.Ordinal);
    }

    private static IUserContext CreateUserContext(bool isAdministrator)
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "test-user-id")],
            "Test"));
        return new UserContext(principal, isAdministrator);
    }

    /// <summary>
    /// Minimal stub for <see cref="IRepositoryDatabaseConnectionCoordinator"/> that returns a
    /// not-configured summary from <see cref="GetAsync"/>. Used by the dialog/panel bunit tests
    /// so we don't need a third-party mocking library.
    /// </summary>
    private sealed class StubDatabaseConnectionCoordinator : IRepositoryDatabaseConnectionCoordinator
    {
        public Task<RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>> GetAsync(
            Guid repositoryId, IUserContext user, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>.Success(
                new RepositoryDatabaseConnectionSummaryDto
                {
                    RepositoryId = repositoryId,
                    IsConfigured = false,
                    HasConnectionString = false,
                    HasUsername = false,
                    HasPassword = false,
                }));
        }

        public Task<RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>> UpsertAsync(
            Guid repositoryId, UpsertDatabaseConnectionRequest request, IUserContext user, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>.Success(
                new RepositoryDatabaseConnectionSummaryDto { RepositoryId = repositoryId, IsConfigured = true }));
        }

        public Task<RepositoryDatabaseConnectionMutationResult<bool>> DeleteAsync(
            Guid repositoryId, IUserContext user, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RepositoryDatabaseConnectionMutationResult<bool>.Success(true));
        }

        public Task<RepositoryDatabaseConnectionTestResult> TestConnectionAsync(
            Guid repositoryId, IUserContext user, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RepositoryDatabaseConnectionTestResult.Success("连接测试成功。"));
        }
    }
}

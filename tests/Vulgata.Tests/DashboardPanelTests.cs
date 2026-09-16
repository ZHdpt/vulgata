using System.Security.Claims;
using System.Reflection;
using Bunit;
using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        Services.AddScoped<IRepositoryProfilingCoordinator>(_ => new StubRepositoryProfilingCoordinator());
        // FluentUI web components invoke JS interop during render (e.g. FluentTextField, InputSelect).
        // bunit's default JSRuntime mode is strict and throws on unhandled calls; switch to loose
        // mode so those calls are silently stubbed instead of failing the render.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void SystemLlmOverridesPanel_RendersAllAgentTypes_AndFallsBackToGlobalDefault()
    {
        var systemId = Guid.NewGuid();
        IReadOnlyList<SystemLlmProviderOverrideSummaryDto> overrides =
        [
            new() { Id = Guid.NewGuid(), SystemId = systemId, AgentType = (int)AgentType.Orchestrator, ProviderName = "DeepSeek V4" },
        ];

        var cut = Render<SystemLlmOverridesPanel>(parameters => parameters
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

        var cut = Render<StandaloneReposPanel>(parameters => parameters
            .Add(p => p.Repositories, repositories));

        Assert.Contains("独立仓库", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("+ 新建独立仓库", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("共享组件库", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("团队工具库", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("management-section", cut.Find("section").ClassList);
        Assert.Equal(2, cut.FindAll(".admin-code").Count);
        Assert.Equal(2, cut.FindAll(".admin-status-pill").Count);

        // Empty state path
        var empty = Render<StandaloneReposPanel>(parameters => parameters
            .Add(p => p.Repositories, []));

        Assert.Contains("暂无独立仓库", empty.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void StandaloneReposPanel_OnCreateRequested_FiresEventCallback()
    {
        bool fired = false;
        var cut = Render<StandaloneReposPanel>(parameters => parameters
            .Add(p => p.Repositories, [])
            .Add(p => p.OnCreateRequested, EventCallback.Factory.Create(this, () => fired = true)));

        // FluentButton renders as a <fluent-button> web component; find by visible text instead of tag name.
        var buttons = cut.FindAll("button, fluent-button");
        buttons.Single(button => button.TextContent.Contains("+ 新建独立仓库", StringComparison.Ordinal)).Click();

        Assert.True(fired);
    }

    [Fact]
    public void StandaloneReposPanel_OnRepositorySelected_FiresWithRepositoryId()
    {
        var repositoryId = Guid.NewGuid();
        Guid? capturedId = null;
        IReadOnlyList<RepositorySummaryDto> repositories =
        [
            new() { Id = repositoryId, Name = "可点击仓库", ScanStatus = "未扫描" },
        ];

        var cut = Render<StandaloneReposPanel>(parameters => parameters
            .Add(p => p.Repositories, repositories)
            .Add(p => p.OnRepositorySelected, EventCallback.Factory.Create<Guid>(this, id => capturedId = id)));

        var buttons = cut.FindAll("button, fluent-button");
        buttons.Single(button => button.TextContent.Contains("查看", StringComparison.Ordinal)).Click();

        Assert.Equal(repositoryId, capturedId);
    }

    [Fact]
    public void StandaloneReposPanel_DemoSnapshot_RendersFakeValuesAndFiresScanRequest()
    {
        var repositoryId = Guid.NewGuid();
        Guid? startedId = null;
        IReadOnlyList<RepositorySummaryDto> repositories =
        [
            new()
            {
                Id = repositoryId,
                Name = "mbs_account",
                ScanStatus = "真实状态不应显示",
                DocumentCount = 1,
            },
        ];
        IReadOnlyDictionary<Guid, DemoRepositoryScanSnapshot> scanStates =
            new Dictionary<Guid, DemoRepositoryScanSnapshot>
            {
                [repositoryId] = new(
                    DemoRepositoryScanPhase.Ready,
                    new DateTimeOffset(2026, 8, 12, 9, 35, 0, TimeSpan.FromHours(8)),
                    186),
            };

        var cut = Render<StandaloneReposPanel>(parameters => parameters
            .Add(p => p.Repositories, repositories)
            .Add(p => p.DemoScanStates, scanStates)
            .Add(p => p.OnScanRequested, EventCallback.Factory.Create<Guid>(this, id => startedId = id)));

        Assert.Contains("待扫描", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("2026-08-12 09:35", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">186<", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("真实状态不应显示", cut.Markup, StringComparison.Ordinal);

        cut.FindAll("button, fluent-button")
            .Single(button => button.TextContent.Contains("开始扫描", StringComparison.Ordinal))
            .Click();

        Assert.Equal(repositoryId, startedId);
    }

    [Fact]
    public void StandaloneReposPanel_ScanningRepository_ViewFiresProgressRequest()
    {
        var repositoryId = Guid.NewGuid();
        Guid? detailId = null;
        Guid? progressId = null;
        IReadOnlyList<RepositorySummaryDto> repositories =
        [
            new() { Id = repositoryId, Name = "mbs_account" },
        ];
        IReadOnlyDictionary<Guid, DemoRepositoryScanSnapshot> scanStates =
            new Dictionary<Guid, DemoRepositoryScanSnapshot>
            {
                [repositoryId] = new(
                    DemoRepositoryScanPhase.Scanning,
                    DateTimeOffset.UtcNow,
                    186),
            };

        var cut = Render<StandaloneReposPanel>(parameters => parameters
            .Add(p => p.Repositories, repositories)
            .Add(p => p.DemoScanStates, scanStates)
            .Add(p => p.OnRepositorySelected, EventCallback.Factory.Create<Guid>(this, id => detailId = id))
            .Add(p => p.OnScanProgressSelected, EventCallback.Factory.Create<Guid>(this, id => progressId = id)));

        cut.FindAll("button, fluent-button")
            .Single(button => button.TextContent.Contains("查看", StringComparison.Ordinal))
            .Click();

        Assert.Null(detailId);
        Assert.Equal(repositoryId, progressId);
    }

    [Fact]
    public void SystemListPanel_RendersSystems_AndRepositoriesPerSystem()
    {
        var alphaId = Guid.NewGuid();
        var betaId = Guid.NewGuid();
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

        var cut = Render<SystemListPanel>(parameters => parameters
            .Add(p => p.Systems, [alpha, beta])
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
    public void SystemListPanel_DemoSnapshot_RendersScanControls()
    {
        var systemId = Guid.NewGuid();
        var repositoryId = Guid.NewGuid();
        SystemEntity system = new("账户系统", null, null, DateTimeOffset.UtcNow);
        typeof(SystemEntity).GetProperty(nameof(SystemEntity.Id))!.SetValue(system, systemId);
        IReadOnlyDictionary<Guid, IReadOnlyList<RepositorySummaryDto>> repositories =
            new Dictionary<Guid, IReadOnlyList<RepositorySummaryDto>>
            {
                [systemId] = [new() { Id = repositoryId, Name = "mbs_account" }],
            };
        IReadOnlyDictionary<Guid, DemoRepositoryScanSnapshot> scanStates =
            new Dictionary<Guid, DemoRepositoryScanSnapshot>
            {
                [repositoryId] = new(
                    DemoRepositoryScanPhase.Completed,
                    new DateTimeOffset(2026, 8, 15, 11, 3, 0, TimeSpan.FromHours(8)),
                    327),
            };

        var cut = Render<SystemListPanel>(parameters => parameters
            .Add(p => p.Systems, [system])
            .Add(p => p.RepositoriesBySystem, repositories)
            .Add(p => p.DemoScanStates, scanStates));

        Assert.Contains("扫描完成", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("2026-08-15 11:03", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">327<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("开始扫描", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemListPanel_EmptySystems_RendersEmptyState()
    {
        var cut = Render<SystemListPanel>(parameters => parameters
            .Add(p => p.Systems, [])
            .Add(p => p.IsAdministrator, false));

        Assert.Contains("尚未添加任何系统。创建你的第一个系统开始扫描。", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemListPanel_Administrator_RendersEditAndDeleteButtons()
    {
        SystemEntity system = new("管理平台", "描述", "上下文", DateTimeOffset.UtcNow);

        var cut = Render<SystemListPanel>(parameters => parameters
            .Add(p => p.Systems, [system])
            .Add(p => p.RepositoriesBySystem, new Dictionary<Guid, IReadOnlyList<RepositorySummaryDto>>())
            .Add(p => p.LlmOverridesBySystem, new Dictionary<Guid, IReadOnlyList<SystemLlmProviderOverrideSummaryDto>>())
            .Add(p => p.IsAdministrator, true));

        Assert.Contains("编辑", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("管理所有者", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("删除", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(
            "admin-danger-button",
            cut.FindAll("button, fluent-button")
                .Single(button => button.TextContent.Contains("删除", StringComparison.Ordinal))
                .ClassList);
    }

    [Fact]
    public void RepositoryDetailDialog_WhenVisible_RendersRepositoryNameAndDatabaseConnectionSection()
    {
        Services.AddScoped<IRepositoryDatabaseConnectionCoordinator>(_ => new StubDatabaseConnectionCoordinator());

        RepositorySummaryDto model = new() { Id = Guid.NewGuid(), Name = "订单仓库" };

        var cut = Render<RepositoryDetailDialog>(parameters => parameters
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

        var cut = Render<RepositoryDetailDialog>(parameters => parameters
            .Add(p => p.Model, model)
            .Add(p => p.Visible, false)
            .Add(p => p.User, CreateUserContext(isAdministrator: true)));

        Assert.DoesNotContain("隐藏仓库", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryDetailDialog_AfterSuccessfulProfiling_RendersChineseSortedLanguageDistribution()
    {
        Services.AddScoped<IRepositoryDatabaseConnectionCoordinator>(_ => new StubDatabaseConnectionCoordinator());
        Services.RemoveAll<IRepositoryProfilingCoordinator>();
        Services.AddScoped<IRepositoryProfilingCoordinator>(_ => new LanguageSummaryProfilingCoordinator());
        RepositorySummaryDto model = new() { Id = Guid.NewGuid(), Name = "多语言仓库" };
        var cut = Render<RepositoryDetailDialog>(parameters => parameters
            .Add(parameter => parameter.Model, model)
            .Add(parameter => parameter.Visible, true)
            .Add(parameter => parameter.User, CreateUserContext(isAdministrator: true)));
        cut.WaitForState(() => cut.Markup.Contains("数据库连接", StringComparison.Ordinal));

        cut.FindAll("button, fluent-button")
            .Single(button => button.TextContent.Contains("开始扫描", StringComparison.Ordinal))
            .Click();
        cut.WaitForState(() => cut.Markup.Contains("扫描完成", StringComparison.Ordinal));

        Assert.Contains("语言分布", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("c_sharp", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("2 个文件", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("typescript", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("3 个文件", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("zig_0+#.-", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("1 个文件", cut.Markup, StringComparison.Ordinal);
        Assert.True(
            cut.Markup.IndexOf("c_sharp", StringComparison.Ordinal) <
            cut.Markup.IndexOf("typescript", StringComparison.Ordinal));
        Assert.True(
            cut.Markup.IndexOf("typescript", StringComparison.Ordinal) <
            cut.Markup.IndexOf("zig_0+#.-", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryDetailDialog_AfterSuccessfulProfiling_RendersChineseFrameworkEvidenceOverview()
    {
        Services.AddScoped<IRepositoryDatabaseConnectionCoordinator>(_ => new StubDatabaseConnectionCoordinator());
        Services.RemoveAll<IRepositoryProfilingCoordinator>();
        Services.AddScoped<IRepositoryProfilingCoordinator>(_ => new LanguageSummaryProfilingCoordinator());
        RepositorySummaryDto model = new() { Id = Guid.NewGuid(), Name = "框架仓库" };
        var cut = Render<RepositoryDetailDialog>(parameters => parameters
            .Add(parameter => parameter.Model, model)
            .Add(parameter => parameter.Visible, true)
            .Add(parameter => parameter.User, CreateUserContext(isAdministrator: true)));
        cut.WaitForState(() => cut.Markup.Contains("数据库连接", StringComparison.Ordinal));

        cut.FindAll("button, fluent-button")
            .Single(button => button.TextContent.Contains("开始扫描", StringComparison.Ordinal))
            .Click();
        cut.WaitForState(() => cut.Markup.Contains("扫描完成", StringComparison.Ordinal));

        Assert.Contains("框架", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("置信度", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("检测依据", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("框架证据警告", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("react", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("置信度 高", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("置信度 中", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("High", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Medium", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("package.json#json:/dependencies/react", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("101", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("audit/000/package.json", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("已跳过", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("框架证据格式不受支持。", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Skipped", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(
            ScanCandidateOutcomeReasons.FrameworkEvidenceUnsupported,
            cut.Markup,
            StringComparison.Ordinal);
        Assert.DoesNotContain("RAW-SECRET", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepositoryDetailDialog_AfterSuccessfulProfiling_RendersEverySortedUnknownLanguagePath()
    {
        Services.AddScoped<IRepositoryDatabaseConnectionCoordinator>(_ => new StubDatabaseConnectionCoordinator());
        Services.RemoveAll<IRepositoryProfilingCoordinator>();
        Services.AddScoped<IRepositoryProfilingCoordinator>(_ => new LanguageSummaryProfilingCoordinator());
        RepositorySummaryDto model = new() { Id = Guid.NewGuid(), Name = "未知语言仓库" };
        var cut = Render<RepositoryDetailDialog>(parameters => parameters
            .Add(parameter => parameter.Model, model)
            .Add(parameter => parameter.Visible, true)
            .Add(parameter => parameter.User, CreateUserContext(isAdministrator: true)));
        cut.WaitForState(() => cut.Markup.Contains("数据库连接", StringComparison.Ordinal));

        cut.FindAll("button, fluent-button")
            .Single(button => button.TextContent.Contains("开始扫描", StringComparison.Ordinal))
            .Click();
        cut.WaitForState(() => cut.Markup.Contains("扫描完成", StringComparison.Ordinal));

        Assert.Contains("未知语言文件", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("3 个文件", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("src/unknown/Alpha.txt", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("src/unknown/Middle.txt", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("src/unknown/Zeta.txt", cut.Markup, StringComparison.Ordinal);
        Assert.True(
            cut.Markup.IndexOf("src/unknown/Alpha.txt", StringComparison.Ordinal) <
            cut.Markup.IndexOf("src/unknown/Middle.txt", StringComparison.Ordinal));
        Assert.True(
            cut.Markup.IndexOf("src/unknown/Middle.txt", StringComparison.Ordinal) <
            cut.Markup.IndexOf("src/unknown/Zeta.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryDetailDialog_AfterUnsafeProviderAudit_RendersOnlyUnknownPath()
    {
        var summary = new ScanRunSummaryDto
        {
            Id = Guid.NewGuid(),
            RepositoryId = Guid.NewGuid(),
            Status = "已完成",
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            UnknownLanguage = new UnknownLanguageSummaryDto
            {
                Paths = ["(unknown)"],
            },
        };
        Services.AddScoped<IRepositoryDatabaseConnectionCoordinator>(
            _ => new StubDatabaseConnectionCoordinator());
        Services.RemoveAll<IRepositoryProfilingCoordinator>();
        Services.AddScoped<IRepositoryProfilingCoordinator>(
            _ => new FixedSummaryProfilingCoordinator(summary));
        RepositorySummaryDto model = new()
        {
            Id = summary.RepositoryId,
            Name = "安全路径仓库",
        };
        var cut = Render<RepositoryDetailDialog>(parameters => parameters
            .Add(parameter => parameter.Model, model)
            .Add(parameter => parameter.Visible, true)
            .Add(parameter => parameter.User, CreateUserContext(isAdministrator: true)));
        cut.WaitForState(() => cut.Markup.Contains("数据库连接", StringComparison.Ordinal));

        cut.FindAll("button, fluent-button")
            .Single(button => button.TextContent.Contains("开始扫描", StringComparison.Ordinal))
            .Click();
        cut.WaitForState(() => cut.Markup.Contains("扫描完成", StringComparison.Ordinal));

        Assert.Contains("未知语言文件", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("(unknown)", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("build-server", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("etc/private", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("host-secret", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepositoryDetailDialog_WithAdversarialProviderFailure_RendersClosedChineseError()
    {
        const string rawDiagnostic =
            "MATCH (n) RETURN n; source=class Secret {}; C:\\host\\repo\\Secret.cs; " +
            "package=attacker-value; password=hunter2; token=sk-live-secret; elapsed=9876ms";
        const string safeMessage = "代码结构分析失败，请稍后重试。";
        Services.AddScoped<IRepositoryDatabaseConnectionCoordinator>(
            _ => new StubDatabaseConnectionCoordinator());
        Services.RemoveAll<IRepositoryProfilingCoordinator>();
        Services.AddScoped<IRepositoryProfilingCoordinator>(
            _ => new AdversarialFailureProfilingCoordinator(rawDiagnostic));
        RepositorySummaryDto model = new() { Id = Guid.NewGuid(), Name = "失败仓库" };
        var cut = Render<RepositoryDetailDialog>(parameters => parameters
            .Add(parameter => parameter.Model, model)
            .Add(parameter => parameter.Visible, true)
            .Add(parameter => parameter.User, CreateUserContext(isAdministrator: true)));
        cut.WaitForState(() => cut.Markup.Contains("数据库连接", StringComparison.Ordinal));

        cut.FindAll("button, fluent-button")
            .Single(button => button.TextContent.Contains("开始扫描", StringComparison.Ordinal))
            .Click();
        cut.WaitForState(() =>
            cut.Markup.Contains(rawDiagnostic, StringComparison.Ordinal) ||
            cut.Markup.Contains(safeMessage, StringComparison.Ordinal));

        Assert.Contains(safeMessage, cut.Markup, StringComparison.Ordinal);
        Assert.InRange(cut.Markup.Length, 1, 32_768);
        Assert.DoesNotContain("MATCH", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attacker-value", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-live-secret", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("9876ms", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryDetailDialog_WithAdversarialLoadException_RendersClosedChineseError()
    {
        const string rawDiagnostic =
            "SELECT secret FROM credentials; C:\\host\\appsettings.json; " +
            "password=hunter2; token=sk-live-secret; elapsed=9876ms";
        const string safeMessage = "加载数据库连接失败，请稍后重试。";
        Services.AddScoped<IRepositoryDatabaseConnectionCoordinator>(
            _ => new ThrowingDatabaseConnectionCoordinator(rawDiagnostic));
        Services.RemoveAll<IRepositoryProfilingCoordinator>();
        Services.AddScoped<IRepositoryProfilingCoordinator>(
            _ => new StubRepositoryProfilingCoordinator());
        RepositorySummaryDto model = new() { Id = Guid.NewGuid(), Name = "加载失败仓库" };

        var cut = Render<RepositoryDetailDialog>(parameters => parameters
            .Add(parameter => parameter.Model, model)
            .Add(parameter => parameter.Visible, true)
            .Add(parameter => parameter.User, CreateUserContext(isAdministrator: true)));
        cut.WaitForState(() =>
            cut.Markup.Contains(rawDiagnostic, StringComparison.Ordinal) ||
            cut.Markup.Contains(safeMessage, StringComparison.Ordinal));

        Assert.Contains(safeMessage, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("credentials", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hunter2", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-live-secret", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("9876ms", cut.Markup, StringComparison.Ordinal);
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

        var cut = Render<DatabaseConnectionPanel>(parameters => parameters
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

    private sealed class ThrowingDatabaseConnectionCoordinator(string diagnostic)
        : IRepositoryDatabaseConnectionCoordinator
    {
        public Task<RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>> GetAsync(
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(diagnostic);

        public Task<RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto>> UpsertAsync(
            Guid repositoryId,
            UpsertDatabaseConnectionRequest request,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RepositoryDatabaseConnectionMutationResult<bool>> DeleteAsync(
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RepositoryDatabaseConnectionTestResult> TestConnectionAsync(
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubRepositoryProfilingCoordinator : IRepositoryProfilingCoordinator
    {
        public Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
            Guid systemId,
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryProfilingResult(RepositoryProfilingOutcome.RepositoryNotFound));

        public Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryProfilingResult(RepositoryProfilingOutcome.RepositoryNotFound));

        public Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
            Guid systemId,
            Guid repositoryId,
            Guid scanRunId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ScanRunSummaryDto?>(null);

        public Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
            Guid repositoryId,
            Guid scanRunId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ScanRunSummaryDto?>(null);
    }

    private sealed class FixedSummaryProfilingCoordinator(ScanRunSummaryDto summary)
        : IRepositoryProfilingCoordinator
    {
        public Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
            Guid systemId,
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryProfilingResult(
                RepositoryProfilingOutcome.Success,
                summary));

        public Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryProfilingResult(
                RepositoryProfilingOutcome.Success,
                summary));

        public Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
            Guid systemId,
            Guid repositoryId,
            Guid scanRunId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ScanRunSummaryDto?>(summary);

        public Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
            Guid repositoryId,
            Guid scanRunId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ScanRunSummaryDto?>(summary);
    }

    private sealed class AdversarialFailureProfilingCoordinator(string diagnostic)
        : IRepositoryProfilingCoordinator
    {
        public Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
            Guid systemId,
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryProfilingResult(
                RepositoryProfilingOutcome.ProviderFailure,
                null,
                diagnostic));

        public Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryProfilingResult(
                RepositoryProfilingOutcome.ProviderFailure,
                null,
                diagnostic));

        public Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
            Guid systemId,
            Guid repositoryId,
            Guid scanRunId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ScanRunSummaryDto?>(null);

        public Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
            Guid repositoryId,
            Guid scanRunId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ScanRunSummaryDto?>(null);
    }

    private sealed class LanguageSummaryProfilingCoordinator : IRepositoryProfilingCoordinator
    {
        private readonly ScanRunSummaryDto _summary = CreateSummaryWithLanguages();

        public Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
            Guid systemId,
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryProfilingResult(
                RepositoryProfilingOutcome.Success,
                _summary));

        public Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryProfilingResult(
                RepositoryProfilingOutcome.Success,
                _summary));

        public Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
            Guid systemId,
            Guid repositoryId,
            Guid scanRunId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ScanRunSummaryDto?>(_summary);

        public Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
            Guid repositoryId,
            Guid scanRunId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ScanRunSummaryDto?>(_summary);

        private static ScanRunSummaryDto CreateSummaryWithLanguages()
        {
            var summary = new ScanRunSummaryDto
            {
                Id = Guid.NewGuid(),
                RepositoryId = Guid.NewGuid(),
                Status = "已完成",
                ParsedFileCount = 6,
                TotalCandidateFileCount = 6,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
            };
            var property = typeof(ScanRunSummaryDto).GetProperty("Languages");
            if (property is null)
            {
                return summary;
            }

            Type elementType = property.PropertyType.GetGenericArguments().Single();
            Array values = Array.CreateInstance(elementType, 3);
            values.SetValue(CreateLanguageValue(elementType, "c_sharp", 2), 0);
            values.SetValue(CreateLanguageValue(elementType, "typescript", 3), 1);
            values.SetValue(CreateLanguageValue(elementType, "zig_0+#.-", 1), 2);
            property.SetValue(summary, values);
            AddFrameworkOverview(summary);
            AddUnknownLanguageOverview(summary);
            return summary;
        }

        private static object CreateLanguageValue(Type type, string language, int distinctFileCount)
        {
            object value = Activator.CreateInstance(type)!;
            type.GetProperty("Language")!.SetValue(value, language);
            type.GetProperty("DistinctFileCount")!.SetValue(value, distinctFileCount);
            return value;
        }

        private static void AddFrameworkOverview(ScanRunSummaryDto summary)
        {
            var frameworksProperty = typeof(ScanRunSummaryDto).GetProperty("Frameworks");
            var auditProperty = typeof(ScanRunSummaryDto).GetProperty("FrameworkEvidenceAudit");
            if (frameworksProperty is null || auditProperty is null)
            {
                return;
            }

            Type frameworkType = frameworksProperty.PropertyType.GetGenericArguments().Single();
            object highFramework = Activator.CreateInstance(frameworkType)!;
            frameworkType.GetProperty("FrameworkName")!.SetValue(highFramework, "react");
            frameworkType.GetProperty("CodeUnitCount")!.SetValue(highFramework, 1);
            frameworkType.GetProperty("StrongestConfidence")!.SetValue(highFramework, "High");
            frameworkType.GetProperty("TotalFactCount")!.SetValue(highFramework, 1);
            PropertyInfo samplesProperty = frameworkType.GetProperty("EvidenceSamples")!;
            Type sampleType = samplesProperty.PropertyType.GetGenericArguments().Single();
            object sample = Activator.CreateInstance(sampleType)!;
            sampleType.GetProperty("CodeUnitId")!.SetValue(sample, Guid.NewGuid());
            sampleType.GetProperty("Confidence")!.SetValue(sample, "High");
            sampleType.GetProperty("DetectionSource")!.SetValue(
                sample,
                "package.json#json:/dependencies/react");
            Array samples = Array.CreateInstance(sampleType, 1);
            samples.SetValue(sample, 0);
            samplesProperty.SetValue(highFramework, samples);
            object mediumFramework = Activator.CreateInstance(frameworkType)!;
            frameworkType.GetProperty("FrameworkName")!.SetValue(mediumFramework, "spring-boot");
            frameworkType.GetProperty("CodeUnitCount")!.SetValue(mediumFramework, 1);
            frameworkType.GetProperty("StrongestConfidence")!.SetValue(mediumFramework, "Medium");
            frameworkType.GetProperty("TotalFactCount")!.SetValue(mediumFramework, 1);
            samplesProperty.SetValue(mediumFramework, Array.CreateInstance(sampleType, 0));
            Array frameworks = Array.CreateInstance(frameworkType, 2);
            frameworks.SetValue(highFramework, 0);
            frameworks.SetValue(mediumFramework, 1);
            frameworksProperty.SetValue(summary, frameworks);

            Type auditType = auditProperty.PropertyType;
            object audit = Activator.CreateInstance(auditType)!;
            auditType.GetProperty("SampledOutcomeCount")!.SetValue(audit, 1);
            auditType.GetProperty("TotalOutcomeCount")!.SetValue(audit, 101);
            auditType.GetProperty("SamplingApplied")!.SetValue(audit, true);
            PropertyInfo detailsProperty = auditType.GetProperty("Details")!;
            Type detailType = detailsProperty.PropertyType.GetGenericArguments().Single();
            object detail = Activator.CreateInstance(detailType)!;
            detailType.GetProperty("Path")!.SetValue(detail, "audit/000/package.json");
            detailType.GetProperty("Outcome")!.SetValue(detail, "已跳过");
            detailType.GetProperty("Reason")!.SetValue(
                detail,
                "框架证据格式不受支持。");
            Array details = Array.CreateInstance(detailType, 1);
            details.SetValue(detail, 0);
            detailsProperty.SetValue(audit, details);
            auditProperty.SetValue(summary, audit);
        }

        private static void AddUnknownLanguageOverview(ScanRunSummaryDto summary)
        {
            PropertyInfo? property = typeof(ScanRunSummaryDto).GetProperty("UnknownLanguage");
            if (property is null)
            {
                return;
            }

            object value = Activator.CreateInstance(property.PropertyType)!;
            value.GetType().GetProperty("Paths")!.SetValue(
                value,
                new[]
                {
                    "src/unknown/Alpha.txt",
                    "src/unknown/Middle.txt",
                    "src/unknown/Zeta.txt",
                });
            property.SetValue(summary, value);
        }
    }
}

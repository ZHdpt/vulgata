using System.Collections.Generic;
using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Shared;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Web.Components.Pages.Management;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

/// <summary>
/// T012 RED coverage for the real persisted Scan management surface.
///
/// The reader stub represents the already-green Core/Infrastructure public
/// seam.  It deliberately does not provide DemoScanUiState or any fake
/// progress source.  The first assertion is expected to fail while
/// ScanHistoryPage is still the existing placeholder; T013 owns the minimal
/// production page/coordinator implementation.
/// </summary>
public sealed class ProductionScanPageTests : BunitContext
{
    public ProductionScanPageTests()
    {
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void ProductionComposition_ResolvesStatusReaderAndCoordinatorForManagementPages()
    {
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();
        using IServiceScope scope = factory.Services.CreateScope();

        Assert.IsType<ProductionScanStatusReader>(
            scope.ServiceProvider.GetRequiredService<IProductionScanStatusReader>());
        Assert.IsType<ProductionScanStatusCoordinator>(
            scope.ServiceProvider.GetRequiredService<ProductionScanStatusCoordinator>());
    }

    [Fact]
    public void ScanExecutionPage_ExposesPublicGuidRouteForPersistedExecution()
    {
        Type? pageType = typeof(ScanHistoryPage).Assembly.GetType(
            "Vulgata.Web.Components.Pages.Management.ScanExecutionPage");

        Assert.True(
            pageType is not null,
            "RED T012: ScanExecutionPage is absent; T013 must add the real persisted execution component.");
        Assert.True(typeof(IComponent).IsAssignableFrom(pageType!));

        RouteAttribute route = Assert.Single(pageType!.GetCustomAttributes<RouteAttribute>());
        Assert.Contains("/management/scan-history/", route.Template, StringComparison.Ordinal);
        Assert.Contains(":guid", route.Template, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanHistoryPage_RemainsProtectedByManagementAccess()
    {
        AuthorizeAttribute? authorize =
            typeof(ScanHistoryPage).GetCustomAttributes<AuthorizeAttribute>().SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal(AuthorizationPolicyNames.ManagementAccess, authorize!.Policy);
    }

    [Fact]
    public void ScanHistoryPage_PersistedPausedExecution_RendersChineseStateCountsReasonAndResumeAction()
    {
        Guid scanExecutionId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var reader = new StubProductionScanStatusReader(
            [new ProductionScanStatusView(
                scanExecutionId,
                Guid.Parse("22222222-2222-4222-8222-222222222222"),
                Guid.Parse("33333333-3333-4333-8333-333333333333"),
                ProductionScanDisplayState.Paused,
                CompletedJobCount: 3,
                TotalJobCount: 8,
                PendingJobCount: 4,
                RunningJobCount: 1,
                RetryScheduledJobCount: 0,
                FailedJobCount: 0,
                NextRetryAt: null,
                ReasonCode: "provider-timeout",
                ReasonTextZh: "LLM 服务暂时不可用，请稍后继续扫描。",
                CanResume: true,
                FailedFiles: [],
                FailedFileCount: 0,
                ObservedAt: new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                StateVersion: 4,
                EvidenceEligibility: ProductionScanEvidenceEligibility.ProductionPersisted)]);
        Services.AddSingleton<IProductionScanStatusReader>(reader);

        IRenderedComponent<ScanHistoryPage> cut = Render<ScanHistoryPage>();

        Assert.Contains("已暂停", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("3", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("8", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("LLM 服务暂时不可用，请稍后继续扫描。", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("继续扫描", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoScanUiState", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("vulgata-scanning.html", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanHistoryPage_PersistedStates_RendersClosedChineseLabelsAndBoundedFailureSummary()
    {
        Services.AddSingleton<IProductionScanStatusReader>(
            new StubProductionScanStatusReader(
            [
                CreateView(ProductionScanDisplayState.Preparing),
                CreateView(ProductionScanDisplayState.Running),
                CreateView(ProductionScanDisplayState.Retrying),
                CreateView(ProductionScanDisplayState.Paused),
                CreateView(ProductionScanDisplayState.Resumed),
                CreateView(ProductionScanDisplayState.Completed),
                CreateView(
                    ProductionScanDisplayState.CompletedWithWarnings,
                    failedFileCount: 21,
                    failedFiles: ["src/neutral/failed-01.java", "src/neutral/failed-02.xml"]),
                CreateView(ProductionScanDisplayState.FailedIntegrity),
                CreateView(ProductionScanDisplayState.FailedSecurity),
                CreateView(ProductionScanDisplayState.Failed),
                CreateView(ProductionScanDisplayState.Cancelled),
            ]));

        IRenderedComponent<ScanHistoryPage> cut = Render<ScanHistoryPage>();

        Assert.Contains("准备中", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("扫描中", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("正在重试", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("已暂停", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("已恢复，等待继续", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("已完成", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("已完成，但有警告", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("完整性校验失败", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("安全校验失败", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("扫描失败", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("已取消", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("src/neutral/failed-01.java", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("src/neutral/failed-02.xml", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("provider", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DemoScanUiState", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("vulgata-scanning.html", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanHistoryPage_MissingOrUnauthorizedReaderResult_RendersNoProtectedDetailsOrResumeAction()
    {
        var reader = new StubProductionScanStatusReader([]);
        Services.AddSingleton<IProductionScanStatusReader>(reader);

        IRenderedComponent<ScanHistoryPage> cut = Render<ScanHistoryPage>();

        Assert.Equal(1, reader.ListCallCount);
        Assert.DoesNotContain("secret", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider-timeout", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("继续扫描", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoScanUiState", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("vulgata-scanning.html", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanHistoryPage_NonRecoverableExecution_DoesNotOfferResumeAction()
    {
        var reader = new StubProductionScanStatusReader(
            [CreateView(ProductionScanDisplayState.Completed)]);
        Services.AddSingleton<IProductionScanStatusReader>(reader);

        IRenderedComponent<ScanHistoryPage> cut = Render<ScanHistoryPage>();

        Assert.Equal(1, reader.ListCallCount);
        Assert.Contains("已完成", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("继续扫描", cut.Markup, StringComparison.Ordinal);
    }

    private static ProductionScanStatusView CreateView(
        ProductionScanDisplayState displayState,
        int failedFileCount = 0,
        IReadOnlyList<string>? failedFiles = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            displayState,
            CompletedJobCount: 3,
            TotalJobCount: 8,
            PendingJobCount: displayState == ProductionScanDisplayState.Running ? 4 : 0,
            RunningJobCount: displayState == ProductionScanDisplayState.Running ? 1 : 0,
            RetryScheduledJobCount: displayState == ProductionScanDisplayState.Retrying ? 1 : 0,
            FailedJobCount: failedFileCount,
            NextRetryAt: displayState == ProductionScanDisplayState.Retrying
                ? new DateTimeOffset(2026, 8, 30, 9, 5, 0, TimeSpan.Zero)
                : null,
            ReasonCode: displayState is ProductionScanDisplayState.FailedIntegrity
                or ProductionScanDisplayState.FailedSecurity
                ? "bounded-failure"
                : null,
            ReasonTextZh: displayState == ProductionScanDisplayState.FailedIntegrity
                ? "持久化状态或源码版本的完整性校验失败。"
                : displayState == ProductionScanDisplayState.FailedSecurity
                    ? "扫描内容未通过安全校验。"
                    : displayState == ProductionScanDisplayState.CompletedWithWarnings
                        ? "扫描已完成，但存在警告。"
                        : null,
            CanResume: displayState == ProductionScanDisplayState.Paused,
            FailedFiles: failedFiles ?? [],
            FailedFileCount: failedFileCount,
            ObservedAt: new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
            StateVersion: displayState == ProductionScanDisplayState.Resumed ? 1 : 0,
            EvidenceEligibility: ProductionScanEvidenceEligibility.ProductionPersisted);

    private sealed class StubProductionScanStatusReader(
        IReadOnlyList<ProductionScanStatusView> views)
        : IProductionScanStatusReader
    {
        private readonly IReadOnlyList<ProductionScanStatusView> _views = views;

        public int ListCallCount { get; private set; }

        public Task<IReadOnlyList<ProductionScanStatusView>> ListVisibleAsync(
            DurableScanAuthorizationScope scope,
            CancellationToken cancellationToken = default)
        {
            ListCallCount++;
            return Task.FromResult(_views);
        }

        public Task<ProductionScanStatusResult> GetVisibleAsync(
            DurableScanAuthorizationScope scope,
            Guid scanExecutionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_views.FirstOrDefault(value => value.ScanExecutionId == scanExecutionId) is { } view
                ? ProductionScanStatusResult.Found(view)
                : ProductionScanStatusResult.Missing());
    }
}

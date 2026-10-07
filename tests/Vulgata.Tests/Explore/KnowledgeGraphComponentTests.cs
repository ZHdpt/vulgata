using System.Reflection;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Components.Explore;
using Vulgata.Web.Components.Pages.Explore;
using Vulgata.Web.Explore;
using Vulgata.Web.ViewModels.Explore;

namespace Vulgata.Tests.Explore;

public sealed class KnowledgeGraphComponentTests : BunitContext
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public void KnowledgeGraph_Fit_MaximumParallelOrSelfEdgesRemainInsideVisibleCanvas(bool selfLoops)
    {
        Configure();
        var fixture = new ControlledGraphFixture();
        var first = fixture.Node(1);
        var second = fixture.Node(2);
        var graph = new ExploreGraph([first, second], Enumerable.Range(0, 80)
            .Select(id => fixture.Edge(id, first, selfLoops ? first : second)).ToImmutableArray(), [], false);
        var cut = Render<KnowledgeGraph>(parameters => parameters.Add(component => component.Graph, graph).Add(component => component.Mode, KnowledgeMode.IT));
        cut.Find("[aria-label='放大']").Click();
        cut.Find("[aria-label='向右平移']").Click();
        cut.Find("[aria-label='适应视图']").Click();
        var view = Numbers(cut.Find("svg.graph-canvas").GetAttribute("viewBox")!);
        var transform = Numbers(cut.Find("g[data-viewport]").GetAttribute("transform")!);
        Assert.Equal(4, view.Length);
        Assert.Equal(3, transform.Length);
        void Visible(double x, double y)
        {
            var actualX = transform[0] + transform[2] * x;
            var actualY = transform[1] + transform[2] * y;
            Assert.InRange(actualX, view[0] - .01, view[0] + view[2] + .01);
            Assert.InRange(actualY, view[1] - .01, view[1] + view[3] + .01);
        }
        foreach (var path in cut.FindAll("svg [data-edge-id]"))
        {
            var data = path.GetAttribute("d")!;
            var points = Numbers(data);
            var cubic = data.Contains('C');
            Assert.True(cubic || data.Contains('Q'), "The test expects the component's published quadratic/cubic SVG curve shape.");
            Assert.Equal(cubic ? 8 : 6, points.Length);
            for (var step = 0; step <= 100; step++)
            {
                var t = step / 100d;
                var u = 1 - t;
                if (cubic)
                {
                    Visible(u * u * u * points[0] + 3 * u * u * t * points[2] + 3 * u * t * t * points[4] + t * t * t * points[6],
                        u * u * u * points[1] + 3 * u * u * t * points[3] + 3 * u * t * t * points[5] + t * t * t * points[7]);
                }
                else
                {
                    Visible(u * u * points[0] + 2 * u * t * points[2] + t * t * points[4],
                        u * u * points[1] + 2 * u * t * points[3] + t * t * points[5]);
                }
            }
        }
        foreach (var circle in cut.FindAll("svg circle"))
        {
            var x = double.Parse(circle.GetAttribute("cx")!, CultureInfo.InvariantCulture);
            var y = double.Parse(circle.GetAttribute("cy")!, CultureInfo.InvariantCulture);
            var radius = double.Parse(circle.GetAttribute("r")!, CultureInfo.InvariantCulture);
            Visible(x - radius, y - radius);
            Visible(x + radius, y + radius);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void KnowledgeGraph_LongUnicodeTitle_BoundsCanvasLabelWithoutLosingAccessibleTitle()
    {
        Configure();
        var fixture = new ControlledGraphFixture();
        var title = string.Concat(Enumerable.Repeat("知识😀", 300));
        var graph = new ExploreGraph([fixture.Node(1) with { Title = title }], [], [], false);
        var cut = Render<KnowledgeGraph>(parameters => parameters.Add(component => component.Graph, graph).Add(component => component.Mode, KnowledgeMode.IT));
        var label = cut.Find("svg text").TextContent;
        Assert.InRange(label.EnumerateRunes().Count(), 1, 40);
        var strictUtf8 = new UTF8Encoding(false, true);
        Assert.Equal(label, strictUtf8.GetString(strictUtf8.GetBytes(label)));
        Assert.Contains(title, cut.Find("ul.node-list").TextContent, StringComparison.Ordinal);
    }

    private static double[] Numbers(string value) => Regex.Matches(value, @"-?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?")
        .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture)).ToArray();

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void KnowledgeGraph_AuthorizedMetadata_IsUpfrontEncodedAndExactLinked()
    {
        Configure();
        var graph = Graph();
        var cut = Render<KnowledgeGraph>(parameters => parameters.Add(component => component.Graph, graph).Add(component => component.Mode, KnowledgeMode.IT));
        Assert.Empty(cut.FindAll("script,img"));
        Assert.Contains("<script>节点</script>", cut.Markup.Replace("&lt;", "<", StringComparison.Ordinal).Replace("&gt;", ">", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("参考上下文", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("不得当作源代码证明", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("许可的不确定解释", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("许可的覆盖缺口", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("REFERENCES", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("GENERATED_FROM", cut.Markup, StringComparison.Ordinal);
        foreach (var node in graph.Nodes)
        {
            Assert.Contains(cut.FindAll("a"), link => link.GetAttribute("href") == ExactDocumentLinkBuilder.Build(node.Handle));
            var button = cut.Find($"button[data-node-id='{node.Handle.DocumentVersionId:D}'],fluent-button[data-node-id='{node.Handle.DocumentVersionId:D}']");
            Assert.Equal("false", button.GetAttribute("aria-pressed"));
        }
        foreach (var edge in graph.Edges)
        {
            Assert.NotNull(cut.Find($"svg [data-edge-id='{edge.RelationshipVersionId:D}']"));
        }
        Assert.Equal(4, cut.FindAll("svg [data-edge-id]").Count);
        Assert.Contains("关系2", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("关系4", cut.Markup, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeMode.Business, "light")]
    [InlineData(KnowledgeMode.IT, "dark")]
    public void GraphPage_ModeThemeAndOrdinaryRoute_UsesManualRefreshOnly(KnowledgeMode mode, string theme)
    {
        var service = new GraphViewModelServiceProbe();
        service.Response = _ => Task.FromResult(service.Result(1, mode));
        Configure(service);
        var cut = Render<GraphPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"[data-theme='{theme}']")));
        cut.WaitForAssertion(() => Assert.Contains("节点1", cut.Markup, StringComparison.Ordinal));
        Assert.Equal(1, service.Calls);
        Assert.Contains("受控测试数据", cut.Markup, StringComparison.Ordinal);
        var authorize = Assert.Single(typeof(GraphPage).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Null(authorize.Policy);
        Assert.Null(authorize.Roles);
        cut.Find("[aria-label='手动刷新']").Click();
        cut.WaitForAssertion(() => Assert.Equal(2, service.Calls));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void GraphPage_ViewportAndSelectionActions_DoNotFetchOrChangeSemanticGraph()
    {
        var service = new GraphViewModelServiceProbe();
        var graph = Graph();
        service.Response = _ => Task.FromResult(new ExploreGraphResult(KnowledgeReadStatus.Success, graph, KnowledgeMode.IT, true));
        Configure(service);
        var cut = Render<GraphPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("g[data-viewport]")));
        var before = JsonSerializer.Serialize(graph);
        var originalTransform = cut.Find("g[data-viewport]").GetAttribute("transform");
        cut.Find("[aria-label='向右平移']").Click();
        Assert.NotEqual(originalTransform, cut.Find("g[data-viewport]").GetAttribute("transform"));
        cut.Find("[aria-label='向左平移']").Click();
        cut.Find("[aria-label='向上平移']").Click();
        cut.Find("[aria-label='向下平移']").Click();
        cut.Find("[aria-label='放大']").Click();
        Assert.NotEqual(originalTransform, cut.Find("g[data-viewport]").GetAttribute("transform"));
        cut.Find("[aria-label='缩小']").Click();
        cut.Find("[aria-label='适应视图']").Click();
        var nodeId = graph.Nodes[0].Handle.DocumentVersionId;
        cut.Find($"button[data-node-id='{nodeId:D}'],fluent-button[data-node-id='{nodeId:D}']").Click();
        Assert.Equal("true", cut.Find($"button[data-node-id='{nodeId:D}'],fluent-button[data-node-id='{nodeId:D}']").GetAttribute("aria-pressed"));
        Assert.Equal(1, service.Calls);
        Assert.Equal(before, JsonSerializer.Serialize(graph));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void KnowledgeGraph_NewProjection_ResetsSelectionAndViewport()
    {
        Configure();
        var graph = Graph();
        var cut = Render<KnowledgeGraph>(parameters => parameters.Add(component => component.Graph, graph).Add(component => component.Mode, KnowledgeMode.IT));
        var initial = cut.Find("g[data-viewport]").GetAttribute("transform");
        cut.Find("[aria-label='放大']").Click();
        var nodeId = graph.Nodes[0].Handle.DocumentVersionId;
        cut.Find($"button[data-node-id='{nodeId:D}'],fluent-button[data-node-id='{nodeId:D}']").Click();
        cut.Render(parameters => parameters.Add(component => component.Graph, graph with { Incomplete = true }).Add(component => component.Mode, KnowledgeMode.IT));
        Assert.Equal(initial, cut.Find("g[data-viewport]").GetAttribute("transform"));
        Assert.Equal("false", cut.Find($"button[data-node-id='{nodeId:D}'],fluent-button[data-node-id='{nodeId:D}']").GetAttribute("aria-pressed"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void KnowledgeGraph_LimitsAndUnknownCoverage_AreSeparateVisibleQualifications()
    {
        Configure();
        var cut = Render<KnowledgeGraph>(parameters => parameters.Add(component => component.Graph,
            Graph() with { Incomplete = true, HasUnknownCoverage = true }).Add(component => component.Mode, KnowledgeMode.IT));
        Assert.Contains("不完整", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("未知", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("许可的覆盖缺口", cut.Markup, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void GraphPage_MissingFeatureServices_ShowsSafeChineseUnavailable()
    {
        var cut = Render<GraphPage>();
        Assert.Contains("不可用", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("svg"));
    }

    private void Configure(GraphViewModelServiceProbe? service = null)
    {
        Services.AddFluentUIComponents();
        JSInterop.Mode = JSRuntimeMode.Loose;
        if (service is not null)
        {
            Services.AddSingleton<IExploreGraphService>(service);
            Services.AddTransient<KnowledgeGraphViewModel>();
        }
    }

    private static ExploreGraph Graph()
    {
        var fixture = new ControlledGraphFixture();
        var first = fixture.Node(1) with
        {
            Title = "<script>节点</script>",
            QualifiedExcerpt = [new("advisory", "许可的解释", ClaimQualification.Advisory, [], ["不得当作源代码证明"])],
        };
        var second = fixture.Node(2, DocumentKind.BusinessLogic);
        return new([first, second], [fixture.Edge(1, first, first), fixture.Edge(2, first, second),
            fixture.Edge(3, second, first, RelationshipKind.GeneratedFrom), fixture.Edge(4, first, second)],
            [new("unknown", "许可的不确定解释", [], ["许可的不确定限制"])], false, new(true, ["许可的覆盖缺口"]));
    }
}

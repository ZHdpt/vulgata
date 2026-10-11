using System.Collections.Immutable;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Explore;

namespace Vulgata.Tests.Explore;

public sealed class KnowledgeGraphProjectionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(39, 39, false)]
    [InlineData(40, 40, false)]
    [InlineData(41, 40, true)]
    public void Project_NodeBoundary_TrimsWithoutInventingNodes(int supplied, int expected, bool incomplete)
    {
        var fixture = new ControlledGraphFixture();
        var seeds = new SearchPage(KnowledgeReadStatus.Success, Enumerable.Range(0, supplied).Select(id => fixture.Node(id)).ToImmutableArray(), null, new(false, []));
        var graph = KnowledgeGraphProjector.Project([seeds], [], false);
        Assert.Equal(expected, graph.Nodes.Length);
        Assert.Equal(incomplete, graph.Incomplete);
        Assert.Empty(graph.Edges);
        Assert.All(graph.Nodes, node => Assert.Contains(node, seeds.Documents));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(79, 79, false)]
    [InlineData(80, 80, false)]
    [InlineData(81, 80, true)]
    public void Project_EdgeBoundary_PreservesAcceptedParallelEdgeIds(int supplied, int expected, bool incomplete)
    {
        var fixture = new ControlledGraphFixture();
        var left = fixture.Node(1);
        var right = fixture.Node(2);
        var edges = Enumerable.Range(0, supplied).Select(id => fixture.Edge(id, left, right)).ToImmutableArray();
        var graph = KnowledgeGraphProjector.Project([new(KnowledgeReadStatus.Success, [left, right], null, null)],
            [new(KnowledgeReadStatus.Success, edges, [], null, null)], false);
        Assert.Equal(expected, graph.Edges.Length);
        Assert.Equal(expected, graph.Edges.Select(edge => edge.RelationshipVersionId).Distinct().Count());
        Assert.Equal(incomplete, graph.Incomplete);
        Assert.All(graph.Edges, edge => Assert.Contains(edge, edges));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Project_CyclesSelfLoopsAndBothKinds_PreservesDirectionAndDoesNotInventHelpers()
    {
        var fixture = new ControlledGraphFixture();
        var left = fixture.Node(1);
        var right = fixture.Node(2, DocumentKind.BusinessLogic);
        var external = fixture.Node(3);
        ImmutableArray<RelatedDocument> edges = [fixture.Edge(1, left, left), fixture.Edge(2, left, right),
            fixture.Edge(3, right, left, RelationshipKind.GeneratedFrom), fixture.Edge(4, left, right), fixture.Edge(5, left, external)];
        var graph = KnowledgeGraphProjector.Project([new(KnowledgeReadStatus.Success, [left, right], null, null)],
            [new(KnowledgeReadStatus.Success, edges, [], null, null)], false);
        Assert.Equal(2, graph.Nodes.Length);
        Assert.Equal(4, graph.Edges.Length);
        Assert.Contains(edges[0], graph.Edges);
        Assert.Contains(edges[2], graph.Edges);
        Assert.DoesNotContain(edges[4], graph.Edges);
        Assert.True(graph.Incomplete, "An accepted relationship outside the displayed endpoint set must remain visibly partial.");
        Assert.DoesNotContain(graph.Nodes, node => node.Handle == external.Handle);
        Assert.All(graph.Nodes, node => Assert.Contains($"version={node.Handle.DocumentVersionId:D}", ExactDocumentLinkBuilder.Build(node.Handle), StringComparison.Ordinal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadGraphAsync_FortyOneSeeds_UsesFixedOneHopBudgetAndFinalWholeCapsulesAsync()
    {
        var fixture = new ControlledGraphFixture();
        fixture.Seeds = new(KnowledgeReadStatus.Success, Enumerable.Range(0, 41).Select(id => fixture.Node(id)).ToImmutableArray(), null, new(false, []));
        using var service = fixture.Service();
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        Assert.Equal(KnowledgeReadStatus.Success, result.Status);
        Assert.Equal(40, result.Graph!.Nodes.Length);
        Assert.True(result.Graph.Incomplete);
        Assert.Equal(8, fixture.Calls.Count);
        Assert.Equal("open", fixture.Calls[0]);
        Assert.Equal("final", fixture.Calls[^1]);
        Assert.Equal(5, fixture.RelatedRequests.Count);
        Assert.Equal(41, fixture.SearchRequest!.PageSize);
        Assert.All(fixture.RelatedRequests, request =>
        {
            Assert.Equal(81, request.PageSize);
            Assert.Null(request.Continuation);
            Assert.Contains(result.Graph.Nodes, node => node.Handle == request.Document);
        });
        Assert.Equal(fixture.Issued.ToArray(), fixture.Reauthorized.ToArray());
        Assert.Equal(KnowledgePurpose.Graph, fixture.Opened!.Purpose);
        Assert.Equal(fixture.Opened, fixture.FinalSelection);
        Assert.All(fixture.Budgets, budget => Assert.InRange(budget.MaximumResultUtf8Bytes, 1, KnowledgeContract.MaximumContentBytes));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadGraphAsync_TruncatedEdges_RetainsCapsuleForWholeRelatedPageAsync()
    {
        var fixture = new ControlledGraphFixture();
        var left = fixture.Node(1);
        var right = fixture.Node(2);
        fixture.Seeds = new(KnowledgeReadStatus.Success, [left, right], null, new(false, []));
        fixture.Related = request => new(KnowledgeReadStatus.Success, request.Document == left.Handle
            ? Enumerable.Range(0, 81).Select(id => fixture.Edge(id, left, right)).ToImmutableArray() : [], [], null, new(false, []));
        using var service = fixture.Service();
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        Assert.Equal(KnowledgeReadStatus.Success, result.Status);
        Assert.Equal(80, result.Graph!.Edges.Length);
        Assert.True(result.Graph.Incomplete);
        Assert.Equal(fixture.Issued.ToArray(), fixture.Reauthorized.ToArray());
        Assert.Contains(fixture.Issued[1], fixture.Reauthorized);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadGraphAsync_EmptyPage_StillReauthorizesOwnerCapsuleAndPreservesCoverageMeaningAsync()
    {
        var fixture = new ControlledGraphFixture();
        fixture.Seeds = fixture.Seeds with { Coverage = new(true, ["受控来源覆盖不完整"]) };
        using var service = fixture.Service();
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        Assert.Equal(KnowledgeReadStatus.Success, result.Status);
        Assert.Empty(result.Graph!.Nodes);
        Assert.Empty(result.Graph.Edges);
        Assert.False(result.Graph.Incomplete);
        Assert.True(result.Graph.Coverage!.IsPartial);
        Assert.Equal(new[] { "open", "search", "final" }, fixture.Calls.ToArray());
        Assert.Equal(fixture.Issued.ToArray(), fixture.Reauthorized.ToArray());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void EncodeSearch_ExactDefaultJsonAndCanonicalDigest_MatchesIndependentContractFormula()
    {
        var fixture = new ControlledGraphFixture();
        var page = fixture.Seeds with { Documents = [fixture.Node(1) with { Title = "中文😀\"\\" }] };
        var encoder = new ExploreGraphOutputEncoder();
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(page), encoder.EncodeSearch(page).ToArray());
        Assert.Equal(ControlledGraphFixture.PageDigest(page), ExploreGraphOutputEncoder.ComputePageDigest(page));
        Assert.Equal("october.explore.graph.default-json.v1", encoder.ProfileId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Project_MixedKnownAndUnknownCoverage_PreservesPermittedGapsAndUnknownFlag()
    {
        var fixture = new ControlledGraphFixture();
        var graph = KnowledgeGraphProjector.Project(
            [new(KnowledgeReadStatus.Success, [fixture.Node(1)], null, new(true, ["已授权的覆盖缺口"]))],
            [new(KnowledgeReadStatus.Success, [], [], null, null)], false);
        Assert.True(graph.HasUnknownCoverage);
        Assert.True(graph.Coverage!.IsPartial);
        Assert.Equal(new[] { "已授权的覆盖缺口" }, graph.Coverage.PermittedGaps.ToArray());
        Assert.False(graph.Incomplete);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadGraphAsync_DifferentPermittedUncertaintiesShareLocalKey_PreservesBothPayloadsAndCapsulesAsync()
    {
        var fixture = new ControlledGraphFixture();
        var left = fixture.Node(1);
        var right = fixture.Node(2);
        fixture.Seeds = fixture.Seeds with { Documents = [left, right] };
        var first = new Uncertainty("local-unknown", "第一个文档的运行目标未确定。", [fixture.Host.Fixture.SourceEvidence], ["缺少部署证据"]);
        var second = new Uncertainty("local-unknown", "第二个文档的响应分支未确定。", [fixture.Host.Fixture.ProviderClaim.Evidence[0]], ["缺少响应契约"]);
        fixture.Related = request => new(KnowledgeReadStatus.Success, [],
            [request.Document == left.Handle ? first : second], null, new(false, []));
        using var service = fixture.Service();
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        Assert.Equal(KnowledgeReadStatus.Success, result.Status);
        Assert.Equal(2, result.Graph!.Uncertainties.Length);
        Assert.Contains(first, result.Graph.Uncertainties);
        Assert.Contains(second, result.Graph.Uncertainties);
        Assert.Equal(3, fixture.Issued.Count);
        Assert.Equal(fixture.Issued.ToArray(), fixture.Reauthorized.ToArray());
    }
}

using System.Collections.Immutable;
using System.Text.Json;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.Explore;

public sealed class KnowledgeGraphAuthorizationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadGraphAsync_DifferentAcquiredMaterialDeniedAtFinal_HasIdenticalMetadataFreeObservableResultAsync()
    {
        // Consumer final-denial noninterference only; scripted pages do not prove owner prefiltering.
        var observable = new List<string>();
        foreach (var additionalNodes in new[] { 0, 2 })
        {
            var fixture = new ControlledGraphFixture { FinalStatus = KnowledgeDependencyStatus.Denied };
            var common = fixture.Node(1);
            var nodes = new[] { common }.Concat(Enumerable.Range(2, additionalNodes).Select(id => fixture.Node(id))).ToImmutableArray();
            fixture.Seeds = fixture.Seeds with { Documents = nodes };
            fixture.Related = request => new(KnowledgeReadStatus.Success,
                request.Document == common.Handle
                    ? nodes.Where(node => node.Handle != common.Handle).Select((node, index) => fixture.Edge(index + 1, common, node)).ToImmutableArray()
                    : [],
                [new("local", $"实际已获取的不同说明-{additionalNodes}", [], [])], null,
                new(additionalNodes != 0, additionalNodes == 0 ? [] : ["实际已获取的覆盖缺口"]));
            using var service = fixture.Service();
            var result = await ((IExploreGraphService)service).LoadGraphAsync();
            Assert.NotEqual(KnowledgeReadStatus.Success, result.Status);
            Assert.Null(result.Graph);
            Assert.Equal(nodes.Length, fixture.RelatedRequests.Count);
            Assert.Equal(fixture.Issued.ToArray(), fixture.Reauthorized.ToArray());
            observable.Add(JsonSerializer.Serialize(result));
        }
        Assert.Equal(observable[0], observable[1]);
        Assert.DoesNotContain("opaque-owner", observable[0], StringComparison.Ordinal);
        Assert.DoesNotContain("实际已获取", observable[0], StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadGraphAsync_BusinessGraph_AcceptsBlOnlyAndRejectsCodeLogicSeedAsync(bool codeLogicSeed)
    {
        var fixture = new ControlledGraphFixture();
        fixture.Host.Context.Current = new(fixture.Host.Fixture.Selection(KnowledgeMode.Business), true);
        var left = fixture.Node(1, codeLogicSeed ? DocumentKind.CodeLogic : DocumentKind.BusinessLogic);
        var right = fixture.Node(2, DocumentKind.BusinessLogic);
        fixture.Seeds = fixture.Seeds with { Documents = [left, right] };
        fixture.Related = request => new(KnowledgeReadStatus.Success,
            request.Document == left.Handle ? [fixture.Edge(1, left, right)] : [], [], null, new(false, []));
        using var service = fixture.Service();
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        if (codeLogicSeed)
        {
            Assert.NotEqual(KnowledgeReadStatus.Success, result.Status);
            Assert.Null(result.Graph);
            Assert.DoesNotContain("节点", JsonSerializer.Serialize(result), StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(KnowledgeReadStatus.Success, result.Status);
            Assert.Equal(KnowledgeMode.Business, result.Mode);
            Assert.Equal(2, result.Graph!.Nodes.Length);
            Assert.All(result.Graph.Nodes, node => Assert.Equal(DocumentKind.BusinessLogic, node.Kind));
            Assert.Single(result.Graph.Edges);
            Assert.Equal(fixture.Issued.ToArray(), fixture.Reauthorized.ToArray());
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeDependencyStatus.Denied)]
    [InlineData(KnowledgeDependencyStatus.AuthorityUnavailable)]
    [InlineData(KnowledgeDependencyStatus.UnknownLineage)]
    [InlineData(KnowledgeDependencyStatus.InvalidProof)]
    public async Task LoadGraphAsync_FinalOwnerFailure_SuppressesWholeAccumulatedGraphAsync(KnowledgeDependencyStatus status)
    {
        var fixture = new ControlledGraphFixture { FinalStatus = status };
        fixture.Seeds = fixture.Seeds with { Documents = [fixture.Node(1)] };
        using var service = fixture.Service();
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        Assert.NotEqual(KnowledgeReadStatus.Success, result.Status);
        Assert.Null(result.Graph);
        Assert.Equal(fixture.Issued.ToArray(), fixture.Reauthorized.ToArray());
        Assert.DoesNotContain("节点", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadGraphAsync_MissingDisclosureCapability_DoesNotUseLegacyReaderAsync()
    {
        var fixture = new ControlledGraphFixture();
        using var service = fixture.Service(ownerAvailable: false);
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        Assert.NotEqual(KnowledgeReadStatus.Success, result.Status);
        Assert.Null(result.Graph);
        Assert.Empty(fixture.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("bytes")]
    [InlineData("output-digest")]
    [InlineData("page-digest")]
    [InlineData("profile")]
    [InlineData("capsule")]
    public async Task LoadGraphAsync_MalformedOwnerEnvelope_FailsClosedAsync(string corruption)
    {
        var fixture = new ControlledGraphFixture();
        fixture.Seeds = fixture.Seeds with { Documents = [fixture.Node(1)] };
        fixture.CorruptSearch = page => corruption switch
        {
            "bytes" => page with { OutputUtf8 = [123, 125] },
            "output-digest" => page with { OutputDigest = new string('0', 64) },
            "page-digest" => page with { PageDigest = new string('0', 64) },
            "profile" => page with { OutputProfileId = "untrusted-profile" },
            _ => page with { Dependencies = null! },
        };
        using var service = fixture.Service();
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        Assert.NotEqual(KnowledgeReadStatus.Success, result.Status);
        Assert.Null(result.Graph);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadGraphAsync_UnacceptedEdgeOrForeignSelection_FailsClosedAsync(bool foreignSelection)
    {
        var fixture = new ControlledGraphFixture();
        var node = fixture.Node(1);
        fixture.Seeds = fixture.Seeds with { Documents = [foreignSelection ? node with { Handle = node.Handle with { PublicationId = Guid.NewGuid() } } : node] };
        fixture.Related = _ => new(KnowledgeReadStatus.Success, [fixture.Edge(1, node, node) with { RelationshipVersionId = null }], [], null, null);
        using var service = fixture.Service();
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        Assert.NotEqual(KnowledgeReadStatus.Success, result.Status);
        Assert.Null(result.Graph);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadGraphAsync_ContextChangesDuringFinalCheck_DiscardsGraphAsync()
    {
        var fixture = new ControlledGraphFixture();
        fixture.Seeds = fixture.Seeds with { Documents = [fixture.Node(1)] };
        fixture.BeforeFinal = () =>
        {
            fixture.Host.Context.Current = new(fixture.Host.Fixture.Selection(KnowledgeMode.Business), true);
            fixture.Host.Context.NotifyChanged();
        };
        using var service = fixture.Service();
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        Assert.NotEqual(KnowledgeReadStatus.Success, result.Status);
        Assert.Null(result.Graph);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadGraphAsync_FinalOwnerIgnoresCancellation_HasNoOverallDeadlineButHonorsUserCancellationAsync()
    {
        var fixture = new ControlledGraphFixture { PendingFinal = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var service = fixture.Service();
        using var cancellation = new CancellationTokenSource();
        var loading = ((IExploreGraphService)service).LoadGraphAsync(cancellation.Token);
        Assert.Contains("final", fixture.Calls);
        fixture.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.False(loading.IsCompleted);
        fixture.Clock.Advance(TimeSpan.FromHours(1));
        Assert.False(loading.IsCompleted);
        cancellation.Cancel();
        var result = await loading.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotEqual(KnowledgeReadStatus.Success, result.Status);
        Assert.Null(result.Graph);
        Assert.True(fixture.FinalToken.IsCancellationRequested);
        fixture.PendingFinal.SetResult(KnowledgeDependencyStatus.Succeeded);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadGraphAsync_BusinessReceivesTechnicalEvidence_FailsWithoutAnyMetadataAsync()
    {
        var fixture = new ControlledGraphFixture();
        fixture.Host.Context.Current = new(fixture.Host.Fixture.Selection(KnowledgeMode.Business), true);
        fixture.Seeds = fixture.Seeds with { Documents = [fixture.Node(1, DocumentKind.BusinessLogic) with { QualifiedExcerpt = [fixture.Host.Fixture.SourceClaim] }] };
        using var service = fixture.Service();
        var result = await ((IExploreGraphService)service).LoadGraphAsync();
        Assert.NotEqual(KnowledgeReadStatus.Success, result.Status);
        Assert.Null(result.Graph);
        Assert.DoesNotContain("Order.cs", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }
}

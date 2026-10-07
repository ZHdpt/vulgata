using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberKnowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.Explore;

/// <summary>
/// Compiled Explore consumer probes against the imported ControlledFixture, not a host or production reader.
/// Its two small scripted chunks do not qualify oversized content, protected/expired cursors or cancellation.
/// </summary>
public sealed class KnowledgeReadContractTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_SharedSectionContinuation_PreservesIdentityAndOrderedQualifiedChunksAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        var handle = fixture.Handle(fixture.ClA, section: "overview");
        var request = new ReadKnowledgeRequest(selection.Token, handle);
        Assert.True(new ReadKnowledgeRequestValidator().Validate(request).IsValid);
        var first = new KnowledgeContentResponse(await reader.ReadExactAsync(new(new(request.SelectionToken), request.Document)));
        Assert.Equal(KnowledgeReadStatus.Success, first.Page.Status);
        Assert.Equal(handle, first.Page.Document!.Handle);
        Assert.NotNull(first.Page.Next);

        var continuation = request with { Continuation = first.Page.Next.Value };
        Assert.True(new ReadKnowledgeRequestValidator().Validate(continuation).IsValid);
        var second = new KnowledgeContentResponse(await reader.ReadExactAsync(new(new(continuation.SelectionToken), continuation.Document, new(continuation.Continuation!))));
        Assert.Equal(KnowledgeReadStatus.Success, second.Page.Status);
        Assert.Equal(handle, second.Page.Document!.Handle);
        Assert.Equal(fixture.ClA.LogicalDocumentId, second.Page.Document.Handle.LogicalDocumentId);
        Assert.Equal("overview", second.Page.Document.Handle.SectionId);
        Assert.Null(second.Page.Next);
        Assert.Equal(fixture.Documents[0].FirstChunk + fixture.Documents[0].SecondChunk, first.Page.Content + second.Page.Content);
        foreach (var page in new[] { first.Page, second.Page })
        {
            Assert.Equal(fixture.Claims.Select(claim => (claim.ClaimGroupId, claim.Text, claim.Qualification)),
                page.Claims.Select(claim => (claim.ClaimGroupId, claim.Text, claim.Qualification)));
            Assert.Equal(fixture.Claims.SelectMany(claim => claim.Evidence), page.Claims.SelectMany(claim => claim.Evidence));
            Assert.Equal(fixture.Claims.SelectMany(claim => claim.Limitations), page.Claims.SelectMany(claim => claim.Limitations));
            Assert.True(page.Coverage!.IsPartial);
            Assert.Equal(64 * 1024, KnowledgeContract.MaximumContentBytes);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_CursorUsedWithDifferentSelectionOrHandle_ReturnsMetadataFreeInvalidContinuationAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        var otherSelection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        var handle = fixture.Handle(fixture.ClA, section: "overview");
        var first = await reader.ReadExactAsync(new(selection, handle));
        Assert.NotNull(first.Next);
        foreach (var request in new[]
        {
            new ExactReadRequest(otherSelection, handle, first.Next),
            new ExactReadRequest(selection, handle with { SectionId = null }, first.Next),
            new ExactReadRequest(selection, fixture.Handle(fixture.ClB), first.Next),
            new ExactReadRequest(selection, handle, new("unissued-token")),
        })
        {
            AssertNoContent(await reader.ReadExactAsync(request), KnowledgeReadStatus.InvalidContinuation);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_UnknownExactIdentity_ReturnsNoMetadataOrNewerFallbackAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        var handle = fixture.Handle(fixture.ClA);
        foreach (var missing in new[]
        {
            handle with { PublicationId = OctoberKnowledgeFixture.Id(999) },
            handle with { LogicalDocumentId = OctoberKnowledgeFixture.Id(999) },
            handle with { DocumentVersionId = OctoberKnowledgeFixture.Id(999) },
            handle with { SectionId = "missing-section" },
        })
        {
            AssertNoContent(await reader.ReadExactAsync(new(selection, missing)), KnowledgeReadStatus.Unavailable);
        }
        var unknown = await reader.OpenReadSelectionAsync(fixture.Selection(publication: OctoberKnowledgeFixture.Id(999)));
        Assert.Equal(KnowledgeReadStatus.Unavailable, unknown.Status);
        Assert.Null(unknown.Selection);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_BusinessModeAndUncitedDeniedInput_ReturnsOnlyPermittedBusinessDocumentsAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection(KnowledgeMode.Business))).Selection!;
        var response = new KnowledgeSearchResponse(await reader.SearchAsync(new(selection, "")));
        Assert.Equal(KnowledgeReadStatus.Success, response.Page.Status);
        Assert.Equal(2, response.Page.Documents.Length);
        Assert.All(response.Page.Documents, document =>
        {
            Assert.Equal(DocumentKind.BusinessLogic, document.Kind);
            Assert.All(document.QualifiedExcerpt, claim => Assert.Empty(claim.Evidence));
        });
        Assert.DoesNotContain(response.Page.Documents, document => document.Handle == fixture.Handle(fixture.BlHidden));
        foreach (var denied in new[] { fixture.Handle(fixture.ClA), fixture.Handle(fixture.BlHidden) })
        {
            AssertNoContent(await reader.ReadExactAsync(new(selection, denied)), KnowledgeReadStatus.Unavailable);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, DisclosureStatus.Denied)]
    [InlineData(true, DisclosureStatus.Denied)]
    [InlineData(false, DisclosureStatus.AuthorityUnavailable)]
    [InlineData(true, DisclosureStatus.AuthorityUnavailable)]
    public async Task ReadExactAsync_AuthorityLostBeforeOrAtDisclosure_ReturnsNoRetainedChunkAsync(bool atDisclosure, DisclosureStatus status)
    {
        var fixture = new OctoberKnowledgeFixture();
        var policy = new ControlledDisclosurePolicy();
        var controlled = new ControlledKnowledgeReader(fixture, policy);
        IOctoberKnowledgeReader reader = controlled;
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        var handle = fixture.Handle(fixture.ClA, section: "overview");
        var first = await reader.ReadExactAsync(new(selection, handle));
        Assert.NotNull(first.Next);
        if (atDisclosure)
        {
            controlled.BeforeDisclosure = () => policy.LiveStatus = status;
        }
        else
        {
            policy.LiveStatus = status;
        }
        AssertNoContent(await reader.ReadExactAsync(new(selection, handle, first.Next)), KnowledgeReadStatus.Unavailable);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetRelatedAsync_GraphPurpose_PreservesDirectedExactHandlesForReaderNavigationAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var graphSelection = (await reader.OpenReadSelectionAsync(fixture.Selection() with { Purpose = KnowledgePurpose.Graph })).Selection!;
        var request = new RelatedKnowledgeRequest(graphSelection.Token, fixture.Handle(fixture.ClA));
        Assert.True(new RelatedKnowledgeRequestValidator().Validate(request).IsValid);
        var response = new KnowledgeRelatedResponse(await reader.GetRelatedAsync(new(new(request.SelectionToken), request.Document, request.PageSize)));
        Assert.Equal(KnowledgeReadStatus.Success, response.Page.Status);
        var edge = Assert.Single(response.Page.Relationships);
        Assert.Equal(RelationshipKind.References, edge.Kind);
        Assert.Equal(fixture.Handle(fixture.ClA), edge.From);
        Assert.Equal(fixture.Handle(fixture.ClB), edge.To);
        Assert.Equal(fixture.Relationship.Meaning, edge.Meaning);
        var uncertainty = Assert.Single(response.Page.PermittedUncertainties);
        Assert.Equal(fixture.Unknown.Key, uncertainty.Key);
        Assert.Equal(fixture.Unknown.Explanation, uncertainty.Explanation);
        Assert.Equal(fixture.Unknown.Evidence.ToArray(), uncertainty.Evidence.ToArray());
        Assert.Equal(fixture.Unknown.Limitations.ToArray(), uncertainty.Limitations.ToArray());
        var readerSelection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        foreach (var endpoint in new[] { edge.From, edge.To })
        {
            var content = await reader.ReadExactAsync(new(readerSelection, endpoint));
            Assert.Equal(KnowledgeReadStatus.Success, content.Status);
            Assert.Equal(endpoint, content.Document!.Handle);
        }
        Assert.Empty((await reader.GetRelatedAsync(new(graphSelection, edge.To))).Relationships);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_SharedBrowseContinuation_PreservesBoundAndRejectsChangedContextAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection())).Selection!;
        var request = new SearchKnowledgeRequest(selection.Token, PageSize: 1);
        var validator = new SearchKnowledgeRequestValidator();
        Assert.True(validator.Validate(request).IsValid);
        var first = await reader.SearchAsync(new(new(request.SelectionToken), request.Query, request.PageSize));
        Assert.Single(first.Documents);
        Assert.NotNull(first.Next);
        var nextRequest = request with { Continuation = first.Next.Value };
        var next = await reader.SearchAsync(new(new(nextRequest.SelectionToken), nextRequest.Query, nextRequest.PageSize, new(nextRequest.Continuation!)));
        Assert.Equal(KnowledgeReadStatus.Success, next.Status);
        Assert.NotEqual(first.Documents[0].Handle, Assert.Single(next.Documents).Handle);
        var otherSelection = (await reader.OpenReadSelectionAsync(fixture.Selection(KnowledgeMode.Business))).Selection!;
        foreach (var changed in new[]
        {
            new KnowledgeSearchRequest(selection, "changed", 1, first.Next),
            new KnowledgeSearchRequest(selection, "", 2, first.Next),
            new KnowledgeSearchRequest(otherSelection, "", 1, first.Next),
        })
        {
            var invalid = await reader.SearchAsync(changed);
            Assert.Equal(KnowledgeReadStatus.InvalidContinuation, invalid.Status);
            Assert.Empty(invalid.Documents);
            Assert.Null(invalid.Next);
            Assert.Null(invalid.Coverage);
        }
        foreach (var size in new[] { 0, KnowledgeContract.MaximumPageSize + 1 })
        {
            Assert.False(validator.Validate(request with { PageSize = size }).IsValid);
            Assert.False(new RelatedKnowledgeRequestValidator().Validate(new RelatedKnowledgeRequest(selection.Token, fixture.Handle(fixture.ClA), size)).IsValid);
        }
        Assert.True(validator.Validate(request with { PageSize = KnowledgeContract.MaximumPageSize }).IsValid);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetRelatedAsync_LaterUnrelatedSuccess_DoesNotRevalidatePreviouslyAccumulatedMaterialAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var policy = new ControlledDisclosurePolicy();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, policy);
        var selection = (await reader.OpenReadSelectionAsync(fixture.Selection() with { Purpose = KnowledgePurpose.Graph })).Selection!;
        var seeds = await reader.SearchAsync(new(selection, ""));
        Assert.Equal(KnowledgeReadStatus.Success, seeds.Status);
        Assert.Contains(seeds.Documents, document => document.Handle == fixture.Handle(fixture.ClA));
        var earlier = await reader.GetRelatedAsync(new(selection, fixture.Handle(fixture.ClA)));
        Assert.Equal(KnowledgeReadStatus.Success, earlier.Status);
        var relationship = Assert.Single(earlier.Relationships);
        Assert.Equal(fixture.Handle(fixture.ClA), relationship.From);
        Assert.Equal(fixture.Handle(fixture.ClB), relationship.To);

        policy.RevokedRepositories.Add(fixture.RepositoryA);

        // A final call for an independent permitted document is only authority for that call.
        // The imported interface/fixture supplies no accumulated-projection disclosure operation.
        var later = await reader.GetRelatedAsync(new(selection, fixture.Handle(fixture.ClB)));
        Assert.Equal(KnowledgeReadStatus.Success, later.Status);
        Assert.Empty(later.Relationships);
        var unrelatedContent = await reader.ReadExactAsync(new(selection, fixture.Handle(fixture.ClB)));
        Assert.Equal(KnowledgeReadStatus.Success, unrelatedContent.Status);
        Assert.Equal(fixture.Handle(fixture.ClB), unrelatedContent.Document!.Handle);
        AssertNoContent(await reader.ReadExactAsync(new(selection, relationship.From)), KnowledgeReadStatus.Unavailable);

        // Previously returned values remain in consumer memory; later success cannot grant redisclosure.
        Assert.Contains(seeds.Documents, document => document.Handle == relationship.From);
        Assert.Single(earlier.Relationships);
    }

    private static void AssertNoContent(ContentPage page, KnowledgeReadStatus expectedStatus)
    {
        Assert.Equal(expectedStatus, page.Status);
        Assert.Null(page.Document);
        Assert.Null(page.Content);
        Assert.Empty(page.Claims);
        Assert.Null(page.Next);
        Assert.Null(page.Coverage);
    }
}

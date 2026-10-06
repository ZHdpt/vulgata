using System.Text;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberKnowledge;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

/// <summary>
/// Compiled Chat consumer probes of the owner-issued contracts and ControlledFixture.
/// No Chat adapter, production authorization, persistence, provider or Web host is exercised.
/// </summary>
public sealed class OctoberChatKnowledgeConformanceTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeMode.IT)]
    [InlineData(KnowledgeMode.Business)]
    public async Task ReadExactAsync_ChatPurposeSharedDto_PreservesExactHandleAndQualifiedChunksAsync(KnowledgeMode mode)
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        ReadSelectionRequest context = fixture.Selection(mode) with { Purpose = KnowledgePurpose.Chat };
        ReadSelectionResult opened = await reader.OpenReadSelectionAsync(context);
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        ReadSelection selection = Assert.IsType<ReadSelection>(opened.Selection);
        DocumentHandle handle = fixture.Handle(mode == KnowledgeMode.Business ? fixture.BlA : fixture.ClA, section: "overview");
        var firstRequest = new ReadKnowledgeRequest(selection.Token, handle);
        Assert.True((await new ReadKnowledgeRequestValidator().ValidateAsync(firstRequest)).IsValid);

        ContentPage first = await reader.ReadExactAsync(new ExactReadRequest(selection, firstRequest.Document));
        Assert.Equal(KnowledgeReadStatus.Success, first.Status);
        ContinuationToken next = Assert.IsType<ContinuationToken>(first.Next);
        var nextRequest = firstRequest with { Continuation = next.Value };
        Assert.True((await new ReadKnowledgeRequestValidator().ValidateAsync(nextRequest)).IsValid);
        var secondResponse = new KnowledgeContentResponse(await reader.ReadExactAsync(
            new ExactReadRequest(selection, nextRequest.Document, new ContinuationToken(nextRequest.Continuation!))));

        Assert.Equal(KnowledgeReadStatus.Success, secondResponse.Page.Status);
        Assert.Equal(handle, first.Document!.Handle);
        Assert.Equal(handle, secondResponse.Page.Document!.Handle);
        Assert.Null(secondResponse.Page.Next);
        ControlledDocument expected = fixture.Documents.Single(document => document.Reference.DocumentVersionId == handle.DocumentVersionId);
        Assert.Equal(expected.FirstChunk, first.Content);
        Assert.Equal(expected.FirstChunk + expected.SecondChunk, first.Content + secondResponse.Page.Content);
        Assert.NotEqual(first.Content, first.Content + secondResponse.Page.Content);
        var utf8 = new UTF8Encoding(false, true);
        foreach (ContentPage page in new[] { first, secondResponse.Page })
        {
            Assert.InRange(utf8.GetByteCount(page.Content!), 1, KnowledgeContract.MaximumContentBytes);
            Assert.Equal(page.Content, utf8.GetString(utf8.GetBytes(page.Content!)));
            Assert.Contains(page.Claims, claim => claim.ClaimGroupId == fixture.SourceClaim.ClaimGroupId && claim.Qualification == ClaimQualification.SourceSupported);
            Assert.Contains(page.Claims, claim => claim.ClaimGroupId == fixture.AdvisoryClaim.ClaimGroupId && claim.Qualification == ClaimQualification.Advisory && claim.Limitations.Contains("AdvisoryNotSourceProof"));
        }

        // This is corpus coverage, not a claim that a prefix exhausted the content stream.
        Assert.True(first.Coverage!.IsPartial);
        Assert.True(secondResponse.Page.Coverage!.IsPartial);
        Assert.Equal("ControlledFixture", OctoberKnowledgeFixture.Provenance);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("token")]
    [InlineData("section")]
    [InlineData("selection")]
    public async Task ReadExactAsync_ChatContinuationChangedBinding_ReturnsNoContentAsync(string changed)
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        ReadSelectionRequest context = fixture.Selection() with { Purpose = KnowledgePurpose.Chat };
        ReadSelection selection = (await reader.OpenReadSelectionAsync(context)).Selection!;
        DocumentHandle handle = fixture.Handle(fixture.ClA, section: "overview");
        ContentPage first = await reader.ReadExactAsync(new ExactReadRequest(selection, handle));
        ContinuationToken continuation = Assert.IsType<ContinuationToken>(first.Next);

        if (changed == "token") continuation = new ContinuationToken(continuation.Value + "changed");
        if (changed == "section") handle = handle with { SectionId = null };
        if (changed == "selection") selection = (await reader.OpenReadSelectionAsync(context)).Selection!;
        ContentPage result = await reader.ReadExactAsync(new ExactReadRequest(selection, handle, continuation));

        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, result.Status);
        Assert.Null(result.Document);
        Assert.Null(result.Content);
        Assert.Empty(result.Claims);
        Assert.Null(result.Next);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_ChatContributorRevokedBetweenChunks_ReturnsNoFurtherContentAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        var policy = new ControlledDisclosurePolicy();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, policy);
        ReadSelection selection = (await reader.OpenReadSelectionAsync(fixture.Selection(KnowledgeMode.Business) with { Purpose = KnowledgePurpose.Chat })).Selection!;
        DocumentHandle handle = fixture.Handle(fixture.BlA, section: "overview");
        ContentPage first = await reader.ReadExactAsync(new ExactReadRequest(selection, handle));
        Assert.NotNull(first.Next);
        int priorChecks = policy.CheckCount;
        policy.RevokedRepositories.Add(fixture.RepositoryB);

        ContentPage result = await reader.ReadExactAsync(new ExactReadRequest(selection, handle, first.Next));

        Assert.Equal(KnowledgeReadStatus.Unavailable, result.Status);
        Assert.Null(result.Document);
        Assert.Null(result.Content);
        Assert.Empty(result.Claims);
        Assert.Null(result.Next);
        Assert.True(policy.CheckCount > priorChecks);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_BusinessChatSharedResponse_ExcludesClAndUncitedDeniedContributorAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        ReadSelection selection = (await reader.OpenReadSelectionAsync(fixture.Selection(KnowledgeMode.Business) with { Purpose = KnowledgePurpose.Chat })).Selection!;
        var request = new SearchKnowledgeRequest(selection.Token);
        Assert.True((await new SearchKnowledgeRequestValidator().ValidateAsync(request)).IsValid);
        var response = new KnowledgeSearchResponse(await reader.SearchAsync(new KnowledgeSearchRequest(selection, request.Query, request.PageSize)));

        Assert.Equal(KnowledgeReadStatus.Success, response.Page.Status);
        Assert.Equal(2, response.Page.Documents.Length);
        Assert.DoesNotContain(response.Page.Documents, document => document.Handle.DocumentVersionId == fixture.BlHidden.DocumentVersionId);
        Assert.All(response.Page.Documents, document =>
        {
            Assert.Equal(DocumentKind.BusinessLogic, document.Kind);
            Assert.All(document.QualifiedExcerpt, claim => Assert.Empty(claim.Evidence));
        });
        DocumentSummary visible = Assert.Single(response.Page.Documents, document => document.Handle.DocumentVersionId == fixture.BlA.DocumentVersionId);
        Assert.Contains(visible.QualifiedExcerpt, claim => claim.Qualification == ClaimQualification.Advisory && claim.Text == fixture.AdvisoryClaim.Text);
        ContentPage denied = await reader.ReadExactAsync(new ExactReadRequest(selection, fixture.Handle(fixture.ClA)));
        Assert.Equal(KnowledgeReadStatus.Unavailable, denied.Status);
        Assert.Null(denied.Document);
        Assert.Null(denied.Content);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetRelatedAsync_ChatSharedResponse_PreservesExactDirectionMeaningAndUnknownAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        ReadSelection selection = (await reader.OpenReadSelectionAsync(fixture.Selection() with { Purpose = KnowledgePurpose.Chat })).Selection!;
        var request = new RelatedKnowledgeRequest(selection.Token, fixture.Handle(fixture.ClA));
        Assert.True((await new RelatedKnowledgeRequestValidator().ValidateAsync(request)).IsValid);
        var response = new KnowledgeRelatedResponse(await reader.GetRelatedAsync(new RelatedReadRequest(selection, request.Document, request.PageSize)));

        Assert.Equal(KnowledgeReadStatus.Success, response.Page.Status);
        RelatedDocument relationship = Assert.Single(response.Page.Relationships);
        Assert.Equal(RelationshipKind.References, relationship.Kind);
        Assert.Equal(fixture.Handle(fixture.ClA), relationship.From);
        Assert.Equal(fixture.Handle(fixture.ClB), relationship.To);
        Assert.Equal(fixture.Relationship.Meaning, relationship.Meaning);
        Uncertainty uncertainty = Assert.Single(response.Page.PermittedUncertainties);
        Assert.Equal(fixture.Unknown.Key, uncertainty.Key);
        Assert.Equal(fixture.Unknown.Explanation, uncertainty.Explanation);
        Assert.Equal(fixture.SourceEvidence, Assert.Single(uncertainty.Evidence));
        Assert.Equal(Assert.Single(fixture.Unknown.Limitations), Assert.Single(uncertainty.Limitations));
        Assert.True(response.Page.Coverage!.IsPartial);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenReadSelectionAsync_ChatWrongDatabase_ReturnsUnavailableWithoutFallbackAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeReader reader = new ControlledKnowledgeReader(fixture, new ControlledDisclosurePolicy());
        ReadSelectionRequest request = fixture.Selection() with
        {
            Purpose = KnowledgePurpose.Chat,
            Publication = new PublicationBinding(fixture.Binding with { CanonicalDatabaseIdentity = "ControlledFixture/wrong" }, fixture.PublicationId),
        };

        ReadSelectionResult result = await reader.OpenReadSelectionAsync(request);

        Assert.Equal(KnowledgeReadStatus.Unavailable, result.Status);
        Assert.Null(result.Selection);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckAsync_ChatDisclosureRequest_UsesCompleteContributorUnionAsync()
    {
        var fixture = new OctoberKnowledgeFixture();
        IOctoberKnowledgeDisclosurePolicy policy = new ControlledDisclosurePolicy();
        ReadSelectionRequest context = fixture.Selection(KnowledgeMode.Business) with { Purpose = KnowledgePurpose.Chat };
        ControlledDocument hidden = fixture.Documents.Single(document => document.Reference == fixture.BlHidden);

        DisclosureStatus result = await policy.CheckAsync(new DisclosureRequest(context, hidden.Contributors, hidden.Kind));

        Assert.Equal(DisclosureStatus.Denied, result);
        Assert.DoesNotContain(hidden.Claims.SelectMany(claim => claim.Evidence), evidence => evidence.Document == fixture.ClC);
        Assert.Contains(fixture.SourceC, hidden.Contributors);
    }
}

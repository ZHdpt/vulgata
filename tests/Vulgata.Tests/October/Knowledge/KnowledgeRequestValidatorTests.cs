using System.Collections.Immutable;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class KnowledgeRequestValidatorTests
{
    private static DocumentHandle Handle => new(Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("20000000-0000-0000-0000-000000000001"), Guid.Parse("30000000-0000-0000-0000-000000000001"), Guid.Parse("40000000-0000-0000-0000-000000000001"), "section-a");

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_DefaultBrowse_PreservesBoundedDefaultsAndOpaqueToken()
    {
        var request = new SearchKnowledgeRequest("selection-token", Continuation: "opaque/+==:not-an-offset");
        Assert.Equal(20, request.PageSize);
        Assert.True(new SearchKnowledgeRequestValidator().Validate(request).IsValid);
        Assert.Equal("opaque/+==:not-an-offset", request.Continuation);
        Assert.Equal(64 * 1024, KnowledgeContract.MaximumContentBytes);
        Assert.Equal(TimeSpan.FromMinutes(30), KnowledgeContract.ContinuationLifetime);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Validate_PageSize_EnforcesSharedSearchAndRelatedBounds(int pageSize, bool expected)
    {
        Assert.Equal(expected, new SearchKnowledgeRequestValidator().Validate(new SearchKnowledgeRequest("selection", PageSize: pageSize)).IsValid);
        Assert.Equal(expected, new RelatedKnowledgeRequestValidator().Validate(new RelatedKnowledgeRequest("selection", Handle, pageSize)).IsValid);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(512, true)]
    [InlineData(513, false)]
    public void Validate_QueryLength_CountsUnicodeScalars(int count, bool expected)
    {
        var validator = new SearchKnowledgeRequestValidator();
        Assert.Equal(expected, validator.Validate(new SearchKnowledgeRequest("selection", new string('中', count))).IsValid);
        Assert.Equal(expected, validator.Validate(new SearchKnowledgeRequest("selection", string.Concat(Enumerable.Repeat("😀", count)))).IsValid);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_Query_RejectsNullOrBrokenUnicodeAndAllowsBrowse()
    {
        var validator = new SearchKnowledgeRequestValidator();
        Assert.False(validator.Validate(new SearchKnowledgeRequest("selection", null!)).IsValid);
        Assert.False(validator.Validate(new SearchKnowledgeRequest("selection", "\ud800")).IsValid);
        Assert.False(validator.Validate(new SearchKnowledgeRequest("selection", "\udc00")).IsValid);
        Assert.True(validator.Validate(new SearchKnowledgeRequest("selection", "")).IsValid);
        Assert.True(validator.Validate(new SearchKnowledgeRequest("selection", "  ")).IsValid);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptySelectionToken_RejectsAllReadRequests(string? token)
    {
        Assert.False(new SearchKnowledgeRequestValidator().Validate(new SearchKnowledgeRequest(token!)).IsValid);
        Assert.False(new ReadKnowledgeRequestValidator().Validate(new ReadKnowledgeRequest(token!, Handle)).IsValid);
        Assert.False(new RelatedKnowledgeRequestValidator().Validate(new RelatedKnowledgeRequest(token!, Handle)).IsValid);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("opaque/+==", true)]
    public void Validate_Continuation_ValidatesPresenceWithoutInterpretingAuthority(string? token, bool expected)
    {
        Assert.Equal(expected, new SearchKnowledgeRequestValidator().Validate(new SearchKnowledgeRequest("selection", Continuation: token)).IsValid);
        Assert.Equal(expected, new ReadKnowledgeRequestValidator().Validate(new ReadKnowledgeRequest("selection", Handle, token)).IsValid);
        Assert.Equal(expected, new RelatedKnowledgeRequestValidator().Validate(new RelatedKnowledgeRequest("selection", Handle, Continuation: token)).IsValid);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_ExactHandle_RejectsMissingIdentityAndBlankSection()
    {
        DocumentHandle[] invalid = [Handle with { BatchId = Guid.Empty }, Handle with { PublicationId = Guid.Empty }, Handle with { LogicalDocumentId = Guid.Empty }, Handle with { DocumentVersionId = Guid.Empty }, Handle with { SectionId = " " }, null!];
        foreach (var handle in invalid)
        {
            Assert.False(new ReadKnowledgeRequestValidator().Validate(new ReadKnowledgeRequest("selection", handle)).IsValid);
            Assert.False(new RelatedKnowledgeRequestValidator().Validate(new RelatedKnowledgeRequest("selection", handle)).IsValid);
        }
        Assert.True(new ReadKnowledgeRequestValidator().Validate(new ReadKnowledgeRequest("selection", Handle with { SectionId = null })).IsValid);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Validate_Selection_RejectsInvalidIdentityModeAndScopeWithoutRequiringManagement()
    {
        var validator = new OpenKnowledgeSelectionRequestValidator();
        var valid = new OpenKnowledgeSelectionRequest(Handle.BatchId, Handle.PublicationId, KnowledgeMode.IT, []);
        Assert.True(validator.Validate(valid).IsValid);
        Assert.True(validator.Validate(valid with { Mode = KnowledgeMode.Business }).IsValid);
        Assert.False(validator.Validate(valid with { BatchId = Guid.Empty }).IsValid);
        Assert.False(validator.Validate(valid with { PublicationId = Guid.Empty }).IsValid);
        Assert.False(validator.Validate(valid with { Mode = (KnowledgeMode)99 }).IsValid);
        Assert.False(validator.Validate(valid with { RepositoryScope = default }).IsValid);
        Assert.False(validator.Validate(valid with { RepositoryScope = [Guid.Empty] }).IsValid);
        Assert.False(validator.Validate(valid with { RepositoryScope = [Handle.BatchId, Handle.BatchId] }).IsValid);
    }
}


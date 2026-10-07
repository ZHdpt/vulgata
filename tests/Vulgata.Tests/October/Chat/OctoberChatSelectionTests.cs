using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.OctoberChat;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Services.OctoberChat;

namespace Vulgata.Tests.October.Chat;

public sealed class OctoberChatSelectionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task OpenAsync_OrdinaryReaderOneSeveralOrStandaloneScope_UsesExactCanonicalSelectionAsync(int scope)
    {
        var fixture = new ControlledChatFixture();
        ReadSelectionRequest request = fixture.Selection() with
        {
            RepositoryScope = scope switch
            {
                1 => [fixture.Knowledge.RepositoryA],
                2 => [fixture.Knowledge.RepositoryA, fixture.Knowledge.RepositoryB],
                _ => [fixture.Knowledge.RepositoryB],
            },
        };
        var service = new OctoberChatSelectionService(fixture.Reader, fixture.Policy);

        OctoberChatSelectionResult result = await service.OpenAsync(request);

        Assert.Equal(OctoberChatEntryStatus.Ready, result.Status);
        OctoberChatConversation conversation = Assert.IsType<OctoberChatConversation>(result.Conversation);
        Assert.NotEqual(Guid.Empty, conversation.ConversationId);
        Assert.NotEqual(Guid.Empty, conversation.ContextId);
        Assert.Equal("ordinary-user", conversation.Request.Subject.SubjectId);
        Assert.Equal(request.Publication, conversation.Request.Publication);
        Assert.Equal(KnowledgePurpose.Chat, conversation.Request.Purpose);
        Assert.Equal(request.Mode, conversation.Request.Mode);
        Assert.Equal(request.RepositoryScope.Order(), conversation.Request.RepositoryScope.Order());
        Assert.Single(fixture.Reader.OpenRequests);
        Assert.NotEmpty(result.Catalog!.Documents);
        Assert.True(fixture.Policy.CheckCount > 0);
        if (scope == 3)
        {
            Assert.Contains(result.Catalog.Documents, item => item.Title == "独立业务流程");
            Assert.DoesNotContain(result.Catalog.Documents, item => item.Handle.DocumentVersionId == fixture.Knowledge.ClA.DocumentVersionId);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_BusinessSelection_DoesNotExposeCodeMetadataOrHiddenContributorTitlesAsync()
    {
        var fixture = new ControlledChatFixture();
        var service = new OctoberChatSelectionService(fixture.Reader, fixture.Policy);

        OctoberChatSelectionResult result = await service.OpenAsync(fixture.Selection(KnowledgeMode.Business));

        Assert.Equal(OctoberChatEntryStatus.Ready, result.Status);
        Assert.Equal(2, result.Catalog!.Documents.Length);
        Assert.All(result.Catalog.Documents, document =>
        {
            Assert.Equal(DocumentKind.BusinessLogic, document.Kind);
            Assert.DoesNotContain("CL-", document.Title, StringComparison.Ordinal);
            Assert.DoesNotContain("src/", document.Title, StringComparison.Ordinal);
            Assert.All(document.QualifiedExcerpt, claim => Assert.Empty(claim.Evidence));
        });
        Assert.DoesNotContain(result.Catalog.Documents, document => document.Title == "隐藏业务标题");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_BusinessLogicNotPublished_ReturnsEmptyWithoutCodeFallbackAsync()
    {
        var fixture = new ControlledChatFixture();
        ReadSelectionRequest request = fixture.Selection(KnowledgeMode.Business);
        request = request with { Publication = request.Publication with { PublicationId = fixture.Knowledge.EarlyPublicationId } };

        OctoberChatSelectionResult result = await new OctoberChatSelectionService(fixture.Reader, fixture.Policy).OpenAsync(request);

        Assert.Equal(OctoberChatEntryStatus.Empty, result.Status);
        Assert.NotNull(result.Conversation);
        Assert.Empty(result.Catalog!.Documents);
        Assert.Equal(request.Publication, result.Conversation.Request.Publication);
        Assert.Single(fixture.Reader.OpenRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("database")]
    [InlineData("batch")]
    [InlineData("publication")]
    public async Task OpenAsync_WrongExactBinding_ReturnsMetadataFreeUnavailableWithoutRetryAsync(string mismatch)
    {
        var fixture = new ControlledChatFixture();
        ReadSelectionRequest request = fixture.Selection();
        request = mismatch switch
        {
            "database" => request with { Publication = request.Publication with { Knowledge = request.Publication.Knowledge with { CanonicalDatabaseIdentity = "other-database" } } },
            "batch" => request with { Publication = request.Publication with { Knowledge = request.Publication.Knowledge with { BatchId = Guid.NewGuid() } } },
            _ => request with { Publication = request.Publication with { PublicationId = Guid.NewGuid() } },
        };

        OctoberChatSelectionResult result = await new OctoberChatSelectionService(fixture.Reader, fixture.Policy).OpenAsync(request);

        Assert.Equal(OctoberChatEntryStatus.Unavailable, result.Status);
        Assert.Null(result.Conversation);
        Assert.Null(result.Catalog);
        Assert.Single(fixture.Reader.OpenRequests);
        Assert.Equal(0, fixture.Reader.SearchCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_MissingHostSelection_IsUnavailableWithoutReaderCallAsync()
    {
        var fixture = new ControlledChatFixture();

        OctoberChatSelectionResult result = await new OctoberChatSelectionService(fixture.Reader, fixture.Policy).OpenAsync(null);

        Assert.Equal(OctoberChatEntryStatus.Unavailable, result.Status);
        Assert.Null(result.Conversation);
        Assert.Null(result.Catalog);
        Assert.Empty(fixture.Reader.OpenRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("subject")]
    [InlineData("authentication")]
    [InlineData("purpose")]
    [InlineData("mode")]
    [InlineData("scope")]
    [InlineData("publication")]
    [InlineData("batch")]
    [InlineData("database")]
    public async Task OpenAsync_InvalidTrustedInput_IsRejectedByValidationBeforeReadAsync(string invalid)
    {
        var fixture = new ControlledChatFixture();
        ReadSelectionRequest request = fixture.Selection();
        request = invalid switch
        {
            "subject" => request with { Subject = request.Subject with { SubjectId = "" } },
            "authentication" => request with { Subject = request.Subject with { AuthenticationBinding = "" } },
            "purpose" => request with { Purpose = KnowledgePurpose.Synthesis },
            "mode" => request with { Mode = (KnowledgeMode)99 },
            "scope" => request with { RepositoryScope = [] },
            "publication" => request with { Publication = request.Publication with { PublicationId = Guid.Empty } },
            "batch" => request with { Publication = request.Publication with { Knowledge = request.Publication.Knowledge with { BatchId = Guid.Empty } } },
            _ => request with { Publication = request.Publication with { Knowledge = request.Publication.Knowledge with { CanonicalDatabaseIdentity = "" } } },
        };

        Assert.False((await new OctoberChatRequestValidator().ValidateAsync(request)).IsValid);
        OctoberChatSelectionResult result = await new OctoberChatSelectionService(fixture.Reader, fixture.Policy).OpenAsync(request);

        Assert.Equal(OctoberChatEntryStatus.InvalidSelection, result.Status);
        Assert.Null(result.Catalog);
        Assert.Null(result.Conversation);
        Assert.Empty(fixture.Reader.OpenRequests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("authority")]
    [InlineData("open")]
    [InlineData("browse")]
    [InlineData("exception")]
    public async Task OpenAsync_ServiceOrAuthorityOutage_FailsClosedWithNoFallbackAsync(string failure)
    {
        var fixture = new ControlledChatFixture();
        if (failure == "authority") fixture.Policy.LiveStatus = DisclosureStatus.AuthorityUnavailable;
        if (failure == "open") fixture.Reader.OpenStatus = KnowledgeReadStatus.UnavailableService;
        if (failure == "browse") fixture.Reader.SearchStatus = KnowledgeReadStatus.UnavailableService;
        if (failure == "exception") fixture.Reader.ThrowOnOpen = true;

        OctoberChatSelectionResult result = await new OctoberChatSelectionService(fixture.Reader, fixture.Policy).OpenAsync(fixture.Selection());

        Assert.Equal(OctoberChatEntryStatus.Unavailable, result.Status);
        Assert.Null(result.Catalog);
        Assert.Null(result.Conversation);
        Assert.InRange(fixture.Reader.OpenRequests.Count, 0, 1);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_AuthorityRevokedDuringBrowse_DoesNotDisclosePriorCatalogAsync()
    {
        var fixture = new ControlledChatFixture();
        fixture.CanonicalReader.BeforeDisclosure = () => fixture.Policy.LiveStatus = DisclosureStatus.Denied;

        OctoberChatSelectionResult result = await new OctoberChatSelectionService(fixture.Reader, fixture.Policy).OpenAsync(fixture.Selection());

        Assert.Equal(OctoberChatEntryStatus.Unavailable, result.Status);
        Assert.Null(result.Conversation);
        Assert.Null(result.Catalog);
    }
}

using System.Text.Json;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberChat;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

/// <summary>Controlled typed mapping and ledger checks, not inference disclosure authorization.</summary>
public sealed class OctoberWikiToolTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("SearchWiki", "订单")]
    [InlineData("BrowseWiki", "")]
    public async Task InvokeAsync_Discovery_UsesBoundSelectionAndDoesNotGrantReadCreditAsync(string name, string query)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync(KnowledgeMode.Business);

        OctoberChatToolResult result = await harness.Tools.InvokeAsync(name, JsonSerializer.Serialize(new { query, pageSize = 2 }));

        Assert.Equal(OctoberChatToolStatus.Succeeded, result.Status);
        KnowledgeSearchRequest sent = Assert.Single(fixture.Reader.SearchRequests);
        Assert.Equal(harness.Binding.Selection, sent.Selection);
        Assert.Equal(query, sent.Query);
        Assert.Equal(2, sent.PageSize);
        Assert.All(result.Search!.Documents, document => Assert.Equal(DocumentKind.BusinessLogic, document.Kind));
        Assert.Empty(harness.Ledger.Snapshot);
        Assert.Equal(1, harness.Budget.ToolCalls);
        Assert.Equal(0, harness.Budget.ActiveTools);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("UnknownTool", "{}")]
    [InlineData("ReadWiki", "{")]
    [InlineData("BrowseWiki", "{\"subject\":\"administrator\"}")]
    [InlineData("BrowseWiki", "{\"publicationId\":\"foreign\"}")]
    [InlineData("BrowseWiki", "{\"selection\":\"foreign\"}")]
    [InlineData("BrowseWiki", "{\"pageSize\":21}")]
    [InlineData("BrowseWiki", "{\"pageSize\":0}")]
    [InlineData("BrowseWiki", "{\"pageSize\":1,\"pageSize\":2}")]
    [InlineData("ReadWiki", "{\"document\":null}")]
    public async Task InvokeAsync_MalformedOrAuthorityOverride_CountsRefusalWithoutCallingReaderAsync(string name, string arguments)
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();

        OctoberChatToolResult result = await harness.Tools.InvokeAsync(name, arguments);

        Assert.Equal(OctoberChatToolStatus.InvalidArguments, result.Status);
        Assert.Equal(1, harness.Budget.ToolCalls);
        Assert.Equal(0, harness.Budget.ActiveTools);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.Empty(fixture.Reader.ExactRequests);
        Assert.Empty(harness.Ledger.Snapshot);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InvokeAsync_RepeatedRefusedCalls_ExhaustsOriginalTurnBudgetAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        for (int index = 0; index < 16; index++)
        {
            Assert.Equal(OctoberChatToolStatus.InvalidArguments, (await harness.Tools.InvokeAsync("invented", "{}")).Status);
        }

        OctoberChatToolResult exhausted = await harness.Tools.InvokeAsync("BrowseWiki", "{}");

        Assert.Equal(OctoberChatToolStatus.BudgetExceeded, exhausted.Status);
        Assert.Empty(fixture.Reader.SearchRequests);
        Assert.True(harness.Budget.IsStopped);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InvokeAsync_BusinessExactCodeRead_IsMetadataFreeUnavailableAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync(KnowledgeMode.Business);

        OctoberChatToolResult result = await harness.Tools.InvokeAsync("ReadWiki", JsonSerializer.Serialize(new { document = fixture.Knowledge.Handle(fixture.Knowledge.ClA) }));

        Assert.Equal(OctoberChatToolStatus.Unavailable, result.Status);
        Assert.Null(result.Content);
        Assert.Empty(harness.Ledger.Snapshot);
        Assert.Equal(harness.Binding.Selection, Assert.Single(fixture.Reader.ExactRequests).Selection);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InvokeAsync_ReportRequiresCurrentTurnExactContent_ReadsDoNotAuthorizeOtherTurnsOrSectionsAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        DocumentHandle root = fixture.Knowledge.Handle(fixture.Knowledge.ClA);
        DocumentHandle section = root with { SectionId = "overview" };
        await harness.Tools.InvokeAsync("BrowseWiki", "{}");
        Assert.Equal(OctoberChatToolStatus.InvalidReport, (await harness.Tools.InvokeAsync("ReportAnswerDocuments", JsonSerializer.Serialize(new { documents = new[] { root } }))).Status);
        Assert.Equal(OctoberChatToolStatus.Succeeded, (await harness.Tools.InvokeAsync("ReadWiki", JsonSerializer.Serialize(new { document = section }))).Status);
        Assert.Equal(OctoberChatToolStatus.InvalidReport, (await harness.Tools.InvokeAsync("ReportAnswerDocuments", JsonSerializer.Serialize(new { documents = new[] { root } }))).Status);
        Assert.Equal(OctoberChatToolStatus.Succeeded, (await harness.Tools.InvokeAsync("ReportAnswerDocuments", JsonSerializer.Serialize(new { documents = new[] { section } }))).Status);

        ControlledChatFixture.WikiHarness otherTurn = await fixture.OpenWikiAsync();
        OctoberChatToolResult foreign = await otherTurn.Tools.InvokeAsync("ReportAnswerDocuments", JsonSerializer.Serialize(new { documents = new[] { section } }));

        Assert.Equal(OctoberChatToolStatus.InvalidReport, foreign.Status);
        Assert.Empty(otherTurn.Ledger.Snapshot);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InvokeAsync_Reports_DeduplicatesFirstReportOrderAndAcceptsEmptyWithoutNewReadsAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        DocumentHandle first = fixture.Knowledge.Handle(fixture.Knowledge.ClA);
        DocumentHandle second = fixture.Knowledge.Handle(fixture.Knowledge.ClB);
        OctoberChatToolResult empty = await harness.Tools.InvokeAsync("ReportAnswerDocuments", "{\"documents\":[]}");
        Assert.Equal(OctoberChatToolStatus.Succeeded, empty.Status);
        Assert.Empty(empty.References);
        await harness.Tools.InvokeAsync("ReadWiki", JsonSerializer.Serialize(new { document = first }));
        await harness.Tools.InvokeAsync("ReadWiki", JsonSerializer.Serialize(new { document = second }));

        OctoberChatToolResult report = await harness.Tools.InvokeAsync("ReportAnswerDocuments", JsonSerializer.Serialize(new { documents = new[] { second, first, second } }));

        Assert.Equal(OctoberChatToolStatus.Succeeded, report.Status);
        Assert.Equal(new[] { second, first }, report.References.Select(item => item.Document));
        Assert.False(report.References[0].IsPartial);
        Assert.True(report.References[1].IsPartial);
        Assert.Equal(2, fixture.Reader.ExactRequests.Count);
        OctoberChatToolResult repeated = await harness.Tools.InvokeAsync("ReportAnswerDocuments", JsonSerializer.Serialize(new { documents = new[] { first } }));
        Assert.Equal(new[] { second, first }, repeated.References.Select(item => item.Document));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InvokeAsync_Related_PreservesExistingDirectionAndUnknownWithoutTargetReadCreditAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        DocumentHandle source = fixture.Knowledge.Handle(fixture.Knowledge.ClA);
        DocumentHandle target = fixture.Knowledge.Handle(fixture.Knowledge.ClB);

        OctoberChatToolResult result = await harness.Tools.InvokeAsync("GetRelatedDocuments", JsonSerializer.Serialize(new { document = source, pageSize = 20 }));

        Assert.Equal(OctoberChatToolStatus.Succeeded, result.Status);
        RelatedDocument relationship = Assert.Single(result.Related!.Relationships);
        Assert.Equal(source, relationship.From);
        Assert.Equal(target, relationship.To);
        Assert.Equal(RelationshipKind.References, relationship.Kind);
        Assert.Equal(fixture.Knowledge.Relationship.Meaning, relationship.Meaning);
        Uncertainty unknown = Assert.Single(result.Related.PermittedUncertainties);
        Assert.Equal(fixture.Knowledge.Unknown.Key, unknown.Key);
        Assert.Equal(fixture.Knowledge.Unknown.Explanation, unknown.Explanation);
        Assert.Equal(fixture.Knowledge.Unknown.Evidence.ToArray(), unknown.Evidence.ToArray());
        Assert.Equal(fixture.Knowledge.Unknown.Limitations.ToArray(), unknown.Limitations.ToArray());
        Assert.Empty(harness.Ledger.Snapshot);
        OctoberChatToolResult reverse = await harness.Tools.InvokeAsync("GetRelatedDocuments", JsonSerializer.Serialize(new { document = target }));
        Assert.Empty(reverse.Related!.Relationships);
        Assert.Equal(OctoberChatToolStatus.InvalidReport, (await harness.Tools.InvokeAsync("ReportAnswerDocuments", JsonSerializer.Serialize(new { documents = new[] { target } }))).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InvokeAsync_StricterPageLimit_RejectsLargerCallRatherThanSilentlyClampingAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync(budget: new OctoberChatBudget(new OctoberChatLimits { PageSize = 1 }));

        Assert.Equal(OctoberChatToolStatus.InvalidArguments, (await harness.Tools.InvokeAsync("BrowseWiki", "{\"pageSize\":2}")).Status);
        OctoberChatToolResult valid = await harness.Tools.InvokeAsync("BrowseWiki", "{\"pageSize\":1}");

        Assert.Equal(OctoberChatToolStatus.Succeeded, valid.Status);
        Assert.Single(valid.Search!.Documents);
        Assert.Equal(1, Assert.Single(fixture.Reader.SearchRequests).PageSize);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InvokeAsync_ForeignPublicationAndMismatchedReturnedDocument_NeverEarnReadCreditAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        DocumentHandle handle = fixture.Knowledge.Handle(fixture.Knowledge.ClA);
        Assert.Equal(OctoberChatToolStatus.Unavailable, (await harness.Tools.InvokeAsync("ReadWiki", JsonSerializer.Serialize(new { document = handle with { PublicationId = Guid.NewGuid() } }))).Status);
        fixture.Reader.ProjectContent = (page, _) => page with { Document = page.Document! with { Handle = fixture.Knowledge.Handle(fixture.Knowledge.ClB) } };

        OctoberChatToolResult mismatched = await harness.Tools.InvokeAsync("ReadWiki", JsonSerializer.Serialize(new { document = handle }));

        Assert.Equal(OctoberChatToolStatus.Unavailable, mismatched.Status);
        Assert.Null(mismatched.Content);
        Assert.Empty(harness.Ledger.Snapshot);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Constructor_LedgerFromAnotherTurn_RejectsCrossTurnCreditReuseAsync()
    {
        var fixture = new ControlledChatFixture();
        ControlledChatFixture.WikiHarness harness = await fixture.OpenWikiAsync();
        OctoberChatTurnBinding other = harness.Binding with { TurnId = Guid.NewGuid() };

        Assert.Throws<ArgumentException>(() => new OctoberWikiTools(fixture.Reader, other, harness.Budget, harness.Ledger));
    }
}

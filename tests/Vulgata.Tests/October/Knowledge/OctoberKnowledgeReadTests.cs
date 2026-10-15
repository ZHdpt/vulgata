using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberKnowledgeReadTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_LargeExcerpts_ReducesPageWithoutLosingContinuationAsync()
    {
        var fixture = new B0bReadFixture();
        var original = fixture.Source.Value.Documents[0];
        var evidence = original.Sections[0].Claims[0].Evidence[0] with { ClaimGroupId = new string('x', 20_000) };
        var claim = original.Sections[0].Claims[0] with { Evidence = [evidence], Limitations = [new string('z', 200_000)] };
        fixture.Source.Value = fixture.Source.Value with
        {
            Documents =
            [
                .. Enumerable.Range(0, 4).Select(_ => original with
                {
                    Reference = new(original.Reference.BatchId, Guid.NewGuid(), Guid.NewGuid()),
                    Sections = [original.Sections[0] with { Claims = [claim] }]
                })
            ]
        };
        var selection = await fixture.OpenAsync();
        var first = await fixture.Reader.SearchAsync(new(selection, "", 4));
        Assert.Equal(KnowledgeReadStatus.Success, first.Status);
        Assert.InRange(first.Documents.Length, 1, 3);
        Assert.NotNull(first.Next);
        Assert.All(first.Documents.SelectMany(x => x.QualifiedExcerpt).SelectMany(x => x.Limitations), text => Assert.True(text.Length <= 256));
        var second = await fixture.Reader.SearchAsync(new(selection, "", 4, first.Next));
        Assert.Equal(KnowledgeReadStatus.Success, second.Status);
        Assert.Equal(4, first.Documents.Length + second.Documents.Length);
        Assert.Empty(first.Documents.Select(x => x.Handle).Intersect(second.Documents.Select(x => x.Handle)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task IndexedPublication_LoadsOnlyPageContent_AndPreservesSearchAndExactSectionsAsync()
    {
        var fixture = new B0bReadFixture();
        var source = new IndexedReadSource(fixture.Source.Value);
        var tokens = new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider());
        var reader = new OctoberKnowledgeReader(source, fixture.Publication, fixture.Policy,
            new OctoberKnowledgeCursorProtector(tokens, fixture.Clock), fixture.Log);
        var opened = await reader.OpenReadSelectionAsync(fixture.Request());
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        Assert.Empty(source.Reads);
        var page = await reader.SearchAsync(new(opened.Selection!, "", 1));
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        Assert.Single(source.Reads);
        Assert.Single(page.Documents);
        source.Reads.Clear();
        var search = await reader.SearchAsync(new(opened.Selection!, "accepted", 1));
        Assert.Equal(KnowledgeReadStatus.Success, search.Status);
        Assert.Single(search.Documents);
        Assert.DoesNotContain(fixture.HiddenBl, source.Reads);
        var exact = await reader.ReadExactAsync(new(opened.Selection!, fixture.Handle(fixture.Cl) with { SectionId = "overview" }));
        Assert.Equal(KnowledgeReadStatus.Success, exact.Status);
        Assert.Contains("accepted order", exact.Content);
        fixture.Policy.Status = DisclosureStatus.Denied;
        source.Reads.Clear();
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await reader.ReadExactAsync(new(opened.Selection!, fixture.Handle(fixture.Cl)))).Status);
        Assert.Empty(source.Reads);
    }

    private sealed class IndexedReadSource(OctoberPublishedKnowledge value) : IOctoberPublishedKnowledgeIndexSource
    {
        public List<DocumentVersionReference> Reads { get; } = [];

        public Task<OctoberPublishedKnowledge?> LoadAsync(PublicationBinding publication, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Whole publication content must not be loaded.");

        public Task<OctoberPublishedKnowledge?> LoadIndexAsync(PublicationBinding publication, CancellationToken cancellationToken = default) =>
            Task.FromResult<OctoberPublishedKnowledge?>(value with { Documents = value.Documents.Select(x => x with { ContentJson = "", Sections = [] }).ToImmutableArray() });

        public Task<OctoberPublishedDocument?> LoadDocumentAsync(PublicationBinding publication, OctoberPublishedDocument document, CancellationToken cancellationToken = default)
        {
            Reads.Add(document.Reference);
            return Task.FromResult<OctoberPublishedDocument?>(value.Documents.Single(x => x.Reference == document.Reference));
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("search")]
    [InlineData("exact")]
    [InlineData("related")]
    public async Task ReadAsync_LargePublication_DoesNotRepeatLiveChecksPerUnrelatedDocumentAsync(string operation)
    {
        var fixture = new B0bReadFixture();
        var original = fixture.Source.Value.Documents.Single(x => x.Reference == fixture.Cl);
        var documents = Enumerable.Range(0, 163).Select(index => original with
        {
            Reference = index == 0 ? original.Reference : new(original.Reference.BatchId, Guid.NewGuid(), Guid.NewGuid()),
            Contributors = original.Contributors.Select(contributor => contributor with
            {
                ContentDigest = index.ToString("x64")
            }).ToImmutableArray()
        }).ToImmutableArray();
        fixture.Source.Value = fixture.Source.Value with { Documents = documents, Coverage = [], Relationships = [], Uncertainties = [] };
        var selection = await fixture.OpenAsync();
        fixture.Policy.Requests.Clear();
        var status = operation switch
        {
            "search" => (await fixture.Reader.SearchAsync(new(selection, ""))).Status,
            "exact" => (await fixture.Reader.ReadExactAsync(new(selection, fixture.Handle(fixture.Cl)))).Status,
            _ => (await fixture.Reader.GetRelatedAsync(new(selection, fixture.Handle(fixture.Cl)))).Status
        };
        Assert.Equal(KnowledgeReadStatus.Success, status);
        Assert.InRange(fixture.Policy.Requests.Count, 3, 10);
        var disclosed = fixture.Policy.Requests.Where(x => x.Kind is not null).ToArray();
        Assert.NotEmpty(disclosed);
        var expected = (operation == "search" ? documents : [documents[0]])
            .SelectMany(x => x.Contributors).ToHashSet();
        Assert.All(disclosed, request => Assert.True(expected.SetEquals(request.AllContributors)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_DeniedContributorGroup_StillReturnsPermittedDocumentsAndRechecksAfterLogAsync()
    {
        var fixture = new B0bReadFixture();
        var selection = await fixture.OpenAsync();
        var page = await fixture.Reader.SearchAsync(new(selection, ""));
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        Assert.Equal(3, page.Documents.Length);
        Assert.DoesNotContain(page.Documents, item => item.Handle == fixture.Handle(fixture.HiddenBl));
        fixture.Log.AfterAppend = () => fixture.Policy.Status = DisclosureStatus.Denied;
        var revoked = await fixture.Reader.SearchAsync(new(selection, ""));
        Assert.Equal(KnowledgeReadStatus.Unavailable, revoked.Status);
        Assert.Empty(revoked.Documents);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAndReadAsync_ExactOrdinaryItSelection_PreservesPublicationQualificationsAndDirectionAsync()
    {
        var fixture = new B0bReadFixture();
        var selection = await fixture.OpenAsync();
        var search = await fixture.Reader.SearchAsync(new(selection, ""));
        Assert.Equal(KnowledgeReadStatus.Success, search.Status);
        Assert.Equal(3, search.Documents.Length);
        Assert.DoesNotContain(search.Documents, document => document.Title == "HIDDEN-BL-TITLE");
        var content = await fixture.Reader.ReadExactAsync(new(selection, fixture.Handle(fixture.Cl)));
        Assert.Equal(KnowledgeReadStatus.Success, content.Status);
        Assert.Equal(fixture.Handle(fixture.Cl), content.Document!.Handle);
        Assert.Contains(content.Claims, claim => claim.Qualification == ClaimQualification.SourceSupported);
        Assert.Contains(content.Claims, claim => claim.Qualification == ClaimQualification.Advisory);
        Assert.Contains("建议性上下文", content.Content!, StringComparison.Ordinal);
        var related = await fixture.Reader.GetRelatedAsync(new(selection, fixture.Handle(fixture.Bl)));
        var edge = Assert.Single(related.Relationships);
        Assert.Equal(fixture.Handle(fixture.Bl), edge.From);
        Assert.Equal(fixture.Handle(fixture.Cl), edge.To);
        Assert.Equal(RelationshipKind.GeneratedFrom, edge.Kind);
        Assert.Contains(fixture.Log.Entries, entry => entry.Operation == KnowledgeReadOperation.ReadExact);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_HugeUnicodeClaim_CompletesWithBoundedQualifiedUtf8PagesAsync()
    {
        var fixture = new B0bReadFixture(large: true);
        var selection = await fixture.OpenAsync();
        var handle = fixture.Handle(fixture.Cl) with { SectionId = "overview" };
        var full = new StringBuilder();
        ContinuationToken? continuation = null;
        int pages = 0;
        do
        {
            var page = await fixture.Reader.ReadExactAsync(new(selection, handle, continuation));
            Assert.Equal(KnowledgeReadStatus.Success, page.Status);
            Assert.Equal(handle, page.Document!.Handle);
            Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(page).Length, 1, KnowledgeContract.MaximumContentBytes);
            var strictUtf8 = new UTF8Encoding(false, true);
            Assert.Equal(page.Content, strictUtf8.GetString(strictUtf8.GetBytes(page.Content!)));
            Assert.NotEmpty(page.Claims);
            Assert.All(page.Claims, claim => Assert.True(Enum.IsDefined(claim.Qualification)));
            full.Append(page.Content);
            continuation = page.Next;
            pages++;
            Assert.InRange(pages, 1, 1000);
        } while (continuation is not null);

        Assert.True(pages > 1);
        string reconstructed = full.ToString();
        Assert.Contains(fixture.LargeText, reconstructed, StringComparison.Ordinal);
        Assert.Equal(1, Count(reconstructed, "BEGIN-EXACT"));
        Assert.Equal(1, Count(reconstructed, "END-EXACT"));
        Assert.Equal(16000, Count(reconstructed, "中文😀"));
        Assert.Contains("来源支持", reconstructed, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_ProtectedContinuation_BindsQueryLimitSelectionModeSubjectAndExpiryAsync()
    {
        var fixture = new B0bReadFixture();
        var selection = await fixture.OpenAsync();
        var first = await fixture.Reader.SearchAsync(new(selection, "", 1));
        Assert.Equal(KnowledgeReadStatus.Success, first.Status);
        Assert.NotNull(first.Next);

        Assert.DoesNotContain(fixture.Publication.PublicationId.ToString(), first.Next!.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(KnowledgeReadStatus.Success, (await fixture.Reader.SearchAsync(new(selection, "", 1, first.Next))).Status);
        foreach (var bad in new[]
                 {
                     new KnowledgeSearchRequest(selection, "changed-query", 1, first.Next), new(selection, "", 2, first.Next),
                     new(selection, "", 1, new(first.Next.Value + "tamper"))
                 })
            Assert.Equal(KnowledgeReadStatus.InvalidContinuation, (await fixture.Reader.SearchAsync(bad)).Status);
        var business = await fixture.OpenAsync(KnowledgeMode.Business);
        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, (await fixture.Reader.SearchAsync(new(business, "", 1, first.Next))).Status);
        fixture.Policy.CurrentSubject = "other-user";
        var other = await fixture.OpenAsync(subject: "other-user");
        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, (await fixture.Reader.SearchAsync(new(other, "", 1, first.Next))).Status);
        fixture.Policy.CurrentSubject = "ordinary-user";
        fixture.Clock.Advance(TimeSpan.FromMinutes(31));
        var expired = await fixture.Reader.SearchAsync(new(selection, "", 1, first.Next));
        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, expired.Status);
        Assert.Empty(expired.Documents);
        Assert.Null(expired.Next);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAndReadAsync_BusinessMode_ExcludesTechnicalAndUncitedContributorMetadataAsync()
    {
        var fixture = new B0bReadFixture();
        var business = await fixture.OpenAsync(KnowledgeMode.Business);
        var page = await fixture.Reader.SearchAsync(new(business, "", 1));
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        Assert.Equal(fixture.Bl.DocumentVersionId, Assert.Single(page.Documents).Handle.DocumentVersionId);
        Assert.Null(page.Next);
        var content = await fixture.Reader.ReadExactAsync(new(business, fixture.Handle(fixture.Bl)));
        Assert.Equal(KnowledgeReadStatus.Success, content.Status);
        string serialized = JsonSerializer.Serialize(content);
        Assert.DoesNotContain(fixture.Cl.DocumentVersionId.ToString(), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TECHNICAL-SECRET", serialized, StringComparison.Ordinal);
        Assert.All(content.Claims, claim => Assert.Empty(claim.Evidence));
        Assert.Contains(content.Claims, claim => claim.Qualification == ClaimQualification.Advisory);
        var related = await fixture.Reader.GetRelatedAsync(new(business, fixture.Handle(fixture.Bl)));
        Assert.Empty(related.Relationships);
        Assert.Empty(related.PermittedUncertainties);
        foreach (var reference in new[] { fixture.Cl, fixture.HiddenBl }) AssertUnavailable(await fixture.Reader.ReadExactAsync(new(business, fixture.Handle(reference))));
        Assert.DoesNotContain("HIDDEN-GAP", JsonSerializer.Serialize(page), StringComparison.Ordinal);
        Assert.Empty(page.Coverage!.PermittedGaps);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("source")]
    [InlineData("log")]
    public async Task ReadExactAsync_AuthorityRevokedDuringMaterializationOrLogging_DiscardsWholeResponseAsync(string stage)
    {
        var fixture = new B0bReadFixture();
        var selection = await fixture.OpenAsync();
        if (stage == "source") fixture.Source.BeforeReturn = () => fixture.Policy.Status = DisclosureStatus.Denied;
        else fixture.Log.AfterAppend = () => fixture.Policy.Status = DisclosureStatus.Denied;
        AssertUnavailable(await fixture.Reader.ReadExactAsync(new(selection, fixture.Handle(fixture.Cl))));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("budget", KnowledgeReadStatus.ReadBudgetExceeded)]
    [InlineData("outage", KnowledgeReadStatus.UnavailableService)]
    [InlineData("log", KnowledgeReadStatus.UnavailableService)]
    [InlineData("cancel", KnowledgeReadStatus.Cancelled)]
    public async Task ReadExactAsync_BudgetOutageLogFailureOrCancellation_ReturnsNoPartialMetadataAsync(string failure, KnowledgeReadStatus expected)
    {
        var fixture = new B0bReadFixture();
        var selection = await fixture.OpenAsync();
        using var cancellation = new CancellationTokenSource();
        if (failure == "budget") fixture.Source.Failure = new OctoberKnowledgeReadBudgetException();
        if (failure == "outage") fixture.Source.Failure = new IOException("Controlled source outage");
        if (failure == "log") fixture.Log.Succeeds = false;
        if (failure == "cancel") fixture.Source.BeforeReturn = cancellation.Cancel;
        var result = await fixture.Reader.ReadExactAsync(new(selection, fixture.Handle(fixture.Cl)), cancellation.Token);
        Assert.Equal(expected, result.Status);
        Assert.Null(result.Document);
        Assert.Null(result.Content);
        Assert.Empty(result.Claims);
        Assert.Null(result.Next);
        Assert.Null(result.Coverage);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_ForeignPublicationVersionOrSection_ReturnsIndistinguishableUnavailableAsync()
    {
        var fixture = new B0bReadFixture();
        var selection = await fixture.OpenAsync();
        var handle = fixture.Handle(fixture.Cl);
        foreach (var invalid in new[]
                 {
                     handle with { PublicationId = Guid.NewGuid() }, handle with { BatchId = Guid.NewGuid() }, handle with { DocumentVersionId = Guid.NewGuid() },
                     handle with { SectionId = "missing" }
                 })
            AssertUnavailable(await fixture.Reader.ReadExactAsync(new(selection, invalid)));
        var foreign = fixture.Request() with
        {
            Publication = fixture.Publication with { Knowledge = fixture.Publication.Knowledge with { CanonicalDatabaseIdentity = "wrong-database" } }
        };
        Assert.Equal(KnowledgeReadStatus.Unavailable, (await fixture.Reader.OpenReadSelectionAsync(foreign)).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_EmptyScopeAndHiddenOnlyRemainder_ReturnsNoDocumentsCursorOrGapMetadataAsync()
    {
        var fixture = new B0bReadFixture();
        var opened = await fixture.Reader.OpenReadSelectionAsync(fixture.Request() with { RepositoryScope = [] });
        Assert.Equal(KnowledgeReadStatus.Success, opened.Status);
        var page = await fixture.Reader.SearchAsync(new(opened.Selection!, ""));
        Assert.Equal(KnowledgeReadStatus.Success, page.Status);
        Assert.Empty(page.Documents);
        Assert.Null(page.Next);
        Assert.Empty(page.Coverage!.PermittedGaps);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SearchAsync_DirectPortInvalidLimitsOrUnicode_ReturnsNoMetadataAsync()
    {
        var fixture = new B0bReadFixture();
        var selection = await fixture.OpenAsync();
        foreach (var request in new[] { new KnowledgeSearchRequest(selection, "", 0), new(selection, "", 101), new(selection, new string('a', 513)), new(selection, "\uD800") })
        {
            var result = await fixture.Reader.SearchAsync(request);
            Assert.Equal(KnowledgeReadStatus.Unavailable, result.Status);
            Assert.Empty(result.Documents);
            Assert.Null(result.Next);
            Assert.Null(result.Coverage);
        }

        string scalarBoundary = string.Concat(Enumerable.Repeat("😀", 512));
        Assert.Equal(KnowledgeReadStatus.Success, (await fixture.Reader.SearchAsync(new(selection, scalarBoundary))).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadExactAsync_ContinuationCannotSwitchHandleSectionOrOperationAsync()
    {
        var fixture = new B0bReadFixture(large: true);
        var selection = await fixture.OpenAsync();
        var handle = fixture.Handle(fixture.Cl) with { SectionId = "overview" };
        var first = await fixture.Reader.ReadExactAsync(new(selection, handle));
        Assert.Equal(KnowledgeReadStatus.Success, first.Status);
        Assert.NotNull(first.Next);

        foreach (var request in new[]
                 {
                     new ExactReadRequest(selection, handle, new(first.Next!.Value + "tamper")), new(selection, handle with { SectionId = null }, first.Next),
                     new(selection, fixture.Handle(fixture.OtherCl) with { SectionId = "overview" }, first.Next)
                 })
        {
            var result = await fixture.Reader.ReadExactAsync(request);
            Assert.Equal(KnowledgeReadStatus.InvalidContinuation, result.Status);
            Assert.Null(result.Document);
            Assert.Null(result.Content);
            Assert.Empty(result.Claims);
            Assert.Null(result.Next);
            Assert.Null(result.Coverage);
        }

        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, (await fixture.Reader.SearchAsync(new(selection, "", 20, first.Next))).Status);
        Assert.Equal(KnowledgeReadStatus.InvalidContinuation, (await fixture.Reader.GetRelatedAsync(new(selection, handle, 20, first.Next))).Status);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetRelatedAsync_TwoUnrelatedFileUnknowns_ReturnsOnlyRequestedExactOwnersAsync()
    {
        var fixture = new B0bReadFixture();
        var original = fixture.Source.Value.Uncertainties[0];
        fixture.Source.Value = fixture.Source.Value with
        {
            Uncertainties =
            [
                original with { Value = original.Value with { Key = "order-only", Explanation = "Order file unknown" }, Owners = [fixture.Cl] },
                original with { Value = original.Value with { Key = "helper-only", Explanation = "Helper file unknown" }, Owners = [fixture.OtherCl] }
            ]
        };
        var selection = await fixture.OpenAsync();
        var order = await fixture.Reader.GetRelatedAsync(new(selection, fixture.Handle(fixture.Cl)));
        var helper = await fixture.Reader.GetRelatedAsync(new(selection, fixture.Handle(fixture.OtherCl)));
        Assert.Equal(KnowledgeReadStatus.Success, order.Status);
        Assert.Equal(KnowledgeReadStatus.Success, helper.Status);
        Assert.Equal("order-only", Assert.Single(order.PermittedUncertainties).Key);
        Assert.Equal("helper-only", Assert.Single(helper.PermittedUncertainties).Key);
        Assert.Single(order.Relationships);
        Assert.Empty(helper.Relationships);
        Assert.Null(order.Next);
        Assert.Null(helper.Next);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task GetRelatedAsync_OwnedUnknownsAndEdges_ShareBoundedContinuationWithoutUnrelatedRemainderAsync()
    {
        var fixture = new B0bReadFixture();
        var original = fixture.Source.Value.Uncertainties[0];
        var owned = Enumerable.Range(0, 5).Select(index => original with { Value = original.Value with { Key = $"owned-{index}" }, Owners = [fixture.Cl] }).ToImmutableArray();
        fixture.Source.Value = fixture.Source.Value with
        {
            Uncertainties = owned.Add(original with { Value = original.Value with { Key = "unrelated" }, Owners = [fixture.OtherCl] })
        };
        var selection = await fixture.OpenAsync();
        ContinuationToken? continuation = null;
        var keys = new List<string>();
        int edges = 0;
        int pages = 0;
        do
        {
            var page = await fixture.Reader.GetRelatedAsync(new(selection, fixture.Handle(fixture.Cl), 2, continuation));
            Assert.Equal(KnowledgeReadStatus.Success, page.Status);
            Assert.InRange(page.Relationships.Length + page.PermittedUncertainties.Length, 1, 2);
            keys.AddRange(page.PermittedUncertainties.Select(value => value.Key));
            edges += page.Relationships.Length;
            continuation = page.Next;
            pages++;
            Assert.InRange(pages, 1, 3);
        } while (continuation is not null);

        Assert.Equal(3, pages);
        Assert.Equal(1, edges);
        Assert.Equal(owned.Select(item => item.Value.Key).Order(), keys.Order());
        Assert.DoesNotContain("unrelated", keys);
    }

    private static void AssertUnavailable(ContentPage page)
    {
        Assert.Equal(KnowledgeReadStatus.Unavailable, page.Status);
        Assert.Null(page.Document);
        Assert.Null(page.Content);
        Assert.Empty(page.Claims);
        Assert.Null(page.Next);
        Assert.Null(page.Coverage);
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        for (int offset = 0; (offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0; offset += value.Length) count++;
        return count;
    }
}

/// <summary>Explicit ControlledFixture read source for BL/security projection only; it never inserts fabricated B2 data into production storage.</summary>
internal sealed class B0bReadFixture
{
    public PublicationBinding Publication { get; } = new(new("ControlledFixture/read-source", "reader-target", Id(800), new string('a', 64)), Id(801));
    public Guid Repository => Id(810);
    public Guid HiddenRepository => Id(811);
    public DocumentVersionReference Cl => new(Id(800), Id(820), Id(821));
    public DocumentVersionReference OtherCl => new(Id(800), Id(822), Id(823));
    public DocumentVersionReference Bl => new(Id(800), Id(824), Id(825));
    public DocumentVersionReference HiddenBl => new(Id(800), Id(826), Id(827));
    public string LargeText { get; } = "BEGIN-EXACT" + string.Concat(Enumerable.Repeat("中文😀", 16000)) + "END-EXACT";
    public B0bPublishedSource Source { get; }
    public B0bReaderDisclosure Policy { get; } = new();
    public B0bReadLog Log { get; } = new();
    public B0bReadClock Clock { get; } = new();
    public IOctoberKnowledgeReader Reader { get; }

    public B0bReadFixture(bool large = false)
    {
        var origin = new PreparedOrigin(Repository, "TECHNICAL-SECRET-root", "TECHNICAL-SECRET/source.cs", new string('b', 64), "prepared-v1");
        var source = new Contributor(ContributorKind.Source, Repository, null, origin.RootBindingIdentity, null, origin.SourceDigest);
        var hidden = source with { RepositoryId = HiddenRepository, RootBindingIdentity = "hidden-root" };
        var advisory = new Contributor(ContributorKind.RepositoryAdvisory, Repository, null, null, Repository, new string('c', 64));
        var evidence = new EvidenceReference(Cl, "fact", new(origin, 1, 3), null, origin.SourceDigest);
        var fact = new QualifiedClaimGroup("fact", large ? LargeText : "The accepted order is processed.", ClaimQualification.SourceSupported, [evidence], []);
        var advice = new QualifiedClaimGroup("advice", "Advisory context suggests timely processing.", ClaimQualification.Advisory,
            [new(null, null, null, Repository, advisory.ContentDigest)], ["AdvisoryNotSourceProof", "TECHNICAL-SECRET-limitation"]);
        ImmutableArray<DocumentSection> sections = [new("overview", 0, "Overview", [fact, advice])];

        OctoberPublishedDocument Document(DocumentVersionReference reference, DocumentKind kind, string title, ImmutableArray<Contributor> contributors) => new(reference, kind,
            title, OctoberKnowledgeAcceptanceFixture.Unit("controlled", null, title, [], sections).ContentJson, sections, contributors);

        var documents = ImmutableArray.Create(Document(Cl, DocumentKind.CodeLogic, "CL order", [source, advisory]),
            Document(OtherCl, DocumentKind.CodeLogic, "CL helper", [source, advisory]), Document(Bl, DocumentKind.BusinessLogic, "订单处理", [source, advisory]),
            Document(HiddenBl, DocumentKind.BusinessLogic, "HIDDEN-BL-TITLE", [source, hidden, advisory]));
        Source = new(new(Publication, documents,
            [
                new(new(Id(830), origin, CoverageDisposition.Accepted, null), [source]),
                new(new(Id(831), origin with { RepositoryId = HiddenRepository }, CoverageDisposition.Unprocessed, "HIDDEN-GAP"), [hidden])
            ],
            [new(new(Id(832), RelationshipKind.GeneratedFrom, Bl, Cl, "TECHNICAL-SECRET-dependency", [evidence], []), [source])],
            [new(new("unknown", "TECHNICAL-SECRET-endpoint is unresolved", [evidence], ["TECHNICAL-SECRET-gap"]), [source], [Cl])]));
        var tokens = new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider());
        Reader = new OctoberKnowledgeReader(Source, Publication, Policy, new OctoberKnowledgeCursorProtector(tokens, Clock), Log);
    }

    public ReadSelectionRequest Request(KnowledgeMode mode = KnowledgeMode.IT, string subject = "ordinary-user") =>
        new(Publication, new(subject, "controlled-stamp"), mode, KnowledgePurpose.Reader, [Repository]);

    public async Task<ReadSelection> OpenAsync(KnowledgeMode mode = KnowledgeMode.IT, string subject = "ordinary-user")
    {
        var result = await Reader.OpenReadSelectionAsync(Request(mode, subject));
        Assert.Equal(KnowledgeReadStatus.Success, result.Status);
        return Assert.IsType<ReadSelection>(result.Selection);
    }

    public DocumentHandle Handle(DocumentVersionReference reference) => new(reference.BatchId, Publication.PublicationId, reference.LogicalDocumentId, reference.DocumentVersionId);
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
}

internal sealed class B0bPublishedSource(OctoberPublishedKnowledge value) : IOctoberPublishedKnowledgeSource
{
    public OctoberPublishedKnowledge Value { get; set; } = value;
    public Action? BeforeReturn { get; set; }
    public Exception? Failure { get; set; }

    public Task<OctoberPublishedKnowledge?> LoadAsync(PublicationBinding publication, CancellationToken cancellationToken = default)
    {
        if (Failure is not null) throw Failure;
        BeforeReturn?.Invoke();
        return Task.FromResult(publication == Value.Publication ? Value : null);
    }
}

internal sealed class B0bReaderDisclosure : IOctoberKnowledgeDisclosurePolicy
{
    public List<DisclosureRequest> Requests { get; } = [];
    public DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
    public string CurrentSubject { get; set; } = "ordinary-user";

    public Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        if (Status != DisclosureStatus.Allowed) return Task.FromResult(Status);
        bool permitted = request.Selection.Subject.SubjectId == CurrentSubject && (request.Selection.Mode != KnowledgeMode.Business || request.Kind != DocumentKind.CodeLogic) &&
                         request.AllContributors.All(contributor => contributor.RepositoryId is null || request.Selection.RepositoryScope.Contains(contributor.RepositoryId.Value));
        return Task.FromResult(permitted ? DisclosureStatus.Allowed : DisclosureStatus.Denied);
    }
}

internal sealed class B0bReadLog : IOctoberKnowledgeReadLog
{
    public bool Succeeds { get; set; } = true;
    public Action? AfterAppend { get; set; }
    public List<KnowledgeReadLogEntry> Entries { get; } = [];

    public Task<bool> AppendAsync(KnowledgeReadLogEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        AfterAppend?.Invoke();
        return Task.FromResult(Succeeds);
    }
}

internal sealed class B0bReadClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan amount) => _now += amount;
}

using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;
using Vulgata.Shared.Explore;
using Vulgata.Tests.Migrations;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Explore;
using Vulgata.Web.Data;

namespace Vulgata.Tests.Explore;

/// <summary>
/// Real PostgreSQL acceptance, publication and reads through Explore with controlled semantic inputs and
/// disclosure adapters except the explicit SQLite Identity integration case. These tests do not prove
/// real Scan generation, persisted BL or PostgreSQL Identity deployment.
/// </summary>
public sealed class ExplorePersistedReaderTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ContinueDocumentAsync_PersistedLargeUnicodeDocumentAndExactSection_PreservesBoundedTextAndQualificationsAsync()
    {
        await using var owned = await PersistedReaderFixture.CreateAsync();
        using var explorer = owned.Explorer(owned.Publication);
        var strictUtf8 = new UTF8Encoding(false, true);
        Assert.True(strictUtf8.GetByteCount(owned.LargeText) > KnowledgeContract.MaximumContentBytes);
        foreach (var section in new string?[] { null, "overview" })
        {
            var handle = owned.Handle(owned.Primary, owned.Publication, section);
            var result = await explorer.OpenDocumentAsync(handle);
            var text = new StringBuilder();
            var claimText = new StringBuilder();
            var cursors = new HashSet<string>(StringComparer.Ordinal);
            var pages = 0;
            var qualifiedFragments = 0;
            while (true)
            {
                Assert.Equal(KnowledgeReadStatus.Success, result.Page.Status);
                Assert.Equal(handle, result.Page.Document!.Handle);
                Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(result.Page).Length, 1, KnowledgeContract.MaximumContentBytes);
                var content = Assert.IsType<string>(result.Page.Content);
                Assert.Equal(content, strictUtf8.GetString(strictUtf8.GetBytes(content)));
                text.Append(content);
                foreach (var claim in result.Page.Claims.Where(claim => claim.ClaimGroupId == "motive"))
                {
                    Assert.Equal(ClaimQualification.Advisory, claim.Qualification);
                    Assert.Equal(new[] { "AdvisoryNotSourceProof" }, claim.Limitations.ToArray());
                    Assert.Equal(OctoberKnowledgeAcceptanceFixture.Id(303), Assert.Single(claim.Evidence).AdvisoryRecordId);
                    claimText.Append(claim.Text);
                    qualifiedFragments++;
                }
                Assert.True(++pages <= 100, "Owner continuation exceeded the bounded test traversal.");
                if (result.Page.Next is null)
                {
                    Assert.Null(result.Session);
                    break;
                }
                Assert.True(cursors.Add(result.Page.Next.Value), "Owner repeated a continuation token.");
                Assert.NotNull(result.Session);
                result = await explorer.ContinueDocumentAsync(result.Session);
            }
            Assert.True(pages > 1);
            Assert.True(qualifiedFragments > 1);
            Assert.Equal(owned.LargeText, claimText.ToString());
            var expected = (section is null ? "Order operation\n" : "") +
                "Overview\n[来源支持] Acquires the order lock.\n[建议性上下文] " + owned.LargeText + "\n限制：AdvisoryNotSourceProof\n";
            Assert.Equal(expected, text.ToString());
        }
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task OpenDocumentAsync_LaterPublicationChangesMembership_PreservesEarlierExactSelectionAndBusinessNoBlAsync()
    {
        await using var owned = await PersistedReaderFixture.CreateAsync();
        // This is immutable publication history over one corpus, not a changed-version or version-chain test.
        var later = await owned.PublishAsync([owned.Helper]);
        Assert.NotEqual(owned.Publication.PublicationId, later.PublicationId);
        using var earlierExplorer = owned.Explorer(owned.Publication);
        var earlierHandle = owned.Handle(owned.Primary, owned.Publication, "overview");
        var exact = await earlierExplorer.OpenDocumentAsync(earlierHandle);
        Assert.Equal(KnowledgeReadStatus.Success, exact.Page.Status);
        Assert.Equal(earlierHandle, exact.Page.Document!.Handle);
        using var laterExplorer = owned.Explorer(later);
        var browse = await laterExplorer.BrowseAsync();
        Assert.Equal(KnowledgeReadStatus.Success, browse.Page.Status);
        Assert.Equal(owned.Handle(owned.Helper, later), Assert.Single(browse.Page.Documents).Handle);
        AssertMetadataFree((await laterExplorer.OpenDocumentAsync(earlierHandle)).Page);
        AssertMetadataFree((await laterExplorer.OpenDocumentAsync(owned.Handle(owned.Primary, later))).Page);
        AssertMetadataFree((await earlierExplorer.OpenDocumentAsync(earlierHandle with { BatchId = Guid.NewGuid() })).Page);
        AssertMetadataFree((await earlierExplorer.OpenDocumentAsync(earlierHandle with { PublicationId = Guid.NewGuid() })).Page);
        AssertMetadataFree((await earlierExplorer.OpenDocumentAsync(earlierHandle with { DocumentVersionId = Guid.NewGuid() })).Page);
        AssertMetadataFree((await earlierExplorer.OpenDocumentAsync(earlierHandle with { SectionId = "missing-section" })).Page);
        using var businessExplorer = owned.Explorer(owned.Publication, KnowledgeMode.Business);
        var business = await businessExplorer.BrowseAsync();
        Assert.Equal(KnowledgeReadStatus.Success, business.Page.Status);
        Assert.Empty(business.Page.Documents);
        Assert.Null(business.Page.Next);
        Assert.Null(business.Session);
        AssertMetadataFree((await businessExplorer.OpenDocumentAsync(earlierHandle)).Page);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ContinueDocumentAsync_PersistedReadLosesAuthorityOrService_ReturnsNoPreviouslyLoadedMetadataAsync()
    {
        await using var owned = await PersistedReaderFixture.CreateAsync();
        using var explorer = owned.Explorer(owned.Publication);
        var handle = owned.Handle(owned.Primary, owned.Publication, "overview");
        foreach (var status in new[] { DisclosureStatus.Denied, DisclosureStatus.AuthorityUnavailable })
        {
            var opened = await explorer.OpenDocumentAsync(handle);
            Assert.Equal(KnowledgeReadStatus.Success, opened.Page.Status);
            Assert.NotNull(opened.Session);
            owned.Policy.Status = status;
            var failed = await explorer.ContinueDocumentAsync(opened.Session);
            AssertMetadataFree(failed.Page);
            Assert.Null(failed.Session);
            owned.Policy.Status = DisclosureStatus.Allowed;
            var restarted = await explorer.OpenDocumentAsync(handle);
            Assert.Equal(KnowledgeReadStatus.Success, restarted.Page.Status);
            Assert.Equal(handle, restarted.Page.Document!.Handle);
            Assert.Equal(opened.Page.Content, restarted.Page.Content);
        }

        var beforeOutage = await explorer.OpenDocumentAsync(handle);
        Assert.NotNull(beforeOutage.Session);
        owned.Acceptance.Factory.Available = false;
        try
        {
            var failed = await explorer.ContinueDocumentAsync(beforeOutage.Session);
            AssertMetadataFree(failed.Page);
            Assert.Null(failed.Session);
        }
        finally { owned.Acceptance.Factory.Available = true; }

        var beforeCancel = await explorer.OpenDocumentAsync(handle);
        Assert.NotNull(beforeCancel.Session);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await explorer.ContinueDocumentAsync(beforeCancel.Session, cancellation.Token);
        Assert.Equal(KnowledgeReadStatus.Cancelled, cancelled.Page.Status);
        AssertMetadataFree(cancelled.Page);
        Assert.Null(cancelled.Session);

        var beforeSignOut = await explorer.OpenDocumentAsync(handle);
        Assert.NotNull(beforeSignOut.Session);
        owned.Authentication.User = new(new ClaimsIdentity());
        owned.Authentication.NotifyChanged();
        var signedOut = await explorer.ContinueDocumentAsync(beforeSignOut.Session);
        AssertMetadataFree(signedOut.Page);
        Assert.Null(signedOut.Session);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ContinueDocumentAsync_PersistedKnowledgeAndRealIdentityStampRevocation_DeniesPreviouslyPermittedExactReadAsync()
    {
        // Knowledge uses runner-owned PostgreSQL; existing Identity/management helper owns separate SQLite files.
        // Acceptance inputs remain controlled. All disclosure decisions use production policy and live Identity.
        await using var owned = await PersistedReaderFixture.CreateAsync(sourceOnly: true);
        await using var identity = await B0bIdentityFixture.CreateAsync();
        await using (var scope = identity.Services.CreateAsyncScope())
        {
            await using var management = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync();
            var repository = Vulgata.Core.Entities.Repository.CreateStandalone("Explore persisted fixture", "https://fixture.invalid/explore.git",
                null, null, DateTimeOffset.UtcNow);
            // Test setup aligns a legitimate management record with the existing acceptance fixture's selected ID.
            // No Knowledge rows are populated or rewritten here.
            management.Entry(repository).Property(item => item.Id).CurrentValue = owned.Acceptance.Origin.RepositoryId;
            management.Repositories.Add(repository);
            await management.SaveChangesAsync();
            Assert.Equal(0, await management.SystemOwnerAssignments.CountAsync());
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByIdAsync(identity.Subject.SubjectId));
            Assert.Equal(new[] { "User" }, (await users.GetRolesAsync(user)).ToArray());
        }

        // The helper intentionally has a stale Administrator principal claim; live UserManager roles remain User.
        var policy = new OctoberKnowledgeDisclosurePolicy(
            new OctoberKnowledgeIdentityAdapter(identity.Services.GetRequiredService<IServiceScopeFactory>(), _ => Task.FromResult(identity.Principal)),
            new OctoberKnowledgeSelectionAuthority(owned.Acceptance.Factory));
        var reader = new OctoberKnowledgeReader(new OctoberKnowledgePublishedSource(owned.Acceptance.Factory), owned.Publication, policy,
            new OctoberKnowledgeCursorProtector(new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider()), new B0bReadClock()),
            new B0bReadLog());
        var authentication = new ExploreTestAuthentication { User = identity.Principal! };
        var context = new ExploreTestContext(new(new(owned.Publication, identity.Subject, KnowledgeMode.IT,
            KnowledgePurpose.Reader, [owned.Acceptance.Origin.RepositoryId]), true));
        using var explorer = new ExplorePresentationService(reader, context, authentication);
        var handle = owned.Handle(owned.Primary, owned.Publication, "overview");
        var opened = await explorer.OpenDocumentAsync(handle);
        Assert.Equal(KnowledgeReadStatus.Success, opened.Page.Status);
        Assert.Equal(handle, opened.Page.Document!.Handle);
        Assert.NotNull(opened.Session);
        Assert.Contains(opened.Page.Claims, claim => claim.Qualification == ClaimQualification.SourceSupported);

        await using (var scope = identity.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByIdAsync(identity.Subject.SubjectId));
            Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
            Assert.NotEqual(identity.Subject.AuthenticationBinding, await users.GetSecurityStampAsync(user));
        }
        // Retain the old authenticated principal/context intentionally: fresh owner checks must reject it.
        var denied = await explorer.ContinueDocumentAsync(opened.Session);
        AssertMetadataFree(denied.Page);
        Assert.Null(denied.Session);
        var restart = await explorer.OpenDocumentAsync(handle);
        AssertMetadataFree(restart.Page);
        Assert.Null(restart.Session);
    }

    private static void AssertMetadataFree(ContentPage page)
    {
        Assert.NotEqual(KnowledgeReadStatus.Success, page.Status);
        Assert.Null(page.Document);
        Assert.Null(page.Content);
        Assert.Empty(page.Claims);
        Assert.Null(page.Next);
        Assert.Null(page.Coverage);
    }

    private sealed class PersistedReaderFixture : IAsyncDisposable
    {
        private readonly DisposablePostgreSqlDatabase _database;
        private readonly CorpusHandle _corpus;
        private readonly AcceptedFile _accepted;
        private PersistedReaderFixture(DisposablePostgreSqlDatabase database, OctoberKnowledgeAcceptanceFixture acceptance,
            AcceptedFile accepted, CorpusHandle corpus, string largeText)
        {
            _database = database;
            Acceptance = acceptance;
            _accepted = accepted;
            _corpus = corpus;
            LargeText = largeText;
        }

        public OctoberKnowledgeAcceptanceFixture Acceptance { get; }
        public B0bReaderDisclosure Policy { get; } = new();
        public ExploreTestAuthentication Authentication { get; } = new();
        public PublicationBinding Publication { get; private set; } = null!;
        public DocumentVersionReference Primary { get; private set; } = null!;
        public DocumentVersionReference Helper { get; private set; } = null!;
        public string LargeText { get; }

        public static async Task<PersistedReaderFixture> CreateAsync(bool sourceOnly = false)
        {
            var database = await DisposablePostgreSqlDatabase.CreateAsync(TestResourceScope.RequireAdministrator());
            OctoberKnowledgeAcceptanceFixture? acceptance = null;
            try
            {
                TestResourceScope.ValidateOwnedTarget(database.ConnectionString);
                var options = new DbContextOptionsBuilder<OctoberDbContext>().UseNpgsql(database.ConnectionString,
                    postgres => postgres.MigrationsHistoryTable("__OctoberMigrationsHistory", "october").CommandTimeout(30)).Options;
                await using (var context = new OctoberDbContext(options)) { await context.Database.MigrateAsync(); }
                acceptance = await OctoberKnowledgeAcceptanceFixture.CreateWithOptionsAsync(options);
                var largeText = "BEGIN-EXPLORE" + string.Concat(Enumerable.Repeat("订单😀", 8000)) + "END-EXPLORE";
                var proposal = acceptance.Proposal;
                var original = proposal.Units[0];
                var overview = original.Sections[0];
                var sections = original.Sections.SetItem(0, overview with
                {
                    Claims = sourceOnly ? [overview.Claims[0] with { Text = largeText }] :
                        overview.Claims.SetItem(1, overview.Claims[1] with { Text = largeText }),
                });
                var unit = OctoberKnowledgeAcceptanceFixture.Unit(original.SemanticSelector, original.ParentSelector,
                    original.Title, original.Anchors, sections);
                proposal = proposal with { Units = proposal.Units.SetItem(0, unit), Observations = [], Uncertainties = [] };
                if (sourceOnly) { proposal = proposal with { Contributors = [acceptance.SourceContributor] }; }
                var acceptanceResult = await acceptance.Writer().AcceptCompletedFileAsync(acceptance.Seal(proposal));
                Assert.Equal(KnowledgeWriteStatus.Committed, acceptanceResult.Status);
                var accepted = Assert.IsType<AcceptedFile>(acceptanceResult.AcceptedFile);
                var freeze = new CorpusFreezeRequest(new(acceptance.Binding.BatchId, OperationKind.FreezeCorpus, Guid.NewGuid()),
                    acceptance.Binding, "", [accepted], [accepted.Coverage]);
                freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
                acceptance.FreezeAuthority.Approve(freeze);
                var frozen = await acceptance.Writer().FreezeCorpusAsync(freeze);
                Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
                var fixture = new PersistedReaderFixture(database, acceptance, accepted, Assert.IsType<CorpusHandle>(frozen.Corpus), largeText);
                fixture.Publication = await fixture.PublishAsync(accepted.Documents);
                var loaded = Assert.IsType<OctoberPublishedKnowledge>(await new OctoberKnowledgePublishedSource(acceptance.Factory).LoadAsync(fixture.Publication));
                fixture.Primary = Assert.Single(loaded.Documents.Where(document => document.Title == original.Title)).Reference;
                fixture.Helper = Assert.Single(loaded.Documents.Where(document => document.Title == "Order helper")).Reference;
                return fixture;
            }
            catch
            {
                try { if (acceptance is not null) { await acceptance.DisposeAsync(); } }
                finally { await database.DisposeAsync(); }
                throw;
            }
        }

        public async Task<PublicationBinding> PublishAsync(ImmutableArray<DocumentVersionReference> documents)
        {
            var request = new PublicationRequest(new(Acceptance.Binding.BatchId, OperationKind.PublishSelection, Guid.NewGuid()),
                _corpus, "", documents, [], [], [], [_accepted.Coverage]);
            request = request with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(request) };
            var result = await Acceptance.Writer().PublishSelectionAsync(request);
            Assert.Equal(KnowledgeWriteStatus.Committed, result.Status);
            return Assert.IsType<PublicationBinding>(result.Publication);
        }

        public DocumentHandle Handle(DocumentVersionReference document, PublicationBinding publication, string? section = null) =>
            new(document.BatchId, publication.PublicationId, document.LogicalDocumentId, document.DocumentVersionId, section);

        public ExplorePresentationService Explorer(PublicationBinding publication, KnowledgeMode mode = KnowledgeMode.IT)
        {
            var tokens = new OctoberKnowledgeDataProtectionTokenProtector(new EphemeralDataProtectionProvider());
            var reader = new OctoberKnowledgeReader(new OctoberKnowledgePublishedSource(Acceptance.Factory), publication,
                Policy, new OctoberKnowledgeCursorProtector(tokens, new B0bReadClock()), new B0bReadLog());
            var context = new ExploreTestContext(new(new(publication, new("ordinary-user", "controlled-stamp"),
                mode, KnowledgePurpose.Reader, [Acceptance.Origin.RepositoryId]), true));
            return new(reader, context, Authentication);
        }

        public async ValueTask DisposeAsync()
        {
            try { await Acceptance.DisposeAsync(); }
            finally { await _database.DisposeAsync(); }
        }
    }
}

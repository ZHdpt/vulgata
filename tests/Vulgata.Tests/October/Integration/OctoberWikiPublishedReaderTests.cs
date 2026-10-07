using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Shared.Explore;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Explore;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberWikiPublishedReaderTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task PublishedLargeDocument_RealReaderAndExplore_ReassembleBoundedPagesAndRejectForeignPublicationAsync()
    {
        await using var fixture = await OctoberKnowledgeAcceptanceFixture.CreateSqliteAsync();
        await VerifyAsync(fixture);
    }

    internal static async Task<PublicationBinding> VerifyAsync(OctoberKnowledgeAcceptanceFixture fixture)
    {
        // These are controlled semantic claims, not native source/provider qualification.
        string text = string.Concat(Enumerable.Repeat("注册与登录 business rule. ", 8000));
        var baseProposal = fixture.Proposal;
        var original = baseProposal.Units[0];
        var section = original.Sections[0];
        var sections = original.Sections.SetItem(0, section with { Claims = [section.Claims[0] with { Text = text }] });
        var unit = OctoberKnowledgeAcceptanceFixture.Unit(original.SemanticSelector, null, original.Title, original.Anchors, sections);
        var proposal = baseProposal with { Units = [unit], Observations = [], Uncertainties = [] };
        var accepted = await fixture.Writer().AcceptCompletedFileAsync(fixture.Seal(proposal));
        Assert.Equal(KnowledgeWriteStatus.Committed, accepted.Status);
        var file = Assert.IsType<AcceptedFile>(accepted.AcceptedFile);
        var freeze = new CorpusFreezeRequest(new(fixture.Binding.BatchId, OperationKind.FreezeCorpus, Guid.NewGuid()), fixture.Binding, "", [file], [file.Coverage]);
        freeze = freeze with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputeFreezeDigest(freeze) };
        fixture.FreezeAuthority.Approve(freeze);
        var frozen = await fixture.Writer().FreezeCorpusAsync(freeze);
        Assert.Equal(KnowledgeWriteStatus.Committed, frozen.Status);
        var publish = new PublicationRequest(new(fixture.Binding.BatchId, OperationKind.PublishSelection, Guid.NewGuid()), frozen.Corpus!, "", file.Documents, [], [], [], [file.Coverage]);
        publish = publish with { CanonicalPayloadDigest = OctoberKnowledgePublicationPolicy.ComputePublicationDigest(publish) };
        var published = await fixture.Writer().PublishSelectionAsync(publish);
        Assert.Equal(KnowledgeWriteStatus.Committed, published.Status);
        var publication = Assert.IsType<PublicationBinding>(published.Publication);
        string directory = Path.Combine(Path.GetTempPath(), "vulgata-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var auth = new IntegrationAuthentication();
            var services = OctoberWikiCompositionTests.Services(auth);
            services.AddOctoberWikiReader(OctoberWikiCompositionTests.Configuration(publication, fixture.Origin.RepositoryId, directory), _ => throw new InvalidOperationException("SQLite factory replaced explicitly"));
            services.Replace(ServiceDescriptor.Singleton<IDbContextFactory<OctoberDbContext>>(fixture.Factory));
            services.Replace(ServiceDescriptor.Singleton<IOctoberKnowledgeDisclosurePolicy>(new B0bReaderDisclosure()));
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var explorer = scope.ServiceProvider.GetRequiredService<IExplorePresentationService>();
            var browse = await explorer.BrowseAsync();
            Assert.Equal(KnowledgeReadStatus.Success, browse.Page.Status);
            var document = Assert.Single(browse.Page.Documents);
            var page = await explorer.OpenDocumentAsync(document.Handle);
            var reconstructed = new StringBuilder();
            int pages = 0;
            do
            {
                Assert.Equal(KnowledgeReadStatus.Success, page.Page.Status);
                Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(page.Page).Length, 1, KnowledgeContract.MaximumContentBytes);
                reconstructed.Append(page.Page.Content);
                Assert.True(++pages < 500, "Continuation did not terminate within bounded fixture pages.");
                if (page.Session is null) break;
                page = await explorer.ContinueDocumentAsync(page.Session);
            } while (true);
            Assert.True(pages > 1);
            Assert.Contains(text, reconstructed.ToString(), StringComparison.Ordinal);
            Assert.Equal(KnowledgeReadStatus.Unavailable, (await explorer.OpenDocumentAsync(document.Handle with { PublicationId = Guid.NewGuid() })).Page.Status);
            OctoberWikiCompositionTests.AssertNoProducer(scope.ServiceProvider);
            // Authentication changes invalidate the current presentation immediately.
            auth.Set(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()));
            Assert.Equal(KnowledgeReadStatus.Unavailable, (await explorer.BrowseAsync()).Page.Status);
            return publication;
        }
        finally { Directory.Delete(directory, true); }
    }
}

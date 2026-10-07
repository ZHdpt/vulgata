using System.Security.Claims;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared;
using Vulgata.Shared.Explore;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Explore;

namespace Vulgata.Tests.Explore;

public sealed class ExactReaderTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_OrdinaryUserAndExactEarlySection_PreservesEveryHandleFieldAsync()
    {
        var host = new ExploreFixtureHost();
        host.Context.Current = new(host.Fixture.Selection(publication: host.Fixture.EarlyPublicationId), true);
        using var vm = host.CreateReader();
        var handle = host.Fixture.Handle(host.Fixture.ClA, host.Fixture.EarlyPublicationId, "overview");
        await vm.OpenAsync(handle);
        Assert.Equal(LoadState.Loaded, vm.LoadState);
        Assert.Equal(handle, vm.Page!.Document!.Handle);
        Assert.Equal(handle, Assert.Single(host.Reader.Reads).Document);
        Assert.Equal(KnowledgePurpose.Reader, Assert.Single(host.Reader.Selections).Purpose);
        Assert.False(host.Authentication.User.IsInRole("Administrator"));
        Assert.False(host.Authentication.User.IsInRole("SystemOwner"));
        Assert.True(vm.IsControlledFixture);
        Assert.True(vm.CanContinue);
        Assert.Equal(KnowledgeMode.IT, vm.Mode);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_BusinessMode_AllowsBlButNeverElevatesForClAsync()
    {
        var host = new ExploreFixtureHost(KnowledgeMode.Business);
        using var vm = host.CreateReader();
        await vm.OpenAsync(host.Fixture.Handle(host.Fixture.BlA));
        Assert.Equal(LoadState.Loaded, vm.LoadState);
        Assert.Equal(DocumentKind.BusinessLogic, vm.Page!.Document!.Kind);
        Assert.All(vm.Page.Claims, claim => Assert.Empty(claim.Evidence));
        await vm.OpenAsync(host.Fixture.Handle(host.Fixture.ClA));
        Assert.Null(vm.Page);
        Assert.Null(vm.Session);
        Assert.False(vm.CanContinue);
        Assert.All(host.Reader.Selections, selection => Assert.Equal(KnowledgeMode.Business, selection.Mode));
        Assert.DoesNotContain("CL-A", vm.StatusText, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_UnknownForbiddenAndMalformedTargets_ClearPriorMetadataAsync()
    {
        var host = new ExploreFixtureHost();
        using var vm = host.CreateReader();
        var valid = host.Fixture.Handle(host.Fixture.ClA);
        foreach (var handle in new[]
        {
            valid with { PublicationId = host.Fixture.EarlyPublicationId },
            valid with { LogicalDocumentId = OctoberKnowledgeFixture.Id(999) },
            valid with { DocumentVersionId = OctoberKnowledgeFixture.Id(999) },
            valid with { SectionId = "missing-section" },
            valid with { BatchId = Guid.Empty },
            valid with { SectionId = " " },
            host.Fixture.Handle(host.Fixture.ClC),
        })
        {
            await vm.OpenAsync(valid);
            Assert.NotNull(vm.Page);
            await vm.OpenAsync(handle);
            Assert.Null(vm.Page);
            Assert.Null(vm.Session);
            Assert.False(vm.CanContinue);
            Assert.NotEqual(LoadState.Loaded, vm.LoadState);
            Assert.DoesNotContain("CL-A", vm.StatusText, StringComparison.Ordinal);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_MismatchedAuthenticatedSubject_DoesNotInvokeKnowledgeAsync()
    {
        var host = new ExploreFixtureHost();
        host.Authentication.User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "different-user")], "ControlledFixture"));
        using var vm = host.CreateReader();
        await vm.OpenAsync(host.Fixture.Handle(host.Fixture.ClA));
        Assert.Null(vm.Page);
        Assert.Empty(host.Reader.Selections);
        Assert.Empty(host.Reader.Reads);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task BrowseAsync_OrdinaryBusinessUser_ExcludesDeniedInputMetadataAndHasBoundedDefaultAsync()
    {
        var host = new ExploreFixtureHost(KnowledgeMode.Business);
        using var vm = host.CreateBrowse();
        await vm.LoadAsync();
        Assert.Equal(LoadState.Loaded, vm.LoadState);
        Assert.Equal(2, vm.Page!.Documents.Length);
        Assert.All(vm.Page.Documents, document => Assert.Equal(DocumentKind.BusinessLogic, document.Kind));
        Assert.DoesNotContain(vm.Page.Documents, document => document.Handle == host.Fixture.Handle(host.Fixture.BlHidden));
        Assert.Equal(KnowledgeContract.DefaultPageSize, Assert.Single(host.Reader.Searches).PageSize);
        Assert.Null(vm.Session);
        Assert.False(vm.CanContinue);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_InvalidSuccessfulProjection_FailsWithoutDisclosingItsMetadataAsync()
    {
        var host = new ExploreFixtureHost();
        var handle = host.Fixture.Handle(host.Fixture.ClA);
        using var vm = host.CreateReader();
        foreach (var page in new[]
        {
            host.Content(handle, "private") with { Document = null },
            host.Content(handle, "private") with { Content = null },
            host.Content(handle with { DocumentVersionId = OctoberKnowledgeFixture.Id(999) }, "private"),
        })
        {
            host.Reader.ReadResponse = (_, _) => Task.FromResult(page);
            await vm.OpenAsync(handle);
            Assert.Null(vm.Page);
            Assert.Null(vm.Session);
            Assert.DoesNotContain("private", vm.StatusText, StringComparison.Ordinal);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Build_ExactLocalLink_EncodesAllIdentitiesAndSectionWithoutModeElevation()
    {
        var host = new ExploreFixtureHost();
        var handle = host.Fixture.Handle(host.Fixture.ClA, section: "章节 /?#&<script>");
        var link = ExactDocumentLinkBuilder.Build(handle);
        Assert.StartsWith("/knowledge/documents/read?", link, StringComparison.Ordinal);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri("https://fixture.invalid" + link).Query);
        Assert.Equal(handle.BatchId.ToString(), query["batch"].ToString());
        Assert.Equal(handle.PublicationId.ToString(), query["publication"].ToString());
        Assert.Equal(handle.LogicalDocumentId.ToString(), query["logical"].ToString());
        Assert.Equal(handle.DocumentVersionId.ToString(), query["version"].ToString());
        Assert.Equal(handle.SectionId, query["section"].ToString());
        Assert.False(query.ContainsKey("mode"));
        Assert.DoesNotContain("<script>", link, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenDocumentAsync_UnpairedSurrogateInVisibleProjection_RejectsWithoutReplacementOrMetadataAsync()
    {
        var host = new ExploreFixtureHost();
        var handle = host.Fixture.Handle(host.Fixture.ClA);
        var valid = host.Content(handle, "safe");
        foreach (var invalidText in new[] { "invalid\uD800text", "invalid\uDC00text" })
        {
            foreach (var page in new[]
            {
                valid with { Content = invalidText },
                valid with { Document = valid.Document! with { Title = invalidText } },
                valid with { Claims = [host.Fixture.SourceClaim with { Text = invalidText }] },
                valid with { Claims = [host.Fixture.AdvisoryClaim with { Limitations = [invalidText] }] },
                valid with { Coverage = new(true, [invalidText]) },
            })
            {
                host.Reader.ReadResponse = (_, _) => Task.FromResult(page);
                var result = await host.Service.OpenDocumentAsync(handle);
                Assert.NotEqual(KnowledgeReadStatus.Success, result.Page.Status);
                Assert.Null(result.Page.Document);
                Assert.Null(result.Page.Content);
                Assert.Empty(result.Page.Claims);
                Assert.Null(result.Page.Coverage);
                Assert.Null(result.Page.Next);
                Assert.Null(result.Session);
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task OpenAsync_UnpairedSurrogateInSection_IsRejectedBeforeOwnerReadAsync()
    {
        var host = new ExploreFixtureHost();
        using var vm = host.CreateReader();
        foreach (var section in new[] { "section\uD800", "section\uDC00" })
        {
            var handle = host.Fixture.Handle(host.Fixture.ClA, section: section);
            await vm.OpenAsync(handle);
            Assert.Null(vm.Page);
            Assert.Null(vm.Session);
            Assert.Empty(host.Reader.Reads);
            Assert.False(new ExploreDocumentHandleValidator().Validate(handle).IsValid);
            Assert.ThrowsAny<ArgumentException>(() => ExactDocumentLinkBuilder.Build(handle));
        }
    }
}

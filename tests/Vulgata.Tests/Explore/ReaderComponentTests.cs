using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared.Explore;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Components.Explore;
using Vulgata.Web.Components.Pages.Explore;
using Vulgata.Web.Explore;

namespace Vulgata.Tests.Explore;

public sealed class ReaderComponentTests : BunitContext
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Browse_TitleSearchCrossesPages_PreviousRereads_EmptySearchRestoresAsync()
    {
        var host = new ExploreFixtureHost();
        var documents = Enumerable.Range(0, 42).Select(index => new DocumentSummary(
            host.Fixture.Handle(host.Fixture.ClA) with { DocumentVersionId = ControlledGraphFixture.Id(100 + index) },
            DocumentKind.CodeLogic, (index % 2 == 0 ? "账户" : "登录") + index, [])).ToArray();
        host.Reader.SearchResponse = request =>
        {
            var offset = request.Continuation is null ? 0 : int.Parse(request.Continuation.Value);
            return new(KnowledgeReadStatus.Success, [.. documents.Skip(offset).Take(6)],
                offset + 6 < documents.Length ? new((offset + 6).ToString()) : null, null);
        };
        using var model = host.CreateBrowse();
        await model.LoadAsync();
        Assert.Equal(42, model.TotalItems);
        Assert.Equal(3, model.TotalPages);
        Assert.Equal(20, model.Page!.Documents.Length);
        await model.GoToPageAsync(3);
        Assert.Equal(3, model.PageNumber);
        Assert.Equal(2, model.Page!.Documents.Length);
        await model.PreviousAsync();
        Assert.Equal(2, model.PageNumber);
        Assert.Equal(20, model.Page!.Documents.Length);
        await model.SearchAsync("账户");
        Assert.Equal(21, model.TotalItems);
        Assert.Equal(2, model.TotalPages);
        Assert.Equal(1, model.PageNumber);
        Assert.Equal(20, model.Page!.Documents.Length);
        Assert.All(model.Page.Documents, document => Assert.StartsWith("账户", document.Title));
        await model.NextAsync();
        Assert.Single(model.Page!.Documents);
        Assert.False(model.CanContinue);
        await model.GoToPageAsync(99);
        Assert.Equal(2, model.PageNumber);
        await model.SearchAsync("不存在");
        Assert.Empty(model.Page!.Documents);
        Assert.Equal(0, model.TotalPages);
        await model.SearchAsync("");
        Assert.Equal(42, model.TotalItems);
        Assert.Equal(20, model.Page!.Documents.Length);
        await model.LoadAsync(12);
        await model.SearchAsync("账户");
        Assert.Equal(12, model.PageSize);
        Assert.Equal(12, model.Page!.Documents.Length);
        Assert.Equal(2, model.TotalPages);
        await model.NextAsync();
        Assert.Equal(9, model.Page!.Documents.Length);
        Assert.Equal(12, model.PageSize);
        await model.LoadAsync(8);
        Assert.Equal(8, model.PageSize);
        Assert.Equal(3, model.TotalPages);

        Configure(host, 2);
        var cut = Render<DocumentsPage>();
        cut.WaitForAssertion(() => Assert.Contains("/ 21 页", cut.Find(".page-number").TextContent));
        Assert.Single(cut.FindAll(".pagination-ellipsis"));
        var browse = Services.GetRequiredService<Vulgata.Web.ViewModels.Explore.DocumentBrowseViewModel>();
        await cut.InvokeAsync(() => browse.GoToPageAsync(11));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".pagination-ellipsis").Count));
        Assert.Equal(2, cut.FindAll(".document-list li").Count);

    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void DocumentSections_UntrustedText_RendersEncodedContentAndAdjacentQualifications()
    {
        var host = new ExploreFixtureHost();
        var page = host.Content(host.Fixture.Handle(host.Fixture.ClA, section: "overview"), "<script>alert('x')</script><img src=x onerror=alert(1)>");
        var cut = Render<DocumentSections>(parameters => parameters.Add(component => component.Page, page));
        Assert.Empty(cut.FindAll(".document-body .body-heading"));
        Assert.Single(cut.FindAll(".document-sections > .body-heading"));
        Assert.Empty(cut.FindAll("script,img"));
        Assert.Contains("<script>alert('x')</script>", cut.Find("section").TextContent, StringComparison.Ordinal);
        Assert.Contains("参考上下文", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("假设", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(host.Fixture.AdvisoryClaim.Limitations[0], cut.Markup, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeMode.Business, "light")]
    [InlineData(KnowledgeMode.IT, "light")]
    public async Task ReaderPage_ExactSection_ShowsPartialNextTerminalAndModeThemeAsync(KnowledgeMode mode, string theme)
    {
        var host = new ExploreFixtureHost(mode);
        var handle = host.Fixture.Handle(mode == KnowledgeMode.Business ? host.Fixture.BlA : host.Fixture.ClA, section: "overview");
        Configure(host);
        Services.GetRequiredService<NavigationManager>().NavigateTo(ExactDocumentLinkBuilder.Build(handle));
        var cut = Render<DocumentReaderPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"[data-theme='{theme}']")));
        cut.WaitForAssertion(() => Assert.Contains("下一部分", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("受控测试数据", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("部分", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(mode == KnowledgeMode.Business ? "业务逻辑" : "代码逻辑", cut.Markup, StringComparison.Ordinal);
        if (mode == KnowledgeMode.IT)
        {
            Assert.Contains("src/Order.cs", cut.Markup, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("src/Order.cs", cut.Markup, StringComparison.Ordinal);
        }
        var anchor = ExactDocumentLinkBuilder.GetSectionAnchor("overview");
        Assert.Equal("-1", cut.Find($"#{anchor}").GetAttribute("tabindex"));
        var next = cut.FindAll("button,fluent-button").Single(button => button.TextContent.Contains("下一部分", StringComparison.Ordinal));
        Assert.False(next.HasAttribute("disabled"));
        next.Click();
        cut.WaitForAssertion(() => Assert.Equal(2, host.Reader.Reads.Count));
        cut.WaitForAssertion(() => Assert.DoesNotContain(cut.FindAll("button,fluent-button"), button => button.TextContent.Contains("下一部分", StringComparison.Ordinal) && !button.HasAttribute("disabled")));
        Assert.Empty(cut.FindAll("#next-chunk"));
        Assert.Contains("已读完", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(handle, host.Reader.Reads[1].Document);
        await Task.CompletedTask;
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ReaderPage_MalformedQuery_ShowsSafeChineseFailureWithoutKnowledgeRead()
    {
        var host = new ExploreFixtureHost();
        Configure(host);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/knowledge/documents/read?batch=not-a-guid&publication=bad&logical=bad&version=bad&mode=IT");
        var cut = Render<DocumentReaderPage>();
        cut.WaitForAssertion(() => Assert.Contains("无效", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(host.Reader.Reads);
        Assert.Empty(host.Reader.Selections);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ReaderPage_BusinessContextAndItQuery_DoesNotElevateOrLeakCl()
    {
        var host = new ExploreFixtureHost(KnowledgeMode.Business);
        Configure(host);
        Services.GetRequiredService<NavigationManager>().NavigateTo(ExactDocumentLinkBuilder.Build(host.Fixture.Handle(host.Fixture.ClA)) + "&mode=IT");
        var cut = Render<DocumentReaderPage>();
        cut.WaitForAssertion(() => Assert.Contains("无法", cut.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("CL-A", cut.Markup, StringComparison.Ordinal);
        Assert.All(host.Reader.Selections, selection => Assert.Equal(KnowledgeMode.Business, selection.Mode));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void DocumentsPage_OrdinaryAuthorizedRoute_ShowsExactLinksWithoutManagementRequirement()
    {
        var host = new ExploreFixtureHost(KnowledgeMode.Business);
        Configure(host);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/knowledge/documents");
        var cut = Render<DocumentsPage>();
        cut.WaitForAssertion(() => Assert.Contains("订单处理", cut.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("隐藏业务标题", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(cut.FindAll("a"), link => link.GetAttribute("href") == ExactDocumentLinkBuilder.Build(host.Fixture.Handle(host.Fixture.BlA)));
        foreach (var type in new[] { typeof(DocumentsPage), typeof(DocumentReaderPage) })
        {
            var auth = Assert.Single(type.GetCustomAttributes<AuthorizeAttribute>());
            Assert.Null(auth.Policy);
            Assert.Null(auth.Roles);
        }
    }

    private void Configure(ExploreFixtureHost host, int pageSize = 20)
    {
        Services.AddFluentUIComponents();
        Services.AddSingleton<IExplorePresentationService>(host.Service);
        Services.AddSingleton(host.CreateReader());
        Services.AddSingleton(host.CreateBrowse());
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./Components/Pages/Explore/DocumentsPage.razor.js").Setup<int>("measurePageSize", _ => true).SetResult(pageSize);
    }
}

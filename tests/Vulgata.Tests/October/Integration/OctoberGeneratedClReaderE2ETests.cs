using System.Diagnostics;
using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Shared;
using Vulgata.Tests.OctoberScan;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Explore;
using Vulgata.Web.Data;
using Xunit.Abstractions;

namespace Vulgata.Tests.October.Integration;

/// <summary>
/// One real Generate and its exact persisted publication, ordinary Identity login and HTTP/SSR reader routes.
/// Execution requires separately approved owned PostgreSQL, prepared source, native tools and live Worker inputs.
/// This does not qualify interactive browser circuits, content continuation, Business generation or graph rendering.
/// </summary>
public sealed class OctoberGeneratedClReaderE2ETests(ITestOutputHelper output)
{
    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task GenerateOnce_OrdinaryItReaderOpensExactCl_BusinessDenied_RevocationClosesAccessAsync()
    {
        Assert.True(SuiteAuthorization.IsEnabled("E2E"), "The E2E suite must be explicitly enabled.");
        _ = TestResourceScope.RequireAdministrator();
        await using var fixture = await OctoberHostSelectionTests.GeneratorPrepareFixture.CreateAsync(output);
        TestResourceScope.ValidateOwnedTarget(fixture.ConnectionString);

        // The integration-owned helper guards approval before acquiring native/provider inputs.
        // No profile, credential, source approval or policy value is inferred in this test.
        var approved = await fixture.PrepareApprovedClInputsAsync(CancellationToken.None);
        await fixture.AssertOctoberSchemaPresentAsync();
        Assert.Equal(fixture.CanonicalDatabaseIdentity, approved.Binding.CanonicalDatabaseIdentity);
        Assert.Equal(fixture.TargetId, approved.Binding.TargetId);
        Assert.Equal(fixture.BatchId, approved.Binding.BatchId);
        Assert.Equal(fixture.RepositoryId, approved.Origin.RepositoryId);
        var adapter = OctoberScanGenerateQualificationFixtureAdapter.ForFixture(fixture, approved.Profile,
            approved.Policy, approved.Origin.RelativePath, approved.Files);
        var execution = await adapter.ExecuteOnceAsync();
        // Parse also rejects nonzero process exits. Never retry: failure may follow a committed publication.
        var report = OctoberGeneratedClReport.Parse(execution.ExitCode, execution.StandardOutput,
            approved.Binding, approved.Origin);
        Assert.NotEmpty(report.Documents);
        Assert.Equal(approved.Binding, report.Publication.Knowledge);

        var ordinary = await fixture.CreateOrdinaryReaderAsync(CancellationToken.None);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByIdAsync(ordinary.SubjectId));
            var roles = await users.GetRolesAsync(user);
            Assert.True(roles.Count == 1 && roles[0] == RoleNames.User, "The reader must have exactly the live User role.");
        }
        // Establish a successful Business selection before testing denial, so an unavailable host cannot pass.
        await WithWebAsync(fixture, report.Publication, KnowledgeMode.Business, async (client, token) =>
        {
            await LoginAsync(fixture, client, ordinary.UserName, ordinary.Password, token);
            using var browseResponse = await client.GetAsync("/knowledge/documents", token);
            Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);
            var browseDocument = await new HtmlParser().ParseDocumentAsync(await browseResponse.Content.ReadAsStringAsync(token), token);
            var browse = browseDocument.QuerySelector(".explore-documents");
            Assert.NotNull(browse);
            Assert.Equal("light", browse.GetAttribute("data-theme"));
            var list = browse.QuerySelector(".document-list");
            Assert.NotNull(list);
            Assert.Empty(list.Children);
            Assert.Contains("当前没有可读取的业务逻辑文档。", browse.TextContent, StringComparison.Ordinal);
            Assert.Null(browse.QuerySelector(".controlled-label"));
            foreach (var handle in report.Documents)
            {
                var denied = await fixture.ReadExactAsReaderAsync(report.Publication, handle,
                    ordinary.SubjectId, ordinary.SecurityStamp, KnowledgeMode.Business, token);
                AssertMetadataFree(denied);
                await AssertUnavailableRouteAsync(client, ExactDocumentLinkBuilder.Build(handle) + "&mode=IT", token);
            }
        });

        // Same account, database, source and report; only the trusted effective mode changes.
        await WithWebAsync(fixture, report.Publication, KnowledgeMode.IT, async (client, token) =>
        {
            await LoginAsync(fixture, client, ordinary.UserName, ordinary.Password, token);
            foreach (var handle in report.Documents)
            {
                var expected = await fixture.ReadExactAsReaderAsync(report.Publication, handle,
                    ordinary.SubjectId, ordinary.SecurityStamp, KnowledgeMode.IT, token);
                Assert.Equal(KnowledgeReadStatus.Success, expected.Status);
                Assert.Equal(handle, expected.Document!.Handle);
                Assert.Equal(DocumentKind.CodeLogic, expected.Document.Kind);
                Assert.False(string.IsNullOrWhiteSpace(expected.Content), "Generated CL must have readable content.");
                Assert.NotEmpty(expected.Claims);
                using var response = await client.GetAsync(ExactDocumentLinkBuilder.Build(handle), token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var document = await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync(token), token);
                var reader = document.QuerySelector(".explore-reader");
                Assert.NotNull(reader);
                Assert.Equal("dark", reader.GetAttribute("data-theme"));
                Assert.Equal(expected.Document.Title, reader.QuerySelector("h2")?.TextContent);
                Assert.Equal(expected.Content, reader.QuerySelector(".document-content")?.TextContent);
                Assert.Contains("代码逻辑文档", reader.TextContent, StringComparison.Ordinal);
                Assert.Null(reader.QuerySelector(".controlled-label"));
                foreach (var claim in expected.Claims)
                {
                    Assert.Contains(reader.QuerySelectorAll(".claim-text"), element => element.TextContent == claim.Text);
                }
            }

            var exact = report.Documents[0];
            var wrongSection = "unpublished-section-" + Guid.NewGuid().ToString("N");
            foreach (var wrong in new[]
            {
                exact with { BatchId = Guid.NewGuid() },
                exact with { PublicationId = Guid.NewGuid() },
                exact with { LogicalDocumentId = Guid.NewGuid() },
                exact with { DocumentVersionId = Guid.NewGuid() },
                exact with { SectionId = wrongSection },
            })
            {
                var denied = await fixture.ReadExactAsReaderAsync(report.Publication, wrong,
                    ordinary.SubjectId, ordinary.SecurityStamp, KnowledgeMode.IT, token);
                AssertMetadataFree(denied);
                await AssertUnavailableRouteAsync(client, ExactDocumentLinkBuilder.Build(wrong), token);
            }

            var currentStamp = await fixture.RotateReaderSecurityStampAsync(ordinary.SubjectId, token);
            Assert.True(!string.Equals(ordinary.SecurityStamp, currentStamp, StringComparison.Ordinal),
                "The live Identity security stamp must change.");
            var revoked = await fixture.ReadExactAsReaderAsync(report.Publication, exact,
                ordinary.SubjectId, ordinary.SecurityStamp, KnowledgeMode.IT, token);
            AssertMetadataFree(revoked);
            // Keep the original cookie. Re-login here would conceal whether current authority is rechecked.
            await AssertUnavailableRouteAsync(client, ExactDocumentLinkBuilder.Build(exact), token, allowLoginRedirect: true);
        });

        output.WriteLine("One approved production Generate; exact generated CL publication read through actual Identity and HTTP/SSR routes.");
        output.WriteLine("Interactive browser/circuit, continuation, BL generation and graph qualification were not performed by this scenario.");
    }

    private static async Task LoginAsync(OctoberHostSelectionTests.GeneratorPrepareFixture fixture, HttpClient client,
        string userName, string password, CancellationToken cancellationToken)
    {
        using var login = await fixture.LoginReaderAsync(client, userName, password, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var location = Assert.IsType<Uri>(login.Headers.Location);
        var destination = location.IsAbsoluteUri ? location : new Uri(client.BaseAddress!, location);
        Assert.Equal(client.BaseAddress!.GetLeftPart(UriPartial.Authority), destination.GetLeftPart(UriPartial.Authority));
        Assert.Equal("/", destination.AbsolutePath);
    }

    private static void AssertMetadataFree(ContentPage page)
    {
        Assert.Equal(KnowledgeReadStatus.Unavailable, page.Status);
        Assert.Null(page.Document);
        Assert.Null(page.Content);
        Assert.Empty(page.Claims);
        Assert.Null(page.Next);
        Assert.Null(page.Coverage);
    }

    private static async Task AssertUnavailableRouteAsync(HttpClient client, string relativeUrl, CancellationToken token,
        bool allowLoginRedirect = false)
    {
        using var response = await client.GetAsync(relativeUrl, token);
        if (allowLoginRedirect && response.StatusCode == HttpStatusCode.Redirect)
        {
            var location = Assert.IsType<Uri>(response.Headers.Location);
            var destination = location.IsAbsoluteUri ? location : new Uri(client.BaseAddress!, location);
            Assert.Equal(client.BaseAddress!.GetLeftPart(UriPartial.Authority), destination.GetLeftPart(UriPartial.Authority));
            Assert.Equal("/Account/Login", destination.AbsolutePath);
            return;
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync(token), token);
        var reader = document.QuerySelector(".explore-reader");
        Assert.NotNull(reader);
        Assert.Null(reader.QuerySelector("h2"));
        Assert.Null(reader.QuerySelector(".document-content"));
        Assert.Empty(reader.QuerySelectorAll(".qualified-claim,.source-reference,.document-sections"));
        Assert.Contains("无法读取此文档", reader.TextContent, StringComparison.Ordinal);
    }

    private static async Task WithWebAsync(OctoberHostSelectionTests.GeneratorPrepareFixture fixture,
        PublicationBinding publication, KnowledgeMode mode, Func<HttpClient, CancellationToken, Task> scenario)
    {
        int port = OctoberHostSelectionTests.ReserveOwnedLoopbackPort();
        using var process = fixture.StartWeb(port, publication, mode);
        // Drain this test's process. Shared readiness may emit its own sanitized diagnostics.
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, CookieContainer = new() };
            using var client = new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(30) };
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await OctoberHostSelectionTests.WaitForWebStartupAsync(process, port, fixture, standardOutput, standardError);
            await scenario(client, timeout.Token);
        }
        finally
        {
            await OctoberHostSelectionTests.StopOwnedWebProcessAsync(process);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await Task.WhenAll(standardOutput, standardError).WaitAsync(cleanup.Token);
            Assert.True(process.HasExited, "The owned Web process must terminate before fixture disposal.");
        }
    }

}

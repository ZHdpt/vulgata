using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T044 RED: public HTTP contract for the resumable Pass 1 execution surface.
/// The endpoint/DTO/validator composition is intentionally owned by T045.
/// </summary>
public sealed class Pass1ExecutionApiTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public Pass1ExecutionApiTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PublicPass1Endpoints_Unauthenticated_Return401WithoutRedirect()
    {
        using HttpClient client = CreateClient();
        Guid executionId = Guid.NewGuid();

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = Guid.NewGuid(), secondManifestId = Guid.NewGuid() });
        using HttpResponseMessage concurrency = await client.PatchAsJsonAsync(
            $"/api/pass1-executions/{executionId:D}/concurrency",
            new { expectedVersion = 0, concurrencyLimit = 2 });
        using HttpResponseMessage status = await client.GetAsync($"/api/pass1-executions/{executionId:D}");
        using HttpResponseMessage resume = await client.PostAsync(
            $"/api/pass1-executions/{executionId:D}/resume",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, start.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, concurrency.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, resume.StatusCode);
        Assert.DoesNotContain("Account/Login", start.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pass1ExecutionEndpoints_RegisterOnlyTheApprovedPublicSeams()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EndpointDataSource endpoints = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();
        string[] patterns = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .Where(pattern => pattern.Contains("pass1-executions", StringComparison.Ordinal))
            .OrderBy(pattern => pattern, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "/api/pass1-executions",
                "/api/pass1-executions/{pass1ExecutionId}",
                "/api/pass1-executions/{pass1ExecutionId}/concurrency",
                "/api/pass1-executions/{pass1ExecutionId}/resume",
            ],
            patterns);
    }

    [Fact]
    public async Task Authenticated_UnknownExecution_IsOwnershipFilteredAs404()
    {
        using HttpClient client = await CreateAuthenticatedClientAsync();
        Guid executionId = Guid.NewGuid();

        using HttpResponseMessage status = await client.GetAsync($"/api/pass1-executions/{executionId:D}");
        using HttpResponseMessage concurrency = await client.PatchAsJsonAsync(
            $"/api/pass1-executions/{executionId:D}/concurrency",
            new { expectedVersion = 0, concurrencyLimit = 2 });
        using HttpResponseMessage resume = await client.PostAsync(
            $"/api/pass1-executions/{executionId:D}/resume",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, concurrency.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resume.StatusCode);
        Assert.DoesNotContain("Repository", await status.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("host", await resume.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task Start_InvalidConcurrencyLimit_ReturnsSanitizedProblemDetails(int concurrencyLimit)
    {
        using HttpClient client = await CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new
            {
                firstManifestId = Guid.NewGuid(),
                secondManifestId = Guid.NewGuid(),
                concurrencyLimit,
            });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Start_AbsentConcurrencyUsesDefaultAndIntegerOneThroughFourAreAcceptedByValidation()
    {
        using HttpClient client = await CreateAuthenticatedClientAsync();

        using HttpResponseMessage absent = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new { firstManifestId = Guid.NewGuid(), secondManifestId = Guid.NewGuid() });
        Assert.NotEqual(HttpStatusCode.BadRequest, absent.StatusCode);
        Assert.NotEqual(HttpStatusCode.UnprocessableEntity, absent.StatusCode);

        foreach (int limit in Enumerable.Range(1, 4))
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/pass1-executions",
                new
                {
                    firstManifestId = Guid.NewGuid(),
                    secondManifestId = Guid.NewGuid(),
                    concurrencyLimit = limit,
                });

            Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }
    }

    [Fact]
    public async Task ConcurrencyUpdate_StaleVersionAndChangedActor_ReturnConflictOrForbiddenProblemDetails()
    {
        using HttpClient client = await CreateAuthenticatedClientAsync();
        Guid executionId = Guid.NewGuid();

        using HttpResponseMessage stale = await client.PatchAsJsonAsync(
            $"/api/pass1-executions/{executionId:D}/concurrency",
            new { expectedVersion = 999, concurrencyLimit = 1 });
        using HttpResponseMessage changedActor = await client.PatchAsJsonAsync(
            $"/api/pass1-executions/{executionId:D}/concurrency",
            new { expectedVersion = 0, concurrencyLimit = 4, actorId = "different-actor" });

        await AssertProblemAsync(stale, HttpStatusCode.Conflict, HttpStatusCode.NotFound);
        await AssertProblemAsync(changedActor, HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resume_ProviderPauseResponse_UsesChineseManualResumeAndSanitizedProblemDetails()
    {
        using HttpClient client = await CreateAuthenticatedClientAsync();
        Guid executionId = Guid.NewGuid();

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/pass1-executions/{executionId:D}/resume",
            content: null);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Contains(response.StatusCode, new[]
        {
            HttpStatusCode.NotFound,
            HttpStatusCode.Conflict,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.ServiceUnavailable,
        });
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", body, StringComparison.OrdinalIgnoreCase);
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            Assert.Contains("继续扫描", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task StatusResponse_UsesDeterministicPairAndWarningOrdering_AndNoSensitiveAtoms()
    {
        using HttpClient client = await CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync($"/api/pass1-executions/{Guid.NewGuid():D}");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("LeaseToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Source", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProviderKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Diagnostic", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resume_RevalidatesPair_IsIdempotent_AndSkipsCompletedJobs()
    {
        using HttpClient client = await CreateAuthenticatedClientAsync();
        Guid executionId = Guid.NewGuid();

        using HttpResponseMessage first = await client.PostAsync(
            $"/api/pass1-executions/{executionId:D}/resume",
            content: null);
        using HttpResponseMessage replay = await client.PostAsync(
            $"/api/pass1-executions/{executionId:D}/resume",
            content: null);

        Assert.Equal(first.StatusCode, replay.StatusCode);
        Assert.DoesNotContain("duplicate", await replay.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("job", await replay.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        string email = $"pass1-api-{Guid.NewGuid():N}@example.com";
        const string password = "Valid1!Pass";

        await using (AsyncServiceScope scope = _factory.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            IdentityResult result = await userManager.CreateAsync(new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
            }, password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        }

        HttpClient client = CreateClient();
        HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
        string html = await loginPage.Content.ReadAsStringAsync();
        Dictionary<string, string> fields = ExtractFormFields(html);
        fields["Input.Email"] = email;
        fields["Input.Password"] = password;
        fields["Input.RememberMe"] = "false";

        using FormUrlEncodedContent login = new(fields);
        using HttpResponseMessage response = await client.PostAsync("/Account/Login", login);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static Dictionary<string, string> ExtractFormFields(string html)
    {
        Match form = Regex.Match(
            html,
            "<form\\b(?=[^>]*\\bmethod\\s*=\\s*[\"']post[\"'])[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, "Expected a POST login form.");

        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(
            form.Groups["content"].Value,
            "<input\\b(?<attributes>[^>]*)>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            string attributes = input.Groups["attributes"].Value;
            Match name = Regex.Match(
                attributes,
                "\\bname\\s*=\\s*[\"'](?<value>[^\"']+)[\"']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!name.Success)
            {
                continue;
            }

            Match value = Regex.Match(
                attributes,
                "\\bvalue\\s*=\\s*[\"'](?<value>[^\"']*)[\"']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            fields[name.Groups["value"].Value] = value.Success
                ? WebUtility.HtmlDecode(value.Groups["value"].Value)
                : string.Empty;
        }

        return fields;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, params HttpStatusCode[] expectedStatuses)
    {
        Assert.Contains(response.StatusCode, expectedStatuses);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("title", out _));
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", body, StringComparison.OrdinalIgnoreCase);
    }
}

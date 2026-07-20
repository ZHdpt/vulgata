using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Shared;
using Vulgata.Shared.Repositories;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class RepositoryApiTests
    : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public RepositoryApiTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ScanRunSummaryMapping_WithUnsortedLanguageSummaries_ReturnsOrdinallySortedBreakdown()
    {
        Guid repositoryId = Guid.NewGuid();
        ScanRun run = ScanRun.Start(repositoryId, 1_800, DateTimeOffset.UtcNow);
        run.SetCandidateCount(0);
        run.Complete(0, 0, DateTimeOffset.UtcNow);
        run.LanguageSummaries.Add(new ScanRunLanguageSummary(run.Id, repositoryId, "zig_0+#.-", 1));
        run.LanguageSummaries.Add(new ScanRunLanguageSummary(run.Id, repositoryId, "typescript", 3));
        run.LanguageSummaries.Add(new ScanRunLanguageSummary(run.Id, repositoryId, "c_sharp", 2));
        MethodInfo? map = typeof(RepositoryProfilingCoordinator).GetMethod(
            "Map",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(map);

        var summary = Assert.IsType<ScanRunSummaryDto>(map!.Invoke(null, [run]));

        Assert.Equal(
            [
                new LanguageValue("c_sharp", 2),
                new LanguageValue("typescript", 3),
                new LanguageValue("zig_0+#.-", 1),
            ],
            ReadLanguages(summary));
    }

    [Fact]
    public async Task ManagementScanRunRoutes_AuthorizedResponse_ContainsEverySortedLanguageAndDistinctFileCountAsync()
    {
        ScanRunSummaryDto summary = CreateSummaryWithLanguages();
        using WebApplicationFactory<Program> factory = CreateFactory(summary);
        const string email = "story42.languages.admin@example.com";
        await CreateUserWithRoleAsync(factory.Services, email, RoleNames.Administrator);
        using HttpClient client = CreateClient(factory);
        await LoginAsync(client, email);
        Guid systemId = Guid.NewGuid();
        Guid repositoryId = Guid.NewGuid();
        Guid scanRunId = summary.Id;
        string[] routes =
        [
            $"/api/systems/{systemId}/repositories/{repositoryId}/scan-runs/{scanRunId}",
            $"/api/repositories/standalone/{repositoryId}/scan-runs/{scanRunId}",
        ];

        foreach (string route in routes)
        {
            using HttpResponseMessage response = await client.GetAsync(route);
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsByteArrayAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(document.RootElement.TryGetProperty("languages", out JsonElement languages));
            Assert.Equal(
                [
                    new LanguageValue("c_sharp", 2),
                    new LanguageValue("typescript", 3),
                    new LanguageValue("zig_0+#.-", 1),
                ],
                languages.EnumerateArray()
                    .Select(item => new LanguageValue(
                        item.GetProperty("language").GetString()!,
                        item.GetProperty("distinctFileCount").GetInt32()))
                    .ToArray());
        }
    }

    [Fact]
    public async Task ManagementScanRunRoutes_WhenVisibilityIsRedacted_ReturnNotFoundWithoutLanguageDisclosureAsync()
    {
        using WebApplicationFactory<Program> factory = CreateFactory(null);
        const string email = "story42.languages.hidden@example.com";
        await CreateUserWithRoleAsync(factory.Services, email, RoleNames.Administrator);
        using HttpClient client = CreateClient(factory);
        await LoginAsync(client, email);
        Guid systemId = Guid.NewGuid();
        Guid repositoryId = Guid.NewGuid();
        Guid scanRunId = Guid.NewGuid();
        string[] routes =
        [
            $"/api/systems/{systemId}/repositories/{repositoryId}/scan-runs/{scanRunId}",
            $"/api/repositories/standalone/{repositoryId}/scan-runs/{scanRunId}",
        ];

        foreach (string route in routes)
        {
            using HttpResponseMessage response = await client.GetAsync(route);
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("扫描记录不存在", body, StringComparison.Ordinal);
            Assert.DoesNotContain("c_sharp", body, StringComparison.Ordinal);
            Assert.DoesNotContain("typescript", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ManagementScanRunRoutes_UserWithoutManagementAccess_ReturnForbiddenAsync()
    {
        using WebApplicationFactory<Program> factory = CreateFactory(CreateSummaryWithLanguages());
        const string email = "story42.languages.user@example.com";
        await CreateUserWithRoleAsync(factory.Services, email, RoleNames.User);
        using HttpClient client = CreateClient(factory);
        await LoginAsync(client, email);
        Guid systemId = Guid.NewGuid();
        Guid repositoryId = Guid.NewGuid();
        Guid scanRunId = Guid.NewGuid();
        string[] routes =
        [
            $"/api/systems/{systemId}/repositories/{repositoryId}/scan-runs/{scanRunId}",
            $"/api/repositories/standalone/{repositoryId}/scan-runs/{scanRunId}",
        ];

        foreach (string route in routes)
        {
            using HttpResponseMessage response = await client.GetAsync(route);
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("c_sharp", body, StringComparison.Ordinal);
            Assert.DoesNotContain("typescript", body, StringComparison.Ordinal);
        }
    }

    private WebApplicationFactory<Program> CreateFactory(ScanRunSummaryDto? summary) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRepositoryProfilingCoordinator>();
                services.AddSingleton<IRepositoryProfilingCoordinator>(
                    new StubRepositoryProfilingCoordinator(summary));
            }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private static async Task CreateUserWithRoleAsync(
        IServiceProvider services,
        string email,
        string role)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        IdentityResult created = await userManager.CreateAsync(user, "Valid1!Pass");
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
        IdentityResult assigned = await userManager.AddToRoleAsync(user, role);
        Assert.True(assigned.Succeeded, string.Join(", ", assigned.Errors.Select(error => error.Description)));
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using HttpResponseMessage get = await client.GetAsync("/Account/Login");
        string html = await get.Content.ReadAsStringAsync();
        Match form = Regex.Match(
            html,
            "<form[^>]*method=\"post\"[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success);
        Dictionary<string, string> fields = Regex.Matches(
                form.Groups["content"].Value,
                "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"[^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .ToDictionary(
                match => WebUtility.HtmlDecode(match.Groups["name"].Value),
                match => WebUtility.HtmlDecode(match.Groups["value"].Value),
                StringComparer.Ordinal);
        fields["Input.Email"] = email;
        fields["Input.Password"] = "Valid1!Pass";
        fields["Input.RememberMe"] = "false";

        using HttpResponseMessage response = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static ScanRunSummaryDto CreateSummaryWithLanguages()
    {
        var summary = new ScanRunSummaryDto
        {
            Id = Guid.NewGuid(),
            RepositoryId = Guid.NewGuid(),
            Status = "已完成",
            ParsedFileCount = 6,
            TotalCandidateFileCount = 6,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
        };
        PropertyInfo? property = typeof(ScanRunSummaryDto).GetProperty("Languages");
        if (property is null)
        {
            return summary;
        }

        Type elementType = property.PropertyType.GetGenericArguments().Single();
        Array values = Array.CreateInstance(elementType, 3);
        values.SetValue(CreateLanguageValue(elementType, "c_sharp", 2), 0);
        values.SetValue(CreateLanguageValue(elementType, "typescript", 3), 1);
        values.SetValue(CreateLanguageValue(elementType, "zig_0+#.-", 1), 2);
        property.SetValue(summary, values);
        return summary;
    }

    private static object CreateLanguageValue(Type type, string language, int distinctFileCount)
    {
        object value = Activator.CreateInstance(type)!;
        type.GetProperty("Language")!.SetValue(value, language);
        type.GetProperty("DistinctFileCount")!.SetValue(value, distinctFileCount);
        return value;
    }

    private static LanguageValue[] ReadLanguages(ScanRunSummaryDto summary)
    {
        PropertyInfo? property = typeof(ScanRunSummaryDto).GetProperty("Languages");
        Assert.NotNull(property);
        var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            property!.GetValue(summary));
        return values.Cast<object>()
            .Select(value => new LanguageValue(
                (string)value.GetType().GetProperty("Language")!.GetValue(value)!,
                (int)value.GetType().GetProperty("DistinctFileCount")!.GetValue(value)!))
            .ToArray();
    }

    private sealed record LanguageValue(string Language, int DistinctFileCount);

    private sealed class StubRepositoryProfilingCoordinator(ScanRunSummaryDto? summary)
        : IRepositoryProfilingCoordinator
    {
        public Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
            Guid systemId,
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(summary is null
                ? new RepositoryProfilingResult(RepositoryProfilingOutcome.RepositoryNotFound)
                : new RepositoryProfilingResult(RepositoryProfilingOutcome.Success, summary));

        public Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(summary is null
                ? new RepositoryProfilingResult(RepositoryProfilingOutcome.RepositoryNotFound)
                : new RepositoryProfilingResult(RepositoryProfilingOutcome.Success, summary));

        public Task<ScanRunSummaryDto?> GetSystemRepositoryRunAsync(
            Guid systemId,
            Guid repositoryId,
            Guid scanRunId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(summary);

        public Task<ScanRunSummaryDto?> GetStandaloneRepositoryRunAsync(
            Guid repositoryId,
            Guid scanRunId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(summary);
    }
}

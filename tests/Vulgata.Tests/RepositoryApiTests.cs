using System.Net;
using System.Reflection;
using System.Text;
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
    public void ScanRunSummaryMapping_WithDuplicateUnsortedUnknownOutcomes_ReturnsExactDistinctOrdinalPaths()
    {
        Guid repositoryId = Guid.NewGuid();
        ScanRun run = ScanRun.Start(repositoryId, 1_800, DateTimeOffset.UtcNow);
        run.SetCandidateCount(3);
        run.Complete(0, 0, DateTimeOffset.UtcNow);
        foreach (string path in
                 new[] { "src/Zeta.txt", "src/Alpha.txt", "src/Zeta.txt", "src/Middle.txt" })
        {
            run.CandidateOutcomes.Add(new ScanCandidateOutcome(
                run.Id,
                path,
                ScanCandidateOutcomeKind.Filtered,
                ScanCandidateOutcomeReasons.UnknownLanguage,
                0,
                DateTimeOffset.UtcNow));
        }

        MethodInfo? map = typeof(RepositoryProfilingCoordinator).GetMethod(
            "Map",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(map);

        var summary = Assert.IsType<ScanRunSummaryDto>(map!.Invoke(null, [run]));

        UnknownLanguageValue unknownLanguage = ReadUnknownLanguage(summary);
        Assert.Equal(3, unknownLanguage.DistinctFileCount);
        Assert.Equal(
            ["src/Alpha.txt", "src/Middle.txt", "src/Zeta.txt"],
            unknownLanguage.Paths);
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
    public async Task ManagementScanRunRoutes_AuthorizedResponse_ContainsSortedFrameworksAndSharedAuditSamplesAsync()
    {
        ScanRunSummaryDto summary = CreateSummaryWithFrameworkOverview();
        using WebApplicationFactory<Program> factory = CreateFactory(summary);
        const string email = "story42.frameworks.admin@example.com";
        await CreateUserWithRoleAsync(factory.Services, email, RoleNames.Administrator);
        using HttpClient client = CreateClient(factory);
        await LoginAsync(client, email);
        string route =
            $"/api/repositories/standalone/{summary.RepositoryId}/scan-runs/{summary.Id}";

        using HttpResponseMessage response = await client.GetAsync(route);
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement frameworks = document.RootElement.GetProperty("frameworks");
        Assert.Equal(
            ["express", "react"],
            frameworks.EnumerateArray()
                .Select(item => item.GetProperty("frameworkName").GetString()!)
                .ToArray());
        JsonElement react = frameworks[1];
        Assert.Equal("High", react.GetProperty("strongestConfidence").GetString());
        Assert.Equal(2, react.GetProperty("codeUnitCount").GetInt32());
        Assert.Equal(3, react.GetProperty("totalFactCount").GetInt32());
        Assert.Equal(
            [
                "package.json#json:/dependencies/react",
                "package-lock.json#json:/packages/node_modules~1react",
            ],
            react.GetProperty("evidenceSamples")
                .EnumerateArray()
                .Select(item => item.GetProperty("detectionSource").GetString()!)
                .ToArray());
        JsonElement audit = document.RootElement.GetProperty("frameworkEvidenceAudit");
        Assert.Equal(40, audit.GetProperty("sampledOutcomeCount").GetInt32());
        Assert.Equal(101, audit.GetProperty("totalOutcomeCount").GetInt32());
        Assert.True(audit.GetProperty("samplingApplied").GetBoolean());
        int sharedDetails = frameworks.EnumerateArray()
            .Sum(item => item.GetProperty("evidenceSamples").GetArrayLength()) +
            audit.GetProperty("details").GetArrayLength();
        Assert.InRange(sharedDetails, 1, 100);
        string body = document.RootElement.GetRawText();
        Assert.DoesNotContain("C:\\", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RAW-SECRET", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManagementScanRunRoute_AuthorizedResponse_ContainsAllTenThousandSortedUnknownPathsAsync()
    {
        string[] expectedPaths =
        [
            .. Enumerable.Range(0, 10_000)
                .Select(index => $"src/unknown/{index:D5}.txt"),
        ];
        ScanRunSummaryDto summary = CreateSummaryWithUnknownLanguage(
            [.. expectedPaths.Reverse(), expectedPaths[0]]);
        using WebApplicationFactory<Program> factory = CreateFactory(summary);
        const string email = "story42.unknown.admin@example.com";
        await CreateUserWithRoleAsync(factory.Services, email, RoleNames.Administrator);
        using HttpClient client = CreateClient(factory);
        await LoginAsync(client, email);
        string route =
            $"/api/repositories/standalone/{summary.RepositoryId}/scan-runs/{summary.Id}";

        using HttpResponseMessage response = await client.GetAsync(route);
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement unknownLanguage = document.RootElement.GetProperty("unknownLanguage");
        Assert.Equal(10_000, unknownLanguage.GetProperty("distinctFileCount").GetInt32());
        Assert.Equal(
            expectedPaths,
            unknownLanguage.GetProperty("paths")
                .EnumerateArray()
                .Select(path => path.GetString()!)
                .ToArray());
    }

    [Fact]
    public async Task ManagementScanRunRoute_AuthorizedResponse_ContainsAllUnknownPathsAtSixteenMebibyteBoundAsync()
    {
        string[] expectedPaths = CreateExactUnknownAuditByteBoundPaths();
        ScanRunSummaryDto summary = CreateSummaryWithUnknownLanguage(
            [.. expectedPaths.Reverse()]);
        using WebApplicationFactory<Program> factory = CreateFactory(summary);
        const string email = "story42.unknown-byte-bound.admin@example.com";
        await CreateUserWithRoleAsync(factory.Services, email, RoleNames.Administrator);
        using HttpClient client = CreateClient(factory);
        await LoginAsync(client, email);
        string route =
            $"/api/repositories/standalone/{summary.RepositoryId}/scan-runs/{summary.Id}";

        using HttpResponseMessage response = await client.GetAsync(route);
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement unknownLanguage = document.RootElement.GetProperty("unknownLanguage");
        Assert.Equal(expectedPaths.Length, unknownLanguage.GetProperty("distinctFileCount").GetInt32());
        Assert.Equal(
            expectedPaths,
            unknownLanguage.GetProperty("paths")
                .EnumerateArray()
                .Select(path => path.GetString()!)
                .ToArray());
    }

    [Fact]
    public async Task ManagementScanRunStartRoute_WhenCandidateAuditBoundIsExceeded_ReturnsBadRequestAsync()
    {
        Assert.True(Enum.TryParse(
            "CandidateAuditBoundExceeded",
            out RepositoryProfilingOutcome outcome));
        const string message = "候选源文件审计范围超出限制。";
        using WebApplicationFactory<Program> factory = CreateFactory(
            null,
            new RepositoryProfilingResult(outcome, null, message));
        const string email = "story42.unknown-bound.admin@example.com";
        await CreateUserWithRoleAsync(factory.Services, email, RoleNames.Administrator);
        using HttpClient client = CreateClient(factory);
        await LoginAsync(client, email);

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/repositories/standalone/{Guid.NewGuid()}/scan-runs",
            content: null);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(message, body, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeGraph", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("提供程序", body, StringComparison.Ordinal);
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
            Assert.DoesNotContain("react", body, StringComparison.Ordinal);
            Assert.DoesNotContain("检测依据", body, StringComparison.Ordinal);
            Assert.DoesNotContain("未知语言文件", body, StringComparison.Ordinal);
            Assert.DoesNotContain("src/unknown", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ManagementScanRunRoutes_UserWithoutManagementAccess_ReturnForbiddenAsync()
    {
        using WebApplicationFactory<Program> factory = CreateFactory(
            CreateSummaryWithUnknownLanguage(["src/unknown/Secret.txt"]));
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
            Assert.DoesNotContain("react", body, StringComparison.Ordinal);
            Assert.DoesNotContain("检测依据", body, StringComparison.Ordinal);
            Assert.DoesNotContain("未知语言文件", body, StringComparison.Ordinal);
            Assert.DoesNotContain("src/unknown", body, StringComparison.Ordinal);
        }
    }

    private WebApplicationFactory<Program> CreateFactory(
        ScanRunSummaryDto? summary,
        RepositoryProfilingResult? startResult = null) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRepositoryProfilingCoordinator>();
                services.AddSingleton<IRepositoryProfilingCoordinator>(
                    new StubRepositoryProfilingCoordinator(summary, startResult));
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

    private static ScanRunSummaryDto CreateSummaryWithFrameworkOverview()
    {
        ScanRunSummaryDto summary = CreateSummaryWithLanguages();
        PropertyInfo? frameworksProperty = typeof(ScanRunSummaryDto).GetProperty("Frameworks");
        PropertyInfo? auditProperty = typeof(ScanRunSummaryDto).GetProperty("FrameworkEvidenceAudit");
        Assert.NotNull(frameworksProperty);
        Assert.NotNull(auditProperty);

        Type frameworkType = frameworksProperty!.PropertyType.GetGenericArguments().Single();
        Array frameworks = Array.CreateInstance(frameworkType, 2);
        frameworks.SetValue(CreateFrameworkSummary(
            frameworkType,
            "react",
            2,
            "High",
            3,
            [
                (Guid.Parse("00000000-0000-0000-0000-000000000001"), "High", "package.json#json:/dependencies/react"),
                (Guid.Parse("00000000-0000-0000-0000-000000000002"), "Medium", "package-lock.json#json:/packages/node_modules~1react"),
            ]), 0);
        frameworks.SetValue(CreateFrameworkSummary(
            frameworkType,
            "express",
            1,
            "High",
            1,
            []), 1);
        frameworksProperty.SetValue(summary, frameworks);

        Type auditType = auditProperty!.PropertyType;
        object audit = Activator.CreateInstance(auditType)!;
        auditType.GetProperty("SampledOutcomeCount")!.SetValue(audit, 40);
        auditType.GetProperty("TotalOutcomeCount")!.SetValue(audit, 101);
        auditType.GetProperty("SamplingApplied")!.SetValue(audit, true);
        PropertyInfo detailsProperty = auditType.GetProperty("Details")!;
        Type detailType = detailsProperty.PropertyType.GetGenericArguments().Single();
        Array details = Array.CreateInstance(detailType, 40);
        for (var index = 0; index < details.Length; index++)
        {
            object detail = Activator.CreateInstance(detailType)!;
            detailType.GetProperty("Path")!.SetValue(detail, $"audit/{index:D3}/package.json");
            detailType.GetProperty("Outcome")!.SetValue(detail, "Skipped");
            detailType.GetProperty("Reason")!.SetValue(
                detail,
                ScanCandidateOutcomeReasons.FrameworkEvidenceUnsupported);
            details.SetValue(detail, index);
        }

        detailsProperty.SetValue(audit, details);
        auditProperty.SetValue(summary, audit);
        return summary;
    }

    private static ScanRunSummaryDto CreateSummaryWithUnknownLanguage(
        IReadOnlyList<string> paths)
    {
        ScanRunSummaryDto summary = CreateSummaryWithLanguages();
        PropertyInfo? property = typeof(ScanRunSummaryDto).GetProperty("UnknownLanguage");
        if (property is null)
        {
            return summary;
        }

        object value = Activator.CreateInstance(property.PropertyType)!;
        value.GetType().GetProperty("Paths")!.SetValue(
            value,
            paths
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray());
        property.SetValue(summary, value);
        return summary;
    }

    private static string[] CreateExactUnknownAuditByteBoundPaths()
    {
        const int pathCount = 8_192;
        const int pathByteCount = 2_048;
        string[] paths =
        [
            .. Enumerable.Range(0, pathCount).Select(index =>
            {
                string prefix = $"src/{index:D4}/";
                return prefix + new string('x', pathByteCount - prefix.Length);
            }),
        ];
        Assert.Equal(
            16 * 1024 * 1024,
            paths.Sum(Encoding.UTF8.GetByteCount));
        return paths;
    }

    private static object CreateFrameworkSummary(
        Type frameworkType,
        string name,
        int codeUnitCount,
        string strongestConfidence,
        int totalFactCount,
        IReadOnlyList<(Guid CodeUnitId, string Confidence, string DetectionSource)> samples)
    {
        object framework = Activator.CreateInstance(frameworkType)!;
        frameworkType.GetProperty("FrameworkName")!.SetValue(framework, name);
        frameworkType.GetProperty("CodeUnitCount")!.SetValue(framework, codeUnitCount);
        frameworkType.GetProperty("StrongestConfidence")!.SetValue(framework, strongestConfidence);
        frameworkType.GetProperty("TotalFactCount")!.SetValue(framework, totalFactCount);
        PropertyInfo samplesProperty = frameworkType.GetProperty("EvidenceSamples")!;
        Type sampleType = samplesProperty.PropertyType.GetGenericArguments().Single();
        Array values = Array.CreateInstance(sampleType, samples.Count);
        for (var index = 0; index < samples.Count; index++)
        {
            object sample = Activator.CreateInstance(sampleType)!;
            sampleType.GetProperty("CodeUnitId")!.SetValue(sample, samples[index].CodeUnitId);
            sampleType.GetProperty("Confidence")!.SetValue(sample, samples[index].Confidence);
            sampleType.GetProperty("DetectionSource")!.SetValue(sample, samples[index].DetectionSource);
            values.SetValue(sample, index);
        }

        samplesProperty.SetValue(framework, values);
        return framework;
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

    private static UnknownLanguageValue ReadUnknownLanguage(ScanRunSummaryDto summary)
    {
        PropertyInfo? property = typeof(ScanRunSummaryDto).GetProperty("UnknownLanguage");
        Assert.NotNull(property);
        object value = property!.GetValue(summary)!;
        int distinctFileCount =
            (int)value.GetType().GetProperty("DistinctFileCount")!.GetValue(value)!;
        var paths = Assert.IsAssignableFrom<IEnumerable<string>>(
            value.GetType().GetProperty("Paths")!.GetValue(value));
        return new UnknownLanguageValue(distinctFileCount, paths.ToArray());
    }

    private sealed record LanguageValue(string Language, int DistinctFileCount);
    private sealed record UnknownLanguageValue(
        int DistinctFileCount,
        IReadOnlyList<string> Paths);

    private sealed class StubRepositoryProfilingCoordinator(
        ScanRunSummaryDto? summary,
        RepositoryProfilingResult? startResult = null)
        : IRepositoryProfilingCoordinator
    {
        public Task<RepositoryProfilingResult> StartSystemRepositoryAsync(
            Guid systemId,
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(startResult ?? (summary is null
                ? new RepositoryProfilingResult(RepositoryProfilingOutcome.RepositoryNotFound)
                : new RepositoryProfilingResult(RepositoryProfilingOutcome.Success, summary)));

        public Task<RepositoryProfilingResult> StartStandaloneRepositoryAsync(
            Guid repositoryId,
            IUserContext user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(startResult ?? (summary is null
                ? new RepositoryProfilingResult(RepositoryProfilingOutcome.RepositoryNotFound)
                : new RepositoryProfilingResult(RepositoryProfilingOutcome.Success, summary)));

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

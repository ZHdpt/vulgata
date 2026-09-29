using Vulgata.Tests.TestInfrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Shared.Systems;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class SystemCrudAdminTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public SystemCrudAdminTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Administrator_SystemManagementPage_ShowsChineseTreeAndEmptyState()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story21.admin.page@example.com", "Valid1!Pass", RoleNames.Administrator);

        using var client = CreateClient();
        await LoginAsync(client, "story21.admin.page@example.com", "Valid1!Pass");

        var response = await client.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("系统树", html, StringComparison.Ordinal);
        Assert.Contains("+ 新建系统", html, StringComparison.Ordinal);
        Assert.Contains("尚未添加任何系统。创建你的第一个系统开始扫描。", html, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Administrator_SystemManagementPage_UsesSharedChatNavigationShell()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        const string email = "story21.admin.shell@example.com";
        await CreateUserWithRolesAsync(email, "Valid1!Pass", RoleNames.Administrator);

        using var client = CreateClient();
        await LoginAsync(client, email, "Valid1!Pass");

        var response = await client.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"top-nav\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"primary-nav\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"management-tabs\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"management-top\"", html, StringComparison.Ordinal);
        Assert.Contains(email, html, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Administrator_CanCreateSystem_AndSeeItInListAndManagementPage()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story21.admin.create@example.com", "Valid1!Pass", RoleNames.Administrator);

        using var client = CreateClient();
        await LoginAsync(client, "story21.admin.create@example.com", "Valid1!Pass");

        var createdSystemId = await CreateSystemAsync(client, "支付中台", "处理支付与清结算", "负责卡支付、退款与对账");

        var listResponse = await client.GetAsync("/api/systems");
        var systems = await ReadJsonArrayAsync(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains(systems, system => system.GetProperty("id").GetGuid() == createdSystemId);
        Assert.Contains(systems, system => system.GetProperty("name").GetString() == "支付中台");

        var pageResponse = await client.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("支付中台", html, StringComparison.Ordinal);
        Assert.Contains("处理支付与清结算", html, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Administrator_CanUpdateAndDeleteEmptySystem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story21.admin.edit@example.com", "Valid1!Pass", RoleNames.Administrator);

        using var client = CreateClient();
        await LoginAsync(client, "story21.admin.edit@example.com", "Valid1!Pass");

        var systemId = await CreateSystemAsync(client, "风控平台", "旧描述", "旧上下文");

        var updateResponse = await client.PutAsJsonAsync($"/api/systems/{systemId}", new
        {
            name = "智能风控平台",
            description = "统一风控规则与评分服务",
            context = "补充上下文信息",
        });

        var updated = await ReadJsonAsync(updateResponse);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("智能风控平台", updated.RootElement.GetProperty("name").GetString());
        Assert.Equal("统一风控规则与评分服务", updated.RootElement.GetProperty("description").GetString());

        var pageResponse = await client.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Contains("智能风控平台", html, StringComparison.Ordinal);
        Assert.Contains("统一风控规则与评分服务", html, StringComparison.Ordinal);

        var deleteResponse = await client.DeleteAsync($"/api/systems/{systemId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/systems");
        var systems = await ReadJsonArrayAsync(listResponse);

        Assert.DoesNotContain(systems, system => system.GetProperty("id").GetGuid() == systemId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DuplicateSystemName_ReturnsChineseValidationProblem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story21.admin.duplicate@example.com", "Valid1!Pass", RoleNames.Administrator);

        using var client = CreateClient();
        await LoginAsync(client, "story21.admin.duplicate@example.com", "Valid1!Pass");

        var existingSystemId = await CreateSystemAsync(client, "结算中心", "结算服务", "上下文");

        var createDuplicateResponse = await client.PostAsJsonAsync("/api/systems", new
        {
            name = "  结算中心  ",
            description = "重复名称",
            context = "重复上下文",
        });

        var createProblem = await createDuplicateResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, createDuplicateResponse.StatusCode);
        Assert.NotNull(createProblem);
        Assert.True(createProblem.Errors.TryGetValue("Name", out string[]? createErrors));
        Assert.Contains(createErrors, error => error.Contains("系统名称已存在", StringComparison.Ordinal));

        var anotherSystemId = await CreateSystemAsync(client, "消息平台", "消息调度", "消息上下文");

        var updateDuplicateResponse = await client.PutAsJsonAsync($"/api/systems/{anotherSystemId}", new
        {
            name = "结算中心",
            description = "尝试重名",
            context = "尝试重名上下文",
        });

        var updateProblem = await updateDuplicateResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, updateDuplicateResponse.StatusCode);
        Assert.NotNull(updateProblem);
        Assert.True(updateProblem.Errors.TryGetValue("Name", out string[]? updateErrors));
        Assert.Contains(updateErrors, error => error.Contains("系统名称已存在", StringComparison.Ordinal));
        Assert.NotEqual(existingSystemId, anotherSystemId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task DeleteSystem_WithAssignedOwnerOrRepository_IsBlockedWithChineseProblem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story21.admin.delete-guard@example.com";
        const string ownerEmail = "story21.owner.delete-guard@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.SystemOwner);

        using var client = CreateClient();
        await LoginAsync(client, adminEmail, "Valid1!Pass");

        var ownerAssignedSystemId = await CreateSystemAsync(client, "渠道总线", "渠道整合", "上下文");
        await AssignOwnerAsync(client, ownerAssignedSystemId, ownerUserId);

        var ownerDeleteResponse = await client.DeleteAsync($"/api/systems/{ownerAssignedSystemId}");
        var ownerProblem = await ownerDeleteResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, ownerDeleteResponse.StatusCode);
        Assert.NotNull(ownerProblem);
        Assert.Contains("请先移除依赖", ownerProblem.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task SystemOwner_OnlySeesAssignedSystems_AndCannotCreateOrModifySystems()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story21.admin.owner-scope@example.com";
        const string ownerEmail = "story21.owner.scope@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.SystemOwner);

        using var adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        var assignedSystemId = await CreateSystemAsync(adminClient, "授信平台", "授信系统", "授信上下文");
        var hiddenSystemId = await CreateSystemAsync(adminClient, "运营平台", "运营系统", "运营上下文");
        await AssignOwnerAsync(adminClient, assignedSystemId, ownerUserId);

        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        var listResponse = await ownerClient.GetAsync("/api/systems");
        var systems = await ReadJsonArrayAsync(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Single(systems);
        Assert.Equal(assignedSystemId, systems[0].GetProperty("id").GetGuid());
        Assert.DoesNotContain(systems, system => system.GetProperty("id").GetGuid() == hiddenSystemId);

        var pageResponse = await ownerClient.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("授信平台", html, StringComparison.Ordinal);
        Assert.DoesNotContain("运营平台", html, StringComparison.Ordinal);
        Assert.DoesNotContain("+ 新建系统", html, StringComparison.Ordinal);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/systems", new
        {
            name = "越权系统",
            description = "不应允许创建",
            context = "不应允许上下文",
        });

        var createProblem = await createResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.NotNull(createProblem);
        Assert.Contains("只有管理员可以修改系统", createProblem.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task User_MainLayout_DoesNotShowManagementNavigation()
    {
        await EnsureApplicationStartedAsync();
        await CreateUserWithRolesAsync("story21.user.nav@example.com", "Valid1!Pass", RoleNames.User);

        using var client = CreateClient();
        await LoginAsync(client, "story21.user.nav@example.com", "Valid1!Pass");

        var response = await client.GetAsync("/");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("管理后台", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/management\"", html, StringComparison.Ordinal);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private async Task EnsureApplicationStartedAsync()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateUserWithRolesAsync(string email, string password, params string[] roles)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));

        foreach (string role in roles.Distinct(StringComparer.Ordinal))
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        }

        return user.Id;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var fields = await GetFormFieldsAsync(client, "/Account/Login");
        fields["Input.Email"] = email;
        fields["Input.Password"] = password;
        fields["Input.RememberMe"] = "false";

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", GetLocationPath(response));
    }

    private static async Task<Guid> CreateSystemAsync(HttpClient client, string name, string description, string context)
    {
        var response = await client.PostAsJsonAsync("/api/systems", new
        {
            name,
            description,
            context,
        });

        var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssignOwnerAsync(HttpClient client, Guid systemId, string userId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/systems/{systemId}/owners",
            new AssignSystemOwnerRequest { UserId = userId });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task ResetDomainStateAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var domainDb = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();

        domainDb.SystemOwnerAssignments.RemoveRange(domainDb.SystemOwnerAssignments);
        domainDb.Repositories.RemoveRange(domainDb.Repositories);
        domainDb.Systems.RemoveRange(domainDb.Systems);

        await domainDb.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, string>> GetFormFieldsAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        string html = await response.Content.ReadAsStringAsync();
        return ExtractFormFields(html);
    }

    private static Dictionary<string, string> ExtractFormFields(string html)
    {
        var formMatch = Regex.Match(
            html,
            "<form[^>]*method=\"post\"[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!formMatch.Success)
        {
            throw new InvalidOperationException("No POST form was found in the supplied HTML.");
        }

        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        var inputMatches = Regex.Matches(
            formMatch.Groups["content"].Value,
            "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match inputMatch in inputMatches)
        {
            fields[WebUtility.HtmlDecode(inputMatch.Groups["name"].Value)] = WebUtility.HtmlDecode(inputMatch.Groups["value"].Value);
        }

        return fields;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    private static async Task<JsonElement[]> ReadJsonArrayAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonAsync(response);
        return document.RootElement.EnumerateArray().Select(element => element.Clone()).ToArray();
    }

    private static string GetLocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location
                       ?? throw new InvalidOperationException("Expected a redirect location header.");

        return location.IsAbsoluteUri
            ? location.PathAndQuery
            : location.OriginalString;
    }
}

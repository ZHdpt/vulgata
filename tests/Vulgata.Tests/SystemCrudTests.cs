using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class SystemCrudAdminTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public SystemCrudAdminTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Administrator_SystemManagementPage_ShowsChineseTreeAndEmptyState()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story21.admin.page@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story21.admin.page@example.com", "Valid1!Pass");

        HttpResponseMessage response = await client.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("系统树", html, StringComparison.Ordinal);
        Assert.Contains("+ 新建系统", html, StringComparison.Ordinal);
        Assert.Contains("尚未添加任何系统。创建你的第一个系统开始扫描。", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrator_CanCreateSystem_AndSeeItInListAndManagementPage()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story21.admin.create@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story21.admin.create@example.com", "Valid1!Pass");

        Guid createdSystemId = await CreateSystemAsync(client, "支付中台", "处理支付与清结算", "负责卡支付、退款与对账");

        HttpResponseMessage listResponse = await client.GetAsync("/api/systems");
        JsonElement[] systems = await ReadJsonArrayAsync(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains(systems, system => system.GetProperty("id").GetGuid() == createdSystemId);
        Assert.Contains(systems, system => system.GetProperty("name").GetString() == "支付中台");

        HttpResponseMessage pageResponse = await client.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("支付中台", html, StringComparison.Ordinal);
        Assert.Contains("处理支付与清结算", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrator_CanUpdateAndDeleteEmptySystem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story21.admin.edit@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story21.admin.edit@example.com", "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "风控平台", "旧描述", "旧上下文");

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/systems/{systemId}", new
        {
            name = "智能风控平台",
            description = "统一风控规则与评分服务",
            context = "补充上下文信息",
        });

        JsonDocument updated = await ReadJsonAsync(updateResponse);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("智能风控平台", updated.RootElement.GetProperty("name").GetString());
        Assert.Equal("统一风控规则与评分服务", updated.RootElement.GetProperty("description").GetString());

        HttpResponseMessage pageResponse = await client.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Contains("智能风控平台", html, StringComparison.Ordinal);
        Assert.Contains("统一风控规则与评分服务", html, StringComparison.Ordinal);

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/systems/{systemId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage listResponse = await client.GetAsync("/api/systems");
        JsonElement[] systems = await ReadJsonArrayAsync(listResponse);

        Assert.DoesNotContain(systems, system => system.GetProperty("id").GetGuid() == systemId);
    }

    [Fact]
    public async Task DuplicateSystemName_ReturnsChineseValidationProblem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story21.admin.duplicate@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story21.admin.duplicate@example.com", "Valid1!Pass");

        Guid existingSystemId = await CreateSystemAsync(client, "结算中心", "结算服务", "上下文");

        HttpResponseMessage createDuplicateResponse = await client.PostAsJsonAsync("/api/systems", new
        {
            name = "  结算中心  ",
            description = "重复名称",
            context = "重复上下文",
        });

        ValidationProblemDetails? createProblem = await createDuplicateResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, createDuplicateResponse.StatusCode);
        Assert.NotNull(createProblem);
        Assert.True(createProblem.Errors.TryGetValue("Name", out string[]? createErrors));
        Assert.Contains(createErrors, error => error.Contains("系统名称已存在", StringComparison.Ordinal));

        Guid anotherSystemId = await CreateSystemAsync(client, "消息平台", "消息调度", "消息上下文");

        HttpResponseMessage updateDuplicateResponse = await client.PutAsJsonAsync($"/api/systems/{anotherSystemId}", new
        {
            name = "结算中心",
            description = "尝试重名",
            context = "尝试重名上下文",
        });

        ValidationProblemDetails? updateProblem = await updateDuplicateResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, updateDuplicateResponse.StatusCode);
        Assert.NotNull(updateProblem);
        Assert.True(updateProblem.Errors.TryGetValue("Name", out string[]? updateErrors));
        Assert.Contains(updateErrors, error => error.Contains("系统名称已存在", StringComparison.Ordinal));
        Assert.NotEqual(existingSystemId, anotherSystemId);
    }

    [Fact]
    public async Task DeleteSystem_WithAssignedOwnerOrRepository_IsBlockedWithChineseProblem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story21.admin.delete-guard@example.com";
        const string ownerEmail = "story21.owner.delete-guard@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.SystemOwner);

        using HttpClient client = CreateClient();
        await LoginAsync(client, adminEmail, "Valid1!Pass");

        Guid ownerAssignedSystemId = await CreateSystemAsync(client, "渠道总线", "渠道整合", "上下文");
        await InsertSystemOwnerAssignmentAsync(ownerAssignedSystemId, ownerUserId);

        HttpResponseMessage ownerDeleteResponse = await client.DeleteAsync($"/api/systems/{ownerAssignedSystemId}");
        ProblemDetails? ownerProblem = await ownerDeleteResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, ownerDeleteResponse.StatusCode);
        Assert.NotNull(ownerProblem);
        Assert.Contains("请先移除依赖", ownerProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        Guid repositoryBoundSystemId = await CreateSystemAsync(client, "数据平台", "数据服务", "上下文");
        await InsertRepositoryAsync(repositoryBoundSystemId, "客户画像仓库", "https://example.com/profile.git");

        HttpResponseMessage repositoryDeleteResponse = await client.DeleteAsync($"/api/systems/{repositoryBoundSystemId}");
        ProblemDetails? repositoryProblem = await repositoryDeleteResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, repositoryDeleteResponse.StatusCode);
        Assert.NotNull(repositoryProblem);
        Assert.Contains("请先移除依赖", repositoryProblem.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemOwner_OnlySeesAssignedSystems_AndCannotCreateOrModifySystems()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story21.admin.owner-scope@example.com";
        const string ownerEmail = "story21.owner.scope@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.SystemOwner);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid assignedSystemId = await CreateSystemAsync(adminClient, "授信平台", "授信系统", "授信上下文");
        Guid hiddenSystemId = await CreateSystemAsync(adminClient, "运营平台", "运营系统", "运营上下文");
        await InsertSystemOwnerAssignmentAsync(assignedSystemId, ownerUserId);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage listResponse = await ownerClient.GetAsync("/api/systems");
        JsonElement[] systems = await ReadJsonArrayAsync(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Single(systems);
        Assert.Equal(assignedSystemId, systems[0].GetProperty("id").GetGuid());
        Assert.DoesNotContain(systems, system => system.GetProperty("id").GetGuid() == hiddenSystemId);

        HttpResponseMessage pageResponse = await ownerClient.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("授信平台", html, StringComparison.Ordinal);
        Assert.DoesNotContain("运营平台", html, StringComparison.Ordinal);
        Assert.DoesNotContain("+ 新建系统", html, StringComparison.Ordinal);

        HttpResponseMessage createResponse = await ownerClient.PostAsJsonAsync("/api/systems", new
        {
            name = "越权系统",
            description = "不应允许创建",
            context = "不应允许上下文",
        });

        ProblemDetails? createProblem = await createResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.NotNull(createProblem);
        Assert.Contains("只有管理员可以修改系统", createProblem.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task User_MainLayout_DoesNotShowManagementNavigation()
    {
        await EnsureApplicationStartedAsync();
        await CreateUserWithRolesAsync("story21.user.nav@example.com", "Valid1!Pass", RoleNames.User);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story21.user.nav@example.com", "Valid1!Pass");

        HttpResponseMessage response = await client.GetAsync("/");
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
        using HttpClient client = CreateClient();
        HttpResponseMessage response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateUserWithRolesAsync(string email, string password, params string[] roles)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        IdentityResult createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));

        foreach (string role in roles.Distinct(StringComparer.Ordinal))
        {
            IdentityResult roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        }

        return user.Id;
    }

    private async Task LoginAsync(HttpClient client, string email, string password)
    {
        Dictionary<string, string> fields = await GetFormFieldsAsync(client, "/Account/Login");
        fields["Input.Email"] = email;
        fields["Input.Password"] = password;
        fields["Input.RememberMe"] = "false";

        HttpResponseMessage response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", GetLocationPath(response));
    }

    private async Task<Guid> CreateSystemAsync(HttpClient client, string name, string description, string context)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/systems", new
        {
            name,
            description,
            context,
        });

        JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task ResetDomainStateAsync()
    {
        await using SqliteConnection connection = new(_factory.ConnectionString);
        await connection.OpenAsync();

        await ExecuteNonQueryAsync(connection, "DELETE FROM SystemOwnerAssignments;");
        await ExecuteNonQueryAsync(connection, "DELETE FROM Repositories;");
        await ExecuteNonQueryAsync(connection, "DELETE FROM Systems;");
    }

    private async Task InsertSystemOwnerAssignmentAsync(Guid systemId, string userId)
    {
        await using SqliteConnection connection = new(_factory.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SystemOwnerAssignments (Id, SystemId, UserId, AssignedAt)
            VALUES ($id, $systemId, $userId, $assignedAt);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$systemId", systemId.ToString());
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$assignedAt", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertRepositoryAsync(Guid systemId, string name, string gitUrl)
    {
        await using SqliteConnection connection = new(_factory.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Repositories (Id, SystemId, Name, GitUrl, CreatedAt, UpdatedAt)
            VALUES ($id, $systemId, $name, $gitUrl, $createdAt, $updatedAt);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$systemId", systemId.ToString());
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$gitUrl", gitUrl);
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Dictionary<string, string>> GetFormFieldsAsync(HttpClient client, string path)
    {
        HttpResponseMessage response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        string html = await response.Content.ReadAsStringAsync();
        return ExtractFormFields(html);
    }

    private static Dictionary<string, string> ExtractFormFields(string html)
    {
        Match formMatch = Regex.Match(
            html,
            "<form[^>]*method=\"post\"[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!formMatch.Success)
        {
            throw new InvalidOperationException("No POST form was found in the supplied HTML.");
        }

        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        MatchCollection inputMatches = Regex.Matches(
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
        using JsonDocument document = await ReadJsonAsync(response);
        return document.RootElement.EnumerateArray().Select(element => element.Clone()).ToArray();
    }

    private static string GetLocationPath(HttpResponseMessage response)
    {
        Uri location = response.Headers.Location
            ?? throw new InvalidOperationException("Expected a redirect location header.");

        return location.IsAbsoluteUri
            ? location.PathAndQuery
            : location.OriginalString;
    }
}
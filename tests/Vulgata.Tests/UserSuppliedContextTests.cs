using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Shared.Systems;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class UserSuppliedContextTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public UserSuppliedContextTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Administrator_CanSaveAndReadGlobalContext_AndSettingsPageShowsSection()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story25.admin.global@example.com";
        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        HttpResponseMessage saveResponse = await adminClient.PutAsJsonAsync("/api/settings/global-context", new
        {
            context = "全局业务术语：订单、履约、结算。",
        });

        ContextStateResponse saved = await ReadRequiredAsync<ContextStateResponse>(saveResponse);

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        Assert.Equal("全局业务术语：订单、履约、结算。", saved.CurrentContext);
        Assert.Null(saved.PendingContext);
        Assert.False(saved.Queued);

        HttpResponseMessage readResponse = await adminClient.GetAsync("/api/settings/global-context");
        ContextStateResponse readBack = await ReadRequiredAsync<ContextStateResponse>(readResponse);

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal("全局业务术语：订单、履约、结算。", readBack.CurrentContext);
        Assert.Null(readBack.PendingContext);

        HttpResponseMessage pageResponse = await adminClient.GetAsync("/management/settings");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("全局上下文（适用于所有系统）", html, StringComparison.Ordinal);
        Assert.Contains("全局业务术语：订单、履约、结算。", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssignedSystemOwner_CanSaveSystemAndRepositoryContexts_AndDashboardShowsSections()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story25.admin.owner@example.com";
        const string ownerEmail = "story25.owner@example.com";
        const string outsiderEmail = "story25.outsider@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);
        await CreateUserWithRolesAsync(outsiderEmail, "Valid1!Pass", RoleNames.SystemOwner);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(adminClient, "供应链平台", "供应链系统", "初始系统上下文");
        await AssignOwnerAsync(adminClient, systemId, ownerUserId);
        Guid repositoryId = await CreateRepositoryAsync(adminClient, systemId, "履约仓库", "初始仓库上下文");

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage saveSystemResponse = await ownerClient.PutAsJsonAsync($"/api/systems/{systemId}/context", new
        {
            context = "系统上下文：履约链路优先。",
        });
        ContextStateResponse savedSystem = await ReadRequiredAsync<ContextStateResponse>(saveSystemResponse);

        Assert.Equal(HttpStatusCode.OK, saveSystemResponse.StatusCode);
        Assert.Equal("系统上下文：履约链路优先。", savedSystem.CurrentContext);
        Assert.Null(savedSystem.PendingContext);

        HttpResponseMessage saveRepositoryResponse = await ownerClient.PutAsJsonAsync($"/api/repositories/{repositoryId}/context", new
        {
            context = "仓库上下文：处理履约聚合根与状态机。",
        });
        ContextStateResponse savedRepository = await ReadRequiredAsync<ContextStateResponse>(saveRepositoryResponse);

        Assert.Equal(HttpStatusCode.OK, saveRepositoryResponse.StatusCode);
        Assert.Equal("仓库上下文：处理履约聚合根与状态机。", savedRepository.CurrentContext);
        Assert.Null(savedRepository.PendingContext);

        HttpResponseMessage readSystemResponse = await ownerClient.GetAsync($"/api/systems/{systemId}/context");
        ContextStateResponse readSystem = await ReadRequiredAsync<ContextStateResponse>(readSystemResponse);
        Assert.Equal(HttpStatusCode.OK, readSystemResponse.StatusCode);
        Assert.Equal("系统上下文：履约链路优先。", readSystem.CurrentContext);

        HttpResponseMessage readRepositoryResponse = await ownerClient.GetAsync($"/api/repositories/{repositoryId}/context");
        ContextStateResponse readRepository = await ReadRequiredAsync<ContextStateResponse>(readRepositoryResponse);
        Assert.Equal(HttpStatusCode.OK, readRepositoryResponse.StatusCode);
        Assert.Equal("仓库上下文：处理履约聚合根与状态机。", readRepository.CurrentContext);

        using HttpClient outsiderClient = CreateClient();
        await LoginAsync(outsiderClient, outsiderEmail, "Valid1!Pass");

        HttpResponseMessage forbiddenSystemResponse = await outsiderClient.PutAsJsonAsync($"/api/systems/{systemId}/context", new
        {
            context = "越权系统上下文",
        });
        ProblemDetails forbiddenSystemProblem = await ReadRequiredAsync<ProblemDetails>(forbiddenSystemResponse);

        Assert.Equal(HttpStatusCode.NotFound, forbiddenSystemResponse.StatusCode);
        Assert.Contains("系统不存在", forbiddenSystemProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage forbiddenRepositoryResponse = await outsiderClient.PutAsJsonAsync($"/api/repositories/{repositoryId}/context", new
        {
            context = "越权仓库上下文",
        });
        ProblemDetails forbiddenRepositoryProblem = await ReadRequiredAsync<ProblemDetails>(forbiddenRepositoryResponse);

        Assert.Equal(HttpStatusCode.NotFound, forbiddenRepositoryResponse.StatusCode);
        Assert.Contains("仓库不存在", forbiddenRepositoryProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage pageResponse = await ownerClient.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("系统管理", html, StringComparison.Ordinal);
        Assert.Contains("供应链平台", html, StringComparison.Ordinal);
        Assert.Contains("履约仓库", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManagementUser_CanSaveStandaloneRepositoryContext()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string ownerEmail = "story25.owner.standalone@example.com";
        await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.SystemOwner);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        Guid repositoryId = await CreateStandaloneRepositoryAsync(ownerClient, "共享知识库", "初始独立仓库上下文");

        HttpResponseMessage saveResponse = await ownerClient.PutAsJsonAsync($"/api/repositories/{repositoryId}/context", new
        {
            context = "独立仓库上下文：所有管理用户共享。",
        });

        ContextStateResponse saved = await ReadRequiredAsync<ContextStateResponse>(saveResponse);

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        Assert.Equal("独立仓库上下文：所有管理用户共享。", saved.CurrentContext);
        Assert.Null(saved.PendingContext);

        HttpResponseMessage readResponse = await ownerClient.GetAsync($"/api/repositories/{repositoryId}/context");
        ContextStateResponse readBack = await ReadRequiredAsync<ContextStateResponse>(readResponse);

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal("独立仓库上下文：所有管理用户共享。", readBack.CurrentContext);
    }

    [Fact]
    public async Task User_CannotWriteContextEndpoints()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story25.admin.user-block@example.com";
        const string userEmail = "story25.user@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        await CreateUserWithRolesAsync(userEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(adminClient, "客服平台", "客服系统", "客服上下文");
        Guid repositoryId = await CreateRepositoryAsync(adminClient, systemId, "客服仓库", "客服仓库上下文");

        using HttpClient userClient = CreateClient();
        await LoginAsync(userClient, userEmail, "Valid1!Pass");

        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PutAsJsonAsync("/api/settings/global-context", new { context = "用户不应写入全局" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PutAsJsonAsync($"/api/systems/{systemId}/context", new { context = "用户不应写入系统" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.PutAsJsonAsync($"/api/repositories/{repositoryId}/context", new { context = "用户不应写入仓库" })).StatusCode);
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

    private async Task ResetDomainStateAsync()
    {
        await using SqliteConnection connection = new(_factory.ConnectionString);
        await connection.OpenAsync();

        await EnsureDomainTablesAsync(connection);

        await ExecuteNonQueryAsync(connection, "DELETE FROM PendingContextChanges;");
        await ExecuteNonQueryAsync(connection, "DELETE FROM GlobalContexts;");
        await ExecuteNonQueryAsync(connection, "DELETE FROM SystemOwnerAssignments;");
        await ExecuteNonQueryAsync(connection, "DELETE FROM Repositories;");
        await ExecuteNonQueryAsync(connection, "DELETE FROM Systems;");
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureDomainTablesAsync(SqliteConnection connection)
    {
        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS Systems (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                NormalizedName TEXT NOT NULL,
                Description TEXT NULL,
                Context TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Systems_NormalizedName
            ON Systems (NormalizedName);
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS Repositories (
                Id TEXT NOT NULL PRIMARY KEY,
                SystemId TEXT NULL,
                Name TEXT NOT NULL,
                GitUrl TEXT NOT NULL,
                Description TEXT NULL,
                Context TEXT NULL,
                NormalizedName TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (SystemId) REFERENCES Systems(Id) ON DELETE RESTRICT
            );
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Repositories_SystemId_NormalizedName
            ON Repositories (SystemId, NormalizedName);
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Repositories_Standalone_NormalizedName
            ON Repositories (NormalizedName)
            WHERE SystemId IS NULL;
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS SystemOwnerAssignments (
                Id TEXT NOT NULL PRIMARY KEY,
                SystemId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                AssignedAt TEXT NOT NULL,
                FOREIGN KEY (SystemId) REFERENCES Systems(Id) ON DELETE RESTRICT
            );
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_SystemOwnerAssignments_SystemId_UserId
            ON SystemOwnerAssignments (SystemId, UserId);
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS GlobalContexts (
                Id TEXT NOT NULL PRIMARY KEY,
                Context TEXT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS PendingContextChanges (
                Id TEXT NOT NULL PRIMARY KEY,
                ScopeType INTEGER NOT NULL,
                ScopeKey TEXT NOT NULL,
                Context TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_PendingContextChanges_ScopeType_ScopeKey
            ON PendingContextChanges (ScopeType, ScopeKey);
            """);
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

        SystemListItem created = await ReadRequiredAsync<SystemListItem>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return created.Id;
    }

    private async Task AssignOwnerAsync(HttpClient client, Guid systemId, string userId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/systems/{systemId}/owners",
            new AssignSystemOwnerRequest { UserId = userId });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<Guid> CreateRepositoryAsync(HttpClient client, Guid systemId, string name, string context)
    {
        string bareRepositoryPath = await CreateBareRepositoryAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/systems/{systemId}/repositories", new
        {
            name,
            gitUrl = bareRepositoryPath,
            description = name + " 描述",
            context,
        });

        RepositoryListItem created = await ReadRequiredAsync<RepositoryListItem>(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return created.Id;
    }

    private async Task<Guid> CreateStandaloneRepositoryAsync(HttpClient client, string name, string context)
    {
        string bareRepositoryPath = await CreateBareRepositoryAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/repositories/standalone", new
        {
            name,
            gitUrl = bareRepositoryPath,
            description = name + " 描述",
            context,
        });

        RepositoryListItem created = await ReadRequiredAsync<RepositoryListItem>(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return created.Id;
    }

    private static async Task<string> CreateBareRepositoryAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "vulgata-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        ProcessStartInfo startInfo = new("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("init");
        startInfo.ArgumentList.Add("--bare");
        startInfo.ArgumentList.Add(directory);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git to create a bare repository.");

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, $"Expected bare repository creation to succeed. stdout: {stdout} stderr: {stderr}");
        return directory;
    }

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response)
    {
        T? value = await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(value);
        return value;
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

    private static string GetLocationPath(HttpResponseMessage response)
    {
        Uri location = response.Headers.Location
            ?? throw new InvalidOperationException("Expected a redirect location header.");

        return location.IsAbsoluteUri
            ? location.PathAndQuery
            : location.OriginalString;
    }

    private sealed class SystemListItem
    {
        public Guid Id { get; set; }
    }

    private sealed class RepositoryListItem
    {
        public Guid Id { get; set; }
    }

    private sealed class ContextStateResponse
    {
        public string? CurrentContext { get; set; }
        public string? PendingContext { get; set; }
        public bool Queued { get; set; }
        public string? StatusMessage { get; set; }
    }
}

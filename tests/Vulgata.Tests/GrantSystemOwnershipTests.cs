using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Shared;
using Vulgata.Shared.Systems;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class GrantSystemOwnershipTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public GrantSystemOwnershipTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Administrator_CanAssignSystemOwner_AndOwnerCanSeeAssignedSystem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story22.admin.assign@example.com";
        const string ownerEmail = "story22.owner.assign@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(adminClient, "统一身份平台", "统一认证授权", "身份上下文");

        HttpResponseMessage assignResponse = await adminClient.PostAsJsonAsync(
            $"/api/systems/{systemId}/owners",
            new AssignSystemOwnerRequest { UserId = ownerUserId });

        Assert.Equal(HttpStatusCode.NoContent, assignResponse.StatusCode);

        IList<string> roles = await GetUserRolesAsync(ownerEmail);
        Assert.Contains(RoleNames.SystemOwner, roles);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage listResponse = await ownerClient.GetAsync("/api/systems");
        List<SystemListItem> systems = await ReadRequiredAsync<List<SystemListItem>>(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains(systems, system => system.Id == systemId && system.Name == "统一身份平台");
    }

    [Fact]
    public async Task AssignSystemOwner_DuplicateAssignment_ReturnsChineseProblem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story22.admin.duplicate@example.com";
        const string ownerEmail = "story22.owner.duplicate@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient client = CreateClient();
        await LoginAsync(client, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "财务核算平台", "财务处理", "财务上下文");

        HttpResponseMessage firstAssign = await client.PostAsJsonAsync(
            $"/api/systems/{systemId}/owners",
            new AssignSystemOwnerRequest { UserId = ownerUserId });
        Assert.Equal(HttpStatusCode.NoContent, firstAssign.StatusCode);

        HttpResponseMessage duplicateAssign = await client.PostAsJsonAsync(
            $"/api/systems/{systemId}/owners",
            new AssignSystemOwnerRequest { UserId = ownerUserId });
        ProblemDetails duplicateProblem = await ReadRequiredAsync<ProblemDetails>(duplicateAssign);

        Assert.Equal(HttpStatusCode.Conflict, duplicateAssign.StatusCode);
        Assert.Contains("该用户已是该系统的所有者", duplicateProblem.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OwnerCandidates_ExcludeAdministrators_AndSupportSearch()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story22.admin.candidates@example.com";
        const string administratorCandidateEmail = "story22.admin.candidate@example.com";
        const string userCandidateEmail = "story22.user.candidate@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string adminCandidateUserId = await CreateUserWithRolesAsync(administratorCandidateEmail, "Valid1!Pass", RoleNames.Administrator);
        string userCandidateUserId = await CreateUserWithRolesAsync(userCandidateEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient client = CreateClient();
        await LoginAsync(client, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "风控策略平台", "风险控制", "风控上下文");

        HttpResponseMessage candidatesResponse = await client.GetAsync($"/api/systems/{systemId}/owner-candidates");

        Assert.Equal(HttpStatusCode.OK, candidatesResponse.StatusCode);

        List<SystemOwnerCandidateDto> candidates = await ReadRequiredAsync<List<SystemOwnerCandidateDto>>(candidatesResponse);
        Assert.DoesNotContain(candidates, candidate => candidate.UserId == adminCandidateUserId);
        Assert.Contains(candidates, candidate => candidate.UserId == userCandidateUserId);

        string keyword = Uri.EscapeDataString("user.candidate");
        HttpResponseMessage keywordResponse = await client.GetAsync($"/api/systems/{systemId}/owner-candidates?keyword={keyword}");

        Assert.Equal(HttpStatusCode.OK, keywordResponse.StatusCode);

        List<SystemOwnerCandidateDto> filtered = await ReadRequiredAsync<List<SystemOwnerCandidateDto>>(keywordResponse);
        Assert.All(filtered, candidate =>
            Assert.Contains("user.candidate", candidate.Email, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RemovingLastOwnerAssignment_RemovesManagementAccessAndKeepsUserRole()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story22.admin.remove@example.com";
        const string ownerEmail = "story22.owner.remove@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid systemA = await CreateSystemAsync(adminClient, "交易平台", "交易系统", "交易上下文");
        Guid systemB = await CreateSystemAsync(adminClient, "营销平台", "营销系统", "营销上下文");

        await AssignOwnerAsync(adminClient, systemA, ownerUserId);
        await AssignOwnerAsync(adminClient, systemB, ownerUserId);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage beforeRemovalResponse = await ownerClient.GetAsync("/api/systems");
        List<SystemListItem> beforeRemoval = await ReadRequiredAsync<List<SystemListItem>>(beforeRemovalResponse);
        Assert.Equal(2, beforeRemoval.Count);

        HttpResponseMessage firstRemoval = await adminClient.DeleteAsync($"/api/systems/{systemA}/owners/{ownerUserId}");
        Assert.Equal(HttpStatusCode.NoContent, firstRemoval.StatusCode);

        HttpResponseMessage afterFirstRemovalResponse = await ownerClient.GetAsync("/api/systems");
        List<SystemListItem> afterFirstRemoval = await ReadRequiredAsync<List<SystemListItem>>(afterFirstRemovalResponse);

        Assert.Single(afterFirstRemoval);
        Assert.Equal(systemB, afterFirstRemoval[0].Id);

        IList<string> afterFirstRoles = await GetUserRolesAsync(ownerEmail);
        Assert.Contains(RoleNames.SystemOwner, afterFirstRoles);

        HttpResponseMessage secondRemoval = await adminClient.DeleteAsync($"/api/systems/{systemB}/owners/{ownerUserId}");
        Assert.Equal(HttpStatusCode.NoContent, secondRemoval.StatusCode);

        IList<string> afterSecondRoles = await GetUserRolesAsync(ownerEmail);
        Assert.DoesNotContain(RoleNames.SystemOwner, afterSecondRoles);
        Assert.Contains(RoleNames.User, afterSecondRoles);

        HttpResponseMessage homeResponse = await ownerClient.GetAsync("/");
        string homeHtml = WebUtility.HtmlDecode(await homeResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        Assert.DoesNotContain("href=\"/management\"", homeHtml, StringComparison.Ordinal);

        HttpResponseMessage managementResponse = await ownerClient.GetAsync("/management");
        Assert.Equal(HttpStatusCode.Redirect, managementResponse.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(managementResponse), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonAdministrator_CannotManageSystemOwnersEndpoints()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story22.admin.forbidden@example.com";
        const string ownerEmail = "story22.owner.forbidden@example.com";
        const string targetEmail = "story22.target.forbidden@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.SystemOwner);
        string targetUserId = await CreateUserWithRolesAsync(targetEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(adminClient, "客服平台", "客服系统", "客服上下文");
        await AssignOwnerAsync(adminClient, systemId, ownerUserId);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage getOwnersResponse = await ownerClient.GetAsync($"/api/systems/{systemId}/owners");
        ProblemDetails getOwnersProblem = await ReadRequiredAsync<ProblemDetails>(getOwnersResponse);

        Assert.Equal(HttpStatusCode.Forbidden, getOwnersResponse.StatusCode);
        Assert.Contains("只有管理员可以管理系统所有者", getOwnersProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage assignResponse = await ownerClient.PostAsJsonAsync(
            $"/api/systems/{systemId}/owners",
            new AssignSystemOwnerRequest { UserId = targetUserId });
        ProblemDetails assignProblem = await ReadRequiredAsync<ProblemDetails>(assignResponse);

        Assert.Equal(HttpStatusCode.Forbidden, assignResponse.StatusCode);
        Assert.Contains("只有管理员可以管理系统所有者", assignProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage removeResponse = await ownerClient.DeleteAsync($"/api/systems/{systemId}/owners/{ownerUserId}");
        ProblemDetails removeProblem = await ReadRequiredAsync<ProblemDetails>(removeResponse);

        Assert.Equal(HttpStatusCode.Forbidden, removeResponse.StatusCode);
        Assert.Contains("只有管理员可以管理系统所有者", removeProblem.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssignedOwner_BlocksSystemDeletionDependencyConstraint()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story22.admin.delete-guard@example.com";
        const string ownerEmail = "story22.owner.delete-guard@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient client = CreateClient();
        await LoginAsync(client, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "账务总线", "账务核心", "账务上下文");
        await AssignOwnerAsync(client, systemId, ownerUserId);

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/systems/{systemId}");
        ProblemDetails deleteProblem = await ReadRequiredAsync<ProblemDetails>(deleteResponse);

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
        Assert.Contains("所有者分配", deleteProblem.Detail ?? string.Empty, StringComparison.Ordinal);
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

    private async Task<IList<string>> GetUserRolesAsync(string email)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        return await userManager.GetRolesAsync(user);
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

    private async Task ResetDomainStateAsync()
    {
        await using SqliteConnection connection = new(_factory.ConnectionString);
        await connection.OpenAsync();

        await EnsureDomainTablesAsync(connection);

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
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (SystemId) REFERENCES Systems(Id) ON DELETE RESTRICT
            );
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

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response)
    {
        T? value = await response.Content.ReadFromJsonAsync<T>();

        if (value is null)
        {
            throw new InvalidOperationException($"Expected JSON response body of type {typeof(T).Name}.");
        }

        return value;
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

        public string Name { get; set; } = string.Empty;
    }
}

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Infrastructure.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using Vulgata.Shared;
using Vulgata.Shared.Systems;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class RepositoryManagementTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public RepositoryManagementTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SystemOwner_CanCreateRepository_ForAssignedSystem_AndPageShowsRepositoryManagement()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story23.admin.create@example.com";
        const string ownerEmail = "story23.owner.create@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid assignedSystemId = await CreateSystemAsync(adminClient, "内容平台", "内容管理", "内容上下文");
        Guid hiddenSystemId = await CreateSystemAsync(adminClient, "营销平台", "营销管理", "营销上下文");
        await AssignOwnerAsync(adminClient, assignedSystemId, ownerUserId);

        string bareRepositoryPath = await CreateBareRepositoryAsync();

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage createResponse = await ownerClient.PostAsJsonAsync($"/api/systems/{assignedSystemId}/repositories", new
        {
            name = "主业务仓库",
            gitUrl = bareRepositoryPath,
            description = "主站代码仓库",
            context = "包含前后端主流程",
        });

        RepositoryDetailResponse created = await ReadRequiredAsync<RepositoryDetailResponse>(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("主业务仓库", created.Name);
        Assert.Equal("未扫描", created.ScanStatus);
        Assert.Equal(0, created.DocumentCount);

        HttpResponseMessage listResponse = await ownerClient.GetAsync($"/api/systems/{assignedSystemId}/repositories");
        List<RepositorySummaryResponse> repositories = await ReadRequiredAsync<List<RepositorySummaryResponse>>(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Single(repositories);
        Assert.Equal(created.Id, repositories[0].Id);
        Assert.Equal("主业务仓库", repositories[0].Name);

        HttpResponseMessage pageResponse = await ownerClient.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("内容平台", html, StringComparison.Ordinal);
        Assert.DoesNotContain("营销平台", html, StringComparison.Ordinal);
        Assert.Contains("主业务仓库", html, StringComparison.Ordinal);
        Assert.Contains("仓库名称", html, StringComparison.Ordinal);
        Assert.Contains("扫描状态", html, StringComparison.Ordinal);
        Assert.Contains("最近扫描时间", html, StringComparison.Ordinal);
        Assert.Contains("文档数量", html, StringComparison.Ordinal);
        Assert.Contains("查看", html, StringComparison.Ordinal);
        Assert.Contains("+ 新建仓库", html, StringComparison.Ordinal);
        Assert.DoesNotContain("+ 新建系统", html, StringComparison.Ordinal);

        _ = hiddenSystemId;
    }

    [Fact]
    public async Task ManagementUsers_CanCreateAndViewStandaloneRepositories_WithoutMixingIntoSystems()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story24.admin.standalone@example.com";
        const string ownerEmail = "story24.owner.standalone@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid assignedSystemId = await CreateSystemAsync(adminClient, "共享平台", "共享系统", "共享上下文");
        await AssignOwnerAsync(adminClient, assignedSystemId, ownerUserId);

        string adminBareRepositoryPath = await CreateBareRepositoryAsync();
        HttpResponseMessage adminCreateResponse = await adminClient.PostAsJsonAsync("/api/repositories/standalone", new
        {
            name = "基础共享库",
            gitUrl = adminBareRepositoryPath,
            description = "管理员创建的共享库",
            context = "供所有管理用户查看",
        });

        RepositoryDetailResponse adminCreated = await ReadRequiredAsync<RepositoryDetailResponse>(adminCreateResponse);

        Assert.Equal(HttpStatusCode.Created, adminCreateResponse.StatusCode);
        Assert.Null(adminCreated.SystemId);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        string ownerBareRepositoryPath = await CreateBareRepositoryAsync();
        HttpResponseMessage ownerCreateResponse = await ownerClient.PostAsJsonAsync("/api/repositories/standalone", new
        {
            name = "团队共享库",
            gitUrl = ownerBareRepositoryPath,
            description = "系统所有者创建的共享库",
            context = "仍不属于任何系统",
        });

        RepositoryDetailResponse ownerCreated = await ReadRequiredAsync<RepositoryDetailResponse>(ownerCreateResponse);

        Assert.Equal(HttpStatusCode.Created, ownerCreateResponse.StatusCode);
        Assert.Null(ownerCreated.SystemId);

        HttpResponseMessage standaloneListResponse = await ownerClient.GetAsync("/api/repositories/standalone");
        List<RepositorySummaryResponse> standaloneRepositories = await ReadRequiredAsync<List<RepositorySummaryResponse>>(standaloneListResponse);

        Assert.Equal(HttpStatusCode.OK, standaloneListResponse.StatusCode);
        Assert.Equal(2, standaloneRepositories.Count);
        Assert.Contains(standaloneRepositories, repository => repository.Name == "基础共享库");
        Assert.Contains(standaloneRepositories, repository => repository.Name == "团队共享库");

        await using (AsyncServiceScope scope = _factory.Services.CreateAsyncScope())
        {
            VulgataDbContext dbContext = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            RepositoryEntity? persistedRepository = await dbContext.Repositories
                .AsNoTracking()
                .SingleOrDefaultAsync(repository => repository.Id == ownerCreated.Id);

            Assert.NotNull(persistedRepository);
            Assert.Null(persistedRepository!.SystemId);
        }

        HttpResponseMessage systemListResponse = await ownerClient.GetAsync($"/api/systems/{assignedSystemId}/repositories");
        List<RepositorySummaryResponse> systemRepositories = await ReadRequiredAsync<List<RepositorySummaryResponse>>(systemListResponse);

        Assert.Equal(HttpStatusCode.OK, systemListResponse.StatusCode);
        Assert.Empty(systemRepositories);

        HttpResponseMessage pageResponse = await ownerClient.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("独立仓库", html, StringComparison.Ordinal);
        Assert.Contains("+ 新建独立仓库", html, StringComparison.Ordinal);
        Assert.Contains("基础共享库", html, StringComparison.Ordinal);
        Assert.Contains("团队共享库", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateStandaloneRepository_WithDuplicateName_ReturnsChineseValidationError()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story24.admin.duplicate@example.com";
        const string ownerEmail = "story24.owner.duplicate@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.SystemOwner);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        string firstBareRepositoryPath = await CreateBareRepositoryAsync();
        HttpResponseMessage firstCreateResponse = await adminClient.PostAsJsonAsync("/api/repositories/standalone", new
        {
            name = "共享组件库",
            gitUrl = firstBareRepositoryPath,
            description = "首次创建",
            context = "用于重名校验",
        });

        RepositoryDetailResponse firstCreated = await ReadRequiredAsync<RepositoryDetailResponse>(firstCreateResponse);
        Assert.Equal(HttpStatusCode.Created, firstCreateResponse.StatusCode);
        Assert.Null(firstCreated.SystemId);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        string secondBareRepositoryPath = await CreateBareRepositoryAsync();
        HttpResponseMessage duplicateCreateResponse = await ownerClient.PostAsJsonAsync("/api/repositories/standalone", new
        {
            name = "共享组件库",
            gitUrl = secondBareRepositoryPath,
            description = "重复名称",
            context = "应拒绝",
        });

        ValidationProblemDetails validationProblem = await ReadRequiredAsync<ValidationProblemDetails>(duplicateCreateResponse);

        Assert.Equal(HttpStatusCode.BadRequest, duplicateCreateResponse.StatusCode);
        Assert.True(validationProblem.Errors.TryGetValue("Name", out string[]? nameErrors));
        Assert.NotNull(nameErrors);
        Assert.Contains(nameErrors!, message => message.Contains("独立仓库名称已存在", StringComparison.Ordinal));

        HttpResponseMessage standaloneListResponse = await ownerClient.GetAsync("/api/repositories/standalone");
        List<RepositorySummaryResponse> standaloneRepositories = await ReadRequiredAsync<List<RepositorySummaryResponse>>(standaloneListResponse);

        Assert.Single(standaloneRepositories);
        Assert.Equal("共享组件库", standaloneRepositories[0].Name);
    }

    [Fact]
    public async Task CreateStandaloneRepository_WithUnreachableGitUrl_ReturnsChineseProblem_AndDoesNotPersist()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string ownerEmail = "story24.owner.unreachable@example.com";
        await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.SystemOwner);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        string missingRepositoryPath = Path.Combine(Path.GetTempPath(), "vulgata-tests", Guid.NewGuid().ToString("N"), "standalone-missing.git");

        HttpResponseMessage createResponse = await ownerClient.PostAsJsonAsync("/api/repositories/standalone", new
        {
            name = "不可达独立仓库",
            gitUrl = missingRepositoryPath,
            description = "测试不可达地址",
            context = "不应写入",
        });

        ProblemDetails problem = await ReadRequiredAsync<ProblemDetails>(createResponse);

        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.StartsWith("Git URL 不可达：", problem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage listResponse = await ownerClient.GetAsync("/api/repositories/standalone");
        List<RepositorySummaryResponse> repositories = await ReadRequiredAsync<List<RepositorySummaryResponse>>(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Empty(repositories);
    }

    [Fact]
    public async Task User_CannotAccessStandaloneRepositoryEndpoints()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string userEmail = "story24.user.forbidden@example.com";
        await CreateUserWithRolesAsync(userEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient userClient = CreateClient();
        await LoginAsync(userClient, userEmail, "Valid1!Pass");

        HttpResponseMessage listResponse = await userClient.GetAsync("/api/repositories/standalone");

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);

        string bareRepositoryPath = await CreateBareRepositoryAsync();
        HttpResponseMessage createResponse = await userClient.PostAsJsonAsync("/api/repositories/standalone", new
        {
            name = "普通用户仓库",
            gitUrl = bareRepositoryPath,
            description = "普通用户不应创建成功",
            context = "禁止访问",
        });

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        HttpResponseMessage pageResponse = await userClient.GetAsync("/");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain("管理后台", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRepository_WithUnreachableGitUrl_ReturnsChineseProblem_AndDoesNotPersist()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story23.admin.unreachable@example.com";
        const string ownerEmail = "story23.owner.unreachable@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(adminClient, "风控平台", "风控系统", "风控上下文");
        await AssignOwnerAsync(adminClient, systemId, ownerUserId);

        string missingRepositoryPath = Path.Combine(Path.GetTempPath(), "vulgata-tests", Guid.NewGuid().ToString("N"), "missing.git");

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage createResponse = await ownerClient.PostAsJsonAsync($"/api/systems/{systemId}/repositories", new
        {
            name = "不可达仓库",
            gitUrl = missingRepositoryPath,
            description = "测试不可达地址",
            context = "不应写入",
        });

        ProblemDetails problem = await ReadRequiredAsync<ProblemDetails>(createResponse);

        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.StartsWith("Git URL 不可达：", problem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage listResponse = await ownerClient.GetAsync($"/api/systems/{systemId}/repositories");
        List<RepositorySummaryResponse> repositories = await ReadRequiredAsync<List<RepositorySummaryResponse>>(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Empty(repositories);
    }

    [Fact]
    public async Task CreateRepository_WhenRemoteRequiresAuthentication_ReturnsChineseProblem_WithoutLeakingSecrets()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story23.admin.auth@example.com";
        const string ownerEmail = "story23.owner.auth@example.com";
        const string secret = "SecretToken123";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(adminClient, "认证平台", "认证系统", "认证上下文");
        await AssignOwnerAsync(adminClient, systemId, ownerUserId);

        await using UnauthorizedGitRemoteServer server = await UnauthorizedGitRemoteServer.StartAsync();
        string protectedUrl = $"http://alice:{secret}@127.0.0.1:{server.Port}/private.git";

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage createResponse = await ownerClient.PostAsJsonAsync($"/api/systems/{systemId}/repositories", new
        {
            name = "受保护仓库",
            gitUrl = protectedUrl,
            description = "需要认证",
            context = "不应泄露凭据",
        });

        ProblemDetails problem = await ReadRequiredAsync<ProblemDetails>(createResponse);
        string detail = problem.Detail ?? string.Empty;

        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        Assert.Contains("需要认证", detail, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, detail, StringComparison.Ordinal);
        Assert.DoesNotContain("alice:" + secret, detail, StringComparison.Ordinal);
        Assert.DoesNotContain(protectedUrl, detail, StringComparison.Ordinal);

        HttpResponseMessage listResponse = await ownerClient.GetAsync($"/api/systems/{systemId}/repositories");
        List<RepositorySummaryResponse> repositories = await ReadRequiredAsync<List<RepositorySummaryResponse>>(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Empty(repositories);
    }

    [Fact]
    public async Task SystemOwner_CannotManageRepositories_ForUnassignedSystem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story23.admin.scope@example.com";
        const string ownerEmail = "story23.owner.scope@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid assignedSystemId = await CreateSystemAsync(adminClient, "支付平台", "支付系统", "支付上下文");
        Guid hiddenSystemId = await CreateSystemAsync(adminClient, "数据平台", "数据系统", "数据上下文");
        await AssignOwnerAsync(adminClient, assignedSystemId, ownerUserId);

        string bareRepositoryPath = await CreateBareRepositoryAsync();
        HttpResponseMessage adminCreateResponse = await adminClient.PostAsJsonAsync($"/api/systems/{hiddenSystemId}/repositories", new
        {
            name = "隐藏仓库",
            gitUrl = bareRepositoryPath,
            description = "管理员创建",
            context = "系统所有者不应访问",
        });
        RepositoryDetailResponse hiddenRepository = await ReadRequiredAsync<RepositoryDetailResponse>(adminCreateResponse);
        Assert.Equal(HttpStatusCode.Created, adminCreateResponse.StatusCode);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage listResponse = await ownerClient.GetAsync($"/api/systems/{hiddenSystemId}/repositories");
        ProblemDetails listProblem = await ReadRequiredAsync<ProblemDetails>(listResponse);

        Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);
        Assert.Contains("系统不存在", listProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage createResponse = await ownerClient.PostAsJsonAsync($"/api/systems/{hiddenSystemId}/repositories", new
        {
            name = "越权仓库",
            gitUrl = bareRepositoryPath,
            description = "越权创建",
            context = "不应成功",
        });
        ProblemDetails createProblem = await ReadRequiredAsync<ProblemDetails>(createResponse);

        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);
        Assert.Contains("系统不存在", createProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage deleteResponse = await ownerClient.DeleteAsync($"/api/systems/{hiddenSystemId}/repositories/{hiddenRepository.Id}");
        ProblemDetails deleteProblem = await ReadRequiredAsync<ProblemDetails>(deleteResponse);

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        Assert.Contains("系统不存在", deleteProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage adminListResponse = await adminClient.GetAsync($"/api/systems/{hiddenSystemId}/repositories");
        List<RepositorySummaryResponse> adminRepositories = await ReadRequiredAsync<List<RepositorySummaryResponse>>(adminListResponse);

        Assert.Single(adminRepositories);
        Assert.Equal(hiddenRepository.Id, adminRepositories[0].Id);
    }

    [Fact]
    public async Task Administrator_CanManageRepositories_ForAnySystem_AndDeleteRefreshesManagementPage()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story23.admin.delete@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(adminClient, "文档平台", "文档系统", "文档上下文");
        string bareRepositoryPath = await CreateBareRepositoryAsync();

        HttpResponseMessage createResponse = await adminClient.PostAsJsonAsync($"/api/systems/{systemId}/repositories", new
        {
            name = "文档仓库",
            gitUrl = bareRepositoryPath,
            description = "管理员管理的仓库",
            context = "用于删除刷新验证",
        });

        RepositoryDetailResponse created = await ReadRequiredAsync<RepositoryDetailResponse>(createResponse);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        HttpResponseMessage beforeDeletePageResponse = await adminClient.GetAsync("/management");
        string beforeDeleteHtml = WebUtility.HtmlDecode(await beforeDeletePageResponse.Content.ReadAsStringAsync());

        Assert.Contains("文档仓库", beforeDeleteHtml, StringComparison.Ordinal);
        Assert.Contains("管理所有者", beforeDeleteHtml, StringComparison.Ordinal);
        Assert.Contains("编辑", beforeDeleteHtml, StringComparison.Ordinal);
        Assert.Contains("+ 新建系统", beforeDeleteHtml, StringComparison.Ordinal);
        Assert.Contains("+ 新建仓库", beforeDeleteHtml, StringComparison.Ordinal);

        HttpResponseMessage deleteResponse = await adminClient.DeleteAsync($"/api/systems/{systemId}/repositories/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage listResponse = await adminClient.GetAsync($"/api/systems/{systemId}/repositories");
        List<RepositorySummaryResponse> repositories = await ReadRequiredAsync<List<RepositorySummaryResponse>>(listResponse);

        Assert.Empty(repositories);

        HttpResponseMessage afterDeletePageResponse = await adminClient.GetAsync("/management");
        string afterDeleteHtml = WebUtility.HtmlDecode(await afterDeletePageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, afterDeletePageResponse.StatusCode);
        Assert.DoesNotContain("文档仓库", afterDeleteHtml, StringComparison.Ordinal);
        Assert.Contains("文档平台", afterDeleteHtml, StringComparison.Ordinal);
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
        T? value = await response.Content.ReadFromJsonAsync<T>();
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

    private class RepositorySummaryResponse
    {
        public Guid Id { get; set; }
        public Guid? SystemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ScanStatus { get; set; } = string.Empty;
        public DateTimeOffset? LastScannedAt { get; set; }
        public int DocumentCount { get; set; }
    }

    private sealed class RepositoryDetailResponse : RepositorySummaryResponse
    {
        public string GitUrl { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Context { get; set; }
    }

    private sealed class UnauthorizedGitRemoteServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _acceptLoop;

        private UnauthorizedGitRemoteServer(TcpListener listener)
        {
            _listener = listener;
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cancellationTokenSource.Token));
        }

        public int Port { get; }

        public static Task<UnauthorizedGitRemoteServer> StartAsync()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new UnauthorizedGitRemoteServer(listener));
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel();
            _listener.Stop();

            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException)
            {
                // Ignore shutdown cancellation.
            }
            catch (SocketException)
            {
                // Ignore socket teardown noise.
            }

            _cancellationTokenSource.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    break;
                }

                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }

        private static async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using TcpClient ownedClient = client;
            using NetworkStream stream = ownedClient.GetStream();
            using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true);

            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
            {
            }

            byte[] responseBytes = Encoding.ASCII.GetBytes(
                "HTTP/1.1 401 Unauthorized\r\n" +
                "WWW-Authenticate: Basic realm=\"git\"\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n\r\n");

            await stream.WriteAsync(responseBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }
}

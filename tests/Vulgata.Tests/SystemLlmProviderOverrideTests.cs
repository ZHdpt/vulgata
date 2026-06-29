using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Shared.Systems;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class SystemLlmProviderOverrideTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public SystemLlmProviderOverrideTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Administrator_CanCreateUpdateAndDeleteSystemLlmProviderOverride()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story32.admin.crud@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story32.admin.crud@example.com", "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "文档生成平台", "系统级覆盖", "Story 3.2");
        LlmProviderResponse firstProvider = await CreateProviderAsync(client, "编排默认一", "https://provider-one.example.com/v1", 1, 0);
        LlmProviderResponse secondProvider = await CreateProviderAsync(client, "编排默认二", "https://provider-two.example.com/v1", 1, 0);
        LlmProviderResponse workerProvider = await CreateProviderAsync(client, "工作代理默认", "https://provider-worker.example.com/v1", 1, 1);

        HttpResponseMessage createResponse = await client.PostAsJsonAsync($"/api/systems/{systemId}/llm-provider-overrides", new
        {
            systemId,
            llmProviderId = firstProvider.Id,
            agentType = 0,
        });

        SystemLlmProviderOverrideResponse created = await ReadRequiredAsync<SystemLlmProviderOverrideResponse>(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(systemId, created.SystemId);
        Assert.Equal(firstProvider.Id, created.LlmProviderId);
        Assert.Equal(0, created.AgentType);
        Assert.Equal("编排默认一", created.ProviderName);
        Assert.False(created.UsesGlobalDefault);

        HttpResponseMessage listResponse = await client.GetAsync($"/api/systems/{systemId}/llm-provider-overrides");
        List<SystemLlmProviderOverrideResponse> overrides = await ReadRequiredAsync<List<SystemLlmProviderOverrideResponse>>(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Single(overrides);
        Assert.Equal(created.Id, overrides[0].Id);

        HttpResponseMessage addWorkerResponse = await client.PostAsJsonAsync($"/api/systems/{systemId}/llm-provider-overrides", new
        {
            systemId,
            llmProviderId = workerProvider.Id,
            agentType = 1,
        });

        SystemLlmProviderOverrideResponse workerOverride = await ReadRequiredAsync<SystemLlmProviderOverrideResponse>(addWorkerResponse);

        Assert.Equal(HttpStatusCode.Created, addWorkerResponse.StatusCode);
        Assert.Equal(1, workerOverride.AgentType);

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/systems/{systemId}/llm-provider-overrides/{created.Id}", new
        {
            systemId,
            llmProviderId = secondProvider.Id,
            agentType = 0,
        });

        SystemLlmProviderOverrideResponse updated = await ReadRequiredAsync<SystemLlmProviderOverrideResponse>(updateResponse);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(secondProvider.Id, updated.LlmProviderId);
        Assert.Equal("编排默认二", updated.ProviderName);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);

        HttpResponseMessage afterUpdateListResponse = await client.GetAsync($"/api/systems/{systemId}/llm-provider-overrides");
        List<SystemLlmProviderOverrideResponse> afterUpdate = await ReadRequiredAsync<List<SystemLlmProviderOverrideResponse>>(afterUpdateListResponse);

        Assert.Equal(2, afterUpdate.Count);
        Assert.Single(afterUpdate.Where(item => item.AgentType == 0));
        Assert.Single(afterUpdate.Where(item => item.AgentType == 1));
        Assert.Equal(2, await CountOverridesAsync(systemId));

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/systems/{systemId}/llm-provider-overrides/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage afterDeleteListResponse = await client.GetAsync($"/api/systems/{systemId}/llm-provider-overrides");
        List<SystemLlmProviderOverrideResponse> afterDelete = await ReadRequiredAsync<List<SystemLlmProviderOverrideResponse>>(afterDeleteListResponse);

        Assert.Single(afterDelete);
        Assert.Equal(workerOverride.Id, afterDelete[0].Id);
    }

    [Fact]
    public async Task SystemOwner_CanManageOverrides_ForAssignedSystemOnly()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story32.admin.owner@example.com";
        const string ownerEmail = "story32.owner.assigned@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid assignedSystemId = await CreateSystemAsync(adminClient, "订单平台", "可见系统", "owner-assigned");
        Guid hiddenSystemId = await CreateSystemAsync(adminClient, "营销平台", "不可见系统", "owner-hidden");
        await AssignOwnerAsync(adminClient, assignedSystemId, ownerUserId);

        LlmProviderResponse orchestratorProvider = await CreateProviderAsync(adminClient, "编排候选", "https://owner-provider.example.com/v1", 1, 0);
        LlmProviderResponse chatProvider = await CreateProviderAsync(adminClient, "对话候选", "https://owner-chat.example.com/v1", 1, 2);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage candidatesResponse = await ownerClient.GetAsync($"/api/systems/{assignedSystemId}/llm-provider-overrides/providers");
        List<LlmProviderResponse> candidates = await ReadRequiredAsync<List<LlmProviderResponse>>(candidatesResponse);

        Assert.Equal(HttpStatusCode.OK, candidatesResponse.StatusCode);
        Assert.Contains(candidates, provider => provider.Id == orchestratorProvider.Id);
        Assert.Contains(candidates, provider => provider.Id == chatProvider.Id);

        HttpResponseMessage createResponse = await ownerClient.PostAsJsonAsync($"/api/systems/{assignedSystemId}/llm-provider-overrides", new
        {
            systemId = assignedSystemId,
            llmProviderId = orchestratorProvider.Id,
            agentType = 0,
        });

        SystemLlmProviderOverrideResponse created = await ReadRequiredAsync<SystemLlmProviderOverrideResponse>(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(orchestratorProvider.Id, created.LlmProviderId);

        HttpResponseMessage hiddenListResponse = await ownerClient.GetAsync($"/api/systems/{hiddenSystemId}/llm-provider-overrides");
        ProblemDetails hiddenListProblem = await ReadRequiredAsync<ProblemDetails>(hiddenListResponse);

        Assert.Equal(HttpStatusCode.NotFound, hiddenListResponse.StatusCode);
        Assert.Contains("系统不存在", hiddenListProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage hiddenCreateResponse = await ownerClient.PostAsJsonAsync($"/api/systems/{hiddenSystemId}/llm-provider-overrides", new
        {
            systemId = hiddenSystemId,
            llmProviderId = orchestratorProvider.Id,
            agentType = 0,
        });

        ProblemDetails hiddenCreateProblem = await ReadRequiredAsync<ProblemDetails>(hiddenCreateResponse);

        Assert.Equal(HttpStatusCode.NotFound, hiddenCreateResponse.StatusCode);
        Assert.Contains("系统不存在", hiddenCreateProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage hiddenCandidatesResponse = await ownerClient.GetAsync($"/api/systems/{hiddenSystemId}/llm-provider-overrides/providers");
        ProblemDetails hiddenCandidatesProblem = await ReadRequiredAsync<ProblemDetails>(hiddenCandidatesResponse);

        Assert.Equal(HttpStatusCode.NotFound, hiddenCandidatesResponse.StatusCode);
        Assert.Contains("系统不存在", hiddenCandidatesProblem.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateOverride_WithUnknownProvider_ReturnsChineseProblem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story32.admin.unknown@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story32.admin.unknown@example.com", "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "知识平台", "未知 provider", "story 3.2 unknown");

        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/systems/{systemId}/llm-provider-overrides", new
        {
            systemId,
            llmProviderId = Guid.NewGuid(),
            agentType = 0,
        });

        ProblemDetails problem = await ReadRequiredAsync<ProblemDetails>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("LLM 提供商不存在", problem.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateOverride_WithMismatchedAgentType_ReturnsChineseValidationError()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story32.admin.mismatch@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story32.admin.mismatch@example.com", "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "客服平台", "角色匹配校验", "story 3.2 mismatch");
        LlmProviderResponse chatProvider = await CreateProviderAsync(client, "仅对话提供商", "https://chat-only.example.com/v1", 1, 2);

        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/systems/{systemId}/llm-provider-overrides", new
        {
            systemId,
            llmProviderId = chatProvider.Id,
            agentType = 0,
        });

        ValidationProblemDetails problem = await ReadRequiredAsync<ValidationProblemDetails>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.Errors.TryGetValue("AgentType", out string[]? errors));
        Assert.NotNull(errors);
        Assert.Contains(errors!, message => message.Contains("默认代理角色", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DuplicateSystemAndAgentType_IsBlockedByDatabaseConstraint()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story32.admin.unique@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story32.admin.unique@example.com", "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "合规平台", "唯一约束", "story 3.2 unique");
        LlmProviderResponse firstProvider = await CreateProviderAsync(client, "唯一约束一", "https://unique-one.example.com/v1", 1, 0);
        LlmProviderResponse secondProvider = await CreateProviderAsync(client, "唯一约束二", "https://unique-two.example.com/v1", 1, 0);

        HttpResponseMessage createResponse = await client.PostAsJsonAsync($"/api/systems/{systemId}/llm-provider-overrides", new
        {
            systemId,
            llmProviderId = firstProvider.Id,
            agentType = 0,
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        await using SqliteConnection connection = new(_factory.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand duplicateInsert = connection.CreateCommand();
        duplicateInsert.CommandText = """
            INSERT INTO SystemLlmProviderOverrides
                (Id, SystemId, LlmProviderId, AgentType, CreatedAt, UpdatedAt)
            VALUES
                ($id, $systemId, $providerId, $agentType, $createdAt, $updatedAt);
            """;
        duplicateInsert.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        duplicateInsert.Parameters.AddWithValue("$systemId", systemId.ToString());
        duplicateInsert.Parameters.AddWithValue("$providerId", secondProvider.Id.ToString());
        duplicateInsert.Parameters.AddWithValue("$agentType", 0);
        duplicateInsert.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        duplicateInsert.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));

        await Assert.ThrowsAnyAsync<SqliteException>(() => duplicateInsert.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task DashboardPage_ShowsOverrideSection_AndEffectiveProviderFallsBackToGlobalDefault()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story32.admin.page@example.com";
        const string ownerEmail = "story32.owner.page@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(adminClient, "智能体平台", "页面展示", "story 3.2 dashboard");
        await AssignOwnerAsync(adminClient, systemId, ownerUserId);

        LlmProviderResponse orchestratorDefault = await CreateProviderAsync(adminClient, "全局编排默认", "https://fallback-orchestrator.example.com/v1", 1, 0);
        LlmProviderResponse orchestratorOverride = await CreateProviderAsync(adminClient, "系统编排覆盖", "https://override-orchestrator.example.com/v1", 1, 0);

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage initialPageResponse = await ownerClient.GetAsync("/management");
        string initialHtml = WebUtility.HtmlDecode(await initialPageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, initialPageResponse.StatusCode);
        Assert.Contains("LLM Provider 覆盖", initialHtml, StringComparison.Ordinal);
        Assert.Contains("编排代理", initialHtml, StringComparison.Ordinal);
        Assert.Contains("工作代理", initialHtml, StringComparison.Ordinal);
        Assert.Contains("对话代理", initialHtml, StringComparison.Ordinal);
        Assert.Contains("使用全局默认提供商", initialHtml, StringComparison.Ordinal);

        EffectiveProviderResult initialEffective = await GetEffectiveProviderAsync(systemId, AgentType.Orchestrator, ownerUserId, false);
        Assert.Equal(orchestratorDefault.Id, initialEffective.ProviderId);
        Assert.False(initialEffective.IsOverride);

        HttpResponseMessage createResponse = await ownerClient.PostAsJsonAsync($"/api/systems/{systemId}/llm-provider-overrides", new
        {
            systemId,
            llmProviderId = orchestratorOverride.Id,
            agentType = 0,
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        EffectiveProviderResult overridden = await GetEffectiveProviderAsync(systemId, AgentType.Orchestrator, ownerUserId, false);
        Assert.Equal(orchestratorOverride.Id, overridden.ProviderId);
        Assert.True(overridden.IsOverride);

        HttpResponseMessage updatedPageResponse = await ownerClient.GetAsync("/management");
        string updatedHtml = WebUtility.HtmlDecode(await updatedPageResponse.Content.ReadAsStringAsync());

        Assert.Contains("系统编排覆盖", updatedHtml, StringComparison.Ordinal);
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

        await ExecuteNonQueryIgnoringMissingTableAsync(connection, "DELETE FROM SystemLlmProviderOverrides;");
        await ExecuteNonQueryIgnoringMissingTableAsync(connection, "DELETE FROM LlmProviders;");
        await ExecuteNonQueryIgnoringMissingTableAsync(connection, "DELETE FROM SystemOwnerAssignments;");
        await ExecuteNonQueryIgnoringMissingTableAsync(connection, "DELETE FROM Repositories;");
        await ExecuteNonQueryIgnoringMissingTableAsync(connection, "DELETE FROM Systems;");
    }

    private static async Task ExecuteNonQueryIgnoringMissingTableAsync(SqliteConnection connection, string sql)
    {
        try
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            // Allow red-phase runs before the new table exists.
        }
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

    private async Task<LlmProviderResponse> CreateProviderAsync(
        HttpClient client,
        string name,
        string baseEndpointUrl,
        int supportedApiTypes,
        int defaultAgentType)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/llm-providers", new
        {
            name,
            baseEndpointUrl,
            apiKey = "override-secret-key",
            supportedApiTypes,
            defaultAgentType,
        });

        LlmProviderResponse created = await ReadRequiredAsync<LlmProviderResponse>(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return created;
    }

    private async Task<int> CountOverridesAsync(Guid systemId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        VulgataDbContext dbContext = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        return await dbContext.SystemLlmProviderOverrides
            .Where(item => item.SystemId == systemId)
            .CountAsync();
    }

    private async Task<EffectiveProviderResult> GetEffectiveProviderAsync(
        Guid systemId,
        AgentType agentType,
        string userId,
        bool isAdministrator)
    {
        Type? coordinatorType = typeof(Program).Assembly.GetType("Vulgata.Web.Data.ISystemLlmProviderOverrideCoordinator");
        Assert.NotNull(coordinatorType);

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        object? coordinator = scope.ServiceProvider.GetService(coordinatorType!);
        Assert.NotNull(coordinator);

        MethodInfo? method = coordinatorType!.GetMethod("GetEffectiveProviderAsync");
        Assert.NotNull(method);

        object? taskObject = method!.Invoke(coordinator, [systemId, agentType, userId, isAdministrator, CancellationToken.None]);
        Assert.NotNull(taskObject);

        Task task = (Task)taskObject!;
        await task;

        object? result = task.GetType().GetProperty("Result")?.GetValue(task);
        Assert.NotNull(result);

        PropertyInfo? providerIdProperty = result!.GetType().GetProperty("ProviderId");
        PropertyInfo? isOverrideProperty = result.GetType().GetProperty("IsOverride");
        Assert.NotNull(providerIdProperty);
        Assert.NotNull(isOverrideProperty);

        return new EffectiveProviderResult(
            (Guid)(providerIdProperty!.GetValue(result) ?? Guid.Empty),
            (bool)(isOverrideProperty!.GetValue(result) ?? false));
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

    private sealed class LlmProviderResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DefaultAgentType { get; set; }
    }

    private sealed class SystemLlmProviderOverrideResponse
    {
        public Guid Id { get; set; }
        public Guid SystemId { get; set; }
        public Guid LlmProviderId { get; set; }
        public int AgentType { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public bool UsesGlobalDefault { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed record EffectiveProviderResult(Guid ProviderId, bool IsOverride);
}

using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class LlmProviderConfigTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public LlmProviderConfigTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Administrator_CanCreateListUpdateAndDeleteLlmProvider_AndApiKeyIsEncrypted()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story31.admin.crud@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story31.admin.crud@example.com", "Valid1!Pass");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/llm-providers", new
        {
            name = "DeepSeek 主提供商",
            baseEndpointUrl = "https://api.deepseek.com/v1",
            apiKey = "super-secret-key",
            supportedApiTypes = 3,
            defaultAgentType = 0,
        });

        LlmProviderResponse created = await ReadRequiredAsync<LlmProviderResponse>(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("DeepSeek 主提供商", created.Name);
        Assert.Equal("https://api.deepseek.com/v1", created.BaseEndpointUrl);
        Assert.True(created.HasApiKey);
        Assert.Equal(3, created.SupportedApiTypes);
        Assert.Equal(0, created.DefaultAgentType);

        string encryptedApiKey = await GetEncryptedApiKeyAsync(created.Id);
        Assert.NotEmpty(encryptedApiKey);
        Assert.NotEqual("super-secret-key", encryptedApiKey);

        HttpResponseMessage listResponse = await client.GetAsync("/api/llm-providers");
        List<LlmProviderResponse> providers = await ReadRequiredAsync<List<LlmProviderResponse>>(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Single(providers);
        Assert.Equal(created.Id, providers[0].Id);
        Assert.Equal("DeepSeek 主提供商", providers[0].Name);

        HttpResponseMessage pageResponse = await client.GetAsync("/management/settings/llm-providers");
        string pageHtml = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("LLM 提供商管理", pageHtml, StringComparison.Ordinal);
        Assert.Contains("新增提供商", pageHtml, StringComparison.Ordinal);
        Assert.Contains("DeepSeek 主提供商", pageHtml, StringComparison.Ordinal);
        Assert.Contains("测试连接", pageHtml, StringComparison.Ordinal);

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/llm-providers/{created.Id}", new
        {
            name = "DeepSeek 生产",
            baseEndpointUrl = "https://api.deepseek.com/v1/",
            apiKey = "",
            supportedApiTypes = 4,
            defaultAgentType = 1,
        });

        LlmProviderResponse updated = await ReadRequiredAsync<LlmProviderResponse>(updateResponse);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("DeepSeek 生产", updated.Name);
        Assert.Equal("https://api.deepseek.com/v1", updated.BaseEndpointUrl);
        Assert.Equal(4, updated.SupportedApiTypes);
        Assert.Equal(1, updated.DefaultAgentType);
        Assert.True(updated.HasApiKey);
        Assert.Equal(encryptedApiKey, await GetEncryptedApiKeyAsync(created.Id));

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/llm-providers/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage afterDeleteResponse = await client.GetAsync("/api/llm-providers");
        List<LlmProviderResponse> afterDeleteProviders = await ReadRequiredAsync<List<LlmProviderResponse>>(afterDeleteResponse);

        Assert.Empty(afterDeleteProviders);
    }

    [Fact]
    public async Task DuplicateLlmProviderName_ReturnsChineseValidationProblem()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story31.admin.duplicate@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story31.admin.duplicate@example.com", "Valid1!Pass");

        LlmProviderResponse existing = await CreateProviderAsync(client, "OpenAI 兼容", "https://provider.example.com/v1", "key-1", 1, 0);
        LlmProviderResponse another = await CreateProviderAsync(client, "Anthropic 兼容", "https://provider-two.example.com/v1", "key-2", 2, 2);

        HttpResponseMessage createDuplicateResponse = await client.PostAsJsonAsync("/api/llm-providers", new
        {
            name = "  OpenAI 兼容  ",
            baseEndpointUrl = "https://duplicate.example.com/v1",
            apiKey = "key-3",
            supportedApiTypes = 1,
            defaultAgentType = 0,
        });

        ValidationProblemDetails createProblem = await ReadRequiredAsync<ValidationProblemDetails>(createDuplicateResponse);

        Assert.Equal(HttpStatusCode.BadRequest, createDuplicateResponse.StatusCode);
        Assert.True(createProblem.Errors.TryGetValue("Name", out string[]? createErrors));
        Assert.NotNull(createErrors);
        Assert.Contains(createErrors!, error => error.Contains("提供商名称已存在", StringComparison.Ordinal));

        HttpResponseMessage updateDuplicateResponse = await client.PutAsJsonAsync($"/api/llm-providers/{another.Id}", new
        {
            name = "OpenAI 兼容",
            baseEndpointUrl = "https://provider-two.example.com/v1",
            apiKey = "",
            supportedApiTypes = 2,
            defaultAgentType = 2,
        });

        ValidationProblemDetails updateProblem = await ReadRequiredAsync<ValidationProblemDetails>(updateDuplicateResponse);

        Assert.Equal(HttpStatusCode.BadRequest, updateDuplicateResponse.StatusCode);
        Assert.True(updateProblem.Errors.TryGetValue("Name", out string[]? updateErrors));
        Assert.NotNull(updateErrors);
        Assert.Contains(updateErrors!, error => error.Contains("提供商名称已存在", StringComparison.Ordinal));
        Assert.NotEqual(existing.Id, another.Id);
    }

    [Fact]
    public async Task ConnectionTest_WithMockHttp_ReturnsChineseSuccessAndFailureMessages()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story31.admin.connection@example.com", "Valid1!Pass", RoleNames.Administrator);

        await using MockLlmProviderServer server = await MockLlmProviderServer.StartAsync("good-key");

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story31.admin.connection@example.com", "Valid1!Pass");

        LlmProviderResponse healthyProvider = await CreateProviderAsync(
            client,
            "本地测试提供商",
            $"http://127.0.0.1:{server.Port}/v1",
            "good-key",
            1,
            0);

        HttpResponseMessage successResponse = await client.PostAsync($"/api/llm-providers/{healthyProvider.Id}/test", content: null);
        ConnectionTestResponse success = await ReadRequiredAsync<ConnectionTestResponse>(successResponse);

        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);
        Assert.True(success.Success);
        Assert.Contains("连接测试成功", success.Message, StringComparison.Ordinal);

        LlmProviderResponse brokenProvider = await CreateProviderAsync(
            client,
            "错误密钥提供商",
            $"http://127.0.0.1:{server.Port}/v1",
            "wrong-key",
            1,
            0);

        HttpResponseMessage failureResponse = await client.PostAsync($"/api/llm-providers/{brokenProvider.Id}/test", content: null);
        ProblemDetails failure = await ReadRequiredAsync<ProblemDetails>(failureResponse);

        Assert.Equal(HttpStatusCode.BadRequest, failureResponse.StatusCode);
        Assert.Contains("连接测试失败", failure.Detail ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("凭据无效", failure.Detail ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong-key", failure.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemOwner_CannotManageLlmProviders_AndSettingsPageDoesNotExposeEntry()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();
        await CreateUserWithRolesAsync("story31.owner.forbidden@example.com", "Valid1!Pass", RoleNames.SystemOwner);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story31.owner.forbidden@example.com", "Valid1!Pass");

        HttpResponseMessage settingsResponse = await client.GetAsync("/management/settings");
        string settingsHtml = WebUtility.HtmlDecode(await settingsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        Assert.DoesNotContain("/management/settings/llm-providers", settingsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("LLM 提供商配置", settingsHtml, StringComparison.Ordinal);

        HttpResponseMessage listResponse = await client.GetAsync("/api/llm-providers");
        ProblemDetails listProblem = await ReadRequiredAsync<ProblemDetails>(listResponse);

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Contains("只有管理员可以管理 LLM 提供商配置", listProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/llm-providers", new
        {
            name = "越权提供商",
            baseEndpointUrl = "https://forbidden.example.com/v1",
            apiKey = "blocked",
            supportedApiTypes = 1,
            defaultAgentType = 0,
        });

        ProblemDetails createProblem = await ReadRequiredAsync<ProblemDetails>(createResponse);

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.Contains("只有管理员可以管理 LLM 提供商配置", createProblem.Detail ?? string.Empty, StringComparison.Ordinal);
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
        await EnsureLlmProviderTableAsync(connection);
        await ExecuteNonQueryAsync(connection, "DELETE FROM LlmProviders;");
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureLlmProviderTableAsync(SqliteConnection connection)
    {
        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS LlmProviders (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                NormalizedName TEXT NOT NULL,
                BaseEndpointUrl TEXT NOT NULL,
                EncryptedApiKey TEXT NOT NULL,
                SupportedApiTypes INTEGER NOT NULL,
                DefaultAgentType INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_LlmProviders_NormalizedName
            ON LlmProviders (NormalizedName);
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

    private async Task<LlmProviderResponse> CreateProviderAsync(
        HttpClient client,
        string name,
        string baseEndpointUrl,
        string apiKey,
        int supportedApiTypes,
        int defaultAgentType)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/llm-providers", new
        {
            name,
            baseEndpointUrl,
            apiKey,
            supportedApiTypes,
            defaultAgentType,
        });

        LlmProviderResponse created = await ReadRequiredAsync<LlmProviderResponse>(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return created;
    }

    private async Task<string> GetEncryptedApiKeyAsync(Guid providerId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        VulgataDbContext dbContext = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();

        string? encryptedApiKey = await dbContext.LlmProviders
            .AsNoTracking()
            .Where(provider => provider.Id == providerId)
            .Select(provider => provider.EncryptedApiKey)
            .SingleOrDefaultAsync();

        Assert.NotNull(encryptedApiKey);
        return encryptedApiKey;
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

    private sealed class LlmProviderResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BaseEndpointUrl { get; set; } = string.Empty;
        public int SupportedApiTypes { get; set; }
        public int DefaultAgentType { get; set; }
        public bool HasApiKey { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class ConnectionTestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private sealed class MockLlmProviderServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _acceptLoop;
        private readonly string _expectedAuthorizationHeader;

        private MockLlmProviderServer(TcpListener listener, string expectedApiKey)
        {
            _listener = listener;
            _expectedAuthorizationHeader = $"Bearer {expectedApiKey}";
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cancellationTokenSource.Token));
        }

        public int Port { get; }

        public static Task<MockLlmProviderServer> StartAsync(string expectedApiKey)
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new MockLlmProviderServer(listener, expectedApiKey));
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
            }
            catch (SocketException)
            {
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

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using TcpClient ownedClient = client;
            using NetworkStream stream = ownedClient.GetStream();
            using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true);

            string requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            string authorizationHeader = string.Empty;
            string line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken) ?? string.Empty))
            {
                if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                {
                    authorizationHeader = line["Authorization:".Length..].Trim();
                }
            }

            bool isAuthorized = authorizationHeader.Equals(_expectedAuthorizationHeader, StringComparison.Ordinal);
            bool isModelsRequest = requestLine.StartsWith("GET /v1/models", StringComparison.Ordinal);

            string body = isAuthorized && isModelsRequest
                ? "{\"data\":[] }"
                : "{\"error\":\"unauthorized\"}";

            string statusLine = isAuthorized && isModelsRequest
                ? "HTTP/1.1 200 OK"
                : "HTTP/1.1 401 Unauthorized";

            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string response = $"{statusLine}\r\nContent-Type: application/json\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n{body}";
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);

            await stream.WriteAsync(responseBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }
}

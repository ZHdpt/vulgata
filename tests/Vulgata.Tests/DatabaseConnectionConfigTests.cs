using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Shared;
using Vulgata.Web.Data;
using Vulgata.Shared.Systems;

namespace Vulgata.Tests;

public sealed class DatabaseConnectionConfigTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public DatabaseConnectionConfigTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ManagementUser_CanCreateAndReadDatabaseConnection_AndSecretsAreEncrypted()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story33.admin.create@example.com";
        const string ownerEmail = "story33.owner.create@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(adminClient, "数据库平台", "数据库管理", "数据库上下文");
        await AssignOwnerAsync(adminClient, systemId, ownerUserId);
        Guid repositoryId = await CreateRepositoryAsync(adminClient, systemId, "连接仓库");

        string sqliteConnectionString = await CreateSqliteConnectionStringAsync();

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage saveResponse = await ownerClient.PutAsJsonAsync($"/api/repositories/{repositoryId}/database-connection", new
        {
            databaseType = 3,
            connectionString = sqliteConnectionString,
            username = "readonly-user",
            password = "super-secret-password",
        });

        DatabaseConnectionSummaryResponse saved = await ReadRequiredAsync<DatabaseConnectionSummaryResponse>(saveResponse);

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        Assert.True(saved.IsConfigured);
        Assert.Equal(3, saved.DatabaseType);
        Assert.True(saved.HasConnectionString);
        Assert.True(saved.HasUsername);
        Assert.True(saved.HasPassword);
        Assert.NotNull(saved.UpdatedAt);

        HttpResponseMessage getResponse = await ownerClient.GetAsync($"/api/repositories/{repositoryId}/database-connection");
        DatabaseConnectionSummaryResponse loaded = await ReadRequiredAsync<DatabaseConnectionSummaryResponse>(getResponse);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.True(loaded.IsConfigured);
        Assert.Equal(saved.RepositoryId, loaded.RepositoryId);
        Assert.Equal(saved.DatabaseType, loaded.DatabaseType);
        Assert.True(loaded.HasConnectionString);
        Assert.True(loaded.HasUsername);
        Assert.True(loaded.HasPassword);

        PersistedDatabaseConnectionRecord persisted = await GetPersistedDatabaseConnectionAsync(repositoryId);

        Assert.NotEqual(sqliteConnectionString, persisted.EncryptedConnectionString);
        Assert.NotEqual("readonly-user", persisted.EncryptedUsername);
        Assert.NotEqual("super-secret-password", persisted.EncryptedPassword);
    }

    [Fact]
    public async Task UpdateDatabaseConnection_WithBlankUsernameAndPassword_PreservesExistingSecrets()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story33.admin.update@example.com";
        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "分析平台", "分析系统", "分析上下文");
        Guid repositoryId = await CreateRepositoryAsync(client, systemId, "更新仓库");

        HttpResponseMessage firstSaveResponse = await client.PutAsJsonAsync($"/api/repositories/{repositoryId}/database-connection", new
        {
            databaseType = 0,
            connectionString = "Host=localhost;Database=vulgata;Username=readonly;Password=first-secret",
            username = "readonly",
            password = "first-secret",
        });

        Assert.Equal(HttpStatusCode.OK, firstSaveResponse.StatusCode);

        PersistedDatabaseConnectionRecord original = await GetPersistedDatabaseConnectionAsync(repositoryId);

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/repositories/{repositoryId}/database-connection", new
        {
            databaseType = 1,
            connectionString = "Server=localhost;Database=vulgata;TrustServerCertificate=true",
            username = "",
            password = "",
        });

        DatabaseConnectionSummaryResponse updated = await ReadRequiredAsync<DatabaseConnectionSummaryResponse>(updateResponse);
        PersistedDatabaseConnectionRecord current = await GetPersistedDatabaseConnectionAsync(repositoryId);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.True(updated.IsConfigured);
        Assert.Equal(1, updated.DatabaseType);
        Assert.Equal(original.EncryptedUsername, current.EncryptedUsername);
        Assert.Equal(original.EncryptedPassword, current.EncryptedPassword);
        Assert.NotEqual(original.EncryptedConnectionString, current.EncryptedConnectionString);
    }

    [Fact]
    public async Task Repository_CanHaveAtMostOneDatabaseConnection()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story33.admin.unique@example.com";
        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "运营平台", "运营系统", "运营上下文");
        Guid repositoryId = await CreateRepositoryAsync(client, systemId, "唯一仓库");

        HttpResponseMessage firstSaveResponse = await client.PutAsJsonAsync($"/api/repositories/{repositoryId}/database-connection", new
        {
            databaseType = 3,
            connectionString = await CreateSqliteConnectionStringAsync(),
            username = "",
            password = "",
        });
        Assert.Equal(HttpStatusCode.OK, firstSaveResponse.StatusCode);

        HttpResponseMessage secondSaveResponse = await client.PutAsJsonAsync($"/api/repositories/{repositoryId}/database-connection", new
        {
            databaseType = 5,
            connectionString = "Driver={Custom};Server=db.internal;Database=vulgata;",
            username = "",
            password = "",
        });
        Assert.Equal(HttpStatusCode.OK, secondSaveResponse.StatusCode);

        int connectionCount = await CountDatabaseConnectionsAsync(repositoryId);
        Assert.Equal(1, connectionCount);
    }

    [Fact]
    public async Task DatabaseConnectionTest_WithSqliteConfiguration_ReturnsChineseSuccessMessage_AndManagementPageShowsSection()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story33.admin.test@example.com";
        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, adminEmail, "Valid1!Pass");

        Guid systemId = await CreateSystemAsync(client, "测试平台", "测试系统", "测试上下文");
        Guid repositoryId = await CreateRepositoryAsync(client, systemId, "测试仓库");
        string sqliteConnectionString = await CreateSqliteConnectionStringAsync();

        HttpResponseMessage saveResponse = await client.PutAsJsonAsync($"/api/repositories/{repositoryId}/database-connection", new
        {
            databaseType = 3,
            connectionString = sqliteConnectionString,
            username = "",
            password = "",
        });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        HttpResponseMessage testResponse = await client.PostAsync($"/api/repositories/{repositoryId}/database-connection/test", content: null);
        ConnectionTestResponse result = await ReadRequiredAsync<ConnectionTestResponse>(testResponse);

        Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
        Assert.True(result.Success);
        Assert.Contains("连接测试成功", result.Message, StringComparison.Ordinal);

        HttpResponseMessage pageResponse = await client.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("数据库连接", html, StringComparison.Ordinal);
        Assert.Contains("测试连接", html, StringComparison.Ordinal);
        Assert.Contains("测试仓库", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemOwner_CannotManageDatabaseConnection_ForHiddenRepository()
    {
        await EnsureApplicationStartedAsync();
        await ResetDomainStateAsync();

        const string adminEmail = "story33.admin.hidden@example.com";
        const string ownerEmail = "story33.owner.hidden@example.com";

        await CreateUserWithRolesAsync(adminEmail, "Valid1!Pass", RoleNames.Administrator);
        string ownerUserId = await CreateUserWithRolesAsync(ownerEmail, "Valid1!Pass", RoleNames.User);

        using HttpClient adminClient = CreateClient();
        await LoginAsync(adminClient, adminEmail, "Valid1!Pass");

        Guid visibleSystemId = await CreateSystemAsync(adminClient, "可见系统", "可见描述", "可见上下文");
        Guid hiddenSystemId = await CreateSystemAsync(adminClient, "隐藏系统", "隐藏描述", "隐藏上下文");
        await AssignOwnerAsync(adminClient, visibleSystemId, ownerUserId);
        Guid hiddenRepositoryId = await CreateRepositoryAsync(adminClient, hiddenSystemId, "隐藏仓库");

        using HttpClient ownerClient = CreateClient();
        await LoginAsync(ownerClient, ownerEmail, "Valid1!Pass");

        HttpResponseMessage getResponse = await ownerClient.GetAsync($"/api/repositories/{hiddenRepositoryId}/database-connection");
        ProblemDetails getProblem = await ReadRequiredAsync<ProblemDetails>(getResponse);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Contains("仓库不存在", getProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage saveResponse = await ownerClient.PutAsJsonAsync($"/api/repositories/{hiddenRepositoryId}/database-connection", new
        {
            databaseType = 3,
            connectionString = await CreateSqliteConnectionStringAsync(),
            username = "",
            password = "",
        });
        ProblemDetails saveProblem = await ReadRequiredAsync<ProblemDetails>(saveResponse);
        Assert.Equal(HttpStatusCode.NotFound, saveResponse.StatusCode);
        Assert.Contains("仓库不存在", saveProblem.Detail ?? string.Empty, StringComparison.Ordinal);

        HttpResponseMessage testResponse = await ownerClient.PostAsync($"/api/repositories/{hiddenRepositoryId}/database-connection/test", content: null);
        ProblemDetails testProblem = await ReadRequiredAsync<ProblemDetails>(testResponse);
        Assert.Equal(HttpStatusCode.NotFound, testResponse.StatusCode);
        Assert.Contains("仓库不存在", testProblem.Detail ?? string.Empty, StringComparison.Ordinal);
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

        await ExecuteNonQueryAsync(connection, "DELETE FROM DatabaseConnections;");
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
            CREATE TABLE IF NOT EXISTS DatabaseConnections (
                Id TEXT NOT NULL PRIMARY KEY,
                RepositoryId TEXT NOT NULL,
                EncryptedConnectionString TEXT NOT NULL,
                DatabaseType INTEGER NOT NULL,
                EncryptedUsername TEXT NULL,
                EncryptedPassword TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (RepositoryId) REFERENCES Repositories(Id) ON DELETE RESTRICT
            );
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_DatabaseConnections_RepositoryId
            ON DatabaseConnections (RepositoryId);
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

    private async Task<Guid> CreateRepositoryAsync(HttpClient client, Guid systemId, string repositoryName)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/systems/{systemId}/repositories", new
        {
            name = repositoryName,
            gitUrl = await CreateBareRepositoryAsync(),
            description = repositoryName + " 描述",
            context = repositoryName + " 上下文",
        });

        RepositoryDetailResponse created = await ReadRequiredAsync<RepositoryDetailResponse>(response);
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

    private static async Task<string> CreateSqliteConnectionStringAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "vulgata-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string databasePath = Path.Combine(directory, "database-connection-test.db");
        await using SqliteConnection connection = new($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS Probe (Id INTEGER PRIMARY KEY);";
        await command.ExecuteNonQueryAsync();

        return $"Data Source={databasePath}";
    }

    private async Task<PersistedDatabaseConnectionRecord> GetPersistedDatabaseConnectionAsync(Guid repositoryId)
    {
        await using SqliteConnection connection = new(_factory.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT RepositoryId, EncryptedConnectionString, EncryptedUsername, EncryptedPassword
            FROM DatabaseConnections
            WHERE RepositoryId = $repositoryId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$repositoryId", repositoryId.ToString());

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected a persisted database connection record.");

        return new PersistedDatabaseConnectionRecord(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private async Task<int> CountDatabaseConnectionsAsync(Guid repositoryId)
    {
        await using SqliteConnection connection = new(_factory.ConnectionString);
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM DatabaseConnections WHERE RepositoryId = $repositoryId;";
        command.Parameters.AddWithValue("$repositoryId", repositoryId.ToString());

        object? scalar = await command.ExecuteScalarAsync();
        Assert.NotNull(scalar);
        return Convert.ToInt32(scalar);
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

    private sealed class RepositoryDetailResponse
    {
        public Guid Id { get; set; }
    }

    private sealed class DatabaseConnectionSummaryResponse
    {
        public Guid RepositoryId { get; set; }
        public int? DatabaseType { get; set; }
        public bool IsConfigured { get; set; }
        public bool HasConnectionString { get; set; }
        public bool HasUsername { get; set; }
        public bool HasPassword { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }

    private sealed class ConnectionTestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private sealed record PersistedDatabaseConnectionRecord(
        Guid RepositoryId,
        string EncryptedConnectionString,
        string? EncryptedUsername,
        string? EncryptedPassword);
}

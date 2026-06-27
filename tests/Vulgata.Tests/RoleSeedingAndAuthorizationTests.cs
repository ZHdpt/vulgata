using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class RoleSeedingAndAuthorizationTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private static readonly string[] SeededRoles = [RoleNames.Administrator, RoleNames.SystemOwner, RoleNames.User];

    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public RoleSeedingAndAuthorizationTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Startup_SeedsExpectedRoles()
    {
        using HttpClient client = CreateClient(_factory);
        HttpResponseMessage startupResponse = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, startupResponse.StatusCode);

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (string roleName in SeededRoles)
        {
            Assert.True(await roleManager.RoleExistsAsync(roleName), $"Expected seeded role '{roleName}' to exist after startup.");
        }
    }

    [Fact]
    public async Task RoleSeeding_IsIdempotentAcrossHostRestarts()
    {
        string databaseDirectory = Path.Combine(Path.GetTempPath(), "vulgata-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(databaseDirectory);

        string databasePath = Path.Combine(databaseDirectory, "role-seeding.db");
        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();

        try
        {
            int firstStartupCount = await StartHostAndCountSeededRolesAsync(connectionString);
            int secondStartupCount = await StartHostAndCountSeededRolesAsync(connectionString);

            Assert.Equal(SeededRoles.Length, firstStartupCount);
            Assert.Equal(SeededRoles.Length, secondStartupCount);
        }
        finally
        {
            if (Directory.Exists(databaseDirectory))
            {
                try
                {
                    Directory.Delete(databaseDirectory, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup for SQLite file locks in test teardown.
                }
            }
        }
    }

    [Fact]
    public async Task UserRole_CannotAccessManagement_SeesAccessDeniedAndNoManagementNav()
    {
        const string email = "roles.user@example.com";
        const string password = "Valid1!Pass";

        await CreateUserWithRoleAsync(email, password, RoleNames.User);

        using HttpClient client = CreateClient(_factory);
        await LoginAsync(client, email, password);

        HttpResponseMessage homeResponse = await client.GetAsync("/");
        string homeHtml = WebUtility.HtmlDecode(await homeResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        Assert.Contains("对话", homeHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("管理后台", homeHtml, StringComparison.Ordinal);

        HttpResponseMessage managementResponse = await client.GetAsync("/management");
        Assert.Equal(HttpStatusCode.Redirect, managementResponse.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(managementResponse), StringComparison.Ordinal);

        HttpResponseMessage deniedResponse = await client.GetAsync("/Account/AccessDenied");
        string deniedHtml = WebUtility.HtmlDecode(await deniedResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, deniedResponse.StatusCode);
        Assert.Contains("访问被拒绝", deniedHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.SystemOwner)]
    public async Task ManagementRoles_CanAccessManagementRoute(string roleName)
    {
        string email = $"roles.{roleName.ToLowerInvariant()}@example.com";
        const string password = "Valid1!Pass";

        await CreateUserWithRoleAsync(email, password, roleName);

        using HttpClient client = CreateClient(_factory);
        await LoginAsync(client, email, password);

        HttpResponseMessage response = await client.GetAsync("/management");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("系统管理", html, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private static async Task<int> StartHostAndCountSeededRolesAsync(string connectionString)
    {
        await using RestartableWebApplicationFactory factory = new(connectionString);
        using HttpClient client = CreateClient(factory);

        HttpResponseMessage startupResponse = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, startupResponse.StatusCode);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext identityDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await identityDb.Roles.CountAsync(role => role.Name != null && SeededRoles.Contains(role.Name));
    }

    private async Task CreateUserWithRoleAsync(string email, string password, string roleName)
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

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, roleName);
        Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        Dictionary<string, string> loginFields = await GetFormFieldsAsync(client, "/Account/Login", "/Account/Login");
        loginFields["Input.Email"] = email;
        loginFields["Input.Password"] = password;
        loginFields["Input.RememberMe"] = "false";

        using FormUrlEncodedContent postData = new(loginFields);
        HttpResponseMessage response = await client.PostAsync("/Account/Login", postData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", GetLocationPath(response));
    }

    private static async Task<Dictionary<string, string>> GetFormFieldsAsync(HttpClient client, string path, string? actionContains = null)
    {
        HttpResponseMessage response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        string html = await response.Content.ReadAsStringAsync();
        return ExtractFormFields(html, actionContains);
    }

    private static Dictionary<string, string> ExtractFormFields(string html, string? actionContains = null)
    {
        Match formMatch = string.IsNullOrWhiteSpace(actionContains)
            ? Regex.Match(
                html,
                "<form[^>]*method=\"post\"[^>]*>(?<content>.*?)</form>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline)
            : Regex.Match(
                html,
                $"<form[^>]*action=\"[^\"]*{Regex.Escape(actionContains)}[^\"]*\"[^>]*>(?<content>.*?)</form>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!formMatch.Success && !string.IsNullOrWhiteSpace(actionContains))
        {
            formMatch = Regex.Match(
                html,
                "<form[^>]*method=\"post\"[^>]*>(?<content>.*?)</form>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        if (!formMatch.Success)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(actionContains)
                ? "No POST form was found in the supplied HTML."
                : $"Form containing action '{actionContains}' was not found.");
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

    private sealed class RestartableWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<VulgataDbContext>>();
                services.RemoveAll<IConfigureOptions<DbContextOptions<ApplicationDbContext>>>();
                services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<VulgataDbContext>();

                services.AddDbContext<ApplicationDbContext>(options =>
                    options
                        .UseSqlite(connectionString, sqliteOptions =>
                            sqliteOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

                services.AddDbContext<VulgataDbContext>(options =>
                    options
                        .UseSqlite(connectionString)
                        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
            });
        }
    }
}

using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed partial class LoginLogoutTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    public LoginLogoutTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private readonly CustomWebApplicationFactory _factory;

    [Fact]
    public void LoginPageSource_UsesFluentInputsAndRemovesOutOfScopeSections()
    {
        string content = ReadRepoFile("src", "dotnet", "Vulgata.Web", "Components", "Account", "Pages", "Login.razor");

        Assert.Contains("<FluentTextField", content, StringComparison.Ordinal);
        Assert.Contains("type=\"email\"", content, StringComparison.Ordinal);
        Assert.Contains("type=\"password\"", content, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"密码\"", content, StringComparison.Ordinal);
        Assert.Contains("邮箱或密码错误", content, StringComparison.Ordinal);
        Assert.DoesNotContain("错误：登录失败，请检查邮箱或密码。", content, StringComparison.Ordinal);
        Assert.DoesNotContain("PasskeySubmit", content, StringComparison.Ordinal);
        Assert.DoesNotContain("PasskeySignInAsync", content, StringComparison.Ordinal);
        Assert.DoesNotContain("PasskeyInputModel", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ExternalLoginPicker", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ResendEmailConfirmation", content, StringComparison.Ordinal);
        Assert.DoesNotContain("form-floating", content, StringComparison.Ordinal);
        Assert.DoesNotContain("form-control", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ProtectedPages_SourceDeclareRequiredAuthorization()
    {
        Assert.Contains("@attribute [Authorize]", ReadRepoFile("src", "dotnet", "Vulgata.Web", "Components", "Pages", "ChatPage.razor"), StringComparison.Ordinal);

        string[] managementPages =
        [
            "DashboardPage.razor",
            "GraphPage.razor",
            "DocumentsPage.razor",
            "ScanHistoryPage.razor",
            "SettingsPage.razor"
        ];

        foreach (string page in managementPages)
        {
            string content = ReadRepoFile("src", "dotnet", "Vulgata.Web", "Components", "Pages", "Management", page);
            Assert.Contains($"@attribute [Authorize(Policy = {nameof(AuthorizationPolicyNames)}.{nameof(AuthorizationPolicyNames.ManagementAccess)})]", content, StringComparison.Ordinal);
        }

        string notFoundContent = ReadRepoFile("src", "dotnet", "Vulgata.Web", "Components", "Pages", "NotFound.razor");
        Assert.DoesNotContain("Authorize", notFoundContent, StringComparison.Ordinal);

        string redirectToLoginContent = ReadRepoFile("src", "dotnet", "Vulgata.Web", "Components", "Account", "Shared", "RedirectToLogin.razor");
        Assert.Contains("Account/Login?returnUrl=", redirectToLoginContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginLink_OnPublicPage_PreservesReturnUrl()
    {
        using HttpClient client = CreateClient();

        string html = await client.GetStringAsync("/not-found");
        string href = ExtractAnchorHref(html, "登录");

        string? returnUrl = GetQueryParameterValue(href, "returnUrl") ?? GetQueryParameterValue(href, "ReturnUrl");

        Assert.NotNull(returnUrl);
        Assert.Equal("/not-found", returnUrl);
    }

    [Fact]
    public async Task Login_WithValidCredentials_RedirectsToChatPage()
    {
        const string email = "login.valid@example.com";
        const string password = "Valid1!Pass";

        await CreateUserAsync(email, password);

        using HttpClient client = CreateClient();
        Dictionary<string, string> loginFields = await GetFormFieldsAsync(client, "/Account/Login");
        loginFields["Input.Email"] = email;
        loginFields["Input.Password"] = password;
        loginFields["Input.RememberMe"] = "false";

        using FormUrlEncodedContent postData = new(loginFields);

        HttpResponseMessage response = await client.PostAsync("/Account/Login", postData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", GetLocationPath(response));

        IEnumerable<string> cookies = response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values
            : [];
        Assert.Contains(cookies, cookie => cookie.Contains(".AspNetCore.Identity.Application", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShowsGenericError()
    {
        const string email = "login.invalid-password@example.com";
        const string password = "Valid1!Pass";

        await CreateUserAsync(email, password);

        using HttpClient client = CreateClient();
        Dictionary<string, string> loginFields = await GetFormFieldsAsync(client, "/Account/Login");
        loginFields["Input.Email"] = email;
        loginFields["Input.Password"] = "Wrong1!Pass";
        loginFields["Input.RememberMe"] = "false";

        using FormUrlEncodedContent postData = new(loginFields);

        HttpResponseMessage response = await client.PostAsync("/Account/Login", postData);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("邮箱或密码错误", html, StringComparison.Ordinal);

        IEnumerable<string> cookies = response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values
            : [];
        Assert.DoesNotContain(cookies, cookie => cookie.Contains(".AspNetCore.Identity.Application", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ShowsSameGenericError()
    {
        using HttpClient client = CreateClient();
        Dictionary<string, string> loginFields = await GetFormFieldsAsync(client, "/Account/Login");
        loginFields["Input.Email"] = "missing.user@example.com";
        loginFields["Input.Password"] = "Wrong1!Pass";
        loginFields["Input.RememberMe"] = "false";

        using FormUrlEncodedContent postData = new(loginFields);

        HttpResponseMessage response = await client.PostAsync("/Account/Login", postData);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("邮箱或密码错误", html, StringComparison.Ordinal);

        IEnumerable<string> cookies = response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values
            : [];
        Assert.DoesNotContain(cookies, cookie => cookie.Contains(".AspNetCore.Identity.Application", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Logout_ClearsCookieAndRedirectsToLogin()
    {
        const string email = "logout.valid@example.com";
        const string password = "Valid1!Pass";

        await CreateUserAsync(email, password);

        using HttpClient client = CreateClient();
        await LoginAsync(client, email, password);

        string homeHtml = await client.GetStringAsync("/");
        Dictionary<string, string> logoutFields = ExtractFormFields(homeHtml, "Account/Logout");

        using FormUrlEncodedContent logoutPost = new(logoutFields);
        HttpResponseMessage logoutResponse = await client.PostAsync("/Account/Logout", logoutPost);

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.Equal("/Account/Login", GetLocationPath(logoutResponse));

        IEnumerable<string> logoutCookies = logoutResponse.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values
            : [];
        Assert.Contains(logoutCookies, cookie => cookie.Contains(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));

        HttpResponseMessage protectedResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, protectedResponse.StatusCode);
        Assert.Contains("Account/Login", protectedResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProtectedRoute_Unauthenticated_RedirectsToLogin()
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        string location = response.Headers.Location?.OriginalString
            ?? throw new InvalidOperationException("Expected a redirect location for unauthenticated access.");
        string? returnUrl = GetQueryParameterValue(location, "returnUrl") ?? GetQueryParameterValue(location, "ReturnUrl");

        Assert.Contains("Account/Login", location, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(returnUrl);
        Assert.True(returnUrl == "/" || returnUrl == "http://localhost/", $"Unexpected returnUrl: {returnUrl}");
    }

    [Fact]
    public async Task Login_WithReturnUrl_RedirectsToOriginalTarget()
    {
        const string email = "login.return-url@example.com";
        const string password = "Valid1!Pass";
        const string returnUrl = "/Account/Manage";

        await CreateUserAsync(email, password);

        using HttpClient client = CreateClient();
        string loginPath = $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        Dictionary<string, string> loginFields = await GetFormFieldsAsync(client, loginPath);
        loginFields["Input.Email"] = email;
        loginFields["Input.Password"] = password;
        loginFields["Input.RememberMe"] = "false";

        using FormUrlEncodedContent postData = new(loginFields);

        HttpResponseMessage response = await client.PostAsync(loginPath, postData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(returnUrl, GetLocationPath(response));
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private async Task CreateUserAsync(string email, string password)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        IdentityResult result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
    }

    private async Task LoginAsync(HttpClient client, string email, string password)
    {
        Dictionary<string, string> loginFields = await GetFormFieldsAsync(client, "/Account/Login");
        loginFields["Input.Email"] = email;
        loginFields["Input.Password"] = password;
        loginFields["Input.RememberMe"] = "false";

        using FormUrlEncodedContent postData = new(loginFields);

        HttpResponseMessage response = await client.PostAsync("/Account/Login", postData);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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

    private static string ExtractAnchorHref(string html, string innerText)
    {
        Match match = Regex.Match(
            html,
            $"<a[^>]*href=\"(?<href>[^\"]+)\"[^>]*>\\s*{Regex.Escape(innerText)}\\s*</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success
            ? WebUtility.HtmlDecode(match.Groups["href"].Value)
            : throw new InvalidOperationException($"Anchor with text '{innerText}' was not found.");
    }

    private static string? GetQueryParameterValue(string uriOrPath, string key)
    {
        Uri uri = Uri.TryCreate(uriOrPath, UriKind.Absolute, out Uri? absoluteUri)
            ? absoluteUri
            : new Uri(new Uri("http://localhost"), uriOrPath);

        return QueryHelpers.ParseQuery(uri.Query).TryGetValue(key, out var value)
            ? value.ToString()
            : null;
    }

    private static string GetLocationPath(HttpResponseMessage response)
    {
        Uri location = response.Headers.Location
            ?? throw new InvalidOperationException("Expected a redirect location header.");

        return location.IsAbsoluteUri
            ? location.PathAndQuery
            : location.OriginalString;
    }

    private static string ReadRepoFile(params string[] segments)
    {
        string filePath = Path.Combine([GetRepoRoot(), .. segments]);
        return File.ReadAllText(filePath);
    }

    private static string GetRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root could not be found. Ensure Vulgata.slnx exists at the solution root.");
    }

    public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseDirectory;

        public CustomWebApplicationFactory()
        {
            _databaseDirectory = Path.Combine(Path.GetTempPath(), "vulgata-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_databaseDirectory);

            string databasePath = Path.Combine(_databaseDirectory, "login-logout-tests.db");
            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                ForeignKeys = true,
            }.ToString();
        }

        public string ConnectionString { get; }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = ConnectionString,
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
                        .UseSqlite(ConnectionString, sqliteOptions =>
                            sqliteOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

                services.AddDbContext<VulgataDbContext>(options =>
                    options
                        .UseSqlite(ConnectionString)
                        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            if (Directory.Exists(_databaseDirectory))
            {
                try
                {
                    Directory.Delete(_databaseDirectory, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup for SQLite file locks in test teardown.
                }
            }
        }
    }
}
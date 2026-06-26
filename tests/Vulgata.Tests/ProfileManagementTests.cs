using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class ProfileManagementTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public ProfileManagementTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProfilePage_LoadsCurrentDisplayNameFallbackAndEmail()
    {
        const string email = "profile.load@example.com";
        const string password = "Valid1!Pass";

        await CreateUserAsync(email, password);

        using HttpClient client = CreateClient();
        await LoginAsync(client, email, password);

        HttpResponseMessage response = await client.GetAsync("/Account/Manage");
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("个人资料", html, StringComparison.Ordinal);
        Assert.True(
            CountOccurrences(html, email) >= 2,
            "Expected the page to render the legacy display-name fallback and the read-only email.");
    }

    [Fact]
    public async Task ProfilePage_Post_UpdatesDisplayName()
    {
        const string email = "profile.display-name@example.com";
        const string password = "Valid1!Pass";
        const string newDisplayName = "新的显示名称";

        string userId = await CreateUserAsync(email, password);

        using HttpClient client = CreateClient();
        await LoginAsync(client, email, password);

        Dictionary<string, string> formFields = await GetFormFieldsAsync(client, "/Account/Manage", "/Account/Manage");
        formFields["Input.DisplayName"] = newDisplayName;

        using FormUrlEncodedContent postData = new(formFields);
        HttpResponseMessage response = await client.PostAsync("/Account/Manage", postData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Manage", GetLocationPath(response));

        string html = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Manage"));
        ApplicationUser user = await FindUserByIdAsync(userId);

        Assert.Equal(newDisplayName, user.DisplayName);
        Assert.Contains("个人资料已更新", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmailPage_Post_UpdatesEmailAndUserName()
    {
        const string originalEmail = "profile.email.original@example.com";
        const string newEmail = "profile.email.updated@example.com";
        const string password = "Valid1!Pass";

        string userId = await CreateUserAsync(originalEmail, password, displayName: "测试用户");

        using HttpClient client = CreateClient();
        await LoginAsync(client, originalEmail, password);

        Dictionary<string, string> formFields = await GetFormFieldsAsync(client, "/Account/Manage/Email", "/Account/Manage/Email");
        formFields["Input.NewEmail"] = newEmail;

        using FormUrlEncodedContent postData = new(formFields);
        HttpResponseMessage response = await client.PostAsync("/Account/Manage/Email", postData);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        ApplicationUser user = await FindUserByIdAsync(userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(newEmail, user.Email);
        Assert.Equal(newEmail, user.UserName);
        Assert.Contains("邮箱已更新", html, StringComparison.Ordinal);

        using HttpClient reloginClient = CreateClient();
        await LoginAsync(reloginClient, newEmail, password);
    }

    [Fact]
    public async Task EmailPage_Post_DuplicateEmailShowsChineseError()
    {
        const string originalEmail = "profile.duplicate.original@example.com";
        const string existingEmail = "profile.duplicate.existing@example.com";
        const string password = "Valid1!Pass";

        string userId = await CreateUserAsync(originalEmail, password);
        await CreateUserAsync(existingEmail, password, displayName: "已存在用户");

        using HttpClient client = CreateClient();
        await LoginAsync(client, originalEmail, password);

        Dictionary<string, string> formFields = await GetFormFieldsAsync(client, "/Account/Manage/Email", "/Account/Manage/Email");
        formFields["Input.NewEmail"] = existingEmail;

        using FormUrlEncodedContent postData = new(formFields);
        HttpResponseMessage response = await client.PostAsync("/Account/Manage/Email", postData);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        ApplicationUser user = await FindUserByIdAsync(userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("该邮箱已被注册", html, StringComparison.Ordinal);
        Assert.Equal(originalEmail, user.Email);
        Assert.Equal(originalEmail, user.UserName);
    }

    [Fact]
    public async Task ChangePassword_WithIncorrectCurrentPassword_ShowsChineseErrorAndKeepsOldPassword()
    {
        const string email = "profile.password.invalid-current@example.com";
        const string password = "Valid1!Pass";
        const string attemptedNewPassword = "Updated1!Pass";

        string userId = await CreateUserAsync(email, password);

        using HttpClient client = CreateClient();
        await LoginAsync(client, email, password);

        Dictionary<string, string> formFields = await GetFormFieldsAsync(client, "/Account/Manage/ChangePassword", "/Account/Manage/ChangePassword");
        formFields["Input.OldPassword"] = "Wrong1!Pass";
        formFields["Input.NewPassword"] = attemptedNewPassword;
        formFields["Input.ConfirmPassword"] = attemptedNewPassword;

        using FormUrlEncodedContent postData = new(formFields);
        HttpResponseMessage response = await client.PostAsync("/Account/Manage/ChangePassword", postData);
        string html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("当前密码不正确", html, StringComparison.Ordinal);
        Assert.True(await CheckPasswordAsync(userId, password));
        Assert.False(await CheckPasswordAsync(userId, attemptedNewPassword));
    }

    [Fact]
    public async Task ChangePassword_WithValidInputs_PreservesSessionAndInvalidatesOldPassword()
    {
        const string email = "profile.password.success@example.com";
        const string oldPassword = "Valid1!Pass";
        const string newPassword = "Updated1!Pass";

        string userId = await CreateUserAsync(email, oldPassword, displayName: "修改密码用户");

        using HttpClient client = CreateClient();
        await LoginAsync(client, email, oldPassword);

        Dictionary<string, string> formFields = await GetFormFieldsAsync(client, "/Account/Manage/ChangePassword", "/Account/Manage/ChangePassword");
        formFields["Input.OldPassword"] = oldPassword;
        formFields["Input.NewPassword"] = newPassword;
        formFields["Input.ConfirmPassword"] = newPassword;

        using FormUrlEncodedContent postData = new(formFields);
        HttpResponseMessage response = await client.PostAsync("/Account/Manage/ChangePassword", postData);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Manage/ChangePassword", GetLocationPath(response));

        string html = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Manage/ChangePassword"));
        Assert.Contains("您的密码已修改", html, StringComparison.Ordinal);

        HttpResponseMessage protectedResponse = await client.GetAsync("/Account/Manage");
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
        Assert.True(await CheckPasswordAsync(userId, newPassword));
        Assert.False(await CheckPasswordAsync(userId, oldPassword));

        using HttpClient reloginClient = CreateClient();
        await LoginAsync(reloginClient, email, newPassword);

        using HttpClient oldPasswordClient = CreateClient();
        HttpResponseMessage oldPasswordResponse = await PostLoginAsync(oldPasswordClient, email, oldPassword);
        string oldPasswordHtml = WebUtility.HtmlDecode(await oldPasswordResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, oldPasswordResponse.StatusCode);
        Assert.Contains("邮箱或密码错误", oldPasswordHtml, StringComparison.Ordinal);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private async Task<string> CreateUserAsync(string email, string password, string? displayName = null)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
        };

        IdentityResult result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        return user.Id;
    }

    private async Task<ApplicationUser> FindUserByIdAsync(string userId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        Assert.NotNull(user);
        return user;
    }

    private async Task<bool> CheckPasswordAsync(string userId, string password)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        Assert.NotNull(user);
        return await userManager.CheckPasswordAsync(user, password);
    }

    private async Task LoginAsync(HttpClient client, string email, string password)
    {
        HttpResponseMessage response = await PostLoginAsync(client, email, password);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", GetLocationPath(response));
    }

    private async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string email, string password)
    {
        Dictionary<string, string> loginFields = await GetFormFieldsAsync(client, "/Account/Login", "/Account/Login");
        loginFields["Input.Email"] = email;
        loginFields["Input.Password"] = password;
        loginFields["Input.RememberMe"] = "false";

        using FormUrlEncodedContent postData = new(loginFields);
        return await client.PostAsync("/Account/Login", postData);
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

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}

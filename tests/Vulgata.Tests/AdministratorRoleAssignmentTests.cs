using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class AdministratorRoleAssignmentTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public AdministratorRoleAssignmentTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_FirstRegisteredUser_BecomesAdministrator()
    {
        await ResetUsersAsync();
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await RegisterAsync(client, "story16.first@example.com", "Valid1!Pass");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", GetLocationPath(response));

        IList<string> roles = await GetUserRolesAsync("story16.first@example.com");

        Assert.Contains(RoleNames.Administrator, roles);
    }

    [Fact]
    public async Task Register_SubsequentUser_DefaultsToUserWhenAdministratorExists()
    {
        await ResetUsersAsync();
        using HttpClient firstClient = CreateClient();
        using HttpClient secondClient = CreateClient();

        await RegisterAsync(firstClient, "story16.bootstrap@example.com", "Valid1!Pass");
        HttpResponseMessage secondResponse = await RegisterAsync(secondClient, "story16.second@example.com", "Valid1!Pass");

        Assert.Equal(HttpStatusCode.Redirect, secondResponse.StatusCode);
        Assert.Equal("/", GetLocationPath(secondResponse));

        IList<string> roles = await GetUserRolesAsync("story16.second@example.com");

        Assert.Contains(RoleNames.User, roles);
        Assert.DoesNotContain(RoleNames.Administrator, roles);
    }

    [Fact]
    public async Task SettingsNavigation_HidesUserManagementForSystemOwner_AndDirectAccessIsDenied()
    {
        await EnsureApplicationStartedAsync();
        await CreateUserWithRolesAsync("story16.owner@example.com", "Valid1!Pass", RoleNames.SystemOwner);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story16.owner@example.com", "Valid1!Pass");

        HttpResponseMessage settingsResponse = await client.GetAsync("/management/settings");
        string settingsHtml = WebUtility.HtmlDecode(await settingsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        Assert.DoesNotContain("/management/settings/users", settingsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("用户管理", settingsHtml, StringComparison.Ordinal);

        HttpResponseMessage userManagementResponse = await client.GetAsync("/management/settings/users");

        Assert.Equal(HttpStatusCode.Redirect, userManagementResponse.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(userManagementResponse), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministratorCanGrantAdministratorRole_AndTargetSessionGainsAccessImmediately()
    {
        await EnsureApplicationStartedAsync();
        string targetUserId = await CreateUserWithRolesAsync("story16.member@example.com", "Valid1!Pass", RoleNames.User);
        await CreateUserWithRolesAsync("story16.admin@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient adminClient = CreateClient();
        using HttpClient targetClient = CreateClient();

        await LoginAsync(adminClient, "story16.admin@example.com", "Valid1!Pass");
        await LoginAsync(targetClient, "story16.member@example.com", "Valid1!Pass");

        HttpResponseMessage beforeGrantResponse = await targetClient.GetAsync("/management/settings/users");
        Assert.Equal(HttpStatusCode.Redirect, beforeGrantResponse.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(beforeGrantResponse), StringComparison.Ordinal);

        HttpResponseMessage adminPageResponse = await adminClient.GetAsync("/management/settings/users");
        string adminPageHtml = WebUtility.HtmlDecode(await adminPageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, adminPageResponse.StatusCode);
        Assert.Contains("用户管理", adminPageHtml, StringComparison.Ordinal);
        Assert.Contains("story16.member@example.com", adminPageHtml, StringComparison.Ordinal);
        Assert.Contains("story16.admin@example.com", adminPageHtml, StringComparison.Ordinal);

        Dictionary<string, string> grantFields = await GetFormFieldsByFormNameAsync(
            adminClient,
            "/management/settings/users",
            $"grant-admin-{targetUserId}");
        HttpResponseMessage grantResponse = await adminClient.PostAsync("/management/settings/users", new FormUrlEncodedContent(grantFields));

        Assert.Equal(HttpStatusCode.Redirect, grantResponse.StatusCode);
        Assert.Equal("/management/settings/users", GetLocationPath(grantResponse));

        HttpResponseMessage refreshedPageResponse = await adminClient.GetAsync("/management/settings/users");
        string refreshedPageHtml = WebUtility.HtmlDecode(await refreshedPageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshedPageResponse.StatusCode);
        Assert.Contains("已授予管理员权限。", refreshedPageHtml, StringComparison.Ordinal);

        IList<string> roles = await GetUserRolesAsync("story16.member@example.com");
        Assert.Contains(RoleNames.Administrator, roles);

        HttpResponseMessage afterGrantResponse = await targetClient.GetAsync("/management/settings/users");
        string targetHtml = WebUtility.HtmlDecode(await afterGrantResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, afterGrantResponse.StatusCode);
        Assert.Contains("用户管理", targetHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministratorCanRemoveAdministratorRole_AndTargetFallsBackToUserImmediately()
    {
        await EnsureApplicationStartedAsync();
        await CreateUserWithRolesAsync("story16.primary-admin@example.com", "Valid1!Pass", RoleNames.Administrator);
        string targetAdminId = await CreateUserWithRolesAsync("story16.secondary-admin@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient actorClient = CreateClient();
        using HttpClient targetClient = CreateClient();

        await LoginAsync(actorClient, "story16.primary-admin@example.com", "Valid1!Pass");
        await LoginAsync(targetClient, "story16.secondary-admin@example.com", "Valid1!Pass");

        HttpResponseMessage beforeRemovalResponse = await targetClient.GetAsync("/management/settings/users");
        Assert.Equal(HttpStatusCode.OK, beforeRemovalResponse.StatusCode);

        Dictionary<string, string> removeFields = await GetFormFieldsByFormNameAsync(
            actorClient,
            "/management/settings/users",
            $"remove-admin-{targetAdminId}");

        HttpResponseMessage removeResponse = await actorClient.PostAsync("/management/settings/users", new FormUrlEncodedContent(removeFields));

        Assert.Equal(HttpStatusCode.Redirect, removeResponse.StatusCode);
        Assert.Equal("/management/settings/users", GetLocationPath(removeResponse));

        IList<string> roles = await GetUserRolesAsync("story16.secondary-admin@example.com");
        Assert.DoesNotContain(RoleNames.Administrator, roles);
        Assert.Contains(RoleNames.User, roles);

        HttpResponseMessage afterRemovalResponse = await targetClient.GetAsync("/management/settings/users");
        Assert.Equal(HttpStatusCode.Redirect, afterRemovalResponse.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(afterRemovalResponse), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovingAdministrator_PreservesSystemOwnerRole()
    {
        await EnsureApplicationStartedAsync();
        await CreateUserWithRolesAsync("story16.remover@example.com", "Valid1!Pass", RoleNames.Administrator);
        string targetId = await CreateUserWithRolesAsync(
            "story16.admin-owner@example.com",
            "Valid1!Pass",
            RoleNames.Administrator,
            RoleNames.SystemOwner);

        using HttpClient actorClient = CreateClient();
        using HttpClient targetClient = CreateClient();

        await LoginAsync(actorClient, "story16.remover@example.com", "Valid1!Pass");
        await LoginAsync(targetClient, "story16.admin-owner@example.com", "Valid1!Pass");

        Dictionary<string, string> removeFields = await GetFormFieldsByFormNameAsync(
            actorClient,
            "/management/settings/users",
            $"remove-admin-{targetId}");

        HttpResponseMessage removeResponse = await actorClient.PostAsync("/management/settings/users", new FormUrlEncodedContent(removeFields));

        Assert.Equal(HttpStatusCode.Redirect, removeResponse.StatusCode);

        IList<string> roles = await GetUserRolesAsync("story16.admin-owner@example.com");
        Assert.DoesNotContain(RoleNames.Administrator, roles);
        Assert.Contains(RoleNames.SystemOwner, roles);

        HttpResponseMessage settingsResponse = await targetClient.GetAsync("/management/settings");
        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);

        HttpResponseMessage userManagementResponse = await targetClient.GetAsync("/management/settings/users");
        Assert.Equal(HttpStatusCode.Redirect, userManagementResponse.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(userManagementResponse), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LastAdministratorRemoval_IsBlocked()
    {
        await EnsureApplicationStartedAsync();
        string onlyAdminId = await CreateUserWithRolesAsync("story16.last-admin@example.com", "Valid1!Pass", RoleNames.Administrator);

        using HttpClient client = CreateClient();
        await LoginAsync(client, "story16.last-admin@example.com", "Valid1!Pass");

        HttpResponseMessage pageResponse = await client.GetAsync("/management/settings/users");
        string pageHtml = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.DoesNotContain($"name=\"UserId\" value=\"{onlyAdminId}\"", pageHtml, StringComparison.Ordinal);

        Dictionary<string, string> postFields = await GetFormFieldsByFormNameAsync(
            client,
            "/management/settings/users",
            "user-management-action");
        postFields["Action"] = "remove";
        postFields["UserId"] = onlyAdminId;

        HttpResponseMessage postResponse = await client.PostAsync("/management/settings/users", new FormUrlEncodedContent(postFields));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal("/management/settings/users", GetLocationPath(postResponse));

        HttpResponseMessage refreshedResponse = await client.GetAsync("/management/settings/users");
        string refreshedHtml = WebUtility.HtmlDecode(await refreshedResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshedResponse.StatusCode);
        Assert.Contains("至少需要保留一名管理员。", refreshedHtml, StringComparison.Ordinal);

        IList<string> roles = await GetUserRolesAsync("story16.last-admin@example.com");
        Assert.Contains(RoleNames.Administrator, roles);
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

    private async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string password)
    {
        Dictionary<string, string> fields = await GetFormFieldsAsync(client, "/Account/Register");
        fields["Input.Email"] = email;
        fields["Input.Password"] = password;
        fields["Input.ConfirmPassword"] = password;

        return await client.PostAsync("/Account/Register", new FormUrlEncodedContent(fields));
    }

    private async Task LoginAsync(HttpClient client, string email, string password)
    {
        Dictionary<string, string> fields = await GetFormFieldsAsync(client, "/Account/Login");
        fields["Input.Email"] = email;
        fields["Input.Password"] = password;
        fields["Input.RememberMe"] = "false";

        HttpResponseMessage response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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

    private async Task ResetUsersAsync()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.UserTokens.RemoveRange(db.UserTokens);
        db.UserClaims.RemoveRange(db.UserClaims);
        db.UserLogins.RemoveRange(db.UserLogins);
        db.UserRoles.RemoveRange(db.UserRoles);
        db.Users.RemoveRange(db.Users);

        await db.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, string>> GetFormFieldsAsync(HttpClient client, string path, string? actionContains = null)
    {
        HttpResponseMessage response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        string html = await response.Content.ReadAsStringAsync();
        return ExtractFormFields(html, actionContains);
    }

    private static async Task<Dictionary<string, string>> GetFormFieldsByFormNameAsync(HttpClient client, string path, string formName)
    {
        HttpResponseMessage response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        string html = await response.Content.ReadAsStringAsync();
        MatchCollection formMatches = Regex.Matches(
            html,
            "<form[^>]*method=\"post\"[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match formMatch in formMatches)
        {
            string formHtml = formMatch.Value;
            string formContent = formMatch.Groups["content"].Value;

            if (FormHasNamedAttribute(formHtml, formName) || FormHasGeneratedHandler(formContent, formName))
            {
                return ExtractInputs(formContent);
            }
        }

        throw new InvalidOperationException($"Form named '{formName}' was not found.");
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
            throw new InvalidOperationException(actionContains is not null
                    ? $"Form containing action '{actionContains}' was not found."
                    : "No POST form was found in the supplied HTML.");
        }

        return ExtractInputs(formMatch.Groups["content"].Value);
    }

    private static Dictionary<string, string> ExtractInputs(string formHtml)
    {
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        MatchCollection inputMatches = Regex.Matches(
            formHtml,
            "<input[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match inputMatch in inputMatches)
        {
            string? name = GetAttributeValue(inputMatch.Value, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string value = GetAttributeValue(inputMatch.Value, "value") ?? string.Empty;
            fields[WebUtility.HtmlDecode(name)] = WebUtility.HtmlDecode(value);
        }

        return fields;
    }

    private static bool FormHasNamedAttribute(string formHtml, string formName) =>
        AttributeContains(formHtml, "formname", formName) || AttributeContains(formHtml, "blazor:formname", formName);

    private static bool FormHasGeneratedHandler(string formHtml, string formName)
    {
        Dictionary<string, string> fields = ExtractInputs(formHtml);
        foreach ((string name, string value) in fields)
        {
            if (name.Contains("handler", StringComparison.OrdinalIgnoreCase)
                && value.Contains(formName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AttributeContains(string html, string attributeName, string expectedValue)
    {
        string? actualValue = GetAttributeValue(html, attributeName);
        return actualValue is not null && actualValue.Contains(expectedValue, StringComparison.Ordinal);
    }

    private static string? GetAttributeValue(string html, string attributeName)
    {
        Match match = Regex.Match(
            html,
            $"(?:^|\\s){Regex.Escape(attributeName)}=\"(?<value>[^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success
            ? match.Groups["value"].Value
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
}

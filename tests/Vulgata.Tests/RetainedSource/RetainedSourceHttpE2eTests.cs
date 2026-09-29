using Vulgata.Tests.TestInfrastructure;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Source;
using Vulgata.Shared;
using Vulgata.Web.Data;

namespace Vulgata.Tests.RetainedSource;

[Collection(RetainedSourceProcessGlobalCollection.Name)]
public sealed class RetainedSourceHttpE2eTests
{
    [Trait("Suite", "E2E")]
    [SuiteFact("E2E")]
    public async Task RepositoryDelete_WhenCaptured_UsesProductionHttpPipelineAndReturnsSanitizedConflict()
    {
        await using RetainedSourceFixture source = await RetainedSourceFixture.CreateAsync();
        await using RetainedSourcePostgreSqlDatabase database =
            await RetainedSourcePostgreSqlDatabase.CreateAsync();

        await using var factory = new RetainedSourcePostgreSqlWebApplicationFactory(
            database.ConnectionString,
            source.SourceStorageRoot);
        using HttpClient authenticated = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        await EnsureApplicationStartedAsync(authenticated);
        string userId = $"retained-source-http-{Guid.NewGuid():N}";
        string loginEmail = $"{userId}@example.invalid";
        Guid systemId = Guid.Parse("13000000-0000-4000-8000-000000000301");
        await SeedAsync(
            factory.Services,
            source,
            userId,
            systemId,
            loginEmail,
            "Valid1!Pass");
        await CaptureAsync(source, factory.Services, userId);
        AssertOwnedSourceComposition(factory, source);
        await LoginAsync(
            authenticated,
            loginEmail,
            "Valid1!Pass");

        string route =
            $"/api/systems/{systemId:D}/repositories/{source.RepositoryId:D}";
        using HttpResponseMessage response = await authenticated.DeleteAsync(route);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("保留", body, StringComparison.Ordinal);
        Assert.DoesNotContain(source.SourceStorageRoot, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(source.Repository.RootPath, body, StringComparison.OrdinalIgnoreCase);
        await AssertCapturedRowsAsync(factory.Services, source.RepositoryId);

        using HttpClient anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        using HttpResponseMessage denied = await anonymous.DeleteAsync(route);
        string deniedBody = await denied.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.DoesNotContain(source.SourceStorageRoot, deniedBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(source.Repository.RootPath, deniedBody, StringComparison.OrdinalIgnoreCase);
        await AssertCapturedRowsAsync(factory.Services, source.RepositoryId);
    }

    private static async Task CaptureAsync(
        RetainedSourceFixture source,
        IServiceProvider services,
        string userId)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        services.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId),
                        new Claim(ClaimTypes.Name, userId),
                        new Claim(ClaimTypes.Role, RoleNames.Administrator),
                    ],
                    authenticationType: "RetainedSourceHttpE2E")),
            };
        SourceCaptureResult result = await scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(
                source.RepositoryId,
                await source.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
    }

    private static async Task SeedAsync(
        IServiceProvider services,
        RetainedSourceFixture source,
        string userId,
        Guid systemId,
        string loginName,
        string password)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UserManager<ApplicationUser> users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = new()
        {
            Id = userId,
            UserName = loginName,
            Email = loginName,
            EmailConfirmed = true,
        };
        IdentityResult created = await users.CreateAsync(user, password);
        Assert.True(
            created.Succeeded,
            string.Join(", ", created.Errors.Select(error => error.Description)));
        IdentityResult role = await users.AddToRoleAsync(user, RoleNames.Administrator);
        Assert.True(
            role.Succeeded,
            string.Join(", ", role.Errors.Select(error => error.Description)));

        VulgataDbContext domain = scope.ServiceProvider
            .GetRequiredService<VulgataDbContext>();
        var system = new Vulgata.Core.Entities.System(
            "retained-source-http-system",
            null,
            null,
            DateTimeOffset.UtcNow);
        SetId(system, systemId);
        Repository repository = system.AddRepository(
            "retained-source-http-repository",
            source.Repository.RootPath,
            null,
            null,
            DateTimeOffset.UtcNow);
        SetId(repository, source.RepositoryId);
        domain.Systems.Add(system);
        domain.Repositories.Add(repository);
        await domain.SaveChangesAsync();
    }

    private static void SetId<T>(T entity, Guid id) =>
        typeof(T).GetProperty("Id")!.SetValue(entity, id);

    private static async Task EnsureApplicationStartedAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        using HttpResponseMessage get = await client.GetAsync("/Account/Login");
        string html = await get.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        Match form = Regex.Match(
            html,
            "<form\\b[^>]*method=\"post\"[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, "The login page did not render a POST form.");

        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(
                     form.Groups["content"].Value,
                     "<input\\b(?<attributes>[^>]*)>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            Match type = Regex.Match(
                input.Groups["attributes"].Value,
                "\\btype=\"(?<value>[^\"]*)\"",
                RegexOptions.IgnoreCase);
            if (!type.Success || !type.Groups["value"].Value.Equals(
                    "hidden",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Match name = Regex.Match(
                input.Groups["attributes"].Value,
                "\\bname=\"(?<value>[^\"]+)\"",
                RegexOptions.IgnoreCase);
            if (!name.Success)
            {
                continue;
            }

            Match value = Regex.Match(
                input.Groups["attributes"].Value,
                "\\bvalue=\"(?<value>[^\"]*)\"",
                RegexOptions.IgnoreCase);
            fields[name.Groups["value"].Value] =
                value.Success ? WebUtility.HtmlDecode(value.Groups["value"].Value) : string.Empty;
        }

        fields["Input.Email"] = email;
        fields["Input.Password"] = password;
        fields["Input.RememberMe"] = "false";
        using FormUrlEncodedContent formContent = new(fields);
        using HttpResponseMessage post = await client.PostAsync("/Account/Login", formContent);
        string postBody = await post.Content.ReadAsStringAsync();
        Assert.True(
            post.StatusCode == HttpStatusCode.Redirect,
            $"Login POST returned {(int)post.StatusCode} {post.StatusCode}: {postBody}");
    }

    private static async Task AssertCapturedRowsAsync(
        IServiceProvider services,
        Guid repositoryId)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        Assert.Equal(
            1,
            await context.SourceSnapshots.CountAsync(snapshot => snapshot.RepositoryId == repositoryId));
        Assert.Equal(
            1,
            await context.Repositories.CountAsync(repository => repository.Id == repositoryId));
    }

    private static void AssertOwnedSourceComposition(
        RetainedSourcePostgreSqlWebApplicationFactory factory,
        RetainedSourceFixture source)
    {
        RetainedSourceCapturePolicy policy = factory.Services
            .GetRequiredService<RetainedSourceCapturePolicy>();
        string expectedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(source.SourceStorageRoot));
        Assert.Equal(expectedRoot, policy.RootPath);
        Assert.True(IsContained(source.RootPath, policy.RootPath));
        Assert.True(IsContained(source.RootPath, factory.OwnerRoot));
        Assert.True(IsContained(factory.OwnerRoot, factory.KeyRingRoot));
        Assert.True(IsContained(factory.OwnerRoot, factory.CodeGraphRoot));
        Assert.True(IsContained(factory.OwnerRoot, factory.JdtLsRoot));

        string[] retainedFiles = Directory.GetFiles(
            policy.RootPath,
            "*",
            SearchOption.AllDirectories);
        Assert.NotEmpty(retainedFiles);
        Assert.Contains(
            retainedFiles,
            path => path.Contains(
                Path.Combine("snapshots", source.RepositoryId.ToString("N")),
                StringComparison.OrdinalIgnoreCase));
        Assert.All(
            retainedFiles,
            path => Assert.True(IsContained(policy.RootPath, path), path));
    }

    private static bool IsContained(string rootPath, string candidatePath)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        string candidate = Path.GetFullPath(candidatePath);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(root, candidate, comparison) ||
               candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }
}
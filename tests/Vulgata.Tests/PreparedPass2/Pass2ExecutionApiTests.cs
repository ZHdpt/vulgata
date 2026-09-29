using Vulgata.Tests.TestInfrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Shared.Scans;
using Vulgata.Web.Data;
using Xunit.Sdk;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// T041 RED: authenticated public HTTP contract for Pass 2 activation and
/// recovery. T043 owns the endpoint implementation and central registration.
/// </summary>
public sealed class Pass2ExecutionApiTests : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public Pass2ExecutionApiTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void Pass2Endpoints_ArePluralAndReachableThroughTheSingleCentralMapper()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EndpointDataSource endpoints = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();
        string[] patterns = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .Where(pattern => pattern.Contains("pass2-executions", StringComparison.Ordinal))
            .OrderBy(pattern => pattern, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "/api/pass2-executions",
                "/api/pass2-executions/{pass2ExecutionId}",
                "/api/pass2-executions/{pass2ExecutionId}/resume",
            ],
            patterns);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PublicPass2Endpoints_Unauthenticated_Return401WithoutRedirectAsync()
    {
        using HttpClient client = CreateClient();
        Guid executionId = Guid.NewGuid();

        using HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/pass2-executions",
            new { pass1PairExecutionId = Guid.NewGuid() });
        using HttpResponseMessage status = await client.GetAsync(
            $"/api/pass2-executions/{executionId:D}");
        using HttpResponseMessage resume = await client.PostAsync(
            $"/api/pass2-executions/{executionId:D}/resume",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, resume.StatusCode);
        Assert.DoesNotContain("Account/Login", create.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task AuthenticatedNonManagementUser_Create_Returns403WithoutScopeDisclosureAsync()
    {
        using AuthenticatedClient authenticated = await CreateAuthenticatedClientAsync();
        HttpClient client = authenticated.Client;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/pass2-executions",
            new { pass1PairExecutionId = Guid.NewGuid() });
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Repository", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SystemId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task AuthenticatedUnknownExecution_StatusAndResume_ReturnOwnershipFiltered404ProblemDetailsAsync()
    {
        using AuthenticatedClient authenticated = await CreateAuthenticatedClientAsync(RoleNames.SystemOwner);
        HttpClient client = authenticated.Client;
        Guid executionId = Guid.NewGuid();

        using HttpResponseMessage status = await client.GetAsync(
            $"/api/pass2-executions/{executionId:D}");
        using HttpResponseMessage resume = await client.PostAsync(
            $"/api/pass2-executions/{executionId:D}/resume",
            content: null);

        await AssertProblemDetailsAsync(status, HttpStatusCode.NotFound);
        await AssertProblemDetailsAsync(resume, HttpStatusCode.NotFound);
        string statusBody = await status.Content.ReadAsStringAsync();
        string resumeBody = await resume.Content.ReadAsStringAsync();
        Assert.DoesNotContain("outside ownership", statusBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Repository", resumeBody, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task Create_MissingPairAndMalformedInput_ReturnValidationProblemDetailsAsync()
    {
        using AuthenticatedClient authenticated = await CreateAuthenticatedClientAsync(RoleNames.SystemOwner);
        HttpClient client = authenticated.Client;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/pass2-executions",
            new { });

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task Create_RejectsCallerSuppliedAuthorityScope_AsSanitizedProblemDetailsAsync()
    {
        using AuthenticatedClient authenticated = await CreateAuthenticatedClientAsync(RoleNames.SystemOwner);
        HttpClient client = authenticated.Client;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/pass2-executions",
            new
            {
                pass1PairExecutionId = Guid.NewGuid(),
                systemId = Guid.NewGuid(),
                repositoryOneId = Guid.NewGuid(),
                repositoryTwoId = Guid.NewGuid(),
                profiledRevisionOne = new string('a', 40),
                profiledRevisionTwo = new string('b', 40),
                providerDocumentVersionId = Guid.NewGuid(),
                sourceRoot = "C:\\secrets\\source",
                providerApiKey = "should-never-be-accepted",
            });

        await AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("should-never-be-accepted", body, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\secrets", body, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ExecutionStateConflict_ReturnsProblemDetailsWithClosedChineseMessageAsync()
    {
        using AuthenticatedClient authenticated = await CreateAuthenticatedClientAsync(RoleNames.SystemOwner);
        HttpClient client = authenticated.Client;
        Guid executionId = Guid.NewGuid();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"/api/pass2-executions/{executionId:D}/resume?expectedStateVersion=999999");
        request.Content = JsonContent.Create(new { expectedStateVersion = 999999 });

        using HttpResponseMessage response = await client.SendAsync(request);

        await AssertProblemDetailsAsync(response, HttpStatusCode.Conflict);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("title", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task StatusResponse_ExposesOnlySanitizedCountsAndWarningsAsync()
    {
        using AuthenticatedClient authenticated = await CreateAuthenticatedClientAsync(RoleNames.SystemOwner);
        HttpClient client = authenticated.Client;

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/pass2-executions/{Guid.NewGuid():D}");

        await AssertProblemDetailsAsync(response, HttpStatusCode.NotFound);
        string body = await response.Content.ReadAsStringAsync();
        foreach (string forbidden in new[]
        {
            "LeaseToken",
            "SourceRoot",
            "ProviderApiKey",
            "ConnectionString",
            "Prompt",
            "ProviderPayload",
            "C:\\",
        })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public void Pass2Api_DoesNotAddStory125ChatOrProductionProgressSurface()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EndpointDataSource endpoints = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();
        string[] pass2Patterns = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .Where(pattern => pattern.Contains("pass2-executions", StringComparison.Ordinal))
            .ToArray();

        Assert.DoesNotContain(pass2Patterns, pattern =>
            pattern.Contains("chat", StringComparison.OrdinalIgnoreCase) ||
            pattern.Contains("progress", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task CreateReplay_IsExactAfterProgressAndTerminalStatus_CandidateCountUsesPlannedDenominatorAsync()
    {
        using AuthenticatedClient authenticated = await CreateAuthenticatedClientAsync(
            RoleNames.SystemOwner,
            disablePass2Worker: true);
        ApiPass2Seed seed = await SeedApiPairAsync(authenticated.UserId, addSecondGroup: true);
        HttpClient client = authenticated.Client;
        object request = new { pass1PairExecutionId = seed.PairExecutionId };

        using HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/pass2-executions",
            request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Pass2ExecutionStartResponse start = await ReadStartAsync(created);
        Assert.False(start.IsReplay);
        Assert.Equal(2, start.JobCount);

        using HttpResponseMessage pendingReplay = await client.PostAsJsonAsync(
            "/api/pass2-executions",
            request);
        Assert.Equal(HttpStatusCode.OK, pendingReplay.StatusCode);
        AssertExactStartReplay(start, await ReadStartAsync(pendingReplay));

        Pass2ExecutionStatusResponse pending = await ReadStatusAsync(client, start.Pass2ExecutionId);
        Assert.Equal(nameof(Pass2ExecutionState.Running), pending.State);
        Assert.Equal(2, pending.CandidateCount);
        Assert.Equal(0, pending.PublishedCount);
        Assert.Equal(2, pending.PendingCount);

        await using VulgataDbContext context = CreateTestContext();
        PreparedPass2Store store = new(context);
        Pass2JobExecution firstJob = await context.Pass2JobExecutions
            .Where(value => value.Pass2ExecutionId == start.Pass2ExecutionId)
            .OrderBy(value => value.GroupOrder)
            .FirstAsync();
        PreparedPass2ClaimResult firstClaim = await store.ClaimNextAsync(
            start.Pass2ExecutionId,
            firstJob.StateChangedAt.AddMinutes(1),
            TimeSpan.FromMinutes(5));
        Guid firstJobId = Assert.IsType<Guid>(firstClaim.JobId);
        Assert.NotNull(firstClaim.LeaseToken);

        Pass2ExecutionStatusResponse running = await ReadStatusAsync(client, start.Pass2ExecutionId);
        Assert.Equal(2, running.CandidateCount);
        Assert.Equal(1, running.RunningCount);
        Assert.Equal(1, running.PendingCount);

        Pass2JobExecution runningJob = await context.Pass2JobExecutions
            .SingleAsync(value => value.Id == firstJobId);
        FlowGroup publishedGroup = await context.FlowGroups
            .SingleAsync(value => value.Id == runningJob.FlowGroupId);
        Guid committedDocumentVersionId = seed.ConsumerDocumentVersionId;
        DateTimeOffset publishedAt = runningJob.StateChangedAt.AddSeconds(1);
        publishedGroup.Publish(committedDocumentVersionId);
        runningJob.Complete(
            runningJob.StateVersion,
            Assert.IsType<Guid>(firstClaim.LeaseToken),
            committedDocumentVersionId,
            new string('f', 64),
            publishedAt);
        Pass2Execution execution = await context.Pass2Executions
            .SingleAsync(value => value.Id == start.Pass2ExecutionId);
        execution.RecordPublished(execution.StateVersion, publishedAt);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        using HttpResponseMessage progressedReplay = await client.PostAsJsonAsync(
            "/api/pass2-executions",
            request);
        Assert.Equal(HttpStatusCode.OK, progressedReplay.StatusCode);
        AssertExactStartReplay(start, await ReadStartAsync(progressedReplay));

        Pass2ExecutionStatusResponse published = await ReadStatusAsync(client, start.Pass2ExecutionId);
        Assert.Equal(2, published.CandidateCount);
        Assert.Equal(1, published.PublishedCount);
        Assert.Equal(1, published.PendingCount);

        Pass2JobExecution secondJob = await context.Pass2JobExecutions
            .Where(value => value.Pass2ExecutionId == start.Pass2ExecutionId)
            .SingleAsync(value => value.State == Pass2JobExecutionState.Pending);
        PreparedPass2ClaimResult secondClaim = await store.ClaimNextAsync(
            start.Pass2ExecutionId,
            secondJob.StateChangedAt.AddMinutes(1),
            TimeSpan.FromMinutes(5));
        Guid secondJobId = Assert.IsType<Guid>(secondClaim.JobId);
        DateTimeOffset retryAt = secondJob.StateChangedAt.AddMinutes(1);
        PreparedPass2DispatchFailureOutcome retry = await store.RecordDispatchFailureAsync(
            start.Pass2ExecutionId,
            secondJobId,
            secondClaim.JobStateVersion,
            Assert.IsType<Guid>(secondClaim.LeaseToken),
            ScanJobFailureCategory.JobPermanent,
            "api-replay-test-retry",
            retryAt);
        Assert.Equal(PreparedPass2DispatchFailureOutcome.RetryScheduled, retry);

        Pass2ExecutionStatusResponse retryScheduled = await ReadStatusAsync(client, start.Pass2ExecutionId);
        Assert.Equal(2, retryScheduled.CandidateCount);
        Assert.Equal(1, retryScheduled.PublishedCount);
        Assert.Equal(1, retryScheduled.RetryScheduledCount);

        PreparedPass2ClaimResult secondAttempt = await store.ClaimNextAsync(
            start.Pass2ExecutionId,
            retryAt.AddSeconds(2),
            TimeSpan.FromMinutes(5));
        PreparedPass2DispatchFailureOutcome failed = await store.RecordDispatchFailureAsync(
            start.Pass2ExecutionId,
            secondJobId,
            secondAttempt.JobStateVersion,
            Assert.IsType<Guid>(secondAttempt.LeaseToken),
            ScanJobFailureCategory.JobPermanent,
            "api-replay-test-failed",
            retryAt.AddSeconds(3));
        Assert.Equal(PreparedPass2DispatchFailureOutcome.Failed, failed);

        using HttpResponseMessage terminalReplay = await client.PostAsJsonAsync(
            "/api/pass2-executions",
            request);
        Assert.Equal(HttpStatusCode.OK, terminalReplay.StatusCode);
        AssertExactStartReplay(start, await ReadStartAsync(terminalReplay));

        Pass2ExecutionStatusResponse terminal = await ReadStatusAsync(client, start.Pass2ExecutionId);
        Assert.Equal(nameof(Pass2ExecutionState.CompletedWithWarnings), terminal.State);
        Assert.Equal(2, terminal.CandidateCount);
        Assert.Equal(1, terminal.PublishedCount);
        Assert.Equal(1, terminal.FailedCount);
        Assert.Equal(1, terminal.Warnings.Count);
    }

    private static async Task<Pass2ExecutionStartResponse> ReadStartAsync(
        HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<Pass2ExecutionStartResponse>() ??
        throw new XunitException("The Pass 2 create response was not readable.");

    private static void AssertExactStartReplay(
        Pass2ExecutionStartResponse original,
        Pass2ExecutionStartResponse replay)
    {
        Assert.Equal(original.Pass2ExecutionId, replay.Pass2ExecutionId);
        Assert.Equal(original.FlowGroupCount, replay.FlowGroupCount);
        Assert.Equal(original.OmissionCount, replay.OmissionCount);
        Assert.Equal(original.JobCount, replay.JobCount);
        Assert.False(original.IsReplay);
        Assert.True(replay.IsReplay);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    private async Task<AuthenticatedClient> CreateAuthenticatedClientAsync(
        string? role = null,
        bool disablePass2Worker = false)
    {
        string email = $"pass2-api-{Guid.NewGuid():N}@example.com";
        const string password = "Valid1!Pass";
        string keyRingPath = Path.Combine(
            Path.GetTempPath(),
            "vulgata-tests",
            $"t041-dataprotection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(keyRingPath);

        WebApplicationFactory<Program> host = _factory.WithWebHostBuilder(builder =>
            builder
                .UseSetting("DataProtection:KeyRingPath", keyRingPath)
                .UseSetting("DataProtection:ApplicationName", "Vulgata.Tests.T041")
                .ConfigureServices(services =>
                {
                    services.AddDataProtection()
                        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                        .SetApplicationName("Vulgata.Tests.T041");

                    if (disablePass2Worker)
                    {
                        foreach (ServiceDescriptor descriptor in services
                            .Where(value => value.ServiceType == typeof(IHostedService) &&
                                value.ImplementationType == typeof(PreparedPass2ExecutionHostedService))
                            .ToArray())
                        {
                            services.Remove(descriptor);
                        }
                    }
                }));

        try
        {
            await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
            {
                UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                IdentityResult result = await userManager.CreateAsync(new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                }, password);
                Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));

                if (role is not null)
                {
                    IdentityResult roleResult = await userManager.AddToRoleAsync(
                        (await userManager.FindByEmailAsync(email))!,
                        role);
                    Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
                }
            }

            HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });
            using HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
            loginPage.EnsureSuccessStatusCode();
            string html = await loginPage.Content.ReadAsStringAsync();
            Dictionary<string, string> fields = ExtractFormFields(html);
            fields["Input.Email"] = email;
            fields["Input.Password"] = password;
            fields["Input.RememberMe"] = "false";

            using FormUrlEncodedContent login = new(fields);
            using HttpResponseMessage response = await client.PostAsync("/Account/Login", login);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            string userId = (await host.Services.CreateAsyncScope().ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>()
                .FindByEmailAsync(email))?.Id
                ?? throw new XunitException("The authenticated test user was not persisted.");
            return new AuthenticatedClient(host, client, keyRingPath, userId);
        }
        catch
        {
            host.Dispose();
            TryDeleteDirectory(keyRingPath);
            throw;
        }
    }

    private static Dictionary<string, string> ExtractFormFields(string html)
    {
        Match form = Regex.Match(
            html,
            "<form\\b(?=[^>]*\\bmethod\\s*=\\s*[\"']post[\"'])[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, "Expected a POST login form.");

        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(
            form.Groups["content"].Value,
            "<input\\b(?<attributes>[^>]*)>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            string attributes = input.Groups["attributes"].Value;
            Match name = Regex.Match(
                attributes,
                "\\bname\\s*=\\s*[\"'](?<value>[^\"']+)[\"']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!name.Success)
            {
                continue;
            }

            Match value = Regex.Match(
                attributes,
                "\\bvalue\\s*=\\s*[\"'](?<value>[^\"']*)[\"']",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            fields[name.Groups["value"].Value] = value.Success
                ? WebUtility.HtmlDecode(value.Groups["value"].Value)
                : string.Empty;
        }

        return fields;
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        params HttpStatusCode[] expectedStatuses)
    {
        Assert.Contains(response.StatusCode, expectedStatuses);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("title", out _));
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ApiPass2Seed> SeedApiPairAsync(string userId, bool addSecondGroup)
    {
        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        await using VulgataDbContext context = CreateTestContext();
        MethodInfo seedMethod = typeof(Pass2FlowPlanningPostgreSqlTests).GetMethod(
            "SeedPass2Async",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The accepted Pass 2 fixture seeder is unavailable.");
        object invocation = seedMethod.Invoke(null, [context, fixture])
            ?? throw new InvalidOperationException("The accepted Pass 2 fixture seeder returned null.");
        Task seedTask = Assert.IsAssignableFrom<Task>(invocation);
        await seedTask;
        object seed = seedTask.GetType().GetProperty("Result")?.GetValue(seedTask)
            ?? throw new InvalidOperationException("The accepted Pass 2 fixture result is unavailable.");

        if (addSecondGroup)
        {
            MethodInfo addDecisions = typeof(Pass2FlowPlanningPostgreSqlTests).GetMethod(
                "AddExactDecisionsAsync",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The accepted Pass 2 decision fixture is unavailable.");
            object addInvocation = addDecisions.Invoke(null, [context, seed, 1])
                ?? throw new InvalidOperationException("The accepted Pass 2 decision fixture returned null.");
            await Assert.IsAssignableFrom<Task>(addInvocation);
        }

        Pass1PairExecution pair = Property<Pass1PairExecution>(seed, "Pair");
        TwoRepositoryScanCoordination coordination = await context
            .TwoRepositoryScanCoordinations
            .SingleAsync(value => value.Id == pair.CoordinationId);
        coordination.BeginPass2(coordination.StateVersion, coordination.UpdatedAt.AddSeconds(1));
        context.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(
            pair.SystemId,
            userId,
            coordination.UpdatedAt));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        ExactPass1DocumentFixture consumer =
            Property<ExactPass1DocumentFixture>(seed, "Consumer");
        ExactPass1DocumentFixture provider =
            Property<ExactPass1DocumentFixture>(seed, "ProviderXml");
        return new(
            pair.Id,
            pair.SystemId,
            consumer.DocumentVersion.Id,
            [consumer.DocumentVersion.RepositoryId, provider.DocumentVersion.RepositoryId]);
    }

    private VulgataDbContext CreateTestContext() => new(
        new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite(_factory.ConnectionString)
            .Options);

    private static T Property<T>(object source, string name) =>
        (T)(source.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(source)
            ?? throw new InvalidOperationException($"Seed property '{name}' is unavailable."));

    private static async Task<Pass2ExecutionStatusResponse> ReadStatusAsync(
        HttpClient client,
        Guid executionId)
    {
        using HttpResponseMessage response = await client.GetAsync(
            $"/api/pass2-executions/{executionId:D}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Pass2ExecutionStatusResponse>()
            ?? throw new XunitException("The Pass 2 status response was not readable.");
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort only; the unique temporary directory is test-owned.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort only; the unique temporary directory is test-owned.
        }
    }

    private sealed class AuthenticatedClient(
        WebApplicationFactory<Program> host,
        HttpClient client,
        string keyRingPath,
        string userId) : IDisposable
    {
        public HttpClient Client { get; } = client;

        public string UserId { get; } = userId;

        public void Dispose()
        {
            Client.Dispose();
            host.Dispose();
            TryDeleteDirectory(keyRingPath);
        }
    }

    private sealed record ApiPass2Seed(
        Guid PairExecutionId,
        Guid SystemId,
        Guid ConsumerDocumentVersionId,
        IReadOnlyList<Guid> RepositoryIds);

    private sealed class AllowAccessVerifier : IRepositoryAuthorizationVerifier
    {
        public ValueTask<bool> HasCurrentAccessAsync(
            IReadOnlyCollection<Guid> requiredRepositoryIds,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }
}

using Vulgata.Tests.TestInfrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.Entities;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Web.Data;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.DurableScanning;

/// <summary>
/// T026 RED contract tests for the SystemOwner manual-resume API.
///
/// These tests intentionally exercise only the HTTP seam.  The endpoint and
/// recovery coordinator are introduced by T027; until then the route returns
/// the observed 404/authorization mismatch and these tests remain RED.
/// </summary>
public sealed class ManualResumeApiTests
    : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private const string Password = "Valid1!Pass";
    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public ManualResumeApiTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Resume_Unauthenticated_Returns401WithoutCookieRedirect()
    {
        SpyRecoveryCoordinator spy = new();
        using WebApplicationFactory<Program> host = CreateConfiguredFactory(spy);
        using HttpClient client = CreateClient(host);

        HttpResponseMessage response = await PostResumeAsync(client, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.DoesNotContain("Account/Login", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(spy.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Resume_AuthenticatedWithoutManagementAccess_Returns403BeforeOwnershipLookup()
    {
        string email = UniqueEmail("forbidden");
        await CreateUserWithRolesAsync(email, RoleNames.User);

        SpyRecoveryCoordinator spy = new();
        using WebApplicationFactory<Program> host = CreateConfiguredFactory(spy);
        using HttpClient client = CreateClient(host);
        await LoginAsync(client, email);

        HttpResponseMessage response = await PostResumeAsync(client, Guid.NewGuid());
        ProblemDetails problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("scan-resume-forbidden", GetProblemCode(problem));
        Assert.Contains("恢复", problem.Detail ?? string.Empty, StringComparison.Ordinal);
        AssertNoSensitiveDetails(problem);
        Assert.Empty(spy.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Resume_MalformedRouteIdentity_Returns400WithBoundedChineseProblem()
    {
        string email = UniqueEmail("malformed");
        await CreateUserWithRolesAsync(email, RoleNames.SystemOwner);

        SpyRecoveryCoordinator spy = new();
        using WebApplicationFactory<Program> host = CreateConfiguredFactory(spy);
        using HttpClient client = CreateClient(host);
        await LoginAsync(client, email);

        HttpResponseMessage response = await client.PostAsync(
            "/api/scan-executions/not-a-guid/resume",
            content: null);
        ProblemDetails problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-scan-execution-id", GetProblemCode(problem));
        Assert.Contains("标识", problem.Detail ?? string.Empty, StringComparison.Ordinal);
        AssertNoSensitiveDetails(problem);
        Assert.Empty(spy.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Resume_EmptyRouteIdentity_Returns400WithSameBoundedContract()
    {
        string email = UniqueEmail("empty");
        await CreateUserWithRolesAsync(email, RoleNames.SystemOwner);

        SpyRecoveryCoordinator spy = new();
        using WebApplicationFactory<Program> host = CreateConfiguredFactory(spy);
        using HttpClient client = CreateClient(host);
        await LoginAsync(client, email);

        HttpResponseMessage response = await client.PostAsync(
            "/api/scan-executions/00000000-0000-0000-0000-000000000000/resume",
            content: null);
        ProblemDetails problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-scan-execution-id", GetProblemCode(problem));
        AssertNoSensitiveDetails(problem);
        Assert.Empty(spy.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Resume_OwnerFilteredMissingAndHiddenExecution_ReturnIdentical404Problem()
    {
        string email = UniqueEmail("owner-hidden");
        string otherOwnerEmail = UniqueEmail("other-owner");
        string ownerUserId = await CreateUserWithRolesAsync(email, RoleNames.SystemOwner);
        string otherOwnerUserId = await CreateUserWithRolesAsync(otherOwnerEmail, RoleNames.SystemOwner);
        Guid hiddenExecutionId = await SeedPausedExecutionAsync(otherOwnerUserId);

        SpyRecoveryCoordinator spy = new();
        using WebApplicationFactory<Program> host = CreateConfiguredFactory(spy);
        using HttpClient client = CreateClient(host);
        await LoginAsync(client, email);

        HttpResponseMessage missingResponse = await PostResumeAsync(client, Guid.NewGuid());
        HttpResponseMessage hiddenResponse = await PostResumeAsync(client, hiddenExecutionId);
        ProblemDetails missing = await ReadProblemAsync(missingResponse);
        ProblemDetails hidden = await ReadProblemAsync(hiddenResponse);

        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);
        Assert.Equal(await ReadBodyAsync(missingResponse), await ReadBodyAsync(hiddenResponse));
        Assert.Equal("scan-execution-not-found", GetProblemCode(missing));
        AssertNoSensitiveDetails(missing);
        Assert.Empty(spy.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Resume_AuthorizedOwner_ReachesRecoveryAndReturnsBoundedSuccessOrRecoveryProblem()
    {
        string email = UniqueEmail("owner");
        string ownerUserId = await CreateUserWithRolesAsync(email, RoleNames.SystemOwner);
        Guid executionId = await SeedPausedExecutionAsync(ownerUserId);

        SpyRecoveryCoordinator spy = new()
        {
            ResponseFactory = SpyRecoveryCoordinator.Applied,
        };
        using WebApplicationFactory<Program> host = CreateConfiguredFactory(spy);
        using HttpClient client = CreateClient(host);
        await LoginAsync(client, email);

        HttpResponseMessage response = await PostResumeAsync(client, executionId);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Worker", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Single(spy.Calls);
        Assert.Equal(executionId, spy.Calls[0].ScanExecutionId);
        Assert.NotEmpty(spy.Calls[0].AuthorizationScope.OwnedSystemIds);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DurableScanStoreOutcome.Conflict, DurableScanReason.StaleStateVersion, 409, "scan-resume-conflict")]
    [InlineData(DurableScanStoreOutcome.Rejected, DurableScanReason.IntegrityViolation, 422, "scan-resume-integrity-rejected")]
    [InlineData(DurableScanStoreOutcome.Rejected, DurableScanReason.ProviderUnavailable, 503, "scan-provider-unavailable")]
    [InlineData(DurableScanStoreOutcome.Rejected, DurableScanReason.Failed, 503, "analysis-runtime-not-ready")]
    public async Task Resume_RecoveryOutcome_UsesExactBoundedProblemDetails(
        DurableScanStoreOutcome outcome,
        DurableScanReason reason,
        int expectedStatus,
        string expectedCode)
    {
        string email = UniqueEmail("failure");
        string ownerUserId = await CreateUserWithRolesAsync(email, RoleNames.SystemOwner);
        Guid executionId = await SeedPausedExecutionAsync(ownerUserId);

        SpyRecoveryCoordinator spy = new()
        {
            Response = new ScanRecoveryResult(outcome, reason, 8),
        };
        using WebApplicationFactory<Program> host = CreateConfiguredFactory(spy);
        using HttpClient client = CreateClient(host);
        await LoginAsync(client, email);

        HttpResponseMessage response = await PostResumeAsync(client, executionId);

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        ProblemDetails problem = await ReadProblemAsync(response);
        Assert.Equal(expectedCode, GetProblemCode(problem));
        AssertNoSensitiveDetails(problem);
        Assert.Single(spy.Calls);
        Assert.Equal(executionId, spy.Calls[0].ScanExecutionId);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Resume_Replay_IsIdempotentAndExposesOnlyBoundedResponseFields()
    {
        string email = UniqueEmail("replay");
        string ownerUserId = await CreateUserWithRolesAsync(email, RoleNames.SystemOwner);
        Guid scanExecutionId = await SeedPausedExecutionAsync(ownerUserId);

        SpyRecoveryCoordinator spy = new()
        {
            ResponseFactory = SpyRecoveryCoordinator.Replay,
        };
        using WebApplicationFactory<Program> host = CreateConfiguredFactory(spy);
        using HttpClient client = CreateClient(host);
        await LoginAsync(client, email);

        HttpResponseMessage first = await PostResumeAsync(client, scanExecutionId);
        HttpResponseMessage replay = await PostResumeAsync(client, scanExecutionId);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        ResumeResponse firstDto = await ReadRequiredAsync<ResumeResponse>(first);
        ResumeResponse replayDto = await ReadRequiredAsync<ResumeResponse>(replay);
        Assert.Equal(firstDto, replayDto);
        Assert.Equal(scanExecutionId, firstDto.ScanExecutionId);
        Assert.False(firstDto.StateChanged);
        Assert.InRange(firstDto.PendingJobCount, 0, int.MaxValue);
        Assert.InRange(firstDto.RunningJobCount, 0, int.MaxValue);
        Assert.InRange(firstDto.RetryScheduledJobCount, 0, int.MaxValue);
        Assert.InRange(firstDto.CompletedJobCount, 0, int.MaxValue);
        Assert.InRange(firstDto.FailedJobCount, 0, int.MaxValue);
        Assert.InRange(firstDto.CancelledJobCount, 0, int.MaxValue);
        Assert.InRange(firstDto.RecoveredExpiredLeaseCount, 0, int.MaxValue);
        Assert.InRange(firstDto.ReleasedDueRetryCount, 0, int.MaxValue);
        Assert.Equal(2, spy.Calls.Count);
        Assert.All(spy.Calls, call => Assert.Equal(scanExecutionId, call.ScanExecutionId));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Resume_DoesNotAcceptCallerSuppliedStateOrRuntimeFields()
    {
        string email = UniqueEmail("input-boundary");
        await CreateUserWithRolesAsync(email, RoleNames.SystemOwner);

        SpyRecoveryCoordinator spy = new();
        using WebApplicationFactory<Program> host = CreateConfiguredFactory(spy);
        using HttpClient client = CreateClient(host);
        await LoginAsync(client, email);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/scan-executions/{Guid.NewGuid():D}/resume");
        request.Content = JsonContent.Create(new
        {
            phase = "Completed",
            retryAt = DateTimeOffset.UtcNow.AddYears(10),
            leaseToken = "caller-supplied",
            path = "C:\\secret\\source",
            profiledRevision = "caller-supplied",
            provider = "caller-supplied",
            executable = "caller-supplied",
            runtime = "caller-supplied",
        });

        HttpResponseMessage response = await client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("C:\\secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("caller-supplied", body, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(spy.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Resume_SuccessContract_DoesNotDispatchWorkerOrInitializeAnalysisRuntime()
    {
        string email = UniqueEmail("no-dispatch");
        string ownerUserId = await CreateUserWithRolesAsync(email, RoleNames.SystemOwner);
        Guid executionId = await SeedPausedExecutionAsync(ownerUserId);

        SpyRecoveryCoordinator spy = new();
        using WebApplicationFactory<Program> host = CreateConfiguredFactory(spy);
        using HttpClient client = CreateClient(host);
        await LoginAsync(client, email);

        HttpResponseMessage response = await PostResumeAsync(client, executionId);
        string body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("worker", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("codegraph", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jdt", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("providerSecret", body, StringComparison.OrdinalIgnoreCase);
        Assert.Single(spy.Calls);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private WebApplicationFactory<Program> CreateConfiguredFactory(SpyRecoveryCoordinator spy) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDurableScanStore>();
                services.AddScoped<IDurableScanStore, DurableScanRepository>();
                services.RemoveAll<IScanRecoveryCoordinator>();
                services.AddSingleton<IScanRecoveryCoordinator>(spy);
            }));

    private async Task<string> CreateUserWithRolesAsync(string email, params string[] roles)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        IdentityResult createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));

        foreach (string role in roles.Distinct(StringComparer.Ordinal))
        {
            IdentityResult roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Description)));
        }

        return user.Id;
    }

    private async Task<Guid> SeedPausedExecutionAsync(string ownerUserId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        VulgataDbContext db = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string revision = new('a', 40);
        Guid executionId = Guid.NewGuid();
        string uniqueSuffix = executionId.ToString("N");
        SystemEntity system = new($"手工恢复测试系统-{uniqueSuffix}", null, null, now);
        Repository repository = system.AddRepository(
            $"手工恢复测试仓库-{uniqueSuffix}",
            $"https://example.invalid/story12-1-resume-{uniqueSuffix}.git",
            null,
            null,
            now);
        ScanRun scanRun = ScanRun.Start(repository.Id, 30, now);
        scanRun.BindProfiledRevision(revision);
        ScanExecution execution = ScanExecution.CreateNewPath(
            executionId,
            repository.Id,
            system.Id,
            scanRun.Id,
            revision,
            "prompt-v1",
            "document-v1",
            "{\"clientVersion\":\"test\"}",
            new('b', 64),
            now);
        execution.BeginRunning(0, now.AddSeconds(1));
        execution.PauseProviderUnavailable(1, "PROVIDER_UNAVAILABLE", now.AddSeconds(2));

        db.Systems.Add(system);
        db.Repositories.Add(repository);
        db.ScanRuns.Add(scanRun);
        db.ScanExecutions.Add(execution);
        db.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(system.Id, ownerUserId, now));
        await db.SaveChangesAsync();
        return executionId;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
        string html = await loginPage.Content.ReadAsStringAsync();
        Dictionary<string, string> fields = ExtractFormFields(html);
        fields["Input.Email"] = email;
        fields["Input.Password"] = Password;
        fields["Input.RememberMe"] = "false";

        using var content = new FormUrlEncodedContent(fields);
        using HttpResponseMessage response = await client.PostAsync("/Account/Login", content);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostResumeAsync(HttpClient client, Guid id) =>
        await client.PostAsync($"/api/scan-executions/{id:D}/resume", content: null);

    private static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response)
    {
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        return problem ?? throw new Xunit.Sdk.XunitException(
            $"Expected ProblemDetails but received {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response)
    {
        T? value = await response.Content.ReadFromJsonAsync<T>();
        return value ?? throw new Xunit.Sdk.XunitException(
            $"Expected {typeof(T).Name} but received {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync();

    private static void AssertNoSensitiveDetails(ProblemDetails problem)
    {
        string serialized = JsonSerializer.Serialize(problem);
        Assert.DoesNotContain("Exception", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stderr", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/var/", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/home/", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetProblemCode(ProblemDetails problem) =>
        problem.Extensions.TryGetValue("code", out object? code)
            ? code?.ToString()
            : problem.Extensions.TryGetValue("reasonCode", out object? reasonCode)
                ? reasonCode?.ToString()
                : problem.Type;

    private static Dictionary<string, string> ExtractFormFields(string html)
    {
        Match formMatch = Regex.Match(
            html,
            "<form[^>]*method=\"post\"[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!formMatch.Success)
        {
            throw new Xunit.Sdk.XunitException("No POST form was rendered on the login page.");
        }

        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        MatchCollection inputMatches = Regex.Matches(
            formMatch.Groups["content"].Value,
            "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match inputMatch in inputMatches)
        {
            fields[WebUtility.HtmlDecode(inputMatch.Groups["name"].Value)] =
                WebUtility.HtmlDecode(inputMatch.Groups["value"].Value);
        }

        return fields;
    }

    private static string UniqueEmail(string purpose) =>
        $"story121.resume.{purpose}.{Guid.NewGuid():N}@example.com";

    private sealed record ResumeResponse(
        Guid ScanExecutionId,
        string Phase,
        int PendingJobCount,
        int RunningJobCount,
        int RetryScheduledJobCount,
        int CompletedJobCount,
        int FailedJobCount,
        int CancelledJobCount,
        int RecoveredExpiredLeaseCount,
        int ReleasedDueRetryCount,
        bool StateChanged,
        string? ReasonCode);

    /// <summary>
    /// Test-only adapter for the accepted recovery seam.  The implementation
    /// must enrich ScanRecoveryResult with the accepted bounded
    /// ScanRecoveryView and IsReplay members so the API can return the exact
    /// ResumeResponse shape without querying unfiltered state or initializing
    /// runtime/Worker services.  The proposed view is:
    /// <c>ScanRecoveryView(ScanExecutionId, Phase, TotalJobCount,
    /// CompletedJobCount, PendingJobCount, RunningJobCount,
    /// RetryScheduledJobCount, FailedJobCount, CancelledJobCount,
    /// RecoveredExpiredLeaseCount, ReleasedDueRetryCount, StateChanged,
    /// Reason)</c>, carried by <c>ScanRecoveryResult.View</c> with
    /// <c>ScanRecoveryResult.IsReplay</c>.
    /// </summary>
    private sealed class SpyRecoveryCoordinator : IScanRecoveryCoordinator
    {
        public ScanRecoveryResult Response { get; set; } =
            new(DurableScanStoreOutcome.Applied, DurableScanReason.None, 1);

        public Func<Guid, ScanRecoveryResult>? ResponseFactory { get; set; }

        public static ScanRecoveryResult Applied(Guid scanExecutionId) =>
            new(
                DurableScanStoreOutcome.Applied,
                DurableScanReason.None,
                8,
                CreateView(scanExecutionId, stateChanged: true, reason: DurableScanReason.None));

        public static ScanRecoveryResult Replay(Guid scanExecutionId) =>
            new(
                DurableScanStoreOutcome.Replay,
                DurableScanReason.None,
                9,
                CreateView(scanExecutionId, stateChanged: false, reason: DurableScanReason.None),
                IsReplay: true);

        public List<RecoveryCall> Calls { get; } = [];

        public Task<ScanRecoveryResult> ResumeAsync(
            Guid scanExecutionId,
            DurableScanAuthorizationScope authorizationScope,
            CancellationToken cancellationToken)
        {
            Calls.Add(new RecoveryCall(scanExecutionId, authorizationScope));
            ScanRecoveryResult response = ResponseFactory?.Invoke(scanExecutionId) ?? Response;
            if ((response.Outcome is DurableScanStoreOutcome.Applied or DurableScanStoreOutcome.Replay) &&
                response.View is null)
            {
                response = response with
                {
                    View = CreateView(
                        scanExecutionId,
                        response.Outcome == DurableScanStoreOutcome.Applied,
                        response.Reason),
                    IsReplay = response.Outcome == DurableScanStoreOutcome.Replay,
                };
            }

            return Task.FromResult(response);
        }

        private static ScanRecoveryView CreateView(
            Guid scanExecutionId,
            bool stateChanged,
            DurableScanReason reason) =>
            new(
                scanExecutionId,
                ScanExecutionPhase.Prepared,
                TotalJobCount: 0,
                CompletedJobCount: 0,
                PendingJobCount: 0,
                RunningJobCount: 0,
                RetryScheduledJobCount: 0,
                FailedJobCount: 0,
                CancelledJobCount: 0,
                RecoveredExpiredLeaseCount: 0,
                ReleasedDueRetryCount: 0,
                StateChanged: stateChanged,
                Reason: reason);
    }

    private sealed record RecoveryCall(
        Guid ScanExecutionId,
        DurableScanAuthorizationScope AuthorizationScope);
}

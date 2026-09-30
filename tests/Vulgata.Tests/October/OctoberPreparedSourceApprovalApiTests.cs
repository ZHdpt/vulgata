using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October;

public sealed class OctoberPreparedSourceApprovalApiTests
{
    private const string TargetId = "october-target-approval-a";
    private const string AlternateTargetId = "october-target-approval-b";
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ManagementAuthorityId = "management-authority-approval-a";
    private const string CanonicalDatabaseIdentity = "postgres://test-cluster/october-target-approval-a";
    private const string RootBindingIdentity = "prepared-root-approval-v1";
    private const string OperatorLabel = "approval-fixture-operator";
    private const string GlobalText = "trusted global advisory";
    private const string RepositoryText = "trusted Repository advisory";
    private static readonly Guid BatchId = Guid.Parse("a5e086cb-14ad-48d7-9cea-59877c1f2e21");
    private static readonly Guid RepositoryId = Guid.Parse("d36746c8-337a-4131-912b-51dc4fe38d32");
    private static readonly Guid GlobalContextId = Guid.Parse("fd167469-c007-4be4-bd7f-49c1f93ac7c1");

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task AuthorizationOnlyProgram_DoesNotExposePreparedSourceApprovalRoutesAsync()
    {
        await using ApprovalHost host = await ApprovalHost.CreateAsync(
            mapPreparedApprovalEndpoints: false,
            OctoberCertificateIdentityStatus.Authenticated);
        string[] routes = host.Factory.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("/api/october-prepared-source-approvals", routes);
        Assert.DoesNotContain("/api/october-prepared-source-approvals/verifications", routes);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task IssueAndVerify_CurrentGrantAndBoundBatch_ReturnExactApprovalTupleAsync()
    {
        await using ApprovalHost host = await ApprovalHost.CreateAuthorizedSelectionAsync();

        using HttpResponseMessage issued = await host.Client.PostAsJsonAsync(
            "/api/october-prepared-source-approvals", CreateIssueRequest(host.ActivationGrant));
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        using JsonDocument issuedBody = JsonDocument.Parse(await issued.Content.ReadAsByteArrayAsync());
        JsonElement issuedTuple = issuedBody.RootElement;
        string approvalIdentity = issuedTuple.GetProperty("approvalIdentity").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(approvalIdentity));
        Assert.Equal(BatchId, issuedTuple.GetProperty("batchId").GetGuid());
        Assert.Equal(RepositoryId, issuedTuple.GetProperty("repositoryId").GetGuid());
        Assert.Equal(host.SubjectId, issuedTuple.GetProperty("subjectId").GetString());
        Assert.Equal((int)PreparedSourcePurpose.WorkerSourceRead,
            issuedTuple.GetProperty("purpose").GetInt32());
        Assert.Equal(RootBindingIdentity, issuedTuple.GetProperty("rootBindingIdentity").GetString());
        Assert.Equal(OperatorLabel, issuedTuple.GetProperty("operatorLabel").GetString());
        Assert.True(issuedTuple.GetProperty("expiresAtUtc").GetDateTimeOffset() > host.Clock.GetUtcNow());
        string responseBody = issuedBody.RootElement.ToString();
        Assert.DoesNotContain("activationGrant", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("physicalRootPath", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(host.ActivationGrant, responseBody, StringComparison.Ordinal);

        using HttpResponseMessage verification = await host.Client.PostAsJsonAsync(
            "/api/october-prepared-source-approvals/verifications",
            CreateVerificationRequest(host.SubjectId, approvalIdentity));
        Assert.Equal(HttpStatusCode.OK, verification.StatusCode);
        using JsonDocument verifiedBody = JsonDocument.Parse(await verification.Content.ReadAsByteArrayAsync());
        Assert.Equal("current", verifiedBody.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, verifiedBody.RootElement.EnumerateObject().Count());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("target")]
    [InlineData("batch")]
    [InlineData("repository")]
    [InlineData("subject")]
    [InlineData("purpose")]
    [InlineData("approval")]
    [InlineData("root-binding")]
    [InlineData("operator-label")]
    public async Task Verify_ChangedApprovalTuple_IsDeniedWithoutSensitiveDiagnosticsAsync(string changedField)
    {
        await using ApprovalHost host = await ApprovalHost.CreateAuthorizedSelectionAsync();
        string approvalIdentity = await host.IssuePreparedSourceApprovalAsync();
        VerificationRequest request = CreateVerificationRequest(host.SubjectId, approvalIdentity);
        request = changedField switch
        {
            "target" => request with { TargetId = AlternateTargetId },
            "batch" => request with { BatchId = Guid.Parse("1de2b9a1-cf3a-4d92-bc7a-449d9ab573cb") },
            "repository" => request with { RepositoryId = Guid.Parse("12ce0775-9dd0-4270-adf9-1ab8e3202d6b") },
            "subject" => request with { SubjectId = "foreign-subject-fixture" },
            "purpose" => request with { Purpose = (PreparedSourcePurpose)99 },
            "approval" => request with { ApprovalIdentity = "foreign-approval-fixture" },
            "root-binding" => request with { RootBindingIdentity = "prepared-root-approval-v2" },
            "operator-label" => request with { OperatorLabel = "foreign-operator-label" },
            _ => throw new ArgumentOutOfRangeException(nameof(changedField), changedField, null),
        };

        using HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/api/october-prepared-source-approvals/verifications", request);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden,
            host.ActivationGrant, RootBindingIdentity, "foreign-subject-fixture");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Verify_ExpiredApproval_IsDeniedAsync()
    {
        await using ApprovalHost host = await ApprovalHost.CreateAuthorizedSelectionAsync();
        string approvalIdentity = await host.IssuePreparedSourceApprovalAsync();
        host.Clock.Advance(TimeSpan.FromMinutes(30));

        using HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/api/october-prepared-source-approvals/verifications",
            CreateVerificationRequest(host.SubjectId, approvalIdentity));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden,
            host.ActivationGrant, RootBindingIdentity);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Verify_RoleRevokedAfterIssuance_IsDeniedAsync()
    {
        await using ApprovalHost host = await ApprovalHost.CreateAuthorizedSelectionAsync();
        string approvalIdentity = await host.IssuePreparedSourceApprovalAsync();
        await host.RemoveAdministratorRoleAsync();

        using HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/api/october-prepared-source-approvals/verifications",
            CreateVerificationRequest(host.SubjectId, approvalIdentity));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden,
            host.ActivationGrant, RootBindingIdentity);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Verify_RepositoryMembershipRemovedAfterIssuance_IsDeniedAsync()
    {
        await using ApprovalHost host = await ApprovalHost.CreateAuthorizedSelectionAsync();
        string approvalIdentity = await host.IssuePreparedSourceApprovalAsync();
        await host.RemoveRepositoryAsync();

        using HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/api/october-prepared-source-approvals/verifications",
            CreateVerificationRequest(host.SubjectId, approvalIdentity));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden,
            host.ActivationGrant, RootBindingIdentity);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Verify_RootBindingRotationAfterIssuance_IsDeniedAsync()
    {
        await using ApprovalHost host = await ApprovalHost.CreateAuthorizedSelectionAsync();
        string approvalIdentity = await host.IssuePreparedSourceApprovalAsync();
        await host.RotateStoredRootBindingAsync();

        using HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/api/october-prepared-source-approvals/verifications",
            CreateVerificationRequest(host.SubjectId, approvalIdentity));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden,
            host.ActivationGrant, RootBindingIdentity);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("/api/october-prepared-source-approvals", false)]
    [InlineData("/api/october-prepared-source-approvals/verifications", true)]
    public async Task ApprovalRoutes_MapCertificateDecisionsToSanitizedApiStatusesAsync(
        string path,
        bool verification)
    {
        (OctoberCertificateIdentityStatus SourceStatus, string? Fingerprint, HttpStatusCode Expected)[] decisions =
        [
            (OctoberCertificateIdentityStatus.Unauthenticated, null, HttpStatusCode.Unauthorized),
            (OctoberCertificateIdentityStatus.Authenticated,
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", HttpStatusCode.Forbidden),
            (OctoberCertificateIdentityStatus.AuthorityUnavailable, null, HttpStatusCode.ServiceUnavailable),
            (OctoberCertificateIdentityStatus.Cancelled, null, HttpStatusCode.ServiceUnavailable),
        ];

        foreach ((OctoberCertificateIdentityStatus status, string? fingerprint, HttpStatusCode expected) in decisions)
        {
            await using ApprovalHost host = await ApprovalHost.CreateAsync(
                mapPreparedApprovalEndpoints: true,
                status,
                fingerprint);
            HttpResponseMessage response = verification
                ? await host.Client.PostAsJsonAsync(path,
                    CreateVerificationRequest("operator-subject-fixture", "approval-identity-fixture"))
                : await host.Client.PostAsJsonAsync(path,
                    CreateIssueRequest("opaque-activation-grant-fixture"));
            using (response)
            {
                Assert.Equal(expected, response.StatusCode);
                Assert.Equal(1, host.CertificateSource.ReadCount);
                await AssertProblemAsync(response, expected, "opaque-activation-grant-fixture",
                    "approval-identity-fixture", "physicalRootPath");
            }
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("/api/october-prepared-source-approvals")]
    [InlineData("/api/october-prepared-source-approvals/verifications")]
    public async Task ApprovalRoutes_RejectUnknownPathAndTokenFieldsAsClosedDtoAsync(string path)
    {
        await using ApprovalHost host = await ApprovalHost.CreateAsync(
            mapPreparedApprovalEndpoints: true,
            OctoberCertificateIdentityStatus.Authenticated,
            Fingerprint);
        using HttpResponseMessage response = path.EndsWith("/verifications", StringComparison.Ordinal)
            ? await host.Client.PostAsJsonAsync(path, new
            {
                targetId = TargetId,
                batchId = BatchId,
                repositoryId = RepositoryId,
                subjectId = "operator-subject-fixture",
                purpose = (int)PreparedSourcePurpose.WorkerSourceRead,
                approvalIdentity = "approval-fixture",
                rootBindingIdentity = RootBindingIdentity,
                operatorLabel = OperatorLabel,
                activationGrant = "must-not-be-a-verification-field",
                physicalRootPath = "must-never-be-sent",
            })
            : await host.Client.PostAsJsonAsync(path, new
            {
                activationGrant = "opaque-activation-grant-fixture",
                targetId = TargetId,
                batchId = BatchId,
                repositoryId = RepositoryId,
                rootBindingIdentity = RootBindingIdentity,
                physicalRootPath = "must-never-be-sent",
            });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest,
            "must-never-be-sent", "must-not-be-a-verification-field");
        Assert.Equal(0, host.CertificateSource.ReadCount);
    }

    private static ApprovalIssueRequest CreateIssueRequest(string activationGrant) => new(
        activationGrant, TargetId, BatchId, RepositoryId, RootBindingIdentity);

    private static VerificationRequest CreateVerificationRequest(string subjectId, string approvalIdentity) => new(
        TargetId,
        BatchId,
        RepositoryId,
        subjectId,
        PreparedSourcePurpose.WorkerSourceRead,
        approvalIdentity,
        RootBindingIdentity,
        OperatorLabel);

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        params string[] forbiddenText)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument problem = JsonDocument.Parse(body);
        Assert.Equal((int)expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
        foreach (string forbidden in forbiddenText)
        {
            if (!string.IsNullOrEmpty(forbidden))
                Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record ApprovalIssueRequest(
        string ActivationGrant,
        string TargetId,
        Guid BatchId,
        Guid RepositoryId,
        string RootBindingIdentity);

    private sealed record VerificationRequest(
        string TargetId,
        Guid BatchId,
        Guid RepositoryId,
        string SubjectId,
        PreparedSourcePurpose Purpose,
        string ApprovalIdentity,
        string RootBindingIdentity,
        string OperatorLabel);

    private sealed record TestBoundSetupData(string TargetId, Guid BatchId) : IOctoberBoundSetupData;

    private sealed class ControlledCertificateSource(
        OctoberCertificateIdentityStatus status,
        string? fingerprint) : IOctoberAuthenticatedCertificateSource
    {
        public int ReadCount { get; private set; }

        public Task<OctoberCertificateIdentityResult> ReadAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(new OctoberCertificateIdentityResult(status, fingerprint));
        }
    }

    private sealed class PreparedSourceApprovalStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(OctoberPreparedSourceApprovalEndpoints.Map);
        };
    }

    private sealed class ApprovalHost : IAsyncDisposable
    {
        private readonly LoginLogoutTests.CustomWebApplicationFactory _baseFactory;
        private readonly SqliteConnection _octoberConnection;

        private ApprovalHost(
            LoginLogoutTests.CustomWebApplicationFactory baseFactory,
            WebApplicationFactory<Program> factory,
            HttpClient client,
            ControlledCertificateSource certificateSource,
            MutableTimeProvider clock,
            SqliteConnection octoberConnection)
        {
            _baseFactory = baseFactory;
            Factory = factory;
            Client = client;
            CertificateSource = certificateSource;
            Clock = clock;
            _octoberConnection = octoberConnection;
        }

        public WebApplicationFactory<Program> Factory { get; }

        public HttpClient Client { get; }

        public ControlledCertificateSource CertificateSource { get; }

        public MutableTimeProvider Clock { get; }

        public string SubjectId { get; private set; } = string.Empty;

        public string ActivationGrant { get; private set; } = string.Empty;

        public string SelectionDigest { get; private set; } = string.Empty;

        public static async Task<ApprovalHost> CreateAsync(
            bool mapPreparedApprovalEndpoints,
            OctoberCertificateIdentityStatus certificateStatus,
            string? fingerprint = null,
            MutableTimeProvider? clock = null)
        {
            LoginLogoutTests.CustomWebApplicationFactory baseFactory = new();
            ControlledCertificateSource source = new(certificateStatus, fingerprint);
            MutableTimeProvider testClock = clock ?? new MutableTimeProvider(
                new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
            SqliteConnection octoberConnection = new("Data Source=:memory:");
            await octoberConnection.OpenAsync();
            WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(CreateOctoberConfiguration()));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOctoberAuthenticatedCertificateSource>();
                    services.AddSingleton<IOctoberAuthenticatedCertificateSource>(source);
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(testClock);
                    if (mapPreparedApprovalEndpoints)
                    {
                        services.AddDbContext<OctoberDbContext>(options =>
                            options.UseSqlite(octoberConnection));
                        services.AddSingleton<IOctoberBoundSetupData>(
                            new TestBoundSetupData(TargetId, BatchId));
                        services.AddScoped<OctoberPreparedSourceApprovalService>();
                        services.AddTransient<IStartupFilter, PreparedSourceApprovalStartupFilter>();
                    }
                });
            });

            HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });
            ApprovalHost host = new(baseFactory, factory, client, source, testClock, octoberConnection);
            return host;
        }

        public static async Task<ApprovalHost> CreateAuthorizedSelectionAsync()
        {
            ApprovalHost host = await CreateAsync(
                mapPreparedApprovalEndpoints: true,
                OctoberCertificateIdentityStatus.Authenticated,
                Fingerprint);
            try
            {
                host.SubjectId = await host.CreateManagementAdministratorAsync();
                await LoginAsync(host.Client);
                await host.SeedOrdinarySelectionAsync();
                (host.ActivationGrant, host.SelectionDigest) = await host.IssueActivationGrantAsync();
                await host.SeedBoundBatchAsync();
                return host;
            }
            catch
            {
                await host.DisposeAsync();
                throw;
            }
        }

        public HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        public async Task<string> IssuePreparedSourceApprovalAsync()
        {
            using HttpResponseMessage response = await Client.PostAsJsonAsync(
                "/api/october-prepared-source-approvals", CreateIssueRequest(ActivationGrant));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
            return body.RootElement.GetProperty("approvalIdentity").GetString()!;
        }

        public async Task RemoveAdministratorRoleAsync()
        {
            await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
            UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = await users.FindByIdAsync(SubjectId) ??
                throw new InvalidOperationException("The controlled operator fixture was not found.");
            IdentityResult removed = await users.RemoveFromRoleAsync(user, RoleNames.Administrator);
            Assert.True(removed.Succeeded, string.Join("; ", removed.Errors.Select(error => error.Description)));
        }

        public async Task RemoveRepositoryAsync()
        {
            await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
            VulgataDbContext database = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            Repository repository = await database.Repositories.SingleAsync(value => value.Id == RepositoryId);
            database.Repositories.Remove(repository);
            await database.SaveChangesAsync();
        }

        public async Task RotateStoredRootBindingAsync()
        {
            await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
            OctoberDbContext database = scope.ServiceProvider.GetRequiredService<OctoberDbContext>();
            OctoberRepositoryRecord binding = await database.RepositorySelections.SingleAsync(value =>
                value.BatchId == BatchId && value.RepositoryId == RepositoryId);
            binding.RootBindingIdentity = "prepared-root-approval-v2";
            await database.SaveChangesAsync();
        }

        private async Task<string> CreateManagementAdministratorAsync()
        {
            await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
            UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = new()
            {
                Id = "october-approval-subject-" + Guid.NewGuid().ToString("N"),
                UserName = "october.approval@example.test",
                Email = "october.approval@example.test",
                EmailConfirmed = true,
            };
            IdentityResult created = await userManager.CreateAsync(user, "Valid1!Pass");
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
            IdentityResult assigned = await userManager.AddToRoleAsync(user, RoleNames.Administrator);
            Assert.True(assigned.Succeeded, string.Join("; ", assigned.Errors.Select(error => error.Description)));
            return user.Id;
        }

        private async Task SeedOrdinarySelectionAsync()
        {
            DateTimeOffset now = Clock.GetUtcNow();
            Repository repository = Repository.CreateStandalone(
                "October approval fixture",
                "https://example.test/october-approval-fixture.git",
                null,
                RepositoryText,
                now);
            GlobalContext global = new(GlobalText, now);
            await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
            VulgataDbContext database = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            database.Entry(repository).Property(nameof(Repository.Id)).CurrentValue = RepositoryId;
            database.Entry(global).Property(nameof(GlobalContext.Id)).CurrentValue = GlobalContextId;
            database.Repositories.Add(repository);
            database.GlobalContexts.Add(global);
            await database.SaveChangesAsync();
        }

        private async Task<(string Grant, string Digest)> IssueActivationGrantAsync()
        {
            object[] repositories =
            [
                new
                {
                    repositoryId = RepositoryId,
                    systemId = (Guid?)null,
                    rootBindingIdentity = RootBindingIdentity,
                    operatorLabel = OperatorLabel,
                },
            ];
            object[] contextSelections =
            [
                new { scope = "global", recordId = GlobalContextId },
                new { scope = "repository", recordId = RepositoryId },
            ];
            object request = new
            {
                targetId = TargetId,
                batchId = BatchId,
                repositories,
                contextSelections,
            };
            using HttpResponseMessage response = await Client.PostAsJsonAsync(
                "/api/october-batch-activation-grants", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
            return (
                body.RootElement.GetProperty("activationGrant").GetString()!,
                body.RootElement.GetProperty("selectionDigest").GetString()!);
        }

        private async Task SeedBoundBatchAsync()
        {
            OctoberRepositorySelection repository = new(
                RepositoryId, null, RootBindingIdentity, OperatorLabel);
            OctoberSetupRequest request = new(
                TargetId,
                BatchId,
                [repository],
                [
                    new OctoberContextSelection(OctoberContextScope.Global, GlobalContextId),
                    new OctoberContextSelection(OctoberContextScope.Repository, RepositoryId),
                ],
                SelectionDigest);
            OctoberSelectedAdvisoryContext context = new(
                ManagementAuthorityId,
                ImmutableArray.Create(repository),
                ImmutableArray.Create(
                    new OctoberAdvisoryContextEntry(RepositoryId, OctoberContextScope.Global,
                        OctoberContextApplicability.Selected, GlobalContextId, GlobalText,
                        OctoberContextEvidenceKind.AdvisoryNotSourceProof),
                    new OctoberAdvisoryContextEntry(RepositoryId, OctoberContextScope.System,
                        OctoberContextApplicability.NotApplicable, null, null,
                        OctoberContextEvidenceKind.AdvisoryNotSourceProof),
                    new OctoberAdvisoryContextEntry(RepositoryId, OctoberContextScope.Repository,
                        OctoberContextApplicability.Selected, RepositoryId, RepositoryText,
                        OctoberContextEvidenceKind.AdvisoryNotSourceProof)),
                ImmutableArray.Create(new OctoberContextDifference(
                    RepositoryId, OctoberContextScope.Global, OctoberContextScope.Repository)));
            Assert.Equal(SelectionDigest, OctoberSelectionDigest.Compute(request, context));
            OctoberAuthorizedSelection authority = new(
                TargetId,
                BatchId,
                SubjectId,
                CanonicalDatabaseIdentity,
                [RepositoryId],
                SelectionDigest,
                context);

            await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
            OctoberDbContext database = scope.ServiceProvider.GetRequiredService<OctoberDbContext>();
            await database.Database.EnsureCreatedAsync();
            IOctoberSetupWriter writer = new OctoberSetupStore(database, Clock);
            OctoberPrepareResult created = await writer.CommitAsync(request, authority);
            Assert.Equal(OctoberPrepareStatus.Created, created.Status);
        }

        private static async Task LoginAsync(HttpClient client)
        {
            Dictionary<string, string> fields = await ReadLoginFormAsync(client);
            fields["Input.Email"] = "october.approval@example.test";
            fields["Input.Password"] = "Valid1!Pass";
            fields["Input.RememberMe"] = "false";
            using HttpResponseMessage response = await client.PostAsync(
                "/Account/Login", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            Dictionary<string, string> authenticatedFields = await ReadLoginFormAsync(client);
            string token = authenticatedFields.Single(key =>
                key.Key.Contains("RequestVerificationToken", StringComparison.Ordinal)).Value;
            client.DefaultRequestHeaders.Remove("RequestVerificationToken");
            client.DefaultRequestHeaders.TryAddWithoutValidation("RequestVerificationToken", token);
        }

        private static async Task<Dictionary<string, string>> ReadLoginFormAsync(HttpClient client)
        {
            using HttpResponseMessage page = await client.GetAsync("/Account/Login");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            string html = await page.Content.ReadAsStringAsync();
            Match form = Regex.Match(html,
                "<form\\b(?=[^>]*method=\"post\")[^>]*>(?<content>.*?)</form>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Assert.True(form.Success, "The real Identity login form supplies the activation grant's authenticated subject.");
            Dictionary<string, string> values = new(StringComparer.Ordinal);
            foreach (Match input in Regex.Matches(form.Groups["content"].Value, "<input\\b[^>]*>",
                         RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                string name = Regex.Match(input.Value, "(?:^|\\s)name=\"(?<value>[^\"]*)\"",
                    RegexOptions.IgnoreCase).Groups["value"].Value;
                string value = Regex.Match(input.Value, "(?:^|\\s)value=\"(?<value>[^\"]*)\"",
                    RegexOptions.IgnoreCase).Groups["value"].Value;
                if (!string.IsNullOrWhiteSpace(name))
                    values[WebUtility.HtmlDecode(name)] = WebUtility.HtmlDecode(value);
            }

            Assert.Contains(values.Keys, key => key.Contains("RequestVerificationToken", StringComparison.Ordinal));
            return values;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            Factory.Dispose();
            _baseFactory.Dispose();
            await _octoberConnection.DisposeAsync();
            await Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset _current = current;

        public override DateTimeOffset GetUtcNow() => _current;

        public void Advance(TimeSpan amount) => _current = _current.Add(amount);
    }

    private static Dictionary<string, string?> CreateOctoberConfiguration() => new(StringComparer.Ordinal)
    {
        ["October:Web:Mode"] = "authorization-only",
        ["October:Web:ManagementAuthorityId"] = ManagementAuthorityId,
        ["October:Web:AllowedTargets:0:TargetId"] = TargetId,
        ["October:Web:AllowedTargets:0:CanonicalDatabaseIdentity"] = CanonicalDatabaseIdentity,
        ["October:Web:AllowedTargets:1:TargetId"] = AlternateTargetId,
        ["October:Web:AllowedTargets:1:CanonicalDatabaseIdentity"] = "postgres://test-cluster/october-target-approval-b",
        ["October:Web:GeneratorCertificates:0:Sha256Fingerprint"] = Fingerprint,
        ["October:Web:GeneratorCertificates:0:TargetIds:0"] = TargetId,
        ["October:Web:GeneratorCertificates:0:TargetIds:1"] = AlternateTargetId,
    };

}

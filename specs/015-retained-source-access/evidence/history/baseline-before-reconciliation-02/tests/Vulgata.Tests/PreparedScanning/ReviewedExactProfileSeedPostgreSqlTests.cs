using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Git;
using Vulgata.Shared;
using Vulgata.Shared.Scans;
using Vulgata.Tests.PreparedPass1;
using Vulgata.Tests.PreparedScanning.TestDoubles;
using Vulgata.Web.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T039 RED: the exact reviewed pair must be entered through the production
/// endpoint with no pre-created scan lifecycle or content rows. Only bounded
/// deterministic analysis ports are replaced; no database repair or direct
/// coordinator invocation is permitted by this test.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class ReviewedExactProfileSeedPostgreSqlTests
{
    [Fact]
    public async Task PostPreparedScan_MatchedProfile_PublishesRequiredSeedsAndExactEvidenceAsync()
    {
        await using PreparedScanInitiationPostgreSqlFixture database =
            await PreparedScanInitiationPostgreSqlFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        await ApplyExistingSchemaAsync(database);
        (SystemEntity system, RepositoryEntity first, RepositoryEntity second) =
            await SeedProfilePairAsync(database, profile);
        using WebApplicationFactory<Program> factory = CreateFactory(
            database.ConnectionString,
            profile,
            new Dictionary<Guid, string>
            {
                [first.Id] = profile.Repositories[0].Alias,
                [second.Id] = profile.Repositories[1].Alias,
            },
            out T039PairBuilderDiagnostics diagnostics);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser administrator = new()
            {
                UserName = "story125.t039.admin@example.com",
                Email = "story125.t039.admin@example.com",
                EmailConfirmed = true,
            };
            IdentityResult created = await users.CreateAsync(administrator, "Valid1!Pass");
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
            IdentityResult role = await users.AddToRoleAsync(administrator, RoleNames.Administrator);
            Assert.True(role.Succeeded, string.Join(", ", role.Errors.Select(error => error.Description)));
        }

        Assert.Equal(
            new T039Counts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            await ReadCountsAsync(database));

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        await LoginAsync(client);
        PreparedScanRequest request = new()
        {
            Repositories = profile.Repositories
                .Select(repository => repository.Alias == profile.Repositories[0].Alias
                    ? new PreparedScanRepositoryRevisionBinding
                    {
                        RepositoryId = first.Id,
                        GitRef = repository.ExpectedRef,
                        ProfiledRevision = repository.ExpectedCommit,
                    }
                    : new PreparedScanRepositoryRevisionBinding
                    {
                        RepositoryId = second.Id,
                        GitRef = repository.ExpectedRef,
                        ProfiledRevision = repository.ExpectedCommit,
                    })
                .ToArray(),
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/systems/{system.Id:D}/prepared-scans", request);
        string body = await response.Content.ReadAsStringAsync();
        T039Counts counts = await ReadCountsAsync(database);

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Matched reviewed profile did not reach the prepared handoff. " +
            $"Status={(int)response.StatusCode}, counts={counts}, body={body}, " +
            $"pair-builder={diagnostics.Exception}");
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(2, document.RootElement.GetProperty("manifests").GetArrayLength());
        Assert.Equal(2, counts.Manifests);
        Assert.True(counts.CodeUnits >= 50, $"Expected bounded selected units, got {counts.CodeUnits}.");
        Assert.Equal(4, counts.HighValueSelected);
        Assert.Equal(0, counts.Pass1PairExecutions);
        Assert.Equal(0, counts.ExactDecisions);
        Assert.Equal(0, counts.ExactEdges);
        Assert.Equal(0, counts.Uncertainties);
    }

    [Fact]
    public async Task PostPreparedScan_DoesNotStartPass1OrLeakLaterStagesAsync()
    {
        await using PreparedScanInitiationPostgreSqlFixture database =
            await PreparedScanInitiationPostgreSqlFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        await ApplyExistingSchemaAsync(database);
        (SystemEntity system, RepositoryEntity first, RepositoryEntity second) =
            await SeedProfilePairAsync(database, profile);
        using WebApplicationFactory<Program> factory = CreateFactory(
            database.ConnectionString,
            profile,
            new Dictionary<Guid, string>
            {
                [first.Id] = profile.Repositories[0].Alias,
                [second.Id] = profile.Repositories[1].Alias,
            },
            out T039PairBuilderDiagnostics diagnostics);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser administrator = new()
            {
                UserName = "story125.t039.failure@example.com",
                Email = "story125.t039.failure@example.com",
                EmailConfirmed = true,
            };
            IdentityResult created = await users.CreateAsync(administrator, "Valid1!Pass");
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
            IdentityResult role = await users.AddToRoleAsync(administrator, RoleNames.Administrator);
            Assert.True(role.Succeeded, string.Join(", ", role.Errors.Select(error => error.Description)));
        }

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        await LoginAsync(client, "story125.t039.failure@example.com");
        PreparedScanRequest request = CreateRequest(profile, first.Id, second.Id);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/systems/{system.Id:D}/prepared-scans", request);
        string body = await response.Content.ReadAsStringAsync();
        T039Counts counts = await ReadCountsAsync(database);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);
        Assert.Equal(
            2,
            counts.Manifests);
        Assert.Equal(
            0,
            counts.Pass1PairExecutions);
        Assert.Equal(
            0,
            counts.ExactDecisions);
        Assert.Equal(
            0,
            counts.ExactEdges);
        Assert.Equal(
            0,
            counts.Uncertainties);
        Assert.True(
            counts.FailedPass1 == 0,
            $"Prepared Scan must not create FailedPass1 before explicit Pass 1 start; " +
            $"status={(int)response.StatusCode}, counts={counts}, body={body}, " +
            $"pair-builder={diagnostics.Exception}");
    }

    [Fact]
    public async Task PostPreparedScan_MatchedSemanticOwnerFailsBeforePublication_AndNonmatchingInputRemainsByteNeutralAsync()
    {
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();

        await using (PreparedScanInitiationPostgreSqlFixture database =
                     await PreparedScanInitiationPostgreSqlFixture.CreateAsync())
        {
            await ApplyExistingSchemaAsync(database);
            (SystemEntity system, RepositoryEntity first, RepositoryEntity second) =
                await SeedProfilePairAsync(database, profile);
            using WebApplicationFactory<Program> factory = CreateFactory(
                database.ConnectionString,
                profile,
                new Dictionary<Guid, string>
                {
                    [first.Id] = profile.Repositories[0].Alias,
                    [second.Id] = profile.Repositories[1].Alias,
                },
                out T039PairBuilderDiagnostics diagnostics);
            await SeedAdministratorAsync(
                factory,
                "story125.t046.nonmatch@example.com",
                system.Id);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });
            await LoginAsync(client, "story125.t046.nonmatch@example.com");

            PreparedScanRequest nonmatching = CreateRequest(profile, first.Id, second.Id);
            PreparedScanRepositoryRevisionBinding original = nonmatching.Repositories[0];
            nonmatching = new()
            {
                Repositories =
                [
                    new()
                    {
                        RepositoryId = original.RepositoryId,
                        GitRef = original.GitRef,
                        ProfiledRevision = new string('f', 40),
                    },
                    nonmatching.Repositories[1],
                ],
            };

            using HttpResponseMessage response = await client.PostAsJsonAsync(
                $"/api/systems/{system.Id:D}/prepared-scans",
                nonmatching);
            string body = await response.Content.ReadAsStringAsync();
            Assert.True(
                response.StatusCode == HttpStatusCode.Created,
                $"A nonmatching profile must retain generic preparation; status={(int)response.StatusCode}, " +
                $"body={body}, pair-builder={diagnostics.Exception}");

            await using VulgataDbContext context = database.CreateContext();
            var pathUnits = await context.CodeUnits.AsNoTracking()
                .Select(unit => new { unit.FilePath, unit.SymbolName, unit.FileCodeUnitIdentityFingerprint })
                .ToArrayAsync();
            Assert.NotEmpty(pathUnits);
            Assert.All(pathUnits, unit =>
            {
                Assert.Equal(unit.FilePath, unit.SymbolName);
                Assert.DoesNotContain(
                    profile.UnsupportedObservations.Select(value => value.OwnerDocument.Symbol),
                    symbol => StringComparer.Ordinal.Equals(symbol, unit.SymbolName));
            });
            Assert.All(pathUnits, unit => Assert.NotNull(unit.FileCodeUnitIdentityFingerprint));
        }

        await using (PreparedScanInitiationPostgreSqlFixture database =
                     await PreparedScanInitiationPostgreSqlFixture.CreateAsync())
        {
            await ApplyExistingSchemaAsync(database);
            (SystemEntity system, RepositoryEntity first, RepositoryEntity second) =
                await SeedProfilePairAsync(database, profile);
            using WebApplicationFactory<Program> factory = CreateFactory(
                database.ConnectionString,
                profile,
                new Dictionary<Guid, string>
                {
                    [first.Id] = profile.Repositories[0].Alias,
                    [second.Id] = profile.Repositories[1].Alias,
                },
                out T039PairBuilderDiagnostics diagnostics);
            await SeedAdministratorAsync(
                factory,
                "story125.t046.matched@example.com",
                system.Id);

            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });
            await LoginAsync(client, "story125.t046.matched@example.com");
            T039Counts before = await ReadCountsAsync(database);
            Assert.Equal(
                new T039Counts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                before);
            using HttpResponseMessage response = await client.PostAsJsonAsync(
                $"/api/systems/{system.Id:D}/prepared-scans",
                CreateRequest(profile, first.Id, second.Id));
            string body = await response.Content.ReadAsStringAsync();
            T039Counts after = await ReadCountsAsync(database);

            Assert.True(
                response.StatusCode == HttpStatusCode.UnprocessableEntity,
                $"Matched semantic-owner evidence must fail closed before publication; " +
                $"status={(int)response.StatusCode}, before={before}, after={after}, body={body}, " +
                $"pair-builder={diagnostics.Exception}");
            Assert.Equal(
                new T039Counts(2, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0),
                after);
        }
    }

    [Fact]
    public async Task PostPreparedScan_ThenPublicPass1_ExactResolutionOwnerSymbolMismatch_IsPreciselyCharacterizedAsync()
    {
        await using PreparedScanInitiationPostgreSqlFixture database =
            await PreparedScanInitiationPostgreSqlFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        await ApplyExistingSchemaAsync(database);
        (SystemEntity system, RepositoryEntity first, RepositoryEntity second) =
            await SeedProfilePairAsync(database, profile);
        string firstRoot = Path.Combine(Path.GetTempPath(), "vulgata-story125-t043", Guid.NewGuid().ToString("N"));
        string secondRoot = Path.Combine(Path.GetTempPath(), "vulgata-story125-t043", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        try
        {
            WriteProfileSources(firstRoot, profile, profile.Repositories[0].Alias, first.Id);
            WriteProfileSources(secondRoot, profile, profile.Repositories[1].Alias, second.Id);
            using WebApplicationFactory<Program> factory = CreateVerticalFactory(
                database.ConnectionString,
                profile,
                new Dictionary<Guid, string>
                {
                    [first.Id] = profile.Repositories[0].Alias,
                    [second.Id] = profile.Repositories[1].Alias,
                },
                new Dictionary<Guid, string>
                {
                    [first.Id] = firstRoot,
                    [second.Id] = secondRoot,
                },
                rejectWorker: false,
                out T039PairBuilderDiagnostics diagnostics);
            await SeedAdministratorAsync(
                factory,
                "story125.t043.admin@example.com",
                system.Id);
            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });
            await LoginAsync(client, "story125.t043.admin@example.com");

            PreparedScanRequest request = CreateRequest(profile, first.Id, second.Id);
            using HttpResponseMessage prepared = await client.PostAsJsonAsync(
                $"/api/systems/{system.Id:D}/prepared-scans", request);
            string preparedBody = await prepared.Content.ReadAsStringAsync();
            Assert.True(
                prepared.StatusCode == HttpStatusCode.Created,
                $"T043 Prepared Scan did not publish: status={(int)prepared.StatusCode}, body={preparedBody}, pair-builder={diagnostics.Exception}");
            Guid[] manifests = JsonDocument.Parse(preparedBody).RootElement
                .GetProperty("manifests").EnumerateArray()
                .Select(value => value.GetProperty("manifestId").GetGuid())
                .ToArray();
            Assert.Equal(2, manifests.Length);

            using HttpResponseMessage started = await client.PostAsJsonAsync(
                "/api/pass1-executions",
                new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 2 });
            string startBody = await started.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
            Pass1ExecutionStartResponse start =
                (await started.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
            Pass1ExecutionStatusResponse terminal = await WaitForT043TerminalAsync(
                client,
                start.Pass1ExecutionId,
                TimeSpan.FromSeconds(90),
                database);
            string pass1Diagnostics = await ReadT043Pass1DiagnosticsAsync(database);
            Assert.True(
                terminal.Phase == "ReadyForResolution",
                $"T043 Pass 1 terminal phase was {terminal.Phase}; warnings={JsonSerializer.Serialize(terminal.Warnings)}; durable={pass1Diagnostics}");
            Assert.True(
                terminal.CompletedCount > 0 && terminal.CompletedCount == terminal.DispatchedCount,
                $"T043 Pass 1 did not complete all bounded jobs: {JsonSerializer.Serialize(terminal)}; start={startBody}");

            ExactResolutionRuntimeRequest runtime = await CreateRuntimeRequestAsync(
                database,
                profile,
                first.Id,
                second.Id);
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            ApplicationUser administrator = await scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>()
                .FindByEmailAsync("story125.t043.admin@example.com") ??
                throw new Xunit.Sdk.XunitException("T043 administrator was not persisted.");
            IHttpContextAccessor accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, administrator.Id),
                    new Claim(ClaimTypes.Role, RoleNames.Administrator),
                ], "T043")),
            };
            IRepositoryAuthorizationVerifier authorization = scope.ServiceProvider
                .GetRequiredService<IRepositoryAuthorizationVerifier>();
            string exactDiagnostics = await ReadT043ExactDiagnosticsAsync(
                scope.ServiceProvider,
                profile,
                runtime,
                firstRoot,
                secondRoot);
            ExactResolutionOutcome applied = await scope.ServiceProvider
                .GetRequiredService<ExactCrossRepositoryResolutionCoordinator>()
                .ResolveAfterPass1Async(
                    runtime,
                    new RepositoryAccessScope([first.Id, second.Id], authorization));
            Assert.Equal(ExactResolutionOutcomeKind.Refused, applied.Kind);
            Assert.Equal("refused", applied.Code);
            Assert.Contains("\"Consumers\":1,\"Providers\":1", exactDiagnostics, StringComparison.Ordinal);
            Assert.Contains(
                "\"SymbolName\":\"src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java\"",
                exactDiagnostics,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"Symbol\":\"cn.com.yitong.actions.cust.UploadOcrToOssOp\"",
                exactDiagnostics,
                StringComparison.Ordinal);

            await using VulgataDbContext verification = database.CreateContext();
            Assert.Equal(0, await verification.CrossRepositoryResolutionDecisions.CountAsync());
            Assert.Equal(0, await verification.DocumentEdges.CountAsync(edge => edge.EdgeType == DocumentEdgeType.References));
            Assert.Equal(0, await verification.Uncertainties.CountAsync());
            Assert.Equal(0, await verification.Pass2Executions.CountAsync());
        }
        finally
        {
            DeleteT043Root(firstRoot);
            DeleteT043Root(secondRoot);
        }
    }

    [Fact]
    public async Task PostPreparedScan_ThenRejectedPass1_PersistsFailedPass1WithoutExactActivationAsync()
    {
        await using PreparedScanInitiationPostgreSqlFixture database =
            await PreparedScanInitiationPostgreSqlFixture.CreateAsync();
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        await ApplyExistingSchemaAsync(database);
        (SystemEntity system, RepositoryEntity first, RepositoryEntity second) =
            await SeedProfilePairAsync(database, profile);
        string firstRoot = Path.Combine(Path.GetTempPath(), "vulgata-story125-t043", Guid.NewGuid().ToString("N"));
        string secondRoot = Path.Combine(Path.GetTempPath(), "vulgata-story125-t043", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        try
        {
            WriteProfileSources(firstRoot, profile, profile.Repositories[0].Alias, first.Id);
            WriteProfileSources(secondRoot, profile, profile.Repositories[1].Alias, second.Id);
            using WebApplicationFactory<Program> factory = CreateVerticalFactory(
                database.ConnectionString,
                profile,
                new Dictionary<Guid, string>
                {
                    [first.Id] = profile.Repositories[0].Alias,
                    [second.Id] = profile.Repositories[1].Alias,
                },
                new Dictionary<Guid, string>
                {
                    [first.Id] = firstRoot,
                    [second.Id] = secondRoot,
                },
                rejectWorker: true,
                out T039PairBuilderDiagnostics diagnostics);
            await SeedAdministratorAsync(
                factory,
                "story125.t043.failure@example.com",
                system.Id);
            using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });
            await LoginAsync(client, "story125.t043.failure@example.com");
            using HttpResponseMessage prepared = await client.PostAsJsonAsync(
                $"/api/systems/{system.Id:D}/prepared-scans",
                CreateRequest(profile, first.Id, second.Id));
            string preparedBody = await prepared.Content.ReadAsStringAsync();
            Assert.True(prepared.StatusCode == HttpStatusCode.Created, preparedBody + diagnostics.Exception);
            Guid[] manifests = JsonDocument.Parse(preparedBody).RootElement
                .GetProperty("manifests").EnumerateArray()
                .Select(value => value.GetProperty("manifestId").GetGuid())
                .ToArray();
            using HttpResponseMessage started = await client.PostAsJsonAsync(
                "/api/pass1-executions",
                new { firstManifestId = manifests[0], secondManifestId = manifests[1], concurrencyLimit = 1 });
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
            Pass1ExecutionStartResponse start = (await started.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
            Pass1ExecutionStatusResponse terminal = await WaitForT043TerminalAsync(
                client, start.Pass1ExecutionId, TimeSpan.FromSeconds(90), database);
            string pass1Diagnostics = await ReadT043Pass1DiagnosticsAsync(database);
            Assert.True(
                terminal.Phase == "FailedPass1",
                $"T043 rejection terminal phase was {terminal.Phase}; warnings={JsonSerializer.Serialize(terminal.Warnings)}; durable={pass1Diagnostics}");
            await using VulgataDbContext verification = database.CreateContext();
            Assert.Equal(1, await verification.Pass1PairExecutions.CountAsync());
            Assert.Equal(0, await verification.CrossRepositoryResolutionDecisions.CountAsync());
            Assert.Equal(0, await verification.DocumentEdges.CountAsync());
            Assert.Equal(0, await verification.Uncertainties.CountAsync());
            Assert.Equal(0, await verification.Pass2Executions.CountAsync());
        }
        finally
        {
            DeleteT043Root(firstRoot);
            DeleteT043Root(secondRoot);
        }
    }

    private static WebApplicationFactory<Program> CreateVerticalFactory(
        string connectionString,
        ExactResolutionProfile profile,
        IReadOnlyDictionary<Guid, string> repositoryAliases,
        IReadOnlyDictionary<Guid, string> repositoryRoots,
        bool rejectWorker,
        out T039PairBuilderDiagnostics diagnostics)
    {
        WebApplicationFactory<Program> factory = CreateFactory(
            connectionString,
            profile,
            repositoryAliases,
            out diagnostics);
        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPreparedPass1JobExecutor>();
            services.AddSingleton<IPreparedPass1JobExecutor>(
                rejectWorker
                    ? new T043RejectedWorkerExecutor()
                    : new T043ValidWorkerExecutor());
            services.RemoveAll<IExactGitRevisionSnapshotService>();
            services.AddSingleton<IExactGitRevisionSnapshotService>(
                new T043SnapshotService(repositoryRoots));
            services.RemoveAll<PreparedPass1RevisionBindingRegistry>();
            services.RemoveAll<IPreparedPass1RevisionBindingRegistry>();
            T043RevisionBindingRegistry registry = new(repositoryRoots);
            services.AddSingleton(registry);
            services.AddSingleton<IPreparedPass1RevisionBindingRegistry>(registry);
            services.RemoveAll<IPreparedRevisionLeaseResolver>();
            services.AddScoped<IPreparedRevisionLeaseResolver>(provider =>
                provider.GetRequiredService<T043RevisionBindingRegistry>());
        }));
    }

    private static async Task SeedAdministratorAsync(
        WebApplicationFactory<Program> factory,
        string email,
        Guid systemId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser administrator = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        IdentityResult created = await users.CreateAsync(administrator, "Valid1!Pass");
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
        IdentityResult role = await users.AddToRoleAsync(administrator, RoleNames.Administrator);
        Assert.True(role.Succeeded, string.Join(", ", role.Errors.Select(error => error.Description)));
        VulgataDbContext domain = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        domain.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(
            systemId,
            administrator.Id,
            PreparedScanTestHost.FixedUtcNow));
        await domain.SaveChangesAsync();
    }

    private static async Task<Pass1ExecutionStatusResponse> WaitForT043TerminalAsync(
        HttpClient client,
        Guid executionId,
        TimeSpan timeout,
        PreparedScanInitiationPostgreSqlFixture database)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        Pass1ExecutionStatusResponse? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync(
                $"/api/pass1-executions/{executionId:D}");
            string body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            last = (await response.Content.ReadFromJsonAsync<Pass1ExecutionStatusResponse>())!;
            if (last.Phase is "ReadyForResolution" or "FailedPass1" or "FailedIntegrity" or
                "FailedSecurity" or "Cancelled")
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException(
            $"T043 Pass 1 did not reach a terminal phase: {last?.Phase ?? "unavailable"}; " +
            $"durable={await ReadT043Pass1DiagnosticsAsync(database)}.");
    }

    private static async Task<string> ReadT043Pass1DiagnosticsAsync(
        PreparedScanInitiationPostgreSqlFixture database)
    {
        await using VulgataDbContext context = database.CreateContext();
        var pair = await context.Pass1PairExecutions.AsNoTracking()
            .Select(value => new
            {
                value.Phase,
                value.ReasonCategory,
                value.ReasonCode,
                value.DispatchedFileCount,
                value.HighValueSeedCount,
            })
            .SingleOrDefaultAsync();
        var jobs = await context.ScanJobExecutions.AsNoTracking()
            .GroupBy(value => new
            {
                value.State,
                value.AttemptCount,
                value.FailureCategory,
                value.FailureCode,
            })
            .Select(group => new
            {
                group.Key.State,
                group.Key.AttemptCount,
                group.Key.FailureCategory,
                group.Key.FailureCode,
                Count = group.Count(),
            })
            .OrderBy(value => value.State)
            .ThenBy(value => value.AttemptCount)
            .ToArrayAsync();
        int documents = await context.DocumentVersions.CountAsync();
        return JsonSerializer.Serialize(new { pair, jobs, documents });
    }

    private static async Task<ExactResolutionRuntimeRequest> CreateRuntimeRequestAsync(
        PreparedScanInitiationPostgreSqlFixture database,
        ExactResolutionProfile profile,
        Guid firstId,
        Guid secondId)
    {
        await using VulgataDbContext context = database.CreateContext();
        Guid[] repositoryIds = [firstId, secondId];
        var manifests = await context.PreparedScanManifests.AsNoTracking()
            .Where(manifest => repositoryIds.Contains(manifest.RepositoryId))
            .ToArrayAsync();
        var executions = await context.ScanExecutions.AsNoTracking()
            .Where(execution => repositoryIds.Contains(execution.RepositoryId))
            .ToArrayAsync();
        ExactResolutionRuntimeBinding[] bindings = [.. profile.Repositories.Select(repository =>
        {
            Guid id = StringComparer.Ordinal.Equals(repository.Alias, profile.Repositories[0].Alias)
                ? firstId
                : secondId;
            PreparedScanManifest manifest = manifests.Single(value => value.RepositoryId == id);
            ScanExecution execution = executions.Single(value => value.ScanRunId == manifest.ScanRunId);
            return new ExactResolutionRuntimeBinding(
                repository.Alias,
                repository.StableGitIdentity,
                id,
                manifest.ScanRunId,
                execution.Id);
        })];
        SystemEntity system = await context.Systems.AsNoTracking()
            .SingleAsync(value => value.Repositories.Any(repository => repository.Id == firstId));
        return ExactResolutionRuntimeRequest.Create(system.Id, profile, bindings);
    }

    private static async Task<string> ReadT043ExactDiagnosticsAsync(
        IServiceProvider services,
        ExactResolutionProfile profile,
        ExactResolutionRuntimeRequest runtime,
        string firstRoot,
        string secondRoot)
    {
        ExactCandidateExtractors extractors = services.GetRequiredService<ExactCandidateExtractors>();
        var counts = new List<object>();
        try
        {
            VulgataDbContext context = services.GetRequiredService<VulgataDbContext>();
            string[] unsupportedPaths = profile.UnsupportedObservations
                .Select(value => value.OwnerDocument.RepositoryRelativePath)
                .ToArray();
            var unsupportedOwners = await context.CodeUnits.AsNoTracking()
                .Where(value => unsupportedPaths.Contains(value.FilePath))
                .Select(value => new
                {
                    value.FilePath,
                    value.SymbolName,
                    value.RepositoryId,
                    value.ScanRunId,
                })
                .ToArrayAsync();
            foreach (ResolutionOccurrence occurrence in profile.Occurrences)
            {
                ExactResolutionRuntimeBinding consumer = runtime.Bindings.Single(value =>
                    StringComparer.Ordinal.Equals(value.Alias, occurrence.ConsumerAlias));
                ExactResolutionRuntimeBinding provider = runtime.Bindings.Single(value =>
                    StringComparer.Ordinal.Equals(value.Alias, occurrence.ProviderAlias));
                string consumerRoot = StringComparer.Ordinal.Equals(
                    consumer.Alias,
                    profile.Repositories[0].Alias)
                    ? firstRoot
                    : secondRoot;
                string providerRoot = StringComparer.Ordinal.Equals(
                    provider.Alias,
                    profile.Repositories[0].Alias)
                    ? firstRoot
                    : secondRoot;
                await using var consumerLease = new T043SnapshotLease(
                    consumer.RepositoryId,
                    consumer.ScanRunId,
                    profile.Repositories.Single(value =>
                        StringComparer.Ordinal.Equals(value.Alias, consumer.Alias)).ExpectedCommit,
                    consumerRoot);
                await using var providerLease = new T043SnapshotLease(
                    provider.RepositoryId,
                    provider.ScanRunId,
                    profile.Repositories.Single(value =>
                        StringComparer.Ordinal.Equals(value.Alias, provider.Alias)).ExpectedCommit,
                    providerRoot);
                IReadOnlyList<ConsumerDispatchCandidate> consumers =
                    await extractors.ExtractConsumersAsync(consumerLease, occurrence);
                IReadOnlyList<ProviderEndpointCandidate> providers =
                    await extractors.ExtractProvidersAsync(providerLease, occurrence);
                counts.Add(new
                {
                    occurrence.OccurrenceId,
                    Consumers = consumers.Count,
                    Providers = providers.Count,
                });
            }

            return $"extractors={JsonSerializer.Serialize(counts)};" +
                $"unsupportedOwners={JsonSerializer.Serialize(unsupportedOwners)};" +
                $"expectedUnsupportedSymbols={JsonSerializer.Serialize(profile.UnsupportedObservations.Select(value => new { value.OwnerDocument.RepositoryRelativePath, value.OwnerDocument.Symbol }))}";
        }
        catch (Exception exception)
        {
            return $"extractors={exception.GetType().Name}:{exception.Message}";
        }
    }

    private static void WriteProfileSources(
        string root,
        ExactResolutionProfile profile,
        string alias,
        Guid repositoryId)
    {
        ProfileRepository repository = profile.Repositories.Single(value =>
            StringComparer.Ordinal.Equals(value.Alias, alias));
        IEnumerable<string> paths = profile.Occurrences
            .Where(value => value.ConsumerAlias == alias)
            .SelectMany(value => value.Consumer.Files)
            .Concat(profile.Occurrences
                .Where(value => value.ProviderAlias == alias)
                .SelectMany(value => value.Provider.Files))
            .Concat(profile.Occurrences
                .Where(value => value.ProviderAlias == alias)
                .Select(value => value.Provider.ImplementationDocument.RepositoryRelativePath))
            .Concat(profile.UnsupportedObservations
                .Where(value => value.OwnerAlias == alias)
                .Select(value => value.OwnerDocument.RepositoryRelativePath))
            .Distinct(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            string language = Path.GetExtension(path).Equals(".java", StringComparison.OrdinalIgnoreCase)
                ? "Java"
                : "XML";
            string fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, CreateReviewedSource(path, language), new UTF8Encoding(false));
        }

        for (int index = 0; index < 60; index++)
        {
            string path = $"src/story125/t039-{index:00}.xml";
            string fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(
                fullPath,
                $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<configuration><entry name=\"{repositoryId:N}-{index:00}\">t039</entry></configuration>\n",
                new UTF8Encoding(false));
        }
    }

    private static void DeleteT043Root(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        ExactResolutionProfile profile,
        IReadOnlyDictionary<Guid, string> repositoryAliases,
        out T039PairBuilderDiagnostics diagnostics)
    {
        diagnostics = new();
        T039PairBuilderDiagnostics capturedDiagnostics = diagnostics;
        StubPreparedRevisionAcquirer revisions = new();
        revisions.Handler = (request, _) => Task.FromResult(new PreparedRevisionAcquisitionResult(
            new PreparedRevisionSnapshot(
                request.RepositoryId,
                request.ScanRunId,
                request.RequestedRef,
                request.ProfiledRevision,
                new('a', 64),
                $"snapshot-{request.RepositoryId:N}",
                $"root-{request.RepositoryId:N}"),
            null));

        StubPreparedSnapshotSource snapshots = new();
        snapshots.Handler = (snapshot, _) => Task.FromResult(new PreparedSnapshotReadResult(
            CreateFiles(snapshot, profile, repositoryAliases[snapshot.RepositoryId]), null));

        StubPreparedCommunicationCatalogSource catalog = new();
        catalog.Handler = _ => Task.FromResult(new PreparedCatalogLoadResult(
            new CommunicationPatternCatalog(
                "communication-pattern-catalog/1.0",
                "story125-t039-catalog",
                "1",
                new('e', 64),
                []),
            null));

        StubPreparedCodeGraphAnalysisPort codeGraph = new();
        codeGraph.ProofHandler = (request, _) => Task.FromResult(new PreparedCodeGraphProofResult(
            AnalysisOperationStatus.Succeeded,
            new CodeGraphCompletenessProof(
                request.Scope.RepositoryId,
                request.Scope.ScanRunId,
                request.Scope.ProfiledRevision,
                request.SnapshotIdentity,
                request.SnapshotRootBindingHash,
                request.Scope.RuntimeIdentityHash,
                "story125-t039-index",
                "codegraph-completeness-proof/1.0",
                true,
                CodeGraphIndexState.Complete,
                0, 0, 0, false, false, 0, 64,
                request.LogicalProjectBinding,
                request.LogicalIndexBinding,
                true, true, true, true, true, true, true,
                new('b', 64), 0),
            null));

        StubPreparedJavaReadinessPort java = new();
        java.Handler = (request, _) => Task.FromResult(new PreparedJavaReadinessResult(
            AnalysisOperationStatus.Succeeded,
            new JavaReadinessProof(
                request.Scope.RepositoryId,
                request.Scope.ScanRunId,
                request.Scope.ProfiledRevision,
                request.SnapshotIdentity,
                request.Scope.RuntimeIdentityHash,
                request.WorkspaceBindingHash,
                "java-readiness/1.0",
                JavaReadinessOutcome.Ready,
                "jdt-language-server-1.61.0-202608141332",
                new('c', 64), 21, "2.25.29", AnalysisTransport.Stdio,
                true,
                request.EligibleJavaRelativePaths.Order(StringComparer.Ordinal).First(),
                true, true, JavaImportState.Usable, 30,
                true, true, true, true, true, new('d', 64), 0),
            null));

        return new PreparedScanInitiationPostgreSqlWebApplicationFactory(connectionString)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPreparedScanRuntimeIdentityResolver>();
                services.AddSingleton<IPreparedScanRuntimeIdentityResolver>(
                    new PublicPreparedScanRuntimeIdentityResolver());
                ServiceDescriptor pairBuilder = services.Single(
                    descriptor => descriptor.ServiceType == typeof(IPreparedScanPairBuilder));
                Type pairBuilderType = pairBuilder.ImplementationType ??
                    throw new InvalidOperationException("T039 expected a typed Prepared Scan pair builder.");
                services.RemoveAll<IPreparedScanPairBuilder>();
                services.AddScoped<IPreparedScanPairBuilder>(serviceProvider =>
                {
                    IPreparedScanPairBuilder inner = (IPreparedScanPairBuilder)
                        ActivatorUtilities.CreateInstance(serviceProvider, pairBuilderType);
                    return new CapturingPreparedScanPairBuilder(inner, capturedDiagnostics);
                });
                services.RemoveAll<IAnalysisRuntimeHealthProbe>();
                services.AddSingleton<IAnalysisRuntimeHealthProbe>(new StubReadyAnalysisRuntimeHealthProbe());
                services.RemoveAll<IPreparedRevisionAcquirer>();
                services.AddSingleton<IPreparedRevisionAcquirer>(revisions);
                services.RemoveAll<IPreparedSnapshotSource>();
                services.AddSingleton<IPreparedSnapshotSource>(snapshots);
                services.RemoveAll<IPreparedCommunicationCatalogSource>();
                services.AddSingleton<IPreparedCommunicationCatalogSource>(catalog);
                services.RemoveAll<IPreparedCodeGraphAnalysisPort>();
                services.AddSingleton<IPreparedCodeGraphAnalysisPort>(codeGraph);
                services.RemoveAll<IPreparedJavaReadinessPort>();
                services.AddSingleton<IPreparedJavaReadinessPort>(java);
                services.RemoveAll<IPreparedTopLevelSymbolSource>();
                services.AddSingleton<IPreparedTopLevelSymbolSource>(new StubPreparedTopLevelSymbolSource());
            }));
    }

    private static PreparedScanRequest CreateRequest(
        ExactResolutionProfile profile,
        Guid firstId,
        Guid secondId) => new()
        {
            Repositories = profile.Repositories.Select((repository, index) => new PreparedScanRepositoryRevisionBinding
            {
                RepositoryId = index == 0 ? firstId : secondId,
                GitRef = repository.ExpectedRef,
                ProfiledRevision = repository.ExpectedCommit,
            }).ToArray(),
        };

    private static async Task<(SystemEntity System, RepositoryEntity First, RepositoryEntity Second)>
        SeedProfilePairAsync(PreparedScanInitiationPostgreSqlFixture database, ExactResolutionProfile profile)
    {
        await using VulgataDbContext context = database.CreateContext();
        DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;
        SystemEntity system = new("Story 12.5 T039 system", "ordinary system", "T039", now);
        RepositoryEntity first = system.AddRepository(
            profile.Repositories[0].StableGitIdentity,
            profile.Repositories[0].StableGitIdentity,
            null,
            profile.Repositories[0].Alias,
            now);
        RepositoryEntity second = system.AddRepository(
            profile.Repositories[1].StableGitIdentity,
            profile.Repositories[1].StableGitIdentity,
            null,
            profile.Repositories[1].Alias,
            now);
        context.Systems.Add(system);
        await context.SaveChangesAsync();
        return (system, first, second);
    }

    private static async Task ApplyExistingSchemaAsync(
        PreparedScanInitiationPostgreSqlFixture database)
    {
        await using VulgataDbContext context = database.CreateContext();
        await context.Database.MigrateAsync();
    }

    private static IReadOnlyList<PreparedSnapshotFile> CreateFiles(
        PreparedRevisionSnapshot snapshot,
        ExactResolutionProfile profile,
        string alias)
    {
        ProfileRepository selectedRepository = profile.Repositories.Single(
            repository => StringComparer.Ordinal.Equals(repository.Alias, alias));
        HashSet<string> requiredPaths = new[] { selectedRepository }
            .SelectMany(repository => profile.Occurrences
                .Where(occurrence => occurrence.ConsumerAlias == repository.Alias)
                .SelectMany(occurrence => occurrence.Consumer.Files)
                .Concat(profile.Occurrences
                    .Where(occurrence => occurrence.ProviderAlias == repository.Alias)
                    .SelectMany(occurrence => occurrence.Provider.Files))
                .Concat(profile.Occurrences
                    .Where(occurrence => occurrence.ProviderAlias == repository.Alias)
                    .Select(occurrence => occurrence.Provider.ImplementationDocument.RepositoryRelativePath))
                .Concat(profile.UnsupportedObservations
                    .Where(observation => observation.OwnerAlias == repository.Alias)
                    .Select(observation => observation.OwnerDocument.RepositoryRelativePath)))
            .ToHashSet(StringComparer.Ordinal);
        List<PreparedSnapshotFile> files = new(requiredPaths.Count + 60);
        foreach (string path in requiredPaths.Order(StringComparer.Ordinal))
        {
            string extension = Path.GetExtension(path);
            string language = extension.Equals(".java", StringComparison.OrdinalIgnoreCase)
                ? "Java"
                : "XML";
            string source = CreateReviewedSource(path, language);
            AddFile(files, snapshot, path, language, source);
        }

        for (int index = 0; index < 60; index++)
        {
            string path = $"src/story125/t039-{index:00}.xml";
            AddFile(
                files,
                snapshot,
                path,
                "XML",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<configuration><entry name=\"{snapshot.RepositoryId:N}-{index:00}\">t039</entry></configuration>\n");
        }

        return files;
    }

    private static string CreateReviewedSource(string path, string language)
    {
        if (StringComparer.Ordinal.Equals(
                path,
                "src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java"))
        {
            return SourceWithLine(
                45,
                "return netTools.execute(ctx, \"cust/updateSession\");");
        }

        if (StringComparer.Ordinal.Equals(
                path,
                "src/main/java/cn/com/yitong/actions/cust/UploadOcrToOssOp.java"))
        {
            return SourceWithLine(
                66,
                "ossClientService.simpleUploadToOSSBase64String(request);");
        }

        if (StringComparer.Ordinal.Equals(
                path,
                "src/main/resources/disgram/in/cust/updateSession.xml"))
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<trans name=\"in/cust/updateSession\">\n</trans>\n";
        }

        if (StringComparer.Ordinal.Equals(
                path,
                "src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java"))
        {
            return SourceWithLine(3, "public final class UpdateSessionOp {}");
        }

        return language == "Java"
            ? "class T039Source { void selected() {} }\n"
            : "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<configuration><entry name=\"t039\">t039</entry></configuration>\n";
    }

    private static string SourceWithLine(int line, string statement)
    {
        var builder = new StringBuilder();
        for (int currentLine = 1; currentLine < line; currentLine++)
        {
            builder.Append("// fixture line ")
                .Append(currentLine)
                .Append('\n');
        }

        return builder.Append(statement).Append('\n').ToString();
    }

    private static void AddFile(
        ICollection<PreparedSnapshotFile> files,
        PreparedRevisionSnapshot snapshot,
        string path,
        string language,
        string source)
    {
        if (files.Any(file => StringComparer.Ordinal.Equals(file.RelativePath, path)))
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(source);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        files.Add(new PreparedSnapshotFile(
            path, language, true, "EligibleProductionSource", hash, hash,
            bytes.LongLength, 1, "utf-8", bytes));
    }

    private static async Task<T039Counts> ReadCountsAsync(PreparedScanInitiationPostgreSqlFixture database)
    {
        await using VulgataDbContext context = database.CreateContext();
        return new(
            await context.ScanRuns.CountAsync(),
            await context.ScanExecutions.CountAsync(),
            await context.TwoRepositoryScanCoordinations.CountAsync(),
            await context.PreparedScanManifests.CountAsync(),
            await context.CodeUnits.CountAsync(),
            await context.PreparedScanManifestEntries.CountAsync(entry =>
                entry.DirectBusinessScore == 1_100 && entry.PairSelectionOrdinal >= 1),
            await context.CrossRepositoryResolutionDecisions.CountAsync(),
            await context.DocumentEdges.CountAsync(),
            await context.Uncertainties.CountAsync(),
            await context.Pass1PairExecutions.CountAsync(),
            await context.ScanExecutions.CountAsync(execution => execution.Phase == ScanExecutionPhase.FailedPass1));
    }

    private static async Task LoginAsync(HttpClient client, string email = "story125.t039.admin@example.com")
    {
        using HttpResponseMessage page = await client.GetAsync("/Account/Login");
        page.EnsureSuccessStatusCode();
        string html = await page.Content.ReadAsStringAsync();
        Match form = Regex.Match(html, "<form[^>]*method=\"post\"[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, "The login form was not rendered.");
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(form.Groups["content"].Value,
                     "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"[^>]*>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            fields[input.Groups["name"].Value] = input.Groups["value"].Value;
        }

        fields["Input.Email"] = email;
        fields["Input.Password"] = "Valid1!Pass";
        fields["Input.RememberMe"] = "false";
        using FormUrlEncodedContent content = new(fields);
        using HttpResponseMessage response = await client.PostAsync("/Account/Login", content);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private sealed class T043ValidWorkerExecutor : IPreparedPass1JobExecutor
    {
        public Task<PreparedPass1RawWorkerResult> ExecuteAsync(
            PreparedPass1WorkerPacket packet,
            string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PreparedPass1RawWorkerResult(
                PreparedPass1RawWorkerOutcome.Completed,
                Encoding.UTF8.GetBytes(CreateValidDocumentJson(packet)),
                null));
        }
    }

    private sealed class T043RejectedWorkerExecutor : IPreparedPass1JobExecutor
    {
        public Task<PreparedPass1RawWorkerResult> ExecuteAsync(
            PreparedPass1WorkerPacket packet,
            string systemPrompt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreparedPass1RawWorkerResult(
                PreparedPass1RawWorkerOutcome.Rejected,
                ReadOnlyMemory<byte>.Empty,
                "invalid-or-repeated-tool-call"));
    }

    private static string CreateValidDocumentJson(PreparedPass1WorkerPacket packet)
    {
        MethodInfo method = typeof(CodeLogicFileV2SchemaTests).GetMethod(
            "BaseJson",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Xunit.Sdk.XunitException("The authoritative Code Logic fixture is unavailable.");
        JsonObject root = JsonNode.Parse((string)method.Invoke(null, null)!)!.AsObject();
        JsonObject binding = root["binding"]!.AsObject();
        binding["jobId"] = packet.JobId.ToString("D");
        binding["scanExecutionId"] = packet.ScanExecutionId.ToString("D");
        binding["systemId"] = packet.SystemId.ToString("D");
        binding["repositoryId"] = packet.RepositoryId.ToString("D");
        binding["scanRunId"] = packet.ScanRunId.ToString("D");
        binding["profiledRevision"] = packet.ProfiledRevision;
        binding["fileCodeUnitId"] = packet.FileCodeUnitId.ToString("D");
        binding["fileCodeUnitIdentityFingerprint"] = packet.FileCodeUnitIdentityFingerprint;
        binding["preparedScanManifestId"] = packet.PreparedScanManifestId.ToString("D");
        binding["preparedScanManifestFingerprint"] = packet.PreparedScanManifestFingerprint;
        binding["pairPackageFingerprint"] = packet.PairPackageFingerprint;
        binding["repositoryRelativePath"] = packet.RepositoryRelativePath;
        binding["byteStart"] = packet.ByteStart;
        binding["byteEnd"] = packet.ByteEnd;
        binding["lineStart"] = packet.LineStart;
        binding["lineEnd"] = packet.LineEnd;
        binding["segmentOrdinal"] = packet.SegmentOrdinal;
        binding["segmentCount"] = packet.SegmentCount;
        binding["fileHash"] = packet.FileHash;
        binding["sourceHash"] = packet.SourceHash;
        binding["promptIdentity"] = packet.PromptIdentity;
        binding["documentSchemaIdentity"] = packet.DocumentSchemaIdentity;
        binding["analysisRuntimeIdentityHash"] = packet.AnalysisRuntimeIdentityHash;
        JsonObject anchor = root["sourceAnchors"]![0]!.AsObject();
        anchor["repositoryRelativePath"] = packet.RepositoryRelativePath;
        anchor["byteStart"] = packet.ByteStart;
        anchor["byteEnd"] = packet.ByteEnd;
        anchor["lineStart"] = packet.LineStart;
        anchor["lineEnd"] = packet.LineEnd;
        anchor["sourceHash"] = packet.SourceHash;
        anchor["evidenceLocator"] = $"{packet.RepositoryRelativePath}#L{packet.LineStart}-L{packet.LineEnd}";
        root["internalUnits"] = new JsonArray();
        root["executionFlow"] = new JsonArray();
        root["dataFlow"] = new JsonArray();
        root["rules"] = new JsonArray();
        root["dataAccess"] = new JsonArray();
        root["communicationEvidence"] = new JsonArray();
        root["knownReferences"] = new JsonArray();
        root["deferredObservations"] = new JsonArray();
        root["assumptions"] = new JsonArray();
        root["filePurpose"]!["responsibilities"] = new JsonArray(
            new JsonObject
            {
                ["description"] = "Describes the prepared file.",
                ["sourceAnchorRefs"] = new JsonArray("a001"),
            });
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private sealed class T043SnapshotService(
        IReadOnlyDictionary<Guid, string> roots) : IExactGitRevisionSnapshotService
    {
        public Task<ICommittedRepositorySnapshotLease> AcquireAsync(
            RepositoryEntity repository,
            Guid scanRunId,
            string expectedRef,
            string expectedCommit,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!roots.TryGetValue(repository.Id, out string? root))
            {
                throw new InvalidOperationException("T043 has no source root for the requested Repository.");
            }

            return Task.FromResult<ICommittedRepositorySnapshotLease>(
                new T043SnapshotLease(repository.Id, scanRunId, expectedCommit, root));
        }
    }

    private sealed class T043RevisionBindingRegistry(
        IReadOnlyDictionary<Guid, string> roots) :
        IPreparedPass1RevisionBindingRegistry,
        IPreparedRevisionLeaseResolver
    {
        private readonly Dictionary<Guid, PreparedPass1PairRevisionBindings> _pairs = [];
        private readonly Dictionary<string, ICommittedRepositorySnapshotLease> _leases =
            new(StringComparer.Ordinal);

        public Task<PreparedPass1PairRevisionBindings?> AcquirePairAsync(
            Guid pairExecutionId,
            IReadOnlyList<PreparedRevisionRequest> requests,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_pairs.TryGetValue(pairExecutionId, out PreparedPass1PairRevisionBindings? existing))
            {
                return Task.FromResult<PreparedPass1PairRevisionBindings?>(existing);
            }

            PreparedPass1RevisionBinding[] bindings = [.. requests.OrderBy(request => request.RepositoryId)
                .Select(request =>
                {
                    string token = PreparedPass1RevisionBindingToken.Create(
                        pairExecutionId,
                        request.RepositoryId,
                        request.ScanRunId,
                        request.ProfiledRevision);
                    if (!roots.TryGetValue(request.RepositoryId, out string? root))
                    {
                        throw new InvalidOperationException("T043 has no exact source root.");
                    }

                    _leases[token] = new T043SnapshotLease(
                        request.RepositoryId,
                        request.ScanRunId,
                        request.ProfiledRevision,
                        root);
                    return new PreparedPass1RevisionBinding(
                        pairExecutionId,
                        request.RepositoryId,
                        request.ScanRunId,
                        request.ProfiledRevision,
                        token);
                })];
            PreparedPass1PairRevisionBindings result = new(pairExecutionId, bindings);
            _pairs[pairExecutionId] = result;
            return Task.FromResult<PreparedPass1PairRevisionBindings?>(result);
        }

        public PreparedPass1RevisionBinding? ResolveBinding(Guid pairExecutionId, Guid repositoryId) =>
            _pairs.TryGetValue(pairExecutionId, out PreparedPass1PairRevisionBindings? pair)
                ? pair.Bindings.SingleOrDefault(binding => binding.RepositoryId == repositoryId)
                : null;

        public ValueTask ReleasePairAsync(Guid pairExecutionId)
        {
            if (_pairs.Remove(pairExecutionId, out PreparedPass1PairRevisionBindings? pair))
            {
                foreach (PreparedPass1RevisionBinding binding in pair.Bindings)
                {
                    _leases.Remove(binding.BindingToken, out ICommittedRepositorySnapshotLease? lease);
                    lease?.DisposeAsync();
                }
            }

            return ValueTask.CompletedTask;
        }

        public bool TryResolveLease(
            string bindingToken,
            out ICommittedRepositorySnapshotLease? lease) =>
            _leases.TryGetValue(bindingToken, out lease);
    }

    private sealed class T043SnapshotLease(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string rootPath) : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId => repositoryId;
        public Guid ScanRunId => scanRunId;
        public string ProfiledRevision => profiledRevision;
        public string RootPath => rootPath;

        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CapturingPreparedScanPairBuilder(
        IPreparedScanPairBuilder inner,
        T039PairBuilderDiagnostics diagnostics) : IPreparedScanPairBuilder
    {
        public async Task<PreparedScanPairBuildResult> BuildAsync(
            PreparedScanInitiationTuple tuple,
            IReadOnlyList<RepositoryEntity> repositories,
            IReadOnlyList<PreparedScanRepositoryRevisionBinding> revisionBindings,
            CancellationToken cancellationToken)
        {
            try
            {
                return await inner.BuildAsync(
                    tuple,
                    repositories,
                    revisionBindings,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostics.Exception = exception;
                throw;
            }
        }
    }

    private sealed class T039PairBuilderDiagnostics
    {
        public Exception? Exception { get; set; }
    }

    private readonly record struct T039Counts(
        int ScanRuns,
        int ScanExecutions,
        int Coordinations,
        int Manifests,
        int CodeUnits,
        int HighValueSelected,
        int ExactDecisions,
        int ExactEdges,
        int Uncertainties,
        int Pass1PairExecutions,
        int FailedPass1);
}

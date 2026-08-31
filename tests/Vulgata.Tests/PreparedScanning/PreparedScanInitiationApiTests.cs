using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
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
using Vulgata.Shared;
using Vulgata.Shared.Scans;
using Vulgata.Tests.PreparedScanning.TestDoubles;
using Vulgata.Web.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T011 is the public behavioral anchor for the new Prepared Scan entrance.
/// It intentionally begins with ordinary domain records only: every scan
/// lifecycle row must be created by the endpoint's production path.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class PreparedScanInitiationApiTests
{
    [Fact]
    public async Task PostPreparedScan_HostilePass1ResourceActivation_Returns503WithoutSideEffectsAsync()
    {
        CountingAuthorizationRechecker rechecker = new();
        CountingConflictInitiationStore initiationStore = new();
        await using PublicPreparedScanScenario scenario = await CreateScenarioAsync(
            services =>
            {
                services.RemoveAll<IPreparedPass1ContractIdentitySource>();
                services.AddSingleton<IPreparedPass1ContractIdentitySource>(_ =>
                    new EmbeddedPreparedPass1ContractIdentitySource(
                        getResourceNames: static () => [],
                        openResource: static _ => null));
                services.RemoveAll<IPreparedScanInitiationAuthorizationRechecker>();
                services.AddScoped<IPreparedScanInitiationAuthorizationRechecker>(_ => rechecker);
                services.RemoveAll<IPreparedScanInitiationStore>();
                services.AddScoped<IPreparedScanInitiationStore>(_ => initiationStore);
            });

        using HttpResponseMessage response = await scenario.Client.PostAsJsonAsync(
            scenario.Route,
            scenario.Request);
        string body = await response.Content.ReadAsStringAsync();

        List<string> failures = [];
        if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
        {
            failures.Add($"HTTP status was {(int)response.StatusCode} instead of 503.");
        }

        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "application/problem+json", StringComparison.Ordinal))
        {
            failures.Add($"Content-Type was '{mediaType ?? "<missing>"}' instead of application/problem+json.");
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            try
            {
                using JsonDocument problem = JsonDocument.Parse(body);
                if (problem.RootElement.GetProperty("status").GetInt32() != 503)
                {
                    failures.Add("ProblemDetails status was not 503.");
                }
                if (!string.Equals(
                        problem.RootElement.GetProperty("code").GetString(),
                        "prepared-scan-initiation-unavailable",
                        StringComparison.Ordinal))
                {
                    failures.Add("ProblemDetails code was not prepared-scan-initiation-unavailable.");
                }
            }
            catch (Exception exception)
            {
                failures.Add($"503 response was not valid ProblemDetails: {exception.Message}");
            }
        }

        if (body.Contains("pass1-code-logic-worker", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("The response disclosed the prepared Pass 1 identity.");
        }

        if (rechecker.CallCount != 0)
        {
            failures.Add($"Fresh authorization was called {rechecker.CallCount} time(s).");
        }
        if (initiationStore.CallCount != 0)
        {
            failures.Add($"Initiation store was called {initiationStore.CallCount} time(s).");
        }
        if (scenario.Observations.RevisionAcquisitionCalls != 0 ||
            scenario.CodeGraphCalls != 0 ||
            scenario.JavaReadinessCalls != 0)
        {
            failures.Add(
                $"Preparation calls were revision={scenario.Observations.RevisionAcquisitionCalls}, " +
                $"codegraph={scenario.CodeGraphCalls}, java={scenario.JavaReadinessCalls}.");
        }

        PreparedScanInitiationCounts counts = await ReadCountsAsync(scenario.Database);
        if (counts != new PreparedScanInitiationCounts(0, 0, 0, 0, 0, 0, 0))
        {
            failures.Add($"Durable initiation state was created: {counts}.");
        }

        Assert.True(
            failures.Count == 0,
            string.Join(" ", failures));
    }

    [Fact]
    public async Task PostPreparedScan_NoPreseededLifecycleState_CreatesCompletePairAndManifestsAsync()
    {
        await using PreparedScanInitiationPostgreSqlFixture database =
            await PreparedScanInitiationPostgreSqlFixture.CreateAsync();
        using WebApplicationFactory<Program> factory =
            CreateFactory(database.ConnectionString, services =>
            {
                services.RemoveAll<IPreparedScanRuntimeIdentityResolver>();
                services.AddSingleton<IPreparedScanRuntimeIdentityResolver>(
                    new PublicPreparedScanRuntimeIdentityResolver());
            });

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser administrator = new()
            {
                UserName = "story125.t011.admin@example.com",
                Email = "story125.t011.admin@example.com",
                EmailConfirmed = true,
            };
            IdentityResult created = await users.CreateAsync(administrator, "Valid1!Pass");
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
            IdentityResult role = await users.AddToRoleAsync(administrator, RoleNames.Administrator);
            Assert.True(role.Succeeded, string.Join(", ", role.Errors.Select(error => error.Description)));
        }

        (SystemEntity system, RepositoryEntity first, RepositoryEntity second) =
            await SeedOrdinaryPairAsync(database);
        PreparedScanInitiationCounts before = await ReadCountsAsync(database);
        Assert.Equal(new PreparedScanInitiationCounts(0, 0, 0, 0, 0, 0, 0), before);

        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        await LoginAsync(client);

        PreparedScanRequest request = new()
        {
            Repositories =
            [
                new()
                {
                    RepositoryId = first.Id,
                    GitRef = "refs/tags/story125-one",
                    ProfiledRevision = "0123456789abcdef0123456789abcdef01234567",
                },
                new()
                {
                    RepositoryId = second.Id,
                    GitRef = "refs/tags/story125-two",
                    ProfiledRevision = "fedcba9876543210fedcba9876543210fedcba98",
                },
            ],
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/systems/{system.Id:D}/prepared-scans",
            request);
        string body = await response.Content.ReadAsStringAsync();
        PreparedScanInitiationCounts diagnosticCounts = await ReadCountsAsync(database);

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 from the public initiation seam for owned database " +
            $"{database.DatabaseName}; actual status was {(int)response.StatusCode}; " +
            $"durable counts were {diagnosticCounts}; response was {body}.");
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        Assert.NotEqual(Guid.Empty, root.GetProperty("coordinationId").GetGuid());
        Assert.Equal(2, root.GetProperty("manifests").GetArrayLength());
        Assert.InRange(root.GetProperty("selectedCount").GetInt32(), 50, 150);

        PreparedScanInitiationCounts after = await ReadCountsAsync(database);
        Assert.Equal(new PreparedScanInitiationCounts(2, 2, 2, 1, 2, 120, 0), after);
    }

    [Fact]
    public async Task PostPreparedScan_IdenticalRequest_ReplaysSameTupleWithoutDuplicatesAsync()
    {
        await using PublicPreparedScanScenario scenario = await CreateScenarioAsync();

        using HttpResponseMessage first = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        string firstBody = await first.Content.ReadAsStringAsync();
        PreparedScanInitiationCounts firstDiagnosticCounts = await ReadCountsAsync(scenario.Database);
        Assert.True(
            first.StatusCode == HttpStatusCode.Created,
            $"Expected 201; actual {(int)first.StatusCode}; durable counts were " +
            $"{firstDiagnosticCounts}; CodeGraph calls {scenario.CodeGraphCalls}; " +
            $"Java calls {scenario.JavaReadinessCalls}; response was {firstBody}.");
        using JsonDocument firstDocument = JsonDocument.Parse(firstBody);
        Guid coordinationId = firstDocument.RootElement.GetProperty("coordinationId").GetGuid();
        PreparedScanInitiationCounts afterFirst = await ReadCountsAsync(scenario.Database);

        using HttpResponseMessage replay = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using JsonDocument replayDocument = JsonDocument.Parse(
            await replay.Content.ReadAsStringAsync());
        Assert.Equal(
            coordinationId,
            replayDocument.RootElement.GetProperty("coordinationId").GetGuid());
        Assert.Equal(afterFirst, await ReadCountsAsync(scenario.Database));
    }

    [Fact]
    public async Task PostPreparedScan_MismatchedPairOrRevision_FailsClosedWithoutDisclosureAsync()
    {
        await using PublicPreparedScanScenario scenario = await CreateScenarioAsync();
        using HttpResponseMessage first = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        PreparedScanInitiationCounts before = await ReadCountsAsync(scenario.Database);

        PreparedScanRequest mismatch = new()
        {
            Repositories =
            [
                new()
                {
                    RepositoryId = scenario.Request.Repositories[0].RepositoryId,
                    GitRef = scenario.Request.Repositories[0].GitRef,
                    ProfiledRevision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                },
                new()
                {
                    RepositoryId = scenario.Request.Repositories[1].RepositoryId,
                    GitRef = scenario.Request.Repositories[1].GitRef,
                    ProfiledRevision = scenario.Request.Repositories[1].ProfiledRevision,
                },
            ],
        };
        using HttpResponseMessage response = await scenario.Client.PostAsJsonAsync(
            scenario.Route, mismatch);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(scenario.System.Id.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await ReadCountsAsync(scenario.Database));
    }

    [Fact]
    public async Task PostPreparedScan_InitialUnauthorized_CreatesNothingAsync()
    {
        await using PublicPreparedScanScenario scenario = await CreateScenarioAsync(login: false);

        using HttpResponseMessage response = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            new PreparedScanInitiationCounts(0, 0, 0, 0, 0, 0, 0),
            await ReadCountsAsync(scenario.Database));
    }

    [Fact]
    public async Task PostPreparedScan_IdenticalConcurrentRequests_ExposeOneTupleAsync()
    {
        await using PublicPreparedScanScenario scenario = await CreateScenarioAsync();
        Task<HttpResponseMessage>[] posts =
        [
            scenario.Client.PostAsJsonAsync(scenario.Route, scenario.Request),
            scenario.Client.PostAsJsonAsync(scenario.Route, scenario.Request),
        ];
        HttpResponseMessage[] responses = await Task.WhenAll(posts);
        try
        {
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
            Guid[] coordinationIds = await Task.WhenAll(
                responses.Select(async response =>
                    JsonDocument.Parse(await response.Content.ReadAsStringAsync())
                        .RootElement.GetProperty("coordinationId").GetGuid()));
            Assert.Equal(1, coordinationIds.Distinct().Count());
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task PostPreparedScan_PostInitiationPreparationFailure_RetriesExactTupleAsync()
    {
        await using PublicPreparedScanScenario scenario = await CreateScenarioAsync();
        scenario.Observations.FailFirstPreparation = true;

        using HttpResponseMessage failed = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        PreparedScanInitiationCounts afterFailure = await ReadCountsAsync(scenario.Database);
        Assert.Equal(2, afterFailure.ScanRuns);
        Assert.Equal(2, afterFailure.ScanExecutions);
        Assert.Equal(1, afterFailure.Coordinations);

        using HttpResponseMessage retry = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        PreparedScanInitiationCounts afterRetry = await ReadCountsAsync(scenario.Database);
        Assert.Equal(afterFailure.ScanRuns, afterRetry.ScanRuns);
        Assert.Equal(afterFailure.ScanExecutions, afterRetry.ScanExecutions);
        Assert.Equal(afterFailure.Coordinations, afterRetry.Coordinations);
        Assert.Equal(2, afterRetry.PreScanResults);
        Assert.Equal(2, afterRetry.Manifests);
        Assert.Equal(120, afterRetry.CodeUnits);
        Assert.Equal(0, afterRetry.LogicalDocuments);
    }

    [Fact]
    public async Task PostPreparedScan_FreshAuthorizationRevocation_CreatesNothingAsync()
    {
        CountingAuthorizationRechecker rechecker = new() { Deny = true };
        await using PublicPreparedScanScenario scenario = await CreateScenarioAsync(
            services =>
            {
                services.RemoveAll<IPreparedScanInitiationAuthorizationRechecker>();
                services.AddScoped<IPreparedScanInitiationAuthorizationRechecker>(_ => rechecker);
            });

        using HttpResponseMessage response = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, rechecker.CallCount);
        Assert.Equal(
            new PreparedScanInitiationCounts(0, 0, 0, 0, 0, 0, 0),
            await ReadCountsAsync(scenario.Database));
    }

    [Fact]
    public async Task PostPreparedScan_PreparedAndAdvancedReplay_UsesNoAnalysisAsync()
    {
        await using PublicPreparedScanScenario scenario = await CreateScenarioAsync();
        using HttpResponseMessage first = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        PreparedScanInitiationCounts beforeReplay = await ReadCountsAsync(scenario.Database);

        using HttpResponseMessage replay = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(beforeReplay, await ReadCountsAsync(scenario.Database));
    }

    [Fact]
    public async Task PostPreparedScan_PartialCorruptOrLegacyState_MapsConflictWithoutAnalysisAsync()
    {
        CountingConflictInitiationStore store = new();
        await using PublicPreparedScanScenario scenario = await CreateScenarioAsync(
            services =>
            {
                services.RemoveAll<IPreparedScanInitiationStore>();
                services.AddScoped<IPreparedScanInitiationStore>(_ => store);
            });

        using HttpResponseMessage response = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, store.CallCount);
        Assert.Equal(0, scenario.CodeGraphCalls);
        Assert.Equal(0, scenario.JavaReadinessCalls);
        Assert.Equal(
            new PreparedScanInitiationCounts(0, 0, 0, 0, 0, 0, 0),
            await ReadCountsAsync(scenario.Database));
    }

    [Fact]
    public async Task PostPreparedScan_DefaultComposition_UsesPreparedPortsAndNoLaterStagesAsync()
    {
        await using PublicPreparedScanScenario scenario = await CreateScenarioAsync();
        using HttpResponseMessage response = await scenario.Client.PostAsJsonAsync(
            scenario.Route, scenario.Request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, scenario.CodeGraphCalls);
        Assert.Equal(2, scenario.JavaReadinessCalls);
        Assert.False(scenario.CgcResolved);
        Assert.True(scenario.BuilderEntryTupleVisible);
    }

    [Fact]
    public void InitiationBoundary_HasNoForbiddenLaterStageDependencyOrLegacyEndpointCoordinator()
    {
        Type coordinator = typeof(Program).Assembly.GetType(
            "Vulgata.Web.Data.PreparedScanInitiationCoordinator",
            throwOnError: true)!;
        Type endpoints = typeof(Program).Assembly.GetType(
            "Vulgata.Web.Api.Scans.PreparedScanEndpoints",
            throwOnError: true)!;
        MethodInfo handler = Assert.Single(
            endpoints.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method => method.Name == "PrepareAsync"));
        Assert.Contains(
            handler.GetParameters(),
            parameter => parameter.ParameterType == coordinator);
        Assert.DoesNotContain(
            handler.GetParameters(),
            parameter => parameter.ParameterType.Name == "PreparedScanCoordinator");

        string[] forbiddenTokens =
        [
            "Worker", "Pass1", "Pass2", "Document", "Edge", "Uncertainty",
            "Chat", "Llm", "Agent", "RepositoryProfilingCoordinator",
        ];
        Assert.DoesNotContain(
            coordinator.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType.Name),
            name => forbiddenTokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void PreparedScanPairBuilder_RequiresExactlyOneServerOwnedReviewedProfileAuthority()
    {
        Type builder = typeof(Program).Assembly.GetType(
            "Vulgata.Web.Data.PreparedScanPairBuilder",
            throwOnError: true)!;
        Type? authority = typeof(PreparedScanPlanner).Assembly.GetType(
            "Vulgata.Core.DomainServices.IReviewedExactProfileSeedAuthority");

        Assert.NotNull(authority);
        ParameterInfo[] matches = builder.GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => parameter.ParameterType == authority)
            .ToArray();
        Assert.Single(matches);
        Assert.False(matches[0].HasDefaultValue);
    }

    private static async Task<PublicPreparedScanScenario> CreateScenarioAsync(
        Action<IServiceCollection>? configureServices = null,
        bool login = true)
    {
        PreparedScanInitiationPostgreSqlFixture database =
            await PreparedScanInitiationPostgreSqlFixture.CreateAsync();
        PublicPreparedScanObservations observations = new();
        Action<IServiceCollection> scenarioConfiguration = services =>
        {
            services.RemoveAll<IPreparedScanRuntimeIdentityResolver>();
            services.AddSingleton<IPreparedScanRuntimeIdentityResolver>(
                new PublicPreparedScanRuntimeIdentityResolver());
            configureServices?.Invoke(services);
        };
        WebApplicationFactory<Program> factory = CreateFactory(
            database.ConnectionString,
            scenarioConfiguration,
            observations);
        try
        {
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                UserManager<ApplicationUser> users =
                    scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                ApplicationUser administrator = new()
                {
                    UserName = "story125.t011.admin@example.com",
                    Email = "story125.t011.admin@example.com",
                    EmailConfirmed = true,
                };
                IdentityResult created = await users.CreateAsync(administrator, "Valid1!Pass");
                Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
                IdentityResult role = await users.AddToRoleAsync(administrator, RoleNames.Administrator);
                Assert.True(role.Succeeded, string.Join(", ", role.Errors.Select(error => error.Description)));
            }

            (SystemEntity system, RepositoryEntity first, RepositoryEntity second) =
                await SeedOrdinaryPairAsync(database);
            HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true,
            });
            if (login)
            {
                await LoginAsync(client);
            }

            return new PublicPreparedScanScenario(
                database,
                factory,
                client,
                system,
                first,
                second,
                CreateRequest(first, second),
                observations);
        }
        catch
        {
            factory.Dispose();
            await database.DisposeAsync();
            throw;
        }
    }

    private static PreparedScanRequest CreateRequest(
        RepositoryEntity first,
        RepositoryEntity second) => new()
        {
            Repositories =
            [
                new()
                {
                    RepositoryId = first.Id,
                    GitRef = "refs/tags/story125-one",
                    ProfiledRevision = "0123456789abcdef0123456789abcdef01234567",
                },
                new()
                {
                    RepositoryId = second.Id,
                    GitRef = "refs/tags/story125-two",
                    ProfiledRevision = "fedcba9876543210fedcba9876543210fedcba98",
                },
            ],
        };

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        Action<IServiceCollection>? configureServices = null,
        PublicPreparedScanObservations? observations = null)
    {
        StubPreparedRevisionAcquirer revisions = new();
        revisions.Handler = async (request, cancellationToken) =>
        {
            if (observations is not null)
            {
                int call = Interlocked.Increment(ref observations.RevisionAcquisitionCalls);
                if (observations.FailFirstPreparation && call == 1)
                {
                    return new PreparedRevisionAcquisitionResult(
                        null,
                        new PreparedScanFailure(
                            PreparedScanFailureReason.AnalysisNotReady,
                            "test-preparation-unavailable",
                            "induced post-initiation failure"));
                }
                await using VulgataDbContext context =
                    new(new DbContextOptionsBuilder<VulgataDbContext>()
                        .UseNpgsql(connectionString)
                        .Options);
                bool runVisible = await context.ScanRuns
                    .AsNoTracking()
                    .AnyAsync(run => run.Id == request.ScanRunId, cancellationToken);
                bool executionVisible = await context.ScanExecutions
                    .AsNoTracking()
                    .AnyAsync(execution => execution.ScanRunId == request.ScanRunId, cancellationToken);
                observations.BuilderEntryTupleVisible &= runVisible && executionVisible;
            }

            return new PreparedRevisionAcquisitionResult(
                new PreparedRevisionSnapshot(
                    request.RepositoryId,
                    request.ScanRunId,
                    request.RequestedRef,
                    request.ProfiledRevision,
                    new('a', 64),
                    $"snapshot-{request.RepositoryId:N}",
                    $"root-{request.RepositoryId:N}"),
                null);
        };

        StubPreparedSnapshotSource snapshots = new();
        snapshots.Handler = (snapshot, _) => Task.FromResult(new PreparedSnapshotReadResult(
            CreateNeutralSnapshotFiles(snapshot), null));

        StubPreparedCommunicationCatalogSource catalog = new();
        catalog.Handler = _ => Task.FromResult(new PreparedCatalogLoadResult(
            new CommunicationPatternCatalog(
                "communication-pattern-catalog/1.0",
                "story125-t011-catalog",
                "1",
                new('e', 64),
                []),
            null));

        StubPreparedCodeGraphAnalysisPort codeGraph = new();
        codeGraph.ProofHandler = (request, _) =>
        {
            if (observations is not null)
            {
                Interlocked.Increment(ref observations.CodeGraphCalls);
            }

            return Task.FromResult(new PreparedCodeGraphProofResult(
            AnalysisOperationStatus.Succeeded,
            new CodeGraphCompletenessProof(
                request.Scope.RepositoryId,
                request.Scope.ScanRunId,
                request.Scope.ProfiledRevision,
                request.SnapshotIdentity,
                request.SnapshotRootBindingHash,
                request.Scope.RuntimeIdentityHash,
                "story125-t011-index",
                "codegraph-completeness-proof/1.0",
                true,
                CodeGraphIndexState.Complete,
                0,
                0,
                0,
                false,
                false,
                0,
                64,
                request.LogicalProjectBinding,
                request.LogicalIndexBinding,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                new('b', 64),
                0),
            null));
        };

        StubPreparedJavaReadinessPort java = new();
        java.Handler = (request, _) =>
        {
            if (observations is not null)
            {
                Interlocked.Increment(ref observations.JavaReadinessCalls);
            }

            return Task.FromResult(new PreparedJavaReadinessResult(
            AnalysisOperationStatus.Succeeded,
            new JavaReadinessProof(
                request.Scope.RepositoryId,
                request.Scope.ScanRunId,
                request.Scope.ProfiledRevision,
                request.SnapshotIdentity,
                request.Scope.RuntimeIdentityHash,
                request.WorkspaceBindingHash,
                "java-readiness/1.0",
                JavaReadinessOutcome.NotApplicable,
                "jdt-language-server-1.61.0-202608141332",
                new('c', 64),
                21,
                "2.25.29",
                AnalysisTransport.Stdio,
                false,
                null,
                false,
                false,
                JavaImportState.NotApplicable,
                0,
                true,
                true,
                true,
                true,
                true,
                new('d', 64),
                0),
            null));
        };

        return new PreparedScanInitiationPostgreSqlWebApplicationFactory(connectionString)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
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
                services.AddSingleton<IPreparedTopLevelSymbolSource>(
                    new StubPreparedTopLevelSymbolSource());
                configureServices?.Invoke(services);
            }));
    }

    private static async Task<(SystemEntity System, RepositoryEntity First, RepositoryEntity Second)>
        SeedOrdinaryPairAsync(PreparedScanInitiationPostgreSqlFixture database)
    {
        await using VulgataDbContext context = database.CreateContext();
        DateTimeOffset now = PreparedScanTestHost.FixedUtcNow;
        SystemEntity system = new("Story 12.5 T011 system", "ordinary system", "T011", now);
        RepositoryEntity first = system.AddRepository(
            "story125-t011-one", "https://example.invalid/story125-one.git", null, "one", now);
        RepositoryEntity second = system.AddRepository(
            "story125-t011-two", "https://example.invalid/story125-two.git", null, "two", now);
        context.Systems.Add(system);
        await context.SaveChangesAsync();
        return (system, first, second);
    }

    private static async Task<PreparedScanInitiationCounts> ReadCountsAsync(
        PreparedScanInitiationPostgreSqlFixture database)
    {
        await using VulgataDbContext context = database.CreateContext();
        return new(
            await context.ScanRuns.CountAsync(),
            await context.PreScanResults.CountAsync(),
            await context.ScanExecutions.CountAsync(),
            await context.TwoRepositoryScanCoordinations.CountAsync(),
            await context.PreparedScanManifests.CountAsync(),
            await context.CodeUnits.CountAsync(),
            await context.LogicalDocuments.CountAsync());
    }

    private static IReadOnlyList<PreparedSnapshotFile> CreateNeutralSnapshotFiles(
        PreparedRevisionSnapshot snapshot)
    {
        List<PreparedSnapshotFile> files = new(60);
        for (int index = 0; index < 60; index++)
        {
            string relativePath = $"src/story125/entry-{index:00}.xml";
            string source = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<configuration><entry name=\"{snapshot.RepositoryId:N}-{index:00}\" /></configuration>\n";
            byte[] bytes = Encoding.UTF8.GetBytes(source);
            string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            files.Add(new PreparedSnapshotFile(
                relativePath,
                "XML",
                IsEligible: true,
                "EligibleProductionSource",
                hash,
                hash,
                bytes.LongLength,
                source.Count(character => character == '\n'),
                "utf-8",
                bytes));
        }

        return files;
    }

    private static async Task LoginAsync(HttpClient client)
    {
        using HttpResponseMessage page = await client.GetAsync("/Account/Login");
        page.EnsureSuccessStatusCode();
        string html = await page.Content.ReadAsStringAsync();
        Match form = Regex.Match(
            html,
            "<form[^>]*method=\"post\"[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, "The login form was not rendered.");
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(
                     form.Groups["content"].Value,
                     "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"[^>]*>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            fields[input.Groups["name"].Value] = input.Groups["value"].Value;
        }

        fields["Input.Email"] = "story125.t011.admin@example.com";
        fields["Input.Password"] = "Valid1!Pass";
        fields["Input.RememberMe"] = "false";
        using FormUrlEncodedContent content = new(fields);
        using HttpResponseMessage response = await client.PostAsync("/Account/Login", content);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}

internal readonly record struct PreparedScanInitiationCounts(
    int ScanRuns,
    int PreScanResults,
    int ScanExecutions,
    int Coordinations,
    int Manifests,
    int CodeUnits,
    int LogicalDocuments);

internal sealed class StubPreparedTopLevelSymbolSource : IPreparedTopLevelSymbolSource
{
    public Task<PreparedJavaSourceResult> GetRangesAsync(
        AnalysisScope scope,
        string relativePath,
        string fileHash,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PreparedJavaSourceResult([], null));
}

internal sealed class PublicPreparedScanScenario : IAsyncDisposable
{
    internal PublicPreparedScanScenario(
        PreparedScanInitiationPostgreSqlFixture database,
        WebApplicationFactory<Program> factory,
        HttpClient client,
        SystemEntity system,
        RepositoryEntity first,
        RepositoryEntity second,
        PreparedScanRequest request,
        PublicPreparedScanObservations observations)
    {
        Database = database;
        Factory = factory;
        Client = client;
        System = system;
        First = first;
        Second = second;
        Request = request;
        Observations = observations;
    }

    internal PreparedScanInitiationPostgreSqlFixture Database { get; }

    internal WebApplicationFactory<Program> Factory { get; }

    internal HttpClient Client { get; }

    internal SystemEntity System { get; }

    internal RepositoryEntity First { get; }

    internal RepositoryEntity Second { get; }

    internal PreparedScanRequest Request { get; }

    internal PublicPreparedScanObservations Observations { get; }

    internal string Route => $"/api/systems/{System.Id:D}/prepared-scans";

    internal int CodeGraphCalls => Observations.CodeGraphCalls;

    internal int JavaReadinessCalls => Observations.JavaReadinessCalls;

    internal bool CgcResolved
    {
        get
        {
            Type? legacy = typeof(Program).Assembly.GetTypes()
                .SingleOrDefault(type => type.Name == "IRepositoryProfilingCoordinator");
            return legacy is not null && Factory.Services.GetService(legacy) is not null;
        }
    }

    internal bool BuilderEntryTupleVisible => Observations.BuilderEntryTupleVisible;

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        return Database.DisposeAsync();
    }
}

internal sealed class PublicPreparedScanObservations
{
    internal int RevisionAcquisitionCalls;

    internal int CodeGraphCalls;

    internal int JavaReadinessCalls;

    internal bool FailFirstPreparation { get; set; }

    internal bool BuilderEntryTupleVisible { get; set; } = true;
}

internal sealed class PublicPreparedScanRuntimeIdentityResolver : IPreparedScanRuntimeIdentityResolver
{
    public Task<PreparedScanRuntimeIdentityResolution> ResolveAsync(
        Guid repositoryId,
        string profiledRevision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AnalysisRuntimeIdentity identity = AnalysisRuntimeIdentity.Create(
            new AnalysisRuntimeOptions("codegraph", "jdt-ls", "java"),
            repositoryId,
            profiledRevision,
            "@colbymchenry/codegraph",
            "1.5.0",
            "https://github.com/colbymchenry/codegraph",
            "codegraph",
            "@colbymchenry/codegraph-win32-x64@1.5.0",
            [
                new("lib/dist/bin/codegraph.js", "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03"),
                new("node.exe", "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3"),
                new("npm-shim.js", "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877"),
            ],
            "jdt-language-server-1.61.0-202608141332",
            "jdt-language-server-1.61.0-202608141332.zip",
            new('e', 64),
            21,
            "StreamJsonRpc",
            "2.25.29",
            "stdio");
        return Task.FromResult(new PreparedScanRuntimeIdentityResolution(identity));
    }
}

internal sealed class CountingAuthorizationRechecker : IPreparedScanInitiationAuthorizationRechecker
{
    internal bool Deny { get; set; }

    internal int CallCount { get; private set; }

    public Task<PreparedScanInitiationAuthorizationResult> RecheckAsync(
        string userId,
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (Deny)
        {
            return Task.FromResult(PreparedScanInitiationAuthorizationResult.Denied);
        }

        return Task.FromResult(new PreparedScanInitiationAuthorizationResult(
            true,
            Guid.TryParse(userId, out Guid id) ? id : Guid.Empty,
            RoleNames.Administrator,
            repositoryIds.Order().ToArray()));
    }
}

internal sealed class CountingConflictInitiationStore : IPreparedScanInitiationStore
{
    internal int CallCount { get; private set; }

    public Task<PreparedScanInitiationResult> CreateOrReplayAsync(
        PreparedScanInitiationCommand command,
        CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(new PreparedScanInitiationResult(
            PreparedScanInitiationOutcome.Conflict));
    }
}

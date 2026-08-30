using System.Net;
using System.Net.Http.Json;
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
    public async Task PostPreparedScan_NoPreseededLifecycleState_CreatesCompletePairAndManifestsAsync()
    {
        await using PreparedScanInitiationPostgreSqlFixture database =
            await PreparedScanInitiationPostgreSqlFixture.CreateAsync();
        using WebApplicationFactory<Program> factory =
            CreateFactory(database.ConnectionString);

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

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 from the public initiation seam for owned database " +
            $"{database.DatabaseName}; actual status was {(int)response.StatusCode}.");
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        Assert.NotEqual(Guid.Empty, root.GetProperty("coordinationId").GetGuid());
        Assert.Equal(2, root.GetProperty("manifests").GetArrayLength());
        Assert.InRange(root.GetProperty("selectedCount").GetInt32(), 50, 150);

        PreparedScanInitiationCounts after = await ReadCountsAsync(database);
        Assert.Equal(new PreparedScanInitiationCounts(2, 0, 2, 1, 2, 0, 0), after);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString)
    {
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
        codeGraph.ProofHandler = (request, _) => Task.FromResult(new PreparedCodeGraphProofResult(
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

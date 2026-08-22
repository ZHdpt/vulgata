using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.Entities;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Shared.Scans;
using Vulgata.Tests.PreparedPass1.Fixtures;
using Vulgata.Tests.PreparedScanning;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T062 RED: authenticated, production-composed Start-to-terminal proof.
/// This boundary deliberately does not replace the dispatcher, claimed packet
/// source, source reader, document committer, provider probe, or analysis
/// adapters. A runtime/version qualification is insufficient if this path
/// cannot execute the approved two-Repository pair end to end.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1ProductionExecutionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task AuthenticatedApprovedPair_StartsRealRunAtEveryLimit_AndPersistsTerminalEvidence(
        int concurrencyLimit)
    {
        await using PreparedScanPostgreSqlFixture fixture = await PreparePairAsync();
        using WebApplicationFactory<Program> host = CreateProductionHost(
            fixture.ConnectionString);
        using HttpClient client = await CreateAuthenticatedClientAsync(host, fixture);

        Guid[] manifestIds = await fixture.Context.PreparedScanManifests
            .OrderBy(value => value.RepositoryId)
            .Select(value => value.Id)
            .ToArrayAsync();
        Assert.Equal(2, manifestIds.Length);

        using HttpResponseMessage start = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new
            {
                firstManifestId = manifestIds[0],
                secondManifestId = manifestIds[1],
                concurrencyLimit,
            });
        string startBody = await start.Content.ReadAsStringAsync();
        Assert.True(
            start.StatusCode == HttpStatusCode.Created,
            $"T062 RED: authenticated production Start was not Created; status={start.StatusCode}, body={startBody}");
        Pass1ExecutionStartResponse started =
            (await start.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
        Assert.Equal(concurrencyLimit, started.ConcurrencyLimit);
        Assert.False(started.IsReplay);

        Pass1ExecutionStatusResponse terminal = await WaitForTerminalAsync(
            client,
            started.Pass1ExecutionId,
            TimeSpan.FromSeconds(30));
        Assert.True(
            terminal.Phase is "Completed" or "CompletedWithWarnings",
            $"T062 RED: production Start did not reach terminal success; phase={terminal.Phase}, " +
            $"dispatched={terminal.DispatchedCount}, completed={terminal.CompletedCount}, " +
            $"failed={terminal.FailedCount}, warnings={terminal.Warnings.Count}, start={startBody}");
        Assert.True(terminal.DispatchedCount > 0, "T062 RED: no real Worker job was dispatched.");
        Assert.Equal(terminal.DispatchedCount, terminal.CompletedCount);
        Assert.Equal(0, terminal.FailedCount);

        await using VulgataDbContext verification =
            PreparedScanPostgreSqlFixture.CreateContext(fixture.ConnectionString);
        Pass1PairExecution pair = await verification.Pass1PairExecutions
            .AsNoTracking()
            .SingleAsync(value => value.Id == started.Pass1ExecutionId);
        Pass1Outcome outcome = await verification.Pass1Outcomes
            .AsNoTracking()
            .SingleAsync(value => value.PairExecutionId == pair.Id);
        Assert.Equal(Pass1OutcomeStatus.Succeeded, outcome.Status);
        Assert.Equal(outcome.DispatchedCount, outcome.CompletedCount);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ReconciliationFingerprint));

        Guid[] jobIds = await verification.ScanJobExecutions
            .AsNoTracking()
            .Where(value => verification.PreparedScanManifests
                .Where(manifest => manifest.Id == pair.ManifestOneId || manifest.Id == pair.ManifestTwoId)
                .Select(manifest => manifest.ScanRunId)
                .Contains(value.ScanRunId))
            .Select(value => value.Id)
            .ToArrayAsync();
        Assert.NotEmpty(jobIds);
        Guid[] documentIds = await verification.ScanJobExecutions
            .AsNoTracking()
            .Where(value => jobIds.Contains(value.Id) && value.CommittedDocumentVersionId != null)
            .Select(value => value.CommittedDocumentVersionId!.Value)
            .ToArrayAsync();
        Assert.NotEmpty(documentIds);
        var documents = await verification.DocumentVersions
            .AsNoTracking()
            .Where(value => documentIds.Contains(value.Id))
            .Select(value => new
            {
                value.Id,
                value.RepositoryRelativePath,
                value.ProfiledRevision,
                value.SchemaIdentity,
                value.ContentFingerprint,
                value.SupersededById,
            })
            .ToArrayAsync();
        Assert.NotEmpty(documents);
        Assert.All(documents, document =>
        {
            Assert.Equal("code-logic/file-v2", document.SchemaIdentity);
            Assert.Equal(PreparedPass1ManifestPairFixtures.ProfiledRevision, document.ProfiledRevision);
            Assert.Null(document.SupersededById);
        });

        var anchors = await verification.SourceAnchors
            .AsNoTracking()
            .Where(value => documents.Select(document => document.Id).Contains(value.DocumentVersionId))
            .ToArrayAsync();
        Assert.Contains(anchors, value => value.RepositoryRelativePath.EndsWith(
            ".xml",
            StringComparison.OrdinalIgnoreCase));
        Assert.Contains(anchors, value => value.RepositoryRelativePath.EndsWith(
            ".java",
            StringComparison.OrdinalIgnoreCase));
        Assert.All(anchors, anchor =>
        {
            Assert.Equal(PreparedPass1ManifestPairFixtures.ProfiledRevision, anchor.ProfiledRevision);
            Assert.Equal(64, anchor.SourceHash.Length);
            Assert.True(anchor.LineStart >= 1 && anchor.LineEnd >= anchor.LineStart);
        });

        var receipts = await verification.Pass1NavigationReceipts
            .AsNoTracking()
            .Where(value => value.PairExecutionId == pair.Id)
            .ToArrayAsync();
        Assert.Contains(receipts, value =>
            value.Operation == Pass1NavigationOperation.Implementations &&
            value.DescriptorJson.Contains(".java", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(receipts, value =>
            value.Operation == Pass1NavigationOperation.ExactSourceEvidence &&
            value.DescriptorJson.Contains(".xml", StringComparison.OrdinalIgnoreCase));
        Assert.All(receipts, receipt =>
        {
            Assert.InRange(receipt.ResultCount, 0, 20);
            Assert.InRange(receipt.EvidenceBytes, 0, 65_536);
            Assert.InRange(receipt.Depth ?? 0, 0, 3);
        });

        string[] fingerprints = documents
            .OrderBy(value => value.Id)
            .Select(value => value.ContentFingerprint)
            .ToArray();
        await Task.Delay(250);
        string[] replayFingerprints = await verification.DocumentVersions
            .AsNoTracking()
            .Where(value => documents.Select(document => document.Id).Contains(value.Id))
            .OrderBy(value => value.Id)
            .Select(value => value.ContentFingerprint)
            .ToArrayAsync();
        Assert.Equal(fingerprints, replayFingerprints);
    }

    internal static async Task<PreparedScanPostgreSqlFixture> PreparePairAsync()
    {
        string administrator = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN")
            ?? throw new InvalidOperationException(
                "T062 requires VULGATA_TEST_POSTGRES_ADMIN for PostgreSQL 17.");
        PreparedScanPostgreSqlFixture fixture =
            await PreparedScanPostgreSqlFixture.CreateAsync(administrator);

        // The fixture seeds the accepted Story 12.2 rows through the older
        // ScanExecution.Start constructor. Bind the execution-only runtime
        // fields before the atomic pair commit, preserving the approved
        // PreScanResult IDs and every committed manifest/context fingerprint.
        const string schema = "code-logic/file-v2";
        const string runtime = "{\"runtime\":\"story-12-3-production\"}";
        string runtimeHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(runtime)));
        await fixture.Context.ScanExecutions.ExecuteUpdateAsync(setters => setters
            .SetProperty(execution => execution.Phase, ScanExecutionPhase.Prepared)
            .SetProperty(execution => execution.PromptIdentity, "story-12-3-production-prompt")
            .SetProperty(execution => execution.DocumentSchemaIdentity, schema)
            .SetProperty(execution => execution.AnalysisRuntimeIdentityJson, runtime)
            .SetProperty(execution => execution.AnalysisRuntimeIdentityHash, runtimeHash)
            .SetProperty(execution => execution.UpdatedAt, PreparedScanTestHost.FixedUtcNow));

        PreparedScanCommitResult committed = await PreparedScanPostgreSqlFixture
            .CreateStore(fixture.Context)
            .CommitPairAsync(fixture.Authorization, fixture.Package, 0, CancellationToken.None);
        Assert.Equal(PreparedScanCommitOutcome.Committed, committed.Outcome);

        return fixture;
    }

    internal static WebApplicationFactory<Program> CreateProductionHost(
        string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            string keyRingPath = Path.Combine(
                Path.GetTempPath(),
                "vulgata-tests",
                "t062-data-protection",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(keyRingPath);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["DataProtection:KeyRingPath"] = keyRingPath,
                    ["DataProtection:ApplicationName"] = "Vulgata.Tests.T062." + Guid.NewGuid().ToString("N"),
                }));
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseSetting("Logging:EventLog:LogLevel:Default", "None");
            builder.UseSetting("Logging:LogLevel:Default", "None");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<VulgataDbContext>>();
                services.RemoveAll<IDbContextFactory<VulgataDbContext>>();
                services.RemoveAll<IConfigureOptions<DbContextOptions<ApplicationDbContext>>>();
                services.RemoveAll<IConfigureOptions<DbContextOptions<VulgataDbContext>>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<VulgataDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<VulgataDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                        npgsql.MigrationsHistoryTable("__IdentityMigrationsHistory", "identity");
                    }));
                services.AddDbContext<VulgataDbContext>(options => options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    }),
                    contextLifetime: ServiceLifetime.Scoped,
                    optionsLifetime: ServiceLifetime.Singleton);
                services.AddDbContextFactory<VulgataDbContext>(options => options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    }));
                services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                    .SetApplicationName("Vulgata.Tests.T062." + Guid.NewGuid().ToString("N"));
            });
        });

    internal static async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactory<Program> host,
        PreparedScanPostgreSqlFixture fixture)
    {
        string email = $"pass1-t062-{Guid.NewGuid():N}@example.com";
        const string password = "Valid1!Pass";
        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> users =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = new() { UserName = email, Email = email, EmailConfirmed = true };
            IdentityResult result = await users.CreateAsync(user, password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
            await users.AddToRoleAsync(user, RoleNames.SystemOwner);
            VulgataDbContext domain = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            domain.SystemOwnerAssignments.Add(SystemOwnerAssignment.Create(
                fixture.SystemId, user.Id, PreparedScanTestHost.FixedUtcNow));
            await domain.SaveChangesAsync();
        }

        HttpClient client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        using HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
        string html = await loginPage.Content.ReadAsStringAsync();
        Match form = Regex.Match(
            html,
            "<form[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, $"T062 authenticated host login unavailable: {loginPage.StatusCode}.");
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (Match input in Regex.Matches(
            form.Groups["content"].Value,
            "<input[^>]*>",
            RegexOptions.IgnoreCase))
        {
            Match name = Regex.Match(input.Value, "name=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
            if (!name.Success)
            {
                continue;
            }

            Match value = Regex.Match(input.Value, "value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase);
            fields[name.Groups["value"].Value] = value.Success
                ? WebUtility.HtmlDecode(value.Groups["value"].Value)
                : string.Empty;
        }
        fields["Input.Email"] = email;
        fields["Input.Password"] = password;
        fields["Input.RememberMe"] = "false";
        using FormUrlEncodedContent login = new(fields);
        using HttpResponseMessage response = await client.PostAsync("/Account/Login", login);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<Pass1ExecutionStatusResponse> WaitForTerminalAsync(
        HttpClient client,
        Guid executionId,
        TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        Pass1ExecutionStatusResponse? last = null;
        while (DateTime.UtcNow < deadline)
        {
            using HttpResponseMessage response = await client.GetAsync(
                $"/api/pass1-executions/{executionId:D}");
            string body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            last = (await response.Content.ReadFromJsonAsync<Pass1ExecutionStatusResponse>())!;
            if (last.Phase is "Completed" or "CompletedWithWarnings" or "Failed" or
                "FailedIntegrity" or "Paused" or "PausedProviderUnavailable")
            {
                return last;
            }

            await Task.Delay(250);
        }

        throw new Xunit.Sdk.XunitException(
            $"T062 RED: production pair did not reach terminal state before timeout; " +
            $"last phase={last?.Phase ?? "unavailable"}.");
    }

}

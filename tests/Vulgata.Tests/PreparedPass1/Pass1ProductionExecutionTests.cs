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
    [T062HostQualificationTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task AuthenticatedApprovedPair_StartsRealRunAtEveryLimit_AndPersistsTerminalEvidence(
        int concurrencyLimit)
    {
        RequireRealHostQualificationPrerequisites();
        T062QualificationInputs qualification = await LoadQualificationInputsAsync();
        using WebApplicationFactory<Program> host = CreateProductionHost(
            qualification.ConnectionString);
        await ConfigureWorkerProviderAsync(
            host,
            qualification.SystemId,
            qualification.ProviderEndpoint,
            qualification.ProviderModel);
        using HttpClient client = await CreateAuthenticatedClientAsync(
            host,
            qualification.SystemId);

        Guid[] manifestIds = qualification.ManifestIds.ToArray();
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
            PreparedScanPostgreSqlFixture.CreateContext(qualification.ConnectionString);
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
                value.RepositoryId,
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
            T062RepositoryQualification repository = qualification.ForRepository(document.RepositoryId);
            Assert.Equal("code-logic/file-v2", document.SchemaIdentity);
            Assert.Equal(repository.ProfiledRevision, document.ProfiledRevision);
            Assert.Null(document.SupersededById);
            Assert.True(
                File.Exists(Path.Combine(
                    repository.SourceRoot,
                    document.RepositoryRelativePath.Replace('/', Path.DirectorySeparatorChar))),
                $"T062 document path is not present in its approved Repository root: " +
                $"repository={document.RepositoryId:D}, path={document.RepositoryRelativePath}");
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
            T062RepositoryQualification repository = qualification.ForRepository(anchor.RepositoryId);
            Assert.Equal(repository.ProfiledRevision, anchor.ProfiledRevision);
            Assert.Equal(64, anchor.SourceHash.Length);
            Assert.True(anchor.LineStart >= 1 && anchor.LineEnd >= anchor.LineStart);
            Assert.True(
                File.Exists(Path.Combine(
                    repository.SourceRoot,
                    anchor.RepositoryRelativePath.Replace('/', Path.DirectorySeparatorChar))),
                $"T062 Source Anchor path is not present in its approved Repository root: " +
                $"repository={anchor.RepositoryId:D}, path={anchor.RepositoryRelativePath}");
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

    internal static async Task<T062QualificationInputs> LoadQualificationInputsAsync()
    {
        RequireRealHostQualificationPrerequisites();
        string connectionString = Environment.GetEnvironmentVariable(
            "VULGATA_T062_QUALIFICATION_CONNECTION")!;
        Guid systemId = ParseRequiredGuid("VULGATA_T062_SYSTEM_ID");
        Guid[] manifestIds =
        [
            ParseRequiredGuid("VULGATA_T062_MANIFEST_ONE_ID"),
            ParseRequiredGuid("VULGATA_T062_MANIFEST_TWO_ID"),
        ];
        string[] roots =
        [
            Environment.GetEnvironmentVariable("VULGATA_T062_REPOSITORY_ONE_ROOT")!,
            Environment.GetEnvironmentVariable("VULGATA_T062_REPOSITORY_TWO_ROOT")!,
        ];
        Uri providerEndpoint = new(
            Environment.GetEnvironmentVariable("VULGATA_T062_WORKER_PROVIDER_ENDPOINT")!);
        string providerModel = Required("VULGATA_T062_WORKER_PROVIDER_MODEL");

        await using VulgataDbContext context =
            PreparedScanPostgreSqlFixture.CreateContext(connectionString);
        PreparedScanManifest[] manifests = await context.PreparedScanManifests
            .Where(manifest => manifestIds.Contains(manifest.Id))
            .Include(manifest => manifest.Entries)
            .ThenInclude(entry => entry.FileCodeUnit)
            .AsNoTracking()
            .ToArrayAsync();
        Assert.Equal(2, manifests.Length);
        Assert.Equal(2, manifests.Select(manifest => manifest.RepositoryId).Distinct().Count());
        Assert.All(manifests, manifest =>
        {
            Assert.Equal(PreparedScanManifestApprovalState.Approved, manifest.ApprovalState);
            Assert.Equal(PreparedScanManifest.CurrentSchemaIdentity, manifest.SchemaIdentity);
            Assert.True(manifest.SelectedCount >= 25);
        });

        Assert.Equal(2, manifestIds.Distinct().Count());
        Dictionary<Guid, string> rootsByManifest = manifestIds
            .Zip(roots)
            .ToDictionary(value => value.First, value => value.Second);
        T062RepositoryQualification[] repositories = manifests
            .Select(manifest =>
            {
                Assert.True(
                    rootsByManifest.TryGetValue(manifest.Id, out string? root),
                    $"T062 loaded an unexpected manifest: {manifest.Id:D}");
                Assert.False(
                    string.IsNullOrWhiteSpace(manifest.ProfiledRevision),
                    $"T062 manifest has no Profiled Revision: {manifest.Id:D}");
                return new T062RepositoryQualification(
                    manifest.Id,
                    manifest.RepositoryId,
                    root!,
                    manifest.ProfiledRevision);
            })
            .OrderBy(value => value.RepositoryId)
            .ToArray();
        Assert.Equal(2, repositories.Select(value => value.RepositoryId).Distinct().Count());
        int exactJavaXmlCount = 0;
        foreach (T062RepositoryQualification repository in repositories)
        {
            PreparedScanManifest manifest = manifests.Single(value => value.Id == repository.ManifestId);
            foreach (PreparedScanManifestEntry entry in manifest.Entries)
            {
                CodeUnit unit = entry.FileCodeUnit
                    ?? throw new Xunit.Sdk.XunitException("T062 manifest entry has no File Code Unit.");
                string fullPath = Path.Combine(
                    repository.SourceRoot,
                    unit.FilePath.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(fullPath), $"T062 source byte prerequisite missing: {fullPath}");
                byte[] bytes = await File.ReadAllBytesAsync(fullPath);
                string sourceHash = Convert.ToHexStringLower(
                    System.Security.Cryptography.SHA256.HashData(bytes));
                Assert.Equal(sourceHash, unit.SourceHash);
                Assert.Equal(sourceHash, unit.FileHash);
                if (unit.FilePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase) ||
                    unit.FilePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    exactJavaXmlCount++;
                }
            }
        }

        Assert.True(exactJavaXmlCount >= 2, "T062 requires exact Java and XML source evidence in the approved pair.");
        return new(
            connectionString,
            systemId,
            manifestIds,
            repositories,
            providerEndpoint,
            providerModel);
    }

    private static Guid ParseRequiredGuid(string variableName) =>
        Guid.TryParse(Environment.GetEnvironmentVariable(variableName), out Guid value) && value != Guid.Empty
            ? value
            : throw new Xunit.Sdk.XunitException($"T062 requires a non-empty {variableName}.");

    private static string Required(string variableName) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variableName))
            ? Environment.GetEnvironmentVariable(variableName)!
            : throw new Xunit.Sdk.XunitException($"T062 requires a non-empty {variableName}.");

    internal static async Task ConfigureWorkerProviderAsync(
        WebApplicationFactory<Program> host,
        Guid systemId,
        Uri endpoint,
        string model)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        VulgataDbContext context = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
        ISecretEncryptionService encryption =
            scope.ServiceProvider.GetRequiredService<ISecretEncryptionService>();
        string apiKey = Environment.GetEnvironmentVariable("VULGATA_T062_WORKER_PROVIDER_API_KEY")
            ?? throw new Xunit.Sdk.XunitException(
                "T062 requires VULGATA_T062_WORKER_PROVIDER_API_KEY for the controlled Worker provider.");
        string encryptedApiKey = await encryption.EncryptAsync(SecretPurposes.LlmApiKey, apiKey);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        LlmProvider? provider = await context.LlmProviders
            .SingleOrDefaultAsync(value => value.Name == model);
        if (provider is null)
        {
            provider = new LlmProvider(
                model,
                endpoint.ToString(),
                encryptedApiKey,
                ApiType.ChatCompletions,
                AgentType.Worker,
                now);
            context.LlmProviders.Add(provider);
        }
        else
        {
            provider.UpdateDetails(
                model,
                endpoint.ToString(),
                encryptedApiKey,
                ApiType.ChatCompletions,
                AgentType.Worker,
                now);
        }

        SystemLlmProviderOverride? providerOverride = await context.SystemLlmProviderOverrides
            .SingleOrDefaultAsync(value => value.SystemId == systemId && value.AgentType == AgentType.Worker);
        if (providerOverride is null)
        {
            context.SystemLlmProviderOverrides.Add(
                SystemLlmProviderOverride.Create(systemId, provider.Id, AgentType.Worker, now));
        }
        else
        {
            providerOverride.Update(provider.Id, AgentType.Worker, now);
        }
        await context.SaveChangesAsync();
    }

    internal static void RequireRealHostQualificationPrerequisites()
    {
        string? reason = HostQualificationSkipReason();
        if (reason is not null)
        {
            SkipQualification(reason);
        }

        string firstRoot = Environment.GetEnvironmentVariable("VULGATA_T062_REPOSITORY_ONE_ROOT")!;
        string secondRoot = Environment.GetEnvironmentVariable("VULGATA_T062_REPOSITORY_TWO_ROOT")!;
        foreach (string root in new[] { firstRoot!, secondRoot! })
        {
            if (!Directory.Exists(root))
            {
                SkipQualification($"T062 host qualification skipped: source root is unavailable: {root}.");
            }

            string[] files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            if (files.Count(file => file.EndsWith(".java", StringComparison.OrdinalIgnoreCase)) == 0 ||
                files.Count(file => file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) == 0)
            {
                SkipQualification(
                    $"T062 host qualification skipped: source root must contain both Java and XML files: {root}.");
            }
        }
    }

    internal static string? HostQualificationSkipReason()
    {
        string? qualificationConnection = Environment.GetEnvironmentVariable(
            "VULGATA_T062_QUALIFICATION_CONNECTION");
        string? postgresAdmin = Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        string? systemId = Environment.GetEnvironmentVariable("VULGATA_T062_SYSTEM_ID");
        string? manifestOneId = Environment.GetEnvironmentVariable("VULGATA_T062_MANIFEST_ONE_ID");
        string? manifestTwoId = Environment.GetEnvironmentVariable("VULGATA_T062_MANIFEST_TWO_ID");
        string? firstRoot = Environment.GetEnvironmentVariable("VULGATA_T062_REPOSITORY_ONE_ROOT");
        string? secondRoot = Environment.GetEnvironmentVariable("VULGATA_T062_REPOSITORY_TWO_ROOT");
        string? providerEndpoint = Environment.GetEnvironmentVariable("VULGATA_T062_WORKER_PROVIDER_ENDPOINT");
        string? providerModel = Environment.GetEnvironmentVariable("VULGATA_T062_WORKER_PROVIDER_MODEL");
        string? sourceBinding = Environment.GetEnvironmentVariable("VULGATA_T062_SOURCE_BINDING_READY");
        if (!string.Equals(
                Environment.GetEnvironmentVariable("VULGATA_T062_APPROVED_PAIR_REAL"),
                "1",
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(qualificationConnection) ||
            string.IsNullOrWhiteSpace(postgresAdmin) ||
            !Guid.TryParse(systemId, out Guid parsedSystemId) || parsedSystemId == Guid.Empty ||
            !Guid.TryParse(manifestOneId, out Guid parsedManifestOneId) || parsedManifestOneId == Guid.Empty ||
            !Guid.TryParse(manifestTwoId, out Guid parsedManifestTwoId) || parsedManifestTwoId == Guid.Empty ||
            parsedManifestOneId == parsedManifestTwoId ||
            string.IsNullOrWhiteSpace(firstRoot) || string.IsNullOrWhiteSpace(secondRoot) ||
            string.IsNullOrWhiteSpace(providerEndpoint) || string.IsNullOrWhiteSpace(providerModel) ||
            !string.Equals(sourceBinding, "1", StringComparison.Ordinal) ||
            !Uri.TryCreate(providerEndpoint, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            return
                "T062 host qualification skipped: set VULGATA_T062_APPROVED_PAIR_REAL=1, " +
                "VULGATA_T062_QUALIFICATION_CONNECTION, VULGATA_T062_SYSTEM_ID, " +
                "VULGATA_T062_MANIFEST_ONE_ID, and VULGATA_T062_MANIFEST_TWO_ID, plus " +
                "VULGATA_T062_REPOSITORY_ONE_ROOT, VULGATA_T062_REPOSITORY_TWO_ROOT, " +
                "VULGATA_T062_WORKER_PROVIDER_ENDPOINT, VULGATA_T062_WORKER_PROVIDER_MODEL, and " +
                "VULGATA_T062_SOURCE_BINDING_READY=1. " +
                "The roots must be the approved profiled revisions with revision-bound Java/XML " +
                "bytes; the endpoint must be an explicitly controlled Worker provider. PostgreSQL " +
                "17 still requires VULGATA_TEST_POSTGRES_ADMIN.";
        }

        foreach (string root in new[] { firstRoot!, secondRoot! })
        {
            if (!Directory.Exists(root))
            {
                return $"T062 host qualification skipped: source root is unavailable: {root}.";
            }

            string[] files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            if (files.Count(file => file.EndsWith(".java", StringComparison.OrdinalIgnoreCase)) == 0 ||
                files.Count(file => file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) == 0)
            {
                return
                    $"T062 host qualification skipped: source root must contain both Java and XML files: {root}.";
            }
        }

        return null;
    }

    private static void SkipQualification(string message)
    {
        throw Xunit.Sdk.SkipException.ForSkip(message);
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
        Guid systemId)
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
                systemId, user.Id, DateTimeOffset.UtcNow));
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
            "<form\\b(?=[^>]*\\bmethod\\s*=\\s*[\"']post[\"'])[^>]*>(?<content>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(form.Success, $"T062 authenticated host login unavailable: {loginPage.StatusCode}.");
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

internal sealed record T062QualificationInputs(
    string ConnectionString,
    Guid SystemId,
    IReadOnlyList<Guid> ManifestIds,
    IReadOnlyList<T062RepositoryQualification> Repositories,
    Uri ProviderEndpoint,
    string ProviderModel)
{
    public T062RepositoryQualification ForRepository(Guid repositoryId) =>
        Repositories.Single(value => value.RepositoryId == repositoryId);
}

internal sealed record T062RepositoryQualification(
    Guid ManifestId,
    Guid RepositoryId,
    string SourceRoot,
    string ProfiledRevision);

internal sealed class T062HostQualificationTheoryAttribute : TheoryAttribute
{
    public T062HostQualificationTheoryAttribute() => Skip =
        Pass1ProductionExecutionTests.HostQualificationSkipReason();
}

internal sealed class T062HostQualificationFactAttribute : FactAttribute
{
    public T062HostQualificationFactAttribute() => Skip =
        Pass1ProductionExecutionTests.HostQualificationSkipReason();
}

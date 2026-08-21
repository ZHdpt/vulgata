using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Tests;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// T036 RED: observes the public Web composition and host boundary for the
/// configured Analysis Runtime.  The startup health/composition assertions are
/// intentionally expected to fail until T037 wires the runtime into Program.
/// </summary>
public sealed class AnalysisRuntimeHealthTests
    : IClassFixture<LoginLogoutTests.CustomWebApplicationFactory>
{
    private static readonly Guid _repositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000036");

    private static readonly Guid _scanRunId =
        Guid.Parse("12000000-0000-4000-8000-000000000037");

    private readonly LoginLogoutTests.CustomWebApplicationFactory _factory;

    public AnalysisRuntimeHealthTests(LoginLogoutTests.CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProductionComposition_RegistersClosedAnalysisPortsAndHealthProbe()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetService<ICodeGraphAnalysisPort>());
        Assert.NotNull(scope.ServiceProvider.GetService<IJavaAnalysisSessionFactory>());
        Assert.NotNull(scope.ServiceProvider.GetService<IRevisionBoundSourceReader>());
        Assert.NotNull(scope.ServiceProvider.GetService<IAnalysisRuntimeHealthProbe>());
    }

    [Fact]
    public async Task ConfiguredIntegrityHealth_MissingJdtArchive_IsBoundedNotReadyAndSanitized()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IAnalysisRuntimeHealthProbe probe =
            scope.ServiceProvider.GetRequiredService<IAnalysisRuntimeHealthProbe>();

        AnalysisHealthProbeResult result = await probe.CheckConfiguredIntegrityAsync(
            CreateScope(),
            CancellationToken.None);

        Assert.Equal(AnalysisRuntimeHealthStatus.NotReady, result.Status);
        Assert.Null(result.RuntimeIdentityHash);
        Assert.Contains(
            result.FailureCode,
            new[]
            {
                "jdt_distribution_archive_missing",
                "jdt_distribution_archive_checksum_missing",
            });
        Assert.InRange(result.Diagnostic.Length, 0, 256);
        Assert.DoesNotContain("C:\\Users", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("G:\\", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stderr", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\n', result.Diagnostic);
        Assert.InRange(result.ComponentChecks.Count, 1, 32);
        Assert.Equal(
            result.ComponentChecks.OrderBy(check => check.Component, StringComparer.Ordinal),
            result.ComponentChecks);
    }

    [Fact]
    public async Task OrdinaryStartup_DoesNotCreateIndexOrImportRepository()
    {
        string program = ReadRepositoryFile("src", "dotnet", "Vulgata.Web", "Program.cs");
        Assert.DoesNotContain(
            "CodeGraphContextProvider",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AddHttpClient<ICodeGraphProvider",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ImportProject",
            program,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManagementSurface_RemainsAvailableWhileRuntimeIsNotReady()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        using HttpResponseMessage response = await client.GetAsync("/management");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/Account/Login",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkerDispatch_IsClosedBeforeRuntimeReadiness()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IAnalysisRuntimeHealthProbe probe =
            scope.ServiceProvider.GetRequiredService<IAnalysisRuntimeHealthProbe>();
        AnalysisHealthProbeResult health = await probe.CheckConfiguredIntegrityAsync(
            CreateScope(),
            CancellationToken.None);

        Assert.Equal(AnalysisRuntimeHealthStatus.NotReady, health.Status);

        string program = ReadRepositoryFile("src", "dotnet", "Vulgata.Web", "Program.cs");
        Assert.Contains("IWorkerAgentDispatcher", program, StringComparison.Ordinal);
        Assert.Contains("NotReady", program, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultCompose_UsesPostgreSqlOnlyAndExplicitPostDeadlineAppProfile()
    {
        string compose = ReadRepositoryFile("docker-compose.yml");

        Assert.Contains("profiles:", compose, StringComparison.Ordinal);
        Assert.Contains("post-deadline-app", compose, StringComparison.Ordinal);
        Assert.Contains("postgres:", compose, StringComparison.Ordinal);

        int appProfileIndex = compose.IndexOf("post-deadline-app", StringComparison.Ordinal);
        int postgresIndex = compose.IndexOf("postgres:", StringComparison.Ordinal);
        Assert.True(appProfileIndex >= 0);
        Assert.True(postgresIndex >= 0);
        Assert.True(
            postgresIndex < appProfileIndex,
            "The default compose graph must leave PostgreSQL as the unprofiled service.");
    }

    [Fact]
    public void ProductionComposition_DoesNotRequireHistoricalCodeGraphContextOrPython()
    {
        string program = ReadRepositoryFile("src", "dotnet", "Vulgata.Web", "Program.cs");
        string compose = ReadRepositoryFile("docker-compose.yml");

        Assert.DoesNotContain("CodeGraphContextProvider", program, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeGraphProvider", program, StringComparison.Ordinal);
        Assert.DoesNotContain("python", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CodeGraphContext", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("python", compose, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeIdentityFailureCategories_RejectWrongVersionHashJavaAndWorkspace()
    {
        AnalysisRuntimeOptions options = new(
            CodeGraphCommandPath: @"G:\\packages\\npm\\prefix\\codegraph.ps1",
            JdtLsInstallationRoot:
                @"C:\\Users\\13728\\.local\\jdt-language-server-1.61.0-202608141332",
            JavaHome: @"C:\\Program Files\\Java\\latest\\jdk-21");

        Assert.Throws<ArgumentException>(() => CreateIdentity(options, javaMajorVersion: 17));
        Assert.Throws<ArgumentException>(() => CreateIdentity(options, clientVersion: "2.25.28"));
        Assert.Throws<ArgumentException>(() => CreateIdentity(options, codeGraphVersion: "1.5.1"));
        Assert.Throws<ArgumentException>(() => CreateIdentity(options, revision: "../../workspace"));
        Assert.Throws<ArgumentException>(() => CreateIdentity(
            options,
            archiveChecksum:
                "A58994113B7E493AA8CED51AD02DAB3E424973223E7213F7ED5B9D70861582E2"));
    }

    [Fact]
    public async Task ConfiguredIntegrityHealth_ValidSuppliedRuntimeIdentity_ReturnsReady()
    {
        using WebApplicationFactory<Program> factory = CreateConfiguredRuntimeFactory(
            CreateValidRuntimeConfiguration());
        using IServiceScope scope = factory.Services.CreateScope();
        IAnalysisRuntimeHealthProbe probe =
            scope.ServiceProvider.GetRequiredService<IAnalysisRuntimeHealthProbe>();

        AnalysisHealthProbeResult result = await probe.CheckConfiguredIntegrityAsync(
            CreateScope(),
            CancellationToken.None);

        Assert.Equal(AnalysisRuntimeHealthStatus.Ready, result.Status);
        Assert.Equal("none", result.FailureCode);
        Assert.Equal(string.Empty, result.Diagnostic);
        Assert.Equal(
            new[] { "codegraph", "java", "jdt-ls", "streamjsonrpc" },
            result.ComponentChecks.Select(check => check.Component));
        Assert.All(
            result.ComponentChecks,
            check => Assert.Equal(AnalysisRuntimeHealthStatus.Ready, check.Status));
    }

    [Fact]
    public async Task ConfiguredIntegrityHealth_ReadyIdentityHash_IsCanonicalAndBoundToScope()
    {
        using WebApplicationFactory<Program> factory = CreateConfiguredRuntimeFactory(
            CreateValidRuntimeConfiguration());
        using IServiceScope scope = factory.Services.CreateScope();
        IAnalysisRuntimeHealthProbe probe =
            scope.ServiceProvider.GetRequiredService<IAnalysisRuntimeHealthProbe>();

        AnalysisHealthProbeResult first = await probe.CheckConfiguredIntegrityAsync(
            CreateScope(),
            CancellationToken.None);
        AnalysisHealthProbeResult second = await probe.CheckConfiguredIntegrityAsync(
            CreateScope(
                Guid.Parse("12000000-0000-4000-8000-000000000038"),
                "fedcba9876543210fedcba9876543210fedcba98"),
            CancellationToken.None);

        Assert.Equal(AnalysisRuntimeHealthStatus.Ready, first.Status);
        Assert.Equal(AnalysisRuntimeHealthStatus.Ready, second.Status);
        Assert.Matches("^[0-9a-f]{64}$", first.RuntimeIdentityHash ?? string.Empty);
        Assert.Matches("^[0-9a-f]{64}$", second.RuntimeIdentityHash ?? string.Empty);
        Assert.NotEqual(first.RuntimeIdentityHash, second.RuntimeIdentityHash);
    }

    [Fact]
    public async Task ConfiguredIntegrityHealth_RepresentativeIdentityMismatches_FailClosed()
    {
        (string Key, string Value, string FailureMarker)[] mismatches =
        [
            ("AnalysisRuntime:JdtLsDistributionArchivePath",
                @"C:\Users\13728\Downloads\missing-jdt-language-server.tar.gz", "archive"),
            ("AnalysisRuntime:JdtLsDistributionArchiveName",
                "jdt-language-server-1.61.0-202608141332.zip", "archive"),
            ("AnalysisRuntime:JdtLsDistributionArchiveSha256", new string('0', 64), "archive"),
            ("AnalysisRuntime:JavaMajorVersion", "17", "java"),
            ("AnalysisRuntime:JdtLsReleaseIdentity", "jdt-language-server-unapproved", "jdt"),
            ("AnalysisRuntime:JdtLsLauncher", "org.eclipse.equinox.launcher-unapproved.jar", "jdt"),
            ("AnalysisRuntime:JdtLsConfiguration", "config_linux", "jdt"),
            ("AnalysisRuntime:StreamJsonRpcName", "unapproved-client", "streamjsonrpc"),
            ("AnalysisRuntime:StreamJsonRpcVersion", "2.25.28", "streamjsonrpc"),
            ("AnalysisRuntime:CodeGraphCommand", "unapproved-codegraph", "codegraph"),
            ("AnalysisRuntime:CodeGraphPackageName", "@unapproved/codegraph", "codegraph"),
            ("AnalysisRuntime:CodeGraphPackageVersion", "1.5.1", "codegraph"),
            ("AnalysisRuntime:CodeGraphBundle", "@unapproved/codegraph-win32-x64@1.5.0", "codegraph"),
            ("AnalysisRuntime:CodeGraphHashes:npm-shim.js", new string('0', 64), "codegraph"),
        ];

        List<string> failures = [];
        foreach ((string key, string value, string failureMarker) in mismatches)
        {
            Dictionary<string, string?> configuration = CreateValidRuntimeConfiguration();
            configuration[key] = value;

            try
            {
                using WebApplicationFactory<Program> factory =
                    CreateConfiguredRuntimeFactory(configuration);
                using IServiceScope scope = factory.Services.CreateScope();
                IAnalysisRuntimeHealthProbe probe =
                    scope.ServiceProvider.GetRequiredService<IAnalysisRuntimeHealthProbe>();
                AnalysisHealthProbeResult result = await probe.CheckConfiguredIntegrityAsync(
                    CreateScope(),
                    CancellationToken.None);

                if (result.Status != AnalysisRuntimeHealthStatus.NotReady ||
                    !result.FailureCode.Contains(failureMarker, StringComparison.OrdinalIgnoreCase) ||
                    result.Diagnostic.Length > 256 ||
                    result.Diagnostic.Contains('\\') ||
                    result.Diagnostic.Contains('/') ||
                    result.Diagnostic.Contains("stderr", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(
                        $"{key}: status={result.Status}; code={result.FailureCode}; diagnostic={result.Diagnostic}");
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{key}: threw {exception.GetType().Name}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void OrdinaryStartup_InvokesConfiguredHealthProbeOnceWithoutDispatchingTools()
    {
        using StartupProbeFactory factory = new();

        _ = factory.Services;

        Assert.Equal(1, factory.Probe.CallCount);
        Assert.Equal(0, factory.Probe.CodeGraphOperationCount);
        Assert.Equal(0, factory.Probe.JavaSessionCount);
        Assert.Equal(0, factory.Probe.WorkerDispatchCount);
    }

    private static AnalysisScope CreateScope(
        Guid? repositoryId = null,
        string? profiledRevision = null) => new(
        repositoryId ?? _repositoryId,
        _scanRunId,
        profiledRevision ?? "0123456789abcdef0123456789abcdef01234567",
        "analysis-runtime-health",
        new string('a', 64),
        new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));

    private static AnalysisRuntimeIdentity CreateIdentity(
        AnalysisRuntimeOptions options,
        int javaMajorVersion = 21,
        string clientVersion = "2.25.29",
        string codeGraphVersion = "1.5.0",
        string revision = "0123456789abcdef0123456789abcdef01234567",
        string? archiveChecksum = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") =>
        AnalysisRuntimeIdentity.Create(
            options,
            _repositoryId,
            revision,
            "@colbymchenry/codegraph",
            codeGraphVersion,
            "https://github.com/colbymchenry/codegraph",
            "codegraph",
            "@colbymchenry/codegraph-win32-x64@1.5.0",
            [
                new(
                    "lib/dist/bin/codegraph.js",
                    "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03"),
                new(
                    "node.exe",
                    "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3"),
                new(
                    "npm-shim.js",
                    "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877"),
            ],
            "jdt-language-server-1.61.0-202608141332",
            "jdt-language-server-1.61.0-202608141332.zip",
            archiveChecksum,
            javaMajorVersion,
            "StreamJsonRpc",
            clientVersion,
            "stdio");

    private WebApplicationFactory<Program> CreateConfiguredRuntimeFactory(
        IReadOnlyDictionary<string, string?> configuration)
        => _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configBuilder) =>
                configBuilder.AddInMemoryCollection(configuration)));

    private static Dictionary<string, string?> CreateValidRuntimeConfiguration() =>
        new(StringComparer.Ordinal)
        {
            ["AnalysisRuntime:CodeGraphCommandPath"] =
                @"G:\packages\npm\prefix\codegraph.ps1",
            ["AnalysisRuntime:CodeGraphCommand"] = "codegraph",
            ["AnalysisRuntime:CodeGraphPackageName"] = "@colbymchenry/codegraph",
            ["AnalysisRuntime:CodeGraphPackageVersion"] = "1.5.0",
            ["AnalysisRuntime:CodeGraphRepository"] =
                "https://github.com/colbymchenry/codegraph",
            ["AnalysisRuntime:CodeGraphBundle"] =
                "@colbymchenry/codegraph-win32-x64@1.5.0",
            ["AnalysisRuntime:CodeGraphHashes:npm-shim.js"] =
                "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877",
            ["AnalysisRuntime:CodeGraphHashes:node.exe"] =
                "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3",
            ["AnalysisRuntime:CodeGraphHashes:lib/dist/bin/codegraph.js"] =
                "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03",
            ["AnalysisRuntime:JdtLsInstallationRoot"] =
                @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332",
            ["AnalysisRuntime:JdtLsDistributionArchivePath"] =
                @"C:\Users\13728\Downloads\jdt-language-server-1.61.0-202608141332.tar.gz",
            ["AnalysisRuntime:JdtLsDistributionArchiveName"] =
                "jdt-language-server-1.61.0-202608141332.tar.gz",
            ["AnalysisRuntime:JdtLsDistributionArchiveSha256"] =
                "bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f",
            ["AnalysisRuntime:JdtLsReleaseIdentity"] =
                "jdt-language-server-1.61.0-202608141332",
            ["AnalysisRuntime:JdtLsLauncher"] =
                "plugins/org.eclipse.equinox.launcher_1.8.0.v20260804-1928.jar",
            ["AnalysisRuntime:JdtLsConfiguration"] = "config_win",
            ["AnalysisRuntime:JavaHome"] = @"C:\Program Files\Java\latest\jdk-21",
            ["AnalysisRuntime:JavaMajorVersion"] = "21",
            ["AnalysisRuntime:StreamJsonRpcName"] = "StreamJsonRpc",
            ["AnalysisRuntime:StreamJsonRpcVersion"] = "2.25.29",
        };

    private static string ReadRepositoryFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([GetRepositoryRoot(), .. segments]));

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("Repository root could not be located.");
    }

    private sealed class StartupProbeFactory : WebApplicationFactory<Program>
    {
        public StartupProbeFactory()
        {
            Probe = new RecordingHealthProbe();
        }

        public RecordingHealthProbe Probe { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IAnalysisRuntimeHealthProbe>(Probe);
            });
        }

    }

    private sealed class RecordingHealthProbe : IAnalysisRuntimeHealthProbe
    {
        public int CallCount { get; private set; }
        public int CodeGraphOperationCount { get; private set; }
        public int JavaSessionCount { get; private set; }
        public int WorkerDispatchCount { get; private set; }

        public Task<AnalysisHealthProbeResult> CheckConfiguredIntegrityAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new AnalysisHealthProbeResult(
                AnalysisRuntimeHealthStatus.NotReady,
                "jdt_distribution_archive_checksum_missing",
                "archive checksum missing",
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                [new AnalysisHealthCheck("jdt-ls", AnalysisRuntimeHealthStatus.NotReady)]));
        }
    }
}

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
    public void ProductionComposition_RegistersClosedAnalysisPortsAndHealthProbe()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
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
    public void OrdinaryStartup_InvokesConfiguredHealthProbeOnceWithoutDispatchingTools()
    {
        using StartupProbeFactory factory = new();

        _ = factory.Services;

        Assert.Equal(1, factory.Probe.CallCount);
        Assert.Equal(0, factory.Probe.CodeGraphOperationCount);
        Assert.Equal(0, factory.Probe.JavaSessionCount);
        Assert.Equal(0, factory.Probe.WorkerDispatchCount);
    }

    private static AnalysisScope CreateScope() => new(
        _repositoryId,
        _scanRunId,
        "0123456789abcdef0123456789abcdef01234567",
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

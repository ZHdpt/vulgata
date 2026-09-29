using Vulgata.Tests.TestInfrastructure;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Shared.Scans;
using Vulgata.Tests.PreparedScanning;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T046 host-runtime qualification for the retained August architecture.
/// This test only observes the pinned host tools and the existing PostgreSQL
/// service; it does not download, install, or alter any runtime.
/// </summary>
[Collection(PreparedScanningCollection.Name)]
public sealed class Pass1HostRuntimeQualificationTests
{
    private const string CodeGraphPackageHash =
        "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03";
    private const string CodeGraphNodeHash =
        "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3";
    private const string NpmShimHash =
        "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877";
    private const string JdtLsArchiveHash =
        "bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f";

    [Trait("Suite", "External")]
    [T062HostQualificationFact]
    public async Task ProductionComposition_RequiresBoundRevisionSourceBeforeClaimedNavigation()
    {
        T062QualificationInputs qualification =
            await Pass1ProductionExecutionTests.LoadQualificationInputsAsync();
        await using var host = Pass1ProductionExecutionTests.CreateProductionHost(
            qualification.ConnectionString);
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IRevisionBoundSourceReader sourceReader =
            scope.ServiceProvider.GetRequiredService<IRevisionBoundSourceReader>();

        Assert.False(
            sourceReader.GetType().Name.Contains("Unbound", StringComparison.Ordinal),
            "T062 RED: production host still registers UnboundRevisionSourceReader; " +
            "claimed source hydration and typed navigation cannot establish the exact " +
            "approved Profiled Revision binding.");
    }

    [Trait("Suite", "External")]
    [T062HostQualificationFact]
    public async Task PinnedRuntimeQualification_MustReachAuthenticatedPreparedPass1Execution()
    {
        T062QualificationInputs qualification =
            await Pass1ProductionExecutionTests.LoadQualificationInputsAsync();
        using var host = Pass1ProductionExecutionTests.CreateProductionHost(
            qualification.ConnectionString);
        using HttpClient client = await Pass1ProductionExecutionTests
            .CreateAuthenticatedClientAsync(host, qualification.SystemId);
        Guid[] manifests = qualification.ManifestIds.ToArray();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/pass1-executions",
            new
            {
                firstManifestId = manifests[0],
                secondManifestId = manifests[1],
                concurrencyLimit = 2,
            });
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"T062 RED: pinned runtime passed qualification but Start was not reachable; status={response.StatusCode}, body={body}");
        Pass1ExecutionStartResponse started =
            (await response.Content.ReadFromJsonAsync<Pass1ExecutionStartResponse>())!;
        Assert.False(started.IsReplay, body);
    }

    [Trait("Suite", "External")]
    [T046HostQualificationFact]
    public async Task PinnedHostTools_AreExactAndJdtLsUsesStdioFactory()
    {
        string codeGraphCommandPath = RequiredPath("VULGATA_T046_CODEGRAPH_COMMAND");
        string javaExecutable = RequiredPath("VULGATA_T046_JAVA_EXECUTABLE");
        string jdtLsRoot = RequiredPath("VULGATA_T046_JDT_LS_ROOT");
        string jdtLsArchive = RequiredPath("VULGATA_T046_JDT_LS_ARCHIVE");
        Assert.True(File.Exists(codeGraphCommandPath), "Configured CodeGraph command is unavailable.");
        Assert.True(File.Exists(javaExecutable), "Configured Java executable is unavailable.");
        Assert.True(File.Exists(jdtLsArchive), "Configured JDT-LS archive is unavailable.");
        Assert.True(Directory.Exists(jdtLsRoot), "Configured JDT-LS installation is unavailable.");

        string codeGraphVersion = await RunAsync(codeGraphCommandPath, ["--version"]);
        Assert.Equal("1.5.0", codeGraphVersion.Trim());

        string javaVersion = await RunAsync(javaExecutable, ["-version"]);
        Assert.Contains("version \"21.", javaVersion, StringComparison.Ordinal);

        string packageRoot = Path.Combine(
            Path.GetDirectoryName(codeGraphCommandPath)!,
            "node_modules",
            "@colbymchenry",
            "codegraph");
        Assert.Equal(CodeGraphPackageHash, await HashAsync(Path.Combine(
            packageRoot,
            "node_modules",
            "@colbymchenry",
            "codegraph-win32-x64",
            "lib",
            "dist",
            "bin",
            "codegraph.js")));
        Assert.Equal(CodeGraphNodeHash, await HashAsync(Path.Combine(
            packageRoot,
            "node_modules",
            "@colbymchenry",
            "codegraph-win32-x64",
            "node.exe")));
        Assert.Equal(NpmShimHash, await HashAsync(Path.Combine(packageRoot, "npm-shim.js")));
        Assert.Equal(JdtLsArchiveHash, await HashAsync(jdtLsArchive));
        Assert.Equal("jdt-language-server-1.61.0-202608141332", new DirectoryInfo(jdtLsRoot).Name);
        Assert.True(File.Exists(Path.Combine(jdtLsRoot, "config_win", "config.ini")));

        // Construction validates the archive and selects the production
        // StreamJsonRpc stdio implementation without exposing raw LSP APIs.
        _ = new JdtLsAnalysisSessionFactory(
            new JdtLsAdapterOptions(
                javaExecutable,
                jdtLsRoot,
                Path.Combine(Path.GetTempPath(), "vulgata-story123-t046-workspaces"),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(10),
                256 * 1024),
            jdtLsArchive,
            JdtLsArchiveHash);
    }

    [Trait("Suite", "External")]
    [SuiteFact("External")]
    public async Task PostgreSql17_OnlyRequiredDockerService_IsReachable()
    {
        string compose = await File.ReadAllTextAsync(FindRepositoryFile("docker-compose.yml"));
        Assert.Contains("image: postgres:17-alpine", compose, StringComparison.Ordinal);
        Assert.Contains("  postgres:", compose, StringComparison.Ordinal);
        Assert.Contains("post-deadline-app", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("image: codegraph", compose, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("image: jdt", compose, StringComparison.OrdinalIgnoreCase);

        string? connectionString = Environment.GetEnvironmentVariable("VULGATA_TEST_ADMIN_CONNECTION") ??
                                    Environment.GetEnvironmentVariable("VULGATA_TEST_POSTGRES_ADMIN");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "T046 requires VULGATA_TEST_ADMIN_CONNECTION or VULGATA_TEST_POSTGRES_ADMIN.");

        TestResourceScope.ValidateAdministrator(connectionString!);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT (current_setting('server_version_num')::integer / 10000);",
            connection);
        Assert.Equal(17, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    private static string FindRepositoryFile(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("Repository root not found."),
            fileName);
    }

    private static string RequiredPath(string variableName)
    {
        string? value = Environment.GetEnvironmentVariable(variableName);
        Assert.False(string.IsNullOrWhiteSpace(value), $"T046 requires {variableName}.");
        return value!;
    }

    private static async Task<string> RunAsync(string fileName, IReadOnlyList<string> arguments)
    {
        bool powershellScript = fileName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo(powershellScript ? "pwsh.exe" : fileName)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (powershellScript)
        {
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(fileName);
        }

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{fileName} did not start.");
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        return stdout + stderr;
    }

    private static async Task<string> HashAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    internal static string? HostQualificationSkipReason()
    {
        string[] required =
        [
            "VULGATA_T046_CODEGRAPH_COMMAND",
            "VULGATA_T046_JAVA_EXECUTABLE",
            "VULGATA_T046_JDT_LS_ROOT",
            "VULGATA_T046_JDT_LS_ARCHIVE",
        ];
        string? missing = required.FirstOrDefault(name =>
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));
        return missing is null
            ? null
            : $"T046 host qualification is opt-in; {missing} is not configured.";
    }
}
internal sealed class T046HostQualificationFactAttribute : SuiteFactAttribute
{
    public T046HostQualificationFactAttribute() : base("External") => Skip =
        Pass1HostRuntimeQualificationTests.HostQualificationSkipReason();
}

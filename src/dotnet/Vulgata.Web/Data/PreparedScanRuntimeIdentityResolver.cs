using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Data;

internal sealed class PreparedScanRuntimeIdentityResolution
{
    internal PreparedScanRuntimeIdentityResolution(
        AnalysisRuntimeIdentity? identity,
        AnalysisHealthProbeResult health)
    {
        Identity = identity;
        Health = health ?? throw new ArgumentNullException(nameof(health));
    }

    public PreparedScanRuntimeIdentityResolution(AnalysisRuntimeIdentity identity)
        : this(identity, ReadyHealth(identity, TimeProvider.System))
    {
    }

    public PreparedScanRuntimeIdentityResolution(
        AnalysisRuntimeIdentity? identity,
        string? failureCode)
        : this(
            identity,
            identity is not null
                ? ReadyHealth(identity, TimeProvider.System)
                : new AnalysisHealthProbeResult(
                    AnalysisRuntimeHealthStatus.NotReady,
                    string.IsNullOrWhiteSpace(failureCode)
                        ? "analysis_runtime_not_ready"
                        : failureCode,
                    "Runtime identity is not ready",
                    TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds(),
                    [new AnalysisHealthCheck(
                        "analysis-runtime",
                        AnalysisRuntimeHealthStatus.NotReady)]))
    {
    }

    public AnalysisRuntimeIdentity? Identity { get; }
    public AnalysisHealthProbeResult Health { get; }
    public string? FailureCode => Health.FailureCode;

    internal static PreparedScanRuntimeIdentityResolution Ready(
        AnalysisRuntimeIdentity identity,
        TimeProvider timeProvider) =>
        new(identity, ReadyHealth(identity, timeProvider));

    internal static PreparedScanRuntimeIdentityResolution NotReady(
        string failureCode,
        string diagnostic,
        string component,
        TimeProvider timeProvider) =>
        new(
            null,
            new AnalysisHealthProbeResult(
                AnalysisRuntimeHealthStatus.NotReady,
                failureCode,
                diagnostic,
                timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                [new AnalysisHealthCheck(component, AnalysisRuntimeHealthStatus.NotReady)]));

    private static AnalysisHealthProbeResult ReadyHealth(
        AnalysisRuntimeIdentity identity,
        TimeProvider timeProvider) =>
        new(
            AnalysisRuntimeHealthStatus.Ready,
            "none",
            string.Empty,
            timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            [
                new AnalysisHealthCheck("codegraph", AnalysisRuntimeHealthStatus.Ready),
                new AnalysisHealthCheck("java", AnalysisRuntimeHealthStatus.Ready),
                new AnalysisHealthCheck("jdt-ls", AnalysisRuntimeHealthStatus.Ready),
                new AnalysisHealthCheck("streamjsonrpc", AnalysisRuntimeHealthStatus.Ready),
            ],
            identity.Sha256);
}

internal interface IPreparedScanRuntimeIdentityResolver
{
    Task<PreparedScanRuntimeIdentityResolution> ResolveAsync(
        Guid repositoryId,
        string profiledRevision,
        CancellationToken cancellationToken = default);
}

internal sealed class ConfiguredPreparedScanRuntimeIdentityResolver(
    IConfiguration configuration,
    TimeProvider timeProvider) : IPreparedScanRuntimeIdentityResolver
{
    private const string _jdtRelease = "jdt-language-server-1.61.0-202608141332";
    private const string _jdtLauncher =
        "plugins/org.eclipse.equinox.launcher_1.8.0.v20260804-1928.jar";
    private const string _jdtConfiguration = "config_win";
    private const string _streamJsonRpcVersion = "2.25.29";

    private static readonly IReadOnlyDictionary<string, string> _codeGraphHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["lib/dist/bin/codegraph.js"] =
                "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03",
            ["node.exe"] =
                "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3",
            ["npm-shim.js"] =
                "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877",
        };

    public async Task<PreparedScanRuntimeIdentityResolution> ResolveAsync(
        Guid repositoryId,
        string profiledRevision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (repositoryId == Guid.Empty || string.IsNullOrWhiteSpace(profiledRevision))
        {
            return NotReady("analysis_runtime_identity_mismatch", "Runtime identity mismatch", "identity");
        }

        string? archivePath = Value("JdtLsDistributionArchivePath");
        string? archiveName = Value("JdtLsDistributionArchiveName");
        string? archiveHash = Value("JdtLsDistributionArchiveSha256");
        if (!IsSha256(archiveHash))
        {
            return NotReady(
                "jdt_distribution_archive_checksum_missing",
                "JDT LS archive checksum missing",
                "jdt-ls");
        }

        if (string.IsNullOrWhiteSpace(archivePath) ||
            string.IsNullOrWhiteSpace(archiveName) ||
            !IsApprovedArchiveName(archiveName) ||
            !string.Equals(Path.GetFileName(archivePath), archiveName, StringComparison.Ordinal) ||
            !File.Exists(archivePath))
        {
            return NotReady(
                "jdt_distribution_archive_missing",
                "JDT LS archive identity missing",
                "jdt-ls");
        }

        if (!string.Equals(
                await ComputeSha256Async(archivePath, cancellationToken),
                archiveHash,
                StringComparison.OrdinalIgnoreCase))
        {
            return NotReady(
                "jdt_distribution_archive_checksum_mismatch",
                "JDT LS archive checksum mismatch",
                "jdt-ls");
        }

        string? installationRoot = Value("JdtLsInstallationRoot");
        string? release = Value("JdtLsReleaseIdentity");
        string? launcher = Value("JdtLsLauncher")?.Replace('\\', '/');
        string? jdtConfiguration = Value("JdtLsConfiguration");
        if (string.IsNullOrWhiteSpace(installationRoot) ||
            !string.Equals(release, _jdtRelease, StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetFileName(Path.TrimEndingDirectorySeparator(installationRoot)),
                _jdtRelease,
                StringComparison.Ordinal) ||
            !string.Equals(launcher, _jdtLauncher, StringComparison.Ordinal) ||
            !string.Equals(jdtConfiguration, _jdtConfiguration, StringComparison.Ordinal) ||
            !File.Exists(Path.Combine(
                installationRoot,
                _jdtLauncher.Replace('/', Path.DirectorySeparatorChar))) ||
            !Directory.Exists(Path.Combine(installationRoot, _jdtConfiguration)))
        {
            return NotReady("jdt_identity_mismatch", "JDT LS identity mismatch", "jdt-ls");
        }

        string? javaHome = Value("JavaHome");
        string javaPath = Path.Combine(javaHome ?? string.Empty, "bin", "java.exe");
        if (!string.Equals(Value("JavaMajorVersion"), "21", StringComparison.Ordinal) ||
            !File.Exists(javaPath) ||
            FileVersionInfo.GetVersionInfo(javaPath).FileMajorPart != 21)
        {
            return NotReady("java_major_version_mismatch", "Java major version mismatch", "java");
        }

        if (!string.Equals(Value("StreamJsonRpcName"), "StreamJsonRpc", StringComparison.Ordinal) ||
            !string.Equals(Value("StreamJsonRpcVersion"), _streamJsonRpcVersion, StringComparison.Ordinal) ||
            !HasExpectedStreamJsonRpcAssembly())
        {
            return NotReady(
                "streamjsonrpc_identity_mismatch",
                "StreamJsonRpc identity mismatch",
                "streamjsonrpc");
        }

        string? commandPath = Value("CodeGraphCommandPath");
        if (!HasExpectedCodeGraphConfiguration(commandPath))
        {
            return NotReady("codegraph_identity_mismatch", "CodeGraph identity mismatch", "codegraph");
        }

        string packageRoot = Path.Combine(
            Path.GetDirectoryName(commandPath!)!,
            "node_modules",
            "@colbymchenry",
            "codegraph");
        if (!HasExpectedCodeGraphPackage(packageRoot))
        {
            return NotReady("codegraph_identity_mismatch", "CodeGraph package mismatch", "codegraph");
        }

        foreach ((string component, string expectedHash) in _codeGraphHashes)
        {
            string configuredHash = Value($"CodeGraphHashes:{component}") ?? string.Empty;
            string componentPath = component == "npm-shim.js"
                ? Path.Combine(packageRoot, component)
                : Path.Combine(
                    packageRoot,
                    "node_modules",
                    "@colbymchenry",
                    "codegraph-win32-x64",
                    component.Replace('/', Path.DirectorySeparatorChar));
            if (!string.Equals(configuredHash, expectedHash, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(componentPath) ||
                !string.Equals(
                    await ComputeSha256Async(componentPath, cancellationToken),
                    expectedHash,
                    StringComparison.Ordinal))
            {
                return NotReady(
                    "codegraph_integrity_mismatch",
                    "CodeGraph integrity mismatch",
                    "codegraph");
            }
        }

        try
        {
            AnalysisRuntimeIdentity identity = AnalysisRuntimeIdentity.Create(
                new AnalysisRuntimeOptions(commandPath!, installationRoot, javaHome!),
                repositoryId,
                profiledRevision,
                Value("CodeGraphPackageName")!,
                Value("CodeGraphPackageVersion")!,
                Value("CodeGraphRepository")!,
                Value("CodeGraphCommand")!,
                Value("CodeGraphBundle")!,
                _codeGraphHashes
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => new AnalysisRuntimeComponentHash(value.Key, value.Value))
                    .ToArray(),
                release!,
                archiveName,
                archiveHash,
                21,
                Value("StreamJsonRpcName")!,
                Value("StreamJsonRpcVersion")!,
                "stdio");
            return PreparedScanRuntimeIdentityResolution.Ready(identity, timeProvider);
        }
        catch (ArgumentException)
        {
            return NotReady("analysis_runtime_identity_mismatch", "Runtime identity mismatch", "identity");
        }
    }

    private PreparedScanRuntimeIdentityResolution NotReady(
        string code,
        string diagnostic,
        string component) =>
        PreparedScanRuntimeIdentityResolution.NotReady(
            code,
            diagnostic,
            component,
            timeProvider);

    private string? Value(string key) =>
        configuration[$"AnalysisRuntime:{key}"]?.Trim();

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsApprovedArchiveName(string value) =>
        string.Equals(value, $"{_jdtRelease}.zip", StringComparison.Ordinal) ||
        string.Equals(value, $"{_jdtRelease}.tar.gz", StringComparison.Ordinal);

    private bool HasExpectedCodeGraphConfiguration(string? commandPath) =>
        !string.IsNullOrWhiteSpace(commandPath) &&
        File.Exists(commandPath) &&
        string.Equals(Path.GetFileName(commandPath), "codegraph.ps1", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Value("CodeGraphCommand"), "codegraph", StringComparison.Ordinal) &&
        string.Equals(Value("CodeGraphPackageName"), "@colbymchenry/codegraph", StringComparison.Ordinal) &&
        string.Equals(Value("CodeGraphPackageVersion"), "1.5.0", StringComparison.Ordinal) &&
        string.Equals(
            Value("CodeGraphRepository"),
            "https://github.com/colbymchenry/codegraph",
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            Value("CodeGraphBundle"),
            "@colbymchenry/codegraph-win32-x64@1.5.0",
            StringComparison.Ordinal);

    private static bool HasExpectedCodeGraphPackage(string packageRoot)
    {
        string manifestPath = Path.Combine(packageRoot, "package.json");
        string bundleRoot = Path.Combine(
            packageRoot,
            "node_modules",
            "@colbymchenry",
            "codegraph-win32-x64");
        if (!File.Exists(manifestPath) || !Directory.Exists(bundleRoot))
        {
            return false;
        }

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement root = manifest.RootElement;
        return root.TryGetProperty("name", out JsonElement name) &&
               string.Equals(name.GetString(), "@colbymchenry/codegraph", StringComparison.Ordinal) &&
               root.TryGetProperty("version", out JsonElement version) &&
               string.Equals(version.GetString(), "1.5.0", StringComparison.Ordinal) &&
               root.TryGetProperty("repository", out JsonElement repository) &&
               repository.TryGetProperty("url", out JsonElement repositoryUrl) &&
               string.Equals(
                   repositoryUrl.GetString(),
                   "git+https://github.com/colbymchenry/codegraph.git",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExpectedStreamJsonRpcAssembly()
    {
        Assembly assembly = Assembly.Load("StreamJsonRpc");
        FileVersionInfo version = FileVersionInfo.GetVersionInfo(assembly.Location);
        return string.Equals(assembly.GetName().Name, "StreamJsonRpc", StringComparison.Ordinal) &&
               version.ProductVersion?.StartsWith(
                   $"{_streamJsonRpcVersion}+",
                   StringComparison.Ordinal) == true;
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vulgata.Core.DomainServices;

public sealed record AnalysisRuntimeOptions(
    string CodeGraphCommandPath,
    string JdtLsInstallationRoot,
    string JavaHome);

public sealed record AnalysisRuntimeComponentHash(
    string RelativeComponent,
    string Sha256);

public sealed record AnalysisRuntimeHealthComponentCheck(
    string Component,
    AnalysisRuntimeHealthStatus Status);

public enum AnalysisRuntimeHealthStatus
{
    Ready,
    NotReady,
}

public enum AnalysisRuntimeHealthFailureCategory
{
    None,
    CodeGraphIntegrityMismatch,
    CodeGraphVersionMismatch,
    JdtDistributionArchiveMissing,
    JdtDistributionArchiveChecksumMissing,
    JavaMajorVersionMismatch,
    ClientVersionMismatch,
    WorkspaceConflict,
    InvalidIdentity,
}

public sealed class AnalysisRuntimeIdentity
{
    private const string _schemaIdentity = "analysis-runtime/1";
    private const string _codeGraphPackageName = "@colbymchenry/codegraph";
    private const string _codeGraphPackageVersion = "1.5.0";
    private const string _codeGraphRepository = "https://github.com/colbymchenry/codegraph";
    private const string _codeGraphCommand = "codegraph";
    private const string _codeGraphPlatformPackage =
        "@colbymchenry/codegraph-win32-x64@1.5.0";
    private const string _jdtLsReleaseIdentity = "jdt-language-server-1.61.0-202608141332";
    private const string _jdtLsDistributionArchiveName =
        "jdt-language-server-1.61.0-202608141332.zip";
    private const string _observedCoreJarSha256 =
        "a58994113b7e493aa8ced51ad02dab3e424973223e7213f7ed5b9d70861582e2";
    private const int _javaMajorVersion = 21;
    private const string _clientName = "StreamJsonRpc";
    private const string _clientVersion = "2.25.29";
    private const string _transport = "stdio";

    private static readonly IReadOnlyDictionary<string, string> _approvedCodeGraphHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["lib/dist/bin/codegraph.js"] =
                "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03",
            ["node.exe"] =
                "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3",
            ["npm-shim.js"] =
                "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877",
        };

    private AnalysisRuntimeIdentity(
        Guid repositoryId,
        string profiledRevision,
        IReadOnlyList<AnalysisRuntimeComponentHash> codeGraphFiles,
        string jdtLsDistributionArchiveSha256,
        string canonicalJson)
    {
        RepositoryId = repositoryId;
        ProfiledRevision = profiledRevision;
        CodeGraphFiles = codeGraphFiles;
        JdtLsDistributionArchiveSha256 = jdtLsDistributionArchiveSha256;
        CanonicalJson = canonicalJson;
        Sha256 = ComputeSha256(canonicalJson);
    }

    public string SchemaIdentity => _schemaIdentity;
    public string CodeGraphPackageName => _codeGraphPackageName;
    public string CodeGraphPackageVersion => _codeGraphPackageVersion;
    public string CodeGraphRepository => _codeGraphRepository;
    public string CodeGraphCommand => _codeGraphCommand;
    public string CodeGraphPlatformPackage => _codeGraphPlatformPackage;
    public IReadOnlyList<AnalysisRuntimeComponentHash> CodeGraphFiles { get; }
    public string JdtLsReleaseIdentity => _jdtLsReleaseIdentity;
    public string JdtLsDistributionArchiveName => _jdtLsDistributionArchiveName;
    public string JdtLsDistributionArchiveSha256 { get; }
    public int JavaMajorVersion => _javaMajorVersion;
    public string ClientName => _clientName;
    public string ClientVersion => _clientVersion;
    public string Transport => _transport;
    public Guid RepositoryId { get; }
    public string ProfiledRevision { get; }
    public string CanonicalJson { get; }
    public string Sha256 { get; }

    public static AnalysisRuntimeIdentity Create(
        AnalysisRuntimeOptions options,
        Guid repositoryId,
        string profiledRevision,
        string codeGraphPackageName,
        string codeGraphPackageVersion,
        string codeGraphRepository,
        string codeGraphCommand,
        string codeGraphPlatformPackage,
        IReadOnlyList<AnalysisRuntimeComponentHash> codeGraphHashes,
        string jdtLsReleaseIdentity,
        string? jdtLsDistributionArchiveName,
        string? jdtLsDistributionArchiveSha256,
        int javaMajorVersion,
        string clientName,
        string clientVersion,
        string transport)
    {
        ArgumentNullException.ThrowIfNull(options);
        RequireConfiguredPath(options.CodeGraphCommandPath, nameof(options.CodeGraphCommandPath));
        RequireConfiguredPath(options.JdtLsInstallationRoot, nameof(options.JdtLsInstallationRoot));
        RequireConfiguredPath(options.JavaHome, nameof(options.JavaHome));
        if (repositoryId == Guid.Empty)
        {
            throw new ArgumentException("Repository identity must not be empty.", nameof(repositoryId));
        }

        string revision = RequireProfiledRevision(profiledRevision);
        RequireExact(codeGraphPackageName, _codeGraphPackageName, nameof(codeGraphPackageName));
        RequireExact(codeGraphPackageVersion, _codeGraphPackageVersion, nameof(codeGraphPackageVersion));
        RequireExact(
            codeGraphRepository,
            _codeGraphRepository,
            nameof(codeGraphRepository),
            ignoreCase: true);
        RequireExact(codeGraphCommand, _codeGraphCommand, nameof(codeGraphCommand));
        RequireExact(
            codeGraphPlatformPackage,
            _codeGraphPlatformPackage,
            nameof(codeGraphPlatformPackage));
        IReadOnlyList<AnalysisRuntimeComponentHash> files =
            RequireCodeGraphHashes(codeGraphHashes);
        RequireExact(
            jdtLsReleaseIdentity,
            _jdtLsReleaseIdentity,
            nameof(jdtLsReleaseIdentity));
        string archiveName = RequireArchiveName(jdtLsDistributionArchiveName);
        string archiveHash = RequireSha256(
            jdtLsDistributionArchiveSha256,
            nameof(jdtLsDistributionArchiveSha256));
        if (string.Equals(archiveHash, _observedCoreJarSha256, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The observed core JAR checksum cannot substitute for the distribution archive checksum.",
                nameof(jdtLsDistributionArchiveSha256));
        }

        if (javaMajorVersion != _javaMajorVersion)
        {
            throw new ArgumentException("Java major version must be 21.", nameof(javaMajorVersion));
        }

        RequireExact(clientName, _clientName, nameof(clientName));
        RequireExact(clientVersion, _clientVersion, nameof(clientVersion));
        RequireExact(transport, _transport, nameof(transport));

        string canonicalJson = JsonSerializer.Serialize(new
        {
            client = new
            {
                clientVersion = _clientVersion,
                name = _clientName,
                transport = _transport,
            },
            codeGraph = new
            {
                command = _codeGraphCommand,
                files = files.Select(file => new
                {
                    relativeComponent = file.RelativeComponent,
                    sha256 = file.Sha256,
                }),
                packageName = _codeGraphPackageName,
                packageVersion = _codeGraphPackageVersion,
                platformPackage = _codeGraphPlatformPackage,
                repository = _codeGraphRepository,
            },
            java = new { majorVersion = _javaMajorVersion },
            jdtLs = new
            {
                distributionArchiveName = archiveName,
                distributionArchiveSha256 = archiveHash,
                releaseIdentity = _jdtLsReleaseIdentity,
            },
            schemaIdentity = _schemaIdentity,
            workspace = new
            {
                profiledRevision = revision,
                repositoryId,
            },
        });

        return new AnalysisRuntimeIdentity(
            repositoryId,
            revision,
            files,
            archiveHash,
            canonicalJson);
    }

    public static string ComputeSha256(string canonicalJson)
    {
        ArgumentNullException.ThrowIfNull(canonicalJson);
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
    }

    private static IReadOnlyList<AnalysisRuntimeComponentHash> RequireCodeGraphHashes(
        IReadOnlyList<AnalysisRuntimeComponentHash> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (AnalysisRuntimeComponentHash value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            string component = value.RelativeComponent.Trim().Replace('\\', '/');
            string hash = RequireSha256(value.Sha256, nameof(value.Sha256));
            if (!_approvedCodeGraphHashes.TryGetValue(component, out string? approvedHash) ||
                !string.Equals(hash, approvedHash, StringComparison.Ordinal) ||
                !normalized.TryAdd(component, hash))
            {
                throw new ArgumentException(
                    "CodeGraph component identity does not match the approved allowlist.",
                    nameof(values));
            }
        }

        if (normalized.Count != _approvedCodeGraphHashes.Count)
        {
            throw new ArgumentException(
                "Every approved CodeGraph component hash is required.",
                nameof(values));
        }

        return Array.AsReadOnly(normalized
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new AnalysisRuntimeComponentHash(pair.Key, pair.Value))
            .ToArray());
    }

    private static string RequireArchiveName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "The exact JDT LS distribution archive filename is required.",
                nameof(value));
        }

        string normalized = value.Trim();
        if (!string.Equals(normalized, _jdtLsDistributionArchiveName, StringComparison.Ordinal) ||
            Path.IsPathRooted(normalized) ||
            normalized.Contains('/') ||
            normalized.Contains('\\'))
        {
            throw new ArgumentException(
                "The exact JDT LS distribution archive filename is required.",
                nameof(value));
        }

        return normalized;
    }

    private static string RequireProfiledRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length is < 7 or > 64 ||
            normalized.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "Profiled Revision must be a bounded Git object identity.",
                nameof(value));
        }

        return normalized;
    }

    private static string RequireSha256(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A SHA-256 value is required.", parameterName);
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64 ||
            normalized.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A SHA-256 value is required.", parameterName);
        }

        return normalized;
    }

    private static void RequireExact(
        string value,
        string expected,
        string parameterName,
        bool ignoreCase = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        StringComparison comparison = ignoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(value.Trim(), expected, comparison))
        {
            throw new ArgumentException("Runtime identity value is not approved.", parameterName);
        }
    }

    private static void RequireConfiguredPath(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }
}

public sealed class AnalysisRuntimeHealthResult
{
    private const int _maximumDiagnosticLength = 256;

    private AnalysisRuntimeHealthResult(
        AnalysisRuntimeHealthStatus status,
        AnalysisRuntimeHealthFailureCategory failureCategory,
        AnalysisRuntimeIdentity? identity,
        string diagnostic,
        DateTimeOffset observedAt)
    {
        Status = status;
        FailureCategory = failureCategory;
        Identity = identity;
        IdentityHash = identity?.Sha256;
        Diagnostic = diagnostic;
        FailureCode = GetFailureCode(failureCategory);
        ObservedAt = observedAt;
        ComponentChecks = CreateComponentChecks(status, failureCategory);
    }

    public AnalysisRuntimeHealthStatus Status { get; }
    public AnalysisRuntimeHealthFailureCategory FailureCategory { get; }
    public AnalysisRuntimeIdentity? Identity { get; }
    public string? IdentityHash { get; }
    public string Diagnostic { get; }
    public string FailureCode { get; }
    public DateTimeOffset ObservedAt { get; }
    public IReadOnlyList<AnalysisRuntimeHealthComponentCheck> ComponentChecks { get; }

    public static AnalysisRuntimeHealthResult Ready(AnalysisRuntimeIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return new AnalysisRuntimeHealthResult(
            AnalysisRuntimeHealthStatus.Ready,
            AnalysisRuntimeHealthFailureCategory.None,
            identity,
            string.Empty,
            DateTimeOffset.UtcNow);
    }

    public static AnalysisRuntimeHealthResult NotReady(
        AnalysisRuntimeHealthFailureCategory failureCategory,
        string diagnostic)
    {
        if (!Enum.IsDefined(failureCategory) ||
            failureCategory == AnalysisRuntimeHealthFailureCategory.None)
        {
            throw new ArgumentOutOfRangeException(nameof(failureCategory));
        }

        return new AnalysisRuntimeHealthResult(
            AnalysisRuntimeHealthStatus.NotReady,
            failureCategory,
            identity: null,
            SanitizeDiagnostic(diagnostic),
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<AnalysisRuntimeHealthComponentCheck> CreateComponentChecks(
        AnalysisRuntimeHealthStatus status,
        AnalysisRuntimeHealthFailureCategory failureCategory)
    {
        string? failedComponent = failureCategory switch
        {
            AnalysisRuntimeHealthFailureCategory.CodeGraphIntegrityMismatch or
            AnalysisRuntimeHealthFailureCategory.CodeGraphVersionMismatch => "codegraph",
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveMissing or
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveChecksumMissing => "jdt-ls",
            AnalysisRuntimeHealthFailureCategory.JavaMajorVersionMismatch => "java",
            AnalysisRuntimeHealthFailureCategory.ClientVersionMismatch => "streamjsonrpc",
            AnalysisRuntimeHealthFailureCategory.WorkspaceConflict => "workspace",
            AnalysisRuntimeHealthFailureCategory.InvalidIdentity => "identity",
            _ => null,
        };
        string[] components = ["codegraph", "identity", "java", "jdt-ls", "streamjsonrpc", "workspace"];
        return Array.AsReadOnly(components
            .OrderBy(component => component, StringComparer.Ordinal)
            .Select(component => new AnalysisRuntimeHealthComponentCheck(
                component,
                status == AnalysisRuntimeHealthStatus.NotReady &&
                string.Equals(component, failedComponent, StringComparison.Ordinal)
                    ? AnalysisRuntimeHealthStatus.NotReady
                    : AnalysisRuntimeHealthStatus.Ready))
            .ToArray());
    }

    private static string GetFailureCode(
        AnalysisRuntimeHealthFailureCategory failureCategory) => failureCategory switch
        {
            AnalysisRuntimeHealthFailureCategory.None => "none",
            AnalysisRuntimeHealthFailureCategory.CodeGraphIntegrityMismatch =>
                "codegraph_integrity_mismatch",
            AnalysisRuntimeHealthFailureCategory.CodeGraphVersionMismatch =>
                "codegraph_version_mismatch",
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveMissing =>
                "jdt_distribution_archive_missing",
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveChecksumMissing =>
                "jdt_distribution_archive_checksum_missing",
            AnalysisRuntimeHealthFailureCategory.JavaMajorVersionMismatch =>
                "java_major_version_mismatch",
            AnalysisRuntimeHealthFailureCategory.ClientVersionMismatch =>
                "client_version_mismatch",
            AnalysisRuntimeHealthFailureCategory.WorkspaceConflict => "workspace_conflict",
            AnalysisRuntimeHealthFailureCategory.InvalidIdentity => "invalid_identity",
            _ => throw new ArgumentOutOfRangeException(nameof(failureCategory)),
        };

    private static string SanitizeDiagnostic(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().Normalize(NormalizationForm.FormC);
        string scrubbedControls = new(normalized
            .Select(character => char.IsControl(character) ? ' ' : character)
            .ToArray());
        string[] safeTokens = scrubbedControls
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token =>
                !token.Contains('\\') &&
                !token.Contains('/') &&
                !token.Contains("stderr", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string sanitized = string.Join(' ', safeTokens);
        if (sanitized.Length == 0)
        {
            sanitized = "Runtime readiness check failed.";
        }

        return sanitized.Length <= _maximumDiagnosticLength
            ? sanitized
            : sanitized[.._maximumDiagnosticLength];
    }
}

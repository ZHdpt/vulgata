using System.Diagnostics;
using System.Security.Cryptography;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// T039 qualification evidence for the currently incomplete host installation.
/// This is a post-GREEN observation: the retained distribution archive and its
/// checksum are intentionally absent, so Ready is not an expected outcome.
/// </summary>
public sealed class JdtLsMissingArchiveQualificationTests
{
    private const string JavaHome = @"C:\Program Files\Java\latest\jdk-21";
    private const string JdtLsRoot =
        @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332";
    private const string ReleaseIdentity = "jdt-language-server-1.61.0-202608141332";
    private const string ArchiveName = "jdt-language-server-1.61.0-202608141332.zip";
    private const string CoreJarSha256 =
        "A58994113B7E493AA8CED51AD02DAB3E424973223E7213F7ED5B9D70861582E2";

    private static readonly Guid _repositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000002");

    [Fact]
    public void SuppliedHost_ReportsJava21AndObservedCoreJarHash_WithoutRetainedArchive()
    {
        Assert.True(Directory.Exists(JavaHome));
        Assert.True(Directory.Exists(JdtLsRoot));
        Assert.Equal(ReleaseIdentity, new DirectoryInfo(JdtLsRoot).Name);

        string javaExecutable = Path.Combine(
            JavaHome,
            "bin",
            OperatingSystem.IsWindows() ? "java.exe" : "java");
        Assert.True(File.Exists(javaExecutable));

        ProcessStartInfo startInfo = new(javaExecutable, "-version")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Java 21 process did not start.");
        Assert.True(process.WaitForExit(TimeSpan.FromSeconds(5)));
        string versionOutput = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
        Assert.Equal(0, process.ExitCode);
        Assert.Contains("version \"21.", versionOutput, StringComparison.Ordinal);

        string coreJar = Path.Combine(
            JdtLsRoot,
            "plugins",
            "org.eclipse.jdt.ls.core_1.61.0.202608141332.jar");
        Assert.True(File.Exists(coreJar));
        string observedCoreHash = GetFileHash(coreJar);
        Assert.Equal(CoreJarSha256, observedCoreHash, ignoreCase: true);

        string archivePath = Path.Combine(JdtLsRoot, ArchiveName);
        Assert.False(File.Exists(archivePath));
        Assert.Empty(Directory.GetFiles(JdtLsRoot, "*.zip", SearchOption.AllDirectories));
        Assert.Empty(
            Directory.GetFiles(JdtLsRoot, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).Contains("sha256", StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(path).Contains("checksum", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".sha512", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".md5", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MissingArchiveOrChecksum_StaysNotReady_AndCoreJarHashCannotSubstitute()
    {
        AnalysisRuntimeOptions options = new(
            CodeGraphCommandPath: "codegraph",
            JdtLsInstallationRoot: JdtLsRoot,
            JavaHome: JavaHome);

        Assert.Throws<ArgumentException>(() => CreateIdentity(
            options,
            archiveName: null,
            archiveSha256: null));
        Assert.Throws<ArgumentException>(() => CreateIdentity(
            options,
            archiveName: ArchiveName,
            archiveSha256: null));
        Assert.Throws<ArgumentException>(() => CreateIdentity(
            options,
            archiveName: ArchiveName,
            archiveSha256: CoreJarSha256));

        AnalysisRuntimeHealthResult missingArchive = AnalysisRuntimeHealthResult.NotReady(
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveMissing,
            "JDT distribution archive is not retained.");
        AssertNotReady(missingArchive, "jdt_distribution_archive_missing");

        AnalysisRuntimeHealthResult missingChecksum = AnalysisRuntimeHealthResult.NotReady(
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveChecksumMissing,
            "JDT distribution archive checksum is not configured.");
        AssertNotReady(missingChecksum, "jdt_distribution_archive_checksum_missing");

        Assert.Null(missingArchive.Identity);
        Assert.Null(missingArchive.IdentityHash);
        Assert.Null(missingChecksum.Identity);
        Assert.Null(missingChecksum.IdentityHash);
        Assert.Equal(
            AnalysisRuntimeHealthStatus.NotReady,
            missingArchive.ComponentChecks.Single(check => check.Component == "jdt-ls").Status);
        Assert.Equal(
            AnalysisRuntimeHealthStatus.NotReady,
            missingChecksum.ComponentChecks.Single(check => check.Component == "jdt-ls").Status);
    }

    private static AnalysisRuntimeIdentity CreateIdentity(
        AnalysisRuntimeOptions options,
        string? archiveName,
        string? archiveSha256) =>
        AnalysisRuntimeIdentity.Create(
            options,
            repositoryId: _repositoryId,
            profiledRevision: "0123456789abcdef0123456789abcdef01234567",
            codeGraphPackageName: "@colbymchenry/codegraph",
            codeGraphPackageVersion: "1.5.0",
            codeGraphRepository: "https://github.com/colbymchenry/codegraph",
            codeGraphCommand: "codegraph",
            codeGraphPlatformPackage: "@colbymchenry/codegraph-win32-x64@1.5.0",
            codeGraphHashes:
            [
                new("npm-shim.js", "80419260F06862D7A422D13EF69E3B24C7A21DBEF4309BCE41DB02AD642B1877"),
                new("node.exe", "B3094D0B49F9AD602262A9921551737BB97637C05DD357A06AE98188D7290AA3"),
                new("lib/dist/bin/codegraph.js", "03E4C791CC0DD91ED264278461BF9A56C0278AA0670D5942FC4732311C66DE03"),
            ],
            jdtLsReleaseIdentity: ReleaseIdentity,
            jdtLsDistributionArchiveName: archiveName,
            jdtLsDistributionArchiveSha256: archiveSha256,
            javaMajorVersion: 21,
            clientName: "StreamJsonRpc",
            clientVersion: "2.25.29",
            transport: "stdio");

    private static string GetFileHash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void AssertNotReady(
        AnalysisRuntimeHealthResult result,
        string expectedFailureCode)
    {
        Assert.Equal(AnalysisRuntimeHealthStatus.NotReady, result.Status);
        Assert.NotEqual(AnalysisRuntimeHealthFailureCategory.None, result.FailureCategory);
        Assert.Equal(expectedFailureCode, result.FailureCode);
        Assert.InRange(result.Diagnostic.Length, 1, 256);
        Assert.DoesNotContain("C:\\", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/", result.Diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", result.Diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("stderr", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }
}

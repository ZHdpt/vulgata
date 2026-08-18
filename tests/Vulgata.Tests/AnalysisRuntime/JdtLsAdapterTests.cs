using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.AnalysisRuntime;

public sealed class JdtLsAdapterTests
{
    private static readonly Guid _repositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000012");

    private const string ProfiledRevision =
        "abcdef0123456789abcdef0123456789abcdef01";

    private const string NpmShimHash =
        "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877";

    private const string NodeHash =
        "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3";

    private const string CodeGraphHash =
        "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03";

    [Fact]
    public void JavaSession_PublicLifecycle_ProvidesAsyncDisposalForShutdownAndCleanup()
    {
        Assert.True(
            typeof(IAsyncDisposable).IsAssignableFrom(typeof(IJavaAnalysisSession)),
            "The accepted JDT-LS session seam must be asynchronously disposable so shutdown, " +
            "exit, stream closure, bounded wait, and forced process-tree cleanup remain observable.");
    }

    [Fact]
    public void JdtLsIdentity_MissingArchiveOrCoreJarSubstitution_RemainsFailClosed()
    {
        Assert.Throws<ArgumentException>(() => CreateIdentity(jdtLsDistributionArchiveName: null));
        Assert.Throws<ArgumentException>(() => CreateIdentity(jdtLsDistributionArchiveSha256: null));
        Assert.Throws<ArgumentException>(() => CreateIdentity(
            jdtLsDistributionArchiveSha256:
                "a58994113b7e493aa8ced51ad02dab3e424973223e7213f7ed5b9d70861582e2"));

        AnalysisRuntimeHealthResult result = AnalysisRuntimeHealthResult.NotReady(
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveChecksumMissing,
            "JDT distribution archive checksum is not configured; root=C:\\Users\\13728\\.local");

        Assert.Equal(AnalysisRuntimeHealthStatus.NotReady, result.Status);
        Assert.Equal(
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveChecksumMissing,
            result.FailureCategory);
        Assert.Null(result.Identity);
        Assert.Null(result.IdentityHash);
        Assert.DoesNotContain("13728", result.Diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain('\\', result.Diagnostic);
        Assert.DoesNotContain('/', result.Diagnostic);
        Assert.Equal("jdt_distribution_archive_checksum_missing", result.FailureCode);
    }

    private static AnalysisRuntimeIdentity CreateIdentity(
        string? jdtLsDistributionArchiveName =
            "jdt-language-server-1.61.0-202608141332.zip",
        string? jdtLsDistributionArchiveSha256 =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") =>
        AnalysisRuntimeIdentity.Create(
            new AnalysisRuntimeOptions(
                CodeGraphCommandPath: @"G:\packages\npm\prefix\codegraph.ps1",
                JdtLsInstallationRoot:
                    @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332",
                JavaHome: @"C:\Program Files\Java\latest\jdk-21"),
            _repositoryId,
            ProfiledRevision,
            "@colbymchenry/codegraph",
            "1.5.0",
            "https://github.com/colbymchenry/codegraph",
            "codegraph",
            "@colbymchenry/codegraph-win32-x64@1.5.0",
            [
                new("lib/dist/bin/codegraph.js", CodeGraphHash),
                new("node.exe", NodeHash),
                new("npm-shim.js", NpmShimHash),
            ],
            "jdt-language-server-1.61.0-202608141332",
            jdtLsDistributionArchiveName,
            jdtLsDistributionArchiveSha256,
            21,
            "StreamJsonRpc",
            "2.25.29",
            "stdio");
}

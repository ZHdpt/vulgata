using System.Diagnostics;
using System.Security.Cryptography;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// T040 post-GREEN qualification against the retained JDT-LS distribution.
/// The test deliberately uses the supplied archive bytes and filename; it
/// must not copy, rename, or extract the operator-supplied artifact.
/// </summary>
public sealed class JdtLsRealQualificationTests
{
    private const string JavaHome = @"C:\Program Files\Java\latest\jdk-21";
    private const string JdtLsRoot =
        @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332";
    private const string ArchivePath =
        @"C:\Users\13728\Downloads\jdt-language-server-1.61.0-202608141332.tar.gz";
    private const string ArchiveName = "jdt-language-server-1.61.0-202608141332.tar.gz";
    private const string ArchiveSha256 =
        "bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f";
    private const string ReleaseIdentity = "jdt-language-server-1.61.0-202608141332";
    private const string ProfiledRevision =
        "0123456789abcdef0123456789abcdef01234567";
    private const string RuntimeIdentityHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CodeGraphHash =
        "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03";
    private const string NodeHash =
        "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3";
    private const string NpmShimHash =
        "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877";

    private static readonly Guid RepositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000012");

    [Fact]
    public void RuntimeIdentity_AcceptsRetainedTarGzArchiveAsConfiguredIdentity()
    {
        AnalysisRuntimeIdentity identity = AnalysisRuntimeIdentity.Create(
            new AnalysisRuntimeOptions(
                CodeGraphCommandPath: @"G:\packages\npm\prefix\codegraph.ps1",
                JdtLsInstallationRoot: JdtLsRoot,
                JavaHome: JavaHome),
            RepositoryId,
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
            ReleaseIdentity,
            ArchiveName,
            ArchiveSha256,
            21,
            "StreamJsonRpc",
            "2.25.29",
            "stdio");

        Assert.Equal(ArchiveName, identity.JdtLsDistributionArchiveName);
        Assert.Equal(ArchiveSha256, identity.JdtLsDistributionArchiveSha256);
    }

    [Fact]
    public async Task RetainedArchive_RealJava21Session_QualifiesNavigationAndBoundedCleanup()
    {
        Assert.True(File.Exists(ArchivePath));
        Assert.Equal(ArchiveName, Path.GetFileName(ArchivePath));
        Assert.Equal(ArchiveSha256, await HashAsync(ArchivePath));
        Assert.True(Directory.Exists(JdtLsRoot));
        Assert.Equal(ReleaseIdentity, new DirectoryInfo(JdtLsRoot).Name);
        Assert.True(File.Exists(Path.Combine(JdtLsRoot, "config_win", "config.ini")));
        Assert.Single(Directory.GetFiles(
            Path.Combine(JdtLsRoot, "plugins"),
            "org.eclipse.equinox.launcher_*.jar",
            SearchOption.TopDirectoryOnly));

        string javaExecutable = Path.Combine(
            JavaHome,
            "bin",
            OperatingSystem.IsWindows() ? "java.exe" : "java");
        Assert.True(File.Exists(javaExecutable));
        Assert.Contains("version \"21.", await ReadJavaVersionAsync(javaExecutable));

        string workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story121-jdt-real-" + Guid.NewGuid().ToString("N"));
        string workspacePath = Path.Combine(
            workspaceRoot,
            RepositoryId.ToString("N"),
            ProfiledRevision);
        Directory.CreateDirectory(workspacePath);
        CopyDirectory(FindFixtureRepository(), workspacePath);

        try
        {
            JdtLsAdapterOptions options = new(
                javaExecutable,
                JdtLsRoot,
                workspaceRoot,
                InitializationTimeout: TimeSpan.FromSeconds(30),
                RequestTimeout: TimeSpan.FromSeconds(30),
                ShutdownTimeout: TimeSpan.FromSeconds(10),
                ResponseByteLimit: 256 * 1024);
            JdtLsAnalysisSessionFactory factory = new(
                options,
                ArchivePath,
                ArchiveSha256);
            AnalysisScope scope = new(
                RepositoryId,
                Guid.NewGuid(),
                ProfiledRevision,
                "t040-" + Guid.NewGuid().ToString("N"),
                RuntimeIdentityHash,
                new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()));
            AnalysisOperationBounds bounds = new(itemLimit: 16, byteLimit: 64 * 1024);
            string relativePath =
                "src/main/java/example/analysis/GreetingService.java";
            JavaNavigationRequest definitionNavigation = new(
                relativePath,
                new AnalysisSourcePosition(12, 24),
                bounds);
            JavaNavigationRequest formatterInterfaceNavigation = new(
                "src/main/java/example/analysis/GreetingFormatter.java",
                new AnalysisSourcePosition(3, 21),
                bounds);

            await using (IJavaAnalysisSession session =
                await factory.OpenAsync(scope, CancellationToken.None))
            {
                JavaNavigationResult symbols = await session.GetDocumentSymbolsAsync(
                    scope,
                    new JavaNavigationRequest(relativePath, null, bounds),
                    CancellationToken.None);
                JavaNavigationResult definition = await session.GetDefinitionAsync(
                    scope, definitionNavigation, CancellationToken.None);
                JavaNavigationResult references = await session.GetReferencesAsync(
                    scope, formatterInterfaceNavigation, CancellationToken.None);
                JavaNavigationResult implementations = await session.GetImplementationsAsync(
                    scope, formatterInterfaceNavigation, CancellationToken.None);

                AssertNavigationSucceeded(symbols);
                AssertNavigationSucceeded(definition);
                AssertNavigationSucceeded(references);
                AssertNavigationSucceeded(implementations);
                Assert.All(
                    symbols.Evidence
                        .Concat(definition.Evidence)
                        .Concat(references.Evidence)
                        .Concat(implementations.Evidence),
                    evidence =>
                    {
                        Assert.DoesNotContain("..", evidence.RelativePath);
                        Assert.False(Path.IsPathRooted(evidence.RelativePath));
                        Assert.InRange(evidence.Range.StartLine, 1, 100);
                        Assert.InRange(
                            evidence.Range.EndLine,
                            evidence.Range.StartLine,
                            100);
                    });

                using CancellationTokenSource cancelled = new();
                cancelled.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    session.GetDefinitionAsync(scope, definitionNavigation, cancelled.Token));

                AnalysisScope otherRevision = new(
                    scope.RepositoryId,
                    scope.ScanRunId,
                    "fedcba9876543210fedcba9876543210fedcba98",
                    scope.WorkingCopyBindingToken,
                    scope.RuntimeIdentityHash,
                    new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()));
                JavaNavigationResult conflict = await session.GetDefinitionAsync(
                    otherRevision, definitionNavigation, CancellationToken.None);
                Assert.Equal(AnalysisOperationStatus.Failed, conflict.Status);
                Assert.Equal(
                    AnalysisPortFailureReason.WorkspaceConflict,
                    conflict.Failure?.Reason);
                Assert.Equal("jdt_scope_mismatch", conflict.Failure?.FailureCode);
            }
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }

            Assert.False(Directory.Exists(workspaceRoot));
        }
    }

    private static void AssertNavigationSucceeded(JavaNavigationResult result)
    {
        Assert.True(
            result.Status == AnalysisOperationStatus.Succeeded,
            result.Failure is null
                ? "JDT-LS navigation did not return a diagnostic."
                : $"{result.Failure.FailureCode}: {result.Failure.Diagnostic}");
        Assert.Null(result.Failure);
        Assert.NotEmpty(result.Evidence);
    }

    private static string FindFixtureRepository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException(
                "Repository root not found."),
            "tests",
            "Vulgata.Tests",
            "Fixtures",
            "AnalysisRuntime",
            "repository");
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.GetDirectories(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.GetFiles(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            string target = Path.Combine(
                destination,
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static async Task<string> ReadJavaVersionAsync(string javaExecutable)
    {
        ProcessStartInfo startInfo = new(javaExecutable, "-version")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Java 21 process did not start.");
        string output = await process.StandardError.ReadToEndAsync();
        output += await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        return output;
    }

    private static async Task<string> HashAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(
            await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }
}

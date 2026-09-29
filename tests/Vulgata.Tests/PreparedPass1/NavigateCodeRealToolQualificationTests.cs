using Vulgata.Tests.TestInfrastructure;
using System.Diagnostics;
using System.Security.Cryptography;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Analysis.Source;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T023 post-GREEN qualification against the pinned Windows-host tools.
/// This test deliberately uses only the typed CodeGraph/JDT-LS/source ports;
/// it does not invoke raw LSP, graph storage, shell, network, or filesystem
/// operations on behalf of the Worker Agent.
/// </summary>
public sealed class NavigateCodeRealToolQualificationTests
{
    private const string CodeGraphCommandPath = @"G:\packages\npm\prefix\codegraph.ps1";
    private const string CodeGraphHash =
        "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03";
    private const string NodeHash =
        "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3";
    private const string NpmShimHash =
        "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877";
    private const string JavaExecutable =
        @"C:\Program Files\Java\latest\jdk-21\bin\java.exe";
    private const string JdtLsRoot =
        @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332";
    private const string JdtLsArchive =
        @"C:\Users\13728\Downloads\jdt-language-server-1.61.0-202608141332.tar.gz";
    private const string JdtLsArchiveHash =
        "bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f";
    private const string JdtLsRelease = "jdt-language-server-1.61.0-202608141332";
    private const string ProfiledRevision =
        "0123456789abcdef0123456789abcdef01234567";
    private const string RuntimeIdentityHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly Guid RepositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000123");

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task PinnedHostRuntime_ReportsCodeGraph15Java21AndJdtLsStdio()
    {
        Assert.True(File.Exists(CodeGraphCommandPath),
            "The pinned CodeGraph host command is unavailable; this is an external prerequisite.");
        Assert.True(File.Exists(JavaExecutable),
            "The pinned Java 21 executable is unavailable; this is an external prerequisite.");
        Assert.True(File.Exists(JdtLsArchive),
            "The pinned JDT-LS distribution archive is unavailable; this is an external prerequisite.");
        Assert.True(Directory.Exists(JdtLsRoot),
            "The pinned JDT-LS installation is unavailable; this is an external prerequisite.");

        string codeGraphVersion = await RunAsync(CodeGraphCommandPath, ["--version"]);
        Assert.Equal("1.5.0", codeGraphVersion.Trim());
        string javaVersion = await RunAsync(JavaExecutable, ["-version"]);
        Assert.Contains("version \"21.", javaVersion, StringComparison.Ordinal);

        string packageRoot = Path.Combine(
            Path.GetDirectoryName(CodeGraphCommandPath)!,
            "node_modules",
            "@colbymchenry",
            "codegraph");
        Assert.Equal(CodeGraphHash, await HashAsync(Path.Combine(
            packageRoot,
            "node_modules",
            "@colbymchenry",
            "codegraph-win32-x64",
            "lib",
            "dist",
            "bin",
            "codegraph.js")));
        Assert.Equal(NodeHash, await HashAsync(Path.Combine(
            packageRoot,
            "node_modules",
            "@colbymchenry",
            "codegraph-win32-x64",
            "node.exe")));
        Assert.Equal(NpmShimHash, await HashAsync(Path.Combine(packageRoot, "npm-shim.js")));
        Assert.Equal(JdtLsArchiveHash, await HashAsync(JdtLsArchive));
        Assert.Equal(JdtLsRelease, new DirectoryInfo(JdtLsRoot).Name);
        Assert.True(File.Exists(Path.Combine(JdtLsRoot, "config_win", "config.ini")));

        // The production factory is intentionally constructed with the
        // accepted stdio distribution contract.  Construction validates the
        // archive identity before any protocol session can be opened.
        _ = new JdtLsAnalysisSessionFactory(
            new JdtLsAdapterOptions(
                JavaExecutable,
                JdtLsRoot,
                Path.Combine(Path.GetTempPath(), "vulgata-story123-t023-workspaces"),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(10),
                256 * 1024),
            JdtLsArchive,
            JdtLsArchiveHash);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task RealTypedTools_NavigateJavaAndRejectXmlNavigation()
    {
        string fixture = FindFixtureRepository();
        string javaWorkspaceRoot = CreateTemporaryRoot();

        AnalysisScope scope = new(
            RepositoryId,
            Guid.Parse("12000000-0000-4000-8000-000000000223"),
            ProfiledRevision,
            "t023-typed-tool-binding",
            RuntimeIdentityHash,
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()));
        string repositoryRoot = Path.Combine(
            javaWorkspaceRoot,
            scope.RepositoryId.ToString("N"),
            scope.ProfiledRevision);
        Directory.CreateDirectory(repositoryRoot);
        CopyDirectory(fixture, repositoryRoot);

        try
        {
            var snapshot = new CodeGraphRepositorySnapshot(
                scope.RepositoryId,
                scope.ScanRunId,
                scope.ProfiledRevision,
                scope.WorkingCopyBindingToken,
                repositoryRoot,
                "t023-neutral-index",
                scope.RuntimeIdentityHash);
            ICodeGraphAnalysisPort codeGraph = new CodeGraphAnalysisAdapter(
                new CodeGraphAdapterOptions(
                    CodeGraphCommandPath,
                    TimeSpan.FromSeconds(30),
                    256 * 1024,
                    16 * 1024),
                new FixedSnapshotResolver(snapshot),
                new CodeGraphProcessRunner());

            AnalysisIndexResult index = await codeGraph.IndexAsync(scope, CancellationToken.None);
            AnalysisIndexResult rebuild = await codeGraph.RebuildAsync(scope, CancellationToken.None);
            CodeGraphStatusResult status = await codeGraph.GetStatusAsync(scope, CancellationToken.None);
            Assert.True(index.Status == AnalysisOperationStatus.Succeeded, Describe(index.Failure));
            Assert.True(rebuild.Status == AnalysisOperationStatus.Succeeded, Describe(rebuild.Failure));
            Assert.True(status.Status == AnalysisOperationStatus.Succeeded, Describe(status.Failure));
            Assert.True(status.Ready);
            Assert.False(status.Stale);

            AnalysisOperationBounds bounds = new(20, 64 * 1024);
            const string fullyQualifiedSymbol = "example.analysis::GreetingService";
            SymbolSearchResult search = await codeGraph.SearchSymbolsAsync(
                scope,
                new AnalysisSymbolQuery("GreetingService", AnalysisSymbolKind.Type, bounds),
                CancellationToken.None);
            RelationshipResult callers = await codeGraph.GetCallersAsync(
                scope,
                new AnalysisSymbolRequest(fullyQualifiedSymbol, bounds),
                CancellationToken.None);
            RelationshipResult callees = await codeGraph.GetCalleesAsync(
                scope,
                new AnalysisSymbolRequest(fullyQualifiedSymbol, bounds),
                CancellationToken.None);
            RelatedFilesResult related = await codeGraph.GetRelatedFilesAsync(
                scope,
                new AnalysisSymbolRequest(fullyQualifiedSymbol, bounds),
                CancellationToken.None);

            Assert.True(search.Status == AnalysisOperationStatus.Succeeded, Describe(search.Failure));
            Assert.True(callers.Status == AnalysisOperationStatus.Succeeded, Describe(callers.Failure));
            Assert.True(callees.Status == AnalysisOperationStatus.Succeeded, Describe(callees.Failure));
            Assert.True(related.Status == AnalysisOperationStatus.Succeeded, Describe(related.Failure));
            Assert.All(search.Symbols, AssertContained);
            Assert.All(callers.Relationships, relationship => AssertContained(relationship));
            Assert.All(callees.Relationships, relationship => AssertContained(relationship));
            Assert.All(related.RelativePaths, path => Assert.False(Path.IsPathRooted(path)));
            Assert.All(related.RelativePaths, path => Assert.DoesNotContain("..", path));

            var javaFactory = new JdtLsAnalysisSessionFactory(
                new JdtLsAdapterOptions(
                    JavaExecutable,
                    JdtLsRoot,
                    javaWorkspaceRoot,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(10),
                    256 * 1024),
                JdtLsArchive,
                JdtLsArchiveHash);
            await using IJavaAnalysisSession java = await javaFactory.OpenAsync(scope, CancellationToken.None);
            JavaNavigationRequest position = new(
                "src/main/java/example/analysis/GreetingService.java",
                new AnalysisSourcePosition(12, 24),
                bounds);
            JavaNavigationRequest formatterPosition = new(
                "src/main/java/example/analysis/GreetingFormatter.java",
                new AnalysisSourcePosition(3, 21),
                bounds);
            JavaNavigationResult definition = await java.GetDefinitionAsync(scope, position, CancellationToken.None);
            JavaNavigationResult references = await java.GetReferencesAsync(scope, formatterPosition, CancellationToken.None);
            JavaNavigationResult implementations = await java.GetImplementationsAsync(
                scope,
                formatterPosition,
                CancellationToken.None);

            Assert.True(definition.Status == AnalysisOperationStatus.Succeeded, Describe(definition.Failure));
            Assert.True(references.Status == AnalysisOperationStatus.Succeeded, Describe(references.Failure));
            Assert.True(implementations.Status == AnalysisOperationStatus.Succeeded, Describe(implementations.Failure));
            Assert.All(definition.Evidence, AssertContained);
            Assert.All(references.Evidence, AssertContained);
            Assert.All(implementations.Evidence, AssertContained);

            // XML is an eligible source type for Pass 1, but Java navigation
            // is not a valid operation for it.  The typed Java port must fail
            // closed and must not return host paths or source text.
            JavaNavigationResult xml = await java.GetDocumentSymbolsAsync(
                scope,
                new JavaNavigationRequest("src/main/resources/neutral.xml", null, bounds),
                CancellationToken.None);
            Assert.True(xml.Status == AnalysisOperationStatus.Failed || xml.Evidence.Count == 0);
            Assert.All(xml.Evidence, AssertContained);
        }
        finally
        {
            DeleteDirectory(repositoryRoot);
            DeleteDirectory(javaWorkspaceRoot);
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ExactSourceFallback_IsSanitizedAndRevisionBound()
    {
        string root = CreateTemporaryRoot();
        const string relativePath = "src/main/resources/neutral.xml";
        string fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        const string source = "<neutral>\n  <entry value=\"bounded\" />\n</neutral>\n";
        await File.WriteAllTextAsync(fullPath, source);
        string sourceHash = Convert.ToHexStringLower(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source)));
        AnalysisScope scope = new(
            RepositoryId,
            Guid.Parse("12000000-0000-4000-8000-000000000323"),
            ProfiledRevision,
            "t023-source-binding",
            RuntimeIdentityHash,
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        try
        {
            IRevisionBoundSourceReader reader = new RevisionBoundSourceReader(
                new TestSnapshotLease(scope, root),
                scope.WorkingCopyBindingToken);
            SourceExcerptResult result = await reader.ReadAsync(
                scope,
                new RevisionBoundSourceRequest(
                    relativePath,
                    new AnalysisLineRange(1, 4),
                    sourceHash,
                    64 * 1024),
                CancellationToken.None);
            Assert.True(result.Status == AnalysisOperationStatus.Succeeded, Describe(result.Failure));
            Assert.Equal(scope.RepositoryId, result.RepositoryId);
            Assert.Equal(scope.ProfiledRevision, result.ProfiledRevision);
            Assert.Equal(relativePath, result.RelativePath);
            Assert.Equal(sourceHash, result.SourceSha256);
            Assert.Equal(source, result.Excerpt);
            Assert.InRange(result.ByteCount, 1, 64 * 1024);
            Assert.DoesNotContain(root, result.Excerpt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("HostPath", result.Excerpt, StringComparison.OrdinalIgnoreCase);

            SourceExcerptResult stale = await reader.ReadAsync(
                new AnalysisScope(
                    scope.RepositoryId,
                    scope.ScanRunId,
                    "fedcba9876543210fedcba9876543210fedcba98",
                    scope.WorkingCopyBindingToken,
                    scope.RuntimeIdentityHash,
                    new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds())),
                new RevisionBoundSourceRequest(relativePath, new AnalysisLineRange(1, 1), sourceHash, 4096),
                CancellationToken.None);
            Assert.Equal(AnalysisOperationStatus.Failed, stale.Status);
            Assert.DoesNotContain(source, stale.Excerpt, StringComparison.Ordinal);
            Assert.DoesNotContain(root, stale.Failure?.Diagnostic ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string Describe(AnalysisPortFailure? failure) => failure is null
        ? "typed tool operation did not return a diagnostic"
        : $"{failure.FailureCode}: {failure.Diagnostic}";

    private static void AssertContained(AnalysisSymbolEvidence evidence)
    {
        Assert.False(Path.IsPathRooted(evidence.RelativePath));
        Assert.DoesNotContain("..", evidence.RelativePath);
        Assert.InRange(evidence.Range.StartLine, 1, 10_000);
        Assert.True(evidence.Range.EndLine >= evidence.Range.StartLine);
    }

    private static void AssertContained(AnalysisRelationshipEvidence evidence)
    {
        Assert.False(Path.IsPathRooted(evidence.RelativePath));
        Assert.DoesNotContain("..", evidence.RelativePath);
        Assert.InRange(evidence.Range.StartLine, 1, 10_000);
        Assert.True(evidence.Range.EndLine >= evidence.Range.StartLine);
    }

    private static string FindFixtureRepository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("Repository root not found."),
            "tests",
            "Vulgata.Tests",
            "Fixtures",
            "AnalysisRuntime",
            "repository");
    }

    private static string CreateTemporaryRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "vulgata-story123-t023-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
        Assert.False(Directory.Exists(path));
    }

    private static async Task<string> RunAsync(string fileName, IReadOnlyList<string> arguments)
    {
        bool powershellScript = fileName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo(
            powershellScript ? "pwsh.exe" : fileName)
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
        Assert.True(process.ExitCode == 0, stderr);
        return stdout + stderr;
    }

    private static async Task<string> HashAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    private sealed class FixedSnapshotResolver(CodeGraphRepositorySnapshot snapshot)
        : ICodeGraphRepositorySnapshotResolver
    {
        public Task<CodeGraphRepositorySnapshot?> ResolveAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CodeGraphRepositorySnapshot?>(snapshot);
        }
    }

    private sealed class TestSnapshotLease(AnalysisScope scope, string root)
        : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId => scope.RepositoryId;
        public Guid ScanRunId => scope.ScanRunId;
        public string ProfiledRevision => scope.ProfiledRevision;
        public string RootPath => root;
        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

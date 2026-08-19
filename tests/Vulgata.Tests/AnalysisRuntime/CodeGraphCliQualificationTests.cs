using System.Security.Cryptography;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;

namespace Vulgata.Tests.AnalysisRuntime;

public sealed class CodeGraphCliQualificationTests
{
    private const string CommandPath = @"G:\packages\npm\prefix\codegraph.ps1";
    private const string CodeGraphHash =
        "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03";
    private const string NodeHash =
        "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3";
    private const string NpmShimHash =
        "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877";
    private static readonly Guid RepositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000002");

    [Fact]
    public async Task InstalledBundle_HealthProbeIsReady_AndDoesNotCreateIndex()
    {
        Assert.True(File.Exists(CommandPath));
        string packageRoot = Path.Combine(
            Path.GetDirectoryName(CommandPath)!,
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

        string fixture = CreateNeutralRepository();
        try
        {
            Assert.Empty(FindCodeGraphDirectories(fixture));
            AnalysisScope scope = CreateScope();
            AnalysisHealthProbeResult before = await new CodeGraphIntegrityProbe()
                .CheckConfiguredIntegrityAsync(scope, CancellationToken.None);
            AnalysisHealthProbeResult after = await new CodeGraphIntegrityProbe()
                .CheckConfiguredIntegrityAsync(scope, CancellationToken.None);

            Assert.Equal(AnalysisRuntimeHealthStatus.Ready, before.Status);
            Assert.Equal(AnalysisRuntimeHealthStatus.Ready, after.Status);
            Assert.Equal(before.Status, after.Status);
            Assert.Equal(before.FailureCode, after.FailureCode);
            Assert.Equal(before.Diagnostic, after.Diagnostic);
            Assert.Equal(before.ComponentChecks, after.ComponentChecks);
            Assert.Empty(FindCodeGraphDirectories(fixture));
        }
        finally
        {
            DeleteAndAssertClean(fixture);
        }
    }

    [Fact]
    public async Task RealCli_AllAllowedJsonOperations_ReturnTypedBoundedEvidence()
    {
        string fixture = CreateNeutralRepository();
        try
        {
            CodeGraphAdapterOptions options = new(
                CommandPath,
                TimeSpan.FromSeconds(30),
                StandardOutputByteLimit: 256 * 1024,
                StandardErrorByteLimit: 16 * 1024);
            AnalysisScope scope = CreateScope();
            CodeGraphRepositorySnapshot snapshot = new(
                scope.RepositoryId,
                scope.ScanRunId,
                scope.ProfiledRevision,
                scope.WorkingCopyBindingToken,
                runtimeIdentityHash: scope.RuntimeIdentityHash,
                repositoryRoot: fixture,
                expectedIndexIdentity: "t038-neutral-index");
            RecordingSnapshotResolver resolver = new(snapshot);
            RecordingProcessRunner process = new();
            ICodeGraphAnalysisPort adapter = new CodeGraphAnalysisAdapter(
                options,
                resolver,
                process);

            AnalysisIndexResult index = await adapter.IndexAsync(scope, CancellationToken.None);
            AnalysisIndexResult rebuild = await adapter.RebuildAsync(scope, CancellationToken.None);
            CodeGraphStatusResult status = await adapter.GetStatusAsync(scope, CancellationToken.None);
            SymbolSearchResult symbols = await adapter.SearchSymbolsAsync(
                scope,
                new AnalysisSymbolQuery(
                    "GreetingService",
                    AnalysisSymbolKind.Type,
                    new AnalysisOperationBounds(8, 16 * 1024)),
                CancellationToken.None);
            RelationshipResult callers = await adapter.GetCallersAsync(
                scope,
                new AnalysisSymbolRequest(
                    "example.analysis::GreetingService",
                    new AnalysisOperationBounds(8, 16 * 1024)),
                CancellationToken.None);
            RelationshipResult callees = await adapter.GetCalleesAsync(
                scope,
                new AnalysisSymbolRequest(
                    "example.analysis::GreetingService",
                    new AnalysisOperationBounds(8, 16 * 1024)),
                CancellationToken.None);
            RelatedFilesResult related = await adapter.GetRelatedFilesAsync(
                scope,
                new AnalysisSymbolRequest(
                    "example.analysis::GreetingService",
                    new AnalysisOperationBounds(8, 16 * 1024)),
                CancellationToken.None);

            AssertSucceeded(index);
            Assert.Equal("t038-neutral-index", index.IndexIdentity);
            Assert.True(index.IndexedFileCount > 0);
            AssertSucceeded(rebuild);
            Assert.Equal("t038-neutral-index", rebuild.IndexIdentity);
            Assert.True(rebuild.IndexedFileCount > 0);
            Assert.Equal(AnalysisOperationStatus.Succeeded, status.Status);
            Assert.True(status.Ready);
            Assert.False(status.Stale);
            Assert.Null(status.Failure);

            Assert.Equal(
                ["query", "GreetingService", "--kind", "class", "--path", fixture, "--limit", "8", "--json"],
                process.Requests[5].Arguments);
            Assert.Equal(AnalysisOperationStatus.Succeeded, symbols.Status);
            Assert.Null(symbols.Failure);
            AnalysisSymbolEvidence symbol = Assert.Single(symbols.Symbols);
            Assert.Equal("example.analysis::GreetingService", symbol.Symbol);
            Assert.Equal(AnalysisSymbolKind.Type, symbol.Kind);
            Assert.Equal("src/main/java/example/analysis/GreetingService.java", symbol.RelativePath);
            Assert.InRange(symbol.Range.StartLine, 1, 100);
            Assert.InRange(symbol.Range.EndLine, symbol.Range.StartLine, 100);

            Assert.Equal(AnalysisOperationStatus.Succeeded, callers.Status);
            Assert.Null(callers.Failure);
            Assert.Empty(callers.Relationships);
            AnalysisRelationshipEvidence callee = Assert.Single(callees.Relationships);
            Assert.Equal(AnalysisOperationStatus.Succeeded, callees.Status);
            Assert.Null(callees.Failure);
            Assert.Equal("example.analysis::GreetingService", callee.SourceSymbol);
            Assert.Equal("GreetingFormatter", callee.TargetSymbol);
            Assert.Equal("src/main/java/example/analysis/GreetingFormatter.java", callee.RelativePath);
            Assert.Equal(AnalysisOperationStatus.Succeeded, related.Status);
            Assert.Null(related.Failure);
            Assert.Contains(
                "src/main/java/example/analysis/GreetingService.java",
                related.RelativePaths);
            Assert.All(related.RelativePaths, path => Assert.DoesNotContain("..", path));

            Assert.Equal(9, resolver.Scopes.Count);
            Assert.All(resolver.Scopes, observed =>
            {
                Assert.Equal(scope.RepositoryId, observed.RepositoryId);
                Assert.Equal(scope.ScanRunId, observed.ScanRunId);
                Assert.Equal(scope.ProfiledRevision, observed.ProfiledRevision);
                Assert.Equal(scope.WorkingCopyBindingToken, observed.WorkingCopyBindingToken);
            });
            Assert.Equal(
                ["init", fixture],
                process.Requests[0].Arguments);
            Assert.Equal(
                ["index", "--quiet", fixture],
                process.Requests[2].Arguments);
            Assert.Equal(
                ["query", "GreetingService", "--kind", "class", "--path", fixture, "--limit", "8", "--json"],
                process.Requests[5].Arguments);
            Assert.All(process.Requests, request =>
            {
                Assert.Equal(CommandPath, request.FileName);
                Assert.Equal(fixture, request.WorkingDirectory);
                Assert.Equal(options.StandardOutputByteLimit, request.StandardOutputByteLimit);
                Assert.Equal(options.StandardErrorByteLimit, request.StandardErrorByteLimit);
            });
            Assert.NotEmpty(FindCodeGraphDirectories(fixture));
        }
        finally
        {
            DeleteAndAssertClean(fixture);
        }
    }

    private static void AssertSucceeded(AnalysisIndexResult result)
    {
        Assert.True(
            result.Status == AnalysisOperationStatus.Succeeded,
            result.Failure is null
                ? "no diagnostic"
                : $"{result.Failure.FailureCode}: {result.Failure.Diagnostic}");
        Assert.Null(result.Failure);
    }

    private static string CreateNeutralRepository()
    {
        string source = FindFixtureRepository();
        string destination = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story121-codegraph-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        CopyDirectory(source, destination);
        Assert.Empty(FindCodeGraphDirectories(destination));
        return destination;
    }

    private static void DeleteAndAssertClean(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Assert.False(Directory.Exists(path));
    }

    private static AnalysisScope CreateScope() => new(
        RepositoryId,
        Guid.NewGuid(),
        "0123456789abcdef0123456789abcdef01234567",
        "t038-" + Guid.NewGuid().ToString("N"),
        new string('a', 64),
        new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds()));

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

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static IReadOnlyList<string> FindCodeGraphDirectories(string root) =>
        Directory.Exists(root)
            ? Directory.GetDirectories(root, ".codegraph", SearchOption.AllDirectories)
            : [];

    private static async Task<string> HashAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    private sealed class RecordingSnapshotResolver(CodeGraphRepositorySnapshot snapshot)
        : ICodeGraphRepositorySnapshotResolver
    {
        public List<AnalysisScope> Scopes { get; } = [];

        public Task<CodeGraphRepositorySnapshot?> ResolveAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Scopes.Add(scope);
            return Task.FromResult<CodeGraphRepositorySnapshot?>(snapshot);
        }
    }

    private sealed class RecordingProcessRunner : ICodeGraphProcessRunner
    {
        private readonly CodeGraphProcessRunner _inner = new();

        public List<CodeGraphProcessRequest> Requests { get; } = [];

        public async Task<CodeGraphProcessResult> RunAsync(
            CodeGraphProcessRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return await _inner.RunAsync(request, cancellationToken);
        }
    }
}

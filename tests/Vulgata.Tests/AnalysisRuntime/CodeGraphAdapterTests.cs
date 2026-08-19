using System.Reflection;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.CodeGraph;

namespace Vulgata.Tests.AnalysisRuntime;

public sealed class CodeGraphAdapterTests
{
    private const string CodeGraphHash =
        "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03";
    private const string NodeHash =
        "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3";
    private const string NpmShimHash =
        "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877";
    private const string JdtArchiveHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly Guid _repositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000002");

    [Fact]
    public async Task CodeGraphAdapter_UsesApprovedIdentity_AndClosedPublicSeamWithoutStartupIndexing()
    {
        AnalysisRuntimeIdentity identity = AnalysisRuntimeIdentity.Create(
            new AnalysisRuntimeOptions(
                CodeGraphCommandPath: @"G:\packages\npm\prefix\codegraph.ps1",
                JdtLsInstallationRoot:
                    @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332",
                JavaHome: @"C:\Program Files\Java\latest\jdk-21"),
            _repositoryId,
            profiledRevision: "0123456789abcdef0123456789abcdef01234567",
            codeGraphPackageName: "@colbymchenry/codegraph",
            codeGraphPackageVersion: "1.5.0",
            codeGraphRepository: "https://github.com/colbymchenry/codegraph",
            codeGraphCommand: "codegraph",
            codeGraphPlatformPackage: "@colbymchenry/codegraph-win32-x64@1.5.0",
            codeGraphHashes:
            [
                new("lib/dist/bin/codegraph.js", CodeGraphHash),
                new("node.exe", NodeHash),
                new("npm-shim.js", NpmShimHash),
            ],
            jdtLsReleaseIdentity: "jdt-language-server-1.61.0-202608141332",
            jdtLsDistributionArchiveName:
                "jdt-language-server-1.61.0-202608141332.zip",
            jdtLsDistributionArchiveSha256: JdtArchiveHash,
            javaMajorVersion: 21,
            clientName: "StreamJsonRpc",
            clientVersion: "2.25.29",
            transport: "stdio");

        Assert.Equal("@colbymchenry/codegraph", identity.CodeGraphPackageName);
        Assert.Equal("1.5.0", identity.CodeGraphPackageVersion);
        Assert.Equal("https://github.com/colbymchenry/codegraph", identity.CodeGraphRepository);
        Assert.Equal("codegraph", identity.CodeGraphCommand);
        Assert.Equal(
            "@colbymchenry/codegraph-win32-x64@1.5.0",
            identity.CodeGraphPlatformPackage);
        Assert.Equal(
            new[]
            {
                new AnalysisRuntimeComponentHash("lib/dist/bin/codegraph.js", CodeGraphHash),
                new AnalysisRuntimeComponentHash("node.exe", NodeHash),
                new AnalysisRuntimeComponentHash("npm-shim.js", NpmShimHash),
            },
            identity.CodeGraphFiles);

        Assembly infrastructureAssembly = typeof(CodeGraphContextProvider).Assembly;
        Type[] adapterTypes = infrastructureAssembly
            .GetTypes()
            .Where(type =>
                type.Namespace == "Vulgata.Infrastructure.Analysis.CodeGraph" &&
                !type.IsAbstract &&
                typeof(ICodeGraphAnalysisPort).IsAssignableFrom(type))
            .ToArray();

        // This is the T030 RED: T031 must provide the production adapter behind
        // the already accepted Core port. Reflection keeps the RED compiling
        // before that production type exists and does not invent a second seam.
        Type adapterType = Assert.Single(adapterTypes);
        Assert.Equal(
            new[]
            {
                "GetCalleesAsync",
                "GetCallersAsync",
                "GetRelatedFilesAsync",
                "GetStatusAsync",
                "IndexAsync",
                "RebuildAsync",
                "SearchSymbolsAsync",
            },
            adapterType
                .GetInterfaces()
                .Single(interfaceType => interfaceType == typeof(ICodeGraphAnalysisPort))
                .GetMethods()
                .Select(method => method.Name)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.DoesNotContain(
            adapterType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method =>
                new[] { "Explore", "Node", "Files", "Daemon", "Unlock", "Install", "Upgrade", "Telemetry" }
                    .Contains(method.Name, StringComparer.OrdinalIgnoreCase));

        Type healthType = typeof(CodeGraphIntegrityProbe);
        Assert.Equal(
            new[] { "CheckConfiguredIntegrityAsync" },
            typeof(IAnalysisRuntimeHealthProbe)
                .GetMethods()
                .Select(method => method.Name)
                .ToArray());
        Assert.DoesNotContain(
            healthType.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name is "IndexAsync" or "RebuildAsync");

        AnalysisPortFailure failure = new(
            AnalysisPortFailureReason.ProcessFailed,
            "codegraph_process_failed",
            "stderr C:\\Users\\13728\\private\\repo\\secret.cs token=do-not-leak");
        Assert.DoesNotContain("C:\\Users", failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("do-not-leak", failure.Diagnostic, StringComparison.Ordinal);
        Assert.InRange(failure.Diagnostic.Length, 1, 256);

        string probeRoot = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story121-codegraph-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(probeRoot);
        try
        {
            string commandPath = @"G:\packages\npm\prefix\codegraph.ps1";
            Assert.True(File.Exists(commandPath), "The supplied codegraph CLI path must remain available.");
            string commandRoot = Path.GetDirectoryName(commandPath)!;
            IReadOnlyList<string> before = SnapshotCodeGraphDirectories(commandRoot, probeRoot);

            AnalysisScope scope = new(
                _repositoryId,
                Guid.Parse("12000000-0000-4000-8000-000000000003"),
                "0123456789abcdef0123456789abcdef01234567",
                "fixture-binding",
                new string('a', 64),
                new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
            IAnalysisRuntimeHealthProbe probe = new CodeGraphIntegrityProbe();
            AnalysisHealthProbeResult result =
                await probe.CheckConfiguredIntegrityAsync(scope, CancellationToken.None);

            Assert.Equal(AnalysisRuntimeHealthStatus.Ready, result.Status);
            Assert.Equal("none", result.FailureCode);
            Assert.Equal(string.Empty, result.Diagnostic);
            Assert.Contains(
                result.ComponentChecks,
                check => check.Component == "codegraph" &&
                    check.Status == AnalysisRuntimeHealthStatus.Ready);
            Assert.Equal(before, SnapshotCodeGraphDirectories(commandRoot, probeRoot));
        }
        finally
        {
            Directory.Delete(probeRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CodeGraphPort_CurrentUnboundAdapter_FailsClosedForEveryAllowedOperation()
    {
        ICodeGraphAnalysisPort adapter = new CodeGraphAnalysisAdapter();
        AnalysisScope scope = new(
            _repositoryId,
            Guid.Parse("12000000-0000-4000-8000-000000000004"),
            "0123456789abcdef0123456789abcdef01234567",
            "fixture-binding",
            new string('a', 64),
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        AnalysisOperationBounds bounds = new(itemLimit: 8, byteLimit: 4_096);
        AnalysisSymbolQuery symbolQuery = new("Fixture", AnalysisSymbolKind.Type, bounds);
        AnalysisSymbolRequest symbolRequest = new("Fixture", bounds);
        string probeRoot = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story121-codegraph-unbound-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(probeRoot);

        try
        {
            IReadOnlyList<string> before = SnapshotCodeGraphDirectories(probeRoot);
            AnalysisIndexResult index = await adapter.IndexAsync(scope, CancellationToken.None);
            AnalysisIndexResult rebuild = await adapter.RebuildAsync(scope, CancellationToken.None);
            CodeGraphStatusResult status = await adapter.GetStatusAsync(scope, CancellationToken.None);
            SymbolSearchResult search = await adapter.SearchSymbolsAsync(
                scope,
                symbolQuery,
                CancellationToken.None);
            RelationshipResult callers = await adapter.GetCallersAsync(
                scope,
                symbolRequest,
                CancellationToken.None);
            RelationshipResult callees = await adapter.GetCalleesAsync(
                scope,
                symbolRequest,
                CancellationToken.None);
            RelatedFilesResult related = await adapter.GetRelatedFilesAsync(
                scope,
                symbolRequest,
                CancellationToken.None);

            AssertUnavailable(index.Status, index.Failure);
            AssertUnavailable(rebuild.Status, rebuild.Failure);
            AssertUnavailable(status.Status, status.Failure);
            AssertUnavailable(search.Status, search.Failure);
            AssertUnavailable(callers.Status, callers.Failure);
            AssertUnavailable(callees.Status, callees.Failure);
            AssertUnavailable(related.Status, related.Failure);
            Assert.Empty(search.Symbols);
            Assert.Empty(callers.Relationships);
            Assert.Empty(callees.Relationships);
            Assert.Empty(related.RelativePaths);
            Assert.Equal(before, SnapshotCodeGraphDirectories(probeRoot));

            using CancellationTokenSource cancelled = new();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                adapter.IndexAsync(scope, cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                adapter.RebuildAsync(scope, cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                adapter.GetStatusAsync(scope, cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                adapter.SearchSymbolsAsync(scope, symbolQuery, cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                adapter.GetCallersAsync(scope, symbolRequest, cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                adapter.GetCalleesAsync(scope, symbolRequest, cancelled.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                adapter.GetRelatedFilesAsync(scope, symbolRequest, cancelled.Token));
            Assert.Equal(before, SnapshotCodeGraphDirectories(probeRoot));
        }
        finally
        {
            Directory.Delete(probeRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CodeGraphPort_AllowedJsonOperations_ReturnBoundedTypedEvidenceFromTrustedFixture()
    {
        // T030 RED: the accepted closed Core seam must map fixed JSON operation
        // results to bounded typed evidence through Infrastructure-owned doubles.
        // No installed CLI is launched and no real Repository index is touched.
        const string root = @"G:\fixtures\story121-codegraph";
        const string indexIdentity = "fixture-index-001";
        AnalysisRuntimeIdentity runtimeIdentity = AnalysisRuntimeIdentity.Create(
            new AnalysisRuntimeOptions(
                CodeGraphCommandPath: @"G:\packages\npm\prefix\codegraph.ps1",
                JdtLsInstallationRoot: @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332",
                JavaHome: @"C:\Program Files\Java\latest\jdk-21"),
            _repositoryId,
            profiledRevision: "0123456789abcdef0123456789abcdef01234567",
            codeGraphPackageName: "@colbymchenry/codegraph",
            codeGraphPackageVersion: "1.5.0",
            codeGraphRepository: "https://github.com/colbymchenry/codegraph",
            codeGraphCommand: "codegraph",
            codeGraphPlatformPackage: "@colbymchenry/codegraph-win32-x64@1.5.0",
            codeGraphHashes:
            [
                new("lib/dist/bin/codegraph.js", CodeGraphHash),
                new("node.exe", NodeHash),
                new("npm-shim.js", NpmShimHash),
            ],
            jdtLsReleaseIdentity: "jdt-language-server-1.61.0-202608141332",
            jdtLsDistributionArchiveName: "jdt-language-server-1.61.0-202608141332.zip",
            jdtLsDistributionArchiveSha256: JdtArchiveHash,
            javaMajorVersion: 21,
            clientName: "StreamJsonRpc",
            clientVersion: "2.25.29",
            transport: "stdio");
        string runtimeIdentityHash = runtimeIdentity.Sha256;
        AnalysisScope scope = new(
            _repositoryId,
            Guid.Parse("12000000-0000-4000-8000-000000000005"),
            "0123456789abcdef0123456789abcdef01234567",
            "fixture-binding",
            runtimeIdentityHash,
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        AnalysisOperationBounds bounds = new(itemLimit: 8, byteLimit: 4_096);
        AnalysisSymbolQuery query = new("Fixture", AnalysisSymbolKind.Type, bounds);
        AnalysisSymbolRequest request = new("Fixture.Root", bounds);

        CodeGraphRepositorySnapshot snapshot = new(
            _repositoryId,
            scope.ScanRunId,
            scope.ProfiledRevision,
            scope.WorkingCopyBindingToken,
            root,
            indexIdentity);
        FakeCodeGraphSnapshotResolver snapshots = new(snapshot);
        FakeCodeGraphProcessRunner process = new();
        process.Enqueue(JsonResult("{\"status\":\"ok\",\"indexIdentity\":\"fixture-index-001\",\"durationMs\":12,\"indexedFileCount\":3}"));
        process.Enqueue(JsonResult("{\"status\":\"ok\",\"indexIdentity\":\"fixture-index-001\",\"durationMs\":18,\"indexedFileCount\":3}"));
        process.Enqueue(JsonResult("{\"status\":\"ready\",\"indexIdentity\":\"fixture-index-001\",\"indexedFileCount\":3}"));
        process.Enqueue(JsonResult("{\"symbols\":[{\"symbol\":\"Fixture.Root\",\"kind\":\"type\",\"relativePath\":\"Fixture/Root.cs\",\"startLine\":2,\"endLine\":4}]}"));
        process.Enqueue(JsonResult("{\"relationships\":[{\"sourceSymbol\":\"Fixture.Caller\",\"targetSymbol\":\"Fixture.Root\",\"relativePath\":\"Fixture/Caller.cs\",\"startLine\":7,\"endLine\":9}]}"));
        process.Enqueue(JsonResult("{\"relationships\":[{\"sourceSymbol\":\"Fixture.Root\",\"targetSymbol\":\"Fixture.Callee\",\"relativePath\":\"Fixture/Callee.cs\",\"startLine\":11,\"endLine\":13}]}"));
        process.Enqueue(JsonResult("{\"relativePaths\":[\"Fixture/Caller.cs\",\"Fixture/Callee.cs\"]}"));

        CodeGraphAdapterOptions options = new(
            @"G:\packages\npm\prefix\codegraph.ps1",
            TimeSpan.FromSeconds(5),
            StandardOutputByteLimit: 65_536,
            StandardErrorByteLimit: 4_096);
        ICodeGraphAnalysisPort adapter = new CodeGraphAnalysisAdapter(options, snapshots, process);

        AnalysisIndexResult index = await adapter.IndexAsync(scope, CancellationToken.None);
        AnalysisIndexResult rebuild = await adapter.RebuildAsync(scope, CancellationToken.None);
        CodeGraphStatusResult status = await adapter.GetStatusAsync(scope, CancellationToken.None);
        SymbolSearchResult symbols = await adapter.SearchSymbolsAsync(
            scope,
            query,
            CancellationToken.None);
        RelationshipResult callers = await adapter.GetCallersAsync(
            scope,
            request,
            CancellationToken.None);
        RelationshipResult callees = await adapter.GetCalleesAsync(
            scope,
            request,
            CancellationToken.None);
        RelatedFilesResult relatedFiles = await adapter.GetRelatedFilesAsync(
            scope,
            request,
            CancellationToken.None);

        Assert.Equal(AnalysisOperationStatus.Succeeded, index.Status);
        Assert.Equal(indexIdentity, index.IndexIdentity);
        Assert.Equal(12, index.DurationMilliseconds);
        Assert.Equal(3, index.IndexedFileCount);
        Assert.Null(index.Failure);
        Assert.Equal(AnalysisOperationStatus.Succeeded, rebuild.Status);
        Assert.Equal(indexIdentity, rebuild.IndexIdentity);
        Assert.Equal(18, rebuild.DurationMilliseconds);
        Assert.Equal(3, rebuild.IndexedFileCount);
        Assert.Null(rebuild.Failure);
        Assert.Equal(AnalysisOperationStatus.Succeeded, status.Status);
        Assert.True(status.Ready);
        Assert.False(status.Stale);
        Assert.Equal(3, status.IndexedFileCount);
        Assert.Null(status.Failure);
        Assert.Equal(indexIdentity, snapshots.LastExpectedIndexIdentity);
        Assert.Equal(7, snapshots.Scopes.Count);

        AnalysisSymbolEvidence symbol = Assert.Single(symbols.Symbols);
        Assert.Equal(AnalysisOperationStatus.Succeeded, symbols.Status);
        Assert.Null(symbols.Failure);
        Assert.Equal("Fixture.Root", symbol.Symbol);
        Assert.Equal(AnalysisSymbolKind.Type, symbol.Kind);
        Assert.Equal("Fixture/Root.cs", symbol.RelativePath);
        Assert.Equal(2, symbol.Range.StartLine);
        Assert.Equal(4, symbol.Range.EndLine);

        AnalysisRelationshipEvidence caller = Assert.Single(callers.Relationships);
        Assert.Equal(AnalysisOperationStatus.Succeeded, callers.Status);
        Assert.Null(callers.Failure);
        Assert.Equal("Fixture.Caller", caller.SourceSymbol);
        Assert.Equal("Fixture.Root", caller.TargetSymbol);
        Assert.Equal("Fixture/Caller.cs", caller.RelativePath);
        Assert.Equal(7, caller.Range.StartLine);
        Assert.Equal(9, caller.Range.EndLine);

        AnalysisRelationshipEvidence callee = Assert.Single(callees.Relationships);
        Assert.Equal(AnalysisOperationStatus.Succeeded, callees.Status);
        Assert.Null(callees.Failure);
        Assert.Equal("Fixture.Root", callee.SourceSymbol);
        Assert.Equal("Fixture.Callee", callee.TargetSymbol);
        Assert.Equal("Fixture/Callee.cs", callee.RelativePath);
        Assert.Equal(11, callee.Range.StartLine);
        Assert.Equal(13, callee.Range.EndLine);

        Assert.Equal(AnalysisOperationStatus.Succeeded, relatedFiles.Status);
        Assert.Null(relatedFiles.Failure);
        Assert.Equal(
            ["Fixture/Caller.cs", "Fixture/Callee.cs"],
            relatedFiles.RelativePaths);

        Assert.Equal(7, process.Requests.Count);
        Assert.Equal(
            ["init", root],
            process.Requests[0].Arguments);
        Assert.Equal(
            ["index", "--quiet", root],
            process.Requests[1].Arguments);
        Assert.Equal(
            ["status", root, "--json"],
            process.Requests[2].Arguments);
        Assert.Equal(
            ["query", "Fixture", "--kind", "type", "--path", root, "--limit", "8", "--json"],
            process.Requests[3].Arguments);
        Assert.Equal(
            ["callers", "Fixture.Root", "--path", root, "--limit", "8", "--json"],
            process.Requests[4].Arguments);
        Assert.Equal(
            ["callees", "Fixture.Root", "--path", root, "--limit", "8", "--json"],
            process.Requests[5].Arguments);
        Assert.Equal(
            ["impact", "Fixture.Root", "--path", root, "--depth", "1", "--json"],
            process.Requests[6].Arguments);
        Assert.All(process.Requests, request =>
        {
            Assert.Equal(options.CommandPath, request.FileName);
            Assert.Equal(root, request.WorkingDirectory);
            Assert.Equal(options.OperationTimeout, request.Timeout);
            Assert.Equal(options.StandardOutputByteLimit, request.StandardOutputByteLimit);
            Assert.Equal(options.StandardErrorByteLimit, request.StandardErrorByteLimit);
        });
        Assert.All(snapshots.Scopes, observed =>
        {
            Assert.Equal(scope.RepositoryId, observed.RepositoryId);
            Assert.Equal(scope.ScanRunId, observed.ScanRunId);
            Assert.Equal(scope.ProfiledRevision, observed.ProfiledRevision);
            Assert.Equal(scope.WorkingCopyBindingToken, observed.WorkingCopyBindingToken);
            Assert.Equal(scope.RuntimeIdentityHash, observed.RuntimeIdentityHash);
        });
    }

    private static CodeGraphProcessResult JsonResult(string json) =>
        new(0, Encoding.UTF8.GetBytes(json), [], TimedOut: false);

    private sealed class FakeCodeGraphSnapshotResolver(CodeGraphRepositorySnapshot snapshot)
        : ICodeGraphRepositorySnapshotResolver
    {
        public List<AnalysisScope> Scopes { get; } = [];

        public string LastExpectedIndexIdentity => snapshot.ExpectedIndexIdentity;

        public Task<CodeGraphRepositorySnapshot?> ResolveAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Scopes.Add(scope);
            return Task.FromResult<CodeGraphRepositorySnapshot?>(snapshot);
        }
    }

    private sealed class FakeCodeGraphProcessRunner : ICodeGraphProcessRunner
    {
        private readonly Queue<CodeGraphProcessResult> _results = [];

        public List<CodeGraphProcessRequest> Requests { get; } = [];

        public void Enqueue(CodeGraphProcessResult result) => _results.Enqueue(result);

        public Task<CodeGraphProcessResult> RunAsync(
            CodeGraphProcessRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private static void AssertUnavailable(
        AnalysisOperationStatus status,
        AnalysisPortFailure? failure)
    {
        Assert.Equal(AnalysisOperationStatus.Failed, status);
        Assert.NotNull(failure);
        Assert.Equal(AnalysisPortFailureReason.Unavailable, failure!.Reason);
        Assert.Equal("codegraph_binding_unavailable", failure.FailureCode);
        Assert.Equal("codegraph binding unavailable", failure.Diagnostic);
        Assert.InRange(failure.Diagnostic.Length, 1, 256);
    }

    private static IReadOnlyList<string> SnapshotCodeGraphDirectories(params string[] roots) =>
        roots
            .SelectMany(root => Directory.Exists(root)
                ? Directory.GetDirectories(root, ".codegraph", SearchOption.AllDirectories)
                : [])
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

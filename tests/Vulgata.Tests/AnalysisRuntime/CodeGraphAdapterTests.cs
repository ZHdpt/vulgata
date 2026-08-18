using System.Reflection;
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

using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Analysis.Source;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// T041 RED: one revision-bound evidence slice across every approved analysis
/// operation.  The doubles substitute only the process, repository snapshot,
/// JDT protocol, and committed snapshot seams; no real tool or host path is
/// used as public or persisted evidence.
/// </summary>
public sealed class BoundedAnalysisEvidenceTests
{
    private static readonly Guid RepositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000012");
    private static readonly Guid ScanRunId =
        Guid.Parse("12000000-0000-4000-8000-000000000112");
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string BindingToken = "binding-1200000000000000000000000000000000000012";
    private const string RuntimeHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ApprovedOperations_ReturnBoundedEvidenceAndRevalidateExactRevisionBinding()
    {
        string root = FindFixtureRepositoryRoot();
        AnalysisScope scope = CreateScope(RuntimeHash);

        RecordingSnapshotResolver snapshots = new(root, scope);
        RecordingCodeGraphProcess process = new(root);
        ICodeGraphAnalysisPort codeGraph = new CodeGraphAnalysisAdapter(
            new CodeGraphAdapterOptions("C:/tools/codegraph.ps1", TimeSpan.FromSeconds(5), 65_536, 8_192),
            snapshots,
            process);

        AnalysisOperationBounds bounds = new(16, 65_536);
        AnalysisIndexResult index = await codeGraph.IndexAsync(scope, CancellationToken.None);
        AnalysisIndexResult rebuild = await codeGraph.RebuildAsync(scope, CancellationToken.None);
        CodeGraphStatusResult status = await codeGraph.GetStatusAsync(scope, CancellationToken.None);
        SymbolSearchResult search = await codeGraph.SearchSymbolsAsync(
            scope,
            new AnalysisSymbolQuery("GreetingService", AnalysisSymbolKind.Type, bounds),
            CancellationToken.None);
        RelationshipResult callers = await codeGraph.GetCallersAsync(
            scope,
            new AnalysisSymbolRequest("GreetingService", bounds),
            CancellationToken.None);
        RelationshipResult callees = await codeGraph.GetCalleesAsync(
            scope,
            new AnalysisSymbolRequest("GreetingService", bounds),
            CancellationToken.None);
        RelatedFilesResult related = await codeGraph.GetRelatedFilesAsync(
            scope,
            new AnalysisSymbolRequest("GreetingService", bounds),
            CancellationToken.None);

        Assert.Equal(AnalysisOperationStatus.Succeeded, index.Status);
        Assert.Equal(AnalysisOperationStatus.Succeeded, rebuild.Status);
        Assert.Equal(AnalysisOperationStatus.Succeeded, status.Status);
        Assert.Equal(AnalysisOperationStatus.Succeeded, search.Status);
        Assert.Equal(AnalysisOperationStatus.Succeeded, callers.Status);
        Assert.Equal(AnalysisOperationStatus.Succeeded, callees.Status);
        Assert.Equal(AnalysisOperationStatus.Succeeded, related.Status);
        Assert.All(search.Symbols, evidence => AssertContainedEvidence(evidence));
        Assert.All(callers.Relationships, evidence => AssertContainedRelationship(evidence));
        Assert.All(callees.Relationships, evidence => AssertContainedRelationship(evidence));
        Assert.All(related.RelativePaths, path => Assert.DoesNotContain("..", path));
        Assert.Equal(7 + 2, snapshots.Scopes.Count);
        Assert.All(snapshots.Scopes, observed => AssertScopeEqual(scope, observed));
        Assert.All(process.Requests, request => Assert.Equal(root, request.WorkingDirectory));

        RecordingJavaProtocol protocol = new();
        IJavaAnalysisSessionFactory javaFactory = new JdtLsAnalysisSessionFactory(
            new JdtLsAdapterOptions(
                "C:/Program Files/Java/latest/jdk-21/bin/java.exe",
                "C:/Users/13728/.local/jdt-language-server-1.61.0-202608141332",
                "C:/workspaces",
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5),
                65_536),
            new RecordingWorkspaceLeaseProvider(scope),
            new RecordingJavaProcessLauncher(),
            new RecordingJavaProtocolFactory(protocol));

        await using IJavaAnalysisSession session = await javaFactory.OpenAsync(scope, CancellationToken.None);
        JavaNavigationRequest document = new(
            "src/main/java/example/analysis/GreetingService.java",
            position: null,
            bounds);
        JavaNavigationRequest position = new(
            document.RelativePath,
            new AnalysisSourcePosition(8, 12),
            bounds);
        JavaNavigationResult symbols = await session.GetDocumentSymbolsAsync(scope, document, CancellationToken.None);
        JavaNavigationResult definition = await session.GetDefinitionAsync(scope, position, CancellationToken.None);
        JavaNavigationResult references = await session.GetReferencesAsync(scope, position, CancellationToken.None);
        JavaNavigationResult implementations = await session.GetImplementationsAsync(scope, position, CancellationToken.None);
        Assert.All(new[] { symbols, definition, references, implementations }, result =>
        {
            Assert.Equal(AnalysisOperationStatus.Succeeded, result.Status);
            Assert.All(result.Evidence, AssertContainedEvidence);
        });
        Assert.Equal(4, protocol.Scopes.Count);
        Assert.All(protocol.Scopes, observed => AssertScopeEqual(scope, observed));

        TestSnapshotLease sourceLease = new(scope, root);
        IRevisionBoundSourceReader source = new RevisionBoundSourceReader(sourceLease, BindingToken);
        const string expectedHash = "50b7491a6ad4a65e4f794127c0b90608b520b4874b6a2006a28073b3d9fa62ee";
        RevisionBoundSourceRequest sourceRequest = new(
            "src/main/java/example/analysis/GreetingService.java",
            new AnalysisLineRange(5, 9),
            expectedHash,
            4_096);
        SourceExcerptResult excerpt = await source.ReadAsync(scope, sourceRequest, CancellationToken.None);
        Assert.Equal(AnalysisOperationStatus.Succeeded, excerpt.Status);
        Assert.Equal(RepositoryId, excerpt.RepositoryId);
        Assert.Equal(ScanRunId, sourceLease.VerifiedScanRunId);
        Assert.Equal(Revision, excerpt.ProfiledRevision);
        Assert.Equal(BindingToken, sourceLease.VerifiedBindingToken);
    }

    [Fact]
    public async Task ApprovedOperations_RejectRuntimeIdentityDriftBeforeToolOrSourceEvidence()
    {
        string root = FindFixtureRepositoryRoot();
        AnalysisScope expected = CreateScope(RuntimeHash);
        AnalysisScope drifted = CreateScope(new string('c', 64));
        RecordingSnapshotResolver snapshots = new(root, expected);
        RecordingCodeGraphProcess process = new(root);
        ICodeGraphAnalysisPort codeGraph = new CodeGraphAnalysisAdapter(
            new CodeGraphAdapterOptions("C:/tools/codegraph.ps1", TimeSpan.FromSeconds(5), 65_536, 8_192),
            snapshots,
            process);

        SymbolSearchResult search = await codeGraph.SearchSymbolsAsync(
            drifted,
            new AnalysisSymbolQuery("GreetingService", AnalysisSymbolKind.Type, new AnalysisOperationBounds(4, 4_096)),
            CancellationToken.None);
        Assert.Equal(AnalysisOperationStatus.Failed, search.Status);
        Assert.Equal(AnalysisPortFailureReason.Rejected, search.Failure?.Reason);
        Assert.Empty(process.Requests);
        Assert.Single(snapshots.Scopes);
        AssertScopeEqual(drifted, snapshots.Scopes[0]);

        TestSnapshotLease lease = new(expected, root);
        IRevisionBoundSourceReader source = new RevisionBoundSourceReader(lease, BindingToken);
        AnalysisScope sourceDrifted = new(
            RepositoryId,
            ScanRunId,
            Revision,
            "binding-drift-1200000000000000000000000000000000000012",
            RuntimeHash,
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        SourceExcerptResult sourceResult = await source.ReadAsync(
            sourceDrifted,
            new RevisionBoundSourceRequest(
                "src/main/java/example/analysis/GreetingService.java",
                new AnalysisLineRange(5, 9),
                "50b7491a6ad4a65e4f794127c0b90608b520b4874b6a2006a28073b3d9fa62ee",
                4_096),
            CancellationToken.None);
        Assert.Equal(AnalysisOperationStatus.Failed, sourceResult.Status);
        Assert.Equal(AnalysisPortFailureReason.Rejected, sourceResult.Failure?.Reason);
        Assert.Equal(0, lease.VerifyCalls);
    }

    private static AnalysisScope CreateScope(string runtimeHash) => new(
        RepositoryId,
        ScanRunId,
        Revision,
        BindingToken,
        runtimeHash,
        new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));

    private static string FindFixtureRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not locate the Vulgata repository root.")
            : Path.GetFullPath(Path.Combine(
                directory.FullName,
                "tests",
                "Vulgata.Tests",
                "Fixtures",
                "AnalysisRuntime",
                "repository"));
    }

    private static void AssertScopeEqual(AnalysisScope expected, AnalysisScope actual)
    {
        Assert.Equal(expected.RepositoryId, actual.RepositoryId);
        Assert.Equal(expected.ScanRunId, actual.ScanRunId);
        Assert.Equal(expected.ProfiledRevision, actual.ProfiledRevision);
        Assert.Equal(expected.WorkingCopyBindingToken, actual.WorkingCopyBindingToken);
        Assert.Equal(expected.RuntimeIdentityHash, actual.RuntimeIdentityHash);
    }

    private static void AssertContainedEvidence(AnalysisSymbolEvidence evidence)
    {
        Assert.False(string.IsNullOrWhiteSpace(evidence.Symbol));
        Assert.DoesNotContain("..", evidence.RelativePath);
        Assert.True(evidence.Range.StartLine > 0);
        Assert.True(evidence.Range.EndLine >= evidence.Range.StartLine);
    }

    private static void AssertContainedRelationship(AnalysisRelationshipEvidence evidence)
    {
        Assert.False(string.IsNullOrWhiteSpace(evidence.SourceSymbol));
        Assert.False(string.IsNullOrWhiteSpace(evidence.TargetSymbol));
        Assert.DoesNotContain("..", evidence.RelativePath);
        Assert.True(evidence.Range.StartLine > 0);
        Assert.True(evidence.Range.EndLine >= evidence.Range.StartLine);
    }

    private sealed class RecordingSnapshotResolver(string root, AnalysisScope authoritativeScope) : ICodeGraphRepositorySnapshotResolver
    {
        public List<AnalysisScope> Scopes { get; } = [];
        public Task<CodeGraphRepositorySnapshot?> ResolveAsync(AnalysisScope scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Scopes.Add(scope);
            return Task.FromResult<CodeGraphRepositorySnapshot?>(new CodeGraphRepositorySnapshot(
                authoritativeScope.RepositoryId,
                authoritativeScope.ScanRunId,
                authoritativeScope.ProfiledRevision,
                authoritativeScope.WorkingCopyBindingToken,
                root,
                "fixture-index",
                runtimeIdentityHash: authoritativeScope.RuntimeIdentityHash));
        }
    }

    private sealed class RecordingCodeGraphProcess(string root) : ICodeGraphProcessRunner
    {
        public List<CodeGraphProcessRequest> Requests { get; } = [];
        public Task<CodeGraphProcessResult> RunAsync(CodeGraphProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            string command = request.Arguments[0];
            string json = command switch
            {
                "status" => $"{{\"initialized\":true,\"version\":\"1.5.0\",\"projectPath\":\"{root.Replace("\\", "\\\\")}\",\"indexPath\":\"{root.Replace("\\", "\\\\")}\\\\.codegraph\",\"lastIndexed\":\"2026-08-19T00:00:00Z\",\"fileCount\":1,\"nodeCount\":1,\"edgeCount\":0,\"dbSizeBytes\":1,\"backend\":\"node-sqlite\",\"journalMode\":\"wal\",\"nodesByKind\":{{\"class\":1}},\"languages\":[\"java\"],\"pendingChanges\":{{\"added\":0,\"modified\":0,\"removed\":0}},\"worktreeMismatch\":null,\"index\":{{\"builtWithVersion\":\"1.5.0\",\"builtWithExtractionVersion\":24,\"currentExtractionVersion\":24,\"reindexRecommended\":false,\"state\":\"complete\",\"pendingRefs\":0}}}}",
                "query" => "[{\"node\":{\"id\":\"1\",\"kind\":\"class\",\"name\":\"GreetingService\",\"qualifiedName\":\"example.GreetingService\",\"filePath\":\"src/main/java/example/GreetingService.java\",\"language\":\"java\",\"startLine\":1,\"endLine\":2,\"startColumn\":0,\"endColumn\":16,\"visibility\":\"public\",\"isExported\":true,\"isAsync\":false,\"isStatic\":false,\"isAbstract\":false,\"updatedAt\":0},\"score\":1.0}]",
                "callers" => "{\"symbol\":\"GreetingService\",\"callers\":[{\"name\":\"run\",\"kind\":\"method\",\"filePath\":\"src/main/java/example/App.java\",\"startLine\":1}]}",
                "callees" => "{\"symbol\":\"GreetingService\",\"callees\":[{\"name\":\"format\",\"kind\":\"method\",\"filePath\":\"src/main/java/example/Formatter.java\",\"startLine\":1}]}",
                "impact" => "{\"symbol\":\"GreetingService\",\"depth\":1,\"nodeCount\":1,\"edgeCount\":0,\"affected\":[{\"name\":\"GreetingService\",\"kind\":\"class\",\"filePath\":\"src/main/java/example/GreetingService.java\",\"startLine\":1}]}",
                _ => "indexed",
            };
            byte[] output = Encoding.UTF8.GetBytes(json);
            return Task.FromResult(new CodeGraphProcessResult(0, output, [], false));
        }
    }

    private sealed class RecordingWorkspaceLeaseProvider(AnalysisScope expected) : IJdtLsWorkspaceLeaseProvider
    {
        public Task<IJdtLsWorkspaceLease> AcquireAsync(AnalysisScope scope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IJdtLsWorkspaceLease>(new RecordingWorkspaceLease(new JdtLsWorkspaceBinding(
                expected.RepositoryId, expected.ScanRunId, expected.ProfiledRevision,
                expected.WorkingCopyBindingToken, "C:/workspace/repository")));
        }
    }

    private sealed class RecordingWorkspaceLease(JdtLsWorkspaceBinding binding) : IJdtLsWorkspaceLease
    {
        public JdtLsWorkspaceBinding Binding { get; } = binding;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingJavaProcessLauncher : IJdtLsProcessLauncher
    {
        public Task<IJdtLsProcess> LaunchAsync(JdtLsProcessStartInfo startInfo, CancellationToken cancellationToken) =>
            Task.FromResult<IJdtLsProcess>(new RecordingJavaProcess());
    }

    private sealed class RecordingJavaProcess : IJdtLsProcess
    {
        public Stream StandardInput { get; } = new MemoryStream();
        public Stream StandardOutput { get; } = new MemoryStream();
        public Task<string> ReadBoundedStandardErrorAsync(int byteLimit, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
        public Task<int> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task KillTreeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingJavaProtocolFactory(RecordingJavaProtocol protocol) : IJdtLsProtocolClientFactory
    {
        public IJdtLsProtocolClient Create(IJdtLsProcess process, int responseByteLimit) => protocol;
    }

    private sealed class RecordingJavaProtocol : IJdtLsProtocolClient
    {
        public List<AnalysisScope> Scopes { get; } = [];
        public Task InitializeAsync(JdtLsWorkspaceBinding binding, TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task InitializedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<JavaNavigationResult> NavigateAsync(string method, AnalysisScope scope, JavaNavigationRequest request, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Scopes.Add(scope);
            return Task.FromResult(new JavaNavigationResult(AnalysisOperationStatus.Succeeded,
                [new AnalysisSymbolEvidence("GreetingService", AnalysisSymbolKind.Type, request.RelativePath, new AnalysisLineRange(1, 1))], null));
        }
        public Task ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestSnapshotLease(AnalysisScope expected, string root) : ICommittedRepositorySnapshotLease
    {
        public Guid RepositoryId => expected.RepositoryId;
        public Guid ScanRunId => expected.ScanRunId;
        public string ProfiledRevision => expected.ProfiledRevision;
        public string RootPath => root;
        public int VerifyCalls { get; private set; }
        public Guid VerifiedScanRunId { get; private set; }
        public string VerifiedBindingToken { get; private set; } = string.Empty;
        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            VerifyCalls++;
            VerifiedScanRunId = ScanRunId;
            VerifiedBindingToken = BindingToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

using Vulgata.Tests.TestInfrastructure;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// T043 adversarial security-negative coverage for the closed analysis ports.
/// The RED assertions use the accepted typed ports and their approved process,
/// snapshot, workspace, and protocol seams; no shell, network, private graph
/// store, or raw LSP surface is introduced by this test.
/// </summary>
public sealed class AnalysisCapabilitySecurityTests
{
    private static readonly Guid RepositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000043");
    private static readonly Guid ScanRunId =
        Guid.Parse("12000000-0000-4000-8000-000000000143");
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string BindingToken = "binding-1200000000000000000000000000000000000043";
    private const string RuntimeHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CodeGraphPort_RejectsCliFlagLikeAtomsWithoutInvokingProcess()
    {
        AnalysisScope scope = CreateScope(RuntimeHash);
        RecordingCodeGraphProcess process = new();
        ICodeGraphAnalysisPort port = CreateCodeGraph(scope, process);
        AnalysisOperationBounds bounds = new(8, 4_096);

        SymbolSearchResult search = await port.SearchSymbolsAsync(
            scope,
            new AnalysisSymbolQuery("--json", AnalysisSymbolKind.Type, bounds),
            CancellationToken.None);
        RelationshipResult callers = await port.GetCallersAsync(
            scope,
            new AnalysisSymbolRequest("--path", bounds),
            CancellationToken.None);
        RelatedFilesResult impact = await port.GetRelatedFilesAsync(
            scope,
            new AnalysisSymbolRequest("--limit", bounds),
            CancellationToken.None);

        AssertRejected(search);
        AssertRejected(callers);
        AssertRejected(impact);
        Assert.Empty(process.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CodeGraphPort_RejectsRuntimeIdentityDriftBeforeProcessEvidence()
    {
        AnalysisScope expected = CreateScope(RuntimeHash);
        AnalysisScope drifted = CreateScope(new string('b', 64));
        RecordingCodeGraphProcess process = new();
        ICodeGraphAnalysisPort port = CreateCodeGraph(expected, process);

        SymbolSearchResult result = await port.SearchSymbolsAsync(
            drifted,
            new AnalysisSymbolQuery(
                "GreetingService",
                AnalysisSymbolKind.Type,
                new AnalysisOperationBounds(8, 4_096)),
            CancellationToken.None);

        AssertRejected(result);
        Assert.Empty(process.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CodeGraphPort_RejectsCrossRepositoryAndStaleRevisionOrBinding()
    {
        AnalysisScope expected = CreateScope(RuntimeHash);
        RecordingCodeGraphProcess process = new();
        ICodeGraphAnalysisPort port = CreateCodeGraph(expected, process);
        AnalysisSymbolRequest request = new(
            "GreetingService",
            new AnalysisOperationBounds(8, 4_096));

        AnalysisScope[] rejectedScopes =
        [
            new(
                Guid.Parse("12000000-0000-4000-8000-000000009999"),
                ScanRunId,
                Revision,
                BindingToken,
                RuntimeHash,
                CreateDeadline()),
            CreateScope(
                "fedcba9876543210fedcba9876543210fedcba98",
                BindingToken,
                RuntimeHash),
            CreateScope(Revision, "binding-1200000000000000000000000000000000000999", RuntimeHash),
        ];

        foreach (AnalysisScope scope in rejectedScopes)
        {
            RelationshipResult result = await port.GetCalleesAsync(
                scope,
                request,
                CancellationToken.None);
            AssertRejected(result);
        }

        Assert.Empty(process.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void TypedRequests_RejectUnboundedRangesPathsAndPayloadsBeforeAnyAdapter()
    {
        Assert.Throws<ArgumentException>(() => new AnalysisSymbolQuery(
            "raw\u0000command",
            AnalysisSymbolKind.Type,
            new AnalysisOperationBounds(8, 4_096)));
        Assert.Throws<ArgumentException>(() => new RevisionBoundSourceRequest(
            "../private-store",
            new AnalysisLineRange(1, 1),
            RuntimeHash,
            4_096));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnalysisOperationBounds(1_001, 4_096));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnalysisOperationBounds(8, 1_048_577));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnalysisLineRange(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnalysisLineRange(9, 8));
        Assert.Throws<ArgumentException>(() => new JavaNavigationRequest(
            "C:/outside.java",
            position: null,
            new AnalysisOperationBounds(8, 4_096)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task JavaSession_RejectsCrossRepositoryStaleRuntimeAndBindingScopesBeforeProtocol()
    {
        AnalysisScope expected = CreateScope(RuntimeHash);
        RecordingJavaProtocol protocol = new();
        JdtLsAnalysisSession session = new(
            expected,
            new JdtLsAdapterOptions(
                @"C:\Program Files\Java\latest\jdk-21\bin\java.exe",
                @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332",
                @"C:\workspaces",
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1),
                4_096),
            new NoopWorkspaceLease(expected),
            new NoopJavaProcess(),
            protocol);

        JavaNavigationRequest request = new(
            "src/main/java/example/analysis/GreetingService.java",
            new AnalysisSourcePosition(1, 1),
            new AnalysisOperationBounds(8, 4_096));
        AnalysisScope[] rejectedScopes =
        [
            new(
                Guid.Parse("12000000-0000-4000-8000-000000009999"),
                ScanRunId,
                Revision,
                BindingToken,
                RuntimeHash,
                CreateDeadline()),
            CreateScope("fedcba9876543210fedcba9876543210fedcba98", BindingToken, RuntimeHash),
            CreateScope(Revision, BindingToken, new string('b', 64)),
            CreateScope(Revision, "binding-1200000000000000000000000000000000000999", RuntimeHash),
        ];

        foreach (AnalysisScope scope in rejectedScopes)
        {
            JavaNavigationResult result = await session.GetDefinitionAsync(
                scope,
                request,
                CancellationToken.None);
            Assert.Equal(AnalysisOperationStatus.Failed, result.Status);
            Assert.Equal(AnalysisPortFailureReason.WorkspaceConflict, result.Failure?.Reason);
        }

        Assert.Empty(protocol.NavigationMethods);
        await session.DisposeAsync();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task JavaWorkspaceLeaseProvider_RejectsConcurrentRevisionWorkspaceCollision()
    {
        string workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story121-security-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        try
        {
            FileJdtLsWorkspaceLeaseProvider provider =
                new(workspaceRoot);
            AnalysisScope scope = CreateScope(RuntimeHash);
            await using IJdtLsWorkspaceLease first =
                await provider.AcquireAsync(scope, CancellationToken.None);

            Exception? collision = await Record.ExceptionAsync(async () =>
            {
                await using IJdtLsWorkspaceLease second =
                    await provider.AcquireAsync(scope, CancellationToken.None);
            });

            Assert.NotNull(collision);
            Assert.Contains("workspace", collision!.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(workspaceRoot, collision.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static ICodeGraphAnalysisPort CreateCodeGraph(
        AnalysisScope expected,
        RecordingCodeGraphProcess process)
    {
        CodeGraphAdapterOptions options = new(
            @"G:\packages\npm\prefix\codegraph.ps1",
            TimeSpan.FromSeconds(1),
            4_096,
            4_096);
        return new CodeGraphAnalysisAdapter(
            options,
            new FixedSnapshotResolver(expected),
            process);
    }

    private static AnalysisScope CreateScope(string runtimeHash) =>
        CreateScope(Revision, BindingToken, runtimeHash);

    private static AnalysisScope CreateScope(
        string revision,
        string bindingToken,
        string runtimeHash) =>
        new(
            RepositoryId,
            ScanRunId,
            revision,
            bindingToken,
            runtimeHash,
            CreateDeadline());

    private static AnalysisDeadline CreateDeadline() =>
        new(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds());

    private static void AssertRejected(SymbolSearchResult result)
    {
        Assert.Equal(AnalysisOperationStatus.Failed, result.Status);
        Assert.Equal(AnalysisPortFailureReason.Rejected, result.Failure?.Reason);
        Assert.Empty(result.Symbols);
    }

    private static void AssertRejected(RelationshipResult result)
    {
        Assert.Equal(AnalysisOperationStatus.Failed, result.Status);
        Assert.Equal(AnalysisPortFailureReason.Rejected, result.Failure?.Reason);
        Assert.Empty(result.Relationships);
    }

    private static void AssertRejected(RelatedFilesResult result)
    {
        Assert.Equal(AnalysisOperationStatus.Failed, result.Status);
        Assert.Equal(AnalysisPortFailureReason.Rejected, result.Failure?.Reason);
        Assert.Empty(result.RelativePaths);
    }

    private sealed class FixedSnapshotResolver(AnalysisScope expected)
        : ICodeGraphRepositorySnapshotResolver
    {
        public Task<CodeGraphRepositorySnapshot?> ResolveAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CodeGraphRepositorySnapshot?>(new CodeGraphRepositorySnapshot(
                expected.RepositoryId,
                expected.ScanRunId,
                expected.ProfiledRevision,
                expected.WorkingCopyBindingToken,
                Path.GetTempPath(),
                "fixture-index",
                runtimeIdentityHash: expected.RuntimeIdentityHash));
        }
    }

    private sealed class RecordingCodeGraphProcess : ICodeGraphProcessRunner
    {
        public List<CodeGraphProcessRequest> Requests { get; } = [];

        public Task<CodeGraphProcessResult> RunAsync(
            CodeGraphProcessRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new CodeGraphProcessResult(
                0,
                Encoding.UTF8.GetBytes("[]"),
                [],
                TimedOut: false));
        }
    }

    private sealed class NoopWorkspaceLease(AnalysisScope scope) : IJdtLsWorkspaceLease
    {
        public JdtLsWorkspaceBinding Binding { get; } = new(
            scope.RepositoryId,
            scope.ScanRunId,
            scope.ProfiledRevision,
            scope.WorkingCopyBindingToken,
            @"C:\workspaces\fixture");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoopJavaProcess : IJdtLsProcess
    {
        public Stream StandardInput { get; } = new MemoryStream();
        public Stream StandardOutput { get; } = new MemoryStream();
        public Task<string> ReadBoundedStandardErrorAsync(
            int byteLimit,
            CancellationToken cancellationToken) => Task.FromResult(string.Empty);
        public Task<int> WaitForExitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken) => Task.FromResult(0);
        public Task KillTreeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingJavaProtocol : IJdtLsProtocolClient
    {
        public List<string> NavigationMethods { get; } = [];

        public Task InitializeAsync(
            JdtLsWorkspaceBinding binding,
            TimeSpan timeout,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task InitializedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<JavaNavigationResult> NavigateAsync(
            string method,
            AnalysisScope scope,
            JavaNavigationRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            NavigationMethods.Add(method);
            return Task.FromResult(new JavaNavigationResult(
                AnalysisOperationStatus.Succeeded,
                [],
                failure: null));
        }
        public Task ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

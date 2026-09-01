using System.Reflection;
using System.Text;
using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Analysis.CodeGraph;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T022 RED boundary for the Story 12.2 revision-bound CodeGraph proof.
///
/// These tests deliberately discover the next public seam by reflection.  That
/// keeps the RED honest (an absent production seam is an assertion failure,
/// rather than a test-project compilation failure) while preventing the test
/// from inventing a second analysis contract.
/// </summary>
public sealed class PreparedCodeGraphReadinessTests
{
    private const string _preparedAdapterName =
        "Vulgata.Infrastructure.Analysis.CodeGraph.PreparedCodeGraphAnalysisAdapter";

    private static readonly string[] _requiredCompletenessProperties =
    [
        "Initialized",
        "IndexState",
        "PendingAdded",
        "PendingModified",
        "PendingRemoved",
        "WorktreeMismatch",
        "ReindexRecommended",
        "PendingReferences",
        "RepositoryId",
        "ScanRunId",
        "ProfiledRevision",
        "SnapshotIdentity",
        "RuntimeIdentityHash",
        "IndexIdentity",
        "ProjectPath",
        "IndexPath",
        "RepositoryBindingMatches",
        "ScanRunBindingMatches",
        "ProfiledRevisionBindingMatches",
        "SnapshotBindingMatches",
        "RuntimeBindingMatches",
        "IndexBindingMatches",
        "TrustedProjectPath",
        "TrustedIndexPath",
        "CodeGraphProofIdentityHash"
    ];

    [Fact]
    public void CodeGraphReadiness_PublicSeam_ExposesTypedRevisionBoundCompletenessProof()
    {
        Type adapterType = RequireType(
            typeof(CodeGraphAnalysisAdapter).Assembly,
            _preparedAdapterName);

        Assert.True(
            typeof(ICodeGraphAnalysisPort).IsAssignableFrom(adapterType),
            "Prepared CodeGraph readiness must remain behind the existing closed Core port.");

        Type statusType = typeof(CodeGraphStatusResult);
        foreach (string propertyName in _requiredCompletenessProperties)
        {
            Assert.NotNull(
                statusType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));
        }

        Assert.Contains(
            adapterType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(method => method.Name),
            name => name is "IndexAsync" or "RebuildAsync");
    }

    [Fact]
    public void CodeGraphReadiness_ApprovedAdapter_UsesPinnedIdentityAndNoLegacyContextOrRawToolSurface()
    {
        Type adapterType = RequireType(
            typeof(CodeGraphAnalysisAdapter).Assembly,
            _preparedAdapterName);

        Assert.DoesNotContain(
            adapterType.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => new[] { "Explore", "Node", "Files", "Daemon", "Unlock", "Install", "Upgrade" }
                .Contains(method.Name, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void CodeGraphReadiness_DurationIsAuditOnly_AndHardFailuresHaveClosedOutcomeSurface()
    {
        Type adapterType = RequireType(
            typeof(CodeGraphAnalysisAdapter).Assembly,
            _preparedAdapterName);

        Type proofType = FindTypeContaining(
            typeof(CodeGraphAnalysisAdapter).Assembly,
            "CodeGraphProof");

        PropertyInfo hash = proofType.GetProperty("CodeGraphProofIdentityHash") ??
            proofType.GetProperty("IdentityHash") ??
            throw new InvalidOperationException("CodeGraph proof identity hash is not exposed.");
        Assert.Equal(typeof(string), hash.PropertyType);

        PropertyInfo? duration = proofType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(property => property.Name.Contains("Duration", StringComparison.Ordinal));
        Assert.NotNull(duration);
        Assert.Contains(
            proofType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name),
            name => name.Contains("Failure", StringComparison.Ordinal) ||
                name.Contains("Complete", StringComparison.Ordinal));

        Assert.NotNull(adapterType);
    }

    [Fact]
    public async Task CodeGraphReadiness_ConcreteAdapterProducesCompletePreparedProofAfterReadiness()
    {
        string repositoryRoot = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story122-codegraph-red-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryRoot);

        try
        {
            AnalysisScope scope = new(
                Guid.Parse("12000000-0000-4000-8000-000000000123"),
                Guid.Parse("22000000-0000-4000-8000-000000000123"),
                "0123456789abcdef0123456789abcdef01234567",
                "story122-codegraph",
                new string('a', 64),
                new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
            const string indexIdentity = "story122-index";
            const string snapshotIdentity = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            const string snapshotRootBindingHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
            var snapshot = new CodeGraphRepositorySnapshot(
                scope.RepositoryId,
                scope.ScanRunId,
                scope.ProfiledRevision,
                scope.WorkingCopyBindingToken,
                repositoryRoot,
                indexIdentity,
                scope.RuntimeIdentityHash);
            var runner = new QueueCodeGraphProcessRunner();
            runner.Enqueue(new CodeGraphProcessResult(
                0,
                Encoding.UTF8.GetBytes("Indexed 1 files, 1 nodes, 0 edges\r\n"),
                [],
                TimedOut: false));
            runner.Enqueue(StatusResult(repositoryRoot));
            runner.Enqueue(new CodeGraphProcessResult(0, [], [], TimedOut: false));
            runner.Enqueue(StatusResult(repositoryRoot));
            runner.Enqueue(StatusResult(repositoryRoot));

            object adapter = new CodeGraphAnalysisAdapter(
                new CodeGraphAdapterOptions(
                    @"G:\\packages\\npm\\prefix\\codegraph.ps1",
                    TimeSpan.FromSeconds(5),
                    StandardOutputByteLimit: 65_536,
                    StandardErrorByteLimit: 4_096),
                new FixedCodeGraphSnapshotResolver(snapshot),
                runner);
            IPreparedCodeGraphAnalysisPort prepared = Assert.IsAssignableFrom<IPreparedCodeGraphAnalysisPort>(adapter);

            AnalysisIndexResult initialized = await prepared.IndexAsync(scope, CancellationToken.None);
            AnalysisIndexResult indexed = await prepared.RebuildAsync(scope, CancellationToken.None);
            CodeGraphStatusResult status = await prepared.GetStatusAsync(scope, CancellationToken.None);
            PreparedCodeGraphProofResult result = await prepared.BuildCompletenessProofAsync(
                new PreparedCodeGraphProofRequest(
                    scope,
                    snapshotIdentity,
                    snapshotRootBindingHash,
                    "repository-project",
                    "repository-index"),
                CancellationToken.None);

            Assert.Equal(AnalysisOperationStatus.Succeeded, initialized.Status);
            Assert.Equal(AnalysisOperationStatus.Succeeded, indexed.Status);
            Assert.Equal(AnalysisOperationStatus.Succeeded, status.Status);
            Assert.Equal(AnalysisOperationStatus.Succeeded, result.Status);
            Assert.Null(result.Failure);
            CodeGraphCompletenessProof proof = Assert.IsType<CodeGraphCompletenessProof>(result.Proof);
            Assert.True(proof.Initialized);
            Assert.Equal(CodeGraphIndexState.Complete, proof.IndexState);
            Assert.Equal(0, proof.PendingAdded);
            Assert.Equal(0, proof.PendingModified);
            Assert.Equal(0, proof.PendingRemoved);
            Assert.False(proof.WorktreeMismatch);
            Assert.False(proof.ReindexRecommended);
            Assert.Equal(0, proof.PendingReferences);
            Assert.Equal(scope.RepositoryId, proof.RepositoryId);
            Assert.Equal(scope.ScanRunId, proof.ScanRunId);
            Assert.Equal(scope.ProfiledRevision, proof.ProfiledRevision);
            Assert.Equal(snapshotIdentity, proof.SnapshotIdentity);
            Assert.Equal(indexIdentity, proof.IndexIdentity);
            Assert.True(proof.RepositoryBindingMatched);
            Assert.True(proof.ScanRunBindingMatched);
            Assert.True(proof.RevisionBindingMatched);
            Assert.True(proof.RuntimeBindingMatched);
            Assert.True(proof.SnapshotBindingMatched);
            Assert.True(proof.ProjectPathTrusted);
            Assert.True(proof.IndexPathTrusted);
            Assert.NotNull(proof.IdentityHash);
        }
        finally
        {
            Directory.Delete(repositoryRoot, recursive: true);
        }
    }

    private static CodeGraphProcessResult StatusResult(string repositoryRoot)
    {
        string status = JsonSerializer.Serialize(new
        {
            initialized = true,
            version = "1.5.0",
            projectPath = repositoryRoot,
            indexPath = Path.Combine(repositoryRoot, ".codegraph"),
            lastIndexed = "2026-08-20T00:00:00.000Z",
            fileCount = 1,
            nodeCount = 1,
            edgeCount = 0,
            dbSizeBytes = 1_024,
            journalMode = "wal",
            backend = "node-sqlite",
            nodesByKind = new Dictionary<string, int> { ["class"] = 1 },
            languages = new[] { "java", "xml" },
            pendingChanges = new { added = 0, modified = 0, removed = 0 },
            worktreeMismatch = (bool?)null,
            index = new
            {
                builtWithVersion = "1.5.0",
                builtWithExtractionVersion = 24,
                currentExtractionVersion = 24,
                reindexRecommended = false,
                state = "complete",
                pendingRefs = 0,
            },
        });
        return new CodeGraphProcessResult(
            0,
            Encoding.UTF8.GetBytes(status),
            [],
            TimedOut: false);
    }

    private sealed class FixedCodeGraphSnapshotResolver(CodeGraphRepositorySnapshot snapshot)
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

    private sealed class QueueCodeGraphProcessRunner : ICodeGraphProcessRunner
    {
        private readonly Queue<CodeGraphProcessResult> _results = [];

        public void Enqueue(CodeGraphProcessResult result) => _results.Enqueue(result);

        public Task<CodeGraphProcessResult> RunAsync(
            CodeGraphProcessRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_results.Dequeue());
        }
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
        Assert.NotNull(type);
        return type!;
    }

    private static Type FindTypeContaining(Assembly assembly, string name)
    {
        Type? type = assembly.GetTypes()
            .FirstOrDefault(candidate => candidate.Name.Contains(name, StringComparison.Ordinal));
        Assert.NotNull(type);
        return type!;
    }
}

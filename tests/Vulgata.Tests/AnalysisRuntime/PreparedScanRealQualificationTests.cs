using System.Diagnostics;
using System.Security.Cryptography;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Git;

namespace Vulgata.Tests.AnalysisRuntime;

/// <summary>
/// T046 qualification against the retained Windows-host Git, CodeGraph, Java 21,
/// and JDT-LS authorities. These tests do not download or substitute tools.
/// </summary>
public sealed class PreparedScanRealQualificationTests
{
    private const string CodeGraphCommandPath = @"G:\packages\npm\prefix\codegraph.ps1";
    private const string CodeGraphHash =
        "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03";
    private const string NodeHash =
        "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3";
    private const string NpmShimHash =
        "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877";
    private const string JavaHome = @"C:\Program Files\Java\latest\jdk-21";
    private const string JdtLsRoot =
        @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332";
    private const string JdtLsArchive =
        @"C:\Users\13728\Downloads\jdt-language-server-1.61.0-202608141332.tar.gz";
    private const string JdtLsArchiveHash =
        "bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f";
    private const string ProfiledRevision =
        "0123456789abcdef0123456789abcdef01234567";
    private const string RuntimeIdentityHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ExactGit_PrivateFullRefAcquisition_BindsCommitAndCleansItsLease()
    {
        string root = CreateTemporaryRoot("git");
        string origin = Path.Combine(root, "origin");
        string snapshots = Path.Combine(root, "snapshots");
        Directory.CreateDirectory(origin);

        try
        {
            await RunProcessAsync("git", ["init", "--quiet", "--initial-branch=main"], origin);
            await File.WriteAllTextAsync(Path.Combine(origin, "neutral.txt"), "neutral\n");
            await RunProcessAsync("git", ["add", "--", "neutral.txt"], origin);
            await RunProcessAsync(
                "git",
                ["-c", "user.name=Vulgata Qualification", "-c", "user.email=qualification@invalid", "commit", "--quiet", "-m", "neutral"],
                origin);
            string commit = (await RunProcessAsync(
                "git", ["rev-parse", "refs/heads/main"], origin)).Trim();

            var service = new ExactGitRevisionSnapshotService(new GitWorkingCopyOptions(snapshots));
            await using ICommittedRepositorySnapshotLease lease = await service.AcquireAsync(
                CreateRepository(origin),
                Guid.NewGuid(),
                "refs/heads/main",
                commit,
                CancellationToken.None);

            Assert.Equal(commit, lease.ProfiledRevision);
            Assert.True(File.Exists(Path.Combine(lease.RootPath, "neutral.txt")));
            Assert.False(Directory.Exists(Path.Combine(lease.RootPath, ".git")));
            await lease.VerifyIdentityAsync(CancellationToken.None);
            Assert.StartsWith(Path.GetFullPath(snapshots), Path.GetFullPath(lease.RootPath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task PinnedCodeGraph_RealCompletePreparedProof_FinishesUnderThirtySeconds()
    {
        await AssertPinnedCodeGraphBundleAsync();
        string repositoryRoot = CopyNeutralRepository("codegraph");
        try
        {
            AnalysisScope scope = CreateScope();
            string snapshotIdentity = new('b', 64);
            string indexIdentity = "t046-neutral-index";
            var snapshot = new CodeGraphRepositorySnapshot(
                scope.RepositoryId,
                scope.ScanRunId,
                scope.ProfiledRevision,
                scope.WorkingCopyBindingToken,
                repositoryRoot: repositoryRoot,
                expectedIndexIdentity: indexIdentity,
                runtimeIdentityHash: scope.RuntimeIdentityHash);
            var adapter = new CodeGraphAnalysisAdapter(
                new CodeGraphAdapterOptions(
                    CodeGraphCommandPath,
                    TimeSpan.FromSeconds(30),
                    StandardOutputByteLimit: 256 * 1024,
                    StandardErrorByteLimit: 16 * 1024),
                new SnapshotResolver(snapshot),
                new CodeGraphProcessRunner());

            Stopwatch timer = Stopwatch.StartNew();
            AnalysisIndexResult initialized = await adapter.IndexAsync(scope, CancellationToken.None);
            AnalysisIndexResult indexed = await adapter.RebuildAsync(scope, CancellationToken.None);
            CodeGraphStatusResult status = await adapter.GetStatusAsync(scope, CancellationToken.None);
            timer.Stop();

            Assert.Equal(AnalysisOperationStatus.Succeeded, initialized.Status);
            Assert.Equal(AnalysisOperationStatus.Succeeded, indexed.Status);
            Assert.Equal(AnalysisOperationStatus.Succeeded, status.Status);
            Assert.True(status.Ready);
            Assert.False(status.Stale);
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(30), $"CodeGraph took {timer.Elapsed}.");

            IPreparedCodeGraphAnalysisPort prepared = Assert.IsAssignableFrom<IPreparedCodeGraphAnalysisPort>(adapter);
            PreparedCodeGraphProofResult result = await prepared.BuildCompletenessProofAsync(
                new PreparedCodeGraphProofRequest(
                    scope,
                    snapshotIdentity,
                    new string('c', 64),
                    "repository-project",
                    "repository-index"),
                CancellationToken.None);

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
            Assert.True(proof.RepositoryBindingMatched);
            Assert.True(proof.ScanRunBindingMatched);
            Assert.True(proof.RevisionBindingMatched);
            Assert.True(proof.RuntimeBindingMatched);
            Assert.True(proof.SnapshotBindingMatched);
            Assert.True(proof.ProjectPathTrusted);
            Assert.True(proof.IndexPathTrusted);
            Assert.Equal(indexIdentity, proof.IndexIdentity);
            Assert.Equal(snapshotIdentity, proof.SnapshotIdentity);
            Assert.InRange(proof.ObservedDurationMilliseconds, 0, 29_999);
        }
        finally
        {
            DeleteDirectory(repositoryRoot);
        }
    }

    [Fact]
    public async Task PinnedJdtLs_RealReadinessFile_FinishesUnderSixtySeconds()
    {
        Assert.True(File.Exists(JdtLsArchive));
        Assert.Equal(JdtLsArchiveHash, await HashAsync(JdtLsArchive));
        Assert.True(Directory.Exists(JdtLsRoot));
        string java = Path.Combine(JavaHome, "bin", "java.exe");
        Assert.True(File.Exists(java));

        string workspaceRoot = CreateTemporaryRoot("jdt");
        AnalysisScope scope = CreateScope();
        string revisionRoot = Path.Combine(
            workspaceRoot,
            scope.RepositoryId.ToString("N"),
            scope.ProfiledRevision);
        Directory.CreateDirectory(revisionRoot);
        CopyDirectory(FindNeutralRepository(), revisionRoot);
        try
        {
            var factory = new JdtLsAnalysisSessionFactory(
                new JdtLsAdapterOptions(
                    java,
                    JdtLsRoot,
                    workspaceRoot,
                    InitializationTimeout: TimeSpan.FromSeconds(30),
                    RequestTimeout: TimeSpan.FromSeconds(30),
                    ShutdownTimeout: TimeSpan.FromSeconds(10),
                    ResponseByteLimit: 256 * 1024),
                JdtLsArchive,
                JdtLsArchiveHash);
            string readinessFile = "src/main/java/example/analysis/GreetingFormatter.java";
            Stopwatch timer = Stopwatch.StartNew();
            await using IJavaAnalysisSession session = await factory.OpenAsync(scope, CancellationToken.None);
            JavaNavigationResult symbols = await session.GetDocumentSymbolsAsync(
                scope,
                new JavaNavigationRequest(
                    readinessFile,
                    null,
                    new AnalysisOperationBounds(16, 64 * 1024)),
                CancellationToken.None);
            timer.Stop();

            Assert.Equal(AnalysisOperationStatus.Succeeded, symbols.Status);
            Assert.Null(symbols.Failure);
            Assert.NotEmpty(symbols.Evidence);
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(60), $"JDT-LS readiness took {timer.Elapsed}.");
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task PreparedJavaReadiness_XmlOnly_IsExplicitNotApplicable()
    {
        AnalysisScope scope = CreateScope();
        IPreparedJavaReadinessPort readiness = new PreparedJavaReadinessService();
        PreparedJavaReadinessResult result = await readiness.ProveReadinessAsync(
            new PreparedJavaReadinessRequest(
                scope,
                new string('b', 64),
                new string('c', 64),
                EligibleJavaRelativePaths: []),
            CancellationToken.None);

        Assert.Equal(AnalysisOperationStatus.Succeeded, result.Status);
        Assert.Null(result.Failure);
        JavaReadinessProof proof = Assert.IsType<JavaReadinessProof>(result.Proof);
        Assert.Equal(JavaReadinessOutcome.NotApplicable, proof.Outcome);
        Assert.Null(proof.ReadinessFile);
        Assert.False(proof.WorkspaceExclusive);
        Assert.False(proof.InitializeCompleted);
        Assert.False(proof.InitializedNotificationSent);
        Assert.Equal(JavaImportState.NotApplicable, proof.ImportState);
        Assert.Equal(0, proof.DocumentSymbolCount);
        Assert.NotNull(proof.IdentityHash);
    }

    [Fact]
    public async Task HardReadinessFailureRejects_OnlyPostReadinessExpansionFallsBack()
    {
        AnalysisScope scope = CreateScope();
        IPreparedJavaReadinessPort readiness = new PreparedJavaReadinessService();
        PreparedJavaReadinessResult hardFailure = await readiness.ProveReadinessAsync(
            new PreparedJavaReadinessRequest(
                scope,
                new string('b', 64),
                new string('c', 64),
                ["src/main/java/example/analysis/GreetingService.java"]),
            CancellationToken.None);
        Assert.Equal(AnalysisOperationStatus.Failed, hardFailure.Status);
        Assert.Null(hardFailure.Proof);
        Assert.NotNull(hardFailure.Failure);

        PreparedFileCodeUnitDraft seed = CreateSeed(scope);
        var expansion = new PreparedRelatedFileExpansion(new FailingPreparedCodeGraphPort());
        PreparedRelatedFileExpansionResult fallback = await expansion.ExpandAsync(
            new PreparedRelatedFilesRequest(
                scope,
                [seed.RelativePath],
                [seed.RelativePath],
                [PreparedRelationshipKind.Calls, PreparedRelationshipKind.Imports, PreparedRelationshipKind.Inherits],
                PreparedRelatedFileExpansion.MaximumHops,
                PreparedRelatedFileExpansion.MaximumFiles,
                DateTimeOffset.UtcNow.AddSeconds(5).ToUnixTimeMilliseconds()),
            [seed],
            CancellationToken.None);

        Assert.Equal(PreparedRelatedFileExpansionOutcome.WarningFallback, fallback.Outcome);
        Assert.Equal(PreparedRelatedFileExpansion.WarningCode, fallback.WarningCode);
        PreparedFileCodeUnitDraft retained = Assert.Single(fallback.Candidates);
        Assert.Equal(seed.DirectBusinessScore, retained.FinalBusinessScore);
        Assert.Equal(seed.RelativePath, retained.RelativePath);
    }

    private static AnalysisScope CreateScope() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000046"),
        Guid.NewGuid(),
        ProfiledRevision,
        "t046-" + Guid.NewGuid().ToString("N"),
        RuntimeIdentityHash,
        new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds()));

    private static PreparedFileCodeUnitDraft CreateSeed(AnalysisScope scope) => new(
        Guid.NewGuid(),
        scope.RepositoryId,
        scope.ScanRunId,
        scope.ProfiledRevision,
        "src/main/java/example/analysis/GreetingService.java",
        new string('d', 64),
        0,
        128,
        1,
        8,
        1,
        1,
        new string('e', 64),
        new string('f', 64),
        800,
        800,
        PreparedSelectionReason.DirectBusinessEvidence,
        0,
        0);

    private static Repository CreateRepository(string origin)
    {
        return Repository.CreateStandalone(
            "Neutral",
            origin,
            description: null,
            context: null,
            DateTimeOffset.UtcNow);
    }

    private static async Task AssertPinnedCodeGraphBundleAsync()
    {
        Assert.True(File.Exists(CodeGraphCommandPath));
        string packageRoot = Path.Combine(
            Path.GetDirectoryName(CodeGraphCommandPath)!,
            "node_modules",
            "@colbymchenry",
            "codegraph");
        Assert.Equal(CodeGraphHash, await HashAsync(Path.Combine(
            packageRoot, "node_modules", "@colbymchenry", "codegraph-win32-x64", "lib", "dist", "bin", "codegraph.js")));
        Assert.Equal(NodeHash, await HashAsync(Path.Combine(
            packageRoot, "node_modules", "@colbymchenry", "codegraph-win32-x64", "node.exe")));
        Assert.Equal(NpmShimHash, await HashAsync(Path.Combine(packageRoot, "npm-shim.js")));
    }

    private static string CopyNeutralRepository(string suffix)
    {
        string destination = CreateTemporaryRoot(suffix);
        CopyDirectory(FindNeutralRepository(), destination);
        return destination;
    }

    private static string FindNeutralRepository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("Repository root not found."),
            "tests", "Vulgata.Tests", "Fixtures", "AnalysisRuntime", "repository");
    }

    private static string CreateTemporaryRoot(string suffix)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"vulgata-story122-t046-{suffix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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
        for (int attempt = 0; attempt < 20 && Directory.Exists(path); attempt++)
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(
                path,
                "*",
                SearchOption.AllDirectories))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
                }
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException) when (attempt < 19)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < 19)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }

        Assert.False(Directory.Exists(path));
    }

    private static async Task<string> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{fileName} did not start.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"{fileName} exited {process.ExitCode}: {standardError}");
        return standardOutput;
    }

    private static async Task<string> HashAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    private sealed class SnapshotResolver(CodeGraphRepositorySnapshot snapshot)
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

    private sealed class FailingPreparedCodeGraphPort : IPreparedCodeGraphAnalysisPort
    {
        private static readonly AnalysisPortFailure _failure = new(
            AnalysisPortFailureReason.TimedOut,
            "related_files_timed_out",
            "related files timed out");

        public Task<PreparedRelatedFilesResult> GetPreparedRelatedFilesAsync(
            PreparedRelatedFilesRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PreparedRelatedFilesResult(AnalysisOperationStatus.Failed, [], _failure));

        public Task<PreparedCodeGraphProofResult> BuildCompletenessProofAsync(
            PreparedCodeGraphProofRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AnalysisIndexResult> IndexAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AnalysisIndexResult> RebuildAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CodeGraphStatusResult> GetStatusAsync(AnalysisScope scope, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SymbolSearchResult> SearchSymbolsAsync(
            AnalysisScope scope,
            AnalysisSymbolQuery query,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RelationshipResult> GetCallersAsync(
            AnalysisScope scope,
            AnalysisSymbolRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RelationshipResult> GetCalleesAsync(
            AnalysisScope scope,
            AnalysisSymbolRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RelatedFilesResult> GetRelatedFilesAsync(
            AnalysisScope scope,
            AnalysisSymbolRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

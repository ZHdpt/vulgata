using Vulgata.Tests.TestInfrastructure;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Analysis.CodeGraph;
using Vulgata.Infrastructure.Analysis.Java;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Git;
using Vulgata.Shared.Scans;
using Vulgata.Tests.PreparedScanning.TestDoubles;
using Vulgata.Web.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;
using SystemEntity = Vulgata.Core.Entities.System;

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
    private const string PreparedPromptIdentity = "prepared-scan-prompt/story122-v1";
    private const string PreparedDocumentSchemaIdentity = "code-logic/file-v2";
    private const string PreparedRuntimeIdentityJson = "{\"runtime\":\"prepared-real-test\"}";
    private static readonly string PreparedRuntimeIdentityHash = new('9', 64);

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
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

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
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

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
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

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
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

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ProductionPairBuilder_JavaRevisionPath_UsesPinnedJdtLsAndProducesRanges()
    {
        string fixtureRoot = CreateTemporaryRoot("t056-real-git");
        try
        {
            (string firstOrigin, string firstRevision, string firstRef) = await CreateLocalJavaGitRepositoryAsync(
                fixtureRoot,
                "one");
            (string secondOrigin, string secondRevision, string secondRef) = await CreateLocalJavaGitRepositoryAsync(
                fixtureRoot,
                "two");

            StubPreparedCodeGraphAnalysisPort codeGraph = new();
            codeGraph.ProofHandler = (request, _) => Task.FromResult(
                new PreparedCodeGraphProofResult(
                AnalysisOperationStatus.Succeeded,
                new CodeGraphCompletenessProof(
                    request.Scope.RepositoryId,
                    request.Scope.ScanRunId,
                    request.Scope.ProfiledRevision,
                    request.SnapshotIdentity,
                    request.SnapshotRootBindingHash,
                    request.Scope.RuntimeIdentityHash,
                    $"repository-{request.Scope.RepositoryId:N}",
                    "codegraph-completeness-proof/1.0",
                    true,
                    CodeGraphIndexState.Complete,
                    0,
                    0,
                    0,
                    false,
                    false,
                    0,
                    64,
                    $"repository-{request.Scope.RepositoryId:N}",
                    $"index-{request.Scope.RepositoryId:N}",
                    true,
                    true,
                    true,
                    true,
                    true,
                    true,
                    true,
                    new('b', 64),
                    0),
                    null));

            StubPreparedCommunicationCatalogSource catalog = new();
            catalog.Handler = _ => Task.FromResult(new PreparedCatalogLoadResult(
                new CommunicationPatternCatalog(
                    "communication-pattern-catalog/1.0",
                    "t056-java-composition",
                    "1",
                    new('c', 64),
                    []),
                null));

            using LoginLogoutTests.CustomWebApplicationFactory baseFactory = new();
            using WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(
                builder => builder.ConfigureTestServices(services =>
                {
                    string gitStorage = Path.Combine(fixtureRoot, "prepared-storage");
                    services.RemoveAll<GitWorkingCopyOptions>();
                    services.AddSingleton(new GitWorkingCopyOptions(gitStorage));
                    services.RemoveAll<ExactGitRevisionSnapshotService>();
                    services.AddScoped<ExactGitRevisionSnapshotService>();
                    services.RemoveAll<PreparedScanRevisionAcquirer>();
                    services.AddScoped<PreparedScanRevisionAcquirer>();
                    services.RemoveAll<IPreparedRevisionAcquirer>();
                    services.AddScoped<IPreparedRevisionAcquirer>(provider =>
                        provider.GetRequiredService<PreparedScanRevisionAcquirer>());
                    services.RemoveAll<IPreparedSnapshotSource>();
                    services.AddScoped<IPreparedSnapshotSource, PreparedSnapshotSource>();
                    services.RemoveAll<IAnalysisRuntimeHealthProbe>();
                    services.AddSingleton<IAnalysisRuntimeHealthProbe>(
                        new StubReadyAnalysisRuntimeHealthProbe());
                    services.RemoveAll<IPreparedCommunicationCatalogSource>();
                    services.AddSingleton<IPreparedCommunicationCatalogSource>(catalog);
                    services.RemoveAll<IPreparedCodeGraphAnalysisPort>();
                    services.AddSingleton<IPreparedCodeGraphAnalysisPort>(codeGraph);
                }));

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AnalysisRuntimeReadiness readiness = scope.ServiceProvider
                .GetRequiredService<AnalysisRuntimeReadiness>();
            readiness.Update(new AnalysisHealthProbeResult(
                AnalysisRuntimeHealthStatus.Ready,
                "t056-host-runtime",
                "pinned host Java 21 and JDT-LS runtime",
                1_800_000_000_000,
                [
                    new AnalysisHealthCheck("codegraph", AnalysisRuntimeHealthStatus.Ready),
                    new AnalysisHealthCheck("jdt-ls", AnalysisRuntimeHealthStatus.Ready),
                ],
                new('9', 64)));

            VulgataDbContext db = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
                (SystemEntity system, RepositoryEntity first, RepositoryEntity second,
                PreparedScanRepositoryRevisionBinding[] bindings) = await SeedJavaBindingsAsync(
                    db,
                    firstOrigin,
                    secondOrigin,
                    firstRevision,
                    secondRevision,
                    firstRef,
                    secondRef);
            IPreparedScanPairBuilder builder = scope.ServiceProvider
                .GetRequiredService<IPreparedScanPairBuilder>();
            PreparedScanInitiationTuple tuple = CreateInitiationTuple(
                db,
                system,
                [first, second],
                bindings);

            PreparedScanPairBuildResult result = await builder.BuildAsync(
                tuple,
                [first, second],
                bindings,
                CancellationToken.None);

            Assert.True(
                result.PairPackage is not null,
                $"Java-containing preparation must reach readiness/ranges; failure={result.Failure?.FailureCode}");
            Assert.Null(result.Failure);
        Assert.Equal(2, result.PairPackage.Repositories.Count);
        IJavaAnalysisSessionFactory sessionFactory = scope.ServiceProvider
            .GetRequiredService<IJavaAnalysisSessionFactory>();
        IPreparedJavaReadinessPort readinessPort = scope.ServiceProvider
            .GetRequiredService<IPreparedJavaReadinessPort>();
        IPreparedTopLevelSymbolSource rangeSource = scope.ServiceProvider
            .GetRequiredService<IPreparedTopLevelSymbolSource>();
        Assert.IsType<JdtLsAnalysisSessionFactory>(sessionFactory);
        Assert.IsType<PreparedJavaReadinessService>(readinessPort);
        Assert.IsType<PreparedJavaRangeSource>(rangeSource);
        Assert.All(result.PairPackage.Repositories, repository =>
            Assert.Equal(JavaReadinessOutcome.Ready, repository.JavaReadiness.Outcome));
        Assert.All(result.PairPackage.Repositories, repository =>
            Assert.True(repository.JavaReadiness.DocumentSymbolCount > 0));
        Assert.Contains(
            result.PairPackage.Repositories.SelectMany(repository => repository.FileCodeUnits),
            unit => unit.RelativePath.EndsWith(".java", StringComparison.Ordinal) &&
                unit.SegmentCount > 1);
        Assert.Equal("jdt-language-server-1.61.0-202608141332", result.PairPackage.Repositories[0]
            .JavaReadiness.JdtLsRelease);
        Assert.Equal(
            "bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f",
            result.PairPackage.Repositories[0].JavaReadiness.JdtLsDistributionArchiveHash);
        Assert.Equal("2.25.29", result.PairPackage.Repositories[0].JavaReadiness.StreamJsonRpcVersion);
            Assert.Equal(21, result.PairPackage.Repositories[0].JavaReadiness.JavaMajor);
        }
        finally
        {
            DeleteDirectory(fixtureRoot);
        }
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ProductionPairBuilder_ExactRevision_UsesFreshCodeGraphBeforeJdtReadiness()
    {
        string fixtureRoot = CreateTemporaryRoot("t058-real-codegraph");
        try
        {
            (string firstOrigin, string firstRevision, string firstRef) =
                await CreateLocalJavaGitRepositoryAsync(fixtureRoot, "one");
            (string secondOrigin, string secondRevision, string secondRef) =
                await CreateLocalJavaGitRepositoryAsync(fixtureRoot, "two");

            StubPreparedCommunicationCatalogSource catalog = new();
            catalog.Handler = _ => Task.FromResult(new PreparedCatalogLoadResult(
                new CommunicationPatternCatalog(
                    "communication-pattern-catalog/1.0",
                    "t058-real-codegraph",
                    "1",
                    new('c', 64),
                    []),
                null));

            using LoginLogoutTests.CustomWebApplicationFactory baseFactory = new();
            using WebApplicationFactory<Program> factory = baseFactory.WithWebHostBuilder(
                builder => builder.ConfigureTestServices(services =>
                {
                    string gitStorage = Path.Combine(fixtureRoot, "prepared-storage");
                    services.RemoveAll<GitWorkingCopyOptions>();
                    services.AddSingleton(new GitWorkingCopyOptions(gitStorage));
                    services.RemoveAll<ExactGitRevisionSnapshotService>();
                    services.AddScoped<ExactGitRevisionSnapshotService>();
                    services.RemoveAll<PreparedScanRevisionAcquirer>();
                    services.AddScoped<PreparedScanRevisionAcquirer>();
                    services.RemoveAll<IPreparedRevisionAcquirer>();
                    services.AddScoped<IPreparedRevisionAcquirer>(provider =>
                        provider.GetRequiredService<PreparedScanRevisionAcquirer>());
                    services.RemoveAll<IAnalysisRuntimeHealthProbe>();
                    services.AddSingleton<IAnalysisRuntimeHealthProbe>(
                        new StubReadyAnalysisRuntimeHealthProbe());
                    services.RemoveAll<IPreparedCommunicationCatalogSource>();
                    services.AddSingleton<IPreparedCommunicationCatalogSource>(catalog);
                    services.RemoveAll<AnalysisRuntimeOptions>();
                    services.AddSingleton(new AnalysisRuntimeOptions(
                        CodeGraphCommandPath,
                        JdtLsRoot,
                        JavaHome));
                }));

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AnalysisRuntimeReadiness readiness = scope.ServiceProvider
                .GetRequiredService<AnalysisRuntimeReadiness>();
            readiness.Update(new AnalysisHealthProbeResult(
                AnalysisRuntimeHealthStatus.Ready,
                "t058-host-runtime",
                "pinned host CodeGraph, Java 21 and JDT-LS runtime",
                1_800_000_000_000,
                [
                    new AnalysisHealthCheck("codegraph", AnalysisRuntimeHealthStatus.Ready),
                    new AnalysisHealthCheck("jdt-ls", AnalysisRuntimeHealthStatus.Ready),
                ],
                new('9', 64)));

            Assert.IsType<CodeGraphAnalysisAdapter>(
                scope.ServiceProvider.GetRequiredService<IPreparedCodeGraphAnalysisPort>());

            VulgataDbContext db = scope.ServiceProvider.GetRequiredService<VulgataDbContext>();
            (SystemEntity system, RepositoryEntity first, RepositoryEntity second,
                PreparedScanRepositoryRevisionBinding[] bindings) = await SeedJavaBindingsAsync(
                    db,
                    firstOrigin,
                    secondOrigin,
                    firstRevision,
                    secondRevision,
                    firstRef,
                    secondRef);
            IPreparedScanPairBuilder builder = scope.ServiceProvider
                .GetRequiredService<IPreparedScanPairBuilder>();
            PreparedScanInitiationTuple tuple = CreateInitiationTuple(
                db,
                system,
                [first, second],
                bindings);

            PreparedScanPairBuildResult result = await builder.BuildAsync(
                tuple,
                [first, second],
                bindings,
                CancellationToken.None);

            Assert.True(
                result.PairPackage is not null,
                $"Exact revision-bound CodeGraph/JDT preparation must complete; failure={result.Failure?.FailureCode}");
            Assert.Null(result.Failure);
            Assert.Equal(2, result.PairPackage.Repositories.Count);
            Assert.Equal(
                firstRevision,
                result.PairPackage.Repositories.Single(repository => repository.RepositoryId == first.Id)
                    .ProfiledRevision);
            Assert.Equal(
                secondRevision,
                result.PairPackage.Repositories.Single(repository => repository.RepositoryId == second.Id)
                    .ProfiledRevision);
            Assert.All(
                result.PairPackage.Repositories,
                repository =>
                {
                    Assert.True(repository.CodeGraphProof.Initialized);
                    Assert.Equal(CodeGraphIndexState.Complete, repository.CodeGraphProof.IndexState);
                    Assert.True(repository.CodeGraphProof.SnapshotBindingMatched);
                    Assert.True(repository.CodeGraphProof.RevisionBindingMatched);
                    Assert.Equal(JavaReadinessOutcome.Ready, repository.JavaReadiness.Outcome);
                });
        }
        finally
        {
            DeleteDirectory(fixtureRoot);
        }
    }

    private static IReadOnlyList<PreparedSnapshotFile> CreateJavaSnapshotFiles(
        PreparedRevisionSnapshot snapshot)
    {
        List<PreparedSnapshotFile> files = new(capacity: 60);
        for (int index = 0; index < 60; index++)
        {
            bool java = index % 2 == 0;
            string relativePath = java
                ? $"src/neutral/Entry-{index:00}.java"
                : $"src/neutral/config-{index:00}.xml";
            string source = java
                ? $"package neutral;\npublic final class Entry{index:00} {{\n    public static final int VALUE = {index};\n}}\n"
                : $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<configuration><entry name=\"neutral-{index:00}\" value=\"{snapshot.RepositoryId:N}\" /></configuration>\n";
            byte[] rawBytes = System.Text.Encoding.UTF8.GetBytes(source);
            string hash = Convert.ToHexStringLower(SHA256.HashData(rawBytes));
            files.Add(new PreparedSnapshotFile(
                relativePath,
                java ? "Java" : "XML",
                IsEligible: true,
                "EligibleProductionSource",
                hash,
                hash,
                rawBytes.LongLength,
                source.Count(character => character == '\n'),
                "utf-8",
                rawBytes));
        }

        return files;
    }

    private static async Task<(string Origin, string Revision, string Ref)> CreateLocalJavaGitRepositoryAsync(
        string root,
        string suffix)
    {
        string origin = Path.Combine(root, $"origin-{suffix}");
        Directory.CreateDirectory(origin);
        await RunProcessAsync("git", ["init", "--quiet", "--initial-branch=main"], origin);
        for (int index = 0; index < 60; index++)
        {
            bool java = index % 2 == 0;
            string relativePath = java
                ? $"src/main/java/example/analysis/Entry{index:00}.java"
                : $"src/main/resources/config-{index:00}.xml";
            string path = Path.Combine(origin, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string source = java
                ? CreateJavaSource(index)
                : $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<configuration><entry name=\"entry-{index:00}\" value=\"{suffix}\" /></configuration>\n";
            await File.WriteAllTextAsync(path, source, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        await File.WriteAllTextAsync(Path.Combine(origin, "neutral.txt"), "qualification\n");
        await RunProcessAsync("git", ["add", "--all"], origin);
        await RunProcessAsync(
            "git",
            ["-c", "user.name=Vulgata Qualification", "-c", "user.email=qualification@invalid", "commit", "--quiet", "-m", "t056"],
            origin);
        string revision = (await RunProcessAsync("git", ["rev-parse", "refs/heads/main"], origin)).Trim();
        string tag = $"t056-{suffix}";
        await RunProcessAsync("git", ["tag", tag, revision], origin);
        return (origin, revision, $"refs/tags/{tag}");
    }

    private static string CreateJavaSource(int index)
    {
        var source = new StringBuilder();
        source.AppendLine("package example.analysis;");
        source.AppendLine($"public final class Entry{index:00} {{");
        if (index == 0)
        {
            string payload = new('a', 40_000);
            source.AppendLine($"    private static final String PAYLOAD = \"{payload}\";");
        }
        else
        {
            source.AppendLine("    public int value() { return 1; }");
        }

        source.AppendLine("}");
        if (index == 0)
        {
            string payload = new('b', 40_000);
            source.AppendLine("final class SecondaryEntry00 {");
            source.AppendLine($"    private static final String PAYLOAD = \"{payload}\";");
            source.AppendLine("}");
        }

        return source.ToString();
    }

    private static async Task<(
        SystemEntity System,
        RepositoryEntity First,
        RepositoryEntity Second,
        PreparedScanRepositoryRevisionBinding[] Bindings)> SeedJavaBindingsAsync(
        VulgataDbContext db,
        string firstOrigin,
        string secondOrigin,
        string firstRevision,
        string secondRevision,
        string firstRef,
        string secondRef)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SystemEntity system = new(
            "T056 Java composition system",
            "private local Java readiness fixture",
            "prepared T056 context",
            now);
        RepositoryEntity first = system.AddRepository(
            "t056-java-one",
            firstOrigin,
            null,
            "Java repository one",
            now);
        RepositoryEntity second = system.AddRepository(
            "t056-java-two",
            secondOrigin,
            null,
            "Java repository two",
            now);
        ScanRun firstRun = ScanRun.Start(first.Id, 1_800, now);
        firstRun.BindProfiledRevision(firstRevision);
        ScanRun secondRun = ScanRun.Start(second.Id, 1_800, now);
        secondRun.BindProfiledRevision(secondRevision);
        ScanContextSnapshot firstSnapshot = ScanContextSnapshot.CapturePrepared(
            firstRun,
            "prepared T056 context",
            "prepared T056 system context",
            first.Context,
            now);
        ScanContextSnapshot secondSnapshot = ScanContextSnapshot.CapturePrepared(
            secondRun,
            "prepared T056 context",
            "prepared T056 system context",
            second.Context,
            now);
        PreScanResult firstPreScan = PreScanResult.CreateApproved(
            first.Id,
            firstRun.Id,
            firstRevision,
            now,
            new('f', 64),
            "预扫描已批准——继续进入顺序演示运行器",
            ConfirmedCommunicationKinds.None,
            0,
            0,
            0,
            false,
            []);
        PreScanResult secondPreScan = PreScanResult.CreateApproved(
            second.Id,
            secondRun.Id,
            secondRevision,
            now,
            new('1', 64),
            "预扫描已批准——继续进入顺序演示运行器",
            ConfirmedCommunicationKinds.None,
            0,
            0,
            0,
            false,
            []);
        ScanExecution firstExecution = ScanExecution.CreateNewPath(
            Guid.NewGuid(), first.Id, system.Id, firstRun.Id, firstRevision,
            PreparedPromptIdentity, PreparedDocumentSchemaIdentity,
            PreparedRuntimeIdentityJson, PreparedRuntimeIdentityHash, now);
        ScanExecution secondExecution = ScanExecution.CreateNewPath(
            Guid.NewGuid(), second.Id, system.Id, secondRun.Id, secondRevision,
            PreparedPromptIdentity, PreparedDocumentSchemaIdentity,
            PreparedRuntimeIdentityJson, PreparedRuntimeIdentityHash, now);
        TwoRepositoryScanCoordination coordination = TwoRepositoryScanCoordination.Create(
            firstExecution,
            secondExecution,
            now);

        db.Systems.Add(system);
        db.ScanRuns.AddRange(firstRun, secondRun);
        db.ScanContextSnapshots.AddRange(firstSnapshot, secondSnapshot);
        db.PreScanResults.AddRange(firstPreScan, secondPreScan);
        db.ScanExecutions.AddRange(firstExecution, secondExecution);
        db.TwoRepositoryScanCoordinations.Add(coordination);
        await db.SaveChangesAsync();

        return (
            system,
            first,
            second,
            [
                new() { RepositoryId = first.Id, GitRef = firstRef, ProfiledRevision = firstRevision },
                new() { RepositoryId = second.Id, GitRef = secondRef, ProfiledRevision = secondRevision },
            ]);
    }

    private static PreparedScanInitiationTuple CreateInitiationTuple(
        VulgataDbContext db,
        SystemEntity system,
        IReadOnlyList<RepositoryEntity> repositories,
        IReadOnlyList<PreparedScanRepositoryRevisionBinding> bindings)
    {
        TwoRepositoryScanCoordination coordination = db.TwoRepositoryScanCoordinations
            .Single(value => value.SystemId == system.Id);
        PreparedScanInitiationRepositoryTuple[] tupleRepositories = repositories
            .Select(repository =>
            {
                PreparedScanRepositoryRevisionBinding binding = bindings.Single(
                    value => value.RepositoryId == repository.Id);
                ScanExecution execution = db.ScanExecutions.Single(
                    value => value.RepositoryId == repository.Id);
                ScanRun run = db.ScanRuns.Single(value => value.Id == execution.ScanRunId);
                return new PreparedScanInitiationRepositoryTuple(
                    repository.Id,
                    binding.GitRef,
                    binding.ProfiledRevision,
                    run.Id,
                    execution.Id,
                    execution.Phase,
                    execution.PromptIdentity!,
                    execution.DocumentSchemaIdentity!,
                    execution.AnalysisRuntimeIdentityJson!,
                    execution.AnalysisRuntimeIdentityHash!,
                    execution.PreScanResultId);
            })
            .ToArray();
        return new(
            system.Id,
            new string('a', 64),
            coordination.CreatedAt,
            coordination.Id,
            coordination.StateVersion,
            coordination.Phase,
            PreparedScanArtifactPresence.None,
            tupleRepositories);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
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

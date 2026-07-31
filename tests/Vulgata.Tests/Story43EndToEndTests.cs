using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CommunicationDetection;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.FrameworkDetection;
using Vulgata.Infrastructure.Git;
using Vulgata.Web.Data;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Tests;

/// <summary>
/// Exercises the one prepared configuration shape intentionally left to the
/// conditional E2E gate: three committed application configuration syntaxes.
/// The broader persistence, authorization, replay, and zero-dispatch matrix is
/// covered by the focused current-boundary integration tests cited by the gate.
/// </summary>
public sealed class Story43EndToEndTests
{
    [Fact]
    public async Task ProductionComposition_TwoCommittedRepositories_PersistsIsolatedProfilesAndApprovedPreScanResultsAsync()
    {
        using var firstSource = new TemporaryGitRepository();
        string firstRevision = WritePreparedRepository(firstSource, "ledger-one");
        using var secondSource = new TemporaryGitRepository();
        string secondRevision = WritePreparedRepository(secondSource, "ledger-two");
        Assert.NotEqual(firstRevision, secondRevision);

        using var storage = new TemporaryDirectory();
        var codeGraph = new PreparedCodeGraphProvider();
        using var databaseFactory = new LoginLogoutTests.CustomWebApplicationFactory();
        using WebApplicationFactory<Program> factory = databaseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<GitWorkingCopyOptions>();
                services.AddSingleton(new GitWorkingCopyOptions(storage.Path));
                services.RemoveAll<ICodeGraphProvider>();
                services.AddSingleton<ICodeGraphProvider>(codeGraph);
            });
        });

        using IServiceScope scope = factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        Assert.Same(codeGraph, services.GetRequiredService<ICodeGraphProvider>());
        Assert.IsType<GitWorkingCopyService>(services.GetRequiredService<IGitWorkingCopyService>());
        Assert.IsType<CommittedRepositorySnapshotService>(services.GetRequiredService<ICommittedRepositorySnapshotService>());
        Assert.IsType<FrameworkEvidenceDetector>(services.GetRequiredService<IFrameworkEvidenceDetector>());
        Assert.IsType<CommunicationKindDetector>(services.GetRequiredService<IPreScanCommunicationDetector>());
        Assert.IsType<ScanRunRepository>(services.GetRequiredService<IScanRunRepository>());
        IRepositoryProfilingCoordinator coordinator = services.GetRequiredService<IRepositoryProfilingCoordinator>();
        Assert.IsType<RepositoryProfilingCoordinator>(coordinator);

        VulgataDbContext db = services.GetRequiredService<VulgataDbContext>();
        Assert.Empty(await db.Repositories.AsNoTracking().ToListAsync());
        Assert.Empty(await db.ScanRuns.AsNoTracking().ToListAsync());
        var firstRepository = RepositoryEntity.CreateStandalone(
            "production-composition-one",
            firstSource.Path,
            null,
            null,
            DateTimeOffset.UtcNow);
        var secondRepository = RepositoryEntity.CreateStandalone(
            "production-composition-two",
            secondSource.Path,
            null,
            null,
            DateTimeOffset.UtcNow);
        db.Repositories.AddRange(firstRepository, secondRepository);
        await db.SaveChangesAsync();

        var user = new TestUserContext();
        RepositoryProfilingResult first = await coordinator.StartStandaloneRepositoryAsync(
            firstRepository.Id,
            user);
        RepositoryProfilingResult second = await coordinator.StartStandaloneRepositoryAsync(
            secondRepository.Id,
            user);

        Assert.Equal(RepositoryProfilingOutcome.Success, first.Outcome);
        Assert.Equal(RepositoryProfilingOutcome.Success, second.Outcome);
        Assert.NotNull(first.Value);
        Assert.NotNull(second.Value);
        Assert.NotEqual(first.Value.Id, second.Value.Id);
        Assert.Equal(2, codeGraph.SnapshotRoots.Count);
        Assert.All(codeGraph.SnapshotRoots, observation => Assert.False(observation.ContainsGitMetadata));

        db.ChangeTracker.Clear();
        Guid[] repositoryIds = [firstRepository.Id, secondRepository.Id];
        ScanRun[] runs = await db.ScanRuns
            .AsNoTracking()
            .Where(run => repositoryIds.Contains(run.RepositoryId))
            .ToArrayAsync();
        PreScanResult[] results = await db.PreScanResults
            .AsNoTracking()
            .Include(result => result.Classifications)
            .Where(result => repositoryIds.Contains(result.RepositoryId))
            .ToArrayAsync();
        CodeUnit[] codeUnits = await db.CodeUnits
            .AsNoTracking()
            .Where(unit => repositoryIds.Contains(unit.RepositoryId))
            .ToArrayAsync();
        CodeUnitFramework[] frameworks = await db.CodeUnitFrameworks
            .AsNoTracking()
            .Where(framework => repositoryIds.Contains(framework.RepositoryId))
            .ToArrayAsync();

        Assert.Equal(2, runs.Length);
        Assert.Equal(2, results.Length);
        Assert.Equal(2, codeUnits.Length);
        Assert.Equal(2, frameworks.Length);
        AssertPersistedComposition(
            firstRepository.Id,
            first.Value.Id,
            firstRevision,
            runs,
            results,
            codeUnits,
            frameworks);
        AssertPersistedComposition(
            secondRepository.Id,
            second.Value.Id,
            secondRevision,
            runs,
            results,
            codeUnits,
            frameworks);

        Assert.All(codeUnits, unit => Assert.NotEqual(CodeUnitStatus.Dispatched, unit.Status));
        Assert.Empty(await db.CodeUnitEdges.AsNoTracking().ToListAsync());
        Assert.Empty(await db.UnresolvedStructuralRelationshipCandidates.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PreparedCommittedConfigurations_ThreeFlattenedSyntaxes_DetectPersistReplayAndMaterializeApprovedHandoffAsync()
    {
        using var source = new TemporaryGitRepository();
        source.Write("config/application.properties", "dubbo.application.name=ledger\n");
        source.Write("config/application.yml", "server.servlet.context-path: /ledger\n");
        source.Write("config/application.yaml", "server.servlet.context-path: /ledger-api\n");
        string committedRevision = source.CommitAll();
        using var storage = new TemporaryDirectory();
        await using VulgataDbContext db = await CreateDatabaseAsync();
        RepositoryEntity repository = RepositoryEntity.CreateStandalone(
            "story-43-e2e",
            source.Path,
            null,
            null,
            DateTimeOffset.UtcNow);
        db.Repositories.Add(repository);
        await db.SaveChangesAsync();

        var workingCopies = new GitWorkingCopyService(new GitWorkingCopyOptions(storage.Path));
        ScanRun run = ScanRun.Start(repository.Id, 1_800, DateTimeOffset.UtcNow);
        Guid scanRunId = run.Id;
        db.ScanRuns.Add(run);
        await db.SaveChangesAsync();
        GitWorkingCopyResult workingCopy = await workingCopies.AcquireAsync(
            repository,
            scanRunId,
            CancellationToken.None);
        Assert.True(workingCopy.Succeeded);
        Assert.Equal(committedRevision, workingCopy.ProfiledRevision);

        var snapshots = new CommittedRepositorySnapshotService(new GitWorkingCopyOptions(storage.Path));
        await using ICommittedRepositorySnapshotLease snapshot = await snapshots.AcquireAsync(
            repository,
            scanRunId,
            workingCopy,
            CancellationToken.None);
        run.SetCandidateCount(0);
        run.BindProfiledRevision(committedRevision);
        run.Complete(0, 0, DateTimeOffset.UtcNow);
        RepositoryProfileDraft profile = RepositoryProfileDraft.Create(
            run, [], [], [], [], [], []);
        var detector = new CommunicationKindDetector(CommunicationEvidenceRuleCatalog.LoadEmbedded());
        PreScanResultDraft result = await detector.DetectAsync(
            profile,
            committedRevision,
            snapshot,
            CancellationToken.None);

        Assert.Equal(ConfirmedCommunicationKinds.Rpc | ConfirmedCommunicationKinds.Http, result.Result.ConfirmedCommunicationKinds);
        Assert.Contains(result.Result.Classifications, item => item.RelativePath == "config/application.properties" && item.Kind == CommunicationKind.Rpc);
        Assert.Contains(result.Result.Classifications, item => item.RelativePath == "config/application.yml" && item.Kind == CommunicationKind.Http);
        Assert.Contains(result.Result.Classifications, item => item.RelativePath == "config/application.yaml" && item.Kind == CommunicationKind.Http);
        Assert.All(result.Result.Classifications, item => Assert.DoesNotContain(':', item.RelativePath));

        RepositoryProfileCommitDraft commit = RepositoryProfileCommitDraft.Create(profile, result);
        var runs = new ScanRunRepository(db);
        PreScanResult first = await runs.PersistCompletedProfileAsync(commit);
        db.ChangeTracker.Clear();
        PreScanResult replay = await runs.PersistCompletedProfileAsync(commit);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, await db.Set<PreScanResult>().CountAsync());
        Assert.Equal(1, await db.ScanRuns.CountAsync());

        db.ChangeTracker.Clear();
        ApprovedPreScanHandoff? handoff = await runs.GetApprovedPreScanHandoffAsync(repository.Id, scanRunId);
        Assert.NotNull(handoff);
        Assert.Equal("1.0", handoff.SchemaVersion);
        Assert.Equal(committedRevision, handoff.ProfiledRevision);
        Assert.Equal([CommunicationKind.Rpc, CommunicationKind.Http], handoff.ConfirmedKinds);
        Assert.Equal("预扫描已批准——继续进入顺序演示运行器", handoff.ApprovalMessage);
    }

    private static async Task<VulgataDbContext> CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<VulgataDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new VulgataDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static string WritePreparedRepository(TemporaryGitRepository source, string marker)
    {
        source.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="2.3.0" />
              </ItemGroup>
            </Project>
            """);
        source.Write(
            "src/App/Controller.cs",
            $$"""
            using Microsoft.AspNetCore.Mvc;
            namespace Prepared;
            [ApiController]
            public sealed class {{marker.Replace("-", "_", StringComparison.Ordinal)}}Controller : ControllerBase
            {
                [HttpGet("/{{marker}}")]
                public string Get() => "ok";
            }
            """);
        source.Write("config/application.properties", $"dubbo.application.name={marker}\n");
        source.Write("config/application.yml", $"server.servlet.context-path: /{marker}\n");
        return source.CommitAll();
    }

    private static void AssertPersistedComposition(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        IReadOnlyList<ScanRun> runs,
        IReadOnlyList<PreScanResult> results,
        IReadOnlyList<CodeUnit> codeUnits,
        IReadOnlyList<CodeUnitFramework> frameworks)
    {
        ScanRun run = Assert.Single(runs, item => item.RepositoryId == repositoryId);
        Assert.Equal(scanRunId, run.Id);
        Assert.Equal(ScanRunStatus.Completed, run.Status);
        Assert.Equal(profiledRevision, run.ProfiledRevision);

        PreScanResult result = Assert.Single(results, item => item.RepositoryId == repositoryId);
        Assert.Equal(scanRunId, result.ScanRunId);
        Assert.Equal(profiledRevision, result.ProfiledRevision);
        Assert.Equal(PreScanApprovalState.PreScanApproved, result.State);
        Assert.Equal(
            ConfirmedCommunicationKinds.Rpc | ConfirmedCommunicationKinds.Http,
            result.ConfirmedCommunicationKinds);
        Assert.Contains(
            result.Classifications,
            item => item.Kind == CommunicationKind.Rpc && item.Level == ClassificationLevel.Confirmed);
        Assert.Contains(
            result.Classifications,
            item => item.Kind == CommunicationKind.Http && item.Level == ClassificationLevel.Confirmed);

        CodeUnit unit = Assert.Single(codeUnits, item => item.RepositoryId == repositoryId);
        Assert.Equal(scanRunId, unit.ScanRunId);
        Assert.Equal("src/App/Controller.cs", unit.FilePath);
        CodeUnitFramework framework = Assert.Single(
            frameworks,
            item => item.RepositoryId == repositoryId);
        Assert.Equal(scanRunId, framework.ScanRunId);
        Assert.Equal(unit.Id, framework.CodeUnitId);
        Assert.Equal("aspnet-core", framework.FrameworkName);
        Assert.Equal(FrameworkConfidence.High, framework.Confidence);
        Assert.StartsWith("src/App/App.csproj#", framework.DetectionSource, StringComparison.Ordinal);
    }

    private sealed class PreparedCodeGraphProvider : ICodeGraphProvider
    {
        public List<SnapshotObservation> SnapshotRoots { get; } = [];

        public Task<CodeGraphProviderResult> ProfileAsync(
            string workingCopyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SnapshotRoots.Add(new(
                workingCopyPath,
                Directory.Exists(System.IO.Path.Combine(workingCopyPath, ".git"))));
            return Task.FromResult(new CodeGraphProviderResult(
                true,
                [
                    new ProviderCodeUnit(
                        "src/App/Controller.cs",
                        "PreparedController",
                        CodeUnitType.Class,
                        3,
                        8,
                        "prepared-controller",
                        RangeConfidence.Exact,
                        "c_sharp"),
                ],
                [],
                [],
                1,
                TimeSpan.FromMilliseconds(1),
                null));
        }
    }

    private sealed record SnapshotObservation(string RootPath, bool ContainsGitMetadata);

    private sealed class TestUserContext : IUserContext
    {
        public string UserId => "production-composition-user";
        public bool IsAdministrator => false;
        public SystemOwnerFilter ToSystemOwnerFilter() => SystemOwnerFilter.OwnedBy(UserId);
    }

    private sealed class TemporaryGitRepository : IDisposable
    {
        public TemporaryGitRepository()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vulgata-story43-e2e-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            RunGit("init", "--quiet");
            RunGit("config", "user.email", "vulgata-tests@example.invalid");
            RunGit("config", "user.name", "Vulgata Tests");
            RunGit("config", "core.autocrlf", "false");
        }

        public string Path { get; }

        public void Write(string relativePath, string contents)
        {
            string path = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        public string CommitAll()
        {
            RunGit("add", "--all");
            RunGit("commit", "--quiet", "-m", "prepared configuration fixture");
            return RunGit("rev-parse", "--verify", "HEAD").Trim();
        }

        public void Dispose() => Delete(Path);

        private string RunGit(params string[] arguments)
        {
            using var process = new Process { StartInfo = new ProcessStartInfo("git") { WorkingDirectory = Path, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
            foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            Assert.True(process.Start());
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
            return output;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vulgata-story43-e2e-storage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose() => Delete(Path);
    }

    private static void Delete(string path)
    {
        try { Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

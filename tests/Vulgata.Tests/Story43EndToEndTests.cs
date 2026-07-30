using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CommunicationDetection;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Git;
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

    private sealed class TemporaryGitRepository : IDisposable
    {
        public TemporaryGitRepository()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vulgata-story43-e2e-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            RunGit("init", "--quiet");
            RunGit("config", "user.email", "vulgata-tests@example.invalid");
            RunGit("config", "user.name", "Vulgata Tests");
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

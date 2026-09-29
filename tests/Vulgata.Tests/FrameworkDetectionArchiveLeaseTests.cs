using Vulgata.Tests.TestInfrastructure;
using System.Diagnostics;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.FrameworkDetection;
using Vulgata.Infrastructure.Git;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Tests;

public sealed class FrameworkDetectionArchiveLeaseTests
{
    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ArchiveOnlyLease_CommittedPackageManifest_ReturnsRevisionBoundFrameworkEvidenceAsync()
    {
        await using ArchiveLeaseFixture fixture = await CreateArchiveLeaseFixtureAsync();
        ICommittedRepositorySnapshotLease lease = fixture.Lease;
        IFrameworkEvidenceDetector detector =
            new FrameworkEvidenceDetector(FrameworkCatalog.LoadEmbedded());

        Assert.False(Directory.Exists(Path.Combine(lease.RootPath, ".git")));

        FrameworkEvidenceDetectionResult result = await detector.DetectDetailedAsync(
            lease,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        PackageEvidence evidence = Assert.Single(result.Evidence);
        Assert.Equal("src/ui/package.json", evidence.ArtifactPath);
        Assert.Equal("npm:react", evidence.PackageCoordinate);
        Assert.Equal(fixture.ProfiledRevision, lease.ProfiledRevision);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ArchiveOnlyLease_ValidIdentity_VerifiesBeforeAndAfterReadsAsync()
    {
        await using ArchiveLeaseFixture fixture = await CreateArchiveLeaseFixtureAsync();
        var recordingLease = new RecordingSnapshotLease(fixture.Lease);
        IFrameworkEvidenceDetector detector =
            new FrameworkEvidenceDetector(FrameworkCatalog.LoadEmbedded());

        FrameworkEvidenceDetectionResult result = await detector.DetectDetailedAsync(
            recordingLease,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, recordingLease.VerifyCallCount);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ArchiveOnlyLease_IdentityMutatesBeforeFinalVerification_FailsClosedAsync()
    {
        await using ArchiveLeaseFixture fixture = await CreateArchiveLeaseFixtureAsync();
        var recordingLease = new RecordingSnapshotLease(
            fixture.Lease,
            verifyCall =>
            {
                if (verifyCall == 2)
                {
                    File.AppendAllText(
                        Path.Combine(fixture.Lease.RootPath, "src", "ui", "package.json"),
                        Environment.NewLine,
                        new UTF8Encoding(false));
                }
            });
        IFrameworkEvidenceDetector detector =
            new FrameworkEvidenceDetector(FrameworkCatalog.LoadEmbedded());

        FrameworkEvidenceDetectionResult result = await detector.DetectDetailedAsync(
            recordingLease,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(FrameworkEvidenceDetectionFailure.DetectorFailed, result.Failure);
        Assert.Empty(result.Evidence);
        Assert.Equal(2, recordingLease.VerifyCallCount);
        Assert.DoesNotContain(fixture.Lease.RootPath, result.Error, StringComparison.Ordinal);
    }

    [Trait("Suite", "LocalTools")]
    [SuiteFact("LocalTools")]
    public async Task ArchiveOnlyLease_DisposedLease_FailsClosedWithoutEvidenceAsync()
    {
        await using ArchiveLeaseFixture fixture = await CreateArchiveLeaseFixtureAsync();
        await fixture.Lease.DisposeAsync();
        IFrameworkEvidenceDetector detector =
            new FrameworkEvidenceDetector(FrameworkCatalog.LoadEmbedded());

        FrameworkEvidenceDetectionResult result = await detector.DetectDetailedAsync(
            fixture.Lease,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(FrameworkEvidenceDetectionFailure.DetectorFailed, result.Failure);
        Assert.Empty(result.Evidence);
        Assert.DoesNotContain(fixture.Lease.RootPath, result.Error, StringComparison.Ordinal);
    }

    private static async Task<ArchiveLeaseFixture> CreateArchiveLeaseFixtureAsync()
    {
        var source = new TemporaryGitRepository();
        var storage = new TemporaryDirectory();
        try
        {
            source.Write(
                "src/ui/package.json",
                """
                {
                  "dependencies": {
                    "react": "19.0.0"
                  }
                }
                """);
            string profiledRevision = source.CommitAll();
            RepositoryEntity repository = RepositoryEntity.CreateStandalone(
                "archive-framework",
                source.Path,
                null,
                null,
                DateTimeOffset.UtcNow);
            ICommittedRepositorySnapshotService snapshotService =
                new CommittedRepositorySnapshotService(
                    new GitWorkingCopyOptions(storage.Path));
            ICommittedRepositorySnapshotLease lease = await snapshotService.AcquireAsync(
                repository,
                Guid.NewGuid(),
                GitWorkingCopyResult.Success(source.Path, profiledRevision),
                CancellationToken.None);
            return new ArchiveLeaseFixture(source, storage, lease, profiledRevision);
        }
        catch
        {
            source.Dispose();
            storage.Dispose();
            throw;
        }
    }

    private sealed class ArchiveLeaseFixture(
        TemporaryGitRepository source,
        TemporaryDirectory storage,
        ICommittedRepositorySnapshotLease lease,
        string profiledRevision) : IAsyncDisposable
    {
        public ICommittedRepositorySnapshotLease Lease { get; } = lease;

        public string ProfiledRevision { get; } = profiledRevision;

        public async ValueTask DisposeAsync()
        {
            await Lease.DisposeAsync();
            source.Dispose();
            storage.Dispose();
        }
    }

    private sealed class RecordingSnapshotLease(
        ICommittedRepositorySnapshotLease inner,
        Action<int>? beforeVerify = null) : ICommittedRepositorySnapshotLease
    {
        public int VerifyCallCount { get; private set; }

        public Guid RepositoryId => inner.RepositoryId;

        public Guid ScanRunId => inner.ScanRunId;

        public string ProfiledRevision => inner.ProfiledRevision;

        public string RootPath => inner.RootPath;

        public async Task VerifyIdentityAsync(
            CancellationToken cancellationToken = default)
        {
            VerifyCallCount++;
            beforeVerify?.Invoke(VerifyCallCount);
            await inner.VerifyIdentityAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TemporaryGitRepository : IDisposable
    {
        public TemporaryGitRepository()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"vulgata-framework-archive-source-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            RunGit("init", "--quiet");
            RunGit("config", "core.autocrlf", "false");
            RunGit("config", "user.email", "vulgata-tests@example.invalid");
            RunGit("config", "user.name", "Vulgata Tests");
        }

        public string Path { get; }

        public void Write(string relativePath, string contents)
        {
            string path = System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        public string CommitAll()
        {
            RunGit("add", "--all");
            RunGit("commit", "--quiet", "-m", "archive framework fixture");
            return RunGit("rev-parse", "--verify", "HEAD").Trim();
        }

        public void Dispose() => Delete(Path);

        private string RunGit(params string[] arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("git")
                {
                    WorkingDirectory = Path,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            foreach (string argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            Assert.True(process.Start());
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                $"git {string.Join(' ', arguments)} failed: {error}");
            return output;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"vulgata-framework-archive-storage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Delete(Path);
    }

    private static void Delete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

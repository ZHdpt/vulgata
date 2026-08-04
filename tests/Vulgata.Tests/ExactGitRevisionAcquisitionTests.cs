using System.Diagnostics;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Git;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Tests;

public sealed class ExactGitRevisionAcquisitionTests
{
    [Fact]
    public async Task AcquireAsync_UnsafeLockRef_RefusesBeforeCreatingAnOperationRootAsync()
    {
        await using ExactCrossRepositoryTemporaryGitRepository source =
            await CreateTaggedSourceAsync(annotatedTag: false);
        string storageRoot = ExactCrossRepositoryCleanup.CreateUniqueRoot("exact-git-unsafe-ref");
        try
        {
            RepositoryEntity repository = CreateRepository(source.RootPath);
            var service = new ExactGitRevisionSnapshotService(
                new GitWorkingCopyOptions(storageRoot));

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.AcquireAsync(
                    repository,
                    Guid.NewGuid(),
                    ExactCrossRepositoryTestValues.ExpectedRef + ".lock",
                    source.Commit,
                    CancellationToken.None));

            Assert.Equal("One safe full tag ref is required. (Parameter 'value')", exception.Message);
            Assert.False(HasOperationDirectories(storageRoot, repository.Id));
        }
        finally
        {
            await ExactCrossRepositoryCleanup.DeleteAsync(storageRoot);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcquireAsync_FullTagRef_ReturnsExactPrivateSnapshotWithoutChangingOrdinaryCheckoutAsync(
        bool annotatedTag)
    {
        await using ExactCrossRepositoryTemporaryGitRepository source =
            await CreateTaggedSourceAsync(annotatedTag);
        string exactCommit = source.Commit;
        source.Write("src/version.txt", "ordinary-head");
        string ordinaryCommit = await source.CommitAllAsync();
        string storageRoot = ExactCrossRepositoryCleanup.CreateUniqueRoot("exact-git-storage");
        try
        {
            RepositoryEntity repository = CreateRepository(source.RootPath);
            var options = new GitWorkingCopyOptions(storageRoot);
            var ordinaryService = new GitWorkingCopyService(options);
            GitWorkingCopyResult initial = await ordinaryService.AcquireAsync(
                repository,
                Guid.NewGuid(),
                CancellationToken.None);
            Assert.True(initial.Succeeded);
            Assert.Equal(ordinaryCommit, initial.ProfiledRevision);
            OrdinaryCheckoutState before = await CaptureOrdinaryStateAsync(
                initial.WorkingCopyPath!);
            var exactService = new ExactGitRevisionSnapshotService(options);
            Guid scanRunId = Guid.NewGuid();

            ICommittedRepositorySnapshotLease lease = await exactService.AcquireAsync(
                repository,
                scanRunId,
                ExactCrossRepositoryTestValues.ExpectedRef,
                exactCommit,
                CancellationToken.None);
            string snapshotRoot = lease.RootPath;

            Assert.Equal(repository.Id, lease.RepositoryId);
            Assert.Equal(scanRunId, lease.ScanRunId);
            Assert.Equal(exactCommit, lease.ProfiledRevision);
            Assert.Matches("^[0-9a-f]{40}$", lease.ProfiledRevision);
            Assert.True(IsContainedByExactRoot(storageRoot, repository.Id, snapshotRoot));
            Assert.NotEqual(initial.WorkingCopyPath, snapshotRoot);
            Assert.False(Directory.Exists(Path.Combine(snapshotRoot, ".git")));
            Assert.Equal(
                "tagged-revision",
                await File.ReadAllTextAsync(
                    Path.Combine(snapshotRoot, "src", "version.txt"),
                    CancellationToken.None));
            await lease.VerifyIdentityAsync(CancellationToken.None);
            Assert.Equal(before, await CaptureOrdinaryStateAsync(initial.WorkingCopyPath!));

            await lease.DisposeAsync();
            await lease.DisposeAsync();
            Assert.False(Directory.Exists(snapshotRoot));
            Assert.False(HasOperationDirectories(storageRoot, repository.Id));

            GitWorkingCopyResult following = await ordinaryService.AcquireAsync(
                repository,
                Guid.NewGuid(),
                CancellationToken.None);
            Assert.True(following.Succeeded);
            Assert.Equal(initial.WorkingCopyPath, following.WorkingCopyPath);
            Assert.Equal(ordinaryCommit, following.ProfiledRevision);
            Assert.Equal(before, await CaptureOrdinaryStateAsync(following.WorkingCopyPath!));
        }
        finally
        {
            await ExactCrossRepositoryCleanup.DeleteAsync(storageRoot);
        }
    }

    [Fact]
    public async Task AcquireAsync_ConcurrentOrdinaryAndExactLeases_KeepOperationRootsAndCheckoutIndependentAsync()
    {
        await using ExactCrossRepositoryTemporaryGitRepository source =
            await CreateTaggedSourceAsync(annotatedTag: false);
        string exactCommit = source.Commit;
        source.Write("src/version.txt", "ordinary-head");
        string ordinaryCommit = await source.CommitAllAsync();
        string storageRoot = ExactCrossRepositoryCleanup.CreateUniqueRoot("exact-git-concurrent");
        try
        {
            RepositoryEntity repository = CreateRepository(source.RootPath);
            var options = new GitWorkingCopyOptions(storageRoot);
            var ordinaryService = new GitWorkingCopyService(options);
            var exactService = new ExactGitRevisionSnapshotService(options);
            GitWorkingCopyResult initial = await ordinaryService.AcquireAsync(
                repository,
                Guid.NewGuid(),
                CancellationToken.None);
            Assert.True(initial.Succeeded);
            OrdinaryCheckoutState before = await CaptureOrdinaryStateAsync(
                initial.WorkingCopyPath!);

            Task<ICommittedRepositorySnapshotLease> firstExactTask = exactService.AcquireAsync(
                repository,
                Guid.NewGuid(),
                ExactCrossRepositoryTestValues.ExpectedRef,
                exactCommit,
                CancellationToken.None);
            Task<ICommittedRepositorySnapshotLease> secondExactTask = exactService.AcquireAsync(
                repository,
                Guid.NewGuid(),
                ExactCrossRepositoryTestValues.ExpectedRef,
                exactCommit,
                CancellationToken.None);
            Task<GitWorkingCopyResult> ordinaryTask = ordinaryService.AcquireAsync(
                repository,
                Guid.NewGuid(),
                CancellationToken.None);

            ICommittedRepositorySnapshotLease[] exactLeases =
                await Task.WhenAll(firstExactTask, secondExactTask);
            GitWorkingCopyResult concurrentOrdinary = await ordinaryTask;
            Assert.True(concurrentOrdinary.Succeeded);
            Assert.Equal(ordinaryCommit, concurrentOrdinary.ProfiledRevision);
            Assert.NotEqual(exactLeases[0].RootPath, exactLeases[1].RootPath);
            Assert.All(exactLeases, lease =>
            {
                Assert.Equal(exactCommit, lease.ProfiledRevision);
                Assert.True(IsContainedByExactRoot(storageRoot, repository.Id, lease.RootPath));
            });
            Assert.Equal(before, await CaptureOrdinaryStateAsync(initial.WorkingCopyPath!));

            string retainedRoot = exactLeases[1].RootPath;
            await exactLeases[0].DisposeAsync();
            Assert.False(Directory.Exists(exactLeases[0].RootPath));
            Assert.True(Directory.Exists(retainedRoot));
            await exactLeases[1].VerifyIdentityAsync(CancellationToken.None);
            await exactLeases[1].DisposeAsync();
            Assert.False(HasOperationDirectories(storageRoot, repository.Id));

            GitWorkingCopyResult following = await ordinaryService.AcquireAsync(
                repository,
                Guid.NewGuid(),
                CancellationToken.None);
            Assert.True(following.Succeeded);
            Assert.Equal(initial.WorkingCopyPath, following.WorkingCopyPath);
            Assert.Equal(ordinaryCommit, following.ProfiledRevision);
            Assert.Equal(before, await CaptureOrdinaryStateAsync(following.WorkingCopyPath!));
        }
        finally
        {
            await ExactCrossRepositoryCleanup.DeleteAsync(storageRoot);
        }
    }

    [Fact]
    public async Task AcquireAsync_MissingMovedSimilarWrongAndArchiveFailures_AreSanitizedAndCleanedAsync()
    {
        await using ExactCrossRepositoryTemporaryGitRepository source =
            await CreateTaggedSourceAsync(annotatedTag: false);
        string expectedCommit = source.Commit;
        source.Write("src/version.txt", "moved-tag");
        string movedCommit = await source.CommitAllAsync();
        string storageRoot = ExactCrossRepositoryCleanup.CreateUniqueRoot("exact-git-refusal");
        try
        {
            RepositoryEntity repository = CreateRepository(source.RootPath);
            var options = new GitWorkingCopyOptions(storageRoot);
            var service = new ExactGitRevisionSnapshotService(options);

            await source.RunAsync(
                ["tag", "--delete", "vulgata/August15th-sample"],
                CancellationToken.None);
            await source.RunAsync(
                ["tag", "vulgata/August15th-sample-similar", expectedCommit],
                CancellationToken.None);
            await AssertSanitizedFailureAsync(
                service,
                repository,
                ExactCrossRepositoryTestValues.ExpectedRef,
                expectedCommit,
                storageRoot);

            await source.RunAsync(
                ["tag", "vulgata/August15th-sample", movedCommit],
                CancellationToken.None);
            await AssertSanitizedFailureAsync(
                service,
                repository,
                ExactCrossRepositoryTestValues.ExpectedRef,
                expectedCommit,
                storageRoot);
            await AssertSanitizedFailureAsync(
                service,
                repository,
                ExactCrossRepositoryTestValues.ExpectedRef,
                new string('0', 40),
                storageRoot);

            var boundedService = new ExactGitRevisionSnapshotService(
                options,
                new CommittedRepositorySnapshotLimits(1, 1));
            await AssertSanitizedFailureAsync(
                boundedService,
                repository,
                ExactCrossRepositoryTestValues.ExpectedRef,
                movedCommit,
                storageRoot);

            string privateMissingRemote = Path.Combine(storageRoot, "private-host-credential");
            RepositoryEntity missingRemote = CreateRepository(privateMissingRemote);
            await AssertSanitizedFailureAsync(
                service,
                missingRemote,
                ExactCrossRepositoryTestValues.ExpectedRef,
                movedCommit,
                storageRoot);
        }
        finally
        {
            await ExactCrossRepositoryCleanup.DeleteAsync(storageRoot);
        }
    }

    [Fact]
    public async Task AcquireAsync_AbbreviatedCommit_RefusesBeforeGitAccessAsync()
    {
        await using ExactCrossRepositoryTemporaryGitRepository source =
            await CreateTaggedSourceAsync(annotatedTag: false);
        string storageRoot = ExactCrossRepositoryCleanup.CreateUniqueRoot("exact-git-short-commit");
        try
        {
            RepositoryEntity repository = CreateRepository(source.RootPath);
            var service = new ExactGitRevisionSnapshotService(
                new GitWorkingCopyOptions(storageRoot));

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.AcquireAsync(
                    repository,
                    Guid.NewGuid(),
                    ExactCrossRepositoryTestValues.ExpectedRef,
                    source.Commit[..12],
                    CancellationToken.None));

            Assert.Equal("One full lowercase Git commit is required. (Parameter 'value')", exception.Message);
            Assert.False(HasOperationDirectories(storageRoot, repository.Id));
        }
        finally
        {
            await ExactCrossRepositoryCleanup.DeleteAsync(storageRoot);
        }
    }

    [Fact]
    public async Task AcquireAsync_CancelledOperation_CleansOnlyItsRootAndLeavesConcurrentLeaseUsableAsync()
    {
        await using ExactCrossRepositoryTemporaryGitRepository source =
            await CreateTaggedSourceAsync(annotatedTag: false);
        string storageRoot = ExactCrossRepositoryCleanup.CreateUniqueRoot("exact-git-cancel");
        try
        {
            RepositoryEntity repository = CreateRepository(source.RootPath);
            var service = new ExactGitRevisionSnapshotService(
                new GitWorkingCopyOptions(storageRoot));
            ICommittedRepositorySnapshotLease retained = await service.AcquireAsync(
                repository,
                Guid.NewGuid(),
                ExactCrossRepositoryTestValues.ExpectedRef,
                source.Commit,
                CancellationToken.None);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.AcquireAsync(
                    repository,
                    Guid.NewGuid(),
                    ExactCrossRepositoryTestValues.ExpectedRef,
                    source.Commit,
                    cancellation.Token));

            Assert.True(Directory.Exists(retained.RootPath));
            await retained.VerifyIdentityAsync(CancellationToken.None);
            Assert.Single(OperationDirectories(storageRoot, repository.Id));
            await retained.DisposeAsync();
            Assert.False(HasOperationDirectories(storageRoot, repository.Id));
        }
        finally
        {
            await ExactCrossRepositoryCleanup.DeleteAsync(storageRoot);
        }
    }

    [Fact]
    public async Task VerifyIdentityAsync_DriftedSnapshot_RefusesWithoutLeakingSourceOrPathAsync()
    {
        await using ExactCrossRepositoryTemporaryGitRepository source =
            await CreateTaggedSourceAsync(annotatedTag: false);
        string storageRoot = ExactCrossRepositoryCleanup.CreateUniqueRoot("exact-git-identity");
        try
        {
            RepositoryEntity repository = CreateRepository(source.RootPath);
            var service = new ExactGitRevisionSnapshotService(
                new GitWorkingCopyOptions(storageRoot));
            ICommittedRepositorySnapshotLease lease = await service.AcquireAsync(
                repository,
                Guid.NewGuid(),
                ExactCrossRepositoryTestValues.ExpectedRef,
                source.Commit,
                CancellationToken.None);
            await File.WriteAllTextAsync(
                Path.Combine(lease.RootPath, "src", "version.txt"),
                "private-source-drift",
                CancellationToken.None);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                lease.VerifyIdentityAsync(CancellationToken.None));

            Assert.Equal("精确版本快照标识不匹配。", exception.Message);
            Assert.DoesNotContain("private-source-drift", exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(lease.RootPath, exception.ToString(), StringComparison.OrdinalIgnoreCase);
            await lease.DisposeAsync();
        }
        finally
        {
            await ExactCrossRepositoryCleanup.DeleteAsync(storageRoot);
        }
    }

    [Fact]
    public async Task DisposeAsync_CleanupFailure_InvalidatesLeaseAndCanRetryOnlyItsOwnRootAsync()
    {
        await using ExactCrossRepositoryTemporaryGitRepository source =
            await CreateTaggedSourceAsync(annotatedTag: false);
        string storageRoot = ExactCrossRepositoryCleanup.CreateUniqueRoot("exact-git-cleanup-failure");
        try
        {
            RepositoryEntity repository = CreateRepository(source.RootPath);
            var service = new ExactGitRevisionSnapshotService(
                new GitWorkingCopyOptions(storageRoot));
            ICommittedRepositorySnapshotLease lease = await service.AcquireAsync(
                repository,
                Guid.NewGuid(),
                ExactCrossRepositoryTestValues.ExpectedRef,
                source.Commit,
                CancellationToken.None);
            string lockedPath = Path.Combine(lease.RootPath, "src", "version.txt");

            await using (var locked = new FileStream(
                lockedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => lease.DisposeAsync().AsTask());
                Assert.Equal("精确 Git 版本资源清理失败。", exception.Message);
                await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                    lease.VerifyIdentityAsync(CancellationToken.None));
            }

            await lease.DisposeAsync();
            Assert.False(Directory.Exists(lease.RootPath));
            Assert.False(HasOperationDirectories(storageRoot, repository.Id));
        }
        finally
        {
            await ExactCrossRepositoryCleanup.DeleteAsync(storageRoot);
        }
    }

    private static async Task<ExactCrossRepositoryTemporaryGitRepository> CreateTaggedSourceAsync(
        bool annotatedTag)
    {
        ExactCrossRepositoryTemporaryGitRepository source =
            await ExactCrossRepositoryTemporaryGitRepository.CreateAsync(
                annotatedTag ? "annotated" : "lightweight",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["src/version.txt"] = "tagged-revision",
                },
                CancellationToken.None);
        if (annotatedTag)
        {
            await source.RunAsync(
                ["tag", "--delete", "vulgata/August15th-sample"],
                CancellationToken.None);
            await source.RunAsync(
                [
                    "tag",
                    "--annotate",
                    "--message",
                    "exact fixture",
                    "vulgata/August15th-sample",
                    source.Commit,
                ],
                CancellationToken.None);
        }

        return source;
    }

    private static RepositoryEntity CreateRepository(string gitUrl) =>
        RepositoryEntity.CreateStandalone(
            "exact-git-fixture",
            gitUrl,
            null,
            null,
            DateTimeOffset.UtcNow);

    private static bool IsContainedByExactRoot(
        string storageRoot,
        Guid repositoryId,
        string candidate)
    {
        string exactRoot = Path.GetFullPath(Path.Combine(
            storageRoot,
            ".exact-revisions",
            repositoryId.ToString("N"))) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(candidate).StartsWith(
            exactRoot,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasOperationDirectories(string storageRoot, Guid repositoryId)
    {
        string repositoryRoot = Path.Combine(
            storageRoot,
            ".exact-revisions",
            repositoryId.ToString("N"));
        return Directory.Exists(repositoryRoot) &&
            Directory.EnumerateFileSystemEntries(repositoryRoot).Any();
    }

    private static string[] OperationDirectories(string storageRoot, Guid repositoryId)
    {
        string repositoryRoot = Path.Combine(
            storageRoot,
            ".exact-revisions",
            repositoryId.ToString("N"));
        return Directory.Exists(repositoryRoot)
            ? [.. Directory.EnumerateDirectories(repositoryRoot)]
            : [];
    }

    private static async Task AssertSanitizedFailureAsync(
        ExactGitRevisionSnapshotService service,
        RepositoryEntity repository,
        string expectedRef,
        string expectedCommit,
        string storageRoot)
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcquireAsync(
                repository,
                Guid.NewGuid(),
                expectedRef,
                expectedCommit,
                CancellationToken.None));
        string diagnostic = exception.ToString();
        Assert.Equal("精确 Git 版本获取失败。", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain(repository.GitUrl, diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(storageRoot, diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(expectedRef, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedCommit, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("fatal:", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fetch", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.False(HasOperationDirectories(storageRoot, repository.Id));
    }

    private static async Task<OrdinaryCheckoutState> CaptureOrdinaryStateAsync(string path)
    {
        string branch = await RunGitAsync(path, ["symbolic-ref", "--short", "HEAD"]);
        string head = await RunGitAsync(path, ["rev-parse", "--verify", "HEAD^{commit}"]);
        string origin = await RunGitAsync(path, ["remote", "get-url", "origin"]);
        string indexTree = await RunGitAsync(path, ["write-tree"]);
        string status = await RunGitAsync(
            path,
            ["status", "--porcelain=v1", "--untracked-files=all"]);
        Assert.NotEmpty(branch);
        Assert.Empty(status);
        return new(path, branch, head, origin, indexTree, status);
    }

    private static async Task<string> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start());
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken.None);
        _ = await errorTask;
        Assert.True(process.ExitCode == 0, "Synthetic Git fixture command failed.");
        return (await outputTask).TrimEnd('\r', '\n');
    }

    private sealed record OrdinaryCheckoutState(
        string Path,
        string Branch,
        string Head,
        string Origin,
        string IndexTree,
        string Status);
}

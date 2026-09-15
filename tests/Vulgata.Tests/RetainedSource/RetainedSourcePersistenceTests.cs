using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Xunit.Abstractions;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourcePersistenceTests
{
    private readonly ITestOutputHelper _output;

    public RetainedSourcePersistenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task AcquireAsync_SameIdentityReplay_ReturnsStableReceiptAndOneVisibleObjectAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        DateTimeOffset capturedAt =
            new DateTimeOffset(2026, 9, 15, 12, 34, 56, TimeSpan.Zero).AddTicks(1);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                timeProvider: new FixedRetainedSourceTimeProvider(capturedAt));

        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);

        SourceCaptureResult first = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);
        SourceCaptureResult replay = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);

        Assert.Equal(SourceCaptureStatus.Completed, first.Status);
        Assert.Equal(SourceCaptureStatus.Replay, replay.Status);
        Assert.NotEqual(first.CorrelationId, replay.CorrelationId);
        Assert.Equal(first.RepositoryId, replay.RepositoryId);
        Assert.Equal(first.ProfiledRevision, replay.ProfiledRevision);
        Assert.Equal(first.GitObjectFormat, replay.GitObjectFormat);
        Assert.Equal(first.ManifestSha256, replay.ManifestSha256);
        Assert.Equal(first.EntryCount, replay.EntryCount);
        Assert.Equal(first.TotalBlobBytes, replay.TotalBlobBytes);

        IDbContextFactory<VulgataDbContext> contextFactory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await contextFactory.CreateDbContextAsync();
        List<SourceSnapshot> snapshots = await context.SourceSnapshots
            .Where(snapshot => snapshot.RepositoryId == fixture.RepositoryId)
            .ToListAsync();

        SourceSnapshot snapshot = Assert.Single(snapshots);
        string objectPath = Path.Combine(
            fixture.SourceStorageRoot,
            snapshot.InternalStorageKey.Replace('/', Path.DirectorySeparatorChar));
        string identityRoot = Path.Combine(
            fixture.SourceStorageRoot,
            "snapshots",
            snapshot.RepositoryId.ToString("N"),
            snapshot.ProfiledRevision);
        string[] sealedObjects = Directory.Exists(identityRoot)
            ? Directory.GetDirectories(identityRoot)
            : [];
        int sealedObjectCount = sealedObjects.Length;
        _output.WriteLine(
            $"first={first.Status}/{first.Code}; replay={replay.Status}/{replay.Code}; " +
            $"databaseRows={snapshots.Count}; sealedObjects={sealedObjectCount}; " +
            $"firstCompletedAt={first.CompletedAt:O}; replayCompletedAt={replay.CompletedAt:O}");

        Assert.Equal(first.ProfiledRevision, snapshot.ProfiledRevision);
        Assert.Equal(first.ManifestSha256, snapshot.ManifestSha256);
        Assert.Equal(first.EntryCount, snapshot.EntryCount);
        Assert.Equal(first.TotalBlobBytes, snapshot.TotalBlobBytes);
        string sealedObject = Assert.Single(sealedObjects);
        Assert.Equal(objectPath, sealedObject);
        Assert.True(Directory.Exists(objectPath));
        Assert.Equal(first.CompletedAt, replay.CompletedAt);
    }

    [Fact]
    public async Task AcquireAsync_InjectedPrePublishFailure_LeavesNoVisibleRowAndFreshRetryAdoptsSealedObjectAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        int authorizationCount = 0;
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configureServices: FailOnceBeforePublish(),
                authorizationWait: (_, _) =>
                {
                    Interlocked.Increment(ref authorizationCount);
                    return Task.CompletedTask;
                });

        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        string revision = await fixture.Repository.ResolveRevisionAsync();
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        SourceCaptureResult failedCapture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        List<SourceSnapshot> afterFailure =
            await LoadSnapshotsAsync(appFixture, fixture.RepositoryId);
        int sealedObjectsAfterFailure = CountSealedObjects(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revision);
        OpenSourceReadContextResult unavailable = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest(),
            testCancellation.Token);

        int authorizationsBeforeRetry = Volatile.Read(ref authorizationCount);
        SourceCaptureResult retry = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        List<SourceSnapshot> afterRetry =
            await LoadSnapshotsAsync(appFixture, fixture.RepositoryId);
        int sealedObjectsAfterRetry = CountSealedObjects(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revision);
        _output.WriteLine(
            $"failed={failedCapture.Status}/{failedCapture.Code}; " +
            $"unavailable={unavailable.Status}/{unavailable.Code}; " +
            $"retry={retry.Status}/{retry.Code}; " +
            $"rowsAfterFailure={afterFailure.Count}; " +
            $"objectsAfterFailure={sealedObjectsAfterFailure}; " +
            $"rowsAfterRetry={afterRetry.Count}; " +
            $"objectsAfterRetry={sealedObjectsAfterRetry}; " +
            $"authorizationsBeforeRetry={authorizationsBeforeRetry}; " +
            $"authorizationsAfterRetry={Volatile.Read(ref authorizationCount)}");

        Assert.Equal(SourceCaptureStatus.StorageFailure, failedCapture.Status);
        Assert.Equal("source_storage_unavailable", failedCapture.Code);
        Assert.Empty(afterFailure);
        Assert.Equal(1, sealedObjectsAfterFailure);
        Assert.Equal(SourceReadStatus.NotFoundOrUnavailable, unavailable.Status);
        Assert.Null(unavailable.Context);
        Assert.Equal(SourceCaptureStatus.Completed, retry.Status);
        Assert.Equal("source_completed", retry.Code);
        Assert.Single(afterRetry);
        Assert.Equal(1, sealedObjectsAfterRetry);
        Assert.Equal(revision, retry.ProfiledRevision);
        Assert.True(Volatile.Read(ref authorizationCount) > authorizationsBeforeRetry);
    }

    [Fact]
    public async Task AcquireAsync_AgedAbandonedOperation_IsRemovedAfterSuccessfulAdoptionAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        IReadOnlyDictionary<string, string?> configuration =
            new Dictionary<string, string?>
            {
                ["SourceStorage:OperationStaleAfterMinutes"] = "1",
            };
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: configuration);
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        string revision = await fixture.Repository.ResolveRevisionAsync();
        string markerPath = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "child-ready.marker");
        string sentinelPath = RetainedSourceOwnedPaths.ResolveChild(
            fixture.SourceStorageRoot,
            "unrelated-sentinel.txt");
        File.WriteAllText(sentinelPath, "preserve");

        ProcessStartInfo startInfo = RetainedSourceSeparateProcessRunner.CreateStartInfo(
            ResolveRepositoryRoot());
        startInfo.Environment[RetainedSourceSeparateProcessChild.ConnectionVariable] =
            appFixture.Database.ConnectionString;
        startInfo.Environment[RetainedSourceSeparateProcessChild.StorageRootVariable] =
            fixture.SourceStorageRoot;
        startInfo.Environment[RetainedSourceSeparateProcessChild.UserVariable] =
            appFixture.UserId;
        startInfo.Environment[RetainedSourceSeparateProcessChild.RepositoryVariable] =
            fixture.RepositoryId.ToString("D");
        startInfo.Environment[RetainedSourceSeparateProcessChild.RefVariable] = permittedRef;
        startInfo.Environment[RetainedSourceSeparateProcessChild.ReadyMarkerVariable] =
            markerPath;
        startInfo.Environment[RetainedSourceSeparateProcessChild.StaleMinutesVariable] = "1";

        using var child = new Process { StartInfo = startInfo };
        Assert.True(child.Start());
        Task<string> childOutput = child.StandardOutput.ReadToEndAsync();
        Task<string> childError = child.StandardError.ReadToEndAsync();
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await WaitForFileAsync(markerPath, testCancellation.Token);
            int markerProcessId = int.Parse(File.ReadAllText(markerPath));
            using Process testHost = Process.GetProcessById(markerProcessId);
            DateTime testHostStartTime = testHost.StartTime;

            string operationsRoot = Path.Combine(fixture.SourceStorageRoot, ".operations");
            string[] abandonedOperations = Directory.Exists(operationsRoot)
                ? Directory.GetDirectories(operationsRoot)
                : [];
            string abandonedOperation = Assert.Single(abandonedOperations);
            RetainedSourceOwnedPaths.EnsureOwnedPath(abandonedOperation);

            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(testCancellation.Token);
            Assert.NotEqual(0, child.ExitCode);
            await WaitForProcessExitAsync(testHost, testHostStartTime, testCancellation.Token);

            Directory.SetLastWriteTimeUtc(
                abandonedOperation,
                DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)));

            IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
                .GetRequiredService<IRetainedSourceApplication>();
            SourceCaptureResult capture = await application.AcquireAsync(
                fixture.RepositoryId,
                permittedRef,
                testCancellation.Token);
            List<SourceSnapshot> snapshots = await LoadSnapshotsAsync(
                appFixture,
                fixture.RepositoryId);
            string[] remainingOperations = Directory.Exists(operationsRoot)
                ? Directory.GetDirectories(operationsRoot)
                : [];
            int sealedObjectCount = CountSealedObjects(
                fixture.SourceStorageRoot,
                fixture.RepositoryId,
                revision);
            _output.WriteLine(
                $"childExit={child.ExitCode}; capture={capture.Status}/{capture.Code}; " +
                $"launcherPid={child.Id}; testHostPid={markerProcessId}; " +
                $"rows={snapshots.Count}; sealedObjects={sealedObjectCount}; " +
                $"remainingOperations={remainingOperations.Length}; " +
                $"sentinelExists={File.Exists(sentinelPath)}");

            Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
            Assert.Equal("source_completed", capture.Code);
            Assert.Single(snapshots);
            Assert.Equal(1, sealedObjectCount);
            Assert.True(File.Exists(sentinelPath));
            Assert.Empty(remainingOperations);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(CancellationToken.None);
            }

            string childStandardOutput = await childOutput;
            string childStandardError = await childError;
            _output.WriteLine(
                $"childExitObserved={child.HasExited}; childExitCode={child.ExitCode}; " +
                $"childOutputLength={childStandardOutput.Length}; " +
                $"childErrorLength={childStandardError.Length}");
            if (childStandardError.Length > 0)
            {
                _output.WriteLine(
                    childStandardError[^Math.Min(childStandardError.Length, 2_000)..]);
            }
        }
    }

    [Fact]
    public async Task AcquireAsync_AgedUnknownOperations_PreservesForeignStateAndSentinelsAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:OperationStaleAfterMinutes"] = "1",
                });

        string operationsRoot = Path.Combine(fixture.SourceStorageRoot, ".operations");
        Directory.CreateDirectory(operationsRoot);
        string unmarkedOperation = Path.Combine(
            operationsRoot,
            Guid.NewGuid().ToString("N"));
        string foreignOperation = Path.Combine(
            operationsRoot,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(unmarkedOperation);
        Directory.CreateDirectory(foreignOperation);
        RetainedSourceOwnedPaths.EnsureOwnedPath(unmarkedOperation);
        RetainedSourceOwnedPaths.EnsureOwnedPath(foreignOperation);
        string foreignLease = Path.Combine(foreignOperation, ".lease");
        string foreignSentinel = Path.Combine(foreignOperation, "foreign-sentinel.txt");
        File.WriteAllText(foreignLease, "foreign-lease-payload");
        File.WriteAllText(foreignSentinel, "preserve");
        DateTime staleAt = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5));
        Directory.SetLastWriteTimeUtc(unmarkedOperation, staleAt);
        Directory.SetLastWriteTimeUtc(foreignOperation, staleAt);

        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        string[] remainingOperations = Directory.Exists(operationsRoot)
            ? Directory.GetDirectories(operationsRoot)
            : [];
        _output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"remainingOperations={remainingOperations.Length}; " +
            $"unmarkedExists={Directory.Exists(unmarkedOperation)}; " +
            $"foreignExists={Directory.Exists(foreignOperation)}; " +
            $"foreignLeaseExists={File.Exists(foreignLease)}; " +
            $"foreignSentinelExists={File.Exists(foreignSentinel)}");

        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        Assert.True(Directory.Exists(unmarkedOperation));
        Assert.True(Directory.Exists(foreignOperation));
        Assert.True(File.Exists(foreignLease));
        Assert.True(File.Exists(foreignSentinel));
        Assert.Equal(2, remainingOperations.Length);
    }

    [Fact]
    public async Task AcquireAsync_WhileAnotherRepositoryHoldsLiveLease_PreservesLiveOperationAsync()
    {
        await using RetainedSourceFixture firstSource =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceFixture secondSource =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                firstSource,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:OperationStaleAfterMinutes"] = "1",
                });
        await AddStandaloneRepositoryAsync(
            appFixture.Database.ConnectionString,
            secondSource);

        string markerPath = RetainedSourceOwnedPaths.ResolveChild(
            firstSource.RootPath,
            "live-child-ready.marker");
        string permittedRef = await secondSource.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        ProcessStartInfo startInfo = RetainedSourceSeparateProcessRunner.CreateStartInfo(
            ResolveRepositoryRoot());
        startInfo.Environment[RetainedSourceSeparateProcessChild.ConnectionVariable] =
            appFixture.Database.ConnectionString;
        startInfo.Environment[RetainedSourceSeparateProcessChild.StorageRootVariable] =
            firstSource.SourceStorageRoot;
        startInfo.Environment[RetainedSourceSeparateProcessChild.UserVariable] =
            appFixture.UserId;
        startInfo.Environment[RetainedSourceSeparateProcessChild.RepositoryVariable] =
            firstSource.RepositoryId.ToString("D");
        startInfo.Environment[RetainedSourceSeparateProcessChild.RefVariable] =
            await firstSource.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        startInfo.Environment[RetainedSourceSeparateProcessChild.ReadyMarkerVariable] =
            markerPath;
        startInfo.Environment[RetainedSourceSeparateProcessChild.StaleMinutesVariable] = "1";

        using var child = new Process { StartInfo = startInfo };
        Assert.True(child.Start());
        Task<string> childOutput = child.StandardOutput.ReadToEndAsync();
        Task<string> childError = child.StandardError.ReadToEndAsync();
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await WaitForFileAsync(markerPath, testCancellation.Token);
            int markerProcessId = int.Parse(File.ReadAllText(markerPath));
            using Process testHost = Process.GetProcessById(markerProcessId);
            DateTime testHostStartTime = testHost.StartTime;
            string operationsRoot = Path.Combine(firstSource.SourceStorageRoot, ".operations");
            string[] liveOperations = Directory.Exists(operationsRoot)
                ? Directory.GetDirectories(operationsRoot)
                : [];
            string liveOperation = Assert.Single(liveOperations);
            string liveLease = Path.Combine(liveOperation, ".lease");

            IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
                .GetRequiredService<IRetainedSourceApplication>();
            SourceCaptureResult secondCapture = await application.AcquireAsync(
                secondSource.RepositoryId,
                permittedRef,
                testCancellation.Token);
            List<SourceSnapshot> snapshots = await LoadSnapshotsAsync(
                appFixture,
                secondSource.RepositoryId);
            string[] remainingOperations = Directory.Exists(operationsRoot)
                ? Directory.GetDirectories(operationsRoot)
                : [];
            _output.WriteLine(
                $"childLauncherPid={child.Id}; childTestHostPid={markerProcessId}; " +
                $"second={secondCapture.Status}/{secondCapture.Code}; rows={snapshots.Count}; " +
                $"liveOperationExists={Directory.Exists(liveOperation)}; " +
                $"liveLeaseExists={File.Exists(liveLease)}; " +
                $"remainingOperations={remainingOperations.Length}");

            Assert.Equal(SourceCaptureStatus.Completed, secondCapture.Status);
            Assert.Single(snapshots);
            Assert.True(Directory.Exists(liveOperation));
            Assert.True(File.Exists(liveLease));
            Assert.Contains(liveOperation, remainingOperations, StringComparer.OrdinalIgnoreCase);

            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(testCancellation.Token);
            Assert.NotEqual(0, child.ExitCode);
            await WaitForProcessExitAsync(testHost, testHostStartTime, testCancellation.Token);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(CancellationToken.None);
            }

            _ = await childOutput;
            _ = await childError;
        }
    }

    [Fact]
    public async Task AcquireAsync_WhenOperationsRootIsOwnedReparsePoint_FailsWithoutOutsideWritesAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true);

        string operationsRoot = Path.Combine(fixture.SourceStorageRoot, ".operations");
        string reparseTarget = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "operations-reparse-target");
        Directory.CreateDirectory(reparseTarget);
        string targetSentinel = Path.Combine(reparseTarget, "outside-sentinel.txt");
        File.WriteAllText(targetSentinel, "preserve");
        Directory.CreateSymbolicLink(operationsRoot, reparseTarget);

        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        List<SourceSnapshot> snapshots = await LoadSnapshotsAsync(
            appFixture,
            fixture.RepositoryId);
        string[] outsideEntries = Directory.GetFileSystemEntries(reparseTarget)
            .Where(path => !StringComparer.OrdinalIgnoreCase.Equals(path, targetSentinel))
            .ToArray();
        _output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; rows={snapshots.Count}; " +
            $"outsideWrites={outsideEntries.Length}; " +
            $"operationsReparse={File.GetAttributes(operationsRoot).HasFlag(FileAttributes.ReparsePoint)}; " +
            $"sentinelExists={File.Exists(targetSentinel)}");

        Assert.Equal(SourceCaptureStatus.StorageFailure, capture.Status);
        Assert.Equal("source_storage_unavailable", capture.Code);
        Assert.Empty(snapshots);
        Assert.Empty(outsideEntries);
        Assert.True(File.Exists(targetSentinel));
    }

    [Fact]
    public async Task AcquireAsync_AgedUnreferencedSealedObject_RemovesOnlyUnreferencedRevisionAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:OperationStaleAfterMinutes"] = "1",
                },
                configureServices: FailOnPublishNumber(2));

        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        SourceCaptureResult revisionA = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        string revisionAId = await fixture.Repository.ResolveRevisionAsync();

        fixture.Repository.WriteText(
            "src/retained-source.txt",
            "revision-b\n");
        await fixture.Repository.CommitAsync("retained-source revision B");
        string revisionBId = await fixture.Repository.ResolveRevisionAsync();
        SourceCaptureResult revisionB = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        string[] revisionBObjects = GetSealedObjectPaths(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revisionBId);
        Assert.Single(revisionBObjects);
        Directory.SetLastWriteTimeUtc(
            revisionBObjects[0],
            DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)));
        Directory.SetLastWriteTimeUtc(
            GetSingleSealedObjectPath(fixture.SourceStorageRoot, fixture.RepositoryId, revisionAId),
            DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)));

        fixture.Repository.WriteText(
            "src/retained-source.txt",
            "revision-c\n");
        await fixture.Repository.CommitAsync("retained-source revision C");
        string revisionCId = await fixture.Repository.ResolveRevisionAsync();
        SourceCaptureResult revisionC = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        List<SourceSnapshot> snapshots = await LoadSnapshotsAsync(
            appFixture,
            fixture.RepositoryId);
        int revisionAObjects = CountSealedObjects(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revisionAId);
        int revisionBObjectsAfterCleanup = CountSealedObjects(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revisionBId);
        int revisionCObjects = CountSealedObjects(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revisionCId);
        OpenSourceReadContextResult reopenedA = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revisionAId,
            new SourceReadGrantRequest(),
            testCancellation.Token);
        SourceTextResult sourceA = await reopenedA.Context!.ReadTextAsync(
            "src/retained-source.txt",
            0,
            1_024,
            testCancellation.Token);
        _output.WriteLine(
            $"A={revisionA.Status}/{revisionA.Code}; " +
            $"B={revisionB.Status}/{revisionB.Code}; " +
            $"C={revisionC.Status}/{revisionC.Code}; " +
            $"rows={snapshots.Count}; AObjects={revisionAObjects}; " +
            $"BObjectsAfterCleanup={revisionBObjectsAfterCleanup}; " +
            $"CObjects={revisionCObjects}; " +
            $"reopenA={reopenedA.Status}/{reopenedA.Code}; " +
            $"readA={sourceA.Status}/{sourceA.Code}");
        await reopenedA.Context.DisposeAsync();

        Assert.Equal(SourceCaptureStatus.Completed, revisionA.Status);
        Assert.Equal(SourceCaptureStatus.StorageFailure, revisionB.Status);
        Assert.Equal("source_storage_unavailable", revisionB.Code);
        Assert.Equal(SourceCaptureStatus.Completed, revisionC.Status);
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(1, revisionAObjects);
        Assert.Equal(0, revisionBObjectsAfterCleanup);
        Assert.Equal(1, revisionCObjects);
        Assert.Equal(SourceReadStatus.Succeeded, reopenedA.Status);
        Assert.Equal(SourceReadStatus.Succeeded, sourceA.Status);
        Assert.Equal("retained-source-fixture\n", sourceA.Content);
    }

    [Fact]
    public async Task AcquireAsync_YoungerUnreferencedSealedObject_PreservesObjectWithinGraceAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:OperationStaleAfterMinutes"] = "1",
                },
                configureServices: FailOnPublishNumber(1));

        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        SourceCaptureResult revisionB = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        string revisionBId = await fixture.Repository.ResolveRevisionAsync();
        string revisionBObject = GetSingleSealedObjectPath(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revisionBId);
        Directory.SetLastWriteTimeUtc(
            revisionBObject,
            DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(5)));

        fixture.Repository.WriteText(
            "src/retained-source.txt",
            "revision-c\n");
        await fixture.Repository.CommitAsync("retained-source revision C");
        string revisionCId = await fixture.Repository.ResolveRevisionAsync();
        SourceCaptureResult revisionC = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        List<SourceSnapshot> snapshots = await LoadSnapshotsAsync(
            appFixture,
            fixture.RepositoryId);
        int revisionBObjects = CountSealedObjects(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revisionBId);
        int revisionCObjects = CountSealedObjects(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revisionCId);
        _output.WriteLine(
            $"B={revisionB.Status}/{revisionB.Code}; C={revisionC.Status}/{revisionC.Code}; " +
            $"rows={snapshots.Count}; BObjectsWithinGrace={revisionBObjects}; " +
            $"CObjects={revisionCObjects}");

        Assert.Equal(SourceCaptureStatus.StorageFailure, revisionB.Status);
        Assert.Equal(SourceCaptureStatus.Completed, revisionC.Status);
        Assert.Equal(1, snapshots.Count);
        Assert.Equal(1, revisionBObjects);
        Assert.Equal(1, revisionCObjects);
    }

    [Fact]
    public async Task AcquireAsync_AgedMalformedManifestObject_PreservesUnknownOwnedStateAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:OperationStaleAfterMinutes"] = "1",
                });

        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult revisionA = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        string revisionAId = await fixture.Repository.ResolveRevisionAsync();

        string unknownRevision = new('a', 40);
        string unknownManifest = new('b', 64);
        string unknownObject = Path.Combine(
            fixture.SourceStorageRoot,
            "snapshots",
            fixture.RepositoryId.ToString("N"),
            unknownRevision,
            unknownManifest);
        Directory.CreateDirectory(unknownObject);
        File.WriteAllText(Path.Combine(unknownObject, "manifest.json"), "{ malformed");
        Directory.SetLastWriteTimeUtc(
            unknownObject,
            DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)));

        fixture.Repository.WriteText(
            "src/retained-source.txt",
            "revision-c\n");
        await fixture.Repository.CommitAsync("retained-source revision C");
        string revisionCId = await fixture.Repository.ResolveRevisionAsync();
        SourceCaptureResult revisionC = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        List<SourceSnapshot> snapshots = await LoadSnapshotsAsync(
            appFixture,
            fixture.RepositoryId);
        int revisionAObjects = CountSealedObjects(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revisionAId);
        int revisionCObjects = CountSealedObjects(
            fixture.SourceStorageRoot,
            fixture.RepositoryId,
            revisionCId);
        _output.WriteLine(
            $"A={revisionA.Status}/{revisionA.Code}; C={revisionC.Status}/{revisionC.Code}; " +
            $"rows={snapshots.Count}; AObjects={revisionAObjects}; " +
            $"CObjects={revisionCObjects}; unknownExists={Directory.Exists(unknownObject)}; " +
            $"unknownManifestExists={File.Exists(Path.Combine(unknownObject, "manifest.json"))}");

        Assert.Equal(SourceCaptureStatus.Completed, revisionA.Status);
        Assert.Equal(SourceCaptureStatus.Completed, revisionC.Status);
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(1, revisionAObjects);
        Assert.Equal(1, revisionCObjects);
        Assert.True(Directory.Exists(unknownObject));
        Assert.True(File.Exists(Path.Combine(unknownObject, "manifest.json")));
    }

    [Fact]
    public async Task AcquireAsync_AgedSealedObjectWithBusyIdentityLock_PreservesUntilLockReleasedAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:OperationStaleAfterMinutes"] = "1",
                });
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult revisionA = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);

        fixture.Repository.WriteText(
            "src/retained-source.txt",
            "revision-b\n");
        await fixture.Repository.CommitAsync("retained-source revision B");
        string revisionBId = await fixture.Repository.ResolveRevisionAsync();
        string markerPath = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "busy-child-ready.marker");
        ProcessStartInfo startInfo = RetainedSourceSeparateProcessRunner.CreateStartInfo(
            ResolveRepositoryRoot());
        startInfo.Environment[RetainedSourceSeparateProcessChild.ConnectionVariable] =
            appFixture.Database.ConnectionString;
        startInfo.Environment[RetainedSourceSeparateProcessChild.StorageRootVariable] =
            fixture.SourceStorageRoot;
        startInfo.Environment[RetainedSourceSeparateProcessChild.UserVariable] =
            appFixture.UserId;
        startInfo.Environment[RetainedSourceSeparateProcessChild.RepositoryVariable] =
            fixture.RepositoryId.ToString("D");
        startInfo.Environment[RetainedSourceSeparateProcessChild.RefVariable] = permittedRef;
        startInfo.Environment[RetainedSourceSeparateProcessChild.ReadyMarkerVariable] =
            markerPath;
        startInfo.Environment[RetainedSourceSeparateProcessChild.StaleMinutesVariable] = "1";

        using var child = new Process { StartInfo = startInfo };
        Assert.True(child.Start());
        Task<string> childOutput = child.StandardOutput.ReadToEndAsync();
        Task<string> childError = child.StandardError.ReadToEndAsync();
        try
        {
            await WaitForFileAsync(markerPath, testCancellation.Token);
            int markerProcessId = int.Parse(File.ReadAllText(markerPath));
            using Process testHost = Process.GetProcessById(markerProcessId);
            DateTime testHostStartTime = testHost.StartTime;
            string revisionBObject = GetSingleSealedObjectPath(
                fixture.SourceStorageRoot,
                fixture.RepositoryId,
                revisionBId);
            Directory.SetLastWriteTimeUtc(
                revisionBObject,
                DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)));
            string lockPath = Path.Combine(
                fixture.SourceStorageRoot,
                ".locks",
                $"{fixture.RepositoryId:N}-{revisionBId}.lock");
            Assert.True(File.Exists(lockPath));

            fixture.Repository.WriteText(
                "src/retained-source.txt",
                "revision-c\n");
            await fixture.Repository.CommitAsync("retained-source revision C");
            string revisionCId = await fixture.Repository.ResolveRevisionAsync();
            SourceCaptureResult revisionC = await application.AcquireAsync(
                fixture.RepositoryId,
                permittedRef,
                testCancellation.Token);
            int revisionBObjectsWhileBusy = CountSealedObjects(
                fixture.SourceStorageRoot,
                fixture.RepositoryId,
                revisionBId);

            Assert.False(child.HasExited);
            Assert.Equal(SourceCaptureStatus.Completed, revisionC.Status);
            Assert.Equal(1, revisionBObjectsWhileBusy);

            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(testCancellation.Token);
            Assert.NotEqual(0, child.ExitCode);
            await WaitForProcessExitAsync(testHost, testHostStartTime, testCancellation.Token);

            SourceCaptureResult replayC = await application.AcquireAsync(
                fixture.RepositoryId,
                permittedRef,
                testCancellation.Token);
            List<SourceSnapshot> snapshots = await LoadSnapshotsAsync(
                appFixture,
                fixture.RepositoryId);
            int revisionBObjectsAfterRelease = CountSealedObjects(
                fixture.SourceStorageRoot,
                fixture.RepositoryId,
                revisionBId);
            int revisionCObjects = CountSealedObjects(
                fixture.SourceStorageRoot,
                fixture.RepositoryId,
                revisionCId);
            _output.WriteLine(
                $"A={revisionA.Status}/{revisionA.Code}; C={revisionC.Status}/{revisionC.Code}; " +
                $"replayC={replayC.Status}/{replayC.Code}; " +
                $"BObjectsWhileBusy={revisionBObjectsWhileBusy}; " +
                $"BObjectsAfterRelease={revisionBObjectsAfterRelease}; " +
                $"CObjects={revisionCObjects}; rows={snapshots.Count}; " +
                $"childExit={child.ExitCode}");

            Assert.Equal(SourceCaptureStatus.Completed, revisionA.Status);
            Assert.Equal(SourceCaptureStatus.Completed, revisionC.Status);
            Assert.Equal(SourceCaptureStatus.Replay, replayC.Status);
            Assert.Equal(2, snapshots.Count);
            Assert.Equal(0, revisionBObjectsAfterRelease);
            Assert.Equal(1, revisionCObjects);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(CancellationToken.None);
            }

            _ = await childOutput;
            _ = await childError;
        }
    }

    [Fact]
    public async Task AcquireAsync_DifferentRepositories_ConcurrentPublicationReturnsCompletedAndOneRowEachAsync()
    {
        await using RetainedSourceFixture firstSource =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceFixture secondSource =
            await RetainedSourceFixture.CreateAsync();
        var barrier = new FinalAuthorizationBarrier(
            new HashSet<Guid> { firstSource.RepositoryId, secondSource.RepositoryId });
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                firstSource,
                standalone: true,
                authorizationWait: barrier.WaitAsync);
        await AddStandaloneRepositoryAsync(
            appFixture.Database.ConnectionString,
            secondSource);

        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        Task<SourceCaptureResult> firstCapture = application.AcquireAsync(
            firstSource.RepositoryId,
            await firstSource.Repository.RunGitAsync(["symbolic-ref", "HEAD"]),
            testCancellation.Token);
        Task<SourceCaptureResult> secondCapture = application.AcquireAsync(
            secondSource.RepositoryId,
            await secondSource.Repository.RunGitAsync(["symbolic-ref", "HEAD"]),
            testCancellation.Token);
        SourceCaptureResult[] results = await Task.WhenAll(firstCapture, secondCapture);

        IDbContextFactory<VulgataDbContext> contextFactory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await contextFactory.CreateDbContextAsync();
        List<SourceSnapshot> snapshots = await context.SourceSnapshots
            .Where(snapshot =>
                snapshot.RepositoryId == firstSource.RepositoryId ||
                snapshot.RepositoryId == secondSource.RepositoryId)
            .ToListAsync();
        int sealedObjectCount = snapshots.Count(snapshot =>
            Directory.Exists(Path.Combine(
                firstSource.SourceStorageRoot,
                snapshot.InternalStorageKey.Replace('/', Path.DirectorySeparatorChar))));
        _output.WriteLine(
            $"first={results[0].Status}/{results[0].Code}; " +
            $"second={results[1].Status}/{results[1].Code}; " +
            $"databaseRows={snapshots.Count}; sealedObjects={sealedObjectCount}");

        Assert.All(results, result => Assert.Equal(SourceCaptureStatus.Completed, result.Status));
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(2, sealedObjectCount);
        Assert.Contains(snapshots, snapshot => snapshot.RepositoryId == firstSource.RepositoryId);
        Assert.Contains(snapshots, snapshot => snapshot.RepositoryId == secondSource.RepositoryId);
    }

    private static async Task AddStandaloneRepositoryAsync(
        string connectionString,
        RetainedSourceFixture source)
    {
        DbContextOptionsBuilder<VulgataDbContext> options = new();
        options.UseNpgsql(connectionString);
        await using var context = new VulgataDbContext(options.Options);
        Repository repository = Repository.CreateStandalone(
            "retained-source-second-standalone",
            source.Repository.RootPath,
            null,
            null,
            DateTimeOffset.UtcNow);
        typeof(Repository).GetProperty(
            nameof(Repository.Id),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .SetValue(repository, source.RepositoryId);
        context.Repositories.Add(repository);
        await context.SaveChangesAsync();
    }

    private static async Task<List<SourceSnapshot>> LoadSnapshotsAsync(
        RetainedSourceCaptureApplicationFixture appFixture,
        Guid repositoryId)
    {
        IDbContextFactory<VulgataDbContext> contextFactory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await contextFactory.CreateDbContextAsync();
        return await context.SourceSnapshots
            .Where(snapshot => snapshot.RepositoryId == repositoryId)
            .ToListAsync();
    }

    private static int CountSealedObjects(
        string sourceStorageRoot,
        Guid repositoryId,
        string profiledRevision)
    {
        string identityRoot = Path.Combine(
            sourceStorageRoot,
            "snapshots",
            repositoryId.ToString("N"),
            profiledRevision);
        return Directory.Exists(identityRoot)
            ? Directory.GetDirectories(identityRoot).Length
            : 0;
    }

    private static string[] GetSealedObjectPaths(
        string sourceStorageRoot,
        Guid repositoryId,
        string profiledRevision)
    {
        string identityRoot = Path.Combine(
            sourceStorageRoot,
            "snapshots",
            repositoryId.ToString("N"),
            profiledRevision);
        return Directory.Exists(identityRoot)
            ? Directory.GetDirectories(identityRoot)
            : [];
    }

    private static string GetSingleSealedObjectPath(
        string sourceStorageRoot,
        Guid repositoryId,
        string profiledRevision) =>
        Assert.Single(GetSealedObjectPaths(sourceStorageRoot, repositoryId, profiledRevision));

    private static async Task WaitForFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        while (!File.Exists(path))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static async Task WaitForProcessExitAsync(
        Process process,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (process.HasExited)
            {
                return;
            }

            try
            {
                if (process.StartTime != startTime)
                {
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static string ResolveRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, RetainedSourceSeparateProcessRunner.ProjectPath)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "The retained-source test repository root could not be resolved.");
    }

    private static Action<IServiceCollection> FailOnceBeforePublish()
        => FailOnPublishNumber(1);

    private static Action<IServiceCollection> FailOnPublishNumber(int failureNumber)
    {
        return services =>
        {
            ServiceDescriptor descriptor = services.Last(candidate =>
                candidate.ServiceType == typeof(IRetainedSourceRepository));
            services.Remove(descriptor);
            services.AddScoped<IRetainedSourceRepository>(serviceProvider =>
                new FailAtPublishNumberRetainedSourceRepository(
                    ResolveRepository(serviceProvider, descriptor),
                    failureNumber));
        };
    }

    private static IRetainedSourceRepository ResolveRepository(
        IServiceProvider services,
        ServiceDescriptor descriptor)
    {
        object implementation = descriptor.ImplementationInstance ??
            descriptor.ImplementationFactory?.Invoke(services) ??
            (descriptor.ImplementationType is not null
                ? ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType)
                : throw new InvalidOperationException(
                    "The retained-source repository registration has no implementation."));
        return (IRetainedSourceRepository)implementation;
    }

    private sealed class FailAtPublishNumberRetainedSourceRepository(
        IRetainedSourceRepository inner,
        int failureNumber) : IRetainedSourceRepository
    {
        private int _publishCount;

        public Task<bool> HasForRepositoryAsync(
            Guid repositoryId,
            CancellationToken cancellationToken = default) =>
            inner.HasForRepositoryAsync(repositoryId, cancellationToken);

        public Task<SourceSnapshot?> GetAsync(
            Guid repositoryId,
            string profiledRevision,
            CancellationToken cancellationToken = default) =>
            inner.GetAsync(repositoryId, profiledRevision, cancellationToken);

        public Task PublishAsync(
            SourceSnapshot sourceSnapshot,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _publishCount) == failureNumber)
            {
                throw new InvalidOperationException("Injected pre-row publication failure.");
            }

            return inner.PublishAsync(sourceSnapshot, cancellationToken);
        }
    }

    private sealed class FinalAuthorizationBarrier(HashSet<Guid> repositoryIds)
    {
        private readonly object _gate = new();
        private readonly HashSet<Guid> _revisionAuthorizations = [];
        private readonly HashSet<Guid> _finalAuthorizations = [];
        private readonly TaskCompletionSource _allRevisionAuthorizations =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _allFinalAuthorizations =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task WaitAsync(
            SourceAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            if (request.ProfiledRevision is null)
            {
                return Task.CompletedTask;
            }

            Task release;
            lock (_gate)
            {
                if (_revisionAuthorizations.Add(request.RepositoryId))
                {
                    if (_revisionAuthorizations.SetEquals(repositoryIds))
                    {
                        _allRevisionAuthorizations.TrySetResult();
                    }

                    release = _allRevisionAuthorizations.Task;
                }
                else
                {
                    _finalAuthorizations.Add(request.RepositoryId);
                    if (_finalAuthorizations.SetEquals(repositoryIds))
                    {
                        _allFinalAuthorizations.TrySetResult();
                    }

                    release = _allFinalAuthorizations.Task;
                }
            }

            return release.WaitAsync(cancellationToken);
        }
    }

    private sealed class FixedRetainedSourceTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override long GetTimestamp() => Stopwatch.GetTimestamp();
    }
}

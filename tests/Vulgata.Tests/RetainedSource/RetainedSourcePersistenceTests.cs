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

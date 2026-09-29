using Vulgata.Tests.TestInfrastructure;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourceCaptureBoundaryTests
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_StalledLoopbackGit_StopsAtConfiguredDeadlineBeforeWatchdog()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        await using var endpoint = new StalledGitHttpEndpoint();
        using var cancellation = new CancellationTokenSource();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:CaptureDeadlineSeconds"] = "1",
                },
                mutateBeforeComposition: connectionString =>
                    RetainedSourceCaptureApplicationFixture.SetOriginAsync(
                        connectionString,
                        fixture.RepositoryId,
                        endpoint.Origin));

        IRetainedSourceApplication application =
            appFixture.Scope.ServiceProvider.GetRequiredService<IRetainedSourceApplication>();
        Stopwatch stopwatch = Stopwatch.StartNew();
        Task<SourceCaptureResult> capture = application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            cancellation.Token);
        Task watchdog = Task.Delay(TimeSpan.FromSeconds(10));
        Task winner = await Task.WhenAny(capture, watchdog);
        if (winner != capture)
        {
            cancellation.Cancel();
        }

        SourceCaptureResult result = await capture.WaitAsync(TimeSpan.FromSeconds(3));
        stopwatch.Stop();

        Assert.True(endpoint.RequestObserved);
        Assert.Null(result.ManifestSha256);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15));

        await using VulgataDbContext context = CreateContext(appFixture.Database.ConnectionString);
        Assert.False(await context.Set<SourceSnapshot>()
            .AnyAsync(snapshot => snapshot.RepositoryId == fixture.RepositoryId));
        Assert.Equal(SourceCaptureStatus.DeadlineExceeded, result.Status);
        Assert.Equal("source_deadline_exceeded", result.Code);
        Assert.Same(capture, winner);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_RevokedSystemOwnerWhileIdentityLockIsHeld_DeniesWithoutNewRow()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        string revision = await fixture.Repository.ResolveRevisionAsync();
        string locksRoot = Path.Combine(fixture.SourceStorageRoot, ".locks");
        Directory.CreateDirectory(locksRoot);
        string lockPath = Path.Combine(locksRoot, $"{fixture.RepositoryId:N}-{revision}.lock");
        await using FileStream identityLock = new(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            1,
            FileOptions.Asynchronous);
        var mutationReached = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int mutationCount = 0;
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                administrator: false,
                systemOwner: true,
                mutateAfterAuthorizationCount: 2,
                mutateAfterFirstAuthorization: connectionString =>
                {
                    Interlocked.Increment(ref mutationCount);
                    return RevokeAndSignalAsync(
                        connectionString,
                        appFixtureSystemId: Guid.Parse("13000000-0000-4000-8000-000000000201"),
                        mutationReached);
                });

        Task<SourceCaptureResult> capture = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(fixture.RepositoryId, permittedRef);
        await mutationReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task observationWindow = Task.Delay(TimeSpan.FromMilliseconds(250));
        Task winner = await Task.WhenAny(capture, observationWindow);
        Assert.NotSame(capture, winner);

        await identityLock.DisposeAsync();
        SourceCaptureResult result = await capture.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, mutationCount);
        await using VulgataDbContext context = CreateContext(appFixture.Database.ConnectionString);
        bool newRowVisible = await context.Set<SourceSnapshot>()
            .AnyAsync(snapshot => snapshot.RepositoryId == fixture.RepositoryId);
        Assert.True(
            result.Status == SourceCaptureStatus.Denied && !newRowVisible,
            $"Expected Denied with no new row; actual status={result.Status}, rowVisible={newRowVisible}.");
        Assert.Equal("source_denied", result.Code);
        Assert.Null(result.ManifestSha256);

        static async Task RevokeAndSignalAsync(
            string connectionString,
            Guid appFixtureSystemId,
            TaskCompletionSource reached)
        {
            await RetainedSourceCaptureApplicationFixture.RevokeSystemAssignmentAsync(
                connectionString,
                appFixtureSystemId);
            reached.TrySetResult();
        }
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_CancelledAtFinalPublicationAuthorization_ReturnsCancelledWithoutRow()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        int mutationCount = 0;
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                mutateAfterAuthorizationCount: 3,
                mutateAfterFirstAuthorization: _ =>
                {
                    Interlocked.Increment(ref mutationCount);
                    cancellation.Cancel();
                    return Task.CompletedTask;
                });

        SourceCaptureResult result = await appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>()
            .AcquireAsync(
                fixture.RepositoryId,
                await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]),
                cancellation.Token);

        Assert.Equal(1, mutationCount);
        Assert.Equal(SourceCaptureStatus.Cancelled, result.Status);
        Assert.Equal("source_cancelled", result.Code);
        Assert.Null(result.ManifestSha256);
        await using VulgataDbContext context = CreateContext(appFixture.Database.ConnectionString);
        Assert.False(await context.Set<SourceSnapshot>()
            .AnyAsync(snapshot => snapshot.RepositoryId == fixture.RepositoryId));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Create_FilesystemRootWithContainedExcludedRoot_RejectsOverlap()
    {
        string filesystemRoot = Path.GetPathRoot(Path.GetTempPath()) ??
            throw new InvalidOperationException("The test host has no filesystem root.");
        IConfiguration configuration = Configuration(("RootPath", filesystemRoot));
        string excludedRoot = Path.Combine(filesystemRoot, "CodeGraph");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RetainedSourceCapturePolicy.Create(configuration, Path.GetTempPath(), excludedRoot));

        Assert.StartsWith("source_configuration_invalid:", exception.Message, StringComparison.Ordinal);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void Create_FilesystemRoot_RetainsFullyQualifiedRoot()
    {
        string filesystemRoot = Path.GetPathRoot(Path.GetTempPath()) ??
            throw new InvalidOperationException("The test host has no filesystem root.");
        IConfiguration configuration = Configuration(
            ("RootPath", filesystemRoot));

        RetainedSourceCapturePolicy policy = RetainedSourceCapturePolicy.Create(
            configuration,
            Path.GetTempPath());

        Assert.True(Path.IsPathRooted(policy.RootPath));
        Assert.Equal(
            Path.GetFullPath(filesystemRoot),
            policy.RootPath);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteTheory("PostgreSql")]
    [InlineData("CaptureDeadlineSeconds")]
    [InlineData("OperationStaleAfterMinutes")]
    public void Create_Int64TimeSpanOverflow_ReturnsSanitizedConfigurationFailure(
        string setting)
    {
        IConfiguration configuration = Configuration(
            (setting, long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            RetainedSourceCapturePolicy.Create(configuration, Path.GetTempPath()));

        Assert.StartsWith("source_configuration_invalid:", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SourceStorage:RootPath"] = Path.Combine(Path.GetTempPath(), "retained-source-boundary"),
            ["SourceStorage:MaximumEntries"] = "10000",
            ["SourceStorage:MaximumBlobBytes"] = "8388608",
            ["SourceStorage:MaximumAggregateBlobBytes"] = "67108864",
            ["SourceStorage:CaptureDeadlineSeconds"] = "600",
            ["SourceStorage:OperationStaleAfterMinutes"] = "1440",
            ["SourceStorage:VerificationBufferBytes"] = "65536",
        };
        foreach ((string key, string value) in values)
        {
            settings[$"SourceStorage:{key}"] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static VulgataDbContext CreateContext(string connectionString)
    {
        DbContextOptionsBuilder<VulgataDbContext> options = new();
        options.UseNpgsql(connectionString);
        return new VulgataDbContext(options.Options);
    }

    private sealed class StalledGitHttpEndpoint : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly TaskCompletionSource _requestObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _acceptTask;

        internal StalledGitHttpEndpoint()
        {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Origin = $"http://127.0.0.1:{port}/stalled.git";
            _acceptTask = AcceptAndStallAsync();
        }

        internal string Origin { get; }

        internal bool RequestObserved => _requestObserved.Task.IsCompletedSuccessfully;

        public async ValueTask DisposeAsync()
        {
            _lifetime.Cancel();
            _release.TrySetResult();
            _listener.Stop();
            try
            {
                await _acceptTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }
            finally
            {
                _lifetime.Dispose();
                _listener.Dispose();
            }
        }

        private async Task AcceptAndStallAsync()
        {
            try
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(_lifetime.Token);
                _requestObserved.TrySetResult();
                await _release.Task.WaitAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }
        }
    }
}

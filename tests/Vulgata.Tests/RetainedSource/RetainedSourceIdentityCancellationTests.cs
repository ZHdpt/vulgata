using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Web.Data;

namespace Vulgata.Tests.RetainedSource;

[Collection(RetainedSourceProcessGlobalCollection.Name)]
public sealed class RetainedSourceIdentityCancellationTests
{
    [Fact]
    public async Task ListAsync_WhenReadDeadlineCancelsProductionIdentityLookup_ReturnsDeadlineExceeded()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture captureApplication =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        IRetainedSourceApplication capture = captureApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult captured = await capture.AcquireAsync(
            fixture.RepositoryId,
            await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        Assert.Equal(SourceCaptureStatus.Completed, captured.Status);
        string revision = captured.ProfiledRevision ??
                          throw new InvalidOperationException("The source capture did not return a revision.");

        var probe = new IdentityLookupCancellationProbe();
        await using RetainedSourceAttachedApplication attached =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                captureApplication.Database.ConnectionString,
                fixture.SourceStorageRoot,
                captureApplication.UserId,
                configureServices: services => ConfigureProbe(services, probe));
        IRetainedSourceApplication application = attached.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();

        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest(deadline: TimeSpan.FromSeconds(1)));
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        Assert.NotNull(opened.Context);
        await using ISourceReadContext context = opened.Context;

        probe.EnableBlocking();
        Task<SourceListResult> operation = context.ListAsync("src", 10, null);
        SourceListResult listed;
        try
        {
            await probe.LookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await probe.CancellationObservedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
            listed = await operation.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            probe.Release();
            try
            {
                await operation.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
            }
        }

        Assert.True(probe.LookupStarted.Task.IsCompletedSuccessfully);
        Assert.True(probe.ReceivedCancellableToken);
        Assert.True(probe.CancellationObserved);
        Assert.Equal(SourceReadStatus.DeadlineExceeded, listed.Status);
        Assert.Equal("source_deadline_exceeded", listed.Code);
        Assert.Equal(fixture.RepositoryId, listed.RepositoryId);
        Assert.Equal(revision, listed.ProfiledRevision);
        Assert.Null(listed.ManifestSha256);
        Assert.Empty(listed.Entries);
        Assert.Empty(listed.Limitations);
        Assert.Null(listed.NextCursor);
    }

    [Fact]
    public async Task ListAsync_WhenReadDeadlineCancelsProductionIdentityRoleLookup_ReturnsDeadlineExceeded()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture captureApplication =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        IRetainedSourceApplication capture = captureApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult captured = await capture.AcquireAsync(
            fixture.RepositoryId,
            await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        Assert.Equal(SourceCaptureStatus.Completed, captured.Status);
        string revision = captured.ProfiledRevision ??
                          throw new InvalidOperationException("The source capture did not return a revision.");

        var probe = new IdentityLookupCancellationProbe("AspNetUserRoles");
        await using RetainedSourceAttachedApplication attached =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                captureApplication.Database.ConnectionString,
                fixture.SourceStorageRoot,
                captureApplication.UserId,
                configureServices: services => ConfigureProbe(services, probe));
        IRetainedSourceApplication application = attached.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();

        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest(deadline: TimeSpan.FromSeconds(1)));
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        Assert.NotNull(opened.Context);
        await using ISourceReadContext context = opened.Context;

        probe.EnableBlocking();
        Task<SourceListResult> operation = context.ListAsync("src", 10, null);
        SourceListResult listed;
        try
        {
            await probe.LookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await probe.CancellationObservedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
            listed = await operation.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            probe.Release();
            try
            {
                await operation.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
            }
        }

        Assert.True(probe.LookupStarted.Task.IsCompletedSuccessfully);
        Assert.True(probe.ReceivedCancellableToken);
        Assert.True(probe.CancellationObserved);
        Assert.Equal(SourceReadStatus.DeadlineExceeded, listed.Status);
        Assert.Equal("source_deadline_exceeded", listed.Code);
        Assert.Equal(fixture.RepositoryId, listed.RepositoryId);
        Assert.Equal(revision, listed.ProfiledRevision);
        Assert.Null(listed.ManifestSha256);
        Assert.Empty(listed.Entries);
        Assert.Empty(listed.Limitations);
        Assert.Null(listed.NextCursor);
    }

    [Fact]
    public async Task ListAsync_WhenProductionIdentityLookupIsCancelled_ReturnsBoundedCancellation()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture captureApplication =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(fixture);

        IRetainedSourceApplication capture = captureApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult captured = await capture.AcquireAsync(
            fixture.RepositoryId,
            await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]));
        Assert.Equal(SourceCaptureStatus.Completed, captured.Status);
        string revision = captured.ProfiledRevision ??
                          throw new InvalidOperationException("The source capture did not return a revision.");

        var probe = new IdentityLookupCancellationProbe();
        await using RetainedSourceAttachedApplication attached =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                captureApplication.Database.ConnectionString,
                fixture.SourceStorageRoot,
                captureApplication.UserId,
                configureServices: services => ConfigureProbe(services, probe));
        IRetainedSourceApplication application = attached.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();

        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        Assert.NotNull(opened.Context);
        await using ISourceReadContext context = opened.Context;

        using var cancellation = new CancellationTokenSource();
        probe.EnableBlocking();
        Task<SourceListResult> operation = context.ListAsync(
            "src",
            10,
            null,
            cancellation.Token);
        SourceListResult? listed = null;
        Exception? propagated = null;
        bool completedWithinBound = false;
        try
        {
            await probe.LookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            Task completed = await Task.WhenAny(
                operation,
                Task.Delay(TimeSpan.FromSeconds(2)));
            completedWithinBound = ReferenceEquals(operation, completed);
            if (completedWithinBound)
            {
                listed = await operation;
            }
        }
        catch (Exception exception)
        {
            propagated = exception;
        }
        finally
        {
            probe.Release();
            try
            {
                await operation.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
            }
        }

        Assert.Null(propagated);
        Assert.True(probe.LookupStarted.Task.IsCompletedSuccessfully);
        Assert.True(probe.ReceivedCancellableToken);
        Assert.True(probe.CancellationObserved);
        Assert.True(completedWithinBound);
        Assert.NotNull(listed);
        Assert.Equal(SourceReadStatus.Cancelled, listed!.Status);
        Assert.Equal("source_cancelled", listed.Code);
        Assert.Equal(fixture.RepositoryId, listed.RepositoryId);
        Assert.Equal(revision, listed.ProfiledRevision);
        Assert.Null(listed.ManifestSha256);
        Assert.Empty(listed.Entries);
        Assert.Empty(listed.Limitations);
        Assert.Null(listed.NextCursor);
    }

    private static void ConfigureProbe(
        IServiceCollection services,
        IdentityLookupCancellationProbe probe)
    {
        services.AddSingleton(probe);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.AddInterceptors(probe));
    }

    private sealed class IdentityLookupCancellationProbe : DbCommandInterceptor
    {
        private readonly string _commandMarker;

        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _blockingEnabled;
        private int _lookupObserved;
        private int _receivedCancellableToken;
        private int _cancellationObserved;

        internal IdentityLookupCancellationProbe(string commandMarker = "AspNetUsers")
        {
            _commandMarker = commandMarker;
        }

        internal TaskCompletionSource LookupStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource CancellationObservedSignal { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool ReceivedCancellableToken =>
            Volatile.Read(ref _receivedCancellableToken) == 1;

        internal bool CancellationObserved =>
            Volatile.Read(ref _cancellationObserved) == 1;

        internal void EnableBlocking() => Interlocked.Exchange(ref _blockingEnabled, 1);

        internal void Release() => _release.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _blockingEnabled) == 1 &&
                Volatile.Read(ref _lookupObserved) == 0 &&
                command.CommandText.Contains(_commandMarker, StringComparison.Ordinal))
            {
                Interlocked.Exchange(ref _lookupObserved, 1);
                Interlocked.Exchange(
                    ref _receivedCancellableToken,
                    cancellationToken.CanBeCanceled ? 1 : 0);
                LookupStarted.TrySetResult();
                if (cancellationToken.CanBeCanceled)
                {
                    try
                    {
                        await _release.Task.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Exchange(ref _cancellationObserved, 1);
                        CancellationObservedSignal.TrySetResult();
                        throw;
                    }
                }
                else
                {
                    await _release.Task;
                }
            }

            return result;
        }
    }
}
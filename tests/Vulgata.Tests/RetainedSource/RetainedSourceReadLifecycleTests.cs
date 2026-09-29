using Vulgata.Tests.TestInfrastructure;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Xunit.Abstractions;

namespace Vulgata.Tests.RetainedSource;

public sealed class RetainedSourceReadLifecycleTests(ITestOutputHelper output)
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task ReadTextAsync_DisposedContextCancelsAdmittedRead_AndLeavesSiblingUsableAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        var barrier = new ReadAuthorizationBarrier();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                authorizationWait: barrier.WaitAsync);
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);

        OpenSourceReadContextResult firstOpen = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            capture.ProfiledRevision!,
            new SourceReadGrantRequest(),
            testCancellation.Token);
        Assert.Equal(SourceReadStatus.Succeeded, firstOpen.Status);
        Assert.NotNull(firstOpen.Context);
        OpenSourceReadContextResult secondOpen = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            capture.ProfiledRevision!,
            new SourceReadGrantRequest(),
            testCancellation.Token);
        Assert.Equal(SourceReadStatus.Succeeded, secondOpen.Status);
        Assert.NotNull(secondOpen.Context);
        ISourceReadContext first = firstOpen.Context!;
        ISourceReadContext second = secondOpen.Context!;
        try
        {
            barrier.Enabled = true;
            Task<SourceTextResult> firstRead = first.ReadTextAsync(
                "src/retained-source.txt",
                0,
                1_024,
                testCancellation.Token);
            await barrier.Entered.Task.WaitAsync(testCancellation.Token);
            await first.DisposeAsync();
            SourceTextResult disposedRead = await firstRead;
            barrier.Enabled = false;
            SourceTextResult siblingRead = await second.ReadTextAsync(
                "src/retained-source.txt",
                0,
                1_024,
                testCancellation.Token);
            output.WriteLine(
                $"capture={capture.Status}/{capture.Code}; " +
                $"disposedRead={disposedRead.Status}/{disposedRead.Code}; " +
                $"siblingRead={siblingRead.Status}/{siblingRead.Code}");

            Assert.Equal(SourceReadStatus.Cancelled, disposedRead.Status);
            Assert.True(disposedRead.Content is null);
            Assert.Equal(SourceReadStatus.Succeeded, siblingRead.Status);
            Assert.Equal("retained-source-fixture\n", siblingRead.Content);
        }
        finally
        {
            barrier.Enabled = false;
            await first.DisposeAsync();
            await second.DisposeAsync();
        }
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task OpenReadContextAsync_SynchronousFirstTimerEvent_LeavesLongGrantUsableAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture captureFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true);
        IRetainedSourceApplication captureApplication = captureFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await captureApplication.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);

        var timeProvider = new SynchronousDueTimeProvider();
        string keyRingPath = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "timer-keyring");
        await using RetainedSourceAttachedApplication readApplication =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                captureFixture.Database.ConnectionString,
                fixture.SourceStorageRoot,
                captureFixture.UserId,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:Read:DeadlineSeconds"] = "8640000",
                },
                dataProtectionKeyRingPath: keyRingPath,
                timeProvider: timeProvider);
        IRetainedSourceApplication reader = readApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();

        OpenSourceReadContextResult opened = await reader.OpenReadContextAsync(
            fixture.RepositoryId,
            capture.ProfiledRevision!,
            new SourceReadGrantRequest(deadline: TimeSpan.FromDays(100)),
            testCancellation.Token);
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        Assert.NotNull(opened.Context);
        SourceEntryResult entry = await opened.Context!.GetEntryAsync(
            "src/retained-source.txt",
            testCancellation.Token);
        output.WriteLine(
            $"timerCallbackInvoked={timeProvider.CallbackInvoked}; " +
            $"opened={opened.Status}/{opened.Code}; entry={entry.Status}/{entry.Code}");

        await opened.Context.DisposeAsync();
        Assert.True(timeProvider.CallbackInvoked);
        Assert.Equal(SourceReadStatus.Succeeded, entry.Status);
    }

    private sealed class ReadAuthorizationBarrier
    {
        internal volatile bool Enabled;

        internal TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal async Task WaitAsync(
            SourceAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enabled || request.Purpose != SourceAuthorizationPurpose.Read)
            {
                return;
            }

            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class SynchronousDueTimeProvider : TimeProvider
    {
        private long _timestamp = Stopwatch.GetTimestamp();

        internal bool CallbackInvoked { get; private set; }

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

        public override long GetTimestamp() => Volatile.Read(ref _timestamp);

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            if (dueTime < TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan)
            {
                throw new InvalidOperationException("The timer due time is invalid.");
            }

            var timer = new SynchronousDueTimer(this, callback, state, period);
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                timer.Schedule(dueTime);
            }

            return timer;
        }

        private void Advance(TimeSpan dueTime)
        {
            if (dueTime > TimeSpan.Zero)
            {
                long ticks = checked((long)(dueTime.TotalSeconds * Stopwatch.Frequency));
                Interlocked.Add(ref _timestamp, ticks);
            }
        }

        private sealed class SynchronousDueTimer(
            SynchronousDueTimeProvider provider,
            TimerCallback callback,
            object? state,
            TimeSpan period) : ITimer
        {
            private int _fired;
            private int _disposed;

            internal void Schedule(TimeSpan dueTime)
            {
                if (dueTime < TimeSpan.Zero || dueTime == Timeout.InfiniteTimeSpan)
                {
                    return;
                }
                if (Interlocked.Exchange(ref _fired, 1) == 1 ||
                    Volatile.Read(ref _disposed) == 1)
                {
                    return;
                }

                provider.Advance(dueTime);
                provider.CallbackInvoked = true;
                callback(state);
            }

            public bool Change(TimeSpan dueTime, TimeSpan newPeriod)
            {
                if (Volatile.Read(ref _disposed) == 1)
                {
                    return false;
                }

                if (Volatile.Read(ref _fired) == 0 && dueTime != Timeout.InfiniteTimeSpan)
                {
                    Schedule(dueTime);
                }

                _ = period;
                _ = newPeriod;
                return true;
            }

            public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}

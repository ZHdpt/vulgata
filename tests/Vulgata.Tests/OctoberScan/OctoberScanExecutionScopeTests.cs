using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

public sealed class OctoberScanExecutionScopeTests
{
    private static readonly Guid Run = Guid.Parse("c3101864-a124-44df-939b-0a54c5c81880");
    private static readonly Guid File = Guid.Parse("a2e01864-a124-44df-939b-0a54c5c81880");
    private static readonly OperationKey Finalization = new(Guid.Parse("b1101864-a124-44df-939b-0a54c5c81880"), OperationKind.FinalizeCorpus, Guid.Parse("d1101864-a124-44df-939b-0a54c5c81880"));

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Acquire_WorkerAndTwoBusinessChildren_ShareOnePermitWithoutOuterPermitAsync()
    {
        await using var scope = Scope();
        var worker = await Granted(scope.AcquireWorkerAttemptAsync(Worker()));
        var first = scope.AcquireProviderAttemptAsync(Business());
        var second = scope.AcquireProviderAttemptAsync(Business(BusinessWorkKind.Synthesis));
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        await Settle(worker, 2);
        var child = await Granted(first);
        Assert.False(second.IsCompleted);
        await Settle(child, 2);
        await Settle(await Granted(second), 2);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckBeforeSend_AuthorityLostAfterGrant_DeniesSendAsync()
    {
        var allowed = true;
        await using var scope = Scope(eligibility: (_, _) => Task.FromResult(allowed ? ProviderAdmissionStatus.Granted : ProviderAdmissionStatus.AuthorityUnavailable));
        var lease = await Granted(scope.AcquireWorkerAttemptAsync(Worker()));
        allowed = false;
        Assert.Equal(ProviderAdmissionStatus.AuthorityUnavailable, await lease.CheckBeforeSendAsync());
        await Settle(lease, 0, ProviderAttemptOutcome.Cancelled);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acquire_QueueOrRateDelay_DoesNotRenewOriginalDeadlineAsync(bool rateDelay)
    {
        var clock = new ManualClock();
        await using var scope = Scope(Policy() with { MaximumRequestsPerMinute = rateDelay ? 1 : 60 }, clock);
        var active = await Granted(scope.AcquireWorkerAttemptAsync(Worker()));
        if (rateDelay) await Settle(active, 1);
        var pending = scope.AcquireWorkerAttemptAsync(Worker() with { MaximumDuration = TimeSpan.FromSeconds(1) });
        Assert.False(pending.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(ProviderAdmissionStatus.DeadlineExceeded, result.Status);
        Assert.Null(result.Lease);
        if (!rateDelay) await Settle(active, 1);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Settle_ActualUsageTwice_ChargesOnceAndReleasesOnceAsync()
    {
        await using var scope = Scope(Policy() with { MaximumTotalTokens = 60 });
        var lease = await Granted(scope.AcquireWorkerAttemptAsync(Worker() with { MaximumTokens = 40 }));
        var usage = Usage(10);
        await lease.SettleAsync(usage);
        await lease.SettleAsync(usage);
        await lease.DisposeAsync();
        var next = await Granted(scope.AcquireWorkerAttemptAsync(Worker() with { MaximumTokens = 50 }));
        await Settle(next, 50);
        var exhausted = await scope.AcquireWorkerAttemptAsync(Worker() with { MaximumTokens = 1 });
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, exhausted.Status);
        Assert.Null(exhausted.Lease);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Settle_UnknownUsage_ConservesReservationRatherThanChargingZeroAsync()
    {
        await using var scope = Scope(Policy() with { MaximumTotalTokens = 50 });
        var lease = await Granted(scope.AcquireWorkerAttemptAsync(Worker() with { MaximumTokens = 40 }));
        await lease.SettleAsync(new(null, null, null, TimeSpan.Zero, TimeSpan.FromSeconds(1), ProviderAttemptOutcome.Completed));
        await lease.DisposeAsync();
        var denied = await scope.AcquireWorkerAttemptAsync(Worker() with { MaximumTokens = 20 });
        Assert.NotEqual(ProviderAdmissionStatus.Granted, denied.Status);
        Assert.Null(denied.Lease);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Dispose_UnsettledTransport_DoesNotReclaimCapacityAsync()
    {
        var scope = Scope();
        var lease = await Granted(scope.AcquireWorkerAttemptAsync(Worker()));
        try
        {
            // An unconfirmed transport is not settlement; disposal may report the failure.
            _ = await Record.ExceptionAsync(() => lease.DisposeAsync().AsTask());
            var next = await scope.AcquireWorkerAttemptAsync(Worker()).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.NotEqual(ProviderAdmissionStatus.Granted, next.Status);
            Assert.Null(next.Lease);
        }
        finally
        {
            await lease.SettleAsync(Usage(0, ProviderAttemptOutcome.Cancelled));
            _ = await Record.ExceptionAsync(() => scope.DisposeAsync().AsTask());
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task HardStop_ActiveAndQueuedCalls_CancelsAndBarsBothPhasesAsync()
    {
        await using var scope = Scope();
        var active = await Granted(scope.AcquireWorkerAttemptAsync(Worker()));
        var queued = scope.AcquireProviderAttemptAsync(Business());
        scope.HardStop();
        Assert.True(active.CancellationToken.IsCancellationRequested);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, await active.CheckBeforeSendAsync());
        Assert.Equal(ProviderAdmissionStatus.HardStopped, (await queued.WaitAsync(TimeSpan.FromSeconds(3))).Status);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, (await scope.AcquireWorkerAttemptAsync(Worker())).Status);
        await Settle(active, 0, ProviderAttemptOutcome.Cancelled);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Eligibility_DrainAllowsAdmittedWorkerThenFencesWorkerOnly_BusinessRetainsOriginalBudgetAsync()
    {
        var drainExpired = false;
        await using var scope = Scope(eligibility: (phase, _) => Task.FromResult(
            drainExpired && phase == OctoberProviderPhase.Worker ? ProviderAdmissionStatus.Denied : ProviderAdmissionStatus.Granted));
        await Settle(await Granted(scope.AcquireWorkerAttemptAsync(Worker())), 1);
        drainExpired = true;
        Assert.Equal(ProviderAdmissionStatus.Denied, (await scope.AcquireWorkerAttemptAsync(Worker())).Status);
        await Settle(await Granted(scope.AcquireProviderAttemptAsync(Business())), 1);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("run")]
    [InlineData("profile")]
    [InlineData("operation")]
    public async Task AcquireBusiness_ForeignBinding_IsDeniedAsync(string mismatch)
    {
        await using var scope = Scope();
        var request = Business();
        request = mismatch switch
        {
            "run" => request with { RunId = Guid.NewGuid() },
            "profile" => request with { ProviderProfileId = "other" },
            "operation" => request with { FinalizationOperation = Finalization with { OperationId = Guid.NewGuid() } },
            _ => throw new InvalidOperationException(),
        };
        var result = await scope.AcquireProviderAttemptAsync(request);
        Assert.Equal(ProviderAdmissionStatus.Denied, result.Status);
        Assert.Null(result.Lease);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Acquire_RetryAfterRequestBudgetExhaustion_DoesNotReceiveNewAllowanceAsync()
    {
        await using var scope = Scope(Policy() with { MaximumProviderRequests = 1, MaximumModelRequestsPerFile = 1, MaximumContinuationWindows = 1 });
        await Settle(await Granted(scope.AcquireWorkerAttemptAsync(Worker())), 1, ProviderAttemptOutcome.Failed);
        var retry = await scope.AcquireWorkerAttemptAsync(Worker());
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, retry.Status);
        Assert.Null(retry.Lease);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Acquire_SameFileNewWindowAfterOriginalFileDeadline_DoesNotRenewFileLifetimeAsync()
    {
        var clock = new ManualClock();
        await using var scope = Scope(Policy() with { FileDeadlineSeconds = 1 }, clock);
        await Settle(await Granted(scope.AcquireWorkerAttemptAsync(Worker())), 1);
        clock.Advance(TimeSpan.FromSeconds(2));
        var nextWindow = await scope.AcquireWorkerAttemptAsync(Worker());
        try
        {
            Assert.Equal(ProviderAdmissionStatus.DeadlineExceeded, nextWindow.Status);
            Assert.Null(nextWindow.Lease);
        }
        finally
        {
            // A broken admission returned an unsent grant; settle only that test-owned cleanup.
            if (nextWindow.Lease is not null) await nextWindow.Lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Acquire_SameFileWindowsAndRetries_SharePerFileRequestCountAsync()
    {
        await using var scope = Scope(Policy() with { MaximumModelRequestsPerFile = 2, MaximumContinuationWindows = 2 });
        await Settle(await Granted(scope.AcquireWorkerAttemptAsync(Worker())), 1);
        await Settle(await Granted(scope.AcquireWorkerAttemptAsync(Worker())), 1, ProviderAttemptOutcome.Failed);
        var retry = await scope.AcquireWorkerAttemptAsync(Worker());
        try
        {
            Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, retry.Status);
            Assert.Null(retry.Lease);
        }
        finally
        {
            if (retry.Lease is not null) await retry.Lease.DisposeAsync();
        }
        await Settle(await Granted(scope.AcquireWorkerAttemptAsync(Worker() with { FileWorkstreamId = Guid.NewGuid() })), 1);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task Acquire_EligibilityThrowsUnrelatedCancellation_FailsClosedWithoutGrantAsync()
    {
        await using var scope = Scope(eligibility: (_, _) =>
            Task.FromException<ProviderAdmissionStatus>(new OperationCanceledException("Controlled callback cancellation unrelated to admission token")));
        var result = await scope.AcquireWorkerAttemptAsync(Worker());
        try
        {
            Assert.Equal(ProviderAdmissionStatus.AuthorityUnavailable, result.Status);
            Assert.Null(result.Lease);
        }
        finally
        {
            if (result.Lease is not null) await result.Lease.DisposeAsync();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task CheckBeforeSend_DelayedAuthorityAfterRateReservation_DoesNotReleaseTwoSendsAtOnceAsync()
    {
        var clock = new ManualClock();
        var gates = System.Threading.Channels.Channel.CreateUnbounded<TaskCompletionSource<ProviderAdmissionStatus>>();
        var hold = false;
        var gateSync = new object();
        Task<ProviderAdmissionStatus> Eligibility(OctoberProviderPhase phase, CancellationToken token)
        {
            lock (gateSync)
            {
                if (!hold) return Task.FromResult(ProviderAdmissionStatus.Granted);
                var gate = new TaskCompletionSource<ProviderAdmissionStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
                gates.Writer.TryWrite(gate);
                return gate.Task.WaitAsync(token);
            }
        }
        await using var scope = Scope(Policy() with { WorkerCap = 2 }, clock, Eligibility);
        var firstAdmission = await scope.AcquireWorkerAttemptAsync(Worker());
        var pendingAdmission = scope.AcquireWorkerAttemptAsync(Worker());
        clock.Advance(TimeSpan.FromSeconds(1));
        var secondAdmission = await pendingAdmission.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(ProviderAdmissionStatus.Granted, firstAdmission.Status);
        Assert.Equal(ProviderAdmissionStatus.Granted, secondAdmission.Status);
        var first = firstAdmission.Lease!;
        var second = secondAdmission.Lease!;
        lock (gateSync) hold = true;
        async Task<long> Check(IOctoberProviderAttemptLease lease)
        {
            Assert.Equal(ProviderAdmissionStatus.Granted, await lease.CheckBeforeSendAsync());
            return clock.GetTimestamp();
        }
        var firstCheck = Check(first);
        var secondCheck = Check(second);
        var retained = new List<TaskCompletionSource<ProviderAdmissionStatus>>();
        Task<bool>? nextGate = null;
        try
        {
            var initialFirst = await gates.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            var initialSecond = await gates.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            initialFirst.TrySetResult(ProviderAdmissionStatus.Granted);
            initialSecond.TrySetResult(ProviderAdmissionStatus.Granted);
            nextGate = gates.Reader.WaitToReadAsync().AsTask();
            await Task.WhenAny(firstCheck, secondCheck, nextGate).WaitAsync(TimeSpan.FromSeconds(3));
            if (nextGate.IsCompletedSuccessfully)
            {
                while (gates.Reader.TryRead(out var gate)) retained.Add(gate);
                nextGate = null;
            }
            clock.Advance(TimeSpan.FromSeconds(2));
            if (!firstCheck.IsCompleted && !secondCheck.IsCompleted && retained.Count < 2)
            {
                nextGate ??= gates.Reader.WaitToReadAsync().AsTask();
                await nextGate.WaitAsync(TimeSpan.FromSeconds(3));
                while (gates.Reader.TryRead(out var gate)) retained.Add(gate);
                nextGate = null;
            }
            // Release delayed authority at one instant. Subsequent callbacks may complete normally.
            lock (gateSync)
            {
                hold = false;
                foreach (var gate in retained) gate.TrySetResult(ProviderAdmissionStatus.Granted);
                while (gates.Reader.TryRead(out var gate)) gate.TrySetResult(ProviderAdmissionStatus.Granted);
            }
            var complete = Task.WhenAll(firstCheck, secondCheck);
            await clock.PumpRateTimersUntilAsync(complete, TimeSpan.FromSeconds(1));
            var stamps = await complete;
            Assert.True(Math.Abs(stamps[0] - stamps[1]) >= TimeSpan.TicksPerSecond,
                "Fresh authority may delay sends, but cannot make expired rate reservations release a burst.");
        }
        finally
        {
            lock (gateSync)
            {
                hold = false;
                foreach (var gate in retained) gate.TrySetResult(ProviderAdmissionStatus.Granted);
                while (gates.Reader.TryRead(out var gate)) gate.TrySetResult(ProviderAdmissionStatus.Granted);
            }
            scope.HardStop();
            _ = await Record.ExceptionAsync(async () => await Task.WhenAll(firstCheck, secondCheck));
            await Settle(first, 0, ProviderAttemptOutcome.Cancelled);
            await Settle(second, 0, ProviderAttemptOutcome.Cancelled);
        }
    }

    private static OctoberExecutionScope Scope(OctoberScanPolicy? policy = null, TimeProvider? clock = null,
        Func<OctoberProviderPhase, CancellationToken, Task<ProviderAdmissionStatus>>? eligibility = null) =>
        new(Run, "selected-profile", policy ?? Policy(), eligibility ?? ((_, _) => Task.FromResult(ProviderAdmissionStatus.Granted)), Finalization, clock);
    private static OctoberScanPolicy Policy() => new()
    {
        OutputStage = "cl-and-bl", MaximumTotalTokens = 1000,
        SourceRules = [new(Guid.NewGuid(), "src", "selected", "Explicit test scope")],
    };
    private static OctoberWorkerProviderAttemptRequest Worker() => new(Run, File, Guid.NewGuid(), "selected-profile", TimeSpan.FromSeconds(10), 20);
    private static ProviderAttemptRequest Business(BusinessWorkKind kind = BusinessWorkKind.Resolution) => new(Run, Finalization, Guid.NewGuid(), kind, "selected-profile", TimeSpan.FromSeconds(10), 20);
    private static ProviderAttemptUsage Usage(long tokens, ProviderAttemptOutcome outcome = ProviderAttemptOutcome.Completed) => new(tokens, 0, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1), outcome);
    private static async Task<IOctoberProviderAttemptLease> Granted(Task<ProviderAdmission> task)
    {
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(ProviderAdmissionStatus.Granted, result.Status);
        var lease = Assert.IsAssignableFrom<IOctoberProviderAttemptLease>(result.Lease);
        Assert.Equal(ProviderAdmissionStatus.Granted, await lease.CheckBeforeSendAsync());
        return lease;
    }
    private static async Task Settle(IOctoberProviderAttemptLease lease, long tokens, ProviderAttemptOutcome outcome = ProviderAttemptOutcome.Completed)
    {
        await lease.SettleAsync(Usage(tokens, outcome));
        await lease.DisposeAsync();
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<ManualTimer, byte> _timers = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<ManualTimer> _scheduled = new();
        private readonly SemaphoreSlim _timerAvailable = new(0);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            _timers.TryAdd(timer, 0);
            timer.Change(dueTime, period);
            _scheduled.Enqueue(timer);
            _timerAvailable.Release();
            return timer;
        }
        internal void Advance(TimeSpan elapsed)
        {
            _timestamp += elapsed.Ticks;
            foreach (var timer in _timers.Keys.ToArray()) timer.FireIfDue();
        }
        internal async Task PumpRateTimersUntilAsync(Task completed, TimeSpan maximumRateDelay)
        {
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (!completed.IsCompleted)
            {
                ManualTimer? due = null;
                while (_scheduled.TryDequeue(out var candidate))
                {
                    var remaining = candidate.Due - _timestamp;
                    if (!candidate.Disposed && remaining >= 0 && remaining <= maximumRateDelay.Ticks)
                    {
                        due = candidate;
                        break;
                    }
                }
                if (due is not null)
                {
                    Advance(TimeSpan.FromTicks(due.Due - _timestamp));
                    await Task.Yield();
                    continue;
                }
                var registered = _timerAvailable.WaitAsync(guard.Token);
                if (await Task.WhenAny(completed, registered) == completed)
                {
                    guard.Cancel();
                    try { await registered; } catch (OperationCanceledException) { }
                    break;
                }
                await registered;
            }
            await completed;
        }
        private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long _due = long.MaxValue;
            private TimeSpan _period = Timeout.InfiniteTimeSpan;
            private bool _disposed;
            internal long Due => _due;
            internal bool Disposed => _disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed) return false;
                _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._timestamp + dueTime.Ticks;
                _period = period;
                return true;
            }
            internal void FireIfDue()
            {
                if (_disposed || clock._timestamp < _due) return;
                _due = _period == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._timestamp + _period.Ticks;
                callback(state);
            }
            public void Dispose() { _disposed = true; clock._timers.TryRemove(this, out _); }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}

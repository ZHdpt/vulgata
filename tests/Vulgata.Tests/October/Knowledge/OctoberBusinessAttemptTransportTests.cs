using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

/// <summary>Controlled attempt transport and host protocol probes, not real HTTP/provider or Scan factory qualification.</summary>
public sealed class OctoberBusinessAttemptTransportTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_MissingAttemptCapability_RefusesBeforeAdmissionAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        using var client = new PlainClient(f.Response);
        var result = await f.ExecuteAsync(client, scope);
        Assert.False(result.HasMaterial);
        Assert.NotNull(result.Failure);
        Assert.NotEqual(ProviderAdmissionStatus.Granted, result.Failure);
        Assert.Empty(result.Attempts);
        Assert.Empty(scope.Requests);
        Assert.Equal(0, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_DifferentSelectedProfile_RefusesBeforeAdmissionAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        using var client = new AttemptClient((_, _) => Task.FromResult(f.Response())) { ProviderProfileId = "another-profile" };
        var result = await f.ExecuteAsync(client, scope);
        Assert.False(result.HasMaterial);
        Assert.NotNull(result.Failure);
        Assert.NotEqual(ProviderAdmissionStatus.Granted, result.Failure);
        Assert.Empty(scope.Requests);
        Assert.Empty(result.Attempts);
        Assert.Equal(0, client.AttemptCalls + client.PlainCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_GrantedLeaseWithoutDispatchObserver_RefusesTransportAndReleasesOnceAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope { OmitObserver = true };
        using var client = new AttemptClient((_, _) => Task.FromResult(f.Response()));
        var result = await f.ExecuteAsync(client, scope);
        Assert.False(result.HasMaterial);
        Assert.Equal(ProviderAdmissionStatus.AuthorityUnavailable, result.Failure);
        Assert.Equal(0, client.AttemptCalls);
        Assert.Equal(0, client.PlainCalls);
        Assert.Single(scope.Requests);
        var lease = Assert.Single(scope.Leases);
        Assert.Equal(1, lease.Disposals);
        Assert.Equal(0, lease.Completions);
        Assert.DoesNotContain("dispatch", lease.Events);
        Assert.All(lease.Usage, usage =>
        {
            Assert.Null(usage.InputTokens);
            Assert.Null(usage.OutputTokens);
            Assert.Null(usage.Cost);
        });
        Assert.All(result.Attempts, usage =>
        {
            Assert.Null(usage.InputTokens);
            Assert.Null(usage.OutputTokens);
        });
        Assert.Equal(0, scope.Active);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_DispatchedSuccess_UsesSameLeaseAndCompletesObserverBeforeSettlementAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        TimeSpan physicalBodyDuration = TimeSpan.Zero;
        using var client = new AttemptClient((_, _) =>
        {
            long bodyStarted = Stopwatch.GetTimestamp();
            var response = f.Response();
            physicalBodyDuration = Stopwatch.GetElapsedTime(bodyStarted);
            return Task.FromResult(response);
        });
        Assert.NotEqual(Stopwatch.Frequency, f.Clock.TimestampFrequency);
        Assert.True(f.Clock.GetTimestamp() < 0);
        long observedStarted = Stopwatch.GetTimestamp();
        var result = await f.ExecuteAsync(client, scope);
        TimeSpan observedDuration = Stopwatch.GetElapsedTime(observedStarted);
        Assert.True(result.HasMaterial);
        Assert.Null(result.Failure);
        var lease = Assert.Single(scope.Leases);
        Assert.Same(lease, Assert.Single(client.Leases));
        Assert.Equal(1, client.AttemptCalls);
        Assert.Equal(0, client.PlainCalls);
        Assert.Equal(new[] { "dispatch", "physical-terminal", "observation-complete", "settle", "dispose" }, lease.Events.ToArray());
        Assert.Equal(1, lease.Completions);
        var usage = Assert.Single(result.Attempts);
        Assert.Equal(10, usage.InputTokens);
        Assert.Equal(5, usage.OutputTokens);
        Assert.True(physicalBodyDuration > TimeSpan.Zero);
        Assert.InRange(usage.ExecutionDuration, physicalBodyDuration, observedDuration);
        Assert.Same(usage, Assert.Single(lease.Usage));
        Assert.Equal(ProviderAttemptOutcome.Completed, usage.Outcome);
        Assert.Equal(0, scope.Active);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_EligibilityIsCheckedOnlyInsideSelectedTransport_NotReservedEarlyAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        using var client = new AttemptClient((_, _) => Task.FromResult(f.Response()));
        var result = await f.ExecuteAsync(client, scope);
        Assert.True(result.HasMaterial);
        Assert.Null(result.Failure);
        var lease = Assert.Single(scope.Leases);
        Assert.Same(lease, Assert.Single(client.Leases));
        Assert.Equal(0, Assert.Single(client.EligibilityChecksAtEntry));
        Assert.Equal(1, lease.EligibilityChecks);
        Assert.Equal(1, client.AttemptCalls);
        Assert.Equal(0, client.PlainCalls);
        Assert.Equal(new[] { "dispatch", "physical-terminal", "observation-complete", "settle", "dispose" }, lease.Events.ToArray());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ObservedFailureBeforeDispatch_SettlesZeroOnlyAfterTerminalObservationAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        using var client = new AttemptClient((_, _) => throw new InvalidOperationException("Controlled pre-dispatch refusal.")) { Dispatch = false };
        var result = await f.ExecuteAsync(client, scope);
        Assert.False(result.HasMaterial);
        Assert.Equal(1, client.AttemptCalls);
        Assert.Equal(0, client.PlainCalls);
        var lease = Assert.Single(scope.Leases);
        Assert.Equal(new[] { "physical-terminal", "observation-complete", "settle", "dispose" }, lease.Events.ToArray());
        var usage = Assert.Single(result.Attempts);
        Assert.Equal(0, usage.InputTokens);
        Assert.Equal(0, usage.OutputTokens);
        Assert.Equal(ProviderAttemptOutcome.Failed, usage.Outcome);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_DispatchedResponseWithoutUsage_PreservesUnknownAccountingAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        using var client = new AttemptClient((_, _) => Task.FromResult(f.Response(known: false)));
        var result = await f.ExecuteAsync(client, scope);
        Assert.True(result.HasMaterial);
        var usage = Assert.Single(result.Attempts);
        Assert.Null(usage.InputTokens);
        Assert.Null(usage.OutputTokens);
        Assert.Null(usage.Cost);
        Assert.Equal(1, client.AttemptCalls);
        Assert.Equal(1, Assert.Single(scope.Leases).Completions);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, ProviderAdmissionStatus.Denied)]
    [InlineData(false, ProviderAdmissionStatus.BudgetExhausted)]
    [InlineData(false, ProviderAdmissionStatus.DeadlineExceeded)]
    [InlineData(false, ProviderAdmissionStatus.HardStopped)]
    [InlineData(false, ProviderAdmissionStatus.AuthorityUnavailable)]
    [InlineData(true, ProviderAdmissionStatus.Denied)]
    [InlineData(true, ProviderAdmissionStatus.BudgetExhausted)]
    [InlineData(true, ProviderAdmissionStatus.DeadlineExceeded)]
    [InlineData(true, ProviderAdmissionStatus.HardStopped)]
    [InlineData(true, ProviderAdmissionStatus.AuthorityUnavailable)]
    public async Task ExecuteAsync_TypedFinalGuardDenial_PreservesExactStatusWithoutDispatchOrRetryAsync(bool synthesis, ProviderAdmissionStatus status)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        int responseCalls = 0;
        using var client = new AttemptClient((_, _) => { responseCalls++; return Task.FromResult(f.Response()); }) { FinalDenial = status };
        var result = await f.ExecuteAsync(client, scope);
        Assert.False(result.HasMaterial);
        Assert.Equal(status, result.Failure);
        Assert.Equal(1, client.AttemptCalls);
        Assert.Equal(0, client.PlainCalls);
        Assert.Equal(0, responseCalls);
        Assert.Equal(0, client.DispatchCallbacks);
        Assert.Single(scope.Requests);
        var lease = Assert.Single(scope.Leases);
        Assert.Same(lease, Assert.Single(client.Leases));
        Assert.Equal(new[] { "physical-terminal", "observation-complete", "settle", "dispose" }, lease.Events.ToArray());
        var usage = Assert.Single(result.Attempts);
        Assert.Equal(0, usage.InputTokens);
        Assert.Equal(0, usage.OutputTokens);
        Assert.Equal(TimeSpan.Zero, usage.ExecutionDuration);
        Assert.Equal(0, scope.Active);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_TypedDenialAfterDispatch_DoesNotInventUnsentUsageAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        using var client = new AttemptClient((_, _) => throw new OctoberBusinessAttemptDeniedException(ProviderAdmissionStatus.BudgetExhausted));
        var result = await f.ExecuteAsync(client, scope);
        Assert.False(result.HasMaterial);
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, result.Failure);
        Assert.Equal(1, client.AttemptCalls);
        Assert.Equal(0, client.PlainCalls);
        Assert.Equal(1, client.DispatchCallbacks);
        Assert.Single(scope.Requests);
        var usage = Assert.Single(result.Attempts);
        Assert.Null(usage.InputTokens);
        Assert.Null(usage.OutputTokens);
        Assert.Null(usage.Cost);
        Assert.Equal(new[] { "dispatch", "physical-terminal", "observation-complete", "settle", "dispose" }, Assert.Single(scope.Leases).Events.ToArray());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_TypedDenialWithFailedObservation_ReportsUnavailableAndUnknownUsageAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope { FailObservation = true };
        using var client = new AttemptClient((_, _) => Task.FromResult(f.Response())) { FinalDenial = ProviderAdmissionStatus.HardStopped };
        var result = await f.ExecuteAsync(client, scope);
        Assert.False(result.HasMaterial);
        Assert.Equal(ProviderAdmissionStatus.AuthorityUnavailable, result.Failure);
        Assert.Equal(1, client.AttemptCalls);
        Assert.Equal(0, client.PlainCalls);
        Assert.Equal(0, client.DispatchCallbacks);
        Assert.Single(scope.Requests);
        var usage = Assert.Single(result.Attempts);
        Assert.Null(usage.InputTokens);
        Assert.Null(usage.OutputTokens);
        Assert.Null(usage.Cost);
        Assert.Equal(new[] { "physical-terminal", "observation-failed", "settle", "dispose" }, Assert.Single(scope.Leases).Events.ToArray());
        Assert.Equal(0, scope.Active);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(ProviderAdmissionStatus.Granted)]
    [InlineData((ProviderAdmissionStatus)(-1))]
    [InlineData((ProviderAdmissionStatus)999)]
    public void AttemptDeniedException_RejectsGrantedAndUndefinedStatuses(ProviderAdmissionStatus status)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new OctoberBusinessAttemptDeniedException(status));
        Assert.Equal("status", exception.ParamName);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExecuteAsync_ObservationCompletionFails_NeverRefundsOrExposesMaterialAsync(bool synthesis, bool dispatched)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope { FailObservation = true };
        using var client = new AttemptClient((_, _) => dispatched
            ? Task.FromResult(f.Response())
            : throw new InvalidOperationException("Controlled unsent refusal.")) { Dispatch = dispatched };
        var result = await f.ExecuteAsync(client, scope);
        Assert.False(result.HasMaterial);
        Assert.Equal(ProviderAdmissionStatus.AuthorityUnavailable, result.Failure);
        var usage = Assert.Single(result.Attempts);
        Assert.Null(usage.InputTokens);
        Assert.Null(usage.OutputTokens);
        Assert.Null(usage.Cost);
        var lease = Assert.Single(scope.Leases);
        Assert.Equal(1, lease.Completions);
        Assert.Equal(1, lease.Disposals);
        Assert.Single(lease.Usage);
        Assert.True(lease.Events.IndexOf("observation-failed") < lease.Events.IndexOf("settle"));
        Assert.Equal(1, client.AttemptCalls);
        Assert.Equal(0, scope.Active);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_PhysicalTaskIgnoresCancellation_RetainsLeaseUntilActualTerminalAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        using var cancellation = new CancellationTokenSource();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new AttemptClient(async (_, _) => { await release.Task; return f.Response(); });
        var active = f.ExecuteAsync(client, scope, cancellation.Token);
        await client.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            cancellation.Cancel();
            Assert.False(active.IsCompleted);
            Assert.Equal(1, scope.Active);
            var lease = Assert.Single(scope.Leases);
            Assert.Empty(lease.Usage);
            Assert.Equal(0, lease.Completions);
            Assert.Equal(0, lease.Disposals);
            Assert.True(client.LastCancellation.IsCancellationRequested);
        }
        finally { release.TrySetResult(); }
        var result = await active.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.HasMaterial);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, result.Failure);
        Assert.Equal(1, client.AttemptCalls);
        Assert.Equal(0, client.PlainCalls);
        Assert.Equal(new[] { "dispatch", "physical-terminal", "observation-complete", "settle", "dispose" }, Assert.Single(scope.Leases).Events.ToArray());
        Assert.Equal(0, scope.Active);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_RetryGetsFreshLease_UsesRemainingOriginalBudgetAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        using var client = new AttemptClient((call, _) =>
        {
            if (call == 1) { f.Clock.Advance(TimeSpan.FromSeconds(7)); throw new IOException("Controlled sent failure."); }
            return Task.FromResult(f.Response());
        });
        var result = await f.ExecuteAsync(client, scope);
        Assert.True(result.HasMaterial);
        Assert.Equal(2, client.AttemptCalls);
        Assert.Equal(0, client.PlainCalls);
        Assert.Equal(2, scope.Leases.Count);
        Assert.NotSame(scope.Leases[0], scope.Leases[1]);
        Assert.NotEqual(scope.Requests[0].AttemptId, scope.Requests[1].AttemptId);
        Assert.Equal(scope.Requests[0].MaximumDuration - TimeSpan.FromSeconds(7), scope.Requests[1].MaximumDuration);
        Assert.All(scope.Requests, request => Assert.Equal(40, request.MaximumTokens));
        for (int i = 0; i < 2; i++)
        {
            Assert.Same(scope.Leases[i], client.Leases[i]);
            Assert.Equal(new[] { "dispatch", "physical-terminal", "observation-complete", "settle", "dispose" }, scope.Leases[i].Events.ToArray());
        }
        Assert.Null(result.Attempts[0].InputTokens);
        Assert.Null(result.Attempts[0].OutputTokens);
        Assert.Equal(10, result.Attempts[1].InputTokens);
        Assert.Equal(1, scope.PeakActive);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_RepeatedSentFailure_StopsAfterTwoAttemptsAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        using var client = new AttemptClient((_, _) => throw new IOException("Controlled repeated sent failure."));
        var result = await f.ExecuteAsync(client, scope);
        Assert.False(result.HasMaterial);
        Assert.Equal(2, client.AttemptCalls);
        Assert.Equal(2, result.Attempts.Length);
        Assert.All(result.Attempts, usage => { Assert.Null(usage.InputTokens); Assert.Null(usage.OutputTokens); });
        Assert.All(scope.Leases, lease => { Assert.Equal(1, lease.Completions); Assert.Equal(1, lease.Disposals); });
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_OriginalDeadlineExpiresDuringSentFailure_DoesNotStartRetryAsync(bool synthesis)
    {
        var f = new AttemptCase(synthesis);
        using var scope = new AttemptScope();
        using var client = new AttemptClient((_, _) =>
        {
            f.Clock.Advance(TimeSpan.FromSeconds(synthesis ? 181 : 121));
            throw new IOException("Controlled failure after original deadline.");
        });
        var result = await f.ExecuteAsync(client, scope);
        Assert.False(result.HasMaterial);
        Assert.Equal(ProviderAdmissionStatus.DeadlineExceeded, result.Failure);
        Assert.Equal(1, client.AttemptCalls);
        Assert.Single(scope.Requests);
        var lease = Assert.Single(scope.Leases);
        Assert.Equal(1, lease.Completions);
        Assert.Equal(1, lease.Disposals);
    }

    private sealed record AttemptResult(bool HasMaterial, ProviderAdmissionStatus? Failure, ImmutableArray<ProviderAttemptUsage> Attempts);

    private sealed class AttemptCase(bool synthesis)
    {
        private readonly ResolutionCase _resolution = new();
        private readonly SynthesisCase? _synthesis = synthesis ? new() : null;
        public AttemptClock Clock { get; } = new();
        public ChatResponse Response(bool known = true) => _synthesis?.Response(known) ?? new(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(_resolution.Witness())))
        {
            FinishReason = ChatFinishReason.Stop,
            Usage = known ? new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 } : null
        };
        public async Task<AttemptResult> ExecuteAsync(IChatClient client, AttemptScope scope, CancellationToken token = default)
        {
            if (_synthesis is { } s)
            {
                var result = await new OctoberBusinessSynthesisExecutor(client, "Controlled accepted-input instructions.", Clock)
                    .ExecuteAsync(s.Input, s.Group.GroupVersionId, s.Operation, s.Run, "attempt-profile", 40, scope,
                        new ResolutionDisclosure(), token, s.Limits);
                return new(result.Content is not null || result.Proposal is not null, result.AdmissionFailure, result.Attempts);
            }
            var resolution = await new OctoberResolutionExecutor(client, "Controlled accepted-input instructions.", Clock)
                .ExecuteAsync(_resolution.Corpus, _resolution.Inquiry(),
                    new(_resolution.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, ResolutionCase.Id(99801)),
                    ResolutionCase.Id(99802), "attempt-profile", 40, scope, new ResolutionDisclosure(), token);
            return new(resolution.Outcome is not null || resolution.Witness is not null, resolution.AdmissionFailure, resolution.Attempts);
        }
    }

    /// <summary>Original budget clock deliberately has neither the Stopwatch epoch nor its frequency.</summary>
    private sealed class AttemptClock : TimeProvider
    {
        private long _elapsedTicks;
        public override long TimestampFrequency => Stopwatch.Frequency == 1009 ? 1013 : 1009;
        public override long GetTimestamp() => -4_000_000_000_000L +
            (long)((decimal)Interlocked.Read(ref _elapsedTicks) * TimestampFrequency / TimeSpan.TicksPerSecond);
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(Interlocked.Read(ref _elapsedTicks));
        public void Advance(TimeSpan duration) => Interlocked.Add(ref _elapsedTicks, duration.Ticks);
    }

    /// <summary>Deliberately has no attempt capability; a normal response must never be requested.</summary>
    private sealed class PlainClient(Func<bool, ChatResponse> response) : IChatClient
    {
        public int Calls { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(response(true)); }
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }

    /// <summary>Explicit controlled dispatch boundary; no HTTP, SDK, provider, or fallback qualification.</summary>
    private sealed class AttemptClient(Func<int, CancellationToken, Task<ChatResponse>> response) : IChatClient, IOctoberBusinessAttemptClient
    {
        public string ProviderProfileId { get; init; } = "attempt-profile";
        public bool Dispatch { get; init; } = true;
        public ProviderAdmissionStatus? FinalDenial { get; init; }
        public int DispatchCallbacks { get; private set; }
        public int AttemptCalls { get; private set; }
        public int PlainCalls { get; private set; }
        public List<IOctoberProviderAttemptLease> Leases { get; } = [];
        public List<int> EligibilityChecksAtEntry { get; } = [];
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken LastCancellation { get; private set; }
        public async Task<ChatResponse> GetAttemptResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options,
            IOctoberProviderAttemptLease lease, Action<long> onDispatched, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(onDispatched);
            Leases.Add(lease);
            EligibilityChecksAtEntry.Add(((AttemptLease)lease).EligibilityChecks);
            int call = ++AttemptCalls;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
            LastCancellation = linked.Token;
            try
            {
                if (await lease.CheckBeforeSendAsync(linked.Token) != ProviderAdmissionStatus.Granted)
                    throw new InvalidOperationException("Controlled final eligibility refusal.");
                linked.Token.ThrowIfCancellationRequested();
                if (FinalDenial is { } denial) throw new OctoberBusinessAttemptDeniedException(denial);
                if (Dispatch)
                {
                    onDispatched(Stopwatch.GetTimestamp());
                    DispatchCallbacks++;
                    ((IOctoberWorkerDispatchObserver)lease).RecordDispatch();
                }
                Entered.TrySetResult();
                return await response(call, linked.Token);
            }
            finally { ((AttemptLease)lease).PhysicalTerminal(); }
        }
        // This deliberate tripwire still supplies a valid response, making bypass an observable behavioral failure.
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        { PlainCalls++; LastCancellation = cancellationToken; Entered.TrySetResult(); return await response(PlainCalls, cancellationToken); }
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }

    /// <summary>Controlled one-permit host model; exact observer events are independent of executor result claims.</summary>
    private sealed class AttemptScope : IOctoberExecutionScope, IOctoberBusinessHandoffLifetime, IDisposable
    {
        private readonly SemaphoreSlim _permit = new(1);
        public bool FailObservation { get; init; }
        public bool OmitObserver { get; init; }
        public List<ProviderAttemptRequest> Requests { get; } = [];
        public List<AttemptLease> Leases { get; } = [];
        public int Active { get; private set; }
        public int PeakActive { get; private set; }
        public CancellationToken HandoffCancellation => CancellationToken.None;
        public ProviderAdmissionStatus CheckHandoffEligibility(Guid runId, OperationKey operation, string providerProfileId) => ProviderAdmissionStatus.Granted;
        public async Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            await _permit.WaitAsync(cancellationToken);
            Active++;
            PeakActive = Math.Max(PeakActive, Active);
            var lease = new AttemptLease(this, request.AttemptId);
            Leases.Add(lease);
            return new(ProviderAdmissionStatus.Granted, OmitObserver ? new UnobservedLease(lease) : lease);
        }
        public void Release() { Active--; _permit.Release(); }
        public void Dispose() => _permit.Dispose();
    }

    /// <summary>Deliberately removes only the optional observer capability from a granted leaf lease.</summary>
    private sealed class UnobservedLease(AttemptLease inner) : IOctoberProviderAttemptLease
    {
        public Guid AttemptId => inner.AttemptId;
        public CancellationToken CancellationToken => inner.CancellationToken;
        public Task<ProviderAdmissionStatus> CheckBeforeSendAsync(CancellationToken cancellationToken = default) => inner.CheckBeforeSendAsync(cancellationToken);
        public Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default) => inner.SettleAsync(usage, cancellationToken);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class AttemptLease(AttemptScope owner, Guid attemptId) : IOctoberProviderAttemptLease, IOctoberWorkerDispatchObserver
    {
        private bool _terminal;
        public Guid AttemptId => attemptId;
        public CancellationToken CancellationToken => CancellationToken.None;
        public List<string> Events { get; } = [];
        public List<ProviderAttemptUsage> Usage { get; } = [];
        public int Completions { get; private set; }
        public int Disposals { get; private set; }
        public int EligibilityChecks { get; private set; }
        public Task<ProviderAdmissionStatus> CheckBeforeSendAsync(CancellationToken cancellationToken = default)
        { EligibilityChecks++; cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(ProviderAdmissionStatus.Granted); }
        public void RecordDispatch() => Events.Add("dispatch");
        public void PhysicalTerminal() { _terminal = true; Events.Add("physical-terminal"); }
        public void CompleteDispatchObservation()
        {
            Completions++;
            if (!_terminal || owner.FailObservation)
            {
                Events.Add("observation-failed");
                throw new InvalidOperationException("Controlled observation completion failure.");
            }
            Events.Add("observation-complete");
        }
        public Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default)
        { Events.Add("settle"); Usage.Add(usage); return Task.CompletedTask; }
        public ValueTask DisposeAsync()
        { Events.Add("dispose"); Disposals++; owner.Release(); return ValueTask.CompletedTask; }
    }
}

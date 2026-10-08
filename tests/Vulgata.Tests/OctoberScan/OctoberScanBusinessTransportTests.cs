using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.Core.Entities;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.October.Knowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Actual selected SDK factory and Business executor over controlled HTTP; no provider/network/native execution.</summary>
public sealed class OctoberScanBusinessTransportTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualFactory_ExplicitBusinessPortUsesSameLeaseAndOnePhysicalCallWithoutSdkRetryAsync(bool fail)
    {
        var input = new ResolutionCase();
        using var transport = new Http(input) { Fail = fail };
        using var client = await Client(transport);
        var port = Assert.IsAssignableFrom<IOctoberBusinessAttemptClient>(client);
        Assert.Equal("selected-profile", port.ProviderProfileId);
        await using var scope = Scope(input);
        var grant = await scope.AcquireProviderAttemptAsync(Attempt(input));
        Assert.Equal(ProviderAdmissionStatus.Granted, grant.Status);
        var lease = new ObservedLease(grant.Lease!);
        int callbacks = 0;
        try
        {
            var call = port.GetAttemptResponseAsync([new(ChatRole.User, "controlled accepted input")], new ChatOptions(), lease, _ => callbacks++);
            if (fail) await Assert.ThrowsAnyAsync<Exception>(() => call);
            else Assert.Equal(ChatFinishReason.Stop, (await call).FinishReason);
            Assert.Equal(1, transport.Calls);
            Assert.Equal(1, callbacks);
            Assert.Equal(1, lease.Dispatches);
            Assert.Equal(0, lease.Settlements);
            Assert.Equal(0, lease.Disposals);
            Assert.Equal("https://never-contacted.invalid/v1/chat/completions", transport.Endpoint);
            Assert.Equal("Bearer synthetic-approved-token", transport.Authorization);
            Assert.Equal("selected-model", transport.Model);
        }
        finally
        {
            lease.CompleteDispatchObservation();
            await lease.SettleAsync(new(fail ? null : 10, fail ? null : 5, null, TimeSpan.Zero, TimeSpan.Zero, fail ? ProviderAttemptOutcome.Failed : ProviderAttemptOutcome.Completed));
            await lease.DisposeAsync();
        }
        Assert.Equal(1, scope.GetSnapshot().Usage.Requests);
        Assert.Equal(fail, scope.GetSnapshot().Usage.HasUnknownUsage);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(ProviderAdmissionStatus.Denied)]
    [InlineData(ProviderAdmissionStatus.AuthorityUnavailable)]
    [InlineData(ProviderAdmissionStatus.HardStopped)]
    public async Task ActualFactory_FinalEligibilityDeniedDuringSdkEnumeration_HasZeroPhysicalCallsAsync(ProviderAdmissionStatus refused)
    {
        var input = new ResolutionCase();
        bool denied = false;
        using var transport = new Http(input);
        using var client = await Client(transport);
        var port = Assert.IsAssignableFrom<IOctoberBusinessAttemptClient>(client);
        await using var scope = Scope(input, (_, _) => Task.FromResult(denied ? refused : ProviderAdmissionStatus.Granted));
        var grant = await scope.AcquireProviderAttemptAsync(Attempt(input));
        var lease = new ObservedLease(Assert.IsAssignableFrom<IOctoberProviderAttemptLease>(grant.Lease));
        IEnumerable<ChatMessage> Messages() { denied = true; yield return new(ChatRole.User, "controlled SDK boundary"); }
        try
        {
            var denialException = await Assert.ThrowsAnyAsync<Exception>(() => port.GetAttemptResponseAsync(Messages(), new ChatOptions(), lease, _ => throw new InvalidOperationException("Denied transport must not dispatch")));
            var marker = Assert.IsType<OctoberBusinessAttemptDeniedException>(denialException);
            Assert.Equal(refused, marker.Status);
            Assert.Equal(0, transport.Calls);
            Assert.Equal(0, lease.Dispatches);
        }
        finally { lease.CompleteDispatchObservation(); await lease.SettleAsync(new(0, 0, null, TimeSpan.Zero, TimeSpan.Zero, ProviderAttemptOutcome.Cancelled)); await lease.DisposeAsync(); }
        Assert.Equal(0, scope.GetSnapshot().Usage.Requests);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualResolutionExecutor_UsesFactoryPhysicalDispatchAndPreservesUnknownFailureUsageAsync(bool fail)
    {
        var input = new ResolutionCase();
        using var transport = new Http(input) { Fail = fail };
        using var client = await Client(transport);
        await using var scope = Scope(input);
        var result = await Execute(input, client, scope);
        Assert.Equal(1, transport.Calls);
        Assert.Single(result.Attempts);
        Assert.Equal(1, scope.GetSnapshot().Usage.Requests);
        Assert.Equal(0, scope.GetSnapshot().OutstandingAttempts);
        if (fail)
        {
            Assert.Null(result.Outcome);
            Assert.Null(result.Attempts[0].InputTokens);
            Assert.Null(result.Attempts[0].OutputTokens);
            Assert.True(scope.GetSnapshot().Usage.HasUnknownUsage);
            var retry = await scope.AcquireProviderAttemptAsync(Attempt(input));
            try { Assert.NotEqual(ProviderAdmissionStatus.Granted, retry.Status); }
            finally { if (retry.Lease is not null) await retry.Lease.DisposeAsync(); }
        }
        else
        {
            Assert.Equal(ResolutionOutcomeKind.Supported, result.Outcome!.Kind);
            Assert.NotNull(result.Witness);
            Assert.Equal(10L, scope.GetSnapshot().Usage.InputTokens);
            Assert.Equal(5L, scope.GetSnapshot().Usage.OutputTokens);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ActualResolutionExecutor_IgnoredHttpCancellationKeepsLeaseUntilPhysicalSettlementAsync()
    {
        var input = new ResolutionCase();
        using var transport = new Http(input) { Hold = true };
        using var client = await Client(transport);
        await using var scope = Scope(input);
        using var cancellation = new CancellationTokenSource();
        var pending = Execute(input, client, scope, cancellation.Token);
        try
        {
            Assert.Same(transport.Entered.Task, await Task.WhenAny(transport.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(3)));
            cancellation.Cancel();
            await Task.WhenAny(pending, Task.Delay(100));
            Assert.False(pending.IsCompleted);
            Assert.Equal(1, scope.GetSnapshot().OutstandingAttempts);
            transport.Release.TrySetResult();
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Null(result.Outcome);
            Assert.Equal(1, transport.Calls);
            Assert.Equal(0, scope.GetSnapshot().OutstandingAttempts);
        }
        finally { transport.Release.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ActualResolutionExecutor_UncooperativeResponseBodyRetainsLeaseUntilSdkSettlementAsync()
    {
        var input = new ResolutionCase();
        using var transport = new Http(input) { HoldBody = true };
        using var client = await Client(transport);
        await using var scope = Scope(input);
        using var cancellation = new CancellationTokenSource();
        var pending = Execute(input, client, scope, cancellation.Token);
        try
        {
            Assert.Same(transport.BodyEntered.Task, await Task.WhenAny(transport.BodyEntered.Task, pending).WaitAsync(TimeSpan.FromSeconds(3)));
            cancellation.Cancel();
            await Task.WhenAny(pending, Task.Delay(100));
            Assert.False(pending.IsCompleted);
            Assert.False(transport.BodyCompleted);
            Assert.Equal(1, scope.GetSnapshot().OutstandingAttempts);
            transport.ReleaseBody.TrySetResult();
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(transport.BodyCompleted);
            Assert.Null(result.Outcome);
            Assert.Equal(1, transport.Calls);
            Assert.Equal(0, scope.GetSnapshot().OutstandingAttempts);
        }
        finally { transport.ReleaseBody.TrySetResult(); await pending.WaitAsync(TimeSpan.FromSeconds(3)); }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ActualExecutor_DelayBeforeFactoryFinalGuard_CannotSpendAnOldRateReservationAsync()
    {
        var input = new ResolutionCase();
        var clock = new OctoberScanAcceptanceHarness.ManualClock();
        using var transport = new Http(input) { Timestamp = clock.GetTimestamp };
        using var selected = await Client(transport);
        using var client = new BeforeFactoryClient(selected, () => clock.Advance(TimeSpan.FromSeconds(61)));
        await using var scope = new OctoberExecutionScope(Run, "selected-profile", new()
        {
            OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100000, MaximumRequestsPerMinute = 1,
            SourceRules = [new(input.Corpus.Documents[0].RepositoryId, "src", "selected", "controlled accepted input")],
        }, (_, _) => Task.FromResult(ProviderAdmissionStatus.Granted), Operation(input), clock);
        Task<ResolutionExecutionResult> Invoke() => new OctoberResolutionExecutor(client, "Controlled inquiry", clock)
            .ExecuteAsync(input.Corpus, input.Inquiry(), Operation(input), Run, "selected-profile", 1000, scope, new ResolutionDisclosure());
        Assert.Equal(ResolutionOutcomeKind.Supported, (await Invoke()).Outcome!.Kind);
        var second = Invoke();
        try
        {
            await Task.WhenAny(second, Task.Delay(50));
            Assert.Equal(1, transport.Calls);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            for (int tick = 0; tick < 3 && !second.IsCompleted; tick++) { clock.Advance(TimeSpan.FromSeconds(60)); await Task.WhenAny(second, Task.Delay(10)); }
            await second.WaitAsync(TimeSpan.FromSeconds(3));
        }
        Assert.Equal(2, transport.Calls);
        Assert.True(TimeSpan.FromTicks(transport.SentAt[1] - transport.SentAt[0]) >= TimeSpan.FromSeconds(60));
    }

    private sealed class BeforeFactoryClient(IChatClient inner, Action firstEntry) : IChatClient, IOctoberBusinessAttemptClient
    {
        private readonly IOctoberBusinessAttemptClient _port = Assert.IsAssignableFrom<IOctoberBusinessAttemptClient>(inner);
        private int _calls;
        public string ProviderProfileId => _port.ProviderProfileId;
        public Task<ChatResponse> GetAttemptResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, IOctoberProviderAttemptLease lease, Action<long> onDispatched, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 1) firstEntry();
            return _port.GetAttemptResponseAsync(messages, options, lease, onDispatched, cancellationToken);
        }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => inner.GetResponseAsync(messages, options, cancellationToken);
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => inner.GetStreamingResponseAsync(messages, options, cancellationToken);
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : inner.GetService(serviceType, serviceKey);
        public void Dispose() { }
    }
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualFactory_TransportLookalikeDenialRemainsSentUnknownFailureAsync(bool canonicalMarker)
    {
        var input = new ResolutionCase();
        using var transport = new Http(input) { ThrowLookalike = !canonicalMarker, ThrowCanonicalMarker = canonicalMarker };
        using var client = await Client(transport);
        var port = Assert.IsAssignableFrom<IOctoberBusinessAttemptClient>(client);
        await using var scope = Scope(input);
        var grant = await scope.AcquireProviderAttemptAsync(Attempt(input));
        var lease = new ObservedLease(Assert.IsAssignableFrom<IOctoberProviderAttemptLease>(grant.Lease));
        try
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => port.GetAttemptResponseAsync([new(ChatRole.User, "controlled input")], new ChatOptions(), lease, _ => { }));
            Assert.IsNotType<OctoberBusinessAttemptDeniedException>(error);
            Assert.Equal(1, transport.Calls);
            Assert.Equal(1, lease.Dispatches);
        }
        finally { lease.CompleteDispatchObservation(); await lease.SettleAsync(new(null, null, null, TimeSpan.Zero, TimeSpan.Zero, ProviderAttemptOutcome.Failed)); await lease.DisposeAsync(); }
        Assert.Equal(1, scope.GetSnapshot().Usage.Requests);
        Assert.True(scope.GetSnapshot().Usage.HasUnknownUsage);
        Assert.Null(scope.GetSnapshot().Usage.InputTokens);
        Assert.Null(scope.GetSnapshot().Usage.OutputTokens);
    }
    private static readonly Guid Run = Guid.Parse("e20e3918-a69e-4a90-9c79-5cdde1c0a427");
    private static OperationKey Operation(ResolutionCase input) => new(input.Corpus.Handle.Binding.BatchId, OperationKind.FinalizeCorpus, Guid.Parse("f75a2106-1498-45dc-bcde-9503fcf04cff"));
    private static OctoberExecutionScope Scope(ResolutionCase input, Func<OctoberProviderPhase, CancellationToken, Task<ProviderAdmissionStatus>>? eligibility = null) => new(Run, "selected-profile",
        new() { OutputStage = "cl-and-bl", WorkerCap = 1, MaximumTotalTokens = 100000, MaximumRequestsPerMinute = 1000000,
            SourceRules = [new(input.Corpus.Documents[0].RepositoryId, "src", "selected", "controlled accepted input")] },
        eligibility ?? ((_, _) => Task.FromResult(ProviderAdmissionStatus.Granted)), Operation(input));
    private static ProviderAttemptRequest Attempt(ResolutionCase input) => new(Run, Operation(input), Guid.NewGuid(), BusinessWorkKind.Resolution, "selected-profile", TimeSpan.FromSeconds(30), 1000);
    private static Task<ResolutionExecutionResult> Execute(ResolutionCase input, IChatClient client, IOctoberExecutionScope scope, CancellationToken token = default) =>
        new OctoberResolutionExecutor(client, "Controlled accepted-document inquiry.").ExecuteAsync(input.Corpus, input.Inquiry(), Operation(input), Run, "selected-profile", 1000, scope, new ResolutionDisclosure(), token);
    private static Task<IChatClient> Client(HttpMessageHandler transport) => new OctoberWorkerClientFactory(new Secret(), transport).CreateAsync(
        new("explicit-selected-source", "only-source", new("selected-profile", ApiType.ChatCompletions, "https://never-contacted.invalid/v1", "selected-model", "credential://explicit"), "synthetic-protected"));
    private sealed class Secret : ISecretEncryptionService
    {
        public Task<string> EncryptAsync(string purpose, string plaintext, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> DecryptAsync(string purpose, string ciphertext, CancellationToken cancellationToken = default) { Assert.Equal("synthetic-protected", ciphertext); return Task.FromResult("synthetic-approved-token"); }
    }
    private sealed class ObservedLease(IOctoberProviderAttemptLease inner) : IOctoberProviderAttemptLease, IOctoberWorkerDispatchObserver
    {
        internal int Dispatches, Settlements, Disposals;
        public Guid AttemptId => inner.AttemptId;
        public CancellationToken CancellationToken => inner.CancellationToken;
        public Task<ProviderAdmissionStatus> CheckBeforeSendAsync(CancellationToken cancellationToken = default) => inner.CheckBeforeSendAsync(cancellationToken);
        public void RecordDispatch() { Dispatches++; ((IOctoberWorkerDispatchObserver)inner).RecordDispatch(); }
        public void CompleteDispatchObservation() => ((IOctoberWorkerDispatchObserver)inner).CompleteDispatchObservation();
        public Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default) { Settlements++; return inner.SettleAsync(usage, cancellationToken); }
        public ValueTask DisposeAsync() { Disposals++; return inner.DisposeAsync(); }
    }
    private sealed class Http(ResolutionCase input) : HttpMessageHandler
    {
        internal bool Fail, Hold, HoldBody, BodyCompleted, ThrowLookalike, ThrowCanonicalMarker;
        internal TaskCompletionSource BodyEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseBody { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        internal string? Endpoint, Authorization, Model;
        internal Func<long>? Timestamp;
        internal List<long> SentAt { get; } = [];
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; if (Timestamp is not null) SentAt.Add(Timestamp()); Endpoint = request.RequestUri!.ToString(); Authorization = request.Headers.Authorization?.ToString();
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Model = body.RootElement.GetProperty("model").GetString();
            Entered.TrySetResult();
            if (ThrowCanonicalMarker) throw new OctoberBusinessAttemptDeniedException(ProviderAdmissionStatus.Denied);
            if (ThrowLookalike) throw new InvalidOperationException("The selected Business attempt was refused by its final admission guard.");
            if (Hold) await Release.Task; // Controlled uncooperative transport; caller must retain this physical work.
            if (Fail) return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"error\":{\"message\":\"controlled failure\"}}", Encoding.UTF8, "application/json") };
            string json = JsonSerializer.Serialize(new { id = "controlled-resolution", @object = "chat.completion", created = 1, model = Model,
                choices = new[] { new { index = 0, message = new { role = "assistant", content = JsonSerializer.Serialize(input.Witness()) }, finish_reason = "stop" } },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 } });
            return new(HttpStatusCode.OK) { Content = HoldBody ? new HeldBody(json, this) : new StringContent(json, Encoding.UTF8, "application/json") };
        }
        private sealed class HeldBody(string json, Http owner) : HttpContent
        {
            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                owner.BodyEntered.TrySetResult();
                try
                {
                    await owner.ReleaseBody.Task; // Intentionally ignores request cancellation; the test releases physical work.
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(json));
                }
                finally { owner.BodyCompleted = true; }
            }
            protected override bool TryComputeLength(out long length) { length = 0; return false; }
        }    }
}

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Vulgata.Agents.OctoberBusiness;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Knowledge;

public sealed class OctoberBusinessExecutionScopeTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ResolutionDiagnostics_TransportRetryThenNullWitness_ReportsCurrentFailureAndRetainsBothSettlementsAsync()
    {
        var fixture = new ResolutionCase();
        using var client = new ResolutionClient("controlled-profile", (attempt, _) => attempt == 1
            ? throw new IOException("RAW_TRANSPORT_RETRY_SECRET")
            : Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "null"))
            {
                FinishReason = ChatFinishReason.Stop,
                Usage = new() { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 }
            }));
        var scope = new ResolutionScopeProbe();
        var result = await ExecuteAsync(fixture, client, scope);
        Assert.Equal(ResolutionFailureReason.WitnessShape, result.FailureReason);
        Assert.NotEqual(ProviderAdmissionStatus.AuthorityUnavailable, result.AdmissionFailure);
        Assert.Null(result.Outcome);
        Assert.Null(result.Witness);
        Assert.Equal(2, client.Calls);
        Assert.Equal(2, result.Attempts.Length);
        Assert.Null(result.Attempts[0].InputTokens);
        Assert.Null(result.Attempts[0].OutputTokens);
        Assert.Equal(10, result.Attempts[1].InputTokens);
        Assert.Equal(5, result.Attempts[1].OutputTokens);
        Assert.Equal(2, scope.SettleCalls);
        Assert.Equal(2, scope.DisposeCalls);
        Assert.DoesNotContain("RAW_TRANSPORT_RETRY_SECRET", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("invalid-json", ResolutionFailureReason.InvalidJson)]
    [InlineData("invalid-guid", ResolutionFailureReason.WitnessShape)]
    [InlineData("string-enum", ResolutionFailureReason.WitnessShape)]
    [InlineData("incomplete", ResolutionFailureReason.IncompleteResponse)]
    [InlineData("output-limit", ResolutionFailureReason.OutputLimit)]
    public async Task ResolutionDiagnostics_ModelFailuresRemainTypedSafeAndRetainSettledUsageAsync(string shape, ResolutionFailureReason expected)
    {
        const string secret = "RAW_MODEL_DIAGNOSTIC_SECRET";
        var fixture = new ResolutionCase();
        var body = JsonNode.Parse(JsonSerializer.Serialize(fixture.Witness()))!;
        body["ConnectionExplanation"] = secret;
        if (shape == "invalid-guid") body["Caller"]!["Document"]!["BatchId"] = secret;
        if (shape == "string-enum") body["SupportingAssertions"]![0]!["Kind"] = secret;
        string payload = shape == "invalid-json" ? "{" + secret : body.ToJsonString();
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, payload))
        {
            FinishReason = shape == "incomplete" ? null : shape == "output-limit" ? ChatFinishReason.Length : ChatFinishReason.Stop,
            Usage = new() { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 }
        }));
        var scope = new ResolutionScopeProbe();
        var result = await ExecuteAsync(fixture, client, scope);
        Assert.Equal(expected, result.FailureReason);
        Assert.NotEqual(ProviderAdmissionStatus.AuthorityUnavailable, result.AdmissionFailure);
        Assert.Null(result.Outcome);
        Assert.Null(result.Witness);
        Assert.Equal(1, client.Calls);
        var usage = Assert.Single(result.Attempts);
        Assert.Equal(10, usage.InputTokens);
        Assert.Equal(5, usage.OutputTokens);
        Assert.Equal(1, scope.SettleCalls);
        Assert.Equal(1, scope.DisposeCalls);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(result), StringComparison.Ordinal);
        if (result.FailureField is { } field)
            Assert.Contains(field, new[] { "Caller", "Provider", "SupportingAssertions", "OpposingAssertions", "BindingToken", "BatchId", "Kind", "LogicalDocumentId", "DocumentVersionId",
                "Caller.Document.BatchId", "SupportingAssertions.Kind" });
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_QualifiedInput_DisclosesOnlyAcceptedContextWithoutToolsAndAccountsOnceAsync()
    {
        var fixture = new ResolutionCase();
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(fixture.Witness())));
        var scope = new ResolutionScopeProbe();
        var authority = new ResolutionDisclosure();
        var result = await ExecuteAsync(fixture, client, scope, authority);
        Assert.Equal(ResolutionOutcomeKind.Supported, result.Outcome!.Kind);
        Assert.Equal(1, client.Calls);
        var request = Assert.Single(client.Requests);
        Assert.Empty(request.Tools);
        string prompt = string.Join("\n", request.Messages.Select(x => x.Text));
        Assert.Contains(fixture.Corpus.Documents[0].Sections[0].Claims[1].Text, prompt, StringComparison.Ordinal);
        Assert.Contains(fixture.Corpus.Documents[1].Sections[0].Claims[0].Text, prompt, StringComparison.Ordinal);
        Assert.NotEmpty(authority.Contributors);
        Assert.All(authority.Contributors, contributors => Assert.True(fixture.Corpus.Documents.SelectMany(x => x.Contributors).ToHashSet().SetEquals(contributors)));
        Assert.Equal(1, scope.Inner.Grants);
        Assert.Equal(1, scope.Inner.Settlements);
        Assert.Equal(1, scope.Inner.Releases);
        Assert.Equal(1, scope.SettleCalls);
        Assert.Equal(1, scope.DisposeCalls);
        var usage = Assert.Single(result.Attempts);
        Assert.Equal(10, usage.InputTokens);
        Assert.Equal(5, usage.OutputTokens);
        Assert.Equal(ProviderAttemptOutcome.Completed, usage.Outcome);
        Assert.Equal(85, scope.Inner.RemainingTokens);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(ProviderAdmissionStatus.Denied)]
    [InlineData(ProviderAdmissionStatus.BudgetExhausted)]
    [InlineData(ProviderAdmissionStatus.DeadlineExceeded)]
    [InlineData(ProviderAdmissionStatus.HardStopped)]
    [InlineData(ProviderAdmissionStatus.AuthorityUnavailable)]
    public async Task ExecuteAsync_HostRefusesAdmission_SendsNothingAndNeverInventsUsageAsync(ProviderAdmissionStatus status)
    {
        var fixture = new ResolutionCase();
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(fixture.Witness())));
        var scope = new ResolutionScopeProbe { AdmissionFailure = status };
        var result = await ExecuteAsync(fixture, client, scope);
        Assert.Equal(status, result.AdmissionFailure);
        Assert.Equal(0, client.Calls);
        Assert.Empty(result.Attempts);
        Assert.Equal(0, scope.Inner.Grants);
        Assert.Equal(0, scope.Inner.Releases);
        Assert.NotEqual(ResolutionOutcomeKind.Supported, result.Outcome?.Kind);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(DisclosureStatus.Denied)]
    [InlineData(DisclosureStatus.AuthorityUnavailable)]
    public async Task ExecuteAsync_LiveContributorAuthorityFails_DoesNotDiscloseAcceptedInputsAsync(DisclosureStatus status)
    {
        var fixture = new ResolutionCase();
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(fixture.Witness())));
        var result = await ExecuteAsync(fixture, client, new ResolutionScopeProbe(), new ResolutionDisclosure { Status = status });
        Assert.Equal(0, client.Calls);
        Assert.NotEqual(ResolutionOutcomeKind.Supported, result.Outcome?.Kind);
        Assert.Equal(status == DisclosureStatus.Denied ? ProviderAdmissionStatus.Denied : ProviderAdmissionStatus.AuthorityUnavailable, result.AdmissionFailure);
        Assert.Equal(status == DisclosureStatus.Denied ? ResolutionFailureReason.DisclosureDenied : ResolutionFailureReason.DisclosureUnavailable, result.FailureReason);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("io")]
    [InlineData("http")]
    [InlineData("timeout")]
    public async Task ExecuteAsync_FinalAuthorityThrowsThenRecovers_DoesNotRetrySuccessfulProviderAsync(string outage)
    {
        var fixture = new ResolutionCase();
        var witness = fixture.Witness();
        Assert.Equal(ResolutionOutcomeKind.Supported,
            OctoberResolutionPolicy.Validate(fixture.Corpus, fixture.Inquiry(), witness).Kind);
        bool providerReturned = false;
        using var client = new ResolutionClient("controlled-profile", (_, _) =>
        {
            var response = Response(witness);
            providerReturned = true;
            return Task.FromResult(response);
        });
        var authority = new ResolutionPostResponseOutage(() => providerReturned, outage);
        var scope = new ResolutionScopeProbe();

        var result = await ExecuteAsync(fixture, client, scope, authority);

        Assert.True(providerReturned);
        Assert.Equal(1, authority.Failures);
        Assert.Equal(ProviderAdmissionStatus.AuthorityUnavailable, result.AdmissionFailure);
        Assert.Equal(ResolutionFailureReason.DisclosureUnavailable, result.FailureReason);
        Assert.Null(result.Outcome);
        Assert.Equal(1, client.Calls);
        Assert.Single(scope.Requests);
        Assert.Equal(1, scope.Inner.Grants);
        Assert.Equal(1, scope.SettleCalls);
        Assert.Equal(1, scope.DisposeCalls);
        var usage = Assert.Single(result.Attempts);
        Assert.Equal(10, usage.InputTokens);
        Assert.Equal(5, usage.OutputTokens);
        Assert.Equal(DisclosureStatus.Allowed, await authority.CheckAsync(fixture.Corpus.Handle, fixture.Inquiry().Contributors));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_GrantThenHardStop_RechecksBeforeSendAndReleasesOnlySettledLeaseAsync()
    {
        var fixture = new ResolutionCase();
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(fixture.Witness())));
        var scope = new ResolutionScopeProbe();
        scope.AfterGrant = scope.Inner.HardStop;
        var result = await ExecuteAsync(fixture, client, scope);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, result.AdmissionFailure);
        Assert.Equal(0, client.Calls);
        Assert.Equal(1, scope.Inner.Grants);
        Assert.Equal(1, scope.Inner.Settlements);
        Assert.Equal(1, scope.Inner.Releases);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_RateQueuedThenDeniedOrExpired_NeverSendsAsync(bool expire)
    {
        var fixture = new ResolutionCase();
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(fixture.Witness())));
        var scope = new ResolutionScopeProbe();
        scope.Inner.RateAllowed = false;
        var pending = ExecuteAsync(fixture, client, scope);
        await scope.FirstRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, client.Calls);
        Assert.False(pending.IsCompleted);
        scope.Inner.DeadlineExpired = expire;
        scope.Inner.RateDenied = !expire;
        scope.Inner.Pump();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expire ? ProviderAdmissionStatus.DeadlineExceeded : ProviderAdmissionStatus.Denied, result.AdmissionFailure);
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, scope.Inner.Grants);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_TwoInquiriesShareCapOne_HoldsNoParentPermitAndNeverOverlapsProviderCallsAsync()
    {
        var fixture = new ResolutionCase();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ResolutionClient("controlled-profile", async (attempt, _) =>
        {
            if (attempt == 1) await release.Task;
            return Response(fixture.Witness());
        });
        var scope = new ResolutionScopeProbe();
        var first = ExecuteAsync(fixture, client, scope);
        Task<ResolutionExecutionResult>? second = null;
        try
        {
            await client.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            second = ExecuteAsync(fixture, client, scope);
            await scope.SecondRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, client.Calls);
            Assert.Equal(0, scope.Inner.Releases);
            Assert.False(second.IsCompleted);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(ResolutionOutcomeKind.Supported, (await first.WaitAsync(TimeSpan.FromSeconds(5))).Outcome!.Kind);
        Assert.NotNull(second);
        Assert.Equal(ResolutionOutcomeKind.Supported, (await second.WaitAsync(TimeSpan.FromSeconds(5))).Outcome!.Kind);
        Assert.Equal(1, client.PeakActive);
        Assert.Equal(2, scope.Inner.Grants);
        Assert.Equal(2, scope.Inner.Settlements);
        Assert.Equal(2, scope.Inner.Releases);
        Assert.Equal(2, scope.SettleCalls);
        Assert.Equal(2, scope.DisposeCalls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_HostSynthesisPeerHoldsSharedPermit_InquiryWaitsUntilPeerTransportSettlesAsync()
    {
        var fixture = new ResolutionCase();
        var scope = new ResolutionScopeProbe();
        // This is a labelled peer lease, not a synthesized BL or a B2 implementation claim.
        var peer = (await scope.AcquireProviderAttemptAsync(new(ResolutionCase.Id(701), Operation,
            ResolutionCase.Id(702), BusinessWorkKind.Synthesis, "controlled-profile", TimeSpan.FromSeconds(30), 40))).Lease!;
        Assert.Equal(ProviderAdmissionStatus.Granted, await peer.CheckBeforeSendAsync());
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(fixture.Witness())));
        var inquiry = ExecuteAsync(fixture, client, scope);
        await scope.SecondRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, client.Calls);
        await peer.DisposeAsync();
        Assert.False(inquiry.IsCompleted);
        await peer.SettleAsync(new(10, 5, null, TimeSpan.Zero, TimeSpan.FromSeconds(1), ProviderAttemptOutcome.Completed));
        await peer.DisposeAsync();
        Assert.Equal(ResolutionOutcomeKind.Supported, (await inquiry.WaitAsync(TimeSpan.FromSeconds(5))).Outcome!.Kind);
        Assert.Equal(2, scope.Inner.Grants);
        Assert.Equal(2, scope.Inner.Releases);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_HardStopWithActiveTransport_DeniesQueuedWorkAndDoesNotReleaseOrAcceptLateResponseAsync()
    {
        var fixture = new ResolutionCase();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new ResolutionClient("controlled-profile", async (_, _) =>
        {
            await release.Task; // Deliberately ignores cancellation until the owned transport settles.
            return Response(fixture.Witness());
        });
        var scope = new ResolutionScopeProbe();
        var active = ExecuteAsync(fixture, client, scope);
        Task<ResolutionExecutionResult>? queued = null;
        try
        {
            await client.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            queued = ExecuteAsync(fixture, client, scope);
            await scope.SecondRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
            scope.Inner.HardStop();
            Assert.True(client.LastCancellation.IsCancellationRequested);
            Assert.Equal(0, scope.Inner.Releases);
            Assert.False(active.IsCompleted);
            Assert.Equal(ProviderAdmissionStatus.HardStopped, (await queued.WaitAsync(TimeSpan.FromSeconds(5))).AdmissionFailure);
        }
        finally { release.TrySetResult(); }
        var late = await active.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(ResolutionOutcomeKind.Supported, late.Outcome?.Kind);
        Assert.Equal(1, client.Calls);
        Assert.Equal(1, scope.Inner.Settlements);
        Assert.Equal(1, scope.Inner.Releases);
        var retry = await ExecuteAsync(fixture, client, scope);
        Assert.Equal(ProviderAdmissionStatus.HardStopped, retry.AdmissionFailure);
        Assert.Equal(1, client.Calls);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_TransientRetry_ReacquiresSameHostBudgetAndRetainsOverallDeadlineAsync()
    {
        var fixture = new ResolutionCase();
        var clock = new B0bReadClock();
        using var client = new ResolutionClient("controlled-profile", (attempt, _) =>
        {
            if (attempt == 1)
            {
                clock.Advance(TimeSpan.FromSeconds(75));
                throw new IOException("Controlled transport failure after send.");
            }
            return Task.FromResult(Response(fixture.Witness()));
        });
        var scope = new ResolutionScopeProbe();
        var result = await ExecuteAsync(fixture, client, scope, clock: clock);
        Assert.Equal(ResolutionOutcomeKind.Supported, result.Outcome!.Kind);
        Assert.Equal(2, client.Calls);
        Assert.Equal(2, scope.Requests.Count);
        Assert.All(scope.Requests, request =>
        {
            Assert.Equal(Operation, request.FinalizationOperation);
            Assert.Equal("controlled-profile", request.ProviderProfileId);
            Assert.Equal(BusinessWorkKind.Resolution, request.WorkKind);
        });
        Assert.NotEqual(scope.Requests[0].AttemptId, scope.Requests[1].AttemptId);
        Assert.InRange(scope.Requests[1].MaximumDuration, TimeSpan.FromTicks(1), TimeSpan.FromSeconds(45));
        Assert.Equal(2, result.Attempts.Length);
        Assert.Null(result.Attempts[0].InputTokens);
        Assert.Null(result.Attempts[0].OutputTokens);
        Assert.Equal(45, scope.Inner.RemainingTokens);
        Assert.Equal(2, scope.Inner.Settlements);
        Assert.Equal(2, scope.Inner.Releases);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_OverallDeadlineExpiresAfterFailure_DoesNotStartFreshRetryAllowanceAsync()
    {
        var fixture = new ResolutionCase();
        var clock = new B0bReadClock();
        using var client = new ResolutionClient("controlled-profile", (_, _) =>
        {
            clock.Advance(TimeSpan.FromSeconds(121));
            throw new IOException("Controlled delayed failure.");
        });
        var scope = new ResolutionScopeProbe();
        var result = await ExecuteAsync(fixture, client, scope, clock: clock);
        Assert.Equal(1, client.Calls);
        Assert.Single(scope.Requests);
        Assert.NotEqual(ResolutionOutcomeKind.Supported, result.Outcome?.Kind);
        Assert.Equal(1, scope.Inner.Releases);
        Assert.Equal(ResolutionFailureReason.Deadline, result.FailureReason);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_RepeatedTransportFailure_StopsAtTwoAttemptsAndSettlesUnknownUsageAsync()
    {
        var fixture = new ResolutionCase();
        using var client = new ResolutionClient("controlled-profile", (_, _) => throw new IOException("Controlled failure."));
        var scope = new ResolutionScopeProbe();
        var result = await ExecuteAsync(fixture, client, scope);
        Assert.Equal(2, client.Calls);
        Assert.Equal(2, result.Attempts.Length);
        Assert.All(result.Attempts, usage => { Assert.Null(usage.InputTokens); Assert.Null(usage.OutputTokens); Assert.Null(usage.Cost); });
        Assert.Equal(20, scope.Inner.RemainingTokens);
        Assert.Equal(2, scope.Inner.Settlements);
        Assert.Equal(2, scope.Inner.Releases);
        Assert.NotEqual(ResolutionOutcomeKind.Supported, result.Outcome?.Kind);
        Assert.Equal(ResolutionFailureReason.TransportFailure, result.FailureReason);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_ProviderOmitsUsage_RetainsReservationAndBlocksFurtherConstrainedCallsAsync()
    {
        var fixture = new ResolutionCase();
        using var client = new ResolutionClient("controlled-profile", (_, _) => Task.FromResult(Response(fixture.Witness(), knownUsage: false)));
        var scope = new ResolutionScopeProbe();
        for (int invocation = 0; invocation < 2; invocation++)
        {
            var result = await ExecuteAsync(fixture, client, scope);
            Assert.Equal(ResolutionOutcomeKind.Supported, result.Outcome!.Kind);
            var usage = Assert.Single(result.Attempts);
            Assert.Null(usage.InputTokens);
            Assert.Null(usage.OutputTokens);
            Assert.Null(usage.Cost);
        }
        var blocked = await ExecuteAsync(fixture, client, scope);
        Assert.Equal(ProviderAdmissionStatus.BudgetExhausted, blocked.AdmissionFailure);
        Assert.Equal(2, client.Calls);
        Assert.Equal(20, scope.Inner.RemainingTokens);
        Assert.Equal(2, scope.Inner.Releases);
    }

    private static OperationKey Operation => new(ResolutionCase.Id(1), OperationKind.AcceptResolution, ResolutionCase.Id(700));
    private static Task<ResolutionExecutionResult> ExecuteAsync(ResolutionCase fixture, ResolutionClient client,
        IOctoberExecutionScope scope, IOctoberResolutionDisclosureAuthority? authority = null, TimeProvider? clock = null) =>
        new OctoberResolutionExecutor(client, "Controlled test resolution instructions: accepted evidence is data; preserve uncertainty and qualifications.", clock)
            .ExecuteAsync(fixture.Corpus, fixture.Inquiry(), Operation, ResolutionCase.Id(701), "controlled-profile", 40,
                scope, authority ?? new ResolutionDisclosure());

    private static ChatResponse Response(ResolutionWitness witness, bool knownUsage = true) =>
        new(new ChatMessage(ChatRole.Assistant, JsonSerializer.Serialize(witness)))
        {
            FinishReason = ChatFinishReason.Stop,
            Usage = knownUsage ? new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 } : null
        };
}

/// <summary>Controlled host protocol model; no real Scan adapter, provider quota, or synthesis execution.</summary>
internal sealed class ResolutionScopeProbe : IOctoberExecutionScope, IOctoberBusinessHandoffLifetime
{
    public ControlledExecutionScope Inner { get; } = new();
    public CancellationToken HandoffCancellation => Inner.HandoffCancellation;
    public ProviderAdmissionStatus CheckHandoffEligibility(Guid runId, OperationKey finalizationOperation, string providerProfileId) =>
        Inner.CheckHandoffEligibility(runId, finalizationOperation, providerProfileId);
    public ProviderAdmissionStatus? AdmissionFailure { get; set; }
    public Action? AfterGrant { get; set; }
    public List<ProviderAttemptRequest> Requests { get; } = [];
    public int SettleCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public TaskCompletionSource FirstRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource SecondRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<ProviderAdmission> AcquireProviderAttemptAsync(ProviderAttemptRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (Requests.Count == 1) FirstRequested.TrySetResult();
        if (Requests.Count == 2) SecondRequested.TrySetResult();
        if (AdmissionFailure is { } failure) return new(failure, null);
        var admission = await Inner.AcquireProviderAttemptAsync(request, cancellationToken);
        if (admission.Status == ProviderAdmissionStatus.Granted) AfterGrant?.Invoke();
        return admission.Lease is { } lease ? admission with { Lease = new LeaseProbe(this, lease) } : admission;
    }
    private sealed class LeaseProbe(ResolutionScopeProbe owner, IOctoberProviderAttemptLease lease) : IOctoberProviderAttemptLease, IOctoberWorkerDispatchObserver
    {
        private IOctoberWorkerDispatchObserver Observer => lease as IOctoberWorkerDispatchObserver
            ?? throw new InvalidOperationException("Missing inner lease dispatch observer.");
        public void RecordDispatch() => Observer.RecordDispatch();
        public void CompleteDispatchObservation() => Observer.CompleteDispatchObservation();
        public Guid AttemptId => lease.AttemptId;
        public CancellationToken CancellationToken => lease.CancellationToken;
        public Task<ProviderAdmissionStatus> CheckBeforeSendAsync(CancellationToken cancellationToken = default) => lease.CheckBeforeSendAsync(cancellationToken);
        public Task SettleAsync(ProviderAttemptUsage usage, CancellationToken cancellationToken = default)
        {
            owner.SettleCalls++;
            return lease.SettleAsync(usage, cancellationToken);
        }
        public ValueTask DisposeAsync()
        {
            owner.DisposeCalls++;
            return lease.DisposeAsync();
        }
    }
}

internal sealed class ResolutionDisclosure : IOctoberResolutionDisclosureAuthority
{
    public DisclosureStatus Status { get; set; } = DisclosureStatus.Allowed;
    public List<ImmutableArray<Contributor>> Contributors { get; } = [];
    public Task<DisclosureStatus> CheckAsync(CorpusHandle corpus, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Contributors.Add(contributors);
        return Task.FromResult(Status);
    }
}

/// <summary>Only the first authority check after a successful controlled response fails.</summary>
internal sealed class ResolutionPostResponseOutage(Func<bool> providerReturned, string outage) : IOctoberResolutionDisclosureAuthority
{
    public int Failures { get; private set; }
    public Task<DisclosureStatus> CheckAsync(CorpusHandle corpus, ImmutableArray<Contributor> contributors, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (providerReturned() && Failures == 0)
        {
            Failures++;
            throw outage switch
            {
                "io" => new IOException("Controlled final authority store outage."),
                "http" => new HttpRequestException("Controlled final authority transport outage."),
                _ => new TimeoutException("Controlled final authority timeout.")
            };
        }
        return Task.FromResult(DisclosureStatus.Allowed);
    }
}

/// <summary>Explicitly profile-bound, controlled task transport. No HTTP, SDK retry or live-provider claim; the passed lease remains owned by the caller.</summary>
internal sealed class ResolutionClient(string providerProfileId, Func<int, CancellationToken, Task<ChatResponse>> response) : IChatClient, IOctoberBusinessAttemptClient
{
    public string ProviderProfileId { get; } = !string.IsNullOrWhiteSpace(providerProfileId) && providerProfileId == providerProfileId.Trim()
        ? providerProfileId : throw new ArgumentException("An explicit exact controlled provider profile is required.", nameof(providerProfileId));
    public IOctoberProviderAttemptLease? LastAttemptLease { get; private set; }
    public async Task<ChatResponse> GetAttemptResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options,
        IOctoberProviderAttemptLease lease, Action<long> onDispatched, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(onDispatched);
        var observer = lease as IOctoberWorkerDispatchObserver
            ?? throw new InvalidOperationException("The same controlled lease must expose dispatch observation.");
        LastAttemptLease = lease;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
        var eligibility = await lease.CheckBeforeSendAsync(linked.Token);
        if (eligibility != ProviderAdmissionStatus.Granted)
            throw new OctoberBusinessAttemptDeniedException(eligibility);
        linked.Token.ThrowIfCancellationRequested();
        long dispatched = Stopwatch.GetTimestamp();
        observer.RecordDispatch();
        onDispatched(dispatched);
        // Await the actual controlled body task even if it ignores cancellation. The executor owns
        // CompleteDispatchObservation, SettleAsync and DisposeAsync after this task settles.
        return await GetResponseAsync(messages, options, linked.Token);
    }
    private int _calls;
    private int _active;
    public int Calls => Volatile.Read(ref _calls);
    public int PeakActive { get; private set; }
    public CancellationToken LastCancellation { get; private set; }
    public int? LastMaximumOutputTokens { get; private set; }
    public List<(ChatMessage[] Messages, string[] Tools)> Requests { get; } = [];
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        int call = Interlocked.Increment(ref _calls);
        int active = Interlocked.Increment(ref _active);
        lock (Requests)
        {
            PeakActive = Math.Max(PeakActive, active);
            Requests.Add((messages.ToArray(), options?.Tools?.Select(x => x.Name).ToArray() ?? []));
        }
        LastCancellation = cancellationToken;
        LastMaximumOutputTokens = options?.MaxOutputTokens;
        Entered.TrySetResult();
        try { return await response(call, cancellationToken); }
        finally { Interlocked.Decrement(ref _active); }
    }
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The controlled bounded inquiry uses one non-streaming typed result.");
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}

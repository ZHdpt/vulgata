using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Agents.OctoberBusiness;

public sealed record BusinessSynthesisResult(BusinessLogicContent? Content, BLDependency? Dependencies,
    ProviderAdmissionStatus? AdmissionFailure, ImmutableArray<ProviderAttemptUsage> Attempts)
{
    public BusinessSynthesisProposal? Proposal { get; init; }
    public ImmutableArray<SynthesisGap> Gaps { get; init; } = [];
    public ImmutableArray<FileCoverage> Coverage { get; init; } = [];
    public ImmutableArray<ResolutionItemDisposition> PlanDispositions { get; init; } = [];
}

/// <summary>Bounded document-first synthesis; the host retains admission and storage authority.</summary>
public sealed class OctoberBusinessSynthesisExecutor
{
    private readonly IChatClient _client;
    private readonly string _instructions;
    private readonly TimeProvider _time;
    private static readonly UTF8Encoding _utf8 = new(false, true);
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public OctoberBusinessSynthesisExecutor(IChatClient client, string synthesisInstructions, TimeProvider? timeProvider = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentException.ThrowIfNullOrWhiteSpace(synthesisInstructions);
        _instructions = synthesisInstructions;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<BusinessSynthesisResult> ExecuteAsync(FlowPlanningInput input, Guid groupVersionId,
        OperationKey operation, Guid runId, string providerProfileId, long maximumTokens,
        IOctoberExecutionScope executionScope, IOctoberResolutionDisclosureAuthority disclosure,
        CancellationToken cancellationToken = default, FlowPlanningLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(executionScope);
        ArgumentNullException.ThrowIfNull(disclosure);
        var attempts = ImmutableArray.CreateBuilder<ProviderAttemptUsage>();
        BusinessSynthesisResult Result(ProviderAdmissionStatus? failure) => new(null, null, failure, attempts.ToImmutable());
        if (executionScope is not IOctoberBusinessHandoffLifetime lifetime) return Result(ProviderAdmissionStatus.AuthorityUnavailable);
        ProviderAdmissionStatus HandoffStatus()
        {
            try { return lifetime.CheckHandoffEligibility(runId, operation, providerProfileId); }
            catch (Exception) { return ProviderAdmissionStatus.AuthorityUnavailable; }
        }
        CancellationToken handoffCancellation;
        try { handoffCancellation = lifetime.HandoffCancellation; }
        catch (Exception) { return Result(ProviderAdmissionStatus.AuthorityUnavailable); }
        var initialHandoff = HandoffStatus();
        if (initialHandoff != ProviderAdmissionStatus.Granted) return Result(initialHandoff);
        FlowGroupVersion? group;
        string payload;
        try
        {
            if (input is null || runId == Guid.Empty || operation.OperationId == Guid.Empty ||
                operation.Kind != OperationKind.FinalizeCorpus || operation.BatchId != input.Corpus.Handle.Binding.BatchId ||
                string.IsNullOrWhiteSpace(providerProfileId) || maximumTokens <= 0)
                return Result(ProviderAdmissionStatus.Denied);
            if (_utf8.GetByteCount(OctoberKnowledgePolicy.SerializeCanonical(input)) > OctoberBusinessSynthesisPolicy.MaximumPayloadUtf8Bytes)
                return Result(ProviderAdmissionStatus.BudgetExhausted);
            var plan = OctoberFlowPlanner.Plan(input, limits);
            group = plan.Status == FlowPlanningStatus.Succeeded ? plan.Groups.SingleOrDefault(x => x.GroupVersionId == groupVersionId) : null;
            if (group is null) return Result(ProviderAdmissionStatus.Denied);
            var gaps = OctoberBusinessSynthesisPolicy.DescribeGaps(input, groupVersionId, limits);
            payload = JsonSerializer.Serialize(new { input, group, gaps });
            if (_utf8.GetByteCount(payload) > OctoberBusinessSynthesisPolicy.MaximumPayloadUtf8Bytes)
                return Result(ProviderAdmissionStatus.BudgetExhausted);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException)
        { return Result(ProviderAdmissionStatus.Denied); }

        long started = _time.GetTimestamp();
        DateTimeOffset startedUtc = _time.GetUtcNow();
        TimeSpan Elapsed() => Max(_time.GetElapsedTime(started), _time.GetUtcNow() - startedUtc);
        for (int index = 0; index < 2; index++)
        {
            var remaining = TimeSpan.FromSeconds(180) - Elapsed();
            if (remaining <= TimeSpan.Zero) return Result(ProviderAdmissionStatus.DeadlineExceeded);
            if (cancellationToken.IsCancellationRequested) return Result(ProviderAdmissionStatus.HardStopped);
            using var deadline = new CancellationTokenSource(remaining, _time);
            using var admissionCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken, handoffCancellation);
            long requested = _time.GetTimestamp();
            ProviderAdmission admission;
            try
            {
                admission = await executionScope.AcquireProviderAttemptAsync(new(runId, operation, Guid.NewGuid(),
                    BusinessWorkKind.Synthesis, providerProfileId, remaining, maximumTokens), admissionCancellation.Token);
            }
            catch (OperationCanceledException)
            { return Result(HandoffStatus() is var stopped && stopped != ProviderAdmissionStatus.Granted
                ? stopped : deadline.IsCancellationRequested ? ProviderAdmissionStatus.DeadlineExceeded : ProviderAdmissionStatus.HardStopped); }
            catch (Exception) { return Result(ProviderAdmissionStatus.AuthorityUnavailable); }
            if (admission.Status != ProviderAdmissionStatus.Granted || admission.Lease is null)
                return Result(admission.Status == ProviderAdmissionStatus.Granted ? ProviderAdmissionStatus.AuthorityUnavailable : admission.Status);

            var lease = admission.Lease;
            var leaseCancellation = lease.CancellationToken;
            using var transportCancellation = CancellationTokenSource.CreateLinkedTokenSource(admissionCancellation.Token, leaseCancellation);
            var wait = _time.GetElapsedTime(requested);
            long executionStarted = _time.GetTimestamp();
            bool sent = false;
            bool retry = false;
            ChatResponse? response = null;
            BusinessSynthesisProposal? proposal = null;
            BusinessSynthesisValidationResult? validated = null;
            ProviderAdmissionStatus? failure = null;
            ProviderAttemptOutcome outcome = ProviderAttemptOutcome.Failed;
            try
            {
                var authority = await CheckAuthorityAsync(disclosure, input.Corpus.Handle, group.Dependencies.Contributors, transportCancellation.Token);
                if (authority != DisclosureStatus.Allowed) failure = AuthorityStatus(authority);
                else
                {
                    var eligibility = await lease.CheckBeforeSendAsync(transportCancellation.Token);
                    if (eligibility != ProviderAdmissionStatus.Granted) failure = eligibility;
                    else if (HandoffStatus() is var stopped && stopped != ProviderAdmissionStatus.Granted) failure = stopped;
                    else if (Elapsed() >= TimeSpan.FromSeconds(180)) failure = ProviderAdmissionStatus.DeadlineExceeded;
                    else
                    {
                        transportCancellation.Token.ThrowIfCancellationRequested();
                        sent = true;
                        try
                        {
                            // Keep physical transport owned until it settles, including ignored cancellation.
                            response = await _client.GetResponseAsync([new(ChatRole.System, _instructions), new(ChatRole.User, payload)],
                                new ChatOptions { Tools = [], ResponseFormat = ChatResponseFormat.Json, MaxOutputTokens = (int)Math.Min(maximumTokens, int.MaxValue) },
                                transportCancellation.Token);
                        }
                        catch (Exception error) when (error is IOException or HttpRequestException or TimeoutException) { retry = true; }
                        if (transportCancellation.IsCancellationRequested || Elapsed() >= TimeSpan.FromSeconds(180))
                            failure = leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested
                                ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded;
                        else if (response is not null && response.FinishReason == ChatFinishReason.Stop &&
                            response.Messages.All(x => x.Role == ChatRole.Assistant && x.Contents.All(c => c is TextContent or UsageContent)) &&
                            _utf8.GetByteCount(response.Text) <= OctoberBusinessSynthesisPolicy.MaximumPayloadUtf8Bytes)
                        {
                            authority = await CheckAuthorityAsync(disclosure, input.Corpus.Handle, group.Dependencies.Contributors, transportCancellation.Token);
                            if (authority != DisclosureStatus.Allowed) failure = AuthorityStatus(authority);
                            else
                            {
                                transportCancellation.Token.ThrowIfCancellationRequested();
                                proposal = JsonSerializer.Deserialize<BusinessSynthesisProposal>(OctoberKnowledgePolicy.CanonicalizeJson(response.Text), _json);
                                if (proposal is not null)
                                    validated = OctoberBusinessSynthesisPolicy.Validate(input, groupVersionId, proposal, limits);
                                if (validated?.Status == BusinessSynthesisValidationStatus.Succeeded) outcome = ProviderAttemptOutcome.Completed;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                failure = leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested
                    ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded;
            }
            catch (Exception) { failure = ProviderAdmissionStatus.AuthorityUnavailable; }
            finally
            {
                if (failure is ProviderAdmissionStatus.HardStopped or ProviderAdmissionStatus.DeadlineExceeded) outcome = ProviderAttemptOutcome.Cancelled;
                var usage = new ProviderAttemptUsage(sent ? Nonnegative(response?.Usage?.InputTokenCount) : 0,
                    sent ? Nonnegative(response?.Usage?.OutputTokenCount) : 0, null, wait, _time.GetElapsedTime(executionStarted), outcome);
                attempts.Add(usage);
                try { await lease.SettleAsync(usage, CancellationToken.None); }
                catch (Exception) { failure = ProviderAdmissionStatus.AuthorityUnavailable; }
                try { await lease.DisposeAsync(); }
                catch (Exception) { failure = ProviderAdmissionStatus.AuthorityUnavailable; }
            }
            var handoffStatus = HandoffStatus();
            if (handoffStatus != ProviderAdmissionStatus.Granted) return Result(handoffStatus);
            if (leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested) return Result(ProviderAdmissionStatus.HardStopped);
            if (deadline.IsCancellationRequested || Elapsed() >= TimeSpan.FromSeconds(180)) return Result(ProviderAdmissionStatus.DeadlineExceeded);
            if (failure is not null) return Result(failure);
            if (validated?.Status == BusinessSynthesisValidationStatus.Succeeded)
            {
                // Settlement/disposal may await. Reauthorize the entire supplied ledger at handoff,
                // using the same attempt deadline rather than opening another allowance or retry.
                try
                {
                    var finalAuthority = await CheckAuthorityAsync(disclosure, input.Corpus.Handle, group.Dependencies.Contributors,
                        transportCancellation.Token);
                    if (finalAuthority != DisclosureStatus.Allowed) return Result(AuthorityStatus(finalAuthority));
                }
                catch (OperationCanceledException)
                {
                    var stopped = HandoffStatus();
                    return Result(stopped != ProviderAdmissionStatus.Granted ? stopped :
                        leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested
                            ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded);
                }
                catch (Exception) { return Result(ProviderAdmissionStatus.AuthorityUnavailable); }
                handoffStatus = HandoffStatus();
                if (handoffStatus != ProviderAdmissionStatus.Granted) return Result(handoffStatus);
                if (handoffCancellation.IsCancellationRequested) return Result(ProviderAdmissionStatus.HardStopped);
                if (leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested)
                    return Result(ProviderAdmissionStatus.HardStopped);
                if (deadline.IsCancellationRequested || Elapsed() >= TimeSpan.FromSeconds(180))
                    return Result(ProviderAdmissionStatus.DeadlineExceeded);
                return new(validated.Content, validated.Dependencies, null, attempts.ToImmutable())
                {
                    Proposal = proposal, Gaps = validated.Gaps,
                    Coverage = validated.Coverage, PlanDispositions = validated.PlanDispositions
                };
            }
            if (!retry) return Result(null);
        }
        return Result(null);
    }

    private static ProviderAdmissionStatus AuthorityStatus(DisclosureStatus status) => status == DisclosureStatus.Denied
        ? ProviderAdmissionStatus.Denied : ProviderAdmissionStatus.AuthorityUnavailable;
    private static long? Nonnegative(long? value) => value is >= 0 ? value : null;
    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;
    private static async Task<DisclosureStatus> CheckAuthorityAsync(IOctoberResolutionDisclosureAuthority authority,
        CorpusHandle corpus, ImmutableArray<Contributor> contributors, CancellationToken token)
    {
        Task<DisclosureStatus> pending = authority.CheckAsync(corpus, contributors, token);
        _ = ObserveAsync(pending);
        return await pending.WaitAsync(token);
    }
    private static async Task ObserveAsync(Task pending)
    {
        try { await pending; }
        catch (Exception) { }
    }
}

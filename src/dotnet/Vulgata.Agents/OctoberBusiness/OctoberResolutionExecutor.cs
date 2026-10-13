using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;

namespace Vulgata.Agents.OctoberBusiness;

public enum ResolutionFailureReason
{
    InvalidJson, WitnessShape, WitnessValidationException, DisclosureDenied, DisclosureUnavailable,
    TransportFailure, IncompleteResponse, OutputLimit, Deadline, HardStop, InputInvalid, ExecutionUnavailable
}

public sealed record ResolutionExecutionResult(ResolutionOutcome? Outcome,
    ProviderAdmissionStatus? AdmissionFailure, ImmutableArray<ProviderAttemptUsage> Attempts)
{
    /// <summary>Original parsed proposal for store revalidation, never accepted relationship authority.</summary>
    public ResolutionWitness? Witness { get; init; }
    public ResolutionFailureReason? FailureReason { get; init; }
    public string? FailureField { get; init; }
}

/// <summary>Bounded accepted-document inquiry; the host owns provider admission and durable replay.</summary>
public sealed class OctoberResolutionExecutor
{
    private readonly IChatClient _client;
    private readonly string _instructions;
    private readonly TimeProvider _time;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public OctoberResolutionExecutor(IChatClient client, string resolutionInstructions, TimeProvider? timeProvider = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentException.ThrowIfNullOrWhiteSpace(resolutionInstructions);
        _instructions = resolutionInstructions;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<ResolutionExecutionResult> ExecuteAsync(ResolutionCorpus corpus, ResolutionInquiry inquiry,
        OperationKey finalizationOperation, Guid runId, string providerProfileId, long maximumTokens,
        IOctoberExecutionScope executionScope, IOctoberResolutionDisclosureAuthority disclosure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionScope);
        ArgumentNullException.ThrowIfNull(disclosure);
        var attempts = ImmutableArray.CreateBuilder<ProviderAttemptUsage>();
        ResolutionFailureReason? reason = null;
        string? field = null;
        ResolutionExecutionResult Result(ProviderAdmissionStatus? failure, ResolutionOutcome? outcome = null) =>
            new(outcome, failure, attempts.ToImmutable())
            {
                FailureReason = outcome is not null ? null : failure switch
                {
                    ProviderAdmissionStatus.Denied => ResolutionFailureReason.DisclosureDenied,
                    ProviderAdmissionStatus.AuthorityUnavailable => ResolutionFailureReason.DisclosureUnavailable,
                    ProviderAdmissionStatus.HardStopped => ResolutionFailureReason.HardStop,
                    ProviderAdmissionStatus.DeadlineExceeded => ResolutionFailureReason.Deadline,
                    _ => reason ?? ResolutionFailureReason.ExecutionUnavailable
                },
                FailureField = outcome is null && failure is null ? field : null
            };
        if (_client is not IOctoberBusinessAttemptClient attemptClient) return Result(ProviderAdmissionStatus.AuthorityUnavailable);
        try
        {
            if (!StringComparer.Ordinal.Equals(attemptClient.ProviderProfileId, providerProfileId)) return Result(ProviderAdmissionStatus.Denied);
        }
        catch (Exception) { return Result(ProviderAdmissionStatus.AuthorityUnavailable); }
        if (executionScope is not IOctoberBusinessHandoffLifetime lifetime) return Result(ProviderAdmissionStatus.AuthorityUnavailable);
        ProviderAdmissionStatus HandoffStatus()
        {
            try { return lifetime.CheckHandoffEligibility(runId, finalizationOperation, providerProfileId); }
            catch (Exception) { return ProviderAdmissionStatus.AuthorityUnavailable; }
        }
        CancellationToken handoffCancellation;
        try { handoffCancellation = lifetime.HandoffCancellation; }
        catch (Exception) { return Result(ProviderAdmissionStatus.AuthorityUnavailable); }
        var initialHandoff = HandoffStatus();
        if (initialHandoff != ProviderAdmissionStatus.Granted) return Result(initialHandoff);
        if (runId == Guid.Empty || finalizationOperation.BatchId != corpus.Handle.Binding.BatchId ||
            finalizationOperation.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(providerProfileId) || maximumTokens <= 0)
            return Result(ProviderAdmissionStatus.Denied);
        ResolutionInquiry? actual;
        try
        {
            actual = OctoberResolutionPolicy.Plan(corpus).Inquiries.SingleOrDefault(x =>
                x.Fingerprint == inquiry.Fingerprint && x.Caller == inquiry.Caller);
        }
        catch (ArgumentException) { return Result(ProviderAdmissionStatus.Denied); }
        if (actual is null) return Result(ProviderAdmissionStatus.Denied);

        long started = _time.GetTimestamp();
        DateTimeOffset startedUtc = _time.GetUtcNow();
        TimeSpan Elapsed() => Max(_time.GetElapsedTime(started), _time.GetUtcNow() - startedUtc);
        // V2 sends exact endpoint evidence. The inquiry fingerprint and every live disclosure
        // check still cover the complete owner-held corpus ledger, including uncited inputs.
        string input;
        try
        {
            input = corpus.ResolutionPolicyVersion == OctoberResolutionPolicy.RetainedPolicyVersion
                ? JsonSerializer.Serialize(OctoberRetainedResolutionPolicy.CreateEvidencePacket(corpus, actual))
                : JsonSerializer.Serialize(new { corpus, inquiry = actual });
        }
        catch (ArgumentException) { return Result(ProviderAdmissionStatus.BudgetExhausted); }
        if (Encoding.UTF8.GetByteCount(input) > 1024 * 1024) return Result(ProviderAdmissionStatus.BudgetExhausted);
        bool retainedEvidence = corpus.ResolutionPolicyVersion == OctoberResolutionPolicy.RetainedPolicyVersion;
        int outputCeiling = (int)Math.Min(maximumTokens, int.MaxValue);
        for (int attemptNumber = 0; attemptNumber < 2; attemptNumber++)
        {
            reason = null;
            field = null;
            ChatMessage[] messages = [new(ChatRole.System, _instructions), new(ChatRole.User, input)];
            long reservation = maximumTokens;
            if (retainedEvidence)
            {
                try { reservation = checked(OctoberBusinessSynthesisRequests.EstimateInputTokens(messages) + outputCeiling); }
                catch (Exception error) when (error is ArgumentException or OverflowException)
                { return Result(ProviderAdmissionStatus.BudgetExhausted); }
            }
            TimeSpan remaining = TimeSpan.FromSeconds(120) - Elapsed();
            if (remaining <= TimeSpan.Zero) return Result(ProviderAdmissionStatus.DeadlineExceeded);
            if (cancellationToken.IsCancellationRequested) return Result(ProviderAdmissionStatus.HardStopped);
            using var deadline = new CancellationTokenSource(remaining, _time);
            using var admissionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token, handoffCancellation);
            long requested = _time.GetTimestamp();
            ProviderAdmission admission;
            try
            {
                admission = await executionScope.AcquireProviderAttemptAsync(new(runId, finalizationOperation,
                    Guid.NewGuid(), BusinessWorkKind.Resolution, providerProfileId, remaining, reservation), admissionCancellation.Token);
            }
            catch (OperationCanceledException) { return Result(HandoffStatus() is var stopped && stopped != ProviderAdmissionStatus.Granted
                ? stopped : deadline.IsCancellationRequested ? ProviderAdmissionStatus.DeadlineExceeded : ProviderAdmissionStatus.HardStopped); }
            catch (Exception) { return Result(ProviderAdmissionStatus.AuthorityUnavailable); }
            if (admission.Status != ProviderAdmissionStatus.Granted || admission.Lease is null)
                return Result(admission.Status == ProviderAdmissionStatus.Granted ? ProviderAdmissionStatus.AuthorityUnavailable : admission.Status);

            var lease = admission.Lease;
            CancellationToken leaseCancellation = lease.CancellationToken;
            using var transportCancellation = CancellationTokenSource.CreateLinkedTokenSource(admissionCancellation.Token, leaseCancellation);
            TimeSpan wait = _time.GetElapsedTime(requested);
            long dispatchedAt = 0;
            long terminalAt = 0;
            var observer = lease as IOctoberWorkerDispatchObserver;
            ChatResponse? response = null;
            bool retry = false;
            ResolutionOutcome? outcome = null;
            ResolutionWitness? parsedWitness = null;
            ProviderAdmissionStatus? failure = null;
            ProviderAttemptOutcome attemptOutcome = ProviderAttemptOutcome.Failed;
            try
            {
                if (observer is null) throw new InvalidOperationException("Business dispatch observation is unavailable.");
                var authority = await CheckAuthorityAsync(disclosure, corpus.Handle, actual.Contributors, transportCancellation.Token);
                if (authority != DisclosureStatus.Allowed)
                    failure = authority == DisclosureStatus.Denied ? ProviderAdmissionStatus.Denied : ProviderAdmissionStatus.AuthorityUnavailable;
                else
                {
                    // The selected transport alone reserves the final send rate at its physical boundary.
                    if (HandoffStatus() is var stopped && stopped != ProviderAdmissionStatus.Granted) failure = stopped;
                    else if (Elapsed() >= TimeSpan.FromSeconds(120)) failure = ProviderAdmissionStatus.DeadlineExceeded;
                    else
                    {
                        transportCancellation.Token.ThrowIfCancellationRequested();
                        // Await actual transport settlement even when it ignores cancellation. Never release its lease early.
                        try
                        {
                            response = await attemptClient.GetAttemptResponseAsync(messages,
                                new ChatOptions { ResponseFormat = ChatResponseFormat.Json, MaxOutputTokens = outputCeiling, Tools = [] },
                                lease, timestamp => Interlocked.CompareExchange(ref dispatchedAt, timestamp, 0),
                                transportCancellation.Token);
                        }
                        catch (OctoberBusinessAttemptDeniedException denied)
                        {
                            failure = denied.Status;
                            retry = false;
                        }
                        catch (Exception exception) when (exception is IOException or HttpRequestException or TimeoutException)
                        {
                            // Only the transport boundary may request another admitted attempt.
                            // Authority, response validation and policy failures never repeat generation.
                            retry = true;
                            reason = ResolutionFailureReason.TransportFailure;
                        }
                        finally { terminalAt = Stopwatch.GetTimestamp(); }
                        if (transportCancellation.IsCancellationRequested || Elapsed() >= TimeSpan.FromSeconds(120))
                            failure = leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested
                                ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded;
                        else if (response is not null && response.FinishReason == ChatFinishReason.Stop &&
                            response.Messages.All(x => x.Role == ChatRole.Assistant && x.Contents.All(c => c is TextContent or UsageContent or TextReasoningContent)) &&
                            Encoding.UTF8.GetByteCount(response.Text) <= KnowledgeContract.MaximumContentBytes)
                        {
                            var finalAuthority = await CheckAuthorityAsync(disclosure, corpus.Handle, actual.Contributors, transportCancellation.Token);
                            if (finalAuthority != DisclosureStatus.Allowed)
                                failure = finalAuthority == DisclosureStatus.Denied ? ProviderAdmissionStatus.Denied : ProviderAdmissionStatus.AuthorityUnavailable;
                            else
                            {
                                transportCancellation.Token.ThrowIfCancellationRequested();
                                string? canonical = null;
                                try { canonical = OctoberKnowledgePolicy.CanonicalizeJson(response.Text); }
                                catch (JsonException) { reason = ResolutionFailureReason.InvalidJson; }
                                if (canonical is not null)
                                {
                                    try { parsedWitness = JsonSerializer.Deserialize<ResolutionWitness>(canonical, _json); }
                                    catch (JsonException error)
                                    {
                                        reason = ResolutionFailureReason.WitnessShape;
                                        field = SafeWitnessField(error.Path);
                                    }
                                    if (parsedWitness is null) reason ??= ResolutionFailureReason.WitnessShape;
                                    else
                                    {
                                        try { outcome = OctoberResolutionPolicy.Validate(corpus, actual, parsedWitness); }
                                        catch (Exception) { reason = ResolutionFailureReason.WitnessValidationException; }
                                    }
                                }
                                attemptOutcome = outcome is null ? ProviderAttemptOutcome.Failed : ProviderAttemptOutcome.Completed;
                            }
                        }
                        else if (response is not null)
                            reason = response.FinishReason == ChatFinishReason.Length || Encoding.UTF8.GetByteCount(response.Text) > KnowledgeContract.MaximumContentBytes
                                ? ResolutionFailureReason.OutputLimit : ResolutionFailureReason.IncompleteResponse;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                failure = leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested
                    ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded;
                attemptOutcome = ProviderAttemptOutcome.Cancelled;
            }
            catch (Exception) { failure = ProviderAdmissionStatus.AuthorityUnavailable; }
            finally
            {
                bool observationComplete = false;
                try
                {
                    if (observer is not null)
                    {
                        observer.CompleteDispatchObservation();
                        observationComplete = true;
                    }
                }
                catch (Exception) { }
                if (!observationComplete)
                {
                    failure = ProviderAdmissionStatus.AuthorityUnavailable;
                    retry = false;
                    outcome = null;
                    attemptOutcome = ProviderAttemptOutcome.Failed;
                }
                if (failure is ProviderAdmissionStatus.HardStopped or ProviderAdmissionStatus.DeadlineExceeded)
                    attemptOutcome = ProviderAttemptOutcome.Cancelled;
                long physicalStart = Volatile.Read(ref dispatchedAt);
                bool sent = physicalStart != 0;
                var usage = new ProviderAttemptUsage(!observationComplete ? null : sent ? Nonnegative(response?.Usage?.InputTokenCount) : 0,
                    !observationComplete ? null : sent ? Nonnegative(response?.Usage?.OutputTokenCount) : 0, null, wait,
                    sent && terminalAt >= physicalStart ? Stopwatch.GetElapsedTime(physicalStart, terminalAt) : TimeSpan.Zero, attemptOutcome);
                attempts.Add(usage);
                try { await lease.SettleAsync(usage, CancellationToken.None); }
                catch (Exception) { failure = ProviderAdmissionStatus.AuthorityUnavailable; retry = false; outcome = null; }
                try { await lease.DisposeAsync(); }
                catch (Exception) { failure = ProviderAdmissionStatus.AuthorityUnavailable; retry = false; outcome = null; }
            }
            var handoffStatus = HandoffStatus();
            if (handoffStatus != ProviderAdmissionStatus.Granted) return Result(handoffStatus);
            if (leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested)
                return Result(ProviderAdmissionStatus.HardStopped);
            if (deadline.IsCancellationRequested || Elapsed() >= TimeSpan.FromSeconds(120))
                return Result(ProviderAdmissionStatus.DeadlineExceeded);
            if (failure is not null) return Result(failure);
            // Preserve this attempt's original proposal for independent store validation.
            // All failure, settlement and late-stop exits above intentionally disclose no witness.
            if (outcome is not null)
            {
                try
                {
                    var authority = await CheckAuthorityAsync(disclosure, corpus.Handle, actual.Contributors, transportCancellation.Token);
                    if (authority != DisclosureStatus.Allowed) return Result(authority == DisclosureStatus.Denied
                        ? ProviderAdmissionStatus.Denied : ProviderAdmissionStatus.AuthorityUnavailable);
                }
                catch (OperationCanceledException)
                {
                    var stopped = HandoffStatus();
                    return Result(stopped != ProviderAdmissionStatus.Granted ? stopped :
                        deadline.IsCancellationRequested || Elapsed() >= TimeSpan.FromSeconds(120)
                            ? ProviderAdmissionStatus.DeadlineExceeded : ProviderAdmissionStatus.HardStopped);
                }
                catch (Exception) { return Result(ProviderAdmissionStatus.AuthorityUnavailable); }
                handoffStatus = HandoffStatus();
                if (handoffStatus != ProviderAdmissionStatus.Granted) return Result(handoffStatus);
                if (transportCancellation.IsCancellationRequested) return Result(deadline.IsCancellationRequested
                    ? ProviderAdmissionStatus.DeadlineExceeded : ProviderAdmissionStatus.HardStopped);
                if (Elapsed() >= TimeSpan.FromSeconds(120)) return Result(ProviderAdmissionStatus.DeadlineExceeded);
                return Result(null, outcome) with { Witness = parsedWitness };
            }
            if (!retry) return Result(null);
        }
        return Result(null);
    }

    private static long? Nonnegative(long? value) => value is >= 0 ? value : null;
    // Emit only fixed DTO labels, never arbitrary provider-controlled paths or values.
    private static string? SafeWitnessField(string? path) => path switch
    {
        "$.Caller.Document.BatchId" => "Caller.Document.BatchId",
        "$.Caller" => "Caller",
        "$.Provider" => "Provider",
        _ => null
    };
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

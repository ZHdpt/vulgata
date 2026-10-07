using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.OctoberBusiness;

public sealed record ResolutionExecutionResult(ResolutionOutcome? Outcome,
    ProviderAdmissionStatus? AdmissionFailure, ImmutableArray<ProviderAttemptUsage> Attempts)
{
    /// <summary>Original parsed proposal for store revalidation, never accepted relationship authority.</summary>
    public ResolutionWitness? Witness { get; init; }
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
        ResolutionExecutionResult Result(ProviderAdmissionStatus? failure, ResolutionOutcome? outcome = null) =>
            new(outcome, failure, attempts.ToImmutable());
        if (runId == Guid.Empty || finalizationOperation.BatchId != corpus.Handle.Binding.BatchId ||
            finalizationOperation.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(providerProfileId) || maximumTokens <= 0)
            return Result(ProviderAdmissionStatus.Denied);
        var actual = OctoberResolutionPolicy.Plan(corpus).Inquiries.SingleOrDefault(x =>
            x.Fingerprint == inquiry.Fingerprint && x.Caller == inquiry.Caller);
        if (actual is null) return Result(ProviderAdmissionStatus.Denied);

        long started = _time.GetTimestamp();
        DateTimeOffset startedUtc = _time.GetUtcNow();
        TimeSpan Elapsed() => Max(_time.GetElapsedTime(started), _time.GetUtcNow() - startedUtc);
        // Send the accepted corpus that the fingerprint/contributor union covers, not caller-supplied replacement facts.
        string input = JsonSerializer.Serialize(new { corpus, inquiry = actual });
        if (Encoding.UTF8.GetByteCount(input) > 1024 * 1024) return Result(ProviderAdmissionStatus.BudgetExhausted);
        for (int attemptNumber = 0; attemptNumber < 2; attemptNumber++)
        {
            TimeSpan remaining = TimeSpan.FromSeconds(120) - Elapsed();
            if (remaining <= TimeSpan.Zero) return Result(ProviderAdmissionStatus.DeadlineExceeded);
            if (cancellationToken.IsCancellationRequested) return Result(ProviderAdmissionStatus.HardStopped);
            using var deadline = new CancellationTokenSource(remaining, _time);
            using var admissionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            long requested = _time.GetTimestamp();
            ProviderAdmission admission;
            try
            {
                admission = await executionScope.AcquireProviderAttemptAsync(new(runId, finalizationOperation,
                    Guid.NewGuid(), BusinessWorkKind.Resolution, providerProfileId, remaining, maximumTokens), admissionCancellation.Token);
            }
            catch (OperationCanceledException) { return Result(deadline.IsCancellationRequested ? ProviderAdmissionStatus.DeadlineExceeded : ProviderAdmissionStatus.HardStopped); }
            catch (Exception) { return Result(ProviderAdmissionStatus.AuthorityUnavailable); }
            if (admission.Status != ProviderAdmissionStatus.Granted || admission.Lease is null)
                return Result(admission.Status == ProviderAdmissionStatus.Granted ? ProviderAdmissionStatus.AuthorityUnavailable : admission.Status);

            var lease = admission.Lease;
            CancellationToken leaseCancellation = lease.CancellationToken;
            using var transportCancellation = CancellationTokenSource.CreateLinkedTokenSource(admissionCancellation.Token, leaseCancellation);
            TimeSpan wait = _time.GetElapsedTime(requested);
            long executionStarted = _time.GetTimestamp();
            ChatResponse? response = null;
            bool sent = false;
            bool retry = false;
            ResolutionOutcome? outcome = null;
            ResolutionWitness? parsedWitness = null;
            ProviderAdmissionStatus? failure = null;
            ProviderAttemptOutcome attemptOutcome = ProviderAttemptOutcome.Failed;
            try
            {
                var authority = await disclosure.CheckAsync(corpus.Handle, actual.Contributors, transportCancellation.Token);
                if (authority != DisclosureStatus.Allowed)
                    failure = authority == DisclosureStatus.Denied ? ProviderAdmissionStatus.Denied : ProviderAdmissionStatus.AuthorityUnavailable;
                else
                {
                    var eligibility = await lease.CheckBeforeSendAsync(transportCancellation.Token);
                    if (eligibility != ProviderAdmissionStatus.Granted) failure = eligibility;
                    else if (Elapsed() >= TimeSpan.FromSeconds(120)) failure = ProviderAdmissionStatus.DeadlineExceeded;
                    else
                    {
                        transportCancellation.Token.ThrowIfCancellationRequested();
                        sent = true;
                        // Await actual transport settlement even when it ignores cancellation. Never release its lease early.
                        try
                        {
                            response = await _client.GetResponseAsync([new(ChatRole.System, _instructions), new(ChatRole.User, input)],
                                new ChatOptions { ResponseFormat = ChatResponseFormat.Json, MaxOutputTokens = (int)Math.Min(maximumTokens, int.MaxValue), Tools = [] },
                                transportCancellation.Token);
                        }
                        catch (Exception exception) when (exception is IOException or HttpRequestException or TimeoutException)
                        {
                            // Only the transport boundary may request another admitted attempt.
                            // Authority, response validation and policy failures never repeat generation.
                            retry = true;
                        }
                        if (transportCancellation.IsCancellationRequested || Elapsed() >= TimeSpan.FromSeconds(120))
                            failure = leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested
                                ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded;
                        else if (response is not null && response.FinishReason == ChatFinishReason.Stop &&
                            response.Messages.All(x => x.Role == ChatRole.Assistant && x.Contents.All(c => c is TextContent or UsageContent)) &&
                            Encoding.UTF8.GetByteCount(response.Text) <= KnowledgeContract.MaximumContentBytes)
                        {
                            var finalAuthority = await disclosure.CheckAsync(corpus.Handle, actual.Contributors, transportCancellation.Token);
                            if (finalAuthority != DisclosureStatus.Allowed)
                                failure = finalAuthority == DisclosureStatus.Denied ? ProviderAdmissionStatus.Denied : ProviderAdmissionStatus.AuthorityUnavailable;
                            else
                            {
                                transportCancellation.Token.ThrowIfCancellationRequested();
                                string canonical = OctoberKnowledgePolicy.CanonicalizeJson(response.Text);
                                parsedWitness = JsonSerializer.Deserialize<ResolutionWitness>(canonical, _json);
                                if (parsedWitness is not null) outcome = OctoberResolutionPolicy.Validate(corpus, actual, parsedWitness);
                                attemptOutcome = outcome is null ? ProviderAttemptOutcome.Failed : ProviderAttemptOutcome.Completed;
                            }
                        }
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
                if (failure is ProviderAdmissionStatus.HardStopped or ProviderAdmissionStatus.DeadlineExceeded)
                    attemptOutcome = ProviderAttemptOutcome.Cancelled;
                var usage = new ProviderAttemptUsage(sent ? Nonnegative(response?.Usage?.InputTokenCount) : 0,
                    sent ? Nonnegative(response?.Usage?.OutputTokenCount) : 0, null, wait,
                    _time.GetElapsedTime(executionStarted), attemptOutcome);
                attempts.Add(usage);
                try { await lease.SettleAsync(usage, CancellationToken.None); }
                catch (Exception) { failure = ProviderAdmissionStatus.AuthorityUnavailable; retry = false; outcome = null; }
                try { await lease.DisposeAsync(); }
                catch (Exception) { failure = ProviderAdmissionStatus.AuthorityUnavailable; retry = false; outcome = null; }
            }
            if (leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested)
                return Result(ProviderAdmissionStatus.HardStopped);
            if (deadline.IsCancellationRequested || Elapsed() >= TimeSpan.FromSeconds(120))
                return Result(ProviderAdmissionStatus.DeadlineExceeded);
            if (failure is not null) return Result(failure);
            // Preserve this attempt's original proposal for independent store validation.
            // All failure, settlement and late-stop exits above intentionally disclose no witness.
            if (outcome is not null) return Result(null, outcome) with { Witness = parsedWitness };
            if (!retry) return Result(null);
        }
        return Result(null);
    }

    private static long? Nonnegative(long? value) => value is >= 0 ? value : null;
    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;
}

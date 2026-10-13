using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices.OctoberScan;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Agents.OctoberBusiness;

public sealed record BusinessSynthesisResult(BusinessLogicContent? Content, BLDependency? Dependencies,
    ProviderAdmissionStatus? AdmissionFailure, ImmutableArray<ProviderAttemptUsage> Attempts)
{
    public BusinessSynthesisProposal? Proposal { get; init; }
    public ImmutableArray<SynthesisGap> Gaps { get; init; } = [];
    public ImmutableArray<FileCoverage> Coverage { get; init; } = [];
    public ImmutableArray<ResolutionItemDisposition> PlanDispositions { get; init; } = [];
    [JsonIgnore]
    public BusinessSynthesisFailureReason FailureReason { get; init; }
    [JsonIgnore]
    public BusinessSemanticReview? SemanticReview { get; init; }
    [JsonIgnore]
    public string? RejectedResponse { get; init; }
    [JsonIgnore]
    public string? ValidationDetail { get; init; }
}

/// <summary>Bounded document-first synthesis; the host retains admission and storage authority.</summary>
public sealed partial class OctoberBusinessSynthesisExecutor
{
    public const int MaximumOutputTokens = 393216;
    public const int MaximumAttempts = 4;
    public const int DeadlineSeconds = 900;
    private readonly IChatClient _client;
    private readonly string _instructions;
    private readonly TimeProvider _time;
    private readonly IOctoberBusinessSourceReader? _sourceReader;
    private readonly Func<BusinessSynthesisDiagnostic, CancellationToken, Task>? _diagnosticCapture;
    private static readonly UTF8Encoding _utf8 = new(false, true);
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public OctoberBusinessSynthesisExecutor(IChatClient client, string synthesisInstructions, TimeProvider? timeProvider = null,
        Func<BusinessSynthesisDiagnostic, CancellationToken, Task>? diagnosticCapture = null,
        IOctoberBusinessSourceReader? sourceReader = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentException.ThrowIfNullOrWhiteSpace(synthesisInstructions);
        _instructions = synthesisInstructions;
        _time = timeProvider ?? TimeProvider.System;
        _diagnosticCapture = diagnosticCapture;
        _sourceReader = sourceReader;
    }

    public Task<BusinessSynthesisResult> ExecuteAsync(FlowPlanningInput input, Guid groupVersionId,
        OperationKey operation, Guid runId, string providerProfileId, long maximumTokens,
        IOctoberExecutionScope executionScope, IOctoberResolutionDisclosureAuthority disclosure,
        CancellationToken cancellationToken = default, FlowPlanningLimits? limits = null) =>
        ExecuteStageAsync(input, groupVersionId, operation, runId, providerProfileId, maximumTokens,
            executionScope, disclosure, cancellationToken, limits, null, null, TimeSpan.FromSeconds(DeadlineSeconds), null);

    private async Task<BusinessSynthesisResult> ExecuteStageAsync(FlowPlanningInput input, Guid groupVersionId,
        OperationKey operation, Guid runId, string providerProfileId, long maximumTokens,
        IOctoberExecutionScope executionScope, IOctoberResolutionDisclosureAuthority disclosure,
        CancellationToken cancellationToken, FlowPlanningLimits? limits,
        BusinessSynthesisProposal? candidate, BusinessSemanticReview? correction, TimeSpan stageBudget,
        OctoberBusinessSourceRetrieval? retrieval)
    {
        ArgumentNullException.ThrowIfNull(executionScope);
        ArgumentNullException.ThrowIfNull(disclosure);
        var attempts = ImmutableArray.CreateBuilder<ProviderAttemptUsage>();
        string? rejectedResponse = null;
        string? validationDetail = null;
        BusinessSynthesisResult Result(ProviderAdmissionStatus? failure,
            BusinessSynthesisFailureReason reason = BusinessSynthesisFailureReason.ExecutionUnavailable) =>
            new(null, null, failure, attempts.ToImmutable())
            { FailureReason = reason, RejectedResponse = rejectedResponse, ValidationDetail = validationDetail };
        if (_client is not IOctoberBusinessAttemptClient attemptClient) return Result(ProviderAdmissionStatus.AuthorityUnavailable);
        try
        {
            if (!StringComparer.Ordinal.Equals(attemptClient.ProviderProfileId, providerProfileId)) return Result(ProviderAdmissionStatus.Denied);
        }
        catch (Exception) { return Result(ProviderAdmissionStatus.AuthorityUnavailable); }
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
        string synthesisPacket;
        try
        {
            if (input is null || runId == Guid.Empty || operation.OperationId == Guid.Empty ||
                operation.Kind != OperationKind.FinalizeCorpus || operation.BatchId != input.Corpus.Handle.Binding.BatchId ||
                string.IsNullOrWhiteSpace(providerProfileId) || maximumTokens is <= 0 or > MaximumOutputTokens)
                return Result(ProviderAdmissionStatus.Denied, BusinessSynthesisFailureReason.InvalidInput);
            if (_utf8.GetByteCount(OctoberKnowledgePolicy.SerializeCanonical(input)) > OctoberBusinessSynthesisPolicy.MaximumOwnerInputUtf8Bytes)
                return Result(ProviderAdmissionStatus.BudgetExhausted, BusinessSynthesisFailureReason.OutputLimit);
            var plan = OctoberFlowPlanner.Plan(input, limits);
            group = plan.Status == FlowPlanningStatus.Succeeded ? plan.Groups.SingleOrDefault(x => x.GroupVersionId == groupVersionId) : null;
            if (group is null) return Result(ProviderAdmissionStatus.Denied, BusinessSynthesisFailureReason.InvalidInput);
            var packet = OctoberBusinessSynthesisPolicy.CreateModelPacket(input, groupVersionId, limits);
            synthesisPacket = JsonSerializer.Serialize(packet);
            if (retrieval is not null) synthesisPacket = retrieval.AddEvidence(synthesisPacket);
            payload = candidate is null ? synthesisPacket : JsonSerializer.Serialize(new
            {
                phase = correction is null ? "semantic-review" : "semantic-correction",
                proposalDigest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(candidate)),
                findings = correction?.Findings
            });
            if (_utf8.GetByteCount(payload) > OctoberBusinessSynthesisPolicy.MaximumPayloadUtf8Bytes)
                return Result(ProviderAdmissionStatus.BudgetExhausted, BusinessSynthesisFailureReason.OutputLimit);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or JsonException)
        { return Result(ProviderAdmissionStatus.Denied, BusinessSynthesisFailureReason.InvalidInput); }

        long started = _time.GetTimestamp();
        DateTimeOffset startedUtc = _time.GetUtcNow();
        TimeSpan Elapsed() => Max(_time.GetElapsedTime(started), _time.GetUtcNow() - startedUtc);
        // Reconstruct the synthesis conversation with its exact latest draft for self-review.
        // Draft text is not evidence; raw private reasoning is neither required nor replayed here.
        ChatMessage[] originalMessages = candidate is null
            ? [new(ChatRole.System, _instructions), new(ChatRole.User, payload)]
            : [new(ChatRole.System, _instructions), new(ChatRole.User, synthesisPacket),
                new(ChatRole.Assistant, OctoberKnowledgePolicy.SerializeCanonical(candidate)), new(ChatRole.User, payload)];
        var history = originalMessages.ToList();
        var synthesisClient = _client as IOctoberBusinessSynthesisClient;
        var lastFailureReason = BusinessSynthesisFailureReason.ExecutionUnavailable;
        var sourceCallIds = new HashSet<string>(StringComparer.Ordinal);
        bool toolsEnabled = retrieval is not null && retrieval.Rounds < OctoberBusinessSourceRetrieval.MaximumRounds;
        int allowedAttempts = MaximumAttempts;
        for (int index = 0; index < allowedAttempts; index++)
        {
            var remaining = stageBudget - Elapsed();
            if (remaining <= TimeSpan.Zero) return Result(ProviderAdmissionStatus.DeadlineExceeded);
            if (cancellationToken.IsCancellationRequested) return Result(ProviderAdmissionStatus.HardStopped);
            using var deadline = new CancellationTokenSource(remaining, _time);
            using var admissionCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken, handoffCancellation);
            long requested = _time.GetTimestamp();
            IList<AITool> tools = toolsEnabled ? [retrieval!.Tool] : [];
            if (retrieval is not null && ++retrieval.Requests > OctoberBusinessSourceRetrieval.MaximumRequests)
                return Result(ProviderAdmissionStatus.BudgetExhausted);
            if (toolsEnabled) retrieval!.Rounds++;
            long reservedTokens;
            try { reservedTokens = checked(OctoberBusinessSynthesisRequests.EstimateInputTokens(history, tools) + maximumTokens); }
            catch (ArgumentException) { return Result(ProviderAdmissionStatus.BudgetExhausted, BusinessSynthesisFailureReason.OutputLimit); }
            var strategy = index switch
            {
                2 => BusinessSynthesisAttemptStrategy.ThinkingDisabled,
                3 => BusinessSynthesisAttemptStrategy.CompactedThinkingDisabled,
                _ => BusinessSynthesisAttemptStrategy.Configured
            };
            ProviderAdmission admission;
            try
            {
                admission = await executionScope.AcquireProviderAttemptAsync(new(runId, operation, Guid.NewGuid(),
                    BusinessWorkKind.Synthesis, providerProfileId, remaining, reservedTokens), admissionCancellation.Token);
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
            long dispatchedAt = 0;
            long terminalAt = 0;
            var observer = lease as IOctoberWorkerDispatchObserver;
            ChatResponse? response = null;
            BusinessSynthesisProposal? proposal = null;
            BusinessSynthesisValidationResult? validated = null;
            BusinessSemanticReview? review = null;
            ProviderAdmissionStatus? failure = null;
            ProviderAttemptOutcome outcome = ProviderAttemptOutcome.Failed;
            var failureReason = BusinessSynthesisFailureReason.ExecutionUnavailable;
            bool parsingProposal = false;
            bool toolResponse = false;
            try
            {
                if (observer is null) throw new InvalidOperationException("Business dispatch observation is unavailable.");
                var authority = await CheckAuthorityAsync(disclosure, input.Corpus.Handle, group.Dependencies.Contributors, transportCancellation.Token);
                if (authority != DisclosureStatus.Allowed) failure = AuthorityStatus(authority);
                else
                {
                    // The selected transport alone reserves the final send rate at its physical boundary.
                    if (HandoffStatus() is var stopped && stopped != ProviderAdmissionStatus.Granted) failure = stopped;
                    else if (Elapsed() >= stageBudget) failure = ProviderAdmissionStatus.DeadlineExceeded;
                    else
                    {
                        transportCancellation.Token.ThrowIfCancellationRequested();
                        try
                        {
                            // Keep physical transport owned until it settles, including ignored cancellation.
                            var options = new ChatOptions { Tools = tools, ToolMode = toolsEnabled ? ChatToolMode.Auto : ChatToolMode.None, ResponseFormat = toolsEnabled ? null : ChatResponseFormat.Json, MaxOutputTokens = (int)maximumTokens };
                            response = synthesisClient is not null
                                ? await synthesisClient.GetSynthesisResponseAsync(history, options, lease, strategy,
                                    timestamp => Interlocked.CompareExchange(ref dispatchedAt, timestamp, 0), transportCancellation.Token)
                                : await attemptClient.GetAttemptResponseAsync(history, options, lease,
                                    timestamp => Interlocked.CompareExchange(ref dispatchedAt, timestamp, 0), transportCancellation.Token);
                        }
                        catch (OctoberBusinessAttemptDeniedException denied)
                        {
                            failure = denied.Status;
                        }
                        catch (Exception error) when (error is IOException or HttpRequestException or TimeoutException)
                        {
                            failureReason = BusinessSynthesisFailureReason.TransportFailure;
                        }
                        finally { terminalAt = Stopwatch.GetTimestamp(); }
                        if (transportCancellation.IsCancellationRequested || Elapsed() >= stageBudget)
                            failure = leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested
                                ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded;
                        else if (response is null)
                        {
                            if (failure is null && failureReason != BusinessSynthesisFailureReason.TransportFailure) failureReason = BusinessSynthesisFailureReason.ResponseMissing;
                        }
                        else if (toolsEnabled && response.FinishReason is { } finish && (finish == ChatFinishReason.ToolCalls || finish == ChatFinishReason.Stop))
                        {
                            toolResponse = response.Messages.All(m => m.Role == ChatRole.Assistant &&
                                m.Contents.All(c => c is TextContent or UsageContent or TextReasoningContent or FunctionCallContent));
                            if (toolResponse) outcome = ProviderAttemptOutcome.Completed;
                            else failureReason = BusinessSynthesisFailureReason.ResponseShape;
                        }
                        else if (response.FinishReason != ChatFinishReason.Stop)
                            failureReason = response.FinishReason == ChatFinishReason.Length
                                ? BusinessSynthesisFailureReason.ResponseLengthLimit : BusinessSynthesisFailureReason.FinishReasonNotStop;
                        else if (!response.Messages.All(x => x.Role == ChatRole.Assistant && x.Contents.All(c => c is TextContent or UsageContent or TextReasoningContent)))
                            failureReason = BusinessSynthesisFailureReason.ResponseShape;
                        else if (_utf8.GetByteCount(response.Text) > OctoberBusinessSynthesisPolicy.MaximumPayloadUtf8Bytes)
                            failureReason = BusinessSynthesisFailureReason.ResponsePayloadLimit;
                        else
                        {
                            authority = await CheckAuthorityAsync(disclosure, input.Corpus.Handle, group.Dependencies.Contributors, transportCancellation.Token);
                            if (authority != DisclosureStatus.Allowed) failure = AuthorityStatus(authority);
                            else
                            {
                                transportCancellation.Token.ThrowIfCancellationRequested();
                                parsingProposal = true;
                                OctoberBusinessSynthesisRequests.RequireUniqueProperties(response.Text);
                                if (candidate is not null && correction is null)
                                {
                                    review = JsonSerializer.Deserialize<BusinessSemanticReview>(OctoberKnowledgePolicy.CanonicalizeJson(response.Text), _json);
                                    string? invalidReview = OctoberBusinessSemanticReview.InvalidReason(review, candidate, input, groupVersionId, limits);
                                    if (invalidReview is not null)
                                    {
                                        validationDetail = invalidReview;
                                        failureReason = BusinessSynthesisFailureReason.SemanticReviewInvalid;
                                        throw new JsonException("Incomplete or ungrounded semantic review.");
                                    }
                                    // A valid negative verdict is an observed review, not accepted content.
                                    proposal = candidate;
                                }
                                else proposal = JsonSerializer.Deserialize<BusinessSynthesisProposal>(OctoberKnowledgePolicy.CanonicalizeJson(response.Text), _json);
                                parsingProposal = false;
                                if (proposal is not null)
                                    validated = OctoberBusinessSynthesisPolicy.Validate(input, groupVersionId, proposal, limits);
                                if (validated?.Status == BusinessSynthesisValidationStatus.Succeeded) outcome = ProviderAttemptOutcome.Completed;
                                else failureReason = proposal is null ? BusinessSynthesisFailureReason.ProposalMissing
                                    : validated?.FailureReason is { } reason && reason != BusinessSynthesisFailureReason.None
                                        ? reason : BusinessSynthesisFailureReason.InvalidStructure;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                failure = leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested
                    ? ProviderAdmissionStatus.HardStopped : ProviderAdmissionStatus.DeadlineExceeded;
                failureReason = BusinessSynthesisFailureReason.ExecutionUnavailable;
            }
            catch (JsonException)
            {
                failure = parsingProposal ? null : ProviderAdmissionStatus.AuthorityUnavailable;
                if (parsingProposal)
                {
                    if (failureReason != BusinessSynthesisFailureReason.SemanticReviewInvalid)
                    {
                        failureReason = BusinessSynthesisFailureReason.JsonInvalid;
                        validationDetail = "JSON syntax, duplicate property, required field, or typed field shape is invalid.";
                    }
                    // Bounded model output remains protected diagnostic data, never accepted evidence.
                    rejectedResponse = response?.Text;
                }
                else failureReason = BusinessSynthesisFailureReason.ExecutionUnavailable;
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
                    validated = null;
                    outcome = ProviderAttemptOutcome.Failed;
                    failureReason = BusinessSynthesisFailureReason.ExecutionUnavailable;
                }
                if (failure is ProviderAdmissionStatus.HardStopped or ProviderAdmissionStatus.DeadlineExceeded) outcome = ProviderAttemptOutcome.Cancelled;
                long physicalStart = Volatile.Read(ref dispatchedAt);
                bool sent = physicalStart != 0;
                var usage = new ProviderAttemptUsage(!observationComplete ? null : sent ? Nonnegative(response?.Usage?.InputTokenCount) : 0,
                    !observationComplete ? null : sent ? Nonnegative(response?.Usage?.OutputTokenCount) : 0, null, wait,
                    sent && terminalAt >= physicalStart ? Stopwatch.GetElapsedTime(physicalStart, terminalAt) : TimeSpan.Zero, outcome);
                attempts.Add(usage);
                try { await lease.SettleAsync(usage, CancellationToken.None); }
                catch (Exception) { failure = ProviderAdmissionStatus.AuthorityUnavailable; failureReason = BusinessSynthesisFailureReason.ExecutionUnavailable; }
                try { await lease.DisposeAsync(); }
                catch (Exception) { failure = ProviderAdmissionStatus.AuthorityUnavailable; failureReason = BusinessSynthesisFailureReason.ExecutionUnavailable; }
            }
            var handoffStatus = HandoffStatus();
            if (handoffStatus != ProviderAdmissionStatus.Granted) return Result(handoffStatus);
            if (leaseCancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested) return Result(ProviderAdmissionStatus.HardStopped);
            if (deadline.IsCancellationRequested || Elapsed() >= stageBudget) return Result(ProviderAdmissionStatus.DeadlineExceeded);
            if (failure is not null) return Result(failure, failureReason);
            if (toolResponse)
            {
                if (response!.Usage?.InputTokenCount is not >= 0 || response.Usage.OutputTokenCount is not >= 0)
                    return Result(ProviderAdmissionStatus.AuthorityUnavailable);
                try
                {
                    var calls = response.Messages.SelectMany(m => m.Contents.OfType<FunctionCallContent>()).ToArray();
                    if (calls.Length > 12 || calls.Any(c => c.InformationalOnly || c.Exception is not null || string.IsNullOrEmpty(c.CallId)) ||
                        calls.Select(c => c.CallId).Distinct(StringComparer.Ordinal).Count() != calls.Length ||
                        calls.Any(c => !sourceCallIds.Add(c.CallId)) ||
                        response.FinishReason == ChatFinishReason.ToolCalls && calls.Length == 0 ||
                        response.FinishReason == ChatFinishReason.Stop && calls.Length != 0)
                        return Result(ProviderAdmissionStatus.Denied, BusinessSynthesisFailureReason.ResponseShape);
                    _ = OctoberBusinessSynthesisRequests.EstimateInputTokens(response.Messages.ToArray(), tools);
                    async Task<bool> AuthorizedAsync()
                    {
                        admissionCancellation.Token.ThrowIfCancellationRequested();
                        var decision = await CheckAuthorityAsync(disclosure, input.Corpus.Handle, group.Dependencies.Contributors, admissionCancellation.Token);
                        admissionCancellation.Token.ThrowIfCancellationRequested();
                        return decision == DisclosureStatus.Allowed && HandoffStatus() == ProviderAdmissionStatus.Granted && Elapsed() < stageBudget;
                    }
                    if (!await AuthorizedAsync()) return Result(ProviderAdmissionStatus.Denied);
                    history.AddRange(response.Messages.Select(m => m.Clone()));
                    foreach (var call in calls)
                    {
                        if (!await AuthorizedAsync()) return Result(ProviderAdmissionStatus.Denied);
                        var read = await retrieval!.ReadAsync(call, admissionCancellation.Token);
                        if (!await AuthorizedAsync()) return Result(ProviderAdmissionStatus.Denied);
                        if (_diagnosticCapture is not null)
                        {
                            await _diagnosticCapture(new(runId, operation, groupVersionId, "source-" + retrieval.Pages.Length,
                                null, null, BusinessSynthesisFailureReason.None, attempts.ToImmutable()) { SourceReads = [read] }, admissionCancellation.Token);
                            if (!await AuthorizedAsync()) return Result(ProviderAdmissionStatus.Denied);
                        }
                        history.Add(new(ChatRole.Tool, [new FunctionResultContent(call.CallId, JsonSerializer.Serialize(read))]));
                    }
                    toolsEnabled = calls.Length > 0 && retrieval!.Rounds < OctoberBusinessSourceRetrieval.MaximumRounds;
                    if (!toolsEnabled)
                        history.Add(new(ChatRole.User, JsonSerializer.Serialize(new { phase = candidate is null ? "synthesis-final" : correction is null ? "semantic-review" : "semantic-correction", finalize = true,
                            proposalDigest = candidate is null ? null : OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(candidate)) })));
                    originalMessages = history.Select(m => m.Clone()).ToArray();
                    index--; // Source investigation consumes its own shared allowance, not structural corrections.
                    continue;
                }
                catch (OperationCanceledException) { return Result(ProviderAdmissionStatus.HardStopped); }
                catch (Exception) { return Result(ProviderAdmissionStatus.AuthorityUnavailable); }
            }
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
                if (deadline.IsCancellationRequested || Elapsed() >= stageBudget)
                    return Result(ProviderAdmissionStatus.DeadlineExceeded);
                return new(validated.Content, validated.Dependencies, null, attempts.ToImmutable())
                {
                    Proposal = proposal, SemanticReview = review, Gaps = validated.Gaps,
                    Coverage = validated.Coverage, PlanDispositions = validated.PlanDispositions,
                    RejectedResponse = rejectedResponse, ValidationDetail = validationDetail
                };
            }
            if (index == allowedAttempts - 1 || synthesisClient?.SupportsSynthesisCorrections != true ||
                response?.FinishReason != ChatFinishReason.Stop || response.Usage?.InputTokenCount is not >= 0 ||
                response.Usage.OutputTokenCount is not >= 0 ||
                !(OctoberBusinessSynthesisRequests.Correctable(failureReason) ||
                    candidate is not null && correction is null && failureReason == BusinessSynthesisFailureReason.SemanticReviewInvalid)) return Result(null, failureReason);
            // No outer transport retry. Every replacement consumes the same original allowance.
            if (index == 2)
            {
                history.Clear();
                history.AddRange(originalMessages);
            }
            else history.AddRange(response.Messages.Select(message => message.Clone()));
            string repair = candidate is not null && correction is null
                ? JsonSerializer.Serialize(new { phase = "semantic-review", proposalDigest = OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(candidate)),
                    failure = failureReason.ToString(), remainingCorrections = MaximumAttempts - index - 2, validationDetail })
                : OctoberBusinessSynthesisRequests.Correction(failureReason, MaximumAttempts - index - 2);
            history.Add(new(ChatRole.User, repair));
            lastFailureReason = failureReason;
        }
        return Result(null, lastFailureReason);
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

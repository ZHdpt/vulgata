using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.OctoberBusiness;

// Process-local model assessments, never canonical evidence or publication authority.
public sealed record BusinessSemanticEvidence([property: JsonRequired] SynthesisClaimInput Input,
    [property: JsonRequired] string Category, [property: JsonRequired] string Quote);
public sealed record BusinessSemanticFinding([property: JsonRequired] string Target,
    [property: JsonRequired] bool Supported, [property: JsonRequired] string Explanation,
    [property: JsonRequired] ImmutableArray<BusinessSemanticEvidence> Evidence);
public sealed record BusinessSemanticReview([property: JsonRequired] string ProposalDigest,
    [property: JsonRequired] ImmutableArray<BusinessSemanticFinding> Findings);

public sealed record BusinessSynthesisDiagnostic(Guid RunId, OperationKey Operation, Guid GroupVersionId,
    string Stage, BusinessSynthesisProposal? Proposal, BusinessSemanticReview? Review,
    BusinessSynthesisFailureReason FailureReason, ImmutableArray<ProviderAttemptUsage> Attempts)
{
    public ImmutableArray<BusinessSourceDiagnostic> SourceReads { get; init; } = [];
    public string? RejectedResponse { get; init; }
    public string? ValidationDetail { get; init; }
}

internal static class OctoberBusinessSemanticReview
{
    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        "NodeNameOrDescription", "Configuration", "ExplicitCondition", "Assignment",
        "DeclaredTransition", "StaticReference", "ImplementationMechanism", "Unknown"
    };

    internal static bool Valid(BusinessSemanticReview? review, BusinessSynthesisProposal candidate,
        FlowPlanningInput input, Guid group, FlowPlanningLimits? limits) =>
        InvalidReason(review, candidate, input, group, limits) is null;

    internal static string? InvalidReason(BusinessSemanticReview? review, BusinessSynthesisProposal candidate,
        FlowPlanningInput input, Guid group, FlowPlanningLimits? limits)
    {
        if (review is null || review.ProposalDigest != OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(candidate)) ||
            review.Findings.IsDefault) return "Review missing, proposal digest mismatch, or findings missing.";
        var expected = new Dictionary<string, HashSet<SynthesisClaimInput>>(StringComparer.Ordinal);
        expected.Add("title", candidate.Sections.SelectMany(s => s.Claims).SelectMany(c => c.Inputs).ToHashSet());
        foreach (var section in candidate.Sections)
        {
            expected.Add("section:" + section.SectionId, section.Claims.SelectMany(c => c.Inputs).ToHashSet());
            foreach (var claim in section.Claims) expected.Add("claim:" + claim.ClaimGroupId, claim.Inputs.ToHashSet());
        }
        if (review.Findings.Length != expected.Count) return $"Expected exactly {expected.Count} findings; received {review.Findings.Length}.";
        var packet = OctoberBusinessSynthesisPolicy.CreateModelPacket(input, group, limits);
        var source = packet.Documents.SelectMany(d => d.Sections.SelectMany(s => s.Claims.Select(c =>
            (Key: new SynthesisClaimInput(d.Version, s.SectionId, c.ClaimGroupId), c.Text))))
            .ToDictionary(x => x.Key, x => x.Text);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in review.Findings)
        {
            if (finding is null || finding.Target is null || !seen.Add(finding.Target) ||
                !expected.TryGetValue(finding.Target, out var permitted) ||
                string.IsNullOrWhiteSpace(finding.Explanation) || finding.Explanation.Length > 2000 ||
                finding.Evidence.IsDefaultOrEmpty || finding.Evidence.Length > 256) return "Invalid, duplicate, or unexpected finding target, explanation, or evidence shape.";
            var cited = new HashSet<SynthesisClaimInput>();
            foreach (var evidence in finding.Evidence)
            {
                if (evidence is null || evidence.Input is null || !permitted.Contains(evidence.Input) ||
                    evidence.Category is null || !Categories.Contains(evidence.Category) ||
                    string.IsNullOrWhiteSpace(evidence.Quote) || evidence.Quote.Length > 8192 ||
                    !source.TryGetValue(evidence.Input, out string? original) ||
                    !original.Contains(evidence.Quote, StringComparison.Ordinal)) return $"Finding {finding.Target}: evidence input, category, or literal CL quote is invalid.";
                cited.Add(evidence.Input);
            }
            if (finding.Target.StartsWith("claim:", StringComparison.Ordinal) && !cited.SetEquals(permitted)) return $"Finding {finding.Target}: cite every input of this exact claim.";
        }
        return null;
    }

    internal static bool SameCoverage(BusinessSynthesisProposal original, BusinessSynthesisProposal corrected) =>
        original.Sections.Length == corrected.Sections.Length && original.Sections.Zip(corrected.Sections).All(pair =>
            pair.First.SectionId == pair.Second.SectionId && pair.First.Ordinal == pair.Second.Ordinal &&
            pair.First.Claims.Length == pair.Second.Claims.Length && pair.First.Claims.Zip(pair.Second.Claims).All(claim =>
                claim.First.ClaimGroupId == claim.Second.ClaimGroupId && claim.First.Qualification == claim.Second.Qualification &&
                claim.First.Inputs.SequenceEqual(claim.Second.Inputs)));
}

public sealed partial class OctoberBusinessSynthesisExecutor
{
    /// <summary>Publication-facing path: synthesis-context self-review, one correction, then full self-review of the latest draft.</summary>
    public async Task<BusinessSynthesisResult> ExecuteReviewedAsync(FlowPlanningInput input, Guid groupVersionId,
        OperationKey operation, Guid runId, string providerProfileId, long maximumTokens,
        IOctoberExecutionScope executionScope, IOctoberResolutionDisclosureAuthority disclosure,
        CancellationToken cancellationToken = default, FlowPlanningLimits? limits = null)
    {
        var allAttempts = ImmutableArray.CreateBuilder<ProviderAttemptUsage>();
        int stageIndex = 0;
        OctoberBusinessSourceRetrieval? retrieval;
        try { retrieval = _sourceReader is null ? null : new(_sourceReader, input, groupVersionId, limits); }
        catch (Exception) { return new(null, null, ProviderAdmissionStatus.Denied, []) { FailureReason = BusinessSynthesisFailureReason.InvalidInput }; }
        long start = _time.GetTimestamp();
        var startUtc = _time.GetUtcNow();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(DeadlineSeconds), _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        BusinessSynthesisResult Stop(BusinessSynthesisFailureReason reason, ProviderAdmissionStatus? admission = null) =>
            new(null, null, admission, allAttempts.ToImmutable()) { FailureReason = reason };
        async Task<BusinessSynthesisResult> Stage(BusinessSynthesisProposal? candidate, BusinessSemanticReview? feedback)
        {
            var remaining = TimeSpan.FromSeconds(DeadlineSeconds) - Max(_time.GetElapsedTime(start), _time.GetUtcNow() - startUtc);
            if (remaining <= TimeSpan.Zero || lifetime.IsCancellationRequested)
                return Stop(BusinessSynthesisFailureReason.ExecutionUnavailable, ProviderAdmissionStatus.DeadlineExceeded);
            var result = await ExecuteStageAsync(input, groupVersionId, operation, runId, providerProfileId, maximumTokens,
                executionScope, disclosure, linked.Token, limits, candidate, feedback, remaining, retrieval);
            allAttempts.AddRange(result.Attempts);
            if (allAttempts.Count > OctoberBusinessSourceRetrieval.MaximumRequests || result.Attempts.Any(x => x.InputTokens is null || x.OutputTokens is null))
                return Stop(BusinessSynthesisFailureReason.SemanticReviewInvalid, ProviderAdmissionStatus.AuthorityUnavailable);
            if (_diagnosticCapture is not null && result.AdmissionFailure is null)
            {
                try
                {
                    if (executionScope is not IOctoberBusinessHandoffLifetime handoff)
                        return Stop(BusinessSynthesisFailureReason.ExecutionUnavailable, ProviderAdmissionStatus.AuthorityUnavailable);
                    using var captureToken = CancellationTokenSource.CreateLinkedTokenSource(linked.Token, handoff.HandoffCancellation);
                    var group = OctoberFlowPlanner.Plan(input, limits).Groups.Single(x => x.GroupVersionId == groupVersionId);
                    async Task<ProviderAdmissionStatus> CaptureEligibilityAsync()
                    {
                        captureToken.Token.ThrowIfCancellationRequested();
                        if (Max(_time.GetElapsedTime(start), _time.GetUtcNow() - startUtc) >= TimeSpan.FromSeconds(DeadlineSeconds))
                            return ProviderAdmissionStatus.DeadlineExceeded;
                        var authority = await CheckAuthorityAsync(disclosure, input.Corpus.Handle, group.Dependencies.Contributors, captureToken.Token);
                        if (authority != DisclosureStatus.Allowed) return AuthorityStatus(authority);
                        if (Max(_time.GetElapsedTime(start), _time.GetUtcNow() - startUtc) >= TimeSpan.FromSeconds(DeadlineSeconds))
                            return ProviderAdmissionStatus.DeadlineExceeded;
                        captureToken.Token.ThrowIfCancellationRequested();
                        return handoff.CheckHandoffEligibility(runId, operation, providerProfileId);
                    }
                    var before = await CaptureEligibilityAsync();
                    if (before != ProviderAdmissionStatus.Granted) return Stop(BusinessSynthesisFailureReason.ExecutionUnavailable, before);
                    var stage = stageIndex switch { 0 => "draft", 1 => "review", 2 => "correction", _ => "re-review" };
                    await _diagnosticCapture(new(runId, operation, groupVersionId, stage,
                        result.Proposal ?? candidate, result.SemanticReview, result.FailureReason, result.Attempts)
                    { SourceReads = retrieval?.Pages ?? [], RejectedResponse = result.RejectedResponse, ValidationDetail = result.ValidationDetail }, captureToken.Token);
                    var after = await CaptureEligibilityAsync();
                    if (after != ProviderAdmissionStatus.Granted) return Stop(BusinessSynthesisFailureReason.ExecutionUnavailable, after);
                }
                catch (OperationCanceledException)
                {
                    return Stop(BusinessSynthesisFailureReason.ExecutionUnavailable,
                        lifetime.IsCancellationRequested ? ProviderAdmissionStatus.DeadlineExceeded : ProviderAdmissionStatus.HardStopped);
                }
                catch (Exception) { return Stop(BusinessSynthesisFailureReason.ExecutionUnavailable, ProviderAdmissionStatus.AuthorityUnavailable); }
            }
            stageIndex++;
            return result with { Attempts = allAttempts.ToImmutable() };
        }
        var draft = await Stage(null, null);
        if (draft.Content is null || draft.Proposal is null) return draft;
        for (int reviewIndex = 0; reviewIndex < 2; reviewIndex++)
        {
            var reviewed = await Stage(draft.Proposal, null);
            if (reviewed.Content is null || reviewed.SemanticReview is null)
                return Stop(reviewed.FailureReason == BusinessSynthesisFailureReason.None
                    ? BusinessSynthesisFailureReason.SemanticReviewInvalid : reviewed.FailureReason, reviewed.AdmissionFailure);
            if (reviewed.SemanticReview.Findings.All(f => f.Supported))
                return reviewed with { SemanticReview = null };
            if (reviewIndex == 1) return Stop(BusinessSynthesisFailureReason.SemanticReviewRejected);
            var corrected = await Stage(draft.Proposal, reviewed.SemanticReview);
            if (corrected.Content is null || corrected.Proposal is null) return corrected;
            if (!OctoberBusinessSemanticReview.SameCoverage(draft.Proposal, corrected.Proposal))
                return Stop(BusinessSynthesisFailureReason.SemanticReviewInvalid);
            draft = corrected;
        }
        return Stop(BusinessSynthesisFailureReason.SemanticReviewRejected);
    }
}

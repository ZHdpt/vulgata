using System.Collections.Immutable;
using System.Text;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

public sealed record SynthesisClaimInput(DocumentVersionReference Document, string SectionId, string ClaimGroupId);
public sealed record SynthesisClaimProposal(string ClaimGroupId, string Text, ClaimQualification Qualification,
    ImmutableArray<SynthesisClaimInput> Inputs, ImmutableArray<string> Limitations);
public sealed record SynthesisSectionProposal(string SectionId, int Ordinal, string Title,
    ImmutableArray<SynthesisClaimProposal> Claims);
public sealed record BusinessSynthesisProposal(string Title, ImmutableArray<SynthesisSectionProposal> Sections);

public enum SynthesisGapKind { ResolutionUncertainty, FileUncertainty, Frontier, Coverage, PlanDisposition }
public sealed record SynthesisGapLocator(SynthesisGapKind Kind, string Key);
/// <summary>Internal exact gap provenance. Explanations and identities are not Business-visible narrative.</summary>
public sealed record SynthesisGap(SynthesisGapLocator Locator, string Explanation,
    ImmutableArray<EvidenceReference> Evidence, ImmutableArray<string> Limitations);
public enum BusinessSynthesisValidationStatus { Succeeded, InvalidInput, InvalidProposal }
public enum BusinessSynthesisFailureReason
{
    None = 0,
    InvalidInput = 1,
    ProposalShape = 2,
    SectionOrder = 3,
    ClaimShape = 4,
    DocumentReference = 5,
    SectionReference = 6,
    ClaimReference = 7,
    QualificationMismatch = 8,
    InputClaimShape = 9,
    OutputLimit = 10,
    InvalidStructure = 11,
    ResponseMissing = 12,
    FinishReasonNotStop = 13,
    ResponseLengthLimit = 14,
    ResponseShape = 15,
    ResponsePayloadLimit = 16,
    ProposalMissing = 17,
    JsonInvalid = 18,
    TransportFailure = 19,
    ExecutionUnavailable = 20,
    UnsupportedRoot = 21,
    NotEvaluated = 22
}
public sealed record BusinessSynthesisValidationResult(BusinessSynthesisValidationStatus Status,
    BusinessLogicContent? Content, BLDependency? Dependencies, ImmutableArray<SynthesisGap> Gaps)
{
    public ImmutableArray<FileCoverage> Coverage { get; init; } = [];
    public ImmutableArray<ResolutionItemDisposition> PlanDispositions { get; init; } = [];
    /// <summary>Process-local classification only; never part of canonical content, payload limits or persistence.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public BusinessSynthesisFailureReason FailureReason { get; init; }
}

/// <summary>Derives qualified BL from exact accepted inputs; model output never owns identities or lineage.</summary>
public static class OctoberBusinessSynthesisPolicy
{
    public const int MaximumPayloadUtf8Bytes = 1024 * 1024;
    public const string GapSectionId = "__knowledge_gaps";
    public const string GapClaimId = "__knowledge_gaps_summary";
    private static readonly UTF8Encoding _utf8 = new(false, true);

    public static ImmutableArray<SynthesisGap> DescribeGaps(FlowPlanningInput input, Guid groupVersionId,
        FlowPlanningLimits? limits = null)
    {
        var group = SelectGroup(input, groupVersionId, limits) ?? throw new ArgumentException("An exact planned flow is required.", nameof(groupVersionId));
        return Gaps(input, group);
    }

    public static BusinessSynthesisValidationResult Validate(FlowPlanningInput input, Guid groupVersionId,
        BusinessSynthesisProposal proposal, FlowPlanningLimits? limits = null)
    {
        BusinessSynthesisValidationResult Invalid(BusinessSynthesisFailureReason reason,
            BusinessSynthesisValidationStatus status = BusinessSynthesisValidationStatus.InvalidProposal) =>
            new(status, null, null, []) { FailureReason = reason };
        FlowGroupVersion? group;
        ImmutableArray<SynthesisGap> gaps;
        try
        {
            group = SelectGroup(input, groupVersionId, limits);
            if (group is null) return Invalid(BusinessSynthesisFailureReason.InvalidInput, BusinessSynthesisValidationStatus.InvalidInput);
            gaps = Gaps(input, group);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { return Invalid(BusinessSynthesisFailureReason.InvalidStructure, BusinessSynthesisValidationStatus.InvalidInput); }
        try
        {
            if (proposal is null) return Invalid(BusinessSynthesisFailureReason.ProposalShape);
            if (!Bounded(proposal)) return Invalid(BusinessSynthesisFailureReason.OutputLimit);
            if (string.IsNullOrWhiteSpace(proposal.Title) || proposal.Sections.IsDefaultOrEmpty ||
                proposal.Sections.Any(x => x is null || string.IsNullOrWhiteSpace(x.SectionId) || x.SectionId == GapSectionId ||
                    string.IsNullOrWhiteSpace(x.Title) || x.Claims.IsDefaultOrEmpty) ||
                proposal.Sections.Select(x => x.SectionId).Distinct(StringComparer.Ordinal).Count() != proposal.Sections.Length)
                return Invalid(BusinessSynthesisFailureReason.ProposalShape);
            if (proposal.Sections.SelectMany(x => x.Claims).Any(x => x is null) ||
                proposal.Sections.SelectMany(x => x.Claims).Select(x => x.ClaimGroupId).Distinct(StringComparer.Ordinal).Count() != proposal.Sections.Sum(x => x.Claims.Length))
                return Invalid(BusinessSynthesisFailureReason.ClaimShape);
            var sections = ImmutableArray.CreateBuilder<DocumentSection>();
            foreach (var section in proposal.Sections.OrderBy(x => x.Ordinal))
            {
                if (section.Ordinal != sections.Count) return Invalid(BusinessSynthesisFailureReason.SectionOrder);
                var claims = ImmutableArray.CreateBuilder<QualifiedClaimGroup>();
                foreach (var proposed in section.Claims)
                {
                    if (string.IsNullOrWhiteSpace(proposed.ClaimGroupId) || proposed.ClaimGroupId == GapClaimId ||
                        string.IsNullOrWhiteSpace(proposed.Text) || !Enum.IsDefined(proposed.Qualification) ||
                        proposed.Qualification == ClaimQualification.Mixed || proposed.Inputs.IsDefaultOrEmpty ||
                        proposed.Limitations.IsDefault || proposed.Limitations.Any(string.IsNullOrWhiteSpace) ||
                        proposed.Inputs.Any(x => x is null) || proposed.Inputs.Distinct().Count() != proposed.Inputs.Length)
                        return Invalid(BusinessSynthesisFailureReason.ClaimShape);
                    var evidence = ImmutableArray.CreateBuilder<EvidenceReference>();
                    var limitations = ImmutableArray.CreateBuilder<string>();
                    limitations.AddRange(proposed.Limitations);
                    bool supplemental = false;
                    foreach (var reference in proposed.Inputs)
                    {
                        if (!group.Dependencies.Documents.Contains(reference.Document)) return Invalid(BusinessSynthesisFailureReason.DocumentReference);
                        var document = input.Corpus.Documents.SingleOrDefault(x => x.Version == reference.Document);
                        var inputSections = document?.Sections.Where(x => x.SectionId == reference.SectionId).ToArray();
                        if (inputSections is not { Length: 1 }) return Invalid(BusinessSynthesisFailureReason.SectionReference);
                        var matches = inputSections[0].Claims.Where(x => x.ClaimGroupId == reference.ClaimGroupId).ToArray();
                        if (matches.Length != 1) return Invalid(BusinessSynthesisFailureReason.ClaimReference);
                        if (matches[0].Qualification != proposed.Qualification) return Invalid(BusinessSynthesisFailureReason.QualificationMismatch);
                        if (matches[0].Evidence.IsDefault || matches[0].Limitations.IsDefault) return Invalid(BusinessSynthesisFailureReason.InputClaimShape);
                        var accepted = matches[0];
                        evidence.AddRange(accepted.Evidence.Select(x => x with { Document = reference.Document, ClaimGroupId = reference.ClaimGroupId }));
                        limitations.AddRange(accepted.Limitations);
                        supplemental |= !group.Members.Contains(reference.Document);
                    }
                    string text = supplemental ? "补充背景（未确认为本流程成员）：" + proposed.Text : proposed.Text;
                    claims.Add(new(proposed.ClaimGroupId, text, proposed.Qualification, evidence.Distinct().ToImmutableArray(),
                        limitations.Distinct(StringComparer.Ordinal).ToImmutableArray()));
                }
                sections.Add(new(section.SectionId, section.Ordinal, section.Title, claims.ToImmutable()));
            }
            if (!gaps.IsEmpty)
            {
                var inherited = gaps.SelectMany(x => x.Limitations.Prepend(x.Explanation)).Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal).ToImmutableArray();
                sections.Add(new(GapSectionId, sections.Count, "理解范围与待确认事项",
                    [new(GapClaimId, "本说明仍受已记录的不确定事项、未完成的输入或分析范围限制影响，相关业务结论需要结合这些局限理解。",
                        ClaimQualification.Uncertain, gaps.SelectMany(x => x.Evidence).Distinct().ToImmutableArray(), inherited)]));
            }
            var content = new BusinessLogicContent(proposal.Title, OctoberKnowledgePolicy.CreateContentJson(proposal.Title, sections.ToImmutable()), sections.ToImmutable());
            var result = new BusinessSynthesisValidationResult(BusinessSynthesisValidationStatus.Succeeded, content, group.Dependencies, gaps)
            {
                Coverage = input.Coverage,
                PlanDispositions = input.PlanDispositions
            };
            return Bounded(result) ? result : Invalid(BusinessSynthesisFailureReason.OutputLimit);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { return Invalid(BusinessSynthesisFailureReason.InvalidStructure); }
    }

    private static FlowGroupVersion? SelectGroup(FlowPlanningInput input, Guid groupVersionId, FlowPlanningLimits? limits)
    {
        if (input is null || groupVersionId == Guid.Empty || !Bounded(input)) return null;
        var plan = OctoberFlowPlanner.Plan(input, limits);
        return plan.Status == FlowPlanningStatus.Succeeded ? plan.Groups.SingleOrDefault(x => x.GroupVersionId == groupVersionId) : null;
    }

    private static ImmutableArray<SynthesisGap> Gaps(FlowPlanningInput input, FlowGroupVersion group)
    {
        var gaps = ImmutableArray.CreateBuilder<SynthesisGap>();
        void Add<T>(SynthesisGapKind kind, T identity, string explanation, ImmutableArray<EvidenceReference> evidence, ImmutableArray<string> limitations) =>
            gaps.Add(new(new(kind, OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(new { kind, identity }))), explanation, evidence, limitations));
        foreach (var unknown in group.Dependencies.Uncertainties)
            Add(SynthesisGapKind.ResolutionUncertainty, new { group.Corpus.Binding.BatchId, unknown.UncertaintyVersionId },
                unknown.Content.Explanation, unknown.Content.Evidence, unknown.Content.Limitations);
        foreach (var unknown in group.Dependencies.FileUncertainties)
            Add(SynthesisGapKind.FileUncertainty, new { unknown.BatchId, unknown.ManifestId, unknown.Key },
                unknown.Content.Explanation, unknown.Content.Evidence, unknown.Content.Limitations);
        foreach (var frontier in group.Frontiers)
            Add(SynthesisGapKind.Frontier, new { group.GroupVersionId, frontier }, "流程的已确认范围之外仍有待确认或受限的分析。", [],
                [OctoberKnowledgePolicy.SerializeCanonical(frontier)]);
        foreach (var coverage in input.Coverage.Where(x => x.Disposition is CoverageDisposition.Failed or CoverageDisposition.Cancelled or CoverageDisposition.Unprocessed))
            Add(SynthesisGapKind.Coverage, new { group.Corpus.Binding.BatchId, coverage.AdmissionId },
                coverage.Reason ?? "输入未完成分析。", [], [OctoberKnowledgePolicy.SerializeCanonical(coverage)]);
        foreach (var disposition in input.PlanDispositions)
            Add(SynthesisGapKind.PlanDisposition, new { group.Corpus.Binding.BatchId, disposition }, disposition.Reason, [],
                [OctoberKnowledgePolicy.SerializeCanonical(disposition)]);
        var result = gaps.OrderBy(x => x.Locator.Kind).ThenBy(x => x.Locator.Key, StringComparer.Ordinal).ToImmutableArray();
        if (!Bounded(result)) throw new ArgumentException("The complete gap ledger exceeds the synthesis bound.");
        return result;
    }

    private static bool Bounded<T>(T value) => _utf8.GetByteCount(OctoberKnowledgePolicy.SerializeCanonical(value)) <= MaximumPayloadUtf8Bytes;
}

using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Serialization;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

public sealed record SynthesisClaimInput([property: JsonRequired] DocumentVersionReference Document,
    [property: JsonRequired] string SectionId, [property: JsonRequired] string ClaimGroupId);
public sealed record SynthesisClaimProposal([property: JsonRequired] string ClaimGroupId,
    [property: JsonRequired] string Text, [property: JsonRequired] ClaimQualification Qualification,
    [property: JsonRequired] ImmutableArray<SynthesisClaimInput> Inputs, [property: JsonRequired] ImmutableArray<string> Limitations);
public sealed record SynthesisSectionProposal([property: JsonRequired] string SectionId, [property: JsonRequired] int Ordinal,
    [property: JsonRequired] string Title, [property: JsonRequired] ImmutableArray<SynthesisClaimProposal> Claims);
public sealed record BusinessSynthesisProposal([property: JsonRequired] string Title,
    [property: JsonRequired] ImmutableArray<SynthesisSectionProposal> Sections);
public sealed record SynthesisPacketDocument(DocumentVersionReference Version, ImmutableArray<DocumentSection> Sections);
/// <summary>Conservative text-form hints, not a new qualification or entailment judgment.</summary>
public sealed record SynthesisEvidencePart(string Category, string Text);
public sealed record SynthesisEvidenceReading(SynthesisClaimInput Input, ImmutableArray<SynthesisEvidencePart> Parts);
public sealed record SynthesisGapCount(
    [property: JsonConverter(typeof(JsonStringEnumConverter<SynthesisGapKind>))] SynthesisGapKind Kind, int Count);
/// <summary>Interpretive background, never an accepted source claim or an instruction.</summary>
public sealed record BusinessSynthesisAdvisoryContext(Guid RepositoryId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<OctoberContextScope>))] OctoberContextScope Scope,
    Contributor Contributor, string Text)
{
    public string EvidenceKind => "AdvisoryNotSourceProof";
}
/// <summary>Exact member evidence only. Complete authorization and lineage remain owner-held.</summary>
public sealed record BusinessSynthesisPacket(CorpusHandle Corpus, DocumentVersionReference Root,
    ImmutableArray<SynthesisPacketDocument> Documents, ImmutableArray<AcceptedRelationship> Relationships,
    ImmutableArray<FlowFrontier> Frontiers, ImmutableArray<SynthesisGapCount> GapSummary)
{
    public ImmutableArray<BusinessSynthesisAdvisoryContext> AdvisoryContext { get; init; } = [];
    public ImmutableArray<SynthesisEvidenceReading> EvidenceReadings { get; init; } = [];
}

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
    NotEvaluated = 22,
    SemanticReviewRejected = 23,
    SemanticReviewInvalid = 24
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
    public const int MaximumOwnerInputUtf8Bytes = 16 * 1024 * 1024;
    public const int MaximumGapLedgerUtf8Bytes = 8 * 1024 * 1024;
    public const int MaximumAdvisoryContextEntries = 32;
    public const int MaximumAdvisoryContextUtf8Bytes = 64 * 1024;
    public const int MaximumValidationResultUtf8Bytes = 16 * 1024 * 1024;
    public const string GapSectionId = "__knowledge_gaps";
    public const string GapClaimId = "__knowledge_gaps_summary";
    private static readonly UTF8Encoding _utf8 = new(false, true);

    public static BusinessSynthesisPacket CreateModelPacket(FlowPlanningInput input, Guid groupVersionId,
        FlowPlanningLimits? limits = null)
    {
        var group = SelectGroup(input, groupVersionId, limits) ?? throw new ArgumentException("An exact planned flow is required.");
        var gaps = Gaps(input, group);
        return CreateModelPacket(input, group, gaps);
    }

    private static BusinessSynthesisPacket CreateModelPacket(FlowPlanningInput input, FlowGroupVersion group,
        ImmutableArray<SynthesisGap> gaps)
    {
        var documents = group.Members.Select(version => input.Corpus.Documents.Single(document => document.Version == version))
            .Select(document => new SynthesisPacketDocument(document.Version, document.Sections)).ToImmutableArray();
        if (!ValidAdvisoryContext(input)) throw new ArgumentException("Invalid or unbounded advisory context.");
        var repositories = input.Corpus.Documents.Where(document => group.Members.Contains(document.Version))
            .Select(document => document.RepositoryId).ToHashSet();
        var packet = new BusinessSynthesisPacket(input.Corpus.Handle, group.RootVersion, documents,
            group.Relationships, group.Frontiers, gaps.GroupBy(gap => gap.Locator.Kind).OrderBy(group => group.Key)
                .Select(group => new SynthesisGapCount(group.Key, group.Count())).ToImmutableArray())
        {
            EvidenceReadings = documents.SelectMany(document => document.Sections.SelectMany(section => section.Claims.Select(claim =>
                new SynthesisEvidenceReading(new(document.Version, section.SectionId, claim.ClaimGroupId), EvidenceParts(claim.Text))))).ToImmutableArray(),
            AdvisoryContext = input.AdvisoryContext.Where(entry => repositories.Contains(entry.RepositoryId))
                .OrderBy(entry => entry.RepositoryId).ThenBy(entry => entry.Scope).ToImmutableArray()
        };
        if (!Bounded(packet)) throw new ArgumentException("The complete member evidence exceeds the model packet bound.");
        return packet;
    }

    // Only isolate explicitly marked quoted descriptions. Everything else stays unknown;
    // lexical guessing must not manufacture source-proven predicates or effects.
    private static ImmutableArray<SynthesisEvidencePart> EvidenceParts(string text)
    {
        const string marker = "描述为「";
        var parts = ImmutableArray.CreateBuilder<SynthesisEvidencePart>();
        int position = 0;
        while (position < text.Length)
        {
            int start = text.IndexOf(marker, position, StringComparison.Ordinal);
            int end = start < 0 ? -1 : text.IndexOf('」', start + marker.Length);
            if (end < 0) { parts.Add(new("Unknown", text[position..])); break; }
            if (start > position) parts.Add(new("Unknown", text[position..start]));
            parts.Add(new("NodeNameOrDescription", text[start..(end + 1)]));
            position = end + 1;
        }
        return parts.ToImmutable();
    }

    private static bool ValidAdvisoryContext(FlowPlanningInput input)
    {
        ImmutableArray<Contributor> effectiveContributors;
        try { effectiveContributors = OctoberBusinessContextOverridePolicy.EffectiveContributors(input); }
        catch (ArgumentException) { return false; }
        if (input.AdvisoryContext.IsDefault || input.AdvisoryContext.Length > MaximumAdvisoryContextEntries) return false;
        long bytes = 0;
        var slots = new HashSet<(Guid, OctoberContextScope)>();
        foreach (var entry in input.AdvisoryContext)
        {
            if (entry is null || entry.RepositoryId == Guid.Empty || !slots.Add((entry.RepositoryId, entry.Scope)) ||
                string.IsNullOrWhiteSpace(entry.Text) || entry.Contributor is not { } contributor ||
                !effectiveContributors.Contains(contributor) || contributor.AdvisoryRecordId is null ||
                contributor.ContentDigest != OctoberKnowledgePolicy.Hash(entry.Text) ||
                !input.Corpus.Documents.Any(document => document.RepositoryId == entry.RepositoryId)) return false;
            if (entry.Scope == OctoberContextScope.Repository)
            {
                if (contributor.Kind != ContributorKind.RepositoryAdvisory || contributor.RepositoryId != entry.RepositoryId || contributor.SystemId is not null) return false;
            }
            else if (entry.Scope == OctoberContextScope.System)
            {
                if (contributor.Kind != ContributorKind.SystemAdvisory || contributor.SystemId is null || contributor.RepositoryId is not null) return false;
            }
            else return false;
            bytes += _utf8.GetByteCount(entry.Text);
            if (bytes > MaximumAdvisoryContextUtf8Bytes) return false;
        }
        return _utf8.GetByteCount(OctoberKnowledgePolicy.SerializeCanonical(input.AdvisoryContext)) <= MaximumAdvisoryContextUtf8Bytes;
    }

    public static ImmutableArray<SynthesisGap> DescribeGaps(FlowPlanningInput input, Guid groupVersionId,
        FlowPlanningLimits? limits = null)
    {
        var group = SelectGroup(input, groupVersionId, limits) ?? throw new ArgumentException("An exact planned flow is required.", nameof(groupVersionId));
        return Gaps(input, group);
    }

    public static BusinessSynthesisValidationResult Validate(FlowPlanningInput input, Guid groupVersionId,
        BusinessSynthesisProposal proposal, FlowPlanningLimits? limits = null)
    {
        try
        {
            var group = SelectGroup(input, groupVersionId, limits);
            return group is null ? InvalidInput() : ValidatePrepared(input, group, proposal);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { return InvalidInput(BusinessSynthesisFailureReason.InvalidStructure); }
    }

    /// <summary>Rebuilds one owner plan for a bounded batch; no caller-supplied plan grants validation.</summary>
    public static ImmutableArray<BusinessSynthesisValidationResult> ValidateAll(FlowPlanningInput input,
        ImmutableArray<FlowSynthesisSubmission> submissions, FlowPlanningLimits? limits = null)
    {
        if (submissions.IsDefault || submissions.Length > OctoberBusinessFinalizationPolicy.MaximumRoots)
            throw new ArgumentException("A bounded explicit submission batch is required.", nameof(submissions));
        if (submissions.IsEmpty) return [];
        ImmutableArray<BusinessSynthesisValidationResult> InvalidBatch(BusinessSynthesisFailureReason reason) =>
            [.. submissions.Select(_ => InvalidInput(reason))];
        try
        {
            if (input is null || !Bounded(input, MaximumOwnerInputUtf8Bytes) ||
                submissions.Where(x => x is not null).Select(x => x.Root).Distinct().Count() != submissions.Count(x => x is not null) ||
                submissions.Where(x => x is not null).Select(x => x.GroupVersionId).Distinct().Count() != submissions.Count(x => x is not null))
                return InvalidBatch(BusinessSynthesisFailureReason.InvalidInput);
            var plan = OctoberFlowPlanner.Plan(input, limits);
            if (plan.Status != FlowPlanningStatus.Succeeded) return InvalidBatch(BusinessSynthesisFailureReason.InvalidInput);
            var groups = plan.Groups.ToDictionary(x => x.GroupVersionId);
            return [.. submissions.Select(submission =>
            {
                if (submission?.Root is not { } root || !groups.TryGetValue(submission.GroupVersionId, out var group) ||
                    group.RootVersion != root.Document || group.Flow.RootUnitId != root.UnitId ||
                    group.Flow.RootKind != root.Kind || group.Flow.RootSelector != root.Selector)
                    return InvalidInput();
                return ValidatePrepared(input, group, submission.Proposal);
            })];
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { return InvalidBatch(BusinessSynthesisFailureReason.InvalidStructure); }
    }

    private static BusinessSynthesisValidationResult InvalidInput(
        BusinessSynthesisFailureReason reason = BusinessSynthesisFailureReason.InvalidInput) =>
        new(BusinessSynthesisValidationStatus.InvalidInput, null, null, []) { FailureReason = reason };

    private static BusinessSynthesisValidationResult ValidatePrepared(FlowPlanningInput input, FlowGroupVersion group,
        BusinessSynthesisProposal proposal)
    {
        BusinessSynthesisValidationResult Invalid(BusinessSynthesisFailureReason reason,
            BusinessSynthesisValidationStatus status = BusinessSynthesisValidationStatus.InvalidProposal) =>
            new(status, null, null, []) { FailureReason = reason };
        ImmutableArray<SynthesisGap> gaps;
        try
        {
            // The store rebuilds the same packet; a model cannot cite an unseen dependency.
            gaps = Gaps(input, group);
            _ = CreateModelPacket(input, group, gaps);
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
                    if (!Enum.IsDefined(proposed.Qualification) || proposed.Qualification == ClaimQualification.Mixed)
                        return Invalid(BusinessSynthesisFailureReason.QualificationMismatch);
                    if (string.IsNullOrWhiteSpace(proposed.ClaimGroupId) || proposed.ClaimGroupId == GapClaimId ||
                        string.IsNullOrWhiteSpace(proposed.Text) || proposed.Inputs.IsDefaultOrEmpty ||
                        proposed.Limitations.IsDefault || proposed.Limitations.Any(string.IsNullOrWhiteSpace) ||
                        proposed.Inputs.Any(x => x is null) || proposed.Inputs.Distinct().Count() != proposed.Inputs.Length)
                        return Invalid(BusinessSynthesisFailureReason.ClaimShape);
                    var evidence = ImmutableArray.CreateBuilder<EvidenceReference>();
                    var limitations = ImmutableArray.CreateBuilder<string>();
                    limitations.AddRange(proposed.Limitations);
                    bool supplemental = false;
                    foreach (var reference in proposed.Inputs)
                    {
                        if (!group.Members.Contains(reference.Document)) return Invalid(BusinessSynthesisFailureReason.DocumentReference);
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
            return Bounded(result, MaximumValidationResultUtf8Bytes) ? result : Invalid(BusinessSynthesisFailureReason.OutputLimit);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { return Invalid(BusinessSynthesisFailureReason.InvalidStructure); }
    }

    private static FlowGroupVersion? SelectGroup(FlowPlanningInput input, Guid groupVersionId, FlowPlanningLimits? limits)
    {
        if (input is null || groupVersionId == Guid.Empty || !Bounded(input, MaximumOwnerInputUtf8Bytes)) return null;
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
        if (!Bounded(result, MaximumGapLedgerUtf8Bytes)) throw new ArgumentException("The complete gap ledger exceeds the owner bound.");
        return result;
    }

    private static bool Bounded<T>(T value, int maximum = MaximumPayloadUtf8Bytes) => _utf8.GetByteCount(OctoberKnowledgePolicy.SerializeCanonical(value)) <= maximum;
}

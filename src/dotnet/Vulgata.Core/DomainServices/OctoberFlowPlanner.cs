using System.Collections.Immutable;
using Vulgata.Core.Entities.OctoberKnowledge;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

public enum FlowRootKind { SelectedCodeUnit, ExportedOperation, Handler, Schedule, DeclarativeWorkflow }
public enum FlowPlanningStatus { Succeeded, InvalidInput, IncompleteResolution }
public enum FlowRootDispositionKind { Planned, UnsupportedKind, InvalidRoot }
public enum FlowFrontierKind { Unresolved, Failed, NotEvaluated, NodeLimit, EdgeLimit, DepthLimit }

/// <summary>Owner-projected immutable CodeUnit identity and its primary accepted source anchors.</summary>
public sealed record FlowPlanningUnit(Guid UnitId, DocumentVersionReference Document,
    string SemanticSelector, PreparedOrigin PrimaryOrigin, ImmutableArray<SourceAnchor> Anchors);

/// <summary>
/// A trusted persistence projection, not an acceptance port for a model or consumer.
/// The future loader must verify the immutable item, witness and complete input provenance.
/// </summary>
public sealed record FlowAcceptedResolution(Guid ItemId, ResolutionOutcome Outcome, Guid? UncertaintyVersionId);

public sealed record FlowRootSelection(Guid UnitId, DocumentVersionReference Document,
    FlowRootKind Kind, string Selector);

public sealed record FlowPlanningInput(ResolutionCorpus Corpus, ImmutableArray<FlowPlanningUnit> Units,
    ImmutableArray<FlowAcceptedResolution> Resolutions, ImmutableArray<FlowRootSelection> Roots,
    ImmutableArray<ResolutionItemDisposition> PlanDispositions, ImmutableArray<FileCoverage> Coverage)
{
    /// <summary>Complete owner-loaded corpus ledger, including coverage inputs with no accepted CodeUnit.</summary>
    public ImmutableArray<Contributor> Contributors { get; init; }
}

/// <summary>Configured limits may only reduce the hard ceilings.</summary>
public sealed record FlowPlanningLimits(int MaxNodes = 256, int MaxEdges = 512, int MaxDepth = 16);

public sealed record FlowUncertainty(Guid UncertaintyVersionId, Uncertainty Content);

public sealed record FlowFrontier(FlowFrontierKind Kind, DocumentVersionReference From,
    DocumentVersionReference? To, Guid? RelationshipVersionId, string? UncertaintyKey);

public sealed record FlowRootDisposition(FlowRootSelection Root, FlowRootDispositionKind Kind, string Reason);

public sealed record FlowPlanningResult(FlowPlanningStatus Status, ImmutableArray<FlowGroupVersion> Groups,
    ImmutableArray<FlowRootDisposition> RootDispositions,
    ImmutableArray<ResolutionItemDisposition> PlanDispositions, ImmutableArray<FileCoverage> Coverage);

/// <summary>
/// Plans finite directed slices from complete accepted B1 input. This controlled increment supports
/// only explicit SelectedCodeUnit roots; other root kinds require a later positive source classifier.
/// Transport records themselves do not establish persisted acceptance or current authorization.
/// </summary>
public static class OctoberFlowPlanner
{
    public const int MaximumNodes = 256;
    public const int MaximumEdges = 512;
    public const int MaximumDepth = 16;

    public static FlowPlanningResult Plan(FlowPlanningInput input, FlowPlanningLimits? limits = null)
    {
        limits ??= new();
        FlowPlanningResult Invalid(FlowPlanningStatus status = FlowPlanningStatus.InvalidInput) =>
            new(status, [], [], [], []);
        if (input is null || input.Corpus is null || input.Corpus.Handle is null ||
            input.Corpus.Documents.IsDefault || input.Units.IsDefault || input.Resolutions.IsDefault ||
            input.Roots.IsDefault || input.PlanDispositions.IsDefault || input.Coverage.IsDefault || input.Contributors.IsDefault ||
            limits.MaxNodes is < 1 or > MaximumNodes || limits.MaxEdges is < 0 or > MaximumEdges ||
            limits.MaxDepth is < 0 or > MaximumDepth)
            return Invalid();

        var documents = input.Corpus.Documents;
        if (documents.Any(x => x is null || x.Version is null || x.RepositoryId == Guid.Empty ||
                x.Sections.IsDefault || x.Contributors.IsDefault || x.Observations.IsDefault ||
                x.Sections.Any(s => s is null || s.Claims.IsDefault || s.Claims.Any(c => c is null || c.Evidence.IsDefault || c.Limitations.IsDefault))) ||
            documents.Select(x => x.Version).Distinct().Count() != documents.Length ||
            documents.Any(x => x.Version.BatchId != input.Corpus.Handle.Binding.BatchId) ||
            input.Units.Any(x => x is null || x.UnitId == Guid.Empty || x.Document is null ||
                x.PrimaryOrigin is null || string.IsNullOrWhiteSpace(x.SemanticSelector) || x.Anchors.IsDefault) ||
            input.Units.Select(x => x.UnitId).Distinct().Count() != input.Units.Length ||
            input.Units.Select(x => x.Document).Distinct().Count() != input.Units.Length ||
            !documents.Select(x => x.Version).ToHashSet().SetEquals(input.Units.Select(x => x.Document)) ||
            input.Resolutions.Any(x => x is null || x.ItemId == Guid.Empty || x.Outcome is null) ||
            input.Resolutions.Select(x => x.ItemId).Distinct().Count() != input.Resolutions.Length ||
            input.Resolutions.Select(x => x.Outcome.Fingerprint).Distinct(StringComparer.Ordinal).Count() != input.Resolutions.Length ||
            input.Roots.Any(x => x is null) || input.PlanDispositions.Any(x => x is null) || input.Coverage.Any(x => x is null) ||
            input.Contributors.Any(x => x is null))
            return Invalid();

        // Corpus provenance also includes terminal coverage with no CodeUnit (for example zero-unit
        // acceptance or unprocessed files). Document citations cannot reconstruct that owner ledger.
        var contributorSet = input.Contributors.ToHashSet();
        if (documents.SelectMany(x => x.Contributors).Any(x => !contributorSet.Contains(x)) ||
            input.Coverage.Any(coverage => coverage.Origin is null || !input.Contributors.Any(contributor =>
                contributor.Kind == ContributorKind.Source &&
                contributor.RepositoryId == coverage.Origin.RepositoryId &&
                contributor.RootBindingIdentity == coverage.Origin.RootBindingIdentity &&
                contributor.ContentDigest == coverage.Origin.SourceDigest)))
            return Invalid();

        ResolutionPlan plan;
        try { plan = OctoberResolutionPolicy.Plan(input.Corpus); }
        catch (ArgumentException) { return Invalid(); }
        var inquiries = plan.Inquiries.ToDictionary(x => x.Fingerprint, StringComparer.Ordinal);
        var byVersion = documents.ToDictionary(x => x.Version);
        var allContributors = Canonical(input.Contributors.Distinct());
        foreach (var item in input.Resolutions)
        {
            if (!inquiries.TryGetValue(item.Outcome.Fingerprint, out var inquiry) ||
                !ValidOutcome(item, inquiry, byVersion, allContributors))
                return Invalid();
        }
        if (input.Resolutions.Where(x => x.Outcome.Relationship is not null)
                .Select(x => x.Outcome.Relationship!.RelationshipVersionId).Distinct().Count() !=
            input.Resolutions.Count(x => x.Outcome.Relationship is not null) ||
            input.Resolutions.Where(x => x.UncertaintyVersionId is not null)
                .Select(x => x.UncertaintyVersionId).Distinct().Count() !=
            input.Resolutions.Count(x => x.UncertaintyVersionId is not null))
            return Invalid();

        var actualDispositions = Canonical(plan.Dispositions);
        var suppliedDispositions = Canonical(input.PlanDispositions);
        if (input.Resolutions.Length != plan.Inquiries.Length ||
            OctoberKnowledgePolicy.SerializeCanonical(actualDispositions) !=
            OctoberKnowledgePolicy.SerializeCanonical(suppliedDispositions))
            return Invalid(FlowPlanningStatus.IncompleteResolution);

        var dependencies = new BLDependency(Canonical(documents.Select(x => x.Version)),
            Canonical(input.Resolutions.Where(x => x.Outcome.Relationship is not null).Select(x => x.Outcome.Relationship!)),
            Canonical(input.Resolutions.Where(x => x.Outcome.Uncertainty is not null)
                .Select(x => new FlowUncertainty(x.UncertaintyVersionId!.Value, x.Outcome.Uncertainty!))), allContributors);
        var groups = ImmutableArray.CreateBuilder<FlowGroupVersion>();
        var dispositions = ImmutableArray.CreateBuilder<FlowRootDisposition>();
        var coverage = Canonical(input.Coverage);
        foreach (var root in Canonical(input.Roots.Distinct()))
        {
            if (!Enum.IsDefined(root.Kind))
            {
                dispositions.Add(new(root, FlowRootDispositionKind.InvalidRoot, "Unknown root kind."));
                continue;
            }
            if (root.Kind != FlowRootKind.SelectedCodeUnit)
            {
                dispositions.Add(new(root, FlowRootDispositionKind.UnsupportedKind, "This root kind requires positive source qualification."));
                continue;
            }
            var unit = input.Units.SingleOrDefault(x => x.UnitId == root.UnitId && x.Document == root.Document && x.SemanticSelector == root.Selector);
            if (unit is null || !ValidRoot(unit, byVersion[unit.Document]))
            {
                dispositions.Add(new(root, FlowRootDispositionKind.InvalidRoot, "The root must pin an exact source-backed primary CodeUnit."));
                continue;
            }
            groups.Add(Slice(input, root, unit, byVersion[unit.Document], inquiries, dependencies, actualDispositions, coverage, limits));
            dispositions.Add(new(root, FlowRootDispositionKind.Planned, "Explicit source-backed CodeUnit selected."));
        }
        return new(FlowPlanningStatus.Succeeded, Canonical(groups), dispositions.ToImmutable(), actualDispositions, coverage);
    }

    private static bool ValidRoot(FlowPlanningUnit unit, ResolutionDocument document)
    {
        if (unit.Anchors.IsEmpty || unit.PrimaryOrigin.RepositoryId != document.RepositoryId ||
            !document.Contributors.Any(c => c.Kind == ContributorKind.Source && c.RepositoryId == document.RepositoryId &&
                c.RootBindingIdentity == unit.PrimaryOrigin.RootBindingIdentity && c.ContentDigest == unit.PrimaryOrigin.SourceDigest))
            return false;
        var sourceEvidence = document.Sections.SelectMany(s => s.Claims)
            .Where(c => c.Qualification == ClaimQualification.SourceSupported).SelectMany(c => c.Evidence)
            .Where(e => e.Source is not null && e.AdvisoryRecordId is null && e.ContentDigest == unit.PrimaryOrigin.SourceDigest)
            .Select(e => e.Source!).ToArray();
        // Unit ranges may contain several smaller accepted claim ranges. They cannot move ownership
        // to a supporting file or become a source-backed root solely through advisory text.
        return unit.Anchors.All(anchor => anchor is not null && anchor.Origin == unit.PrimaryOrigin &&
            anchor.StartLine > 0 && anchor.EndLine >= anchor.StartLine && sourceEvidence.Any(e =>
                e.Origin == unit.PrimaryOrigin && e.StartLine >= anchor.StartLine && e.EndLine <= anchor.EndLine));
    }

    private static bool ValidOutcome(FlowAcceptedResolution item, ResolutionInquiry inquiry,
        Dictionary<DocumentVersionReference, ResolutionDocument> documents, ImmutableArray<Contributor> contributors)
    {
        var outcome = item.Outcome;
        if (!Enum.IsDefined(outcome.Kind) || outcome.Contributors.IsDefault ||
            OctoberKnowledgePolicy.SerializeCanonical(Canonical(outcome.Contributors.Distinct())) !=
            OctoberKnowledgePolicy.SerializeCanonical(contributors) ||
            (outcome.Uncertainty is null) != (item.UncertaintyVersionId is null) || item.UncertaintyVersionId == Guid.Empty)
            return false;
        if (outcome.Uncertainty is { } uncertainty &&
            (uncertainty.Key != "resolution/" + inquiry.Fingerprint || string.IsNullOrWhiteSpace(uncertainty.Explanation) ||
             uncertainty.Evidence.IsDefault || uncertainty.Limitations.IsDefault))
            return false;
        if (outcome.Kind != ResolutionOutcomeKind.Supported)
            return outcome.Relationship is null && outcome.Uncertainty is not null;
        var edge = outcome.Relationship;
        if (edge is null || edge.RelationshipVersionId == Guid.Empty || edge.Kind != RelationshipKind.References ||
            edge.From != inquiry.Caller.Document || !documents.ContainsKey(edge.To) ||
            !inquiry.Candidates.Any(c => c.Document == edge.To) || string.IsNullOrWhiteSpace(edge.Meaning) ||
            edge.SupportingEvidence.IsDefaultOrEmpty || edge.OpposingEvidence.IsDefault)
            return false;
        bool Grounded(EvidenceReference evidence) => evidence is not null && evidence.Source is not null &&
            evidence.AdvisoryRecordId is null && documents.Values.Any(d =>
                d.RepositoryId == evidence.Source.Origin.RepositoryId && d.Sections.SelectMany(s => s.Claims).Any(c =>
                    c.Qualification == ClaimQualification.SourceSupported && c.Evidence.Contains(evidence)));
        return edge.SupportingEvidence.Concat(edge.OpposingEvidence).All(Grounded) &&
            new[] { edge.From, edge.To }.All(version => documents[version].Sections.SelectMany(s => s.Claims)
                .Any(c => c.Qualification == ClaimQualification.SourceSupported && c.Evidence.Any(edge.SupportingEvidence.Contains)));
    }

    private static FlowGroupVersion Slice(FlowPlanningInput input, FlowRootSelection root, FlowPlanningUnit unit,
        ResolutionDocument rootDocument, Dictionary<string, ResolutionInquiry> inquiries, BLDependency dependencies,
        ImmutableArray<ResolutionItemDisposition> planDispositions, ImmutableArray<FileCoverage> coverage, FlowPlanningLimits limits)
    {
        var flow = new LogicalFlow(Identity(new { domain = "october/logical-flow/1", batchId = root.Document.BatchId,
            root.UnitId, root.Kind, root.Selector }), root.Document.BatchId, unit.UnitId, root.Kind, root.Selector, rootDocument.RepositoryId);
        var members = new HashSet<DocumentVersionReference> { root.Document };
        var edges = new List<AcceptedRelationship>();
        var unknowns = new List<FlowUncertainty>();
        var frontiers = new List<FlowFrontier>();
        var queue = new Queue<(DocumentVersionReference Document, int Depth)>();
        queue.Enqueue((root.Document, 0));
        var byCaller = input.Resolutions.ToLookup(x => inquiries[x.Outcome.Fingerprint].Caller.Document);
        while (queue.TryDequeue(out var current))
        {
            foreach (var item in Canonical(byCaller[current.Document]))
            {
                var outcome = item.Outcome;
                if (outcome.Uncertainty is not null)
                    unknowns.Add(new(item.UncertaintyVersionId!.Value, outcome.Uncertainty));
                if (outcome.Relationship is not { } edge)
                {
                    var kind = outcome.Kind switch
                    {
                        ResolutionOutcomeKind.Failed => FlowFrontierKind.Failed,
                        ResolutionOutcomeKind.NotEvaluated => FlowFrontierKind.NotEvaluated,
                        _ => FlowFrontierKind.Unresolved
                    };
                    frontiers.Add(new(kind, current.Document, null, null, outcome.Uncertainty!.Key));
                    continue;
                }
                bool visited = members.Contains(edge.To);
                FlowFrontierKind? bound = edges.Count >= limits.MaxEdges ? FlowFrontierKind.EdgeLimit :
                    !visited && current.Depth >= limits.MaxDepth ? FlowFrontierKind.DepthLimit :
                    !visited && members.Count >= limits.MaxNodes ? FlowFrontierKind.NodeLimit : null;
                if (bound is not null)
                {
                    frontiers.Add(new(bound.Value, current.Document, edge.To, edge.RelationshipVersionId, outcome.Uncertainty?.Key));
                    continue;
                }
                edges.Add(edge);
                if (members.Add(edge.To)) queue.Enqueue((edge.To, current.Depth + 1));
            }
            foreach (var disposition in planDispositions.Where(x => x.Observation.Document == current.Document))
                frontiers.Add(new(FlowFrontierKind.NotEvaluated, current.Document, null, null, null));
        }
        var orderedMembers = Canonical(members);
        var orderedEdges = Canonical(edges);
        var orderedUnknowns = Canonical(unknowns);
        var orderedFrontiers = Canonical(frontiers.Distinct());
        var groupId = Identity(new { domain = "october/flow-group/1", flow, input.Corpus.Handle,
            rootVersion = root.Document, members = orderedMembers, relationships = orderedEdges,
            uncertainties = orderedUnknowns, frontiers = orderedFrontiers, limits, dependencies, planDispositions, coverage });
        return new(groupId, flow, input.Corpus.Handle, root.Document, orderedMembers,
            orderedEdges, orderedUnknowns, orderedFrontiers, limits, dependencies);
    }

    private static ImmutableArray<T> Canonical<T>(IEnumerable<T> values) =>
        values.OrderBy(OctoberKnowledgePolicy.SerializeCanonical, StringComparer.Ordinal).ToImmutableArray();

    private static Guid Identity<T>(T value) => new(Convert.FromHexString(
        OctoberKnowledgePolicy.Hash(OctoberKnowledgePolicy.SerializeCanonical(value))[..32]));
}

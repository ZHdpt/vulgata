using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class ChatRelationshipEvidenceProjector(VulgataDbContext context) :
    IChatRelationshipProjector
{
    private readonly VulgataDbContext _context = context;

    internal static ChatRelationshipEvidenceProjector Create(VulgataDbContext context) =>
        new(context);

    public async Task<ChatRetrievalStageResult<ChatRelationshipExpansionProjection>> ProjectAsync(
        AuthorizedChatSnapshot snapshot,
        ChatLexicalPrimaryProjection primary,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(primary);
        ChatRelationshipProjection projected = await ProjectRelationshipsAsync(
            snapshot.Scope.SystemId,
            snapshot.Conversation.Scope.Mode,
            snapshot.Scope.RepositoryIds,
            primary.ExpansionSeedDocumentVersionIds,
            primary.PrimaryDocuments.Select(document => document.DocumentVersionId).ToArray(),
            [],
            5 - primary.PrimaryDocuments.Count,
            32,
            cancellationToken);
        return projected.IsInconsistent
            ? ChatRetrievalStageResult<ChatRelationshipExpansionProjection>
                .InconsistentCurrentEvidence()
            : await ChatEvidenceRetriever.MaterializeExpansionAsync(
                _context,
                snapshot,
                primary,
                projected,
                cancellationToken);
    }

    private async Task<ChatRelationshipProjection> ProjectRelationshipsAsync(
        Guid systemId,
        ChatMode mode,
        IReadOnlyCollection<Guid> repositoryIds,
        IReadOnlyCollection<Guid> seedDocumentVersionIds,
        IReadOnlyCollection<Guid> includedDocumentVersionIds,
        IReadOnlyCollection<Guid> excludedEdgeIds,
        int relatedDocumentLimit,
        int relationshipLimit,
        CancellationToken cancellationToken)
    {
        if (relatedDocumentLimit < 0 || relationshipLimit <= 0)
        {
            return ChatRelationshipProjection.Empty;
        }

        Guid[] repositories = repositoryIds.Distinct().Order().ToArray();
        Guid[] seeds = TakeDistinctInOrder(seedDocumentVersionIds, 3);
        HashSet<Guid> included = includedDocumentVersionIds.ToHashSet();
        HashSet<Guid> excludedEdges = excludedEdgeIds.ToHashSet();
        if (repositories.Length != 2 || seeds.Length == 0)
        {
            return ChatRelationshipProjection.Empty;
        }

        ProjectableEdgeRow[] eligibleCandidates = await (
            from edge in _context.DocumentEdges.AsNoTracking()
            join target in _context.DocumentVersions.AsNoTracking()
                on edge.TargetDocumentVersionId equals target.Id
            where seeds.Contains(edge.SourceDocumentVersionId) &&
                !excludedEdges.Contains(edge.Id) &&
                (edge.EdgeType == DocumentEdgeType.References ||
                    edge.EdgeType == DocumentEdgeType.GeneratedFrom) &&
                repositories.Contains(edge.SourceRepositoryId) &&
                repositories.Contains(edge.TargetRepositoryId)
            select new ProjectableEdgeRow(
                edge.Id,
                edge.SourceDocumentVersionId,
                edge.SourceRepositoryId,
                edge.SourceScanRunId,
                edge.TargetDocumentVersionId,
                edge.TargetRepositoryId,
                target.LogicalDocumentId,
                edge.TargetScanRunId,
                edge.EdgeType))
            .ToArrayAsync(cancellationToken);
        IGrouping<Guid, ProjectableEdgeRow>[] candidateGroups = eligibleCandidates
            .GroupBy(candidate => candidate.EdgeId)
            .ToArray();
        if (candidateGroups.Any(group => group.Distinct().Skip(1).Any()))
        {
            return ChatRelationshipProjection.Invalid;
        }

        Dictionary<Guid, int> seedRanks = seeds
            .Select((seed, rank) => (seed, rank))
            .ToDictionary(item => item.seed, item => item.rank);
        ProjectableEdgeRow[] candidates = candidateGroups
            .Select(group => group.First())
            .OrderBy(candidate => seedRanks[candidate.SourceDocumentVersionId])
            .ThenBy(candidate => RelationshipPreference(mode, candidate.EdgeType))
            .ThenBy(candidate => candidate.SourceRepositoryId == candidate.TargetRepositoryId ? 0 : 1)
            .ThenBy(candidate => candidate.TargetRepositoryId)
            .ThenBy(candidate => candidate.TargetLogicalDocumentId)
            .ThenBy(candidate => candidate.TargetDocumentVersionId)
            .ThenBy(candidate => candidate.EdgeId)
            .ToArray();

        var relationships = new List<ProjectedChatRelationship>();
        var relatedSections = new List<DocumentSection>();
        var flowGroups = new List<EvidenceFlowGroupAuthority>();
        var addedDocuments = new HashSet<Guid>();
        var projectedTargets = new Dictionary<Guid, bool>();
        var projectedEdges = new HashSet<Guid>();
        var additionsBySeed = new Dictionary<Guid, int>();

        foreach (ProjectableEdgeRow candidate in candidates)
        {
            if (relationships.Count >= relationshipLimit)
            {
                break;
            }

            int additionsForSeed = additionsBySeed.GetValueOrDefault(
                candidate.SourceDocumentVersionId);
            bool sourceCurrent = await IsCurrentEndpointAsync(
                    candidate.SourceDocumentVersionId,
                    candidate.SourceRepositoryId,
                    candidate.SourceScanRunId,
                    systemId,
                    cancellationToken);
            bool targetCurrent = await IsCurrentEndpointAsync(
                    candidate.TargetDocumentVersionId,
                    candidate.TargetRepositoryId,
                    candidate.TargetScanRunId,
                    systemId,
                    cancellationToken);
            if (!sourceCurrent || !targetCurrent)
            {
                continue;
            }

            bool alreadyIncluded = included.Contains(candidate.TargetDocumentVersionId) ||
                addedDocuments.Contains(candidate.TargetDocumentVersionId);
            if (!alreadyIncluded &&
                (additionsForSeed >= 2 || addedDocuments.Count >= relatedDocumentLimit))
            {
                continue;
            }

            ChatExactDecisionSet? exactDecisionSet = null;
            if (candidate.EdgeType == DocumentEdgeType.References &&
                candidate.SourceRepositoryId != candidate.TargetRepositoryId)
            {
                exactDecisionSet = await FindAcceptedExactDecisionSetAsync(
                    systemId,
                    candidate,
                    cancellationToken);
                if (exactDecisionSet is null)
                {
                    continue;
                }
            }

            DocumentSection? relatedSection = await _context.DocumentSections
                .AsNoTracking()
                .Where(section => section.DocumentVersionId == candidate.TargetDocumentVersionId)
                .OrderBy(section => section.SectionOrder)
                .ThenBy(section => section.StableSectionId)
                .FirstOrDefaultAsync(cancellationToken);
            if (relatedSection is null)
            {
                continue;
            }

            bool hasExactAuthority = exactDecisionSet is not null;
            if (projectedTargets.TryGetValue(
                    candidate.TargetDocumentVersionId,
                    out bool existingHasExactAuthority))
            {
                if (existingHasExactAuthority && !hasExactAuthority)
                {
                    continue;
                }

                return ChatRelationshipProjection.Invalid;
            }

            if (!projectedEdges.Add(candidate.EdgeId))
            {
                return ChatRelationshipProjection.Invalid;
            }

            projectedTargets.Add(candidate.TargetDocumentVersionId, hasExactAuthority);

            IReadOnlyList<EvidenceFlowGroupAuthority> projectedFlowGroups = [];
            if (candidate.EdgeType == DocumentEdgeType.References)
            {
                FlowGroupProjection flowProjection = await ProjectFlowGroupsAsync(
                    systemId,
                    candidate,
                    exactDecisionSet,
                    included.Concat(addedDocuments)
                        .Append(candidate.TargetDocumentVersionId)
                        .ToHashSet(),
                    cancellationToken);
                if (flowProjection.IsInconsistent)
                {
                    return ChatRelationshipProjection.Invalid;
                }

                projectedFlowGroups = flowProjection.FlowGroups;
                if (flowGroups.Count + projectedFlowGroups.Count > 10 ||
                    flowGroups.Sum(group => group.Steps.Count) +
                        projectedFlowGroups.Sum(group => group.Steps.Count) > 12)
                {
                    projectedFlowGroups = [];
                }
            }

            Guid? selectedDecisionId = exactDecisionSet?.SelectedDecision.Id;
            CrossRepositoryResolutionDecision? selectedDecision = exactDecisionSet?.Decisions
                .FirstOrDefault(decision => decision.Id == selectedDecisionId);
            relationships.Add(new ProjectedChatRelationship(
                candidate.EdgeId,
                candidate.SourceDocumentVersionId,
                candidate.TargetDocumentVersionId,
                candidate.EdgeType,
                selectedDecisionId,
                exactDecisionSet?.Count ?? 0,
                exactDecisionSet?.Fingerprint,
                selectedDecision?.ProviderEvidenceSetFingerprint,
                projectedFlowGroups.FirstOrDefault()?.FlowGroupId));
            flowGroups.AddRange(projectedFlowGroups);
            if (!alreadyIncluded)
            {
                additionsBySeed[candidate.SourceDocumentVersionId] = additionsForSeed + 1;
            }

            if (relatedSections.All(section => section.StableSectionId != relatedSection.StableSectionId))
            {
                relatedSections.Add(relatedSection);
            }

            if (!alreadyIncluded)
            {
                addedDocuments.Add(candidate.TargetDocumentVersionId);
            }
        }

        ProjectedChatRelationship[] canonicalRelationships = relationships
            .OrderBy(relationship => relationship.EdgeType == DocumentEdgeType.References ? 0 : 1)
            .ThenBy(relationship => relationship.EdgeId)
            .ToArray();
        return new ChatRelationshipProjection(
            canonicalRelationships,
            relatedSections,
            flowGroups);
    }

    private static Guid[] TakeDistinctInOrder(
        IReadOnlyCollection<Guid> values,
        int limit)
    {
        var seen = new HashSet<Guid>();
        return values.Where(seen.Add).Take(limit).ToArray();
    }

    private static int RelationshipPreference(ChatMode mode, DocumentEdgeType edgeType) =>
        mode == ChatMode.Business
            ? edgeType == DocumentEdgeType.GeneratedFrom ? 0 : 1
            : edgeType == DocumentEdgeType.References ? 0 : 1;

    private Task<bool> IsCurrentEndpointAsync(
        Guid documentVersionId,
        Guid repositoryId,
        Guid scanRunId,
        Guid systemId,
        CancellationToken cancellationToken) =>
        (from logical in _context.LogicalDocuments.AsNoTracking()
         join version in _context.DocumentVersions.AsNoTracking()
             on logical.CurrentVersionId equals version.Id
         join scanRun in _context.ScanRuns.AsNoTracking()
             on version.ScanRunId equals scanRun.Id
         where version.Id == documentVersionId &&
             version.RepositoryId == repositoryId &&
             version.ScanRunId == scanRunId &&
             version.SystemId == systemId &&
             version.SupersededById == null &&
             scanRun.RepositoryId == repositoryId &&
             scanRun.Status == ScanRunStatus.Completed &&
             scanRun.ProfiledRevision == version.ProfiledRevision
         select version.Id).AnyAsync(cancellationToken);

    private async Task<ChatExactDecisionSet?> FindAcceptedExactDecisionSetAsync(
        Guid systemId,
        ProjectableEdgeRow candidate,
        CancellationToken cancellationToken)
    {
        CrossRepositoryResolutionDecision[] decisions = await _context
            .CrossRepositoryResolutionDecisions
            .AsNoTracking()
            .Where(decision =>
                decision.SystemId == systemId &&
                decision.DocumentEdgeId == candidate.EdgeId &&
                decision.ResolutionSchemaIdentity == "exact-cross-repository-resolution/2.0" &&
                decision.OutcomeKind == "ExactMatch" &&
                decision.ReasonCode == "unique_provider")
            .Take(257)
            .ToArrayAsync(cancellationToken);
        if (decisions.Length is < 1 or > 256)
        {
            return null;
        }

        DocumentEdge edge = await _context.DocumentEdges
            .AsNoTracking()
            .SingleAsync(item => item.Id == candidate.EdgeId, cancellationToken);
        Guid[] decisionIds = decisions.Select(decision => decision.Id).ToArray();
        ExactResolutionProviderEvidenceContribution[] contributions = await _context
            .ExactResolutionProviderEvidenceContributions
            .AsNoTracking()
            .Where(item => decisionIds.Contains(item.ResolutionDecisionId))
            .OrderBy(item => item.ResolutionDecisionId)
            .ThenBy(item => item.ProviderOrdinal)
            .ThenBy(item => item.EvidenceOrdinal)
            .ToArrayAsync(cancellationToken);

        if (contributions.Length < decisions.Length ||
            decisions.Any(item => contributions.Count(contribution =>
                contribution.ResolutionDecisionId == item.Id) > 256) ||
            !ChatExactDecisionSetAuthority.TryCreate(
                edge,
                decisions,
                contributions,
                out ChatExactDecisionSet decisionSet))
        {
            return null;
        }

        return decisionSet;
    }

    private async Task<FlowGroupProjection> ProjectFlowGroupsAsync(
        Guid systemId,
        ProjectableEdgeRow candidate,
        ChatExactDecisionSet? decisionSet,
        IReadOnlySet<Guid> includedDocumentVersionIds,
        CancellationToken cancellationToken)
    {
        Guid[] decisionIds = decisionSet?.Decisions.Select(decision => decision.Id).ToArray() ?? [];
        FlowGroupRow[] rows = await (
            from flowGroup in _context.FlowGroups.AsNoTracking()
            join trace in _context.FlowTraces.AsNoTracking()
                on flowGroup.Id equals trace.FlowGroupId
            join step in _context.FlowTraceSteps.AsNoTracking()
                on trace.Id equals step.FlowTraceId
            where flowGroup.ExactResolutionEdgeId == candidate.EdgeId &&
                (decisionSet != null && flowGroup.ExactResolutionDecisionId.HasValue &&
                    decisionIds.Contains(flowGroup.ExactResolutionDecisionId.Value) ||
                 decisionSet == null && !flowGroup.ExactResolutionDecisionId.HasValue)
            orderby flowGroup.GroupOrder, flowGroup.Id, trace.TraceOrder, step.StepOrder, step.Id
            select new FlowGroupRow(
                flowGroup.Id,
                flowGroup.SchemaIdentity,
                flowGroup.State,
                flowGroup.SystemId,
                flowGroup.BusinessLogicDocumentVersionId,
                flowGroup.RepositoryId,
                flowGroup.ScanRunId,
                flowGroup.GroupOrder,
                flowGroup.ExactResolutionDecisionId,
                flowGroup.ExactResolutionEdgeId,
                flowGroup.PlanFingerprint,
                flowGroup.MembershipFingerprint,
                trace.Id,
                trace.TraceOrder,
                step.Id,
                step.StepOrder,
                step.StepKind,
                step.Description,
                step.DocumentVersionId,
                step.DocumentRepositoryId,
                step.DocumentScanRunId,
                step.EdgeId,
                step.ExactResolutionDecisionId))
            .Take(13)
            .ToArrayAsync(cancellationToken);

        int associatedGroupCount = await _context.FlowGroups.AsNoTracking()
            .CountAsync(group => group.ExactResolutionEdgeId == candidate.EdgeId,
                cancellationToken);
        if (associatedGroupCount == 0)
        {
            return FlowGroupProjection.Empty;
        }

        if (rows.Length is < 1 or > 12)
        {
            return FlowGroupProjection.Invalid;
        }

        Dictionary<Guid, int> decisionRanks = (decisionSet?.Decisions ?? [])
            .Select((decision, index) => (decision.Id, index))
            .ToDictionary(item => item.Id, item => item.index);
        FlowGroupRow[][] groups = rows
            .GroupBy(row => row.FlowGroupId)
            .OrderBy(group => group.First().ExactResolutionDecisionId is Guid decisionId
                ? decisionRanks[decisionId]
                : int.MaxValue)
            .ThenBy(group => group.First().GroupOrder)
            .ThenBy(group => group.Key)
            .Select(group => group.ToArray())
            .ToArray();
        if (groups.Length != associatedGroupCount || groups.Length > 10)
        {
            return FlowGroupProjection.Invalid;
        }

        var authorities = new List<EvidenceFlowGroupAuthority>(groups.Length);
        foreach (FlowGroupRow[] groupRows in groups)
        {
            FlowGroupRow first = groupRows[0];
            CrossRepositoryResolutionDecision? decision = decisionSet?.Decisions.Single(item =>
                item.Id == first.ExactResolutionDecisionId);
            bool hasRequiredGeneratedFrom = decision is null ||
                await _context.DocumentEdges.AsNoTracking().AnyAsync(edge =>
                    edge.EdgeType == DocumentEdgeType.GeneratedFrom &&
                    edge.SourceDocumentVersionId == first.BusinessLogicDocumentVersionId &&
                    (edge.TargetDocumentVersionId == candidate.SourceDocumentVersionId ||
                     edge.TargetDocumentVersionId == candidate.TargetDocumentVersionId),
                    cancellationToken);
            if (!string.Equals(first.SchemaIdentity, "endpoint-flow-group/1.0", StringComparison.Ordinal) ||
                !string.Equals(first.State, "Published", StringComparison.Ordinal) ||
                first.SystemId != systemId ||
                first.BusinessLogicDocumentVersionId is not Guid businessDocumentVersionId ||
                !includedDocumentVersionIds.Contains(businessDocumentVersionId) ||
                first.RepositoryId != candidate.SourceRepositoryId ||
                first.ScanRunId != candidate.SourceScanRunId ||
                first.ExactResolutionEdgeId != candidate.EdgeId ||
                first.ExactResolutionDecisionId != decision?.Id ||
                !hasRequiredGeneratedFrom ||
                !IsLowerHexSha256(first.PlanFingerprint) ||
                !IsLowerHexSha256(first.MembershipFingerprint) ||
                groupRows.Any(row =>
                    row.FlowTraceId != first.FlowTraceId ||
                    row.FlowTraceOrder != 1 ||
                    row.ExactResolutionDecisionId != first.ExactResolutionDecisionId ||
                    row.ExactResolutionEdgeId != first.ExactResolutionEdgeId) ||
                !groupRows.Select(row => row.StepOrder)
                    .SequenceEqual(Enumerable.Range(1, groupRows.Length)))
            {
                return FlowGroupProjection.Invalid;
            }

            foreach (FlowGroupRow row in groupRows)
            {
                if (!includedDocumentVersionIds.Contains(row.DocumentVersionId) ||
                    !await IsCurrentEndpointAsync(
                        row.DocumentVersionId,
                        row.DocumentRepositoryId,
                        row.DocumentScanRunId,
                        systemId,
                        cancellationToken) ||
                    row.EdgeId is Guid edgeId && edgeId != candidate.EdgeId ||
                    row.ExactResolutionDecisionId is Guid stepDecisionId &&
                        stepDecisionId != decision?.Id)
                {
                    return FlowGroupProjection.Invalid;
                }
            }

            authorities.Add(new EvidenceFlowGroupAuthority(
                first.FlowGroupId,
                first.SchemaIdentity!,
                first.State,
                businessDocumentVersionId,
                first.ExactResolutionDecisionId,
                candidate.EdgeId,
                first.PlanFingerprint,
                first.MembershipFingerprint!,
                first.FlowTraceId,
                first.FlowTraceOrder,
                groupRows.Select(row => new EvidenceFlowStepAuthority(
                    row.FlowTraceStepId,
                    row.StepOrder,
                    row.StepKind,
                    row.Description,
                    row.DocumentVersionId,
                    row.DocumentRepositoryId,
                    row.DocumentScanRunId,
                    row.EdgeId,
                    row.StepExactResolutionDecisionId)).ToArray()));
        }

        return new FlowGroupProjection(authorities);
    }

    private static bool IsLowerHexSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record ProjectableEdgeRow(
        Guid EdgeId,
        Guid SourceDocumentVersionId,
        Guid SourceRepositoryId,
        Guid SourceScanRunId,
        Guid TargetDocumentVersionId,
        Guid TargetRepositoryId,
        Guid TargetLogicalDocumentId,
        Guid TargetScanRunId,
        DocumentEdgeType EdgeType);

    private sealed record FlowGroupRow(
        Guid FlowGroupId,
        string? SchemaIdentity,
        string State,
        Guid? SystemId,
        Guid? BusinessLogicDocumentVersionId,
        Guid RepositoryId,
        Guid ScanRunId,
        int GroupOrder,
        Guid? ExactResolutionDecisionId,
        Guid? ExactResolutionEdgeId,
        string PlanFingerprint,
        string? MembershipFingerprint,
        Guid FlowTraceId,
        int FlowTraceOrder,
        Guid FlowTraceStepId,
        int StepOrder,
        FlowTraceStepKind StepKind,
        string Description,
        Guid DocumentVersionId,
        Guid DocumentRepositoryId,
        Guid DocumentScanRunId,
        Guid? EdgeId,
        Guid? StepExactResolutionDecisionId);
}

internal static class ChatRelationshipProjectorFactory
{
    internal static IChatRelationshipProjector Create(VulgataDbContext context) =>
        ChatRelationshipEvidenceProjector.Create(context);
}

internal sealed record FlowGroupProjection(
    IReadOnlyList<EvidenceFlowGroupAuthority> FlowGroups,
    bool IsInconsistent = false)
{
    internal static FlowGroupProjection Empty { get; } = new([]);
    internal static FlowGroupProjection Invalid { get; } = new([], true);
}

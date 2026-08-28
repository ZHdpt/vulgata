using Microsoft.EntityFrameworkCore;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

internal sealed class ChatRelationshipEvidenceProjector(VulgataDbContext context)
{
    private readonly VulgataDbContext _context = context;

    internal async Task<ChatRelationshipProjection> ProjectAsync(
        Guid systemId,
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
        Guid[] seeds = seedDocumentVersionIds.Distinct().Order().Take(3).ToArray();
        HashSet<Guid> included = includedDocumentVersionIds.ToHashSet();
        HashSet<Guid> excludedEdges = excludedEdgeIds.ToHashSet();
        if (repositories.Length != 2 || seeds.Length == 0)
        {
            return ChatRelationshipProjection.Empty;
        }

        ProjectableEdgeRow[] candidates = await _context.DocumentEdges
            .AsNoTracking()
            .Where(edge => seeds.Contains(edge.SourceDocumentVersionId) &&
                !seeds.Contains(edge.TargetDocumentVersionId) &&
                !excludedEdges.Contains(edge.Id) &&
                (edge.EdgeType == DocumentEdgeType.References ||
                    edge.EdgeType == DocumentEdgeType.GeneratedFrom) &&
                repositories.Contains(edge.SourceRepositoryId) &&
                repositories.Contains(edge.TargetRepositoryId))
            .OrderBy(edge => edge.SourceRepositoryId == edge.TargetRepositoryId)
            .ThenBy(edge => edge.SourceDocumentVersionId)
            .ThenBy(edge => edge.EdgeType)
            .ThenBy(edge => edge.TargetDocumentVersionId)
            .ThenBy(edge => edge.Id)
            .Select(edge => new ProjectableEdgeRow(
                edge.Id,
                edge.SourceDocumentVersionId,
                edge.SourceRepositoryId,
                edge.SourceScanRunId,
                edge.TargetDocumentVersionId,
                edge.TargetRepositoryId,
                edge.TargetScanRunId,
                edge.EdgeType))
            .Take(97)
            .ToArrayAsync(cancellationToken);

        var relationships = new List<ProjectedChatRelationship>();
        var relatedSections = new List<DocumentSection>();
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
            if (additionsForSeed >= 2)
            {
                continue;
            }

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
            if (!alreadyIncluded && addedDocuments.Count >= relatedDocumentLimit)
            {
                continue;
            }

            CrossRepositoryResolutionDecision? exactDecision = null;
            if (candidate.EdgeType == DocumentEdgeType.References &&
                candidate.SourceRepositoryId != candidate.TargetRepositoryId)
            {
                exactDecision = await FindAcceptedExactDecisionAsync(
                    systemId,
                    candidate,
                    cancellationToken);
                if (exactDecision is null)
                {
                    continue;
                }
            }

            DocumentSection? relatedSection = null;
            if (!alreadyIncluded)
            {
                relatedSection = await _context.DocumentSections
                    .AsNoTracking()
                    .Where(section =>
                        section.DocumentVersionId == candidate.TargetDocumentVersionId)
                    .OrderBy(section => section.SectionOrder)
                    .ThenBy(section => section.StableSectionId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (relatedSection is null)
                {
                    continue;
                }
            }

            bool hasExactAuthority = exactDecision is not null;
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

            relationships.Add(new ProjectedChatRelationship(
                candidate.EdgeId,
                candidate.SourceDocumentVersionId,
                candidate.TargetDocumentVersionId,
                candidate.EdgeType,
                exactDecision?.EvidenceFingerprint,
                exactDecision?.ProviderEvidenceSetFingerprint));
            additionsBySeed[candidate.SourceDocumentVersionId] = additionsForSeed + 1;

            if (relatedSection is not null && addedDocuments.Add(candidate.TargetDocumentVersionId))
            {
                relatedSections.Add(relatedSection);
            }
        }

        return new ChatRelationshipProjection(relationships, relatedSections);
    }

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

    private async Task<CrossRepositoryResolutionDecision?> FindAcceptedExactDecisionAsync(
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
            .Take(2)
            .ToArrayAsync(cancellationToken);
        if (decisions.Length != 1)
        {
            return null;
        }

        CrossRepositoryResolutionDecision decision = decisions[0];
        DocumentEdge edge = await _context.DocumentEdges
            .AsNoTracking()
            .SingleAsync(item => item.Id == candidate.EdgeId, cancellationToken);
        ExactResolutionProviderEvidenceContribution[] contributions = await _context
            .ExactResolutionProviderEvidenceContributions
            .AsNoTracking()
            .Where(item => item.ResolutionDecisionId == decision.Id)
            .OrderBy(item => item.ProviderOrdinal)
            .ThenBy(item => item.EvidenceOrdinal)
            .Take(257)
            .ToArrayAsync(cancellationToken);

        return contributions.Length is >= 1 and <= 256 &&
            ExactResolutionCanonicalAuthority.MatchesPersistedExactAuthority(
                decision,
                edge,
                contributions)
            ? decision
            : null;
    }

    private sealed record ProjectableEdgeRow(
        Guid EdgeId,
        Guid SourceDocumentVersionId,
        Guid SourceRepositoryId,
        Guid SourceScanRunId,
        Guid TargetDocumentVersionId,
        Guid TargetRepositoryId,
        Guid TargetScanRunId,
        DocumentEdgeType EdgeType);
}

internal sealed record ChatRelationshipProjection(
    IReadOnlyList<ProjectedChatRelationship> Relationships,
    IReadOnlyList<DocumentSection> RelatedSections,
    bool IsInconsistent = false)
{
    internal static ChatRelationshipProjection Empty { get; } = new([], []);
    internal static ChatRelationshipProjection Invalid { get; } = new([], [], true);
}

internal sealed record ProjectedChatRelationship(
    Guid EdgeId,
    Guid SourceDocumentVersionId,
    Guid TargetDocumentVersionId,
    DocumentEdgeType EdgeType,
    string? ExactEvidenceFingerprint,
    string? ExactProviderEvidenceSetFingerprint);

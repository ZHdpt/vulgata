using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedChatScopeResolver(VulgataDbContext context) : IPreparedChatScopeResolver
{
    public async Task<PreparedChatScopeBinding?> ResolveAsync(
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repositoryIds);
        ArgumentNullException.ThrowIfNull(accessScope);
        Guid[] requestedIds = repositoryIds.Distinct().Order().ToArray();
        if (requestedIds.Length is < 2 or > 256 ||
            requestedIds.Any(repositoryId => !accessScope.Contains(repositoryId)))
        {
            return null;
        }

        PreparedEvidenceRow[] evidence = await (
            from decision in context.CrossRepositoryResolutionDecisions.AsNoTracking()
            join edge in context.DocumentEdges.AsNoTracking()
                on decision.DocumentEdgeId equals edge.Id
            join consumer in context.LogicalDocuments.AsNoTracking()
                on edge.SourceDocumentVersionId equals consumer.CurrentVersionId
            join provider in context.LogicalDocuments.AsNoTracking()
                on edge.TargetDocumentVersionId equals provider.CurrentVersionId
            where decision.ResolutionSchemaIdentity == "exact-cross-repository-resolution/2.0" &&
                decision.SystemId == systemId &&
                decision.OutcomeKind == "ExactMatch" &&
                decision.ReasonCode == "unique_provider" &&
                decision.ProviderDocumentVersionId.HasValue &&
                decision.ProviderRepositoryId.HasValue &&
                decision.ProviderScanRunId.HasValue &&
                edge.EdgeType == DocumentEdgeType.References &&
                decision.ConsumerDocumentVersionId == edge.SourceDocumentVersionId &&
                decision.ConsumerRepositoryId == edge.SourceRepositoryId &&
                decision.ConsumerScanRunId == edge.SourceScanRunId &&
                decision.ProviderDocumentVersionId.Value == edge.TargetDocumentVersionId &&
                decision.ProviderRepositoryId.Value == edge.TargetRepositoryId &&
                decision.ProviderScanRunId.Value == edge.TargetScanRunId &&
                edge.SourceRepositoryId != edge.TargetRepositoryId &&
                requestedIds.Contains(edge.SourceRepositoryId) &&
                requestedIds.Contains(edge.TargetRepositoryId)
            orderby decision.Id
            select new PreparedEvidenceRow(
                decision.Id,
                edge.Id,
                edge.SourceRepositoryId,
                edge.SourceScanRunId,
                edge.SourceDocumentVersionId,
                decision.ConsumerProfiledRevision,
                edge.TargetRepositoryId,
                edge.TargetScanRunId,
                edge.TargetDocumentVersionId,
                decision.ProviderProfiledRevision!))
            .Take(2)
            .ToArrayAsync(cancellationToken);
        if (evidence.Length != 1)
        {
            return null;
        }

        PreparedEvidenceRow exact = evidence[0];
        Guid[] exactRepositoryIds = [exact.ConsumerRepositoryId, exact.ProviderRepositoryId];
        ExactResolutionProviderEvidenceContribution[] contributions = await context
            .ExactResolutionProviderEvidenceContributions
            .AsNoTracking()
            .Where(contribution => contribution.ResolutionDecisionId == exact.DecisionId)
            .Take(257)
            .ToArrayAsync(cancellationToken);
        CrossRepositoryResolutionDecision exactDecision = await context
            .CrossRepositoryResolutionDecisions
            .AsNoTracking()
            .SingleAsync(decision => decision.Id == exact.DecisionId, cancellationToken);
        DocumentEdge exactEdge = await context.DocumentEdges
            .AsNoTracking()
            .SingleAsync(edge => edge.Id == exact.EdgeId, cancellationToken);
        if (contributions.Length is < 1 or > 256 ||
            contributions.Any(contribution =>
                contribution.ProviderDocumentVersionId != exact.ProviderDocumentVersionId ||
                contribution.RepositoryId != exact.ProviderRepositoryId ||
                contribution.ScanRunId != exact.ProviderScanRunId ||
                !StringComparer.Ordinal.Equals(
                    contribution.ProfiledRevision,
                    exact.ProviderProfiledRevision)) ||
            !ExactResolutionCanonicalAuthority.MatchesPersistedExactAuthority(
                exactDecision,
                exactEdge,
                contributions))
        {
            return null;
        }

        int repositoryCount = await context.Repositories.AsNoTracking().CountAsync(
            repository => exactRepositoryIds.Contains(repository.Id) && repository.SystemId == systemId,
            cancellationToken);
        bool consumerIsCurrent = await CurrentEndpointAsync(
            exact.ConsumerRepositoryId,
            exact.ConsumerScanRunId,
            exact.ConsumerDocumentVersionId,
            exact.ConsumerProfiledRevision,
            sourceAnchorIds: null,
            cancellationToken);
        bool providerIsCurrent = await CurrentEndpointAsync(
            exact.ProviderRepositoryId,
            exact.ProviderScanRunId,
            exact.ProviderDocumentVersionId,
            exact.ProviderProfiledRevision,
            contributions.Select(contribution => contribution.SourceAnchorId).ToArray(),
            cancellationToken);
        if (repositoryCount != 2 || !consumerIsCurrent || !providerIsCurrent)
        {
            return null;
        }

        return new PreparedChatScopeBinding(
            systemId,
            exact.ConsumerRepositoryId,
            exact.ProviderRepositoryId);
    }

    private async Task<bool> CurrentEndpointAsync(
        Guid repositoryId,
        Guid scanRunId,
        Guid documentVersionId,
        string expectedCommit,
        IReadOnlyCollection<Guid>? sourceAnchorIds,
        CancellationToken cancellationToken)
    {
        bool scanRunIsCurrent = await context.ScanRuns.AsNoTracking().AnyAsync(
            scanRun =>
                scanRun.Id == scanRunId &&
                scanRun.RepositoryId == repositoryId &&
                scanRun.Status == ScanRunStatus.Completed &&
                scanRun.ProfiledRevision == expectedCommit,
            cancellationToken);
        bool documentIsCurrent = await (
            from logical in context.LogicalDocuments.AsNoTracking()
            join version in context.DocumentVersions.AsNoTracking()
                on logical.CurrentVersionId equals version.Id
            where version.Id == documentVersionId &&
                logical.RepositoryId == repositoryId &&
                version.RepositoryId == repositoryId &&
                version.ScanRunId == scanRunId &&
                version.SupersededById == null &&
                version.ProfiledRevision == expectedCommit
            select version.Id)
            .AnyAsync(cancellationToken);
        int? expectedAnchorCount = sourceAnchorIds?.Distinct().Count();
        int currentAnchorCount = await context.SourceAnchors.AsNoTracking().CountAsync(
            anchor =>
                (sourceAnchorIds == null || sourceAnchorIds.Contains(anchor.Id)) &&
                anchor.DocumentVersionId == documentVersionId &&
                anchor.RepositoryId == repositoryId &&
                anchor.ScanRunId == scanRunId &&
                anchor.ProfiledRevision == expectedCommit,
            cancellationToken);
        bool anchorsAreCurrent = expectedAnchorCount.HasValue
            ? currentAnchorCount == expectedAnchorCount.Value
            : currentAnchorCount > 0;
        return scanRunIsCurrent && documentIsCurrent && anchorsAreCurrent;
    }

    private sealed record PreparedEvidenceRow(
        Guid DecisionId,
        Guid EdgeId,
        Guid ConsumerRepositoryId,
        Guid ConsumerScanRunId,
        Guid ConsumerDocumentVersionId,
        string ConsumerProfiledRevision,
        Guid ProviderRepositoryId,
        Guid ProviderScanRunId,
        Guid ProviderDocumentVersionId,
        string ProviderProfiledRevision);
}

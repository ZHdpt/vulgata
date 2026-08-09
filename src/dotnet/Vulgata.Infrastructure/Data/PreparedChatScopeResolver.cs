using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedChatScopeResolver(
    VulgataDbContext context,
    ExactResolutionProfileCatalog profileCatalog) : IPreparedChatScopeResolver
{
    public async Task<PreparedChatScopeBinding?> ResolveAsync(
        Guid systemId,
        IReadOnlyCollection<Guid> repositoryIds,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repositoryIds);
        ArgumentNullException.ThrowIfNull(accessScope);
        ExactResolutionProfile profile = profileCatalog.Load();
        if (profile.Repositories.Count != 2 || profile.Occurrences.Count != 1)
        {
            return null;
        }

        ResolutionOccurrence occurrence = profile.Occurrences[0];
        ProfileRepository consumerProfile = profile.Repositories.Single(
            repository => StringComparer.Ordinal.Equals(repository.Alias, occurrence.ConsumerAlias));
        ProfileRepository providerProfile = profile.Repositories.Single(
            repository => StringComparer.Ordinal.Equals(repository.Alias, occurrence.ProviderAlias));
        PreparedEvidenceRow[] evidence = await context.ExactCrossRepositoryEdgeEvidence
            .AsNoTracking()
            .Where(value =>
                value.ProfileSchemaVersion == profile.SchemaVersion &&
                value.ProfileId == profile.ProfileId &&
                value.ProfileVersion == profile.ProfileVersion &&
                value.ProfileFingerprint == profile.ProfileFingerprint &&
                value.OccurrenceId == occurrence.OccurrenceId)
            .Select(value => new PreparedEvidenceRow(
                value.EdgeId,
                value.ConsumerRepositoryId,
                value.ConsumerScanRunId,
                value.ConsumerDocumentVersionId,
                value.ConsumerSourceAnchorId,
                value.ProviderRepositoryId,
                value.ProviderScanRunId,
                value.ProviderDocumentVersionId,
                value.ProviderSourceAnchorId))
            .Take(2)
            .ToArrayAsync(cancellationToken);
        if (evidence.Length != 1)
        {
            return null;
        }

        PreparedEvidenceRow exact = evidence[0];
        Guid[] exactRepositoryIds =
            [exact.ConsumerRepositoryId, exact.ProviderRepositoryId];
        Guid[] requestedIds = repositoryIds.Distinct().Order().ToArray();
        if (requestedIds.Length is < 2 or > 256 ||
            exactRepositoryIds.Any(repositoryId => !requestedIds.Contains(repositoryId)) ||
            requestedIds.Any(repositoryId => !accessScope.Contains(repositoryId)))
        {
            return null;
        }

        int repositoryCount = await context.Repositories.AsNoTracking().CountAsync(
            repository => exactRepositoryIds.Contains(repository.Id) && repository.SystemId == systemId,
            cancellationToken);
        bool edgeIsCurrent = await context.DocumentEdges.AsNoTracking().AnyAsync(
            edge =>
                edge.Id == exact.EdgeId &&
                edge.EdgeType == DocumentEdgeType.References &&
                edge.SourceRepositoryId == exact.ConsumerRepositoryId &&
                edge.SourceDocumentVersionId == exact.ConsumerDocumentVersionId &&
                edge.TargetRepositoryId == exact.ProviderRepositoryId &&
                edge.TargetDocumentVersionId == exact.ProviderDocumentVersionId,
            cancellationToken);
        bool consumerIsCurrent = await CurrentEndpointAsync(
            exact.ConsumerRepositoryId,
            exact.ConsumerScanRunId,
            exact.ConsumerDocumentVersionId,
            exact.ConsumerSourceAnchorId,
            consumerProfile.ExpectedCommit,
            cancellationToken);
        bool providerIsCurrent = await CurrentEndpointAsync(
            exact.ProviderRepositoryId,
            exact.ProviderScanRunId,
            exact.ProviderDocumentVersionId,
            exact.ProviderSourceAnchorId,
            providerProfile.ExpectedCommit,
            cancellationToken);
        if (repositoryCount != 2 || !edgeIsCurrent || !consumerIsCurrent || !providerIsCurrent)
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
        Guid sourceAnchorId,
        string expectedCommit,
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
        bool anchorIsCurrent = await context.SourceAnchors.AsNoTracking().AnyAsync(
            anchor =>
                anchor.Id == sourceAnchorId &&
                anchor.DocumentVersionId == documentVersionId &&
                anchor.RepositoryId == repositoryId &&
                anchor.ScanRunId == scanRunId &&
                anchor.ProfiledRevision == expectedCommit,
            cancellationToken);
        return scanRunIsCurrent && documentIsCurrent && anchorIsCurrent;
    }

    private sealed record PreparedEvidenceRow(
        Guid EdgeId,
        Guid ConsumerRepositoryId,
        Guid ConsumerScanRunId,
        Guid ConsumerDocumentVersionId,
        Guid ConsumerSourceAnchorId,
        Guid ProviderRepositoryId,
        Guid ProviderScanRunId,
        Guid ProviderDocumentVersionId,
        Guid ProviderSourceAnchorId);
}

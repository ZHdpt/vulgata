using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class PreparedScanHandoffSource(VulgataDbContext dbContext)
    : IPreparedScanHandoffSource
{
    public async Task<PreparedScanHandoff?> GetAsync(
        PreparedScanAuthorizationScope authorizationScope,
        Guid coordinationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);
        Guid[] repositoryIds = authorizationScope.RepositoryIds
            .Distinct()
            .Order()
            .ToArray();
        if (authorizationScope.UserId == Guid.Empty ||
            authorizationScope.SystemId == Guid.Empty ||
            coordinationId == Guid.Empty ||
            repositoryIds.Length != 2)
        {
            return null;
        }

        int ownedRepositoryCount = await dbContext.Repositories
            .AsNoTracking()
            .CountAsync(
                repository => repositoryIds.Contains(repository.Id) &&
                    repository.SystemId == authorizationScope.SystemId,
                cancellationToken);
        if (ownedRepositoryCount != repositoryIds.Length)
        {
            return null;
        }

        PreparedScanManifest[] manifests = await dbContext
            .Set<PreparedScanManifest>()
            .AsNoTracking()
            .Where(manifest =>
                manifest.CoordinationId == coordinationId &&
                repositoryIds.Contains(manifest.RepositoryId) &&
                manifest.ApprovalState == PreparedScanManifestApprovalState.Approved &&
                manifest.SchemaIdentity == PreparedScanManifest.CurrentSchemaIdentity)
            .Include(manifest => manifest.Entries)
            .ThenInclude(entry => entry.FileCodeUnit)
            .Include(manifest => manifest.Audits)
            .AsSplitQuery()
            .ToArrayAsync(cancellationToken);
        if (manifests.Length != 2 ||
            manifests.Select(manifest => manifest.RepositoryId).Distinct().Count() != 2)
        {
            return null;
        }

        Guid[] scanRunIds = manifests.Select(manifest => manifest.ScanRunId).ToArray();
        PreScanResult[] results = await dbContext.PreScanResults
            .AsNoTracking()
            .Where(result => scanRunIds.Contains(result.ScanRunId))
            .Include(result => result.Classifications)
            .ToArrayAsync(cancellationToken);
        Guid[] contextIds = manifests.Select(manifest => manifest.ContextSnapshotId).ToArray();
        ScanContextSnapshot[] contexts = await dbContext.ScanContextSnapshots
            .AsNoTracking()
            .Where(context => contextIds.Contains(context.Id))
            .ToArrayAsync(cancellationToken);
        if (results.Length != 2 || contexts.Length != 2 ||
            results.Any(result => !result.IsPreparedV2))
        {
            return null;
        }

        string pairSelectionFingerprint = manifests[0].PairSelectionFingerprint;
        string? pairPackageFingerprint = results[0].PairPackageFingerprint;
        if (pairPackageFingerprint is null ||
            manifests.Any(manifest => !StringComparer.Ordinal.Equals(
                manifest.PairSelectionFingerprint,
                pairSelectionFingerprint)) ||
            results.Any(result => !StringComparer.Ordinal.Equals(
                result.PairPackageFingerprint,
                pairPackageFingerprint)))
        {
            return null;
        }

        var bindings = new List<PreparedScanManifestBinding>(2);
        var units = new List<PreparedFileCodeUnitDraft>();
        foreach (PreparedScanManifest manifest in manifests)
        {
            PreScanResult? result = results.SingleOrDefault(candidate =>
                candidate.RepositoryId == manifest.RepositoryId &&
                candidate.ScanRunId == manifest.ScanRunId);
            ScanContextSnapshot? context = contexts.SingleOrDefault(candidate =>
                candidate.Id == manifest.ContextSnapshotId &&
                candidate.RepositoryId == manifest.RepositoryId &&
                candidate.ScanRunId == manifest.ScanRunId);
            if (!IsCompleteBinding(manifest, result, context))
            {
                return null;
            }

            bindings.Add(new PreparedScanManifestBinding(
                manifest.RepositoryId,
                manifest.ScanRunId,
                manifest.ProfiledRevision,
                manifest.Id,
                manifest.Fingerprint,
                manifest.CodeGraphProofIdentityHash,
                manifest.JavaReadinessIdentityHash,
                manifest.PreparedScanContextIdentityHash));

            PreparedScanManifestEntry[] entries =
            [
                .. manifest.Entries.OrderBy(entry => entry.ManifestOrdinal),
            ];
            if (entries.Length != manifest.SelectedCount ||
                !entries.Select(entry => entry.ManifestOrdinal)
                    .SequenceEqual(Enumerable.Range(1, entries.Length)) ||
                manifest.Audits.Count != manifest.CandidateCount ||
                !manifest.Audits
                    .OrderBy(audit => audit.Ordinal)
                    .Select(audit => audit.Ordinal)
                    .SequenceEqual(Enumerable.Range(1, manifest.Audits.Count)))
            {
                return null;
            }

            foreach (PreparedScanManifestEntry entry in entries)
            {
                CodeUnit? unit = entry.FileCodeUnit;
                if (unit is null ||
                    entry.CoordinationId != coordinationId ||
                    unit.RepositoryId != manifest.RepositoryId ||
                    unit.ScanRunId != manifest.ScanRunId ||
                    unit.UnitType != CodeUnitType.File ||
                    unit.Status != CodeUnitStatus.Pending ||
                    unit.FileHash is null ||
                    unit.ByteStart is null ||
                    unit.ByteEndExclusive is null ||
                    unit.SegmentOrdinal is null ||
                    unit.SegmentCount is null ||
                    unit.FileCodeUnitIdentityFingerprint is null)
                {
                    return null;
                }

                units.Add(new PreparedFileCodeUnitDraft(
                    unit.Id,
                    unit.RepositoryId,
                    unit.ScanRunId,
                    manifest.ProfiledRevision,
                    unit.FilePath,
                    unit.FileHash,
                    unit.ByteStart.Value,
                    unit.ByteEndExclusive.Value,
                    unit.LineStart,
                    unit.LineEnd,
                    unit.SegmentOrdinal.Value,
                    unit.SegmentCount.Value,
                    unit.SourceHash,
                    unit.FileCodeUnitIdentityFingerprint,
                    entry.DirectBusinessScore,
                    entry.FinalBusinessScore,
                    entry.SelectionReason,
                    entry.ManifestOrdinal,
                    entry.PairSelectionOrdinal));
            }
        }

        units.Sort((left, right) => left.PairSelectionOrdinal.CompareTo(right.PairSelectionOrdinal));
        if (units.Count is < 50 or > 150 ||
            !units.Select(unit => unit.PairSelectionOrdinal)
                .SequenceEqual(Enumerable.Range(1, units.Count)))
        {
            return null;
        }

        return PreparedScanHandoffFactory.Create(
            coordinationId,
            pairSelectionFingerprint,
            pairPackageFingerprint,
            bindings,
            units,
            units.Count);
    }

    private static bool IsCompleteBinding(
        PreparedScanManifest manifest,
        PreScanResult? result,
        ScanContextSnapshot? context) =>
        result is not null &&
        context is not null &&
        result.IsPreparedV2 &&
        result.PreparedScanManifestId == manifest.Id &&
        StringComparer.Ordinal.Equals(result.PreparedScanManifestFingerprint, manifest.Fingerprint) &&
        result.ContextSnapshotId == context.Id &&
        StringComparer.Ordinal.Equals(result.PreparedScanContextIdentityHash, manifest.PreparedScanContextIdentityHash) &&
        StringComparer.Ordinal.Equals(result.ContextContentHash, manifest.ContextContentHash) &&
        StringComparer.Ordinal.Equals(context.PreparedScanContextIdentityHash, manifest.PreparedScanContextIdentityHash) &&
        StringComparer.Ordinal.Equals(context.ContentHash, manifest.ContextContentHash) &&
        StringComparer.Ordinal.Equals(context.ProfiledRevision, manifest.ProfiledRevision) &&
        StringComparer.Ordinal.Equals(result.ProfiledRevision, manifest.ProfiledRevision) &&
        result.TotalConfirmedEvidenceCount == manifest.TotalConfirmedEvidenceCount &&
        result.TotalHintCount == manifest.TotalHintCount &&
        result.TotalWarningCount == manifest.TotalWarningCount &&
        result.EvidenceTruncated == manifest.EvidenceTruncated &&
        result.Classifications.Count == manifest.RetainedClassificationCount &&
        result.Classifications.All(classification =>
            classification.PreScanResultId == result.Id);
}

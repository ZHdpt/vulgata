using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Data.October;

public sealed class OctoberBatchSelector : IOctoberBatchSelector
{
    private readonly OctoberDbContext _database;
    private readonly string _targetId;
    private readonly string _canonicalDatabaseIdentity;

    public OctoberBatchSelector(
        OctoberDbContext database,
        string targetId,
        string canonicalDatabaseIdentity)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("An exact October target is required.", nameof(targetId));
        }
        if (string.IsNullOrWhiteSpace(canonicalDatabaseIdentity))
        {
            throw new ArgumentException("A canonical database identity is required.",
                nameof(canonicalDatabaseIdentity));
        }

        _targetId = targetId;
        _canonicalDatabaseIdentity = canonicalDatabaseIdentity;
    }

    public async Task<OctoberSelectionResult> BindAsync(
        OctoberStartupSelection selection,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Rejected(OctoberSelectionStatus.Cancelled);
        if (selection is null || string.IsNullOrWhiteSpace(selection.TargetId) ||
            !string.Equals(selection.TargetId, _targetId, StringComparison.Ordinal))
            return Rejected(OctoberSelectionStatus.MissingTarget);
        if (!ValidAuthority(selection))
            return Rejected(OctoberSelectionStatus.Denied);

        try
        {
            OctoberBatchRecord? batch = await _database.Batches.AsNoTracking()
                .SingleOrDefaultAsync(value => value.BatchId == selection.BatchId,
                    cancellationToken);
            if (batch is null)
            {
                bool anotherBatch = await _database.Batches.AsNoTracking()
                    .AnyAsync(cancellationToken);
                return Rejected(anotherBatch
                    ? OctoberSelectionStatus.BatchMismatch : OctoberSelectionStatus.MissingBatch);
            }
            if (batch.State != OctoberBatchState.Selectable ||
                !string.Equals(batch.TargetId, _targetId, StringComparison.Ordinal) ||
                !string.Equals(batch.CanonicalDatabaseIdentity,
                    _canonicalDatabaseIdentity, StringComparison.Ordinal) ||
                !string.Equals(batch.SubjectId, selection.Authority.SubjectId,
                    StringComparison.Ordinal) ||
                !string.Equals(batch.ManagementAuthorityId,
                    selection.Authority.SelectedContext!.ManagementAuthorityId, StringComparison.Ordinal) ||
                batch.SelectedAtUtc.Offset != TimeSpan.Zero)
                return Rejected(OctoberSelectionStatus.Denied);

            OctoberRepositoryRecord[] repositories = await _database.RepositorySelections.AsNoTracking()
                .Where(value => value.BatchId == batch.BatchId).ToArrayAsync(cancellationToken);
            OctoberContextReferenceRecord[] references = await _database.ContextReferences.AsNoTracking()
                .Where(value => value.BatchId == batch.BatchId).ToArrayAsync(cancellationToken);
            OctoberAdvisoryEntryRecord[] entries = await _database.AdvisoryEntries.AsNoTracking()
                .Where(value => value.BatchId == batch.BatchId).ToArrayAsync(cancellationToken);
            OctoberContextDifferenceRecord[] differences = await _database.ContextDifferences.AsNoTracking()
                .Where(value => value.BatchId == batch.BatchId).ToArrayAsync(cancellationToken);
            if (!MatchesStableSelection(selection.Authority, repositories, references))
                return Rejected(OctoberSelectionStatus.Denied);

            OctoberRepositorySelection[] storedRepositories = repositories
                .OrderBy(value => value.RepositoryId)
                .Select(value => new OctoberRepositorySelection(value.RepositoryId,
                    value.SystemId, value.RootBindingIdentity, value.OperatorLabel)).ToArray();
            OctoberContextSelection[] storedReferences = references
                .OrderBy(value => value.Scope).ThenBy(value => value.RecordId)
                .Select(value => new OctoberContextSelection((OctoberContextScope)value.Scope,
                    value.RecordId)).ToArray();
            OctoberSelectedAdvisoryContext historicalContext = new(
                batch.ManagementAuthorityId,
                storedRepositories.ToImmutableArray(),
                entries.OrderBy(value => value.RepositoryId).ThenBy(value => value.Scope)
                    .Select(value => new OctoberAdvisoryContextEntry(value.RepositoryId,
                        (OctoberContextScope)value.Scope,
                        (OctoberContextApplicability)value.Applicability,
                        value.OriginRecordId, value.Value,
                        (OctoberContextEvidenceKind)value.EvidenceKind)).ToImmutableArray(),
                differences.OrderBy(value => value.RepositoryId).ThenBy(value => value.LeftScope)
                    .ThenBy(value => value.RightScope)
                    .Select(value => new OctoberContextDifference(value.RepositoryId,
                        (OctoberContextScope)value.LeftScope,
                        (OctoberContextScope)value.RightScope)).ToImmutableArray());
            OctoberSetupRequest historicalRequest = new(batch.TargetId, batch.BatchId,
                storedRepositories.ToImmutableArray(), storedReferences.ToImmutableArray(),
                batch.SelectionDigest);
            if (!string.Equals(OctoberSelectionDigest.Compute(historicalRequest, historicalContext),
                    batch.SelectionDigest, StringComparison.Ordinal))
                return Rejected(OctoberSelectionStatus.Denied);

            OctoberAuthorizedSelection historicalAuthority = new(batch.TargetId, batch.BatchId,
                batch.SubjectId, batch.CanonicalDatabaseIdentity,
                storedRepositories.Select(value => value.RepositoryId).ToImmutableArray(),
                batch.SelectionDigest, historicalContext);
            OctoberBoundSetupSnapshot snapshot = new(historicalRequest, historicalAuthority,
                batch.SelectedAtUtc);
            return new OctoberSelectionResult(OctoberSelectionStatus.Selected,
                new HistoricalBoundSetupData(batch.TargetId, batch.BatchId, snapshot));
        }
        catch (OperationCanceledException)
        {
            return Rejected(OctoberSelectionStatus.Cancelled);
        }
        catch (ArgumentException)
        {
            return Rejected(OctoberSelectionStatus.Denied);
        }
        catch (Exception)
        {
            return Rejected(OctoberSelectionStatus.AuthorityUnavailable);
        }
    }

    private bool ValidAuthority(OctoberStartupSelection selection)
    {
        OctoberAuthorizedSelection? authority = selection.Authority;
        return selection.BatchId != Guid.Empty && authority is not null &&
            authority.SelectedContext is not null &&
            !string.IsNullOrWhiteSpace(authority.SelectedContext.ManagementAuthorityId) &&
            !string.IsNullOrWhiteSpace(authority.SubjectId) &&
            string.Equals(authority.TargetId, selection.TargetId, StringComparison.Ordinal) &&
            authority.BatchId == selection.BatchId &&
            string.Equals(authority.CanonicalDatabaseIdentity,
                _canonicalDatabaseIdentity, StringComparison.Ordinal) &&
            authority.RepositoryIds is { Count: > 0 } &&
            !authority.SelectedContext.Repositories.IsDefault &&
            !authority.SelectedContext.Entries.IsDefault;
    }

    private static bool MatchesStableSelection(
        OctoberAuthorizedSelection authority,
        OctoberRepositoryRecord[] repositories,
        OctoberContextReferenceRecord[] references)
    {
        OctoberSelectedAdvisoryContext current = authority.SelectedContext!;
        if (repositories.Length != authority.RepositoryIds.Count ||
            repositories.Length != current.Repositories.Length ||
            current.Repositories.Select(value => value.RepositoryId).Distinct().Count() !=
                repositories.Length ||
            !repositories.Select(value => value.RepositoryId).ToHashSet()
                .SetEquals(authority.RepositoryIds) ||
            current.Repositories.Any(selected => !repositories.Any(stored =>
                stored.RepositoryId == selected.RepositoryId && stored.SystemId == selected.SystemId &&
                string.Equals(stored.RootBindingIdentity, selected.RootBindingIdentity,
                    StringComparison.Ordinal) &&
                string.Equals(stored.OperatorLabel, selected.OperatorLabel, StringComparison.Ordinal))))
            return false;

        HashSet<(int Scope, Guid RecordId)> currentReferences = current.Entries
            .Where(value => value.Applicability == OctoberContextApplicability.Selected &&
                value.OriginRecordId is not null)
            .Select(value => ((int)value.Scope, value.OriginRecordId!.Value)).ToHashSet();
        return references.Length == currentReferences.Count &&
            references.All(value => currentReferences.Contains((value.Scope, value.RecordId)));
    }

    private static OctoberSelectionResult Rejected(OctoberSelectionStatus status) => new(status, null);

    private sealed record HistoricalBoundSetupData(
        string TargetId,
        Guid BatchId,
        OctoberBoundSetupSnapshot Snapshot) : IOctoberHistoricalBoundSetupData;
}

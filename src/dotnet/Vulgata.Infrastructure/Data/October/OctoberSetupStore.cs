using Vulgata.Core.DomainServices;
using Microsoft.EntityFrameworkCore;

namespace Vulgata.Infrastructure.Data.October;

public sealed class OctoberSetupStore : IOctoberSetupWriter
{
    private readonly OctoberDbContext _database;
    private readonly TimeProvider _clock;

    public OctoberSetupStore(OctoberDbContext database, TimeProvider clock)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<OctoberPrepareResult> CommitAsync(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return new OctoberPrepareResult(OctoberPrepareStatus.Cancelled, null);
        if (request is null || authority is null || authority.SelectedContext is null ||
            request.BatchId == Guid.Empty || string.IsNullOrWhiteSpace(request.TargetId) ||
            string.IsNullOrWhiteSpace(authority.SubjectId) ||
            string.IsNullOrWhiteSpace(authority.CanonicalDatabaseIdentity) ||
            !string.Equals(request.TargetId, authority.TargetId, StringComparison.Ordinal) ||
            request.BatchId != authority.BatchId ||
            !string.Equals(request.SelectionDigest, authority.SelectionDigest, StringComparison.Ordinal))
            return new OctoberPrepareResult(OctoberPrepareStatus.Denied, null);

        if (request.Repositories.Count != authority.RepositoryIds.Count ||
            request.Repositories.Count != authority.SelectedContext.Repositories.Length ||
            !request.Repositories.Select(value => value.RepositoryId)
                .ToHashSet().SetEquals(authority.RepositoryIds) ||
            request.Repositories.Any(value => !authority.SelectedContext.Repositories.Contains(value)))
            return new OctoberPrepareResult(OctoberPrepareStatus.Denied, null);

        try
        {
            if (!string.Equals(OctoberSelectionDigest.Compute(request, authority.SelectedContext),
                    request.SelectionDigest, StringComparison.Ordinal))
                return new OctoberPrepareResult(OctoberPrepareStatus.Denied, null);
        }
        catch (ArgumentException)
        {
            return new OctoberPrepareResult(OctoberPrepareStatus.Denied, null);
        }

        try
        {
            await using var transaction = await _database.Database.BeginTransactionAsync(cancellationToken);
            OctoberBatchRecord? existing = await _database.Batches.AsNoTracking()
                .SingleOrDefaultAsync(value => value.SelectionSlot == 1, cancellationToken);
            if (existing is not null)
            {
                bool identical = await MatchesExistingAsync(existing, request, authority, cancellationToken);
                return identical
                    ? new OctoberPrepareResult(OctoberPrepareStatus.AlreadyIdentical,
                        Committed(existing))
                    : new OctoberPrepareResult(OctoberPrepareStatus.Conflict, null);
            }

            DateTimeOffset selectedAtUtc = _clock.GetUtcNow().ToUniversalTime();
            _database.Batches.Add(new OctoberBatchRecord
            {
                BatchId = request.BatchId,
                TargetId = request.TargetId,
                SubjectId = authority.SubjectId,
                CanonicalDatabaseIdentity = authority.CanonicalDatabaseIdentity,
                SelectionDigest = request.SelectionDigest,
                ManagementAuthorityId = authority.SelectedContext.ManagementAuthorityId,
                SelectedAtUtc = selectedAtUtc,
                State = OctoberBatchState.Selectable
            });
            _database.RepositorySelections.AddRange(request.Repositories.Select(repository =>
                new OctoberRepositoryRecord
                {
                    BatchId = request.BatchId,
                    RepositoryId = repository.RepositoryId,
                    SystemId = repository.SystemId,
                    RootBindingIdentity = repository.RootBindingIdentity,
                    OperatorLabel = repository.OperatorLabel
                }));
            _database.ContextReferences.AddRange(request.ContextSelections.Select(reference =>
                new OctoberContextReferenceRecord
                {
                    BatchId = request.BatchId,
                    Scope = (int)reference.Scope,
                    RecordId = reference.RecordId
                }));
            _database.AdvisoryEntries.AddRange(authority.SelectedContext.Entries.Select(entry =>
                new OctoberAdvisoryEntryRecord
                {
                    BatchId = request.BatchId,
                    RepositoryId = entry.RepositoryId,
                    Scope = (int)entry.Scope,
                    Applicability = (int)entry.Applicability,
                    OriginRecordId = entry.OriginRecordId,
                    Value = entry.Value,
                    EvidenceKind = (int)entry.EvidenceKind
                }));
            _database.ContextDifferences.AddRange(authority.SelectedContext.Differences.Select(difference =>
                new OctoberContextDifferenceRecord
                {
                    BatchId = request.BatchId,
                    RepositoryId = difference.RepositoryId,
                    LeftScope = (int)difference.LeftScope,
                    RightScope = (int)difference.RightScope
                }));
            await _database.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None);
            return new OctoberPrepareResult(OctoberPrepareStatus.Created,
                new OctoberCommittedBatch(request.TargetId, request.BatchId,
                    authority.CanonicalDatabaseIdentity, request.SelectionDigest, selectedAtUtc));
        }
        catch (OperationCanceledException)
        {
            return new OctoberPrepareResult(OctoberPrepareStatus.Cancelled, null);
        }
        catch (Exception)
        {
            return await ReconcileAfterFailureAsync(request, authority);
        }
    }

    private async Task<OctoberPrepareResult> ReconcileAfterFailureAsync(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority)
    {
        try
        {
            _database.ChangeTracker.Clear();
            OctoberBatchRecord? retained = await _database.Batches.AsNoTracking()
                .SingleOrDefaultAsync(value => value.SelectionSlot == 1, CancellationToken.None);
            if (retained is null)
                return new OctoberPrepareResult(OctoberPrepareStatus.StorageFailure, null);

            return await MatchesExistingAsync(retained, request, authority, CancellationToken.None)
                ? new OctoberPrepareResult(OctoberPrepareStatus.AlreadyIdentical, Committed(retained))
                : new OctoberPrepareResult(OctoberPrepareStatus.Conflict, null);
        }
        catch (Exception)
        {
            return new OctoberPrepareResult(OctoberPrepareStatus.StorageFailure, null);
        }
    }

    private async Task<bool> MatchesExistingAsync(
        OctoberBatchRecord existing,
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority,
        CancellationToken cancellationToken)
    {
        if (existing.BatchId != request.BatchId || existing.State != OctoberBatchState.Selectable ||
            !string.Equals(existing.TargetId, request.TargetId, StringComparison.Ordinal) ||
            !string.Equals(existing.SubjectId, authority.SubjectId, StringComparison.Ordinal) ||
            !string.Equals(existing.CanonicalDatabaseIdentity,
                authority.CanonicalDatabaseIdentity, StringComparison.Ordinal) ||
            !string.Equals(existing.SelectionDigest, request.SelectionDigest, StringComparison.Ordinal) ||
            !string.Equals(existing.ManagementAuthorityId,
                authority.SelectedContext!.ManagementAuthorityId, StringComparison.Ordinal))
            return false;

        OctoberRepositoryRecord[] repositories = await _database.RepositorySelections.AsNoTracking()
            .Where(value => value.BatchId == request.BatchId).ToArrayAsync(cancellationToken);
        OctoberContextReferenceRecord[] references = await _database.ContextReferences.AsNoTracking()
            .Where(value => value.BatchId == request.BatchId).ToArrayAsync(cancellationToken);
        OctoberAdvisoryEntryRecord[] entries = await _database.AdvisoryEntries.AsNoTracking()
            .Where(value => value.BatchId == request.BatchId).ToArrayAsync(cancellationToken);
        OctoberContextDifferenceRecord[] differences = await _database.ContextDifferences.AsNoTracking()
            .Where(value => value.BatchId == request.BatchId).ToArrayAsync(cancellationToken);

        return repositories.Length == request.Repositories.Count &&
            request.Repositories.All(selected => repositories.Any(stored =>
                stored.RepositoryId == selected.RepositoryId && stored.SystemId == selected.SystemId &&
                string.Equals(stored.RootBindingIdentity, selected.RootBindingIdentity, StringComparison.Ordinal) &&
                string.Equals(stored.OperatorLabel, selected.OperatorLabel, StringComparison.Ordinal))) &&
            references.Length == request.ContextSelections.Count &&
            request.ContextSelections.All(selected => references.Any(stored =>
                stored.Scope == (int)selected.Scope && stored.RecordId == selected.RecordId)) &&
            entries.Length == authority.SelectedContext.Entries.Length &&
            authority.SelectedContext.Entries.All(selected => entries.Any(stored =>
                stored.RepositoryId == selected.RepositoryId && stored.Scope == (int)selected.Scope &&
                stored.Applicability == (int)selected.Applicability &&
                stored.OriginRecordId == selected.OriginRecordId &&
                string.Equals(stored.Value, selected.Value, StringComparison.Ordinal) &&
                stored.EvidenceKind == (int)selected.EvidenceKind)) &&
            differences.Length == authority.SelectedContext.Differences.Length &&
            authority.SelectedContext.Differences.All(selected => differences.Any(stored =>
                stored.RepositoryId == selected.RepositoryId &&
                stored.LeftScope == (int)selected.LeftScope && stored.RightScope == (int)selected.RightScope));
    }

    private static OctoberCommittedBatch Committed(OctoberBatchRecord batch) => new(
        batch.TargetId, batch.BatchId, batch.CanonicalDatabaseIdentity,
        batch.SelectionDigest, batch.SelectedAtUtc);
}

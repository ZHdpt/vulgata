using System.Collections.Immutable;

namespace Vulgata.Core.DomainServices;

public sealed class OctoberBatchSetupPolicy
{
    private readonly IOctoberSetupWriter _writer;

    public OctoberBatchSetupPolicy(IOctoberSetupWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public async Task<OctoberPrepareResult> PrepareAsync(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new OctoberPrepareResult(OctoberPrepareStatus.Cancelled, null);
        }

        if (request is null || authority is null || request.Repositories is null ||
            request.ContextSelections is null || authority.RepositoryIds is null ||
            authority.SelectedContext is null)
        {
            return new OctoberPrepareResult(OctoberPrepareStatus.Denied, null);
        }

        OctoberSetupRequest frozenRequest = request with
        {
            Repositories = request.Repositories.ToImmutableArray(),
            ContextSelections = request.ContextSelections.ToImmutableArray()
        };
        OctoberSelectedAdvisoryContext context = authority.SelectedContext with
        {
            Repositories = authority.SelectedContext.Repositories.IsDefault
                ? default : authority.SelectedContext.Repositories.ToImmutableArray(),
            Entries = authority.SelectedContext.Entries.IsDefault
                ? default : authority.SelectedContext.Entries.ToImmutableArray(),
            Differences = authority.SelectedContext.Differences.IsDefault
                ? default : authority.SelectedContext.Differences.ToImmutableArray()
        };
        OctoberAuthorizedSelection frozenAuthority = authority with
        {
            RepositoryIds = authority.RepositoryIds.ToImmutableArray(),
            SelectedContext = context
        };
        if (!IsExactAuthorizedSelection(frozenRequest, frozenAuthority))
            return new OctoberPrepareResult(OctoberPrepareStatus.Denied, null);

        OctoberPrepareResult result = await _writer.CommitAsync(frozenRequest, frozenAuthority, cancellationToken);
        if (result.Status is not (OctoberPrepareStatus.Created or OctoberPrepareStatus.AlreadyIdentical))
        {
            return result with { CommittedBatch = null };
        }

        OctoberCommittedBatch? committed = result.CommittedBatch;
        if (committed is null ||
            !string.Equals(committed.TargetId, frozenRequest.TargetId, StringComparison.Ordinal) ||
            committed.BatchId != frozenRequest.BatchId ||
            !string.Equals(committed.CanonicalDatabaseIdentity, frozenAuthority.CanonicalDatabaseIdentity,
                StringComparison.Ordinal) ||
            !string.Equals(committed.SelectionDigest, frozenRequest.SelectionDigest, StringComparison.Ordinal) ||
            committed.SelectedAtUtc is null || committed.SelectedAtUtc.Value.Offset != TimeSpan.Zero)
        {
            return new OctoberPrepareResult(OctoberPrepareStatus.StorageFailure, null);
        }

        return result;
    }

    private static bool IsExactAuthorizedSelection(
        OctoberSetupRequest? request,
        OctoberAuthorizedSelection? authority)
    {
        if (request is null || authority is null ||
            string.IsNullOrWhiteSpace(request.TargetId) ||
            request.BatchId == Guid.Empty ||
            string.IsNullOrWhiteSpace(authority.SubjectId) ||
            string.IsNullOrWhiteSpace(authority.CanonicalDatabaseIdentity) ||
            string.IsNullOrWhiteSpace(request.SelectionDigest) ||
            request.Repositories is null ||
            request.ContextSelections is null ||
            authority.RepositoryIds is null ||
            !string.Equals(request.TargetId, authority.TargetId, StringComparison.Ordinal) ||
            request.BatchId != authority.BatchId ||
            !string.Equals(request.SelectionDigest, authority.SelectionDigest, StringComparison.Ordinal) ||
            request.Repositories.Count == 0 ||
            request.Repositories.Count != authority.RepositoryIds.Count)
        {
            return false;
        }

        HashSet<Guid> selectedRepositoryIds = [];
        foreach (OctoberRepositorySelection repository in request.Repositories)
        {
            if (repository is null || repository.RepositoryId == Guid.Empty ||
                repository.SystemId == Guid.Empty ||
                string.IsNullOrWhiteSpace(repository.RootBindingIdentity) ||
                string.IsNullOrWhiteSpace(repository.OperatorLabel) ||
                !selectedRepositoryIds.Add(repository.RepositoryId))
            {
                return false;
            }
        }

        HashSet<Guid> authorizedRepositoryIds = [];
        foreach (Guid repositoryId in authority.RepositoryIds)
        {
            if (repositoryId == Guid.Empty || !authorizedRepositoryIds.Add(repositoryId))
            {
                return false;
            }
        }

        if (!selectedRepositoryIds.SetEquals(authorizedRepositoryIds) || authority.SelectedContext is null)
            return false;

        OctoberSelectedAdvisoryContext context = authority.SelectedContext;
        try
        {
            if (!IsValidContext(request, context) ||
                !string.Equals(OctoberSelectionDigest.Compute(request, context),
                    request.SelectionDigest, StringComparison.Ordinal))
                return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        return true;
    }

    private static bool IsValidContext(OctoberSetupRequest request, OctoberSelectedAdvisoryContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ManagementAuthorityId) ||
            context.ManagementAuthorityId != context.ManagementAuthorityId.Trim() ||
            context.Repositories.IsDefault || context.Entries.IsDefault || context.Differences.IsDefault ||
            context.Repositories.Length != request.Repositories.Count ||
            context.Entries.Length != request.Repositories.Count * 3)
            return false;

        Dictionary<Guid, OctoberRepositorySelection> selectedRepositories =
            request.Repositories.ToDictionary(repository => repository.RepositoryId);
        HashSet<Guid> trustedRepositories = [];
        foreach (OctoberRepositorySelection repository in context.Repositories)
        {
            if (repository is null || !trustedRepositories.Add(repository.RepositoryId) ||
                !selectedRepositories.TryGetValue(repository.RepositoryId, out OctoberRepositorySelection? selected) ||
                repository != selected)
                return false;
        }

        HashSet<(OctoberContextScope, Guid)> references = [];
        foreach (OctoberContextSelection reference in request.ContextSelections)
        {
            if (reference is null || reference.RecordId == Guid.Empty ||
                reference.Scope is not (OctoberContextScope.Global or OctoberContextScope.System or
                    OctoberContextScope.Repository) ||
                !references.Add((reference.Scope, reference.RecordId)))
                return false;
        }
        if (references.Count(value => value.Item1 == OctoberContextScope.Global) > 1)
            return false;

        Dictionary<(Guid, OctoberContextScope), OctoberAdvisoryContextEntry> slots = [];
        foreach (OctoberAdvisoryContextEntry entry in context.Entries)
        {
            if (entry is null || !selectedRepositories.ContainsKey(entry.RepositoryId) ||
                entry.EvidenceKind != OctoberContextEvidenceKind.AdvisoryNotSourceProof ||
                !slots.TryAdd((entry.RepositoryId, entry.Scope), entry))
                return false;
        }

        HashSet<(OctoberContextScope, Guid)> consumed = [];
        string? globalValue = null;
        bool sawGlobalValue = false;
        HashSet<(Guid, OctoberContextScope, OctoberContextScope)> expectedDifferences = [];
        foreach (OctoberRepositorySelection repository in request.Repositories)
        {
            List<OctoberAdvisoryContextEntry> selectedSlots = [];
            foreach (OctoberContextScope scope in Enum.GetValues<OctoberContextScope>())
            {
                if (!slots.TryGetValue((repository.RepositoryId, scope), out OctoberAdvisoryContextEntry? entry))
                    return false;

                Guid? expectedOrigin = scope switch
                {
                    OctoberContextScope.Global => references
                        .Where(value => value.Item1 == scope).Select(value => (Guid?)value.Item2).SingleOrDefault(),
                    OctoberContextScope.System => repository.SystemId is Guid systemId &&
                        references.Contains((scope, systemId)) ? systemId : null,
                    OctoberContextScope.Repository => references.Contains((scope, repository.RepositoryId))
                        ? repository.RepositoryId : null,
                    _ => null
                };
                OctoberContextApplicability expectedApplicability =
                    scope == OctoberContextScope.System && repository.SystemId is null
                        ? OctoberContextApplicability.NotApplicable
                        : expectedOrigin is null
                            ? OctoberContextApplicability.NotSelected
                            : OctoberContextApplicability.Selected;
                if (entry.Applicability != expectedApplicability || entry.OriginRecordId != expectedOrigin ||
                    (expectedOrigin is null && entry.Value is not null))
                    return false;

                if (expectedOrigin is Guid origin)
                {
                    consumed.Add((scope, origin));
                    selectedSlots.Add(entry);
                    if (scope == OctoberContextScope.Global)
                    {
                        if (sawGlobalValue && !string.Equals(globalValue, entry.Value, StringComparison.Ordinal))
                            return false;
                        globalValue = entry.Value;
                        sawGlobalValue = true;
                    }
                }
            }

            for (int left = 0; left < selectedSlots.Count; left++)
            for (int right = left + 1; right < selectedSlots.Count; right++)
            {
                if (!string.Equals(selectedSlots[left].Value, selectedSlots[right].Value,
                        StringComparison.Ordinal))
                    expectedDifferences.Add((repository.RepositoryId,
                        selectedSlots[left].Scope, selectedSlots[right].Scope));
            }
        }
        if (!references.SetEquals(consumed))
            return false;

        HashSet<(Guid, OctoberContextScope, OctoberContextScope)> actualDifferences = [];
        foreach (OctoberContextDifference difference in context.Differences)
        {
            if (difference is null || difference.LeftScope >= difference.RightScope ||
                !actualDifferences.Add((difference.RepositoryId, difference.LeftScope, difference.RightScope)))
                return false;
        }
        return expectedDifferences.SetEquals(actualDifferences);
    }
}

namespace Vulgata.Core.DomainServices;

public sealed record OctoberSetupRequest(
    string TargetId,
    Guid BatchId,
    IReadOnlyList<OctoberRepositorySelection> Repositories,
    IReadOnlyList<OctoberContextSelection> ContextSelections,
    string SelectionDigest);

public sealed record OctoberRepositorySelection(
    Guid RepositoryId,
    Guid? SystemId,
    string RootBindingIdentity,
    string OperatorLabel);

public enum OctoberContextScope
{
    Global,
    System,
    Repository
}

public sealed record OctoberContextSelection(
    OctoberContextScope Scope,
    Guid RecordId);

public sealed record OctoberAuthorizedSelection(
    string TargetId,
    Guid BatchId,
    string SubjectId,
    string CanonicalDatabaseIdentity,
    IReadOnlyList<Guid> RepositoryIds,
    string SelectionDigest,
    OctoberSelectedAdvisoryContext? SelectedContext = null);

public sealed record OctoberSelectedAdvisoryContext(
    string ManagementAuthorityId,
    System.Collections.Immutable.ImmutableArray<OctoberRepositorySelection> Repositories,
    System.Collections.Immutable.ImmutableArray<OctoberAdvisoryContextEntry> Entries,
    System.Collections.Immutable.ImmutableArray<OctoberContextDifference> Differences);

public enum OctoberContextApplicability
{
    Selected,
    NotSelected,
    NotApplicable
}

public enum OctoberContextEvidenceKind
{
    AdvisoryNotSourceProof
}

public sealed record OctoberAdvisoryContextEntry(
    Guid RepositoryId,
    OctoberContextScope Scope,
    OctoberContextApplicability Applicability,
    Guid? OriginRecordId,
    string? Value,
    OctoberContextEvidenceKind EvidenceKind);

public sealed record OctoberContextDifference(
    Guid RepositoryId,
    OctoberContextScope LeftScope,
    OctoberContextScope RightScope);

public enum OctoberPrepareStatus
{
    Created,
    AlreadyIdentical,
    Conflict,
    Denied,
    AuthorityUnavailable,
    Cancelled,
    StorageFailure
}

public sealed record OctoberCommittedBatch(
    string TargetId,
    Guid BatchId,
    string CanonicalDatabaseIdentity,
    string SelectionDigest,
    DateTimeOffset? SelectedAtUtc = null);

public sealed record OctoberPrepareResult(
    OctoberPrepareStatus Status,
    OctoberCommittedBatch? CommittedBatch);

public interface IOctoberSetupWriter
{
    Task<OctoberPrepareResult> CommitAsync(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority,
        CancellationToken cancellationToken = default);
}

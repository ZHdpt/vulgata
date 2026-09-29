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
    string SelectionDigest);

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
    string SelectionDigest);

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

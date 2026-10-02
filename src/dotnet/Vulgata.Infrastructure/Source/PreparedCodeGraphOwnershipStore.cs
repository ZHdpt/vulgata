using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

internal sealed record PreparedToolDirectoryObjectIdentity(
    uint VolumeSerialNumber, uint FileIndexHigh, uint FileIndexLow);

internal enum PreparedCodeGraphOwnershipPhase
{
    Absent, Creating, Ready, Disposing, Removed, Quarantined,
}

internal sealed record PreparedCodeGraphWorkspaceOwnership(
    Guid OwnershipId,
    long Version,
    PreparedCodeGraphOwnershipPhase Phase,
    PreparedSourceApproval Approval,
    string PhysicalRootPath,
    PreparedToolDirectoryObjectIdentity RootObjectIdentity,
    string WorkspaceOwnerIdentity,
    PreparedToolDirectoryObjectIdentity? StateEntryObjectIdentity);

internal enum PreparedCodeGraphOwnershipTransitionStatus
{
    Committed, Conflict, Unavailable,
}

internal sealed record PreparedCodeGraphOwnershipTransitionResult(
    PreparedCodeGraphOwnershipTransitionStatus Status,
    PreparedCodeGraphWorkspaceOwnership? Record);

internal enum PreparedCodeGraphOwnershipLoadStatus
{
    Found, VerifiedAbsent, Unavailable,
}

internal sealed record PreparedCodeGraphOwnershipLoadResult(
    PreparedCodeGraphOwnershipLoadStatus Status,
    PreparedCodeGraphWorkspaceOwnership? Record);

internal enum PreparedCodeGraphOwnershipViewStatus
{
    VerifiedOwned, VerifiedUnowned, Unavailable,
}

internal sealed record PreparedCodeGraphOwnershipViewResult(
    PreparedCodeGraphOwnershipViewStatus Status,
    PreparedCodeGraphWorkspaceOwnership? Record);

/// <summary>
/// The configured application-owned store must persist every transition atomically
/// with an ownership-ID and version comparison before returning Committed.
/// No in-memory implementation is supplied as a production fallback.
/// </summary>
internal interface IPreparedCodeGraphOwnershipStore
{
    Task<PreparedCodeGraphOwnershipLoadResult> LoadAsync(
        PreparedCodeGraphWorkspaceOwnership expectedBinding,
        CancellationToken cancellationToken = default);

    Task<PreparedCodeGraphOwnershipLoadResult> FindExactWorkspaceAsync(
        PreparedSourceApproval approval,
        string physicalRootPath,
        PreparedToolDirectoryObjectIdentity rootObjectIdentity,
        string derivedWorkspaceOwnerIdentity,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PreparedCodeGraphOwnershipLoadResult(
            PreparedCodeGraphOwnershipLoadStatus.Unavailable, null));

    Task<PreparedCodeGraphOwnershipTransitionResult> BeginCreateAsync(
        PreparedCodeGraphWorkspaceOwnership absent,
        CancellationToken cancellationToken = default);

    Task<PreparedCodeGraphOwnershipTransitionResult> CommitCreatedAsync(
        PreparedCodeGraphWorkspaceOwnership creating,
        PreparedToolDirectoryObjectIdentity createdEntry,
        CancellationToken cancellationToken = default);

    Task<PreparedCodeGraphOwnershipTransitionResult> BeginDispositionAsync(
        PreparedCodeGraphWorkspaceOwnership ready,
        CancellationToken cancellationToken = default);

    Task<PreparedCodeGraphOwnershipTransitionResult> CommitRemovedAsync(
        PreparedCodeGraphWorkspaceOwnership disposing,
        CancellationToken cancellationToken = default);

    Task<PreparedCodeGraphOwnershipTransitionResult> QuarantineAsync(
        PreparedCodeGraphWorkspaceOwnership current,
        CancellationToken cancellationToken = default);
}

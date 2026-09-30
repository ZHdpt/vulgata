using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

internal interface IPreparedCodeGraphWorkspaceOwnershipVerifier
{
    ValueTask<bool> VerifyCurrentAsync(
        PreparedCodeGraphWorkspaceOwnership ownership,
        CancellationToken cancellationToken = default);

    ValueTask<bool> VerifyDispositionReadyAsync(
        PreparedCodeGraphWorkspaceOwnership ownership,
        CancellationToken cancellationToken = default);
}

internal sealed class PreparedCodeGraphStateScope
{
    private readonly PreparedToolRootScope _root;

    internal PreparedCodeGraphStateScope(PreparedToolRootScope root) => _root = root;

    internal string GetPhysicalStatePathForTrustedToolBinding() =>
        Path.Combine(_root.GetPhysicalRootPathForTrustedToolBinding(), ".codegraph");
}

/// <summary>
/// Coordinates one owned state transition under a current held prepared root.
/// A configured durable ownership store is mandatory; a store error never means
/// that a workspace is absent or available for adoption.
/// </summary>
internal sealed class PreparedCodeGraphStateCoordinator
{
    private readonly IPreparedSourceCurrentAuthorityVerifier _sourceAuthority;
    private readonly IPreparedToolUseAuthorizer _toolAuthorization;
    private readonly IPreparedCodeGraphWorkspaceOwnershipVerifier _workspaceOwner;
    private readonly IPreparedCodeGraphOwnershipStore _store;

    internal PreparedCodeGraphStateCoordinator(
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization,
        IPreparedCodeGraphWorkspaceOwnershipVerifier workspaceOwnershipVerifier,
        IPreparedCodeGraphOwnershipStore ownershipStore)
    {
        _sourceAuthority = sourceAuthority ?? throw new ArgumentNullException(nameof(sourceAuthority));
        _toolAuthorization = toolAuthorization ?? throw new ArgumentNullException(nameof(toolAuthorization));
        _workspaceOwner = workspaceOwnershipVerifier ?? throw new ArgumentNullException(nameof(workspaceOwnershipVerifier));
        _store = ownershipStore ?? throw new ArgumentNullException(nameof(ownershipStore));
    }

    internal async Task<PreparedToolWorkspaceCheckStatus> UseOwnedStateAsync(
        PreparedToolWorkspaceLease codeGraphLease,
        PreparedCodeGraphWorkspaceOwnership workspaceOwnership,
        Func<PreparedCodeGraphStateScope, CancellationToken, ValueTask> use,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codeGraphLease);
        ArgumentNullException.ThrowIfNull(workspaceOwnership);
        ArgumentNullException.ThrowIfNull(use);
        if (codeGraphLease.ToolPurpose != PreparedToolPurpose.CodeGraphPreparation ||
            codeGraphLease.RepositoryId != workspaceOwnership.Approval.RepositoryId ||
            !ValidBinding(workspaceOwnership))
        {
            return PreparedToolWorkspaceCheckStatus.Denied;
        }

        PreparedToolWorkspaceCheckStatus operation = PreparedToolWorkspaceCheckStatus.RootUnavailable;
        PreparedToolWorkspaceCheckStatus leaseStatus = await codeGraphLease.UseContainedRootAsync(
            PreparedToolPurpose.CodeGraphPreparation,
            async (root, token) =>
            {
                if (!RootMatches(root, workspaceOwnership))
                {
                    operation = PreparedToolWorkspaceCheckStatus.RootUnavailable;
                    return;
                }

                operation = await VerifyUseAuthorityAsync(workspaceOwnership, token);
                if (operation != PreparedToolWorkspaceCheckStatus.Current)
                {
                    return;
                }

                PreparedCodeGraphOwnershipLoadResult? loaded = await TryLoadAsync(workspaceOwnership, token);
                if (workspaceOwnership.Phase == PreparedCodeGraphOwnershipPhase.Absent &&
                    workspaceOwnership.Version == 0 && workspaceOwnership.StateEntryObjectIdentity is null &&
                    loaded?.Status == PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent)
                {
                    // A same-name source entry must never be adopted as owned state.
                    if (root.ObserveCodeGraphEntryIdentity() is not null)
                    {
                        operation = PreparedToolWorkspaceCheckStatus.Denied;
                        return;
                    }

                    PreparedCodeGraphOwnershipTransitionResult? begun = await TryTransitionAsync(
                        () => _store.BeginCreateAsync(workspaceOwnership, token));
                    if (begun?.Status != PreparedCodeGraphOwnershipTransitionStatus.Committed ||
                        begun.Record is not { Phase: PreparedCodeGraphOwnershipPhase.Creating } creating ||
                        !SameBinding(workspaceOwnership, creating) || creating.Version <= workspaceOwnership.Version)
                    {
                        operation = PreparedToolWorkspaceCheckStatus.RootUnavailable;
                        return;
                    }

                    if (!root.TryCreateCodeGraphEntryExclusively())
                    {
                        await TryQuarantineAsync(root, creating);
                        operation = PreparedToolWorkspaceCheckStatus.RootUnavailable;
                        return;
                    }

                    PreparedToolDirectoryObjectIdentity? entry = root.ObserveCodeGraphEntryIdentity();
                    operation = await VerifyUseAuthorityAsync(creating, token);
                    if (entry is null || !RootMatches(root, creating) ||
                        operation != PreparedToolWorkspaceCheckStatus.Current)
                    {
                        await TryQuarantineAsync(root, creating);
                        operation = PreparedToolWorkspaceCheckStatus.RootUnavailable;
                        return;
                    }

                    PreparedCodeGraphOwnershipTransitionResult? committed = await TryTransitionAsync(
                        () => _store.CommitCreatedAsync(creating, entry, token));
                    if (committed?.Status != PreparedCodeGraphOwnershipTransitionStatus.Committed ||
                        committed.Record is not { Phase: PreparedCodeGraphOwnershipPhase.Ready } ready ||
                        !SameBinding(creating, ready) || ready.Version <= creating.Version ||
                        ready.StateEntryObjectIdentity != entry)
                    {
                        await TryQuarantineAsync(root, creating);
                        operation = PreparedToolWorkspaceCheckStatus.RootUnavailable;
                        return;
                    }

                    workspaceOwnership = ready;
                }
                else if (loaded?.Status == PreparedCodeGraphOwnershipLoadStatus.Found &&
                    loaded.Record is { Phase: PreparedCodeGraphOwnershipPhase.Ready } current &&
                    SameBinding(workspaceOwnership, current) &&
                    workspaceOwnership.Version == current.Version &&
                    current.StateEntryObjectIdentity is not null &&
                    root.ObserveCodeGraphEntryIdentity() == current.StateEntryObjectIdentity)
                {
                    workspaceOwnership = current;
                }
                else
                {
                    operation = PreparedToolWorkspaceCheckStatus.RootUnavailable;
                    return;
                }

                operation = await VerifyUseAuthorityAsync(workspaceOwnership, token);
                if (operation != PreparedToolWorkspaceCheckStatus.Current ||
                    !RootMatches(root, workspaceOwnership) ||
                    root.ObserveCodeGraphEntryIdentity() != workspaceOwnership.StateEntryObjectIdentity)
                {
                    operation = PreparedToolWorkspaceCheckStatus.RootUnavailable;
                    return;
                }

                await use(new PreparedCodeGraphStateScope(root), token);
                operation = await VerifyUseAuthorityAsync(workspaceOwnership, token);
                if (operation != PreparedToolWorkspaceCheckStatus.Current ||
                    !RootMatches(root, workspaceOwnership) ||
                    root.ObserveCodeGraphEntryIdentity() != workspaceOwnership.StateEntryObjectIdentity)
                {
                    await TryQuarantineAsync(root, workspaceOwnership);
                    operation = PreparedToolWorkspaceCheckStatus.RootUnavailable;
                }
            }, cancellationToken);

        return leaseStatus == PreparedToolWorkspaceCheckStatus.Current ? operation : leaseStatus;
    }

    internal async Task<PreparedCodeGraphOwnershipViewResult> GetOwnershipViewAsync(
        PreparedCodeGraphWorkspaceOwnership workspaceBinding,
        PreparedSourceApproval currentApproval,
        CancellationToken cancellationToken = default)
    {
        if (!ValidBinding(workspaceBinding) || workspaceBinding.Approval != currentApproval ||
            await VerifySourceAndOwnerAsync(workspaceBinding, cancellationToken) !=
                PreparedToolWorkspaceCheckStatus.Current)
        {
            return UnavailableView();
        }

        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? root) =
            PreparedSourceTraversal.OpenRoot(workspaceBinding.PhysicalRootPath);
        using (root)
        {
            if (status != PreparedSourceTraversal.OpenStatus.Opened || root is null ||
                Stable(PreparedSourceTraversal.ObserveDirectory(root)) != workspaceBinding.RootObjectIdentity)
            {
                return UnavailableView();
            }

            PreparedCodeGraphOwnershipLoadResult? loaded = await TryLoadAsync(workspaceBinding, cancellationToken);
            (PreparedSourceTraversal.OpenStatus entryStatus, SafeFileHandle? entry) =
                PreparedSourceTraversal.OpenRelativeDirectory(root, ".codegraph");
            using (entry)
            {
                PreparedToolDirectoryObjectIdentity? entryIdentity = entry is null
                    ? null : Stable(PreparedSourceTraversal.ObserveDirectory(entry));
                if (loaded?.Status == PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent &&
                    workspaceBinding.Phase == PreparedCodeGraphOwnershipPhase.Absent &&
                    workspaceBinding.Version == 0 && workspaceBinding.StateEntryObjectIdentity is null &&
                    entryStatus is PreparedSourceTraversal.OpenStatus.Missing or PreparedSourceTraversal.OpenStatus.Opened)
                {
                    return new PreparedCodeGraphOwnershipViewResult(
                        PreparedCodeGraphOwnershipViewStatus.VerifiedUnowned, null);
                }

                if (loaded?.Status == PreparedCodeGraphOwnershipLoadStatus.Found &&
                    loaded.Record is { Phase: PreparedCodeGraphOwnershipPhase.Ready } ready &&
                    SameBinding(workspaceBinding, ready) && ready.StateEntryObjectIdentity is not null &&
                    entryStatus == PreparedSourceTraversal.OpenStatus.Opened &&
                    entryIdentity == ready.StateEntryObjectIdentity &&
                    Stable(PreparedSourceTraversal.ObserveDirectory(root)) == ready.RootObjectIdentity)
                {
                    return new PreparedCodeGraphOwnershipViewResult(
                        PreparedCodeGraphOwnershipViewStatus.VerifiedOwned, ready);
                }
            }
        }

        return UnavailableView();
    }

    internal async Task<PreparedToolWorkspaceCheckStatus> DisposeOwnedStateAsync(
        PreparedSourceApproval dispositionApproval,
        PreparedCodeGraphWorkspaceOwnership workspaceOwnership,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceOwnership);
        if (!ValidBinding(workspaceOwnership) ||
            workspaceOwnership.Phase != PreparedCodeGraphOwnershipPhase.Ready ||
            workspaceOwnership.StateEntryObjectIdentity is null ||
            dispositionApproval != workspaceOwnership.Approval)
        {
            return PreparedToolWorkspaceCheckStatus.Denied;
        }

        PreparedToolWorkspaceCheckStatus approval = await VerifyDispositionAuthorityAsync(
            workspaceOwnership, cancellationToken);
        if (approval != PreparedToolWorkspaceCheckStatus.Current)
        {
            return approval;
        }

        (PreparedSourceTraversal.OpenStatus openStatus, SafeFileHandle? rootHandle) =
            PreparedSourceTraversal.OpenRoot(workspaceOwnership.PhysicalRootPath);
        if (openStatus != PreparedSourceTraversal.OpenStatus.Opened || rootHandle is null)
        {
            return PreparedToolWorkspaceCheckStatus.RootUnavailable;
        }

        using (rootHandle)
        {
            PreparedSourceTraversal.DirectoryIdentity? observedRoot =
                PreparedSourceTraversal.ObserveDirectory(rootHandle);
            if (observedRoot is null || Stable(observedRoot) != workspaceOwnership.RootObjectIdentity)
            {
                return PreparedToolWorkspaceCheckStatus.RootUnavailable;
            }

            var scope = new PreparedToolRootScope(
                workspaceOwnership.PhysicalRootPath,
                rootHandle,
                observedRoot,
                workspaceOwnership.Approval.RepositoryId);
            try
            {
                if (scope.ObserveCodeGraphEntryIdentity() != workspaceOwnership.StateEntryObjectIdentity ||
                    !await DispositionCheckAsync(scope, workspaceOwnership, expectEntry: true, cancellationToken))
                {
                    return PreparedToolWorkspaceCheckStatus.RootUnavailable;
                }

                PreparedCodeGraphOwnershipTransitionResult? begun = await TryTransitionAsync(
                    () => _store.BeginDispositionAsync(workspaceOwnership, cancellationToken));
                if (begun?.Status != PreparedCodeGraphOwnershipTransitionStatus.Committed ||
                    begun.Record is not { Phase: PreparedCodeGraphOwnershipPhase.Disposing } disposing ||
                    !SameBinding(workspaceOwnership, disposing) ||
                    disposing.Version <= workspaceOwnership.Version ||
                    disposing.StateEntryObjectIdentity != workspaceOwnership.StateEntryObjectIdentity)
                {
                    return PreparedToolWorkspaceCheckStatus.RootUnavailable;
                }

                try
                {
                    bool removed = await scope.RemoveRecordedCodeGraphEntryAsync(
                        disposing.StateEntryObjectIdentity!,
                        token => DispositionCheckAsync(scope, disposing, expectEntry: true, token),
                        cancellationToken);
                    if (!removed ||
                        !await DispositionCheckAsync(scope, disposing, expectEntry: false, cancellationToken) ||
                        !scope.IsCodeGraphEntryAbsent())
                    {
                        await TryQuarantineDispositionAsync(disposing);
                        return PreparedToolWorkspaceCheckStatus.RootUnavailable;
                    }

                    PreparedCodeGraphOwnershipTransitionResult? terminal = await TryTransitionAsync(
                        () => _store.CommitRemovedAsync(disposing, cancellationToken));
                    if (terminal?.Status != PreparedCodeGraphOwnershipTransitionStatus.Committed ||
                        terminal.Record is not { Phase: PreparedCodeGraphOwnershipPhase.Removed } saved ||
                        !SameBinding(disposing, saved) || saved.Version <= disposing.Version ||
                        saved.StateEntryObjectIdentity is not null)
                    {
                        await TryQuarantineDispositionAsync(disposing);
                        return PreparedToolWorkspaceCheckStatus.RootUnavailable;
                    }

                    PreparedCodeGraphOwnershipLoadResult? readback = await TryLoadAsync(saved, cancellationToken);
                    if (readback?.Status != PreparedCodeGraphOwnershipLoadStatus.Found ||
                        readback.Record != saved || !scope.IsCodeGraphEntryAbsent() ||
                        !RootMatches(scope, saved))
                    {
                        await TryQuarantineDispositionAsync(saved);
                        return PreparedToolWorkspaceCheckStatus.RootUnavailable;
                    }

                    return PreparedToolWorkspaceCheckStatus.Current;
                }
                catch (OperationCanceledException)
                {
                    await TryQuarantineDispositionAsync(disposing);
                    return PreparedToolWorkspaceCheckStatus.Cancelled;
                }
                catch (Exception)
                {
                    await TryQuarantineDispositionAsync(disposing);
                    return PreparedToolWorkspaceCheckStatus.RootUnavailable;
                }
            }
            finally
            {
                scope.Invalidate();
            }
        }
    }

    private async Task<bool> DispositionCheckAsync(
        PreparedToolRootScope scope,
        PreparedCodeGraphWorkspaceOwnership ownership,
        bool expectEntry,
        CancellationToken cancellationToken)
    {
        if (await VerifyDispositionAuthorityAsync(ownership, cancellationToken) !=
                PreparedToolWorkspaceCheckStatus.Current || !RootMatches(scope, ownership) ||
            expectEntry && scope.ObserveCodeGraphEntryIdentity() != ownership.StateEntryObjectIdentity)
        {
            return false;
        }

        PreparedCodeGraphOwnershipLoadResult? loaded = await TryLoadAsync(ownership, cancellationToken);
        return loaded?.Status == PreparedCodeGraphOwnershipLoadStatus.Found &&
            loaded.Record == ownership;
    }

    private async Task<PreparedToolWorkspaceCheckStatus> VerifyDispositionAuthorityAsync(
        PreparedCodeGraphWorkspaceOwnership ownership,
        CancellationToken cancellationToken)
    {
        PreparedToolWorkspaceCheckStatus use = await VerifyUseAuthorityAsync(ownership, cancellationToken);
        if (use != PreparedToolWorkspaceCheckStatus.Current)
        {
            return use;
        }

        try
        {
            return await _workspaceOwner.VerifyDispositionReadyAsync(ownership, cancellationToken)
                ? PreparedToolWorkspaceCheckStatus.Current
                : PreparedToolWorkspaceCheckStatus.Denied;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PreparedToolWorkspaceCheckStatus.Cancelled;
        }
        catch (Exception)
        {
            return PreparedToolWorkspaceCheckStatus.AuthorityUnavailable;
        }
    }

    private async Task TryQuarantineDispositionAsync(PreparedCodeGraphWorkspaceOwnership expected)
    {
        PreparedCodeGraphOwnershipLoadResult? loaded = await TryLoadAsync(expected, CancellationToken.None);
        if (loaded?.Status == PreparedCodeGraphOwnershipLoadStatus.Found &&
            loaded.Record is { } current && SameBinding(expected, current) &&
            current.Version >= expected.Version &&
            current.Phase is PreparedCodeGraphOwnershipPhase.Disposing or PreparedCodeGraphOwnershipPhase.Removed)
        {
            await TryTransitionAsync(() => _store.QuarantineAsync(current, CancellationToken.None));
        }
    }

    private async Task<PreparedToolWorkspaceCheckStatus> VerifyUseAuthorityAsync(
        PreparedCodeGraphWorkspaceOwnership ownership,
        CancellationToken cancellationToken)
    {
        PreparedToolWorkspaceCheckStatus source = await VerifySourceAndOwnerAsync(ownership, cancellationToken);
        if (source != PreparedToolWorkspaceCheckStatus.Current)
        {
            return source;
        }

        try
        {
            PreparedToolUseAuthorityResult tool = await _toolAuthorization.VerifyCurrentAsync(
                ownership.Approval, PreparedToolPurpose.CodeGraphPreparation, cancellationToken);
            return tool.Status switch
            {
                PreparedToolUseAuthorityStatus.Current => PreparedToolWorkspaceCheckStatus.Current,
                PreparedToolUseAuthorityStatus.Denied => PreparedToolWorkspaceCheckStatus.Denied,
                _ => PreparedToolWorkspaceCheckStatus.AuthorityUnavailable,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PreparedToolWorkspaceCheckStatus.Cancelled;
        }
        catch (Exception)
        {
            return PreparedToolWorkspaceCheckStatus.AuthorityUnavailable;
        }
    }

    private async Task<PreparedToolWorkspaceCheckStatus> VerifySourceAndOwnerAsync(
        PreparedCodeGraphWorkspaceOwnership ownership,
        CancellationToken cancellationToken)
    {
        try
        {
            PreparedSourceAuthorityResult source = await _sourceAuthority.VerifyCurrentAsync(
                ownership.Approval, cancellationToken);
            if (source.Status == PreparedSourceAuthorityStatus.Unavailable)
            {
                return PreparedToolWorkspaceCheckStatus.AuthorityUnavailable;
            }

            if (source.Status != PreparedSourceAuthorityStatus.Current ||
                source.CurrentApproval != ownership.Approval ||
                !await _workspaceOwner.VerifyCurrentAsync(ownership, cancellationToken))
            {
                return PreparedToolWorkspaceCheckStatus.Denied;
            }

            return PreparedToolWorkspaceCheckStatus.Current;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PreparedToolWorkspaceCheckStatus.Cancelled;
        }
        catch (Exception)
        {
            return PreparedToolWorkspaceCheckStatus.AuthorityUnavailable;
        }
    }

    private async Task<PreparedCodeGraphOwnershipLoadResult?> TryLoadAsync(
        PreparedCodeGraphWorkspaceOwnership ownership,
        CancellationToken token)
    {
        try { return await _store.LoadAsync(ownership, token); }
        catch (Exception) { return null; }
    }

    private static async Task<PreparedCodeGraphOwnershipTransitionResult?> TryTransitionAsync(
        Func<Task<PreparedCodeGraphOwnershipTransitionResult>> transition)
    {
        try { return await transition(); }
        catch (Exception) { return null; }
    }

    private async Task<bool> TryQuarantineAsync(
        PreparedToolRootScope root,
        PreparedCodeGraphWorkspaceOwnership expected)
    {
        // The transition that prompted quarantine may have persisted even when
        // its acknowledgement was lost. Compare against the freshly loaded
        // version, never the older Creating/Ready copy held by this call.
        if (!RootMatches(root, expected) ||
            await VerifyUseAuthorityAsync(expected, CancellationToken.None) !=
                PreparedToolWorkspaceCheckStatus.Current)
        {
            return false;
        }

        PreparedCodeGraphOwnershipLoadResult? loaded = await TryLoadAsync(expected, CancellationToken.None);
        if (loaded?.Status != PreparedCodeGraphOwnershipLoadStatus.Found ||
            loaded.Record is not { } current || !SameBinding(expected, current) ||
            current.Version < expected.Version ||
            current.Phase is not (PreparedCodeGraphOwnershipPhase.Creating or PreparedCodeGraphOwnershipPhase.Ready) ||
            current.Phase == PreparedCodeGraphOwnershipPhase.Ready &&
                (current.StateEntryObjectIdentity is null ||
                 root.ObserveCodeGraphEntryIdentity() != current.StateEntryObjectIdentity))
        {
            return false;
        }

        PreparedCodeGraphOwnershipTransitionResult? quarantine = await TryTransitionAsync(
            () => _store.QuarantineAsync(current, CancellationToken.None));
        if (quarantine?.Status != PreparedCodeGraphOwnershipTransitionStatus.Committed ||
            quarantine.Record is not { Phase: PreparedCodeGraphOwnershipPhase.Quarantined } saved ||
            !SameBinding(current, saved) || saved.Version <= current.Version)
        {
            return false;
        }

        PreparedCodeGraphOwnershipLoadResult? readback = await TryLoadAsync(saved, CancellationToken.None);
        return readback?.Status == PreparedCodeGraphOwnershipLoadStatus.Found &&
            readback.Record == saved && RootMatches(root, expected);
    }

    private static bool ValidBinding(PreparedCodeGraphWorkspaceOwnership binding) =>
        binding.OwnershipId != Guid.Empty && binding.Approval.RepositoryId != Guid.Empty &&
        binding.RootObjectIdentity is not null &&
        !string.IsNullOrWhiteSpace(binding.WorkspaceOwnerIdentity) &&
        Path.IsPathFullyQualified(binding.PhysicalRootPath);

    private static bool SameBinding(
        PreparedCodeGraphWorkspaceOwnership expected,
        PreparedCodeGraphWorkspaceOwnership observed) =>
        expected.OwnershipId == observed.OwnershipId &&
        expected.Approval == observed.Approval &&
        string.Equals(expected.PhysicalRootPath, observed.PhysicalRootPath, StringComparison.OrdinalIgnoreCase) &&
        expected.RootObjectIdentity == observed.RootObjectIdentity &&
        expected.WorkspaceOwnerIdentity == observed.WorkspaceOwnerIdentity;

    private static bool RootMatches(
        PreparedToolRootScope root,
        PreparedCodeGraphWorkspaceOwnership binding) =>
        string.Equals(root.GetPhysicalRootPathForTrustedToolBinding(), binding.PhysicalRootPath,
            StringComparison.OrdinalIgnoreCase) &&
        root.ObserveRootObjectIdentity() == binding.RootObjectIdentity;

    private static PreparedToolDirectoryObjectIdentity? Stable(
        PreparedSourceTraversal.DirectoryIdentity? identity) =>
        identity is null ? null : new PreparedToolDirectoryObjectIdentity(
            identity.VolumeSerialNumber, identity.FileIndexHigh, identity.FileIndexLow);

    private static PreparedCodeGraphOwnershipViewResult UnavailableView() =>
        new(PreparedCodeGraphOwnershipViewStatus.Unavailable, null);
}

using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

internal enum PreparedManagedWorkspaceHostStatus
{
    Observed,
    Protected,
    Denied,
    Unavailable,
}

internal sealed record PreparedManagedWorkspaceHostObservation(
    PreparedManagedWorkspaceHostStatus Status,
    PreparedToolDirectoryObjectIdentity? RootObjectIdentity,
    string? DerivedWorkspaceOwnerIdentity);

internal sealed record PreparedManagedWorkspaceHostHoldResult(
    PreparedManagedWorkspaceHostStatus Status,
    IPreparedManagedWorkspaceHostHold? Hold);

internal interface IPreparedManagedWorkspaceHostHold : IAsyncDisposable
{
    ValueTask<PreparedManagedWorkspaceHostStatus> CheckCurrentAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        CancellationToken cancellationToken = default);
}

internal interface IPreparedManagedWorkspaceHostVerifier
{
    Task<PreparedManagedWorkspaceHostObservation> ObserveAsync(
        PreparedSourceGrantRequest applicationHeldSourceGrant,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default);

    Task<PreparedManagedWorkspaceHostHoldResult> VerifyAndHoldAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        CancellationToken cancellationToken = default);
}

internal enum PreparedWorkspaceSettlementStatus
{
    Settled,
    Active,
    Unknown,
}

internal interface IPreparedWorkspaceResourceSettlementProbe
{
    ValueTask<PreparedWorkspaceSettlementStatus> InspectOwnedResourcesAsync(
        CancellationToken cancellationToken = default);
}

internal interface IPreparedWorkspacePreviousGenerationVerifier
{
    ValueTask<PreparedWorkspaceSettlementStatus> VerifySettledAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        CancellationToken cancellationToken = default);
}

internal enum PreparedWorkspaceAdmissionStatus
{
    Opened,
    Denied,
    Unavailable,
    Cancelled,
}

internal sealed record PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus Status);

internal sealed record PreparedWorkspaceBorrowResult(
    PreparedWorkspaceSettlementStatus Status,
    PreparedWorkspaceBorrow? Borrow);

internal sealed class PreparedWorkspaceBorrow : IAsyncDisposable
{
    private readonly PreparedWorkspaceUseRegistry _registry;
    private readonly IPreparedWorkspaceResourceSettlementProbe _resources;
    private Task<PreparedWorkspaceSettlementStatus>? _inspection;

    internal PreparedWorkspaceBorrow(
        PreparedWorkspaceUseRegistry registry,
        IPreparedWorkspaceResourceSettlementProbe resources)
    {
        _registry = registry;
        _resources = resources;
    }

    internal async ValueTask<PreparedWorkspaceSettlementStatus> CheckSettlementAsync(
        CancellationToken cancellationToken)
    {
        Task<PreparedWorkspaceSettlementStatus> inspection = _inspection ??=
            _resources.InspectOwnedResourcesAsync(cancellationToken).AsTask();
        try
        {
            return await inspection.WaitAsync(cancellationToken);
        }
        finally
        {
            if (inspection.IsCompleted)
            {
                _inspection = null;
            }
        }
    }

    public ValueTask DisposeAsync() => _registry.CompleteBorrowAsync(this);
}

internal sealed class PreparedWorkspaceUseRegistry : IAsyncDisposable
{
    private readonly IPreparedSourceCurrentAuthorityVerifier _sourceAuthority;
    private readonly IPreparedToolUseAuthorizer _toolAuthorization;
    private readonly IPreparedCodeGraphOwnershipStore _ownershipStore;
    private readonly RetainedSourceCapturePolicy _sourceStoragePolicy;
    private readonly IPreparedManagedWorkspaceHostVerifier _hostProtection;
    private readonly IPreparedWorkspacePreviousGenerationVerifier _previousGeneration;
    private readonly TimeSpan _admissionDeadline;
    private readonly TimeSpan _dispositionDeadline;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _publicationGate = new();
    private readonly HashSet<PreparedWorkspaceBorrow> _borrows = [];
    private readonly HashSet<Task> _pendingProofChecks = [];
    private readonly HashSet<Task> _pendingHostAcquisitions = [];
    private readonly HashSet<Task> _pendingHoldDisposals = [];
    private IPreparedManagedWorkspaceHostHold? _hold;
    private PreparedCodeGraphWorkspaceOwnership? _admittedBinding;
    private RegistryState _state = RegistryState.Closed;
    private int _shutdownRequested;
    private Task? _deferredShutdownCleanup;

    private enum RegistryState { Closed, Open, Closing, Unknown, Disposed }

    private sealed class HeldCheckOwnershipTransferredException : OperationCanceledException
    {
        internal HeldCheckOwnershipTransferredException(CancellationToken cancellationToken)
            : base(cancellationToken)
        {
        }
    }

    internal PreparedWorkspaceUseRegistry(
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization,
        IPreparedCodeGraphOwnershipStore ownershipStore,
        RetainedSourceCapturePolicy sourceStoragePolicy,
        IPreparedManagedWorkspaceHostVerifier hostProtection,
        IPreparedWorkspacePreviousGenerationVerifier previousGeneration,
        TimeSpan admissionDeadline,
        TimeSpan dispositionDeadline)
    {
        _sourceAuthority = sourceAuthority ?? throw new ArgumentNullException(nameof(sourceAuthority));
        _toolAuthorization = toolAuthorization ?? throw new ArgumentNullException(nameof(toolAuthorization));
        _ownershipStore = ownershipStore ?? throw new ArgumentNullException(nameof(ownershipStore));
        _sourceStoragePolicy = sourceStoragePolicy ?? throw new ArgumentNullException(nameof(sourceStoragePolicy));
        _hostProtection = hostProtection ?? throw new ArgumentNullException(nameof(hostProtection));
        _previousGeneration = previousGeneration ?? throw new ArgumentNullException(nameof(previousGeneration));
        _admissionDeadline = admissionDeadline > TimeSpan.Zero
            ? admissionDeadline : throw new ArgumentOutOfRangeException(nameof(admissionDeadline));
        _dispositionDeadline = dispositionDeadline > TimeSpan.Zero
            ? dispositionDeadline : throw new ArgumentOutOfRangeException(nameof(dispositionDeadline));
    }

    internal async Task<PreparedWorkspaceAdmissionResult> AdmitAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        PreparedToolPurpose selectedPurpose,
        CancellationToken cancellationToken = default)
    {
        if (binding is null || cancellationToken.IsCancellationRequested ||
            Volatile.Read(ref _shutdownRequested) != 0 || HasPendingChecks())
        {
            return new PreparedWorkspaceAdmissionResult(cancellationToken.IsCancellationRequested
                ? PreparedWorkspaceAdmissionStatus.Cancelled
                : PreparedWorkspaceAdmissionStatus.Unavailable);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_admissionDeadline);
        IPreparedManagedWorkspaceHostHold? pendingHold = null;
        try
        {
            await _gate.WaitAsync(deadline.Token);
            try
            {
                if (_state != RegistryState.Closed || Volatile.Read(ref _shutdownRequested) != 0 ||
                    HasPendingChecks() ||
                    binding.Phase is not (PreparedCodeGraphOwnershipPhase.Absent or PreparedCodeGraphOwnershipPhase.Ready))
                {
                    return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                PreparedWorkspaceAdmissionStatus authority = await CheckAuthorityAsync(
                    binding, selectedPurpose, deadline.Token);
                if (authority != PreparedWorkspaceAdmissionStatus.Opened)
                {
                    return new PreparedWorkspaceAdmissionResult(authority);
                }

                if (!await CheckStoreAsync(binding, deadline.Token))
                {
                    return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                if (binding.Phase == PreparedCodeGraphOwnershipPhase.Ready &&
                    await AwaitProofAsync(
                        _previousGeneration.VerifySettledAsync(binding, deadline.Token).AsTask(),
                        deadline.Token)
                        != PreparedWorkspaceSettlementStatus.Settled)
                {
                    return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                Task<PreparedManagedWorkspaceHostHoldResult> acquisition =
                    _hostProtection.VerifyAndHoldAsync(binding, _sourceStoragePolicy, deadline.Token);
                PreparedManagedWorkspaceHostHoldResult host;
                try
                {
                    host = await acquisition.WaitAsync(deadline.Token);
                }
                catch (OperationCanceledException)
                {
                    TrackLateHostAcquisition(acquisition);
                    throw;
                }
                pendingHold = host.Hold;
                if (host.Status != PreparedManagedWorkspaceHostStatus.Protected || pendingHold is null)
                {
                    return new PreparedWorkspaceAdmissionResult(host.Status == PreparedManagedWorkspaceHostStatus.Denied
                        ? PreparedWorkspaceAdmissionStatus.Denied
                        : PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                PreparedManagedWorkspaceHostStatus heldStatus;
                try
                {
                    heldStatus = await CheckHeldCurrentAsync(pendingHold, binding, deadline.Token);
                }
                catch (HeldCheckOwnershipTransferredException)
                {
                    pendingHold = null;
                    throw;
                }

                if (heldStatus != PreparedManagedWorkspaceHostStatus.Protected)
                {
                    return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                authority = await CheckAuthorityAsync(binding, selectedPurpose, deadline.Token);
                if (authority != PreparedWorkspaceAdmissionStatus.Opened ||
                    !await CheckStoreAsync(binding, deadline.Token))
                {
                    return new PreparedWorkspaceAdmissionResult(authority == PreparedWorkspaceAdmissionStatus.Denied
                        ? PreparedWorkspaceAdmissionStatus.Denied
                        : PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                try
                {
                    heldStatus = await CheckHeldCurrentAsync(pendingHold, binding, deadline.Token);
                }
                catch (HeldCheckOwnershipTransferredException)
                {
                    pendingHold = null;
                    throw;
                }

                if (heldStatus != PreparedManagedWorkspaceHostStatus.Protected)
                {
                    return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                deadline.Token.ThrowIfCancellationRequested();
                lock (_publicationGate)
                {
                    if (_shutdownRequested != 0)
                    {
                        return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Unavailable);
                    }

                    _hold = pendingHold;
                    pendingHold = null;
                    _admittedBinding = binding;
                    _state = RegistryState.Open;
                }

                return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Opened);
            }
            finally
            {
                try
                {
                    if (pendingHold is not null)
                    {
                        IPreparedManagedWorkspaceHostHold holdToDispose = pendingHold;
                        pendingHold = null;
                        Task disposal = holdToDispose.DisposeAsync().AsTask();
                        try
                        {
                            await disposal.WaitAsync(deadline.Token);
                        }
                        catch
                        {
                            if (!disposal.IsCompleted)
                            {
                                TrackPendingHoldDisposal(disposal);
                            }

                            throw;
                        }
                    }
                }
                catch
                {
                    _state = RegistryState.Unknown;
                    throw new InvalidOperationException("Partial host hold cleanup is unconfirmed.");
                }
                finally
                {
                    _gate.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            return new PreparedWorkspaceAdmissionResult(cancellationToken.IsCancellationRequested
                ? PreparedWorkspaceAdmissionStatus.Cancelled
                : PreparedWorkspaceAdmissionStatus.Unavailable);
        }
        catch
        {
            return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Unavailable);
        }
    }

    private async Task<PreparedWorkspaceAdmissionStatus> CheckAuthorityAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        PreparedToolPurpose purpose,
        CancellationToken cancellationToken)
    {
        PreparedSourceAuthorityResult source = await AwaitProofAsync(
            _sourceAuthority.VerifyCurrentAsync(binding.Approval, cancellationToken),
            cancellationToken);
        if (source.Status != PreparedSourceAuthorityStatus.Current ||
            source.CurrentApproval != binding.Approval)
        {
            return source.Status == PreparedSourceAuthorityStatus.Revoked
                ? PreparedWorkspaceAdmissionStatus.Denied
                : PreparedWorkspaceAdmissionStatus.Unavailable;
        }

        PreparedToolUseAuthorityResult tool = await AwaitProofAsync(
            _toolAuthorization.VerifyCurrentAsync(binding.Approval, purpose, cancellationToken),
            cancellationToken);
        return tool.Status switch
        {
            PreparedToolUseAuthorityStatus.Current => PreparedWorkspaceAdmissionStatus.Opened,
            PreparedToolUseAuthorityStatus.Denied => PreparedWorkspaceAdmissionStatus.Denied,
            _ => PreparedWorkspaceAdmissionStatus.Unavailable,
        };
    }

    private async Task<T> AwaitProofAsync<T>(Task<T> verification, CancellationToken cancellationToken)
    {
        try
        {
            return await verification.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TrackLateProofCheck(verification);
            throw;
        }
    }

    private void TrackLateProofCheck(Task verification)
    {
        lock (_publicationGate)
        {
            _pendingProofChecks.Add(verification);
        }

        _ = ObserveLateProofCheckAsync(verification);
    }

    private bool HasPendingChecks()
    {
        lock (_publicationGate)
        {
            return _pendingProofChecks.Count != 0 || _pendingHostAcquisitions.Count != 0 ||
                _pendingHoldDisposals.Count != 0;
        }
    }

    private void TrackPendingHoldDisposal(Task disposal)
    {
        lock (_publicationGate)
        {
            _pendingHoldDisposals.Add(disposal);
        }

        _ = ObservePendingHoldDisposalAsync(disposal);
    }

    private async Task ObservePendingHoldDisposalAsync(Task disposal)
    {
        try
        {
            await disposal;
        }
        catch
        {
            _state = RegistryState.Unknown;
        }
        finally
        {
            lock (_publicationGate)
            {
                _pendingHoldDisposals.Remove(disposal);
            }
        }
    }

    private void TrackLateHostAcquisition(Task<PreparedManagedWorkspaceHostHoldResult> acquisition)
    {
        lock (_publicationGate)
        {
            _pendingHostAcquisitions.Add(acquisition);
        }

        _ = DisposeLateHostAcquisitionAsync(acquisition);
    }

    private async Task<PreparedManagedWorkspaceHostStatus> CheckHeldCurrentAsync(
        IPreparedManagedWorkspaceHostHold hold,
        PreparedCodeGraphWorkspaceOwnership binding,
        CancellationToken cancellationToken)
    {
        Task<PreparedManagedWorkspaceHostStatus> check =
            hold.CheckCurrentAsync(binding, cancellationToken).AsTask();
        try
        {
            return await check.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            lock (_publicationGate)
            {
                _pendingProofChecks.Add(check);
            }

            _ = DisposeAfterLateHeldCheckAsync(check, hold);
            throw new HeldCheckOwnershipTransferredException(cancellationToken);
        }
    }

    private async Task DisposeAfterLateHeldCheckAsync(
        Task<PreparedManagedWorkspaceHostStatus> check,
        IPreparedManagedWorkspaceHostHold hold)
    {
        try
        {
            await check;
        }
        catch
        {
            // The result is no longer authority after the admission deadline.
        }

        try
        {
            await hold.DisposeAsync();
        }
        catch
        {
            lock (_publicationGate)
            {
                Volatile.Write(ref _shutdownRequested, 1);
                _state = RegistryState.Unknown;
            }
        }
        finally
        {
            lock (_publicationGate)
            {
                _pendingProofChecks.Remove(check);
            }
        }
    }

    private async Task DisposeLateHostAcquisitionAsync(
        Task<PreparedManagedWorkspaceHostHoldResult> acquisition)
    {
        try
        {
            PreparedManagedWorkspaceHostHoldResult result = await acquisition;
            if (result.Hold is not null)
            {
                await result.Hold.DisposeAsync();
            }
        }
        catch
        {
            lock (_publicationGate)
            {
                Volatile.Write(ref _shutdownRequested, 1);
                _state = RegistryState.Unknown;
            }
        }
        finally
        {
            lock (_publicationGate)
            {
                _pendingHostAcquisitions.Remove(acquisition);
            }
        }
    }

    private async Task ObserveLateProofCheckAsync(Task verification)
    {
        try
        {
            await verification;
        }
        catch
        {
            // A late proof cannot authorize a timed-out operation.
        }
        finally
        {
            lock (_publicationGate)
            {
                _pendingProofChecks.Remove(verification);
            }
        }
    }

    private async Task<bool> CheckStoreAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        CancellationToken cancellationToken)
    {
        PreparedCodeGraphOwnershipLoadResult loaded = await AwaitProofAsync(
            _ownershipStore.LoadAsync(binding, cancellationToken), cancellationToken);
        return binding.Phase switch
        {
            PreparedCodeGraphOwnershipPhase.Absent =>
                loaded.Status == PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent && loaded.Record is null,
            PreparedCodeGraphOwnershipPhase.Ready =>
                loaded.Status == PreparedCodeGraphOwnershipLoadStatus.Found && loaded.Record == binding,
            _ => false,
        };
    }

    internal async Task<PreparedWorkspaceBorrowResult> BeginBorrowAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        PreparedToolPurpose purpose,
        IPreparedWorkspaceResourceSettlementProbe resources,
        CancellationToken cancellationToken = default)
    {
        if (binding is null || resources is null)
        {
            return new PreparedWorkspaceBorrowResult(PreparedWorkspaceSettlementStatus.Unknown, null);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_admissionDeadline);
        try
        {
            await _gate.WaitAsync(deadline.Token);
            try
            {
                if (_state != RegistryState.Open || Volatile.Read(ref _shutdownRequested) != 0 ||
                    binding != _admittedBinding ||
                    _hold is null ||
                    purpose is not (PreparedToolPurpose.JdtNavigation or PreparedToolPurpose.CodeGraphPreparation) ||
                    purpose == PreparedToolPurpose.CodeGraphPreparation &&
                    binding.Phase != PreparedCodeGraphOwnershipPhase.Ready)
                {
                    return new PreparedWorkspaceBorrowResult(PreparedWorkspaceSettlementStatus.Unknown, null);
                }

                if (await CheckAuthorityAsync(binding, purpose, deadline.Token)
                        != PreparedWorkspaceAdmissionStatus.Opened ||
                    !await CheckStoreAsync(binding, deadline.Token))
                {
                    _state = RegistryState.Unknown;
                    return new PreparedWorkspaceBorrowResult(PreparedWorkspaceSettlementStatus.Unknown, null);
                }

                PreparedManagedWorkspaceHostStatus heldStatus;
                try
                {
                    heldStatus = await AwaitProofAsync(
                        _hold.CheckCurrentAsync(binding, deadline.Token).AsTask(), deadline.Token);
                }
                catch
                {
                    _state = RegistryState.Unknown;
                    throw;
                }

                if (heldStatus != PreparedManagedWorkspaceHostStatus.Protected)
                {
                    _state = RegistryState.Unknown;
                    return new PreparedWorkspaceBorrowResult(PreparedWorkspaceSettlementStatus.Unknown, null);
                }

                deadline.Token.ThrowIfCancellationRequested();
                lock (_publicationGate)
                {
                    if (_shutdownRequested != 0)
                    {
                        return new PreparedWorkspaceBorrowResult(PreparedWorkspaceSettlementStatus.Unknown, null);
                    }

                    var borrow = new PreparedWorkspaceBorrow(this, resources);
                    _borrows.Add(borrow);
                    return new PreparedWorkspaceBorrowResult(PreparedWorkspaceSettlementStatus.Active, borrow);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return new PreparedWorkspaceBorrowResult(PreparedWorkspaceSettlementStatus.Unknown, null);
        }
        catch
        {
            _state = RegistryState.Unknown;
            return new PreparedWorkspaceBorrowResult(PreparedWorkspaceSettlementStatus.Unknown, null);
        }
    }

    internal async Task<PreparedWorkspaceSettlementStatus> CloseForDispositionAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        CancellationToken cancellationToken = default)
    {
        if (binding is null)
        {
            return PreparedWorkspaceSettlementStatus.Unknown;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_dispositionDeadline);
        try
        {
            await _gate.WaitAsync(deadline.Token);
            try
            {
                if (_state == RegistryState.Open)
                {
                    _state = RegistryState.Closing;
                }

                if (_state != RegistryState.Closing || binding != _admittedBinding || _hold is null ||
                    await CheckAuthorityAsync(binding, PreparedToolPurpose.CodeGraphPreparation, deadline.Token)
                        != PreparedWorkspaceAdmissionStatus.Opened ||
                    !await CheckStoreAsync(binding, deadline.Token) ||
                    await AwaitProofAsync(
                        _hold.CheckCurrentAsync(binding, deadline.Token).AsTask(), deadline.Token)
                        != PreparedManagedWorkspaceHostStatus.Protected)
                {
                    _state = RegistryState.Unknown;
                    return PreparedWorkspaceSettlementStatus.Unknown;
                }

                foreach (PreparedWorkspaceBorrow borrow in _borrows)
                {
                    PreparedWorkspaceSettlementStatus status = await borrow.CheckSettlementAsync(deadline.Token);
                    if (status != PreparedWorkspaceSettlementStatus.Settled)
                    {
                        return status;
                    }
                }

                deadline.Token.ThrowIfCancellationRequested();
                return _borrows.Count == 0
                    ? PreparedWorkspaceSettlementStatus.Settled
                    : PreparedWorkspaceSettlementStatus.Active;
            }
            catch
            {
                _state = RegistryState.Unknown;
                return PreparedWorkspaceSettlementStatus.Unknown;
            }
            finally
            {
                _gate.Release();
            }
        }
        catch
        {
            return PreparedWorkspaceSettlementStatus.Unknown;
        }
    }

    internal async ValueTask CompleteBorrowAsync(PreparedWorkspaceBorrow borrow)
    {
        using var deadline = new CancellationTokenSource(_dispositionDeadline);
        await _gate.WaitAsync();
        try
        {
            if (!_borrows.Contains(borrow))
            {
                return;
            }

            try
            {
                if (await borrow.CheckSettlementAsync(deadline.Token)
                    == PreparedWorkspaceSettlementStatus.Settled)
                {
                    _borrows.Remove(borrow);
                }
                else
                {
                    _state = RegistryState.Unknown;
                }
            }
            catch
            {
                _state = RegistryState.Unknown;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        using var deadline = new CancellationTokenSource(_dispositionDeadline);
        lock (_publicationGate)
        {
            Volatile.Write(ref _shutdownRequested, 1);
        }

        try
        {
            await _gate.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            // Keep ownership of a stalled operation's eventual host-hold cleanup.
            lock (_publicationGate)
            {
                _deferredShutdownCleanup ??= CompleteDeferredShutdownAsync();
            }
            return;
        }

        try
        {
            if (_state == RegistryState.Disposed)
            {
                return;
            }

            _state = RegistryState.Closing;
            bool allSettled = true;
            foreach (PreparedWorkspaceBorrow borrow in _borrows)
            {
                try
                {
                    if (await borrow.CheckSettlementAsync(deadline.Token)
                        != PreparedWorkspaceSettlementStatus.Settled)
                    {
                        allSettled = false;
                    }
                }
                catch
                {
                    allSettled = false;
                    break;
                }
            }

            _state = allSettled ? RegistryState.Disposed : RegistryState.Unknown;
            if (_hold is not null)
            {
                Task disposal = _hold.DisposeAsync().AsTask();
                _hold = null;
                try
                {
                    await disposal.WaitAsync(deadline.Token);
                }
                catch
                {
                    _state = RegistryState.Unknown;
                    if (!disposal.IsCompleted)
                    {
                        TrackPendingHoldDisposal(disposal);
                    }
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task CompleteDeferredShutdownAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _state = RegistryState.Unknown;
            if (_hold is not null)
            {
                try
                {
                    await _hold.DisposeAsync();
                    _hold = null;
                }
                catch
                {
                    // Keep Unknown: an unsuccessful hold release is not settlement.
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}

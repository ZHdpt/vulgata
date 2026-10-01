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
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
    private IPreparedManagedWorkspaceHostHold? _hold;
    private RegistryState _state = RegistryState.Closed;

    private enum RegistryState { Closed, Open, Unknown, Disposed }

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
        if (binding is null || cancellationToken.IsCancellationRequested)
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
                if (_state != RegistryState.Closed ||
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
                    await _previousGeneration.VerifySettledAsync(binding, deadline.Token)
                        != PreparedWorkspaceSettlementStatus.Settled)
                {
                    return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                PreparedManagedWorkspaceHostHoldResult host = await _hostProtection.VerifyAndHoldAsync(
                    binding, _sourceStoragePolicy, deadline.Token);
                pendingHold = host.Hold;
                if (host.Status != PreparedManagedWorkspaceHostStatus.Protected || pendingHold is null)
                {
                    return new PreparedWorkspaceAdmissionResult(host.Status == PreparedManagedWorkspaceHostStatus.Denied
                        ? PreparedWorkspaceAdmissionStatus.Denied
                        : PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                if (await pendingHold.CheckCurrentAsync(binding, deadline.Token)
                    != PreparedManagedWorkspaceHostStatus.Protected)
                {
                    return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                authority = await CheckAuthorityAsync(binding, selectedPurpose, deadline.Token);
                if (authority != PreparedWorkspaceAdmissionStatus.Opened ||
                    !await CheckStoreAsync(binding, deadline.Token) ||
                    await pendingHold.CheckCurrentAsync(binding, deadline.Token)
                        != PreparedManagedWorkspaceHostStatus.Protected)
                {
                    return new PreparedWorkspaceAdmissionResult(authority == PreparedWorkspaceAdmissionStatus.Denied
                        ? PreparedWorkspaceAdmissionStatus.Denied
                        : PreparedWorkspaceAdmissionStatus.Unavailable);
                }

                deadline.Token.ThrowIfCancellationRequested();
                _hold = pendingHold;
                pendingHold = null;
                _state = RegistryState.Open;
                return new PreparedWorkspaceAdmissionResult(PreparedWorkspaceAdmissionStatus.Opened);
            }
            finally
            {
                try
                {
                    if (pendingHold is not null)
                    {
                        await pendingHold.DisposeAsync();
                    }
                }
                catch
                {
                    _state = RegistryState.Unknown;
                    throw;
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
        PreparedSourceAuthorityResult source = await _sourceAuthority.VerifyCurrentAsync(
            binding.Approval, cancellationToken);
        if (source.Status != PreparedSourceAuthorityStatus.Current ||
            source.CurrentApproval != binding.Approval)
        {
            return source.Status == PreparedSourceAuthorityStatus.Revoked
                ? PreparedWorkspaceAdmissionStatus.Denied
                : PreparedWorkspaceAdmissionStatus.Unavailable;
        }

        PreparedToolUseAuthorityResult tool = await _toolAuthorization.VerifyCurrentAsync(
            binding.Approval, purpose, cancellationToken);
        return tool.Status switch
        {
            PreparedToolUseAuthorityStatus.Current => PreparedWorkspaceAdmissionStatus.Opened,
            PreparedToolUseAuthorityStatus.Denied => PreparedWorkspaceAdmissionStatus.Denied,
            _ => PreparedWorkspaceAdmissionStatus.Unavailable,
        };
    }

    private async Task<bool> CheckStoreAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        CancellationToken cancellationToken)
    {
        PreparedCodeGraphOwnershipLoadResult loaded = await _ownershipStore.LoadAsync(
            binding, cancellationToken);
        return binding.Phase switch
        {
            PreparedCodeGraphOwnershipPhase.Absent =>
                loaded.Status == PreparedCodeGraphOwnershipLoadStatus.VerifiedAbsent && loaded.Record is null,
            PreparedCodeGraphOwnershipPhase.Ready =>
                loaded.Status == PreparedCodeGraphOwnershipLoadStatus.Found && loaded.Record == binding,
            _ => false,
        };
    }

    internal Task<PreparedWorkspaceBorrowResult> BeginBorrowAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        PreparedToolPurpose purpose,
        IPreparedWorkspaceResourceSettlementProbe resources,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new PreparedWorkspaceBorrowResult(
            PreparedWorkspaceSettlementStatus.Unknown, null));

    internal Task<PreparedWorkspaceSettlementStatus> CloseForDispositionAsync(
        PreparedCodeGraphWorkspaceOwnership binding,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PreparedWorkspaceSettlementStatus.Unknown);

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_state == RegistryState.Disposed)
            {
                return;
            }

            _state = RegistryState.Disposed;
            if (_hold is not null)
            {
                await _hold.DisposeAsync();
                _hold = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}

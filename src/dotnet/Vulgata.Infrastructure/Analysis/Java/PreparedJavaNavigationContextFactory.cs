using System.Collections.Concurrent;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Infrastructure.Analysis.Java;

public sealed record PreparedJavaNavigationProfile(
    string DisposableStateRoot,
    TimeSpan InitializationTimeout,
    TimeSpan RequestTimeout,
    TimeSpan ShutdownTimeout,
    int MaximumResponseBytes);

public enum PreparedJavaNavigationOpenStatus
{
    Opened,
    Denied,
    AuthorityUnavailable,
    RootUnavailable,
    ConfigurationUnavailable,
    ReadinessFailed,
    Cancelled,
    DeadlineExceeded,
    CleanupUnconfirmed,
}

public sealed record PreparedJavaNavigationOpenResult(
    PreparedJavaNavigationOpenStatus Status,
    IPreparedJavaNavigationContext? Context);

/// <summary>
/// Trusted runtime adapter. Before a session is returned, the runtime owns and
/// settles every resource it started, including its failure path.
/// </summary>
public interface IPreparedJdtLsRuntime
{
    Task<IPreparedJdtLsSession> OpenAsync(
        PreparedToolWorkspaceLease workspaceLease,
        PreparedJavaNavigationProfile profile,
        CancellationToken cancellationToken = default);
}

public sealed class PreparedJdtLsOpenFailureException : Exception
{
    public PreparedJdtLsOpenFailureException(
        PreparedJavaNavigationOpenStatus status,
        bool cleanupConfirmed)
    {
        Status = status;
        CleanupConfirmed = cleanupConfirmed;
    }

    public PreparedJavaNavigationOpenStatus Status { get; }

    public bool CleanupConfirmed { get; }
}

public interface IPreparedJdtLsSession : IAsyncDisposable
{
    Task<PreparedJdtLsRawReply> NavigateAsync(
        PreparedJavaNavigationOperation operation,
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class PreparedJdtSessionSettlementProbe : IPreparedWorkspaceResourceSettlementProbe
{
    private int _status = (int)PreparedWorkspaceSettlementStatus.Active;

    internal bool IsSettled => (PreparedWorkspaceSettlementStatus)Volatile.Read(ref _status) ==
        PreparedWorkspaceSettlementStatus.Settled;

    internal void ConfirmSettled() => Interlocked.CompareExchange(
        ref _status, (int)PreparedWorkspaceSettlementStatus.Settled,
        (int)PreparedWorkspaceSettlementStatus.Active);

    internal void MarkUnknown() => Interlocked.Exchange(
        ref _status, (int)PreparedWorkspaceSettlementStatus.Unknown);

    public ValueTask<PreparedWorkspaceSettlementStatus> InspectOwnedResourcesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult((PreparedWorkspaceSettlementStatus)Volatile.Read(ref _status));
    }
}

public sealed record PreparedJdtLsRawReply(
    IReadOnlyList<PreparedJdtLsRawLocation> Locations,
    bool LimitReached,
    string? CapabilityLimitation);

public sealed record PreparedJdtLsRawLocation(
    string RawProtocolUri,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter);

/// <summary>
/// Opens one purpose-bound navigation context through an injected trusted runtime.
/// It never resolves or launches an executable itself.
/// </summary>
public sealed class PreparedJavaNavigationContextFactory
{
    private static readonly ConcurrentDictionary<Guid, RepositorySlot> RepositorySlots = new();

    private sealed class RepositorySlot
    {
        internal SemaphoreSlim Gate { get; } = new(1, 1);

        internal bool Quarantined { get; set; }

        internal Task? PendingSessionDisposal { get; set; }
    }

    private readonly PreparedJavaNavigationProfile _profile;
    private readonly IPreparedJdtLsRuntime _runtime;
    private readonly PreparedCodeGraphStateCoordinator? _ownershipCoordinator;
    private readonly Func<CancellationToken, ValueTask<PreparedCodeGraphWorkspaceOwnership?>>? _getWorkspaceBinding;
    private readonly PreparedWorkspaceUseRegistry? _registry;

    public PreparedJavaNavigationContextFactory(
        PreparedJavaNavigationProfile profile,
        IPreparedJdtLsRuntime runtime)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _ownershipCoordinator = null;
    }

    internal PreparedJavaNavigationContextFactory(
        PreparedJavaNavigationProfile profile,
        IPreparedJdtLsRuntime runtime,
        PreparedCodeGraphStateCoordinator ownershipCoordinator,
        Func<CancellationToken, ValueTask<PreparedCodeGraphWorkspaceOwnership?>> getWorkspaceBinding)
        : this(profile, runtime)
    {
        _ownershipCoordinator = ownershipCoordinator ?? throw new ArgumentNullException(nameof(ownershipCoordinator));
        _getWorkspaceBinding = getWorkspaceBinding ?? throw new ArgumentNullException(nameof(getWorkspaceBinding));
    }

    internal PreparedJavaNavigationContextFactory(
        PreparedJavaNavigationProfile profile,
        IPreparedJdtLsRuntime runtime,
        PreparedCodeGraphStateCoordinator ownershipCoordinator,
        Func<CancellationToken, ValueTask<PreparedCodeGraphWorkspaceOwnership?>> getWorkspaceBinding,
        PreparedWorkspaceUseRegistry registry)
        : this(profile, runtime, ownershipCoordinator, getWorkspaceBinding)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public async Task<PreparedJavaNavigationOpenResult> OpenAsync(
        PreparedToolWorkspaceLease workspaceLease,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceLease);

        if (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedJavaNavigationOpenStatus.Cancelled);
        }

        if (workspaceLease.ToolPurpose != PreparedToolPurpose.JdtNavigation)
        {
            return Failed(PreparedJavaNavigationOpenStatus.Denied);
        }

        if (_ownershipCoordinator is null || _getWorkspaceBinding is null)
        {
            return Failed(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable);
        }

        if (!ValidProfile(_profile))
        {
            return Failed(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable);
        }

        PreparedToolWorkspaceCheckStatus leaseStatus =
            await workspaceLease.CheckCurrentAsync(cancellationToken);
        if (leaseStatus != PreparedToolWorkspaceCheckStatus.Current)
        {
            return Failed(Map(leaseStatus));
        }

        RepositorySlot slot = RepositorySlots.GetOrAdd(
            workspaceLease.RepositoryId, static _ => new RepositorySlot());
        using var waitDeadline = new CancellationTokenSource(_profile.InitializationTimeout);
        using var waitToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, waitDeadline.Token);
        try
        {
            await slot.Gate.WaitAsync(waitToken.Token);
        }
        catch (OperationCanceledException)
        {
            return Failed(cancellationToken.IsCancellationRequested
                ? PreparedJavaNavigationOpenStatus.Cancelled
                : PreparedJavaNavigationOpenStatus.DeadlineExceeded);
        }

        bool transferred = false;
        try
        {
            if (slot.Quarantined)
            {
                return Failed(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed);
            }

            leaseStatus = await workspaceLease.CheckCurrentAsync(cancellationToken);
            if (leaseStatus != PreparedToolWorkspaceCheckStatus.Current)
            {
                return Failed(Map(leaseStatus));
            }

            PreparedCodeGraphWorkspaceOwnership? binding;
            try
            {
                binding = await _getWorkspaceBinding(waitToken.Token).AsTask().WaitAsync(waitToken.Token);
            }
            catch (OperationCanceledException) when (waitToken.IsCancellationRequested)
            {
                return Failed(cancellationToken.IsCancellationRequested
                    ? PreparedJavaNavigationOpenStatus.Cancelled
                    : PreparedJavaNavigationOpenStatus.DeadlineExceeded);
            }
            catch (Exception)
            {
                return Failed(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable);
            }

            if (binding is null || !workspaceLease.MatchesApproval(binding.Approval))
            {
                return Failed(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable);
            }

            bool stateRootIsSeparate = false;
            bool bindingMatchesRoot = false;
            PreparedToolWorkspaceCheckStatus stateCheck = await workspaceLease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    stateRootIsSeparate = scope.IsSeparateDisposableStateRoot(
                        _profile.DisposableStateRoot);
                    bindingMatchesRoot = string.Equals(
                        scope.GetPhysicalRootPathForTrustedToolBinding(),
                        binding.PhysicalRootPath,
                        StringComparison.OrdinalIgnoreCase) &&
                        scope.ObserveRootObjectIdentity() == binding.RootObjectIdentity;
                    return ValueTask.CompletedTask;
                },
                waitToken.Token);
            if (stateCheck != PreparedToolWorkspaceCheckStatus.Current)
            {
                return Failed(Map(stateCheck));
            }

            if (!stateRootIsSeparate || !bindingMatchesRoot)
            {
                return Failed(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable);
            }

            PreparedCodeGraphOwnershipViewResult ownershipView;
            try
            {
                ownershipView = await _ownershipCoordinator.GetOwnershipViewAsync(
                    binding, binding.Approval, waitToken.Token).WaitAsync(waitToken.Token);
            }
            catch (OperationCanceledException) when (waitToken.IsCancellationRequested)
            {
                return Failed(cancellationToken.IsCancellationRequested
                    ? PreparedJavaNavigationOpenStatus.Cancelled
                    : PreparedJavaNavigationOpenStatus.DeadlineExceeded);
            }
            catch (Exception)
            {
                return Failed(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable);
            }

            if (ownershipView.Status is not (PreparedCodeGraphOwnershipViewStatus.VerifiedUnowned or
                PreparedCodeGraphOwnershipViewStatus.VerifiedOwned))
            {
                return Failed(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable);
            }

            PreparedWorkspaceBorrow? sessionRegistration = null;
            PreparedJdtSessionSettlementProbe? sessionProbe = null;
            if (_registry is not null)
            {
                sessionProbe = new PreparedJdtSessionSettlementProbe();
                PreparedCodeGraphWorkspaceOwnership currentBinding =
                    ownershipView.Status == PreparedCodeGraphOwnershipViewStatus.VerifiedOwned
                        ? ownershipView.Record! : binding;
                PreparedWorkspaceBorrowResult registered = await _registry.BeginBorrowAsync(
                    currentBinding, PreparedToolPurpose.JdtNavigation, sessionProbe, waitToken.Token);
                if (registered.Borrow is null)
                {
                    return Failed(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable);
                }

                sessionRegistration = registered.Borrow;
            }

            IPreparedJdtLsSession session;
            try
            {
                session = await _runtime.OpenAsync(workspaceLease, _profile, waitToken.Token);
            }
            catch (PreparedJdtLsOpenFailureException failure)
            {
                if (!failure.CleanupConfirmed)
                {
                    slot.Quarantined = true;
                    sessionProbe?.MarkUnknown();
                }
                else
                {
                    sessionProbe?.ConfirmSettled();
                    if (sessionRegistration is not null)
                    {
                        await sessionRegistration.DisposeAsync();
                    }
                }

                return Failed(failure.CleanupConfirmed
                    ? failure.Status
                    : PreparedJavaNavigationOpenStatus.CleanupUnconfirmed);
            }
            catch (Exception)
            {
                slot.Quarantined = true;
                sessionProbe?.MarkUnknown();
                return Failed(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed);
            }

            if (session is null)
            {
                slot.Quarantined = true;
                sessionProbe?.MarkUnknown();
                return Failed(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed);
            }

            leaseStatus = await workspaceLease.CheckCurrentAsync(cancellationToken);
            if (waitToken.IsCancellationRequested || leaseStatus != PreparedToolWorkspaceCheckStatus.Current)
            {
                Task? disposal = null;
                try
                {
                    disposal = session.DisposeAsync().AsTask();
                    await disposal.WaitAsync(_profile.ShutdownTimeout);
                    sessionProbe?.ConfirmSettled();
                    if (sessionRegistration is not null)
                    {
                        await sessionRegistration.DisposeAsync();
                    }
                    return Failed(waitToken.IsCancellationRequested
                        ? cancellationToken.IsCancellationRequested
                            ? PreparedJavaNavigationOpenStatus.Cancelled
                            : PreparedJavaNavigationOpenStatus.DeadlineExceeded
                        : Map(leaseStatus));
                }
                catch (Exception)
                {
                    slot.Quarantined = true;
                    sessionProbe?.MarkUnknown();
                    if (disposal is not null)
                    {
                        slot.PendingSessionDisposal = disposal;
                        _ = ObservePendingSessionDisposalAsync(slot, disposal);
                    }

                    return Failed(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed);
                }
            }

            transferred = true;
            return new PreparedJavaNavigationOpenResult(
                PreparedJavaNavigationOpenStatus.Opened,
                new PreparedJdtLsNavigationContext(
                    workspaceLease,
                    session,
                    _ownershipCoordinator,
                    binding,
                    cleanupConfirmed =>
                    {
                        if (!cleanupConfirmed)
                        {
                            slot.Quarantined = true;
                        }

                        slot.Gate.Release();
                    },
                    _profile.RequestTimeout,
                    _profile.ShutdownTimeout,
                    sessionRegistration,
                    sessionProbe));
        }
        finally
        {
            if (!transferred)
            {
                slot.Gate.Release();
            }
        }
    }

    private static async Task ObservePendingSessionDisposalAsync(RepositorySlot slot, Task disposal)
    {
        try
        {
            await disposal;
        }
        catch
        {
            // Late disposal cannot change the earlier unknown cleanup result.
        }
        finally
        {
            if (ReferenceEquals(slot.PendingSessionDisposal, disposal))
            {
                slot.PendingSessionDisposal = null;
            }
        }
    }

    private static bool ValidProfile(PreparedJavaNavigationProfile profile) =>
        !string.IsNullOrWhiteSpace(profile.DisposableStateRoot) &&
        Path.IsPathFullyQualified(profile.DisposableStateRoot) &&
        profile.InitializationTimeout > TimeSpan.Zero &&
        profile.RequestTimeout > TimeSpan.Zero &&
        profile.ShutdownTimeout > TimeSpan.Zero &&
        profile.MaximumResponseBytes > 0;

    private static PreparedJavaNavigationOpenStatus Map(PreparedToolWorkspaceCheckStatus status) =>
        status switch
        {
            PreparedToolWorkspaceCheckStatus.AuthorityUnavailable =>
                PreparedJavaNavigationOpenStatus.AuthorityUnavailable,
            PreparedToolWorkspaceCheckStatus.RootUnavailable =>
                PreparedJavaNavigationOpenStatus.RootUnavailable,
            PreparedToolWorkspaceCheckStatus.Cancelled =>
                PreparedJavaNavigationOpenStatus.Cancelled,
            PreparedToolWorkspaceCheckStatus.DeadlineExceeded =>
                PreparedJavaNavigationOpenStatus.DeadlineExceeded,
            _ => PreparedJavaNavigationOpenStatus.Denied,
        };

    private static PreparedJavaNavigationOpenResult Failed(PreparedJavaNavigationOpenStatus status) =>
        new(status, Context: null);
}

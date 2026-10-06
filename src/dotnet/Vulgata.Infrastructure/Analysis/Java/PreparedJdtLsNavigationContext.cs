using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Infrastructure.Analysis.Java;

/// <summary>
/// Owns the returned protocol session. The separately issued workspace lease is
/// borrowed and remains the trusted caller's disposal responsibility.
/// </summary>
public sealed class PreparedJdtLsNavigationContext : IPreparedJavaNavigationContext
{
    private readonly PreparedToolWorkspaceLease _workspaceLease;
    private readonly IPreparedJdtLsSession _session;
    private readonly PreparedCodeGraphStateCoordinator? _ownershipCoordinator;
    private readonly PreparedCodeGraphWorkspaceOwnership? _workspaceBinding;
    private readonly bool _dedicated;
    private readonly Action<bool> _releaseSlot;
    private readonly TimeSpan _requestTimeout;
    private readonly TimeSpan _shutdownTimeout;
    private readonly PreparedWorkspaceBorrow? _sessionRegistration;
    private readonly PreparedJdtSessionSettlementProbe? _sessionProbe;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposalRequested;
    private int _disposed;
    private int _slotReleased;
    private int _deferredRetirementStarted;
    private bool _sessionSettled;
    private Task? _pendingSessionDisposal;

    internal PreparedJdtLsNavigationContext(
        PreparedToolWorkspaceLease workspaceLease,
        IPreparedJdtLsSession session,
        PreparedCodeGraphStateCoordinator ownershipCoordinator,
        PreparedCodeGraphWorkspaceOwnership workspaceBinding,
        Action<bool> releaseSlot,
        TimeSpan requestTimeout,
        TimeSpan shutdownTimeout,
        PreparedWorkspaceBorrow? sessionRegistration = null,
        PreparedJdtSessionSettlementProbe? sessionProbe = null)
    {
        _workspaceLease = workspaceLease;
        _session = session;
        _ownershipCoordinator = ownershipCoordinator;
        _workspaceBinding = workspaceBinding;
        _releaseSlot = releaseSlot;
        _requestTimeout = requestTimeout;
        _shutdownTimeout = shutdownTimeout;
        _sessionRegistration = sessionRegistration;
        _sessionProbe = sessionProbe;
    }

    internal PreparedJdtLsNavigationContext(
        PreparedToolWorkspaceLease workspaceLease,
        IPreparedJdtLsSession session,
        Action<bool> releaseSlot,
        TimeSpan requestTimeout,
        TimeSpan shutdownTimeout)
    {
        _workspaceLease = workspaceLease;
        _session = session;
        _dedicated = true;
        _releaseSlot = releaseSlot;
        _requestTimeout = requestTimeout;
        _shutdownTimeout = shutdownTimeout;
    }

    public Guid RepositoryId => _workspaceLease.RepositoryId;

    public Task<PreparedJavaNavigationResult> GetDefinitionAsync(
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken = default) =>
        NavigateAsync(PreparedJavaNavigationOperation.Definition, request, cancellationToken);

    public Task<PreparedJavaNavigationResult> GetReferencesAsync(
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken = default) =>
        NavigateAsync(PreparedJavaNavigationOperation.References, request, cancellationToken);

    private async Task<PreparedJavaNavigationResult> NavigateAsync(
        PreparedJavaNavigationOperation operation,
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken)
    {
        using var requestDeadline = new CancellationTokenSource(_requestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, requestDeadline.Token);
        try
        {
            await _gate.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            return Empty(operation, cancellationToken.IsCancellationRequested
                ? PreparedJavaNavigationOutcome.Cancelled
                : PreparedJavaNavigationOutcome.DeadlineExceeded);
        }

        try
        {
            if (linked.IsCancellationRequested)
            {
                return Empty(operation, cancellationToken.IsCancellationRequested
                    ? PreparedJavaNavigationOutcome.Cancelled
                    : PreparedJavaNavigationOutcome.DeadlineExceeded);
            }

            if (Volatile.Read(ref _disposed) != 0 ||
                Volatile.Read(ref _disposalRequested) != 0 || !ValidRequest(request))
            {
                return Empty(operation, PreparedJavaNavigationOutcome.Denied);
            }

            PreparedToolWorkspaceCheckStatus before;
            Task<PreparedToolWorkspaceCheckStatus>? pendingAuthority = null;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                pendingAuthority = _workspaceLease.CheckCurrentAsync(cancellationToken);
                before = await pendingAuthority.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                if (pendingAuthority is { IsCompleted: false })
                {
                    _ = ObserveLateAuthorityCheckAsync(pendingAuthority);
                }

                return Empty(operation, cancellationToken.IsCancellationRequested
                    ? PreparedJavaNavigationOutcome.Cancelled
                    : PreparedJavaNavigationOutcome.DeadlineExceeded);
            }
            if (linked.IsCancellationRequested)
            {
                return Empty(operation, cancellationToken.IsCancellationRequested
                    ? PreparedJavaNavigationOutcome.Cancelled
                    : PreparedJavaNavigationOutcome.DeadlineExceeded);
            }

            if (before != PreparedToolWorkspaceCheckStatus.Current)
            {
                return Empty(operation, Map(before));
            }

            PreparedCodeGraphOwnershipViewResult? ownershipView = null;
            if (!_dedicated)
            {
            try
            {
                ownershipView = await _ownershipCoordinator!.GetOwnershipViewAsync(
                    _workspaceBinding!, _workspaceBinding!.Approval, linked.Token).WaitAsync(linked.Token);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                return Empty(operation, cancellationToken.IsCancellationRequested
                    ? PreparedJavaNavigationOutcome.Cancelled
                    : PreparedJavaNavigationOutcome.DeadlineExceeded);
            }
            catch (Exception)
            {
                return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (ownershipView.Status == PreparedCodeGraphOwnershipViewStatus.VerifiedOwned)
            {
                PreparedCodeGraphWorkspaceOwnership? owned = ownershipView.Record;
                if (owned is not { Phase: PreparedCodeGraphOwnershipPhase.Ready,
                    StateEntryObjectIdentity: not null } ||
                    owned.OwnershipId != _workspaceBinding!.OwnershipId)
                {
                    return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
                }

                bool matchesOwnedEntry = false;
                PreparedToolOwnedEntryPathClassification incomingPath =
                    PreparedToolOwnedEntryPathClassification.Unknown;
                PreparedToolWorkspaceCheckStatus rootCheck = await _workspaceLease.UseContainedRootAsync(
                    PreparedToolPurpose.JdtNavigation,
                    (scope, _) =>
                    {
                        matchesOwnedEntry = scope.ObserveCodeGraphEntryIdentity() ==
                            owned.StateEntryObjectIdentity;
                        if (matchesOwnedEntry)
                        {
                            incomingPath = scope.ClassifyOwnedEntryDescendant(
                                request.RelativePath, owned.StateEntryObjectIdentity);
                        }

                        return ValueTask.CompletedTask;
                    },
                    linked.Token);
                if (rootCheck != PreparedToolWorkspaceCheckStatus.Current || !matchesOwnedEntry ||
                    incomingPath == PreparedToolOwnedEntryPathClassification.Unknown)
                {
                    return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
                }

                if (incomingPath == PreparedToolOwnedEntryPathClassification.Owned)
                {
                    return Empty(operation, PreparedJavaNavigationOutcome.Denied);
                }
            }
            else if (ownershipView.Status != PreparedCodeGraphOwnershipViewStatus.VerifiedUnowned)
            {
                return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }
            }

            if (linked.IsCancellationRequested)
            {
                return Empty(operation, cancellationToken.IsCancellationRequested
                    ? PreparedJavaNavigationOutcome.Cancelled
                    : PreparedJavaNavigationOutcome.DeadlineExceeded);
            }

            if (Volatile.Read(ref _disposalRequested) != 0)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? PreparedJavaNavigationOutcome.Denied
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            PreparedJdtLsRawReply raw;
            Task<PreparedJdtLsRawReply>? pendingReply = null;
            try
            {
                pendingReply = _session.NavigateAsync(operation, request, linked.Token);
                raw = await pendingReply.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                bool protocolSettled = pendingReply is null ||
                    await AwaitProtocolSettlementAsync(pendingReply);
                bool settled = await RetireSessionUnderGateAsync(protocolSettled);
                return Empty(operation, settled
                    ? cancellationToken.IsCancellationRequested
                        ? PreparedJavaNavigationOutcome.Cancelled
                        : PreparedJavaNavigationOutcome.DeadlineExceeded
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }
            catch (PreparedJdtResponseBytesExceededException)
            {
                bool settled = await RetireSessionUnderGateAsync();
                if (!settled)
                {
                    return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
                }

                if (linked.IsCancellationRequested)
                {
                    return Empty(operation, cancellationToken.IsCancellationRequested
                        ? PreparedJavaNavigationOutcome.Cancelled
                        : PreparedJavaNavigationOutcome.DeadlineExceeded);
                }

                return new PreparedJavaNavigationResult(
                    RepositoryId,
                    operation,
                    PreparedJavaNavigationOutcome.Limited,
                    [],
                    [],
                    PreparedJavaStoppingLimit.ResponseBytes);
            }
            catch (Exception)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? PreparedJavaNavigationOutcome.ProtocolFailed
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (Volatile.Read(ref _disposalRequested) != 0)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? PreparedJavaNavigationOutcome.Denied
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (linked.IsCancellationRequested)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? cancellationToken.IsCancellationRequested
                        ? PreparedJavaNavigationOutcome.Cancelled
                        : PreparedJavaNavigationOutcome.DeadlineExceeded
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (raw?.Locations is null)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? PreparedJavaNavigationOutcome.ProtocolFailed
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            var locations = new List<PreparedJavaLocation>();
            var limitations = new List<PreparedJavaLimitation>();
            PreparedCodeGraphWorkspaceOwnership? ownedRecord =
                ownershipView?.Status == PreparedCodeGraphOwnershipViewStatus.VerifiedOwned
                    ? ownershipView.Record : null;
            bool ownedEntryMatches = true;
            PreparedToolWorkspaceCheckStatus mapping;
            Task<PreparedToolWorkspaceCheckStatus>? pendingMapping = null;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                pendingMapping = _workspaceLease.UseContainedRootAsync(
                    PreparedToolPurpose.JdtNavigation,
                    (scope, _) =>
                    {
                        if (linked.IsCancellationRequested)
                        {
                            return ValueTask.CompletedTask;
                        }

                        if (ownedRecord is not null)
                        {
                            ownedEntryMatches = scope.ObserveCodeGraphEntryIdentity() ==
                                ownedRecord.StateEntryObjectIdentity;
                            if (!ownedEntryMatches)
                            {
                                return ValueTask.CompletedTask;
                            }
                        }

                        foreach (PreparedJdtLsRawLocation location in raw.Locations.Take(request.MaximumLocations))
                        {
                            if (linked.IsCancellationRequested)
                            {
                                break;
                            }

                            if (location is not null &&
                                scope.TryMapContainedFileUri(location.RawProtocolUri, out PreparedSourceLocation? source) &&
                                source is not null &&
                                ValidRange(location))
                            {
                                if (ownedRecord is not null && source.RelativePath.StartsWith(
                                        ".codegraph/", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                locations.Add(new PreparedJavaLocation(
                                    source,
                                    location.StartLine,
                                    location.StartCharacter,
                                    location.EndLine,
                                    location.EndCharacter));
                            }
                            else
                            {
                                limitations.Add(new PreparedJavaLimitation("UnmappableLocation", null));
                            }
                        }

                        return ValueTask.CompletedTask;
                    },
                    cancellationToken);
                mapping = await pendingMapping.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                if (pendingMapping is { IsCompleted: false })
                {
                    _ = ObserveLateAuthorityCheckAsync(pendingMapping);
                }

                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? cancellationToken.IsCancellationRequested
                        ? PreparedJavaNavigationOutcome.Cancelled
                        : PreparedJavaNavigationOutcome.DeadlineExceeded
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }
            if (linked.IsCancellationRequested)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? cancellationToken.IsCancellationRequested
                        ? PreparedJavaNavigationOutcome.Cancelled
                        : PreparedJavaNavigationOutcome.DeadlineExceeded
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (mapping != PreparedToolWorkspaceCheckStatus.Current)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? Map(mapping)
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (!ownedEntryMatches)
            {
                _ = await RetireSessionUnderGateAsync();
                return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            PreparedToolWorkspaceCheckStatus disclosure;
            Task<PreparedToolWorkspaceCheckStatus>? pendingDisclosure = null;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                pendingDisclosure = _workspaceLease.CheckCurrentAsync(cancellationToken);
                disclosure = await pendingDisclosure.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                if (pendingDisclosure is { IsCompleted: false })
                {
                    _ = ObserveLateAuthorityCheckAsync(pendingDisclosure);
                }

                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? cancellationToken.IsCancellationRequested
                        ? PreparedJavaNavigationOutcome.Cancelled
                        : PreparedJavaNavigationOutcome.DeadlineExceeded
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }
            if (linked.IsCancellationRequested)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? cancellationToken.IsCancellationRequested
                        ? PreparedJavaNavigationOutcome.Cancelled
                        : PreparedJavaNavigationOutcome.DeadlineExceeded
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (disclosure != PreparedToolWorkspaceCheckStatus.Current)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? Map(disclosure)
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (!_dedicated)
            {
            PreparedCodeGraphOwnershipViewResult disclosureView;
            try
            {
                disclosureView = await _ownershipCoordinator!.GetOwnershipViewAsync(
                    _workspaceBinding!, _workspaceBinding!.Approval, linked.Token).WaitAsync(linked.Token);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? cancellationToken.IsCancellationRequested
                        ? PreparedJavaNavigationOutcome.Cancelled
                        : PreparedJavaNavigationOutcome.DeadlineExceeded
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }
            catch (Exception)
            {
                _ = await RetireSessionUnderGateAsync();
                return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (disclosureView.Status != ownershipView!.Status ||
                disclosureView.Record != ownershipView.Record)
            {
                _ = await RetireSessionUnderGateAsync();
                return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }
            }

            if (linked.IsCancellationRequested)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? cancellationToken.IsCancellationRequested
                        ? PreparedJavaNavigationOutcome.Cancelled
                        : PreparedJavaNavigationOutcome.DeadlineExceeded
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (Volatile.Read(ref _disposalRequested) != 0)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? PreparedJavaNavigationOutcome.Denied
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            bool locationLimited = raw.LimitReached || raw.Locations.Count > request.MaximumLocations;
            bool capabilityLimited = !string.IsNullOrWhiteSpace(raw.CapabilityLimitation);
            if (capabilityLimited)
            {
                limitations.Add(new PreparedJavaLimitation("CapabilityUnavailable", null));
            }

            bool limited = locationLimited || capabilityLimited;
            PreparedJavaNavigationOutcome outcome = limited
                ? PreparedJavaNavigationOutcome.Limited
                : locations.Count > 0
                    ? PreparedJavaNavigationOutcome.Observed
                    : PreparedJavaNavigationOutcome.NoResultObserved;
            return new PreparedJavaNavigationResult(
                RepositoryId,
                operation,
                outcome,
                locations,
                limitations,
                locationLimited ? PreparedJavaStoppingLimit.LocationLimit : null);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool ValidRequest(PreparedJavaNavigationRequest? request) =>
        request is not null &&
        PreparedSourceTraversal.IsCanonicalRelativePath(request.RelativePath, allowEmpty: false) &&
        request.RelativePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase) &&
        request.Line > 0 && request.Character > 0 &&
        request.MaximumLocations is > 0 and <= 100;

    private static async Task ObserveLateAuthorityCheckAsync(
        Task<PreparedToolWorkspaceCheckStatus> pendingAuthority)
    {
        try
        {
            _ = await pendingAuthority;
        }
        catch (Exception)
        {
            // A late authority result cannot authorize or disclose this request.
        }
    }

    private static bool ValidRange(PreparedJdtLsRawLocation location) =>
        location.StartLine > 0 && location.StartCharacter > 0 &&
        location.EndLine >= location.StartLine &&
        location.EndCharacter > 0 &&
        (location.EndLine > location.StartLine ||
         location.EndCharacter >= location.StartCharacter);

    private static PreparedJavaNavigationOutcome Map(PreparedToolWorkspaceCheckStatus status) =>
        status switch
        {
            PreparedToolWorkspaceCheckStatus.AuthorityUnavailable =>
                PreparedJavaNavigationOutcome.AuthorityUnavailable,
            PreparedToolWorkspaceCheckStatus.Cancelled =>
                PreparedJavaNavigationOutcome.Cancelled,
            PreparedToolWorkspaceCheckStatus.DeadlineExceeded =>
                PreparedJavaNavigationOutcome.DeadlineExceeded,
            _ => PreparedJavaNavigationOutcome.Denied,
        };

    private PreparedJavaNavigationResult Empty(
        PreparedJavaNavigationOperation operation,
        PreparedJavaNavigationOutcome outcome) =>
        new(RepositoryId, operation, outcome, [], [], null);

    public async ValueTask DisposeAsync()
    {
        if (!_dedicated)
        {
            await _gate.WaitAsync();
            try
            {
                _ = await RetireSessionUnderGateAsync();
            }
            finally
            {
                _gate.Release();
            }

            return;
        }

        Volatile.Write(ref _disposalRequested, 1);
        using var deadline = new CancellationTokenSource(_shutdownTimeout);
        try
        {
            await _gate.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            _sessionProbe?.MarkUnknown();
            ReleaseSlotOnce(cleanupConfirmed: false);
            StartDeferredRetirement();
            throw new TimeoutException("jdt_cleanup_unconfirmed");
        }

        try
        {
            if (!await RetireSessionUnderGateAsync(disposalDeadline: deadline.Token))
            {
                throw new TimeoutException("jdt_cleanup_unconfirmed");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void StartDeferredRetirement()
    {
        if (Interlocked.Exchange(ref _deferredRetirementStarted, 1) == 0)
        {
            _ = RetireAfterGateAsync();
        }
    }

    private async Task RetireAfterGateAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _ = await RetireSessionUnderGateAsync();
        }
        catch (Exception)
        {
            // The earlier public result remains cleanup-unconfirmed.
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> AwaitProtocolSettlementAsync(Task<PreparedJdtLsRawReply> pendingReply)
    {
        try
        {
            await pendingReply.WaitAsync(_shutdownTimeout);
            return true;
        }
        catch (TimeoutException)
        {
            return pendingReply.IsCompleted;
        }
        catch (Exception)
        {
            // A fault or cancellation has settled the owned protocol call.
            return true;
        }
    }

    private async Task<bool> RetireSessionUnderGateAsync(
        bool protocolSettled = true,
        CancellationToken disposalDeadline = default)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return _sessionSettled;
        }

        Task? disposal = null;
        try
        {
            disposal = _session.DisposeAsync().AsTask();
            if (disposalDeadline.CanBeCanceled)
            {
                await disposal.WaitAsync(disposalDeadline);
            }
            else
            {
                await disposal.WaitAsync(_shutdownTimeout);
            }
            _sessionSettled = protocolSettled;
            if (_sessionSettled)
            {
                _sessionProbe?.ConfirmSettled();
                if (_sessionRegistration is not null)
                {
                    await _sessionRegistration.DisposeAsync();
                }
            }
            else
            {
                _sessionProbe?.MarkUnknown();
            }

            return _sessionSettled;
        }
        catch (Exception)
        {
            _sessionSettled = false;
            _sessionProbe?.MarkUnknown();
            if (disposal is not null)
            {
                _pendingSessionDisposal = disposal;
                _ = ObservePendingSessionDisposalAsync(disposal);
            }

            return false;
        }
        finally
        {
            ReleaseSlotOnce(_sessionSettled);
        }
    }

    private void ReleaseSlotOnce(bool cleanupConfirmed)
    {
        if (Interlocked.Exchange(ref _slotReleased, 1) == 0)
        {
            _releaseSlot(cleanupConfirmed);
        }
    }

    private async Task ObservePendingSessionDisposalAsync(Task disposal)
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
            if (ReferenceEquals(_pendingSessionDisposal, disposal))
            {
                _pendingSessionDisposal = null;
            }
        }
    }
}

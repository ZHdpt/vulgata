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
    private readonly PreparedCodeGraphStateCoordinator _ownershipCoordinator;
    private readonly PreparedCodeGraphWorkspaceOwnership _workspaceBinding;
    private readonly Action<bool> _releaseSlot;
    private readonly TimeSpan _requestTimeout;
    private readonly TimeSpan _shutdownTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposed;

    internal PreparedJdtLsNavigationContext(
        PreparedToolWorkspaceLease workspaceLease,
        IPreparedJdtLsSession session,
        PreparedCodeGraphStateCoordinator ownershipCoordinator,
        PreparedCodeGraphWorkspaceOwnership workspaceBinding,
        Action<bool> releaseSlot,
        TimeSpan requestTimeout,
        TimeSpan shutdownTimeout)
    {
        _workspaceLease = workspaceLease;
        _session = session;
        _ownershipCoordinator = ownershipCoordinator;
        _workspaceBinding = workspaceBinding;
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

            if (Volatile.Read(ref _disposed) != 0 || !ValidRequest(request))
            {
                return Empty(operation, PreparedJavaNavigationOutcome.Denied);
            }

            PreparedToolWorkspaceCheckStatus before =
                await _workspaceLease.CheckCurrentAsync(cancellationToken);
            if (before != PreparedToolWorkspaceCheckStatus.Current)
            {
                return Empty(operation, Map(before));
            }

            PreparedCodeGraphOwnershipViewResult ownershipView;
            try
            {
                ownershipView = await _ownershipCoordinator.GetOwnershipViewAsync(
                    _workspaceBinding, _workspaceBinding.Approval, linked.Token).WaitAsync(linked.Token);
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
                    owned.OwnershipId != _workspaceBinding.OwnershipId)
                {
                    return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
                }

                if (request.RelativePath.StartsWith(".codegraph/", StringComparison.OrdinalIgnoreCase))
                {
                    bool matchesOwnedEntry = false;
                    PreparedToolWorkspaceCheckStatus rootCheck = await _workspaceLease.UseContainedRootAsync(
                        PreparedToolPurpose.JdtNavigation,
                        (scope, _) =>
                        {
                            matchesOwnedEntry = scope.ObserveCodeGraphEntryIdentity() ==
                                owned.StateEntryObjectIdentity;
                            return ValueTask.CompletedTask;
                        },
                        linked.Token);
                    if (rootCheck != PreparedToolWorkspaceCheckStatus.Current || !matchesOwnedEntry)
                    {
                        return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
                    }

                    return Empty(operation, PreparedJavaNavigationOutcome.Denied);
                }
            }
            else if (ownershipView.Status != PreparedCodeGraphOwnershipViewStatus.VerifiedUnowned)
            {
                return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            if (linked.IsCancellationRequested)
            {
                return Empty(operation, cancellationToken.IsCancellationRequested
                    ? PreparedJavaNavigationOutcome.Cancelled
                    : PreparedJavaNavigationOutcome.DeadlineExceeded);
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
            catch (Exception)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? PreparedJavaNavigationOutcome.ProtocolFailed
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
                ownershipView.Status == PreparedCodeGraphOwnershipViewStatus.VerifiedOwned
                    ? ownershipView.Record : null;
            bool ownedEntryMatches = true;
            PreparedToolWorkspaceCheckStatus mapping = await _workspaceLease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
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

            PreparedToolWorkspaceCheckStatus disclosure =
                await _workspaceLease.CheckCurrentAsync(cancellationToken);
            if (disclosure != PreparedToolWorkspaceCheckStatus.Current)
            {
                bool settled = await RetireSessionUnderGateAsync();
                return Empty(operation, settled
                    ? Map(disclosure)
                    : PreparedJavaNavigationOutcome.ConfigurationUnavailable);
            }

            PreparedCodeGraphOwnershipViewResult disclosureView;
            try
            {
                disclosureView = await _ownershipCoordinator.GetOwnershipViewAsync(
                    _workspaceBinding, _workspaceBinding.Approval, linked.Token).WaitAsync(linked.Token);
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

            if (disclosureView.Status != ownershipView.Status ||
                disclosureView.Record != ownershipView.Record)
            {
                _ = await RetireSessionUnderGateAsync();
                return Empty(operation, PreparedJavaNavigationOutcome.ConfigurationUnavailable);
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
        await _gate.WaitAsync();
        try
        {
            _ = await RetireSessionUnderGateAsync();
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

    private async Task<bool> RetireSessionUnderGateAsync(bool protocolSettled = true)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return true;
        }

        bool cleanupConfirmed = false;
        try
        {
            await _session.DisposeAsync().AsTask().WaitAsync(_shutdownTimeout);
            cleanupConfirmed = true;
            return protocolSettled;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            _releaseSlot(cleanupConfirmed && protocolSettled);
        }
    }
}

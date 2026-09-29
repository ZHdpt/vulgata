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
    private readonly Action<bool> _releaseSlot;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposed;

    internal PreparedJdtLsNavigationContext(
        PreparedToolWorkspaceLease workspaceLease,
        IPreparedJdtLsSession session,
        Action<bool> releaseSlot)
    {
        _workspaceLease = workspaceLease;
        _session = session;
        _releaseSlot = releaseSlot;
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
        await _gate.WaitAsync();
        try
        {
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

            PreparedJdtLsRawReply raw;
            try
            {
                raw = await _session.NavigateAsync(operation, request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Empty(operation, PreparedJavaNavigationOutcome.Cancelled);
            }
            catch (Exception)
            {
                return Empty(operation, PreparedJavaNavigationOutcome.ProtocolFailed);
            }

            if (raw?.Locations is null)
            {
                return Empty(operation, PreparedJavaNavigationOutcome.ProtocolFailed);
            }

            var locations = new List<PreparedJavaLocation>();
            var limitations = new List<PreparedJavaLimitation>();
            PreparedToolWorkspaceCheckStatus mapping = await _workspaceLease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    foreach (PreparedJdtLsRawLocation location in raw.Locations.Take(request.MaximumLocations))
                    {
                        if (location is not null &&
                            scope.TryMapContainedFileUri(location.Uri, out PreparedSourceLocation? source) &&
                            source is not null &&
                            ValidRange(location))
                        {
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
                return Empty(operation, Map(mapping));
            }

            PreparedToolWorkspaceCheckStatus disclosure =
                await _workspaceLease.CheckCurrentAsync(cancellationToken);
            if (disclosure != PreparedToolWorkspaceCheckStatus.Current)
            {
                return Empty(operation, Map(disclosure));
            }

            bool limited = raw.LimitReached || raw.Locations.Count > request.MaximumLocations;
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
                limited ? PreparedJavaStoppingLimit.LocationLimit : null);
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
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                bool cleanupConfirmed = false;
                try
                {
                    await _session.DisposeAsync();
                    cleanupConfirmed = true;
                }
                finally
                {
                    _releaseSlot(cleanupConfirmed);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}

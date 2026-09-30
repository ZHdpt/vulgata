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
    }

    private readonly PreparedJavaNavigationProfile _profile;
    private readonly IPreparedJdtLsRuntime _runtime;

    public PreparedJavaNavigationContextFactory(
        PreparedJavaNavigationProfile profile,
        IPreparedJdtLsRuntime runtime)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
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

            bool stateRootIsSeparate = false;
            PreparedToolWorkspaceCheckStatus stateCheck = await workspaceLease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    stateRootIsSeparate = scope.IsSeparateDisposableStateRoot(
                        _profile.DisposableStateRoot);
                    return ValueTask.CompletedTask;
                },
                cancellationToken);
            if (stateCheck != PreparedToolWorkspaceCheckStatus.Current)
            {
                return Failed(Map(stateCheck));
            }

            if (!stateRootIsSeparate)
            {
                return Failed(PreparedJavaNavigationOpenStatus.ConfigurationUnavailable);
            }

            IPreparedJdtLsSession session;
            try
            {
                session = await _runtime.OpenAsync(workspaceLease, _profile, cancellationToken);
            }
            catch (PreparedJdtLsOpenFailureException failure)
            {
                if (!failure.CleanupConfirmed)
                {
                    slot.Quarantined = true;
                }

                return Failed(failure.CleanupConfirmed
                    ? failure.Status
                    : PreparedJavaNavigationOpenStatus.CleanupUnconfirmed);
            }
            catch (Exception)
            {
                slot.Quarantined = true;
                return Failed(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed);
            }

            if (session is null)
            {
                slot.Quarantined = true;
                return Failed(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed);
            }

            leaseStatus = await workspaceLease.CheckCurrentAsync(cancellationToken);
            if (leaseStatus != PreparedToolWorkspaceCheckStatus.Current)
            {
                try
                {
                    await session.DisposeAsync();
                    return Failed(Map(leaseStatus));
                }
                catch (Exception)
                {
                    slot.Quarantined = true;
                    return Failed(PreparedJavaNavigationOpenStatus.CleanupUnconfirmed);
                }
            }

            transferred = true;
            return new PreparedJavaNavigationOpenResult(
                PreparedJavaNavigationOpenStatus.Opened,
                new PreparedJdtLsNavigationContext(
                    workspaceLease,
                    session,
                    cleanupConfirmed =>
                    {
                        if (!cleanupConfirmed)
                        {
                            slot.Quarantined = true;
                        }

                        slot.Gate.Release();
                    },
                    _profile.RequestTimeout,
                    _profile.ShutdownTimeout));
        }
        finally
        {
            if (!transferred)
            {
                slot.Gate.Release();
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

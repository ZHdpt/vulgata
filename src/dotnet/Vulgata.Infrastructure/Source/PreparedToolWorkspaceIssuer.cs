using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

public enum PreparedToolPurpose
{
    JdtNavigation,
    CodeGraphPreparation,
}

public enum PreparedToolUseAuthorityStatus
{
    Current,
    Denied,
    Unavailable,
}

public sealed record PreparedToolUseAuthorityResult(PreparedToolUseAuthorityStatus Status);

public interface IPreparedToolUseAuthorizer
{
    Task<PreparedToolUseAuthorityResult> VerifyCurrentAsync(
        PreparedSourceApproval approval,
        PreparedToolPurpose toolPurpose,
        CancellationToken cancellationToken = default);
}

public sealed record PreparedToolWorkspaceGrantRequest(
    PreparedSourceApproval Approval,
    string PhysicalRootPath,
    PreparedToolPurpose ToolPurpose,
    TimeSpan Deadline);

public enum PreparedToolWorkspaceIssueStatus
{
    Issued,
    Denied,
    AuthorityUnavailable,
    RootUnavailable,
    UnsupportedPlatform,
    Cancelled,
    DeadlineExceeded,
}

public sealed record PreparedToolWorkspaceIssueResult(
    PreparedToolWorkspaceIssueStatus Status,
    PreparedToolWorkspaceLease? Lease);

public enum PreparedToolWorkspaceLifetime
{
    Open,
    AuthorityClosed,
    ExpiredOrCancelled,
    Disposed,
}

public enum PreparedToolWorkspaceCheckStatus
{
    Current,
    Denied,
    AuthorityUnavailable,
    RootUnavailable,
    Cancelled,
    DeadlineExceeded,
    Disposed,
}

/// <summary>
/// Holds the prepared root privately for one trusted tool operation. The root is
/// never a Worker-facing argument or a serializable property.
/// </summary>
public sealed class PreparedToolWorkspaceLease : IAsyncDisposable
{
    private readonly PreparedSourceApproval _approval;
    private readonly string _rootPath;
    private readonly SafeFileHandle _rootHandle;
    private readonly PreparedSourceTraversal.DirectoryIdentity _rootIdentity;
    private readonly IPreparedSourceCurrentAuthorityVerifier _sourceAuthority;
    private readonly IPreparedToolUseAuthorizer _toolAuthorization;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly long _startedAt;
    private readonly TimeSpan _remainingDeadline;
    private int _lifetime;
    private PreparedToolWorkspaceCheckStatus _closedStatus;

    internal PreparedToolWorkspaceLease(
        PreparedSourceApproval approval,
        PreparedToolPurpose toolPurpose,
        string rootPath,
        SafeFileHandle rootHandle,
        PreparedSourceTraversal.DirectoryIdentity rootIdentity,
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization,
        TimeSpan remainingDeadline)
    {
        _approval = approval;
        RepositoryId = approval.RepositoryId;
        ToolPurpose = toolPurpose;
        _rootPath = rootPath;
        _rootHandle = rootHandle;
        _rootIdentity = rootIdentity;
        _sourceAuthority = sourceAuthority;
        _toolAuthorization = toolAuthorization;
        _remainingDeadline = remainingDeadline;
        _startedAt = Stopwatch.GetTimestamp();
    }

    public Guid RepositoryId { get; }

    public PreparedToolPurpose ToolPurpose { get; }

    public PreparedToolWorkspaceLifetime Lifetime =>
        (PreparedToolWorkspaceLifetime)Volatile.Read(ref _lifetime);

    public async Task<PreparedToolWorkspaceCheckStatus> CheckCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync();
        try
        {
            if (Lifetime != PreparedToolWorkspaceLifetime.Open)
            {
                return _closedStatus;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.Cancelled);
            }

            TimeSpan remaining = _remainingDeadline - Stopwatch.GetElapsedTime(_startedAt);
            if (remaining <= TimeSpan.Zero)
            {
                return Close(PreparedToolWorkspaceCheckStatus.DeadlineExceeded);
            }

            using var deadline = new CancellationTokenSource(remaining);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, deadline.Token);
            try
            {
                PreparedSourceAuthorityResult source = await _sourceAuthority.VerifyCurrentAsync(
                    _approval, linked.Token);
                if (source is null || source.Status == PreparedSourceAuthorityStatus.Unavailable)
                {
                    return Close(PreparedToolWorkspaceCheckStatus.AuthorityUnavailable);
                }

                if (source.Status != PreparedSourceAuthorityStatus.Current ||
                    source.CurrentApproval != _approval)
                {
                    return Close(PreparedToolWorkspaceCheckStatus.Denied);
                }

                PreparedToolUseAuthorityResult tool = await _toolAuthorization.VerifyCurrentAsync(
                    _approval, ToolPurpose, linked.Token);
                if (tool is null || tool.Status == PreparedToolUseAuthorityStatus.Unavailable)
                {
                    return Close(PreparedToolWorkspaceCheckStatus.AuthorityUnavailable);
                }

                if (tool.Status != PreparedToolUseAuthorityStatus.Current)
                {
                    return Close(PreparedToolWorkspaceCheckStatus.Denied);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.Cancelled);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.DeadlineExceeded);
            }
            catch (Exception)
            {
                return Close(PreparedToolWorkspaceCheckStatus.AuthorityUnavailable);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.Cancelled);
            }

            if (deadline.IsCancellationRequested ||
                Stopwatch.GetElapsedTime(_startedAt) >= _remainingDeadline)
            {
                return Close(PreparedToolWorkspaceCheckStatus.DeadlineExceeded);
            }

            (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? observedRoot) =
                PreparedSourceTraversal.OpenRoot(_rootPath);
            if (status != PreparedSourceTraversal.OpenStatus.Opened || observedRoot is null)
            {
                return Close(PreparedToolWorkspaceCheckStatus.RootUnavailable);
            }

            using (observedRoot)
            {
                if (PreparedSourceTraversal.ObserveDirectory(observedRoot) != _rootIdentity ||
                    PreparedSourceTraversal.ObserveDirectory(_rootHandle) != _rootIdentity)
                {
                    return Close(PreparedToolWorkspaceCheckStatus.RootUnavailable);
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Close(PreparedToolWorkspaceCheckStatus.Cancelled);
            }

            if (deadline.IsCancellationRequested ||
                Stopwatch.GetElapsedTime(_startedAt) >= _remainingDeadline)
            {
                return Close(PreparedToolWorkspaceCheckStatus.DeadlineExceeded);
            }

            return PreparedToolWorkspaceCheckStatus.Current;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (Lifetime != PreparedToolWorkspaceLifetime.Disposed)
            {
                _rootHandle.Dispose();
                _closedStatus = PreparedToolWorkspaceCheckStatus.Disposed;
                Volatile.Write(ref _lifetime, (int)PreparedToolWorkspaceLifetime.Disposed);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private PreparedToolWorkspaceCheckStatus Close(PreparedToolWorkspaceCheckStatus status)
    {
        _rootHandle.Dispose();
        _closedStatus = status;
        Volatile.Write(ref _lifetime, (int)(status is PreparedToolWorkspaceCheckStatus.Denied or
            PreparedToolWorkspaceCheckStatus.AuthorityUnavailable
            ? PreparedToolWorkspaceLifetime.AuthorityClosed
            : PreparedToolWorkspaceLifetime.ExpiredOrCancelled));
        return status;
    }
}

/// <summary>
/// Issues a trusted, purpose-specific physical workspace lease only after both
/// current source approval and separate tool authorization are verified.
/// </summary>
public sealed class PreparedToolWorkspaceIssuer
{
    private readonly IPreparedSourceCurrentAuthorityVerifier _sourceAuthority;
    private readonly IPreparedToolUseAuthorizer _toolAuthorization;

    public PreparedToolWorkspaceIssuer(
        IPreparedSourceCurrentAuthorityVerifier sourceAuthority,
        IPreparedToolUseAuthorizer toolAuthorization)
    {
        _sourceAuthority = sourceAuthority ?? throw new ArgumentNullException(nameof(sourceAuthority));
        _toolAuthorization = toolAuthorization ?? throw new ArgumentNullException(nameof(toolAuthorization));
    }

    public async Task<PreparedToolWorkspaceIssueResult> IssueAsync(
        PreparedToolWorkspaceGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.Cancelled);
        }

        if (!ValidRequest(request))
        {
            return Failed(PreparedToolWorkspaceIssueStatus.Denied);
        }

        if (request.Deadline == TimeSpan.Zero)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.DeadlineExceeded);
        }

        long startedAt = Stopwatch.GetTimestamp();
        using var deadline = new CancellationTokenSource(request.Deadline);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadline.Token);

        try
        {
            PreparedSourceAuthorityResult source = await _sourceAuthority.VerifyCurrentAsync(
                request.Approval, linked.Token);
            if (source is null || source.Status == PreparedSourceAuthorityStatus.Unavailable)
            {
                return Failed(PreparedToolWorkspaceIssueStatus.AuthorityUnavailable);
            }

            if (source.Status != PreparedSourceAuthorityStatus.Current ||
                source.CurrentApproval != request.Approval)
            {
                return Failed(PreparedToolWorkspaceIssueStatus.Denied);
            }

            PreparedToolUseAuthorityResult tool = await _toolAuthorization.VerifyCurrentAsync(
                request.Approval, request.ToolPurpose, linked.Token);
            if (tool is null || tool.Status == PreparedToolUseAuthorityStatus.Unavailable)
            {
                return Failed(PreparedToolWorkspaceIssueStatus.AuthorityUnavailable);
            }

            if (tool.Status != PreparedToolUseAuthorityStatus.Current)
            {
                return Failed(PreparedToolWorkspaceIssueStatus.Denied);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.Cancelled);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.DeadlineExceeded);
        }
        catch (Exception)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.AuthorityUnavailable);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.Cancelled);
        }

        if (deadline.IsCancellationRequested || Stopwatch.GetElapsedTime(startedAt) >= request.Deadline)
        {
            return Failed(PreparedToolWorkspaceIssueStatus.DeadlineExceeded);
        }

        if (!OperatingSystem.IsWindows())
        {
            return Failed(PreparedToolWorkspaceIssueStatus.UnsupportedPlatform);
        }

        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? root) =
            PreparedSourceTraversal.OpenRoot(request.PhysicalRootPath);
        if (root is null)
        {
            return Failed(status == PreparedSourceTraversal.OpenStatus.Denied
                ? PreparedToolWorkspaceIssueStatus.Denied
                : PreparedToolWorkspaceIssueStatus.RootUnavailable);
        }

        if (cancellationToken.IsCancellationRequested || deadline.IsCancellationRequested ||
            Stopwatch.GetElapsedTime(startedAt) >= request.Deadline)
        {
            root.Dispose();
            return Failed(cancellationToken.IsCancellationRequested
                ? PreparedToolWorkspaceIssueStatus.Cancelled
                : PreparedToolWorkspaceIssueStatus.DeadlineExceeded);
        }

        PreparedSourceTraversal.DirectoryIdentity? rootIdentity =
            PreparedSourceTraversal.ObserveDirectory(root);
        if (rootIdentity is null)
        {
            root.Dispose();
            return Failed(PreparedToolWorkspaceIssueStatus.RootUnavailable);
        }

        TimeSpan remaining = request.Deadline - Stopwatch.GetElapsedTime(startedAt);
        if (remaining <= TimeSpan.Zero)
        {
            root.Dispose();
            return Failed(PreparedToolWorkspaceIssueStatus.DeadlineExceeded);
        }

        return new PreparedToolWorkspaceIssueResult(
            PreparedToolWorkspaceIssueStatus.Issued,
            new PreparedToolWorkspaceLease(
                request.Approval,
                request.ToolPurpose,
                request.PhysicalRootPath,
                root,
                rootIdentity,
                _sourceAuthority,
                _toolAuthorization,
                remaining));
    }

    private static bool ValidRequest(PreparedToolWorkspaceGrantRequest request) =>
        request.Approval is { } approval &&
        approval.BatchId != Guid.Empty &&
        approval.RepositoryId != Guid.Empty &&
        approval.Purpose == PreparedSourcePurpose.WorkerSourceRead &&
        !string.IsNullOrWhiteSpace(approval.SubjectId) &&
        !string.IsNullOrWhiteSpace(approval.ApprovalIdentity) &&
        !string.IsNullOrWhiteSpace(approval.RootBindingIdentity) &&
        !string.IsNullOrWhiteSpace(approval.OperatorLabel) &&
        Enum.IsDefined(request.ToolPurpose) &&
        request.Deadline >= TimeSpan.Zero &&
        request.Deadline <= PreparedSourceGrantLimits.Default.Deadline &&
        !string.IsNullOrWhiteSpace(request.PhysicalRootPath) &&
        Path.IsPathFullyQualified(request.PhysicalRootPath);

    private static PreparedToolWorkspaceIssueResult Failed(PreparedToolWorkspaceIssueStatus status) =>
        new(status, Lease: null);
}

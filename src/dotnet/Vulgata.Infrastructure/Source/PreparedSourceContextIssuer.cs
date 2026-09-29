using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

/// <summary>
/// Issues a single prepared-source context from trusted application configuration.
/// The verifier is mandatory; no cached or unconditional approval is sufficient.
/// </summary>
public sealed class PreparedSourceContextIssuer
{
    private readonly IPreparedSourceCurrentAuthorityVerifier _authority;

    public PreparedSourceContextIssuer(IPreparedSourceCurrentAuthorityVerifier authority)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
    }

    public async Task<PreparedSourceIssueResult> IssueAsync(
        PreparedSourceGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedSourceIssueStatus.Cancelled);
        }

        if (request.Limits is null || request.Limits.Deadline < TimeSpan.Zero ||
            !WithinApplicationCaps(request.Limits) ||
            !ValidApproval(request.Approval) ||
            string.IsNullOrWhiteSpace(request.PhysicalRootPath) ||
            !Path.IsPathFullyQualified(request.PhysicalRootPath))
        {
            return Failed(PreparedSourceIssueStatus.Denied);
        }

        if (request.Limits.Deadline == TimeSpan.Zero)
        {
            return Failed(PreparedSourceIssueStatus.DeadlineExceeded);
        }

        long issuedAt = Stopwatch.GetTimestamp();
        using var deadline = new CancellationTokenSource(request.Limits.Deadline);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);

        PreparedSourceAuthorityResult authority;
        try
        {
            authority = await _authority.VerifyCurrentAsync(
                request.Approval,
                linked.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedSourceIssueStatus.Cancelled);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return Failed(PreparedSourceIssueStatus.DeadlineExceeded);
        }
        catch (Exception)
        {
            return Failed(PreparedSourceIssueStatus.AuthorityUnavailable);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedSourceIssueStatus.Cancelled);
        }

        if (deadline.IsCancellationRequested)
        {
            return Failed(PreparedSourceIssueStatus.DeadlineExceeded);
        }

        if (authority is null || authority.Status == PreparedSourceAuthorityStatus.Unavailable)
        {
            return Failed(PreparedSourceIssueStatus.AuthorityUnavailable);
        }

        if (authority.Status != PreparedSourceAuthorityStatus.Current ||
            authority.CurrentApproval != request.Approval)
        {
            return Failed(PreparedSourceIssueStatus.Denied);
        }

        if (!OperatingSystem.IsWindows())
        {
            return Failed(PreparedSourceIssueStatus.UnsupportedPlatform);
        }

        (PreparedSourceTraversal.OpenStatus rootStatus, SafeFileHandle? rootHandle) =
            PreparedSourceTraversal.OpenRoot(
            request.PhysicalRootPath);
        if (rootHandle is null)
        {
            return Failed(rootStatus == PreparedSourceTraversal.OpenStatus.Denied
                ? PreparedSourceIssueStatus.Denied
                : PreparedSourceIssueStatus.RootUnavailable);
        }

        if (cancellationToken.IsCancellationRequested || deadline.IsCancellationRequested)
        {
            rootHandle.Dispose();
            return Failed(cancellationToken.IsCancellationRequested
                ? PreparedSourceIssueStatus.Cancelled
                : PreparedSourceIssueStatus.DeadlineExceeded);
        }

        TimeSpan remaining = request.Limits.Deadline -
            Stopwatch.GetElapsedTime(issuedAt);
        if (remaining <= TimeSpan.Zero)
        {
            rootHandle.Dispose();
            return Failed(PreparedSourceIssueStatus.DeadlineExceeded);
        }

        return new PreparedSourceIssueResult(
            PreparedSourceIssueStatus.Issued,
            new PreparedSourceReadContext(
                request.Approval,
                rootHandle,
                _authority,
                request.Limits,
                remaining));
    }

    private static bool WithinApplicationCaps(PreparedSourceGrantLimits limits)
    {
        PreparedSourceGrantLimits maximum = PreparedSourceGrantLimits.Default;
        return limits.MaximumOperations > 0 &&
            limits.MaximumOperations <= maximum.MaximumOperations &&
            limits.MaximumMetadataEntries > 0 &&
            limits.MaximumMetadataEntries <= maximum.MaximumMetadataEntries &&
            limits.MaximumExaminedBytes > 0 &&
            limits.MaximumExaminedBytes <= maximum.MaximumExaminedBytes &&
            limits.MaximumReturnedBytes > 0 &&
            limits.MaximumReturnedBytes <= maximum.MaximumReturnedBytes &&
            limits.Deadline <= maximum.Deadline;
    }

    private static bool ValidApproval(PreparedSourceApproval? approval) =>
        approval is not null &&
        approval.BatchId != Guid.Empty &&
        approval.RepositoryId != Guid.Empty &&
        approval.Purpose == PreparedSourcePurpose.WorkerSourceRead &&
        !string.IsNullOrWhiteSpace(approval.SubjectId) &&
        !string.IsNullOrWhiteSpace(approval.ApprovalIdentity) &&
        !string.IsNullOrWhiteSpace(approval.RootBindingIdentity) &&
        !string.IsNullOrWhiteSpace(approval.OperatorLabel);

    private static PreparedSourceIssueResult Failed(PreparedSourceIssueStatus status) =>
        new(status, Context: null);

}

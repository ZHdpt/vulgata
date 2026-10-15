using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.CodeGraph;

namespace Vulgata.Infrastructure.Source;

/// <summary>
/// Issues a single prepared-source context from trusted application configuration.
/// The verifier is mandatory; no cached or unconditional approval is sufficient.
/// </summary>
public sealed class PreparedSourceContextIssuer
{
    // Business's bounded verifier reads a file twice before releasing any cited slice.
    // These are private grant ceilings; per-call limits and the model-facing budgets remain separate.
    private static readonly PreparedSourceGrantLimits _businessMaximum = new(
        MaximumOperations: 512,
        MaximumMetadataEntries: 1,
        MaximumExaminedBytes: 16L * 1024 * 1024,
        MaximumReturnedBytes: 8L * 1024 * 1024,
        Deadline: TimeSpan.FromSeconds(30));
    private readonly IPreparedSourceCurrentAuthorityVerifier _authority;

    public PreparedSourceContextIssuer(IPreparedSourceCurrentAuthorityVerifier authority)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
    }

    /// <summary>Trusted host observation for bounded inventory restart reconciliation.</summary>
    public Task<string?> ObserveFileIdentityAsync(
        IPreparedSourceReadContext context,
        string relativePath,
        CancellationToken cancellationToken = default)
        => context is PreparedSourceReadContext issued
            ? issued.ObserveFileIdentityAsync(_authority, relativePath, cancellationToken)
            : Task.FromResult<string?>(null);

    public Task<PreparedSourceIssueResult> IssueAsync(
        PreparedSourceGrantRequest request,
        PreparedCodeGraphConnection ownedState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownedState);
        return IssueCoreAsync(request, ownedState, cancellationToken);
    }

    public Task<PreparedSourceIssueResult> IssueAsync(
        PreparedSourceGrantRequest request,
        CancellationToken cancellationToken = default)
        => IssueCoreAsync(request, null, cancellationToken);

    private async Task<PreparedSourceIssueResult> IssueCoreAsync(
        PreparedSourceGrantRequest request,
        PreparedCodeGraphConnection? ownedState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Failed(PreparedSourceIssueStatus.Cancelled);
        }

        if (request.Limits is null || request.Limits.Deadline < TimeSpan.Zero ||
            !ValidApproval(request.Approval) ||
            !WithinApplicationCaps(request.Limits, request.Approval.Purpose) ||
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

        if (ownedState is not null && !ownedState.MatchesSourceBinding(request, rootHandle))
        {
            rootHandle.Dispose();
            return Failed(PreparedSourceIssueStatus.Denied);
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
                remaining,
                ownedState));
    }

    private static bool WithinApplicationCaps(PreparedSourceGrantLimits limits, PreparedSourcePurpose purpose)
    {
        PreparedSourceGrantLimits maximum = purpose == PreparedSourcePurpose.BusinessSynthesisRead
            ? _businessMaximum : PreparedSourceGrantLimits.Default;
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
        approval.Purpose is (PreparedSourcePurpose.WorkerSourceRead or PreparedSourcePurpose.BusinessSynthesisRead) &&
        !string.IsNullOrWhiteSpace(approval.SubjectId) &&
        !string.IsNullOrWhiteSpace(approval.ApprovalIdentity) &&
        !string.IsNullOrWhiteSpace(approval.RootBindingIdentity) &&
        !string.IsNullOrWhiteSpace(approval.OperatorLabel);

    private static PreparedSourceIssueResult Failed(PreparedSourceIssueStatus status) =>
        new(status, Context: null);

}

using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

public delegate Task<PreparedSourceAuthorityResult> OctoberCurrentApprovalCheck(
    string targetId,
    PreparedSourceApproval requestedApproval,
    CancellationToken cancellationToken);

public sealed class OctoberPreparedSourceAuthorityVerifier : IPreparedSourceCurrentAuthorityVerifier
{
    private readonly string _targetId;
    private readonly OctoberCurrentApprovalCheck _currentApprovalCheck;
    private readonly Func<PreparedSourceApproval, bool> _hasCurrentRootBinding;

    public OctoberPreparedSourceAuthorityVerifier(
        string targetId,
        OctoberCurrentApprovalCheck currentApprovalCheck,
        Func<PreparedSourceApproval, bool> hasCurrentRootBinding)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("An exact October target is required.", nameof(targetId));
        }

        _targetId = targetId;
        _currentApprovalCheck = currentApprovalCheck ??
            throw new ArgumentNullException(nameof(currentApprovalCheck));
        _hasCurrentRootBinding = hasCurrentRootBinding ??
            throw new ArgumentNullException(nameof(hasCurrentRootBinding));
    }

    public async Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
        PreparedSourceApproval requestedApproval,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidApproval(requestedApproval))
        {
            return new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Revoked, null);
        }

        PreparedSourceAuthorityResult? currentDecision;
        try
        {
            currentDecision = await _currentApprovalCheck(_targetId, requestedApproval, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Unavailable, null);
        }

        if (currentDecision is null || currentDecision.Status == PreparedSourceAuthorityStatus.Unavailable)
        {
            return new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Unavailable, null);
        }

        if (currentDecision.Status != PreparedSourceAuthorityStatus.Current ||
            currentDecision.CurrentApproval != requestedApproval)
        {
            return new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Revoked, null);
        }

        try
        {
            return _hasCurrentRootBinding(requestedApproval)
                ? new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Current, requestedApproval)
                : new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Revoked, null);
        }
        catch (Exception)
        {
            return new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Unavailable, null);
        }
    }

    private static bool ValidApproval(PreparedSourceApproval? approval) =>
        approval is not null && approval.BatchId != Guid.Empty &&
        approval.RepositoryId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(approval.SubjectId) &&
        approval.Purpose is (PreparedSourcePurpose.WorkerSourceRead or PreparedSourcePurpose.BusinessSynthesisRead) &&
        !string.IsNullOrWhiteSpace(approval.ApprovalIdentity) &&
        !string.IsNullOrWhiteSpace(approval.RootBindingIdentity) &&
        !string.IsNullOrWhiteSpace(approval.OperatorLabel);
}

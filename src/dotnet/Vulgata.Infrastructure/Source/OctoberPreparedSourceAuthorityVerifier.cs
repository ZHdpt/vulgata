using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Source;

public delegate Task<PreparedSourceAuthorityResult> OctoberWebApprovalCheck(
    string targetId,
    PreparedSourceApproval requestedApproval,
    CancellationToken cancellationToken);

public sealed class OctoberPreparedSourceAuthorityVerifier : IPreparedSourceCurrentAuthorityVerifier
{
    private readonly string _targetId;
    private readonly OctoberWebApprovalCheck _webApprovalCheck;
    private readonly Func<PreparedSourceApproval, bool> _hasCurrentRootBinding;

    public OctoberPreparedSourceAuthorityVerifier(
        string targetId,
        OctoberWebApprovalCheck webApprovalCheck,
        Func<PreparedSourceApproval, bool> hasCurrentRootBinding)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException("An exact October target is required.", nameof(targetId));
        }

        _targetId = targetId;
        _webApprovalCheck = webApprovalCheck ?? throw new ArgumentNullException(nameof(webApprovalCheck));
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

        PreparedSourceAuthorityResult? webDecision;
        try
        {
            webDecision = await _webApprovalCheck(_targetId, requestedApproval, cancellationToken);
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

        if (webDecision is null || webDecision.Status == PreparedSourceAuthorityStatus.Unavailable)
        {
            return new PreparedSourceAuthorityResult(PreparedSourceAuthorityStatus.Unavailable, null);
        }

        if (webDecision.Status != PreparedSourceAuthorityStatus.Current ||
            webDecision.CurrentApproval != requestedApproval)
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
        approval.Purpose == PreparedSourcePurpose.WorkerSourceRead &&
        !string.IsNullOrWhiteSpace(approval.ApprovalIdentity) &&
        !string.IsNullOrWhiteSpace(approval.RootBindingIdentity) &&
        !string.IsNullOrWhiteSpace(approval.OperatorLabel);
}

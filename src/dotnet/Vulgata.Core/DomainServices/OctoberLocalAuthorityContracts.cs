namespace Vulgata.Core.DomainServices;

public interface IOctoberOperatorSession : IAsyncDisposable
{
    string SubjectId { get; }
    string ManagementAuthorityId { get; }
    DateTimeOffset ExpiresAtUtc { get; }
}

public enum OctoberAuthenticationStatus
{
    Authenticated,
    Denied,
    AdditionalAuthenticationRequired,
    Unavailable,
    Cancelled
}

public sealed record OctoberAuthenticationResult(
    OctoberAuthenticationStatus Status,
    IOctoberOperatorSession? Session);

public sealed record OctoberAuthoritySelectionRequest(
    string TargetId,
    Guid BatchId,
    OctoberSetupRequest? PrepareRequest);

public enum OctoberAuthorityDecisionStatus
{
    Authorized,
    Denied,
    MissingBatch,
    BatchMismatch,
    Unavailable,
    Cancelled
}

public sealed record OctoberAuthoritySelectionResult(
    OctoberAuthorityDecisionStatus Status,
    OctoberSetupRequest? Request,
    OctoberAuthorizedSelection? Authority);

public enum OctoberSourceApprovalStatus
{
    Issued,
    Denied,
    Unavailable,
    Cancelled
}

public sealed record OctoberSourceApprovalResult(
    OctoberSourceApprovalStatus Status,
    PreparedSourceApproval? Approval,
    DateTimeOffset? ExpiresAtUtc);

public enum OctoberRevocationStatus
{
    Revoked,
    AlreadyRevoked,
    Denied,
    Unavailable,
    Cancelled
}

public interface IOctoberLocalAuthority
{
    Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken);

    Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(
        IOctoberOperatorSession session,
        OctoberAuthoritySelectionRequest request,
        CancellationToken cancellationToken);

    Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(
        IOctoberOperatorSession session,
        IOctoberHistoricalBoundSetupData boundSetup,
        Guid repositoryId,
        CancellationToken cancellationToken);

    Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(
        IOctoberOperatorSession session,
        string targetId,
        PreparedSourceApproval approval,
        CancellationToken cancellationToken);

    Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(
        IOctoberOperatorSession session,
        string targetId,
        string approvalIdentity,
        CancellationToken cancellationToken);
}

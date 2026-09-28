namespace Vulgata.Core.DomainServices;

/// <summary>
/// The exact approval that a trusted issuer binds to one live prepared-source context.
/// The current-authority verifier checks this binding at issuance and before disclosure.
/// </summary>
public sealed record PreparedSourceApproval(
    Guid BatchId,
    Guid RepositoryId,
    string SubjectId,
    PreparedSourcePurpose Purpose,
    string ApprovalIdentity,
    string RootBindingIdentity,
    string OperatorLabel);

public enum PreparedSourcePurpose
{
    WorkerSourceRead,
}

/// <summary>
/// Trusted issuance input. PhysicalRootPath is application-held configuration; it is
/// never an operation argument or a property of the Worker-facing read context.
/// Infrastructure opens and retains the root identity and handles privately.
/// </summary>
public sealed record PreparedSourceGrantRequest(
    PreparedSourceApproval Approval,
    string PhysicalRootPath,
    PreparedSourceGrantLimits Limits);

public enum PreparedSourceIssueStatus
{
    Issued,
    Denied,
    AuthorityUnavailable,
    RootUnavailable,
    UnsupportedPlatform,
    Cancelled,
    DeadlineExceeded,
}

/// <summary>
/// Issuance failures expose only a closed status and no context or trusted input.
/// </summary>
public sealed record PreparedSourceIssueResult(
    PreparedSourceIssueStatus Status,
    IPreparedSourceReadContext? Context);

/// <summary>
/// A trusted issuer may lower these grant limits but cannot raise the application caps.
/// All counters are cumulative across calls on one context.
/// </summary>
public sealed record PreparedSourceGrantLimits(
    int MaximumOperations,
    int MaximumMetadataEntries,
    long MaximumExaminedBytes,
    long MaximumReturnedBytes,
    TimeSpan Deadline)
{
    public static PreparedSourceGrantLimits Default { get; } = new(
        MaximumOperations: 100,
        MaximumMetadataEntries: 1_000,
        MaximumExaminedBytes: 8 * 1024 * 1024,
        MaximumReturnedBytes: 1024 * 1024,
        Deadline: TimeSpan.FromSeconds(30));
}

public static class PreparedSourceCallLimits
{
    public const int MaximumListEntries = 100;
    public const int MaximumReadBytes = 64 * 1024;
    public const int MaximumSearchMatches = 64;
    public const int MaximumSearchLiteralUtf8Bytes = 256;
    public const int MaximumExaminedBytes = 1024 * 1024;
}

/// <summary>
/// A verifier must return the current approval for the requested exact binding,
/// revocation/change, or inability to establish current authority. An unavailable
/// result never authorizes use of a prior approval.
/// </summary>
public interface IPreparedSourceCurrentAuthorityVerifier
{
    Task<PreparedSourceAuthorityResult> VerifyCurrentAsync(
        PreparedSourceApproval requestedApproval,
        CancellationToken cancellationToken = default);
}

public enum PreparedSourceAuthorityStatus
{
    Current,
    Revoked,
    Unavailable,
}

public sealed record PreparedSourceAuthorityResult(
    PreparedSourceAuthorityStatus Status,
    PreparedSourceApproval? CurrentApproval);

/// <summary>
/// The only source capability given to Agent Execution. One context is bound to one
/// grant; operation requests cannot select a batch, Repository, subject or root.
/// </summary>
public interface IPreparedSourceReadContext : IAsyncDisposable
{
    Guid RepositoryId { get; }

    PreparedSourceContextLifetime Lifetime { get; }

    Task<PreparedSourceResult> ListAsync(
        PreparedSourceListRequest request,
        CancellationToken cancellationToken = default);

    Task<PreparedSourceResult> ReadTextRangeAsync(
        PreparedSourceReadTextRangeRequest request,
        CancellationToken cancellationToken = default);

    Task<PreparedSourceResult> SearchTextAsync(
        PreparedSourceSearchTextRequest request,
        CancellationToken cancellationToken = default);
}

public enum PreparedSourceContextLifetime
{
    Open,
    AuthorityClosed,
    ExpiredOrCancelled,
    Disposed,
}

// Directory paths may be empty to denote the granted root; file paths may not.
// Infrastructure validates the canonical, slash-separated relative spelling.
public sealed record PreparedSourceListRequest(
    string RelativeDirectory,
    int PageSize,
    string? Continuation = null);

public sealed record PreparedSourceReadTextRangeRequest(
    string RelativePath,
    long ByteOffset,
    int ByteCount);

public sealed record PreparedSourceSearchTextRequest(
    string RelativeDirectory,
    string Literal,
    int PageSize,
    string? Continuation = null);

public enum PreparedSourceOperation
{
    List,
    ReadTextRange,
    SearchText,
}

public enum PreparedSourceOutcome
{
    Complete,
    CompleteEmpty,
    Limited,
    Incomplete,
    Denied,
    AuthorityUnavailable,
    Missing,
    Unreadable,
    UnsupportedText,
    InvalidRange,
    ChangedInput,
    Cancelled,
    DeadlineExceeded,
}

public enum PreparedSourceEntryKind
{
    Directory,
    RegularFile,
    NonTraversableLink,
}

public sealed record PreparedSourceLocation(Guid RepositoryId, string RelativePath);

public sealed record PreparedSourceReadLocation(
    Guid RepositoryId,
    string RelativePath,
    long ByteStart,
    long ByteEndExclusive);

public sealed record PreparedSourceMatchLocation(
    Guid RepositoryId,
    string RelativePath,
    long ByteStart,
    int ByteLength,
    int Line,
    int UnicodeScalarColumn);

public sealed record PreparedSourceEntry(
    PreparedSourceLocation Location,
    PreparedSourceEntryKind Kind,
    long? ObservedByteLength = null);

public sealed record PreparedSourceMatch(
    PreparedSourceMatchLocation Location,
    string? Excerpt = null);

public enum PreparedSourceLimitationReason
{
    NonTraversableLink,
    Missing,
    Unreadable,
    UnsupportedText,
    ChangedInput,
}

public sealed record PreparedSourceLimitation(
    PreparedSourceLocation Location,
    PreparedSourceLimitationReason Reason);

public enum PreparedSourceStoppingLimit
{
    PageSize,
    CallReturnedBytes,
    CallExaminedBytes,
    GrantOperations,
    GrantMetadataEntries,
    GrantExaminedBytes,
    GrantReturnedBytes,
}

/// <summary>
/// Sanitized public result. A limited page provides a same-scope continuation,
/// next byte offset or exhausted limit; authority loss discards all payload.
/// </summary>
public sealed record PreparedSourceResult
{
    public required Guid RepositoryId { get; init; }
    public required PreparedSourceOperation Operation { get; init; }
    public required PreparedSourceOutcome Outcome { get; init; }
    public IReadOnlyList<PreparedSourceEntry> Entries { get; init; } = [];
    public PreparedSourceReadLocation? ReadLocation { get; init; }
    public string? Text { get; init; }
    public long? ObservedFileByteLength { get; init; }
    public IReadOnlyList<PreparedSourceMatch> Matches { get; init; } = [];
    public string? Continuation { get; init; }
    public long? NextByteOffset { get; init; }
    public PreparedSourceStoppingLimit? StoppingLimit { get; init; }
    public IReadOnlyList<PreparedSourceLimitation> Limitations { get; init; } = [];
}

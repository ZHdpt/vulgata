namespace Vulgata.Core.DomainServices;

/// <summary>
/// A binding-scoped native CodeGraph session. Trusted composition owns its lifetime;
/// Worker functions only borrow it and never select a physical workspace.
/// </summary>
public interface ICodeGraphWorkerToolSession : IAsyncDisposable
{
    Task<CodeGraphToolReadiness> DiscoverAsync(CancellationToken cancellationToken);

    Task<CodeGraphToolInvocationResult> ExploreAsync(
        string query,
        int maxFiles,
        CancellationToken cancellationToken);
}

public enum CodeGraphToolReadinessStatus
{
    Ready,
    Unavailable,
    Unprepared,
    PreparationFailed,
    ConnectionFailed,
    DiscoveryFailed,
    AuthorityDenied,
    AuthorityUnavailable,
    Expired,
    Cancelled,
}

public enum CodeGraphToolStage
{
    Preparation,
    Connection,
    Discovery,
    Invocation,
}

/// <summary>Stable, non-diagnostic reason for a failed or limited native step.</summary>
public enum CodeGraphToolIssueCode
{
    None,
    InstallationUnavailable,
    SourceUnprepared,
    PreparationFailed,
    ConnectionFailed,
    ToolMissing,
    SchemaChanged,
    InvocationFailed,
    AuthorityDenied,
    AuthorityUnavailable,
    SessionExpired,
    DeadlineExceeded,
    Cancelled,
    IncompleteIndex,
    ResultLimit,
    UncontainedSource,
}

/// <summary>Discovery state without native server text or physical paths.</summary>
public sealed record CodeGraphToolReadiness(
    CodeGraphToolReadinessStatus Status,
    Guid? RepositoryId = null,
    CodeGraphToolStage Stage = CodeGraphToolStage.Discovery,
    CodeGraphToolIssueCode IssueCode = CodeGraphToolIssueCode.None)
{
    public bool IsReady =>
        Status == CodeGraphToolReadinessStatus.Ready &&
        RepositoryId is { } repositoryId && repositoryId != Guid.Empty &&
        Stage == CodeGraphToolStage.Discovery &&
        IssueCode == CodeGraphToolIssueCode.None;
}

public enum CodeGraphToolInvocationStatus
{
    Complete,
    NoMatch,
    Limited,
    Denied,
    AuthorityUnavailable,
    Unavailable,
    Failed,
    TimedOut,
    Cancelled,
    Expired,
}

/// <summary>A bounded, verified excerpt at a Repository-relative source location.</summary>
public sealed record CodeGraphSourceExcerpt(
    PreparedSourceLocation Location,
    string Text,
    int? LineStart = null,
    int? LineEnd = null);

/// <summary>
/// Sanitized native-tool outcome. A producer supplies only approved Repository-relative
/// locations and bounded source text after its final authority and containment checks.
/// Text is an optional structural summary; Excerpts carry source snippets.
/// </summary>
public sealed record CodeGraphToolInvocationResult
{
    public required CodeGraphToolInvocationStatus Status { get; init; }
    public Guid? RepositoryId { get; init; }
    public CodeGraphToolIssueCode IssueCode { get; init; }
    public IReadOnlyList<CodeGraphToolIssueCode> Limitations { get; init; } = [];
    public string? Text { get; init; }
    public IReadOnlyList<PreparedSourceLocation> Locations { get; init; } = [];
    public IReadOnlyList<CodeGraphSourceExcerpt> Excerpts { get; init; } = [];
}

namespace Vulgata.Core.DomainServices;

/// <summary>
/// A borrowed, Repository-bound navigation capability. Its trusted caller owns
/// disposal after all Worker function calls have settled.
/// </summary>
public interface IPreparedJavaNavigationContext : IAsyncDisposable
{
    Guid RepositoryId { get; }

    Task<PreparedJavaNavigationResult> GetDefinitionAsync(
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken = default);

    Task<PreparedJavaNavigationResult> GetReferencesAsync(
        PreparedJavaNavigationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PreparedJavaNavigationRequest(
    string RelativePath,
    int Line,
    int Character,
    int MaximumLocations);

public sealed record PreparedJavaNavigationResult(
    Guid RepositoryId,
    PreparedJavaNavigationOperation Operation,
    PreparedJavaNavigationOutcome Outcome,
    IReadOnlyList<PreparedJavaLocation> Locations,
    IReadOnlyList<PreparedJavaLimitation> Limitations,
    PreparedJavaStoppingLimit? StoppingLimit);

public sealed record PreparedJavaLocation(
    PreparedSourceLocation Source,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter);

public enum PreparedJavaNavigationOperation
{
    Definition,
    References,
}

public enum PreparedJavaNavigationOutcome
{
    Observed,
    NoResultObserved,
    Limited,
    Denied,
    AuthorityUnavailable,
    ChangedInput,
    ConfigurationUnavailable,
    ReadinessFailed,
    ProtocolFailed,
    Cancelled,
    DeadlineExceeded,
}

public sealed record PreparedJavaLimitation(string Code, PreparedSourceLocation? Source);

public enum PreparedJavaStoppingLimit
{
    LocationLimit,
    ResponseBytes,
    Deadline,
}

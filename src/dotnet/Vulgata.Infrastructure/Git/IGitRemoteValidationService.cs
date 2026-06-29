namespace Vulgata.Infrastructure.Git;

public interface IGitRemoteValidationService
{
    Task<GitRemoteValidationResult> ValidateAsync(string gitUrl, CancellationToken cancellationToken = default);
}

public enum GitRemoteValidationStatus
{
    Reachable,
    AuthenticationRequired,
    Unreachable,
}

public sealed record GitRemoteValidationResult(GitRemoteValidationStatus Status, string Message)
{
    public static GitRemoteValidationResult Reachable { get; } =
        new(GitRemoteValidationStatus.Reachable, "可达");

    public static GitRemoteValidationResult AuthenticationRequired(string message) =>
        new(GitRemoteValidationStatus.AuthenticationRequired, message);

    public static GitRemoteValidationResult Unreachable(string message) =>
        new(GitRemoteValidationStatus.Unreachable, message);
}

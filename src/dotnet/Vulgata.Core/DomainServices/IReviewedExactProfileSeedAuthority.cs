namespace Vulgata.Core.DomainServices;

public enum ReviewedExactProfileSeedAuthorityOutcome
{
    NonMatching,
    Matched,
    Unavailable,
}

public enum ReviewedExactProfileSeedRole
{
    Consumer,
    ProviderDeclaration,
    ProviderImplementation,
    UnsupportedObservationOwner,
}

public sealed record ReviewedExactProfileSeedRepositoryBinding
{
    public ReviewedExactProfileSeedRepositoryBinding(
        Guid repositoryId,
        string stableGitIdentity,
        string requestedRef,
        string profiledRevision)
    {
        if (repositoryId == Guid.Empty || string.IsNullOrWhiteSpace(stableGitIdentity) ||
            !IsSafeFullRef(requestedRef) || !IsLowercaseRevision(profiledRevision))
        {
            throw new ArgumentException("An exact reviewed-profile repository binding is required.");
        }

        RepositoryId = repositoryId;
        StableGitIdentity = stableGitIdentity;
        RequestedRef = requestedRef;
        ProfiledRevision = profiledRevision;
    }

    public Guid RepositoryId { get; }
    public string StableGitIdentity { get; }
    public string RequestedRef { get; }
    public string ProfiledRevision { get; }

    private static bool IsSafeFullRef(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.StartsWith("refs/", StringComparison.Ordinal) &&
        value.Length <= 512 &&
        !value.Contains("..", StringComparison.Ordinal) &&
        value.All(character => !char.IsControl(character) && !char.IsWhiteSpace(character));

    private static bool IsLowercaseRevision(string value) =>
        value is { Length: 40 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public sealed record ReviewedExactProfileSeedRequest
{
    public ReviewedExactProfileSeedRequest(
        IEnumerable<ReviewedExactProfileSeedRepositoryBinding> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ReviewedExactProfileSeedRepositoryBinding[] copy = repositories.ToArray();
        if (copy.Length != 2 || copy.Any(value => value is null) ||
            copy.Select(value => value.RepositoryId).Distinct().Count() != 2 ||
            copy.Select(value => value.StableGitIdentity).Distinct(StringComparer.Ordinal).Count() != 2)
        {
            throw new ArgumentException("Exactly two distinct reviewed-profile repository bindings are required.", nameof(repositories));
        }

        Repositories = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<ReviewedExactProfileSeedRepositoryBinding> Repositories { get; }
}

public sealed record ReviewedExactProfileRequiredSource
{
    public ReviewedExactProfileRequiredSource(
        Guid repositoryId,
        string relativePath,
        IEnumerable<ReviewedExactProfileSeedRole> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ReviewedExactProfileSeedRole[] copy = roles.Distinct().Order().ToArray();
        if (repositoryId == Guid.Empty || string.IsNullOrWhiteSpace(relativePath) || copy.Length == 0)
        {
            throw new ArgumentException("A reviewed-profile required source must be fully bound.");
        }

        RepositoryId = repositoryId;
        RelativePath = AnalysisContractGuard.RequireRelativePath(relativePath);
        Roles = Array.AsReadOnly(copy);
    }

    public Guid RepositoryId { get; }
    public string RelativePath { get; }
    public IReadOnlyList<ReviewedExactProfileSeedRole> Roles { get; }
}

public sealed record ReviewedExactProfileSeedAuthorityResult
{
    public ReviewedExactProfileSeedAuthorityResult(
        ReviewedExactProfileSeedAuthorityOutcome outcome,
        IEnumerable<ReviewedExactProfileRequiredSource> requiredSources,
        string? profileId = null,
        string? profileFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(requiredSources);
        ReviewedExactProfileRequiredSource[] copy = requiredSources.ToArray();
        if ((outcome == ReviewedExactProfileSeedAuthorityOutcome.Matched) !=
                (copy.Length > 0 && profileId is not null && profileFingerprint is not null) ||
            (outcome != ReviewedExactProfileSeedAuthorityOutcome.Matched &&
                (copy.Length != 0 || profileId is not null || profileFingerprint is not null)))
        {
            throw new ArgumentException("The reviewed-profile authority result is inconsistent.");
        }

        Outcome = outcome;
        RequiredSources = Array.AsReadOnly(copy);
        ProfileId = profileId;
        ProfileFingerprint = profileFingerprint;
    }

    public ReviewedExactProfileSeedAuthorityOutcome Outcome { get; }
    public IReadOnlyList<ReviewedExactProfileRequiredSource> RequiredSources { get; }
    public string? ProfileId { get; }
    public string? ProfileFingerprint { get; }
}

public interface IReviewedExactProfileSeedAuthority
{
    ReviewedExactProfileSeedAuthorityResult Evaluate(ReviewedExactProfileSeedRequest request);
}

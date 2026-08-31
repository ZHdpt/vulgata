using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.CrossRepository;

public sealed class ReviewedExactProfileSeedAuthority(ExactResolutionProfileCatalog catalog)
    : IReviewedExactProfileSeedAuthority
{
    public ReviewedExactProfileSeedAuthorityResult Evaluate(ReviewedExactProfileSeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ExactResolutionProfile profile;
        try
        {
            profile = catalog.Load();
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return Empty(ReviewedExactProfileSeedAuthorityOutcome.Unavailable);
        }

        Dictionary<string, ReviewedExactProfileSeedRepositoryBinding> bindings = request.Repositories
            .ToDictionary(value => value.StableGitIdentity, StringComparer.Ordinal);
        if (profile.Repositories.Count != request.Repositories.Count ||
            profile.Repositories.Any(repository =>
                !bindings.TryGetValue(repository.StableGitIdentity, out ReviewedExactProfileSeedRepositoryBinding? binding) ||
                !StringComparer.Ordinal.Equals(binding.RequestedRef, repository.ExpectedRef) ||
                !StringComparer.Ordinal.Equals(binding.ProfiledRevision, repository.ExpectedCommit)))
        {
            return Empty(ReviewedExactProfileSeedAuthorityOutcome.NonMatching);
        }

        var required = new Dictionary<(Guid RepositoryId, string RelativePath), HashSet<ReviewedExactProfileSeedRole>>();
        foreach (ResolutionOccurrence occurrence in profile.Occurrences)
        {
            Guid consumer = RepositoryId(profile, bindings, occurrence.ConsumerAlias);
            Guid provider = RepositoryId(profile, bindings, occurrence.ProviderAlias);
            Add(required, consumer, occurrence.Consumer.Files, ReviewedExactProfileSeedRole.Consumer);
            Add(required, provider, occurrence.Provider.Files, ReviewedExactProfileSeedRole.ProviderDeclaration);
            Add(required, provider, [occurrence.Provider.ImplementationDocument.RepositoryRelativePath],
                ReviewedExactProfileSeedRole.ProviderImplementation);
        }

        foreach (UnsupportedObservation observation in profile.UnsupportedObservations)
        {
            Add(required, RepositoryId(profile, bindings, observation.OwnerAlias),
                [observation.OwnerDocument.RepositoryRelativePath],
                ReviewedExactProfileSeedRole.UnsupportedObservationOwner);
        }

        ReviewedExactProfileRequiredSource[] sources = required
            .OrderBy(value => value.Key.RepositoryId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(value => value.Key.RelativePath, StringComparer.Ordinal)
            .Select(value => CreateRequiredSource(profile, bindings, value.Key, value.Value))
            .ToArray();
        return new(
            ReviewedExactProfileSeedAuthorityOutcome.Matched,
            sources,
            profile.ProfileId,
            profile.ProfileFingerprint);
    }

    private static ReviewedExactProfileRequiredSource CreateRequiredSource(
        ExactResolutionProfile profile,
        IReadOnlyDictionary<string, ReviewedExactProfileSeedRepositoryBinding> bindings,
        (Guid RepositoryId, string RelativePath) key,
        IReadOnlyCollection<ReviewedExactProfileSeedRole> roles)
    {
        UnsupportedObservation[] owners = profile.UnsupportedObservations
            .Where(observation =>
                RepositoryId(profile, bindings, observation.OwnerAlias) == key.RepositoryId &&
                StringComparer.Ordinal.Equals(
                    observation.OwnerDocument.RepositoryRelativePath,
                    key.RelativePath))
            .ToArray();
        if (owners.Length == 0)
        {
            return new(key.RepositoryId, key.RelativePath, roles);
        }

        if (owners.Length != 1)
        {
            throw new InvalidDataException("The reviewed semantic owner is ambiguous.");
        }

        UnsupportedObservation owner = owners[0];
        return new(
            key.RepositoryId,
            key.RelativePath,
            roles,
            owner.OwnerDocument.Symbol,
            owner.Selector.Line);
    }

    private static Guid RepositoryId(
        ExactResolutionProfile profile,
        IReadOnlyDictionary<string, ReviewedExactProfileSeedRepositoryBinding> bindings,
        string alias) => bindings[profile.Repositories.Single(value =>
            StringComparer.Ordinal.Equals(value.Alias, alias)).StableGitIdentity].RepositoryId;

    private static void Add(
        IDictionary<(Guid RepositoryId, string RelativePath), HashSet<ReviewedExactProfileSeedRole>> result,
        Guid repositoryId,
        IEnumerable<string> paths,
        ReviewedExactProfileSeedRole role)
    {
        foreach (string path in paths)
        {
            var key = (repositoryId, path);
            if (!result.TryGetValue(key, out HashSet<ReviewedExactProfileSeedRole>? roles))
            {
                roles = [];
                result.Add(key, roles);
            }

            roles.Add(role);
        }
    }

    private static ReviewedExactProfileSeedAuthorityResult Empty(
        ReviewedExactProfileSeedAuthorityOutcome outcome) => new(outcome, []);
}

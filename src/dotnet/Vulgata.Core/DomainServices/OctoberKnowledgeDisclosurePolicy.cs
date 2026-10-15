using System.Collections.Immutable;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>Trusted persisted selection facts, never supplied by a browser or model.</summary>
public sealed record KnowledgeRepositoryBinding(Guid RepositoryId, Guid? SystemId, string RootBindingIdentity);

public sealed record KnowledgeAdvisoryBinding(Guid RepositoryId, ContributorKind Kind, Guid RecordId, string ContentDigest);

public sealed record KnowledgeSelectionAuthoritySnapshot(
    PublicationBinding Publication,
    ImmutableArray<KnowledgeRepositoryBinding> Repositories,
    ImmutableArray<KnowledgeAdvisoryBinding> Advisory);

public interface IOctoberKnowledgeSelectionAuthority
{
    Task<KnowledgeSelectionAuthoritySnapshot?> LoadAsync(PublicationBinding publication, CancellationToken cancellationToken = default);
}

/// <summary>Fresh host authentication and existing management records, without producer authority.</summary>
public interface IOctoberKnowledgeLiveReadAuthority
{
    Task<DisclosureStatus> CheckAsync(KnowledgeSubject subject, KnowledgeSelectionAuthoritySnapshot snapshot,
        ImmutableArray<Guid> requestedScope, ImmutableArray<Contributor> allContributors,
        CancellationToken cancellationToken = default);
}

public sealed class OctoberKnowledgeDisclosurePolicy(
    IOctoberKnowledgeLiveReadAuthority liveAuthority,
    IOctoberKnowledgeSelectionAuthority selectionAuthority) : IOctoberKnowledgeDisclosurePolicy
{
    private readonly IOctoberKnowledgeLiveReadAuthority _liveAuthority = liveAuthority ?? throw new ArgumentNullException(nameof(liveAuthority));
    private readonly IOctoberKnowledgeSelectionAuthority _selectionAuthority = selectionAuthority ?? throw new ArgumentNullException(nameof(selectionAuthority));

    public async Task<DisclosureStatus> CheckAsync(DisclosureRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request?.Selection is not { } selection || selection.Subject is not { } subject ||
            string.IsNullOrWhiteSpace(subject.SubjectId) || string.IsNullOrWhiteSpace(subject.AuthenticationBinding) ||
            selection.Publication is null || !Enum.IsDefined(selection.Mode) ||
            selection.Purpose is not (KnowledgePurpose.Reader or KnowledgePurpose.Chat or KnowledgePurpose.Graph) ||
            selection.RepositoryScope.IsDefault || selection.RepositoryScope.Any(x => x == Guid.Empty) ||
            selection.RepositoryScope.Distinct().Count() != selection.RepositoryScope.Length ||
            request.AllContributors.IsDefault || request.Kind is { } kind && !Enum.IsDefined(kind) ||
            request.Kind is not null && request.AllContributors.IsEmpty ||
            selection.Mode == KnowledgeMode.Business && request.Kind == DocumentKind.CodeLogic)
            return DisclosureStatus.Denied;

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var authorityToken = deadline.Token;
        try
        {
            var snapshot = await _selectionAuthority.LoadAsync(selection.Publication, authorityToken).WaitAsync(authorityToken);
            authorityToken.ThrowIfCancellationRequested();
            if (snapshot is null || snapshot.Publication != selection.Publication || snapshot.Repositories.IsDefault ||
                snapshot.Advisory.IsDefault || snapshot.Repositories.Any(x => x is null || x.RepositoryId == Guid.Empty ||
                                                                              x.SystemId == Guid.Empty || string.IsNullOrWhiteSpace(x.RootBindingIdentity)) ||
                snapshot.Repositories.Select(x => x.RepositoryId).Distinct().Count() != snapshot.Repositories.Length)
                return DisclosureStatus.Denied;

            var repositories = snapshot.Repositories.ToDictionary(x => x.RepositoryId);
            var scope = selection.RepositoryScope.ToHashSet();
            if (scope.Any(x => !repositories.ContainsKey(x)) ||
                request.AllContributors.Any(x => !ContributorMatches(x, scope, repositories, snapshot.Advisory)))
                return DisclosureStatus.Denied;

            // Empty scope is an explicit cleared selection; it never expands to all repositories.
            var decision = await _liveAuthority.CheckAsync(subject, snapshot, selection.RepositoryScope,
                request.AllContributors, authorityToken).WaitAsync(authorityToken);
            authorityToken.ThrowIfCancellationRequested();
            return Enum.IsDefined(decision) ? decision : DisclosureStatus.AuthorityUnavailable;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return DisclosureStatus.AuthorityUnavailable;
        }
    }

    private static bool ContributorMatches(Contributor contributor, HashSet<Guid> scope,
        Dictionary<Guid, KnowledgeRepositoryBinding> repositories, ImmutableArray<KnowledgeAdvisoryBinding> advisory)
    {
        if (contributor is null || contributor.ContentDigest is not { Length: 64 } digest || !digest.All(char.IsAsciiHexDigit))
            return false;
        KnowledgeRepositoryBinding? repository = null;
        if (contributor.RepositoryId is { } repositoryId &&
            (!scope.Contains(repositoryId) || !repositories.TryGetValue(repositoryId, out repository) ||
             contributor.SystemId is { } systemId && repository.SystemId != systemId))
            return false;

        if (contributor.Kind is ContributorKind.Source or ContributorKind.SourceMetadata)
        {
            bool shape = contributor.Kind == ContributorKind.Source
                ? OctoberKnowledgeInputPolicy.IsValidSourceContributor(contributor)
                : OctoberKnowledgeInputPolicy.IsValidMetadataContributor(contributor);
            return shape && repository is not null && contributor.RootBindingIdentity == repository.RootBindingIdentity;
        }

        if (contributor.SourceOrigin is not null || contributor.MetadataOrigin is not null) return false;

        if (contributor.AdvisoryRecordId is not { } recordId || recordId == Guid.Empty || contributor.RootBindingIdentity is not null)
            return false;
        bool validShape = contributor.Kind switch
        {
            ContributorKind.GlobalAdvisory => contributor.RepositoryId is null && contributor.SystemId is null,
            ContributorKind.SystemAdvisory => contributor.RepositoryId is null && contributor.SystemId == recordId,
            ContributorKind.RepositoryAdvisory => repository is not null && contributor.RepositoryId == recordId,
            _ => false
        };
        if (!validShape) return false;

        var selected = advisory.Where(x => x is not null && scope.Contains(x.RepositoryId) &&
                                           x.Kind == contributor.Kind && x.RecordId == recordId &&
                                           (contributor.Kind != ContributorKind.RepositoryAdvisory || x.RepositoryId == contributor.RepositoryId) &&
                                           (contributor.Kind != ContributorKind.SystemAdvisory ||
                                            repositories.TryGetValue(x.RepositoryId, out var bound) && bound.SystemId == contributor.SystemId)).ToArray();
        // One publication can retain several accepted revisions of the same advisory.
        return selected.Any(x => x.ContentDigest == contributor.ContentDigest);
    }
}

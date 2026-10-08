using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Infrastructure.Data.October.Knowledge;

namespace Vulgata.Web.Data;

public sealed partial class OctoberLocalAuthority
{
    /// <summary>Bind a retained publication to this issuer's original authenticated operator session.</summary>
    public async Task<OctoberOperatorReaderBinding?> TryBindReaderAsync(IOctoberOperatorSession session,
        PublicationBinding publication, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (session is not OperatorSession current || !current.IsCurrentFor(this) ||
            publication?.Knowledge is not { } binding || publication.PublicationId == Guid.Empty ||
            binding.TargetId != _configuration.TargetId || binding.BatchId != _configuration.BatchId ||
            binding.CanonicalDatabaseIdentity != _configuration.CanonicalDatabaseIdentity ||
            binding.SelectionDigest is not { Length: 64 } || !binding.SelectionDigest.All(char.IsAsciiHexDigit))
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_configuration.OperationTimeout);
        AsyncServiceScope resources = default;
        bool ownsResources = false;
        try
        {
            var authorized = await AuthorizeSelectionAsync(current, new(binding.TargetId, binding.BatchId, null), timeout.Token);
            if (!MatchesReaderSelection(authorized, current, binding)) return null;
            resources = _scopes.CreateAsyncScope();
            ownsResources = true;
            var services = resources.ServiceProvider;
            var contexts = services.GetService<IDbContextFactory<OctoberDbContext>>();
            var protector = services.GetService<IOctoberKnowledgeTokenProtector>();
            var log = services.GetService<IOctoberKnowledgeReadLog>();
            var claims = services.GetService<IOptions<IdentityOptions>>()?.Value.ClaimsIdentity;
            if (contexts is null || protector is null || log is null || claims is null) return null;
            var subject = new KnowledgeSubject(current.SubjectId, current.OriginalSecurityStamp);
            var selection = new ReadSelectionRequest(publication, subject, KnowledgeMode.IT, KnowledgePurpose.Reader,
                authorized.Authority!.RepositoryIds.Order().ToImmutableArray());
            var selections = new OctoberKnowledgeSelectionAuthority(contexts);
            var snapshot = await selections.LoadAsync(publication, timeout.Token);
            if (snapshot is null || snapshot.Publication != publication ||
                !snapshot.Repositories.Select(repository => repository.RepositoryId).Order().SequenceEqual(selection.RepositoryScope))
                return null;
            authorized = await AuthorizeSelectionAsync(current, new(binding.TargetId, binding.BatchId, null), timeout.Token);
            if (!MatchesReaderSelection(authorized, current, binding)) return null;
            timeout.Token.ThrowIfCancellationRequested();
            if (!current.IsCurrentFor(this)) return null;
            var result = new OctoberOperatorReaderBinding(subject, selection, resources, isOpen =>
            {
                var live = new OctoberKnowledgeIdentityAdapter(_scopes, async token =>
                {
                    if (!isOpen() || !current.IsCurrentFor(this)) return null;
                    var latest = await AuthorizeSelectionAsync(current, new(binding.TargetId, binding.BatchId, null), token);
                    token.ThrowIfCancellationRequested();
                    if (!isOpen() || !MatchesReaderSelection(latest, current, binding)) return null;
                    // No refreshed stamp or role claim can renew the original authentication.
                    return new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(claims.UserIdClaimType, subject.SubjectId),
                        new Claim(claims.SecurityStampClaimType, subject.AuthenticationBinding)
                    ], "OctoberOriginalOperator"));
                }, _configuration.OperationTimeout);
                return new OctoberKnowledgeReader(new OctoberKnowledgePublishedSource(contexts), publication,
                    new OctoberKnowledgeDisclosurePolicy(live, selections),
                    new OctoberKnowledgeCursorProtector(protector, _clock), log);
            });
            ownsResources = false;
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return null; }
        finally { if (ownsResources) await resources.DisposeAsync(); }
    }

    private bool MatchesReaderSelection(OctoberAuthoritySelectionResult result, OperatorSession current, KnowledgeBinding binding) =>
        current.IsCurrentFor(this) && result.Status == OctoberAuthorityDecisionStatus.Authorized &&
        result.Request is { } request && result.Authority is { } authority &&
        request.TargetId == binding.TargetId && request.BatchId == binding.BatchId && request.SelectionDigest == binding.SelectionDigest &&
        authority.SubjectId == current.SubjectId && authority.TargetId == binding.TargetId && authority.BatchId == binding.BatchId &&
        authority.CanonicalDatabaseIdentity == binding.CanonicalDatabaseIdentity && authority.SelectionDigest == binding.SelectionDigest &&
        authority.SelectedContext?.ManagementAuthorityId == current.ManagementAuthorityId;
}

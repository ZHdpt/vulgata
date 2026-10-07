using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

/// <summary>Uses an explicit trusted host principal accessor and fresh Identity/management scopes.</summary>
public sealed class OctoberKnowledgeIdentityAdapter : IOctoberKnowledgeLiveReadAuthority
{
    private readonly IServiceScopeFactory _scopes;
    private readonly Func<CancellationToken, Task<ClaimsPrincipal?>> _trustedCurrentPrincipal;
    private readonly TimeSpan _operationTimeout;

    public OctoberKnowledgeIdentityAdapter(IServiceScopeFactory scopes,
        Func<CancellationToken, Task<ClaimsPrincipal?>> trustedCurrentPrincipal, TimeSpan? operationTimeout = null)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _trustedCurrentPrincipal = trustedCurrentPrincipal ?? throw new ArgumentNullException(nameof(trustedCurrentPrincipal));
        _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(10);
        if (_operationTimeout <= TimeSpan.Zero || _operationTimeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(operationTimeout));
    }

    public async Task<DisclosureStatus> CheckAsync(KnowledgeSubject subject, KnowledgeSelectionAuthoritySnapshot snapshot,
        ImmutableArray<Guid> requestedScope, ImmutableArray<Contributor> allContributors,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (subject is null || string.IsNullOrWhiteSpace(subject.SubjectId) || string.IsNullOrWhiteSpace(subject.AuthenticationBinding) ||
            snapshot is null || snapshot.Repositories.IsDefault || requestedScope.IsDefault || allContributors.IsDefault)
            return DisclosureStatus.Denied;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_operationTimeout);
        var token = timeout.Token;
        try
        {
            var principal = await _trustedCurrentPrincipal(token).WaitAsync(token);
            var identities = principal?.Identities.Where(x => x.IsAuthenticated).ToArray();
            if (identities is not { Length: 1 }) return DisclosureStatus.Denied;
            await using var scope = _scopes.CreateAsyncScope();
            var claims = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity;
            // Take both bindings from the same authenticated identity, never another cached identity.
            if (identities[0].FindFirst(claims.UserIdClaimType)?.Value != subject.SubjectId ||
                identities[0].FindFirst(claims.SecurityStampClaimType)?.Value != subject.AuthenticationBinding)
                return DisclosureStatus.Denied;

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(subject.SubjectId).WaitAsync(token);
            if (user is null || !await signIn.CanSignInAsync(user).WaitAsync(token) ||
                await users.IsLockedOutAsync(user).WaitAsync(token) ||
                await users.GetSecurityStampAsync(user).WaitAsync(token) != subject.AuthenticationBinding)
                return DisclosureStatus.Denied;
            var roles = await users.GetRolesAsync(user).WaitAsync(token);
            if (!roles.Any(x => x is RoleNames.Administrator or RoleNames.SystemOwner or RoleNames.User))
                return DisclosureStatus.Denied;

            var repositories = snapshot.Repositories.ToDictionary(x => x.RepositoryId);
            if (requestedScope.Any(x => !repositories.ContainsKey(x))) return DisclosureStatus.Denied;
            if (requestedScope.IsEmpty && allContributors.IsEmpty)
            {
                token.ThrowIfCancellationRequested();
                return DisclosureStatus.Allowed;
            }

            await using var management = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<VulgataDbContext>>().CreateDbContextAsync(token);
            foreach (var repositoryId in requestedScope)
            {
                var bound = repositories[repositoryId];
                var current = await management.Repositories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == repositoryId, token);
                // Omitted contributor SystemId cannot bypass the persisted association, including standalone.
                if (current is null || current.SystemId != bound.SystemId || bound.SystemId is { } systemId &&
                    !await management.Systems.AsNoTracking().AnyAsync(x => x.Id == systemId, token))
                    return DisclosureStatus.Denied;
            }
            foreach (var contributor in allContributors)
            {
                if (contributor.Kind == ContributorKind.Source) continue;
                if (contributor.AdvisoryRecordId is not { } recordId) return DisclosureStatus.Denied;
                string? liveContext = contributor.Kind switch
                {
                    ContributorKind.GlobalAdvisory => await management.GlobalContexts.AsNoTracking()
                        .Where(x => x.Id == recordId).Select(x => x.Context).SingleOrDefaultAsync(token),
                    ContributorKind.SystemAdvisory => await management.Systems.AsNoTracking()
                        .Where(x => x.Id == recordId).Select(x => x.Context).SingleOrDefaultAsync(token),
                    ContributorKind.RepositoryAdvisory => await management.Repositories.AsNoTracking()
                        .Where(x => x.Id == recordId).Select(x => x.Context).SingleOrDefaultAsync(token),
                    _ => null
                };
                // Clearing/removing context revokes it. Nonempty edits do not rewrite the historical snapshot.
                if (string.IsNullOrWhiteSpace(liveContext)) return DisclosureStatus.Denied;
            }
            token.ThrowIfCancellationRequested();
            return DisclosureStatus.Allowed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return DisclosureStatus.AuthorityUnavailable; }
    }
}

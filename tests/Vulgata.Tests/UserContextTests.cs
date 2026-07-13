using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Web.RequestPipeline;

namespace Vulgata.Tests;

/// <summary>
/// Unit tests for the UserContext adapter and SystemOwnerFilter record.
/// These pin the three visibility rules (admin sees all, owner sees own, filter shape)
/// that previously lived inline in 32 endpoint call sites.
/// </summary>
public sealed class UserContextTests
{
    private const string TestUserId = "user-123";

    [Fact]
    public void SystemOwnerFilter_AllSystems_AllowsAllAndHasNoOwnerId()
    {
        SystemOwnerFilter filter = SystemOwnerFilter.AllSystems();

        Assert.True(filter.AllowAll);
        Assert.Null(filter.OwnerId);
    }

    [Fact]
    public void SystemOwnerFilter_OwnedBy_HasUserIdAndDoesNotAllowAll()
    {
        SystemOwnerFilter filter = SystemOwnerFilter.OwnedBy(TestUserId);

        Assert.False(filter.AllowAll);
        Assert.Equal(TestUserId, filter.OwnerId);
    }

    [Fact]
    public async Task UserContext_AdministratorPrincipal_YieldsIsAdministratorTrueAndAllSystemsFilter()
    {
        ClaimsPrincipal principal = BuildPrincipal(TestUserId, authenticated: true);
        IUserContext userContext = new UserContext(principal, new AlwaysSucceedAuthorizationService());

        bool isAdministrator = userContext.IsAdministrator;

        Assert.True(isAdministrator);
        Assert.Equal(TestUserId, userContext.UserId);

        SystemOwnerFilter filter = userContext.ToSystemOwnerFilter();
        Assert.True(filter.AllowAll);
        Assert.Null(filter.OwnerId);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task UserContext_NonAdministratorPrincipal_YieldsIsAdministratorFalseAndOwnedByFilter()
    {
        ClaimsPrincipal principal = BuildPrincipal(TestUserId, authenticated: true);
        IUserContext userContext = new UserContext(principal, new AlwaysFailAuthorizationService());

        bool isAdministrator = userContext.IsAdministrator;

        Assert.False(isAdministrator);
        Assert.Equal(TestUserId, userContext.UserId);

        SystemOwnerFilter filter = userContext.ToSystemOwnerFilter();
        Assert.False(filter.AllowAll);
        Assert.Equal(TestUserId, filter.OwnerId);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task UserContext_MissingNameIdentifierClaim_YieldsEmptyUserId()
    {
        ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.User)],
            authenticationType: "Test"));

        IUserContext userContext = new UserContext(principal, new AlwaysFailAuthorizationService());

        Assert.Equal(string.Empty, userContext.UserId);

        await Task.CompletedTask;
    }

    private static ClaimsPrincipal BuildPrincipal(string userId, bool authenticated)
    {
        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
        ];

        string? authenticationType = authenticated ? "Test" : null;
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
    }

    private sealed class AlwaysSucceedAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    private sealed class AlwaysFailAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}

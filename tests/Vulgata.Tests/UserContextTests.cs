using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
    private const string _testUserId = "user-123";

    [Fact]
    public void SystemOwnerFilter_AllSystems_AllowsAllAndHasNoOwnerId()
    {
        var filter = SystemOwnerFilter.AllSystems();

        Assert.True(filter.AllowAll);
        Assert.Null(filter.OwnerId);
    }

    [Fact]
    public void SystemOwnerFilter_OwnedBy_HasUserIdAndDoesNotAllowAll()
    {
        var filter = SystemOwnerFilter.OwnedBy(_testUserId);

        Assert.False(filter.AllowAll);
        Assert.Equal(_testUserId, filter.OwnerId);
    }

    [Fact]
    public async Task UserContext_AdministratorPrincipal_YieldsIsAdministratorTrueAndAllSystemsFilter()
    {
        var principal = BuildPrincipal(_testUserId, authenticated: true);
        IUserContext userContext = new UserContext(principal, new AlwaysSucceedAuthorizationService());

        bool isAdministrator = userContext.IsAdministrator;

        Assert.True(isAdministrator);
        Assert.Equal(_testUserId, userContext.UserId);

        var filter = userContext.ToSystemOwnerFilter();
        Assert.True(filter.AllowAll);
        Assert.Null(filter.OwnerId);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task UserContext_NonAdministratorPrincipal_YieldsIsAdministratorFalseAndOwnedByFilter()
    {
        var principal = BuildPrincipal(_testUserId, authenticated: true);
        IUserContext userContext = new UserContext(principal, new AlwaysFailAuthorizationService());

        bool isAdministrator = userContext.IsAdministrator;

        Assert.False(isAdministrator);
        Assert.Equal(_testUserId, userContext.UserId);

        var filter = userContext.ToSystemOwnerFilter();
        Assert.False(filter.AllowAll);
        Assert.Equal(_testUserId, filter.OwnerId);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task UserContext_MissingNameIdentifierClaim_YieldsEmptyUserId()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.User)],
            authenticationType: "Test"));

        IUserContext userContext = new UserContext(principal, new AlwaysFailAuthorizationService());

        Assert.Equal(string.Empty, userContext.UserId);

        await Task.CompletedTask;
    }

    [Fact]
    public void UserContext_AsyncAuthorizationUnderRendererContext_DoesNotDeadlock()
    {
        using var completed = new ManualResetEventSlim();
        Exception? failure = null;
        bool isAdministrator = false;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
            try
            {
                var principal = BuildPrincipal(_testUserId, authenticated: true);
                IUserContext userContext = new UserContext(
                    principal,
                    new YieldingAuthorizationService());
                isAdministrator = userContext.IsAdministrator;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                completed.Set();
            }
        })
        {
            IsBackground = true,
        };

        thread.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
        Assert.Null(failure);
        Assert.True(isAdministrator);
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

    private sealed class YieldingAuthorizationService : IAuthorizationService
    {
        public async Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            await Task.Yield();
            return AuthorizationResult.Success();
        }

        public async Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            string policyName)
        {
            await Task.Yield();
            return AuthorizationResult.Success();
        }
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
        }
    }
}

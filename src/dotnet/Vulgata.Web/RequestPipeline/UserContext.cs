using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;

namespace Vulgata.Web.RequestPipeline;

/// <summary>
/// Adapter that resolves <see cref="IUserContext"/> from the current <see cref="ClaimsPrincipal"/>
/// and the <c>AdministratorOnly</c> authorization policy. Registered as scoped; one instance per HTTP request.
/// For Blazor Server interactive circuits (where <see cref="IHttpContextAccessor"/> is null), use the
/// <see cref="UserContext(ClaimsPrincipal, bool)"/> constructor with a pre-resolved administrator flag
/// from <see cref="ClaimsPrincipal.IsInRole"/>.
/// </summary>
public sealed class UserContext : IUserContext
{
    private readonly ClaimsPrincipal _principal;
    private readonly IAuthorizationService? _authorization;
    private readonly Lazy<string> _userIdLazy;
    private readonly Lazy<Task<bool>> _isAdministratorLazy;

    public UserContext(ClaimsPrincipal principal, IAuthorizationService authorization)
    {
        _principal = principal;
        _authorization = authorization;
        _userIdLazy = new Lazy<string>(() => _principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        _isAdministratorLazy = new Lazy<Task<bool>>(IsAdministratorCoreAsync);
    }

    /// <summary>
    /// Constructs a <see cref="UserContext"/> with a pre-resolved administrator flag.
    /// Use this in Blazor Server circuits where <see cref="IHttpContextAccessor"/> is null
    /// and the administrator role is already available on the <see cref="ClaimsPrincipal"/> via
    /// the revalidating <see cref="AuthenticationStateProvider"/>.
    /// </summary>
    public UserContext(ClaimsPrincipal principal, bool isAdministrator)
    {
        _principal = principal;
        _authorization = null;
        _userIdLazy = new Lazy<string>(() => _principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        _isAdministratorLazy = new Lazy<Task<bool>>(() => Task.FromResult(isAdministrator));
    }

    public string UserId => _userIdLazy.Value;

    public bool IsAdministrator => _isAdministratorLazy.Value.IsCompleted
        ? _isAdministratorLazy.Value.Result
        : _isAdministratorLazy.Value.GetAwaiter().GetResult();

    public SystemOwnerFilter ToSystemOwnerFilter() => IsAdministrator
        ? SystemOwnerFilter.AllSystems()
        : SystemOwnerFilter.OwnedBy(UserId);

    private async Task<bool> IsAdministratorCoreAsync()
    {
        AuthorizationResult result = await _authorization.AuthorizeAsync(_principal, AuthorizationPolicyNames.AdministratorOnly);
        return result.Succeeded;
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;

namespace Vulgata.Web.RequestPipeline;

/// <summary>
/// Adapter that resolves <see cref="IUserContext"/> from the current <see cref="ClaimsPrincipal"/>
/// and the <c>AdministratorOnly</c> authorization policy. Registered as scoped; one instance per HTTP request.
/// </summary>
public sealed class UserContext : IUserContext
{
    private readonly ClaimsPrincipal _principal;
    private readonly IAuthorizationService _authorization;
    private readonly Lazy<string> _userIdLazy;
    private readonly Lazy<Task<bool>> _isAdministratorLazy;

    public UserContext(ClaimsPrincipal principal, IAuthorizationService authorization)
    {
        _principal = principal;
        _authorization = authorization;
        _userIdLazy = new Lazy<string>(() => _principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        _isAdministratorLazy = new Lazy<Task<bool>>(IsAdministratorCoreAsync);
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

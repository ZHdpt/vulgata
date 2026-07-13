namespace Vulgata.Core.DomainServices;

/// <summary>
/// Resolves the authenticated user's identity and administrator status once per request.
/// Replaces the per-endpoint AuthorizeAsync + FindFirstValue pattern that was duplicated 32 times.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// The authenticated user's Identity identifier. Empty string if unauthenticated.
    /// </summary>
    string UserId { get; }

    /// <summary>
    /// True if the user satisfies the <c>AdministratorOnly</c> authorization policy.
    /// </summary>
    bool IsAdministrator { get; }

    /// <summary>
    /// Builds the visibility filter used by System and Repository queries.
    /// Administrators see all systems; non-administrators see only systems they own.
    /// </summary>
    SystemOwnerFilter ToSystemOwnerFilter();
}

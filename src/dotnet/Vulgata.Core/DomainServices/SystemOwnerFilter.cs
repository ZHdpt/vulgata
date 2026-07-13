namespace Vulgata.Core.DomainServices;

/// <summary>
/// Captures the visibility rule for System and Repository aggregates.
/// When <see cref="AllowAll"/> is true, no ownership filter applies (administrator).
/// Otherwise only aggregates owned by <see cref="OwnerId"/> are visible.
/// Replaces the (string userId, bool isAdministrator) tuple duplicated across coordinators and repositories.
/// </summary>
public readonly record struct SystemOwnerFilter
{
    public string? OwnerId { get; }
    public bool AllowAll { get; }

    private SystemOwnerFilter(string? ownerId, bool allowAll)
    {
        OwnerId = ownerId;
        AllowAll = allowAll;
    }

    /// <summary>
    /// Administrator view: every System and Repository is visible regardless of ownership.
    /// </summary>
    public static SystemOwnerFilter AllSystems() => new(ownerId: null, allowAll: true);

    /// <summary>
    /// System Owner view: only aggregates owned by <paramref name="userId"/> are visible.
    /// </summary>
    public static SystemOwnerFilter OwnedBy(string userId) => new(ownerId: userId, allowAll: false);
}

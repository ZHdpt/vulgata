using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Shared.Systems;

namespace Vulgata.Web.Data;

public interface ISystemOwnershipCoordinator
{
    Task<IReadOnlyList<SystemOwnerAssignmentDto>> ListOwnersAsync(Guid systemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemOwnerCandidateDto>> ListOwnerCandidatesAsync(Guid systemId, string? keyword, CancellationToken cancellationToken = default);

    Task<SystemOwnershipAssignmentResult> AssignOwnerAsync(Guid systemId, string userId, CancellationToken cancellationToken = default);

    Task<SystemOwnershipRemovalResult> RemoveOwnerAsync(Guid systemId, string userId, CancellationToken cancellationToken = default);
}

public enum SystemOwnershipAssignmentOutcome
{
    Assigned,
    SystemNotFound,
    UserNotFound,
    UserIsAdministrator,
    AlreadyAssigned,
    Failed,
}

public sealed record SystemOwnershipAssignmentResult(SystemOwnershipAssignmentOutcome Outcome, IdentityResult? IdentityResult = null)
{
    public static SystemOwnershipAssignmentResult Assigned { get; } = new(SystemOwnershipAssignmentOutcome.Assigned);

    public static SystemOwnershipAssignmentResult SystemNotFound { get; } = new(SystemOwnershipAssignmentOutcome.SystemNotFound);

    public static SystemOwnershipAssignmentResult UserNotFound { get; } = new(SystemOwnershipAssignmentOutcome.UserNotFound);

    public static SystemOwnershipAssignmentResult UserIsAdministrator { get; } = new(SystemOwnershipAssignmentOutcome.UserIsAdministrator);

    public static SystemOwnershipAssignmentResult AlreadyAssigned { get; } = new(SystemOwnershipAssignmentOutcome.AlreadyAssigned);

    public static SystemOwnershipAssignmentResult Failed(IdentityResult identityResult) =>
        new(SystemOwnershipAssignmentOutcome.Failed, identityResult);
}

public enum SystemOwnershipRemovalOutcome
{
    Removed,
    SystemNotFound,
    AssignmentNotFound,
    Failed,
}

public sealed record SystemOwnershipRemovalResult(SystemOwnershipRemovalOutcome Outcome, IdentityResult? IdentityResult = null)
{
    public static SystemOwnershipRemovalResult Removed { get; } = new(SystemOwnershipRemovalOutcome.Removed);

    public static SystemOwnershipRemovalResult SystemNotFound { get; } = new(SystemOwnershipRemovalOutcome.SystemNotFound);

    public static SystemOwnershipRemovalResult AssignmentNotFound { get; } = new(SystemOwnershipRemovalOutcome.AssignmentNotFound);

    public static SystemOwnershipRemovalResult Failed(IdentityResult identityResult) =>
        new(SystemOwnershipRemovalOutcome.Failed, identityResult);
}

public sealed class SystemOwnershipCoordinator(
    ISystemRepository systemRepository,
    UserManager<ApplicationUser> userManager)
    : ISystemOwnershipCoordinator
{
    private static readonly SemaphoreSlim OwnershipMutationLock = new(1, 1);

    public async Task<IReadOnlyList<SystemOwnerAssignmentDto>> ListOwnersAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SystemOwnerAssignmentSummary> assignments = await systemRepository.ListOwnersAsync(systemId, cancellationToken);
        if (assignments.Count == 0)
        {
            return [];
        }

        HashSet<string> userIds = assignments
            .Select(assignment => assignment.UserId)
            .ToHashSet(StringComparer.Ordinal);

        List<ApplicationUser> users = await userManager.Users
            .Where(user => userIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        Dictionary<string, ApplicationUser> userLookup = users.ToDictionary(user => user.Id, StringComparer.Ordinal);

        return assignments
            .Select(assignment =>
            {
                if (userLookup.TryGetValue(assignment.UserId, out ApplicationUser? user))
                {
                    return new SystemOwnerAssignmentDto(
                        user.Id,
                        BuildDisplayName(user),
                        user.Email ?? user.UserName ?? string.Empty,
                        assignment.AssignedAt);
                }

                return new SystemOwnerAssignmentDto(
                    assignment.UserId,
                    $"已删除用户（{assignment.UserId}）",
                    string.Empty,
                    assignment.AssignedAt);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SystemOwnerCandidateDto>> ListOwnerCandidatesAsync(
        Guid systemId,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SystemOwnerAssignmentSummary> assignments = await systemRepository.ListOwnersAsync(systemId, cancellationToken);

        HashSet<string> assignedUserIds = assignments
            .Select(assignment => assignment.UserId)
            .ToHashSet(StringComparer.Ordinal);

        IList<ApplicationUser> administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
        HashSet<string> administratorUserIds = administrators
            .Select(user => user.Id)
            .ToHashSet(StringComparer.Ordinal);

        IQueryable<ApplicationUser> query = userManager.Users
            .Where(user => !assignedUserIds.Contains(user.Id) && !administratorUserIds.Contains(user.Id));

        string normalizedKeyword = (keyword ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            query = query.Where(user =>
                (user.Email ?? string.Empty).ToUpper().Contains(normalizedKeyword)
                || (user.UserName ?? string.Empty).ToUpper().Contains(normalizedKeyword)
                || (user.DisplayName ?? string.Empty).ToUpper().Contains(normalizedKeyword));
        }

        List<ApplicationUser> users = await query
            .OrderBy(user => user.Email)
            .ThenBy(user => user.UserName)
            .Take(50)
            .ToListAsync(cancellationToken);

        return users
            .Select(user => new SystemOwnerCandidateDto(
                user.Id,
                BuildDisplayName(user),
                user.Email ?? user.UserName ?? string.Empty))
            .ToList();
    }

    public async Task<SystemOwnershipAssignmentResult> AssignOwnerAsync(
        Guid systemId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserId = NormalizeUserId(userId);
        if (string.IsNullOrWhiteSpace(normalizedUserId))
        {
            return SystemOwnershipAssignmentResult.UserNotFound;
        }

        await OwnershipMutationLock.WaitAsync(cancellationToken);
        try
        {
            ApplicationUser? user = await userManager.FindByIdAsync(normalizedUserId);
            if (user is null)
            {
                return SystemOwnershipAssignmentResult.UserNotFound;
            }

            if (await userManager.IsInRoleAsync(user, RoleNames.Administrator))
            {
                return SystemOwnershipAssignmentResult.UserIsAdministrator;
            }

            SystemOwnerAssignmentWriteResult assignmentResult = await systemRepository.AssignOwnerAsync(
                systemId,
                normalizedUserId,
                DateTimeOffset.UtcNow,
                cancellationToken);

            if (assignmentResult.Outcome == SystemOwnerAssignmentWriteOutcome.SystemNotFound)
            {
                return SystemOwnershipAssignmentResult.SystemNotFound;
            }

            if (assignmentResult.Outcome == SystemOwnerAssignmentWriteOutcome.AlreadyAssigned)
            {
                return SystemOwnershipAssignmentResult.AlreadyAssigned;
            }

            await systemRepository.SaveChangesAsync(cancellationToken);

            if (!await userManager.IsInRoleAsync(user, RoleNames.SystemOwner))
            {
                IdentityResult addRoleResult = await userManager.AddToRoleAsync(user, RoleNames.SystemOwner);
                if (!addRoleResult.Succeeded)
                {
                    await systemRepository.RemoveOwnerAsync(systemId, normalizedUserId, cancellationToken);
                    await systemRepository.SaveChangesAsync(cancellationToken);
                    return SystemOwnershipAssignmentResult.Failed(addRoleResult);
                }
            }

            return SystemOwnershipAssignmentResult.Assigned;
        }
        finally
        {
            OwnershipMutationLock.Release();
        }
    }

    public async Task<SystemOwnershipRemovalResult> RemoveOwnerAsync(
        Guid systemId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserId = NormalizeUserId(userId);
        if (string.IsNullOrWhiteSpace(normalizedUserId))
        {
            return SystemOwnershipRemovalResult.AssignmentNotFound;
        }

        await OwnershipMutationLock.WaitAsync(cancellationToken);
        try
        {
            if (await systemRepository.GetByIdAsync(systemId, cancellationToken) is null)
            {
                return SystemOwnershipRemovalResult.SystemNotFound;
            }

            bool removed = await systemRepository.RemoveOwnerAsync(systemId, normalizedUserId, cancellationToken);
            if (!removed)
            {
                return SystemOwnershipRemovalResult.AssignmentNotFound;
            }

            await systemRepository.SaveChangesAsync(cancellationToken);

            ApplicationUser? user = await userManager.FindByIdAsync(normalizedUserId);
            if (user is null)
            {
                return SystemOwnershipRemovalResult.Removed;
            }

            bool isAdministrator = await userManager.IsInRoleAsync(user, RoleNames.Administrator);
            int remainingAssignments = await systemRepository.CountOwnerAssignmentsByUserAsync(normalizedUserId, cancellationToken);

            if (!isAdministrator && remainingAssignments == 0)
            {
                bool hasSystemOwnerRole = await userManager.IsInRoleAsync(user, RoleNames.SystemOwner);
                if (hasSystemOwnerRole)
                {
                    IdentityResult removeRoleResult = await userManager.RemoveFromRoleAsync(user, RoleNames.SystemOwner);
                    if (!removeRoleResult.Succeeded)
                    {
                        return SystemOwnershipRemovalResult.Failed(removeRoleResult);
                    }
                }

                bool hasUserRole = await userManager.IsInRoleAsync(user, RoleNames.User);
                if (!hasUserRole)
                {
                    IdentityResult addUserRoleResult = await userManager.AddToRoleAsync(user, RoleNames.User);
                    if (!addUserRoleResult.Succeeded)
                    {
                        return SystemOwnershipRemovalResult.Failed(addUserRoleResult);
                    }
                }
            }

            return SystemOwnershipRemovalResult.Removed;
        }
        finally
        {
            OwnershipMutationLock.Release();
        }
    }

    private static string NormalizeUserId(string userId) => (userId ?? string.Empty).Trim();

    private static string BuildDisplayName(ApplicationUser user) =>
        user.DisplayName ?? user.Email ?? user.UserName ?? "(未命名用户)";
}

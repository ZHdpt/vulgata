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
    private static readonly SemaphoreSlim _ownershipMutationLock = new(1, 1);

    public async Task<IReadOnlyList<SystemOwnerAssignmentDto>> ListOwnersAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await systemRepository.ListOwnersAsync(systemId, cancellationToken);
        if (assignments.Count == 0)
        {
            return [];
        }

        var userIds = assignments
            .Select(assignment => assignment.UserId)
            .ToHashSet(StringComparer.Ordinal);

        var users = await userManager.Users
            .Where(user => userIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        var userLookup = users.ToDictionary(user => user.Id, StringComparer.Ordinal);

        return assignments
            .Select(assignment =>
            {
                if (userLookup.TryGetValue(assignment.UserId, out var user))
                {
                    return new SystemOwnerAssignmentDto
                    {
                        UserId = user.Id,
                        DisplayName = BuildDisplayName(user),
                        Email = user.Email ?? user.UserName ?? string.Empty,
                        AssignedAt = assignment.AssignedAt,
                    };
                }

                return new SystemOwnerAssignmentDto
                {
                    UserId = assignment.UserId,
                    DisplayName = $"已删除用户（{assignment.UserId}）",
                    Email = string.Empty,
                    AssignedAt = assignment.AssignedAt,
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SystemOwnerCandidateDto>> ListOwnerCandidatesAsync(
        Guid systemId,
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        var assignments = await systemRepository.ListOwnersAsync(systemId, cancellationToken);

        var assignedUserIds = assignments
            .Select(assignment => assignment.UserId)
            .ToHashSet(StringComparer.Ordinal);

        var administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
        var administratorUserIds = administrators
            .Select(user => user.Id)
            .ToHashSet(StringComparer.Ordinal);

        var query = userManager.Users
            .Where(user => !assignedUserIds.Contains(user.Id) && !administratorUserIds.Contains(user.Id));

        string normalizedKeyword = (keyword ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            query = query.Where(user =>
                (user.Email ?? string.Empty).ToUpper().Contains(normalizedKeyword)
                || (user.UserName ?? string.Empty).ToUpper().Contains(normalizedKeyword)
                || (user.DisplayName ?? string.Empty).ToUpper().Contains(normalizedKeyword));
        }

        var users = await query
            .OrderBy(user => user.Email)
            .ThenBy(user => user.UserName)
            .Take(50)
            .ToListAsync(cancellationToken);

        return users
            .Select(user => new SystemOwnerCandidateDto
            {
                UserId = user.Id,
                DisplayName = BuildDisplayName(user),
                Email = user.Email ?? user.UserName ?? string.Empty,
            })
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

        await _ownershipMutationLock.WaitAsync(cancellationToken);
        try
        {
            var user = await userManager.FindByIdAsync(normalizedUserId);
            if (user is null)
            {
                return SystemOwnershipAssignmentResult.UserNotFound;
            }

            if (await userManager.IsInRoleAsync(user, RoleNames.Administrator))
            {
                return SystemOwnershipAssignmentResult.UserIsAdministrator;
            }

            var assignmentResult = await systemRepository.AssignOwnerAsync(
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
                var addRoleResult = await userManager.AddToRoleAsync(user, RoleNames.SystemOwner);
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
            _ownershipMutationLock.Release();
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

        await _ownershipMutationLock.WaitAsync(cancellationToken);
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

            var user = await userManager.FindByIdAsync(normalizedUserId);
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
                    var removeRoleResult = await userManager.RemoveFromRoleAsync(user, RoleNames.SystemOwner);
                    if (!removeRoleResult.Succeeded)
                    {
                        return SystemOwnershipRemovalResult.Failed(removeRoleResult);
                    }
                }

                bool hasUserRole = await userManager.IsInRoleAsync(user, RoleNames.User);
                if (!hasUserRole)
                {
                    var addUserRoleResult = await userManager.AddToRoleAsync(user, RoleNames.User);
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
            _ownershipMutationLock.Release();
        }
    }

    private static string NormalizeUserId(string userId) => (userId ?? string.Empty).Trim();

    private static string BuildDisplayName(ApplicationUser user) =>
        user.DisplayName ?? user.Email ?? user.UserName ?? "(未命名用户)";
}

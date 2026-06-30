using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Shared.Systems;
using Vulgata.Web.Data;
using Vulgata.Web.Validators;
using SystemEntity = Vulgata.Core.Entities.System;

namespace Vulgata.Web.Api.Systems;

internal static class SystemApiEndpoints
{
    public static void MapSystemApiEndpoints(this WebApplication app)
    {
        RouteGroupBuilder systemsApi = app.MapGroup("/api/systems")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        systemsApi.MapGet("/", async (
            ISystemRepository repository,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;

            IReadOnlyList<SystemEntity> systems = await repository.ListVisibleAsync(userId, isAdministrator, cancellationToken);

            return Results.Ok(systems.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                description = s.Description,
                context = s.Context,
                createdAt = s.CreatedAt,
                updatedAt = s.UpdatedAt,
            }));
        });

        systemsApi.MapPost("/", async (
            CreateSystemRequest request,
            IValidator<CreateSystemRequest> validator,
            ISystemRepository repository,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以修改系统。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            bool nameExists = await repository.NameExistsAsync(request.Name, cancellationToken: cancellationToken);
            if (nameExists)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Name"] = ["系统名称已存在。"],
                });
            }

            SystemEntity newSystem = new(
                request.Name,
                request.Description,
                request.Context,
                DateTimeOffset.UtcNow);

            await repository.AddAsync(newSystem, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/systems/{newSystem.Id}", new
            {
                id = newSystem.Id,
                name = newSystem.Name,
                description = newSystem.Description,
                context = newSystem.Context,
                createdAt = newSystem.CreatedAt,
                updatedAt = newSystem.UpdatedAt,
            });
        });

        systemsApi.MapPut("/{id:guid}", async (
            Guid id,
            UpdateSystemRequest request,
            IValidator<UpdateSystemRequest> validator,
            ISystemRepository repository,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以修改系统。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            bool nameExists = await repository.NameExistsAsync(request.Name, id, cancellationToken);
            if (nameExists)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Name"] = ["系统名称已存在。"],
                });
            }

            SystemEntity? existing = await repository.GetByIdAsync(id, cancellationToken);
            if (existing is null)
            {
                return Results.Problem(
                    detail: "系统不存在。",
                    statusCode: StatusCodes.Status404NotFound);
            }

            existing.UpdateDetails(request.Name, request.Description, request.Context, DateTimeOffset.UtcNow);
            await repository.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                id = existing.Id,
                name = existing.Name,
                description = existing.Description,
                context = existing.Context,
                createdAt = existing.CreatedAt,
                updatedAt = existing.UpdatedAt,
            });
        });

        systemsApi.MapDelete("/{id:guid}", async (
            Guid id,
            ISystemRepository repository,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以修改系统。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            SystemDeleteResult result = await repository.DeleteIfNoDependenciesAsync(id, cancellationToken);

            if (result.Outcome == SystemDeleteOutcome.Deleted)
            {
                await repository.SaveChangesAsync(cancellationToken);
                return Results.NoContent();
            }

            if (result.Outcome == SystemDeleteOutcome.NotFound)
            {
                return Results.Problem(
                    detail: "系统不存在。",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Problem(
                detail: $"请先移除依赖（{result.RepositoryCount} 个仓库、{result.OwnerAssignmentCount} 个所有者分配），再删除该系统。",
                statusCode: StatusCodes.Status409Conflict);
        });

        systemsApi.MapGet("/{systemId:guid}/owners", async (
            Guid systemId,
            ISystemRepository repository,
            ISystemOwnershipCoordinator ownershipCoordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以管理系统所有者。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (await repository.GetByIdAsync(systemId, cancellationToken) is null)
            {
                return Results.Problem(
                    detail: "系统不存在。",
                    statusCode: StatusCodes.Status404NotFound);
            }

            IReadOnlyList<SystemOwnerAssignmentDto> owners = await ownershipCoordinator.ListOwnersAsync(systemId, cancellationToken);
            return Results.Ok(owners);
        });

        systemsApi.MapGet("/{systemId:guid}/owner-candidates", async (
            Guid systemId,
            string? keyword,
            ISystemRepository repository,
            ISystemOwnershipCoordinator ownershipCoordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以管理系统所有者。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (await repository.GetByIdAsync(systemId, cancellationToken) is null)
            {
                return Results.Problem(
                    detail: "系统不存在。",
                    statusCode: StatusCodes.Status404NotFound);
            }

            IReadOnlyList<SystemOwnerCandidateDto> candidates =
                await ownershipCoordinator.ListOwnerCandidatesAsync(systemId, keyword, cancellationToken);

            return Results.Ok(candidates);
        });

        systemsApi.MapPost("/{systemId:guid}/owners", async (
            Guid systemId,
            AssignSystemOwnerRequest request,
            IValidator<AssignSystemOwnerRequest> validator,
            ISystemOwnershipCoordinator ownershipCoordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以管理系统所有者。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            SystemOwnershipAssignmentResult result = await ownershipCoordinator.AssignOwnerAsync(
                systemId,
                request.UserId,
                cancellationToken);

            if (result.Outcome == SystemOwnershipAssignmentOutcome.Assigned)
            {
                return Results.NoContent();
            }

            if (result.Outcome == SystemOwnershipAssignmentOutcome.SystemNotFound)
            {
                return Results.Problem(
                    detail: "系统不存在。",
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (result.Outcome == SystemOwnershipAssignmentOutcome.UserNotFound)
            {
                return Results.Problem(
                    detail: "用户不存在。",
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (result.Outcome == SystemOwnershipAssignmentOutcome.UserIsAdministrator)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["UserId"] = ["管理员已具备全局权限，无需分配系统所有者。"],
                });
            }

            if (result.Outcome == SystemOwnershipAssignmentOutcome.AlreadyAssigned)
            {
                return Results.Problem(
                    detail: "该用户已是该系统的所有者。",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return Results.Problem(
                detail: BuildIdentityErrorMessage(result.IdentityResult?.Errors),
                statusCode: StatusCodes.Status500InternalServerError);
        });

        systemsApi.MapDelete("/{systemId:guid}/owners/{userId}", async (
            Guid systemId,
            string userId,
            ISystemOwnershipCoordinator ownershipCoordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以管理系统所有者。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            SystemOwnershipRemovalResult result = await ownershipCoordinator.RemoveOwnerAsync(systemId, userId, cancellationToken);

            if (result.Outcome == SystemOwnershipRemovalOutcome.Removed)
            {
                return Results.NoContent();
            }

            if (result.Outcome == SystemOwnershipRemovalOutcome.SystemNotFound)
            {
                return Results.Problem(
                    detail: "系统不存在。",
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (result.Outcome == SystemOwnershipRemovalOutcome.AssignmentNotFound)
            {
                return Results.Problem(
                    detail: "系统所有者分配不存在。",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Problem(
                detail: BuildIdentityErrorMessage(result.IdentityResult?.Errors),
                statusCode: StatusCodes.Status500InternalServerError);
        });
    }

    private static string BuildIdentityErrorMessage(IEnumerable<IdentityError>? errors)
    {
        if (errors is null)
        {
            return "系统所有者角色变更失败，请稍后重试。";
        }

        string detail = string.Join("；", errors.Select(error => error.Description));
        return string.IsNullOrWhiteSpace(detail)
            ? "系统所有者角色变更失败，请稍后重试。"
            : detail;
    }
}

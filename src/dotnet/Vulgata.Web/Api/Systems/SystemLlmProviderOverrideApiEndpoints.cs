using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Vulgata.Shared;
using Vulgata.Shared.LlmProviders;
using Vulgata.Web.Data;
using Vulgata.Web.Validators;

namespace Vulgata.Web.Api.Systems;

internal static class SystemLlmProviderOverrideApiEndpoints
{
    public static void MapSystemLlmProviderOverrideApiEndpoints(this WebApplication app)
    {
        RouteGroupBuilder systemLlmProviderOverridesApi = app.MapGroup("/api/systems/{systemId:guid}/llm-provider-overrides")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);
        MapSystemLlmProviderOverrideEndpoints(systemLlmProviderOverridesApi);

        RouteGroupBuilder systemLlmOverridesApi = app.MapGroup("/api/systems/{systemId:guid}/llm-overrides")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);
        MapSystemLlmProviderOverrideEndpoints(systemLlmOverridesApi);
    }

    private static void MapSystemLlmProviderOverrideEndpoints(RouteGroupBuilder routeGroup)
    {
        routeGroup.MapGet("/", async (
            Guid systemId,
            ISystemLlmProviderOverrideCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            SystemLlmProviderOverrideMutationResult<IReadOnlyList<SystemLlmProviderOverrideSummaryDto>> result =
                await coordinator.ListAsync(systemId, userId, isAdministrator, cancellationToken);

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.SystemNotFound)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Ok(result.Value ?? []);
        });

        routeGroup.MapGet("/providers", async (
            Guid systemId,
            ISystemLlmProviderOverrideCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            SystemLlmProviderOverrideMutationResult<IReadOnlyList<LlmProviderSummaryDto>> result =
                await coordinator.ListProvidersAsync(systemId, userId, isAdministrator, cancellationToken);

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.SystemNotFound)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Ok(result.Value ?? []);
        });

        routeGroup.MapPost("/", async (
            Guid systemId,
            UpsertSystemLlmProviderOverrideRequest request,
            IValidator<UpsertSystemLlmProviderOverrideRequest> validator,
            ISystemLlmProviderOverrideCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            if (request.SystemId != systemId)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["SystemId"] = ["请求中的系统标识与路由不一致。"],
                });
            }

            SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto> result =
                await coordinator.UpsertAsync(systemId, null, request, userId, isAdministrator, cancellationToken);

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.SystemNotFound)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.ProviderNotFound)
            {
                return Results.Problem(detail: "LLM 提供商不存在。", statusCode: StatusCodes.Status400BadRequest);
            }

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.AgentTypeMismatch)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["AgentType"] = [result.Message ?? "所选 LLM 提供商的默认代理角色与目标代理角色不匹配。"],
                });
            }

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.DuplicateAgentType)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["AgentType"] = [result.Message ?? "同一系统下该代理角色已存在覆盖配置。"],
                });
            }

            if (result.Value is null)
            {
                return Results.Problem(detail: "系统级覆盖保存失败。", statusCode: StatusCodes.Status500InternalServerError);
            }

            if (result.IsCreated)
            {
                return Results.Created($"/api/systems/{systemId}/llm-provider-overrides/{result.Value.Id}", result.Value);
            }

            return Results.Ok(result.Value);
        });

        routeGroup.MapPut("/{overrideId:guid}", async (
            Guid systemId,
            Guid overrideId,
            UpsertSystemLlmProviderOverrideRequest request,
            IValidator<UpsertSystemLlmProviderOverrideRequest> validator,
            ISystemLlmProviderOverrideCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            if (request.SystemId != systemId)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["SystemId"] = ["请求中的系统标识与路由不一致。"],
                });
            }

            SystemLlmProviderOverrideMutationResult<SystemLlmProviderOverrideSummaryDto> result =
                await coordinator.UpsertAsync(systemId, overrideId, request, userId, isAdministrator, cancellationToken);

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.SystemNotFound)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.OverrideNotFound)
            {
                return Results.Problem(detail: "系统级 LLM 覆盖不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.ProviderNotFound)
            {
                return Results.Problem(detail: "LLM 提供商不存在。", statusCode: StatusCodes.Status400BadRequest);
            }

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.AgentTypeMismatch)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["AgentType"] = [result.Message ?? "所选 LLM 提供商的默认代理角色与目标代理角色不匹配。"],
                });
            }

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.DuplicateAgentType)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["AgentType"] = [result.Message ?? "同一系统下该代理角色已存在覆盖配置。"],
                });
            }

            if (result.Value is null)
            {
                return Results.Problem(detail: "系统级覆盖更新失败。", statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Ok(result.Value);
        });

        routeGroup.MapDelete("/{overrideId:guid}", async (
            Guid systemId,
            Guid overrideId,
            ISystemLlmProviderOverrideCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            SystemLlmProviderOverrideMutationResult<bool> result = await coordinator.DeleteAsync(
                systemId,
                overrideId,
                userId,
                isAdministrator,
                cancellationToken);

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.SystemNotFound)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            if (result.Outcome == SystemLlmProviderOverrideMutationOutcome.OverrideNotFound)
            {
                return Results.Problem(detail: "系统级 LLM 覆盖不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.NoContent();
        });
    }
}

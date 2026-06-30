using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Vulgata.Shared;
using Vulgata.Shared.LlmProviders;
using Vulgata.Web.Data;
using Vulgata.Web.Validators;

namespace Vulgata.Web.Api.LlmProviders;

internal static class LlmProviderApiEndpoints
{
    public static void MapLlmProviderApiEndpoints(this WebApplication app)
    {
        RouteGroupBuilder llmProvidersApi = app.MapGroup("/api/llm-providers")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        llmProvidersApi.MapGet("/", async (
            ILlmProviderManagementCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以管理 LLM 提供商配置。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            IReadOnlyList<LlmProviderSummaryDto> providers = await coordinator.ListAsync(cancellationToken);
            return Results.Ok(providers);
        });

        llmProvidersApi.MapPost("/", async (
            CreateLlmProviderRequest request,
            IValidator<CreateLlmProviderRequest> validator,
            ILlmProviderManagementCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以管理 LLM 提供商配置。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            LlmProviderMutationResult<LlmProviderSummaryDto> result = await coordinator.CreateAsync(request, cancellationToken);
            if (result.Outcome == LlmProviderMutationOutcome.DuplicateName)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Name"] = [result.Message ?? "提供商名称已存在。"],
                });
            }

            return Results.Created($"/api/llm-providers/{result.Value!.Id}", result.Value);
        });

        llmProvidersApi.MapPut("/{id:guid}", async (
            Guid id,
            UpdateLlmProviderRequest request,
            IValidator<UpdateLlmProviderRequest> validator,
            ILlmProviderManagementCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以管理 LLM 提供商配置。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            LlmProviderMutationResult<LlmProviderSummaryDto> result = await coordinator.UpdateAsync(id, request, cancellationToken);
            if (result.Outcome == LlmProviderMutationOutcome.NotFound)
            {
                return Results.Problem(
                    detail: "LLM 提供商不存在。",
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (result.Outcome == LlmProviderMutationOutcome.DuplicateName)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Name"] = [result.Message ?? "提供商名称已存在。"],
                });
            }

            return Results.Ok(result.Value);
        });

        llmProvidersApi.MapDelete("/{id:guid}", async (
            Guid id,
            ILlmProviderManagementCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以管理 LLM 提供商配置。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            LlmProviderMutationResult<bool> result = await coordinator.DeleteAsync(id, cancellationToken);
            if (result.Outcome == LlmProviderMutationOutcome.NotFound)
            {
                return Results.Problem(
                    detail: "LLM 提供商不存在。",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Results.NoContent();
        });

        llmProvidersApi.MapPost("/{id:guid}/test", async (
            Guid id,
            ILlmProviderManagementCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            if (!authResult.Succeeded)
            {
                return Results.Problem(
                    detail: "只有管理员可以管理 LLM 提供商配置。",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            LlmProviderConnectionTestResult result = await coordinator.TestConnectionAsync(id, cancellationToken);
            if (result.Outcome == LlmProviderConnectionTestOutcome.NotFound)
            {
                return Results.Problem(
                    detail: result.Message,
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (result.Outcome == LlmProviderConnectionTestOutcome.Success)
            {
                return Results.Ok(new LlmProviderConnectionTestResponse
                {
                    Success = true,
                    Message = result.Message,
                });
            }

            return Results.Problem(
                detail: result.Message,
                statusCode: StatusCodes.Status400BadRequest);
        });
    }
}

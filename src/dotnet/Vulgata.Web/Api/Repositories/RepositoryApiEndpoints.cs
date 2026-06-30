using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Git;
using Vulgata.Shared;
using Vulgata.Shared.Repositories;
using Vulgata.Web.Data;
using Vulgata.Web.Validators;
using RepositoryEntity = Vulgata.Core.Entities.Repository;

namespace Vulgata.Web.Api.Repositories;

internal static class RepositoryApiEndpoints
{
    public static void MapRepositoryApiEndpoints(this WebApplication app)
    {
        RouteGroupBuilder repoApi = app.MapGroup("/api/systems/{systemId:guid}/repositories")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        repoApi.MapGet("/", async (
            Guid systemId,
            ISystemRepository systemRepository,
            IRepositoryRepository repositoryRepository,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken) is null)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            IReadOnlyList<RepositoryEntity> repos = await repositoryRepository.ListBySystemAsync(systemId, cancellationToken);

            return Results.Ok(repos.Select(r => new
            {
                id = r.Id,
                systemId = r.SystemId,
                name = r.Name,
                gitUrl = r.GitUrl,
                description = r.Description,
                context = r.Context,
                scanStatus = "未扫描",
                lastScannedAt = (DateTimeOffset?)null,
                documentCount = 0,
            }));
        });

        repoApi.MapPost("/", async (
            Guid systemId,
            CreateRepositoryRequest request,
            IValidator<CreateRepositoryRequest> validator,
            ISystemRepository systemRepository,
            IRepositoryRepository repositoryRepository,
            IGitRemoteValidationService gitValidation,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken) is null)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            bool nameExists = await repositoryRepository.NameExistsAsync(systemId, request.Name, cancellationToken: cancellationToken);
            if (nameExists)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Name"] = ["仓库名称已存在。"],
                });
            }

            GitRemoteValidationResult gitResult = await gitValidation.ValidateAsync(request.GitUrl, cancellationToken);
            if (gitResult.Status == GitRemoteValidationStatus.AuthenticationRequired)
            {
                return Results.Problem(
                    detail: $"Git URL 不可达：{gitResult.Message}",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (gitResult.Status == GitRemoteValidationStatus.Unreachable)
            {
                return Results.Problem(
                    detail: $"Git URL 不可达：{gitResult.Message}",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            RepositoryEntity newRepo = RepositoryEntity.Create(systemId, request.Name, request.GitUrl, request.Description, request.Context, now);

            await repositoryRepository.AddAsync(newRepo, cancellationToken);
            await repositoryRepository.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/systems/{systemId}/repositories/{newRepo.Id}", new
            {
                id = newRepo.Id,
                systemId = newRepo.SystemId,
                name = newRepo.Name,
                gitUrl = newRepo.GitUrl,
                description = newRepo.Description,
                context = newRepo.Context,
                scanStatus = "未扫描",
                lastScannedAt = (DateTimeOffset?)null,
                documentCount = 0,
            });
        });

        repoApi.MapDelete("/{repositoryId:guid}", async (
            Guid systemId,
            Guid repositoryId,
            ISystemRepository systemRepository,
            IRepositoryRepository repositoryRepository,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (await systemRepository.GetVisibleByIdAsync(systemId, userId, isAdministrator, cancellationToken) is null)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            RepositoryEntity? repo = await repositoryRepository.GetBySystemAndIdAsync(systemId, repositoryId, cancellationToken);
            if (repo is null)
            {
                return Results.Problem(detail: "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            repositoryRepository.Remove(repo);
            await repositoryRepository.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        RouteGroupBuilder standaloneRepoApi = app.MapGroup("/api/repositories/standalone")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        standaloneRepoApi.MapGet("/", async (
            IRepositoryManagementCoordinator repositoryCoordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            IReadOnlyList<RepositorySummaryDto> repositories = await repositoryCoordinator.ListStandaloneVisibleAsync(
                userId,
                isAdministrator,
                cancellationToken);

            return Results.Ok(repositories);
        });

        standaloneRepoApi.MapPost("/", async (
            CreateRepositoryRequest request,
            IValidator<CreateRepositoryRequest> validator,
            IRepositoryManagementCoordinator repositoryCoordinator,
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

            RepositoryMutationResult<RepositoryDetailDto> result = await repositoryCoordinator.CreateStandaloneAsync(
                request,
                userId,
                isAdministrator,
                cancellationToken);

            if (result.Outcome == RepositoryMutationOutcome.Success && result.Value is not null)
            {
                return Results.Created($"/api/repositories/standalone/{result.Value.Id}", result.Value);
            }

            if (result.Outcome == RepositoryMutationOutcome.DuplicateName)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Name"] = [result.Message ?? "独立仓库名称已存在。"],
                });
            }

            if (result.Outcome == RepositoryMutationOutcome.GitAuthenticationRequired
                || result.Outcome == RepositoryMutationOutcome.GitUnreachable)
            {
                return Results.Problem(
                    detail: $"Git URL 不可达：{result.Message}",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Problem(
                detail: "独立仓库创建失败。",
                statusCode: StatusCodes.Status500InternalServerError);
        });

        RouteGroupBuilder repositoryDatabaseConnectionApi = app.MapGroup("/api/repositories/{repositoryId:guid}/database-connection")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        repositoryDatabaseConnectionApi.MapGet("/", async (
            Guid repositoryId,
            IRepositoryDatabaseConnectionCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto> result = await coordinator.GetAsync(
                repositoryId,
                userId,
                isAdministrator,
                cancellationToken);

            if (result.Outcome == RepositoryDatabaseConnectionMutationOutcome.RepositoryNotFound)
            {
                return Results.Problem(detail: result.Message ?? "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Ok(result.Value);
        });

        repositoryDatabaseConnectionApi.MapPut("/", async (
            Guid repositoryId,
            UpsertDatabaseConnectionRequest request,
            IValidator<UpsertDatabaseConnectionRequest> validator,
            IRepositoryDatabaseConnectionCoordinator coordinator,
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

            RepositoryDatabaseConnectionMutationResult<RepositoryDatabaseConnectionSummaryDto> result = await coordinator.UpsertAsync(
                repositoryId,
                request,
                userId,
                isAdministrator,
                cancellationToken);

            if (result.Outcome == RepositoryDatabaseConnectionMutationOutcome.RepositoryNotFound)
            {
                return Results.Problem(detail: result.Message ?? "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Ok(result.Value);
        });

        repositoryDatabaseConnectionApi.MapDelete("/", async (
            Guid repositoryId,
            IRepositoryDatabaseConnectionCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            RepositoryDatabaseConnectionMutationResult<bool> result = await coordinator.DeleteAsync(
                repositoryId,
                userId,
                isAdministrator,
                cancellationToken);

            if (result.Outcome == RepositoryDatabaseConnectionMutationOutcome.RepositoryNotFound)
            {
                return Results.Problem(detail: result.Message ?? "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.NoContent();
        });

        repositoryDatabaseConnectionApi.MapPost("/test", async (
            Guid repositoryId,
            IRepositoryDatabaseConnectionCoordinator coordinator,
            ClaimsPrincipal user,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            AuthorizationResult authResult = await authorization.AuthorizeAsync(user, AuthorizationPolicyNames.AdministratorOnly);
            bool isAdministrator = authResult.Succeeded;
            string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            RepositoryDatabaseConnectionTestResult result = await coordinator.TestConnectionAsync(
                repositoryId,
                userId,
                isAdministrator,
                cancellationToken);

            if (result.Outcome == RepositoryDatabaseConnectionTestOutcome.RepositoryNotFound)
            {
                return Results.Problem(detail: result.Message, statusCode: StatusCodes.Status404NotFound);
            }

            if (result.Outcome == RepositoryDatabaseConnectionTestOutcome.Success)
            {
                return Results.Ok(new RepositoryDatabaseConnectionTestResponse
                {
                    Success = true,
                    Message = result.Message,
                });
            }

            return Results.Problem(detail: result.Message, statusCode: StatusCodes.Status400BadRequest);
        });
    }
}

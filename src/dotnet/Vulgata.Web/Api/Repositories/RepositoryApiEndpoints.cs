using FluentValidation;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Shared.Repositories;
using Vulgata.Web.Data;
using Vulgata.Web.Validators;

namespace Vulgata.Web.Api.Repositories;

internal static class RepositoryApiEndpoints
{
    public static void MapRepositoryApiEndpoints(this WebApplication app)
    {
        var repoApi = app.MapGroup("/api/systems/{systemId:guid}/repositories")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        repoApi.MapGet("/", async (
            Guid systemId,
            ISystemRepository systemRepository,
            IRepositoryRepository repositoryRepository,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            if (await systemRepository.GetVisibleByIdAsync(systemId, user.ToSystemOwnerFilter(), cancellationToken) is null)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            var repos = await repositoryRepository.ListBySystemAsync(systemId, cancellationToken);

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
            IRepositoryManagementCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result =
                await coordinator.CreateAsync(systemId, request, user, cancellationToken);

            return result.Outcome switch
            {
                RepositoryMutationOutcome.Success => Results.Created(
                    $"/api/systems/{systemId}/repositories/{result.Value!.Id}", result.Value),

                RepositoryMutationOutcome.SystemNotFound => Results.Problem(
                    detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound),

                RepositoryMutationOutcome.DuplicateName => Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["Name"] = [result.Message ?? "仓库名称已存在。"] }),

                RepositoryMutationOutcome.GitAuthenticationRequired or
                    RepositoryMutationOutcome.GitUnreachable => Results.Problem(
                        detail: $"Git URL 不可达：{result.Message}", statusCode: StatusCodes.Status400BadRequest),

                _ => Results.Problem(detail: "仓库创建失败。", statusCode: StatusCodes.Status500InternalServerError)
            };
        });

        repoApi.MapDelete("/{repositoryId:guid}", async (
            Guid systemId,
            Guid repositoryId,
            ISystemRepository systemRepository,
            IRepositoryRepository repositoryRepository,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            if (await systemRepository.GetVisibleByIdAsync(systemId, user.ToSystemOwnerFilter(), cancellationToken) is null)
            {
                return Results.Problem(detail: "系统不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            var repo = await repositoryRepository.GetBySystemAndIdAsync(systemId, repositoryId, cancellationToken);
            if (repo is null)
            {
                return Results.Problem(detail: "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            repositoryRepository.Remove(repo);
            await repositoryRepository.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        repoApi.MapPost("/{repositoryId:guid}/scan-runs", async (
            Guid systemId,
            Guid repositoryId,
            IRepositoryProfilingCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            RepositoryProfilingResult result = await coordinator.StartSystemRepositoryAsync(
                systemId,
                repositoryId,
                user,
                cancellationToken);
            return ToProfilingResult(
                result,
                $"/api/systems/{systemId}/repositories/{repositoryId}/scan-runs");
        });

        repoApi.MapGet("/{repositoryId:guid}/scan-runs/{scanRunId:guid}", async (
            Guid systemId,
            Guid repositoryId,
            Guid scanRunId,
            IRepositoryProfilingCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            ScanRunSummaryDto? result = await coordinator.GetSystemRepositoryRunAsync(
                systemId,
                repositoryId,
                scanRunId,
                user,
                cancellationToken);
            return result is null
                ? Results.Problem(detail: "扫描记录不存在。", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(result);
        });

        var standaloneRepoApi = app.MapGroup("/api/repositories/standalone")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        standaloneRepoApi.MapGet("/", async (
            IRepositoryManagementCoordinator repositoryCoordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var repositories = await repositoryCoordinator.ListStandaloneVisibleAsync(user, cancellationToken);
            return Results.Ok(repositories);
        });

        standaloneRepoApi.MapPost("/", async (
            CreateRepositoryRequest request,
            IValidator<CreateRepositoryRequest> validator,
            IRepositoryManagementCoordinator repositoryCoordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result = await repositoryCoordinator.CreateStandaloneAsync(request, user, cancellationToken);

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

        standaloneRepoApi.MapPost("/{repositoryId:guid}/scan-runs", async (
            Guid repositoryId,
            IRepositoryProfilingCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            RepositoryProfilingResult result = await coordinator.StartStandaloneRepositoryAsync(
                repositoryId,
                user,
                cancellationToken);
            return ToProfilingResult(
                result,
                $"/api/repositories/standalone/{repositoryId}/scan-runs");
        });

        standaloneRepoApi.MapGet("/{repositoryId:guid}/scan-runs/{scanRunId:guid}", async (
            Guid repositoryId,
            Guid scanRunId,
            IRepositoryProfilingCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            ScanRunSummaryDto? result = await coordinator.GetStandaloneRepositoryRunAsync(
                repositoryId,
                scanRunId,
                user,
                cancellationToken);
            return result is null
                ? Results.Problem(detail: "扫描记录不存在。", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(result);
        });

        var repositoryDatabaseConnectionApi = app.MapGroup("/api/repositories/{repositoryId:guid}/database-connection")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        repositoryDatabaseConnectionApi.MapGet("/", async (
            Guid repositoryId,
            IRepositoryDatabaseConnectionCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var result =
                await coordinator.GetAsync(repositoryId, user, cancellationToken);

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
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result =
                await coordinator.UpsertAsync(repositoryId, request, user, cancellationToken);

            if (result.Outcome == RepositoryDatabaseConnectionMutationOutcome.RepositoryNotFound)
            {
                return Results.Problem(detail: result.Message ?? "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Ok(result.Value);
        });

        repositoryDatabaseConnectionApi.MapDelete("/", async (
            Guid repositoryId,
            IRepositoryDatabaseConnectionCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var result =
                await coordinator.DeleteAsync(repositoryId, user, cancellationToken);

            if (result.Outcome == RepositoryDatabaseConnectionMutationOutcome.RepositoryNotFound)
            {
                return Results.Problem(detail: result.Message ?? "仓库不存在。", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.NoContent();
        });

        repositoryDatabaseConnectionApi.MapPost("/test", async (
            Guid repositoryId,
            IRepositoryDatabaseConnectionCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var result = await coordinator.TestConnectionAsync(repositoryId, user, cancellationToken);

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

    private static IResult ToProfilingResult(RepositoryProfilingResult result, string collectionPath)
    {
        return result.Outcome switch
        {
            RepositoryProfilingOutcome.Success => Results.Created(
                $"{collectionPath}/{result.Value!.Id}",
                result.Value),
            RepositoryProfilingOutcome.RepositoryNotFound => Results.Problem(
                detail: "仓库不存在。",
                statusCode: StatusCodes.Status404NotFound),
            RepositoryProfilingOutcome.GitFailure => Results.Problem(
                detail: "仓库克隆失败，请检查仓库地址和访问权限。",
                statusCode: StatusCodes.Status400BadRequest),
            RepositoryProfilingOutcome.ProviderUnavailable => Results.Problem(
                detail: "本地 CodeGraph 服务不可用，请联系管理员。",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            RepositoryProfilingOutcome.ProviderFailure => Results.Problem(
                detail: "代码结构分析失败，请稍后重试。",
                statusCode: StatusCodes.Status502BadGateway),
            RepositoryProfilingOutcome.CandidateAuditBoundExceeded => Results.Problem(
                detail: "候选源文件审计范围超出限制。",
                statusCode: StatusCodes.Status400BadRequest),
            _ => Results.Problem(detail: "扫描启动失败。", statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}

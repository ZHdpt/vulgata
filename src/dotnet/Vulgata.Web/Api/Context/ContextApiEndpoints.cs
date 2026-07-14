using Vulgata.Core.DomainServices;
using Vulgata.Shared;

namespace Vulgata.Web.Api.Context;

internal static class ContextApiEndpoints
{
    public static void MapContextApiEndpoints(this WebApplication app)
    {
        var settingsApi = app.MapGroup("/api/settings")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        settingsApi.MapGet("/global-context", async (
            IContextManagementCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var state = await coordinator.GetStateAsync(ContextScope.Global, user, cancellationToken);

            return Results.Ok(new ContextStateResponse
            {
                CurrentContext = state.CurrentValue,
                PendingContext = state.PendingValue,
                Queued = state.PendingValue is not null,
                StatusMessage = state.PendingValue is not null ? "上下文修改将在当前扫描完成后生效" : null,
            });
        }).RequireAuthorization(AuthorizationPolicyNames.AdministratorOnly);

        settingsApi.MapPut("/global-context", async (
            HttpRequest request,
            IContextManagementCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            if (!user.IsAdministrator)
            {
                return Results.Forbid();
            }

            var body = await request.ReadFromJsonAsync<ContextSaveRequest>(cancellationToken);
            if (body is null)
            {
                return Results.Problem(detail: "请求格式无效。", statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await coordinator.SaveAsync(ContextScope.Global, body.Context, user, cancellationToken);
            if (!result.IsSuccess)
            {
                return MapSaveFailure(result);
            }

            var state = await coordinator.GetStateAsync(ContextScope.Global, user, cancellationToken);
            return Results.Ok(ToResponse(state));
        }).RequireAuthorization(AuthorizationPolicyNames.AdministratorOnly);

        var systemContextApi = app.MapGroup("/api/systems/{systemId:guid}/context")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        systemContextApi.MapGet("/", async (
            Guid systemId,
            IContextManagementCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var state = await coordinator.GetStateAsync(ContextScope.ForSystem(systemId), user, cancellationToken);
            return Results.Ok(ToResponse(state));
        });

        systemContextApi.MapPut("/", async (
            Guid systemId,
            HttpRequest request,
            IContextManagementCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var body = await request.ReadFromJsonAsync<ContextSaveRequest>(cancellationToken);
            if (body is null)
            {
                return Results.Problem(detail: "请求格式无效。", statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await coordinator.SaveAsync(ContextScope.ForSystem(systemId), body.Context, user, cancellationToken);
            if (!result.IsSuccess)
            {
                return MapSaveFailure(result);
            }

            var state = await coordinator.GetStateAsync(ContextScope.ForSystem(systemId), user, cancellationToken);
            return Results.Ok(ToResponse(state));
        });

        var repoContextApi = app.MapGroup("/api/repositories/{repositoryId:guid}/context")
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);

        repoContextApi.MapGet("/", async (
            Guid repositoryId,
            IContextManagementCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var state = await coordinator.GetStateAsync(ContextScope.ForRepository(repositoryId), user, cancellationToken);
            return Results.Ok(ToResponse(state));
        });

        repoContextApi.MapPut("/", async (
            Guid repositoryId,
            HttpRequest request,
            IContextManagementCoordinator coordinator,
            IUserContext user,
            CancellationToken cancellationToken) =>
        {
            var body = await request.ReadFromJsonAsync<ContextSaveRequest>(cancellationToken);
            if (body is null)
            {
                return Results.Problem(detail: "请求格式无效。", statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await coordinator.SaveAsync(ContextScope.ForRepository(repositoryId), body.Context, user, cancellationToken);
            if (!result.IsSuccess)
            {
                return MapSaveFailure(result);
            }

            var state = await coordinator.GetStateAsync(ContextScope.ForRepository(repositoryId), user, cancellationToken);
            return Results.Ok(ToResponse(state));
        });
    }

    private static ContextStateResponse ToResponse(ContextState state) => new()
    {
        CurrentContext = state.CurrentValue,
        PendingContext = state.PendingValue,
        Queued = state.PendingValue is not null,
        StatusMessage = state.PendingValue is not null ? "上下文修改将在当前扫描完成后生效" : null,
    };

    private static IResult MapSaveFailure(Result result) => result.Outcome switch
    {
        ContextSaveOutcome.Forbidden => Results.Forbid(),
        ContextSaveOutcome.SystemNotFound => Results.Problem(detail: result.Message, statusCode: StatusCodes.Status404NotFound),
        ContextSaveOutcome.RepositoryNotFound => Results.Problem(detail: result.Message, statusCode: StatusCodes.Status404NotFound),
        _ => Results.Problem(detail: result.Message ?? "保存失败。", statusCode: StatusCodes.Status500InternalServerError),
    };
}

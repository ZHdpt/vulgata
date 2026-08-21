using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Shared.Scans;
using Vulgata.Web.Data;

namespace Vulgata.Web.Api.Scans;

internal static class Pass1ExecutionEndpoints
{
    public static void MapPass1ExecutionApiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/pass1-executions", StartAsync).RequireAuthorization();
        app.MapPatch("/api/pass1-executions/{pass1ExecutionId}/concurrency", UpdateConcurrencyAsync)
            .RequireAuthorization();
        app.MapGet("/api/pass1-executions/{pass1ExecutionId}", GetStatusAsync)
            .RequireAuthorization();
        app.MapPost("/api/pass1-executions/{pass1ExecutionId}/resume", ResumeAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> StartAsync(
        StartPass1ExecutionRequest request,
        HttpContext httpContext,
        [FromServices] IValidator<StartPass1ExecutionRequest> validator,
        [FromServices] IAuthorizationService authorization,
        [FromServices] IUserContext user,
        [FromServices] PreparedPass1Coordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (!(await validator.ValidateAsync(request, cancellationToken)).IsValid)
        {
            return Problem(400, "invalid-pass1-start-request", "Pass 1 执行请求无效。");
        }

        PreparedPass1StartBinding? binding = await coordinator.ResolveStartAsync(
            request, user, cancellationToken);
        if (binding is null)
        {
            return NotFound();
        }

        if (!await HasManagementAccessAsync(authorization, httpContext))
        {
            return Forbidden();
        }

        PreparedPass1StartResult result = await coordinator.StartAsync(
            binding,
            request.ConcurrencyLimit ?? 2,
            cancellationToken);
        return result.Outcome switch
        {
            PreparedPass1StartOutcome.Created when result.PairExecutionId is Guid id &&
                result.ConcurrencyLimit is int limit =>
                Results.Created(
                    $"/api/pass1-executions/{id:D}",
                    new Pass1ExecutionStartResponse(id, limit, false)),
            PreparedPass1StartOutcome.Replay when result.PairExecutionId is Guid id &&
                result.ConcurrencyLimit is int limit =>
                Results.Ok(new Pass1ExecutionStartResponse(id, limit, true)),
            PreparedPass1StartOutcome.NotFound => NotFound(),
            PreparedPass1StartOutcome.Conflict =>
                Problem(409, "pass1-start-conflict", "Pass 1 执行状态已变更，请刷新后重试。"),
            PreparedPass1StartOutcome.Rejected =>
                Problem(422, "pass1-start-rejected", "已批准交接校验未通过，未启动 Pass 1。"),
            _ => Unavailable(),
        };
    }

    private static async Task<IResult> UpdateConcurrencyAsync(
        Guid pass1ExecutionId,
        UpdatePass1ConcurrencyRequest request,
        HttpContext httpContext,
        [FromServices] IValidator<UpdatePass1ConcurrencyRequest> validator,
        [FromServices] IAuthorizationService authorization,
        [FromServices] IUserContext user,
        [FromServices] PreparedPass1Coordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (!(await validator.ValidateAsync(request, cancellationToken)).IsValid)
        {
            return Problem(400, "invalid-pass1-concurrency-request", "Pass 1 并发数请求无效。");
        }

        PreparedPass1ExecutionBinding? binding = await coordinator.ResolveExecutionAsync(
            pass1ExecutionId, user, cancellationToken);
        if (binding is null)
        {
            return NotFound();
        }

        if (!await HasManagementAccessAsync(authorization, httpContext) ||
            request.ActorId is { Length: > 0 } actorId &&
                !StringComparer.Ordinal.Equals(actorId, user.UserId))
        {
            return Forbidden();
        }

        PreparedPass1ConcurrencyUpdateResult result = await coordinator.UpdateConcurrencyAsync(
            binding, request, user.UserId, cancellationToken);
        return result.Outcome switch
        {
            PreparedPass1ConcurrencyUpdateOutcome.Updated or
                PreparedPass1ConcurrencyUpdateOutcome.NoChange or
                PreparedPass1ConcurrencyUpdateOutcome.Replay
                when result.ConcurrencyLimit is int limit &&
                    result.StateVersion is long version &&
                    result.ConcurrencyRevision is long revision =>
                Results.Ok(new Pass1ConcurrencyResponse(
                    pass1ExecutionId,
                    limit,
                    version,
                    revision,
                    result.PreservedLeaseCount,
                    result.Outcome == PreparedPass1ConcurrencyUpdateOutcome.Replay)),
            PreparedPass1ConcurrencyUpdateOutcome.NotFound => NotFound(),
            PreparedPass1ConcurrencyUpdateOutcome.Conflict =>
                Problem(409, "pass1-concurrency-conflict", "Pass 1 状态已变更，请刷新后重试。"),
            PreparedPass1ConcurrencyUpdateOutcome.Ineligible or
                PreparedPass1ConcurrencyUpdateOutcome.Rejected =>
                Problem(422, "pass1-concurrency-rejected", "当前 Pass 1 阶段不允许更改并发数。"),
            _ => Unavailable(),
        };
    }

    private static async Task<IResult> GetStatusAsync(
        Guid pass1ExecutionId,
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorization,
        [FromServices] IUserContext user,
        [FromServices] PreparedPass1Coordinator coordinator,
        CancellationToken cancellationToken)
    {
        PreparedPass1ExecutionBinding? binding = await coordinator.ResolveExecutionAsync(
            pass1ExecutionId, user, cancellationToken);
        if (binding is null)
        {
            return NotFound();
        }

        if (!await HasManagementAccessAsync(authorization, httpContext))
        {
            return Forbidden();
        }

        Pass1ExecutionStatusResponse? status = await coordinator.GetStatusAsync(
            binding, cancellationToken);
        return status is null
            ? Problem(422, "pass1-status-integrity-rejected", "Pass 1 状态校验未通过。")
            : Results.Ok(status);
    }

    private static async Task<IResult> ResumeAsync(
        Guid pass1ExecutionId,
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorization,
        [FromServices] IUserContext user,
        [FromServices] PreparedPass1Coordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (httpContext.Request.ContentLength is > 0 ||
            httpContext.Request.Headers.TransferEncoding.Count > 0)
        {
            return Problem(400, "invalid-pass1-resume-request", "继续扫描请求不得包含调用方状态。");
        }

        PreparedPass1ExecutionBinding? binding = await coordinator.ResolveExecutionAsync(
            pass1ExecutionId, user, cancellationToken);
        if (binding is null)
        {
            return NotFound();
        }

        if (!await HasManagementAccessAsync(authorization, httpContext))
        {
            return Forbidden();
        }

        PreparedPass1RecoveryResult result = await coordinator.ResumeAsync(
            pass1ExecutionId, binding.AuthorizationScope, cancellationToken);
        return result.Outcome switch
        {
            PreparedPass1RecoveryOutcome.Continued or PreparedPass1RecoveryOutcome.NoOpReplay
                when result.PairExecutionId == pass1ExecutionId &&
                    result.PairState is not null && result.StateVersion is not null =>
                Results.Ok(ToResumeResponse(pass1ExecutionId, result)),
            PreparedPass1RecoveryOutcome.NotFound => NotFound(),
            PreparedPass1RecoveryOutcome.Paused when result.Reason == DurableScanReason.ProviderUnavailable =>
                Problem(503, "pass1-provider-unavailable", result.ManualResumeAction ?? "继续扫描"),
            PreparedPass1RecoveryOutcome.Paused =>
                Problem(409, "pass1-resume-paused", result.ManualResumeAction ?? "检查后手动继续扫描。"),
            PreparedPass1RecoveryOutcome.Rejected or PreparedPass1RecoveryOutcome.FailedClosed =>
                Problem(422, "pass1-resume-integrity-rejected", "Pass 1 恢复校验未通过，已保留现有文档。"),
            PreparedPass1RecoveryOutcome.Cancelled =>
                Problem(409, "pass1-resume-cancelled", "Pass 1 恢复已取消。"),
            _ => Unavailable(),
        };
    }

    private static Pass1ResumeResponse ToResumeResponse(
        Guid pass1ExecutionId,
        PreparedPass1RecoveryResult result) => new(
        pass1ExecutionId,
        result.PairState!.Value.ToString(),
        result.StateVersion!.Value,
        result.RecoveredExpiredLeaseCount,
        result.ReleasedDueRetryCount,
        result.SkippedCompletedJobCount,
        result.PendingJobCount,
        result.Continued,
        result.IsReplay,
        result.ManualResumeAction);

    private static async Task<bool> HasManagementAccessAsync(
        IAuthorizationService authorization,
        HttpContext httpContext) =>
        (await authorization.AuthorizeAsync(
            httpContext.User,
            AuthorizationPolicyNames.ManagementAccess)).Succeeded;

    private static IResult NotFound() => Problem(
        404,
        "pass1-execution-not-found",
        "Pass 1 执行不存在或当前用户无权访问。");

    private static IResult Forbidden() => Problem(
        403,
        "pass1-execution-forbidden",
        "当前用户无权管理 Pass 1 执行。");

    private static IResult Unavailable() => Problem(
        503,
        "pass1-execution-unavailable",
        "Pass 1 执行服务当前不可用，请稍后重试。");

    private static IResult Problem(int status, string code, string detail)
    {
        ProblemDetails problem = new()
        {
            Status = status,
            Type = code,
            Title = "Pass 1 执行请求失败",
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        return Results.Json(problem, statusCode: status, contentType: "application/problem+json");
    }
}

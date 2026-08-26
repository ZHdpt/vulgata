using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;
using Vulgata.Shared.Scans;
using Vulgata.Web.Data;

namespace Vulgata.Web.Api.Scans;

internal static class Pass2ExecutionEndpoints
{
    public static void MapPass2ExecutionApiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/pass2-executions", StartAsync).RequireAuthorization();
        app.MapGet("/api/pass2-executions/{pass2ExecutionId}", GetStatusAsync)
            .RequireAuthorization();
        app.MapPost("/api/pass2-executions/{pass2ExecutionId}/resume", ResumeAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> StartAsync(
        StartPass2ExecutionRequest request,
        HttpContext httpContext,
        [FromServices] IValidator<StartPass2ExecutionRequest> validator,
        [FromServices] IAuthorizationService authorization,
        [FromServices] IUserContext user,
        [FromServices] PreparedPass2Coordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (!await HasManagementAccessAsync(authorization, httpContext))
        {
            return Forbidden();
        }

        if (!(await validator.ValidateAsync(request, cancellationToken)).IsValid)
        {
            return Problem(400, "invalid-pass2-start-request", "Pass 2 执行请求无效。");
        }

        PreparedPass2StartResult result = await coordinator.StartAsync(
            request.Pass1PairExecutionId,
            user,
            cancellationToken);
        return result.Outcome switch
        {
            PreparedPass2StartOutcome.Created when result.ExecutionId is Guid executionId =>
                Results.Created(
                    $"/api/pass2-executions/{executionId:D}",
                    ToStartResponse(executionId, result)),
            PreparedPass2StartOutcome.Replay when result.ExecutionId is Guid executionId =>
                Results.Ok(ToStartResponse(executionId, result)),
            PreparedPass2StartOutcome.NotFound => NotFound(),
            PreparedPass2StartOutcome.Conflict =>
                Problem(409, "pass2-start-conflict", "Pass 2 执行状态已变更，请刷新后重试。"),
            PreparedPass2StartOutcome.Rejected =>
                Problem(422, "pass2-start-rejected", "Pass 2 启动校验未通过。"),
            PreparedPass2StartOutcome.Cancelled =>
                Problem(409, "pass2-start-cancelled", "Pass 2 启动已取消。"),
            _ => Unavailable(),
        };
    }

    private static async Task<IResult> GetStatusAsync(
        Guid pass2ExecutionId,
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorization,
        [FromServices] IUserContext user,
        [FromServices] PreparedPass2Coordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (!await HasManagementAccessAsync(authorization, httpContext))
        {
            return Forbidden();
        }

        PreparedPass2ExecutionStatus? status = await coordinator.GetStatusAsync(
            pass2ExecutionId,
            user,
            cancellationToken);
        return status is null ? NotFound() : Results.Ok(ToStatusResponse(status));
    }

    private static async Task<IResult> ResumeAsync(
        Guid pass2ExecutionId,
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorization,
        [FromServices] IUserContext user,
        [FromServices] PreparedPass2Coordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (!await HasManagementAccessAsync(authorization, httpContext))
        {
            return Forbidden();
        }

        if (httpContext.Request.Query.ContainsKey("expectedStateVersion") ||
            httpContext.Request.ContentLength is > 0 ||
            httpContext.Request.Headers.TransferEncoding.Count > 0)
        {
            return Problem(
                409,
                "pass2-resume-state-conflict",
                "Pass 2 执行状态已变更，请刷新后重试。");
        }

        PreparedPass2RecoveryResult result = await coordinator.ResumeAsync(
            pass2ExecutionId,
            user,
            cancellationToken);
        return result.Outcome switch
        {
            PreparedPass2RecoveryOutcome.Continued or
                PreparedPass2RecoveryOutcome.Completed or
                PreparedPass2RecoveryOutcome.NoOpReplay
                when result.ExecutionId is Guid executionId &&
                    result.State is Pass2ExecutionState state &&
                    result.StateVersion is long stateVersion =>
                Results.Ok(new Pass2ExecutionResumeResponse(
                    executionId,
                    state.ToString(),
                    stateVersion,
                    result.RecoveredJobCount,
                    result.FailedJobCount,
                    result.IsReplay)),
            PreparedPass2RecoveryOutcome.NotFound => NotFound(),
            PreparedPass2RecoveryOutcome.PausedProviderUnavailable =>
                Problem(503, "pass2-provider-unavailable", "提供方当前不可用，请稍后手动继续。"),
            PreparedPass2RecoveryOutcome.Conflict =>
                Problem(409, "pass2-resume-conflict", "Pass 2 执行状态已变更，请刷新后重试。"),
            PreparedPass2RecoveryOutcome.Rejected =>
                Problem(422, "pass2-resume-rejected", "Pass 2 恢复校验未通过。"),
            PreparedPass2RecoveryOutcome.Cancelled =>
                Problem(409, "pass2-resume-cancelled", "Pass 2 恢复已取消。"),
            _ => Unavailable(),
        };
    }

    private static Pass2ExecutionStartResponse ToStartResponse(
        Guid executionId,
        PreparedPass2StartResult result) => new(
        executionId,
        result.FlowGroupCount,
        result.OmissionCount,
        result.JobCount,
        result.IsReplay);

    private static Pass2ExecutionStatusResponse ToStatusResponse(
        PreparedPass2ExecutionStatus status) => new(
        status.ExecutionId,
        status.State,
        status.StateVersion,
        status.PublishedCount + status.FailedCount + status.IncompleteCount +
            status.BoundExcludedCount + status.OmissionCount + status.PendingJobCount +
            status.RunningJobCount + status.RetryScheduledJobCount,
        status.PublishedCount,
        status.FailedCount,
        status.IncompleteCount,
        status.BoundExcludedCount,
        status.OmissionCount,
        status.PendingJobCount,
        status.RunningJobCount,
        status.RetryScheduledJobCount,
        status.Warnings.Select(warning => new Pass2ExecutionWarningResponse(
            warning.GroupIdentity,
            warning.Fingerprint,
            warning.ReasonCode)).ToArray());

    private static async Task<bool> HasManagementAccessAsync(
        IAuthorizationService authorization,
        HttpContext httpContext) =>
        (await authorization.AuthorizeAsync(
            httpContext.User,
            AuthorizationPolicyNames.ManagementAccess)).Succeeded;

    private static IResult NotFound() => Problem(
        404,
        "pass2-execution-not-found",
        "Pass 2 执行不存在或当前用户无权访问。");

    private static IResult Forbidden() => Problem(
        403,
        "pass2-execution-forbidden",
        "当前用户无权管理 Pass 2 执行。");

    private static IResult Unavailable() => Problem(
        503,
        "pass2-execution-unavailable",
        "Pass 2 执行服务当前不可用，请稍后重试。");

    private static IResult Problem(int status, string code, string detail)
    {
        ProblemDetails problem = new()
        {
            Status = status,
            Type = code,
            Title = "Pass 2 执行请求失败",
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        return Results.Json(problem, statusCode: status, contentType: "application/problem+json");
    }
}

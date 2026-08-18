using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Shared.Scans;

namespace Vulgata.Web.Api.Scans;

internal static class ManualResumeEndpoints
{
    public static void MapManualResumeApiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/scan-executions/{scanExecutionId}/resume", ResumeAsync);
    }

    private static async Task<IResult> ResumeAsync(
        string scanExecutionId,
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorization,
        [FromServices] IValidator<ManualResumeRequest> validator,
        [FromServices] IUserContext user,
        [FromServices] ISystemRepository systemRepository,
        [FromServices] IDurableScanStore durableScanStore,
        [FromServices] IScanRecoveryCoordinator recoveryCoordinator,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        AuthorizationResult policyResult = await authorization.AuthorizeAsync(
            httpContext.User,
            AuthorizationPolicyNames.ManagementAccess);
        if (!policyResult.Succeeded)
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "scan-resume-forbidden",
                "当前用户无权恢复扫描。");
        }

        ManualResumeRequest request = new(scanExecutionId);
        if (!(await validator.ValidateAsync(request, cancellationToken)).IsValid ||
            !Guid.TryParse(scanExecutionId, out Guid parsedScanExecutionId) ||
            parsedScanExecutionId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "invalid-scan-execution-id",
                "扫描执行标识无效。");
        }

        if (httpContext.Request.ContentLength is > 0 ||
            httpContext.Request.Headers.TransferEncoding.Count > 0)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "invalid-resume-request",
                "恢复扫描请求不得包含调用方状态。");
        }

        var visibleSystems = await systemRepository.ListVisibleAsync(
            user.ToSystemOwnerFilter(),
            cancellationToken);
        DurableScanAuthorizationScope authorizationScope = new(
            user.UserId,
            visibleSystems.Select(system => system.Id).ToArray());

        ScanRecoverySnapshotResult visibleSnapshot =
            await durableScanStore.ReadRecoverySnapshotAsync(
                authorizationScope,
                parsedScanExecutionId,
                cancellationToken);
        if (visibleSnapshot.Outcome == DurableScanStoreOutcome.NotFound)
        {
            return NotFound();
        }

        ScanRecoveryResult result = await recoveryCoordinator.ResumeAsync(
            parsedScanExecutionId,
            authorizationScope,
            cancellationToken);

        if (result.Outcome is DurableScanStoreOutcome.Applied or DurableScanStoreOutcome.Replay)
        {
            return ToSuccess(parsedScanExecutionId, result);
        }

        return result switch
        {
            { Outcome: DurableScanStoreOutcome.NotFound } => NotFound(),
            { Reason: DurableScanReason.Unauthorized } => NotFound(),
            { Outcome: DurableScanStoreOutcome.Conflict } => Problem(
                StatusCodes.Status409Conflict,
                "scan-resume-conflict",
                "扫描状态已变化，请刷新后重试。"),
            { Reason: DurableScanReason.IntegrityViolation or DurableScanReason.InvalidIdentity } => Problem(
                StatusCodes.Status422UnprocessableEntity,
                "scan-resume-integrity-rejected",
                "扫描恢复校验未通过，已保留现有文档。"),
            { Reason: DurableScanReason.ProviderUnavailable } => Problem(
                StatusCodes.Status503ServiceUnavailable,
                "scan-provider-unavailable",
                "扫描提供商当前不可用，请稍后重试。"),
            _ => Problem(
                StatusCodes.Status503ServiceUnavailable,
                "analysis-runtime-not-ready",
                "分析运行时尚未就绪，请联系管理员。"),
        };
    }

    private static IResult ToSuccess(Guid scanExecutionId, ScanRecoveryResult result)
    {
        ScanRecoveryView? view = result.View;
        if (view is null ||
            view.ScanExecutionId != scanExecutionId ||
            HasNegativeCount(view))
        {
            return Problem(
                StatusCodes.Status422UnprocessableEntity,
                "scan-resume-integrity-rejected",
                "扫描恢复结果不完整，已保留现有文档。");
        }

        return Results.Ok(new ManualResumeResponse(
            view.ScanExecutionId,
            view.Phase.ToString(),
            view.PendingJobCount,
            view.RunningJobCount,
            view.RetryScheduledJobCount,
            view.CompletedJobCount,
            view.FailedJobCount,
            view.CancelledJobCount,
            view.RecoveredExpiredLeaseCount,
            view.ReleasedDueRetryCount,
            view.StateChanged,
            ToReasonCode(view.Reason)));
    }

    private static bool HasNegativeCount(ScanRecoveryView view) =>
        view.TotalJobCount < 0 ||
        view.PendingJobCount < 0 ||
        view.RunningJobCount < 0 ||
        view.RetryScheduledJobCount < 0 ||
        view.CompletedJobCount < 0 ||
        view.FailedJobCount < 0 ||
        view.CancelledJobCount < 0 ||
        view.RecoveredExpiredLeaseCount < 0 ||
        view.ReleasedDueRetryCount < 0;

    private static string? ToReasonCode(DurableScanReason reason) => reason switch
    {
        DurableScanReason.None => null,
        DurableScanReason.StaleStateVersion => "stale-state-version",
        DurableScanReason.LeaseMismatch => "lease-mismatch",
        DurableScanReason.IntegrityViolation => "integrity-violation",
        DurableScanReason.ProviderUnavailable => "provider-unavailable",
        DurableScanReason.Cancelled => "cancelled",
        DurableScanReason.Failed => "failed",
        DurableScanReason.InvalidIdentity => "invalid-identity",
        DurableScanReason.Unauthorized => "unauthorized",
        _ => "failed",
    };

    private static IResult NotFound() => Problem(
        StatusCodes.Status404NotFound,
        "scan-execution-not-found",
        "扫描执行不存在或当前用户无权访问。");

    private static IResult Problem(int statusCode, string code, string detail)
    {
        ProblemDetails problem = new()
        {
            Status = statusCode,
            Type = code,
            Title = "扫描恢复请求失败",
            Detail = detail,
        };
        problem.Extensions["code"] = code;

        return Results.Json(
            problem,
            statusCode: statusCode,
            contentType: "application/problem+json");
    }
}

using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulgata.Core.DomainServices;
using Vulgata.Shared;
using Vulgata.Shared.Scans;
using Vulgata.Web.Data;

namespace Vulgata.Web.Api.Scans;

public static class PreparedScanEndpoints
{
    public static void MapPreparedScanApiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/systems/{systemId:guid}/prepared-scans", PrepareAsync)
            .RequireAuthorization(AuthorizationPolicyNames.ManagementAccess);
    }

    private static async Task<IResult> PrepareAsync(
        Guid systemId,
        PreparedScanRequest request,
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorization,
        [FromServices] IValidator<PreparedScanRequest> validator,
        [FromServices] IUserContext user,
        [FromServices] PreparedScanCoordinator coordinator,
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
                PreparedScanProblemCodes.Forbidden,
                "当前用户无权准备扫描。");
        }

        FluentValidation.Results.ValidationResult validation =
            await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid || systemId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                PreparedScanProblemCodes.InvalidRequest,
                string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct()));
        }

        PreparedScanPreparationResult result = await coordinator.PrepareAsync(
            systemId,
            request,
            user,
            cancellationToken);
        return result.Outcome switch
        {
            PreparedScanPreparationOutcome.Created when result.Response is not null =>
                Results.Created(
                    $"/api/systems/{systemId:D}/prepared-scans/{result.Response.CoordinationId:D}",
                    result.Response),
            PreparedScanPreparationOutcome.Replay when result.Response is not null =>
                Results.Ok(result.Response),
            PreparedScanPreparationOutcome.NotFound => Problem(
                StatusCodes.Status404NotFound,
                PreparedScanProblemCodes.NotFound,
                "系统或仓库不存在，或当前用户无权访问。"),
            PreparedScanPreparationOutcome.Conflict => Problem(
                StatusCodes.Status409Conflict,
                result.FailureCode ?? PreparedScanProblemCodes.Conflict,
                "准备输入或已批准包已变更，请刷新后重试。"),
            PreparedScanPreparationOutcome.Rejected => Problem(
                StatusCodes.Status422UnprocessableEntity,
                result.FailureCode ?? PreparedScanProblemCodes.Rejected,
                "扫描准备校验未通过，未产生已批准交接。"),
            _ => Problem(
                StatusCodes.Status503ServiceUnavailable,
                result.FailureCode ?? PreparedScanProblemCodes.Unavailable,
                "分析运行时当前不可用，请稍后重试。"),
        };
    }

    private static IResult Problem(int statusCode, string code, string detail)
    {
        ProblemDetails problem = new()
        {
            Status = statusCode,
            Type = code,
            Title = "扫描准备请求失败",
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        return Results.Json(
            problem,
            statusCode: statusCode,
            contentType: "application/problem+json");
    }

    private static class PreparedScanProblemCodes
    {
        internal const string InvalidRequest = "invalid-prepared-scan-request";
        internal const string Forbidden = "prepared-scan-forbidden";
        internal const string NotFound = "prepared-scan-not-found";
        internal const string Conflict = "prepared-scan-conflict";
        internal const string Rejected = "prepared-scan-rejected";
        internal const string Unavailable = "prepared-scan-unavailable";
    }
}

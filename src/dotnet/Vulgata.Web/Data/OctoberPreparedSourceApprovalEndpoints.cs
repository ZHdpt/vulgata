using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Routing;

namespace Vulgata.Web.Data;

internal static class OctoberPreparedSourceApprovalEndpoints
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/october-prepared-source-approvals", IssueAsync);
        endpoints.MapPost("/api/october-prepared-source-approvals/verifications", VerifyAsync);
    }

    private static async Task<IResult> IssueAsync(
        HttpContext context,
        OctoberGeneratorCertificateVerifier verifier,
        OctoberPreparedSourceApprovalService service,
        CancellationToken cancellationToken)
    {
        OctoberPreparedSourceApprovalRequest? request =
            await ReadAsync<OctoberPreparedSourceApprovalRequest>(context, cancellationToken);
        if (request is null || string.IsNullOrWhiteSpace(request.TargetId))
            return BadRequest();

        OctoberGeneratorCertificateDecision certificate = await verifier.VerifyAsync(
            context, request.TargetId, cancellationToken);
        return certificate.Status switch
        {
            OctoberGeneratorCertificateStatus.Authorized =>
                await service.IssueAsync(request, cancellationToken),
            OctoberGeneratorCertificateStatus.Unauthenticated => Unauthorized(),
            OctoberGeneratorCertificateStatus.Forbidden => Forbidden(),
            _ => Unavailable()
        };
    }

    private static async Task<IResult> VerifyAsync(
        HttpContext context,
        OctoberGeneratorCertificateVerifier verifier,
        OctoberPreparedSourceApprovalService service,
        CancellationToken cancellationToken)
    {
        OctoberPreparedSourceVerificationRequest? request =
            await ReadAsync<OctoberPreparedSourceVerificationRequest>(context, cancellationToken);
        if (request is null || string.IsNullOrWhiteSpace(request.TargetId))
            return BadRequest();

        OctoberGeneratorCertificateDecision certificate = await verifier.VerifyAsync(
            context, request.TargetId, cancellationToken);
        return certificate.Status switch
        {
            OctoberGeneratorCertificateStatus.Authorized =>
                await service.VerifyAsync(request, cancellationToken),
            OctoberGeneratorCertificateStatus.Unauthenticated => Unauthorized(),
            OctoberGeneratorCertificateStatus.Forbidden => Forbidden(),
            _ => Unavailable()
        };
    }

    private static async Task<T?> ReadAsync<T>(HttpContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(
                context.Request.Body, RequestJsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static IResult BadRequest() =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest);

    private static IResult Unauthorized() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized);

    private static IResult Forbidden() =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden);

    private static IResult Unavailable() =>
        Results.Problem(title: "authority_unavailable",
            statusCode: StatusCodes.Status503ServiceUnavailable);
}

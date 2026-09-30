using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;

namespace Vulgata.Web.Data;

internal static class OctoberBatchActivationEndpoints
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/october-batch-activation-grants", IssueAsync);

        app.MapPost("/api/october-batch-activation-grants/validations", ValidateAsync);
    }

    private static async Task<IResult> IssueAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        OctoberBatchActivationService activation,
        CancellationToken cancellationToken)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden);
        }

        OctoberActivationSelection? selection = await ReadRequestAsync<OctoberActivationSelection>(
            context, cancellationToken);
        return selection is null
            ? Results.Problem(statusCode: StatusCodes.Status400BadRequest)
            : await activation.IssueAsync(context, selection, cancellationToken);
    }

    private static async Task<IResult> ValidateAsync(
        HttpContext context,
        OctoberGeneratorCertificateVerifier verifier,
        OctoberBatchActivationService activation,
        CancellationToken cancellationToken)
    {
        OctoberActivationValidationRequest? request =
            await ReadRequestAsync<OctoberActivationValidationRequest>(context, cancellationToken);
        if (request is null || string.IsNullOrWhiteSpace(request.TargetId))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest);
        }

        OctoberGeneratorCertificateDecision decision = await verifier.VerifyAsync(
            context,
            request.TargetId,
            cancellationToken);
        return decision.Status switch
        {
            OctoberGeneratorCertificateStatus.Unauthenticated => Unauthorized(),
            OctoberGeneratorCertificateStatus.Forbidden => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden),
            OctoberGeneratorCertificateStatus.Authorized =>
                await activation.ValidateAsync(request, cancellationToken),
            _ => Unavailable()
        };
    }

    private static async Task<T?> ReadRequestAsync<T>(
        HttpContext context,
        CancellationToken cancellationToken)
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

    private static IResult Unauthorized() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized);

    private static IResult Unavailable() =>
        Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable);
}

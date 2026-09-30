using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vulgata.Infrastructure.Data;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

internal sealed record OctoberActivationRepository(
    Guid RepositoryId, Guid? SystemId, string? RootBindingIdentity, string? OperatorLabel);

internal sealed record OctoberActivationContext(string? Scope, Guid RecordId);

internal sealed record OctoberActivationSelection(
    string? TargetId, Guid BatchId,
    IReadOnlyList<OctoberActivationRepository>? Repositories,
    IReadOnlyList<OctoberActivationContext>? ContextSelections);

internal sealed record OctoberActivationValidationRequest(
    string? ActivationGrant, string? TargetId, Guid BatchId,
    IReadOnlyList<OctoberActivationRepository>? Repositories,
    IReadOnlyList<OctoberActivationContext>? ContextSelections)
{
    public OctoberActivationSelection Selection =>
        new(TargetId, BatchId, Repositories, ContextSelections);
}

internal sealed record OctoberActivationGrantPayload(
    string Purpose, string SubjectId, string TargetId, Guid BatchId,
    string SelectionDigest, DateTimeOffset ExpiresAtUtc);

internal sealed class OctoberBatchActivationService(
    IAuthorizationService authorization,
    UserManager<ApplicationUser> userManager,
    VulgataDbContext database,
    IDataProtectionProvider dataProtection,
    OctoberWebTargetAllowlist targets,
    TimeProvider clock)
{
    private const string GrantPurpose = "october-batch-activation-v1";
    private static readonly TimeSpan GrantLifetime = TimeSpan.FromMinutes(5);

    public async Task<IResult> IssueAsync(
        HttpContext context,
        OctoberActivationSelection selection,
        CancellationToken cancellationToken)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Problem(StatusCodes.Status401Unauthorized, "unauthenticated");
        }

        if (!IsValidSelection(selection))
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid_selection");
        }

        try
        {
            if (!(await authorization.AuthorizeAsync(
                    context.User, AuthorizationPolicyNames.ManagementAccess)).Succeeded)
            {
                return Problem(StatusCodes.Status403Forbidden, "not_authorized");
            }

            string? subjectId = userManager.GetUserId(context.User);
            if (string.IsNullOrWhiteSpace(subjectId) ||
                !await HasCurrentSelectionAuthorityAsync(subjectId, selection, cancellationToken))
            {
                return Problem(StatusCodes.Status403Forbidden, "not_authorized");
            }

            string digest = SelectionDigest(selection);
            DateTimeOffset expiry = clock.GetUtcNow().Add(GrantLifetime);
            OctoberActivationGrantPayload payload = new(
                GrantPurpose, subjectId, selection.TargetId!, selection.BatchId, digest, expiry);
            string grant = dataProtection.CreateProtector(GrantPurpose)
                .Protect(JsonSerializer.Serialize(payload));
            return Results.Json(new
            {
                activationGrant = grant,
                expiresAtUtc = expiry,
                targetId = selection.TargetId,
                batchId = selection.BatchId,
                selectionDigest = digest,
                subjectId
            }, statusCode: StatusCodes.Status201Created);
        }
        catch (OperationCanceledException)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "cancelled");
        }
        catch (Exception)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "authority_unavailable");
        }
    }

    public async Task<IResult> ValidateAsync(
        OctoberActivationValidationRequest request,
        CancellationToken cancellationToken)
    {
        OctoberActivationSelection selection = request.Selection;
        if (!IsValidSelection(selection) || string.IsNullOrWhiteSpace(request.ActivationGrant))
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid_selection");
        }

        OctoberActivationGrantPayload? payload;
        try
        {
            string plaintext = dataProtection.CreateProtector(GrantPurpose)
                .Unprotect(request.ActivationGrant);
            payload = JsonSerializer.Deserialize<OctoberActivationGrantPayload>(plaintext);
        }
        catch (CryptographicException)
        {
            return Problem(StatusCodes.Status403Forbidden, "invalid_grant");
        }
        catch (JsonException)
        {
            return Problem(StatusCodes.Status403Forbidden, "invalid_grant");
        }

        if (payload is null || payload.Purpose != GrantPurpose ||
            payload.ExpiresAtUtc <= clock.GetUtcNow() ||
            !string.Equals(payload.TargetId, selection.TargetId, StringComparison.Ordinal) ||
            payload.BatchId != selection.BatchId ||
            !string.Equals(payload.SelectionDigest, SelectionDigest(selection), StringComparison.Ordinal))
        {
            return Problem(StatusCodes.Status403Forbidden, "invalid_grant");
        }

        try
        {
            if (!await HasCurrentSelectionAuthorityAsync(payload.SubjectId, selection, cancellationToken))
            {
                return Problem(StatusCodes.Status403Forbidden, "not_authorized");
            }

            return Results.Ok(new
            {
                subjectId = payload.SubjectId,
                targetId = selection.TargetId,
                batchId = selection.BatchId,
                canonicalDatabaseIdentity = targets.CanonicalDatabaseIdentities[selection.TargetId!],
                repositoryIds = selection.Repositories!.Select(repository => repository.RepositoryId).ToArray(),
                selectionDigest = payload.SelectionDigest,
                expiresAtUtc = payload.ExpiresAtUtc
            });
        }
        catch (OperationCanceledException)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "cancelled");
        }
        catch (Exception)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "authority_unavailable");
        }
    }

    private bool IsValidSelection(OctoberActivationSelection selection)
    {
        if (string.IsNullOrWhiteSpace(selection.TargetId) ||
            !targets.CanonicalDatabaseIdentities.ContainsKey(selection.TargetId) ||
            selection.BatchId == Guid.Empty ||
            selection.Repositories is not { Count: > 0 and <= 100 } ||
            selection.ContextSelections is not { Count: <= 400 })
        {
            return false;
        }

        HashSet<Guid> repositoryIds = [];
        foreach (OctoberActivationRepository repository in selection.Repositories)
        {
            if (repository is null || repository.RepositoryId == Guid.Empty ||
                repository.SystemId == Guid.Empty ||
                string.IsNullOrWhiteSpace(repository.RootBindingIdentity) ||
                string.IsNullOrWhiteSpace(repository.OperatorLabel) ||
                !repositoryIds.Add(repository.RepositoryId))
            {
                return false;
            }
        }

        HashSet<(string, Guid)> contexts = [];
        foreach (OctoberActivationContext context in selection.ContextSelections)
        {
            if (context is null || context.RecordId == Guid.Empty ||
                context.Scope is not ("global" or "system" or "repository") ||
                !contexts.Add((context.Scope, context.RecordId)))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> HasCurrentSelectionAuthorityAsync(
        string subjectId,
        OctoberActivationSelection selection,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(subjectId);
        if (user is null)
        {
            return false;
        }

        bool administrator = await userManager.IsInRoleAsync(user, RoleNames.Administrator);
        bool systemOwner = await userManager.IsInRoleAsync(user, RoleNames.SystemOwner);
        if (!administrator && !systemOwner)
        {
            return false;
        }

        foreach (OctoberActivationRepository selected in selection.Repositories!)
        {
            Vulgata.Core.Entities.Repository? repository = await database.Repositories
                .AsNoTracking().SingleOrDefaultAsync(value => value.Id == selected.RepositoryId,
                    cancellationToken);
            if (repository is null || repository.SystemId != selected.SystemId)
            {
                return false;
            }

            if (selected.SystemId is Guid systemId &&
                (!await database.Systems.AsNoTracking().AnyAsync(value => value.Id == systemId,
                    cancellationToken) ||
                 (!administrator && !await database.SystemOwnerAssignments.AsNoTracking()
                     .AnyAsync(value => value.SystemId == systemId && value.UserId == subjectId,
                         cancellationToken))))
            {
                return false;
            }
        }

        foreach (OctoberActivationContext context in selection.ContextSelections!)
        {
            bool current = context.Scope switch
            {
                "global" => await database.GlobalContexts.AsNoTracking()
                    .AnyAsync(value => value.Id == context.RecordId, cancellationToken),
                "system" => selection.Repositories!.Any(repository =>
                    repository.SystemId == context.RecordId) &&
                    await database.Systems.AsNoTracking()
                        .AnyAsync(value => value.Id == context.RecordId, cancellationToken),
                "repository" => selection.Repositories!.Any(repository =>
                    repository.RepositoryId == context.RecordId),
                _ => false
            };
            if (!current)
            {
                return false;
            }
        }

        return true;
    }

    private static string SelectionDigest(OctoberActivationSelection selection)
    {
        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            targetId = selection.TargetId,
            batchId = selection.BatchId,
            repositories = selection.Repositories!
                .OrderBy(repository => repository.RepositoryId)
                .Select(repository => new
                {
                    repository.RepositoryId,
                    repository.SystemId,
                    repository.RootBindingIdentity,
                    repository.OperatorLabel
                }),
            contexts = selection.ContextSelections!
                .OrderBy(context => context.Scope, StringComparer.Ordinal)
                .ThenBy(context => context.RecordId)
                .Select(context => new { context.Scope, context.RecordId })
        });
        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    private static IResult Problem(int status, string code) =>
        Results.Problem(title: code, statusCode: status);
}

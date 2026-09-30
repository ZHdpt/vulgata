using System.Collections.Immutable;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
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
    string SelectionDigest, DateTimeOffset ExpiresAtUtc,
    OctoberSetupRequest Selection,
    OctoberSelectedAdvisoryContext SelectedContext);

internal sealed record OctoberWebManagementAuthority(string Id);

internal sealed class OctoberBatchActivationService(
    IAuthorizationService authorization,
    UserManager<ApplicationUser> userManager,
    VulgataDbContext database,
    IDataProtectionProvider dataProtection,
    OctoberWebTargetAllowlist targets,
    OctoberWebManagementAuthority managementAuthority,
    TimeProvider clock)
{
    private const string GrantPurpose = "october-batch-activation-v2";
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
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                return Problem(StatusCodes.Status403Forbidden, "not_authorized");
            }

            OctoberSelectedAdvisoryContext? selectedContext = await CaptureCurrentSelectionAsync(
                subjectId, selection, cancellationToken);
            if (selectedContext is null)
            {
                return Problem(StatusCodes.Status403Forbidden, "not_authorized");
            }

            OctoberSetupRequest setupRequest = ToSetupRequest(selection);
            string digest = OctoberSelectionDigest.Compute(setupRequest, selectedContext);
            setupRequest = setupRequest with { SelectionDigest = digest };
            DateTimeOffset expiry = clock.GetUtcNow().Add(GrantLifetime);
            OctoberActivationGrantPayload payload = new(
                GrantPurpose, subjectId, selection.TargetId!, selection.BatchId, digest, expiry,
                setupRequest, selectedContext);
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
            payload.Selection is null || payload.SelectedContext is null ||
            payload.ExpiresAtUtc <= clock.GetUtcNow() ||
            !string.Equals(payload.TargetId, selection.TargetId, StringComparison.Ordinal) ||
            payload.BatchId != selection.BatchId ||
            !string.Equals(payload.SelectedContext.ManagementAuthorityId,
                managementAuthority.Id, StringComparison.Ordinal))
        {
            return Problem(StatusCodes.Status403Forbidden, "invalid_grant");
        }

        try
        {
            OctoberSetupRequest requested = ToSetupRequest(selection);
            string protectedDigest = OctoberSelectionDigest.Compute(payload.Selection, payload.SelectedContext);
            string requestedDigest = OctoberSelectionDigest.Compute(requested, payload.SelectedContext);
            if (!string.Equals(protectedDigest, payload.SelectionDigest, StringComparison.Ordinal) ||
                !string.Equals(requestedDigest, payload.SelectionDigest, StringComparison.Ordinal) ||
                !string.Equals(payload.Selection.SelectionDigest, payload.SelectionDigest, StringComparison.Ordinal))
            {
                return Problem(StatusCodes.Status403Forbidden, "invalid_grant");
            }

            OctoberSelectedAdvisoryContext? liveContext = await CaptureCurrentSelectionAsync(
                payload.SubjectId, selection, cancellationToken);
            if (liveContext is null ||
                !string.Equals(OctoberSelectionDigest.Compute(requested, liveContext),
                    payload.SelectionDigest, StringComparison.Ordinal))
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
                selectedContext = ToResponseContext(payload.SelectedContext),
                expiresAtUtc = payload.ExpiresAtUtc
            });
        }
        catch (ArgumentException)
        {
            return Problem(StatusCodes.Status403Forbidden, "invalid_grant");
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

    private async Task<OctoberSelectedAdvisoryContext?> CaptureCurrentSelectionAsync(
        string subjectId,
        OctoberActivationSelection selection,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(subjectId);
        if (user is null)
        {
            return null;
        }

        bool administrator = await userManager.IsInRoleAsync(user, RoleNames.Administrator);
        bool systemOwner = await userManager.IsInRoleAsync(user, RoleNames.SystemOwner);
        if (!administrator && !systemOwner)
        {
            return null;
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        Dictionary<Guid, Vulgata.Core.Entities.Repository> repositories = [];
        foreach (OctoberActivationRepository selected in selection.Repositories!)
        {
            Vulgata.Core.Entities.Repository? repository = await database.Repositories
                .AsNoTracking().SingleOrDefaultAsync(value => value.Id == selected.RepositoryId,
                    cancellationToken);
            if (repository is null || repository.SystemId != selected.SystemId)
            {
                return null;
            }

            repositories.Add(selected.RepositoryId, repository);

            if (selected.SystemId is Guid systemId &&
                (!await database.Systems.AsNoTracking().AnyAsync(value => value.Id == systemId,
                    cancellationToken) ||
                 (!administrator && !await database.SystemOwnerAssignments.AsNoTracking()
                     .AnyAsync(value => value.SystemId == systemId && value.UserId == subjectId,
                         cancellationToken))))
            {
                return null;
            }
        }

        Dictionary<Guid, string?> globalValues = [];
        Dictionary<Guid, string?> systemValues = [];
        Dictionary<Guid, string?> repositoryValues = [];
        foreach (OctoberActivationContext context in selection.ContextSelections!)
        {
            switch (context.Scope)
            {
                case "global":
                    Vulgata.Core.Entities.GlobalContext? global = await database.GlobalContexts
                        .AsNoTracking().SingleOrDefaultAsync(value => value.Id == context.RecordId,
                            cancellationToken);
                    if (global is null || globalValues.Count != 0) return null;
                    globalValues.Add(context.RecordId, global.Context);
                    break;
                case "system":
                    if (!selection.Repositories.Any(value => value.SystemId == context.RecordId)) return null;
                    Vulgata.Core.Entities.System? system = await database.Systems.AsNoTracking()
                        .SingleOrDefaultAsync(value => value.Id == context.RecordId, cancellationToken);
                    if (system is null) return null;
                    systemValues.Add(context.RecordId, system.Context);
                    break;
                case "repository":
                    if (!repositories.TryGetValue(context.RecordId, out var selectedRepository)) return null;
                    repositoryValues.Add(context.RecordId, selectedRepository.Context);
                    break;
                default:
                    return null;
            }
        }

        List<OctoberAdvisoryContextEntry> entries = [];
        List<OctoberContextDifference> differences = [];
        foreach (OctoberActivationRepository selected in selection.Repositories.OrderBy(
                     value => value.RepositoryId))
        {
            List<OctoberAdvisoryContextEntry> selectedEntries = [];
            AddEntry(OctoberContextScope.Global, globalValues.Count == 0 ? null : globalValues.Single(),
                notApplicable: false);
            AddEntry(OctoberContextScope.System,
                selected.SystemId is Guid systemId && systemValues.TryGetValue(systemId, out string? systemValue)
                    ? new KeyValuePair<Guid, string?>(systemId, systemValue) : null,
                notApplicable: selected.SystemId is null);
            AddEntry(OctoberContextScope.Repository,
                repositoryValues.TryGetValue(selected.RepositoryId, out string? repositoryValue)
                    ? new KeyValuePair<Guid, string?>(selected.RepositoryId, repositoryValue) : null,
                notApplicable: false);

            for (int left = 0; left < selectedEntries.Count; left++)
            for (int right = left + 1; right < selectedEntries.Count; right++)
            {
                if (!string.Equals(selectedEntries[left].Value, selectedEntries[right].Value,
                        StringComparison.Ordinal))
                    differences.Add(new OctoberContextDifference(selected.RepositoryId,
                        selectedEntries[left].Scope, selectedEntries[right].Scope));
            }

            void AddEntry(OctoberContextScope scope, KeyValuePair<Guid, string?>? selectedValue,
                bool notApplicable)
            {
                OctoberAdvisoryContextEntry entry = new(selected.RepositoryId, scope,
                    notApplicable ? OctoberContextApplicability.NotApplicable : selectedValue is null
                        ? OctoberContextApplicability.NotSelected : OctoberContextApplicability.Selected,
                    selectedValue?.Key, selectedValue?.Value,
                    OctoberContextEvidenceKind.AdvisoryNotSourceProof);
                entries.Add(entry);
                if (selectedValue is not null) selectedEntries.Add(entry);
            }
        }

        return new OctoberSelectedAdvisoryContext(
            managementAuthority.Id,
            selection.Repositories.Select(value => new OctoberRepositorySelection(
                value.RepositoryId, value.SystemId, value.RootBindingIdentity!, value.OperatorLabel!))
                .OrderBy(value => value.RepositoryId).ToImmutableArray(),
            entries.ToImmutableArray(),
            differences.ToImmutableArray());
    }

    private static OctoberSetupRequest ToSetupRequest(OctoberActivationSelection selection)
    {
        return new OctoberSetupRequest(selection.TargetId!, selection.BatchId,
            selection.Repositories!.Select(value => new OctoberRepositorySelection(
                value.RepositoryId, value.SystemId, value.RootBindingIdentity!, value.OperatorLabel!))
                .ToImmutableArray(),
            selection.ContextSelections!.Select(value => new OctoberContextSelection(value.Scope switch
            {
                "global" => OctoberContextScope.Global,
                "system" => OctoberContextScope.System,
                "repository" => OctoberContextScope.Repository,
                _ => throw new ArgumentException("Invalid context scope.")
            }, value.RecordId)).ToImmutableArray(),
            string.Empty);
    }

    private static object ToResponseContext(OctoberSelectedAdvisoryContext context) => new
    {
        managementAuthorityId = context.ManagementAuthorityId,
        repositories = context.Repositories.Select(value => new
        {
            repositoryId = value.RepositoryId,
            systemId = value.SystemId,
            rootBindingIdentity = value.RootBindingIdentity,
            operatorLabel = value.OperatorLabel
        }),
        entries = context.Entries.Select(value => new
        {
            repositoryId = value.RepositoryId,
            scope = ScopeName(value.Scope),
            applicability = value.Applicability switch
            {
                OctoberContextApplicability.Selected => "selected",
                OctoberContextApplicability.NotSelected => "not-selected",
                _ => "not-applicable"
            },
            originRecordId = value.OriginRecordId,
            value = value.Value,
            evidenceKind = "advisory-not-source-proof"
        }),
        differences = context.Differences.Select(value => new
        {
            repositoryId = value.RepositoryId,
            leftScope = ScopeName(value.LeftScope),
            rightScope = ScopeName(value.RightScope)
        })
    };

    private static string ScopeName(OctoberContextScope scope) => scope switch
    {
        OctoberContextScope.Global => "global",
        OctoberContextScope.System => "system",
        _ => "repository"
    };

    private static IResult Problem(int status, string code) =>
        Results.Problem(title: code, statusCode: status);
}

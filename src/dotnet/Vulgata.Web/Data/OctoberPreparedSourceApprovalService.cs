using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure.Data.October;
using Vulgata.Shared;

namespace Vulgata.Web.Data;

internal sealed record OctoberPreparedSourceApprovalRequest(
    string? ActivationGrant,
    string? TargetId,
    Guid BatchId,
    Guid RepositoryId,
    string? RootBindingIdentity);

internal sealed record OctoberPreparedSourceVerificationRequest(
    string? TargetId,
    Guid BatchId,
    Guid RepositoryId,
    string? SubjectId,
    PreparedSourcePurpose Purpose,
    string? ApprovalIdentity,
    string? RootBindingIdentity,
    string? OperatorLabel);

internal sealed class OctoberPreparedSourceApprovalService(
    IOctoberBoundSetupData boundSetupData,
    OctoberDbContext october,
    VulgataDbContext management,
    UserManager<ApplicationUser> users,
    IDataProtectionProvider protection,
    OctoberWebTargetAllowlist targets,
    OctoberWebManagementAuthority managementAuthority,
    TimeProvider clock)
{
    private const string ActivationPurpose = "october-batch-activation-v2";
    private static readonly TimeSpan ApprovalLifetime = TimeSpan.FromMinutes(10);

    public async Task<IResult> IssueAsync(
        OctoberPreparedSourceApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ActivationGrant) ||
            string.IsNullOrWhiteSpace(request.TargetId) || request.BatchId == Guid.Empty ||
            request.RepositoryId == Guid.Empty || string.IsNullOrWhiteSpace(request.RootBindingIdentity))
            return BadRequest();

        if (!IsBoundTarget(request.TargetId, request.BatchId))
            return Forbidden();

        OctoberActivationGrantPayload? grant;
        try
        {
            string plaintext = protection.CreateProtector(ActivationPurpose)
                .Unprotect(request.ActivationGrant);
            grant = JsonSerializer.Deserialize<OctoberActivationGrantPayload>(plaintext);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return Forbidden();
        }

        if (grant is null || grant.Purpose != ActivationPurpose ||
            grant.ExpiresAtUtc <= clock.GetUtcNow() ||
            grant.Selection is null || grant.SelectedContext is null ||
            !string.Equals(grant.TargetId, request.TargetId, StringComparison.Ordinal) ||
            grant.BatchId != request.BatchId ||
            !string.Equals(grant.SelectedContext.ManagementAuthorityId,
                managementAuthority.Id, StringComparison.Ordinal))
            return Forbidden();

        try
        {
            if (!string.Equals(OctoberSelectionDigest.Compute(grant.Selection, grant.SelectedContext),
                    grant.SelectionDigest, StringComparison.Ordinal) ||
                !string.Equals(grant.Selection.SelectionDigest,
                    grant.SelectionDigest, StringComparison.Ordinal) ||
                grant.SelectedContext.Repositories.Length != grant.Selection.Repositories.Count ||
                grant.Selection.Repositories.Any(value =>
                    !grant.SelectedContext.Repositories.Contains(value)) ||
                !grant.Selection.Repositories.Any(value => value.RepositoryId == request.RepositoryId &&
                    string.Equals(value.RootBindingIdentity, request.RootBindingIdentity,
                        StringComparison.Ordinal)))
                return Forbidden();

            OctoberBatchRecord? batch = await FindBoundBatchAsync(request.TargetId, request.BatchId,
                cancellationToken);
            OctoberRepositoryRecord? binding = await FindBindingAsync(request.BatchId,
                request.RepositoryId, cancellationToken);
            if (batch is null || binding is null ||
                !string.Equals(batch.SubjectId, grant.SubjectId, StringComparison.Ordinal) ||
                !string.Equals(batch.ManagementAuthorityId,
                    managementAuthority.Id, StringComparison.Ordinal) ||
                !string.Equals(binding.RootBindingIdentity,
                    request.RootBindingIdentity, StringComparison.Ordinal) ||
                !await MatchesStoredReferencesAsync(grant.Selection, cancellationToken) ||
                !await HasCurrentAuthorityAsync(grant.SubjectId, binding, cancellationToken) ||
                !await HasCurrentAdvisoryValuesAsync(grant.SelectedContext, cancellationToken))
                return Forbidden();

            string approvalIdentity = Guid.NewGuid().ToString("N");
            DateTimeOffset expiresAtUtc = clock.GetUtcNow().Add(ApprovalLifetime);
            october.PreparedSourceApprovals.Add(new OctoberPreparedSourceApprovalRecord
            {
                ApprovalIdentity = approvalIdentity,
                BatchId = request.BatchId,
                RepositoryId = request.RepositoryId,
                SubjectId = grant.SubjectId,
                Purpose = (int)PreparedSourcePurpose.WorkerSourceRead,
                RootBindingIdentity = binding.RootBindingIdentity,
                OperatorLabel = binding.OperatorLabel,
                ExpiresAtUtc = expiresAtUtc,
                IsRevoked = false
            });
            await october.SaveChangesAsync(cancellationToken);
            return Results.Json(new
            {
                approvalIdentity,
                batchId = request.BatchId,
                repositoryId = request.RepositoryId,
                subjectId = grant.SubjectId,
                purpose = (int)PreparedSourcePurpose.WorkerSourceRead,
                rootBindingIdentity = binding.RootBindingIdentity,
                operatorLabel = binding.OperatorLabel,
                expiresAtUtc
            }, statusCode: StatusCodes.Status201Created);
        }
        catch (ArgumentException)
        {
            return Forbidden();
        }
        catch (OperationCanceledException)
        {
            return Unavailable("cancelled");
        }
        catch (Exception)
        {
            return Unavailable();
        }
    }

    public async Task<IResult> VerifyAsync(
        OctoberPreparedSourceVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TargetId) || request.BatchId == Guid.Empty ||
            request.RepositoryId == Guid.Empty || string.IsNullOrWhiteSpace(request.SubjectId) ||
            string.IsNullOrWhiteSpace(request.ApprovalIdentity) ||
            string.IsNullOrWhiteSpace(request.RootBindingIdentity) ||
            string.IsNullOrWhiteSpace(request.OperatorLabel) ||
            request.Purpose != PreparedSourcePurpose.WorkerSourceRead)
            return Forbidden();

        if (!IsBoundTarget(request.TargetId, request.BatchId))
            return Forbidden();

        try
        {
            OctoberPreparedSourceApprovalRecord? approval = await october.PreparedSourceApprovals
                .AsNoTracking().SingleOrDefaultAsync(value =>
                    value.ApprovalIdentity == request.ApprovalIdentity, cancellationToken);
            OctoberBatchRecord? batch = await FindBoundBatchAsync(request.TargetId, request.BatchId,
                cancellationToken);
            OctoberRepositoryRecord? binding = await FindBindingAsync(request.BatchId,
                request.RepositoryId, cancellationToken);
            if (approval is null || batch is null || binding is null || approval.IsRevoked ||
                approval.ExpiresAtUtc <= clock.GetUtcNow() ||
                approval.BatchId != request.BatchId || approval.RepositoryId != request.RepositoryId ||
                approval.Purpose != (int)request.Purpose ||
                !string.Equals(approval.SubjectId, request.SubjectId, StringComparison.Ordinal) ||
                !string.Equals(approval.RootBindingIdentity,
                    request.RootBindingIdentity, StringComparison.Ordinal) ||
                !string.Equals(approval.OperatorLabel, request.OperatorLabel, StringComparison.Ordinal) ||
                !string.Equals(batch.SubjectId, request.SubjectId, StringComparison.Ordinal) ||
                !string.Equals(batch.ManagementAuthorityId,
                    managementAuthority.Id, StringComparison.Ordinal) ||
                !string.Equals(binding.RootBindingIdentity,
                    request.RootBindingIdentity, StringComparison.Ordinal) ||
                !string.Equals(binding.OperatorLabel, request.OperatorLabel, StringComparison.Ordinal) ||
                !await HasCurrentAuthorityAsync(request.SubjectId, binding, cancellationToken))
                return Forbidden();

            return Results.Ok(new { status = "current" });
        }
        catch (OperationCanceledException)
        {
            return Unavailable("cancelled");
        }
        catch (Exception)
        {
            return Unavailable();
        }
    }

    private bool IsBoundTarget(string targetId, Guid batchId) =>
        string.Equals(boundSetupData.TargetId, targetId, StringComparison.Ordinal) &&
        boundSetupData.BatchId == batchId &&
        targets.CanonicalDatabaseIdentities.ContainsKey(targetId);

    private async Task<OctoberBatchRecord?> FindBoundBatchAsync(
        string targetId, Guid batchId, CancellationToken cancellationToken)
    {
        OctoberBatchRecord? batch = await october.Batches.AsNoTracking()
            .SingleOrDefaultAsync(value => value.BatchId == batchId &&
                value.State == OctoberBatchState.Selectable, cancellationToken);
        return batch is not null &&
            string.Equals(batch.TargetId, targetId, StringComparison.Ordinal) &&
            string.Equals(batch.CanonicalDatabaseIdentity,
                targets.CanonicalDatabaseIdentities[targetId], StringComparison.Ordinal)
                ? batch : null;
    }

    private Task<OctoberRepositoryRecord?> FindBindingAsync(
        Guid batchId, Guid repositoryId, CancellationToken cancellationToken) =>
        october.RepositorySelections.AsNoTracking().SingleOrDefaultAsync(value =>
            value.BatchId == batchId && value.RepositoryId == repositoryId, cancellationToken);

    private async Task<bool> MatchesStoredReferencesAsync(
        OctoberSetupRequest selection, CancellationToken cancellationToken)
    {
        OctoberContextReferenceRecord[] stored = await october.ContextReferences.AsNoTracking()
            .Where(value => value.BatchId == selection.BatchId).ToArrayAsync(cancellationToken);
        return stored.Length == selection.ContextSelections.Count &&
            selection.ContextSelections.All(selected => stored.Any(value =>
                value.Scope == (int)selected.Scope && value.RecordId == selected.RecordId));
    }

    private async Task<bool> HasCurrentAuthorityAsync(
        string subjectId, OctoberRepositoryRecord binding, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await users.FindByIdAsync(subjectId);
        if (user is null) return false;
        bool administrator = await users.IsInRoleAsync(user, RoleNames.Administrator);
        bool systemOwner = await users.IsInRoleAsync(user, RoleNames.SystemOwner);
        if (!administrator && !systemOwner) return false;

        Vulgata.Core.Entities.Repository? current = await management.Repositories.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == binding.RepositoryId, cancellationToken);
        if (current is null || current.SystemId != binding.SystemId) return false;
        if (binding.SystemId is not Guid systemId) return true;
        return await management.Systems.AsNoTracking().AnyAsync(value => value.Id == systemId,
                cancellationToken) &&
            (administrator || await management.SystemOwnerAssignments.AsNoTracking().AnyAsync(
                value => value.SystemId == systemId && value.UserId == subjectId,
                cancellationToken));
    }

    private async Task<bool> HasCurrentAdvisoryValuesAsync(
        OctoberSelectedAdvisoryContext selectedContext, CancellationToken cancellationToken)
    {
        foreach (OctoberAdvisoryContextEntry entry in selectedContext.Entries)
        {
            if (entry.Applicability != OctoberContextApplicability.Selected) continue;
            if (entry.OriginRecordId is not Guid origin) return false;
            (bool exists, string? current) = entry.Scope switch
            {
                OctoberContextScope.Global => await ReadGlobalAsync(origin, cancellationToken),
                OctoberContextScope.System => await ReadSystemAsync(origin, cancellationToken),
                OctoberContextScope.Repository => await ReadRepositoryAsync(origin, cancellationToken),
                _ => (false, null)
            };
            if (!exists || !string.Equals(current, entry.Value, StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private async Task<(bool Exists, string? Value)> ReadGlobalAsync(Guid id, CancellationToken cancellationToken)
    {
        Vulgata.Core.Entities.GlobalContext? value = await management.GlobalContexts.AsNoTracking()
            .SingleOrDefaultAsync(context => context.Id == id, cancellationToken);
        return (value is not null, value?.Context);
    }

    private async Task<(bool Exists, string? Value)> ReadSystemAsync(Guid id, CancellationToken cancellationToken)
    {
        Vulgata.Core.Entities.System? value = await management.Systems.AsNoTracking()
            .SingleOrDefaultAsync(system => system.Id == id, cancellationToken);
        return (value is not null, value?.Context);
    }

    private async Task<(bool Exists, string? Value)> ReadRepositoryAsync(Guid id, CancellationToken cancellationToken)
    {
        Vulgata.Core.Entities.Repository? value = await management.Repositories.AsNoTracking()
            .SingleOrDefaultAsync(repository => repository.Id == id, cancellationToken);
        return (value is not null, value?.Context);
    }

    private static IResult BadRequest() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest);

    private static IResult Forbidden() => Results.Problem(
        title: "not_authorized", statusCode: StatusCodes.Status403Forbidden);

    private static IResult Unavailable(string code = "authority_unavailable") => Results.Problem(
        title: code,
        statusCode: StatusCodes.Status503ServiceUnavailable);
}

using Vulgata.Core.DomainServices;

namespace Vulgata.Web.Data;

public enum OctoberSelectedDataStartupStatus
{
    Bound,
    Denied,
    Unavailable,
    Cancelled
}

public sealed record OctoberSelectedDataStartupResult(
    OctoberSelectedDataStartupStatus Status,
    IOctoberBoundSetupData? BoundSetupData);

public interface IOctoberSelectedDataStartupBinder
{
    Task<OctoberSelectedDataStartupResult> BindAsync(
        OctoberWebStartupSelection selection,
        CancellationToken cancellationToken = default);
}

internal sealed class OctoberSelectedDataStartupBinder(
    IOctoberLocalAuthority authority,
    IOctoberBatchSelector selector,
    string expectedManagementAuthorityId) : IOctoberSelectedDataStartupBinder
{
    public async Task<OctoberSelectedDataStartupResult> BindAsync(
        OctoberWebStartupSelection selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (cancellationToken.IsCancellationRequested)
            return new OctoberSelectedDataStartupResult(OctoberSelectedDataStartupStatus.Cancelled, null);
        if (selection.Mode != OctoberWebMode.SelectedData ||
            string.IsNullOrWhiteSpace(selection.TargetId) ||
            selection.BatchId is not { } batchId || batchId == Guid.Empty)
            return new OctoberSelectedDataStartupResult(OctoberSelectedDataStartupStatus.Denied, null);

        try
        {
            OctoberAuthenticationResult authenticated = await authority.AuthenticateAsync(cancellationToken);
            await using IOctoberOperatorSession? session = authenticated.Session;
            cancellationToken.ThrowIfCancellationRequested();
            if (authenticated.Status != OctoberAuthenticationStatus.Authenticated || session is null)
                return new OctoberSelectedDataStartupResult(authenticated.Status switch
                {
                    OctoberAuthenticationStatus.Cancelled => OctoberSelectedDataStartupStatus.Cancelled,
                    OctoberAuthenticationStatus.Unavailable => OctoberSelectedDataStartupStatus.Unavailable,
                    _ => OctoberSelectedDataStartupStatus.Denied
                }, null);

            OctoberAuthoritySelectionResult current = await authority.AuthorizeSelectionAsync(
                session, new OctoberAuthoritySelectionRequest(selection.TargetId, batchId, null),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (current.Status != OctoberAuthorityDecisionStatus.Authorized ||
                current.Authority is not { } authorized ||
                !string.Equals(session.ManagementAuthorityId, expectedManagementAuthorityId,
                    StringComparison.Ordinal) ||
                !string.Equals(authorized.SelectedContext?.ManagementAuthorityId,
                    expectedManagementAuthorityId, StringComparison.Ordinal) ||
                !string.Equals(authorized.TargetId, selection.TargetId, StringComparison.Ordinal) ||
                authorized.BatchId != batchId ||
                !string.Equals(authorized.SubjectId, session.SubjectId, StringComparison.Ordinal))
                return new OctoberSelectedDataStartupResult(current.Status switch
                {
                    OctoberAuthorityDecisionStatus.Cancelled => OctoberSelectedDataStartupStatus.Cancelled,
                    OctoberAuthorityDecisionStatus.Unavailable => OctoberSelectedDataStartupStatus.Unavailable,
                    _ => OctoberSelectedDataStartupStatus.Denied
                }, null);

            OctoberSelectionResult result = await selector.BindAsync(
                new OctoberStartupSelection(selection.TargetId, batchId, authorized),
                cancellationToken);
            return result.Status == OctoberSelectionStatus.Selected &&
                   result.BoundSetupData is { } bound &&
                   string.Equals(bound.TargetId, selection.TargetId, StringComparison.Ordinal) &&
                   bound.BatchId == batchId
                ? new OctoberSelectedDataStartupResult(OctoberSelectedDataStartupStatus.Bound, bound)
                : new OctoberSelectedDataStartupResult(
                    result.Status == OctoberSelectionStatus.Cancelled
                        ? OctoberSelectedDataStartupStatus.Cancelled
                        : OctoberSelectedDataStartupStatus.Denied, null);
        }
        catch (OperationCanceledException)
        {
            return new OctoberSelectedDataStartupResult(OctoberSelectedDataStartupStatus.Cancelled, null);
        }
        catch (Exception)
        {
            return new OctoberSelectedDataStartupResult(OctoberSelectedDataStartupStatus.Unavailable, null);
        }
    }
}

using Vulgata.Core.DomainServices;

namespace Vulgata.OctoberGenerator;

public enum OctoberGeneratorMode
{
    Prepare,
    Select
}

public sealed record OctoberGeneratorCommand(
    OctoberGeneratorMode Mode,
    string TargetId,
    Guid BatchId,
    OctoberSetupRequest? SetupRequest);

public interface IOctoberConfiguredTarget
{
    Task<OctoberGeneratorExecutionResult> ExecuteAsync(
        OctoberGeneratorCommand command,
        OctoberAuthorizedSelection authority,
        CancellationToken cancellationToken = default);
}

public enum OctoberGeneratorExecutionStatus
{
    Prepared,
    Selected,
    Denied,
    AuthorityUnavailable,
    TargetUnavailable,
    Cancelled
}

public sealed record OctoberGeneratorExecutionResult(
    OctoberGeneratorExecutionStatus Status,
    OctoberCommittedBatch? CommittedBatch = null,
    IOctoberBoundSetupData? BoundSetupData = null);

public interface IOctoberGeneratorModule
{
    Task<OctoberGeneratorExecutionResult> ExecuteAsync(
        OctoberGeneratorCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class OctoberGeneratorModule : IOctoberGeneratorModule
{
    private readonly IOctoberLocalAuthority _authority;
    private readonly IOctoberConfiguredTarget _target;
    private readonly string _expectedManagementAuthorityId;

    public OctoberGeneratorModule(
        IOctoberLocalAuthority authority,
        IOctoberConfiguredTarget target,
        string expectedManagementAuthorityId)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(expectedManagementAuthorityId) ||
            !string.Equals(expectedManagementAuthorityId,
                expectedManagementAuthorityId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("An exact management authority is required.",
                nameof(expectedManagementAuthorityId));
        }

        _expectedManagementAuthorityId = expectedManagementAuthorityId;
    }

    public async Task<OctoberGeneratorExecutionResult> ExecuteAsync(
        OctoberGeneratorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (cancellationToken.IsCancellationRequested)
            return new OctoberGeneratorExecutionResult(OctoberGeneratorExecutionStatus.Cancelled);
        if (!Enum.IsDefined(command.Mode) || string.IsNullOrWhiteSpace(command.TargetId) ||
            command.BatchId == Guid.Empty ||
            (command.Mode == OctoberGeneratorMode.Prepare) != (command.SetupRequest is not null))
            return new OctoberGeneratorExecutionResult(OctoberGeneratorExecutionStatus.Denied);

        bool targetReached = false;
        try
        {
            OctoberAuthenticationResult authentication = await _authority.AuthenticateAsync(cancellationToken);
            await using IOctoberOperatorSession? session = authentication.Session;
            cancellationToken.ThrowIfCancellationRequested();
            if (authentication.Status != OctoberAuthenticationStatus.Authenticated || session is null)
            {
                return Failed(authentication.Status switch
                {
                    OctoberAuthenticationStatus.Cancelled => OctoberGeneratorExecutionStatus.Cancelled,
                    OctoberAuthenticationStatus.Unavailable =>
                        OctoberGeneratorExecutionStatus.AuthorityUnavailable,
                    OctoberAuthenticationStatus.Authenticated =>
                        OctoberGeneratorExecutionStatus.AuthorityUnavailable,
                    _ => OctoberGeneratorExecutionStatus.Denied
                });
            }

            OctoberAuthoritySelectionResult selection = await _authority.AuthorizeSelectionAsync(
                session,
                new OctoberAuthoritySelectionRequest(command.TargetId, command.BatchId,
                    command.SetupRequest),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (selection.Status != OctoberAuthorityDecisionStatus.Authorized)
            {
                return Failed(selection.Status switch
                {
                    OctoberAuthorityDecisionStatus.Cancelled => OctoberGeneratorExecutionStatus.Cancelled,
                    OctoberAuthorityDecisionStatus.Unavailable =>
                        OctoberGeneratorExecutionStatus.AuthorityUnavailable,
                    _ => OctoberGeneratorExecutionStatus.Denied
                });
            }

            OctoberSetupRequest? canonical = selection.Request;
            OctoberAuthorizedSelection? authority = selection.Authority;
            if (canonical is null || authority?.SelectedContext is null ||
                !string.Equals(session.ManagementAuthorityId,
                    _expectedManagementAuthorityId, StringComparison.Ordinal) ||
                !string.Equals(canonical.TargetId, command.TargetId, StringComparison.Ordinal) ||
                canonical.BatchId != command.BatchId ||
                !string.Equals(authority.TargetId, command.TargetId, StringComparison.Ordinal) ||
                authority.BatchId != command.BatchId ||
                !string.Equals(authority.SubjectId, session.SubjectId, StringComparison.Ordinal) ||
                !string.Equals(authority.SelectedContext.ManagementAuthorityId,
                    _expectedManagementAuthorityId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(authority.CanonicalDatabaseIdentity) ||
                !string.Equals(canonical.SelectionDigest,
                    authority.SelectionDigest, StringComparison.Ordinal))
            {
                return Failed(OctoberGeneratorExecutionStatus.Denied);
            }

            targetReached = true;
            OctoberGeneratorCommand trustedCommand = command with { SetupRequest =
                command.Mode == OctoberGeneratorMode.Prepare ? canonical : null };
            OctoberGeneratorExecutionResult result = await _target.ExecuteAsync(
                trustedCommand, authority, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed(OctoberGeneratorExecutionStatus.Cancelled);
        }
        catch (Exception)
        {
            return Failed(targetReached
                ? OctoberGeneratorExecutionStatus.TargetUnavailable
                : OctoberGeneratorExecutionStatus.AuthorityUnavailable);
        }
    }

    private static OctoberGeneratorExecutionResult Failed(OctoberGeneratorExecutionStatus status) =>
        new(status);
}

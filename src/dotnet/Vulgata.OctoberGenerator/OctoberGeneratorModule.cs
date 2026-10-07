using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Configuration;

namespace Vulgata.OctoberGenerator;

public enum OctoberGeneratorMode
{
    Prepare,
    Select,
    Generate
}

public sealed record OctoberGeneratorCommand(
    OctoberGeneratorMode Mode,
    string TargetId,
    Guid BatchId,
    OctoberSetupRequest? SetupRequest,
    OctoberScanPolicy? ScanPolicy = null);

public sealed record OctoberGenerateExecution(
    IOctoberOperatorSession Session,
    IOctoberHistoricalBoundSetupData Setup,
    OctoberAuthorizedSelection Authority,
    OctoberScanPolicy Policy,
    OctoberSelectedWorkerProvider Provider);

public sealed class OctoberSelectedWorkerProvider
{
    internal OctoberSelectedWorkerProvider(string sourceIdentity, string precedence,
        OctoberProviderProfile profile, string protectedCredential)
    {
        SourceIdentity = sourceIdentity;
        Precedence = precedence;
        Profile = profile;
        ProtectedCredential = protectedCredential;
    }

    public string SourceIdentity { get; }
    public string Precedence { get; }
    public OctoberProviderProfile Profile { get; }
    internal string ProtectedCredential { get; }

    public override string ToString() => nameof(OctoberSelectedWorkerProvider);
}

public interface IOctoberGenerateOperation
{
    Task<OctoberGeneratorExecutionResult> ExecuteAsync(
        OctoberGenerateExecution execution,
        CancellationToken cancellationToken = default);
}

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
    private readonly IOctoberGenerateOperation? _generate;
    private readonly OctoberSelectedWorkerProvider? _provider;

    public OctoberGeneratorModule(
        IOctoberLocalAuthority authority,
        IOctoberConfiguredTarget target,
        string expectedManagementAuthorityId,
        IOctoberGenerateOperation? generate = null,
        OctoberSelectedWorkerProvider? provider = null)
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
        _generate = generate;
        _provider = provider;
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
        if (command.Mode == OctoberGeneratorMode.Generate &&
            (command.ScanPolicy is null || _generate is null || _provider is null ||
             !new OctoberScanPolicyValidator().Validate(command.ScanPolicy).IsValid))
            return Failed(OctoberGeneratorExecutionStatus.Denied);
        if (command.Mode != OctoberGeneratorMode.Generate && command.ScanPolicy is not null)
            return Failed(OctoberGeneratorExecutionStatus.Denied);

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
            if (command.Mode == OctoberGeneratorMode.Generate)
                trustedCommand = trustedCommand with
                {
                    Mode = OctoberGeneratorMode.Select,
                    ScanPolicy = null
                };
            OctoberGeneratorExecutionResult result = await _target.ExecuteAsync(
                trustedCommand, authority, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (command.Mode == OctoberGeneratorMode.Generate)
            {
                if (result.Status != OctoberGeneratorExecutionStatus.Selected)
                    return result;
                if (result.BoundSetupData is not IOctoberHistoricalBoundSetupData setup ||
                    !MatchesSelection(setup, canonical, authority) ||
                    command.ScanPolicy!.SourceRules.Any(rule =>
                        !authority.RepositoryIds.Contains(rule.RepositoryId)))
                    return Failed(OctoberGeneratorExecutionStatus.Denied);

                // Selection is rebound inside this session. The operation must settle
                // its tools and writes before this owning scope disposes the session.
                result = await _generate!.ExecuteAsync(new OctoberGenerateExecution(
                    session, setup, authority, command.ScanPolicy, _provider!), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
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

    private static bool MatchesSelection(IOctoberHistoricalBoundSetupData setup,
        OctoberSetupRequest canonical, OctoberAuthorizedSelection authority)
    {
        try
        {
            OctoberBoundSetupSnapshot snapshot = setup.Snapshot;
            if (snapshot is null || snapshot.Request is null || snapshot.Authority is null)
                return false;
            OctoberSetupRequest request = snapshot.Request;
            OctoberAuthorizedSelection selected = snapshot.Authority;
            if (setup.TargetId != canonical.TargetId || setup.BatchId != canonical.BatchId ||
                request.TargetId != canonical.TargetId || request.BatchId != canonical.BatchId ||
                selected.TargetId != authority.TargetId || selected.BatchId != authority.BatchId ||
                selected.CanonicalDatabaseIdentity != authority.CanonicalDatabaseIdentity ||
                selected.SubjectId != authority.SubjectId ||
                selected.SelectionDigest != authority.SelectionDigest ||
                request.SelectionDigest != canonical.SelectionDigest ||
                snapshot.SelectedAtUtc.Offset != TimeSpan.Zero ||
                selected.SelectedContext is not { } context ||
                authority.SelectedContext is not { } current ||
                !selected.RepositoryIds.Order().SequenceEqual(authority.RepositoryIds.Order()) ||
                !authority.RepositoryIds.Order().SequenceEqual(
                    canonical.Repositories.Select(repository => repository.RepositoryId).Order()) ||
                !request.Repositories.OrderBy(repository => repository.RepositoryId)
                    .SequenceEqual(canonical.Repositories.OrderBy(repository => repository.RepositoryId)) ||
                !context.Repositories.OrderBy(repository => repository.RepositoryId)
                    .SequenceEqual(current.Repositories.OrderBy(repository => repository.RepositoryId)) ||
                !context.Repositories.OrderBy(repository => repository.RepositoryId)
                    .SequenceEqual(request.Repositories.OrderBy(repository => repository.RepositoryId)))
                return false;

            return OctoberSelectionDigest.Compute(request, context) == authority.SelectionDigest &&
                OctoberSelectionDigest.Compute(canonical, current) == authority.SelectionDigest;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

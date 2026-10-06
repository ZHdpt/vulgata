using Vulgata.Core.DomainServices;

namespace Vulgata.OctoberGenerator;

public enum OctoberConnectedTargetStatus
{
    Matched,
    Mismatched,
    Unavailable,
    Cancelled
}

public interface IOctoberConnectedTargetIdentityVerifier
{
    Task<OctoberConnectedTargetStatus> VerifyAsync(
        string targetId,
        string canonicalDatabaseIdentity,
        CancellationToken cancellationToken = default);
}

public interface IOctoberTargetMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Owns the exact configured target's prepare/select dependencies without exposing storage handles.
/// </summary>
public sealed class OctoberConfiguredTarget : IOctoberConfiguredTarget
{
    private readonly string _targetId;
    private readonly string _canonicalDatabaseIdentity;
    private readonly IOctoberConnectedTargetIdentityVerifier _identityVerifier;
    private readonly IOctoberTargetMigrator _migrator;
    private readonly OctoberBatchSetupPolicy _policy;
    private readonly IOctoberBatchSelector _selector;

    public OctoberConfiguredTarget(
        string targetId,
        string canonicalDatabaseIdentity,
        IOctoberConnectedTargetIdentityVerifier identityVerifier,
        IOctoberTargetMigrator migrator,
        IOctoberSetupWriter writer,
        IOctoberBatchSelector selector)
    {
        if (string.IsNullOrWhiteSpace(targetId) ||
            !string.Equals(targetId, targetId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("An exact target is required.", nameof(targetId));
        }

        if (string.IsNullOrWhiteSpace(canonicalDatabaseIdentity) ||
            !string.Equals(canonicalDatabaseIdentity,
                canonicalDatabaseIdentity.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("An exact database identity is required.",
                nameof(canonicalDatabaseIdentity));
        }

        _targetId = targetId;
        _canonicalDatabaseIdentity = canonicalDatabaseIdentity;
        _identityVerifier = identityVerifier ?? throw new ArgumentNullException(nameof(identityVerifier));
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        _policy = new OctoberBatchSetupPolicy(writer ?? throw new ArgumentNullException(nameof(writer)));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    public async Task<OctoberGeneratorExecutionResult> ExecuteAsync(
        OctoberGeneratorCommand command,
        OctoberAuthorizedSelection authority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(authority);
        if (cancellationToken.IsCancellationRequested)
            return Failed(OctoberGeneratorExecutionStatus.Cancelled);
        if (!string.Equals(command.TargetId, _targetId, StringComparison.Ordinal) ||
            !string.Equals(authority.TargetId, _targetId, StringComparison.Ordinal) ||
            !string.Equals(authority.CanonicalDatabaseIdentity,
                _canonicalDatabaseIdentity, StringComparison.Ordinal) ||
            authority.BatchId != command.BatchId)
            return Failed(OctoberGeneratorExecutionStatus.Denied);

        try
        {
            OctoberConnectedTargetStatus connected = await _identityVerifier.VerifyAsync(
                _targetId, _canonicalDatabaseIdentity, cancellationToken);
            if (connected != OctoberConnectedTargetStatus.Matched)
                return Failed(connected == OctoberConnectedTargetStatus.Cancelled
                    ? OctoberGeneratorExecutionStatus.Cancelled
                    : OctoberGeneratorExecutionStatus.TargetUnavailable);

            if (command.Mode == OctoberGeneratorMode.Select)
            {
                OctoberSelectionResult selected = await _selector.BindAsync(
                    new OctoberStartupSelection(_targetId, command.BatchId, authority),
                    cancellationToken);
                return selected.Status == OctoberSelectionStatus.Selected &&
                       selected.BoundSetupData is { } bound &&
                       string.Equals(bound.TargetId, _targetId, StringComparison.Ordinal) &&
                       bound.BatchId == command.BatchId
                    ? new OctoberGeneratorExecutionResult(
                        OctoberGeneratorExecutionStatus.Selected, BoundSetupData: bound)
                    : Failed(selected.Status == OctoberSelectionStatus.Cancelled
                        ? OctoberGeneratorExecutionStatus.Cancelled
                        : OctoberGeneratorExecutionStatus.Denied);
            }

            if (command.Mode != OctoberGeneratorMode.Prepare || command.SetupRequest is not { } request ||
                !string.Equals(request.TargetId, _targetId, StringComparison.Ordinal) ||
                request.BatchId != command.BatchId ||
                !string.Equals(request.SelectionDigest,
                    authority.SelectionDigest, StringComparison.Ordinal))
                return Failed(OctoberGeneratorExecutionStatus.Denied);

            cancellationToken.ThrowIfCancellationRequested();
            await _migrator.MigrateAsync(cancellationToken);
            OctoberPrepareResult prepared = await _policy.PrepareAsync(
                request, authority, cancellationToken);
            OctoberCommittedBatch? committed = prepared.CommittedBatch;
            return (prepared.Status is OctoberPrepareStatus.Created or OctoberPrepareStatus.AlreadyIdentical) &&
                   committed is not null &&
                   string.Equals(committed.TargetId, _targetId, StringComparison.Ordinal) &&
                   committed.BatchId == command.BatchId &&
                   string.Equals(committed.CanonicalDatabaseIdentity,
                       _canonicalDatabaseIdentity, StringComparison.Ordinal) &&
                   string.Equals(committed.SelectionDigest,
                       authority.SelectionDigest, StringComparison.Ordinal) &&
                   committed.SelectedAtUtc is { } selectedAtUtc &&
                   selectedAtUtc.Offset == TimeSpan.Zero
                ? new OctoberGeneratorExecutionResult(
                    OctoberGeneratorExecutionStatus.Prepared, CommittedBatch: committed)
                : Failed(prepared.Status == OctoberPrepareStatus.Cancelled
                    ? OctoberGeneratorExecutionStatus.Cancelled
                    : prepared.Status == OctoberPrepareStatus.StorageFailure
                        ? OctoberGeneratorExecutionStatus.TargetUnavailable
                    : OctoberGeneratorExecutionStatus.Denied);
        }
        catch (OperationCanceledException)
        {
            return Failed(OctoberGeneratorExecutionStatus.Cancelled);
        }
        catch (Exception)
        {
            return Failed(OctoberGeneratorExecutionStatus.TargetUnavailable);
        }
    }

    private static OctoberGeneratorExecutionResult Failed(OctoberGeneratorExecutionStatus status) =>
        new(status);
}

internal sealed class UnconfiguredOctoberTargetDependencies :
    IOctoberConnectedTargetIdentityVerifier,
    IOctoberTargetMigrator,
    IOctoberSetupWriter,
    IOctoberBatchSelector
{
    public Task<OctoberConnectedTargetStatus> VerifyAsync(
        string targetId,
        string canonicalDatabaseIdentity,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OctoberConnectedTargetStatus.Unavailable);

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("October target is not configured."));

    public Task<OctoberPrepareResult> CommitAsync(
        OctoberSetupRequest request,
        OctoberAuthorizedSelection authority,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new OctoberPrepareResult(OctoberPrepareStatus.AuthorityUnavailable, null));

    public Task<OctoberSelectionResult> BindAsync(
        OctoberStartupSelection selection,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new OctoberSelectionResult(OctoberSelectionStatus.AuthorityUnavailable, null));
}

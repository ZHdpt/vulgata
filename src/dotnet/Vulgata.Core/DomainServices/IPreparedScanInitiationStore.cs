using Vulgata.Core.Entities;

namespace Vulgata.Core.DomainServices;

public enum PreparedScanInitiationOutcome
{
    Created,
    Replay,
    NotFound,
    Conflict,
    Rejected,
    Cancelled,
    Unavailable,
}

public enum PreparedScanArtifactPresence
{
    None,
    Present,
}

public sealed record PreparedScanInitiationRepositoryCommand(
    Guid RepositoryId,
    string RequestedRef,
    string ProfiledRevision,
    string RuntimeIdentityJson,
    string RuntimeIdentityHash);

public sealed record PreparedScanInitiationCommand(
    Guid AuthorizationUserId,
    string EffectiveAuthorizationMode,
    Guid SystemId,
    IReadOnlyList<PreparedScanInitiationRepositoryCommand> Repositories,
    string PromptIdentity,
    string DocumentSchemaIdentity,
    int ProviderProcessTimeoutSeconds,
    DateTimeOffset InitiatedAt);

public sealed record PreparedScanInitiationRepositoryTuple(
    Guid RepositoryId,
    string RequestedRef,
    string ProfiledRevision,
    Guid ScanRunId,
    Guid ScanExecutionId,
    ScanExecutionPhase ScanExecutionPhase,
    string PromptIdentity,
    string DocumentSchemaIdentity,
    string RuntimeIdentityJson,
    string RuntimeIdentityHash,
    Guid PreScanResultId);

public sealed record PreparedScanInitiationTuple(
    Guid SystemId,
    string EffectiveAuthorizationScopeFingerprint,
    DateTimeOffset InitiatedAt,
    Guid CoordinationId,
    long CoordinationStateVersion,
    TwoRepositoryScanCoordinationPhase CoordinationPhase,
    PreparedScanArtifactPresence PreparedArtifactPresence,
    IReadOnlyList<PreparedScanInitiationRepositoryTuple> Repositories);

public sealed record PreparedScanInitiationResult(
    PreparedScanInitiationOutcome Outcome,
    PreparedScanInitiationTuple? Tuple = null);

public interface IPreparedScanInitiationStore
{
    Task<PreparedScanInitiationResult> CreateOrReplayAsync(
        PreparedScanInitiationCommand command,
        CancellationToken cancellationToken);
}

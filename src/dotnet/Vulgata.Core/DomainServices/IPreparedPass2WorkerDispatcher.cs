namespace Vulgata.Core.DomainServices;

public interface IPreparedPass2WorkerDispatcher
{
    Task<PreparedPass2DispatchResult> DispatchAsync(
        PreparedPass2DispatchRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Rehydrates a claimed job from durable server authority. Implementations
/// must not accept Repository, revision, provider, membership, or evidence
/// authority from an HTTP or Worker caller.
/// </summary>
public interface IPreparedPass2DispatchRequestSource
{
    Task<PreparedPass2DispatchHydrationResult> HydrateAsync(
        Guid pass2ExecutionId,
        Guid pass2JobExecutionId,
        long jobStateVersion,
        Guid leaseToken,
        CancellationToken cancellationToken = default);
}

public enum PreparedPass2DispatchHydrationOutcome
{
    Hydrated,
    IntegrityRefused,
    SecurityRefused,
}

public sealed record PreparedPass2DispatchHydrationResult(
    PreparedPass2DispatchHydrationOutcome Outcome,
    PreparedPass2DispatchRequest? Request);

public sealed record PreparedPass2DispatchRequest(
    Guid Pass2JobExecutionId,
    Guid LeaseToken,
    DateTimeOffset LeaseExpiresAt,
    int AttemptOrdinal,
    Guid Pass2ExecutionId,
    Guid FlowGroupId,
    Guid ExactResolutionDecisionId,
    Guid ExactResolutionEdgeId,
    string ExactResolutionDecisionFingerprint,
    string ProviderEvidenceFingerprint,
    string Protocol,
    string NormalizedEndpointIdentifierFingerprint,
    string ConsumerEntryStableKey,
    string LogicalDocumentStableKey,
    string PlanFingerprint,
    string MembershipFingerprint,
    string PromptIdentity,
    string SubmissionSchemaIdentity,
    string ToolDeclarationIdentity,
    IReadOnlyList<Pass2BusinessLogicEvidenceBinding> InitialEvidence,
    PreparedPass2Flow Flow,
    PreparedPass2RuntimeBinding RuntimeBinding)
{
    public string PromptFingerprint { get; init; } = string.Empty;
    public string SubmissionSchemaFingerprint { get; init; } = string.Empty;
    public string ToolDeclarationFingerprint { get; init; } = string.Empty;
}

public sealed record PreparedPass2DispatchResult(
    bool Accepted,
    string? ReasonCode);

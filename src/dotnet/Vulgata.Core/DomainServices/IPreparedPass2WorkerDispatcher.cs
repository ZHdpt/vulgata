namespace Vulgata.Core.DomainServices;

public interface IPreparedPass2WorkerDispatcher
{
    Task<PreparedPass2DispatchResult> DispatchAsync(
        PreparedPass2DispatchRequest request,
        CancellationToken cancellationToken = default);
}

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
    PreparedPass2RuntimeBinding RuntimeBinding);

public sealed record PreparedPass2DispatchResult(
    bool Accepted,
    string? ReasonCode);

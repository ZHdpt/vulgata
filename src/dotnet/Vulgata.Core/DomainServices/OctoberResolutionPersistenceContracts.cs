namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>
/// Producer-only proposal. The store reloads the exact accepted corpus and recomputes the inquiry
/// and outcome. A witness cannot supply an authoritative relationship. A witness-free disposition
/// is restricted to truthful Failed/NotEvaluated host execution facts validated by the shared guard.
/// </summary>
public sealed record ResolutionAcceptanceRequest(OperationKey Operation, CorpusHandle Corpus, Guid AttemptId,
    string CanonicalPayloadDigest, string InquiryFingerprint, ResolutionWitness? Witness,
    ResolutionOutcomeKind? ExecutionDisposition = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolutionPolicyVersion { get; init; }
}

public sealed record ResolutionCorpusResult(KnowledgeReadStatus Status, ResolutionCorpus? Corpus);
public sealed record ResolutionAcceptanceResult(KnowledgeWriteStatus Status, OperationKey Operation, ResolutionOutcome? Outcome);

/// <summary>
/// Private accepted-corpus resolution persistence; ordinary visibility still requires an explicit
/// immutable publication. Every replay authenticates stored complete contributors before disclosure.
/// </summary>
public interface IOctoberResolutionStore
{
    Task<ResolutionCorpusResult> LoadCorpusAsync(CorpusHandle corpus, CancellationToken cancellationToken = default);
    Task<ResolutionAcceptanceResult> AcceptOutcomeAsync(ResolutionAcceptanceRequest request, CancellationToken cancellationToken = default);
    Task<ResolutionAcceptanceResult> LookupOutcomeAsync(CorpusHandle corpus, OperationKey operation, string payloadDigest,
        CancellationToken cancellationToken = default);
}

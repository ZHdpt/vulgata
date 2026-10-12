using System.Collections.Immutable;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>
/// Host-selected logical finalization inputs. Default roots are invalid; an explicit empty selection
/// permits a durable NoContent result without provider calls. A fingerprint is not a credential grant.
/// </summary>
public sealed record BusinessFinalizationSelection(ImmutableArray<FlowRootSelection> Roots,
    FlowPlanningLimits Limits, Guid RunId, string ProviderProfileId,
    string ProviderConfigurationFingerprint, string PromptDigest, long MaximumTokens)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public BusinessAdvisoryContextOverride? ContextOverride { get; init; }
}

/// <summary>
/// The single nonsecret provider configuration projection used by Business and its Scan caller.
/// Endpoint spelling is exact. CredentialReferenceId identifies a host-owned reference, never
/// plaintext, ciphertext or a credential hash. The host must bind the actual client and credential
/// lifetime and recheck eligibility before dispatch; this projection cannot authorize rotation.
/// </summary>
public sealed record BusinessProviderConfigurationProjection(string SourceIdentity, int Precedence,
    string ProfileName, string ApiType, string Endpoint, string Model, string CredentialReferenceId);

/// <summary>Original model proposal only; the persistence owner derives accepted content and provenance.</summary>
public sealed record FlowSynthesisSubmission(FlowRootSelection Root, Guid GroupVersionId,
    BusinessSynthesisProposal Proposal);

public enum FlowFinalizationDispositionKind { Failed, NotEvaluated }

/// <summary>Terminal host facts checked through F1, never model-supplied execution authority.</summary>
public sealed record FlowFinalizationDisposition(FlowRootSelection Root, Guid? GroupVersionId,
    FlowFinalizationDispositionKind Kind, string Reason);

/// <summary>
/// CanonicalPayloadDigest binds the original finalization request. CanonicalOutcomeDigest additionally
/// binds original proposals and terminal dispositions, so changed retries cannot replace an outcome.
/// The owner reloads immutable inputs and validates synthesis; transport dependencies are not authority.
/// </summary>
public sealed record FlowFinalizationPublicationRequest(OperationKey Operation, CorpusHandle Corpus,
    string CanonicalPayloadDigest, BusinessFinalizationSelection Selection,
    ImmutableArray<FlowSynthesisSubmission> Completed, ImmutableArray<FlowFinalizationDisposition> Dispositions,
    string CanonicalOutcomeDigest);

/// <summary>
/// Producer-only atomic flow publication and durable finalization-result reconciliation. Lookup never
/// regenerates content. An absent row is indeterminate and grants no provider dispatch authority;
/// Scan retains single-flight ownership, F1 eligibility and the shared execution budgets.
/// </summary>
public interface IOctoberFlowPublicationStore
{
    Task<BusinessFinalizationResult> PublishAsync(FlowFinalizationPublicationRequest request,
        CancellationToken cancellationToken = default);

    Task<BusinessFinalizationResult> LookupAsync(CorpusHandle corpus, OperationKey operation,
        string canonicalPayloadDigest, CancellationToken cancellationToken = default);
}

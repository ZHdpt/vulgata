using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vulgata.Core.DomainServices.OctoberKnowledge;

/// <summary>Host-only disclosure protocol bounds, independent of model-output or public page limits.</summary>
public static class OctoberKnowledgeDisclosureLimits
{
    public const int Version = 1;
    public const int MaximumContributors = 4096;
    public const int MaximumOriginGroups = 128;
    public const int MaximumCanonicalPayloadUtf8Bytes = 512 * 1024;
    public const int MaximumProtectedPayloadUtf8Bytes = 1024 * 1024;
}

/// <summary>
/// Self-contained owner-protected dependency capsule. No per-read persistence lookup is required.
/// All fields must match the authenticated protected payload. This is neither permission nor model/UI data.
/// </summary>
public sealed record KnowledgeDependencyHandle(Guid SetId, string BindingDigest, string ProtectedPayload);

/// <summary>Owner-protected exact consumed-content-chain proof; never trust consumer chunk counts or completeness.</summary>
public sealed record KnowledgeConsumedContentHandle(Guid ReceiptId, string BindingDigest, string ProtectedPayload);

public enum KnowledgeDependencyStatus
{
    Succeeded, Denied, AuthorityUnavailable, InvalidProof, UnknownLineage, BudgetExceeded, Cancelled,
}

public sealed record KnowledgeDependencyResult(KnowledgeDependencyStatus Status, KnowledgeDependencyHandle? Dependencies);

/// <summary>
/// Owner-resolved immutable stream facts. Coverage describes source analysis, independently of whether
/// every chunk of this exact whole-document or section stream was admitted as consumer input.
/// </summary>
public sealed record KnowledgeConsumedContentReceipt(KnowledgeConsumedContentHandle Proof, DocumentHandle Document,
    bool IsPartial, int SuccessfulChunks, long ContentBytes, CoverageProjection? OwnerCoverage,
    KnowledgeDependencyHandle Dependencies);
public sealed record KnowledgeConsumedContentResult(KnowledgeDependencyStatus Status, KnowledgeConsumedContentReceipt? Receipt);

/// <summary>
/// Mechanical trusted-host encoding of the complete consumer wrapper using its actual serializer.
/// Methods are deterministic, synchronous, bounded and side-effect-free; they grant no authority.
/// Return the exact JSON object bytes consumed by the model/consumer, never a JSON-quoted JSON string.
/// ProfileId identifies a fixed, conformance-tested wrapper/serializer version.
/// </summary>
public interface IOctoberKnowledgeOutputEncoder
{
    string ProfileId { get; }
    ImmutableArray<byte> EncodeSearch(SearchPage page);
    ImmutableArray<byte> EncodeContent(ContentPage page);
    ImmutableArray<byte> EncodeRelated(RelatedPage page);
}

/// <summary>
/// Host-supplied remaining limits only narrow canonical limits. They never reset a turn/graph budget.
/// Complete encoded bytes, not content bytes or an arbitrary envelope reserve, decide acceptance.
/// </summary>
public sealed record KnowledgeReadOutputBudget(int MaximumResultUtf8Bytes, long RemainingAggregateUtf8Bytes,
    IOctoberKnowledgeOutputEncoder Encoder);

/// <summary>
/// Host-only atomic page/lineage result. Send only OutputUtf8 to its intended consumer, never serialize
/// this envelope to model, API or UI. Page and both digests must describe those same encoded bytes.
/// Search/content shapes remain compatible; related edges add the separately declared nullable accepted-edge ID.
/// </summary>
public sealed record KnowledgeDisclosedPage<T>(
    [property: JsonIgnore] T Page,
    [property: JsonIgnore] KnowledgeDependencyHandle Dependencies,
    [property: JsonIgnore] string PageDigest,
    [property: JsonIgnore] string OutputProfileId,
    [property: JsonIgnore] string OutputDigest,
    [property: JsonIgnore] ImmutableArray<byte> OutputUtf8) where T : class;
public sealed record KnowledgeDisclosureReadResult<T>(KnowledgeReadStatus Status,
    [property: JsonIgnore] KnowledgeDisclosedPage<T>? Disclosure)
    where T : class;

/// <summary>
/// Optional owner read surface for consumers retaining aggregate disclosure dependencies. A successful
/// page and its complete dependency capsule are issued together after final live checks and logging.
/// Failure has no page, capsule, encoded bytes or read credit. No post-read dependency-fetch race.
/// The existing reader port remains compatible; it does not promise this stronger aggregate capability.
/// </summary>
public interface IOctoberKnowledgeDisclosedReader
{
    Task<KnowledgeDisclosureReadResult<SearchPage>> SearchAsync(KnowledgeSearchRequest request,
        KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default);
    Task<KnowledgeDisclosureReadResult<ContentPage>> ReadExactAsync(ExactReadRequest request,
        KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default);
    Task<KnowledgeDisclosureReadResult<RelatedPage>> GetRelatedAsync(RelatedReadRequest request,
        KnowledgeReadOutputBudget budget, CancellationToken cancellationToken = default);
}

/// <summary>
/// Knowledge-owned capsule issuance, bounded union and live authorization. Independent of dialogue
/// persistence; graph consumers can reauthorize all acquired pages in one final call. No new grants,
/// source reads, consumer-supplied contributor unions, implicit empty proofs or expiry-as-permission.
/// Every call honors the caller's original bounded lifetime and fresh host-derived subject.
/// </summary>
public interface IOctoberKnowledgeDependencyAuthority
{
    /// <summary>
    /// Issue an explicit initial question/static-instructions-only attestation after current selection/owner
    /// checks. The trusted coordinator must have no history, advisory/context facts or previous generated
    /// content in that projection. InitialInputDigest binds the exact initial projection. Missing lineage
    /// cannot use this operation. An empty initial graph also qualifies; acquired Knowledge invalidates
    /// the aggregate no-Knowledge classification without removing its initial origin.
    /// </summary>
    Task<KnowledgeDependencyResult> CreateNoKnowledgeRootAsync(ReadSelectionRequest selection,
        string initialInputDigest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticate, flatten and union all nonempty supplied capsules without losing any previous origin.
    /// Same subject/publication/mode/purpose/scope is required. Bounds fail before new consumer input;
    /// never evict an old dependency or nest ciphertext. Coordinator retains the whole accumulated union.
    /// </summary>
    Task<KnowledgeDependencyResult> MergeAsync(ReadSelectionRequest selection,
        ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default);

    /// <summary>
    /// One bounded live aggregate check over every supplied capsule/origin/contributor, including metadata
    /// and uncited/transitive inputs. Empty/default/missing lineage is not an authorized empty union.
    /// Denial/outage suppresses the entire dependent result; rereading a guessed cited subset is forbidden.
    /// </summary>
    Task<KnowledgeDependencyStatus> ReauthorizeAsync(ReadSelectionRequest selection,
        ImmutableArray<KnowledgeDependencyHandle> dependencies, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prove coverage of owner-issued successful content pages admitted by the trusted consumer coordinator.
    /// Validate exact target/section, stable projection and raw-chunk digests/positions; encrypted continuation
    /// string equality is not stream identity. Identical semantic replay adds no credit; the same position
    /// with changed payload/coverage is InvalidProof. Detect skipped first/middle/end
    /// chunks. Search/related, refused or oversized results grant no content credit. A partial proof remains
    /// partial; null source coverage never means complete analysis.
    /// </summary>
    Task<KnowledgeConsumedContentResult> BuildConsumedContentAsync(ReadSelectionRequest selection,
        DocumentHandle document, ImmutableArray<KnowledgeDependencyHandle> admittedContentPages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticate and resolve the complete proof, compare its binding and live authority, and verify all
    /// consumed-page origins are included in inferenceDependencies. Dialogue compares its reported reference
    /// to these owner-resolved facts; caller-provided flags/counts/coverage are never authority.
    /// </summary>
    Task<KnowledgeConsumedContentResult> VerifyConsumedContentAsync(ReadSelectionRequest selection,
        KnowledgeConsumedContentHandle proof, KnowledgeDependencyHandle inferenceDependencies,
        CancellationToken cancellationToken = default);
}

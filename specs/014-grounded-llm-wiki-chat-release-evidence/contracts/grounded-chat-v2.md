# Contract: Grounded Chat v2

## Boundary and sequencing

`PreparedChatCoordinator` remains the sole answer orchestrator and executes:

1. validate the caller's normalized question, Business/IT mode, authorized System, and exactly two selected Repositories;
2. load its server-owned bounded conversation state and create one monotonic absolute answer deadline capped at 29 seconds;
3. authorize the scope and resolve current Repository membership;
4. retrieve one closed packet through `IChatEvidenceRetriever`;
5. extract/rank provider-visible grounded candidates from that packet;
6. stream one primary synthesis request through the dedicated server-only transport and, only for syntax rejection with at least three seconds remaining, stream one repair;
7. strictly assemble one complete UTF-8 response with valid byte bounds and exactly one terminal, then validate shape, packet fingerprint, every selected handle, every material claim, and every citation;
8. re-fetch/revalidate selected Documents, sections, Source Anchors, decisions, Edges, Flow Groups, and final access;
9. atomically return one complete `GroundedAnswer` and append one validated conversation turn.

No provider fragment, claim, citation, handle, relationship, or Source Anchor detail crosses the client boundary before step 8. Any failure returns a bounded Chinese outcome with zero partial answer.

## Retrieval interface

The provider-independent interface accepts only server-owned values:

```csharp
public interface IChatEvidenceRetriever
{
    Task<ChatEvidenceResult> RetrieveAsync(
        ChatRetrievalRequest request,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default);
}
```

`ChatRetrievalRequest` includes normalized question/scope/mode, bounded transient conversation, server-created `AuthorizedChatScope`, server-loaded `ChatAdvisoryContextSnapshot`, and deadline. It does not contain caller-selected advisory text or Context/Document/Edge/decision/Flow Group IDs.

## Single typed retrieval pipeline

```text
AuthorizedChatSnapshot
  -> ChatLexicalPrimaryProjection
  -> ChatRelationshipExpansionProjection
  -> ChatEvidenceAssembly
  -> EvidencePacket v2
```

The result union at every stage is `Succeeded`, `InsufficientEvidence`, `NotFoundOrUnauthorized`, or `InconsistentCurrentEvidence`. The lexical projection is the sole primary Document/section authority. Relationship expansion receives immutable primary identities and can only append complete optional units. `ChatEvidenceAssembly` is the sole packet builder. There is no question-triggered branch, projection-only final return, exact-row packet rebuild, legacy-authority query, alternate builder, or feature-flagged coexistence path.

The implementation cutover deletes `RequiresCrossRepositoryEvidence`, `ExactRelationshipRow`, `ProvenanceRow`, the retriever-owned v2 rebuild, duplicate builder calls, and every Story 12.5 use of `ExactCrossRepositoryEdgeEvidence`/prepared exact-profile truth. Legacy rows remain persistence history only.

## Deterministic primary selection

Eligible rows are current authorized `IndexEntry` and `DocumentSection` projections inside the selected System/two Repositories. The ranker extracts:

- exact symbols and endpoint identifiers;
- Repository names and Repository-relative paths;
- normalized ASCII identifiers/terms;
- CJK bigrams after the closed ignored-term filter;
- Business or IT mode terms; and
- bounded terms from prior validated conversation context.

The candidate window is the highest-ranked 256 distinct stable section IDs or exhaustion. Current eligible rows stream in stable-ID order and are deduplicated/scored before the top-K cap; 257 or more eligible rows are not refused merely for size. Duplicate stable IDs must be byte-equivalent. After NFC normalization, parse structured symbol/business/technical term arrays and use normalized token equality; malformed arrays are inconsistent. These exact integer components apply at most once when any applicable term matches: Repository name 50, key symbol/endpoint 40, path 30, active-mode term 25 (`BusinessTerms` only in Business Mode, `TechnicalTerms` only in IT Mode), search text 20, heading 5, title 3, prose 1, plus exactly one point for a prior current eligible Logical Document. Other fields use normalized ordinal-ignore-case containment. Every positive-score candidate competes in the same rank; metadata presence never pre-filters prose candidates.

Advisory context comes only from applied `GlobalContext`, selected `System.Context`, and both selected `Repository.Context` values loaded through `IChatAdvisoryContextReader` after authorization; pending changes and caller text are excluded. Normalize NFC, line endings and outer whitespace; compose Global -> System -> Repositories ordered by RFC 4122 ID; tokenize with the same ASCII/CJK rules. Empty applied values produce canonical empty context. Exactly 8,192 composed UTF-8 bytes succeeds; 8,193 refuses without truncation or provider dispatch. Advisory terms do not affect a positive direct pass. Only when direct question/conversation scoring yields zero candidates does fallback rank current Business Logic summary sections with question/conversation/advisory terms using search/summary 20, heading 5, title 3 and prose 1 at most once. No positive fallback returns insufficient evidence.

Canonical order is descending total score, then mode preference, Repository ID, Logical Document ID, section order, stable section ID. Business mode prefers Business Logic then Code Logic. IT mode prefers Code Logic TechnicalDetail/Evidence, other Code Logic, then Business Logic.

Selection rules:

- at most five Documents;
- exactly twelve primary sections or exhaustion, and never more than twenty total after expansion;
- at most four sections per Document;
- take the strongest section from each selected Document before any extra section;
- no useful result after fallback returns insufficient evidence without provider dispatch.

## Relationship projector

Only current persisted facts may add related evidence:

- current `REFERENCES` Edges;
- current `GENERATED_FROM` Edges;
- accepted exact-match `CrossRepositoryResolutionDecision` plus its exact current directed REFERENCES `DocumentEdge`; and
- complete `Published` Flow Groups with current Business Logic Document, exact decision/Edge binding, Flow Trace, and ordered steps.

Legacy `ExactCrossRepositoryEdgeEvidence` never authorizes v2 expansion or currentness.

Expansion seeds are exactly the first `min(3, primaryDocumentCount)` primary Documents. Traverse only stored direction: REFERENCES source -> target and GENERATED_FROM Business Logic source -> Code Logic target. Cross-Repository REFERENCES requires at least one independently valid accepted v2 decision bound to that Edge; multiple distinct current decision slots are valid Story 12.4 authority. A slot is `(Pass1PairExecutionId, ConsumerCandidateId, ConsumerEntryStableKey, Protocol, NormalizedEndpointIdentifier)`. Order valid decisions by pair ID, protocol, consumer stable entry key, normalized endpoint, consumer candidate ID and decision ID under Story 12.4 byte ordering. The relationship binds the first as `selectedExactDecisionId` plus the complete valid `exactDecisionCount` and `exactDecisionSetFingerprint`; the set fingerprint uses the length-prefixed canonical decision authority and recomputed contribution fingerprints defined by AD-3. Same-Repository REFERENCES and GENERATED_FROM carry null/zero decision authority. A Flow Group is eligible only when its current Published Business Logic Document is included, its exact decision belongs to that Edge's valid decision set, and its Edge/trace/steps/fingerprints/current revisions are internally consistent.

Candidate order is seed rank; mode relationship preference; same-Repository before Cross-Repository within that preference; target Repository ID; target Logical Document ID; target Document Version ID; Edge ID. Flow Groups then order by owning Edge rank, decision rank inside the Edge set, GroupOrder, Flow Group ID, trace order, step order, step ID. Deduplicate exact identities and conflicting duplicate decision slots before bounds; distinct decisions/Flow Groups sharing one Edge remain independent. Add at most two new related Documents per seed, follow exactly one hop, and never exceed five Documents, thirty-two relationships, ten Flow Group authorities, or twelve steps.

Every applicable relationship and Flow Group has one closed state: `absent`, `present-valid`, `present-inconsistent`, or `selected-required`. Optional absent/inconsistent facts support no claim but do not poison unrelated lexical primaries. Ranking leaves a valid complete unit optional; retention after all complete-unit packing drops promotes it to selected-required. Any subsequent missing/stale/inconsistent required Document, section, Source Anchor, decision, Edge, Flow Group, trace, step, or fingerprint rejects the whole assembly with no lower-ranked substitution.

## Packet contract

The 2.2 JSON shape is defined by [chat-evidence-packet-v2.schema.json](chat-evidence-packet-v2.schema.json). It is the single accepted cutover shape; 2.1 cannot represent complete many-decision and advisory-source authority and is not accepted in parallel. Every handle is packet-local and opaque. The canonical fingerprint includes schema and selection-policy versions, request/scope/mode, ordered primary Documents/sections, expansion seeds, each Cross-Repository Edge's selected decision/count/full-set fingerprint, decision-owned Flow Group bindings, exact advisory text/authority fingerprint, and conversation state. Semantic validation requires Cross-Repository REFERENCES to carry non-null selected/set authority with count >=1 and requires same-Repository REFERENCES/GENERATED_FROM to carry null/zero authority.

Bounds:

- 1-5 Documents;
- 1-20 sections, <=4 per Document;
- 1-32 Source Anchors;
- 0-32 relationships;
- 0-12 flow steps;
- no relationship depth >1;
- evidence <=65,536 UTF-8 bytes;
- conversation <=4,096 UTF-8 bytes;
- advisory context <=8,192 UTF-8 bytes.

Each Cross-Repository relationship is emitted once even when multiple decisions share its Edge. Its `flowGroupHandles` may bind multiple independently selected Published Flow Groups, each carrying its own `exactDecisionId`, owning relationship handle and decision rank.

Packing order is strongest-per-Document primary units, remaining lexical primary units, then ranked `present-valid` relationship/Flow Group units. On overflow, drop lowest-ranked optional relationship/Flow Group units first and then lowest-ranked non-strongest lexical extras, pruning newly unreachable support after each whole-unit drop. After all drops, retained relationship/Flow Group units become selected-required. Strongest-per-Document evidence and selected-required support are never dropped or partially retained. Required-only overflow refuses assembly; text is never truncated.

## Conversation contract

At most two prior validated turns are retained. Each has question, complete validated answer, prior packet fingerprint, cited handles, and 0-5 stable Logical Document IDs serialized as retrieval hints. Those IDs and their order are fingerprint-bound. A prior hint receives only the exact one-point total boost after the Document independently passes current eligibility. All underlying rows are re-fetched. Prior prose and handles cannot support a current claim.

New conversation, mode/scope change, access loss, or coordinator disposal clears state. Failed attempts do not append state. Conversation is never persisted or logged.

## Provider streaming and repair contract

The provider receives only question, mode, packet fingerprint, opaque handles, bounded packet content, and bounded conversation context. It receives no database/source/analysis-tool access and no callable retrieval tool.

Both primary and repair use [chat-provider-streaming-v1.md](chat-provider-streaming-v1.md). One 29-second deadline covers retrieval through render. The ordered pipeline keeps raw reads/body, strict wire UTF-8, SSE framing, received logical Provider Chunks, assembled received response, JSON/escape decoding, decoded answer deltas, and final decoded answer distinct. One complete nonterminal SSE `data` event is one Provider Chunk: before JSON decoding, its exact received `data`-value payload bytes, including JSON syntax/escapes/metadata but excluding SSE framing and `[DONE]`, must be <=16,384. The sum across all Provider Chunks must be <=65,536 received bytes. Separate raw-body/SSE safety caps and equal decoded-delta/final-answer defense-in-depth limits do not replace those normative received caps. Only a non-empty decoded delta or valid `[DONE]` resets inactivity, and physical EOF is observed after terminal to reject missing/duplicate/contradictory/post-terminal behavior.

Track B emits only neutral content-dark runtime observations: opaque observation ID/sequence, request/attempt/provider/request fingerprints, response/read/EOF dispositions, timings, counters and transport outcomes. `ChatSynthesisAttempt.RuntimeObservationId` points only to this transport record. Validation/repair/duration belong to the separate coordinator `CompletionSummaryId`/`ValidationOutcome`. Neither contains candidate, run, corpus case, public key, signature or export path and neither is release evidence.

Track C composes a sealed same-process attestation sink admitted only by the production raw response-header adapter. It signs the immutable runtime observation plus candidate/run/case/process, exact adapter type/version/assembly hash, provider configuration, canonical `stream=true` request, response status/media/protocol, first physical read, and EOF disposition. Aggregate SDK fake chunks are release-ineligible regardless of metric consistency.

Initial response syntax rejection may produce one repair request only when at least three seconds remain. Repair receives the same packet/fingerprint/deadline, the invalid bounded structured output, and one closed syntax category. It cannot add evidence or change scope. There is no third call and no non-streaming provider fallback may produce an accepted provider answer.

Malformed/overlong/surrogate/truncated UTF-8, size overflow, disconnect, inactivity, cancellation, deadline, missing/duplicate/contradictory terminal, post-terminal data, semantic rejection, unknown/mismatched handle, unsupported claim, or currentness failure discards the whole buffer and is never repairable.

Provider streaming is not UI progressive reveal. After the entire result passes every barrier, existing Unicode text-element reveal may present the accepted answer; reduced motion or insufficient remaining deadline renders it immediately. Operational deterministic rescue is not a streamed provider result and cannot count as an accepted Tier 3 streamed answer.

## Currentness contract

Immediately before render, validate:

- every selected Logical Document still points to the expected immutable current Document Version;
- every selected section span/content and Source Anchor identity/path/line/hash;
- every relationship's current directed Edge and both authorized endpoints;
- every Cross-Repository REFERENCES relationship's canonical selected decision, complete valid decision count/set fingerprint and contribution authority;
- every selected Flow Group is still `Published`, bound to the same current Business Logic Document, decision, Edge, trace, and ordered steps;
- the same authorized applied Global/System/two-Repository advisory Context values/order/`UpdatedAt`/authority fingerprint; and
- current access to both selected Repositories.

Any mismatch returns one non-identifying evidence-unavailable/missing-equivalent result with no citations.

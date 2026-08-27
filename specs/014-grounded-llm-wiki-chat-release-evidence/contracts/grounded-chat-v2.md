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

`ChatRetrievalRequest` includes normalized question/scope/mode, bounded transient conversation, and deadline. It does not contain caller-selected Document/Edge/Flow Group IDs.

## Deterministic primary selection

Eligible rows are current authorized `IndexEntry` and `DocumentSection` projections inside the selected System/two Repositories. The ranker extracts:

- exact symbols and endpoint identifiers;
- Repository names and Repository-relative paths;
- normalized ASCII identifiers/terms;
- CJK bigrams after the closed ignored-term filter;
- Business or IT mode terms; and
- bounded terms from prior validated conversation context.

Precedence is exact symbol, endpoint, path, title, heading, mode-specific term, prose, bounded Business Logic/advisory fallback. Score components are integers. Canonical ties use mode Document-type preference, Repository ID, Logical Document ID, section order, and stable section ID.

Selection rules:

- at most five Documents;
- approximately twelve primary sections and never more than twenty total;
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

Expansion starts from only the top two or three primary Documents, follows exactly one hop, adds at most two related Documents per seed, and never exceeds five Documents, thirty-two relationships, or twelve steps. Canonical order is applied before bounds. Cycles, duplicates, unauthorized/stale endpoints, missing anchors, incomplete/unpublished groups, non-exact/non-unique decisions, and direction mismatch are excluded. Required support failure rejects the packet.

## Packet contract

The JSON shape is defined by [chat-evidence-packet-v2.schema.json](chat-evidence-packet-v2.schema.json). Every handle is packet-local and opaque. The canonical fingerprint includes the schema, request/scope/mode, complete ordered evidence, accepted decision/Flow Group bindings, advisory context, and conversation state.

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

The builder may drop an optional whole low-ranked section and its unused optional support. It never truncates section text or required evidence.

## Conversation contract

At most two prior validated turns are retained. Each has question, complete validated answer, prior packet fingerprint, cited handles, and stable Logical Document hints. A prior hint receives only a small fixed boost after the Document independently passes current eligibility. All underlying rows are re-fetched. Prior prose and handles cannot support a current claim.

New conversation, mode/scope change, access loss, or coordinator disposal clears state. Failed attempts do not append state. Conversation is never persisted or logged.

## Provider streaming and repair contract

The provider receives only question, mode, packet fingerprint, opaque handles, bounded packet content, and bounded conversation context. It receives no database/source/analysis-tool access and no callable retrieval tool.

Both primary and repair use [chat-provider-streaming-v1.md](chat-provider-streaming-v1.md). One 29-second deadline covers retrieval through render. The ordered pipeline keeps raw body reads, strict wire UTF-8, SSE fields/events, JSON/escape decoding, decoded answer deltas, and final assembly distinct. One decoded delta after JSON unescape/re-encoding is the <=16,384-byte Provider Chunk; concatenated decoded deltas are the <=65,536-byte complete response. Separate raw/SSE implementation safety caps do not replace those acceptance caps. Only a non-empty decoded delta or valid `[DONE]` resets inactivity, and physical EOF is observed after terminal to reject missing/duplicate/contradictory/post-terminal behavior.

Tier 3 accepts only content-dark observations signed by the same-process production raw response-header adapter and bound to candidate/case/request, exact adapter type/version/assembly hash, provider configuration, canonical `stream=true` request, response status/media/protocol, first physical read, and EOF disposition. Aggregate SDK fake chunks are release-ineligible regardless of metric consistency.

Initial response syntax rejection may produce one repair request only when at least three seconds remain. Repair receives the same packet/fingerprint/deadline, the invalid bounded structured output, and one closed syntax category. It cannot add evidence or change scope. There is no third call and no non-streaming provider fallback may produce an accepted provider answer.

Malformed/overlong/surrogate/truncated UTF-8, size overflow, disconnect, inactivity, cancellation, deadline, missing/duplicate/contradictory terminal, post-terminal data, semantic rejection, unknown/mismatched handle, unsupported claim, or currentness failure discards the whole buffer and is never repairable.

Provider streaming is not UI progressive reveal. After the entire result passes every barrier, existing Unicode text-element reveal may present the accepted answer; reduced motion or insufficient remaining deadline renders it immediately. Operational deterministic rescue is not a streamed provider result and cannot count as an accepted Tier 3 streamed answer.

## Currentness contract

Immediately before render, validate:

- every selected Logical Document still points to the expected immutable current Document Version;
- every selected section span/content and Source Anchor identity/path/line/hash;
- every relationship's current directed Edge and both authorized endpoints;
- every Cross-Repository REFERENCES relationship's accepted exact decision-to-Edge binding;
- every selected Flow Group is still `Published`, bound to the same current Business Logic Document, decision, Edge, trace, and ordered steps; and
- current access to both selected Repositories.

Any mismatch returns one non-identifying evidence-unavailable/missing-equivalent result with no citations.

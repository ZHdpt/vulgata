# OCT-EXPLORE data model

This is a read/presentation model, not a database schema. OCT-BUSINESS owns shared identities, DTO definitions, acceptance and persistence. Terms below are responsibilities until its compiled checkpoint supplies exact types; no Explore equivalent may be minted.

Initial imported type mapping (T001–T003 only): Core `Vulgata.Core.DomainServices.OctoberKnowledge.DocumentHandle` uses Guid batch/publication/logical/version identities and optional string SectionId. `ReadSelection` and `ContinuationToken` remain opaque owner records. `ContentPage` contains Status, DocumentSummary, Content, qualified Claims, Next and Coverage; `RelatedDocument` contains Kind, exact From/To and Meaning. Use Shared `ReadKnowledgeRequest`/`KnowledgeContentResponse` and `RelatedKnowledgeRequest`/`KnowledgeRelatedResponse` wrappers, never a parallel Knowledge model. The existing controlled fixture validates these shapes; production section hierarchy, byte-limited large-content splitting and final accumulated graph-disclosure semantics remain owner dependencies, as listed in the consumer boundary. None is implemented by this lane's import probes.

## Owner-supplied values

| Value | Required fields/meaning | Relationships and validation |
| --- | --- | --- |
| Selection | Explicit batch/publication and version-bound Knowledge Read Context; effective permitted scope/mode/purpose and trusted caller binding | Shared service proves membership. A client identifier is input, not authorization. Missing selection never resolves implicitly to latest. |
| Exact Document reference | Owning publication identity, Document Version identity and optional stable section identity | Section belongs to exact version; publication permits that version. Preserve logical identity if the owner supplies it; never derive semantic IDs from titles/paths. |
| Safe Document result | Safe title/type, structured ordered sections, permitted provenance, material Uncertainties and currentness/coverage/source-availability qualifications | Business only BL and permitted nontechnical fields; entire composite withheld if any contributor fails live policy. Unknown content schema fails safely. |
| Accepted relationship | Relationship evidence identity, exact source/target, REFERENCES or GENERATED_FROM, supported meaning/direction | Both endpoints and relationship contributors must be allowed in the same selection. BL-to-CL provenance is omitted in Business without revealing hidden topology. |
| Discovery/related page | Permitted results, selection-bound continuation, bounded-result/coverage qualification | Owner applies policy before counts/continuation. No denied totals, hidden-degree metadata or denied-target placeholders. |
| Exact-content chunk / continuation | Owner-defined structured projection, at most 64 KiB UTF-8 per response, ordered section/chunk position, adjacent qualifications and opaque next/terminal state | Owner cursor binds database/batch/publication, subject/purpose/mode/scope, operation/query/order/limit, exact section/chunk position and expiry. Consume its existing type; never mint local offsets or a second continuation schema. Valid UTF-8 and complete owner-defined chunk structure are required. |

## Explore-owned transient state

- Browse request: explicit owner selection, bounded page size (default 20), owner-issued continuation. No advanced filters/search in this slice.
- Reader state: current exact reference including optional section, only the current safe content chunk with its qualifications, owner-issued opaque next cursor/terminal state, `LoadState`, generic error code and request generation. Explicit next replaces rather than appends; no previous-chunk stack, persisted content or shared cache. Content completeness is separate from `LoadState.Loaded`: a loaded chunk may be visibly partial.
- Graph view: owner selection stamp, at most 40 nodes/80 accepted Edges, displayed counts, safe qualifications and `Incomplete`. Nodes preserve owner exact reference and safe labels; Edges preserve accepted identity/type/direction. View is derived, never an accepted Knowledge record.
- Viewport state: pan offsets, bounded zoom, selected visible node and layout coordinates. No domain Edge mutation or shared persistence.
- Request capture: view generation plus mode/scope/selection, cancellation source and deadline. Only a matching successful capture may publish state.

## State transitions

`Idle -> Loading -> Loaded | Empty | NoResults | Error`. Manual reload uses `Refreshing`; explicit cancellation may use `Cancelling` before returning to empty/idle safe state. `NoResults` means an authorized bounded query has no matches; `Empty` means no permitted published items in the selected view. Safe unavailable/denied detail is feature status, not an extension of the shared LoadState enum.

New request, mode/scope/selection change or learned access loss clears owned protected results and invalidates earlier captures. Coherent authorized result replaces the graph atomically; an error/timeout never restores cached data or joins old nodes to new Edges. Pointer/layout state may be reused only where it contains no withdrawn protected metadata. A fresh read is required on reattachment.

For content continuation, capture the next cursor before clearing the displayed chunk, then allow only one outstanding next request. Retain it only as in-flight request state bound to the same generation and exact selection/version/section. Success replaces the view with the next owner-ordered chunk and cursor; failure/cancellation clears in-flight and displayed state. Context changes also invalidate the generation, including version or section navigation. Terminal state comes only from the owner, not from a short/empty chunk, locally counted bytes or an error. Invalid/expired continuation requires explicit restart at the same exact target using a fresh authorized selection; no old chunks/cursors survive that restart. An oversized section remains partial until its own terminal result, without substituting a whole-Document read.

## Persistence and disclosure

No new tables, migrations, DbContext, semantic cache, publication selection store or relationship acceptance records. Knowledge owns all such records. UI serialization/rendering is a disclosure: the Web feature facade obtains final live validation from the shared owner before releasing the accumulated result. Identifiers and diagnostics are not permission to disclose titles, source paths, contributor names or counts. Previously delivered pixels cannot be recalled, but no new interaction can reuse prior authorization blindly.

# OCT-CHAT consumer boundaries and checkpoint

The 2026-10-07 contract-only continuation is recorded in [Chat's joint dialogue/disclosure contribution](dialogue-disclosure-contribution.md): concrete proposed fields/limits, admission/save/history semantics, full-input lineage gaps and Business conformance decisions. It is a consumer proposal awaiting owner agreement, not implemented canonical declarations or authority to resume dependent tasks. Its baseline imports integrated Knowledge/reader implementations; earlier K0-import status below remains historical to that checkpoint.

Status: consumer requirements mapped to the imported K0 declarations/controlled fixture in [imported-k0-and-w0.md](imported-k0-and-w0.md). Production Knowledge services and dialogue persistence are not delivered by that import. OCT-BUSINESS owns canonical Knowledge and persistence types. Chat owns only conversation/events and wiki adapter semantics. Integration owns composition and shared provider admission; Explore owns exact reader routing.

## K0 — required compiled contract checkpoint

Before dependent production implementation, pin one integration commit containing the Business-owned Core interfaces/DTOs and shared labelled fixture. The bounded preparation imports `3bcd815b09cfbac7418d6644a2a75816e0788ec0`, containing Business candidate `07c3fe4463e3228a6982fd106b58148ca87974bd` and handoff `7a189c34aec625202f74846d4907ded787ccbdca`. Exact symbols, controlled conformance and the independent W0 dependency are recorded in [imported-k0-and-w0.md](imported-k0-and-w0.md); integration still owns aggregate checkpoint acceptance. This acknowledgement does not accept K0 on another lane's behalf or authorize production implementation.

K0 must demonstrate these semantics with the common fixture:

| Boundary | Required meaning / checkpoint observation |
| --- | --- |
| Selection and ordinary-reader policy | Explicit database target/batch/publication, permitted Repository membership and mode. Separate real Identity/System/Repository authority from operator setup. Unknown/unpublished/mismatch/unavailable cannot fall back. |
| Knowledge Read Context | Version-bound discovery, pagination, related membership and exact content; no long-lived database transaction across network/model work. New publication cannot change a turn's selected membership. |
| Live authorization | Subject, Chat purpose, mode and every source/advisory contributor checked before tool/model/UI/history disclosure; authorization outage fails closed. A contributor union can be authorized without exposing denied metadata. |
| Bounded shared reads | Search/browse, exact content/section, existing-related reads; limits/continuation/provenance/qualifications/Uncertainties returned. Pagination tokens cannot change selection/scope or disclose denied counts. |
| Exact handles | Document Version and optional section bound to selected publication; canonical titles only after permission. No implicit latest, fabricated Git origin or legacy ScanRun. |
| Dialogue port | Begin/fence/lookup, ordered authorized history, atomic terminal outcome, identical replay/conflict and orphan interruption per data-model.md. Business implements persistence; Chat owns lifecycle. |
| Shared fixture | CL, BL, accepted relationship, real-shaped uncertainty, disallowed contributor, exact sections and wrong/unpublished selections; includes one/several/standalone membership and denial/revocation cases. Label fixture outputs as controlled. |

Dialogue additions may be a small owner-issued extension commit to K0, but their implementation and schema must land before C1 performs any real provider or production user-facing dispatch. Prove durable begin/fence and terminal save/lookup with focused PostgreSQL checks first. Before that, C1 is test-only controlled mechanics using agreed interface doubles; no live provider execution, production fallback or shadow schema. Owner corrections propagate as shared commits to all consumers.

## Wiki tool facade (Chat-owned, names provisional until tool approval)

Server binds subject, purpose=Chat, mode, conversation, turn, read selection and budgets. None are model-overridable arguments. Tools never accept host paths, raw SQL, arbitrary URLs, credentials or arbitrary publication IDs.

| Logical tool | Bounded model input | Output and effects |
| --- | --- | --- |
| SearchWiki / BrowseWiki | query/filter allowed by B0; page size and opaque continuation | Permitted metadata/handles, provenance, uncertainty/coverage and continuation. No fabricated total denied count; discovery is not a content read. |
| ReadWiki | selection-issued handle, optional exact section, optional owner-issued opaque content continuation | At most 64 KiB of complete UTF-8 content/projection per result with material claim-group qualifications, actual returned coverage and owner-issued continuation; only successfully returned content contributes read-ledger coverage. |
| GetRelatedDocuments | permitted selection-issued handle, allowed relationship kind and continuation | Existing accepted relationship direction/meaning and permitted targets; no inferred Edge or content-read credit for targets. |
| ReportAnswerDocuments | ordered bounded current-turn read handles/sections | Validated accepted references or bounded sanitized invalid-reference result. Non-reading report operation affects only Chat footer state. It cannot mutate Knowledge or supply titles/URLs. Empty list valid. |

ReadWiki passes content continuation unchanged to the Business-owned exact-read port; it neither decodes nor constructs cursors. Business binds the token to database/batch/publication, subject/purpose/mode/scope, operation, exact version/section/chunk position and expiry. Each continuation rechecks live authority; changing a handle/section/selection, tampering or expiry returns the owner's safe invalid-continuation/unavailable result, never another selection. Documents and individual sections larger than 64 KiB remain readable through successive bounded calls. Preserve complete UTF-8, qualified claim-group context and explicit returned coverage at each boundary; a chunk must not detach a material clause from its advisory/mixed attribution. All calls/results consume the existing aggregate/turn budgets; exhausted budgets permit an explicitly incomplete answer, not silent truncation or a claim that unread content was read. Cancellation/failed reads add no partial page to the ledger.

Reference report is explicit and may occur before or after text within the conversation. Deduplicate in first accepted report order; repeated reports may add valid handles, never renumber streamed text. Reject invented/foreign-turn/foreign-section handles. A document/section reference can identify partially read content, but its ledger and permitted footer coverage must say partial and qualify only successfully returned chunks; a returned prefix never qualifies unseen sections or the remainder of that section. Mark whole-document/section coverage complete only when the owner's coverage/continuation establishes all of that target was returned. Omitted/failed report yields an empty/limited footer, not regenerated text or a false grounding claim. Recheck accepted reports at final footer rendering and persistence/history disclosure. No automatic fallback that cites every search result.

Retrieved prose is untrusted evidence. Application allowlists and contracts remain authoritative even if a Document requests another tool, changes scope, or includes fake report IDs. Hypotheses/unknowns must be labelled from their first streamed text via prompt and behavior qualification, not a complete-answer semantic gate.

Preserve Business's existing `SourceSupported`, `Advisory`, `Hypothesis`, `Uncertain` and `Mixed` qualifications at material claim-group level, with its ClaimGroupId/evidence references and explicit clause attribution for Mixed groups. Reuse owner types; do not define a competing enum/schema. Prompts require visible source/advisory distinctions beside the affected prose from its first streamed presentation, including content before/between tool rounds and narrative follow-ups. A hidden footer, contributor list or document-level tag is insufficient. Business Mode retains safe attribution without CL/source identifiers. Saved text and reused completed history retain the material qualifications; bounded history selection cannot keep the claim while dropping its qualifying context. If that context cannot fit, omit the whole affected group/turn with an honest context-limit indication. No per-sentence citations, whole-answer semantic verifier, full-answer buffering or new publication gate is introduced.

Shared conformance fixture: reuse Business's source-supported lock-acquisition versus supplied business-motive example. A source-backed lock action cannot become a proven business motive or duration merely because advisory context suggests it. Preserve explicit attribution in mixed answers and in a follow-up using saved history; distinguish missing evidence from a source-supported conclusion. Include owner-fixture cases for both a >64-KiB multi-section Document and a >64-KiB single section, with qualified multibyte text crossing chunk boundaries. Explore owns reader UI continuation separately.

## Typed execution/UI events (Chat-owned)

Envelope: ConversationId, context identity, TurnId, monotonically increasing per-turn sequence and UTC observation time. Events are local application types, not a new public MCP API.

- `TurnStarted`: actual admitted running state, no fictional percentage.
- `ToolStarted`, `ToolCompleted`, `ToolFailed`: stable tool-call ID and generic localized category; never raw arguments/results, document titles without authorization or hidden reasoning.
- `AnswerDelta`: immediately released authorized text. Small render coalescing up to 50 ms target is allowed, full-answer buffering is not. This is a target to measure, not universal provider latency.
- `ReferencesUpdated`: currently authorized exact reference projection independent of answer delivery.
- `TurnSaved`: terminal content/reference/status only after committed outcome confirmed.
- `TurnSaveUncertain`: sanitized saving-indeterminate state, generation already stopped, retry disabled pending durable lookup.

Tools and text may interleave. Ignore duplicate event sequences within one attachment and reject events for another context/turn; no new reconnect replay protocol. Sanitize rendered Markdown, disable raw HTML and suppress model-authored external links/citation URLs. Only server-issued exact reader references become clickable Document links. Diagnostics record IDs/timings/counts/status, not raw content.

## Execution limits and provider completion

Deployment-overridable initial defaults (technical budgets, not coverage promises): question 8,000 characters; 6 provider request rounds; 16 total tool calls; 2 simultaneous tool calls maximum; tool arguments 16 KiB each; shared-read page <=20 items or stricter B0 cap; individual content result <=64 KiB; aggregate tool results <=256 KiB; output <=64 KiB UTF-8; history <=12 completed turns and <=48 KiB; total turn <=180 seconds; provider SSE body <=2 MiB per round and <=8 MiB per turn. Count refused/malformed calls against the budget. Do not silently truncate UTF-8, tool JSON or claimed complete content; use labelled bounded/section reads or a classified incomplete outcome.

Integration coordinates an agreed provider request/rate policy with separately owned process-local Web and generator permits; this is not a cross-process coordinator or globally enforced capacity claim. Chat uses Web's pool; generator Business finalization uses Scan's pool. Acquire/release per actual provider request; waiting consumes original turn deadline. Tool work does not hold an unnecessary provider permit. Original limits never reset across rounds, content continuation or save recovery. Disable automatic transport/SDK generation retries and failover for this slice.

Native tool fragments assemble by request/choice/call index and ID with consistent function name; reject conflicting IDs/names, duplicate execution and unknown tools. Invoke only a fully assembled validated call. End-of-stream alone, length/content-filter finish, partial arguments, missing terminal framing or outstanding function calls cannot count as completed. MAF continuation through a tool-call finish is distinct from final generation completion. Preserve strict UTF-8, framing, byte/deadline and sanitized-error checks in the provider adapter.

## Shared reader and composition handoff

Explore supplies a server-side exact-destination projection consuming B0's publication/version/section identity and current mode. Route path/parameter spelling is deliberately owned by Explore, not invented here. Unknown or revoked destinations return unavailable/denied; no redirect to current/legacy content. C2 can test an agreed projection fixture; real navigation cannot pass until the actual route is integrated.

Integration binds the opt-in October Chat module to ordinary Web's frozen DefaultConnection and explicit publication. Web does not migrate October, require generator operator credentials or invoke IOctoberBatchSelector as reader authority. Existing administration starts without published wiki; Chat reports unavailable. Host-specific selected Chat profile/credential source, shared admission and orphan-owner identity must be supplied explicitly before C3.

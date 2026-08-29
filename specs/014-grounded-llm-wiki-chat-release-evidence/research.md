# Research: Grounded LLM-Wiki Chat & Release Evidence

## Sources inspected

- `AGENTS.md`; `.specify/memory/constitution.md`; `docs/UBIQUITOUS_LANGUAGE.md`
- `docs/bmad/epics.md`, Story 12.5
- `docs/bmad/implementation-artifacts/12-5-grounded-llm-wiki-chat-and-august-30-release-evidence.md`
- August 30 sprint-change proposal, bounded-scanning architecture, and dated PRD overlay
- accepted Story 12.4 artifacts at immutable dependency commit `4798b97d9f23aef587829175df7ab7ebc3daf995`
- `specs/008-demo-chat-release-gate` as stale lifecycle but reusable implementation history
- Story 12.5 specification content, `decisions/design-correction-02.md`, and exact clean source checkpoint `719c2a5afc9b2a0161fff4c9deb7b26531ebf66d`
- current Prepared Chat contracts, single typed retriever, current-v2 relationship/currentness validators, scoped conversation, raw streaming provider dispatch/result validation, completion observation, Unicode-safe page reveal, tests, durable Scan entities/recovery/manual-resume API, accepted Track A UI/evidence preparation, and release harness

No `CONTEXT-MAP.md`, `CONTEXT.md`, repository ADR directory, or context ADR exists at the inspected boundary.

## R-1: Prepared Chat remains the controlling module

**Decision**: deepen the current `EvidencePacket` → candidate extraction/ranking → `ChatAgentDispatcher` → `ChatAgentResultValidator` → currentness/authorization → complete render flow.

**Rationale**: source already proves closed opaque handles, canonical fingerprint, authorization before retrieval and after synthesis, current Document/section/Source Anchor/relationship revalidation, bounded claims/citations, provider failures, Business/IT modes, and a mature page/test surface. Story 12.5 changes retrieval authority and bounded continuity, not the trust boundary.

**Alternatives considered**:

- General Agent tool loop: rejected because the dated architecture explicitly replaces it with bounded server retrieval and the provider must not choose scope or relationships.
- New parallel Chat endpoint/service: rejected because it duplicates security ordering and creates ambiguous production composition.

**Consequence**: packet schema and internal interfaces version; public page semantics and failure outcomes remain recognizable.

## R-2: Legacy exact evidence is not accepted v2 relationship truth

**Decision**: a Cross-Repository expansion fact requires one or more independently valid accepted Story 12.4 exact-match `CrossRepositoryResolutionDecision` rows plus their shared exact current directed `DocumentEdge`. `ExactCrossRepositoryEdgeEvidence` is legacy-only. For one Edge, select the first decision under the Story 12.4 slot ordering as the relationship witness and bind the complete valid set through count plus canonical fingerprint; distinct valid decisions are not inconsistent and may own distinct Published Flow Groups.

**Rationale**: checkpoint `719c2a5a` already removed the legacy retriever/currentness authority and consumes the accepted v2 projector. Story 12.4 source explicitly permits many endpoint/entry decisions to share one document-level Edge, retains independent evidence, and binds each complete/published Flow Group to its decision. Legacy one-to-one profile evidence cannot represent that cardinality, so mutation/structural tests must prevent its return.

**Alternatives considered**:

- Accept either legacy or v2: rejected because a stale legacy row could authorize an answer after v2 found ambiguity or changed current evidence.
- Backfill v2 decisions from legacy rows: rejected because endpoint/consumer-entry identity and full contribution evidence are not reconstructable.

**Consequence**: Track B waits for the accepted final Story 12.4 commit and adapts without changing its contract. B2 must prove two endpoint/entry decisions -> one Edge -> two independently decision-owned Published Flow Groups remain stable/current under randomized enumeration, while conflicting duplicate slots or later set-fingerprint changes fail closed.

## R-3: Relational projection, not a new persistence/search system

**Decision**: deterministically score current `IndexEntry`/`DocumentSection` rows, then expand one persisted relationship hop.

**Rationale**: the current store already materializes the exact sources named by PRD FR-4.7/FR-11.6. The August architecture defines exact symbol/endpoint/path/title/heading precedence, CJK bigrams, mode terms, bounded fallback, and stable ties. PostgreSQL queries plus an in-process canonical ranker satisfy the release without embeddings or schema changes.

**Alternatives considered**:

- PostgreSQL full-text/trigram as sole ranking: rejected because mixed CJK/exact identifier precedence and cross-provider deterministic behavior need an explicit canonical scorer.
- Embeddings/cross-encoder: explicitly deferred and adds operational/data-boundary risk.

**Consequence**: stream current eligible sections in stable-ID order, deduplicate before a bounded top-K, and retain the highest-ranked 256 or exhaustion; a 257th or larger eligible set is ranked rather than refused. Parse structured term arrays and use token equality, apply each published integer component `50/40/30/25/20/5/3/1` at most once where 25 applies only to the active mode's term field, add one total prior-current-Document point, and let all positive-score candidates compete before selecting strongest-per-Document then extras to an exact target of twelve. Randomized enumeration, duplicate conflicts, 257+ sets, and weak-metadata/strong-prose cases must produce the specified identities/fingerprints.

## R-3A: The single typed cutover is a preserved architecture invariant

**Decision**: preserve exactly one typed snapshot -> lexical primary projection -> current-v2 expansion -> packet-builder pipeline. Keep the literal Cross-Repository question classifier, exact-row rebuild branch, projection-only early final return, duplicate packet construction, direct legacy authority reads, and direct in-method projector construction absent.

**Rationale**: the referenced audit observed those defects at an older checkpoint, but exact source checkpoint `719c2a5a` already constructor-composes lexical projection, relationship projection and one packet builder and contains none of the named legacy symbols. Replanning their deletion would discard valid implementation evidence. The current design problem is acceptance/mutation proof and B4 publication ownership, so the corrected plan makes the topology a regression invariant.

**Alternatives considered**:

- Add T080 to special-case the branch: rejected because it preserves the design defect and another question spelling can select another authority.
- Feature-flag old and new retrieval: rejected because coexistence keeps two production authorities and makes release evidence configuration-dependent.
- Let the projector return a complete packet: rejected because it can replace lexical identities and duplicates packing policy.

**Consequence**: structural and mutation tests enforce one packet-builder call, mandatory relationship-projector interaction and absence of classifier/legacy queries. Behavioral tests freeze lexical identities while relationship fixtures vary. B1 can be reaccepted independently without rewriting the retriever.

## R-3B: Applied Context is the only advisory source

**Decision**: add provider-independent `IChatAdvisoryContextReader` over the existing applied `GlobalContext`, selected `System.Context`, and exact two selected `Repository.Context` fields. It accepts only server-created `AuthorizedChatScope`, reads inside the retrieval transaction, ignores `PendingContextChange`, and returns a typed currentness/fingerprint snapshot. Missing/all-blank applied values are a valid canonical empty context.

**Rationale**: existing Context management already distinguishes applied values from pending changes and the existing composition service establishes Global -> System -> Repository semantics. Calling that per Repository would duplicate Global/System context and supplies neither caller authorization nor currentness identity. The Chat-specific reader composes the exact two-Repository scope once without persistence changes or caller-controlled text.

Normalize NFC, CRLF/CR to LF and outer Unicode whitespace; reject disallowed controls. Order Repository units by RFC 4122 ID bytes. Extract the same distinct ordinal ASCII terms/CJK bigrams as lexical retrieval. The complete canonical text is 0-8,192 UTF-8 bytes: exact succeeds, +1 returns `InconsistentCurrentEvidence`, and no truncation/drop is permitted. Advisory terms run only in the zero-direct-match Business Logic summary fallback, with search/summary 20, heading 5, title 3 and prose 1 each at most once. Packet and authority fingerprints bind exactly the used text/source identity/order/`UpdatedAt`; render currentness recomputes it.

**Alternatives considered**:

- Caller-provided advisory text: rejected because it bypasses server ownership and authorization.
- Call `IContextCompositionService` twice: rejected because Global/System are duplicated and no typed currentness binding is returned.
- Silently truncate/drop Context units: rejected because the actual provider input would no longer equal the source authority and canonical fingerprint.

**Consequence**: B1 public-seam vectors cover positive fallback, empty/nonmatching/pending/caller-negative cases, authorization/currentness mutation, stable two-Repository ordering, fingerprint change, and exact 8,192/+1 bytes before provider dispatch.

## R-4: Relationship expansion is a closed projector

**Decision**: use only current REFERENCES, GENERATED_FROM, accepted exact decision/Edge pairs, and complete `Published` Flow Groups; seeds are exactly the first `min(3, primaryDocumentCount)` lexical Documents, <=2 additions per seed, <=5 Documents, one hop, <=32 relationships, <=10 Flow Group authorities, <=12 flow steps.

**Rationale**: these are publication facts. Current Flow Group code has explicit `Complete` → `Published` and candidate decision/Edge fields. Raw CodeGraph and incomplete groups are analysis/planning evidence, not published wiki truth.

**Alternatives considered**:

- Recursive CTE: rejected because it makes depth and cycle effects harder to audit and exceeds one hop.
- Treat all Flow Groups with a Business Logic ID as eligible: rejected because state, decision, trace, and current-version consistency must also hold.

**Consequence**: one projector returns complete relationship support units or closed exclusion reasons under the four-state table `absent` / `present-valid` / `present-inconsistent` / `selected-required`. Optional invalid facts cannot poison unrelated lexical evidence; after selection, any required-support defect refuses the assembly and cannot substitute a lower-ranked unit.

## R-5: Encoded packet size belongs to a dedicated builder

**Decision**: `ChatEvidencePacketBuilder` canonicalizes handles and enforces 64 KiB evidence, 4 KiB conversation, and 8 KiB server-owned advisory context using UTF-8 byte counts; it binds the advisory authority fingerprint and the canonical selected-decision/count/full-set fingerprint for every Cross-Repository relationship.

**Rationale**: current `EvidencePacket.Create` enforces cardinalities but no byte cap. Chinese text and JSON framing make character counts insufficient. A builder can drop optional whole low-ranked sections without letting retrieval or provider code partially truncate support.

**Alternatives considered**:

- Put byte checks in the EF query: rejected because JSON framing, handles, and relationship dependencies do not exist until projection.
- Let provider SDK truncate: rejected because it breaks the fingerprint and could remove citations.

**Consequence**: packet construction is deterministic and independently testable with exact/+1 UTF-8 vectors.

## R-6: Conversation is scoped transient server state

**Decision**: retain at most two prior validated turns in the already-scoped `PreparedChatCoordinator`; clear on new conversation/scope/mode/access failure/disposal.

**Rationale**: `ChatPage` already presents turns, but `AskAsync` is stateless. The coordinator is server-owned and can retain stable Document hints without accepting caller-fabricated evidence. No persistence is needed; the feature explicitly excludes saved Chat sessions and long memory.

**Alternatives considered**:

- Browser/local storage: rejected because clients could alter handles and stale answers.
- EF Chat session: rejected because it adds migration/lifecycle/privacy scope.
- Carry only previous prose: rejected because retrieval needs stable current-document hints and prose is not evidence.

**Consequence**: restart loses conversation continuity by design but persisted wiki/Chat capability remains; each follow-up fully re-fetches evidence.

## R-7: Mandatory provider streaming needs a dedicated Chat seam

**Decision**: replace only `ChatAgentDispatcher`'s provider-call seam with an Agents-internal raw OpenAI-compatible adapter and ordered raw-body -> strict wire UTF-8 -> SSE -> received logical Provider Chunk gate -> assembled received-response gate -> JSON/escape -> decoded answer-delta bytes -> final decoded-answer assembly pipeline. Both primary and repair use it under one 29-second deadline.

**Rationale**: current aggregate/Pass 1/Pass 2 paths cannot prove which bytes a bound governs. FR-054 explicitly says received bytes and the Provider Stream definition separates response chunks from terminal outcome. Therefore one complete nonterminal SSE `data` event is the logical Provider Chunk, and its exact received `data`-value payload octets—including JSON syntax, escapes and metadata, excluding only SSE framing and `[DONE]`—are counted before JSON decoding. Each is <=16,384 and their attempt-wide sum is <=65,536. A 16,384-byte requested read buffer, 131,072-byte SSE line/event cap, 1,048,576-byte raw-body cap, decoded-delta <=16,384 and final decoded-answer <=65,536 remain independent safety/defense bounds; none redefines the received domains.

**Alternatives considered**:

- Keep `RunAsync` or treat SDK aggregation as streaming: rejected because raw framing/byte/terminal observations would be unprovable.
- Reuse Pass 1/Pass 2 transport directly: rejected because their liveness, terminal, aggregation, and bounds intentionally serve different workflows.
- Generalize all Agent streaming now: rejected because it broadens ownership and regression scope; Story 12.5 needs one dedicated Chat seam.
- Measure only decoded answer deltas: rejected because escape/metadata-heavy chunks could exceed FR-054 received-byte bounds while decoding small.
- Count complete SSE framing as the Provider Chunk: rejected because physical line endings, field syntax, comments and parser-inserted multi-line separators are framing, not the logical provider payload; their overhead is already bounded separately.
- Reset timeout for each call: rejected because repair could exceed PRD NFR-1.2.
- Retry semantic/evidence failures: rejected because it can invent claims and disclose why protected evidence failed.

**Consequence**: received `\u4e2d` counts six Provider Chunk bytes before JSON and three decoded-delta bytes afterward; metadata grows received totals while leaving decoded counters unchanged. The offending 16,385th per-chunk or 65,537th assembled received byte rejects before JSON decoding. Pure post-JSON seams retain exact/+1 decoded bounds; full-pipeline collisions prove received ownership wins first. Only a valid non-empty decoded delta or `[DONE]` resets inactivity. Every stage discards the whole pipeline on failure, and Track C recomputes every independent content-dark counter. Pass 1/Pass 2 remain unchanged evidence, not authority.

## R-7B: Streaming transport is not progressive browser disclosure

**Decision**: provider frames remain untrusted server-side data through complete syntax, packet-fingerprint, evidence-handle, citation, semantic, currentness, and authorization validation. Only an accepted complete answer can enter existing `ChatClaimReveal`; reduced motion or insufficient remaining deadline renders it immediately.

**Rationale**: a later byte can invalidate JSON, terminal integrity, citations, or semantics, and authorization/currentness can change while the provider runs. The current `ChatTurn` reveal already enumerates Unicode text elements, which is appropriate presentation after acceptance but not a security boundary for provider chunks.

**Alternatives considered**:

- Forward deltas after local JSON token boundaries: rejected because final shape and evidence validation are not known.
- Forward complete individual claims early: rejected because the packet/currentness/authorization result is atomic.
- Remove progressive reveal: unnecessary; its accepted-output-only implementation remains useful and supports reduced-motion behavior.

**Consequence**: `ChatPage` shows only generic pending/cancellation state until acceptance. No fragment, claim, citation, handle, relationship, or Source Anchor detail crosses the client boundary early. A Web-scoped publication owner precomputes immediate/progressive presentation and commits only accepted immutable content.

## R-7C: Deadline acceptance needs one named publication linearization point

**Decision**: add one scoped `IPreparedChatPublicationOwner`. Before its critical section, the coordinator completes transport, validation, re-fetch/currentness/authorization, answer construction, proposed conversation construction, runtime-observation lookup, neutral completion-summary-template construction, reveal-plan selection and all allocation/fallible callbacks. The owner then samples cancellation and the monotonic deadline once and performs one non-throwing atomic terminal-state exchange on a preallocated composite candidate. This `AcceptedPublicationCommit` simultaneously makes the answer, next conversation, accepted completion summary and reveal directive authoritative.

**Rationale**: checkpoint `719c2a5a` performs a live deadline check, constructs success, mutates conversation, records completion telemetry and returns across separate statements. Adding another check after each statement cannot close the race because time can always advance after the newest check. A linearizable state machine instead defines the authoritative observation: under 29 seconds means the monotonic sample immediately before the sole commit is strictly before the absolute deadline. Time after the commit does not retroactively invalidate accepted state, and no later time read or fallible success mutation is permitted.

**Alternatives considered**:

- Keep adding fake-clock checks around conversation, telemetry and result return: rejected because it has no final owner and generated T079-T081 convergence churn.
- Record an accepted completion summary before publishing the answer: rejected because timeout/cancellation could leave false accepted telemetry.
- Publish the answer first and append conversation/summary later: rejected because readers could observe a success that has no matching current conversation or completion authority.
- Hold an EF transaction or database lock through provider streaming and browser rendering: rejected because publication/conversation are scoped transient state and a long transaction cannot make client scheduling atomic.
- Define success by a clock check after UI rendering: rejected as the only Track B safety rule because the browser can be delayed indefinitely and already disclosed accepted content cannot be rolled back.

**Consequence**: Track B has one logical acceptance point and zero post-commit success side effects. Immediate reveal stores the complete visible accepted answer in the composite record. Progressive reveal is allowed only when precomputed to fit the internal budget; a live page supervisor may switch the already accepted answer to complete display without changing acceptance. Track C separately records actual complete-render time and rejects release evidence at 30,000 ms or later. This adds one Web seam and no persistence, Story 12.4, provider transport or security-boundary change.

## R-8: Production Scan UI reads durable state directly

**Decision**: preserve the accepted Track A specific Core read interface, Infrastructure EF projector, Web current-user coordinator, and Chinese production pages. Reuse the existing recovery/manual-resume authority; this plan correction makes no Track A production change.

**Rationale**: `ScanExecution` and `ScanJobExecution` persist prepared/running/retry/pause/completed-with-warning/integrity/security state, next attempts, counts, and File Code Unit identities. `ManualResumeEndpoints` already proves authorization and recovery. `ScanHistoryPage` is a placeholder; `DemoScanPage` uses random circuit state and an iframe.

**Alternatives considered**:

- Promote `DemoScanUiState`: rejected because it is explicitly fake and session-scoped.
- Add SignalR now: rejected because live push is not required; bounded refresh over persisted state proves truth with less shared integration.

**Consequence**: no Scan strategy changes; status is a read projection, and resume remains a durable authorized command.

## R-9: Evaluation and evidence are schema-validated data

**Decision**: a fixed 12-20 case JSON corpus and closed Tier 1/2/3 envelope retain all prior status/check rules. Track B emits a release-neutral content-dark runtime observation from the production raw response-header adapter. Release schema 2.2 makes a Track C-only sealed same-process attestation sink the exporter: it admits observations only directly from that adapter and signs candidate/run/case/process bindings without adding release concerns to the runtime transport contract.

**Rationale**: Story 11.5 harness supplies useful ownership/cleanup/polling patterns, but its image/container topology, prepared profile, August 15 identity, and lifecycle are stale. Story 12.5 needs candidate/revision/tool/prompt/fingerprint/restart/screenshot traceability and automatic authorization-negative failure.

**Alternatives considered**:

- Markdown checklist only: rejected because field completeness and aggregate metrics are not mechanically enforceable.
- Desired-answer fixtures: rejected because usefulness must be judged without prescribing provider prose.
- Free-form or merely tier-counted check IDs: rejected because a passing envelope could omit a specific build, restart, real-Scan, real-Chat, or integrity gate while preserving the same tier count.
- Trust recorded aggregates after threshold checks: rejected because mutually inconsistent counts and percentages could still pass; every aggregate must be recomputed from its named hashed receipt artifact.
- Truncation, binary floating-point rounding, or banker's rounding for usefulness: rejected because implementations could disagree at a half-step; exact rational two-decimal round-half-up arithmetic is the single portable rule.
- Provider-content logs or screenshots as streaming proof: rejected because they expose protected evidence and are unnecessary; timing/count/outcome metrics prove the transport without content.

**Consequence**: Track B runtime observations contain only opaque observation ID/sequence, request/attempt/provider/request fingerprints, response/read/EOF dispositions, timings, counters and transport outcome. `ChatSynthesisAttempt.RuntimeObservationId` never carries validation. A separate coordinator `CompletionSummaryId` owns ordered attempt IDs, repair use, validation outcome and accepted duration. During Track C, process start creates an ephemeral ECDSA P-256 attestation authority; preflight binds its public-key/process/candidate and exact adapter type/version/assembly hash. The sealed sink signs a projection over both immutable sources plus run/case/candidate context. The semantic verifier validates signature, sealed admission, exact transport equality, exact completion-summary equality, and same-process/candidate bindings before aggregate recomputation. A perfectly consistent SDK aggregate fake-chunk receipt is rejected solely for missing authorized adapter-to-sink provenance. All earlier status conditioning, append-only correction, half-up scoring, ten checks, command/artifact bindings, and content redaction remain unchanged.

## R-10: Tier 2/3 database safety is a preconnection invariant

**Decision**: accept only a newly generated `vulgata_story125_*` database and reject known development/Story 12.4 qualification identities before opening a product connection.

**Rationale**: release verification is destructive to its own disposable database. Prefix validation after connection is too late. Existing Story 11.5 nonce-ownership logic demonstrates a reusable fail-closed pattern.

**Alternatives considered**:

- Operator confirmation: rejected because it is not deterministic and does not prevent automation error.
- Shared integration database: expressly forbidden and invalidates restart/duplicate evidence.

**Consequence**: invalid target is a recorded preflight failure with zero product read/write.

## R-11: No persistence migration

**Decision**: derive every new Chat/Scan/evidence view from existing accepted rows; store conversation and release-run working state outside production EF.

**Rationale**: the current model already contains the necessary facts. New tables would create avoidable ownership collision with in-progress Story 12.4 and complicate the release.

**Alternatives considered**:

- Add Chat session/evaluation/evidence tables: rejected as out of scope and unnecessary.
- Add a denormalized Chat relationship projection table: rejected because it duplicates current Edge/decision/Flow Group authority and risks drift.

**Consequence**: any discovered missing durable fact is a plan blocker, not permission to edit `VulgataDbContext` or migrations.

## R-12: Planned-change trigger outcome

**Decision**: architecture review is mandatory and accepted plan review must be followed by a planned-change authorization pause before task generation.

**Rationale**: although the provider receives no callable retrieval tool, repository implementation history explicitly classifies `IChatEvidenceRetriever.RetrieveAsync` as a Chat Agent retrieval tool. Checkpoint `719c2a5a` already embodies the material cutover from one prepared legacy exact-flow packet to current section-ranked, v2 decision/Edge/Flow Group-expanded, conversation-bound packet 2.2 evidence and from aggregate `RunAsync` to mandatory bounded primary/repair streaming; the corrected plan preserves and mutation-guards those changes. AD-7C additionally changes the server Chat completion/publication boundary to one deadline-linearized owner. Together they satisfy the Agent-tool trigger. The real Scan UI is read/resume over existing policy and is not a Scan strategy change. The dated PRD overlay already authorizes bounded server retrieval, one-hop persisted expansion, citations, and under-30-second Chat; the streaming/publication trust boundary strengthens those results and creates no PRD conflict/weakening.

**Alternatives considered**: classify only provider-callable JSON functions as tools. Rejected because `specs/008-demo-chat-release-gate/plan.md` and its Chat contract already recorded the exact Core retrieval interface as the governed Chat Agent tool.

**Consequence**: after independent plan acceptance, pause for explicit authorization of AD-2 through AD-7C and AD-10 before tasks, citing specification FR-053 through FR-060, PRD lines 42-44, 285, and 380, and `design-correction-02.md`. Then run Sol/high task authoring, definition review, and the separate mandatory implementation-approval gate. Track B/C dependency gates still apply.

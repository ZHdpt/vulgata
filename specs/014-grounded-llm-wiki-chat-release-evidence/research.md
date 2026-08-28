# Research: Grounded LLM-Wiki Chat & Release Evidence

## Sources inspected

- `AGENTS.md`; `.specify/memory/constitution.md`; `docs/UBIQUITOUS_LANGUAGE.md`
- `docs/bmad/epics.md`, Story 12.5
- `docs/bmad/implementation-artifacts/12-5-grounded-llm-wiki-chat-and-august-30-release-evidence.md`
- August 30 sprint-change proposal, bounded-scanning architecture, and dated PRD overlay
- accepted Story 12.4 artifacts at immutable dependency commit `4798b97d9f23aef587829175df7ab7ebc3daf995`
- `specs/008-demo-chat-release-gate` as stale lifecycle but reusable implementation history
- accepted Story 12.5 spec and `decisions/design-correction-01.md` at clean source commit `73c317f10917236e7a4ac0ad55f4966abd18024e`
- current Prepared Chat contracts, retriever, currentness validator, coordinator, non-streaming provider dispatch/result validation, Unicode-safe page reveal, tests, Pass 1/Pass 2 streaming implementations, durable Scan entities/recovery/manual-resume API, placeholder Scan-history page, fake demo state, and release harness

No `CONTEXT-MAP.md`, `CONTEXT.md`, repository ADR directory, or context ADR exists at the inspected boundary.

## R-1: Prepared Chat remains the controlling module

**Decision**: deepen the current `EvidencePacket` → candidate extraction/ranking → `ChatAgentDispatcher` → `ChatAgentResultValidator` → currentness/authorization → complete render flow.

**Rationale**: source already proves closed opaque handles, canonical fingerprint, authorization before retrieval and after synthesis, current Document/section/Source Anchor/relationship revalidation, bounded claims/citations, provider failures, Business/IT modes, and a mature page/test surface. Story 12.5 changes retrieval authority and bounded continuity, not the trust boundary.

**Alternatives considered**:

- General Agent tool loop: rejected because the dated architecture explicitly replaces it with bounded server retrieval and the provider must not choose scope or relationships.
- New parallel Chat endpoint/service: rejected because it duplicates security ordering and creates ambiguous production composition.

**Consequence**: packet schema and internal interfaces version; public page semantics and failure outcomes remain recognizable.

## R-2: Legacy exact evidence is not accepted v2 relationship truth

**Decision**: a Cross-Repository expansion fact requires an accepted Story 12.4 exact-match `CrossRepositoryResolutionDecision` plus its exact current directed `DocumentEdge`. `ExactCrossRepositoryEdgeEvidence` is legacy-only.

**Rationale**: current `ChatEvidenceRetriever` joins one embedded profile occurrence to one legacy exact-evidence row. Current `ChatEvidenceCurrentnessValidator` repeats that assumption. Story 12.4 candidate source explicitly permits many endpoint/entry decisions to share one document-level Edge, retains independent evidence, and binds each complete/published Flow Group to its decision. Legacy one-to-one profile evidence cannot represent that cardinality.

**Alternatives considered**:

- Accept either legacy or v2: rejected because a stale legacy row could authorize an answer after v2 found ambiguity or changed current evidence.
- Backfill v2 decisions from legacy rows: rejected because endpoint/consumer-entry identity and full contribution evidence are not reconstructable.

**Consequence**: Track B waits for the accepted final Story 12.4 commit and adapts to its accepted names without changing its contract.

## R-3: Relational projection, not a new persistence/search system

**Decision**: deterministically score current `IndexEntry`/`DocumentSection` rows, then expand one persisted relationship hop.

**Rationale**: the current store already materializes the exact sources named by PRD FR-4.7/FR-11.6. The August architecture defines exact symbol/endpoint/path/title/heading precedence, CJK bigrams, mode terms, bounded fallback, and stable ties. PostgreSQL queries plus an in-process canonical ranker satisfy the release without embeddings or schema changes.

**Alternatives considered**:

- PostgreSQL full-text/trigram as sole ranking: rejected because mixed CJK/exact identifier precedence and cross-provider deterministic behavior need an explicit canonical scorer.
- Embeddings/cross-encoder: explicitly deferred and adds operational/data-boundary risk.

**Consequence**: stream current eligible sections in stable-ID order, deduplicate before a bounded top-K, and retain the highest-ranked 256 or exhaustion; a 257th or larger eligible set is ranked rather than refused. Parse structured term arrays and use token equality, apply each published integer component `50/40/30/25/20/5/3/1` at most once where 25 applies only to the active mode's term field, add one total prior-current-Document point, and let all positive-score candidates compete before selecting strongest-per-Document then extras to an exact target of twelve. Randomized enumeration, duplicate conflicts, 257+ sets, and weak-metadata/strong-prose cases must produce the specified identities/fingerprints.

## R-3A: A single typed cutover is mandatory

**Decision**: replace the current two-path retriever with one typed snapshot -> lexical primary projection -> current-v2 expansion -> packet-builder pipeline. Delete the literal Cross-Repository question classifier, exact-row rebuild branch, projection-only early final return, duplicate packet construction, direct legacy authority reads, and direct in-method projector construction.

**Rationale**: pinned source shows `RetrieveInsideTransactionAsync` first creates a complete projection packet and then, only when `RequiresCrossRepositoryEvidence` recognizes `跨仓库` or `cross-repository`, executes a second `ExactRelationshipRow`/provenance path and creates another packet. T021/T055/T065/T070/T075/T080 repeatedly attempted to constrain branches without removing the second primary authority. Test growth could not establish the architectural postcondition because no seam represented immutable lexical primaries followed by optional expansion.

**Alternatives considered**:

- Add T080 to special-case the branch: rejected because it preserves the design defect and another question spelling can select another authority.
- Feature-flag old and new retrieval: rejected because coexistence keeps two production authorities and makes release evidence configuration-dependent.
- Let the projector return a complete packet: rejected because it can replace lexical identities and duplicates packing policy.

**Consequence**: architectural interaction RED tests must fail against the old composition before replacement writes. Structural tests enforce one packet-builder call and absence of classifier/legacy queries; behavioral tests fix lexical identities while relationship fixtures vary.

## R-4: Relationship expansion is a closed projector

**Decision**: use only current REFERENCES, GENERATED_FROM, accepted exact decision/Edge pairs, and complete `Published` Flow Groups; seeds are exactly the first `min(3, primaryDocumentCount)` lexical Documents, <=2 additions per seed, <=5 Documents, one hop, <=32 relationships, <=10 Flow Group authorities, <=12 flow steps.

**Rationale**: these are publication facts. Current Flow Group code has explicit `Complete` → `Published` and candidate decision/Edge fields. Raw CodeGraph and incomplete groups are analysis/planning evidence, not published wiki truth.

**Alternatives considered**:

- Recursive CTE: rejected because it makes depth and cycle effects harder to audit and exceeds one hop.
- Treat all Flow Groups with a Business Logic ID as eligible: rejected because state, decision, trace, and current-version consistency must also hold.

**Consequence**: one projector returns complete relationship support units or closed exclusion reasons under the four-state table `absent` / `present-valid` / `present-inconsistent` / `selected-required`. Optional invalid facts cannot poison unrelated lexical evidence; after selection, any required-support defect refuses the assembly and cannot substitute a lower-ranked unit.

## R-5: Encoded packet size belongs to a dedicated builder

**Decision**: `ChatEvidencePacketBuilder` canonicalizes handles and enforces 64 KiB evidence, 4 KiB conversation, and 8 KiB advisory context using UTF-8 byte counts.

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

**Decision**: replace only `ChatAgentDispatcher`'s provider-call seam with an Agents-internal raw OpenAI-compatible adapter and ordered raw-body -> strict wire UTF-8 -> SSE -> JSON/escape -> decoded answer-delta bytes -> final answer assembly pipeline. Both primary and repair use it under one 29-second deadline.

**Rationale**: current aggregate/Pass 1/Pass 2 paths cannot prove which bytes a bound governs. Physical reads split arbitrarily; SSE and JSON have independent grammar; JSON escapes change lexical versus answer byte counts. FR-054 is implementable when Provider Chunk means one decoded content delta re-encoded as strict UTF-8 (<=16,384) and complete response means concatenated decoded delta bytes (<=65,536). A 16,384-byte requested read buffer, 131,072-byte SSE line/event cap, and 1,048,576-byte raw-body cap separately bound implementation overhead without weakening acceptance.

**Alternatives considered**:

- Keep `RunAsync` or treat SDK aggregation as streaming: rejected because raw framing/byte/terminal observations would be unprovable.
- Reuse Pass 1/Pass 2 transport directly: rejected because their liveness, terminal, aggregation, and bounds intentionally serve different workflows.
- Generalize all Agent streaming now: rejected because it broadens ownership and regression scope; Story 12.5 needs one dedicated Chat seam.
- Reset timeout for each call: rejected because repair could exceed PRD NFR-1.2.
- Retry semantic/evidence failures: rejected because it can invent claims and disclose why protected evidence failed.

**Consequence**: JSON escape bytes count in raw/SSE safety domains and after unescape in decoded-delta acceptance; `\u4e2d` is six raw bytes and three answer bytes. Only a valid non-empty decoded delta or `[DONE]` resets inactivity. The adapter reads through physical EOF; every stage owns its grammar/overflow/failure and discards the whole pipeline. Exact/+1 and arbitrary-split vectors cover wire scalars, CR/LF, SSE fields, JSON tokens/escapes, deltas, terminal metadata, and post-terminal reads. Pass 1/Pass 2 remain unchanged evidence, not authority.

## R-7B: Streaming transport is not progressive browser disclosure

**Decision**: provider frames remain untrusted server-side data through complete syntax, packet-fingerprint, evidence-handle, citation, semantic, currentness, and authorization validation. Only an accepted complete answer can enter existing `ChatClaimReveal`; reduced motion or insufficient remaining deadline renders it immediately.

**Rationale**: a later byte can invalidate JSON, terminal integrity, citations, or semantics, and authorization/currentness can change while the provider runs. The current `ChatTurn` reveal already enumerates Unicode text elements, which is appropriate presentation after acceptance but not a security boundary for provider chunks.

**Alternatives considered**:

- Forward deltas after local JSON token boundaries: rejected because final shape and evidence validation are not known.
- Forward complete individual claims early: rejected because the packet/currentness/authorization result is atomic.
- Remove progressive reveal: unnecessary; its accepted-output-only implementation remains useful and supports reduced-motion behavior.

**Consequence**: `ChatPage` shows only generic pending/cancellation state until acceptance. No fragment, claim, citation, handle, relationship, or Source Anchor detail crosses the client boundary early. The reveal consumes the same deadline and cannot push completion beyond it.

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

**Consequence**: Track B runtime observations contain only opaque observation ID/sequence, request/attempt/provider/request fingerprints, response/read/EOF dispositions, timings, counters and outcome. During Track C, process start creates an ephemeral ECDSA P-256 attestation authority; preflight binds its public-key/process/candidate and exact adapter type/version/assembly hash. The sealed sink signs the immutable runtime observation plus run/case/candidate context. The semantic verifier validates signature, sealed admission, and same-process/candidate bindings before aggregate recomputation. A perfectly consistent SDK aggregate fake-chunk receipt is rejected solely for missing authorized adapter-to-sink provenance. All earlier status conditioning, append-only correction, half-up scoring, ten checks, command/artifact bindings, and content redaction remain unchanged.

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

**Rationale**: although the provider receives no callable retrieval tool, repository implementation history explicitly classifies `IChatEvidenceRetriever.RetrieveAsync` as a Chat Agent retrieval tool. Story 12.5 materially changes that tool from one prepared legacy exact-flow packet to current section-ranked, v2 decision/Edge/Flow Group-expanded, conversation-bound packet v2 evidence, and changes the Chat Agent provider dispatch from aggregate `RunAsync` to mandatory bounded primary/repair streaming. That satisfies the Agent-tool trigger. The real Scan UI is read/resume over existing policy and is not a Scan strategy change. The dated PRD overlay already authorizes bounded server retrieval, one-hop persisted expansion, citations, and under-30-second Chat; the streaming trust boundary strengthens those results and creates no PRD conflict/weakening.

**Alternatives considered**: classify only provider-callable JSON functions as tools. Rejected because `specs/008-demo-chat-release-gate/plan.md` and its Chat contract already recorded the exact Core retrieval interface as the governed Chat Agent tool.

**Consequence**: after independent plan acceptance, pause for explicit authorization of AD-2 through AD-7B before tasks, citing revised spec FR-053 through FR-060 and PRD lines 42-44, 285, and 380. Then run tasks/definition review and the separate mandatory implementation-approval gate. Track B/C dependency gates still apply.

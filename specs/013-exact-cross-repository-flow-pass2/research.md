# Research: Exact Cross-Repository Flow & Pass 2

## Authority order and dated release overlay

**Decision**: Treat the August 30 bounded-scanning design sections 11-12 and the approved 2026-08-17 recovery proposal as the dated release overlay over PRD FR-4.5, FR-6.1 and FR-6.2. Preserve the PRD's one Worker Agent per Flow Group, Business Logic Document synthesis, GENERATED_FROM provenance, honest Uncertainty, and post-scan resolution intent while narrowing this release to exact Custom RPC, safely normalizable exact HTTP, endpoint-centered grouping, exactly two Repositories, one relationship hop, and the stated cardinality/byte bounds.

**Rationale**: The proposal explicitly records these temporary replacements and says the long-term V1 requirements remain backlog. BMad Story 12.4 and its accepted specification repeat the same boundary.

**Alternatives considered**:

- Implement general graph-first path tracing and clustering now. Rejected because it contradicts the dated overlay and the Story 12.4 exclusions.
- Delete or weaken the broad PRD requirements. Rejected because the proposal preserves them as post-release backlog.

## Production endpoint evidence from accepted Story 12.2/12.3 outputs

**Decision**: Do not add endpoint/provider meaning to `CommunicationKindClassification`. Reconstruct candidates at Story 12.4 time from the exact current pair: Confirmed classification bindings, immutable `code-logic/file-v2` content, Source Anchors, `CrossRepository` deferred observations, exact same-Repository REFERENCES Edges, and a fresh safe evaluation of the same closed catalog rule against revision-bound source.

For Custom RPC, a consumer endpoint comes only from a grammar-valid target symbol/path in a `CrossRepository` deferred observation that shares Source Anchor evidence with a Confirmed RPC communication observation. A cross-version consumer-entry locator is derived from stable source semantics—Repository/path, containing qualified type/member signature or stable catalog locator, communication-site kind, and structural occurrence among supported outbound sites in that owner—rather than endpoint value, Document Version or anchor coordinates. A provider endpoint comes only from safe XML catalog matches reproduced at persisted Evidence Locators and owned by a current XML Code Logic Document Version. HTTP requires safe accepted forms on both sides; otherwise it remains an Uncertainty.

**Rationale**: The Ubiquitous Language states that a Communication Kind Classification identifies only kind and does not resolve endpoint or role. Story 12.3 deliberately leaves endpoint/provider resolution to Story 12.4. The current catalog evaluator already retains exact identifiers transiently, and the revision-bound reader plus Source Anchors lets Story 12.4 reproduce them without trusting free-form prose or altering old rows.

**Alternatives considered**:

- Parse the Worker's `observation` prose. Rejected because it is model-authored and not deterministic identity evidence.
- Backfill endpoint columns into Story 12.2 classifications. Rejected because it would reinterpret an accepted kind-only contract.
- Use the embedded prepared-demo exact-resolution profile as production configuration. Rejected because the August design names it a regression oracle and because it contains reviewed occurrence selectors rather than general current evidence.

## Protocol and endpoint normalization

**Decision**: Use one closed protocol-aware normalizer and ordinal comparison. Custom RPC applies Unicode NFC, trim, and only catalog-declared fixed-prefix removal; it does not case-fold, take a simple name, or infer aliases. Safe HTTP accepts only queryless/fragmentless catalog-identified HTTP/HTTPS absolute or origin-form values with no userinfo, backslash, control, ambiguous dot segment, or percent-encoded reserved character. It lowercases scheme/ASCII host and removes default port; any path/trailing-slash rewrite must be explicitly catalog-declared.

**Rationale**: Exact normalization must be reproducible and must not collapse semantically distinct endpoints. Failing closed is explicitly preferred to an unsafe HTTP match.

**Alternatives considered**:

- Direct string comparison without normalization. Rejected because catalog-declared safe prefix and basic URI canonicalization are authoritative.
- General `System.Uri` output. Rejected because platform canonicalization and escaping can change meaning outside the approved subset.
- Fuzzy route or symbol matching. Rejected by the story and dated overlay.

## Provider evidence aggregation, uniqueness, and provider implementation

**Decision**: Treat a provider catalog/classification/locator/Source Anchor match as a contributing observation, not as one provider. Within each exact protocol/key bucket, canonically aggregate and deduplicate all observations by `ProviderIdentity = (System, Repository, Scan Run, Profiled Revision, current provider XML Document Version)`. Retain the complete sorted contributing-evidence set and its fingerprint, then count distinct provider identities. Resolve only when that count is one. Two locators or classifications in the same provider Document remain one provider; two current provider Document Versions remain ambiguous. The provider implementation Document is then the unique current target of a validated same-Repository REFERENCES Edge from that XML Document. Implementation uniqueness is a Flow Group completeness rule, not a provider tie-breaker.

**Rationale**: FR-005 counts the sole current provider Document Version, not evidence-row multiplicity. Aggregation retains every fact required for audit/replay while preventing duplicate evidence from manufacturing a false `multiple_providers` outcome. It also satisfies the required Flow Group membership of provider XML plus implementation without letting graph proximity choose the provider.

**Alternatives considered**:

- Resolve directly to an implementation Document. Rejected because implementation links may be missing/ambiguous and are not the endpoint declaration.
- Prefer a provider with a unique implementation when multiple endpoint providers exist. Rejected because it silently breaks the sole-provider rule.
- Count each classification or locator as a provider. Rejected because multiple valid observations can belong to the same current provider Document Version.

## Exact decision-to-document-Edge persistence compatibility

**Decision**: Keep existing `DocumentEdge` uniqueness `(SourceDocumentVersionId, TargetDocumentVersionId, EdgeType)` and keep `ExactCrossRepositoryEdgeEvidence` unchanged as the legacy profile-v1 one-to-one extension. Add one versioned `CrossRepositoryResolutionDecision` per v2 consumer candidate outcome. An exact-match decision owns endpoint/consumer-entry/provider evidence and has a many-to-one FK to the reusable document-level REFERENCES Edge. An unresolved decision owns one Uncertainty and no Edge. Every distinct provider observation persists as one normalized `ExactResolutionProviderEvidenceContribution` child of the decision, so replay reconstructs the complete provider set rather than trusting a digest.

The Edge semantic identity depends only on source Document Version, target Document Version and `References`. A missing Edge receives a deterministic tuple-derived ID; a valid pre-existing row with the tuple is reused with its actual ID. Thus two endpoints or two consumer entries in the same consumer Document that resolve to the same provider Document produce two decisions and two Flow Group seeds but one graph Edge. Decision identity/uniqueness depends on pair, consumer entry/candidate, protocol, normalized endpoint and outcome/provider identity. A later exact decision may link and resolve a prior Uncertainty explicitly; it does not overwrite the prior unresolved decision.

**Rationale**: The Edge is document-level graph topology, while resolution is endpoint/consumer-entry evidence. Conflating them violates the existing unique index and loses a valid second Flow Group. A first-class decision deepens the existing graph without inventing an Edge type or duplicating topology.

**Alternatives considered**:

- Add duplicate REFERENCES Edges for each endpoint. Rejected because it violates the accepted source/target/type uniqueness contract.
- Store multiple endpoint decisions in the one-to-one legacy evidence row. Rejected because one scalar row cannot preserve independent identities, evidence, replay or group seeds.
- Backfill legacy evidence into v2 decisions. Rejected because legacy rows do not contain safe complete endpoint/consumer-entry multiplicity; they remain readable only through the legacy path.

## Flow Group persistence before Business Logic publication

**Decision**: Make the existing `FlowGroup.BusinessLogicDocumentVersionId` optional before publication and add versioned endpoint-centered plan/state fields. Seed one Flow Group from every current v2 exact-match decision, not from each distinct Edge; persist unique `ExactResolutionDecisionId` and deliberately nonunique `ExactResolutionEdgeId`. Persist both a pair/current-evidence-specific Flow Group instance identity and a separate `business-flow-logical-key/1.0` stable key based only on System, consumer/provider Repositories, protocol, normalized endpoint and the cross-version consumer-entry stable key. Reuse `FlowTrace` and `FlowTraceStep` to persist the canonical planned path. Attach the same Flow Group to the Business Logic Document Version atomically on acceptance.

**Rationale**: The current entity represents only a materialized Business Logic result, while Story 12.4 needs durable completeness, omission, job identity and replay before a Worker runs. Separating the two identities lets each pair retain exact execution replay while later Code Logic Document Versions of the same semantic endpoint flow append to the existing consumer-owned Logical Document. Deepening avoids a separate prepared-group model and preserves existing published rows.

**Alternatives considered**:

- New `PreparedFlowGroup` copied into `FlowGroup`. Rejected because the two representations can diverge.
- Keep groups in memory until commit. Rejected because restart and honest omissions are durable requirements.

## Group ordering and release bounds

**Decision**: Canonically order candidate groups by protocol, normalized endpoint UTF-8 bytes, consumer Repository/Document/entry identity, then provider Repository UUID bytes. Persist at most ten Flow Groups. A required-core gap yields `Incomplete`; a candidate needing more than eight Documents or sixteen steps yields `BoundExcluded`; candidates after ten become bounded `Pass2Omission` records. Do not silently trim a group and call it complete.

**Rationale**: This makes repeated planning byte-equivalent and keeps the terminal denominator honest. Optional one-hop evidence is either wholly within the group bound or the candidate is explicitly excluded.

**Alternatives considered**:

- Greedy truncation of optional neighbors. Rejected because omitted context could make the explanation falsely complete.
- LLM or graph clustering. Rejected by scope.

## Pass 2 job persistence

**Decision**: Add a specialized `Pass2JobExecution` bound one-to-one with a complete Flow Group and a `Pass2Execution` bound to the Story 12.3 pair/coordination. Reuse the state/version/lease/failure/retry semantics and provider circuit policy established by Stories 12.1/12.3. Dispatch serially in canonical group order for the dated release.

**Rationale**: `ScanJobExecution` is relationally and semantically bound to one File Code Unit, Repository and Scan Run. A Flow Group spans two Repositories and cannot be represented honestly by that row. A durable specialized job provides restart/replay without weakening the existing table.

**Alternatives considered**:

- Make the File Code Unit fields nullable and overload `ScanJobExecution`. Rejected because it destroys an accepted invariant and still leaves ambiguous Repository ownership.
- In-memory jobs. Rejected because exact restart/acknowledgement replay is required.
- Parallel dispatch. Deferred because no release requirement needs it and serial order reduces provider/resource risk.

## Closed retrieval operations and accounting

**Decision**: `retrieve_code_logic` exposes exactly `Search`, `Sections`, `SourceAnchors`, `SourceExcerpt`, and `RelatedDocuments`. Scope is server-owned. Calls are reserved durably before reading; admitted results receive opaque handles and content-dark descriptor rows. The hard job-wide budget is eight calls, ten returned results, four distinct additional Documents, four sections per Document, and 65,536 cumulative returned UTF-8 bytes.

An exact replay of the same reserved call ordinal reconstructs the same descriptors without double charging. A later repeated request consumes a new call and repeated returned bytes/results again; the distinct additional-Document count increases only for a new Document. A request that would exceed any bound returns one refusal and adds zero result/Document/section/byte usage.

**Rationale**: Reservation-before-read survives crashes and prevents retry from resetting budget. Job-wide cumulative accounting satisfies the explicit bounds and keeps replay deterministic without persisting source content.

**Alternatives considered**:

- In-memory per-provider-turn counters. Rejected because restart/retry can reset them.
- Store returned source text. Rejected for data stewardship and because exact source can be re-read by revision/hash.
- Count only distinct results/bytes. Rejected because repeated calls could bypass the budget.

## Evidence use and GENERATED_FROM derivation

**Decision**: Assign opaque handles to initial and retrieved evidence. Require every material result section/main step to cite handles. Vulgata maps accepted handles to current Document Versions, derives section authorization and the global distinct used set, and creates GENERATED_FROM from that derived set. Eligibility, grouping or return alone never implies use.

**Rationale**: This directly proves all-and-only-used provenance and prevents the model from omitting or adding Document IDs.

**Alternatives considered**:

- Trust a provider-authored `generatedFromDocumentVersionIds` list. Rejected because it can be incomplete or excessive.
- Create provenance for every Flow Group/retrieved Document. Rejected because returned-but-unused Documents must not receive an Edge.

## Provider conversation and result submission

**Decision**: Reuse Story 12.3's characterized bounded streaming and DeepSeek-compatible strict projection mechanics. Pass 2 has exactly two declared functions: `retrieve_code_logic` for evidence and `submit_business_logic` for finalization. Use the same eight retrieval-capable turns, forced turn-nine submission, optional turn-ten correction, no request eleven, zero SDK retry, raw/request/response byte limits, and content-dark diagnostics.

**Rationale**: The one-shot legacy Pass 2 JSON path cannot conduct bounded retrieval. A dedicated finalization function gives the local validator complete candidate bytes and avoids treating provider structured-output acceptance as semantic acceptance.

**Alternatives considered**:

- Free-form final JSON after tool use. Rejected because terminal framing and complete local validation are less reliable on the qualified provider boundary.
- Add direct CodeGraph/JDT-LS tools. Rejected for the release; source/Document retrieval is mandatory and direct analysis is optional.

## Business Logic Document cross-scan identity, ownership, and immutability

**Decision**: The stable Logical Document is owned by the consumer Repository and keyed by `business-flow/v1/<sha256>`, whose canonical preimage is `business-flow-logical-key/1.0`, System, consumer Repository, provider Repository, protocol, normalized endpoint and `ConsumerEntryStableKey`. It excludes pair/execution/scan/revision/Document/anchor/Edge/planner identities. A successful later pair with the same key appends an immutable Document Version and moves the same current pointer; changed endpoint/protocol/Repository/System/semantic entry creates a new key and leaves the old chain intact. Provider evidence is reached through GENERATED_FROM to provider Code Logic Documents and their Source Anchors rather than copied into consumer-owned Source Anchors.

**Rationale**: Existing domain tables require one Repository owner per Logical Document/Document Version/Source Anchor and already enforce unique `(Repository, DocumentType, StableKey)` plus a concurrency-token current pointer. Consumer ownership is deterministic, the cross-scan key preserves Version Chains across changing current Code Logic Documents, and the provenance graph preserves cross-Repository grounding.

**Alternatives considered**:

- A System-owned Document with no Repository. Rejected because it conflicts with existing knowledge-store ownership and authorization.
- Copy provider source anchors under the consumer Repository. Rejected because it falsifies Repository/Scan Run identity.
- Use pair-specific Flow Group ID as `StableKey`. Rejected because every scan pair would create an unrelated Logical Document and no cross-scan Version Chain.

**Schema evolution**: execution/planner/resolution version changes do not alter `business-flow-logical-key/1.0`. A semantic locator/canonicalization change requires a new logical-key schema identity. No new identity may silently reuse an old chain; reuse requires an explicit reviewed key-alias/data migration proving equivalence, otherwise it creates a new stable key.

## Atomic publication and replay

**Decision**: In one PostgreSQL transaction, revalidate job lease/group/exact decision/shared Edge/contributions/handles/current Documents, acquire a stable-key-scoped lock, resolve or create the unique consumer-owned Logical Document, lock/revalidate its current pointer, materialize the immutable Business Logic Document successor, derive/persist GENERATED_FROM, attach/publish the Flow Group and traces, complete the job, and advance the current pointer. Another decision sharing the same Edge is never substituted for the group's own decision evidence. Competing cross-pair publications serialize; a loser reloads and revalidates the winner's pointer before any append. Exact same-completion replay reconstructs the full chain/graph and writes nothing; changed replay conflicts or performs a newly validated successor append, never a second logical slot.

**Rationale**: This survives acknowledgement loss without partial provenance or duplicate Documents and makes current-pointer concurrency explicit across different pair-specific Flow Groups sharing one semantic key.

**Alternatives considered**:

- Knowledge commit followed by job completion. Rejected because a crash exposes an unacknowledged partial state.
- Fingerprint-only replay. Rejected because missing/extra relationship rows could be hidden.

## Partial completion and terminal reporting

**Decision**: Successful groups commit independently. Failed jobs, incomplete groups, bound-excluded groups, and group-limit omissions have closed durable outcomes. Pass 2 completes as warning-bearing when any non-success exists, including zero successful groups, unless integrity/security closes more strongly. Resolution Edges and Uncertainties are never rewritten by group failure.

**Rationale**: This matches Story AC-4 and preserves useful output without fabricating missing flows.

**Alternatives considered**:

- Pair-wide rollback on one group failure. Rejected because the story explicitly permits remaining groups to succeed.
- Synthesize from reduced evidence. Rejected because incomplete evidence must not produce a Business Logic Document.

## Migration shape

**Decision**: One cohesive migration owns Flow Group compatibility changes—including exact-decision/stable consumer-entry/logical-key fields—new v2 `CrossRepositoryResolutionDecision`/provider-contribution ownership, Uncertainty links, Pass 2 execution/jobs/retrieval/omissions, indexes, FKs and snapshot. It leaves the existing DocumentEdge source/target/type unique index and legacy `ExactCrossRepositoryEdgeEvidence` rows untouched and performs no unsafe v2 backfill. It also reuses the existing Logical Document Repository/type/stable-key unique index and current-pointer concurrency token. Prove clean and legacy apply, rollback/reapply when no v2 rows prevent restoration, and no pending model changes.

**Rationale**: These relational changes jointly enforce start, planning, job, retrieval, publication and replay atomicity; an intermediate schema would not be valid.

**Alternatives considered**:

- Multiple independently deployed migrations. Rejected because there is no safe partially implemented Story 12.4 runtime.
- Destructive replacement/backfill. Rejected because legacy prepared-demo and Story 12.1-12.3 rows must remain readable.

## ADR and context search

**Decision**: Record that the pinned tree contains no `CONTEXT-MAP.md`, `CONTEXT.md`, `docs/adr/`, or context-scoped ADR to apply.

**Rationale**: The Constitution requires conflicts to be surfaced. None can be evaluated where no artifact exists; the plan must not invent an ADR.

**Alternatives considered**: none.

## Research conclusion

All technical unknowns are resolved from repository authority and current source. No authority contradiction, external dependency selection, unresolved design question, or template marker remains. The three planned-change triggers remain workflow approvals after independent plan review, not research blockers.

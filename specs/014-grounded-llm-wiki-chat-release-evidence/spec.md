# Feature Specification: Grounded LLM-Wiki Chat & Release Evidence

**Feature Branch**: `demo0831/story-12-5`

**Created**: 2026-08-27

**Status**: Draft

**Input**: Story 12.5 — a fresh schema-v3 activation that delivers a bounded delta over the existing Prepared Chat implementation: current LLM-Wiki retrieval, streaming grounded multi-turn Business/IT answers, real Scan-state presentation, and August 30 release evidence. `specs/008-demo-chat-release-gate` is implementation history only, never lifecycle authority.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask a Grounded Business or IT Question (Priority: P1, Track B)

As an authenticated user, I can ask a scoped question in Business Mode or IT Mode and receive an answer grounded only in current authorized Documents from the generated LLM-Wiki, so that I can understand the demonstrated Cross-Repository business flow at the level of detail I need.

**Why this priority**: Grounded question answering is the primary user value, while currentness and authorization prevent a fluent answer from becoming an untrustworthy disclosure.

**Independent Test**: With Story 12.4's accepted final checkpoint integrated, ask representative Business and IT questions over an authorized System and two Repositories; verify deterministic retrieval, mode-appropriate answers, bounded evidence, and missing-equivalent behavior for unauthorized or stale scope.

**Acceptance Scenarios**:

1. **Given** an authenticated user selects an authorized System, its two release Repositories, and Business Mode, **When** the user asks a relevant question, **Then** the answer is an understandable Chinese business narrative grounded in current Business Logic Documents and supporting Code Logic Documents.
2. **Given** the same authorized scope in IT Mode, **When** the user asks a technical question, **Then** the answer presents technical detail with current Document and Source Anchor evidence.
3. **Given** a scoped question, **When** evidence is retrieved, **Then** deterministic lexical scoring selects at most five current authorized Documents and bounded sections, with exact symbol, endpoint, path, title, and heading matches preferred over prose matches.
4. **Given** useful primary evidence exists, **When** related evidence is expanded, **Then** expansion is at most one hop and uses only current persisted REFERENCES Edges, GENERATED_FROM Edges, exact Cross-Repository Resolution decisions and their DocumentEdges, and published Flow Groups.
5. **Given** no useful current evidence exists after the bounded fallback, **When** answering is attempted, **Then** the user receives a bounded insufficient-evidence outcome and no fabricated claim.

---

### User Story 2 - Verify Every Material Claim Across Turns (Priority: P1, Track B)

As a user, I can continue a short conversation and inspect evidence for every material technical claim, so that follow-up answers remain useful without treating earlier prose as truth.

**Why this priority**: Multi-turn convenience must not weaken the evidence boundary; each answer must be independently auditable against current source-grounded Documents.

**Independent Test**: Ask an initial question and two follow-ups, then supersede or revoke access to cited evidence. Verify bounded conversation continuity, re-fetch and revalidation, complete citations, whole-answer rejection on semantic evidence failure, and no restricted disclosure.

**Acceptance Scenarios**:

1. **Given** a valid bounded evidence packet, **When** synthesis runs, **Then** the packet contains opaque evidence handles, a canonical fingerprint, current sections, Source Anchors, required relationships, bounded advisory context, and bounded conversation state.
2. **Given** an answer contains material technical claims, **When** validation completes, **Then** every accepted claim cites supporting packet evidence and the complete answer is validated before any claim is rendered.
3. **Given** synthesis begins, **When** the provider responds, **Then** it uses a streaming response and the server assembles bounded untrusted chunks without exposing provider text to the user.
4. **Given** streamed chunks split a multi-byte or multi-code-point user-visible character, **When** the complete stream is assembled, **Then** strict UTF-8 validation preserves the character and no broken or replacement character is rendered.
5. **Given** streamed provider data is still arriving or the assembled answer has not completed syntax, packet, authorization, currentness, citation, and semantic validation, **When** the Chat UI updates, **Then** zero partial claims, citations, handles, relationships, or provider fragments are rendered.
6. **Given** the provider disconnects, exceeds the inactivity or overall deadline, is cancelled, exceeds a chunk or response-size bound, emits malformed UTF-8, omits or duplicates its terminal outcome, or sends data after terminal completion, **When** streaming ends, **Then** the whole attempt fails closed with a bounded Chinese outcome and no partial answer.
7. **Given** the provider returns syntactically malformed output and enough of the shared 30-second answer budget remains, **When** correction is attempted, **Then** at most one streaming syntax repair is allowed; semantic evidence failure rejects the whole answer without repair.
8. **Given** a fully validated Grounded Answer, **When** it is presented, **Then** the existing Unicode-safe progressive reveal may display that accepted answer without exposing the earlier provider stream, and reduced-motion preference may display it immediately.
9. **Given** a user asks a follow-up, **When** earlier cited Documents influence retrieval, **Then** prior validated answers are conversation context only and every underlying Document, section, Source Anchor, relationship, current version, and authorization decision is re-fetched and revalidated.
10. **Given** authorization is revoked or evidence becomes stale between retrieval and rendering, **When** currentness validation runs, **Then** the answer is withheld and the outcome reveals no restricted identity, source, relationship, citation, or business detail.

---

### User Story 3 - Monitor and Resume a Real Scan (Priority: P1, Track A)

As a SystemOwner, I can see the real persisted state of a production Scan and act when it pauses, so that progress and release screenshots describe actual durable behavior rather than simulated Backup B state.

**Why this priority**: Operational trust and release evidence require the UI to reflect durable product state, including warnings and recovery, independently of final Chat relationship integration.

**Independent Test**: Present persisted Scan executions in preparation, running, retry, paused, resumed, completed, completed-with-warnings, and failed states; verify Chinese labels, counts, bounded reasons, next retry, failed-file summary, authorization, and the `继续扫描` action without using fake state.

**Acceptance Scenarios**:

1. **Given** an authorized SystemOwner views a real Scan, **When** its persisted state changes, **Then** the Chinese UI distinguishes preparation/indexing, running, retrying, paused, resumed, completed, completed with warnings, and integrity/security failure.
2. **Given** a running or retrying Scan, **When** status is shown, **Then** completed and total counts, the next retry when present, and a bounded non-sensitive reason are visible.
3. **Given** a recoverable paused Scan, **When** the SystemOwner selects `继续扫描`, **Then** the UI invokes the authorized durable manual-resume behavior and reflects its persisted outcome.
4. **Given** a Scan completes with warnings, **When** the SystemOwner opens its result, **Then** a bounded failed-file summary is accessible without presenting omitted work as successful.
5. **Given** isolated fake Backup B state is enabled for development, **When** release evidence is collected, **Then** that state and its screenshots are explicitly ineligible as release evidence.

---

### User Story 4 - Prepare a Repeatable Chat Evaluation (Priority: P2, Track A)

As a release evaluator, I have a fixed, source-traceable question corpus and scoring record, so that Chat usefulness, citation coverage, authorization, and timing can be evaluated consistently before and during the final release run.

**Why this priority**: A curated evaluation prevents selective demonstrations and can be prepared before Story 12.4's final checkpoint without asserting final retrieval behavior.

**Independent Test**: Validate a 12–20 question corpus across Business/IT modes, expected scope, expected evidence themes, useful-answer rubric, citation checks, authorization-negative cases, timing fields, and source authority references without executing dependency-sensitive production work.

**Acceptance Scenarios**:

1. **Given** the release question corpus, **When** its entries are reviewed, **Then** it contains 12–20 representative Business/IT questions, including cross-Repository flow, failure or decision conditions, follow-ups, and insufficient-evidence cases.
2. **Given** a corpus entry, **When** it is evaluated, **Then** its authorized scope, mode, expected evidence themes, useful-answer criteria, citation expectation, and timing result can be recorded without embedding a desired answer as ground truth.
3. **Given** an authorization-negative case, **When** results are scored, **Then** any restricted evidence or existence disclosure is an automatic failure.
4. **Given** evidence-preparation artifacts, **When** they are reported, **Then** they distinguish planned checks from observed evidence and preserve revisions, versions, fingerprints, commands, durations, and artifact provenance.

---

### User Story 5 - Prove the August 30 Release Candidate (Priority: P1, Track C)

As a release owner, I can inspect one lightweight, human-auditable Track C-MVP evidence set from the accepted candidate, so that release claims are backed by real tools, real source, durable restart behavior, and grounded Chat results without building a second release-proof product.

**Why this priority**: Story 12.5 is not complete until the integrated product is observed under the release boundary; test-only or fake-state claims are insufficient.

**Independent Test**: After Story 12.4's accepted final commit is integrated and Track B is accepted, execute Tier 1 regression plus one combined host integration and real release run against one pinned candidate and one isolated disposable database; verify the retained outputs, restart, manual evaluation, screenshots, and security boundaries.

**Acceptance Scenarios**:

1. **Given** a Tier 1 checkpoint, **When** focused verification runs, **Then** retrieval, relationship expansion, streaming assembly and failure handling, post-validation reveal, packet validation, citations, conversation bounds, authorization, currentness, UI states, evaluation fixtures, and deterministic behavior are covered without a network LLM.
2. **Given** Track C-MVP begins, **When** preflight completes, **Then** it records the candidate commit, exact Repository Profiled Revisions, tool and provider versions, non-secret provider configuration identity, unique lowercase `vulgata_story125_*` database ownership, and redacted commands with exit results before product data access.
3. **Given** the combined host integration and real release run, **When** final verification runs, **Then** host-run Vulgata uses PostgreSQL 17 as the only required Docker service, two real sample Repositories, real CodeGraph, JDT LS, and configured LLM, and at least 50 automatically selected File Code Units.
4. **Given** the real Scan completes, **When** straightforward database queries inspect release outcomes, **Then** at least one exact Cross-Repository REFERENCES Edge, one honest Uncertainty, and three Business Logic Documents including one Cross-Repository flow exist, with source-anchored Pass 1 and Pass 2 output bound to the exact Profiled Revisions.
5. **Given** the twelve representative questions are executed while the provider is available, **When** results are manually scored, **Then** at least 80% are useful, 100% of accepted material technical claims are cited, zero unauthorized evidence is disclosed, mandatory streaming remains in use, and every accepted answer completes in under 30 seconds.
6. **Given** the application process restarts against the same release database, **When** the authenticated wiki and Chat are reopened, **Then** persisted Documents and grounded Chat remain accessible without duplicate publication or data loss.
7. **Given** final evidence is assembled, **When** release claims are reviewed, **Then** a human-readable Markdown report contains the preflight, commands and exits, query results, manual evaluation table, restart proof, redacted real Scan/Business Chat/IT Chat/Cross-Repository/persisted-result screenshots, failures, and limitations.
8. **Given** Track C-MVP is implemented, **When** scope is reviewed, **Then** it adds no ECDSA attestation sink, private/public key lifecycle, sealed raw-adapter admission, Track C counter recomputation, rigid ten-check release envelope, schema-2.2 semantic machinery, immutable supersession/closure graph, duplicate Tier 2 orchestration, live outage injection, automated screenshot validator, or release-summary product feature.

### Edge Cases

- The query contains only stop words or terms outside the selected scope; retrieval returns insufficient evidence rather than unrelated popular Documents.
- Equal lexical scores occur across Documents or sections; canonical stable ordering makes repeated results identical.
- A one-hop relationship would introduce a sixth Document, a cycle, an unauthorized endpoint, a stale version, or an unanchored section; the candidate is excluded and the packet remains valid.
- A Business Logic Document follows GENERATED_FROM to a Code Logic Document that was superseded after retrieval; currentness revalidation rejects the answer.
- A Cross-Repository Resolution decision exists without its exact current DocumentEdge, or the Edge direction disagrees with the decision; it cannot support expansion or a claim.
- A Flow Group is incomplete, unpublished, stale, or bound to a different Profiled Revision; it cannot support Chat evidence.
- A prior answer cites a handle absent from the newly built packet; the handle is not carried forward as evidence.
- The provider times out, is cancelled, emits malformed output twice, or returns a material claim with an unknown handle; no partial answer is rendered.
- A streamed UTF-8 sequence or Unicode grapheme spans multiple chunks; assembly preserves the complete character and never decodes a chunk as an independent final string.
- The provider emits empty keep-alive chunks but no answer data for ten seconds; the inactivity deadline ends the attempt even though transport activity may still exist.
- A chunk exceeds 16 KiB, the assembled response exceeds 64 KiB, the stream ends without one terminal outcome, emits duplicate or contradictory terminal outcomes, or emits non-empty data after terminal completion; the response is rejected without parsing or reveal.
- The provider disconnects after sending syntactically complete-looking JSON but before the required terminal outcome; the untrusted buffer is discarded.
- Cancellation races with a final chunk or terminal outcome; cancellation wins unless the complete answer already passed every acceptance check, and no partially accepted state survives.
- The 30-second budget leaves no time for syntax repair; the original invalid answer is rejected immediately.
- A SystemOwner loses access while a resume or status request is in flight; the result becomes missing-equivalent and discloses no protected state.
- Track C-MVP points at a database whose name does not match `vulgata_story125_*`, Story 12.4's qualification database, or the main development database; verification fails before product data is read or written.
- A screenshot contains fake Backup B state, secrets, absolute host paths, or a result from a different candidate; it is rejected from the release evidence set.

## Requirements *(mandatory)*

### Requirement Disposition Against Existing Prepared Chat

This classification describes the pinned authoring boundary at `ec3c68613ea6d9afdbebfb72bb3ef57395fd751e`, derived from source authority `0d835c5f65f70a3c6a9561c6ff600069bb93e832`. It is a planning input, not a claim that Story 12.5 acceptance has already been met.

| Disposition | Existing obligation or seam | Story 12.5 treatment |
| --- | --- | --- |
| Already implemented | Authenticated Chinese Chat with explicit Business/IT modes and selected System/two-Repository scope | Preserve and regression-test; broaden only as required by current release evidence, not by automatic scope discovery. |
| Already implemented | Missing-equivalent authorization checks before retrieval and again before rendering | Preserve for Documents and both endpoints of every relationship used. |
| Already implemented | Current Document, section, Source Anchor, relationship, and packet-fingerprint validation | Preserve; adapt relationship authority without weakening fail-closed behavior. |
| Already implemented | Opaque evidence handles, canonical packet fingerprint, and bounded cardinalities of up to five Documents, twenty sections, thirty-two Source Anchors, and twelve flow steps | Reuse as the packet foundation and verify the Story 12.5 relationship and conversation bounds. |
| Already implemented | Grounded material-claim validation and Document citations with Source Anchor line ranges | Preserve for both modes; no claim renders before whole-answer validation. |
| Already implemented | Lexical supplementary section selection using Chinese bigrams and ASCII identifiers | Reuse as evidence; make deterministic section ranking cover the current LLM-Wiki authority. |
| Already implemented | Candidate ranking, mode-aware presentation, provider failure handling, and currentness revalidation | Preserve and evaluate against the new corpus. |
| Already implemented | Existing Chat page answer, citation, waiting, cancellation, and scroll behavior | Reuse rather than rewrite, subject to Story 12.5 UI acceptance. |
| Partially implemented | Retrieval is bounded and database-backed but begins from a prepared exact profile and a required legacy exact-edge row rather than general current projections | Replace the starting authority in Track B after the dependency gate. |
| Partially implemented | Supplementary lexical matching exists, but complete deterministic ranking over Index Entries and Document Sections with mode-specific fallback is not yet proven | Complete and bound in Track B. |
| Partially implemented | REFERENCES, GENERATED_FROM, and flow steps enter packets, but relationship expansion is anchored to the prepared flow rather than general one-hop current relationship selection | Complete using accepted Story 12.4 v2 authority in Track B. |
| Partially implemented | Evidence packets are closed and fingerprinted, but bounded short multi-turn state and re-fetch boosting are not complete | Add only the bounded conversation behavior in Track B. |
| Partially implemented | Semantic validation fails closed, but the single time-aware syntax-repair allowance is not established for Chat | Complete without allowing semantic repair; any repair response uses the same streaming boundary. |
| Partially implemented | Chat synthesis has an overall timeout and bounded failure mapping, while provider response consumption is currently non-streaming | Replace provider response consumption with bounded server-side streaming assembly in Track B; reuse the established Pass 1/Pass 2 streaming patterns only as evidence, not as Chat authority. |
| Partially implemented | Durable Scan state, retry, pause, resume, warning, and failure domain/API seams exist, while the production management UI still relies on separate or incomplete presentation seams | Complete real persisted Chinese status presentation in Track A. |
| Partially implemented | Story 11.5 release harness, fixtures, and PostgreSQL tests provide reusable patterns, but target the stale August 15 prepared path | Reuse patterns only; create Story 12.5 evaluation and evidence artifacts in Track A/C. |
| Partially implemented | Existing tests cover Prepared Chat security, currentness, ranking, citations, and UI behavior | Retain as regression evidence and add v2 relationship, multi-turn, real-state UI, and tiered release coverage. |
| Obsolete | `specs/008-demo-chat-release-gate` lifecycle, approvals, workflow state, prepared-flow acceptance, and August 15 release identity | Never resume or cite as current lifecycle authority. |
| Obsolete | `ExactCrossRepositoryEdgeEvidence` plus embedded prepared profile/occurrence fingerprint as final Chat relationship authority | Final Track B must use accepted Story 12.4 v2 CrossRepositoryResolutionDecision and its exact current DocumentEdge. |
| Obsolete | Exactly one hard-coded prepared occurrence as the prerequisite for all retrieval | Use the selected authorized current release scope and accepted persisted relationship facts. |
| Obsolete | Python/CodeGraphContext/application-container assumptions from the August 15 release path | Follow the August 30 host runtime and PostgreSQL-only Docker-service boundary. |
| Obsolete | Fake Backup B Scan state as demonstration proof | It may remain isolated development behavior but is never release evidence. |
| Missing | Accepted-v2-aware retrieval and one-hop expansion over current DocumentEdges, GENERATED_FROM Edges, and published Flow Groups | Deliver in Track B only after the dependency gate. |
| Missing | Bounded two-question/two-answer conversation state with prior-citation boost and mandatory re-fetch/revalidation | Deliver in Track B. |
| Missing | Single time-aware syntax repair and explicit no-semantic-repair behavior | Deliver in Track B. |
| Missing | Real persisted production Scan progress, retry, pause/resume, warning, and failure UI | Deliver in Track A. |
| Missing | A source-traceable 12–20 question corpus and usefulness/citation/authorization/timing rubric | Prepare in Track A and execute finally in Track C. |
| Missing | Story 12.5 lightweight preflight, combined-run report, screenshots, restart proof, and candidate/revision traceability | Prepare structure in Track A; collect final evidence in Track C-MVP. |
| Missing | Final real-LLM host run over two real sample Repositories and 50–150 automatically selected File Code Units | Deliver only in Track C. |
| Missing | Observed 80% usefulness, 100% accepted technical-claim citation, zero authorization leakage, and under-30-second results | Establish only from Track C evidence. |

**Disposition totals**: 8 already implemented, 9 partially implemented, 5 obsolete, and 8 missing obligations or seams.

### Dependency and Delivery Gates

- **Track A — dependency-independent**: definition, production Scan-state UI, evaluation fixtures, question corpus, evidence templates, screenshot plan, release-run preparation, and characterization of the existing Unicode-safe post-validation reveal may proceed after the normal implementation-approval gate. Track A does not expose or simulate provider chunks.
- **Track B — dependency-sensitive**: provider streaming transport, bounded server-side assembly, retrieval authority replacement, current relationship expansion, Flow Group integration, and final relationship/currentness validation MUST NOT begin until Story 12.4 has an accepted final checkpoint and its final commit is integrated into this branch.
- **Track C-MVP — final verification**: one combined host integration and real-LLM release run, straightforward database queries, restart, screenshot capture, twelve-question execution, and Markdown release reporting MUST NOT begin until the Story 12.4 gate is satisfied and Track B is accepted on the integrated candidate. Track C-MVP reuses accepted Track B streaming evidence and does not add a second attestation/envelope product.
- The presence of candidate v2 contracts at this pinned boundary is read-only dependency evidence, not acceptance of Story 12.4.
- If integration requires changing an unaccepted Story 12.4 contract, work stops and the conflict returns to Story 12.4 authority rather than being redefined here.

### Functional Requirements

#### Scope, Authorization, and Modes

- **FR-001**: Story 12.5 MUST remain a bounded delta over the existing Prepared Chat implementation and MUST reuse its accepted production behavior and regression evidence where those conform to this specification.
- **FR-002**: Users MUST authenticate and explicitly select Business Mode or IT Mode, one authorized System, and the two release Repositories before asking a release-scope question.
- **FR-003**: Authorization MUST be checked before retrieval and again before rendering for the selected scope, every included Document, and both endpoints of every included relationship.
- **FR-004**: Unauthorized, revoked, missing, stale, or mismatched scope MUST be missing-equivalent and MUST disclose no protected identity, metadata, source, relationship, citation, or business detail.
- **FR-005**: Business Mode MUST prioritize understandable Chinese business-flow explanations and Business Logic Documents while retaining expandable citations to supporting current evidence.
- **FR-006**: IT Mode MUST prioritize current Code Logic Documents, Source Anchors, and REFERENCES Edges and MAY show authorized Repository-relative paths and line ranges directly.

#### Deterministic LLM-Wiki Retrieval

- **FR-007**: Retrieval MUST use only current authorized Index Entries, Document Sections, Documents, Source Anchors, DocumentEdges, Cross-Repository Resolution decisions, and Flow Groups from the selected scope.
- **FR-008**: Retrieval MUST extract relevant symbols, endpoints, Repository names, ASCII terms, CJK bigrams, and mode-specific business or technical terms from the question and bounded conversation state.
- **FR-009**: Retrieval MUST score candidates deterministically, with exact symbol, endpoint, path, title, and heading matches ranking above prose matches and stable canonical ordering resolving ties.
- **FR-010**: Retrieval MUST select at most five Documents and approximately twelve primary sections, never more than twenty sections total or four sections from one Document.
- **FR-011**: Selection MUST take the strongest eligible section from each selected Document before adding strong extra sections.
- **FR-012**: When primary matching is insufficient, retrieval MUST use a bounded fallback to current Business Logic summaries and advisory context terms; if useful evidence remains insufficient, it MUST refuse to synthesize.
- **FR-013**: Retrieval MUST NOT use embeddings, vector search, semantic reranking, cross-encoders, raw CodeGraph proximity, unrestricted Agent tool loops, or autonomous multi-hop retrieval.

#### Current Persisted Relationship Expansion

- **FR-014**: Track B MUST treat accepted Story 12.4 v2 CrossRepositoryResolutionDecision records and their exact current DocumentEdges as the canonical Cross-Repository relationship authority; legacy ExactCrossRepositoryEdgeEvidence MUST NOT authorize final expansion.
- **FR-015**: Expansion MAY use only current persisted REFERENCES Edges, GENERATED_FROM Edges, exact accepted Cross-Repository REFERENCES Edges, and complete published Flow Groups within the selected authorized System.
- **FR-016**: Expansion MUST begin from only the top two or three selected Documents, follow at most one relationship hop, add at most two related Documents per seed, and keep the entire packet within five Documents.
- **FR-017**: Expansion MUST remain within thirty-two relationships and twelve ordered flow steps, reject cycles and duplicates, and use stable canonical ordering at every bound.
- **FR-018**: Business Mode SHOULD prefer Business Logic Documents and MAY follow GENERATED_FROM Edges to supporting Code Logic Documents; IT Mode SHOULD begin with Code Logic Documents, Source Anchors, and REFERENCES Edges.
- **FR-019**: Every expanded Document, Edge, Cross-Repository Resolution decision, Flow Group, flow step, and Source Anchor MUST be revalidated for currentness, authorization, direction, System, Repository, Profiled Revision, and internal consistency before packet inclusion.
- **FR-020**: An incomplete or unpublished Flow Group, an unresolved or non-unique decision, a missing or directionally inconsistent DocumentEdge, and an unavailable required Source Anchor MUST NOT support expansion or an accepted claim.

#### Evidence Packet, Synthesis, Citations, and Conversation

- **FR-021**: Each answer attempt MUST use one closed evidence packet containing opaque handles for one to five Documents, one to twenty sections, one to thirty-two Source Anchors, at most thirty-two relationships, and at most twelve flow steps.
- **FR-022**: The packet MUST include only complete sections and complete relationship-required support; low-ranked sections MAY be dropped to meet the bound, but required evidence MUST NOT be partially truncated.
- **FR-023**: Evidence content MUST remain within 64 KiB, conversation state within 4 KiB, and user advisory context within 8 KiB, measured as encoded packet content.
- **FR-024**: A canonical SHA-256 fingerprint MUST bind the complete ordered packet used for synthesis and later claim validation.
- **FR-025**: The synthesis provider MUST receive only the bounded question, selected mode, packet fingerprint, opaque handles, and allowed packet content; it MUST NOT gain direct database, source Repository, or analysis-tool access.
- **FR-026**: Every material technical claim MUST cite one or more valid packet handles whose current sections and Source Anchors support that claim.
- **FR-027**: The whole answer and all claim-to-citation mappings MUST pass semantic evidence validation before any answer claim is rendered; semantic validation failure MUST reject the whole answer.
- **FR-028**: At most one syntax repair MAY occur, and only when the remaining answer-time budget permits; syntax repair MUST NOT add evidence or repair a semantic failure.
- **FR-029**: Conversation state MUST contain at most the last two user questions, last two validated answers, previously cited handles, selected scope, and mode.
- **FR-030**: A follow-up MAY give a small deterministic boost to previously cited current Documents, but every underlying Document, section, Source Anchor, and relationship MUST be re-fetched and revalidated; previous answer text MUST remain context, never evidence.
- **FR-031**: Provider failure, cancellation, timeout, invalid output, insufficient evidence, or expired answer budget MUST render a bounded Chinese outcome with no raw diagnostics, credentials, partial answer, unsupported claim, or stale result.
- **FR-032**: Every accepted Chat answer MUST complete within 30 seconds from accepted submission to complete rendered result.

#### Real Production Scan State UI

- **FR-033**: The authorized Chinese management UI MUST present real persisted Scan and job state for preparation/indexing, running, retry, paused, resumed, completed, completed with warnings, and integrity/security failure.
- **FR-034**: The UI MUST present completed/total counts, the next retry when scheduled, a bounded pause or failure reason, and a bounded failed-file summary where applicable.
- **FR-035**: A recoverable paused Scan MUST expose the `继续扫描` action only to an authorized SystemOwner and MUST reflect the durable manual-resume outcome.
- **FR-036**: Isolated fake Backup B state MAY remain available for explicit development-only use but MUST NOT share production persistence, represent a real Scan, or support release evidence.

#### Evaluation and Release Evidence

- **FR-037**: Track A MUST define a fixed 12–20 question corpus spanning Business/IT modes, cross-Repository flow, business rules, technical traces, failure conditions, follow-ups, insufficient evidence, and authorization-negative behavior.
- **FR-038**: Each corpus entry MUST record scope, mode, question, expected evidence themes, useful-answer rubric, citation expectation, authorization expectation, timing, and observed result without prescribing provider prose.
- **FR-039**: Release evidence MUST honestly distinguish completed, failed, blocked, and not-run work and MUST preserve the candidate commit, exact Repository Profiled Revisions, tool/provider versions, non-secret provider configuration identity, database ownership, redacted commands and exit results, query results, durations, failures, and limitations in a human-readable Markdown report.
- **FR-040**: Tier 1 MUST verify closed contracts, deterministic selection, streaming chunk assembly and failure handling, validated-answer reveal, relationship and packet bounds, authorization, currentness, citations, conversation behavior, UI state mapping, and evaluation fixtures without a network LLM.
- **FR-041**: Track C-MVP MUST use one clean disposable PostgreSQL database named `vulgata_story125_*` for the combined host integration and real release run.
- **FR-042**: The combined run MUST use host-run Vulgata, PostgreSQL 17 as the only required Docker service, two real Java/XML sample Repositories, real CodeGraph, JDT LS Java PreScan/navigation, safe catalog-driven XML parsing, configured LLM, and at least 50 automatically selected File Code Units.
- **FR-043**: Straightforward database queries MUST prove at least one exact Cross-Repository REFERENCES Edge, at least one honest Uncertainty, and at least three Business Logic Documents including one Cross-Repository flow, with real source-anchored Pass 1 and Pass 2 output.
- **FR-044**: When the provider is available, Track C-MVP MUST execute twelve representative questions and manually record at least 80% useful answers, 100% citation coverage for accepted material technical claims, zero unauthorized evidence, mandatory streaming, and every accepted answer under 30 seconds.
- **FR-045**: Track C-MVP MUST restart the Vulgata application process once against the same disposable database and prove authenticated persisted wiki and grounded Chat access without duplicate publication or data loss.
- **FR-046**: Final evidence MUST include manually captured and redacted real screenshots of Scan, grounded Business Chat, grounded IT Chat with Source Anchors, a Cross-Repository result, and persisted access after restart; secrets, absolute host paths, fake state, and unrelated candidate output are forbidden.
- **FR-047**: Track C-MVP MUST never use Story 12.4's qualification database or the main development database; an invalid database target MUST fail verification before product data is read or written.

#### Dependency, Ownership, and Change Boundaries

- **FR-048**: Track A production work MAY begin only after mandatory implementation approval; Track B and Track C production work MUST additionally satisfy the Story 12.4 accepted-final-checkpoint and integrated-final-commit gate.
- **FR-049**: Story 12.5 MUST NOT alter an unaccepted Story 12.4 contract; any required change MUST be escalated to the Story 12.4 authority before Story 12.5 proceeds.
- **FR-050**: The feature SHOULD require no new persistence migration and MUST NOT change VulgataDbContext, migrations, or VulgataDbContextModelSnapshot unless an accepted plan proves the change unavoidable and ownership is explicitly coordinated.
- **FR-051**: Program.cs, dependency registrations, project files, and central endpoint registration MUST be treated as shared integration files with explicit ownership coordination.
- **FR-052**: Story 12.5 MUST NOT modify, clean, stage, or depend on uncommitted files, temporary directories, databases, or Git state in the separate Story 12.4 worktree.

#### Streaming Transport and Validated Reveal

- **FR-053**: Every primary synthesis response and the optional syntax-repair response MUST use provider-side streaming transport; a non-streaming provider response MUST NOT be accepted as the Story 12.5 release path.
- **FR-054**: Provider chunks MUST be treated as untrusted transport data and assembled only on the server. Each chunk MUST be no larger than 16 KiB and the complete assembled response MUST be no larger than 64 KiB, measured as received bytes.
- **FR-055**: Streaming assembly MUST preserve UTF-8 sequences split across chunks and MUST reject malformed, overlong, truncated, or otherwise invalid final UTF-8 without substituting replacement characters.
- **FR-056**: One shared answer budget MUST cover retrieval, primary streaming, optional single syntax repair, validation, and completion of the accepted result. The overall budget MUST remain under 30 seconds, and ten seconds without a provider data chunk or valid terminal outcome MUST end the current stream for inactivity.
- **FR-057**: User cancellation MUST cancel provider streaming and assembly. Provider disconnect, inactivity, overall timeout, cancellation, chunk overflow, total overflow, malformed UTF-8, missing terminal outcome, duplicate or contradictory terminal outcome, and non-empty data after terminal completion MUST discard the untrusted buffer and produce no partial Grounded Answer.
- **FR-058**: No provider chunk, assembled fragment, partial claim, citation, evidence handle, relationship, or source detail MAY render before the complete response passes syntax parsing, packet-fingerprint binding, authorization, currentness, citation, and semantic evidence validation.
- **FR-059**: After complete acceptance, the UI MAY progressively reveal only the validated Grounded Answer using Unicode-safe user-visible text boundaries; reduced-motion preference MAY reveal the full accepted answer immediately. This reveal is presentation of accepted content, not the provider stream.
- **FR-060**: Streaming evidence MUST record request identity, stream start and terminal times, time to first data chunk, chunk count, received byte count, inactivity/overall deadline outcome, cancellation or rejection category, syntax-repair use, validation outcome, and total accepted-answer duration without recording provider chunk content, secrets, or unauthorized evidence.

### Key Entities

- **Chat Scope**: The selected mode, authorized System, and two authorized release Repositories that bound one question and its follow-ups.
- **Evidence Packet**: The closed, canonically ordered, fingerprinted set of current Documents, sections, Source Anchors, relationships, flow steps, bounded advisory context, and bounded conversation state available for one answer.
- **Grounded Answer**: A mode-appropriate Chinese answer for which the complete set of material technical claims and citations passed packet, currentness, and authorization validation before rendering.
- **Provider Stream**: The bounded sequence of untrusted response chunks and exactly one valid terminal outcome received for primary synthesis or the optional syntax repair; it is assembled server-side and is never itself user-visible evidence.
- **Material Technical Claim**: A factual statement about code, a relationship, a decision, or a business flow that requires supporting packet evidence.
- **Document Citation**: A user-visible mapping from one or more accepted claims to a current Document and authorized Source Anchor detail.
- **Conversation State**: The bounded transient context from at most two prior questions and validated answers, previously cited handles, selected scope, and mode; it is never source evidence.
- **Chat Evaluation Case**: One fixed question, scope, mode, expected evidence themes, scoring expectations, timing, and observed result in the 12–20 question corpus.
- **Release Evidence Set**: The candidate-bound Markdown report, lightweight preflight, command/exit log, database query results, manual evaluation table, redacted screenshots, restart proof, failures, and limitations from the combined Track C-MVP run.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For 100% of accepted answers, all included Documents, Source Anchors, relationships, and Flow Groups are current, authorized, and inside the selected System and two-Repositories scope at render time.
- **SC-002**: Repeating the same question, scope, mode, conversation state, and persisted evidence produces the same ordered Document, section, relationship, and flow-step selection in 100% of deterministic retrieval checks.
- **SC-003**: Every evidence packet contains 1–5 Documents, 1–20 sections, 1–32 Source Anchors, no more than 32 relationships, no more than 12 flow steps, and no relationship path deeper than one hop.
- **SC-004**: 100% of accepted material technical claims map to valid supporting packet evidence and Source Anchors; zero unsupported or partially validated claims are rendered.
- **SC-005**: Across unauthorized, revoked, stale, superseded, malformed, incomplete, mismatched-direction, and over-bound cases, zero protected facts and zero partial answers are rendered.
- **SC-006**: A user can ask an initial question and two follow-ups in one uninterrupted flow, while every follow-up re-fetches and revalidates its evidence and uses prior answers only as context.
- **SC-007**: An authorized SystemOwner can identify every required real Scan state, inspect counts/retry/warning detail, and resume an eligible paused Scan from the Chinese UI; zero fake Backup B states appear in accepted release evidence.
- **SC-008**: The release corpus contains 12–20 reviewable questions with complete scope, mode, evidence-theme, usefulness, citation, authorization, timing, and observed-result fields.
- **SC-009**: When the provider is available, the twelve-question Track C-MVP table yields at least 80% useful answers, 100% citation coverage for accepted material technical claims, zero unauthorized evidence disclosures, and 100% of accepted answers in under 30 seconds.
- **SC-010**: The final Scan covers 50–150 automatically selected File Code Units across exactly two real sample Repositories, with every high-value seed successful and ordinary failures within the accepted budget.
- **SC-011**: The final candidate contains at least one exact Cross-Repository REFERENCES Edge, one honest Uncertainty, and three Business Logic Documents including one Cross-Repository flow.
- **SC-012**: One application-process restart against the same disposable release database preserves authenticated wiki and grounded Chat access with zero duplicate published Documents caused by restart.
- **SC-013**: Every final release claim is supported in one human-readable candidate-bound report by recorded Profiled Revisions, versions, commands/exits, query results, manual evaluation rows, screenshots, restart evidence, failures, or limitations.
- **SC-014**: Zero Track C-MVP attempts read from or write to Story 12.4's qualification database or the main development database; every accepted attempt uses a uniquely named `vulgata_story125_*` database.
- **SC-015**: In 100% of accepted Chat attempts, the primary provider response and any syntax-repair response use streaming transport, stay within the 16 KiB chunk and 64 KiB total bounds, and produce exactly one valid terminal outcome.
- **SC-016**: Across streamed partial JSON, split UTF-8, disconnect, ten-second inactivity, cancellation, overflow, malformed UTF-8, missing terminal, duplicate or contradictory terminal, and post-terminal-data cases, zero provider fragments, claims, citations, handles, relationships, or source details render before complete validation.
- **SC-017**: For 100% of accepted streamed answers, all retrieval, streaming, optional repair, validation, and accepted-result completion fit within one under-30-second budget, and any progressive UI reveal contains only Unicode-safe units from the already validated Grounded Answer.

## Assumptions

- Story 12.4's pinned candidate contracts and implementation seams are dependency evidence only; its accepted final checkpoint and final commit are not present at specification time.
- The current Prepared Chat implementation and tests are reusable evidence, but `specs/008-demo-chat-release-gate` is stale lifecycle history and grants no approval or completion status.
- The accepted Story 12.4 final commit will expose current CrossRepositoryResolutionDecision, exact DocumentEdge, complete GENERATED_FROM, and Flow Group publication authority without requiring Story 12.5 to redefine those contracts.
- Existing authentication, Repository authorization, LLM provider configuration, immutable Document storage, Source Anchors, and durable Scan recovery remain available and retain their existing security boundaries.
- Conversation state is transient and bounded; saved Chat sessions, uploads, long-term memory, automatic scope determination, and broad post-August V1 Chat tools remain out of scope.
- The provider supports a streaming response for the configured Chat model. Existing bounded streaming transports from Prepared Pass 1 and Pass 2 are reusable implementation evidence, while Story 12.5 defines the independent Chat acceptance boundary.
- No new persistence migration is expected. Shared integration files will be coordinated only during an accepted implementation plan.

## Source Traceability

- `docs/bmad/epics.md` — canonical Story 12.5 user story and acceptance criteria.
- `docs/bmad/implementation-artifacts/12-5-grounded-llm-wiki-chat-and-august-30-release-evidence.md` — fresh activation, parallel split, dependency gates, and ownership boundary.
- `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md` — August 30 scope, tiered verification, UI states, release metrics, and explicit deferrals.
- `docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md` — controlling retrieval, expansion, packet, conversation, runtime, and release bounds.
- `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md` — dated August 30 overlay, Business/IT value, authorization, citations, and NFR-1.2.
- `docs/UBIQUITOUS_LANGUAGE.md` — canonical domain terms.
- `.specify/memory/constitution.md` and `AGENTS.md` — security, ownership, UI, validation, and project-governance constraints.
- `specs/013-exact-cross-repository-flow-pass2/` — read-only candidate v2 dependency evidence; not accepted Story 12.4 completion authority.
- `specs/008-demo-chat-release-gate/` and current Prepared Chat production/tests — stale specification history and reusable implementation evidence only.

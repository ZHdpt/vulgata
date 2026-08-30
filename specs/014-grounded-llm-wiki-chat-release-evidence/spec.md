# Feature Specification: Grounded LLM-Wiki Chat & Release Evidence

**Feature Branch**: `demo0831/story-12-5-trackC`

**Created**: 2026-08-27

**Status**: Draft

**Input**: Approved Story 12.5 Track C-MVP correction at `1517d8c85eed643a092fbb1c997f232c90e6b52c`, applied over the accepted Track B checkpoint `f292caec427f9844be3c0f65b4cb00a9292080a5`; the bounded in-place Prepared Scan initiation correction `story-12-5-correct-course-prepared-scan-initiation-01` accepted at authority checkpoint `34c6cb8e3d2ea0858cf8a2711aa17d46526df1b9`; and the approved release-blocking exact-profile seed-selection correction `story-12-5-run-03-all-remaining-tasks-approval-02`. Run-03 remains immutable diagnostic evidence: it proved the initiation/replay path and exposed that generic bounded selection retained zero high-value seeds and excluded three of four reviewed required sources. The current correction preserves accepted Track A/Track B behavior and the prior Prepared Scan initiation scope, adds only the server-owned matched-profile high-value-seed precondition, and requires a fresh guarded release database after its technical gates pass. `specs/008-demo-chat-release-gate` remains implementation history only, never lifecycle authority.

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

**Independent Test**: After Story 12.4's accepted final commit is integrated, Track B is accepted, and the exact-profile seed precondition passes its technical gates, execute Tier 1 regression plus one combined host integration and fresh real release run against one pinned candidate and one new isolated disposable database; verify the retained outputs, restart, manual evaluation, screenshots, and security boundaries without mutating or reusing run-03.

**Acceptance Scenarios**:

1. **Given** a Tier 1 checkpoint, **When** focused verification runs, **Then** retrieval, relationship expansion, streaming assembly and failure handling, post-validation reveal, packet validation, citations, conversation bounds, authorization, currentness, UI states, evaluation fixtures, and deterministic behavior are covered without a network LLM.
2. **Given** Track C-MVP begins, **When** preflight completes, **Then** it records the candidate commit, exact Repository Profiled Revisions, tool and provider versions, non-secret provider configuration identity, unique lowercase `vulgata_story125_*` database ownership, and redacted commands with exit results before product data access.
3. **Given** the combined host integration and real release run, **When** final verification runs, **Then** host-run Vulgata uses PostgreSQL 17 as the only required Docker service, two real Java/XML Repositories bound to their exact Profiled Revisions, real CodeGraph, JDT LS Java navigation, safe catalog-driven XML parsing, a configured real LLM, and at least 50 automatically selected File Code Units.
4. **Given** the real Scan completes, **When** straightforward database queries inspect release outcomes, **Then** at least one exact Cross-Repository REFERENCES Edge, one honest Uncertainty, and three Business Logic Documents including one Cross-Repository flow exist, with source-anchored Pass 1 and Pass 2 output bound to the exact Profiled Revisions.
5. **Given** the twelve representative questions are executed while the provider is available, **When** results are manually scored, **Then** at least 80% are useful, 100% of accepted material technical claims are cited, zero unauthorized evidence is disclosed, mandatory streaming remains in use, and every accepted answer completes in under 30 seconds.
6. **Given** the application process restarts against the same release database, **When** the authenticated wiki and Chat are reopened, **Then** persisted Documents and grounded Chat remain accessible without duplicate publication or data loss.
7. **Given** final evidence is assembled, **When** release claims are reviewed, **Then** one human-readable Markdown report contains the preflight, commands and exits, query results, manual evaluation table, restart proof, failures, limitations, and at least one manually captured and redacted real screenshot in each of five categories: Scan, grounded Business Chat, grounded IT Chat with Source Anchors, an exact Cross-Repository result, and persisted wiki/Chat access after restart.
8. **Given** Track C-MVP is implemented, **When** scope is reviewed, **Then** it adds no ECDSA attestation sink, private/public key lifecycle, sealed raw-adapter admission, Track C counter recomputation, rigid ten-check release envelope, schema-2.2 semantic machinery, immutable supersession/closure graph, duplicate Tier 2 orchestration, live outage injection, automated screenshot validator, or release-summary product feature.
9. **Given** a required real tool, configured provider, credential, network route, or host prerequisite is unavailable, **When** Track C-MVP preflight or execution reaches that dependency, **Then** the report records a sanitized blocked disposition and the missing prerequisite without claiming the corresponding run, question results, screenshots, or Story completion.
10. **Given** the exact release pair matches the embedded reviewed exact-resolution profile, **When** Prepared Scan selection runs, **Then** every safe eligible source required for its consumer, provider declaration, provider implementation, and unsupported observation is retained as a mandatory high-value seed; a missing, unsafe, filtered, ambiguous, or out-of-bound required source blocks Manifest publication.

---

### User Story 6 - Initiate the Exact Prepared Scan from Ordinary Records (Priority: P1, Track C Prerequisite)

As an authenticated authorized user, I can start the bounded two-Repository Prepared Scan from one ordinary System, exactly two ordinary Repositories, and their exact requested revisions, so that a clean production database can reach the accepted Prepared Scan pipeline without seeded lifecycle records or a retired profiling path.

**Why this priority**: The real release run cannot begin from ordinary production records today. The existing preparation pipeline is accepted, but its durable entrance must create one complete, replayable pair before external analysis can safely begin.

**Independent Test**: Through default production composition, begin with only an authenticated authorized user, one ordinary System, exactly two ordinary Repository records, exact Git-ref/Profiled-Revision bindings, and a ready accepted CodeGraph 1.5/JDT-LS runtime. Do not pre-seed a Scan Run, Pre-Scan Result, Scan Execution, Two-Repository coordination, Prepared Scan Manifest, File Code Unit, or Document.

**Acceptance Scenarios**:

1. **Given** the independent-test starting state, **When** the user submits the authenticated Prepared Scan request, **Then** the production entrance creates or replays exactly two revision-bound Scan Runs, two new-path Scan Executions, and one canonical Preparing Two-Repository coordination as one complete durable initiation tuple.
2. **Given** that tuple is accepted, **When** initiation completes, **Then** the existing Prepared Scan pipeline receives the exact tuple and publishes the pair's Prepared Scan Manifests through its established authorization and atomic-publication behavior.
3. **Given** the same authorized System, canonical Repository pair, Git refs, Profiled Revisions, Analysis Runtime, prompt, and Document schema identities, **When** the request is repeated, **Then** it returns the same durable tuple and does not create duplicates.
4. **Given** an existing initiation tuple, **When** a request changes a Repository, Profiled Revision, identity binding, or pair membership, **Then** it fails closed without attaching to or changing the existing tuple.
5. **Given** a user lacks authorization for the System or either Repository, **When** initiation is requested, **Then** it creates no initiation record and reveals no protected Repository or revision detail.
6. **Given** the initiation tuple commits but preparation later fails, **When** the exact authorized request is retried, **Then** it reuses the same tuple and can safely resume or replay preparation without partial or cross-paired durable state.
7. **Given** identical requests race, **When** initiation settles, **Then** every successful caller observes one canonical tuple and no partially visible, duplicate, or cross-paired tuple exists.
8. **Given** default production composition, **When** the pair is prepared, **Then** the accepted CodeGraph 1.5, JDT-LS, and safe XML path is used and neither CodeGraphContext nor the historical profiling entrance resolves or executes.
9. **Given** initiation and preparation are in progress, **When** their effects are inspected, **Then** initiation performs no Git or analysis work inside its durable atomic boundary, and neither initiation nor preparation starts a Worker, Pass 1, Cross-Repository Resolution, Pass 2, Document, Edge, Uncertainty, Chat, or LLM action owned by a later stage.

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
- Track C-MVP points at a database whose name does not match `vulgata_story125_*`, or targets Story 12.4's qualification database or the main development database; verification fails before product data is read or written.
- A screenshot contains fake Backup B state, secrets, absolute host paths, or a result from a different candidate; it is rejected from the release evidence set.
- A real LLM or analysis-tool prerequisite is unavailable; the attempt is reported as blocked with sanitized diagnostics and no release-success claim rather than being replaced by fake provider state, a duplicate harness, or fabricated evidence.
- Two identical Prepared Scan initiation requests race before either observes durable state; both settle on one canonical pair and neither can expose a half-created or cross-paired tuple.
- A retry changes only one Repository, one Profiled Revision, one Git ref, the Analysis Runtime identity, prompt identity, Document schema identity, or authorization scope; it conflicts or fails closed rather than attaching to the earlier tuple.
- Prepared Scan initiation commits and source acquisition or later preparation fails; the exact authorized retry reuses the committed tuple and does not create replacement Scan Runs or a second coordination.
- A historical v1 profiling result exists for either Repository; it cannot satisfy, replace, or be attached to the new-path Prepared Scan handoff.
- Authorization changes after initiation but before Prepared Scan Manifest publication; the established publication recheck withholds the prepared result without disclosing protected state.
- The pair matches the reviewed exact-resolution profile but one required path is absent, filtered, unsafe, maps to multiple Repository candidates, or would violate existing seed/split bounds; preparation fails closed before either Manifest publishes.
- A nonmatching Repository pair or revision is prepared; it receives no reviewed-profile seed promotion and retains the generic deterministic selection behavior.

## Requirements *(mandatory)*

### Requirement Disposition at the Current Accepted Boundary

This classification describes the current pinned boundary: Story 12.4 is an accepted immutable dependency at `4798b97d9f23aef587829175df7ab7ebc3daf995`; Track A and Track B are accepted in checkpoint `f292caec427f9844be3c0f65b4cb00a9292080a5`; the approved Track C-MVP correction is bound by `1517d8c85eed643a092fbb1c997f232c90e6b52c`; the Prepared Scan initiation scope is accepted at `34c6cb8e3d2ea0858cf8a2711aa17d46526df1b9`; and the exact-profile seed-selection correction is approved under `story-12-5-run-03-all-remaining-tasks-approval-02`. Accepted source and tests remain evidence and MUST NOT be reimplemented merely to produce Track C proof.

| Disposition | Obligation or evidence | Story 12.5 treatment |
| --- | --- | --- |
| Accepted Track A | Real persisted Chinese Scan-state presentation, authorized manual resume, the fixed 12–20 question corpus and scoring rules, release preparation, and Unicode-safe post-validation reveal characterization | Preserve the accepted behavior and tests unchanged; Track C observes the real product and uses the prepared corpus. |
| Accepted Track B | Deterministic current LLM-Wiki retrieval, accepted Story 12.4 relationship authority, one-hop expansion, bounded Evidence Packets and Conversation State, authorization/currentness/citation validation, and grounded Business/IT presentation | Preserve the accepted behavior and tests unchanged; Track C does not create a parallel retrieval or validation path. |
| Accepted Track B | Mandatory provider streaming, bounded server-side assembly, strict UTF-8 handling, cancellation/deadline/terminal fail-closed behavior, whole-answer validation, sole accepted-result publication, and zero pre-validation reveal | Preserve the accepted behavior and tests unchanged; Track C may cite accepted observations and exercise the production path but does not recompute transport byte domains or counters. |
| Required Track C prerequisite | One authenticated production entrance from an ordinary System, exactly two ordinary Repositories, and exact Git-ref/Profiled-Revision bindings into the existing Prepared Scan pipeline | Preserve the accepted atomic, idempotent initiation boundary and add only the matched reviewed-profile high-value-seed precondition required for the accepted exact-resolution stage. |
| Required Track C-MVP | One combined host integration and real release run over two real Java/XML Repositories and their exact Profiled Revisions, with real CodeGraph, JDT LS, safe XML parsing, configured real LLM, and at least 50 automatically selected File Code Units | Execute the corrected production path against one clean `vulgata_story125_*` database; do not create a duplicate Scan or Chat harness. |
| Required Track C-MVP | Straightforward queries for source-anchored Pass 1/Pass 2 output, one exact Cross-Repository REFERENCES Edge, one honest Uncertainty, and three Business Logic Documents including a Cross-Repository flow | Record the observed results in one human-readable report without a second release-proof data model. |
| Required Track C-MVP | Twelve representative questions when the provider is available, same-database application-process restart, and five manually captured redacted real screenshot categories | Record manual results, persisted access, failures, and limitations; report unavailable prerequisites as blocked rather than fabricating evidence. |
| Deferred post-competition | Cryptographic attestation/key lifecycle, sealed raw-adapter admission, Track C counter recomputation, rigid ten-check release envelope, schema-2.2 proof machinery, immutable supersession/closure graphs, exhaustive hashing/cross-linking, duplicate controlled-provider orchestration, live outage injection, automated screenshot binding, and a release-summary product feature | Excluded from current acceptance and MUST NOT be restored by planning or implementation. |
| Invalidated downstream evidence | The existing Track C release contract, release schema, proof-oriented harness obligations, plan, and tasks that encode the deferred mechanisms | Read-only historical evidence until regenerated by their owning downstream stages; they do not define this specification's acceptance boundary. |
| Forbidden change | EF migrations, `VulgataDbContext`, `VulgataDbContextModelSnapshot`, Story 12.4 contracts or implementation, and Scan behavior outside the accepted initiation and reviewed-profile seed exceptions | No general Scan redesign, caller-supplied profile/path, or nonmatching-pair selection change is permitted. |

### Dependency and Delivery Gates

- **Track A — accepted and preserved**: its production Scan-state UI, evaluation fixtures and corpus, release preparation, and post-validation reveal evidence are accepted ancestry of `f292caec427f9844be3c0f65b4cb00a9292080a5`; this correction authorizes no Track A rewrite.
- **Track B — accepted and preserved**: its grounded retrieval, relationship expansion, mandatory streaming, fail-closed validation, publication, authorization, currentness, Source Anchor, citation, and Conversation State behavior and tests are accepted at `f292caec427f9844be3c0f65b4cb00a9292080a5`; this correction authorizes no Track B rewrite.
- **Track C prerequisite — accepted Prepared Scan entrance**: preserve the atomic, idempotent exactly-two-Repository initiation and replay boundary accepted at `34c6cb8e3d2ea0858cf8a2711aa17d46526df1b9`. The exact-profile correction MUST NOT reopen its tuple identity, authorization, transaction, external-work, legacy-path, or later-stage ownership boundaries.
- **Track C prerequisite — approved exact-profile seed precondition**: the approved `story-12-5-run-03-all-remaining-tasks-approval-02` correction changes only matched reviewed-profile seed retention. It remains subject to schema-v3 independent plan review, task/definition review, implementation decision, TDD, convergence, quality, E2E, and final verification gates; the standing approval does not waive those technical gates or any scope/security boundary.
- **Track C-MVP — fresh corrected final verification**: run-03 remains read-only diagnostic evidence and MUST NOT be promoted, repaired, or reused as the passed release lane. After the corrected definition and implementation pass their technical gates, one fresh guarded database and complete combined run are required under the authority at `1517d8c85eed643a092fbb1c997f232c90e6b52c`.
- Story 12.4 is already accepted and integrated at `4798b97d9f23aef587829175df7ab7ebc3daf995`; Track C-MVP MUST consume it unchanged.
- Existing proof-oriented Track C contracts, schemas, plan, and tasks are invalidated downstream evidence only and MUST NOT be treated as current authority before their owning stages regenerate them.

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
- **FR-039**: Release evidence MUST honestly distinguish completed, failed, blocked, and not-run work and MUST preserve the candidate commit, exact Repository Profiled Revisions, tool/provider versions, non-secret provider configuration identity, database ownership, redacted commands and exit results, query results, durations, failures, and limitations in one human-readable Markdown report.
- **FR-040**: Tier 1 MUST verify closed contracts, deterministic selection, streaming chunk assembly and failure handling, validated-answer reveal, relationship and packet bounds, authorization, currentness, citations, conversation behavior, UI state mapping, and evaluation fixtures without a network LLM.
- **FR-041**: Track C-MVP MUST use one clean disposable PostgreSQL database named `vulgata_story125_*` for the combined host integration and real release run.
- **FR-042**: The combined run MUST use host-run Vulgata, PostgreSQL 17 as the only required Docker service, two real Java/XML Repositories bound to their exact Profiled Revisions, real CodeGraph, JDT LS Java PreScan/navigation, safe catalog-driven XML parsing, a configured real LLM, and at least 50 automatically selected File Code Units.
- **FR-043**: Straightforward database queries MUST prove at least one exact Cross-Repository REFERENCES Edge, at least one honest Uncertainty, and at least three Business Logic Documents including one Cross-Repository flow, with real source-anchored Pass 1 and Pass 2 output bound to the exact Profiled Revisions.
- **FR-044**: When the configured real LLM is available, Track C-MVP MUST execute twelve representative questions and manually record at least 80% useful answers, 100% citation coverage for accepted material technical claims, zero unauthorized evidence disclosures, mandatory streaming, and every accepted answer under 30 seconds.
- **FR-045**: Track C-MVP MUST restart the Vulgata application process once against the same disposable database and prove authenticated persisted wiki and grounded Chat access without duplicate publication or data loss.
- **FR-046**: Final evidence MUST include at least one manually captured and redacted real screenshot in each of five categories: Scan, grounded Business Chat, grounded IT Chat with Source Anchors, an exact Cross-Repository result, and persisted wiki/Chat access after restart; secrets, absolute host paths, fake state, and unrelated candidate output are forbidden.
- **FR-047**: Track C-MVP MUST never use Story 12.4's qualification database or the main development database; an invalid database target MUST fail verification before product data is read or written.

#### Dependency, Ownership, and Change Boundaries

- **FR-048**: Track A and Track B behavior and tests accepted at `f292caec427f9844be3c0f65b4cb00a9292080a5` MUST remain unchanged by the Track C-MVP correction; Track C execution MUST still pass every downstream schema-v3 authorization and verification gate.
- **FR-049**: Track C-MVP MUST NOT alter Story 12.4 contracts or implementation and MUST NOT generally add or materially change Scan parsing, filtering, scheduling, grouping, retry, dispatch, or other analysis behavior. The only Scan-path exceptions are the bounded exactly-two-Repository initiation boundary in FR-063 through FR-078 and the matched reviewed-profile seed precondition in FR-079 through FR-086.
- **FR-050**: Track C-MVP MUST NOT add or change EF migrations, `VulgataDbContext`, or `VulgataDbContextModelSnapshot`.
- **FR-051**: Track C-MVP support MUST be limited to the bounded production Prepared Scan initiation exception in FR-063 through FR-078, the reviewed-profile seed precondition in FR-079 through FR-086, and the database guard, lightweight preflight, execution-command recording, straightforward output queries, manual evidence capture, and Markdown reporting necessary to execute and observe the existing product path; it MUST NOT recreate Scan or Chat orchestration in a duplicate harness.
- **FR-052**: Story 12.5 MUST NOT modify, clean, stage, or depend on uncommitted files, temporary directories, databases, or Git state in a separate worktree.

#### Streaming Transport and Validated Reveal

- **FR-053**: Every primary synthesis response and the optional syntax-repair response MUST use provider-side streaming transport; a non-streaming provider response MUST NOT be accepted as the Story 12.5 release path.
- **FR-054**: Provider chunks MUST be treated as untrusted transport data and assembled only on the server. Each chunk MUST be no larger than 16 KiB and the complete assembled response MUST be no larger than 64 KiB, measured as received bytes.
- **FR-055**: Streaming assembly MUST preserve UTF-8 sequences split across chunks and MUST reject malformed, overlong, truncated, or otherwise invalid final UTF-8 without substituting replacement characters.
- **FR-056**: One shared answer budget MUST cover retrieval, primary streaming, optional single syntax repair, validation, and completion of the accepted result. The overall budget MUST remain under 30 seconds, and ten seconds without a provider data chunk or valid terminal outcome MUST end the current stream for inactivity.
- **FR-057**: User cancellation MUST cancel provider streaming and assembly. Provider disconnect, inactivity, overall timeout, cancellation, chunk overflow, total overflow, malformed UTF-8, missing terminal outcome, duplicate or contradictory terminal outcome, and non-empty data after terminal completion MUST discard the untrusted buffer and produce no partial Grounded Answer.
- **FR-058**: No provider chunk, assembled fragment, partial claim, citation, evidence handle, relationship, or source detail MAY render before the complete response passes syntax parsing, packet-fingerprint binding, authorization, currentness, citation, and semantic evidence validation.
- **FR-059**: After complete acceptance, the UI MAY progressively reveal only the validated Grounded Answer using Unicode-safe user-visible text boundaries; reduced-motion preference MAY reveal the full accepted answer immediately. This reveal is presentation of accepted content, not the provider stream.
- **FR-060**: Accepted Track B streaming evidence MUST record request identity, stream start and terminal times, time to first data chunk, chunk count, received byte count, inactivity/overall deadline outcome, cancellation or rejection category, syntax-repair use, validation outcome, and total accepted-answer duration without recording provider chunk content, secrets, or unauthorized evidence. Track C-MVP MAY quote that accepted evidence and MUST NOT recompute raw-body, SSE, provider-chunk, decoded-answer, or final-answer counter domains.
- **FR-061**: Track C-MVP MUST NOT add cryptographic attestation or key lifecycle, sealed raw-adapter admission, a rigid ten-check release envelope, schema-2.2 release-proof machinery, immutable run supersession or closure graphs, exhaustive artifact hashing/cross-linking, separate controlled-provider orchestration, live outage injection, automated screenshot binding, or a release-summary product feature.
- **FR-062**: If a required real tool, configured provider, credential, network route, or host prerequisite is unavailable, Track C-MVP MUST record a sanitized blocked disposition and MUST NOT substitute fake provider state, fabricated evidence, or a completion claim for the unavailable work.

#### Bounded Prepared Scan Initiation

- **FR-063**: The authenticated Prepared Scan request MUST identify exactly one authorized System, exactly two distinct authorized Repositories belonging to that System, a safe full Git ref for each Repository, and the exact expected lowercase Profiled Revision for each ref.
- **FR-064**: System and Repository visibility, ownership, and request authorization MUST be validated before any initiation state is created; the established authorization recheck immediately before Prepared Scan Manifest publication MUST remain in force.
- **FR-065**: One atomic durable boundary MUST create or replay the complete initiation tuple: exactly two Profiled-Revision-bound Scan Runs, exactly two new-path Scan Executions, and exactly one canonical Preparing Two-Repository coordination. The complete tuple or none of it MUST be visible.
- **FR-066**: Each new-path Scan Execution MUST retain the accepted current Analysis Runtime, prompt, Document schema, Repository, and Profiled Revision identities, and the coordination MUST bind the canonical pair without permitting a cross-paired execution.
- **FR-067**: The initiation atomic boundary MUST close before source acquisition or any Git, CodeGraph, JDT-LS, XML, or other long-running external analysis begins.
- **FR-068**: After initiation succeeds, the existing Prepared Scan behavior MUST receive the exact tuple and retain ownership of source acquisition, CodeGraph 1.5, JDT-LS, safe XML processing, deterministic selection including FR-079 through FR-086, atomic Prepared Scan Manifest publication, final authorization recheck, and the existing prepared handoff/response.
- **FR-069**: Repeating an identical authorized request with the same System, canonical Repository pair, Git refs, Profiled Revisions, Analysis Runtime, prompt, Document schema, and authorization scope MUST return the same initiation tuple and MUST NOT create duplicate Scan Runs, Scan Executions, or coordination.
- **FR-070**: A request that differs in System, Repository pair or membership, Git ref, Profiled Revision, Analysis Runtime identity, prompt identity, Document schema identity, or authorization scope MUST conflict or fail closed and MUST NOT attach to, mutate, or disclose the existing tuple.
- **FR-071**: Concurrent identical requests MUST settle on one canonical initiation tuple; no caller or observer MAY see a partial, duplicate, or cross-paired tuple.
- **FR-072**: If initiation commits and later preparation fails, an exact authorized retry MUST reuse the same tuple and safely resume or replay the accepted Prepared Scan behavior without replacement initiation state.
- **FR-073**: A historical v1 profiling result, legacy Pre-Scan result, or legacy profiling entrance MUST NOT create, satisfy, replace, or authorize the new-path Prepared Scan handoff.
- **FR-074**: Default production composition MUST use the accepted CodeGraph 1.5 and JDT-LS analysis boundaries and MUST NOT resolve, register, modify, bridge, adapt, fall back to, or execute `CodeGraphContextProvider`, the historical `RepositoryProfilingCoordinator`, or the obsolete `/scan-runs` profiling entrance for this path.
- **FR-075**: Initiation MUST NOT acquire source, run external analysis, or mutate a File Code Unit, Document, Edge, Uncertainty, or LLM-visible artifact. Initiation and preparation MUST NOT start Worker, Pass 1, Cross-Repository Resolution, Pass 2, Chat, or LLM work owned by an accepted later stage.
- **FR-076**: A production-composition vertical test MUST begin only with an authenticated authorized user, one ordinary System, exactly two ordinary Repository records, exact Git-ref/Profiled-Revision bindings, and a ready accepted CodeGraph 1.5/JDT-LS runtime. It MUST pre-seed no Scan Run, Pre-Scan Result, Scan Execution, Two-Repository coordination, Prepared Scan Manifest, File Code Unit, or Document, and MUST prove all nine acceptance scenarios in User Story 6. Real CodeGraph CLI qualification MAY support but MUST NOT substitute for this test.
- **FR-077**: After implementation acceptance, every retained blocked or diagnostic Track C database, including run-03, MUST remain unchanged as historical evidence. The release continuation MUST create a fresh guarded lowercase `vulgata_story125_*` database, repeat sanitized preflight and production bootstrap, enter through the corrected Prepared Scan path, and continue Track C only after the real Prepared Scan succeeds.
- **FR-078**: This correction MUST NOT redesign the general Scan lifecycle, restore standalone or single-Repository profiling, support arbitrary N-Repository initiation, delete historical tables or compatibility code, or consolidate legacy Scan entrances. Those obligations remain explicit post-competition backlog.

#### Reviewed Exact-Profile Seed Precondition

- **FR-079**: One server-owned read-only authority MUST derive required source paths only from the existing embedded reviewed `ExactResolutionProfile`; no request, user context, LLM, provider, or external file MAY supply or alter its profile, paths, aliases, refs, revisions, scores, or selectors.
- **FR-080**: The reviewed-profile seed authority MUST activate only when exactly two Repository stable identities, safe full Git refs, and Profiled Revisions completely and uniquely match the reviewed profile. Any pair, ref, or revision that does not complete that exact identity match is nonmatching and MUST receive no profile-derived classification, score, promotion, rejection, or other selection effect.
- **FR-081**: On a complete match, every safe eligible File Code Unit required for the consumer, provider declaration, provider implementation, and unsupported-observation owner MUST receive one closed reviewed-profile business-evidence classification and MUST participate in the existing high-value-seed authority.
- **FR-082**: A matched profile with any required source absent, filtered, unsafe, non-contained, multiply mapped, lacking an exact bounded split, or exceeding the existing maximum 20 seeds per Repository or 150 selected pair units MUST reject preparation before either Prepared Scan Manifest publishes.
- **FR-083**: Selection MUST remain input-order-independent and deterministic. Identical authorized replay over the same exact revisions and reviewed profile MUST produce the same selected units, scores, audit rows, package fingerprint, and Manifests.
- **FR-084**: All reviewed-profile required high-value seeds MUST succeed in Pass 1. An ordinary-failure budget MUST NOT permit exact resolution or Pass 2 to continue when any required seed failed or lacks a current source-anchored Code Logic Document.
- **FR-085**: Production code MUST contain no sample Repository name, path, endpoint, symbol, or line number. It MAY read those values only through the existing validated embedded profile contract after exact identity activation.
- **FR-086**: This exception MUST NOT change generic evidence classification for a nonmatching pair, increase existing bounds, add an EF migration or model change, mutate Story 12.4 contracts, restore CGC or the historical profiling entrance, or start Worker, Pass 1/2, Document, Edge, Uncertainty, Chat, or LLM activity during preparation.

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
- **Prepared Scan Initiation Tuple**: The all-or-nothing durable binding of exactly two Profiled-Revision-bound Scan Runs, their two new-path Scan Executions with current Analysis Runtime, prompt, and Document schema identities, and one canonical Preparing Two-Repository coordination.
- **Reviewed Exact-Profile Seed Set**: The immutable server-derived mapping from the existing reviewed exact-resolution profile and its exact two Repository stable identities, safe full Git refs, and Profiled Revisions to every required safe eligible File Code Unit that must survive bounded selection as a high-value seed.

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
- **SC-010**: The final Scan covers 50–150 automatically selected File Code Units across exactly two real Java/XML Repositories bound to their exact Profiled Revisions, with every high-value seed successful and ordinary failures within the accepted budget.
- **SC-011**: The final candidate contains at least one exact Cross-Repository REFERENCES Edge, one honest Uncertainty, and three Business Logic Documents including one Cross-Repository flow.
- **SC-012**: One application-process restart against the same disposable release database preserves authenticated wiki and grounded Chat access with zero duplicate published Documents caused by restart.
- **SC-013**: Every final release claim is supported in one human-readable candidate-bound report by recorded Profiled Revisions, versions, commands/exits, query results, manual evaluation rows, all five required screenshot categories, restart evidence, failures, or limitations.
- **SC-014**: Zero Track C-MVP attempts read from or write to Story 12.4's qualification database or the main development database; every accepted attempt uses a uniquely named `vulgata_story125_*` database.
- **SC-015**: In 100% of accepted Chat attempts, the primary provider response and any syntax-repair response use streaming transport, stay within the 16 KiB chunk and 64 KiB total bounds, and produce exactly one valid terminal outcome.
- **SC-016**: Across streamed partial JSON, split UTF-8, disconnect, ten-second inactivity, cancellation, overflow, malformed UTF-8, missing terminal, duplicate or contradictory terminal, and post-terminal-data cases, zero provider fragments, claims, citations, handles, relationships, or source details render before complete validation.
- **SC-017**: For 100% of accepted streamed answers, all retrieval, streaming, optional repair, validation, and accepted-result completion fit within one under-30-second budget, and any progressive UI reveal contains only Unicode-safe units from the already validated Grounded Answer.
- **SC-018**: For 100% of unavailable required external prerequisites, the report identifies the sanitized blocked condition, records no fabricated observation, and makes zero success claims for the unexecuted release work.
- **SC-019**: From the no-preseed production-composition starting state, 100% of authorized successful initiations create one complete tuple and reach Prepared Scan Manifest publication through the accepted preparation path.
- **SC-020**: Across repeated and concurrent identical authorized initiation requests, 100% of successful results identify the same two Scan Runs, two new-path Scan Executions, and one coordination, with zero duplicate, partial, or cross-paired tuple.
- **SC-021**: Across changed Repository pair, Git ref, Profiled Revision, runtime, prompt, Document schema, or authorization-scope requests, zero mismatched requests attach to or mutate an existing tuple and zero protected tuple detail is disclosed.
- **SC-022**: Across unauthorized initiation attempts, zero Scan Runs, Scan Executions, or Two-Repository coordination records are created.
- **SC-023**: After an induced failure between committed initiation and Prepared Scan Manifest publication, an exact authorized retry reuses the same tuple and can reach publication without replacement or cross-paired initiation state.
- **SC-024**: In 100% of production-composition checks, the Prepared Scan path uses the accepted CodeGraph 1.5/JDT-LS boundaries, resolves no CodeGraphContext or historical profiling fallback, and starts zero Worker, Pass 1, Cross-Repository Resolution, Pass 2, Document, Edge, Uncertainty, Chat, or LLM activity owned by a later stage.
- **SC-025**: In 100% of observed initiations, the complete durable tuple becomes visible before external analysis begins, and no transaction that creates or replays that tuple remains open during Git, CodeGraph, JDT-LS, XML, or other long-running analysis.
- **SC-026**: For the matched reviewed release pair, 100% of profile-required consumer, provider declaration, provider implementation, and unsupported-observation File Code Units are selected as high-value seeds inside the unchanged bounds.
- **SC-027**: After an exact profile identity match, across absent, unsafe, filtered, non-contained, multiply mapped, split-invalid, ambiguous, and over-bound required-source cases, zero Prepared Scan Manifests publish and zero later-stage work starts.
- **SC-028**: Across nonmatching pairs/revisions and neutral fixtures, the reviewed-profile authority changes zero candidate scores, selected units, fingerprints, or audit outcomes.
- **SC-029**: One no-preseed production-composition vertical reaches a terminal accepted Pass 1 with every reviewed-profile seed successful and then creates the accepted exact Cross-Repository decision/REFERENCES Edge and honest Uncertainty without direct database repair.

## Assumptions

- Story 12.4 is an accepted immutable dependency at `4798b97d9f23aef587829175df7ab7ebc3daf995`, including current CrossRepositoryResolutionDecision, exact DocumentEdge, complete GENERATED_FROM, and Flow Group publication authority.
- Track A and Track B behavior and tests are accepted at `f292caec427f9844be3c0f65b4cb00a9292080a5`; the Track C-MVP correction at `1517d8c85eed643a092fbb1c997f232c90e6b52c` preserves them.
- `specs/008-demo-chat-release-gate` is stale lifecycle history. Existing Track C release contracts, schemas, proof-oriented harness obligations, plan, and tasks that encode deferred automation are invalidated downstream evidence, not current acceptance authority.
- Existing authentication, Repository authorization, LLM provider configuration, immutable Document storage, Source Anchors, and durable Scan recovery remain available and retain their existing security boundaries.
- Conversation state is transient and bounded; saved Chat sessions, uploads, long-term memory, automatic scope determination, and broad post-August V1 Chat tools remain out of scope.
- The configured real LLM is expected to support the accepted mandatory streaming Chat path. If the provider or another required external prerequisite is unavailable, the honest result is blocked evidence rather than a substituted fake run.
- No EF migration, `VulgataDbContext`, model-snapshot, Story 12.4, or Scan-behavior change outside the bounded FR-063 through FR-078 initiation and FR-079 through FR-086 reviewed-profile seed exceptions is permitted in Track C-MVP.
- Run-03 and every earlier blocked or excluded Track C database remain immutable historical evidence; none may be repaired, migrated, or reused to satisfy the fresh corrected release lane.

## Source Traceability

- `docs/bmad/epics.md` — canonical Story 12.5 user story and acceptance criteria.
- `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-29-story-12-5-track-c-mvp.md` — approved Track C-MVP retained/deferred boundary and success conditions.
- `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-30-story-12-5-prepared-scan-initiation.md` — approved bounded in-place Prepared Scan initiation correction, transaction/replay outcomes, vertical-test boundary, and post-competition exclusions.
- `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-31-story-12-5-exact-profile-seed-selection.md` — approved real-run correction binding the reviewed exact profile's required sources into the existing high-value-seed authority.
- `docs/bmad/implementation-artifacts/12-5-grounded-llm-wiki-chat-and-august-30-release-evidence.md` — original activation and ownership history, superseded wherever the approved Track C-MVP correction differs.
- `docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md` — August 30 scope, tiered verification, UI states, release metrics, and explicit deferrals.
- `docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md` — controlling retrieval, expansion, packet, conversation, runtime, and release bounds.
- `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md` — dated August 30 overlay, Business/IT value, authorization, citations, and NFR-1.2.
- `docs/UBIQUITOUS_LANGUAGE.md` — canonical domain terms.
- `.specify/memory/constitution.md` and `AGENTS.md` — security, ownership, UI, validation, and project-governance constraints.
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-b-final-01.md` — accepted Track B behavior, tests, conditional E2E, and exact verification inventory.
- `specs/014-grounded-llm-wiki-chat-release-evidence/decisions/correct-course-track-c-mvp-01.md` — accepted Track C-MVP correction decision.
- `specs/014-grounded-llm-wiki-chat-release-evidence/decisions/correct-course-prepared-scan-initiation-01.md` — accepted specification/planning authority for the missing production Prepared Scan entrance; it does not authorize tasks or implementation.
- `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-03/release-report.md` — retained diagnostic evidence proving the accepted initiation/replay path, zero high-value seeds, three cap-excluded reviewed sources, fail-closed exact-resolution refusal, and the fresh-database requirement.
- `specs/013-exact-cross-repository-flow-pass2/` — accepted Story 12.4 dependency evidence consumed unchanged by Story 12.5.
- `specs/008-demo-chat-release-gate/` and current Prepared Chat production/tests — stale specification history and reusable implementation evidence only.

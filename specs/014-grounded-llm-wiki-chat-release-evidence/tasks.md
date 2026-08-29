---
description: "Fresh dependency-ordered Story 12.5 tasks for the accepted design-correction-02 boundary"
---

# Tasks: Grounded LLM-Wiki Chat & August 30 Release Evidence

**Input**: `.specify/memory/constitution.md`, `docs/UBIQUITOUS_LANGUAGE.md`, Story 12.5 in `docs/bmad/epics.md`, `docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md`, `spec.md`, accepted corrected `plan.md`, `research.md`, corrected `data-model.md`, `quickstart.md`, every contract under `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/`, `decisions/design-correction-02.md`, `decisions/planned-change-authorization-04.md`, `reviews/definition-review/definition-review-07.json`, and `reviews/plan-review/plan-review-11.json`.

**Pinned authority**: Source commit `76de29c8fb060508c475731533232939e7fe8695`; specification SHA-256 `a7367b1f64fcfc2cf099d7fd204575f314e35994204ccb0cd7b7f2dacf6f7dd4`; plan SHA-256 `fad42dcd3a3f393809e95500ea6d6c7607ce4a16787d8b16d054b415ce533318`; corrected data-model SHA-256 `dc2017b93f1fd15235e351f7abe7e4cabc4344d66fe1af09f00ffbc14d8fa348`; immutable accepted Story 12.4 dependency commit `4798b97d9f23aef587829175df7ab7ebc3daf995`.

**Fresh-generation disposition**: The prior 81-task list, including its final deadline-convergence continuation tail, is invalidated historical evidence. These T001-T053 tasks replace it completely; no old completion checkbox or prior Track B/Track C verdict transfers to this boundary.

**Definition-review correction disposition**: Definition review 07 rejects task hash `3b3614edd1ecbefc9df1ab7783cf16507148a11b4ab4bd21509407cfc60a6075`. The accepted plan-review-11 correction changes only the data-model overview, keeps the specification/plan/architecture scope unchanged, and permits reuse of planned-change authorization 04. This task set corrects S125-DR7-001 and S125-DR7-002 without adding work or changing Track ordering.

**Classification rule**: `[EXISTING-EVIDENCE]` means characterize and reaccept behavior already present at the pinned source; do not manufacture a RED. `[RED]` means observe failure at the named public seam before production work. `[GREEN]` is the smallest production/harness change that turns that observed RED green. `[GATE]` and `[VERIFY]` never imply acceptance by themselves.

**Ownership rule**: The root assigns one coordinated **Integration Owner** for `src/dotnet/Vulgata.Web/Program.cs`, `src/dotnet/Vulgata.Agents/DependencyInjection.cs`, `src/dotnet/Vulgata.Web/Api/ApiEndpointRegistrationExtensions.cs`, project files, and central package files. No other task edits those paths. Story 12.4 source/contracts, `VulgataDbContext`, configurations, migrations, and `VulgataDbContextModelSnapshot` are read-only.

**B-INT test ownership rule**: The root assigns one named **B-INT Interaction Test Owner** for `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` and `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs`. That owner completes T033 before starting T034; these shared-file tasks are never parallel.

**Prohibited scope**: No EF migration; Story 12.4 contract mutation; vector/embedding/cross-encoder retrieval; raw CodeGraph proximity; autonomous multi-hop or unrestricted Agent tool loop; fake Backup B release evidence; non-streaming accepted provider answer; provider/unvalidated fragment at the client; Track C candidate/process/signature/export fields in Track B telemetry.

## Phase 1: Setup and authority preflight

**Purpose**: Re-observe the exact branch, hashes, dependency, and task disposition.

- [x] T001 Classification: GATE — Verify branch, clean worktree/index, source commit `76de29c8fb060508c475731533232939e7fe8695`, accepted spec/plan/data-model hashes, accepted plan-review-11, ancestral Story 12.4 commit `4798b97d9f23aef587829175df7ab7ebc3daf995`, and reusable authorization-04 before any test/production write; record the result in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/preflight/task-regeneration-01.md` (FR-048, FR-049, FR-052, SC-013).
- [x] T002 [P] Classification: EXISTING-EVIDENCE — Reclassify current production/tests as preserved Track A, reacceptance-only B1-B3, missing B4 publication owner, missing B-INT, and closed Track C; record exact source/test owners and the discarded prior 81-task history in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/ownership/task-disposition-01.md` (FR-001, FR-048, FR-051).

**Checkpoint**: The accepted corrected definition and fresh task disposition are pinned.

---

## Phase 2: Foundational boundary guards and contract validation

**Purpose**: Block every user-story phase on current ownership, schema, transport-vocabulary, and forbidden-write proof.

- [x] T003 [P] Classification: EXISTING-EVIDENCE — Reaccept the existing no-migration, no-model-snapshot, Story 12.4 read-only, forbidden-database, fake-state, and shared-file-owner guards in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs` and `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier2PostgreSqlTests.cs`; any failed boundary stops implementation instead of authorizing a migration or dependency edit (FR-036, FR-047, FR-049-FR-052, SC-014).
- [x] T004 Classification: VERIFY — Validate all Story 12.5 JSON schemas, exact cross-artifact transport vocabulary/caps, accepted hashes, and task checklist format; write command/result evidence to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/task-definition-01.md` without changing contracts (FR-021-FR-024, FR-039, FR-054-FR-060, SC-003, SC-013, SC-015-SC-017).

**Checkpoint**: Authority, ownership, and forbidden-write guards are current. A mismatch returns to plan/definition review before implementation.

---

## Phase 3: Preserved Track A reacceptance - User Stories 3 and 4

**Purpose**: Preserve accepted production Scan UI and evaluation preparation; no Track A rewrite is planned.

### User Story 3 - Monitor and resume a real Scan (Priority: P1, Track A)

**Independent Test**: Persisted preparation/running/retry/paused/resumed/completed/warning/failure rows render bounded Chinese production state, and only an authorized recoverable pause exposes `继续扫描`; fake state is absent.

- [x] T005 [US3] Classification: EXISTING-EVIDENCE — Run and reaccept persisted state mapping, counts, earliest retry, bounded reason, twenty-file failure summary, missing-equivalent authorization, durable resume, and fake-state exclusion in `tests/Vulgata.Tests/ProductionScanStatusReaderTests.cs`, `tests/Vulgata.Tests/ProductionScanPageTests.cs`, and `tests/Vulgata.Tests/DurableScanning/ManualResumeApiTests.cs` (FR-033-FR-036, SC-007).

### User Story 4 - Prepare a repeatable Chat evaluation (Priority: P2, Track A)

**Independent Test**: The fixed 12-20-case corpus and closed run-envelope contracts validate without desired prose, fake state, forbidden database access, or fabricated passed evidence.

- [x] T006 [P] [US4] Classification: EXISTING-EVIDENCE — Run and reaccept corpus count/categories, acyclic at-most-three-question follow-ups, no `expectedAnswer`, authorization-negative automatic failure, exact half-up scoring, passed-only receipts, ten-check inventory, append-only supersession, and database guard coverage in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs` and `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs` (FR-037-FR-040, FR-046-FR-047, SC-008, SC-014).
- [x] T007 [US4] Classification: VERIFY — Record the unchanged Track A focused command results and accepted-evidence classification in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-a-preservation-01.md`; a regression opens a separately reviewed Track A correction and does not broaden Track B ownership (FR-033-FR-040, SC-007-SC-008).

**Checkpoint**: Track A remains preserved. Its evidence does not claim B1-B4, B-INT, or Track C completion.

---

## Phase 4: Track B1 - lexical primary projection and packet 2.2 reacceptance (User Story 1)

**Goal**: Reaccept the existing single typed `AuthorizedChatSnapshot -> ChatLexicalPrimaryProjection -> ChatRelationshipExpansionProjection -> ChatEvidenceAssembly` topology with lexical projection as the sole primary authority.

**Independent Test**: Equivalent literal/neutral wording and arbitrary relationship fixture mutations retain identical primary Document/section identities; only one constructor-composed relationship projector and one packet builder can participate.

- [x] T008 [US1] Classification: EXISTING-EVIDENCE — Reaccept and mutation-guard the single typed topology, exactly-once lexical/relationship/builder interactions, and absence of `RequiresCrossRepositoryEvidence`, `ExactRelationshipRow`, `ProvenanceRow`, projection-only final return, direct projector construction, alternate packet return, duplicate build, and Story 12.5 legacy authority in `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` and `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` (FR-001, FR-007, FR-013-FR-014, FR-021, SC-002).
- [x] T009 [P] [US1] Classification: EXISTING-EVIDENCE — Reaccept NFC/token-array parsing, ASCII/CJK extraction, exact `50/40/30/25/20/5/3/1` scoring, active-mode-only term field, prior-current-Document +1, full-set ranking before top 256, strongest-per-Document, twelve-primary target, five-Document/four-section bounds, randomized ties, and insufficient fallback in `tests/Vulgata.Tests/ChatSupplementaryEvidenceSelectorTests.cs` and `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` (FR-005-FR-013, SC-002-SC-003).
- [x] T010 [P] [US1] Classification: EXISTING-EVIDENCE — Reaccept server-created authorization scope, applied Global/System/exact-two-Repository Context, canonical Repository order, caller/pending exclusion, fallback-only advisory terms, authority/currentness fingerprinting, and exact 8,192/8,193 UTF-8 behavior in `tests/Vulgata.Tests/ChatAdvisoryContextReaderTests.cs`, `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs`, and `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs` (FR-002-FR-004, FR-012, FR-019, FR-023-FR-025, SC-001, SC-005).
- [x] T011 [P] [US1] Classification: EXISTING-EVIDENCE — Reaccept the sole packet-2.2 builder, independently constructed fingerprint vectors, opaque handles, primary/seed identities, complete sections/Source Anchors, whole-unit drop order, selected-required refusal, 1-5/1-20/1-32/32/10/12 bounds, and exact/+1 evidence/conversation/advisory UTF-8 limits in `tests/Vulgata.Tests/ChatEvidencePacketBuilderTests.cs` and `tests/Vulgata.Tests/ChatEvidencePacketV22ContractTests.cs` (FR-021-FR-025, SC-002-SC-003).
- [x] T012 [US1] Classification: EXISTING-EVIDENCE — Reaccept packet-2.2 candidate extraction and provider blindness in `tests/Vulgata.Tests/ChatCandidateExtractorTests.cs`, `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs`, and `tests/Vulgata.Tests/ChatContractTests.cs`, proving only bounded question/mode/fingerprint/handles/packet/conversation enter synthesis and no database, source Repository, analysis tool, legacy profile, vector, or unrestricted tool is callable (FR-013, FR-025, SC-004-SC-005).
- [x] T013 [US1] Classification: VERIFY — Run the focused B1 suites plus relevant regressions and write exact commands, results, immutable primary identities/fingerprint vectors, and structural-guard evidence to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-b1-reacceptance-01.md`; accept B1 independently without claiming B2-B4 (FR-001-FR-013, FR-021-FR-025, SC-001-SC-005).

**Checkpoint**: B1 is independently accepted only if all current behavior and topology guards pass unchanged.

---

## Phase 5: Track B2 - current-v2 relationship and currentness reacceptance (User Story 1)

**Goal**: Hold B1 primaries fixed while reaccepting the sole optional relationship projector and its closed support states.

**Independent Test**: Two valid decisions sharing one current directed Edge yield one relationship, selected canonical decision, count two, stable full-set fingerprint, and two decision-owned Published Flow Groups; selected-required mutation rejects the whole assembly without substitution.

- [x] T014 [US1] Classification: EXISTING-EVIDENCE — Reaccept one current Cross-Repository REFERENCES Edge with many independently valid Story 12.4 decision slots, canonical selected decision, complete contribution recomputation, decision count/set fingerprint, and distinct decision-owned Published Flow Groups under randomized enumeration in `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs` and `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` (FR-014-FR-015, FR-019-FR-021, SC-001-SC-002).
- [x] T015 [P] [US1] Classification: EXISTING-EVIDENCE — Reaccept every `absent`, `present-valid`, `present-inconsistent`, and `selected-required` relationship/Flow Group state, including legacy-only/non-exact/missing/reversed/stale/wrong-scope/wrong-revision/fingerprint/conflicting-slot/incomplete/unpublished cases, in `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs` (FR-014-FR-020, SC-001, SC-005).
- [x] T016 [P] [US1] Classification: EXISTING-EVIDENCE — Reaccept Business/IT relationship preference, first `min(3,count)` lexical seeds, two additions per seed, stored direction, one hop, cycle/duplicate handling, five Documents, 32 relationships, ten Flow Group authorities, twelve ordered steps, and complete eligible-set ranking before bounds in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` (FR-015-FR-018, FR-021-FR-022, SC-002-SC-003).
- [x] T017 [US1] Classification: EXISTING-EVIDENCE — Reaccept post-selection currentness for every selected Document, section, Source Anchor, Edge, decision contribution set, Published Flow Group, trace, step, System, Repository, Scan Run, and Profiled Revision in `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs` and `tests/Vulgata.Tests/ChatEvidencePacketBuilderTests.cs`; optional invalid facts support nothing, while selected-required drift withholds the entire answer (FR-003-FR-004, FR-019-FR-024, SC-001, SC-004-SC-005).
- [x] T018 [US1] Classification: VERIFY — Run focused B1+B2 suites and write decision-set, Flow Group ordering, state-table, mutation, no-legacy-authority, and no-Story-12.4-mutation evidence to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-b2-reacceptance-01.md`; accept B2 independently without claiming B3/B4 (FR-014-FR-024, FR-049-FR-050, SC-001-SC-005).

**Checkpoint**: B2 is independently accepted, with Story 12.4 remaining read-only.

---

## Phase 6: Track B3 - bounded transient conversation reacceptance (User Story 2)

**Goal**: Reaccept two-turn transient Conversation State as context only, with full re-fetch and revalidation on every follow-up.

**Independent Test**: Initial question and two follow-ups use prior eligible Logical Document identities only as +1 hints, evict older state, and fail closed on row/access mutation without carrying prose or handles as evidence.

- [x] T019 [P] [US2] Classification: EXISTING-EVIDENCE — Reaccept last-two validated turns, fourth-turn eviction, exact 4,096/4,097 UTF-8 behavior, prior packet/handle/Logical Document binding, reset on new conversation/mode/scope/access loss/disposal, and non-persistence in `tests/Vulgata.Tests/ChatConversationStateTests.cs` (FR-023, FR-029, SC-006).
- [x] T020 [US2] Classification: EXISTING-EVIDENCE — Reaccept initial plus two follow-ups, exactly-once evidence re-fetch/currentness/final authorization/advisory reread per turn, prior-current-Document +1 only after eligibility, missing-equivalent revocation, and zero activation or replacement of live current Conversation State on failed attempts in `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs` and `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` (FR-003-FR-004, FR-008, FR-029-FR-031, SC-001, SC-005-SC-006).
- [x] T021 [P] [US2] Classification: EXISTING-EVIDENCE — Reaccept complete-result packet fingerprint, handle, claim, citation, Source Anchor, semantic, and no-repair-on-semantic-failure validation in `tests/Vulgata.Tests/ChatAgentResultValidatorTests.cs` and `tests/Vulgata.Tests/ChatContractTests.cs`; prior answer prose remains synthesis context and never validates a current claim (FR-024-FR-031, SC-004-SC-006).
- [x] T022 [US2] Classification: VERIFY — Run focused B1-B3 suites and record turn eviction, reset, re-fetch/mutation, bounded-context, claim/citation, and no-failed-live-state-activation evidence in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-b3-reacceptance-01.md`; accept B3 independently without claiming provider publication (FR-023-FR-031, SC-001, SC-004-SC-006).

**Checkpoint**: B3 is independently accepted; provider/publication acceptance remains B4 work.

---

## Phase 7: Track B4 - strict streaming, atomic accepted publication, and accepted-only reveal (User Story 2)

**Goal**: Preserve the correct raw transport and add one Web-scoped publication owner with one `AcceptedPublicationCommit` linearization point.

**Independent Test**: Cancellation/timeout/preparation failure at every pre-commit transition publishes nothing; a single preallocated candidate commits answer, Conversation State, completion summary, and reveal directive atomically when the sole monotonic sample is before deadline, and advancing time after that sample cannot create another success check.

### Behavioral RED

- [x] T023 [US2] Classification: RED — Add public-seam state-machine tests proving a prepared candidate has zero live answer, Conversation State, accepted completion summary, or reveal effect through `PublicationCandidateReady`, and that every earlier failure closes without publication, in `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs` (FR-027, FR-031-FR-032, FR-056-FR-059, SC-004-SC-005, SC-016-SC-017).
- [x] T024 [US2] Classification: RED — Add fake monotonic clock/cancellation tests for the single-writer critical section: cancellation wins before the clock sample, `sample >= deadline` publishes nothing, `sample < deadline` performs exactly one atomic terminal exchange, and a scheduler advancing time immediately after the sample cannot trigger another acceptance check, in `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs` (FR-032, FR-056-FR-057, SC-005, SC-017; AD-7C).
- [x] T025 [US2] Classification: RED — Add throwing runtime-observation lookup, completion-template preparation, conversation preparation, reveal-plan allocation/callback, and structural mutation tests that forbid direct accepted-summary emission, activation or replacement of live current Conversation State, successful `PreparedChatResult` construction, or page publication outside `AcceptedPublicationCommit`; explicitly permit immutable candidate-only `AppendValidatedTurn` and proposed-state construction before the publication critical section in `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs`, `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`, and `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` (FR-027, FR-029-FR-032, FR-056-FR-060, SC-005-SC-006, SC-016-SC-017; AD-7C).
- [x] T026 [P] [US2] Classification: RED — Add client-boundary tests proving `ChatPage` reads content only from an `AcceptedPublicationCommit`, provider/pending buffers remain invisible, committed `ImmediateAccepted` is complete, `ProgressiveAccepted` uses Unicode text elements, and schedule lag/reduced motion switches only already accepted content to full display in `tests/Vulgata.Tests/ChatValidatedRevealTests.cs` and `tests/Vulgata.Tests/ChatPageTests.cs` (FR-027, FR-032, FR-058-FR-059, SC-004-SC-005, SC-016-SC-017).

### Minimal GREEN

- [x] T027 [US2] Classification: GREEN — Add the Web-scoped `IPreparedChatPublicationOwner`, closed publication states, immutable preallocated `PreparedChatPublicationCandidate`, committed composite record, reveal directive, and synchronous non-throwing atomic `AcceptedPublicationCommit` in `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs`; no persistence, Agents, Infrastructure, or Story 12.4 type owns publication (FR-027, FR-031-FR-032, FR-056-FR-060, SC-004-SC-005, SC-016-SC-017).
- [x] T028 [US2] Classification: GREEN — Refactor only the accepted tail of `PreparedChatCoordinator.CompleteGroundedAnswerAsync` to use immutable candidate-only `AppendValidatedTurn` when constructing the proposed next Conversation State with the immutable answer, runtime-observation IDs, neutral completion template, final currentness/authorization/advisory results, and reveal directive before delegating once to `IPreparedChatPublicationOwner`; remove only activation or replacement of live current Conversation State and accepted completion recording outside `AcceptedPublicationCommit`, and make follow-up reads obtain current Conversation State authority from the committed composite in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` (FR-003-FR-004, FR-027-FR-032, FR-056-FR-060, SC-001, SC-004-SC-006, SC-016-SC-017; AD-7C).
- [x] T029 [US2] Classification: GREEN — Make `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`, `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`, and `src/dotnet/Vulgata.Web/Components/Pages/ChatTurn.cs` consume only the committed composite reveal directive, preserve generic pending/cancel state and render-time scrolling, and supervise progressive schedule lag without publishing new content (FR-005-FR-006, FR-027, FR-032, FR-058-FR-059, SC-004-SC-005, SC-016-SC-017).

### Streaming reacceptance and B4 exit

- [x] T030 [US2] Classification: EXISTING-EVIDENCE — Reaccept mandatory raw primary/repair streaming, shared 29-second deadline and three-second repair threshold, strict split wire UTF-8/SSE/JSON, one `[DONE]` plus physical EOF, ten-second decoded-data inactivity, received 16,384/16,385 and 65,536/65,537 pre-JSON caps, separate raw-body/SSE/decoded-delta/final-answer domains, buffer discard, and closed failures in `tests/Vulgata.Tests/ChatProviderStreamAssemblerTests.cs`, `tests/Vulgata.Tests/OpenAiCompatibleChatProviderStreamingTransportTests.cs`, and `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs` (FR-028, FR-031-FR-032, FR-053-FR-057, SC-005, SC-015-SC-017).
- [x] T031 [US2] Classification: VERIFY — Reaccept Track B runtime/completion separation and mutation-guard exact production raw-adapter composition, proving content-dark immutable counters/outcomes, coordinator-owned validation, no candidate/run/case/key/signature/export fields, no SDK aggregate acceptance, and no Track C sink in production B composition in `tests/Vulgata.Tests/ChatStreamObservationProvenanceTests.cs`, `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs`, and `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` (FR-053-FR-060, SC-015-SC-017).
- [x] T032 [US2] Classification: VERIFY — Run the focused B4 RED-to-GREEN suite, B1-B3 regressions, and Web build; record RED commands, minimal GREEN diff, publication transition evidence, byte/deadline/cancellation results, zero-fragment client proof, and neutral telemetry proof in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-b4-implementation-01.md` (FR-025-FR-032, FR-053-FR-060, SC-004-SC-005, SC-015-SC-017).

**Checkpoint**: B4 is independently accepted. Aggregate green tests do not yet claim Track B completion.

---

## Phase 8: Track B integration checkpoint B-INT (User Stories 1 and 2)

**Goal**: Prove the production composition traverses the accepted B1-B4 ports exactly once and has no alternate authority or publication path.

**Independent Test**: Authenticated Business and IT initial/follow-up requests execute lexical -> relationship -> packet/current conversation -> raw stream -> publication owner once; replacing or skipping any port fails the interaction test.

- [x] T033 [US1] Classification: RED — As the named B-INT Interaction Test Owner, add a production-boundary interaction test with counting/mutation ports for authenticated Business initial/follow-up flows, proving exactly one lexical projection, relationship projection, packet build, current conversation re-fetch, strict raw provider attempt, currentness/final authorization pass, and publication-owner commit in `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` and `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` (FR-001-FR-005, FR-007-FR-032, FR-053-FR-059, SC-001-SC-006, SC-015-SC-017).
- [x] T034 [US2] Classification: RED — After T033, the same B-INT Interaction Test Owner adds the equivalent IT initial/follow-up interaction and mutations that replace lexical primaries, skip relationships, bypass re-fetch, use non-streaming aggregation, activate or replace live current Conversation State outside `AcceptedPublicationCommit`, publish success twice, or compose Track C attestation; explicitly permit candidate-only proposed-state construction, and require each mutation to fail in `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` and `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` (FR-001-FR-004, FR-006-FR-032, FR-053-FR-060, SC-001-SC-006, SC-015-SC-017; AD-7C).
- [x] T035 [US2] Classification: GREEN — As the sole Integration Owner, register the scoped `IPreparedChatPublicationOwner` and resolve the exact accepted B1-B4 ports once in `src/dotnet/Vulgata.Web/Program.cs` and, only if the observed interaction RED requires it, `src/dotnet/Vulgata.Agents/DependencyInjection.cs`; do not edit central endpoint/project/package files or compose `Story125StreamAttestationSink` in Track B (FR-048, FR-051, FR-053-FR-060).
- [x] T036 [US2] Classification: VERIFY — Run focused B1-B4, authenticated integration, Web build, full Tier 1 tests, `git diff --check`, and guards proving no `VulgataDbContext`/migration/snapshot, Story 12.4 contract, legacy Chat authority, second packet builder, activation or replacement of live current Conversation State outside `AcceptedPublicationCommit`, second publication owner, Track C sink, or release field is reachable; also prove immutable candidate-only proposed-state construction remains reachable and follow-up reads derive Conversation State authority from the committed composite, then record results in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/integration/track-b-int-01.md` (FR-048-FR-060, SC-001-SC-008, SC-014-SC-017; AD-7C).
- [x] T037 [US2] Classification: VERIFY — Bind the independently accepted B1, B2, B3, B4, and B-INT evidence with exact candidate/diff/test hashes in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-integration-01.md`; this is the only task allowed to propose Track B implementation completion (FR-039-FR-040, FR-048-FR-052, SC-013).

**Checkpoint**: Track B implementation is checkpoint-ready, not automatically converged, reviewed, E2E-accepted, or finally verified.

---

## Phase 9: Convergence, review, E2E-decision, and final Track B verification preparation

**Purpose**: Prepare traceable inputs for independent workflow gates without treating task execution as gate acceptance.

- [x] T038 Classification: VERIFY — Run convergence against the accepted specification/plan/tasks and current implementation, rejecting green-count-only claims and checking every B1-B4/B-INT mutation invariant; write the traceable remaining-work report to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/convergence/track-b-corrected-01.md` and return any real gap to a new dependency-ordered RED/GREEN task before review (FR-001-FR-032, FR-048-FR-060, SC-001-SC-006, SC-015-SC-017).
- [x] T039 Classification: VERIFY — Prepare independent quality-review evidence covering correctness, authorization/currentness, strict transport, atomic publication, telemetry separation, regression risk, no migration, and Story 12.4 compatibility in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/review/track-b-quality-input-01.md`; only an external reviewer may issue the verdict (FR-003-FR-004, FR-019-FR-032, FR-049-FR-060, SC-001, SC-004-SC-005, SC-015-SC-017).
- [ ] T040 Classification: VERIFY — Prepare the conditional economical E2E decision from accepted B-INT and quality evidence in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/e2e/track-b-decision-input-01.md`; if browser/API E2E is required, verify real authenticated Business/IT accepted-only render and cancellation with no fake state, and do not substitute Track C release evidence (FR-002-FR-006, FR-027, FR-031-FR-032, FR-058-FR-059, SC-004-SC-007, SC-016-SC-017).
- [ ] T041 Classification: VERIFY — After convergence, quality review, and the conditional E2E decision are accepted, run focused/full tests, Web build, schema/semantic checks, `git diff --check`, no-migration/Story-12.4 boundary guard, and exact hash inventory; write final Track B verification to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-b-final-01.md` and bind the accepted checkpoint before opening Track C (FR-039-FR-040, FR-048-FR-060, SC-001-SC-008, SC-013-SC-017).

**Checkpoint**: Track C remains closed until T041 is accepted and one clean candidate is pinned.

---

## Phase 10: Track C - same-candidate Tier 2/Tier 3 release proof (User Story 5)

**Hard dependency**: Start only after accepted Track A preservation, B1, B2, B3, B4, B-INT, convergence, quality review, conditional E2E decision, final Track B verification, and a clean pinned candidate. This ordering remains mandatory even when implementation approval is already hash-bound.

**Goal**: Produce honest same-candidate Tier 2/Tier 3 evidence using real required tools, controlled/real providers as specified, and uniquely owned `vulgata_story125_*` PostgreSQL databases.

**Independent Test**: A passed schema-2.2 envelope recomputes every Scan/Chat/stream/restart/database/screenshot claim from authenticated same-candidate artifacts and rejects direct/unsigned/foreign/SDK-fake or fake-Backup-B evidence.

- [ ] T042 [US5] Classification: GATE — Verify the accepted Track B checkpoint, clean candidate, exact integrated Story 12.4 commit, production raw-adapter type/hash, tool/provider prerequisites, and a newly generated owned lowercase `vulgata_story125_*` database before any product connection; record the preflight in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tier-2/preflight-01.md` (FR-041-FR-042, FR-047-FR-052, SC-010, SC-013-SC-015).
- [ ] T043 [US5] Classification: RED — Add sealed-admission/same-process attestation REDs rejecting direct caller submission, unsigned/foreign candidate/run/case/request/process/provider/adapter bindings, altered counters, content-bearing fields, and aggregate-consistent SDK fake chunks in `tests/Vulgata.Tests/ChatStreamObservationProvenanceTests.cs` and `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier3QualificationTests.cs` (FR-039-FR-040, FR-044, FR-060, SC-009, SC-013, SC-015-SC-017).
- [ ] T044 [US5] Classification: GREEN — Implement the Track C-only ephemeral ECDSA P-256 `Story125StreamAttestationSink` with sealed raw-adapter admission, immutable runtime/completion binding, process public-key fingerprint, and signed content-dark export in `src/dotnet/Vulgata.Agents/ChatExecution/Story125StreamAttestationSink.cs`; the same Integration Owner adds its Track C-only registration in `src/dotnet/Vulgata.Agents/DependencyInjection.cs` and `src/dotnet/Vulgata.Web/Program.cs` without changing Track B contracts (FR-039-FR-040, FR-044, FR-051, FR-060, SC-009, SC-013, SC-015-SC-017).
- [ ] T045 [US5] Classification: RED — Add schema/semantic RED vectors for exact ten-check inventory, status-conditioned receipts, immutable supersession, command/observation/artifact closure, sealed signature/admission, distinct received/decoded domains, per-case attempt order, complete corpus ID sets, exact half-up scoring, passed-only screenshots, and all +1/fake/foreign negatives in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs` and `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs` (FR-037-FR-047, FR-060, SC-008-SC-017).
- [ ] T046 [US5] Classification: GREEN — Implement the minimum schema-2.2 semantic validation and recomputation needed by T045 in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`, preserving immutable run directories and never manufacturing missing observations or success values (FR-039-FR-047, FR-060, SC-009-SC-017).
- [ ] T047 [US5] Classification: RED — Add Tier 2 public-harness REDs for unique database ownership before connection, current migrations, neutral Java/XML fixtures, real Git/CodeGraph/JDT LS, controlled provider outage, persisted retry/pause, Vulgata process restart, provider restoration, authorized manual resume, and duplicate Document/Edge/publication queries in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier2PostgreSqlTests.cs` (FR-041, FR-045, FR-047, SC-012, SC-014).
- [ ] T048 [US5] Classification: GREEN — Implement the smallest Tier 2 orchestration, redacted command/hash/artifact capture, database ownership/cleanup receipts, and restart/resume duplicate proof in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier2PostgreSqlTests.cs` and `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`; do not add product design corrections inside the evidence run (FR-039, FR-041, FR-045, FR-047, SC-012-SC-014).
- [ ] T049 [US5] Classification: VERIFY — Execute Tier 2 against one proven-owned `vulgata_story125_*` PostgreSQL database and record an honest passed/failed/blocked envelope plus preflight/ownership/cleanup, exact commands, exit codes, tool/provider identities, pause/restart/resume, and duplicate receipts under `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tier-2/run-01/` (FR-039, FR-041, FR-045, FR-047, SC-012-SC-014).
- [ ] T050 [US5] Classification: RED — Add Tier 3 candidate/topology/real-tool/real-Scan/corpus/restart/screenshot REDs in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier3QualificationTests.cs` for exactly two real Repositories, 50-150 automatic File Code Units, all high-value seeds, bounded ordinary failures, exact Cross-Repository REFERENCES Edge, honest Uncertainty, three Business Logic Documents including a Cross-Repository flow, all corpus cases, streamed accepted answers, citations/leakage/timing, restart continuity, and five screenshot categories (FR-042-FR-046, FR-060, SC-009-SC-017).
- [ ] T051 [US5] Classification: GREEN — Implement only the Tier 3 candidate-bound runner/projection and receipt wiring required by T050 in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier3QualificationTests.cs` and `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`; production failures pin a new candidate after normal TDD/review instead of patching product behavior inside the run (FR-039, FR-042-FR-046, FR-060, SC-009-SC-017).
- [ ] T052 [US5] Classification: VERIFY — Execute the real host Tier 3 run with Vulgata, CodeGraph, Java 21, JDT LS, configured LLM, PostgreSQL 17 as the only required Docker service, the fixed corpus, same-database process restart, actual complete-render observations, and five candidate-bound redacted screenshots; preserve honest non-pass output under `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tier-3/run-01/` (FR-039, FR-042-FR-046, FR-060, SC-009-SC-017).
- [ ] T053 [US5] Classification: VERIFY — Validate the final envelope against `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/story125-release-evidence.schema.json`, recompute every aggregate/signature/reference/hash, verify exact ten checks, all corpus IDs, zero non-streaming accepted answers, 100% accepted technical-claim citations, zero leakage, exact half-up usefulness >=80%, complete render <30 seconds, restart/database ownership, screenshot categories/redaction, `git diff --check`, and candidate hashes; write the final release checkpoint to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-c-final-01.md` only if all checks pass (FR-039-FR-047, FR-060, SC-009-SC-017).

**Checkpoint**: Story 12.5 release completion requires accepted T053 evidence; tests, fake state, planned commands, or a partially successful run are insufficient.

---

## Dependencies and execution order

### Boundary order

1. Phase 1 authority preflight precedes Phase 2 foundational guards.
2. Phase 2 blocks every user-story write; preserved Track A reacceptance can then run independently but must be green before Track C.
3. B1 T008-T013 precedes B2 T014-T018.
4. B1+B2 precede B3 T019-T022.
5. B1-B3 precede B4 T023-T032; T023-T026 must show honest public-seam RED before T027-T029 GREEN.
6. B1-B4 precede B-INT T033-T037; the named B-INT Interaction Test Owner completes T033 before T034, and both REDs precede Integration Owner GREEN T035.
7. B-INT precedes convergence/review/E2E/final verification T038-T041.
8. Track C T042-T053 remains closed until T041 is accepted and a clean candidate is pinned; each Track C RED precedes its paired GREEN.

### Shared-file ownership

- T035 is the sole Track B Integration Owner task for `src/dotnet/Vulgata.Web/Program.cs` and any necessary `src/dotnet/Vulgata.Agents/DependencyInjection.cs` change.
- T044 reuses that same coordinated Integration Owner only after Track B acceptance to add Track C composition.
- T033 and T034 have one B-INT Interaction Test Owner for `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` and `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs`; T034 starts only after T033 completes.
- No task currently authorizes changes to `src/dotnet/Vulgata.Web/Api/ApiEndpointRegistrationExtensions.cs`, any `.csproj`, `Directory.Packages.props`, `VulgataDbContext`, configurations, migrations, model snapshot, or Story 12.4 contracts. Discovery that one is necessary returns to plan.

### Parallel opportunities

- T002 and T003 may run in parallel after T001.
- T009-T011 are independent B1 characterization suites after T008 establishes the topology boundary.
- T015 and T016 are independent B2 characterization suites after T014 fixes the shared-Edge authority.
- T019 and T021 may run in parallel after B2 acceptance.
- T026 may be authored in parallel with T023-T025; all four REDs block T027-T029.
- Track C test authoring may parallelize only where files do not overlap; `Story125ReleaseHarness.cs`, DI/Program, and each run directory have one owner.

---

## Requirement and success-criterion coverage

| Boundary | Functional requirements | Success criteria |
| --- | --- | --- |
| Setup/ownership | FR-001, FR-036, FR-039-FR-040, FR-047-FR-052, FR-054-FR-060 | SC-003, SC-013-SC-017 |
| Preserved Track A | FR-033-FR-040, FR-046-FR-047 | SC-007-SC-008, SC-014 |
| B1 | FR-001-FR-013, FR-021-FR-025 | SC-001-SC-005 |
| B2 | FR-003-FR-004, FR-014-FR-024 | SC-001-SC-005 |
| B3 | FR-003-FR-004, FR-008, FR-023-FR-031 | SC-001, SC-004-SC-006 |
| B4 | FR-025-FR-032, FR-053-FR-060 | SC-004-SC-005, SC-015-SC-017 |
| B-INT/gates | FR-001-FR-032, FR-039-FR-040, FR-048-FR-060 | SC-001-SC-008, SC-013-SC-017 |
| Track C | FR-037-FR-047, FR-060 | SC-008-SC-017 |

All FR-001-FR-060 and SC-001-SC-017 have at least one implementation/reacceptance task and one verification or release-proof owner.

### Architecture-decision coverage

| Boundary | Accepted plan architecture |
| --- | --- |
| Setup/foundational | AD-1, AD-10, AD-11, AD-12 |
| B1 | AD-1, AD-2, AD-2A, AD-2B, AD-5 |
| B2 | AD-2, AD-3, AD-4, AD-5 |
| B3 | AD-1, AD-2B, AD-5, AD-6 |
| B4 | AD-1, AD-7, AD-7B, AD-7C |
| Preserved Track A | AD-8, AD-9 |
| B-INT/gates | AD-1, AD-2, AD-2A, AD-2B, AD-3, AD-4, AD-5, AD-6, AD-7, AD-7B, AD-7C, AD-10, AD-12 |
| Track C | AD-9, AD-10, AD-11, AD-12 |

---

## Task count and implementation strategy

- **53 total tasks**: Setup 2; Foundational 2; preserved Track A 3; B1 6; B2 5; B3 4; B4 10; B-INT 5; convergence/review/E2E/verification preparation 4; Track C 12.
- **Classification**: 17 `[EXISTING-EVIDENCE]`; 10 `[RED]`; 8 `[GREEN]`; 18 `[GATE]`/`[VERIFY]` or checkpoint/evidence tasks.
- **Suggested MVP boundary**: accepted B1+B2+B3+B4 plus B-INT. Track C is required for Story 12.5 release completion but cannot begin before the accepted Track B checkpoint.
- **Test-first rule**: Existing-correct B1-B3/raw-streaming behavior is reaccepted without fabricated RED. Missing B4 publication ownership, B-INT interaction behavior, and Track C attestation/harness behavior use public-seam RED -> minimal GREEN -> focused regression -> checkpoint.

## Self-review

- Every task uses the strict checklist form, a sequential ID, an exact file/directory path, and a story label only inside a user-story phase.
- The task set replaces rather than appends to the invalidated prior 81-task history and contains no continuation-wave task logic.
- B1, B2, B3, B4, and B-INT each have an independent goal, test, exit evidence, and dependency edge.
- The typed single topology, immutable lexical primaries, closed relationship/Flow Group states, many decisions per Edge, packet 2.2, full re-fetch/currentness, strict byte/SSE/JSON/deadline behavior, one Web publication owner, atomic `AcceptedPublicationCommit`, accepted-only reveal, Track B/Track C telemetry separation, and same-candidate release proof are explicit.
- Candidate-only immutable `AppendValidatedTurn`/proposed-state construction is explicitly permitted before the critical section; only activation or replacement of live current Conversation State outside `AcceptedPublicationCommit` is forbidden, and follow-up reads derive Conversation State authority from the committed composite.
- T033 and T034 are sequential shared-file tasks owned by one named B-INT Interaction Test Owner; neither task is marked or described as parallel.
- No EF, Story 12.4, vector/autonomous-tool, fake-evidence, unrestricted-provider-tool, or unvalidated-client-output task is present.
- Track C is dependency-ordered behind accepted B1-B4, B-INT, convergence, review, conditional E2E decision, verification, and candidate pinning.

## Phase 11: Convergence

- [x] T054 [US1] Classification: RED-to-GREEN — Add a public-seam RED vector in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` with two valid accepted decisions sharing one Cross-Repository Edge but Flow Group IDs ordered opposite to the decision-set order, then apply the minimal GREEN correction in `src/dotnet/Vulgata.Infrastructure/Data/ChatRelationshipEvidenceProjector.cs` so the relationship binds the canonical first decision while retaining every decision-owned Published Flow Group per AD-3 (partial; FR-014, FR-019, SC-001-SC-002).

### Quality-review-03 correction ownership and execution order

**Purpose**: Close only `QR-TB-005` through `QR-TB-008` from the independent `REVISE` verdict bound to candidate `a0eda652f01cb7b410a270783128d1eaefbd0ec1`, preserving accepted AD-7B/AD-7C and the existing Track B architecture.

**Shared-file ownership**: The root assigns one named **Track B Quality Correction Test Owner** for all overlapping test paths in T055, T057, T059, and T061 and one named **Track B Quality Correction Production Owner** for all overlapping Web paths in T056, T058, T060, and T062. T055-T062 are strictly serial and are not parallelizable. The existing B-INT Interaction Test Owner retains `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` and `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs`; the correction test owner must coordinate those edits with that owner. `src/dotnet/Vulgata.Web/Program.cs` remains under the sole Integration Owner and is verification-only because the accepted typed scoped registrations already exist; a newly observed need to edit it returns to the root for explicit ownership rather than broadening these tasks.

#### QR-TB-005 — accepted publication cannot be retroactively cancelled

- [x] T055 [US2] Classification: RED — As the Track B Quality Correction Test Owner, add a public page RED that cancels after `AcceptedPublicationCommit` but both before the first progressive-reveal delay and during a later reveal delay, and prove the committed Grounded Answer, complete claims, Document Citations, and follow-up Conversation State remain identical to the visible commit and never become `Cancelled`, in `tests/Vulgata.Tests/ChatPageTests.cs` (QR-TB-005; AD-7B, AD-7C; FR-029, FR-032, FR-058-FR-059; SC-006, SC-016-SC-017).
- [x] T056 [US2] Classification: GREEN — After T055 is observed RED, make the minimum accepted-publication presentation correction in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`: caller cancellation remains authoritative before the owner linearization point, but once the page observes a published `AcceptedPublicationCommit`, cancellation or reveal-delay interruption can only switch that already accepted answer to immediate complete display and cannot route through `ChatTurn.Finish(Cancelled)` or clear its claims/citations; add no second success owner or provider-fragment path (QR-TB-005; AD-7B, AD-7C; FR-031-FR-032, FR-057-FR-059; SC-005-SC-006, SC-016-SC-017).

#### QR-TB-006 — non-throwing owner linearization and owner-authoritative composite

- [x] T057 [US2] Classification: RED — As the Track B Quality Correction Test Owner, add behavioral RED vectors for cancellation before the sole sample, sample exactly at/after deadline, sample strictly before deadline followed by scheduler advance, a `TimeProvider.GetElapsedTime` seam that throws, and concurrent repeated commit calls; prove exactly one terminal exchange, no candidate stranded `Pending`, no post-sample fallible call, and the owner-current committed composite atomically contains answer, proposed Conversation State, completion summary, and reveal directive while the coordinator has no later live-state activation, in `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs` and `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs` (QR-TB-006; AD-7C; FR-031-FR-032, FR-056-FR-057; SC-005-SC-006, SC-017).
- [x] T058 [US2] Classification: GREEN — After T057 is observed RED, apply the minimum owner-boundary correction in `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs` and `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`: precompute every fallible conversion/allocation before the critical section, use only cancellation sampling, one monotonic timestamp, primitive non-throwing deadline/duration arithmetic, primitive writes, and one atomic terminal exchange inside it, make that exchange activate the owner's current committed composite, remove the coordinator's post-commit `_acceptedPublicationCommit` mutation, and derive follow-up/reset authority from the owner; update only the existing owner double required by that public contract in `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs` (QR-TB-006; AD-7C; FR-029, FR-031-FR-032, FR-056-FR-057; SC-005-SC-006, SC-017).

#### QR-TB-007 — reveal cost and safety margin are bound before commit

- [x] T059 [US2] Classification: RED — As the Track B Quality Correction Test Owner, add RED vectors proving the immutable publication candidate binds deterministic Unicode text-element reveal cost and one named fixed non-zero safety margin before commit; require `ImmediateAccepted` when remaining budget equals the schedule cost, is below or equal to schedule cost plus margin, or scheduler lag exhausts the bound, while a strictly fitting progressive schedule exposes only already accepted content and completes within the bound, in `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs`, `tests/Vulgata.Tests/ChatPageTests.cs`, and `tests/Vulgata.Tests/ChatValidatedRevealTests.cs` (QR-TB-007; AD-7B, AD-7C; FR-032, FR-056, FR-059; SC-016-SC-017).
- [x] T060 [US2] Classification: GREEN — After T059 is observed RED, minimally extend the preallocated reveal directive/candidate and coordinator preparation in `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs` and `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` to bind the Unicode schedule cost plus fixed safety margin before the owner sample, and change `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs` to consume that committed directive without recomputing acceptance from wall-clock-only delay cost; choose `ImmediateAccepted` unless the complete progressive schedule strictly fits and retain the live supervisor solely to complete already accepted content on lag (QR-TB-007; AD-7B, AD-7C; FR-032, FR-056, FR-059; SC-016-SC-017).

#### QR-TB-008 — required typed constructor and scoped DI binding

- [x] T061 [US2] Classification: RED — As the Track B Quality Correction Test Owner, replace the weakened constructor-shape expectation with REDs requiring exactly one non-optional `IChatStreamCompletionSummaryRecorder` and one non-optional `IPreparedChatPublicationOwner`, rejecting `object`, nullable/default/fallback binding, direct owner construction, and a null recorder; prove production composition supplies the exact same scoped recorder/owner instances and accepted completion data remains available only through the committed composite, in `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs`, `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`, and `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` (QR-TB-008; AD-7, AD-7C; FR-053, FR-060; B-INT).
- [x] T062 [US2] Classification: GREEN — After T061 is observed RED, change only `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` to require typed `IChatStreamCompletionSummaryRecorder` and `IPreparedChatPublicationOwner` constructor dependencies, remove `object?`, nullable/default parameters, the null recorder, and direct owner fallback, and update direct-construction fixtures—without weakening assertions—in `tests/Vulgata.Tests/ChatPageTests.cs`, `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`, and `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs`; verify the existing `src/dotnet/Vulgata.Web/Program.cs` registration already supplies both exact scoped instances and do not edit it unless the root separately reassigns Integration Owner authority (QR-TB-008; AD-7, AD-7C; FR-051, FR-053, FR-060; B-INT).

#### Correction verification and invalidated gate reruns

- [x] T063 Classification: VERIFY — After T055-T062 are GREEN, run the six affected suites `PreparedChatPublicationOwnerTests`, `PreparedChatCoordinatorTests`, `PreparedChatIntegrationTests`, `ChatPipelineCompositionTests`, `ChatPageTests`, and `ChatValidatedRevealTests`, the authenticated Business/IT initial/follow-up interaction vectors, Web build, `git diff --check`, and no-EF/no-Story-12.4/no-Track-C guards; record exact RED commands, minimal GREEN owned-path diff, focused counts, warnings, and hashes in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-quality-correction-03.md` (QR-TB-005-QR-TB-008; FR-029-FR-032, FR-048-FR-060; SC-005-SC-006, SC-013, SC-015-SC-017).
- [x] T064 Classification: VERIFY — Rerun independent convergence against the unchanged accepted specification/plan plus append-only tasks through T063 and the complete corrected implementation, including the full bounded Track B matrix and mutation/interaction invariants; write the fresh traceable verdict to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/convergence/track-b-quality-correction-01.md`, append further tasks only for a new source-traceable gap, and do not advance quality review on a non-converged result (FR-001-FR-032, FR-048-FR-060; SC-001-SC-006, SC-015-SC-017).
- [x] T065 Classification: VERIFY — Only after T064 reports clean convergence, prepare a verdict-neutral corrected quality input bound to the new candidate, complete `a0eda652..candidate` correction delta, unchanged accepted authority hashes, T063/T064 evidence, exact owned-path manifest, environment exclusions, and no-EF/no-Story-12.4/no-Track-C proof in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/review/track-b-quality-input-02.md`; the implementer/task author must not issue or predict the verdict (FR-039-FR-040, FR-048-FR-060; SC-013, SC-015-SC-017).
- [x] T066 Classification: GATE — Dispatch a fresh independent quality reviewer against the candidate and T065 input; only that reviewer may write `specs/014-grounded-llm-wiki-chat-release-evidence/reviews/quality-review/track-b-quality-review-04.json`, which must explicitly disposition `QR-TB-005` through `QR-TB-008` and review correctness, security, edge cases, architecture seams, tests, regression risk, and repository standards; `REVISE` returns to append-only test-first correction tasks, while `BLOCKED` or `ACCEPT` follows the existing workflow without opening E2E, final verification, or Track C from this task alone (FR-039-FR-040, FR-048-FR-060; SC-013, SC-015-SC-017).

**Phase 11 correction dependency**: `T055 -> T056 -> T057 -> T058 -> T059 -> T060 -> T061 -> T062 -> T063 -> T064 -> T065 -> T066`. No task changes the specification, plan, PRD, BMad authority, Story 12.4, EF persistence, Track A, or Track C. The existing T040/T041 and all T042-T053 remain blocked until a fresh accepted quality verdict and the ordinary downstream gates.

## Phase 12: Convergence

- [x] T067 Classification: VERIFY — Update the stale `ChatStreamObservationProvenanceTests.PreparedChatCoordinator_ComposesPublicationOwnerForCompletionAuthority` characterization to require the accepted non-optional typed `IPreparedChatPublicationOwner` and `IChatStreamCompletionSummaryRecorder` constructor dependencies (not defaulted/fallback binding), then rerun that provenance suite and the bounded Track B matrix so all current constructor and composition assertions agree with T061-T062 (FR-053, FR-060; SC-015-SC-017; B-INT; partial).

## Phase 13: Quality-review-04 correction for QR-TB-009

**Purpose**: Close only `QR-TB-009` from the independent `REVISE` verdict in `specs/014-grounded-llm-wiki-chat-release-evidence/reviews/quality-review/track-b-quality-review-04.json`, using the bounded design in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/review/track-b-quality-correction-plan-02.md`. The accepted specification, plan, AD-6, AD-7B, AD-7C, re-fetch/revalidation behavior, accepted-only reveal, and mandatory streaming remain unchanged.

**Serialized ownership**: The root assigns one named **QR-TB-009 Test Owner** for T068-T071 and one named **QR-TB-009 Production Owner** for T072-T073. T068-T073 are strictly serial and are not parallelizable because they overlap `ChatPageTests.cs`, `PreparedChatPublicationOwnerTests.cs`, `PreparedChatCoordinatorTests.cs`, `ChatPipelineCompositionTests.cs`, `ChatPage.razor.cs`, `PreparedChatPublicationOwner.cs`, and `PreparedChatCoordinator.cs`. The existing B-INT Interaction Test Owner must coordinate the T071 `ChatPipelineCompositionTests.cs` write; no other worker edits these paths concurrently. `Program.cs`, Agents transport, Infrastructure retrieval/currentness, and Story 12.4 paths are verification-only.

### Public behavioral RED

- [x] T068 [US2] Classification: RED — As the QR-TB-009 Test Owner, extend `tests/Vulgata.Tests/ChatPageTests.cs` with a public page flow that accepts one Grounded Answer, invokes `新对话`, asks the next question in the same Business/IT mode and identical System/two-Repository scope, and proves the second `ChatRetrievalRequest.Conversation` has the correct scope but zero turns, prior answers, cited handles, packet fingerprint, or Logical Document hints; retain the existing visible clear/focus assertions and observe the current hidden prior-context behavior as RED (QR-TB-009; AD-6, AD-7C; FR-023, FR-029-FR-030; SC-006).
- [x] T069 [US2] Classification: RED — After T068 is captured, add an in-flight reset race in `tests/Vulgata.Tests/ChatPageTests.cs` and `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs`: hold a request/candidate after it has captured the pre-reset owner generation but before terminal commit, invoke `新对话`, release the old work, then prove it publishes no answer/completion/current Conversation State, cannot reactivate on a repeated delayed commit, does not reappear in the page, and the next same-mode/same-scope retrieval receives empty Conversation State (QR-TB-009; AD-6, AD-7C; FR-029-FR-032, FR-057-FR-059; SC-005-SC-006, SC-016-SC-017).
- [x] T070 [US2] Classification: RED — After T069 is captured, add the progressive-reveal reset race in `tests/Vulgata.Tests/ChatPageTests.cs`: pause a Unicode-safe reveal after `AcceptedPublicationCommit`, invoke `新对话`, release every late reveal continuation, and prove neither text/citations nor the pre-reset committed Conversation State becomes page-visible or owner-current again; the next identical-scope request must receive an empty Conversation State while accepted-only/provider-fragment protections remain intact (QR-TB-009; AD-7B, AD-7C; FR-027, FR-029-FR-030, FR-058-FR-059; SC-004-SC-006, SC-016-SC-017).
- [x] T071 [US2] Classification: RED — After T070 is captured, add owner/coordinator/composition REDs in `tests/Vulgata.Tests/PreparedChatPublicationOwnerTests.cs`, `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`, and `tests/Vulgata.Tests/ChatPipelineCompositionTests.cs` requiring one atomic owner snapshot of `(conversation generation, current published composite)`, an owner-serialized reset that advances generation and clears current state, immutable generation binding on every candidate, and a commit-time same-generation check under the existing single-writer boundary; reject a coordinator-local `PreparedChatPublicationAuthority`, unbound candidate, alternate reset/current-state owner, or any first/repeated/concurrent pre-reset commit that can activate or reactivate after reset (QR-TB-009; AD-6, AD-7C; FR-029-FR-032, FR-056-FR-060; SC-005-SC-006, SC-016-SC-017; B-INT).

### Minimal GREEN

- [x] T072 [US2] Classification: GREEN — After T068-T071 are observed RED, as the QR-TB-009 Production Owner make the minimum generation-fence correction in `src/dotnet/Vulgata.Web/Data/PreparedChatPublicationOwner.cs` and `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`: move current-commit/read/reset authority behind the scoped publication owner, return one atomic generation/current snapshot for request Conversation State, bind every candidate to that generation, serialize reset generation-advance plus current-clear with commit, and fail a stale-generation candidate closed before `AcceptedPublicationCommit` so it can never activate/reactivate; remove the coordinator-local shadow authority while preserving same-scope re-fetch/revalidation, accepted composite contents, cancellation/deadline linearization, typed recorder/owner dependencies, and bounded Chinese outcomes (QR-TB-009; AD-6, AD-7C; FR-003-FR-004, FR-019, FR-029-FR-032, FR-056-FR-060; SC-001, SC-005-SC-006, SC-016-SC-017).
- [x] T073 [US2] Classification: GREEN — After T072 is GREEN, minimally change `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs` so explicit `新对话` invokes the coordinator's owner-backed reset/generation boundary before completing the existing cancellation, page-version, delayed clear, notice, and focus sequence; retain the page version solely as presentation-race protection, preserve accepted-only Unicode/reduced-motion reveal and existing mode/scope behavior, and add no provider-buffer, second success owner, persistence, or browser-session state (QR-TB-009; AD-6, AD-7B, AD-7C; FR-029-FR-032, FR-058-FR-059; SC-005-SC-006, SC-016-SC-017).

### Correction verification and invalidated gate reruns

- [x] T074 Classification: VERIFY — After T068-T073 are GREEN, run `ChatPageTests`, `PreparedChatPublicationOwnerTests`, `PreparedChatCoordinatorTests`, `ChatPipelineCompositionTests`, `ChatValidatedRevealTests`, `PreparedChatIntegrationTests`, and `ChatStreamObservationProvenanceTests`, including authenticated Business/IT initial/follow-up, accepted-turn reset, in-flight reset, progressive-reveal reset, stale-generation repeated/concurrent commit, re-fetch/currentness, mandatory-streaming, and no-second-owner vectors; run the Web build, `git diff --check`, and no-EF/no-Story-12.4/no-Track-C guards, then record exact RED/GREEN commands, counts, warnings, owned diff, and hashes in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-b-quality-correction-04.md` (QR-TB-009; FR-001-FR-032, FR-048-FR-060; SC-001-SC-006, SC-013, SC-015-SC-017).
- [x] T075 Classification: VERIFY — Only after T074 passes, rerun independent convergence against the unchanged accepted specification/plan, append-only tasks through T074, and the complete corrected implementation; require traceable proof that explicit reset now discards owner-current Conversation State, every candidate is generation-bound, stale candidates cannot activate/reactivate, and all prior Track B/B-INT invariants remain green, writing the fresh result to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/convergence/track-b-quality-correction-03.md`; append more tasks only for a new source-traceable gap and do not advance quality review on a non-converged result (QR-TB-009; FR-001-FR-032, FR-048-FR-060; SC-001-SC-006, SC-015-SC-017).
- [x] T076 Classification: VERIFY — Only after T075 reports clean convergence, prepare a verdict-neutral candidate-bound input at `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/review/track-b-quality-input-03.md` containing the unchanged accepted authority hashes, raw QR-TB-009 finding, complete correction-base-to-candidate delta, T074/T075 evidence, exact owned-path manifest, environment exclusions, and no-EF/no-Story-12.4/no-Track-C proof; challenge reset/commit/reveal interleavings and preservation of re-fetch/revalidation, accepted-only reveal, streaming, and scoped DI without issuing or predicting a verdict (FR-039-FR-040, FR-048-FR-060; SC-013, SC-015-SC-017).
- [x] T077 Classification: GATE — Dispatch a fresh independent quality reviewer against the converged candidate and T076 input; only that reviewer may write `specs/014-grounded-llm-wiki-chat-release-evidence/reviews/quality-review/track-b-quality-review-05.json`, which must explicitly disposition `QR-TB-009` and review the complete current boundary for correctness, security, reset/commit/reveal races, architecture seams, tests, regression risk, and repository standards. `REVISE` returns to new append-only test-first correction tasks; `BLOCKED` or `ACCEPT` follows the existing workflow without this task itself opening E2E, final Track B verification, T040/T041, or Track C (FR-039-FR-040, FR-048-FR-060; SC-013, SC-015-SC-017).

**Phase 13 dependency**: `T068 -> T069 -> T070 -> T071 -> T072 -> T073 -> T074 -> T075 -> T076 -> T077`. No task changes the specification, plan, PRD, BMad authority, Story 12.4, EF persistence, Track A, provider transport, Infrastructure retrieval/currentness, or Track C. This is the minimum accepted-scope implementation correction and does not require a new planned-change or implementation authorization.

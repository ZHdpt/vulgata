---
description: "Dependency-ordered Story 12.5 implementation tasks"
---

# Tasks: Grounded LLM-Wiki Chat & August 30 Release Evidence

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, and the contracts under `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/`.

**Authority**: Generated from accepted plan SHA-256 `9b84e94ea2bb564b8a08b521023ab449ae7c5731e52cce057d5e63bad2c02b6a`, plan-review-09 SHA-256 `fabe3fe5baa1f29025479746862c3fda36df4b2e81d02f7f0baa29a519ae015c`, and planned-change authorization-03 scope SHA-256 `f3057c1106d59d96a804b6a7c8902df653b6c23e4deb7402759c0ffd71a84f16`. Track A is completed evidence. Track B requires the accepted Story 12.4 commit `4798b97d9f23aef587829175df7ab7ebc3daf995` and a new implementation approval. Track C is closed until Track B is accepted.

**Boundaries**: This is a bounded delta over Prepared Chat. Do not add an EF migration, change `VulgataDbContext` or its snapshot, mutate Story 12.4 contracts, use embeddings/vector search/autonomous loops, or use fake Backup B for release evidence. `Program.cs`, DI registrations, project files, and central endpoint registration have one coordinated owner. Track B records neutral runtime observations; only Track C creates signed release attestation.

**TDD rule**: For each incomplete behavior, write and run the named public-seam RED before the smallest GREEN implementation. Existing-correct behavior is characterization-only and is not fabricated as RED. Every slice ends with focused and surrounding regression verification.

## Phase 1: Setup and lifecycle-safe preflight

- [x] T001 Pin `specs/014-grounded-llm-wiki-chat-release-evidence/` with `check-prerequisites.ps1 -RequireTasks -IncludeTasks`, record branch `demo0831/story-12-5`, common base `0d835c5f65f70a3c6a9561c6ff600069bb93e832`, clean index, accepted Story 12.4 commit/checkpoint, and worktree ownership in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/preflight/README.md`.
- [x] T002 Add the no-migration/no-`VulgataDbContext`/no-model-snapshot, Story 12.4 non-overlap, shared-file ownership, and database-isolation guard tests in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs`.
- [x] T003 [P] Add Track A, Track B, and Track C entry-gate assertions and approval/checkpoint templates in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/gates/README.md`.
- [x] T004 Add disposable lowercase `vulgata_story125_*` database-name preflight and forbidden-target rejection coverage in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier2PostgreSqlTests.cs`.

## Phase 2: Foundational contracts, corpus, and ownership

- [x] T005 Add positive/negative JSON contract tests for `contracts/chat-evidence-packet-v2.schema.json`, `story125-evaluation-case.schema.json`, and `story125-release-evidence.schema.json` in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs`.
- [x] T006 Add corpus schema, count, acyclic-follow-up, no-prescribed-answer, and source-reference RED coverage in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs` before the fixture exists.
- [x] T007 Create the source-traceable 12–20 question corpus covering Chinese Business/IT, Cross-Repository flow, business rules, technical traces, failures, follow-ups, insufficient evidence, and authorization-negative cases in `tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json`.
- [x] T008 [P] Add the shared-file owner map and command/provider/artifact identity manifest template in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/ownership/README.md`.
- [x] T009 [P] Characterize the existing neutral Java/XML fixture provenance in `tests/Vulgata.Tests/Fixtures/PreparedScanning/fixture-manifest.json` without duplicating or overwriting fixture files.

## Phase 3: Track A — production Scan status and evidence preparation (completed)

**Existing/accepted evidence**: Production Scan status projection/UI, Chinese state behavior, manual resume, fake Backup B exclusion, corpus/evidence schemas, scoring, and release-gate foundations are already implemented and checkpointed. These tasks remain as truthful completed evidence; do not rerun independent Track A review unless later changes alter Track A behavior.

- [x] T010 [P] [US3] Characterize persisted preparation/indexing/running/retry/paused/resumed/completed/warning/integrity-failure states, counts, retry, bounded reasons, failed-file summaries, authorization, and fake-state exclusion in `tests/Vulgata.Tests/ProductionScanStatusReaderTests.cs`.
- [x] T011 [US3] Implement the bounded production status projection in `src/dotnet/Vulgata.Core/DomainServices/ProductionScanStatusContracts.cs` and `src/dotnet/Vulgata.Infrastructure/Data/ProductionScanStatusReader.cs` with `EvidenceEligibility=ProductionPersisted`.
- [x] T012 [P] [US3] Characterize Chinese Scan UI labels, counts, warnings, authorization, and `继续扫描` in `tests/Vulgata.Tests/ProductionScanPageTests.cs`.
- [x] T013 [US3] Implement real-state management UI in `src/dotnet/Vulgata.Web/Data/ProductionScanStatusCoordinator.cs`, `src/dotnet/Vulgata.Web/Components/Pages/Management/ScanHistoryPage.razor`, `ScanExecutionPage.razor`, and `ScanExecutionPage.razor.css`, using durable manual resume only.
- [x] T014 [US3] Verify resume re-read, stale/hidden execution, and fake Backup B exclusion in `tests/Vulgata.Tests/DurableScanning/ManualResumeApiTests.cs` and `tests/Vulgata.Tests/ProductionScanStatusReaderTests.cs`.
- [x] T015 [US4] Characterize semantic/adversarial status-conditioned release envelopes, passed-only receipts, fixed checks/screenshots, content-dark fields, provenance, leakage, and failure vectors in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs` and `Story125EvaluationContractTests.cs`.
- [x] T016 [US4] Implement the schema/semantic evidence validator and cross-artifact consistency checks in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`.
- [x] T017 [US4] Add citation coverage, authorization leakage, usefulness, timing, and `providerDisposition` scoring tests in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs`.
- [x] T018 [US4] Implement scoring and source-traceable corpus/evidence preparation in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`, `Story125EvaluationContractTests.cs`, and `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/evaluation/README.md`.
- [x] T019 [US4] Verify Track A database preflight/fake-state rejection and coordinated registration of status reader/coordinator in `src/dotnet/Vulgata.Web/Program.cs` with focused tests before Track B.

## Phase 4: Track B1 — typed lexical projection and sole packet path (Priority: P1)

**Goal**: Establish one typed pipeline, with lexical projection as the sole primary authority and one packet builder. The old literal-question classifier, exact-row rebuild, projection-first early return, duplicate packet assembly, and direct projector construction are deletion targets.

**Independent test**: Equivalent wording and relationship fixture mutations produce identical lexical primary Document/section/Source Anchor identities; structural tests prove no alternate packet path remains.

- [x] T020 [US1] Add the architectural interaction RED in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` and `ChatEvidenceCurrentnessTests.cs`: a Cross-Repository-literal question and neutral equivalent must share lexical primaries, and adding/removing/staling relationship facts must not replace those primaries.
- [x] T021 [P] [US1] Add deterministic section-retrieval RED vectors in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` and `tests/Vulgata.Tests/ChatSupplementaryEvidenceSelectorTests.cs` for normalized symbol/endpoint/path/title/heading tokens, Business/IT terms, prose fallback, stable ties, integer score components `50/40/30/25/20/5/3/1`, and malformed metadata fail-closed behavior; separately characterize the existing provider-candidate ranking contract in `tests/Vulgata.Tests/ChatCandidateRankerTests.cs` without conflating its count-based claim ranking with section retrieval.
- [x] T022 [US1] Add exact bound RED vectors for the ranked candidate window of 256, five Documents, twelve primary sections or exhaustion, four sections per Document, strongest-per-Document ordering, 32 Source Anchors, and packet evidence limits in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs` and `ChatEvidencePacketBuilderTests.cs`.
- [x] T023 [US1] Add a structural RED that fails while the obsolete classifier/legacy authority/early return/alternate builder path exists, naming the exact seams in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs`.
- [x] T024 [US1] Add the four immutable typed handoffs `AuthorizedChatSnapshot`, `ChatLexicalPrimaryProjection`, `ChatRelationshipExpansionProjection`, and `ChatEvidenceAssembly`, plus the closed stage-result union `Succeeded|InsufficientEvidence|NotFoundOrUnauthorized|InconsistentCurrentEvidence`, in the exact Core contracts `src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceRetriever.cs` and `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs`; no intermediate stage may return a final packet.
- [x] T025 [US1] Implement the deterministic lexical projection and exact ranking/candidate bounds in `src/dotnet/Vulgata.Infrastructure/Data/ChatSupplementaryEvidenceSelector.cs` and the typed integration seam in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`, preserving current authorization/currentness and existing-correct ranker behavior.
- [x] T026 [US1] Delete the literal-question classifier, `ExactRelationshipRow` rebuild, projection-first early return, legacy `ExactCrossRepositoryEdgeEvidence` authority, duplicate packet construction, and direct projector construction from `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`; make the structural RED pass without changing Story 12.4 contracts.
- [x] T027 [US1] Add packet-v2 RED vectors for opaque handles, complete sections, Source Anchors, relationship/flow metadata, canonical fingerprint, exact/+1 UTF-8 evidence bounds, whole-unit packing, and no raw database/provider access in `tests/Vulgata.Tests/ChatEvidencePacketBuilderTests.cs`.
- [x] T028 [US1] Implement `ChatEvidencePacketBuilder` and its Core request seam in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidencePacketBuilder.cs` and `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs`; bind all ordered packet fields including titles/headings/descriptions, and drop optional complete units or refuse required units without truncation.
- [x] T029 [US1] Add the packet-2.2 candidate-extractor RED/characterization vectors in `tests/Vulgata.Tests/ChatCandidateExtractorTests.cs`: Cross-Repository REFERENCES support must use selected decision, complete decision count, and decision-set fingerprint authority, while a legacy `ProfileFingerprint`-only relationship is rejected; preserve and regression-run the unchanged embedded Business/IT prompt catalog through `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs`, `src/dotnet/Vulgata.Agents/ChatExecution/ChatPromptCatalog.cs`, `src/dotnet/Vulgata.Agents/Prompts/chat-business.v1.md`, and `src/dotnet/Vulgata.Agents/Prompts/chat-it.v1.md`.
- [x] T030 [US1] Implement the minimal packet-2.2 candidate extraction GREEN in `src/dotnet/Vulgata.Agents/ChatExecution/ChatCandidateExtractor.cs`, consuming selected decision/count/set authority and never assuming legacy `ProfileFingerprint`; keep provider-candidate ranking separate from section retrieval and make T021 characterization plus T029 vectors pass.
- [x] T031 [US1] Add the server-owned advisory Context RED vectors at the dedicated public seam `tests/Vulgata.Tests/ChatAdvisoryContextReaderTests.cs` for applied Global/System/exact-two Repository Context, authorization/currentness, pending/caller exclusion, canonical ordering, blank/nonmatching behavior, and exact 8,192/+1 UTF-8 bounds.
- [x] T032 [US1] Add `IChatAdvisoryContextReader` at the exact Core path `src/dotnet/Vulgata.Core/DomainServices/IChatAdvisoryContextReader.cs` and implement it at the exact Infrastructure path `src/dotnet/Vulgata.Infrastructure/Data/ChatAdvisoryContextReader.cs`, binding applied-context authority and fingerprinting; use advisory terms only for the bounded Business Logic summary fallback.
- [x] T033 [US1] Wire the four-stage B1 pipeline and dedicated advisory reader through `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs` and the coordinated owners `src/dotnet/Vulgata.Agents/DependencyInjection.cs`/`src/dotnet/Vulgata.Web/Program.cs`; prove composition and focused B1 regression before B2.

## Phase 5: Track B2 — current-v2 relationship expansion and currentness (Priority: P1)

**Goal**: Add one-hop current persisted REFERENCES/GENERATED_FROM and exact Cross-Repository evidence without changing lexical primaries. Preserve every valid decision for one Edge and each decision-owned Published complete Flow Group.

**Independent test**: Shared-Edge fixtures with two independently valid decisions produce one relationship with selected decision, count `2`, stable full-set fingerprint, and two decision-ranked Flow Groups; invalid or mutated support fails closed according to the closed state table.

- [x] T034 [US1] Add the B2 architectural RED for two decisions/one current directed Edge/two decision-owned Published Flow Groups and for post-selection decision-set mutation in `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs`.
- [x] T035 [US1] Add relationship/Flow Group state-table RED vectors for `absent`, `present-valid`, `present-inconsistent`, and `selected-required`, including cycles, duplicate slots, stale/mismatched endpoints/revisions, unpublished/incomplete groups, unavailable Source Anchors, and authorization loss in `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs`.
- [x] T036 [US1] Add deterministic expansion RED vectors for first `min(3,count)` seeds, at most two related Documents per seed, one hop, 20 total sections, 32 relationships, 12 ordered flow steps, allowlisted REFERENCES/GENERATED_FROM direction, deduplication, and canonical ordering in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs`.
- [x] T037 [US1] Implement current-v2 relationship authority in `src/dotnet/Vulgata.Infrastructure/Data/ChatRelationshipEvidenceProjector.cs` and `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs`, accepting current `DocumentEdge`, exact accepted decisions, current `GENERATED_FROM`, and Published complete Flow Groups only; legacy evidence is never sufficient.
- [x] T038 [US1] Implement the one-hop expansion handoff in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs` and `src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceCurrentnessValidator.cs`, preserving immutable B1 `PrimaryDocumentIds`, `PrimarySectionIds`, ranks, and Source Anchors.
- [x] T039 [US1] Extend packet-v2 binding in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidencePacketBuilder.cs` to include selected decision, complete decision count/set fingerprint, exact Edge identity, decision-owned Flow Group authorities, relationship depth, and complete-unit packing; run B1+B2 focused and surrounding regression.

## Phase 6: Track B3 — bounded transient conversation and validated synthesis (Priority: P1)

**Goal**: Support two follow-ups using transient, bounded context that is re-fetched and revalidated; validate complete answers before any reveal.

**Independent test**: Initial question plus two follow-ups preserve scope/mode, use previous citations only as hints, re-fetch current evidence, evict the fourth turn, and reject stale/unauthorized evidence or unsupported whole answers.

- [x] T040 [P] [US2] Add RED conversation vectors for initial plus two follow-ups, fourth-turn eviction, 4 KiB bound, scope/mode reset, prior-citation boost, re-fetch, authorization/currentness mutation, and restart non-persistence in `tests/Vulgata.Tests/ChatConversationStateTests.cs`.
- [x] T041 [US2] Implement transient `ChatConversationState` and validated-turn contracts in `src/dotnet/Vulgata.Core/DomainServices/ChatConversationContracts.cs`; retain only the last two questions/answers, cited handles, scope, and mode.
- [x] T042 [US2] Integrate re-fetch-only conversation hints, one shared deadline, currentness revalidation, bounded Chinese outcomes, and no stale-result reuse in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`.
- [x] T043 [P] [US2] Add RED claim/citation/security vectors for handle/Source Anchor coverage, fingerprint binding, whole-answer semantic rejection, missing-equivalent failure, one syntax repair only, and no semantic repair in `tests/Vulgata.Tests/ChatAgentResultValidatorTests.cs` and `PreparedChatCoordinatorTests.cs`.
- [x] T044 [US2] Implement provider-independent complete-result validation in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentResultValidator.cs` and coordinator integration in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`; no partial claim, citation, handle, relationship, or Source Anchor may cross the client boundary.

## Phase 7: Track B4 — mandatory raw streaming, whole-answer barrier, and accepted-only reveal (Priority: P1)

**Goal**: Every primary and optional syntax-repair response uses strict raw provider streaming. Received wire domains are enforced before JSON decoding, while decoded safety limits remain separate; only the fully validated Grounded Answer is revealed.

**Independent test**: Arbitrary reads, split UTF-8, malformed framing, terminal errors, exact/+1 received caps, deadline/inactivity, cancellation, and validation failures yield zero partial client output; valid streams render Unicode-safe accepted text.

- [x] T045 [P] [US2] Add RED arbitrary-read vectors in `tests/Vulgata.Tests/ChatProviderStreamAssemblerTests.cs` for strict split UTF-8, CR/LF/CRLF SSE, JSON escapes/surrogates, one terminal plus physical EOF, post-terminal data, cancellation, inactivity, and malformed/truncated/overlong UTF-8.
- [x] T046 [P] [US2] Add received-byte RED vectors in `tests/Vulgata.Tests/OpenAiCompatibleChatProviderStreamingTransportTests.cs` for one nonterminal SSE `data` event as one received Provider Chunk at 16,384/16,385 bytes, assembled received response at 65,536/65,537 bytes before JSON, escape-heavy/metadata-heavy payloads, zero-output failure, and separate raw-body/SSE/decoded-delta/final-answer caps.
- [x] T047 [US2] Add RED composition/provenance vectors in `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs` and `PreparedChatCoordinatorTests.cs` proving primary and one syntax repair use the dedicated stream, non-streaming cannot be accepted, one 29-second deadline covers all stages, inactivity is ten seconds, and repair is limited to three seconds.
- [x] T048 [US2] Add RED client-barrier/reveal vectors in `tests/Vulgata.Tests/ChatValidatedRevealTests.cs` and `ChatPageTests.cs` proving no provider fragment or evidence detail renders before validation, accepted Unicode text-element reveal, reduced-motion immediate render, and existing render-timed scrolling.
- [x] T049 [US2] Implement staged strict stream assembly and closed failure ownership in `src/dotnet/Vulgata.Agents/ChatExecution/ChatProviderStreamAssembler.cs`, `IChatProviderStreamingTransport.cs`, and `src/dotnet/Vulgata.Core/DomainServices/ChatStreamingContracts.cs`, with distinct raw-read/body, SSE, received-chunk, assembled-received, decoded-delta, final-answer, terminal, and deadline counters.
- [x] T050 [US2] Implement the production raw streaming adapter and neutral content-dark `ChatStreamRuntimeObservation` in `src/dotnet/Vulgata.Agents/ChatExecution/OpenAiCompatibleChatProviderStreamingTransport.cs` and `ChatStreamObservationAuthority.cs`; bind transport-only `RuntimeObservationId` and exclude Track C candidate/signature/export fields.
- [x] T051 [US2] Implement streaming primary/repair dispatch and shared budget propagation in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs`, `src/dotnet/Vulgata.Core/DomainServices/IChatAgentDispatcher.cs`, and `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`, with coordinated registration in `src/dotnet/Vulgata.Agents/DependencyInjection.cs` and `src/dotnet/Vulgata.Web/Program.cs`.
- [x] T052 [US2] Implement accepted-only Unicode-safe reveal and remaining-budget behavior in `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`, `ChatPage.razor.cs`, and `src/dotnet/Vulgata.Web/Components/Pages/ChatTurn.cs`; preserve Chinese Business/IT behavior and `OnAfterRenderAsync` scrolling.
- [x] T053 [US2] Run the B4 focused stream/validator/UI suite, Web build, and full Tier 1 regression; record Track B neutral runtime observations separately from any future Track C attestation.

## Phase 8: Track C — final release verification (closed until separate approval)

**Hard gate**: These tasks are not implementable or runnable until T020–T053 are accepted, Track B quality review/convergence/E2E decision/verification pass, the Track B checkpoint is bound, and a separate Track C approval is recorded. Track C must use real provider/host execution and a unique disposable `vulgata_story125_*` database; fake Backup B state and synthetic provider state cannot support release evidence.

- [ ] T054 [US5] After the Track C gate, add RED Tier 2 PostgreSQL restart/resume/outage/duplicate-prevention and ownership-receipt tests in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier2PostgreSqlTests.cs`.
- [ ] T055 [US5] After the Track C gate, implement Tier 2 real-tool orchestration and redacted command/artifact collection in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier2PostgreSqlTests.cs`.
- [ ] T056 [US5] After the Track C gate, add RED provenance and production-composition tests in `tests/Vulgata.Tests/ChatStreamObservationProvenanceTests.cs` and `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier3QualificationTests.cs` proving direct caller and aggregate-consistent SDK fake-chunk rejection, sealed raw-adapter admission, same-process identity, and no content-bearing observation.
- [ ] T057 [US5] After the Track C gate, implement `Story125StreamAttestationSink` with sealed-admission signing, immutable provenance binding, and exact per-domain aggregate recomputation in `src/dotnet/Vulgata.Agents/ChatExecution/Story125StreamAttestationSink.cs`; coordinate registration only through `src/dotnet/Vulgata.Agents/DependencyInjection.cs` and `src/dotnet/Vulgata.Web/Program.cs`.
- [ ] T058 [US5] After the Track C gate, add RED Tier 3 candidate/topology/tool/database preflight and forbidden-target tests in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier3QualificationTests.cs`; downstream GREEN/evidence owner is T059 in the same test file.
- [ ] T059 [US5] After the Track C gate, implement candidate-bound preflight, fixed ten-check runner, same-candidate artifact/hash binding, status-conditioned receipts, and sealed observation projection in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier3QualificationTests.cs` and `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`.
- [ ] T060 [US5] After the Track C gate, add RED real Scan/grounded Chat/restart/screenshot checks in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier3QualificationTests.cs` and evidence assertions in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs` for two Repositories, 50–150 File Code Units, exact Cross-Repository REFERENCES, honest Uncertainty, three Business Logic Documents including a Cross-Repository flow, corpus usefulness/citation/leakage/timing, and zero non-streaming accepted answers; downstream GREEN/evidence owners are T061 and T062.
- [ ] T061 [US5] After the Track C gate, execute the conditional E2E matrix and real host release run using Vulgata, CodeGraph, Java 21, JDT LS, PostgreSQL 17, and configured LLM; project T060 results into `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs` and record honest failed/blocked/not-run outcomes without fake evidence.
- [ ] T062 [US5] After the Track C gate, perform final schema/hash/diff/reproducibility and screenshot review against `contracts/story125-release-evidence.schema.json`, `quickstart.md`, and the candidate-bound `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/release/` manifest; create the Track C release checkpoint only if every required check passes.

## Dependencies and execution order

- Track A T001–T019 is completed evidence and remains preserved.
- Track B requires the accepted Story 12.4 completion commit/checkpoint and a fresh Track B implementation approval. B1 T020–T033 precedes B2 T034–T039; B2 precedes B3 T040–T044; B3 precedes B4 T045–T053.
- Architectural REDs T020/T023 and the shared-Edge RED T034 must be observed before their replacement production writes. No convergence task may substitute for these interaction tests.
- Track C T054–T062 is closed until the Track B checkpoint and separate Track C approval. No task here authorizes Track C early execution.
- Within each slice: RED tests → minimal GREEN → focused regression → surrounding regression → coherent checkpoint. Shared `Program.cs`, DI, project, and endpoint files require the named coordinated owner.

## Parallel execution opportunities

- T003, T008, and T009 are independent documentation/fixture-preparation work.
- Within B1, T021, T022, T027, T029, and T031 can be authored in parallel after the architectural RED boundary is agreed; implementation remains ordered through T024–T033.
- Within B2, T035 and T036 can be authored in parallel after T034; projector and retriever integration remains sequential.
- Within B3, T040 and T043 are independent RED suites.
- Within B4, T045–T048 are independent RED suites over separate seams; T049–T052 remain ordered by transport → dispatch → reveal.
- Track C tasks are parallelizable only after its mandatory gate and only with explicit ownership of the release harness and evidence directory.

## Implementation strategy

1. Preserve and regression-check completed Track A.
2. Implement B1 as the smallest architectural cutover: typed lexical projection, one packet path, advisory Context, and deletion of the obsolete path.
3. Add B2 current-v2 relationship expansion without changing lexical primary identities.
4. Add B3 transient conversation and complete-answer validation.
5. Add B4 mandatory raw streaming and accepted-only reveal; stop for convergence, quality review, E2E decision, verification, and Track B checkpoint.
6. Start Track C only after a separate approval and run real Tier 2/Tier 3 evidence collection.

## Self-review

- Source references reviewed against `spec.md`, accepted `plan.md`, `data-model.md`, `quickstart.md`, all contracts, Story 12.4 dependency contracts, and observed source/test topology.
- Every incomplete accepted behavior has a public-seam RED before GREEN; existing-correct Track A and characterization behavior is not fabricated as RED.
- B1–B4 order, deletion inventory, many-decisions-per-Edge authority, decision-owned Published Flow Groups, advisory Context ownership, packet 2.2 binding, deterministic constants, received pre-JSON byte domains, neutral Track B telemetry, and Track C signed attestation are represented.
- No placeholder task IDs, guessed paths, EF migration, Story 12.4 mutation, vector/autonomous retrieval, fake release evidence, or early Track C execution is authorized.

## Phase 9: Convergence

**Goal**: Close the remaining Track B deadline-propagation proof without changing the accepted streaming architecture or opening Track C.

- [x] T063 [US2] Add fake-clock behavioral RED in `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs` proving that exactly ten seconds without a non-empty decoded data chunk or valid terminal cancels the actual underlying provider stream and returns `TimedOut` with no accepted result, plus exact 2,999/3,000 ms remaining-budget vectors proving syntax repair is refused below and permitted at the accepted threshold; implement the minimal cancellation propagation correction in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs`, then run focused dispatcher/coordinator/transport regression. Traces to FR-056, FR-057, plan AD-7, and T047 (`partial`). Track C remains closed until this task passes TDD, convergence reruns cleanly, and all remaining Track B gates bind a complete checkpoint.

## Phase 10: Convergence

**Goal**: Close the remaining accepted Track B contract cutovers without opening Track C or changing Story 12.4/persistence authority.

- [x] T064 [US1] Replace the 257-row refusal characterization with a public-seam RED proving any larger eligible current set is fully scored/deduplicated and only the highest-ranked 256 distinct sections are retained, then implement the bounded top-K lexical window without changing deterministic ranking or primary identities. Traces to FR-007, FR-009, plan AD-2, and T022/T025 (`contradicts`).
- [x] T065 [US1] Add schema-conformance and fingerprint REDs for the single accepted `grounded-chat-evidence/2.2` packet shape, then cut `EvidencePacket`, `ChatEvidencePacketBuildRequest`, retrieval assembly, serialization, validation, and complete-unit packing over to the required selection policy/primary/seed identities, advisory-context authority fingerprint, relationship `selectedExactDecisionId` plus decision-set authority, multiple decision-owned `flowGroupHandles`, flow-group handles/relationship binding/decision rank, flow-step group binding, and conversation `priorLogicalDocumentIds`; remove the parallel 2.0 runtime shape and prove the complete canonical 2.2 packet against `contracts/chat-evidence-packet-v2.schema.json`. Traces to FR-021 through FR-025, plan AD-5, and T028/T039 (`contradicts`).
- [x] T066 [US2] Add composition/leakage REDs for release-neutral Track B `ChatStreamRuntimeObservation` and separate coordinator-owned `ChatStreamCompletionSummary`, then remove candidate/run/case/public-key/signature/export and post-transport validation coupling from the Track B stream request/authority while retaining content-dark transport counters and immutable runtime/completion IDs; keep sealed admission and signed candidate attestation exclusively behind the still-closed Track C sink. Traces to FR-060, plan AD-7/AD-7B/AD-10, and T050/T051/T053 (`contradicts`).

## Phase 11: Convergence

**Goal**: Close remaining accepted Track B architecture and currentness gaps without opening Track C or changing Story 12.4/persistence authority.

- [x] T067 [US1] Complete the exact `AuthorizedChatSnapshot` -> `ChatLexicalPrimaryProjection` -> `ChatRelationshipExpansionProjection` -> `ChatEvidenceAssembly` handoff in `ChatEvidenceRetriever`, make the relationship projector and packet builder constructor-composed dependencies, and remove the retriever's direct projector fallback/alternate untyped construction; add public composition coverage proving one typed lexical -> relationship -> packet path. Traces to FR-007, FR-014, FR-021, plan AD-2/AD-2A, and T024/T033 (`partial`).
- [x] T068 [US1] Add public-seam REDs for relationship candidates beyond the current query cap and for Business/IT relationship preference, then rank and deduplicate the complete eligible edge set by seed rank, mode preference, repository/target identity, and edge identity before applying relationship and per-seed bounds. Traces to FR-015 through FR-018, plan AD-4, and T036/T038 (`partial`).
- [x] T069 [US1] Add a currentness RED with 11 through 256 valid exact decisions sharing one current Cross-Repository Edge, then load and canonicalize the complete bounded decision set rather than rejecting more than ten rows, preserving count/set-fingerprint and decision-owned Flow Group validation. Traces to FR-014, FR-019, FR-020, plan AD-3, and T037/T039 (`partial`).
- [x] T070 [US1] Add an IT-mode advisory-fallback RED and apply the same bounded current Business Logic summary plus server-owned advisory-term fallback when primary lexical matching is empty in either mode, without changing positive primary identities or allowing caller/pending Context. Traces to FR-012, FR-013, plan AD-2/AD-2B, and T025/T032 (`partial`).
- [x] T071 [US2] Add multi-unit packet REDs proving over-bound packing drops the lowest-ranked optional relationship/Flow Group or lexical extra unit first, then preserve complete-unit reachability and required-unit refusal; carry explicit rank through the build request instead of sorting optional handles lexicographically. Traces to FR-022 through FR-024, plan AD-5, and T028/T039 (`partial`).
- [x] T072 [US2] Add a render-barrier RED that mutates applied Global/System/selected-Repository advisory Context or its authority fingerprint after retrieval, then re-read and revalidate the same server-owned Context before reveal and withhold the answer on any value, membership, ordering, currentness, or applied/pending change. Traces to FR-019, FR-024, FR-027, plan AD-2B/AD-7B, and T032/T042/T044 (`partial`).

## Phase 12: Convergence

**Goal**: Enforce the accepted typed retrieval handoff at runtime without changing Story 12.4 authority, persistence, or the closed Track C boundary.

- [x] T073 [US1] Complete the runtime `AuthorizedChatSnapshot` -> `ChatLexicalPrimaryProjection` -> `ChatRelationshipExpansionProjection` -> `ChatEvidenceAssembly` handoff: construct and pass the immutable authorized snapshot into lexical selection, preserve the lexical projection as the sole primary authority, wrap the final packet request in `ChatEvidenceAssembly`, and make the sole packet builder consume that typed assembly; add structural/public-seam tests proving retrieval cannot bypass the typed stages with direct request/repository arguments or a raw build request. Traces to FR-007, FR-014, FR-021, plan AD-2/AD-2A, and T067 (`partial`).

## Phase 13: Convergence

**Goal**: Close remaining substantive Track B authority and per-Edge currentness gaps without opening Track C or changing Story 12.4 contracts.

- [ ] T074 [US1] Move applied advisory-context acquisition into the server-owned `ChatEvidenceRetriever` serializable transaction: constructor-compose `IChatAdvisoryContextReader`, create the `AuthorizedChatScope` only after authorization, ignore caller-supplied advisory text, bind the reader result into `AuthorizedChatSnapshot`, and preserve closed missing/unauthorized/inconsistent outcomes; remove any duplicate pre-transaction advisory authority or add a race-focused seam proving the snapshot and packet use the same applied Context. Traces to FR-004, FR-007, FR-012, FR-019, plan AD-2/AD-2B, and T032/T073 (`partial`).
- [ ] T075 [US1] Rework `ChatEvidenceCurrentnessValidator` exact-decision and contribution loading to enforce the accepted 1-256 decision bound independently for each included Cross-Repository Edge, preserving complete per-Edge decision-set fingerprints and decision-owned Flow Group validation; add a multi-Edge public-seam RED proving that valid decision sets across separate Edges are not falsely rejected by one global `.Take(257)`/contribution cap. Traces to FR-014, FR-019, FR-020, plan AD-3, and T069 (`partial`).

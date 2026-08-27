---
description: "Dependency-ordered Story 12.5 implementation tasks"
---

# Tasks: Grounded LLM-Wiki Chat & August 30 Release Evidence

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, and all contracts under `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/`.

**Authority**: Task generation is authorized by planned-change scope `7581b411951e52fa08dd979bcc3b6a286645c1373ff60657044d851448fda05c` only. Track A remains behind implementation approval. Track B requires an accepted Story 12.4 final checkpoint and its final commit integrated into this branch. Track C requires accepted Track B.

**TDD rule**: For each behavioral slice, write the named RED/characterization test first, verify the test is meaningful, then implement the smallest GREEN change and run focused plus surrounding regression tests. Existing-correct behavior is characterization-only and is never described as fabricated RED.

## Phase 1: Setup and gate-safe preflight

- [ ] T001 Pin the supported per-worktree context with executable PowerShell `$previousFeatureDirectory = $env:SPECIFY_FEATURE_DIRECTORY; try { $env:SPECIFY_FEATURE_DIRECTORY = 'specs/014-grounded-llm-wiki-chat-release-evidence'; $expectedFeatureDirectory = (Resolve-Path -LiteralPath 'specs/014-grounded-llm-wiki-chat-release-evidence').Path; $prerequisites = (& .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks | ConvertFrom-Json); if ((Resolve-Path -LiteralPath $prerequisites.FEATURE_DIR).Path -ne $expectedFeatureDirectory) { throw \"FEATURE_DIR must resolve to feature 014\" } } finally { if ($null -eq $previousFeatureDirectory) { Remove-Item Env:SPECIFY_FEATURE_DIRECTORY -ErrorAction SilentlyContinue } else { $env:SPECIFY_FEATURE_DIRECTORY = $previousFeatureDirectory } }` before any implementation command; record branch `demo0831/story-12-5`, base `0d835c5f65f70a3c6a9561c6ff600069bb93e832`, current HEAD, clean index, and Story 12.4 non-overlap in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/preflight/README.md`.
- [ ] T002 Add one explicit C# no-migration/no-DbContext-delta guard test in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs` covering `VulgataDbContext`, migrations, model snapshot, Story 12.4 paths, and shared-file ownership.
- [ ] T003 [P] Add Track A/B/C entry-gate assertions and implementation-approval evidence template in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/gates/README.md`.
- [ ] T004 Add a disposable database-name validator test in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier2PostgreSqlTests.cs` requiring unique lowercase `vulgata_story125_*` before product connection and rejecting development/Story 12.4 qualification databases.

## Phase 2: Foundational contracts, fixtures, and owners

- [ ] T005 Add schema loading and positive/negative JSON contract tests for `specs/014-grounded-llm-wiki-chat-release-evidence/contracts/chat-evidence-packet-v2.schema.json`, `story125-evaluation-case.schema.json`, and `story125-release-evidence.schema.json` in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs`.
- [ ] T006 Add corpus schema/count/acyclic-follow-up/no-expected-answer RED tests in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs`, before the fixture exists, and verify the failure is caused by the absent fixture/schema rather than a malformed test.
- [ ] T007 Create the source-traceable 12–20 case corpus, including Business/IT, cross-Repository flow, failure/decision, follow-up, insufficient-evidence, and authorization-negative cases, in `tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json`.
- [ ] T008 [P] Add shared-file owner map and command/provider/artifact identity manifest template in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/ownership/README.md`.
- [ ] T009 [P] Reuse the exact neutral Java/XML fixtures `tests/Vulgata.Tests/Fixtures/PreparedScanning/repository/src/main/java/neutral/app/OrderGateway.java`, `tests/Vulgata.Tests/Fixtures/PreparedScanning/repository/src/main/resources/neutral/communication.xml`, and `tests/Vulgata.Tests/Fixtures/PreparedScanning/repository/vendor/neutral/vendor.xml`; characterize/update only their existing manifest boundary at `tests/Vulgata.Tests/Fixtures/PreparedScanning/fixture-manifest.json`, recording provenance without duplicating or overwriting the fixtures.

## Phase 3: User Story 3 — Real persisted Scan state and resume (Priority: P1, Track A)

**Independent test**: With approved Track A implementation, persisted Scan states render in Chinese, authorize only recoverable manual resume, and never accept fake Backup B state.

- [ ] T010 [P] [US3] Add RED/characterization coverage for every persisted Scan phase, counts, retry, bounded reason, failed-file summary, missing-equivalent behavior, and fake-state exclusion in `tests/Vulgata.Tests/ProductionScanStatusReaderTests.cs`.
- [ ] T011 [US3] Implement the bounded production status projection in `src/dotnet/Vulgata.Core/DomainServices/ProductionScanStatusContracts.cs` and `src/dotnet/Vulgata.Infrastructure/Data/ProductionScanStatusReader.cs`, preserving authorization/currentness and `EvidenceEligibility=ProductionPersisted`.
- [ ] T012 [P] [US3] Add RED UI component coverage for Chinese state labels, counts, warning/failure summaries, authorization, and `继续扫描` in `tests/Vulgata.Tests/ProductionScanPageTests.cs`.
- [ ] T013 [US3] Implement the real-state management UI in `src/dotnet/Vulgata.Web/Data/ProductionScanStatusCoordinator.cs`, `src/dotnet/Vulgata.Web/Components/Pages/Management/ScanHistoryPage.razor`, `src/dotnet/Vulgata.Web/Components/Pages/Management/ScanExecutionPage.razor`, and `src/dotnet/Vulgata.Web/Components/Pages/Management/ScanExecutionPage.razor.css`, using existing durable manual resume only.
- [ ] T014 [US3] Add focused regression coverage for resume re-read, stale/hidden execution, and fake Backup B exclusion in `tests/Vulgata.Tests/DurableScanning/ManualResumeApiTests.cs` and `tests/Vulgata.Tests/ProductionScanStatusReaderTests.cs`.

## Phase 4: User Story 4 — Repeatable evaluation and evidence preparation (Priority: P2, Track A)

**Independent test**: The corpus and evidence tooling validate source references, status-conditioned results, bounded fields, exact scoring, and database isolation without executing Track B/C production work.

- [ ] T015 [US4] Add RED semantic and adversarial tests for status-conditioned envelopes, passed-only receipts, immutable supersession, fixed ten-check inventory, five screenshot categories, exact half-up scoring, content-dark fields, altered/foreign/unsigned stream provenance, fake aggregate chunks, omitted/duplicate/reordered attempts, terminal/outcome/repair/deadline mismatch, content leakage, and non-passed success claims in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs` and `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs`; include an explicit cross-artifact consistency RED comparing the accepted byte domains/caps and literal `transportOutcome`, `rejectionCategory`, and `validationOutcome` vocabularies across `specs/014-grounded-llm-wiki-chat-release-evidence/plan.md`, `data-model.md`, `contracts/chat-provider-streaming-v1.md`, `contracts/release-evidence-contract.md`, and `contracts/story125-release-evidence.schema.json`, and verify these fail before the validator/runner exists.
- [ ] T016 [US4] Implement schema/semantic evidence validation and the cross-artifact consistency GREEN in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`, with exact command/artifact/case/observation bindings and no inferred observations; make the T015 vectors pass.
- [ ] T017 [US4] Add evaluation scoring tests for citation coverage, authorization leakage, usefulness, timing, and `providerDisposition` in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs`.
- [ ] T018 [US4] Implement source-traceable corpus/evidence preparation tooling in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125EvaluationContractTests.cs` and `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/evaluation/README.md`, making T006/T007 and the corpus contract pass.
- [ ] T019 [US4] Add database preflight and fake Backup B rejection integration coverage in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs`, then GREEN-register `ProductionScanStatusReader` and `ProductionScanStatusCoordinator` through the single coordinated Track A owner in `src/dotnet/Vulgata.Web/Program.cs` after T011/T013, with focused composition coverage in `tests/Vulgata.Tests/ProductionScanStatusReaderTests.cs` and `tests/Vulgata.Tests/ProductionScanPageTests.cs`, before the Track B gate; do not touch Agents `DependencyInjection.cs` unless Track A actually requires it under the accepted plan.

## Phase 5: User Stories 1–2 — Current v2 retrieval, grounded synthesis, and bounded conversation (Priority: P1, Track B)

**Hard prerequisite**: Do not start T020–T033 until the accepted Story 12.4 final checkpoint is recorded and its final commit is integrated into this branch. Compare accepted exact-resolution, `DocumentEdges`, `GENERATED_FROM`, and published `FlowGroups` contracts first; block with `BLOCKED_STORY_12_4_CONTRACT` rather than changing them. Mandatory Story 12.5 implementation approval is also required.

**Independent test**: On the integrated accepted dependency, authorized Business/IT questions produce deterministic current packets and independently validated answers; legacy-only exact-edge evidence and all invalid transport/semantic states fail closed.

- [ ] T020 [P] [US1] [US2] Add RED retrieval/ranking vectors for exact symbol/endpoint/path/title/heading precedence, mode terms, prose/fallback, stable ties, strongest-per-Document packing, and all exact/+1 bounds in `tests/Vulgata.Tests/ChatEvidenceRetrievalTests.cs`, `tests/Vulgata.Tests/ChatSupplementaryEvidenceSelectorTests.cs`, and `tests/Vulgata.Tests/ChatCandidateRankerTests.cs`.
- [ ] T021 [US1] [US2] Implement current IndexEntry/DocumentSection projection and the single deterministic lexical ranking authority in `src/dotnet/Vulgata.Infrastructure/Data/ChatSupplementaryEvidenceSelector.cs`, integrate it through `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs` and `src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceRetriever.cs`, and retain `src/dotnet/Vulgata.Agents/ChatExecution/ChatCandidateRanker.cs` only for its existing mode/candidate responsibility; remove legacy ExactCrossRepositoryEdgeEvidence as authority.
- [ ] T022 [P] [US1] Add RED relationship vectors for accepted v2 exact decision plus directed current `DocumentEdge`, REFERENCES/GENERATED_FROM allowlist, one-hop seed/related limits, cycles, stale/mismatched endpoints, and complete Published FlowGroups in `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs`.
- [ ] T023 [US1] Implement v2 relationship proof and one-hop expansion by integrating `src/dotnet/Vulgata.Infrastructure/Data/ChatRelationshipEvidenceProjector.cs` into `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceRetriever.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidenceCurrentnessValidator.cs`, and the Core contract `src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceCurrentnessValidator.cs`, preserving authorization, currentness, revision, direction, and FlowGroup completeness.
- [ ] T024 [P] [US1] Add RED packet contract vectors for packet-v2 handles, Source Anchors, relationships, flow steps, canonical fingerprint, UTF-8 byte limits, complete-unit packing, and no raw graph/provider access in `tests/Vulgata.Tests/ChatEvidencePacketBuilderTests.cs`.
- [ ] T025 [US1] Implement packet-v2 construction and canonical fingerprinting in `src/dotnet/Vulgata.Infrastructure/Data/ChatEvidencePacketBuilder.cs` and `src/dotnet/Vulgata.Core/DomainServices/ChatContracts.cs` without persistence changes.
- [ ] T026 [P] [US2] Add RED conversation tests for initial plus two follow-ups, fourth-turn eviction, scope/mode/reset behavior, 4 KiB bound, prior-citation boost, re-fetch, and authorization/currentness mutation barriers in `tests/Vulgata.Tests/ChatConversationStateTests.cs`.
- [ ] T027 [US2] Implement bounded transient conversation state and re-fetch-only context in `src/dotnet/Vulgata.Core/DomainServices/ChatConversationContracts.cs` and `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`.
- [ ] T028 [US1] [US2] Add RED synthesis/citation/security tests for claim-to-handle/Source Anchor coverage, whole-answer validation barrier, missing-equivalent failure, and no semantic repair in `tests/Vulgata.Tests/ChatAgentResultValidatorTests.cs` and `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`; extend the appropriate pre-GREEN coverage in `tests/Vulgata.Tests/ChatEvidenceCurrentnessTests.cs`, `tests/Vulgata.Tests/ChatCandidateExtractorTests.cs`, and `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs` for currentness, candidate extraction, prompt selection, and the provider-independent result boundary. Existing-correct validator/candidate/prompt behavior remains characterization-only.
- [ ] T029 [US1] [US2] Integrate current packet, relationship, conversation, authorization, and currentness seams in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`; GREEN the result/currentness/candidate/prompt owners in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentResultValidator.cs`, `src/dotnet/Vulgata.Core/DomainServices/IChatEvidenceCurrentnessValidator.cs`, `src/dotnet/Vulgata.Agents/ChatExecution/ChatCandidateExtractor.cs`, `src/dotnet/Vulgata.Agents/ChatExecution/ChatPromptCatalog.cs`, `src/dotnet/Vulgata.Agents/Prompts/chat-business.v1.md`, and `src/dotnet/Vulgata.Agents/Prompts/chat-it.v1.md` without creating a second architecture. Use the exact coordinated DI/endpoint owner files (`src/dotnet/Vulgata.Agents/DependencyInjection.cs`, `src/dotnet/Vulgata.Web/Program.cs`) and make no endpoint change unless separately plan-authorized.

## Phase 6: User Story 2 — Mandatory provider streaming and accepted-only reveal (Priority: P1, Track B)

- [ ] T030 [P] [US2] Add RED arbitrary-read streaming vectors covering strict split UTF-8, CR/LF/CRLF, SSE fields/events, JSON tokens/escapes/surrogate pairs, terminal/EOF, post-terminal data, and exact/+1 raw/SSE/delta/final caps in `tests/Vulgata.Tests/ChatProviderStreamAssemblerTests.cs`.
- [ ] T031 [US2] Implement the staged strict stream assembler and failure ownership in `src/dotnet/Vulgata.Agents/ChatExecution/ChatProviderStreamAssembler.cs`, `src/dotnet/Vulgata.Agents/ChatExecution/IChatProviderStreamingTransport.cs`, and `src/dotnet/Vulgata.Core/DomainServices/ChatStreamingContracts.cs`, enforcing 16 KiB decoded chunk, 64 KiB final, raw/SSE safety caps, strict UTF-8, one terminal, EOF, cancellation, inactivity, and overall deadlines.
- [ ] T032 [P] [US2] Add RED production-composition and provenance tests proving primary and one syntax repair use the raw-adapter streaming transport, non-streaming cannot be accepted, signed content-dark receipts bind candidate/case/request/provider/`stream=true`/headers/read/EOF, and aggregate-consistent SDK fake chunks fail in `tests/Vulgata.Tests/OpenAiCompatibleChatProviderStreamingTransportTests.cs` and `tests/Vulgata.Tests/ChatStreamObservationProvenanceTests.cs`.
- [ ] T033 [US2] Implement the production OpenAI-compatible raw streaming adapter and ephemeral observation authority in `src/dotnet/Vulgata.Agents/ChatExecution/OpenAiCompatibleChatProviderStreamingTransport.cs` and `src/dotnet/Vulgata.Agents/ChatExecution/ChatStreamObservationAuthority.cs`; register it after the types exist through the single coordinated owner in `src/dotnet/Vulgata.Agents/DependencyInjection.cs` plus `src/dotnet/Vulgata.Web/Program.cs`.
- [ ] T034 [US2] Add RED coordinator timing/repair/cancellation tests for one 29-second deadline, ten-second decoded-data inactivity, three-second repair window, one syntax repair only, and no semantic repair in `tests/Vulgata.Tests/ChatAgentDispatcherTests.cs` and `tests/Vulgata.Tests/PreparedChatCoordinatorTests.cs`.
- [ ] T035 [US2] Implement primary/repair streaming dispatch and shared deadline propagation in `src/dotnet/Vulgata.Agents/ChatExecution/ChatAgentDispatcher.cs`, `src/dotnet/Vulgata.Core/DomainServices/IChatAgentDispatcher.cs`, `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`, and `src/dotnet/Vulgata.Web/Program.cs` through the coordinated integration owner.
- [ ] T036 [P] [US2] Add RED browser-boundary tests proving zero provider fragments/claims/citations/handles/relationships/Source Anchors before complete validation and insufficient-remaining-deadline immediate rendering in `tests/Vulgata.Tests/ChatValidatedRevealTests.cs` and `tests/Vulgata.Tests/ChatPageTests.cs`; add characterization-only assertions for existing Unicode text-element reveal, reduced-motion behavior, and render-timed scrolling.
- [ ] T037 [US2] Implement only the minimal coordinator/UI deadline propagation needed for insufficient-remaining-deadline immediate rendering in `src/dotnet/Vulgata.Web/Data/PreparedChatCoordinator.cs`, `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor`, `src/dotnet/Vulgata.Web/Components/Pages/ChatPage.razor.cs`, and `src/dotnet/Vulgata.Web/Components/Pages/ChatTurn.cs`; preserve existing post-validation reveal and `OnAfterRenderAsync` scrolling, with no provider stream crossing the client boundary.

## Phase 7: User Story 5 — Tier 2 and Tier 3 release verification (Priority: P1, Track C)

**Hard prerequisite**: T020–T037 are accepted, Story 12.4 remains integrated and accepted, and Track B implementation/review gates pass. Do not run a real-LLM release claim from fake Backup B or synthetic provider state.

**Independent test**: A single candidate-bound, disposable-PostgreSQL evidence set proves real analysis, grounded streaming Chat, restart continuity, and release artifacts with honest failures and limitations.

- [ ] T038 [US5] Add RED Tier 2 harness tests for clean `vulgata_story125_*` PostgreSQL, controlled provider outage, persisted pause/retry, process restart, manual resume, duplicate prevention, and ownership receipts in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier2PostgreSqlTests.cs`.
- [ ] T039 [US5] Implement Tier 2 orchestration and redacted command/artifact collection in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier2PostgreSqlTests.cs` using real Git/CodeGraph/JDT LS and neutral fixtures.
- [ ] T040 [US5] Add RED Tier 3 candidate/topology/tool/database preflight tests for one candidate, accepted Story 12.4 commit, PostgreSQL 17 only, two real Repositories, 50–150 automatic File Code Units, and forbidden database/path rejection in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier3QualificationTests.cs`.
- [ ] T041 [US5] Implement Tier 3 preflight and candidate-bound raw-adapter observation binding in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier3QualificationTests.cs`.
- [ ] T042 [US5] After T040 and T041, add RED Tier 3 Scan/Chat/restart checks for high-value seeds, failure budget, exact Edge, honest Uncertainty, three Business Logic Documents including a Cross-Repository flow, 12–20 cases, >=80% usefulness, 100% accepted-claim citation, zero leakage, all accepted answers streaming and under 30 seconds in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125Tier3QualificationTests.cs`; T043 is the minimal GREEN runner owner.
- [ ] T043 [US5] Implement the fixed ten-check release runner and recomputation of Scan, relationship, Chat, stream, restart, database, and scoring receipts in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`.
- [ ] T044 [P] [US5] Add RED screenshot tests for real Scan, Business answer, IT answer, restart, and release summary categories, candidate binding, hashes, redaction, and fake Backup B exclusion in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs`.
- [ ] T045 [US5] Implement candidate-bound screenshot capture/indexing and content-dark stream receipt export in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`.

## Phase 8: Final verification and release integrity

- [ ] T046 Add final adversarial regression vectors for any newly discovered binding or artifact-integrity edge case in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarnessTests.cs`; principal validator/runner vectors are owned by the pre-GREEN T015 task and must not be deferred here.
- [ ] T047 Run focused Track A/B/C test matrices after T046, including the final adversarial regression vectors, and record commands, versions, durations, warnings, and errors in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/tier-results.md`.
- [ ] T048 Run `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`, full `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj`, schema/semantic validation, `git diff --check`, path/database/provider identity checks, and receipt cross-checks; record output in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/final-verification.md`.
- [ ] T049 Review all accepted behaviors against the retained Prepared Chat characterization tests in `tests/Vulgata.Tests/PreparedChatIntegrationTests.cs`, `ChatContractTests.cs`, `ChatEvidenceCurrentnessTests.cs`, `ChatPageTests.cs`, and `ChatTurnTests.cs`.
- [ ] T050 Verify final artifact hashes, candidate/dependency commits, no Story 12.4 overlap, no migration/DbContext/snapshot change, one disposable database per run, ten-check inventory, five screenshot categories, and explicit limitations in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/README.md`.

## Dependencies and execution order

```text
T001–T009 -> T010–T019 (Track A)
T001–T009 + accepted Story 12.4 final checkpoint + integrated final commit + implementation approval
  -> T020–T037 (Track B)
T020–T037 accepted -> T038–T045 (Track C)
T010–T045 -> T046–T050
```

Parallel opportunities are limited to `[P]` tasks with disjoint files: contract/corpus/preflight preparation; independent RED vectors within a track; and final evidence/adversarial checks after implementation. `Program.cs`, DI, project files, central endpoint registration, `VulgataDbContext`, migrations, model snapshot, and Story 12.4 contracts have explicit single-owner or no-touch rules.

## Traceability matrix

| Requirements | Tasks |
|---|---|
| FR-001–FR-013 | T020–T021, T024–T029, T036–T037 |
| FR-014–FR-020 | T022–T023 |
| FR-021–FR-024 | T024–T025 |
| FR-025–FR-032 | T028–T035 |
| FR-033–FR-036 | T010–T014, T044–T045 |
| FR-037–FR-040 | T005–T008, T015–T019, T043, T046 |
| FR-041–FR-047 | T002–T004, T038–T045, T048–T050 |
| FR-048–FR-052 | T001–T004, T008, T020, T040, T050 |
| FR-053–FR-060 | T030–T037, T042–T046, T048–T050 |
| SC-001–SC-006 | T020–T029, T034–T037 |
| SC-007–SC-009 | T010–T019 |
| SC-010–SC-014 | T038–T050 |
| SC-015–SC-017 | T030–T037, T042–T050 |

## Implementation strategy

Track A is the preparation and real persisted Scan/UI increment. Track B is the bounded current-v2 retrieval and mandatory streaming Chat increment, only after the accepted dependency and implementation gate. Track C is the release proof increment, only after Track B acceptance. No task authorizes an EF migration, Story 12.4 contract change, fake-state release claim, vector search, autonomous loop, or production write before the mandatory implementation approval.

## Author self-review

PASS. Fifty tasks use the required checkbox/ID/optional-parallel/story-label format and exact repository paths. All FR-001–FR-060 and SC-001–SC-017 are mapped; streaming requirements explicitly cover raw/SSE/JSON/decoded/final domains, strict UTF-8, terminal/EOF, liveness, provenance, and accepted-only reveal. Behavioral RED tasks precede minimal GREEN tasks; existing behavior is characterized rather than fabricated as RED. Track A/B/C boundaries, Story 12.4 checkpoint/commit prerequisites, implementation approval, no-migration rule, shared-file owners, disposable database guard, fake Backup B exclusion, ten checks, five screenshots, and final validation are explicit. No placeholders or unaccepted contract changes are introduced.

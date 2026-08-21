---
description: "Dependency-ordered implementation tasks for Story 12.3"
---

# Tasks: Resumable File Pass 1 & Code Logic Documents

**Feature**: `specs/012-resumable-file-pass1-code-logic`
**Authority boundary**: accepted plan hash `f913da57b120faa9a64e21d380b08159d2bdfcab9b3f5e37e60dfe7cbdca0c41`; planned-change authorization `b422424248a47e1bfda677ca7362dd12addc4861873f173edd4579ecb3d3199a`; base `9d763845b311e7b5e8ae23fa78c24631233f9840`.
**Execution rule**: every behavior task is a public-seam RED task followed by its sole-owner minimal GREEN task. Test authors own only test paths; implementers own only production paths. No task authorizes Story 12.4/12.5 work.

## Phase 1: Setup and authority inventory

**Purpose**: establish bounded fixtures, source ownership, and qualification inputs without changing runtime behavior.

- [x] T001 [P] Record the accepted Story 12.1/12.2 seam inventory, exact manifest-pair fixtures, runtime identities, and forbidden-scope vocabulary in `specs/012-resumable-file-pass1-code-logic/implementation-notes.md` (FR-035/036/053-056, AD-3/13).
- [x] T002 [P] Add deterministic neutral Java/XML pair fixtures and approved two-manifest fixture builders under `tests/Vulgata.Tests/PreparedPass1/Fixtures/` without changing production or accepted Story 12.1/12.2 fixtures (FR-001/002/036, SC-001/012).
- [x] T003 Create the externalized v2 Worker prompt in `src/dotnet/Vulgata.Agents/Prompts/pass1-code-logic-worker.v2.md` and register that exact file as an embedded resource in `src/dotnet/Vulgata.Agents/Vulgata.Agents.csproj`; verify the project builds with the resource present before any dispatcher/provider task starts, and never hardcode the prompt (FR-021-FR-030/054, SC-005/006, AD-3/6/7).

## Phase 2: Foundational persistence and canonical contracts

**Purpose**: create the additive domain/persistence vocabulary that all five vertical slices consume. No user-story implementation begins until this phase is green.

- [x] T004 [P] [RED] Write independent canonical identity vectors and reconstruction tests in `tests/Vulgata.Tests/PreparedPass1/Pass1CanonicalEnvelopeTests.cs` for pair, concurrency change, job, navigation receipt, observation, projection, anchor, edge, and commit identities (FR-004/007/008/014/020/028/032/034/041/045/047/050/051, SC-001/004/005/007/010/011, AD-1/2/5/6/7/8/11).
- [x] T005 Implement the sole canonical codec and envelope contracts in `src/dotnet/Vulgata.Core/DomainServices/PreparedPass1CanonicalCodec.cs` and `src/dotnet/Vulgata.Core/DomainServices/PreparedPass1Contracts.cs`; preserve Story 12.1/12.2 codecs and pass T004 (FR-004/007/008/014/020/028/032/034/041/045/047/050/051, AD-1/2/5/6/7/8/11).
- [x] T006 [RED] Write the pre-production public/domain-seam RED in `tests/Vulgata.Tests/PreparedPass1/Pass1MigrationTests.cs` for new `ScanExecution` and `TwoRepositoryScanCoordination` transitions, version/time guards, terminal immutability, warning-free `Complete`, `FailedPass1`/`Pass1Policy`, legacy-state preservation, and the additive migration lifecycle contract (FR-038/042/044/047/049/056, SC-006/008/009, AD-12).
- [x] T007 Implement additive entities and state transitions in `src/dotnet/Vulgata.Core/Entities/Pass1PairExecution.cs`, `Pass1ConcurrencyChange.cs`, `Pass1NavigationBudget.cs`, `Pass1NavigationReceipt.cs`, `Pass1Outcome.cs`, `ScanExecution.cs`, `ScanExecutionPhase.cs`, `TwoRepositoryScanCoordination.cs`, and `SourceAnchor.cs` (FR-035/038/039/040/041/042/043/044/047/048/049/056, AD-10/11/12/13).
- [x] T008 Add sole-owner EF configurations and the additive `AddResumablePass1CodeLogic` migration plus model snapshot in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/Pass1*.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/DocumentVersionConfiguration.cs`, `SourceAnchorConfiguration.cs`, `ScanExecutionConfiguration.cs`, `TwoRepositoryScanCoordinationConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/`, and `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`; prove PostgreSQL apply/rollback/reapply and v1/v2 coexistence without backfill (FR-021/025/031/032/035/044/056, SC-006/007, AD-12).

**Checkpoint**: canonical identity, additive schema, and model lifecycle are independently testable; no worker, navigation, document, recovery, or reconciliation consumer may precede T005/T007/T008.

## Phase 3: User Story 1 — Dispatch the Approved Pair Deterministically (P1, MVP)

**Goal**: consume exactly one approved two-Repository Prepared Scan Manifest pair and create a complete, stable, pair-wide job set with shared concurrency.
**Independent test**: T009/T011/T013/T015 RED suites are observed failing before T010/T012/T014/T016 GREEN; pair-start PostgreSQL fault injection proves complete-or-none visibility and ten completion schedules preserve identity/order.

- [x] T009 [P] [US1] Add public-seam pair-start RED tests in `tests/Vulgata.Tests/PreparedPass1/Pass1PairStartTests.cs` for exact two-manifest intake, authorization/coordination/currentness/mismatch rejection, complete job-set creation, Story 12.1 job identity reuse, and exact start replay/conflict (FR-001-FR-005/035/036/056, SC-001/002, AD-1/3/12).
- [x] T010 [US1] Implement the sole pair-start GREEN path in `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs` through `src/dotnet/Vulgata.Core/DomainServices/IPreparedPass1Store.cs`; reconstruct, validate, and atomically persist both manifests, jobs, budgets, and pair identity before claims (FR-001-FR-005/035/036/056, SC-001/002, AD-1/3).
- [x] T011 [P] [US1] Add shared-capacity/order RED tests in `tests/Vulgata.Tests/PreparedPass1/Pass1ConcurrencyAndOrderingTests.cs` and `Pass1ConcurrencyUpdateTests.cs` for default two, limits one-through-four, pair ordinal claims, Manifest Ordinal display, races, expiry, active leases, versioned updates, exact replay, stale writers, and ten completion permutations (FR-006-FR-008, SC-001, AD-2).
- [x] T012 [US1] Implement sole-owner pair-lock claim/update/order behavior in `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs` and `src/dotnet/Vulgata.Core/DomainServices/IPreparedPass1Store.cs`; preserve leases, initial-limit replay, effective-limit audit, canonical Pair Selection Ordinal, and Repository-local Manifest Ordinal (FR-006-FR-008/035/044, SC-001, AD-2).
- [x] T013 [P] [US1] Add dispatcher packet/dispatch-loop RED tests in `tests/Vulgata.Tests/PreparedPass1/PreparedPass1WorkerDispatcherTests.cs` for exactly one job per prepared File Code Unit, bound snapshot/classification projection, no extra enumeration, shared capacity, deterministic completion-independent packet identity, and no navigation scheduling (FR-004/007/009/017/036, SC-001/003, AD-3/4).
- [x] T014 [US1] Implement the sole prepared dispatch owner in `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1WorkerDispatcher.cs`, `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1ResultParser.cs`, `src/dotnet/Vulgata.Core/DomainServices/IPreparedPass1WorkerDispatcher.cs`, and `src/dotnet/Vulgata.Web/Data/PreparedPass1DispatchWorker.cs`; consume the already-embedded prompt from T003, build server-owned v2 packets and one job loop without modifying legacy dispatcher/result paths (FR-004/007/009/017/035/036/054/056, SC-001/003/012, AD-3).
- [x] T015 [US1] Add pair-start PostgreSQL atomicity RED tests in `tests/Vulgata.Tests/PreparedPass1/Pass1PairPostgreSqlAtomicTests.cs` with failure injection before/after every start write boundary and competing-claimer visibility checks (FR-005/006, SC-002, AD-1/2).
- [x] T016 [US1] Complete the sole PostgreSQL start/claim transaction implementation in `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs` and validate T015 against PostgreSQL 17, retaining zero partial queue visibility and no required Docker service beyond PostgreSQL (FR-005/006/053, SC-002/012, AD-1/2).

## Phase 4: User Story 2 — Navigate Only Bounded Authorized Evidence (P1)

**Goal**: expose exactly one closed, revision-bound `navigate_code` facade and a bounded Worker loop.
**Independent test**: all eight allowed operations, direct-document/source fallback, refusal receipts, depth/call/result/byte limits, forbidden-access nondisclosure, and no scheduling pass the focused suites.

- [x] T017 [P] [US2] Write navigation public-seam RED tests in `tests/Vulgata.Tests/PreparedPass1/NavigateCodeContractTests.cs` for all eight operations, current-document preference, exact source fallback, stable target identity, one-hop/depth-three, 20-result, 65,536-byte, eight-call, duplicate/cycle, and metadata-only refusal behavior (FR-010-FR-016/019/020, SC-003/004, AD-4/5).
- [x] T018 [US2] Implement the bounded navigation GREEN service in `src/dotnet/Vulgata.Core/DomainServices/IPass1NavigationService.cs` and `src/dotnet/Vulgata.Infrastructure/Analysis/PreparedNavigation/PreparedPass1NavigationService.cs`; bind Repository/Scan Run/Profiled Revision/hash/segment scope, prefer authorized current Documents, otherwise return immediate bounded typed evidence (FR-010-FR-016/019/020, AD-4/5).
- [x] T019 [P] [US2] Write navigation security RED tests in `tests/Vulgata.Tests/PreparedPass1/NavigateCodeSecurityTests.cs` for cross-Repository/stale/hash/authorization/raw LSP/CodeGraph/storage/CodeGraphContext/shell/network/filesystem/host-path/write/scheduling attempts and indistinguishable absence (FR-011/017/018/054, SC-003, AD-4/5).
- [x] T020 [US2] Harden the sole navigation composition in `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedNavigateCodeFunction.cs` and `src/dotnet/Vulgata.Web/Program.cs`; consume the T003 embedded prompt/resource registration, register exactly one closed AIFunction, and expose no forbidden tool/provider path (FR-010/017/018/053/054, SC-003/012, AD-4).
- [x] T021 [P] [US2] Write bounded provider-loop RED tests in `tests/Vulgata.Tests/PreparedPass1/PreparedPass1WorkerDispatcherTests.cs` for ten provider turns, eight tool attempts, durable remaining navigation budget, valid/invalid/repeated calls, schema-valid deferred fallback, and no ninth attempt (FR-014-FR-016/021/028/040, SC-004/009, AD-5).
- [x] T022 [US2] Implement the minimal bounded Worker loop and receipt persistence in `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1WorkerDispatcher.cs`, `PreparedNavigateCodeFunction.cs`, `PreparedPass1ResultParser.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs`; preserve budget across retry/restart and release no evidence on refusal (FR-014-FR-017/040/044, SC-004/009, AD-5).
- [x] T023 [US2] Run post-GREEN real typed-tool qualification in `tests/Vulgata.Tests/PreparedPass1/NavigateCodeRealToolQualificationTests.cs` after T003, T014, T018, and T020; verify pinned CodeGraph 1.5.0, Java 21, JDT LS stdio, supported Java/XML operations, unsupported combinations, and sanitized source fallback without editing production code (FR-010-FR-019/053/054, SC-003/012, AD-4).

## Phase 5: User Story 3 — Commit One Authoritative Code Logic Document (P1)

**Goal**: validate closed `code-logic/file-v2` JSON and atomically persist one immutable Document Version plus deterministic projections.
**Independent test**: valid fixtures commit once; every specified mutation commits no graph rows; projections reproduce byte-identically and exact replay is zero-write.

- [x] T024 [P] [US3] Write schema/semantic RED tests in `tests/Vulgata.Tests/PreparedPass1/CodeLogicFileV2SchemaTests.cs` for strict JSON, unsafe/unknown/duplicate fields, byte/depth bounds, identities, ranges, anchors, classifications, references, deferred observations, and claim-anchor requirements (FR-021-FR-028/031/033/034, SC-004/006, AD-6/8).
- [x] T025 [US3] Implement strict result parsing/validation in `src/dotnet/Vulgata.Core/DomainServices/CodeLogicFileV2Contracts.cs`, `src/dotnet/Vulgata.Core/DomainServices/PreparedPass1CanonicalCodec.cs`, and `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1ResultParser.cs`; consume the externalized prompt created and embedded by T003, accept only server-owned closed v2 JSON, and never accept Worker projections/IDs (FR-021-FR-029/033, AD-6/8).
- [x] T026 [P] [US3] Write deterministic projection RED/golden tests in `tests/Vulgata.Tests/PreparedPass1/CodeLogicFileV2ProjectionTests.cs` for canonical Markdown, up to nine/group Sections, Index Entry bounds, Source Anchor/Edge ordering, commit fingerprint, and no claim absent from JSON (FR-029-FR-031, SC-005, AD-7).
- [x] T027 [US3] Implement the pure projector in `src/dotnet/Vulgata.Core/DomainServices/CodeLogicFileV2Projector.cs`; derive all Markdown, Sections, Index Entry, anchors, and direct same-Repository edge proposals without LLM authorship or completion-order input (FR-029-FR-031, AD-7).
- [x] T028 [P] [US3] Write atomic commit/replay RED tests in `tests/Vulgata.Tests/PreparedPass1/Pass1DocumentCommitTests.cs` for every identity/authorization/hash/range/schema/bound/reference/classification/receipt/anchor mutation, one immutable Version Chain owner, exact lost acknowledgement replay, changed replay conflict, and zero partial writes (FR-032-FR-034/056, SC-006/007, AD-8/9/12).
- [x] T029 [US3] Implement the sole document commit port in `src/dotnet/Vulgata.Core/DomainServices/IPass1CodeLogicDocumentCommitter.cs` and `src/dotnet/Vulgata.Infrastructure/Data/Pass1CodeLogicDocumentCommitter.cs`; revalidate all bindings and atomically persist Logical Document, Document Version, ContentJson, projections, anchors, same-repository edge proposals, and completed job (FR-032-FR-034/056, AD-8/9).
- [x] T030 [P] [US3] Write PostgreSQL document/job atomicity RED tests in `tests/Vulgata.Tests/PreparedPass1/Pass1DocumentPostgreSqlAtomicTests.cs` with failures before/after every write, concurrent commit, stale lease, cancellation, replay, Version Chain, and scalar reconstruction (FR-032-FR-034/043/044, SC-006/007, AD-9).
- [x] T031 [US3] Complete the PostgreSQL transaction and exact replay GREEN implementation in `src/dotnet/Vulgata.Infrastructure/Data/Pass1CodeLogicDocumentCommitter.cs`, preserving immutable historical v1/v2 records and zero-write replay/conflict (FR-032-FR-035/043/044/056, SC-006/007, AD-9/12).

## Phase 6: User Story 4 — Pause and Resume Without Losing Work (P1)

**Goal**: durable provider circuit, classified failures, one isolated retry, restart recovery, and Chinese manual resume preserve completed Documents.
**Independent test**: first run T036/T037 against the public coordinator/store recovery seam and observe durable revalidation, lease, retry, probe, and pause behavior; then run T044/T045 for the authenticated POST Resume endpoint and verify the exact pause/fail/retry mapping, Chinese manual-resume action, response sanitization, and duplicate-free continuation.

- [x] T032 [P] [US4] Write failure classification RED tests in `tests/Vulgata.Tests/PreparedPass1/Pass1FailurePolicyTests.cs` for one JobTransient retry, exhausted ordinary count, provider configuration pause, unclassified pause, cancellation, integrity/security fail-closed, sanitized durable status, and no denominator inflation (FR-040-FR-044, SC-009, AD-11).
- [x] T033 [US4] Implement sole failure-policy mapping in `src/dotnet/Vulgata.Agents/WorkerExecution/PreparedPass1WorkerDispatcher.cs`, `src/dotnet/Vulgata.Core/DomainServices/PreparedPass1Contracts.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs`; make persisted attempt/state govern retry and never persist raw diagnostics (FR-040-FR-044, AD-11).
- [x] T034 [P] [US4] Write provider-circuit RED tests in `tests/Vulgata.Tests/PreparedPass1/Pass1ProviderCircuitTests.cs` for exact 10/30/60/120/300-second windows, deterministic jitter, bounded Retry-After, pair-wide claim stop, fifth-failure pause, probe recovery, and completed-document preservation (FR-037-FR-039/043, SC-008, AD-10).
- [x] T035 [US4] Implement provider health probe/circuit GREEN behavior in `src/dotnet/Vulgata.Agents/WorkerExecution/WorkerAgentProviderHealthProbe.cs`, `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs`, and `src/dotnet/Vulgata.Web/Data/PreparedPass1Coordinator.cs`; persist pair-wide state and permit only remaining valid work after recovery (FR-037-FR-039/043/044, SC-008, AD-10).
- [x] T036 [P] [US4] Write the observed pre-production coordinator/store recovery RED in `tests/Vulgata.Tests/PreparedPass1/Pass1RecoveryAndResumeApiTests.cs` for pair/child revalidation, Repository/Profiled Revision/hash/prompt/schema/runtime checks, expired leases, due retries, provider probes, cancellation, exact no-op replay, completed-job skipping, durable pause/fail state, and sanitized state atoms; leave authenticated endpoint status/authentication assertions to T044 (FR-039/043/044, SC-008/009, AD-10/11).
- [x] T037 [US4] Implement the recovery/coordinator GREEN owner in `src/dotnet/Vulgata.Web/Data/PreparedPass1Coordinator.cs` and `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs`; preserve existing authorization/API semantics, revalidate both children plus pair, and leave the shared public endpoint/DTO/validator/registration paths to T045 (FR-039/043/044/053/056, SC-008/009, AD-10/11/13).

## Phase 7: User Story 5 — Finish Honestly and Reconcile Exact Local References (P1)

**Goal**: produce bounded terminal outcome/warning inventory and exact same-Repository REFERENCES edges without mutating immutable Documents or entering later stories.
**Independent test**: zero/exact/over-budget and seed failures produce exact status; ten completion orders produce identical warning/edge sets and only accepted Resolving handoff.

- [x] T038 [P] [US5] Write terminal accounting RED tests in `tests/Vulgata.Tests/PreparedPass1/Pass1FailurePolicyTests.cs` for denominator, zero/exact/one-over budget, seed reconstruction/mandatory success, warning-free/CompletedWithWarnings/failed outcomes, bounded canonical failed-file inventory, and no later-pass authorization (FR-045-FR-049/052/055, SC-010, AD-11/13).
- [x] T039 [US5] Implement sole outcome/failure inventory and coordination handoff in `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs`, `src/dotnet/Vulgata.Core/Entities/Pass1Outcome.cs`, `ScanExecution.cs`, and `TwoRepositoryScanCoordination.cs`; use Story 12.2 seed/order authority and stop at `Resolving` without Story 12.4/12.5 work (FR-045-FR-049/052/055/056, AD-11/13).
- [x] T040 [P] [US5] Write reconciliation RED tests in `tests/Vulgata.Tests/PreparedPass1/Pass1ReconciliationTests.cs` for immediate/deferred observations, exact current same-Repository targets, stale/ambiguous/unsupported/cross-Repository/self/duplicate rejection, ten completion orders, idempotence, warning order, and zero Document mutation (FR-050-FR-052, SC-011, AD-7/8/13).
- [x] T041 [US5] Implement deterministic reconciliation in `src/dotnet/Vulgata.Core/DomainServices/PreparedPass1Contracts.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Pass1CodeLogicDocumentCommitter.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs`; create only exact current same-Repository REFERENCES Edges in canonical order and never rewrite ContentJson/Document Versions (FR-050-FR-052, AD-8/9/13).
- [x] T042 [P] [US5] Write reconciliation PostgreSQL atomicity RED coverage in `tests/Vulgata.Tests/PreparedPass1/Pass1DocumentPostgreSqlAtomicTests.cs` for edge/outcome commit rollback, stale writers, cancellation, replay, and no partial visibility (FR-047/050/051, SC-007/011, AD-9/13).
- [x] T043 [US5] Complete the sole reconciliation transaction GREEN path in `src/dotnet/Vulgata.Infrastructure/Data/Pass1CodeLogicDocumentCommitter.cs` and `PreparedPass1Store.cs`; prove pair outcome, child statuses, edge set, and warning inventory commit atomically (FR-047/050/051, AD-9/11/13).

## Phase 8: User Story 1 public execution API and final qualification

**Purpose**: complete the authenticated public Start, active-concurrency update, Status, and Resume endpoint surface after store/coordinator contracts are green, then run nonbehavioral qualification. Shared API paths are serialized under T045.

**Independent test**: T044 must be observed RED for POST Start, PATCH concurrency, GET Status, and authenticated POST Resume before T045; T045 then compiles the shared DTO/validator/endpoint/registration/composition paths and passes the complete API suite without duplicating T037’s coordinator/store behavior.

- [x] T044 [US1] [RED] Write observed public-seam API tests in `tests/Vulgata.Tests/PreparedPass1/Pass1ExecutionApiTests.cs` for authenticated POST Start, PATCH active-concurrency update, GET Status, and complete `POST /api/pass1-executions/{pass1ExecutionId}/resume` assertions: 401/403/ownership-filtered 404; 400/409/422/503 ProblemDetails; absent/default and integer 1-4 validation; immutable initial versus effective-limit replay; expected-version/actor/ownership races; pair/status/warning ordering; Chinese `继续扫描`; resume revalidation/no-op/completed-job skipping; and sanitized responses without credentials, source, host paths, or raw diagnostics (FR-001-FR-008/039/043/044, SC-001/002/008/009, AD-1/2/10/11/13).
- [x] T045 [US1] Implement the sole public API GREEN owner in `src/dotnet/Vulgata.Shared/Scans/Pass1ExecutionContracts.cs`, `src/dotnet/Vulgata.Shared/Scans/Pass1ExecutionValidators.cs`, `src/dotnet/Vulgata.Web/Api/Scans/Pass1ExecutionEndpoints.cs`, `src/dotnet/Vulgata.Web/Data/PreparedPass1Coordinator.cs`, `src/dotnet/Vulgata.Web/Api/ApiEndpointRegistrationExtensions.cs`, and `src/dotnet/Vulgata.Web/Program.cs`; compose POST Start, PATCH concurrency, GET Status, and recovery/manual-resume actions over T010/T012/T035/T037, map all required ProblemDetails/status codes, preserve ManagementAccess and ownership nondisclosure, and never duplicate store semantics (FR-001-FR-008/039/043/044/053/056, SC-001/002/008/009, AD-1/2/10/11/13).

## Phase 9: Cross-cutting qualification and final barrier

**Purpose**: qualification/documentation/metadata tasks only; these do not add new behavior or absorb later stories. T046, T047, and T049 establish the stable qualification boundary; T048 is the serialized pre-convergence barrier, while root-owned pinned verification after convergence, quality review, and E2E remains mandatory and is not replaced by T048.

- [ ] T046 [P] Run host runtime qualification and record pinned CodeGraph/Java/JDT-LS/PostgreSQL evidence in `tests/Vulgata.Tests/PreparedPass1/Pass1HostRuntimeQualificationTests.cs` and `artifacts/qualification/story-12.3/host-runtime.md` (FR-053/054/056, SC-012, AD-3/4).
- [ ] T047 [P] Run production boundary/composition/literal inspection and write `artifacts/qualification/story-12.3/production-boundary-inspection.json` from `tests/Vulgata.Tests/PreparedPass1/Pass1ProductionBoundaryQualificationTests.cs`; route any forbidden match back to the owning RED/GREEN slice instead of weakening the denylist (FR-018/053-FR-056, SC-003/012, AD-4/12/13).
- [ ] T048 Run the stable pre-convergence migration, PostgreSQL 17, focused PreparedPass1, adjacent DurableScanning/PreparedScanning/KnowledgeStore/AnalysisRuntime, Web build, full suite, and `git diff --check` barrier after successful T046, T047, and T049 evidence; capture results in `specs/012-resumable-file-pass1-code-logic/verification/final-verification.md`, without replacing root-owned pinned verification after convergence, quality review, and E2E (SC-001-SC-012).
- [ ] T049 [P] Verify no Story 12.4/12.5 production paths, `CodeGraphContext`, `retrieve_code_logic`, Pass 2, Business Logic Documents, Chat, final release UI, evaluation questions, or release run were added; record the exclusion check in `specs/012-resumable-file-pass1-code-logic/verification/exclusions.md` (FR-054/055/056, AD-3/13, SC-012).

## Dependency order and bounded parallelism

- T001-T002 are nonbehavioral setup and may run in parallel; T003 is a required prompt/resource build prerequisite and must complete before T014, T020, T025, or T045; T004-T008 are foundational and block all story phases.
- Each RED task must be observed failing before its paired GREEN task starts. A RED task and its GREEN task are never parallel. T006 must fail before T007/T008; T044 must fail after T010/T012/T035/T037 contracts and before T045.
- Within a story, RED tasks with disjoint test files may run in parallel after their prerequisite contracts; their GREEN tasks are serial per sole-owned production path. T009/T011/T013/T015, T017/T019/T021, T024/T026/T028/T030, T032/T034/T036, and T038/T040/T042 are parallel groups. T023 is post-GREEN qualification and is not parallel with its prerequisites.
- US1 depends on T003/T005/T006/T007/T008 and T044/T045; US2 depends on T003/T014/T018 and T007/T008; US3 depends on T003/T005/T007/T008 and T014/T018; US4 depends on T010/T012/T014/T022 and T007/T008, then T037/T045 for public resume; US5 depends on T029/T031/T033/T035/T037/T045.
- T046, T047, and T049 require all behavior slices, including T045, and may run in parallel as qualification/evidence tasks. T048 is not parallel: it depends on successful completion and durable evidence from T046, T047, and T049, then provides the stable pre-convergence suite/build/migration/diff barrier. Root-owned pinned verification after convergence, quality review, and E2E remains a later gate.

## Ownership map

| Sole-owned shared path | Tasks | Rule |
| --- | --- | --- |
| `PreparedPass1Store.cs` / `IPreparedPass1Store.cs` | T010, T012, T016, T022, T033, T035, T037, T039, T041, T043 | One implementation owner sequence; no other task edits this path concurrently. |
| `Pass1CodeLogicDocumentCommitter.cs` | T029, T031, T041, T043 | Commit/reconciliation owner; tests only touch test files. |
| `PreparedPass1WorkerDispatcher.cs` | T014, T022, T033 | Dispatcher owner sequence; all depend on T003. |
| `PreparedPass1ResultParser.cs` | T014, T025 | Packet/parser owner sequence; both depend on T003. |
| `pass1-code-logic-worker.v2.md` + `Vulgata.Agents.csproj` embedded-resource item | T003 | Sole prompt/resource owner; completed before T014/T020/T025. |
| `PreparedPass1Coordinator.cs` | T035, T037, T045 | Provider/recovery/API coordinator sequence; T045 follows T037 and owns public composition. |
| `Pass1ExecutionContracts.cs`, `Pass1ExecutionValidators.cs`, `Pass1ExecutionEndpoints.cs`, `ApiEndpointRegistrationExtensions.cs`, `Program.cs` | T045 | Sole public API composition owner; T037 owns coordinator/store recovery only, and no task edits these paths concurrently. |
| `Pass1ExecutionApiTests.cs` | T044 | Sole public Start/update/status API RED owner. |
| `Pass1RecoveryAndResumeApiTests.cs` | T036 | Sole coordinator/store recovery RED owner; endpoint authentication and Resume HTTP assertions belong to T044. |
| migrations/configuration | T008 | Sole migration/configuration owner. |
| `PreparedPass1WorkerDispatcherTests.cs` | T013, T021 | Test-author sequence; no production edits. |
| `Pass1FailurePolicyTests.cs` | T032, T038 | Test-author sequence after US4 behavior is available. |
| `Pass1DocumentPostgreSqlAtomicTests.cs` | T030, T042 | Test-author sequence; document atomic and reconciliation cases remain separate sections. |

## Exact RED to GREEN ownership map

Every RED task below is owned by `tdd-test-author` and must record an observed failing public-seam test before its paired GREEN task. Every GREEN task below is owned by `tdd-implementer` and may edit only the production paths named in that task.

| RED (`tdd-test-author`) | GREEN (`tdd-implementer`) |
| --- | --- |
| T004 | T005 |
| T006 | T007/T008 |
| T009 | T010 |
| T011 | T012 |
| T013 | T014 |
| T015 | T016 |
| T017 | T018 |
| T019 | T020 |
| T021 | T022 |
| T024 | T025 |
| T026 | T027 |
| T028 | T029 |
| T030 | T031 |
| T032 | T033 |
| T034 | T035 |
| T036 | T037 |
| T038 | T039 |
| T040 | T041 |
| T042 | T043 |
| T044 | T045 |

T006/T007/T008 are the explicit AD-12 test-first compatibility slice: T006 is the observed transition/state RED, T007 owns domain transition GREEN behavior, and T008 owns additive EF configuration/migration lifecycle GREEN mechanics. T030/T031 are the PostgreSQL verification/implementation pair for the T028 commit behavior; T028 remains the public commit RED owner. T023 is a post-GREEN qualification task with explicit dependencies, not a RED/GREEN behavior pair.

## Traceability matrix

- FR-001-FR-009: T009-T016, T044-T045; FR-010-FR-020: T017-T023; FR-021-FR-034: T024-T031; FR-035-FR-044: T032-T037, T044-T045; FR-045-FR-052: T038-T043; FR-053-FR-056: T001-T003, T014, T020, T023, T037, T045, T046-T049.
- SC-001/002: T004/T009-T016/T044-T045; SC-003/004: T017-T023; SC-005/006/007: T024-T031; SC-008/009: T032-T037/T044-T045; SC-010/011: T038-T043; SC-012: T046-T049.
- User Story 1 scenarios 1-5: T009-T016/T044-T045; User Story 2 scenarios 1-6: T017-T023; User Story 3 scenarios 1-5: T024-T031; User Story 4 scenarios 1-6: T032-T037/T044-T045; User Story 5 scenarios 1-6: T038-T043. Edge cases are explicitly covered by the corresponding RED task named in each slice.
- AD-1/2: T004-T016/T044-T045; AD-3/4/5: T013-T023; AD-6/7/8/9: T024-T031 and T040-T043; AD-10/11: T032-T039/T044-T045; AD-12: T006-T008/T025/T031; AD-13: T039-T043/T045/T046-T049.

## Nonbehavioral task classification

T001-T002 and T046-T049 are inventory, fixture, qualification, evidence, or exclusion verification. T003 is a nonbehavioral externalized-prompt/resource build prerequisite verified by compilation; T008 is additive EF schema/migration mechanics verified by T006/T008 lifecycle tests, not fabricated RED/GREEN behavior. All other tasks are observable behavior and have an explicit public-seam RED before the sole-owner GREEN implementation.

## Definition and scope guard

No task implements Cross-Repository Resolution, endpoint-centered flow grouping, `retrieve_code_logic`, Pass 2, Business Logic Documents, Chat, final release UI, evaluation questions, release run, CodeGraphContext, raw analysis protocols/storage, shell/network/database tools, or unrestricted Agent loops. Any compatibility correction must remain limited to AD-12 and be evidenced by T006/T008.

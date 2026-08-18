---
description: "Dependency-ordered implementation tasks for Durable Scan Foundation & Analysis Runtime"
---

# Tasks: Durable Scan Foundation & Analysis Runtime

**Input boundary**: `specs/010-durable-scan-foundation/spec.md` (SHA-256 `DA24F90063D0B66D50D642BED8D1E1709257C6BF8B0B42E4AE89929CE5FEEFB2`) and accepted `plan.md` (SHA-256 `389426AD17DDC65971F3F29E1CE06B18483346E45E38A33A7C13A2F298652637`), plus `research.md`, `data-model.md`, `quickstart.md`, and all three contracts.

**Workflow boundary**: schema `2.0`, execution level `unified`; planned-change authorization was granted at `2026-08-18T08:43:37Z`. These tasks authorize implementation planning only and preserve every later TDD, convergence, quality-review, E2E, security, migration, and verification gate.

**Ownership rule**: `tdd-test-author` writes and runs behavioral RED tests first; `tdd-implementer` makes the minimum GREEN production change; the test author then runs focused and surrounding verification. The single integration owner owns `VulgataDbContext`, all new EF configurations, migration/designer/model snapshot, atomic knowledge-store integration, shared cross-cutting contracts, `Directory.Packages.props`, `Program.cs`, and `docker-compose.yml`. No two implementers may edit those paths concurrently.

## Phase 1: Setup and pinned boundary

**Purpose**: establish executable test seams and preserve the approved runtime/repository boundary. No production behavior is claimed by setup-only tasks.

- [ ] T001 [P] Record the accepted Story 12.1 authority hashes, schema `2.0`/`unified` activation, approved planned-change authorization, and excluded Story 11.5/`specs/008`/non-ancestral checkpoint boundary in `specs/010-durable-scan-foundation/README.md` (documentation-only; verify the record against `spec.md`, `plan.md`, `docs/bmad/implementation-artifacts/12-1-durable-scan-foundation-and-analysis-runtime.md`, and the workflow activation).
- [ ] T002 [P] Add the neutral contained Java/source fixture and deterministic revision metadata under `tests/Vulgata.Tests/Fixtures/AnalysisRuntime/` for one authorized Repository/Profiled Revision; prove fixture paths cannot escape the fixture root.
- [ ] T003 [P] Add test helpers under `tests/Vulgata.Tests/AnalysisRuntime/` and `tests/Vulgata.Tests/DurableScanning/` for fixed clocks, deterministic IDs, canonical hashes, disposable databases, process-launcher/filesystem doubles, and sanitized diagnostics; no production behavior is introduced.
- [ ] T004 [P] Add the disposable PostgreSQL 17 test harness and unique database lifecycle helper under `tests/Vulgata.Tests/Migrations/` and `tests/Vulgata.Tests/DurableScanning/`; ensure cleanup drops only the uniquely named test database.
- [ ] T005 (single integration owner) Pin StreamJsonRpc `2.25.29` in `Directory.Packages.props`, add its `PackageReference` to `src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj`, and perform a bounded restore/build of the Infrastructure project; do not edit `docker-compose.yml` or production composition in this prerequisite task (FR-016, FR-024, FR-027).

## Phase 2: Foundational contracts and domain invariants

**Purpose**: define the Core value objects, entities, ports, and typed results that all story slices depend on. Tests precede every observable behavior.

- [ ] T006 [P] [US1] (tdd-test-author RED) Add domain and persistence-seam tests for immutable Scan Context Snapshot capture, canonical content/schema identity, Repository/Scan Run/Profiled Revision binding, and both accepted temporal insertion orders in `tests/Vulgata.Tests/DurableScanning/ScanContextSnapshotTests.cs`: snapshot-first and execution-first must validate `ScanRun.StartedAt <= Snapshot.CapturedAt <= new-path ScanExecution.StartedAt`; test both invalid bounds and assert the second insert fails without a partial snapshot/execution insert (FR-001, FR-002, SC-001, PR-003).
- [ ] T007 [US1] (tdd-implementer GREEN) Implement `ScanContextSnapshot` and its bounded factories/value objects in `src/dotnet/Vulgata.Core/Entities/ScanContextSnapshot.cs` and supporting Core files; enforce identity/immutability without machine paths, while the integration owner’s persistence seam performs second-insert cross-record ordering validation and rollback for either creation order (FR-001, FR-002, FR-028, PR-003).
- [ ] T008 [P] [US1] (tdd-test-author RED) Add domain tests covering Scan Job Execution deterministic identity, manifest ordinal, every state, attempt/retry/lease/completion shape, legal transitions, expired-lease boundaries, and contradiction rejection in `tests/Vulgata.Tests/DurableScanning/ScanJobExecutionTests.cs` (FR-003, FR-004, SC-001, SC-007).
- [ ] T009 [US1] (tdd-implementer GREEN) Implement `ScanJobExecution` and state/result/failure value objects in `src/dotnet/Vulgata.Core/Entities/ScanJobExecution.cs` and supporting Core files with monotonic state-version and deterministic identity rules (FR-003, FR-004, FR-028).
- [ ] T010 [P] [US1] (tdd-test-author RED) Add tests for evolved Scan Execution phases, pause/failure classifications, normalized prompt/schema/provider/runtime bindings, cancellation, `CompletedWithWarnings`, and state-version transitions in `tests/Vulgata.Tests/DurableScanning/ScanExecutionFoundationTests.cs` (FR-005, FR-009, FR-028).
- [ ] T011 [US1] (tdd-implementer GREEN) Evolve `src/dotnet/Vulgata.Core/Entities/ScanExecution.cs` and related phase/failure types to implement T010 while retaining readable legacy phase atoms/columns and without adding automatic background resume (FR-005, FR-010, FR-031).
- [ ] T012 [P] [US1] (tdd-test-author RED) Add tests for exactly-two distinct authorized Repository bindings, same-System ownership, revision binding, independent Repository Scan identities, combined phases, and preservation of one valid outcome when the other fails in `tests/Vulgata.Tests/DurableScanning/TwoRepositoryScanCoordinationTests.cs` (FR-011, FR-012, FR-013, SC-003).
- [ ] T013 [US1] (tdd-implementer GREEN) Implement `TwoRepositoryScanCoordination` and its bounded lifecycle in `src/dotnet/Vulgata.Core/Entities/TwoRepositoryScanCoordination.cs` (FR-011, FR-012, FR-013, FR-028).
- [ ] T014 [P] [US1] (tdd-test-author RED) Add contract tests for `IDurableScanStore`, `ITwoRepositoryScanCoordinationStore`, recovery results, atomic commit/replay results, and conditional version/lease semantics in `tests/Vulgata.Tests/DurableScanning/DurableScanContractTests.cs` from `specs/010-durable-scan-foundation/contracts/durable-scan-contracts.md` (FR-006, FR-007, FR-008, FR-009, FR-013).
- [ ] T015 (single integration owner) (tdd-implementer GREEN) Add the durable store, coordination store, recovery, and atomic commit ports/results in the exact nonoverlapping files under `src/dotnet/Vulgata.Core/DomainServices/` matching `contracts/durable-scan-contracts.md`; serialize this shared-contract handoff after T014 and keep retry windows, attempt budgets, concurrency, ordering, and Worker dispatch policy out of Story 12.1 (FR-006-FR-010, FR-031).
- [ ] T016 [P] [US2] (tdd-test-author RED) Add tests for canonical normalized Analysis Runtime Identity, fixed CodeGraph/JDT LS/Java/StreamJsonRpc values, path non-identity, missing archive checksum, and bounded health result classification in `tests/Vulgata.Tests/AnalysisRuntime/AnalysisRuntimeIdentityTests.cs` (FR-014, FR-015, FR-016, FR-017, FR-018, FR-019, FR-020, FR-021, FR-022, FR-028, SC-004, SC-008).
- [ ] T017 [US2] (tdd-implementer GREEN) Implement bounded runtime identity/options/health value objects only in the nonoverlapping `src/dotnet/Vulgata.Core/DomainServices/AnalysisRuntimeIdentity.cs`; this task MUST NOT edit `src/dotnet/Vulgata.Core/DomainServices/AnalysisRuntimeContracts.cs`. Require the exact JDT distribution archive checksum and never substitute the core JAR hash (FR-014-FR-022, FR-028).
- [ ] T018 [P] [US3] (tdd-test-author RED) Add closed-capability contract tests for CodeGraph, Java navigation, revision-bound source reading, lifecycle failures, result limits, and all forbidden raw protocol/shell/network/internal-store/filesystem/cross-Repository/stale-revision accesses in `tests/Vulgata.Tests/AnalysisRuntime/ClosedAnalysisPortTests.cs` (FR-023-FR-027, SC-005).
- [ ] T019 (single integration owner) [US3] (tdd-implementer GREEN) After T018 RED, be the sole writer of `src/dotnet/Vulgata.Core/DomainServices/AnalysisRuntimeContracts.cs` and add the typed closed analysis ports and bounded evidence/failure models; expose only index/rebuild/status/symbol search/callers/callees/related files, document symbols/definition/references/implementations, and contained exact source ranges (FR-023-FR-027, FR-031).

## Phase 3: User Story 1 - Resume Trusted Scan Work (Priority: P1)

**Goal**: reconstruct durable Repository Scan progress after restart, preserve committed Documents, and manually recover only valid eligible work.

**Independent test**: mixed completed/pending/running/retry-scheduled jobs survive a real application restart against PostgreSQL; repeated manual resume is idempotent and never duplicates a Document Version.

- [ ] T020 [P] [US1] (tdd-test-author RED) Add store tests for snapshot/job/coordination persistence, deterministic unique keys, composite Repository/Scan Run ownership, state checks, lease/retry/completion shape, restrictive durable-evidence deletes, and the PR-003 second-insert transaction boundary in `tests/Vulgata.Tests/DurableScanning/DurableScanStoreTests.cs` (FR-001-FR-005, FR-011-FR-013, SC-001, SC-003, SC-007, PR-003).
- [ ] T021 [US1] (tdd-implementer GREEN) Implement `src/dotnet/Vulgata.Infrastructure/Data/DurableScanRepository.cs` and coordination persistence against the Core ports, including conditional claim/transition, state versions, expired leases, due retries, terminal-state protection, and bounded contradiction failures (FR-003-FR-009, FR-011-FR-013).
- [ ] T022 [P] [US1] (tdd-test-author RED) Add atomic completion tests with injected failure before/after Document materialization, before job completion, exact completed replay after lost response, and non-identical replay conflict in `tests/Vulgata.Tests/DurableScanning/AtomicDocumentJobCommitTests.cs` (FR-006, FR-007, SC-001, SC-007).
- [ ] T023 (single integration owner) (tdd-implementer GREEN) Refactor `src/dotnet/Vulgata.Infrastructure/Data/WorkerAgentResultCommitter.cs` and `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeStoreRepository.cs` to commit one deterministic Document Version and Completed job atomically; evaluate exact already-Completed replay before the new Running lease/version branch and return mutation-free `Committed` or `Conflict` as specified. This shared atomic-commit edit is serialized after T022 RED and before recovery/API work (FR-006, FR-007).
- [ ] T024 [P] [US1] (tdd-test-author RED) Add recovery coordinator tests for repeated invocation, valid/expired/invalid leases, future/due/exhausted retries, cancellation, each revalidation mismatch, provider pause, integrity contradiction, committed-document preservation, and no automatic loop/dispatch in `tests/Vulgata.Tests/DurableScanning/RecoveryCoordinatorTests.cs` (FR-007-FR-010, SC-001, SC-002, SC-009).
- [ ] T025 [US1] (tdd-implementer GREEN) Implement explicit idempotent recovery and revalidation in `src/dotnet/Vulgata.Infrastructure/Data/DurableScanRecoveryCoordinator.cs`, with authorization/revision/file/prompt/schema/provider/runtime checks outside database locks, bounded classified stop reasons, and no retry scheduling or Worker dispatch (FR-007-FR-010, FR-031).
- [ ] T026 [P] [US1] (tdd-test-author RED) Add API authorization, ownership privacy, route validation, sanitized ProblemDetails, bounded response, and idempotent replay tests for manual resume in `tests/Vulgata.Tests/DurableScanning/ManualResumeApiTests.cs` (FR-008-FR-010, SC-009).
- [ ] T027 [US1] (tdd-implementer GREEN) Add the bounded request/response DTOs and validator in `src/dotnet/Vulgata.Shared/Scans/ManualResumeContracts.cs` and the `ManagementAccess` endpoint in `src/dotnet/Vulgata.Web/Api/Scans/ManualResumeEndpoints.cs`; enforce 401 unauthenticated, 403 policy failure, ownership-filtered 404 without existence probe, 409 conflict, and sanitized Chinese ProblemDetails (FR-008-FR-010).
- [ ] T028 [US1] (tdd-test-author RED) After the migration/model slice T045-T046 is applied to the disposable PostgreSQL 17 database, add real restart/concurrency tests with mixed durable states, repeated manual resume, competing stale transitions, and atomic result visibility in `tests/Vulgata.Tests/DurableScanning/DurableScanRestartPostgreSqlTests.cs` (FR-006-FR-010, SC-001, SC-002, SC-007, SC-009).
- [ ] T029 [single integration owner] (tdd-implementer GREEN) Integrate durable stores, atomic commit, recovery, and manual resume contracts into `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, all new configurations under `src/dotnet/Vulgata.Infrastructure/Data/Configurations/`, and `src/dotnet/Vulgata.Web/Program.cs`; this wiring is serialized after the migration/model slice T045-T046 and keeps cross-cutting registration under one writer (FR-001-FR-013).

## Phase 4: User Story 2 - Trust the Analysis Runtime (Priority: P1)

**Goal**: qualify the approved Windows-host CodeGraph and Java 21/JDT LS runtime without ordinary-startup indexing or historical CGC dependencies.

**Independent test**: real host tools and neutral fixture prove approved identities/readiness; mismatches and missing archive checksum fail closed before Worker dispatch.

- [ ] T030 [P] [US2] (tdd-test-author RED) Add CodeGraph adapter tests for exact package/command/repository/bundle identity, all three approved installed-file SHA-256 values, allowed JSON operations, sanitized diagnostics, and ordinary startup no-index/no-`.codegraph` behavior in `tests/Vulgata.Tests/AnalysisRuntime/CodeGraphAdapterTests.cs` (FR-014, FR-015, FR-019, FR-022, FR-023, SC-004, SC-005, SC-008).
- [ ] T031 [US2] (tdd-implementer GREEN) Implement the closed CodeGraph adapter, integrity probe, parser, and process lifecycle under `src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/`; use the installed `codegraph` CLI and pinned hashes, never expose shell/raw graph/storage access, and never index during ordinary health checks (FR-014, FR-015, FR-019, FR-022, FR-023, FR-027).
- [ ] T032 [P] [US2] (tdd-test-author RED) Add JDT LS adapter tests for Java major 21, release identity, archive checksum required/non-substitutive core-JAR observation, StreamJsonRpc `2.25.29` stdio initialization, cancellation/shutdown/forced cleanup, exclusive revision workspace, and sanitized lifecycle failures in `tests/Vulgata.Tests/AnalysisRuntime/JdtLsAdapterTests.cs` (FR-016-FR-019, FR-027, SC-004, SC-005).
- [ ] T033 [US2] (tdd-implementer GREEN) After single-integration-owner package task T005 and T032 RED, implement the closed JDT LS stdio adapter and workspace lease under `src/dotnet/Vulgata.Infrastructure/Analysis/Java/`, launching the exact Equinox distribution through configured Java 21 and exposing only typed navigation operations (FR-016-FR-019, FR-024, FR-027).
- [ ] T034 [P] [US2] (tdd-test-author RED) Add revision-contained source reader tests for exact bounded ranges/excerpts, path containment, size/result limits, stale revision, cross-Repository, cancellation, and sanitized errors in `tests/Vulgata.Tests/AnalysisRuntime/RevisionBoundSourceReaderTests.cs` (FR-021, FR-025-FR-027, SC-005).
- [ ] T035 [US2] (tdd-implementer GREEN) Implement `src/dotnet/Vulgata.Infrastructure/Analysis/Source/RevisionBoundSourceReader.cs` with authorized Repository/Profiled Revision containment and bounded typed excerpts; reject every unbounded or stale/cross-Repository request (FR-021, FR-025-FR-027).
- [ ] T036 [P] [US2] (tdd-test-author RED) Add health/composition tests that observe ordinary Web startup invoking configured integrity verification, creating no index/import side effect, exposing a bounded persisted/readiness result, and keeping management startup available while Worker dispatch is gated closed in NotReady state; also cover wrong version/hash/Java major/workspace, malformed response, missing archive/checksum (`JdtDistributionArchiveMissing`/`JdtDistributionArchiveChecksumMissing`), host-run app/tools, PostgreSQL-only default Docker, and absence of CodeGraphContext/Python prerequisites in `tests/Vulgata.Tests/AnalysisRuntime/AnalysisRuntimeHealthTests.cs` (FR-017-FR-022, SC-004, SC-008).
- [ ] T037 [single integration owner] (tdd-implementer GREEN) Wire runtime options, health probe, typed ports, durable stores, and manual-resume service in `src/dotnet/Vulgata.Web/Program.cs`; explicitly execute configured integrity verification during ordinary startup, persist/expose only the bounded readiness result, keep management startup available, and gate Worker dispatch fail-closed while NotReady. Update only the approved `post-deadline-app` profile in `docker-compose.yml`; do not register `CodeGraphContextProvider` or use `docker/Dockerfile` (FR-019-FR-022, SC-008).
- [ ] T038 [P] [US2] (qualification; no expected RED) After T031 and T037 are GREEN, run real CodeGraph qualification tests against a neutral Repository revision, verifying package/version/bundle/three hashes, allowed operations, and no `.codegraph` creation during ordinary startup in `tests/Vulgata.Tests/AnalysisRuntime/CodeGraphCliQualificationTests.cs` (FR-014, FR-015, FR-019, SC-004, SC-005, SC-008).
- [ ] T039 [P] [US2] (qualification; no expected RED) Run explicit missing-archive qualification evidence using the supplied JDT LS install root `C:/Users/13728/.local/jdt-language-server-1.61.0-202608141332` and Java 21 path `C:/Program Files/Java/latest/jdk-21` in `tests/Vulgata.Tests/AnalysisRuntime/JdtLsMissingArchiveQualificationTests.cs`; assert readiness remains NotReady and the core JAR hash is non-substitutive (FR-017, SC-004).
- [ ] T040 [P] [US2] (qualification; no expected RED; conditional) After T033 and T037 are GREEN and only when the exact retained distribution archive and expected checksum are supplied, run real JDT LS neutral-fixture qualification in `tests/Vulgata.Tests/AnalysisRuntime/JdtLsRealQualificationTests.cs`; cover initialize/initialized, symbols/definition/references/implementations, cancellation, shutdown/exit, cleanup, and cross-revision workspace rejection (FR-016-FR-019, FR-024, FR-027, SC-004, SC-005).

## Phase 5: User Story 3 - Use Bounded Analysis Evidence (Priority: P2)

**Goal**: make every analysis operation typed, revision-bound, lifecycle-bounded, and fail closed.

**Independent test**: allowed operations work on the neutral fixture and all forbidden capability escalations are rejected.

- [ ] T041 [P] [US3] (tdd-test-author RED) Add integration tests that exercise every allowed CodeGraph, Java navigation, and source-reading operation through the typed ports and assert Repository/Profiled Revision binding in `tests/Vulgata.Tests/AnalysisRuntime/BoundedAnalysisEvidenceTests.cs` (FR-023-FR-026, SC-005).
- [ ] T042 [US3] (tdd-implementer GREEN) Wire typed adapters to Core ports and bounded evidence DTOs under `src/dotnet/Vulgata.Infrastructure/Analysis/` without exposing process handles, raw protocol, arbitrary query language, internal stores, network clients, or unrestricted filesystem access (FR-023-FR-027).
- [ ] T043 [P] [US3] (tdd-test-author RED) Add adversarial security-negative tests for shell/raw command, raw LSP, arbitrary graph query, internal CodeGraph database, network, path traversal, excessive ranges/results, cross-Repository, stale revision, and workspace collision in `tests/Vulgata.Tests/AnalysisRuntime/AnalysisCapabilitySecurityTests.cs` (FR-025-FR-027, SC-005).
- [ ] T044 [US3] (tdd-implementer GREEN) Harden adapter validation, cancellation/timeouts, process-tree cleanup, diagnostic sanitization, workspace leases, and closed failure mapping in `src/dotnet/Vulgata.Infrastructure/Analysis/CodeGraph/`, `src/dotnet/Vulgata.Infrastructure/Analysis/Java/`, and `src/dotnet/Vulgata.Infrastructure/Analysis/Source/` (FR-026, FR-027, SC-005).

## Phase 6: User Story 4 - Preserve Existing Knowledge Through Migration (Priority: P2)

**Goal**: add the durable foundation without changing protected historical knowledge or legacy Scan readability.

**Independent test**: a real PostgreSQL 17 database applies, rolls back, reapplies, and concurrently exercises the additive schema while protected rows remain readable.

- [ ] T045 [P] [US4] (tdd-test-author RED) Add EF configuration/model tests for all new entities, composite ownership FKs, unique deterministic IDs/ordinals, filtered committed Document Version identity, state/lease/retry/completion checks, UTC timestamps, Restrict/NoAction deletes, legacy-row compatibility, and both-order PR-003 second-insert/no-partial-insert behavior in `tests/Vulgata.Tests/Migrations/DurableScanModelTests.cs` (FR-001-FR-005, FR-011-FR-013, FR-028-FR-030, SC-003, SC-006, SC-007, PR-003).
- [ ] T046 [single integration owner] (tdd-implementer GREEN) Before T028/T029 restart/API integration, add one cohesive additive migration, designer, and model snapshot under `src/dotnet/Vulgata.Infrastructure/Data/Migrations/`, update `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs` and one configuration per entity under `src/dotnet/Vulgata.Infrastructure/Data/Configurations/`, and implement second-insert PR-003 ordering validation with transaction rollback for snapshot-first and execution-first inserts; retain legacy columns/rows and make Down remove only new Story 12.1 structures (FR-001-FR-005, FR-011-FR-013, FR-029-FR-030, PR-003).
- [ ] T047 [P] [US4] (qualification; no expected RED) After T046 is GREEN, run real PostgreSQL 17 apply/rollback/reapply, pending-model-change, protected Document/Source Anchor/Edge/Uncertainty/prepared-demo readability, and unique database cleanup tests in `tests/Vulgata.Tests/Migrations/DurableScanMigrationPostgreSqlTests.cs` (FR-029, FR-030, SC-006).
- [ ] T048 [P] [US4] (qualification; no expected RED) After T023, T028, and T046 are GREEN, run real PostgreSQL concurrent transition and atomicity qualification with competing stale state versions/leases and injected commit failures in `tests/Vulgata.Tests/Migrations/DurableScanConcurrencyPostgreSqlTests.cs` (FR-006, FR-030, SC-007).
- [ ] T049 [single integration owner] (tdd-implementer GREEN) Integrate the migration and model snapshot with `src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj`, startup migration behavior in `src/dotnet/Vulgata.Web/Program.cs`, and the existing immutable knowledge model without changing protected semantics (FR-029, FR-030).

## Phase 7: Cross-cutting verification and handoff

**Purpose**: verify the complete Story 12.1 boundary and document explicit exclusions. These tasks do not implement later stories.

- [ ] T050 [P] Run focused durable-scan and analysis-runtime suites from `specs/010-durable-scan-foundation/quickstart.md`: `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter 'FullyQualifiedName~DurableScanning|FullyQualifiedName~AnalysisRuntime'`; record RED/GREEN evidence and no implementation claim for skipped real JDT qualification when its archive/checksum is absent.
- [ ] T051 [P] Run migration/restart/tool/security verification commands from `specs/010-durable-scan-foundation/quickstart.md`, including `DurableScanMigrationPostgreSql`, `DurableScanRestartPostgreSql`, `CodeGraphCliQualification`, `JdtLsMissingArchiveQualification`, and conditional `JdtLsRealQualification`; retain sanitized evidence and verify disposable databases are dropped.
- [ ] T052 [single integration owner] Verify `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`, full `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj`, migration model consistency, and `docker compose config` against the approved host-run/PostgreSQL-only boundary; do not modify `docker/Dockerfile` or invoke historical CGC qualification.
- [ ] T053 [P] Verify source-reference coverage and canonical terminology against `docs/UBIQUITOUS_LANGUAGE.md`, Constitution, relevant ADR/context files, the August 17/18 approved proposals, corrected August design, PRD overlay, `docs/bmad/epics.md`, and the Story record; report any authority conflict instead of inferring a resolution.
- [ ] T054 [P] Read-only verify the implementation candidate against the frozen exclusions in `specs/010-durable-scan-foundation/spec.md` and `plan.md`; record evidence only in the active workflow/convergence evidence owned by the root, never by mutating `tasks.md`: no Java/XML PreScan, Prepared Scan Manifest, File Code Unit selection/splitting, Worker generation/dispatch policy, `navigate_code`, Cross-Repository Resolution, Pass 2, Chat retrieval, production progress UI, or final release evidence (FR-031, FR-032).

## Dependencies and execution order

1. Setup T001-T005 is metadata/fixture preparation; T005 is the package-only single-integration-owner dependency task and must complete before T033. The production composition change is exclusively T036 RED → T037 GREEN.
2. Foundational contracts and invariants execute T006→T019 in RED→GREEN pairs; T015 and T019 are serialized shared-contract writes under the integration owner, while T017 is identity-only and cannot edit the shared contract file.
3. The migration/model slice T045→T046 is a prerequisite gate before T020/T021, T023, T025/T027, and T028/T029 can be GREEN; T006/T020/T045 REDs precede T046's second-insert implementation. The list placement of the US4 phase does not permit implementation out of this dependency order.
4. User Story 1 executes T020→T029 after T046: stores and atomic commit precede recovery/API; T023 and T029 are single integration-owner work, and T028's real restart RED is run only against the applied schema.
5. User Story 2 executes T030→T040: T005 precedes T033, adapters and startup health precede qualification; T037 owns composition and startup dispatch gating. T039 is always executable; T040 is conditional NotReady evidence until the exact archive/checksum exists.
5. User Story 3 executes T041→T044 after runtime adapters; security-negative tests remain mandatory.
6. User Story 4 executes T045→T049 as the migration/compatibility gate, with T046 before T028/T029 and T047/T048 qualification after the schema and behavior are GREEN; T049 remains serialized integration-owner wiring.
7. T050-T054 run only after all selected behavior slices are GREEN. T053-T054 are read-only handoff evidence; then the root freezes tasks, runs `speckit-converge`, obtains independent quality review, applies the E2E decision, and performs final verification. No second definition-review gate is generated here.

### Parallel opportunities

- T002-T004 and independent RED tasks T006, T008, T010, T012, T016, and T018 can be prepared in parallel, but each GREEN task waits for its own RED evidence; T005 is serialized as the package-only owner and T036→T037 is the sole composition transition.
- T030, T032, and T034 are independent adapter RED slices after the runtime contracts; T038-T040 are qualification evidence slices after their adapters.
- T045 may prepare its RED before T046; T046/T049 remain serialized under the single integration owner, and T047/T048 are post-GREEN qualification with no fabricated RED.
- No parallel worker may touch `VulgataDbContext`, EF configurations/migration/snapshot, `Program.cs`, `docker-compose.yml`, package management, or shared cross-cutting contracts while the integration owner is active.

## Traceability

### Functional requirements

| Requirement | Tasks |
|---|---|
| FR-001–FR-002 | T006–T007, T020, T045–T046 |
| FR-003–FR-004 | T008–T009, T020–T021, T045–T046 |
| FR-005 | T010–T011, T020–T021, T045–T046 |
| FR-006 | T014–T015, T022–T023, T028–T029, T048–T049 |
| FR-007–FR-010 | T014–T015, T024–T029 |
| FR-011–FR-013 | T012–T013, T020–T021, T045–T046 |
| FR-014–FR-015 | T016–T017, T030–T031, T036, T038 |
| FR-016–FR-022 | T016–T017, T032–T040, T036–T037 |
| FR-023–FR-027 | T018–T019, T030–T044 |
| FR-028 | T007, T009, T013, T017, T045–T046 |
| FR-029–FR-030 | T045–T052 |
| FR-031–FR-032 | T011, T015, T019, T025, T031, T037, T054 |

### Success criteria

| Criterion | Tasks |
|---|---|
| SC-001 | T006, T008, T020, T022–T025, T028, T050 |
| SC-002 | T024–T025, T028, T050 |
| SC-003 | T012–T013, T020–T021, T045–T046 |
| SC-004 | T016–T017, T030–T033, T036, T038–T040, T050–T051 |
| SC-005 | T018–T019, T030–T035, T038, T040–T044, T050–T051 |
| SC-006 | T045–T049, T051–T052 |
| SC-007 | T008, T020, T022, T028, T045, T048 |
| SC-008 | T030–T031, T036–T038, T052 |
| SC-009 | T024–T029, T050–T052 |

### Definition-review remediation closure

| Finding | Concrete closure |
|---|---|
| DR-001 / PR-003 | T006 and T020/T045 are behavioral REDs for snapshot-first and execution-first insertion, both invalid temporal bounds, second-insert validation, and no-partial-insert rollback; T007/T046 are the minimal Core/EF GREEN implementation. |
| DR-002 | T005 is the package-only single integration-owner dependency task that pins StreamJsonRpc `2.25.29`, adds the Infrastructure reference, and restores/builds before T033; T036 RED → T037 GREEN is the sole production composition transition. |
| DR-003 | T015 and T019 are explicit serialized single-integration-owner shared-contract writers; T017 is identity-only and explicitly prohibited from editing `AnalysisRuntimeContracts.cs`; T023, T029, T037, T046, and T049 own their respective atomic-commit, composition, and migration paths. Test authors retain separate RED ownership. |
| DR-004 | The dependency graph makes T045→T046 the migration/model prerequisite before T028/T029; T047/T048 run only after the schema and behavior are GREEN. |
| DR-005 | T038–T040 and T047–T048 are explicitly post-GREEN qualification tasks with no expected RED; behavioral RED/GREEN remains in T030–T037, T045–T046, and the focused seams. |
| DR-006 | T036 observes ordinary-startup integrity execution, bounded readiness exposure, no index/import, management availability, and closed Worker dispatch; T037 implements the startup execution and dispatch gate. |
| DR-007 | T054 is read-only and writes no task artifact; final ordering freezes tasks, then advances to root-owned convergence, independent quality review, conditional E2E, and final verification without a duplicate definition-review gate. |

### Author self-review and hook disposition

- Source-reference coverage: PASS. All intended Core, Infrastructure, Shared, Web, composition, migration, fixture, and test paths are concrete; accepted plan ownership and exclusions are named.
- Requirement coverage: PASS. FR-001–FR-032 and SC-001–SC-009 are each mapped; PR-003 is covered in both insertion orders with rollback evidence.
- Dependency/ownership ordering: PASS. StreamJsonRpc precedes the JDT adapter; the migration/model slice is a prerequisite for real restart/concurrency; all shared integration paths are serialized under the single integration owner.
- TDD evidence integrity: PASS. New behavior has RED→minimal GREEN; post-GREEN real-tool/database qualification is explicitly no-expected-RED.
- Runtime behavior: PASS. Ordinary startup integrity execution/readiness exposure and fail-closed Worker dispatch are explicit.
- Workflow handoff: PASS. The frozen task boundary leads to convergence, quality review, conditional E2E, and final verification; no worker mutates tasks.md during implementation.
- Placeholder scan: PASS. No template task identifiers, unfinished-work markers, or unresolved clarification remains.
- Extension hooks: `.specify/extensions.yml` has no `hooks.before_tasks` or `hooks.after_tasks`; the existing optional `after_specify`/`after_plan` hooks do not apply to task generation and were skipped.

## Explicit exclusions

This feature does not implement or test the complete Java/XML PreScan, Prepared Scan Manifest, File Code Unit selection/splitting, Worker generation or dispatch policy, `navigate_code`, Cross-Repository Resolution, Pass 2, Chat retrieval, production progress/pause/resume UI, or final release evidence. Those belong to Stories 12.2–12.5. Historical Story 11.5, `specs/008-demo-chat-release-gate`, Backup B, CodeGraphContext/Python qualification, and checkpoint `d91772004cf6ad56d4fcd38b37917958733f693e` are excluded from acceptance.

## Implementation strategy

Deliver the P1 MVP through the migration prerequisite T045-T046 plus durable state/recovery foundation (T006-T029) and runtime readiness boundary (T016-T040), then add bounded analysis security (T041-T044), migration qualification (T047-T049), and final integrated verification (T050-T054). Each new observable behavior is test-first; post-GREEN real-tool/database qualification, fixtures, documentation, and verification are explicitly labeled with no fabricated RED evidence. After the frozen task boundary, the root advances through convergence, quality review, the conditional E2E decision, and final verification.

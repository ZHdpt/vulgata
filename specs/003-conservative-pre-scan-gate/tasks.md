# Tasks: Conservative Pre-Scan Gate

**Input**: Accepted design documents from `specs/003-conservative-pre-scan-gate/`

**Boundary**: Base commit `e7597082bf6acd4e7dfa05a56d393deae787a3d4`; accepted plan bundle SHA-256 `32dbd8130f68051942fd5f832444e987af8439fc6a239e05b1ab9e77e0dd817b`; planned-change authorization SHA-256 `5024e082868ee49503b367c6684e457c8b312226bb9b4aacdb70bc4a4cdf85ed`

**Execution level**: `expedited-mvp`

**Tests**: Every behavior group starts with an observed, transient RED test and ends with focused GREEN verification. Never stage, commit, or checkpoint a failing test.

**Organization**: Tasks are grouped by user story and bounded so each group can be implemented with RED → GREEN → REFACTOR evidence.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel after its stated dependencies because it owns different files
- **[Story]**: Maps the task to the corresponding story in `spec.md`
- Every task names its exact file path(s)

---

## Phase 1: Setup and Accepted Delivery Definition

**Purpose**: Bind the independently accepted and explicitly authorized delivery boundary before implementation.

- [ ] T001 [P] Verify `specs/003-conservative-pre-scan-gate/reviews/plan-review-3.json` is `ACCEPT` for base commit `e7597082bf6acd4e7dfa05a56d393deae787a3d4` and bundle SHA-256 `32dbd8130f68051942fd5f832444e987af8439fc6a239e05b1ab9e77e0dd817b`, and verify `specs/003-conservative-pre-scan-gate/evidence/planned-change-authorization-1.json` has SHA-256 `5024e082868ee49503b367c6684e457c8b312226bb9b4aacdb70bc4a4cdf85ed` and authorizes task generation without authorizing dispatch or waiving later gates.
- [ ] T002 [P] Run the read-only `speckit-analyze` cross-artifact check after this task list exists against `specs/003-conservative-pre-scan-gate/spec.md`, `specs/003-conservative-pre-scan-gate/plan.md`, and `specs/003-conservative-pre-scan-gate/tasks.md`; stop for correction if it finds a blocking inconsistency and do not edit the accepted spec or plan.
- [ ] T003 Create the first expedited accepted-delivery-definition checkpoint containing the activated Story 4.3 brief at `docs/bmad/implementation-artifacts/4-3-conservative-pre-scan-gate-and-communication-type-detection.md`, accepted artifacts under `specs/003-conservative-pre-scan-gate/`, plan-review acceptance, authorization evidence, analysis result, and this task list; begin implementation only after T001 and T002 pass, and retain Story 4.3 ownership of `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*AddConservativePreScanGate*` through the completed-story checkpoint.

**Checkpoint**: The accepted delivery definition is pinned; no implementation starts earlier.

---

## Phase 2: Foundational — Exact Committed-Tree Snapshot

**Purpose**: Establish one safe immutable Profiled Revision snapshot used by all profiling and detection readers.

**Critical**: This phase blocks every user story.

- [ ] T004 Add and run failing committed-snapshot tests in `tests/Vulgata.Tests/PreScanCommunicationDetectionTests.cs` proving that acquisition rejects a dirty reusable checkout; safely materializes the exact Profiled Revision from Git object data; rejects absolute, escaping, control-bearing, symlink, reparse-point, duplicate, per-entry oversized, aggregate oversized, and malformed entries; and never publishes a usable lease on failure. Record the observed RED before production changes and do not commit it.
- [ ] T005 Extend `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs` with the bounded lowercase `GitWorkingCopyResult.ProfiledRevision` and committed-snapshot lease contract, harden `src/dotnet/Vulgata.Infrastructure/Git/GitWorkingCopyService.cs` to reject dirty reusable checkouts and capture `git rev-parse --verify HEAD`, and implement guarded Git-object materialization plus acquisition-lock lifetime in `src/dotnet/Vulgata.Infrastructure/Git/CommittedRepositorySnapshotService.cs`.
- [ ] T006 Run the committed-snapshot filter in `tests/Vulgata.Tests/PreScanCommunicationDetectionTests.cs` to GREEN, including disposal/cleanup and cancellation, then refactor without changing the observed behavior.
- [ ] T007 Add and run failing integration tests in `tests/Vulgata.Tests/RepositoryProfilingTests.cs` proving CodeGraph profiling and the detector receive the same private snapshot root and Profiled Revision, while a snapshot mutation, revision advance, dirty late checkout, or tracked-byte mutation with unchanged `HEAD` fails closed before persistence. Record the observed RED and do not commit it.
- [ ] T008 Change `src/dotnet/Vulgata.Infrastructure/CodeGraph/CodeGraphContextProvider.cs` and `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs` to consume one leased committed snapshot for CodeGraphContext, language/framework readers, and later detection; verify snapshot identity immediately before persistence and release the lease only after commit/failure.
- [ ] T009 Run the snapshot-sharing and late-mutation filter in `tests/Vulgata.Tests/RepositoryProfilingTests.cs` to GREEN and verify zero partial profile or Pre-Scan Result is observable on every fail-closed path.

**Checkpoint**: All profiling inputs are bound to one immutable committed tree; mutable checkout bytes are no longer a profiling authority.

---

## Phase 3: User Story 1 — Classify Prepared Communication Evidence (Priority: P1) MVP

**Goal**: Deterministically classify only prepared RPC/HTTP evidence with closed truth levels, complete confirmed-kind truth, bounded samples, and no LLM or Human-in-the-Loop.

**Independent Test**: Run `PreScanCommunicationDetectionTests` against direct RPC/HTTP, import-only, malformed, unsafe, unsupported, duplicate, and more-than-100 both-kind fixtures; only direct supported evidence confirms, all outputs are deterministic and bounded, and the exact Profiled Revision remains authoritative.

### Domain lifecycle RED → GREEN

- [ ] T010 [US1] Add and run failing domain tests in `tests/Vulgata.Tests/PreScanCommunicationDetectionTests.cs` for the non-circular `RepositoryProfileDraft` → `IPreScanCommunicationDetector` → `PreScanResultDraft` → `RepositoryProfileCommitDraft` lifecycle, closed Rpc/Http and Confirmed/Hint/Warning values, confidence/rule/provenance compatibility, safe Evidence Locator bounds, and identity/Profiled Revision invariants. Record the observed RED and do not commit it.
- [ ] T011 [US1] Implement immutable domain types and invariants in `src/dotnet/Vulgata.Core/Entities/PreScanResult.cs` and `src/dotnet/Vulgata.Core/Entities/CommunicationKindClassification.cs`, add `IPreScanCommunicationDetector` in `src/dotnet/Vulgata.Core/DomainServices/IPreScanCommunicationDetector.cs`, and add profile-only `PreScanResultDraft` plus composite `RepositoryProfileCommitDraft` in `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`.
- [ ] T012 [US1] Run the domain-lifecycle filter in `tests/Vulgata.Tests/PreScanCommunicationDetectionTests.cs` to GREEN and verify `RepositoryProfileDraft` has no detector/result dependency and no intermediate durable state.

### Closed classification RED → GREEN

- [ ] T013 [US1] Add and run failing detector tests in `tests/Vulgata.Tests/PreScanCommunicationDetectionTests.cs` for exact direct RPC and HTTP annotations/controllers/clients/configuration as Confirmed/High; package, namespace, import, and framework-only evidence as Hint/Conservative; malformed, unsafe, oversized, contradictory, truncated, and unsupported-family inputs as Warning/None or omission; exact duplicate collapse; independent hint retention; path/source/secret redaction; zero-evidence and warnings-only approval; and zero network, package execution, LLM, HITL, endpoint resolution, catalog management, deferred-family confirmation, Worker Agent tool, or dispatch behavior. Record the observed RED and do not commit it.
- [ ] T014 [US1] Add the embedded closed rule vocabulary and validated loader in `src/dotnet/Vulgata.Infrastructure/CommunicationDetection/Catalog/communication-evidence-rules.v1.json` and `src/dotnet/Vulgata.Infrastructure/CommunicationDetection/CommunicationEvidenceRuleCatalog.cs`, accepting only versioned Rpc/Http rules and closed evidence categories/tokens.
- [ ] T015 [US1] Implement bounded snapshot-only matching, normalization, duplicate aggregation, fixed confidence/provenance mapping, safe Evidence Locators, and isolated anomaly handling in `src/dotnet/Vulgata.Infrastructure/CommunicationDetection/CommunicationKindDetector.cs`; never retain matched source text or attacker-controlled discriminators.
- [ ] T016 [US1] Run the closed-classification filter in `tests/Vulgata.Tests/PreScanCommunicationDetectionTests.cs` to GREEN and confirm unsupported MQ, file/storage, page-navigation, object-flow, Service Topology, provider/consumer, endpoint, REFERENCES, Uncertainty, and Cross-Repository Resolution behavior remains absent.

### Complete truth and bounded sampling RED → GREEN

- [ ] T017 [US1] Add and run a failing adversarial test in `tests/Vulgata.Tests/PreScanCommunicationDetectionTests.cs` with more than 100 direct matches across both Rpc and Http plus duplicate/hint/warning evidence, asserting authoritative pre-truncation confirmed flags and counts, a streaming ordinal SHA-256 over every normalized pre-truncation locator plus Profiled Revision/flags/counts, deterministic maximum-100 sampling, one reserved direct confirmed sample per confirmed kind, one reserved `BoundExceeded` warning when truncated, and identical output across input-order permutations. Record the observed RED and do not commit it.
- [ ] T018 [US1] Implement authoritative `ConfirmedCommunicationKinds`, pre-truncation totals, streaming canonical fingerprinting, deterministic ordering/reservation/fill, `OccurrenceCount`, and the 100-sample cap in `src/dotnet/Vulgata.Infrastructure/CommunicationDetection/CommunicationKindDetector.cs` without deriving confirmed truth from retained samples.
- [ ] T019 [US1] Run the adversarial sampling/fingerprint filter in `tests/Vulgata.Tests/PreScanCommunicationDetectionTests.cs` to GREEN and verify Rpc and Http both remain confirmed and auditable after truncation.

**Checkpoint**: User Story 1 is independently complete and is the expedited MVP classification increment.

---

## Phase 4: User Story 2 — Publish One Approved Pre-Scan Handoff (Priority: P1)

**Goal**: Atomically commit one immutable approved result with the profile and expose a typed, authorized, revision-bound handoff through existing routes only.

**Independent Test**: Persist, retry, and retrieve confirmed, warning-only, and truncated both-kind outcomes. Verify one atomic logical result, complete confirmed evidence projection, current authorization checks, exact revision binding, no partial rows, no new route, and no dispatch side effect.

### Atomic profile/result persistence RED → GREEN

- [ ] T020 [US2] Add and run failing integration tests in `tests/Vulgata.Tests/RepositoryProfilingTests.cs` for one composite profile/result transaction, `pre_scan_approved` direct transition, same-fingerprint idempotent replay, different-fingerprint fail-closed replay, provider/detector/persistence failure rollback, Profiled Revision equality across ScanRun/profile/result/fingerprint, and zero Worker Agent jobs, dispatches, REFERENCES Edges, Uncertainties, endpoint resolutions, or deferred-family facts. Record the observed RED and do not commit it.
- [ ] T021 [US2] Add EF mappings, closed-value checks, one-to-one/child ownership, cascade, uniqueness, compatibility constraints, and deterministic materialization indexes in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/PreScanResultConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/CommunicationKindClassificationConfiguration.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`.
- [ ] T022 [US2] Extend the specific contract in `src/dotnet/Vulgata.Core/DomainServices/IScanRunRepository.cs`, implement atomic composite persistence and replay fingerprint checks in `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, and invoke detection plus `RepositoryProfileCommitDraft` persistence only after a complete profile in `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`.
- [ ] T023 [US2] Run the `RepositoryProfilingTests` PreScan persistence filter to GREEN and verify an injected final-commit failure leaves zero profile/result/classification rows while same-fingerprint retry yields one logical immutable result.

### Additive migration RED → GREEN

- [ ] T024 [P] [US2] Add and run failing model/migration tests in `tests/Vulgata.Tests/MigrationTests.cs` under class `PreScanMigrationTests` for the new tables, PK/FK ownership, unique `(RepositoryId, ScanRunId)` result, unique `(PreScanResultId, Fingerprint)` child, closed-state/schema/revision/flag/count/path/locator/occurrence compatibility checks, rollback atomicity, and no unrelated Document, retrieval, graph, endpoint, dispatch, or uncertainty table. Record the observed RED and do not commit it.
- [ ] T025 [P] [US2] Generate the additive `AddConservativePreScanGate` migration files under `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_AddConservativePreScanGate.cs` and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_AddConservativePreScanGate.Designer.cs`, and update `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`; preserve Story 4.3 exclusive ownership of these paths through its completed-story checkpoint.
- [ ] T026 [US2] Run `PreScanMigrationTests` in `tests/Vulgata.Tests/MigrationTests.cs` to GREEN on the test provider and verify the model has no unintended table or relationship.

### Typed authorized handoff RED → GREEN

- [ ] T027 [P] [US2] Add and run failing handoff/application tests in `tests/Vulgata.Tests/PreScanHandoffTests.cs` plus existing-route composition tests in `tests/Vulgata.Tests/RepositoryApiTests.cs` for all schema `1.0` fields; confirmed kinds derived from flags; bounded ordered `confirmedEvidence`, hints, and warnings; one reserved confirmed sample per kind after truncation; stable original Profiled Revision after checkout advancement; downstream requested-revision mismatch failure; wrong-System, hidden, revoked-after-load, missing, unauthenticated, and non-ManagementAccess non-disclosure; and zero new public route. Record the observed RED and do not commit it.
- [ ] T028 [P] [US2] Add immutable `ApprovedPreScanHandoff` materialization and Profiled Revision validation in `src/dotnet/Vulgata.Core/DomainServices/ApprovedPreScanHandoff.cs`, extend the materialized query in `src/dotnet/Vulgata.Core/DomainServices/IScanRunRepository.cs` and `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, and add the bounded transport projection including `confirmedEvidence` in `src/dotnet/Vulgata.Shared/Repositories/ApprovedPreScanHandoffDto.cs`.
- [ ] T029 [US2] Implement system-scoped and standalone pre/post-load visibility checks, schema/revision fail-closed mapping, and redaction in `src/dotnet/Vulgata.Web/Data/PreScanHandoffService.cs`; register it in `src/dotnet/Vulgata.Web/Program.cs` and compose it only into the existing GET handlers in `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs` under `ManagementAccess`.
- [ ] T030 [US2] Run `PreScanHandoffTests` and the PreScan subset of `RepositoryApiTests` to GREEN, proving no absolute/UNC/host path, working-copy root, source, secret, raw diagnostic/provider payload, internal database ID, entity fingerprint, existence timing, or arbitrary discriminator is returned.

**Checkpoint**: User Story 2 independently supplies one durable, authorized, revision-bound Pre-Scan Handoff and grants no dispatch authority.

---

## Phase 5: User Story 3 — Review a Safe Conservative Outcome (Priority: P2)

**Goal**: Durably expose the exact bounded Simplified-Chinese approval outcome and structurally safe logs under current visibility.

**Independent Test**: Approve warning-bearing and warning-free results, inspect persistence/log capture/authorized summaries, then revoke visibility; exact messages persist, logs contain only closed identifiers/counts/rule-set version, and the revoked user learns nothing.

### Chinese observability and redaction RED → GREEN

- [ ] T031 [US3] Add and run failing tests in `tests/Vulgata.Tests/PreScanHandoffTests.cs` and `tests/Vulgata.Tests/RepositoryProfilingTests.cs` asserting exact warning message `预扫描已批准并保留审慎警告——继续进入顺序演示运行器`, exact warning-free message `预扫描已批准——继续进入顺序演示运行器`, durable ScanRun storage, safe structured logs limited to RepositoryId/ScanRunId/state/schema/counts/rule-set version, bounded closed warning data, and zero disclosure after visibility revocation. Record the observed RED and do not commit it.
- [ ] T032 [US3] Persist the closed approval message on `src/dotnet/Vulgata.Core/Entities/ScanRun.cs`, select it and emit identifier/count-only structured logging in `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, and preserve post-load redaction in `src/dotnet/Vulgata.Web/Data/PreScanHandoffService.cs`.
- [ ] T033 [US3] Run the Chinese observability and revoked-visibility filters in `tests/Vulgata.Tests/PreScanHandoffTests.cs` and `tests/Vulgata.Tests/RepositoryProfilingTests.cs` to GREEN and inspect captured logs for source, secret, host path, raw exception, or unbounded data.

**Checkpoint**: All three user stories are independently testable and the accepted expedited-MVP behavior is complete.

---

## Phase 6: Verification and Cross-Cutting Gates

**Purpose**: Verify the current Story 4.3 boundary without expanding into deferred families or Story 11 dispatch.

- [ ] T034 Run `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`, the focused `PreScanCommunicationDetectionTests`, `RepositoryProfilingTests` PreScan filter, `PreScanHandoffTests`, `PreScanMigrationTests`, and full `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj`; all must pass with no analyzer warnings and no committed failing test, using `specs/003-conservative-pre-scan-gate/quickstart.md` as the expected-result authority.
- [ ] T035 Validate PostgreSQL migration apply, rollback to `20260720060442_FrameworkEvidenceAuditCount`, re-apply, and `dotnet ef migrations has-pending-model-changes` for `src/dotnet/Vulgata.Infrastructure/Vulgata.Infrastructure.csproj` with startup `src/dotnet/Vulgata.Web/Vulgata.Web.csproj`, then run current-boundary profiling/persistence/read integration from `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/PreScanHandoffTests.cs`, and `tests/Vulgata.Tests/MigrationTests.cs`.
- [ ] T036 [P] Inspect the implemented diff for changes to `docker/Dockerfile`, `docker-compose.yml`, `src/dotnet/Vulgata.Web/Program.cs` startup/runtime behavior, or container dependencies; if none exist, record the explicit no-trigger Docker decision in `docs/bmad/implementation-artifacts/4-3-conservative-pre-scan-gate-and-communication-type-detection.md`, otherwise run the Story 4.3 Docker build and record its result there. The clean-database two-Repository rehearsal and unconditional final Docker build remain Story 11.5 responsibilities.

---

## Dependencies and Execution Order

### Phase dependencies

- **Setup (T001–T003)**: T001 and T002 may run in parallel; T003 depends on both and blocks implementation.
- **Foundational snapshot (T004–T009)**: Depends on T003; execute each RED → implementation → GREEN group in order and complete the phase before any user story.
- **US1 (T010–T019)**: Depends on T009; lifecycle precedes classification, which precedes sampling.
- **US2 (T020–T030)**: Depends on T019; atomic persistence must be GREEN before migration and handoff groups. After T023, migration RED work (T024) and handoff RED work (T027) may proceed in parallel; T025 depends on T024, T026 depends on T025, T028 depends on T027, T029 depends on T028, and T030 depends on T029.
- **US3 (T031–T033)**: Depends on T030 because it reuses the persisted result and authorized service.
- **Verification (T034–T036)**: Depends on T026 and T033; T036 may run beside T034, while T035 follows the generated migration and must finish before review.

### User story completion order

```text
Accepted delivery definition
  -> committed-tree foundation
  -> US1 deterministic classification
  -> US2 atomic persistence + handoff
  -> US3 safe Chinese outcome
  -> current-boundary verification
```

- **US1** has no dependency on another user story after the foundation and is the classification MVP.
- **US2** consumes US1's complete immutable draft and authoritative flags/fingerprint.
- **US3** consumes US2's durable result and authorization service.
- No task may introduce a new public route, Worker Agent tool/job/dispatch, Scan Coordinator, LLM/HITL/manual approval, endpoint resolution, Communication Pattern Catalog management, deferred communication family, REFERENCES Edge, Uncertainty, or Cross-Repository Resolution.

### Shared-file ownership

- Story 4.3 exclusively owns `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_AddConservativePreScanGate*` from T003 through migration verification, combined quality review, acceptance, and the completed-story checkpoint.
- Story 11.1 must not mutate those paths during this interval and must branch/rebase from the exact published Story 4.3 completed-story checkpoint before creating its migration.

---

## Parallel Examples

### Accepted boundary

```text
Task T001: Verify plan-review and planned-change authorization evidence.
Task T002: Run read-only cross-artifact analysis over spec, plan, and tasks.
```

### User Story 2 after T023

```text
Migration lane:
  T024 RED migration/model tests -> T025 migration -> T026 GREEN

Handoff lane:
  T027 RED handoff/API tests -> T028 Core/Infrastructure/Shared projection
  -> T029 Web authorization/existing-route composition -> T030 GREEN
```

These lanes own different files except that Story 4.3 remains the sole owner of all EF migration/snapshot work; merge and verify both lanes before T031.

---

## Expedited-MVP Implementation Strategy

1. Pin the accepted delivery definition with T001–T003.
2. Complete the committed-tree foundation with two bounded RED → GREEN groups.
3. Deliver US1 in three bounded RED → GREEN groups: domain lifecycle, closed classification, and deterministic complete-truth sampling.
4. Deliver US2 in three bounded groups: atomic persistence, migration, and authorized handoff; parallelize only the migration and handoff lanes after persistence is GREEN.
5. Deliver US3 as one bounded RED → GREEN observability group.
6. Run current-boundary build, focused/full tests, PostgreSQL migration validation, integration coverage, and the conditional Docker decision.
7. Hand the pinned implementation and RED/GREEN evidence to one fresh combined quality reviewer, then perform the conditional E2E decision and final verification required by `expedited-mvp`; do not claim deferred Epic 5–10 or Story 11 behavior.

## Notes

- RED evidence must identify the focused command, expected missing behavior, actual failure, and boundary; it is transient and must never be committed.
- A GREEN task includes focused regression and refactor verification for its preceding behavior group.
- Evidence remains concise: command, exit code, observed boundary/commit, timestamp, and result; retain raw output only for failures, security, migrations, or runtime contracts.
- Do not edit the accepted `spec.md` or `plan.md` during implementation. A material contradiction returns to correction and fresh authorization when required.

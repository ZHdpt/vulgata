# Tasks: Story 4.3 Architecture Remediation

**Input**: Accepted July 31 correction plan under `specs/003-conservative-pre-scan-gate/`

**Execution level**: `expedited-mvp`

**Scope**: Correction-only closure of the three reserved Epic 4 architecture findings at source commit `1b9c98bb4999e254ab19704643477732f92c842c`. The medium catalog-locality refactor, new product behavior, migrations, Worker Agent tools, dispatch, scheduling, retry, and provider failover are excluded.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: May run concurrently because the task owns different files and has no dependency on an incomplete task.
- **[US1]**: Restores deterministic framework evidence over the verified committed Repository snapshot.
- **[US2]**: Restores mandatory Communication Kind Classification and atomic profile-to-approval persistence.
- Every test task must be observed RED before its paired production task; no failing state may be staged or committed.

## Phase 1: Accepted Correction Boundary

**Purpose**: Pin the authorized remediation before any implementation work.

- [x] T001 Verify `specs/003-conservative-pre-scan-gate/reviews/plan-review-4.json` is `ACCEPT`, `specs/003-conservative-pre-scan-gate/evidence/planned-change-authorization-2.json` authorizes task generation at commit `1b9c98bb4999e254ab19704643477732f92c842c`, and `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-31-epic-4-architecture-remediation.md` reserves exactly the three HIGH/MUST/Strong findings; stop if any boundary differs and do not edit those files.

---

## Phase 2: Remediation RED Baseline

**Purpose**: Make every reserved defect and the production composition gap fail observably before production edits.

- [ ] T002 [P] [US1] Add and run `ArchiveOnlyLease_*` tests in `tests/Vulgata.Tests/FrameworkDetectionArchiveLeaseTests.cs` through public `ICommittedRepositorySnapshotService` and `IFrameworkEvidenceDetector`, proving the real materialized lease has no `.git`, the current detector fails on that archive representation, lease identity is checked around reads, unsafe or mutated identity fails closed, and expected bounded framework facts are the required GREEN outcome; retain the observed RED output without staging or committing it.
- [ ] T003 [P] [US2] Add and run `PublicConstructors_*` and `FinalPersistenceContract_*` reflection tests in `tests/Vulgata.Tests/ArchitectureRemediationContractTests.cs`, proving the current public `RepositoryProfilingCoordinator` surface contains a constructor missing `IPreScanCommunicationDetector` or `ICommittedRepositorySnapshotService`, the empty/missing adapters remain discoverable, and `IScanRunRepository` exposes a `RepositoryProfileDraft` final persistence overload; retain the observed RED output without staging or committing it.
- [ ] T004 [P] [US2] Replace the manual-only happy path with a failing `ProductionComposition_*` regression in `tests/Vulgata.Tests/Story43EndToEndTests.cs` that resolves `IRepositoryProfilingCoordinator` from the Web composition root, uses a clean temporary database and two committed Repositories, replaces only external `ICodeGraphProvider`, and requires real snapshot, framework, communication, and persistence interfaces to produce Profiled Revision-bound framework/RPC/HTTP facts plus one atomically visible approved Pre-Scan Result per ScanRun; retain the observed RED output without staging or committing it.

**Checkpoint**: T002, T003, and T004 each fail for the corresponding pre-correction defect, not for fixture, build, or environment errors.

---

## Phase 3: User Story 1 — Archive-Only Framework Detection (Priority: P1)

**Goal**: Framework evidence consumes the live verified committed snapshot lease and never treats `RootPath` as a Git working tree.

**Independent Test**: The archive-only lease contains no `.git`, yet the public framework detector returns the expected bounded facts for its exact Profiled Revision, verifies identity before and after reads, and fails closed after mutation without invoking Git under `RootPath`.

- [ ] T005 [US1] Change `IFrameworkEvidenceDetector.DetectAsync` and `DetectDetailedAsync` in `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs` to require `ICommittedRepositorySnapshotLease`, retaining the existing closed result and applicability contracts.
- [ ] T006 [US1] Rework `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs` to reject null/disposed/identity-invalid leases, verify identity before and after bounded reads, inventory containment-checked regular files directly under the live lease `RootPath`, preserve existing path/artifact/aggregate/record/parse/audit/redaction bounds, and remove all Git identity, inventory, and blob operations from the archive detector path.
- [ ] T007 [US1] Run `FrameworkDetectionArchiveLeaseTests` in `tests/Vulgata.Tests/FrameworkDetectionArchiveLeaseTests.cs` to GREEN and confirm success without `.git`, exact Profiled Revision binding, before/after identity verification, mutation failure with no result, bounded diagnostics, and zero Git command against the archive `RootPath`.

**Checkpoint**: `epic4-framework-snapshot-representation-mismatch` has behavior-level RED/GREEN evidence through the public interfaces.

---

## Phase 4: User Story 2 — Required Detection and Atomic Final Persistence (Priority: P1)

**Goal**: No public construction or repository seam can approve without real detection or durably complete a profile without its Pre-Scan Result.

**Independent Test**: Reflection finds only constructors requiring both real seams and exactly one composite final persistence method; focused integration proves rollback, same-fingerprint replay, conflicting replay rejection, redaction, and idempotency remain intact.

- [ ] T008 [US2] Update `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs` to pass the live `ICommittedRepositorySnapshotLease` into framework detection, keep the coordinator identity verification immediately before atomic persistence, remove the two bypass constructors plus `EmptyPreScanCommunicationDetector` and `MissingCommittedRepositorySnapshotService`, and retain only public constructors that explicitly require both `ICommittedRepositorySnapshotService` and `IPreScanCommunicationDetector`.
- [ ] T009 [US2] Remove the `RepositoryProfileDraft` overload from `src/dotnet/Vulgata.Core/DomainServices/IScanRunRepository.cs` and `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, leaving `PersistCompletedProfileAsync(RepositoryProfileCommitDraft, CancellationToken)` as the sole final profile operation while preserving its single transaction, rollback, same-fingerprint no-op replay, conflicting-replay failure, recovery, and materialized handoff reads.
- [ ] T010 [US2] Update every affected explicit real/fake framework, snapshot, detector, and Scan Run repository seam in `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, including decorators that observe late failure, persistence, recovery, and snapshot lifetime; remove profile-only fake methods and pass the lease through all `IFrameworkEvidenceDetector` fakes.
- [ ] T011 [US2] Update constructor callers and `IScanRunRepository` fakes in `tests/Vulgata.Tests/RepositoryApiTests.cs` and `tests/Vulgata.Tests/PreScanHandoffTests.cs` to supply explicit required real/fake snapshot and Communication Kind Classification seams and to implement only the composite final persistence contract.
- [ ] T012 [US2] Run `ArchitectureRemediationContractTests` in `tests/Vulgata.Tests/ArchitectureRemediationContractTests.cs` plus the Pre-Scan, rollback, replay, late-mutation, and recovery filters in `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/PreScanHandoffTests.cs` to GREEN, proving no bypass adapter/type/constructor or profile-only overload remains and security, redaction, atomicity, idempotency, and fail-closed Profiled Revision behavior are unchanged.

**Checkpoint**: `epic4-empty-prescan-compatibility-path` and `epic4-profile-only-persistence-path` have contract-level RED/GREEN evidence, and all callers compile against explicit required seams.

---

## Phase 5: Production Composition Closure

**Goal**: Exercise the corrected path through the Web production composition root.

**Independent Test**: Two clean committed Repositories complete through the resolved production coordinator with only CodeGraph replaced; their archive-only snapshots, framework and Communication Kind Classification facts, Profiled Revisions, and atomic Pre-Scan Results remain isolated and correct.

- [ ] T013 [US2] Run the `ProductionComposition_*` regression in `tests/Vulgata.Tests/Story43EndToEndTests.cs` to GREEN through the registrations in `src/dotnet/Vulgata.Web/Program.cs`, verifying only `ICodeGraphProvider` is replaced, all four real interfaces are crossed, each archive has no `.git`, framework and RPC/HTTP facts share the exact Profiled Revision, profile plus Pre-Scan Result visibility is atomic, and no Worker Agent job, dispatch, Edge, Uncertainty, endpoint resolution, or Cross-Repository Resolution is created.

**Checkpoint**: Production composition supplies shared closure evidence for all three reserved findings.

---

## Phase 6: Verification and Review/E2E Handoff

**Purpose**: Prove migration neutrality and deliver a clean expedited-MVP review boundary.

- [ ] T014 Run `dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web` and `PreScanMigrationTests` in `tests/Vulgata.Tests/MigrationTests.cs`; require no pending model change and no diff under `src/dotnet/Vulgata.Infrastructure/Data/Migrations/`, `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, or `src/dotnet/Vulgata.Infrastructure/Data/Configurations/`.
- [ ] T015 Run the focused architecture, framework, profiling, handoff, API, and production-composition regressions in `tests/Vulgata.Tests/FrameworkDetectionArchiveLeaseTests.cs`, `tests/Vulgata.Tests/ArchitectureRemediationContractTests.cs`, `tests/Vulgata.Tests/FrameworkDetectionTests.cs`, `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/PreScanHandoffTests.cs`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/Story43EndToEndTests.cs`; require all GREEN with bounded/redacted diagnostics and no deferred catalog-locality behavior.
- [ ] T016 Run `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj` and full `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj`; require exit 0 with no analyzer/code-style failure and no failing test.
- [ ] T017 Run the clean-database two-Repository Story 4.3 E2E in `tests/Vulgata.Tests/Story43EndToEndTests.cs` and `docker build --progress=plain -f docker/Dockerfile -t vulgata:story-4-3-architecture-remediation .`; require exact Profiled Revision isolation, one approved result per ScanRun, and a successful container build.
- [ ] T018 Prepare the accepted source/test boundary for one independent combined quality review and conditional E2E handoff by returning the exact diff and RED/GREEN, migration-neutral, focused, full-build/test, clean-database two-Repository, and Docker command evidence for `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`, `src/dotnet/Vulgata.Core/DomainServices/IScanRunRepository.cs`, `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, and the changed files under `tests/Vulgata.Tests/`; do not update workflow/BMad lifecycle state, stage, or commit.

---

## Dependencies and Execution Order

### Task dependency graph

```text
T001
 ├─ T002 ─ T005 ─ T006 ─ T007 ─┐
 ├─ T003 ────────────────────────┼─ T008 ─ T009 ─ T010 ─ T011 ─ T012 ─┐
 └─ T004 ────────────────────────┘                                     ├─ T013
                                                                       └─ T014
T013 + T014 ─ T015 ─ T016 ─ T017 ─ T018
```

- T002, T003, and T004 may run in parallel after T001 because they own different test files.
- T008 depends on T007 because the coordinator must compile against the lease-based framework interface.
- T009 follows T008 so the coordinator and repository move to their final public shapes before fake/decorator cleanup.
- T010 and T011 are ordered because `tests/Vulgata.Tests/RepositoryApiTests.cs` participates in both constructor and repository fake compilation.
- T013 requires T012; it must turn the already-observed production-composition RED from T004 GREEN without replacing any additional production interface.
- T014 may run after T012 and in parallel with T013 because it is read-only and migration-neutral.
- T015 through T018 are sequential acceptance gates.

### Bounded TDD groups and file ownership

- **Group A — archive framework seam (T002, T005–T007)** owns `tests/Vulgata.Tests/FrameworkDetectionArchiveLeaseTests.cs`, `src/dotnet/Vulgata.Core/DomainServices/RepositoryProfilingContracts.cs`, and `src/dotnet/Vulgata.Infrastructure/FrameworkDetection/FrameworkEvidenceDetector.cs`.
- **Group B — mandatory construction and composite persistence (T003, T008–T012)** owns `tests/Vulgata.Tests/ArchitectureRemediationContractTests.cs`, `src/dotnet/Vulgata.Web/Data/RepositoryProfilingCoordinator.cs`, `src/dotnet/Vulgata.Core/DomainServices/IScanRunRepository.cs`, `src/dotnet/Vulgata.Infrastructure/Data/ScanRunRepository.cs`, `tests/Vulgata.Tests/RepositoryProfilingTests.cs`, `tests/Vulgata.Tests/RepositoryApiTests.cs`, and `tests/Vulgata.Tests/PreScanHandoffTests.cs`.
- **Group C — production composition (T004, T013)** owns `tests/Vulgata.Tests/Story43EndToEndTests.cs`; it writes its RED before Groups A/B production edits and resumes for GREEN only after T012.
- **Verification group (T014–T018)** owns no production file and performs only checks and evidence handoff.

The groups have disjoint write ownership. Group A completes before Group B production edits; Group C pauses between its RED and GREEN while Groups A/B correct the production seams.

## Parallel Opportunities

- T002, T003, and T004 are parallel RED tasks with disjoint files.
- After T012, T013 production-composition GREEN and T014 migration-neutral verification may run in parallel.
- No production implementation tasks are marked parallel because the public interface changes compile through the coordinator and its fakes in dependency order.

## Implementation Strategy

1. Pin the accepted correction boundary with T001.
2. Observe all three discriminating RED surfaces with T002–T004.
3. Complete Group A and independently validate archive-only framework detection.
4. Complete Group B and independently validate required construction plus sole atomic persistence.
5. Complete Group C through the Web production composition root.
6. Run migration-neutral, focused, full, E2E, Docker, and combined-review handoff gates.

## Completion Criteria

- All three reserved audit findings have explicit observed RED and GREEN tasks.
- Production composition crosses real snapshot, framework, communication, and persistence interfaces and replaces only CodeGraph.
- Every public coordinator constructor requires the snapshot and Pre-Scan detector seams.
- The only final profile persistence method accepts `RepositoryProfileCommitDraft`.
- No migration/model, PRD/spec/plan, catalog-locality, Worker Agent, dispatch, scheduling, retry, provider-failover, Edge, Uncertainty, endpoint, or Cross-Repository Resolution scope is added.
- Security, redaction, atomicity, idempotency, Profiled Revision binding, focused/full tests, clean-database two-Repository E2E, Docker, and combined review remain mandatory.

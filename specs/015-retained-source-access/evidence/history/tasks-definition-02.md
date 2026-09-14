---
description: "Dependency-ordered tasks for Story 13.1 retained source access"
---

# Tasks: Retained Source Access

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, and both files under `contracts/`.

**Tests**: TDD is mandatory. Each observable behavior has one public-seam RED task, the minimal production GREEN task(s) needed for that result, and focused/surrounding verification before the next behavior. All tests use `tests/Vulgata.Tests/Vulgata.Tests.csproj`; no new test project is planned.

**Scope**: Exact retained source, scoped relative reads, durable publication/reopen, live authorization, safe cleanup, and source-only evidence. No CodeGraph/LSP/LLM/semantic Scan/Knowledge/Chat/Docker/backup/restore feature, public Snapshot UI, Worker Agent tool, or wholesale pair migration.

## Phase 1: Setup

- [ ] T001 [P] Create isolated local-Git and persistent-source-storage fixture helpers in `tests/Vulgata.Tests/RetainedSource/RetainedSourceFixture.cs`, with no CodeGraph/LSP/LLM/semantic-store dependency (quickstart.md; SC-009).
- [ ] T002 [P] Add source-only evidence labels and process-launch utilities in `tests/Vulgata.Tests/RetainedSource/RetainedSourceTestFixtures.cs`, targeting the existing `tests/Vulgata.Tests/Vulgata.Tests.csproj` test host (FR-021/022; SC-009/010).
- [ ] T003 [P] Add public contract-shape RED assertions in `tests/Vulgata.Tests/RetainedSource/RetainedSourceApplicationContractTests.cs` for capture arguments (Repository/ref/cancellation only), ordinary logical paths/results, and absence of roots, credentials, storage keys, and tokens (FR-004/012; contract file).

## Phase 2: Core contracts and policy slices

**Purpose**: Declare logical contracts, then complete each policy/codec slice before capture depends on it. Physical workspace access stays entirely in Infrastructure.

- [ ] T004 Define `SourceSnapshot` identity/outcome values in `src/dotnet/Vulgata.Core/Entities/SourceSnapshot.cs` and `src/dotnet/Vulgata.Core/DomainServices/RetainedSourceContracts.cs`, including lower-case 40/64-character revisions, `sha1`/`sha256`, manifest identity, counts, and sanitized statuses (FR-003/004/019; data-model.md).
- [ ] T005 Define acquire/reopen/grant/relative-list/range/text and logical authorization contracts in `src/dotnet/Vulgata.Core/DomainServices/IRetainedSourceRepository.cs` and `src/dotnet/Vulgata.Core/DomainServices/RetainedSourceContracts.cs`; do not define a workspace/provider contract in Core (FR-006/007/012; DR-13.1-002).
- [ ] T006 Add public-seam RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePolicyTests.cs` for every `SourceStorage:*` bound, immutable startup policy, caller-unraiseable limits, and fail-closed `source_configuration_invalid` registration (plan §8; PR-13.1-001).
- [ ] T007 Implement policy binding/validation in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCapturePolicy.cs`, `src/dotnet/Vulgata.Infrastructure/DependencyInjection.cs`, and `src/dotnet/Vulgata.Web/Program.cs`; run T006 focused and surrounding startup tests to establish GREEN (plan §8; PR-13.1-001).
- [ ] T008 Add public capture/open RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceManifestTests.cs` using the application contract to prove canonical manifest bytes, raw-path ordering, count/byte sums, object-format/revision agreement, and explicit unsupported entries (FR-004/008/009; SC-001/003).
- [ ] T009 Implement canonical manifest codec in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceManifestCodec.cs` from `specs/015-retained-source-access/contracts/retained-source-manifest-v1.schema.json`, then run T008 plus surrounding capture/open tests for GREEN (data-model.md; DR-13.1-001).

## Phase 3: User Story 1 — Capture an Exact Source Snapshot (P1, MVP)

**Goal**: Authorized System-member and standalone Repositories resolve one full commit, capture raw committed bytes, and expose only verified completion.

- [ ] T010 [US1] Add RED public capture tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureTests.cs` for live authority before Git, one full-ref resolution, Repository+ProfiledRevision identity, raw bytes after ref/working-copy changes, and standalone capture without pair/System identity (FR-001–004; SC-001/002/003).
- [ ] T011 [US1] Implement bounded raw Git object acquisition in `src/dotnet/Vulgata.Infrastructure/Source/GitCommittedObjectReader.cs`, reusing safe process/cancellation patterns from `src/dotnet/Vulgata.Infrastructure/Git/GitWorkingCopyService.cs`, and run T010 focused tests to GREEN for resolution/raw-byte behavior (FR-003/008).
- [ ] T012 [US1] Add RED public failure tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureTests.cs` for auth/ref/transport failures, configured entry/blob/aggregate limits, cancellation/deadline, owned staging cleanup, sanitized outcomes, and no completed partial capture (FR-005/014/019/020; SC-005/008).
- [ ] T013 [US1] Implement flat blob/manifest staging and verification in `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, including raw paths, strict UTF-8 paths, modes/object IDs, symlink/gitlink/unsafe limitations, streaming bounds, manifest-last verification, and disposable cleanup; run T012 focused/surrounding tests to GREEN (FR-004/005/009/014).
- [ ] T014 [US1] Add RED authorization tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceAuthorizationTests.cs` for Administrator/SystemOwner/standalone authority, origin/System binding changes, missing subject/verifier, and pre-publication revocation (FR-001/002/010/019; SC-002/004).
- [ ] T015 [US1] Implement `SourceRepositoryAuthorizationVerifier` in `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs`, deriving `IUserContext` subject and rechecking Repository/role/assignment/origin/revision; run T014 focused and surrounding capture tests to GREEN (plan AD-4).
- [ ] T016 [US1] Add RED persistence-shape tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs` for `SourceSnapshots`, immutable fields, uniqueness, fixed digest/schema/object-format constraints, non-negative counts, and Repository `Restrict` (FR-004/017/018).
- [ ] T017 [US1] Implement EF entity configuration in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/SourceSnapshotConfiguration.cs` and `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`; run T016 focused model checks to GREEN before publication depends on the row shape (data-model.md).
- [ ] T018 [US1] Add RED public deletion-conflict tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs` for the existing Repository delete route/coordinator returning sanitized Chinese `409` when retained source is referenced (FR-017/018).
- [ ] T019 [US1] Implement that approved conflict mapping in `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs` and `src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs`; run T018 and surrounding Repository-management tests to GREEN without adding DTO/UI/schema (plan AD-1; DR-13.1-001).
- [ ] T020 [US1] Add the additive migration `src/dotnet/Vulgata.Infrastructure/Data/Migrations/<timestamp>_AddRetainedSourceSnapshots.cs`, changing only `SourceSnapshots` and its constraints; run migration/delete checks from `tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs` to GREEN (FR-017/018).
- [ ] T021 [US1] Add RED public completion tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureTests.cs` for atomic completion only after verified staging, Repository/revision identity, and explicit `Completed`/`Replay`/failure outcomes (FR-004/005/019; SC-001/005).
- [ ] T022 [US1] Implement `RetainedSourceRepository` in `src/dotnet/Vulgata.Infrastructure/Data/RetainedSourceRepository.cs` and capture orchestration in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`, composing T007/T009/T011/T013/T015/T017/T019/T020 and non-cancellable row linearization; run T021 plus T010/T012/T014 surrounding suites to GREEN (FR-001–005/014/019/020).
- [ ] T023 [US1] Add additive registration in `src/dotnet/Vulgata.Infrastructure/DependencyInjection.cs` and `src/dotnet/Vulgata.Web/Program.cs` without a public Snapshot route or legacy pair replacement, then run the complete US1 focused suite and record SC-001/002/005/008 source-only evidence.

## Phase 4: User Story 2 — Read Retained Source Through a Scoped Grant (P1)

**Goal**: Fresh live-authorized contexts provide bounded relative metadata/content with cumulative budgets, deadlines, cancellation, and explicit limitations.

- [ ] T024 [US2] Add one RED public grant/list/metadata test group in `tests/Vulgata.Tests/RetainedSource/RetainedSourceReadContextTests.cs` for fresh authority, Repository/revision/snapshot binding, bounded listing, scope-bound cursors, exact relative containment, and unsupported entries (FR-006/007/009/011; SC-003/004).
- [ ] T025 [US2] Implement cumulative grant/deadline reservations in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceReadContext.cs`; run T024 focused and surrounding grant tests to GREEN (FR-006/013/014).
- [ ] T026 [US2] Add one RED public byte/text/budget test group in `tests/Vulgata.Tests/RetainedSource/RetainedSourceReadContextTests.cs` for exact ranges, strict UTF-8, concurrent atomic reservations, exact exhaustion, monotonic deadline, cancellation, no partial payload, and no automatic refresh (FR-006/008/012/013/014; SC-003/005).
- [ ] T027 [US2] Implement reopen verification and relative metadata/list/range/text operations in `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs` and `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceReadContext.cs`; run T026 and surrounding capture/open tests to GREEN (FR-006–009/011/012).
- [ ] T028 [US2] Add RED live-denial/disclosure tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceAuthorizationTests.cs` for expiry/revocation, Repository/revision mismatch, absolute/backslash/dot/NUL/control/escaping/outside paths, and absence of credentials/physical paths (FR-010–012; SC-004).
- [ ] T029 [US2] Wire grant opening and every-operation authority recheck in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs` and `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs`; run T028 plus T024–T027 surrounding suites to GREEN (FR-006/010/019/020).
- [ ] T030 [US2] Run the focused US2 suite and capture regressions, recording RED→GREEN evidence for SC-003/004/005 and confirming ordinary consumers never receive trusted workspace paths.

## Phase 5: User Story 3 — Reopen Durable Source Safely (P1)

**Goal**: Stable publication, concurrent retry/adoption/cleanup, and fresh-process reopen survive lease/process/remote loss.

- [ ] T031 [US3] Add RED publication/concurrency tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePersistenceTests.cs` for same-identity replay, cross-process lock, atomic seal/adopt/row insert, crash between rename/insert, stale cleanup, digest conflict, no partial visibility, concurrent readers, lease release, no pruning, and fresh-authority retry (FR-005/014/017–020; SC-006/007/008).
- [ ] T032 [US3] Implement repository publication and immutable row visibility in `src/dotnet/Vulgata.Infrastructure/Data/RetainedSourceRepository.cs`, then run T031 focused publication tests plus PostgreSQL surrounding tests to GREEN (FR-004/005/015/017/018).
- [ ] T033 [US3] Implement identity locks, same-volume seal/adopt, crash recovery, stale-operation cleanup, and digest conflicts in `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs` and `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`; run T031 and capture regressions to GREEN (FR-005/014/017–020).
- [ ] T034 [US3] Add RED trusted-workspace boundary tests in `tests/Vulgata.Tests/RetainedSource/TrustedSourceWorkspaceTests.cs` proving no Core/ordinary workspace contract, separate Infrastructure authorization, contained materialization, refusal of symlink/gitlink/unsafe/colliding paths, and owned cleanup (FR-012/014/016; plan AD-6).
- [ ] T035 [US3] Implement `src/dotnet/Vulgata.Infrastructure/Source/TrustedSourceWorkspaceProvider.cs` for separately authorized Infrastructure adapters only; run T034 focused and surrounding source-read tests to GREEN (FR-012/014/016).
- [ ] T036 [US3] Add RED fresh-process tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessTests.cs` that launch `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --filter FullyQualifiedName~RetainedSourceSeparateProcessChild` with `VULGATA_RETAINED_SOURCE_CHILD=1`, sharing only PostgreSQL/source root; assert remote unavailable, original process/lease gone, no token/clone/temp/cache dependency (FR-015/016/021; SC-006/009).
- [ ] T037 [US3] Add the `VULGATA_RETAINED_SOURCE_CHILD` child-mode entry and production-composition fixture in `tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessChild.cs` and `tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessTests.cs`, using the existing test host and no new project; run T036 to GREEN through a fresh authorization/context (FR-015/021; SC-006).
- [ ] T038 [US3] Run PostgreSQL migration, persistence, trusted-workspace, and separate-process suites together, recording SC-006/007/008 evidence and proving canonical source is independent of CodeGraph data-root/workspace cleanup.

## Phase 6: User Story 4 — Use the Source Capability Independently (P2)

- [ ] T039 [US4] Add source-only qualification tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCapabilityBoundaryTests.cs` asserting capture/read/denial/cancellation/concurrency/reopen execute with CodeGraph, LSP, LLM, semantic Knowledge, Chat, full Scan, Docker, backup, and restore absent (FR-021; SC-009).
- [ ] T040 [US4] Add outcome/evidence classification tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCapabilityBoundaryTests.cs` for success, limitation, denial, cancellation, acquisition/integrity/storage failure, and explicit source-only labels (FR-019/022; SC-010).
- [ ] T041 [US4] Validate `specs/015-retained-source-access/quickstart.md` commands and focused test selection, keeping static, controlled, PostgreSQL, and separate-process evidence distinct and leaving later E2E predicates for the governed gate (FR-022; SC-009/010).

## Phase 7: Polish and final validation

- [ ] T042 Add RED sanitized-logging tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceLoggingTests.cs` for capture, reopen, authorization, deletion conflict, and all failure diagnostics, asserting no source, credentials, origin URL, unsafe path, storage key, or physical path (FR-012/019; NFR-2.3).
- [ ] T043 Implement sanitized diagnostics in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`, `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, and `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs`; run T042 focused and surrounding suites to GREEN (FR-012/019; DR-13.1-001).
- [ ] T044 Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore` plus all retained-source, migration, Repository-management, and separate-process suites; confirm SC-001–SC-010 and no unrelated edits (quickstart.md).
- [ ] T045 Perform final traceability/boundary verification against `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, both contracts, and `docs/agent-workflow/e2e-decision-matrix.md`; document source-only qualification and no composite Scan/Knowledge/Chat claim (FR-021/022; SC-009/010).

## Coverage and execution order

| Coverage | Tasks |
| --- | --- |
| FR-001–005 | T004–T023 |
| FR-006–014 | T005, T024–T030, T034–T035, T042–T043 |
| FR-015–020 | T016–T023, T031–T038 |
| FR-021–022 | T001–T003, T036–T041, T045 |
| SC-001/002 | T010–T023 |
| SC-003/004 | T024–T030 |
| SC-005 | T012–T013, T026–T030 |
| SC-006/007/008 | T031–T038 |
| SC-009/010 | T001–T003, T036–T045 |

Each task ID is in sequential execution order. Setup tasks with distinct files may run in parallel; all shared production files and all behavioral microcycles are serialized. No parallel example claims shared-file work can overlap.

## Implementation strategy

MVP is T001–T023: exact authorized capture, standalone support, raw-byte integrity, atomic completion, migration, and approved Chinese `409` conflict behavior. Then execute US2 T024–T030, durable US3 T031–T038, source-only US4 T039–T041, and final logging/validation T042–T045. Existing-correct surrounding coverage is verification only and does not fabricate RED.

## Self-review

- **Eight handoff checks**: (1) checklist IDs are sequential T001–T045; (2) all references point to preceding or explicitly integrated tasks; (3) each behavior is RED→minimal GREEN→focused/surrounding verification; (4) migration, repository publication, and public deletion mapping precede completion claims; (5) no shared-file parallel production example; (6) T018 RED precedes explicit T019 Chinese `409` implementation; (7) T036–T037 use a concrete filtered test-host child invocation in the existing project; (8) public runtime assertions precede behavior, while codec tests support rather than replace public capture/open behavior.
- **Findings**: DR-13.1-001 resolved by T006–T009, T016–T023, T034–T035, and T042–T043. DR-13.1-002 resolved by T005 and T034–T035; Core has no workspace access.
- **Coverage and paths**: PASS. Exact plan paths, single-project tests, FR-001–022, SC-001–010, deletion conflict, logging, durable storage, trusted workspace, and separate-process evidence are named.

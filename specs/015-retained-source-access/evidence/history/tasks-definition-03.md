---
description: "Dependency-ordered tasks for Story 13.1 retained source access"
---

# Tasks: Retained Source Access

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, and both `contracts/` files.

**Tests**: TDD is mandatory for new behavior. A public RED task is followed by every production prerequisite needed for that public assertion, one GREEN verification task, and then the next behavior. Existing-correct or qualification-only checks are explicitly test-only and must not fabricate RED. All tests remain in `tests/Vulgata.Tests/Vulgata.Tests.csproj`.

**Scope**: Exact retained source, scoped relative reads, durable publication/reopen, live authorization, safe cleanup, and source-only evidence. No CodeGraph/LSP/LLM/semantic Scan/Knowledge/Chat/Docker/backup/restore feature, public Snapshot UI, Worker Agent tool, or wholesale pair migration.

## Phase 1: Setup and declarations

- [ ] T001 [P] Create isolated local-Git and persistent-source-storage fixtures in `tests/Vulgata.Tests/RetainedSource/RetainedSourceFixture.cs`, with source-only dependencies and declared external durable storage (quickstart.md; SC-009).
- [ ] T002 [P] Add test-only child-process scaffolding in `tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessTests.cs` and `tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessChild.cs`, including the `VULGATA_RETAINED_SOURCE_CHILD=1` mode and filtered existing-project invocation; do not claim runtime qualification yet (FR-015/021; SC-006).
- [ ] T003 [P] Add source-only evidence labels and fixture utilities in `tests/Vulgata.Tests/RetainedSource/RetainedSourceTestFixtures.cs`, targeting the existing test project and excluding CodeGraph/LSP/LLM/semantic stores (FR-021/022; SC-009/010).
- [ ] T004 Define logical `SourceSnapshot`, outcome values, acquire/reopen/grant/read contracts, and logical authorization inputs in `src/dotnet/Vulgata.Core/Entities/SourceSnapshot.cs`, `src/dotnet/Vulgata.Core/DomainServices/IRetainedSourceRepository.cs`, and `src/dotnet/Vulgata.Core/DomainServices/RetainedSourceContracts.cs`; keep physical workspace/provider access out of Core (FR-003/004/006/012; DR-13.1-002).
- [ ] T005 Add public contract-shape and identity-validation RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceApplicationContractTests.cs` after T004, asserting capture has only Repository/ref/cancellation, revisions/object formats/counts validate, ordinary results exclude physical data, and workspace is absent from Core/ordinary contracts (FR-003/004/012; SC-001/010).
- [ ] T006 Verify T004 declarations with T005 focused runtime assertions and surrounding compile/contract checks; this is the only contract-first group and does not claim capture behavior GREEN (Constitution IV).

## Phase 2: User Story 1 — Capture an Exact Source Snapshot (P1, MVP)

**Goal**: One public capture call supports authorized System-member and standalone Repositories and returns verified committed source.

- [ ] T007 [US1] Add one integrated public capture RED test in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureTests.cs` for a simple authorized local Repository: full-ref resolution, Repository+ProfiledRevision identity, exact bytes, and standalone/no-pair authorization. The test must execute through the application contract and fail at runtime because the capability is not implemented (FR-001–004; SC-001/002).
- [ ] T008 [US1] Implement startup-validated `SourceCapturePolicy` in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCapturePolicy.cs`, `src/dotnet/Vulgata.Infrastructure/DependencyInjection.cs`, and `src/dotnet/Vulgata.Web/Program.cs`, including fail-closed `source_configuration_invalid`; this is a prerequisite for T007 and is not GREEN by itself (plan §8; PR-13.1-001).
- [ ] T009 [US1] Implement bounded raw Git object acquisition in `src/dotnet/Vulgata.Infrastructure/Source/GitCommittedObjectReader.cs`, reusing safe process/cancellation patterns from `src/dotnet/Vulgata.Infrastructure/Git/GitWorkingCopyService.cs`; continue the same T007 group (FR-003/008).
- [ ] T010 [US1] Implement flat manifest/blob staging in `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, including raw paths, strict UTF-8 paths, modes/object IDs, symlink/gitlink/unsafe limitations, streaming bounds, and manifest-last verification; continue the same T007 group (FR-004/005/009/014).
- [ ] T011 [US1] Implement source-specific live authorization in `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs`, deriving `IUserContext` subject and checking Administrator/SystemOwner/standalone authority and origin/System binding; continue the same T007 group (FR-001/002/010).
- [ ] T012 [US1] Add source-owned EF configuration in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/SourceSnapshotConfiguration.cs` and `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, including immutable fields, uniqueness, digest/schema/object-format constraints, non-negative counts, and `DeleteBehavior.Restrict`; continue the same T007 group (FR-004/017/018).
- [ ] T013 [US1] Add migration `src/dotnet/Vulgata.Infrastructure/Data/Migrations/<timestamp>_AddRetainedSourceSnapshots.cs` for only `SourceSnapshots`; continue the same T007 group and preserve prepared/pair tables (FR-017/018).
- [ ] T014 [US1] Implement `RetainedSourceRepository` in `src/dotnet/Vulgata.Infrastructure/Data/RetainedSourceRepository.cs` and `RetainedSourceCaptureService` in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`, composing T008–T013, atomic verified completion, sanitized outcomes, and non-cancellable row linearization (FR-001–005/014/019/020).
- [ ] T015 [US1] Register the capability additively in `src/dotnet/Vulgata.Infrastructure/DependencyInjection.cs` and `src/dotnet/Vulgata.Web/Program.cs`, without a Snapshot UI or legacy pair replacement; finish the public seam needed by T007 (scope boundary).
- [ ] T016 [US1] Run T007 and its surrounding Git/authorization/migration tests to claim GREEN for the integrated capture group; verify exact committed bytes after ref/working-copy changes, standalone capture, integrity, and no completed partial capture (SC-001/002/005/008).

### Capture extension groups

- [ ] T017 [US1] Add detailed public capture/open RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceManifestTests.cs` for canonical UTF-8 manifest bytes, raw-path order, count/byte sums, object-format/revision agreement, and explicit unsupported entries; T016 must already be GREEN (FR-004/008/009; SC-003).
- [ ] T018 [US1] Extend `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceManifestCodec.cs` and store validation to satisfy T017, then run T017 plus capture/open surrounding tests to GREEN (data-model.md).
- [ ] T019 [US1] Add public deletion-conflict RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs` for Repository deletion returning sanitized Chinese `409` when retained source is referenced (FR-017/018).
- [ ] T020 [US1] Implement the approved conflict mapping in `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs` and `src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs`; run T019 and surrounding Repository-management tests to GREEN without DTO/UI/schema (FR-017/018).

## Phase 3: User Story 2 — Read Retained Source Through a Scoped Grant (P1)

- [ ] T021 [US2] Add one integrated public grant/list/metadata RED group in `tests/Vulgata.Tests/RetainedSource/RetainedSourceReadContextTests.cs` for a completed T016 snapshot: fresh live authority, Repository/revision binding, bounded listing, metadata, scope-bound cursors, relative containment, and unsupported entries (FR-006/007/009/010/011; SC-003/004).
- [ ] T022 [US2] Implement cumulative grant/deadline reservations in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceReadContext.cs`, reopen verification and listing/metadata operations in `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, and grant opening in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`; run T021 and surrounding capture tests to GREEN only after all three files satisfy the public assertions (FR-006/007/009/013/014).
- [ ] T023 [US2] Add public byte/text/budget RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceReadContextTests.cs` for exact ranges, strict UTF-8, atomic concurrent reservations, exact exhaustion, monotonic deadline, cancellation, no partial payload, and no budget refresh (FR-006/008/012/013/014; SC-003/005).
- [ ] T024 [US2] Implement byte/range/text operations and post-I/O bounds in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceReadContext.cs` and `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`; run T023 and surrounding read/list tests to GREEN (FR-006/008/013/014).
- [ ] T025 [US2] Add live-denial/disclosure RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceAuthorizationTests.cs` for expiry/revocation, Repository/revision mismatch, absolute/backslash/dot/NUL/control/escaping/outside paths, and no credential/physical-path disclosure (FR-010–012; SC-004).
- [ ] T026 [US2] Implement every-operation authority recheck and sanitized denial in `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs` and `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`; run T025 plus T021–T024 surrounding suites to GREEN (FR-010/019/020).
- [ ] T027 [US2] Run the complete focused US2 suite and capture regressions, recording SC-003/004/005 evidence and confirming ordinary consumers never receive a trusted workspace path.

## Phase 4: User Story 3 — Reopen Durable Source Safely (P1)

- [ ] T028 [US3] Add integrated publication/concurrency RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePersistenceTests.cs` for same-identity replay, cross-process lock, atomic seal/adopt/row insert, crash recovery, stale cleanup, digest conflict, no partial visibility, concurrent readers, lease release, no pruning, and fresh-authority retry (FR-005/014/017–020; SC-006/007/008).
- [ ] T029 [US3] Implement repository publication/row visibility plus identity lock, seal/adopt, crash recovery, stale cleanup, and digest conflict in `src/dotnet/Vulgata.Infrastructure/Data/RetainedSourceRepository.cs`, `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, and `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`; run T028 and surrounding PostgreSQL/capture tests to GREEN after all prerequisites are present (FR-005/014/017–020).
- [ ] T030 [US3] Add trusted-workspace boundary RED tests in `tests/Vulgata.Tests/RetainedSource/TrustedSourceWorkspaceTests.cs` for Infrastructure-only authorization, contained materialization, symlink/gitlink/unsafe/collision refusal, and owned cleanup (FR-012/014/016; plan AD-6).
- [ ] T031 [US3] Implement `src/dotnet/Vulgata.Infrastructure/Source/TrustedSourceWorkspaceProvider.cs` and run T030 plus surrounding source-read tests to GREEN; no Core or ordinary contract changes (FR-012/014/016).
- [ ] T032 [US3] Run the prepared child harness from T002 as a test-only fresh-process qualification in `tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessTests.cs`, invoking `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --filter FullyQualifiedName~RetainedSourceSeparateProcessChild` with `VULGATA_RETAINED_SOURCE_CHILD=1`; verify fresh authorization/context, remote unavailable, original process/lease gone, and no token/clone/temp/cache dependency (FR-015/016/021; SC-006/009).
- [ ] T033 [US3] Run PostgreSQL migration, persistence, trusted-workspace, and fresh-process suites together, recording SC-006/007/008 evidence and proving canonical source is independent of CodeGraph data-root/workspace cleanup.

## Phase 5: User Story 4 — Use the Source Capability Independently (P2)

- [ ] T034 [US4] Add test-only source-boundary qualification in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCapabilityBoundaryTests.cs` for capture/read/denial/cancellation/concurrency/reopen with CodeGraph, LSP, LLM, semantic Knowledge, Chat, full Scan, Docker, backup, and restore absent (FR-021; SC-009).
- [ ] T035 [US4] Add test-only outcome/evidence classification in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCapabilityBoundaryTests.cs` for success, limitation, denial, cancellation, acquisition/integrity/storage failure, and explicit source-only labels (FR-019/022; SC-010).
- [ ] T036 [US4] Validate `specs/015-retained-source-access/quickstart.md` commands and evidence classes without changing it; leave later E2E authorization/persistence/migration predicates to the governed gate (FR-022; SC-009/010).

## Phase 6: Polish and final validation

- [ ] T037 Add RED sanitized-logging tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceLoggingTests.cs` for capture, reopen, authorization, deletion conflict, and failure diagnostics, asserting no source, credentials, origin URL, unsafe path, storage key, or physical path (FR-012/019; NFR-2.3).
- [ ] T038 Implement sanitized diagnostics in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`, `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, and `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs`; run T037 focused and surrounding suites to GREEN (FR-012/019).
- [ ] T039 Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore` plus retained-source, migration, Repository-management, and separate-process suites; confirm SC-001–SC-010 and no unrelated edits (quickstart.md).
- [ ] T040 Perform final traceability/boundary verification against `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, both contracts, and `docs/agent-workflow/e2e-decision-matrix.md`; document source-only qualification and no composite Scan/Knowledge/Chat claim (FR-021/022; SC-009/010).

## Coverage and group map

| Integrated group | RED | All production prerequisites | GREEN / verification | Entry prerequisites |
| --- | --- | --- | --- | --- |
| Contract declarations | T005 | T004 | T006 | Setup T001–T003 |
| Initial capture | T007 | T008–T015 | T016 | T004–T006; public contract executable |
| Manifest detail | T017 | T018 | T018 focused/surrounding run | T016 capture/open GREEN |
| Delete conflict | T019 | T020 | T020 focused/surrounding run | T012 EF shape and T016 publication |
| Grant/list/metadata | T021 | T022 | T022 focused/surrounding run | T016 completed snapshot |
| Byte/text/budget | T023 | T024 | T024 focused/surrounding run | T022 grant/list GREEN |
| Live denial | T025 | T026 | T026 focused/surrounding run | T022–T024 |
| Publication/concurrency | T028 | T029 | T029 focused/surrounding run | T016/T020 and T022 |
| Trusted workspace | T030 | T031 | T031 focused/surrounding run | T029 published source |
| Fresh process | test-only T032 | T002 harness already present | T032/T033 | T029 and T031 |
| Logging | T037 | T038 | T038 focused/surrounding run | T016–T036 |

FR coverage: T004–T020 cover FR-001–005; T021–T027 and T030–T031 cover FR-006–014; T012–T020 and T028–T033 cover FR-015–020; T001–T003 and T032–T040 cover FR-021–022. SC-001/002 are T007–T016; SC-003/004 T021–T027; SC-005 T007/T023–T027; SC-006/007/008 T028–T033; SC-009/010 T001–T003/T032–T040.

## Execution strategy and self-review

MVP is T001–T020: declarations, integrated capture, manifest detail, migration/publication shape, and approved Chinese `409`. Then execute read groups T021–T027, durable groups T028–T033, source-only qualification T034–T036, and logging/final validation T037–T040.

IDs are sequential T001–T040. Shared production files are serialized; only setup tasks with distinct files are parallelizable. No GREEN claim occurs before all listed production prerequisites. T002 creates the child harness before T032 test-only qualification. T005/T006 explicitly verify declarations before behavior. DR-13.1-001 is resolved by integrated groups T007–T016, T021–T026, T028–T033, and T037–T038. DR-13.1-002 remains resolved: Core has no workspace contract. Paths, FR-001–022, SC-001–010, migration/delete/config/trusted boundaries, and source-only evidence are covered.

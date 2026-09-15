---
description: "Dependency-ordered tasks for Story 13.1 retained source access"
---

# Tasks: Retained Source Access

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, and both `contracts/` files.

**Tests**: TDD is mandatory for new behavior. Each public RED group is followed by all production prerequisites required for that assertion, one GREEN verification task, then the next group. Existing-correct and qualification-only checks are test-only and do not fabricate RED. All tests use `tests/Vulgata.Tests/Vulgata.Tests.csproj`.

**Scope**: Exact retained source, scoped relative reads, durable publication/reopen, live authorization, safe cleanup, and source-only evidence. No CodeGraph/LSP/LLM/semantic Scan/Knowledge/Chat/Docker/backup/restore feature, public Snapshot UI, Worker Agent tool, or wholesale pair migration.

## Phase 1: Setup and declarations

- [x] T001 [P] Create isolated local-Git and persistent-source-storage fixtures in `tests/Vulgata.Tests/RetainedSource/RetainedSourceFixture.cs`, with no semantic/tool dependencies (quickstart.md; SC-009).
- [x] T002 [P] Add test-only child-process scaffolding in `tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessTests.cs` and `tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessChild.cs`, including `VULGATA_RETAINED_SOURCE_CHILD=1` and the filtered existing-project invocation; do not claim qualification yet (FR-015/021).
- [x] T003 [P] Add source-only evidence labels/utilities in `tests/Vulgata.Tests/RetainedSource/RetainedSourceTestFixtures.cs`, targeting the existing test project (FR-021/022; SC-009/010).
- [x] T004 Define only declarative `SourceSnapshot`, outcome types, acquire/reopen/grant/read signatures, and logical authorization inputs in `src/dotnet/Vulgata.Core/Entities/SourceSnapshot.cs`, `src/dotnet/Vulgata.Core/DomainServices/IRetainedSourceRepository.cs`, and `src/dotnet/Vulgata.Core/DomainServices/RetainedSourceContracts.cs`; include no validation behavior and no workspace/provider contract (FR-003/004/006/012; DR-13.1-002).
- [x] T005 Add public runtime RED contract tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceApplicationContractTests.cs` for revision/object-format/count validation, capture arguments (Repository/ref/cancellation only), ordinary logical results, and absence of physical workspace data (FR-003/004/012; SC-001/010).
- [x] T006 Implement the identity/value validation exercised by T005 in `src/dotnet/Vulgata.Core/Entities/SourceSnapshot.cs` and `src/dotnet/Vulgata.Core/DomainServices/RetainedSourceContracts.cs`, preserving Core purity and no workspace access.
- [x] T007 Run T005 focused and surrounding contract tests to GREEN; this verifies declarations plus validation only and does not claim capture behavior.

## Phase 2: User Story 1 — Capture an Exact Source Snapshot (P1, MVP)

- [x] T008 [US1] Add public runtime RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureTests.cs` for simple authorized capture, one full-ref resolution, Repository+ProfiledRevision identity, exact committed bytes after ref/working-copy mutation, and standalone/no-pair authorization (FR-001–004; SC-001/002).
- [x] T009 [US1] Add public runtime RED failure/authority tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureTests.cs` for invalid/missing `SourceStorage:*` configuration; caller-unraiseable entry-cap, blob-cap, and aggregate-cap exhaustion as three distinct cases with their distinct sanitized limit outcomes; monotonic capture-deadline expiry with its distinct deadline outcome; auth-before-Git, Git auth/transport/ref failure, pre-publication authority/origin/System rebinding, cancellation before verification/publication, storage exhaustion, owned staging cleanup, sanitized failures, and zero partial completion (FR-001/005/014/019/020; SC-005/008).
- [x] T010 [US1] Implement fail-closed `SourceCapturePolicy` binding/validation in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCapturePolicy.cs`, `src/dotnet/Vulgata.Infrastructure/DependencyInjection.cs`, and `src/dotnet/Vulgata.Web/Program.cs` as the first prerequisite for T008/T009 (plan §8; PR-13.1-001).
- [x] T011 [US1] Implement bounded raw Git object acquisition in `src/dotnet/Vulgata.Infrastructure/Source/GitCommittedObjectReader.cs`, reusing safe process/cancellation patterns from `src/dotnet/Vulgata.Infrastructure/Git/GitWorkingCopyService.cs` (FR-003/008).
- [x] T012 [US1] Implement flat manifest/blob staging and verification in `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, including raw paths, strict UTF-8 paths, modes/object IDs, symlink/gitlink/unsafe limitations, streaming bounds, manifest-last verification, and disposable cleanup (FR-004/005/009/014).
- [x] T013 [US1] Implement live source authorization in `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs`, deriving `IUserContext` and checking Administrator/SystemOwner/standalone authority plus origin/System binding (FR-001/002/010).
- [x] T014 [US1] Add RED owned-PostgreSQL tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs` for actual migration application, fixed/check constraints, revision/object-format/digest shapes, uniqueness, immutable row behavior, non-negative counts, and `DeleteBehavior.Restrict` (FR-004/017/018; SC-008).
- [x] T015 [US1] Implement EF configuration in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/SourceSnapshotConfiguration.cs` and `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, satisfying T014’s fixed/check/uniqueness/immutability/Restrict assertions.
- [x] T016 [US1] Add migration `src/dotnet/Vulgata.Infrastructure/Data/Migrations/<timestamp>_AddRetainedSourceSnapshots.cs` for only `SourceSnapshots`, preserving prepared/pair tables, and make T014 runnable against the owned PostgreSQL fixture.
- [x] T017 [US1] Implement `RetainedSourceRepository` in `src/dotnet/Vulgata.Infrastructure/Data/RetainedSourceRepository.cs` and `RetainedSourceCaptureService` in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`, composing T010–T013/T015–T016, atomic verified completion, sanitized outcomes, and non-cancellable row linearization.
- [x] T018 [US1] Register the capability additively in `src/dotnet/Vulgata.Infrastructure/DependencyInjection.cs` and `src/dotnet/Vulgata.Web/Program.cs`, without Snapshot UI or legacy pair replacement.
- [x] T019 [US1] Run T008/T009 capture tests, T014 PostgreSQL tests, and surrounding Git/authorization/migration tests to GREEN; record exact bytes after ref mutation, failure boundaries, fresh authority, cleanup, no partial rows, and SC-001/002/005/008.
- [x] T020 [US1] Add public capture-only RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceManifestTests.cs` for canonical UTF-8 manifest bytes, raw-path order, count/byte sums, object-format/revision agreement, and explicit unsupported entries (FR-004/008/009; SC-003).
- [x] T021 [US1] Implement/complete `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceManifestCodec.cs` and store validation for T020 without adding open/reopen assertions; run T020 plus capture surrounding tests to GREEN.
- [x] T022 [US1] Add public deletion-conflict RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs` for existing Repository deletion returning sanitized Chinese `409` when retained source is referenced (FR-017/018).
- [x] T023 [US1] Implement the approved mapping in `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs` and `src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs`; run T022 and surrounding Repository tests to GREEN without DTO/UI/schema.

## Phase 3: User Story 2 — Read Retained Source Through a Scoped Grant (P1)

- [x] T024 [US2] Add public runtime RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceReadContextTests.cs` for fresh grant authority, Repository/revision/snapshot binding, manifest/blob digest verification on open, missing/corrupt retained state, bounded listing/metadata, scope-bound cursors, relative containment, and unsupported entries against the completed T019 snapshot (FR-006/007/009/010/011/015/019; SC-003/004/006/008).
- [x] T025 [US2] Implement cumulative grant/deadline reservations, reopen verification, grant opening, and listing/metadata operations in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceReadContext.cs`, `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, and `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`; run T024 and surrounding capture tests to GREEN only after all three files satisfy the public assertions.
- [x] T026 [US2] Add public runtime RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceReadContextTests.cs` for exact byte ranges, strict UTF-8, concurrent atomic reservations, exact exhaustion, monotonic deadline, cancellation, no partial payload, and no automatic refresh (FR-006/008/012/013/014; SC-003/005).
- [x] T027 [US2] Implement byte/range/text operations and post-I/O bounds in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceReadContext.cs` and `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`; run T026 and surrounding list/open tests to GREEN.
- [x] T028 [US2] Add live-denial RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceAuthorizationTests.cs` for expiry/revocation, Repository/revision mismatch, absolute/backslash/dot/NUL/control/escaping/outside paths, and no credential/physical-path disclosure (FR-010–012; SC-004).
- [x] T029 [US2] Implement every-operation authority recheck and sanitized denial in `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs` and `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`; run T028 plus T024–T027 surrounding suites to GREEN.
- [x] T030 [US2] Run the complete focused US2 suite and capture regressions, recording SC-003/004/005 and confirming ordinary consumers never receive trusted workspace paths.

## Phase 4: User Story 3 — Reopen Durable Source Safely (P1)

- [x] T031 [US3] Add integrated publication/concurrency RED tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePersistenceTests.cs` for same-identity replay, cross-process lock, atomic seal/adopt/row insert, crash recovery, stale cleanup, digest conflict, no partial visibility, concurrent readers, lease release, no pruning, and fresh-authority retry (FR-005/014/017–020; SC-006/007/008).
- [x] T032 [US3] Implement repository publication/row visibility plus identity lock, seal/adopt, crash recovery, stale cleanup, and digest conflict in `src/dotnet/Vulgata.Infrastructure/Data/RetainedSourceRepository.cs`, `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, and `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`; run T031 and surrounding PostgreSQL/capture tests to GREEN after all prerequisites are present.
- [ ] T033 [US3] Add trusted-workspace RED tests in `tests/Vulgata.Tests/RetainedSource/TrustedSourceWorkspaceTests.cs` for Infrastructure-only authorization, contained materialization, symlink/gitlink/unsafe/collision refusal, and owned cleanup (FR-012/014/016; plan AD-6).
- [ ] T034 [US3] Implement `src/dotnet/Vulgata.Infrastructure/Source/TrustedSourceWorkspaceProvider.cs` and run T033 plus surrounding source-read tests to GREEN; no Core/ordinary workspace contract.
- [ ] T035 [US3] Run the prepared child harness from T002 as a test-only fresh-process qualification in `tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessTests.cs`, invoking `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-build --filter FullyQualifiedName~RetainedSourceSeparateProcessChild` with `VULGATA_RETAINED_SOURCE_CHILD=1`; verify fresh authorization/context, remote unavailable, original process/lease gone, and no token/clone/temp/cache dependency (FR-015/016/021; SC-006/009).
- [ ] T036 [US3] Run PostgreSQL migration, persistence, trusted-workspace, and fresh-process suites together, recording SC-006/007/008 and independence from CodeGraph data-root/workspace cleanup.

## Phase 5: User Story 4 and final validation (P2)

- [ ] T037 [US4] Add test-only source-boundary qualification in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCapabilityBoundaryTests.cs` for capture/read/denial/cancellation/concurrency/reopen with CodeGraph, LSP, LLM, semantic Knowledge, Chat, full Scan, Docker, backup, and restore absent (FR-021; SC-009).
- [ ] T038 [US4] Add test-only outcome/evidence classification in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCapabilityBoundaryTests.cs` for success, limitation, denial, cancellation, acquisition/integrity/storage failure, and source-only labels (FR-019/022; SC-010).
- [ ] T039 [P] Add RED sanitized-logging tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceLoggingTests.cs` for capture, reopen, authorization, deletion conflict, and failure diagnostics, asserting no source, credentials, origin URL, unsafe path, storage key, or physical path (FR-012/019; NFR-2.3).
- [ ] T040 Implement sanitized diagnostics in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`, `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, and `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs`; run T039 focused/surrounding tests to GREEN.
- [ ] T041 Validate `specs/015-retained-source-access/quickstart.md` commands and distinct static/controlled/PostgreSQL/separate-process evidence; leave later E2E predicates to the governed gate (FR-022; SC-009/010).
- [ ] T042 Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore` plus all retained-source, migration, Repository-management, and separate-process suites; confirm SC-001–SC-010 and no unrelated edits.
- [ ] T043 Perform final traceability against `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, both contracts, and `docs/agent-workflow/e2e-decision-matrix.md`; document source-only qualification and no composite Scan/Knowledge/Chat claim.

## Integrated group map and omission coverage

| Group | RED test author task | All production tasks | GREEN checkpoint | Entry |
| --- | --- | --- | --- | --- |
| Declarations/identity | T005 | T004, T006 | T007 | T001–T003 |
| Capture success/failure | T008, T009 | T010–T018 | T019 | T007 |
| Capture manifest detail | T020 | T021 | T021 | T019 |
| PostgreSQL/delete | T014, T022 | T015–T018, T023 | T019 for PostgreSQL constraints/publication; T023 for public Chinese `409` | T004; T017 |
| Grant/list | T024 | T025 | T025 | T019 |
| Byte/text/budget | T026 | T027 | T027 | T025 |
| Live denial | T028 | T029 | T029 | T025–T027 |
| Publication/concurrency | T031 | T032 | T032 | T019/T023 |
| Trusted workspace | T033 | T034 | T034 | T032 |
| Fresh process | test-only T035 | T002 scaffolding | T035/T036 | T032/T034 |
| Logging | T039 | T040 | T040 | T019–T038 |

| Omitted case from review | RED coverage | Production | GREEN |
| --- | --- | --- | --- |
| Invalid config / caller cannot raise limits / configured caps / monotonic deadline | T009 | T010, T017 | T019 |
| Auth before Git / Git auth-transport-ref failures | T009 | T011, T013, T017 | T019 |
| Pre-publication revoke/rebind / cancellation boundaries | T009 | T013, T017 | T019 |
| Storage exhaustion / cleanup / sanitized failure / zero partial row | T009 | T012, T017 | T019 |
| Ref and working-copy mutation | T008 | T011–T012, T017 | T019 |
| Migration applied / fixed-check / uniqueness / immutable / Restrict | T014 | T015–T018 | T019 after EF, migration, repository, and composition prerequisites |
| Public Chinese `409` deletion conflict | T022 | T023 | T023 |

## Coverage and self-review

FR-001–005: T004–T023. FR-006–014: T024–T030, T033–T034, T039–T040. FR-015–020: T014–T023, T031–T036. FR-021–022: T001–T003, T035–T043. SC-001/002: T008–T019. SC-003/004: T020–T030. SC-005: T009/T019/T026–T030. SC-006/007/008: T031–T036. SC-009/010: T001–T003/T035–T043.

Self-review: IDs are sequential T001–T043; each referenced task exists and precedes its dependent GREEN claim; T004 is declarations only; T005 RED precedes T006 implementation and T007 GREEN; T020 is capture-only and open integrity belongs to T024/T025; T002 scaffolds the child before T035 qualification; T009 explicitly authors separate entry/blob/aggregate cap and deadline cases with distinct outcomes; all omitted review cases have explicit RED/production/GREEN mapping above; T019 is the PostgreSQL GREEN only after T015–T018, while T023 is the separate public deletion GREEN; PostgreSQL tests are owned and run after actual migration/configuration; Core exposes no workspace seam; no shared production tasks are marked parallel. Existing-correct qualification tasks T035/T037/T038/T041 are test-only and make no fabricated RED claim.

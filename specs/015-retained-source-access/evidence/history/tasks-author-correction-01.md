---
description: "Dependency-ordered implementation tasks for Story 13.1"
---

# Tasks: Retained Source Access

**Input**: `specs/015-retained-source-access/spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, and `contracts/`

**Story**: 13.1 — Retain exact Repository source with scoped read access

**Tests**: TDD is mandatory for observable behavior. Every behavioral test task below is written first and must demonstrate RED before its corresponding implementation task. All tests belong to `tests/Vulgata.Tests`; no new test project is introduced.

**Scope guard**: This task list covers only the retained-source capability. It does not add CodeGraph/LSP/LLM/semantic Scan/Knowledge/Chat/Docker/backup/restore behavior, a public Snapshot UI, a Worker Agent tool, or a wholesale pair-era migration.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the source-capability seams and isolated fixture conventions without implementing production behavior.

- [ ] T001 [P] Add the retained-source contract and manifest schema fixtures to `tests/Vulgata.Tests/RetainedSource/RetainedSourceTestFixtures.cs`, using isolated local Git repositories, an external persistent source-storage root, and the single `tests/Vulgata.Tests/Vulgata.Tests.csproj` project (plan §7; quickstart.md).
- [ ] T002 [P] Add the retained-source test fixture helpers and explicit source-only evidence labels in `tests/Vulgata.Tests/RetainedSource/RetainedSourceFixture.cs`, ensuring fixtures do not reference CodeGraph, LSP, LLM, semantic tables, Chat, or full Scan activation (FR-021/022, SC-009/010).
- [ ] T003 [P] Add contract-shape tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceApplicationContractTests.cs` proving capture accepts only Repository/ref/cancellation, no caller limit/deadline/root/token fields, and ordinary results exclude physical paths, credentials, origins, raw unsafe paths, and storage keys (FR-004/012, plan §6/§8).

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Define the domain contracts, immutable policy, manifest codec, and database shape that all story slices consume. Keep this phase limited to seams needed by the first RED tests.

- [ ] T004 Define `SourceSnapshot`, immutable source identities, and source outcome/value objects in `src/dotnet/Vulgata.Core/Entities/SourceSnapshot.cs` and `src/dotnet/Vulgata.Core/DomainServices/RetainedSourceContracts.cs`, including `RepositoryId`, lower-case 40/64-character `ProfiledRevision`, `sha1`/`sha256` agreement, `retained-source-manifest/1.0`, non-negative counts, and sanitized statuses from `data-model.md` (FR-003/004/005/019).
- [ ] T005 Define the Core application and authorization interfaces in `src/dotnet/Vulgata.Core/DomainServices/IRetainedSourceRepository.cs` and `src/dotnet/Vulgata.Core/DomainServices/RetainedSourceContracts.cs` for acquire, reopen/grant, relative metadata/list/range/strict-UTF8 operations, and logical live-authority inputs; keep all physical workspace/provider access out of Core (FR-006/007/012, contract file).
- [ ] T006 Add RED policy/configuration tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePolicyTests.cs` for every `SourceStorage:*` rule, immutable startup policy, caller-unraiseable limits, and fail-closed `source_configuration_invalid` composition without CodeGraph/temporary/unbounded fallback (plan §8; PR-13.1-001).
- [ ] T007 Add RED canonical-manifest tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceManifestTests.cs` for UTF-8 without BOM, deterministic property order, lower-case hex, base64 raw paths, raw-path ordering, count/byte sums, object-format/revision agreement, capabilities, and limitations (FR-004/008/009, data-model.md).
- [ ] T008 Add RED EF/publication-shape tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs` for immutable fields, unique `(RepositoryId, ProfiledRevision)`, unique internal key, fixed schema/digest constraints, non-negative counts, `DeleteBehavior.Restrict`, and the public deletion-conflict mapping (data-model.md; FR-017/018).
- [ ] T039 Implement startup-validated immutable `SourceCapturePolicy` and read-policy binding in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCapturePolicy.cs`, `src/dotnet/Vulgata.Infrastructure/DependencyInjection.cs`, and `src/dotnet/Vulgata.Web/Program.cs`; T006 must be RED first and T040 must verify focused/surrounding GREEN (plan §8; PR-13.1-001).
- [ ] T040 Implement canonical manifest serialization/validation in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceManifestCodec.cs` using `specs/015-retained-source-access/contracts/retained-source-manifest-v1.schema.json`; T007 must be RED first and T041 must verify focused/surrounding GREEN (FR-004/008/009, data-model.md).
- [ ] T041 Add the source-owned EF model/configuration and migration-ready registration in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/SourceSnapshotConfiguration.cs` and `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`; T008 must be RED first and T044 must verify the focused/surrounding PostgreSQL shape before publication depends on it (data-model.md; FR-017/018).
- [ ] T042 Verify the policy slice from T006/T039 with focused `tests/Vulgata.Tests/RetainedSource/RetainedSourcePolicyTests.cs` and surrounding DI startup tests, recording RED→GREEN and fail-closed configuration evidence (PR-13.1-001).
- [ ] T043 Verify the manifest slice from T007/T040 with focused `tests/Vulgata.Tests/RetainedSource/RetainedSourceManifestTests.cs` and surrounding capture/open tests, recording RED→GREEN and canonical-byte evidence (FR-004/008/009).
- [ ] T044 Verify the EF/configuration slice from T008/T041 with focused `tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs` and surrounding migration/delete checks, recording RED→GREEN before capture publication depends on the row shape (FR-017/018).

## Phase 3: User Story 1 — Capture an Exact Source Snapshot (Priority: P1) 🎯 MVP

**Goal**: Capture an authorized System-member or standalone Repository at one full commit from raw Git objects, verify it, and publish only a complete immutable Source Snapshot.

**Independent Test**: A local Git fixture captures through the public application contract, survives ref/working-copy changes, works without pair/System identity for standalone Repository, and leaves no completed row/object after cancellation or pre-publication failure (spec User Story 1; FR-001–005, FR-014, SC-001/002/005).

### Tests for User Story 1 (write first; observe RED)

- [ ] T009 [US1] Add RED capture contract tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureTests.cs` for live authorization before Git, one full-ref resolution, Repository+ProfiledRevision identity, raw committed bytes despite `.gitattributes`/autocrlf/working-copy/ref changes, integrity receipts, and System-member plus standalone fixtures without pair or parent System identity (FR-001–004; SC-001/002/003).
- [ ] T010 [US1] Add RED failure/limit tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureTests.cs` for invalid server policy, ref/auth/transport failure, entry/blob/aggregate limits, monotonic capture deadline, cancellation before verification and before publication, sanitized outcomes, owned staging cleanup, and zero completed partial captures (FR-005/014/019/020; SC-005/008).

### Implementation for User Story 1

- [ ] T011 [US1] Implement bounded binary-safe Git plumbing in `src/dotnet/Vulgata.Infrastructure/Source/GitCommittedObjectReader.cs`, reusing safe `ArgumentList`/no-prompt/process cancellation patterns from `src/dotnet/Vulgata.Infrastructure/Git/GitWorkingCopyService.cs` while resolving the permitted ref once, enumerating all committed leaves, and reading raw object bytes without checkout or `git archive` transforms (FR-003/008; plan Architecture Decision 2).
- [ ] T012 [US1] Implement immutable flat manifest/blob staging and verification in `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, including raw path bytes, safe strict-UTF8 relative paths, modes/object IDs, SHA-256 blobs, symlink/gitlink/unsafe-path limitations, bounded streaming, manifest-last write, reread verification, and contained operation cleanup (FR-004/005/009/014; data-model.md).
- [ ] T013 [US1] Implement source-specific live authorization in `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs`, deriving subject from `IUserContext` and reloading Administrator/SystemOwner/standalone ownership, Repository existence, current origin/System binding, and revision binding at capture and pre-publication (FR-001/002/010/019; plan Architecture Decision 4).
- [ ] T014 [US1] Implement capture orchestration and sanitized outcomes in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`, composing policy, authorization, Git reader, manifest/store, cancellation/deadline gates, and explicit `Completed`, `Replay`, `Denied`, `AcquisitionFailure`, `IntegrityFailure`, `StorageFailure`, `Cancelled`, and limit/deadline statuses (FR-001–005/014/019/020; contract file).
- [ ] T015 [US1] Add additive dependency-injection registration and application composition in `src/dotnet/Vulgata.Infrastructure/DependencyInjection.cs`, `src/dotnet/Vulgata.Web/Program.cs`, and `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs`, exposing the capability without a new HTTP/UI Snapshot route and without replacing legacy pair consumers (plan Repository Fit; scope boundaries).
- [ ] T016 [US1] Re-run `tests/Vulgata.Tests/RetainedSource/RetainedSourceCaptureTests.cs` and the relevant existing Git/Repository-management tests after T011–T015, recording RED→GREEN evidence for SC-001/002/003/005/008 and confirming no source-only result is labelled Scan/Knowledge/Chat/Docker qualification.

## Phase 4: User Story 2 — Read Retained Source Through a Scoped Grant (Priority: P1)

**Goal**: Issue a fresh live-authorized process-local read context and serve bounded, exact, Repository-relative metadata/content with cumulative budget, deadline, containment, cancellation, and explicit limitations.

**Independent Test**: A completed snapshot is read after checkout/ref changes; expired/revoked/mismatched/escaping requests disclose nothing; shared counters and deadline cannot be refreshed or bypassed (spec User Story 2; FR-006–014, SC-003–005).

### Tests for User Story 2 (write first; observe RED)

- [ ] T017 [US2] Add RED grant/metadata/list tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceReadContextTests.cs` for fresh live authority, Repository/revision/snapshot binding, bounded directory/file metadata, opaque scope-bound cursors, exact ordinal relative-path containment, and unavailable unsafe/symlink/gitlink capabilities (FR-006/007/009/010/011; SC-003/004).
- [ ] T018 [US2] Add RED content/budget tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceReadContextTests.cs` for exact byte ranges, strict UTF-8 boundaries, cumulative operation/metadata/byte reservations, concurrent calls, exact-boundary exhaustion, monotonic deadline, cancellation, no partial payload, no automatic fresh budget, and no physical-path/credential disclosure (FR-006/008/012/013/014; SC-004/005).
- [ ] T019 [P] [US2] Add RED authorization adversary tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceAuthorizationTests.cs` for revoked/expired subject, Repository and ProfiledRevision mismatch, moved/deleted Repository, changed origin/System binding, missing verifier/subject, absolute/backslash/dot/NUL/control/escaping/outside paths, and sanitized denial/not-found outcomes (FR-010–012; SC-004).

### Implementation for User Story 2

- [ ] T020 [US2] Implement the process-local cumulative grant and monotonic deadline in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceReadContext.cs`, with atomic reservations before I/O, failed/denied/cancelled attempt consumption, server-only lowerable grants, cancellation/deadline checks before/after authorization and I/O, and disposal that never mutates canonical source (FR-006/013/014; data-model.md).
- [ ] T021 [US2] Implement reopen verification and ordinary relative metadata/list/range/strict-UTF8 operations in `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs` and `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceReadContext.cs`, requiring matching row/object/manifest/blob digests, exact `/` ordinal paths, bounded results, and explicit `Unsupported`/`NotFoundOrUnavailable` outcomes (FR-006–009/011/012; contract file).
- [ ] T022 [US2] Wire grant opening and live authorization into `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs` and `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs`, ensuring fresh process contexts disclose no existence/count/hash before authority and every operation rechecks applicable authority (FR-006/010/019/020).
- [ ] T023 [US2] Re-run focused read, authorization, and capture regression suites and the contract-shape checks in `tests/Vulgata.Tests/RetainedSource/`, recording RED→GREEN evidence for SC-003/004/005 and confirming ordinary consumers never receive a physical trusted-workspace path.

## Phase 5: User Story 3 — Reopen Durable Source Safely (Priority: P1)

**Goal**: Publish one stable PostgreSQL/source-object identity, coordinate concurrent capture/replay/adoption/cleanup safely, and reopen exact bytes from a fresh process while the remote is unavailable.

**Independent Test**: Capture, dispose the original context/process, disable the remote, start a fresh production-composed helper process sharing only PostgreSQL and configured source root, and verify bytes; exercise races, corruption, capacity, retry, and lease release.

### Tests for User Story 3 (write first; observe RED)

- [ ] T024 [P] [US3] Add RED persistence/migration tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs` for `SourceSnapshots`, immutable rows, uniqueness, fixed schema/digest/object-format constraints, non-negative counts, Repository `Restrict` deletion, and sanitized Chinese `409` mapping through `src/dotnet/Vulgata.Web/Api/Repositories/RepositoryApiEndpoints.cs` / `src/dotnet/Vulgata.Web/Data/RepositoryManagementCoordinator.cs` (FR-004/017/018; data-model.md).
- [ ] T025 [P] [US3] Add RED publication/retry/concurrency tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourcePersistenceTests.cs` for same-identity replay, cross-process identity lock, atomic seal/adopt/row insertion, crash between rename and insert, stale sealed-object cleanup, digest conflict, no partial visibility, concurrent readers, lease release, no canonical pruning, and fresh-authority retry (FR-005/014/017–020; SC-006/007/008).
- [ ] T026 [P] [US3] Add RED true separate-process tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceSeparateProcessTests.cs`, launching a helper from the existing `tests/Vulgata.Tests/Vulgata.Tests.csproj` test assembly or its existing executable entry point rather than creating a new project, and prove fresh application composition reopens after original process/lease ends with remote unavailable and no old token, temporary directory, clone, CodeGraph, LSP, LLM, semantic store, or cache (FR-015/016/021; SC-006/009).

### Implementation for User Story 3

- [ ] T027 [US3] Add the additive `SourceSnapshots` migration at `src/dotnet/Vulgata.Infrastructure/Data/Migrations/<timestamp>_AddRetainedSourceSnapshots.cs` and ensure `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs` applies it without altering prepared/pair-era tables or adopting historical disposable/archive state (FR-017/018; plan migration boundary).
- [ ] T028 [US3] Implement PostgreSQL repository persistence and immutable publication in `src/dotnet/Vulgata.Infrastructure/Data/RetainedSourceRepository.cs`, including the sole row visibility point, matching object verification, unique Repository/revision replay, and `DeleteBehavior.Restrict` semantics (FR-004/005/015/017/018; data-model.md).
- [ ] T029 [US3] Implement cross-process identity locking, atomic same-volume seal/adoption, crash recovery, stale-operation cleanup, and digest-conflict handling in `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs` and `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`, with no fallible cleanup or remote call after successful row commit (FR-005/014/017–020; plan Architecture Decision 3).
- [ ] T030 [US3] Add RED trusted-workspace boundary tests in `tests/Vulgata.Tests/RetainedSource/TrustedSourceWorkspaceTests.cs` proving the physical provider is absent from Core/ordinary contracts, requires a separately authorized Infrastructure purpose, refuses symlink/gitlink/unsafe/colliding paths, contains materialization, and cleans only its owned disposable workspace (FR-012/014/016; plan Architecture Decision 6).
- [ ] T045 [US3] Implement contained trusted workspace materialization in `src/dotnet/Vulgata.Infrastructure/Source/TrustedSourceWorkspaceProvider.cs`, available only to separately authorized Infrastructure adapters; T030 must be RED first and T032 must include focused/surrounding GREEN verification (FR-012/014/016; plan Architecture Decision 6).
- [ ] T031 [US3] Complete production DI and fresh-process composition in `src/dotnet/Vulgata.Infrastructure/DependencyInjection.cs`, `src/dotnet/Vulgata.Web/Program.cs`, and the separate-process fixture files from T026, sharing only configured PostgreSQL/source storage and reopening through the public application contract (FR-015/016/021; SC-006/009).
- [ ] T032 [US3] Run the PostgreSQL, persistence, and separate-process suites plus surrounding migration checks, recording evidence for SC-006/007/008 and verifying remote unavailability, process restart, and canonical-source retention independently of CodeGraph data-root/workspace cleanup.

## Phase 6: User Story 4 — Use the Source Capability Independently (Priority: P2)

**Goal**: Make source-only qualification and reporting bounded, reproducible, and clearly separated from later Scan/Knowledge/Chat/deployment claims.

**Independent Test**: The complete Story 13.1 suite runs on isolated local Git fixtures and declared persistent source storage with analysis tools/semantic stores absent, and evidence classifies outcomes without overclaiming later capabilities (FR-021/022, SC-009/010).

### Tests and verification for User Story 4

- [ ] T033 [US4] Add a source-only qualification harness in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCapabilityBoundaryTests.cs` that fails if CodeGraph, LSP, LLM, semantic Knowledge, Chat, full Scan, Docker-stack, backup, or restore dependencies are invoked while exercising capture, read, denial, cancellation, concurrency, and reopen (FR-021; SC-009).
- [ ] T034 [US4] Add evidence assertions and outcome classification checks in `tests/Vulgata.Tests/RetainedSource/RetainedSourceCapabilityBoundaryTests.cs` for success, limitation, denial, cancellation, acquisition/integrity/storage failure, and explicit source-only qualification labels; ensure no result claims Scan/wiki/Chat/full-Docker completion (FR-019/022; SC-010).
- [ ] T035 [US4] Validate `specs/015-retained-source-access/quickstart.md` commands and the focused test selection against the implemented paths, recording static contract, controlled test, PostgreSQL, and separate-process evidence as distinct classes without changing the quickstart artifact (quickstart.md; FR-022).

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Complete bounded validation and traceability after all vertical slices are green.

- [ ] T036 [P] Add RED sanitized-logging tests in `tests/Vulgata.Tests/RetainedSource/RetainedSourceLoggingTests.cs` for capture, reopen, authorization, deletion-conflict, and failure diagnostics, asserting no source, credentials, origin URL, raw unsafe path, storage key, or physical path is emitted (FR-012/019; NFR-2.3).
- [ ] T046 Harden sanitized logging and diagnostics in `src/dotnet/Vulgata.Infrastructure/Source/RetainedSourceCaptureService.cs`, `src/dotnet/Vulgata.Infrastructure/Source/DurableRetainedSourceStore.cs`, and `src/dotnet/Vulgata.Web/Data/SourceRepositoryAuthorizationVerifier.cs`; T036 must be RED first and T047 must verify focused/surrounding GREEN (FR-012/019; NFR-2.3).
- [ ] T047 Verify public Repository deletion-conflict and logging behavior with `tests/Vulgata.Tests/RetainedSource/RetainedSourcePostgreSqlTests.cs` and `tests/Vulgata.Tests/RetainedSource/RetainedSourceLoggingTests.cs`, preserving sanitized Chinese `409` behavior and the no-disclosure assertions (FR-012/017/019; NFR-2.3).
- [ ] T037 Run the complete retained-source test set from `tests/Vulgata.Tests/RetainedSource/`, relevant EF migration/delete tests, and `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore`; confirm all SC-001–SC-010 predicates and no unrelated project/test changes (quickstart.md; plan §7).
- [ ] T038 Perform final source-only boundary and implementation-scope verification against `specs/015-retained-source-access/spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, both contracts, and `docs/agent-workflow/e2e-decision-matrix.md`; record that later E2E authorization/persistence/migration predicates remain for the governed gate and are not predeclared skipped (FR-021/022; SC-009/010).

## Requirements and Success-Criteria Coverage

| Coverage | Tasks |
| --- | --- |
| FR-001–005 capture, identity, atomic completion | T004–T005, T009–T016, T027–T029, T039–T044 |
| FR-006–014 scoped grants, reads, containment, live authority, bounds, cleanup | T005–T007, T017–T023, T030, T036, T045–T047 |
| FR-015–020 durable reopen, stable retries, retention, truthful failures | T008, T024–T032, T041–T045 |
| FR-021–022 independent use and evidence boundary | T001–T003, T026, T031, T033–T038 |
| SC-001/002 exact/system/standalone capture | T009, T011–T016 |
| SC-003 exact relative reads | T017–T023 |
| SC-004 denial/disclosure | T018–T023, T036 |
| SC-005 bounds/cancellation | T010, T018, T020, T023 |
| SC-006 fresh-process reopen | T026, T031–T032 |
| SC-007 concurrency/stable identity | T025, T028–T032 |
| SC-008 truthful failure/retry | T010, T025, T029, T032 |
| SC-009 source-only qualification | T001–T003, T026, T033–T035 |
| SC-010 outcome/evidence classification | T034–T038 |

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001–T003 may run in parallel; they establish test seams and contract guards.
- **Foundational (Phase 2)**: T004–T008 establish contracts and RED tests; T039–T044 then implement and verify policy, manifest, and EF/publication shape before any story implementation. T005 is the only Core interface declaration; workspace remains Infrastructure-only.
- **User Stories**: US1 follows Phase 2 and is the MVP. US2 follows the completed capture contract and can begin once T004–T007 exist, but its integrated verification depends on US1 capture fixtures. US3 depends on capture/store contracts and extends persistence/publication; US4 depends on all source behavior.
- **Polish**: T036–T038 follow all desired stories and their focused tests.

### User Story Dependencies

- **US1 (P1)**: Phase 2 only; MVP and source identity owner.
- **US2 (P1)**: Phase 2 plus a completed Source Snapshot fixture from US1; no later-story dependency.
- **US3 (P1)**: Phase 2 plus US1 capture/store seams; T024–T026 RED tests may run in parallel with US2 tests because they use distinct files, but T027–T031 and T045 integrate only after T008/T041/T044 and the capture/store contract are stable.
- **US4 (P2)**: Depends on US1–US3 behavior and evidence; it adds qualification guards only.

### Within Each User Story

Tests are written and observed RED before production work. Implement the narrowest seam needed for GREEN, then run the focused and surrounding tests. Keep Core contracts before Infrastructure, Infrastructure before Web composition, and migration/persistence before fresh-process proof. Test-only and evidence-boundary tasks do not fabricate a RED result for already-correct behavior.

## Parallel Execution Examples

### Setup and foundational

```text
T001, T002, T003 may run in parallel (distinct fixture/contract-test files).
After setup, T004 and T005 can be developed in parallel as declarations; T006–T008 RED tests follow, then T039–T044 run sequentially by policy, manifest, and EF/publication ownership.
```

### US1

```text
T009 and T010 are sequential because they share `RetainedSourceCaptureTests.cs`.
T011 and T012 may proceed in parallel after contract REDs; T013 is independent Web authorization work.
T014 depends on T011–T013; T015 depends on T004–T006 and T014; T016 follows the slice.
```

### US2

```text
T017 and T018 are sequential because they share `RetainedSourceReadContextTests.cs`; T019 may run in parallel in its distinct test file.
T020 and T021 may proceed in parallel after REDs; T022 integrates live authorization and T023 verifies the slice.
```

### US3

```text
T024, T025, and T026 may run in parallel (distinct PostgreSQL, persistence, and helper-process test files).
T027 and T028 are sequential persistence setup; T029 depends on both. T030 is the trusted-workspace RED seam and T045 is its implementation before T031; T031 depends on T027–T030, T041, T044, and T045; T032 validates the complete durable slice.
```

### US4 and final validation

```text
T033 and T034 are sequential because they share `RetainedSourceCapabilityBoundaryTests.cs`.
T035 follows the implemented paths; T036 is the logging RED task, T046 implements it, and T047 verifies logging plus deletion conflict; T037–T038 are final sequential checks.
```

No parallel group edits the same production file concurrently without a declared owner. Where tasks name the same file, the dependency order above governs the edits.

## Implementation Strategy

### MVP First (User Story 1)

1. Complete T001–T008, then T039–T044 as the policy/manifest/EF RED→GREEN prerequisite slices.
2. Execute T009–T016 as a RED→GREEN capture slice.
3. Stop at the US1 checkpoint and verify exact commit identity, standalone capture, raw bytes, integrity, cancellation, and no partial publication.

### Incremental Delivery

1. Add US2 scoped read grants and bounded operations; verify independently while preserving US1 capture behavior.
2. Add US3 PostgreSQL publication, concurrent retry/adoption, durable retention, trusted workspace boundary, and true separate-process reopen.
3. Add US4 source-only qualification/evidence guards.
4. Run final polish and all success-criteria predicates only after the vertical slices are green.

## Self-Review Record

- **Source-reference coverage**: PASS. The raw Story 13.1 boundary, `spec.md` FR/SC, complete plan decisions, `research.md` alternatives, `data-model.md` invariants, both contracts, `quickstart.md`, accepted plan-review/authorization boundary, E2E decision matrix, and directly inspected Core/Infrastructure/Web/test seams are reflected in task descriptions and coverage tables.
- **Exact paths**: PASS. Every checklist task names a concrete repository-relative file or directory. The only generated artifact is the EF migration at `src/dotnet/Vulgata.Infrastructure/Data/Migrations/<timestamp>_AddRetainedSourceSnapshots.cs`, whose timestamp is generated by the migration tool while the intent and location are fixed.
- **Dependency order**: PASS after correction. Core declarations precede tests; T006–T008 RED tests precede T039–T041 implementations and T042–T044 focused verification; capture precedes reads; publication/migration precede fresh-process reopen; trusted workspace T030 RED precedes T045 implementation; logging T036 RED precedes T046 implementation and T047 verification.
- **Vertical behavioral slices**: PASS after correction. Policy, manifest, EF/publication, capture, read, persistence, trusted-workspace, logging, and source-only slices each have explicit RED, minimal GREEN, and focused/surrounding verification ownership. US1 remains independently demonstrable MVP; no giant behavior-first foundation or same-file parallel edit is required.
- **Validation criteria**: PASS. T016/T023/T032/T035/T037/T038 identify focused tests, PostgreSQL, migration, separate-process, source-only, and evidence-classification checks. E2E is left for the governed later gate; it is not predeclared skipped.
- **Definition findings**: DR-13.1-001 RESOLVED in tasks. Policy, manifest, EF/publication, trusted-workspace, and logging behaviors now have explicit preceding RED tests, minimal implementation tasks, and focused/surrounding verification tasks; compile failures remain intermediate diagnostics only. DR-13.1-002 RESOLVED in tasks. T005 contains only logical Core contracts, while T030/T045 keep the physical workspace seam entirely inside Infrastructure with a preceding boundary test.
- **Scope/authority**: PASS. No task changes spec/plan/state/evidence, adds a Worker Agent tool, activates a Scan, or claims composite PRD FR-4.9/NFR-4.1 completion.

---

description: "Dependency-ordered, test-first tasks for Story 11.1"
---

# Tasks: Minimal Knowledge Store and Retrieval Projection

**Input**: Accepted design documents in `specs/004-minimal-knowledge-store/`

**Prerequisites**: `spec.md`, `plan.md`, `research.md`, `data-model.md`,
`quickstart.md`, `contracts/canonical-document-content.md`, and
`contracts/knowledge-store-repository.md`

**Execution level**: `expedited-mvp`

**Story authority**:
`docs/bmad/implementation-artifacts/11-1-minimal-knowledge-store-and-retrieval-projection.md`

**Pinned predecessor**:
`b851ba165d6ebe560227bfdc3b206a814280e9b2`

**Tests**: Required. Every bounded production group starts with an observed
transient RED, reaches GREEN, and completes a REFACTOR verification before the
next group changes production. Record the command, exit code, pinned boundary,
timestamp, and concise result in the sprint-loop handoff; never commit a failing
test.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: May run in parallel because it owns different files and has no
  dependency on an incomplete task.
- **[Story]**: Maps the task to a user story in `spec.md`.
- Every task names its exact write or validation path.

## Ownership and collision rules

| Lane | Exclusive paths | Scheduling rule |
| --- | --- | --- |
| Integration owner | `tests/Vulgata.Tests/MigrationTests.cs:142`, `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_AddMinimalKnowledgeStore.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_AddMinimalKnowledgeStore.Designer.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs` | Only this owner may modify these paths. The Story 4.3 edit is limited to the approved migration pin. |
| Knowledge-store domain lane | `src/dotnet/Vulgata.Core/Entities/KnowledgeDocumentTypes.cs`, the nine new knowledge-store entity files under `src/dotnet/Vulgata.Core/Entities/`, `src/dotnet/Vulgata.Core/DomainServices/IKnowledgeStoreRepository.cs`, `src/dotnet/Vulgata.Core/DomainServices/KnowledgeStoreContracts.cs`, `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeDocumentMaterializer.cs`, `tests/Vulgata.Tests/KnowledgeStoreDomainTests.cs` | G1 completes before the persistence lane implements against these contracts. |
| Knowledge-store persistence lane | the nine new files under `src/dotnet/Vulgata.Infrastructure/Data/Configurations/`, `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeStoreRepository.cs`, `src/dotnet/Vulgata.Web/Program.cs`, `tests/Vulgata.Tests/KnowledgeStorePersistenceTests.cs`, `tests/Vulgata.Tests/KnowledgeStoreRetrievalTests.cs`, `tests/Vulgata.Tests/KnowledgeStorePostgreSqlConcurrencyTests.cs` | G2 through G5 are serialized through one owner; do not run workers concurrently against `KnowledgeStoreRepository.cs`. |
| Test-harness lane | `tests/Vulgata.Tests/KnowledgeStoreTestFixtures.cs`, `tests/Vulgata.Tests/KnowledgeStoreTestHarness.cs` | May prepare both files in parallel before any production edit. |
| Root/review lanes | `specs/004-minimal-knowledge-store/workflow-state.json`, BMad lifecycle files, and read-only review/E2E commands | Root alone updates lifecycle state, stages, and commits. Reviewers and E2E runners are read-only. |

The integration owner must generate the migration only after the knowledge-store
entities and configurations are stable. Knowledge-store workers must not edit
any integration-owner path or any other Story 4.3 file.

---

## Phase 1: Setup and protected predecessor compatibility

**Purpose**: Record and verify the one authorized predecessor compatibility
change before Story 11.1 adds a later migration.

- [ ] T001 Integration owner only: replace the latest-migration call with `migrator.Migrate("20260730083207_AddConservativePreScanGate");` at `tests/Vulgata.Tests/MigrationTests.cs:142` and make no other Story 4.3 edit
- [ ] T002 Verify the diff from `b851ba165d6ebe560227bfdc3b206a814280e9b2` changes no Story 4.3 path except the exact line in `tests/Vulgata.Tests/MigrationTests.cs`
- [ ] T003 Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~PreScanMigrationTests"` and record the passing command/exit/boundary evidence for `tests/Vulgata.Tests/MigrationTests.cs`

**Checkpoint**: The protected predecessor test is pinned to its own migration
and remains GREEN before Story 11.1 model evolution.

---

## Phase 2: Foundational test harness

**Purpose**: Prepare deterministic, provider-aware test-only infrastructure.
These tasks add no production behavior.

- [ ] T004 [P] Add deterministic Repository, ScanRun, Code Unit, canonical-content, exact-boundary, and two-Repository fixture builders with stable GUIDs in `tests/Vulgata.Tests/KnowledgeStoreTestFixtures.cs`
- [ ] T005 [P] Add SQLite foreign-key-enabled context creation, parameter-capture, injected `SaveChanges` failure points, change-tracker cleanup assertions, and PostgreSQL barrier helpers in `tests/Vulgata.Tests/KnowledgeStoreTestHarness.cs`

**Checkpoint**: G1 through G6 can observe reproducible RED/GREEN results without
production-only failure hooks.

---

## Phase 3: User Story 1 — Commit Traceable Knowledge Atomically (Priority: P1) MVP

**Goal**: Commit one bounded Code Logic Document or Business Logic Document
with immutable content, exact provenance, narrow flow evidence, and complete
retrieval projections as one durable unit.

**Independent Test**: Commit valid Code Logic and Business Logic fixtures, then
exercise every deterministic rejection and injected persistence boundary.
Success exposes exactly one complete aggregate; every failure exposes zero new
knowledge-store rows.

### G1 — Closed domain content and deterministic materialization

- [ ] T006 [US1] Before editing production, add failing tests for exact JSON allowlists, duplicate/unknown fields, closed enum/schema values, Form-C normalization, forbidden controls, safe Repository-relative paths, all required credential/host-path/raw-source/raw-diagnostic rejection fixtures and safe counterexamples, every exact UTF-8/count limit plus one-byte/one-item overflow, contiguous section/flow ordering, Edge semantics, deterministic JSON/Markdown/fingerprint, and projection bounds in `tests/Vulgata.Tests/KnowledgeStoreDomainTests.cs`; run the focused test and capture the observed RED
- [ ] T007 [US1] Implement the closed enums/value rules and immutable aggregate plus specific repository contracts in `src/dotnet/Vulgata.Core/Entities/KnowledgeDocumentTypes.cs`, `src/dotnet/Vulgata.Core/Entities/LogicalDocument.cs`, `src/dotnet/Vulgata.Core/Entities/DocumentVersion.cs`, `src/dotnet/Vulgata.Core/Entities/SourceAnchor.cs`, `src/dotnet/Vulgata.Core/Entities/DocumentEdge.cs`, `src/dotnet/Vulgata.Core/Entities/FlowGroup.cs`, `src/dotnet/Vulgata.Core/Entities/FlowTrace.cs`, `src/dotnet/Vulgata.Core/Entities/FlowTraceStep.cs`, `src/dotnet/Vulgata.Core/Entities/IndexEntry.cs`, `src/dotnet/Vulgata.Core/Entities/DocumentSection.cs`, `src/dotnet/Vulgata.Core/DomainServices/IKnowledgeStoreRepository.cs`, and `src/dotnet/Vulgata.Core/DomainServices/KnowledgeStoreContracts.cs`
- [ ] T008 [US1] Implement strict canonical parsing/normalization, deterministic Markdown/fingerprint generation, and bounded `IndexEntry`/ordered `DocumentSection` derivation without filesystem or `index.md` access in `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeDocumentMaterializer.cs`
- [ ] T009 [US1] Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStoreDomainTests"`, refactor only `src/dotnet/Vulgata.Core/Entities/`, `src/dotnet/Vulgata.Core/DomainServices/`, and `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeDocumentMaterializer.cs`, then rerun to record G1 GREEN

### G2 — Atomic initial commit and relational ownership

- [ ] T010 [US1] Before editing persistence production, add failing SQLite relational tests for valid Code Logic/Business Logic initial commits, exact `REFERENCES`/`GENERATED_FROM` endpoints, ordered flow evidence, validation rejection, fault injection at each save boundary, zero partial rows, no `FOR UPDATE` on SQLite, tracker cleanup, and direct composite-FK corruption attempts for current/successor ownership, anchor Repository/ScanRun/Code Unit ownership, and both Edge endpoint tuples in `tests/Vulgata.Tests/KnowledgeStorePersistenceTests.cs`; run the focused test and capture the observed RED
- [ ] T011 [US1] Map owner-safe alternate/composite keys, closed checks, restrictive/no-action deletes, concurrency tokens, uniqueness, and Repository-scoped indexes in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/LogicalDocumentConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/DocumentVersionConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/SourceAnchorConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/DocumentEdgeConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/FlowGroupConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/FlowTraceConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/FlowTraceStepConfiguration.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Configurations/IndexEntryConfiguration.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/Configurations/DocumentSectionConfiguration.cs`
- [ ] T012 [US1] Implement pre-validation, authorized endpoint checks, SQLite serializable/no-op write reservation, execution-strategy-compatible atomic initial commit, closed rejection mapping, rollback, and tracker cleanup in `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeStoreRepository.cs`, then register only `IKnowledgeStoreRepository` in `src/dotnet/Vulgata.Web/Program.cs`
- [ ] T013 [US1] Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStorePersistenceTests"`, refactor only the G2 configuration/repository paths, and rerun to record G2 GREEN with every composite-FK corruption fixture rejected

### G6 — Additive EF migration owned by the integration owner

- [ ] T014 [US1] Integration owner only: before generating the migration, add failing operation/model tests for exactly nine Story 11.1 tables, permitted additive operations, composite FKs, restrictive deletes, checks, uniqueness, query indexes, clean apply, rollback to `20260730083207_AddConservativePreScanGate`, reapply, and preservation of Story 4.3 schema in `tests/Vulgata.Tests/KnowledgeStoreMigrationTests.cs`; run the focused test and capture the observed RED
- [ ] T015 [US1] Integration owner only: add the nine knowledge-store `DbSet` properties in `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs` and generate `AddMinimalKnowledgeStore` into `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_AddMinimalKnowledgeStore.cs`, `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_AddMinimalKnowledgeStore.Designer.cs`, and `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs` without editing a prior migration
- [ ] T016 [US1] Integration owner only: run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStoreMigrationTests|FullyQualifiedName~PreScanMigrationTests"`, refactor only Story 11.1 EF-owned files, and rerun to record G6 GREEN
- [ ] T017 [US1] Run the complete US1 slice with `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStoreDomainTests|FullyQualifiedName~KnowledgeStorePersistenceTests|FullyQualifiedName~KnowledgeStoreMigrationTests|FullyQualifiedName~PreScanMigrationTests"` and verify the observable rows against `specs/004-minimal-knowledge-store/spec.md`

**Checkpoint**: User Story 1 is independently GREEN: complete commits are
visible atomically, invalid/corrupt commits leave no facts, and the additive
model preserves Story 4.3.

---

## Phase 4: User Story 2 — Retrieve Current Authorized Knowledge (Priority: P1)

**Goal**: Materialize only current, finite, Repository-authorized knowledge and
fully redact unauthorized relationship targets.

**Independent Test**: Retrieve the same Logical Document with full, partial,
missing, unauthorized, and revoked Repository scopes and with 0–100 requested
sections. Hidden source access is indistinguishable from missing; restricted
targets expose only the restricted marker.

### G5 — Repository-scoped retrieval and redaction

- [ ] T018 [US2] Before editing retrieval production, add failing tests for current-only materialization, exact requested-section ordering, unknown section IDs, duplicate/over-100 section rejection, 1–256 authorized Repository IDs, hidden-source null equivalence, scope revocation, bounded anchors/Edges/flows, and full Edge/flow-step/section-reference target redaction with no Edge type/ID, Repository/Document/Version ID, title, path, anchor, content, term, behavior, source, secret, or provider diagnostic in `tests/Vulgata.Tests/KnowledgeStoreRetrievalTests.cs`; run the focused test and capture the observed RED
- [ ] T019 [US2] Add immutable finite read models, `RepositoryAccessScope`, requested-section validation, and restricted-relationship shapes without EF types in `src/dotnet/Vulgata.Core/DomainServices/KnowledgeStoreContracts.cs` and `src/dotnet/Vulgata.Core/DomainServices/IKnowledgeStoreRepository.cs`
- [ ] T020 [US2] Implement Repository-first `GetCurrentAsync`, no-tracking materialization, current-version selection, requested-section filtering, finite projection loading, and target redaction before restricted data crosses Infrastructure in `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeStoreRepository.cs`
- [ ] T021 [US2] Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStoreRetrievalTests"`, refactor only the G5 contract/repository paths, and rerun to record G5 GREEN
- [ ] T022 [US2] Run the independent authorization slice in `tests/Vulgata.Tests/KnowledgeStoreRetrievalTests.cs` twice—before and after revoking a target/source Repository from the supplied scope—and verify no tracked entity, cached authorization decision, raw source, host path, secret, or diagnostic is returned

**Checkpoint**: User Story 2 is independently GREEN and every retrieval applies
current Repository scope as its outer boundary.

---

## Phase 5: User Story 3 — Preserve History and Rebuild Retrieval Truth (Priority: P2)

**Goal**: Insert a successor without rewriting history and replace only one
immutable version's derived retrieval projection atomically.

**Independent Test**: Commit version N+1, prove the Version Chain and exact
historical evidence, race two successor writers, race an Edge commit against
target supersession, and rebuild a projection twice plus one injected failure.

### G3/G4 — Version Chain, rebuild, retry, and endpoint currentness

- [ ] T023 [US3] Before editing production, add failing tests for `ExpectedCurrentVersionId`, one immutable successor, predecessor-only `SupersededById` transition, exactly one current pointer, preserved historical content/anchors/Edges/flows, stale-writer conflict with zero losing rows, repeatable projection rebuild, replacement limited to one version, injected rebuild rollback, unchanged canonical fingerprints, and zero `index.md` reads in `tests/Vulgata.Tests/KnowledgeStorePersistenceTests.cs`; run the focused test and capture the observed RED
- [ ] T024 [US3] Implement optimistic supersession and Version Chain transitions plus canonical-JSON-only, transactionally replaced `IndexEntry`/`DocumentSection` rebuild in `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeStoreRepository.cs` and `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeDocumentMaterializer.cs`
- [ ] T025 [US3] Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStorePersistenceTests"`, refactor only the G3/G4 repository/materializer paths, and rerun to record SQLite sequential/rollback GREEN
- [ ] T026 [US3] Before editing PostgreSQL locking production, add failing two-context barrier tests for deterministic database-UUID owner-lock order, two writers starting from one current version with exactly one committed successor, and a waiting commit that revalidates a superseded target as `NonCurrentEndpoint`, plus PostgreSQL composite-FK corruption attempts and zero losing/stale child, Edge, or projection rows in `tests/Vulgata.Tests/KnowledgeStorePostgreSqlConcurrencyTests.cs`; run against disposable PostgreSQL and capture the observed RED
- [ ] T027 [US3] Implement parameterized sorted-owner `SELECT ... ORDER BY "Id" FOR UPDATE`, post-lock endpoint/source revalidation, whole-transaction execution-strategy replay with stable operation/version IDs, commit verification, closed conflict/rejection results, and provider-safe SQLite branching in `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeStoreRepository.cs`
- [ ] T028 [US3] Run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter "FullyQualifiedName~KnowledgeStorePostgreSqlConcurrencyTests"` against disposable PostgreSQL, refactor only `src/dotnet/Vulgata.Infrastructure/Data/KnowledgeStoreRepository.cs`, and rerun to record G3 PostgreSQL GREEN with exactly one successor and no stale endpoint Edge
- [ ] T029 [US3] Run the full independent history/rebuild slice in `tests/Vulgata.Tests/KnowledgeStorePersistenceTests.cs` and `tests/Vulgata.Tests/KnowledgeStorePostgreSqlConcurrencyTests.cs` and verify both historical versions retain their exact original Document Version endpoints after supersession

**Checkpoint**: User Story 3 is independently GREEN under SQLite rollback
coverage and authoritative PostgreSQL two-context currentness races.

---

## Phase 6: Cross-cutting verification, expedited review, and E2E disposition

**Purpose**: Pin one reviewed candidate, obtain the required independent
combined judgment, execute the economical persistence E2E, and verify migration
and regression safety.

- [ ] T030 Refactor duplicated test builders and validate all rejection results remain closed and provider diagnostics absent across `tests/Vulgata.Tests/KnowledgeStoreDomainTests.cs`, `tests/Vulgata.Tests/KnowledgeStorePersistenceTests.cs`, `tests/Vulgata.Tests/KnowledgeStoreRetrievalTests.cs`, and `tests/Vulgata.Tests/KnowledgeStorePostgreSqlConcurrencyTests.cs`
- [ ] T031 Root only: after every included focused test is GREEN, pin the reviewed-candidate boundary and RED/GREEN/REFACTOR evidence in `specs/004-minimal-knowledge-store/workflow-state.json` without staging any failing state
- [ ] T032 Dispatch one fresh independent combined expedited quality review over `specs/004-minimal-knowledge-store/spec.md`, `specs/004-minimal-knowledge-store/plan.md`, `specs/004-minimal-knowledge-store/tasks.md`, the pinned implementation diff, all Story 11.1 tests, authorization/security boundaries, migration invariants, regressions, and known limitations; record ACCEPT or raw REVISE findings in `specs/004-minimal-knowledge-store/workflow-state.json`
- [ ] T033 After combined-review ACCEPT, have the E2E runner execute the focused domain, persistence, retrieval/redaction, PostgreSQL concurrency, and migration groups from `specs/004-minimal-knowledge-store/quickstart.md`; record `passed` or a fully justified current-boundary `skipped-covered` decision in `specs/004-minimal-knowledge-store/workflow-state.json`
- [ ] T034 Apply Story 11.1 to a clean disposable PostgreSQL database, roll back exactly to `20260730083207_AddConservativePreScanGate`, prove all nine Story 11.1 tables disappear while Story 4.3 remains, reapply, and run `dotnet ef migrations has-pending-model-changes --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web`; record exit-0 evidence against `src/dotnet/Vulgata.Infrastructure/Data/Migrations/VulgataDbContextModelSnapshot.cs`
- [ ] T035 Run `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj` and `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj`, with zero warnings/errors/failures, against `src/dotnet/Vulgata.Web/Vulgata.Web.csproj` and `tests/Vulgata.Tests/Vulgata.Tests.csproj`
- [ ] T036 Root only: verify the final diff adds no candidate-selection, dispatch-binding, endpoint-binding, Uncertainty, reverse-index, virtual-tree/export, public API, Agent tool, Scan strategy, container/runtime behavior, or Story 4.3 edit beyond the approved pin, then record verification and completion in `specs/004-minimal-knowledge-store/workflow-state.json`

**Checkpoint**: The Story 11.1 candidate is independently accepted, its
required focused E2E and PostgreSQL migration lifecycle pass, the full Web
build/test suite is clean, and Story 11.5 retains ownership of the final
clean-database two-Repository rehearsal and Docker image.

---

## Dependencies and execution order

### Phase dependencies

1. **Phase 1** is mandatory and blocks every Story 11.1 model/migration task.
2. **Phase 2** depends on Phase 1 and blocks all production edits.
3. **US1 / G1** depends on Phase 2.
4. **US1 / G2** depends on G1 GREEN because it implements the accepted Core
   contract.
5. **US1 / G6** depends on stable G1 entities and G2 configurations; only the
   integration owner may evolve EF-owned files.
6. **US2 / G5** depends on US1 GREEN and reuses the same serialized persistence
   owner.
7. **US3 / G3-G4** depends on US1 and US2 GREEN and reuses the same repository
   owner; its PostgreSQL RED must precede row-lock production.
8. **Phase 6** depends on every selected story group GREEN and REFACTORED.

### User story dependency graph

```text
Protected Story 4.3 pin
        |
Test harness
        |
US1: atomic durable aggregate + additive schema
        |
US2: authorized current retrieval
        |
US3: Version Chain + projection rebuild + PostgreSQL races
        |
Combined review -> focused E2E -> migration/full verification
```

- **US1 (P1 MVP)** is the minimum durable increment and can be validated
  independently.
- **US2 (P1)** requires US1's committed aggregate but has an independent
  authorization/redaction test boundary.
- **US3 (P2)** requires the US1 Version Chain schema and US2 materialized
  contract but independently proves history, rebuild, and concurrency.

### Bounded TDD group order

| Group | Required RED | GREEN owner | Independent completion criterion |
| --- | --- | --- | --- |
| G1 | `KnowledgeStoreDomainTests` fails on absent closed content/materialization behavior | Knowledge-store domain lane | All deterministic fixture families and exact bounds pass; output is canonical and finite. |
| G2 | `KnowledgeStorePersistenceTests` fails on absent atomic commit/relational invariants | Knowledge-store persistence lane | Valid aggregates are complete; validation, injected failure, and composite corruption leave zero partial facts. |
| G6 | `KnowledgeStoreMigrationTests` fails before the new migration exists | Integration owner | Exactly nine additive tables apply/rollback/reapply with Story 4.3 preserved. |
| G5 | `KnowledgeStoreRetrievalTests` fails on absent retrieval/redaction | Knowledge-store persistence lane | Full/partial/revoked scopes return only permitted finite materialized data. |
| G3/G4 | Persistence and PostgreSQL concurrency tests fail on absent supersession/rebuild/locking | Knowledge-store persistence lane | History/projections remain exact; two-context races admit no losing or stale facts. |

## Parallel opportunities

Only collision-free work is parallelized:

```text
After T003:
  T004 -> tests/Vulgata.Tests/KnowledgeStoreTestFixtures.cs
  T005 -> tests/Vulgata.Tests/KnowledgeStoreTestHarness.cs
```

After the reviewed candidate is pinned, read-only combined review preparation
may inspect the accepted artifacts while the root prepares command metadata,
but T032 remains one independent judgment and T033 waits for its ACCEPT.
Production tasks are intentionally serialized because G2–G5 share
`KnowledgeStoreRepository.cs`; migration generation waits for stable model
files.

## Implementation strategy

### MVP first

1. Complete the protected predecessor pin and test harness.
2. Complete G1 RED→GREEN→REFACTOR.
3. Complete G2 RED→GREEN→REFACTOR.
4. Complete integration-owner G6 RED→GREEN→REFACTOR.
5. Stop and independently validate US1 before retrieval/history work.

### Incremental delivery

1. **US1** delivers atomic durable knowledge and the additive schema.
2. **US2** adds finite authorized retrieval without public API/UI/Agent scope.
3. **US3** adds immutable Version Chain transitions, projection recovery, and
   authoritative PostgreSQL currentness races.
4. The expedited combined review, focused E2E, migration lifecycle, full
   build, and full test suite gate completion.

## Notes

- No task adds a public API, UI, Agent tool, Scan behavior, candidate/dispatch/
  endpoint binding, Uncertainty, reverse index, virtual tree, export behavior,
  or final Story 11.5 demonstration.
- `index_entries` and `document_sections` are database-backed retrieval truth;
  no task reads or parses `index.md`.
- No production failure hook is permitted; use the test interceptor in
  `tests/Vulgata.Tests/KnowledgeStoreTestHarness.cs`.
- PostgreSQL two-context tests—not SQLite—are authoritative for row-lock wait
  and endpoint-currentness behavior.
- Docker verification and the final clean-database two-Repository demonstration
  remain Story 11.5 because Story 11.1 changes no container/runtime behavior.

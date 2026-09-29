---

description: "Task list for Story 18.4 owned October batch setup"
---

# Tasks: Owned October Batch Setup

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [quickstart.md](quickstart.md), [batch setup contract](contracts/batch-setup-selection.md), and [provider contract](contracts/provider-binding.md). Story 18.1's accepted [source contract](../016-contained-source-tools/contracts/prepared-source-tools.md) is a dependency, not an implementation target.

**Prerequisites**: Current independent plan-only ACCEPT is `evidence/plan-review-05.json`. Complete-definition review and implementation approval still precede every production write. Runtime, database, migration and certificate work requires explicitly authorized owned targets; a reachable or previously used resource is not authority.

**Tests**: Changed production behavior uses one accepted public-seam RED, minimal GREEN, final-shape assessment and focused/surrounding GREEN per coherent group. Test owner writes only `tests/Vulgata.Tests/` and its `TestInfrastructure/suites.json` inventory; production owner writes only `src/dotnet/` and project/build configuration. Root owns gate evidence and Git. Existing correct behavior may use truthful test-only or direct verification without fabricated RED. Classify all new tests and helpers per `docs/agents/testing.md`; the default regression command is `pwsh -NoProfile -File eng/test.ps1`.

**Organization**: Phase 2 establishes shared contracts and storage; each later phase delivers one independently testable user story. A task citing `ID-*` implements or verifies that accepted decision. `ID-X*` alternatives and `ID-C*` consequences are intentionally not task citations.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add only the minimal second host shell required for two-host selection.

- [ ] T001 Create the minimal `src/dotnet/Vulgata.OctoberGenerator/Vulgata.OctoberGenerator.csproj` and project reference/build wiring, with no generator analysis loop; verify project restore/build directly before behavioral work. Route: nonbehavioral direct verification. (FR-012; ID-R02, ID-L02)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the one shared Core selection/setup port and isolated October storage before any host-specific behavior.

- [ ] T002 As test owner, add a RED public-port test in `tests/Vulgata.Tests/October/OctoberSetupPolicyTests.cs` for exact trusted target/batch/Repository selection, no request-supplied subject/connection/path, standalone Repository and sanitized closed outcomes; classify this test file in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-001, FR-002, FR-012; ID-I01, ID-I02, ID-T01)
- [ ] T003 As production owner after T002 RED, add the shared `PrepareAsync(OctoberSetupRequest, OctoberAuthorizedSelection, CancellationToken)` policy and `IOctoberBatchSelector.BindAsync(OctoberStartupSelection, CancellationToken)` closed results in `src/dotnet/Vulgata.Core/DomainServices/OctoberBatchSetupContracts.cs`; derive operator/target only from trusted authorization, reject selection-digest substitution, and return a bound capability only on Selected. Record minimal and final GREEN through T002's owner. (FR-001, FR-005, FR-012; ID-R01, ID-I01, ID-I02, ID-A01, ID-F01)
- [ ] T004 As test owner, add a RED model/isolation test in `tests/Vulgata.Tests/October/OctoberPersistenceModelTests.cs` for the dedicated fixed `october` schema, `october.__OctoberMigrationsHistory`, separate snapshot and zero October entities/configurations in `VulgataDbContext`; classify it in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-006, FR-009; ID-S03, ID-T02)
- [ ] T005 As production owner after T004 RED, establish the dedicated `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberDbContext.cs`, explicit configuration filtering in `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs`, and separate `src/dotnet/Vulgata.Infrastructure/Data/October/Migrations/` migration/history convention. Later entity-owning T011 and T013 must update the fixed-schema migration and snapshot; this context-only task does not claim their tables exist. Do not add October mappings to ordinary startup; record minimal/final GREEN with T004. (FR-009; ID-R02, ID-S03, ID-L02)

**Checkpoint**: One Core contract exists; October storage is a separate model and migration history. No real database has been touched by these tasks.

---

## Phase 3: User Story 1 - Create a valid owned batch (Priority: P1) 🎯 MVP

**Goal**: One authorized operator can prepare an atomic, valid batch with organizational/context provenance and a non-forgeable, live prepared-source approval association.

**Independent Test**: A permitted System owner/Administrator can prepare a System member and a standalone Repository; denied, substituted, cancelled or conflicting requests leave no selectable partial batch or invented pair/FlowGroup/ScanRun.

### Behavioral RED and implementation slices

- [ ] T006 [US1] As test owner, add RED API tests for authenticated `POST /api/october-batch-activation-grants` and certificate-authenticated `POST /api/october-batch-activation-grants/validations` in `tests/Vulgata.Tests/October/OctoberActivationApiTests.cs`; cover cookie/antiforgery, real ManagementAccess, System member versus standalone Repository, allowlisted target, canonical digest, forged/expired/substituted grant, missing/out-of-scope certificate, 400/401/403/503 ProblemDetails and cancellation. Classify the test/helper files in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-001, FR-002; ID-I05, ID-I06, ID-A01, ID-F01, ID-L01, ID-T01)
- [ ] T007 [US1] As production owner after T006 RED, implement the closed Web grant/validation routes and target-scoped mutual-TLS generator proof in `src/dotnet/Vulgata.Web/Program.cs` and `src/dotnet/Vulgata.Web/Data/OctoberBatchActivationService.cs`. Use existing Identity, Data Protection and live Repository/target checks; ordinary grant issuance must not open an October DB. Record minimal/final GREEN with T006. (FR-001, FR-002; ID-R01, ID-I05, ID-I06, ID-L01, ID-F01)
- [ ] T008 [US1] As test owner, add RED shared-policy and selected-context tests in `tests/Vulgata.Tests/October/OctoberSetupPolicyTests.cs` for exact selection-digest comparison, real subject, valid membership, standalone `SystemId == null`, distinct Global/System/Repository value/scope/origin/selection time, explicit nonapplicable scope and retained disagreement; assert advisory text is never source proof. (FR-002, FR-003; ID-I01, ID-R01, ID-T01)
- [ ] T009 [US1] As production owner after T008 RED, implement the shared setup policy and context-provenance selection in `src/dotnet/Vulgata.Core/DomainServices/OctoberBatchSetupPolicy.cs`; use existing management context records without flattening away origin/conflict, and record minimal/final GREEN with T008. (FR-002, FR-003; ID-R01, ID-L01, ID-S02)
- [ ] T010 [US1] As test owner, add RED selected-store tests in `tests/Vulgata.Tests/October/OctoberSetupStoreTests.cs` for an October Batch unique within its database with only `NotSelectable` or `Selectable` state; one selected batch may have multiple Repository selections, but each `(BatchId, RepositoryId)` has at most one immutable versioned opaque root binding. Assert all setup-owned rows commit together, cancellation rolls back, exact identical retry observes the committed identity and a conflicting retry cannot overwrite it; classify the test in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-001, FR-002, FR-004; ID-I04, ID-S02, ID-F02, ID-T01)
- [ ] T011 [US1] As production owner after T010 RED, implement only October Batch, Repository selection, advisory context and opaque root-binding persistence plus the selected writer in `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberSetupStore.cs` and `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberDbContext.cs`; add their tables, constraints and separate fixed-`october` schema/history snapshot in `src/dotnet/Vulgata.Infrastructure/Data/October/Migrations/`. Do not store `PhysicalRootPath`, activation-token bytes, source bytes, handles or continuations; do not create pair/FlowGroup/ScanRun/Knowledge records. Record minimal/final GREEN with T010. (FR-001, FR-002, FR-003, FR-004, FR-009, FR-011; ID-R02, ID-I04, ID-S02, ID-S03, ID-F02, ID-L03)
- [ ] T012 [US1] As test owner, add RED public-route tests for `POST /api/october-prepared-source-approvals` and `POST /api/october-prepared-source-approvals/verifications` in `tests/Vulgata.Tests/October/OctoberPreparedSourceApprovalApiTests.cs`. A current approval must bind the exact `BatchId`, `RepositoryId`, real `SubjectId`, `WorkerSourceRead`, `ApprovalIdentity`, `RootBindingIdentity`, `OperatorLabel`, expiry and revocation state, without token/path; stale/foreign/expired/role-revoked/membership-changed/target-changed/mapping-rotated and unavailable cases fail closed with sanitized API statuses and cancellation. Classify the test in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-001, FR-011; ID-I04, ID-I05, ID-I06, ID-S02, ID-T04)
- [ ] T013 [US1] As production owner after T012 RED, implement short-lived approval records and the two closed Web routes in `src/dotnet/Vulgata.Web/Data/OctoberPreparedSourceApprovalService.cs`, `src/dotnet/Vulgata.Web/Program.cs` and `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberSetupStore.cs`; update the approval table/constraints and separate fixed-`october` migration/snapshot in `src/dotnet/Vulgata.Infrastructure/Data/October/Migrations/`. Recheck exact current Identity/Repository/target/binding authority at creation and verification; definite denial maps Revoked and indeterminate/unreachable authority maps Unavailable. Record minimal/final GREEN with T012. (FR-001, FR-009, FR-011; ID-I04, ID-I05, ID-I06, ID-S02, ID-S03, ID-A01, ID-F01)
- [ ] T014 [US1] As test owner, add RED direct public-seam tests of the missing Story 18.4 `OctoberPreparedSourceAuthorityVerifier` and `PreparedSourceBinding` in `tests/Vulgata.Tests/October/OctoberPreparedSourceBindingTests.cs`. Instantiate the production adapter with controlled Web approval responses, setup records, trusted root-version mapping and an owned root, then call it through the existing `PreparedSourceContextIssuer`. The new adapter must map exact approval tuple/root version to Current, definite revocation/change to Revoked and outage/indeterminate authority to Unavailable at issuance and before disclosure; it must create no context on failed issue, issue separate contexts per Repository and release the caller-owned context after borrowed calls settle. Expect RED because the October adapter/composition is absent. Reuse unchanged Story 18.1 issuer/function status, three-function schema, containment, cancellation, disposal and continuation tests as GREEN regression evidence rather than requiring them to fail; classify this test in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-011; ID-I04, ID-I05, ID-S04, ID-A01, ID-F01, ID-L03, ID-T04)
- [ ] T015 [US1] As production owner after T014 RED, supply the generator-side production `IPreparedSourceCurrentAuthorityVerifier` and trusted mapping/issuance composition in `src/dotnet/Vulgata.Infrastructure/Source/OctoberPreparedSourceAuthorityVerifier.cs` and `src/dotnet/Vulgata.OctoberGenerator/PreparedSourceBinding.cs`. Call existing `PreparedSourceContextIssuer.IssueAsync(PreparedSourceGrantRequest, CancellationToken)` with the exact current Web/setup/root binding; borrow one issued context for existing `PreparedSourceFunctions.Create()`, await calls and dispose it through trusted caller ownership. Never change Story 18.1 public contracts or add a fourth tool; record minimal/final GREEN with T014 and run existing 18.1 source regressions. (FR-011; ID-I04, ID-I05, ID-S04, ID-A01, ID-F01, ID-L03)

**Checkpoint**: US1 setup and direct source-binding seams are independently testable; valid setup does not claim later Worker dispatch or source-tool readiness.

---

## Phase 4: User Story 2 - Select exactly one batch and database (Priority: P1)

**Goal**: The minimal generator and conditional October Web bind the same exact authorized target/batch once per process and reject mismatch before downstream October data access.

**Independent Test**: With two explicitly owned databases carrying identical schema and disjoint IDs, actual generator `prepare`/`select` and October Web select A/A and B/B, reject A/B and missing pairs, and switch only by restart.

### Behavioral RED and implementation slices

- [ ] T016 [US2] As test owner, add RED selector tests in `tests/Vulgata.Tests/October/OctoberBatchSelectorTests.cs` for closed Selected/MissingTarget/MissingBatch/BatchMismatch/Denied/AuthorityUnavailable/Cancelled results, bound capability only on Selected, canonical connected-database identity and zero latest/other-target fallback; classify the test in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-005, FR-006; ID-I02, ID-S01, ID-F01, ID-T02)
- [ ] T017 [US2] As production owner after T016 RED, implement the selected October adapter in `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberBatchSelector.cs`, resolving only allowlisted deployment target IDs and binding one exact connection/factory to target plus batch for the process lifetime; perform read-only connected identity validation before migration and batch lookup before consumer access. Record minimal/final GREEN with T016. (FR-005, FR-006; ID-R02, ID-I02, ID-S01, ID-F01)
- [ ] T018 [US2] As test owner, add a SelfContained RED host-composition test in `tests/Vulgata.Tests/October/OctoberHostCompositionTests.cs` using controlled target/selector, grant-validation HTTP handler and certificate identity without a native process, listener, external database or real credential. Exercise generator `prepare` versus read-only `select` composition and conditional October Web wiring, exact A/A versus A/B selection, bounded cancellation, sanitized failure and restart-only binding; expect RED because the host composition is missing. Classify the file SelfContained in `tests/Vulgata.Tests/TestInfrastructure/suites.json`; reserve actual process, PostgreSQL and certificate qualification for T020 after root establishes owned resources. (FR-005, FR-006, FR-012; ID-I02, ID-I05, ID-S01, ID-T02)
- [ ] T019 [US2] As production owner after T018 RED, implement bounded `prepare`/`select` startup in `src/dotnet/Vulgata.OctoberGenerator/Program.cs`, its authenticated Web client in `src/dotnet/Vulgata.OctoberGenerator/OctoberWebAuthorityClient.cs`, and explicitly conditional October Web composition in `src/dotnet/Vulgata.Web/Program.cs`. Forward cancellation/deadline; run October migrations only in authorized prepare mode; select is read-only and performs no generation/Worker dispatch. Record minimal/final GREEN with T018. (FR-005, FR-006, FR-012; ID-R02, ID-I02, ID-I05, ID-A01, ID-F01, ID-L02)
- [ ] T020 [US2] After root establishes two disposable, explicitly owned PostgreSQL 17 targets plus a separate ordinary Identity target and owned certificate/allowlists, as test owner add and run actual generator-process and October Web integration qualification in `tests/Vulgata.Tests/October/OctoberHostSelectionTests.cs` through `pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql`. Classify it PostgreSql in `tests/Vulgata.Tests/TestInfrastructure/suites.json`; verify the complete post-T013 fixed schema and `october.__OctoberMigrationsHistory` on both fresh targets, A/A and B/B, A/B and missing rejection, no cross-target reads/writes, no fallback, select-mode read-only behavior and cleanup. Route: test-only/direct qualification of already GREEN host behavior, without fabricated RED or another story's database/credentials. (FR-005, FR-006, FR-009; ID-S01, ID-S03, ID-T02)

**Checkpoint**: Both real hosts can prove exact selection; this does not imply analysis, Knowledge or provider execution.

---

## Phase 5: User Story 3 - Bind context and provider roles explicitly (Priority: P2)

**Goal**: Context retains scope/origin/conflict, and each host binds complete Worker/Chat profiles from one declared source without hidden discovery, secret disclosure or failover.

**Independent Test**: Select conflicting Global/System/Repository values, then complete distinct/shared and incomplete provider profiles; retained context is qualified and provider failures are sanitized.

### Behavioral RED and implementation slices

- [ ] T021 [US3] As test owner, add RED integration tests in `tests/Vulgata.Tests/October/OctoberContextSelectionTests.cs` for Global, optional System and Repository record references, immutable selected values/scope/origin/time, explicit absent System context for standalone Repository and retained disagreement after management context changes; classify the test in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-002, FR-003; ID-R01, ID-S02, ID-L01, ID-T01)
- [ ] T022 [US3] As production owner after T021 RED, complete the Infrastructure selected-context projection in `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberContextSelectionStore.cs` and its use from `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberSetupStore.cs`; never substitute flattened `IContextCompositionService` text for origin-bearing values. Record minimal/final GREEN with T021. (FR-002, FR-003; ID-R01, ID-S02, ID-L01)
- [ ] T023 [US3] As test owner, add RED binder tests in `tests/Vulgata.Tests/October/OctoberProviderBindingTests.cs` for one explicitly declared source/precedence per process, complete API type/endpoint/model/protected credential reference per named Worker/Chat profile, deliberate shared profile, missing/conflicting field denial, no cross-source backfill, unspecified `.env` discovery, auto failover, secret/raw-source diagnostic or paid provider call; classify the test in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-007, FR-008, FR-010; ID-I03, ID-R03, ID-S01, ID-F01, ID-T03)
- [ ] T024 [US3] As production owner after T023 RED, implement atomic role-profile binding and sanitized validation in `src/dotnet/Vulgata.Infrastructure/Configuration/OctoberProviderBinder.cs` and compose it for each host in `src/dotnet/Vulgata.OctoberGenerator/Program.cs` and `src/dotnet/Vulgata.Web/Program.cs`. Use the explicitly selected host-supplied route; do not copy protected DB rows without a later reviewed compatibility decision. Record minimal/final GREEN with T023. (FR-007, FR-008; ID-R03, ID-I03, ID-S01, ID-F01)

**Checkpoint**: US3 context and provider bindings are independently observable; configuration validity is not paid-provider readiness.

---

## Phase 6: User Story 4 - Preserve October and ordinary isolation (Priority: P2)

**Goal**: October composition and migrations exist only in authorized October mode; ordinary Web remains unaffected and setup outcomes reveal no sensitive material.

**Independent Test**: Ordinary startup retains its EF model/migration/route behavior and applies zero October migrations; authorized October startup uses only its selected target, and every diagnostic is sanitized.

### Behavioral RED and implementation slices

- [ ] T025 [US4] As test owner, add RED ordinary-versus-October composition tests in `tests/Vulgata.Tests/October/OctoberStartupIsolationTests.cs` for ordinary `VulgataDbContext` model/configuration/migration list, existing Identity/domain migrations and routes, zero October registration/migrations on ordinary startup, and fail-closed October startup before serving requests; classify the test in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-009, FR-012; ID-R02, ID-S03, ID-L02, ID-T02)
- [ ] T026 [US4] As production owner after T025 RED, finish October-only service registration/migration gating in `src/dotnet/Vulgata.Web/Program.cs` and `src/dotnet/Vulgata.Infrastructure/Data/October/OctoberDbContext.cs`; preserve ordinary Identity/domain startup and source validation paths. Record minimal/final GREEN with T025. (FR-009; ID-R02, ID-S03, ID-L02, ID-F01)
- [ ] T027 [US4] As test owner, add RED diagnostics tests in `tests/Vulgata.Tests/October/OctoberSetupDiagnosticsTests.cs` for selected/missing/denied/unavailable/cancelled/storage/provider classes without grants, credentials, physical paths, raw source or false tool, paid-provider and Knowledge readiness; classify the test in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. (FR-008, FR-010; ID-F01, ID-T03)
- [ ] T028 [US4] As production owner after T027 RED, implement sanitized host/API outcome formatting in `src/dotnet/Vulgata.Web/Data/OctoberSetupDiagnostics.cs` and `src/dotnet/Vulgata.OctoberGenerator/OctoberSetupDiagnostics.cs`; preserve closed error classes and API ProblemDetails without login redirect/HTML re-execution. Record minimal/final GREEN with T027. (FR-008, FR-010; ID-I06, ID-F01)
- [ ] T029 [US4] After root supplies explicitly authorized owned PostgreSQL targets, a separate ordinary Identity target and certificate material, as test owner add/run `tests/Vulgata.Tests/October/OctoberPostgreSqlIsolationTests.cs` with `pwsh -NoProfile -File eng/test.ps1 -Suite PostgreSql`; classify that file PostgreSql in `tests/Vulgata.Tests/TestInfrastructure/suites.json`. On fresh targets prove the complete post-T013 fixed October schema, separate `october.__OctoberMigrationsHistory` and snapshot, ordinary versus October migrations/startup, no ordinary-target October migration or cross-target write, and runner cleanup. This is owned-resource qualification of already GREEN behavior, not a fabricated RED. (FR-006, FR-009; ID-S03, ID-T02)

**Checkpoint**: Ordinary operation remains independent; October failures are visible and sanitized.

---

## Phase 7: Cross-Cutting Verification

**Purpose**: Validate the assembled Story 18.4 boundary against its quickstart without broadening product scope.

- [ ] T030 Run the focused Story 18.4 `tests/Vulgata.Tests/October/` selection and the repository `pwsh -NoProfile -File eng/test.ps1` SelfContained command, plus the configured `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj`; verify `tests/Vulgata.Tests/TestInfrastructure/suites.json` classification and reconcile every result with `specs/017-october-batch-setup/quickstart.md`. Root records truthful test/build evidence; external-resource cases stay separate. (FR-001–FR-012; ID-T01, ID-T02, ID-T03, ID-T04)

---

## Dependencies & Execution Order

- Gate before T001: root confirms complete-definition ACCEPT and explicit implementation approval bound to the accepted hashes. No task line itself grants production or resource authority.
- T001 precedes generator-host work. T002→T003 and T004→T005 are foundational RED/GREEN slices; they precede story phases. Test owner and production owner retain disjoint paths and exchange evidence through root.
- US1 depends on Phase 2. Within US1: T006→T007, T008→T009, T010→T011, T012→T013 and T014→T015. Grant/selection and durable binding precede approval and issuer composition. US1 is the MVP.
- US2 depends on US1's valid setup and Phase 2. Within US2: T016→T017, then SelfContained controlled host RED T018→production T019. Only after root establishes owned PostgreSQL/Identity/certificate resources may T020 author/run the actual process/two-host qualification. Controlled composition alone is insufficient for the two-host acceptance outcome.
- US3 depends on US1's selected context and startup composition. Within US3: T021→T022 and T023→T024. The two slices may proceed independently only with disjoint files and settled host-composition ownership.
- US4 follows the relevant host/DB/provider slices. Within US4: T025→T026 and T027→T028; T029 requires authorized owned resources and settled host composition.
- T030 follows all behavioral slices and their final-shape focused/surrounding GREEN. Root then applies convergence, lookback, quality, conditional E2E and pinned final verification under the installed workflow.

## Parallel Example

After foundational GREEN, T006 grant-route tests and T010 selected-store tests can be written by separate test owners only if root grants disjoint files and serializes use of shared `suites.json`. T021 context and T023 provider RED tests can likewise be prepared independently. No parallel marker is asserted for work sharing `Program.cs`, `OctoberSetupStore.cs`, the test suite inventory, or one PostgreSQL runner/container.

## Implementation Strategy

1. Implement one accepted public-seam behavior slice at a time: test RED, minimal production GREEN, final affected-unit assessment, then focused and surrounding GREEN.
2. Complete US1 as the first valid batch setup. Add exact two-host selection in US2, then context/provider binding in US3 and ordinary-startup isolation in US4.
3. Establish owned PostgreSQL/certificate/Identity resources before T020 or T029; never require them for the SelfContained T018 RED. Run the repository's supported suite launcher and preserve complete fixed-schema/history and cleanup evidence. Do not use another story's database, credentials or process.
4. Keep JDT-LS, CodeGraph, Scan/Worker dispatch, paid provider execution, Knowledge acceptance/publication/read, Chat and later stories outside these tasks.

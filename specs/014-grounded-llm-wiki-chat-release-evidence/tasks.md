---
description: "Dependency-ordered Story 12.5 reviewed exact-profile seed and fresh Track C-MVP release tasks"
---

# Tasks: Story 12.5 Reviewed Exact-Profile Seeds and Fresh Release Continuation

**Input**: Corrected `spec.md`, accepted exact-profile `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/prepared-scan-initiation.md`, `contracts/release-evidence-contract.md`, `.specify/memory/constitution.md`, `AGENTS.md`, Story 12.5 in `docs/bmad/epics.md`, the August 31 exact-profile seed-selection Sprint Change Proposal, accepted `reviews/plan-review/plan-review-exact-profile-seeds-01.json`, accepted `decisions/planned-change-authorization-exact-profile-seed-selection-03.md`, the immutable run-03 diagnostic report, the existing task/evidence history, and the current source/tests named below.

**Pinned definition**: Source checkpoint `7fc5bd1b8b787d5ff205378abb9e6a464bf00eb5`; corrected spec SHA-256 `6da3e251b217ab8dd8190f84c4cf864b994be1e87090558a29e125f5648ec8b7`; corrected plan SHA-256 `35766983a07db621efbdef3fa45934b2f66d86514d15deb490b5eca83ca75c43`; accepted plan-review SHA-256 `31c848cfdc4e5522b57efb4c9673b13f16569d49d2f38891e810cf4cca2fb49a`; accepted planned-change scope SHA-256 `c8aea284ced8fdc564bfc505c3eea73bd96f7aec5a56008f3df7a24e73f0eecc`; accepted Track A/Track B checkpoint `f292caec427f9844be3c0f65b4cb00a9292080a5`; immutable Story 12.4 dependency `4798b97d9f23aef587829175df7ab7ebc3daf995`.

**Regeneration disposition**: T001-T035 remain checked and preserved as immutable completed history. This includes accepted Track A/B, the bounded Prepared Scan initiation entrance, the Prepared Pass 1 v2 identity authority, the public/postgreSQL replay and authorization corrections, all nine accepted run-03 Pass 1 retry-strategy corrections, and the completed run-03 preflight/database bootstrap. Run-03 subsequently completed Prepared Scan and Pass 1 but honestly exposed zero high-value seeds and a fail-closed exact-resolution refusal; its report and every associated database are immutable diagnostic evidence, never release success. The former unchecked T036-T045 release lane is replaced from the earliest affected boundary by T036-T060 below. No old unchecked completion claim transfers.

**Definition-review correction disposition**: The accepted plan-review is `plan-review-prepared-pass1-identity-authority-03.json`. DR-PSI-001 remains closed by T020-T022; DR-PSI-002 by T023-T024 and corrected T028-T029; DR-PSI-003 by T015-T017. This file is the only regenerated task artifact; no upstream artifact, source, workflow state, evidence result, stage, or commit is changed.

**Lifecycle authorization boundary**: Historical T001-T035 retain their then-valid approvals. For the current correction, accepted decision `story-12-5-planned-change-authorization-exact-profile-seed-selection-03` advances the accepted plan to task generation under `story-12-5-run-03-all-remaining-tasks-approval-02`. Before T037 or any new test/source/database/product write, an independent definition review must accept this exact `tasks.md`, the root orchestrator must persist an implementation-authorization binding to the accepted definition hash, and T036 must pass. The standing authorization covers the bounded correction, a fresh temporary database, and remote LLM egress, but waives no TDD, convergence, review, E2E, isolation, security, or final-verification gate.

**Classification rule**: `EXISTING-EVIDENCE` preserves accepted evidence without manufacturing a RED. `COMPATIBILITY` may be green on first run. `RED` must be observed failing at the named behavioral seam before its paired `GREEN`. `GREEN` is the smallest approved production change that turns its owning RED green. `GATE`, `RUN`, `QUERY`, `RESTART`, `MANUAL`, `REPORT`, and `VERIFY` neither waive later gates nor authorize scope expansion.

**TDD rule**: T011 and T025 remain preserved historical REDs for the accepted entrance and Prepared Pass 1 identity correction. Current T037-T039 are the non-substitutable exact-profile authority/planner, authenticated builder/composition, and disposable-PostgreSQL no-preseed REDs. They must fail for the missing reviewed-profile seed authority before the sole coordinated production GREEN T040. A direct planner test, SQLite substitute, pre-seeded Document/Edge, or run-03 observation cannot satisfy T039/T043.

**Hard boundaries**: No task may change `VulgataDbContext.cs`, `Data/Configurations/*`, migrations, `VulgataDbContextModelSnapshot`, any Story 12.4 contract/resolver/store, `ExactResolutionProfile`, `ExactResolutionProfileCatalog`, its embedded resource/parser, accepted Track A/B or Prepared Scan initiation/Pass 1 behavior, generic selection for a nonmatching pair, the existing 20-per-Repository or 150-pair bounds, Pass 1 failure/store policy, caller request/profile/path/alias/ref/revision authority, general/single/N-Repository Scan behavior, Worker/Pass 2/Chat/LLM behavior, `CodeGraphContextProvider`, `RepositoryProfilingCoordinator`, `/scan-runs`, project/package files, Git index/history, retained databases, or the historical release-envelope machinery. Production source may contain no reviewed sample Repository name, path, endpoint, symbol, or line literal. No external analysis may execute inside the initiation transaction. Any need for those changes is `BLOCKED` and returns to plan; it is not implementation improvisation.

## Immutable accepted-evidence ledger

| Boundary | Immutable authority | Treatment |
| --- | --- | --- |
| Track A | `f292caec427f9844be3c0f65b4cb00a9292080a5`; `evidence/verification/track-a-01.md`; `evidence/checkpoints/track-a-preservation-01.md` | Regress only; do not edit or reimplement. |
| Track B | `f292caec427f9844be3c0f65b4cb00a9292080a5`; `evidence/verification/track-b-final-01.md`; `evidence/e2e/track-b-run-01.md`; `reviews/quality-review/track-b-quality-review-05.json` | Regress only; quote content-dark streaming evidence without recomputation. |
| Story 12.4 | `4798b97d9f23aef587829175df7ab7ebc3daf995` | Consume exact decisions, current Edges, GENERATED_FROM, and published Flow Groups read-only. |
| Completed Track C-MVP support | `evidence/verification/track-c-mvp-support-01.md`; `evidence/verification/track-c-mvp-tier1-01.md` | Preserve T003-T007 RED/GREEN and regression results; rerun only as surrounding regression. |
| Truthful preflight/blocker evidence | `evidence/track-c-mvp/run-01/release-report.md`; `evidence/track-c-mvp/run-02/release-report.md`; `evidence/checkpoints/track-c-mvp-blocked-01.md`; `evidence/checkpoints/track-c-mvp-blocked-02.md` | Preserve as blocked history. Run 02 proves the missing entrance; it is not release success or reusable product data. |
| Historical Track C proof subsystem | `contracts/story125-release-evidence.schema.json` and invalidated historical tasks | Retain only as deferred compatibility history; never current acceptance authority. |

---

## Phase 1: Preserved completed definition and evidence entry

**Purpose**: Preserve, without relabeling, the completed pre-correction entry and accepted dependency evidence.

- [x] T001 Classification: GATE — Verify branch `demo0831/story-12-5-trackC`, the then-governing definition/review/approval bindings, clean/owned Git state, accepted Track A/B checkpoint ancestry, and Story 12.4 ancestry before the completed support lane; retain the read-only result in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/preflight/track-c-mvp-entry-01.md` (FR-048-FR-052, SC-013-SC-014).
- [x] T002 Classification: EXISTING-EVIDENCE — Bind the immutable Track A/B and Story 12.4 evidence ledger, record invalidated proof-subsystem work, and preserve the no-regression/no-transferred-approval result in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/preflight/track-c-mvp-entry-01.md` (FR-001-FR-040, FR-048-FR-061, SC-001-SC-008, SC-013-SC-017).

---

## Phase 2: Preserved User Story 5 support slice (Priority: P1)

**Goal**: Preserve the completed Markdown-report compatibility and preconnection database guard support.

**Independent Test**: The current Markdown report contract is representable without the historical envelope, and invalid/protected database names invoke a supplied connection factory zero times.

- [x] T003 [P] [US5] Classification: COMPATIBILITY — Preserve `tests/Vulgata.Tests/ReleaseGate/Story125/Story125TrackCMvpReportContractTests.cs` as the completed current-Markdown-boundary characterization; do not convert it into attestation, ten-check, counter, closure, duplicate-orchestration, outage, screenshot-automation, or release-summary work (FR-037-FR-040, FR-046, FR-061-FR-062, SC-008, SC-013, SC-018).
- [x] T004 [P] [US5] Classification: RED — Preserve the observed preconnection RED in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125TrackCMvpPreflightTests.cs` for the then-missing `Story125ReleaseHarness.CreateOwnedDatabaseConnection<T>` seam (FR-041, FR-047, FR-051, SC-014).
- [x] T005 [US5] Classification: GREEN — Preserve the minimal completed preconnection GREEN in `tests/Vulgata.Tests/ReleaseGate/Story125/Story125ReleaseHarness.cs`; it invokes the factory exactly once only after `IsOwnedDatabaseName` accepts the target and adds no provisioner, product host, query, cleanup, or release-envelope behavior (FR-041, FR-047, FR-051, SC-014).
- [x] T006 [US5] Classification: VERIFY — Preserve the completed focused results and exact owned diff in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-c-mvp-support-01.md` (FR-037-FR-041, FR-047-FR-051, FR-061-FR-062, SC-008, SC-013-SC-014, SC-018).

---

## Phase 3: Preserved User Story 5 Tier 1 regression (Priority: P1)

**Goal**: Preserve the accepted product/security regression that preceded the failed real run.

**Independent Test**: Accepted Track A/B suites and support tests passed with documented environment-only exclusions, Web built, and the then-owned diff had no forbidden production change.

- [x] T007 [US5] Classification: VERIFY — Preserve the commands, counts, exclusions, build result, and forbidden-path observations in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-c-mvp-tier1-01.md`; this is regression evidence, not proof of Prepared Scan initiation (FR-001-FR-040, FR-048-FR-061, SC-001-SC-008, SC-013-SC-017).

---

## Phase 4: Preserved User Story 5 preflight and blocker discovery (Priority: P1)

**Goal**: Preserve the truthful external readiness and guarded-database work that exposed the production entrance gap.

**Independent Test**: Run 02 used one guarded lowercase `vulgata_story125_*` database and real ready prerequisites, then stopped with zero Scan Runs/coordinations/Manifests when the ordinary-record production entrance was unavailable.

- [x] T008 [US5] Classification: GATE — Preserve the completed non-secret tool/provider/Repository/authentication/browser preflight in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-02/release-report.md`; do not refresh it into a current candidate claim (FR-039, FR-041-FR-042, FR-046-FR-047, FR-052, FR-062, SC-010, SC-013-SC-014, SC-018).
- [x] T009 [US5] Classification: GATE — Preserve the completed run-02 owned-database guard, creation, production bootstrap, exact two ordinary Repository records, and blocked checkpoint in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-02/release-report.md` and `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-c-mvp-blocked-02.md`; never reuse, mutate, repair, or delete that database (FR-039, FR-041, FR-047, FR-051-FR-052, FR-062, FR-077, SC-013-SC-014, SC-018).

**Checkpoint**: T001-T009 are immutable completed evidence. Their completion does not satisfy the current definition review, implementation approval, US6 implementation, or release run.

---

## Phase 5: Current definition acceptance and implementation-entry gate

**Purpose**: Bind the accepted Prepared Scan initiation definition before the first current RED or production write.

- [x] T010 Classification: GATE — Verify source commit `2fb097a0311965bfb0c4abef46dcf73de7e274eb`, accepted scope `50958154329acbac8070ecec04f5b6c3527a6cc047129feac024c0e093eb1eab`, plan-review attempt 02 `ACCEPT`, planned-change authorization 01, definition-review attempt 01 `REVISE` with DR-PSI-001 through DR-PSI-003 closed by this task set, a fresh independent definition-review attempt 02 `ACCEPT` at `specs/014-grounded-llm-wiki-chat-release-evidence/reviews/definition-review/definition-review-prepared-scan-initiation-02.json`, and explicit implementation approval at `specs/014-grounded-llm-wiki-chat-release-evidence/decisions/implementation-approval-prepared-scan-initiation-01.md`; record branch/HEAD/hashes/ancestry/clean ownership and the exclusive owners below in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/preflight/prepared-scan-initiation-entry-01.md`, stopping before T011 on any mismatch (FR-048-FR-052, FR-063-FR-078, SC-013-SC-014, SC-019-SC-025).

---

## Phase 6: User Story 6 - Public no-preseed behavioral anchor (Priority: P1) 🎯 MVP entrance

**Goal**: Observe the missing production behavior at the existing authenticated endpoint before any production implementation.

**Independent Test**: A `WebApplicationFactory<Program>` host using disposable PostgreSQL begins with only an authorized user, one ordinary System, exactly two ordinary Repositories, exact safe-ref/Profiled-Revision bindings, and deterministic ready internal runtime identities; the existing endpoint fails to create/publish from this state before the GREEN work.

- [x] T011 [US6] Classification: RED — Add `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationApiTests.cs` and test-only `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationPostgreSqlFixture.cs`; require every test database to be a newly created lowercase `vulgata_story125_*` disposable, pre-seed no Scan Run, Pre-Scan Result, Scan Execution, Two-Repository coordination, Prepared Scan Manifest, File Code Unit, or Document, call only `POST /api/systems/{systemId}/prepared-scans`, and assert the first authorized request should return 201 with one complete five-row tuple and two Manifests. Run only this named test, confirm it fails against the current direct-coordinator entrance for the expected missing-initiation behavior, and record the unmodified RED in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/prepared-scan-initiation-public-red-01.md` before any `src/dotnet/` edit (US6 scenarios 1-2; FR-063-FR-068, FR-076, SC-019, SC-025).

**Checkpoint**: T011 is the non-substitutable public RED. Keep its expectation unchanged until T028 turns the same test GREEN.

---

## Phase 7: User Story 6 - Core identity and closed initiation contract (Priority: P1)

**Goal**: Define a stable, infrastructure-free command/result/tuple port and deterministic replay identities.

**Independent Test**: Literal golden vectors pin both execution GUIDs and the coordination GUID, Repository reversal is stable, and changing each replay-bound dimension changes the expected identity without using the production builder to compute expected constants.

- [x] T012 [US6] Classification: RED — Add `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationIdentityTests.cs` for the exact `CanonicalEnvelope` domain `vulgata/story-12.5/prepared-scan-initiation-execution/1.0`, tags 1-10, exact kind `PreparedScanInitiationExecution`, lowercase SHA-256, two literal execution GUIDs, literal coordination GUID, Repository-reversal stability, canonical pair ordering, and one-field mutations for user/effective mode, System, Repository, case-sensitive full ref, Profiled Revision, prompt, Document schema, runtime JSON, and runtime hash; observe the compile/behavior RED before Core production changes (US6 scenarios 3-4; FR-066, FR-069-FR-070, FR-073, FR-076, SC-020-SC-021).
- [x] T013 [US6] Classification: GREEN — After T012 RED, add the specific non-EF port/command/result/immutable tuple projection in `src/dotnet/Vulgata.Core/DomainServices/IPreparedScanInitiationStore.cs`, implement the framing owner in `src/dotnet/Vulgata.Core/DomainServices/PreparedScanInitiationIdentityBuilder.cs`, and add only the new domain whitelist entry in `src/dotnet/Vulgata.Core/DomainServices/CanonicalEnvelope.cs`; expose no `DbSet`, `IQueryable`, public request field, transaction, connection, runtime choice, or general repository abstraction (FR-063-FR-066, FR-069-FR-070, FR-073, SC-020-SC-021).
- [x] T014 [US6] Classification: VERIFY — Run `PreparedScanInitiationIdentityTests` plus existing `PreparedCanonicalEnvelopeTests`, `PreparedCanonicalEnvelopeGoldenTests`, Story 12.2 identity tests, and a Core dependency guard; record exact RED/GREEN results and file hashes in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/prepared-scan-initiation-core-01.md`, proving every existing canonical output is unchanged (FR-066, FR-069-FR-070, FR-073, FR-078, SC-020-SC-021).

---

## Phase 8: User Story 6 - Infrastructure PostgreSQL atomicity and replay (Priority: P1)

**Goal**: Create or replay the complete tuple atomically under the accepted schema-free System advisory lock.

**Independent Test**: Independent PostgreSQL scopes/connections prove one tuple for identical races, at most one winner for competing pair/revision races, full rollback on every pre-commit failure, and no transaction or lock surviving into external analysis.

- [x] T015 [US6] Classification: RED — Add `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationPostgreSqlTests.cs` using only the T011 `PreparedScanInitiationPostgreSqlFixture` and a fresh lowercase `vulgata_story125_*` database per test boundary. Before Infrastructure implementation, observe REDs for: absent create; exact replay; reversed request order; changed ref/revision/runtime/prompt/schema/effective authorization conflict; historical uncoordinated Runs ignored; legacy/partial/extra/ambiguous/cross-paired state rejected; artifact presence and current phase reported; identical independent-connection races converge; competing pair/revision races yield at most one winner; `55P03` lock timeout, cancellation, injected save failure, and an explicitly injected pre-commit transaction failure each expose zero rows; one `SaveChangesAsync`/commit; an independent observer sees all five rows only after commit; and an ambiguous post-commit acknowledgement failure maps to unavailable without claiming absence, after which an exact retry materializes or replays one canonical tuple with no duplicate (US6 scenarios 1, 3-4, 6-7, 9; FR-065-FR-067, FR-069-FR-073, FR-075-FR-076, SC-019-SC-023, SC-025).
- [x] T016 [US6] Classification: GREEN — After T015 RED, implement only `src/dotnet/Vulgata.Infrastructure/Data/PreparedScanInitiationStore.cs`: scoped `VulgataDbContext`, `ReadCommitted`, `SET LOCAL lock_timeout = '5s'`, and `pg_advisory_xact_lock` keyed by the signed big-endian first eight SHA-256 bytes of `vulgata/story-12.5/prepared-scan-initiation-system-lock/1\n{system-id-lowercase-D}`; under-lock ownership/state reads; two `ScanRun.Start` aggregates with exact compatibility value 1,800 plus `BindProfiledRevision`; two `ScanExecution.CreateNewPath` aggregates with `PreScanResultId == Guid.Empty` and exact prompt/schema/runtime bindings; one `TwoRepositoryScanCoordination.Create`; one shared initiation instant; exactly one save and commit; exact materialized replay; rollback/tracker clearing for failures proved before commit; unavailable for an ambiguous commit acknowledgement without a false zero-row assertion; and exact-retry create-or-replay recovery to one canonical tuple. Inject/call no Git, CodeGraph, JDT-LS, XML, source, Worker, Pass, Document, Edge, Uncertainty, Chat, LLM, or evidence port (FR-065-FR-067, FR-069-FR-073, FR-075, SC-019-SC-023, SC-025).
- [x] T017 [US6] Classification: VERIFY — Run `PreparedScanInitiationPostgreSqlTests` against PostgreSQL 17, record database names, independent-scope concurrency counts, pre-commit rollback/timeout/cancellation evidence, ambiguous-commit unavailable-plus-exact-retry evidence, complete-tuple visibility, and cleanup of only each test-owned disposable in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/prepared-scan-initiation-postgresql-01.md`; SQLite cannot satisfy this task and the retained run-02 database must not be contacted (FR-065-FR-067, FR-069-FR-073, FR-075-FR-076, SC-019-SC-023, SC-025).

---

## Phase 9: User Story 6 - Exact-tuple preparation and phase-aware handoff (Priority: P1)

**Goal**: Make the accepted Prepared Scan coordinator/builder consume the exact initiated tuple and recover complete handoffs without rediscovery or analysis replay.

**Independent Test**: Created or retryable `Preparing + None` enters preparation with exact IDs; prepared/advanced exact state recovers the existing handoff with zero analysis; partial/corrupt/incompatible state conflicts without rewind or repair.

- [x] T018 [US6] Classification: EXISTING-EVIDENCE — The retained tuple/handoff RED was observed in `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationCoordinatorTests.cs` before shared coordinator/builder changes, covering immutable tuple IDs, runtime/time bindings, no rediscovery, retry, complete-handoff replay, and partial/corrupt conflict; preserve its evidence at `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/prepared-scan-initiation-identity-authority-blocked-01.md` (US6 scenarios 2, 3, 6; FR-064, FR-066, FR-068-FR-073, FR-076, SC-019-SC-023).
- [x] T019 [US6] Classification: EXISTING-EVIDENCE — The exact-tuple/handoff implementation is retained in `src/dotnet/Vulgata.Web/Data/PreparedScanCoordinator.cs` and `src/dotnet/Vulgata.Web/Data/PreparedScanPairBuilder.cs`; it requires the immutable tuple, removes tuple-less production discovery, preserves handoff validation, initiation time/runtime proof bindings, and existing analysis/publication behavior (FR-064, FR-066, FR-068-FR-073, FR-075, SC-019-SC-023, SC-025).

---

## Phase 10: User Story 6 - Existing Prepared Scan seam compatibility (Priority: P1)

**Goal**: Update every accepted test/qualification caller of the replaced tuple-less builder seam without retaining a tuple-less production overload.

**Independent Test**: The four accepted suites preserve their existing behavioral assertions while their fixture/composition calls construct and pass the immutable initiation tuple and assert the new coordinator/builder graph.

- [x] T020 [US6] Classification: EXISTING-EVIDENCE — The retained compatibility RED was observed immediately after T019 in the four accepted suites and recorded in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/prepared-scan-initiation-compatibility-red-01.md`; no new RED may be manufactured (FR-068, FR-074, FR-076, SC-019, SC-024).
- [x] T021 [US6] Classification: EXISTING-EVIDENCE — The retained compatibility GREEN updated only fixture/composition calls and seam assertions in the four accepted suites, preserving acquisition, CodeGraph, JDT-LS, XML, selection, timeout, deterministic, failure, and qualification assertions (FR-068, FR-074, FR-076, SC-019, SC-024).
- [x] T022 [US6] Classification: EXISTING-EVIDENCE — Retained compatibility results and assertion inventory are recorded in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/prepared-scan-initiation-compatibility-01.md`; host-only exclusions remain environmental and no tuple-less production overload is permitted (FR-068, FR-074, FR-076, SC-019, SC-024).

---

## Phase 11: User Story 6 - Web runtime identity and fresh authorization (Priority: P1)

**Goal**: Resolve server-owned current identities, then perform a fresh live authorization/ownership check immediately before the first initiation write.

**Independent Test**: Runtime not-ready fails before the store; Administrator role, SystemOwner role, assignment, and Repository membership revocation between checks each return missing-equivalent with zero store calls/writes.

- [x] T023 [US6] Classification: EXISTING-EVIDENCE — The retained runtime-authority RED and configured resolver characterization are recorded in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/prepared-scan-initiation-identity-authority-blocked-01.md`; preserve shared health/integrity behavior, canonical `analysis-runtime/1`, and no-CGC/legacy resolution (US6 scenario 8; FR-063, FR-066, FR-074, FR-076, SC-024).
- [x] T024 [US6] Classification: EXISTING-EVIDENCE — The retained authorization/currentness suite is GREEN (14/14), with barrier revocation and missing-equivalent zero-write behavior recorded in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/prepared-scan-initiation-identity-authority-blocked-01.md`; preserve the distinct pre-publication check (US6 scenarios 4-5; FR-063-FR-064, FR-070, FR-076, SC-021-SC-022).
- [x] T025 [US6] Classification: RED — After a fresh definition review accepts this regenerated boundary and a newly scope-bound implementation approval is recorded, add/extend tests first in `tests/Vulgata.Tests/PreparedScanning/PreparedPass1ContractIdentitySourceTests.cs`, `PreparedScanInitiationIdentityTests.cs`, `PreparedScanInitiationCoordinatorTests.cs`, and `PreparedScanInitiationApiTests.cs`. Assert default composition binds the embedded v2 prompt to `pass1-code-logic-worker.v2/sha256/{lowercase-64-hex}` plus `code-logic/file-v2`; null, legacy-v1, malformed-hash, wrong-schema, missing, duplicate, invalid-UTF8 and schema-inconsistent authorities fail HTTP 503 before fresh authorization/store/external analysis; characterize unchanged `WorkerAgentDispatchIdentitySource` v1/`vulgata.knowledge-document/1.0`; retain the unchanged T011 no-preseed public RED; and add a layering RED proving `PreparedScanInitiationAuthorizationRechecker.cs` must not import EF Core or resolve `VulgataDbContext`, while existing specific Core repository/authorization seams are sufficient. Record honest RED in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/prepared-pass1-identity-authority-red-01.md` before new Core/Agents/Web production edits (US6 scenarios 1-2, 8; FR-063-FR-066, FR-073-FR-076, SC-019-SC-025).
- [x] T026 [US6] Classification: GREEN — After T025 RED, add only `src/dotnet/Vulgata.Core/DomainServices/PreparedPass1ContractIdentity.cs` and `src/dotnet/Vulgata.Core/DomainServices/IPreparedPass1ContractIdentitySource.cs`; expose exactly prompt identity and Document schema identity, no prompt text/provider/dispatcher/tool/transaction/persistence/public-request fields, and preserve existing Core dependency and canonical-envelope outputs (FR-066, FR-073, FR-076, SC-020-SC-021).
- [x] T027 [US6] Classification: GREEN — After T026, add only `src/dotnet/Vulgata.Agents/WorkerExecution/EmbeddedPreparedPass1ContractIdentitySource.cs`; resolve exactly embedded logical resource `Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md`, decode strict UTF-8, reject missing/duplicate/empty/schema-inconsistent content, hash exact decoded text re-encoded UTF-8, and return closed v2/hash plus `code-logic/file-v2`. Do not edit `WorkerAgentDispatchIdentitySource.cs`, either prompt, dispatcher, provider, tools, retry or scheduling behavior (FR-066, FR-073, FR-075, SC-020-SC-024).
- [x] T028 [US6] Classification: GREEN — After T025-T027 RED/GREEN, one exclusive Web/composition owner edits `src/dotnet/Vulgata.Web/Data/PreparedScanInitiationAuthorizationRechecker.cs` plus `PreparedScanInitiationCoordinator.cs`, `src/dotnet/Vulgata.Web/Api/Scans/PreparedScanEndpoints.cs`, and `src/dotnet/Vulgata.Web/Program.cs`. Remove the rechecker’s `Microsoft.EntityFrameworkCore` and `Vulgata.Infrastructure.Data.VulgataDbContext` access; retain Web-only Identity/UserManager orchestration for role checks, and use existing materialized Core seams `ISystemRepository` and `IRepositoryRepository` backed by Infrastructure for SystemOwnerAssignment and System/Repository ownership reads. Replace only the coordinator dependency on `IScanRecoveryDispatchIdentitySource` with `IPreparedPass1ContractIdentitySource`; register exactly one scoped Core-to-Agents mapping, preserve runtime resolver/fresh recheck, map existing 404/409/503 outcomes, and prove unchanged T011 reaches exact tuple/handoff. No Web hardcoded prompt/schema, legacy fallback, DTO, Agent tool, CGC/legacy profiling, `/scan-runs`, or later-stage call; no concurrent task may edit these shared files (FR-063-FR-076, SC-019-SC-025).

---

## Phase 13: Surrounding regression, forbidden-path guard, and implementation acceptance

**Purpose**: Prove the bounded delta without reopening accepted Track A/B or entering the real release run prematurely.

- [x] T029 Classification: VERIFY — After T028, run focused identity, coordinator, API, runtime-resolver, and PostgreSQL-only initiation filters plus unchanged T011; add an automated/read-only path guard that `PreparedScanInitiationAuthorizationRechecker.cs` contains no `Microsoft.EntityFrameworkCore`/`VulgataDbContext` reference and that its dependencies are the materialized Core `ISystemRepository`/`IRepositoryRepository` seams, with Infrastructure-backed DI. Record literal v2 hash/schema, hostile ordering, legacy v1 regression, default DI graph, no-CGC/CodeGraph 1.5/JDT-LS composition, all-nine scenarios, tuple visibility, no-later-stage counters, phase recovery, database isolation, and RED/GREEN chronology in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/prepared-scan-initiation-identity-authority-01.md` (depends T028; FR-063-FR-076, SC-019-SC-025).
- [x] T030 Classification: VERIFY — After T029, run focused Track A/B regression for corpus/preconnection support, Prepared Chat retrieval/currentness/packet/citation/conversation, strict streaming, accepted publication, UI, production Scan status/resume, deterministic behavior, existing Prepared Scan, and Story 12.2 identities; run Web build and `git diff --check`, and repeat forbidden-path/dependency guards proving no Web EF/DbContext access, no migration/model changes, no legacy recovery-source mutation, and no CGC/legacy/general-Scan/later-stage changes. Record results in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/prepared-scan-initiation-regression-01.md` (depends T029; FR-048-FR-052, FR-073-FR-078, SC-013-SC-018, SC-024-SC-025).
- [x] T031 Classification: VERIFY — After T030, run independent convergence against accepted spec/plan/tasks and exact implementation/test evidence, then prepare verdict-neutral quality-review input with candidate/base/diff, corrected hashes, owners, RED/GREEN evidence, scenario matrix, authorization/concurrency/transaction proof, default CodeGraph 1.5/no-CGC result, forbidden-path result, and external exclusions in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/convergence/prepared-scan-initiation-01.md` and `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/review/prepared-scan-initiation-quality-input-01.md`; an EF/model/Story 12.4/CGC/legacy/general-Scan/later-stage need is a blocked contradiction, not an appended task (depends T030; FR-063-FR-078, SC-019-SC-025).
- [x] T032 Classification: GATE — Dispatch fresh independent quality review of the regenerated definition and implementation evidence; only the reviewer may write `specs/014-grounded-llm-wiki-chat-release-evidence/reviews/quality-review/prepared-scan-initiation-quality-review-01.json`. `REVISE` returns only to the owning approved TDD slice, `BLOCKED` preserves a safe checkpoint, and only `ACCEPT` advances (depends T031; FR-063-FR-078, SC-019-SC-025).
- [x] T033 Classification: GATE — After T032 `ACCEPT`, rerun focused initiation filters, Web build, diff check, corrected artifact hashes, scope and forbidden guards, then bind the implementation-accepted candidate in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/prepared-scan-initiation-implementation-accepted-01.md`; no run-03 external work may start before this checkpoint (depends T032; FR-063-FR-078, SC-019-SC-025).

---

## Phase 14: User Story 5 - Fresh guarded run-03 preflight and database (Priority: P1)

**Goal**: Start a new candidate/database release lane only after the corrected entrance is accepted.

**Independent Test**: Before product access, run 03 binds one accepted candidate, two exact Repository Profiled Revisions, ready real prerequisites, and a new owned lowercase database distinct from every retained database.

- [x] T034 [US5] Classification: GATE — Create `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-03/release-report.md` with truthful `running` or sanitized `blocked` disposition; pin the T033 candidate, exact two real Java/XML Repository aliases/Profiled Revisions, clean owned state, PostgreSQL 17/Docker, CodeGraph 1.5 hashes, Java 21, JDT-LS identity/hash, non-secret configured streaming-provider identity/credential presence/network readiness, authenticated authorization readiness, and browser/manual-screenshot capability. Preserve run 01/run 02 and their databases unchanged; never print secrets, connection strings, credentials, tokens, source, or absolute host paths (FR-039, FR-041-FR-042, FR-046-FR-047, FR-052, FR-062, FR-077, SC-010, SC-013-SC-014, SC-018).
- [x] T035 [US5] Classification: GATE — Generate a fresh unique lowercase `vulgata_story125_*` name through the accepted preconnection guard, prove it differs from every retained database and invokes zero product connection/host/query calls before acceptance, then create only that database with the approved administrator mechanism and production-bootstrap one Administrator, one authorized ordinary System, provider assignment, and exactly two ordinary Repository records at the pinned refs/revisions. Pre-seed no Scan Run, Pre-Scan Result, Scan Execution, coordination, Manifest, File Code Unit, or Document; record sanitized purpose/owner/version/commands/exits in the run-03 report (FR-041, FR-047, FR-051-FR-052, FR-062, FR-076-FR-077, SC-013-SC-014, SC-018-SC-019).

**External blocker protocol**: A missing/unsafe prerequisite sets run 03 to `blocked`, records only completed truthful observations in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-c-mvp-blocked-03.md`, and leaves dependent tasks unchecked. Do not reuse run 02, fake a provider, fabricate evidence, delete any retained database, or repeat an unchanged expensive run.

---

**Historical checkpoint**: T034-T035 and all later observations recorded in `evidence/track-c-mvp/run-03/release-report.md` are immutable diagnostic evidence. The run-03 database, report, commands, Pass 1 Documents, audits, and failed exact-resolution outcome may be inspected read-only but must never be repaired, migrated, restarted, deleted, or reused by T036 onward.

---

## Phase 15: Current exact-profile definition and implementation-entry gate

**Purpose**: Bind the accepted exact-profile plan, independent definition verdict, and standing implementation authority before the first current RED.

- [ ] T036 Classification: GATE — Verify branch `demo0831/story-12-5-trackC`, accepted source checkpoint `7fc5bd1b8b787d5ff205378abb9e6a464bf00eb5`, spec/plan/review/planned-change hashes pinned above, accepted `plan-review-exact-profile-seeds-01.json`, accepted `planned-change-authorization-exact-profile-seed-selection-03.md`, a fresh independent `ACCEPT` at `specs/014-grounded-llm-wiki-chat-release-evidence/reviews/definition-review/definition-review-exact-profile-seeds-01.json`, and a root-owned implementation-authorization binding at `specs/014-grounded-llm-wiki-chat-release-evidence/decisions/implementation-approval-exact-profile-seed-selection-03.md`; record branch/HEAD/hashes/ancestry/owned diff, the immutable run-03 boundary, and the exclusive owners below in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/preflight/exact-profile-seed-entry-01.md`, stopping before T037 on any mismatch (FR-048-FR-052, FR-077-FR-086, SC-013-SC-014, SC-026-SC-029).

---

## Phase 16: User Story 5 - Exact-profile authority and planner RED (Priority: P1)

**Goal**: Observe the missing closed server authority and 1,100 planner tier before production changes.

**Independent Test**: A complete exact persisted-name/full-ref/Profiled-Revision match derives the canonical required Repository/path/role union and every required segment survives a saturated top-20 seed set; Repository order reversal is stable, while any complete nonmatch produces the byte-identical generic plan, audits, and fingerprint.

- [ ] T037 [US5] Classification: RED — Add `tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedAuthorityTests.cs` and extend `tests/Vulgata.Tests/PreparedScanning/PreparedSelectionPlannerTests.cs` first. Assert one-to-one exact activation from two distinct server-observed Repository IDs, ordinal persisted names, safe full refs, and lowercase Profiled Revisions; canonical consumer/provider-declaration/provider-implementation/unsupported-owner Repository-path-role derivation; request-order independence; catalog-unavailable closure; score 1,100 as `DirectBusinessEvidence`; all required split segments winning the unchanged top-20-per-Repository and 150-pair plan; and a nonmatching name/ref/revision/pair changing zero candidates, scores, order, audits, selection, or fingerprint. Observe compile/behavior RED for the absent Core port/evidence value/Infrastructure authority and record it in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/exact-profile-seed-authority-red-01.md` before any current `src/dotnet/` edit (US5 scenario 10; FR-079-FR-083, FR-085-FR-086, SC-026-SC-028).

---

## Phase 17: User Story 5 - Builder/public/composition RED (Priority: P1)

**Goal**: Pin matched fail-closed behavior, nonmatch neutrality, and closed production composition at the authenticated Prepared Scan seam.

**Independent Test**: The same no-preseed authenticated endpoint used by the accepted entrance promotes every matched required segment, but publishes neither Manifest when any matched requirement is absent, filtered, unsafe, non-contained, hash-invalid, duplicate/multiply mapped, split-invalid, ambiguous, or over the unchanged bound.

- [ ] T038 [US5] Classification: RED — Extend `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationApiTests.cs`, `PreparedScanReleaseQualificationTests.cs`, `PreparedScanProductionLiteralQualificationTests.cs`, and `PreparedScanBoundaryTests.cs` before production edits. Require matched promotion before CodeGraph expansion/`PreparedScanPlanner.Plan`; all segments of every required path at score 1,100; deterministic Manifest/audit/package fingerprints under candidate and Repository reversal; byte-identical nonmatch output; bounded `prepared-scan-reviewed-profile-seed-incomplete` rejection before either Manifest/store commit for missing, filtered, unsafe, non-contained, empty/hash-invalid, duplicate, ambiguous, exact-split-invalid, more-than-20-required-segment, and final-selection omission cases; bounded unavailable mapping for catalog integrity failure; exactly one default server-owned authority; no public/request profile/path/role/score/selector field; and no reviewed sample Repository/path/endpoint/symbol/line literal in current production source. Preserve accepted initiation/replay/authorization and no-CGC assertions, observe the current matched-path RED, and record it in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/exact-profile-seed-builder-red-01.md` (FR-079-FR-083, FR-085-FR-086, SC-026-SC-028).

---

## Phase 18: User Story 5 - No-preseed PostgreSQL exact-outcome RED (Priority: P1)

**Goal**: Prove the missing behavior at the production-composition vertical rather than substituting pure planner or pre-seeded Story 12.4 fixtures.

**Independent Test**: A fresh disposable PostgreSQL database begins only with an authenticated authorized user, one ordinary System, exactly two ordinary Repositories at the reviewed refs/revisions, provider/runtime configuration, and accessible source; the public endpoint must reach Prepared Manifests, terminal accepted Pass 1, the unchanged accepted exact decision/current REFERENCES Edge, and one honest Uncertainty without pre-seeded lifecycle/content rows or direct repair.

- [ ] T039 [US5] Classification: RED — Add `tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedPostgreSqlTests.cs` using only the accepted `PreparedScanInitiationPostgreSqlFixture` and a newly created lowercase `vulgata_story125_*` database per test boundary. Pre-seed no Scan Run, Pre-Scan Result, Scan Execution, coordination, Manifest, File Code Unit, Document, DocumentEdge, exact decision, Edge, or Uncertainty; enter only through authenticated `POST /api/systems/{systemId}/prepared-scans`; use production composition with bounded deterministic test doubles only for external CodeGraph/JDT-LS/Worker provider execution; assert both Repositories contribute every required selected score-1,100 segment, all required jobs succeed, exact resolution creates the accepted current exact REFERENCES Edge and honest Uncertainty, and a deliberately failed required job reaches existing `FailedPass1` with zero exact-resolution/Pass 2 start. Observe the matched release-pair RED without changing `PreparedPass1Store`, Story 12.4, or database rows, and record it in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/exact-profile-seed-postgresql-red-01.md` (FR-076-FR-086, SC-019, SC-024-SC-029).

---

## Phase 19: User Story 5 - Coordinated minimal production GREEN (Priority: P1)

**Goal**: Turn T037-T039 green with one closed contract/value, one read-only adapter, and the smallest builder/DI composition.

**Independent Test**: The exact same RED expectations pass without modifying persistence, profile/catalog/parser/resource, Story 12.4, Pass 1 policy, generic nonmatching behavior, bounds, public inputs, or accepted earlier paths.

- [ ] T040 [US5] Classification: GREEN — After T037-T039 REDs, one exclusive coordinated production owner makes the minimal change across only `src/dotnet/Vulgata.Core/DomainServices/IReviewedExactProfileSeedAuthority.cs`, `src/dotnet/Vulgata.Core/DomainServices/PreparedScanPlanner.cs`, new `src/dotnet/Vulgata.Infrastructure/CrossRepository/ReviewedExactProfileSeedAuthority.cs`, `src/dotnet/Vulgata.Web/Data/PreparedScanPairBuilder.cs`, and shared `src/dotnet/Vulgata.Web/Program.cs`. Add immutable matched/nonmatching/unavailable Core values and closed roles; add `PreparedBusinessEvidence.ReviewedExactResolutionProfile`, score 1,100, direct-evidence mapping, and candidate validation; derive required paths only through the unchanged injected `ExactResolutionProfileCatalog`; activate only on the complete exact server-observed identity tuple; promote every exact bounded segment; validate all matched required sources before CodeGraph expansion/plan/commit; register exactly one server-owned authority; and map the two bounded failure outcomes. Do not edit EF/DbContext/configurations/migrations/snapshot, Story 12.4, `ExactResolutionProfile`/catalog/resource/parser, `PreparedPass1Store`/policy, accepted initiation/Pass 1 corrections, CGC/legacy profiling, caller DTOs, generic nonmatching values, bounds, or production sample literals (FR-079-FR-086, SC-026-SC-029).

---

## Phase 20: Focused verification and required-seed gate

**Purpose**: Verify deterministic selection, matched failures, neutral generic behavior, existing Pass 1 authority, and the no-preseed vertical independently.

- [ ] T041 [P] Classification: VERIFY — Run T037/T038 focused authority/planner/builder/public suites plus existing `PreparedSelectionPlannerTests`, `PreparedFileCodeUnitAndSelectionTests`, `PreparedScanAtomicReplayTests`, and `PreparedScanReleaseQualificationTests`; record exact matched/reversal/nonmatch vectors, prior-vs-current neutral fingerprints, every matched failure case before commit, 1,100 direct-evidence audit rows, unchanged 20/150 constants, default DI, no caller authority, no production sample literals, and RED/GREEN chronology in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/exact-profile-seed-selection-01.md` (depends T040; FR-079-FR-083, FR-085-FR-086, SC-026-SC-028).
- [ ] T042 [P] Classification: COMPATIBILITY — Extend only `tests/Vulgata.Tests/PreparedPass1/Pass1PairPostgreSqlAtomicTests.cs` with score-1,100 Manifest entries to prove the existing direct-score `>=400`, top-20-per-Manifest authority counts every reviewed segment as required, one required failure produces `FailedPass1`, and zero exact-resolution/Pass 2 activation occurs; run it with existing high-value/ordinary-failure tests and record unchanged store/policy hashes plus results in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/exact-profile-required-pass1-gate-01.md`. Do not edit `PreparedPass1Store.cs`, its retry/reconciliation/failure policy, hosted services, or Worker behavior (depends T040; FR-084, FR-086, SC-026, SC-029).
- [ ] T043 Classification: VERIFY — Run `ReviewedExactProfileSeedPostgreSqlTests` against a fresh lowercase disposable PostgreSQL 17 database, proving no lifecycle/content pre-seed, production endpoint/DI composition, two approved Manifests, required score-1,100 segments from both Repositories, all required Pass 1 success, unchanged exact decision/current REFERENCES Edge/honest Uncertainty, deliberate required-failure closure, no direct database repair, and cleanup of only the test-owned disposable; record database identity, exact commands/results, and source/row preconditions in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/exact-profile-seed-postgresql-01.md`. SQLite, direct planner calls, run-03, pre-seeded Documents/Edges, and Story 12.4 provisioner fixtures cannot substitute (depends T041-T042; FR-076-FR-086, SC-019, SC-024-SC-029).

---

## Phase 21: Regression, convergence, quality, conditional E2E, and technical acceptance

**Purpose**: Accept the same bounded candidate before any fresh release database or remote provider run.

- [ ] T044 Classification: VERIFY — After T043, run surrounding accepted Prepared Scan initiation/replay/authorization/identity/PostgreSQL/CodeGraph 1.5/JDT-LS/XML suites, Prepared Pass 1 retry/reconciliation/production-capacity suites, Story 12.4 exact-resolution/Pass 2 suites, Track A/B Scan/Chat/security/streaming/corpus suites, Web build, `git diff --check`, and automated forbidden-path/source-literal/default-DI/nonmatch-golden guards. Record exact counts, environmental exclusions, owned diff, and hashes in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/exact-profile-seed-regression-01.md`; any generic behavior, bound, Pass 1 policy, Story 12.4, EF/model, CGC/legacy, public-input, or production-literal change is a return-to-plan contradiction (FR-001-FR-086, SC-001-SC-029).
- [ ] T045 Classification: VERIFY — Run independent convergence against the accepted corrected spec/plan/tasks and T037-T044 evidence; append only genuinely unbuilt in-scope work, otherwise produce `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/convergence/exact-profile-seed-selection-01.md` and verdict-neutral input `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/review/exact-profile-seed-quality-input-01.md` with candidate/base/diff, RED/GREEN chronology, FR-079-FR-086/SC-026-SC-029 matrix, neutral-golden hashes, PostgreSQL exact-outcome proof, preserved boundaries, and external exclusions (depends T044).
- [ ] T046 Classification: GATE — Dispatch fresh independent quality review of the converged exact-profile definition and implementation; only the reviewer may write `specs/014-grounded-llm-wiki-chat-release-evidence/reviews/quality-review/exact-profile-seed-quality-review-01.json`. `REVISE` returns only to the owning approved TDD slice, `BLOCKED` preserves an honest checkpoint, and only `ACCEPT` advances (depends T045; FR-079-FR-086, SC-026-SC-029).
- [ ] T047 Classification: VERIFY — After T046 `ACCEPT`, make and execute the conditional economical E2E decision against the same candidate: use the authenticated endpoint plus disposable PostgreSQL production composition to verify exact matched seed selection, no-preseed Prepared Scan, required Pass 1 gate, accepted exact Edge/Uncertainty, and no-CGC/no-later-stage leakage; real CodeGraph CLI 2/2 may supplement but never replace the product vertical. Write the decision/execution evidence to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/e2e/exact-profile-seed-selection-01.md`; do not start the release run or call the remote LLM here (depends T046; FR-076-FR-086, SC-019, SC-024-SC-029).
- [ ] T048 Classification: GATE — After T047 accepts, rerun focused exact-profile/PostgreSQL/Pass 1 filters, Web build, diff check, authority hashes, forbidden-path guards, candidate/Story 12.4 ancestry, and immutable run-03 checks; bind the technically accepted correction in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/exact-profile-seed-implementation-accepted-01.md`. No fresh release database, host process, remote provider call, or screenshot begins before this checkpoint (depends T047; FR-048-FR-052, FR-077-FR-086, SC-013-SC-014, SC-026-SC-029).

---

## Phase 22: User Story 5 - Fresh guarded run-04 and one combined real release run (Priority: P1)

**Goal**: Execute one fresh candidate/database lane after exact-profile technical acceptance, using real CodeGraph 1.5, JDT-LS, safe XML, remote streaming LLM, application restart, and manually redacted screenshots.

**Independent Test**: A never-before-used guarded lowercase database starts from ordinary records and the corrected endpoint, retains every reviewed required seed, reaches the exact Edge and honest Uncertainty, serves grounded Business/IT Chat through the configured real streaming provider, and preserves wiki/Chat access after one application-process restart.

- [ ] T049 [US5] Classification: GATE — Create new `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-04/release-report.md` with truthful `running` or sanitized `blocked` disposition; pin the T048 candidate, exact two real Java/XML Repository stable identities/full refs/Profiled Revisions, clean owned state, PostgreSQL 17/Docker, CodeGraph 1.5 hashes, Java 21, JDT-LS identity/hash, non-secret configured remote streaming-provider identity/credential presence/network readiness, authenticated authorization readiness, and browser/manual-screenshot capability. Prove run-04 is not run-03 and preserve every run-01/run-02/run-03 report/database unchanged; never print secrets, connection strings, credentials, tokens, source, reviewed production sample literals, or absolute host paths (FR-039, FR-041-FR-047, FR-052, FR-062, FR-077, FR-085, SC-010, SC-013-SC-014, SC-018, SC-026-SC-029).
- [ ] T050 [US5] Classification: GATE — Generate a fresh unique lowercase `vulgata_story125_*` name through the accepted preconnection guard, prove it differs from every retained and test-owned database and invokes zero product connection/host/query calls before acceptance, then create only that database with the approved administrator mechanism and production-bootstrap one Administrator, one authorized ordinary System, provider assignment, and exactly two ordinary Repository records at the reviewed refs/revisions. Pre-seed no Scan Run, Pre-Scan Result, Scan Execution, coordination, Manifest, File Code Unit, Document, Edge, exact decision, or Uncertainty; record sanitized purpose/owner/version/commands/exits in the run-04 report and never contact, clone, migrate, repair, delete, or reuse run-03 (depends T049; FR-041, FR-047, FR-051-FR-052, FR-062, FR-076-FR-077, SC-013-SC-014, SC-018-SC-019, SC-029).
- [ ] T051 [US5] Classification: RUN — Start host-run Vulgata against only the T050 database, authenticate, and submit the corrected existing `POST /api/systems/{systemId}/prepared-scans` request with exactly the two ordinary Repositories and reviewed refs/Profiled Revisions. Query one complete initiation tuple and two approved real Manifests before Pass 1; prove every reviewed required Repository/path/segment is selected at direct score 1,100, high-value, and inside unchanged 20/150 bounds; repeat after publication and prove identical replay with no duplicate or analysis rerun. Only after these predicates pass, execute accepted Pass 1, exact Cross-Repository Resolution, and Pass 2 to terminal state; any required-seed failure must stop at existing `FailedPass1`. Record real CodeGraph 1.5/JDT-LS/safe XML plus sanitized commands/exits/durations in the run-04 report; never use `/scan-runs`, CGC, legacy profiling, direct database repair, or run-03 state (depends T050; FR-039, FR-042-FR-043, FR-049-FR-052, FR-062, FR-068, FR-073-FR-086, SC-010-SC-011, SC-018-SC-019, SC-023-SC-029).
- [ ] T052 [US5] Classification: QUERY — Against only the run-04 database, record bounded read-only rows for the complete initiation tuple; two Manifests/revisions/tool bindings; 50-150 File Code Units; each reviewed required Repository/path/segment selected with score 1,100; terminal Pass 1/Pass 2 jobs; required/high-value selected/succeeded/failed counts with required failed zero; ordinary failure budget; source-anchored current Code Logic and Business Logic Documents; one accepted decision-bound exact Cross-Repository REFERENCES Edge; one honest Uncertainty; at least three Business Logic Documents; one published Cross-Repository Flow Group; and pre-restart Document/Edge/Flow Group identities/counts. Use actual mappings, never insert/repair/recompute missing facts, and treat a missing predicate as failed/blocked evidence (depends T051; FR-039, FR-043, FR-049-FR-051, FR-077, FR-081-FR-085, SC-010-SC-014, SC-026, SC-029).
- [ ] T053 [US5] Classification: MANUAL — Before the first remote provider call, pin exactly `q01`-`q12` from `tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json`; execute every case through the authenticated Business/IT page and the real configured remote `OpenAiCompatibleChatProviderStreamingTransport` with fresh `ChatConversationState` per row, topical lineage only for q01-q03, and q12 limited to `prompt-boundary-only`. Record all twelve rows, accepted/cited material-claim counts, usefulness, unauthorized disclosure, mandatory streaming, render duration, fingerprint when exposed, Tier 1 references, provider availability, and limitations in the run-04 report; sanitize egress evidence and do not replace failures or count deterministic rescue/fake/non-streaming output (depends T052; FR-037-FR-039, FR-044, FR-053-FR-060, FR-062, SC-008-SC-009, SC-013, SC-015-SC-018).
- [ ] T054 [US5] Classification: RESTART — Record pre-restart process/database/candidate/revision identity, stop only Vulgata, retain PostgreSQL and the run-04 database, restart with identical configuration, re-authenticate, open persisted wiki content, and submit one new grounded Chat request through the real configured streaming provider; exclude transient Conversation State survival from the claim and record closed results in the run-04 report. Never start or contact run-03 (depends T053; FR-039, FR-045, FR-053, FR-077, SC-012-SC-015).
- [ ] T055 [US5] Classification: QUERY — Rerun read-only DocumentVersion, DocumentEdge, FlowGroup, exact decision/Edge, Uncertainty, required-seed audit and publication identity/count observations after T054, compare with T052, and record persisted access, data-loss disposition, stable Profiled Revision bindings, and zero restart-caused duplicate publication; never correct a product/result failure in the database (depends T054; FR-039, FR-043, FR-045, FR-049-FR-051, FR-077, FR-083-FR-084, SC-011-SC-014, SC-026, SC-029).
- [ ] T056 [US5] Classification: MANUAL — Capture at least one real repository-relative screenshot at `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-04/screenshots/scan.png`, `business-chat.png`, `it-chat-source-anchors.png`, `cross-repository-result.png`, and `post-restart-wiki-chat.png`; manually redact/reject secrets, credentials, connection strings, absolute host paths, fake Backup B state, any run-03 state, or unrelated candidate output, and record category/time/real-state/redaction results without screenshot automation or hashes (depends T055; FR-039, FR-046, FR-061-FR-062, FR-077, SC-013, SC-018).
- [ ] T057 [US5] Classification: REPORT — Complete `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-04/release-report.md` against `contracts/release-evidence-contract.md` with exactly one truthful disposition; candidate/revisions/tool/provider/database identities; ordered sanitized commands/exits; exact-profile required-seed rows; all required query observations; twelve fresh-state remote-provider rows when provider execution occurred; q12's bounded oracle; restart; five screenshot links for a pass; failures and limitations. `passed` requires every Scan/seed/exact-output/Chat/restart/screenshot threshold, no forbidden change, and explicit proof that no run-03 database or artifact supplied the result (depends T056; FR-039-FR-047, FR-053, FR-060-FR-062, FR-077-FR-086, SC-009-SC-018, SC-026-SC-029).

---

## Phase 23: Independent release E2E, final verification, and completion

**Purpose**: Apply remaining schema-v3 gates to the same accepted implementation candidate and run-04 report.

- [ ] T058 Classification: VERIFY — After T057, run the required independent release E2E gate against the run-04 combined host evidence and only economical same-candidate browser/API checks needed for authenticated real Prepared Scan, reviewed required seeds, real Scan state, Business/IT accepted-only remote-streaming Chat render, exact Cross-Repository result, and post-restart wiki/Chat access; do not create a second orchestration run, contact run-03, or substitute mocks, and write the decision/execution evidence to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/e2e/track-c-mvp-run-04.md` (FR-039-FR-046, FR-053-FR-062, FR-063-FR-086, SC-009-SC-029).
- [ ] T059 Classification: VERIFY — After T048 and T058 accept, run final focused/full-applicable tests, exact-profile/PostgreSQL no-preseed acceptance, required Pass 1 gate, Web build, `git diff --check`, exact hash inventory, candidate/Story 12.4 ancestry, immutable run-03 checks, run-04 database/revision/restart bindings, report checklist, five screenshot/redaction checks, no-secret/sample-literal scan, neutral golden fingerprints, default sole seed authority, CodeGraph 1.5/no-CGC composition, and every forbidden-path guard; write the pinned truthful verdict to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-c-mvp-final-04.md` (FR-039-FR-086, SC-009-SC-029).
- [ ] T060 Classification: GATE — Only after T059 verifies a passed run-04 report and every definition/TDD/convergence/quality/E2E/final-verification gate accepts the same candidate, bind completion in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-c-mvp-completion-04.md`; then only the root orchestrator may update Story 12.5 in `docs/bmad/implementation-artifacts/sprint-status.yaml` and workflow-owned state. Never mark done for blocked, failed, not-run, unreviewed, uncheckpointed, run-03 reuse/repair, generic drift, missing required seed, reused database, or partial evidence (FR-039, FR-048-FR-052, FR-062-FR-086, SC-013-SC-029).

---

## Dependencies and execution order

### Phase dependencies

1. T001-T035 are immutable completed evidence. They preserve accepted Track A/B, Prepared Scan initiation, Prepared Pass 1 corrections, and run-03 history; they authorize no current source write or release reuse.
2. Fresh definition acceptance plus the scope-bound implementation authorization precedes `T036`; `T036 -> T037`.
3. T037, T038, and T039 are ordered current REDs at pure authority/planner, authenticated builder/composition, and disposable-PostgreSQL exact-outcome seams. All three must fail for the missing matched-profile authority before T040.
4. `T037 -> T038 -> T039 -> T040`; T040 is the sole coordinated production GREEN and no other task may concurrently edit its five owned production paths.
5. After T040, T041 and T042 may run in parallel because they own disjoint test/evidence paths; `T041 + T042 -> T043` proves the production-composition vertical.
6. `T043 -> T044 -> T045 -> T046 -> T047 -> T048`; surrounding regression, convergence, independent quality, conditional economical E2E, and technical acceptance precede all release work.
7. `T048 -> T049 -> T050 -> T051 -> T052 -> T053 -> T054 -> T055 -> T056 -> T057` is one fresh run-04 candidate/database/report lane. Run-03 is never a dependency or input.
8. `T057 -> T058 -> T059 -> T060`; release E2E, final verification, and completion never substitute for earlier technical gates.
9. Any authority contradiction or external blocker follows the named blocked protocol and prevents all dependent tasks.

### Dependency graph

```text
historical accepted implementation/evidence T001-T033
historical immutable run-03 entry/evidence T034-T035
                         |
definition ACCEPT + authorization binding -> T036
                         |
                         v
T037 authority/planner RED -> T038 builder/public RED -> T039 PostgreSQL exact-outcome RED
                                                                    |
                                                                    v
                                                         T040 coordinated GREEN
                                                           /                 \
                                                        T041               T042
                                                           \                 /
                                                                    T043
                                                                     |
T044 -> T045 -> T046 quality -> T047 conditional E2E -> T048 technical acceptance
                                                                     |
                                                                     v
T049 -> T050 -> T051 -> T052 -> T053 remote LLM -> T054 -> T055 -> T056 -> T057
                                                                     |
                                                                     v
                                                       T058 -> T059 -> T060
```

### Preserved evidence boundary

```text
T001-T035 are checked history and are not regenerated or rerun by this task
definition. Fresh exact-profile definition acceptance plus its implementation
binding starts the executable T036 boundary. Run-03 remains read-only forever.
```

Only T041/T042 are parallel: they follow the same GREEN but own disjoint suites. All other current tasks are serial because they depend on an observed RED, share production composition, use one PostgreSQL/candidate/report lane, or are ordered acceptance gates.

---

## Exact ownership and path map

| Owner/boundary | Writable paths | Read-only/forbidden authority |
| --- | --- | --- |
| Public/Authority Test Owner (T011, T025) | `tests/Vulgata.Tests/PreparedScanning/PreparedPass1ContractIdentitySourceTests.cs`; `PreparedScanInitiationIdentityTests.cs`; `PreparedScanInitiationCoordinatorTests.cs`; `PreparedScanInitiationApiTests.cs`; `PreparedScanInitiationPostgreSqlFixture.cs` | Production until corrected RED is recorded; retained databases always read-only/no-contact. |
| Core Identity/Contract Owner (T012-T014) | `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationIdentityTests.cs`; `src/dotnet/Vulgata.Core/DomainServices/IPreparedScanInitiationStore.cs`; `PreparedScanInitiationIdentityBuilder.cs`; one domain whitelist entry in `CanonicalEnvelope.cs` | No EF/infrastructure dependency; existing envelope grammar/output read-only. |
| PostgreSQL Store Owner (T015-T017) | `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationPostgreSqlTests.cs`; `src/dotnet/Vulgata.Infrastructure/Data/PreparedScanInitiationStore.cs` | `VulgataDbContext.cs`, configurations, migrations, snapshot forbidden. |
| Web Application Test Owner (T018, T024) | `tests/Vulgata.Tests/PreparedScanning/PreparedScanInitiationCoordinatorTests.cs` | Endpoint/Program and production untouched until RED. |
| Exclusive Prepared Scan Integration Test Owner (T020-T022) | `tests/Vulgata.Tests/PreparedScanning/PreparedScanApiTests.cs`; `tests/Vulgata.Tests/PreparedScanning/PreparedScanReleaseQualificationTests.cs`; `tests/Vulgata.Tests/AnalysisRuntime/PreparedScanRealQualificationTests.cs`; `tests/Vulgata.Tests/PreparedScanning/PreparedScanBoundaryTests.cs` | Only tuple fixture/composition calls and seam assertions may change; preserve behavioral assertions and forbid a tuple-less production overload. |
| Runtime Resolver Test Owner (T023) | `tests/Vulgata.Tests/PreparedScanning/PreparedScanRuntimeIdentityResolverTests.cs` | CGC and historical profiling forbidden. |
| Core Identity Owner (T026) | `src/dotnet/Vulgata.Core/DomainServices/PreparedPass1ContractIdentity.cs`; `IPreparedPass1ContractIdentitySource.cs` | Dependency-free value/port only; no EF, prompt text, provider, dispatcher, transaction, persistence, or public DTO. |
| Agents Identity Owner (T027) | `src/dotnet/Vulgata.Agents/WorkerExecution/EmbeddedPreparedPass1ContractIdentitySource.cs` | Must not edit `WorkerAgentDispatchIdentitySource.cs`, prompts, dispatcher/provider/tools, retry, or scheduling. |
| Exclusive Web/Composition Owner (T028) | `src/dotnet/Vulgata.Web/Data/PreparedScanInitiationCoordinator.cs`; `PreparedScanEndpoints.cs`; `Program.cs`; existing runtime/rechecker paths | One coordinated owner only; no legacy recovery source, Web hardcode, DTO, CGC, `/scan-runs`, or later-stage change. |
| Historical verification/review owners (T029-T033) | Existing evidence/review/checkpoint paths named in those completed tasks | Accepted history; no regeneration or source write. |
| Immutable Run-03 Evidence Owner (T034-T035 and later diagnostic observations) | Existing `evidence/track-c-mvp/run-03/release-report.md` and its database/artifacts | Read-only/no-contact authority; no repair, migration, restart, clone, deletion, or release reuse. |
| Exact-Profile Test Owners (T037-T039) | `tests/Vulgata.Tests/PreparedScanning/ReviewedExactProfileSeedAuthorityTests.cs`; `PreparedSelectionPlannerTests.cs`; `PreparedScanInitiationApiTests.cs`; `PreparedScanReleaseQualificationTests.cs`; `PreparedScanProductionLiteralQualificationTests.cs`; `PreparedScanBoundaryTests.cs`; new `ReviewedExactProfileSeedPostgreSqlTests.cs` | Production untouched until all three REDs are recorded; catalog/resource/parser, Story 12.4, Pass 1 store/policy, EF/model, retained databases forbidden. |
| Exclusive Exact-Profile Production Owner (T040) | `src/dotnet/Vulgata.Core/DomainServices/IReviewedExactProfileSeedAuthority.cs`; `PreparedScanPlanner.cs`; `src/dotnet/Vulgata.Infrastructure/CrossRepository/ReviewedExactProfileSeedAuthority.cs`; `src/dotnet/Vulgata.Web/Data/PreparedScanPairBuilder.cs`; `src/dotnet/Vulgata.Web/Program.cs` | One coordinated owner only. No concurrent shared-file edit; no catalog/resource/parser, caller profile/path, sample literal, generic nonmatch, bound, Pass 1 policy, EF/model, Story 12.4, CGC/legacy change. |
| Required-Seed Compatibility Owner (T042) | `tests/Vulgata.Tests/PreparedPass1/Pass1PairPostgreSqlAtomicTests.cs`; named verification evidence | `src/dotnet/Vulgata.Infrastructure/Data/PreparedPass1Store.cs`, retry/reconciliation/failure policy, Worker and hosted services read-only. |
| Technical verification/review owners (T041-T048) | Exact evidence/review/checkpoint paths named in each task | Source changes only through the owning approved TDD task; root alone owns state/staging/commit. |
| Run-04 Evidence Owner (T049-T057) | `evidence/track-c-mvp/run-04/release-report.md`; five named files under `run-04/screenshots/`; optional run-04 blocked checkpoint | Product/database rows observed only; no repair/insertion/deletion, no run-03 contact, and no second release lane. |
| Final E2E/verification owners (T058-T060) | Exact run-04 E2E, verification, and checkpoint paths named in each task | Same candidate only; root alone owns workflow state, staging, commit, and completion. |

All workers are not alone in the codebase: they must re-observe shared files, preserve unrelated edits, never revert another owner's work, and stop on ownership overlap. Root alone stages, commits, updates workflow state, or exercises user gates.

---

## User Story 6 scenario traceability

| US6 scenario | Primary RED/GREEN/verification owners | Success evidence |
| --- | --- | --- |
| 1. Complete five-row tuple from ordinary records | T011 -> T013/T016/T028 -> T029 | FR-063-FR-066, FR-076; SC-019-SC-020 |
| 2. Exact tuple handed to existing preparation and two Manifests publish | T011, T018, T020 -> T019/T021/T028 -> T022/T029 | FR-068, FR-076; SC-019 |
| 3. Identical request returns the same tuple without duplicates | T012, T015, T018, T020 -> T013/T016/T019/T021/T028 -> T029 | FR-069, FR-071, FR-076; SC-020 |
| 4. Pair/revision/identity mismatch fails closed without attachment/mutation/disclosure | T012, T015, T024, T025 -> T013/T016/T028 -> T029 | FR-070, FR-076; SC-021 |
| 5. Initial and fresh-recheck authorization denial creates nothing | T024, T025 -> T028 -> T029 | FR-064, FR-076; SC-022 |
| 6. Post-initiation preparation failure retries the same tuple; prepared/advanced replay recovers | T015, T018 -> T016/T019/T028 -> T029 | FR-069, FR-072, FR-076; SC-020, SC-023 |
| 7. Identical/competing concurrency cannot expose duplicate/partial/cross-paired state | T015 -> T016 -> T017/T029 | FR-065, FR-071, FR-076; SC-020, SC-025 |
| 8. Default CodeGraph 1.5/JDT-LS path and no CGC/historical fallback | T020, T023, T025 -> T021/T028 -> T029 | FR-074, FR-076; SC-024 |
| 9. No external work in transaction and zero later-stage effects | T015, T018, T025 -> T016/T019/T028 -> T017/T029 | FR-067, FR-075-FR-076; SC-024-SC-025 |

---

## User Story 5 exact-profile scenario traceability

| Exact-profile/release outcome | Primary RED/GREEN/verification owners | Success evidence |
| --- | --- | --- |
| Complete exact pair/ref/revision match derives all four closed source roles | T037 -> T040 -> T041/T043 | FR-079-FR-081; SC-026 |
| Repository/candidate order remains deterministic | T037-T038 -> T040 -> T041/T043 | FR-083; SC-026-SC-028 |
| Nonmatching pair/ref/revision is byte-identically neutral | T037-T038 -> T040 -> T041/T044 | FR-080, FR-086; SC-028 |
| Missing/filtered/unsafe/non-contained/duplicate/ambiguous/split-invalid/over-bound required evidence publishes zero Manifest | T038-T039 -> T040 -> T041/T043 | FR-082; SC-027 |
| Every required segment is score 1,100, selected, and high-value inside unchanged 20/150 bounds | T037-T039 -> T040 -> T041/T043/T051-T052 | FR-081-FR-083; SC-026 |
| Required Pass 1 failure uses existing `FailedPass1` and blocks exact work | T039 -> T040 -> T042-T043 | FR-084, FR-086; SC-029 |
| Default DI is server-owned with no caller profile/path or production sample literals | T038 -> T040 -> T041/T044/T059 | FR-079, FR-085-FR-086; SC-028 |
| No-preseed PostgreSQL vertical reaches unchanged exact Edge and honest Uncertainty | T039 -> T040 -> T043/T047/T051-T052 | FR-076-FR-086; SC-029 |
| Fresh release proof uses remote streaming LLM, restart, screenshots, and never run-03 | T049-T057 -> T058-T060 | FR-039-FR-047, FR-053-FR-062, FR-077; SC-009-SC-018 |

---

## Requirement and success-criterion coverage

| Boundary | Requirements | Success criteria | Owner |
| --- | --- | --- | --- |
| Immutable Track A/B and support/preflight evidence | FR-001-FR-062 | SC-001-SC-018 | Ledger, T001-T009, T029-T035, T044 |
| Request, identity, fresh authorization | FR-063-FR-064, FR-066, FR-069-FR-070 | SC-019-SC-022 | T011-T014, T023-T025, T028-T029 |
| Atomic tuple, transaction and concurrency | FR-065-FR-067, FR-069-FR-073 | SC-019-SC-023, SC-025 | T015-T019, T028-T029 |
| Exact existing-pipeline handoff, tuple-seam compatibility, and phase-aware recovery | FR-068-FR-073 | SC-019-SC-023 | T018-T022, T028-T029 |
| Production composition and zero later-stage effects | FR-074-FR-076 | SC-024-SC-025 | T023-T029 |
| Immutable run-03 diagnostic boundary and fresh continuation | FR-077 | SC-009-SC-014, SC-018-SC-029 | T034-T036, T048-T060 |
| Sole exact-profile authority and complete activation | FR-079-FR-081, FR-085 | SC-026, SC-028 | T037-T041, T043-T044 |
| Matched fail-closed required sources and deterministic unchanged bounds | FR-082-FR-083, FR-086 | SC-026-SC-028 | T037-T041, T043-T044 |
| Existing required Pass 1 gate and exact outcome | FR-084, FR-086 | SC-026, SC-029 | T039-T043, T047, T051-T052 |
| Fresh real release, remote LLM, restart, screenshots, final proof | FR-039-FR-047, FR-053-FR-062, FR-077-FR-086 | SC-009-SC-018, SC-026-SC-029 | T049-T060 |
| Explicit non-goals/backlog guard | FR-048-FR-052, FR-061-FR-062, FR-073-FR-075, FR-078, FR-085-FR-086 | SC-013-SC-018, SC-024-SC-029 | Every phase; especially T040-T048 and T059 |

All FR-063-FR-086, SC-019-SC-029, all nine US6 scenarios, and every reviewed exact-profile outcome have an owning RED/GREEN/verification or preserved accepted path. FR-001-FR-062 and SC-001-SC-018 remain immutable accepted behavior plus surrounding regression and the fresh run-04 release lane; run-03 remains diagnostic history and none is silently reimplemented.

---

## Implementation strategy

### MVP-first sequence

1. Accept this exact task definition and bind the standing implementation authority to its hash.
2. Observe T037-T039 RED at the authority/planner, authenticated builder/public, and PostgreSQL no-preseed exact-outcome seams.
3. Use one coordinated owner for the minimal Core/Infrastructure/Web/Program GREEN.
4. Prove matched failure cases, deterministic unchanged bounds, byte-identical nonmatch neutrality, existing required Pass 1 failure behavior, and the production-composition exact Edge/Uncertainty vertical.
5. Run surrounding regression, convergence, independent quality review, conditional economical E2E, and technical acceptance.
6. Create fresh guarded run-04 and a new database only after T048; prove reviewed required seeds before Pass 1 and never contact run-03.
7. Finish one real Scan + remote streaming Chat + application restart + screenshot report, release E2E, final verification, and root-owned completion checkpoint.

### Failure boundaries

- Any need for EF/DbContext/configuration/migration/snapshot, Story 12.4, exact profile/catalog/resource/parser, caller profile/path, production sample literal, bound increase, generic nonmatch change, Pass 1 policy/store, CGC/legacy/general-Scan, or Track A/B rewrite is `BLOCKED`; return to plan without inventing a workaround.
- A PostgreSQL test cannot fall back to SQLite and must use only its own disposable lowercase `vulgata_story125_*` database.
- Run-03 is immutable diagnostic evidence. A run-04 external prerequisite failure is sanitized blocked evidence, not permission to contact/reuse/repair run-03, fake evidence, or repeat an unchanged expensive run.
- A missing release row is a product/result failure; never insert, repair, delete, or reinterpret database state.
- Standing implementation/egress authority does not authorize Git staging/commit, retained-database cleanup, secret disclosure, scope expansion, or Story completion.

---

## Inline task-author self-review

| Lens | Result | Evidence |
| --- | --- | --- |
| Accepted-definition coverage | PASS | Corrected spec/plan/research/data model/quickstart/prepared-scan contract, August 31 Sprint Change Proposal, accepted exact-profile plan review and planned-change decision, current source/tests, run-03 diagnostic report, and fresh-run obligations are represented. |
| Completed-evidence preservation | PASS | T001-T035 remain checked. Track A/B, Prepared Scan initiation, Prepared Pass 1 identity/transaction corrections, and run-03 diagnostic evidence are immutable; no unchecked old T036-T045 success claim transfers. |
| Earliest affected boundary | PASS | T036 is the first current gate after run-03 exposed the seed-definition gap. No accepted earlier implementation is regenerated or relabeled as current RED/GREEN. |
| Public-seam TDD honesty | PASS | T037-T039 must fail first at pure authority/planner, authenticated builder/public, and PostgreSQL no-preseed exact-outcome seams. T040 is the sole production GREEN; direct planner tests and SQLite cannot replace T039/T043. |
| Dependency ordering | PASS | Definition/authorization -> three REDs -> one coordinated GREEN -> deterministic/failure/Pass 1/PostgreSQL verification -> regression -> convergence -> quality -> conditional E2E -> technical acceptance -> fresh run-04 -> release E2E/final verification. |
| Exact ownership | PASS | T040 alone owns all five coordinated production paths. Test, Pass 1 compatibility, review, run-04 evidence, and final gate owners are disjoint; root retains state, staging, commit, and user gates. |
| Exact activation and neutrality | PASS | Complete ordinal persisted-name/full-ref/Profiled-Revision matching, one-to-one Repository binding, canonical role/path derivation, reversal determinism, catalog-unavailable closure, and byte-identical nonmatch behavior are explicit. |
| Matched failure coverage | PASS | Missing, filtered, unsafe, non-contained, empty/hash-invalid, duplicate/multiply mapped, ambiguous, split-invalid, >20 required segments, selection omission, and unavailable authority all reject before either Manifest commits. |
| Deterministic bounds | PASS | Score 1,100 is the sole new closed tier; existing 20-per-Repository and 150-pair bounds, ordering, audits, package fingerprints, Manifests, and neutral golden outputs are pinned without increases. |
| Existing Pass 1 authority | PASS | T042 proves score 1,100 flows through unchanged direct-score `>=400`/top-20 mandatory accounting and existing `FailedPass1`; `PreparedPass1Store`/policy/Worker/hosted-service edits are forbidden. |
| PostgreSQL exact outcome | PASS | T039/T043 begin without lifecycle/content rows and drive the production endpoint through required seeds and Pass 1 to unchanged exact decision/current REFERENCES Edge and honest Uncertainty; Story 12.4 fixtures, row repair, SQLite, and run-03 cannot substitute. |
| Composition and security | PASS | Exactly one server-owned default authority, zero caller profile/path/role/score/selector fields, zero production sample literals, CodeGraph 1.5/JDT-LS/no-CGC composition, existing authorization/replay checks, and no later-stage preparation leakage are guarded. |
| Scope exclusions | PASS | EF/DbContext/configuration/migration/snapshot, Story 12.4, profile/catalog/resource/parser, generic nonmatch, bounds, Pass 1 policy, caller authority, production samples, CGC/legacy/general Scan, and accepted Track A/B/initiation/Pass 1 rewrites are explicitly forbidden. |
| Release continuation | PASS | Run-04 begins only after T048, uses a new guarded database, proves required seeds before Pass 1, invokes the real remote streaming provider, restarts only Vulgata, captures five redacted screenshots, and never contacts or reuses run-03. |
| FR/SC/source traceability | PASS | Coverage tables own FR-079-FR-086 and SC-026-SC-029 while preserving FR-001-FR-078/SC-001-SC-025; every corrected source authority has at least one RED/GREEN/verification or immutable-preservation task. |
| Checklist format | PASS | All 60 tasks are sequential, use checkbox + `T###`, include `[P]` only for disjoint T041/T042 work, carry required `[US5]/[US6]` labels in story phases, and name exact source/test/evidence paths. |

**Self-review conclusion**: `TASKS_READY_FOR_INDEPENDENT_DEFINITION_REVIEW_EXACT_PROFILE_SEEDS_ATTEMPT_01`. T001-T035 remain accepted history; T036 is the earliest current gate; T037-T039 supply honest REDs; T040 is the single coordinated minimal GREEN; T041-T048 prove deterministic/fail-closed/neutral/Pass 1/PostgreSQL/regression/review/E2E acceptance; and T049-T060 require a fresh run-04 with remote LLM, restart, screenshots, and final verification. This task definition authorizes no production implementation by itself; the next action is independent definition review and the root-owned implementation-authorization binding already permitted by the standing decision.

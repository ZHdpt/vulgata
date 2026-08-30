---
description: "Dependency-ordered Story 12.5 Prepared Scan initiation and Track C-MVP release-continuation tasks"
---

# Tasks: Story 12.5 Prepared Scan Initiation and Release Continuation

**Input**: `spec.md`, accepted corrected `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/prepared-scan-initiation.md`, `contracts/release-evidence-contract.md`, `.specify/memory/constitution.md`, `AGENTS.md`, Story 12.5 in `docs/bmad/epics.md`, the August 30 Prepared Scan Sprint Change Proposal, plan-review attempts 01-02, accepted planned-change authorization 01, the existing task/evidence history, and the current source/tests named below.

**Pinned definition**: Source commit `d84788f4d4cb7536dac119c62c60cd77352adeb1`; corrected spec SHA-256 `469f72c4e7fc62874e082a7f425351d9f46cf422ae8641d4bee7b64915dd67c0`; corrected plan SHA-256 `6ebb0a525721716bfcbb3dce0d330b4846a6f3cf50c604ceb2bf9b0ed41346a8`; accepted plan-review `reviews/plan-review/plan-review-prepared-pass1-identity-authority-03.json`; accepted Track A/Track B checkpoint `f292caec427f9844be3c0f65b4cb00a9292080a5`; immutable Story 12.4 dependency `4798b97d9f23aef587829175df7ab7ebc3daf995`.

**Regeneration disposition**: T001-T024 remain checked and preserved, including the partial T025-T027 chronology as historical evidence only. Old T025 (runtime/Web GREEN), T026 (public all-nine RED), T027 (endpoint/DI GREEN), T028 (verification), T029 (regression), T030 (convergence), T031 (review input), T032 (quality review), and T033 (implementation gate) are replaced in-place by the corrected T025-T033 below; none of their unchecked completion claims transfers. New T025-T029 are the identity-authority RED/Core/Agents/Web/verification slice; new T030-T033 retain only the accepted regression, convergence/review, quality, and implementation-acceptance continuation. Historical run 01/run 02, blocked checkpoints, and the retained run-02 database remain immutable and are never reused or contacted.

**Definition-review correction disposition**: The accepted plan-review is `plan-review-prepared-pass1-identity-authority-03.json`. DR-PSI-001 remains closed by T020-T022; DR-PSI-002 by T023-T024 and corrected T028-T029; DR-PSI-003 by T015-T017. This file is the only regenerated task artifact; no upstream artifact, source, workflow state, evidence result, stage, or commit is changed.

**Lifecycle authorization boundary**: The accepted planned-change decision authorizes this task generation and independent definition review only. Before T011 or any source/test/database/product write, an independent definition review must accept this exact `tasks.md`, the root orchestrator must obtain explicit implementation approval bound to the same definition hash, and T010 must pass. No checkbox, earlier Track C approval, expired standing authorization, or completed preflight transfers implementation authority.

**Classification rule**: `EXISTING-EVIDENCE` preserves accepted evidence without manufacturing a RED. `COMPATIBILITY` may be green on first run. `RED` must be observed failing at the named behavioral seam before its paired `GREEN`. `GREEN` is the smallest approved production change that turns its owning RED green. `GATE`, `RUN`, `QUERY`, `RESTART`, `MANUAL`, `REPORT`, and `VERIFY` neither waive later gates nor authorize scope expansion.

**TDD rule**: T011 is the preserved no-preseed public RED. Corrected T025 is the first new identity-authority RED and must run before T026-T028 production GREENs. A direct store test cannot substitute for T011 or the completed public vertical in T028-T029.

**Hard boundaries**: No task may change `VulgataDbContext.cs`, `Data/Configurations/*`, migrations, `VulgataDbContextModelSnapshot`, Story 12.4, accepted Track A/B behavior/tests, general/single/N-Repository Scan behavior, selection/dispatch/retry algorithms, Worker/Pass/Document/Edge/Uncertainty/Chat/LLM behavior, `CodeGraphContextProvider`, `RepositoryProfilingCoordinator`, `/scan-runs`, project/package files, Git index/history, retained databases, or the historical release-envelope machinery. No external analysis may execute inside the initiation transaction. Any need for those changes is `BLOCKED` and returns to plan; it is not an implementation improvisation.

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
- [ ] T025 [US6] Classification: RED — After a fresh definition review accepts this regenerated boundary and a newly scope-bound implementation approval is recorded, add/extend tests first in `tests/Vulgata.Tests/PreparedScanning/PreparedPass1ContractIdentitySourceTests.cs`, `PreparedScanInitiationIdentityTests.cs`, `PreparedScanInitiationCoordinatorTests.cs`, and `PreparedScanInitiationApiTests.cs`. Assert default composition binds the embedded v2 prompt to `pass1-code-logic-worker.v2/sha256/{lowercase-64-hex}` plus `code-logic/file-v2`; null, legacy-v1, malformed-hash, wrong-schema, missing, duplicate, invalid-UTF8 and schema-inconsistent authorities fail HTTP 503 before fresh authorization/store/external analysis; characterize unchanged `WorkerAgentDispatchIdentitySource` v1/`vulgata.knowledge-document/1.0`; retain the unchanged T011 no-preseed public RED; and add a layering RED proving `PreparedScanInitiationAuthorizationRechecker.cs` must not import EF Core or resolve `VulgataDbContext`, while existing specific Core repository/authorization seams are sufficient. Record honest RED in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/tdd/prepared-pass1-identity-authority-red-01.md` before new Core/Agents/Web production edits (US6 scenarios 1-2, 8; FR-063-FR-066, FR-073-FR-076, SC-019-SC-025).
- [ ] T026 [US6] Classification: GREEN — After T025 RED, add only `src/dotnet/Vulgata.Core/DomainServices/PreparedPass1ContractIdentity.cs` and `src/dotnet/Vulgata.Core/DomainServices/IPreparedPass1ContractIdentitySource.cs`; expose exactly prompt identity and Document schema identity, no prompt text/provider/dispatcher/tool/transaction/persistence/public-request fields, and preserve existing Core dependency and canonical-envelope outputs (FR-066, FR-073, FR-076, SC-020-SC-021).
- [ ] T027 [US6] Classification: GREEN — After T026, add only `src/dotnet/Vulgata.Agents/WorkerExecution/EmbeddedPreparedPass1ContractIdentitySource.cs`; resolve exactly embedded logical resource `Vulgata.Agents.Prompts.pass1-code-logic-worker.v2.md`, decode strict UTF-8, reject missing/duplicate/empty/schema-inconsistent content, hash exact decoded text re-encoded UTF-8, and return closed v2/hash plus `code-logic/file-v2`. Do not edit `WorkerAgentDispatchIdentitySource.cs`, either prompt, dispatcher, provider, tools, retry or scheduling behavior (FR-066, FR-073, FR-075, SC-020-SC-024).
- [ ] T028 [US6] Classification: GREEN — After T025-T027 RED/GREEN, one exclusive Web/composition owner edits `src/dotnet/Vulgata.Web/Data/PreparedScanInitiationAuthorizationRechecker.cs` plus `PreparedScanInitiationCoordinator.cs`, `src/dotnet/Vulgata.Web/Api/Scans/PreparedScanEndpoints.cs`, and `src/dotnet/Vulgata.Web/Program.cs`. Remove the rechecker’s `Microsoft.EntityFrameworkCore` and `Vulgata.Infrastructure.Data.VulgataDbContext` access; retain Web-only Identity/UserManager orchestration for role checks, and use existing materialized Core seams `ISystemRepository` and `IRepositoryRepository` backed by Infrastructure for SystemOwnerAssignment and System/Repository ownership reads. Replace only the coordinator dependency on `IScanRecoveryDispatchIdentitySource` with `IPreparedPass1ContractIdentitySource`; register exactly one scoped Core-to-Agents mapping, preserve runtime resolver/fresh recheck, map existing 404/409/503 outcomes, and prove unchanged T011 reaches exact tuple/handoff. No Web hardcoded prompt/schema, legacy fallback, DTO, Agent tool, CGC/legacy profiling, `/scan-runs`, or later-stage call; no concurrent task may edit these shared files (FR-063-FR-076, SC-019-SC-025).

---

## Phase 13: Surrounding regression, forbidden-path guard, and implementation acceptance

**Purpose**: Prove the bounded delta without reopening accepted Track A/B or entering the real release run prematurely.

- [ ] T029 Classification: VERIFY — After T028, run focused identity, coordinator, API, runtime-resolver, and PostgreSQL-only initiation filters plus unchanged T011; add an automated/read-only path guard that `PreparedScanInitiationAuthorizationRechecker.cs` contains no `Microsoft.EntityFrameworkCore`/`VulgataDbContext` reference and that its dependencies are the materialized Core `ISystemRepository`/`IRepositoryRepository` seams, with Infrastructure-backed DI. Record literal v2 hash/schema, hostile ordering, legacy v1 regression, default DI graph, no-CGC/CodeGraph 1.5/JDT-LS composition, all-nine scenarios, tuple visibility, no-later-stage counters, phase recovery, database isolation, and RED/GREEN chronology in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/prepared-scan-initiation-identity-authority-01.md` (depends T028; FR-063-FR-076, SC-019-SC-025).
- [ ] T030 Classification: VERIFY — After T029, run focused Track A/B regression for corpus/preconnection support, Prepared Chat retrieval/currentness/packet/citation/conversation, strict streaming, accepted publication, UI, production Scan status/resume, deterministic behavior, existing Prepared Scan, and Story 12.2 identities; run Web build and `git diff --check`, and repeat forbidden-path/dependency guards proving no Web EF/DbContext access, no migration/model changes, no legacy recovery-source mutation, and no CGC/legacy/general-Scan/later-stage changes. Record results in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/prepared-scan-initiation-regression-01.md` (depends T029; FR-048-FR-052, FR-073-FR-078, SC-013-SC-018, SC-024-SC-025).
- [ ] T031 Classification: VERIFY — After T030, run independent convergence against accepted spec/plan/tasks and exact implementation/test evidence, then prepare verdict-neutral quality-review input with candidate/base/diff, corrected hashes, owners, RED/GREEN evidence, scenario matrix, authorization/concurrency/transaction proof, default CodeGraph 1.5/no-CGC result, forbidden-path result, and external exclusions in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/convergence/prepared-scan-initiation-01.md` and `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/review/prepared-scan-initiation-quality-input-01.md`; an EF/model/Story 12.4/CGC/legacy/general-Scan/later-stage need is a blocked contradiction, not an appended task (depends T030; FR-063-FR-078, SC-019-SC-025).
- [ ] T032 Classification: GATE — Dispatch fresh independent quality review of the regenerated definition and implementation evidence; only the reviewer may write `specs/014-grounded-llm-wiki-chat-release-evidence/reviews/quality-review/prepared-scan-initiation-quality-review-01.json`. `REVISE` returns only to the owning approved TDD slice, `BLOCKED` preserves a safe checkpoint, and only `ACCEPT` advances (depends T031; FR-063-FR-078, SC-019-SC-025).
- [ ] T033 Classification: GATE — After T032 `ACCEPT`, rerun focused initiation filters, Web build, diff check, corrected artifact hashes, scope and forbidden guards, then bind the implementation-accepted candidate in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/prepared-scan-initiation-implementation-accepted-01.md`; no run-03 external work may start before this checkpoint (depends T032; FR-063-FR-078, SC-019-SC-025).

---

## Phase 14: User Story 5 - Fresh guarded run-03 preflight and database (Priority: P1)

**Goal**: Start a new candidate/database release lane only after the corrected entrance is accepted.

**Independent Test**: Before product access, run 03 binds one accepted candidate, two exact Repository Profiled Revisions, ready real prerequisites, and a new owned lowercase database distinct from every retained database.

- [ ] T034 [US5] Classification: GATE — Create `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-03/release-report.md` with truthful `running` or sanitized `blocked` disposition; pin the T033 candidate, exact two real Java/XML Repository aliases/Profiled Revisions, clean owned state, PostgreSQL 17/Docker, CodeGraph 1.5 hashes, Java 21, JDT-LS identity/hash, non-secret configured streaming-provider identity/credential presence/network readiness, authenticated authorization readiness, and browser/manual-screenshot capability. Preserve run 01/run 02 and their databases unchanged; never print secrets, connection strings, credentials, tokens, source, or absolute host paths (FR-039, FR-041-FR-042, FR-046-FR-047, FR-052, FR-062, FR-077, SC-010, SC-013-SC-014, SC-018).
- [ ] T035 [US5] Classification: GATE — Generate a fresh unique lowercase `vulgata_story125_*` name through the accepted preconnection guard, prove it differs from every retained database and invokes zero product connection/host/query calls before acceptance, then create only that database with the approved administrator mechanism and production-bootstrap one Administrator, one authorized ordinary System, provider assignment, and exactly two ordinary Repository records at the pinned refs/revisions. Pre-seed no Scan Run, Pre-Scan Result, Scan Execution, coordination, Manifest, File Code Unit, or Document; record sanitized purpose/owner/version/commands/exits in the run-03 report (FR-041, FR-047, FR-051-FR-052, FR-062, FR-076-FR-077, SC-013-SC-014, SC-018-SC-019).

**External blocker protocol**: A missing/unsafe prerequisite sets run 03 to `blocked`, records only completed truthful observations in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-c-mvp-blocked-03.md`, and leaves dependent tasks unchecked. Do not reuse run 02, fake a provider, fabricate evidence, delete any retained database, or repeat an unchanged expensive run.

---

## Phase 15: User Story 5 - Corrected Prepared Scan and one combined real release run (Priority: P1)

**Goal**: Enter through the corrected endpoint, prove real Prepared Scan first, then continue the accepted Pass/relationship/Chat/restart evidence path.

**Independent Test**: One host candidate and the fresh run-03 database produce the required two-Repository initiation/Manifests, 50-150 File Code Units, terminal Pass 1/Pass 2, exact Cross-Repository evidence, twelve real Chat rows, same-database restart, and five redacted screenshots.

- [ ] T036 [US5] Classification: RUN — Start host-run Vulgata against only the T035 database, authenticate, and submit the corrected existing `POST /api/systems/{systemId}/prepared-scans` request with exactly the two ordinary Repositories and pinned refs/Profiled Revisions. Query one complete initiation tuple and two real approved Prepared Scan Manifests before Pass 1; repeat the request after publication and prove the same Runs/executions/coordination/Manifests, 200 replay, zero duplicate rows, and zero external preparation rerun. Only after real Prepared Scan succeeds, execute the accepted Pass 1, exact Cross-Repository Resolution, and Pass 2 surfaces to terminal state. Record real CodeGraph 1.5/JDT-LS/safe XML and sanitized commands/exits/durations in the run-03 report; never call `/scan-runs`, CGC, the historical profiling coordinator, or change downstream Scan behavior (FR-039, FR-042-FR-043, FR-049-FR-052, FR-062, FR-068, FR-073-FR-077, SC-010-SC-011, SC-018-SC-019, SC-023-SC-025).
- [ ] T037 [US5] Classification: QUERY — Against only the run-03 database, record bounded read-only rows for the complete initiation tuple; two Manifests/revisions/tool bindings; 50-150 File Code Units; terminal Pass 1/Pass 2 jobs; high-value seeds selected/succeeded/failed with failed zero and selected equal succeeded; ordinary failure budget; source-anchored current Code Logic and Business Logic Documents; one accepted decision-bound exact Cross-Repository REFERENCES Edge; one honest Uncertainty; at least three Business Logic Documents; one published Cross-Repository Flow Group; and pre-restart Document/Edge/Flow Group identities/counts in the run-03 report. Use actual mappings, never insert/repair/recompute missing facts, and do not claim operator timing proves transaction ordering (FR-039, FR-043, FR-049-FR-051, FR-077, SC-010-SC-014).
- [ ] T038 [US5] Classification: MANUAL — Before the first provider call, pin exactly `q01`-`q12` from `tests/Vulgata.Tests/ReleaseGate/Story125/Fixtures/story125-question-corpus.json`; execute every case through the authenticated Business/IT page and real configured `OpenAiCompatibleChatProviderStreamingTransport` with fresh `ChatConversationState` per row, topical lineage only for q01-q03, and q12 limited to `prompt-boundary-only`. Record all twelve rows, accepted/cited material-claim counts, usefulness, unauthorized disclosure, streaming, render duration, fingerprint when exposed, Tier 1 references, and limitations in the run-03 report; do not replace failures or count deterministic rescue/fake/non-streaming output (FR-037-FR-039, FR-044, FR-053-FR-060, FR-062, SC-008-SC-009, SC-013, SC-015-SC-018).
- [ ] T039 [US5] Classification: RESTART — Record pre-restart process/database identity, stop only Vulgata, retain PostgreSQL and the run-03 database, restart with the same configuration, re-authenticate, open persisted wiki content, and submit one new grounded Chat request; exclude transient Conversation State survival from the claim and record closed results in the run-03 report (FR-039, FR-045, FR-053, SC-012-SC-013, SC-015).
- [ ] T040 [US5] Classification: QUERY — Rerun read-only DocumentVersion, DocumentEdge, and FlowGroup identity/count observations after T039, compare with T037, and record persisted access, data-loss disposition, and zero restart-caused duplicate publication; never correct a product/result failure in the database (FR-039, FR-043, FR-045, FR-049-FR-051, SC-011-SC-014).
- [ ] T041 [US5] Classification: MANUAL — Capture at least one real repository-relative screenshot at `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-03/screenshots/scan.png`, `business-chat.png`, `it-chat-source-anchors.png`, `cross-repository-result.png`, and `post-restart-wiki-chat.png`; manually redact/reject secrets, credentials, connection strings, absolute host paths, fake Backup B state, retained-run state, or unrelated candidate output, and record category/time/real-state/redaction results without screenshot automation or hashes (FR-039, FR-046, FR-061-FR-062, SC-013, SC-018).
- [ ] T042 [US5] Classification: REPORT — Complete `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/track-c-mvp/run-03/release-report.md` against `contracts/release-evidence-contract.md` with exactly one truthful disposition; candidate/revisions/tool/provider/database identities; ordered sanitized commands/exits; all required query observations; twelve fresh-state rows when provider execution occurred; q12's bounded oracle; restart; five screenshot links for a pass; failures and limitations. `passed` requires every Scan/output/Chat/restart/screenshot threshold and no forbidden change (FR-039-FR-047, FR-053, FR-060-FR-062, FR-077, SC-009-SC-018).

---

## Phase 16: Independent E2E, final verification, and completion

**Purpose**: Apply remaining schema-v3 gates to the same accepted implementation candidate and run-03 report.

- [ ] T043 Classification: VERIFY — After T042, run the required independent E2E gate against the combined host-run evidence and only economical same-candidate browser/API checks needed for authenticated real Prepared Scan, real Scan state, Business/IT accepted-only Chat render, exact Cross-Repository result, and post-restart wiki/Chat access; do not create a second orchestration run or substitute mocks, and write the decision/execution evidence to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/e2e/track-c-mvp-run-03.md` (FR-039-FR-046, FR-053-FR-062, FR-063-FR-077, SC-009-SC-025).
- [ ] T044 Classification: VERIFY — After T033 and T043 accept, run final focused/full-applicable tests, PostgreSQL initiation acceptance, Web build, `git diff --check`, exact hash inventory, candidate/Story 12.4 ancestry, run-03 database/revision/restart bindings, report checklist, five screenshot/redaction checks, no-secret scan, default CodeGraph 1.5/no-CGC composition, and every forbidden-path guard; write the pinned truthful verdict to `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/verification/track-c-mvp-final-03.md` (FR-039-FR-078, SC-009-SC-025).
- [ ] T045 Classification: GATE — Only after T044 verifies a passed run-03 report and every review/E2E/verification gate accepts the same candidate, bind completion in `specs/014-grounded-llm-wiki-chat-release-evidence/evidence/checkpoints/track-c-mvp-completion-03.md`; then only the root orchestrator may update Story 12.5 in `docs/bmad/implementation-artifacts/sprint-status.yaml` and workflow-owned state. Never mark done for blocked, failed, not-run, unreviewed, uncheckpointed, reused-database, or partial evidence (FR-039, FR-048-FR-052, FR-062-FR-078, SC-013-SC-025).

---

## Dependencies and execution order

### Phase dependencies

1. T001-T009 are immutable completed evidence; they authorize nothing current.
2. Fresh definition review and explicit implementation approval precede `T010`; `T010 -> T011`.
3. `T011 -> T012 -> T013 -> T014` establishes the public RED and Core identity/contract.
4. `T014 -> T015 -> T016 -> T017` establishes PostgreSQL atomicity/replay before Web integration.
5. `T017 -> T018 -> T019`; exact-tuple builder/handoff behavior precedes application orchestration.
6. `T019 -> T020 -> T021 -> T022` updates and verifies every accepted caller of the removed tuple-less seam before application orchestration proceeds.
7. T018-T024 are preserved completed/existing-evidence history bound to the retained checkpoint; they authorize no rerun and are not dependencies for new production edits.
8. Fresh definition acceptance plus newly bound implementation approval -> T025; `T025 -> T026 -> T027 -> T028 -> T029`. T025 is the corrected identity/layering RED, T026/T027 are only Core/Agents identity GREEN, T028 is the exclusive Web/rechecker composition GREEN, and the original T011 public expectation must turn GREEN at T028.
9. `T029 -> T030 -> T031 -> T032 -> T033`; no real external release run begins before implementation acceptance.
10. `T033 -> T034 -> T035 -> T036 -> T037 -> T038 -> T039 -> T040 -> T041 -> T042` is one fresh candidate/database/report lane.
11. `T042 -> T043 -> T044 -> T045`; task completion never substitutes for independent gate acceptance.
12. Any authority contradiction or external blocker follows the named blocked protocol and prevents all dependent tasks.

### Dependency graph

```text
historical completed evidence T001-T009
                 |
definition review -> implementation approval -> T010 -> T011 public RED
                                                        |
                                                        v
T012 -> T013 -> T014 -> T015 -> T016 -> T017 -> T018 -> T019 -> T020 -> T021 -> T022
                                                                                      |-> T023 --|
                                                                                      |-> T024 --|-> T025 -> T026 -> T027 -> T028 -> T029
                                                                                                                                |
                                                                                                                                v
T030 -> T031 -> T032 -> T033 -> T034 -> T035 -> T036 -> T037 -> T038 -> T039 -> T040 -> T041 -> T042 -> T043 -> T044 -> T045
```

### Preserved evidence boundary

```text
T018-T024 are checked existing evidence and are not rerun. Fresh definition
acceptance plus implementation approval starts the executable T025 boundary.
```

All other current tasks are serial because they depend on an observed RED, share production composition, use one PostgreSQL/candidate/report lane, or are ordered acceptance gates.

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
| Verification/review owners (T029-T033, T043-T045) | Exact evidence/review/checkpoint paths named in each task | Source changes only through the owning approved TDD task; root alone owns state/staging/commit. |
| Run-03 Evidence Owner (T034-T042) | `evidence/track-c-mvp/run-03/release-report.md`; five named files under `run-03/screenshots/`; optional blocked checkpoint 03 | Product/database rows observed only; no repair/insertion/deletion or retained-run mutation. |

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

## Requirement and success-criterion coverage

| Boundary | Requirements | Success criteria | Owner |
| --- | --- | --- | --- |
| Immutable Track A/B and support/preflight evidence | FR-001-FR-062 | SC-001-SC-018 | Ledger, T001-T009, T029 |
| Request, identity, fresh authorization | FR-063-FR-064, FR-066, FR-069-FR-070 | SC-019-SC-022 | T011-T014, T023-T025, T028-T029 |
| Atomic tuple, transaction and concurrency | FR-065-FR-067, FR-069-FR-073 | SC-019-SC-023, SC-025 | T015-T019, T028-T029 |
| Exact existing-pipeline handoff, tuple-seam compatibility, and phase-aware recovery | FR-068-FR-073 | SC-019-SC-023 | T018-T022, T028-T029 |
| Production composition and zero later-stage effects | FR-074-FR-076 | SC-024-SC-025 | T023-T029 |
| Fresh real-run continuation | FR-077 | SC-009-SC-014, SC-018-SC-025 | T034-T044 |
| Explicit non-goals/backlog guard | FR-048-FR-052, FR-061-FR-062, FR-073-FR-075, FR-078 | SC-013-SC-018, SC-024-SC-025 | Every phase; especially T029-T033 and T044 |

All FR-063-FR-078, SC-019-SC-025, and all nine US6 scenarios have an owning RED, GREEN, and verification path. FR-001-FR-062 and SC-001-SC-018 remain either immutable accepted behavior plus regression or exact run-03 release work; none is silently reimplemented.

---

## Implementation strategy

### MVP-first sequence

1. Accept this definition and obtain explicit implementation approval.
2. Observe the public no-preseed RED.
3. Implement Core identity/contract, PostgreSQL atomicity, exact-tuple handoff, accepted-caller compatibility, shared health/initiation runtime identity, Web authorization, and public composition in that order.
4. Prove all nine US6 scenarios and surrounding regression.
5. Converge, independently review, and checkpoint the implementation candidate.
6. Start a fresh guarded run 03; prove Prepared Scan before any Pass 1 continuation.
7. Finish one Scan + Chat + restart + screenshot report, E2E, final verification, and root-owned completion checkpoint.

### Failure boundaries

- A migration/model/Story 12.4/CGC/legacy/general-Scan/later-stage need is `BLOCKED`; return to plan without editing upstream authority or inventing a workaround.
- A PostgreSQL test cannot fall back to SQLite and must use only its own disposable lowercase `vulgata_story125_*` database.
- A run-03 external prerequisite failure is sanitized blocked evidence, not permission to reuse run 02, fake evidence, or rerun unchanged.
- A missing release row is a product/result failure; never insert, repair, delete, or reinterpret database state.
- Production implementation approval does not authorize Git staging/commit, database cleanup, external egress beyond separately approved run prerequisites, or Story completion.

---

## Inline task-author self-review

| Lens | Result | Evidence |
| --- | --- | --- |
| Accepted-definition coverage | PASS | Corrected spec/plan/research/data model/quickstart/contracts, Story 12.5, Sprint Change Proposal, plan reviews, authorization, existing tasks, current source/tests, and truthful blocked run evidence are represented. |
| Completed-evidence preservation | PASS | T001-T024 are checked/preserved, with T018-T024 explicitly bound to the retained checkpoint as existing evidence; run 01/run 02 and blocked checkpoints remain immutable. No prior unchecked task or success claim transfers. |
| Public-seam TDD honesty | PASS | T011 is the preserved endpoint/PostgreSQL no-preseed RED; corrected T025 is the identity-authority RED before T026-T028 Core/Agents/Web GREENs, and T029 verifies the same public seam. |
| Dependency ordering | PASS | Public RED -> Core -> Infrastructure -> exact handoff -> accepted-caller compatibility -> shared runtime/auth -> endpoint/DI -> regression -> convergence/review/acceptance -> fresh run 03 -> E2E/final verification. |
| Exact ownership | PASS | Every source/test/evidence file has one owner; the four old-seam suites belong only to T020-T022, and Program/endpoints/shared integration is serialized under one exclusive Program/Prepared Scan Integration Owner. Root retains state, staging, commit, and user gates. |
| Tuple-seam compatibility | PASS | T020-T022 name all four accepted callers, update only immutable-tuple fixture/composition calls and graph assertions, preserve behavioral assertions, and prohibit a tuple-less production overload. |
| Prepared Pass 1 identity authority | PASS | T025 tests v2/hash/schema and hostile fail-closed ordering plus unchanged v1 recovery; T026 owns the Core value/port; T027 owns the sole Agents implementation; T028 replaces only Web composition. |
| PostgreSQL acceptance | PASS | T011/T015/T017/T028/T029 require fresh lowercase `vulgata_story125_*` disposable databases, independent connections, identical/competing races, rollback, cancellation, and lock timeout; SQLite is explicitly insufficient. Pre-commit failure proves zero rows, while ambiguous post-commit acknowledgement proves unavailable plus exact-retry convergence without a false absence claim. |
| Authorization/security | PASS | Initial, fresh pre-store, and pre-publication checks are distinct. Administrator/SystemOwner/assignment/Repository revocation is missing-equivalent with zero store calls/writes and no protected detail. |
| Replay/recovery | PASS | Fixed identity vectors, mismatch dimensions, retryable pre-publication replay, prepared/advanced no-analysis recovery, and corrupt/legacy conflict are explicit. |
| Composition/side effects | PASS | Default CodeGraph 1.5/JDT-LS and no-CGC composition are tested; Worker/Pass/Document/Edge/Uncertainty/Chat/LLM effects are counted as zero; transaction visibility/order is observable. |
| Scope exclusions | PASS | No migration/DbContext/configuration/snapshot, Story 12.4, Track A/B, general/single/N-Repository Scan, later-stage behavior, fake evidence, retained-database deletion, or external analysis in transaction is authorized. |
| Release continuation | PASS | Run 03 is fresh, starts only after implementation acceptance, enters the corrected endpoint from ordinary records, and retains every current Track C Scan/query/Chat/restart/screenshot/report gate. |
| FR/SC/scenario traceability | PASS | Coverage tables own FR-063-FR-078, SC-019-SC-025, and each of nine US6 scenarios while preserving FR-001-FR-062/SC-001-SC-018 authority. |
| Checklist format | PASS | All 45 tasks use sequential `T###`, checkbox, optional `[P]`, required story label in story phases, classification, exact paths, and dependency-aware descriptions. |

**Self-review conclusion**: `TASKS_READY_FOR_INDEPENDENT_DEFINITION_REVIEW_ATTEMPT_04`. Raw findings `DR-PSI-IA-003-001` and `DR-PSI-IA-003-002` are addressed at the tasks owner: T018-T024 are checked existing evidence, T025 is the layering/identity RED, T026/T027 are only Core/Agents GREEN, and T028-T030 explicitly remove and guard Web EF access. This task definition authorizes no production implementation by itself; the next action is the independent definition review, followed by the explicit implementation-approval pause.

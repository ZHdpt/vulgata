# Tasks: Sequential Scan Runner & Typed Agent Dispatch

**Input**: Design documents from `specs/005-sequential-scan-runner/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: TDD is mandatory for this story. Every group records transient failing test evidence (RED) before its production change and reaches GREEN before the next group.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. The six TDD groups (G1–G6) from plan.md map to phases as follows: G1 → Foundational; G2 → US1; G3 → US2 + US5; G4 → US3; G5 → US4; G6 → Migration.

**Execution level**: `expedited-mvp` (August 15 exception). EF migrations, `VulgataDbContext.cs`, `Program.cs`, and `sprint-status.yaml` are integration-owner-gated; tasks touching them are marked **[INTEGRATION-OWNER-GATED]** and must be coordinated before commit.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Existing Vulgata solution layout: `src/dotnet/<Project>/`, `tests/Vulgata.Tests/`
- Core: `src/dotnet/Vulgata.Core/` (entities, domain services, ports — no EF deps)
- Infrastructure: `src/dotnet/Vulgata.Infrastructure/` (EF, configurations, repositories, migrations)
- Web: `src/dotnet/Vulgata.Web/` (DI composition root)
- Tests: `tests/Vulgata.Tests/` (xUnit + SQLite relational integration)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm Story 11.1 boundary is intact and no new packages are needed.

- [X] T001 Verify Story 11.1 boundary: build `src/dotnet/Vulgata.Web/Vulgata.Web.csproj` clean and confirm baseline migration `20260731041654_AddMinimalKnowledgeStore` applies; document the boundary commit `f23f6ee38d1dc6602dde43d4e2a1b4aa54e03b4d` in the implementation log
- [X] T002 Confirm no new NuGet packages are required (existing EF Core 10.0.9, Npgsql 10.0.3, System.Text.Json, xUnit, SQLite cover all Story 11.2 needs per plan.md Technical Context)

---

## Phase 2: Foundational (Blocking Prerequisites) — G1 Typed Contracts + Ports + Entity Skeleton

**Purpose**: Core domain types, port interfaces, and the `ScanExecution` entity skeleton that EVERY user story depends on. This phase implements TDD group G1 (closed typed contracts) and the structural prerequisites for G2–G5.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### G1 Tests (RED first)

- [X] T003 [P] Write failing contract tests for `WorkerAgentJobKind` closed allowlist in `tests/Vulgata.Tests/SequentialScanRunnerContractTests.cs` — assert only `Pass1CodeLogicDocument` is defined; unknown kinds are rejected before dispatch
- [X] T004 [P] Write failing contract tests for `WorkerAgentJobPacket` factory in `tests/Vulgata.Tests/SequentialScanRunnerContractTests.cs` — assert finite UTF-8/count limits (FilePath ≤2048, SymbolName ≤512, ProfiledRevision lowercase Git OID ≤64); sensitive-content guard rejects absolute host paths, secrets, raw-source carriers outside accepted anchors, unbounded diagnostics; rejects unauthorized Repository identity; rejects source text; rejects arbitrary payload/`byte[]`/`JsonElement`
- [X] T005 [P] Write failing contract tests for `WorkerAgentResultEnvelope` factory in `tests/Vulgata.Tests/SequentialScanRunnerContractTests.cs` — assert closed `Outcome` enum (`Valid`, `SchemaInvalid`, `Error`, `Unbounded`); `BoundedReason` required on non-`Valid`, optional on `Valid`, Chinese ≤512 UTF-8 bytes, non-sensitive; `ReceivedAt >= DispatchedAt`; packet/result identity match (`JobId`, `ScanExecutionId`, `Kind`); sensitive-content guard on every string; `Payload` present only on `Valid`
- [X] T006 [P] Write failing contract tests for `WorkerAgentResultPayload` closed allowlist in `tests/Vulgata.Tests/SequentialScanRunnerContractTests.cs` — assert ad-hoc fields are rejected; shape validation only (Story 11.2 does not consume payload fields)
- [X] T007 [P] Write failing contract tests for deterministic packet ordering in `tests/Vulgata.Tests/SequentialScanRunnerContractTests.cs` — assert packets are ordered by `FilePath` (ordinal), then `LineStart`, then stable `CodeUnitId`

### G1 Implementation (GREEN)

- [X] T008 [P] Create `WorkerAgentJobKind` closed enum in `src/dotnet/Vulgata.Core/DomainServices/WorkerAgentJobKind.cs` — single value `Pass1CodeLogicDocument` for Story 11.2
- [X] T009 [P] Create `WorkerAgentJobPacket` immutable Core type with static factory `Create` in `src/dotnet/Vulgata.Core/DomainServices/WorkerAgentJobPacket.cs` — validate all invariants per `contracts/worker-agent-job-packet.md`: closed `BoundedSourceSlice` allowlist (FilePath, LineStart, LineEnd, SymbolName, UnitType, Language, SourceHash, FileIdentityHash); Form-C normalization; Story 11.1 sensitive-content guard; throw `ArgumentException` on rejection (rejected value does not cross the contract)
- [X] T010 [P] Create `WorkerAgentResultEnvelope` immutable Core type with static factory `Create` in `src/dotnet/Vulgata.Core/DomainServices/WorkerAgentResultEnvelope.cs` — validate per `contracts/worker-agent-result-envelope.md`: closed `Outcome`, identity match, `ReceivedAt >= DispatchedAt`, sensitive-content guard
- [X] T011 [P] Create `WorkerAgentResultPayload` closed allowlist Core type in `src/dotnet/Vulgata.Core/DomainServices/WorkerAgentResultPayload.cs` — reserved for Story 11.3; shape-only validation in Story 11.2
- [X] T012 Verify G1 GREEN: run `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter FullyQualifiedName~SequentialScanRunnerContractTests` and confirm all G1 tests pass

### Entity skeleton + ports (prerequisite for G2–G5)

- [X] T013 [P] Create `ScanExecutionPhase` closed enum in `src/dotnet/Vulgata.Core/Entities/ScanExecutionPhase.cs` — values: `Pass1Planning`, `Pass1Running`, `Pass1Completed`, `Completed`, `Failed`, `Cancelled`
- [X] T014 [P] Create `ScanExecutionFailureKind` closed enum in `src/dotnet/Vulgata.Core/Entities/ScanExecutionFailureKind.cs` — values: `SchemaInvalid`, `Error`, `Unbounded`, `MissingPrerequisite`, `DuplicateActiveScan`, `MissingCodeUnit`, `ForbiddenContent`, `OverLimitPacket`
- [X] T015 [P] Create `ScanExecution` Core entity in `src/dotnet/Vulgata.Core/Entities/ScanExecution.cs` — fields per `data-model.md` (Id, RepositoryId, SystemId, ScanRunId, PreScanResultId, ProfiledRevision, Phase, InFlightJobId, CompletedJobCount, TotalJobCount, RestartIndicator, TerminalMessage, TerminalReasonKind, StartedAt, TerminalAt); `MaxRestartIndicator` constant = 32; phase transition methods (`Start`, `Plan`, `Advance`, `RecordValid`, `RecordFailure`, `Finalize`, `Cancel`) enforcing the state machine; private constructor; no EF annotations
- [X] T016 [P] Create `ScanExecutionStateView` Core read model in `src/dotnet/Vulgata.Core/DomainServices/ScanExecutionStateView.cs` — carries Phase, InFlightJobId, CompletedJobCount, TotalJobCount, RestartIndicator, TerminalMessage, TerminalReasonKind, StartedAt, TerminalAt
- [X] T017 [P] Create `IApprovedPreScanHandoffSource` port in `src/dotnet/Vulgata.Core/DomainServices/IApprovedPreScanHandoffSource.cs` — `Task<ApprovedPreScanHandoff?> GetAsync(Guid repositoryId, Guid scanRunId, CancellationToken)` per `contracts/sequential-scan-runner.md`
- [X] T018 [P] Create `IScanExecutionStore` port in `src/dotnet/Vulgata.Core/DomainServices/IScanExecutionStore.cs` — `GetAsync`, `FindActiveAsync`, `PersistAsync`, `ReadStateAsync` per `contracts/sequential-scan-runner.md`
- [X] T019 [P] Create `ICodeUnitSource` port in `src/dotnet/Vulgata.Core/DomainServices/ICodeUnitSource.cs` — `Task<IReadOnlyList<CodeUnit>> GetPreparedAsync(Guid scanRunId, Guid repositoryId, CancellationToken)` per `contracts/sequential-scan-runner.md`
- [X] T020 [P] Create `IWorkerAgentDispatcher` port in `src/dotnet/Vulgata.Core/DomainServices/IWorkerAgentDispatcher.cs` — `Task<WorkerAgentResultEnvelope> DispatchAsync(WorkerAgentJobPacket packet, CancellationToken)` per `contracts/sequential-scan-runner.md` and plan.md §2

**Checkpoint**: Foundation ready — G1 contracts GREEN, `ScanExecution` entity skeleton + all ports defined. User story implementation can now begin.

---

## Phase 3: User Story 1 — Start and Refusal (Priority: P1) 🎯 MVP — G2

**Goal**: A SystemOwner starts a deadline Scan from an approved Pre-Scan Handoff; the runner creates exactly one durable `ScanExecution` in `Pass1Planning` with exact identity links, or refuses with a bounded Chinese reason.

**Independent Test**: Prepare one approved Pre-Scan Handoff and verify the runner creates exactly one `ScanExecution` in `Pass1Planning` with correct identity links, and that duplicate/unapproved/missing-revision starts are refused. Run: `dotnet test --filter FullyQualifiedName~SequentialScanRunnerStartTests`

### G2 Tests (RED first)

- [X] T021 [P] [US1] Write failing test: valid start from approved handoff creates exactly one `ScanExecution` in `Pass1Planning` with exact identity links (ScanRun, Repository, System, PreScanResult) in `tests/Vulgata.Tests/SequentialScanRunnerStartTests.cs`
- [X] T022 [P] [US1] Write failing test: refusal with `MissingPrerequisite` when handoff is missing, not approved, or missing Profiled Revision in `tests/Vulgata.Tests/SequentialScanRunnerStartTests.cs`
- [X] T023 [P] [US1] Write failing test: refusal with `DuplicateActiveScan` when an active `ScanExecution` already exists for the Repository/ScanRun in `tests/Vulgata.Tests/SequentialScanRunnerStartTests.cs`
- [X] T024 [P] [US1] Write failing test: no concurrency, lease, replay, failover, retry, or multi-Repository scheduling is introduced on start (negative assertion) in `tests/Vulgata.Tests/SequentialScanRunnerStartTests.cs`

### G2 Implementation (GREEN)

- [X] T025 [US1] Create `SequentialScanRunner` Core domain service in `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs` — implement `StartAsync(repositoryId, scanRunId, systemId, cancellationToken)`: load handoff via `IApprovedPreScanHandoffSource`; refuse `MissingPrerequisite` if missing/not-approved/missing-revision; refuse `DuplicateActiveScan` via `IScanExecutionStore.FindActiveAsync`; on success create one `ScanExecution` via `ScanExecution.Start`, persist atomically via `IScanExecutionStore.PersistAsync`, return its `Id`. Inject `TimeProvider` for deterministic timestamps.
- [X] T026 [US1] Verify G2 GREEN: run `dotnet test --filter FullyQualifiedName~SequentialScanRunnerStartTests` and confirm all G2 tests pass

**Checkpoint**: User Story 1 fully functional — a deadline Scan can be started from an approved Pre-Scan and refusals are bounded.

---

## Phase 4: User Story 2 — Sequential Typed Dispatch (Priority: P1) — G3 (dispatch half)

**Goal**: The runner advances from `Pass1Planning` to `Pass1Running`, materializes one bounded typed job packet per prepared Code Unit, and dispatches them one at a time against `IWorkerAgentDispatcher` in deterministic order.

**Independent Test**: With a fake/test dispatcher that records the sequence, verify ordering, bounded content, and one-at-a-time dispatch without any LLM call. Run: `dotnet test --filter FullyQualifiedName~SequentialScanRunnerDispatchTests`

### G3 Dispatch Tests (RED first)

- [X] T027 [P] [US2] Write failing test: `RunPass1Async` transitions `Pass1Planning` → `Pass1Running` and sets `TotalJobCount` from prepared Code Units in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs`
- [X] T028 [P] [US2] Write failing test: one `WorkerAgentJobPacket` is constructed per prepared Code Unit in deterministic order (FilePath ordinal, then LineStart, then CodeUnitId) in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs`
- [X] T029 [P] [US2] Write failing test: packets are dispatched strictly one at a time through `IWorkerAgentDispatcher` (fake dispatcher records sequence; no `Task.WhenAll`, no concurrency) in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs`
- [X] T030 [P] [US2] Write failing test: each packet names Worker Agent kind, bounded source slice, exact Code Unit identity, and Profiled Revision; carries no unauthorized Repository, secret, absolute host path, source text, or unbounded diagnostic in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs`
- [X] T031 [P] [US2] Write failing test: on `Valid` result, `RecordValid` clears `InFlightJobId` and increments `CompletedJobCount`; `InFlightJobId` is set during dispatch and persisted in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs`
- [X] T032 [P] [US2] Write failing test: no concurrency/lease/replay/failover/retry/multi-Repository scheduling is observable (negative assertion) in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs`

### G3 Dispatch Implementation (GREEN)

- [X] T033 [US2] Implement `SequentialScanRunner.RunPass1Async(scanExecutionId, cancellationToken)` in `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs` — load `ScanExecution`; require `Phase == Pass1Planning`; transition to `Pass1Running` via `ScanExecution.Plan` (sets `TotalJobCount`); load prepared Code Units via `ICodeUnitSource.GetPreparedAsync` in deterministic order; for each: construct `WorkerAgentJobPacket`, `ScanExecution.Advance` (set `InFlightJobId`), persist, dispatch through `IWorkerAgentDispatcher`, on `Valid` call `ScanExecution.RecordValid` and persist. No concurrency/lease/replay/failover/retry.
- [X] T034 [US2] Create fake/test `IWorkerAgentDispatcher` recording harness in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs` — records packet sequence; configurable outcome per packet; no LLM call
- [X] T035 [US2] Verify G3 dispatch GREEN: run `dotnet test --filter FullyQualifiedName~SequentialScanRunnerDispatchTests` and confirm all dispatch tests pass

**Checkpoint**: User Story 2 functional — typed jobs dispatched sequentially in deterministic order.

---

## Phase 5: User Story 5 — Deterministic Completion (Priority: P1) — G3 (completion half)

**Goal**: When every prepared typed job has returned a schema-validated result, the runner finalizes through `Pass1Completed` to `Completed` with a bounded Chinese success message, exact job count, and terminal timestamp.

**Independent Test**: With a fake dispatcher returning `Valid` for every Code Unit, verify the terminal phase sequence, exact job count, and Chinese success message. Run: `dotnet test --filter FullyQualifiedName~SequentialScanRunnerDispatchTests`

### G3 Completion Tests (RED first)

- [X] T036 [P] [US5] Write failing test: when `CompletedJobCount == TotalJobCount`, runner transitions through `Pass1Completed` to `Completed` in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs`
- [X] T037 [P] [US5] Write failing test: terminal `TerminalMessage` is bounded Chinese, non-sensitive, and carries the exact count of dispatched jobs in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs`
- [X] T038 [P] [US5] Write failing test: `TerminalAt >= StartedAt` and terminal timestamp is set in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs`
- [X] T039 [P] [US5] Write failing test: no deferred Pass 2 flow planning, flow-group clustering, Cross-Repository Resolution, retry, or failover behavior is claimed or executed (negative assertion) in `tests/Vulgata.Tests/SequentialScanRunnerDispatchTests.cs`

### G3 Completion Implementation (GREEN)

- [X] T040 [US5] Implement `SequentialScanRunner` completion path in `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs` — when `CompletedJobCount == TotalJobCount` after `RecordValid`, call `ScanExecution.Finalize` (transition `Pass1Running` → `Pass1Completed` → `Completed`) with bounded Chinese success message and exact job count; persist atomically
- [X] T041 [US5] Verify G3 completion GREEN: run `dotnet test --filter FullyQualifiedName~SequentialScanRunnerDispatchTests` and confirm all completion tests pass

**Checkpoint**: User Story 5 functional — deterministic completion closes the sequential dispatch loop.

---

## Phase 6: User Story 3 — Durable State Across Restart (Priority: P1) — G4

**Goal**: A `ScanExecution` in `Pass1Running` with one typed job in flight survives a process restart; the durable state reports phase, in-flight job, completed count, and a bounded restart indicator. The runner does not silently resume LLM work or fabricate a result.

**Independent Test**: Simulate a restart against persisted state and verify the reported phase, in-flight job, and completed count are correct and that no LLM work is silently resumed. Run: `dotnet test --filter FullyQualifiedName~SequentialScanRunnerRestartTests`

### G4 Tests (RED first)

- [X] T042 [P] [US3] Write failing test: persisted state reports current phase, in-flight job identity, and completed count after a transition in `tests/Vulgata.Tests/SequentialScanRunnerRestartTests.cs`
- [X] T043 [P] [US3] Write failing test: on restart re-read of a `Pass1Running` row with non-null `InFlightJobId`, `RestartIndicator` increments (bounded 0–32) and persists in `tests/Vulgata.Tests/SequentialScanRunnerRestartTests.cs`
- [X] T044 [P] [US3] Write failing test: restart does NOT silently resume LLM work or fabricate a result in `tests/Vulgata.Tests/SequentialScanRunnerRestartTests.cs`
- [X] T045 [P] [US3] Write failing test: `ReadStateAsync` distinguishes `Pass1Running` from `Completed`, `Failed`, and `Cancelled` using persisted state alone in `tests/Vulgata.Tests/SequentialScanRunnerRestartTests.cs`
- [X] T046 [P] [US3] Write failing test: restart indicator stays bounded across multiple restarts while a job is in flight; never overflows in `tests/Vulgata.Tests/SequentialScanRunnerRestartTests.cs`
- [X] T047 [P] [US3] Write failing test: every phase transition is persisted atomically (SQLite relational integration — serializable transaction, no partial state visible) in `tests/Vulgata.Tests/SequentialScanRunnerRestartTests.cs`

### G4 Implementation (GREEN)

- [X] T048 [US3] Create `ScanExecutionRepository` in `src/dotnet/Vulgata.Infrastructure/Data/ScanExecutionRepository.cs` — implements `IScanExecutionStore` using EF Core against `VulgataDbContext`; persists `ScanExecution` rows in schema `vulgata` table `ScanExecutions`; every phase transition is one atomic transaction (row locked inside transaction before mutation); composite FKs bind `(ScanRunId, RepositoryId)` to `ScanRun` alternate key and `RepositoryId` to `Repository`. For SQLite tests: no `FOR UPDATE`; begin serializable transaction with parameterized no-op update mirroring Story 11.1 pattern. [NOTE: DbSet declaration and migration deferred to G6; adapter compiles via `Set<ScanExecution>()` and is exercised by G6 integration tests.]
- [X] T049 [US3] Implement `SequentialScanRunner.ReadStateAsync(scanExecutionId, cancellationToken)` in `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs` — return `ScanExecutionStateView` carrying phase, in-flight job, completed count, total count, restart indicator, terminal message. On restart re-read of `Pass1Running` with non-null `InFlightJobId`, increment bounded `RestartIndicator` and persist, but do NOT resume LLM job and do NOT fabricate a result.
- [X] T050 [US3] Create `ApprovedPreScanHandoffSource` Infrastructure adapter in `src/dotnet/Vulgata.Infrastructure/Data/ApprovedPreScanHandoffSource.cs` — implements `IApprovedPreScanHandoffSource` by wrapping existing `IScanRunRepository.GetApprovedPreScanHandoffAsync`
- [X] T051 [US3] Create `CodeUnitSource` Infrastructure adapter in `src/dotnet/Vulgata.Infrastructure/Data/CodeUnitSource.cs` — implements `ICodeUnitSource.GetPreparedAsync` returning prepared Code Units for the ScanRun/Repository in deterministic order
- [X] T052 [US3] Verify G4 GREEN: run `dotnet test --filter FullyQualifiedName~SequentialScanRunnerRestartTests` and confirm all G4 tests pass

**Checkpoint**: User Story 3 functional — durable state survives restart without silent LLM resume.

---

## Phase 7: User Story 4 — Failure Isolation (Priority: P1) — G5

**Goal**: On schema-invalid, errored, or unbounded Worker Agent results, the runner transitions to `Failed` with a bounded Chinese message naming the failing Repository. No Document Version is committed, no Story 11.1 invariant is weakened, and no partial state is visible.

**Independent Test**: With a fake dispatcher returning schema-invalid, errored, and unbounded results, verify the Scan fails with a Chinese message and no Document Version is committed. Run: `dotnet test --filter FullyQualifiedName~SequentialScanRunnerFailureTests`

### G5 Tests (RED first)

- [X] T053 [P] [US4] Write failing test: `SchemaInvalid` result → `Failed` with Chinese `TerminalMessage` naming the failing Repository and `TerminalReasonKind.SchemaInvalid` in `tests/Vulgata.Tests/SequentialScanRunnerFailureTests.cs`
- [X] T054 [P] [US4] Write failing test: `Error` result → `Failed` with `TerminalReasonKind.Error` in `tests/Vulgata.Tests/SequentialScanRunnerFailureTests.cs`
- [X] T055 [P] [US4] Write failing test: `Unbounded` result → `Failed` with `TerminalReasonKind.Unbounded` in `tests/Vulgata.Tests/SequentialScanRunnerFailureTests.cs`
- [X] T056 [P] [US4] Write failing test: on failure, no Document Version is committed (runner never calls `IKnowledgeStoreRepository`) in `tests/Vulgata.Tests/SequentialScanRunnerFailureTests.cs`
- [X] T057 [P] [US4] Write failing test: on failure, no partial `ScanExecution` state is visible (atomic transaction) in `tests/Vulgata.Tests/SequentialScanRunnerFailureTests.cs`
- [X] T058 [P] [US4] Write failing test: boundary rejection — missing Code Unit → `Failed` with `TerminalReasonKind.MissingCodeUnit` in `tests/Vulgata.Tests/SequentialScanRunnerFailureTests.cs`
- [X] T059 [P] [US4] Write failing test: boundary rejection — oversized packet → `Failed` with `TerminalReasonKind.OverLimitPacket` in `tests/Vulgata.Tests/SequentialScanRunnerFailureTests.cs`
- [X] T060 [P] [US4] Write failing test: boundary rejection — forbidden content → `Failed` with `TerminalReasonKind.ForbiddenContent` in `tests/Vulgata.Tests/SequentialScanRunnerFailureTests.cs`
- [X] T061 [P] [US4] Write failing test: no Story 11.1 knowledge-store invariant is weakened (negative assertion — Story 11.1 rows untouched) in `tests/Vulgata.Tests/SequentialScanRunnerFailureTests.cs`

### G5 Implementation (GREEN)

- [X] T062 [US4] Implement `SequentialScanRunner.RecordFailure` path in `src/dotnet/Vulgata.Core/DomainServices/SequentialScanRunner.cs` — on `SchemaInvalid`/`Error`/`Unbounded`: call `ScanExecution.RecordFailure` (transition to `Failed`, set bounded Chinese `TerminalMessage` naming the failing Repository, set `TerminalReasonKind`, clear `InFlightJobId`), persist atomically, do NOT call `IKnowledgeStoreRepository`, do NOT commit any Document Version. Boundary rejection (missing Code Unit, oversized packet, forbidden content) enforced before dispatch via `WorkerAgentJobPacket.Create` factory; runner records corresponding `TerminalReasonKind` and fails the Scan.
- [X] T063 [US4] Verify `ScanExecution.RecordFailure` transition enforces: only from `Pass1Running`, sets terminal fields, clears `InFlightJobId`, terminal phases are immutable in `src/dotnet/Vulgata.Core/Entities/ScanExecution.cs`
- [X] T064 [US4] Verify G5 GREEN: run `dotnet test --filter FullyQualifiedName~SequentialScanRunnerFailureTests` and confirm all G5 tests pass

**Checkpoint**: User Story 4 functional — failure isolation protects the knowledge store and leaves no partial state.

---

## Phase 8: Migration (Integration-Owner-Gated) — G6

**Purpose**: Add the additive `ScanExecutions` migration, EF configuration, and one-line DI registration. All tasks in this phase are **[INTEGRATION-OWNER-GATED]** and must be coordinated with the integration owner before commit.

**⚠️ CRITICAL**: Do NOT modify Story 11.1 migration history. The migration is additive only. Rollback baseline is `20260731041654_AddMinimalKnowledgeStore`.

### G6 Tests (RED first)

- [X] T065 [P] Write failing test: migration applies from a clean database in `tests/Vulgata.Tests/SequentialScanRunnerMigrationTests.cs`
- [X] T066 [P] Write failing test: migration rolls back to `20260731041654_AddMinimalKnowledgeStore` with Story 11.1 tables remaining and `ScanExecutions` disappearing in `tests/Vulgata.Tests/SequentialScanRunnerMigrationTests.cs`
- [X] T067 [P] Write failing test: migration reapplies with constraints/indexes present (`CK_ScanExecutions_Phase_Closed`, `CK_ScanExecutions_TerminalReasonKind_Closed`, `CHECK (0 <= "RestartIndicator" AND "RestartIndicator" <= 32)`) in `tests/Vulgata.Tests/SequentialScanRunnerMigrationTests.cs`
- [X] T068 [P] Write failing test: `dotnet ef migrations has-pending-model-changes` exits 0 (no pending model changes) in `tests/Vulgata.Tests/SequentialScanRunnerMigrationTests.cs`
- [X] T069 [P] Write failing test: operation allowlist — FKs are `Restrict`/`NoAction`, no domain delete method, alternate keys `(Id, RepositoryId)` and `(ScanRunId, RepositoryId)`, indexes by Repository/ScanRun/Phase in `tests/Vulgata.Tests/SequentialScanRunnerMigrationTests.cs`

### G6 Implementation (GREEN) — [INTEGRATION-OWNER-GATED]

- [X] T070 [INTEGRATION-OWNER-GATED] Create `ScanExecutionConfiguration` in `src/dotnet/Vulgata.Infrastructure/Data/Configurations/ScanExecutionConfiguration.cs` — `IEntityTypeConfiguration<ScanExecution>`: table `ScanExecutions` schema `vulgata`; PK `Id`; alternate keys `(Id, RepositoryId)` and `(ScanRunId, RepositoryId)`; `Phase` HasConversion<string> MaxLength(32) with `CK_ScanExecutions_Phase_Closed`; `TerminalReasonKind` HasConversion<string> MaxLength(32) with `CK_ScanExecutions_TerminalReasonKind_Closed`; `RestartIndicator` CHECK (0–32); `TerminalMessage` MaxLength(512); `ProfiledRevision` MaxLength(64); FKs to `Repository` and composite FK to `ScanRun` alternate key, all `Restrict`/`NoAction`; indexes by RepositoryId, ScanRunId, Phase. Follow `ScanRunConfiguration.cs` structural pattern.
- [X] T071 [INTEGRATION-OWNER-GATED] Add one `DbSet<ScanExecution>` and schema registration to `src/dotnet/Vulgata.Infrastructure/Data/VulgataDbContext.cs` — coordinate with integration owner before commit; no other changes to this file
- [X] T072 [INTEGRATION-OWNER-GATED] Generate additive EF migration `*_AddSequentialScanRunner` via `dotnet ef migrations add AddSequentialScanRunner --project src/dotnet/Vulgata.Infrastructure --startup-project src/dotnet/Vulgata.Web` — verify it is additive only and does not touch Story 11.1 migration history; files: `src/dotnet/Vulgata.Infrastructure/Data/Migrations/*_AddSequentialScanRunner.cs`, `*_AddSequentialScanRunner.Designer.cs`, `VulgataDbContextModelSnapshot.cs`
- [X] T073 [INTEGRATION-OWNER-GATED] Add one-line DI registration for `SequentialScanRunner` and its Infrastructure adapters (`ApprovedPreScanHandoffSource`, `CodeUnitSource`, `ScanExecutionRepository`) to `src/dotnet/Vulgata.Web/Program.cs` — coordinate with integration owner before commit; no other changes to this file
- [X] T074 Verify G6 GREEN: run `dotnet test --filter FullyQualifiedName~SequentialScanRunnerMigrationTests` and confirm all G6 tests pass
- [X] T075 [INTEGRATION-OWNER-GATED] Update `docs/bmad/implementation-artifacts/sprint-status.yaml` story 11-2 status to `done` after all groups GREEN — coordinate with integration owner before commit

**Checkpoint**: Migration complete — `ScanExecutions` table additive, Story 11.1 history intact, DI registered.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Full regression, Web build, and quickstart validation across all stories.

- [ ] T076 [P] Run full regression: `dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --no-restore --filter FullyQualifiedName!~CodeGraphProviderContractTests&FullyQualifiedName!~KnowledgeStorePostgreSqlConcurrencyTests` and confirm all green
- [ ] T077 [P] Run Web build: `dotnet build src/dotnet/Vulgata.Web/Vulgata.Web.csproj --no-restore` and confirm zero errors (existing package-source/EF-version warnings may remain)
- [ ] T078 Run all 6 quickstart scenarios from `specs/005-sequential-scan-runner/quickstart.md` and confirm expected outcomes
- [ ] T079 Verify negative assertions across G1–G5: no public API, UI, Chat Agent tool, Scan parsing strategy, Pass 2 flow planning, flow-group clustering, candidate selection, dispatch-site/endpoint binding, general Cross-Repository Resolution, or Story 11.3 document-generation behavior was introduced
- [ ] T080 Verify no failing test is committed across the story (SC-006)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **US1 (Phase 3, G2)**: Depends on Foundational (G1 contracts + `ScanExecution` entity + ports)
- **US2 (Phase 4, G3 dispatch)**: Depends on US1 (`StartAsync` must exist before `RunPass1Async`)
- **US5 (Phase 5, G3 completion)**: Depends on US2 (dispatch loop must exist before completion path)
- **US3 (Phase 6, G4)**: Depends on US1 + US2 (`ScanExecution` entity and dispatch behavior must exist before durable persistence/restart)
- **US4 (Phase 7, G5)**: Depends on US2 (dispatch must exist before failure path)
- **Migration (Phase 8, G6)**: Depends on US3 (`ScanExecutionRepository` must exist before migration); INTEGRATION-OWNER-GATED
- **Polish (Phase 9)**: Depends on all user stories + Migration being complete

### User Story Dependencies

- **US1 (P1)**: Foundational → US1. No dependencies on other stories.
- **US2 (P1)**: US1 → US2. Requires `StartAsync` to create the `ScanExecution` that `RunPass1Async` advances.
- **US5 (P1)**: US2 → US5. Completion is the terminal path of the dispatch loop.
- **US3 (P1)**: US1 + US2 → US3. Durable persistence wraps the entity and transitions already exercised by US1/US2.
- **US4 (P1)**: US2 → US4. Failure isolation is the failure branch of the dispatch loop.
- **Migration (G6)**: US3 → Migration. The EF configuration requires the repository and entity to be final.

### Within Each User Story

- Tests (RED) MUST be written and FAIL before implementation
- Entity/contract methods before service methods
- Service methods before Infrastructure adapters
- Each group reaches GREEN before the next group begins
- Commit after each GREEN group

### Parallel Opportunities

- All G1 contract test tasks (T003–T007) can run in parallel (different test sections, same file — coordinate writes)
- All G1 contract implementation tasks (T008–T011) can run in parallel (different files)
- All entity/port skeleton tasks (T013–T020) can run in parallel (different files)
- All G2 test tasks (T021–T024) can run in parallel
- All G3 dispatch test tasks (T027–T032) can run in parallel
- All G4 test tasks (T042–T047) can run in parallel
- All G5 test tasks (T053–T061) can run in parallel
- All G6 test tasks (T065–T069) can run in parallel
- Polish tasks T076 and T077 can run in parallel

---

## Parallel Example: Foundational Phase (G1)

```bash
# Launch all G1 contract tests together (coordinate file writes):
Task: "Contract test for WorkerAgentJobKind in SequentialScanRunnerContractTests.cs"
Task: "Contract test for WorkerAgentJobPacket in SequentialScanRunnerContractTests.cs"
Task: "Contract test for WorkerAgentResultEnvelope in SequentialScanRunnerContractTests.cs"
Task: "Contract test for WorkerAgentResultPayload in SequentialScanRunnerContractTests.cs"

# Launch all G1 contract implementations together (different files):
Task: "Create WorkerAgentJobKind in WorkerAgentJobKind.cs"
Task: "Create WorkerAgentJobPacket in WorkerAgentJobPacket.cs"
Task: "Create WorkerAgentResultEnvelope in WorkerAgentResultEnvelope.cs"
Task: "Create WorkerAgentResultPayload in WorkerAgentResultPayload.cs"

# Launch all entity/port skeletons together (different files):
Task: "Create ScanExecutionPhase in ScanExecutionPhase.cs"
Task: "Create ScanExecutionFailureKind in ScanExecutionFailureKind.cs"
Task: "Create ScanExecution in ScanExecution.cs"
Task: "Create ScanExecutionStateView in ScanExecutionStateView.cs"
Task: "Create IApprovedPreScanHandoffSource in IApprovedPreScanHandoffSource.cs"
Task: "Create IScanExecutionStore in IScanExecutionStore.cs"
Task: "Create ICodeUnitSource in ICodeUnitSource.cs"
Task: "Create IWorkerAgentDispatcher in IWorkerAgentDispatcher.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify boundary)
2. Complete Phase 2: Foundational (G1 contracts + entity skeleton + ports)
3. Complete Phase 3: User Story 1 (G2 — start and refusal)
4. **STOP and VALIDATE**: Test User Story 1 independently via `SequentialScanRunnerStartTests`

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 (start) → Test independently → G2 GREEN
3. Add US2 (dispatch) → Test independently → G3 dispatch GREEN
4. Add US5 (completion) → Test independently → G3 completion GREEN
5. Add US3 (durable restart) → Test independently → G4 GREEN
6. Add US4 (failure isolation) → Test independently → G5 GREEN
7. Add Migration → Test independently → G6 GREEN (INTEGRATION-OWNER-GATED)
8. Polish → Full regression + Web build → Story complete

### TDD Discipline (expedited-mvp)

Each TDD group (G1–G6) follows transient RED→GREEN:
1. Write failing tests for the group
2. Verify RED (transient failing test evidence recorded)
3. Write minimal implementation
4. Verify GREEN
5. Commit after GREEN
6. No failing test is committed

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- **[INTEGRATION-OWNER-GATED]** tasks (T070–T075) must be coordinated with the integration owner before commit; do not mutate EF migrations, `VulgataDbContext.cs`, `Program.cs`, or `sprint-status.yaml` without that coordination
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
- The runner is deterministic application code, not an LLM agent; the LLM-backed Worker Agent is Story 11.3

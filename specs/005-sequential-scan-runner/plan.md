# Implementation Plan: Sequential Scan Runner & Typed Agent Dispatch

**Branch**: `story112` | **Date**: 2026-07-31 |
**Spec**: [spec.md](spec.md) | **Execution level**: `expedited-mvp`

## Summary

Add a deterministic application-code Sequential Scan Runner that consumes
one `ApprovedPreScanHandoff` for a prepared Repository and drives one Scan
execution from `pre_scan_approved` through typed Worker Agent job dispatch,
durable phase transitions, and terminal success or failure. Add typed Agent
job contracts — a closed typed job packet and a schema-validated result
envelope — plus an `IWorkerAgentDispatcher` port. The runner dispatches one
job at a time against the port. No LLM call is executed in this story; a
fake/test dispatcher is the only execution surface. The runner persists
durable execution state atomically with phase transitions; a restart re-reads
state and surfaces the in-flight phase without silently resuming LLM work. On
any Worker Agent job failure the runner stops the Scan with a durable
Chinese user-visible message and commits no Document Version from the invalid
result, leaving Story 11.1 knowledge-store invariants intact.

This story adds no concurrency, lease, replay, failover, retry policy,
multi-Repository scheduling, public API, UI, Chat Agent tool, Scan parsing
strategy, Pass 2 flow planning, flow-group clustering, candidate selection,
dispatch-site/endpoint binding, general Cross-Repository Resolution, or Story
11.3 document-generation behavior.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)

**Primary Dependencies**: existing EF Core 10.0.9, Npgsql EF provider
10.0.3, ASP.NET Core DI, `System.Text.Json`; no new package

**Storage**: PostgreSQL 17, schema `vulgata`, through `VulgataDbContext`

**Testing**: xUnit in `tests/Vulgata.Tests`; SQLite relational integration
for fast invariant/atomicity tests plus a fake/test dispatcher

**Target Platform**: Vulgata single-container Linux deployment and Windows
development

**Project Type**: existing Blazor Web App with Core, Infrastructure, Shared,
Web, and Tests boundaries

**Performance Goals**: one bounded runner state transition per transaction;
one typed job in flight at a time; deterministic in-order dispatch

**Constraints**:

- one Repository at a time, one typed job at a time; no concurrent
  supersteps, dynamic leases, state replay, provider failover, retry
  policies, or multi-Repository scheduling;
- the runner executes no LLM call; all dispatch goes through
  `IWorkerAgentDispatcher`; a fake/test dispatcher is the only execution
  surface inside this story's tests;
- typed job packets and result envelopes are closed allowlists with finite
  UTF-8/count limits; the same sensitive-content guard Story 11.1 applies
  (no absolute host paths, secrets, raw-source carriers outside accepted
  anchors, or unbounded diagnostics) is enforced before dispatch and on
  every result;
- runner state is persisted atomically with each phase transition; a restart
  re-reads durable state and surfaces the in-flight phase without silently
  resuming LLM work or fabricating a result;
- on Worker Agent job failure (schema-invalid, errored, or unbounded result),
  the Scan transitions to `failed` with a bounded Chinese user-visible
  message naming the failing Repository, no Document Version is committed
  from the invalid result, and no Story 11.1 invariant is weakened;
- a SystemOwner-facing query using persisted state alone must distinguish
  `pass1_running` from `completed`, `failed`, and `cancelled`;
- any EF migration added for runner-execution state is additive only and
  must not modify Story 11.1 migration history; EF migrations,
  `VulgataDbContext.cs`, `Program.cs`, and `sprint-status.yaml` are
  integration-owner-gated;
- no failing test is committed.

**Scale/Scope**: one prepared Repository demonstration; one Scan execution
with one typed job per prepared Code Unit, dispatched strictly in order

## Authority Reconciliation

1. PRD section 5.16 and Epic 11 are the accepted deadline overlay. Full Epic
   5 Scan coordination remains backlog and is not claimed complete.
2. The July scanning architecture remains the direction, but Story 11.2
   implements only the sequential runner, typed job packet, schema-validated
   result envelope, `IWorkerAgentDispatcher` port, and durable runner-execution
   state. Concurrency, leases, supersteps, replay, failover, retry, Pass 2
   flow planning, flow-group clustering, candidate selection,
   dispatch-site/endpoint binding, general Cross-Repository Resolution, and
   the LLM-backed Worker Agent are deferred and tracked as limitations.
3. Story 11.1 is complete at candidate
   `f23f6ee38d1dc6602dde43d4e2a1b4aa54e03b4d`; its knowledge-store entities,
   migration, repository contract, and workflow files remain unchanged by
   this story. Story 11.1 rollback baseline is
   `20260731041654_AddMinimalKnowledgeStore`.
4. The runner is deterministic application code, not an LLM agent; the
   LLM-backed Worker Agent is Story 11.3 and is out of scope here. The typed
   Agent contract is a port, not an LLM binding; Story 11.3 supplies the
   LLM-backed implementation.
5. Existing Story 11.1 seams are reused: `ScanRun`, `PreScanResult`,
   `ApprovedPreScanHandoff`,
   `IScanRunRepository.GetApprovedPreScanHandoffAsync`, and
   `IKnowledgeStoreRepository`. The existing `AgentType` enumeration is not
   modified; a typed Worker Agent job-kind enumeration is added alongside it.
6. No `docs/adr/`, context-scoped ADR directory, or `CONTEXT-MAP.md` exists
   at this boundary. No ADR/context decision is invented; the accepted spec,
   Constitution, glossary, PRD, and July architecture are sufficient.

## Constitution Check

### Before design

| Gate | Result | Evidence |
| --- | --- | --- |
| I. Layered boundaries | PASS | Core owns the typed job/result contracts, the `IWorkerAgentDispatcher` port, and the runner-execution entity/state interfaces; Infrastructure alone owns EF, transactions, mappings, migration, and the durable runner-state repository; Web only registers the one-line runner DI. |
| II. Domain purity/language | PASS | No annotations, generic repository, `IQueryable`, or `DbSet` escape; exact Scan, Repository, Code Unit, Worker Agent, Pre-Scan, Pass 1, and Cross-Repository terms are used. |
| III. Validation/contracts | PASS | No public/API DTO is introduced. The internal typed job packet and result envelope are closed allowlists and validate UTF-8/count/path plus the Story 11.1 sensitive-content guard before dispatch and on every result; failure produces a bounded Chinese user-visible message. |
| IV. Test-first integration | PASS | Six transient RED→GREEN groups cover Core contract invariants, start/refusal, sequential dispatch, durable state across restart, failure isolation, and deterministic completion. Migration tests (if a migration is added) cover clean apply, rollback to Story 11.1 baseline, reapply. |
| V. Security/stewardship | PASS | Typed job packets carry no unauthorized Repository, secret, absolute host path, source text outside accepted anchors, or unbounded diagnostic; results are schema-validated and bounded; on failure no Document Version is committed and no Story 11.1 invariant is weakened. |
| Immutable traceable truth | PASS | Runner-execution state is append/transition-only; phase transitions are atomic and durable; no Document Version is fabricated from invalid results. |
| Deadline exception | PASS | Migration, authorization, security, test, review, E2E-decision, and verification gates remain; deferred Epic 5 families and Story 11.3 LLM behavior are absent. |

### After design

PASS. Phase 1 keeps all EF work in Infrastructure, adds one specific
runner-state repository, uses owner-safe composite foreign keys to the
existing `ScanRun`/`Repository`, persists phase transitions atomically,
fails closed on invalid results, and keeps the public/API, Agent-tool, Scan
parsing, and deferred binding boundaries unchanged. The Complexity Tracking
table is unnecessary.

## Design

### 1. Typed Worker Agent job contracts (Core)

A closed `WorkerAgentJobKind` enumeration names the typed job kinds the
deadline runner dispatches. For Story 11.2 only `Pass1CodeLogicDocument` is
defined; Story 11.3 may add `Pass2BusinessLogicDocument`. The enumeration is
closed: an unknown kind is rejected before dispatch.

`WorkerAgentJobPacket` is an immutable Core record/class with exactly:

- `JobId` (Guid, non-empty, runner-assigned);
- `Kind` (`WorkerAgentJobKind`, closed);
- `ScanExecutionId` (Guid, non-empty, links to the durable Scan execution
  record);
- `RepositoryId`, `SystemId`, `ScanRunId` (Guid, non-empty, exact identity
  from the approved handoff);
- `CodeUnitId` (Guid, non-empty, exact Code Unit identity);
- `ProfiledRevision` (string, bounded lowercase Git object ID, copied from
  the approved handoff);
- `BoundedSourceSlice` (closed allowlist: `FilePath` repository-relative,
  `LineStart`, `LineEnd`, `SymbolName`, `UnitType`, `Language`, `SourceHash`,
  `FileIdentityHash` — exact Code Unit identity, no source text);
- finite UTF-8/count limits on every string;
- the Story 11.1 sensitive-content guard applied to every string before
  construction: no absolute host paths, secrets, raw-source carriers outside
  accepted anchors, or unbounded diagnostics.

`WorkerAgentResultEnvelope` is an immutable Core record/class with exactly:

- `JobId`, `ScanExecutionId` (Guid, must match the dispatched packet);
- `Kind` (`WorkerAgentJobKind`, must match);
- `Outcome` (`Valid`, `SchemaInvalid`, `Error`, `Unbounded`);
- `BoundedReason` (Chinese user-visible string, finite length, non-sensitive,
  present on every non-`Valid` outcome and optional on `Valid`);
- `DispatchedAt`, `ReceivedAt` (`DateTimeOffset`, `ReceivedAt >=
  DispatchedAt`);
- on `Valid` only, a closed `WorkerAgentResultPayload` allowlist reserved for
  Story 11.3 (no payload field is consumed by Story 11.2; the runner
  validates shape only);
- the Story 11.1 sensitive-content guard applied to every string.

Both contracts are constructed through static factory methods that validate
every invariant and return either the instance or throw a closed
`ArgumentException`; rejected values and provider diagnostics do not cross
the contract. This is the testable boundary; the plan does not claim semantic
detection of every possible secret.

### 2. `IWorkerAgentDispatcher` port (Core)

```text
public interface IWorkerAgentDispatcher
{
    Task<WorkerAgentResultEnvelope> DispatchAsync(
        WorkerAgentJobPacket packet,
        CancellationToken cancellationToken = default);
}
```

Story 11.2 ships only a fake/test dispatcher in Tests. Story 11.3 supplies
the LLM-backed implementation. The runner depends on the port, never on a
concrete LLM client.

### 3. Durable Scan execution record and phase transitions (Core + Infrastructure)

A new `ScanExecution` Core entity owns one deadline Scan run. Fields:

- `Id` (Guid, runner-assigned);
- `RepositoryId`, `SystemId`, `ScanRunId`, `PreScanResultId` (Guid, exact
  identity from the approved handoff);
- `ProfiledRevision` (string, bounded, from the handoff);
- `Phase` (`ScanExecutionPhase`: `Pass1Planning`, `Pass1Running`,
  `Pass1Completed`, `Completed`, `Failed`, `Cancelled`);
- `InFlightJobId` (nullable Guid, the currently dispatched job, null when
  idle);
- `CompletedJobCount` (int, non-negative, the count of jobs that returned a
  `Valid` result);
- `TotalJobCount` (int, non-negative, set at planning time from the prepared
  Code Units);
- `RestartIndicator` (int, bounded 0–`MaxRestartIndicator`, increments on a
  restart re-read only when a job was in flight; never overflows);
- `TerminalMessage` (nullable string, bounded Chinese, set on terminal
  phases);
- `TerminalReasonKind` (nullable `ScanExecutionFailureKind` closed enum:
  `SchemaInvalid`, `Error`, `Unbounded`, `MissingPrerequisite`,
  `DuplicateActiveScan`, `MissingCodeUnit`, `ForbiddenContent`,
  `OverLimitPacket` — closed);
- `StartedAt`, `TerminalAt` (`DateTimeOffset`, `TerminalAt >= StartedAt`);
- alternate key `(Id, RepositoryId)` and `(ScanRunId, RepositoryId)`.

Phase transitions are enforced by the entity and atomic with persistence:

- `Start`: only from an approved handoff → `Pass1Planning`;
- `Plan`: `Pass1Planning` → `Pass1Running` (sets `TotalJobCount`);
- `Advance`: `Pass1Running` → dispatch one job, set `InFlightJobId`, persist;
- `RecordValid`: `Pass1Running` (same `InFlightJobId`) → clear
  `InFlightJobId`, increment `CompletedJobCount`, persist;
- `RecordFailure`: `Pass1Running` → `Failed` with Chinese `TerminalMessage`
  and `TerminalReasonKind`, clear `InFlightJobId`, persist;
- `Finalize`: when `CompletedJobCount == TotalJobCount` → `Pass1Completed`
  then `Completed` with Chinese success message and exact job count;
- `Cancel`: `Pass1Planning`/`Pass1Running` → `Cancelled` with Chinese
  message (no LLM resume).

`MaxRestartIndicator` is a bounded constant (e.g., 32). The restart re-read
path does not mutate phase; it only refreshes `RestartIndicator` when
`InFlightJobId` is non-null and reports the in-flight phase.

### 4. Sequential Scan Runner (Core domain service)

`SequentialScanRunner` is a Core domain service that depends on:

- `IApprovedPreScanHandoffSource` (read port; implemented by Infrastructure
  via the existing `IScanRunRepository.GetApprovedPreScanHandoffAsync`
  wrapper);
- `IScanExecutionStore` (write/read port for `ScanExecution`);
- `IWorkerAgentDispatcher`;
- `ICodeUnitSource` (read port for the prepared Code Units of the ScanRun);
- `TimeProvider` (for timestamps; injected for deterministic tests).

Behavior:

1. `StartAsync(repositoryId, scanRunId, ...)`: load the approved handoff;
   refuse if missing, not approved, missing Profiled Revision, or an active
   `ScanExecution` already exists for the Repository/ScanRun. On success,
   create exactly one `ScanExecution` in `Pass1Planning`, persist atomically.
2. `RunPass1Async(scanExecutionId, ...)`: transition to `Pass1Running`, load
   the prepared Code Units in deterministic order (by `FilePath` then
   `LineStart` then stable `Id`), set `TotalJobCount`, then for each Code
   Unit construct one `WorkerAgentJobPacket`, dispatch it through
   `IWorkerAgentDispatcher`, and on `Valid` record `RecordValid`; on
   `SchemaInvalid`/`Error`/`Unbounded` record `RecordFailure` and stop. No
   concurrency, lease, replay, failover, or retry.
3. `CompleteAsync`: when all jobs are `Valid`, finalize through
   `Pass1Completed` to `Completed`.
4. `ReadStateAsync(scanExecutionId)`: return a bounded read model
   (`ScanExecutionStateView`) carrying phase, in-flight job, completed count,
   total count, restart indicator, and terminal message — enough to
   distinguish `Pass1Running` from `Completed`/`Failed`/`Cancelled` using
   persisted state alone.

The runner never executes an LLM call. It never commits a Document Version;
Story 11.3 does that. On failure it leaves Story 11.1 invariants intact and
commits no partial state.

### 5. Durable persistence and restart behavior (Infrastructure)

`ScanExecutionRepository` in Infrastructure implements `IScanExecutionStore`
using EF Core against `VulgataDbContext`. It persists `ScanExecution` rows in
schema `vulgata` under table `ScanExecutions`. Every phase transition is one
atomic `ExecuteInTransactionAsync` retry-safe transaction; the row is locked
inside the transaction before mutation. Composite foreign keys bind
`(ScanRunId, RepositoryId)` to the existing `ScanRun` alternate key and
`RepositoryId` to `Repository`.

On a process restart, `ReadStateAsync` re-reads the durable row. If
`InFlightJobId` is non-null and `Phase == Pass1Running`, it increments
`RestartIndicator` (bounded) and persists, but does **not** resume the LLM
job and does **not** fabricate a result. The SystemOwner-facing view reports
the in-flight phase and the bounded restart indicator. A later
operator-initiated action (out of scope for Story 11.2) may cancel or
re-run; the runner itself does not silently resume.

For SQLite integration tests, the repository must not emit unsupported
`FOR UPDATE`; it begins a serializable transaction and acquires the
database-wide write reservation with a parameterized no-op update, mirroring
the Story 11.1 pattern.

### 6. Relational invariants and migration policy

The migration adds only `ScanExecutions`. All foreign keys use
`Restrict`/`NoAction`; there is no domain delete method. Owner-safe
alternate/composite keys enforce `(Id, RepositoryId)` and
`(ScanRunId, RepositoryId)` identity. `Phase` has a closed relational check
constraint `CK_ScanExecutions_Phase_Closed` permitting only the six values;
`TerminalReasonKind` has `CK_ScanExecutions_TerminalReasonKind_Closed`
permitting only the closed failure kinds. `RestartIndicator` has a
`CHECK (0 <= "RestartIndicator" AND "RestartIndicator" <= 32)`. Indexed
query paths: by Repository, by ScanRun, by Phase.

Migration verification must:

1. apply from a clean database;
2. roll back exactly to `20260731041654_AddMinimalKnowledgeStore` and prove
   Story 11.1 tables remain while `ScanExecutions` disappears;
3. reapply and prove constraints/indexes;
4. run `dotnet ef migrations has-pending-model-changes` with exit 0.

The migration, `VulgataDbContext.cs` (one `DbSet<ScanExecution>` + schema
registration), and the one-line `Program.cs` DI registration are
integration-owner-gated. Story 11.2 implementers propose the additive
migration and the one-line registrations but do not commit them without
integration-owner coordination.

### 7. Failure isolation and Story 11.1 invariant protection

On `SchemaInvalid`, `Error`, or `Unbounded`:

- transition to `Failed`, set Chinese `TerminalMessage` naming the failing
  Repository and a bounded non-sensitive reason, set `TerminalReasonKind`,
  clear `InFlightJobId`;
- do **not** call `IKnowledgeStoreRepository` and do **not** commit any
  Document Version;
- the atomic transaction ensures no partial `ScanExecution` state is visible;
- Story 11.1 knowledge-store rows are untouched; the failure does not weaken
  any Story 11.1 invariant.

Boundary rejection (missing Code Unit, oversized packet, forbidden content,
over-limit collection, unauthorized Repository) is enforced in the
`WorkerAgentJobPacket` factory before dispatch; the runner records the
corresponding `TerminalReasonKind` and fails the Scan.

## Project Structure

### Documentation

```text
specs/005-sequential-scan-runner/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── worker-agent-job-packet.md
│   ├── worker-agent-result-envelope.md
│   └── sequential-scan-runner.md
├── checklists/requirements.md
└── spec.md
```

### Planned source boundary

```text
src/dotnet/
├── Vulgata.Core/
│   ├── Entities/
│   │   ├── ScanExecution.cs
│   │   ├── ScanExecutionPhase.cs
│   │   └── ScanExecutionFailureKind.cs
│   └── DomainServices/
│       ├── WorkerAgentJobKind.cs
│       ├── WorkerAgentJobPacket.cs
│       ├── WorkerAgentResultEnvelope.cs
│       ├── WorkerAgentResultPayload.cs
│       ├── IWorkerAgentDispatcher.cs
│       ├── IScanExecutionStore.cs
│       ├── IApprovedPreScanHandoffSource.cs
│       ├── ICodeUnitSource.cs
│       ├── SequentialScanRunner.cs
│       └── ScanExecutionStateView.cs
├── Vulgata.Infrastructure/Data/
│   ├── Configurations/
│   │   └── ScanExecutionConfiguration.cs
│   ├── ScanExecutionRepository.cs
│   ├── ApprovedPreScanHandoffSource.cs
│   ├── CodeUnitSource.cs
│   ├── VulgataDbContext.cs            # one DbSet + schema registration (integration-owner-gated)
│   └── Migrations/
│       ├── *_AddSequentialScanRunner.cs        # integration-owner-gated
│       ├── *_AddSequentialScanRunner.Designer.cs
│       └── VulgataDbContextModelSnapshot.cs    # integration-owner-gated
└── Vulgata.Web/Program.cs              # one-line DI registration (integration-owner-gated)

tests/Vulgata.Tests/
├── SequentialScanRunnerContractTests.cs
├── SequentialScanRunnerStartTests.cs
├── SequentialScanRunnerDispatchTests.cs
├── SequentialScanRunnerRestartTests.cs
├── SequentialScanRunnerFailureTests.cs
└── SequentialScanRunnerMigrationTests.cs
```

**Structure Decision**: extend the existing Core/Infrastructure repository
pattern and composition root. Story 11.2 implementers do not modify Story
11.1 entities, services, migration, tests, or workflow artifacts. The
additive `ScanExecutions` migration, `VulgataDbContext` `DbSet`/schema
registration, and the one-line DI registration are the only existing
production files updated by Story 11.2, and each is integration-owner-gated.

## Production and Test Ownership

| Group | Production ownership | Test ownership / required RED |
| --- | --- | --- |
| G1 closed typed contracts | Core `WorkerAgentJobKind`, `WorkerAgentJobPacket`, `WorkerAgentResultEnvelope`, `WorkerAgentResultPayload` | `SequentialScanRunnerContractTests`: closed kind allowlist; finite UTF-8/count limits; sensitive-content rejection (absolute host path, secret, raw-source carrier, unbounded diagnostic); deterministic ordering; packet/result identity match; outcome closed enum |
| G2 start and refusal | `SequentialScanRunner.StartAsync`, `IApprovedPreScanHandoffSource` | `SequentialScanRunnerStartTests`: valid start creates one `ScanExecution` in `Pass1Planning`; refusal on missing/not-approved/missing-revision/duplicate-active; exact identity links |
| G3 sequential dispatch | `SequentialScanRunner.RunPass1Async`, `IWorkerAgentDispatcher`, `ICodeUnitSource` | `SequentialScanRunnerDispatchTests`: `Pass1Planning`→`Pass1Running`; one job per Code Unit in deterministic order; one at a time; `TotalJobCount` set; fake dispatcher records sequence; no concurrency/lease/replay/failover/retry |
| G4 durable state across restart | `ScanExecution`, `ScanExecutionRepository`, `IScanExecutionStore` | `SequentialScanRunnerRestartTests`: persisted state reports phase, in-flight job, completed count; restart increments bounded `RestartIndicator` when in flight; no silent LLM resume; `ReadStateAsync` distinguishes `Pass1Running` from `Completed`/`Failed`/`Cancelled` |
| G5 failure isolation | `SequentialScanRunner.RecordFailure`, `ScanExecution` terminal transition | `SequentialScanRunnerFailureTests`: schema-invalid/error/unbounded → `Failed` with Chinese message naming Repository; no Document Version committed; no Story 11.1 invariant weakened; no partial state visible; boundary rejection (missing Code Unit, oversized packet, forbidden content, over-limit, unauthorized Repository) |
| G6 migration | `ScanExecutionConfiguration`, `VulgataDbContext`, migration, snapshot | `SequentialScanRunnerMigrationTests`: operation allowlist, FK/delete/index/check shape including `CK_ScanExecutions_Phase_Closed`, `CK_ScanExecutions_TerminalReasonKind_Closed`, `RestartIndicator` bound; clean apply, rollback to Story 11.1 baseline, reapply; PostgreSQL commands in quickstart |

Every group records transient failing test evidence before its production
change and reaches GREEN before the next group. Full regression and Web
build follow the focused groups.

## Requirement-to-Design/Test Trace

| Requirements | Design | Tests |
| --- | --- | --- |
| FR-001, FR-002 | `StartAsync`, approved handoff load, one `ScanExecution` in `Pass1Planning`, exact identity links | G2 |
| FR-003, FR-011, FR-012 | `RunPass1Async`, sequential dispatch through `IWorkerAgentDispatcher`, no concurrency/lease/replay/failover/retry | G3 |
| FR-004 | closed `WorkerAgentJobPacket` allowlist with sensitive-content guard | G1, G3 |
| FR-005, FR-006 | `ScanExecution` entity, `ScanExecutionRepository` atomic transitions, restart re-read | G4 |
| FR-007 | `ReadStateAsync` → `ScanExecutionStateView` distinguishes phases | G4 |
| FR-008, FR-009 | `RecordFailure`, terminal Chinese message, no Document Version, Story 11.1 invariant protection | G5 |
| FR-010 | `CompleteAsync` through `Pass1Completed` to `Completed` with exact count and Chinese message | G3 |
| FR-013 | no public API/UI/Chat tool/Scan parsing/Pass 2/CRR/Story 11.3 behavior | G1–G5 (negative assertions) |
| FR-014, FR-015 | additive migration, integration-owner-gated paths | G6 |

## E2E Decision

Initial predicates:

| Predicate | Triggered | Decision / owner |
| --- | --- | --- |
| User-visible workflow | No | no UI/API behavior |
| Authentication/authorization | No | no new policy; reuses existing SystemOwner role |
| API/SignalR/persistence/multi-module | Yes | Required: Core→Infrastructure→PostgreSQL runner-state persistence; `e2e-runner` |
| EF migration/observable lifecycle | Yes | Required: additive `ScanExecutions` migration, rollback/reapply; `e2e-runner` |
| Existing complete integration coverage | No at plan boundary | no coverage override may be claimed before current-boundary passing evidence |

The economical E2E is the focused runner-state persistence, restart, and
migration integration groups in [quickstart.md](quickstart.md). A later
coverage override is allowed only if those tests cover every triggered
predicate at the pinned reviewed boundary. The clean-database two-Repository
demonstration and Docker image remain Story 11.5.

## Plan Review and Planned-Change Authorization

**Plan review: required.** The expedited exception requires independent
review because this plan establishes runner-state persistence and migration
invariants and triggers two planned-change gates.

**Repository planned-change authorization: TRIGGERED — pause before tasks.**
This plan triggers two `AGENTS.md` planned-change gates:

1. **Scan strategy — dispatch behavior.** The Sequential Scan Runner is a new
   Scan execution strategy with deterministic sequential dispatch. Although
   it introduces no concurrency, lease, replay, failover, retry, or
   multi-Repository scheduling, it is a new dispatch behavior and therefore
   requires planned-change authorization after plan-review accepts and
   before task generation.
2. **Worker Agent tool / agent execution adapter.** The typed
   `WorkerAgentJobPacket`, `WorkerAgentResultEnvelope`, and
   `IWorkerAgentDispatcher` port are a new Worker Agent tool / agent
   execution adapter contract. They do not execute an LLM call in this
   story, but they define the contract the Story 11.3 LLM-backed Worker
   Agent must implement, and therefore require planned-change authorization
   after plan-review accepts and before task generation.

The orchestrator's implementation intent: proceed to `speckit-tasks` after
both authorizations, dispatching fresh `tdd-implementer` groups G1–G6 in
order, then the combined expedited `quality-review`, then conditional E2E
(persistence + migration), then verify (Web build + full non-final-image
suite), then complete. The integration-owner-gated additive migration,
`VulgataDbContext` `DbSet`/schema registration, and one-line `Program.cs`
DI registration will be coordinated with the integration owner before
commit; the orchestrator will not mutate them without that coordination.

The authorization request is approval to proceed from the accepted plan to
task generation only. It does not waive any later TDD, review, E2E,
security, migration, or verification gate.

## Complexity Tracking

No Constitution violation or additional architectural layer is introduced.

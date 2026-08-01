---
story_key: 11-2-sequential-scan-runner-and-typed-agent-dispatch
title: "Story 11.2: Sequential Scan Runner & Typed Agent Dispatch"
Status: ready-for-dev
Epic: 11
Story: 2
created: 2026-07-31
execution_level: expedited-mvp
depends_on:
  - 11-1-minimal-knowledge-store-and-retrieval-projection
references:
  - docs/bmad/epics.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md
  - docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md
  - docs/agent-workflow/august-15-expedited-mvp-exception.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - specs/004-minimal-knowledge-store/plan.md
---

# Story 11.2: Sequential Scan Runner & Typed Agent Dispatch

## Story

As a **SystemOwner**,
I want one prepared Repository Scan to run sequentially from approved pre-scan
metadata into typed Worker Agent jobs,
so that the August 15 demonstration has a reliable execution path without
unfinished concurrency infrastructure.

## Scope and boundary

- Add a deterministic application-code Sequential Scan Runner that consumes an
  approved Pre-Scan Handoff for one prepared Repository and drives one Scan
  execution from `pre_scan_approved` through typed job dispatch, durable phase
  transitions, and terminal success or failure.
- Add typed Agent job contracts that name Worker Agent job kinds, their bounded
  packet shape, and their schema-validated result envelope, without executing
  any LLM call in this story. The runner dispatches jobs against an injected
  `IWorkerAgentDispatcher` port; the deadline LLM-backed implementation is
  Story 11.3 work.
- Process one Repository at a time, one job at a time. No concurrent
  supersteps, dynamic leases, state replay, provider failover, or
  multi-Repository scheduling.
- Persist enough runner state (phase, current job, terminal status, restart
  count) to report current phase, success, and failure after a server restart.
  No background replay service is added; a restart re-reads durable state and
  surfaces the in-flight phase without silently resuming LLM work.
- On any Worker Agent job failure, the runner stops the Scan with a durable,
  Chinese user-visible failure and commits no generated Document Version from
  the invalid result. Failure does not corrupt the Story 11.1 knowledge store.
- Reuse Story 11.1 `IKnowledgeStoreRepository` for any knowledge-store write
  this story performs. Do not add a new authorization policy, public retrieval
  API, Chat Agent tool, Scan parsing strategy, provider failover, retry
  policy, or Story 11.3 document-generation behavior.
- Defer: concurrent scans, Worker Agent leases, superstep grouping, dynamic
  dispatch, retry policies, provider failover, state replay, Pass 2 flow
  planning, flow-group clustering, candidate selection, dispatch-site/endpoint
  binding, general Cross-Repository Resolution, and the actual LLM Worker
  Agent implementation.
- Story 11.1 is complete at candidate commit
  `f23f6ee38d1dc6602dde43d4e2a1b4aa54e03b4d`. This story owns the next
  additive seam from that boundary and must not modify Story 11.1
  implementation, migration, or workflow files.

## Acceptance Criteria

### AC-1: Start a deadline Scan from an approved Pre-Scan Handoff

**Given** a Repository has a `pre_scan_approved` Pre-Scan Result and a
completed ScanRun with a bound Profiled Revision
**When** the SystemOwner starts the deadline Scan through the Sequential Scan
Runner
**Then** deterministic application code creates exactly one durable Scan
execution record in the `pass1_planning` phase, links it to the exact ScanRun,
Repository, System, and approved Pre-Scan Result, and refuses to start if any
prerequisite is missing, not approved, or already has an active Scan
**And** no concurrent superstep, lease, replay, failover, or multi-Repository
scheduling behavior is introduced.

### AC-2: Dispatch typed Worker Agent jobs sequentially

**Given** a Scan execution is in `pass1_planning` with the prepared Code Units
from the approved Pre-Scan Handoff
**When** the runner advances
**Then** it transitions to `pass1_running`, materializes one bounded typed job
packet per prepared Code Unit, and dispatches them one at a time against the
injected `IWorkerAgentDispatcher` port
**And** the typed job contract names the Worker Agent kind, the bounded source
slice, the exact Code Unit identity, the Profiled Revision, and no unauthorized
Repository, secret, absolute host path, source text outside an accepted
anchor, or unbounded diagnostic
**And** the runner does not implement concurrent supersteps, dynamic leases,
state replay, provider failover, or multi-Repository scheduling.

### AC-3: Persist durable state across a server restart

**Given** a Scan execution is in `pass1_running` with one typed job in flight
**When** the process restarts
**Then** the durable state reports the current phase, the in-flight job
identity, the count of completed jobs, and a bounded restart indicator
**And** the runner does not silently resume LLM work or fabricate a result
**And** a SystemOwner-facing query can distinguish `pass1_running` from
`completed`, `failed`, and `cancelled` using the persisted state alone.

### AC-4: Stop on Worker Agent job failure without committing invalid Documents

**Given** a Worker Agent job returns a schema-invalid result, an unhandled
error, or a result that fails the typed contract's bounded validation
**When** the Sequential Scan Runner records the failure
**Then** the Scan transitions to `failed` with a durable, Chinese user-visible
failure message that names the failing Repository and a bounded non-sensitive
reason
**And** no generated Document Version is committed from the invalid result
**And** no Story 11.1 knowledge-store invariant is weakened, and no partial
runner state is left visible.

### AC-5: Complete the deadline Scan deterministically

**Given** every prepared typed job has returned a schema-validated result
**When** the runner finalizes
**Then** the Scan transitions through `pass1_completed` to `completed` with a
bounded Chinese user-visible success message, the exact count of dispatched
jobs, and the terminal timestamp
**And** no deferred Pass 2 flow planning, flow-group clustering,
  Cross-Repository Resolution, retry, or failover behavior is claimed or
  executed.

## Context resolutions

1. **Story 11.1 dependency is satisfied.** Its completed-story candidate
   `f23f6ee38d1dc6602dde43d4e2a1b4aa54e03b4d` is the source boundary for this
   branch; its knowledge-store entities, migration, repository contract, and
   workflow files remain unchanged.
2. **The runner is deterministic application code, not an LLM agent.** It owns
   scan state transitions, typed job construction, sequential dispatch, and
   terminal status. It does not perform LLM analysis; the LLM-backed Worker
   Agent is Story 11.3.
3. **The typed Agent contract is a port, not an LLM binding.** Story 11.2
   defines `IWorkerAgentDispatcher`, the typed job packet, and the schema-
   validated result envelope. Story 11.3 supplies the LLM-backed
   implementation. A test/fake dispatcher is the only allowed execution
   surface inside this story.
4. **Sequential means one at a time.** One Repository at a time, one job at a
   time. No supersteps, leases, replay, failover, or scheduling beyond a
   single in-order cursor.
5. **Persistence reuses Story 11.1 boundaries.** Any knowledge-store write
   goes through `IKnowledgeStoreRepository`. Runner-execution state lives in
   its own bounded schema and is owned by the runner; it does not duplicate
   Document Version, Edge, or flow evidence.
6. **The deadline cut is explicit.** Concurrency, leases, replay, failover,
   retry policies, Pass 2 flow planning, flow-group clustering, candidate
   selection, dispatch-site/endpoint binding, general Cross-Repository
   Resolution, and the actual LLM Worker Agent are deferred and tracked as
   limitations, never as failing tests.

## Existing implementation seams

- `ScanRun` and `PreScanResult` carry the Repository/ScanRun/Profiled Revision
  identity and the `pre_scan_approved` state this story consumes.
- `ApprovedPreScanHandoff` (Core) is the bounded read model of an approved
  Pre-Scan; `IScanRunRepository.GetApprovedPreScanHandoffAsync` is the existing
  retrieval seam and may be extended or wrapped by a runner-owned service.
- `IKnowledgeStoreRepository` is the Story 11.1 atomic write surface for any
  generated Document Version, Edge, anchor, flow, or projection.
- `AgentType` enumerates `Orchestrator`, `Worker`, `Chat`; this story may add
  the typed Worker Agent job-kind enumeration alongside it without changing
  the enum.
- `Program.cs` already registers specific Core contracts to Infrastructure
  implementations; the one-line runner registration is the only `Program.cs`
  change this story needs and is integration-owner coordinated.
- `VulgataDbContext` is the shared domain context; runner-execution state
  requires either a new additive entity configuration (owned by this story)
  or a runner-owned durable record that reuses the existing connection. The
  accepted plan will choose one approach and prove it does not touch Story
  11.1 migration history.

## Implementation guardrails

- Keep Core free of EF Core and infrastructure dependencies. The runner port
  and typed contracts live in Core; the durable runner state and EF
  configuration live in Infrastructure.
- Use closed domain types and a specific runner contract; do not add data
  annotations, a generic repository, `IQueryable`, or exposed `DbSet`.
- The typed job packet and result envelope are closed allowlists with finite
  UTF-8/count limits, deterministic ordering, and the same sensitive-content
  guard Story 11.1 applies (no absolute host paths, secrets, raw-source
  carriers outside accepted anchors, or unbounded diagnostics).
- The runner never executes an LLM call. All dispatch goes through
  `IWorkerAgentDispatcher`; the deadline LLM-backed implementation is Story
  11.3. A fake/test dispatcher is the only execution surface inside this
  story's tests.
- Persist runner state atomically with phase transitions. A restart re-reads
  durable state and surfaces the in-flight phase; it does not silently resume
  LLM work or fabricate a result.
- On job failure, transition to `failed`, persist a bounded Chinese
  user-visible message, and commit no Document Version from the invalid
  result. Failure does not weaken Story 11.1 knowledge-store invariants.
- Generate no EF migration that modifies Story 11.1 migration history. If
  runner-execution state needs persistence, the migration is additive only
  and is generated after the plan judge accepts the migration invariants.
  EF migration, `VulgataDbContext.cs`, and `Program.cs` edits are
  integration-owner coordinated per the operator directive.
- Add no public API, UI, Chat Agent tool, Scan parsing strategy, provider
  failover, retry policy, Pass 2 flow planning, flow-group clustering,
  candidate selection, dispatch-site/endpoint binding, general
  Cross-Repository Resolution, or Story 11.3 document-generation behavior.
- Commit no failing test.

## Testing requirements

- Observe transient RED then GREEN for every included production behavior.
- Cover Core invariants: closed typed job/result contracts, finite bounds,
  sensitive-content rejection, deterministic ordering, and phase-transition
  rules.
- Cover runner behavior: start from an approved handoff, refuse missing/not-
  approved/duplicate-active prerequisites, sequential dispatch in order, phase
  transitions, durable state after a simulated restart, Chinese failure
  message on schema-invalid/errored/unbounded result, and deterministic
  completion.
- Cover failure isolation: no Document Version is committed from an invalid
  result, no Story 11.1 invariant is weakened, and no partial runner state is
  visible after failure.
- Cover boundary rejection: unauthorized Repository, missing Code Unit,
  oversized packet, forbidden content, and over-limit collections are rejected
  before dispatch.
- If a runner-state migration is added, extend PostgreSQL migration validation
  with clean apply, rollback to
  `20260731041654_AddMinimalKnowledgeStore`, reapply, required
  tables/constraints/indexes, and no pending model changes.
- Treat migration, persistence, and observable data lifecycle as requiring
  current-boundary integration coverage. The final clean-database
  two-Repository demonstration and final Docker image remain Story 11.5
  responsibilities unless this story changes container/runtime behavior.
- Run the Web build and full non-final-image test suite before completion.

## Tasks / Subtasks

- [ ] Tasks are generated from the accepted SpecKit plan and recorded in the
      mapped `tasks.md`.

## References

- Story authority: [Source: `docs/bmad/epics.md#Story-112-Sequential-Scan-Runner--Typed-Agent-Dispatch`]
- Canonical scanning plan: [Source: `docs/bmad/planning-artifacts/architecture/scanning-v1-technical-plan.md`]
- Canonical storage contracts: [Source: `docs/bmad/planning-artifacts/architecture/scanning-v1-data-contracts.md`]
- Deadline slice: [Source: `docs/bmad/planning-artifacts/sprint-change-proposal-2026-07-30-august-15-demo-slice.md`]
- Expedited workflow: [Source: `docs/agent-workflow/august-15-expedited-mvp-exception.md`]
- Canonical terminology: [Source: `docs/UBIQUITOUS_LANGUAGE.md`]
- Integration predecessor: [Source: `specs/004-minimal-knowledge-store/plan.md`]

## Dev Agent Record

### Context reference

- Preparation and activation source commit:
  `96ba064ca321427d7f8bd5aa914a6026536608b5`.
- Story 11.1 completed-story candidate:
  `f23f6ee38d1dc6602dde43d4e2a1b4aa54e03b4d`.
- Execution level: `expedited-mvp`.
- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/11-2-sequential-scan-runner-and-typed-agent-dispatch.json`.
- Proposed SpecKit short name: `sequential-scan-runner`.
- SpecKit feature directory: pending `speckit-specify`.
- Current workflow stage: `context` (pending activation).
- Latest workflow checkpoint ID: pending story-prepared checkpoint.

### Agent Model Used

- Root sprint-loop orchestrator

### Debug Log References

- None.

### Completion Notes List

- Story prepared from the completed Story 11.1 boundary at
  `96ba064ca321427d7f8bd5aa914a6026536608b5` on branch `story112`.
- Execution level `expedited-mvp` verified against the August 15 exception
  allowlist (Stories 11-1 through 11-5) and unexpired lifetime.
- Two planned-change triggers identified for the plan stage: Scan strategy
  (scheduling/grouping/retry/dispatch behavior) and Worker Agent tool / agent
  execution adapter (typed Agent dispatch contracts). The orchestrator will
  pause for planned-change authorization after independent plan-review accepts
  and before task generation.
- Integration-owner coordination gates: EF migrations, `VulgataDbContext.cs`,
  `Program.cs`, and `sprint-status.yaml` transitions. The orchestrator will
  not mutate these without explicit integration-owner coordination.

### File List

- `docs/bmad/implementation-artifacts/11-2-sequential-scan-runner-and-typed-agent-dispatch.md`
- `docs/bmad/implementation-artifacts/sprint-status.yaml`
- `docs/bmad/implementation-artifacts/workflow-activations/11-2-sequential-scan-runner-and-typed-agent-dispatch.json`

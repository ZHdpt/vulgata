# Feature Specification: Sequential Scan Runner & Typed Agent Dispatch

**Feature Branch**: `story112`

**Created**: 2026-07-31

**Status**: Draft

**Input**: User description: "Add a deterministic application-code Sequential Scan Runner that consumes an approved Pre-Scan Handoff for one prepared Repository and drives one Scan execution from `pre_scan_approved` through typed Worker Agent job dispatch, durable phase transitions, and terminal success or failure. Add typed Agent job contracts that name Worker Agent job kinds, their bounded packet shape, and their schema-validated result envelope, without executing any LLM call in this story. Process one Repository at a time, one job at a time. No concurrency, leases, replay, failover, retry, or multi-Repository scheduling. The runner is application code, not an LLM agent; the LLM-backed Worker Agent is a separate story."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SystemOwner starts a deadline Scan from an approved Pre-Scan (Priority: P1)

A SystemOwner has one prepared Repository whose Pre-Scan Result is
`pre_scan_approved` and whose ScanRun has a bound Profiled Revision. The
SystemOwner starts the deadline Scan through the Sequential Scan Runner.
Deterministic application code creates exactly one durable Scan execution
record in the `pass1_planning` phase, links it to the exact ScanRun,
Repository, System, and approved Pre-Scan Result, and refuses to start if any
prerequisite is missing, not approved, or already has an active Scan.

**Why this priority**: This is the entry point for the August 15
demonstration slice. Without a reliable, deterministic start from an approved
Pre-Scan, no downstream Pass 1 generation can run. It establishes the
durable execution record that every later phase depends on.

**Independent Test**: Can be fully tested by preparing one approved
Pre-Scan Handoff and verifying that the runner creates exactly one durable
Scan execution record in `pass1_planning` with correct identity links, and
that duplicate or unapproved starts are refused.

**Acceptance Scenarios**:

1. **Given** a Repository has a `pre_scan_approved` Pre-Scan Result and a
   completed ScanRun with a bound Profiled Revision, **When** the
   SystemOwner starts the deadline Scan through the Sequential Scan Runner,
   **Then** exactly one durable Scan execution record is created in the
   `pass1_planning` phase, linked to the exact ScanRun, Repository, System,
   and approved Pre-Scan Result.
2. **Given** a Repository with no approved Pre-Scan, a missing Profiled
   Revision, or an already-active Scan, **When** the SystemOwner tries to
   start, **Then** the runner refuses to start and reports a bounded,
   Chinese user-visible reason.
3. **Given** the runner is started, **When** the start is observed, **Then**
   no concurrent superstep, lease, replay, failover, retry, or
   multi-Repository scheduling behavior is introduced.

---

### User Story 2 - Runner dispatches typed Worker Agent jobs sequentially (Priority: P1)

The runner advances a Scan execution from `pass1_planning` to
`pass1_running`, materializes one bounded typed job packet per prepared
Code Unit, and dispatches them one at a time against an injected
`IWorkerAgentDispatcher` port. The typed job contract names the Worker
Agent kind, the bounded source slice, the exact Code Unit identity, the
Profiled Revision, and no unauthorized Repository, secret, absolute host
path, source text outside an accepted anchor, or unbounded diagnostic.

**Why this priority**: Sequential typed dispatch is the core of the
deadline execution path. It must be deterministic and bounded so the
August 15 demonstration is reproducible and so the Story 11.3 LLM-backed
Worker Agent has a stable port to implement.

**Independent Test**: Can be fully tested with a fake/test dispatcher that
records the sequence of typed job packets and verifies ordering, bounded
content, and one-at-a-time dispatch without any LLM call.

**Acceptance Scenarios**:

1. **Given** a Scan execution is in `pass1_planning` with the prepared Code
   Units from the approved Pre-Scan Handoff, **When** the runner advances,
   **Then** it transitions to `pass1_running`, materializes one bounded
   typed job packet per prepared Code Unit, and dispatches them one at a
   time against the injected `IWorkerAgentDispatcher` port.
2. **Given** a typed job packet is being constructed, **When** the packet is
   assembled, **Then** it names the Worker Agent kind, the bounded source
   slice, the exact Code Unit identity, and the Profiled Revision, and it
   contains no unauthorized Repository, secret, absolute host path, source
   text outside an accepted anchor, or unbounded diagnostic.
3. **Given** the runner is dispatching, **When** the sequence is observed,
   **Then** no concurrent supersteps, dynamic leases, state replay,
   provider failover, retry policy, or multi-Repository scheduling is
   introduced.

---

### User Story 3 - Runner persists durable state across a server restart (Priority: P1)

A Scan execution is in `pass1_running` with one typed job in flight when the
process restarts. The durable state reports the current phase, the in-flight
job identity, the count of completed jobs, and a bounded restart indicator.
The runner does not silently resume LLM work or fabricate a result. A
SystemOwner-facing query can distinguish `pass1_running` from `completed`,
`failed`, and `cancelled` using the persisted state alone.

**Why this priority**: The August 15 demonstration must survive a server
restart without losing the visible execution state. Silent resumption or
fabricated results would break trust in the wiki and violate the immutability
discipline.

**Independent Test**: Can be fully tested by simulating a restart against
persisted state and verifying that the reported phase, in-flight job, and
completed count are correct and that no LLM work is silently resumed.

**Acceptance Scenarios**:

1. **Given** a Scan execution is in `pass1_running` with one typed job in
   flight, **When** the process restarts, **Then** the durable state
   reports the current phase, the in-flight job identity, the count of
   completed jobs, and a bounded restart indicator.
2. **Given** a restart has occurred, **When** the runner re-reads durable
   state, **Then** it does not silently resume LLM work or fabricate a
   result.
3. **Given** persisted state alone, **When** a SystemOwner-facing query
   runs, **Then** it can distinguish `pass1_running` from `completed`,
   `failed`, and `cancelled`.

---

### User Story 4 - Runner stops on Worker Agent job failure without committing invalid Documents (Priority: P1)

A Worker Agent job returns a schema-invalid result, an unhandled error, or a
result that fails the typed contract's bounded validation. The runner
transitions the Scan to `failed` with a durable, Chinese user-visible
failure message that names the failing Repository and a bounded
non-sensitive reason. No generated Document Version is committed from the
invalid result. No Story 11.1 knowledge-store invariant is weakened, and no
partial runner state is left visible.

**Why this priority**: Failure isolation is a security and integrity
requirement. The knowledge store must never absorb invalid Worker Agent
output, and the demonstration must show a clean failure rather than
corrupted partial state.

**Independent Test**: Can be fully tested with a fake dispatcher that
returns schema-invalid, errored, and unbounded results, verifying that the
Scan fails with a Chinese message and no Document Version is committed.

**Acceptance Scenarios**:

1. **Given** a Worker Agent job returns a schema-invalid result, an
   unhandled error, or a result that fails the typed contract's bounded
   validation, **When** the runner records the failure, **Then** the Scan
   transitions to `failed` with a durable, Chinese user-visible failure
   message that names the failing Repository and a bounded non-sensitive
   reason.
2. **Given** a failed Worker Agent job, **When** the failure is recorded,
   **Then** no generated Document Version is committed from the invalid
   result.
3. **Given** a failed Worker Agent job, **When** the failure is recorded,
   **Then** no Story 11.1 knowledge-store invariant is weakened and no
   partial runner state is left visible.

---

### User Story 5 - Runner completes the deadline Scan deterministically (Priority: P1)

Every prepared typed job has returned a schema-validated result. The runner
finalizes the Scan through `pass1_completed` to `completed` with a bounded
Chinese user-visible success message, the exact count of dispatched jobs,
and the terminal timestamp. No deferred Pass 2 flow planning, flow-group
clustering, Cross-Repository Resolution, retry, or failover behavior is
claimed or executed.

**Why this priority**: Deterministic completion closes the August 15
demonstration loop and proves the sequential path is reproducible end to
end without the deferred infrastructure.

**Independent Test**: Can be fully tested with a fake dispatcher that
returns schema-validated results for every prepared Code Unit, verifying
the terminal phase sequence, the exact job count, and the Chinese success
message.

**Acceptance Scenarios**:

1. **Given** every prepared typed job has returned a schema-validated
   result, **When** the runner finalizes, **Then** the Scan transitions
   through `pass1_completed` to `completed` with a bounded Chinese
   user-visible success message, the exact count of dispatched jobs, and
   the terminal timestamp.
2. **Given** the runner is finalizing, **When** finalization is observed,
   **Then** no deferred Pass 2 flow planning, flow-group clustering,
   Cross-Repository Resolution, retry, or failover behavior is claimed or
   executed.

---

### Edge Cases

- What happens when a prepared Code Unit identity is missing from the
  approved Pre-Scan Handoff? The runner refuses to start or, if detected
  mid-dispatch, fails the Scan with a bounded Chinese reason.
- What happens when a typed job packet exceeds its finite UTF-8/count
  limits or carries a forbidden content kind? The runner rejects the packet
  before dispatch and fails the Scan.
- What happens when the process restarts more than once while a job is in
  flight? The restart indicator stays bounded; the runner does not
  increment unbounded retry counters or silently resume LLM work.
- What happens when a Worker Agent result references an unauthorized
  Repository or carries an absolute host path? The bounded validation
  rejects it, the Scan fails, and no Document Version is committed.
- What happens when an over-limit collection is returned in a result
  envelope? The bounded validation rejects it and the Scan fails.
- What happens when two start requests arrive for the same Repository? The
  second is refused because an active Scan already exists.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Sequential Scan Runner MUST start a deadline Scan only
  from a `pre_scan_approved` Pre-Scan Result with a completed ScanRun and a
  bound Profiled Revision, and MUST refuse to start if any prerequisite is
  missing, not approved, or already has an active Scan.
- **FR-002**: On a valid start, the runner MUST create exactly one durable
  Scan execution record in the `pass1_planning` phase, linked to the exact
  ScanRun, Repository, System, and approved Pre-Scan Result.
- **FR-003**: The runner MUST transition through `pass1_planning` to
  `pass1_running`, materialize one bounded typed job packet per prepared
  Code Unit, and dispatch them one at a time against the injected
  `IWorkerAgentDispatcher` port.
- **FR-004**: Each typed job packet MUST name the Worker Agent kind, the
  bounded source slice, the exact Code Unit identity, and the Profiled
  Revision, and MUST NOT carry any unauthorized Repository, secret,
  absolute host path, source text outside an accepted anchor, or unbounded
  diagnostic.
- **FR-005**: The runner MUST persist durable state atomically with each
  phase transition, recording the current phase, the in-flight job
  identity, the count of completed jobs, and a bounded restart indicator.
- **FR-006**: On a process restart, the runner MUST re-read durable state
  and report the current phase, in-flight job, and completed count, and
  MUST NOT silently resume LLM work or fabricate a result.
- **FR-007**: A SystemOwner-facing query using persisted state alone MUST
  be able to distinguish `pass1_running` from `completed`, `failed`, and
  `cancelled`.
- **FR-008**: When a Worker Agent job returns a schema-invalid result, an
  unhandled error, or a result that fails the typed contract's bounded
  validation, the runner MUST transition the Scan to `failed` with a
  durable, Chinese user-visible failure message that names the failing
  Repository and a bounded non-sensitive reason.
- **FR-009**: On Worker Agent job failure, the runner MUST NOT commit any
  generated Document Version from the invalid result, MUST NOT weaken any
  Story 11.1 knowledge-store invariant, and MUST NOT leave partial runner
  state visible.
- **FR-010**: When every prepared typed job has returned a schema-validated
  result, the runner MUST transition the Scan through `pass1_completed` to
  `completed` with a bounded Chinese user-visible success message, the
  exact count of dispatched jobs, and the terminal timestamp.
- **FR-011**: The runner MUST NOT implement concurrent supersteps, dynamic
  leases, state replay, provider failover, retry policies, or
  multi-Repository scheduling.
- **FR-012**: The runner MUST NOT execute any LLM call. All dispatch MUST
  go through `IWorkerAgentDispatcher`. A fake/test dispatcher is the only
  execution surface inside this story's tests.
- **FR-013**: The runner MUST NOT add a public API, UI, Chat Agent tool,
  Scan parsing strategy, Pass 2 flow planning, flow-group clustering,
  candidate selection, dispatch-site/endpoint binding, general
  Cross-Repository Resolution, or Story 11.3 document-generation behavior.
- **FR-014**: Any EF migration added for runner-execution state MUST be
  additive only, MUST NOT modify Story 11.1 migration history, and MUST be
  generated after the plan judge accepts the migration invariants.
- **FR-015**: All mutable boundaries on the integration-owner-gated paths
  (EF migrations, `VulgataDbContext.cs`, `Program.cs`,
  `sprint-status.yaml`) MUST be coordinated with the integration owner
  before mutation.

### Key Entities *(include if feature involves data)*

- **Scan Execution Record**: The durable record of one deadline Scan run,
  carrying the phase (`pass1_planning`, `pass1_running`, `pass1_completed`,
  `completed`, `failed`, `cancelled`), links to the ScanRun, Repository,
  System, and approved Pre-Scan Result, the in-flight job identity, the
  count of completed jobs, a bounded restart indicator, and terminal
  status, message, and timestamp.
- **Typed Worker Agent Job Packet**: The bounded input to one Worker Agent
  job, naming the Worker Agent kind, the bounded source slice, the exact
  Code Unit identity, and the Profiled Revision, with finite UTF-8/count
  limits and a sensitive-content guard.
- **Typed Worker Agent Result Envelope**: The schema-validated output of
  one Worker Agent job, a closed allowlist with finite bounds and the same
  sensitive-content guard Story 11.1 applies.
- **IWorkerAgentDispatcher**: The port the runner dispatches typed jobs
  against. Story 11.2 defines the port and the fake/test dispatcher; the
  LLM-backed implementation is Story 11.3.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A SystemOwner can start a deadline Scan from one approved
  Pre-Scan Handoff and observe exactly one durable Scan execution record in
  the `pass1_planning` phase within a single sequential pass.
- **SC-002**: The runner dispatches typed Worker Agent jobs strictly one at
  a time, in deterministic order, with zero concurrent supersteps, leases,
  replays, failovers, retries, or multi-Repository scheduling observable
  in any test.
- **SC-003**: After a simulated server restart, the persisted state alone
  is sufficient to report the current phase, in-flight job, and completed
  count, with zero silent LLM resumptions or fabricated results.
- **SC-004**: On any Worker Agent job failure (schema-invalid, errored, or
  unbounded), zero Document Versions are committed from the invalid
  result, and the failure is reported with a bounded Chinese user-visible
  message naming the failing Repository.
- **SC-005**: On full success, the Scan transitions through
  `pass1_completed` to `completed` with the exact count of dispatched jobs
  and a bounded Chinese success message, with zero deferred Pass 2,
  Cross-Repository Resolution, retry, or failover behavior executed.
- **SC-006**: Every committed test stays green; no failing test is
  committed across the story.

## Assumptions

- Story 11.1 is complete at candidate commit
  `f23f6ee38d1dc6602dde43d4e2a1b4aa54e03b4d` and its knowledge-store
  entities, migration, repository contract, and workflow files remain
  unchanged by this story.
- The August 15 demonstration slice is the active delivery scope; this
  story runs under the `expedited-mvp` execution level authorized by
  `docs/agent-workflow/august-15-expedited-mvp-exception.md` for Stories
  11-1 through 11-5, expiring `2026-08-15T23:59:59+08:00`.
- The runner is deterministic application code, not an LLM agent; the
  LLM-backed Worker Agent is Story 11.3 and is out of scope here.
- The typed Agent contract is a port, not an LLM binding; Story 11.3
  supplies the LLM-backed implementation. A fake/test dispatcher is the
  only execution surface inside this story.
- Existing Story 11.1 seams are reused: `ScanRun`, `PreScanResult`,
  `ApprovedPreScanHandoff`, `IScanRunRepository.GetApprovedPreScanHandoffAsync`,
  and `IKnowledgeStoreRepository`.
- Existing `AgentType` enumeration (`Orchestrator`, `Worker`, `Chat`) is
  not modified by this story; a typed Worker Agent job-kind enumeration
  may be added alongside it.
- Runner-execution state persistence is additive only: either a new
  additive entity configuration owned by this story or a runner-owned
  durable record reusing the existing connection. The accepted plan
  chooses one approach and proves it does not touch Story 11.1 migration
  history.
- The one-line runner DI registration in `Program.cs` is the only
  `Program.cs` change this story needs and is integration-owner
  coordinated.
- Deferred items (concurrency, leases, replay, failover, retry, Pass 2
  flow planning, flow-group clustering, candidate selection,
  dispatch-site/endpoint binding, general Cross-Repository Resolution, and
  the LLM Worker Agent) are tracked as limitations, never as failing
  tests.
- EF migrations, `VulgataDbContext.cs`, `Program.cs`, and
  `sprint-status.yaml` are integration-owner-gated boundaries; the
  orchestrator does not mutate them without explicit coordination.

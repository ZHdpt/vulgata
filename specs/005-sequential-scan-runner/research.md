# Research: Sequential Scan Runner & Typed Agent Dispatch

**Date**: 2026-07-31 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Research tasks

### R1: Sequential dispatch without concurrency infrastructure

**Decision**: A single in-order cursor in the runner drives one
`IWorkerAgentDispatcher.DispatchAsync` call at a time inside
`RunPass1Async`. No `Task.WhenAll`, no `Channel`, no semaphore, no lease
table.

**Rationale**: The August 15 demonstration slice explicitly defers
concurrency, leases, supersteps, replay, failover, and retry (Epic 5). A
sequential `foreach` over the deterministically ordered prepared Code Units
is the simplest deterministic path that satisfies FR-003 and FR-011 and keeps
the Story 11.3 LLM-backed Worker Agent behind a stable port.

**Alternatives considered**:

- `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 1`: rejected — it
  still implies a concurrency model and would muddy the "no concurrency"
  boundary;
- a `Channel<T>` with a single consumer: rejected — unnecessary
  indirection for a strictly sequential cursor;
- an actor/superstep model: rejected — explicitly deferred to Epic 5.

### R2: Durable runner-state shape and restart semantics

**Decision**: A single `ScanExecution` row per deadline Scan, with phase,
in-flight job, completed count, total count, bounded restart indicator, and
terminal fields. On restart, re-read the row, increment the bounded
`RestartIndicator` only if a job was in flight, and surface the in-flight
phase. Do not resume the LLM job and do not fabricate a result.

**Rationale**: FR-005, FR-006, and FR-007 require that persisted state alone
distinguish `Pass1Running` from terminal phases and that a restart not
silently resume LLM work. A bounded `RestartIndicator` (0–32) makes the
restart observable without an unbounded retry counter. Silently resuming the
LLM job would violate the "no replay" boundary and could fabricate results
after a partial failure.

**Alternatives considered**:

- a separate `ScanExecutionJob` row per dispatched job: rejected for Story
  11.2 — it adds a second table and a state machine the deadline does not
  need; Epic 5's Scan Coordinator can introduce it later;
- resuming the in-flight LLM job on restart: rejected — violates the "no
  replay" boundary and risks fabricated results;
- an unbounded restart counter: rejected — must stay bounded per the
  sensitive-content/finite-bounds discipline.

### R3: Typed job packet and result envelope as closed Core contracts

**Decision**: `WorkerAgentJobPacket` and `WorkerAgentResultEnvelope` are
immutable Core types with static factory methods that validate every
invariant, including the Story 11.1 sensitive-content guard. A closed
`WorkerAgentJobKind` enumeration names the kinds; for Story 11.2 only
`Pass1CodeLogicDocument` is defined.

**Rationale**: The packet and envelope are the contract the Story 11.3
LLM-backed Worker Agent must implement. Making them closed, validated Core
types now prevents the LLM-backed adapter from drifting and keeps
sensitive-content rejection testable at the Core boundary. Reserving a
closed `WorkerAgentResultPayload` allowlist on `Valid` results lets Story
11.3 add fields without reopening the envelope shape.

**Alternatives considered**:

- a `byte[]` or `JsonElement` payload: rejected — unbounded and bypasses the
  closed-allowlist discipline;
- defining the contract in Infrastructure: rejected — violates Principle I
  (layered boundaries) and Principle II (domain purity);
- shipping the LLM-backed dispatcher in Story 11.2: rejected — out of scope;
  Story 11.3 owns it.

### R4: Migration policy and integration-owner coordination

**Decision**: Add only `ScanExecutions` as an additive migration. Roll back
to `20260731041654_AddMinimalKnowledgeStore` (the Story 11.1 baseline). The
migration, `VulgataDbContext.cs` `DbSet`/schema registration, and the
one-line `Program.cs` DI registration are integration-owner-gated per the
operator directive.

**Rationale**: FR-014 and FR-015 require an additive-only migration that
does not modify Story 11.1 history, and the operator directive gates EF
migrations, `VulgataDbContext.cs`, `Program.cs`, and `sprint-status.yaml`.
Coordinating these before commit keeps the boundary clean and lets the
implementer proceed with Core/Infrastructure/Test work in parallel.

**Alternatives considered**:

- a runner-owned durable record outside EF (e.g., a JSON file): rejected —
  violates the single-database discipline and breaks the restart predicate;
- modifying Story 11.1 migration history: rejected — FR-014 forbids it.

### R5: Failure isolation and Story 11.1 invariant protection

**Decision**: On `SchemaInvalid`/`Error`/`Unbounded`, transition to `Failed`
inside one atomic transaction, set a bounded Chinese `TerminalMessage` and
closed `TerminalReasonKind`, clear `InFlightJobId`, and do not call
`IKnowledgeStoreRepository`. The atomic transaction ensures no partial
`ScanExecution` state is visible.

**Rationale**: FR-008 and FR-009 require that no Document Version be
committed from an invalid result and that no Story 11.1 invariant be
weakened. The runner has no direct knowledge-store write path in Story 11.2
(it never calls `IKnowledgeStoreRepository`), so the protection is structural
plus transactional. The Chinese message and closed reason keep the failure
user-visible and non-sensitive.

**Alternatives considered**:

- committing a partial Document Version from prior valid jobs and marking
  the rest failed: rejected — violates immutability and the "no partial
  state" rule;
- a non-atomic "best effort" failure record: rejected — could leave partial
  runner state visible.

---
story_key: 12-3-resumable-file-pass-1-and-code-logic-documents
title: "Story 12.3: Resumable File Pass 1 & Code Logic Documents"
Status: in-progress
Epic: 12
Story: 3
created: 2026-08-21
depends_on:
  - 12-1-durable-scan-foundation-and-analysis-runtime
  - 12-2-lightweight-java-xml-pre-scan-and-prepared-scan-manifest
references:
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-18-august-demo-host-runtime-and-jdtls-pin.md
  - docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/epics.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - AGENTS.md
  - .specify/memory/constitution.md
---

# Story 12.3: Resumable File Pass 1 & Code Logic Documents

## User story

As a **SystemOwner**,
I want bounded File Code Units analyzed concurrently with durable recovery,
so that Vulgata produces trustworthy Code Logic Documents without losing an
entire Scan to an LLM outage or one peripheral file failure.

## Delivery boundary

Story 12.3 consumes exactly two approved Prepared Scan Manifests and owns the
bounded shared Pass 1 queue, one Worker Agent job per prepared File Code Unit,
the closed `navigate_code` tool, authoritative `code-logic/file-v2` results,
deterministic Document projections, durable provider/job failure policy,
truthful partial completion, and exact same-Repository post-Pass1
reconciliation.

Story 12.4 Cross-Repository Resolution, endpoint-centered grouping,
`retrieve_code_logic`, Pass 2, and Business Logic Documents remain excluded.
Story 12.5 Chat, final release UI, evaluation questions, and release run remain
excluded. CodeGraphContext and unrestricted Agent tool loops remain excluded.

## Inherited authority

- Story 12.1 supplies deterministic Scan Job Execution identity, leases,
  atomic result/job commits, replay, restart recovery, revalidation, and manual
  resume contracts.
- Story 12.2 supplies exact Prepared Scan Manifest identity, canonical pair and
  Repository ordering, exact hashes, analysis-readiness proofs, Communication
  Kind Classifications, File Code Units, and pair-wide atomic replay.
- Vulgata, CodeGraph `@colbymchenry/codegraph@1.5.0`, Java 21, and JDT LS
  `jdt-language-server-1.61.0-202608141332` over stdio run on the Windows host.
  PostgreSQL 17 is the only required Docker service.
- Accepted Story 12.1 and Story 12.2 behavior may change only through a proven,
  explicitly routed compatibility correction.

## Planned-change triggers

The plan necessarily adds or materially changes a Worker Agent tool and Scan
scheduling, concurrency, retry, pause/resume, and dispatch behavior. It also
implements the dated Epic 12 replacement for legacy PRD clauses. After an
independent plan-only `ACCEPT`, the root must pause before task generation for
explicit planned-change authorization identifying triggers, effects,
implementation intent, affected plan sections, PRD clauses, and remaining
gates.

## Ownership

The active root is the sole lifecycle-state writer and committer. Workers may
write only within their explicit dispatch boundary and must not stage, commit,
delegate, switch branches, merge, rebase, reset, push, pull, or rewrite
history. Detailed evidence belongs in durable artifacts and Git; compact
workflow state uses schema 3.0.

## Dev Agent Record

### Context reference

- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/12-3-resumable-file-pass-1-and-code-logic-documents.json`.
- Proposed SpecKit short name: `resumable-file-pass1-code-logic`.
- Expected feature directory: `specs/012-resumable-file-pass1-code-logic`.
- Workflow schema/execution: `3.0` / unified.
- Authority source commit:
  `433834aca53fae868812685a85a9c3c830824025`.

### Context synthesis outcome

The independent read-only context synthesis returned `READY_FOR_SPEC` with no
unresolved authority contradiction. No `CONTEXT-MAP.md`, `CONTEXT.md`, or
repository ADR exists in this checkout. Specification must make exact the
paired job and Document identities, authorized classification access, exact
Story 12.2 hash consumption, current-Document/source navigation behavior,
deferred-observation reconciliation, provider/failure accounting, and partial
completion rules. Architecture-bearing planning is expected; task generation
and production implementation remain unauthorized.

### Specification outcome

`speckit-specify` allocated `specs/012-resumable-file-pass1-code-logic`.
The specification contains 28 acceptance scenarios, 56 numbered Functional
Requirements, and 12 measurable Success Criteria with zero clarification or
template placeholders. Its requirements checklist and inline self-review pass
bounded intent, terminology, authority reconciliation, source coverage,
Story 12.4/12.5 exclusions, and completeness. `plan-architect-high` is the
next owner because the Worker Agent tool, pair-wide scheduling/provider
circuit, authoritative JSON/projection contracts, and reconciliation identity
require architecture decisions. Task generation and production implementation
remain unauthorized.

### Planning outcome

`plan-architect-high` completed `speckit-plan` with 56/56 Functional
Requirements, 12/12 Success Criteria, and 28/28 acceptance scenarios traced.
The plan isolates a new prepared Pass 1 dispatcher and closed `navigate_code`
facade, defines strict `code-logic/file-v2` JSON and canonical identities,
deterministic projections and reconciliation, pair-wide concurrency/provider
recovery, exact partial-completion policy, and public-seam TDD/qualification.
One additive migration is planned for new-path completion/failure atoms,
v1/v2 Document schema allowlisting, nullable v2-only Source Anchor byte bounds,
and new Story 12.3 persistence. Historical rows and accepted Story 12.1/12.2
semantics remain unchanged. The complete plan self-review passed; independent
plan-only review is next. Task generation and production implementation remain
unauthorized.

### Plan-only review iteration 1

The fresh independent reviewer returned `REVISE` with three High plan-owned
findings. PR-001 requires a durable versioned concurrency-setting change seam
that preserves active leases. PR-002 requires every communication kind in a
Worker result to match the referenced authoritative Story 12.2
Communication Kind Classification. PR-003 requires a truthful deferred
observation representation for bound exhaustion even when no navigation
receipt is emitted. The raw verdict is preserved at
`specs/012-resumable-file-pass1-code-logic/reviews/plan-review/plan-review-01.json`.
Correction returns automatically to the plan owner; tasks and production
implementation remain unauthorized.

### Plan-only review iteration 2 and authorization pause

Fresh independent review returned `ACCEPT` with no findings. It independently
closed PR-001 through PR-003, revalidated all 56 Functional Requirements,
12 Success Criteria, and 28 acceptance scenarios, and accepted the additive
migration, compatibility isolation, security, replay, host-runtime, and
Story 12.4/12.5 exclusion boundaries. The raw verdict is preserved at
`specs/012-resumable-file-pass1-code-logic/reviews/plan-review/plan-review-02.json`.
The workflow is paused before task generation for explicit planned-change
authorization bound to scope
`b422424248a47e1bfda677ca7362dd12addc4861873f173edd4579ecb3d3199a`.
The pending request is at
`specs/012-resumable-file-pass1-code-logic/decisions/planned-change-authorization-01.md`.
Tasks and production implementation remain unauthorized.

### Planned-change authorization

The user explicitly approved scope
`b422424248a47e1bfda677ca7362dd12addc4861873f173edd4579ecb3d3199a`
on 2026-08-21. The workflow may proceed to dependency-ordered task
generation. This approval does not authorize production implementation; fresh
combined definition review and the separate implementation-approval gate
remain mandatory.

### Plan correction after plan review iteration 1

The plan owner closed PR-001 with an authorized, versioned 1-4 concurrency
update whose immutable audit identity distinguishes initial from effective
settings, preserves active leases, and applies only to new claims. PR-002 is
closed by a `Rpc|Http` communication-kind allowlist and exact equality to the
referenced Story 12.2 classification identity and fields. PR-003 is closed by
metadata-only `BoundExceeded` receipts, including call-eight and first
depth-four cases, which prove exhaustion without inventing source evidence.
All affected plan artifacts were regenerated and self-review, schema parsing,
56/56 FR, 12/12 SC, 28/28 scenario coverage, and whitespace checks passed.
Fresh independent plan-only review iteration 2 is next; tasks and production
implementation remain unauthorized.

### Task-generation outcome

The task author produced 47 dependency-ordered tasks (`T001`-`T047`) covering
all 56 Functional Requirements, 12 Success Criteria, 28 acceptance scenarios,
and 13 architecture decisions. The task set defines exact RED-to-GREEN
ownership, sole owners for shared persistence and migration paths, bounded
parallel work, host-runtime qualification, and explicit Story 12.4/12.5
exclusion checks. Hash, numbering, placeholder, and whitespace validation
passed. Fresh combined definition review is next; production implementation
remains unauthorized.

### Combined definition review iteration 1

Fresh independent combined review returned `REVISE`. `DR1-001` (High,
tasks-owned) found that v2 prompt creation and `Vulgata.Agents.csproj`
embedding were assigned in an order that cannot preserve a compilable GREEN
sequence. `DR1-002` (Medium, tasks-owned) found contradictory RED/GREEN and
parallel labels in the task summary and maps. Requirement, scenario, and
architecture-decision coverage remains complete; PR-001 through PR-003 remain
closed; Story 12.1/12.2 preservation and Story 12.4/12.5 exclusions passed.
The raw verdict is preserved at
`specs/012-resumable-file-pass1-code-logic/reviews/definition-review/definition-review-01.json`.
Correction returns automatically to the task owner; production implementation
remains unauthorized.

### Task correction after definition review iteration 1

The task owner closed `DR1-001` by assigning one prerequisite task sole
ownership of both the externalized v2 Worker prompt and its explicit embedded
resource registration before dispatcher, provider, or parser consumers. It
closed `DR1-002` by correcting the US1 RED/GREEN references, removing parallel
markers from dependent GREEN work, and making real typed-tool qualification an
explicit post-GREEN task. The corrected 47-task sequence remains contiguous and
retains complete FR, SC, scenario, and architecture-decision coverage. Hash,
path, dependency, placeholder, and whitespace checks passed. Fresh independent
combined definition review iteration 2 is next; production implementation
remains unauthorized.

### Combined definition review iteration 2

Fresh independent review verified `DR1-001` and `DR1-002` closed, then returned
`REVISE` with two new High task-owned findings. `DR2-001` requires explicit
public-seam RED/GREEN ownership for Pass 1 Start, active-concurrency update,
and Status endpoints. `DR2-002` requires the AD-12 Scan and coordination state
transitions to be preceded by an observed behavioral RED and mapped to their
exact GREEN owners. The accepted specification and plan remain valid, with
complete coverage, PR-002/PR-003 closure, Story 12.1/12.2 preservation, and
Story 12.4/12.5 exclusions intact. The raw verdict is preserved at
`specs/012-resumable-file-pass1-code-logic/reviews/definition-review/definition-review-02.json`.
Correction returns automatically to the task owner; production implementation
remains unauthorized.

### Task correction after definition review iteration 2

The task owner closed `DR2-001` with an explicit public-seam API RED/GREEN
pair for authenticated Start, active-concurrency update, Status, and compatible
recovery actions, including exact DTO, validator, endpoint, coordinator,
registration, composition, and test paths. It closed `DR2-002` by making the
AD-12 Scan/coordination transition suite an observed pre-production RED paired
to the domain and migration GREEN owners. The corrected definition now has 49
contiguous tasks and retains the previously accepted prompt/resource ordering,
qualification sequencing, complete traceability, preservation boundaries, and
exclusions. The current task hash was re-observed after the final ownership-map
edit and all direct checks passed. Fresh independent combined definition review
iteration 3 is next; production implementation remains unauthorized.

### Combined definition review iteration 3

Fresh independent review verified all prior findings except one remaining High
task-owned sequencing defect and returned `REVISE`. `DR3-001` found that the
authenticated Resume API RED was paired to a coordinator/store GREEN task that
explicitly did not own the endpoint, DTO, validator, or registration paths.
The required correction separates coordinator/store recovery behavior from the
complete Resume public API assertions and pairs each with its actual sole GREEN
owner. All other coverage, authority preservation, architecture decisions,
prior finding closures, and exclusions passed. The raw verdict is preserved at
`specs/012-resumable-file-pass1-code-logic/reviews/definition-review/definition-review-03.json`.
This third correction routes automatically to the task owner; production
implementation remains unauthorized.

### Task correction after definition review iteration 3

The task owner closed `DR3-001` by pairing coordinator/store recovery RED
coverage to its exact coordinator/store GREEN owner and moving the complete
authenticated Resume HTTP assertions into the public API RED paired with the
sole endpoint/DTO/validator/registration/composition GREEN owner. Phase
acceptance text, dependency order, shared-path ownership, RED/GREEN mapping,
and traceability now agree. The final 49-task artifact hash, size, numbering,
paths, placeholders, and whitespace were independently re-observed. Fresh
combined definition review iteration 4 is next; production implementation
remains unauthorized.

### Combined definition review iteration 4 and correction-loop pause

Fresh independent review returned the fourth consecutive `REVISE` with one
High task-owned finding. `DR4-001` found that the final verification task can
run in parallel with host-runtime, production-boundary/security, and exclusion
qualification that may still change tests or produce mandatory evidence. The
required correction makes those three qualification tasks prerequisites and
runs final verification nonparallel as the stable pre-convergence barrier,
without replacing the root's later pinned post-review/E2E verification. The
raw verdict is preserved at
`specs/012-resumable-file-pass1-code-logic/reviews/definition-review/definition-review-04.json`.
Per workflow policy, no fifth correction cycle may start without explicit user
direction. Production implementation remains unauthorized.

### Fifth correction authorization

The user explicitly approved correction scope
`04356ccf78c8e6ac996f18eae0429e793b73412c32ad4adaeef781b19edd9574`.
The workflow may correct only `DR4-001` in `tasks.md` and run a fresh combined
definition review. The accepted specification and plan remain unchanged, and
production implementation remains unauthorized.

### Combined definition review iteration 5 and implementation pause

Fresh independent combined review returned `ACCEPT` with zero findings against
the final current boundary. It revalidated all 56 Functional Requirements, 12
Success Criteria, 28 acceptance scenarios, 13 architecture decisions, every
prior PR/DR finding closure, Story 12.1/12.2 preservation, the approved host
runtime, and Story 12.4/12.5 exclusions. The raw verdict is preserved at
`specs/012-resumable-file-pass1-code-logic/reviews/definition-review/definition-review-05.json`.
The accepted definition is bound to implementation-approval scope
`4879d08354c64ae796e172e722708b5c76090858518f9f2f7b958f6d082c5359`.
The workflow pauses for explicit production implementation authorization; TDD,
convergence, quality review, E2E, security, migration, verification, and
completion checkpoints remain mandatory.

### Implementation approval

The user explicitly approved implementation scope
`4879d08354c64ae796e172e722708b5c76090858518f9f2f7b958f6d082c5359`
on 2026-08-21. The accepted 49-task definition may proceed through TDD.
Story 12.4/12.5 exclusions and every convergence, review, E2E, qualification,
verification, security, migration, and checkpoint gate remain in force.

### Implementation checkpoint: T001-T008

The foundation group completed the accepted seam inventory, deterministic
two-manifest fixtures, externalized v2 Worker prompt, canonical Pass 1
contracts, additive domain state, and the `AddResumablePass1CodeLogic`
migration. T004 first observed one expected missing-codec failure out of four
tests; T006 first observed six expected missing-transition/schema failures out
of seven tests. Production then turned the canonical and adjacent contract
set green (21/21), the migration seam green (7/7), and the root-owned combined
foundation plus AD-12 compatibility selection green (14/14). Core,
Infrastructure, and Agents builds completed with zero errors; EF reports no
pending model changes; `git diff --check` is clean. Accepted Story 12.1/12.2
state atoms and document schema v1 remain supported alongside the additive v2
contract. Checkpoint `bmad-12-3-implement-20260821T115533Z-73963f` hands off
to US1 tasks T009-T016.

### Implementation checkpoint: T009-T016

US1 now consumes exactly two approved Prepared Scan Manifests, creates the
complete deterministic pair-wide job and navigation-budget set atomically,
claims by canonical Pair Selection Ordinal under one shared limit, preserves
Repository-local Manifest Ordinal display, and supports authorized versioned
concurrency changes. The additive prepared dispatcher emits one server-owned
packet per prepared File Code Unit from the embedded v2 prompt and does not
enumerate or schedule navigation. The RED seams first observed the missing
store, claim/update, dispatcher, and PostgreSQL race behavior. Root verification
then passed 71/71 tests, including 11 strict PostgreSQL 17 start/claim cases,
four pair-start cases, eleven concurrency/order cases, four prepared-dispatcher
cases, and legacy-adjacent dispatcher coverage. The real claim-race correction
materializes the pair `FOR UPDATE` lock, reads post-lock committed queue state
under `READ COMMITTED`, clears stale tracking, and uses durable prepared time
for Start audit/lease ordering. Builds completed with zero errors and
`git diff --check` is clean. Checkpoint
`bmad-12-3-implement-20260821T124742Z-7c1a2e` hands off to bounded
`navigate_code` tasks T017-T023.

### Authorized task correction after definition review iteration 4

The task owner closed `DR4-001` within the approved scope. Host-runtime,
production-boundary/security, and exclusion qualification remain mutually
parallel after all behavior slices; final verification is now nonparallel and
depends on all three durable evidence outputs. It is explicitly a stable
pre-convergence barrier and does not replace the root-owned pinned verification
after convergence, quality review, and E2E. The final 49-task hash, size,
numbering, dependencies, placeholders, and whitespace were independently
re-observed. Fresh combined definition review attempt 5 is next; production
implementation remains unauthorized.

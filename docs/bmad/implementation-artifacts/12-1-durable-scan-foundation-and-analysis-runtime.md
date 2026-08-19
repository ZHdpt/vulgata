---
story_key: 12-1-durable-scan-foundation-and-analysis-runtime
title: "Story 12.1: Durable Scan Foundation & Analysis Runtime"
Status: in-progress
Epic: 12
Story: 1
created: 2026-08-18
depends_on: []
references:
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-17-august-30-bounded-scanning-recovery.md
  - docs/bmad/planning-artifacts/sprint-change-proposal-2026-08-18-august-demo-host-runtime-and-jdtls-pin.md
  - docs/bmad/planning-artifacts/architecture/august-30-bounded-scanning-design.md
  - docs/bmad/planning-artifacts/prds/prd-vulgata-2026-06-12/prd.md
  - docs/bmad/planning-artifacts/architecture.md
  - docs/bmad/epics.md
  - docs/UBIQUITOUS_LANGUAGE.md
  - AGENTS.md
  - .specify/memory/constitution.md
---

# Story 12.1: Durable Scan Foundation & Analysis Runtime

## User story

As a **SystemOwner**,
I want Scan progress, context, and analysis-tool identity persisted durably,
so that a bounded two-Repository Scan can pause, restart, and resume without
losing trusted work.

## Delivery boundary

This is the first dependency-ordered story in the approved Epic 12 August 30
overlay. It establishes durable Scan foundations, analysis runtime identity and
health, typed analysis ports, and recovery/resume semantics. Story 12.2 owns
complete lightweight Java/XML PreScan and the Prepared Scan Manifest; Stories
12.3-12.5 own Worker generation, Cross-Repository Pass 2, and grounded Chat.

The August 30 bounded-scanning proposal and the August 18 host-runtime
correction are already approved. Those approvals establish planning authority;
they do not authorize task generation or implementation. Story 12.1 must
receive an independent plan-only `ACCEPT`, then explicit planned-change
authorization, before tasks are generated.

Historical Story 11.5 and `specs/008-demo-chat-release-gate` are excluded from
this story's definition and ownership.

## Acceptance criteria

### AC-1: Additive durable model and migration lifecycle

**Given** the August 30 migration is applied to a clean PostgreSQL database
**When** the model is inspected
**Then** it persists one immutable Scan Context Snapshot per Repository Scan
Run, deterministic per-file Scan Job Execution state, the required paused and
completed-with-warning phases, and one lightweight System-level coordination
record binding exactly two release Repositories
**And** existing immutable Document, Source Anchor, Edge, Uncertainty, and
prepared-demo rows remain readable
**And** apply, rollback, reapply, concurrent update, and no-pending-model-change
verification pass.

### AC-2: Reproducible host analysis runtime

**Given** production composition starts on the Windows demonstration host
**When** analysis dependencies are verified
**Then** CodeGraph CLI `@colbymchenry/codegraph@1.5.0`, its Windows x64 bundle,
and Eclipse JDT LS `jdt-language-server-1.61.0-202608141332` are pinned to the
approved identities and checksums
**And** JDT LS runs on Java 21 over stdio through StreamJsonRpc `2.25.29`, with
an isolated workspace per Repository revision
**And** PostgreSQL 17 is the only required Docker service for the August release
**And** CodeGraphContext, its Python runtime, and its qualification harness are
not required by production startup or the release gate.

### AC-3: Typed bounded analysis ports

**Given** Vulgata invokes analysis services
**When** Core or Worker code requests structural evidence
**Then** it uses typed CodeGraph, Java LSP, and revision-bound source-reader
ports
**And** raw CLI, raw LSP messages, internal CodeGraph storage, shell access,
network access, and unrestricted filesystem reads do not cross those ports.

### AC-4: Durable restart and manual resume

**Given** a Scan Execution is persisted with completed, pending, running, or
retry-scheduled jobs
**When** the application process restarts or an operator resumes it against the
same PostgreSQL database
**Then** committed Documents are retained, expired leases return to Pending,
and committed jobs do not duplicate Documents
**And** remaining jobs resume only after Repository access, Profiled Revision,
file hashes, prompt/schema identity, provider availability, and analysis-runtime
identity are revalidated.

## Authority reconciliation

- The August 18 correction replaces the August 30 application's Docker-runtime
  gate with a host-run Vulgata/CodeGraph/Java/JDT-LS topology and PostgreSQL-only
  Docker operation. PRD NFR-4.1 remains post-deadline V1 work.
- The dated August 30 design supersedes conflicting legacy CGC-dependent,
  method-level scheduling, orchestration, retry, retrieval, deployment, and
  release-gate details for Epic 12. Broader Epics 5-10 remain backlog.
- `delivery_scope.execution_level: standard` was stale metadata from the prior
  workflow version. This activation repairs it to `unified`; the new activation
  uses `schema_version: "2.0"` and `execution_level: "unified"`.
- `Scan` continues to target exactly one Repository. The approved design's new
  lightweight record coordinates two Repository Scan lifecycles; it is not a
  System-level Scan. Its final canonical Ubiquitous Language name must be
  decided and added during specification.
- The non-ancestral activation checkpoint `d91772004cf6ad56d4fcd38b37917958733f693e`
  is preserved as superseded evidence only. No files are imported from it.
- No repository ADR, `CONTEXT.md`, or `CONTEXT-MAP.md` exists in this checkout.

## Planned-change triggers

The plan-review gate must classify every applicable repository trigger:

1. material additions or changes to Worker Agent tools;
2. material Scan strategy changes, including parsing, filtering, scheduling,
   grouping, retry, resume, or dispatch behavior; and
3. conflicts with or temporary replacements of legacy PRD requirements.

The accepted Story 12.1 plan must explain runtime, persistence, migration,
retry/resume, and Scan effects, and cite the affected PRD/design clauses. The
subsequent human authorization permits progression to task generation only.

## Observed host qualification inputs

- `codegraph` resolves to `G:/packages/npm/prefix/codegraph.ps1` and reports
  `1.5.0`; its three approved installed-file SHA-256 values match the August 18
  correction.
- Java is pinned by machine configuration to
  `C:/Program Files/Java/latest/jdk-21` and reports `21.0.12`.
- JDT LS is supplied at
  `C:/Users/13728/.local/jdt-language-server-1.61.0-202608141332`; the observed
  core bundle is `org.eclipse.jdt.ls.core_1.61.0.202608141332.jar` with SHA-256
  `A58994113B7E493AA8CED51AD02DAB3E424973223E7213F7ED5B9D70861582E2`.
- No adjacent JDT LS distribution archive was observed. The plan must retain the
  authority requirement to record and verify the exact installed archive
  checksum before readiness passes; it must not treat the core-bundle hash as a
  substitute for that archive checksum.
- Absolute installation paths are host configuration only and must not enter
  domain identity, prompts, or persisted release identity without the approved
  normalization described by the August 18 correction.

## Existing implementation seams

- `ScanRun` and the narrow historical `ScanExecution` already persist a
  Repository-bound profiled revision and sequential prepared-run state.
- `VulgataDbContext`, `ScanExecutionRepository`, and the existing migration
  tests provide additive persistence, transactional, locking, concurrency, and
  reversibility seams that the plan must preserve or deepen.
- Production composition and Docker still include CodeGraphContext/Python
  qualification behavior. Epic 12 replaces that release-critical composition
  with version/checksum-pinned host CodeGraph and Java 21/JDT LS adapters.
- No current Scan Context Snapshot, Scan Job Execution, Java LSP adapter, or
  provider-unavailable paused phase exists.

## Ownership and exclusions

The active sprint-loop root is the sole workflow-state writer and committer.
Migrations, shared domain contracts, `VulgataDbContext`, and the model snapshot
remain under this single Story 12.1 integration owner. Dispatched workers must
not stage, commit, delegate, or change Git history.

Definition-stage ownership is limited to this story record, its activation,
the Sprint tracker, the allocated `specs/010-durable-scan-foundation` feature,
the managed AGENTS.md SpecKit pointer at the appropriate planning stage, and
the Ubiquitous Language addition required by the accepted specification.
Production/test ownership will be made exact by the accepted plan before task
generation. Story 11.5, `specs/008-demo-chat-release-gate`, Backup B/demo-only
UI state, and unrelated files remain excluded.

## Dev Agent Record

### Context reference

- Canonical activation:
  `docs/bmad/implementation-artifacts/workflow-activations/12-1-durable-scan-foundation-and-analysis-runtime.json`
- Proposed SpecKit short name: `durable-scan-foundation`.
- Allocated feature directory: `specs/010-durable-scan-foundation`.
- Workflow schema/execution: `2.0` / `unified`.
- Current workflow stage: `implement` (accepted definition checkpoint pending; implementation not started).
- Authority source commit:
  `f8b56ed4ab081d83bbdcfa050a6ff69a76ccba5d`.

### Context synthesis outcome

Fresh synthesis at activation commit
`dd3111bebbe295aa43a01e95be7bbdb97d3eecff` returned `READY_FOR_SPEC` with no
unresolved authority contradiction. The current CGC composition is
implementation drift from the approved Epic 12 overlay. The absent JDT LS
distribution archive checksum remains a hard runtime-readiness prerequisite,
not a definition blocker; the observed core JAR hash is not a substitute.

### Specification outcome

`speckit-specify` allocated `specs/010-durable-scan-foundation`. The author
self-review passed bounded intent, measurable acceptance, terminology,
authority alignment, source-reference coverage, and placeholder checks. The
specification selected **Two-Repository Scan Coordination** as the canonical
term and the root added it to the Ubiquitous Language glossary. Planning is the
next owner; task generation and implementation remain unauthorized.

### Planning outcome

`plan-architect-high` completed `speckit-plan` with a passing author
self-review. The plan makes the required persistence, migration, atomic commit,
lease/concurrency, recovery, runtime-identity, closed-port, JDT-LS lifecycle,
and host-composition decisions. The current stage is `plan-review`; task
generation remains prohibited pending an independent plan-only `ACCEPT` and
explicit planned-change authorization.

### Plan-only review outcome

Independent review iteration 1 returned `REVISE`: two High plan findings cover
the unreachable completed-job replay path and ambiguous 403/404 ownership
mapping; one Medium finding corrects the Scan Context Snapshot timestamp
invariant. All findings name `plan` as earliest owner. The raw verdict is
preserved at
`specs/010-durable-scan-foundation/reviews/plan-review/plan-review-01.json`.
Under the unified workflow, High analysis remediation requires explicit user
approval before the plan author edits artifacts.

The user approved remediation of `PR-001`, `PR-002`, and `PR-003` on
2026-08-18. This approval is limited to plan correction and fresh plan-only
review; it is not the planned-change authorization required before tasks.

The plan author corrected all three findings and repeated the full self-review
with a passing result. The corrected plan now evaluates exact already-completed
replay before new-commit lease/version checks, makes 401/403/404 behavior
deterministic and privacy-preserving, and validates snapshot capture between
the bound Scan Run and associated new-path Scan Execution start timestamps.

Fresh independent plan-only review iteration 2 returned `ACCEPT` with no
findings and confirmed all three corrections closed. The accepted plan is now
paused at the explicit planned-change authorization checkpoint. Tasks and
implementation remain prohibited until that separate authorization is granted.

The user granted the separate planned-change authorization on 2026-08-18. It
permits progression to task generation only and does not waive TDD, independent
definition review, convergence, quality review, E2E, security, migration, or
final verification gates.

`speckit-tasks` produced 54 dependency-ordered tasks with complete FR/SC
traceability and explicit behavioral RED-to-GREEN ownership. An initial worker
attempt inspected a substitute checkout and wrote nothing; the retry was pinned
to the assigned G: checkout and passed author self-review. A fresh independent
full-definition review returned `REVISE` with five High and two Medium findings,
all owned by `tasks`. The raw verdict is preserved at
`specs/010-durable-scan-foundation/reviews/definition-review/definition-review-01.json`.
The user approved remediation of `DR-001` through `DR-007` on 2026-08-18. This
approval permits regeneration of `tasks.md`, author self-review, and a fresh
independent definition review only; production implementation remains prohibited.

The task author regenerated only `tasks.md` and passed self-review with all 54
entries valid. The corrected task boundary explicitly carries PR-003 temporal
transaction behavior, pins StreamJsonRpc before the JDT adapter, serializes the
single integration owner, orders the migration before restart/concurrency work,
separates qualification from honest RED evidence, executes startup readiness and
fail-closed Worker dispatch gating, and hands the frozen definition to convergence
without a duplicate post-implementation definition review. Independent full
definition review iteration 2 is now pending; implementation remains prohibited.

Independent full-definition review iteration 2 returned `REVISE`. Coverage remains
complete at 32/32 functional requirements, 9/9 buildable success criteria, four
user scenarios, and 54 tasks, and the first review's temporal, migration-order,
qualification, startup-readiness, and workflow-handoff findings are closed. Two
High task-owned defects remain: T005 changes `docker-compose.yml` before T036's
behavioral RED and duplicates T037, while T017/T019 retain an undefined circular
handoff for `AnalysisRuntimeContracts.cs`. The raw verdict is preserved at
`specs/010-durable-scan-foundation/reviews/definition-review/definition-review-02.json`.
The unified workflow requires new explicit user approval before these task edits;
production implementation remains prohibited.

The user approved remediation of `DR2-001` and `DR2-002` on 2026-08-18. This
approval is limited to correcting `tasks.md`, author self-review, and the third
independent full-definition review. It does not authorize production implementation.

The task author corrected only `tasks.md` and passed self-review. T005 is now
package-only; T036 RED to T037 GREEN is the sole production-composition change.
T017 is identity-file-only, and T019 is the sole shared
`AnalysisRuntimeContracts.cs` writer after T018 RED. All earlier findings remain
closed across 54 valid tasks. Independent definition-review iteration 3 is pending;
under the unified workflow, another `REVISE` would stop the gate.

Fresh independent definition-review iteration 3 returned `ACCEPT` with no
findings. It verified all 32 Functional Requirements, all 9 buildable Success
Criteria, four scenarios, and 54 executable tasks, and independently confirmed
every prior plan and definition finding closed. The missing JDT distribution
archive/checksum remains fail-closed and required for later real qualification.
The accepted-definition recovery checkpoint is now pending. Production
implementation has not started and is not authorized by the latest remediation
approval.

The accepted-definition checkpoint bound uniquely to
`76e4c9a07d0d9689d0c958ba46c5baaad7d87bd2`. The user subsequently authorized
production implementation on 2026-08-18. Implementation now proceeds through
the accepted public seams using required RED, minimal GREEN, and surrounding
verification evidence; no later gate is waived.

### Implementation group 1

Setup tasks T001 and T005 are complete: the accepted boundary is recorded and
StreamJsonRpc 2.25.29 is centrally pinned/referenced. The bounded Infrastructure
build passed with 0 errors; 791 existing analyzer/source warnings remain separate.

The first Scan Context Snapshot tracer bullet observed an honest compile RED
(CS0246/CS0103), then reached minimal GREEN through the public `Capture` seam.
Independent verification passed 1/1 focused and 119/119 surrounding Core/domain
tests. T007 is complete. T006 remains open for the symmetric persistence ordering,
upper timestamp bound, and no-partial-insert cases owned by T045-T046.

### Implementation group 2

The first Scan Job Execution tracer bullet observed an honest missing-type compile
RED, then reached minimal GREEN through public `Create` and `AcquireLease` seams.
Independent verification passed 1/1 focused and 10/10 surrounding tests. The
aggregate now has deterministic identity, a valid Pending initial shape, and one
monotonic Pending-to-Running lease transition. T008 and T009 remain open for retry,
completion, failure, cancellation, and contradiction behavior.

### Implementation group 3

The retry-scheduling tracer bullet observed an honest compile RED for the missing
retry state, failure category, transition, and retry metadata. Minimal GREEN adds
the Running-to-Retry Scheduled transition with exact lease-token and state-version
guards evaluated before mutation, bounded normalized failure metadata, lease
clearing, preserved attempt count, and one state-version increment. Independent
verification passed 2/2 focused and 12/12 surrounding durable/domain tests. T008
and T009 remain open for completion, terminal failure, cancellation, retry
reacquisition, expired-lease recovery, completion linkage, and the remaining
failure categories.

### Implementation group 4

The recovery/reacquisition tracer bullet observed an honest compile RED for the
missing due-retry and expired-lease recovery seams. Minimal GREEN now permits a
due Retry Scheduled job to return to Pending and a strictly expired Running lease
to recover to Pending, while rejecting early, exact-expiry, stale-version, and
wrong-state contradictions before mutation. Retry and lease metadata are cleared,
attempt counts are preserved by recovery, and each accepted recovery advances the
state version once; normal lease acquisition then records the next attempt.
Independent verification passed 3/3 focused and 13/13 surrounding tests. T008 and
T009 remain open for terminal states, completion linkage, the complete bounded
failure-category policy, and retry exhaustion.

### Implementation group 5

The terminal-state tracer bullet observed an honest compile RED for the absent
Completed, Failed, and Cancelled transitions, the Document Version completion
link, and seven missing closed failure-category atoms. Minimal GREEN implements
all six job states and all eight failure categories, exact lease/version guards,
immutable Completed linkage, bounded normalized failure codes, terminal field
shapes, and mutation-free terminal rejection. Independent verification passed
14/14 focused and 24/24 surrounding durable/domain tests. The accepted domain
scope of T008 and T009 is complete; EF mapping, conditional store concurrency,
atomic Document/job completion, migration, and recovery integration remain owned
by later tasks.

### Implementation group 6

The evolved Scan Execution domain slice first exposed and corrected a test-clock
defect that attempted to move `UpdatedAt` backward; the corrected test remained
an honest compile RED for the new-path factory, phases, bindings, and transitions.
Minimal GREEN adds complete normalized new-path bindings, host-path rejection,
the additive Prepared/running/pause/warning/failure atoms, exact-version and UTC
monotonic transitions, manual pause recovery, bounded reasons, cancellation, and
terminal immutability while preserving the historical `Start` factory, legacy
phase atoms, and legacy columns. Independent verification passed 3/3 focused,
14/14 surrounding durable/concurrency, and 79/79 Sequential Scan Runner tests.
T010 and T011 are complete; persistence and migration compatibility remain later
integration tasks.

### Implementation group 7

The exactly-two coordination slice observed an honest missing-aggregate compile
RED in the actual pinned G: checkout. Minimal GREEN adds canonical deterministic
pair identity, complete same-System Repository/Scan Run/Scan Execution/Profiled
Revision bindings, the closed combined lifecycle, bounded reasons, exact-version
UTC-monotonic transitions, child-success gating, warning propagation, and terminal
immutability without per-file or general N-Repository state. Independent review
found and corrected a test-coverage defect where invalid children were initially
rejected from Preparing rather than Pass2; the remediated test now proves each
invalid child outcome blocks success while both child outcomes and all bindings
remain unchanged. Final verification passed 4/4 focused and 21/21 combined domain
tests. T012 and T013 are complete; store authorization/locking and migration remain
later tasks.

### Implementation group 8

The durable Core contract slice observed an honest missing-type compile RED in
the root-verified G: checkout. Minimal GREEN adds four specific asynchronous
ports and immutable closed models for authorization, recovery snapshots,
progress, coordination, conditional state-version/lease commands, atomic result
commit, and already-Completed replay representation. Reflection tests prove the
surface exposes no EF, IQueryable, DbSet, process, network, stream, raw-source,
general N-Repository, retry-window, attempt-budget, concurrency, ordering, or
Worker-dispatch policy types. Independent verification passed 1/1 focused and
23/23 Durable Scanning Core/domain tests. T014 and T015 are complete; concrete
store, atomic commit, and recovery implementations remain later tasks.

### Implementation group 9

The Analysis Runtime Identity slice observed an honest missing-type RED, then
reached an initial GREEN in the T017 single-file boundary. Independent verification
found four accepted-design gaps: non-explicit field sorting, a `version` rather
than `clientVersion` key, an unversioned CodeGraph platform package, and an
incomplete health observation model. Added RED assertions drove the remediation.
Final GREEN provides recursively ordinal-sorted canonical JSON, the exact
versioned CodeGraph bundle and three hashes, JDT LS/Java/StreamJsonRpc values,
Repository/Profiled Revision binding, host-path non-identity, immutable ordered
component checks, bounded machine failure codes, UTC observations, and required
JDT archive/checksum non-substitution. Verification passed 7/7 focused and 30/30
Analysis Runtime/Durable Scanning tests. T016 and T017 are complete. Real JDT-LS
qualification remains separately fail-closed until the retained distribution
archive and approved checksum are supplied.

### Implementation group 10

The closed analysis-port slice observed an honest missing-contract RED. Minimal
GREEN adds the sole shared `AnalysisRuntimeContracts.cs` boundary with typed
CodeGraph capabilities, Java session/navigation, exact revision-bound source
ranges, runtime health, Repository/revision/workspace scope, finite limits,
cancellation, and immutable bounded evidence/failure results. The first test run
then exposed two harness defects: cyclic framework type traversal caused a stack
overflow, and an ordered enumerable was not materialized for xUnit comparison.
Both were corrected in the test only; production remained unchanged. Final
reflection evidence excludes raw protocol/JSON-RPC, shell/flags, network, internal
store, filesystem, whole-file/unbounded source, process/stream, EF, IQueryable,
cross-Repository, and stale-revision capabilities. Verification passed 7/7
focused, 14/14 Analysis Runtime, and 37/37 combined tests. T018 and T019 are
complete.

### Implementation group 11

Parallel test-only prerequisites T002-T004 are complete. The neutral Java 21
fixture carries deterministic Repository/Profiled Revision and per-file hashes,
plus definition/reference/implementation/call relationships, with traversal and
rooted-path escape rejected. Reusable helpers provide fixed UTC time,
deterministic IDs, recursively canonical hashes, process/filesystem doubles,
cancellation observation, and bounded sanitized diagnostics. The disposable
PostgreSQL 17 harness generates only strongly prefixed exact names, validates and
quotes identifiers, preflights major version 17, targets/terminates/drops only
the exact owned database, and keeps cleanup retryable until absence is proved.
Root combined verification passed 24/24 tests after avoiding a known concurrent
MVC manifest file lock with `--no-build`. Live PostgreSQL create/drop remains
deferred because no `VULGATA_TEST_POSTGRES_ADMIN` is configured and Docker is
unavailable; later PostgreSQL gates still require live evidence.

### Implementation group 12

The required pre-migration RED boundary combined the remaining T006 temporal
persistence cases, T020 store behavior, and T045 EF metadata. Honest compilation
failed for the absent Snapshot DbSet and Durable Scan Repository. The serialized
T046 owner then added three entity configurations, evolved Scan Execution mapping,
DbSets, symmetric PR-003 validation, and one additive migration/designer/snapshot;
T021 added the concrete durable/coordination repository in one separate file.
Initial execution exposed test-fixture defects: incomplete ownership principal
graphs and runtime-model check-constraint inspection. Test owners corrected only
their fixtures/harnesses, preserving the production invariants. Root verification
passed 18/18 combined tests, a 0-error Infrastructure build, and explicit-context
EF pending-model validation (`No changes have been made`). The migration creates
only the three Story 12.1 tables, additive Scan Execution columns/constraints and
their indexes/FKs; Down removes only those structures and restores the previous
phase check. T006, T020, T021, T045, and T046 are complete. Live PostgreSQL 17
apply/rollback/reapply remains T047 and is not claimed here.

### Implementation group 13

The atomic Document/job completion slice observed an honest 18-case RED at the
durable result-committer seam. The serialized T023 owner evolved the existing
Worker Agent result committer and Knowledge Store repository without changing
the accepted Core contract. One EF transaction now materializes exactly one
deterministic immutable Document Version graph and completes the matching
Running Scan Job Execution. Injected failures and cancellation roll back both
sides. Exact already-Completed lost-response replay is evaluated before stale
lease/version validation and returns mutation-free `Committed`; every changed
canonical identity, fingerprint, content, link, or cardinality returns
`Conflict` without mutation. Root/test-author verification passed 18/18 focused
atomic tests and 16/16 surrounding committer tests; the implementer also passed
165/165 Worker Agent and Knowledge Store regressions. T022 and T023 are
complete. Real PostgreSQL row-lock and concurrent atomicity qualification
remains T048 and is not claimed here.

### Implementation group 14

The recovery slice first exposed a traceable generated-contract omission: AD-5
and the accepted ownership plan require revalidation outside database locks,
but the generated Core boundary contained only the durable store. The initial
test correctly could not infer live Repository/revision/hash/prompt/schema/
provider/runtime validity from mutable database rows. The test author replaced
that invalid inference with the accepted closed injected revalidation port, and
production added only its immutable bounded result, the coordinator, and the
store's cross-row Document-link integrity checks. Recovery now reads an
ownership-filtered immutable snapshot, invokes external revalidation before
conditional writes, recovers only expired Running and due Retry Scheduled jobs,
preserves identities, attempts, terminal jobs, and committed Documents, and
fails closed on cancellation, provider unavailability, mismatches, or linkage
contradictions. It adds no loop, timer, retry policy, scheduling, or Worker
dispatch. Original test-author verification passed 16/16 focused tests; adjacent
durable/atomic regressions passed 39/39 and Infrastructure built with zero
errors. T024 and T025 are complete. Concrete host adapter composition remains in
the later accepted runtime/integration task boundaries.

### Implementation group 15

The manual-resume API RED exposed that the first recovery result shape could not
truthfully produce the already accepted bounded HTTP response. A narrow T024/
T025 remediation added an immutable recovery view carrying the exact execution,
phase, total and six state counts, recovered-expired and released-due counts,
state-change flag, and bounded reason. Valid recovery now moves an eligible
provider/operator pause to `Prepared`, rereads durable progress after conditional
job transitions, and distinguishes the first changed result from a mutation-free
idempotent replay. Original test-author verification passed 18/18 focused tests;
adjacent durable regressions passed 42/42 and Infrastructure built with zero
errors. No dispatch, scheduling, timer, retry policy, or endpoint behavior was
added in this remediation.

### Implementation group 16

After explicit user authorization for the disclosed state-changing endpoint,
the manual-resume HTTP slice progressed from an endpoint-only 12-failure RED to
GREEN. The bounded Shared contract and validator accept no caller-controlled
phase, lease, retry, revision, path, provider, executable, or runtime state. The
Web endpoint evaluates authentication and `ManagementAccess` before ownership,
derives the caller's visible Systems, performs only the durable ownership-filtered
prelookup, returns indistinguishable missing/hidden 404 responses, and invokes
recovery once only for a visible execution. It maps the truthful recovery view
to the bounded response and emits sanitized Chinese ProblemDetails for exact
400/401/403/404/409/422/503 outcomes. It initializes no analysis runtime and
dispatches no Worker Agent. Independent verification passed 13/13 focused API
tests, 6/6 ownership tests, and a zero-error Web build. T026 and T027 are
complete. Production DI remains intentionally deferred to T029/T037.

### Implementation group 17

T047 qualified the additive migration lifecycle on the real local PostgreSQL 17
container through a strongly-prefixed disposable database. The harness migrated
to the pre-Story baseline, seeded protected Document Version, Source Anchor,
Document Edge, Uncertainty, prepared-compatible, and legacy Scan data, applied
Story 12.1, verified no pending model changes and continued readability, rolled
back only Story 12.1, reverified protected/legacy rows through raw SQL, and
reapplied successfully. Two harness-only defects were corrected during the run:
the test now uses explicit migration targets/history configuration and handles
already-open connections/PostgreSQL NULL `to_regclass` results correctly. The
focused real-PostgreSQL test passed 1/1, and a final catalog query proved no
`vulgata_story121_%` database remained. T047 is complete; no production,
migration, model, container, volume, or composition file changed.

### Implementation group 18

T028 and T029 completed the durable restart and production-composition slice.
The behavioral RED boots fresh production `Program` hosts over one disposable
PostgreSQL 17 database, reconstructs mixed Pending, Running, RetryScheduled,
Completed, Failed, and Cancelled Scan Job Execution state, repeats manual resume,
competes stale lease claims from separate hosts, and verifies the committed
Document Version remains atomically visible. The honest RED was the missing
`IDurableScanStore` production registration. T029 now shares one scoped
`DurableScanRepository` across both durable store interfaces, one scoped
`WorkerAgentResultCommitter` across the legacy and durable commit interfaces,
and registers the recovery coordinator without adding runtime-health behavior.
The test factory overrides only the fixed clock and the later T037 runtime-backed
revalidation prerequisite. Real PostgreSQL verification passed 1/1, the complete
DurableScanning slice passed 84/84, adjacent composition/recovery/API checks
passed 34/34, Web built with zero errors, and catalog cleanup found no remaining
`vulgata_story121_%` database. T028 and T029 are complete; runtime integrity and
readiness composition remains fail-closed and owned by T036-T037.

### Implementation group 19

T034 and T035 completed the bounded revision-contained Source reader. The RED
covered exact ranges, UTF-8 byte bounds, Repository, Scan Run, Profiled Revision,
snapshot identity, traversal, root containment, reparse points, stale and
cross-Repository requests, cancellation, and sanitized failures. The GREEN
exposes only bounded typed excerpts through the Core port; no absolute path,
file handle, shell, network, unrestricted filesystem capability, or unrelated
source crosses that boundary. Independent verification passed 2/2 focused tests
and the combined Analysis Runtime suite passed 27/27.

T048 also completed real PostgreSQL 17 concurrency and atomicity qualification:
competing Pending claims and Running transitions each produced exactly one
Applied and one Conflict result, while injected commit failures at each tested
save boundary left no partial Document, Document Version, Source Anchor,
Section, or index rows and preserved the Scan Job Execution in Running. The
focused qualification passed 4/4 and final catalog inspection found no owned
temporary database.

CodeGraph and JDT LS work remains deliberately partial. The CodeGraph integrity
probe verified the supplied 1.5.0 installation and all three approved hashes
without launching or indexing, and the public adapter is characterized as
fail-closed when no trusted snapshot/process binding exists. The first JDT LS
tracer verified the required async-disposal seam and the absent retained archive
and checksum remain fail-closed despite the supplied Java 21 and JDT LS install.
T030-T033 remain open until the allowed-operation success path and complete JDT
stdio/workspace/cancellation/shutdown lifecycle are implemented and verified.

### Implementation group 20

After explicit authorization for the exact Infrastructure-only process seams,
T030 and T031 completed the closed CodeGraph adapter. The behavioral RED fixed
the trusted snapshot resolver and bounded process-runner contracts and exercised
all seven allowed operations. The GREEN revalidates Repository, Scan Run,
Profiled Revision, and working-copy binding on every call; maps only the fixed
allowlisted arguments; strictly bounds and parses UTF-8 JSON into typed evidence;
and maps timeouts, malformed responses, process failures, and diagnostics closed.
The parameterless adapter remains fail-closed and the integrity probe performs
no indexing. Original-author verification passed 3/3 focused tests and 32/32
Analysis Runtime tests. T030/T031 are complete; real neutral-fixture tool
qualification remains T038 after production health composition.

T032 and T033 completed the closed Java analysis lifecycle through
Infrastructure-internal process, exclusive revision-workspace, and typed
protocol seams. The behavioral RED observes exact Java 21, Equinox,
`config_win`, and stdio launch arguments; initialize/initialized; all four typed
navigation operations; cancellation; workspace collision; sanitized failures;
ordered shutdown/exit; bounded wait; forced process-tree cleanup; and lease and
process disposal. Original-author verification passed 3/3 focused tests and
32/32 Analysis Runtime tests. No raw LSP, process, filesystem, workspace, host
path, or network capability crosses Core. T032/T033 are complete. The missing
retained JDT distribution archive and authoritative checksum still make T040
ineligible and fail-closed.

T039 separately qualified that fail-closed host state against Java 21.0.12 and
the supplied JDT LS 1.61.0 installation: no retained ZIP or checksum sidecar is
present, the observed core-JAR hash matches its diagnostic value but is rejected
as a substitute, and both missing-archive failure codes remain sanitized and
NotReady. T049 was also verified existing-correct without production edits: SDK
compile inclusion, startup migration, model tests, real PostgreSQL migration
lifecycle, and real Program-host restart all passed, with protected knowledge
readable and no owned temporary database remaining.

### Implementation group 21

T036 and T037 completed production Analysis Runtime health and composition. The
behavioral RED observed seven missing behaviors across closed-port registration,
ordinary-startup integrity invocation, bounded NotReady state, historical CGC
removal, Worker dispatch gating, and PostgreSQL-only default composition. The
GREEN invokes the configured integrity probe once without indexing, importing,
opening a Java session, or dispatching a Worker; retains a bounded sanitized
in-memory readiness result; keeps management startup available; gates every
production Worker dispatch while NotReady; and uses readiness for Scan recovery
revalidation. Request-bound analysis ports resolve but fail closed until T042
supplies their trusted production binding.

Production no longer registers `CodeGraphContextProvider`. After explicit
authorization, the sole historical `RepositoryProfilingCoordinator` registration
that required `ICodeGraphProvider` was replaced with a bounded fail-closed
coordinator until T042; historical source and endpoints remain intact. The app
service now uses only the `post-deadline-app` profile, so default Compose exposes
PostgreSQL alone. Original-author verification passed 9/9 health tests, corrected
the three stale pre-gate Worker registration assertions, passed 3/3 Worker tests
and 41/41 Analysis Runtime tests, and built Web with zero errors. T036/T037 are
complete; real CodeGraph qualification remains T038.

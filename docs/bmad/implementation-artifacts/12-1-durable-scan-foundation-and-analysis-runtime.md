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

### Implementation group 22

T038 completed real CodeGraph 1.5.0 qualification against a unique temporary
copy of the neutral Repository. The qualification verified the installed
package/version/bundle and all three approved component hashes, proved repeated
ordinary integrity checks create no `.codegraph`, and exercised the closed
adapter through init, rebuild, status, symbol search, callers, callees, and
related-file impact. Real output exposed four bounded adapter mismatches that
were corrected through focused T031 RED/GREEN cycles: status field types and
`complete` readiness, successful empty stdout from `index --quiet`, the Core
Type to CLI `class` mapping, and the additional strict symbol-node metadata.
Each correction retained exact unknown/duplicate-field rejection, trusted-path
validation, cancellation, byte/item limits, and typed Core projection.

Final real-tool verification passed 2/2, adjacent CodeGraph/health regression
verification passed 12/12, Infrastructure built with zero errors, and no
temporary Repository, `.codegraph`, or isolated build artifact remained. T038
is complete. The supplied JDT installation still lacks the retained
distribution archive and authoritative checksum, so conditional T040 remains
ineligible and fail-closed rather than skipped as successful evidence.

### Implementation group 23

T041-T044 completed the bounded Analysis Evidence and adversarial capability
slice. The T041 behavioral boundary exercises all seven CodeGraph operations,
all four Java navigation operations, and exact Source reading through the typed
Core ports. An authoritative CodeGraph Repository snapshot now carries the
canonical Runtime Identity hash in addition to Repository, Scan Run, Profiled
Revision, and working-copy binding; any mismatch is rejected after resolution
and before process execution. No raw process, protocol, graph store, network,
or unrestricted filesystem capability crosses the Core boundary.

T043 adversarial tests cover cross-Repository and stale revision/binding/runtime
requests, bounded ranges and byte/item limits, path escape, Java workspace
collision, and option-shaped CodeGraph atoms. T044 rejects leading-hyphen CLI
atoms before snapshot or process activity while preserving valid qualified
symbols and the fixed adapter-owned argument templates. Independent verification
passed 6/6 security tests and 51/51 Analysis Runtime tests; focused bounded
evidence passed 2/2, adapter tests passed 3/3, real CodeGraph qualification
passed 2/2, and Infrastructure built with zero errors. T041-T044 are complete.

### Implementation group 24

Final focused and real-environment verification is complete. T050 passed all
134 Durable Scanning and Analysis Runtime tests with no failures or skips.
T051 passed real PostgreSQL 17 migration apply/rollback/reapply 1/1, real
application restart/recovery 1/1, real CodeGraph qualification 2/2,
missing-archive JDT qualification 2/2, and capability security 6/6; disposable
database and Repository cleanup counts were zero. The conditional real JDT
qualification remains ineligible because no retained distribution archive and
authoritative checksum were supplied, and no Ready claim is made.

T053 found complete authority/source coverage and no unresolved contradiction.
The managed SpecKit pointer and feature selector target `specs/010`; canonical
Repository, Profiled Revision, Scan Run, Scan Job Execution, Document Version,
Edge, Uncertainty, and Cross-Repository terminology is preserved. The approved
August 18 correction explicitly governs the host-run application/tool and
PostgreSQL-only Docker boundary. No applicable ADR or context files exist in
this checkout. T054 found no new PreScan, Prepared Scan Manifest, selection or
splitting, `navigate_code`, Cross-Repository Resolution, Chat, production UI,
or final release implementation. The combined Pass2 atom is coordination state
only, and regenerated historical model metadata does not implement later-story
behavior.

T052 remains blocked. Web builds with zero errors, Durable Scan model tests pass
6/6, default Compose is PostgreSQL-only, and the approved profile adds Vulgata,
but the exact full test command reports 1,485 passed and 14 failed. Five failures
require unavailable external PostgreSQL/provider-image inputs; the remaining
nine are stale historical Story 4.3, Story 11.5, CodeGraphContext qualification,
migration-shape, or unrelated management-UI expectations. None is a Story 12.1
focused regression, but the full-suite gate cannot be declared successful and
the approved boundary forbids modifying those historical paths. Convergence,
quality review, conditional E2E disposition, and final acceptance have not
started.

### Implementation group 25

The user authorized a bounded T052 disposition for thirteen non-Story-12.1
failures: four historical knowledge-store PostgreSQL tests requiring
`VULGATA_TEST_POSTGRES`; one historical provider-image contract requiring
`VULGATA_PROVIDER_CONTRACT_IMAGE`; and eight stale or excluded Story 4.3,
Story 11.5, historical CGC qualification, migration-shape, and unrelated UI
expectations. The current Story 12.1 ordinary-startup health test remains
mandatory and is not covered by that disposition.

The retained JDT-LS distribution archive is now available at the operator-
supplied path with SHA-256
`bbb2297d69a69a8216e17b799744b9ec5454974bbb5e498d45ad3505d8b4fa0f`.
T040 therefore became eligible. Real qualification proved the archive bytes,
Java 21.0.12, the extracted 1.61.0 release, initialize/initialized, clean Maven
import/build, `ServiceReady`, document symbols, definition, references,
cancellation, shutdown/exit, workspace rejection, and cleanup. It also exposed
and retained narrow corrections for the actual `.tar.gz` runtime identity,
explicit archive-path hashing, contained absolute file URIs, the initialized
parameter-object notification, and StreamJsonRpc response decoding.

T040 is blocked on the required real `textDocument/implementation` capability.
The pinned JDT-LS server advertises `implementationProvider`, but repeated
requests against both the interface type and its method declaration return an
empty array after successful project import. Controlled tests falsified
`didOpen`, `ServiceReady` timing, client capabilities, operation order, module
flags/import health, and response-shape parsing as causes. No Java process or
temporary workspace remains. The failing qualification test and two minimal
production candidates remain unstaged and uncommitted; T040 stays unchecked,
and convergence cannot begin.

### Implementation group 26

T040 is complete after independently verifying the focused fix from the
referenced diagnostic task. The defect was a path-role collision: the
Repository working copy had also been used as Eclipse's `-data` workspace,
while the extracted distribution's shared `config_win` directory was used as
writable Equinox configuration. JDT-LS could import the Maven project and
answer other navigation requests, but its implementation/type-hierarchy index
was not reliably registered in that arrangement.

The adapter now keeps the Repository working copy solely as LSP `rootUri`,
creates a contained revision-bound `-data` directory under the configured JDT
workspace root, and copies the retained distribution `config.ini` into a
private configuration directory beside that data. The archive path/hash,
absolute contained document URI, parameter-object `initialized` notification,
and bounded `JToken` response decoding corrections from group 25 remain.
Adjacent tests lock down path containment, separation from the Repository and
installation directories, configuration copying, and cleanup.

Independent verification passed the real retained-archive qualification 2/2,
all JDT-LS tests 7/7, and the complete Analysis Runtime boundary 53/53. The
real test exercised initialize/initialized, symbols, definition, references,
nonempty implementations, cancellation, shutdown/exit, cleanup, and
cross-revision rejection using Java 21 and JDT-LS 1.61.0. No Story 12.1
recoverable failure remains; T052 and the remaining workflow gates follow.

### Implementation group 27

T052 is complete under the user's bounded disposition for thirteen exact
non-Story-12.1 failures. PostgreSQL 17 was healthy on the approved Compose
service, and the mandatory ordinary-startup Analysis Runtime health test passed
1/1. The whole repository test assembly then passed 1,488/1,488 with only the
four named historical knowledge-store PostgreSQL tests, one historical
provider-image contract test, and eight named stale/excluded Story 4.3, Story
11.5, historical CGC, migration-shape, or unrelated UI expectations filtered
out. No Story 12.1 test or class-wide boundary was excluded.

The Web project built with zero errors and nine existing package/version
warnings. Durable Scan model consistency passed 6/6. Compose resolves to only
`postgres` by default and to `postgres` plus `vulgata` under the
`post-deadline-app` profile. `docker/Dockerfile` and historical CGC
qualification were not invoked or modified. Implementation is ready for the
unified convergence gate.

### Convergence iteration 1

The independent convergence analyst returned `tasks_appended`, not CLEAN. It
verified the pinned `c6e9014` implementation and 53/53 Analysis Runtime tests,
then identified two traceable remaining gaps. T055 closes the production
ordinary-startup integrity gap: Ready currently depends on a hard-coded JDT-LS
`.zip` under the installation root and does not validate the configured
archive path, Java/JDT/StreamJsonRpc identities, or configured CodeGraph
identity. T056 closes the production recovery gap: the durable recovery
snapshot and revalidator currently prove System ownership and global readiness
but do not carry and verify Repository access, Profiled Revision/source hashes,
prompt/schema/provider identity, or the bound Runtime Identity.

The analyst appended only T055 and T056 to `tasks.md`; no source, tests,
accepted definition, workflow state, staging, or history changed. The unified
workflow returns to test-first implementation for these tasks before a second
convergence pass.

### Implementation group 28

T055 and T056 close convergence iteration 1. T055 now verifies the configured
archive path/name/bytes, exact JDT-LS release/launcher/configuration, Java 21,
StreamJsonRpc 2.25.29, and CodeGraph command/package/repository/version/bundle
plus actual component hashes before Ready. It launches neither tool and does
not index, import, or create Repository state. A Ready result carries the
canonical scope-bound Runtime Identity hash; NotReady carries no hash.

T056 extends the immutable recovery read model with bounded Repository access,
Profiled Revision, ordered File Code Unit SourceHash bindings, prompt/schema,
provider availability, and Runtime Identity data. The production revalidator
uses a fresh durable binding read and Worker Agent provider resolution, then
recomputes configured integrity for the snapshot Repository/Profiled Revision.
It requires exact ordinal identity/hash equality before the coordinator can
make a conditional transition. Null, malformed, duplicated, unauthorized,
unavailable, stale, or mismatched inputs fail closed; existing committed-
Document integrity behavior is unchanged.

The two behavioral RED groups were frozen before production work. Independent
verification passed 23/23 focused health/revalidation tests and 150/150
Analysis Runtime plus Durable Scanning tests. Core, Infrastructure, and Web all
built with zero errors; only existing package/version warnings remain. T055 and
T056 are complete and the candidate returns to convergence iteration 2.

### Convergence iteration 2

The independent convergence analyst returned `tasks_appended`, not CLEAN. It
verified that T055 closes configured Analysis Runtime integrity, but found that
T056's production `RuntimeScanRecoveryBindingProbe` still obtains Repository
access, Profiled Revision, affected SourceHash, prompt/schema, and provider
fields by rereading the same durable recovery snapshot. Those comparisons are
therefore self-consistency checks rather than independent current-state
revalidation.

The analyst appended only T057. It requires fresh observations from current
Repository authorization/access, revision/source, prompt/schema, and provider
authorities before any recovery transition, while retaining the independently
recomputed configured Runtime Identity hash and fail-closed committed-Document
integrity. The workflow returns to behavioral RED and minimal GREEN before a
third convergence pass.

### Implementation group 29

T057 closes convergence iteration 2. The immutable recovery snapshot now
carries its bound Scan Run ID. The production binding probe no longer depends
on `IDurableScanStore` and never rereads the Scan Execution to manufacture
"current" values. It resolves the exact current Repository/System binding,
loads the bound Scan Run with its current File Code Units, derives the current
Worker prompt identity from the embedded Pass 1 prompt and the closed Knowledge
Document schema identity, and resolves the current Worker provider assignment.

Only File Code Unit IDs selected by the immutable snapshot are projected from
the current Scan Run, with exact run/repository/type/hash/cardinality checks.
Missing, duplicate, malformed, unauthorized, failed/aborted, unavailable, or
mismatched observations fail closed before any recovery transition. The
configured Analysis Runtime hash remains independently recomputed.

The behavioral RED was frozen before production work. Independent verification
passed 12/12 focused tests and 151/151 surrounding Durable Scanning, recovery,
manual-resume, and Analysis Runtime tests. The Web build, including Core,
Infrastructure, Agents, and Web, completed with zero errors. T057 is complete,
and the committed candidate proceeds to convergence iteration 3.

### Convergence iteration 3

The independent convergence analyst returned CLEAN at pinned commit
`af756bc`. It checked all 32 functional requirements, nine success criteria,
four Story 12.1 scenarios, ten architecture decisions, five Constitution
principles, all 57 completed tasks, and the explicit exclusions. No remaining
task was appended and `tasks.md` stayed byte-for-byte unchanged.

Focused recovery revalidation passed 12/12. The analyst independently confirmed
that current Repository/System, Scan Run/File Code Unit, Worker prompt/schema,
provider, and configured Runtime Identity observations no longer originate from
the persisted recovery snapshot. Story 12.1 advances to independent quality
review.

### Quality review iteration 1

The independent quality reviewer returned REVISE at pinned commit `8fa8b8f`.
Focused Story 12.1 verification passed 162/162 and Web built with zero errors,
but three HIGH implementation gaps remain. QR-001 found that durable job-manifest
replay can accept a strict subset because it lacks complete bidirectional
canonical equality. QR-002 found that the durable Scan commit path trusts a
well-shaped caller fingerprint instead of recomputing the canonical
materialization fingerprint. QR-003 found that a configured runtime-health
exception during manual resume can escape as an unclassified 500 rather than a
bounded fail-closed result.

All three findings are implementation-owned and have separable behavioral RED
boundaries. They return to TDD, after which convergence and quality review must
run again. The review did not reopen any approved or excluded historical scope.

### Quality correction group 30

QR-001 through QR-003 are corrected test-first. Durable job-manifest replay now
requires pristine canonical initial jobs, exact `1..N` supplied order, equal
cardinality, deterministic identities, and complete position-by-position state
equality; subset, superset, reordered, or mutated manifests cannot replay or
mutate durable state. Independent verification passed 10/10 focused and
103/103 Durable Scanning tests.

The durable Scan commit path now constructs the canonical Knowledge Commit,
materializes it, and recomputes its commit fingerprint before replay acceptance,
transaction creation, or mutation. A caller-supplied well-shaped but
noncanonical fingerprint returns `Conflict`/`IntegrityViolation`; canonical
commit and lost-response replay remain valid. Independent verification passed
19/19 focused and 39/39 adjacent persistence/committer tests.

Manual recovery now maps any non-cancellation configured-health exception to a
bounded `InvalidIdentity` result without diagnostic leakage; caller cancellation
still propagates. Independent verification passed 14/14 focused and 86/86
adjacent recovery/manual-resume/Analysis Runtime tests, and Web built with zero
errors. The correction invalidates downstream convergence and quality review,
which must rerun on the committed boundary.

### Convergence iteration 4

The independent convergence analyst returned CLEAN at corrected commit
`6a1858c`. It verified QR-001 through QR-003 against the full Story 12.1
definition and appended no task. Focused manifest replay passed 10/10, atomic
commit passed 19/19, and recovery revalidation passed 14/14. Static inspection
confirmed strict complete manifest equality, Infrastructure-derived canonical
fingerprints before mutation/replay, and bounded configured-health failure with
cancellation propagation. The corrected candidate advances to quality review
iteration 2.

### Quality review iteration 2

The independent quality reviewer returned REVISE at checkpoint `d340e8f5`.
Prior findings QR-001 through QR-003 are closed, but QR2-001 found that manual
recovery persists each eligible Scan Job Execution transition and the Scan
Execution phase transition separately. A later version conflict, integrity
failure, or cancellation can therefore return failure after earlier mutations
have committed, contrary to AD-5's single conditional transaction and the
captured-StateVersion contract. The required correction is a public-seam
behavioral RED proving zero partial mutation, followed by a batch store
transaction that validates the complete captured recovery set before one
commit. Convergence and quality review must rerun after the correction.

### QR2-001 correction

QR2-001 is corrected test-first. The frozen public-seam tests now require one
closed recovery batch containing the captured Scan Execution StateVersion and
the complete Scan Job Execution transition set. The coordinator invokes that
batch exactly once. The durable store validates membership, identity, every
captured StateVersion, terminal Document integrity, transition timing, and the
optional phase precondition before mutation inside one serializable EF
transaction, followed by one SaveChanges and one commit. A stale second job now
returns conflict with every job and the Scan Execution phase unchanged.
Independent verification passed 31/31 focused and 27/27 adjacent recovery and
manual-resume tests; Core, Infrastructure, and Web builds have zero errors.
This behavior correction invalidates convergence iteration 4 and quality review
iteration 2, which must rerun on the committed boundary.

### Convergence iteration 5

The independent convergence analyst returned CLEAN at corrected checkpoint
`5ad2562e`. It checked FR-001 through FR-032, SC-001 through SC-009, all four
acceptance scenarios, AD-1 through AD-10, and all 57 completed tasks. QR2-001's
single complete batch, full pre-mutation validation, one serializable
transaction, and stale-later-job zero-mutation behavior were independently
verified. Focused recovery and store suites passed, Durable Scanning passed
105/105, and Web built with zero errors. No task was appended. The corrected
candidate advances to quality review iteration 3.

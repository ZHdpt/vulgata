# Sprint Loop State Machine

## Sources of Truth

BMad owns coarse lifecycle state. A story-local `workflow-state.json` owns resumable internal progress but cannot invent or override BMad status. State updates occur only after their evidence is durable.

## Execution Levels

Every story records `execution_level` as `full`, `standard`, or
`expedited-mvp`; omission is invalid. Full is the default at initial selection.
Standard is available only through the eligibility contract in
`$run-bmad-sprint-loop`. `expedited-mvp` is a temporary closed allowlist
defined by `docs/agent-workflow/august-15-expedited-mvp-exception.md`; it
expires after Epic 11 or the August 15 deadline and cannot be generalized.
Both reduced levels keep TDD, conditional E2E, final verification, Git safety,
and planned-change authorization.

Full uses every internal stage. Standard uses:

```text
context, specify, plan, conditional plan-review, tasks, implement,
quality-review, conditional-e2e, verify, complete
```

For Standard and `expedited-mvp`, `quality-review` is one independent combined acceptance-and-quality gate and `active_gates.acceptance-review.status` is `invalidated`. Skipped Full-only stages do not acquire fabricated accepted records. A level change is never implicit: if Standard eligibility becomes false, stop for explicit `Standard -> Full` authorization and resume from the earliest omitted or invalid stage. If the expedited allowlist or lifetime no longer applies, stop and select Standard or Full under its ordinary rules.

`expedited-mvp` uses the same internal stage sequence and combined review shape
as Standard, but only for its approved deadline stories. Its three planned
checkpoints and evidence limits come from the dated exception. Deferred
acceptance is recorded as a limitation, never as a passing result or failing
committed test.

## BMad Lifecycle

The only permitted status vocabulary is `backlog`, `ready-for-dev`, `in-progress`, `review`, and `done`.

| From | Trigger and gate | To | Durable effect |
|---|---|---|---|
| `backlog` | BMad create-story produces validated story context | `ready-for-dev` | Story file and sprint status are checkpointed |
| `ready-for-dev` | Queue selection, preflight, and context synthesis succeed; SpecKit cycle is about to start | `in-progress` | A story-activation checkpoint durably records the development record, sprint status, branch/HEAD, ownership, and activation checkpoint ID before `speckit-specify`; it contains no unaccepted SpecKit artifact |
| `in-progress` | Every implementation task group has verified red-green-refactor evidence and the candidate boundary is pinned | `review` | A ready-for-review checkpoint durably records the story/status transition and candidate identity before the applicable Full review axes or reduced-level combined review is dispatched |
| `review` | Full: both independent reviews accept. Standard or `expedited-mvp`: its combined quality review accepts. In every level, required E2E passes or has a valid coverage override, final verification passes, the reviewed boundary remains current, and every task/checklist is complete | `done` | Story development record, workflow state, and sprint status are checkpointed |

`done` is terminal for this workflow. A requested `done` story is rejected as new work. An `in-progress` or `review` story is resumed rather than duplicated. Any reverse transition or status not listed above requires the applicable BMad correction workflow and explicit user direction; the sprint loop does not invent it.

## BMad Epic Lifecycle

The epic status vocabulary is `backlog`, `in-progress`, and `done`.

| From | Trigger and gate | To | Durable effect |
|---|---|---|---|
| `backlog` | The first story activation checkpoint succeeds | `in-progress` | Epic and sprint status changes are committed atomically with the story activation checkpoint |
| `in-progress` | Every story is `done`; no story workflow is blocking; exact-clean-source build/tests and architecture gate pass; reserved dispositions are approved; retrospective choice is `run-after-close` or `declined` | `done` | Gate/evidence/disposition/status artifacts and provisional transition are committed; `done` becomes trusted only after checkpoint binding |

An epic with active/done stories but `backlog` status is inconsistent and stops. Before new-transaction preflight or ordinary eligibility, epic close resolves any `pending-binding` transaction. The committed gate deliberately remains pending; `effective_phase = bound` is derived durably from that immutable record plus a verified direct-child checkpoint whose `Workflow-Manifest-SHA256` matches its complete Git tuple set. “Offer retrospective” records `run-after-close` or `declined` after the architecture gate. A scheduled run starts only after binding under separate retrospective state and checkpoint.

## Internal Stages

The exact internal stage names are:

```text
context, specify, spec-review, plan, plan-review, tasks, analyze,
implement, acceptance-review, quality-review, conditional-e2e, verify, complete
```

| Stage | Owner | Entry condition | Exit evidence and next stage |
|---|---|---|---|
| `context` | Orchestrator and `story-context-synthesizer` | Selected non-`done` story; preflight recorded | Cited brief and authority conflicts resolved → `specify` |
| `specify` | `spec-author` | Valid context brief | `spec.md`, checklist, feature path, and mapping recorded → `spec-review` |
| `spec-review` | Fresh `spec-judge` | Specification boundary pinned | `ACCEPT` with no blocking finding → `plan`; `REVISE` → fresh `specify`; `BLOCKED` → stop |
| `plan` | `plan-architect` | Accepted specification | Plan artifact set and Constitution checks produced → `plan-review` |
| `plan-review` | Fresh `plan-judge` | Complete planning set pinned | `ACCEPT` with no gate failure → `tasks`; `REVISE` → fresh `plan`; `BLOCKED` → stop |
| `tasks` | `task-author` | Accepted plan | Dependency-ordered `tasks.md` with test-first groups → `analyze` |
| `analyze` | Read-only `artifact-analyst` | Current `spec.md`, `plan.md`, and `tasks.md` | No Critical/High → `implement`; blocking findings → human approval gate, responsible author, dependent regeneration, then rerun `analyze` |
| `implement` | Fresh `tdd-implementer` per group | Full: accepted analysis. Standard: accepted eligibility and root coverage validation. `expedited-mvp`: active dated authority and accepted delivery definition. In every level, the dependency group is unblocked | Red-green-refactor evidence and completed task group; after all groups, checkpoint `in-progress → review`, pin boundary, then enter the applicable Full axes or combined review |
| `acceptance-review` | Fresh `acceptance-reviewer` | Story status is `review`; same pinned implementation boundary as quality review | Per-gate verdict recorded in `active_gates`; scalar stage remains here until both axes return |
| `quality-review` | Fresh `quality-reviewer` | Story status is `review`; Full uses the same pinned implementation boundary as acceptance review; Standard supplies story/spec/plan/tasks plus the pinned implementation boundary | Full applies the stored quality verdict after acceptance joins; Standard uses one combined acceptance-and-quality verdict; applicable `ACCEPT` → `conditional-e2e` |
| `conditional-e2e` | Orchestrator; `e2e-runner` when required | Full: both review axes accepted. Standard: combined quality review accepted | Required E2E passes, or skip rationale cites covering evidence → `verify` |
| `verify` | Orchestrator | Tasks and gates complete | Story tests, web build, full tests, checklists, and unchanged review boundary pass → `complete` |
| `complete` | Orchestrator | Verification evidence durable and story is already `review` | BMad transitions `review → done`; provisional complete state becomes trusted only after the final checkpoint trailer resolves in Git |

In Full, acceptance and quality reviews may run in parallel but are represented independently in `active_gates`. On entry, scalar `stage` is `acceptance-review` and both gate records become `active` with identical boundary identities. Returns are persisted in fixed acceptance-then-quality key order regardless of completion order. When both accept, history closes `acceptance-review`, records `quality-review` accepted from its stored verdict, and advances atomically. A blocking return invalidates both verdicts after correction because the boundary changes.

In Standard, scalar stage enters `quality-review` directly. The acceptance gate remains `invalidated`; the quality verdict explicitly covers both acceptance completeness and quality. Any tracked correction invalidates and reruns that combined gate.

The BMad story remains `review` during review-driven implementation or E2E correction. Internal state returns to `implement`, creates a new pinned candidate, and reruns the applicable Full axes or reduced-level combined review; it does not invent a reverse BMad status transition.

## Gate Advancement and Human Decisions

A validated `ACCEPT` from `spec-judge`, `plan-judge`, `acceptance-reviewer`, or `quality-reviewer` completes that gate. Once all required reviewers for a boundary accept, the orchestrator advances and creates the applicable local checkpoint without requesting duplicate user approval. A nonblocking analysis result, passing E2E gate, or passing verification behaves the same way.

The workflow pauses for explicit user direction when the next action depends on authority outside the reviewing agent's contract: remediation of a Critical/High `speckit-analyze` finding; unresolved product intent/scope or three `REVISE` outcomes; a Strong architecture recommendation or ADR conflict; divergent history or destructive VCS recovery; inseparable worktree ownership; unavailable required infrastructure; unsafe external actions; or an ambiguous E2E failure. `REVISE` findings that remain inside already approved intent and scope follow the correction loop automatically for at most three attempts. `BLOCKED` stops immediately and does not consume an attempt.

## Invalidation Dependencies

Changing an input invalidates its owning stage and every dependent stage. The earliest invalid stage is rerun.

| Changed or missing input | Earliest invalid stage | Also invalidates |
|---|---|---|
| BMad story, epic scope, PRD/architecture authority, Constitution, ADR, context doc, or Ubiquitous Language relevant to the story | `context` | Every later stage |
| Story brief | `context` | `specify` through `complete` |
| `spec.md`, requirements checklist, or story/feature mapping | `specify` | Every later level-required stage through `complete` |
| Any plan artifact or Constitution check | `plan` | Every later level-required stage through `complete` |
| `tasks.md` | `tasks` | Full: `analyze` through `complete`; Standard: `implement` through `complete` |
| Analysis report, analyzed artifact hash, or approved analysis remediation | `analyze` | `implement` through `complete`; upstream remediation additionally invalidates dependent plan/tasks as applicable |
| Implementation or test file | `implement` | Both review axes, E2E decision/result, `verify`, and `complete` |
| Review correction | `implement` | Every affected review axis and all later stages |
| Tracked E2E/integration test only | Full: `acceptance-review`; Standard or `expedited-mvp`: `quality-review` | Applicable Full verdicts or combined verdict, E2E decision/result, `verify`, and `complete`; ownership stays with `e2e-runner` and there is no test-only review bypass |
| Production behavior covered by E2E | `implement` | Both reviews, E2E decision/result, `verify`, and `complete`; production correction returns to `tdd-implementer` |
| Verification command set or final candidate boundary | `verify` | `complete`; changed production boundary returns to affected review axes |
| Malformed or missing workflow state | Earliest stage provable from validated artifacts | All unproven later stages |

An approved analysis correction to `spec.md` regenerates plan artifacts and tasks; a correction to plan artifacts regenerates tasks; a correction confined to `tasks.md` reruns analysis. In all cases, `speckit-analyze` runs again immediately before implementation.

## Resume Algorithm

1. Load complete BMad sprint state and the story's development record.
2. Derive `docs/bmad/implementation-artifacts/workflow-activations/<story-key>.json`; for a story activated by this workflow, require and validate that record and its checkpoint before consulting any feature mapping.
3. If the activation exists but is not yet superseded, resume `context`/`specify` from its proposed feature short name. If superseded, follow `superseded_by_state`, validate the one mapped feature directory and both mapping sides, then parse `workflow-state.json`.
4. Legacy fallback: when an already-`in-progress`/`review` story predates activation records and its activation file is absent, read only the feature directory explicitly named in its BMad development record. Resume it when valid workflow state points back to the same story/epic/path, both sides are unique, and artifacts/checkpoint/ownership validate. Do not duplicate or stop solely for the absent activation file. Missing, inferred, partial, conflicting, duplicate, or many-to-one legacy mappings stop for user direction.
5. Resolve the last checkpoint ID from Git trailers, obtain its immutable SHA, and verify it is an ancestor of current `HEAD`.
6. Compare the recorded branch, preflight `HEAD`, repository/Git/worktree identities, exact status snapshot, owned/unrelated paths, artifact hashes, review diff identities, and gate evidence with current state.
7. If inputs are unchanged and a stage is complete, treat rerun as a no-op.
8. On changed, missing, or malformed inputs, select the earliest invalid owner from the dependency table and clear only dependent later claims.
9. Record a recoverable agent/tool failure without advancing. Retry the same stage with a fresh direct-child agent when safe.
10. Stop for user direction on divergent history or a destructive recovery proposal, inseparable unrelated edits, unresolved intent/scope or authority conflicts, unavailable required infrastructure, unsafe external actions, or three `REVISE` outcomes at one gate. Any `BLOCKED` verdict stops immediately without incrementing that counter.

Resume never repairs divergence through reset, rebase, force push, or silent branch switching. A checkpointed stage is complete only when its local commit succeeds and the checkpoint ID resolves in Git history.

## Idempotency and Failure Rules

- State writes are atomic.
- Completed stages with identical input commit and artifact hashes are no-ops.
- Rejected gates and failing implementation/tests never advance or become checkpoints.
- A dirty worktree is permitted only for exact paths recorded in `repository_preflight.owned_paths`; unrelated paths and their status remain untouched and unstaged. Missing or inseparable ownership stops for user direction.
- Parallel disjoint work is serialized before shared-file changes, index operations, lifecycle updates, or commits.
- A failure record contains stage, agent role, source boundary, command/error evidence, retryability, and the earliest safe resume stage.

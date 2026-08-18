# Sprint Loop State Machine

## Sources of Truth

BMad owns coarse lifecycle. A story-local `workflow-state.json` owns resumable internal progress and cannot override BMad. New states use schema `2.0` and `execution_level: unified`.

## BMad Lifecycle

The story vocabulary is `backlog`, `ready-for-dev`, `in-progress`, `review`, and `done`.

| From | Gate | To |
|---|---|---|
| backlog | BMad sprint planning/readiness creates a bounded story | ready-for-dev |
| ready-for-dev | queue resolution, preflight, ownership, and activation succeed | in-progress |
| in-progress | all current tasks pass, convergence is clean, and the candidate is pinned | review |
| review | quality, E2E decision, final verification, and checkpoint binding succeed | done |

The epic vocabulary is `backlog`, `in-progress`, and `done`. First story activation starts the epic. Only close-bmad-epic may complete it.

## Unified Internal Stages

```text
context, specify, plan, plan-review, tasks, definition-review,
implement, converge, quality-review, conditional-e2e, verify, complete
```

`plan-review` appears only for a repository planned-change trigger. It must accept, and the named user authorization must be durable, before `tasks`.

| Stage | Owner | Exit |
|---|---|---|
| context | root; optional context synthesizer | pinned authority set and separable ownership |
| specify | spec author | specification plus successful self-review |
| plan | plan architect | complete plan plus successful self-review |
| plan-review | definition reviewer in plan-only mode | accepted verdict and required user authorization |
| tasks | task author | dependency-ordered tasks plus successful self-review |
| definition-review | fresh definition reviewer | one accepted spec/plan/tasks/analyze verdict |
| implement | test author then implementer | honest RED, minimal GREEN, surrounding GREEN; or truthful nonbehavioral evidence |
| converge | convergence analyst | clean outcome, or appended tasks returning to implement |
| quality-review | fresh quality reviewer | accepted converged boundary |
| conditional-e2e | root; E2E runner when required | passing evidence or complete current-boundary override |
| verify | root | focused tests, Web build, full tests, required risk checks, unchanged boundary |
| complete | root | review to done transition bound to a unique ancestral checkpoint |

## Correction Ownership

Every definition finding carries `earliest_owner`.

| Earliest owner | Resume | Invalidate/regenerate |
|---|---|---|
| spec | specify | plan, tasks, definition review, implementation, convergence, review, E2E, verification |
| plan | plan | tasks and every later stage |
| tasks | tasks | definition review and every later stage |
| implementation/test | implement | convergence and every later stage |
| convergence task append | implement | convergence and every later stage |
| quality behavior finding | implement | convergence, quality, E2E, verification |
| quality definition finding | named definition owner | all downstream stages |
| tracked E2E test | quality-review | quality, E2E, verification |
| production correction | implement | convergence and every later stage |

A spec correction regenerates plan and tasks. A plan correction regenerates tasks. A task correction changes tasks only. Hash invalidation does not consume a review attempt.

## Convergence Loop

`converged` advances. `tasks_appended` returns to implementation and then converge. Behavior-changing tasks use TDD; test-only, documentation, and non-executable metadata use bounded normal execution without fabricated RED. An intent or authority defect returns to definition.

Stop on the third non-converged pass unless each pass strictly reduces the open set and the next work remains bounded.

## Legacy v1 Migration

Existing schema-1.0 states remain readable evidence. They cannot resume mutation directly.

1. Validate the historical state with the archived v1 schema.
2. Resolve its BMad mapping, checkpoint trailers, ancestry, artifact hashes, and owned paths.
3. Map the last proven historical stage to the earliest unified owner:
   - context/spec-review -> specify;
   - plan/plan-review -> plan;
   - tasks/analyze -> tasks;
   - implement -> implement;
   - acceptance/quality review -> converge;
   - conditional-e2e/verify -> quality-review;
   - complete -> terminal v2 normalization only when the original bytes, current BMad `done` status, ancestral completion-state binding, and ancestral `done` transition are all proven; otherwise return to `quality-review`.
4. Write a schema-2.0 state that preserves v1 state/evidence paths as superseded history and records the migration decision.
5. Revalidate before any mutation.

The expired `expedited-mvp` value is historical evidence only and grants no authority.

A legacy completion may bind state and `done` in one commit or in two separately proven ancestral commits. The migration report is the compatibility bridge; it preserves the old state hash, both commit proofs, and whether the legacy trailer vocabulary differs from v2. This normalization does not rerun delivery or convert malformed legacy fields into current evidence.

## Resume and Failure

Resume validates activation/mapping, schema version, branch and Git/worktree identities, exact status/ownership, artifact hashes, evidence, boundary identities, trailers, and ancestry. Completed stages with identical inputs are no-ops. Restart at the earliest invalid owner and preserve superseded evidence.

Record retryable tool failures without advancing. Stop for unresolved intent/authority, three REVISE outcomes, BLOCKED, divergent history, destructive recovery, inseparable user work, unavailable required infrastructure, unsafe external action, or ambiguous E2E failure. Never repair through reset, rebase, force-push, implicit merge, or silent branch switching.

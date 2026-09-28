# Governed Delivery State Machine

## Sources of Truth

The configured adapter owns coarse lifecycle. Story-local `workflow-state.json` is a compact resumable snapshot and cannot override it. New states use `3.1`; historical `3.0` remains readable under its original contract. Detailed history stays in durable artifacts and Git. BMad identity fields remain for adapter compatibility.

## Initial BMad Lifecycle Adapter

The story vocabulary is `backlog`, `ready-for-dev`, `in-progress`, `review`, and `done`.

| From | Gate | To |
|---|---|---|
| backlog | BMad sprint planning/readiness creates a bounded story | ready-for-dev |
| ready-for-dev | queue resolution, preflight, ownership, and activation succeed | in-progress |
| in-progress | all current tasks pass, convergence is clean, and the candidate is pinned | review |
| review | current lookback, quality, E2E decision, final verification, resolved recovery, and checkpoint binding succeed | done |

The epic vocabulary is `backlog`, `in-progress`, and `done`. First story activation starts the epic. Only close-governed-epic may complete it.

## Unified Internal Stages

```text
context, specify, plan, plan-review, tasks, definition-review,
implementation-approval, implement, converge, lookback, quality-review,
conditional-e2e, verify, complete
```

Implementation design is classified inside `plan` as `none`, `bounded`, or `architecture`; it is not a stage. `plan-review` appears for a repository planned-change trigger or `architecture`. Before entering it, the root maps the plan path/hash into state, requires explicit governed classification/reason, and runs state-bound deterministic validation. The review must accept, and the named authorization (human by default, or an explicitly designated delegate for this gate) must be durable, before `tasks`.

| Stage | Owner | Exit |
|---|---|---|
| context | root; optional context synthesizer | pinned authority set and separable ownership |
| specify | persistent definition author | specification plus successful self-review |
| plan | same definition author follow-up; current escalation exception when triggered | complete plan, valid Markdown implementation-design contract, and successful self-review |
| plan-review | retained definition reviewer in plan-only mode | accepted verdict and scoped human or permitted delegated authorization |
| tasks | same definition author follow-up | dependency-ordered tasks, complete design-ID coverage, and successful self-review |
| definition-review | same retained definition reviewer, or history-complete recovery replacement | one accepted spec/plan/tasks/analyze verdict |
| implementation-approval | root and user or explicitly designated delegate | accepted definition hashes explicitly authorized for production implementation |
| implement | retained task-group test author then implementer | honest RED, minimal GREEN, required final-shape assessment by both owners, conditional local refactor, final surrounding GREEN; or truthful nonbehavioral evidence |
| converge | convergence analyst | clean outcome; reused/new tasks to existing owner; definition defect to earliest owner |
| lookback | root | bounded Story Lookback section, selective lessons/approved-doc synchronization, current proof |
| quality-review | fresh quality reviewer | accepted converged boundary |
| conditional-e2e | root; E2E runner when required | passing evidence or complete current-boundary override |
| verify | root | focused tests, configured build/full-test commands, required risk checks, unchanged boundary |
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

A spec correction invalidates plan classification. The active author classifies afresh on spec or classification/trigger findings. Before recovery takeover, it completes `none|bounded` or uses the retained plan-only Sol/high exception for architecture; tasks return to the ordinary author. After takeover, the retained recovery author handles all classifications and definition phases. Preserve valid text, IDs, code/tests, and completed work, and reconcile affected tasks/checks; go-back is not Git rollback. Hash invalidation does not consume or reset recovery. Meaningful later findings or changed lookback inputs/outputs invalidate its proof before quality/completion; routine record bookkeeping does not.

## Convergence Loop

For a material late reply or other changed boundary, root declares the earliest affected `owner` on the existing invalidation event. It may move recovery earlier, never past an unresolved earlier obligation. Explicit invalidation retires the old causal diagnosis without resetting counts/limits or releasing holds. The current event-policy discriminator preserves historical unmarked receipt replay; see `artifact-contracts.md`. An unanswered optional question causes no event.

`converged` advances to root lookback. `tasks_reused` and `tasks_appended` return to the appropriate code/test owner then convergence; mixed results use `tasks_appended`. Task-description/dependency changes route to the definition author. Behavior changes use TDD; other work uses truthful direct verification. `return_to_definition` takes precedence over speculative appends and identifies `spec|plan|tasks`; authority conflicts require a decision. Unfinished prerequisite work returns to its existing task before convergence is invoked.

One episode spans every unresolved correction, including newly discovered defects and upstream moves. Initial negative result sets `failed_results=1`; failed repairs produce 2, 3, 4. Diagnose at 3 before another repair; at 4, pause until an accepted finite extension bound to episode/scope raises `pause_at`. Count each authoritative result once; unchanged reports/checks, expected RED, BLOCKED, and technical failure do not increment. Neither input replacement nor intermediate ACCEPT resets it. See `review-and-correction.md` for technical retry and diagnosis contracts.

A pre-production episode closes on complete-definition ACCEPT plus required earlier plan conditions. Production entry promotes closure to final verification; post-implementation episodes require the final current convergence/quality/E2E/verified boundary. The closure result advances `transition_head` and sets `recovery` null; never delete the head or active author profile. A fourth-result paused snapshot is structurally valid; the targeted guard forbids the next repair.

## Resume and Failure

Resume validates mapping, version/size, checkout identity, artifacts, transition-head/result projection, selected author, current lookback/evidence, and checkpoint binding. Reuse author/reviewer sessions; replacements receive raw artifacts, governing decisions, failed approaches, current stage/boundary, and ordered verdict paths. Never persist runtime handles. Run the narrow guard before advancement; a stale result, erased episode, or superseded author cannot restart work. A pending complete checkpoint is trusted only after unique ancestral binding, exact trailers, committed-state proof, and the lifecycle done transition. New 3.1 lookback proofs resolve from the completion commit, not today's project memory.

Active 3.0 adoption is a single root-owned bounded transition to 3.1, never an installer rewrite. Inspect current evidence/relevant verdicts, preserve known failures, and retain a fourth-failure pause. Old zero counters do not prove no failures; diagnose uncertain allowance and request bounded continuation if it remains indeterminate. An accepted boundary with no unresolved correction may initialize null recovery. Historical completed 3.0 state/checkpoints remain unchanged and need no lookback retrofit. Upgrade skills/validator together; no partial downgrade after adoption.

Persist technical operation identity/invocation count/result in existing failure evidence referenced by `resume`; at most three root-issued invocations including original, across resume. Stop earlier for fatal/unavailable infrastructure or authority; never use an author upgrade to fix an environment. Existing ownership, unsafe-action, divergence, and ambiguous E2E stops remain. Never repair through reset, rebase, force-push, implicit merge, or silent branch switching.

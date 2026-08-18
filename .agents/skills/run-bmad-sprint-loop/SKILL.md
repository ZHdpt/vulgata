---
name: run-bmad-sprint-loop
description: Use when implementing or resuming one or more BMad stories or epics under repository-governed delivery controls.
---

# Run BMad Sprint Loop

Deliver each BMad story as one recoverable SpecKit feature. BMad owns queue and lifecycle; SpecKit owns definition and convergence; the installed `tdd` discipline owns behavior changes; repository authorities own invariants; the active root owns state, evidence, gates, checkpoints, and Git.

Read only the branch references needed by the current stage:

- lifecycle and resume: `docs/agent-workflow/state-machine.md`;
- durable records and dispatch shapes: `docs/agent-workflow/artifact-contracts.md`;
- roles and model escalation: `docs/agent-workflow/agent-catalog.md`;
- correction routing: `docs/agent-workflow/review-and-correction.md`;
- E2E predicates: `docs/agent-workflow/e2e-decision-matrix.md`;
- operator recovery: `docs/agent-workflow/operator-runbook.md`.

Use Vulgata terms from `docs/UBIQUITOUS_LANGUAGE.md` verbatim. Surface Constitution, ADR, context, or PRD conflicts instead of resolving authority by inference.

## Inputs and Queue

Resolve exactly one target: `next`, `resume`, one story, one epic, or a dependency-ordered list. `dry-run` reports resolution, ownership, stages, agents, gates, checkpoints, and E2E predicates without mutation. Reject ambiguous keys, duplicate activation, and new work for `done` stories. Never expand scope to an unmet prerequisite without user authorization.

New activations use `schema_version: "2.0"` and `execution_level: "unified"`. Historical v1 `full`, `standard`, and `expedited-mvp` states are read-only until the migration procedure in the state machine records their earliest valid unified stage. The expired August 15 exception cannot authorize new or resumed work.

## Preflight and Ownership

Before mutation, record branch, full `HEAD`, repository root, Git common directory, worktree path, exact porcelain/index state, owned paths, unrelated paths, and observation time. Load the selected BMad artifacts, Constitution, relevant ADR/context documents, repository instructions, and current mapped state. Require a non-detached branch unless the host provides an explicit native handoff. Preserve unrelated work and stop when owned and unrelated changes are inseparable.

The active root is the sole lifecycle/state writer and committer. Workers never stage or commit. The root stages explicit owned paths only after refreshing preflight, inspecting the staged diff, running `git diff --cached --check`, and completing checkpoint verification. No implicit branch switch, reset, rebase, merge, push, force-push, history rewrite, or pull request.

## Unified Story Flow

```text
preflight/context
-> speckit-specify + self-review
-> speckit-plan + self-review
-> conditional early plan-review + planned-change authorization
-> speckit-tasks + self-review
-> independent definition-review
-> TDD implementation
-> speckit-converge <-> implementation until converged
-> independent quality-review
-> conditional-e2e
-> pinned verification
-> complete
```

### 1. Define

Dispatch authors serially over one pinned authority set:

- `spec-author` (`gpt-5.6-sol`, medium) runs `speckit-specify`, then self-checks bounded intent, measurable acceptance, terminology, and placeholders.
- `plan-architect` (`gpt-5.6-sol`, medium) runs `speckit-plan`, then self-checks feasibility, repository fit, failure behavior, test seams, and Constitution/ADR alignment. Use the conditional `plan-architect-high` (`gpt-5.6-sol`, high) only when the plan makes architecture decisions; record the decision trigger in the dispatch.
- `task-author` (`gpt-5.6-luna`, medium) runs `speckit-tasks`, then self-checks complete source-reference coverage, exact paths, dependency order, and vertical test-first slices.

Self-review is part of each authoring turn, not an independent gate or extra worker.

When `AGENTS.md` identifies a planned-change trigger, dispatch `definition-reviewer` in plan-only mode after the plan. An accepted independent verdict is required before the root requests the named user authorization and before task generation. Approval authorizes only progression to tasks. For untriggered work, continue directly to tasks.

After tasks, dispatch one fresh `definition-reviewer` (`gpt-5.6-sol`, medium) over the raw BMad story, specification, complete plan set, tasks, Constitution, ADRs, contexts, and relevant source. It runs the read-only `speckit-analyze` checks and judges BMad alignment, feasibility, testability, terminology, coverage, task ordering, and unresolved placeholders in one verdict.

Every finding names its earliest owner: `spec`, `plan`, or `tasks`. `REVISE` returns raw findings to that author, regenerates all downstream artifacts, and reruns the independent review. A spec correction regenerates plan and tasks; a plan correction regenerates tasks; a task correction changes tasks only. Stop on `BLOCKED`, unresolved intent/authority, or the third `REVISE`. Critical/High analysis remediation still requires explicit user approval before artifact edits.

### 2. Implement with TDD

Partition accepted tasks into coherent dependency-ready behavior slices. For every observable production-behavior change:

1. a `tdd-test-author` (`gpt-5.6-luna`, high) writes one public-seam behavioral test and observes the expected RED;
2. a `tdd-implementer` (`gpt-5.6-sol`, medium) makes the minimal production change to reach GREEN;
3. the test author verifies the focused and surrounding tests;
4. the root records evidence and checkpoints a coherent task group.

Reuse the same test author through follow-up turns for a coherent group. Keep production implementation at `gpt-5.6-sol`, medium; architecture decisions must already be accepted in the plan. Refactoring is review-stage work performed while green; it is not evidence claimed inside the red-green loop.

Existing-correct behavior that only lacks a test is test-only work and must not fabricate RED. Documentation and non-executable metadata use bounded normal execution plus direct verification. Behavior-preserving refactors require characterization coverage when current tests do not protect the seam.

### 3. Converge

After the current tasks are implemented, dispatch `convergence-analyst` (`gpt-5.6-luna`, high) to run `speckit-converge`. It may append only traceable Convergence tasks to `tasks.md`; it never edits intent artifacts or application code.

- `converged`: pin the candidate and advance.
- `tasks_appended`: classify every task. Missing, partial, contradictory, or removed observable behavior returns through TDD. Test-only, documentation, and non-executable metadata use bounded normal execution. Then rerun convergence.
- an intent/authority defect that cannot be repaired as remaining implementation work returns to the earliest definition owner instead of being hidden as a code task.

Stop on the third non-converged pass unless the latest pass strictly reduced the open set and the next work is already bounded; never loop without measurable progress.

### 4. Review, E2E, and Verify

Dispatch one fresh `quality-reviewer` (`gpt-5.6-sol`, medium) over the converged pinned boundary. It judges correctness, security, edge cases, tests, architecture seams, regression risk, and repository standards; convergence supplies implementation-to-definition coverage, so the reviewer does not repeat a separate acceptance ceremony.

`REVISE` returns raw findings to the applicable test/implementation or definition owner. Behavior correction requires regression RED; definition correction invalidates convergence. After correction, rerun every invalidated downstream stage. Stop on `BLOCKED` or the third `REVISE`.

Apply the E2E decision matrix. Required E2E uses `e2e-runner` (`gpt-5.6-luna`, high), which may edit tests but never production. Product failure returns to TDD; a test defect stays with the runner; ambiguous failure stops for user direction. Any tracked test edit invalidates quality review.

Final verification runs story-specific tests, Web build, full tests, required security/migration checks, and confirms the reviewed boundary is unchanged. A coverage-based E2E skip must name current-boundary tests proving every triggered observable predicate.

## Checkpoints, Resume, and Completion

Create recovery checkpoints only when durable content exists: activation; accepted definition; verified implementation group; converged review candidate; correction; completion. Adjacent artifact-only work shares the accepted-definition checkpoint. Every commit uses an explicit path manifest and unique workflow trailers; completion is trusted only after the pending checkpoint ID resolves uniquely to an ancestral commit.

On resume, validate mapping, schema version, branch/Git/worktree identity, ownership, artifact hashes, stage evidence, review boundary, checkpoint trailers, and ancestry. Restart at the earliest invalid owner and preserve superseded evidence. Hash invalidation is not a judge revision. Record retryable tool failure without advancing; stop for divergent history, destructive recovery, inseparable ownership, unsafe external action, unavailable required infrastructure, or authority conflict.

Complete a story only when definition is accepted, every task is complete, behavior changes have honest RED/GREEN evidence, convergence is clean, quality review accepts the current boundary, E2E is passed or validly skipped, final verification passes, and `review -> done` is bound to the completion checkpoint. Report story, feature directory, checkpoint SHAs, tests/build, convergence, review, E2E, residual nonblocking findings, preserved unrelated work, and next queue item. Close an eligible epic only through `$close-bmad-epic`.

## Dispatch Contract

Use direct-child project roles only; workers may not delegate. Every dispatch supplies the seven slots in `references/dispatch-contract.md`: role/objective, authoritative inputs, workflow boundary, constraints/skills, write boundary, output/evidence, completion/failure. Send judges raw pinned artifacts and correcting workers unchanged raw findings. A validated independent `ACCEPT`, clean convergence, passing E2E, or passing verification advances without duplicate user confirmation.

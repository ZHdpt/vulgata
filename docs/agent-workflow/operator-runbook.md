# Sprint Loop Operator Runbook

## Start Prompts

Use these prompts exactly for the common entry points:

```text
Use $run-bmad-sprint-loop for story 4-1.
```

```text
Use $run-bmad-sprint-loop for epic 4.
```

```text
Use $run-bmad-sprint-loop for epics 4 and 5.
```

```text
Use $run-bmad-sprint-loop to resume the active story.
```

```text
Use $run-bmad-sprint-loop for story 4-1 in dry-run mode.
```

```text
Use $close-bmad-epic for epic 4.
```

The root agent assumes the orchestrator role. Worker agents are direct children; no intermediate orchestrator is spawned.

## What the Operator Should See

For a story request, the orchestrator reports the resolved BMad key/status, one mapped SpecKit feature directory, current branch/preflight `HEAD`, repository/Git/worktree identity, exact owned and unrelated status paths, current internal stage, the next gate, and any required human decision. Epic and multi-epic requests show the dependency-ordered story queue and execute one story lifecycle at a time where mutations overlap.

Dry-run mode validates queue resolution, source authorities, mapping proposal, direct-child dispatch plan, stage sequence, E2E predicates, verification commands, and expected checkpoints. It does not create the SpecKit feature, edit feature/BMad/source files, stage, or commit.

Resume validates the two-way mapping, artifact hashes, checkpoint trailers/ancestry, and worktree ownership. For workflow-activated stories it also requires the canonical story activation record. An `in-progress`/`review` story that predates activation records may resume without that file only when its BMad development record and existing SpecKit `workflow-state.json` form a valid, unique two-way mapping and all checkpoint/artifact/ownership checks pass. It restarts at the earliest invalid or incomplete stage and stops rather than repairing divergent history.

## Required Human Gates

The orchestrator pauses for explicit human direction when:

- `speckit-analyze` reports a Critical or High finding: approve the presented artifact-specific remediation before any analyzed artifact is edited;
- a correction or conflict requires choosing, expanding, or changing product intent or scope rather than restoring an already accepted contract;
- a gate reaches three `REVISE` outcomes (`BLOCKED` stops immediately and does not increment this counter);
- authorities conflict, including a Constitution/ADR/product-source conflict;
- checkpoint ancestry diverges, a destructive VCS recovery is proposed, or active-story changes cannot be separated from unrelated user edits;
- required verification needs unavailable infrastructure or an unsafe external action;
- an E2E failure cannot be classified as product or test failure;
- the epic audit emits Strong recommendations: explicitly approve each recorded disposition as resolved, rejected with a load-bearing reason, deferred through an explicit tracked decision, or accepted;
- any ADR conflict is found during epic closure: explicitly approve its recorded disposition.

A validated `ACCEPT` from an independent spec, plan, acceptance, or quality judge is the approval for that stage. The orchestrator persists it and advances without asking the operator to approve the same result again. Normal stage advancement, direct-child dispatch, local tests, explicit-path staging, and contract-defined local checkpoint commits likewise require no repeated prompt once the workflow has been invoked. Every reserved decision listed above retains its explicit human gate.

## Expected Local Commits

The orchestrator is the sole committer and creates recoverable local checkpoints when their preconditions pass:

| Checkpoint | Expected content |
|---|---|
| BMad story prepared | Validated story file and transition to `ready-for-dev` |
| BMad story activated | Canonical story-keyed activation record, story `ready-for-dev → in-progress`, and parent epic `backlog → in-progress` when applicable, after context/preflight and before specification |
| Specification accepted | Accepted `spec.md`, checklist, mapping/state, and gate evidence |
| Plan accepted | Accepted complete plan artifact set and Constitution evidence |
| Tasks analyzed | `tasks.md`, accepted read-only analysis evidence, and state |
| Task group verified | One cohesive implementation/test increment with red-green-refactor evidence |
| Story ready for review | Story record and sprint-status `in-progress → review` after all implementation groups, before reviewer dispatch |
| Review correction verified | Focused regression test, correction, and passing fresh review evidence |
| Story completed | Final story/state/task metadata and BMad `review → done` after accepted reviews, E2E decision/result, and full verification |
| Epic gate completed | Complete latest-HEAD architecture record, retrospective/status artifacts, and epic `in-progress → done` transition |

Adjacent artifact-only checkpoints may combine only when no useful recovery boundary is lost. Unaccepted artifacts never combine with implementation, and unrelated stories never combine. Each commit uses explicit owned paths, passes `git diff --cached --check`, and records the checkpoint ID in its durable record. Story commits carry `BMad-Story` and `Workflow-Scope: story/<key>`; epic commits carry `BMad-Epic` and `Workflow-Scope: epic/<key>`. Both carry `Workflow-Stage` and `Workflow-Checkpoint`.

These are local commits only. The workflow does not switch branches, push, merge, rebase, rewrite history, open a pull request, or otherwise mutate a remote unless the user separately requests that distinct action.

## Recovery

- `REVISE`: a fresh responsible author/implementer receives raw findings, corrects within scope, and the affected gate reruns.
- `BLOCKED`: the workflow records evidence and waits without advancing lifecycle state.
- Failed tool/agent: retry the same stage with a fresh direct child when the recorded boundary is still valid.
- Missing/malformed artifact: resume from its earliest owning stage.
- Product E2E failure: return to fresh TDD implementation, rerun both reviews, then rerun E2E.
- Defective E2E test: `e2e-runner` corrects test code only; because a tracked test changed, rerun both reviews and then E2E.
- Dirty worktree: preserve and exclude unrelated edits; stop if ownership cannot be separated.

## Epic Close Sequence

1. Verify every epic story is `done` and no blocking workflow state remains.
2. Confirm the epic is `in-progress`; run full build/tests at the current commit.
3. Dispatch the read-only architecture auditor using `improve-codebase-architecture`.
4. Keep its HTML report in the OS temporary directory; durably record and verify its hash, source commit, complete findings, evidence, and dispositions.
5. Resolve all Strong recommendations and surface every ADR conflict through the human gate. If remediation changes tracked files or `HEAD`, rerun full build/tests and the architecture audit at the new `HEAD`.
6. Offer the BMad retrospective; run it or record the user's explicit decline.
7. Transition the epic `in-progress → done` and create the local epic checkpoint only after the current-HEAD gate and retrospective decision pass.

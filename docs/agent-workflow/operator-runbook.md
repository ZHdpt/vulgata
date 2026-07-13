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
- the epic audit emits a Critical/High finding, violated MUST, Strong recommendation, missing/invalid classification, or ADR conflict: explicitly approve each allowed finding-specific disposition; deferred Strong findings require an external tracked identifier, owner, rationale, and due/closure condition;
- an ADR conflict is found: choose only conform implementation, supersede/amend the ADR through a tracked ADR decision, or block closure.

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
| Epic gate completed | Complete exact-source architecture record, every referenced command-evidence file, accepted disposition/ADR artifacts, retrospective choice, and provisional epic `in-progress → done` transition |

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

1. Verify every story is bound `done`, the epic is `in-progress`, and no blocking/provisional workflow state remains.
2. Pin `HEAD`. Create a clean disposable detached build/test worktree at that SHA and a separate fresh audit worktree. Prove commit/index/tree equality and no untracked audit input. Run full build/tests in the first; store full-schema command evidence plus hashed raw output under the epic gate `evidence/` directory.
3. Create a separate fresh clean audit checkout at the same SHA. The root runs the documented `epic-close-audit` profile, dispatching `epic-architecture-auditor` as direct-child independent Explore/classification judge; grilling and side effects are deferred to corrective work.
4. Canonically preserve the complete raw response and hash. Verify every candidate's blob/excerpt citations against the checkout and map stable raw IDs one-to-one without weakening classifications. Generate visible HTML cards from the normalized payload and validate every field/hash.
5. Recheck source equality and resolve all reserved findings. Deferred Strong findings require external tracked work. ADR conflicts may conform, supersede/amend via tracked ADR decision, or block.
6. Route production/test remediation through BMad correct-course and `$run-bmad-sprint-loop`; keep the epic `in-progress` and restart closure at new `HEAD`. Do not commit code remediation from the close skill.
7. Offer the retrospective and record explicit `run-after-close` or `declined`. Preflight all retrospective activation/append/completion hooks, but do not create retrospective state or run it inside the close transaction.
8. Dispose and separately record both clean worktrees before pending binding. New transactions require an empty index and clean target preimages. Hash the canonical rename-disabled parent-to-result change set, including explicit deletion tombstones and the final gate blob, and bind it in `Workflow-Manifest-SHA256`. Trust `done` only after direct-parent recomputation; mixed-index recovery preserves all bytes without writing.
9. If scheduled, run `$bmad-retrospective` after binding under `retrospective-state.json`, allowlisted writes, and a separate checkpoint/resume. Its learning creates follow-up work, not close evidence.

Before close commit and again during recovery, run the executable contract with `python .agents/skills/run-bmad-sprint-loop/scripts/validate_workflow.py --repo . --epic-gate <gate-path>`. Use `--epic-evidence` for an individual command record and `--retrospective-state` for the post-close transaction. Pending-binding validation derives repository identities, raw audit/citations, preimages, the complete staged/committed change set, evidence files, and checkpoint trailers from Git rather than trusting booleans in the record.

Every pre-binding stop atomically updates canonical `architecture-gate.json` with transaction phase, worktree/index snapshots, status, earliest safe resume step, exact source/boundary, evidence paths, retryability, and failure. Resume resolves pending binding first, otherwise revalidates or restarts Step 1. Post-binding retrospective failure updates only its separate state.

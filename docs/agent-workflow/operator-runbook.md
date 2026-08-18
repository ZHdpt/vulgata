# Sprint Loop Operator Runbook

## Start

Examples:

```text
Use $run-bmad-sprint-loop for next.
Use $run-bmad-sprint-loop for story 4-2.
Use $run-bmad-sprint-loop for epic 4.
Use $run-bmad-sprint-loop resume.
Use $run-bmad-sprint-loop dry-run for epic 4.
```

New work always uses the unified v2 flow. A historical Full, Standard, or expedited state first runs the migration branch in the state machine.

## Expected Progress

The operator should see compact stage updates, not repeated ceremony explanations:

1. selected story, dependencies, branch/HEAD, ownership;
2. spec/plan/tasks self-review results;
3. one independent definition verdict;
4. RED/GREEN evidence by coherent behavior group;
5. convergence outcome and appended-task count;
6. one quality verdict;
7. E2E decision and final verification;
8. bound checkpoint and next queue item.

## Human Gates

Pause only for:

- planned-change authorization after accepted early plan review and before tasks;
- Critical/High definition remediation;
- unresolved product intent, authority conflict, BLOCKED, or third REVISE;
- inseparable ownership or divergent/destructive Git recovery;
- unavailable required infrastructure or unsafe external action;
- ambiguous E2E failure;
- reserved epic architecture findings and retrospective choice.

Independent ACCEPT, clean convergence, passing commands, and safe checkpoint binding do not need repeated confirmation.

## Checkpoints

Ordinary recovery boundaries are activation, accepted definition, each coherent verified implementation group, converged review candidate, real correction, and completion. Stage explicit owned paths only. Preserve unrelated work. Never blanket-stage.

## Recovery

Use the state machine's earliest-owner table. Do not reconstruct missing verdicts, fabricate RED, guess mappings, or repair Git history. Historical v1 state is evidence input to an explicit v2 migration, never silently relabeled.

## Epic Close

After all stories are bound done, use close-bmad-epic. It verifies a clean pinned commit, runs the architecture audit, resolves reserved dispositions, binds epic done, then runs or declines the retrospective as selected.

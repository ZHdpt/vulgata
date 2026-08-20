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

New and resumed work uses the compact schema-v3 unified flow.

## Expected Progress

The operator should see compact stage updates, not repeated ceremony explanations:

1. selected story, dependencies, branch/HEAD, ownership;
2. spec/plan/tasks self-review results;
3. one independent definition verdict;
4. one informed implementation approval;
5. RED/GREEN evidence by coherent behavior group;
6. convergence outcome and appended-task count;
7. one quality verdict;
8. E2E decision, final verification, bound checkpoint, and next queue item.

## Human Gates

Pause only for:

- planned-change authorization after accepted early plan review and before tasks;
- accepted-definition implementation approval before the first production write;
- contradiction with prior user approval/higher authority, or a new architecture decision outside the accepted plan;
- unresolved product intent, hard blocker, BLOCKED, or the fourth consecutive REVISE/non-converged result in one phase;
- inseparable ownership or divergent/destructive Git recovery;
- unavailable required infrastructure or unsafe external action;
- ambiguous E2E failure;
- reserved epic architecture findings and retrospective choice.

Ordinary REVISE correction, independent ACCEPT, clean convergence, passing commands, and safe checkpoint binding do not need confirmation. Every real approval request supplies the decision, brief, files/authorities, options, recommendation and consequences, scope hash, and remaining gates.

## Checkpoints

Ordinary recovery boundaries are activation, accepted definition, each coherent verified implementation group, converged review candidate, real correction, and completion. Stage explicit owned paths only. Preserve unrelated work. Never blanket-stage.

## Recovery

Use the state machine's earliest-owner table. Do not reconstruct missing verdicts, fabricate RED, guess mappings, or repair Git history. Detailed history is recovered from durable evidence and Git, not accumulated in workflow state.

## Epic Close

After all stories are bound done, use close-bmad-epic. It verifies a clean pinned commit, runs the architecture audit, resolves reserved dispositions, binds epic done, then runs or declines the retrospective as selected.

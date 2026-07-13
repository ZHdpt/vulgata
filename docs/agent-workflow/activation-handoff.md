# Workflow Activation Handoff

The implemented workflow is committed on branch `codex/bmad-speckit-sprint-loop`. Codex discovers repository skills when a session starts, so the session root must contain that branch's `.agents/skills/` and `.codex/agents/` files.

## Use the Existing Worktree

```powershell
Set-Location G:\source\repos\vulgata\.worktrees\codex-sprint-loop
git branch --show-current
git status --short
codex
```

Require branch `codex/bmad-speckit-sprint-loop` and a clean status. Start a new Codex session after changing checkout/worktree; an already-running session rooted at ordinary `main` will not retroactively discover these skills.

Then use an exact entry prompt from [the operator runbook](operator-runbook.md), for example:

```text
Use $run-bmad-sprint-loop for epics 4 and 5.
```

The current Vulgata queue will pause before Story 4.1 until the out-of-scope `SP.1 Worker Agent Execution Adapter Validation Spike` is authorized/completed or its blocking boundary is changed through BMad course correction.

## Integrate Later

If a human chooses to integrate the feature branch into `main`, perform that Git operation separately, resolve it under the repository's normal review policy, and restart Codex from the resulting checkout. The workflow never merges, rebases, pushes, or opens a PR implicitly.

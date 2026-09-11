# Workflow activation guidance

Use the assigned current checkout, root [AGENTS.md](../../AGENTS.md) and
[project state](../agents/project-state.md). Verify Git identity, local changes and the
[current tracker](../bmad/implementation-artifacts/sprint-status.yaml) before resolving
work. No specific old branch or worktree is required.

The generic replacement backlog is defined in [epics](../bmad/epics.md). At the
September 11 planning checkpoint it begins with Story 13.1; read live tracking rather
than treating this dated sentence as an active feature selector. Historical Epics 4,
11 and 12, the SP.1 checkpoint and old specs remain evidence, not a resumed queue.

Use the current repository skill with the matching harness/role configuration. Codex
roles implement the unified workflow; legacy Copilot delivery adapters are fenced
until separately synchronized. Start a fresh session after changing installed skills
or role configuration when the harness loads those at session startup.

See [the operator runbook](operator-runbook.md) for invocation and gates. A safe
orientation request is:

```text
Use $run-bmad-sprint-loop dry-run for next.
```

Deliberately activate the selected story's feature before feature-specific commands.
Pass its actual plan path when refreshing agent context; never infer activation from
a recently modified historical plan. Preserve unrelated working-tree changes and
follow workflow ownership. Git integration/cleanup is a separate authorized action;
this handoff does not require a reset, branch switch, merge, rebase or commit.

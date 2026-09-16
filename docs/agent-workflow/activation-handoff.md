# Workflow activation guidance

Use the assigned checkout, [AGENTS.md](../../AGENTS.md),
[project state](../agents/project-state.md) and the
[live tracker](../bmad/implementation-artifacts/sprint-status.yaml). Verify current
Git identity, local changes and ownership before mutation; no old branch or worktree
is an instruction to switch checkouts.

After the 0.7 installation, no story is active. Story 13.1 is complete and 13.2
remains backlog; resolve the live queue before starting anything. Historical Epics
4/11/12, old specs, activation receipts and pending-shaped historical metadata are
evidence, not automatic resume instructions.

Start a fresh Codex session to discover the installed skills and governed profiles.
Only that harness currently supports governed delivery; unrelated Copilot helpers
are not replacements. A read-only orientation request is:

```text
Use $run-governed-delivery dry-run for next.
```

Read the configured adapters in `.governed-delivery/config.toml` and the installed
[operator runbook](../../.agents/skills/run-governed-delivery/references/operator-runbook.md).
Deliberately activate the authorized story's feature before feature-specific
commands, then pass its actual plan path when refreshing agent context. Never choose
a historical plan by modification time. Preserve consumer approval gates and
unrelated changes. Installation or this handoff grants no implementation, reset,
branch-switch, merge, rebase or push authority.

See [adoption](adoption-0.7.md) for verification and historical-state limitations.

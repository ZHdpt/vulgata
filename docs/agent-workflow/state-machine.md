# Governed delivery state machine

Use the installed
[state-machine.md](../../.agents/skills/run-governed-delivery/references/state-machine.md)
for stages, invalidation, recovery, lookback and completion. The configured BMad
tracker owns coarse lifecycle; story-local state cannot override it.

New activations use schema 3.1. Preserve completed and retired-story snapshots;
installation does not adopt or resume them. See [adoption](adoption-0.7.md) for the
historical checkpoint boundary and [project state](../agents/project-state.md) for
the current queue.

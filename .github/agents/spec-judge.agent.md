---
name: spec-judge
description: "Legacy standalone spec-judge adapter; not a current unified definition gate."
tools: [read, search]
agents: []
user-invocable: false
---

# Legacy Copilot adapter

The unified workflow uses self-review and the current independent definition-reviewer boundary, not this retired standalone gate. This file is retained for discoverability and historical reference;
it must not dispatch or certify current governed story/epic work.

Use the current definition-reviewer contract in a compatible harness.

Read root [AGENTS.md](../../AGENTS.md), [project state](../../docs/agents/project-state.md),
the current [workflow architecture](../../docs/agent-workflow/architecture.md) and
[agent catalog](../../docs/agent-workflow/agent-catalog.md). Report the compatibility
limit instead of recreating missing roles, granting tools, choosing substitute models
or executing the retired recipe.

Adapter synchronization is a separate bounded task. Its old tool/model metadata is
not execution authority or a claim of current harness parity. No implementation,
lifecycle update, staging, commit, service startup or cleanup is authorized by this
legacy entry point. Historical instructions remain available in Git; read them only
for an explicitly historical request, never as current defaults.

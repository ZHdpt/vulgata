---
name: plan-judge
description: "Legacy plan-judge adapter; not the current plan-only or combined definition review gate."
tools: [read, search]
agents: []
user-invocable: false
---

# Legacy Copilot adapter

Plan-only triggered review and combined definition review are owned by the current definition-reviewer contract. This file is retained for discoverability and historical reference;
it must not dispatch or certify current governed story/epic work.

Use the current plan-review/definition-review stages and compatible definition-reviewer role.

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

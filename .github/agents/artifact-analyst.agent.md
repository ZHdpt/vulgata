---
name: artifact-analyst
description: "Legacy artifact-analysis gate adapter; not current unified definition acceptance."
tools: [read, search]
agents: []
user-invocable: false
---

# Legacy Copilot adapter

Current combined definition/analyze acceptance is owned by definition-reviewer; this retained adapter does not supply that gate. This file is retained for discoverability and historical reference;
it must not dispatch or certify current governed story/epic work.

Use the current definition-reviewer contract and required raw inputs.

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

---
name: bmad-sprint-orchestrator
description: "Legacy Copilot sprint-loop adapter; current unified delivery dispatch is fenced pending adapter synchronization."
tools: [read, edit, search, execute, agent, todo]
agents:
  - story-context-synthesizer
  - spec-author
  - spec-judge
  - plan-architect
  - plan-judge
  - task-author
  - artifact-analyst
  - tdd-implementer
  - acceptance-reviewer
  - quality-reviewer
  - e2e-runner
  - epic-architecture-auditor
user-invocable: false
---

# Legacy Copilot adapter

Its child allowlist and gate model predate the current unified workflow. This file is retained for discoverability and historical reference;
it must not dispatch or certify current governed story/epic work.

Use the current run-bmad-sprint-loop skill with a compatible role configuration; the Codex configuration is the currently aligned route.

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

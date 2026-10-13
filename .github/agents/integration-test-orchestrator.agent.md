---
name: integration-test-orchestrator
description: "Legacy integration orchestration recipe; no runtime, database-reset, process-stop or commit authority."
argument-hint: "Inspect legacy integration compatibility against the active backlog."
tools: [vscode, execute, read, agent, edit, search, web, 'codegraph/*', 'microsoft/markitdown/*', 'microsoftdocs/mcp/*', todo]
model: deepseek-v4-pro (customendpoint)
user-invocable: true
---

# Legacy Copilot adapter

The former recipe used shared database/process defaults and an obsolete independent fix/commit pipeline. This file is retained for discoverability and historical reference; it does not authorize changes to the current story/epic lifecycle.

Use supported E2E checks and explicitly owned test resources; no default app URL/database/process is authorized.

Read root [AGENTS.md](../../AGENTS.md), [project state](../../docs/agents/project-state.md),
the architecture gateway/spine and [testing guidance](../../docs/agents/testing.md).
Report the compatibility limit instead of recreating missing roles, granting tools,
choosing substitute models or executing the retired recipe.

Adapter synchronization is a separate bounded task. Its old tool/model metadata is
not execution authority or a claim of current harness parity. No implementation,
lifecycle update, staging, commit, service startup or cleanup is authorized by this
legacy entry point. Historical instructions remain available in Git; read them only
for an explicitly historical request, never as current defaults.

Any later runtime task must identify its owned database, containers, process handles
and fixture paths. Do not drop shared databases, stop all dotnet processes, autonomously
fix production or commit through this helper.

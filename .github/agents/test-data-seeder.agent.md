---
name: test-data-seeder
description: "Legacy direct-SQL seed recipe; no shared-database or credential-copy authority."
argument-hint: "Inspect legacy adapter compatibility; current delivery uses the active backlog and workflow."
tools: [execute, read, edit, 'postgresql-mcp/*']
model: deepseek-v4-flash (customendpoint)
user-invocable: true
---

# Legacy Copilot adapter

The former recipe assumed a shared connection, copied credentials/ciphertext and illustrative SQL/schema values. This file is retained for discoverability and historical reference;
it must not dispatch or certify current governed story/epic work.

Use current supported fixtures or application creation paths within explicitly authorized disposable resources.

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

Do not copy existing users' password hashes, provider ciphertext or connection secrets.
Do not fabricate encrypted values or use a named shared database by default. Actual
fixture preparation requires a current, explicitly authorized resource/data contract.

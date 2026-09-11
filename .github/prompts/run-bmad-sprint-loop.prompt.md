---
name: "Run BMad Sprint Loop"
description: "Legacy Copilot entry point; consult current workflow/harness compatibility before delivery."
argument-hint: "next | story 13-1 | epic 13 | resume | dry-run"
agent: bmad-sprint-orchestrator
---

The referenced Copilot orchestrator is a legacy adapter whose role allowlist and
ownership/gates are not synchronized with the current unified workflow. Do not start
current delivery through it or silently recreate missing roles.

Read root [AGENTS.md](../../AGENTS.md) and
[activation guidance](../../docs/agent-workflow/activation-handoff.md). Report the
compatibility limit and use the current repository skill with a compatible harness
for the explicitly requested scope. Old examples/feature timestamps never reactivate
historical stories. Adapter synchronization is a separate bounded request, not implicit
permission for implementation, model/tool changes or runtime operations.

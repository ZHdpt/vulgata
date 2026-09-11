---
name: "Close BMad Epic"
description: "Legacy Copilot close entry point; requires a synchronized current delivery adapter."
argument-hint: "current epic whose stories are all durably done"
agent: bmad-sprint-orchestrator
---

The referenced Copilot orchestrator is not synchronized with the current unified
workflow. Do not certify an epic through that legacy adapter.

Read root [AGENTS.md](../../AGENTS.md), current lifecycle evidence and
[activation guidance](../../docs/agent-workflow/activation-handoff.md). Use the current
close-bmad-epic skill with a compatible harness only when all intended stories are
durably complete. Definition acceptance is not implementation completion. Preserve
the pinned independent audit, dispositions and checkpoint boundaries; this entry point
authorizes no implicit staging, commit, remediation or runtime work.

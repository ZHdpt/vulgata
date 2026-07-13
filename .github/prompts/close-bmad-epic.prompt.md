---
name: "Close BMad Epic"
description: "Use when every story in a BMad epic appears complete and the epic needs final verification, architecture review, and safe closure. Trigger phrases: close-bmad-epic, close epic 4, verify epic 4."
argument-hint: "epic 4 | epic 5"
agent: bmad-sprint-orchestrator
---
Use the sprint-loop orchestrator to close `$ARGUMENTS` through the epic-close gate.

Before staging any epic-close artifacts:
- validate the workflow customizations and schemas the close flow depends on;
- confirm the current epic-close run is starting from a non-mutating validation pass;
- keep the architecture audit, dispositions, and checkpoint binding separate from any remediation work.

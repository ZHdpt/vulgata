---
name: "Run BMad Sprint Loop"
description: "Use when implementing, resuming, or dry-running a BMad story or epic through the gated sprint-loop workflow. Trigger phrases: run-bmad-sprint-loop, story 4-1, epic 4, resume active story, dry-run."
argument-hint: "story 4-1 | epic 4 | epics 4 and 5 | resume | story 4-1 in dry-run mode"
agent: bmad-sprint-orchestrator
---
Use the sprint-loop orchestrator for `$ARGUMENTS`.

Before any real story implementation in this session:
- validate the workflow customizations and supporting assets that the workflow depends on;
- run a non-mutating dry run for the requested scope;
- stop and report the dry-run result before making implementation changes.

When the scope is unclear, resolve it to exactly one of: story, epic, story list, epic list, or resume.

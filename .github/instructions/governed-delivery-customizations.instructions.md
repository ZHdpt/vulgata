---
description: "Use when editing delivery customizations under .github. Preserve governed workflow ownership and harness boundaries."
applyTo: ".github/agents/*.agent.md, .github/prompts/*.prompt.md, .github/hooks/*.json, .github/hooks/scripts/*.ps1, .github/instructions/*.instructions.md"
---

# Governed delivery customization rules

- Read root [AGENTS.md](../../AGENTS.md) and the current
  [workflow architecture](../../docs/agent-workflow/architecture.md),
  [state machine](../../docs/agent-workflow/state-machine.md) and
  [agent catalog](../../docs/agent-workflow/agent-catalog.md) before changing an adapter.
  These own current stages, review/TDD/convergence, human gates and risk escalation;
  do not reproduce obsolete v1 gate rules here.
- Use native Copilot discovery locations: `.github/agents`, `.github/prompts`,
  `.github/instructions` and `.github/hooks`. Prompts are entry points; durable
  workflow law remains in `.agents/skills/run-governed-delivery/`; `docs/agent-workflow/`
  contains consumer adoption guidance and forwarding links, not copied contracts.
- Preserve direct-child workflow topology and disjoint writes. The active root alone
  owns lifecycle/state and authorized commits; tests, production, convergence and
  independent reviewers follow the current catalog rather than an old combined role.
- The obsolete Copilot sprint-loop roles and entry points were removed. The installed
  Codex profiles are the supported governed-delivery route; unrelated Copilot helpers
  do not establish harness parity. Synchronization is a separate task; do not expand permissions or
  change models/allowlists as incidental memory maintenance.
- Reference installed skills/adapters rather than copying their bodies. Keep hooks
  small, auditable and repository-relative. Git safety must protect destructive or
  ownership-crossing changes without blocking ordinary reads.
- Preserve one story to one deliberately selected SpecKit feature, current independent
  definition/quality gates, honest TDD and convergence, conditional E2E, pinned evidence
  and sole-committer rules. Architecture/Agent-tool/Scan/PRD decisions follow current
  plan-review and user-authorization rules; historical waivers do not carry forward.

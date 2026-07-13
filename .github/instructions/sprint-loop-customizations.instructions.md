---
description: "Use when creating or editing sprint-loop customizations under .github/agents, .github/prompts, .github/hooks, or .github/instructions. Covers native VS Code Copilot discovery rules, tool boundaries, and workflow invariants."
applyTo: ".github/agents/*.agent.md, .github/prompts/*.prompt.md, .github/hooks/*.json, .github/hooks/scripts/*.ps1, .github/instructions/*.instructions.md"
---
# Sprint Loop Customization Rules

- Put discoverable VS Code Copilot customizations only in `.github/agents`, `.github/prompts`, `.github/instructions`, and `.github/hooks`. Do not rely on `.github/workflows` for native discovery.
- Keep the root sprint-loop orchestrator as the only workflow agent that has both `agent` and `execute` tools. Worker agents should get the smallest tool set that fits their role and should set `agents: []`.
- Preserve direct-child topology. Workflow worker agents must not delegate to other agents.
- Reference installed BMad, SpecKit, Superpowers, and `improve-codebase-architecture` assets by loading them from the workspace when needed. Do not copy or fork their bodies into these customizations.
- Keep prompts as entry points. Keep durable workflow law in `docs/agent-workflow/` and durable schemas/scripts in committed files.
- Hooks must stay small, auditable, and path-stable. Use repository-relative paths and avoid environment-variable-based pathing.
- Any Git-safety hook should target destructive history changes, branch switching, or blanket staging instead of blocking ordinary reads and edits.
- Preserve these workflow invariants exactly: one BMad story to one SpecKit feature, `specify -> plan -> tasks -> analyze -> implement`, explicit approval for blocking analysis remediation, dual implementation review, conditional E2E, and the sole-committer boundary.

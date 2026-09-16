# Vulgata workspace instructions

Read [AGENTS.md](../AGENTS.md) as the shared project entry point. It routes current
architecture, glossary, story lifecycle, development rules and test guidance.

- For resumption or reuse, read [project state](../docs/agents/project-state.md) and
  verify current files. Historical specs, July plans and old feature timestamps do
  not select current work.
- For current Scan/Knowledge/Chat design, use the architecture gateway/spine and
  canonical glossary reached from AGENTS. July documents are only for explicitly
  historical work, not a competing architecture.
- Governed-delivery 0.7 is currently supported through the installed Codex profiles
  and `$run-governed-delivery` skill. Obsolete Copilot sprint-loop agents and run/close
  prompts were removed. Remaining unrelated helpers are not delivery orchestrators;
  do not substitute them for the governed ownership, approval or verification gates.
  Ordinary read/edit assistance under AGENTS remains supported; Copilot delivery
  parity requires a separate bounded adapter task.
- The SpecKit managed context lives in AGENTS.md. Refresh it only for a deliberately
  selected feature and explicit actual plan path; do not maintain a second plan pointer
  here or auto-select historical plans by modification time.

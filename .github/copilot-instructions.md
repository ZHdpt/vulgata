## Vulgata Workspace Instructions

### Core References

- Read `AGENTS.md` for project structure, stack, testing discipline, and repository rules.
- Use terms from `docs/UBIQUITOUS_LANGUAGE.md` exactly. Prefer `Cross-Repository` over `Cross-System`, `Orchestrator Agent`, `Worker Agent`, `REFERENCES Edge`, `GENERATED_FROM Edge`, and `Uncertainty`.
- When editing the July scanning plan, cross-check `docs/scanning_plan_july/00-cross-reference.md` before changing any numbered section.

### Scanning-Plan Work

- Treat each numbered scanning-plan document as a separate decision surface. Do not silently spread a local change across `01`-`08` without naming the affected sections.
- Separate stable structure from path-specific reasoning. If a relationship depends on caller context, record it as a dispatch-site or trace-time concern rather than a base `REFERENCES` edge.
- Keep deterministic structure and semantic interpretation separate. Prefer CodeGraph, schemas, and workflow steps for structural facts; use LLM reasoning for tradeoffs, prose synthesis, and unresolved ambiguities.
- Surface contradictions explicitly. If a proposal conflicts with the cross-reference analysis, a glossary term, or an earlier section, call out the conflict and resolve it in the reply.
- When proposing changes, respond in this order: decision, rationale, affected sections, open risks, next edit target.

### Working Style

- Anchor discussion to concrete files and sections instead of restating the whole architecture in free text.
- Keep scanning-plan sessions narrow. Finish one section or one cross-section dependency chain before moving to the next.
- When a request is primarily architecture or document design, prefer editing the source document directly instead of only summarizing the plan.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->

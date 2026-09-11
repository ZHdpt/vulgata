# Domain guidance

For domain names, use [UBIQUITOUS_LANGUAGE.md](../UBIQUITOUS_LANGUAGE.md). For current
Scan/Knowledge/Chat responsibilities and decisions, read the
[architecture gateway](../bmad/planning-artifacts/architecture.md) and its spine.
These are current authorities; historical plans/specs describe their own accepted scope.

## Conditional context documents

If `CONTEXT-MAP.md` exists at the repository root, follow its pointers to the relevant
`CONTEXT.md` files. If `docs/adr/` or context-scoped `src/<context>/docs/adr/` exists,
read decisions relevant to the change. Their absence is not a requirement to scaffold
new documents before useful work; this repository currently uses the canonical glossary
and architecture spine rather than a populated context-map/ADR tree.

Context descriptions complement the canonical vocabulary; they do not silently replace
it. When a needed term or ownership seam is genuinely missing, surface that specific gap
through the applicable domain/architecture workflow instead of inventing another glossary.

## Conflicts and history

Identify the exact conflicting requirement/decision, scope and evidence. Propose a
scoped correction through the owning workflow; neither file age, an archived accepted
plan nor an as-built workaround automatically supersedes the adopted target. Preserve
historical evidence and distinguish accepted design, existing code and demonstrated
runtime behavior.

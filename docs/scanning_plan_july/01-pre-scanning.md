# 01 — Pre-Scanning

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Review pending — extracting from original plan; questions raised below.

---

## TODO — Open Questions from July Review

> These are concerns raised that need to be addressed before finalizing this section.

1. **Goals of pre-scanning need to be explicitly stated.** What exactly must pre-scanning produce before Pass 1 can begin? Is it just `code_units` rows and a Communication Pattern Catalog snapshot, or is there more?

2. **General strategy — single agent approach.** Current plan has the Orchestrator Agent (LLM) doing pre-scan profiling. Should pre-scanning be done by a single non-LLM agent/tool instead? What questions should it ask the System Owner? The System Owner knows their repo — can we capture that knowledge upfront?

3. **Questions to ask the System Owner (pre-scan interview):**
   - What language(s)/framework(s) does this repo use?
   - What are the entry points? (controllers, queue consumers, scheduled tasks, etc.)
   - What external systems/services does this repo call?
   - What communication patterns are used? (RPC, HTTP, message queues, file-based)
   - Are there any directories/files to exclude from scanning?
   - What are the key business domains or bounded contexts?
   - Any known dead code or legacy sections to skip?

4. **One-shot vs. iterative.** Should the System Owner interview be a single pass, or should it iterate (e.g., "Here's what I found — does this look right?")?

5. **Communication Pattern Catalog population.** Current plan says written by the System Owner. Should pre-scanning attempt to auto-detect patterns from imports/annotations and present candidates for approval?

---

## Extracted Content from Original Plan

### Scan Lifecycle (excerpt)

```
Pre-Scan Profiling
  → CodeGraph parses repos into code_units
  → Scan filter excludes non-code files
  → Communication Pattern Catalog loaded
```

### Pre-Scan Profiling

Before any worker is dispatched, the orchestrator performs reconnaissance over the repository:

1. **CodeGraph parses source files** into `code_units` rows: one per function, method, class, or file-level scope, with exact `line_start`/`line_end` from the AST. No LLM involvement — this is deterministic tree-sitter parsing. `range_confidence` is set to `high` for AST-verified ranges, `medium` for regex-fallback ranges, `low` for heuristic ranges.

2. **Language and framework detection** populates `repositories.language` and identifies which Communication Pattern Catalog entries apply.

3. **Scan filter** excludes build artifacts, generated code, test fixtures, and configuration data from the `code_units` pool. Only filtered code_units are dispatched to workers.

4. **Batch planning:** The orchestrator groups `code_units` into supersteps. Related code_units (caller/callee pairs within the same file, classes in the same module) are batched together so that cross-verification (FR-4.12) can run within the same superstep.

### Document Pre-Allocation

After profiling but before dispatching any workers, the orchestrator bulk-creates `documents` rows for every filtered `code_unit`:

1. **One document row per filtered code_unit:** `doc_type = 'code_logic'`, `status = 'pending'`, `title` derived from `code_unit.symbol_name`, `content_md_path = NULL`, `is_current = true`, `version = 1`.

2. **Tree structure:** All CL Docs have `parent_document_id = NULL` initially. The source-mirroring tree is computed from document `path` strings at display time. No synthetic directory-document rows are created.

3. **Document IDs are now known across the entire scan.** Every worker can create `REFERENCES` edges to any other document in the same repo — the target `document_id` exists in the database and satisfies the FK constraint.

### Relevant Tables

- `systems` — top-level business systems
- `repositories` — git repos belonging to a system, with `scan_status`, `language`, `last_commit_hash`
- `communication_patterns` — per-system configuration of how that system communicates externally
- `code_units` — source code anchors determined by CodeGraph at pre-scan profiling

### Scan Coordinator (Non-LLM Background Service)

A singleton service. **Not an LLM agent** — deterministic application code.

- **Scan queue:** Maintains pending scan requests. One scan per repo at a time.
- **Git operations:** Pulls remote changes. Records commit hashes.
- **Orchestrator lifecycle:** Spawns one Orchestrator Agent per repo scan. Monitors via heartbeat.
- **Concurrency control:** `MaxConcurrentScans` (default 1), `MaxWorkersPerScan` (default 5).

---

## Notes for Revision

- The term "pre-scanning" conflates two distinct phases: (a) System Owner interview/metadata gathering, and (b) deterministic CodeGraph parsing. Should these be separate sections?
- Current plan has the LLM Orchestrator doing profiling. If we move to a non-LLM orchestrator (see [02-agent-orchestrator.md](./02-agent-orchestrator.md)), pre-scan profiling would be entirely deterministic — no LLM involvement needed.
- The `communication_patterns` table is per-system. Should the System Owner interview happen once per system, or once per repo?

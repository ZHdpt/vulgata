# 05 — How CodeGraph Works During Pass 1 Scanning

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Review pending — extracting from original plan; questions raised below.

---

## TODO — Open Questions from July Review

> These are concerns raised that need to be addressed before finalizing this section.

1. **CodeGraph's role in the scanning pipeline needs explicit documentation.** The current plan mentions CodeGraph in the pre-scan profiling section but doesn't detail how it integrates with the Pass 1 worker agents. Specifically:
   - What data does CodeGraph produce that Pass 1 workers consume?
   - Does CodeGraph run once (pre-scan) or is it called during Pass 1 as well?
   - How do workers use CodeGraph data to understand call relationships?

2. **CodeGraph as a tool for workers.** Beyond pre-scan profiling, workers could use CodeGraph as a tool during document generation:
   - `find_callers(symbol_name)` → who calls this function?
   - `find_callees(symbol_name)` → what does this function call?
   - `get_symbol_context(symbol_name)` → surrounding symbols in the same file
   - `get_imports(file_path)` → what does this file import?

   This would allow workers to trace call chains and build more accurate REFERENCES edges, rather than relying solely on reading source text and guessing.

3. **Call graph traversal depth.** How deep should workers trace call chains? A single function might call `A()` which calls `B()` which calls `C()`. Should:
   - Workers only record direct calls (one hop)?
   - Workers follow the chain N levels deep?
   - The orchestrator provide pre-computed call paths from CodeGraph?

4. **Handling of cross-file references.** CodeGraph can resolve symbols across files within the same repo. This is critical for:
   - Finding the target of an imported function call
   - Resolving class inheritance hierarchies
   - Tracing interface implementations

   Does the current plan leverage this, or do workers re-discover these relationships by reading source?

5. **Performance and caching.** If CodeGraph is called as a tool by every worker, the index needs to be:
   - Built once per repo (not per worker)
   - Queryable concurrently by multiple workers
   - Fast enough to support interactive agent tool calls (< 100ms)

6. **CodeGraph vs. tree-sitter directly.** The original plan mentions both. Clarification needed:
   - Is CodeGraph the MCP server (`codegraph_*` tools)?
   - Is tree-sitter the underlying parser that CodeGraph uses?
   - For the scanning pipeline, should we use CodeGraph MCP tools or call tree-sitter directly?

---

## Extracted Content from Original Plan

### `code_units` table

Source code anchors — determined by CodeGraph/tree-sitter at pre-scan profiling, not by LLM guessing.

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `repo_id` | FK → repositories | |
| `file_path` | TEXT | Relative to repo root |
| `file_identity_hash` | TEXT | SHA-256 of first 4096 bytes — **rename detection** |
| `line_start` | INT | |
| `line_end` | INT | |
| `symbol_name` | TEXT | AST symbol: `CheckoutHandler.processPayment()` |
| `language` | TEXT | |
| `unit_type` | ENUM | function, method, class, file |
| `source_hash` | TEXT | SHA-256 of source text within line range |
| `range_confidence` | ENUM | CodeGraph-validated: high, medium, low |
| `created_at` | TIMESTAMPTZ | |

**Why CodeGraph determines boundaries, not LLMs:** LLMs hallucinate line ranges. A document claiming to describe lines 42-156 when the function actually spans 42-189 silently breaks citation accuracy (FR-11.7) and incremental re-scan (FR-9.3). CodeGraph provides deterministic, AST-verified boundaries.

### Pre-Scan Profiling — CodeGraph's Role

1. **CodeGraph parses source files** into `code_units` rows: one per function, method, class, or file-level scope, with exact `line_start`/`line_end` from the AST. No LLM involvement — this is deterministic tree-sitter parsing. `range_confidence` is set to `high` for AST-verified ranges, `medium` for regex-fallback ranges, `low` for heuristic ranges.

2. **Language and framework detection** populates `repositories.language` and identifies which Communication Pattern Catalog entries apply.

3. **Scan filter** excludes build artifacts, generated code, test fixtures, and configuration data from the `code_units` pool.

### Pass 1 Dispatch

Each worker receives one `code_unit` — the source text within `line_start`..`line_end`, the `symbol_name`, the list of pre-allocated document IDs for related code_units, and supplementary context (neighboring code_units, import statements, framework patterns from the Communication Pattern Catalog).

### Source Anchoring — Three-Layer Mechanism

**Layer 1 — Structural (code_units):** CodeGraph/tree-sitter parses source into `code_units` at repository registration (pre-scan profiling).

**Layer 2 — Semantic (document_sources):** During document generation, the LLM agent emits structured metadata declaring which `code_units` a document is "generated from" (primary role) or "mentions" (referenced role).

**Layer 3 — Fast Lookup (reverse_index):** Populated atomically post-generation: for every line in every `code_unit` that a document has a primary link to, insert one row.

### Two-Phase Change Detection (for incremental re-scan)

**Phase 1 — Line-Overlap Filter:** Uses `reverse_index` to find documents whose source lines overlap with changed code.

**Phase 2 — Hash Verification:** Compares `source_hash` of changed code_units against stored hashes.

---

## Notes for Revision

- CodeGraph is currently used **only** for pre-scan profiling (producing `code_units`) and file rename detection (`file_identity_hash`). It is **not** used during Pass 1 document generation itself.
- Workers currently receive "the list of pre-allocated document IDs for related code_units" — but how "related" is determined is vague. If CodeGraph provides call-graph data, this could be the source of relatedness.
- The Microsoft Agent Framework's MCP tools already expose CodeGraph as `codegraph_callers`, `codegraph_callees`, `codegraph_trace`, etc. Workers could use these directly.
- There's a design tension: if workers use CodeGraph tools to trace call chains, and CodeGraph gives deterministic results, should REFERENCES edges from static call analysis be created deterministically (not by LLM)? The LLM's value is in interpreting *why* the call matters, not *that* it exists.

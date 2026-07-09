# 05 — How CodeGraph Works During Pass 1 Scanning

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Section 05 completed - CodeGraph-backed Pass 1 rubric agreed and ready to use as input for sections 03, 06, and 08.

---

## Original Open Questions from July Review

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

Citation payload rule:

Every Source Anchoring record used for citation should preserve enough identity to survive file renames and line drift:

- repository ID
- scanned commit hash
- file path at scan time
- Code Unit ID
- line start and line end
- source hash or snippet hash
- Document version ID that made the claim

Rendered document citations may display only the path and line range, but retrieval should keep the full payload internally.

### Two-Phase Change Detection (for incremental re-scan)

**Phase 1 — Line-Overlap Filter:** Uses `reverse_index` to find documents whose source lines overlap with changed code.

**Phase 2 — Hash Verification:** Compares `source_hash` of changed code_units against stored hashes.

---

## Notes for Revision

- CodeGraph is currently used **only** for pre-scan profiling (producing `code_units`) and file rename detection (`file_identity_hash`). It is **not** used during Pass 1 document generation itself.
- Workers currently receive "the list of pre-allocated document IDs for related code_units" — but how "related" is determined is vague. If CodeGraph provides call-graph data, this could be the source of relatedness.
- The Microsoft Agent Framework's MCP tools already expose CodeGraph as `codegraph_callers`, `codegraph_callees`, `codegraph_trace`, etc. Workers could use these directly.
- There's a design tension: if workers use CodeGraph tools to trace call chains, and CodeGraph gives deterministic results, should REFERENCES edges from static call analysis be created deterministically (not by LLM)? The LLM's value is in interpreting *why* the call matters, not *that* it exists.

---

## Clarification Notes for Current Review

### 1. Working definition of a code unit

For this plan, a **code unit** is the smallest source slice that the system is willing to assign as a standalone Pass 1 analysis job.

In V1, a code unit should usually be one of:

- function
- method
- class
- file-level module scope when smaller symbols are not useful or not reliably available

It should be produced by structural parsing, not guessed by the LLM.

Practical definition:

- a stable symbol or source range
- with a known file path
- with `line_start` and `line_end`
- with a stable identity for rescans and hashing
- with enough local meaning to support a Code Logic document or a meaningful partial document

### 2. Code unit selection is language- and framework-sensitive

The code unit type may vary by ecosystem.

Examples:

- C# / Java: method, class, controller action, hosted-service method
- Python: function, class, module-level callable region
- JavaScript / TypeScript: exported function, class, component, module scope
- DSL or config-heavy frameworks: a meaningful source file may be treated as one unit when symbol-level splitting is not useful

The key rule is not "always smallest symbol." The key rule is "smallest structurally stable and documentable analysis unit."

### 3. What a code unit is not

A code unit should not be:

- an arbitrary chunk chosen only by token count
- a business flow
- a whole repo subsystem by default
- a binary resource
- a meaningless getter/setter-sized slice when that would create useless documents

### 4. Relationship between code units and Pass 1 reading behavior

The worker's assigned code unit is the center of gravity for the job.

That means:

- the document is primarily about that unit
- other code may be read for context
- but the worker should not silently turn one code-unit job into freeform subsystem exploration

If deeper context is required, the worker should use bounded structural expansion or emit uncertainty rather than redefining the job.

### 5. Pass 1 should allow provisional results and bounded revisit

Pass 1 should not assume that the first time a code unit is scanned is always the best possible moment to finalize its Code Logic document.

This matters when scanning `A` before `B` gives only partial understanding, but scanning `B` later materially enriches the explanation of `A`.

Example:

```text
A -> B
```

When scanning `A`, the worker may already know that `A` depends heavily on `B`, but `B`'s own document has not been generated yet. After `B` is scanned, `A` may deserve a richer explanation.

The recommended rule is:

- Pass 1 may produce a **provisional** document for `A`
- `A` may declare deferred enrichment dependencies on later code-unit outputs
- the orchestrator may schedule one bounded refinement job for `A` after those dependencies are satisfied

### 6. When should Pass 1 revisit an earlier code unit?

The answer is **sometimes, but not by default and not indefinitely**.

Do revisit when later information materially changes:

- important downstream behavior explanation
- dispatch-site binding quality
- framework interaction understanding
- uncertainty reduction that substantially improves the document

Do not revisit just because:

- a referenced callee now has more detail but does not materially change `A`'s own logic
- every neighbor document became available
- the scan could theoretically keep enriching forever

So the trigger should be **material enrichment**, not mere existence of more context.

For V1, "material enrichment" must be decided by deterministic predicates, not a fresh LLM orchestration judgment.

Schedule a refinement only when the earlier Code Logic Document declared a dependency and the later result can:

- convert an uncertainty into a dispatch site, candidate set, or exact `REFERENCES` edge
- improve a dispatch site with stronger binding hints or a bounded candidate set
- provide an endpoint, resource, or configuration artifact explicitly named in the dependency
- clarify a direct callee/provider/resource relationship that the earlier job marked as important

If the later result merely adds detail without changing one of those facts, preserve the existing document and leave enrichment to Pass 2 retrieval context.

### 7. How the revisit should work

The revisit should be a **bounded refinement wave**, not a full uncontrolled rescan.

Recommended behavior:

1. initial Pass 1 job for `A` completes with either:
   - final result, or
   - provisional result plus deferred enrichment dependencies
2. later job `B` completes
3. orchestrator checks whether `B` satisfies one of `A`'s declared dependencies
4. if the dependency is material, orchestrator schedules one refinement job for `A`
5. refinement job reads:
   - `A`'s original source slice
   - `A`'s existing document
   - `B`'s finished document or structured output
   - relevant CodeGraph context
6. refinement job updates `A` once

This avoids reopening every upstream node every time a downstream node finishes.

### 8. Preventing infinite backtracking

To prevent `A -> B -> C -> ...` from causing endless rescans, V1 should enforce limits such as:

- one refinement per code unit per scan, or a very small fixed cap
- refinement only for declared dependencies
- no recursive re-refinement chains unless explicitly justified by policy
- unresolved cases fall back to explicit uncertainty instead of more revisits

This keeps Pass 1 convergent.

### 8A. Incremental re-scan behavior

V1 incremental re-scan should favor predictable regeneration over complex refinement replay.

Recommended rule:

- changed Code Units produce new Code Logic Document versions
- direct and transitive impact analysis marks dependent Documents stale using `REFERENCES` and `GENERATED_FROM`
- if a re-scanned document declares direct deferred dependencies that are satisfied within the same re-scan batch, run the same one-step refinement rule as initial Pass 1
- do not chase refinement waves outside the affected set during V1 incremental re-scan
- if the changed output may affect a Business Logic Document, mark the BL Document stale and regenerate it through normal Pass 2 flow-group planning rather than patching the prose in place

This keeps re-scan quality close to initial scan quality without turning every incremental scan into a full system scan.

### 9. Relation to dispatch and binding

This revisit model is especially important for dispatch-sensitive analysis.

Examples:

- `A` calls `B`
- `B` contains a dispatch site
- only after scanning another relevant unit, config file, or provider document does the likely binding become clearer

In that situation:

- `B` may first produce a provisional document with dispatch-site uncertainty
- later context may allow a refinement of `B`
- or `A`'s flow-specific meaning may be resolved later in Pass 2 without needing to fully rewrite `B`

The key distinction is:

- Pass 1 refinement improves **code logic documentation quality**
- Pass 2 still owns **path-specific business flow interpretation**

### 10. Pass 1 edge-emission decision rubric

Pass 1 needs an explicit rule for what kind of graph fact it is allowed to emit from a code unit.

The worker should not decide this ad hoc. It should apply a fixed decision rubric.

The four outcomes are:

1. **base `REFERENCES` edge**
2. **bounded candidate-set `REFERENCES` edges**
3. **dispatch site**
4. **uncertainty**

### 10A. Evidence hierarchy

The worker should treat these evidence levels differently.

#### Level 1 — exact local structural evidence

Examples:

- direct concrete call target resolved by CodeGraph
- direct imported symbol resolved to one known symbol
- exact route or endpoint resolved from local code/config/spec evidence

This level is strong enough for a base `REFERENCES` edge.

#### Level 2 — bounded local alternative set

Examples:

- explicit local `if/else` with two visible call targets
- visible small switch over known handlers
- small bounded provider set visible from the current unit and its attached local/config context

This level is strong enough for candidate-set `REFERENCES` edges, but only if the bounded set is justified from the current unit itself.

#### Level 3 — abstract invocation with binding hints

Examples:

- interface or virtual dispatch where concrete target depends on caller-provided instance
- DI-bound dependency where the local unit knows the contract but not the concrete implementation
- generated client/proxy where the contract is known but endpoint binding depends on environment or caller path

This level is strong enough for a **dispatch site**, not for stable base edges.

#### Level 4 — weak, open, or unsupported evidence

Examples:

- unconstrained reflection
- opaque plugin lookup
- runtime string dispatch with no defensible target family
- unsupported binary-resource-driven behavior

This level is not strong enough for edges or a useful dispatch site. It should become an `uncertainty`.

### 10B. Rule 1 — When to emit a base `REFERENCES` edge

Emit a base `REFERENCES` edge only when the callee is justified from the current unit and bounded attached context.

Required conditions:

- CodeGraph or attached config/spec evidence resolves the target exactly
- the target is a stable property of the current unit, not only of a caller path
- the worker can explain why this dependency belongs to the current document's logic

Examples:

- `OrderService.Place()` calls `RiskEvaluator.Evaluate()` directly
- controller action calls one concrete repository method
- REST client call maps deterministically to one provider endpoint

### 10C. Rule 2 — When to emit bounded candidate-set `REFERENCES` edges

Emit candidate-set `REFERENCES` edges only when the current unit itself justifies a small bounded set.

Required conditions:

- the alternatives are finite and small
- the alternatives arise from local branch/config logic visible to the worker
- the worker can defend why the set belongs to the unit itself, not to unknown caller context

Required metadata:

- shared `candidate_group_id`
- `candidate_rank` if ranking exists
- `candidate_count`
- condition or branch notes when visible

Examples:

- local branch calls `C` or `D`
- local feature flag selects one of two implementations

Do **not** use candidate-set edges when the set only becomes visible because upstream callers or environment bindings choose the callee.

### 10D. Rule 3 — When to emit a dispatch site

Emit a dispatch site when the unit clearly performs an invocation but the concrete callee depends on caller-bound or environment-bound binding.

Required conditions:

- the abstract invocation is real and structurally defensible
- the worker can identify the invocation site and at least some contract or binding hints
- the worker cannot honestly claim a stable base edge from the unit alone

Typical cases:

- interface parameter dispatch
- virtual dispatch with caller-chosen instance
- constructor-injected dependency with multiple possible runtime bindings
- service-locator lookup
- generated RPC client whose concrete endpoint depends on configuration or runtime binding

Dispatch-site output should carry:

- call symbol
- receiver or contract hints
- protocol if applicable
- endpoint or operation hints if applicable
- resolution scope such as `caller_bound`, `config_bound`, or `runtime_open`

### 10E. Rule 4 — When to emit an uncertainty

Emit an `uncertainty` when the worker cannot even form a defensible dispatch site or bounded candidate set.

Typical cases:

- reflection without credible target hints
- unsupported binary resource behavior
- runtime string-based registry lookup with no bounded contract family
- framework magic that remains opaque after bounded expansion and one refinement opportunity

The worker should prefer:

- base edge when exact and stable
- candidate-set edges when locally bounded
- dispatch site when abstract but structurally real
- uncertainty only when the above three are not defensible

### 10F. Cross-repo communication-specific application

For cross-repo communication behavior, apply the same rubric.

#### Exact endpoint known

- create or match the canonical endpoint entity
- emit a base `REFERENCES` edge if the target document is already resolvable, or the endpoint-linked unresolved record needed by later resolution

#### Small bounded endpoint set known locally

- emit candidate-set `REFERENCES` edges only if the bounded set is locally justified

#### Endpoint depends on caller/config/runtime binding

- emit a dispatch site with endpoint or contract hints
- do not force speculative base edges

#### Endpoint family not even defensible

- emit uncertainty

### 10G. Relationship to bounded refinement

This rubric is applied first during the initial Pass 1 job.

After later units or resources become available, one bounded refinement job may upgrade the result.

Allowed upgrades:

- uncertainty -> dispatch site
- dispatch site -> better dispatch site metadata
- dispatch site -> exact `REFERENCES` edge when later local evidence makes the target stable from the unit's perspective
- provisional candidate set -> better-ranked or better-explained candidate set

Not allowed by default:

- repeated oscillation between edge types
- uncontrolled reopening after every downstream document finishes
- path-specific binding being baked into the base graph just because Pass 2 later found one concrete trace

### 10H. Decision table

| Situation seen in Pass 1 | Output |
|---|---|
| exact locally justified target | base `REFERENCES` edge |
| small bounded locally justified alternatives | candidate-set `REFERENCES` edges |
| abstract invocation with caller/config/runtime binding hints | dispatch site |
| no defensible bounded target or contract family | `uncertainty` |

### 10I. What CodeGraph is responsible for vs what the worker is responsible for

CodeGraph should provide:

- exact symbol boundaries
- direct callers/callees when structurally resolvable
- imports and implementations
- inheritance/override and cross-file structure

The worker should decide:

- whether the structural fact is stable enough for a base edge
- whether a bounded set is locally justified
- whether the invocation is caller-/environment-bound and should become a dispatch site
- whether evidence is too weak and must remain uncertainty

So CodeGraph provides the structural evidence floor, while the worker classifies that evidence into one of the four allowed outputs.

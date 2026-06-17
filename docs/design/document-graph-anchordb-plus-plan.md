# AnchorDB+ v2 — Winning Document Graph Architecture

**Status:** Final Recommendation
**Origin:** Winner of 3-candidate, 4-judge, 1-round grill-with-docs debate (June 17, 2026)
**Synthesized from:** AnchorDB (Relational Adjacency), ArborGraph (File-System Native), Riverbed Graph (Graph-Native)

---

## Why This Part Is Designed As It Is

The document graph is the structural backbone of Vulgata. It must answer five questions correctly under the constraints of a 9-week, 2-person + AI competition timeline:

| Question | Why It Matters |
|----------|---------------|
| Where is each document in the source tree? | FR-4.6 — tree navigation is how users browse |
| What source code produced each document? | FR-4.3, FR-11.7 — every claim must cite its origin |
| If these N files change, what documents break? | FR-9.3 — incremental re-scan must be precise |
| Which documents in other repos link to this one? | FR-6.2 — cross-repo tracing is the primary demo success criterion |
| Can we render this as a live graph? | FR-12.6, NFR-1.3 — the competition demo's visual centerpiece |

**The trap we avoided:** Building a custom graph engine, event-sourcing layer, or distributed graph database. At demo scale (~200 documents, ~500 edges), SQLite recursive CTEs provide identical graph traversal capability with zero custom infrastructure. The adjacency list IS the graph database.

**The insight we stole:** Cross-repo is not a schema — it's a query-time emergent property. Any edge where `source.repo_id != target.repo_id` is a cross-repo link. No separate table, no dual-write, no bridge schema. One `edges` table, one recursive CTE, all queries uniform.

**The keystone we adapted:** A `reverse_index` table (one row per source line per document) makes the most operationally critical query — "which documents came from these changed files?" — answerable in O(1). Stolen from ArborGraph, implemented relationally.

**The gap we closed:** Uncertainty is a first-class table with a status lifecycle, not "unknown = no row" in an edges table. You cannot render what you do not store. Every uncertainty state (resolved, discovered-but-unresolved, unknown) is a row the Blazor.Diagrams renderer can display.

Three independent teams starting from relational, filesystem-native, and graph-native philosophies converged on these same patterns. That convergence is the strongest evidence that this design is correct.

---

## Architecture Overview

**Stack:** SQLite via EF Core, WAL mode, single Docker container (ASP.NET Core + Blazor Server)
**Scale target:** ~2 Systems, ~12 repos, ~200 documents, ~500 edges (competition demo)
**Query philosophy:** Every graph operation is a single SQL statement. No custom graph engine.

### Core Insight

> The adjacency list IS the graph database. A single `edges` table with `edge_type` discriminator, plus recursive CTEs, provides path traversal, blast radius, and dependency chains without leaving SQLite. Cross-repo is not a schema concept — it's discovered at query time when `source_document.repo_id != target_document.repo_id`.

---

## Data Model: 12 Tables

### 1. `systems`
Top-level business systems being analyzed.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `name` | TEXT | Display name |
| `slug` | TEXT UNIQUE | URL-safe identifier |
| `description` | TEXT | |
| `scan_status` | ENUM | not_started, scanning, complete, stale |
| `last_scan_at` | DATETIME | |
| `created_at` | DATETIME | |
| `updated_at` | DATETIME | |

### 2. `repositories`
Git repositories belonging to a system.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `system_id` | FK → systems | |
| `name` | TEXT | |
| `slug` | TEXT | URL-safe identifier, derived from name (lowercased, hyphenated) |
| `git_url` | TEXT | Remote URL |
| `local_path` | TEXT | Local clone path |
| `default_branch` | TEXT | |
| `language` | ENUM | Detected during pre-scan profiling |
| `last_commit_hash` | TEXT | Git HEAD at last scan |
| `scan_status` | ENUM | not_started, scanning, complete, stale |
| `created_at` | DATETIME | |
| `updated_at` | DATETIME | |

### 3. `communication_patterns`
Per-system configuration of how that system communicates externally (PRD: Communication Pattern Catalog). This is the data backing the Service Topology MCP tool. Written by the System Owner. **Note:** The architecture decision is to move this to per-repo (matching the repo-boundary scan model), but that migration is deferred to a separate design doc. V1 ships with per-system CP as specified in the PRD.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `system_id` | FK → systems | Which system this pattern applies to |
| `pattern_type` | ENUM | rpc, http, mq, file, page_navigation (PRD §5.5) |
| `endpoint_pattern` | TEXT | Regex or prefix pattern for matching endpoints (e.g., `RiskService\.\w+`, `/api/v1/.*`) |
| `target_system_id` | FK → systems | Nullable — which system is typically the target of calls matching this pattern |
| `normalization_rule` | TEXT | How to normalize endpoint identifiers for matching (e.g., "strip query params from HTTP URLs", "convert topic separators '.' → '/'") |
| `is_active` | BOOL | Whether this pattern is used during scanning |
| `created_at` | DATETIME | |
| `updated_at` | DATETIME | |

INDEX(system_id, pattern_type), INDEX(endpoint_pattern)

**How it's used:**
- **Pre-Scan:** Loaded to provide workers with detection patterns.
- **Pass 1 edge creation:** When a worker detects a call matching `endpoint_pattern`, it creates an uncertainty with the qualified `endpoint_identifier` and `target_system_id` from the matching row.
- **Cross-Repo Resolution:** `normalization_rule` is applied to both the source's endpoint_identifier and the target repo's document edges before matching, eliminating format-drift false negatives.
- **Uncertainty promotion:** When a System Owner adds a new `communication_patterns` row, any `uncertainties` with `sub_status = 'unknown_target'` whose extracted endpoint matches the new pattern are promoted to `sub_status = 'cross_repo'` and become eligible for resolution.

### 4. `documents`
Core document nodes — both code-linked and business-logic documents.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `system_id` | FK → systems | |
| `repo_id` | FK → repositories | Nullable for non-code-linked docs |
| `path` | TEXT | Hierarchical path: `src/auth/login.md` |
| `title` | TEXT | Human-readable title |
| `content_md_path` | TEXT | Filesystem path to markdown body |
| `doc_type` | ENUM | code_logic, business_logic |
| `status` | ENUM | **pending** (pre-allocated, worker not yet run), **generated** (worker completed), **failed** (worker errored after retry), **superseded** (replaced by newer version via superseded_by_id) |
| `parent_document_id` | FK self → documents | **Tree hierarchy** — nullable for roots |
| `depth` | INT | Denormalized tree depth for fast subtree queries |
| `sort_order` | INT | Sibling ordering within parent |
| `version` | INT | Starts at 1, increments on re-scan |
| `is_current` | BOOL | False for superseded versions |
| `superseded_by_id` | FK self → documents | **Version chain head** — linked list for history |
| `metadata_json` | TEXT | Arbitrary LLM-generated metadata |
| `scan_id` | FK → scans | Which scan produced this version |
| `created_at` | DATETIME | |
| `updated_at` | DATETIME | |

**Key relationships:**
- `parent_document_id` forms the tree hierarchy (answers "where is this organized?")
- `superseded_by_id` forms the version chain (old docs never deleted — FR-4.10, FR-9.4)
- `is_current` + `superseded_by_id IS NULL` identifies the live version

### 5. `code_units`
Source code anchors — determined by CodeGraph/tree-sitter at pre-scan profiling, not by LLM guessing.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
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
| `created_at` | DATETIME | |

**Why CodeGraph determines boundaries, not LLMs:** LLMs hallucinate line ranges. A document claiming to describe lines 42-156 when the function actually spans 42-189 silently breaks citation accuracy (FR-11.7) and incremental re-scan (FR-9.3). CodeGraph provides deterministic, AST-verified boundaries. Confidence is tagged so reviewers know which ranges to double-check.

### 6. `document_sources`
M:N link between documents and code units, with role semantics.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `document_id` | FK → documents | |
| `code_unit_id` | FK → code_units | |
| `role` | ENUM | **primary** (document is about this code), **referenced** (code was cited) |
| `created_at` | DATETIME | |

UNIQUE(document_id, code_unit_id)

### 7. `edges` — THE GRAPH TABLE
**This is the graph database.** One table, doc→doc edges only. Tree hierarchy lives in `documents.parent_document_id`. Source anchoring lives in `document_sources`. The `edges` table does one thing: tracks how Documents relate to other Documents.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `source_document_id` | FK → documents | |
| `target_document_id` | FK → documents | |
| `edge_type` | ENUM | **REFERENCES** (doc cites/calls/links to another doc), **GENERATED_FROM** (Business Logic Doc was built from a Code Logic Doc — the two-pass provenance chain) |
| `protocol` | ENUM | rpc, http, mq, file, page_navigation — for cross-repo edges (PRD §5.5: Cross-System Communication Detection). **page_navigation edges must have `confidence <= 0.5`** due to inherently loose coupling between apps (PRD FR-5.6). |
| `endpoint_identifier` | TEXT | System-qualified: `system_slug:endpoint` (e.g., `mobile-banking:RiskService.evaluate`) |
| `endpoint_identifier_normalized` | TEXT | Normalized form for matching (via Communication Pattern Catalog) |
| `provider_repo_id` | FK → repositories | Repository that exposes the interface (FR-5.7) |
| `consumer_repo_id` | FK → repositories | Repository that calls the interface (FR-5.8) |
| `confidence` | REAL | 0.0–1.0 — LLM-generated edges have lower confidence |
| `metadata_json` | TEXT | Edge-specific metadata |
| `scan_id` | FK → scans | |
| `created_at` | DATETIME | |

-- No UNIQUE constraint on edges. Multiple REFERENCES edges between the same two
-- documents are valid — e.g., when two repos communicate through different
-- channels (RPC + MQ) between the same logical services, producing edges with
-- different protocol/endpoint_identifier. Application-level dedup (SELECT-before-
-- INSERT) handles intra-repo deduplication where a redundant edge is detected.
-- V2 adds partial unique indexes (SQLite 3.35+) for cross-repo edges.

**Design note — what lives where and why:**

| Relationship | Stored in | Reason |
|-------------|-----------|--------|
| Doc → Doc dependency | `edges` (REFERENCES) | The graph. Drives impact analysis. |
| BL Doc → CL Doc provenance | `edges` (GENERATED_FROM) | Doc→doc. Drives two-pass re-scan: if a Code Logic Doc changes, the Business Logic Doc built from it is stale. |
| Doc → Code Unit | `document_sources` | M:N join with role semantics (primary/referenced). Different tables, different cardinality. |
| Tree hierarchy | `documents.parent_document_id` | Single parent, no edge type needed. Self-referencing FK is cleaner than a separate edge row. |

**Why CONTAINS is NOT an edge type:** `documents.parent_document_id` already defines the tree. Storing the same hierarchy again as CONTAINS edges doubles the write surface for every tree mutation (document move, re-parent, insert) and guarantees drift. The tree is a column, not an edge type.

**Why GENERATED_FROM is doc→doc, not doc→code_unit:** Both `source_document_id` and `target_document_id` are FK → documents. A row pointing to a `code_units` row cannot exist — wrong table. The doc↔code_unit relationship is handled by `document_sources`. But the Business Logic Document → Code Logic Document provenance chain IS a doc→doc relationship and belongs here. When a Code Logic Doc is re-scanned and changes, any Business Logic Doc with a GENERATED_FROM edge pointing to it is flagged stale.

**Why CROSS_REPO is NOT an edge_type:** If an edge is both REFERENCES and "CROSS_REPO", which type wins? The answer: cross-repo-ness is a property of the endpoints, not the edge. Any edge where `source.repo_id != target.repo_id` IS cross-repo — discovered at query time:

```sql
-- All cross-repo edges, regardless of type
SELECT e.* FROM edges e
JOIN documents ds ON e.source_document_id = ds.id
JOIN documents dt ON e.target_document_id = dt.id
WHERE ds.repo_id != dt.repo_id
```

**Why bidirectional edges are NOT stored:** Storing both REFERENCES and REFERENCED_BY doubles the edge count and creates consistency problems. Reverse traversal is achieved at query time by swapping source/target in the JOIN. The recursive CTE handles bidirectional traversal naturally.

**Why `edges.target_document_id` is NOT NULL:** An edge represents a resolved relationship between two known documents. Pending cross-repo references — where the target document doesn't exist yet — are stored as `uncertainties` rows, not as edges with NULL targets. This keeps the `edges` table semantically clean (every row is a fact, not a hypothesis) and avoids NULL-handling in every graph traversal query. When the target repo is scanned and the uncertainty is resolved, a proper edge is created with both endpoints known.

**Why provider_repo_id / consumer_repo_id are FKs, not TEXT:** TEXT columns drift on repo rename and have no referential integrity. FK columns prevent dangling references to deleted repositories and enable JOIN-based repo resolution queries without string matching. The system can be derived via JOIN through repositories.system_id when PRD-level system grouping is needed.

### 8. `uncertainties`
First-class uncertainty tracking — every state is a row, including "unknown."

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `document_id` | FK → documents | Which document has the uncertainty |
| `type` | ENUM | unresolved_call, external_system, database_query, ambiguous_logic, human_input_needed |
| `status` | ENUM | **unresolved** (= PRD "discovered-but-unresolved"), **resolved** (= PRD "resolved"), **wontfix** (terminal non-resolution — human decision, FR-6.4 acceptable) |
| `sub_status` | ENUM | Nullable. Within `unresolved`: **cross_repo** (target repo known, waiting for scan), **unknown_target** (no repo/endpoint identified — PRD "unknown"), **ambiguous** (multiple possible targets). NULL when status = 'resolved' or 'wontfix'. |
| `description` | TEXT | What the agent is uncertain about |
| `context_snippet` | TEXT | The code or text that triggered the uncertainty |
| `endpoint_identifier` | TEXT | System-qualified: `system_slug:endpoint` (e.g., `mobile-banking:RiskService.evaluate`). Recorded even when target unknown. |
| `endpoint_identifier_normalized` | TEXT | Normalized form for matching (via Communication Pattern Catalog). |
| `target_repo_id` | FK → repositories | Nullable — which repository is believed to be the target |
| `resolved_by_document_id` | FK → documents | Nullable — which document resolved it |
| `resolved_by_human_input` | TEXT | Nullable — HITL answer |
| `created_at` | DATETIME | |
| `resolved_at` | DATETIME | |

**PRD alignment:** The PRD defines 3 user-facing states: resolved, discovered-but-unresolved, unknown. The database uses 3 status values + a sub_status column:
- `status = 'resolved'` = PRD "resolved"
- `status = 'unresolved'` + `sub_status = 'cross_repo'` or `'ambiguous'` = PRD "discovered-but-unresolved"
- `status = 'unresolved'` + `sub_status = 'unknown_target'` = PRD "unknown"
- `status = 'wontfix'` = Terminal non-resolution, not in PRD — a V1 extension. Human marks "we know about this, we accept it won't be resolved" (FR-6.4: dangling links are acceptable).

**Why this closes the "unknown = no row" gap:** The original AnchorDB represented the unknown state as the absence of a row. You cannot render what you do not store. Now `sub_status = 'unknown_target'` stores the unknown state as a row — Blazor.Diagrams can render it as a red diamond node with whatever partial information is available (endpoint string literal, code context).

### 9. `reverse_index` — THE KEYSTONE
O(1) source→document lookup. One row per source line per document.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `repo_id` | FK → repositories | |
| `code_unit_id` | FK → code_units | Denormalized for speed |
| `file_path` | TEXT | Denormalized |
| `line_number` | INT | The source line covered |
| `document_id` | FK → documents | |

INDEX(repo_id, file_path, line_number), INDEX(document_id), INDEX(code_unit_id)

**Scale:** At 200 documents × avg 30 lines each = 6,000 rows. Trivial for SQLite (<5ms). At enterprise scale (100K+ docs), switch to SQLite R-tree for spatial line-range queries.

**Regeneration:** Dropped and rebuilt atomically after each scan. Not incrementally patched — at 6K rows, full rebuild is faster than surgical patching with consistency verification.

### 10. `index_entries`
Authoritative index catalog for LLM-wiki retrieval. Generated after each scan.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `document_id` | FK → documents | |
| `doc_type` | ENUM | code_logic, business_logic |
| `system_slug` | TEXT | |
| `repo_slug` | TEXT | |
| `key_symbols` | TEXT | Comma-separated symbol names for search |
| `summary` | TEXT | One-line description for LLM selection |
| `path` | TEXT | Document path in tree |
| `scan_id` | FK → scans | |
| `created_at` | DATETIME | |

This is what gets rendered as YAML-frontmatter `index.md`. The LLM-wiki retrieval pattern: (1) read index.md, (2) parse structured entries, (3) filter by relevance, (4) read 3-5 full documents, (5) follow cross-repo links, (6) synthesize with citations.

### 11. `scans`
Audit trail of each scan execution.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `repo_id` | FK → repositories | |
| `trigger` | ENUM | manual, git_webhook, scheduled |
| `status` | ENUM | running, completed, failed, cancelled |
| `base_commit_hash` | TEXT | |
| `target_commit_hash` | TEXT | |
| `started_at` | DATETIME | |
| `completed_at` | DATETIME | |
| `documents_created` | INT | |
| `documents_updated` | INT | |
| `documents_unchanged` | INT | |
| `uncertainties_created` | INT | |
| `uncertainties_resolved` | INT | |
| `errors_count` | INT | |
| `log_path` | TEXT | Filesystem path to detailed log |

### 12. `cross_repo_stale_notices`
Informational only — no automatic cascade triggering.

| Column | Type | Notes |
|--------|------|-------|
| `id` | GUID PK | |
| `source_document_id` | FK → documents | The document that changed |
| `target_repo_id` | FK → repositories | The repository that should know about it |
| `target_document_id` | FK → documents | The document in the target system that links here |
| `change_type` | ENUM | content_changed, edge_changed, deleted |
| `notified_at` | DATETIME | |
| `acknowledged_at` | DATETIME | Nullable |

**Why informational only:** Automatic cascade (Repo A change → trigger Repo B re-scan → triggers Repo C re-scan...) creates re-scan storms and deadlocks across repo boundaries. Instead, each repo's System Owner sees stale notices and decides when to re-scan. The graph is eventually consistent by design.

---

## Architecture Components

The scanning system has two layers: a non-LLM Scan Coordinator that manages the operational lifecycle, and per-repo LLM-powered Orchestrator Agents that run scans.

### Scan Coordinator (Non-LLM Background Service)

A singleton service in the ASP.NET Core application. **Not an LLM agent** — it is deterministic application code.

**Responsibilities:**
- **Scan queue:** Maintains a queue of pending scan requests (manual triggers, git webhooks, scheduled). Ensures only one scan runs per repo at a time via `repositories.scan_status` check.
- **Git operations:** Pulls remote changes before scan start. Records `base_commit_hash` and `target_commit_hash` on the `scans` row.
- **Orchestrator lifecycle:** Spawns one Orchestrator Agent per repo scan. Monitors orchestrator health via heartbeat (`scan_agent_logs.last_heartbeat_at`). If the orchestrator stalls (no heartbeat for 5 minutes), marks the scan as `failed` and releases the repo lock.
- **Cross-repo resolution trigger:** When a repo scan completes, notifies the resolution engine to process pending `unresolved/cross_repo` uncertainties targeting that repo.
- **Concurrency control:** Configurable limits on concurrent scans (`MaxConcurrentScans`, default 1 for V1) and workers per scan (`MaxWorkersPerScan`, default 5).

### Orchestrator Agent (LLM Agent, Per-Repo)

An LLM-powered agent spawned by the Scan Coordinator for each repo scan. Uses the Microsoft Agent Framework.

**Responsibilities:**
- **Pre-Scan Profiling:** Runs CodeGraph on the repo, produces `code_units` rows, applies scan filter.
- **Document Pre-Allocation:** Bulk-creates `documents` rows for all code_units before dispatching workers.
- **Superstep dispatch:** Groups related code_units into supersteps, dispatches Worker Agents concurrently, fans in results.
- **Cross-verification coordination:** Provides workers with related drafts within the same superstep.
- **Progress tracking:** Updates `scan_agent_logs` with current status, heartbeat timestamps.
- **Error handling:** Retries failed workers once, marks documents as `failed` on second failure, continues scan.
- **Post-scan:** Triggers reverse_index rebuild, index_entries regeneration, index.md write, cross_repo_stale_notices.

### Worker Agent (LLM Agent)

Dispatched by the Orchestrator to process a single code_unit (Pass 1) or a set of CL Docs (Pass 2).

### Concurrency Model

| Limit | V1 Default | Mechanism |
|-------|-----------|-----------|
| Concurrent scans (global) | 1 | Scan Coordinator queue + `repositories.scan_status` guard |
| Workers per scan | 5 | Orchestrator superstep fan-out limit |
| SQLite writers | 1 (WAL mode) | EF Core transaction serialization |
| SQLite readers during scan | Unlimited (WAL mode) | Concurrent reads allowed |

**V1 rationale:** A single concurrent scan is sufficient for the demo (~12 repos, scanned sequentially or one-at-a-time). SQLite WAL mode allows users to browse existing documents while a scan writes new ones. V2 adds concurrent scan support with PostgreSQL.

---

## Document Generation Process (The Scanning Pipeline)

The data model above describes the graph at rest. This section describes how the graph is *built* — the scanning pipeline that creates documents, edges, and uncertainties, and the resolution process that links documents across repository boundaries.

### Scan Lifecycle

```
Pre-Scan Profiling
  → CodeGraph parses repos into code_units
  → Scan filter excludes non-code files
  → Communication Pattern Catalog loaded

Document Pre-Allocation
  → One documents row created per code_unit (status = 'pending', title from symbol_name)
  → Document IDs are now known — all Pass 1 edges can reference them
  → Tree structure established via parent_document_id mirroring source directories

Pass 1: Code Logic Documents
  → Orchestrator dispatches workers per code_unit in superstep batches
  → Workers produce CL Doc body + document_sources rows + REFERENCES edges
  → Workers receive drafts from related workers in same superstep for cross-verification
  → On worker failure: retry once, then mark document status = 'failed', continue scan

Pass 2: Business Logic Documents
  → Orchestrator dispatches one BL Doc worker per system (not per flow)
  → Worker receives all CL Docs in the system, identifies business flows autonomously
  → Worker produces BL Docs + GENERATED_FROM edges to source CL Docs

Cross-Repo Resolution
  → unresolved/cross_repo uncertainties matched against target repo's docs
  → endpoint_identifier match requires repo-qualified lookup (not raw string match)
  → open uncertainties re-evaluated when communication patterns are updated
  → Deadlock-breaking protocol for circular dependencies

Post-Scan
  → reverse_index rebuilt atomically (temp table + RENAME in transaction)
  → index_entries regenerated from current documents
  → index.md written (YAML frontmatter from index_entries)
  → log.md appended (scan event with parseable timestamp)
  → cross_repo_stale_notices posted
  → Orphaned markdown files reconciled (files without DB rows → archived)
```

### Pre-Scan Profiling

Before any worker is dispatched, the orchestrator performs reconnaissance over the repository:

1. **CodeGraph parses source files** into `code_units` rows: one per function, method, class, or file-level scope, with exact `line_start`/`line_end` from the AST. No LLM involvement — this is deterministic tree-sitter parsing. `range_confidence` is set to `high` for AST-verified ranges, `medium` for regex-fallback ranges, `low` for heuristic ranges.

2. **Language and framework detection** populates `repositories.language` and identifies which Communication Pattern Catalog entries apply.

3. **Scan filter** excludes build artifacts, generated code, test fixtures, and configuration data from the `code_units` pool. Only filtered code_units are dispatched to workers.

4. **Batch planning:** The orchestrator groups `code_units` into supersteps. Related code_units (caller/callee pairs within the same file, classes in the same module) are batched together so that cross-verification (FR-4.12) can run within the same superstep.

### Document Pre-Allocation

After profiling but before dispatching any workers, the orchestrator bulk-creates `documents` rows for every filtered `code_unit` in the scan (after the scan filter has excluded build artifacts, generated code, test fixtures, and configuration data):

1. **One document row per filtered code_unit:** `doc_type = 'code_logic'`, `status = 'pending'`, `title` derived from `code_unit.symbol_name` (e.g., `RiskEvaluator.evaluate()`), `content_md_path = NULL`, `is_current = true`, `version = 1`.

2. **Tree structure:** All CL Docs have `parent_document_id = NULL` initially. The source-mirroring tree (FR-4.6) is computed from document `path` strings at display time — the Blazor TreeView groups by path segment, showing `src/` → `main/` → `java/` → `com/` → `bank/` → `risk/` containing the RiskEvaluator and CreditCheckService documents. No synthetic directory-document rows are created. BL Docs have `parent_document_id` set explicitly to their logical grouping (e.g., `business-flows/risk-evaluation/`). This avoids status-model problems (a synthetic row has no code_unit, no worker, and fits none of `pending`/`generated`/`failed`/`superseded`).

3. **Document IDs are now known across the entire scan.** Every worker can create `REFERENCES` edges to any other document in the same repo — the target `document_id` exists in the database and satisfies the FK constraint. The target's body will be filled in when its worker runs.

This step eliminates "dangling references within the same repo." The only uncertainties are cross-repo.

### Pass 1: Code Logic Documents

**Dispatch:** The orchestrator fans out `code_units` to Worker Agents in superstep batches. A superstep is a group of related code_units (caller/callee pairs within the same file, classes in the same module) dispatched concurrently to enable cross-verification within the batch. Each worker receives one `code_unit` — the source text within `line_start`..`line_end`, the `symbol_name`, the list of pre-allocated document IDs for related code_units, and supplementary context (neighboring code_units, import statements, framework patterns from the Communication Pattern Catalog).

**Generation:** The Worker Agent reads the code and produces a Code Logic Document — a structured markdown file describing what the code does: architecture role, data flow, call chains, framework usage. On success: the markdown is written to `content_md_path`, the pre-allocated `documents` row is UPDATED (content_md_path, metadata_json set; status → 'generated'). On failure (LLM timeout, malformed output, rate-limit): the orchestrator retries once. If the retry also fails, the document's `status` is set to `'failed'` and the scan continues (NFR-3.1: a failed worker shall not fail the entire scan).

**Source linking:** The worker emits `document_sources` rows linking the new document to:
- The primary `code_unit` it was generated from (role = `primary`)
- Any additional `code_units` it references or cites (role = `referenced`)

**Edge creation — within-system REFERENCES:**

When the worker identifies that its code_unit calls another function/method/class:

1. **Target in same repo (any state):** All `documents` rows were pre-allocated — the target `document_id` always exists. The worker inserts a `REFERENCES` edge immediately. The `protocol`, `endpoint_identifier`, `provider_repo_id`, and `consumer_repo_id` columns are populated from the Communication Pattern Catalog if the call matches a known pattern. The edge is valid regardless of whether the target has been generated yet.

2. **Target is in a different repo — cross-repo call detected:** The worker cannot create a `REFERENCES` edge because the target document doesn't exist in the local database (different repo, possibly different scan). Instead, it creates an `uncertainties` row:
   ```
   status = 'unresolved'         -- "discovered-but-unresolved" (PRD §3)
   sub_status = 'cross_repo'     -- internal refinement: known to be cross-repo
   type = 'external_system'
   endpoint_identifier = <repo_slug:endpoint — e.g., "risk-service:RiskService.evaluate">
   target_repo_id = <resolved via Communication Pattern Catalog, or NULL>
   description = "Call to RiskService.evaluate() in risk-service repo — target not yet scanned"
   ```
   The `REFERENCES` edge is NOT created now. It will be created later during cross-repo resolution. The uncertainty IS the placeholder — Blazor.Diagrams renders it as a dashed edge to a diamond-shaped node labeled with the endpoint_identifier (FR-12.6).

3. **Target is completely unknown:** If the worker detects a call but cannot identify the target repo at all (no Communication Pattern Catalog match, no Service Topology MCP result), it creates an `uncertainties` row with `status = 'unresolved'`, `sub_status = 'unknown_target'`, `target_repo_id = NULL`, `endpoint_identifier` set to whatever was extractable (URL string literal, method name pattern). This appears in the uncertainty dashboard for human review (HITL, FR-7.1).

**Worker failure handling:**
- First failure: retry once with the same code_unit and prompt.
- Second failure: set document `status = 'failed'`, record error in `scans.errors_count`, continue scan.
- Documents with `status = 'failed'` appear in the graph as red-bordered nodes. Edges pointing to them (from other documents that reference them) are valid — the edge exists, the target exists, but the content is missing.
- On scan retry (manual trigger), failed documents are re-dispatched to workers.

**Cross-verification (FR-4.12):** Within a superstep, the orchestrator provides each worker with the draft output (markdown body and claimed edges) of related workers in the same batch, via a shared DB read of the just-inserted `documents` and `edges` rows. The workers include a cross-verification section in their output:
- "I verified that [related doc] correctly describes [relationship]. We agree."
- OR: "I disagree with [related doc] on [specific claim]. My analysis shows [alternative]."
Disagreements produce `uncertainties` rows with `type = 'ambiguous_logic'`, recording both workers' claims. These uncertainties appear in the HITL dashboard for human arbitration. They do NOT block the scan — both documents are marked `status = 'generated'` with the disagreement noted in `metadata_json`.

**Disputed edge handling:** If Worker A inserted a `REFERENCES` edge before Worker B disputed it, the edge is NOT deleted. It is kept in place with `confidence` lowered to `0.5` and the uncertainty ID recorded in `metadata_json` (e.g., `{"disputed_by_uncertainty": "<uuid>"}`). The edge represents Worker A's evidence — deleting it loses the signal. The Blazor.Diagrams renderer draws disputed edges as yellow dashed lines with a warning icon. On HITL resolution: if the human confirms the edge is correct, `confidence` is restored to `1.0` and the uncertainty is closed. If the human overrides (Worker B was right), the disputed edge is deleted, the correct edge is created, and the uncertainty is closed.

### Pass 2: Business Logic Documents

After all CL Docs for a system are generated (or failed), the orchestrator begins Pass 2.

**Dispatch strategy — one worker per system (not per flow):** The original design proposed dispatching per "business flow" identified from the graph, but this has a circular dependency: flows can't be identified without analyzing the graph, which is the Pass 2 work itself. Instead, V1 dispatches a single Pass 2 worker per system. The worker receives:
- The full set of CL Docs in the system (their markdown bodies, metadata, and REFERENCES edges)
- The Communication Pattern Catalog for the system
- A prompt instructing it to identify business flows autonomously from the CL Doc graph

**Flow identification (done by the worker, not the orchestrator):** The worker analyzes the REFERENCES graph between CL Docs and identifies clusters that form business processes. Heuristics: entry points (controller handlers, queue consumers, scheduled tasks), termination points (database writes, external API calls, response returns), path coherence (a sequence of CL Docs that together process a single user action or business event). The worker names each flow (e.g., "Risk Evaluation," "Loan Approval") and produces one BL Doc per flow.

**Generation:** For each identified flow, the worker synthesizes a business-level narrative: validation rules, decision flows, regulatory checks, process steps. Each BL Doc is written to `content_md_path` with `doc_type = 'business_logic'`, `status = 'generated'`. Its `parent_document_id` points to a logical path (e.g., `business-flows/risk-evaluation/`).

**Provenance edges:** For each CL Doc the BL Doc was built from, the worker inserts a `GENERATED_FROM` edge:
```
source = <BL Doc ID>
target = <CL Doc ID>
edge_type = 'GENERATED_FROM'
confidence = 0.85  (LLM-generated, not deterministic)
```

These edges drive two-pass re-scan: when a CL Doc changes, any BL Doc with a `GENERATED_FROM` edge to it is flagged stale.

**Cross-BL-Doc references:** If the worker identifies that one business flow feeds into another (e.g., "Risk Evaluation" output feeds into "Loan Approval"), it inserts a `REFERENCES` edge between the two BL Docs. BL-to-BL REFERENCES edges are structurally identical to CL-to-CL REFERENCES edges — both use `edge_type = 'REFERENCES'` in the same `edges` table. They can be intra-repo (two BL Docs in the same repo) or cross-repo (BL Doc in repo A references BL Doc in repo B). The impact analysis recursive CTE walks all REFERENCES edges uniformly regardless of doc_type.

**BL Doc regeneration on re-scan:** During incremental re-scan, the impact analysis recursive CTE walks both `REFERENCES` and `GENERATED_FROM` edges. CL Doc changes propagate through `GENERATED_FROM` to flag BL Docs stale. Stale BL Docs are regenerated using the same Pass 2 worker, which receives the updated CL Docs and produces new BL Doc versions. Old BL Docs are superseded (`is_current = false`, `superseded_by_id` → new version). BL-to-BL `REFERENCES` edges are copied forward and re-evaluated.

### Cross-Repo Resolution

After a repo's scan completes (Pass 1 + Pass 2), the resolution engine runs. This is where dangling cross-repo links get resolved or formally recorded as known unknowns.

**Resolution trigger:** When a repo scan completes, the orchestrator notifies the resolution engine. The resolution engine queries all `uncertainties` rows where `status = 'unresolved'` AND `sub_status = 'cross_repo'` AND `target_repo_id = <completed_repo_id>`.

**Repo-qualified endpoint matching:** Raw endpoint strings can collide across repos (two repos may both expose `/api/users`). The resolution engine matches on the composite key `(target_repo_id, endpoint_identifier)`, not on `endpoint_identifier` alone. Additionally, the Communication Pattern Catalog for the target repo provides canonical endpoint patterns that normalize identifier formats before matching.

**Matching algorithm:**

```
For each unresolved uncertainty targeting the completed repo (sub_status = 'cross_repo'):
  1. Normalize the endpoint_identifier using the target repo's Communication Pattern Catalog
     (e.g., strip query params from URLs, normalize topic name separators).
  2. Query the target repo's documents WHERE:
     - provider_repo_id = <completed_repo_id>
     - endpoint_identifier_normalized = @normalized_id
  3. If exactly one match found (high confidence):
     - Create a REFERENCES edge:
         source = uncertainty.document_id
         target = matched_document.id
         edge_type = 'REFERENCES'
         confidence = 0.9
     - Set uncertainty.status = 'resolved'
     - Set uncertainty.resolved_by_document_id = matched_document.id
  4. If multiple matches found (ambiguous):
     - Create a REFERENCES edge to the best match
     - Set uncertainty.status = 'resolved'
     - Add resolution note: "Matched to X; alternatives: Y, Z"
     - Edge confidence lowered to 0.6
  5. If no match found:
     - Uncertainty stays 'unresolved' with sub_status = 'cross_repo'
     - This is acceptable (FR-6.4: dangling links are known unknowns)
     - Will be retried when the target repo is re-scanned
```

**Uncertainty state transitions — full lifecycle:**

```
                     ┌─────────┐
    Agent discovers  │         │  HITL review determines
    cross-repo ────→ │unresolved│ ←── target repo ──────┐
    call             │(cross_  │                        │
                     │ repo)   │                        │
                     └────┬────┘                        │
                          │                             │
                ┌─────────┼──────────┐                  │
                │         │          │                  │
          Target repo   Target     Human               │
          scanned +     repo       marks as            │
          matched ──→   never      "known              │
                │       scanned    unknown"            │
                │       (dangling)    │                │
                ▼         │           ▼                │
           ┌────────┐     │      ┌────────┐           │
           │resolved│     │      │ wontfix│           │
           └────────┘     │      └────────┘           │
                          │                            │
                          ▼                            │
                     stays 'unresolved'                │
                     (retried on re-scan)              │
                                                       │
   ┌─────────┐                                         │
   │ unknown │  Agent detected a call but              │
   │(unknown │  could not identify target              │
   │ _target)│  repo or endpoint ──────────────────────┘
   └────┬────┘  New Communication Pattern
        │       Catalog entry added ──→ promoted to
        │       'unresolved' with sub_status = 'cross_repo'
        │
        ▼
   HITL human identifies target ──→ status → 'resolved'
   HITL human marks as known unknown ──→ status → 'wontfix'
```

**Key: 'unknown' is a row now.** The original "unknown = no row" gap is closed. Uncertainties with `status = 'unresolved'` and `sub_status = 'unknown_target'` represent the PRD's "unknown" state — an agent detected something it couldn't classify at all. These appear in the HITL dashboard. When the System Owner adds a Communication Pattern Catalog entry that covers the endpoint pattern, all matching 'unknown_target' uncertainties are promoted to `sub_status = 'cross_repo'` and become eligible for resolution.

**Deadlock-breaking protocol:** When Repo A has `unresolved/cross_repo → B` AND Repo B has `unresolved/cross_repo → A` (mutual pending):

1. Count unresolved cross_repo uncertainties for each repo
2. Repo with fewer pending resolves first (smaller problem surface)
3. If tied: repo with earlier `last_scan_at` resolves first (older scan yields to newer)
4. Once the first repo resolves, the second repo's pending uncertainties may now have matches → resolution proceeds
5. If both resolve and uncertainties remain unmatched on both sides: both stay `unresolved/cross_repo` — manually triaged via HITL dashboard
6. If both have zero pending: no-op (nothing to resolve, no deadlock exists)

**Known V1 limitation — BL Doc stale markers after cross-repo resolution:** When a BL Doc is generated before its cross-repo targets have been scanned, the BL Doc body may contain "unscanned" or placeholder references (e.g., "credit score from riskeng-service (unscanned)"). Cross-repo resolution creates the REFERENCES edge but does NOT trigger BL Doc regeneration — the BL Doc's CL Docs didn't change, so the impact analysis CTE returns empty. The LLM-wiki chat agent compensates at query time by following the now-resolved cross-repo edge and synthesizing a complete answer. Full BL Doc regeneration on cross-repo resolution (walking GENERATED_FROM edges in reverse from the newly-linked CL Doc to find dependent BL Docs) is deferred to V2.

### Dangling Link Handling Summary

| Situation | What happens during scan | What the graph shows |
|-----------|------------------------|---------------------|
| Caller doc created, callee in same repo (any state) | REFERENCES edge created immediately (all doc IDs pre-allocated) | Solid edge to doc node (target may show status='pending' until its worker runs) |
| Caller doc created, callee in different repo, target repo already scanned | Cross-repo resolution runs → REFERENCES edge created | Solid edge between documents across repo boundaries |
| Caller doc created, callee in different repo, target repo NOT scanned | Uncertainty: `status='unresolved'`, `sub_status='cross_repo'` | Dashed edge to diamond node labeled with `repo_slug:endpoint` |
| Caller doc created, callee completely unknown | Uncertainty: `status='unresolved'`, `sub_status='unknown_target'` | Red diamond node, appears in HITL dashboard |
| Target repo later scanned | Resolution engine matches `(target_repo_id, endpoint_identifier_normalized)` | Dashed edge becomes solid, diamond node disappears |
| Target repo scanned but no match found | Uncertainty stays `unresolved/cross_repo` (dangling link — FR-6.4 acceptable) | Dashed edge remains, flagged as "known unknown" |
| Human answers HITL question | Uncertainty `status = 'resolved'` via `resolved_by_human_input` | Edge marked as human-sourced, lower confidence |
| Human marks as accepted dangling link | Uncertainty `status = 'wontfix'` (terminal non-resolution) | Dashed edge remains, marked as "accepted known unknown" |

### Transaction Boundaries

The document generation process must balance atomicity (don't lose work on crash) against simplicity (no distributed transactions). The approach:

1. **Document body (markdown file) written first** — if the DB insert fails, the orphaned file is harmless (periodic reconciliation cleans it up).
2. **DB rows inserted in order:** `documents` row → `document_sources` rows → `edges` rows → `uncertainties` rows. Each step is a separate write within one EF Core transaction.
3. **Scan state persisted** after each superstep completes (not after each individual document). If the server crashes mid-superstep, the superstep is retried (idempotent — code_units are hashed, duplicate documents are detected by `(repo_id, file_path, line_start)` unique constraint).
4. **reverse_index and index_entries rebuilt atomically after scan completes** — not incrementally patched during scan. At demo scale (6K reverse_index rows, 200 index_entries), a full rebuild is < 100ms.

### log.md (FR-4.8)

An append-only activity log with parseable timestamps, written to the filesystem alongside the document tree. Format (one entry per line):

```
## [2026-06-17 14:32] scan:started | repo=risk-service commit=abc123
## [2026-06-17 14:32] scan:pass1 | repo=risk-service files=47
## [2026-06-17 14:35] scan:pass2 | repo=risk-service flows=3
## [2026-06-17 14:35] scan:completed | repo=risk-service docs=52 edges=118 uncertainties=4
## [2026-06-17 14:40] query:chat | user=lin system=mobile-banking question="how does risk evaluation work?"
## [2026-06-17 14:42] scan:incremental | repo=risk-service trigger=git_webhook files=3
```

The `scans` table's `log_path` column points to the per-scan detailed log. `log.md` is the human-readable summary log at the system level. Both are written in the Post-Scan step.

---

## Source Anchoring

### Three-Layer Mechanism

**Layer 1 — Structural (code_units):** CodeGraph/tree-sitter parses source into `code_units` at repository registration (pre-scan profiling), recording `file_path`, `file_identity_hash` (SHA-256 of first 4096 bytes), `line_start`, `line_end`, `symbol_name`, `unit_type`, `source_hash` (SHA-256 of exact source lines), `range_confidence` (CodeGraph-validated: high/medium/low).

**Layer 2 — Semantic (document_sources):** During document generation, the LLM agent emits structured metadata declaring which `code_units` a document is "generated from" (primary role) or "mentions" (referenced role). These become `document_sources` rows.

**Layer 3 — Fast Lookup (reverse_index):** Populated atomically post-generation: for every line in every `code_unit` that a document has a primary link to, insert one row. This is the O(1) source→document mapping.

### Forward Trace (Document → Source)
```sql
SELECT cu.file_path, cu.line_start, cu.line_end, cu.symbol_name, ds.role
FROM documents d
JOIN document_sources ds ON d.id = ds.document_id
JOIN code_units cu ON ds.code_unit_id = cu.id
WHERE d.id = @documentId
```

### Reverse Trace (Source → Documents)
```sql
-- O(1) via reverse_index
SELECT DISTINCT d.id, d.title, d.path
FROM reverse_index ri
JOIN documents d ON ri.document_id = d.id
WHERE ri.repo_id = @repoId
  AND ri.file_path = @filePath
  AND ri.line_number BETWEEN @lineStart AND @lineEnd
  AND d.is_current = 1
```

### Two-Phase Change Detection

**Phase 1 — Line-Overlap Filter (fast, imprecise):**
```sql
SELECT DISTINCT ri.document_id, cu.id as code_unit_id
FROM reverse_index ri
JOIN code_units cu ON ri.code_unit_id = cu.id
WHERE ri.file_path = @changedFilePath
  AND ri.line_number BETWEEN @newLineStart AND @newLineEnd
```
Returns ~5 candidate documents. Most changed lines won't overlap any document's range — eliminated instantly.

**Phase 2 — Hash Verification (precise, only on candidates):**
For each candidate, compare the new code_unit's `source_hash` against the existing one. If hash unchanged → document is still fresh, skip regeneration. If hash changed → document is stale, queue for regeneration.

**Why both phases:** Phase 1 alone would flag documents whose non-overlapping parts of the same file changed. Phase 2 alone would require hashing every document's source for every file change. Together: fast AND correct.

---

## Impact Analysis (4-Step SQL Cascade)

```
Input: List of changed (file_path, line_start, line_end) tuples from git diff
Output: Ordered set of affected document IDs with impact distance
```

### Step 1 — Direct Impact
```sql
SELECT DISTINCT ri.document_id, 1 as distance
FROM reverse_index ri
JOIN code_units cu ON ri.code_unit_id = cu.id
WHERE cu.id IN (@changedCodeUnitIds)
   OR (ri.file_path = @path AND ri.line_number BETWEEN @start AND @end)
```

### Step 2 — Intra-System Transitive Closure
```sql
WITH RECURSIVE closure(doc_id, distance) AS (
    -- Seed: directly affected documents
    SELECT da.document_id, 0
    FROM (SELECT DISTINCT document_id FROM directly_affected) da

    UNION  -- UNION (not UNION ALL) provides cycle detection

    -- Walk: follow all doc→doc edges (REFERENCES + GENERATED_FROM) outward
    SELECT e.target_document_id, c.distance + 1
    FROM edges e
    JOIN closure c ON e.source_document_id = c.doc_id
    JOIN documents d ON e.target_document_id = d.id
    WHERE c.distance < 5  -- Safety limit for cycle dampening
      AND e.edge_type IN ('REFERENCES', 'GENERATED_FROM')
      AND d.system_id IN (
          SELECT DISTINCT d2.system_id
          FROM directly_affected da2
          JOIN documents d2 ON da2.document_id = d2.id
      )
)
SELECT doc_id, MIN(distance) as min_distance
FROM closure
GROUP BY doc_id
```

### Step 3 — Cross-Repo Reachability
```sql
SELECT e.target_document_id, cs.min_distance + 1 as distance,
       dt.repo_id as target_repo_id
FROM edges e
JOIN documents ds ON e.source_document_id = ds.id
JOIN documents dt ON e.target_document_id = dt.id
JOIN intra_affected cs ON e.source_document_id = cs.doc_id
WHERE ds.repo_id != dt.repo_id  -- Cross-repo is a query, not a schema
  AND dt.is_current = 1
```

### Step 4 — Uncertainty Reopening
```sql
SELECT u.id, u.document_id, u.type, u.status, u.sub_status,
       CASE
           WHEN u.document_id IN (SELECT doc_id FROM intra_affected) THEN 'source_changed'
           WHEN u.resolved_by_document_id IN (SELECT doc_id FROM intra_affected) THEN 'resolution_stale'
       END as reopen_reason
FROM uncertainties u
WHERE (u.status = 'resolved'
       AND (u.document_id IN (SELECT doc_id FROM intra_affected)
            OR u.resolved_by_document_id IN (SELECT doc_id FROM intra_affected)))
   OR (u.status = 'unresolved' AND u.sub_status = 'cross_repo'
       AND u.document_id IN (SELECT doc_id FROM intra_affected))
-- Reopens both: resolved uncertainties whose resolution is now stale,
-- AND cross-repo uncertainties in documents that were re-scanned
-- (the regenerated doc may have different cross-repo calls now)
```

---

## Cross-Repo Resolution

> Detailed in [Document Generation Process — Cross-Repo Resolution](#cross-repo-resolution-1) above. This section preserved for architectural emphasis.

### Summary

When a repo scan completes, the resolution engine queries `uncertainties` with `status = 'unresolved'`, `sub_status = 'cross_repo'`, and `target_repo_id = <completed_repo_id>`. Matching uses the repo-qualified composite key `(target_repo_id, endpoint_identifier_normalized)`, not raw string matching. Deadlock-breaking: fewer-pending-first, older-scan-yields. Full lifecycle diagram and state transitions in the Document Generation Process section.

---

## Live Graph Visualization (Blazor.Diagrams)

### Data Flow

```
SQLite (12 tables)
    ↓ EF Core queries
GraphStateService (singleton, in-memory)
    ↓ SignalR hub push
Blazor.Diagrams (renders DiagramNode + DiagramLink)
```

### Key Queries

**FullGraph** — load entire system graph:
```sql
SELECT d.id, d.title, d.doc_type, d.path, d.parent_document_id, d.depth,
       e.id as edge_id, e.source_document_id, e.target_document_id,
       e.edge_type, e.confidence
FROM documents d
LEFT JOIN edges e ON d.id = e.source_document_id
WHERE d.system_id = @systemId
  AND d.is_current = 1
ORDER BY d.depth, d.sort_order
```
~200 nodes, ~400 edges — <50ms on SQLite.

**UncertaintyOverlay** — render open questions as diamond nodes:
```sql
SELECT u.id, u.document_id, u.type, u.description, u.status,
       d.title as document_title
FROM uncertainties u
JOIN documents d ON u.document_id = d.id
WHERE u.status = 'unresolved'
  AND d.system_id = @systemId
```

**ImpactSubgraph** — highlight blast radius: Materializes the impact CTE to a temp table, then applies highlight styling to affected nodes.

**CrossRepoBridges** — global graph view across repos:
```sql
SELECT e.*, ds.title as source_title, ds.system_id as source_system,
       dt.title as target_title, dt.system_id as target_system
FROM edges e
JOIN documents ds ON e.source_document_id = ds.id
JOIN documents dt ON e.target_document_id = dt.id
WHERE ds.repo_id != dt.repo_id
  AND ds.is_current = 1 AND dt.is_current = 1
```

### Layout

- **Default:** Hierarchical — respects `parent_document_id` tree, mirrors source code structure (familiar to developers)
- **Toggle:** Force-directed — nodes spring-layout by edge connections, reveals hidden cross-repo patterns
- **Edge styling:** Intra-repo = solid light-gray. Cross-repo = dashed bold colored. Pending = dashed to placeholder. Uncertainty = red diamond nodes.

### Real-Time Updates

SignalR hub pushes `graph-updated` events on scan completion. Blazor.Diagrams incrementally patches: new nodes enter with animation, removed nodes fade, edges re-color. NFR-1.3: <5 seconds from document generation to graph update.

---

## Incremental Re-Scan

### Trigger
Git webhook or manual "Rescan" button. System compares `last_commit_hash` with remote HEAD.

### V1 Algorithm

1. **Git diff:** `git diff last_commit..HEAD --name-only` → list of changed file paths
2. **Parse changed files:** For each changed file, CodeGraph parses into new `code_units`. Upsert: if `(repo_id, file_path, line_start)` matches an existing code_unit, compare `source_hash`. If hash unchanged, skip. If new code_unit, insert.
3. **Two-phase detection:** For each changed `code_unit`, run Phase 1 (reverse-index line-overlap filter) → Phase 2 (hash comparison on candidates) → produce list of stale documents.
4. **Queue stale documents** for LLM regeneration. Generate new document versions (version N+1).
5. **Supersede:** Set old document `is_current = false`, `superseded_by_id` → new document ID. Old document is never deleted (FR-4.10, FR-9.4).
6. **Re-evaluate edges:** Copy old edges to new document, then LLM confirms or updates each one. New edges may have different targets or confidence.
7. **Reopen uncertainties:** Uncertainties linked to stale documents are set to `status = 'unresolved'` (previously resolved → needs re-verification; previously cross_repo → needs re-evaluation against regenerated document).
8. **Rebuild reverse_index** for affected documents (drop old rows, insert new rows).
9. **Regenerate index_entries** for affected documents.
10. **Post cross_repo_stale_notices** to systems that link to changed documents.
11. **Mark scan complete.**

### File Rename Detection

`file_identity_hash` (SHA-256 of first 4096 bytes) enables rename detection:
```sql
-- Find code_units that might be the renamed version of a deleted file
SELECT * FROM code_units
WHERE file_identity_hash = @oldFileIdentityHash
  AND file_path != @oldFilePath
```
If found: update `file_path` on existing `code_units` (rename), don't create new ones (which would orphan all documents linked to the old path).

---

## LLM-Wiki Retrieval

### index.md Format (YAML Frontmatter)

```markdown
---
entries:
  - doc_id: "abc-123"
    title: "RiskEvaluator.evaluate()"
    doc_type: "code_logic"
    system: "mobile-banking"
    repo: "risk-service"
    key_symbols: "RiskEvaluator, evaluate, CreditCheck, FraudDetection"
    summary: "Evaluates risk for a mobile banking transaction by calling CreditCheck and FraudDetection services"
    path: "src/main/java/com/bank/risk/RiskEvaluator/"
  - doc_id: "def-456"
    title: "Loan Approval Business Flow"
    doc_type: "business_logic"
    system: "mobile-banking"
    repo: "loan-service"
    key_symbols: "loan approval, credit check, underwriting"
    summary: "End-to-end loan approval flow: application → credit check → underwriting → decision"
    path: "business-flows/loan-approval/"
---
```

### Retrieval Workflow

1. Chat agent reads `index.md`
2. Parses YAML frontmatter for structured fields
3. Filters by `system`, `doc_type`, `key_symbols` relevance to user query
4. Selects 3–5 most relevant documents
5. Reads each document's full markdown body from `content_md_path`
6. Follows `REFERENCES` edges to related documents (cross-repo if applicable)
7. Synthesizes answer with citations to source documents (FR-11.6, FR-11.7)

---

## V1 vs V2 Scope

### V1 (Competition Demo, 9 Weeks)

| Included | Excluded |
|----------|----------|
| All 12 tables, EF Core + SQLite, WAL mode | Event sourcing |
| CodeGraph for deterministic code_unit extraction | Surgical document patching |
| SignalR + Blazor.Diagrams live graph | Full-text search (FTS5) |
| Automatic cross-repo uncertainty resolution with deadlock-breaking | Multi-language tree-sitter parsers |
| Two-phase change detection for incremental re-scan | Performance optimization for >1000 docs |
| index.md with YAML frontmatter for LLM-wiki | Cross-repo scan orchestration for circular dependencies |
| Manual + automatic cross-repo edge creation | Graph query API for external consumers |
| Document version history via superseded_by_id linked list | |
| Git webhook → incremental re-scan of changed files | |
| Single Docker container deploy | |

### V2 (Post-Competition)

- Event sourcing layered on top of scan + document tables (backfilled from existing data)
- Surgical document patching (diff-based, only update changed sections)
- Full-text search via SQLite FTS5
- Multi-language tree-sitter parsers
- Graph query API (REST + GraphQL)
- Cross-repo scan orchestration (deadlock detection for circular dependencies)
- Performance optimization for >1000 documents (SQLite R-tree for spatial line-range queries)
- In-memory graph materialization at startup for sub-millisecond traversal

### Migration Path

V1 tables are a subset of V2. V2 adds tables (`event_log`, `document_patches`) and columns (`edges.confidence` gets more nuanced values). Core tables (`documents`, `edges`, `uncertainties`, `reverse_index`) are stable from V1. V2's event log can be backfilled from existing `scans` + `documents` tables.

---

## What Each Competing Plan Contributed

### From AnchorDB (Foundation)
SQLite + EF Core as sole storage engine, adjacency-list edges table, recursive CTEs for impact analysis, `parent_document_id` tree hierarchy, `superseded_by_id` linked-list version history, 4-phase scan workflow, uniform cross-repo edge handling.

### From ArborGraph (Keystone Feature)
Reverse-index table (one row per source line per document) enabling O(1) source→document lookup. Two-phase sub-file change detection. Scan journal with atomic writes. Cooperative file locking. Explicit rename handling via `file_identity_hash`. The philosophical insight that tree and graph answer different questions.

### From Riverbed Graph (Conceptual Framework)
Cross-repo as emergent topological property (not a schema concept). Typed edge vocabulary. First-class uncertainty with full status lifecycle. Edge confidence scores (0.0–1.0). Provider/Consumer role fields. CodeGraph integration for deterministic code_unit extraction.

---

## Rejected Ideas (and Why)

| Idea | Source | Rationale |
|------|--------|-----------|
| Event sourcing in V1 | Riverbed (original) | Building a custom event store in 9 weeks is not credible |
| Separate graph DB (Neo4j, etc.) | Riverbed (original) | Violates single-container deploy (NFR-4.1); CTEs provide same capability |
| "Unknown = no row" | AnchorDB (original) | Makes uncertainty invisible to renderer; every state must be a row |
| vis.js/D3 for visualization | ArborGraph (original) | Violates FR-12.6 (requires Blazor.Diagrams) |
| Bidirectional edge storage | Riverbed (original) | Doubles edge count, creates consistency problems; reverse via JOIN swap |
| CROSS_REPO as edge_type | AnchorDB (original) | Type inconsistency with REFERENCES; cross-repo is endpoint topology |
| Filesystem as sole database | ArborGraph (core) | No ACID across files; filesystem retained for markdown bodies only |
| consumer_count as stored column | AnchorDB (original) | Drift inevitable; replaced with `COUNT(DISTINCT ...)` query-time aggregate |

# Document Graph Architecture — Decision Log

**Date:** 2026-06-17
**Final plan:** [document-graph-anchordb-plus-plan.md](document-graph-anchordb-plus-plan.md)

---

## Phase 1: Candidate Proposals

Three independent architects proposed document graph designs for Vulgata, each from a different philosophy.

### Candidate A: AnchorDB (Relational Adjacency in SQLite)
**Key insight:** The adjacency list IS the graph database. A single `document_links` table + recursive CTE provides everything a dedicated graph DB gives you — path traversal, blast radius, dependency chains — without leaving SQLite. Cross-system edges are first-class rows in the same table; `system_id` on each node is just an attribute.

Four core tables: documents, code_units, document_links, scans. Impact analysis via 3-step recursive CTE. Tree via `parent_document_id`, graph via `document_links` — orthogonal concerns.

### Candidate B: ArborGraph (File-System Native + Sidecar YAML)
**Key insight:** The directory tree and dependency graph answer different questions. By storing both in side-by-side files, with `.reverse-index.yaml` as the keystone for O(1) source→document lookup, impact queries become hash lookups without any database.

Documents as markdown in source-mirroring directory tree. Sidecar YAML per document. `[[wikilinks]]` for cross-references. No SQL — filesystem IS the database.

### Candidate C: Riverbed Graph (Graph-Native + Event Log)
**Key insight:** Cross-system is not a schema concept but an emergent topological property — any edge whose endpoints span subgraph boundaries. Six node types, seven edge types, ten event types. Everything is a typed edge in a unified graph.

Append-only event log as source of truth. In-memory adjacency layer materialized from events. Impact analysis is constrained BFS.

---

## Phase 2: Judge Panel

Four independent judges evaluated all three candidates across four dimensions:

| Dimension | AnchorDB | ArborGraph | Riverbed Graph |
|-----------|:---:|:---:|:---:|
| Simplicity & Buildability | **8** | 5 | 3 |
| Source Tracing & Impact | 7 | 7 | **8** |
| Cross-System & Visualization | 8 | 5 | **9** |
| Incrementality & Evolution | 6 | 7 | **9** |
| **Average** | **7.25** | 6.0 | **7.25** |

**Judge 1 (Simplicity):** "Design A is the only design that can credibly be built and demoed within 9 weeks. Design B looks simple but is a swamp of cross-file consistency. Design C is architectural overreach."

**Judge 2 (Source Tracing):** "Design C wins on correctness and completeness — the stated priorities. Design B has the single best feature (O(1) reverse-index lookup). Design A's cross-system step is name-dropped without mechanism."

**Judge 3 (Cross-System & Visualization):** "Design C leads on Blazor.Diagrams alignment and cross-system modeling. Design A's fatal flaw is 'unknown = no row' — the unknown state is invisible to the renderer. Design B bypasses Blazor.Diagrams for vis.js/D3."

**Judge 4 (Incrementality):** "Design C wins on all five criteria. Event-sourced with typed events, scan-boundary diffs, replayable audit trail. Design A needs better file-identity tracking."

---

## Phase 3: Initial Synthesis — AnchorDB+

Took SQLite/EF Core from A (pragmatic storage), reverse-index from B (O(1) keystone), typed edges + first-class uncertainty + cross-system-as-topology from C (conceptual framework).

11 tables (later 12 after grill fixes), recursive CTEs for impact analysis, cross-system as query-time emergent property (`ds.system_id != dt.system_id`), first-class uncertainty table.

---

## Phase 4: Rotating Grill-with-Docs Debate

One round of adversarial grilling with 3 groups (judger + examinee per plan), role rotation planned for subsequent rounds. **Orchestrator called early stop** — all three plans independently converged on the same core architecture.

### Round 1 Improvements

| Plan | Improvements | Key Changes |
|------|:---:|-------------|
| AnchorDB | 31 | Cross-system resolution elevated to V1, CodeGraph line-range validation, file_identity_hash rename detection, SignalR live updates, index_entries table |
| ArborGraph | 42 | Replaced vis.js/D3 with Blazor.Diagrams, corrected impact analysis to follow INBOUND edges, added 3-state uncertainty, added Provider/Consumer roles, scan journal with atomic writes |
| Riverbed Graph | 34 | Deferred event sourcing to V2, V1 uses SQLite + EF Core, simplified from 7 edge types to 5, stole two-phase change detection + reverse-index from ArborGraph |

### Convergence Discovery

Three plans from radically different starting philosophies (relational, filesystem-native, graph-native) independently discovered the same 11 architectural patterns. Remaining differences were implementation substrate preferences, not architectural disagreements.

### Ideas Rejected (with Rationale)

| Idea | Source | Why Rejected |
|------|--------|-------------|
| Event sourcing in V1 | Riverbed (original) | Not credible for 9-week timeline |
| Separate graph DB (Neo4j, etc.) | Riverbed (original) | Violates single-container deploy (NFR-4.1) |
| "Unknown = no row" | AnchorDB (original) | Makes uncertainty invisible to renderer |
| vis.js/D3 for visualization | ArborGraph (original) | Violates FR-12.6 (requires Blazor.Diagrams) |
| Bidirectional edge storage | Riverbed (original) | Doubles edge count, consistency problems |
| CROSS_SYSTEM as edge_type | AnchorDB (original) | Type inconsistency; cross-system is endpoint topology |
| Filesystem as sole database | ArborGraph (core) | No ACID across files |
| consumer_count as stored column | AnchorDB (original) | Drift inevitable |

---

## Phase 5: Second Grill (Single Judger on Winner Plan)

Grill score: **5/10** — solid relational schema, unimplementable process.

### Critical Issues Found

1. **Pass 2 dispatch hand-wave:** Per-flow dispatch had a circular dependency (can't identify flows without analyzing graph, which IS the Pass 2 work)
2. **Cross-verification (FR-4.12) no mechanism:** Workers "see each other's drafts" but no shared-state protocol defined
3. **Document pre-allocation undefined in lifecycle:** Referenced but no step in the Scan Lifecycle
4. **endpoint_identifier no system qualification:** Cross-system collisions possible
5. **No LLM worker failure handling:** Entire pipeline depends on LLM calls
6. **Uncertainty state model drifted from PRD:** 4 database states vs. PRD's 3

### Fixes Applied to Winner Plan

- Document Pre-Allocation step added to Scan Lifecycle
- Pass 2 changed to one-worker-per-system (avoids circular dependency)
- Cross-verification mechanism: shared DB read within superstep
- System-qualified endpoint matching via normalized composite key
- Worker failure: retry-once → mark failed → continue scan
- Uncertainty status aligned to PRD 3-state model with `sub_status` refinement
- documents.status column added (pending/generated/failed/superseded)
- communication_patterns table added
- 12 tables total

---

## Phase 6: Terminology Shift — System → Repo Boundary

Decision: scans are per-repo, not per-system. The graph boundary discriminator changed from `system_id` to `repo_id`. Applied across all table names, column names, SQL queries, and prose (~40 edits).

### Changes Applied

| Old | New |
|-----|-----|
| `ds.system_id != dt.system_id` | `ds.repo_id != dt.repo_id` |
| `edges.provider_system_id` / `consumer_system_id` | `provider_repo_id` / `consumer_repo_id` (FK → repositories) |
| `uncertainties.target_system_id` | `target_repo_id` (FK → repositories) |
| `cross_system_stale_notices` | `cross_repo_stale_notices` |
| `sub_status = 'cross_system'` | `sub_status = 'cross_repo'` |
| "Cross-System" (section titles, prose) | "Cross-Repo" (~25 occurrences) |

### Added Architecture Components

- **Scan Coordinator:** Non-LLM background service for scan queue, git operations, orchestrator lifecycle, concurrency control
- **Orchestrator Agent:** LLM agent, per-repo, spawned by Scan Coordinator
- **Concurrency Model:** Configurable limits (concurrent scans: 1, workers per scan: 5)

---

## Phase 7: Final QA Grill — 6 Issues Found and Fixed

1. **Edge dispute handling:** Disputed edges kept with confidence=0.5, linked to uncertainty, HITL resolution path described
2. **UNIQUE constraint on edges dropped:** Multiple REFERENCES edges between same two documents allowed (different protocols/endpoints)
3. **Synthetic directory documents removed:** Virtual tree from document paths replaces synthetic rows
4. **log.md (FR-4.8) added:** Post-Scan step includes append-only activity log with parseable timestamps
5. **Page navigation low-confidence:** `protocol = 'page_navigation'` edges must have `confidence <= 0.5` (FR-5.6)
6. **BL Doc stale markers acknowledged:** Known V1 limitation — chat agent compensates at query time, full fix deferred to V2

### PRD Coverage Audit Result

Full audit of FRs in PRD sections 5.4–5.9 against the plan. No contradictions found. One gap (log.md — fixed). Six items delegated to other design docs (Prompt Workbench, object flow analysis, global knowledge injection, HITL priority, third-party library handling). Nothing goes beyond the PRD.

---

## Key Insights That Survived Every Round

1. Cross-repo is query-time topology, not a schema — `ds.repo_id != dt.repo_id`
2. Two-phase sub-file change detection — O(1) filter + precise hash
3. Reverse-index table is the keystone — O(1) source→document lookup
4. Uncertainty must be first-class — every state is a row
5. Typed edge vocabulary gives unambiguous traversal semantics
6. Tree (parent_document_id) and graph (edges) are orthogonal concerns
7. CodeGraph/tree-sitter determines code_unit boundaries — not LLMs
8. The adjacency list IS the graph database at Vulgata's scale
9. Document immutability via superseded_by_id linked list
10. SignalR is the correct tech for live graph updates (NFR-1.3)

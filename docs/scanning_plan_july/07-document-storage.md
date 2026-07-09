# 07 — Document Storage: Filesystem vs. Document Database

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Section 07 completed - PostgreSQL-first hybrid document storage agreed and ready to use as input for section 08.

---

## Original Open Questions from July Review

> These are concerns raised that need to be addressed before finalizing this section.

1. **Current plan: documents stored as markdown files on the filesystem.** The `documents` table stores metadata (title, path, status, version, etc.) while `content_md_path` points to a markdown file on disk. This has advantages:
   - Simple to implement (no blob storage infrastructure)
   - Git-friendly (markdown files can be versioned alongside source)
   - Human-readable (can open in any editor)
   - LLM-friendly (markdown is the native format for most LLMs)

   But also drawbacks:
   - No structured fields — all content is free-text markdown
   - No querying within document bodies (unless loaded and parsed)
   - Consistency: the DB row and the file can drift (file deleted, DB row remains)
   - No partial updates — updating a document means rewriting the entire file
   - Cross-referencing between documents requires parsing markdown or relying on DB edges

2. **Should a document database (MongoDB) be introduced for more structural documents?** Alternatives to consider:
   - **MongoDB (document store):** Store each document as a JSON document with structured fields (sections, key-value metadata, embedded references). Enables field-level queries and partial updates.
   - **PostgreSQL JSONB:** Keep documents in PostgreSQL as JSONB columns. ACID guarantees, same transactions as graph metadata, no additional infrastructure.
   - **Hybrid:** Metadata and graph in PostgreSQL relational tables; document bodies in PostgreSQL JSONB (structured) or TEXT (markdown). Filesystem used only for export/backup.

   **Comparison:**

   | Aspect | Filesystem (current) | PostgreSQL JSONB | MongoDB |
   |--------|---------------------|------------------|---------|
   | Structured fields | ❌ Free-text only | ✅ JSON schema | ✅ Document schema |
   | Query within docs | ❌ Must load & parse | ✅ JSONPath / GIN indexes | ✅ Native queries |
   | Partial updates | ❌ Rewrite entire file | ✅ `jsonb_set()` | ✅ `$set` operator |
   | ACID with graph data | ❌ Separate systems | ✅ Same transaction | ❌ Separate DB |
   | Git versioning | ✅ Native | ❌ Needs export | ❌ Needs export |
   | Human readability | ✅ Any editor | ❌ Need SQL client | ❌ Need MongoDB client |
   | Infrastructure | ✅ None (filesystem) | ✅ Already have PostgreSQL | ❌ New service (violates docker-compose NFR-4.1) |
   | LLM consumption | ✅ Native format | ⚠️ Needs conversion | ⚠️ Needs conversion |

3. **What would "structured documents" look like?** Instead of a single markdown blob, a document could have:
   ```json
   {
     "title": "RiskEvaluator.evaluate()",
     "sections": [
       {
         "heading": "Purpose",
         "content": "Evaluates risk for a mobile banking transaction...",
         "references": ["doc-456", "doc-789"]
       },
       {
         "heading": "Call Chain",
         "content": "Called by CheckoutHandler → calls CreditCheck → calls FraudDetection",
         "references": ["doc-123", "doc-456"]
       },
       {
         "heading": "Database Interactions",
         "content": "Reads from risk_config table, writes to risk_audit_log",
         "references": []
       }
     ],
     "metadata": {
       "entry_point": true,
       "framework": "Spring Boot",
       "protocol": "rpc"
     }
   }
   ```
   This would enable:
   - Section-level retrieval (chat agent asks "what database does this touch?" → only load the Database Interactions section)
   - Structured reference validation (references are arrays, not embedded in prose)
   - LLM can still consume it (convert to markdown at query time, or feed JSON directly)

4. **Migration complexity.** If we move to structured documents, existing markdown documents need migration. Options:
   - LLM-based migration (feed markdown → LLM extracts sections → store as JSON)
   - Start fresh with V2, keep V1 as filesystem-based
   - Dual-write during transition period

5. **Document versioning with structured storage.** Current plan uses `superseded_by_id` linked list for version history. With structured documents, version diffing becomes possible — store only changed sections, not entire document duplicates.

---

## Extracted Content from Original Plan

### `documents` table — storage fields

| Column | Type | Notes |
|--------|------|-------|
| `content_md_path` | TEXT | Filesystem path to markdown body |
| `metadata_json` | TEXT | Arbitrary LLM-generated metadata |

### Transaction Boundaries (excerpt)

1. **Document body (markdown file) written first** — if the DB insert fails, the orphaned file is harmless (periodic reconciliation cleans it up).
2. **DB rows inserted in order:** `documents` row → `document_sources` rows → `edges` rows → `uncertainties` rows. Each step is a separate write within one EF Core transaction.
4. **reverse_index and index_entries rebuilt atomically after scan completes.**

### `index_entries` table — for LLM-wiki retrieval

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `document_id` | FK → documents | |
| `key_symbols` | TEXT | Comma-separated symbol names for search |
| `summary` | TEXT | One-line description for LLM selection |
| `path` | TEXT | Document path in tree |

This is what gets rendered as YAML-frontmatter `index.md`.

### `log.md` (FR-4.8)

An append-only activity log written to the filesystem. Format:
```
## [2026-06-17 14:32] scan:started | repo=risk-service commit=abc123
## [2026-06-17 14:35] scan:completed | repo=risk-service docs=52 edges=118 uncertainties=4
## [2026-06-17 14:40] query:chat | user=lin system=mobile-banking question="how does risk evaluation work?"
```

---

## Notes for Revision

- PostgreSQL JSONB seems like the pragmatic middle-ground: structured enough for field-level queries and partial updates, no new infrastructure, same ACID transactions as the graph metadata. The key question is whether JSONB queries at demo scale (~200 docs) are fast enough. Answer: yes, trivially.
- The filesystem isn't going away entirely — markdown export for human consumption and git versioning is valuable. A "write-through" pattern could work: store canonical document in JSONB, export to filesystem on scan completion.
- Earlier review considered filesystem storage acceptable for a small demo, but the finalized direction below chooses PostgreSQL-first storage from the beginning because section 06 now requires structured BL trace metadata and section 08 needs database-backed retrieval hooks.

---

## Finalized Direction for Section 07

### 1. Core decision

V1 should use **PostgreSQL-first hybrid document storage**.

That means:

- PostgreSQL is the canonical store for Documents
- document bodies are stored in PostgreSQL, not only on the filesystem
- each generated Document has both structured JSONB and rendered Markdown forms
- filesystem Markdown is an export artifact, not the source of truth
- MongoDB is not introduced in V1

The recommended V1 storage model is:

```text
documents
  relational identity, tree, status, version, summary fields
  content_jsonb      canonical structured document payload
  content_markdown   rendered Markdown cache for display and LLM full-document reads
  export_md_path     optional generated filesystem export path
```

This keeps Vulgata inside the existing PostgreSQL deployment while giving section 08 enough structure for retrieval.

### 2. Why not filesystem as canonical storage?

Filesystem Markdown is useful, but it should not be authoritative.

The main reasons:

- DB rows, graph edges, uncertainties, dispatch sites, and document bodies need transactional consistency.
- section-level retrieval should not require parsing Markdown files on every query.
- Pass 2 trace metadata from section 06 needs a structured home.
- generated Documents are immutable, so PostgreSQL can store full version snapshots cleanly.
- retrieval, graph visualization, and impact analysis already begin from database state.

The filesystem should remain valuable for:

- human-readable exports
- review packets
- offline snapshots
- optional git-friendly artifacts
- debug inspection

But if the filesystem export and PostgreSQL disagree, PostgreSQL wins and the export is regenerated.

### 3. Why not MongoDB?

MongoDB is not recommended for V1.

Reasons:

- it adds another service to the deployment
- it splits transaction boundaries between graph metadata and document bodies
- it complicates migrations, backups, and local demo setup
- PostgreSQL JSONB already provides the needed document structure

MongoDB may be reconsidered only if PostgreSQL JSONB becomes a proven bottleneck at much larger scale.

### 4. Canonical document payload

`content_jsonb` should store a structured document payload with a schema version.

Recommended shape:

```json
{
  "schema_version": 1,
  "title": "RiskEvaluator.evaluate()",
  "doc_type": "code_logic",
  "summary": "Evaluates risk for a mobile banking transaction.",
  "sections": [
    {
      "id": "purpose",
      "kind": "purpose",
      "heading": "Purpose",
      "content_markdown": "Evaluates risk for a mobile banking transaction.",
      "source_refs": ["code_unit:..."],
      "document_refs": ["document:..."],
      "uncertainty_refs": []
    }
  ],
  "retrieval": {
    "key_symbols": ["RiskEvaluator", "evaluate"],
    "business_terms": ["risk evaluation"],
    "technical_terms": ["credit check", "fraud detection"]
  },
  "scan": {
    "scan_id": "...",
    "generator": "pass1_code_unit",
    "confidence": 0.9
  }
}
```

The JSONB payload should be structured enough for retrieval and rendering, but it should not duplicate the whole graph model.

### 5. What belongs in relational tables vs JSONB

The rule should be:

**Facts needed for graph traversal, impact analysis, authorization, or cross-document integrity belong in relational tables. Presentation and retrieval hints may live in JSONB.**

Relational tables remain authoritative for:

- logical Document identity, Document version identity, type, status, tree placement, and Version Chain
- `document_sources`
- `edges`
- `uncertainties`
- `dispatch_sites`
- `communication_endpoints`
- `flow_groups`
- `flow_traces`
- `flow_trace_steps`
- trace candidate selections and dispatch-site bindings
- `reverse_index`
- `index_entries`
- `document_sections`
- Source Anchoring and citation payload records derived from `code_units`, `document_sources`, and `reverse_index`

JSONB stores:

- document sections
- section headings and section kinds
- rendered section content
- source/reference IDs used inside sections
- retrieval terms
- Pass 1 metadata that is useful for display
- rendered Pass 2 trace summaries that are useful for explanation

Do not rely on JSONB arrays as the only source of truth for `REFERENCES` edges, `GENERATED_FROM` edges, Source Anchoring, flow traces, candidate selections, dispatch-site bindings, endpoint bindings, or unresolved trace items. Those must stay relational.

Schema ownership:

- Section 04 owns `communication_endpoints`, `dispatch_sites`, and `dispatch_site_endpoint_candidates`.
- Section 07 owns document body/version storage, retrieval indexes, and Pass 2 flow-trace persistence tables.
- Section 08 consumes these tables through retrieval services; it should not define new storage truth.

### 6. Rendered Markdown

`content_markdown` should be a deterministic render of `content_jsonb`.

The renderer should:

- order sections consistently
- include citations and source anchors
- include uncertainty notes where relevant
- include dispatch-site or trace ambiguity notes for Business Logic Documents
- avoid inventing graph edges from prose

This gives Vulgata:

- fast full-document reads for the chat agent
- simple display in the document viewer
- stable export content
- a deterministic source for V1 section-level retrieval

If a Worker Agent submits Markdown directly, the persistence layer should still wrap it into a JSONB envelope rather than treating raw Markdown as the only canonical body.

### 7. Pass 2 trace metadata requirements

Section 06 requires BL Documents to preserve path-state semantics.

For Business Logic Documents, relational trace tables should include:

- source flow group ID
- entry point document IDs
- trace IDs used
- trace steps
- selected candidate groups
- unresolved candidate groups
- dispatch-site bindings
- unresolved dispatch sites
- endpoint bindings
- trace termination reasons
- resolution state at generation
- endpoint IDs referenced
- uncertainty IDs referenced
- confidence summary

This metadata is not just decoration. Section 08 needs it so chat can avoid flattening unresolved branches into certain business facts.

`content_jsonb` should include a rendered copy of the same identifiers for display and export, but the database records above remain authoritative.

Recommended BL render copy:

```json
{
  "flow": {
    "flow_group_id": "...",
    "entry_point_document_ids": ["..."],
    "trace_ids": ["..."],
    "selected_candidate_groups": [
      {
        "candidate_group_id": "cg-b-001",
        "selected_document_id": "...",
        "basis": "caller path selected the configured handler"
      }
    ],
    "unresolved_candidate_groups": [],
    "dispatch_bindings": [
      {
        "dispatch_site_id": "ds-b-001",
        "selected_document_id": "...",
        "basis": "DI registration resolved implementation"
      }
    ],
    "unresolved_dispatch_sites": [],
    "endpoint_ids": ["..."],
    "uncertainty_ids": []
  }
}
```

### 7A. Flow-trace persistence schema

V1 should implement the Pass 2 trace evidence as relational tables owned by the document storage model.

Recommended minimum schema:

```text
flow_groups
  id
  scan_id
  repository_id
  business_logic_document_id
  business_logic_document_version_id
  title
  entry_point_document_ids_jsonb
  grouping_score_jsonb
  trace_count
  confidence
  created_at

flow_traces
  id
  flow_group_id
  entry_point_document_id
  entry_point_document_version_id
  confidence_floor
  termination_reason
  resolution_state_jsonb
  created_at

flow_trace_steps
  id
  flow_trace_id
  step_order
  document_id
  document_version_id
  edge_id
  step_kind              -- reference, candidate_selection, dispatch_binding, endpoint_handoff, uncertainty, termination
  condition_text
  confidence
  evidence_jsonb

flow_trace_candidate_selections
  id
  flow_trace_id
  candidate_group_id
  selected_document_id
  selected_document_version_id
  status                 -- selected, unresolved, capped
  basis
  confidence

flow_trace_dispatch_bindings
  id
  flow_trace_id
  dispatch_site_id
  selected_document_id
  selected_document_version_id
  selected_endpoint_id
  status                 -- bound, candidate, unresolved
  basis
  confidence

flow_trace_endpoint_bindings
  id
  flow_trace_id
  communication_endpoint_id
  target_document_id
  target_document_version_id
  status                 -- resolved, unscanned, unauthorized, ambiguous
  basis
  confidence
```

Implementation notes:

- unresolved candidate groups and unresolved dispatch sites are rows with `status = unresolved`, not only JSON arrays
- `resolution_state_jsonb` records generation-time cross-repo and authorization state for retrieval, but queryable IDs stay in the binding tables
- `business_logic_document_version_id` may be null until the BL Document version is committed, then filled in the same transaction or immediate post-commit update
- if a later Cross-Repository Resolution changes a trace target, affected BL Document versions should receive `resolution_outdated_at`

### 8. Versioning model

Generated Documents remain immutable.

The Version Chain should continue to use `superseded_by_id`.

V1 should distinguish two identities:

- **Logical Document identity:** the stable slot addressed by jobs, UI routes, same-scan references, and re-scan matching.
- **Document Version identity:** the immutable generated snapshot stored in `documents` or an equivalent version table.

Pre-allocation should create a logical slot or a planned/provisional version row. A planned row is not a generated Document and must not be exposed as graph truth. Once a Worker Agent result is validated and committed, that version becomes immutable. Any later refinement or re-scan creates a new version, links the prior version through the Version Chain, and updates the logical current pointer.

Edges are versioned facts. A generated edge must point to generated Document versions. During a scan, references may be staged against logical identities, but scan finalization must resolve them to concrete generated versions or downgrade them to uncertainties. When a target version is later superseded, old edges are preserved for history and current retrieval should either follow the logical current version with a stale notice or force regeneration of affected Documents according to the re-scan policy.

Recommended V1 rule:

- store a full JSONB and Markdown snapshot for each generated Document version
- do not mutate old document versions
- do not optimize by storing JSON diffs in V1
- compute diffs between versions on demand or in a derived table later
- include `resolution_outdated_at` and `resolution_outdated_reason` on Document versions or an equivalent stale-notice table for BL Documents whose generation-time cross-repo state no longer matches current graph truth

This preserves auditability and keeps incremental re-scan simple.

JSONB partial updates are useful for future tools, but generated scan output should still prefer append-only replacement through a new Document version.

### 9. Transaction boundaries

The old "write Markdown file first, then insert DB rows" sequence should be replaced.

Recommended write sequence:

1. Worker Agent submits a structured result contract.
2. Persistence service validates the contract.
3. Persistence service renders Markdown from the structured payload.
4. One database transaction writes:
   - `documents`
   - `document_sources`
   - `edges`
   - `uncertainties`
   - `dispatch_sites`
   - `flow_groups`, `flow_traces`, trace steps, and trace bindings when writing Pass 2 output
   - `document_sections`
   - other scan-output rows
5. Post-scan jobs rebuild:
   - `reverse_index`
   - `index_entries`
   - retrieval indexes
6. Export jobs write Markdown files and `index.md` from database state.

This makes the database commit the source of truth and turns filesystem output into a reproducible build artifact.

### 10. `index.md` and `log.md`

`index.md` should be generated from `index_entries` and document metadata.

It should not be the canonical retrieval index.

Recommended rule:

- `index_entries` is canonical for structured search
- `index.md` is an export/readability artifact
- section 08 tools should query `index_entries`, not ask the LLM to read a giant `index.md`

`log.md` should follow the same pattern.

Recommended rule:

- structured scan and query events belong in database tables
- `log.md` is an exported chronological view
- operational chat questions should query structured scan/event tables, not RAG over `log.md`

This keeps FR-4.7 and FR-4.8 compatible with a database-first design.

### 11. Retrieval indexes handed to section 08

Section 07 should not fully decide retrieval strategy, but it should store enough data to support it.

Recommended derived retrieval structures:

- `index_entries` for document-level search and summaries
- `document_sections` for section-level retrieval
- PostgreSQL full-text search vectors over title, summary, key symbols, business terms, technical terms, and section content
- trigram or exact-symbol fallback indexes for Chinese, mixed-language, and identifier-heavy queries
- optional embeddings later through `pgvector`

For V1, the minimum should be:

- document-level `index_entries`
- materialized `document_sections` derived from `content_jsonb`
- `content_markdown` full-document reads for short Documents and display
- relational graph traversal through `edges`

Section 08 assumes section-level retrieval from V1. `document_sections` can still be materialized from `content_jsonb` without changing the canonical document model.

Recommended `document_sections` schema:

```text
document_sections
  id
  document_id
  document_version_id
  section_id
  section_order
  section_kind
  heading
  content_markdown
  token_estimate
  source_anchor_refs_jsonb
  document_refs_jsonb
  uncertainty_refs_jsonb
  search_text
  search_vector
  created_at
```

Build/rebuild rules:

- materialize `document_sections` from `content_jsonb.sections` whenever a Document Version is committed
- rebuild rows for a version by deleting and re-inserting that version's sections in the same transaction or post-commit index job
- `search_vector` covers English and identifier-heavy text where PostgreSQL full-text is useful
- `search_text` plus trigram indexes support Chinese, mixed Chinese/English, and exact symbol fallback
- retrieval should rank section hits first, then fall back to full `content_markdown` for short Documents or continuity-sensitive answers

### 12. Migration stance

There is no large existing corpus of generated scan Documents in the current codebase, so V1 can start directly with PostgreSQL-first storage.

Recommended migration stance:

- do not build a Markdown-to-JSON migration pipeline now
- do not introduce dual authoritative stores
- add schema versioning to `content_jsonb` from the beginning
- if early prototypes produce Markdown-only Documents, wrap them in a minimal JSONB envelope

Minimal envelope:

```json
{
  "schema_version": 1,
  "sections": [
    {
      "id": "body",
      "kind": "body",
      "heading": "Body",
      "content_markdown": "..."
    }
  ]
}
```

### 13. Practical answers to the original five questions

#### Q1 - Filesystem Markdown

Answer:

- keep Markdown exports
- do not use filesystem Markdown as canonical storage
- store canonical document bodies in PostgreSQL

#### Q2 - MongoDB vs PostgreSQL JSONB vs hybrid

Answer:

- use PostgreSQL JSONB plus rendered Markdown in PostgreSQL
- reject MongoDB for V1
- treat filesystem output as export

#### Q3 - Structured document shape

Answer:

- use a versioned JSONB payload with sections, retrieval hints, source refs, document refs, and scan metadata
- keep graph facts relational
- render Markdown deterministically from JSONB

#### Q4 - Migration complexity

Answer:

- no heavy migration is needed because scan Documents are not implemented yet
- if Markdown-only prototypes exist, wrap them in a minimal JSONB envelope

#### Q5 - Versioning

Answer:

- preserve immutable full Document versions through the Version Chain
- distinguish Logical Document identity from immutable Document Version identity
- store full JSONB and Markdown snapshots per version in V1
- defer section-level diff storage until it proves necessary

### 14. What section 07 assumes from earlier sections

Section 07 assumes:

- section 03 keeps graph truth in relational `edges`
- section 05 preserves Source Anchoring through `document_sources` and `reverse_index`
- section 06 requires durable records for flow traces, branch decisions, dispatch bindings, endpoint bindings, and unresolved states
- section 04 owns endpoint and dispatch-site base tables, while section 07 owns Pass 2 trace binding tables

### 15. What section 07 hands to section 08

Section 07 hands these assumptions forward:

- retrieval should query database indexes, not parse `index.md` as the canonical source
- `content_markdown` is available for full-document reads
- `document_sections` is available for section-aware retrieval, materialized from `content_jsonb`
- graph traversal should use relational `edges`
- unresolved dispatch and candidate-branch metadata is available relationally for answer grounding
- BL Document versions expose `resolution_outdated_at` or equivalent stale-notice data when Cross-Repository Resolution changes generation-time assumptions
- `index.md` and `log.md` are exported views, not retrieval sources of truth

### 16. Resolved decisions summary

The completed decisions for section 07 are:

1. PostgreSQL is the canonical Document store.
2. MongoDB is rejected for V1.
3. `content_jsonb` stores structured document content.
4. `content_markdown` stores a deterministic rendered Markdown cache.
5. Filesystem Markdown is export-only.
6. Graph facts remain relational, not embedded only in JSONB.
7. BL Document trace metadata from Pass 2 must be stored structurally.
8. Generated Document versions remain immutable full snapshots in V1.
9. `index.md` and `log.md` are generated views over database state.
10. Section 08 should retrieve from database indexes plus graph traversal, not from filesystem parsing.
11. V1 distinguishes Logical Document identity from immutable Document Version identity.
12. `document_sections` is a V1 retrieval requirement, not an optional later optimization.
13. Section 07 owns the V1 schema for `flow_groups`, `flow_traces`, trace steps, trace bindings, and `document_sections`.
14. BL Document versions need a queryable `resolution_outdated_at` marker or equivalent stale-notice row.

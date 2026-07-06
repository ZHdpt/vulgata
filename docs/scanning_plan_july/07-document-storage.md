# 07 — Document Storage: Filesystem vs. Document Database

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Review pending — extracting from original plan; questions raised below.

---

## TODO — Open Questions from July Review

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
- For the competition demo, filesystem storage is probably fine. The structured document question matters more at V2 scale or if the chat agent struggles with retrieval accuracy. But it's worth designing the schema to make the switch easy (e.g., add a `content_jsonb` column alongside `content_md_path`).

# 08 — Context Retrieval in Chatting

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Review pending — extracting from original plan; questions raised below.

---

## TODO — Open Questions from July Review

> These are concerns raised that need to be addressed before finalizing this section.

1. **Repo selection feature changes the retrieval landscape.** The user can select which repo(s) to ask questions about. This means:
   - The chat agent must scope its document search to the selected repo(s)
   - Cross-repo references from the selected repo should still be traversable
   - The `index.md` (currently per-system) may need to be queryable per-repo

2. **LLM-Wiki retrieval — ensuring the agent finds the right document.** The current retrieval workflow:
   ```
   1. Chat agent reads index.md
   2. Parses YAML frontmatter
   3. Filters by system, doc_type, key_symbols relevance
   4. Selects 3-5 most relevant documents
   5. Reads each document's full markdown body
   6. Follows REFERENCES edges to related documents
   7. Synthesizes answer with citations
   ```
   **Problems with this approach:**
   - **index.md is a single file.** At 200 documents it works. At 2000 documents, the agent can't fit the entire index in context. Pagination or filtering needed.
   - **"Filters by relevance" is vague.** The agent decides relevance heuristically. This is error-prone — it might skip the right document because the `key_symbols` don't match the user's natural language query.
   - **No semantic search.** `key_symbols` is comma-separated text. A user asking "how does the payment flow work" won't match a document whose `key_symbols` is "CheckoutHandler, processPayment, StripeClient" unless the agent is smart about synonym matching.
   - **Step 6 (following edges) is depth-limited.** The agent follows edges manually — it reads doc A, sees edge to doc B, reads doc B. This is slow (multiple tool calls) and depth-limited (agent may stop after 1-2 hops).

3. **Do we need RAG (Retrieval-Augmented Generation) for retrieval?** Options:
   - **Embedding-based semantic search:** Generate embeddings for each document (via an embedding model). Store in PostgreSQL with `pgvector`. User query → embed → find top-K most similar documents by cosine distance. This replaces the `key_symbols` heuristic with mathematical similarity.
   - **Hybrid search:** Combine semantic search (embeddings) with keyword search (full-text on `index_entries.summary`). Rank and merge results.
   - **Graph-enhanced retrieval:** Start with semantic search, then follow REFERENCES edges to expand context. This combines the best of both: find the right document via embeddings, then gather related context via graph traversal.
   - **Agentic retrieval:** The chat agent uses tools: `search_docs(query, repo_id)` → returns top-K, `get_doc(doc_id)` → returns full body, `get_related_docs(doc_id)` → follows edges. The agent iteratively gathers context until it has enough to answer.

4. **Should we index `log.md` for RAG?** `log.md` contains:
   - Scan history (when was each repo scanned, what changed)
   - Query history (what questions were asked, by whom)
   - This is operational metadata, not source code documentation

   **Arguments for indexing log.md:**
   - Users might ask "when was this repo last scanned?" or "what changed in the last scan?"
   - The agent could learn from past queries: "3 people asked about the payment flow — maybe I should pre-load those docs"
   - Scan logs help the agent understand staleness: "I last scanned this repo 2 weeks ago, the answer may be outdated"

   **Arguments against:**
   - `log.md` is append-only text, not structured. Hard to query programmatically.
   - The `scans` table and `cross_repo_stale_notices` already provide structured answers to operational questions.
   - Indexing log.md for RAG adds complexity with marginal benefit at demo scale.

5. **Context window management.** The chat agent has a limited context window. If a user asks a broad question ("explain the entire checkout flow"), the agent might need 20+ documents. Strategies:
   - **Progressive disclosure:** Start with summaries from `index_entries`, let the user ask follow-ups for details
   - **Document summarization:** Pre-generate short summaries (already in `index_entries.summary`) and use those for initial filtering
   - **Hierarchical retrieval:** First retrieve BL Docs (high-level), then drill into CL Docs on demand
   - **Citation-based lazy loading:** Answer with citations, only load cited documents when the user asks for details

6. **Cross-repo retrieval.** When a user selects Repo A and asks a question, the answer may depend on Repo B (cross-repo REFERENCES). Should the agent:
   - Automatically follow cross-repo edges and include Repo B's documents?
   - Tell the user "this also involves Repo B — would you like me to include it?"
   - Only follow cross-repo edges if the user explicitly enables cross-repo mode?

---

## Extracted Content from Original Plan

### LLM-Wiki Retrieval

#### index.md Format (YAML Frontmatter)

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

#### Retrieval Workflow

1. Chat agent reads `index.md`
2. Parses YAML frontmatter for structured fields
3. Filters by `system`, `doc_type`, `key_symbols` relevance to user query
4. Selects 3–5 most relevant documents
5. Reads each document's full markdown body from `content_md_path`
6. Follows `REFERENCES` edges to related documents (cross-repo if applicable)
7. Synthesizes answer with citations to source documents (FR-11.6, FR-11.7)

### `index_entries` table

| Column | Type | Notes |
|--------|------|-------|
| `id` | UUID PK | |
| `document_id` | FK → documents | |
| `doc_type` | ENUM | code_logic, business_logic |
| `system_slug` | TEXT | |
| `repo_slug` | TEXT | |
| `key_symbols` | TEXT | Comma-separated symbol names for search |
| `summary` | TEXT | One-line description for LLM selection |
| `path` | TEXT | Document path in tree |
| `scan_id` | FK → scans | |

This is what gets rendered as YAML-frontmatter `index.md`. The LLM-wiki retrieval pattern: (1) read index.md, (2) parse structured entries, (3) filter by relevance, (4) read 3-5 full documents, (5) follow cross-repo links, (6) synthesize with citations.

### Live Graph Visualization (context — how it connects)

```
PostgreSQL (12 tables)
    ↓ EF Core queries
GraphStateService (singleton, in-memory)
    ↓ SignalR hub push
Blazor.Diagrams (renders DiagramNode + DiagramLink)
```

### `log.md` (FR-4.8) — excerpt

```
## [2026-06-17 14:32] scan:started | repo=risk-service commit=abc123
## [2026-06-17 14:35] scan:completed | repo=risk-service docs=52 edges=118 uncertainties=4
## [2026-06-17 14:40] query:chat | user=lin system=mobile-banking question="how does risk evaluation work?"
```

---

## Notes for Revision

- The current retrieval is "LLM-Wiki" style: the agent reads an index, then follows links. This is essentially Web 1.0 navigation applied to documents. RAG (embeddings + vector search) is the modern approach and would improve retrieval accuracy significantly.
- PostgreSQL with `pgvector` extension enables embeddings without adding infrastructure. This aligns with the "no new services" philosophy of the original plan.
- **Repo-scoped retrieval** is a critical feature. The chat agent needs tooling that accepts `repo_id` as a parameter. The `index_entries` table already has `repo_slug` — this should be used for filtering.
- **Agentic retrieval** (the agent uses tools to search, read, follow edges) is more flexible than pre-computed retrieval pipelines. The agent can dynamically decide how many documents to read, when to follow edges, and when to stop — based on whether it has enough context to answer.
- `log.md` is probably not worth indexing separately. The `scans` table answers operational questions, and query history could be tracked in its own table if needed.

# 08 — Context Retrieval in Chatting

**Parent:** [AnchorDB+ v2 — Winning Document Graph Architecture](../bmad/planning-artifacts/architecture/document-graph-anchordb-plus-plan.md)
**Extracted:** 2026-07-06
**Status:** Section 08 completed - database-backed agentic LLM-wiki retrieval agreed.

---

## Original Open Questions from July Review

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

---

## Finalized Direction for Section 08

### 1. Core decision

V1 should use **database-backed agentic LLM-wiki retrieval**.

That means Vulgata keeps the LLM-wiki idea:

- retrieve a small set of relevant Documents
- read the selected Documents in coherent form
- follow graph relationships when needed
- synthesize an answer with citations

But the Chat Agent should not literally read a large `index.md` file as the canonical search step.

Instead, the Chat Agent should use typed retrieval tools over:

- `index_entries`
- `document_sections`
- `documents.content_markdown`
- `documents.content_jsonb`
- relational `edges`
- `uncertainties`
- `communication_endpoints`
- `flow_groups`, `flow_traces`, trace steps, and trace bindings
- structured scan/event tables

`index.md` remains an exported wiki artifact, not the retrieval source of truth.

### 2. Retrieval architecture

The recommended V1 architecture has three layers.

#### Layer 1 - Deterministic retrieval service

Application code owns:

- authorization filtering
- repo/System scope filtering
- full-text and metadata search
- graph expansion
- confidence filtering
- document budget enforcement
- citation payload construction
- redaction of unauthorized Repository, Document, endpoint, Edge, and Uncertainty details

#### Layer 2 - Chat Agent retrieval tools

The Chat Agent calls typed tools such as:

- `search_documents`
- `get_document`
- `get_document_sections`
- `expand_related_documents`
- `get_uncertainty_summary`
- `get_endpoint_context`
- `get_flow_trace_context`
- `get_source_anchor_context`
- `get_scan_status`

The agent chooses which tools to call and when to stop, but it does not hand-parse indexes or freely query tables.

#### Layer 3 - Answer synthesis

The Chat Agent synthesizes the answer from a bounded evidence packet.

The answer should:

- follow Business Mode or IT Mode rules
- cite Documents and source lines where available
- preserve uncertainty and low-confidence notes
- avoid flattening unresolved branches into certain facts

### 3. V1 search strategy

V1 should use **hybrid structured search without mandatory embeddings**.

Recommended V1 search signals:

- PostgreSQL full-text search over title, summary, key symbols, business terms, technical terms, and rendered content
- materialized `document_sections` search over section text and section kinds
- metadata filters over System, Repository, doc type, status, confidence, scan recency, and access scope
- exact/partial symbol matching for IT Mode
- business-term matching for Business Mode
- endpoint identifier matching for cross-repo communication questions
- graph expansion from high-ranked seed Documents
- trigram, exact-token, and business-term alias fallback for Chinese, mixed Chinese/English, and identifier-heavy queries

Embeddings through `pgvector` should be designed as an optional upgrade path, not required for the first V1 retrieval implementation.

Why:

- PostgreSQL full-text search needs no new provider or embedding model
- generated Documents are structured and summarized by design
- graph expansion carries much of the "semantic" relationship signal
- Chinese and mixed-language matching can start with section search, business-term aliases, exact symbols, and trigram fallback before requiring embeddings
- embeddings add operational cost and provider capability questions
- section 07 already keeps `content_jsonb` and section structure ready for future embedding rows

If retrieval quality is poor with full-text plus graph expansion, `pgvector` can be added later without replacing the retrieval architecture.

Chinese search caveat:

- PostgreSQL built-in full-text search should not be treated as a complete Chinese tokenizer.
- V1 should rely on exact identifier matching, business-term aliases extracted into retrieval metadata, `document_sections.search_text`, and trigram indexes for Chinese or mixed Chinese/English queries.
- A proper tokenizer such as `zhparser`, an application-side tokenizer, or embeddings can be evaluated after the deadline if retrieval quality demands it and the extension is acceptable for the Docker/PostgreSQL deployment.
- The V1 acceptance bar is reliable recall for known business terms, source symbols, endpoint identifiers, and section headings, not general-purpose Chinese semantic search.

### 4. Database-backed LLM-wiki interpretation

The original FR-11.6 LLM-wiki workflow remains valid as a user-facing product behavior, but its implementation should be reinterpreted.

Original phrase:

```text
read index.md -> select 3-5 Documents -> read each -> follow cross-repo links -> synthesize
```

Recommended implementation:

```text
query index_entries/search indexes -> select seed Documents -> read content_markdown or sections -> expand via graph tools -> synthesize
```

This keeps the spirit of LLM-wiki retrieval while avoiding the scaling and reliability problems of placing the whole index in the prompt.

### 5. Scope handling

Retrieval must always run inside an authorization-aware scope.

Inputs:

- current user ID and roles
- selected Systems
- selected Repositories
- mode: Business Mode or IT Mode
- optional uploaded-message context
- user query

Scope rules:

- if the user selects Repositories, seed search starts in those Repositories
- if the user selects Systems, seed search includes their Repositories
- standalone Repositories are included only when explicitly selected or when graph expansion reaches them through authorized edges
- all retrieval results must be filtered by the user's access rights
- cross-repo expansion may read outside the selected scope only through explicit graph relationships and only if the target is authorized

Authorization contract:

- Repository authorization is the outer boundary. In the current management model, Administrators can see all Repositories, while SystemOwners see Repositories through visible Systems; standalone Repository visibility must be treated as an explicit policy choice before chat uses it for cross-repo expansion.
- Document authorization is inherited from the owning Repository unless a later Document-level policy narrows it.
- Edge authorization requires both endpoint Documents to be authorized before the target title, Repository name, endpoint name, or behavior can be disclosed.
- Endpoint authorization follows the owning Repository for provider-side endpoints and the source Repository for consumer-side hints; cross-repo endpoint names are redacted unless the relevant Repository is authorized.
- Uncertainty summaries must be redaction-aware. If the target Repository or endpoint is unauthorized, the summary may say that an external dependency exists only if that fact is allowed by policy; it must not leak restricted Repository names, route names, endpoint names, or business behavior.
- Trace and flow-group records are readable only when every included Document or endpoint detail is authorized or redacted in the returned evidence packet.

If no Systems or Repositories are selected, Vulgata should auto-scope:

1. extract query terms, symbols, business entities, and endpoint-like strings
2. search `index_entries` across all authorized Systems and Repositories
3. rank candidate scopes by match quality and recency
4. pick a small scope, usually top 1-3 Systems or Repositories
5. tell the user which scope was selected

### 6. Mode-aware retrieval

Business Mode and IT Mode should use the same retrieval substrate but different ranking and answer policies.

#### Business Mode

Prefer:

- Business Logic Documents
- BL sections that summarize flow, actors, decisions, business rules, and exceptions
- cross-repo handoff explanations
- high-confidence summaries

Avoid by default:

- raw source-code detail
- deep Code Logic Document chains
- implementation-only helper Documents

Business Mode may still load Code Logic Documents when:

- no suitable BL Document exists
- a BL Document cites a CL Document needed to verify a claim
- the user asks for detail that requires source grounding

#### IT Mode

Prefer:

- Code Logic Documents
- source anchors and file paths
- call chains
- endpoints, dispatch sites, candidate groups, and uncertainties
- supporting Business Logic Documents for broader process context

IT Mode should include code references and source line citations when available.

### 7. Retrieval flow

Recommended V1 flow:

1. Build retrieval scope from user selection or auto-scope.
2. Run `search_documents` against database-backed search indexes.
3. Select seed Documents:
   - 2-4 BL Documents for Business Mode when available
   - 3-5 CL Documents for IT Mode or code-specific questions
4. Load selected Documents:
   - relevant sections from `document_sections`
   - full `content_markdown` only for short Documents or when the answer needs whole-document continuity
5. Expand graph context:
   - outgoing/incoming `REFERENCES`
   - `GENERATED_FROM` from BL to CL when verification is needed
   - cross-repo `REFERENCES`
   - endpoint-linked relationships
   - unresolved uncertainties related to selected Documents
   - flow traces, trace steps, candidate selections, and dispatch-site bindings for selected BL Documents
6. Re-rank and trim the evidence packet.
7. Synthesize the answer with citations.

The default retrieval budget should be small and predictable.

Recommended initial budget:

- 3-5 seed Documents
- up to 5 graph-expanded Documents
- maximum 10 Documents total
- section-level loading by default, with full-document reads only when budget allows and continuity is needed

### 8. Graph expansion policy

Graph expansion should be deterministic and budgeted.

Recommended expansion order:

1. For BL Documents, follow `GENERATED_FROM` to supporting CL Documents when the user needs source grounding.
2. Follow high-confidence `REFERENCES` edges relevant to the query.
3. Follow cross-repo `REFERENCES` when they represent business-relevant handoffs.
4. Read endpoint context when the query asks about integrations or consumers/providers.
5. Include uncertainty summaries when a selected Document has unresolved dependencies.

Graph expansion should avoid:

- low-confidence edges below the retrieval threshold unless the user asks about uncertainty
- utility/helper Documents unless they explain a business decision
- unlimited transitive traversal
- treating dispatch-site hints as resolved edges

Dispatch-site and candidate-branch records from section 06 must be preserved in the evidence packet when they explain why a path was selected, left unresolved, or downgraded.

### 9. Cross-repo retrieval policy

V1 should automatically follow cross-repo `REFERENCES` when all of these are true:

- the target Document is authorized for the current user
- the edge or endpoint match is relevant to the query
- the target fits inside the retrieval budget
- the answer would be materially incomplete without it

The Chat Agent should not stop to ask permission merely because a related Document is in another Repository.

Instead, the answer should disclose scope expansion, for example:

```text
我还参考了 risk-service，因为 checkout-service 的风控调用指向那里。
```

When a cross-repo edge points to an unauthorized or unscanned target:

- do not expose restricted content
- mention only the allowed uncertainty summary
- redact target Repository, endpoint, route, and behavior details when the user is not authorized to see them
- cite the source Document's uncertainty or endpoint record

For unresolved cross-repo links:

- include the uncertainty summary
- state whether the target Repository is known, unknown, unscanned, or ambiguous
- do not fabricate provider behavior

### 10. Context window management

Context control should be handled before synthesis, not left to the final LLM prompt.

Recommended rules:

- prefer summaries first, then full Documents only when needed
- prefer BL Documents for broad business questions
- use section-level retrieval for large Documents
- cap total Document count
- include trace metadata for BL Documents when unresolved branches matter
- deduplicate shared CL Documents across multiple BL Documents
- drop low-value utility Documents first
- preserve citations for every retained claim source

For broad questions, the agent should provide a high-level answer and cite key BL Documents rather than trying to load an entire System.

### 11. `log.md` and operational questions

Do not index `log.md` for RAG in V1.

Operational questions should be answered from structured tables:

- scan history
- scan status
- stale notices
- query history if/when stored
- repository scan timestamps
- document version metadata

`log.md` remains an exported chronological view for humans.

Examples:

- "When was this repo last scanned?" -> query scan table
- "What changed in the last scan?" -> query scan/version metadata
- "Is this answer stale?" -> query scan recency and stale notices

### 12. Retrieval tools

Recommended V1 tool contracts:

#### `search_documents`

Inputs:

- query
- selected system IDs
- selected repository IDs
- mode
- doc type preference
- max results

Returns:

- document ID
- title
- doc type
- System/Repository
- path
- summary
- confidence
- search score
- matched fields
- latest scan metadata

#### `get_document`

Inputs:

- document ID
- include mode: `summary`, `sections`, `full`

Returns:

- metadata
- content Markdown or selected sections
- citation anchors
- source anchors
- uncertainty summary

#### `expand_related_documents`

Inputs:

- seed document IDs
- relation kinds
- max depth
- max results
- include cross-repo boolean

Returns:

- related Documents
- edge metadata
- confidence
- reason for inclusion

#### `get_endpoint_context`

Inputs:

- endpoint ID or endpoint identifier text
- max consumers/providers

Returns:

- provider endpoint metadata
- consumer Documents
- provider Documents where known
- unresolved endpoint-linked uncertainties

#### `get_uncertainty_summary`

Inputs:

- document IDs
- include resolved boolean

Returns:

- unresolved cross-repo links
- unknown targets
- ambiguous targets
- dangling links
- resolution status

#### `get_scan_status`

Inputs:

- System IDs or Repository IDs

Returns:

- last scan time
- scan status
- stale notices
- failed/partial document counts
- unresolved uncertainty counts

### 13. Ranking model

V1 ranking should be explainable and deterministic.

Suggested ranking factors:

- full-text score
- title and symbol exact matches
- business term matches
- selected scope match
- doc type preference by mode
- recency of scan
- confidence
- graph proximity to seed Documents
- endpoint match strength
- unresolved/stale penalty

The retrieval service should return matched fields and inclusion reasons so the Chat Agent can explain why a Document was used when needed.

### 14. Confidence and stale handling

Default retrieval should exclude Documents with confidence below `0.5`, unless the user asks specifically about uncertain or low-confidence areas.

Answers should flag:

- low-confidence sources
- unresolved uncertainties
- stale source Documents
- Cross-Repo Stale Notices
- partial scan gaps
- unscanned target Repositories
- BL Documents whose generation-time resolution state differs from current graph truth
- `resolution_outdated_at` or equivalent stale-notice markers

Do not hide uncertainty from Business Mode. Business Mode should phrase it plainly, not technically.

When retrieval combines stale Document prose with newer graph evidence:

- keep generation-time evidence and query-time evidence separate in the internal evidence packet
- cite the stale BL Document for what it claimed at generation time
- cite the newly resolved target CL/BL Document, endpoint, or Edge for current graph truth
- tell the user plainly that the BL Document was generated before the later Cross-Repository Resolution
- never let a stale BL statement such as "provider unscanned" override a newer authorized target Document

### 15. Section-level retrieval

Section-level retrieval is required for V1 and should be materialized as a derived index, not hand-parsed from JSONB during every chat turn.

Recommended path:

- V1 minimum: query `index_entries` and `document_sections`, then read only relevant sections by default
- V1 fallback: read full `content_markdown` for short Documents or when section retrieval cannot preserve continuity
- V2: add section embeddings if full-text search is not enough

`document_sections` can include:

- document ID
- section ID
- section kind
- heading
- content text or Markdown
- search vector
- token estimate
- citation anchors

### 16. Embeddings and `pgvector`

Do not require embeddings for initial V1.

Design for them by:

- keeping stable document and section IDs
- storing schema versions
- materializing sections
- keeping retrieval services abstracted behind tools

Add `pgvector` when:

- full-text plus graph expansion misses too many relevant Documents
- natural-language business questions frequently fail due to synonym gaps
- the team has a reliable embedding model available inside allowed infrastructure

When embeddings are added, use hybrid ranking rather than replacing structured search.

### 17. Citation policy

Every answer should cite the Documents used for substantive claims.

IT Mode citations should prefer:

- Code Logic Documents
- Source Anchoring payloads with commit, file path, Code Unit ID, line range, and snippet or snippet hash
- endpoint records where relevant

Business Mode citations should prefer:

- Business Logic Documents
- concise source Document references
- uncertainty summaries when a claim is incomplete

If a claim comes from graph traversal rather than prose, cite the source and target Documents and include the Edge reason in the internal evidence packet.

Citation payloads should be built from Source Anchoring records, not ad hoc JSONB strings. The payload should include enough identity to survive file renames and line drift:

- repository ID and scanned commit
- file path at scan time
- Code Unit ID
- line range
- source hash or snippet hash
- Document version ID that made the claim

### 18. Practical answers to the original six questions

#### Q1 - Repo-scoped retrieval

Answer:

- retrieval is always scope-aware and authorization-aware
- selected Repositories constrain seed search
- cross-repo graph expansion may include authorized related Repositories when relevant

#### Q2 - LLM-wiki limitations

Answer:

- keep LLM-wiki as the product pattern
- implement it through database-backed tools, not by reading `index.md`
- use `index_entries`, full-text search, structured Documents, and graph expansion
- use `document_sections` for default evidence loading instead of relying on whole-document reads

#### Q3 - RAG and embeddings

Answer:

- do not require embeddings for initial V1
- use PostgreSQL full-text search, section search, exact symbol matching, business-term aliases, trigram fallback, and graph-enhanced retrieval first
- keep `pgvector` as an upgrade path

#### Q4 - `log.md` indexing

Answer:

- do not index `log.md` for RAG
- answer operational questions from structured scan/event tables
- keep `log.md` as an exported human-readable timeline

#### Q5 - Context window management

Answer:

- use budgeted retrieval
- prefer summaries and BL Documents for broad questions
- load sections by default and full Documents only when needed
- cap graph expansion

#### Q6 - Cross-repo retrieval

Answer:

- automatically follow authorized, relevant cross-repo `REFERENCES` within budget
- disclose cross-repo expansion in the answer
- represent unresolved or unauthorized targets as uncertainty, not invented content
- redact unauthorized Repository, endpoint, route, Edge, and behavior details

### 19. What section 08 assumes from earlier sections

Section 08 assumes:

- section 04 provides `communication_endpoints` and endpoint-aware cross-repo relationships
- section 06 stores BL trace, branch, dispatch, endpoint, and unresolved evidence as durable records
- section 07 stores canonical Documents in PostgreSQL with `content_jsonb`, `content_markdown`, `document_sections`, `resolution_outdated_at` or equivalent stale markers, and database-backed indexes

### 20. What section 08 hands to implementation

Section 08 hands these assumptions forward:

- retrieval should be implemented as application services and typed Chat Agent tools
- `index.md` and `log.md` remain exports, not canonical retrieval inputs
- V1 retrieval should start with PostgreSQL full-text/metadata search, section search, language/symbol fallbacks, and graph expansion
- Chinese and mixed-language retrieval should use trigram/exact/business-term fallback in V1 rather than depend on PostgreSQL built-in tokenization alone
- embeddings are optional and should be hidden behind the retrieval service
- cross-repo expansion is automatic when authorized, relevant, and within budget
- answer synthesis must preserve uncertainty, stale notices, and dispatch/candidate ambiguity
- stale BL prose must be separated from newer query-time graph evidence

### 21. Resolved decisions summary

The completed decisions for section 08 are:

1. V1 uses database-backed agentic LLM-wiki retrieval.
2. The Chat Agent uses typed retrieval tools rather than reading `index.md`.
3. Seed search is scope-aware, authorization-aware, and mode-aware.
4. V1 search starts with PostgreSQL full-text, section search, metadata ranking, language/symbol fallbacks, and graph expansion.
5. Embeddings and `pgvector` are optional upgrade paths, not initial requirements.
6. Cross-repo `REFERENCES` are followed automatically when authorized, relevant, and budgeted.
7. `log.md` is not indexed for RAG; operational questions use structured tables.
8. Context windows are managed by budgets, section loading, and graph expansion caps.
9. Citations must link claims to Documents and source anchors where available.
10. Uncertainty, stale notices, dispatch-site ambiguity, and candidate branches must remain visible in answers.
11. Repository, Document, Edge, endpoint, trace, and Uncertainty details are redacted unless authorized.
12. `document_sections` is part of V1 retrieval, while full-document reads are a fallback for short or continuity-sensitive Documents.
13. Chinese retrieval does not depend on built-in PostgreSQL tokenization alone; V1 uses exact, alias, section, and trigram fallback.

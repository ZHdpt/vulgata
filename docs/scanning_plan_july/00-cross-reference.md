# Cross-Reference Analysis — Scanning Plan July

**Date:** 2026-07-07
**Scope:** Cross-referencing all 8 question documents under `docs/scanning_plan_july/`

---

## Dependency Matrix

Each cell shows how a decision in the row-document affects or constrains questions in the column-document.

|  | 01-PreScan | 02-Orch | 03-Graph | 04-XRepo | 05-CG | 06-Pass2 | 07-Storage | 08-Retrieval |
|--|-----------|---------|---------|---------|-------|---------|-----------|-------------|
| **01** | — | ⚡⚡⚡ | ○ | ⚡⚡ | ⚡ | ⚡⚡ | ○ | ○ |
| **02** | ⚡⚡⚡ | — | ⚡⚡ | ⚡ | ⚡⚡⚡ | ⚡⚡⚡ | ○ | ⚡ |
| **03** | ○ | ○ | — | ⚡⚡⚡ | ⚡⚡ | ⚡⚡ | ○ | ⚡ |
| **04** | ⚡ | ○ | ⚡⚡ | — | ⚡ | ○ | ○ | ⚡⚡ |
| **05** | ⚡⚡ | ⚡⚡ | ⚡⚡ | ⚡ | — | ⚡⚡ | ○ | ⚡ |
| **06** | ⚡⚡ | ⚡⚡ | ⚡⚡ | ○ | ⚡ | — | ○ | ⚡ |
| **07** | ○ | ○ | ○ | ○ | ○ | ○ | — | ⚡⚡⚡ |
| **08** | ○ | ○ | ○ | ⚡ | ⚡ | ○ | ⚡⚡ | — |

> ⚡⚡⚡ = Critical dependency — decision in row MUST be resolved before column can be finalized  
> ⚡⚡ = Strong dependency — decision in row significantly shapes options in column  
> ⚡ = Notable dependency — decision in row affects edge cases or specific questions in column  
> ○ = No meaningful dependency

---

## Critical Chains (Decisions That Cascade)

### Chain 1: Orchestrator Design → Everything Else

**02 (Agent Orchestrator) is the hub.** The decision between "LLM magnet orchestrator" and "non-LLM workflow orchestrator" radiates to:

| Affected Document | Specific Question Affected | Why |
|------------------|---------------------------|-----|
| **01 Q2** | Single agent for pre-scanning? | If orchestrator is non-LLM, pre-scanning becomes deterministic tooling, not an agent task |
| **01 Q1** | What pre-scanning must produce? | Non-LLM orchestrator calls CodeGraph directly, owns `code_units` production |
| **02 Q3** | Responsibility table | This IS the question — the table defines the split |
| **05 Q1** | CodeGraph's role in pipeline | Non-LLM orchestrator owns CodeGraph indexing; workers use it as a tool |
| **05 Q2** | CodeGraph as worker tool | Tool-based agent interface (02 Q4) enables `find_callers`/`find_callees` as worker tools |
| **05 Q5** | Performance/caching | Non-LLM orchestrator manages a single CodeGraph index instance per repo |
| **06 Q5** | Pass 2 dispatch granularity | Workflow engine can deterministically group entry points, dispatch multiple workers |
| **06 Q1** | Entry point recording in Pass 1 | Workflow engine collects entry points from worker outputs; no LLM coordination needed |

**Recommendation:** Resolve 02 first. It's the architectural keystone.

---

### Chain 2: Edge Types + Communication Endpoints → Cross-Repo Resolution

**03 (Document Graph) and 04 (Cross-Repo Communication) are deeply intertwined.**

| 03 Question | 04 Question | Relationship |
|------------|------------|-------------|
| 03 Q2 — RPC proxy → endpoint mapping | 04 Q1 — `communication_endpoints` table | The `communication_endpoints` table IS the solution to 03 Q2. A proxy call to `riskClient.evaluate()` maps to an `communication_endpoints` row with `identifier = "risk-service:RiskService.evaluate"`. The agent doesn't need to guess — it queries the table. |
| 03 Q1 — Dynamic references (virtual methods) | 04 Q2 — Agent endpoint discovery | When an agent encounters `processor.Process()` where `processor` is an interface, it can look up `communication_endpoints` for the interface → implementation mapping, just like RPC proxy → endpoint. |
| 03 Q5 — New edge types (PUBLISHES/SUBSCRIBES) | 04 Q3 — Endpoint discovery (message queues) | Event-driven architectures need both: a `PUBLISHES`/`SUBSCRIBES` edge type AND a `communication_endpoints` row with `protocol = 'mq'` for the topic/queue. |
| 03 Q4 — Edge confidence decomposition | 04 Q5 — Normalization rules | If normalization is deterministic (not prose-based), edges matched through normalization have higher confidence than edges matched by string similarity. Confidence becomes: `structural` (CodeGraph-verified) > `endpoint_matched` (normalized) > `heuristic` (string similarity). |

**Recommendation:** Design 03 and 04 together. The `communication_endpoints` table is the bridge between "ambiguous proxy call" and "resolved graph edge."

---

### Chain 3: Document Storage → Context Retrieval

**07 (Storage) decisions dictate what 08 (Retrieval) can do.**

| 07 Question | 08 Question | Relationship |
|------------|------------|-------------|
| 07 Q2 — Filesystem vs JSONB vs MongoDB | 08 Q2 — LLM-wiki retrieval | Filesystem = read entire markdown file. JSONB = query sections by path (`$.sections[?(@.heading == "Database Interactions")]`). This fundamentally changes how the chat agent gathers context. |
| 07 Q3 — Structured document sections | 08 Q5 — Context window management | Section-level retrieval means the agent loads 3 sections of 200 words each instead of 3 documents of 2000 words each. Context window management becomes trivial. |
| 07 Q3 — Structured reference arrays | 08 Q6 — Cross-repo retrieval | References stored as arrays in JSONB (`"references": ["doc-456"]`) can be resolved by the retrieval engine in one query (JOIN + JSONB expansion), instead of the agent parsing markdown prose to find links. |
| 07 Q5 — Structured version diffing | 08 Q4 — log.md indexing | If documents support diff-based versions, the retrieval agent can ask "what changed in the RiskEvaluator since last scan?" and get a structured diff, not just "here's the new version, figure it out." |

**Recommendation:** Decide 07 before 08. Retrieval strategy is downstream of storage format.

---

### Chain 4: CodeGraph Usage → Edge Accuracy → Pass 2 Quality

**05 (CodeGraph) determines the quality floor for 03 (edges) and 06 (Pass 2).**

| 05 Question | Affected Area | Relationship |
|------------|--------------|-------------|
| 05 Q2 — CodeGraph as worker tool | 03 Q1 — Dynamic references | If workers can call `find_callees(symbol)`, they get a deterministic list of static calls. The LLM's job becomes: (a) identify which of these are dynamic, (b) tag them with lower confidence, (c) create uncertainties for the ambiguous ones. Without CodeGraph as a tool, the LLM reads source and guesses — producing both false positives and false negatives. |
| 05 Q3 — Traversal depth | 06 Q2 — Invocation chain traversal | If CodeGraph provides pre-computed call paths up to depth N, Pass 2 workflow can deterministically trace from entry point to termination point. No LLM needed for the traversal — only for the narrative. |
| 05 Q4 — Cross-file references | 03 Q1 — DI/virtual method resolution | The leap from `@Autowired PaymentProcessor processor` to the concrete `StripePaymentProcessor` class requires cross-file symbol resolution. CodeGraph can do this deterministically; an LLM reading only the current file cannot. |
| 05 Q6 — CodeGraph vs tree-sitter | 01 Q1 — Pre-scanning output | If CodeGraph MCP tools are used, pre-scanning produces `code_units` + call graph + symbol index in one pass. If only tree-sitter, pre-scanning produces only `code_units` — call relationships must be discovered by workers or not at all. |

**Recommendation:** Resolve 05 Q6 (CodeGraph MCP vs direct tree-sitter) first. This determines whether workers have call-graph tools at all, which cascades into 03 and 06.

---

### Chain 5: Pre-Scanning Interview → Pass 1 Entry Points → Pass 2 Flow Discovery

**01 (Pre-Scanning), 05 (CodeGraph), and 06 (Pass 2) form a pipeline dependency.**

```
01 Q3 (System Owner tells us entry points)
    +
05 Q2 (CodeGraph detects entry-point annotations like @Controller, @KafkaListener)
    ↓
06 Q1 (Entry points recorded during Pass 1 in metadata_json)
    ↓
06 Q2 (Pass 2 traces invocation chain from each entry point)
    ↓
06 Q3 (Overlapping flows resolved algorithmically)
```

**Key insight:** If we can identify entry points during pre-scanning + Pass 1 (from System Owner interview AND CodeGraph annotation detection), the Pass 2 "one worker per system" bottleneck disappears. The workflow engine can:
1. Collect all entry points
2. Trace REFERENCES transitively from each
3. Cluster overlapping traces into flow groups
4. Dispatch one worker per flow group

This eliminates the circular dependency that forced the original "one worker per system" design.

---

## Question-to-Question Cross-Reference Table

Every question in every document, mapped to related questions in other documents.

| Doc.Question | Short Title | Related Questions | Nature of Dependency |
|-------------|------------|-------------------|---------------------|
| 01.Q1 | Goals of pre-scanning | 02.Q3, 05.Q1, 05.Q6 | What pre-scanning produces depends on who runs it and what tools they have |
| 01.Q2 | Single agent approach | 02.Q1, 02.Q2, 02.Q3 | "Single agent" is undefined until orchestrator design is decided |
| 01.Q3 | System Owner interview | 04.Q4, 06.Q1, 04.Q2 | Owner tells us entry points & comm patterns that agents will later discover/confirm |
| 01.Q4 | One-shot vs iterative | 02.Q2 | Iterative interview needs an agent; one-shot could be a form. Orchestrator design affects this |
| 01.Q5 | Auto-detect comm patterns | 04.Q2, 04.Q4, 05.Q2 | Auto-detection = agents use CodeGraph to find annotations. Complements/replaces owner config |
| 02.Q1 | Magnet vs workflow | 01.Q1, 01.Q2, 05.Q1, 06.Q5 | **Architectural keystone.** Affects who does everything in the pipeline |
| 02.Q2 | Tool-based dispatch | 05.Q2, 05.Q5, 03.Q1 | Tool interface design depends on what tools CodeGraph exposes to workers |
| 02.Q3 | Responsibility table | ALL other docs | Defines LLM vs non-LLM boundary for every pipeline step |
| 02.Q4 | Tool interface design | 05.Q2, 03.Q1, 04.Q2 | Worker tools must align with what CodeGraph, edges, and endpoints provide |
| 02.Q5 | Superstep grouping | 05.Q2, 05.Q4 | Call-graph-based grouping needs CodeGraph cross-file resolution |
| 03.Q1 | Dynamic references | 04.Q2, 05.Q2, 05.Q4, 06.Q2, 06.Q4 | Dynamic dispatch affects Pass 1 edge creation, Pass 2 traversal, and frontend handling |
| 03.Q2 | RPC proxy mapping | 04.Q1, 04.Q2, 04.Q3 | The `communication_endpoints` table IS the answer to "how does the agent map proxy→endpoint" |
| 03.Q3 | GENERATED_FROM semantics | 06.Q3, 06.Q2 | Direction and multiplicity of provenance edges depends on how Pass 2 groups flows |
| 03.Q4 | Edge confidence decomposition | 04.Q5, 05.Q2 | Confidence dimensions: structural (CodeGraph) > endpoint-matched (normalized) > heuristic (LLM) |
| 03.Q5 | New edge types | 04.Q3, 06.Q4 | PUBLISHES/SUBSCRIBES needs mq endpoints; OVERRIDES needs inheritance resolution; page_navigation needs frontend Pass 2 |
| 04.Q1 | communication_endpoints table | 03.Q2, 03.Q1, 08.Q1, 08.Q6 | Canonical endpoint entity bridges graph edges, uncertainties, and chat retrieval |
| 04.Q2 | Agent endpoint discovery | 01.Q5, 05.Q2, 03.Q1, 03.Q2 | Agents need CodeGraph to detect annotations; discovery feeds into communication_endpoints |
| 04.Q3 | Endpoint discovery sources | 03.Q5, 05.Q2 | Each protocol (HTTP, gRPC, MQ, etc.) needs different CodeGraph detection patterns |
| 04.Q4 | Replace vs complement comm_patterns | 01.Q3, 01.Q5 | If agents auto-discover, System Owner role shifts from "define" to "review" |
| 04.Q5 | Normalization rules | 03.Q4 | Deterministic normalization → higher confidence edges; prose-based → lower |
| 05.Q1 | CodeGraph role in pipeline | 01.Q1, 02.Q3, 06.Q1 | Current: pre-scan only. Proposed: also during Pass 1 as worker tool |
| 05.Q2 | CodeGraph as worker tool | 02.Q2, 02.Q4, 03.Q1, 03.Q2, 04.Q2, 04.Q3 | Enables deterministic call tracing, annotation detection, cross-file resolution |
| 05.Q3 | Traversal depth | 03.Q1, 06.Q2 | How deep CodeGraph traces affects both edge creation (Pass 1) and flow tracing (Pass 2) |
| 05.Q4 | Cross-file references | 03.Q1, 02.Q5 | Needed for DI resolution, superstep grouping, virtual method → implementation mapping |
| 05.Q5 | Performance/caching | 02.Q2, 02.Q5 | Who runs CodeGraph index, when, and how workers access it |
| 05.Q6 | CodeGraph vs tree-sitter | 01.Q1, 02.Q3 | Determines whether workers have call-graph tools at all |
| 06.Q1 | Entry points in Pass 1 | 01.Q3, 05.Q1, 02.Q3, 03.Q1 | Entry point detection = System Owner input + CodeGraph annotation scanning + worker tagging |
| 06.Q2 | Invocation chain traversal | 05.Q3, 03.Q1, 03.Q3 | Traversal depth and dynamic dispatch handling determine flow completeness |
| 06.Q3 | Overlapping flows | 03.Q3, 03.Q5 | Shared CL Docs → multiple GENERATED_FROM edges or a shared sub-flow BL Doc |
| 06.Q4 | Frontend approach | 03.Q1, 03.Q5, 04.Q3, 05.Q2 | Frontend needs: different entry point concept, page_navigation edges, component-level docs |
| 06.Q5 | Pass 2 dispatch granularity | 02.Q3, 02.Q1 | Non-LLM orchestrator → deterministic grouping → parallel workers per flow group |
| 07.Q1 | Filesystem markdown | 08.Q2, 08.Q5 | Current approach; retrieval loads entire files |
| 07.Q2 | MongoDB vs JSONB vs filesystem | 08.Q2, 08.Q3, 08.Q5 | JSONB = same-DB structured storage; MongoDB = new infrastructure; filesystem = current |
| 07.Q3 | Structured document design | 08.Q2, 08.Q5, 08.Q6 | Section-level retrieval, structured references, context window efficiency |
| 07.Q4 | Migration complexity | — | No cross-doc dependency; internal trade-off |
| 07.Q5 | Structured versioning | 08.Q4, 06.Q2 | Diff-based versions enable "what changed?" queries |
| 08.Q1 | Repo-scoped retrieval | 04.Q1, 06.Q4 | `communication_endpoints` helps cross-repo linking; frontend needs page_navigation scoping |
| 08.Q2 | LLM-wiki limitations | 07.Q1, 07.Q2, 07.Q3, 05.Q2 | index.md approach is fragile; structured docs + embeddings improve accuracy |
| 08.Q3 | RAG / embeddings | 07.Q2, 07.Q3 | JSONB + pgvector in same PostgreSQL = no new infrastructure |
| 08.Q4 | log.md indexing | 07.Q5 | Structured versioning in scans table makes log.md indexing unnecessary |
| 08.Q5 | Context window management | 07.Q3, 03.Q5, 06.Q3 | Section-level loading + hierarchical retrieval (BL→CL) reduces token usage |
| 08.Q6 | Cross-repo retrieval | 04.Q1, 03.Q1 | `communication_endpoints` enables follow-cross-repo-edges tool for chat agent |

---

## Contradictions & Tensions

These are places where two documents propose approaches that may conflict.

### Tension 1: "LLM decides" vs "deterministic algorithm"

| Doc | Position | Doc | Position |
|-----|---------|-----|---------|
| **02 (current plan)** | LLM orchestrator decides superstep grouping, dispatch ordering, cross-verification coordination | **02 (proposed)** | Deterministic workflow engine does all of the above |
| **03 (current plan)** | Workers read source and LLM-decide what edges to create | **05 (proposed)** | CodeGraph provides deterministic call graph; workers annotate with meaning |
| **06 (current plan)** | One LLM worker identifies flows autonomously | **06 (proposed)** | Workflow traces REFERENCES deterministically; LLM narrates |

**Resolution needed:** For each decision point, is the LLM adding value (interpreting meaning, generating prose) or adding risk (guessing structure that CodeGraph can determine)? The proposed approach: deterministic for structure, LLM for semantics.

### Tension 2: `communication_patterns` (System Owner) vs `communication_endpoints` (agent-discovered)

| Doc | Position |
|-----|---------|
| **04 Q4** | Two options: replace `communication_patterns` entirely, or keep both and reconcile |
| **01 Q5** | Auto-detect patterns from code and present for approval |
| **01 Q3** | System Owner pre-configures patterns during interview |

**Resolution needed:** If agents can discover endpoints reliably (using CodeGraph + framework knowledge), `communication_patterns` becomes a review/override layer. If agents are unreliable, System Owner pre-configuration is essential and auto-detection is a nice-to-have. The answer depends on 05 Q2 (CodeGraph as worker tool).

### Tension 3: One Pass 2 worker vs many

| Doc | Position |
|-----|---------|
| **06 (current plan)** | One worker per system — forced by circular dependency (flows unknown before analysis) |
| **06 Q1 + Q2 (proposed)** | Entry points tagged in Pass 1 → deterministic flow tracing → one worker per flow group |
| **02 Q3** | If orchestrator is non-LLM workflow, it can deterministically group and dispatch |

**Resolution path:** 06 Q1 (entry points in Pass 1) breaks the circular dependency. If resolved, 06 Q5 (dispatch granularity) shifts from "one worker" to "one per flow group." 02 Q3 determines whether the dispatcher is an LLM or a workflow engine, which affects how sophisticated the grouping can be.

### Tension 4: Filesystem simplicity vs structured retrieval power

| Doc | Position |
|-----|---------|
| **07 Q1** | Filesystem: simple, git-friendly, LLM-native. Good enough for demo |
| **07 Q2-Q3** | JSONB: structured, queryable, ACID, no new infra. Better for retrieval |
| **08 Q2** | Current LLM-wiki retrieval is fragile; needs semantic search |
| **08 Q3** | RAG + pgvector works naturally with JSONB documents |

**Resolution path:** This is a V1 vs V2 decision more than a contradiction. V1 can use filesystem + index.md. But the `documents` table should add a `content_jsonb` column from the start (nullable in V1) so the migration path exists. The key insight: 08 Q3 (RAG with pgvector) is much easier if documents are already in PostgreSQL (JSONB) rather than on the filesystem.

---

## Suggested Resolution Order

Based on the dependency analysis, here's the recommended order to resolve these questions:

1. **02 — Agent Orchestrator Design** (Architectural keystone)
   - Q1: Magnet vs workflow orchestrator → determines everything below
   - Q3: Responsibility table → defines LLM vs non-LLM boundary
   - Q5: Superstep grouping algorithm

2. **05 — CodeGraph During Pass 1** (Tool capability foundation)
   - Q6: CodeGraph MCP vs direct tree-sitter → determines available tools
   - Q1-Q2: CodeGraph's role in pipeline and as worker tool
   - Q3-Q4: Traversal depth and cross-file resolution

3. **01 — Pre-Scanning** (Depends on 02 + 05)
   - Q1: Goals (what CodeGraph produces + what owner provides)
   - Q2-Q3: Interview strategy (depends on orchestrator design)
   - Q5: Auto-detect comm patterns (depends on CodeGraph tooling)

4. **04 — Cross-Repo Communication Endpoints** (Depends on 03 + 05)
   - Q1: `communication_endpoints` table design
   - Q2-Q3: Agent endpoint discovery (depends on CodeGraph tools)
   - Q4-Q5: Relationship to `communication_patterns` and normalization

5. **03 — Document Graph Design** (Depends on 04 + 05)
   - Q1-Q2: Dynamic references and RPC mapping (solved by 04 + 05)
   - Q3-Q5: GENERATED_FROM, confidence, new edge types

6. **06 — Pass 2 Scanning** (Depends on 01 + 02 + 03 + 05)
   - Q1: Entry points (depends on 01 + 05)
   - Q2: Invocation chain (depends on 03 + 05)
   - Q3-Q5: Flows, frontend, dispatch (depends on 02 + 03)

7. **07 — Document Storage** (Depends on 08 requirements)
   - Q1-Q5: Format decision (informs 08 but also needs 08's requirements)

8. **08 — Context Retrieval** (Depends on 04 + 07)
   - Q1-Q6: Retrieval strategy (depends on storage format + endpoint table)

> **Note:** 07 and 08 have a mutual dependency. 07's storage format enables certain retrieval strategies (08), but 08's retrieval requirements justify 07's complexity. In practice: decide the retrieval UX first (08), then choose storage to support it (07).

---

## Summary Statistics

- **Total questions across all docs:** 39
- **Questions with cross-doc dependencies:** 35 (90%)
- **Questions with no cross-doc dependencies:** 4 (07.Q4 migration complexity is the clearest standalone)
- **Critical chains:** 5 (Orchestrator, Edge+Endpoint, Storage+Retrieval, CodeGraph→Pass2, PreScan→Pass2)
- **Tensions requiring resolution:** 4 (LLM vs deterministic, comm_patterns vs comm_endpoints, one vs many Pass 2 workers, filesystem vs structured)
- **Documents that should be designed together:** 03+04 (edges+endpoints), 07+08 (storage+retrieval), 01+05+06 (pre-scan + CodeGraph + Pass 2 pipeline)
